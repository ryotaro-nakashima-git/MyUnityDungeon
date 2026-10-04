using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 📜 **波の決算**（③）のUI。防衛戦が終わった直後、地上へ渡す前に1枚だけ見せる。
///
/// <para>
/// ⚠⚠ **これまでは無かった。** 波が終わると画面はそのまま地上へ切り替わり、
///   起きたことは右のトーストが数枚流れて消えるだけだった。＝**余韻がゼロ**。
///   何より、**押した手が効いたのかどうかが分からない**ので、次の波に持っていける学びが無い。
/// </para>
///
/// <para>
/// ⚠⚠ **報酬は1つも足さない。** ここは既に払われた物を数え直して見せるだけ
///   （→ [[WaveReport]] ／ [[difficulty-curve-orders]]）。
/// ⚠ 一番下の『この波で選んだ手』が本体。数字の羅列より、
///   **自分の選択が何をしたか**が見えることの方が効く（→ [[dopamine-plan-and-how-tree]]）。
/// ⚠ 行は**作り直さない**。使う数だけ最初に用意して出し入れする
///   （`Destroy` は遅延するので、同じフレームに作り直すと古い行が残る → [[ui-conventions]]）。
/// </para>
/// </summary>
public partial class GameUIManager
{
    private const float RPT_W = 780f, RPT_H = 580f;
    private const int RptCostMax = 4;    // 代償の行
    private const int RptChoiceMax = 5;  // 選んだ手の行

    private GameObject reportPanel;
    private RectTransform rptCard, rptChoiceHeader, rptGoBtn;
    private TextMeshProUGUI rptTitle, rptTime, rptVerdict, rptYield, rptSpend;
    private TextMeshProUGUI rptNextHead, rptNextA, rptNextB;   // 🛡️ 次の備え（W-2）
    private readonly List<TextMeshProUGUI> rptTileVal = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> rptCostRows = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> rptChoiceHead = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> rptChoiceBody = new List<TextMeshProUGUI>();
    private readonly int[] rptTileTarget = new int[4];
    private readonly string[] rptTileSuffix = new string[4];
    private float rptCount;              // 0→1 の数え上げ
    private bool reportHolding;

