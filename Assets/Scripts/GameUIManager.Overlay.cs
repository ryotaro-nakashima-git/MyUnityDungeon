using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 重なりもの一式：階層拡張・降下演出・リザルト・腹心の報告・号令バー・トースト・ログ・セーブ・設定・発見。
/// <para>`GameUIManager` の partial。フィールドの本体は GameUIManager.cs 側にある。</para>
/// </summary>
public partial class GameUIManager
{

    private void BuildExpandPanel(RectTransform root)
    {
        var panel = Panel(root, "ExpandPanel", PANEL);
        expandPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(720, 470);
        panel.rectTransform.anchoredPosition = new Vector2(0, 10);
        Outline(panel, LINE2); SkinPanel(panel);

        float pad = 22f, w = 720 - pad * 2;
        var title = Text(panel, "領域（広さ＝配置枠と名声／深さ＝報酬倍率）", 14.5f, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 14, w - 40, 22);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => expandPanel.SetActive(false));
        Place((RectTransform)close.transform, 720 - pad - 28, 12, 28, 26);
        var sub = Text(panel, "広げる＝その階に置ける要素が+4枠／名声が上がり客が増える。深くする＝その階の撃破報酬が上がる。", 11, MUTED, TextAlignmentOptions.Left);
        Place(sub.rectTransform, pad, 38, w, 16);
        domainSummaryText = Text(panel, "", 11.5f, C("#8cb8e6"), TextAlignmentOptions.Left, FontStyles.Bold);
        Place(domainSummaryText.rectTransform, pad, 56, w, 16);

        var cont = NewRect("Rows", panel.rectTransform);
        Place(cont, pad, 80, w, 470 - 80 - pad);
        expandRowsContainer = cont;

