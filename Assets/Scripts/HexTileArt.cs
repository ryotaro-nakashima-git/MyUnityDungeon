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
    //  13 軍団(近接) ／ 14 軍団(射手)
    public const int Count = 15;
    public const int FogIndex = 7;
    public const int OutlineIndex = 8;    // 🚩 支配の境界（白で描く。所有者の色で着色して使う）
    public const int KinIndex = 9;        // 👑 眷属＝司令官（盾）
    public const int ScoutIndex = 10;     // 🔭 斥候（矢）
    public const int EnemyIndex = 11;     // ⚔️ 敵軍（角のある菱形）
    public const int SelectIndex = 12;    // 選択中の枠
    // ⚔️ 軍団（U-1）。**兵科が形で分かる**ようにする＝戦線を見て「前が前衛・後ろが射手」と読める。
    //    色でも分けるが、色だけだと敵軍と紛れる（実測で菱形と区別が付かなかった）。
    public const int LegionIndex = 13;      // 近接（前衛/突撃）＝横長の隊列ブロック
    public const int LegionRangedIndex = 14; // 射手/術者＝山形（後ろから撃つ形）

    // ============ 🖼️ 外部の絵をアトラスに焼く（施設・拠点・配下） ============
    // ⚠ 盤は**1枚のメッシュ**なので、絵を増やすにはアトラスにセルを足すしかない。
    //    セルが横1列だと 128px×68 で 8,704px になり**テクスチャ上限(8192)を超える**。
    //    だからグリッド（Cols列）に並べる。UvOf を通していれば呼ぶ側は変えなくてよい。
    public const int Cols = 8;

    /// <summary>`Resources/Surface/<name>.png` から焼くセル。並び順が index になる。</summary>
    private static readonly string[] SpriteCells =
    {
        // 🏛️ 施設16種（DistrictCatalog の id と同じ名前で置く）
        "manafount", "altar", "market", "forge", "barracks", "warehouse", "training",
        "farm", "harbor", "bazaar", "shrine", "masonry", "embassy", "arsenal", "academy", "hideout",
        // 🏙️ 拠点と都市と砦
        "town", "city", "fort",
    };
    public const int SpriteBase = Count;                       // 外部セルの開始index
    public static int SpriteCellCount => SpriteCells.Length;
    public static int TotalCells => Count + SpriteCells.Length + MinionCellCount;
    /// <summary>
    /// 🧟 配下34種＋👾ユニークの1枚絵も焼く（軍団と眷属を**種の姿**で盤に出すため）。
    /// ⚠ ユニークは `catalogIndex` が 1000 以上なので、そのままでは列に並べられない。
    ///   末尾に**通常種のあと**へ詰めて置き、`MinionIndex` で振り分ける。
    /// </summary>
    public static int MinionCellCount => MinionCatalog.Count + UniqueCatalog.Count;
    public static int MinionBase => Count + SpriteCells.Length;

    /// <summary>施設・拠点の名前からセルindexを引く（無ければ -1）。</summary>
    public static int SpriteIndex(string name)
    {
        for (int i = 0; i < SpriteCells.Length; i++) if (SpriteCells[i] == name) return SpriteBase + i;
        return -1;
    }
    /// <summary>配下の種（catalog index）からセルindexを引く。ユニークも通せる。</summary>
    public static int MinionIndex(int catalogIndex)
    {
        if (UniqueCatalog.IsUnique(catalogIndex))
        {
            int u = UniqueCatalog.LocalOf(catalogIndex);
            if (u < 0 || u >= UniqueCatalog.Count) return -1;
            return MinionBase + MinionCatalog.Count + u;
        }
        if (catalogIndex < 0 || catalogIndex >= MinionCatalog.Count) return -1;
        return MinionBase + catalogIndex;
    }

    private static Texture2D _atlas;
    public static Texture2D Atlas { get { if (_atlas == null) Build(); return _atlas; } }

    /// <summary>アトラス内のUV矩形（グリッド）。</summary>
    public static Rect UvOf(int index)
    {
        var a = Atlas;
        int rows = Mathf.CeilToInt(TotalCells / (float)Cols);
        int cx = index % Cols, cy = index / Cols;
        float w = CellW / (float)a.width;
        float h = CellH / (float)a.height;
        // ⚠ UVは下が0。行は上から数えているので反転する。
        return new Rect(cx * w, (rows - 1 - cy) * h, w, h);
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
        int rows = Mathf.CeilToInt(TotalCells / (float)Cols);
        int w = CellW * Cols, h = CellH * rows;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);

        for (int t = 0; t < Count; t++)
        {
            int ox = (t % Cols) * CellW;
            int oy = (rows - 1 - t / Cols) * CellH;
            for (int y = 0; y < CellH; y++)
                for (int x = 0; x < CellW; x++)
                {
                    int dst = (oy + y) * w + ox + x;
                    // 🚩 重ねる絵（地形ではないもの）は別に描く
                    if (t >= OutlineIndex)
                    {
                        px[dst] = Overlay(t, x, y - Depth);
                        continue;
                    }
                    // 天面は上寄せ（y は下が0）。側面は天面を Depth だけ下にずらしたもの。
                    float a = 0f; bool isTop = false;
                    if (InHex(x, y - Depth)) { a = 1f; isTop = true; }
                    else if (InHex(x, y)) a = 1f;              // 側面（天面に隠れない部分だけ残る）
                    if (a <= 0f) { px[dst] = new Color(0, 0, 0, 0); continue; }

                    Color c = isTop ? TopCol[t] : SideCol[t];   // ※ t < OutlineIndex のときだけここへ来る
                    if (isTop) c = Motif(t, x, y - Depth, c);
                    else c *= 0.92f;                           // 側面はさらに少し落とす
                    // ふちを少し暗くして輪郭を出す
                    float e = EdgeFade(x, isTop ? y - Depth : y);
                    c = new Color(c.r * e, c.g * e, c.b * e, 1f);
                    px[dst] = c;
                }
        }
        // 🖼️ 外部の絵（施設・拠点）と配下の1枚絵を焼き込む
        for (int i = 0; i < SpriteCells.Length; i++)
            BlitSprite(px, w, rows, SpriteBase + i, Resources.Load<Sprite>("Surface/" + SpriteCells[i]));
        for (int i = 0; i < MinionCatalog.Count; i++)
            BlitSprite(px, w, rows, MinionBase + i, MinionSprite.ByIndex(i));
        for (int i = 0; i < UniqueCatalog.Count; i++)
            BlitSprite(px, w, rows, MinionBase + MinionCatalog.Count + i, MinionSprite.ByIndex(UniqueCatalog.GlobalOf(i)));

        tex.SetPixels(px);
        tex.Apply();
        _atlas = tex;
    }

    /// <summary>
    /// スプライトをセルへ**縦横比を保って**貼る。
    /// ⚠ 絵の大きさは種類ごとにバラバラ（36〜60px）なので、そのまま貼ると盤で大小がそろわない。
    ///   セルの内側に収まる最大の倍率で中央に置く。
    /// ⚠ 読めないテクスチャ（Read/Write 無効）は黙って諦める。落とすほどのことではない。
    /// </summary>
    private static void BlitSprite(Color[] px, int atlasW, int rows, int cell, Sprite sp)
    {
        if (sp == null || sp.texture == null || !sp.texture.isReadable) return;
        int ox = (cell % Cols) * CellW;
        int oy = (rows - 1 - cell / Cols) * CellH;
        var r = sp.textureRect;
        int sw = Mathf.RoundToInt(r.width), sh = Mathf.RoundToInt(r.height);
        if (sw <= 0 || sh <= 0) return;
        var src = sp.texture.GetPixels(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y), sw, sh);
        // セルの内側 88% に収める（縁に触れると隣のセルへ滲む）
        float fit = Mathf.Min(CellW * 0.88f / sw, CellH * 0.88f / sh);
        int dw = Mathf.Max(1, Mathf.RoundToInt(sw * fit)), dh = Mathf.Max(1, Mathf.RoundToInt(sh * fit));
        int px0 = ox + (CellW - dw) / 2, py0 = oy + (CellH - dh) / 2;
        for (int y = 0; y < dh; y++)
            for (int x = 0; x < dw; x++)
            {
                int sx = Mathf.Clamp(Mathf.FloorToInt(x / fit), 0, sw - 1);
                int sy = Mathf.Clamp(Mathf.FloorToInt(y / fit), 0, sh - 1);
                var c = src[sy * sw + sx];
                if (c.a <= 0.02f) continue;
                px[(py0 + y) * atlasW + px0 + x] = c;
            }
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
            case LegionIndex:
            {
                // 隊列のブロック（横長の長方形＋下に台座）＝地に足のついた近接部隊
                if (Mathf.Abs(x) <= 0.40f && y <= 0.30f && y >= -0.18f) return Color.white;
                if (Mathf.Abs(x) <= 0.30f && y < -0.18f && y >= -0.34f) return Color.white;
                return clear;
            }
            case LegionRangedIndex:
            {
                // 山形（^）＝後ろから撃つ部隊。近接ブロックと形で見分けが付く
                if (y > 0.34f || y < -0.30f) return clear;
                float t4 = Mathf.InverseLerp(0.34f, -0.30f, y);      // 0=頂点 1=裾
                float half = Mathf.Lerp(0.03f, 0.42f, t4);
                float ax2 = Mathf.Abs(x);
                if (ax2 > half) return clear;
                return (ax2 < half - 0.17f) ? clear : Color.white;   // 中を抜いて線にする
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
