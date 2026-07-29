using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🌍 地上盤の手続き生成（Civ VII 1.2.5 の「ボロノイ図でプレートを模擬する」方式を参考にした簡易版）。
///
/// Civ VII の生成手順:
///   ①低解像度で点を撒いてプレートを模擬 → ②ルールに従ってプレートを成長 → ③解像度を上げて陸塊を作る
///   → ④島を伸ばし海岸を浸食し、山と火山を足す → ⑤ヘクスに割り当てる
/// ここでも同じ順序を踏む。狙いも同じで、**直線的な海岸線を作らず、95%は"普通"の盤**になるよう調整してある。
///
/// 盤は **幅W×高さHの長方形で東西がループ**する（[[HexGrid]]）。南北の端は極地として海に寄せる。
/// 盤の中央がダンジョンの入口で、必ず陸。
///
/// ⚠ 防衛や産出は「入口からの距離」で伸ばすが、**盤の大きさで距離の最大値が変わる**ので、
///   そのまま使うと大きい盤で防衛が青天井になる。**0〜9に正規化した `depth`** に直してから使う。
/// 関連: [[SurfaceMap]] [[civ-surface-districts]] [[civ7-roadmap]]。
/// </summary>
public static class SurfaceGen
{
    /// <summary>盤の大きさ。数値は Civ の実寸に合わせてある。</summary>
    public enum Size { Proto = 0, Small = 1, Medium = 2, Large = 3 }

    // ⚠ 盤の見た目の比は「タイル数の比」ではない。
    //    列の間隔は √3・size、行の間隔は 1.5・size・Squash なので、実際の比は **(1.1547 / Squash) × W/H**。
    //    Squash=0.76 で 20×14 にしていたら実測 **2.17:1** の細長い帯になり、「横に並びすぎ」に見えていた。
    //    Squash を 0.90 に緩めたうえで、**世界1つが 16:9** に収まる W/H = 1.386 で組み直してある。
    private static readonly int[,] Dims = { { 19, 14 }, { 57, 41 }, { 79, 57 }, { 98, 71 } };

    public static int WidthOf(Size s) => Dims[Mathf.Clamp((int)s, 0, 3), 0];
    public static int HeightOf(Size s) => Dims[Mathf.Clamp((int)s, 0, 3), 1];
    public static int TileCount(Size s) => WidthOf(s) * HeightOf(s);
    public static string NameOf(Size s)
        => s == Size.Proto ? "試作" : s == Size.Small ? "小" : s == Size.Medium ? "中" : "大";

    private class Cell
    {
        public int col, row, dist;
        public float depth;            // 0〜9に正規化した「入口からの遠さ」
        public int plate;
        public bool land, coast, mountain, volcano, river;
        public SurfaceMap.Terrain terrain;
        public SurfaceMap.Resource resource;
        public int naturalWonder = -1;
    }

    /// <summary>盤を生成して SurfaceMap 用の Region 一覧を返す。</summary>
    public static List<SurfaceMap.Region> Generate(Size size, int seed)
        => Generate(WidthOf(size), HeightOf(size), seed);

    public static List<SurfaceMap.Region> Generate(int W, int H, int seed)
    {
        var rng = new System.Random(seed);
        int gateCol = W / 2, gateRow = H / 2;

        // ── ① ヘクスを敷き、プレートの種を撒く ──
        var cells = new Cell[W * H];
        int maxDist = 1;
        for (int row = 0; row < H; row++)
            for (int col = 0; col < W; col++)
            {
                int d = HexGrid.Distance(col, row, gateCol, gateRow, W);
                if (d > maxDist) maxDist = d;
                cells[row * W + col] = new Cell { col = col, row = row, dist = d, plate = -1 };
            }
        foreach (var c in cells) c.depth = c.dist * 9f / maxDist;   // 盤の大きさによらず 0〜9 に揃える

        int plateCount = Mathf.Clamp(W * H / 90, 5, 60);       // プレート数は面積に比例
        var seeds = new List<Cell>();
        for (int i = 0; i < plateCount; i++)
            for (int tries = 0; tries < 40; tries++)
            {
                var c = cells[rng.Next(cells.Length)];
                if (c.plate >= 0) continue;
                c.plate = i; seeds.Add(c); break;
            }

        // ── ② プレートを成長させる（BFS＝ボロノイ相当） ──
        var queue = new Queue<Cell>(seeds);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            for (int d = 0; d < 6; d++)
            {
                var n = Neigh(cells, c, d, W, H);
                if (n != null && n.plate < 0) { n.plate = c.plate; queue.Enqueue(n); }
            }
        }

