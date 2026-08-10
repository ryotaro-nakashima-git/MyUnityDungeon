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
    }

    // ⚠ readonly にしない。[[SaveSystem]] は readonly を「カタログ＝保存しない」の目印に使うので、
    //    readonly のままだと軍団がセーブに乗らない（[[DungeonFloorManager.floors]] で一度踏んだ）。
    private static List<Legion> all;
    private static int nextId = 1;
    private static void EnsureInit() { if (all == null) all = new List<Legion>(); }

    public static void Reset() { all = new List<Legion>(); nextId = 1; }
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

    /// <summary>編成コスト（DPと素材）。素材＝Civの生産力に相当させる。</summary>
    public static int DpCostOf(int catalogIndex) => MinionCatalog.Get(catalogIndex).tierCP * 20;
    public static int MatCostOf(int catalogIndex) => 4 + MinionCatalog.Get(catalogIndex).tierCP / 2;
    /// <summary>毎ターンの維持費（素材）。並べ放題にしないための蓋。→ U-2 で徴収する。</summary>
    public static int UpkeepOf(Legion l) => 1 + MinionCatalog.Get(l.catalogIndex).tierCP / 8;

    public static string NameOf(Legion l)
        => MinionCatalog.Get(l.catalogIndex).jpName + "軍団";

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
    /// 🏗️ 軍団を編成する。**1タイル1軍団**（重ならない）＝戦線が線になる。
    /// U-1 では即時編成。U-2 で「拠点で数ターンかけて生産」に置き換える。
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

        int dp = DpCostOf(catalogIndex), mat = MatCostOf(catalogIndex);
        var res = DungeonResourceManager.Instance;
        if (res != null && res.CraftMaterials < mat) { why = "素材が足りない（要" + mat + "）"; return null; }
        if (res != null && !res.TrySpendDP(dp)) { why = "DPが足りない（要" + dp + "）"; return null; }
        if (res != null) res.TrySpendMaterial(mat);

        var l = new Legion
        {
            id = nextId++, catalogIndex = catalogIndex, regionId = regionId,
            level = MinionRoster.SummonLevel(),      // 🌱 新兵は世界水準で出る（迷宮の召喚と同じ規則）
        };
        all.Add(l);
        Debug.Log($"⚔️『編成』{NameOf(l)}（{ClassName(ClassOf(l))}・Lv{l.level}・戦力{PowerOf(l):0}）を {r.name} で編成（-{dp}DP -{mat}素材）");
        NotifySystem.Push($"<b>{NameOf(l)}</b>（{ClassName(ClassOf(l))}）を {r.name} で編成", NotifySystem.Kind.Gain, regionId);
        return l;
    }

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

    /// <summary>目標へ近づく次の1歩（通れて・味方が居なくて・一番近づく隣）。</summary>
    private static int NextStep(Legion l, int target)
    {
        var cur = SurfaceMap.Get(l.regionId);
        var tgt = SurfaceMap.Get(target);
        if (cur == null || tgt == null) return -1;
        int best = -1, bestD = SurfaceMap.HexDist(cur, tgt);
        foreach (var n in SurfaceMap.Neighbors(l.regionId))
        {
            if (!SurfaceMap.IsPassable(n)) continue;
            if (At(n.id) != null) continue;
            if (!n.owned && n.owner != SurfaceMap.OwnerNeutral) continue;   // 敵領は素通りできない
            int d = SurfaceMap.HexDist(n, tgt);
            if (d < bestD) { bestD = d; best = n.id; }
        }
        return best;
    }

    /// <summary>ターンの解決：移動力を戻し、進軍指示があれば歩かせる。</summary>
    public static void ResolveTurn(int turn)
    {
        EnsureInit();
        foreach (var l in all)
        {
            l.mp = MovementOf(l);
            if (l.marchTarget < 0 || l.marchTarget == l.regionId) { l.marchTarget = -1; continue; }
            while (MpOf(l) > 0)
            {
                int nxt = NextStep(l, l.marchTarget);
                if (nxt < 0) break;
                string why;
                if (!TryStep(l, nxt, out why)) break;
                if (l.regionId == l.marchTarget) break;
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
