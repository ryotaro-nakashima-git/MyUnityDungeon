using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🖥️ <b>全画面の画面</b>（研究ツリー・地上ツリー・魔物図鑑）の共通部品。
///
/// ⚠ これまでは 1820×1020 の窓で、周りの約50pxから迷宮の画面がのぞいていた
///   ＝「窓が乗っているだけ」に見え、別の画面に来た感じがしなかった（ユーザー承認の画面案）。
///   いまは<b>画面全体</b>を覆い、上に<b>専用の帯</b>（題・タブ・その画面で使う資源・×／Esc）を置く。
/// ⚠ 背景の模様（`EnsurePattern`）は<b>そのまま</b>（ユーザー指定）。札は不透明にして読みやすさを保つ。
/// ⚠ 迷宮の上の帯が隠れるので、<b>その画面で使う資源は帯の右</b>で読ませる。
/// <para>`GameUIManager` の partial。</para>
/// </summary>
public partial class GameUIManager
{
    private RectTransform researchTabHost, surfaceTreeTabHost, codexTabHost;
    private TextMeshProUGUI[] researchChips, surfaceTreeChips, codexChips;

    /// <summary>全画面の帯の高さ。</summary>
    private const float FullHdrH = 72f;

    /// <summary>
    /// いまの Canvas の大きさ（CanvasScaler と同じ式で出す）。
    /// ⚠ 組み立ては Start で走るので、Canvas の rect がまだ決まっていないことがある。式で出せば確実。
    /// </summary>
    private static Vector2 FullSize()
    {
        var refRes = UIKit.ReferenceRes();
        float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
        float match = sh > sw ? 1f : 0.5f;
        float logW = Mathf.Log(sw / refRes.x, 2f), logH = Mathf.Log(sh / refRes.y, 2f);
        float scale = Mathf.Pow(2f, Mathf.Lerp(logW, logH, match));
        return new Vector2(sw / scale, sh / scale);
    }

    /// <summary>全画面の器（真ん中に置いて Canvas と同じ大きさ）。</summary>
    private Image FullPanel(RectTransform root, string name, out float W, out float H)
    {
        var size = FullSize();
        W = size.x; H = size.y;
        var panel = Panel(root, name, PANEL);
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = size;
        panel.rectTransform.anchoredPosition = Vector2.zero;
        SkinPanel(panel);
        return panel;
    }

    /// <summary>
    /// 全画面の帯。左＝題と一言、真ん中＝タブの置き場（<paramref name="tabHost"/>）、
    /// 右＝資源の札の置き場（<paramref name="chipHost"/>）と ×。
    /// ⚠ タブも札も**横並びのレイアウト**に入れる（数が変わっても真ん中／右に寄ったまま）。
    /// </summary>
    private void BuildFullHeader(Image panel, float W, string title, string sub, UnityEngine.Events.UnityAction onClose,
                                 out RectTransform tabHost, out Image chipHost)
    {
        var hdr = Panel(panel, "FullHeader", C("#17141f"));
        Place(hdr.rectTransform, 0, 0, W, FullHdrH);
        var line = Panel(hdr, "Line", LINE2);
        Place(line.rectTransform, 0, FullHdrH - 2, W, 2);

        var t = Text(hdr, title, 26, GOLD, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        t.enableWordWrapping = false;
        Place(t.rectTransform, 28, 0, 360, FullHdrH);
        float tw = Mathf.Min(360f, t.GetPreferredValues(title).x);
        if (!string.IsNullOrEmpty(sub))
        {
            var s = Text(hdr, sub, 12.5f, MUTED, TextAlignmentOptions.MidlineLeft);
            s.enableWordWrapping = true;
            Place(s.rectTransform, 28 + tw + 18, 10, 330, FullHdrH - 20);
        }

        // ── 真ん中：タブ ──
        tabHost = NewRect("Tabs", hdr.rectTransform);
        Place(tabHost, W * 0.5f - 480f, 13, 960f, 46);
        var th = tabHost.gameObject.AddComponent<HorizontalLayoutGroup>();
        th.spacing = 6; th.childAlignment = TextAnchor.MiddleCenter;
        th.childControlWidth = false; th.childControlHeight = false;
        th.childForceExpandWidth = false; th.childForceExpandHeight = false;

        // ── 右：× と資源 ──
        var close = PrimaryButton(hdr, "×", PANEL2, TEXT, onClose);
        Place((RectTransform)close.transform, W - 28 - 46, 13, 46, 46);
        AddTooltip(close.gameObject, "閉じる　<color=#9c95b4>[Esc]</color>");
        chipHost = Panel(hdr, "Chips", new Color(0, 0, 0, 0));
        chipHost.raycastTarget = false;
        Place(chipHost.rectTransform, W - 28 - 46 - 16 - 420, 14, 420, 44);
        var ch = chipHost.gameObject.AddComponent<HorizontalLayoutGroup>();
        ch.spacing = 8; ch.childAlignment = TextAnchor.MiddleRight;
        ch.childControlWidth = true; ch.childControlHeight = true;
        ch.childForceExpandWidth = false; ch.childForceExpandHeight = false;
    }

    /// <summary>帯のタブ1つ（押すと <paramref name="onClick"/>）。選んでいれば金の枠。</summary>
    private Image FullTab(RectTransform host, string label, float w, bool on, bool dim, UnityEngine.Events.UnityAction onClick)
    {
        var tab = Panel(host, "Tab_" + label, on ? C("#2c2540") : PANEL2);
        tab.rectTransform.sizeDelta = new Vector2(w, 44);
        Outline(tab, on ? GOLD : LINE);
        var t = Text(tab.rectTransform, label, 16, on ? C("#ffd24a") : (dim ? FAINT : TEXT), TextAlignmentOptions.Center, FontStyles.Bold);
        t.enableWordWrapping = false;
        StretchFull(t.rectTransform);
        var bt = tab.gameObject.AddComponent<Button>(); bt.targetGraphic = tab;
        bt.onClick.AddListener(onClick);
        return tab;
    }

    /// <summary>研究ツリーの帯の札（研究点・習熟・危険度）。迷宮の研究と地上ツリーで同じ並び。</summary>
    private TextMeshProUGUI[] TreeChips(Image chipHost)
    {
        return new[]
        {
            ResChip(chipHost, UITheme.Research, "研究点", "0", "research"),
            ResChip(chipHost, C("#ffd24a"), "習熟", "0", null),
            ResChip(chipHost, UITheme.Grade, "危険度", "三級", "danger"),
        };
    }

    private static void SetTreeChips(TextMeshProUGUI[] chips)
    {
        if (chips == null || chips.Length < 3) return;
        if (chips[0] != null) chips[0].text = ResearchState.RP + " RP";
        if (chips[1] != null) chips[1].text = ResearchState.MasteredCount.ToString();
        if (chips[2] != null) chips[2].text = DangerRank.Name;
    }
}
