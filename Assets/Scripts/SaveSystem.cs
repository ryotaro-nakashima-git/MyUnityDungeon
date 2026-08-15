using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 💾 セーブ / ロード（Phase E-19）。**1プレイ数時間なのに中断できない**のは製品として致命的だった。
///
/// ## なぜ「各systemに ToJson/FromJson を書く」方式にしなかったか
/// 保存が要る状態は 20 以上のクラスに散っている（[[game-polish-plan]] のセーブ設計メモ）。
/// 手書きだと 1,500 行を超えるうえ、**フィールドを1つ足すたびに書き忘れて壊れる**。
/// そこで **静的フィールドをリフレクションで丸ごと写し取る**方式にした。
///
/// ## 保存する / しないの決まり（⚠ 新しい state を足すときはここを守る）
/// | 書き方 | 扱い |
/// |---|---|
/// | `private static List&lt;Kin&gt; all;` | **保存される**（ふつうの状態） |
/// | `private static readonly PolicyDef[] policies = {...}` | **保存されない** ＝ **カタログの目印**。
///   古いセーブが新しいバランス調整を上書きしてしまう事故を、この1語で防いでいる |
/// | `const` / `[NonSerialized]` / `UnityEngine.Object` 由来の型 | 保存されない |
///
/// ## ファイルの形（自己記述型）
/// 先頭に**型表**（フィールド名と型の一覧）を書くので、読む側は**ファイル自身の情報だけで復号できる**。
/// つまり後からフィールドやシステムを足しても、**古いセーブが読める**（無い物は捨て、増えた物は既定値のまま）。
/// 本体は GZip で圧縮する（地上4,500タイル込みで 1MB → 100KB 程度）。
///
/// ## 制約
/// - **準備フェーズでのみ保存できる**。戦闘中は冒険者・防衛体が場に居て、
///   それを保存すると「戦闘の途中から再開」を作り込む必要が出る（Civ も1手ごとの保存）。
/// - 迷宮の見た目は保存しない。`FloorData`（地形＋配置記録）から**組み直す**。
/// 関連: [[GameSetup]] [[DungeonFloorManager]] [[ui-conventions]]。
/// </summary>
public static class SaveSystem
{
    public const int Format = 1;
    public const int SlotCount = 3;          // 手動スロット 1..3（0 はオートセーブ）

    /// <summary>ロードが終わってUIを組み直したいとき用。GameUIManager が差し込む（event ではなく代入式＝作り直しで漏れない）。</summary>
    public static Action Loaded;

    /// <summary>この世界を遊んだ実時間（秒）。⚠ 演出と同じく unscaled で数える（倍速に引っ張られない）。</summary>
    public static float PlaySeconds;
    public static void TickPlayTime(float unscaledDt)
    {
        if (GameSetup.Started) PlaySeconds += unscaledDt;
    }

    public static string Dir { get { return Path.Combine(Application.persistentDataPath, "saves"); } }
    public static string FileOf(int slot)
    {
        return Path.Combine(Dir, slot <= 0 ? "auto.sav" : ("slot" + slot + ".sav"));
    }

    // ============================================================
    // 保存対象
    // ============================================================
    private static readonly Type[] StaticTypes =
    {
        typeof(GameSetup),
        typeof(SurfaceMap), typeof(SettlementSystem), typeof(KinRoster), typeof(ScoutSystem),
        typeof(EnemyForce), typeof(DiplomacySystem), typeof(RivalLords), typeof(EraSystem),
        typeof(PolicySystem), typeof(AttributeSystem), typeof(ResearchState), typeof(EurekaTracker),
        typeof(MinionRoster), typeof(MinionEvolution), typeof(TrainingSystem), typeof(NarrativeSystem),
        typeof(DiscoverySystem), typeof(GuideSystem), typeof(NotifySystem), typeof(LureEconomy),
        typeof(VictorySystem), typeof(RelicManager), typeof(EmotionTreeManager), typeof(DemonLordRaceTree),
        typeof(LordStance),   // 👑 構え（鎮座/親征）・立つ階・捕食値・喰らいの段
        typeof(MutationSystem),   // 🧬 世界の変異（現れた種類と、それぞれが現れたターン）
        typeof(MerchantShop), typeof(AccessoryInventory),   // 🛒💍 行商人の品揃えと装飾品の手持ち
        // 🔭🛡️ 次の波の名簿と、張ってある備え。
        //   ⚠ 名簿を保存しないと**ロード後に引き直され、予告した波と違う波が来る**（予告が嘘になる）。
        typeof(WaveRoster), typeof(WardSystem),
        typeof(Excavation),   // ⛏️ このターンに使った工事の回数（地形そのものは FloorData 側に載る）
        typeof(IncidentSystem),   // ⚡ 答え待ちの異変と、そのターン限りの効果
        // 📊 この周の記録。⚠ [[Achievements]] は入れない（PlayerPrefs側＝周を越える持ち物なので、
        //    セーブに含めると別の周の解除状況で上書きされる）。
        typeof(RunStats),
    };

