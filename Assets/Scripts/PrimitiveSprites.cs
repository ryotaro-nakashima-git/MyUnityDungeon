using UnityEngine;

/// <summary>
/// キャラのパーツ用に、白い基本形状スプライト（円・角丸矩形・矩形）を手続き生成してキャッシュする。
/// 色は各SpriteRendererのcolorで着色、サイズはtransform.localScale（ワールド単位）で調整する。
/// PPU=テクスチャ幅なので scale=1 で 1ユニット。
/// </summary>
public static class PrimitiveSprites
{
    private static Sprite _sq, _circle, _round;

    public static Sprite Square()
    {
        if (_sq == null) _sq = Make(8, (x, y, n) => true);
        return _sq;
    }

    public static Sprite Circle()
    {
        if (_circle == null) _circle = Make(64, (x, y, n) =>
        {
            float dx = x - n / 2f + 0.5f, dy = y - n / 2f + 0.5f;
            float r = n / 2f - 0.5f;
            return dx * dx + dy * dy <= r * r;
        });
        return _circle;
    }

    public static Sprite RoundRect()
    {
        if (_round == null) _round = Make(64, (x, y, n) =>
        {
            float r = 16f;
            float cx = Mathf.Clamp(x + 0.5f, r, n - r);
            float cy = Mathf.Clamp(y + 0.5f, r, n - r);
            float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
            return dx * dx + dy * dy <= r * r;
        });
        return _round;
    }

    private static Sprite Make(int n, System.Func<int, int, int, bool> inside)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[n * n];
        var clear = new Color(1, 1, 1, 0);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = inside(x, y, n) ? Color.white : clear;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }
}
