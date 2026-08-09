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
        var title = Text(panel, "研究ツリー（分野ごとに前提を線で接続／知識でRP蓄積）", 17, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 16, FS_W - 420, 24);
        researchRpText = Text(panel, "", 15, C("#8cb8e6"), TextAlignmentOptions.Right, FontStyles.Bold);
        Place(researchRpText.rectTransform, FS_W - pad - 300, 16, 260, 24);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => researchPanel.SetActive(false));
        Place((RectTransform)close.transform, FS_W - pad - 32, 14, 32, 30);

        researchContentW = FS_W - pad * 2;
        float contentH = FS_H - 66f - pad;
        researchNodeContainer = MakeVScroll(panel, pad, 66f, researchContentW, contentH);

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
        if (researchRpText != null) researchRpText.text = "研究点 <color=#8cb8e6>" + ResearchState.RP + " RP</color>";
        for (int i = researchNodeContainer.childCount - 1; i >= 0; i--)
        {
            var c = researchNodeContainer.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        // 🗺️ 地上研究は「地上」パネル内の専用タブへ移した（Civの技術/社会制度の二本立てに倣う）
        var fields = new ResearchField[] { ResearchField.Monster, ResearchField.Magic, ResearchField.Domain, ResearchField.Refine, ResearchField.DemonLord };
        float cellW = 232f, cellH = 82f, hGap = 56f, vGap = 16f;
        float y = 6f;
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
                int dep = ResearchDepth(n, 0);
                int r = rowOfDepth.TryGetValue(dep, out var rr) ? rr : 0;
                rowOfDepth[dep] = r + 1;
                if (r + 1 > maxRows) maxRows = r + 1;
                pos[n.id] = new Vector2(dep * (cellW + hGap), bandTop + r * (cellH + vGap));
            }
            // 分野見出し
            var head = Text(researchNodeContainer, "▍" + ResearchCatalog.FieldName(field), 15, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
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
        researchNodeContainer.sizeDelta = new Vector2(0f, y + 12f);
    }

    // 研究ノード1セル。
    private void AddResearchCell(RectTransform parent, ResearchNode node, float x, float y, float w, float h)
    {
        bool done = ResearchState.IsResearched(node.id);
        bool prereqOK = ResearchState.PrereqMet(node);
        bool can = ResearchState.CanResearch(node.id);
        var cell = Panel(parent, "R_" + node.id, CARD);
        Place(cell.rectTransform, x, y, w, h); Outline(cell, done ? GREEN : (can ? GOLD : LINE));
        var nm = Text(cell.rectTransform, node.jpName, 12.5f, done ? GREEN : (prereqOK ? TEXT : FAINT), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(nm.rectTransform, 9, 6, w - 18, 16);
        int effCost = ResearchState.EffectiveCost(node); // 🧠 知識ランクの割引後
        string state = done ? "研究済" : (prereqOK ? ("コスト " + effCost + " RP" + (effCost < node.cost ? " <size=80%><color=#5cc47c>(-" + (node.cost - effCost) + ")</color></size>" : "")) : "― 前提未達");
        var st = Text(cell.rectTransform, state, 10.5f, done ? GREEN : (can ? GOLD : MUTED), TextAlignmentOptions.TopLeft);
        Place(st.rectTransform, 9, 24, w - 18, 14);
        var ds = Text(cell.rectTransform, node.desc, 9.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(ds.rectTransform, 9, 38, w - 18, 26);
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

    // 親右端→子左端の直交接続線（水平→垂直→水平の3セグ）。座標は上原点。
    private void ResearchConnector(float x1, float y1, float x2, float y2)
    {
        float midX = (x1 + x2) / 2f;
        LineRect(researchNodeContainer, Mathf.Min(x1, midX), y1 - 1f, Mathf.Abs(midX - x1), 2f);
        LineRect(researchNodeContainer, midX - 1f, Mathf.Min(y1, y2), 2f, Mathf.Abs(y2 - y1) + 2f);
        LineRect(researchNodeContainer, Mathf.Min(midX, x2), y2 - 1f, Mathf.Abs(x2 - midX), 2f);
    }
}