    // シーンに1つだけ居る側（＝インスタンスのフィールド）。
    // ⚠ `RelicManager` / `EmotionTreeManager` は**静的と実体の両方**に状態を持つので両方の表に載せる。
    // ⚠ `DemonLord` は迷宮を組み直すと作り直されるので、この表には載せるが**復元は最後**にやる。
    private static readonly Type[] BehaviourTypes =
    {
        typeof(DungeonResourceManager), typeof(DungeonTurnManager), typeof(DungeonUpgradeManager),
        typeof(DungeonFeatureManager), typeof(DungeonFloorManager),
        typeof(RelicManager), typeof(EmotionTreeManager), typeof(DemonLord),
    };

    /// <summary>
    /// 🪝 単純な写し取りでは足りないクラス用の逃げ道。
    /// 例：`EmotionTreeManager` はノードの中に `Func&lt;bool&gt;`（＝保存できない）と解放フラグが同居しているので、
    /// 解放フラグだけを別の入れ物に移してから保存する。
    /// </summary>
    public interface ISaveHook
    {
        void OnBeforeSave();
        void OnAfterLoad();
    }

    private static string KeyOf(Type t, bool staticSide) { return staticSide ? t.FullName : (t.FullName + "@"); }

    // ============================================================
    // 見出し（ロード画面用。中身を全部読まずに1行で出す）
    // ============================================================
    public struct Slot
    {
        public bool exists;
        public int format, turn, dp, floors, owned;
        public string era, savedAt;
        public float playSeconds;
        public string error;
    }

    public static Slot Peek(int slot)
    {
        var s = new Slot();
        try
        {
            string p = FileOf(slot);
            if (!File.Exists(p)) return s;
            using (var fs = File.OpenRead(p))
            using (var r = new BinaryReader(fs))
            {
                if (new string(r.ReadChars(4)) != "DBRS") { s.error = "形式が違う"; return s; }
                s.format = r.ReadInt32();
                s.turn = r.ReadInt32(); s.dp = r.ReadInt32(); s.floors = r.ReadInt32(); s.owned = r.ReadInt32();
                s.era = r.ReadString(); s.savedAt = r.ReadString(); s.playSeconds = r.ReadSingle();
                s.exists = true;
            }
        }
        catch (Exception e) { s.error = e.Message; }
        return s;
    }

    public static string PlayTimeText(float sec)
    {
        int t = Mathf.Max(0, Mathf.RoundToInt(sec));
        return (t / 3600) + "時間" + ((t / 60) % 60).ToString("00") + "分";
    }

    // ============================================================
    // 保存
    // ============================================================
    public static bool CanSave(out string why)
    {
        why = "";
        if (!GameSetup.Started) { why = "ゲームがまだ始まっていない"; return false; }
        var turn = DungeonTurnManager.Instance;
        // ⚠ 保存は**前半（迷宮フェーズ）のみ**。ロードは必ず前半から再開する作りなので、
        //   後半（地上）で保存できてしまうと、読み直したときに前半をもう一度やることになり、
        //   そのターンの防衛戦が二重に起きる。
        if (turn != null && !turn.IsDungeonPhase)
        { why = turn.IsSurfacePhase ? "地上フェーズでは保存できない（ターンを終えてから）" : "戦闘中は保存できない"; return false; }
        return true;
    }

