using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ⚔️ 遠征の編成と進行の窓（④-c UI）。
///
/// <b>この窓がやることは1つだけ ―― 「どっちに置くか」を、置いた結果と一緒に見せる。</b>
/// 連れて行ける数に上限は無いので、数の管理は要らない。要るのは
/// <b>「残る守りが何体になったか」を常に出すこと</b>。連れて行く側だけ数えても判断できない。
///
/// ⚠ <b>専用の窓にする</b>（図鑑の『個体』タブに間借りさせない）。あちらは装備と進化の場所で、
///   目的（守りを組む）が正反対になる。混ぜると「どっちの操作をしているのか」が分からなくなる。
/// ⚠ <b>勝率は出さない</b>（→ [[readiness-and-trade]]）。出すのは相手の事実だけ。
/// ⚠ <b>突入したあとは組み替えられない。</b>「連れて行った個体は守りに立てない」という代償は
///   波をまたいで効くから重い。波ごとに出し入れできると代償が消える。
///
/// 関連: [[Expedition]] [[NestSystem]] [[RaidBoard]] [[MinionRank]]。
/// </summary>
public partial class GameUIManager
{
    /// <summary>遠征の窓を開いているか（地上パネルの上に重ねる）。</summary>
    private bool expeditionOpen;
    private GameObject expeditionPanel;
    private RectTransform expeditionBody;

    /// <summary>外から開閉する（地上の巣カードのボタンから）。</summary>
    public void OpenExpeditionWindow() { expeditionOpen = true; RefreshExpeditionWindow(); }
    public void CloseExpeditionWindow() { expeditionOpen = false; RefreshExpeditionWindow(); }

    /// <summary>他の窓と同じ作り方（`BuildPrisonPanel` などと揃える）。⚠ 起動時に1度だけ組む。</summary>
    private void BuildExpeditionPanel(RectTransform root)
    {
        var p = Panel(root, "ExpeditionWindow", PANEL);
        expeditionPanel = p.gameObject;
        Anchor(p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        p.rectTransform.sizeDelta = new Vector2(760, 560);
        p.rectTransform.anchoredPosition = Vector2.zero;
        Outline(p, LINE2);

        var body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
        body.SetParent(p.rectTransform, false);
        Anchor(body, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1));
        body.sizeDelta = new Vector2(760, 560);
        body.anchoredPosition = Vector2.zero;
        expeditionBody = body;
        expeditionPanel.SetActive(false);
    }

    /// <summary>
    /// 窓の中身。⚠ 毎回作り直す（人数が動くので差分更新にすると必ずずれる）。
    /// </summary>
    private void RefreshExpeditionWindow()
    {
        bool show = expeditionOpen && Expedition.Active;
        if (!show)
        {
            if (expeditionPanel != null) expeditionPanel.SetActive(false);
            return;
        }
        if (expeditionPanel == null) return;
        expeditionPanel.SetActive(true);

        var c = expeditionBody;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }

        var party = Expedition.Current;
        var nest = NestSystem.At(party.nestIndex);
        var sn = nest != null ? nest.snap : null;
        float W = 760f, y = 0f;

        // ── 見出し ──
        var bar = Panel(c, "Bar", HUD_BG); Place(bar.rectTransform, 0, 0, W, 40); Outline(bar, LINE);
        var ttl = Text(bar.rectTransform, "⚔ 遠征の編成", 16, TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Place(ttl.rectTransform, 16, 10, 200, 22);
        if (sn != null)
        {
            var sub = Text(bar.rectTransform, "<color=" + sn.KindColor + ">" + sn.name + "</color> <size=88%><color=#9c95b4>"
                + sn.KindName + "・" + sn.FloorCount + "層・難度 " + AdventurerAI.RankLetter(sn.tier) + "</color></size>",
                13, MUTED, TextAlignmentOptions.MidlineLeft);
            Place(sub.rectTransform, 190, 11, W - 260, 20);
        }
        var xb = PrimaryButton(bar, "閉じる", PANEL2, MUTED, () => { expeditionOpen = false; RefreshExpeditionWindow(); });
        Place((RectTransform)xb.transform, W - 86, 7, 72, 26);
        y = 48;

        if (party.phase == Expedition.Phase.Descending) { BuildExpeditionProgress(c, W, ref y, party, sn); return; }
        BuildExpeditionForming(c, W, ref y, party, sn);
    }

