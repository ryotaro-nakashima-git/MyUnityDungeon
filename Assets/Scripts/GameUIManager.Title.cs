using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// タイトル画面（開始／戦績／続きから／遊び方／世界設定）と新規開始。
/// <para>`GameUIManager` の partial。フィールドの本体は GameUIManager.cs 側にある。</para>
/// </summary>
public partial class GameUIManager
{

    // ================= 🎬 タイトル画面／世界設定 =================
    //  起動時はここで止め、『地上の広さ・宝箱の量・階層数・迷宮タイプ』を選んでから世界を作る。
    //  初期DPは **開始予算 − 初期迷宮の建造費**（GameSetup）。豪華に始めるほど手元が乏しくなる。
    private void BuildTitleScreen()
    {
        var tRoot = MakeCanvas("TitleCanvas", 300);
        titleRoot = tRoot.gameObject;
        titlePages[0] = BuildTitlePage(tRoot).gameObject;
        titlePages[1] = BuildSetupPage(tRoot).gameObject;
        titlePages[2] = BuildHelpPage(tRoot).gameObject;
        titlePages[3] = BuildLoadPage(tRoot).gameObject;
        titlePages[4] = BuildRecordPage(tRoot).gameObject;

        if (!showTitleOnStart) { titleRoot.SetActive(false); return; }
        if (dungeonCanvas != null) dungeonCanvas.enabled = false;   // 背後のHUDを止める
        if (GameSetup.Seed == 0) GameSetup.Seed = Random.Range(1, int.MaxValue);
        SoundSystem.PlayBgm(SoundSystem.Bgm.Prepare);   // 🔊 タイトルから曲を敷く
        ShowTitlePage(0);
    }

    private void ShowTitlePage(int page)
    {
        for (int i = 0; i < titlePages.Length; i++)
            if (titlePages[i] != null) titlePages[i].SetActive(i == page);
        if (page == 1) RefreshTitleSel();
        if (page == 3) FillSaveRows(titleLoadBody, SAVE_W, true);
        if (page == 4) RefreshRecordPage();
    }

