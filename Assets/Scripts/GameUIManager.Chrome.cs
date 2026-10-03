using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 🪟 窓の手触り（段A）。窓ごとに書き足さず、**ここ1か所で全部の窓の面倒を見る**。
/// <list type="bullet">
/// <item>閉じる手段を3つ：右上の ×（無い窓には足す）・Esc・窓の外を押す</item>
/// <item>窓の後ろに幕：全画面の窓は盤を隠す（0.9）、小さい窓は薄く（0.35）</item>
/// <item>開くときは 0.18秒で浮き上がり、閉じるときは 0.12秒で消える</item>
/// <item>クリック音：押した瞬間に「カチ」。押せないボタンは「ブッ」、閉じる系は取消の音</item>
/// <item>説明（ツールチップ）を、触れたアイコンのすぐ下に矢印つきで出す</item>
/// </list>
/// ⚠ 窓は**自分で開いたことを知らせない**（`SetActive(true)` が50か所以上に散っている）。
///   だから毎フレーム「前は閉じていて、今は開いている」を見て、開いた瞬間を拾う。
/// <para>`GameUIManager` の partial。</para>
/// </summary>
public partial class GameUIManager
{
    private const float PopIn = 0.18f, PopOut = 0.12f;
    private const float FullscreenMinW = 1400f;   // これより広い窓は「全画面」として扱う（幕を濃く）

    /// <summary>幕と Esc と × の面倒を見る窓。⚠ 決算・異変（答えるまで閉じない）は入れない。</summary>
    private GameObject[] ChromePanels()
    {
        return new GameObject[]
        { settingsPanel, savePanel, guidePanel, omenPanel, prisonPanel, logPanel, minionPanel, researchPanel,
          demonPanel, emotionPanel, relicPanel, expandPanel, surfaceTreePanel, expeditionPanel,
          ritualPanel, shopPanel, chestGradePanel };
    }

    private readonly List<GameObject> chromeOpenOrder = new List<GameObject>();   // 開いた順（最後＝手前）
    private readonly Dictionary<GameObject, float> chromeClosing = new Dictionary<GameObject, float>();
    private readonly Dictionary<RectTransform, float> chromePopping = new Dictionary<RectTransform, float>();
    private Image chromeBackdrop;
    private readonly List<RaycastResult> chromeHits = new List<RaycastResult>();

    /// <summary>毎フレーム（`Update` から）。</summary>
    private void TickChrome()
    {
        float dt = Mathf.Max(Time.unscaledDeltaTime, 1f / 120f);
        var panels = ChromePanels();

        // ① 開いた瞬間を拾う／閉じたものを外す
        foreach (var p in panels)
        {
            if (p == null) continue;
            bool on = p.activeInHierarchy;
            bool was = chromeOpenOrder.Contains(p);
            if (on && !was) { chromeOpenOrder.Add(p); EnsureCloseButton(p); EnsurePattern(p); StartPop(p); }
            else if (!on && was) { chromeOpenOrder.Remove(p); chromeClosing.Remove(p); ResetLook(p); }
        }

        // ② 開くときの動き（浮き上がり）
        if (chromePopping.Count > 0)
        {
            var keys = new List<RectTransform>(chromePopping.Keys);
            foreach (var rt in keys)
            {
                if (rt == null) { chromePopping.Remove(rt); continue; }
                float t = chromePopping[rt] + dt;
                float k = Mathf.Clamp01(t / PopIn), e = 1f - (1f - k) * (1f - k) * (1f - k);
                rt.localScale = Vector3.one * Mathf.Lerp(0.965f, 1f, e);
                if (k >= 1f) { rt.localScale = Vector3.one; chromePopping.Remove(rt); }
                else chromePopping[rt] = t;
            }
        }

        // ③ 閉じるときの動き（消える）→ 終わったら本当に閉じる
        if (chromeClosing.Count > 0)
        {
            var keys = new List<GameObject>(chromeClosing.Keys);
            foreach (var p in keys)
            {
                if (p == null) { chromeClosing.Remove(p); continue; }
                float t = chromeClosing[p] + dt;
                float k = Mathf.Clamp01(t / PopOut);
                var cg = p.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f - k;
                p.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.98f, k);
                if (k >= 1f) { chromeClosing.Remove(p); CloseNow(p); ResetLook(p); }
                else chromeClosing[p] = t;
            }
        }

