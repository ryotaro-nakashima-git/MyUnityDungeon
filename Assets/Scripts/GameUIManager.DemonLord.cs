using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 魔王パネル・感情ツリー・階層タブ・遺物パネル。
/// <para>`GameUIManager` の partial。フィールドの本体は GameUIManager.cs 側にある。</para>
/// </summary>
public partial class GameUIManager
{

    // ---------- 魔王パネル（成長/進化） ----------
    private void BuildDemonPanel(RectTransform root)
    {
        var panel = Panel(root, "DemonPanel", PANEL);
        demonPanel = panel.gameObject;
        Anchor(panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        panel.rectTransform.sizeDelta = new Vector2(520, 848);
        panel.rectTransform.anchoredPosition = new Vector2(16, -72);
        Outline(panel, LINE2); SkinPanel(panel);

        float pad = 16f, w = 520 - pad * 2;
        var eyebrow = Text(panel, "魔王の成長（BPでステ強化 → 条件を満たすと種族進化）", 11, GOLD, TextAlignmentOptions.Left, FontStyles.Bold); Place(eyebrow.rectTransform, pad, 12, w, 16);
        dlLevelText = Text(panel, "Lv 1", 18, TEXT, TextAlignmentOptions.Left, FontStyles.Bold); Place(dlLevelText.rectTransform, pad, 30, 140, 24);
        dlBpText = Text(panel, "BP 10", 14, VIOLET, TextAlignmentOptions.Right, FontStyles.Bold); Place(dlBpText.rectTransform, pad + w - 130, 33, 130, 20);
        dlRaceText = Text(panel, "種族: 人種", 12.5f, MUTED, TextAlignmentOptions.Left); Place(dlRaceText.rectTransform, pad, 58, w, 18);

        // 📊 ステータス（各ランクの"意味"を右に表示）
        var sl = Text(panel, "ステータス（BPで強化・S=最大）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold); Place(sl.rectTransform, pad, 82, w, 16);
        for (int i = 0; i < 5; i++)
        {
            int idx = i; float y = 102 + i * 30;
            var nm = Text(panel, DemonLord.StatNames[i], 13, TEXT, TextAlignmentOptions.Left); Place(nm.rectTransform, pad, y, 60, 22);
            var rk = Text(panel, "E", 15, GOLD, TextAlignmentOptions.Center, FontStyles.Bold); Place(rk.rectTransform, pad + 60, y, 30, 22); statRankTexts[i] = rk;
            var eff = Text(panel, "", 10.5f, MUTED, TextAlignmentOptions.Left); Place(eff.rectTransform, pad + 96, y + 3, w - 160, 18);
            statEffectTexts[i] = eff;
            var plus = PrimaryButton(panel, "＋", GOLD, C("#231704"), () => { DemonLord.Instance?.TrySpendBPOnStat(idx); RefreshDemonPanel(); });
            Place((RectTransform)plus.transform, pad + w - 48, y, 48, 24); statPlusBtns[i] = plus;
        }

        // ⚔️🛡️ 魔王の装備（錬成ランクで割引・上限UP）
        var eq = Text(panel, "魔王の武具（錬成ランクで鍛造が安く・上限が上がる）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(eq.rectTransform, pad, 256, w, 16);
        dlEquipRow = NewRect("DLEquip", panel.rectTransform);
        Place(dlEquipRow, pad, 274, w, 62);

        // 🧬 進化（現在の種族からの分岐のみ表示）
        var el = Text(panel, "種族進化（分岐・3段階）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold); Place(el.rectTransform, pad, 344, w, 16);
        dlEvolveRow = NewRect("DLEvolve", panel.rectTransform);
        Place(dlEvolveRow, pad, 362, w, 180);

        // 👑 構え（鎮座／親征）＝ 魔王が盤のどこに立つか
        var stl = Text(panel, "魔王の構え（奥で待つか、前に出るか・準備フェーズのみ変更可）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(stl.rectTransform, pad, 552, w, 16);
        dlStanceRow = NewRect("DLStance", panel.rectTransform);
        Place(dlStanceRow, pad, 570, w, 96);

        // 🍽️ 捕食（喰らいの段）
        var dvl = Text(panel, "捕食（喰らって糧にする）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(dvl.rectTransform, pad, 674, w, 16);
        dlDevourRow = NewRect("DLDevour", panel.rectTransform);
        Place(dlDevourRow, pad, 692, w, 30);
        dlDevourText = Text(panel, "", 10, MUTED, TextAlignmentOptions.Left);
        Place(dlDevourText.rectTransform, pad, 726, w, 16);
        dlFeedStrip = MakeHScroll(panel, pad, 744, w, 84);

        RefreshDemonPanel();
        demonPanel.SetActive(false);
    }

    // 魔王パネルの『見た目が変わる条件』だけを拾った署名。変化した時だけ作り直す。
    private string DemonPanelSig()
    {
        var dl = DemonLord.Instance; if (dl == null) return "";
        var sb = new System.Text.StringBuilder();
        sb.Append(dl.Level).Append('/').Append(dl.BP).Append('/').Append((int)dl.CurrentRace)
          .Append('/').Append(dl.WeaponGrade).Append('/').Append(dl.ArmorGrade).Append('/').Append((int)dl.WeaponType)
          .Append('/').Append(dl.ForgeGradeCap);
        for (int i = 0; i < 5; i++) sb.Append('/').Append(dl.GetStatRank(i));
        foreach (var r in DemonLordRaceTree.ChildrenOf(dl.CurrentRace)) sb.Append(dl.IsRaceAvailable(r) ? '1' : '0');
        // 👑 構えと捕食も見た目に出るので署名に入れる（入れないと押しても表示が変わらない）
        sb.Append('|').Append((int)LordStance.Current).Append('/').Append(LordStance.StationFloor)
          .Append('/').Append(LordStance.DevourExp).Append('/').Append(LordStance.DevourRank)
          .Append('/').Append(LordStance.DevouredThisTurn).Append('/').Append(FeedCandidates().Count)
          .Append('/').Append(DungeonFloorManager.Instance != null ? DungeonFloorManager.Instance.BuiltFloorCount : 1)
          .Append(DungeonTurnManager.Instance != null && DungeonTurnManager.Instance.IsPreparePhase ? 'P' : 'B');
        return sb.ToString();
    }

    // 感情ツリーの署名（解禁状態＋各ノードの購入可否）。所持感情そのものは RefreshEmotionPools で軽量更新する。
    private string EmotionPanelSig()
    {
        var et = EmotionTreeManager.Instance; if (et == null) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var n in et.Nodes) sb.Append(n.unlocked ? '2' : et.CanUnlock(n) ? '1' : '0');
        sb.Append('|');
        foreach (var n in et.Fusions) sb.Append(n.unlocked ? '2' : et.CanUnlock(n) ? '1' : '0');
        return sb.Append('|').Append(et.ResearchPointBonus).ToString();
    }

    private void RefreshEmotionPools()
    {
        var et = EmotionTreeManager.Instance; if (et == null) return;
        for (int r = 0; r < 4; r++)
            if (emoRouteHeads[r] != null) emoRouteHeads[r].text = EmotionRouteHeadText(r, et);
    }

    private string EmotionRouteHeadText(int r, EmotionTreeManager et)
        => "▍<color=" + EmotionTreeManager.RouteColors[r] + ">" + EmotionTreeManager.RouteNames[r] + "</color>"
         + "　<size=88%><color=#9c95b4>所持 " + et.Pool((EmotionTreeManager.Route)r) + "</color></size>";

    private void RefreshDemonPanel()
    {
        var dl = DemonLord.Instance; if (dl == null) return;
        var rd = DemonLordRaceTree.Get(dl.CurrentRace);
        if (dlLevelText != null) dlLevelText.text = "Lv " + dl.Level;
        if (dlBpText != null) dlBpText.text = "BP " + dl.BP;
        if (dlRaceText != null)
            dlRaceText.text = "種族: <color=#e3a94a>" + dl.RaceName + "</color>（第" + rd.stage + "形態）　"
                + "<color=" + MagicCatalog.ElementColor(rd.element) + ">◆" + MagicCatalog.ElementName(rd.element) + "</color>"
                + (rd.skill != MinionSkillKind.None ? "　<color=#57c3ab>◆" + MinionSkill.Name(rd.skill) + "</color>" : "");

        // 📊 各ステの効果を実数で表示（死にステを無くす）
        string[] eff = {
            "最大HP +" + (130 * dl.GetStatRank(0)),
            "攻撃力 +" + (6 * dl.GetStatRank(1)) + "・魔法階級↑",
            "研究RP +" + dl.KnowledgeRank + "/ターン・研究費 ×" + dl.ResearchCostMult.ToString("0.00"),
            "配下コスト ×" + dl.DefenderCostMult.ToString("0.00") + "・拡張 ×" + dl.DomainCostMult.ToString("0.00"),
            "鍛造費 ×" + dl.ForgeCostMult.ToString("0.00") + "・鍛造上限+" + dl.ForgeGradeBonus + "・戦利品+" + dl.RefineLootBonus,
        };
        for (int i = 0; i < 5; i++)
        {
            if (statRankTexts[i] != null) statRankTexts[i].text = dl.StatRankLabel(i);
            if (statEffectTexts[i] != null) statEffectTexts[i].text = eff[i];
            if (statPlusBtns[i] != null) statPlusBtns[i].interactable = dl.GetStatRank(i) < 5 && dl.BP > 0;
        }

        // ⚔️🛡️ 装備行を作り直す
        if (dlEquipRow != null)
        {
            for (int i = dlEquipRow.childCount - 1; i >= 0; i--) { var c = dlEquipRow.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
            BuildDLEquipSlot(EquipmentCatalog.Slot.Weapon, dl, 0f);
            BuildDLEquipSlot(EquipmentCatalog.Slot.Armor, dl, 30f);
        }

        // 🧬 進化分岐（現在の種族からの子のみ）
        if (dlEvolveRow != null)
        {
            for (int i = dlEvolveRow.childCount - 1; i >= 0; i--) { var c = dlEvolveRow.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
            evolveBtns.Clear();
            var kids = DemonLordRaceTree.ChildrenOf(dl.CurrentRace);
            if (kids.Count == 0)
            {
                var t = Text(dlEvolveRow, "<color=#ffd24a>最終形態に到達している（これ以上の進化は無い）</color>", 12, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(t.rectTransform, 2, 4, 460, 20);
            }
            else
            {
                float cw = 152f, ch = 56f;
                for (int i = 0; i < kids.Count; i++)
                {
                    var r = kids[i]; var d = DemonLordRaceTree.Get(r);
                    bool ok = dl.IsRaceAvailable(r);
                    float cx = (i % 3) * (cw + 6), cy = (i / 3) * (ch + 6);
                    var card = Panel(dlEvolveRow, "Evo_" + r, CARD);
                    Place(card.rectTransform, cx, cy, cw, ch); Outline(card, ok ? GOLD : LINE);
                    var nm = Text(card.rectTransform, d.jpName, 12.5f, ok ? TEXT : FAINT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                    Place(nm.rectTransform, 8, 4, cw - 16, 16);
                    var sub = Text(card.rectTransform,
                        "<color=" + MagicCatalog.ElementColor(d.element) + ">◆" + MagicCatalog.ElementName(d.element) + "</color>"
                        + (d.skill != MinionSkillKind.None ? " <color=#57c3ab>◆" + MinionSkill.Name(d.skill) + "</color>" : ""), 9.5f, MUTED, TextAlignmentOptions.TopLeft);
                    Place(sub.rectTransform, 8, 21, cw - 16, 14);
                    var req = Text(card.rectTransform, ok ? "<color=#5cc47c>進化できる</color>" : "<color=#9c95b4>" + DemonLordRaceTree.RequirementText(r) + "</color>", 9.5f, FAINT, TextAlignmentOptions.TopLeft);
                    Place(req.rectTransform, 8, 36, cw - 16, 14);
                    AddTooltip(card.gameObject, d.jpName + "：" + d.note);
                    if (ok)
                    {
                        var rr = r;
                        var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
                        btn.onClick.AddListener(() => { if (DemonLord.Instance != null && DemonLord.Instance.EvolveTo(rr)) RefreshDemonPanel(); });
                    }
                }
            }
        }

        RefreshStanceRow();
        RefreshDevourRow();
    }

    // ---------- 👑 構え（鎮座／親征）----------
    private void RefreshStanceRow()
    {
        if (dlStanceRow == null) return;
        for (int i = dlStanceRow.childCount - 1; i >= 0; i--) { var c = dlStanceRow.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }

        bool prep = DungeonTurnManager.Instance == null || DungeonTurnManager.Instance.IsPreparePhase;
        float cw = 236f;
        for (int i = 0; i < 2; i++)
        {
            var s = (LordStance.Stance)i;
            bool on = LordStance.Current == s;
            var card = Panel(dlStanceRow, "Stance_" + i, on ? SEL : CARD);
            Place(card.rectTransform, i * (cw + 8f), 0, cw, 40);
            Outline(card, on ? GOLD : LINE);
            var nm = Text(card.rectTransform, (on ? "◆ " : "") + LordStance.StanceName(s), 13.5f, on ? GOLD : TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm.rectTransform, 10, 4, cw - 20, 17);
            var sub = Text(card.rectTransform,
                s == LordStance.Stance.Expedition ? "立つ階を選ぶ／魂を喰らう" : "動かない／配下を喰らう・BP+2",
                9.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(sub.rectTransform, 10, 22, cw - 20, 14);
            AddTooltip(card.gameObject, LordStance.StanceName(s) + "\n" + LordStance.StanceDesc(s));
            if (prep && !on)
            {
                var ss = s;
                var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
                btn.onClick.AddListener(() => { if (LordStance.SetStance(ss)) { ReplaceFloorsForStance(); RefreshDemonPanel(); } });
            }
        }

        // 親征のときだけ「どの階に立つか」を選ばせる
        int fc = DungeonFloorManager.Instance != null ? Mathf.Max(1, DungeonFloorManager.Instance.BuiltFloorCount) : 1;
        if (LordStance.IsExpedition)
        {
            var lb = Text(dlStanceRow, "立つ階", 10.5f, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(lb.rectTransform, 0, 48, 46, 24);
            for (int f = 0; f < fc; f++)
            {
                int ff = f;
                bool here = LordStance.LordFloorIndex(fc) == f;
                var b = Panel(dlStanceRow, "SF_" + f, here ? SEL : PANEL2);
                Place(b.rectTransform, 50 + f * 62f, 48, 56, 24); Outline(b, here ? CRIMSON : LINE);
                var t = Text(b.rectTransform, "B" + (f + 1) + "F", 11.5f, here ? GOLD : TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
                StretchFull(t.rectTransform);
                AddTooltip(b.gameObject, "B" + (f + 1) + "F に立つ。ここで侵攻は止まる（深い階の防衛は使われない）。"
                    + "深度報酬 ×" + (DungeonFloorManager.Instance != null ? DungeonFloorManager.Instance.DepthRewardMult(f) : 1f).ToString("0.00"));
                if (prep && !here)
                {
                    var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
                    btn.onClick.AddListener(() => { if (LordStance.SetStationFloor(ff)) { ReplaceFloorsForStance(); RefreshDemonPanel(); } });
                }
            }
        }
        else
        {
            var t = Text(dlStanceRow, "<color=#9c95b4>魔王は B" + fc + "F（最下層）から動かない。</color>", 10.5f, MUTED, TextAlignmentOptions.Left);
            Place(t.rectTransform, 0, 52, 470, 16);
        }
    }

    /// <summary>構えを変えたら魔王の実体だけを移す（⚠ 階を作り直すと配置が消える）。</summary>
    private void ReplaceFloorsForStance()
    {
        var fm = DungeonFloorManager.Instance;
        if (fm != null) fm.RefreshLordPresence();
        RefreshFloorTabs();   // タブの『魔』印も移す
    }

    // ---------- 🍽️ 捕食 ----------
    private List<MinionRoster.Individual> FeedCandidates()
    {
        var list = new List<MinionRoster.Individual>();
        var fm = DungeonFeatureManager.Instance;
        foreach (var v in MinionRoster.All)
        {
            if (UniqueCatalog.IsUnique(v.catalogIndex)) continue;                 // ユニークは喰わせない
            if (fm != null && (fm.IsIndividualPlaced(v.id) || fm.IsIndividualInAnySquad(v.id))) continue;
            if (KinRoster.IsAwayFromDungeon(v.id)) continue;
            list.Add(v);
        }
        return list;
    }

    private void RefreshDevourRow()
    {
        if (dlDevourRow == null) return;
        for (int i = dlDevourRow.childCount - 1; i >= 0; i--) { var c = dlDevourRow.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }

        var st = Text(dlDevourRow,
            "捕食値 <color=#e3a94a>" + LordStance.DevourExp + "</color>　喰らいの段 <color=#e3a94a>第" + LordStance.DevourRank + "段</color>"
            + "　<size=88%><color=#9c95b4>基礎HP+" + LordStance.BonusHP.ToString("0") + " 攻撃+" + LordStance.BonusAtk.ToString("0.0") + "</color></size>",
            12, TEXT, TextAlignmentOptions.Left);
        Place(st.rectTransform, 0, 6, 330, 20);
        bool can = LordStance.CanRankUp;
        var rb = PrimaryButton(dlDevourRow, "段を上げる -" + LordStance.NextRankCost, can ? BLOOD : PANEL2, can ? TEXT : FAINT,
            () => { if (LordStance.TryRankUp()) RefreshDemonPanel(); }, can);
        Place((RectTransform)rb.transform, 334, 0, 154, 28);
        AddTooltip(rb.gameObject, "喰らいの段：1段ごとに魔王の**基礎**最大HP+70／**基礎**攻撃+2.5。"
            + "\n<color=#9c95b4>倍率ではなく加算なので、装備や種族と掛け合わさって暴れることはない。</color>");

        var cand = FeedCandidates();
        if (dlDevourText != null)
        {
            string why = LordStance.IsExpedition
                ? "<color=#e08a3c>親征中は喰えない</color>（鎮座に戻すと喰える）"
                : "残り <color=#e3a94a>" + LordStance.DevourLeftThisTurn + "</color> 体／ターン";
            dlDevourText.text = Fix("配下を喰らう ― " + why + "　<color=#6f6889>盤・隊・地上に出ていない個体だけ。ユニークは喰えない。装備ごと消える</color>");
        }

        if (dlFeedStrip == null) return;
        for (int i = dlFeedStrip.childCount - 1; i >= 0; i--) { var c = dlFeedStrip.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
        if (cand.Count == 0)
        {
            var t = Text(dlFeedStrip, "<color=#6f6889>喰わせられる配下がいない（盤から外すか、召喚すると増える）</color>", 11, FAINT, TextAlignmentOptions.TopLeft);
            Place(t.rectTransform, 4, 24, 440, 18);
            dlFeedStrip.sizeDelta = new Vector2(460f, 0f);
            return;
        }
        float cwid = 132f;
        for (int i = 0; i < cand.Count; i++)
        {
            var v = cand[i];
            string why2; bool ok = LordStance.CanDevour(v.id, out why2);
            int gain = LordStance.DevourValue(v.id);
            var d = MinionCatalog.Get(v.catalogIndex);
            var card = Panel(dlFeedStrip, "Feed_" + v.id, CARD);
            Place(card.rectTransform, i * (cwid + 6f), 2, cwid, 72);
            Outline(card, ok ? CRIMSON : LINE);
            var nm = Text(card.rectTransform, d.jpName, 11.5f, ok ? TEXT : FAINT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm.rectTransform, 8, 5, cwid - 16, 16);
            var lv = Text(card.rectTransform, "Lv" + v.level + "　#" + v.id, 10, MUTED, TextAlignmentOptions.TopLeft);
            Place(lv.rectTransform, 8, 22, cwid - 16, 14);
            // ⚠ 132px の札に長い理由文を入れると2行に折り返して下の行と重なる。括弧の前で切る。
            int paren = why2.IndexOf('（');
            string shortWhy = paren > 0 ? why2.Substring(0, paren) : why2;
            var gn = Text(card.rectTransform, ok ? "<color=#e3a94a>捕食値 +" + gain + "</color>" : "<color=#6f6889>" + shortWhy + "</color>",
                10.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(gn.rectTransform, 8, 38, cwid - 16, 16);
            var hint = Text(card.rectTransform, ok ? "<color=#8a2530>押すと喰らう</color>" : "", 9.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(hint.rectTransform, 8, 54, cwid - 16, 14);
            AddTooltip(card.gameObject, d.jpName + " 個体#" + v.id + "（Lv" + v.level + "）\n"
                + (ok ? "喰らうと捕食値 +" + gain + "。⚠ 装備ごと消えて戻せない。" : why2));
            if (ok)
            {
                int id = v.id;
                var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
                btn.onClick.AddListener(() => { if (LordStance.TryDevour(id)) RefreshDemonPanel(); });
            }
        }
        dlFeedStrip.sizeDelta = new Vector2(cand.Count * (cwid + 6f) + 4f, 0f);
    }

    // ⚔️🛡️ 魔王の装備スロット1つ分（アイコン＋グレード＋鍛造＋武器種切替）
    private void BuildDLEquipSlot(EquipmentCatalog.Slot slot, DemonLord dl, float y)
    {
        bool isW = slot == EquipmentCatalog.Slot.Weapon;
        int g = isW ? dl.WeaponGrade : dl.ArmorGrade;
        Color tint = g >= 0 ? Color.Lerp(Color.white, C(EquipmentCatalog.ColorHex(g)), 0.5f) : new Color(0.6f, 0.6f, 0.66f, 1f);
        IconImg(dlEquipRow, isW ? EquipmentCatalog.WeaponTypeIcon(dl.WeaponType) : "icon_shield", 0, y, 24, tint);
        var lbl = Text(dlEquipRow, isW ? EquipmentCatalog.WeaponTypeName(dl.WeaponType) : "防具", 11, isW ? TEXT : MUTED, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(lbl.rectTransform, 28, y + 4, 44, 16);
        var chip = Panel(dlEquipRow, "dlg_" + slot, C("#0f0d16"));
        Place(chip.rectTransform, 74, y, 118, 24); Outline(chip, LINE);
        var gt = Text(chip.rectTransform, "<color=" + EquipmentCatalog.ColorHex(g) + ">" + EquipmentCatalog.Name(g) + "</color>", 11.5f, TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(gt.rectTransform);
        if (g < dl.ForgeGradeCap)
        {
            int cost = dl.NextForgeCost(slot);
            var fb = PrimaryButton(dlEquipRow, "鍛造 -" + cost, BLOOD, TEXT, () => { if (DemonLord.Instance.TryForge(slot)) RefreshDemonPanel(); }, true);
            Place((RectTransform)fb.transform, 198, y, 116, 24);
        }
        else
        {
            var mx = Text(dlEquipRow, g >= EquipmentCatalog.MaxGrade ? "<color=#ffd24a>最高位</color>" : "<color=#8cb8e6>錬成ランク/研究が必要</color>", 10, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(mx.rectTransform, 198, y + 4, 116, 18);
        }
        if (isW)
        {
            var tb = PrimaryButton(dlEquipRow, "種別→" + EquipmentCatalog.WeaponTypeName((dl.WeaponType + 1) % EquipmentCatalog.WeaponTypeCount), PANEL2, TEAL,
                () => { DemonLord.Instance.CycleWeaponType(); RefreshDemonPanel(); });
            Place((RectTransform)tb.transform, 320, y, 120, 24);
            var d = EquipmentCatalog.WType(dl.WeaponType);
            AddTooltip(tb.gameObject, d.jpName + "：" + d.note + string.Format("（攻×{0:0.00} 射程+{1:0.0}）", d.atkMult, d.rangeBonus));
        }
    }

    // ---------- 感情ツリーパネル ----------
    private void BuildEmotionPanel(RectTransform root)
    {
        var panel = Panel(root, "EmotionPanel", PANEL);
        emotionPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(FS_W, FS_H);
        panel.rectTransform.anchoredPosition = new Vector2(0, 0);
        Outline(panel, LINE2); SkinPanel(panel);

        float pad = 26f;
        var title = Text(panel, "感情ツリー（冒険者の体験で感情が貯まる／◆=Eurekaでコスト-40%／◆=2ルートの複合）", 16, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 16, FS_W - 120, 24);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => emotionPanel.SetActive(false));
        Place((RectTransform)close.transform, FS_W - pad - 32, 14, 32, 30);

        emotionNodeContainer = MakeVScroll(panel, pad, 62f, FS_W - pad * 2, FS_H - 62f - pad);

        RefreshEmotionPanel();
        emotionPanel.SetActive(false);
    }

    private void RefreshEmotionPanel()
    {
        var et = EmotionTreeManager.Instance; if (et == null || emotionNodeContainer == null) return;
        for (int i = emotionNodeContainer.childCount - 1; i >= 0; i--)
        { var c = emotionNodeContainer.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }

        float W = FS_W - 52f;
        float cellW = 236f, cellH = 74f, hGap = 40f, vGap = 14f;
        float y = 4f;

        // ── 4ルートを行として並べ、段(tier)を列にする（Civ風の横ツリー）──
        for (int r = 0; r < 4; r++)
        {
            var route = (EmotionTreeManager.Route)r;
            var head = Text(emotionNodeContainer, EmotionRouteHeadText(r, et), 15, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(head.rectTransform, 2, y, W - 4, 20);
            emoRouteHeads[r] = head;
            float rowY = y + 24f;
            for (int t = 0; t < 4; t++)
            {
                var n = et.Get(route, t); if (n == null) continue;
                float cx = t * (cellW + hGap);
                if (t > 0) LineRect(emotionNodeContainer, cx - hGap, rowY + cellH / 2f - 1f, hGap, 2f); // 段の接続線
                AddEmotionCell(n, cx, rowY, cellW, cellH, et);
            }
            y = rowY + cellH + vGap + 6f;
        }

        // ── ✨ 複合ノード ──
        var fh = Text(emotionNodeContainer, "◆ 複合（2つのルートを進めると解禁・感情は両方から半分ずつ支払う）", 15, C("#8cb8e6"), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(fh.rectTransform, 2, y, W - 4, 20); y += 26f;
        for (int i = 0; i < et.Fusions.Count; i++)
        {
            var n = et.Fusions[i];
            float cx = (i % 4) * (cellW + hGap), cy = y + (i / 4) * (cellH + vGap);
            AddEmotionCell(n, cx, cy, cellW, cellH, et);
        }
        y += ((et.Fusions.Count + 3) / 4) * (cellH + vGap) + 10f;

        // 研究連携の説明
        var link = Text(emotionNodeContainer,
            "<color=#5cc47c>研究連携</color>：各ルートの最終段は毎ターン研究点+1（現在 +" + et.ResearchPointBonus + "）／魔王研究『感情増幅』で感情+35%",
            12, MUTED, TextAlignmentOptions.TopLeft);
        Place(link.rectTransform, 2, y, W - 4, 20); y += 26f;

        emotionNodeContainer.sizeDelta = new Vector2(0f, y + 12f);
    }

    // 感情ノード1セル（通常/複合の両対応）
    private void AddEmotionCell(EmotionTreeManager.Node n, float x, float y, float w, float h, EmotionTreeManager et)
    {
        bool can = et.CanUnlock(n);
        bool prereqOK = n.isFusion ? et.FusionPrereqMet(n) : (n.tier == 0 || et.IsUnlocked(n.route, n.tier - 1));
        var cell = Panel(emotionNodeContainer, "emo_" + n.name, CARD);
        Place(cell.rectTransform, x, y, w, h);
        Outline(cell, n.unlocked ? GREEN : (can ? GOLD : LINE));

        string star = et.EurekaReady(n) && !n.unlocked ? " <color=#f5c56b>◆</color>" : "";
        var nm = Text(cell.rectTransform, (n.isFusion ? "◆" : "") + n.name + star, 13, n.unlocked ? GREEN : (prereqOK ? TEXT : FAINT), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(nm.rectTransform, 9, 5, w - 18, 17);

        int cost = et.EffectiveCost(n);
        string state;
        if (n.unlocked) state = "解禁済";
        else if (!prereqOK) state = n.isFusion
            ? "― " + EmotionTreeManager.RouteNames[(int)n.reqRouteA] + (n.reqTierA + 1) + "段 ＋ " + EmotionTreeManager.RouteNames[(int)n.reqRouteB] + (n.reqTierB + 1) + "段"
            : "― 前段が必要";
        else state = n.isFusion
            ? EmotionTreeManager.RouteNames[(int)n.reqRouteA] + "/" + EmotionTreeManager.RouteNames[(int)n.reqRouteB] + " 各" + (cost / 2)
            : "コスト " + cost + (n.eurekaHint != null ? "　<size=85%><color=#6f6889>(" + n.eurekaHint + ")</color></size>" : "");
        var st = Text(cell.rectTransform, state, 10.5f, n.unlocked ? GREEN : (can ? GOLD : MUTED), TextAlignmentOptions.TopLeft);
        Place(st.rectTransform, 9, 24, w - 18, 15);

        var ds = Text(cell.rectTransform, n.desc, 9.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(ds.rectTransform, 9, 41, w - 18, h - 44);

        if (can)
        {
            var node = n;
            var btn = cell.gameObject.AddComponent<Button>(); btn.targetGraphic = cell;
            btn.onClick.AddListener(() => { if (EmotionTreeManager.Instance.TryUnlock(node)) RefreshEmotionPanel(); });
        }
    }

    // ---------- フロアタブ（階層切替） ----------
    private void BuildFloorTabs(RectTransform root)
    {
        var panel = Panel(root, "FloorTabs", C("#0e0b16"));
        floorTabsPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
        panel.rectTransform.sizeDelta = new Vector2(5 * 76 + 12, 34);
        panel.rectTransform.anchoredPosition = new Vector2(0, -66);
        Outline(panel, LINE2);
        var h = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(6, 6, 4, 4); h.spacing = 6; h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            var b = Panel(panel, "FloorTab_" + i, PANEL2); SizeElem(b.gameObject, 70, 26); Outline(b, LINE);
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
            btn.onClick.AddListener(() => { floorMgr?.SwitchTo(idx); RefreshFloorTabs(); });
            var t = Text(b.rectTransform, "B" + (i + 1) + "F", 12, TEXT, TextAlignmentOptions.Center, FontStyles.Bold); StretchFull(t.rectTransform);
            floorTabs.Add((b, t, idx));
        }
        RefreshFloorTabs();
    }

    private void RefreshFloorTabs()
    {
        if (floorTabsPanel == null) return;
        int n = floorMgr != null ? floorMgr.BuiltFloorCount : 0;
        if (n <= 1) { floorTabsPanel.SetActive(false); return; } // 1層のみなら非表示
        floorTabsPanel.SetActive(true);
        for (int i = 0; i < floorTabs.Count; i++)
        {
            bool on = i < n;
            floorTabs[i].img.gameObject.SetActive(on);
            if (!on) continue;
            bool cur = i == floorMgr.CurrentFloorIndex;
            bool deepest = floorMgr.IsLordFloor(i);   // 👑 『魔』印は最下層ではなく**魔王が立つ階**に付く（親征で動く）
            SetTxt(floorTabs[i].label, "B" + (i + 1) + "F" + (deepest ? "魔" : ""));
            floorTabs[i].img.color = cur ? SEL : PANEL2;
            var o = floorTabs[i].img.GetComponent<Outline>(); if (o != null) o.effectColor = cur ? GOLD : (deepest ? CRIMSON : LINE);
            floorTabs[i].label.color = cur ? GOLD : (deepest ? CRIMSON : TEXT);
        }
    }

    // ---------- 遺物パネル（3層バフ・全体層） ----------
    private void BuildRelicPanel(RectTransform root)
    {
        var panel = Panel(root, "RelicPanel", PANEL);
        relicPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(FS_W, FS_H);
        panel.rectTransform.anchoredPosition = new Vector2(0, 0);
        Outline(panel, LINE2); SkinPanel(panel);

        float pad = 22f, w = FS_W - pad * 2;
        var title = Text(panel, "遺物（全体パッシブ・実績で獲得 → スロットぶんだけ装備）", 16, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 14, w - 60, 24);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => relicPanel.SetActive(false));
        Place((RectTransform)close.transform, FS_W - pad - 32, 12, 32, 30);

        relicSlotText = Text(panel, "装備スロット: ―", 12.5f, VIOLET, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(relicSlotText.rectTransform, pad, 44, w, 18);

        var rm = RelicManager.Instance;
        int count = rm != null ? rm.Catalog.Count : 0;
        float cw = (w - 3 * 12) / 4f, ch = 96f;
        for (int i = 0; i < count; i++)
        {
            int idx = i; var rel = rm.Catalog[i];
            float cx = pad + (i % 4) * (cw + 12);
            float cy = 72 + (i / 4) * (ch + 10);
            var card = Panel(panel, "Relic_" + i, CARD);
            Place(card.rectTransform, cx, cy, cw, ch); Outline(card, LINE);
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
            btn.onClick.AddListener(() => { RelicManager.Instance?.Toggle(idx); RefreshRelicPanel(); });
            var nm = Text(card.rectTransform, rel.name, 13.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm.rectTransform, 10, 8, cw - 16, 18);
            var ds = Text(card.rectTransform, rel.desc, 10.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(ds.rectTransform, 10, 28, cw - 16, 30);
            var st = Text(card.rectTransform, "", 10.5f, GREEN, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(st.rectTransform, 10, 62, cw - 16, 28);
            AddTooltip(card.gameObject, rel.name + "：" + rel.desc + "\n<color=#9c95b4>獲得条件: " + rel.howTo + "</color>");
            relicCards.Add((card, st, idx));
        }

        RefreshRelicPanel();
        relicPanel.SetActive(false);
    }

    private void RefreshRelicPanel()
    {
        var rm = RelicManager.Instance; if (rm == null) return;
        if (relicSlotText != null)
        {
            var parts = new List<string>();
            for (int i = 0; i < rm.SlotCount; i++)
            {
                int ci = rm.SlotAt(i);
                parts.Add(ci >= 0 ? rm.Catalog[ci].name : "空き");
            }
            relicSlotText.text = "装備スロット(" + rm.SlotCount + "): " + string.Join(" / ", parts)
                + "　<color=#9c95b4>― 獲得 " + rm.UnlockedCount + "/" + rm.Catalog.Count
                + "　スロットは領域研究『遺物の祭壇／宝物庫／霊廟』で4つまで増える</color>";
        }
        foreach (var c in relicCards)
        {
            bool got = rm.IsUnlocked(c.idx);
            bool eq = rm.IsEquipped(c.idx);
            if (c.label != null)
            {
                c.label.text = !got ? "<color=#6f6889>― 未獲得: " + rm.Catalog[c.idx].howTo + "</color>"
                             : eq ? "装備中" : "未装備";
                c.label.color = eq ? GREEN : FAINT;
            }
            if (c.card != null)
            {
                c.card.color = eq ? SEL : CARD;
                var o = c.card.GetComponent<Outline>(); if (o != null) o.effectColor = eq ? GOLD : (got ? LINE2 : LINE);
                // 未獲得は淡色
                var g = c.card.GetComponent<CanvasGroup>(); if (g == null) g = c.card.gameObject.AddComponent<CanvasGroup>();
                g.alpha = got ? 1f : 0.5f;
            }
        }
    }
}
