using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 🖱️ 地上ヘクス盤のパン（ドラッグ）とズーム（ホイール）。
///
/// 盤が91〜271タイルになって1画面に収まらなくなったので、Civ と同じく
/// **掴んで動かす／ホイールで寄る** 操作を入れる。ScrollRect は縦横どちらかしか自然に扱えず、
/// ヘクス盤は自由な2軸移動が要るので専用に作る。
///
/// 実装メモ: `IDragHandler` と `IScrollHandler` **だけ**を実装する（EventTrigger は使わない）。
/// タイル側のボタンはドラッグを実装していないので、この親までイベントが上がってくる。
/// 関連: [[UITooltipTrigger]]（同じ理由でEventTriggerを避けている）。
/// </summary>
public class HexMapPanZoom : MonoBehaviour, IDragHandler, IScrollHandler, IBeginDragHandler
{
    public RectTransform content;      // 動かす対象（ヘクスを並べた親）
    public float minZoom = 0.45f, maxZoom = 1.6f;
    public System.Action onChanged;

    private float zoom = 1f;
    private Vector2 pan;
    private bool dragged;

    public float Zoom => zoom;
    public bool DraggedThisPress => dragged;

    public void ResetView() { zoom = 1f; pan = Vector2.zero; Apply(); }

    public void OnBeginDrag(PointerEventData e) { dragged = false; }

    public void OnDrag(PointerEventData e)
    {
        if (content == null) return;
        dragged = true;
        pan += e.delta;
        Apply();
    }

    public void OnScroll(PointerEventData e)
    {
        if (content == null) return;
        float before = zoom;
        zoom = Mathf.Clamp(zoom * (1f + e.scrollDelta.y * 0.12f), minZoom, maxZoom);
        if (!Mathf.Approximately(before, zoom))
        {
            pan *= zoom / before;   // カーソル位置ではなく中心基準（盤全体を見る用途なので十分）
            Apply();
        }
    }

    private void Apply()
    {
        if (content == null) return;
        content.localScale = Vector3.one * zoom;
        content.anchoredPosition = pan;
        if (onChanged != null) onChanged();
    }
}