    private void BuildReportPanel(RectTransform root)
    {
        var wrap = Panel(root, "WaveReport", new Color(0f, 0f, 0f, 0.55f));
        reportPanel = wrap.gameObject;
        StretchFull(wrap.rectTransform);   // ⚠ 背景の暗幕はクリックを吸う（盤を触らせない）
        // 🖱️ **暗幕を押しても閉じる**（C-2）。⚠ 札(card)は自分でクリックを受け止めるので、
        //   札の上を押してもここには届かない ―― 「外側を押したら閉じる」がそのまま成立する。
        //   ⚠ 決算は**見せるだけ**の窓なので、外側で閉じても失うものが無い（取り返しがつく）。
        var wrapBtn = wrap.gameObject.AddComponent<Button>();
        wrapBtn.transition = Selectable.Transition.None;
        wrapBtn.onClick.AddListener(CloseReport);

        var card = Panel(wrap, "Card", PANEL);
        Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rptCard = card.rectTransform;
        card.rectTransform.sizeDelta = new Vector2(RPT_W, RPT_H);
        card.rectTransform.anchoredPosition = Vector2.zero;
        Outline(card, GOLD); SkinPanel(card);

        float pad = 26f, w = RPT_W - pad * 2;

        // ⚠⚠ **1行の枠は必ず「文字の大きさ×1.5」より高くする。**
        //   TMP は行の高さが枠に収まらないと、はみ出すのではなく**1文字も描かない**。
        //   実測：12.5pt を 18px の枠に入れたら（必要 18.11px）**丸ごと消えた**。
        //   見切れではないので、見た目には「その行だけ実装されていない」ように見える → [[ui-conventions]]。
        var eyebrow = Text(card, "防衛戦の決算", 11, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(eyebrow.rectTransform, pad, 16, 300, 16); eyebrow.characterSpacing = 8;
        rptTitle = Text(card, "", 24, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(rptTitle.rectTransform, pad, 32, 460, 36);
        rptTime = Text(card, "", 13, MUTED, TextAlignmentOptions.Right);
        Place(rptTime.rectTransform, RPT_W - pad - 300, 38, 300, 20);
        rptVerdict = Text(card, "", 13, C("#cbb684"), TextAlignmentOptions.Left);
        rptVerdict.enableWordWrapping = false;
        Place(rptVerdict.rectTransform, pad, 72, w, 20);

        // ── 大きい4つ（数え上がる）──
        string[] tileLab = { "倒した", "逃した", "捕えた", "最高連撃" };
        Color[] tileCol = { CRIMSON, C("#e08a3c"), VIOLET, GOLD };
        float tw = (w - 3 * 10f) / 4f;
        rptTileVal.Clear();
        for (int i = 0; i < 4; i++)
        {
            var tile = Panel(card, "Tile" + i, CARD);
            Place(tile.rectTransform, pad + i * (tw + 10f), 100, tw, 82);
            Outline(tile, LINE);
            var lab = Text(tile, tileLab[i], 10.5f, FAINT, TextAlignmentOptions.Center);
            Place(lab.rectTransform, 0, 10, tw, 16);
            var val = Text(tile, "0", 30, tileCol[i], TextAlignmentOptions.Center, FontStyles.Bold);
            Place(val.rectTransform, 0, 28, tw, 46);
            rptTileVal.Add(val);
        }

        // ── 実り ──
        var yh = Text(card, "実り", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(yh.rectTransform, pad, 194, 200, 16);
        rptYield = Text(card, "", 14.5f, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        rptYield.enableWordWrapping = false;
        Place(rptYield.rectTransform, pad, 212, w, 22);
        rptSpend = Text(card, "", 12.5f, MUTED, TextAlignmentOptions.Left);
        rptSpend.enableWordWrapping = false;
        Place(rptSpend.rectTransform, pad, 236, w, 20);

        // ── 代償 ──
        var ch = Text(card, "代償", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(ch.rectTransform, pad, 266, 200, 16);
        rptCostRows.Clear();
        for (int i = 0; i < RptCostMax; i++)
        {
            var t = Text(card, "", 12.5f, C("#e6a0a0"), TextAlignmentOptions.Left);
            t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
            Place(t.rectTransform, pad + 8, 286 + i * 21, w - 8, 22);   // ⚠ 22px（12.5pt には 18px では足りない）
            rptCostRows.Add(t);
        }

        // ── この波で選んだ手（ここが本体）──
        var hh = Text(card, "この波で選んだ手", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(hh.rectTransform, pad, 380, 300, 16);
        rptChoiceHeader = hh.rectTransform;
        rptChoiceHead.Clear(); rptChoiceBody.Clear();
        for (int i = 0; i < RptChoiceMax; i++)
        {
            var head = Text(card, "", 12.5f, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
            head.enableWordWrapping = false;
            Place(head.rectTransform, pad + 8, 400 + i * 21, 100, 21);
            var body = Text(card, "", 12, MUTED, TextAlignmentOptions.Left);
            body.enableWordWrapping = false; body.overflowMode = TextOverflowModes.Ellipsis;
            Place(body.rectTransform, pad + 112, 400 + i * 21, w - 120, 21);
            rptChoiceHead.Add(head); rptChoiceBody.Add(body);
        }

        BuildReportGrowth(card.rectTransform);   // 🌱 H3「配下の活躍」（位置は FillReport で詰める）

        // ── 🛡️ 次の備え（W-2）──
        // ⚠⚠ **決算の最後に置く。** ここは「何が起きたか」の締めであると同時に、
        //   プレイヤーが次の準備フェーズへ持っていく**唯一の持ち帰り**になる場所。
        //   ⚠ 位置は `FillReport` で中身に合わせて詰め直す（固定の座標では選んだ手の行数に負ける）。
        rptNextHead = Text(card, "次の備え", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(rptNextHead.rectTransform, pad, 480, 300, 16);
        rptNextA = Text(card, "", 12.5f, TEXT, TextAlignmentOptions.Left);
        rptNextA.enableWordWrapping = false; rptNextA.overflowMode = TextOverflowModes.Ellipsis;
        Place(rptNextA.rectTransform, pad + 8, 500, w - 8, 22);
        rptNextB = Text(card, "", 11.5f, MUTED, TextAlignmentOptions.Left);
        rptNextB.enableWordWrapping = false; rptNextB.overflowMode = TextOverflowModes.Ellipsis;
        Place(rptNextB.rectTransform, pad + 8, 521, w - 8, 21);

        var go = PrimaryButton(card, "地上へ ▶", BLOOD, TEXT, CloseReport, true);
        rptGoBtn = (RectTransform)go.transform;
        Place(rptGoBtn, RPT_W - pad - 200, RPT_H - 58, 200, 38);
        AddTooltip(((RectTransform)go.transform).gameObject, "後半（地上）へ進みます　<color=#9c95b4>[Space]</color>");

        reportPanel.SetActive(false);
    }

    /// <summary>
    /// 📜 **決算を見せてから画面を渡す**（`DungeonTurnManager.EnterSurfacePhase` から呼ぶ）。
    /// ⚠ 勝敗が決していれば出さない（リザルトの前に別の窓を挟まない）。
    /// </summary>
    public void OnPhaseChangedAfterReport()
    {
        if (reportPanel == null || !WaveReport.Ready || VictorySystem.Decided || !GameSetup.Started)
        { WaveReport.Consume(); OnPhaseChanged(); return; }
        FillReport();
        reportHolding = true;
        rptCount = 0f;
        reportPanel.SetActive(true);
        reportPanel.transform.SetAsLastSibling();
        PlayFadeIn(reportPanel);
        SoundSystem.Play(SoundSystem.Sfx.Turn, 0.7f);
    }

    /// <summary>▶ 決算を閉じて地上へ。⚠ ここが唯一の出口（閉じ忘れると画面が迷宮のまま止まる）。</summary>
    public void CloseReport()
    {
        if (!reportHolding) return;
        reportHolding = false;
        WaveReport.Consume();
        if (reportPanel != null) reportPanel.SetActive(false);
        OnPhaseChanged();
    }

    /// <summary>決算が出ているか（ホットキーの行き先を横取りするため）。</summary>
    public bool ReportOpen { get { return reportHolding; } }

    private void FillReport()
    {
        int turnNo = WaveReport.Turn;
        SetTxt(rptTitle, "第 " + turnNo + " 波 ― " + (WaveReport.Flawless ? "<color=#5cc47c>無傷で凌いだ</color>" : "凌いだ"));
        int mm = (int)(WaveReport.Seconds / 60f), ss = (int)(WaveReport.Seconds % 60f);
        SetTxt(rptTime, "来襲 " + WaveReport.Came + " 名　決着まで " + mm + ":" + ss.ToString("00")
            + "　最深 B" + WaveReport.DeepestFloor + "F");
        SetTxt(rptVerdict, WaveReport.Verdict());

        rptTileTarget[0] = WaveReport.Killed; rptTileSuffix[0] = "";
        rptTileTarget[1] = WaveReport.Escaped; rptTileSuffix[1] = "";
        rptTileTarget[2] = WaveReport.Captured; rptTileSuffix[2] = "";
        rptTileTarget[3] = WaveReport.BestCombo; rptTileSuffix[3] = WaveReport.BestCombo > 0 ? " <size=55%>連</size>" : "";

        // 実り
        var y = new System.Text.StringBuilder();
        y.Append("<color=#e3a94a>+" + UITheme.Num(WaveReport.DpGained) + " DP</color>");
        if (WaveReport.MatGained > 0) y.Append("　<color=#57c3ab>+" + WaveReport.MatGained + " 素材</color>");
        if (WaveReport.EmotionGained > 0) y.Append("　<color=#c04a6a>+" + WaveReport.EmotionGained + " 感情</color>");
        if (WaveReport.RpGained > 0) y.Append("　<color=#8cb8e6>+" + WaveReport.RpGained + " 研究点</color>");
        if (WaveReport.FameGained > 0) y.Append("　<color=#e05a5a>+" + WaveReport.FameGained + " 名声</color>");
        SetTxt(rptYield, y.ToString());
        // ⚠ 号令を撃った波だけ収支を出す。撃っていない波に「差引」を出すと、
        //   何も失っていないのに損をしたように読める。
        SetTxt(rptSpend, WaveReport.DpSpent > 0
            ? "号令に <color=#e05a5a>-" + UITheme.Num(WaveReport.DpSpent) + " DP</color>　→　差引 <b>"
              + (WaveReport.DpNet >= 0 ? "<color=#5cc47c>+" : "<color=#e05a5a>")
              + UITheme.Num(WaveReport.DpNet) + " DP</color></b>"
            : "");

        // 代償
        var costs = new List<string>();
        if (WaveReport.GearLooted > 0)
            costs.Add("戦利品 <b>" + WaveReport.GearLooted + "</b> を持ち逃げされた（世界の装備水準 "
                + LureEconomy.GradeText(WaveReport.GearBefore) + " → <b>" + LureEconomy.GradeText(WaveReport.GearAfter) + "</b>）");
        if (WaveReport.DefendersLost > 0)
            costs.Add("防衛体を <b>" + WaveReport.DefendersLost + "</b> 失った");
        if (WaveReport.LordHpAfter < WaveReport.LordHpBefore - 0.001f)
            costs.Add("魔王に届かれた（HP " + Mathf.RoundToInt(WaveReport.LordHpBefore * 100f) + "% → <b>"
                + Mathf.RoundToInt(WaveReport.LordHpAfter * 100f) + "%</b>）");
        // ⚠ 表示（0.1刻み）が変わったときだけ書く。「等級0.2 → 等級0.2 に下がった」が出ていた
        if (WaveReport.GearAfter < WaveReport.GearBefore
            && LureEconomy.GradeText(WaveReport.GearAfter) != LureEconomy.GradeText(WaveReport.GearBefore))
            costs.Add("<color=#5cc47c>奪還した ― 世界の装備水準が " + LureEconomy.GradeText(WaveReport.GearBefore)
                + " → <b>" + LureEconomy.GradeText(WaveReport.GearAfter) + "</b> に下がった</color>");
        if (costs.Count == 0) costs.Add("<color=#5cc47c>失った物は無い</color>");
        for (int i = 0; i < rptCostRows.Count; i++)
        {
            bool on = i < costs.Count;
            rptCostRows[i].gameObject.SetActive(on);
            if (on) SetTxt(rptCostRows[i], "・" + costs[i]);
        }

        // ⚠ **中身に合わせて詰める。** 行数は波ごとに変わるので、固定の位置だと
        //   代償が1行しか無い波で下に大きな空白が空き、ボタンだけが遠くに浮いて「作りかけ」に見える。
        int shown = 0;
        for (int i = 0; i < rptCostRows.Count; i++) if (rptCostRows[i].gameObject.activeSelf) shown++;
        float nextY = 286f + shown * 21f + 12f;
        if (rptChoiceHeader != null) Place(rptChoiceHeader, 26f, nextY, 300, 16);

        // 選んだ手
        var ch = WaveReport.Choices;
        float rowY = nextY + 20f;
        for (int i = 0; i < rptChoiceHead.Count; i++)
        {
            bool on = i < ch.Count;
            rptChoiceHead[i].gameObject.SetActive(on);
            rptChoiceBody[i].gameObject.SetActive(on);
            if (!on) continue;
            Place(rptChoiceHead[i].rectTransform, 34f, rowY + i * 21f, 100, 21);
            Place(rptChoiceBody[i].rectTransform, 138f, rowY + i * 21f, RPT_W - 52f - 120f, 21);
            SetTxt(rptChoiceHead[i], "◆ " + ch[i].Key);
            SetTxt(rptChoiceBody[i], ch[i].Value);
        }
        // ⚠ 1つも押していない波は**空欄にせず、そう書く**。空の見出しは「壊れている」に見える。
        if (ch.Count == 0 && rptChoiceHead.Count > 0)
        {
            rptChoiceHead[0].gameObject.SetActive(true);
            rptChoiceBody[0].gameObject.SetActive(true);
            Place(rptChoiceHead[0].rectTransform, 34f, rowY, 100, 21);
            Place(rptChoiceBody[0].rectTransform, 138f, rowY, RPT_W - 52f - 120f, 21);
            SetTxt(rptChoiceHead[0], "―");
            SetTxt(rptChoiceBody[0], "この波では何も選ばなかった（大招集・泳がせ・流言・備え・号令）");
        }

        // 🛡️ 次の備え（W-2）― 選んだ手の下に詰める
        float nx = rowY + Mathf.Max(1, ch.Count) * 21f + 12f;
        nx = FillReportGrowth(nx);   // 🌱 H3：配下の活躍（動いた個体が居なければ何も足さない）
        if (rptNextHead != null) Place(rptNextHead.rectTransform, 26f, nx, 300, 16);
        if (rptNextA != null)
        {
            Place(rptNextA.rectTransform, 34f, nx + 19f, RPT_W - 52f - 8f, 22);
            // ⚠⚠ **次の名簿で判定する。** 大招集の人数（ForecastCount）で判定していたので、
            //   大招集を切らない・休み中のターンにも毎回「危ない・約2.5倍が来る」と出ていた（通しプレイで発見）。
            //   大招集を切った場合の人数は、下の行（rptNextB）が別に書いている。
            SetTxt(rptNextA, FeverSystem.ReadinessLine(WaveRoster.Count));
        }
        if (rptNextB != null)
        {
            Place(rptNextB.rectTransform, 34f, nx + 41f, RPT_W - 52f - 8f, 21);
            // ⚠ 休み中に「切れば」と書くと押せない手を勧めることになる（4回やった失敗）。休みは休みと書く。
            var t0 = DungeonTurnManager.Instance;
            int rest = t0 != null ? FeverSystem.ReadyTurn - t0.CurrentTurn : 0;
            SetTxt(rptNextB, "この波は <b>" + WaveReport.Came + "</b> 体を <b>" + WaveReport.Killed
                + "</b> 体倒して凌いだ。次の名簿は <b>" + WaveRoster.Count + "</b> 体、"
                + (rest > 0
                    ? "<color=#e3a94a>◆ 大招集</color>は あと <b>" + rest + "</b> ターン休み。"
                    : "<color=#e3a94a>◆ 大招集</color>を切れば およそ <b>" + FeverSystem.ForecastCount + "</b> 体になる。"));
        }

        // ボタンとカードの高さを、最後の行の下に合わせる
        float lastY = nx + 41f + 21f + 12f;
        if (rptGoBtn != null) Place(rptGoBtn, RPT_W - 26f - 200f, lastY, 200, 38);
        if (rptCard != null) rptCard.sizeDelta = new Vector2(RPT_W, lastY + 38f + 24f);
    }

    /// <summary>📜 数え上がり。⚠ `unscaledDeltaTime`（決算は戦闘の外＝倍速に引っ張られない）。</summary>
    private void TickReport()
    {
        if (!reportHolding || reportPanel == null || !reportPanel.activeSelf) return;
        if (rptCount < 1f)
        {
            rptCount = Mathf.Min(1f, rptCount + Time.unscaledDeltaTime / 0.55f);
            float e = 1f - (1f - rptCount) * (1f - rptCount);   // 後半ゆっくり
            for (int i = 0; i < rptTileVal.Count; i++)
                SetTxt(rptTileVal[i], Mathf.RoundToInt(rptTileTarget[i] * e) + rptTileSuffix[i]);
        }
    }
}
