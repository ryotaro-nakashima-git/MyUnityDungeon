using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⚔️ 盤の上を歩く敵の軍（U2）。他魔王の軍と、人間の奪還軍。
///
/// **これまでは数値だけで解決していた**：他魔王は「一番手薄な自領」を遠隔から一撃で奪い、
/// 奪還軍も同じだった。突然もぎ取られるので、**防ぎようも読みようも無かった**。
///
/// U2では Civ と同じく、敵も**盤の上に立って歩いてくる**：
/// - 本拠地（人間は中立の町）から軍が湧き、**毎ターン移動力ぶん**近づく
/// - **支配地域(ZoC)**：こちらの眷属に隣接したら足を止める＝壁になる
/// - 目標に隣り合ってから**攻城**する＝**来るのが見えるので迎え撃てる**
/// - こちらから**迎撃**もできる（眷属で軍そのものを叩く。タイルは取らない）
///
/// 純static・実行時保持。関連: [[RivalLords]] [[KinRoster]] [[surface-units-u1]]。
/// </summary>
public static class EnemyForce
{
    /// <summary>
    /// ⚔️ その兵の役目（③地上の作り直し）。
    /// ⚠⚠ <b>これが虫食いを止める本体。</b> 旧仕様は全部の兵が「一番守りの薄い1タイル」を
    ///   目指して歩き、勝つとそのタイル1枚を奪っていた。
    ///   いまは <b>敵対した集落の兵(March)だけが進軍</b>し、それ以外は自分の版図と
    ///   警戒圏から出ない。
    /// </summary>
    public enum Role { March = 0, Guard = 1 }

    public class Army
    {
        public int id;
        public int owner;        // -1＝人類の集落／0..＝他魔王のindex
        /// <summary>🏘️ どの集落が出した兵か（owner==-1 のときだけ意味がある／-1＝主なし）。</summary>
        public int realmIndex = -1;
        /// <summary>役目。守りの兵は版図＋警戒圏から出ない。</summary>
        public Role role = Role.March;
        public string name;
        public float power;
        public int regionId;
        public int targetId = -1;
        public int mp;
        public int idleTurns;    // 目標に届かないまま経った回数（長いと引き上げる）
        /// <summary>⏮️ このターンの開始位置（Phase C-14：動きを見せるために覚えておく）。</summary>
        public int prevRegionId = -1;
        /// <summary>⏳ 集結が済むまでの残りターン。0になるまで**動かない**（湧いた場所で見えている）。</summary>
        public int musterTurns;
        /// <summary>
        /// ⚔️ 兵科（U-3）。こちらの軍団と**同じ物差し**で持つ。
        /// ⚠ 敵に兵科が無いと三すくみが片側にしか効かず、「どの軍団を当てるか」の判断が生まれない。
        /// </summary>
        public LegionRoster.Cls cls;
        /// <summary>
        /// 🏯 **迷宮そのものを狙っている**（S-3）。地上に狙う先が無くなった奪還軍がこうなる。
        /// ⚠ これが無いと、**版図を全部失ったほうが安全になる**（狙う先が消えて軍が帰ってしまう）。
        /// </summary>
        public bool toDungeon;
        /// <summary>入口に着いてから雪崩れ込むまでの残りターン。⚠ 0になる前に地上で潰せば防げる。</summary>
        public int gateTurns;
    }

    /// <summary>湧くときの兵科。術者は稀（人の軍は前衛と射手が主）。</summary>
    private static LegionRoster.Cls RollClass(bool human)
    {
        int r = Random.Range(0, 100);
        if (human) return r < 34 ? LegionRoster.Cls.Van : r < 64 ? LegionRoster.Cls.Assault
                                 : r < 90 ? LegionRoster.Cls.Archer : LegionRoster.Cls.Caster;
        return r < 26 ? LegionRoster.Cls.Van : r < 56 ? LegionRoster.Cls.Assault
             : r < 80 ? LegionRoster.Cls.Archer : LegionRoster.Cls.Caster;
    }

    public const int Movement = 2;          // 1ターンに歩けるタイル数（重い地形は入れない）
    public const int MaxPerRival = 2;       // 1人の魔王が同時に出せる軍
    public const int MaxHuman = 2;          // （旧）奪還軍の同時数。集落制になったので使っていない

    /// <summary>🔥 荒らされたタイルが産出を止めている長さ。敵が去っても数ターン戻らない。</summary>
    public const int PillageTurns = 3;