    public static bool Save(int slot, out string err)
    {
        err = "";
        if (!CanSave(out err)) return false;
        try
        {
            // 表示中のフロアの配置は FeatureManager 側に居るので、FloorData に書き戻してから保存する
            var fm = DungeonFloorManager.Instance;
            if (fm != null) fm.SyncCurrentFloorFeatures();

            Directory.CreateDirectory(Dir);
            string path = FileOf(slot);
            string tmp = path + ".tmp";

            using (var fs = File.Create(tmp))
            {
                var res = DungeonResourceManager.Instance;
                var turn = DungeonTurnManager.Instance;
                using (var head = new BinaryWriter(fs, System.Text.Encoding.UTF8, true))
                {
                    head.Write("DBRS".ToCharArray());
                    head.Write(Format);
                    head.Write(turn != null ? turn.CurrentTurn : 1);
                    head.Write(res != null ? res.DungeonPoints : 0);
                    head.Write(fm != null ? fm.BuiltFloorCount : 0);
                    head.Write(CountOwned());
                    head.Write(EraSystem.EraName(EraSystem.Current));
                    head.Write(DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
                    head.Write(PlaySeconds);
                }
                using (var gz = new GZipStream(fs, System.IO.Compression.CompressionLevel.Optimal, true))
                using (var w = new BinaryWriter(gz))
                {
                    WriteBody(w);
                }
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);   // 書き切ってから差し替える（途中で落ちても前のセーブが残る）

            long size = new FileInfo(path).Length;
            Debug.Log("💾『保存』" + SlotName(slot) + " へ書き出した（" + (size / 1024) + " KB）: " + path);
            return true;
        }
        catch (Exception e)
        {
            err = e.Message;
            Debug.LogError("💾 保存に失敗: " + e);
            return false;
        }
    }

    public static void AutoSave()
    {
        string why;
        if (!CanSave(out why)) return;
        string err;
        Save(0, out err);
    }

    public static string SlotName(int slot) { return slot <= 0 ? "オート" : ("スロット" + slot); }

    private static int CountOwned()
    {
        int n = 0;
        for (int i = 0; i < SurfaceMap.Count; i++)
        {
            var r = SurfaceMap.Get(i);
            if (r != null && r.owner == SurfaceMap.OwnerSelf) n++;
        }
        return n;
    }

    private static void WriteBody(BinaryWriter w)
    {
        wTable = new List<TypeEntry>();
        wIndex = new Dictionary<Type, int>();

        // 先に本体を作る（型表は書きながら育つので、あとで前に付ける）
        byte[] payload;
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            var chunks = new List<KeyValuePair<string, object>>();
            var owners = new List<Type>();
            foreach (var t in StaticTypes) { chunks.Add(new KeyValuePair<string, object>(KeyOf(t, true), null)); owners.Add(t); }
            foreach (var t in BehaviourTypes)
            {
                var o = UnityEngine.Object.FindFirstObjectByType(t);
                if (o == null) continue;
                var hook = o as ISaveHook;
                if (hook != null) hook.OnBeforeSave();     // 🪝 保存できない形の状態を、保存できる形へ移す
                chunks.Add(new KeyValuePair<string, object>(KeyOf(t, false), o));
                owners.Add(t);
            }

            bw.Write(chunks.Count);
            for (int ci = 0; ci < chunks.Count; ci++)
            {
                var kv = chunks[ci];
                var t = owners[ci];
                bw.Write(kv.Key);
                var fields = SaveFields(t, kv.Value == null);
                bw.Write(fields.Count);
                foreach (var f in fields)
                {
                    bw.Write(f.Name);
                    int ti = IndexOf(f.FieldType);
                    bw.Write(ti);
                    WriteVal(bw, ti, f.GetValue(kv.Value));
                }
            }
            bw.Flush();
            payload = ms.ToArray();
        }

        // 型表 → 本体
        w.Write(wTable.Count);
        foreach (var e in wTable)
        {
            w.Write(e.kind); w.Write(e.prim); w.Write(e.a); w.Write(e.b);
            w.Write(e.isValueType);
            w.Write(e.name ?? "");
            int n = e.fieldNames != null ? e.fieldNames.Count : 0;
            w.Write(n);
            for (int i = 0; i < n; i++) { w.Write(e.fieldNames[i]); w.Write(e.fieldTypes[i]); }
        }
        w.Write(payload.Length);
        w.Write(payload);
    }

