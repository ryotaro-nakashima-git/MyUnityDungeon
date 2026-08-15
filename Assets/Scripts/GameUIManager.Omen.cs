using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🔭 **先触れ**（次の波の名簿）と 🛡️ **備え**（その波への一手）の画面。
/// <para>`GameUIManager` の partial。データは <see cref="WaveRoster"/> / <see cref="WardSystem"/>。</para>
/// <para>
/// ⚠ ここは「読む」と「打つ」を**同じ画面**に置く。別々にすると、読んだ内容を覚えたまま
///   別の画面へ行って選ぶことになり、せっかくの情報が判断に繋がらない。
/// </para>
/// </summary>
public partial class GameUIManager
{
    private const float OMEN_W = 1180f;

    private void BuildOmenPanel(RectTransform root)
    {
        var panel = Panel(root, "OmenPanel", PANEL);
        omenPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(OMEN_W, 760);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        Outline(panel, LINE2); SkinPanel(panel);

        // ⚠ 画面に出す文字列に絵文字を書かない（□になるか丸ごと落ちる → [[ui-conventions]]）
        var title = Text(panel, "先触れ ― 次に来る波", 17, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, 24, 16, OMEN_W - 90, 26);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => omenPanel.SetActive(false));
        Place((RectTransform)close.transform, OMEN_W - 56, 14, 32, 30);

