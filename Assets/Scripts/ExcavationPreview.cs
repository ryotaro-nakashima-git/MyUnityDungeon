using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⛏️👀 掘削の**先読み表示**（盤の上の色つき＋帯の1行）。
///
/// <para>
/// ⚠⚠ **この見せ方が無いと掘削は成立しない。** クリックする前に
/// 「どこが」「何マス」「道のりがどう変わるか」が見えないなら、掘削はただの線引き作業になる。
/// 見えるから、盤を読んで決める遊びになる。→ [[Excavation]]
/// </para>
///
/// <para>盤の上にしか出ないので、UIのCanvasではなくワールド空間のスプライトで描く。</para>
/// </summary>
public class ExcavationPreview : MonoBehaviour
{
    private static ExcavationPreview instance;
    public static ExcavationPreview Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindFirstObjectByType<ExcavationPreview>();
                if (instance == null) instance = new GameObject("ExcavationPreview").AddComponent<ExcavationPreview>();
            }
            return instance;
        }
    }

    private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
    // ⚠⚠ **キャッシュしない**（縦の迷宮 F-2以降）。1回掴むと B1F の盤を握り続け、
    //   B2F以降で掘削のプレビューが**別の階の座標で描かれる**。→ [[DungeonGridSystem]]
    private DungeonGridSystem grid { get { return DungeonGridSystem.Active; } }
    private string lastSig = "";
    /// <summary>いまカーソルの下で起きること（HUDの帯が読む）。空なら帯を出さない。</summary>
    public string Line { get; private set; }

    /// <summary>
    /// ホバーのたびに呼ぶ。⚠ **同じマスなら何もしない**（毎フレーム作り直すと重いしチラつく）。
    /// </summary>
    public void Show(int toolMode, Vector2Int cell)
    {
        // ⚠ 署名に**階**を混ぜる（同じセルでも階が違えばプレビューを描き直す）
        int fl = grid != null ? grid.FloorIndex : 0;
        string sig = fl + "/" + toolMode + ":" + cell.x + "," + cell.y + ":" + (Excavation.AwaitingDigTarget ? "d" : "-");
        if (sig == lastSig) return;
        lastSig = sig;

        List<Vector2Int> cells;
        Line = Excavation.Preview(toolMode, cell, out cells);
        Paint(cells, Excavation.AwaitingDigTarget || toolMode == 15
            ? new Color(0.45f, 0.72f, 0.95f, 0.55f)     // 掘る＝青
            : new Color(0.95f, 0.45f, 0.45f, 0.55f));   // 塞ぐ＝赤
    }

    public void Clear()
    {
        if (string.IsNullOrEmpty(lastSig) && string.IsNullOrEmpty(Line)) return;
        lastSig = ""; Line = "";
        Paint(null, Color.clear);
    }

    private void Paint(List<Vector2Int> cells, Color col)
    {
        int n = cells != null ? cells.Count : 0;
        while (pool.Count < n)
        {
            var go = new GameObject("Cell");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MarkerArt.Pixel(); sr.sortingOrder = 40;
            pool.Add(sr);
        }
        for (int i = 0; i < pool.Count; i++)
        {
            bool on = i < n && grid != null;
            pool[i].gameObject.SetActive(on);
            if (!on) continue;
            pool[i].transform.position = grid.GridToWorld(cells[i].x, cells[i].y) + new Vector3(0, 0, -0.7f);
            pool[i].transform.localScale = Vector3.one * 0.9f;
            pool[i].color = col;
        }
    }
}
