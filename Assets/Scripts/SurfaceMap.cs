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

    // 🏔️ 地形（Civの地形に相当。施設の隣接ボーナスの源）
    public enum Terrain { Waste, Plains, Forest, Hills, Mountain, Marsh }
    // 💎 資源（Civの戦略/ボーナス資源に相当）
    public enum Resource { None, Iron, Manastone, Grain, Livestock, Gem, Timber }

    public class Region
    {
        public int id;
        public string name;
        public RegionType type;
        public int defense;                 // 中立時の防衛力（攻略に必要な戦力の目安）
        public int dpYield, matYield, rpYield, fameYield;
        public int[] links;                 // 隣接する領域（ヘクス盤から自動導出）
        // ⬡ ヘクス盤（axial座標。半径2＝19タイルで領域数とちょうど一致する）
        public int q, r;
        public Terrain terrain;
        public bool river;                  // 川（交易所の major 隣接源）
        public bool wonder;                 // 自然の驚異（祭壇の major 隣接源）
        public Resource resource;
        public int district = -1;           // 建てた施設（DistrictCatalog index／-1=なし）
        public int wonderIndex = -1;        // ★ 遺産（WonderCatalog index／-1=なし）。盤の生成時にまれに湧く
        // 👥 人口（Civの都市成長に相当）。食料で増え、施設の産出倍率になる。統治力が足りないと不穏になる。
        public int pop = 0;
        public int foodStock = 0;
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
        //  ⬡ axial座標(q,r)の半径2ヘクス盤＝1+6+12＝**ちょうど19タイル**。領域数と一致するので
        //     1領域＝1ヘクスで並べられる。隣接(links)はヘクスの6方向から自動で導出する（手書きしない）。
        //     中心(0,0)が迷宮の入口。外側の環ほど防衛が固く、他魔王の本拠地も外側に置く。
        regions = new List<Region>
        {
            //  id  q   r  名前              種別               防衛  DP  素材  RP 名声   地形            川     驚異  資源
            H(0,  0,  0, "迷宮前の荒れ地",  RegionType.Gate,     0,    0,  0,  0,  0, Terrain.Waste,    false, false, Resource.None),
            H(1,  0,  1, "灰かぶりの集落",  RegionType.Village,  60,  30,  1,  0,  6, Terrain.Plains,   false, false, Resource.Livestock),
            H(2, -1,  1, "霧ざわめく森",    RegionType.Forest,   80,  20,  3,  0,  4, Terrain.Forest,   false, false, Resource.Timber),
            H(3,  1,  0, "鉄錆の坑道",      RegionType.Mine,    110,  25,  5,  0,  5, Terrain.Hills,    false, false, Resource.Iron),
            H(4,  0, -1, "東の街道",        RegionType.Village, 160,  55,  2,  1, 10, Terrain.Plains,   true,  false, Resource.Grain),
            H(5,  1, -1, "麦守りの里",      RegionType.Village, 190,  70,  2,  1, 11, Terrain.Plains,   false, false, Resource.Grain),
            H(6, -1,  0, "古き樹の祠",      RegionType.Forest,  220,  35,  4,  2,  8, Terrain.Forest,   false, true,  Resource.None),
            H(7,  2,  0, "深層鉱脈",        RegionType.Mine,    260,  45, 10,  1, 10, Terrain.Mountain, false, false, Resource.Manastone),
            H(8,  0, -2, "宿場町ラウム",    RegionType.Town,    360, 130,  4,  2, 20, Terrain.Plains,   true,  false, Resource.None),
            H(9,  1, -2, "職人街ヴァル",    RegionType.Town,    420, 110, 12,  2, 22, Terrain.Hills,    false, false, Resource.Iron),
            H(10,-2,  1, "祈りの丘",        RegionType.Forest,  460,  60,  5,  4, 16, Terrain.Hills,    false, true,  Resource.None),
            H(11,-1,  2, "廃修道院",        RegionType.Fort,    560,  90,  8,  4, 24, Terrain.Plains,   false, false, Resource.Gem),
            H(12, 2, -2, "石造りの砦",      RegionType.Fort,    640, 120,  9,  3, 26, Terrain.Hills,    false, false, Resource.None),
            H(13, 1,  1, "辺境伯領",        RegionType.Town,    820, 210, 10,  5, 34, Terrain.Plains,   true,  false, Resource.Grain),
            H(14, 2, -1, "騎士団駐屯地",    RegionType.Fort,    900, 150, 16,  4, 36, Terrain.Hills,    false, false, Resource.Iron),
            H(15, 0,  2, "城塞都市アルバ",  RegionType.City,   1250, 340, 20,  8, 55, Terrain.Plains,   true,  false, Resource.Gem),
            // 🔥 他魔王の支配領域（真核がある本拠地）。落とすと真核を奪える＝その魔王を排除。
            H(16,-2,  2, "紅蓮の坑洞",      RegionType.Domain,  700, 180, 14,  5, 30, Terrain.Mountain, false, false, Resource.Manastone),
            H(17,-2,  0, "常夜の樹海",      RegionType.Domain,  980, 240, 16,  6, 38, Terrain.Forest,   false, true,  Resource.Timber),
            H(18,-1, -1, "凍てつく王座",    RegionType.Domain, 1400, 380, 24,  9, 60, Terrain.Mountain, true,  false, Resource.Gem),

            // ── 第3環（18タイル）＝盤を広げたぶんの外周。人間側の本国が並ぶ ──
            H(19, 3,  0, "塩の平原",        RegionType.Village, 520,  95,  4,  2, 20, Terrain.Plains,   false, false, Resource.Grain),
            H(20, 3, -1, "渡し守の村",      RegionType.Village, 560, 105,  4,  2, 21, Terrain.Plains,   true,  false, Resource.Livestock),
            H(21, 3, -2, "黒曜の断崖",      RegionType.Mine,    640,  70, 14,  3, 24, Terrain.Mountain, false, false, Resource.Manastone),
            H(22, 3, -3, "囁きの湿原",      RegionType.Forest,  600,  60,  6,  4, 22, Terrain.Marsh,    true,  false, Resource.None),
            H(23, 2, -3, "灯台跡",          RegionType.Fort,    720, 115,  7,  3, 27, Terrain.Hills,    false, false, Resource.None),
            H(24, 1, -3, "隠れ里キル",      RegionType.Village, 660, 120,  5,  3, 25, Terrain.Forest,   false, true,  Resource.Timber),
            H(25, 0, -3, "朽ちた水路",      RegionType.Town,    880, 175,  8,  4, 33, Terrain.Plains,   true,  false, Resource.None),
            H(26,-1, -2, "銀鉱の峠",        RegionType.Mine,    760,  90, 16,  3, 28, Terrain.Mountain, false, false, Resource.Iron),
            H(27,-2, -1, "風抜けの谷",      RegionType.Forest,  700,  75,  7,  4, 26, Terrain.Hills,    false, false, Resource.None),
            H(28,-3,  0, "巡礼の橋",        RegionType.Town,    920, 190,  8,  5, 34, Terrain.Plains,   true,  false, Resource.Gem),
            H(29,-3,  1, "北の牧草地",      RegionType.Village, 680, 130,  5,  2, 26, Terrain.Plains,   false, false, Resource.Livestock),
            H(30,-3,  2, "硫黄の池",        RegionType.Mine,    800,  85, 15,  4, 29, Terrain.Marsh,    false, false, Resource.Manastone),
            H(31,-3,  3, "忘れられた墓域",  RegionType.Fort,    980, 140,  9,  6, 36, Terrain.Waste,    false, true,  Resource.None),
            H(32,-2,  3, "綻びの森",        RegionType.Forest,  740,  80,  8,  4, 27, Terrain.Forest,   false, false, Resource.Timber),
            H(33,-1,  3, "石切り場",        RegionType.Mine,    820, 100, 17,  3, 30, Terrain.Hills,    false, false, Resource.Iron),
            H(34, 0,  3, "古戦場",          RegionType.Fort,   1050, 160, 11,  5, 38, Terrain.Plains,   false, false, Resource.None),
            H(35, 1,  2, "涸れ井戸の里",    RegionType.Village, 700, 125,  5,  3, 26, Terrain.Waste,    false, false, Resource.Grain),
            H(36, 2,  1, "星降りの丘",      RegionType.Town,   1150, 205, 12,  7, 42, Terrain.Hills,    false, true,  Resource.Gem),
        };
        BuildLinksFromHex();
        PlaceWonders();
        regions[0].owner = OwnerSelf; // 迷宮の目の前は最初から自領（進軍の起点）
    }

    /// <summary>★ 遺産を盤にまれに置く。生成のたびに場所が変わる（Civの「1つしか無い」希少さ）。</summary>
    private static void PlaceWonders()
    {
        // 外周寄り（第2環以降）かつ他魔王の本拠地でないタイルが候補
        var cand = new List<Region>();
        foreach (var r in regions)
        {
            int d = (Mathf.Abs(r.q) + Mathf.Abs(r.r) + Mathf.Abs(r.q + r.r)) / 2;
            if (d >= 2 && r.rivalHome < 0 && r.type != RegionType.Gate) cand.Add(r);
        }
        for (int i = 0; i < cand.Count; i++)   // シャッフル
        {
            int j = Random.Range(i, cand.Count);
            var t = cand[i]; cand[i] = cand[j]; cand[j] = t;
        }
        // 遺産の種類も重複しないように選ぶ
        var kinds = new List<int>();
        for (int i = 0; i < WonderCatalog.Count; i++) kinds.Add(i);
        for (int i = 0; i < kinds.Count; i++)
        {
            int j = Random.Range(i, kinds.Count);
            int t = kinds[i]; kinds[i] = kinds[j]; kinds[j] = t;
        }
        int n = Random.Range(2, 5);            // 2〜4個
        n = Mathf.Min(n, Mathf.Min(cand.Count, kinds.Count));
        for (int i = 0; i < n; i++)
        {
            cand[i].wonderIndex = kinds[i];
            cand[i].defense += WonderCatalog.Get(kinds[i]).defenseBonus;   // 遺産は守りが固い
        }
        Debug.Log($"★『遺産』{n}個が盤に生成された");
    }

    // 👥 人口（Civの都市成長）。統治力を超えると不穏＝産出が落ちる。
    public const int MaxPop = 6;
    /// <summary>その領域の統治力（人口の許容量）。砦と兵舎で伸びる。</summary>
    public static int GovernanceOf(int id)
    {
        var r = Get(id);
        int g = 2 + r.fortLevel;
        if (r.district >= 0 && DistrictCatalog.Get(r.district).yield == DistrictCatalog.Yield.Defense) g += 2;
        if (ResearchState.IsResearched("s_govern")) g += 2;
        return g;
    }
    public static bool IsUnrest(int id) => Get(id).pop > GovernanceOf(id);

    /// <summary>そのタイル単体の食料。人口を養う量。</summary>
    public static int FoodOf(Region t)
    {
        int f = 0;
        if (t.terrain == Terrain.Plains) f += 2;
        else if (t.terrain == Terrain.Forest || t.terrain == Terrain.Marsh || t.terrain == Terrain.Hills) f += 1;
        if (t.river) f += 1;
        if (t.resource == Resource.Grain || t.resource == Resource.Livestock) f += 2;
        return f;
    }

    /// <summary>👥 人口が「働く」タイル＝自タイル＋食料の高い隣接タイルを人口ぶんだけ。Civの市民配置に相当。</summary>
    public static List<Region> WorkedTiles(int id)
    {
        var r = Get(id);
        var l = new List<Region> { r };
        if (r.pop <= 1) return l;
        var ns = new List<Region>(Neighbors(id));
        ns.Sort((a, b) => FoodOf(b).CompareTo(FoodOf(a)));
        for (int i = 0; i < ns.Count && l.Count < r.pop; i++) l.Add(ns[i]);
        return l;
    }
    public static int FoodIncome(int id)
    {
        int f = 0;
        foreach (var t in WorkedTiles(id)) f += FoodOf(t);
        return f - Get(id).pop;      // 人口1につき1消費
    }
    /// <summary>人口による産出倍率（施設と領域の両方に掛かる）。</summary>
    public static float PopMult(int id)
    {
        var r = Get(id);
        float m = 1f + 0.15f * Mathf.Max(0, r.pop - 1);
        if (IsUnrest(id)) m *= 0.5f;   // 不穏＝半減
        return m;
    }

    /// <summary>毎ターンの人口成長（食料が貯まると増える）。</summary>
    public static void GrowPopulation()
    {
        foreach (var r in regions)
        {
            if (!r.owned || r.type == RegionType.Gate) continue;
            if (r.pop <= 0) { r.pop = 1; r.foodStock = 0; continue; }
            r.foodStock += FoodIncome(r.id);
            if (r.foodStock < 0) r.foodStock = 0;
            // 🏠 Civの住居上限に相当：統治力+1 を超えては増えない。
            //    （放っておくと際限なく増えて永久に不穏になってしまうため。
            //      「あと1人ぶんだけ無理が利く」＝砦/兵舎/研究で統治力を上げる動機になる）
            if (r.pop >= GovernanceOf(r.id) + 1) { r.foodStock = Mathf.Min(r.foodStock, 8 * r.pop); continue; }
            int need = 8 * r.pop;
            if (r.foodStock >= need && r.pop < MaxPop)
            {
                r.foodStock -= need; r.pop++;
                Debug.Log($"👥『人口増加』{r.name} の人口が {r.pop} になった（統治力{GovernanceOf(r.id)}）"
                    + (IsUnrest(r.id) ? " ― <color=#e05a5a>不穏</color>：砦か兵舎で統治力を上げないと産出が半減する" : ""));
            }
        }
    }

    // ⬡ axialの6方向。ここから links を作るので、盤を組み替えても隣接が自動で追従する。
    private static readonly int[,] HexDirs = { { 1, 0 }, { 1, -1 }, { 0, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 } };

    private static void BuildLinksFromHex()
    {
        foreach (var a in regions)
        {
            var l = new List<int>();
            for (int d = 0; d < 6; d++)
            {
                int nq = a.q + HexDirs[d, 0], nr = a.r + HexDirs[d, 1];
                foreach (var b in regions) if (b.q == nq && b.r == nr) { l.Add(b.id); break; }
            }
            a.links = l.ToArray();
        }
    }

    /// <summary>隣接する領域を列挙する（施設の隣接ボーナス計算にも使う）。</summary>
    public static List<Region> Neighbors(int id)
    {
        EnsureInit();
        var l = new List<Region>();
        foreach (var n in Get(id).links) l.Add(regions[n]);
        return l;
    }

    /// <summary>ヘクスの中心座標（UI描画用。pointy-top配置）。size＝外接円の半径。</summary>
    public static Vector2 HexPos(Region r, float size)
        => new Vector2(size * 1.7320508f * (r.q + r.r * 0.5f), size * 1.5f * r.r);

    private static Region H(int id, int q, int r, string n, RegionType t, int def, int dp, int mat, int rp, int fame,
                            Terrain terr, bool river, bool wonder, Resource res)
        => new Region
        {
            id = id, q = q, r = r, name = n, type = t, defense = def,
            dpYield = dp, matYield = mat, rpYield = rp, fameYield = fame,
            terrain = terr, river = river, wonder = wonder, resource = res, links = new int[0]
        };

    public static string TerrainName(Terrain t)
    {
        switch (t)
        {
            case Terrain.Plains: return "平地";
            case Terrain.Forest: return "森";
            case Terrain.Hills: return "丘陵";
            case Terrain.Mountain: return "山岳";
            case Terrain.Marsh: return "湿地";
            default: return "荒地";
        }
    }
    public static string TerrainColor(Terrain t)
    {
        switch (t)
        {
            case Terrain.Plains: return "#6b7a4a";
            case Terrain.Forest: return "#3f6b45";
            case Terrain.Hills: return "#7a6a4a";
            case Terrain.Mountain: return "#6a6a78";
            case Terrain.Marsh: return "#4a6a68";
            default: return "#5a5060";
        }
    }
    public static string ResourceName(Resource r)
    {
        switch (r)
        {
            case Resource.Iron: return "鉄";
            case Resource.Manastone: return "魔石";
            case Resource.Grain: return "穀物";
            case Resource.Livestock: return "家畜";
            case Resource.Gem: return "宝石";
            case Resource.Timber: return "良材";
            default: return "";
        }
    }

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
        // 🔭 斥候：自領の2つ先まで見える
        if (ResearchState.IsResearched("s_scout"))
            foreach (var l in r.links) foreach (var l2 in regions[l].links) if (regions[l2].owned) return true;
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
        d += DistrictCatalog.DefenseBonusAt(id);   // 🏛️ 兵舎ぶんの防衛
        d += WonderCatalog.DefenseBonusAll;        // ★ 遺産『不落の城壁』
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
            float pm = PopMult(r.id);                      // 👥 人口（不穏なら半減）
            dp += Mathf.RoundToInt(r.dpYield * pm); mat += Mathf.RoundToInt(r.matYield * pm);
            rp += Mathf.RoundToInt(r.rpYield * pm); fame += r.fameYield;
        }
        dp = Mathf.RoundToInt(dp * WonderCatalog.RegionDPMult);   // ★ 遺産『黄金の秤』
        if (ResearchState.IsResearched("s_settle"))   // 🏘️ 拠点化：産出+25%
        { dp = Mathf.RoundToInt(dp * 1.25f); mat = Mathf.RoundToInt(mat * 1.25f); rp = Mathf.RoundToInt(rp * 1.25f); }
        return (dp, mat, rp, fame);
    }

    /// <summary>支配領域による『世界水準』の押し上げ。広げるほど強い冒険者が来る（対数＋上限＝カーブを壊さない）。</summary>
    public static float WorldTierBias => Mathf.Min(1.2f, Mathf.Log(1f + OwnedCount) * 0.5f);
}
