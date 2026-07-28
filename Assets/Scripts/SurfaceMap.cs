using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🗺️ 地上（支配領域の外）＝4Xの eXpand/eXploit/eXterminate を担う層。
///
/// 原作の核心『支配領域を増やすポイントは眷属化』を成立させるための盤面。
/// 配下はダンジョンから出られないが、**真名を与えた眷属は配下を率いて外へ出られる**。
/// - 19領域のノードグラフ。自領に隣接する領域から順に見える(探索)→侵攻できる。
/// - 領域には**所有者**がある：中立(人間側) / 自分 / 他魔王。他魔王の本拠地を落とすと真核を奪える＝eXterminate。
/// - 支配した領域は毎ターン DP/素材/研究点 を産出する（ダンジョン内で稼ぐのとは別の収入源）。
/// - **奪い返される**：人間側の奪還軍と他魔王が毎ターン攻めてくる。守るには眷属を駐留させるか砦化する。
/// - 支配が広がるほど『世界水準』が上がる＝ダンジョンに来る冒険者が強くなる（原作の泳がせと同じ両刃）。
///   ※難易度カーブを壊さないよう、寄与は対数＋上限。→ [[difficulty-curve-orders]]
///
/// 純static・実行時保持（ドメインリロードで初期化）。関連: [[KinRoster]] [[RivalLords]] / DungeonTurnManager(ターン解決)。
/// </summary>
public static class SurfaceMap
{
    public enum RegionType { Gate, Village, Forest, Mine, Town, Fort, City, Domain }

    // 所有者。0=中立(人間側) / 1=自分 / 2以上=他魔王(RivalLords index + 2)
    public const int OwnerNeutral = 0;
    public const int OwnerSelf = 1;
    public const int OwnerRivalBase = 2;

    public class Region
    {
        public int id;
        public string name;
        public RegionType type;
        public int defense;                 // 中立時の防衛力（攻略に必要な戦力の目安）
        public int dpYield, matYield, rpYield, fameYield;
        public int[] links;                 // 隣接する領域
        public int owner = OwnerNeutral;
        public int fortLevel;               // 🏯 砦化(0-3)。自領の防衛力を上げる
        public int rivalHome = -1;          // 他魔王の本拠地なら、その魔王index
        public int lastResultTurn = -1;     // 直近で戦闘が起きたターン（UI表示用）
        public string lastResult = "";      // 直近の戦果テキスト

        public bool owned => owner == OwnerSelf;
        public bool IsRival => owner >= OwnerRivalBase;
        public int RivalIndex => owner - OwnerRivalBase;
    }

    private static List<Region> regions;

    private static void EnsureInit() { if (regions == null) Build(); }

    public static void Reset() { regions = null; EnsureInit(); }