    // ============================================================
    // 読み込み
    // ============================================================
    public static bool Load(int slot, out string err)
    {
        err = "";
        string path = FileOf(slot);
        if (!File.Exists(path)) { err = "セーブがありません"; return false; }
        try
        {
            List<TypeEntry> table;
            Dictionary<string, Chunk> chunks;
            using (var fs = File.OpenRead(path))
            {
                using (var head = new BinaryReader(fs, System.Text.Encoding.UTF8, true))
                {
                    if (new string(head.ReadChars(4)) != "DBRS") { err = "形式が違う"; return false; }
                    int fmt = head.ReadInt32();
                    if (fmt > Format) { err = "新しい形式のセーブ（v" + fmt + "）"; return false; }
                    head.ReadInt32(); head.ReadInt32(); head.ReadInt32(); head.ReadInt32();
                    head.ReadString(); head.ReadString();
                    PlaySeconds = head.ReadSingle();
                }
                using (var gz = new GZipStream(fs, CompressionMode.Decompress, true))
                using (var r = new BinaryReader(gz))
                {
                    ReadBody(r, out table, out chunks);
                }
            }
            rTable = table;
            Apply(chunks);
            Debug.Log("💾『読み込み』" + SlotName(slot) + " から復元した（第" + (DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 0) + "ターン）");
            return true;
        }
        catch (Exception e)
        {
            err = e.Message;
            Debug.LogError("💾 読み込みに失敗: " + e);
            return false;
        }
    }

    private class Chunk
    {
        public List<string> names = new List<string>();
        public List<int> types = new List<int>();
        public List<object> vals = new List<object>();
    }

    private static void ReadBody(BinaryReader r, out List<TypeEntry> table, out Dictionary<string, Chunk> chunks)
    {
        int tc = r.ReadInt32();
        table = new List<TypeEntry>(tc);
        for (int i = 0; i < tc; i++)
        {
            var e = new TypeEntry();
            e.kind = r.ReadByte(); e.prim = r.ReadByte(); e.a = r.ReadInt32(); e.b = r.ReadInt32();
            e.isValueType = r.ReadBoolean();
            e.name = r.ReadString();
            int n = r.ReadInt32();
            if (n > 0)
            {
                e.fieldNames = new List<string>(n); e.fieldTypes = new List<int>(n);
                for (int k = 0; k < n; k++) { e.fieldNames.Add(r.ReadString()); e.fieldTypes.Add(r.ReadInt32()); }
            }
            table.Add(e);
        }
        rTable = table;

        r.ReadInt32();     // payload の長さ（今は使わない。将来スキップしたくなったとき用）
        chunks = new Dictionary<string, Chunk>();
        int cc = r.ReadInt32();
        for (int i = 0; i < cc; i++)
        {
            string cls = r.ReadString();
            var c = new Chunk();
            int fc = r.ReadInt32();
            for (int k = 0; k < fc; k++)
            {
                c.names.Add(r.ReadString());
                int ti = r.ReadInt32();
                c.types.Add(ti);
                c.vals.Add(ReadVal(r, ti));      // ⚠ 今のコードに無いフィールドでも**必ず読み進める**
            }
            chunks[cls] = c;
        }
    }

    /// <summary>復元の順番。⚠ 迷宮を組み直してから魔王を戻す（組み直しで魔王が作られるため）。</summary>
    private static void Apply(Dictionary<string, Chunk> chunks)
    {
        foreach (var t in StaticTypes) ApplyTo(t, null, chunks);

        var hooks = new List<ISaveHook>();
        foreach (var t in BehaviourTypes)
        {
            if (t == typeof(DemonLord)) continue;            // 魔王は迷宮を組み直したあと
            var o = UnityEngine.Object.FindFirstObjectByType(t);
            if (o == null) continue;
            ApplyTo(t, o, chunks);
            var h = o as ISaveHook; if (h != null) hooks.Add(h);
        }

        // 迷宮を組み直す（地形・配置・魔王の実体）
        var fm = DungeonFloorManager.Instance;
        if (fm != null) fm.RebuildAfterLoad();

        // 魔王は組み直しで作られるので、そのあとで中身を戻す
        var dl = UnityEngine.Object.FindFirstObjectByType<DemonLord>();
        if (dl != null) { ApplyTo(typeof(DemonLord), dl, chunks); dl.RefreshAfterLoad(); }

        foreach (var h in hooks) h.OnAfterLoad();            // 🪝 移し替えた状態を本来の置き場へ戻す

        var res = DungeonResourceManager.Instance;
        if (res != null) res.UpdateResourceUIDisplay();
        var turn = DungeonTurnManager.Instance;
        if (turn != null) turn.RefreshAfterLoad();

        var sv = UnityEngine.Object.FindFirstObjectByType<SurfaceView>();
        if (sv != null) sv.MarkDirty();      // 盤は1枚メッシュなので「汚れた」印を付ければ組み直る
        NotifySystem.Dirty = true;
        GameSetup.Started = true; GameSetup.WaitForTitle = false;
        if (Loaded != null) Loaded();
    }