    /// <summary>
    /// ⚔️ <b>同時に進軍できる人類の兵の数</b>。
    /// ⚠⚠ これが無いと盤が兵で埋まる。実測：9集落すべてが敵対すると
    ///   守備上限（村1／町2／都市4）の合計だけ湧いて **30体が一斉に進軍した**。
    ///   集落から湧く形にしたぶん出どころは増えたので、**押し寄せる数のほうを絞る**。
    ///   溢れたぶんは湧かないのではなく<b>守備に回る</b>（盤には居るが版図から出ない）。
    /// </summary>
    public static int MaxMarching => 3 + (int)EraSystem.Current * 2;

    /// <summary>いま進軍している人類の兵の数。</summary>
    public static int MarchingCount()
    {
        EnsureInit(); int n = 0;
        foreach (var a in all) if (a.owner == -1 && a.role == Role.March) n++;
        return n;
    }

    /// <summary>🔥 荒らされたタイルの回復（毎ターン先頭で呼ぶ）。</summary>
    public static void TickPillage()
    {
        foreach (var r in SurfaceMap.All) if (r.pillagedTurns > 0) r.pillagedTurns--;
    }

    /// <summary>
    /// ⏳ 集結にかかるターン数。**人間は集団で生きる生き物**なので、1つの行動に数ターンかける。
    ///
    /// ⚠ 旧仕様は湧いた次の瞬間から毎ターン2タイル進んでいたので、実測で**2ターン目から毎ターン
    ///   領地を削られる**状態だった（湧く条件が `90×世界水準 + 60×log(1+fame/50) ≥ 100` で、
    ///   世界水準には『領地数』のバイアスが入るため、**広げるほど早く湧く**のも効いていた）。
    ///   集結ターンを置くと、こちらに**見てから手を打つ猶予**ができる＝盤面が読めるようになる。
    /// </summary>
    public const int HumanMuster = 2;
    public const int RivalMuster = 1;
    /// <summary>撃退したあと次の奪還軍が湧くまでの間（波状攻撃で息継ぎができない問題への対処）。</summary>
    public const int HumanCooldownTurns = 3;
    private static int humanCooldown;
    public static int HumanCooldown => humanCooldown;
    public static void StartHumanCooldown() { humanCooldown = HumanCooldownTurns; }
    public static void TickHumanCooldown() { if (humanCooldown > 0) humanCooldown--; }

    /// <summary>
    /// まだ集結中の奪還軍がいるか。**いるあいだは次を出さない**。
    /// ⚠ これが無いと、上限2体が連続したターンに湧いて**2つの軍が同時に押し寄せる**
    ///   （ユーザー報告「2人とかで2ターン目から毎ターン奪られる」の直接の形）。
    ///   1つずつ来れば、迎撃するか砦を建てるかの判断が1件ずつ成立する。
    /// </summary>
    public static bool AnyHumanMustering()
    {
        EnsureInit();
        foreach (var a in all) if (a.owner < 0 && a.musterTurns > 0) return true;
        return false;
    }

    private static List<Army> all;
    private static int nextId = 1;
    private static void EnsureInit() { if (all == null) all = new List<Army>(); }
    /// <summary>
    /// 🏯 迷宮へ雪崩れ込んだ軍の戦力の合計（→ [[WaveRoster]] が次の波に足す）。
    /// ⚠ **状態なのでセーブに載る**（`readonly` にしない → [[SaveSystem]]）。
    /// </summary>
    private static float pendingAssault;
    public static float PendingAssault { get { return pendingAssault; } }

    /// <summary>次の波に混ぜたら空にする。⚠ 受け取る側が1回だけ呼ぶこと。</summary>
    public static float TakeAssault() { float v = pendingAssault; pendingAssault = 0f; return v; }

    /// <summary>入口に着いていて、次のターンに雪崩れ込む軍（→ [[Foretell]] が出す）。</summary>
    public static int TurnsToAssault(out float power, out string name)
    {
        EnsureInit(); power = 0f; name = "";
        int best = int.MaxValue;
        foreach (var a in all)
        {
            if (!a.toDungeon) continue;
            int t = a.regionId == SurfaceMap.GateId ? a.gateTurns
                  : Mathf.Max(1, SurfaceMap.HexDist(SurfaceMap.Get(a.regionId), SurfaceMap.Get(SurfaceMap.GateId)));
            if (t < best) { best = t; power = a.power; name = a.name; }
        }
        return best == int.MaxValue ? -1 : best;
    }

    public static void Reset() { all = new List<Army>(); nextId = 1; humanCooldown = 0; pendingAssault = 0f; }

