using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🗿👀 **トーテムの効き目を盤に描く**（G-4）。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：トーテムは「範囲内の配下を強くする」仕掛けなのに、
///   その範囲が**盤のどこにも描かれていなかった**。置いた本人が
///   「いま誰に効いているのか」を確かめられないので、置き場所が判断にならない。
///   さらに G-3 で**半径が盤の広さで変わる**ようになったので、見せないと余計に分からない。
/// </para>
///
/// <para>
/// ⚠⚠ **これは倍率をひとつも足さない。** 既にある層を**見せるだけ**。
///   「殺戮部屋に隣接ボーナスを足す」案は採らない ―― 配下側の掛け算の層は既に多く、
///   そこに12本目を積むと難易度カーブが崩れる → [[difficulty-curve-orders]]。
///   狙っている快感（自分の作った濃いところを見てニヤリとする）は、**可視化で出る**。
/// </para>
///
/// <para>
/// ⚠ 重ねがけは **2つまで**（`totemBuffMaxStack`）。3つ目は効かないので、
///   **濃さも2段で止める**。ここで3段目を描くと「重ねるほど強い」という嘘になる。
/// ⚠ 盤の上にしか出ないので、UIのCanvasではなくワールド空間のスプライトで描く
///   （`ExcavationPreview` と同じ作り）。
/// ⚠⚠ 盤は**キャッシュしない**（縦の迷宮）。1回掴むと B1F を握り続け、別の階に描いてしまう。
/// </para>
/// </summary>
public class TotemRangeView : MonoBehaviour
{
    private static TotemRangeView instance;
    public static TotemRangeView Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindFirstObjectByType<TotemRangeView>();
                if (instance == null) instance = new GameObject("TotemRangeView").AddComponent<TotemRangeView>();
            }
            return instance;
        }
    }

    private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
    private DungeonGridSystem grid { get { return DungeonGridSystem.Active; } }
    private string lastSig = "";

    /// <summary>いま何マスに効いているか（HUDの帯が読む）。空なら帯を出さない。</summary>
    public string Line { get; private set; }

    /// <summary>
    /// 🗿 トーテムツールのとき：**選んでいる種類**の効き目＋カーソル位置に置いた場合の範囲。
    /// ⚠ 種類で絞るのは、重ねがけが**同じ種類どうしでしか起きない**から（→ `TotemSum`）。
    /// </summary>
    public void ShowForTotem(TotemCatalog.Kind kind, Vector2Int hover, bool hoverValid)
    {
        var g = grid;
        if (g == null) { Clear(); return; }
        var fm = DungeonFeatureManager.Instance;
        if (fm == null) { Clear(); return; }

        int size = g.CurrentPlayableSize;
        int radius = TotemCatalog.EffectiveRadius(TotemCatalog.Get((int)kind).radius, size);
        string sig = "T" + g.FloorIndex + "/" + (int)kind + "/" + radius + "/" + hover.x + "," + hover.y
                   + "/" + (hoverValid ? 1 : 0) + "/" + fm.PlacedCount;
        if (sig == lastSig) return;
        lastSig = sig;

        // 既に置いてある**同じ種類**の位置を集める（重ねがけは同種どうしでしか起きない）
        var all = new List<Vector2Int>(); var kinds = new List<int>();
        fm.CollectTotems(g.FloorIndex, all, kinds);
        var sources = new List<Vector2Int>();
        for (int i = 0; i < all.Count; i++) if (kinds[i] == (int)kind) sources.Add(all[i]);

        var cells = new List<Vector2Int>();
        var cols = new List<Color>();
        var baseCol = ColorOf(kind);
        int already = 0, gained = 0;

        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                if (g.GetTileType(x, y) == DungeonGridSystem.TileType.None) continue;
                int n = 0;
                for (int i = 0; i < sources.Count; i++)
                    if (Manhattan(sources[i], x, y) <= radius) n++;
                bool inHover = hoverValid && Manhattan(hover, x, y) <= radius;
                if (n == 0 && !inHover) continue;

                if (n > 0) already++;
                // 🆕 カーソルのぶんで**新しく届くようになる**マスだけ、色を変えて教える。
                //    ⚠ ここが「置く価値」そのもの。既に届いているところに重ねても2つ目までしか効かない。
                bool isNew = inHover && n == 0;
                if (isNew) gained++;

                cells.Add(new Vector2Int(x, y));
                if (isNew) cols.Add(new Color(1f, 1f, 1f, 0.42f));                       // 新しく届く＝白
                else if (n >= 2) cols.Add(Alpha(baseCol, 0.40f));                        // 上限（重ねがけ2）
                else cols.Add(Alpha(baseCol, 0.20f));                                    // 1つぶん
            }

        Paint(cells, cols);
        Line = hoverValid
            ? "🗿 " + TotemCatalog.Name((int)kind) + "（半径 " + radius + "）― ここに置くと <b>" + gained
              + "</b> マスに新しく届く／既に効いている " + already + " マス"
            : "🗿 " + TotemCatalog.Name((int)kind) + "（半径 " + radius + "）― いま " + already + " マスに効いている";
    }

    /// <summary>
    /// 🛡️ 部隊ツールのとき：**どこが厚いか**だけを出す（種類は問わない）。
    /// ⚠ 隊員はトーテムの中に立たせてこそ効くので、置く前に濃いところが見えている必要がある。
    /// </summary>
    public void ShowCoverage()
    {
        var g = grid;
        var fm = DungeonFeatureManager.Instance;
        if (g == null || fm == null) { Clear(); return; }
        int size = g.CurrentPlayableSize;
        string sig = "C" + g.FloorIndex + "/" + fm.PlacedCount + "/" + size;
        if (sig == lastSig) return;
        lastSig = sig;

        var sources = new List<Vector2Int>(); var kinds = new List<int>();
        fm.CollectTotems(g.FloorIndex, sources, kinds);
        var radii = new List<int>();
        for (int i = 0; i < sources.Count; i++)
            radii.Add(TotemCatalog.EffectiveRadius(TotemCatalog.Get(kinds[i]).radius, size));

        var cells = new List<Vector2Int>();
        var cols = new List<Color>();
        var teal = new Color(0.35f, 0.85f, 0.78f);
        int covered = 0;
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                if (g.GetTileType(x, y) == DungeonGridSystem.TileType.None) continue;
                int n = 0;
                for (int i = 0; i < sources.Count; i++)
                    if (Manhattan(sources[i], x, y) <= radii[i]) n++;
                if (n == 0) continue;
                covered++;
                cells.Add(new Vector2Int(x, y));
                cols.Add(Alpha(teal, n >= 2 ? 0.34f : 0.16f));   // ⚠ 2段まで（上限が2だから）
            }
        Paint(cells, cols);
        Line = covered > 0 ? "🗿 トーテムが効いている範囲（濃い＝2つ重なっている）― " + covered + " マス" : "";
    }

    public void Clear()
    {
        if (string.IsNullOrEmpty(lastSig) && string.IsNullOrEmpty(Line)) return;
        lastSig = ""; Line = "";
        Paint(null, null);
    }

    private static int Manhattan(Vector2Int a, int x, int y) { return Mathf.Abs(a.x - x) + Mathf.Abs(a.y - y); }
    private static Color Alpha(Color c, float a) { c.a = a; return c; }
    private static Color ColorOf(TotemCatalog.Kind k)
    {
        Color c;
        ColorUtility.TryParseHtmlString(TotemCatalog.Get((int)k).colorHex, out c);
        return c;
    }

    private void Paint(List<Vector2Int> cells, List<Color> cols)
    {
        int n = cells != null ? cells.Count : 0;
        while (pool.Count < n)
        {
            var go = new GameObject("Cell");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MarkerArt.Pixel();
            sr.sortingOrder = 38;      // ⚠ 掘削のプレビュー(40)より下。掘っている最中はそちらを優先して見せる
            pool.Add(sr);
        }
        for (int i = 0; i < pool.Count; i++)
        {
            bool on = i < n && grid != null;
            pool[i].gameObject.SetActive(on);
            if (!on) continue;
            pool[i].transform.position = grid.GridToWorld(cells[i].x, cells[i].y) + new Vector3(0f, 0f, -0.65f);
            pool[i].transform.localScale = Vector3.one * 0.94f;
            pool[i].color = cols[i];
        }
    }
}
