using UnityEngine;

/// <summary>
/// 🎨 マップ上の配置マーカーの見た目（手続き生成のスプライト）。
///
/// 方針:
/// - **隊/ボスは『駐留の目印』**なので主張を抑える＝キャラを隠さない四隅のかぎ括弧。ボスだけ小さな王冠を足す。
/// - **トーテム/スポナー/特殊敵/階段は『そこに在る物』**なので、形で一目で分かる図形にする。
/// - すべて 64×64 の Texture2D を実行時に描き、静的にキャッシュする（外部アセット不要・URP設定に影響されない）。
///
/// 座標系: 各描画関数は正規化座標 (x,y ∈ -1..1、中心が原点、上が +y) で判定する。
/// 関連: [[dangeon-3-current-code]] DungeonFeatureManager(マーカー生成) / DungeonFloorManager(階段)。
/// </summary>
public static class MarkerArt
{
    private const int S = 64; // テクスチャ解像度

    private static Sprite _bracket, _crown, _obelisk, _portal, _rhombus, _stairs, _pixel;

    /// <summary>1×1の白（塗り潰し用）。</summary>
    public static Sprite Pixel()
    {
        if (_pixel == null)
        {
            var tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.white); tex.Apply();
            _pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        }
        return _pixel;
    }

    /// <summary>🛡️ 四隅のかぎ括弧＝『ここに駐留している』。中央を空けるのでキャラが隠れない。</summary>
    public static Sprite Bracket()
    {
        if (_bracket == null) _bracket = Build((x, y) =>
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            float outer = Mathf.Max(ax, ay);
            bool ring = outer <= 0.96f && outer >= 0.78f;   // 太さのある正方形の枠
            bool corner = Mathf.Min(ax, ay) >= 0.44f;        // 四隅だけ残す＝かぎ括弧
            return ring && corner;
        });
        return _bracket;
    }

    /// <summary>👑 小さな王冠（ボス用。かぎ括弧の上に重ねる）。</summary>
    public static Sprite Crown()
    {
        if (_crown == null) _crown = Build((x, y) =>
        {
            float ax = Mathf.Abs(x);
            if (ax > 0.62f) return false;
            if (y >= -0.42f && y <= -0.10f) return true;                 // 台座の帯
            if (y > -0.10f && y <= 0.58f)
            {
                // 3つの尖り（中央・左右）。各中心からの距離で高さを決める。
                float t = Mathf.Min(Mathf.Abs(x + 0.42f), Mathf.Min(Mathf.Abs(x), Mathf.Abs(x - 0.42f)));
                return y <= 0.56f - 2.9f * t;
            }
            return false;
        });
        return _crown;
    }

    /// <summary>🗿 石柱（トーテム）。下すぼまりの台形＋頂点の宝珠。種類ごとの色を乗せて使う。</summary>
    public static Sprite Obelisk()
    {
        if (_obelisk == null) _obelisk = Build((x, y) =>
        {
            float ax = Mathf.Abs(x);
            if (y >= -0.80f && y <= -0.62f) return ax <= 0.52f;             // 基礎（土台）
            if (y > -0.62f && y <= 0.42f) return ax <= 0.34f - 0.10f * y;   // 柱（上へ細くなる）
            if (y > 0.42f) return ax + Mathf.Abs(y - 0.62f) <= 0.30f;       // 頂点の宝珠（菱形）
            return false;
        });
        return _obelisk;
    }

    /// <summary>🌀 渦（スポナー）。切れ目のある二重リング＋中心核。</summary>
    public static Sprite Portal()
    {
        if (_portal == null) _portal = Build((x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            if (r <= 0.20f) return true;                                    // 中心核
            float a = Mathf.Atan2(y, x);
            bool gap = Mathf.Abs(Mathf.Sin(a * 1.5f)) > 0.42f;              // 3つの切れ目
            if (r >= 0.44f && r <= 0.58f && gap) return true;               // 内リング
            if (r >= 0.74f && r <= 0.90f && !gap) return true;              // 外リング（切れ目を互い違いに）
            return false;
        });
        return _portal;
    }

    /// <summary>◆ 菱形の輪＋中心（特殊敵）。</summary>
    public static Sprite Rhombus()
    {
        if (_rhombus == null) _rhombus = Build((x, y) =>
        {
            float d = Mathf.Abs(x) + Mathf.Abs(y);
            if (d <= 0.30f) return true;                 // 中心
            return d >= 0.62f && d <= 0.92f;             // 輪
        });
        return _rhombus;
    }

    /// <summary>▼ 下り階段。3段の踏面＋下向き矢印。</summary>
    public static Sprite Stairs()
    {
        if (_stairs == null) _stairs = Build((x, y) =>
        {
            // 左上から右下へ落ちる3段
            if (x >= -0.92f && x <= -0.24f && y >= 0.30f && y <= 0.52f) return true;
            if (x >= -0.30f && x <= 0.26f && y >= -0.06f && y <= 0.16f) return true;
            if (x >= 0.20f && x <= 0.92f && y >= -0.42f && y <= -0.20f) return true;
            // 段の立ち上がり（縦の面）
            if (x >= -0.30f && x <= -0.24f && y >= -0.06f && y <= 0.52f) return true;
            if (x >= 0.20f && x <= 0.26f && y >= -0.42f && y <= 0.16f) return true;
            // 下向き矢印（左下に小さく）
            float ax = Mathf.Abs(x + 0.55f);
            if (y <= -0.44f && y >= -0.92f && ax <= 0.30f + (y + 0.44f) * 0.6f) return true;
            return false;
        });
        return _stairs;
    }

    // ---- 生成ヘルパー ----
    private static Sprite Build(System.Func<float, float, bool> inside)
    {
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[S * S];
        for (int j = 0; j < S; j++)
        {
            for (int i = 0; i < S; i++)
            {
                // 2×2 のスーパーサンプリングでギザギザを抑える
                int hit = 0;
                for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        float x = ((i + 0.25f + sx * 0.5f) / S) * 2f - 1f;
                        float y = ((j + 0.25f + sy * 0.5f) / S) * 2f - 1f;
                        if (inside(x, y)) hit++;
                    }
                byte a = (byte)(hit * 255 / 4);
                px[j * S + i] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }
}