    public static IReadOnlyList<Army> All { get { EnsureInit(); return all; } }
    public static int Count { get { EnsureInit(); return all.Count; } }
    public static Army At(int regionId)
    {
        EnsureInit();
        foreach (var a in all) if (a.regionId == regionId) return a;
        return null;
    }
    public static int CountOf(int owner)
    {
        EnsureInit(); int n = 0;
        foreach (var a in all) if (a.owner == owner) n++;
        return n;
    }
    public static string ColorOf(Army a)
    {
        return a.owner < 0 ? "#e8e2d0" : RivalLords.ColorOf(a.owner);
    }
    public static string OwnerName(Army a)
    {
        return a.owner < 0 ? "人間の奪還軍" : RivalLords.NameOf(a.owner) + "の軍";
    }

    // ============ 湧く ============
    /// <summary>他魔王が本拠地から軍を切り出す。出したぶんだけ本体の力は減る（際限なく湧かない）。</summary>
    public static void SpawnFromRival(int rivalIndex)
    {
        EnsureInit();
        var rv = RivalLords.Get(rivalIndex);
        if (rv.defeated) return;
        if (CountOf(rivalIndex) >= MaxPerRival) return;
        int home = RivalLords.HomeOf(rivalIndex);
        if (home < 0) return;
        float take = rv.power * 0.45f;
        if (take < 60f) return;
        rv.power -= take;
        var a = new Army
        {
            id = nextId++, owner = rivalIndex, name = RivalLords.NameOf(rivalIndex) + "の軍",
            power = take, regionId = home, mp = Movement, musterTurns = RivalMuster, cls = RollClass(false),
        };
        all.Add(a);
        Debug.Log($"⚔️『進発』{a.name}（{LegionRoster.ClassName(a.cls)}・戦力{take:0}）が {SurfaceMap.Get(home).name} に集まりつつある");
        NotifySystem.Push($"<b>{a.name}</b>（戦力{take:0}）が {SurfaceMap.Get(home).name} に集まりつつある（{RivalMuster}ターン後に進発）", NotifySystem.Kind.Danger, home);
    }

    // ══════════════ 🏘️ 集落から湧く（③地上の作り直し・2026-09-07）══════════════
    // ⚠⚠ **旧 `SpawnHuman` を廃した。** あれは「自領に隣接する中立タイルからランダムに」
    //   軍を湧かせていた ―― 文字どおり**どこからともなく湧いていた**。
    //   いまは **必ず集落の中心から** 出る（Civ VII と同じで、ユニットは集落で生産される）。
    //   どこから来るのかが盤の上で最初から見えているので、読んで手が打てる。

    /// <summary>その集落が出している兵の数。</summary>
    public static int CountOfRealm(int realmIndex)
    {
        EnsureInit(); int n = 0;
        foreach (var a in all) if (a.owner == -1 && a.realmIndex == realmIndex) n++;
        return n;
    }

    /// <summary>🧹 集落が陥落したら、その集落が出していた兵は主を失って霧散する。</summary>
    public static void DisbandOf(int realmIndex)
    {
        EnsureInit();
        for (int i = all.Count - 1; i >= 0; i--)
            if (all[i].owner == -1 && all[i].realmIndex == realmIndex)
            {
                Debug.Log("🏳️『主を失った』" + all[i].name + " は散り散りになった");
                all.RemoveAt(i);
            }
    }

    /// <summary>
    /// 🏘️ 集落が兵を1体出す。⚠ <b>湧く場所は必ず中心</b>。
    /// 戦力はその集落の格と、世界の育ち具合（＝これまでと同じ物差し）で決まる。
    /// </summary>
    public static void SpawnFromRealm(int realmIndex)
    {
        EnsureInit();
        var p = HumanRealm.At(realmIndex);
        if (p == null || p.destroyed) return;
        var c = SurfaceMap.Get(p.regionId);
        if (c == null) return;

        float scale = 1f + 0.6f * p.grade;
        float army = HumanArmyPower() * scale;
        var a = new Army
        {
            id = nextId++, owner = -1, realmIndex = realmIndex,
            name = p.name + "の" + (p.posture >= HumanRealm.Hostile ? "討伐隊" : "守備隊"),
            power = army, regionId = p.regionId, mp = Movement,
            musterTurns = HumanMuster, cls = RollClass(true),
            // 👁️ 敵対していない集落の兵は**警戒圏から出ない**（＝虫食いが構造的に起きない）。
            //    ⚠ 進軍の枠が埋まっているときも守備に回す（湧かせない、ではなく**出さない**）。
            role = (p.posture >= HumanRealm.Hostile && MarchingCount() < MaxMarching)
                 ? Role.March : Role.Guard,
        };
        all.Add(a);
        Debug.Log($"⚔️『徴集』{a.name}（{LegionRoster.ClassName(a.cls)}・戦力{army:0}）が {c.name} で編成された"
            + $"／{(a.role == Role.March ? "進軍" : "守備")}");
        if (a.role == Role.March)
            NotifySystem.Push($"<b>{a.name}</b>（戦力{army:0}）が <b>{c.name}</b> で編成された。"
                + $"<b>{HumanMuster}ターン後</b>に動き出す", NotifySystem.Kind.Danger, c.id);
    }

