using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⚔️ 軍団（Legion）── 地上に並べる戦闘ユニット（U-1）。
///
/// **なぜ要るか**：地上の駒は「眷属（真名を持つ個体）」しか無く、盤の上に3〜4体しか立たなかった。
/// Civ のような**戦線の押し引き**は、駒が並んで初めて成立する。
/// 眷属は Civ でいう **司令官(Commander)** に相当するので、足りないのは**司令官が率いる中身**。
///
/// **設計の要点**
/// - 軍団は `MinionCatalog` の34種から作る。**迷宮で育てた進化ツリーがそのまま地上の強さになる**
///   （＝迷宮側の投資が地上に直結する。別の育成軸を増やさない）。
/// - ⚠ 軍団は**個体(`MinionRoster.Individual`)を消費しない**。消費すると盤に20体並べた時点で
///   ロスターが空になり、迷宮に置く駒が無くなる。軍団は「種＋練度」で作る抽象。
///   個体は今までどおり迷宮の配置と眷属化に使う。
/// - 役割(`MinionCatalog.Role`)から**兵科**を導く。前衛は殴られ役、射手は後ろから撃つ＝
///   **並べる意味**が出る。
///
/// U-1 の範囲：実体・移動・ZoC・盤への描画。生産キューと維持費は U-2、戦闘の兵科差は U-3。
/// 関連: [[KinRoster]]（司令官）[[EnemyForce]]（敵軍）[[SurfaceMap]] [[SurfaceView]]。
/// </summary>
public static class LegionRoster
{
    /// <summary>兵科。`MinionCatalog.Role` から導く（別々に持つと必ずズレる）。</summary>
    public enum Cls { Van, Assault, Archer, Caster }

    public class Legion
    {
        public int id;
        public int catalogIndex;      // 配下の種
        public int level = 1;         // 練度（配下の個体Lvに相当）
        public int strength = 100;    // 残兵 0..100（損耗。0で壊滅）
        public int regionId = -1;
        public int marchTarget = -1;  // 進軍先（-1＝待機）
        public int mp = -1;           // 今ターンの残り移動力（-1＝満タン）
        public int commanderKinId = -1;   // 属する司令官の個体ID（U-3で指揮ボーナスに使う）
        public int exp;               // 🎖️ 歴戦（U-4）。会戦と攻城で貯まり、練度が上がる
        public bool foughtThisTurn;   // このターン戦ったか（戦ったターンは補給が入らない）
    }

    // ⚠ readonly にしない。[[SaveSystem]] は readonly を「カタログ＝保存しない」の目印に使うので、
    //    readonly のままだと軍団がセーブに乗らない（[[DungeonFloorManager.floors]] で一度踏んだ）。
    private static List<Legion> all;
    private static int nextId = 1;
    private static void EnsureInit() { if (all == null) all = new List<Legion>(); }

    public static void Reset() { all = new List<Legion>(); builds = new List<Build>(); nextId = 1; starving = false; }
    public static IReadOnlyList<Legion> All { get { EnsureInit(); return all; } }
    public static int Count { get { EnsureInit(); return all.Count; } }

    // ============ 兵科と能力値 ============
    public static Cls ClassOf(int catalogIndex)
    {
        switch (MinionCatalog.Get(catalogIndex).role)
        {
            case MinionCatalog.Role.Tank:   return Cls.Van;
            case MinionCatalog.Role.Ranged: return Cls.Archer;
            case MinionCatalog.Role.Melee:  return Cls.Assault;
            default:                        return Cls.Caster;   // Buff/Debuff
        }
    }
    public static Cls ClassOf(Legion l) => ClassOf(l.catalogIndex);

    public static string ClassName(Cls c)
        => c == Cls.Van ? "前衛" : c == Cls.Assault ? "突撃" : c == Cls.Archer ? "射手" : "術者";
    public static string ClassHex(Cls c)
        => c == Cls.Van ? "#8cb8e6" : c == Cls.Assault ? "#e05a5a" : c == Cls.Archer ? "#5cc47c" : "#b48be6";

    /// <summary>射程（タイル）。0＝隣接のみ。射手と術者は1つ後ろから撃てる＝前衛で守る意味が出る。</summary>
    public static int RangeOf(Cls c) => (c == Cls.Archer || c == Cls.Caster) ? 1 : 0;
    public static int RangeOf(Legion l) => RangeOf(ClassOf(l));

    /// <summary>移動力。獣は速い／前衛は重い（Civの騎兵3・歩兵2に相当）。</summary>
    public static int MovementOf(Legion l)
    {
        var d = MinionCatalog.Get(l.catalogIndex);
        int m = 2;
        if (d.family == ZombieAI.Species.Beast) m += 1;                 // 🐺 獣は速い（Civの騎兵3に相当）
        if (ResearchState.IsResearched("s_logistics")) m += 1;          // 兵站
        m += EraSystem.MoveBonus;                                       // 📜 誓約『軍旅の誓い』
        m += PolicySystem.KinMoveBonus;                                 // 🏛️ 政体／祝祭
        return Mathf.Max(1, m);
    }
    public static int MpOf(Legion l) => l == null ? 0 : (l.mp < 0 ? MovementOf(l) : l.mp);

    /// <summary>
    /// 戦力。**迷宮側の投資（進化段階・練度・魔王の格）がそのまま乗る**。
    /// ⚠ ここに新しい倍率を足すときは、迷宮の `SpawnDefender` と二重計上にならないか確認する。
    /// </summary>
    public static float PowerOf(Legion l)
    {
        if (l == null) return 0f;
        var d = MinionCatalog.Get(l.catalogIndex);
        float baseP = 40f * d.hpMult * d.atkMult;                    // 種の素の強さ
        float lv = MinionRoster.LevelMult(l.level);                  // 練度
        float evo = MinionEvolution.DepthMult(l.catalogIndex);       // 進化段階
        float dl = DemonLord.Instance != null ? DemonLord.Instance.MinionPowerMult : 1f;   // 👑 魔王の格
        return baseP * lv * evo * dl * (l.strength / 100f);
    }