        // ── ③ プレートごとに陸か海かを決める（入口のプレートは必ず陸） ──
        int gatePlate = cells[gateRow * W + gateCol].plate;
        var isLandPlate = new bool[plateCount];
        for (int i = 0; i < plateCount; i++) isLandPlate[i] = rng.NextDouble() < 0.45;
        if (gatePlate >= 0) isLandPlate[gatePlate] = true;

        // 🌏 陸の割合を目標に寄せる。
        //    ※プレートの当たり外れ任せだと実測で陸27%〜55%とばらつき、盤ごとに遊びが別物になった。
        //      地球は約29%、Civの盤も概ね3〜4割なので 0.42（小さい盤は詰まらないよう 0.50）を狙う。
        var plateSize = new int[plateCount];
        foreach (var c in cells) if (c.plate >= 0) plateSize[c.plate]++;
        float target = (W * H <= 600 ? 0.50f : 0.42f) * cells.Length;
        int landCells = 0;
        for (int i = 0; i < plateCount; i++) if (isLandPlate[i]) landCells += plateSize[i];
        var order = new List<int>(); for (int i = 0; i < plateCount; i++) order.Add(i);
        Shuffle(order, rng);
        foreach (int i in order)                       // 足りなければ陸を増やす
        {
            if (landCells >= target) break;
            if (isLandPlate[i]) continue;
            isLandPlate[i] = true; landCells += plateSize[i];
        }
        foreach (int i in order)                       // 多すぎれば海に戻す（入口のプレートは残す）
        {
            if (landCells <= target * 1.15f) break;
            if (!isLandPlate[i] || i == gatePlate) continue;
            if (landCells - plateSize[i] < target * 0.85f) continue;
            isLandPlate[i] = false; landCells -= plateSize[i];
        }
        foreach (var c in cells) c.land = c.plate >= 0 && isLandPlate[c.plate];

        // 🧊 南北の端は極地として海に寄せる（東西はループするので端が無い＝Civと同じ）
        int polar = Mathf.Max(1, H / 14);
        foreach (var c in cells)
        {
            int edge = Mathf.Min(c.row, H - 1 - c.row);
            if (edge < polar) c.land = false;
            else if (edge < polar + 1 && rng.NextDouble() < 0.6) c.land = false;
        }

        // ── ④ 浸食と島：孤立した陸/海をならして、直線的な海岸線を消す ──
        for (int pass = 0; pass < 2; pass++)
        {
            var flip = new List<Cell>();
            foreach (var c in cells)
            {
                if (c.dist == 0 || c.row < polar || c.row >= H - polar) continue;
                int landN = 0, total = 0;
                for (int d = 0; d < 6; d++) { var n = Neigh(cells, c, d, W, H); if (n == null) continue; total++; if (n.land) landN++; }
                if (c.land && landN <= 1) flip.Add(c);                                // 出っ張りを削る
                else if (!c.land && landN >= total - 1 && total >= 5) flip.Add(c);     // 内海を埋める
            }
            foreach (var c in flip) c.land = !c.land;
        }
        // 入口とその周りは必ず陸（初手で詰まないように）
        var gate = cells[gateRow * W + gateCol];
        gate.land = true;
        for (int d = 0; d < 6; d++) { var n = Neigh(cells, gate, d, W, H); if (n != null) n.land = true; }

        // ── ⑤ 海岸・山・火山 ──
        foreach (var c in cells)
        {
            if (!c.land) continue;
            for (int d = 0; d < 6; d++) { var n = Neigh(cells, c, d, W, H); if (n == null || !n.land) { c.coast = true; break; } }
        }
        foreach (var c in cells)
        {
            if (!c.land || c.dist == 0) continue;
            bool border = false;
            for (int d = 0; d < 6; d++) { var n = Neigh(cells, c, d, W, H); if (n != null && n.plate != c.plate && n.land) { border = true; break; } }
            if (border && rng.NextDouble() < 0.45) c.mountain = true;      // プレート境界に山脈
            else if (!c.coast && rng.NextDouble() < 0.06) c.mountain = true;
            if (c.mountain && rng.NextDouble() < 0.12) c.volcano = true;
        }

