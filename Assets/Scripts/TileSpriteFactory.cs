using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// タイルの見た目スプライトを手続き生成（Texture2D）してキャッシュする。外部画像なし。
/// 床/通路は石畳（縁取り＋上下ベベル）、宝箱/罠はアイコン付き。空間テーマの色調(tint)を焼き込む。
/// </summary>
public static class TileSpriteFactory
{
    private const int N = 32; // タイル解像度（px）＝1ワールドユニット
    private static readonly Dictionary<(DungeonGridSystem.TileType, Color), Sprite> cache
        = new Dictionary<(DungeonGridSystem.TileType, Color), Sprite>();

    public static Sprite Get(DungeonGridSystem.TileType type, Color tint)
    {
        var key = (type, tint);
        if (cache.TryGetValue(key, out var s)) return s;
        s = Build(type, tint);
        cache[key] = s;
        return s;
    }

    private static Color Mul(Color c, Color t)
        => new Color(Mathf.Clamp01(c.r * t.r), Mathf.Clamp01(c.g * t.g), Mathf.Clamp01(c.b * t.b), 1f);
    private static Color Scale(Color c, float k)
        => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);

    private static Sprite Build(DungeonGridSystem.TileType type, Color tint)
    {
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[N * N];

        bool corridor = (type == DungeonGridSystem.TileType.Corridor);
        // 素の石色（tint前）。通路は暗く平ら、部屋系は明るめで立体感。
        Color baseStone = corridor ? new Color(0.34f, 0.32f, 0.37f) : new Color(0.56f, 0.53f, 0.58f);
        Color baseCol = Mul(baseStone, tint);
        Color hi = Scale(baseCol, 1.20f);
        Color lo = Scale(baseCol, 0.78f);
        Color border = Scale(baseCol, 0.52f);

        // ベース塗り＋上下ベベル
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Color c = baseCol;
                if (!corridor)
                {
                    if (y >= N - 2) c = hi;        // 上辺（明）※SetPixelsは下から上なので上=大きいy
                    else if (y <= 1) c = lo;       // 下辺（暗）
                }
                px[y * N + x] = c;
            }
        // 外周1pxの縁取り
        for (int i = 0; i < N; i++)
        {
            px[i] = border; px[(N - 1) * N + i] = border;       // 下辺・上辺
            px[i * N] = border; px[i * N + (N - 1)] = border;   // 左辺・右辺
        }

        if (type == DungeonGridSystem.TileType.TreasureChest) DrawChest(px);
        else if (type == DungeonGridSystem.TileType.Trap) DrawTrap(px);

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }

    private static void Fill(Color[] px, int x0, int y0, int x1, int y1, Color c)
    {
        for (int y = Mathf.Max(0, y0); y <= Mathf.Min(N - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(N - 1, x1); x++)
                px[y * N + x] = c;
    }

    // 宝箱アイコン（金の箱＋蓋＋錠）
    private static void DrawChest(Color[] px)
    {
        Color body = new Color(0.72f, 0.53f, 0.20f);
        Color lid = new Color(0.86f, 0.67f, 0.29f);
        Color edge = new Color(0.42f, 0.29f, 0.09f);
        Color latch = new Color(0.97f, 0.86f, 0.52f);
        Fill(px, 8, 9, 23, 22, edge);       // 外枠
        Fill(px, 9, 10, 22, 21, body);      // 本体
        Fill(px, 9, 17, 22, 21, lid);       // 蓋
        Fill(px, 9, 16, 22, 16, edge);      // 蓋の境
        Fill(px, 14, 13, 17, 16, latch);    // 錠
    }

    // 罠アイコン（赤いスパイク3本＋暗い下地）
    private static void DrawTrap(Color[] px)
    {
        Color pit = new Color(0.16f, 0.10f, 0.11f);
        Color spike = new Color(0.82f, 0.28f, 0.26f);
        Fill(px, 6, 6, 25, 12, pit); // 暗い窪み
        int[] cxs = { 10, 16, 22 };
        int apexY = 22, h = 12, halfW = 4;
        foreach (int cx in cxs)
            for (int r = 0; r < h; r++)
            {
                int y = apexY - r;                       // 上が尖る
                int hw = Mathf.RoundToInt(halfW * (float)r / h);
                Fill(px, cx - hw, y, cx + hw, y, spike);
            }
    }
}
