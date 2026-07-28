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
/// 盤は axial 半径 R のヘクス（タイル数 = 3R(R+1)+1）。小5=91 / 中7=169 / 大9=271。
/// 中心(0,0)は必ず陸で、そこがダンジョンの入口になる。
/// 関連: [[SurfaceMap]] [[civ-surface-districts]]。
/// </summary>
public static class SurfaceGen
{
    public enum Size { Small = 5, Medium = 7, Large = 9 }
    public static int TileCount(Size s) { int R = (int)s; return 3 * R * (R + 1) + 1; }

    private static readonly int[,] Dirs = { { 1, 0 }, { 1, -1 }, { 0, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 } };
    private static int Dist(int q, int r) => (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(q + r)) / 2;

    private class Cell
    {
        public int q, r, dist;
        public int plate;
        public bool land, coast, mountain, volcano, river;
        public SurfaceMap.Terrain terrain;
        public SurfaceMap.Resource resource;
        public int naturalWonder = -1;
    }

    /// <summary>盤を生成して SurfaceMap 用の Region 一覧を返す。</summary>
    public static List<SurfaceMap.Region> Generate(Size size, int seed)
    {
        var rng = new System.Random(seed);
        int R = (int)size;

        // ── ① ヘクスを敷き、プレートの種を撒く ──
        var cells = new List<Cell>();
        var byKey = new Dictionary<long, Cell>();
        for (int q = -R; q <= R; q++)
            for (int r = Mathf.Max(-R, -q - R); r <= Mathf.Min(R, -q + R); r++)
            {
                var c = new Cell { q = q, r = r, dist = Dist(q, r), plate = -1 };
                cells.Add(c); byKey[Key(q, r)] = c;
            }

        int plateCount = Mathf.Max(4, R);                      // プレート数は盤の大きさに比例
        var seeds = new List<Cell>();
        for (int i = 0; i < plateCount; i++)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                var c = cells[rng.Next(cells.Count)];
                if (c.plate >= 0) continue;
                c.plate = i; seeds.Add(c); break;
            }
        }

        // ── ② プレートを成長させる（BFS＝ボロノイ相当） ──
        var queue = new Queue<Cell>(seeds);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var n in Neigh(byKey, c))
                if (n.plate < 0) { n.plate = c.plate; queue.Enqueue(n); }
        }

        // ── ③ プレートごとに陸か海かを決める（中心のプレートは必ず陸） ──
        int centerPlate = byKey[Key(0, 0)].plate;
        var isLandPlate = new bool[plateCount];
        for (int i = 0; i < plateCount; i++) isLandPlate[i] = rng.NextDouble() < 0.55;
        isLandPlate[centerPlate] = true;
        int landPlates = 0; foreach (var b in isLandPlate) if (b) landPlates++;
        if (landPlates < 2) for (int i = 0; i < plateCount && landPlates < 2; i++) if (!isLandPlate[i]) { isLandPlate[i] = true; landPlates++; }
        foreach (var c in cells) c.land = isLandPlate[c.plate];

        // 外周は海に寄せる（盤の外が海で閉じている感じを出す）
        foreach (var c in cells) if (c.dist >= R && rng.NextDouble() < 0.75) c.land = false;

        // ── ④ 浸食と島：孤立した陸/海をならして、直線的な海岸線を消す ──
        for (int pass = 0; pass < 2; pass++)
        {
            var flip = new List<Cell>();
            foreach (var c in cells)
            {
                if (c.dist == 0) continue;                        // 中心は必ず陸のまま
                int landN = 0, total = 0;
                foreach (var n in Neigh(byKey, c)) { total++; if (n.land) landN++; }
                if (c.land && landN <= 1) flip.Add(c);            // 出っ張りを削る
                else if (!c.land && landN >= total - 1 && total >= 5) flip.Add(c); // 内海を埋める
            }
            foreach (var c in flip) c.land = !c.land;
        }
        // 中心の周りは必ず陸（初手で詰まないように）
        foreach (var n in Neigh(byKey, byKey[Key(0, 0)])) n.land = true;

        // ── ⑤ 海岸・山・火山 ──
        foreach (var c in cells)
        {
            if (!c.land) continue;
            foreach (var n in Neigh(byKey, c)) if (!n.land) { c.coast = true; break; }
        }
        foreach (var c in cells)
        {
            if (!c.land || c.dist == 0) continue;
            bool border = false;
            foreach (var n in Neigh(byKey, c)) if (n.plate != c.plate && n.land) { border = true; break; }
            if (border && rng.NextDouble() < 0.55) c.mountain = true;      // プレート境界に山脈
            else if (!c.coast && rng.NextDouble() < 0.07) c.mountain = true;
            if (c.mountain && rng.NextDouble() < 0.12) c.volcano = true;
        }

        // ── ⑥ バイオーム・川・資源 ──
        foreach (var c in cells)
        {
            if (!c.land) { c.terrain = SurfaceMap.Terrain.Ocean; continue; }
            if (c.mountain) { c.terrain = SurfaceMap.Terrain.Mountain; continue; }
            double n = rng.NextDouble();
            if (c.dist == 0) c.terrain = SurfaceMap.Terrain.Waste;
            else if (c.coast && n < 0.30) c.terrain = SurfaceMap.Terrain.Marsh;
            else if (n < 0.28) c.terrain = SurfaceMap.Terrain.Forest;
            else if (n < 0.48) c.terrain = SurfaceMap.Terrain.Hills;
            else if (n < 0.90) c.terrain = SurfaceMap.Terrain.Plains;
            else c.terrain = SurfaceMap.Terrain.Waste;
        }
        // 川：山から海へ下る筋を数本
        int rivers = Mathf.Max(2, R - 1);
        for (int i = 0; i < rivers; i++)
        {
            Cell cur = null;
            for (int t = 0; t < 30 && cur == null; t++) { var c = cells[rng.Next(cells.Count)]; if (c.land && c.mountain) cur = c; }
            if (cur == null) continue;
            for (int step = 0; step < R * 2; step++)
            {
                cur.river = true;
                Cell next = null; int best = int.MaxValue;
                foreach (var n in Neigh(byKey, cur))
                {
                    if (n.river) continue;
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
        // 🏔️ 自然の驚異：陸の奥まったところに数個
        int nwCount = Mathf.Clamp(R - 2, 2, NaturalWonders.Length);
        var nwPool = new List<int>(); for (int i = 0; i < NaturalWonders.Length; i++) nwPool.Add(i);
        Shuffle(nwPool, rng);
        var nwCand = new List<Cell>();
        foreach (var c in cells) if (c.land && c.dist >= 2 && c.naturalWonder < 0) nwCand.Add(c);
        Shuffle(nwCand, rng);
        for (int i = 0; i < nwCount && i < nwCand.Count; i++) nwCand[i].naturalWonder = nwPool[i];

        // ── ⑦ Region へ変換 ──
        var regions = new List<SurfaceMap.Region>();
        int id = 0;
        foreach (var c in cells)
        {
            var reg = new SurfaceMap.Region
            {
                id = id++, q = c.q, r = c.r,
                terrain = c.terrain, river = c.river, resource = c.resource,
                wonder = c.naturalWonder >= 0, naturalWonder = c.naturalWonder,
                isOcean = !c.land, isCoast = c.coast, volcano = c.volcano,
                links = new int[0]
            };
            reg.name = NameFor(c, rng);
            reg.type = TypeFor(c);
            // 防衛と産出は中心からの距離で伸ばす（外側ほど手強く、実入りも良い）
            float k = 1f + c.dist * 0.55f;
            reg.defense = c.land ? Mathf.RoundToInt((40 + c.dist * 95) * (c.mountain ? 1.25f : 1f)) : 0;
            reg.dpYield = c.land ? Mathf.RoundToInt(18 * k) : 0;
            reg.matYield = c.land ? Mathf.RoundToInt(1 + c.dist * 0.8f) : 0;
            reg.rpYield = c.land && c.dist >= 3 ? Mathf.RoundToInt(c.dist * 0.4f) : 0;
            reg.fameYield = c.land ? Mathf.RoundToInt(4 + c.dist * 2.2f) : 0;
            if (c.naturalWonder >= 0) { reg.defense = Mathf.RoundToInt(reg.defense * 1.4f); reg.fameYield += 8; }
            regions.Add(reg);
        }
        return regions;
    }

    private static long Key(int q, int r) => ((long)(q + 1000) << 20) | (uint)(r + 1000);
    private static IEnumerable<Cell> Neigh(Dictionary<long, Cell> map, Cell c)
    {
        for (int d = 0; d < 6; d++)
        {
            Cell n;
            if (map.TryGetValue(Key(c.q + Dirs[d, 0], c.r + Dirs[d, 1]), out n)) yield return n;
        }
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
            case SurfaceMap.Terrain.Hills: return c.dist >= 4 ? SurfaceMap.RegionType.Fort : SurfaceMap.RegionType.Mine;
            case SurfaceMap.Terrain.Marsh: return SurfaceMap.RegionType.Forest;
            default: return c.dist >= 5 ? SurfaceMap.RegionType.City : (c.dist >= 3 ? SurfaceMap.RegionType.Town : SurfaceMap.RegionType.Village);
        }
    }
}