        RefreshExpandPanel();
        expandPanel.SetActive(false);
    }

    private void RefreshExpandPanel()
    {
        if (expandRowsContainer == null || floorMgr == null) return;
        for (int i = expandRowsContainer.childCount - 1; i >= 0; i--)
        {
            var c = expandRowsContainer.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        bool prep = turn == null || turn.IsPreparePhase;
        int n = floorMgr.BuiltFloorCount;
        if (domainSummaryText != null)
            domainSummaryText.text = "名声 " + floorMgr.DomainRenown + "（拡張 " + floorMgr.ExpandedRenown + "段）"
                + " → ウェーブ増員 +" + DungeonFloorManager.RenownBonusAdventurers
                + "・冒険者ランク +" + DungeonFloorManager.RenownHeroRankBias.ToString("0.00")
                + "　<color=#9c95b4>広く深いほど強い客が来る＝旨いが危険</color>";
        float rowH = 52f, y = 0f, w = expandRowsContainer.rect.width;
        if (n == 0)
        {
            var none = Text(expandRowsContainer, "<color=#9c95b4>まず迷宮を生成してください。</color>", 12, MUTED, TextAlignmentOptions.Left);
            Place(none.rectTransform, 0, 4, w, 18);
            return;
        }
        for (int i = 0; i < n; i++)
        {
            int fi = i;
            var row = Panel(expandRowsContainer, "ExRow_" + i, CARD);
            Place(row.rectTransform, 0, y, w, rowH - 6); Outline(row, LINE);
            int size = floorMgr.FloorSize(i);
            bool deepest = floorMgr.IsDeepest(i);
            var nm = Text(row.rectTransform, "B" + (i + 1) + "F" + (deepest ? " 魔" : "") + "  <size=112%>" + size + "×" + size + "</size>", 13, deepest ? CRIMSON : TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm.rectTransform, 12, 6, 170, 20);
            // 🏛️ この階が今いくらの器と報酬を持っているか
            var gain = Text(row.rectTransform,
                "<color=#57c3ab>配置枠 " + floorMgr.PlacementCap(i) + "</color>　<color=#e3a94a>報酬 ×" + floorMgr.DepthRewardMult(i).ToString("0.00") + "</color>",
                10.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(gain.rectTransform, 12, 26, 200, 16);
            if (floorMgr.CanExpandFloor(i))
            {
                int ns = floorMgr.NextFloorSize(i), rp = floorMgr.ExpandRPCost(i), dp = floorMgr.ExpandDPCost(i);
                var info = Text(row.rectTransform,
                    "→ " + ns + "×" + ns + " <color=#5cc47c>(枠+4)</color>    <color=#8cb8e6>" + rp + " RP</color>  <color=#e3a94a>" + dp + " DP</color>",
                    12, MUTED, TextAlignmentOptions.Left);
                Place(info.rectTransform, 216, 13, w - 326, 20);
                var btn = PrimaryButton(row, "拡張", BLOOD, TEXT, () => { if (floorMgr.TryExpandFloor(fi)) { RefreshExpandPanel(); RefreshFloorTabs(); } }, true);
                Place((RectTransform)btn.transform, w - 98, 8, 86, 30);
                btn.interactable = prep && ResearchState.RP >= rp && (res == null || res.DungeonPoints >= dp);
            }
            else
            {
                var mx = Text(row.rectTransform, "<color=#5cc47c>最大 (50×50)</color>", 12, GREEN, TextAlignmentOptions.Left);
                Place(mx.rectTransform, 216, 15, 200, 16);
            }
            y += rowH;
        }

        // 🏢 縦拡張（階層追加）行：準備中のみ・削除不可・4層以降は領域研究(d_floor4/5)ゲート
        if (n < 5)
        {
            var addRow = Panel(expandRowsContainer, "AddFloorRow", CARD);
            Place(addRow.rectTransform, 0, y, w, rowH - 6); Outline(addRow, BLOOD_DK);
            bool can = floorMgr.CanAddFloor();
            int cost = floorMgr.AddFloorDPCost();
            string need = floorMgr.AddFloorResearchNeeded();
            var nm2 = Text(addRow.rectTransform, "＋ 第" + (n + 1) + "層を追加（最下層に）", 13, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm2.rectTransform, 12, 13, 220, 20);
            string info = can ? ("<color=#e3a94a>" + cost + " DP</color>")
                : (need != "" && ResearchCatalog.TryGet(need, out var rn) ? "<color=#8cb8e6>🔬 研究『" + rn.jpName + "』が必要</color>" : "—");
            var inf = Text(addRow.rectTransform, info, 12, MUTED, TextAlignmentOptions.Left);
            Place(inf.rectTransform, 248, 13, w - 350, 20);
            var abtn = PrimaryButton(addRow, "追加", BLOOD, TEXT, () => { if (floorMgr.TryAddFloor()) { RefreshExpandPanel(); RefreshFloorTabs(); } }, true);
            Place((RectTransform)abtn.transform, w - 98, 8, 86, 30);
            abtn.interactable = prep && can && (res == null || res.DungeonPoints >= cost);
        }
    }

    // ---------- descent演出（フェード＋降下トースト） ----------
    private void BuildDescentFX(RectTransform root)
    {
        // フロア切替フェード（全画面・黒・最前面）
        var fade = Panel(root, "FloorFade", Color.black);
        StretchFull(fade.rectTransform);
        floorFadeCg = fade.gameObject.AddComponent<CanvasGroup>();
        floorFadeCg.alpha = 0f; floorFadeCg.blocksRaycasts = false; floorFadeCg.interactable = false;
        fade.rectTransform.SetAsLastSibling();

        // 降下トースト（中央上寄りバナー）
        var toast = Panel(root, "DescentToast", C("#0e0b16"));
        Anchor(toast, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        toast.rectTransform.sizeDelta = new Vector2(540, 96);
        toast.rectTransform.anchoredPosition = new Vector2(0, 130);
        Outline(toast, GOLD);
        descentToastText = Text(toast, "", 30, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(descentToastText.rectTransform);
        descentToastCg = toast.gameObject.AddComponent<CanvasGroup>();
        descentToastCg.alpha = 0f; descentToastCg.blocksRaycasts = false; descentToastCg.interactable = false;
        toast.rectTransform.SetAsLastSibling();
    }

    /// <summary>降下トーストを表示（DungeonFloorManager.Descentから呼ばれる）。</summary>
    public void ShowDescentToast(string floorLabel, int survivors)
    {
        if (descentToastText == null) return;
        SetTxt(descentToastText, $"{floorLabel} へ降下！　<size=60%><color=#9c95b4>生存者 {survivors}</color></size>");
        descentToastTimer = 1.7f;
        if (descentToastCg != null) descentToastCg.alpha = 1f;
    }

    /// <summary>フロア切替の暗転フェードを再生。</summary>
    public void PlayFloorTransition()
    {
        floorFadeTimer = FADE_DUR;
        if (floorFadeCg != null) floorFadeCg.alpha = 1f;
    }

    // ================= 🏁 リザルト（Phase F-23） =================
    //  ⚠ 以前はここが `GAME OVER` の4文字だけで、**ボタンが1つも無かった**（＝終わったら何もできない）。
    //     何をどこまでやったのかを残し、次の周へ送り出すのがこの画面の仕事。
    private RectTransform resultBody;

    /// <summary>
    /// ⚠ リザルトは**専用のCanvas**（order 330）に置く。以前は迷宮のCanvasに居たので、
    /// あとから開く『腹心の報告』が上に重なって**結果が読めなかった**
    /// （`SetAsLastSibling` は、その後で誰かが同じことをすれば負ける）。
    /// </summary>
    private void BuildGameOverOverlay(RectTransform ignored)
    {
        var root = MakeCanvas("ResultCanvas", 330);
        // 背景は**不透明**にする。半透明だと盤が透けて数字が読みにくく、区切りとしても弱い
        var panel = Panel(root, "GameOverPanel", C("#0b0910"));
        StretchFull(panel.rectTransform);
        resultBody = NewRect("ResultBody", panel.rectTransform);
        Anchor(resultBody, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        resultBody.sizeDelta = new Vector2(860, 640);
        resultBody.anchoredPosition = Vector2.zero;
        panel.gameObject.SetActive(false);
        gameOverPanel = panel.gameObject;
    }

    public void ShowGameOver() { ShowResult(false); }

    /// <summary>🏁 周の終わり。勝敗・スコア・記録・新しく解けた実績を出す。</summary>
    public void ShowResult(bool win)
    {
        if (gameOverPanel == null || resultBody == null) return;
        CloseGuide(); OpenExclusive(null);
        SetSurfaceMode(false);   // 🏁 リザルトの後ろに地上の盤を残さない
        if (logPanel != null) logPanel.SetActive(false);
        if (savePanel != null) savePanel.SetActive(false);
        int before = Achievements.UnlockedCount;
        RunStats.CommitRun(win);              // 通算に足し、実績を見る（1周に1度だけ通る）
        int gained = Achievements.UnlockedCount - before;

        for (int i = resultBody.childCount - 1; i >= 0; i--) Destroy(resultBody.GetChild(i).gameObject);
        float w = 860, y = 0;

        var eyebrow = Text(resultBody, GameSetup.DailySeed ? "DAILY  " + GameSetup.TodayLabel : Difficulty.CurrentName,
            13, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(eyebrow.rectTransform, 0, y, w, 20); eyebrow.characterSpacing = 8; y += 26;
        var t1 = Text(resultBody, win ? "<color=#e3a94a>世界は塗り替えられた</color>" : "<color=#b0202b>GAME OVER</color>",
            54, TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(t1.rectTransform, 0, y, w, 76); y += 82;
        var t2 = Text(resultBody, win ? "迷宮は地上を呑み込んだ。" : "魔王が討伐された。",
            18, MUTED, TextAlignmentOptions.Center);
        Place(t2.rectTransform, 0, y, w, 28); y += 40;

        // スコア
        int score = RunStats.FinalScore(win);
        var card = Panel(resultBody, "score", CARD);
        Place(card.rectTransform, 130, y, w - 260, 96); Outline(card, GOLD);
        var sc = Text(card.rectTransform, UITheme.Num(score), 46, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(sc.rectTransform, 0, 8, w - 260, 56);
        var br = Text(card.rectTransform, "素点 " + RunStats.BaseScore + "　×　難易度 " + Difficulty.ScoreMult.ToString("0.0")
            + "　×　早さ " + RunStats.PaceMult.ToString("0.00") + (win ? "　×　勝利 1.5" : ""),
            11.5f, FAINT, TextAlignmentOptions.Center);
        Place(br.rectTransform, 0, 66, w - 260, 20);
        y += 108;

        // 記録
        string[,] rows =
        {
            { "凌いだ波", RunStats.WavesSurvived.ToString() },
            { "倒した冒険者", RunStats.Kills.ToString() },
            { "逃がした数", RunStats.Escapes.ToString() },
            { "守り切った最深", "B" + Mathf.Max(1, RunStats.DeepestHeld) + "F" },
            { "最大版図", RunStats.PeakRegions + " タイル" },
            { "研究", ResearchState.ResearchedCount + " ノード" },
            { "眷属", KinRoster.All.Count + " 体" },
            { "到達ターン", RunStats.Turn.ToString() },
            { "稼いだDP", UITheme.Num(RunStats.DpEarned) },
            { "遊んだ時間", SaveSystem.PlayTimeText(SaveSystem.PlaySeconds) },
        };
        float colW = (w - 260) / 2f;
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            float rx = 130 + (i % 2) * colW, ry = y + (i / 2) * 26;
            var k = Text(resultBody, rows[i, 0], 12.5f, MUTED, TextAlignmentOptions.Left);
            Place(k.rectTransform, rx, ry, colW * 0.55f, 20);
            var v2 = Text(resultBody, rows[i, 1], 13, TEXT, TextAlignmentOptions.Right, FontStyles.Bold);
            Place(v2.rectTransform, rx + colW * 0.55f, ry, colW * 0.4f, 20);
        }
        y += (rows.GetLength(0) + 1) / 2 * 26 + 14;

        var best = Text(resultBody, "自己最高 <color=#e3a94a>" + UITheme.Num(RunStats.BestScore) + "</color>"
            + "　通算 " + RunStats.Runs + "周（勝利 " + RunStats.Wins + "）"
            + (gained > 0 ? "　<color=#b48be6>実績 +" + gained + "</color>" : ""),
            12.5f, FAINT, TextAlignmentOptions.Center);
        Place(best.rectTransform, 0, y, w, 20); y += 34;

        var again = PrimaryButton(resultBody, "もう一度", BLOOD, C("#f0d9a0"), () =>
        {
            gameOverPanel.SetActive(false);
            BackToTitle(); ShowTitlePage(1);
        }, true);
        Place((RectTransform)again.transform, w / 2 - 250, y, 240, 52);
        var toTitle = PrimaryButton(resultBody, "タイトルへ", PANEL2, TEXT, () =>
        {
            gameOverPanel.SetActive(false);
            BackToTitle();
        });
        Place((RectTransform)toTitle.transform, w / 2 + 10, y, 240, 52);
        y += 62;

        resultBody.sizeDelta = new Vector2(w, y);
        gameOverPanel.SetActive(true);
        gameOverPanel.transform.SetAsLastSibling();
        SoundSystem.PlayBgm(SoundSystem.Bgm.None);
        SoundSystem.Play(win ? SoundSystem.Sfx.Discover : SoundSystem.Sfx.Loss);
    }

    // ================= 📖 腹心の報告（ターン頭の物語ガイド） =================
    private void BuildGuidePanel(RectTransform root)
    {
        var panel = Panel(root, "GuidePanel", PANEL);
        guidePanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(GUIDE_W, 520);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        Outline(panel, LINE2); SkinPanel(panel);

        guideBody = NewRect("Body", panel.rectTransform);
        Place(guideBody, 26, 22, GUIDE_W - 52, 400);
        // ⚠ 操作行は**専用の入れ物**に入れて毎回まるごと作り直す。
        //    パネル直下に置くと、作り直すたびにボタンが増える。
        guideFooter = NewRect("Footer", panel.rectTransform);
        Place(guideFooter, 26, 440, GUIDE_W - 52, 44);
        panel.gameObject.SetActive(false);
    }

    private void OpenGuide()
    {
        if (guidePanel == null) return;
        OpenExclusive(null);                 // 他の全画面パネルは畳む
        RefreshGuidePanel();
        guidePanel.SetActive(true);
        guidePanel.transform.SetAsLastSibling();
        PlayFadeIn(guidePanel);
    }
    private void CloseGuide() { if (guidePanel != null) guidePanel.SetActive(false); }

    /// <summary>報告の中身を組み直す。開くときだけ呼ぶ（毎フレーム作り直すとボタンが死ぬ）。</summary>
    private void RefreshGuidePanel()
    {
        if (guideBody == null) return;
        for (int i = guideBody.childCount - 1; i >= 0; i--) Destroy(guideBody.GetChild(i).gameObject);

        var b = GuideSystem.Latest;
        if (b == null) return;
        float w = GUIDE_W - 52, y = 0;

        var eye = Text(guideBody, "第 " + b.turn + " ターン ・ 腹心の報告", 11, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(eye.rectTransform, 0, y, w, 16); eye.characterSpacing = 6; y += 20;
        var hd = Text(guideBody, b.headline, 24, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(hd.rectTransform, 0, y, w, 32); y += 38;
        var st = Text(guideBody, b.story, 14, MUTED, TextAlignmentOptions.TopLeft);
        Place(st.rectTransform, 0, y, w, 46); y += 56;

        // ⏪ 前ターンの結果（Phase A-2）。地上の解決は1フレームで終わるので、ここで初めて「見える」。
        if (b.results.Count > 0 || b.gainedDp != 0)
        {
            var rh = Text(guideBody, "前のターンに起きたこと", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(rh.rectTransform, 0, y, w, 16); y += 20;
            if (b.gainedDp != 0 || b.gainedMat != 0 || b.gainedRp != 0 || b.gainedFame != 0)
            {
                string inc = "";
                if (b.gainedDp != 0) inc += "<color=#e3a94a>DP " + (b.gainedDp > 0 ? "+" : "") + b.gainedDp.ToString("N0") + "</color>　";
                if (b.gainedMat != 0) inc += "<color=#57c3ab>素材 " + (b.gainedMat > 0 ? "+" : "") + b.gainedMat + "</color>　";
                if (b.gainedRp != 0) inc += "<color=#8cb8e6>研究点 " + (b.gainedRp > 0 ? "+" : "") + b.gainedRp + "</color>　";
                if (b.gainedFame != 0) inc += "<color=#e05a5a>名声 " + (b.gainedFame > 0 ? "+" : "") + b.gainedFame + "</color>";
                var it = Text(guideBody, inc, 13, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
                Place(it.rectTransform, 4, y, w - 8, 20); y += 24;
            }
            for (int i = 0; i < b.results.Count; i++)
            {
                var n = b.results[i];
                string col = NotifySystem.ColorOf(n.kind);
                var row = Panel(guideBody, "R" + i, CARD);
                Place(row.rectTransform, 0, y, w, 30); Outline(row, LINE);
                var bar2 = Panel(row.rectTransform, "bar", C(col)); Place(bar2.rectTransform, 0, 0, 3, 30);
                var tx = Text(row.rectTransform, n.text, 12, TEXT, TextAlignmentOptions.Left);
                Place(tx.rectTransform, 12, 5, w - 24, 20);
                if (n.regionId >= 0)
                {
                    int rid2 = n.regionId;
                    var bt = row.gameObject.AddComponent<Button>(); bt.targetGraphic = row;
                    bt.onClick.AddListener(() => { CloseGuide(); JumpToRegion(rid2); });
                }
                y += 34;
            }
            y += 8;
        }

        if (b.advices.Count > 0)
        {
            var ah = Text(guideBody, "進言", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(ah.rectTransform, 0, y, w, 16); y += 20;
            for (int i = 0; i < b.advices.Count; i++)
            {
                var a = b.advices[i];
                var card = Panel(guideBody, "Advice" + i, CARD);
                Place(card.rectTransform, 0, y, w, 56); Outline(card, LINE);
                var dot = Panel(card.rectTransform, "dot", GOLD); Place(dot.rectTransform, 12, 22, 8, 8);
                var tt = Text(card.rectTransform, a.title, 14, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
                Place(tt.rectTransform, 28, 8, w - 40, 20);
                var wy = Text(card.rectTransform, a.why, 11.5f, MUTED, TextAlignmentOptions.TopLeft);
                Place(wy.rectTransform, 28, 30, w - 40, 20);
                y += 62;
            }
        }

        for (int i = 0; i < b.lessons.Count; i++)
        {
            if (i == 0)
            {
                var lh = Text(guideBody, "覚えておくこと（この局面ではじめて意味を持つ仕組み）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
                Place(lh.rectTransform, 0, y, w, 16); y += 20;
            }
            var box = Panel(guideBody, "Lesson" + i, C("#181528"));
            Place(box.rectTransform, 0, y, w, 54); Outline(box, LINE);
            var bar = Panel(box.rectTransform, "bar", VIOLET); Place(bar.rectTransform, 0, 0, 3, 54);
            var tx = Text(box.rectTransform, b.lessons[i], 12, TEXT, TextAlignmentOptions.TopLeft);
            Place(tx.rectTransform, 14, 8, w - 26, 40);
            y += 60;
        }

        // 中身に合わせて窓の高さを決める（余白と操作行のぶんを足す）
        guideBody.sizeDelta = new Vector2(w, y);
        var prt = (RectTransform)guidePanel.transform;
        prt.sizeDelta = new Vector2(GUIDE_W, y + 22 + 78);

        for (int i = guideFooter.childCount - 1; i >= 0; i--) Destroy(guideFooter.GetChild(i).gameObject);
        Place(guideFooter, 26, y + 34, w, 44);
        var mute = PrimaryButton(guideFooter, GuideSystem.Enabled ? "今後は出さない" : "毎ターン出す", PANEL2, MUTED,
            () => { GuideSystem.Enabled = !GuideSystem.Enabled; RefreshGuidePanel(); });
        Place((RectTransform)mute.transform, 0, 0, 200, 44);
        var ok = PrimaryButton(guideFooter, "わかった", BLOOD, C("#f0d9a0"), CloseGuide, true);
        Place((RectTransform)ok.transform, w - 220, 0, 220, 44);
    }

    // ================= 📯 魔王の号令（Phase D） =================
    private void BuildCommandBar(RectTransform root)
    {
        var bar = Panel(root, "CommandBar", C("#0e0b16"));
        commandBar = bar.gameObject;
        Anchor(bar, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        float w = CommandSystem.Count * 150f + 16f;
        bar.rectTransform.sizeDelta = new Vector2(w, 74);
        bar.rectTransform.anchoredPosition = new Vector2(0, UITheme.BarH + 10f);
        Outline(bar, C("#6a2028")); SkinPanel(bar);

        cmdBtns.Clear(); cmdCdTexts.Clear();
        for (int i = 0; i < CommandSystem.Count; i++)
        {
            int ci = i; var d = CommandSystem.Get(i);
            var card = Panel(bar, "Cmd" + i, CARD);
            Place(card.rectTransform, 8 + i * 150f, 8, 142, 58);
            Outline(card, C(d.colorHex));
            var n1 = Text(card.rectTransform, d.jpName, 13, TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(n1.rectTransform, 4, 8, 134, 18);
            var n2 = Text(card.rectTransform, d.dp + " DP", 11, C(d.colorHex), TextAlignmentOptions.Center);
            Place(n2.rectTransform, 4, 28, 134, 14);
            var cd = Text(card.rectTransform, "", 11.5f, FAINT, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(cd.rectTransform, 4, 42, 134, 14);
            var bt = card.gameObject.AddComponent<Button>(); bt.targetGraphic = card;
            bt.onClick.AddListener(() => { CommandSystem.TryUse(ci); RefreshCommandBar(); });
            AddTooltip(card.gameObject, d.jpName + "\n" + d.desc + "\nDP" + d.dp + "／クールダウン " + d.cd + "秒");
            cmdBtns.Add(card); cmdCdTexts.Add(cd);
        }
        commandBar.SetActive(false);
    }

    /// <summary>号令の使用可否とクールダウンを反映（毎フレーム。ボタンは作り直さないので安全）。</summary>
    private void RefreshCommandBar()
    {
        for (int i = 0; i < cmdBtns.Count; i++)
        {
            float left = CommandSystem.CooldownLeft(i);
            string why; bool ok = CommandSystem.CanUse(i, out why);
            cmdBtns[i].color = ok ? CardHiOrCard(true) : CardHiOrCard(false);
            var o = cmdBtns[i].GetComponent<Outline>();
            if (o != null) o.effectColor = ok ? C(CommandSystem.Get(i).colorHex) : LINE;
            SetTxt(cmdCdTexts[i], left > 0f ? Mathf.CeilToInt(left) + " 秒" : (ok ? "<color=#5cc47c>使える</color>" : "<color=#6f6889>" + why + "</color>"));
        }
    }
    private Color CardHiOrCard(bool hi) { return hi ? UITheme.CardHi : UITheme.Card; }

    // ================= 🔔 通知トーストとログ（Phase A） =================
    private void BuildToasts(RectTransform root)
    {
        toastRoot = NewRect("Toasts", root);
        // 右上・上部HUDの下から下へ積む
        toastRoot.anchorMin = new Vector2(1, 1); toastRoot.anchorMax = new Vector2(1, 1); toastRoot.pivot = new Vector2(1, 1);
        toastRoot.anchoredPosition = new Vector2(-16, -72);
        toastRoot.sizeDelta = new Vector2(TOAST_W, 400);
    }

    /// <summary>トーストを並べ直す。⚠ 変化したときだけ（毎フレーム作り直すと押下中にButtonが死ぬ）。</summary>
    private void RefreshToasts()
    {
        if (toastRoot == null) return;
        for (int i = toastRoot.childCount - 1; i >= 0; i--) Destroy(toastRoot.GetChild(i).gameObject);
        var list = NotifySystem.Toasts;
        float y = 0;
        for (int i = list.Count - 1; i >= 0; i--)   // 新しいものが上
        {
            var n = list[i];
            string col = NotifySystem.ColorOf(n.kind);
            var card = Panel(toastRoot, "T" + i, C("#14111e"));
            Place(card.rectTransform, 0, y, TOAST_W, 44);
            Outline(card, C(col));
            var bar = Panel(card.rectTransform, "bar", C(col));
            Place(bar.rectTransform, 0, 0, 4, 44);
            var tx = Text(card.rectTransform, n.text, 12.5f, TEXT, TextAlignmentOptions.Left);
            Place(tx.rectTransform, 14, 6, TOAST_W - 26, 32);
            var cg = card.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = Mathf.Clamp01(n.life / 1.2f);            // 消える直前だけ薄く
            if (n.regionId >= 0)
            {
                int rid = n.regionId;
                var bt = card.gameObject.AddComponent<Button>(); bt.targetGraphic = card;
                bt.onClick.AddListener(() => JumpToRegion(rid));   // 🔔 押すとその場所へ飛ぶ
            }
            y += 48;
        }
    }

    private void RefreshSpeedBtns()
    {
        int cur = turn != null ? turn.SpeedIndex : 1;
        for (int i = 0; i < speedBtns.Count; i++) SetSel(speedBtns[i], i == cur);
    }

    /// <summary>🔔 通知からその場所へ飛ぶ（地上モードに入り、盤をそこへ寄せて選択する）。</summary>
    private void JumpToRegion(int regionId)
    {
        if (regionId < 0 || regionId >= SurfaceMap.Count) return;
        // ⏳ 地上へ飛べるのは**後半（地上フェーズ）だけ**。前半に飛ぶとフェーズ分けが崩れる。
        if (turn != null && !turn.IsSurfacePhase) return;
        if (!surfaceModeOn) SetSurfaceMode(true);
        selectedRegionId = regionId; surfaceActionMsg = "";
        if (surfaceView != null) { surfaceView.SetSelected(regionId); surfaceView.CenterOn(regionId); }
        RefreshSurfacePanel();
    }

    private void BuildLogPanel(RectTransform root)
    {
        var panel = Panel(root, "LogPanel", PANEL);
        logPanel = panel.gameObject;
        Anchor(panel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        panel.rectTransform.sizeDelta = new Vector2(560, 620);
        panel.rectTransform.anchoredPosition = new Vector2(-16, -72);
        Outline(panel, LINE2); SkinPanel(panel);
        var t = Text(panel, "記録（直近" + NotifySystem.MaxLog + "件・押すとその場所へ飛ぶ）", 12.5f, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(t.rectTransform, 16, 12, 460, 18);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => logPanel.SetActive(false));
        Place((RectTransform)close.transform, 560 - 42, 10, 28, 24);
        logBody = MakeVScroll(panel, 14, 38, 560 - 28, 620 - 52); logW = 560 - 28;
        panel.gameObject.SetActive(false);
    }

    private void RefreshLogPanel()
    {
        var c = logBody; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = logW, y = 0;
        var list = NotifySystem.Log;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var n = list[i];
            string col = NotifySystem.ColorOf(n.kind);
            var row = Panel(c, "L" + i, i % 2 == 0 ? CARD : C("#171423"));
            Place(row.rectTransform, 0, y, w - 6, 34); Outline(row, LINE);
            var bar = Panel(row.rectTransform, "bar", C(col)); Place(bar.rectTransform, 0, 0, 3, 34);
            var tt = Text(row.rectTransform, "<size=85%><color=#6f6889>T" + n.turn + "</color></size>  " + n.text,
                12f, TEXT, TextAlignmentOptions.Left);
            Place(tt.rectTransform, 12, 7, w - 24, 20);
            if (n.regionId >= 0)
            {
                int rid = n.regionId;
                var bt = row.gameObject.AddComponent<Button>(); bt.targetGraphic = row;
                bt.onClick.AddListener(() => { logPanel.SetActive(false); JumpToRegion(rid); });
            }
            y += 38;
        }
        if (list.Count == 0)
        {
            var e = Text(c, "<color=#6f6889>まだ何も起きていません。</color>", 12, FAINT, TextAlignmentOptions.Left);
            Place(e.rectTransform, 8, 0, w - 16, 20); y = 28;
        }
        c.sizeDelta = new Vector2(0f, Mathf.Max(y + 8, 80));
    }

    // ================= 💾 セーブ / ロード（Phase E-19） =================
    //  ⚠ 保存は**準備フェーズのみ**（戦闘中の場を保存すると「戦いの途中から再開」を作り込むことになる）。
    //     オートセーブはターンの頭で自動的に書かれる。→ [[SaveSystem]]
    private const float SAVE_W = 760f;

    private void BuildSavePanel(RectTransform root)
    {
        var panel = Panel(root, "SavePanel", PANEL);
        savePanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(SAVE_W, 452);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        Outline(panel, LINE2); SkinPanel(panel);
        var t = Text(panel, "記録の保存と読み込み", 16, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(t.rectTransform, 20, 14, 400, 22);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => savePanel.SetActive(false));
        Place((RectTransform)close.transform, SAVE_W - 46, 12, 28, 24);
        saveBody = NewRect("Body", panel.rectTransform);
        Place(saveBody, 20, 44, SAVE_W - 40, 396);
        panel.gameObject.SetActive(false);
    }

    private void OpenSavePanel()
    {
        if (savePanel == null) return;
        bool on = !savePanel.activeSelf;
        savePanel.SetActive(on);
        if (!on) return;
        RefreshSavePanel();
        savePanel.transform.SetAsLastSibling();
        PlayFadeIn(savePanel);
    }

    private void RefreshSavePanel() { FillSaveRows(saveBody, SAVE_W - 40, false); }

    /// <summary>スロットの一覧。タイトルの『続きから』とゲーム中の『記録』で同じ物を使う。</summary>
    private void FillSaveRows(RectTransform body, float w, bool loadOnly)
    {
        if (body == null) return;
        for (int i = body.childCount - 1; i >= 0; i--) { var g = body.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }

        string why;
        bool canSave = SaveSystem.CanSave(out why);
        float y = 0;

        for (int slot = 0; slot <= SaveSystem.SlotCount; slot++)
        {
            int s = slot;
            var info = SaveSystem.Peek(s);
            var row = Panel(body, "S" + s, s == 0 ? C("#171423") : CARD);
            Place(row.rectTransform, 0, y, w, 74); Outline(row, LINE);

            var name = Text(row.rectTransform, s == 0 ? "オート" : ("スロット " + s), 14, s == 0 ? MUTED : TEXT,
                TextAlignmentOptions.Left, FontStyles.Bold);
            Place(name.rectTransform, 16, 12, 120, 20);

            string body1, body2;
            if (!info.exists)
            {
                body1 = "<color=#6f6889>― 空 ―</color>";
                body2 = s == 0 ? "<color=#6f6889>ターンの頭で自動的に書かれます</color>" : "";
            }
            else
            {
                body1 = "第 <b>" + info.turn + "</b> ターン　" + info.era + "　" + info.floors + "層";
                body2 = "<color=#e3a94a>DP " + info.dp.ToString("N0") + "</color>　領地 " + info.owned
                    + "　<color=#6f6889>" + info.savedAt + "　プレイ " + SaveSystem.PlayTimeText(info.playSeconds) + "</color>";
            }
            float tw = w - 330;                        // 名前(110) と ボタン(2つ=190) の間
            var l1 = Text(row.rectTransform, body1, 13, TEXT, TextAlignmentOptions.Left);
            l1.enableWordWrapping = false;
            Place(l1.rectTransform, 126, 12, tw, 20);
            var l2 = Text(row.rectTransform, body2, 11.5f, FAINT, TextAlignmentOptions.Left);
            l2.enableWordWrapping = false;
            Place(l2.rectTransform, 126, 36, tw, 18);

            float bx = w - 186;
            if (!loadOnly && s != 0)
            {
                var sv = PrimaryButton(row.rectTransform, info.exists ? "上書き" : "保存",
                    canSave ? BLOOD : PANEL2, canSave ? C("#f0d9a0") : FAINT, () => DoSave(s));
                Place((RectTransform)sv.transform, bx, 22, 82, 30);
                if (!canSave) AddTooltip(((RectTransform)sv.transform).gameObject, why);
                bx += 90;
            }
            else bx += 90;

            var ld = PrimaryButton(row.rectTransform, "読込", info.exists ? PANEL2 : C("#17141f"),
                info.exists ? TEXT : FAINT, () => DoLoad(s));
            Place((RectTransform)ld.transform, bx, 22, 82, 30);

            y += 80;
        }

        var note = Text(body, canSave
            ? "<color=#6f6889>保存できるのは準備フェーズだけです。オートはターンの頭に上書きされます。</color>"
            : "<color=#df5a5a>" + why + "</color>", 11.5f, FAINT, TextAlignmentOptions.Left);
        Place(note.rectTransform, 2, y + 6, w - 4, 18);
        body.sizeDelta = new Vector2(w, y + 30);
    }

    private void DoSave(int slot)
    {
        string err;
        if (SaveSystem.Save(slot, out err))
        {
            SoundSystem.Play(SoundSystem.Sfx.Save);
            NotifySystem.Push("💾 <b>" + SaveSystem.SlotName(slot) + "</b> に保存した", NotifySystem.Kind.Info);
            RefreshSavePanel();
        }
        else NotifySystem.Push("💾 保存できない ― " + err, NotifySystem.Kind.Danger);
    }

    private void DoLoad(int slot)
    {
        string err;
        if (!SaveSystem.Load(slot, out err))
        {
            NotifySystem.Push("💾 読み込めない ― " + err, NotifySystem.Kind.Danger);
            return;
        }
        if (savePanel != null) savePanel.SetActive(false);
        if (titleRoot != null) titleRoot.SetActive(false);
        if (dungeonCanvas != null) dungeonCanvas.enabled = true;
        OnGameLoaded();
    }

    /// <summary>ロード後にUI側を全部作り直す。⚠ 盤・タブ・選択状態は保存していないので**ここで整える**。</summary>
    private void OnGameLoaded()
    {
        selectedRegionId = -1;
        SetSurfaceMode(false);
        OpenExclusive(null);
        placementSig = null;                 // 署名を空にして、ストリップを必ず作り直させる
        RefreshSelections(); RefreshCost(); RefreshFloorTabs(); RefreshSurfaceSizeBtns();
        RefreshSquadTray(); RefreshEmotionPools();
        if (surfaceView != null) surfaceView.MarkDirty();
        NotifySystem.Push("💾 記録を読み込んだ ― 第 " + (turn != null ? turn.CurrentTurn : 1) + " ターンから",
            NotifySystem.Kind.Story);
    }

    // ================= ⚙️ 設定（Phase E-21） =================
    private GameObject settingsPanel; private RectTransform settingsBody;
    private const float SET_W = 560f;

    /// <summary>
    /// ⚠ 設定は**専用のCanvas**に置く。タイトル画面（order 300）からもゲーム中からも開くので、
    /// 迷宮のCanvasに置くとタイトル表示中は `dungeonCanvas.enabled = false` で消え、
    /// タイトルのCanvasに置くとゲーム中は `titleRoot` ごと畳まれて消える。
    /// </summary>
    private void BuildSettingsPanel()
    {
        var root = MakeCanvas("SettingsCanvas", 320);
        var panel = Panel(root, "SettingsPanel", PANEL);
        settingsPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(SET_W, 400);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        Outline(panel, LINE2); SkinPanel(panel);
        var t = Text(panel, "設定", 16, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(t.rectTransform, 20, 14, 300, 22);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => settingsPanel.SetActive(false));
        Place((RectTransform)close.transform, SET_W - 46, 12, 28, 24);
        settingsBody = NewRect("Body", panel.rectTransform);
        Place(settingsBody, 20, 46, SET_W - 40, 340);
        panel.gameObject.SetActive(false);
    }

    private void OpenSettings()
    {
        if (settingsPanel == null) return;
        bool on = !settingsPanel.activeSelf;
        settingsPanel.SetActive(on);
        if (!on) return;
        RefreshSettingsPanel();
        settingsPanel.transform.SetAsLastSibling();
        PlayFadeIn(settingsPanel);
    }

    private void RefreshSettingsPanel()
    {
        var c = settingsBody; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = SET_W - 40, y = 0;

        var h = Text(c, "音量", 12, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(h.rectTransform, 0, y, w, 16); h.characterSpacing = 4; y += 24;

        y = VolumeRow(c, w, y, "全体", SoundSystem.Master, v => SoundSystem.Master = v);
        y = VolumeRow(c, w, y, "BGM", SoundSystem.BgmVolume, v => SoundSystem.BgmVolume = v);
        y = VolumeRow(c, w, y, "効果音", SoundSystem.SeVolume, v => { SoundSystem.SeVolume = v; SoundSystem.Play(SoundSystem.Sfx.Click); });
        y += 10;

        var h2 = Text(c, "表示", 12, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(h2.rectTransform, 0, y, w, 16); h2.characterSpacing = 4; y += 24;

        // 📖 腹心の報告：慣れたら切れるように（初心者向けの説明が毎ターン出るのは中盤から邪魔）
        var gb = PrimaryButton(c, GuideSystem.Enabled ? "腹心の報告を出す：オン" : "腹心の報告を出す：オフ",
            GuideSystem.Enabled ? PANEL2 : C("#17141f"), GuideSystem.Enabled ? TEXT : FAINT,
            () => { GuideSystem.Enabled = !GuideSystem.Enabled; RefreshSettingsPanel(); });
        Place((RectTransform)gb.transform, 0, y, w, 34); y += 42;

        var note = Text(c, "<color=#6f6889>音は全部その場で合成しています（音のファイルは使っていません）。</color>",
            11.5f, FAINT, TextAlignmentOptions.Left);
        Place(note.rectTransform, 0, y, w, 18); y += 26;

        var tb = PrimaryButton(c, "タイトルへ戻る", PANEL2, MUTED, BackToTitle);
        Place((RectTransform)tb.transform, 0, y, w * 0.5f - 6, 36);
        var qb = PrimaryButton(c, "ゲームを終了", PANEL2, MUTED, QuitGame);
        Place((RectTransform)qb.transform, w * 0.5f + 6, y, w * 0.5f - 6, 36);
        y += 44;

        c.sizeDelta = new Vector2(w, y);
        var prt = (RectTransform)settingsPanel.transform;
        prt.sizeDelta = new Vector2(SET_W, y + 70);
    }

    /// <summary>音量の1行。⚠ uGUI の Slider は部品を自前で組む必要がある（背景／伸びる面／つまみ）。</summary>
    private float VolumeRow(RectTransform parent, float w, float y, string label, float value, UnityAction<float> onChanged)
    {
        var lab = Text(parent, label, 13, TEXT, TextAlignmentOptions.Left);
        Place(lab.rectTransform, 0, y + 4, 80, 20);
        var val = Text(parent, Mathf.RoundToInt(value * 100f) + "%", 12, MUTED, TextAlignmentOptions.Right);
        Place(val.rectTransform, w - 56, y + 5, 52, 18);

        float sw = w - 150;
        var track = Panel(parent, "track_" + label, C("#0e0b16"));
        Place(track.rectTransform, 88, y + 10, sw, 10); Outline(track, LINE);

        var fillArea = NewRect("fillArea", track.rectTransform);
        Place(fillArea, 0, 0, sw, 10);
        var fill = Panel(fillArea, "fill", GOLD);
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(1, 1);
        fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;

        var handle = Panel(track.rectTransform, "handle", TEXT);
        handle.rectTransform.sizeDelta = new Vector2(14, 22);

        var sl = track.gameObject.AddComponent<Slider>();
        sl.fillRect = fill.rectTransform;
        sl.handleRect = handle.rectTransform;
        sl.targetGraphic = handle;
        sl.direction = Slider.Direction.LeftToRight;
        sl.minValue = 0f; sl.maxValue = 1f; sl.value = value;
        sl.onValueChanged.AddListener(v => { onChanged(v); SetTxt(val, Mathf.RoundToInt(v * 100f) + "%"); });
        return y + 34;
    }

    /// <summary>⚠ タイトルへ戻すのは**保存していない進行を捨てる**こと。オートセーブがあるので直前のターンには戻れる。</summary>
    private void BackToTitle()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (savePanel != null) savePanel.SetActive(false);
        // ⚠ リザルトのCanvasはタイトル(300)より上(330)にある。畳まないとタイトルが出ているのに触れない
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        OpenExclusive(null); SetSurfaceMode(false);
        GameSetup.Started = false;
        if (dungeonCanvas != null) dungeonCanvas.enabled = false;
        if (titleRoot != null) { titleRoot.SetActive(true); ShowTitlePage(0); }
        SoundSystem.PlayBgm(SoundSystem.Bgm.Prepare);
    }

    // ================= 🔦 発見（S4） =================
    private void BuildDiscoveryPanel(RectTransform root)
    {
        var panel = Panel(root, "DiscoveryPanel", PANEL);
        discoveryPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(DISC_W, 360);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        Outline(panel, C("#e3a94a")); SkinPanel(panel);
        discoveryBody = NewRect("Body", panel.rectTransform);
        Place(discoveryBody, 26, 22, DISC_W - 52, 300);
        panel.gameObject.SetActive(false);
    }

    private void RefreshDiscoveryPanel()
    {
        if (discoveryBody == null) return;
        for (int i = discoveryBody.childCount - 1; i >= 0; i--) Destroy(discoveryBody.GetChild(i).gameObject);
        if (DiscoverySystem.Pending < 0) return;
        var d = DiscoverySystem.Get(DiscoverySystem.Pending);
        float w = DISC_W - 52, y = 0;

        var eye = Text(discoveryBody, "発見　―　" + (DiscoverySystem.PendingRegion >= 0
            ? SurfaceMap.Get(DiscoverySystem.PendingRegion).name : ""), 11, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(eye.rectTransform, 0, y, w, 16); eye.characterSpacing = 6; y += 20;
        var ti = Text(discoveryBody, d.title, 22, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(ti.rectTransform, 0, y, w, 30); y += 36;
        var st = Text(discoveryBody, d.story, 13.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(st.rectTransform, 0, y, w, 46); y += 56;

        for (int i = 0; i < 2; i++)
        {
            int ci = i;
            var ch = i == 0 ? d.a : d.b;
            var card = Panel(discoveryBody, "DC_" + i, CARD);
            Place(card.rectTransform, 0, y, w, 56); Outline(card, LINE);
            var t1 = Text(card.rectTransform, ch.label, 14, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(t1.rectTransform, 14, 8, w - 28, 20);
            var t2 = Text(card.rectTransform, "<size=90%><color=#e3a94a>" + DiscoverySystem.Reward(ch).Trim() + "</color></size>",
                11.5f, GOLD, TextAlignmentOptions.Left);
            Place(t2.rectTransform, 14, 30, w - 28, 18);
            var bt = card.gameObject.AddComponent<Button>(); bt.targetGraphic = card;
            bt.onClick.AddListener(() =>
            {
                if (DiscoverySystem.Choose(ci))
                {
                    if (discoveryPanel != null) discoveryPanel.SetActive(false);
                    if (surfaceView != null) surfaceView.MarkDirty();
                    if (surfaceModeOn) RefreshSurfacePanel();
                }
            });
            y += 62;
        }
        var prt = (RectTransform)discoveryPanel.transform;
        discoveryBody.sizeDelta = new Vector2(w, y);
        prt.sizeDelta = new Vector2(DISC_W, y + 44);
    }
}
