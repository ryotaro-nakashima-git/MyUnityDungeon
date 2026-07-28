using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🗺️ 地上（支配領域の外）＝4Xの eXpand/eXploit を担う層。
///
/// 原作の核心『支配領域を増やすポイントは眷属化』を成立させるための盤面。
/// 配下はダンジョンから出られないが、**真名を与えた眷属は配下を率いて外へ出られる**。
/// - 16領域のノードグラフ。入口(id0)に隣接する領域から順に見える(探索)→侵攻できる。
/// - 支配した領域は毎ターン DP/素材/研究点 を産出する（ダンジョン内で稼ぐのとは別の収入源）。
/// - ただし支配が広がるほど『世界水準』が上がる＝ダンジョンに来る冒険者が強くなる（原作の泳がせと同じ両刃）。
///   ・難易度カーブを壊さないよう、寄与は対数＋上限。→ [[difficulty-curve-orders]]
///
/// 純static・実行時保持（ドメインリロードで初期化）。関連: [[KinRoster]] / DungeonTurnManager(ターン解決)。
/// </summary>
public static class SurfaceMap
{
    public enum RegionType { Gate, Village, Forest, Mine, Town, Fort, City }

    public class Region
    {
        public int id;
        public string name;
        public RegionType type;
        public int defense;                 // 攻略に必要な戦力の目安
        public int dpYield, matYield, rpYield, fameYield;
        public int[] links;                 // 隣接する領域
        public bool owned;                  // 支配済みか
        public int lastResultTurn = -1;     // 直近で戦闘が起きたターン（UI表示用）
        public string lastResult = "";      // 直近の戦果テキスト
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
            R(2,  "霧ざわめく森",    RegionType.Forest,   80,  20,  3,  0,  4,  new[]{0,4,6}),
            R(3,  "鉄錆の坑道",      RegionType.Mine,    110,  25,  5,  0,  5,  new[]{0,5,7}),
            R(4,  "東の街道",        RegionType.Village, 160,  55,  2,  1, 10,  new[]{1,2,8}),
            R(5,  "麦守りの里",      RegionType.Village, 190,  70,  2,  1, 11,  new[]{1,3,8,9}),
            R(6,  "古き樹の祠",      RegionType.Forest,  220,  35,  4,  2,  8,  new[]{2,10}),
            R(7,  "深層鉱脈",        RegionType.Mine,    260,  45, 10,  1, 10,  new[]{3,9}),
            R(8,  "宿場町ラウム",    RegionType.Town,    360, 130,  4,  2, 20,  new[]{4,5,11,12}),
            R(9,  "職人街ヴァル",    RegionType.Town,    420, 110, 12,  2, 22,  new[]{5,7,12}),
            R(10, "祈りの丘",        RegionType.Forest,  460,  60,  5,  4, 16,  new[]{6,11}),
            R(11, "廃修道院",        RegionType.Fort,    560,  90,  8,  4, 24,  new[]{8,10,13}),
            R(12, "石造りの砦",      RegionType.Fort,    640, 120,  9,  3, 26,  new[]{8,9,13,14}),
            R(13, "辺境伯領",        RegionType.Town,    820, 210, 10,  5, 34,  new[]{11,12,15}),
            R(14, "騎士団駐屯地",    RegionType.Fort,    900, 150, 16,  4, 36,  new[]{12,15}),
            R(15, "城塞都市アルバ",  RegionType.City,   1250, 340, 20,  8, 55,  new[]{13,14}),
        };
        regions[0].owned = true; // 迷宮の目の前は最初から自領（進軍の起点）
    }

    private static Region R(int id, string n, RegionType t, int def, int dp, int mat, int rp, int fame, int[] links)
        => new Region { id = id, name = n, type = t, defense = def, dpYield = dp, matYield = mat, rpYield = rp, fameYield = fame, links = links };

    public static int Count { get { EnsureInit(); return regions.Count; } }
    public static Region Get(int id) { EnsureInit(); return regions[Mathf.Clamp(id, 0, regions.Count - 1)]; }
    public static IReadOnlyList<Region> All { get { EnsureInit(); return regions; } }

    /// <summary>支配済みの領域に隣接していれば『見えている』＝侵攻先に選べる。</summary>
    public static bool IsDiscovered(int id)
    {
        EnsureInit();
        var r = Get(id);
        if (r.owned) return true;
        foreach (var l in r.links) if (regions[l].owned) return true;
        return false;
    }

    public static int OwnedCount { get { EnsureInit(); int n = 0; foreach (var r in regions) if (r.owned && r.type != RegionType.Gate) n++; return n; } }

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
            default: return "#6f6889";
        }
    }

    // ============ 毎ターンの産出 ============
    public static void CollectYields()
    {
        EnsureInit();
        int dp = 0, mat = 0, rp = 0, fame = 0;
        foreach (var r in regions)
        {
            if (!r.owned || r.type == RegionType.Gate) continue;
            dp += r.dpYield; mat += r.matYield; rp += r.rpYield; fame += r.fameYield;
        }
        if (dp == 0 && mat == 0 && rp == 0) return;
        var res = DungeonResourceManager.Instance;
        if (res != null) { res.AddDP(dp); res.AddMaterial(mat); res.AddFame(fame); }
        if (rp > 0) ResearchState.AddRP(rp);
        Debug.Log($"🗺️『地上の産出』支配{OwnedCount}領域 → +{dp}DP +{mat}素材 +{rp}RP +{fame}名声");
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