        // ── ⑥ バイオーム・川・資源 ──
        //     緯度で気候を変える（極寄り＝荒れ地/丘、赤道寄り＝森/湿地）＝Civのバイオーム帯
        foreach (var c in cells)
        {
            if (!c.land) { c.terrain = SurfaceMap.Terrain.Ocean; continue; }
            if (c.mountain) { c.terrain = SurfaceMap.Terrain.Mountain; continue; }
            if (c.dist == 0) { c.terrain = SurfaceMap.Terrain.Waste; continue; }
            float lat = Mathf.Abs(c.row - (H - 1) * 0.5f) / ((H - 1) * 0.5f);   // 0=赤道 1=極
            double n = rng.NextDouble();
            if (lat > 0.72f) c.terrain = n < 0.45 ? SurfaceMap.Terrain.Waste : (n < 0.80 ? SurfaceMap.Terrain.Hills : SurfaceMap.Terrain.Plains);
            else if (c.coast && n < 0.26) c.terrain = SurfaceMap.Terrain.Marsh;
            else if (n < 0.20 + (1f - lat) * 0.22f) c.terrain = SurfaceMap.Terrain.Forest;
            else if (n < 0.52) c.terrain = SurfaceMap.Terrain.Hills;
            else if (n < 0.93) c.terrain = SurfaceMap.Terrain.Plains;
            else c.terrain = SurfaceMap.Terrain.Waste;
        }
        // 川：山から海へ下る筋。本数は面積に比例
        int rivers = Mathf.Clamp(W * H / 120, 3, 80);
        for (int i = 0; i < rivers; i++)
        {
            Cell cur = null;
            for (int t = 0; t < 40 && cur == null; t++) { var c = cells[rng.Next(cells.Length)]; if (c.land && c.mountain) cur = c; }
            if (cur == null) continue;
            for (int step = 0; step < 24; step++)
            {
                cur.river = true;
                Cell next = null; int best = int.MaxValue;
                for (int d = 0; d < 6; d++)
                {
                    var n = Neigh(cells, cur, d, W, H);
                    if (n == null || n.river) continue;
                    int score = n.land ? n.dist : -100;            // 海に向かって下る
                    if (score < best) { best = score; next = n; }
                }
                if (next == null || !next.land) break;
                cur = next;
            }
        }
        // 資源：地形に合ったものを撒く
        foreach (var c in cells)
        {
            if (!c.land || c.dist == 0) continue;
            if (rng.NextDouble() > 0.30) continue;
            switch (c.terrain)
            {
                case SurfaceMap.Terrain.Mountain: c.resource = rng.NextDouble() < 0.5 ? SurfaceMap.Resource.Manastone : SurfaceMap.Resource.Gem; break;
                case SurfaceMap.Terrain.Hills: c.resource = rng.NextDouble() < 0.6 ? SurfaceMap.Resource.Iron : SurfaceMap.Resource.Gem; break;
                case SurfaceMap.Terrain.Forest: c.resource = SurfaceMap.Resource.Timber; break;
                case SurfaceMap.Terrain.Plains: c.resource = rng.NextDouble() < 0.5 ? SurfaceMap.Resource.Grain : SurfaceMap.Resource.Livestock; break;
                default: break;
            }
        }
        // 🏔️ 自然の驚異：陸の奥まったところに。盤が広いほど多い（種類は使い回す）
        int nwCount = Mathf.Clamp(W * H / 400, 2, 24);
        var nwCand = new List<Cell>();
        foreach (var c in cells) if (c.land && c.depth >= 2f) nwCand.Add(c);
        Shuffle(nwCand, rng);
        for (int i = 0; i < nwCount && i < nwCand.Count; i++) nwCand[i].naturalWonder = rng.Next(NaturalWonders.Length);

