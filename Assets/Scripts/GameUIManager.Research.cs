using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 研究ツリーのパネル（Civ風の段組みと接続線）。
/// <para>`GameUIManager` の partial。フィールドの本体は GameUIManager.cs 側にある。</para>
/// </summary>
public partial class GameUIManager
{

    // ---------- 研究ツリー（全画面・分野バンド＋前提を線で接続／Civ風） ----------
    private void BuildResearchPanel(RectTransform root)
    {
        var panel = Panel(root, "ResearchPanel", PANEL);
        researchPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(FS_W, FS_H);
        panel.rectTransform.anchoredPosition = new Vector2(0, 0);
        Outline(panel, LINE2); SkinPanel(panel);

        float pad = 26f;
        var title = Text(panel, "研究ツリー（前提を線で接続／研究済みのノードは<color=#ffd24a>習熟</color>で二段目に進む）", 17, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 16, FS_W - 560, 24);
        researchRpText = Text(panel, "", 14, C("#8cb8e6"), TextAlignmentOptions.Right, FontStyles.Bold);
        Place(researchRpText.rectTransform, FS_W - pad - 480, 16, 440, 24);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => researchPanel.SetActive(false));
        Place((RectTransform)close.transform, FS_W - pad - 32, 14, 32, 30);

        researchContentW = FS_W - pad * 2;
        float contentH = FS_H - 66f - pad;
        // ⚠ 縦だけのスクロールでは tier5以降の列（実測で横2,880px）が丸ごと見切れる。2軸で持つ。
        researchNodeContainer = MakeScroll2D(panel, pad, 66f, researchContentW, contentH);

