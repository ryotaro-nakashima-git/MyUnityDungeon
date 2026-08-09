using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 常設のHUD：上部バー・魔王HP・迷宮生成パネル・下部コマンドバー・ツールボタン・毎フレームの更新。
/// <para>`GameUIManager` の partial。フィールドの本体は GameUIManager.cs 側にある。</para>
/// </summary>
public partial class GameUIManager
{

    // ---------- ②上部HUD ----------
    private void BuildTopBar(RectTransform root)
    {
        var bar = Panel(root, "TopBar", HUD_BG);
        Anchor(bar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        bar.rectTransform.sizeDelta = new Vector2(0, 60); bar.rectTransform.anchoredPosition = Vector2.zero;
        AddBottomBorder(bar);

        var hlg = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset((int)UITheme.S3, (int)UITheme.S3, (int)UITheme.S2, (int)UITheme.S2);
        hlg.spacing = 10; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        // ⚠ 作品名はここに置かない。**上部バーは 2,236px あって画面(1920)から 316px はみ出していた**
        //    ＝右端の資源チップが見切れていた原因。ゲーム中に作品名は要らないのでタイトル画面だけに置く。

        // ターン/フェーズ ピル
        var pill = Panel(bar, "TurnPill", C("#0e0b16"));
        SizeElem(pill.gameObject, 228, 34);
        Outline(pill, LINE2);
        var ph = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
        ph.padding = new RectOffset(12, 10, 4, 4); ph.spacing = 8; ph.childAlignment = TextAnchor.MiddleLeft;
        ph.childControlWidth = true; ph.childControlHeight = true; ph.childForceExpandWidth = false;
        turnText = Text(pill, "Turn 1", 15, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        SizeElem(turnText.gameObject, 70, 26);
        phasePill = Panel(pill, "PhaseTag", C("#183726"));
        SizeElem(phasePill.gameObject, 120, 24); Round(phasePill);
        var pt = phasePill.gameObject.AddComponent<HorizontalLayoutGroup>();
        pt.padding = new RectOffset(9, 9, 2, 2); pt.childAlignment = TextAnchor.MiddleCenter;
        pt.childControlWidth = true; pt.childControlHeight = true;
        phaseText = Text(phasePill, "準備フェーズ", 12, GREEN, TextAlignmentOptions.Center, FontStyles.Bold);

        // 魔王パネルの開閉ボタン
        var dlBtn = PrimaryButton(bar, "魔王", PANEL2, TEXT, () => OpenExclusive(demonPanel));
        SizeElem(dlBtn.gameObject, 58, UITheme.BtnH);
        var emoBtn = PrimaryButton(bar, "感情", PANEL2, TEXT, () => OpenExclusive(emotionPanel));
        SizeElem(emoBtn.gameObject, 58, UITheme.BtnH);
        var relBtn = PrimaryButton(bar, "遺物", PANEL2, TEXT, () => { OpenExclusive(relicPanel); RefreshRelicPanel(); });
        SizeElem(relBtn.gameObject, 58, UITheme.BtnH);
        var rsBtn = PrimaryButton(bar, "研究", PANEL2, TEXT, () => { OpenExclusive(researchPanel); RefreshResearchPanel(); });
        SizeElem(rsBtn.gameObject, 58, UITheme.BtnH);
        var exBtn = PrimaryButton(bar, "拡張", PANEL2, TEXT, () => { OpenExclusive(expandPanel); RefreshExpandPanel(); });
        SizeElem(exBtn.gameObject, 58, UITheme.BtnH);
        var gdBtn = PrimaryButton(bar, "報告", PANEL2, TEXT, () => { if (guidePanel != null && guidePanel.activeSelf) CloseGuide(); else OpenGuide(); });
        SizeElem(gdBtn.gameObject, 58, UITheme.BtnH);
        var logBtn = PrimaryButton(bar, "記録", PANEL2, TEXT, () =>
        {
            if (logPanel == null) return;
            bool on = !logPanel.activeSelf;
            logPanel.SetActive(on);
            if (on) { RefreshLogPanel(); logPanel.transform.SetAsLastSibling(); PlayFadeIn(logPanel); }
        });
        SizeElem(logBtn.gameObject, 58, UITheme.BtnH);
        var savBtn = PrimaryButton(bar, "保存", PANEL2, TEXT, OpenSavePanel);
        SizeElem(savBtn.gameObject, 58, UITheme.BtnH);
        var setBtn = PrimaryButton(bar, "設定", PANEL2, TEXT, OpenSettings);
        SizeElem(setBtn.gameObject, 58, UITheme.BtnH);
        var surBtn =PrimaryButton(bar, "地上", PANEL2, TEXT, () => { OpenExclusive(null); SetSurfaceMode(surfacePanel == null || !surfacePanel.activeSelf); });
        SizeElem(surBtn.gameObject, 62, UITheme.BtnH);

        // ⚠️ 危険の可視化（戦闘中だけ中身が入る）
        dangerText = Text(bar, "", 12.5f, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        dangerText.enableWordWrapping = false;
        SizeElem(dangerText.gameObject, 210, 34);

        // 🩸 魔王HPバー（討伐＝ゲームオーバーの核。常時可視）
        BuildDemonLordHpBar(bar);

        // 伸縮スペーサ
        Spacer(bar);

        // 資源
        dpText = ResChip(bar, UITheme.DP, "DP", "0", "dp");
        fameText = ResChip(bar, UITheme.Fame, "名声", "0", "fame");
        matText = ResChip(bar, UITheme.Material, "素材", "0", "material");
        threatText = ResChip(bar, UITheme.Danger, "脅威度", "1.00", "threat"); // 🕸️ 誘導経済：世界の脅威度
        slotText = ResChip(bar, UITheme.Research, "配置枠", "0/8", "slot");    // 🏛️ 領域：この階に置ける要素数（広げると増える）
        worldText = ResChip(bar, UITheme.Influence, "世界水準", "G Lv1", "world"); // 🌍 次に来る冒険者の目安（急に強くならないか事前に読めるように）
        FitBarWidth(bar);   // 📏 はみ出さないことを保証する
    }

    /// <summary>
    /// 📏 バーが画面幅からはみ出さないようにする**安全網**（Phase B）。
    /// 上部バーは実測 2,236px あって画面(1920)から **316px はみ出し、右端の資源チップが見切れていた**。
    /// 個々の幅を詰めて根本は直したが、**今後ボタンを足しても壊れない**ようにここで最後に均す。
    /// </summary>
    private void FitBarWidth(Image bar)
    {
        var h = bar.GetComponent<HorizontalLayoutGroup>();
        if (h == null) return;
        float fixedW = 0f; int n = 0;
        var les = new List<LayoutElement>();
        foreach (Transform ch in bar.transform)
        {
            var le = ch.GetComponent<LayoutElement>();
            if (le == null) continue;
            n++;
            if (le.preferredWidth > 0) { fixedW += le.preferredWidth; les.Add(le); }
        }
        float avail = UITheme.ScreenW - h.padding.left - h.padding.right - h.spacing * Mathf.Max(0, n - 1);
        if (fixedW <= avail || fixedW <= 0f) return;
        float k = avail / fixedW;
        foreach (var le in les) { le.preferredWidth *= k; le.minWidth = le.preferredWidth; }
        Debug.Log($"📏『バーを詰めた』{bar.name}：必要 {fixedW:0}px → 収まる {avail:0}px（×{k:0.00}）");
    }

    private TextMeshProUGUI ResChip(Graphic parent, Color accent, string label, string value, string icon = null)
    {
        // 🎨 Phase B：**幅118→86に圧縮**（6個で192px節約＝見切れの主因のひとつ）。
        //    ラベルを小さく上に、数値を大きく下に置く「縦2段」にすると、狭くても読める。
        var chip = Panel(parent, "Res_" + label, C("#1b1828"));
        SizeElem(chip.gameObject, 86, 42); Outline(chip, LINE);
        var accentBar = Panel(chip, "accent", accent);
        accentBar.rectTransform.anchorMin = new Vector2(0, 0); accentBar.rectTransform.anchorMax = new Vector2(0, 1);
        accentBar.rectTransform.pivot = new Vector2(0, 0.5f);
        accentBar.rectTransform.anchoredPosition = Vector2.zero;
        accentBar.rectTransform.sizeDelta = new Vector2(3, 0);
        // 🖼️ 手続き生成のアイコン（フォントに無い記号で□になる問題を根治する → [[UIIcons]]）
        float tx0 = 9f;
        if (!string.IsNullOrEmpty(icon))
        {
            var ic = Panel(chip.rectTransform, "ic", accent);
            ic.sprite = UIIcons.Get(icon); ic.type = Image.Type.Simple; ic.preserveAspect = true;
            ic.raycastTarget = false;
            Place(ic.rectTransform, 9, 12, 18, 18);
            tx0 = 31f;
        }
        var lab = Text(chip.rectTransform, label, 9.5f, FAINT, TextAlignmentOptions.Left);
        Place(lab.rectTransform, tx0, 4, 86 - tx0 - 6, 12);
        var val = Text(chip.rectTransform, value, 15.5f, accent, TextAlignmentOptions.Left, FontStyles.Bold);
        val.enableWordWrapping = false; val.enableAutoSizing = true; val.fontSizeMin = 9f; val.fontSizeMax = 15.5f;
        Place(val.rectTransform, tx0, 16, 86 - tx0 - 6, 20);
        return val;
    }

    // 🩸 魔王HPバー（上部HUD・Bloodlinesバー）
    private void BuildDemonLordHpBar(Graphic bar)
    {
        var wrap = Panel(bar, "DLHpBar", HUD_BG); SizeElem(wrap.gameObject, 176, 40); Outline(wrap, BLOOD_DK);
        dlHpBar = wrap.gameObject;
        dlHpLabel = Text(wrap.rectTransform, "魔王 Lv1", 10.5f, BLOOD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(dlHpLabel.rectTransform, 10, 5, 156, 14);

        var track = Panel(wrap.rectTransform, "track", C("#241014"));
        Place(track.rectTransform, 10, 21, DL_HP_TRACK_W, 12);
        ApplyFrame(track, barTrack, Color.white);

        dlHpFill = Panel(track.rectTransform, "fill", BLOOD);
        dlHpFill.rectTransform.anchorMin = new Vector2(0, 0.5f);
        dlHpFill.rectTransform.anchorMax = new Vector2(0, 0.5f);
        dlHpFill.rectTransform.pivot = new Vector2(0, 0.5f);
        dlHpFill.rectTransform.anchoredPosition = Vector2.zero;
        dlHpFill.rectTransform.sizeDelta = new Vector2(DL_HP_TRACK_W, 12);
        if (barFill != null)
        {
            dlHpFill.sprite = barFill; dlHpFill.color = Color.white;
            dlHpFill.type = Image.Type.Filled; dlHpFill.fillMethod = Image.FillMethod.Horizontal; dlHpFill.fillOrigin = 0;
        }
    }

    // ---------- ①迷宮生成パネル ----------
    private void BuildGenPanel(RectTransform root)
    {
        var panel = Panel(root, "GenPanel", PANEL);
        genPanel = panel.gameObject;
        Anchor(panel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        panel.rectTransform.sizeDelta = new Vector2(360, 612);
        panel.rectTransform.anchoredPosition = new Vector2(-16, -76);
        Outline(panel, LINE2); Round(panel, 14); SkinPanel(panel);

        float pad = 16f, w = 360 - pad * 2;

        // ヘッダ
        var eyebrow = Text(panel, "領域創造", 11, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(eyebrow.rectTransform, pad, 14, w, 16); eyebrow.characterSpacing = 8;
        var title = Text(panel, "迷宮を生成する", 19, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 30, w, 26);
        var sub = Text(panel, "タイプ・空間・宝箱量を選ぶと迷路が自動生成されます。生成後に罠やスポナーを手動配置してください。", 11.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(sub.rectTransform, pad, 58, w, 40);

        // 迷宮タイプ（2x2カード）
        var tl = Text(panel, "迷宮タイプ", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(tl.rectTransform, pad, 104, w, 16);
        string[] tNames = { "標準", "迷路", "大空洞", "蟻の巣" };
        // 🏔️ 形の説明ではなく **得と損** を出す（選ぶ理由が見えるように）
        string[] tDesc = {
            "配置枠+2 ／ 癖なし",
            "冒険者が長居+35% ／ 宝箱-25%",
            "部隊+10%・徘徊+1 ／ 集客-15%",
            "宝箱+50%・集客+20% ／ トーテム半径-1" };
        float cw = (w - 8) / 2f, chH = 50;
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            float cx = pad + (i % 2) * (cw + 8);
            float cy = 124 + (i / 2) * (chH + 8);
            var b = Card(panel, cx, cy, cw, chH, tNames[i], tDesc[i], () => { selType = idx; generator?.SetDungeonType(idx); RefreshSelections(); RefreshCost(); });
            typeBtns.Add(b);
        }

        // 空間タイプ（チップ 3+2）
        var sl = Text(panel, "空間タイプ", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(sl.rectTransform, pad, 240, w, 16);
        string[] sNames = { "洞窟", "遺跡", "城塞", "溶岩", "氷雪" };
        Color[] sCols = { C("#5a5560"), C("#5c6446"), C("#4e5674"), C("#7a3a30"), C("#4a6480") };
        float chipW = (w - 16) / 3f, chipH = 30;
        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            float cx = pad + (i % 3) * (chipW + 8);
            float cy = 260 + (i / 3) * (chipH + 8);
            var b = Chip(panel, cx, cy, chipW, chipH, sNames[i], sCols[i], () => { selSpace = idx; generator?.SetSpaceType(idx); RefreshSelections(); RefreshThemeEffect(); });
            AddTooltip(b.gameObject, sNames[i] + "：" + DungeonTheme.SpaceEffect((DungeonGenerator.SpaceType)idx));
            spaceBtns.Add(b);
        }
        // 🏔️ 選択中の空間タイプの効果（チップだけでは分からないので明示する）
        spaceEffectText = Text(panel, "", 10.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(spaceEffectText.rectTransform, pad, 322, w, 16);
        RefreshThemeEffect();

        // 🌍 地上の広さ（Civのマップサイズ相当）。盤は手続き生成なので毎回違う地形になる。
        var gl = Text(panel, "地上の広さ（Civ準拠。毎回違う地形が生成されます）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(gl.rectTransform, pad, 344, w, 16);
        var gSizes = new[] { SurfaceGen.Size.Tiny, SurfaceGen.Size.Small, SurfaceGen.Size.Medium, SurfaceGen.Size.Large };
        var gNames = new string[4];
        for (int i = 0; i < 4; i++) gNames[i] = SurfaceGen.NameOf(gSizes[i]) + " " + SurfaceGen.TileCount(gSizes[i]);
        surfaceSizeBtns.Clear();
        float gw = (w - 24) / 4f;
        for (int i = 0; i < 4; i++)
        {
            int gi = i;
            var b = Panel(panel, "GSize_" + i, PANEL2);
            Place(b.rectTransform, pad + i * (gw + 8), 362, gw, 26); Outline(b, LINE);
            var tx = Text(b.rectTransform, gNames[i], 11.5f, TEXT, TextAlignmentOptions.Center, FontStyles.Bold); StretchFull(tx.rectTransform);
            var bt = b.gameObject.AddComponent<Button>(); bt.targetGraphic = b;
            bt.onClick.AddListener(() =>
            {
                SurfaceMap.Regenerate(gSizes[gi], Random.Range(1, int.MaxValue));
                selectedRegionId = SurfaceMap.IndexOfCenter();
                if (surfaceView != null) { surfaceView.FitToBoard(); surfaceView.CenterOn(selectedRegionId); }
                RefreshSurfaceSizeBtns(); RefreshSurfacePanel();
            });
            AddTooltip(b.gameObject, gNames[i] + "タイル（幅" + SurfaceGen.WidthOf(gSizes[gi]) + "×高さ" + SurfaceGen.HeightOf(gSizes[gi])
                + "・東西がループします）\n引き切ると世界がちょうど1つ収まります。");
            surfaceSizeBtns.Add(b);
        }
        RefreshSurfaceSizeBtns();

        // 宝箱量
        var cl = Text(panel, "宝箱の量（階層の広さに比例して増えます）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(cl.rectTransform, pad, 400, w, 16);
        string[] cNames = { "少", "中", "多" };
        float ccw = (w - 16) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            float cx = pad + i * (ccw + 8);
            var b = Chip(panel, cx, 420, ccw, 30, cNames[i], GOLD, () => { selChest = idx; generator?.SetChestAmount(idx); RefreshSelections(); RefreshCost(); });
            chestBtns.Add(b);
        }

        // 階層数（多いほどコスト大・魔王まで遠い＝防御が深くなる）
        var fl = Text(panel, "階層数（深いほどコスト大・防御が深くなる）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(fl.rectTransform, pad, 458, w, 16);
        string[] fNames = { "1層", "2層", "3層" };
        float fcw = (w - 16) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            float cx = pad + i * (fcw + 8);
            var b = Chip(panel, cx, 478, fcw, 30, fNames[i], VIOLET, () => { selFloors = idx; floorMgr?.SetFloorCount(idx + 1); RefreshSelections(); RefreshCost(); });
            floorCountBtns.Add(b);
        }

        // コスト表示
        costText = Text(panel, "生成コスト  500 DP", 12.5f, MUTED, TextAlignmentOptions.Left);
        Place(costText.rectTransform, pad, 516, w, 18);

        // 生成ボタン
        generateBtn = PrimaryButton(panel, "迷宮を生成する", BLOOD, C("#f0d9a0"), () =>
        {
            if (generator == null) return;
            if (floorMgr != null) floorMgr.SetFloorCount(selFloors + 1);
            bool ok = generator.TryGenerateWithCost();
            RefreshCost();
            RefreshFloorTabs();
        }, true);
        Place((RectTransform)generateBtn.transform, pad, 540, w, 44);

        RefreshSelections();
    }

    // ---------- ③下部コマンドバー ----------
    private void BuildBottomBar(RectTransform root)
    {
        var bar = Panel(root, "BottomBar", HUD_BG);
        bottomBar = bar.gameObject;   // 🌍 地上モードでは隠す
        Anchor(bar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        bar.rectTransform.sizeDelta = new Vector2(0, 60); bar.rectTransform.anchoredPosition = Vector2.zero;
        AddTopBorder(bar);
        var h = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset((int)UITheme.S3, (int)UITheme.S3, 9, 9); h.spacing = 8; h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;

        var hint = Text(bar, "配置ツール", 11, FAINT, TextAlignmentOptions.Left);
        SizeElem(hint.gameObject, 68, 40);

        ToolButton(bar, "トーテム", TEAL, () => { input?.SetToolMode(6); ShowStripFor(6); }, 6, "トーテム：範囲に効果を撒く『面の層』。13種（強化/家系特化/冒険者弱体/罠・感情連携/回復）。種類は領域研究で解禁。");
        ToolButton(bar, "罠", CRIMSON, () => { input?.SetToolMode(3); ShowStripFor(3); }, 3, "罠：踏んだ冒険者にダメージと状態異常。種類は領域研究で解禁（盗賊はMPで解除）。");
        ToolButton(bar, "スポナー", VIOLET, () => { input?.SetToolMode(7); ShowStripFor(7); }, 7, "スポナー：戦闘中に雑魚を湧かせ続ける。数で消耗させる。");
        ToolButton(bar, "ボス", CRIMSON, () => { input?.SetToolMode(8); ShowStripFor(8); }, 8, "ボス任命：召喚した個体を各階1体だけボスに。強化＋大型化して出現する。");
        ToolButton(bar, "特殊敵", GOLD, () => { input?.SetToolMode(9); ShowStripFor(9); }, 9, "特殊敵：素材を払って6種から配置。強力な単体戦力。");
        ToolButton(bar, "宝箱", GREEN, () => { input?.SetToolMode(12); ShowStripFor(12); }, 12, "宝箱(誘導)：拾得装備を素材に錬成。集客を上げるが装備を奪われる両刃。錬成研究で解禁。");
        ToolButton(bar, "部隊", C("#8cb8e6"), () => { input?.SetToolMode(11); ShowStripFor(11); }, 11, "部隊：この階の隊員(個体)を1体ずつ好きなマスへ配置する。");
        ToolButton(bar, "消去", MUTED, () => { input?.SetToolMode(10); ShowStripFor(10); }, 10, "消去：配置した要素を撤去する（準備フェーズのみ・右クリックでも可）。");

        // 🧟 配下セレクタ（図鑑を開いてロスター16種から選ぶ）
        var sp = Text(bar, "配下", 11, FAINT, TextAlignmentOptions.Center);
        SizeElem(sp.gameObject, 40, 40);
        var codexBtn = PrimaryButton(bar, "図鑑 →", PANEL2, TEXT, () => { OpenExclusive(minionPanel); RefreshMinionCodex(); RefreshSquadTray(); });
        SizeElem(codexBtn.gameObject, 76, 42);
        minionBarLabel = Text(bar, "", 12, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        SizeElem(minionBarLabel.gameObject, 168, 42);
        UpdateMinionBarLabel();

        Spacer(bar);

        var extendBtn = PrimaryButton(bar, "時間+1分", PANEL2, TEXT, () => turn?.ExtendWaveLimit());
        SizeElem(extendBtn.gameObject, 104, 42);
        AddTooltip(extendBtn.gameObject, "DPを払って戦闘フェーズの制限時間を永続的に+1分（序盤3分）。");

        // ⏩ 戦闘の速度（Phase A-5）。3分をただ見ているだけの時間を短くし、見せ場では止められるように。
        speedBtns.Clear();
        for (int i = 0; i < DungeonTurnManager.SpeedNames.Length; i++)
        {
            int si = i;
            var b = Panel(bar, "Speed" + i, CARD); SizeElem(b.gameObject, 38, 42); Outline(b, LINE);
            var tx = Text(b.rectTransform, DungeonTurnManager.SpeedNames[i], 13, TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
            StretchFull(tx.rectTransform);
            var bt = b.gameObject.AddComponent<Button>(); bt.targetGraphic = b;
            bt.onClick.AddListener(() => { turn?.SetSpeed(si); RefreshSpeedBtns(); });
            AddTooltip(b.gameObject, si == 0 ? "一時停止（戦闘中だけ効きます）" : "戦闘を " + DungeonTurnManager.SpeedNames[si] + " の速さで進める");
            speedBtns.Add(b);
        }
        RefreshSpeedBtns();

        invadeBtn = PrimaryButton(bar, "⚔ 侵略開始", BLOOD, TEXT, () => turn?.StartBattlePhase(), true);
        SizeElem(invadeBtn.gameObject, 158, 42);
        FitBarWidth(bar);   // 📏 はみ出さないことを保証する
    }

    // ================= ライブ更新 =================
    // 🔄 配置・階層の変化をリアルタイムにストリップへ反映する。
    //    ⚠ 毎フレーム作り直すと押下中にButtonが破棄されてクリックが成立しない（既知の罠）。
    //      **署名を比べて変わったときだけ**作り直す。
    private string placementSig;
    private void RefreshOnPlacementChange()
    {
        if (featureMgr == null) return;
        var sb = new System.Text.StringBuilder();
        sb.Append(floorMgr != null ? floorMgr.CurrentFloorIndex : 0).Append('|')
          .Append(featureMgr.PlacedCount).Append('|')
          .Append(DungeonFeatureManager.SquadMaxSlots).Append('|');
        foreach (var id in featureMgr.CurrentSquad) sb.Append(id).Append(',');
        sb.Append('|').Append(featureMgr.PlacedIndividualsSig());
        string sig = sb.ToString();
        if (sig == placementSig) return;
        placementSig = sig;
        ShowStripFor(input != null ? input.CurrentToolMode : -1);   // 見えているストリップを作り直す
        RefreshSquadTray();
        if (minionPanel != null && minionPanel.activeSelf) RefreshMinionCodex();
    }


    private void Update()
    {
        RefreshOnPlacementChange();
        TickFades();
        SaveSystem.TickPlayTime(Time.unscaledDeltaTime);   // ⏱️ 遊んだ実時間（倍速に引っ張られない）
        // 🏁 勝敗が決したらリザルトへ（勝ちも負けも同じ画面。自分の勝ち以外は全部敗北）
        if (VictorySystem.Decided && GameSetup.Started && gameOverPanel != null && !gameOverPanel.activeSelf)
            ShowResult(VictorySystem.Winner == VictorySystem.Self);
        // 🔔 トースト：寿命を減らし、**変わったときだけ**並べ直す（毎フレーム作り直すとボタンが死ぬ）
        NotifySystem.Tick(Time.unscaledDeltaTime);
        string tsig = NotifySystem.Signature;
        if (NotifySystem.Dirty || tsig != toastSig)
        {
            NotifySystem.Dirty = false; toastSig = tsig;
            RefreshToasts();
            if (logPanel != null && logPanel.activeSelf) RefreshLogPanel();
        }
        // 🔦 発見：未読があれば開く（迷宮でも地上でも出す）
        if (DiscoverySystem.Pending >= 0 && discoveryPanel != null && !discoveryPanel.activeSelf
            && (titleRoot == null || !titleRoot.activeSelf))
        {
            RefreshDiscoveryPanel();
            discoveryPanel.SetActive(true);
            discoveryPanel.transform.SetAsLastSibling();
            PlayFadeIn(discoveryPanel);
            SoundSystem.Play(SoundSystem.Sfx.Discover);
        }
        // 📖 ターン頭の報告：未読があれば開く（地上を見ている間は盤の邪魔をせず、戻ってから出す）
        if (GuideSystem.Unread && !surfaceModeOn && GameSetup.Started
            && (titleRoot == null || !titleRoot.activeSelf))
        {
            GuideSystem.Unread = false;
            OpenGuide();
        }
        if (res != null)
        {
            SetNumber(dpText, res.DungeonPoints);
            SetNumber(fameText, res.DungeonFame);
            SetNumber(matText, res.CraftMaterials);
        }
        if (threatText != null) threatText.text = LureEconomy.ThreatLabel;
        if (slotText != null && featureMgr != null) slotText.text = featureMgr.PlacedCount + "/" + featureMgr.PlacementCap;
        if (worldText != null)
        {
            float wt = AdventurerAI.WorldTierNow();
            SetTxt(worldText, AdventurerAI.RankLetter(Mathf.RoundToInt(wt)) + " Lv" + AdventurerAI.ExpectedLevelNow());
        }
        if (turn != null)
        {
            if (turnText != null) turnText.text = "Turn " + turn.CurrentTurn;
            bool prep = turn.IsPreparePhase;
            if (phaseText != null)
            {
                if (prep) { phaseText.text = "準備フェーズ"; phaseText.color = GREEN; }
                else
                {
                    float rem = turn.RemainingWaveTime;
                    int mm = (int)(rem / 60f); int ss = (int)(rem % 60f);
                    SetTxt(phaseText, $"戦闘 {mm}:{ss:00}"); phaseText.color = CRIMSON;
                }
            }
            // 📯 号令は戦闘中だけ。⚠ 中身は作り直さず**値だけ**更新する（作り直すとクリックが成立しない）
            if (commandBar != null)
            {
                bool show = !prep;
                if (commandBar.activeSelf != show) { commandBar.SetActive(show); if (show) PlayFadeIn(commandBar); }
                if (show) RefreshCommandBar();
            }
            // ⚠️ 危険の可視化（Phase D-18）：いま何人入っていて、一番強いのは誰か
            if (dangerText != null)
            {
                if (prep) SetTxt(dangerText, "");
                else
                {
                    var advs = Object.FindObjectsByType<AdventurerAI>(FindObjectsInactive.Exclude);
                    int top = 0; float tp = 0f;
                    foreach (var a in advs) { if (a.CombatPower > tp) { tp = a.CombatPower; top = a.Level; } }
                    int floorNow = floorMgr != null ? floorMgr.CurrentFloorIndex + 1 : 1;
                    SetTxt(dangerText, advs.Length == 0
                        ? "<color=#5cc47c>侵入者なし</color>"
                        : $"<color=#e05a5a>侵入 {advs.Length}</color>　<color=#e08a3c>最強 Lv{top}</color>　<color=#9c95b4>B{floorNow}F</color>");
                }
            }
            if (phasePill != null) phasePill.color = prep ? C("#183726") : C("#3a1a1a");
            if (genPanel != null && genPanel.activeSelf != prep) genPanel.SetActive(prep);
            if (invadeBtn != null) invadeBtn.interactable = prep;
        }
        if (demonPanel != null && demonPanel.activeSelf)
        {
            string s = DemonPanelSig();
            if (s != dlSig) { dlSig = s; RefreshDemonPanel(); }
        }
        if (emotionPanel != null && emotionPanel.activeSelf)
        {
            string s = EmotionPanelSig();
            if (s != emoSig) { emoSig = s; RefreshEmotionPanel(); }
            else RefreshEmotionPools();
        }
        if (relicPanel != null && relicPanel.activeSelf) RefreshRelicPanel();
        RefreshFloorTabs();

        // 🩸 魔王HPバーのライブ更新
        if (dlHpFill != null)
        {
            var dl = DemonLord.Instance;
            float r = dl != null ? Mathf.Clamp01(dl.HPRatio) : 1f;
            if (dlHpFill.type == Image.Type.Filled) dlHpFill.fillAmount = r;
            else dlHpFill.rectTransform.sizeDelta = new Vector2(DL_HP_TRACK_W * r, dlHpFill.rectTransform.sizeDelta.y);
            if (dlHpLabel != null && dl != null) dlHpLabel.text = "魔王 Lv" + dl.Level;
            if (dlHpBar != null)
            {
                var cg = dlHpBar.GetComponent<CanvasGroup>(); if (cg == null) cg = dlHpBar.AddComponent<CanvasGroup>();
                cg.alpha = (dl != null && !dl.IsPresent) ? 0.35f : 1f; // 不在フロアでは淡色
            }
        }

        // descent演出のフェード制御（timeScaleに依存しないunscaledで動かす）
        if (descentToastTimer > 0f && descentToastCg != null)
        {
            descentToastTimer -= Time.unscaledDeltaTime;
            descentToastCg.alpha = descentToastTimer >= 0.5f ? 1f : Mathf.Clamp01(descentToastTimer / 0.5f);
            if (descentToastTimer <= 0f) descentToastCg.alpha = 0f;
        }
        if (floorFadeTimer > 0f && floorFadeCg != null)
        {
            floorFadeTimer -= Time.unscaledDeltaTime;
            floorFadeCg.alpha = Mathf.Clamp01(floorFadeTimer / FADE_DUR);
        }
    }

    private void RefreshCost()
    {
        if (costText == null || generator == null) return;
        int cost = generator.GetGenerationCost();
        SetTxt(costText, "生成コスト  <b><color=#e3a94a>" + cost.ToString("N0") + " DP</color></b>");
        if (generateBtn != null)
        {
            bool afford = res == null || res.DungeonPoints >= cost;
            generateBtn.interactable = afford;
        }
    }

    private void RefreshSelections()
    {
        for (int i = 0; i < typeBtns.Count; i++) SetSel(typeBtns[i], i == selType);
        for (int i = 0; i < spaceBtns.Count; i++) SetSel(spaceBtns[i], i == selSpace);
        for (int i = 0; i < chestBtns.Count; i++) SetSel(chestBtns[i], i == selChest);
        for (int i = 0; i < floorCountBtns.Count; i++) SetSel(floorCountBtns[i], i == selFloors);
    }

    // 🔧 選択中ツールのハイライト管理（mode → チップ）
    private readonly List<(Image img, int mode)> toolChips = new List<(Image, int)>();
    private int activeToolMode = -1;
    private void SetActiveTool(int mode)
    {
        activeToolMode = mode;
        foreach (var t in toolChips)
        {
            bool on = t.mode == mode;
            t.img.color = on ? SEL : CARD;
            var o = t.img.GetComponent<Outline>();
            if (o != null) { o.effectColor = on ? GOLD : LINE; o.effectDistance = on ? new Vector2(2, -2) : new Vector2(1, -1); }
        }
    }

    // ツールボタン（mode>=0 でハイライト対象／tip でツールチップ）
    private void ToolButton(Graphic bar, string label, Color accent, UnityAction onClick, int mode = -1, string tip = null)
    {
        var img = Panel(bar, "Tool_" + label, CARD); SizeElem(img.gameObject, 92, 40); Outline(img, LINE);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(() => SoundSystem.Play(SoundSystem.Sfx.Click));   // 🔊 押した手応え（全ボタン共通）
        btn.onClick.AddListener(onClick);
        if (mode >= 0)
        {
            toolChips.Add((img, mode));
            int m = mode;
            btn.onClick.AddListener(() => SetActiveTool(m));
        }
        if (!string.IsNullOrEmpty(tip)) AddTooltip(img.gameObject, tip);
        var dot = Panel(img.rectTransform, "dot", accent);
        dot.rectTransform.anchorMin = new Vector2(0, 0.5f); dot.rectTransform.anchorMax = new Vector2(0, 0.5f);
        dot.rectTransform.pivot = new Vector2(0, 0.5f); dot.rectTransform.anchoredPosition = new Vector2(10, 0);
        dot.rectTransform.sizeDelta = new Vector2(9, 9);
        var t = Text(img.rectTransform, label, 12, TEXT, TextAlignmentOptions.Center);
        StretchOffset(t.rectTransform, 22, 6, 6, 6);
    }
    // 眷属種族ボタン（選択ハイライト付き・コンパクト）
    private Image SpeciesButton(Graphic bar, string label, Color accent, UnityAction onClick)
    {
        var img = Panel(bar, "Species_" + label, CARD); SizeElem(img.gameObject, 54, 40); Outline(img, LINE);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(() => SoundSystem.Play(SoundSystem.Sfx.Click));   // 🔊 押した手応え（全ボタン共通）
        btn.onClick.AddListener(onClick);
        var t = Text(img.rectTransform, label, 12, accent, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(t.rectTransform);
        return img;
    }
    private void RefreshSpecies()
    {
        for (int i = 0; i < speciesBtns.Count; i++) SetSel(speciesBtns[i], i == selSpecies);
    }

    private void ToolButtonDisabled(Graphic bar, string label)
    {
        var img = Panel(bar, "Tool_" + label, C("#141220")); SizeElem(img.gameObject, 108, 40); Outline(img, C("#252036"));
        var t = Text(img.rectTransform, label, 11.5f, FAINT, TextAlignmentOptions.Center);
        StretchFull(t.rectTransform);
    }
}
