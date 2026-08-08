using UnityEngine;

/// <summary>
/// 🎨 地上ヘクスの絵を1枚のアトラス（横に並べたテクスチャ）として実行時に焼く。
///
/// 盤を**1枚のメッシュ**で描く（[[SurfaceView]]）ので、タイルごとの絵はUVで切り替える。
/// 1タイル＝4頂点なので、1万タイルでも4万頂点＝メッシュ1つに収まる。
///
/// 各セルは「天面のヘクス＋下に伸びる側面（厚み）＋地形のモチーフ」を描いてある。
/// 天面は縦に潰して（`Squash`）俯瞰に見せる ― C1からの見た目を踏襲。
/// 関連: [[SurfaceView]] [[MarkerArt]]（UI用の小さな記号はあちら）。
/// </summary>
public static class HexTileArt
{
    public const int CellW = 128;          // 1タイルの絵の幅（＝ヘクスの横幅）
    // 天面の縦の潰し。俯瞰に見せるためのものだが、**強く潰すと盤全体が細長い帯になる**
    // （0.76 で試作盤が 2.17:1 になった）。0.90 でヘクスらしさと奥行きの両立。→ [[SurfaceGen]] の Dims
    public const float Squash = 0.90f;
    public const int Depth = 24;           // 側面の厚み（px）
    public static int HexH => Mathf.RoundToInt(CellW / 0.8660254f * Squash);   // 天面の高さ
    public static int CellH => HexH + Depth;

    // アトラスの並び（SurfaceMap.Terrain と同じ順＋末尾に未探索、そのあとに重ねる絵）
    //  0..6 地形 ／ 7 未探索 ／ 8 国境の輪郭 ／ 9 眷属 ／ 10 斥候 ／ 11 敵軍 ／ 12 選択
    public const int Count = 13;
    public const int FogIndex = 7;
    public const int OutlineIndex = 8;    // 🚩 支配の境界（白で描く。所有者の色で着色して使う）
    public const int KinIndex = 9;        // 👑 眷属（盾）
    public const int ScoutIndex = 10;     // 🔭 斥候（矢）
    public const int EnemyIndex = 11;     // ⚔️ 敵軍（角のある菱形）
    public const int SelectIndex = 12;    // 選択中の枠

    private static Texture2D _atlas;
    public static Texture2D Atlas { get { if (_atlas == null) Build(); return _atlas; } }

    /// <summary>アトラス内のUV矩形。</summary>
    public static Rect UvOf(int index)
    {
        var a = Atlas;
        float w = CellW / (float)a.width;
        return new Rect(index * w, 0f, w, 1f);
    }
    public static Rect UvOf(SurfaceMap.Terrain t) => UvOf((int)t);

    // 天面の色（C1の配色を踏襲）／側面はその暗い版
    private static readonly Color[] TopCol =
    {
        // 未探索(FogIndex)は背景に沈みすぎると盤に見えないので、少しだけ明るい石板色にする
        C(0x5a5060), C(0x6b7a4a), C(0x3f6b45), C(0x7a6a4a), C(0x6a6a78), C(0x4a6a68), C(0x1e3a58), C(0x272138),
    };
    private static readonly Color[] SideCol =
    {
        C(0x3d3543), C(0x4a5433), C(0x284630), C(0x54462c), C(0x4c4a5c), C(0x2c4746), C(0x122740), C(0x17131f),
    };
    private static Color C(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, 1f);