        // ⚠ 中身は毎回まるごと作り直すので、**専用の入れ物**に入れる（パネル直下だとカードが増え続ける）。
        omenBody = MakeVScroll(panel, 24, 52, OMEN_W - 48, 760 - 52 - 24);
        omenPanel.SetActive(false);
    }

    private void OpenOmen()
    {
        if (omenPanel == null) return;
        OpenExclusive(null);
        RefreshOmenPanel();
        omenPanel.SetActive(true);
        omenPanel.transform.SetAsLastSibling();
        PlayFadeIn(omenPanel);
    }

    private void RefreshOmenPanel()
    {
        if (omenBody == null) return;
        for (int i = omenBody.childCount - 1; i >= 0; i--) Destroy(omenBody.GetChild(i).gameObject);

        int turn = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 1;
        WaveRoster.EnsureRolled(turn);

        // ⚠⚠ `MakeVScroll` の Content は**横ストレッチ**（幅＝ビューポート幅）。
        //   `sizeDelta.x` は**加算ぶん**なので、ここに実幅を入れると幅が2倍になり、
        //   pivotが中央なので**左右に半分ずつはみ出して見切れる**（実際にそうなった）。
        //   → 中身を置く幅はビューポート幅そのもの、`sizeDelta.x` は 0。→ [[ui-conventions]]
        float w = OMEN_W - 48;
        float y = 0;
        int lv = WaveRoster.ScoutLevel;

        // ── 読みの深さ ──
        var eye = Text(omenBody, "読みの深さ：<b>" + WaveRoster.ScoutLevelName[Mathf.Clamp(lv, 0, 4)] + "</b>"
            + (lv < 4 ? "　<color=#9c95b4>領域研究『耳を澄ます → 斥候の目 → 魔力の読み → 看破』で深くなる</color>" : ""),
            12, FAINT, TextAlignmentOptions.Left);
        Place(eye.rectTransform, 0, y, w, 18); y += 24;

        // ── 要約カード ──
        var head = Panel(omenBody, "Head", CARD);
        Place(head.rectTransform, 0, y, w, 96); Outline(head, LINE2);
        var cnt = Text(head.rectTransform, WaveRoster.CountText, 30, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(cnt.rectTransform, 18, 12, 260, 40);
        var sub = Text(head.rectTransform,
            lv >= 1 ? "最高 <color=#e05a5a>" + AdventurerAI.RankLetter(WaveRoster.MaxRank) + "級</color>　平均 Lv" + WaveRoster.AvgLevel
                    : "数も強さも、まだ霧の向こうだ",
            13, MUTED, TextAlignmentOptions.Left);
        Place(sub.rectTransform, 18, 56, 300, 22);
        var read = Text(head.rectTransform, WaveRoster.Reading(), 13, TEXT, TextAlignmentOptions.TopLeft);
        Place(read.rectTransform, 320, 14, w - 340, 72);
        y += 104;

        // ── 職の内訳（Lv2〜）──
        if (lv >= 2)
        {
            var jh = Text(omenBody, "編成", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(jh.rectTransform, 0, y, w, 16); y += 20;
            var jc = WaveRoster.JobCounts();
            float cw = (w - 3 * 10) / 4f;
            for (int i = 0; i < 4; i++)
            {
                var jb = (AdventurerAI.Job)i;
                Color jcol = C(WaveRoster.JobColor(jb));
                bool many = WaveRoster.Many(jc, jb, WaveRoster.Count);
                var card = Panel(omenBody, "Job" + i, many ? SEL : CARD);
                Place(card.rectTransform, i * (cw + 10), y, cw, 58); Outline(card, many ? jcol : LINE);
                var nm = Text(card.rectTransform, WaveRoster.JobName(jb), 12.5f, jcol, TextAlignmentOptions.Left, FontStyles.Bold);
                Place(nm.rectTransform, 12, 8, cw - 24, 18);
                var vt = Text(card.rectTransform, jc[i] + " 体", 18, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
                Place(vt.rectTransform, 12, 28, cw - 24, 24);
                // 割合バー（数えた結果をそのまま長さにする）
                float ratio = WaveRoster.Count > 0 ? (float)jc[i] / WaveRoster.Count : 0f;
                var bar = Panel(card.rectTransform, "bar", jcol);
                Place(bar.rectTransform, 0, 55, Mathf.Max(2f, cw * ratio), 3);
            }
            y += 66;
            var pu = Text(omenBody, "目的：踏破 " + WaveRoster.ConquerCount + " 体 ／ 探索 "
                + (WaveRoster.Count - WaveRoster.ConquerCount) + " 体　<color=#9c95b4>踏破目的はまっすぐ最下層へ来る</color>",
                12, MUTED, TextAlignmentOptions.Left);
            Place(pu.rectTransform, 2, y, w, 18); y += 26;
        }

        // ── 属性（Lv3〜）──
        if (lv >= 3)
        {
            var mh = Text(omenBody, "魔力", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(mh.rectTransform, 0, y, w, 16); y += 20;
            var box = Panel(omenBody, "Elem", C("#181528"));
            Place(box.rectTransform, 0, y, w, 56); Outline(box, LINE);
            var bar2 = Panel(box.rectTransform, "b", VIOLET); Place(bar2.rectTransform, 0, 0, 3, 56);
            var et = Text(box.rectTransform, "向こうが持ち込む属性： " + WaveRoster.ElementText()
                + "　（術者 " + WaveRoster.CasterCount + " 体）", 12.5f, TEXT, TextAlignmentOptions.Left);
            Place(et.rectTransform, 14, 8, w - 26, 20);
            string adv = WaveRoster.BestElementAdvice();
            var at = Text(box.rectTransform, string.IsNullOrEmpty(adv) ? "こちらに使える属性がまだ無い（魔法研究）" : "こちらの一手： <b>" + adv + "</b>",
                12.5f, GREEN, TextAlignmentOptions.Left);
            Place(at.rectTransform, 14, 30, w - 26, 20);
            y += 64;
        }

        // ── 🛡️ 備え ──
        var wh = Text(omenBody, "備え　<color=#9c95b4>このターンだけ効く一手。1つだけ張れる（張り替えは全額戻る）</color>",
            11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(wh.rectTransform, 0, y, w, 16); y += 20;
        if (!WardSystem.Unlocked)
        {
            var lk = Panel(omenBody, "WardLock", CARD);
            Place(lk.rectTransform, 0, y, w, 44); Outline(lk, LINE);
            var lt = Text(lk.rectTransform, "未解禁 - 領域研究『備えの心得』を取ると、読んだ結果に対して手が打てるようになる。",
                12.5f, MUTED, TextAlignmentOptions.Left);
            Place(lt.rectTransform, 14, 12, w - 26, 20);
            y += 52;
        }
        else
        {
            float ww = (w - 2 * 10) / 3f;
            for (int i = 0; i < WardSystem.Count; i++)
            {
                int idx = i; var d = WardSystem.Get(i);
                bool on = WardSystem.Selected == i;
                var card = Panel(omenBody, "Ward" + i, on ? SEL : CARD);
                Place(card.rectTransform, (i % 3) * (ww + 10), y + (i / 3) * (110 + 10), ww, 110);
                Outline(card, on ? GOLD : LINE);
                var bt = card.gameObject.AddComponent<Button>(); bt.targetGraphic = card;
                bt.onClick.AddListener(() =>
                {
                    string why;
                    if (!WardSystem.TrySelect(idx, out why))
                    {
                        NotifySystem.Push("備えを張れない：" + why, NotifySystem.Kind.Loss);
                        SoundSystem.Play(SoundSystem.Sfx.Error);
                    }
                    RefreshOmenPanel();
                });
                // 左端の色帯で見分ける（絵文字は使えない）
                var side = Panel(card.rectTransform, "side", C(d.colorHex));
                Place(side.rectTransform, 0, 0, 3, 110);
                var nm = Text(card.rectTransform, d.jpName, 14, on ? GOLD : TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
                Place(nm.rectTransform, 12, 8, ww - 24, 20);
                var ag = Text(card.rectTransform, "対： " + d.against, 11, C("#8cb8e6"), TextAlignmentOptions.Left);
                Place(ag.rectTransform, 12, 28, ww - 24, 16);
                var ds = Text(card.rectTransform, d.desc, 11.5f, MUTED, TextAlignmentOptions.TopLeft);
                Place(ds.rectTransform, 12, 46, ww - 24, 40);
                var cs = Text(card.rectTransform, on ? "張っている（押すと剥がす）" : d.costDp + " DP",
                    12, on ? GREEN : C("#e3a94a"), TextAlignmentOptions.Left, FontStyles.Bold);
                Place(cs.rectTransform, 12, 86, ww - 24, 18);
            }
            int rows = (WardSystem.Count + 2) / 3;
            y += rows * (110 + 10) + 6;
        }

        // ── 名簿（Lv4）──
        if (lv >= 4 && WaveRoster.Count > 0)
        {
            var nh = Text(omenBody, "名簿（1人ずつ）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(nh.rectTransform, 0, y, w, 16); y += 20;
            var all = WaveRoster.All;
            float rw = (w - 10) / 2f;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                var row = Panel(omenBody, "Ros" + i, CARD);
                Place(row.rectTransform, (i % 2) * (rw + 10), y + (i / 2) * 30, rw, 26); Outline(row, LINE);
                var tx = Text(row.rectTransform,
                    "<color=#e05a5a>" + AdventurerAI.RankLetter(e.rank) + "級</color> "
                    + "<color=" + WaveRoster.JobColor(e.job) + ">" + WaveRoster.JobName(e.job) + "</color> Lv" + e.level
                    + "　<color=#9c95b4>" + (e.purpose == AdventurerAI.Purpose.Conquer ? "踏破" : "探索") + "</color>"
                    + (e.hasSpell ? "　<color=#b48ce6>" + e.spell.jpName + "</color>" : ""),
                    11.5f, TEXT, TextAlignmentOptions.Left);
                Place(tx.rectTransform, 10, 4, rw - 20, 18);
            }
            y += ((all.Count + 1) / 2) * 30 + 6;
        }

        omenBody.sizeDelta = new Vector2(0f, y + 12);
    }
}