    private static void ApplyTo(Type t, object instance, Dictionary<string, Chunk> chunks)
    {
        Chunk c;
        if (t == null || !chunks.TryGetValue(KeyOf(t, instance == null), out c)) return;
        var flags = BindingFlags.Public | BindingFlags.NonPublic | (instance == null ? BindingFlags.Static : BindingFlags.Instance);
        for (int i = 0; i < c.names.Count; i++)
        {
            FieldInfo f = null;
            try { f = t.GetField(c.names[i], flags); } catch { }
            if (f == null || !ShouldSave(f)) continue;          // 消えた/対象外のフィールドは捨てる
            object v;
            try { v = Coerce(f.FieldType, c.vals[i]); }
            catch (Exception e) { Debug.LogWarning("💾 " + t.Name + "." + f.Name + " を復元できず: " + e.Message); continue; }
            if (v == null && f.FieldType.IsValueType) continue;  // 値型に null は入れない
            try { f.SetValue(instance, v); } catch { }
        }
    }

    // ============================================================
    // 型表
    // ============================================================
    private class TypeEntry
    {
        public byte kind;           // 0=素の値 1=配列 2=二次元配列 3=List 4=HashSet 5=Dictionary 6=まとまり
        public byte prim;           // kind0: 1 bool / 2 byte / 3 int / 4 long / 5 float / 6 double / 7 string
        public int a, b;            // 要素の型index（Dictionary は a=キー b=値）
        public bool isValueType;    // kind6: 構造体か（構造体は null になり得ないので在否の印を書かない）
        public string name;
        public List<string> fieldNames;
        public List<int> fieldTypes;
        public List<FieldInfo> fields;   // 書き出し側だけが持つ
    }

    private static List<TypeEntry> wTable;
    private static Dictionary<Type, int> wIndex;
    private static List<TypeEntry> rTable;

    private static int IndexOf(Type t)
    {
        int i;
        if (wIndex.TryGetValue(t, out i)) return i;
        var e = new TypeEntry();
        wTable.Add(e); i = wTable.Count - 1; wIndex[t] = i;    // ⚠ 先に登録（自分自身を含む型でも無限再帰しない）

        if (t.IsEnum) { e.kind = 0; e.prim = 3; }
        else if (t == typeof(bool)) { e.kind = 0; e.prim = 1; }
        else if (t == typeof(byte) || t == typeof(sbyte)) { e.kind = 0; e.prim = 2; }
        else if (t == typeof(int) || t == typeof(short) || t == typeof(ushort) || t == typeof(uint) || t == typeof(char)) { e.kind = 0; e.prim = 3; }
        else if (t == typeof(long) || t == typeof(ulong)) { e.kind = 0; e.prim = 4; }
        else if (t == typeof(float)) { e.kind = 0; e.prim = 5; }
        else if (t == typeof(double) || t == typeof(decimal)) { e.kind = 0; e.prim = 6; }
        else if (t == typeof(string)) { e.kind = 0; e.prim = 7; }
        else if (t.IsArray) { e.kind = (byte)(t.GetArrayRank() == 2 ? 2 : 1); e.a = IndexOf(t.GetElementType()); }
        else if (IsGeneric(t, typeof(List<>))) { e.kind = 3; e.a = IndexOf(t.GetGenericArguments()[0]); }
        else if (IsGeneric(t, typeof(HashSet<>))) { e.kind = 4; e.a = IndexOf(t.GetGenericArguments()[0]); }
        else if (IsGeneric(t, typeof(Dictionary<,>)))
        {
            e.kind = 5; e.a = IndexOf(t.GetGenericArguments()[0]); e.b = IndexOf(t.GetGenericArguments()[1]);
        }
        else
        {
            e.kind = 6; e.isValueType = t.IsValueType; e.name = t.FullName;
            e.fieldNames = new List<string>(); e.fieldTypes = new List<int>(); e.fields = new List<FieldInfo>();
            foreach (var f in SaveFields(t, false))
            {
                e.fieldNames.Add(f.Name); e.fields.Add(f); e.fieldTypes.Add(IndexOf(f.FieldType));
            }
        }
        return i;
    }

