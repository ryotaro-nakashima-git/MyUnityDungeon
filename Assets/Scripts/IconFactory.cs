using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🎨 <b>ボタンのアイコンを手続きで描く</b>（UI刷新 B-1）。外部素材を使わない。
///
/// <para>
/// ⚠⚠ <b>なぜ要るか</b>：迷宮の画面には<b>常時24個の文字ボタン</b>が並んでいて（上12・下12）、
///   盤より先にバーを読むことになっていた。畳む（B-2）前に、まず<b>絵で分かる</b>状態を作る
///   ―― 絵が弱いまま畳むと<b>ただ探しにくくなるだけ</b>だから（→ [[k6-and-ui-plan]]）。
/// </para>
///
/// <para>
/// <b>描き方の決まり</b>（承認済みの画面案どおり）：
/// <list type="bullet">
/// <item><b>線だけで描く・塗らない</b>。24格子で考え、線の太さは 1.6（格子の単位）。</item>
/// <item><b>色はここでは付けない。</b>白で描いて、`Image.color` で色を乗せる
///   ―― 普段は薄灰、選ぶと金の<b>2状態だけ</b>。⚠ アイコンごとに色を変えると、
///   12個が別々に光って<b>いまの文字バーと同じ「うるさい」</b>に戻る。</item>
/// <item><b>迷ったら「そのものの形」を描く</b>。抽象記号にしない。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠ 絵だけで完全に伝わる必要はない。<b>絵は「思い出す手がかり」、hover が「説明」</b>という分担。
///   説明は `IconCatalog` が持ち、`AddTooltip` が出す。
/// ⚠ 生成したスプライトは<b>使い回す</b>（`cache`）。ボタンごとに描くと、
///   画面を組み直すたびにテクスチャが増え続ける。
/// </para>
///
/// 関連: [[PrimitiveSprites]]（同じ手続き生成の先例） [[UIKit]] [[IconCatalog]]。
/// </summary>
public static class IconFactory
{
    /// <summary>描く解像度。24格子 × 4 ＝ 96px。⚠ 小さすぎると線が潰れ、大きすぎると重い。</summary>
    private const int N = 96;
    private const float Scale = N / 24f;
    /// <summary>線の太さ（24格子の単位）。承認済みの決まり。</summary>
    private const float Stroke = 1.6f;

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // ── 描くもの（線分と円だけ。角の丸みは距離場が勝手にやってくれる）──
    private struct Seg { public Vector2 a, b; }
    private struct Ring { public Vector2 c; public float r; }

    private static List<Seg> segs;
    private static List<Ring> rings;