    private static void Build()
    {
        int w = CellW * Count, h = CellH;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[w * h];

        for (int t = 0; t < Count; t++)
        {
            int ox = t * CellW;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < CellW; x++)
                {
                    // 🚩 重ねる絵（地形ではないもの）は別に描く
                    if (t >= OutlineIndex)
                    {
                        px[y * w + ox + x] = Overlay(t, x, y - Depth);
                        continue;
                    }
                    // 天面は上寄せ（y は下が0）。側面は天面を Depth だけ下にずらしたもの。
                    float a = 0f; bool isTop = false;
                    if (InHex(x, y - Depth)) { a = 1f; isTop = true; }
                    else if (InHex(x, y)) a = 1f;              // 側面（天面に隠れない部分だけ残る）
                    if (a <= 0f) { px[y * w + ox + x] = new Color(0, 0, 0, 0); continue; }

                    Color c = isTop ? TopCol[t] : SideCol[t];   // ※ t < OutlineIndex のときだけここへ来る
                    if (isTop) c = Motif(t, x, y - Depth, c);
                    else c *= 0.92f;                           // 側面はさらに少し落とす
                    // ふちを少し暗くして輪郭を出す
                    float e = EdgeFade(x, isTop ? y - Depth : y);
                    c = new Color(c.r * e, c.g * e, c.b * e, 1f);
                    px[y * w + ox + x] = c;
                }
        }
        tex.SetPixels(px);
        tex.Apply();
        _atlas = tex;
    }

    /// <summary>
    /// 🚩 地形の上に重ねる絵（白＋アルファ）。使う側が色を掛けるので、ここでは形だけ描く。
    /// ⚠ 記号（◆□×＋）で代用すると**フォントに無い字が□になる**。絵にすれば根治する（[[UIIcons]] と同じ理由）。
    /// </summary>
    private static Color Overlay(int t, int px, int py)
    {
        var clear = new Color(1, 1, 1, 0);
        if (py < 0 || py >= HexH) return clear;
        float x = (px + 0.5f) / CellW * 2f - 1f;         // -1..1
        float y = (py + 0.5f) / HexH * 2f - 1f;          // -1..1（上が+）
        switch (t)
        {
            case OutlineIndex:
            {
                // ヘクスの縁の内側だけを残す帯＝支配の境界線
                float d = InHexDepth(x, y);
                return (d >= 0f && d < 0.16f) ? Color.white : clear;
            }
            case SelectIndex:
            {
                float d = InHexDepth(x, y);
                return (d >= 0f && d < 0.09f) ? Color.white : clear;
            }
            case KinIndex:
            {
                // 盾（上は平ら・下は尖る）
                if (Mathf.Abs(x) > 0.34f || y > 0.42f || y < -0.46f) return clear;
                float t2 = Mathf.InverseLerp(0.42f, -0.46f, y);
                float half = Mathf.Lerp(0.34f, 0.02f, t2 * t2);
                return Mathf.Abs(x) <= half ? Color.white : clear;
            }
            case ScoutIndex:
            {
                // 上向きの矢
                if (y > 0.44f || y < -0.40f) return clear;
                float t3 = Mathf.InverseLerp(0.44f, -0.40f, y);
                float half = Mathf.Lerp(0.02f, 0.36f, t3);
                if (Mathf.Abs(x) > half) return clear;
                return (t3 > 0.62f && Mathf.Abs(x) > half - 0.16f) ? clear : Color.white;
            }
            case EnemyIndex:
            {
                // 角のある菱形（敵）
                float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
                if (ax / 0.40f + ay / 0.48f > 1f) return clear;
                if (ax / 0.22f + ay / 0.26f < 1f) return clear;   // 中を抜いて見やすく
                return Color.white;
            }
        }
        return clear;
    }

    /// <summary>ヘクスの縁からの距離（0=縁 / 大きいほど内側 / 負ならヘクスの外）。</summary>
    private static float InHexDepth(float x, float y)
    {
        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        float limit = 1f - ax * 0.5f;
        if (ax > 1f || ay > limit) return -1f;
        return Mathf.Min(1f - ax, (limit - ay) * 0.9f);
    }

    /// <summary>天面のヘクス（pointy-top を縦に潰したもの）の内側か。</summary>
    private static bool InHex(int px, int py)
    {
        // pointy-top の頂点は 上(0,1)/下(0,-1)/横(±1,±0.5) → 内側は |y| <= 1 - |x|/2
        float x = (px + 0.5f) / CellW * 2f - 1f;                       // -1..1
        float y = (py + 0.5f) / HexH * 2f - 1f;                        // -1..1
        return Mathf.Abs(x) <= 1f && Mathf.Abs(y) <= 1f - Mathf.Abs(x) * 0.5f;
    }

    private static float EdgeFade(int px, int py)
    {
        float x = (px + 0.5f) / CellW * 2f - 1f;
        float y = (py + 0.5f) / HexH * 2f - 1f;
        float d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
        return d > 0.93f ? 0.78f : 1f;
    }

    /// <summary>地形のモチーフ（山なら尖り、森なら木立…）。単色だと平坦なので少しだけ形を描く。</summary>
    private static Color Motif(int t, int px, int py, Color baseCol)
    {
        float x = (px + 0.5f) / CellW * 2f - 1f;
        float y = (py + 0.5f) / HexH * 2f - 1f;
        switch ((SurfaceMap.Terrain)t)
        {
            case SurfaceMap.Terrain.Mountain:
                // 3つの尖り
                for (int i = -1; i <= 1; i++)
                {
                    float cx = i * 0.42f, hgt = i == 0 ? 0.62f : 0.44f;
                    if (Mathf.Abs(x - cx) < (hgt - (y + 0.35f)) * 0.55f && y > -0.35f && y < hgt - 0.35f)
                        return Lighten(baseCol, y > hgt - 0.55f ? 0.42f : 0.20f);
                }
                break;
            case SurfaceMap.Terrain.Hills:
                for (int i = -1; i <= 1; i += 2)
                {
                    float cx = i * 0.34f;
                    if ((x - cx) * (x - cx) + (y + 0.10f) * (y + 0.10f) * 2.4f < 0.055f) return Lighten(baseCol, 0.18f);
                }
                break;
            case SurfaceMap.Terrain.Forest:
                for (int i = -1; i <= 1; i++)
                {
                    float cx = i * 0.38f, cy = i == 0 ? 0.10f : -0.12f;
                    if (Mathf.Abs(x - cx) < (0.30f - (y - cy)) * 0.42f && y > cy - 0.02f && y < cy + 0.30f)
                        return Darken(baseCol, 0.30f);
                }
                break;
            case SurfaceMap.Terrain.Marsh:
                if (Mathf.Abs(y - Mathf.Sin(x * 6f) * 0.10f + 0.05f) < 0.055f) return Lighten(baseCol, 0.16f);
                break;
            case SurfaceMap.Terrain.Ocean:
                if (Mathf.Abs(y - Mathf.Sin(x * 5f + 1f) * 0.10f - 0.18f) < 0.045f) return Lighten(baseCol, 0.14f);
                if (Mathf.Abs(y - Mathf.Sin(x * 5f) * 0.10f + 0.22f) < 0.045f) return Lighten(baseCol, 0.10f);
                break;
            case SurfaceMap.Terrain.Plains:
                if (Mathf.Abs(y + 0.30f) < 0.03f && Mathf.Abs(x) < 0.45f) return Lighten(baseCol, 0.10f);
                break;
            default:   // 荒地：斑
                if (((px * 7 + py * 13) % 37) < 3) return Lighten(baseCol, 0.12f);
                break;
        }
        return baseCol;
    }

    private static Color Lighten(Color c, float k) => new Color(c.r + (1 - c.r) * k, c.g + (1 - c.g) * k, c.b + (1 - c.b) * k, 1f);
    private static Color Darken(Color c, float k) => new Color(c.r * (1 - k), c.g * (1 - k), c.b * (1 - k), 1f);
}