    /// <summary>
    /// 人間の兵1体ぶんの戦力。⚠ <b>物差しは変えない</b>（旧 `SpawnHuman` に渡していた値と同じ源）。
    /// ここを触ると難易度カーブが動く → [[difficulty-curve-orders]]。
    /// </summary>
    private static float humanArmyPower = 120f;
    public static void SetHumanArmyPower(float v) { humanArmyPower = Mathf.Max(1f, v); }
    public static float HumanArmyPower() { return humanArmyPower; }

    // ============ 動く ============
    /// <summary>
    /// 狙う先。
    ///
    /// ⚠⚠ <b>旧仕様がここで虫食いを作っていた。</b>「一番守りの薄い<b>1タイル</b>」を選ぶので、
    ///   こちらの都市が無事なのに端のタイルから1枚ずつもぎ取られていた。
    ///   → 人類の兵は <b>こちらの『拠点』（Town/City）</b> を狙う。版図タイルは狙わない
    ///     ＝ <b>拠点を落とさない限り版図は動かない</b>（Civ VII と同じ）。
    /// ⚠ 守りの兵(Guard)は狙う先を持たない（自分の版図と警戒圏の中だけを行き来する）。
    /// </summary>
    private static int PickTarget(Army a)
    {
        if (a.owner < 0 && a.role == Role.Guard) return -1;

        SurfaceMap.Region best = null; float bestScore = float.MaxValue;
        foreach (var r in SurfaceMap.All)
        {
            if (r.isOcean || r.type == SurfaceMap.RegionType.Gate) continue;
            bool hostile;
            if (a.owner < 0)
            {
                // 🏘️ 人類は**こちらの拠点だけ**を狙う（版図の1枚は取りに来ない）
                hostile = r.owned && r.settle != SurfaceMap.Settle.None;
            }
            else
            {
                // 🔥 他魔王も同じ作法：拠点か、主のいない荒野を取りに行く
                hostile = (r.owned && r.settle != SurfaceMap.Settle.None)
                       || (r.IsHuman && HumanRealm.IndexOfRegion(r.id) >= 0)
                       || (r.IsRival && r.RivalIndex != a.owner && r.settle != SurfaceMap.Settle.None);
            }
            if (!hostile) continue;
            float d = SurfaceMap.DefenseOf(r.id) + SurfaceMap.HexDist(SurfaceMap.Get(a.regionId), r) * 25f;
            if (d < bestScore) { bestScore = d; best = r; }
        }
        return best != null ? best.id : -1;
    }

    /// <summary>
    /// 👁️ 守りの兵が出てよい範囲か（自分の版図＋警戒圏）。
    /// ⚠ ここを緩めると守備隊が盤を歩き回り、結局いまの虫食いに戻る。
    /// </summary>
    private static bool WithinWatch(Army a, int regionId)
    {
        var p = HumanRealm.At(a.realmIndex);
        if (p == null) return false;
        var c = SurfaceMap.Get(p.regionId); var t = SurfaceMap.Get(regionId);
        if (c == null || t == null) return false;
        return SurfaceMap.HexDist(c, t) <= HumanRealm.RadiusOf(p.grade) + HumanRealm.WatchRing(p.grade);
    }

    /// <summary>次の1歩（通れる隣で、目標に一番近づくもの）。</summary>
    private static int NextStep(Army a, int target)
    {
        var cur = SurfaceMap.Get(a.regionId);
        var tgt = SurfaceMap.Get(target);
        int bestId = -1; int bestDist = SurfaceMap.HexDist(cur, tgt);
        foreach (var n in SurfaceMap.Neighbors(a.regionId))
        {
            if (!SurfaceMap.IsPassable(n)) continue;
            if (n.id == target) continue;                      // 目標には「攻める」ので踏み込まない
            if (At(n.id) != null) continue;                    // 味方の軍と重ならない
            // ⚠⚠ **こちらの版図には踏み込ませる（③地上の作り直し）。**
            //   旧仕様はここで `n.owned` を弾いていた。狙う先が「一番手薄な1タイル」だった頃は
            //   それでも成立していたが、狙う先を**拠点だけ**にした途端、
            //   敵は自領の外周で足を止めて3ターン後に引き上げるようになり
            //   **こちらが完全に無敵になった**（実測：9集落すべて敵対で25ターン、自領19タイルが1枚も減らない）。
            //   Civ でも敵は国境の中を歩いて略奪する。**版図は通れる／奪えるのは拠点だけ**が正しい形。
            if (n.owned && n.settle != SurfaceMap.Settle.None) continue;   // 拠点は攻城してからでないと入れない
            int d = SurfaceMap.HexDist(n, tgt);
            if (d < bestDist) { bestDist = d; bestId = n.id; }
        }
        return bestId;
    }