    /// <summary>
    /// 🔨 編成に要る**生産力**（U-2）。拠点が毎ターン積んで、貯まったら完成する（Civの生産と同じ）。
    /// ⚠ 即時購入にしない。即時だと「DPが余ったら好きなだけ並べる」になり、
    ///   **戦線を作るのに要る時間**という判断が消える。
    /// </summary>
    public static int BuildCostOf(int catalogIndex) => 20 + MinionCatalog.Get(catalogIndex).tierCP * 8;
    /// <summary>着工に要る初期費用（DP）。生産力とは別に、始める決断のコスト。</summary>
    public static int DpCostOf(int catalogIndex) => MinionCatalog.Get(catalogIndex).tierCP * 12;
    /// <summary>毎ターンの維持費（素材）。並べ放題にしないための蓋。</summary>
    public static int UpkeepOf(Legion l) => 1 + MinionCatalog.Get(l.catalogIndex).tierCP / 8;
    public static int TotalUpkeep
    {
        get { EnsureInit(); int n = 0; foreach (var l in all) n += UpkeepOf(l); return n; }
    }

    /// <summary>
    /// 同時に持てる軍団の数。拠点が増えるほど並べられる＝**地上を耕す理由**になる。
    /// ⚠ 上限を作らないと、素材さえあれば盤が軍団で埋まる。Civ も維持費で実質の蓋をしている。
    /// </summary>
    public static int Cap
    {
        get
        {
            int n = 3 + SettlementSystem.SettlementCount * 2 + SettlementSystem.CityCount;
            if (ResearchState.IsResearched("s_logistics")) n += 2;   // 兵站
            if (ResearchState.IsResearched("s_conquer")) n += 2;     // 簒奪の作法
            return n;
        }
    }

    public static string NameOf(Legion l)
        => MinionCatalog.Get(l.catalogIndex).jpName + "軍団";

    // ============ 🔨 生産（U-2） ============
    /// <summary>拠点で進行中の建造。1拠点1件（Civの都市の生産と同じ）。</summary>
    public class Build
    {
        public int regionId;
        public int catalogIndex;
        public int progress;
    }
    private static List<Build> builds;      // ⚠ readonly にしない（[[SaveSystem]] が保存しなくなる）
    private static void EnsureBuilds() { if (builds == null) builds = new List<Build>(); }
    public static IReadOnlyList<Build> Builds { get { EnsureBuilds(); return builds; } }
    public static Build BuildAt(int regionId)
    {
        EnsureBuilds(); foreach (var b in builds) if (b.regionId == regionId) return b; return null;
    }

    /// <summary>
    /// その拠点が1ターンに積める生産力。人口が中心＝**拠点を育てるほど早く兵が出る**。
    /// ⚠ 面積ではなく人口に紐づける（面積に比例させると版図を広げただけで生産が爆発する）。
    /// </summary>
    public static int ProductionAt(int regionId)
    {
        var r = SurfaceMap.Get(regionId);
        if (r == null || !r.owned || r.settle == SurfaceMap.Settle.None) return 0;
        int p = 3 + r.pop * 2;
        if (r.settle == SurfaceMap.Settle.City) p += 3;
        p += DistrictCatalog.DefenseBonusAt(regionId) > 0 ? 2 : 0;   // 🏛️ 兵舎のある拠点は兵を出しやすい
        p += DistrictCatalog.ProductionBonusAt(regionId);            // 🔨 造兵廠（B-1）
        return p;
    }

    public static bool CanStartBuild(int regionId, int catalogIndex, out string why)
    {
        why = "";
        var r = SurfaceMap.Get(regionId);
        if (r == null || !r.owned || r.settle == SurfaceMap.Settle.None) { why = "拠点でないと生産できない"; return false; }
        if (BuildAt(regionId) != null) { why = "この拠点は既に何かを造っている"; return false; }
        if (!MinionEvolution.IsUnlocked(catalogIndex)) { why = "その種はまだ解禁されていない"; return false; }
        if (Count + builds.Count >= Cap) { why = "軍団の上限（" + Cap + "）に届いている。拠点を増やすこと"; return false; }
        int dp = DpCostOf(catalogIndex);
        var res = DungeonResourceManager.Instance;
        if (res != null && res.DungeonPoints < dp) { why = "DPが足りない（要" + dp + "）"; return false; }
        return true;
    }

