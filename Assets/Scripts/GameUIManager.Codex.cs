using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 配下図鑑と、下部バーの上に出る配置ストリップ群（部隊／ボス／特殊／罠／トーテム）と個体装備。
/// <para>`GameUIManager` の partial。フィールドの本体は GameUIManager.cs 側にある。</para>
/// </summary>
public partial class GameUIManager
{

    // ---------- 配下図鑑（全画面・家系タブ＋段階グループのカードグリッド／CDO2風） ----------
    private void BuildMinionCodex(RectTransform root)
    {
        var panel = Panel(root, "MinionCodex", PANEL);
        minionPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(FS_W, FS_H);
        panel.rectTransform.anchoredPosition = new Vector2(0, 0);
        Outline(panel, LINE2); SkinPanel(panel);

        float pad = 26f;
        var title = Text(panel, "配下図鑑（家系タブ→段階で選ぶ／進化の系統を一覧）", 17, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 16, FS_W - 240, 24);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => minionPanel.SetActive(false));
        Place((RectTransform)close.transform, FS_W - pad - 32, 14, 32, 30);

        // 左：家系タブ（全体/不死/獣/魔族）＋個体(装備)タブ 縦並び
        codexTabBtns.Clear();
        string[] fam = { "全体", "不死", "獣", "魔族", "個体" };
        Color[] famCol = { TEXT, GREEN, GOLD, VIOLET, C("#8cb8e6") };
        float tabX = pad, tabY0 = 66f, tabW = 128f, tabH = 46f, tabGap = 8f;
        for (int i = 0; i < fam.Length; i++)
        {
            int idx = i;
            var b = Panel(panel, "CodexTab_" + i, CARD);
            Place(b.rectTransform, tabX, tabY0 + i * (tabH + tabGap), tabW, tabH); Outline(b, LINE);
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
            btn.onClick.AddListener(() => { codexFamilyTab = idx; RefreshMinionCodex(); });
            var tt = Text(b.rectTransform, fam[i], 14, famCol[i], TextAlignmentOptions.Center, FontStyles.Bold); StretchFull(tt.rectTransform);
            codexTabBtns.Add(b);
        }

        // 右：スクロールするカードグリッド
        float contentX = tabX + tabW + 18f;
        codexContentW = FS_W - contentX - pad;
        float footerH = 116f;
        float contentH = FS_H - 66f - footerH - 10f;
        minionListContainer = MakeVScroll(panel, contentX, 66f, codexContentW, contentH);