    /// <summary>
    /// 🏯 **迷宮へ向かう軍の1歩**（S-3）。⚠ 通常の `NextStep` は
    /// 「隣で一番近いところ」を選ぶだけの貪欲法なので、**窪みに嵌まって往復する**
    /// （実測：石造りの牧草地 ↔ 廃里 を3ターン往復して引き上げてしまった）。
    /// 入口までは必ず着いてほしいので、ここだけ**幅優先で本当の道**を引く。
    /// ⚠ こちらの領域も通す（この状態のプレイヤーは地上をほぼ失っている）。
    /// </summary>
    private static int NextStepToGate(Army a, int gate)
    {
        if (a.regionId == gate) return -1;
        var prev = new Dictionary<int, int>();
        var q = new Queue<int>();
        prev[a.regionId] = a.regionId; q.Enqueue(a.regionId);
        bool found = false;
        while (q.Count > 0 && !found)
        {
            int cur = q.Dequeue();
            foreach (var n in SurfaceMap.Neighbors(cur))
            {
                if (prev.ContainsKey(n.id)) continue;
                if (n.id != gate && !SurfaceMap.IsPassable(n)) continue;
                prev[n.id] = cur;
                if (n.id == gate) { found = true; break; }
                q.Enqueue(n.id);
            }
        }
        if (!found) return -1;
        // 入口から手前へ辿って、**最初の1歩**を取り出す
        int at = gate;
        while (prev[at] != a.regionId) at = prev[at];
        return at;
    }

    /// <summary>🚧 こちらの眷属に隣接しているか（Civの支配地域＝足が止まる）。</summary>
    public static bool InKinZoC(int regionId)
    {
        foreach (var n in SurfaceMap.Neighbors(regionId))
        {
            var k = KinRoster.KinAt(n.id);
            if (k != null && k.injuryTurns <= 0) return true;
        }
        // ⚔️ 軍団も戦線を張る（U-1）。ここを眷属だけにしておくと
        //    「並べても敵が素通りする」になり、並べる意味が半分になる。
        return LegionRoster.InZoC(regionId);
    }

