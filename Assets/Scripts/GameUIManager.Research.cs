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

    /// <summary>研究点・危険度・習熟数の1行（迷宮ツリーと地上ツリーで同じものを出す）。</summary>
    private static string TreeStatusLine()
        => "危険度 <color=#e0a45a>" + DangerRank.Name + "</color>"
         + "　習熟 <color=#ffd24a>" + ResearchState.MasteredCount + "</color>"
         + "　研究点 <color=#8cb8e6>" + ResearchState.RP + " RP</color>";

    private void RefreshResearchPanel()
    {
        if (researchNodeContainer == null) return;
        if (researchRpText != null) researchRpText.text = TreeStatusLine();
        // 🗺️ 地上研究と業の研究は地上側の専用ツリーへ（Civの技術／社会制度の二本立てに倣う）
        BuildTreeGraph(researchNodeContainer, researchContentW,
            new[] { ResearchField.Monster, ResearchField.Magic, ResearchField.Domain, ResearchField.Refine, ResearchField.DemonLord },
            () => { RefreshResearchPanel(); RefreshMinionCodex(); });
    }

    /// <summary>
    /// 🌳 ツリーを1枚描く（Civ風：段＝列・前提＝線・分野ごとに帯）。
    /// **迷宮ツリーと地上ツリーの両方がここを呼ぶ**ので、片方だけ見た目が古くなることがない。
    /// `onChanged` は研究／習熟が成立したときの作り直し（呼び元のパネルによって違う）。
    /// ⚠ `container` は必ず `MakeScroll2D` のものを渡すこと。実測で横2,800px を超える。
    /// </summary>
    private void BuildTreeGraph(RectTransform container, float containerW, ResearchField[] fields, System.Action onChanged)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            var g = container.GetChild(i).gameObject; g.SetActive(false); Destroy(g);
        }
        float cellW = 232f, cellH = 100f, hGap = 56f, vGap = 14f;   // 📚 習熟の行ぶん背を伸ばした
        float y = 6f, maxX = containerW;
        foreach (var field in fields)
        {
            var ordered = ResearchCatalog.ByField(field);
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
            var head = Text(container, "▍" + ResearchCatalog.FieldName(field) + "　<size=80%><color=#6f6889>"
                + FieldEraBreakdown(ordered) + "</color></size>", 15, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(head.rectTransform, 2, y, containerW - 4, 20);
            // 先に接続線を敷く（親→子）
            foreach (var n in ordered)
            {
                if (n.prereq == null) continue;
                foreach (var p in n.prereq)
                {
                    if (!pos.ContainsKey(p) || !pos.ContainsKey(n.id)) continue;
                    Vector2 P = pos[p], Cc = pos[n.id];
                    // 🔗 合流（前提2つ以上）の線は色を変える。Civの格子はここが読めないと辿れない。
                    ResearchConnector(container, P.x + cellW, P.y + cellH / 2f, Cc.x, Cc.y + cellH / 2f,
                        ResearchState.IsResearched(p), n.prereq.Length >= 2);
                }
            }
            // セル本体
            foreach (var n in ordered)
            {
                Vector2 P = pos[n.id];
                AddResearchCell(container, n, P.x, P.y, cellW, cellH, onChanged);
            }
            y = bandTop + Mathf.Max(1, maxRows) * (cellH + vGap) + 18f;
        }
        // ⚠ 2軸スクロールの Content はストレッチしないので、**幅も**入れる（入れないと右の列が掴めない）。
        container.sizeDelta = new Vector2(maxX, y + 12f);
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
    private void AddResearchCell(RectTransform parent, ResearchNode node, float x, float y, float w, float h, System.Action onChanged)
    {
        bool done = ResearchState.IsResearched(node.id);
        bool prereqOK = ResearchState.PrereqMet(node);
        bool eraOK = ResearchState.EraMet(node);
        bool gateOK = ResearchState.GateMet(node);
        bool can = ResearchState.CanResearch(node.id);
        bool mastered = ResearchState.IsMastered(node.id);
        bool sealed_ = ResearchState.ExclusiveBlocked(node);   // 🔒 別の道を選んだので永久に閉じた
        int repeats = node.repeatable ? ResearchState.RepeatCount(node.id) : 0;
        var cell = Panel(parent, "R_" + node.id, CARD);
        Place(cell.rectTransform, x, y, w, h);
        Outline(cell, sealed_ ? C("#4a2030") : mastered ? GOLD : (done ? GREEN : (can ? GOLD : LINE)));
        var nm = Text(cell.rectTransform,
            (sealed_ ? "<s>" : "") + (mastered ? "◆" : "") + node.jpName + (sealed_ ? "</s>" : "")
            + (repeats > 0 ? " <color=#ffd24a>×" + repeats + "</color>" : ""), 12.5f,
            sealed_ ? C("#6b4a55") : done ? GREEN : ((prereqOK && eraOK) ? TEXT : FAINT),
            TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(nm.rectTransform, 9, 6, w - 18, 16);
        int effCost = ResearchState.EffectiveCost(node); // 🧠 知識ランクの割引後
        // 開かない理由は「時代 → 前提 → 解放条件」の順に1つだけ出す（全部並べると読めない）
        string state;
        Color stateC;
        if (sealed_)
        {
            state = "封印 ― 『" + ResearchState.ExclusiveChosenName(node.exclusive) + "』を選んだ";
            stateC = C("#a05a70");
        }
        else if (node.repeatable)
        {
            int rc = ResearchState.RepeatCost(node);
            state = "重ねる " + rc + " RP" + (repeats > 0 ? " <size=80%><color=#6f6889>(" + (repeats + 1) + "回目)</color></size>" : "");
            stateC = can ? GOLD : MUTED;
        }
        else if (done) { state = "研究済"; stateC = GREEN; }
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
        // 🔒 排他：まだ選んでいない分岐は「選ぶと他が閉じる」ことを**押す前に**見せる。
        if (!string.IsNullOrEmpty(node.exclusive) && !done && !sealed_)
        {
            var ex = Text(cell.rectTransform, "<color=#e05a5a>◆選ぶと他の刻印は永久に閉じる</color>",
                9.5f, C("#e05a5a"), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(ex.rectTransform, 9, h - 38f, w - 18, 16);
        }
        // 📚 習熟（Civ VIIのMastery）：研究済みのノードにだけ出る第2段階。後続の前提ではないので、
        //    ここを押すか先へ進むかは毎回の選択になる。⚠ 反復ノードに習熟は出さない（重ねるのが伸ばし方）。
        else if (done && !node.repeatable) AddMasteryRow(cell.rectTransform, node, w, h - 38f, onChanged);
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
            btn.onClick.AddListener(() => { if (ResearchState.TryResearch(node.id) && onChanged != null) onChanged(); });
        }
    }

    // 習熟の1行（研究済みのセルの下段）。押せるときだけボタンにする。
    private void AddMasteryRow(RectTransform cell, ResearchNode node, float w, float rowY, System.Action onChanged)
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
                () => { if (ResearchState.TryMaster(node.id) && onChanged != null) onChanged(); });
            Place((RectTransform)b.transform, 9, rowY, w - 18, 18);
            var lb = b.GetComponentInChildren<TMP_Text>(); if (lb != null) lb.fontSize = 9.5f;
        }
        else
        {
            var t = Text(cell, "<color=#6f6889>習熟 " + cost + "RP ― " + why + "</color>", 9.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(t.rectTransform, 9, rowY, w - 18, 16);
        }
    }

    /// <summary>
    /// 親右端→子左端の直交接続線（水平→垂直→水平の3セグ）。座標は上原点。
    /// 済んだ前提は緑、**合流（前提2つ以上）は金**にする。
    /// ⚠ Civの格子は「この子は2本の線が来ている」が見えないと辿れない。全部同じ灰色にしない。
    /// </summary>
    private void ResearchConnector(RectTransform parent, float x1, float y1, float x2, float y2, bool prereqDone, bool merge)
    {
        Color col = prereqDone ? (merge ? GOLD : GREEN) : (merge ? C("#6d5a2e") : LINE2);
        float th = merge ? 3f : 2f;
        float midX = (x1 + x2) / 2f;
        LineRect(parent, Mathf.Min(x1, midX), y1 - th / 2f, Mathf.Abs(midX - x1), th, col);
        LineRect(parent, midX - th / 2f, Mathf.Min(y1, y2), th, Mathf.Abs(y2 - y1) + th, col);
        LineRect(parent, Mathf.Min(midX, x2), y2 - th / 2f, Mathf.Abs(x2 - midX), th, col);
    }
}