        // ── ⑦ Region へ変換 ──
        var regions = new List<SurfaceMap.Region>(cells.Length);
        for (int i = 0; i < cells.Length; i++)
        {
            var c = cells[i];
            var reg = new SurfaceMap.Region
            {
                id = i, col = c.col, row = c.row, depth = c.depth,
                terrain = c.terrain, river = c.river, resource = c.resource,
                wonder = c.naturalWonder >= 0, naturalWonder = c.naturalWonder,
                isOcean = !c.land, isCoast = c.coast, volcano = c.volcano,
                links = new int[0]
            };
            reg.name = NameFor(c, rng);
            reg.type = TypeFor(c);
            // 防衛と産出は入口からの遠さで伸ばす（外側ほど手強く、実入りも良い）。
            // depth は 0〜9 に正規化済みなので、盤を広げても1タイルあたりの強さは変わらない。
            float k = 1f + c.depth * 0.55f;
            reg.defense = c.land ? Mathf.RoundToInt((40 + c.depth * 95) * (c.mountain ? 1.25f : 1f)) : 0;
            reg.dpYield = c.land ? Mathf.RoundToInt(18 * k) : 0;
            reg.matYield = c.land ? Mathf.RoundToInt(1 + c.depth * 0.8f) : 0;
            reg.rpYield = c.land && c.depth >= 3f ? Mathf.RoundToInt(c.depth * 0.4f) : 0;
            reg.fameYield = c.land ? Mathf.RoundToInt(4 + c.depth * 2.2f) : 0;
            if (c.naturalWonder >= 0) { reg.defense = Mathf.RoundToInt(reg.defense * 1.4f); reg.fameYield += 8; }
            regions.Add(reg);
        }
        return regions;
    }

    private static Cell Neigh(Cell[] cells, Cell c, int d, int W, int H)
    {
        int nc, nr;
        if (!HexGrid.Neighbor(c.col, c.row, d, W, H, out nc, out nr)) return null;
        return cells[nr * W + nc];
    }

    private static void Shuffle<T>(List<T> l, System.Random rng)
    {
        for (int i = 0; i < l.Count; i++) { int j = rng.Next(i, l.Count); var t = l[i]; l[i] = l[j]; l[j] = t; }
    }

    // ============ 🏔️ 自然の驚異（Civの Natural Wonder。固有名＋効果） ============
    public struct NaturalWonderDef { public string jpName, desc; public string colorHex; }
    public static readonly NaturalWonderDef[] NaturalWonders =
    {
        NW("虚ろの大穴",   "隣接する施設の産出 +2",             "#8cb8e6"),
        NW("燃える湖",     "隣接する鉱錬所・兵舎の産出 +2",     "#e0703c"),
        NW("千年樹",       "隣接する祭壇の産出 +3",             "#5cc47c"),
        NW("霜の女王像",   "この領域の守り +150",               "#7fd3e6"),
        NW("囁く石柱群",   "隣接する魔泉の産出 +3",             "#b478e6"),
        NW("血染めの滝",   "この領域で得る名声 +50%",           "#c04a6a"),
        NW("天泣の谷",     "隣接タイルの食料 +1",               "#57c3ab"),
    };
    private static NaturalWonderDef NW(string n, string d, string c) => new NaturalWonderDef { jpName = n, desc = d, colorHex = c };

    // ============ 地名の生成 ============
    private static readonly string[] PreLand = { "灰かぶり", "霧ざわめく", "鉄錆の", "麦守りの", "古き樹の", "深層", "宿場", "職人街", "祈りの", "廃", "石造りの", "辺境", "騎士団", "城塞", "塩の", "渡し守の", "黒曜の", "囁きの", "灯台", "隠れ里", "朽ちた", "銀鉱の", "風抜けの", "巡礼の", "北の", "硫黄の", "忘れられた", "綻びの", "石切り", "古", "涸れ井戸の", "星降りの", "翠玉の", "薄氷の", "陽炎の", "轟きの", "静寂の", "赤錆の" };
    private static readonly string[] SufPlains = { "集落", "里", "街道", "牧草地", "平原", "宿場町" };
    private static readonly string[] SufForest = { "森", "祠", "樹海", "木立", "苗床" };
    private static readonly string[] SufHills = { "丘", "峠", "砦", "採石場", "段丘" };
    private static readonly string[] SufMount = { "坑道", "鉱脈", "断崖", "霊峰", "坑洞" };
    private static readonly string[] SufMarsh = { "湿原", "沼沢", "池", "水路" };
    private static readonly string[] SufOcean = { "海", "浅瀬", "外洋", "海溝", "水道" };

    private static string NameFor(Cell c, System.Random rng)
    {
        if (c.dist == 0) return "迷宮前の荒れ地";
        string[] suf;
        switch (c.terrain)
        {
            case SurfaceMap.Terrain.Ocean: suf = SufOcean; break;
            case SurfaceMap.Terrain.Forest: suf = SufForest; break;
            case SurfaceMap.Terrain.Hills: suf = SufHills; break;
            case SurfaceMap.Terrain.Mountain: suf = SufMount; break;
            case SurfaceMap.Terrain.Marsh: suf = SufMarsh; break;
            default: suf = SufPlains; break;
        }
        return PreLand[rng.Next(PreLand.Length)] + suf[rng.Next(suf.Length)];
    }

    private static SurfaceMap.RegionType TypeFor(Cell c)
    {
        if (c.dist == 0) return SurfaceMap.RegionType.Gate;
        if (!c.land) return SurfaceMap.RegionType.Sea;
        switch (c.terrain)
        {
            case SurfaceMap.Terrain.Forest: return SurfaceMap.RegionType.Forest;
            case SurfaceMap.Terrain.Mountain: return SurfaceMap.RegionType.Mine;
            case SurfaceMap.Terrain.Hills: return c.depth >= 4f ? SurfaceMap.RegionType.Fort : SurfaceMap.RegionType.Mine;
            case SurfaceMap.Terrain.Marsh: return SurfaceMap.RegionType.Forest;
            default: return c.depth >= 5f ? SurfaceMap.RegionType.City : (c.depth >= 3f ? SurfaceMap.RegionType.Town : SurfaceMap.RegionType.Village);
        }
    }
}