    // ============ ターンの解決 ============
    public static void ResolveTurn(int turn)
    {
        EnsureInit();
        for (int i = all.Count - 1; i >= 0; i--)
        {
            var a = all[i];
            if (a.owner >= 0 && RivalLords.Get(a.owner).defeated) { all.RemoveAt(i); continue; }
            a.prevRegionId = a.regionId;   // ⏮️ どこから動いたかを覚えておく（あとで盤で再生する）
            // 🏘️ 進軍する人類の兵は少し速い（自分の国の街道を通ってくる、という理屈）。
            //   ⚠ 集落から出す形にしたぶん**出発点が遠くなった**ので、ここを 2 のままにすると
            //     移動だけで20ターン以上かかり、1周のあいだ脅威が盤に届かない。
            a.mp = (a.owner < 0 && a.role == Role.March) ? Movement + 1 : Movement;

            // ⏳ 集結中は動かない。**見えているのに動かない1〜2ターン**が、こちらの対処の猶予になる。
            if (a.musterTurns > 0)
            {
                a.musterTurns--;
                string when = a.musterTurns > 0 ? $"あと{a.musterTurns}ターンで進発" : "次のターンに進発する";
                Debug.Log($"⏳『集結』{a.name} は {SurfaceMap.Get(a.regionId).name} で兵を集めている（{when}）");
                NotifySystem.Push($"<b>{a.name}</b> が {SurfaceMap.Get(a.regionId).name} で集結中（{when}）", NotifySystem.Kind.Danger, a.regionId);
                continue;
            }

            // 🏯 **入口に着いた奪還軍は、数えて雪崩れ込む**（S-3）。
            //   ⚠ 見えているのに1ターン動かない ―― そのあいだに地上で潰せば防げる。
            if (a.toDungeon && a.regionId == SurfaceMap.GateId)
            {
                if (a.gateTurns > 0)
                {
                    a.gateTurns--;
                    NotifySystem.Push("<b>" + a.name + "</b>（戦力" + Mathf.RoundToInt(a.power)
                        + "）が<b>迷宮の入口</b>に着いた。次のターン、坑道へ雪崩れ込む", NotifySystem.Kind.Danger, a.regionId);
                    continue;
                }
                pendingAssault += a.power;
                Debug.Log("🏯『雪崩れ込み』" + a.name + "（戦力" + Mathf.RoundToInt(a.power) + "）が迷宮へ入った");
                NotifySystem.Push("<b>" + a.name + " が坑道へ雪崩れ込んだ</b> ― 次の波に加わる", NotifySystem.Kind.Danger);
                all.RemoveAt(i);
                continue;
            }

            // 👁️ 守りの兵は狙う先を持たず、**版図と警戒圏の中だけ**を行き来する。
            //    ⚠ ここで return せずに `PickTarget` に落とすと、旧仕様の虫食いにそのまま戻る。
            if (a.owner < 0 && a.role == Role.Guard)
            {
                PatrolWatch(a);
                continue;
            }
            // 🏘️ 集落が敵対をやめた／滅んだら、その兵はもう進軍しない
            if (a.owner < 0 && a.realmIndex >= 0)
            {
                var rp = HumanRealm.At(a.realmIndex);
                if (rp == null || rp.destroyed) { Retreat(a, i, "主の集落が無くなった"); continue; }
                if (rp.posture < HumanRealm.Hostile && !a.toDungeon) { a.role = Role.Guard; PatrolWatch(a); continue; }
            }

            if (a.targetId < 0 || !IsStillHostile(a, a.targetId)) a.targetId = PickTarget(a);
            if (a.targetId < 0)
            {
                // ⚠⚠ **ここが「地上を放置しても負けない」の正体だった。**
                //   狙う先（＝こちらの地上の領域）が無くなると、奪還軍は「狙う先が無くなった」と
                //   言って**帰っていた**。つまり版図を全部失ったほうが安全だった。
                //   → 人間の奪還軍は帰らず、**迷宮そのものへ向かう**。
                //   他の魔王(owner>=0)は土地が欲しいだけなので、これまでどおり引き上げる。
                int gate = SurfaceMap.GateId;
                if (a.owner < 0 && gate >= 0 && !a.toDungeon)
                {
                    a.toDungeon = true; a.targetId = gate; a.gateTurns = 1;
                    Debug.Log("🏯『矛先が迷宮へ』" + a.name + " は奪う土地が無くなり、坑道そのものを目指しはじめた");
                    NotifySystem.Push("<b>" + a.name + " の矛先が迷宮へ向いた</b> ― 奪い返す土地が無くなった",
                        NotifySystem.Kind.Danger, a.regionId);
                }
                else { Retreat(a, i, "狙う先が無くなった"); continue; }
            }

            var tgt = SurfaceMap.Get(a.targetId);
            while (a.mp > 0)
            {
                // 隣り合ったら攻城。⚠ ただし**迷宮の入口へ向かっている軍は攻城しない**（そのまま入る）
                if (SurfaceMap.HexDist(SurfaceMap.Get(a.regionId), tgt) <= 1)
                {
                    if (a.toDungeon) { a.regionId = a.targetId; break; }
                    Assault(a, i, turn); break;
                }
                // 🚧 支配地域：眷属の隣では足が止まる
                if (InKinZoC(a.regionId))
                {
                    Debug.Log($"🚧『足止め』{a.name} は {SurfaceMap.Get(a.regionId).name} で眷属に睨まれて動けない");
                    break;
                }
                int nxt = a.toDungeon ? NextStepToGate(a, a.targetId) : NextStep(a, a.targetId);
                if (nxt < 0) { a.idleTurns++; break; }
                // 🏯 入口そのものへ踏み込む1歩なら、そこで止まって数える
                if (a.toDungeon && nxt == a.targetId) { a.regionId = nxt; a.idleTurns = 0; break; }
                int cost = SurfaceMap.MoveCost(SurfaceMap.Get(nxt));
                if (cost > a.mp) break;
                a.regionId = nxt; a.mp -= cost; a.idleTurns = 0;
                // 🔥 こちらの版図に踏み込んだら荒らす（奪いはしないが、産出が止まる）
                var stepped = SurfaceMap.Get(nxt);
                if (stepped != null && stepped.owned && stepped.settle == SurfaceMap.Settle.None)
                {
                    if (stepped.pillagedTurns <= 0)
                        NotifySystem.Push("<b>" + stepped.name + " が荒らされている</b>（" + OwnerName(a)
                            + "）― 産出が止まる", NotifySystem.Kind.Loss, stepped.id);
                    stepped.pillagedTurns = PillageTurns;
                }
            }
            // ⚠ **迷宮へ向かう軍は引き上げない。** 引き上げると「放置しても負けない」に逆戻りする。
            //   ⚠⚠ ただし **道が本当に無い軍は畳む。** 実測：海を挟んだ集落から出た軍が
            //     `NextStepToGate` で経路を見つけられず、停滞14ターンで盤に居座り続け、
            //     **進軍の枠を1つ永久に潰していた**（3枠のうち1枠が死に、脅威が2/3に減る）。
            //     引き上げても集落側がまた出すので「放置しても負けない」には戻らない。
            if (a.idleTurns >= (a.toDungeon ? 6 : 3) && i < all.Count && all.Contains(a))
                Retreat(a, all.IndexOf(a), a.toDungeon ? "迷宮への道が見つからない" : "道が塞がれた");
        }
    }