    private static bool IsGeneric(Type t, Type def)
    {
        return t.IsGenericType && t.GetGenericTypeDefinition() == def;
    }

    /// <summary>保存対象のフィールド。⚠ `readonly` は「カタログ」の目印として**除外**する。</summary>
    private static List<FieldInfo> SaveFields(Type t, bool staticSide)
    {
        var list = new List<FieldInfo>();
        if (t == null) return list;
        var flags = BindingFlags.Public | BindingFlags.NonPublic | (staticSide ? BindingFlags.Static : BindingFlags.Instance);
        foreach (var f in t.GetFields(flags))
        {
            if (ShouldSave(f)) { list.Add(f); continue; }
            WarnIfLooksLikeState(t, f);
        }
        list.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));   // 並びを安定させる
        return list;
    }

    /// <summary>
    /// 🛎️ 「状態なのに readonly」を見つけたら知らせる**安全網**。
    /// `DungeonFloorManager.floors` が `readonly` だったせいで、**迷宮そのものが保存されず復元後に0層**になった。
    /// カタログはほぼ `static readonly`、状態は実体側に付くので、**実体の readonly なコレクション**だけを疑う。
    /// 意図して保存しない物（マーカーの持ち主など）は `[NonSerialized]` を付けて黙らせること。
    /// </summary>
    private static readonly HashSet<string> warned = new HashSet<string>();
    private static void WarnIfLooksLikeState(Type owner, FieldInfo f)
    {
        if (!f.IsInitOnly || f.IsStatic || f.IsNotSerialized) return;
        var ft = f.FieldType;
        bool collection = ft.IsArray || IsGeneric(ft, typeof(List<>)) || IsGeneric(ft, typeof(HashSet<>)) || IsGeneric(ft, typeof(Dictionary<,>));
        if (!collection) return;
        string key = owner.FullName + "." + f.Name;
        if (!warned.Add(key)) return;
        Debug.LogWarning("💾 " + key + " は readonly なのでセーブに乗りません。"
            + "状態なら readonly を外し、保存しないなら [NonSerialized] を付けてください。");
    }

    private static bool ShouldSave(FieldInfo f)
    {
        if (f.IsLiteral || f.IsInitOnly || f.IsNotSerialized) return false;
        return CanHandle(f.FieldType, 0);
    }

    private static bool CanHandle(Type t, int depth)
    {
        if (depth > 8 || t == null) return false;
        if (t.IsEnum || t.IsPrimitive || t == typeof(string) || t == typeof(decimal)) return true;
        if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return false;   // GameObject / Sprite / Text など
        if (typeof(Delegate).IsAssignableFrom(t)) return false;
        if (t.IsPointer || t.IsInterface || t.IsAbstract) return false;
        if (t.IsArray) return t.GetArrayRank() <= 2 && CanHandle(t.GetElementType(), depth + 1);
        if (IsGeneric(t, typeof(List<>)) || IsGeneric(t, typeof(HashSet<>))) return CanHandle(t.GetGenericArguments()[0], depth + 1);
        if (IsGeneric(t, typeof(Dictionary<,>)))
            return CanHandle(t.GetGenericArguments()[0], depth + 1) && CanHandle(t.GetGenericArguments()[1], depth + 1);
        if (t.IsGenericType) return false;                                   // それ以外のジェネリックは扱わない
        if (!t.IsValueType && t.GetConstructor(Type.EmptyTypes) == null) return false;
        return true;
    }

    // ============================================================
    // 値の書き出し
    // ============================================================
    private static void WriteVal(BinaryWriter w, int ti, object v)
    {
        var e = wTable[ti];
        switch (e.kind)
        {
            case 0:
                switch (e.prim)
                {
                    case 1: w.Write(v != null && Convert.ToBoolean(v)); break;
                    case 2: w.Write(v == null ? (byte)0 : Convert.ToByte(v)); break;
                    case 3: w.Write(v == null ? 0 : Convert.ToInt32(v)); break;
                    case 4: w.Write(v == null ? 0L : Convert.ToInt64(v)); break;
                    case 5: w.Write(v == null ? 0f : Convert.ToSingle(v)); break;
                    case 6: w.Write(v == null ? 0.0 : Convert.ToDouble(v)); break;
                    default:
                        w.Write(v != null);
                        if (v != null) w.Write((string)v);
                        break;
                }
                break;

            case 1:
                {
                    w.Write(v != null); if (v == null) break;
                    var ar = (Array)v;
                    w.Write(ar.Length);
                    for (int i = 0; i < ar.Length; i++) WriteVal(w, e.a, ar.GetValue(i));
                    break;
                }
            case 2:
                {
                    w.Write(v != null); if (v == null) break;
                    var ar = (Array)v;
                    int d0 = ar.GetLength(0), d1 = ar.GetLength(1);
                    w.Write(d0); w.Write(d1);
                    for (int x = 0; x < d0; x++) for (int y = 0; y < d1; y++) WriteVal(w, e.a, ar.GetValue(x, y));
                    break;
                }
            case 3:
            case 4:
                {
                    w.Write(v != null); if (v == null) break;
                    var tmp = new List<object>();
                    foreach (var o in (IEnumerable)v) tmp.Add(o);
                    w.Write(tmp.Count);
                    for (int i = 0; i < tmp.Count; i++) WriteVal(w, e.a, tmp[i]);
                    break;
                }
            case 5:
                {
                    w.Write(v != null); if (v == null) break;
                    var d = (IDictionary)v;
                    w.Write(d.Count);
                    foreach (DictionaryEntry de in d) { WriteVal(w, e.a, de.Key); WriteVal(w, e.b, de.Value); }
                    break;
                }
            default:
                {
                    if (!e.isValueType) { w.Write(v != null); if (v == null) break; }
                    for (int i = 0; i < e.fields.Count; i++)
                        WriteVal(w, e.fieldTypes[i], v == null ? null : e.fields[i].GetValue(v));
                    break;
                }
        }
    }

    // ============================================================
    // 値の読み込み（今のコードの型に依存せず、**ファイルの型表だけ**で復号する）
    // ============================================================
    private class ObjVal { public int ti; public object[] vals; }
    private class Arr2 { public int d0, d1; public object[] cells; }

    private static object ReadVal(BinaryReader r, int ti)
    {
        var e = rTable[ti];
        switch (e.kind)
        {
            case 0:
                switch (e.prim)
                {
                    case 1: return r.ReadBoolean();
                    case 2: return r.ReadByte();
                    case 3: return r.ReadInt32();
                    case 4: return r.ReadInt64();
                    case 5: return r.ReadSingle();
                    case 6: return r.ReadDouble();
                    default: return r.ReadBoolean() ? r.ReadString() : null;
                }
            case 1:
                {
                    if (!r.ReadBoolean()) return null;
                    int n = r.ReadInt32();
                    var a = new object[n];
                    for (int i = 0; i < n; i++) a[i] = ReadVal(r, e.a);
                    return a;
                }
            case 2:
                {
                    if (!r.ReadBoolean()) return null;
                    var g = new Arr2(); g.d0 = r.ReadInt32(); g.d1 = r.ReadInt32();
                    g.cells = new object[g.d0 * g.d1];
                    for (int i = 0; i < g.cells.Length; i++) g.cells[i] = ReadVal(r, e.a);
                    return g;
                }
            case 3:
            case 4:
                {
                    if (!r.ReadBoolean()) return null;
                    int n = r.ReadInt32();
                    var l = new List<object>(n);
                    for (int i = 0; i < n; i++) l.Add(ReadVal(r, e.a));
                    return l;
                }
            case 5:
                {
                    if (!r.ReadBoolean()) return null;
                    int n = r.ReadInt32();
                    var l = new List<object[]>(n);
                    for (int i = 0; i < n; i++) { var k = ReadVal(r, e.a); var v = ReadVal(r, e.b); l.Add(new object[] { k, v }); }
                    return l;
                }
            default:
                {
                    if (!e.isValueType && !r.ReadBoolean()) return null;
                    var o = new ObjVal { ti = ti, vals = new object[e.fieldNames != null ? e.fieldNames.Count : 0] };
                    for (int i = 0; i < o.vals.Length; i++) o.vals[i] = ReadVal(r, e.fieldTypes[i]);
                    return o;
                }
        }
    }

    /// <summary>復号した素の値を、**いまのコードの型**へ寄せる。無い物は捨て、足りない物は既定値のまま。</summary>
    private static object Coerce(Type t, object v)
    {
        if (v == null) return null;
        if (t.IsEnum) return Enum.ToObject(t, Convert.ToInt64(v));
        if (t == typeof(string)) return v as string;
        if (t.IsPrimitive || t == typeof(decimal)) return Convert.ChangeType(v, t);

        if (t.IsArray && t.GetArrayRank() == 1)
        {
            var src = v as object[]; if (src == null) return null;
            var el = t.GetElementType();
            var a = Array.CreateInstance(el, src.Length);
            for (int i = 0; i < src.Length; i++) { var c = Coerce(el, src[i]); if (c != null) a.SetValue(c, i); }
            return a;
        }
        if (t.IsArray && t.GetArrayRank() == 2)
        {
            var g = v as Arr2; if (g == null) return null;
            var el = t.GetElementType();
            var a = Array.CreateInstance(el, g.d0, g.d1);
            for (int x = 0; x < g.d0; x++)
                for (int y = 0; y < g.d1; y++)
                {
                    var c = Coerce(el, g.cells[x * g.d1 + y]);
                    if (c != null) a.SetValue(c, x, y);
                }
            return a;
        }
        if (IsGeneric(t, typeof(List<>)))
        {
            var src = v as List<object>; if (src == null) return null;
            var el = t.GetGenericArguments()[0];
            var l = (IList)Activator.CreateInstance(t);
            foreach (var o in src) l.Add(Coerce(el, o));
            return l;
        }
        if (IsGeneric(t, typeof(HashSet<>)))
        {
            var src = v as List<object>; if (src == null) return null;
            var el = t.GetGenericArguments()[0];
            var hs = Activator.CreateInstance(t);
            var add = t.GetMethod("Add", new[] { el });
            foreach (var o in src) { var c = Coerce(el, o); if (c != null) add.Invoke(hs, new[] { c }); }
            return hs;
        }
        if (IsGeneric(t, typeof(Dictionary<,>)))
        {
            var src = v as List<object[]>; if (src == null) return null;
            var kt = t.GetGenericArguments()[0]; var vt = t.GetGenericArguments()[1];
            var d = (IDictionary)Activator.CreateInstance(t);
            foreach (var pair in src)
            {
                var k = Coerce(kt, pair[0]); if (k == null) continue;
                d[k] = Coerce(vt, pair[1]);
            }
            return d;
        }

        var ov = v as ObjVal; if (ov == null) return null;
        var e = rTable[ov.ti];
        object inst;
        try { inst = Activator.CreateInstance(t); } catch { return null; }
        if (e.fieldNames == null) return inst;
        for (int i = 0; i < e.fieldNames.Count; i++)
        {
            FieldInfo f = null;
            try { f = t.GetField(e.fieldNames[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); } catch { }
            if (f == null || !ShouldSave(f)) continue;
            object c;
            try { c = Coerce(f.FieldType, ov.vals[i]); } catch { continue; }
            if (c == null && f.FieldType.IsValueType) continue;
            try { f.SetValue(inst, c); } catch { }
        }
        return inst;
    }

    // ============================================================
    // 後始末
    // ============================================================
    public static bool Delete(int slot)
    {
        try
        {
            string p = FileOf(slot);
            if (!File.Exists(p)) return false;
            File.Delete(p);
            return true;
        }
        catch (Exception e) { Debug.LogError("💾 削除に失敗: " + e.Message); return false; }
    }

    public static bool AnySave()
    {
        for (int i = 0; i <= SlotCount; i++) if (File.Exists(FileOf(i))) return true;
        return false;
    }
}