    public static bool TryStartBuild(int regionId, int catalogIndex)
    {
        EnsureBuilds();
        string why;
        if (!CanStartBuild(regionId, catalogIndex, out why)) { Debug.LogWarning("⚠️ " + why); return false; }
        int dp = DpCostOf(catalogIndex);
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(dp)) return false;
        builds.Add(new Build { regionId = regionId, catalogIndex = catalogIndex });
        int turns = Mathf.CeilToInt(BuildCostOf(catalogIndex) / (float)Mathf.Max(1, ProductionAt(regionId)));
        Debug.Log($"🔨『着工』{MinionCatalog.Get(catalogIndex).jpName}軍団 を {SurfaceMap.Get(regionId).name} で（-{dp}DP・約{turns}ターン）");
        NotifySystem.Push($"<b>{MinionCatalog.Get(catalogIndex).jpName}軍団</b> の生産を開始（約{turns}ターン）", NotifySystem.Kind.Gain, regionId);
        return true;
    }

    public static bool CancelBuild(int regionId)
    {
        EnsureBuilds();
        var b = BuildAt(regionId); if (b == null) return false;
        builds.Remove(b);
        Debug.Log($"🛑『取りやめ』{SurfaceMap.Get(regionId).name} の生産を止めた");
        return true;
    }

    /// <summary>生産の進行と完成。ターンの解決から呼ぶ。</summary>
    private static void TickBuilds()
    {
        EnsureBuilds();
        for (int i = builds.Count - 1; i >= 0; i--)
        {
            var b = builds[i];
            var r = SurfaceMap.Get(b.regionId);
            // 拠点を失ったら生産も消える（奪われた土地では造れない）
            if (r == null || !r.owned || r.settle == SurfaceMap.Settle.None)
            {
                Debug.Log("🛑『生産中止』拠点を失ったため生産が止まった");
                builds.RemoveAt(i); continue;
            }
            b.progress += ProductionAt(b.regionId);
            if (b.progress < BuildCostOf(b.catalogIndex)) continue;

            // 完成：拠点そのものが埋まっていれば、空いている隣へ出す
            int place = At(b.regionId) == null && SurfaceMap.IsPassable(r) ? b.regionId : -1;
            if (place < 0)
                foreach (var n in SurfaceMap.Neighbors(b.regionId))
                    if (n.owned && SurfaceMap.IsPassable(n) && At(n.id) == null) { place = n.id; break; }
            if (place < 0) continue;   // 置き場が無ければ完成を待たせる（進捗はそのまま）

            var l = new Legion
            {
                id = nextId++, catalogIndex = b.catalogIndex, regionId = place,
                level = MinionRoster.SummonLevel(),
            };
            EnsureInit(); all.Add(l);
            builds.RemoveAt(i);
            Debug.Log($"⚔️『完成』{NameOf(l)}（{ClassName(ClassOf(l))}・Lv{l.level}）が {SurfaceMap.Get(place).name} に現れた");
            NotifySystem.Push($"<b>{NameOf(l)}</b>（{ClassName(ClassOf(l))}）が完成", NotifySystem.Kind.Gain, place);
        }
    }

    /// <summary>
    /// 💰 維持費の徴収。素材が足りなければ**軍団が痩せる**（Civの解散に相当）。
    /// ⚠ 「払えないと即解散」にすると事故で全滅するので、まず損耗させて警告を出す。
    /// </summary>
    private static void TickUpkeep()
    {
        EnsureInit();
        if (all.Count == 0) return;
        int need = TotalUpkeep;
        var res = DungeonResourceManager.Instance;
        if (res == null) return;
        starving = false;
        if (res.TrySpendMaterial(need)) return;
        starving = true;   // 🏰 払えなかったターンは補給も止まる（痩せながら回復もしない）

        Debug.Log($"⚠️『補給不足』素材が足りず（要{need}）軍団が痩せている");
        NotifySystem.Push($"<b>補給不足</b>　素材が足りない（要{need}）。軍団が痩せていく", NotifySystem.Kind.Loss);
        for (int i = all.Count - 1; i >= 0; i--) Damage(all[i], 12);
    }

    // ============ 編成・解散 ============
    public static Legion Get(int id)
    {
        EnsureInit(); foreach (var l in all) if (l.id == id) return l; return null;
    }
    public static Legion At(int regionId)
    {
        EnsureInit(); foreach (var l in all) if (l.regionId == regionId) return l; return null;
    }

    /// <summary>
    /// 🏗️ 軍団を**即座に**盤へ置く。
    ///
    /// ⚠ 通常の入手経路は `TryStartBuild`（拠点で生産）。こちらは**急ぎで買う**手段で、
    ///   生産力のぶんをDPで肩代わりするので割高にしてある（Civの Gold 購入に相当）。
    ///   即時が安いと「戦線を作るのに要る時間」という判断が消えるので、必ず生産より不利に保つ。
    /// </summary>
    public static Legion TryForm(int catalogIndex, int regionId, out string why)
    {
        EnsureInit();
        why = "";
        var r = SurfaceMap.Get(regionId);
        if (r == null || !r.owned) { why = "自領でないと編成できない"; return null; }
        // ⚠ 自領には山岳のような**通れないタイル**も含まれる。そこで編成すると
        //    どの隣へも移動できず、**永久に動けない軍団**ができる（実測で踏んだ）。
        if (!SurfaceMap.IsPassable(r)) { why = SurfaceMap.TerrainName(r.terrain) + "には軍団を置けない"; return null; }
        if (At(regionId) != null) { why = "そのタイルには既に軍団がいる"; return null; }
        if (!MinionEvolution.IsUnlocked(catalogIndex)) { why = "その種はまだ解禁されていない"; return null; }
        if (Count >= Cap) { why = "軍団の上限（" + Cap + "）に届いている"; return null; }

        int dp = RushCostOf(catalogIndex);
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(dp)) { why = "DPが足りない（要" + dp + "）"; return null; }

        var l = new Legion
        {
            id = nextId++, catalogIndex = catalogIndex, regionId = regionId,
            level = MinionRoster.SummonLevel(),      // 🌱 新兵は世界水準で出る（迷宮の召喚と同じ規則）
        };
        all.Add(l);
        Debug.Log($"⚔️『即時編成』{NameOf(l)}（{ClassName(ClassOf(l))}・Lv{l.level}・戦力{PowerOf(l):0}）を {r.name} に（-{dp}DP）");
        NotifySystem.Push($"<b>{NameOf(l)}</b>（{ClassName(ClassOf(l))}）を {r.name} に配備", NotifySystem.Kind.Gain, regionId);
        return l;
    }

    /// <summary>即時編成のDP。着工費＋生産力ぶんの割高な肩代わり。</summary>
    public static int RushCostOf(int catalogIndex) => DpCostOf(catalogIndex) + BuildCostOf(catalogIndex) * 25;

    public static bool Disband(int id)
    {
        EnsureInit();
        for (int i = 0; i < all.Count; i++)
            if (all[i].id == id)
            {
                Debug.Log($"🕊️『解散』{NameOf(all[i])} を解散した");
                all.RemoveAt(i); return true;
            }
        return false;
    }

    /// <summary>損耗させる。0になったら盤から消える。</summary>
    public static void Damage(Legion l, int amount)
    {
        if (l == null) return;
        l.strength -= Mathf.Max(0, amount);
        if (l.strength > 0) return;
        Debug.Log($"💀『壊滅』{NameOf(l)} が失われた");
        NotifySystem.Push($"<b>{NameOf(l)}</b> が<b>壊滅</b>した", NotifySystem.Kind.Loss, l.regionId);
        EnsureInit(); all.Remove(l);
    }

    // ============ 🚧 支配地域（ZoC） ============
    /// <summary>
    /// そのタイルが**こちらの軍団に睨まれている**か。敵軍はここで足が止まる。
    /// ⚠ 眷属だけを見ていた `EnemyForce.InKinZoC` と合わせて使う（軍団も戦線を張れないと
    ///   「並べる」意味が半分になる）。
    /// </summary>
    public static bool InZoC(int regionId)
    {
        EnsureInit();
        foreach (var n in SurfaceMap.Neighbors(regionId))
        {
            var l = At(n.id);
            if (l != null && l.strength > 0) return true;
        }
        return false;
    }

    /// <summary>そのタイルの守備に足される戦力（駐留している軍団）。</summary>
    public static float GarrisonPowerAt(int regionId)
    {
        var l = At(regionId);
        return l != null ? PowerOf(l) * KinRoster.GarrisonBonus : 0f;
    }

    // ============ ⚔️ 兵科の相性（U-3） ============
    /// <summary>
    /// 三すくみ。**突撃は後衛を食い、前衛は突撃を受け止め、射手は前衛を削る**。
    /// ⚠ 数値を細かく分けない。3本の矢印だけにしておくと、盤を見た瞬間に
    ///   「どれをどこへ当てるか」が読める。読めない相性表は無いのと同じ。
    /// </summary>
    public static float CounterMult(Cls atk, Cls def)
    {
        bool defBack = def == Cls.Archer || def == Cls.Caster;
        bool atkBack = atk == Cls.Archer || atk == Cls.Caster;
        if (atk == Cls.Assault && defBack) return 1.5f;      // 突撃 → 後衛
        if (atk == Cls.Van && def == Cls.Assault) return 1.4f;   // 前衛 → 突撃
        if (atkBack && def == Cls.Van) return 1.3f;          // 射手・術者 → 前衛
        return 1f;
    }

    /// <summary>相性の説明（UIとツールチップで同じ文を使う）。</summary>
    public static string CounterHint(Cls c)
    {
        switch (c)
        {
            case Cls.Van:     return "突撃に強い／射手に弱い";
            case Cls.Assault: return "射手・術者に強い／前衛に弱い";
            case Cls.Archer:  return "前衛に強い／突撃に弱い（射程1）";
            default:          return "前衛に強い／突撃に弱い（射程1）";
        }
    }

    // ============ 🎖️ 司令官の指揮（U-3） ============
    /// <summary>指揮の届く距離。昇進『号令』で1つ伸びる。</summary>
    public static int CommandRadiusOf(KinRoster.Kin k) => KinPromotion.Has(k, 6) ? 2 : 1;

    /// <summary>
    /// そのタイルに届いている指揮の倍率（届いていなければ1）。
    /// ⚠ **重ねない**（一番強い司令官のぶんだけ）。重ねると司令官を固めるだけの作業になるうえ、
    ///   掛け算の軸が増える → [[difficulty-curve-orders]]。上限は 1.20。
    /// </summary>
    public static float CommandMultAt(int regionId)
    {
        var here = SurfaceMap.Get(regionId);
        if (here == null) return 1f;
        float best = 1f;
        foreach (var k in KinRoster.All)
        {
            if (k.regionId < 0 || k.injuryTurns > 0) continue;
            var kr = SurfaceMap.Get(k.regionId);
            if (kr == null) continue;
            if (SurfaceMap.HexDist(kr, here) > CommandRadiusOf(k)) continue;
            float m = 1.12f + (KinPromotion.Has(k, 8) ? 0.08f : 0f);   // 🎖️『軍旗』
            if (m > best) best = m;
        }
        return best;
    }

    /// <summary>そのタイルへ指揮を届かせている司令官（UIで名前を出すため）。</summary>
    public static KinRoster.Kin CommanderAt(int regionId)
    {
        var here = SurfaceMap.Get(regionId);
        if (here == null) return null;
        KinRoster.Kin best = null; float bestM = 1f;
        foreach (var k in KinRoster.All)
        {
            if (k.regionId < 0 || k.injuryTurns > 0) continue;
            var kr = SurfaceMap.Get(k.regionId);
            if (kr == null || SurfaceMap.HexDist(kr, here) > CommandRadiusOf(k)) continue;
            float m = 1.12f + (KinPromotion.Has(k, 8) ? 0.08f : 0f);
            if (best == null || m > bestM) { best = k; bestM = m; }
        }
        return best;
    }

    /// <summary>実戦で使う戦力（相性と指揮を掛けたもの）。</summary>
    public static float BattlePowerOf(Legion l, Cls against)
        => PowerOf(l) * CounterMult(ClassOf(l), against) * CommandMultAt(l.regionId);

    // ============ 🎖️ 歴戦（U-4） ============
    /// <summary>次の練度までに要る歴戦値。上げるほど重い（生き延びた軍団ほど値打ちが出る）。</summary>
    public static int ExpNeed(int level) => 60 + Mathf.Clamp(level, 1, MinionRoster.MaxLevel) * 22;

    /// <summary>
    /// 歴戦を足す。**戦って生き延びた軍団だけが育つ**＝盤の上の1体を守る理由になる。
    /// ⚠ 与えたダメージに比例させない。強い相手ほど削れないので、それだと格上と戦うほど育たなくなる。
    ///   「戦った回数」と「相手の格」で入れる。
    /// </summary>
    public static void GainExp(Legion l, int amount, string why)
    {
        if (l == null || amount <= 0) return;
        l.exp += amount;
        while (l.level < MinionRoster.MaxLevel && l.exp >= ExpNeed(l.level))
        {
            l.exp -= ExpNeed(l.level);
            l.level++;
            Debug.Log($"🎖️『歴戦』{NameOf(l)} の練度が Lv{l.level} に上がった（{why}）");
            NotifySystem.Push($"<b>{NameOf(l)}</b> の練度が <b>Lv{l.level}</b> に（{why}）", NotifySystem.Kind.Gain, l.regionId);
        }
        if (l.level >= MinionRoster.MaxLevel) l.exp = 0;
    }

    /// <summary>相手の格に応じた歴戦値（負けても半分は入る）。</summary>
    private static int BattleExp(float enemyPower, bool won)
    {
        int e = Mathf.RoundToInt(14f + Mathf.Log(1f + Mathf.Max(0f, enemyPower) / 60f) * 18f);
        return won ? e : Mathf.Max(4, e / 2);
    }

    // ============ 🏰 補給と回復（U-4） ============
    /// <summary>補給が足りず回復できないターンか（維持費を払えなかった）。</summary>
    private static bool starving;

    /// <summary>
    /// このタイルで1ターンに戻る残兵。**自領でしか癒えない**（Civと同じ）。
    /// ⚠ これが無いと軍団は削られる一方で、数ターンで盤の駒が全部使いものにならなくなる
    ///   （U-3を入れた時点で実際にそうなっていた）。
    /// </summary>
    public static int HealRateAt(int regionId)
    {
        var r = SurfaceMap.Get(regionId);
        if (r == null || !r.owned) return 0;
        int h = 8;
        if (r.settle == SurfaceMap.Settle.Town) h = 15;
        else if (r.settle == SurfaceMap.Settle.City) h = 20;
        if (DistrictCatalog.DefenseBonusAt(regionId) > 0) h += 5;   // 🏛️ 兵舎
        return h;
    }

    /// <summary>回復できない理由（UIに出す。空なら回復できる）。</summary>
    public static string HealBlockReason(Legion l)
    {
        if (l.strength >= 100) return "満員";
        if (starving) return "補給不足";
        if (l.foughtThisTurn) return "交戦中";
        var r = SurfaceMap.Get(l.regionId);
        if (r == null || !r.owned) return "自領の外";
        return "";
    }

    private static void TickSupply()
    {
        EnsureInit();
        foreach (var l in all)
        {
            if (l.strength >= 100) { l.foughtThisTurn = false; continue; }
            if (!starving && !l.foughtThisTurn)
            {
                int h = HealRateAt(l.regionId);
                if (h > 0) l.strength = Mathf.Min(100, l.strength + h);
            }
            l.foughtThisTurn = false;
        }
    }

    // ============ ⚔️ 会戦（U-3） ============
    /// <summary>
    /// 戦力比から損耗（％）を出す。Civ VII の「戦闘力差で被害が決まる」形。
    /// ⚠ 差を線形にすると一撃で消し飛ぶ。**べき乗で圧縮**して、押し引きが数ターン続くようにする。
    /// </summary>
    public static int DamagePercent(float atk, float def)
    {
        if (def <= 0.01f) return 60;
        float r = Mathf.Clamp(atk / Mathf.Max(0.01f, def), 0.2f, 5f);
        // ⚠ 上限を60にすると格上に触れた瞬間に6割溶けて、退く判断をする前に壊滅する（実測）。
        //    50なら最悪でも2ターン残るので、次のターンに下げるという手が成立する。
        return Mathf.Clamp(Mathf.RoundToInt(26f * Mathf.Pow(r, 0.7f)), 5, 50);
    }

    /// <summary>
    /// 🏴 守りを抜かれたタイルにいた軍団の始末（敵の攻城が通ったとき）。
    /// ⚠ これが無いと、**敵が軍団の上に乗って共存する**（実際にそうなっていた）。
    /// 半壊させて隣の自領へ退かせ、退き先が無ければ壊滅。
    /// </summary>
    public static void OnTileOverrun(int regionId, string byWhom)
    {
        var l = At(regionId);
        if (l == null) return;
        Damage(l, 50);
        if (l.strength <= 0) return;                    // Damage の中で壊滅済み
        foreach (var n in SurfaceMap.Neighbors(regionId))
        {
            if (!n.owned || !SurfaceMap.IsPassable(n) || At(n.id) != null) continue;
            l.regionId = n.id; l.marchTarget = -1; l.mp = 0;
            Debug.Log($"↩️『後退』{NameOf(l)} が {byWhom} に押し出され {n.name} へ下がった（残兵{l.strength}）");
            NotifySystem.Push($"<b>{NameOf(l)}</b> が押し出され {n.name} へ後退（残兵{l.strength}）", NotifySystem.Kind.Loss, n.id);
            return;
        }
        Debug.Log($"💀『退路なし』{NameOf(l)} は下がる先が無く討ち取られた");
        Damage(l, 100);
    }

    /// <summary>射程内にいる敵軍のうち、一番近くて一番弱っているもの（＝とどめを優先）。</summary>
    private static EnemyForce.Army FindEnemy(Legion l, int reach, out int dist)
    {
        dist = 99;
        var here = SurfaceMap.Get(l.regionId);
        if (here == null) return null;
        EnemyForce.Army best = null;
        foreach (var a in EnemyForce.All)
        {
            var ar = SurfaceMap.Get(a.regionId);
            if (ar == null) continue;
            int d = SurfaceMap.HexDist(here, ar);
            if (d < 1 || d > reach) continue;
            if (best == null || d < dist || (d == dist && a.power < best.power)) { best = a; dist = d; }
        }
        return best;
    }

    /// <summary>
    /// 🗡️ 戦線の会戦。**射程内の敵軍と自動で撃ち合う**。
    ///
    /// - 前衛・突撃（射程0）は隣接した相手と**殴り合う**（反撃を受ける）。
    /// - 射手・術者（射程1）は**距離2から一方的に削れる**。距離1まで詰められると反撃を受ける。
    ///   ＝前衛を前に置いて射手を下げる、という並べ方そのものが手になる。
    /// ⚠ 敵の足を止めるのは ZoC。止めた相手を削るのがここ。片方だけだと戦線にならない。
    /// </summary>
    public static void ResolveBattles(int turn)
    {
        EnsureInit();
        for (int i = all.Count - 1; i >= 0; i--)
        {
            var l = all[i];
            if (l.strength <= 0) continue;
            int reach = 1 + RangeOf(l);
            int dist;
            var a = FindEnemy(l, reach, out dist);
            if (a == null) continue;

            var myCls = ClassOf(l);
            float mine = BattlePowerOf(l, a.cls);
            float theirs = a.power * Random.Range(0.9f, 1.1f);
            int hit = DamagePercent(mine, theirs);
            float enemyBefore = a.power;
            a.power -= a.power * hit / 100f;
            l.foughtThisTurn = true;   // 🏰 戦ったターンは補給が入らない（前線に置きっぱなしにはできない）

            string where = SurfaceMap.Get(l.regionId).name;
            bool counter = dist <= 1;   // 隣接していれば反撃を食う（射手も前に出れば殴られる）
            if (counter)
            {
                int back = DamagePercent(theirs * CounterMult(a.cls, myCls), mine);
                Damage(l, back);
            }
            Debug.Log($"🗡️『会戦』{NameOf(l)}（{ClassName(myCls)}）→ {a.name}（{ClassName(a.cls)}）"
                + $" 距離{dist}・相性×{CounterMult(myCls, a.cls):0.0}・{hit}%削った"
                + (counter ? "（反撃を受けた）" : "（射程外から一方的に）") + $" @{where}");

            if (l.strength > 0) GainExp(l, BattleExp(enemyBefore, a.power < 40f), "会戦");
            if (a.power < 40f)
            {
                var loot = DungeonResourceManager.Instance;
                int dp = Mathf.RoundToInt(Mathf.Max(0f, a.power) * 1.2f + 40f);
                if (loot != null) { loot.AddDP(dp); loot.AddMaterial(4); }
                var cmd = CommanderAt(l.regionId);
                if (cmd != null) KinPromotion.AddMerit(cmd, 2, "麾下の軍団が敵軍を破った");
                EnemyForce.BreakArmy(a, "戦線に討ち取られた");
                NotifySystem.Push($"<b>{NameOf(l)}</b> が {a.name} を<b>討ち取った</b>（+{dp}DP）", NotifySystem.Kind.Gain, l.regionId);
            }
            else if (l.strength > 0)
            {
                // ⚠ 毎ターンの交戦をトーストに出すとうるさいので、ふだんはログだけ。
                //   ただし**半分を割ったら警告する**。退くか増援を送るかを決める合図になる。
                bool hurt = l.strength <= 50;
                NotifySystem.Push(hurt
                    ? $"<b>{NameOf(l)}</b> が半壊（残兵{l.strength}%）― {a.name} と交戦中"
                    : $"{NameOf(l)} が {a.name} と交戦（{hit}%削った／残兵{l.strength}）",
                    hurt ? NotifySystem.Kind.Loss : NotifySystem.Kind.Info, l.regionId);
            }
        }
    }

    // ============ 移動 ============
    /// <summary>隣へ1歩。地形の重さぶん移動力を使う。⚠ 敵領には**攻めてからでないと**入れない。</summary>
    public static bool TryStep(Legion l, int toRegion, out string why)
    {
        why = "";
        if (l == null) { why = "軍団がいない"; return false; }
        var to = SurfaceMap.Get(toRegion);
        if (to == null) { why = "その先は盤の外"; return false; }
        if (!SurfaceMap.IsPassable(to)) { why = SurfaceMap.TerrainName(to.terrain) + "には入れない"; return false; }
        bool adj = false;
        foreach (var n in SurfaceMap.Neighbors(l.regionId)) if (n.id == toRegion) { adj = true; break; }
        if (!adj) { why = "隣り合っていない"; return false; }
        if (At(toRegion) != null) { why = "そこには既に味方の軍団がいる"; return false; }
        if (!to.owned && to.owner != SurfaceMap.OwnerNeutral) { why = "敵領へは攻めてからでないと入れない"; return false; }
        int cost = SurfaceMap.MoveCost(to);
        if (cost > MpOf(l)) { why = "移動力が足りない（要" + cost + "・残り" + MpOf(l) + "）"; return false; }
        l.mp = MpOf(l) - cost;
        l.regionId = toRegion;
        return true;
    }

    public static bool SetMarchTarget(int legionId, int regionId)
    {
        var l = Get(legionId); if (l == null) return false;
        l.marchTarget = regionId;
        var r = SurfaceMap.Get(regionId);
        Debug.Log($"🗺️『進軍指示』{NameOf(l)} → {(r != null ? r.name : "?")}");
        return true;
    }

    /// <summary>
    /// 目標へ向かう次の1歩。**幅優先で道を引く**。
    ///
    /// ⚠ 以前は「距離が減る隣」だけを見る貪欲法だった。それだと**回り込みができない**。
    ///   実測：司令官のいるタイルの隣6面のうち4面が山岳で、通れる2面の片方が埋まっていたため、
    ///   麾下の軍団が何ターン経っても距離2から動かなかった。進軍指示でも同じことが起きる。
    /// ⚠ 盤は最大1万タイルあるので**全面は探索しない**。目標までの距離＋3の範囲だけ見る。
    ///   軍団の移動は近距離なので、これで足りるうえ探索が盤の大きさに引きずられない。
    /// </summary>
    private static int NextStep(Legion l, int target)
    {
        var start = SurfaceMap.Get(l.regionId);
        var tgt = SurfaceMap.Get(target);
        if (start == null || tgt == null || l.regionId == target) return -1;
        int bound = SurfaceMap.HexDist(start, tgt) + 3;

        var prev = new Dictionary<int, int> { { l.regionId, -1 } };
        var q = new Queue<int>();
        q.Enqueue(l.regionId);
        int found = -1, guard = 0;
        while (q.Count > 0 && found < 0 && guard++ < 3000)
        {
            int cur = q.Dequeue();
            foreach (var n in SurfaceMap.Neighbors(cur))
            {
                if (prev.ContainsKey(n.id)) continue;
                if (!SurfaceMap.IsPassable(n)) continue;
                if (!n.owned && n.owner != SurfaceMap.OwnerNeutral) continue;   // 敵領は素通りできない
                if (n.id != target && At(n.id) != null) continue;               // 味方で塞がった道は避ける
                if (SurfaceMap.HexDist(start, n) > bound) continue;
                prev[n.id] = cur;
                if (n.id == target) { found = n.id; break; }
                q.Enqueue(n.id);
            }
        }
        if (found < 0) return -1;
        int step = found;
        while (prev[step] != l.regionId) step = prev[step];
        return step;
    }

    // ============ 🏰 攻城（U-4：軍団が土地を取る） ============
    /// <summary>
    /// 兵科ごとの攻城の得手不得手。**射手は城攻めに弱い**（Civの遠隔が都市に効きづらいのと同じ）。
    /// ⚠ これが無いと「射手だけ並べれば全部片づく」になり、前衛を作る理由が消える。
    /// </summary>
    public static float SiegeMult(Cls c)
        => c == Cls.Assault ? 1.25f : c == Cls.Van ? 1.0f : c == Cls.Caster ? 0.85f : 0.7f;

    /// <summary>
    /// 🗡️ 側面支援：隣に並んでいる味方の軍団1体につき +8%（最大3体・+24%）。
    /// **横に並べるほど攻めが通る**＝戦線を作る動機そのもの。
    /// </summary>
    public static float FlankBonusAt(int regionId, int exceptLegionId)
    {
        int n = 0;
        foreach (var nb in SurfaceMap.Neighbors(regionId))
        {
            var l = At(nb.id);
            if (l != null && l.id != exceptLegionId && l.strength > 0) n++;
        }
        return 1f + Mathf.Min(3, n) * 0.08f;
    }

    /// <summary>攻城に使う戦力（相性の代わりに攻城適性・指揮・側面が乗る）。</summary>
    public static float SiegePowerOf(Legion l)
        => PowerOf(l) * SiegeMult(ClassOf(l)) * CommandMultAt(l.regionId) * FlankBonusAt(l.regionId, l.id)
         * SyncretismSystem.SiegeMult;   // 🜏 習合『鬼種の血』

    public static bool CanAssault(Legion l, int targetRegion, out string why)
    {
        why = "";
        if (l == null) { why = "軍団がいない"; return false; }
        var t = SurfaceMap.Get(targetRegion);
        if (t == null) { why = "その先は盤の外"; return false; }
        if (t.owned) { why = "そこは既に自領"; return false; }
        if (!SurfaceMap.IsPassable(t)) { why = SurfaceMap.TerrainName(t.terrain) + "は攻められない"; return false; }
        if (t.type == SurfaceMap.RegionType.Gate) { why = "迷宮の入口は地上の軍では落とせない"; return false; }
        bool adj = false;
        foreach (var n in SurfaceMap.Neighbors(l.regionId)) if (n.id == targetRegion) { adj = true; break; }
        if (!adj) { why = "隣り合っていない"; return false; }
        if (MpOf(l) <= 0) { why = "このターンはもう動けない"; return false; }
        if (l.foughtThisTurn) { why = "このターンは既に戦っている"; return false; }
        return true;
    }

    /// <summary>
    /// 🏰 隣の敵領・中立領を攻める。勝てば占領して踏み込む。
    /// ⚠ 眷属の攻城（`KinRoster.ResolveAttack`）とは**別の判定にしない**と役割が被る。
    ///   眷属は「1体で殴り込む英雄」、軍団は「並べて押す線」なので、
    ///   軍団側は**側面支援**と**兵科の攻城適性**で決まるようにしてある。
    /// </summary>
    public static bool TryAssault(int legionId, int targetRegion, out string why)
    {
        var l = Get(legionId);
        if (!CanAssault(l, targetRegion, out why)) return false;
        var t = SurfaceMap.Get(targetRegion);

        float power = SiegePowerOf(l);
        int def = SurfaceMap.DefenseOf(targetRegion);
        float ratio = def > 0 ? power / def : 99f;
        int wasRival = t.IsRival ? t.RivalIndex : -1;
        l.mp = 0; l.foughtThisTurn = true;
        t.lastResultTurn = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 0;

        string cls = ClassName(ClassOf(l));
        if (ratio >= 1.15f)
        {
            Damage(l, 15);
            t.lastResult = "軍団が制圧";
            TakeRegion(l, t, wasRival);
            GainExp(l, BattleExp(def, true), "制圧");
            Debug.Log($"🏰『制圧』{NameOf(l)}（{cls}）が {t.name} を落とした（{power:0} vs 守り{def}）");
            NotifySystem.Push($"<b>{NameOf(l)}</b> が {t.name} を<b>制圧</b>（{power:0} vs {def}）", NotifySystem.Kind.Gain, t.id);
            return true;
        }
        if (ratio >= 0.9f)
        {
            Damage(l, 35);
            if (Get(legionId) == null)
            {
                Debug.Log($"💀『相討ち』{NameOf(l)} は {t.name} を落とす前に消耗しきった");
                return false;
            }
            t.lastResult = "軍団が辛勝";
            TakeRegion(l, t, wasRival);
            GainExp(l, Mathf.RoundToInt(BattleExp(def, true) * 1.2f), "辛勝");
            Debug.Log($"🏰『辛勝』{NameOf(l)}（{cls}）が {t.name} を落とした（{power:0} vs 守り{def}・残兵{l.strength}）");
            NotifySystem.Push($"<b>{NameOf(l)}</b> が {t.name} を<b>辛勝</b>で制圧（残兵{l.strength}%）", NotifySystem.Kind.Gain, t.id);
            return true;
        }
        int hurt = ratio >= 0.6f ? 40 : 60;
        t.lastResult = "軍団の攻撃を撃退";
        GainExp(l, BattleExp(def, false), "攻めあぐねた");
        Damage(l, hurt);
        Debug.Log($"🛡️『攻めあぐね』{NameOf(l)}（{cls}）は {t.name} を落とせなかった（{power:0} vs 守り{def}・-{hurt}%）");
        NotifySystem.Push($"{NameOf(l)} が {t.name} を落とせなかった（-{hurt}%）", NotifySystem.Kind.Loss, t.id);
        why = "守りを抜けなかった";
        return false;
    }

    /// <summary>占領の後始末。⚠ 眷属の制圧と**同じ道を通す**（片方だけ真核や独立勢力の処理が漏れる）。</summary>
    private static void TakeRegion(Legion l, SurfaceMap.Region t, int wasRival)
    {
        SurfaceMap.SetOwner(t.id, SurfaceMap.OwnerSelf);
        KinRoster.OnRegionConquered(t, wasRival);
        l.regionId = t.id; l.marchTarget = -1;
        var cmd = CommanderAt(t.id);
        if (cmd != null) KinPromotion.AddMerit(cmd, wasRival >= 0 ? 4 : 2, "麾下の軍団が土地を取った");
    }

    // ============ 🎖️ 麾下に付ける（パック移動・U-3） ============
    /// <summary>
    /// 軍団を司令官（眷属）の麾下に入れる。`kinIndividualId` に -1 を渡すと独立。
    /// 麾下の軍団は**進軍指示が無ければ司令官に付いて動く**＝
    /// 1体ずつ行き先を指定しなくても戦線がまとまって前進する。
    /// </summary>
    public static bool AttachTo(int legionId, int kinIndividualId)
    {
        var l = Get(legionId); if (l == null) return false;
        if (kinIndividualId >= 0 && KinRoster.Of(kinIndividualId) == null) return false;
        l.commanderKinId = kinIndividualId;
        var k = kinIndividualId >= 0 ? KinRoster.Of(kinIndividualId) : null;
        Debug.Log(k != null
            ? $"🎖️『麾下』{NameOf(l)} を {k.trueName} の指揮下に入れた"
            : $"🎖️『独立』{NameOf(l)} を指揮下から外した");
        return true;
    }

    /// <summary>その司令官の麾下にいる軍団の数。</summary>
    public static int FollowerCount(int kinIndividualId)
    {
        EnsureInit(); int n = 0;
        foreach (var l in all) if (l.commanderKinId == kinIndividualId) n++;
        return n;
    }

    /// <summary>ターンの解決：移動力を戻し、進軍指示があれば歩かせる。麾下は司令官に付いていく。</summary>
    public static void ResolveTurn(int turn)
    {
        EnsureInit();
        TickBuilds();     // 🔨 生産の進行と完成
        TickUpkeep();     // 💰 維持費（足りなければ痩せる）
        TickSupply();     // 🏰 補給（自領で休んでいれば残兵が戻る）
        foreach (var l in all)
        {
            l.mp = MovementOf(l);
            int target = l.marchTarget;
            // 🎖️ 進軍指示が無ければ司令官へ寄る。指揮の届く範囲に入っていれば動かない
            //    （毎ターン同じタイルへ吸い寄せられて重なり待ちになるのを避ける）。
            if (target < 0 && l.commanderKinId >= 0)
            {
                var k = KinRoster.Of(l.commanderKinId);
                if (k == null) l.commanderKinId = -1;              // 司令官が失われたら独立に戻す
                else if (k.regionId >= 0)
                {
                    var kr = SurfaceMap.Get(k.regionId); var lr = SurfaceMap.Get(l.regionId);
                    if (kr != null && lr != null && SurfaceMap.HexDist(kr, lr) > CommandRadiusOf(k))
                        target = k.regionId;
                }
            }
            if (target < 0 || target == l.regionId) { if (l.marchTarget == l.regionId) l.marchTarget = -1; continue; }
            bool following = l.marchTarget < 0;   // 司令官に付いていくだけの移動か
            while (MpOf(l) > 0)
            {
                int nxt = NextStep(l, target);
                if (nxt < 0) break;
                string why;
                if (!TryStep(l, nxt, out why)) break;
                if (l.regionId == target) break;
                // 🎖️ 追従は**指揮が届いたら止める**。司令官のタイルまで詰めると、
                //    1タイル1軍団の制限で後続が団子になり、戦線が線にならない。
                if (following)
                {
                    var k2 = KinRoster.Of(l.commanderKinId);
                    if (k2 != null && SurfaceMap.HexDist(SurfaceMap.Get(k2.regionId), SurfaceMap.Get(l.regionId)) <= CommandRadiusOf(k2)) break;
                }
            }
            if (l.regionId == l.marchTarget) l.marchTarget = -1;
        }
    }

    /// <summary>盤を作り直したときに、盤の外へ出てしまった軍団を畳む。</summary>
    public static void ClampToBoard()
    {
        EnsureInit();
        for (int i = all.Count - 1; i >= 0; i--)
            if (all[i].regionId < 0 || all[i].regionId >= SurfaceMap.Count) all.RemoveAt(i);
    }
}