    private static void Build()
    {
        regions = new List<Region>
        {
            //  id 名前              種別               防衛  DP  素材  RP 名声  隣接
            R(0,  "迷宮前の荒れ地",  RegionType.Gate,     0,    0,  0,  0,  0,  new[]{1,2,3}),
            R(1,  "灰かぶりの集落",  RegionType.Village,  60,  30,  1,  0,  6,  new[]{0,4,5}),
            R(2,  "霧ざわめく森",    RegionType.Forest,   80,  20,  3,  0,  4,  new[]{0,4,6,16}),
            R(3,  "鉄錆の坑道",      RegionType.Mine,    110,  25,  5,  0,  5,  new[]{0,5,7}),
            R(4,  "東の街道",        RegionType.Village, 160,  55,  2,  1, 10,  new[]{1,2,8}),
            R(5,  "麦守りの里",      RegionType.Village, 190,  70,  2,  1, 11,  new[]{1,3,8,9}),
            R(6,  "古き樹の祠",      RegionType.Forest,  220,  35,  4,  2,  8,  new[]{2,10,16}),
            R(7,  "深層鉱脈",        RegionType.Mine,    260,  45, 10,  1, 10,  new[]{3,9,17}),
            R(8,  "宿場町ラウム",    RegionType.Town,    360, 130,  4,  2, 20,  new[]{4,5,11,12}),
            R(9,  "職人街ヴァル",    RegionType.Town,    420, 110, 12,  2, 22,  new[]{5,7,12,17}),
            R(10, "祈りの丘",        RegionType.Forest,  460,  60,  5,  4, 16,  new[]{6,11,16}),
            R(11, "廃修道院",        RegionType.Fort,    560,  90,  8,  4, 24,  new[]{8,10,13}),
            R(12, "石造りの砦",      RegionType.Fort,    640, 120,  9,  3, 26,  new[]{8,9,13,14,17}),
            R(13, "辺境伯領",        RegionType.Town,    820, 210, 10,  5, 34,  new[]{11,12,15,18}),
            R(14, "騎士団駐屯地",    RegionType.Fort,    900, 150, 16,  4, 36,  new[]{12,15,18}),
            R(15, "城塞都市アルバ",  RegionType.City,   1250, 340, 20,  8, 55,  new[]{13,14}),
            // 🔥 他魔王の支配領域（真核がある本拠地）。落とすと真核を奪える＝その魔王を排除。
            R(16, "紅蓮の坑洞",      RegionType.Domain,  700, 180, 14,  5, 30,  new[]{2,6,10}),
            R(17, "常夜の樹海",      RegionType.Domain,  980, 240, 16,  6, 38,  new[]{7,9,12}),
            R(18, "凍てつく王座",    RegionType.Domain, 1400, 380, 24,  9, 60,  new[]{13,14}),
        };
        regions[0].owner = OwnerSelf; // 迷宮の目の前は最初から自領（進軍の起点）
    }

    private static Region R(int id, string n, RegionType t, int def, int dp, int mat, int rp, int fame, int[] links)
        => new Region { id = id, name = n, type = t, defense = def, dpYield = dp, matYield = mat, rpYield = rp, fameYield = fame, links = links };

    public static int Count { get { EnsureInit(); return regions.Count; } }
    public static Region Get(int id) { EnsureInit(); return regions[Mathf.Clamp(id, 0, regions.Count - 1)]; }
    public static IReadOnlyList<Region> All { get { EnsureInit(); return regions; } }

    /// <summary>自領に隣接していれば『見えている』＝侵攻先に選べる。</summary>
    public static bool IsDiscovered(int id)
    {
        EnsureInit();
        var r = Get(id);
        if (r.owned) return true;
        foreach (var l in r.links) if (regions[l].owned) return true;
        return false;
    }

    public static int OwnedCount { get { EnsureInit(); int n = 0; foreach (var r in regions) if (r.owned && r.type != RegionType.Gate) n++; return n; } }
    public static int CountOwnedBy(int owner) { EnsureInit(); int n = 0; foreach (var r in regions) if (r.owner == owner && r.type != RegionType.Gate) n++; return n; }

    /// <summary>他魔王の本拠地を index から引く（-1なら見つからない）。</summary>
    public static int HomeRegionOfRival(int rivalIndex)
    {
        EnsureInit();
        foreach (var r in regions) if (r.rivalHome == rivalIndex) return r.id;
        return -1;
    }
    public static void AssignRivalHome(int regionId, int rivalIndex)
    {
        var r = Get(regionId);
        r.rivalHome = rivalIndex;
        r.owner = OwnerRivalBase + rivalIndex;
    }

    // ============ 🏯 砦化（自領の防衛力を上げる） ============
    public const int MaxFort = 3;
    private static readonly int[] FortDefense = { 0, 120, 300, 560 };   // 砦レベル→防衛力の加算
    public static int FortCost(int level) => 300 + level * 450;         // 次のレベルにするDP

    /// <summary>その領域の現在の防衛力。中立/他魔王は素の値、自領は砦＋駐留眷属で決まる。</summary>
    public static int DefenseOf(int id)
    {
        var r = Get(id);
        if (!r.owned) return r.defense;
        int d = Mathf.RoundToInt(r.defense * 0.35f) + FortDefense[Mathf.Clamp(r.fortLevel, 0, MaxFort)];
        return d + Mathf.RoundToInt(KinRoster.GarrisonPowerAt(id));
    }