        // 下：部隊編成トレイ（固定フッタ）
        float footTop = FS_H - footerH;
        var trayLabel = Text(panel, "部隊編成（役割を散らすほど部隊バフ↑）／＋隊で追加 → 図鑑を閉じ『部隊』ツールで個別配置", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(trayLabel.rectTransform, contentX, footTop + 8, codexContentW, 16);
        var slots = NewRect("SquadSlots", panel.rectTransform);
        Place(slots, contentX, footTop + 30, codexContentW - 132f, 32);
        squadSlotContainer = slots;
        // ⚠⚠ 幅と『クリア』の位置を **5枠べた書き** にしていたので、研究で6枠目が増えた瞬間
        //    スロットがボタンの下に潜って読めなくなった。位置は `RefreshSquadTray` で毎回引き直す。
        squadClearBtn = PrimaryButton(panel, "クリア", PANEL2, TEXT, () => { featureMgr?.SquadClear(); RefreshSquadTray(); RefreshMinionCodex(); });
        Place((RectTransform)squadClearBtn.transform, contentX + 5 * 108f + 12f, footTop + 30, 120, 32);
        squadTrayLeft = contentX; squadTrayTop = footTop + 30f;
        squadInfoText = Text(panel, "", 12.5f, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(squadInfoText.rectTransform, contentX, footTop + 72, codexContentW, 18);

        RefreshMinionCodex();
        RefreshSquadTray();
        minionPanel.SetActive(false);
    }


    // 🛡️ 編成トレイの再描画（5枠：個体名/空、クリックで抜く）＋コスト/コンプ表示
    private void RefreshSquadTray()
    {
        if (squadSlotContainer == null || featureMgr == null) return;
        for (int i = squadSlotContainer.childCount - 1; i >= 0; i--)
        {
            var c = squadSlotContainer.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        var squad = featureMgr.CurrentSquad; // 🧬 個体IDのリスト（この階の隊）
        int nSlots = DungeonFeatureManager.SquadMaxSlots;
        // 🧩 枠数は研究・政策・属性で増える。**幅から逆算して縮める**（増えた枠が
        //    『クリア』の下へ潜ったり、パネルからはみ出したりしないように）。
        const float ClearW = 120f, ClearGap = 12f;
        float avail = Mathf.Max(200f, codexContentW - ClearW - ClearGap);
        float slotW = Mathf.Min(108f, avail / Mathf.Max(1, nSlots));
        float slotH = 30;
        if (squadClearBtn != null)
            Place((RectTransform)squadClearBtn.transform, squadTrayLeft + nSlots * slotW + ClearGap, squadTrayTop, ClearW, 32);
        for (int i = 0; i < nSlots; i++)
        {
            int slot = i;
            var chip = Panel(squadSlotContainer, "Slot_" + i, CARD); Place(chip.rectTransform, i * slotW, 0, slotW - 6, slotH); Outline(chip, LINE);
            bool filled = i < squad.Count;
            var v = filled ? MinionRoster.Get(squad[i]) : null;
            string label = v != null ? MinionCatalog.Get(v.catalogIndex).jpName + " <size=76%>Lv" + v.level + "</size>" : "空";
            var col = v != null ? RoleColor(MinionCatalog.Get(v.catalogIndex).role) : FAINT;
            var tt = Text(chip.rectTransform, label, 10.5f, col, TextAlignmentOptions.Center, FontStyles.Bold); StretchFull(tt.rectTransform);
            if (filled)
            {
                var b = chip.gameObject.AddComponent<Button>(); b.targetGraphic = chip;
                b.onClick.AddListener(() => { featureMgr.SquadRemoveAt(slot); RefreshSquadTray(); RefreshMinionCodex(); });
            }
        }
        if (squadInfoText != null)
        {
            int roles = featureMgr.SquadDistinctRoles(); float comp = featureMgr.SquadCompMult();
            int n = squad.Count;
            var fmgr = DungeonFloorManager.Instance;
            string floorLbl = "B" + ((fmgr != null ? fmgr.CurrentFloorIndex : 0) + 1) + "F";
            squadInfoText.text = n == 0 ? "<color=#9c95b4>" + floorLbl + " の隊は空です。『個体』タブで＋隊 → 下部バー『部隊』で配置</color>"
                : string.Format("<color=#8cb8e6>{0}</color> の隊　役割{1}種　部隊バフ <color=#5cc47c>×{2:0.00}</color>　<size=88%><color=#9c95b4>（階層ごとに別の隊を編成できます）</color></size>", floorLbl, roles, comp);
        }
        RefreshSquadStrip();
    }

    // 🎯 隊員配置ストリップ（下部バー上・2段）：上=種類(隊)を選ぶ、下=その種類の個体(Lv)を選ぶ→マスクリックで個別配置。
    private void BuildSquadStrip(RectTransform root)
    {
        var panel = Panel(root, "SquadStrip", C("#0e0b16"));
        Anchor(panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        panel.rectTransform.sizeDelta = new Vector2(700, 44);
        panel.rectTransform.anchoredPosition = new Vector2(0, 66);
        Outline(panel, LINE2);
        squadStrip = panel.gameObject;
        RefreshSquadStrip();
        squadStrip.SetActive(false); // 表示は『部隊』ツールで制御（ShowStripFor）
    }


    private void ShowStripFor(int mode)
    {
        if (squadStrip != null) squadStrip.SetActive(mode == 11);
        if (bossStrip != null) bossStrip.SetActive(mode == 8);
        if (trapStrip != null) trapStrip.SetActive(mode == 3);
        if (totemStrip != null) totemStrip.SetActive(mode == 6);
        if (specialStrip != null) specialStrip.SetActive(mode == 9);
        if (mode == 11) RefreshSquadStrip();
        else if (mode == 8) RefreshBossStrip();
        else if (mode == 3) RefreshTrapStrip();
        else if (mode == 6) RefreshTotemStrip();
        else if (mode == 9) RefreshSpecialStrip();
    }

    private void RefreshSquadStrip()
    {
        if (squadStrip == null || featureMgr == null) return;
        for (int i = squadStrip.transform.childCount - 1; i >= 0; i--)
        {
            var c = squadStrip.transform.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        var strip = (RectTransform)squadStrip.transform;
        var squad = featureMgr.CurrentSquad; // 🧬 個体IDのリスト
        var fmgr = DungeonFloorManager.Instance;
        string floorLbl = "B" + ((fmgr != null ? fmgr.CurrentFloorIndex : 0) + 1) + "F";
        var lbl = Text(strip, floorLbl + " の隊員 →", 10.5f, C("#8cb8e6"), TextAlignmentOptions.Left, FontStyles.Bold);
        Place(lbl.rectTransform, 12, 12, 92, 15);
        if (squad.Count == 0)
        {
            var h = Text(strip, "<color=#9c95b4>図鑑の『個体』タブで『＋隊』して編成してください（隊は階層ごと）</color>", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(h.rectTransform, 108, 12, 460, 16);
            strip.sizeDelta = new Vector2(580, 44);
            return;
        }
        int sel = Mathf.Clamp(featureMgr.SquadPlaceSlot, 0, squad.Count - 1);

        // 隊員＝個体そのもの。配置済みは淡色、未配置のみ選択可。
        float bw = 128, x0 = 108;
        for (int i = 0; i < squad.Count; i++)
        {
            int slot = i; int id = squad[i];
            var v = MinionRoster.Get(id);
            var b = Panel(strip, "Member_" + i, CARD);
            Place(b.rectTransform, x0 + i * (bw + 4), 7, bw, 28); Outline(b, LINE);
            bool placed = featureMgr.IsIndividualPlaced(id);
            string nm = v != null ? MinionCatalog.Get(v.catalogIndex).jpName + " <size=76%>Lv" + v.level + "</size>" : "?";
            var col = v != null ? RoleColor(MinionCatalog.Get(v.catalogIndex).role) : FAINT;
            var tt = Text(b.rectTransform, nm, 10f, placed ? FAINT : col, TextAlignmentOptions.Center, FontStyles.Bold);
            StretchFull(tt.rectTransform);
            if (!placed)
            {
                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
                btn.onClick.AddListener(() => { featureMgr.SetSquadPlaceSlot(slot); input?.SetToolMode(11); RefreshSquadStrip(); });
                SetSel(b, i == sel);
            }
            else b.color = C("#0f0d16"); // 配置済は暗く
        }
        strip.sizeDelta = new Vector2(x0 + squad.Count * (bw + 4) + 8, 44);
    }

    // 👑 ボス任命ストリップ（『ボス』ツールで表示）：召喚した全個体から1体を選び、マスをクリックでこのフロアのボスに。
    private void BuildBossStrip(RectTransform root)
    {
        var panel = Panel(root, "BossStrip", C("#0e0b16"));
        Anchor(panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        panel.rectTransform.sizeDelta = new Vector2(BossStripW, 46);
        panel.rectTransform.anchoredPosition = new Vector2(0, 66);
        Outline(panel, LINE2);
        bossStrip = panel.gameObject;

        // 見出し（固定）＋ 個体リスト（横スクロール）。所持個体が増えても見切れないようにする。
        bossStripLabel = Text(panel, "", 11, CRIMSON, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(bossStripLabel.rectTransform, 12, 4, BossStripW - 24, 16);
        bossStripContent = MakeHScroll(panel, 8, 21, BossStripW - 16, 24);

        RefreshBossStrip();
        bossStrip.SetActive(false);
    }

    private void RefreshBossStrip()
    {
        if (bossStrip == null || featureMgr == null || bossStripContent == null) return;
        var c = bossStripContent;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }

        // 見出し＋このフロアの現ボス状態
        int bossId = featureMgr.CurrentBossIndividualId();
        string status = "未設定";
        if (bossId >= 0)
        {
            var bi = MinionRoster.Get(bossId);
            status = bi != null ? ("現ボス " + MinionCatalog.Get(bi.catalogIndex).jpName + " Lv" + bi.level) : "設定済";
        }
        var allInd = MinionRoster.All;
        SetTxt(bossStripLabel, "◆ボス任命：個体を選び→マスをクリックでこの階のボスに → <color=#9c95b4>(" + status + ")</color>"
            + "  <size=90%><color=#6f6889>所持 " + allInd.Count + "体・横にスクロールできます</color></size>");

        float bw = 130, gap = 4;
        if (allInd.Count == 0)
        {
            var hint = Text(c, "<color=#6f6889>図鑑で『召喚』して個体を作成してください</color>", 11, FAINT, TextAlignmentOptions.MidlineLeft);
            Place(hint.rectTransform, 4, 4, 360, 18);
            c.sizeDelta = new Vector2(380, 0f);
            return;
        }
        int curInd = featureMgr.SelectedIndividualId;
        int shown = 0;
        for (int i = 0; i < allInd.Count; i++)
        {
            var v = allInd[i]; int id = v.id;
            bool placed = featureMgr.IsIndividualPlaced(id);
            int inSquad = featureMgr.SquadFloorOfIndividual(id);   // 👑 隊に居る個体はボスにできない（実体は1つ）
            bool away = KinRoster.IsAwayFromDungeon(id);           // 🗺️ 地上に出ている個体もボスにできない
            bool busy = placed || inSquad >= 0 || away;
            var d = MinionCatalog.Get(v.catalogIndex);
            var b = Panel(c, "BI_" + id, CARD);
            Place(b.rectTransform, shown * (bw + gap), 1, bw, 22); Outline(b, LINE);
            string sfx = inSquad >= 0 ? " <size=80%><color=#6f6889>B" + (inSquad + 1) + "F隊</color></size>"
                       : away ? " <size=80%><color=#6f6889>地上</color></size>" : "";
            var tt = Text(b.rectTransform, d.jpName + " Lv" + v.level + sfx, 9.5f, busy ? FAINT : RoleColor(d.role), TextAlignmentOptions.Center, FontStyles.Bold);
            StretchFull(tt.rectTransform);
            if (inSquad >= 0) AddTooltip(b.gameObject, "B" + (inSquad + 1) + "F の隊に編成済み。先に隊から外すとボスに任命できます。");
            else if (away) AddTooltip(b.gameObject, "眷属またはその配下として地上に出ています。");
            if (!busy)
            {
                int cat = v.catalogIndex;
                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
                btn.onClick.AddListener(() => { featureMgr.SetSelectedMinion(cat); featureMgr.SetPlaceIndividual(id); input?.SetToolMode(8); RefreshBossStrip(); });
                SetSel(b, id == curInd);
                // 🜏 任命したら継ぐ魔神の名と加護
                AddTooltip(b.gameObject, GoetiaCatalog.TitleOf(id) + " を継ぐ ／ " + GoetiaCatalog.Blessing(GoetiaCatalog.PillarOf(id).rank));
            }
            else b.color = C("#0f0d16");
            shown++;
        }
        c.sizeDelta = new Vector2(shown * (bw + gap) + 8, 0f);
    }

    // 👾 特殊エネミー種類ストリップ（『特殊敵』ツールで表示）：6種のGDDから選んでマスに配置。
    private void BuildSpecialStrip(RectTransform root)
    {
        var panel = Panel(root, "SpecialStrip", C("#0e0b16"));
        Anchor(panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        panel.rectTransform.sizeDelta = new Vector2(760, 40);
        panel.rectTransform.anchoredPosition = new Vector2(0, 66);
        Outline(panel, LINE2);
        specialStrip = panel.gameObject;
        RefreshSpecialStrip();
        specialStrip.SetActive(false);
    }

    private void RefreshSpecialStrip()
    {
        if (specialStrip == null || featureMgr == null) return;
        for (int i = specialStrip.transform.childCount - 1; i >= 0; i--)
        {
            var c = specialStrip.transform.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        var strip = (RectTransform)specialStrip.transform;
        // 👾 種類ではなく**持っている個体**を並べる。育てた1体をそのまま盤に立てるため。
        var owned = MinionRoster.Uniques();
        var lbl = Text(strip, owned.Count > 0 ? "ユニーク →" : "ユニーク（未所持）", 11, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(lbl.rectTransform, 12, 12, 96, 16);
        if (owned.Count == 0)
        {
            var non = Text(strip, "<color=#6f6889>ガチャで引き当てると、ここに並びます。</color>", 11, FAINT, TextAlignmentOptions.Left);
            Place(non.rectTransform, 112, 12, 420, 16);
            strip.sizeDelta = new Vector2(560, 40);
            return;
        }
        int sel = featureMgr.SelectedUniqueId;
        float bw = 132, x0 = 112;
        for (int k = 0; k < owned.Count; k++)
        {
            var v = owned[k];
            var d = MinionCatalog.Get(v.catalogIndex);
            bool placed = featureMgr.IsIndividualPlaced(v.id);
            var b = Panel(strip, "Sp_" + v.id, CARD);
            Place(b.rectTransform, x0 + k * (bw + 4), 5, bw, 30); Outline(b, placed ? LINE : GOLD);
            if (!placed)
            {
                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
                btn.onClick.AddListener(() => { featureMgr.SetSelectedUniqueId(v.id); input?.SetToolMode(9); RefreshSpecialStrip(); });
            }
            var tt = Text(b.rectTransform, d.jpName + " <size=84%>#" + v.id + " Lv" + v.level + "</size>",
                10f, placed ? FAINT : GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
            StretchFull(tt.rectTransform);
            AddTooltip(b.gameObject, d.jpName + "（" + MinionCatalog.RankName(d.rank) + "）\n" + d.note
                + (placed ? "\n<color=#e08a3c>もう盤に出ています。</color>" : "\n押してからマスを選ぶと置けます。"));
            SetSel(b, v.id == sel && !placed);
        }
        strip.sizeDelta = new Vector2(x0 + owned.Count * (bw + 4) + 8, 40);
    }

    // 🪤 罠の種類ストリップ（『罠』ツールで種類を選ぶ。ロック=領域研究で未解禁）
    private void BuildTrapStrip(RectTransform root)
    {
        var panel = Panel(root, "TrapStrip", C("#0e0b16"));
        Anchor(panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        panel.rectTransform.sizeDelta = new Vector2(780, 40);
        panel.rectTransform.anchoredPosition = new Vector2(0, 150);
        Outline(panel, LINE2);
        var lbl = Text(panel, "罠の種類 →", 11, CRIMSON, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(lbl.rectTransform, 12, 12, 84, 16);
        trapStrip = panel.gameObject;
        RefreshTrapStrip();
        trapStrip.SetActive(false);
    }

    private void RefreshTrapStrip()
    {
        if (trapStrip == null || featureMgr == null) return;
        for (int i = trapStrip.transform.childCount - 1; i >= 1; i--)
        {
            var c = trapStrip.transform.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        int sel = featureMgr.SelectedTrapKind;
        float bw = 110, x0 = 100;
        for (int k = 0; k < TrapCatalog.Count; k++)
        {
            int kk = k; var d = TrapCatalog.Get(k);
            bool unlocked = TrapCatalog.IsUnlocked(k);
            var b = Panel(trapStrip.transform, "Trap_" + k, CARD);
            Place(b.rectTransform, x0 + k * (bw + 4), 5, bw, 30); Outline(b, LINE);
            // 🖼️ 罠アイコン（該当あるもののみ：通常=棘/炎=火球/出血=槍）
            string ticon = k == 0 ? "icon_trap_spikes" : k == 2 ? "icon_fireball" : k == 5 ? "icon_trap_spears" : null;
            if (ticon != null) IconImg(b.rectTransform, ticon, 5, 5, 20, unlocked ? d.color : FAINT);
            var tt = Text(b.rectTransform, d.name + (unlocked ? " <size=78%><color=#9c95b4>" + d.dpCost + "</color></size>" : " ×"), 10.5f, unlocked ? d.color : FAINT, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(tt.rectTransform, ticon != null ? 26 : 4, 0, bw - (ticon != null ? 28 : 6), 30); tt.alignment = TextAlignmentOptions.Center;
            if (unlocked)
            {
                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
                btn.onClick.AddListener(() => { featureMgr.SetSelectedTrapKind(kk); input?.SetToolMode(3); RefreshTrapStrip(); });
            }
            SetSel(b, k == sel && unlocked);
        }
        ((RectTransform)trapStrip.transform).sizeDelta = new Vector2(x0 + TrapCatalog.Count * (bw + 4) + 8, 40);
    }

    // 🗿 トーテムストリップ（『トーテム』ツールで表示）：13種から選んで配置する。
    private void BuildTotemStrip(RectTransform root)
    {
        var panel = Panel(root, "TotemStrip", C("#0e0b16"));
        Anchor(panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        panel.rectTransform.sizeDelta = new Vector2(1400, 40);
        panel.rectTransform.anchoredPosition = new Vector2(0, 66);
        Outline(panel, LINE2);
        totemStrip = panel.gameObject;
        RefreshTotemStrip();
        totemStrip.SetActive(false);
    }

    private void RefreshTotemStrip()
    {
        if (totemStrip == null || featureMgr == null) return;
        for (int i = totemStrip.transform.childCount - 1; i >= 0; i--)
        {
            var c = totemStrip.transform.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        int sel = featureMgr.SelectedTotemKind;
        float bw = 104, x0 = 6;
        for (int k = 0; k < TotemCatalog.Count; k++)
        {
            int kk = k; var d = TotemCatalog.Get(k);
            var col = C(d.colorHex);
            bool unlocked = TotemCatalog.IsUnlocked(k);
            var b = Panel(totemStrip.transform, "Totem_" + k, CARD);
            Place(b.rectTransform, x0 + k * (bw + 4), 5, bw, 30); Outline(b, LINE);
            IconImg(b.rectTransform, d.icon, 5, 6, 18, unlocked ? col : FAINT);
            var tt = Text(b.rectTransform, d.jpName + (unlocked ? "\n<size=76%><color=#9c95b4>" + d.dpCost + "DP</color></size>" : "\n<size=76%>― 未解禁</size>"),
                9.5f, unlocked ? col : FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(tt.rectTransform, 26, 0, bw - 28, 30);
            AddTooltip(b.gameObject, d.jpName + "：" + d.desc + "（半径" + d.radius + "・重ねがけ2まで）"
                + (unlocked ? "" : "\n<color=#e05a5a>領域研究が必要</color>"));
            if (unlocked)
            {
                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
                btn.onClick.AddListener(() => { featureMgr.SetSelectedTotemKind(kk); input?.SetToolMode(6); RefreshTotemStrip(); });
            }
            SetSel(b, k == sel && unlocked);
        }
        ((RectTransform)totemStrip.transform).sizeDelta = new Vector2(x0 + TotemCatalog.Count * (bw + 4) + 8, 40);
    }

    private static Color RoleColor(MinionCatalog.Role r)
    {
        switch (r)
        {
            case MinionCatalog.Role.Tank: return C("#57c3ab");
            case MinionCatalog.Role.Melee: return C("#df5a5a");
            case MinionCatalog.Role.Ranged: return C("#b48be6");
            case MinionCatalog.Role.Buff: return C("#e3a94a");
            default: return C("#5cc47c"); // Debuff
        }
    }

    // ランク色（S/A=高位=金赤、B/C=青、D以下=淡色）。リッチテキスト用の16進。
    private static string RankHex(MinionCatalog.Rank r)
    {
        switch (r)
        {
            case MinionCatalog.Rank.S: return "#ffd24a";
            case MinionCatalog.Rank.A: return "#e88a4a";
            case MinionCatalog.Rank.B: return "#8cb8e6";
            case MinionCatalog.Rank.C: return "#79a9d6";
            default: return "#9c95b4"; // D/E/F/G
        }
    }

    private void RefreshMinionCodex()
    {
        if (minionListContainer == null) return;
        for (int i = 0; i < codexTabBtns.Count; i++) SetSel(codexTabBtns[i], i == codexFamilyTab);
        // 既存を破棄して作り直し（Destroyは遅延実行なので、まず非表示化して同フレームの重なりを防ぐ）
        for (int i = minionListContainer.childCount - 1; i >= 0; i--)
        {
            var c = minionListContainer.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        float W = codexContentW; if (W < 60f) W = 1400f;
        int selIdx = featureMgr != null ? featureMgr.SelectedMinionIndex : -1;

        // 🧬 個体タブ：召喚した個体ごとに武器/防具スロットを装備（PE）
        if (codexFamilyTab == 4) { RefreshCodexIndividuals(W); return; }

        // 表示する家系（全体=3家系スタック、個別=1家系）
        var fams = new List<ZombieAI.Species>();
        if (codexFamilyTab <= 0) { fams.Add(ZombieAI.Species.Undead); fams.Add(ZombieAI.Species.Beast); fams.Add(ZombieAI.Species.Demonkin); }
        else fams.Add((ZombieAI.Species)(codexFamilyTab - 1));
        bool showFamHead = fams.Count > 1;

        // ⚠⚠ 段を足したら**ここも一緒に増やす**。4で止めていたせいで
        //    王種(depth4)・古代種(depth5) が**図鑑に一度も出てこなかった**（実際に起きた）。
        //    段数は数えて出す＝次に段が増えても勝手に載る。
        string[] stageNames = { "基本", "進化Ⅰ", "上位Ⅱ", "最上位Ⅲ", "王種Ⅳ", "古代種Ⅴ" };
        int maxStage = 0;
        for (int k = 0; k < MinionCatalog.Count; k++) maxStage = Mathf.Max(maxStage, MinionEvolution.Depth(k));
        string[] famNames = { "不死", "獣", "魔族" };
        Color[] famCols = { GREEN, GOLD, VIOLET };
        float cardW = 224f, cardH = 126f, gap = 12f;
        int cols = Mathf.Max(1, (int)((W + gap) / (cardW + gap)));
        float y = 4f;

        foreach (var famv in fams)
        {
            if (showFamHead)
            {
                int fi = (int)famv;
                var fh = Text(minionListContainer, "◆ " + famNames[fi] + " 系統", 15, famCols[fi], TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(fh.rectTransform, 2, y, W - 4, 22); y += 30f;
            }
            for (int stage = 0; stage <= maxStage; stage++)
            {
                var idxs = new List<int>();
                for (int k = 0; k < MinionCatalog.Count; k++)
                {
                    var d = MinionCatalog.Get(k);
                    if (d.family != famv || MinionEvolution.Depth(k) != stage) continue;
                    idxs.Add(k);
                }
                if (idxs.Count == 0) continue;
                string stName = stage < stageNames.Length ? stageNames[stage] : "第" + (stage + 1) + "段";
                var sh = Text(minionListContainer, stName + "  <size=80%><color=#6f6889>(" + idxs.Count + ")</color></size>", 12.5f, MUTED, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(sh.rectTransform, 6, y, W - 8, 18); y += 24f;
                for (int n = 0; n < idxs.Count; n++)
                {
                    int col = n % cols, rr = n / cols;
                    AddCodexCard(minionListContainer, idxs[n], col * (cardW + gap), y + rr * (cardH + gap), cardW, cardH, selIdx);
                }
                int rows = (idxs.Count + cols - 1) / cols;
                y += rows * (cardH + gap) + 8f;
            }
            y += 12f;
        }
        minionListContainer.sizeDelta = new Vector2(0f, y + 12f);
    }

    // 図鑑カード1枚（種類＝MinionCatalog index）。名前/役割/ランク/ステータス/個体情報＋＋隊/召喚/進化。
    private void AddCodexCard(RectTransform parent, int kk, float x, float y, float w, float h, int selIdx)
    {
        var d = MinionCatalog.Get(kk);
        bool unlocked = MinionEvolution.IsUnlocked(kk);
        var card = Panel(parent, "Card_" + d.id, CARD);
        Place(card.rectTransform, x, y, w, h); Outline(card, LINE);
        var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
        btn.onClick.AddListener(() => { if (unlocked) { featureMgr?.SetSelectedMinion(kk); UpdateMinionBarLabel(); } RefreshMinionCodex(); });

        var nm = Text(card.rectTransform, d.jpName, 14, unlocked ? TEXT : FAINT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(nm.rectTransform, 10, 7, w - 20, 18);
        var role = Text(card.rectTransform, "[" + MinionCatalog.RoleName(d.role) + "] <color=" + RankHex(d.rank) + ">" + MinionCatalog.RankName(d.rank) + "</color>", 11, RoleColor(d.role), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(role.rectTransform, 10, 27, w - 20, 15);
        var stat = Text(card.rectTransform, string.Format("T{0}  HP×{1:0.00} ATK×{2:0.00} SPD×{3:0.00}", d.tierCP, d.hpMult, d.atkMult, d.spdMult), 10, MUTED, TextAlignmentOptions.TopLeft);
        Place(stat.rectTransform, 10, 45, w - 20, 14);
        // 💫 スキル／🔮 魔法（術者のみ）
        string skl = MinionSkill.Label(kk);
        MagicCatalog.Spell msp;
        if (MagicCatalog.TryPickMinionSpell(kk, out msp))
            skl += "<color=" + msp.colorHex + ">◆" + msp.jpName + "</color>";
        else if (d.style == CharacterVisual.AttackStyle.Cast)
            skl += "<color=#6f6889>・魔法未解禁</color>";
        var sk = Text(card.rectTransform, skl, 9.5f, TEXT, TextAlignmentOptions.TopLeft);
        Place(sk.rectTransform, 10, 59, w - 20, 14);
        var note = Text(card.rectTransform, "", 9.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(note.rectTransform, 10, 74, w - 20, 16);

        if (unlocked)
        {
            // 🧬 個体情報（数＋最高Lv）
            int cnt = MinionRoster.CountOfType(kk); int top = MinionRoster.TopLevelOfType(kk);
            note.text = cnt > 0
                ? "<color=#8cb8e6>個体 " + cnt + " 体 ・ 最高Lv " + top + "</color>"
                : "<color=#6f6889>未召喚（召喚で個体を作成）</color>";
            // ・ 隊の編成は『個体』タブで個体ごとに行う（同じ個体を二重に置けないようにするため）
            // 召喚（DPで個体を1体追加）
            int scost = MinionRoster.SummonCost(kk);
            var sumBtn = PrimaryButton(card, "召喚 -" + scost, BLOOD, TEXT, () => { if (MinionRoster.TrySummon(kk) != null) { RefreshMinionCodex(); RefreshSquadStrip(); } }, true);
            Place((RectTransform)sumBtn.transform, w - 116, h - 28, 106, 22);
        }
        else
        {
            string pn = MinionEvolution.PrereqName(kk);
            if (MinionEvolution.CanEvolve(kk))
                SetTxt(note, "<color=#e3a94a>◆ " + pn + " から進化可 ・ " + MinionEvolution.EvolveCost(kk) + "DP</color>");
            else if (MinionEvolution.TierResearchNeeded(kk))
                SetTxt(note, "<color=#8cb8e6>・ 研究で開放（" + MinionEvolution.TierResearchName(kk) + "）</color>");
            else
                SetTxt(note, "<color=#9c95b4>― " + pn + " の解禁が必要</color>");
            if (MinionEvolution.CanEvolve(kk))
            {
                var evoBtn = PrimaryButton(card, "進化", BLOOD, TEXT, () => { if (MinionEvolution.TryEvolve(kk)) RefreshMinionCodex(); }, true);
                Place((RectTransform)evoBtn.transform, w - 62, h - 28, 52, 22);
            }
        }
        SetSel(card, kk == selIdx);
    }

    private void UpdateMinionBarLabel()
    {
        if (minionBarLabel == null || featureMgr == null) return;
        var d = featureMgr.SelectedMinion;
        SetTxt(minionBarLabel, d.jpName + " <size=78%><color=#9c95b4>[" + MinionCatalog.RoleName(d.role) + "/T" + d.tierCP + "]</color></size>");
    }

    // 🧬⚔️🛡️ 個体タブ：召喚した個体ごとに武器/防具スロットを鍛造・装着（PE）。
    private void RefreshCodexIndividuals(float W)
    {
        var all = MinionRoster.All;
        float y = 4f;
        if (all.Count == 0)
        {
            var h = Text(minionListContainer, "<color=#9c95b4>図鑑で種類を『召喚』すると、ここで個体ごとに武器/防具を装備できます。</color>", 13, MUTED, TextAlignmentOptions.TopLeft);
            Place(h.rectTransform, 6, y, W - 12, 24); y += 30f;
            y = AddGachaRow(W, y);   // ⚠ 手持ちが0でも引ける入口を出す（ここが無いと最初の1体が引けない）
            y = AddShopRow(W, y);
            minionListContainer.sizeDelta = new Vector2(0f, y + 12f);
            return;
        }
        var fmgr = DungeonFloorManager.Instance;
        string floorLbl = "B" + ((fmgr != null ? fmgr.CurrentFloorIndex : 0) + 1) + "F";
        int squadN = featureMgr != null ? featureMgr.CurrentSquad.Count : 0;
        var head = Text(minionListContainer,
            "◆ 個体の管理　<color=#8cb8e6>＋隊＝" + floorLbl + " の隊に編成(" + squadN + "/" + DungeonFeatureManager.SquadMaxSlots + ")</color>"
            + "　<color=#e3a94a>進化＝Lv/装備を保ったまま上位形態へ</color>　<color=#9c95b4>装備＝DPで1段ずつ鍛造</color>",
            14, C("#8cb8e6"), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(head.rectTransform, 2, y, W - 4, 22); y += 26f;
        y = AddGachaRow(W, y);
        y = AddShopRow(W, y);
        float rowH = 104f;
        for (int i = 0; i < all.Count; i++)
        {
            AddIndividualEquipRow(all[i].id, y, W, rowH);
            y += rowH + 8f;
        }
        minionListContainer.sizeDelta = new Vector2(0f, y + 12f);
    }

    /// <summary>
    /// 🎰 召喚の儀（ガチャ）。**ユニーク魔物はここでしか出ない**。
    /// ⚠ 一覧から選ぶ通常召喚は残す。ガチャは「幅を作る」もので、近道ではない。
    /// </summary>
    private float AddGachaRow(float W, float y)
    {
        var box = Panel(minionListContainer, "GachaRow", CARD);
        Place(box.rectTransform, 0, y, W, 62); Outline(box, GOLD);
        var t1 = Text(box.rectTransform,
            "🎰 召喚の儀　<size=88%><color=#9c95b4>何が応えるかは選べない。"
            + "<color=#ffd24a>ユニーク魔物はここでしか出ない</color>（いま "
            + (SummonGacha.CurrentUniqueChance * 100f).ToString("0.0") + "%"
            + (SummonGacha.MissStreak > 0 ? "・外し " + SummonGacha.MissStreak + " 回ぶん上乗せ" : "") + "）</color></size>",
            13, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(t1.rectTransform, 12, 8, W - 200, 18);
        var t2 = Text(box.rectTransform, string.IsNullOrEmpty(SummonGacha.LastResult)
            ? "<color=#6f6889>まだ引いていない。</color>"
            : "直前：" + (SummonGacha.LastWasUnique ? "<color=#ffd24a>" : "<color=#8cb8e6>") + SummonGacha.LastResult + "</color>",
            11.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(t2.rectTransform, 12, 32, W - 200, 18);
        string why; bool ok = SummonGacha.CanRoll(out why);
        var b = PrimaryButton(box, "引く " + SummonGacha.Cost + " DP", ok ? PANEL2 : PANEL, ok ? GOLD : C("#4a4560"),
            () => { if (SummonGacha.TryRoll()) { RefreshMinionCodex(); RefreshSpecialStrip(); } });
        Place((RectTransform)b.transform, W - 172, 16, 158, 30);
        if (!ok) AddTooltip(((RectTransform)b.transform).gameObject, why);
        else AddTooltip(((RectTransform)b.transform).gameObject,
            "解禁済みの種から1体が必ず手に入り、低確率でユニーク魔物が出ます。" + "\n" + "外すほど次のユニーク確率が上がります。");
        return y + 70f;
    }

    private void AddIndividualEquipRow(int id, float y, float W, float h)
    {
        var v = MinionRoster.Get(id); if (v == null) return;
        var d = MinionCatalog.Get(v.catalogIndex);
        bool placed = featureMgr != null && featureMgr.IsIndividualPlaced(id);
        var row = Panel(minionListContainer, "IndRow_" + id, CARD);
        Place(row.rectTransform, 0, y, W, h); Outline(row, LINE);

        // 左：種類名 / Lv / 合計効果 / 配置状態
        var nm = Text(row.rectTransform, d.jpName + " <size=76%><color=#9c95b4>#" + id + "</color></size>", 14, RoleColor(d.role), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(nm.rectTransform, 12, 8, 236, 20);
        float totalAtk = MinionRoster.EquipAtkMult(id) * MinionRoster.TypeAtkMult(id);
        string expTxt = v.level >= MinionRoster.MaxLevel ? " <color=#ffd24a>MAX</color>"
            : " <size=88%><color=#6f6889>exp " + v.exp + "/" + MinionRoster.ExpPerLevel + "</color></size>";
        // ⚠ 折り返すと下の『ボス任命名』に食い込んで重なる → 1行に固定して収まらない分だけ縮める
        var lv = Text(row.rectTransform, "Lv " + v.level + expTxt + "  <color=#8cb8e6>攻×" + totalAtk.ToString("0.00") + " 硬×" + MinionRoster.EquipHpMult(id).ToString("0.00") + "</color>", 11.5f, MUTED, TextAlignmentOptions.TopLeft);
        lv.enableAutoSizing = true; lv.fontSizeMin = 8.5f; lv.fontSizeMax = 11.5f;
        Place(lv.rectTransform, 12, 32, 244, 18);
        // 🜏 ボスに任命したときに継ぐ魔神の名（個体ごとに固定）
        var go = Text(row.rectTransform, "◆" + GoetiaCatalog.RichTitleOf(id), 10.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(go.rectTransform, 12, 52, 246, 16);
        AddTooltip(row.gameObject, "ボス任命時: " + GoetiaCatalog.TitleOf(id) + " ／ " + GoetiaCatalog.Blessing(GoetiaCatalog.PillarOf(id).rank));
        // 所属：この個体がどの階の隊にいるか（1個体=1隊）／ボスに任命されているか（ボスは隊に入れない）
        int squadFloor = featureMgr != null ? featureMgr.SquadFloorOfIndividual(id) : -1;
        int bossFloor = featureMgr != null ? featureMgr.BossFloorOfIndividual(id) : -1;
        var myKin = KinRoster.Of(id);                      // 🗺️ 自身が眷属か
        var myLeader = KinRoster.LeaderOfFollower(id);     // 🗺️ どこかの眷属に率いられているか
        // ⚠ 「編成済み」とだけ書くと、3階から見たとき **その個体が1階の隊なのか2階の隊なのか分からず**、
        //    別の階の個体を誤って外す事故が起きる。所属は必ず**階層名で**書き、
        //    いま見ている階と違うときは色を変えて『他階』と添える。
        int hereFloor = DungeonFloorManager.Instance != null ? DungeonFloorManager.Instance.CurrentFloorIndex : 0;
        bool otherFloor = squadFloor >= 0 && squadFloor != hereFloor;
        string squadTxt = squadFloor < 0 ? ""
            : otherFloor
                ? "<color=#e08a3c>B" + (squadFloor + 1) + "F隊 <size=86%>(他階)</size></color>"
                : "<color=#57c3ab>B" + (squadFloor + 1) + "F隊 <size=86%>(この階)</size></color>";
        string belong = myKin != null ? "<color=#ffd24a>眷属『" + myKin.trueName + "』</color>"
            : myLeader != null ? "<color=#e3a94a>" + myLeader.trueName + "の配下</color>"
            : bossFloor >= 0 ? "<color=#e07a7a>B" + (bossFloor + 1) + "Fボス</color>"
            : squadFloor >= 0 ? squadTxt : "<color=#6f6889>未編成</color>";
        var st = Text(row.rectTransform, belong + "　" + (placed ? "<color=#e3a94a>配置中</color>" : "<color=#6f6889>待機</color>"), 11, FAINT, TextAlignmentOptions.TopLeft);
        Place(st.rectTransform, 130, 32, 140, 16);

        // 🏋️④ 実戦の反芻：冒険者が到達しなかった階層に置いた個体だけ、素材で経験を注げる
        if (TrainingSystem.IsTraining(id))
        {
            var tr = TrainingSystem.Of(id);
            var tt = Text(row.rectTransform, "<color=#e08a3c>◆訓練中 あと" + tr.turnsLeft + "ターン（"
                + SurfaceMap.Get(tr.regionId).name + "）</color>", 10.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(tt.rectTransform, 130, 50, 130, 16);
        }
        else
        {
            string whyD; bool canD = TrainingSystem.CanDrill(id, out whyD);
            int dcost = TrainingSystem.DrillCost(id);
            var db = PrimaryButton(row, "反芻 素材" + dcost, canD ? PANEL2 : PANEL, canD ? C("#e08a3c") : C("#4a4560"),
                () => { if (TrainingSystem.TryDrill(id)) RefreshMinionCodex(); });
            Place((RectTransform)db.transform, 130, 48, 122, 22);
            AddTooltip(((RectTransform)db.transform).gameObject,
                canD ? "素材 " + dcost + " を注いで +" + TrainingSystem.DrillExp + "exp。\n冒険者が到達しなかった階層に置いた個体だけが使える（戦えなかったぶんを埋める手段）。"
                     : whyD);
        }

        // 右：武器スロット（上）／防具スロット（下）
        AddEquipSlot(row, id, EquipmentCatalog.Slot.Weapon, "武器", 262, 10);
        AddEquipSlot(row, id, EquipmentCatalog.Slot.Armor, "防具", 262, 44);
        // 💍 装飾品（1個体1つ）。⚠ x=430 に置くと**武器/防具の『強化＋』ボタン(x484〜616)に丸かぶり**する。
        //    装備列は x262〜796（種別→ の右端まで）を使い切っているので、その右の空きに出す。
        AddAccessorySlot(row, id, 812, 8, W - 812 - 16);

        // 下段：🛡️隊編成（この階の隊へ）＋ 🧬個体進化（Lv/装備を保ったまま上位形態へ）
        float by = h - 30f;
        if (myKin != null || myLeader != null)
        {
            // 🗺️ 地上に出ている：ダンジョンの編成には使えない。操作は『地上』パネルで行う。
            string t = myKin != null ? "<color=#ffd24a>◆ 眷属『" + myKin.trueName + "』</color><size=84%><color=#6f6889>（『地上』パネルで編成・進軍）</color></size>"
                                     : "<color=#e3a94a>◆ " + myLeader.trueName + " の配下として地上に出ています</color>";
            var kt = Text(row.rectTransform, t, 10.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(kt.rectTransform, 12, by + 4, 300, 18);
        }
        else if (bossFloor >= 0)
        {
            // 👑 ボス任命中：実体は1つなので隊には入れない。外したいときはマップ上で撤去する。
            var bt = Text(row.rectTransform, "<color=#e07a7a>◆ B" + (bossFloor + 1) + "F のボス</color><size=84%><color=#6f6889>（隊には編成できません）</color></size>",
                10.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(bt.rectTransform, 12, by + 4, 240, 18);
        }
        else if (squadFloor >= 0)
        {
            // ⚠ ボタンにも**どの階から外すのか**を書く。ここが「隊から外す」だけだったので、
            //    3階を見ながら1階のゴブリンを外してしまう事故が起きた。
            var rmBtn = PrimaryButton(row, "B" + (squadFloor + 1) + "F の隊から外す",
                PANEL2, otherFloor ? C("#e08a3c") : MUTED,
                () => { featureMgr.SquadRemoveIndividual(id); RefreshMinionCodex(); RefreshSquadTray(); });
            Place((RectTransform)rmBtn.transform, 12, by, 148, 24);
            var rlb = rmBtn.GetComponentInChildren<TMP_Text>(); if (rlb != null) rlb.fontSize = 11f;
            AddTooltip(((RectTransform)rmBtn.transform).gameObject,
                "この個体は <b>B" + (squadFloor + 1) + "F</b> の隊にいます"
                + (otherFloor ? "（いま見ているのは B" + (hereFloor + 1) + "F です）" : "")
                + "\n外すと、盤に置いてあった場合は<b>その配置も解けます</b>。");
        }
        else
        {
            var addBtn = PrimaryButton(row, "＋隊 (" + floorLabelNow() + ")", PANEL2, TEAL, () => { if (featureMgr != null && featureMgr.SquadAdd(id)) { RefreshMinionCodex(); RefreshSquadTray(); } });
            Place((RectTransform)addBtn.transform, 12, by, 116, 24);
        }

        // 🗺️ 眷属化：**条件を満たしていなくても常に欄を出し**、何が足りないかをチェックリストで見せる。
        //    （以前は条件を全部満たすまでボタン自体が現れず「どうすれば出るのか」が分からなかった）
        if (myKin == null && myLeader == null)
        {
            var reqs = KinRoster.NameRequirements(id);
            bool can = KinRoster.MeetsNameRequirements(id);
            int roll = nameRolls.ContainsKey(id) ? nameRolls[id] : 0;
            string cand = KinRoster.NameCandidate(id, roll);
            int kcost = KinRoster.NameCost(id);

            var kb = PrimaryButton(row, can ? ("眷属化：" + cand) : "眷属化", PANEL2, can ? GOLD : FAINT, () =>
            {
                int rr = nameRolls.ContainsKey(id) ? nameRolls[id] : 0;
                if (KinRoster.TryName(id, rr)) { RefreshMinionCodex(); RefreshSurfacePanel(); }
            });
            float kx = W - 210f;   // 行の右端に寄せる（装備スロット列と重ならないように）
            Place((RectTransform)kb.transform, kx, by, 152, 24);
            kb.interactable = can;

            // 条件のチェックリスト（満たしたものは緑の◆、未達は灰の・）
            var sb2 = new System.Text.StringBuilder();
            foreach (var q in reqs)
                sb2.Append(q.met ? "<color=#5cc47c>◆" + q.label + "</color>  " : "<color=#6f6889>・" + q.label + "</color>  ");
            var chk = Text(row.rectTransform, sb2.ToString(), 9f, FAINT, TextAlignmentOptions.TopRight);
            chk.enableWordWrapping = false;
            Place(chk.rectTransform, kx - 500f, by - 15, 690f, 14);

            AddTooltip(kb.gameObject, "真名『" + cand + "』を与えて眷属にする（-" + kcost + "DP）。\n"
                + "眷属は配下を率いて地上へ出られるが、ダンジョンの隊・ボスには使えなくなる。\n"
                + (can ? "" : "※ 上の条件をすべて満たすと押せるようになります。"));

            if (can)
            {
                var rb = PrimaryButton(row, "↻", PANEL2, MUTED, () =>
                {
                    nameRolls[id] = (nameRolls.ContainsKey(id) ? nameRolls[id] : 0) + 1;
                    RefreshMinionCodex();
                });
                Place((RectTransform)rb.transform, kx + 156f, by, 30, 24);
                AddTooltip(rb.gameObject, "別の真名の候補を出す");
            }
        }

        // 進化先（直系の子）を並べる。研究段階が未解禁なら理由を表示。
        float ex = 136f;
        var children = MinionEvolution.ChildrenOf(v.catalogIndex);
        if (children.Count == 0)
        {
            var mx = Text(row.rectTransform, "<color=#6f6889>これ以上進化しない（最終形態）</color>", 10.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(mx.rectTransform, ex, by + 4, 300, 18);
        }
        else
        {
            foreach (var ci in children)
            {
                int target = ci;
                var cd = MinionCatalog.Get(ci);
                bool ok = MinionEvolution.CanIndividualEvolveTo(ci);
                int cost = MinionEvolution.EvolveCost(ci);
                if (ok)
                {
                    var eb = PrimaryButton(row, "◆進化 " + cd.jpName + " -" + cost, BLOOD, TEXT,
                        () => { if (MinionRoster.TryEvolveIndividual(id, target)) { RefreshMinionCodex(); RefreshSquadTray(); } }, true);
                    Place((RectTransform)eb.transform, ex, by, 168, 24);
                }
                else
                {
                    var lk = Panel(row.rectTransform, "evolk_" + id + "_" + ci, C("#0f0d16"));
                    Place(lk.rectTransform, ex, by, 168, 24); Outline(lk, LINE);
                    var lt = Text(lk.rectTransform, "<color=#8cb8e6>・" + cd.jpName + "（研究）</color>", 10, FAINT, TextAlignmentOptions.Center, FontStyles.Bold);
                    StretchFull(lt.rectTransform);
                }
                ex += 172f;
            }
        }
    }

    private string floorLabelNow()
    {
        var fmgr = DungeonFloorManager.Instance;
        return "B" + ((fmgr != null ? fmgr.CurrentFloorIndex : 0) + 1) + "F";
    }


    /// <summary>
    /// 🛒 行商人。**品揃えは3枠で、ターンが変わると引き直す**（逃したものは戻らない）。
    /// ⚠ 買った枠は売り切れのまま残す。埋め直すと「今買うべきか」の判断が消える。
    /// </summary>
    private float AddShopRow(float W, float y)
    {
        var box = Panel(minionListContainer, "ShopRow", CARD);
        Place(box.rectTransform, 0, y, W, 76); Outline(box, C("#57c3ab"));
        var t1 = Text(box.rectTransform,
            "🛒 行商人　<size=88%><color=#9c95b4>今回きりの品揃え。ターンが変わると入れ替わる"
            + "（手持ちの装飾品 " + AccessoryInventory.TotalCount + " 個）</color></size>",
            13, C("#57c3ab"), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(t1.rectTransform, 12, 6, W - 24, 18);
        float bw = (W - 40) / 3f;
        for (int i = 0; i < MerchantShop.Slots; i++)
        {
            int si = i;
            int item = MerchantShop.SlotItem(i);
            var card = Panel(box.rectTransform, "Shop_" + i, PANEL2);
            Place(card.rectTransform, 12 + i * (bw + 8), 28, bw, 42);
            Outline(card, item >= 0 ? C(AccessoryCatalog.ColorHex(item)) : LINE);
            if (item < 0)
            {
                var so = Text(card.rectTransform, "<color=#4a4560>売り切れ</color>", 11, FAINT, TextAlignmentOptions.Center);
                StretchFull(so.rectTransform);
                continue;
            }
            var d = AccessoryCatalog.Get(item);
            var nm = Text(card.rectTransform, d.jpName + " <size=80%><color=#6f6889>" + AccessoryCatalog.RarityName(d.rarity) + "</color></size>",
                11.5f, C(d.colorHex), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm.rectTransform, 8, 4, bw - 80, 16);
            var ef = Text(card.rectTransform, "<size=90%><color=#9c95b4>" + AccessoryCatalog.EffectLine(item) + "</color></size>",
                10.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(ef.rectTransform, 8, 22, bw - 80, 16);
            string why; bool ok = MerchantShop.CanBuy(i, out why);
            var b = PrimaryButton(card, d.price + "DP", ok ? CARD : PANEL, ok ? GOLD : C("#4a4560"),
                () => { if (MerchantShop.TryBuy(si)) RefreshMinionCodex(); });
            Place((RectTransform)b.transform, bw - 72, 8, 64, 26);
            var blb = b.GetComponentInChildren<TMP_Text>(); if (blb != null) blb.fontSize = 10f;
            AddTooltip(card.gameObject, d.jpName + "（" + AccessoryCatalog.RarityName(d.rarity) + "）" + "\n" + ""
                + AccessoryCatalog.EffectLine(item) + "" + "\n" + "" + d.desc + (ok ? "" : "" + "\n" + "" + why));
        }
        return y + 84f;
    }

    /// <summary>
    /// 💍 装飾品の枠。押すたびに「外す → 手持ちA → 手持ちB → …→ 外す」と回す。
    /// ⚠ 一覧を開かせない。1個体1枠しかないので、回すほうが速い（眷属の麾下と同じ考え方）。
    /// ⚠⚠ 置き場所に注意。装備列（x262〜796）と**重ねない**こと。
    ///   以前 x=430 に置いていて『強化＋』ボタンの上に乗り、押せない/読めない状態になっていた。
    /// </summary>
    private void AddAccessorySlot(Image row, int id, float x, float yy, float w)
    {
        var v = MinionRoster.Get(id); if (v == null) return;
        int cur = v.accessory;
        // ⚠ 横1本に収める。下に伸ばすと**下段の『眷属化』のチェックリスト(y59〜)に食い込む**。
        w = Mathf.Max(240f, w);
        const float labW = 52f, chipW = 232f, gap = 8f;
        var lab = Text(row.rectTransform, "装飾品", 10, FAINT, TextAlignmentOptions.TopLeft);
        Place(lab.rectTransform, x, yy + 6, labW, 14);
        var chip = Panel(row.rectTransform, "Acc_" + id, CARD);
        Place(chip.rectTransform, x + labW, yy, chipW, 26);
        Outline(chip, cur >= 0 ? C(AccessoryCatalog.ColorHex(cur)) : LINE);
        var t = Text(chip.rectTransform, cur >= 0 ? AccessoryCatalog.Name(cur) : "<color=#6f6889>なし</color>",
            11.5f, cur >= 0 ? C(AccessoryCatalog.ColorHex(cur)) : FAINT, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(t.rectTransform);
        // 何が起きるかを**開かずに読める**ようにする（1枠しかないので、名前だけだと選べない）
        float effX = x + labW + chipW + gap;
        var eff = Text(row.rectTransform,
            cur >= 0 ? "<color=#9c95b4>" + AccessoryCatalog.EffectLine(cur) + "</color>"
                     : "<color=#4a4560>押すと手持ちから着ける（" + AccessoryInventory.TotalCount + " 個・行商人で買える）</color>",
            10, MUTED, TextAlignmentOptions.TopLeft);
        eff.enableWordWrapping = false; eff.enableAutoSizing = true; eff.fontSizeMin = 8f; eff.fontSizeMax = 10f;
        Place(eff.rectTransform, effX, yy + 6, Mathf.Max(80f, x + w - effX), 14);
        var b = chip.gameObject.AddComponent<Button>(); b.targetGraphic = chip;
        b.onClick.AddListener(() => { AccessoryInventory.Equip(id, NextAccessoryFor(cur)); RefreshMinionCodex(); });
        AddTooltip(chip.gameObject, cur >= 0
            ? AccessoryCatalog.Name(cur) + "" + "\n" + "" + AccessoryCatalog.EffectLine(cur) + "" + "\n" + "" + AccessoryCatalog.Get(cur).desc
              + "" + "\n" + "押すと次の装飾品へ（手持ち " + AccessoryInventory.TotalCount + " 個）"
            : "装飾品はまだ着けていない。押すと手持ちから着ける（手持ち " + AccessoryInventory.TotalCount + " 個）"
              + "" + "\n" + "行商人（下部バー『商』）で買える。");
    }

    /// <summary>装飾品の巡回：なし → 手持ちを順に → なし。⚠ いま着けている物は手持ちに無いので候補に足す。</summary>
    private static int NextAccessoryFor(int cur)
    {
        var items = AccessoryInventory.Items();
        if (cur >= 0) items.Insert(0, cur);          // いま着けている物を先頭に置いて順序を安定させる
        if (items.Count == 0) return -1;
        if (cur < 0) return items[0];
        for (int i = 0; i < items.Count; i++)
            if (items[i] == cur) return (i + 1 < items.Count) ? items[i + 1] : -1;
        return -1;
    }

    private void AddEquipSlot(Image row, int id, EquipmentCatalog.Slot slot, string label, float x, float yy)
    {
        int g = MinionRoster.GradeOf(id, slot);
        bool isWeapon = slot == EquipmentCatalog.Slot.Weapon;
        int wt = MinionRoster.WeaponTypeOf(id);
        // 🖼️ アイコン：武器は"種別"のアイコン（剣/斧/弓/杖…）、防具は盾。素材グレード色で着色。
        Color tint = g >= 0 ? Color.Lerp(Color.white, C(EquipmentCatalog.ColorHex(g)), 0.5f) : new Color(0.6f, 0.6f, 0.66f, 1f);
        IconImg(row.rectTransform, isWeapon ? EquipmentCatalog.WeaponTypeIcon(wt) : "icon_shield", x + 4, yy - 1, 26, tint);
        var lbl = Text(row.rectTransform, isWeapon ? EquipmentCatalog.WeaponTypeName(wt) : label, 11, isWeapon ? TEXT : MUTED, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(lbl.rectTransform, x + 32, yy + 3, 30, 16);
        // 現在グレードのチップ
        var chip = Panel(row.rectTransform, "g_" + slot + "_" + id, C("#0f0d16"));
        Place(chip.rectTransform, x + 64, yy, 150, 24); Outline(chip, LINE);
        var gt = Text(chip.rectTransform, "<color=" + EquipmentCatalog.ColorHex(g) + ">" + EquipmentCatalog.Name(g) + "</color>", 12, TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(gt.rectTransform);
        // 強化＋（次グレードへ鍛造・DP消費）
        if (g < EquipmentCatalog.MaxGrade)
        {
            int cost = EquipmentCatalog.ForgeCost(g + 1);
            int fmat = EquipmentCatalog.ForgeMaterial(g + 1);
            var fb = PrimaryButton(row, "強化＋ -" + cost + (fmat > 0 ? " -" + fmat + "素材" : ""), BLOOD, TEXT,
                () => { if (MinionRoster.TryForge(id, slot)) RefreshMinionCodex(); }, true);
            Place((RectTransform)fb.transform, x + 222, yy, 132, 24);
            // ⚖️ 1段でどれだけ変わるかを見せる（見せないと「上げる意味あるの？」になる）
            AddTooltip(((RectTransform)fb.transform).gameObject,
                EquipmentCatalog.Name(g) + " → " + EquipmentCatalog.Name(g + 1) + "　" + EquipmentCatalog.StepText(g, slot)
                + "\n1段階でおよそ +22%（レベル5〜6ぶん）。" + (fmat > 0 ? "\nミスリル以上は素材も要ります。" : ""));
        }
        else
        {
            var mx = Text(row.rectTransform, "<color=#ffd24a>最高グレード</color>", 11, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(mx.rectTransform, x + 222, yy + 3, 132, 18);
        }
        // 外す
        if (g >= 0)
        {
            var rb = PrimaryButton(row, "外す", PANEL2, MUTED, () => { MinionRoster.Unequip(id, slot); RefreshMinionCodex(); });
            Place((RectTransform)rb.transform, x + 360, yy, 56, 24);
        }
        // ⚔️ 武器種の切替（無償＝"戦い方"の選択）。次の種別を予告表示。
        if (isWeapon)
        {
            int nextT = (wt + 1) % EquipmentCatalog.WeaponTypeCount;
            var d = EquipmentCatalog.WType(wt);
            var tb = PrimaryButton(row, "種別→" + EquipmentCatalog.WeaponTypeName(nextT), PANEL2, TEAL,
                () => { MinionRoster.CycleWeaponType(id); RefreshMinionCodex(); });
            Place((RectTransform)tb.transform, x + 422, yy, 112, 24);
            AddTooltip(tb.gameObject, EquipmentCatalog.WeaponTypeName(wt) + "：" + d.note
                + string.Format("（攻×{0:0.00} 間隔×{1:0.00} 射程+{2:0.0}）", d.atkMult, d.intervalMult, d.rangeBonus));
        }
    }
}
