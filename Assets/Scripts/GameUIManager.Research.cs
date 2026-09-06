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
        var title = Text(panel, "研究ツリー　<size=80%><color=#9c95b4>時代ごとに入れ替わる／ノードにカーソルを合わせると中身が出る</color></size>", 17, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 16, FS_W - 560, 24);
        researchRpText = Text(panel, "", 14, C("#8cb8e6"), TextAlignmentOptions.Right, FontStyles.Bold);
        Place(researchRpText.rectTransform, FS_W - pad - 480, 16, 440, 24);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => researchPanel.SetActive(false));
        Place((RectTransform)close.transform, FS_W - pad - 32, 14, 32, 30);

        researchContentW = FS_W - pad * 2;
        float contentH = FS_H - 66f - pad;
        // ⚠ 縦だけのスクロールでは tier5以降の列（実測で横2,880px）が丸ごと見切れる。2軸で持つ。
        researchNodeContainer = MakeScroll2D(panel, pad, 66f, researchContentW, contentH);

        BuildResearchTip(panel.rectTransform);   // 🔍 専用ツールチップの器
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
        // ⚠⚠ **K-3：時代でツリーを割る。** Civ VII は「その時代ぶんが全部見える」形で、
        //   時代が変われば別のツリーになる。233枚を1枚に積んでいたのが
        //   「1周で8%しか触れない」の正体だったので、ここで3枚に割って見せる。
        int eraTab = researchEraTab >= 0 ? researchEraTab : (int)EraSystem.Current;
        float cellW = 268f, cellH = 78f, hGap = 62f, vGap = 12f;   // 🕯️ 説明文を外したぶん低くした
        float y = 6f, maxX = containerW;
        y = BuildEraTabs(container, containerW, eraTab, y, onChanged);
        foreach (var field in fields)
        {
            var all = ResearchCatalog.ByField(field);
            var ordered = new List<ResearchNode>();
            foreach (var n0 in all) if ((int)n0.era == eraTab) ordered.Add(n0);
            if (ordered.Count == 0) continue;
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
            int fdone = 0; foreach (var n1 in ordered) if (ResearchState.IsResearched(n1.id)) fdone++;
            var head = Text(container, "▍" + ResearchCatalog.FieldName(field) + "　<size=80%><color=#6f6889>"
                + fdone + "/" + ordered.Count + "</color></size>", 15, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
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

    /// <summary>
    /// 🕯️ **時代タブ**（K-3）。Civ VII のツリーは時代ごとに丸ごと入れ替わるので、ここで選ばせる。
    /// ⚠ 既定は**いまの時代**。まだ来ていない時代も覗けるが、そこのノードは研究できない。
    /// </summary>
    private float BuildEraTabs(RectTransform c, float w, int eraTab, float y, System.Action onChanged)
    {
        float tw = 168f;
        for (int e = 0; e < 3; e++)
        {
            int ei = e;
            bool on = eraTab == e;
            bool reached = (int)EraSystem.Current >= e;
            int total = 0, done = 0;
            foreach (var n in ResearchCatalog.All)
            {
                if ((int)n.era != e) continue;
                total++;
                if (ResearchState.IsResearched(n.id)) done++;
            }
            var tab = Panel(c, "EraTab" + e, on ? C("#2c2540") : PANEL2);
            Place(tab.rectTransform, 2 + e * (tw + 6), y, tw, 34);
            Outline(tab, on ? GOLD : LINE);
            var t = Text(tab.rectTransform,
                "<b>" + EraSystem.EraName((EraSystem.Era)e).Replace("の時代", "") + "</b>"
                + "  <size=78%><color=#9c95b4>" + done + "/" + total + "</color></size>"
                + (reached ? "" : "  <size=72%><color=#4a4560>まだ来ていない</color></size>"),
                12.5f, on ? GOLD : (reached ? TEXT : FAINT), TextAlignmentOptions.Center);
            StretchFull(t.rectTransform);
            var bt = tab.gameObject.AddComponent<Button>(); bt.targetGraphic = tab;
            bt.onClick.AddListener(() => { researchEraTab = ei; if (onChanged != null) onChanged(); });
            AddTooltip(tab.gameObject, EraSystem.EraName((EraSystem.Era)ei) + "のツリーを見る"
                + (reached ? "" : "（まだ来ていないので研究はできない）"));
        }
        return y + 44f;
    }

    /// <summary>
    /// 🎁 そのノードが配る物のアイコン列（K-3・アーティファクトの「貰える物のアイコン」）。
    ///
    /// ⚠⚠ **いまの `Research.cs` は 162/233 が「+X%」で、配る「物」を持っていない。**
    ///   なので当面は**分野と効果の種類から引く**。K-3の後半でノードを「物」に付け替えたら、
    ///   ここは本物の解禁対象（建造物・配下・カード…）に差し替える。
    /// </summary>
    private static string[] NodeGiveIcons(ResearchNode n)
    {
        var l = new List<string>();
        switch (n.field)
        {
            case ResearchField.Monster: l.Add("pop"); break;
            case ResearchField.Domain: l.Add("slot"); break;
            case ResearchField.Refine: l.Add("material"); break;
            case ResearchField.DemonLord: l.Add("danger"); break;
            case ResearchField.Magic: l.Add("emotion"); break;
            case ResearchField.Surface: l.Add("influence"); break;
            default: l.Add("research"); break;
        }
        switch (n.effect)
        {
            case ResEffect.DefenderHp: case ResEffect.DefenderAtk: case ResEffect.DefenderSpeed:
            case ResEffect.ResistAll: l.Add("threat"); break;
            case ResEffect.TrapDamage: l.Add("slot"); break;
            case ResEffect.MagicPower: l.Add("emotion"); break;
            case ResEffect.DpYield: l.Add("dp"); break;
            case ResEffect.MaterialYield: l.Add("material"); break;
            case ResEffect.RpYield: l.Add("research"); break;
            case ResEffect.EmotionGain: l.Add("emotion"); break;
            case ResEffect.ExpGain: l.Add("world"); break;
            case ResEffect.LordPower: l.Add("danger"); break;
            case ResEffect.KinPower: case ResEffect.SurfaceDefense: case ResEffect.SurfaceYield: l.Add("influence"); break;
            case ResEffect.MutationSuppress: l.Add("mutation"); break;
        }
        if (n.repeatable) l.Add("world");
        return l.ToArray();
    }

    /// <summary>
    /// 🕯️ **ノードのカード**（K-3・アーティファクトの通り）。
    ///
    /// ⚠⚠ **カードに数字と説明文を載せない。** 載せるのは
    ///   ①名前 ②所要ターン（押せるときだけ） ③貰える物のアイコン ④印（天啓/排他/反復/条件）だけ。
    ///   詳しいことは**ホバーの中**。旧版は説明文・コスト・天啓の条件まで詰め込んでいて、
    ///   24枚並べた時点で読めなかった。
    /// ⚠ 状態は**色ではなく枠の明るさ**で分ける（色は「何の枝か」に使う）。
    /// </summary>
    private void AddResearchCell(RectTransform parent, ResearchNode node, float x, float y, float w, float h, System.Action onChanged)
    {
        bool done = ResearchState.IsResearched(node.id);
        bool prereqOK = ResearchState.PrereqMet(node);
        bool eraOK = ResearchState.EraMet(node);
        bool gateOK = ResearchState.GateMet(node);
        bool can = ResearchState.CanResearch(node.id);
        bool mastered = ResearchState.IsMastered(node.id);
        bool sealed_ = ResearchState.ExclusiveBlocked(node);
        int repeats = node.repeatable ? ResearchState.RepeatCount(node.id) : 0;

        var cell = Panel(parent, "R_" + node.id, done ? C("#1d1a26") : (can ? C("#241d16") : CARD));
        Place(cell.rectTransform, x, y, w, h);
        Outline(cell, sealed_ ? C("#4a2030") : can ? GOLD : (done ? C("#5a4e2e") : LINE));

        // ① 名前
        var nm = Text(cell.rectTransform,
            (sealed_ ? "<s>" : "") + (mastered ? "◆" : "") + node.jpName + (sealed_ ? "</s>" : "")
            + (repeats > 0 ? " <color=#ffd24a>×" + repeats + "</color>" : ""), 13.5f,
            sealed_ ? C("#6b4a55") : done ? C("#cbbfa0") : ((prereqOK && eraOK) ? TEXT : FAINT),
            TextAlignmentOptions.TopLeft, FontStyles.Bold);
        nm.enableWordWrapping = false;
        nm.overflowMode = TextOverflowModes.Ellipsis;
        // ⚠⚠ **TMPは行の高さが枠より大きいと1文字も描かない。** 13.5pt には 22px 要る
        //   （実測：18px にしたら chars=0 で名前が全部消えた）。→ [[ui-conventions]]
        Place(nm.rectTransform, 10, 5, w - 88, 22);

        // ② 所要ターン（押せるときだけ）／研究済みは ✔
        if (done)
        {
            var ck = Text(cell.rectTransform, "✔", 14f, GOLD, TextAlignmentOptions.TopRight, FontStyles.Bold);
            Place(ck.rectTransform, w - 32, 5, 24, 22);
        }
        else if (can)
        {
            // ⚠ 研究点が足りているときに「―」を出すと「終わらない」に見える。**今すぐ**と言う。
            int turns = ResearchTurnsFor(node);
            var tn = Text(cell.rectTransform,
                turns == 0 ? "<color=#5cc47c>今すぐ</color>" : turns < 0 ? "―" : turns + "ターン",
                11.5f, C("#d8c8a0"), TextAlignmentOptions.TopRight);
            tn.enableWordWrapping = false;
            Place(tn.rectTransform, w - 80, 6, 72, 20);
        }

        // 仕切り線（Civのカードにある細い罫）
        LineRect(cell.rectTransform, 10, 27, w - 20, 1, C("#3a3222"));

        // ③ 貰える物のアイコン
        // ⚠⚠ **絵文字は使わない。** このプロジェクトのフォント(NotoSansJP)に無く、**黙って消える**
        //   （実測：ここを絵文字で書いたら1つも出なかった。🔨 のときと同じ罠）。→ [[UIIcons]]
        var give = NodeGiveIcons(node);
        for (int i = 0; i < give.Length && i < 5; i++)
        {
            var ic = Panel(cell.rectTransform, "g" + i, C("#2a2418"));
            Place(ic.rectTransform, 10 + i * 27, 34, 24, 24); Outline(ic, C("#6b5a38"));
            var sp = UIIcons.Get(give[i]);
            if (sp != null)
            {
                var im = Panel(ic.rectTransform, "s", UIIcons.IsArt(give[i]) ? Color.white : C("#d8c8a0"));
                im.sprite = sp; im.type = Image.Type.Simple; im.preserveAspect = true; im.raycastTarget = false;
                Place(im.rectTransform, 3, 3, 18, 18);
            }
        }

        // ④ 印
        float bx = w - 10;
        bx = ResearchBadge(cell.rectTransform, bx, node.repeatable, "反復", "#8fd0b0", "#2f6a52");
        bx = ResearchBadge(cell.rectTransform, bx, !string.IsNullOrEmpty(node.exclusive) && !sealed_, "排他", "#d47a7a", "#7a3030");
        bx = ResearchBadge(cell.rectTransform, bx, !string.IsNullOrEmpty(node.eureka) && !done,
            EurekaTracker.Has(node.id) ? "天啓 済" : "天啓", "#e0b23a", "#7a6224");
        bx = ResearchBadge(cell.rectTransform, bx, !gateOK && eraOK && prereqOK && !done, "条件", "#9c95b4", "#4a4268");

        // 📚 習熟は研究済みのときだけ下段に
        if (done && !node.repeatable) AddMasteryRow(cell.rectTransform, node, w, h - 22f, onChanged);

        // 🔍 詳しいことは**ホバーの中**。
        // ⚠⚠ 汎用の `AddTooltip`（画面下の 560×30 の固定箱）は使わない ―― 研究の説明を流し込むと
        //   **枠からはみ出て読めない**。専用パネルに出す。→ [[GameUIManager.ResearchTip]]
        {
            var nd = node;
            var tt = cell.gameObject.GetComponent<UITooltipTrigger>();
            if (tt == null) tt = cell.gameObject.AddComponent<UITooltipTrigger>();
            tt.tip = nd.jpName;
            tt.onShow = _ => ShowResearchTip(nd);
            tt.onHide = HideResearchTip;
        }

        if (can)
        {
            var btn = cell.gameObject.AddComponent<Button>(); btn.targetGraphic = cell;
            btn.onClick.AddListener(() => { if (ResearchState.TryResearch(node.id) && onChanged != null) onChanged(); });
        }
    }

    /// <summary>小さな印を右下に積む。返り値は次に置ける右端。</summary>
    private float ResearchBadge(RectTransform cell, float rightX, bool show, string label, string fg, string bd)
    {
        if (!show) return rightX;
        float bw = label.Length >= 4 ? 52f : 40f;
        var b = Panel(cell, "b_" + label, C("#0e0c15"));
        Place(b.rectTransform, rightX - bw, 59, bw, 16); Outline(b, C(bd));
        var t = Text(b.rectTransform, label, 9f, C(fg), TextAlignmentOptions.Center);
        StretchFull(t.rectTransform);
        return rightX - bw - 4f;
    }

    /// <summary>
    /// ⏳ 所要ターン ＝ 残りコスト ÷ 毎ターンの研究点。
    /// ⚠ **予測ではなく、前ターンに実際に入った量**で割る（→ 上部バーの増分と同じ考え方）。
    ///   0 なら「―」を出す（いつまでも終わらないことを、7,000ターンのような数字で誤魔化さない）。
    /// </summary>
    private int ResearchTurnsFor(ResearchNode node)
    {
        int cost = node.repeatable ? ResearchState.RepeatCost(node) : ResearchState.EffectiveCost(node);
        int left = Mathf.Max(0, cost - ResearchState.RP);
        if (left <= 0) return 0;
        int per = yGainRp > 0 ? yGainRp : (DemonLord.Instance != null ? 1 + DemonLord.Instance.KnowledgeRank : 1);
        if (per <= 0) return -1;
        return Mathf.CeilToInt(left / (float)per);
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