    /// <summary>
    /// 👁️ 守りの兵の1ターン。版図の中をゆっくり動くだけで、警戒圏の外へは出ない。
    /// ⚠ 中心にいる1体目は**動かさない**（Civ VII の独立勢力の守備兵と同じ＝門番）。
    /// </summary>
    private static void PatrolWatch(Army a)
    {
        var p = HumanRealm.At(a.realmIndex);
        if (p == null) return;
        if (a.regionId == p.regionId && CountOfRealm(a.realmIndex) <= 1) return;   // 🏯 門番は動かない

        var cands = new List<SurfaceMap.Region>();
        foreach (var n in SurfaceMap.Neighbors(a.regionId))
        {
            if (!SurfaceMap.IsPassable(n)) continue;
            if (At(n.id) != null) continue;
            if (n.owned) continue;                       // こちらの領域には踏み込まない（守りなので）
            if (!WithinWatch(a, n.id)) continue;
            cands.Add(n);
        }
        if (cands.Count == 0) return;
        var pick = cands[Random.Range(0, cands.Count)];
        a.regionId = pick.id;
    }

    private static bool IsStillHostile(Army a, int id)
    {
        var r = SurfaceMap.Get(id);
        if (a.owner < 0) return r.owned;
        return r.owned || (r.owner != SurfaceMap.OwnerRivalBase + a.owner && r.owner != SurfaceMap.OwnerNeutral);
    }

    /// <summary>攻城：隣り合った目標を攻める。勝てば占領してそこへ入り、負ければ削れて下がる。</summary>
    private static void Assault(Army a, int index, int turn)
    {
        var tgt = SurfaceMap.Get(a.targetId);
        // 🏯 迷宮の入口だけは地上の軍では落とせない（そこは迷宮側の防衛戦で決着する）
        if (tgt.type == SurfaceMap.RegionType.Gate) { a.targetId = -1; return; }
        float atk = a.power * Random.Range(0.85f, 1.15f);
        float def = SurfaceMap.DefenseOf(tgt.id);
        tgt.lastResultTurn = turn;
        if (atk > def)
        {
            bool wasMine = tgt.owned;
            // ⚠ そのタイルに軍団がいたら**押し出す**。やらないと敵が軍団の上に乗って共存する（U-3で判明）。
            LegionRoster.OnTileOverrun(tgt.id, OwnerName(a));
            // 🏘️ 落とした先が**こちらの拠点**なら、その拠点の版図も一緒に持っていかれる。
            //    ⚠ Civ VII と同じ「所有は集落に属する」を、こちら側にも同じ形で適用する
            //      （敵にだけ集落の作法を課して、自分は1枚ずつ削られる、では筋が通らない）。
            int newOwner = a.owner < 0 ? SurfaceMap.OwnerHumanBase + Mathf.Max(0, a.realmIndex)
                                       : SurfaceMap.OwnerRivalBase + a.owner;
            if (a.owner < 0 && a.realmIndex < 0) newOwner = SurfaceMap.OwnerNeutral;
            int carried = 0;
            if (wasMine && tgt.settle != SurfaceMap.Settle.None)
            {
                foreach (var t in SurfaceMap.All)
                    if (t.owned && t.homeSettlement == tgt.id && t.id != tgt.id)
                    { SurfaceMap.SetOwner(t.id, newOwner); carried++; }
            }
            SurfaceMap.SetOwner(tgt.id, newOwner);
            tgt.lastResult = (a.owner < 0 ? "奪還された" : RivalLords.NameOf(a.owner) + "に奪われた");
            a.regionId = tgt.id; a.targetId = -1;
            a.power *= 0.75f;
            if (carried > 0)
                NotifySystem.Push("<b>" + tgt.name + " が落ちた</b> ― 版図 <b>" + carried + " タイル</b>も一緒に失った",
                    NotifySystem.Kind.Loss, tgt.id);
            if (wasMine)
            {
                KinRoster.OnRegionLost(tgt.id, OwnerName(a));
                Debug.Log($"🔥『領域を奪われた』{OwnerName(a)} が {tgt.name} を落とした（敵{atk:0} vs 守り{def:0}）");
                NotifySystem.Push($"<b>{tgt.name} を奪われた</b>（{OwnerName(a)}・敵{atk:0} vs 守り{def:0}）", NotifySystem.Kind.Loss, tgt.id);
            }
            else Debug.Log($"⚔️『{OwnerName(a)}』が {tgt.name} を制圧");
        }
        else
        {
            tgt.lastResult = (a.owner < 0 ? "奪還軍" : RivalLords.NameOf(a.owner)) + "の攻撃を撃退";
            a.power *= 0.7f;
            a.targetId = -1;
            Debug.Log($"🛡️『防衛成功』{tgt.name} が {OwnerName(a)} を退けた（敵{atk:0} vs 守り{def:0}）");
            if (tgt.owned) NotifySystem.Push($"{tgt.name} が {OwnerName(a)} を<b>退けた</b>", NotifySystem.Kind.Gain, tgt.id);
            if (a.power < 50f) Retreat(a, index, "壊滅寸前");
        }
    }

