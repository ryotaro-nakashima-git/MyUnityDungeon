using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 💬 ホバーで説明を出すだけの軽量コンポーネント。
///
/// **なぜ EventTrigger を使わないのか（重要）**
/// `UnityEngine.EventSystems.EventTrigger` は IPointerEnter/Exit だけでなく
/// **IScrollHandler / IBeginDragHandler / IDragHandler / IEndDragHandler も実装している**。
/// uGUIのイベントは「その interface を実装した最初の祖先」で止まるため、
/// ツールチップを付けたカードの上ではホイールもドラッグも EventTrigger に吸われ、
/// 親の ScrollRect に届かなくなる＝**その要素の上ではスクロールできない**。
/// （図鑑の個体タブ・遺物・研究などが「選択できる所にマウスがあるとスライドできない」状態だった原因）
///
/// そこで **IPointerEnterHandler / IPointerExitHandler だけ**を実装する。
/// スクロールもドラッグも実装していないので、そのまま親の ScrollRect へバブリングする。
/// 関連: [[dangeon-3-current-code]] GameUIManager.AddTooltip。
/// </summary>
public class UITooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string tip;
    public System.Action<string> onShow;
    /// <summary>💬 触れた物の位置も渡す版（段A：説明をアイコンのすぐ下に出す）。あればこちらを優先する。</summary>
    public System.Action<string, RectTransform> onShowAt;
    public System.Action onHide;

    public void OnPointerEnter(PointerEventData e)
    {
        if (onShowAt != null) onShowAt(tip, transform as RectTransform);
        else if (onShow != null) onShow(tip);
    }
    public void OnPointerExit(PointerEventData e) { if (onHide != null) onHide(); }
    private void OnDisable() { if (onHide != null) onHide(); }
}
