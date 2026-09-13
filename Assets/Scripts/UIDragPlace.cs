using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 🖐️ **掴んで盤に落とす**（UI刷新 B-4）。配置ストリップの1マスに付ける。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：いままでの配置は「ストリップで選ぶ → 盤をクリックする」の2手だった。
///   選んだことが盤に伝わっている気配が無いので、**何を持っているのか分からないまま**
///   盤を押すことになる。掴んで運べば、持っている物が指の先にあるので迷わない
///   ―― Civ も CDO2 も、置く物は必ず「手にある」形で見せている。
/// </para>
///
/// <para>
/// ⚠ **掴んだ瞬間に、押したときと同じ選択を走らせる**（`onGrab`）。
///   ドラッグは「選ぶ」の代わりではなく**選んでから運ぶ**なので、選択の道は1本のままにする。
/// ⚠ 落とす先の判定は `GridInputHandler.DropAtScreen` に渡す ―― 配置の振り分けは向こうが持つ。
/// ⚠ 影（指に追従する絵）は **`raycastTarget = false`**。true にすると自分自身が
///   ポインタを遮って「UIの上に落ちた」ことになり、**絶対に置けない**。
/// ⚠ 影は専用の Canvas（最前面）に置く。ストリップの中に置くと、帯の外に出た瞬間に切れる。
/// </para>
///
/// 関連: [[GridInputHandler]]（落とし先） [[PlacementOverlay]]（置ける所の緑）。
/// </summary>
public class UIDragPlace : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>
    /// 掴んだ瞬間に走らせる**選択だけ**。
    /// ⚠⚠⚠ **ここで帯を作り直してはいけない。**（実測でここを踏んだ）
    ///   ストリップの `Refresh*Strip` は中身を `SetActive(false)` してから `Destroy` する。
    ///   掴んだ直後にそれを呼ぶと、**いま掴んでいるマス自身が死ぬ** ―― EventSystem は
    ///   `pointerDrag` が非アクティブになった時点でドラッグを捨てるので、
    ///   `OnEndDrag` が二度と来ず、**何をどこに落としても置けない**。
    ///   作り直しは `onDone`（落とし終わったあと）に回すこと。
    /// </summary>
    public System.Action onGrab;
    /// <summary>落とし終わってから走らせる後始末（帯の作り直しなど）。⚠ ここで自分が消えてよい。</summary>
    public System.Action onDone;
    /// <summary>指に付いてくる絵。無ければ影は出さない（それでも落とせる）。</summary>
    public Sprite art;
    /// <summary>影の大きさ（px）。</summary>
    public float ghostSize = 56f;

    private static RectTransform ghostRoot;
    private static Image ghost;
    // ⚠⚠ 帯は**横スクロールする**（`MakeHScroll`）。マスがドラッグを全部食うと、
    //   指で帯を送れなくなる（マウスはホイールがあるが、タッチには無い）。
    //   → **横に払ったらスクロールへ渡す／上下に引いたら掴む**で分ける。
    //     帯は画面の下、盤はその上にあるので「上へ引く＝盤へ運ぶ」は素直な動き。
    private ScrollRect scroller;
    private bool forwarding;

    public void OnBeginDrag(PointerEventData e)
    {
        if (scroller == null) scroller = GetComponentInParent<ScrollRect>();
        forwarding = scroller != null && Mathf.Abs(e.delta.x) > Mathf.Abs(e.delta.y);
        if (forwarding)
        {
            ExecuteEvents.Execute(scroller.gameObject, e, ExecuteEvents.beginDragHandler);
            return;
        }
        if (onGrab != null) onGrab();
        SoundSystem.Play(SoundSystem.Sfx.Click);
        EnsureGhost();
        if (ghost == null) return;
        ghost.sprite = art;
        ghost.enabled = art != null;
        ghost.rectTransform.sizeDelta = new Vector2(ghostSize, ghostSize);
        ghostRoot.gameObject.SetActive(true);
        Move(e.position);
    }

    public void OnDrag(PointerEventData e)
    {
        if (forwarding)
        {
            if (scroller != null) ExecuteEvents.Execute(scroller.gameObject, e, ExecuteEvents.dragHandler);
            return;
        }
        Move(e.position);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (forwarding)
        {
            forwarding = false;
            if (scroller != null) ExecuteEvents.Execute(scroller.gameObject, e, ExecuteEvents.endDragHandler);
            return;
        }
        if (ghostRoot != null) ghostRoot.gameObject.SetActive(false);
        // ⚠ **UIの上で離したら置かない**。帯の中で手が滑っただけで盤に置かれると、
        //   取り消せない出費（DP）になる。`pointerCurrentRaycast` は影を無視して拾える
        //   （影は raycastTarget = false なので、そもそも当たらない）。
        if (e.pointerCurrentRaycast.gameObject == null)
        {
            var gi = Object.FindFirstObjectByType<GridInputHandler>();
            if (gi != null) gi.DropAtScreen(e.position);
        }
        // 🔁 帯の作り直しは**ここ**。⚠ この中で自分が消えるが、`Destroy` はフレーム末なので問題ない。
        if (onDone != null) onDone();
    }

    private static void Move(Vector2 screenPos)
    {
        if (ghost == null) return;
        // 影の Canvas は ScreenSpaceOverlay なので、画面座標をそのまま位置に使える
        ghost.rectTransform.position = screenPos;
    }

    private static void EnsureGhost()
    {
        if (ghost != null) return;
        var go = new GameObject("DragGhostCanvas", typeof(RectTransform), typeof(Canvas));
        var cv = go.GetComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 900;   // ⚠ ツールチップ(200)より上、設定(320)より下でよい
        ghostRoot = (RectTransform)go.transform;
        Object.DontDestroyOnLoad(go);

        var g = new GameObject("Ghost", typeof(RectTransform)).AddComponent<Image>();
        g.rectTransform.SetParent(ghostRoot, false);
        g.raycastTarget = false;                       // ⚠⚠ ここを true にすると絶対に置けない
        g.preserveAspect = true;
        g.color = new Color(1f, 1f, 1f, 0.85f);
        ghost = g;
        ghostRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 1マスに掴む機能を付ける。⚠ 既に付いていたら上書きする（作り直しのたびに増やさない）。
    /// ⚠⚠ `onGrab` は**選ぶだけ**・`onDone` が**帯の作り直し**。混ぜると掴んだ瞬間に自分が死ぬ。
    /// </summary>
    public static void Attach(GameObject cell, Sprite art, System.Action onGrab,
                              System.Action onDone = null, float ghostSize = 56f)
    {
        if (cell == null) return;
        var d = cell.GetComponent<UIDragPlace>();
        if (d == null) d = cell.AddComponent<UIDragPlace>();
        d.art = art; d.onGrab = onGrab; d.onDone = onDone; d.ghostSize = ghostSize;
    }
}
