using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🗡️⛓️ **因縁と牢**の画面。`GameUIManager` の partial。
/// データは <see cref="Nemesis"/>（名のある冒険者）と <see cref="Prison"/>（捕虜）。
///
/// <para>
/// ⚠ **2つを同じ画面に置く**。因縁の相手を「捕らえる／折る／解き放つ」は同じ判断の裏表で、
///   別画面にすると「誰を捕らえたか」を覚えたまま移動して選ぶことになる（先触れ＋備えと同じ理屈）。
/// </para>
/// </summary>
public partial class GameUIManager
{
    private const float PRISON_W = 1180f;

    private void BuildPrisonPanel(RectTransform root)
    {
        var panel = Panel(root, "PrisonPanel", PANEL);
        prisonPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(PRISON_W, 760);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        Outline(panel, LINE2); SkinPanel(panel);

        // ⚠ 画面に出す文字列に絵文字を書かない（□になる → [[ui-conventions]]）
        var title = Text(panel, "因縁と牢", 17, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, 24, 16, PRISON_W - 90, 26);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => prisonPanel.SetActive(false));
        Place((RectTransform)close.transform, PRISON_W - 56, 14, 32, 30);

        prisonBody = MakeVScroll(panel, 24, 52, PRISON_W - 48, 760 - 52 - 24);
        prisonPanel.SetActive(false);
    }

    private void OpenPrison()
    {
        if (prisonPanel == null) return;
        OpenExclusive(null);
        RefreshPrisonPanel();
        prisonPanel.SetActive(true);
        prisonPanel.transform.SetAsLastSibling();
        PlayFadeIn(prisonPanel);
    }

    private void RefreshPrisonPanel()
    {
        if (prisonBody == null) return;
        for (int i = prisonBody.childCount - 1; i >= 0; i--) Destroy(prisonBody.GetChild(i).gameObject);

        // ⚠⚠ `MakeVScroll` の Content は横ストレッチ。中身の幅はビューポート幅そのもの、
        //   `sizeDelta.x` は 0（実幅を入れると2倍になって左右に見切れる → [[ui-conventions]]）。
        float w = PRISON_W - 48;
        float y = 0;

        y = BuildPrisonSection(w, y);
        y = BuildNemesisSection(w, y);

        prisonBody.sizeDelta = new Vector2(0f, y + 12);
    }

    // ============ ⛓️ 牢 ============

    private float BuildPrisonSection(float w, float y)
    {
        var head = Text(prisonBody, "牢　<color=#9c95b4>倒れた冒険者を殺さず捕らえる。撃破DPも素材も入らない代わりに、尋問で研究点が採れる</color>",
            11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(head.rectTransform, 0, y, w, 16); y += 20;

        if (!Prison.Unlocked)
        {
            var lk = Panel(prisonBody, "PrisonLock", CARD);
            Place(lk.rectTransform, 0, y, w, 44); Outline(lk, LINE);
            var lt = Text(lk.rectTransform, "未解禁 - 領域研究『囚牢』を取ると、方針を<b>生け捕り</b>に切り替えられるようになる。",
                12.5f, MUTED, TextAlignmentOptions.Left);
            Place(lt.rectTransform, 14, 12, w - 26, 20);
            return y + 52;
        }

        // ── 方針と収容 ──
        var bar = Panel(prisonBody, "PrisonHead", CARD);
        Place(bar.rectTransform, 0, y, w, 78); Outline(bar, LINE2);
        var cap = Text(bar.rectTransform, "捕虜 " + Prison.Count + " / " + Prison.Capacity, 24, TEXT,
            TextAlignmentOptions.Left, FontStyles.Bold);
        Place(cap.rectTransform, 18, 10, 240, 32);
        var up = Text(bar.rectTransform, Prison.Count > 0
            ? "維持費 <color=#e3a94a>" + Prison.UpkeepTotal + " DP</color>／ターン　"
              + "尋問 <color=#8cb8e6>残り " + Prison.InterrogationsLeft + "/" + Prison.InterrogationsPerTurn + "</color>"
              + "　<color=#9c95b4>払えないと一番手強い者に破られる</color>"
            : "空いている", 12, MUTED, TextAlignmentOptions.Left);
        Place(up.rectTransform, 18, 46, w - 320, 20);

        bool alive = Prison.TakeAlive;
        var tg = PrimaryButton(bar.rectTransform, alive ? "方針：生け捕り" : "方針：殲滅",
            alive ? SEL : PANEL2, alive ? GOLD : TEXT,
            () => { Prison.SetTakeAlive(!Prison.TakeAlive); SoundSystem.Play(SoundSystem.Sfx.Click); RefreshPrisonPanel(); });
        Place((RectTransform)tg.transform, w - 220, 14, 200, 34);
        var tgh = Text(bar.rectTransform, alive ? "倒した者は牢へ（撃破DPは入らない）" : "倒した者はそのまま死ぬ",
            11, alive ? GREEN : MUTED, TextAlignmentOptions.Right);
        Place(tgh.rectTransform, w - 420, 52, 400, 18);
        y += 86;

        if (Prison.Count == 0)
        {
            var em = Text(prisonBody, "<color=#9c95b4>まだ誰も捕らえていない。方針を『生け捕り』にして波を凌ぐと、倒れた者がここへ来る。</color>",
                12, MUTED, TextAlignmentOptions.Left);
            Place(em.rectTransform, 4, y, w, 20);
            return y + 30;
        }

        // ── 捕虜カード ──
        var list = Prison.All;
        for (int i = 0; i < list.Count; i++)
        {
            var c = list[i];
            int cid = c.id;
            var card = Panel(prisonBody, "Cap" + cid, CARD);
            Place(card.rectTransform, 0, y, w, 128); Outline(card, c.nemesisId > 0 ? GOLD : LINE);
            var side = Panel(card.rectTransform, "side", C(WaveRoster.JobColor(c.job)));
            Place(side.rectTransform, 0, 0, 3, 128);

            var nm = Text(card.rectTransform, c.name, 14.5f, c.nemesisId > 0 ? GOLD : TEXT,
                TextAlignmentOptions.Left, FontStyles.Bold);
            Place(nm.rectTransform, 14, 8, 420, 22);
            var jb = Text(card.rectTransform, Prison.JobLabel(c), 11.5f, MUTED, TextAlignmentOptions.Left);
            Place(jb.rectTransform, 14, 30, 420, 18);

            // 気力／反抗心のバー（数えた値をそのまま長さにする）
            y += 0;
            // ⚠ 満量は `Prison` 側の上限と必ず揃える。ここを古い値のままにすると
            //   バーが実際より短く見えて「折れるまであとどれくらいか」が読めなくなる（実測でやった）。
            Meter(card.rectTransform, 14, 54, 300, "気力", c.vigor, Prison.VigorMax, C("#6ecf8e"));
            Meter(card.rectTransform, 14, 76, 300, "反抗心", c.defiance, Prison.DefianceMax,
                c.defiance <= 0 ? C("#6ecf8e") : C("#e05a5a"));

            bool lethal = Prison.WillBreak(c);
            var info = Text(card.rectTransform,
                "尋問 " + c.interrogated + " 回　次の尋問で <color=#8cb8e6>+" + Prison.InterrogateRpGain(c) + " RP</color>"
                + (c.nemesisId > 0 ? "　<color=#e3a94a>名のある者は倍</color>" : "")
                + (lethal ? "　<color=#e05a5a>この一度で事切れる（実りは倍）</color>" : "")
                + "\n維持 " + Prison.UpkeepPerCaptive(c) + " DP/ターン"
                + (c.defiance > 0 ? "　<color=#9c95b4>反抗心が0になると調伏できる</color>" : "　<color=#6ecf8e>折れている ― 調伏できる</color>"),
                11.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(info.rectTransform, 330, 54, w - 620, 46);

            // ── 処遇の4択 ──
            float bx = w - 272, bw = 128, bh = 26;
            string why;
            bool canInt = Prison.CanInterrogate(c, out why);
            // ⚠ 殺してしまう一手は**押す前に見た目が変わる**こと（黙って殺すと事故になる）
            var b1 = PrimaryButton(card.rectTransform,
                (lethal ? "搾る " : "尋問 ") + Prison.InterrogateDpCost(c) + "DP",
                canInt ? (lethal ? CARD : PANEL2) : CARD,
                canInt ? (lethal ? C("#e05a5a") : TEXT) : FAINT,
                () => { string w2; if (!Prison.TryInterrogate(cid, out w2)) Warn(w2); RefreshPrisonPanel(); });
            Place((RectTransform)b1.transform, bx, 12, bw, bh);

            bool canConv = Prison.CanConvert(c, out why);
            var b2 = PrimaryButton(card.rectTransform, "調伏 " + Prison.ConvertDpCost(c) + "DP",
                canConv ? SEL : CARD, canConv ? GOLD : FAINT,
                () => { string w2; if (!Prison.TryConvert(cid, out w2)) Warn(w2); RefreshPrisonPanel(); });
            Place((RectTransform)b2.transform, bx + bw + 8, 12, bw, bh);

            var b3 = PrimaryButton(card.rectTransform, "供物 +" + Prison.DevourDpGain(c) + "DP", PANEL2, TEXT,
                () => { string w2; if (!Prison.TryDevour(cid, out w2)) Warn(w2); RefreshPrisonPanel(); });
            Place((RectTransform)b3.transform, bx, 46, bw, bh);

            var b4 = PrimaryButton(card.rectTransform, "解放 +" + Prison.RansomDpGain(c) + "DP", PANEL2, C("#e05a5a"),
                () => { string w2; if (!Prison.TryRelease(cid, out w2)) Warn(w2); RefreshPrisonPanel(); });
            Place((RectTransform)b4.transform, bx + bw + 8, 46, bw, bh);

            var hint = Text(card.rectTransform,
                "調伏＝配下になる（素材 " + Prison.ConvertMaterialCost(c) + "）／供物＝魔王が喰らう／解放＝<b>必ず恨んで戻る</b>",
                10.5f, FAINT, TextAlignmentOptions.Left);
            Place(hint.rectTransform, bx, 80, bw * 2 + 8, 34);

            y += 136;
        }
        return y + 6;
    }

    /// <summary>ラベル付きの横棒。⚠ 長さは実測値そのまま（見せ方で盛らない）。</summary>
    private void Meter(RectTransform parent, float x, float y, float w, string label, int v, int max, Color col)
    {
        var lb = Text(parent, label + " " + v, 10.5f, FAINT, TextAlignmentOptions.Left);
        Place(lb.rectTransform, x, y, 90, 16);
        var bg = Panel(parent, "mbg" + label, C("#181528"));
        Place(bg.rectTransform, x + 78, y + 4, w - 78, 8);
        float f = max > 0 ? Mathf.Clamp01((float)v / max) : 0f;
        var fg = Panel(bg.rectTransform, "mfg", col);
        Place(fg.rectTransform, 0, 0, Mathf.Max(2f, (w - 78) * f), 8);
    }

    private void Warn(string why)
    {
        if (string.IsNullOrEmpty(why)) return;
        NotifySystem.Push(why, NotifySystem.Kind.Loss);
        SoundSystem.Play(SoundSystem.Sfx.Error);
    }

    // ============ 🗡️ 因縁 ============

    private float BuildNemesisSection(float w, float y)
    {
        y += 8;
        var head = Text(prisonBody, "因縁　<color=#9c95b4>半分以上削ってなお逃げ切った者、奈落を這い上がった者は名を得る。逃がすたびに強くなって戻る</color>",
            11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(head.rectTransform, 0, y, w, 16); y += 20;

        var all = Nemesis.All;
        if (all.Count == 0)
        {
            var em = Text(prisonBody, "<color=#9c95b4>まだ誰にも顔を覚えられていない。取り逃がしたときに、はじめて名が生まれる。</color>",
                12, MUTED, TextAlignmentOptions.Left);
            Place(em.rectTransform, 4, y, w, 20);
            return y + 30;
        }

        var sum = Text(prisonBody, "野に在り <b>" + Nemesis.AtLargeCount + "</b>　討伐済 <b>" + Nemesis.SlainCount
            + "</b>　次の波に来る <b>" + WaveRoster.NamedCount + "</b>", 12, MUTED, TextAlignmentOptions.Left);
        Place(sum.rectTransform, 4, y, w, 20); y += 26;

        float rw = (w - 10) / 2f;
        int shown = 0;
        for (int i = 0; i < all.Count; i++)
        {
            var h = all[i];
            var row = Panel(prisonBody, "Nem" + h.id, h.state == Nemesis.State_Slain ? PANEL2 : CARD);
            Place(row.rectTransform, (shown % 2) * (rw + 10), y + (shown / 2) * 62, rw, 56);
            Outline(row, h.state == Nemesis.State_AtLarge ? GOLD : LINE);

            var side = Panel(row.rectTransform, "side", C(WaveRoster.JobColor(h.job)));
            Place(side.rectTransform, 0, 0, 3, 56);

            var nm = Text(row.rectTransform, Nemesis.DisplayName(h), 13,
                h.state == Nemesis.State_Slain ? FAINT : TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(nm.rectTransform, 12, 6, rw - 120, 20);
            var st = Text(row.rectTransform, Nemesis.StateName(h.state), 11,
                h.state == Nemesis.State_AtLarge ? C("#e05a5a") : MUTED, TextAlignmentOptions.Right);
            Place(st.rectTransform, rw - 108, 8, 96, 16);

            var de = Text(row.rectTransform,
                AdventurerAI.RankLetter(Mathf.Clamp(h.rank + Nemesis.RankBonus(h), 0, 7)) + "級 "
                + WaveRoster.JobName(h.job) + " Lv" + h.level
                + "　逃走 " + h.escapes + "　恨み " + h.grudge
                + "　<color=#e3a94a>HP×" + Nemesis.HpMult(h).ToString("0.00") + " 攻×" + Nemesis.AtkMult(h).ToString("0.00") + "</color>",
                11, MUTED, TextAlignmentOptions.Left);
            Place(de.rectTransform, 12, 28, rw - 24, 18);

            // 伸びしろのバー（上限があることを見せる＝青天井ではないと分かる）
            var gbg = Panel(row.rectTransform, "gbg", C("#181528"));
            Place(gbg.rectTransform, 12, 48, rw - 24, 4);
            var gfg = Panel(gbg.rectTransform, "gfg", GOLD);
            Place(gfg.rectTransform, 0, 0, Mathf.Max(2f, (rw - 24) * Nemesis.GrowthFrac(h)), 4);

            shown++;
        }
        return y + ((shown + 1) / 2) * 62 + 6;
    }
}
