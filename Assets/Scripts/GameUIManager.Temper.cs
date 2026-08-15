using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🧠 **気性の2択**（召喚のとき／調教のとき）。研究『見極め』で開く。
/// <para>`GameUIManager` の partial。データは <see cref="MinionTemperament"/> / <see cref="MinionRoster"/>。</para>
/// <para>
/// ⚠ ここは**引き直しではなく選択**の窓。引き直せる形にすると「当たりが出るまで回す」になり、
///   12種を平均1.0で釣り合わせた意味が消える（→ [[MinionTemperament]]）。
/// </para>
/// </summary>
public partial class GameUIManager
{
    private GameObject temperPanel;
    private RectTransform temperBody;
    private const float TEMPER_W = 720f;

    private void BuildTemperPanel(RectTransform root)
    {
        var panel = Panel(root, "TemperPanel", PANEL);
        temperPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(TEMPER_W, 300);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        Outline(panel, GOLD); SkinPanel(panel);
        temperBody = NewRect("Body", panel.rectTransform);
        Place(temperBody, 22, 18, TEMPER_W - 44, 264);
        temperPanel.SetActive(false);
    }

    /// <summary>召喚の2択。選んだ気性で個体が生まれる。</summary>
    private void OpenTemperChoiceForSummon(int catalogIndex)
    {
        int a, b; MinionRoster.RollTwoTempers(-1, out a, out b);
        ShowTemperChoice("この子はどう育った？", MinionCatalog.Get(catalogIndex).jpName + " を召喚する", a, b, t =>
        {
            if (MinionRoster.TrySummon(catalogIndex, t) != null) { RefreshMinionCodex(); RefreshSquadStrip(); }
        });
    }

    /// <summary>調教の2択。いまの気性は出さない（同じものに払わされないように）。</summary>
    private void OpenTemperChoiceForRetrain(int id)
    {
        var v = MinionRoster.Get(id); if (v == null) return;
        int a, b; MinionRoster.RollTwoTempers(v.temper, out a, out b);
        string nm = MinionCatalog.Get(v.catalogIndex).jpName;
        ShowTemperChoice("鍛え直す", nm + " #" + id + "　いまは『" + MinionTemperament.Name(v.temper) + "』　-"
            + MinionTemperament.RetrainCost + "DP", a, b, t =>
        {
            string why;
            if (MinionRoster.TryRetrain(id, t, out why)) RefreshMinionCodex();
            else { NotifySystem.Push("調教できない：" + why, NotifySystem.Kind.Loss); SoundSystem.Play(SoundSystem.Sfx.Error); }
        });
    }

    private void ShowTemperChoice(string title, string sub, int a, int b, System.Action<int> pick)
    {
        if (temperBody == null) return;
        for (int i = temperBody.childCount - 1; i >= 0; i--) Destroy(temperBody.GetChild(i).gameObject);
        float w = TEMPER_W - 44;

        var ttl = Text(temperBody, title, 18, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(ttl.rectTransform, 0, 0, w, 26);
        var sb = Text(temperBody, sub, 12, MUTED, TextAlignmentOptions.Left);
        Place(sb.rectTransform, 0, 28, w, 20);

        float cw = (w - 14) / 2f;
        int[] two = { a, b };
        for (int i = 0; i < 2; i++)
        {
            int t = two[i];
            var d = MinionTemperament.Get(t);
            var card = Panel(temperBody, "T" + i, CARD);
            Place(card.rectTransform, i * (cw + 14), 56, cw, 150); Outline(card, C(d.colorHex));
            var side = Panel(card.rectTransform, "side", C(d.colorHex));
            Place(side.rectTransform, 0, 0, 3, 150);
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
            btn.onClick.AddListener(() => { CloseTemper(); pick(t); });
            var nm = Text(card.rectTransform, d.jpName, 20, C(d.colorHex), TextAlignmentOptions.Left, FontStyles.Bold);
            Place(nm.rectTransform, 14, 12, cw - 28, 26);
            var ds = Text(card.rectTransform, d.desc, 12, TEXT, TextAlignmentOptions.TopLeft);
            Place(ds.rectTransform, 14, 44, cw - 28, 74);
            var aim = Text(card.rectTransform, AimLabel(d), 11, C("#8cb8e6"), TextAlignmentOptions.Left, FontStyles.Bold);
            Place(aim.rectTransform, 14, 122, cw - 28, 18);
        }

        var cancel = PrimaryButton(temperBody, "やめる", PANEL2, MUTED, CloseTemper);
        Place((RectTransform)cancel.transform, w - 150, 214, 150, 38);

        temperPanel.SetActive(true);
        temperPanel.transform.SetAsLastSibling();
        PlayFadeIn(temperPanel);
    }

    private void CloseTemper() { if (temperPanel != null) temperPanel.SetActive(false); }

    /// <summary>「誰を狙うか」を1行で。数値より**この行**が選ぶ理由になる。</summary>
    private static string AimLabel(MinionTemperament.Def d)
    {
        switch (d.aim)
        {
            case MinionTemperament.Aim.Strongest: return "狙い： いちばん強い相手";
            case MinionTemperament.Aim.Weakest: return "狙い： いちばん弱った相手";
            case MinionTemperament.Aim.Caster: return "狙い： 術者を優先";
            case MinionTemperament.Aim.Sticky: return "狙い： 倒すまで変えない";
            default:
                if (d.leash == 1) return "狙い： 近い相手／置いたマスから離れない";
                if (d.leash >= 5) return "狙い： 近い相手／どこまでも追う";
                return "狙い： いちばん近い相手";
        }
    }

    /// <summary>個体行に出す気性のバッジ文字列（色つき）。</summary>
    private static string TemperBadge(int temper)
    {
        var d = MinionTemperament.Get(temper);
        return "<color=" + d.colorHex + ">" + d.jpName + "</color>";
    }
}