        RefreshResearchPanel();
        researchPanel.SetActive(false);
    }

    // 同分野内の前提連鎖の長さ（＝ツリーの横位置。全前提が同分野なのでDAG）。
    private int ResearchDepth(ResearchNode n, int guard)
    {
        if (n.prereq == null || n.prereq.Length == 0 || guard > 12) return 0;
        int best = 0;
        foreach (var p in n.prereq)
            if (ResearchCatalog.TryGet(p, out var pn))
            {
                int d = ResearchDepth(pn, guard + 1) + 1;
                if (d > best) best = d;
            }
        return best;
    }

    private void RefreshResearchPanel()
    {
        if (researchNodeContainer == null) return;
        if (researchRpText != null)
            researchRpText.text = "危険度 <color=#e0a45a>" + DangerRank.Name + "</color>"
                + "　習熟 <color=#ffd24a>" + ResearchState.MasteredCount + "</color>"
                + "　研究点 <color=#8cb8e6>" + ResearchState.RP + " RP</color>";
        for (int i = researchNodeContainer.childCount - 1; i >= 0; i--)
        {
            var c = researchNodeContainer.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        // 🗺️ 地上研究は「地上」パネル内の専用タブへ移した（Civの技術/社会制度の二本立てに倣う）
        var fields = new ResearchField[] { ResearchField.Monster, ResearchField.Magic, ResearchField.Domain, ResearchField.Refine, ResearchField.DemonLord };
        float cellW = 232f, cellH = 100f, hGap = 56f, vGap = 14f;   // 📚 習熟の行ぶん背を伸ばした
        float y = 6f, maxX = researchContentW;
        foreach (var field in fields)
        {
            var nodes = ResearchCatalog.ByField(field);
            var ordered = new List<ResearchNode>(nodes);
            ordered.Sort((a, b) => a.row.CompareTo(b.row)); // 安定配置
            // 各ノードの depth(横位置) と 同depth内の row(縦位置) を決める
            var pos = new Dictionary<string, Vector2>();
            var rowOfDepth = new Dictionary<int, int>();
            float bandTop = y + 28f;
            int maxRows = 0;
            foreach (var n in ordered)
            {
                // 段は「宣言された tier」と「前提連鎖の長さ」の大きい方。
                // ⚠ tier だけだと旧ノード（tierを持たない）が2列に潰れ、depth だけだと
                //    合流ノードが親より手前に来ることがある。両取りするのが正解。
                int dep = Mathf.Max(n.tier, ResearchDepth(n, 0));
                int r = rowOfDepth.TryGetValue(dep, out var rr) ? rr : 0;
                rowOfDepth[dep] = r + 1;
                if (r + 1 > maxRows) maxRows = r + 1;
                pos[n.id] = new Vector2(dep * (cellW + hGap), bandTop + r * (cellH + vGap));
                if (pos[n.id].x + cellW + 24f > maxX) maxX = pos[n.id].x + cellW + 24f;
            }
            // 分野見出し（時代の内訳つき。どこまでが今の時代で開くのか帯の頭で分かるように）
            var head = Text(researchNodeContainer, "▍" + ResearchCatalog.FieldName(field) + "　<size=80%><color=#6f6889>"
                + FieldEraBreakdown(ordered) + "</color></size>", 15, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(head.rectTransform, 2, y, researchContentW - 4, 20);
            // 先に接続線を敷く（親→子）
            foreach (var n in ordered)
            {
                if (n.prereq == null) continue;
                foreach (var p in n.prereq)
                {
                    if (!pos.ContainsKey(p) || !pos.ContainsKey(n.id)) continue;
                    Vector2 P = pos[p], Cc = pos[n.id];
                    ResearchConnector(P.x + cellW, P.y + cellH / 2f, Cc.x, Cc.y + cellH / 2f);
                }
            }
            // セル本体
            foreach (var n in ordered)
            {
                Vector2 P = pos[n.id];
                AddResearchCell(researchNodeContainer, n, P.x, P.y, cellW, cellH);
            }
            y = bandTop + Mathf.Max(1, maxRows) * (cellH + vGap) + 18f;
        }
        // ⚠ 2軸スクロールの Content はストレッチしないので、**幅も**入れる（入れないと右の列が掴めない）。
        researchNodeContainer.sizeDelta = new Vector2(maxX, y + 12f);
    }

    // 「胎動6／伸長14／終焉10・習熟3」のような1行。研究済みと習熟の数も添える。
    private string FieldEraBreakdown(List<ResearchNode> nodes)
    {
        int d = 0, g = 0, e = 0, done = 0, mast = 0;
        foreach (var n in nodes)
        {
            if (n.era == EraSystem.Era.Dawn) d++; else if (n.era == EraSystem.Era.Growth) g++; else e++;
            if (ResearchState.IsResearched(n.id)) done++;
            if (ResearchState.IsMastered(n.id)) mast++;
        }
        return "胎動" + d + "／伸長" + g + "／終焉" + e + "　修了 " + done + "/" + nodes.Count + "・習熟 " + mast;
    }

    // 研究ノード1セル。
    private void AddResearchCell(RectTransform parent, ResearchNode node, float x, float y, float w, float h)
    {
        bool done = ResearchState.IsResearched(node.id);
        bool prereqOK = ResearchState.PrereqMet(node);
        bool eraOK = ResearchState.EraMet(node);
        bool gateOK = ResearchState.GateMet(node);
        bool can = ResearchState.CanResearch(node.id);
        bool mastered = ResearchState.IsMastered(node.id);
        var cell = Panel(parent, "R_" + node.id, CARD);
        Place(cell.rectTransform, x, y, w, h);
        Outline(cell, mastered ? GOLD : (done ? GREEN : (can ? GOLD : LINE)));
        var nm = Text(cell.rectTransform, (mastered ? "◆" : "") + node.jpName, 12.5f,
            done ? GREEN : ((prereqOK && eraOK) ? TEXT : FAINT), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(nm.rectTransform, 9, 6, w - 18, 16);
        int effCost = ResearchState.EffectiveCost(node); // 🧠 知識ランクの割引後
        // 開かない理由は「時代 → 前提 → 解放条件」の順に1つだけ出す（全部並べると読めない）
        string state;
        Color stateC;
        if (done) { state = "研究済"; stateC = GREEN; }
        else if (!eraOK) { state = "― " + EraSystem.EraName(node.era) + "から"; stateC = C("#c9a8ff"); }
        else if (!prereqOK) { state = "― 前提未達"; stateC = MUTED; }
        else if (!gateOK) { state = "未開放：" + ResearchState.GateText(node); stateC = C("#e0a45a"); }
        else
        {
            state = "コスト " + effCost + " RP"
                  + (effCost < node.cost ? " <size=80%><color=#5cc47c>(-" + (node.cost - effCost) + ")</color></size>" : "");
            stateC = can ? GOLD : MUTED;
        }
        var st = Text(cell.rectTransform, state, 10.5f, stateC, TextAlignmentOptions.TopLeft);
        Place(st.rectTransform, 9, 24, w - 18, 14);
        var ds = Text(cell.rectTransform, node.desc, 9.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(ds.rectTransform, 9, 39, w - 18, 22);
        // ⚠ 既定は Overflow なので、長い説明が下の『習熟』行に**重なって読めなくなる**。ここだけ切り詰める。
        ds.overflowMode = TextOverflowModes.Ellipsis;
        // 📚 習熟（Civ VIIのMastery）：研究済みのノードにだけ出る第2段階。後続の前提ではないので、
        //    ここを押すか先へ進むかは毎回の選択になる。
        if (done) AddMasteryRow(cell.rectTransform, node, w, h - 38f);
        // 💡 天啓（Civのユーレカ）：達成済みなら光らせ、未達なら「何をすれば安くなるか」を見せる
        if (!string.IsNullOrEmpty(node.eureka))
        {
            bool got = EurekaTracker.Has(node.id);
            var eu = Text(cell.rectTransform,
                got ? "<color=#ffd24a>◆天啓達成 40%引き</color>" : "<color=#6f6889>天啓: " + node.eureka + "</color>",
                9.5f, got ? GOLD : FAINT, TextAlignmentOptions.TopLeft, got ? FontStyles.Bold : FontStyles.Normal);
            Place(eu.rectTransform, 9, h - 20, w - 18, 16);
        }
        if (can)
        {
            var btn = cell.gameObject.AddComponent<Button>(); btn.targetGraphic = cell;
            btn.onClick.AddListener(() => { if (ResearchState.TryResearch(node.id)) { RefreshResearchPanel(); RefreshMinionCodex(); } });
        }
    }

    // 習熟の1行（研究済みのセルの下段）。押せるときだけボタンにする。
    private void AddMasteryRow(RectTransform cell, ResearchNode node, float w, float rowY)
    {
        if (ResearchState.IsMastered(node.id))
        {
            var t = Text(cell, "<color=#ffd24a>◆習熟済</color> <color=#8a8299>" + ResearchState.MasteryLabel(node) + "</color>",
                9.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(t.rectTransform, 9, rowY, w - 18, 16);
            return;
        }
        int cost = ResearchState.MasteryCost(node);
        string why = ResearchState.MasteryBlockReason(node);
        if (string.IsNullOrEmpty(why))
        {
            var b = PrimaryButton(cell, "習熟 " + cost + "RP ｜ " + ResearchState.MasteryLabel(node), PANEL2, GOLD,
                () => { if (ResearchState.TryMaster(node.id)) { RefreshResearchPanel(); RefreshMinionCodex(); } });
            Place((RectTransform)b.transform, 9, rowY, w - 18, 18);
            var lb = b.GetComponentInChildren<TMP_Text>(); if (lb != null) lb.fontSize = 9.5f;
        }
        else
        {
            var t = Text(cell, "<color=#6f6889>習熟 " + cost + "RP ― " + why + "</color>", 9.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(t.rectTransform, 9, rowY, w - 18, 16);
        }
    }

    // 親右端→子左端の直交接続線（水平→垂直→水平の3セグ）。座標は上原点。
    private void ResearchConnector(float x1, float y1, float x2, float y2)
    {
        float midX = (x1 + x2) / 2f;
        LineRect(researchNodeContainer, Mathf.Min(x1, midX), y1 - 1f, Mathf.Abs(midX - x1), 2f);
        LineRect(researchNodeContainer, midX - 1f, Mathf.Min(y1, y2), 2f, Mathf.Abs(y2 - y1) + 2f);
        LineRect(researchNodeContainer, Mathf.Min(midX, x2), y2 - 1f, Mathf.Abs(x2 - midX), 2f);
    }
}
