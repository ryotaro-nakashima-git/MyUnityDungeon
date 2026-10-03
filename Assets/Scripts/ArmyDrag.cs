using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 🖐️ 配下の画面（H1）：名簿の札を掴んで、配属の盤の枠へ落とす。
///
/// ⚠ 盤（ワールド）へ運ぶ `UIDragPlace` とは別物。こちらは **UI から UI へ**。
///   落とした先は `EventSystem.RaycastAll` で拾い、`ArmySlot` の付いた枠だけを相手にする。
/// ⚠ 掴んでいるあいだは札の写し（半透明）が指に付いてくる。元の札は動かさない
///   （名簿は ScrollRect の中なので、元を動かすとスクロールと喧嘩する）。
/// </summary>
public class ArmyDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int individualId = -1;
    public Sprite art;
    public System.Action<int, ArmySlot> onDrop;   // 落とした先（枠でなければ null）

    private RectTransform ghost;
    private Canvas rootCanvas;

    public void OnBeginDrag(PointerEventData e)
    {
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;
        if (rootCanvas == null) return;
        var go = new GameObject("ArmyDragGhost", typeof(RectTransform));
        ghost = (RectTransform)go.transform;
        ghost.SetParent(rootCanvas.transform, false);
        ghost.SetAsLastSibling();
        ghost.sizeDelta = new Vector2(84, 84);
        var img = go.AddComponent<Image>();
        img.sprite = art; img.preserveAspect = true; img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0.85f);
        Move(e);
    }

    public void OnDrag(PointerEventData e) { Move(e); }

    public void OnEndDrag(PointerEventData e)
    {
        if (ghost != null) Destroy(ghost.gameObject);
        ghost = null;
        ArmySlot hit = null;
        if (EventSystem.current != null)
        {
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(e, results);
            foreach (var r in results)
            {
                hit = r.gameObject.GetComponentInParent<ArmySlot>();
                if (hit != null) break;
            }
        }
        if (onDrop != null) onDrop(individualId, hit);
    }

    private void Move(PointerEventData e)
    {
        if (ghost == null || rootCanvas == null) return;
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rootCanvas.transform, e.position,
            rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera, out local);
        ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
        ghost.anchoredPosition = local;
    }
}

/// <summary>配属の盤の枠1つ（どの階の、ボス枠か隊の枠か）。</summary>
public class ArmySlot : MonoBehaviour
{
    public int floor;
    public bool boss;
}