    private Image BuildTitlePage(RectTransform root)
    {
        var page = Panel(root, "TitlePage", C("#0b0910"));
        StretchFull(page.rectTransform);

        var eyebrow = Text(page, "DUNGEON  BATTLE  ROYALE", 13, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(eyebrow.rectTransform, 460, 236, 1000, 20); eyebrow.characterSpacing = 10;
        var t = Text(page, "ダンジョン<color=#b0202b>バトルロワイヤル</color>", 62, TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(t.rectTransform, 460, 262, 1000, 88);
        var line = Panel(page, "line", BLOOD); Place(line.rectTransform, 810, 360, 300, 2);
        var sub = Text(page, "迷宮を統べ、地上を侵す。", 17, MUTED, TextAlignmentOptions.Center);
        Place(sub.rectTransform, 460, 378, 1000, 26);

        var b1 = PrimaryButton(page, "新しい世界を始める", BLOOD, C("#f0d9a0"), () => ShowTitlePage(1), true);
        Place((RectTransform)b1.transform, 800, 470, 320, 58);
        // 💾 セーブの有無は**押した先の画面**で見せる（ここで判定して灰色にすると、
        //    あとから保存してタイトルへ戻ったときに押せないままになる）
        var bC = PrimaryButton(page, "続きから", PANEL2, TEXT, () => ShowTitlePage(3));
        Place((RectTransform)bC.transform, 800, 542, 320, 46);
        var b2 = PrimaryButton(page, "遊び方", PANEL2, TEXT, () => ShowTitlePage(2));
        Place((RectTransform)b2.transform, 800, 600, 320, 46);
        var bR = PrimaryButton(page, "戦績・実績", PANEL2, TEXT, () => ShowTitlePage(4));
        Place((RectTransform)bR.transform, 800, 658, 320, 46);
        var bS = PrimaryButton(page, "設定", PANEL2, TEXT, OpenSettings);
        Place((RectTransform)bS.transform, 800, 716, 320, 46);
        var b3 = PrimaryButton(page, "終了", PANEL2, MUTED, QuitGame);
        Place((RectTransform)b3.transform, 800, 774, 320, 46);

        var foot = Text(page, "配下を育て、罠を敷き、押し寄せる冒険者を退ける。地上へ眷属を放ち、世界を塗り替えよ。", 12, FAINT, TextAlignmentOptions.Center);
        Place(foot.rectTransform, 460, 846, 1000, 22);
        return page;
    }

    private RectTransform titleLoadBody, recordStatBody, recordAchBody;

    /// <summary>
    /// 🏅 戦績（Phase F-23/F-24/F-25）。**周を越えて残るもの**をここに集める：
    /// 通算記録・実績・形見。⚠ セーブ([[SaveSystem]])は1周の中身しか持たないので、
    /// こちらは `PlayerPrefs` 側（[[RunStats]] [[Achievements]] [[NarrativeSystem]]）を見る。
    /// </summary>
    private Image BuildRecordPage(RectTransform root)
    {
        var page = Panel(root, "RecordPage", C("#0b0910"));
        StretchFull(page.rectTransform);
        var t = Text(page, "戦績", 30, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(t.rectTransform, 300, 110, 600, 40);
        recordStatBody = NewRect("StatBody", page.rectTransform);
        Place(recordStatBody, 300, 164, 520, 280);

        var t2 = Text(page, "実績", 20, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(t2.rectTransform, 300, 470, 520, 28);
        var ach = MakeVScroll(page, 300, 506, 1320, 330);
        recordAchBody = ach;

        var back = PrimaryButton(page, "戻る", PANEL2, TEXT, () => ShowTitlePage(0));
        Place((RectTransform)back.transform, 300, 860, 220, 50);
        page.gameObject.SetActive(false);
        return page;
    }

    private void RefreshRecordPage()
    {
        if (recordStatBody != null)
        {
            for (int i = recordStatBody.childCount - 1; i >= 0; i--) Destroy(recordStatBody.GetChild(i).gameObject);
            string[,] rows =
            {
                { "遊んだ周", RunStats.Runs + " 周" },
                { "勝ち切った回数", RunStats.Wins + " 回" },
                { "最高スコア", UITheme.Num(RunStats.BestScore) },
                { "最速の勝利", RunStats.BestTurn > 0 ? RunStats.BestTurn + " ターン" : "-" },
                { "通算の撃破", UITheme.Num(RunStats.TotalKills) },
                { "通算の波", UITheme.Num(RunStats.TotalWaves) },
                { "通算の時間", SaveSystem.PlayTimeText(RunStats.TotalSeconds) },
                { "実績", Achievements.UnlockedCount + " / " + Achievements.Count },
                { "形見", NarrativeSystem.UnlockedCount + " / " + NarrativeSystem.MementoCount
                    + "（枠 " + NarrativeSystem.Slots + "）" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                var k = Text(recordStatBody, rows[i, 0], 13, MUTED, TextAlignmentOptions.Left);
                Place(k.rectTransform, 0, i * 28, 280, 22);
                var v = Text(recordStatBody, rows[i, 1], 14, TEXT, TextAlignmentOptions.Right, FontStyles.Bold);
                Place(v.rectTransform, 280, i * 28, 240, 22);
            }
        }

        var c = recordAchBody; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = 1320 - 14, cw = (w - 20) / 3f, y = 0;
        for (int i = 0; i < Achievements.Count; i++)
        {
            var a = Achievements.Get(i);
            bool got = Achievements.IsUnlocked(i);
            float cx = (i % 3) * (cw + 10), cy = (i / 3) * 62;
            var card = Panel(c, "A" + i, got ? CARD : C("#100e18"));
            Place(card.rectTransform, cx, cy, cw, 54); Outline(card, got ? GOLD_DK : LINE);
            // 🏅 未達成の隠し実績は中身を伏せる（探す楽しみを残す）
            bool veil = a.hidden && !got;
            var nm = Text(card.rectTransform, veil ? "??????" : a.jpName, 13.5f, got ? GOLD : MUTED,
                TextAlignmentOptions.Left, FontStyles.Bold);
            Place(nm.rectTransform, 12, 7, cw - 24, 20);
            var how = Text(card.rectTransform, veil ? "<color=#3a3550>隠し実績</color>" : a.how, 11, FAINT, TextAlignmentOptions.Left);
            Place(how.rectTransform, 12, 29, cw - 24, 18);
            y = cy + 62;
        }
        c.sizeDelta = new Vector2(0f, Mathf.Max(y + 8, 80));
    }

    /// <summary>💾 タイトルの『続きから』。ゲーム中の保存画面と同じ行を、読込だけにして並べる。</summary>
    private Image BuildLoadPage(RectTransform root)
    {
        var page = Panel(root, "LoadPage", C("#0b0910"));
        StretchFull(page.rectTransform);
        var t = Text(page, "続きから", 30, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(t.rectTransform, 630, 150, 800, 40);
        var s = Text(page, "<color=#6f6889>保存した記録を読み込みます。オートはターンの頭に書かれたものです。</color>",
            13, FAINT, TextAlignmentOptions.Left);
        Place(s.rectTransform, 630, 194, 800, 20);
        titleLoadBody = NewRect("LoadBody", page.rectTransform);
        Place(titleLoadBody, 630, 232, SAVE_W, 400);
        var back = PrimaryButton(page, "戻る", PANEL2, TEXT, () => ShowTitlePage(0));
        Place((RectTransform)back.transform, 630, 660, 220, 50);
        page.gameObject.SetActive(false);
        return page;
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private Image BuildHelpPage(RectTransform root)
    {
        var page = Panel(root, "HelpPage", C("#0b0910"));
        StretchFull(page.rectTransform);
        var t = Text(page, "遊び方", 30, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(t.rectTransform, 360, 120, 1200, 40);
        string body =
            "<b><color=#e3a94a>1. 準備フェーズ</color></b>\n"
            + "  DPを払って罠・スポナー・トーテムを敷き、配下を召喚して各階に配置する。部隊を組み、1体をボスに任命できる。\n\n"
            + "<b><color=#e3a94a>2. 防衛戦</color></b>\n"
            + "  『侵略開始』で冒険者のウェーブが突入する。倒す・怖がらせる・宝箱を漁らせる、どれもDPと感情になる。\n"
            + "  最下層の魔王が討たれたら敗北。\n\n"
            + "<b><color=#e3a94a>3. 育てる</color></b>\n"
            + "  経験値は深い階層ほど多く入る（魔素濃度）。冒険者は自分の格に合う深さまでしか降りてこない。\n"
            + "  取り残された個体は、地上の訓練所か『実戦の反芻』で埋める。\n\n"
            + "<b><color=#e3a94a>4. 地上（4X）</color></b>\n"
            + "  上部の『地上』から世界地図へ。眷属を指揮官として送り、領域を獲り、拠点を築き、施設で産出を伸ばす。\n"
            + "  時代・偉業・外交・勝利条件はすべて左端のメニューから。\n\n"
            + "<b><color=#e3a94a>操作</color></b>  左クリック＝配置 ／ 右クリック＝撤去 ／ ホイール＝ズーム ／ ドラッグ＝移動";
        var b = Text(page, body, 15, TEXT, TextAlignmentOptions.TopLeft);
        Place(b.rectTransform, 360, 176, 1200, 620);
        var back = PrimaryButton(page, "戻る", PANEL2, TEXT, () => ShowTitlePage(0));
        Place((RectTransform)back.transform, 360, 830, 220, 50);
        return page;
    }

    private static readonly SurfaceGen.Size[] TitleWorldSizes =
        { SurfaceGen.Size.Tiny, SurfaceGen.Size.Small, SurfaceGen.Size.Medium, SurfaceGen.Size.Large };

    private Image BuildSetupPage(RectTransform root)
    {
        var page = Panel(root, "SetupPage", C("#0b0910"));
        StretchFull(page.rectTransform);

        var eye = Text(page, "世界設定", 12, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(eye.rectTransform, 360, 62, 600, 18); eye.characterSpacing = 8;
        var t = Text(page, "この世界の始まりを決める", 30, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(t.rectTransform, 360, 82, 900, 40);
        var sub = Text(page, "選んだ迷宮はそのまま建造されます。<b>開始予算から建造費を引いた残りが初期DP</b>です。豪華に始めるほど手元は乏しくなります。",
            13, MUTED, TextAlignmentOptions.Left);
        Place(sub.rectTransform, 360, 126, 1200, 22);

        float lx = 360, rx = 980, cw = 580;

        // ---- 左：迷宮タイプ ----
        var l1 = Text(page, "迷宮タイプ（形の性格。得と損がセット）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(l1.rectTransform, lx, 168, cw, 16);
        string[] tNames = { "標準", "迷路", "大空洞", "蟻の巣" };
        string[] tDesc = {
            "配置枠+2 ／ 癖なし",
            "冒険者が長居+35% ／ 宝箱-25%",
            "部隊+10%・徘徊+1 ／ 集客-15%",
            "宝箱+50%・集客+20% ／ トーテム半径-1" };
        tTypeBtns.Clear();
        float tcw = (cw - 10) / 2f;
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            var b = Card(page, lx + (i % 2) * (tcw + 10), 188 + (i / 2) * 62, tcw, 54, tNames[i], tDesc[i],
                () => { GameSetup.DungeonTypeIdx = idx; RefreshTitleSel(); });
            var cost = Text(b.rectTransform, "+" + GameSetup.TypeCost(i) + " DP", 10.5f, GOLD, TextAlignmentOptions.Right);
            Place(cost.rectTransform, tcw - 76, 7, 66, 16);
            tTypeBtns.Add(b);
        }

        // ---- 左：空間タイプ ----
        var l2 = Text(page, "空間タイプ（属性の性格。費用はかかりません）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(l2.rectTransform, lx, 322, cw, 16);
        string[] sNames = { "洞窟", "遺跡", "城塞", "溶岩", "氷雪" };
        Color[] sCols = { C("#5a5560"), C("#5c6446"), C("#4e5674"), C("#7a3a30"), C("#4a6480") };
        tSpaceBtns.Clear();
        float scw = (cw - 20) / 3f;
        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            var b = Chip(page, lx + (i % 3) * (scw + 10), 342 + (i / 3) * 40, scw, 32, sNames[i], sCols[i],
                () => { GameSetup.SpaceTypeIdx = idx; RefreshTitleSel(); });
            tSpaceBtns.Add(b);
        }
        titleSpaceEffText = Text(page, "", 11.5f, MUTED, TextAlignmentOptions.Left);
        Place(titleSpaceEffText.rectTransform, lx, 424, cw, 18);

        // ---- 左：宝箱の量 ----
        var l3 = Text(page, "宝箱の量（多いほど集客と収入が増えるが、建造費も嵩む）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(l3.rectTransform, lx, 456, cw, 16);
        string[] cNames = { "少", "中", "多" };
        tChestBtns.Clear();
        float ccw = (cw - 20) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var b = Chip(page, lx + i * (ccw + 10), 476, ccw, 34, cNames[i] + "  (" + GameSetup.ChestCost(i) + "DP/層)", GOLD,
                () => { GameSetup.ChestIdx = idx; RefreshTitleSel(); });
            tChestBtns.Add(b);
        }

        // ---- 右：階層数 ----
        var r1 = Text(page, "初期階層数（深いほど守りは厚いが、器のぶん建造費も増える）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(r1.rectTransform, rx, 168, cw, 16);
        string[] fNames = { "1層", "2層", "3層" };
        tFloorBtns.Clear();
        float fcw = (cw - 20) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int n = i + 1;
            var b = Chip(page, rx + i * (fcw + 10), 188, fcw, 34, fNames[i], VIOLET,
                () => { GameSetup.FloorCount = n; RefreshTitleSel(); });
            tFloorBtns.Add(b);
        }
        var r1n = Text(page, "予算は +400/層 しか増えないので、深く始めるなら宝箱は少なめに。魔王は最下層にのみ実在します。",
            11.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(r1n.rectTransform, rx, 228, cw, 34);

        // ---- 右：地上の広さ ----
        var r2 = Text(page, "地上の広さ（Civ準拠。毎回ちがう地形が生成されます）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(r2.rectTransform, rx, 274, cw, 16);
        tWorldBtns.Clear();
        float wcw = (cw - 30) / 4f;
        for (int i = 0; i < 4; i++)
        {
            int wi = i;
            var b = Panel(page, "TWorld_" + i, CARD);
            Place(b.rectTransform, rx + i * (wcw + 10), 294, wcw, 34); Outline(b, LINE);
            var nm = Text(b.rectTransform, SurfaceGen.NameOf(TitleWorldSizes[i]), 12.5f, TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(nm.rectTransform, 0, 3, wcw, 16);
            var ct = Text(b.rectTransform, SurfaceGen.TileCount(TitleWorldSizes[i]) + "タイル", 10, MUTED, TextAlignmentOptions.Center);
            Place(ct.rectTransform, 0, 19, wcw, 14);
            var bt = b.gameObject.AddComponent<Button>(); bt.targetGraphic = b;
            bt.onClick.AddListener(() => { GameSetup.WorldSize = TitleWorldSizes[wi]; RefreshTitleSel(); });
            tWorldBtns.Add(b);
        }
        var r2n = Text(page, "広いほど攻める先も守る先も増えます。東西はループします（初期DPには影響しません）。",
            11.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(r2n.rectTransform, rx, 334, cw, 20);

        // ---- 右：シード ----
        var r3 = Text(page, "世界の種（同じ数字なら同じ地形になります）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(r3.rectTransform, rx, 366, cw, 16);
        var seedBox = Panel(page, "SeedBox", CARD);
        Place(seedBox.rectTransform, rx, 386, cw - 150, 34); Outline(seedBox, LINE);
        titleSeedText = Text(seedBox.rectTransform, "-", 13, TEXT, TextAlignmentOptions.Center); StretchFull(titleSeedText.rectTransform);
        var reroll = PrimaryButton(page, "引き直す", PANEL2, TEXT, () =>
            { GameSetup.Seed = Random.Range(1, int.MaxValue); GameSetup.DailySeed = false; RefreshTitleSel(); });
        Place((RectTransform)reroll.transform, rx + cw - 140, 386, 140, 34);

        // ---- 右：難易度（F-22）----
        var r4 = Text(page, "難易度（仕組みは変わりません。世の本気度と取り分だけが動きます）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(r4.rectTransform, rx, 430, cw, 16);
        tDiffBtns.Clear();
        float dcw = (cw - 30) / 4f;
        for (int i = 0; i < Difficulty.Count; i++)
        {
            int di = i;
            var d = Difficulty.Get(i);
            var b = Panel(page, "TDiff_" + i, CARD);
            Place(b.rectTransform, rx + i * (dcw + 10), 450, dcw, 34); Outline(b, LINE);
            var nm = Text(b.rectTransform, d.jpName, 13, C(d.colorHex), TextAlignmentOptions.Center, FontStyles.Bold);
            Place(nm.rectTransform, 0, 3, dcw, 16);
            var ct = Text(b.rectTransform, "スコア ×" + d.score.ToString("0.0"), 10, MUTED, TextAlignmentOptions.Center);
            Place(ct.rectTransform, 0, 19, dcw, 14);
            var bt = b.gameObject.AddComponent<Button>(); bt.targetGraphic = b;
            bt.onClick.AddListener(() => { GameSetup.DifficultyIdx = di; RefreshTitleSel(); });
            tDiffBtns.Add(b);
        }
        titleDiffText = Text(page, "", 11.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(titleDiffText.rectTransform, rx, 490, cw, 34);

        // ---- 右：日替わりの世界（F-26）----
        //  同じ日なら誰がやっても同じ世界。腕の比べどころになる。
        titleDailyBtn = PrimaryButton(page, "今日の世界に挑む", PANEL2, TEXT, () =>
        {
            GameSetup.DailySeed = !GameSetup.DailySeed;
            if (GameSetup.DailySeed)
            {
                GameSetup.Seed = GameSetup.TodaySeed;
                GameSetup.WorldSize = SurfaceGen.Size.Medium;
                GameSetup.DifficultyIdx = 2;              // 日替わりは条件を固定する（記録を比べるため）
                GameSetup.DungeonTypeIdx = GameSetup.TodaySeed % 4;
                GameSetup.SpaceTypeIdx = (GameSetup.TodaySeed / 4) % 5;
                GameSetup.ChestIdx = 1; GameSetup.FloorCount = 2;
            }
            RefreshTitleSel();
        });
        Place((RectTransform)titleDailyBtn.transform, rx, 530, cw - 150, 36);
        titleDailyText = Text(page, "", 11.5f, FAINT, TextAlignmentOptions.Left);
        Place(titleDailyText.rectTransform, rx + cw - 140, 540, 140, 20);

        // ---- 下：初期DPの内訳 ----
        var box = Panel(page, "BudgetBox", PANEL); Place(box.rectTransform, lx, 660, 1200, 116);
        Outline(box, LINE2); SkinPanel(box);
        // ⚠ 全角のマイナス(−)はUIフォントに無く、サニタイズで**消える**。半角ハイフンを使う。
        var bl = Text(box, "初期DP（開始予算 - 初期迷宮の建造費）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(bl.rectTransform, 20, 14, 700, 16);
        titleBudgetText = Text(box, "", 22, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(titleBudgetText.rectTransform, 20, 36, 1160, 30);
        titleNoteText = Text(box, "", 11.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(titleNoteText.rectTransform, 20, 72, 1160, 34);

        // ---- 決定 ----
        var back = PrimaryButton(page, "戻る", PANEL2, TEXT, () => ShowTitlePage(0));
        Place((RectTransform)back.transform, lx, 812, 220, 56);
        titleStartBtn = PrimaryButton(page, "この世界で始める", BLOOD, C("#f0d9a0"), StartNewGame, true);
        Place((RectTransform)titleStartBtn.transform, lx + 780, 812, 420, 56);

        page.gameObject.SetActive(false);
        return page;
    }

    private void RefreshTitleSel()
    {
        for (int i = 0; i < tTypeBtns.Count; i++) SetSel(tTypeBtns[i], i == GameSetup.DungeonTypeIdx);
        for (int i = 0; i < tSpaceBtns.Count; i++) SetSel(tSpaceBtns[i], i == GameSetup.SpaceTypeIdx);
        for (int i = 0; i < tChestBtns.Count; i++) SetSel(tChestBtns[i], i == GameSetup.ChestIdx);
        for (int i = 0; i < tFloorBtns.Count; i++) SetSel(tFloorBtns[i], i == GameSetup.FloorCount - 1);
        for (int i = 0; i < tWorldBtns.Count; i++) SetSel(tWorldBtns[i], TitleWorldSizes[i] == GameSetup.WorldSize);
        for (int i = 0; i < tDiffBtns.Count; i++) SetSel(tDiffBtns[i], i == GameSetup.DifficultyIdx);
        if (titleDiffText != null)
        {
            var d = Difficulty.Current;
            SetTxt(titleDiffText, d.desc + "\n<color=#6f6889>冒険者の伸び ×" + d.advPower.ToString("0.00")
                + "／人数 ×" + d.advCount.ToString("0.00") + "／他魔王 ×" + d.rivalGrow.ToString("0.00")
                + "／取り分 ×" + d.reward.ToString("0.00") + "</color>");
        }
        if (titleDailyText != null)
        {
            int b = RunStats.DailyBest(GameSetup.TodaySeed);
            SetTxt(titleDailyText, GameSetup.TodayLabel + (b > 0 ? "\n<color=#e3a94a>最高 " + UITheme.Num(b) + "</color>" : "\n<color=#6f6889>未挑戦</color>"));
        }
        if (titleDailyBtn != null)
        {
            var img = titleDailyBtn.targetGraphic as Image;
            if (img != null) img.color = GameSetup.DailySeed ? BLOOD : PANEL2;
        }

        if (titleSpaceEffText != null)
            SetTxt(titleSpaceEffText, DungeonTheme.SpaceName((DungeonGenerator.SpaceType)GameSetup.SpaceTypeIdx)
                + "：" + DungeonTheme.SpaceEffect((DungeonGenerator.SpaceType)GameSetup.SpaceTypeIdx));
        if (titleSeedText != null) SetTxt(titleSeedText, GameSetup.Seed.ToString("N0"));
        if (titleBudgetText != null)
            SetTxt(titleBudgetText, "開始予算 <color=#9c95b4>" + GameSetup.Budget.ToString("N0") + "</color>  -  建造費 <color=#df5a5a>"
                + GameSetup.BuildCost.ToString("N0") + "</color>  ＝  初期DP <color=#e3a94a>" + GameSetup.StartDP.ToString("N0") + "</color>");
        if (titleNoteText != null)
        {
            string s = "建造費の内訳： 基本 300 ＋ 宝箱 " + GameSetup.ChestCost(GameSetup.ChestIdx) + " ＝ "
                + (300 + GameSetup.ChestCost(GameSetup.ChestIdx)) + " × " + GameSetup.FloorCount + "層"
                + "  ＋  タイプ " + GameSetup.TypeCost(GameSetup.DungeonTypeIdx);
            if (GameSetup.OverBudget)
                s += "\n<color=#df5a5a>予算を超えています。</color>最低 " + GameSetup.MinStartDP + " DP は残りますが、序盤は宝箱と魔王だけで凌ぐことになります。";
            SetTxt(titleNoteText, s);
        }
    }

    /// <summary>世界設定を各システムへ流し、迷宮と地上を生成してゲームを始める。</summary>
    private void StartNewGame()
    {
        // 迷宮側の設定（生成パネルの選択状態も揃えておく）
        selType = GameSetup.DungeonTypeIdx; selSpace = GameSetup.SpaceTypeIdx;
        selChest = GameSetup.ChestIdx; selFloors = GameSetup.FloorCount - 1;
        if (generator != null)
        {
            generator.SetDungeonType(GameSetup.DungeonTypeIdx);
            generator.SetSpaceType(GameSetup.SpaceTypeIdx);
            generator.SetChestAmount(GameSetup.ChestIdx);
        }
        if (floorMgr != null) floorMgr.SetFloorCount(GameSetup.FloorCount);

        // 🌍 地上を作り直す（広さと種）。迷宮のあるタイルを選び直させる。
        SurfaceMap.Regenerate(GameSetup.WorldSize, GameSetup.Seed);
        selectedRegionId = -1;

        // 💰 初期DP＝予算−建造費。**建造費はここで前払い済み**なので、生成そのものは無料で行う。
        if (res != null) res.SetDP(GameSetup.StartDP);

        GameSetup.WaitForTitle = false; GameSetup.Started = true;
        // 📊 周の記録をまっさらにする（⚠ ここを忘れると前の周の数字が混ざる）
        RunStats.ResetRun();
        VictorySystem.Reset();
        if (featureMgr != null) featureMgr.ResetRunCounters();
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (generator != null) generator.GenerateAndBuild();
        PolicySystem.Reset(); AttributeSystem.Reset(); DiscoverySystem.Reset(); ScoutSystem.Reset();
        EnemyForce.Reset(); NotifySystem.Reset();
        KinRoster.GrantStarterKin();                      // 🌅 初手から地上に出られるよう眷属を1体
        GuideSystem.Reset(); GuideSystem.OnTurnStart(1);   // 📖 第1ターンの報告（開幕の手引き）

        if (titleRoot != null) titleRoot.SetActive(false);
        if (dungeonCanvas != null) dungeonCanvas.enabled = true;
        RefreshSelections(); RefreshCost(); RefreshFloorTabs(); RefreshSurfaceSizeBtns();

        Debug.Log($"🎬『開始』{DungeonTheme.TypeName((DungeonGenerator.DungeonType)GameSetup.DungeonTypeIdx)}／"
            + $"{DungeonTheme.SpaceName((DungeonGenerator.SpaceType)GameSetup.SpaceTypeIdx)}／宝箱{GameSetup.ChestIdx}／"
            + $"{GameSetup.FloorCount}層／地上{SurfaceMap.Count}タイル(seed {SurfaceMap.MapSeed})／初期DP {GameSetup.StartDP}"
            + $"（予算{GameSetup.Budget} − 建造費{GameSetup.BuildCost}）");
    }
}
