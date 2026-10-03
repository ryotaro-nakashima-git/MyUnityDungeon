using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🛡️ <b>配下の画面</b>（H1・承認済みの改善案）。図鑑の「個体」タブを置き換える全画面。
///
/// 左＝名簿（絵の札）／真ん中＝<b>配属の盤</b>（階ごとにボス1枠＋隊の枠、地上の眷属）／右＝選んだ1体。
/// 札を掴んで枠へ落とす（または札を押して枠を押す）だけで配属が決まる。
///
/// ⚠⚠ ユーザー指摘の二度手間を消すのがこの画面の目的：
///   以前は隊＝図鑑の個体タブ、ボス＝盤の『ボス』道具で、ボスは隊に入れないので
///   「ボスにしたい1体は隊に入れずに盤から選ぶ」しかなかった。
///   いまは**ボス枠へ入れると隊から自動で外れ、隊の枠へ入れるとボスから自動で外れる**
///   （`DungeonFeatureManager.AppointBoss` / `AssignSquad`）。
/// ⚠ 決まりは変えない：1体は1か所だけ／ボスと隊員の配置は無償／ユニークは隊の枠を食わない／眷属は地上の窓で動かす。
/// <para>`GameUIManager` の partial。</para>
/// </summary>
public partial class GameUIManager
{
    private GameObject armyPanel;
    private RectTransform armyRoster, armyFloors, armyDetail, armyDetailBody;
    private float armyRosterW, armyFloorsW, armyDetailW;
    private TextMeshProUGUI[] armyChips;
    private TextMeshProUGUI armyRosterHead;
    private readonly List<Image> armyFilterBtns = new List<Image>();
    private readonly List<Image> armySubBtns = new List<Image>();
    private int armySel = -1, armyFilter = 0, armySub = 0;
    private bool armySortLv = true;
    private TextMeshProUGUI armySortLabel;

    private static readonly string[] ArmyFilterNames = { "全員", "待機", "隊", "ボス", "地上" };