        // ④ 幕：いちばん手前の窓のすぐ後ろに敷く
        TickBackdrop();

        // ⑤ クリック音
        TickClickSound();
    }

    private GameObject TopChromePanel()
    {
        for (int i = chromeOpenOrder.Count - 1; i >= 0; i--)
        {
            var p = chromeOpenOrder[i];
            if (p != null && p.activeInHierarchy && !chromeClosing.ContainsKey(p) && CanvasShown(p)) return p;
        }
        return null;
    }

    /// <summary>⚠ 地上では迷宮の Canvas ごと `enabled=false` にする（窓は activeSelf のまま残る）。
    /// 見えていない窓を「手前」と数えると、Esc が見えない窓を閉じて何も起きないように見える。</summary>
    private static bool CanvasShown(GameObject p)
    {
        var cv = p.GetComponentInParent<Canvas>();
        if (cv == null) return true;
        var root = cv.rootCanvas;
        return cv.enabled && (root == null || root.enabled);
    }

    private void StartPop(GameObject p)
    {
        var rt = p.transform as RectTransform;
        if (rt == null) return;
        rt.localScale = Vector3.one * 0.965f;
        chromePopping[rt] = 0f;
        PlayFadeIn(p);
    }

    private void ResetLook(GameObject p)
    {
        if (p == null) return;
        p.transform.localScale = Vector3.one;
        var cg = p.GetComponent<CanvasGroup>(); if (cg != null) cg.alpha = 1f;
        var rt = p.transform as RectTransform; if (rt != null) chromePopping.Remove(rt);
    }

    /// <summary>動きをつけて閉じる（×・Esc・幕から）。</summary>
    private bool CloseAnimated(GameObject p)
    {
        if (p == null || !p.activeInHierarchy || chromeClosing.ContainsKey(p)) return false;
        chromeClosing[p] = 0f;
        var cg = p.GetComponent<CanvasGroup>(); if (cg == null) cg = p.AddComponent<CanvasGroup>();
        fadingIn.Remove(cg);   // ⚠ 開くときのフェードが残っていると、閉じながら濃くなる
        HideTooltip();
        return true;
    }

    /// <summary>窓ごとの「閉じる」の中身。⚠ 印を持っている窓は印も下ろす（下ろさないと次の再描画で開き直す）。</summary>
    private void CloseNow(GameObject p)
    {
        if (p == guidePanel) CloseGuide();
        else if (p == expeditionPanel) CloseExpeditionWindow();
        else if (p == chestGradePanel) { chestGradeOpen = false; RefreshChestGradeWindow(); }
        else p.SetActive(false);
    }

    /// <summary>
    /// × が無い窓に足す（魔王の窓など）。⚠ 既にある × は二重に作らない ―― 直下の「×」の札を探す。
    /// </summary>
    private void EnsureCloseButton(GameObject p)
    {
        var tr = p.transform;
        if (tr.Find("ChromeClose") != null) return;
        for (int i = 0; i < tr.childCount; i++)
        {
            var c = tr.GetChild(i);
            if (c.GetComponent<Button>() == null) continue;
            var lbl = c.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null && (lbl.text == "×" || lbl.text == "閉じる")) return;
        }
        var target = p;
        var b = PrimaryButton(tr, "×", PANEL2, TEXT, () => CloseAnimated(target));
        b.name = "ChromeClose";
        var rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(32f, 30f);
        rt.anchoredPosition = new Vector2(-12f, -10f);
        var le = b.GetComponent<LayoutElement>(); if (le == null) le = b.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
    }

    // ============ 🖼️ 窓の背景の模様（段D） ============
    private Sprite panelPattern;
    private bool panelPatternTried;

    /// <summary>
    /// 窓の背景に、うっすら模様を敷く（ユーザー選択：C＝生成した模様。金の飾り枠が並ぶ魔導書の頁）。
    /// ⚠ 召喚の儀・行商人は自前の背景絵があるので敷かない。⚠ 文字の読みやすさを落とさないよう、ごく薄く。
    /// </summary>
    private void EnsurePattern(GameObject p)
    {
        if (p == ritualPanel || p == shopPanel) return;
        if (p.transform.Find("BgPattern") != null) return;
        if (!panelPatternTried) { panelPatternTried = true; panelPattern = Resources.Load<Sprite>("UI/pattern_grimoire"); }
        if (panelPattern == null) return;
        var img = new GameObject("BgPattern", typeof(RectTransform)).AddComponent<Image>();
        img.rectTransform.SetParent(p.transform, false);
        img.sprite = panelPattern;
        img.type = Image.Type.Tiled;
        img.pixelsPerUnitMultiplier = 0.5f;          // 1枚＝256（ドットを2倍で見せる）
        img.color = new Color(1f, 1f, 1f, 0.07f);   // ⚠ 0.2 だと金の枠が強すぎて文字を食った（実測）
        img.raycastTarget = false;
        StretchOffset(img.rectTransform, 6, 6, 6, 6);
        var le = img.gameObject.AddComponent<LayoutElement>(); le.ignoreLayout = true;
        // 中身の下に敷く。⚠ スキンの枠（子の "Frame"）が不透明な絵なので、**その上**に置かないと隠れる（実測で見えなかった）
        var frame = p.transform.Find("Frame");
        if (frame != null) img.transform.SetSiblingIndex(frame.GetSiblingIndex() + 1);
        else img.transform.SetAsFirstSibling();
    }

    private void TickBackdrop()
    {
        var top = TopChromePanel();
        if (top == null)
        {
            if (chromeBackdrop != null && chromeBackdrop.gameObject.activeSelf) chromeBackdrop.gameObject.SetActive(false);
            return;
        }
        if (chromeBackdrop == null)
        {
            chromeBackdrop = UIKit.Panel(top.transform.parent, "ChromeBackdrop", new Color(0.03f, 0.02f, 0.05f, 0.35f));
            var brt = chromeBackdrop.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(20000f, 20000f);   // 親の大きさに頼らない（親が盤いっぱいとは限らない）
            brt.anchoredPosition = Vector2.zero;
            var bb = chromeBackdrop.gameObject.AddComponent<Button>();
            bb.transition = Selectable.Transition.None;
            bb.onClick.AddListener(() =>
            {
                if (GachaRevealing) { SkipGachaReveal(); return; }   // ✦ 演出中は閉じずに飛ばす
                var t = TopChromePanel(); if (t != null) CloseAnimated(t);
            });
        }
        var bt = chromeBackdrop.transform;
        if (bt.parent != top.transform.parent) bt.SetParent(top.transform.parent, false);
        if (!chromeBackdrop.gameObject.activeSelf) chromeBackdrop.gameObject.SetActive(true);
        // ⚠ 窓は開くたびに `SetAsLastSibling` されるので、毎フレーム「窓の1つ後ろ」を確かめ直す
        int ti = top.transform.GetSiblingIndex(), bi = bt.GetSiblingIndex();
        if (bi != ti - 1)
        {
            // ⚠ 幕が窓より前にあるときは、抜いた時点で窓が1つ詰まる → 窓の1つ前へ。
            //   後ろにあるときは窓の位置に差し込む（窓が1つ後ろへずれる）。
            bt.SetSiblingIndex(bi < ti ? ti - 1 : ti);
        }
        var rt = top.transform as RectTransform;
        bool full = rt != null && rt.rect.width >= FullscreenMinW;
        float a = full ? 0.9f : 0.35f;
        var col = chromeBackdrop.color;
        if (Mathf.Abs(col.a - a) > 0.001f) { col.a = Mathf.MoveTowards(col.a, a, Time.unscaledDeltaTime * 6f); chromeBackdrop.color = col; }
    }

    private void TickClickSound()
    {
        var ptr = UnityEngine.InputSystem.Pointer.current;
        var es = EventSystem.current;
        if (ptr == null || es == null || !ptr.press.wasPressedThisFrame) return;
        var ped = new PointerEventData(es) { position = ptr.position.ReadValue() };
        chromeHits.Clear();
        es.RaycastAll(ped, chromeHits);
        if (chromeHits.Count == 0) return;
        var sel = chromeHits[0].gameObject.GetComponentInParent<Selectable>();
        if (sel == null) return;
        if (!sel.IsInteractable()) { SoundSystem.Play(SoundSystem.Sfx.Error, 0.45f); return; }
        string n = sel.gameObject.name;
        var lbl = sel.GetComponentInChildren<TMP_Text>();
        bool closeLike = n == "ChromeClose" || n == "ChromeBackdrop"
            || (lbl != null && (lbl.text == "×" || lbl.text == "閉じる" || lbl.text == "戻る"));
        SoundSystem.Play(closeLike ? SoundSystem.Sfx.Cancel : SoundSystem.Sfx.Click, closeLike ? 0.55f : 0.5f);
    }

    // ============ 💬 説明をアイコンの近くに ============
    private RectTransform tooltipArrow;

    /// <summary>
    /// 触れた物のすぐ下に、矢印つきで出す。画面の下端に当たるときは上に出し、左右は画面の内側へ寄せる。
    /// ⚠ 幅は中身に合わせる（短い説明が 560px の帯で出ると、どこの説明か分からなかった）。
    /// </summary>
    private void ShowTooltipAt(string s, RectTransform target)
    {
        if (tooltipGO == null) return;
        if (target == null) { ShowTooltip(s); return; }
        SetTxt(tooltipText, s);
        tooltipGO.SetActive(true); tooltipGO.transform.SetAsLastSibling();
        var prt = (RectTransform)tooltipGO.transform;
        var canvasRt = prt.parent as RectTransform;
        if (canvasRt == null) return;

        // 大きさ（中身に合わせる・最大 520px）
        const float maxW = 520f, padX = 12f, padY = 7f;
        string shown = tooltipText.text;
        Vector2 one = tooltipText.GetPreferredValues(shown);
        float w = Mathf.Min(one.x, maxW - padX * 2f) + padX * 2f;
        float h = tooltipText.GetPreferredValues(shown, w - padX * 2f, 0f).y + padY * 2f;
        bool multi = one.x > maxW - padX * 2f || shown.Contains("\n");
        tooltipText.alignment = multi ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center;
        StretchOffset(tooltipText.rectTransform, padX, padY, padX, padY);
        prt.sizeDelta = new Vector2(w, h);

        // 触れた物の上下の端（ツールチップのCanvasの座標で）
        var cv = target.GetComponentInParent<Canvas>();
        Camera cam = (cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay) ? cv.worldCamera : null;
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);   // 0=左下 1=左上 2=右上 3=右下
        Vector2 lo, hi;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, RectTransformUtility.WorldToScreenPoint(cam, (corners[0] + corners[3]) * 0.5f), null, out lo);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, RectTransformUtility.WorldToScreenPoint(cam, (corners[1] + corners[2]) * 0.5f), null, out hi);

        float halfW = canvasRt.rect.width * 0.5f, halfH = canvasRt.rect.height * 0.5f;
        const float gap = 10f, edge = 8f;
        bool below = lo.y - gap - h >= -halfH + edge;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, below ? 1f : 0f);
        float x = Mathf.Clamp(lo.x, -halfW + w * 0.5f + edge, halfW - w * 0.5f - edge);
        float y = below ? lo.y - gap : hi.y + gap;
        prt.anchoredPosition = new Vector2(x, y);

        // 矢印（触れた物の真ん中を指す）
        if (tooltipArrow == null)
        {
            var ai = UIKit.Panel(prt, "Arrow", tooltipGO.GetComponent<Image>().color);
            ai.raycastTarget = false;
            tooltipArrow = ai.rectTransform;
            tooltipArrow.sizeDelta = new Vector2(11f, 11f);
            tooltipArrow.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tooltipArrow.SetAsFirstSibling();   // 文字の下に敷く
        }
        tooltipArrow.gameObject.SetActive(true);
        tooltipArrow.anchorMin = tooltipArrow.anchorMax = new Vector2(0.5f, below ? 1f : 0f);
        tooltipArrow.pivot = new Vector2(0.5f, 0.5f);
        float ax = Mathf.Clamp(lo.x - x, -w * 0.5f + 12f, w * 0.5f - 12f);
        tooltipArrow.anchoredPosition = new Vector2(ax, 0f);
    }
}
