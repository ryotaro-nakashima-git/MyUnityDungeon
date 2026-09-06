using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 🔨 **生産タブ**（K-1・アーティファクトの画面02）。
///
/// <para>
/// Civ VII の都市パネルの作りをそのまま持ち込む：
///   ① 上に**拠点の産出行**（6色のチップ）
///   ② **生産／購入** の2タブ
///   ③ **分類ごとの一覧**（建造物／配下／…）。各行に「何が貰えるか」と**所要ターン**
///   ④ 右に**待ち行列**（先頭が建造中・下は順番待ち）
/// ⚠ **購入で買えるのは物だけ。** 研究点も時間も買えない（→ [[civ7-actual-screens]]）。
/// ⚠ **購入は1ターンに1件まで**（レートだけで縛ると後半に全部買えて「順番を選ぶ」遊びが消える）。
/// </para>
///
/// 関連: [[ProductionSystem]] [[DistrictCatalog]] [[LegionRoster]] [[ui-conventions]]。
/// </summary>
public partial class GameUIManager
{
    /// <summary>生産タブで見る拠点。⚠ 選ばれていなければ**生産力がいちばん高い拠点**に寄せる。</summary>
    private int ProdRegion()
    {
        var r = SurfaceMap.Get(prodRegionId);
        if (r != null && r.owned && r.settle != SurfaceMap.Settle.None) return prodRegionId;
        int best = -1, bestP = -1;
        foreach (var g in SurfaceMap.All)
        {
            if (!g.owned || g.settle == SurfaceMap.Settle.None) continue;
            int p = ProductionSystem.ProductionAt(g.id);
            if (p > bestP) { bestP = p; best = g.id; }
        }
        prodRegionId = best;
        return best;
    }

    private void RefreshProductionPanel()
    {
        var c = prodContainer; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = prodW, y = 0f;

        int rid = ProdRegion();
        if (rid < 0)
        {
            var none = Text(c, "<color=#9c95b4>まだ拠点がありません。地上で領域を得て『拠点を築く』と、ここで物を作れるようになります。</color>",
                12f, MUTED, TextAlignmentOptions.TopLeft);
            none.enableWordWrapping = true;
            Place(none.rectTransform, 4, 0, w - 8, 60);
            SetContentHeight(c, 70);
            return;
        }
        var reg = SurfaceMap.Get(rid);

        // ── ① 拠点を選ぶ帯（複数持っていたら押して切り替える）──
        y = BuildProdRegionRow(c, w, y, rid);

        // ── ② その拠点の産出（6色のチップ）──
        y = BuildProdYieldRow(c, w, y, rid);

        // ── ②b 置き場を比べているときの差分（K-2・画面03）──
        y = BuildPlacementDelta(c, w, y, rid);

        // ── ③ 生産／購入 タブ ──
        y = BuildProdTabs(c, w, y);

        // ── ④ 待ち行列（先頭＝建造中）──
        y = BuildProdQueue(c, w, y, rid);

        // ── ⑤ 作れる物の一覧 ──
        y = BuildProdCatalog(c, w, y, rid);

        SetContentHeight(c, y + 12f);
    }

    /// <summary>スクロールの中身の高さを合わせる。⚠ これを忘れると下が切れて押せなくなる。</summary>
    private void SetContentHeight(RectTransform c, float h)
    {
        c.sizeDelta = new Vector2(c.sizeDelta.x, Mathf.Max(h, 10f));
    }