    /// <summary>名前からアイコンを引く。⚠ 知らない名前なら null（呼び側は文字に落ちる）。</summary>
    public static Sprite Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        Sprite sp;
        if (cache.TryGetValue(name, out sp)) return sp;
        segs = new List<Seg>(); rings = new List<Ring>();
        if (!Draw(name)) { cache[name] = null; return null; }
        sp = Rasterize();
        cache[name] = sp;
        return sp;
    }

    // ============ 小さな描画命令（24格子・y は下向き） ============
    private static void L(float x0, float y0, float x1, float y1)
        => segs.Add(new Seg { a = new Vector2(x0, y0), b = new Vector2(x1, y1) });

    /// <summary>折れ線。⚠ 点は x,y の順に並べる。</summary>
    private static void P(params float[] v)
    { for (int i = 0; i + 3 < v.Length; i += 2) L(v[i], v[i + 1], v[i + 2], v[i + 3]); }

    /// <summary>閉じた折れ線。</summary>
    private static void Poly(params float[] v)
    { P(v); if (v.Length >= 4) L(v[v.Length - 2], v[v.Length - 1], v[0], v[1]); }

    private static void C(float cx, float cy, float r)
        => rings.Add(new Ring { c = new Vector2(cx, cy), r = r });

    /// <summary>点（小さな円を線の太さで潰して塗る）。</summary>
    private static void Dot(float cx, float cy) => C(cx, cy, 0.35f);

    private static void Box(float x, float y, float w, float h)
        => Poly(x, y, x + w, y, x + w, y + h, x, y + h);

    // ============ 24個の形 ============
    private static bool Draw(string n)
    {
        switch (n)
        {
            // ── 常時見えている2つ ──
            case "戦略":   // 城の胸壁＝内政の入口
                P(4, 19, 4, 7, 8, 4, 12, 7, 16, 4, 20, 7, 20, 19);
                L(8, 19, 8, 13); L(16, 19, 16, 13); C(12, 11.4f, 1.5f); return true;
            case "配置":   // 区画＋＋
                Box(3.5f, 3.5f, 6.5f, 6.5f); Box(14, 3.5f, 6.5f, 6.5f); Box(3.5f, 14, 6.5f, 6.5f);
                L(17.2f, 14.4f, 17.2f, 20.4f); L(14.2f, 17.4f, 20.2f, 17.4f); return true;

            // ── 戦略の中身 ──
            case "魔王":   // 角の生えた冠
                P(5, 9, 3, 4, 7, 6.5f, 12, 3, 17, 6.5f, 21, 4, 19, 9);
                Poly(5, 9, 19, 9, 17.2f, 19, 6.8f, 19); Dot(9.5f, 13); Dot(14.5f, 13); return true;
            case "感情":   // 裂けた心臓
                P(12, 20, 4.6f, 14.4f, 3.5f, 9.6f, 6.4f, 6.2f, 12, 7.4f, 17.6f, 6.2f, 20.5f, 9.6f, 19.4f, 14.4f, 12, 20);
                P(12, 7.4f, 10.2f, 12.4f, 13.6f, 12.4f, 12, 16.6f); return true;
            case "遺物":   // 壺
                L(9, 3, 15, 3);
                P(10, 3, 10, 5.5f, 7, 12, 7, 16, 11, 20, 13, 20, 17, 16, 17, 12, 14, 5.5f, 14, 3);
                L(7.4f, 11, 16.6f, 11); return true;
            case "研究":   // フラスコ
                P(10, 3, 10, 9.2f, 5.6f, 16.8f, 7.6f, 21, 16.4f, 21, 18.4f, 16.8f, 14, 9.2f, 14, 3);
                L(9, 3, 15, 3); L(7.6f, 14.5f, 16.4f, 14.5f); return true;
            case "拡張":   // 層＋上下の矢
                L(4, 6, 20, 6); L(4, 12, 20, 12); L(4, 18, 20, 18);
                P(9.8f, 5.2f, 12, 3, 14.2f, 5.2f); P(9.8f, 18.8f, 12, 21, 14.2f, 18.8f); return true;
            case "先触れ": // 見開いた目
                P(2.5f, 12, 6, 6.6f, 12, 5.5f, 18, 6.6f, 21.5f, 12);
                P(2.5f, 12, 6, 17.4f, 12, 18.5f, 18, 17.4f, 21.5f, 12);
                C(12, 12, 2.6f); return true;
            case "因縁":   // 交差した刃と、その結び目
                // ⚠ 鍔まで描くと 96px では潰れて**片方の刃しか見えない**（最初にそう描いた）。
                //   交差そのものを主役にして、真ん中に結び目を置く。
                L(4, 4, 20, 20); L(20, 4, 4, 20); C(12, 12, 2.3f); return true;
            case "報告":   // 吹き出し＝腹心が話しかけてくる
                // ⚠ 『記録』と同じ絵にしてはいけない（最初にそうして、同じボタンが2つ並んだ）。
                //   報告は**人が話す**、記録は**書き留めた物**。形で役割を分ける。
                P(4, 16.4f, 3.2f, 12.4f, 4.4f, 8.2f, 8.4f, 5.6f, 12, 5.2f, 15.6f, 5.6f, 19.6f, 8.2f,
                  20.8f, 12.4f, 19.4f, 16f, 15.6f, 18.2f, 12, 18.6f, 9.6f, 18.4f, 6.4f, 21, 7, 17.6f, 4, 16.4f);
                Dot(9, 12); Dot(12, 12); Dot(15, 12); return true;
            case "記録":   // 栞のある書
                P(7, 4, 17, 4, 19, 6, 19, 20, 16, 18, 13, 20, 10, 18, 7, 20, 7, 4);
                L(9.5f, 8.5f, 14.5f, 8.5f); L(9.5f, 12, 14.5f, 12); return true;
            case "保存":   // 記録板
                Poly(4.5f, 4.5f, 15.5f, 4.5f, 20, 9, 20, 19.5f, 4.5f, 19.5f);
                P(8, 4.5f, 8, 9.5f, 14, 9.5f, 14, 4.5f); Box(8, 14, 8, 5.5f); return true;
            case "設定":   // 歯車
                // ⚠ 円を細く・棘を長くすると**太陽**に見える（最初にそう描いて誤読した）。
                //   歯車は「太い輪に短い歯」。輪を大きく、歯は輪のすぐ外だけ。
                C(12, 12, 4.4f);
                L(12, 4.6f, 12, 6.6f); L(12, 17.4f, 12, 19.4f); L(4.6f, 12, 6.6f, 12); L(17.4f, 12, 19.4f, 12);
                L(6.6f, 6.6f, 8f, 8f); L(16f, 16f, 17.4f, 17.4f);
                L(17.4f, 6.6f, 16f, 8f); L(8f, 16f, 6.6f, 17.4f); return true;

            // ── 配置の中身 ──
            case "トーテム":
                Box(7.5f, 3, 9, 16); L(5, 21, 19, 21);
                Dot(9.5f, 7); Dot(14.5f, 7); L(9, 11, 15, 11); L(9.5f, 15, 14.5f, 15); return true;
            case "罠":     // 噛み合う牙
                L(3.5f, 7.5f, 20.5f, 7.5f); P(6, 7.5f, 8, 11.5f, 10, 7.5f, 12, 11.5f, 14, 7.5f, 16, 11.5f, 18, 7.5f);
                L(3.5f, 20.5f, 20.5f, 20.5f); P(6, 20.5f, 8, 16.5f, 10, 20.5f, 12, 16.5f, 14, 20.5f, 16, 16.5f, 18, 20.5f); return true;
            case "巣":     // 卵の入った巣
                P(3, 13.5f, 5.6f, 18.4f, 12, 20, 18.4f, 18.4f, 21, 13.5f); L(3, 13.5f, 21, 13.5f);
                C(8.6f, 11.2f, 2.4f); C(15.4f, 11.2f, 2.4f); C(12, 8.8f, 2.4f); return true;
            case "環境":   // 葉
                P(12, 21, 12.6f, 14.4f, 15.5f, 8.5f, 20, 5, 20.4f, 9.6f, 18, 15, 12, 18);
                P(12, 21, 11, 16.6f, 8.5f, 12.5f); return true;
            case "巨大":   // 神殿
                L(3, 20, 21, 20); P(5, 20, 5, 9, 12, 4, 19, 9, 19, 20);
                P(9.5f, 20, 9.5f, 15, 14.5f, 15, 14.5f, 20); L(9.5f, 11, 14.5f, 11); return true;
            case "ボス":   // 王冠
                Poly(4, 17, 3, 6, 7.6f, 9.4f, 12, 4, 16.4f, 9.4f, 21, 6, 20, 17); L(4, 20, 20, 20); return true;
            case "特殊敵": // 目のある星
                Poly(12, 3, 14.5f, 8.6f, 20.6f, 9.3f, 16.1f, 13.4f, 17.3f, 19.4f, 12, 16.4f, 6.7f, 19.4f, 7.9f, 13.4f, 3.4f, 9.3f, 9.5f, 8.6f);
                C(12, 12, 1.5f); return true;
            case "宝箱":   // 錠前つきの箱
                P(3.5f, 19, 3.5f, 10.5f, 6, 6.6f, 10, 6, 14, 6, 18, 6.6f, 20.5f, 10.5f, 20.5f, 19, 3.5f, 19);
                L(3.5f, 12.5f, 20.5f, 12.5f); L(12, 11, 12, 14.5f); return true;
            case "部隊":   // 三人
                C(12, 7, 2.6f); P(7.5f, 19, 8.6f, 15.4f, 12, 14.4f, 15.4f, 15.4f, 16.5f, 19);
                C(4.8f, 10.5f, 2f); P(1.6f, 19, 2.4f, 16.4f, 5.2f, 15.6f);
                C(19.2f, 10.5f, 2f); P(22.4f, 19, 21.6f, 16.4f, 18.8f, 15.6f); return true;
            case "塞ぐ":   // 石積み
                Box(3.5f, 5.5f, 17, 13);
                L(3.5f, 10, 20.5f, 10); L(3.5f, 14.5f, 20.5f, 14.5f);
                L(9, 5.5f, 9, 10); L(15, 10, 15, 14.5f); L(9, 14.5f, 9, 18.5f); return true;
            case "掘る":   // つるはし
                // ⚠ 弧を2本にすると **V字** に見える（最初にそう描いた）。
                //   頭は弧1本、柄はまっすぐ縦。これで「振り下ろす道具」に読める。
                P(3.2f, 9.6f, 7.5f, 6.2f, 12, 5.4f, 16.5f, 6.2f, 20.8f, 9.6f);
                L(12, 5.6f, 12, 21); return true;
            case "消去":
                L(6, 6, 18, 18); L(18, 6, 6, 18); return true;
            case "等級":   // 宝石の面
                Poly(12, 3, 20, 9, 12, 21, 4, 9); L(4, 9, 20, 9);
                P(12, 3, 8.5f, 9, 12, 21); P(12, 3, 15.5f, 9, 12, 21); return true;
            case "布陣":
                L(12, 3, 12, 21); C(6, 8, 1.9f); C(18, 8, 1.9f); C(6, 16, 1.9f); C(18, 16, 1.9f); return true;
            case "魔物":   // 骸骨（いちばん触る場所なので、いちばん分かりやすい形にする）
                P(4.4f, 11.4f, 5.2f, 6.4f, 9.4f, 2.8f, 14.6f, 2.8f, 18.8f, 6.4f, 19.6f, 11.4f,
                  17.4f, 15.2f, 17.4f, 19, 15.4f, 21, 8.6f, 21, 6.6f, 19, 6.6f, 15.2f, 4.4f, 11.4f);
                C(9.2f, 10.4f, 1.7f); C(14.8f, 10.4f, 1.7f);
                L(10.6f, 14.6f, 13.4f, 14.6f); L(9.4f, 16.4f, 9.4f, 21); L(12, 16.4f, 12, 21); L(14.6f, 16.4f, 14.6f, 21); return true;

            // ── 資源チップ ──
            case "DP":
                C(12, 12, 8.5f); L(12, 6.8f, 12, 17.2f);
                P(9.4f, 9.4f, 13.6f, 9.4f, 15.4f, 10.6f, 15.4f, 13.4f, 13.6f, 14.6f, 9.4f, 14.6f); return true;
            case "素材":
                Poly(12, 3, 20, 7.5f, 20, 16.5f, 12, 21, 4, 16.5f, 4, 7.5f);
                P(4, 7.5f, 12, 12, 20, 7.5f); L(12, 12, 12, 21); return true;
            case "研究点":
                C(11, 11, 6.5f); L(15.6f, 15.6f, 20.6f, 20.6f);
                L(11, 7.8f, 11, 14.2f); L(7.8f, 11, 14.2f, 11); return true;
            case "名声":   // 炎（噂が燃え広がる）
                // ⚠ 茎と台座を足すと **木** に見える（最初にそう描いた）。炎は外形と芯だけ。
                P(12, 2.6f, 16.2f, 8, 17.4f, 12.4f, 16, 17, 12, 20.4f, 8, 17, 6.6f, 12.4f,
                  7.8f, 8, 12, 2.6f);
                P(12, 10.4f, 14, 13.6f, 12, 17.2f, 10, 13.6f, 12, 10.4f); return true;
            case "生産":   // 稲妻
                Poly(14, 3, 5, 14, 11, 14, 10, 21, 19, 10, 13, 10); return true;
            case "枠":
                Box(3.5f, 3.5f, 6.5f, 6.5f); Box(14, 3.5f, 6.5f, 6.5f);
                Box(3.5f, 14, 6.5f, 6.5f); Box(14, 14, 6.5f, 6.5f); return true;
            case "脅威":
                Poly(12, 3, 21, 19.5f, 3, 19.5f); L(12, 9.5f, 12, 14.4f); Dot(12, 17); return true;
        }
        return false;
    }

    // ============ 距離場でラスタライズ（角の丸みは勝手に付く） ============
    private static Sprite Rasterize()
    {
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[N * N];
        float half = Stroke * Scale * 0.5f;

        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                // ⚠ y を反転する。定義は24格子の**下向き**（SVGと同じ）だが、
                //   テクスチャは上向き。ここを忘れると全部の絵が上下逆になる。
                var p = new Vector2(x + 0.5f, N - (y + 0.5f));
                float d = float.MaxValue;
                for (int i = 0; i < segs.Count; i++)
                {
                    var s = segs[i];
                    d = Mathf.Min(d, DistToSeg(p, s.a * Scale, s.b * Scale));
                }
                for (int i = 0; i < rings.Count; i++)
                {
                    var r = rings[i];
                    d = Mathf.Min(d, Mathf.Abs(Vector2.Distance(p, r.c * Scale) - r.r * Scale));
                }
                // ふちを1pxぼかす（拡大しても線がガタつかない）
                float a = Mathf.Clamp01((half - d) / 1.4f + 0.5f);
                px[y * N + x] = new Color(1f, 1f, 1f, a);
            }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }

    private static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-5f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }
}
