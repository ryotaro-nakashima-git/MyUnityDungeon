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
    public enum RegionType { Gate, Village, Forest, Mine, Town, Fort, City, Domain, Sea }

    // 所有者。0=中立(人間側) / 1=自分 / 2以上=他魔王(RivalLords index + 2)
    public const int OwnerNeutral = 0;
    public const int OwnerSelf = 1;
    public const int OwnerRivalBase = 2;

    // 🏔️ 地形（Civの地形に相当。施設の隣接ボーナスの源）
    public enum Terrain { Waste, Plains, Forest, Hills, Mountain, Marsh, Ocean }
    // 🏙️ 拠点の格（Civ VII の Settlement）。None＝版図（どこかの拠点の領土）→ [[SettlementSystem]]
    public enum Settle { None, Town, City }
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
        // ⬡ ヘクス盤（odd-r offset。幅W×高さHの長方形で東西がループする → [[HexGrid]]）
        public int col, row;
        public float depth;                 // 入口からの遠さを 0〜9 に正規化した値（盤の大きさによらない）
        public Terrain terrain;
        public bool river;                  // 川（交易所の major 隣接源）
        public bool wonder;                 // 自然の驚異（祭壇の major 隣接源）
        public Resource resource;
        public int district = -1;           // 建てた施設（DistrictCatalog index／-1=なし）
        public int district2 = -1;          // 🏙️ 街区：同じタイルの2つ目の施設（Civ VIIのQuarter）
        public bool specialist;             // 👷 専門家を置いたタイル（施設の隣接ボーナス2倍）
        public int wonderIndex = -1;        // ★ 遺産（WonderCatalog index／-1=なし）。盤の生成時にまれに湧く
        // 🏙️ 拠点（Civ VII）。None＝版図。人口/食料/施設/特化は拠点だけが持つ。→ [[SettlementSystem]]
        public Settle settle = Settle.None;
        public int focus = -1;              // 🎯 特化（Townのみ。SettlementSystem.Focus の index）
        public int homeSettlement = -1;     // このタイルを版図に持つ拠点のid（-1＝未編入の辺境）
        public int celebrateTurns;          // 🎉 祝祭の残りターン
        public int happyStock;              // 幸福の余剰の蓄積（祝祭のゲージ）
        public int borderStock;             // 🌱 国境の自動拡張のゲージ（Civの文化圏拡張に相当）
        // 👥 人口（Civの都市成長に相当）。食料で増え、産出倍率になる。統治力が足りないと不満が出る。
        public int pop = 0;
        public int foodStock = 0;
        // 🌊 海：占領できず、陸路が通らない（渡航研究があると1マスだけ越えられる＝Civの Distant Lands）
        public bool isOcean, isCoast, volcano;
        public int naturalWonder = -1;      // 🏔️ SurfaceGen.NaturalWonders の index
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

    // 🌍 盤の大きさ（極小1,160 / 小2,337 / 中4,503 / 大6,958タイル）。ゲーム開始前に選ぶ。
    //    既定はCivのStandard相当（中）。※以前は暫定の「試作266」が既定のままで、
    //      横に少し動かすだけで世界を一周してしまっていた。
    public static SurfaceGen.Size MapSize = SurfaceGen.Size.Medium;
    public static int MapSeed = 0;
    public static int MapW { get; private set; }
    public static int MapH { get; private set; }
    /// <summary>(col,row) → id。近傍の導出を O(1) にする（無いと盤の生成が O(n²) で1万タイル2秒かかる）。</summary>
    private static int[] cellIndex;
    public static int IdAt(int col, int row)
    {
        EnsureInit();
        if (row < 0 || row >= MapH) return -1;
        return cellIndex[row * MapW + HexGrid.WrapCol(col, MapW)];
    }
    /// <summary>盤を作り直す（大きさ・種を変えて再生成）。</summary>
    public static void Regenerate(SurfaceGen.Size size, int seed)
    {
        MapSize = size; MapSeed = seed;
        regions = null; RivalLords.Reset(); EnsureInit();
    }

    private static void Build()
    {
        // 🌍 手続き生成（プレート→陸海→浸食→山→バイオーム→川→資源→自然の驚異）
        if (MapSeed == 0) MapSeed = Random.Range(1, int.MaxValue);
        MapW = SurfaceGen.WidthOf(MapSize); MapH = SurfaceGen.HeightOf(MapSize);
        regions = SurfaceGen.Generate(MapSize, MapSeed);
        cellIndex = new int[MapW * MapH];
        for (int i = 0; i < cellIndex.Length; i++) cellIndex[i] = -1;
        foreach (var r in regions) cellIndex[r.row * MapW + r.col] = r.id;
        BuildLinksFromHex();
        PlaceRivalHomes();
        PlaceWonders();
        // 🏙️ 迷宮の目の前は最初から自領、かつ**首都(City)**。ここを起点に版図が広がる。
        var cap = regions[IndexOfCenter()];
        cap.owner = OwnerSelf; cap.settle = Settle.City; cap.pop = 1; cap.homeSettlement = cap.id;
        SettlementSystem.ReassignTerritory();
        seen = null; MarkSeen(cap.id, 2);   // 👁️ 盤を作り直したら視界も作り直す（迷宮の周りだけ見えている状態から）
        Debug.Log($"🌍『地上を生成』{regions.Count}タイル（{SizeName(MapSize)}・seed {MapSeed}）／首都〈{cap.name}〉");
    }

    public static string SizeName(SurfaceGen.Size s) => SurfaceGen.NameOf(s);

    /// <summary>迷宮の入口＝盤の中央。</summary>
    public static int IndexOfCenter()
    {
        int id = cellIndex != null ? cellIndex[(MapH / 2) * MapW + (MapW / 2)] : -1;
        return id >= 0 ? id : 0;
    }

    /// <summary>🔥 他魔王の本拠地を、中心から遠い陸のタイルに散らして置く。</summary>
    private static void PlaceRivalHomes()
    {
        var cand = new List<Region>();
        foreach (var r in regions) if (!r.isOcean && r.depth >= 3f) cand.Add(r);
        if (cand.Count == 0) return;
        // 互いに離れた3箇所を選ぶ。※盤が広いと総当たりが重いので候補を間引いてから探す
        if (cand.Count > 400)
        {
            var thin = new List<Region>();
            int stride = cand.Count / 400;
            for (int i = 0; i < cand.Count; i += stride) thin.Add(cand[i]);
            cand = thin;
        }
        var chosen = new List<Region>();
        for (int i = 0; i < 3 && cand.Count > 0; i++)
        {
            Region best = null; int bestScore = -1;
            foreach (var c in cand)
            {
                if (chosen.Contains(c)) continue;
                int score = Mathf.RoundToInt(c.depth);
                foreach (var o in chosen) score += HexDist(c, o);
                if (score > bestScore) { bestScore = score; best = c; }
            }
            if (best == null) break;
            chosen.Add(best);
            best.type = RegionType.Domain;
            best.name = new[] { "紅蓮の坑洞", "常夜の樹海", "凍てつく王座" }[i];
            best.defense = Mathf.RoundToInt(best.defense * 2.2f + 400);
            best.dpYield += 60; best.matYield += 8; best.rpYield += 3; best.fameYield += 14;
        }
        for (int i = 0; i < chosen.Count; i++) { chosen[i].rivalHome = i; chosen[i].owner = OwnerRivalBase + i; }
        // ※ RivalLords 側では本拠地を割り当てない（固定IDを書くと海に乗る）。
    }
    public static int HexDist(Region a, Region b) => HexGrid.Distance(a.col, a.row, b.col, b.row, MapW);

    /// <summary>★ 遺産を盤にまれに置く。生成のたびに場所が変わる（Civの「1つしか無い」希少さ）。</summary>
    private static void PlaceWonders()
    {
        // 入口から離れていて他魔王の本拠地でないタイルが候補
        var cand = new List<Region>();
        foreach (var r in regions)
            if (r.depth >= 2f && r.rivalHome < 0 && r.type != RegionType.Gate && !r.isOcean) cand.Add(r);
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

    // 👥 人口（Civの都市成長）。統治力を超えた分は不満になる。→ [[SettlementSystem]]
    public const int MaxPop = 8;
    /// <summary>その拠点の人口の上限（都市は大きく育つ）。</summary>
    public static int MaxPopOf(int id) => Get(id).settle == Settle.City ? 8 : 5;

    /// <summary>その拠点の統治力（人口の許容量）。砦・兵舎・都市・研究で伸びる。</summary>
    public static int GovernanceOf(int id)
    {
        var r = Get(id);
        int g = 2 + r.fortLevel;
        if (r.settle == Settle.City) g += 2;
        foreach (var t in SettlementSystem.TerritoryOf(id))
            if (t.district >= 0 && DistrictCatalog.Get(t.district).yield == DistrictCatalog.Yield.Defense) g += 2;
        if (ResearchState.IsResearched("s_govern")) g += 2;
        if (r.settle == Settle.Town && r.focus == 5) g += 1;   // 🎯 砦の町
        return g;
    }
    /// <summary>不満が出ているか（表示用）。C1までの「不穏＝×0.5」ではなく、**1点につき-5%**の線形に変わった。</summary>
    public static bool IsUnrest(int id) => Get(id).settle != Settle.None && SettlementSystem.NetHappy(id) < 0;

    /// <summary>そのタイル単体の食料。人口を養う量。</summary>
    public static int FoodOf(Region t)
    {
        int f = 0;
        if (t.terrain == Terrain.Plains) f += 2;
        else if (t.terrain == Terrain.Forest || t.terrain == Terrain.Marsh || t.terrain == Terrain.Hills) f += 1;
        if (t.river) f += 1;
        if (t.resource == Resource.Grain || t.resource == Resource.Livestock) f += 2;
        // 🏙️ 拠点タイルは Civ の「都市中心」と同じく基礎食料を持つ。
        //    ※これが無いと、荒地に置かれた首都が食料0のまま人口1で永久に止まる（実測で14ターン止まった）。
        if (t.settle != Settle.None) f = Mathf.Max(f, 3);
        return f;
    }

    /// <summary>
    /// 👥 人口が「働く」タイル＝拠点自身＋**版図の中で**食料の高いタイルを人口ぶんだけ。Civの市民配置に相当。
    /// 専門家を置いたタイルは市民が耕さない（専門家がそこに就いているため）。
    /// </summary>
    public static List<Region> WorkedTiles(int id)
    {
        var r = Get(id);
        var l = new List<Region> { r };
        if (r.settle == Settle.None || r.pop <= 1) return l;
        var ns = new List<Region>();
        foreach (var t in SettlementSystem.TerritoryOf(id)) if (t.id != id && !t.specialist) ns.Add(t);
        ns.Sort((a, b) => FoodOf(b).CompareTo(FoodOf(a)));
        for (int i = 0; i < ns.Count && l.Count < r.pop; i++) l.Add(ns[i]);
        return l;
    }
    public static int FoodIncome(int id)
    {
        var r = Get(id);
        if (r.settle == Settle.None) return 0;
        int f = 0;
        foreach (var t in WorkedTiles(id)) f += FoodOf(t);
        f += DistrictCatalog.WarehouseFoodAt(id);                 // 📦 倉庫
        f += SettlementSystem.FocusFoodBonus(id, f);              // 🎯 成長/農耕の町
        int spec = 0;
        foreach (var t in SettlementSystem.TerritoryOf(id)) if (t.specialist) spec++;
        f += EraSystem.FoodBonus;                                 // 📜 誓約『豊穣』／☄災厄『飢饉』
        f += PolicySystem.FoodBonus;                              // 🏛️ 政策『開墾』
        f += AttributeSystem.FoodBonus;                           // 🎖️ 属性『入植の理』
        return f - r.pop - spec * 2;      // 人口1につき1消費／専門家1人につき2消費
    }

    /// <summary>
    /// 産出倍率。**版図のタイルは所属する拠点の倍率**を使う（未編入の辺境は産出しない）。
    /// 不満（1点-5%・最大-80%） × 祝祭。
    /// ⚠ **人口の項はここには入れない**。人口は「働くタイルの数」として既に効いているので、
    ///   倍率にも入れると二重になる（国境が自動で広がるようになった途端、産出が青天井になった）。
    ///   → [[difficulty-curve-orders]]
    /// </summary>
    public static float PopMult(int id)
    {
        int s = SettlementSystem.SettlementOf(id);
        if (s < 0) return 0f;                     // 🚩 未編入の辺境＝何も産まない
        var r = Get(s);
        float m = SettlementSystem.HappinessMult(s);
        if (r.celebrateTurns > 0) m *= SettlementSystem.CelebrateMult;   // 🎉 祝祭
        return m;
    }

    /// <summary>毎ターンの人口成長（食料が貯まると増える）。**拠点だけ**が育つ。</summary>
    public static void GrowPopulation()
    {
        foreach (var r in regions)
        {
            if (!r.owned || r.settle == Settle.None) continue;
            if (r.pop <= 0) { r.pop = 1; r.foodStock = 0; continue; }
            r.foodStock += FoodIncome(r.id);
            if (r.foodStock < 0) r.foodStock = 0;
            // 🏠 Civの住居上限に相当：統治力+2 を超えては増えない。
            //    （放っておくと際限なく増えて永久に不満になってしまうため。
            //      「あと2人ぶんだけ無理が利く」＝砦/兵舎/都市化/研究で統治力を上げる動機になる）
            int cap = Mathf.Min(MaxPopOf(r.id), GovernanceOf(r.id) + 2);
            if (r.pop >= cap) { r.foodStock = Mathf.Min(r.foodStock, 8 * r.pop); continue; }
            int need = 8 * r.pop;
            if (r.foodStock >= need)
            {
                r.foodStock -= need; r.pop++;
                int net = SettlementSystem.NetHappy(r.id);
                Debug.Log($"👥『人口増加』{r.name} の人口が {r.pop} になった（統治力{GovernanceOf(r.id)}）"
                    + (net < 0 ? $" ― <color=#e05a5a>不満{-net}＝産出{-net * 5}%減</color>：砦・兵舎・施設で立て直す" : ""));
            }
        }
    }

    /// <summary>
    /// 隣接リンクを作る。**インデックス経由で O(n)**。
    /// ※以前は「全タイル×6方向×全タイル線形走査」の O(n²) で、1万タイルの盤の生成に1.9秒かかっていた。
    /// </summary>
    private static void BuildLinksFromHex()
    {
        var buf = new List<int>(6);
        foreach (var a in regions)
        {
            buf.Clear();
            for (int d = 0; d < 6; d++)
            {
                int nc, nr;
                if (!HexGrid.Neighbor(a.col, a.row, d, MapW, MapH, out nc, out nr)) continue;
                int id = cellIndex[nr * MapW + nc];
                if (id >= 0) buf.Add(id);
            }
            a.links = buf.ToArray();
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

    /// <summary>ヘクスの中心座標（描画用。pointy-top配置）。size＝外接円の半径。</summary>
    public static Vector2 HexPos(Region r, float size) => HexGrid.WorldPos(r.col, r.row, size);

    public static string TerrainName(Terrain t)
    {
        switch (t)
        {
            case Terrain.Plains: return "平地";
            case Terrain.Forest: return "森";
            case Terrain.Hills: return "丘陵";
            case Terrain.Mountain: return "山岳";
            case Terrain.Marsh: return "湿地";
            case Terrain.Ocean: return "海";
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
            case Terrain.Ocean: return "#1e3a58";
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

    // ============ 👁️ 視界（U1：ユニットが歩いた先を『見た』ことにする） ============
    //  ⚠ 「見えている(IsSeen)」と「手が届く(IsDiscovered)」は別。
    //     前者は**描画**（霧を剥がすか）、後者は**進軍先に選べるか**。海は見えても支配できない。
    private static bool[] seen;
    private static void EnsureSeen()
    {
        EnsureInit();
        if (seen == null || seen.Length != regions.Count) seen = new bool[regions.Count];
    }

    /// <summary>そのタイルを中心に radius タイルぶんを『見た』ことにする（海も含めて記憶する）。</summary>
    public static void MarkSeen(int centerId, int radius)
    {
        EnsureSeen();
        if (centerId < 0 || centerId >= regions.Count) return;
        var dist = new Dictionary<int, int>();
        var q = new Queue<int>();
        dist[centerId] = 0; q.Enqueue(centerId); seen[centerId] = true;
        while (q.Count > 0)
        {
            int cur = q.Dequeue();
            int d = dist[cur];
            if (d >= radius) continue;
            foreach (var l in regions[cur].links)
            {
                if (dist.ContainsKey(l)) continue;
                dist[l] = d + 1; seen[l] = true;
                q.Enqueue(l);
            }
        }
    }

    /// <summary>一度でも見たか（＝霧を剥がして描くか）。見た土地は覚えている（Civと同じ）。</summary>
    public static bool IsSeen(int id)
    {
        EnsureSeen();
        if (id < 0 || id >= regions.Count) return false;
        return seen[id] || IsDiscovered(id);
    }

    /// <summary>自領に隣接していれば『見えている』＝侵攻先に選べる。歩いて見た土地も対象。</summary>
    public static bool IsDiscovered(int id)
    {
        EnsureInit();
        var r = Get(id);
        if (r.isOcean) return false;                 // 🌊 海は支配できない
        if (r.owned) return true;
        if (seen != null && seen.Length == regions.Count && seen[id]) return true;   // 👁️ 歩いて見た
        foreach (var l in r.links) if (regions[l].owned) return true;
        // 🔭 斥候：自領の2つ先まで見える
        if (ResearchState.IsResearched("s_scout"))
            foreach (var l in r.links) foreach (var l2 in regions[l].links) if (regions[l2].owned) return true;
        // 🚢 渡航：海を1マスだけ越えた先（＝Civの Distant Lands）に手が届くようになる
        //    研究『渡航術』のほか、🎖️昇進『沿岸航行/遠洋』を持つ眷属がいても届く
        if (ResearchState.IsResearched("s_voyage") || KinRoster.AnySeaCross() > 0)
            foreach (var l in r.links)
            {
                var sea = regions[l];
                if (!sea.isOcean) continue;
                foreach (var l2 in sea.links) if (regions[l2].owned) return true;
            }
        return false;
    }

    public static int OwnedCount { get { EnsureInit(); int n = 0; foreach (var r in regions) if (r.owned && r.type != RegionType.Gate && !r.isOcean) n++; return n; } }
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
        d += SettlementSystem.FocusDefense(id);    // 🎯 砦の町
        d += EraSystem.DefenseBonus;               // 📜 誓約『城塞の誓い』
        d += PolicySystem.TerritoryDefense;        // 🏛️ 政策『城塞化』／政体の祝祭
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
        if (owner != OwnerSelf)
        {
            // 🏙️ 落とされた拠点は消える（Civの都市略奪に相当）。専門家も街区も失う。
            r.fortLevel = 0; r.settle = Settle.None; r.focus = -1; r.pop = 0; r.foodStock = 0;
            r.celebrateTurns = 0; r.happyStock = 0; r.specialist = false;
        }
        SettlementSystem.ReassignTerritory();   // 版図は所有が変わるたびに引き直す
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
            case RegionType.Sea: return "海域";
            default: return "迷宮前";   // ※『拠点』は Settle.Town を指す語になったので改名（C2）
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
            case RegionType.Sea: return "#4a80b0";
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
        // 🚩 産出するのは **拠点の人口が働いているタイルだけ**（Civの市民配置そのもの）。
        //    支配しているだけ／版図に入っているだけのタイルは何も産まない。
        //    ⚠ 以前は「版図の全タイル」が産出していたので、国境の自動拡張を入れた途端に
        //      98タイルで +4,806DP/+558名声 まで膨れた。働くタイルは人口ぶんしか無いので、
        //      これで自然に頭打ちになる。→ [[difficulty-curve-orders]]
        foreach (var s in regions)
        {
            if (!s.owned || s.settle == Settle.None) continue;
            float pm = PopMult(s.id);                       // 不満 × 祝祭
            if (pm <= 0f) continue;
            foreach (var t in WorkedTiles(s.id))
            {
                if (t.isOcean) continue;
                fame += Mathf.RoundToInt(t.fameYield * pm);
                dp += Mathf.RoundToInt(t.dpYield * pm); mat += Mathf.RoundToInt(t.matYield * pm);
                rp += Mathf.RoundToInt(t.rpYield * pm);
            }
        }
        dp = Mathf.RoundToInt(dp * WonderCatalog.RegionDPMult * EraSystem.RegionDpMult * PolicySystem.RegionDpTotal * AttributeSystem.RegionDpMult);   // ★遺産『黄金の秤』／📜誓約『黄金』／☄災厄『枯渇』
        fame = Mathf.RoundToInt(fame * EraSystem.FameMult * NarrativeSystem.FameMult * AttributeSystem.FameMult);      // 📜誓約『秘匿』／🕯️形見『灰の懐中時計』
        if (ResearchState.IsResearched("s_settle"))   // 🏘️ 拠点化：産出+25%
        { dp = Mathf.RoundToInt(dp * 1.25f); mat = Mathf.RoundToInt(mat * 1.25f); rp = Mathf.RoundToInt(rp * 1.25f); }
        return (dp, mat, rp, fame);
    }

    /// <summary>支配領域による『世界水準』の押し上げ。広げるほど強い冒険者が来る（対数＋上限＝カーブを壊さない）。</summary>
    public static float WorldTierBias => Mathf.Min(1.2f, Mathf.Log(1f + OwnedCount) * 0.5f);
}