    private float BuildProdRegionRow(RectTransform c, float w, float y, int rid)
    {
        var owned = new List<SurfaceMap.Region>();
        foreach (var g in SurfaceMap.All) if (g.owned && g.settle != SurfaceMap.Settle.None) owned.Add(g);

        var head = Text(c, "◆ 拠点", 12f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(head.rectTransform, 4, y, w - 8, 16); y += 20;

        float bx = 4f;
        for (int i = 0; i < owned.Count && i < 12; i++)
        {
            var g = owned[i]; int id = g.id;
            bool sel = id == rid;
            string label = g.name + " <size=85%><color=#9c95b4>" + ProductionSystem.ProductionAt(id) + " 生産</color></size>";
            var b = PrimaryButton(c, label, sel ? CardHiColor() : PANEL2, sel ? GOLD : TEXT,
                () => { prodRegionId = id; RefreshSurfacePanel(); });
            float bw = 132f;
            if (bx + bw > w - 8) { bx = 4f; y += 32f; }
            Place((RectTransform)b.transform, bx, y, bw, 28);
            bx += bw + 6f;
        }
        return y + 36f;
    }

    private Color CardHiColor() { return C("#2c2540"); }

    /// <summary>😊 その拠点が毎ターン生む産出。⚠ 6色は上部バーと同じ意味で使う。</summary>
    private float BuildProdYieldRow(RectTransform c, float w, float y, int rid)
    {
        var reg = SurfaceMap.Get(rid);
        int prod = ProductionSystem.ProductionAt(rid);
        int happy = SettlementSystem.HappyOf(rid);
        int pop = reg != null ? reg.pop : 0;

        var row = Panel(c, "ProdYields", C("#191626"));
        Place(row.rectTransform, 4, y, w - 8, 40); Outline(row, LINE);

        float x = 10f;
        x = YieldPill(row, x, "生産", prod.ToString(), UITheme.Production);
        x = YieldPill(row, x, "人口", pop.ToString(), UITheme.Food);
        x = YieldPill(row, x, "幸福", (happy > 0 ? "+" : "") + happy, happy < 0 ? CRIMSON : UITheme.Happy);
        x = YieldPill(row, x, "魔力", (res != null ? res.DungeonPoints : 0).ToString(), UITheme.DP);
        x = YieldPill(row, x, "購入", ProductionSystem.PurchasesLeft + "/" + ProductionSystem.PurchasesPerTurn,
            ProductionSystem.PurchasesLeft > 0 ? UITheme.Focus : FAINT);
        return y + 48f;
    }

    private float YieldPill(Image parent, float x, string label, string value, Color col)
    {
        var p = Panel(parent.rectTransform, "Pill_" + label, C("#12101b"));
        Place(p.rectTransform, x, 7, 84, 26); Outline(p, LINE);
        var sw = Panel(p.rectTransform, "sw", col);
        Place(sw.rectTransform, 0, 0, 3, 26);
        var l = Text(p.rectTransform, label, 9.5f, FAINT, TextAlignmentOptions.Left);
        Place(l.rectTransform, 8, 2, 40, 11);
        var v = Text(p.rectTransform, value, 13f, col, TextAlignmentOptions.Left, FontStyles.Bold);
        v.enableWordWrapping = false;
        Place(v.rectTransform, 8, 11, 70, 15);
        return x + 90f;
    }

    /// <summary>
    /// 🔍 **画面03：選ぶ前に、選んだ結果が数字で見える**（K-2）。
    ///
    /// 比べている施設を、いま見ている拠点に建てたら**毎ターンの産出がいくつ増えるか**を出す。
    /// ⚠ 数字は `DistrictCatalog.PreviewYieldAt` から取る ―― 実際に加算する式と同じ物を使う
    ///   （別々に書くと「見せた差分」と「実際の増分」がずれて嘘になる）。
    /// ⚠ 盤側は `SurfaceView.placementPreview` が全タイルに隣接ボーナスを出している。
    /// </summary>
    private float BuildPlacementDelta(RectTransform c, float w, float y, int rid)
    {
        // 盤の下敷きを更新（比べていなければ消す）
        if (surfaceView != null)
        {
            if (prodPreviewDistrict < 0) surfaceView.placementPreview = null;
            else
            {
                var map = new Dictionary<int, int>();
                foreach (var g in SurfaceMap.All)
                {
                    if (!g.owned || g.isOcean) continue;
                    bool asQ; string w2;
                    if (!DistrictCatalog.CanBuild(g.id, out asQ, out w2)) continue;
                    if (DistrictCatalog.Get(prodPreviewDistrict).id == "harbor" && !DistrictCatalog.IsCoastal(g.id)) continue;
                    map[g.id] = DistrictCatalog.PreviewAdjacencyAt(prodPreviewDistrict, g.id);
                }
                surfaceView.placementPreview = map;
            }
            surfaceView.Redraw();
        }
        if (prodPreviewDistrict < 0) return y;

        var d = DistrictCatalog.Get(prodPreviewDistrict);
        string detail;
        int adj = DistrictCatalog.Adjacency(prodPreviewDistrict, rid, out detail);
        var pv = DistrictCatalog.PreviewYieldAt(prodPreviewDistrict, rid);

        var box = Panel(c, "PlaceDelta", C("#1b1826"));
        Place(box.rectTransform, 4, y, w - 8, 84); Outline(box, C("#7d6438"));

        var h = Text(box.rectTransform, "<color=" + d.colorHex + ">" + d.jpName + "</color> をここに建てると",
            12.5f, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(h.rectTransform, 12, 6, w - 40, 18);

        var sub = Text(box.rectTransform, "<color=#9c95b4>基礎1＋隣接" + adj + "　" + detail + "</color>",
            10.5f, MUTED, TextAlignmentOptions.Left);
        sub.enableWordWrapping = false;
        Place(sub.rectTransform, 12, 24, w - 40, 15);

        float x = 12f;
        x = DeltaPill(box, x, "生産", pv.prod, UITheme.Production);
        x = DeltaPill(box, x, "DP", pv.dp, UITheme.DP);
        x = DeltaPill(box, x, "素材", pv.mat, UITheme.Material);
        x = DeltaPill(box, x, "研究", pv.rp, UITheme.Research);
        x = DeltaPill(box, x, "食料", pv.food, UITheme.Food);
        x = DeltaPill(box, x, "防衛", pv.def, UITheme.Grade);
        x = DeltaPill(box, x, "威名", pv.inf, UITheme.Influence);

        var tip = Text(box.rectTransform, "<color=#6f6889>盤の明るいタイルほど良い置き場。数字はそこに建てたときの隣接ボーナス。</color>",
            10f, FAINT, TextAlignmentOptions.Left);
        Place(tip.rectTransform, 12, 66, w - 40, 14);
        return y + 92f;
    }

    /// <summary>差分の小さな札。⚠ 0 のものは薄く出す（消すと「何が増えないか」が読めない）。</summary>
    private float DeltaPill(Image parent, float x, string label, int v, Color col)
    {
        var p = Panel(parent.rectTransform, "D_" + label, C("#12101b"));
        Place(p.rectTransform, x, 40, 62, 24); Outline(p, LINE);
        var sw = Panel(p.rectTransform, "sw", v > 0 ? col : C("#332e49"));
        Place(sw.rectTransform, 0, 0, 3, 24);
        var l = Text(p.rectTransform, label, 9f, FAINT, TextAlignmentOptions.Left);
        Place(l.rectTransform, 7, 1, 40, 11);
        var t = Text(p.rectTransform, v > 0 ? "+" + v : "―", 12f, v > 0 ? UITheme.Food : FAINT,
            TextAlignmentOptions.Left, FontStyles.Bold);
        Place(t.rectTransform, 7, 10, 50, 14);
        return x + 66f;
    }

    private float BuildProdTabs(RectTransform c, float w, float y)
    {
        string[] names = { "生産", "購入" };
        float tw = 110f;
        for (int i = 0; i < 2; i++)
        {
            int ti = i;
            bool sel = prodTab == i;
            var b = PrimaryButton(c, names[i], sel ? CardHiColor() : PANEL2, sel ? GOLD : MUTED,
                () => { prodTab = ti; RefreshSurfacePanel(); });
            Place((RectTransform)b.transform, 4 + i * (tw + 6), y, tw, 30);
        }
        var hint = Text(c, prodTab == 0
                ? "<color=#6f6889>選ぶと待ち行列に積まれ、毎ターン生産力ぶん進む。</color>"
                : "<color=#6f6889>残りを魔力点で埋めて即座に完成させる。<b>1ターンに1件まで。</b>物しか買えない。</color>",
            11f, MUTED, TextAlignmentOptions.Left);
        Place(hint.rectTransform, 4 + 2 * (tw + 6) + 8, y + 6, w - (2 * (tw + 6)) - 24, 18);
        return y + 38f;
    }

    /// <summary>④ 待ち行列。先頭が建造中。⚠ 上下ボタンで順番を入れ替えられる（Civ の並べ替え）。</summary>
    private float BuildProdQueue(RectTransform c, float w, float y, int rid)
    {
        var q = ProductionSystem.QueueAt(rid);
        var t = Text(c, "◆ 待ち行列 <size=85%><color=#9c95b4>" + q.Count + "/6</color></size>",
            12f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(t.rectTransform, 4, y, w - 8, 16); y += 20;

        if (q.Count == 0)
        {
            var e = Text(c, "<color=#6f6889>まだ何も積んでいません。下の一覧から選ぶとここに積まれます。</color>",
                11.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(e.rectTransform, 4, y, w - 8, 18);
            return y + 26f;
        }

        for (int i = 0; i < q.Count; i++)
        {
            var it = q[i];
            bool building = i == 0;
            int need = ProductionSystem.CostOf(it.kind, it.index);
            int left = ProductionSystem.TurnsLeft(it);
            var card = Panel(c, "Q" + i, building ? C("#1b1826") : C("#151220"));
            Place(card.rectTransform, 4, y, w - 8, building ? 54 : 40);
            Outline(card, building ? C("#7d6438") : LINE);

            var lbl = Text(card.rectTransform, building ? "建造中" : "順番待ち " + i,
                9.5f, building ? GOLD : FAINT, TextAlignmentOptions.Left);
            Place(lbl.rectTransform, 10, 4, 90, 12);

            var nm = Text(card.rectTransform, ProductionSystem.NameOf(it), 13f, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
            nm.enableWordWrapping = false;
            Place(nm.rectTransform, 10, 16, w - 240, 18);

            var tn = Text(card.rectTransform,
                it.progress + "/" + need + "　<color=#9c95b4>" + (left < 0 ? "―" : "あと" + left + "T") + "</color>",
                11.5f, MUTED, TextAlignmentOptions.Right);
            tn.enableWordWrapping = false;
            Place(tn.rectTransform, w - 236, 16, 110, 18);

            if (building)
            {
                var bar = Panel(card.rectTransform, "bar", C("#12101b"));
                Place(bar.rectTransform, 10, 40, w - 30, 4);
                var fill = Panel(bar.rectTransform, "fill", UITheme.Production);
                Place(fill.rectTransform, 0, 0, (w - 30) * Mathf.Clamp01(it.progress / (float)Mathf.Max(1, need)), 4);
            }

            // 購入／並べ替え／取りやめ
            float bx = w - 122;
            if (prodTab == 1)
            {
                string why; bool can = ProductionSystem.CanPurchase(it, out why);
                int cost = ProductionSystem.PurchaseCost(it);
                var pb = PrimaryButton(card.rectTransform, cost + "DP", can ? BLOOD : PANEL2, can ? TEXT : FAINT,
                    () => { if (ProductionSystem.TryPurchase(it)) { SoundSystem.Play(SoundSystem.Sfx.Confirm); RefreshSurfacePanel(); } });
                Place((RectTransform)pb.transform, bx, 12, 78, 24);
                AddTooltip(pb.gameObject, can ? "残り " + ProductionSystem.Remaining(it) + " 生産力を " + cost + "DP で埋める" : why);
                bx += 84;
            }
            else
            {
                var up = PrimaryButton(card.rectTransform, "▲", PANEL2, i > 0 ? TEXT : FAINT,
                    () => { if (ProductionSystem.MoveUp(it)) RefreshSurfacePanel(); });
                Place((RectTransform)up.transform, bx, 12, 26, 24);
                var dn = PrimaryButton(card.rectTransform, "▼", PANEL2, i < q.Count - 1 ? TEXT : FAINT,
                    () => { if (ProductionSystem.MoveDown(it)) RefreshSurfacePanel(); });
                Place((RectTransform)dn.transform, bx + 30, 12, 26, 24);
                bx += 62;
            }
            var x = PrimaryButton(card.rectTransform, "×", PANEL2, MUTED,
                () => { if (ProductionSystem.Cancel(it)) RefreshSurfacePanel(); });
            Place((RectTransform)x.transform, w - 40, 12, 26, 24);
            AddTooltip(x.gameObject, "取りやめる（積んだ生産力は戻らない）");

            y += (building ? 58 : 44);
        }
        return y + 8f;
    }

    /// <summary>⑤ 作れる物。⚠ Civ と同じで**分類ごと**に並べ、行に「何が貰えるか」と所要ターンを出す。</summary>
    private float BuildProdCatalog(RectTransform c, float w, float y, int rid)
    {
        y = ProdGroup(c, w, y, "建造物");
        int prod = Mathf.Max(1, ProductionSystem.ProductionAt(rid));
        for (int i = 0; i < DistrictCatalog.Count; i++)
        {
            if (!DistrictCatalog.IsUnlocked(i)) continue;
            int di = i;
            var d = DistrictCatalog.Get(i);
            int cost = ProductionSystem.CostOf(ProductionSystem.Kind.District, i);
            string why; bool can = ProductionSystem.CanEnqueue(rid, ProductionSystem.Kind.District, i, out why);
            bool previewing = prodPreviewDistrict == i;
            y = ProdRow(c, w, y, (previewing ? "<color=#e3a94a>◈ </color>" : "") + d.jpName, d.desc,
                cost, Mathf.CeilToInt(cost / (float)prod), can, why,
                C(d.colorHex), () => { if (ProductionSystem.TryEnqueue(rid, ProductionSystem.Kind.District, di)) RefreshSurfacePanel(); },
                // 🔍 K-2：押すと盤の全タイルに「そこに建てたときの隣接ボーナス」が出る
                previewing ? "やめる" : "盤で比べる",
                () => { prodPreviewDistrict = previewing ? -1 : di; RefreshSurfacePanel(); });
        }

        y = ProdGroup(c, w, y, "大工事（迷宮に効く）");
        for (int i = 0; i < ProductionSystem.Works.Count; i++)
        {
            int wi = i;
            int cost = ProductionSystem.CostOf(ProductionSystem.Kind.Work, i);
            string why; bool can = ProductionSystem.CanEnqueue(rid, ProductionSystem.Kind.Work, i, out why);
            string extra = "";
            if (i != ProductionSystem.Works.NewFloor)
            {
                int k = i == ProductionSystem.Works.DrillGround
                    ? (int)GreatWorkCatalog.Kind.DrillGround : (int)GreatWorkCatalog.Kind.GreatNest;
                int v = ProductionSystem.GreatWorkVouchers(k);
                if (v > 0) extra = "　<color=#e3a94a>許可 " + v + "枚</color>";
            }
            y = ProdRow(c, w, y, ProductionSystem.Works.Name(i) + extra, ProductionSystem.Works.Desc(i),
                cost, Mathf.CeilToInt(cost / (float)prod), can, why, UITheme.Grade,
                () => { if (ProductionSystem.TryEnqueue(rid, ProductionSystem.Kind.Work, wi)) RefreshSurfacePanel(); });
        }

        y = ProdGroup(c, w, y, "プロジェクト");
        for (int i = 0; i < ProductionSystem.Projects.Count; i++)
        {
            int pi = i;
            int cost = ProductionSystem.CostOf(ProductionSystem.Kind.Project, i);
            string why; bool can = ProductionSystem.CanEnqueue(rid, ProductionSystem.Kind.Project, i, out why);
            y = ProdRow(c, w, y, ProductionSystem.Projects.Name(i), ProductionSystem.Projects.Desc(i),
                cost, Mathf.CeilToInt(cost / (float)prod), can, why, UITheme.Influence,
                () => { if (ProductionSystem.TryEnqueue(rid, ProductionSystem.Kind.Project, pi)) RefreshSurfacePanel(); });
        }

        y = ProdGroup(c, w, y, "配下（軍団）");
        int shown = 0;
        for (int i = 0; i < MinionCatalog.Count && shown < 14; i++)
        {
            if (!MinionEvolution.IsUnlocked(i)) continue;
            int ci = i; shown++;
            var m = MinionCatalog.Get(i);
            int cost = ProductionSystem.CostOf(ProductionSystem.Kind.Legion, i);
            string why; bool can = ProductionSystem.CanEnqueue(rid, ProductionSystem.Kind.Legion, i, out why);
            y = ProdRow(c, w, y, m.jpName + "軍団", "地上を進む部隊。武勲で昇進する。", cost,
                Mathf.CeilToInt(cost / (float)prod), can, why, UITheme.Blood,
                () => { if (ProductionSystem.TryEnqueue(rid, ProductionSystem.Kind.Legion, ci)) RefreshSurfacePanel(); });
        }
        return y;
    }

    private float ProdGroup(RectTransform c, float w, float y, string name)
    {
        var g = Panel(c, "G_" + name, C("#221d33"));
        Place(g.rectTransform, 4, y, w - 8, 22);
        var t = Text(g.rectTransform, name, 10.5f, GOLD, TextAlignmentOptions.Left);
        Place(t.rectTransform, 10, 0, w - 30, 22);
        return y + 26f;
    }

    private float ProdRow(RectTransform c, float w, float y, string name, string desc,
        int cost, int turns, bool can, string why, Color accent, UnityEngine.Events.UnityAction onClick,
        string extraLabel = null, UnityEngine.Events.UnityAction onExtra = null)
    {
        var card = Panel(c, "R_" + name, C("#151220"));
        Place(card.rectTransform, 4, y, w - 8, 44); Outline(card, LINE);
        var sw = Panel(card.rectTransform, "sw", can ? accent : C("#332e49"));
        Place(sw.rectTransform, 0, 0, 3, 44);

        var nm = Text(card.rectTransform, name, 13f, can ? TEXT : FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        nm.enableWordWrapping = false;
        Place(nm.rectTransform, 12, 4, w - 180, 17);
        var ds = Text(card.rectTransform, can ? desc : "<color=#6f6889>" + why + "</color>", 10.5f, MUTED, TextAlignmentOptions.Left);
        ds.enableWordWrapping = false;
        Place(ds.rectTransform, 12, 22, w - 180, 15);

        var tn = Text(card.rectTransform, "<b>" + turns + "</b>T <size=85%><color=#9c95b4>" + cost + " 生産</color></size>",
            12f, can ? UITheme.Production : FAINT, TextAlignmentOptions.Right);
        tn.enableWordWrapping = false;
        Place(tn.rectTransform, w - 168, 13, 86, 18);

        var b = PrimaryButton(card.rectTransform, "積む", can ? PANEL2 : C("#191626"), can ? TEXT : FAINT, onClick);
        Place((RectTransform)b.transform, w - 74, 10, 62, 24);
        AddTooltip(b.gameObject, can ? name + " を待ち行列に積む（" + cost + " 生産力・約" + turns + "ターン）" : why);
        if (!string.IsNullOrEmpty(extraLabel) && onExtra != null)
        {
            var eb = PrimaryButton(card.rectTransform, extraLabel, PANEL2, GOLD, onExtra);
            Place((RectTransform)eb.transform, w - 160, 10, 82, 24);
            AddTooltip(eb.gameObject, "盤の全タイルに『そこに建てたときの隣接ボーナス』を出す。濃いタイルほど良い置き場。");
        }
        return y + 48f;
    }
}
