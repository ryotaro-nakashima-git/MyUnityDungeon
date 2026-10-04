using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🌱 <b>育った手応え</b>の見せ方（H3）。中身の記録は [[MinionGrowth]]。
/// - 格上げ・進化の<b>場面</b>：上の真ん中に札を大きく出す（約2.5秒・押すと飛ばせる・盤は触れる）
/// - 決算の<b>「配下の活躍」</b>：この波で倒した数・Lv・格が動いた個体を札で並べる
/// - 呼び名の<b>付け直し</b>（配下の画面から）
/// ⚠ 数値は何も変えない（見せ方だけ）。
/// <para>`GameUIManager` の partial。</para>
/// </summary>
public partial class GameUIManager
{
    // ================= 格上げ・進化の場面 =================
    private RectTransform momentRoot;
    private CanvasGroup momentGroup;
    private Image momentArt;
    private TextMeshProUGUI momentTitle, momentLine;
    private float momentT = -1f;
    private const float MomentIn = 0.22f, MomentHold = 2.3f, MomentOut = 0.35f;

    private void BuildMomentBanner()
    {
        var cv = MakeCanvas("GrowthMomentCanvas", 235);   // ⚠ 地上(110)・説明(200)より上、案内(240)・幕間(250)より下
        momentRoot = NewRect("Moment", cv);
        momentRoot.anchorMin = momentRoot.anchorMax = momentRoot.pivot = new Vector2(0.5f, 1f);
        momentRoot.sizeDelta = new Vector2(620f, 128f);
        momentRoot.anchoredPosition = new Vector2(0f, -96f);
        momentGroup = momentRoot.gameObject.AddComponent<CanvasGroup>();
        var bg = Panel(momentRoot, "Bg", C("#191726"));
        StretchFull(bg.rectTransform); Outline(bg, GOLD); SkinPanel(bg);
        var skip = bg.gameObject.AddComponent<Button>(); skip.transition = Selectable.Transition.None;
        skip.onClick.AddListener(() => { if (momentT >= 0f && momentT < MomentIn + MomentHold) momentT = MomentIn + MomentHold; });
        var frame = Panel(bg, "Por", C("#15131f"));
        Place(frame.rectTransform, 16, 14, 100, 100); Outline(frame, LINE2);
        momentArt = new GameObject("Art", typeof(RectTransform)).AddComponent<Image>();
        momentArt.rectTransform.SetParent(frame.rectTransform, false);
        momentArt.raycastTarget = false; momentArt.preserveAspect = true;
        Place(momentArt.rectTransform, 6, 6, 88, 88);
        momentTitle = Text(bg, "", 26, C("#ffd24a"), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        momentTitle.enableWordWrapping = false;
        Place(momentTitle.rectTransform, 132, 16, 470, 36);
        momentLine = Text(bg, "", 15, TEXT, TextAlignmentOptions.TopLeft);
        momentLine.enableWordWrapping = true;
        Place(momentLine.rectTransform, 132, 56, 470, 60);
        momentRoot.gameObject.SetActive(false);
    }

    /// <summary>毎フレーム（Hud の Update から）。⚠ 幕間・オープニング・タイトル中は待つ。</summary>
    private void TickMoment()
    {
        if (momentRoot == null) return;
        if (momentT < 0f)
        {
            if (MinionGrowth.Moments.Count == 0) return;
            if (InterludePlaying || OpeningPlaying || !GameSetup.Started) return;
            var m = MinionGrowth.Moments.Dequeue();
            var v = MinionRoster.Get(m.id);
            if (v == null) return;
            var sp = MinionSprite.ByIndex(v.catalogIndex);
            momentArt.sprite = sp != null ? sp : IconFactory.Get("魔物");
            SetTxt(momentTitle, m.title);
            SetTxt(momentLine, m.line);
            momentRoot.gameObject.SetActive(true);
            momentRoot.SetAsLastSibling();
            momentT = 0f;
            SoundSystem.Play(SoundSystem.Sfx.Discover, 0.8f);
        }
        momentT += Time.unscaledDeltaTime;
        float a, s;
        if (momentT < MomentIn) { float p = momentT / MomentIn; a = p; s = Mathf.Lerp(0.9f, 1f, 1f - (1f - p) * (1f - p)); }
        else if (momentT < MomentIn + MomentHold) { a = 1f; s = 1f; }
        else { float p = (momentT - MomentIn - MomentHold) / MomentOut; a = 1f - p; s = 1f; }
        momentGroup.alpha = Mathf.Clamp01(a);
        momentRoot.localScale = new Vector3(s, s, 1f);
        if (momentT >= MomentIn + MomentHold + MomentOut) { momentRoot.gameObject.SetActive(false); momentT = -1f; }
    }

    // ================= 決算の「配下の活躍」 =================
    private TextMeshProUGUI rptGrowthHead;
    private RectTransform rptGrowthHost;

    private void BuildReportGrowth(RectTransform card)
    {
        rptGrowthHead = Text(card, "配下の活躍", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(rptGrowthHead.rectTransform, 26, 0, 300, 16);
        rptGrowthHost = NewRect("Growth", card);
        Place(rptGrowthHost, 26, 20, RPT_W - 52, 64);
    }

    /// <summary>「配下の活躍」を <paramref name="y"/> から並べて、次の y を返す。動いた個体が居なければ見出しごと畳む。</summary>
    private float FillReportGrowth(float y)
    {
        if (rptGrowthHost == null) return y;
        for (int i = rptGrowthHost.childCount - 1; i >= 0; i--) { var g = rptGrowthHost.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        var list = MinionGrowth.Diff();
        bool any = list.Count > 0;
        rptGrowthHead.gameObject.SetActive(any);
        rptGrowthHost.gameObject.SetActive(any);
        if (!any) return y;
        Place(rptGrowthHead.rectTransform, 26f, y, 300, 16);
        Place(rptGrowthHost, 26f, y + 20f, RPT_W - 52f, 64f);
        int n = Mathf.Min(4, list.Count);
        float cw = (RPT_W - 52f - 10f * 3) / 4f;
        for (int i = 0; i < n; i++)
        {
            var e = list[i];
            var v = MinionRoster.Get(e.id); if (v == null) continue;
            var cell = Panel(rptGrowthHost, "G" + i, i == 0 ? C("#2a2235") : CARD);
            Place(cell.rectTransform, i * (cw + 10f), 0, cw, 64); Outline(cell, i == 0 ? GOLD : LINE);
            var a = new GameObject("Art", typeof(RectTransform)).AddComponent<Image>();
            a.rectTransform.SetParent(cell.rectTransform, false); a.raycastTarget = false; a.preserveAspect = true;
            var sp = MinionSprite.ByIndex(v.catalogIndex); a.sprite = sp != null ? sp : IconFactory.Get("魔物");
            Place(a.rectTransform, 6, 8, 48, 48);
            var nm = Text(cell.rectTransform, (i == 0 && e.kills > 0 ? "<color=#ffd24a>◆</color>" : "") + MinionRoster.NameOf(v), 13.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            nm.enableWordWrapping = false; nm.enableAutoSizing = true; nm.fontSizeMin = 10; nm.fontSizeMax = 13.5f;
            Place(nm.rectTransform, 60, 6, cw - 66, 22);
            var sb = new System.Text.StringBuilder();
            if (e.kills > 0) sb.Append("<color=#e05a5a>撃破 ").Append(e.kills).Append("</color>  ");
            if (e.lvTo > e.lvFrom) sb.Append("<color=#5cc47c>Lv").Append(e.lvFrom).Append("→").Append(e.lvTo).Append("</color>  ");
            if (e.rankTo > e.rankFrom) sb.Append("<color=#ffd24a>").Append(MinionRank.Name(e.rankTo)).Append("に成った</color>  ");
            if (e.evolved) sb.Append("<color=#b48be6>進化</color>");
            var ln = Text(cell.rectTransform, sb.ToString(), 11.5f, MUTED, TextAlignmentOptions.TopLeft);
            ln.enableWordWrapping = true;
            Place(ln.rectTransform, 60, 30, cw - 66, 32);
        }
        if (list.Count > 4)
        {
            var more = Text(rptGrowthHost, "ほか " + (list.Count - 4) + " 体", 11, FAINT, TextAlignmentOptions.Right);
            Place(more.rectTransform, RPT_W - 52f - 200f, -20f, 200, 16);
        }
        return y + 20f + 64f + 12f;
    }

    // ================= 呼び名の付け直し =================
    private GameObject nameEditBox;

    /// <summary>配下の画面の名前の所に、その場で打てる欄を出す。⚠ 空にすると魔神の名に戻る。</summary>
    private void OpenNameEdit(RectTransform parent, int id, float x, float y, float w)
    {
        var v = MinionRoster.Get(id); if (v == null) return;
        if (nameEditBox != null) Destroy(nameEditBox);
        var box = Panel(parent, "NameEdit", C("#0f0d16"));
        nameEditBox = box.gameObject;
        Place(box.rectTransform, x, y, w, 40); Outline(box, GOLD);
        var area = NewRect("TextArea", box.rectTransform);
        area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(10, 4); area.offsetMax = new Vector2(-10, -4);
        area.gameObject.AddComponent<RectMask2D>();
        var txt = Text(area, "", 20, TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        txt.enableWordWrapping = false; StretchFull(txt.rectTransform);
        var ph = Text(area, "名前（空にすると魔神の名に戻る）", 13, FAINT, TextAlignmentOptions.MidlineLeft);
        ph.enableWordWrapping = false; StretchFull(ph.rectTransform);
        var input = box.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area; input.textComponent = txt; input.placeholder = ph;
        input.targetGraphic = box;
        input.characterLimit = 10;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.text = v.nickname ?? "";
        input.onEndEdit.AddListener(s =>
        {
            var vv = MinionRoster.Get(id);
            if (vv != null)
            {
                string t = (s ?? "").Trim();
                vv.nickname = string.IsNullOrEmpty(t) ? null : t;
            }
            if (nameEditBox != null) { Destroy(nameEditBox); nameEditBox = null; }
            RefreshArmy();
        });
        input.ActivateInputField();
        input.Select();
    }
}