    // ══════════ 編成中 ══════════
    private void BuildExpeditionForming(RectTransform c, float W, ref float y, Expedition.Party party, DungeonSnapshot sn)
    {
        var cands = Expedition.Candidates();
        float colW = (W - 36) / 2f;
        float listH = 340f;

        // 左＝迷宮に残す／右＝連れて行く
        var left = Panel(c, "Stay", CARD); Place(left.rectTransform, 12, y, colW, listH + 30); Outline(left, C("#7a2630"));
        var lh = Text(left.rectTransform, "迷宮に残す", 14, C("#e05a5a"), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Place(lh.rectTransform, 10, 6, colW - 90, 20);
        var ln = Text(left.rectTransform, cands.Count + " 体", 13, C("#e05a5a"), TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        Place(ln.rectTransform, colW - 84, 6, 74, 20);

        var right = Panel(c, "Go", CARD); Place(right.rectTransform, 24 + colW, y, colW, listH + 30); Outline(right, GOLD);
        var rh = Text(right.rectTransform, "連れて行く", 14, GOLD, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Place(rh.rectTransform, 10, 6, colW - 110, 20);
        var rn = Text(right.rectTransform, (party.members.Count + 1) + " 体・上限なし", 12, GOLD, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        Place(rn.rectTransform, colW - 134, 6, 124, 20);

        // ⚠ `MakeVScroll` は **Content の RectTransform をそのまま返す**（out ではない）。
        //   幅は親側で決まるので、行の幅は colW から引いて合わせる（→ [[kin-surface-4x]] の幅の罠）。
        var lc = MakeVScroll(left, 6, 30, colW - 12, listH - 6);
        var rc = MakeVScroll(right, 6, 30, colW - 12, listH - 6);

        float ry = 0f;
        // 率いる眷属は必ず先頭・外せない
        var lead = MinionRoster.Get(party.leaderId);
        var kin = KinRoster.Of(party.leaderId);
        if (lead != null)
        {
            var row = Panel(rc, "Lead", SEL); Place(row.rectTransform, 0, ry, colW - 30, 28); Outline(row, GOLD);
            var t = Text(row.rectTransform, "『" + (kin != null ? kin.trueName : "?") + "』<color="
                + MinionRank.ColorOf(lead.rank) + ">" + MinionRank.DisplayName(lead) + "</color>",
                12.5f, GOLD, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            Place(t.rectTransform, 8, 4, colW - 90, 20);
            var lv = Text(row.rectTransform, "Lv" + lead.level + "　率", 11.5f, MUTED, TextAlignmentOptions.MidlineRight);
            Place(lv.rectTransform, colW - 100, 4, 66, 20);
            ry += 30;
        }
        for (int i = 0; i < party.members.Count; i++)
        {
            int id = party.members[i];
            ry += AddExpeditionRow(rc, colW, ry, id, false);
        }
        rc.sizeDelta = new Vector2(0, Mathf.Max(listH, ry + 8));

        float ly = 0f;
        for (int i = 0; i < cands.Count; i++) ly += AddExpeditionRow(lc, colW, ly, cands[i], true);
        if (cands.Count == 0)
        {
            var e = Text(lc, "<color=#6f6889>迷宮に残る個体がいません。\nこの波は守りが空になります。</color>",
                12, FAINT, TextAlignmentOptions.Top);
            Place(e.rectTransform, 8, 20, colW - 40, 44); ly = 70;
        }
        lc.sizeDelta = new Vector2(0, Mathf.Max(listH, ly + 8));
        y += listH + 40;

        // ⚠ 残る守りを常に出す。**この窓の要**（連れて行く側だけ数えても判断できない）
        int stayN = cands.Count;
        var warn = Panel(c, "Warn", stayN == 0 ? C("#2a1418") : CARD);
        Place(warn.rectTransform, 12, y, W - 24, 46); Outline(warn, stayN == 0 ? C("#7a2630") : LINE);
        var wt = Text(warn.rectTransform,
            "連れて行く <b>" + (party.members.Count + 1) + "体</b> は、遠征のあいだ<b>迷宮の守りに立てない</b>。"
            + "　残る守り <color=" + (stayN == 0 ? "#ff9a9a" : "#9c95b4") + "><b>" + stayN + "体</b></color>"
            + (stayN == 0 ? "　― <color=#ff9a9a>この波、迷宮は無防備になる</color>" : ""),
            12.5f, TEXT, TextAlignmentOptions.MidlineLeft);
        Place(wt.rectTransform, 12, 6, W - 48, 34);
        y += 54;

        // 突入 / 取り消す
        bool canGo = party.members.Count > 0;
        var gb = PrimaryButton(c, "突入する", canGo ? BLOOD : PANEL, canGo ? C("#f0d9a0") : C("#4a4560"),
            () =>
            {
                var tm = DungeonTurnManager.Instance;
                if (Expedition.Launch(tm != null ? tm.CurrentTurn : 1)) RefreshExpeditionWindow();
            }, canGo);   // ⚠ red=true で主要アクションの赤枠になる（タイトルと同じ作法）
        Place((RectTransform)gb.transform, 12, y, 160, 32);
        var cb = PrimaryButton(c, "宣言を取り消す", PANEL2, MUTED, () => { Expedition.Cancel(); expeditionOpen = false; RefreshExpeditionWindow(); RefreshSurfacePanel(); });
        Place((RectTransform)cb.transform, 180, y, 150, 32);
        // ⚠ 高さ20pxに2行は入らない（下の行が切れる → [[ui-conventions]]）。縮められるようにする。
        var hint = Text(c, "<color=#6f6889>突入すると次の波から1層ずつ降りる。降りている間は編成を変えられない。</color>",
            11f, FAINT, TextAlignmentOptions.MidlineLeft);
        hint.enableAutoSizing = true; hint.fontSizeMin = 9f; hint.fontSizeMax = 11f;
        Place(hint.rectTransform, 340, y + 4, W - 356, 26);
    }

    /// <summary>1行。⚠ 押すと反対側へ移る（「先に隊から外してください」と突き返さない）。</summary>
    private float AddExpeditionRow(RectTransform parent, float colW, float y, int individualId, bool toGo)
    {
        var v = MinionRoster.Get(individualId);
        if (v == null) return 0f;
        var d = MinionCatalog.Get(v.catalogIndex);
        var row = Panel(parent, "U" + individualId, PANEL2);
        Place(row.rectTransform, 0, y, colW - 30, 28); Outline(row, LINE);

        string nm = v.rank > 0
            ? "<color=" + MinionRank.ColorOf(v.rank) + ">" + MinionRank.Name(v.rank) + "・</color>" + d.jpName
            : d.jpName;
        var t = Text(row.rectTransform, nm, 12.5f, TEXT, TextAlignmentOptions.MidlineLeft);
        t.enableAutoSizing = true; t.fontSizeMin = 9.5f; t.fontSizeMax = 12.5f;
        Place(t.rectTransform, 8, 4, colW - 116, 20);
        var lv = Text(row.rectTransform, "Lv" + v.level, 11.5f, MUTED, TextAlignmentOptions.MidlineRight);
        Place(lv.rectTransform, colW - 104, 4, 48, 20);
        var mv = Text(row.rectTransform, toGo ? "→" : "←", 14, toGo ? GOLD : C("#e05a5a"), TextAlignmentOptions.Center, FontStyles.Bold);
        Place(mv.rectTransform, colW - 52, 4, 20, 20);

        var btn = row.gameObject.AddComponent<Button>(); btn.targetGraphic = row;
        int id = individualId; bool go = toGo;
        btn.onClick.AddListener(() =>
        {
            if (go) Expedition.AddMember(id); else Expedition.RemoveMember(id);
            RefreshExpeditionWindow();
        });
        string nl = System.Environment.NewLine;
        AddTooltip(row.gameObject, MinionRank.DisplayName(v) + "　Lv" + v.level + nl
            + MinionCatalog.RoleName(d.role) + "／" + MinionTemperament.Name(v.temper) + nl
            + "<color=#9c95b4>" + (go ? "押すと連れて行く（隊やボスからは自動で外れる）" : "押すと迷宮に残す") + "</color>");
        return 30f;
    }

    // ══════════ 降りている最中 ══════════
    private void BuildExpeditionProgress(RectTransform c, float W, ref float y, Expedition.Party party, DungeonSnapshot sn)
    {
        int floors = sn != null ? sn.FloorCount : 1;

        var info = Text(c, "連れて行った <color=#e3a94a><b>" + (party.members.Count + 1) + "体</b></color>"
            + "　失った <color=#e05a5a><b>" + party.lost + "体</b></color>"
            + (party.stalled > 0 ? "　<color=#e08a3c>足踏み " + party.stalled + "/" + Expedition.MaxStall + "波</color>" : ""),
            13.5f, TEXT, TextAlignmentOptions.MidlineLeft);
        Place(info.rectTransform, 14, y, W - 28, 22); y += 30;

        // 層の帯
        float fw = (W - 24 - (floors - 1) * 6f) / floors;
        for (int i = 0; i < floors; i++)
        {
            bool done = i < party.floor, now = i == party.floor;
            var b = Panel(c, "F" + i, done ? C("#16241b") : now ? C("#2a1418") : PANEL2);
            Place(b.rectTransform, 12 + i * (fw + 6f), y, fw, 34);
            Outline(b, done ? C("#2f5c3f") : now ? CRIMSON : LINE);
            var t = Text(b.rectTransform, (i + 1) + "層 <size=88%>" + (done ? "抜けた" : now ? "交戦中" : "―") + "</size>",
                12, done ? C("#8fe0a8") : now ? C("#ff9a9a") : FAINT, TextAlignmentOptions.Center,
                now ? FontStyles.Bold : FontStyles.Normal);
            Place(t.rectTransform, 4, 7, fw - 8, 20);
        }
        y += 44;

        int guards = RaidBoard.GuardsAlive(party.floor);
        int mine = RaidBoard.RaidersAlive(party.floor);
        var live = Text(c, "いま " + (party.floor + 1) + "層　守り <color=#e05a5a><b>" + guards + "</b></color>"
            + "　こちら <color=#5cc47c><b>" + mine + "</b></color>"
            + "<size=88%><color=#6f6889>　（最深部に着けばその階を抜ける）</color></size>",
            13, MUTED, TextAlignmentOptions.MidlineLeft);
        Place(live.rectTransform, 14, y, W - 28, 22); y += 32;

        var vb = PrimaryButton(c, RaidBoard.IsViewing ? "迷宮へ戻る" : "遠征先を覗く", PANEL2, GOLD,
            () =>
            {
                if (RaidBoard.IsViewing) RaidBoard.StopViewing();
                else RaidBoard.Show(Expedition.Current.floor);
                RefreshExpeditionWindow();
            });
        Place((RectTransform)vb.transform, 12, y, 170, 32);
        var rb = PrimaryButton(c, "引き上げる", PANEL2, C("#e08a3c"),
            () => { Expedition.Retreat("引き上げを命じた"); expeditionOpen = false; RefreshExpeditionWindow(); RefreshSurfacePanel(); });
        Place((RectTransform)rb.transform, 190, y, 150, 32);
        var note = Text(c, "<color=#6f6889>引き上げても個体は失わない（傷ついて帰る）。失うのは倒れたときだけ。</color>",
            11.5f, FAINT, TextAlignmentOptions.MidlineLeft);
        Place(note.rectTransform, 350, y + 6, W - 366, 20);
        y += 42;

        var hint2 = Text(c, "<color=#9c95b4>遠征は<b>波と同じ時間</b>で進む。こちらが攻められているあいだに、1つの波で1層降りる。"
            + "覗いているあいだも、置く・号令・権能は<b>こちらの迷宮</b>に効く。</color>",
            12, MUTED, TextAlignmentOptions.TopLeft);
        Place(hint2.rectTransform, 14, y, W - 28, 44);
    }
}
