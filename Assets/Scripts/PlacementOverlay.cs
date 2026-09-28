using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🟩 **置ける所を盤に緑で敷く**（UI刷新 B-4）。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：いまの配置は「押してみて、断られて、初めて置けないと分かる」形だった。
///   壁・既に何かある・枠が上限 ―― どれも**押す前から決まっている**のに、盤には何も出ていない。
///   Civ も CDO2 も「置ける場所を先に光らせる」形で、これは演出ではなく**手数の削減**にあたる。
/// </para>
///
/// <para>
/// ⚠⚠ **判定を自前で持たない。**答えは必ず `DungeonFeatureManager.CanPlaceAt` に訊く。
///   ここに写経すると、実際の配置と食い違って「緑なのに置けない」が生まれる
///   ―― それは何も出さないより悪い（→ [[DungeonFeatureManager]]）。
/// ⚠ **「持ち物が足りない」は緑では言わない。**DPが無いだけで盤が真っ赤になっても、
///   なぜ置けないのかは伝わらない。マスの話だけをここが持つ。
/// </para>
///
/// <para>
/// ⚠ 盤の上にしか出ないので、UIのCanvasではなくワールド空間のスプライトで描く
///   （`TotemRangeView` `ExcavationPreview` と同じ作り）。
/// ⚠⚠ 盤は**キャッシュしない**（縦の迷宮）。1回掴むと B1F を握り続け、別の階に描いてしまう。
/// ⚠ 毎フレーム全マスを見ない。**同じ状況なら描き直さない**（`sig`）。
/// </para>
///
/// 関連: [[GridInputHandler]]（いつ出すか） [[TotemRangeView]]（重なる下敷き・こちらが下）。
/// </summary>
public class PlacementOverlay : MonoBehaviour
{
    private static PlacementOverlay instance;
    public static PlacementOverlay Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindFirstObjectByType<PlacementOverlay>();
                if (instance == null) instance = new GameObject("PlacementOverlay").AddComponent<PlacementOverlay>();
            }
            return instance;
        }
    }

    private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
    private DungeonGridSystem grid { get { return DungeonGridSystem.Active; } }
    private string lastSig = "";

    /// <summary>いま何マスに置けるか（0なら出していない）。</summary>
    public int Count { get; private set; }

    // 🎨 置ける＝緑／撤去できる＝赤。
    //   ⚠⚠ **塗り潰しをやめて枠にした**（実測）。置ける所は床のほぼ全部なので、
    //     マスを染めると迷宮が丸ごと緑になり、床の絵が消えた。α を下げると今度は
    //     明るい床の上で見えなくなる ―― 濃さの問題ではなく**形の問題**だった。
    //     枠なら、はっきり見えて、しかも盤の絵は残る。
    private static readonly Color CanPlace = new Color(0.42f, 0.92f, 0.55f, 0.62f);
    private static readonly Color CanErase = new Color(0.94f, 0.40f, 0.40f, 0.62f);
    // 👆 いま指しているマスだけは**塗り潰す**（どこに落ちるかが一目で分かる）
    private static readonly Color HoverOk = new Color(0.42f, 0.92f, 0.56f, 0.42f);
    private static readonly Color HoverNg = new Color(0.90f, 0.33f, 0.33f, 0.40f);

    /// <summary>
    /// 🟩 そのツールで置けるマスを敷く。`hover` は指しているマス（盤の外なら (-1,-1)）。
    /// ⚠ 置けるツールでなければ何も出さない（`IsPlacingTool` が門番）。
    /// </summary>
    public void Show(int toolMode, Vector2Int hover)
    {
        var g = grid;
        var fm = DungeonFeatureManager.Instance;
        if (g == null || fm == null || !DungeonFeatureManager.IsPlacingTool(toolMode)) { Clear(); return; }

        int size = g.CurrentPlayableSize;
        // ⚠ 署名に**枠の使用数と階**を入れる。置いた直後に緑が減らないと嘘になる。
        string sig = "P" + toolMode + "/" + g.FloorIndex + "/" + size + "/" + fm.PlacedCount
                   + "/" + hover.x + "," + hover.y;
        if (sig == lastSig) return;
        lastSig = sig;

        bool erase = toolMode == 10;
        var cells = new List<Vector2Int>();
        var cols = new List<Color>();
        var fills = new List<bool>();   // true＝塗り潰し（指している1マスだけ）
        string why;
        int n = 0;
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                var c = new Vector2Int(x, y);
                if (!fm.CanPlaceAt(toolMode, c, out why)) continue;
                n++;
                cells.Add(c);
                cols.Add(erase ? CanErase : CanPlace);
                fills.Add(false);
                if (c == hover) { cells.Add(c); cols.Add(HoverOk); fills.Add(true); }
            }
        // ⚠ 指している先が**置けない**ときも、そこだけは赤く出す
        //   ―― 「反応が無い」と「置けない」は、遊ぶ側にとって別のこと。
        if (hover.x >= 0 && hover.y >= 0 && hover.x < size && hover.y < size
            && !fm.CanPlaceAt(toolMode, hover, out why))
        { cells.Add(hover); cols.Add(HoverNg); fills.Add(true); }

        Count = n;
        Paint(cells, cols, fills);
    }

    public void Clear()
    {
        if (string.IsNullOrEmpty(lastSig) && Count == 0) return;
        lastSig = ""; Count = 0;
        Paint(null, null, null);
    }

    private void Paint(List<Vector2Int> cells, List<Color> cols, List<bool> fills)
    {
        int n = cells != null ? cells.Count : 0;
        while (pool.Count < n)
        {
            var go = new GameObject("Cell");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            // ⚠ トーテムの範囲(38)より**下**。範囲は「効き目」で、こちらは「置ける場所」。
            //   同時に出るときは効き目のほうを読ませたい。
            sr.sortingOrder = 36;
            pool.Add(sr);
        }
        for (int i = 0; i < pool.Count; i++)
        {
            bool on = i < n && grid != null;
            pool[i].gameObject.SetActive(on);
            if (!on) continue;
            bool fill = fills != null && i < fills.Count && fills[i];
            pool[i].sprite = fill ? MarkerArt.Pixel() : MarkerArt.CellRing();
            pool[i].transform.position = grid.GridToWorld(cells[i].x, cells[i].y) + new Vector3(0f, 0f, -0.6f);
            pool[i].transform.localScale = Vector3.one * 0.94f;
            pool[i].color = cols[i];
        }
    }
}
