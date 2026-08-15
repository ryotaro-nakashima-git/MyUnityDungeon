using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ⚡ **迷宮の異変**の窓（→ <see cref="IncidentSystem"/>）。
/// <para>`GameUIManager` の partial。</para>
/// <para>
/// ⚠ この窓は**閉じられない**（『やめる』が無い）。答えないと効果が宙に浮くし、
///   「後で考える」を許すと結局ターン終わりまで放置されて、事件が事件でなくなる。
///   代わりに**どの選択肢も一長一短**にしてあるので、悩みはしても詰まらない。
/// </para>
/// </summary>
public partial class GameUIManager
{
    private GameObject incidentPanel;
    private RectTransform incidentBody;
    private const float INC_W = 860f;
    private string incidentSig = "";

    private void BuildIncidentPanel(RectTransform root)
    {
        var panel = Panel(root, "IncidentPanel", PANEL);
        incidentPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(INC_W, 480);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        Outline(panel, C("#b0202b")); SkinPanel(panel);
        incidentBody = NewRect("Body", panel.rectTransform);
        Place(incidentBody, 26, 22, INC_W - 52, 440);
        incidentPanel.SetActive(false);
    }

    /// <summary>
    /// 毎フレーム見る。⚠ **署名方式**（同じ事件なら作り直さない）。
    /// 毎フレーム組み直すと、押している最中にボタンが破棄されてクリックが成立しない。
    /// </summary>
    private void RefreshIncident()
    {
        if (incidentPanel == null) return;
        string sig = IncidentSystem.HasPending ? IncidentSystem.Pending.id : "";
        if (sig == incidentSig) return;
        incidentSig = sig;

        if (!IncidentSystem.HasPending) { incidentPanel.SetActive(false); return; }
        BuildIncidentBody(IncidentSystem.Pending);
        incidentPanel.SetActive(true);
        incidentPanel.transform.SetAsLastSibling();
        PlayFadeIn(incidentPanel);
    }

    private void BuildIncidentBody(IncidentSystem.Def d)
    {
        for (int i = incidentBody.childCount - 1; i >= 0; i--) Destroy(incidentBody.GetChild(i).gameObject);
        float w = INC_W - 52, y = 0;

        var eye = Text(incidentBody, "迷宮の異変", 11, C("#e05a5a"), TextAlignmentOptions.Left, FontStyles.Bold);
        Place(eye.rectTransform, 0, y, w, 16); eye.characterSpacing = 6; y += 20;
        var ttl = Text(incidentBody, d.title, 26, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(ttl.rectTransform, 0, y, w, 34); y += 40;
        var body = Text(incidentBody, d.body, 14, MUTED, TextAlignmentOptions.TopLeft);
        Place(body.rectTransform, 0, y, w, 52); y += 62;

        int n = d.choices.Length;
        float ch = 88f;
        for (int i = 0; i < n; i++)
        {
            int idx = i; var c = d.choices[i];
            var card = Panel(incidentBody, "C" + i, CARD);
            Place(card.rectTransform, 0, y, w, ch); Outline(card, LINE2);
            var side = Panel(card.rectTransform, "side", GOLD); Place(side.rectTransform, 0, 0, 3, ch);
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
            btn.onClick.AddListener(() => { IncidentSystem.Choose(idx); RefreshIncident(); });
            var lb = Text(card.rectTransform, c.label, 17, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(lb.rectTransform, 16, 12, w - 32, 24);
            var ds = Text(card.rectTransform, c.desc, 12.5f, TEXT, TextAlignmentOptions.TopLeft);
            Place(ds.rectTransform, 16, 40, w - 32, 40);
            y += ch + 10;
        }

        var note = Text(incidentBody, "どれを選んでも一長一短。効果は<b>このターンだけ</b>。",
            11.5f, FAINT, TextAlignmentOptions.Left);
        Place(note.rectTransform, 2, y, w, 18); y += 24;

        incidentBody.sizeDelta = new Vector2(w, y);
        var prt = (RectTransform)incidentPanel.transform;
        prt.sizeDelta = new Vector2(INC_W, y + 44);
    }
}