    // ================= 組み立て =================
    private void BuildArmyPanel(RectTransform root)
    {
        float FW, FH;
        var panel = FullPanel(root, "ArmyPanel", out FW, out FH);
        armyPanel = panel.gameObject;
        RectTransform tabHost; Image chipHost;
        BuildFullHeader(panel, FW, "配下", "札を掴んで真ん中の枠へ。ボス枠へ入れると隊から自動で外れる",
            () => armyPanel.SetActive(false), out tabHost, out chipHost);
        FullTab(tabHost, "編成", 132f, true, false, () => { });
        FullTab(tabHost, "図鑑", 132f, false, false, () =>
        {
            armyPanel.SetActive(false);
            if (codexFamilyTab == 4) codexFamilyTab = 0;
            OpenExclusive(minionPanel); RefreshMinionCodex(); RefreshSquadTray();
        });
        armyChips = new[]
        {
            ResChip(chipHost, UITheme.DP, "DP", "0", "dp"),
            ResChip(chipHost, UITheme.Material, "素材", "0", "material"),
            ResChip(chipHost, C("#9c95b4"), "配下", "0", null),
        };

        float top = FullHdrH + 16f, bottom = 20f, gap = 16f, pad = 24f;
        float h = FH - top - bottom;
        armyDetailW = 470f; armyFloorsW = 540f;
        armyRosterW = FW - pad * 2 - armyFloorsW - armyDetailW - gap * 2;

        // ── 左：名簿 ──
        var rc = Panel(panel, "ArmyRosterCol", C("#191726"));
        Place(rc.rectTransform, pad, top, armyRosterW, h); Outline(rc, LINE2);
        armyRosterHead = Text(rc.rectTransform, "名簿", 19, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(armyRosterHead.rectTransform, 16, 12, armyRosterW - 32, 26);
        armyFilterBtns.Clear();
        for (int i = 0; i < ArmyFilterNames.Length; i++)
        {
            int fi = i;
            var b = PrimaryButton(rc, ArmyFilterNames[i], PANEL2, MUTED, () => { armyFilter = fi; RefreshArmy(); });
            Place((RectTransform)b.transform, 16 + i * 74, 46, 68, 28);
            armyFilterBtns.Add(b.GetComponent<Image>());
        }
        var sortB = PrimaryButton(rc, "", PANEL2, MUTED, () => { armySortLv = !armySortLv; RefreshArmy(); });
        Place((RectTransform)sortB.transform, armyRosterW - 16 - 120, 46, 120, 28);
        armySortLabel = sortB.GetComponentInChildren<TextMeshProUGUI>();
        armyRoster = MakeVScroll(rc, 16, 86, armyRosterW - 32, h - 86 - 12);

        // ── 真ん中：配属の盤 ──
        var fc = Panel(panel, "ArmyFloorsCol", C("#191726"));
        Place(fc.rectTransform, pad + armyRosterW + gap, top, armyFloorsW, h); Outline(fc, LINE2);
        var fh = Text(fc.rectTransform, "配属の盤　<size=70%><color=#9c95b4>階ごとにボス1体と隊。枠を押すと選んだ1体が入る</color></size>",
            19, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(fh.rectTransform, 16, 12, armyFloorsW - 32, 26);
        armyFloors = MakeVScroll(fc, 16, 48, armyFloorsW - 32, h - 48 - 12);

        // ── 右：選んだ1体 ──
        var dc = Panel(panel, "ArmyDetailCol", C("#191726"));
        Place(dc.rectTransform, FW - pad - armyDetailW, top, armyDetailW, h); Outline(dc, LINE2);
        armyDetail = dc.rectTransform;

        armyPanel.SetActive(false);
    }

    /// <summary>配下の画面を開く（図鑑の「個体」タブ・ほかの入口から）。</summary>
    private void OpenArmy()
    {
        if (armyPanel == null) return;
        if (minionPanel != null) minionPanel.SetActive(false);
        if (!armyPanel.activeSelf) OpenExclusive(armyPanel);
        armyPanel.transform.SetAsLastSibling();
        RefreshArmy();
    }

    // ================= 描き直し =================
    private void RefreshArmy()
    {
        if (armyPanel == null || !armyPanel.activeSelf) return;
        if (armyChips != null && res != null)
        {
            SetNumber(armyChips[0], res.DungeonPoints);
            SetNumber(armyChips[1], res.CraftMaterials);
            armyChips[2].text = MinionRoster.All.Count + "体";
        }
        if (armySel >= 0 && MinionRoster.Get(armySel) == null) armySel = -1;
        if (armySel < 0 && MinionRoster.All.Count > 0) armySel = MinionRoster.All[0].id;
        RefreshArmyRoster();
        RefreshArmyFloors();
        RefreshArmyDetail();
    }

    /// <summary>いまの配属。kind: 0待機 1隊 2ボス 3地上 4訓練</summary>
    private string ArmyAssignLabel(int id, out int kind, out bool placed)
    {
        kind = 0; placed = featureMgr != null && featureMgr.IsIndividualPlaced(id);
        var kin = KinRoster.Of(id); var leader = KinRoster.LeaderOfFollower(id);
        if (kin != null) { kind = 3; return "地上・眷属"; }
        if (leader != null) { kind = 3; return "地上・" + leader.trueName + "の配下"; }
        if (TrainingSystem.IsTraining(id)) { kind = 4; return "訓練中"; }
        if (featureMgr == null) return "待機";
        int bf = featureMgr.BossFloorOfIndividual(id);
        if (bf >= 0) { kind = 2; return "B" + (bf + 1) + "F ボス"; }
        int sf = featureMgr.SquadFloorOfIndividual(id);
        if (sf >= 0) { kind = 1; return "B" + (sf + 1) + "F 隊"; }
        return "待機";
    }

    private static Color ArmyKindColor(int kind)
    {
        switch (kind)
        {
            case 1: return new Color(0.07f, 0.19f, 0.16f);
            case 2: return new Color(0.23f, 0.09f, 0.13f);
            case 3: return new Color(0.2f, 0.16f, 0.06f);
            case 4: return new Color(0.2f, 0.13f, 0.06f);
            default: return new Color(0.13f, 0.12f, 0.19f);
        }
    }
    private static string ArmyKindHex(int kind)
    {
        switch (kind) { case 1: return "#7fe0c2"; case 2: return "#f0a0a0"; case 3: return "#ffd24a"; case 4: return "#e08a3c"; default: return "#6f6889"; }
    }

    private void RefreshArmyRoster()
    {
        var c = armyRoster; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        for (int i = 0; i < armyFilterBtns.Count; i++) SetSel(armyFilterBtns[i], i == armyFilter);
        if (armySortLabel != null) armySortLabel.text = armySortLv ? "並び：Lv順" : "並び：種類順";

        var list = new List<MinionRoster.Individual>();
        int idle = 0;
        foreach (var v in MinionRoster.All)
        {
            int kind; bool pl; ArmyAssignLabel(v.id, out kind, out pl);
            if (kind == 0) idle++;
            bool show = armyFilter == 0 || (armyFilter == 1 && (kind == 0 || kind == 4)) || (armyFilter == 2 && kind == 1)
                     || (armyFilter == 3 && kind == 2) || (armyFilter == 4 && kind == 3);
            if (show) list.Add(v);
        }
        if (armySortLv) list.Sort((a, b) => b.level != a.level ? b.level.CompareTo(a.level) : a.id.CompareTo(b.id));
        else list.Sort((a, b) => a.catalogIndex != b.catalogIndex ? a.catalogIndex.CompareTo(b.catalogIndex) : b.level.CompareTo(a.level));
        if (armyRosterHead != null)
            SetTxt(armyRosterHead, "名簿　<size=70%><color=#9c95b4>" + MinionRoster.All.Count + "体（待機 " + idle + "）</color></size>");

        float W = armyRosterW - 32f;
        int cols = Mathf.Max(3, Mathf.FloorToInt((W + 10f) / 160f));
        float cw = (W - 10f * (cols - 1)) / cols, ch = 196f;
        if (list.Count == 0)
        {
            var e = Text(c, MinionRoster.All.Count == 0
                ? "<color=#9c95b4>まだ配下が居ない。図鑑で種を選んで『召喚』するか、左の列の『召喚』で引く。</color>"
                : "<color=#9c95b4>この絞り込みに当てはまる配下は居ない。</color>", 14, MUTED, TextAlignmentOptions.TopLeft);
            Place(e.rectTransform, 4, 6, W - 8, 60);
            c.sizeDelta = new Vector2(0f, 80f);
            return;
        }
        for (int i = 0; i < list.Count; i++)
        {
            int col = i % cols, row = i / cols;
            ArmyCard(c, list[i], col * (cw + 10f), row * (ch + 12f), cw, ch);
        }
        c.sizeDelta = new Vector2(0f, ((list.Count + cols - 1) / cols) * (ch + 12f) + 8f);
    }

    private void ArmyCard(RectTransform parent, MinionRoster.Individual v, float x, float y, float w, float h)
    {
        var d = MinionCatalog.Get(v.catalogIndex);
        int kind; bool placed;
        string asg = ArmyAssignLabel(v.id, out kind, out placed);
        bool sel = v.id == armySel;
        var card = Panel(parent, "Army_" + v.id, sel ? C("#2a2235") : CARD);
        Place(card.rectTransform, x, y, w, h); Outline(card, sel ? GOLD : LINE2);
        var bt = card.gameObject.AddComponent<Button>(); bt.targetGraphic = card;
        int id = v.id;
        bt.onClick.AddListener(() => { armySel = id; RefreshArmy(); });

        var sp = MinionSprite.ByIndex(v.catalogIndex);
        var art = new GameObject("Art", typeof(RectTransform)).AddComponent<Image>();
        art.rectTransform.SetParent(card.rectTransform, false);
        art.raycastTarget = false; art.preserveAspect = true;
        art.sprite = sp != null ? sp : IconFactory.Get("魔物");
        Place(art.rectTransform, (w - 84f) * 0.5f, 22, 84, 84);

        // 左上＝格／右上＝気性
        if (v.rank > 0)
        {
            var rk = Text(card.rectTransform, "▲" + MinionRank.Name(v.rank), 11.5f, C(MinionRank.ColorOf(v.rank)), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            rk.enableWordWrapping = false;
            Place(rk.rectTransform, 7, 4, w * 0.5f, 18);
        }
        var td = MinionTemperament.Get(v.temper);
        var tp = Text(card.rectTransform, td.jpName, 11.5f, C(td.colorHex), TextAlignmentOptions.TopRight, FontStyles.Bold);
        tp.enableWordWrapping = false;
        Place(tp.rectTransform, w * 0.45f, 4, w * 0.55f - 7, 18);

        var nm = Text(card.rectTransform, MinionRoster.NameOf(v), 16, TEXT, TextAlignmentOptions.Top, FontStyles.Bold);
        nm.enableWordWrapping = false; nm.enableAutoSizing = true; nm.fontSizeMin = 11; nm.fontSizeMax = 16;
        Place(nm.rectTransform, 4, 110, w - 8, 24);
        var spn = Text(card.rectTransform, d.jpName + "　Lv" + v.level, 12.5f, MUTED, TextAlignmentOptions.Top);
        spn.enableWordWrapping = false; spn.enableAutoSizing = true; spn.fontSizeMin = 9; spn.fontSizeMax = 12.5f;
        Place(spn.rectTransform, 4, 134, w - 8, 20);
        // Lv の棒（次のLvまで）
        var track = Panel(card, "LvTrack", C("#211f31"));
        Place(track.rectTransform, 8, 158, w - 16, 5);
        float frac = v.level >= MinionRoster.MaxLevel ? 1f : Mathf.Clamp01(v.exp / (float)MinionRoster.ExpPerLevel);
        var fill = Panel(track, "Fill", C("#5cc47c"));
        Place(fill.rectTransform, 0, 0, (w - 16) * frac, 5);
        // 下の帯＝いまの配属（色で読める）
        var band = Panel(card, "Assign", ArmyKindColor(kind));
        Place(band.rectTransform, 1, h - 26, w - 2, 25);
        var bl = Text(band.rectTransform, asg + (kind == 1 || kind == 2 ? (placed ? "" : " <size=85%>(未配置)</size>") : ""),
            12.5f, C(ArmyKindHex(kind)), TextAlignmentOptions.Center, FontStyles.Bold);
        bl.enableWordWrapping = false; bl.enableAutoSizing = true; bl.fontSizeMin = 9; bl.fontSizeMax = 12.5f;
        StretchFull(bl.rectTransform);

        // 🖐️ 掴んで配属の盤へ（地上に出ている者は動かせない）
        if (kind != 3)
        {
            var dr = card.gameObject.AddComponent<ArmyDrag>();
            dr.individualId = id; dr.art = art.sprite;
            dr.onDrop = (iid, slot) => { if (slot != null) ArmyAssign(iid, slot.floor, slot.boss); };
        }
        AddTooltip(card.gameObject, CodexIndividualTip(v));
    }

    private string CodexIndividualTip(MinionRoster.Individual v)
    {
        var d = MinionCatalog.Get(v.catalogIndex);
        var td = MinionTemperament.Get(v.temper);
        return "<b>" + MinionRoster.NameOf(v) + "</b>　" + d.jpName + " Lv" + v.level
            + "\n<color=" + td.colorHex + ">気性『" + td.jpName + "』</color>　<color=#9c95b4>" + MinionCatalog.RoleName(d.role) + "</color>"
            + "\n<color=#6f6889>押すと右に詳しく・掴んで真ん中の枠へ</color>";
    }

    /// <summary>配属する（ボス枠／隊の枠）。⚠ 自動で前の配属から外れる（二度手間を消す）。</summary>
    private void ArmyAssign(int id, int floor, bool boss)
    {
        if (featureMgr == null || id < 0) return;
        string why; bool ok = boss ? featureMgr.AppointBoss(floor, id, out why) : featureMgr.AssignSquad(floor, id, out why);
        if (!ok)
        {
            NotifySystem.Push("配属できない：" + why, NotifySystem.Kind.Loss);
            SoundSystem.Play(SoundSystem.Sfx.Error);
        }
        else SoundSystem.Play(SoundSystem.Sfx.Place, 0.7f);
        armySel = id;
        RefreshArmy(); RefreshSquadTray(); RefreshSquadStrip(); RefreshBossStrip();
    }

    private void RefreshArmyFloors()
    {
        var c = armyFloors; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float W = armyFloorsW - 32f, y = 0f;
        int floors = floorMgr != null ? Mathf.Max(1, floorMgr.BuiltFloorCount) : 1;
        float bossS = 74f;
        for (int f = 0; f < floors; f++)
        {
            int cap = featureMgr != null ? featureMgr.SquadMaxSlotsOf(f) : 5;
            float slotS = Mathf.Min(64f, (W - 24 - bossS - 20f) / Mathf.Max(1, cap) - 6f);
            var squad = featureMgr != null ? featureMgr.SquadListOf(f) : new List<int>();
            int boss = featureMgr != null ? featureMgr.AppointedBossOf(f) : -1;
            int placedN = 0, assignedN = squad.Count + (boss >= 0 ? 1 : 0);
            foreach (var sid in squad) if (featureMgr.IsIndividualPlaced(sid)) placedN++;
            if (boss >= 0 && featureMgr.BossPlacedOn(f)) placedN++;

            float bh = 136f;
            var blk = Panel(c, "Floor_" + f, C("#16141f"));
            Place(blk.rectTransform, 0, y, W, bh); Outline(blk, LINE);
            var hd = Text(blk.rectTransform, "B" + (f + 1) + "F", 17, C("#8cb8e6"), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(hd.rectTransform, 12, 8, 80, 24);
            var stt = Text(blk.rectTransform, assignedN == 0 ? "<color=#6f6889>まだ誰も居ない</color>"
                : (placedN < assignedN ? "<color=#e08a3c>置いた " + placedN + "/" + assignedN + "</color>　<size=85%><color=#6f6889>下の『部隊』で盤へ</color></size>"
                                        : "<color=#5cc47c>置いた " + placedN + "/" + assignedN + "</color>"),
                13, MUTED, TextAlignmentOptions.TopRight, FontStyles.Bold);
            stt.enableWordWrapping = false;
            Place(stt.rectTransform, 90, 10, W - 102, 22);

            float sy = 50f;
            var bl = Text(blk.rectTransform, "ボス", 12, C("#f0a0a0"), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(bl.rectTransform, 12, sy - 18, 60, 16);
            ArmySlotCell(blk.rectTransform, f, true, boss, 12, sy, bossS);
            var sep = Panel(blk, "Sep", LINE2);
            Place(sep.rectTransform, 12 + bossS + 10, sy + 6, 1, bossS - 12);
            var sl = Text(blk.rectTransform, "隊　<size=85%><color=#6f6889>" + squad.Count + "/" + cap + "</color></size>", 12, C("#7fe0c2"), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(sl.rectTransform, 12 + bossS + 20, sy - 18, 120, 16);
            for (int s = 0; s < cap; s++)
            {
                int occ = s < squad.Count ? squad[s] : -1;
                ArmySlotCell(blk.rectTransform, f, false, occ, 12 + bossS + 20 + s * (slotS + 6), sy + (bossS - slotS), slotS);
            }
            y += bh + 10f;
        }

        // ── 地上（眷属）：ここでは並べるだけ。動かすのは地上の窓 ──
        var kins = KinRoster.All;
        float kh = 104f;
        var kb = Panel(c, "Surface", C("#1b170c"));
        Place(kb.rectTransform, 0, y, W, kh); Outline(kb, C("#6b5a1f"));
        var kt = Text(kb.rectTransform, "地上（眷属）　<size=80%><color=#9c95b4>" + kins.Count + "体・地上の窓で動かす</color></size>", 15, C("#ffd24a"), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(kt.rectTransform, 12, 8, W - 24, 22);
        for (int k = 0; k < kins.Count && k < 7; k++)
        {
            var kv = MinionRoster.Get(kins[k].individualId);
            var cell = Panel(kb, "Kin_" + k, CARD);
            Place(cell.rectTransform, 12 + k * 64, 38, 58, 58); Outline(cell, LINE2);
            if (kv != null)
            {
                var a = new GameObject("Art", typeof(RectTransform)).AddComponent<Image>();
                a.rectTransform.SetParent(cell.rectTransform, false); a.raycastTarget = false; a.preserveAspect = true;
                var ksp = MinionSprite.ByIndex(kv.catalogIndex); a.sprite = ksp != null ? ksp : IconFactory.Get("魔物");
                Place(a.rectTransform, 5, 3, 48, 46);
                int kid = kv.id;
                var kbt = cell.gameObject.AddComponent<Button>(); kbt.targetGraphic = cell;
                kbt.onClick.AddListener(() => { armySel = kid; RefreshArmy(); });
                AddTooltip(cell.gameObject, "眷属『" + kins[k].trueName + "』　" + MinionCatalog.Get(kv.catalogIndex).jpName + " Lv" + kv.level);
            }
        }
        if (kins.Count == 0)
        {
            var e = Text(kb.rectTransform, "<color=#6f6889>まだ眷属が居ない。右の「育成」から真名を与えると、地上で軍を率いる。</color>", 12.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(e.rectTransform, 12, 40, W - 24, 40);
        }
        y += kh + 8f;
        c.sizeDelta = new Vector2(0f, y);
    }

    private void ArmySlotCell(RectTransform parent, int floor, bool boss, int occ, float x, float y, float s)
    {
        var cell = Panel(parent, (boss ? "Boss_" : "Sq_") + floor + "_" + x, boss ? C("#24141b") : C("#1b1928"));
        Place(cell.rectTransform, x, y, s, s); Outline(cell, boss ? C("#a54a55") : (occ >= 0 ? LINE2 : C("#332e49")));
        var tag = cell.gameObject.AddComponent<ArmySlot>(); tag.floor = floor; tag.boss = boss;
        var bt = cell.gameObject.AddComponent<Button>(); bt.targetGraphic = cell;
        var v = occ >= 0 ? MinionRoster.Get(occ) : null;
        if (v != null)
        {
            var a = new GameObject("Art", typeof(RectTransform)).AddComponent<Image>();
            a.rectTransform.SetParent(cell.rectTransform, false); a.raycastTarget = false; a.preserveAspect = true;
            var sp = MinionSprite.ByIndex(v.catalogIndex); a.sprite = sp != null ? sp : IconFactory.Get("魔物");
            bool placed = featureMgr != null && featureMgr.IsIndividualPlaced(occ);
            a.color = placed ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            Place(a.rectTransform, 4, 2, s - 8, s - 16);
            var lv = Text(cell.rectTransform, "Lv" + v.level + (placed ? "" : " 未"), 10, placed ? C("#5cc47c") : C("#e08a3c"), TextAlignmentOptions.Bottom, FontStyles.Bold);
            lv.enableWordWrapping = false;
            Place(lv.rectTransform, 1, s - 16, s - 2, 15);
            if (occ == armySel) Outline(cell, GOLD);
            AddTooltip(cell.gameObject, "<b>" + MinionRoster.NameOf(v) + "</b>　" + MinionCatalog.Get(v.catalogIndex).jpName + " Lv" + v.level
                + (placed ? "" : "\n<color=#e08a3c>まだ盤に置いていない（下の『部隊』で置く）</color>")
                + "\n<color=#6f6889>押すと選ぶ／ほかの札を選んでから押すと入れ替え</color>");
        }
        else
        {
            var t = Text(cell.rectTransform, "＋", boss ? 24 : 20, C("#463f5c"), TextAlignmentOptions.Center);
            StretchFull(t.rectTransform);
            AddTooltip(cell.gameObject, boss ? "B" + (floor + 1) + "F のボス枠。選んだ1体を入れる（隊に居れば自動で外れる）"
                                           : "B" + (floor + 1) + "F の隊の枠。選んだ1体を入れる（ボスなら自動で外れる）");
        }
        int f0 = floor; bool b0 = boss; int o0 = occ;
        bt.onClick.AddListener(() =>
        {
            // 札を選んでいて、それが別の者なら入れる。そうでなければ、入っている者を選ぶ
            if (armySel >= 0 && armySel != o0 && MinionRoster.Get(armySel) != null && !KinRoster.IsAwayFromDungeon(armySel))
            {
                if (o0 >= 0 && !b0) featureMgr.UnassignIndividual(o0);   // 隊の枠が埋まっていたら入れ替え
                ArmyAssign(armySel, f0, b0);
            }
            else if (o0 >= 0) { armySel = o0; RefreshArmy(); }
        });
    }

    // ================= 右：選んだ1体 =================
    private void RefreshArmyDetail()
    {
        var c = armyDetail; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float W = armyDetailW, pad = 20f;
        var v = armySel >= 0 ? MinionRoster.Get(armySel) : null;
        if (v == null)
        {
            var e = Text(c, "<color=#9c95b4>名簿の札を押すと、ここに詳しく出ます。</color>", 14, MUTED, TextAlignmentOptions.TopLeft);
            Place(e.rectTransform, pad, 20, W - pad * 2, 40);
            return;
        }
        var d = MinionCatalog.Get(v.catalogIndex);
        int id = v.id;
        int kind; bool placed; string asg = ArmyAssignLabel(id, out kind, out placed);

        var frame = Panel(c, "Por", C("#15131f"));
        Place(frame.rectTransform, pad, 18, 132, 132); Outline(frame, LINE2);
        var art = new GameObject("Art", typeof(RectTransform)).AddComponent<Image>();
        art.rectTransform.SetParent(frame.rectTransform, false); art.raycastTarget = false; art.preserveAspect = true;
        var sp = MinionSprite.ByIndex(v.catalogIndex); art.sprite = sp != null ? sp : IconFactory.Get("魔物");
        Place(art.rectTransform, 8, 8, 116, 116);

        float tx = pad + 132 + 16, tw = W - tx - pad;
        var nm = Text(c, MinionRoster.NameOf(v), 26, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        nm.enableWordWrapping = false; nm.enableAutoSizing = true; nm.fontSizeMin = 15; nm.fontSizeMax = 26;
        Place(nm.rectTransform, tx, 16, tw, 34);
        var spn = Text(c, d.jpName + "　<color=" + RankHex(d.rank) + ">等級" + MinionCatalog.RankName(d.rank) + "</color>・" + MinionCatalog.RoleName(d.role),
            13.5f, MUTED, TextAlignmentOptions.TopLeft);
        spn.enableWordWrapping = false; spn.enableAutoSizing = true; spn.fontSizeMin = 10; spn.fontSizeMax = 13.5f;
        Place(spn.rectTransform, tx, 52, tw, 20);
        var al = Text(c, "<color=" + ArmyKindHex(kind) + ">" + asg + "</color>"
            + (kind == 1 || kind == 2 ? (placed ? "　<color=#6f6889>盤に置いた</color>" : "　<color=#e08a3c>まだ置いていない</color>") : ""),
            14, MUTED, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        al.enableWordWrapping = false;
        Place(al.rectTransform, tx, 74, tw, 20);
        // Lv と格の棒
        float lvFrac = v.level >= MinionRoster.MaxLevel ? 1f : Mathf.Clamp01(v.exp / (float)MinionRoster.ExpPerLevel);
        DetailBar(c, "Lv " + v.level + (v.level >= MinionRoster.MaxLevel ? "　MAX" : ""), v.level >= MinionRoster.MaxLevel ? "" : "exp " + v.exp + "/" + MinionRoster.ExpPerLevel,
            lvFrac, C("#5cc47c"), tx, 98, tw);
        DetailBar(c, "格：" + (v.rank > 0 ? MinionRank.Name(v.rank) : "無印"), "武功 " + v.deed + "・撃破 " + v.kills,
            -1f, C(MinionRank.ColorOf(v.rank)), tx, 128, tw);

        float y = 162f;
        if (kind == 1 || kind == 2 || kind == 4)
        {
            var ub = PrimaryButton(c, "配属を解いて待機へ", PANEL2, MUTED, () => { featureMgr.UnassignIndividual(id); RefreshArmy(); RefreshSquadTray(); RefreshSquadStrip(); });
            Place((RectTransform)ub.transform, pad, y, 200, 30);
            if (kind == 4) ub.interactable = false;
            y += 40f;
        }

        // サブタブ
        armySubBtns.Clear();
        string[] subs = { "育成", "装備", "記録" };
        float sw = (W - pad * 2 - 12) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int si = i;
            var b = PrimaryButton(c, subs[i], PANEL2, i == armySub ? C("#ffd24a") : MUTED, () => { armySub = si; RefreshArmyDetail(); });
            Place((RectTransform)b.transform, pad + i * (sw + 6), y, sw, 32);
            var im = b.GetComponent<Image>(); SetSel(im, i == armySub); armySubBtns.Add(im);
        }
        y += 44f;
        var body = MakeVScroll((Image)c.GetComponent<Image>(), pad, y, W - pad * 2, c.rect.height - y - 14f);
        armyDetailBody = body;
        float bw = W - pad * 2;
        if (armySub == 0) ArmyGrowTab(body, v, bw);
        else if (armySub == 1) ArmyEquipTab(body, v, bw);
        else ArmyRecordTab(body, v, bw);
    }

    private void DetailBar(RectTransform c, string left, string right, float frac, Color col, float x, float y, float w)
    {
        var l = Text(c, left, 13.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        l.enableWordWrapping = false; Place(l.rectTransform, x, y, w * 0.55f, 18);
        var r = Text(c, right, 12, MUTED, TextAlignmentOptions.TopRight);
        r.enableWordWrapping = false; Place(r.rectTransform, x + w * 0.4f, y, w * 0.6f, 18);
        if (frac >= 0f)
        {
            var tr = Panel(c, "Bar_" + left, C("#211f31"));
            Place(tr.rectTransform, x, y + 19, w, 6);
            var fi = Panel(tr, "Fill", col);
            Place(fi.rectTransform, 0, 0, w * frac, 6);
        }
    }

    /// <summary>1行（左に見出しの箱・真ん中に説明・右にボタン）。返り値＝次の y。</summary>
    private float ArmyRow(RectTransform c, float y, float w, string head, string body, string btn, bool enabled,
                          UnityEngine.Events.UnityAction onClick, string tip = null, float h = 58f)
    {
        var box = Panel(c, "RowHead_" + head + y, C("#1b1928"));
        Place(box.rectTransform, 0, y + 6, 50, 46); Outline(box, LINE2);
        var ht = Text(box.rectTransform, head, 12, MUTED, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(ht.rectTransform);
        float bwid = string.IsNullOrEmpty(btn) ? 0f : 118f;
        var bt = Text(c, body, 13, TEXT, TextAlignmentOptions.MidlineLeft);
        bt.enableWordWrapping = true;
        Place(bt.rectTransform, 60, y, w - 60 - bwid - 8, h);
        if (!string.IsNullOrEmpty(btn))
        {
            var b = PrimaryButton(c, btn, enabled ? PANEL2 : PANEL, enabled ? C("#ffd24a") : C("#4a4560"), onClick);
            Place((RectTransform)b.transform, w - bwid, y + 14, bwid, 30);
            b.interactable = enabled;
            if (!string.IsNullOrEmpty(tip)) AddTooltip(b.gameObject, tip);
        }
        var ln = Panel(c, "Ln" + y, LINE);
        Place(ln.rectTransform, 0, y + h + 3, w, 1);
        return y + h + 8f;
    }

    private void ArmyGrowTab(RectTransform c, MinionRoster.Individual v, float w)
    {
        int id = v.id; float y = 4f;
        // 気性
        var td = MinionTemperament.Get(v.temper);
        string whyT; bool canT = MinionRoster.CanRetrain(id, out whyT);
        y = ArmyRow(c, y, w, "気性", "<color=" + td.colorHex + "><b>" + td.jpName + "</b></color>\n<size=88%><color=#9c95b4>" + td.desc + "</color></size>",
            "調教 " + MinionTemperament.RetrainCost + "DP", canT, () => { if (MinionRoster.CanRetrain(id, out whyT)) OpenTemperChoiceForRetrain(id); },
            canT ? "気性を2択で選び直す" : whyT, 66f);
        // 位（段5→6で選ぶ）
        if (MinionRank.AwaitingCrown(v))
        {
            if (MinionRank.CanChoose(v, MinionRank.CrownKing))
                y = ArmyRow(c, y, w, "位", "<b>キングの位</b>\n<size=88%><color=#9c95b4>麾下の軍団が兵科で不利な当たりをしなくなる。選ぶとクイーンは閉じる</color></size>",
                    "キングを継ぐ", true, () => { if (MinionRank.ChooseCrown(id, MinionRank.CrownKing)) RefreshArmy(); }, null, 66f);
            if (MinionRank.CanChoose(v, MinionRank.CrownQueen))
                y = ArmyRow(c, y, w, "位", "<b>クイーンの位</b>\n<size=88%><color=#9c95b4>統率+20／麾下の軍団が自領の外でも癒える。選ぶとキングは閉じる</color></size>",
                    "クイーンを継ぐ", true, () => { if (MinionRank.ChooseCrown(id, MinionRank.CrownQueen)) RefreshArmy(); }, null, 66f);
        }
        // 進化
        var kids = MinionEvolution.ChildrenOf(v.catalogIndex);
        if (kids.Count == 0)
            y = ArmyRow(c, y, w, "進化", "<color=#9c95b4>これ以上進化しない（最終形態）</color>", null, false, null);
        foreach (var ci in kids)
        {
            var cd = MinionCatalog.Get(ci);
            bool ok = MinionEvolution.CanIndividualEvolveTo(ci);
            int cost = MinionEvolution.EvolveCost(ci);
            int target = ci;
            string why = ok ? "<size=88%><color=#9c95b4>Lv・装備・格を保ったまま姿を変える</color></size>"
                : MinionEvolution.TierResearchNeeded(ci) ? "<size=88%><color=#8cb8e6>研究『" + MinionEvolution.TierResearchName(ci) + "』で開く</color></size>"
                : "<size=88%><color=#6f6889>この種の解禁が先</color></size>";
            y = ArmyRow(c, y, w, "進化", "<b>" + cd.jpName + "</b>　<size=85%><color=" + RankHex(cd.rank) + ">" + MinionCatalog.RankName(cd.rank) + "</color></size>\n" + why,
                ok ? "進化 " + cost + "DP" : "進化", ok, () => { if (MinionRoster.TryEvolveIndividual(id, target)) { RefreshArmy(); RefreshSquadTray(); } });
        }
        // 反芻
        if (TrainingSystem.IsTraining(id))
        {
            var tr = TrainingSystem.Of(id);
            y = ArmyRow(c, y, w, "訓練", "<color=#e08a3c>訓練中 あと " + tr.turnsLeft + " ターン（" + SurfaceMap.Get(tr.regionId).name + "）</color>", null, false, null);
        }
        else
        {
            string whyD; bool canD = TrainingSystem.CanDrill(id, out whyD);
            y = ArmyRow(c, y, w, "反芻", "素材 " + TrainingSystem.DrillCost(id) + " で +" + TrainingSystem.DrillExp + "exp\n<size=88%><color=#9c95b4>"
                + (canD ? "冒険者が届かなかった階に置いた個体だけ使える" : whyD) + "</color></size>",
                "反芻", canD, () => { if (TrainingSystem.TryDrill(id)) RefreshArmy(); });
        }
        // 眷属化
        var myKin = KinRoster.Of(id); var myLeader = KinRoster.LeaderOfFollower(id);
        if (myKin != null)
            y = ArmyRow(c, y, w, "地上", "<color=#ffd24a>眷属『" + myKin.trueName + "』</color>\n<size=88%><color=#9c95b4>地上の窓で編成・進軍する</color></size>", null, false, null);
        else if (myLeader != null)
            y = ArmyRow(c, y, w, "地上", "<color=#e3a94a>" + myLeader.trueName + " の配下として地上に出ている</color>", null, false, null);
        else
        {
            var reqs = KinRoster.NameRequirements(id);
            bool can = KinRoster.MeetsNameRequirements(id);
            int roll = nameRolls.ContainsKey(id) ? nameRolls[id] : 0;
            string cand = KinRoster.NameCandidate(id, roll);
            var sb = new System.Text.StringBuilder();
            foreach (var q in reqs) sb.Append(q.met ? "<color=#5cc47c>◆" + q.label + "</color> " : "<color=#6f6889>・" + q.label + "</color> ");
            y = ArmyRow(c, y, w, "眷属", (can ? "真名『" + cand + "』を与えて眷属にする" : "眷属にする（真名を与える）")
                + "\n<size=82%>" + sb + "</size>",
                "眷属化 " + KinRoster.NameCost(id) + "DP", can, () =>
                {
                    int rr = nameRolls.ContainsKey(id) ? nameRolls[id] : 0;
                    if (KinRoster.TryName(id, rr)) { RefreshArmy(); RefreshSurfacePanel(); }
                }, "眷属は配下を率いて地上へ出る。迷宮の隊・ボスには使えなくなる", 76f);
            if (can)
            {
                var rb = PrimaryButton(c, "別の真名 ↻", PANEL2, MUTED, () => { nameRolls[id] = (nameRolls.ContainsKey(id) ? nameRolls[id] : 0) + 1; RefreshArmy(); });
                Place((RectTransform)rb.transform, w - 118, y - 4, 118, 26);
                y += 28f;
            }
        }
        c.sizeDelta = new Vector2(0f, y + 8f);
    }

    private void ArmyEquipTab(RectTransform c, MinionRoster.Individual v, float w)
    {
        int id = v.id; float y = 4f;
        int fcap = EquipmentCatalog.ResearchGradeCap();
        if (DemonLord.Instance != null) fcap = Mathf.Min(EquipmentCatalog.MaxGrade, fcap + DemonLord.Instance.ForgeGradeBonus);
        foreach (var slot in new[] { EquipmentCatalog.Slot.Weapon, EquipmentCatalog.Slot.Armor })
        {
            int g = MinionRoster.GradeOf(id, slot);
            bool isW = slot == EquipmentCatalog.Slot.Weapon;
            int wt = MinionRoster.WeaponTypeOf(id);
            string head = isW ? EquipmentCatalog.WeaponTypeName(wt) : "防具";
            string grade = "<color=" + EquipmentCatalog.ColorHex(g) + "><b>" + EquipmentCatalog.Name(g) + "</b></color>";
            string btn; bool en; string tip = null;
            if (g >= EquipmentCatalog.MaxGrade) { btn = "最高等級"; en = false; }
            else if (g >= fcap) { btn = "上限"; en = false; tip = "次の等級『" + EquipmentCatalog.Name(g + 1) + "』を開くには\n" + EquipmentCatalog.CapExplain(); }
            else
            {
                int cost = EquipmentCatalog.ForgeCost(g + 1), fmat = EquipmentCatalog.ForgeMaterial(g + 1);
                btn = "強化 " + cost + (fmat > 0 ? "+素" + fmat : ""); en = true;
                tip = EquipmentCatalog.Name(g) + " → " + EquipmentCatalog.Name(g + 1) + "　" + EquipmentCatalog.StepText(g, slot);
            }
            var sl = slot;
            y = ArmyRow(c, y, w, head, grade + (g >= 0 ? "" : "　<size=85%><color=#6f6889>素手</color></size>")
                + "\n<size=85%><color=#9c95b4>" + (g < EquipmentCatalog.MaxGrade && g < fcap ? "次は『" + EquipmentCatalog.Name(g + 1) + "』（触れると伸び幅）" : "") + "</color></size>",
                btn, en, () => { if (MinionRoster.TryForge(id, sl)) RefreshArmy(); }, tip);
            float bx = 60f;
            if (g >= 0)
            {
                var ub = PrimaryButton(c, "外す", PANEL2, MUTED, () => { MinionRoster.Unequip(id, sl); RefreshArmy(); });
                Place((RectTransform)ub.transform, bx, y - 4, 70, 24); bx += 76f;
            }
            if (isW)
            {
                int nextT = (wt + 1) % EquipmentCatalog.WeaponTypeCount;
                var d = EquipmentCatalog.WType(wt);
                var tb = PrimaryButton(c, "種別→" + EquipmentCatalog.WeaponTypeName(nextT), PANEL2, TEAL, () => { MinionRoster.CycleWeaponType(id); RefreshArmy(); });
                Place((RectTransform)tb.transform, bx, y - 4, 120, 24);
                AddTooltip(tb.gameObject, EquipmentCatalog.WeaponTypeName(wt) + "：" + d.note
                    + string.Format("（攻×{0:0.00} 間隔×{1:0.00} 射程+{2:0.0}）", d.atkMult, d.intervalMult, d.rangeBonus));
            }
            if (g >= 0 || isW) y += 26f;
        }
        int cur = v.accessory;
        y = ArmyRow(c, y, w, "装飾", cur >= 0 ? "<color=" + AccessoryCatalog.ColorHex(cur) + "><b>" + AccessoryCatalog.Name(cur) + "</b></color>\n<size=85%><color=#9c95b4>" + AccessoryCatalog.EffectLine(cur) + "</color></size>"
                                             : "<color=#6f6889>なし</color>\n<size=85%><color=#6f6889>手持ち " + AccessoryInventory.TotalCount + " 個・行商人で買える</color></size>",
            "次の装飾品", AccessoryInventory.TotalCount > 0 || cur >= 0, () => { AccessoryInventory.Equip(id, NextAccessoryFor(cur)); RefreshArmy(); });
        float totalAtk = MinionRoster.EquipAtkMult(id) * MinionRoster.TypeAtkMult(id);
        var sum = Text(c, "<color=#8cb8e6>装備の合計：攻×" + totalAtk.ToString("0.00") + "　硬×" + MinionRoster.EquipHpMult(id).ToString("0.00") + "</color>", 13, MUTED, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(sum.rectTransform, 0, y + 4, w, 20);
        c.sizeDelta = new Vector2(0f, y + 32f);
    }

    private void ArmyRecordTab(RectTransform c, MinionRoster.Individual v, float w)
    {
        int id = v.id;
        string nl = System.Environment.NewLine;
        var sb = new System.Text.StringBuilder();
        sb.Append("<b>撃破</b>　").Append(v.kills).Append("　　<b>武功</b>　").Append(v.deed).Append(nl);
        sb.Append("<color=").Append(MinionRank.ColorOf(v.rank)).Append(">格：").Append(v.rank > 0 ? MinionRank.Name(v.rank) : "無印").Append("</color>").Append(nl);
        sb.Append("<color=#9c95b4>").Append(MinionRank.ProgressText(v)).Append("</color>").Append(nl).Append(nl);
        sb.Append("<b>ボスに就いたとき</b>").Append(nl);
        sb.Append(GoetiaCatalog.TitleOf(id)).Append("　<color=#9c95b4>").Append(GoetiaCatalog.Blessing(GoetiaCatalog.PillarOf(id).rank)).Append("</color>").Append(nl).Append(nl);
        var td = MinionTemperament.Get(v.temper);
        sb.Append("<b>気性</b>　<color=").Append(td.colorHex).Append(">").Append(td.jpName).Append("</color>").Append(nl);
        sb.Append("<color=#9c95b4>").Append(td.desc).Append("</color>");
        var t = Text(c, sb.ToString(), 14, TEXT, TextAlignmentOptions.TopLeft);
        t.enableWordWrapping = true;
        Place(t.rectTransform, 0, 4, w, 400);
        c.sizeDelta = new Vector2(0f, 420f);
    }
}
