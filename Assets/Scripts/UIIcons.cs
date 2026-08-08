using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🖼️ UIのアイコンを**手続き生成**する（Phase B-8）。
///
/// **なぜ手続き生成か**：
/// - 資源や状態のアイコンが無く、HUDが「文字だけ」で素人っぽかった。素材を待たずに今すぐ埋められる。
/// - ⚠ 記号（◆ ＋ ×…）で代用すると、**UIフォントに無い字が □ になる**（このプロジェクトで何度も踏んだ）。
///   絵にしてしまえばフォントに依存しないので**根治**する。
///
/// 白で描いて、使う側が `Image.color` で着色する（[[UITheme]] の意味の色をそのまま乗せる）。
/// 64px・アルファ付き。1回作ってキャッシュする。
/// </summary>
public static class UIIcons
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private const int N = 64;

    public static Sprite Get(string id)
    {
        Sprite s;
        if (cache.TryGetValue(id, out s)) return s;
        s = Build(id);
        cache[id] = s;
        return s;
    }

    private static Sprite Build(string id)
    {
        switch (id)
        {
            case "dp":       return Make(Coin);
            case "material": return Make(Ingot);
            case "research": return Make(Book);
            case "emotion":  return Make(Heart);
            case "fame":     return Make(Flag);
            case "threat":   return Make(Warn);
            case "slot":     return Make(Grid);
            case "world":    return Make(Star);
            case "food":     return Make(Leaf);
            case "influence":return Make(Ring);
            case "pop":      return Make(Person);
            case "move":     return Make(Boot);
            default:         return Make(Dot);
        }
    }

    // ============ 形（0..1 の座標で「そこが塗られるか」を返す） ============
    private static bool Dot(float x, float y) { return Len(x - .5f, y - .5f) < .42f; }

    // 💰 コイン：外の円＋内側のリング
    private static bool Coin(float x, float y)
    {
        float d = Len(x - .5f, y - .5f);
        if (d > .46f) return false;
        return d < .40f || d > .43f ? d <= .46f : false;
    }

    // 🪨 素材：インゴット（台形）
    private static bool Ingot(float x, float y)
    {
        if (y < .30f || y > .70f) return false;
        float t = (y - .30f) / .40f;                 // 下ほど広い
        float half = Mathf.Lerp(.40f, .26f, t);
        return Mathf.Abs(x - .5f) < half;
    }

    // 📖 研究：本（背表紙＋ページ）
    private static bool Book(float x, float y)
    {
        if (x < .18f || x > .82f || y < .18f || y > .82f) return false;
        if (Mathf.Abs(x - .5f) < .035f) return true;              // 綴じ目
        return Mathf.Abs(x - .5f) > .06f;                          // ページ
    }

    // ❤️ 感情：ハート
    private static bool Heart(float x, float y)
    {
        float px = (x - .5f) * 2.2f, py = (y - .42f) * 2.2f;
        py = -py;
        float a = px * px + py * py - .30f;
        return a * a * a - px * px * py * py * py < 0f;
    }

    // 🚩 名声：旗
    private static bool Flag(float x, float y)
    {
        if (x > .24f && x < .32f && y > .14f && y < .86f) return true;       // 竿
        if (x < .32f || x > .82f || y < .48f || y > .84f) return false;      // 旗
        float t = (x - .32f) / .50f;
        return Mathf.Abs(y - .66f) < Mathf.Lerp(.18f, .06f, t);
    }

    // ⚠ 脅威度：三角の警告
    private static bool Warn(float x, float y)
    {
        if (y < .20f || y > .84f) return false;
        float t = (y - .20f) / .64f;
        float half = Mathf.Lerp(.44f, .02f, t);
        if (Mathf.Abs(x - .5f) > half) return false;
        if (Mathf.Abs(x - .5f) < .055f && y > .34f && y < .62f) return false; // ！の縦
        if (Mathf.Abs(x - .5f) < .055f && y > .24f && y < .30f) return false; // ！の点
        return true;
    }

    // ▦ 配置枠：4分割のグリッド
    private static bool Grid(float x, float y)
    {
        if (x < .16f || x > .84f || y < .16f || y > .84f) return false;
        bool gapX = Mathf.Abs(x - .5f) < .045f;
        bool gapY = Mathf.Abs(y - .5f) < .045f;
        return !(gapX || gapY);
    }

    // ★ 世界水準：星
    private static bool Star(float x, float y)
    {
        float px = x - .5f, py = y - .5f;
        float ang = Mathf.Atan2(py, px);
        float r = Len(px, py);
        float k = .30f + .16f * Mathf.Cos(5f * (ang - Mathf.PI * .5f));
        return r < k;
    }

    // 🌿 食料：葉
    private static bool Leaf(float x, float y)
    {
        float px = (x - .5f), py = (y - .5f);
        float rx = (px + py) * .7071f, ry = (py - px) * .7071f;
        if (Mathf.Abs(ry) < .02f && Mathf.Abs(rx) < .34f) return true;      // 葉脈
        return (rx * rx) / (.34f * .34f) + (ry * ry) / (.17f * .17f) < 1f;
    }

    // ◎ 威名：二重丸
    private static bool Ring(float x, float y)
    {
        float d = Len(x - .5f, y - .5f);
        return (d < .46f && d > .36f) || d < .18f;
    }

    // 👤 人口：人
    private static bool Person(float x, float y)
    {
        if (Len(x - .5f, y - .72f) < .16f) return true;                      // 頭
        if (y > .52f) return false;
        float t = (y - .16f) / .36f;
        return Mathf.Abs(x - .5f) < Mathf.Lerp(.30f, .18f, t) && y > .16f;   // 体
    }

    // 👣 移動力：足あと（丸2つ）
    private static bool Boot(float x, float y)
    {
        return Len(x - .38f, y - .62f) < .17f || Len(x - .62f, y - .36f) < .17f;
    }

    private static float Len(float a, float b) { return Mathf.Sqrt(a * a + b * b); }

    // ============ 生成（4xでサンプリングして縁を滑らかに） ============
    private static Sprite Make(System.Func<float, float, bool> shape)
    {
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[N * N];
        const int SS = 3;   // スーパーサンプリング（ギザギザを消す）
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float fx = (x + (sx + .5f) / SS) / N;
                        float fy = (y + (sy + .5f) / SS) / N;
                        if (shape(fx, fy)) hit++;
                    }
                float a = hit / (float)(SS * SS);
                px[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f), N);
    }
}