    public static bool TryFortify(int id)
    {
        var r = Get(id);
        if (!r.owned) { Debug.LogWarning("⚠️ 自領以外は砦化できません。"); return false; }
        if (r.fortLevel >= MaxFort) { Debug.LogWarning("⚠️ 既に最大まで砦化されています。"); return false; }
        int cost = FortCost(r.fortLevel);
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(cost)) { Debug.LogWarning($"⚠️ DP不足で砦化できません（要{cost}DP）。"); return false; }
        r.fortLevel++;
        Debug.Log($"🏯『砦化』{r.name} を砦Lv{r.fortLevel} に強化（-{cost}DP・防衛+{FortDefense[r.fortLevel] - FortDefense[r.fortLevel - 1]}）");
        return true;
    }

    /// <summary>領域の所有者が変わる（奪う/奪われる）。砦は落ちるとリセット。</summary>
    public static void SetOwner(int id, int owner)
    {
        var r = Get(id);
        if (r.owner == owner) return;
        r.owner = owner;
        if (owner != OwnerSelf) r.fortLevel = 0;
    }

    public static string OwnerName(int owner)
    {
        if (owner == OwnerSelf) return "自領";
        if (owner >= OwnerRivalBase) return RivalLords.NameOf(owner - OwnerRivalBase);
        return "中立";
    }
    public static string OwnerColor(int owner)
    {
        if (owner == OwnerSelf) return "#5cc47c";
        if (owner >= OwnerRivalBase) return RivalLords.ColorOf(owner - OwnerRivalBase);
        return "#9c95b4";
    }

    public static string TypeName(RegionType t)
    {
        switch (t)
        {
            case RegionType.Village: return "集落";
            case RegionType.Forest: return "森";
            case RegionType.Mine: return "鉱山";
            case RegionType.Town: return "町";
            case RegionType.Fort: return "砦";
            case RegionType.City: return "都市";
            case RegionType.Domain: return "魔王領";
            default: return "拠点";
        }
    }
    public static string TypeColor(RegionType t)
    {
        switch (t)
        {
            case RegionType.Village: return "#e3a94a";
            case RegionType.Forest: return "#5cc47c";
            case RegionType.Mine: return "#9aa3b0";
            case RegionType.Town: return "#8cb8e6";
            case RegionType.Fort: return "#b478e6";
            case RegionType.City: return "#e05a5a";
            case RegionType.Domain: return "#ff6a4a";
            default: return "#6f6889";
        }
    }

    // ============ 毎ターンの産出 ============
    public static void CollectYields()
    {
        EnsureInit();
        var y = YieldSummary();
        if (y.dp == 0 && y.mat == 0 && y.rp == 0) return;
        var res = DungeonResourceManager.Instance;
        if (res != null) { res.AddDP(y.dp); res.AddMaterial(y.mat); res.AddFame(y.fame); }
        if (y.rp > 0) ResearchState.AddRP(y.rp);
        Debug.Log($"🗺️『地上の産出』支配{OwnedCount}領域 → +{y.dp}DP +{y.mat}素材 +{y.rp}RP +{y.fame}名声");
    }

    public static (int dp, int mat, int rp, int fame) YieldSummary()
    {
        EnsureInit();
        int dp = 0, mat = 0, rp = 0, fame = 0;
        foreach (var r in regions)
        {
            if (!r.owned || r.type == RegionType.Gate) continue;
            dp += r.dpYield; mat += r.matYield; rp += r.rpYield; fame += r.fameYield;
        }
        return (dp, mat, rp, fame);
    }

    /// <summary>支配領域による『世界水準』の押し上げ。広げるほど強い冒険者が来る（対数＋上限＝カーブを壊さない）。</summary>
    public static float WorldTierBias => Mathf.Min(1.2f, Mathf.Log(1f + OwnedCount) * 0.5f);
}