    private static void Retreat(Army a, int index, string why)
    {
        EnsureInit();
        int at = all.IndexOf(a);
        if (at < 0) return;
        if (a.owner >= 0) RivalLords.Get(a.owner).power += a.power * 0.6f;   // 本体に還る
        else StartHumanCooldown();                                            // ⏳ 人間側は次を出すまで間を置く
        all.RemoveAt(at);
        Debug.Log($"↩️『引き上げ』{a.name} が退いた（{why}）");
    }

    /// <summary>
    /// 外から軍を消す（U-3：軍団の会戦で討ち取ったとき）。
    /// ⚠ `Retreat` は private かつ「本体に還す／クールダウン」の後始末が要るので、
    ///   `all.Remove` を呼び出し側にやらせない。**消え方は1箇所に集める。**
    /// </summary>
    public static bool BreakArmy(Army a, string why)
    {
        EnsureInit();
        int at = all.IndexOf(a);
        if (at < 0) return false;
        Retreat(a, at, why);
        return true;
    }

    // ============ 迎撃（こちらから叩く） ============
    /// <summary>眷属が軍を叩く。勝てば軍は消え、負ければ眷属が傷つく。タイルは取らない。</summary>
    public static bool ResolveIntercept(KinRoster.Kin k, Army a)
    {
        EnsureInit();
        float mine = KinRoster.ArmyPower(k) * KinPromotion.AssaultMult(k);
        float theirs = a.power * Random.Range(0.9f, 1.1f);
        int at = all.IndexOf(a);
        if (mine > theirs)
        {
            if (at >= 0) all.RemoveAt(at);
            if (a.owner < 0) StartHumanCooldown();                            // ⏳ 撃退したぶんの息継ぎ
            var res = DungeonResourceManager.Instance;
            int loot = Mathf.RoundToInt(a.power * 1.2f);
            if (res != null) { res.AddDP(loot); res.AddMaterial(6); }
            KinPromotion.AddMerit(k, 3, "野戦で軍を破った");
            MinionRank.OnSurfaceKill(k.individualId);       // 👑 段4『タイラント』の門（→ [[MinionRank]]）
            KinRoster.ReportFieldBattle(k, theirs, true);   // 📈 野戦でも育つ
            Debug.Log($"⚔️『迎撃成功』{k.trueName} が {a.name} を撃ち破った（{mine:0} vs {theirs:0}・+{loot}DP）");
            NotifySystem.Push($"『{k.trueName}』が {a.name} を<b>撃ち破った</b>（+{loot}DP）", NotifySystem.Kind.Gain, k.regionId);
            return true;
        }
        a.power *= 0.8f;
        KinRoster.ReportFieldBattle(k, theirs, false);
        k.injuryTurns = Mathf.Max(k.injuryTurns, 2);
        Debug.Log($"⚔️『迎撃失敗』{k.trueName} は {a.name} に押し返された（{mine:0} vs {theirs:0}・2ターン負傷）");
        NotifySystem.Push($"『{k.trueName}』が {a.name} に<b>押し返された</b>（2ターン負傷）", NotifySystem.Kind.Loss, k.regionId);
        return false;
    }
}
