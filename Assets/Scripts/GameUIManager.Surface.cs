using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 地上（4X）の全画面。ヘクス盤・領域詳細・眷属一覧・外交／物語／政策／属性／時代／勝利。
/// ここだけで約2,000行あるので、さらに割るならこのファイルを分ける。
/// <para>`GameUIManager` の partial。フィールドの本体は GameUIManager.cs 側にある。</para>
/// </summary>
public partial class GameUIManager
{

    // ---------- 階層拡張トラック（横拡張：研究点＋DP） ----------
    // ---------- 🗺️ 地上（4X）パネル：眷属を編成して領域へ進軍させる ----------

    private void BuildSurfacePanel(RectTransform root)
    {
        // 🌍 地上は**盤そのものをUnityのシーンで描く**（[[SurfaceView]]）ので、
        //    このパネルは**透明な器**にして、UIは必要なところにだけ不透明な板を敷く。
        //    こうしないと盤の上にUIの背景がかぶって世界が見えない。
        var panel = Panel(root, "SurfacePanel", new Color(0, 0, 0, 0));
        surfacePanel = panel.gameObject;
        panel.raycastTarget = false;
        Anchor(panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        panel.rectTransform.offsetMin = Vector2.zero; panel.rectTransform.offsetMax = Vector2.zero;
        var inner = Panel(panel, "SurfaceInner", new Color(0, 0, 0, 0));
        inner.raycastTarget = false;
        Anchor(inner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        inner.rectTransform.sizeDelta = new Vector2(FS_W, FS_H);
        inner.rectTransform.anchoredPosition = Vector2.zero;
        surfaceInnerRt = inner.rectTransform;   // 🖱️ ホイールを盤に渡してよいかの判定に使う
        panel = inner;

        float pad = 22f, w = FS_W - pad * 2;

        // ── 🗂️ Civ式のUI：常時出すのは**上の帯だけ**。あとは左のメニューから開く。
        //    盤がシーンそのものになった以上、パネルを敷きっぱなしにすると世界が見えない。
        //    「開いているときだけ場所を取る」形にして、既定では**何も開いていない**。
        float barH = 84f;
        var headBg = Panel(panel, "HeadBg", PANEL);
        Place(headBg.rectTransform, 0, 0, FS_W, barH); Outline(headBg, LINE2); SkinPanel(headBg);
        surfaceTurnText = Text(panel, "地上", 17, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        surfaceTurnText.enableWordWrapping = false;
        Place(surfaceTurnText.rectTransform, pad, 10, 196, 24);
        // ▶▶ **次の一手と、ターンの締め**（K-6 A-2）。
        //
        // ⚠⚠ **上の帯に入れてはいけない。** 最初そうしたら、帯の中の小さなボタンになって
        //   「大ボタン」の値打ちが丸ごと消えた（指摘を受けた）。Civ VII も、この作品の画面案も、
        //   **盤の上に浮く独立した塊**として右下に置いている ―― 目が最後に行く場所で、手が一番近い。
        // ⚠ 帯に縛られないので、**文字が読める大きさ**にできる。ここが帯との決定的な違い。
        // ⚠ 『ターンを終える』も一緒にここへ移す。**次の一手 → 締め**が縦に並ぶことで、
        //   「まだ手がある／もう無い」がそのまま上下の並びになる。
        {
            // ⚠ 高さは `GameUIManager.Hud` の `SurfCardW/SurfHintH/...` と同じ値を使う
            //   （手が尽きたとき、あちらがこの塊を縮める）
            float cardW = SurfCardW, hintH = SurfHintH, bigH = SurfBigH, endH = SurfEndH;
            var stack = Panel(panel, "SurfAction", new Color(0.08f, 0.07f, 0.10f, 0.86f));
            surfActionStack = stack.gameObject;
            Outline(stack, LINE2);
            Anchor(stack, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            stack.rectTransform.sizeDelta = new Vector2(cardW + 20f, hintH + bigH + endH + 26f);
            stack.rectTransform.anchoredPosition = new Vector2(-pad, pad);

            surfNextHint = Text(stack.rectTransform, "", 11f, C("#9c95b4"), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            surfNextHint.enableWordWrapping = false;
            Place(surfNextHint.rectTransform, 10, 6, cardW, hintH);

            surfNextBtn = PrimaryButton(stack, "", C("#e3a94a"), C("#1a1206"), () => DoNextAction(true), true);
            Place((RectTransform)surfNextBtn.transform, 10, 6 + hintH, cardW, bigH);

            // ⏳ 後半の締め。**ここを押すと世界が1ターン進む**ので、赤い主要アクションにして
            //    「迷宮へ戻る」ではなく「ターンを終える」と書く（戻る場所ではなく、次へ送る操作）。
            var endTurnBtn = PrimaryButton(stack, "ターンを終える ▶", BLOOD, TEXT, () =>
            {
                if (turn != null && turn.IsSurfacePhase) turn.EndSurfacePhase();
            }, true);
            surfEndBtnRt = (RectTransform)endTurnBtn.transform;
            Place(surfEndBtnRt, 10, 12 + hintH + bigH, cardW, endH);
            AddTooltip(((RectTransform)endTurnBtn.transform).gameObject,
                "地上の行動を終えて、次のターンの<b>前半（迷宮）</b>へ進みます。"
                + "押すと他の魔王と人間の軍が動き、産出が入ります。　<color=#9c95b4>[Space]</color>");
        }
        // 💰 段G（地上）：**迷宮と同じ帯**にそろえた。大事な4つ（DP・研究点・素材・名声）を大きく、
        //   地上の物（支配・生産・幸福・拠点）は小さな札。⚠ 以前は同じ4資源が帯の小札と、帯の下の別の列に**2回**出ていた。
        //   ⚠ 並びは迷宮の帯と同じ（DP・研究点・素材・名声）。毎回同じ場所に同じ物があること。
        //   ⚠ 左のターン表示（「地上　第3ターン 後半」）と重ならない位置から始める。
        {
            var row = NewRect("SurfYieldRow", panel.rectTransform);
            Place(row, pad + 210f, 6, FS_W - pad * 2 - 210f, 44);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 6; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            var rowG = row.gameObject.AddComponent<Image>(); rowG.color = new Color(0, 0, 0, 0); rowG.raycastTarget = false;
            // ⚠ この4つは収穫が吸い込まれる先でもある（→ `SurfaceChipWorldTarget`）
            surfDpText   = YieldChip(rowG, UITheme.DP,       "DP",     "0", "dp",       out surfDpDelta, true, true);
            surfRpText   = YieldChip(rowG, UITheme.Research, "研究点", "0", "research", out surfRpDelta, true, true);
            surfMatText  = YieldChip(rowG, UITheme.Material, "素材",   "0", "material", out surfMatDelta, true, true);
            surfFameText = YieldChip(rowG, UITheme.Fame,     "名声",   "0", "fame",     out surfFameDelta, true, true);
            BarDivider(rowG);
            surfDomText    = YieldChip(rowG, C("#5cc47c"),       "支配",   "0", null,     out _unusedDelta, false);
            surfProdText   = YieldChip(rowG, UITheme.Production, "生産力", "0", "hammer", out _unusedDelta, false);
            surfHappyText  = YieldChip(rowG, UITheme.Happy,      "幸福度", "0", null,     out _unusedDelta, false);
            surfSettleText = YieldChip(rowG, UITheme.Influence,  "拠点",   "0/0", null,   out _unusedDelta, false);
            AddTooltip(surfDpText.transform.parent.gameObject, "魔力点（DP）。括弧は地上から毎ターン入る量");
            AddTooltip(surfRpText.transform.parent.gameObject, "研究点。括弧は地上から毎ターン入る量");
            AddTooltip(surfMatText.transform.parent.gameObject, "素材。括弧は地上から毎ターン入る量");
            AddTooltip(surfFameText.transform.parent.gameObject, "名声。括弧は地上から毎ターン入る量");
            AddTooltip(surfDomText.transform.parent.gameObject, "支配している領域の数／盤の領域の数");
            AddTooltip(surfProdText.transform.parent.gameObject, "拠点の生産力の合計（建造物と軍団を作る速さ）");
            AddTooltip(surfHappyText.transform.parent.gameObject, "拠点の幸福度の合計。マイナスだと産出が落ちる");
            AddTooltip(surfSettleText.transform.parent.gameObject, "");
            surfSettleTip = surfSettleText.transform.parent.GetComponent<UITooltipTrigger>();
        }
        // 📜 状況の一行（時代・政策・他の魔王…）。⚠ 長いので**1行で切って、触れると全文**。
        //   以前は2行の細かい文字が帯に詰まっていて、どこを読めばいいか分からなかった。
        {
            var st = Panel(panel, "SurfStatus", new Color(0, 0, 0, 0));   // ⚠ 透明＝盤のホイール判定では素通し
            Place(st.rectTransform, pad, 56, FS_W - pad * 2, 22);
            surfaceRivalText = Text(st.rectTransform, "", 12.5f, C("#c9c2dc"), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            surfaceRivalText.enableWordWrapping = false;
            surfaceRivalText.overflowMode = TextOverflowModes.Ellipsis;
            StretchFull(surfaceRivalText.rectTransform);
            AddTooltip(st.gameObject, "");
            surfStatusTip = st.GetComponent<UITooltipTrigger>();
        }

        // ── 📋 左端のメニュー（押すとその機能の窓が開く／もう一度押すと閉じる）──
        // 🎨 **絵の柱にした**（UI刷新 B-1・地上ぶん）。幅 74×62 → 44×44。
        //   ⚠ 並びも index も変えていない ―― `switch (surfaceMenuTab)` と `wt` がこの順に依存している。
        float railX = 12f, railY = barH + 12f, railW = 44f, itemH = 44f;
        surfaceMenuBtns.Clear(); surfaceTabBtns.Clear(); boardOnlyLabels.Clear();
        // ⚠⚠ **この並びの index が `switch (surfaceMenuTab)` と `wt` に対応している。**
        //   途中に足したら3箇所とも直すこと（→ [[legion-system]] で index ずれを踏んでいる）。
        string[] mNames = { "領域", "生産", "勢力", "眷属", "軍団", "ツリー", "政策", "属性", "外交", "時代", "勝利", "物語" };
        string[] mTips =
        {
            "選択中のタイルの詳細と操作（施設・拠点・砦・進軍）",
            "拠点で何を作るか。建造物と配下を待ち行列に積み、余った魔力点で順番を追い越す",
            "自分の拠点と他の魔王の一覧。押すとその場所へ飛ぶ",
            "眷属の編成と進軍先の指定",
            "軍団の生産と進軍（拠点で造って盤に並べる）",
            "地上研究のツリー",
            "政体と政策スロット（カードを差し替えて方針を変える）",
            "属性ツリー（偉業で得た点を恒久強化に）",
            "威名・独立勢力・交易路・他魔王との盟約",
            "時代の進行・偉業・誓約・災厄",
            "4本の勝ち筋と、いま誰が抜け出しているか",
            "物語の事件と、周回を越えて持ち込む形見",
        };
        for (int i = 0; i < mNames.Length; i++)
        {
            int mi = i;
            var b = Panel(panel, "SMenu_" + i, PANEL2);
            // 🏷️ 段G：迷宮の列と同じく**絵の下に名前**を添える（覚えなくても読める）。そのぶん1段を 50→66 に
            float iy = railY + i * (itemH + 22);
            Place(b.rectTransform, railX, iy, railW, itemH); Outline(b, LINE2); SkinPanel(b);
            {
                var lab = Text(panel, mNames[i], 12.5f, MUTED, TextAlignmentOptions.Top);
                lab.raycastTarget = false; lab.enableWordWrapping = false;
                Place(lab.rectTransform, railX - 8, iy + itemH + 1, railW + 16, 19);
            }
            var sp = IconFactory.Get(mNames[i]);
            if (sp != null)
            {
                // ⚠ 絵は raycast を吸わない（吸うとボタンが押せなくなる）
                var ic = new GameObject("Ic", typeof(RectTransform)).AddComponent<Image>();
                ic.rectTransform.SetParent(b.rectTransform, false);
                ic.sprite = sp; ic.color = TEXT; ic.raycastTarget = false;
                ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                ic.rectTransform.anchoredPosition = Vector2.zero;
                ic.rectTransform.sizeDelta = new Vector2(itemH * 0.66f, itemH * 0.66f);
            }
            else
            {
                // ⚠ 絵が無い名前は文字に落ちる（描き忘れても画面が壊れない）
                var lab = Text(b.rectTransform, mNames[i], 11f, TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
                StretchFull(lab.rectTransform);
            }
            var bt = b.gameObject.AddComponent<Button>(); bt.targetGraphic = b;
            bt.onClick.AddListener(() => { surfaceMenuTab = (surfaceMenuTab == mi) ? -1 : mi; RefreshSurfacePanel(); });
            // ⚠ 説明は `IconCatalog` に一本化する（`mTips` と2か所に置かない）
            AddTooltip(b.gameObject, IconCatalog.Tip(mNames[mi]));
            surfaceMenuBtns.Add(b);
        }

        // ── 🪟 メニューから開く窓（1つずつ・閉じられる）──
        // ⚠ 柱が細くなったぶん、窓を少し広く取れる
        float winX = railX + railW + 10f, winY = railY, winW = 660f, winH = FS_H - winY - 120f;
        surfaceWindow = Panel(panel, "SurfaceWindow", PANEL);
        Place(surfaceWindow.rectTransform, winX, winY, winW, winH); Outline(surfaceWindow, LINE2); SkinPanel(surfaceWindow);
        surfaceWindowTitle = Text(surfaceWindow.rectTransform, "", 13.5f, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(surfaceWindowTitle.rectTransform, 14, 10, winW - 60, 18);
        var wclose = PrimaryButton(surfaceWindow, "×", PANEL2, TEXT, () => { surfaceMenuTab = -1; RefreshSurfacePanel(); });
        Place((RectTransform)wclose.transform, winW - 38, 8, 26, 24);
        float cw = winW - 28f, cy = 36f, ch = winH - cy - 12f;
        regionListContainer = MakeVScroll(surfaceWindow, 14, cy, cw, ch); regionListW = cw;
        statusContainer = MakeVScroll(surfaceWindow, 14, cy, cw, ch); statusW = cw;
        kinListContainer = MakeVScroll(surfaceWindow, 14, cy, cw, ch); kinListW = cw;
        legionContainer = MakeVScroll(surfaceWindow, 14, cy, cw, ch); legionW = cw;
        prodContainer = MakeVScroll(surfaceWindow, 14, cy, cw, ch); prodW = cw;   // 🔨 K-1 生産
        surfaceTreeRoot = MakeVScroll(surfaceWindow, 14, cy, cw, ch); surfaceTreeW = cw;
        policyContainer = MakeVScroll(surfaceWindow, 14, cy, cw, ch); policyW = cw;
        attrContainer = MakeVScroll(surfaceWindow, 14, cy, cw, ch); attrW = cw;
        eraContainer = MakeVScroll(surfaceWindow, 14, cy, cw, ch); eraW = cw;
        victoryContainer = MakeVScroll(surfaceWindow, 14, cy, cw, ch); victoryW = cw;
        diploContainer = MakeVScroll(surfaceWindow, 14, cy, cw, ch); diploW = cw;
        storyContainer = MakeVScroll(surfaceWindow, 14, cy, cw, ch); storyW = cw;

        // ── 🏷️ 選択中タイルの小さな帯（窓を開かなくても何を選んだか分かる）──
        // 🗺️ 段B：**タイルの札**として右下（大ボタンの上）へ移した。ユニットの命令は左下の「ユニットの札」が持つ。
        //   ⚠ ここに残すのはタイルの操作だけ（拠点を築く・斥候を出す・詳細）。→ [[GameUIManager.Units]]
        float tileW = 480f, tileH = 132f;
        surfaceBanner = Panel(panel, "SurfaceBanner", PANEL);
        Place(surfaceBanner.rectTransform, FS_W - pad - tileW, FS_H - pad - (SurfHintH + SurfBigH + SurfEndH + 26f) - 12f - tileH, tileW, tileH);
        Outline(surfaceBanner, LINE2); SkinPanel(surfaceBanner);
        surfaceBannerText = Text(surfaceBanner.rectTransform, "", 12f, TEXT, TextAlignmentOptions.TopLeft);
        Place(surfaceBannerText.rectTransform, 14, 8, tileW - 120, 76);
        var openDetail = PrimaryButton(surfaceBanner, "詳細", PANEL2, GOLD, () => { surfaceMenuTab = 0; RefreshSurfacePanel(); });
        Place((RectTransform)openDetail.transform, tileW - 100, 10, 86, 28);
        bannerActions = NewRect("BannerActions", surfaceBanner.rectTransform);
        Place(bannerActions, 14, 92, tileW - 28, 30);

        // 🕹️ ユニットの札と、命令待ちの列（左下）
        BuildUnitCard(panel, winX);

        // 💰 地上の画面にも資源を出す（→ [[GameUIManager.Harvest]]）。
        //    ⚠ 地上モードでは迷宮 Canvas ごと畳むので、上部バーのチップは見えていない。
        BuildSurfaceResChips(panel, barH, pad);
        BuildSurfaceTreePanel(panel);

        RefreshSurfacePanel();
        surfacePanel.SetActive(false);
    }

    /// <summary>
    /// 🖱️ ポインタが**地上UIの不透明な板の上**にあるか（＝盤にホイールを渡してはいけないか）。
    ///
    /// ⚠⚠ `EventSystem.IsPointerOverGameObject()` **だけでは足りない**。
    ///   `GraphicRaycaster` は `Graphic.depth == -1`（まだ描画バッチに乗っていない）を飛ばすので、
    ///   開いた直後のパネルは「UIの上」と判定されない。実測でツリーを開いても中央のヒットが 0 だった。
    ///   ここは**矩形で直接見る**。描画状態に依存しないので、開いた瞬間から確実に効く。
    ///
    /// 走査するのは `SurfaceInner` の**直下の子だけ**（帯・左メニュー・開いているパネル）。
    /// 透明な器（alpha 0）は素通しにしたいので、**背景が見える板だけ**を数える。
    /// </summary>
    public bool PointerOverSurfaceUI(Vector2 screenPos)
    {
        if (surfaceInnerRt == null || !surfaceInnerRt.gameObject.activeInHierarchy) return false;
        // 🖥️ 地上ツリーは全画面なので、開いているあいだは盤に何も渡さない
        if (surfaceTreePanel != null && surfaceTreePanel.activeInHierarchy) return true;
        for (int i = 0; i < surfaceInnerRt.childCount; i++)
        {
            var c = surfaceInnerRt.GetChild(i) as RectTransform;
            if (c == null || !c.gameObject.activeSelf) continue;
            var img = c.GetComponent<Image>();
            if (img == null || !img.enabled || img.color.a <= 0.05f) continue;   // 透明な器は素通し
            if (RectTransformUtility.RectangleContainsScreenPoint(c, screenPos, null)) return true;
        }
        return false;
    }

    /// <summary>
    /// 🌳 地上ツリーの全画面パネル（G-4）。地上研究＋業の研究を、迷宮ツリーと**同じ絵**で出す。
    /// ⚠ 620px の窓に入れていたのが元の姿だったが、70ノードのツリーは幅2,000pxを超える。
    ///   狭い窓に押し込むと3列のカード一覧になり、**前提のつながりが一切見えない**（＝ツリーではない）。
    /// </summary>
    private void BuildSurfaceTreePanel(Image parent)
    {
        // 🖥️ **全画面**（→ [[GameUIManager.FullScreen]]）。⚠ 親は地上の**外側の器**（SurfaceInner は 1820×1020 の窓の大きさ）
        float FW, FH;
        var p = FullPanel((RectTransform)surfacePanel.transform, "SurfaceTreePanel", out FW, out FH);
        surfaceTreePanel = p.gameObject;
        Image chipHost;
        BuildFullHeader(p, FW, "地上ツリー", "国のしくみを育てる木／習熟で二段目へ",
            () => surfaceTreePanel.SetActive(false), out surfaceTreeTabHost, out chipHost);
        surfaceTreeChips = TreeChips(chipHost);

        float pad = 28f;
        surfaceTreeGraphW = FW - pad * 2;
        surfaceTreeGraph = MakeScroll2D(p, pad, FullHdrH + 12f, surfaceTreeGraphW, FH - FullHdrH - 12f - pad);
        surfaceTreePanel.SetActive(false);
    }

    private void OpenSurfaceTree()
    {
        if (surfaceTreePanel == null) return;
        surfaceMenuTab = -1;                    // 左の窓は畳む（全画面と二重に出さない）
        surfaceTreePanel.SetActive(true);
        surfaceTreePanel.transform.SetAsLastSibling();
        RefreshSurfaceTree();
        RefreshSurfacePanel();
    }

    private readonly List<Image> surfaceSizeBtns = new List<Image>();
    private void RefreshSurfaceSizeBtns()
    {
        var sizes = new[] { SurfaceGen.Size.Tiny, SurfaceGen.Size.Small, SurfaceGen.Size.Medium, SurfaceGen.Size.Large };
        for (int i = 0; i < surfaceSizeBtns.Count && i < 4; i++) SetSel(surfaceSizeBtns[i], SurfaceMap.MapSize == sizes[i]);
    }

    private void RefreshThemeEffect()
    {
        if (spaceEffectText == null) return;
        SetTxt(spaceEffectText, "→ " + DungeonTheme.SpaceEffect((DungeonGenerator.SpaceType)selSpace));
    }

    // 🌍 地上モード：迷宮のカメラ・タイル・下部ツールバーを畳んで、盤だけの画面にする。
    //    以前は全画面パネルの背後に迷宮が透けていて「別のレイヤーに来た」感じが出なかった。
    // 🌍 地上モードのあいだ畳んだ迷宮側のUI（戻すときに元へ）

    /// <summary>
    /// ⏳ フェーズが変わったら**画面ごと切り替える**（`DungeonTurnManager` から呼ばれる）。
    /// 前半＝迷宮、後半＝地上。プレイヤーが自分で行き来する必要はない（『地上』ボタンは廃止）。
    /// </summary>
    public void OnPhaseChanged()
    {
        if (turn == null) return;
        // 🎬 幕間が流れている最中にフェーズがまた変わったら、その幕間は畳んで切り替えを済ませてから比べる。
        //   ⚠ 畳まないと、幕間の最後の切り替えが**後から**来て、迷宮の準備中に地上の画面が残る（通しプレイで発見）。
        if (InterludePlaying) FinishInterludeNow();
        bool wantSurface = turn.IsSurfacePhase;
        // 🎬 フェーズが入れ替わるときだけ幕間を挟む（画面の切り替えは幕間の中で行う → [[GameUIManager.Interlude]]）
        if (surfaceModeOn != wantSurface) { if (!TryPlayInterlude(wantSurface)) SetSurfaceMode(wantSurface); }
        else RefreshSurfacePanel();
    }

    private void SetSurfaceMode(bool on)
    {
        if (surfacePanel == null) return;
        surfaceModeOn = on;                 // ※先に立てる（RefreshSurfacePanel がこの値で盤の表示を決めるため）
        surfacePanel.SetActive(on);
        // 🌳 全画面のツリーは持ち越さない（前回開いたまま地上に入ると、いきなり盤が隠れる）
        if (surfaceTreePanel != null) surfaceTreePanel.SetActive(false);

        // 🗂️ 迷宮側のUIは **Canvasごと** 止める。
        //    1枚ずつ畳む方式だと、地上モード中にあとから開くパネル（生成パネルなど）を取りこぼして
        //    盤の上に居座ってしまう（実測で発生）。Canvasを切れば、増えたパネルも自動的に付いてくる。
        if (dungeonCanvas != null) dungeonCanvas.enabled = !on;
        HideTooltip();
        // 🔊 地上と迷宮で曲を変える（場面が切り替わったことが音でも分かる）
        if (GameSetup.Started)
            SoundSystem.PlayBgm(on ? SoundSystem.Bgm.Surface
                : (turn != null && !turn.IsPreparePhase ? SoundSystem.Bgm.Battle : SoundSystem.Bgm.Prepare));

        // 🎥 迷宮のカメラを止めて、地上のカメラに渡す。
        //    ⚠ 迷宮の GameObject は**消さない**（enabled を落とすだけ）ので、階層・配置・個体・進行は
        //      そのままメモリに残る＝戻ったときに完全に元通りになる。畳む＝壊す ではない。
        //    ⚠ `Camera.main` 1台だけを見ると取りこぼす（タグ付けや2台目のカメラ次第）。**有効なカメラを全部畳む**。
        if (on)
        {
            if (surfaceView == null)
            {
                surfaceView = SurfaceView.Create(uiFont);
                // 🕹️ 段B：ユニットを選ぶ → 塗られたマスで命令（→ [[GameUIManager.Units]]）
                surfaceView.onPick = OnBoardPick;
                surfaceView.onHover = OnBoardHover;
                // 🖱️ 右クリック：攻める場面ならそれをやめる／でなければ選択を外す
                surfaceView.onRightPick = () =>
                {
                    if (unitAttackMode) { unitAttackMode = false; surfaceActionMsg = ""; RefreshSurfacePanel(); }
                    else if (UnitExists(selUnit)) ClearUnitSelection();
                };
            }
            surfaceView.PlayEnemyReplay();   // ⏭️ 前ターンに敵軍がどう動いたかを見せてから操作させる
            // 未選択・未発見・盤を作り直した直後は、必ず**迷宮のあるタイル**から始める
            if (selectedRegionId < 0 || selectedRegionId >= SurfaceMap.Count
                || !SurfaceMap.IsDiscovered(selectedRegionId)) selectedRegionId = SurfaceMap.IndexOfCenter();
            // ⚠⚠ ここで `foldedCameras.Clear()` を**してはいけない**。
            //   集め直すループは「いま有効なカメラ」だけを拾うが、**さっき自分が畳んだ本カメラは
            //   もう有効ではないので拾えない**。つまり2回目に地上へ入った瞬間に記録が空になり、
            //   `ターンを終える` で戻すべきカメラを失う ＝ **どのカメラも点かない**
            //   （Game ビューが `Display 1 / No cameras rendering` になる）。
            //   実測：地上へ folded=1 → もう一度地上へ folded=0 → 終えると Main Camera=off のまま。
            //   ⚠ 地上の大ボタン（`GoToAdvice("surface:…")`）は**地上に居るまま**押されるので、
            //     この2回目は例外ではなく**普通の操作**として起きる。
            //   畳んだ記録は**足すだけ**にする（あとから増えたカメラも拾えるように毎回まわす）。
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (c == surfaceView.cam || !c.enabled) continue;
                c.enabled = false;
                if (!foldedCameras.Contains(c)) foldedCameras.Add(c);
            }
            surfaceView.SetActiveView(true);
            surfaceView.FitToBoard();
            surfaceView.SetSelected(selectedRegionId);
            surfaceView.CenterOn(selectedRegionId);
            surfacePanel.transform.SetAsLastSibling();
            RefreshSurfacePanel();
        }
        else
        {
            if (surfaceView != null) surfaceView.SetActiveView(false);
            foreach (var c in foldedCameras) if (c != null) c.enabled = true;
            foldedCameras.Clear();
        }
        // フロアタブは階層が2つ以上あるときだけ出す（畳む前の状態に関係なく決め直す）
        if (!on && floorTabsPanel != null)
            floorTabsPanel.SetActive(floorMgr != null && floorMgr.BuiltFloorCount > 1);
    }

    private void RefreshSurfacePanel()
    {
        // ⚔️ 遠征の窓は地上と一緒に描き直す（層が進む・体数が減るので、開きっぱなしだと古くなる）
        RefreshExpeditionWindow();
        if (surfacePanel == null || kinListContainer == null) return;
        if (surfaceView != null)
        {
            // 🐾 選んでいるユニットの歩ける所（緑）・攻められる所（赤）を盤に出す（→ [[GameUIManager.Units]]）
            ApplyUnitOverlays();   // 👑 眷属の位置や支配が変わっていれば盤も描き直す
        }
        for (int i = 0; i < surfaceTabBtns.Count; i++) SetSel(surfaceTabBtns[i], i == surfaceTab);
        // 🌍 盤は SurfaceView（ワールド空間）が描くので、uGUIのヘクス盤は畳んだまま使わない
        if (hexMapRoot != null) hexMapRoot.parent.gameObject.SetActive(false);
        // ⚠ どのメニューを開いていても地上カメラは**止めない**。止めると迷宮のカメラも止まったままで
        //    有効なカメラが0台になり、前のフレーム（迷宮）が残って見える。
        if (surfaceView != null) { surfaceView.SetActiveView(surfaceModeOn); surfaceView.MarkDirty(); }

        for (int i = 0; i < surfaceMenuBtns.Count; i++) SetSel(surfaceMenuBtns[i], i == surfaceMenuTab);
        bool open = surfaceMenuTab >= 0;
        if (surfaceWindow != null) surfaceWindow.gameObject.SetActive(open);
        // 窓を開いているあいだは左が埋まるので、注目タイルを右寄りに置く
        if (surfaceView != null) surfaceView.FocusOffsetX = open ? -0.19f : 0f;
        // ⚠ index は `mNames` の並びと `switch (surfaceMenuTab)` に対応している。ここも一緒に直すこと。
        if (regionListContainer != null) regionListContainer.parent.gameObject.SetActive(surfaceMenuTab == 0);
        if (prodContainer != null) prodContainer.parent.gameObject.SetActive(surfaceMenuTab == 1);
        if (statusContainer != null) statusContainer.parent.gameObject.SetActive(surfaceMenuTab == 2);
        if (kinListContainer != null) kinListContainer.parent.gameObject.SetActive(surfaceMenuTab == 3);
        if (legionContainer != null) legionContainer.parent.gameObject.SetActive(surfaceMenuTab == 4);
        if (surfaceTreeRoot != null) surfaceTreeRoot.parent.gameObject.SetActive(surfaceMenuTab == 5);
        if (policyContainer != null) policyContainer.parent.gameObject.SetActive(surfaceMenuTab == 6);
        if (attrContainer != null) attrContainer.parent.gameObject.SetActive(surfaceMenuTab == 7);
        if (diploContainer != null) diploContainer.parent.gameObject.SetActive(surfaceMenuTab == 8);
        if (eraContainer != null) eraContainer.parent.gameObject.SetActive(surfaceMenuTab == 9);
        if (victoryContainer != null) victoryContainer.parent.gameObject.SetActive(surfaceMenuTab == 10);
        if (storyContainer != null) storyContainer.parent.gameObject.SetActive(surfaceMenuTab == 11);

        if (open && surfaceWindowTitle != null)
        {
            string[] wt = { "選択中の領域", "生産", "勢力（押すとその場所へ飛ぶ）", "眷属", "軍団", "地上研究ツリー", "政体と政策", "属性ツリー", "外交", "時代", "勝利", "物語と形見" };
            SetTxt(surfaceWindowTitle, "◆ " + wt[Mathf.Clamp(surfaceMenuTab, 0, wt.Length - 1)]);
        }
        // 🗂️ 窓を開いたまま別の入口を押したら、中身を縦に滑り込ませる（入口の列が縦なので）
        {
            int sdir = TabChanged("surfwin", surfaceMenuTab);
            if (sdir != 0)
            {
                RectTransform[] boxes = { regionListContainer, prodContainer, statusContainer, kinListContainer, legionContainer, surfaceTreeRoot,
                                          policyContainer, attrContainer, diploContainer, eraContainer, victoryContainer, storyContainer };
                var box = boxes[Mathf.Clamp(surfaceMenuTab, 0, boxes.Length - 1)];
                if (box != null) SlideWhole(box.parent as RectTransform, sdir, true);
            }
        }
        switch (surfaceMenuTab)
        {
            case 0: RefreshRegionDetail(); break;
            case 1: RefreshProductionPanel(); break;   // 🔨 K-1
            case 2: RefreshSurfaceStatus(); break;
            case 3: RefreshKinList(); break;
            case 4: RefreshLegionPanel(); break;
            case 5: RefreshSurfaceTreeGate(); break;
            case 6: RefreshPolicyPanel(); break;
            case 7: RefreshAttrPanel(); break;
            case 8: RefreshDiploPanel(); break;
            case 9: RefreshEraPanel(); break;
            case 10: RefreshVictoryPanel(); break;
            case 11: RefreshStoryPanel(); break;
        }
        RefreshSurfaceBanner();
        RefreshUnitCard();
        RefreshSurfaceHeader();
    }

    /// <summary>🏷️ 選択中タイルの小さな帯。窓を開かなくても「いま何を選んでいるか」が分かるようにする。</summary>
    private void RefreshSurfaceBanner()
    {
        if (surfaceBannerText == null) return;
        if (selectedRegionId < 0 || !SurfaceMap.IsDiscovered(selectedRegionId))
        {
            SetTxt(surfaceBannerText, "<color=#9c95b4>タイルを押すと、ここにその土地の様子が出ます。ユニットは盤か左下の列から選びます。</color>");
            if (bannerActions != null)
                for (int i = bannerActions.childCount - 1; i >= 0; i--)
            { var old_ = bannerActions.GetChild(i).gameObject; old_.SetActive(false); Destroy(old_); }   // ⚠ Destroy は遅延する。先に黙らせないと、同じフレーム内に作り直したとき古いボタンが残って押せてしまう
            return;
        }
        var r = SurfaceMap.Get(selectedRegionId);
        var sb = new System.Text.StringBuilder();
        sb.Append("<color=" + SurfaceMap.OwnerColor(r.owner) + ">[" + SurfaceMap.OwnerName(r.owner) + "]</color> ");
        sb.Append("<b>" + r.name + "</b>  <size=90%><color=#9c95b4>" + SurfaceMap.TerrainName(r.terrain)
            + "（踏破" + (SurfaceMap.IsPassable(r) ? SurfaceMap.MoveCost(r).ToString() : "不可") + "）</color></size>");
        if (r.settle == SurfaceMap.Settle.City) sb.Append(" <color=#e3c34a>都市</color>");
        else if (r.settle == SurfaceMap.Settle.Town) sb.Append(" <color=#8cb8e6>拠点〈" + SettlementSystem.FocusName(r.focus) + "〉</color>");
        sb.Append("\n" + (r.owned ? "守り <color=#5cc47c>" : "防衛 <color=#e05a5a>") + SurfaceMap.DefenseOf(r.id) + "</color>");
        if (r.resource != SurfaceMap.Resource.None)
            sb.Append("　<color=#e3c34a>" + SurfaceMap.ResourceName(r.resource) + "</color>"
                + (r.resourceAssigned ? "<size=88%><color=#5cc47c>[割当]</color></size>" : "<size=88%><color=#6f6889>[枠外]</color></size>"));
        if (r.river) sb.Append("　<color=#5aa8e0>川</color>");
        if (r.wonderIndex >= 0) sb.Append("　<color=#ffd24a>遺産〈" + WonderCatalog.Get(r.wonderIndex).jpName + "〉</color>");
        if (r.settle != SurfaceMap.Settle.None)
        {
            int net = SettlementSystem.NetHappy(r.id);
            sb.Append("　人口 <color=#e3c34a>" + r.pop + "</color>　"
                + (net < 0 ? "<color=#e05a5a>不満" + (-net) + "（産出" + (net * 5) + "%）</color>" : "<color=#5cc47c>幸福+" + net + "</color>"));
        }
        else if (r.owned && SettlementSystem.SettlementOf(r.id) < 0) sb.Append("　<color=#e08a3c>未編入の辺境</color>");
        var ea = EnemyForce.At(r.id);
        if (ea != null)
            sb.Append("　<color=" + EnemyForce.ColorOf(ea) + ">◆" + ea.name + " 戦力" + ea.power.ToString("0") + "</color>");
        // ⚠ ユニットの札が出ているときは結果は札の側に出す（同じ文が2か所に並ばないように）
        if (!string.IsNullOrEmpty(surfaceActionMsg) && !UnitExists(selUnit)) sb.Append("\n" + surfaceActionMsg);
        SetTxt(surfaceBannerText, sb.ToString());
        RefreshBannerActions(r);
    }

    /// <summary>
    /// 🗺️ タイルの札に、**タイルの操作だけ**を並べる（斥候を出す・拠点を築く）。
    /// ⚠ 段B で、ユニットの命令（移動・攻撃・進軍・守る・鍛錬・軍団の選択/解散）は
    ///   ユニットの札へ移した（→ [[GameUIManager.Units]]）。前はここがタイル基準で全部を混ぜていて、
    ///   動かすユニットも `ActiveKin()` が勝手に選んでいた。
    /// </summary>
    private void RefreshBannerActions(SurfaceMap.Region r)
    {
        if (bannerActions == null) return;
        for (int i = bannerActions.childCount - 1; i >= 0; i--)
            { var old_ = bannerActions.GetChild(i).gameObject; old_.SetActive(false); Destroy(old_); }   // ⚠ Destroy は遅延する。先に黙らせないと、同じフレーム内に作り直したとき古いボタンが残って押せてしまう

        float x = 0f, h = 30f;
        int rid = r.id;

        // 🔭 斥候（S4）：安く速く、地形を無視して霧を剥がす専門職
        string scWhy;
        if (ScoutSystem.CanSpawn(rid, out scWhy))
        {
            var b = PrimaryButton(bannerActions, "斥候を出す（" + ScoutSystem.Cost + "DP）", PANEL2, C("#8cb8e6"), () =>
            {
                if (ScoutSystem.TrySpawn(rid))
                {
                    surfaceActionMsg = "<color=#8cb8e6>斥候を送り出しました（移動力" + ScoutSystem.Movement + "・視界" + ScoutSystem.Vision + "・戦えません）。</color>";
                    var ns = ScoutSystem.At(rid);
                    if (ns != null) { SelectUnit(new UnitOrders.Unit(UnitOrders.Kind.Scout, ns.id), false); return; }
                }
                RefreshSurfacePanel();
            });
            Place((RectTransform)b.transform, x, 0, 178, h); x += 186;
            AddTooltip(b.gameObject, "斥候は森や荒地の重さを無視して動き、周囲" + ScoutSystem.Vision + "タイルを見通します。\n戦えないので敵領には入れません。上限 " + ScoutSystem.Limit + "体。");
        }

        string why;
        if (SettlementSystem.CanFound(rid, out why))
        {
            var b = PrimaryButton(bannerActions, "拠点を築く", PANEL2, C("#8cb8e6"), () =>
            {
                if (SettlementSystem.TryFound(rid))
                {
                    surfaceActionMsg = "<color=#5cc47c>拠点を築きました。周囲のタイルが自領になります。</color>";
                    if (surfaceView != null) surfaceView.PopText(rid, "拠点を築いた", "#8cb8e6");
                }
                RefreshSurfacePanel();
                if (surfaceView != null) surfaceView.MarkDirty();
            });
            Place((RectTransform)b.transform, x, 0, 140, h); x += 148;
            AddTooltip(b.gameObject, "拠点を築くと周囲1タイルが自領になり、人口が増えると版図が広がります（都市に昇格すると2タイル）。");
        }
        else if (!string.IsNullOrEmpty(why) && r.owned)
        {
            var t = Text(bannerActions, "<color=#6f6889>拠点：" + why + "</color>", 11f, FAINT, TextAlignmentOptions.Left);
            Place(t.rectTransform, x, 7, 420 - x, 18);
        }
    }

    /// <summary>📖 物語：起きている事件の選択と、周回を越えて持ち込む形見（C7）。</summary>
    private void RefreshStoryPanel()
    {
        var c = storyContainer; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = storyW, y = 0f;

        // ── 起きている事件 ──
        if (NarrativeSystem.HasPending)
        {
            var ev = NarrativeSystem.Event(NarrativeSystem.Pending);
            var head = Panel(c, "EvHead", CARD);
            float bodyH = 76f;
            Place(head.rectTransform, 0, y, w - 6, bodyH); Outline(head, GOLD);
            var t1 = Text(head.rectTransform, "<color=#ffd24a>◆ " + ev.title + "</color>", 14, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(t1.rectTransform, 12, 8, w - 30, 20);
            var t2 = Text(head.rectTransform, "<size=95%><color=#c9c2e0>" + ev.body + "</color></size>", 12, MUTED, TextAlignmentOptions.TopLeft);
            Place(t2.rectTransform, 12, 30, w - 30, 42);
            y += bodyH + 8;
            for (int i = 0; i < ev.choices.Length; i++)
            {
                int ci = i; var ch = ev.choices[i];
                var card = Panel(c, "Ch_" + i, CARD);
                Place(card.rectTransform, 0, y, w - 6, 46); Outline(card, LINE2);
                var n1 = Text(card.rectTransform, "<color=#e3c34a>" + ch.label + "</color>", 12.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(n1.rectTransform, 12, 6, w - 30, 18);
                var n2 = Text(card.rectTransform, "<size=92%><color=#9c95b4>" + ch.desc + "</color></size>", 11f, MUTED, TextAlignmentOptions.TopLeft);
                Place(n2.rectTransform, 12, 25, w - 30, 16);
                var bt = card.gameObject.AddComponent<Button>(); bt.targetGraphic = card;
                bt.onClick.AddListener(() => { if (NarrativeSystem.Choose(ci)) RefreshSurfacePanel(); });
                y += 50;
            }
            y += 10;
        }
        else
        {
            var n = Text(c, "<color=#6f6889>いまは何も起きていません。ターンを重ねると、状況に応じた事件が起こります。</color>",
                11.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(n.rectTransform, 8, y, w - 16, 20); y += 28;
        }

        // ── 形見 ──
        var mh = Text(c, "◆ 形見（周回を越えて持ち込む・" + NarrativeSystem.Slots + "枠）　<size=88%><color=#9c95b4>解禁 "
            + NarrativeSystem.UnlockedCount + "/" + NarrativeSystem.MementoCount + "</color></size>",
            12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(mh.rectTransform, 4, y, w - 8, 18); y += 22;
        for (int s = 0; s < NarrativeSystem.Slots; s++)
        {
            int si = s; int cur = NarrativeSystem.SlotOf(s);
            var row = Panel(c, "MS_" + s, PANEL2);
            Place(row.rectTransform, 0, y, w - 6, 30); Outline(row, LINE2);
            var t = Text(row.rectTransform, "枠" + (s + 1) + "：" + (cur < 0 ? "<color=#6f6889>空</color>"
                : "<color=" + NarrativeSystem.Memento(cur).colorHex + ">" + NarrativeSystem.Memento(cur).jpName + "</color>"),
                12f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(t.rectTransform, 12, 7, w - 110, 18);
            if (cur >= 0)
            {
                var cb = PrimaryButton(row, "外す", PANEL, C("#e05a5a"), () => { NarrativeSystem.TryEquip(si, -1); RefreshSurfacePanel(); });
                Place((RectTransform)cb.transform, w - 96, 3, 82, 24);
            }
            y += 34;
        }
        y += 6;
        for (int i = 0; i < NarrativeSystem.MementoCount; i++)
        {
            int mi = i; var md = NarrativeSystem.Memento(i);
            bool got = NarrativeSystem.IsUnlocked(i);
            bool on = NarrativeSystem.Equipped(i);
            var card = Panel(c, "M_" + i, on ? PANEL2 : CARD);
            Place(card.rectTransform, 0, y, w - 6, 44); Outline(card, on ? C(md.colorHex) : (got ? LINE2 : LINE));
            var n1 = Text(card.rectTransform, (on ? "◆ " : "") + "<color=" + (got ? md.colorHex : "#4a4560") + ">" + md.jpName + "</color>"
                + "　<size=90%><color=#9c95b4>" + md.desc + "</color></size>", 12f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(n1.rectTransform, 12, 5, w - 130, 18);
            var n2 = Text(card.rectTransform, "<size=88%><color=" + (got ? "#5cc47c" : "#6f6889") + ">"
                + (got ? "解禁済み" : "条件：" + md.unlock) + "</color></size>", 10.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(n2.rectTransform, 12, 24, w - 130, 16);
            if (got && !on)
            {
                // ⚠ 枠は実績で増える（2→3）。ボタンを固定で2つ描かない。→ [[Achievements]]
                int ns = NarrativeSystem.Slots;
                for (int s = 0; s < ns; s++)
                {
                    int si2 = s;
                    var eb = PrimaryButton(card, "枠" + (s + 1), PANEL2, C(md.colorHex),
                        () => { NarrativeSystem.TryEquip(si2, mi); RefreshSurfacePanel(); });
                    Place((RectTransform)eb.transform, w - 12 - (ns - s) * 56, 4, 52, 18);
                }
            }
            y += 48;
        }
        c.sizeDelta = new Vector2(0f, Mathf.Max(y + 8, 80));
    }

    /// <summary>🕊️ 外交：威名・独立勢力・交易路・他魔王との盟約（C5）。</summary>
    private void RefreshDiploPanel()
    {
        var c = diploContainer; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = diploW, y = 0f;

        var head = Panel(c, "DHead", CARD);
        Place(head.rectTransform, 0, y, w - 6, 46); Outline(head, C("#57c3ab"));
        var h1 = Text(head.rectTransform, "威名 <color=#57c3ab>" + DiplomacySystem.Influence + "</color>"
            + "　<size=90%><color=#9c95b4>毎ターン +" + DiplomacySystem.IncomePerTurn + "</color></size>", 13.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(h1.rectTransform, 12, 6, w - 30, 20);
        var h2 = Text(head.rectTransform, "<size=90%><color=#6f6889>名声とは別物。威名は他の勢力を動かす力で、冒険者の強さには効きません。</color></size>",
            11f, FAINT, TextAlignmentOptions.TopLeft);
        Place(h2.rectTransform, 12, 26, w - 30, 16);
        y += 54;

        // ── 他魔王との関係 ──
        // ⚔️ **いちばん上に置く**（敵も経営する・段④／百年の決着）。
        //   ⚠ ジャーナルの赤い帯（他の魔王の儀）は「▶ 外交で見る」でここへ飛ぶ。下の方に埋もれていると
        //     飛んでも見つからない。**bot の迷宮の中身と前のターンの手も見せる** ―― 見えない所で強くなるのは理不尽。
        var rh = Text(c, "◆ 他の魔王" + (DiplomacySystem.WarWeariness > 0
            ? "　<color=#e05a5a>厭戦 全拠点に不満+" + DiplomacySystem.WarWeariness + "</color>" : ""),
            12.5f, CRIMSON, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(rh.rectTransform, 4, y, w - 8, 18); y += 22;
        for (int i = 0; i < RivalLords.Count; i++)
        {
            int rid2 = i; var rv = RivalLords.Get(i);
            int fct = i + 1;
            int rp = VictorySystem.RitePathOf(fct);
            float ch = rp >= 0 ? 112 : 82;
            var card = Panel(c, "RV_" + i, CARD);
            Place(card.rectTransform, 0, y, w - 6, ch); Outline(card, rp >= 0 ? C("#e05a5a") : C(rv.colorHex));
            int pl = DiplomacySystem.PeaceLeft(i);
            var n1 = Text(card.rectTransform, "<color=" + rv.colorHex + ">" + rv.name + "</color> <size=88%><color=#9c95b4>" + rv.title + "</color></size>"
                + (rv.defeated ? " <color=#5cc47c>［排除］</color>" : pl > 0 ? " <color=#57c3ab>［不可侵 あと" + pl + "］</color>" : " <color=#e05a5a>［交戦中］</color>"),
                12.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(n1.rectTransform, 12, 5, w - 30, 18);
            var snap = RivalBrain.DungeonOf(i);
            var n2 = Text(card.rectTransform, "<size=90%><color=#9c95b4>力 " + rv.power.ToString("0") + "／" + RivalLords.TerritoryOf(i) + "領"
                + (snap != null ? "　迷宮 <color=#e8e4f2>" + snap.FloorCount + "層</color>・守り <color=#e8e4f2>" + snap.TotalGuards
                    + "</color>・段 <color=#e8e4f2>" + snap.tier + "</color>・主 Lv<color=#e8e4f2>" + snap.lordLevel
                    + "</color>・名声 <color=#e8e4f2>" + RivalBrain.FameOf(i) + "</color>" : "") + "</color></size>",
                11f, MUTED, TextAlignmentOptions.TopLeft);
            n2.enableWordWrapping = false;
            Place(n2.rectTransform, 12, 26, w - 30, 16);
            string la = RivalBrain.LastActOf(i);
            var n3 = Text(card.rectTransform, "<size=88%><color=#6f6889>前のターン：" + (string.IsNullOrEmpty(la) ? "―" : la) + "</color></size>",
                10.5f, FAINT, TextAlignmentOptions.TopLeft);
            n3.enableWordWrapping = false; n3.overflowMode = TextOverflowModes.Ellipsis;
            Place(n3.rectTransform, 12, 44, w - 320, 16);
            AddTooltip(n3.gameObject, "前のターン：" + la);
            n3.raycastTarget = true;
            if (rp >= 0)
            {
                int pg = VictorySystem.RiteProgressOf(fct);
                var n4 = Text(card.rectTransform, "<color=#e05a5a><b>◆『" + VictorySystem.RiteName((VictorySystem.Path)rp) + "』 " + pg + "/"
                    + VictorySystem.RiteTurns + "</b></color>" + (VictorySystem.RiteStalled(fct)
                        ? "<color=#9c95b4>（条件を割って止まっている）</color>"
                        : "<color=#9c95b4>　巣へ攻め込み階を落とすと " + VictorySystem.RiteSetback + " ターン押し戻せる</color>"),
                    11.5f, TEXT, TextAlignmentOptions.TopLeft);
                n4.enableWordWrapping = false;
                Place(n4.rectTransform, 12, 64, w - 30, 18);
            }
            if (!rv.defeated)
            {
                float by = ch - 32;
                if (pl <= 0)
                {
                    var pb = PrimaryButton(card, "不可侵 " + DiplomacySystem.PeaceCost(i), PANEL2, C("#57c3ab"),
                        () => { if (DiplomacySystem.TryMakePeace(rid2)) RefreshSurfacePanel(); });
                    Place((RectTransform)pb.transform, w - 292, by, 134, 26);
                }
                var ib = PrimaryButton(card, "讒言 " + DiplomacySystem.InciteCost, PANEL2, C("#e05a5a"),
                    () => { if (DiplomacySystem.TryIncite(rid2)) RefreshSurfacePanel(); });
                Place((RectTransform)ib.transform, w - 152, by, 138, 26);
            }
            y += ch + 4;
        }
        y += 8;

        // ── 独立勢力 ──
        var ph = Text(c, "◆ 独立勢力 " + DiplomacySystem.SuzerainCount + "/" + DiplomacySystem.Powers.Count
            + "（働きかけ " + DiplomacySystem.CourtCost() + "威名 → 好意+" + DiplomacySystem.CourtGain + "）",
            12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(ph.rectTransform, 4, y, w - 8, 18); y += 22;
        for (int i = 0; i < DiplomacySystem.Powers.Count; i++)
        {
            int pi = i; var p = DiplomacySystem.Powers[i];
            var kd = DiplomacySystem.Kind(p.kind);
            bool mine = p.suzerain == 0;
            bool seen = SurfaceMap.IsDiscovered(p.regionId);
            bool suz = mine && p.stage >= 2;
            float ch2 = (suz ? 92 : 60);
            var card = Panel(c, "P_" + i, CARD);
            Place(card.rectTransform, 0, y, w - 6, ch2); Outline(card, p.destroyed ? C("#4a4560") : mine ? C(kd.colorHex) : LINE2);
            var n1 = Text(card.rectTransform, "<color=" + kd.colorHex + ">" + kd.jpName + "</color> " + p.name
                + (p.destroyed ? " <color=#6f6889>［消滅］</color>"
                   : suz ? " <color=#5cc47c>［宗主国］</color>"
                   : mine ? " <color=#e3a94a>［友好 あと" + Mathf.Max(0, DiplomacySystem.StageTurns - p.stageTurns) + "ターンで宗主国・恵みは半分］</color>"
                   : p.suzerain > 0 ? " <color=#e05a5a>［" + RivalLords.NameOf(p.suzerain - 1) + "に従属］</color>" : ""),
                12.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(n1.rectTransform, 12, 5, w - 30, 18);
            var n2 = Text(card.rectTransform, "<size=90%><color=#9c95b4>" + kd.desc + "</color></size>", 11f, MUTED, TextAlignmentOptions.TopLeft);
            Place(n2.rectTransform, 12, 24, w - 165, 16);
            var n3 = Text(card.rectTransform, "好意 <color=#57c3ab>" + p.favor + "/" + DiplomacySystem.FavorNeed + "</color>"
                + (seen ? "" : "　<color=#6f6889>（未発見）</color>"), 11f, MUTED, TextAlignmentOptions.TopLeft);
            Place(n3.rectTransform, 12, 41, w - 165, 16);
            if (!mine && p.suzerain < 0 && seen && !p.destroyed)
            {
                var bb = PrimaryButton(card, "働きかけ " + DiplomacySystem.CourtCost(), PANEL2, C("#57c3ab"),
                    () => { if (DiplomacySystem.TryCourt(pi)) RefreshSurfacePanel(); });
                Place((RectTransform)bb.transform, w - 152, 8, 138, 26);
            }
            // 🏛️ 宗主国だけができること（Civ VII の宗主国限定外交）
            if (suz && !p.destroyed)
            {
                var g1 = PrimaryButton(card, "成長 " + DiplomacySystem.ProjectGrow, PANEL2, C("#5cc47c"),
                    () => { if (DiplomacySystem.TryProjectGrow(pi)) RefreshSurfacePanel(); });
                Place((RectTransform)g1.transform, 12, 62, 132, 24);
                AddTooltip(((RectTransform)g1.transform).gameObject, "威名" + DiplomacySystem.ProjectGrow + "。一番小さい拠点に人と糧が送られ、人口が1つ育つ。");
                var g2 = PrimaryButton(card, "軍備 " + DiplomacySystem.ProjectLevy, PANEL2, C("#df5a5a"),
                    () => { if (DiplomacySystem.TryProjectLevy(pi)) RefreshSurfacePanel(); });
                Place((RectTransform)g2.transform, 150, 62, 132, 24);
                AddTooltip(((RectTransform)g2.transform).gameObject, "威名" + DiplomacySystem.ProjectLevy + "。兵と物資の供出（DP+400・素材+12）。");
                var g3 = PrimaryButton(card, "併合 " + DiplomacySystem.ProjectAnnex, PANEL2, C("#e3c34a"),
                    () => { if (DiplomacySystem.TryProjectAnnex(pi)) RefreshSurfacePanel(); });
                Place((RectTransform)g3.transform, 288, 62, 132, 24);
                AddTooltip(((RectTransform)g3.transform).gameObject, "威名" + DiplomacySystem.ProjectAnnex + "。その土地を自分の拠点(Town)として取り込む。恵みは失う。");
            }
            var jb = PrimaryButton(card, "位置へ", PANEL2, TEXT, () =>
            {
                selectedRegionId = DiplomacySystem.Powers[pi].regionId;
                if (surfaceView != null) { surfaceView.SetSelected(selectedRegionId); surfaceView.CenterOn(selectedRegionId); }
                RefreshSurfacePanel();
            });
            Place((RectTransform)jb.transform, w - 152, 38, 138, 22);
            y += ch2 + 4;
        }
        y += 8;

        // ── 交易路 ──
        var th2 = Text(c, "◆ 交易路 " + DiplomacySystem.Routes.Count + "/" + DiplomacySystem.RouteLimit
            + "（" + DiplomacySystem.RouteCost + "威名・" + DiplomacySystem.RouteRange + "マスまで）", 12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(th2.rectTransform, 4, y, w - 8, 18); y += 22;
        for (int i = 0; i < DiplomacySystem.Routes.Count; i++)
        {
            int ri = i; var r = DiplomacySystem.Routes[i];
            var card = Panel(c, "R_" + i, CARD);
            Place(card.rectTransform, 0, y, w - 6, 32); Outline(card, LINE2);
            var n1 = Text(card.rectTransform, SurfaceMap.Get(r.a).name + " ― " + SurfaceMap.Get(r.b).name
                + "　<size=88%><color=#9c95b4>" + SurfaceMap.HexDist(SurfaceMap.Get(r.a), SurfaceMap.Get(r.b)) + "マス</color></size>",
                11.5f, TEXT, TextAlignmentOptions.TopLeft);
            Place(n1.rectTransform, 12, 8, w - 110, 18);
            var cb = PrimaryButton(card, "閉じる", PANEL2, C("#e05a5a"), () => { DiplomacySystem.CloseRoute(ri); RefreshSurfacePanel(); });
            Place((RectTransform)cb.transform, w - 100, 4, 86, 24);
            y += 36;
        }
        if (selectedRegionId >= 0)
        {
            var sel2 = SurfaceMap.Get(selectedRegionId);
            if (sel2.owned && sel2.settle != SurfaceMap.Settle.None)
            {
                var nh = Text(c, "<size=92%><color=#9c95b4>選択中『" + sel2.name + "』から結べる相手</color></size>", 11.5f, MUTED, TextAlignmentOptions.TopLeft);
                Place(nh.rectTransform, 8, y, w - 16, 16); y += 20;
                int shown = 0;
                foreach (var o in SurfaceMap.All)
                {
                    if (shown >= 6) break;
                    if (!o.owned || o.settle == SurfaceMap.Settle.None || o.id == sel2.id) continue;
                    if (SurfaceMap.HexDist(sel2, o) > DiplomacySystem.RouteRange) continue;
                    int oid = o.id;
                    var card = Panel(c, "NR_" + oid, CARD);
                    Place(card.rectTransform, 0, y, w - 6, 30); Outline(card, LINE);
                    var n1 = Text(card.rectTransform, o.name + "　<size=88%><color=#9c95b4>" + SurfaceMap.HexDist(sel2, o) + "マス</color></size>",
                        11.5f, MUTED, TextAlignmentOptions.TopLeft);
                    Place(n1.rectTransform, 12, 7, w - 110, 18);
                    var ob = PrimaryButton(card, "結ぶ", PANEL2, C("#e3c34a"),
                        () => { if (DiplomacySystem.TryOpenRoute(selectedRegionId, oid)) RefreshSurfacePanel(); });
                    Place((RectTransform)ob.transform, w - 100, 3, 86, 24);
                    y += 34; shown++;
                }
            }
        }
        y += 8;

        c.sizeDelta = new Vector2(0f, Mathf.Max(y + 8, 80));
    }

    /// <summary>
    /// 📜 **ジャーナル**（K-6 A-3 → 百年の決着で作り替え）。
    ///
    /// <para>
    /// ⚠⚠ 中身は「点と閾値」から「**4つの条件と儀**」になった（→ [[VictorySystem]]）。
    ///   画面は承認済みの案（`fab7d593`）の形：条件4行（✓/−・いま/要る・▶）→ 満ちた数 → 儀の帯 → 他の勢力。
    /// ⚠ **開くのは宣言した1本だけ**（無ければ、いちばん条件の満ちている1本）。4本同時は長すぎる。
    /// ⚠ **宣言に報酬も罰も無い**（変わるのは腹心の進言だけ）は据え置き。
    /// ⚠ **ボタンを増やさない。**地上の左の柱は12個のまま、中身だけを替える。
    /// </para>
    /// </summary>
    private void RefreshVictoryPanel()
    {
        var c = victoryContainer; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = victoryW, y = 0f;

        // ── 見出し ──
        var head = Panel(c, "VHead", CARD);
        Place(head.rectTransform, 0, y, w - 6, 78); Outline(head, GOLD);
        var h1 = Text(head.rectTransform, VictorySystem.Decided
            ? VictorySystem.HeaderLine()
            : "勝ちは4本。どれも <color=#e3c34a>4つの条件のうち" + VictorySystem.CondNeed + "つ</color> を満たすと『儀』が開き、"
              + "生産の列で <color=#e3c34a>" + VictorySystem.RiteTurns + "ターン</color> かけて完成させると勝ちです。",
            12f, TEXT, TextAlignmentOptions.TopLeft);
        Place(h1.rectTransform, 12, 7, w - 30, 36);
        var h2 = Text(head.rectTransform, "<size=92%><color=#9c95b4>他の魔王も同じ形で儀を始めます。先に終えられるとこちらの敗北 ―― "
            + "その巣へ攻め込んで階を落とせば押し戻せます。</color></size>", 11f, MUTED, TextAlignmentOptions.TopLeft);
        Place(h2.rectTransform, 12, 45, w - 30, 30);
        y += 86;

        // ── ⚠ 他の魔王の儀（いちばん上に赤で）──
        int rf = VictorySystem.MostUrgentRivalRite();
        if (rf >= 0 && !VictorySystem.Decided)
        {
            int rp = VictorySystem.RitePathOf(rf), rpg = VictorySystem.RiteProgressOf(rf);
            var al = Panel(c, "VAlarm", C("#2a1014"));
            Place(al.rectTransform, 0, y, w - 6, 34); Outline(al, C("#e05a5a"));
            var at = Text(al.rectTransform, "<color=#e05a5a><b>◆ " + VictorySystem.FactionName(rf) + " が『"
                + VictorySystem.RiteName((VictorySystem.Path)rp) + "』を進めている　" + rpg + "/" + VictorySystem.RiteTurns
                + "</b></color>" + (VictorySystem.RiteStalled(rf) ? "<color=#9c95b4>（条件を割って止まっている）</color>"
                : "<color=#9c95b4>　あと " + (VictorySystem.RiteTurns - rpg) + " ターンで敗北</color>"),
                11.5f, TEXT, TextAlignmentOptions.MidlineLeft);
            Place(at.rectTransform, 12, 8, w - 150, 18);
            var gb = Panel(al.rectTransform, "Go", PANEL2);
            Place(gb.rectTransform, w - 132, 6, 116, 22); Outline(gb, C("#e05a5a"));
            var gt = Text(gb.rectTransform, "▶ 外交で見る", 10.5f, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
            StretchFull(gt.rectTransform);
            var gbtn = gb.gameObject.AddComponent<Button>(); gbtn.targetGraphic = gb;
            gbtn.onClick.AddListener(() => GoToAdvice("surface:外交"));
            y += 42;
        }

        // ── 📜 宣言の帯 ──
        int decl = VictorySystem.DeclaredPath;
        var drow = Panel(c, "VDecl", PANEL2);
        Place(drow.rectTransform, 0, y, w - 6, 34); Outline(drow, LINE);
        var dl0 = Text(drow.rectTransform, "狙う道：", 11.5f, MUTED, TextAlignmentOptions.MidlineLeft);
        Place(dl0.rectTransform, 10, 9, 54, 16);
        float dx = 66;
        for (int p = 0; p < VictorySystem.PathCount; p++)
        {
            int pp = p; var path = (VictorySystem.Path)p;
            bool on = decl == p;
            float bw2 = on ? 96 : 66;
            var b = Panel(drow.rectTransform, "D_" + p, on ? C(VictorySystem.PathColor(path)) : CARD);
            Place(b.rectTransform, dx, 5, bw2, 24); Outline(b, on ? GOLD : LINE2);
            var t = Text(b.rectTransform, VictorySystem.PathName(path) + (on ? " ◆宣言中" : ""), 11.5f,
                on ? C("#1a1206") : C(VictorySystem.PathColor(path)), TextAlignmentOptions.Center, FontStyles.Bold);
            StretchFull(t.rectTransform);
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
            btn.onClick.AddListener(() => { VictorySystem.Declare(pp); RefreshVictoryPanel(); RefreshNextAction(); });
            AddTooltip(b.gameObject, "<b>" + VictorySystem.PathName(path) + "の道</b>" + "\n"
                + VictorySystem.PathDesc(path) + "\n"
                + "<color=#9c95b4>" + (on ? "もう一度押すと取り消します。" : "宣言すると、この道の条件が腹心の進言に混ざります。")
                + "</color>" + "\n" + "<color=#6f6889>⚠ 報酬も罰もありません。いつでも変えられます。</color>");
            dx += bw2 + 6;
        }
        y += 42;

        // ── 開く1本：宣言した道／儀をやっている道／いちばん満ちている道 ──
        int open = decl;
        if (open < 0) open = VictorySystem.RitePathOf(VictorySystem.Self);
        if (open < 0)
        {
            int best = -1;
            for (int p = 0; p < VictorySystem.PathCount; p++)
                if (best < 0 || VictorySystem.MetCount(VictorySystem.Self, (VictorySystem.Path)p)
                              > VictorySystem.MetCount(VictorySystem.Self, (VictorySystem.Path)best)) best = p;
            open = best;
        }
        y = DrawDeclaredPath((VictorySystem.Path)open, c, w, y, decl == open);

        // ── 残りの3本は行のまま（条件の丸4つ＋儀の状態）──
        for (int p = 0; p < VictorySystem.PathCount; p++)
        {
            if (p == open) continue;
            var path = (VictorySystem.Path)p;
            var conds = VictorySystem.Conditions(VictorySystem.Self, path);
            int met = VictorySystem.MetCount(VictorySystem.Self, path);
            var row = Panel(c, "VF_" + p, C("#100e1a"));
            Place(row.rectTransform, 0, y, w - 6, 28); Outline(row, LINE);
            var n = Text(row.rectTransform, VictorySystem.PathName(path), 12f,
                C(VictorySystem.PathColor(path)), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            Place(n.rectTransform, 12, 6, 60, 16);
            var sbd = new System.Text.StringBuilder();
            for (int i = 0; i < conds.Count; i++)
                sbd.Append(conds[i].Met ? "<color=#5cc47c>●</color>" : "<color=#3a3150>●</color>");
            var dots = Text(row.rectTransform, sbd.ToString() + "　<size=90%><color=#9c95b4>" + met + "/" + conds.Count + "</color></size>",
                12f, TEXT, TextAlignmentOptions.MidlineLeft);
            dots.enableWordWrapping = false;
            Place(dots.rectTransform, 76, 6, 150, 16);
            string st = met >= VictorySystem.CondNeed ? "<color=#e3c34a>『" + VictorySystem.RiteName(path) + "』を始められる</color>"
                      : "<color=#6f6889>あと " + (VictorySystem.CondNeed - met) + " つで『" + VictorySystem.RiteName(path) + "』</color>";
            var stt = Text(row.rectTransform, st, 11f, MUTED, TextAlignmentOptions.MidlineRight);
            stt.enableWordWrapping = false;
            Place(stt.rectTransform, 230, 6, w - 6 - 242, 16);
            var tip = new System.Text.StringBuilder();
            tip.Append("<b>").Append(VictorySystem.PathName(path)).Append("の道</b> ― ").Append(VictorySystem.PathDesc(path));
            for (int i = 0; i < conds.Count; i++)
                tip.Append("\n").Append(conds[i].Met ? "<color=#5cc47c>✓</color> " : "<color=#6f6889>−</color> ")
                   .Append(conds[i].label).Append("　").Append(conds[i].HaveText).Append(" / ").Append(conds[i].NeedText);
            tip.Append("\n").Append("<color=#6f6889>上の『").Append(VictorySystem.PathName(path)).Append("』を押すと、この道が開きます。</color>");
            AddTooltip(row.gameObject, tip.ToString());
            y += 32;
        }

        // ── 勢力の並び（条件の満ちた数・儀）──
        y += 8;
        var th = Text(c, "◆ 勢力ごとの条件（" + VictorySystem.CondNeed + "つ満ちると儀が開く）", 12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(th.rectTransform, 4, y, w - 8, 18); y += 22;
        float colW = (w - 100) / 6f;
        var hdr = Text(c, "勢力", 10.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(hdr.rectTransform, 12, y, 90, 15);
        for (int p = 0; p < VictorySystem.PathCount; p++)
        {
            var t2 = Text(c, VictorySystem.PathName((VictorySystem.Path)p), 10.5f,
                C(VictorySystem.PathColor((VictorySystem.Path)p)), TextAlignmentOptions.TopRight);
            Place(t2.rectTransform, 100 + p * colW, y, colW - 6, 15);
        }
        var t4 = Text(c, "儀", 10.5f, GOLD, TextAlignmentOptions.TopRight);
        Place(t4.rectTransform, 100 + 4 * colW, y, colW - 6, 15);
        var t3 = Text(c, "総合", 10.5f, GOLD, TextAlignmentOptions.TopRight);
        Place(t3.rectTransform, 100 + 5 * colW, y, colW - 6, 15);
        y += 18;
        for (int f2 = 0; f2 < VictorySystem.FactionCount; f2++)
        {
            bool me = f2 == VictorySystem.Self;
            bool human = f2 == VictorySystem.HumanIndex;
            bool dead = !me && !human && RivalLords.Get(f2 - 1).defeated;
            var nm = Text(c, "<color=" + VictorySystem.FactionColor(f2) + ">" + VictorySystem.FactionName(f2) + "</color>"
                + (dead ? "<size=85%><color=#6f6889> 排除</color></size>" : ""),
                11f, MUTED, TextAlignmentOptions.TopLeft, me ? FontStyles.Bold : FontStyles.Normal);
            Place(nm.rectTransform, 12, y, 90, 16);
            for (int p = 0; p < VictorySystem.PathCount; p++)
            {
                string cell;
                if (human) cell = "<color=#3a3150>―</color>";
                else
                {
                    int m = VictorySystem.MetCount(f2, (VictorySystem.Path)p);
                    cell = (m >= VictorySystem.CondNeed ? "<color=" + (me ? "#e3c34a" : "#e05a5a") + ">" : "") + m + "/4"
                         + (m >= VictorySystem.CondNeed ? "</color>" : "");
                }
                var v = Text(c, cell, 11f, me ? C("#5cc47c") : FAINT, TextAlignmentOptions.TopRight);
                Place(v.rectTransform, 100 + p * colW, y, colW - 6, 16);
            }
            int rp2 = VictorySystem.RitePathOf(f2);
            string rite = human ? "<color=#6f6889>持たない</color>"
                : rp2 < 0 ? "<color=#3a3150>―</color>"
                : "<color=" + (me ? "#e3c34a" : "#e05a5a") + ">" + VictorySystem.PathName((VictorySystem.Path)rp2) + " "
                  + VictorySystem.RiteProgressOf(f2) + "/" + VictorySystem.RiteTurns + "</color>";
            var rv2 = Text(c, rite, 11f, FAINT, TextAlignmentOptions.TopRight);
            Place(rv2.rectTransform, 100 + 4 * colW, y, colW - 6, 16);
            var tot = Text(c, VictorySystem.TotalScore(f2).ToString(), 11f,
                me ? C("#5cc47c") : MUTED, TextAlignmentOptions.TopRight, FontStyles.Bold);
            Place(tot.rectTransform, 100 + 5 * colW, y, colW - 6, 16);
            // 他の魔王：何が満ちているかは hover で（見えない所で強くなるのは理不尽）
            if (!me && !human)
            {
                var tip = new System.Text.StringBuilder();
                tip.Append("<b>").Append(VictorySystem.FactionName(f2)).Append("</b> の条件");
                for (int p = 0; p < VictorySystem.PathCount; p++)
                {
                    var conds = VictorySystem.Conditions(f2, (VictorySystem.Path)p);
                    tip.Append("\n").Append("<color=").Append(VictorySystem.PathColor((VictorySystem.Path)p)).Append(">")
                       .Append(VictorySystem.PathName((VictorySystem.Path)p)).Append("</color>　");
                    for (int i = 0; i < conds.Count; i++)
                        tip.Append(conds[i].Met ? "<color=#e05a5a>" : "<color=#6f6889>").Append(conds[i].label).Append(" ")
                           .Append(conds[i].HaveText).Append("/").Append(conds[i].NeedText).Append("</color>")
                           .Append(i < conds.Count - 1 ? "・" : "");
                }
                AddTooltip(nm.gameObject, tip.ToString());
                nm.raycastTarget = true;
            }
            y += 18;
        }
        var foot = Text(c, "<size=92%><color=#6f6889>人間側は儀を持ちません ―― 人間側の勝ち方は魔王を討つことです。"
            + "決着しないまま終焉の時代が終われば、総合（4本の点の合計）で決まります。</color></size>", 11f, FAINT, TextAlignmentOptions.TopLeft);
        Place(foot.rectTransform, 12, y + 4, w - 24, 32); y += 40;

        c.sizeDelta = new Vector2(0f, Mathf.Max(y + 8, 80));
    }

    /// <summary>
    /// 📜 開いた1本（名前・**4つの条件**・満ちた数・**儀の帯**・他の勢力の同じ道）。
    /// ⚠ 条件は `VictorySystem.Conditions` を**そのまま**並べる。ここで数えない。
    /// </summary>
    private float DrawDeclaredPath(VictorySystem.Path path, RectTransform c, float w, float y, bool declared)
    {
        var conds = VictorySystem.Conditions(VictorySystem.Self, path);
        int met = VictorySystem.MetCount(VictorySystem.Self, path);
        int myRite = VictorySystem.RitePathOf(VictorySystem.Self);
        bool riteHere = myRite == (int)path;
        bool unlocked = met >= VictorySystem.CondNeed;

        float riteH = (riteHere || unlocked) ? 58 : 0;
        float cardH = 36 + conds.Count * 24 + 30 + riteH + 26 + (declared ? 30 : 8);
        var card = Panel(c, "VOpen", CARD);
        Place(card.rectTransform, 0, y, w - 6, cardH); Outline(card, C(VictorySystem.PathColor(path)));

        var n1 = Text(card.rectTransform, "<color=" + VictorySystem.PathColor(path) + ">" + VictorySystem.PathName(path) + "の道</color>"
            + "　<size=88%><color=#9c95b4>― " + VictorySystem.PathDesc(path) + "</color></size>",
            13f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(n1.rectTransform, 12, 8, w - 30, 18);

        float gy = 34;
        for (int i = 0; i < conds.Count; i++)
        {
            var q = conds[i];
            var ln = Panel(card.rectTransform, "C" + i, new Color(0, 0, 0, 0));
            Place(ln.rectTransform, 12, gy, w - 34, 22);
            var mk = Text(ln.rectTransform, q.Met ? "✓" : "−", 13f, q.Met ? C("#5cc47c") : FAINT, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(mk.rectTransform, 0, 2, 20, 18);
            var lb = Text(ln.rectTransform, q.label, 12f, q.Met ? TEXT : MUTED, TextAlignmentOptions.MidlineLeft);
            Place(lb.rectTransform, 26, 2, w - 330, 18);
            var vv = Text(ln.rectTransform, q.HaveText + " / " + q.NeedText, 11.5f,
                q.Met ? C("#5cc47c") : MUTED, TextAlignmentOptions.MidlineRight);
            vv.enableWordWrapping = false;
            Place(vv.rectTransform, w - 300, 2, 140, 18);
            if (!q.Met && !string.IsNullOrEmpty(q.go))
            {
                string key = q.go;
                var gb = Panel(ln.rectTransform, "Go", PANEL2);
                Place(gb.rectTransform, w - 150, 2, 104, 18); Outline(gb, LINE2);
                var gt = Text(gb.rectTransform, "▶ " + GoLabel(key), 10.5f, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
                StretchFull(gt.rectTransform);
                var gbtn = gb.gameObject.AddComponent<Button>(); gbtn.targetGraphic = gb;
                gbtn.onClick.AddListener(() => GoToAdvice(key));
            }
            var rule = Panel(card.rectTransform, "R" + i, LINE);
            Place(rule.rectTransform, 12, gy + 23, w - 34, 1);
            gy += 24;
        }

        // 満ちた数
        var gate = Text(card.rectTransform, "4つのうち <color=" + (unlocked ? "#5cc47c" : "#e3c34a") + "><b>" + met + "つ</b></color> 満たした"
            + (unlocked ? "　――　<color=#e3c34a><b>『" + VictorySystem.RiteName(path) + "』が解禁された</b></color>"
                        : "　<color=#6f6889>（あと " + (VictorySystem.CondNeed - met) + " つで『" + VictorySystem.RiteName(path) + "』）</color>"),
            11.5f, MUTED, TextAlignmentOptions.MidlineLeft);
        Place(gate.rectTransform, 12, gy + 6, w - 34, 18);
        gy += 30;

        // ◆ 儀の帯
        if (riteHere || unlocked)
        {
            var rb = Panel(card.rectTransform, "Rite", C("#1c1608"));
            Place(rb.rectTransform, 12, gy, w - 34, 50); Outline(rb, GOLD);
            if (riteHere)
            {
                int pg = VictorySystem.RiteProgressOf(VictorySystem.Self);
                bool stall = VictorySystem.RiteStalled(VictorySystem.Self);
                var rt = Text(rb.rectTransform, "◆ " + VictorySystem.RiteName(path) + "　―　生産の列で仕上げる", 12.5f, GOLD,
                    TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(rt.rectTransform, 10, 5, w - 60, 16);
                var bar = Panel(rb.rectTransform, "Bar", PANEL2);
                Place(bar.rectTransform, 10, 24, w - 54, 7); Outline(bar, LINE);
                var fill = Panel(bar, "Fill", GOLD);
                Place(fill.rectTransform, 0, 0, (w - 54) * Mathf.Clamp01(pg / (float)VictorySystem.RiteTurns), 7);
                var rs = Text(rb.rectTransform, pg + " / " + VictorySystem.RiteTurns + " ターン　　残り " + (VictorySystem.RiteTurns - pg) + " ターン　　"
                    + (stall ? "<color=#e05a5a>⚠ 条件を割って止まっている</color>" : "⚠ 完成した瞬間に勝ちます"),
                    10.5f, MUTED, TextAlignmentOptions.TopLeft);
                Place(rs.rectTransform, 10, 34, w - 60, 14);
            }
            else if (myRite >= 0)
            {
                var rt = Text(rb.rectTransform, "◆ " + VictorySystem.RiteName(path) + " は開いているが、いまは『"
                    + VictorySystem.RiteName((VictorySystem.Path)myRite) + "』を進めている（儀は同時に1つ）",
                    11.5f, MUTED, TextAlignmentOptions.MidlineLeft);
                Place(rt.rectTransform, 10, 8, w - 60, 34);
            }
            else
            {
                int rid = ProductionSystem.RiteRegion();
                string why;
                bool can = rid >= 0 && ProductionSystem.CanEnqueue(rid, ProductionSystem.Kind.Rite, (int)path, out why);
                var rt = Text(rb.rectTransform, "◆ " + VictorySystem.RiteName(path), 12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(rt.rectTransform, 10, 5, w - 220, 16);
                var rs = Text(rb.rectTransform, rid >= 0
                    ? "<color=#9c95b4>" + SurfaceMap.Get(rid).name + " の列の先頭に積み、" + VictorySystem.RiteTurns + "ターンで成る。買えない。</color>"
                    : "<color=#e05a5a>拠点が無いと始められない</color>", 10.5f, MUTED, TextAlignmentOptions.TopLeft);
                Place(rs.rectTransform, 10, 26, w - 220, 18);
                var pb = PrimaryButton(rb.rectTransform, "儀を始める", can ? C("#e3a94a") : PANEL2, can ? C("#1a1206") : FAINT, () =>
                {
                    int r2 = ProductionSystem.RiteRegion();
                    if (r2 >= 0 && ProductionSystem.TryEnqueue(r2, ProductionSystem.Kind.Rite, (int)path))
                    {
                        SoundSystem.Play(SoundSystem.Sfx.Confirm);
                        NotifySystem.Push("『" + VictorySystem.RiteName(path) + "』を始めた　" + VictorySystem.RiteTurns + "ターンで成る",
                            NotifySystem.Kind.Story, r2);
                        RefreshVictoryPanel(); RefreshNextAction();
                    }
                }, true);
                Place((RectTransform)pb.transform, w - 196, 10, 150, 30);
                var plab = pb.GetComponentInChildren<TextMeshProUGUI>();
                if (plab != null && can) plab.color = NextFg(pb);
            }
            gy += 58;
        }

        // 他の勢力の同じ道
        var sb = new System.Text.StringBuilder("他の勢力：");
        bool first = true;
        for (int f = 1; f < VictorySystem.HumanIndex; f++)
        {
            if (RivalLords.Get(f - 1).defeated) continue;
            if (!first) sb.Append("　―　");
            first = false;
            int m = VictorySystem.MetCount(f, path);
            sb.Append("<color=").Append(VictorySystem.FactionColor(f)).Append(">").Append(VictorySystem.FactionName(f)).Append("</color> ")
              .Append(m >= VictorySystem.CondNeed ? "<color=#e05a5a>" + m + "/4</color>" : m + "/4");
        }
        if (first) sb.Append("残っていない");
        var riv = Text(card.rectTransform, sb.ToString(), 11f, FAINT, TextAlignmentOptions.MidlineLeft);
        riv.enableWordWrapping = false;
        Place(riv.rectTransform, 12, gy, w - 34, 18);
        gy += 24;

        if (declared)
        {
            var warn = Text(card.rectTransform, "<size=92%><color=#9c95b4>⚠ 宣言しても何も起きません。<b>報酬も罰もありません。</b>"
                + "変わるのは、腹心がこの道の足りない条件を<b>進言に混ぜる</b>ようになることだけです。</color></size>",
                11f, MUTED, TextAlignmentOptions.TopLeft);
            Place(warn.rectTransform, 14, gy, w - 34, 28);
        }

        return y + cardH + 8;
    }

    /// <summary>▶ ボタンに出す短い行き先名（`GoToAdvice` のキーから作る）。</summary>
    private static string GoLabel(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        int i = key.IndexOf(':');
        if (i < 0) return key;
        string kind = key.Substring(0, i), what = key.Substring(i + 1);
        if (kind == "dungeon") return "迷宮へ";
        if (kind == "tool") return what + "を置く";
        return what;
    }

    /// <summary>⏳ 時代：進行度・偉業・誓約・災厄（C3）。</summary>
    /// <summary>💎 その拠点の資源の使用状況（枠に入って初めて効く）。</summary>
    private string ResourceUsageText(int settlementId)
    {
        int used, slots, total;
        SettlementSystem.ResourceUsage(settlementId, out used, out slots, out total);
        if (total == 0 && slots == 0) return "";
        string col = used < total ? "#e08a3c" : "#e3c34a";
        return "　資源 <color=" + col + ">" + used + "/" + slots + "</color>"
            + (total > used ? "<size=88%><color=#6f6889>（版図に" + total + "・枠外" + (total - used) + "）</color></size>" : "");
    }

    // 🏛️ 政体と政策（S1）。スロットを押す → 手札のカードを押す、で差し替える。
    private int selectedPolicySlot = -1;
    private void RefreshPolicyPanel()
    {
        var c = policyContainer; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = policyW, y = 0f;

        // ── 政体 ──
        var gh = Text(c, "◆ 政体（時代の変わり目は無料。途中で変えるなら "
            + PolicySystem.SwitchCost + "DP）" + (PolicySystem.IsFreeSwitch ? "　<color=#5cc47c>いまは無料で選べます</color>" : ""),
            12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(gh.rectTransform, 4, y, w - 8, 18); y += 22;
        for (int i = 0; i < PolicySystem.GovCount; i++)
        {
            int gi = i; var g = PolicySystem.Gov(i);
            bool on = PolicySystem.GovIndex == i;
            var card = Panel(c, "Gov_" + i, on ? PANEL2 : CARD);
            Place(card.rectTransform, 0, y, w - 6, 52); Outline(card, on ? C(g.colorHex) : LINE);
            var n1 = Text(card.rectTransform, "<color=" + g.colorHex + ">" + g.jpName + "</color>"
                + "　<size=88%><color=#9c95b4>枠 戦" + g.war + "・富" + g.wealth + "・秘" + g.arcane + "・民" + g.civic + "</color></size>"
                + (on ? "　<color=#5cc47c>選択中</color>" : ""), 13f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(n1.rectTransform, 12, 7, w - 30, 20);
            var n2 = Text(card.rectTransform, "<size=92%><color=#9c95b4>" + g.desc + "</color></size>", 11.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(n2.rectTransform, 12, 28, w - 30, 18);
            var bt = card.gameObject.AddComponent<Button>(); bt.targetGraphic = card;
            bt.onClick.AddListener(() => { if (PolicySystem.TrySetGov(gi)) RefreshSurfacePanel(); });
            AddTooltip(card.gameObject, g.jpName + "\n" + g.desc + "\n祝祭A：" + g.festA + "\n祝祭B：" + g.festB);
            y += 56;
        }
        y += 6;

        // ── 祝祭中のボーナス（2択）──
        var cg = PolicySystem.CurrentGov;
        var fh = Text(c, "◆ 祝祭中のボーナス（どちらを効かせるか）"
            + (PolicySystem.AnyCelebrating ? "　<color=#5cc47c>いま祝祭中</color>" : "　<color=#6f6889>祝祭が起きたら効きます</color>"),
            12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(fh.rectTransform, 4, y, w - 8, 18); y += 22;
        for (int i = 0; i < 2; i++)
        {
            int fi = i;
            bool on = PolicySystem.FestivalChoice == i;
            var card = Panel(c, "Fest_" + i, on ? PANEL2 : CARD);
            Place(card.rectTransform, 0, y, w - 6, 32); Outline(card, on ? C(cg.colorHex) : LINE);
            var t = Text(card.rectTransform, (i == 0 ? cg.festA : cg.festB) + (on ? "　<color=#5cc47c>選択中</color>" : ""),
                12f, TEXT, TextAlignmentOptions.Left);
            Place(t.rectTransform, 12, 7, w - 30, 20);
            var bt = card.gameObject.AddComponent<Button>(); bt.targetGraphic = card;
            bt.onClick.AddListener(() => { PolicySystem.FestivalChoice = fi; RefreshSurfacePanel(); });
            y += 36;
        }
        y += 6;

        // ── スロット ──
        var layout = PolicySystem.SlotLayout();
        var sh = Text(c, "◆ 政策スロット（押して選び、下の手札から差す）　<size=88%><color=#9c95b4>"
            + PolicySystem.SlotSummary() + "</color></size>", 12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(sh.rectTransform, 4, y, w - 8, 18); y += 22;
        for (int i = 0; i < layout.Count; i++)
        {
            int si = i;
            var kind = layout[i];
            int pi = PolicySystem.SlottedAt(i);
            bool sel = selectedPolicySlot == i;
            var card = Panel(c, "Slot_" + i, sel ? PANEL2 : CARD);
            Place(card.rectTransform, 0, y, w - 6, 36);
            Outline(card, sel ? GOLD : C(PolicySystem.KindColor(kind)));
            string label = "<color=" + PolicySystem.KindColor(kind) + ">［" + PolicySystem.KindName(kind) + "］</color> ";
            label += pi >= 0
                ? "<b>" + PolicySystem.Policy(pi).jpName + "</b>　<size=88%><color=#9c95b4>" + PolicySystem.Policy(pi).desc + "</color></size>"
                  + (PolicySystem.IsObsolete(pi) ? "　<color=#e08a3c>陳腐化(効果半減)</color>" : "")
                : "<color=#6f6889>空き</color>";
            var t = Text(card.rectTransform, label, 12f, TEXT, TextAlignmentOptions.Left);
            Place(t.rectTransform, 12, 9, w - 90, 20);
            var bt = card.gameObject.AddComponent<Button>(); bt.targetGraphic = card;
            bt.onClick.AddListener(() => { selectedPolicySlot = (selectedPolicySlot == si) ? -1 : si; RefreshSurfacePanel(); });
            if (pi >= 0)
            {
                var rm = PrimaryButton(card, "外す", PANEL, C("#e05a5a"), () => { PolicySystem.TrySlot(si, -1); RefreshSurfacePanel(); });
                Place((RectTransform)rm.transform, w - 74, 5, 60, 26);
            }
            y += 40;
        }
        y += 6;

        // ── 手札 ──
        var hh = Text(c, "◆ 手札（時代が進むと増える。色の合うスロットにだけ差せる）", 12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(hh.rectTransform, 4, y, w - 8, 18); y += 22;
        for (int i = 0; i < PolicySystem.PolicyCount; i++)
        {
            int pi = i; var p = PolicySystem.Policy(i);
            bool unlocked = PolicySystem.IsUnlocked(i);
            bool active = PolicySystem.IsActive(i);
            string col = PolicySystem.KindColor(p.kind);
            var card = Panel(c, "P_" + i, active ? PANEL2 : CARD);
            Place(card.rectTransform, 0, y, w - 6, 38);
            Outline(card, active ? C(col) : (unlocked ? LINE : C("#241f33")));
            string head = "<color=" + (unlocked ? col : "#4a4560") + ">■" + PolicySystem.KindName(p.kind) + "</color> "
                + (unlocked ? "<b>" + p.jpName + "</b>" : "<color=#6f6889>" + p.jpName + "</color>")
                + (active ? "　<color=#5cc47c>差してある</color>" : "")
                + (unlocked && PolicySystem.IsObsolete(i) ? "　<color=#e08a3c>陳腐化</color>" : "")
                + (unlocked ? "" : "　<size=88%><color=#6f6889>" + EraSystem.EraName(p.era) + "から</color></size>");
            var t1 = Text(card.rectTransform, head, 12f, TEXT, TextAlignmentOptions.TopLeft);
            Place(t1.rectTransform, 12, 5, w - 30, 18);
            var t2 = Text(card.rectTransform, "<size=92%><color=#9c95b4>" + p.desc + "</color></size>", 11.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(t2.rectTransform, 12, 21, w - 30, 16);
            if (unlocked && !active)
            {
                var bt = card.gameObject.AddComponent<Button>(); bt.targetGraphic = card;
                bt.onClick.AddListener(() =>
                {
                    if (selectedPolicySlot < 0) { Debug.LogWarning("⚠️ 先に差したいスロットを選んでください。"); return; }
                    if (PolicySystem.TrySlot(selectedPolicySlot, pi)) selectedPolicySlot = -1;
                    RefreshSurfacePanel();
                });
            }
            y += 42;
        }

        c.sizeDelta = new Vector2(0f, Mathf.Max(y + 8, 80));
    }

    // 🎖️ 属性ツリー（S2）。偉業＝レガシーの道で得た点を、軸ごとに4段まで恒久強化に変える。
    private void RefreshAttrPanel()
    {
        var c = attrContainer; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = attrW, y = 0f;

        var h0 = Text(c, "◆ 属性（偉業を達成すると、その軸の点が入る。小1点／大2点）"
            + "　<size=88%><color=#9c95b4>取得 " + AttributeSystem.TakenCount + "/24・手持ち " + AttributeSystem.TotalPoints + "</color></size>",
            12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(h0.rectTransform, 4, y, w - 8, 18); y += 20;
        var h1 = Text(c, "<size=92%><color=#9c95b4>点は<b>軸ごとに別</b>。通った道のぶんしか伸びない（＝やったことが形になる）。時代をまたいで残ります。</color></size>",
            11.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(h1.rectTransform, 4, y, w - 8, 18); y += 24;

        for (int a = 0; a < AttributeSystem.AxisCount; a++)
        {
            var ax = (AttributeSystem.Axis)a;
            string col = AttributeSystem.AxisColor(ax);
            var ah = Text(c, "<color=" + col + ">■ " + AttributeSystem.AxisName(ax) + "</color>"
                + "　<size=88%><color=#9c95b4>手持ち " + AttributeSystem.Points(ax) + "／累計 " + AttributeSystem.Earned(ax)
                + "　" + AttributeSystem.AxisDesc(ax) + "</color></size>", 12.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(ah.rectTransform, 4, y, w - 8, 18); y += 22;

            for (int t = 0; t < AttributeSystem.Tiers; t++)
            {
                int ai = a, ti = t;
                var nd = AttributeSystem.Node(ax, t);
                bool got = AttributeSystem.Taken(ax, t);
                string why; bool can = AttributeSystem.CanTake(ax, t, out why);
                var card = Panel(c, "A_" + a + "_" + t, got ? PANEL2 : CARD);
                Place(card.rectTransform, 14, y, w - 20, 34);
                Outline(card, got ? C(col) : (can ? GOLD : LINE));
                var t1 = Text(card.rectTransform, "<size=88%><color=#6f6889>" + (t + 1) + "段</color></size>　"
                    + (got ? "<color=" + col + ">" + nd.jpName + "</color>" : nd.jpName)
                    + "　<size=90%><color=#9c95b4>" + nd.desc + "</color></size>"
                    + (got ? "　<color=#5cc47c>取得済</color>" : ""), 12f, TEXT, TextAlignmentOptions.Left);
                Place(t1.rectTransform, 12, 8, w - 110, 20);
                if (!got)
                {
                    var bt = PrimaryButton(card, can ? "取る" : "×", can ? PANEL2 : PANEL, can ? C(col) : C("#4a4560"),
                        () => { if (AttributeSystem.TryTake((AttributeSystem.Axis)ai, ti)) RefreshSurfacePanel(); });
                    Place((RectTransform)bt.transform, w - 92, 4, 62, 26);
                    if (!can) AddTooltip(card.gameObject, why);
                }
                y += 38;
            }
            y += 8;
        }
        c.sizeDelta = new Vector2(0f, Mathf.Max(y + 8, 80));
    }

    /// <summary>
    /// 🜏 習合の欄。**選べるときだけ**3枚のカードを出し、ふだんは継いだ血の1行だけにする。
    /// ⚠ 常に選択肢を並べると「いつでも取れる」ように見える。取れるのは時代の変わり目だけ。
    /// </summary>
    private float AddSyncretismSection(RectTransform c, float w, float y)
    {
        var h1 = Text(c, "◆ 習合　<size=88%>" + SyncretismSystem.Summary + "</size>",
            12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(h1.rectTransform, 4, y, w - 8, 18); y += 22;

        if (!SyncretismSystem.Pending)
        {
            var note = Text(c, "<color=#6f6889>他の魔王の系統は<b>時代が変わった直後</b>に1つだけ継げる"
                + "（排除した相手の系統は半額）。</color>", 11f, MUTED, TextAlignmentOptions.TopLeft);
            Place(note.rectTransform, 4, y, w - 8, 30); y += 34;
            return y;
        }

        var call = Text(c, "<color=#ffd24a>◆ 習合の機 ― 1つだけ継げる。見送ってもよい</color>",
            12f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(call.rectTransform, 4, y, w - 8, 18); y += 22;

        for (int i = 0; i < SyncretismSystem.Count; i++)
        {
            int li = i;
            var d = SyncretismSystem.Get(i);
            bool had = SyncretismSystem.Has(i);
            bool dead = SyncretismSystem.IsRivalDefeated(i);
            int cost = SyncretismSystem.CostOf(i);
            string why; bool ok = SyncretismSystem.CanAdopt(i, out why);

            var card = Panel(c, "Sync_" + i, CARD);
            Place(card.rectTransform, 0, y, w - 6, 62); Outline(card, had ? GREEN : (ok ? C(d.colorHex) : LINE));
            var nm = Text(card.rectTransform, "<color=" + d.colorHex + ">" + d.jpName + "</color>"
                + (dead ? " <size=84%><color=#5cc47c>（排除済 ― 半額）</color></size>" : ""),
                13f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm.rectTransform, 12, 6, w - 150, 18);
            var ds = Text(card.rectTransform, "<size=92%><color=#9c95b4>" + d.desc + "</color></size>",
                11f, MUTED, TextAlignmentOptions.TopLeft);
            Place(ds.rectTransform, 12, 26, w - 150, 32);
            if (had)
            {
                var t = Text(card.rectTransform, "<color=#5cc47c>継承済</color>", 11.5f, GREEN, TextAlignmentOptions.TopRight);
                Place(t.rectTransform, w - 140, 20, 126, 18);
            }
            else
            {
                var b = PrimaryButton(card, "継ぐ " + cost + " 威名", ok ? PANEL2 : PANEL, ok ? C(d.colorHex) : C("#4a4560"),
                    () => { if (SyncretismSystem.TryAdopt(li)) RefreshSurfacePanel(); });
                Place((RectTransform)b.transform, w - 140, 18, 126, 26);
                if (!ok) AddTooltip(((RectTransform)b.transform).gameObject, why);
            }
            y += 66;
        }
        var skip = PrimaryButton(c, "見送る（自分の血のまま）", PANEL2, MUTED,
            () => { SyncretismSystem.Skip(); RefreshSurfacePanel(); });
        Place((RectTransform)skip.transform, 4, y, 220, 26); y += 34;
        return y;
    }

    private void RefreshEraPanel()
    {
        var c = eraContainer; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = eraW, y = 0f;

        // ── 現在の時代 ──
        var head = Panel(c, "EraHead", CARD);
        Place(head.rectTransform, 0, y, w - 6, 78); Outline(head, C("#c9a8ff"));
        var t1 = Text(head.rectTransform, "<color=#c9a8ff>" + EraSystem.EraName(EraSystem.Current) + "</color>"
            + "　<size=88%><color=#9c95b4>進行 " + EraSystem.Progress + "/" + EraSystem.Need
            + "／世界水準+" + EraSystem.TierBias.ToString("0.0") + "</color></size>", 14, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(t1.rectTransform, 12, 8, w - 30, 20);
        var t2 = Text(head.rectTransform, "<size=92%><color=#9c95b4>" + EraSystem.EraDesc(EraSystem.Current) + "</color></size>", 11.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(t2.rectTransform, 12, 30, w - 30, 18);
        var bar = Panel(head, "Bar", PANEL2);
        Place(bar.rectTransform, 12, 54, w - 34, 12); Outline(bar, LINE);
        var fill = Panel(bar, "Fill", C("#c9a8ff"));
        Place(fill.rectTransform, 0, 0, (w - 34) * EraSystem.Progress / (float)EraSystem.Need, 12);
        y += 86;

        // ── 🜏 習合（G-5）：時代の変わり目に他の魔王の系統を1つ継ぐ ──
        y = AddSyncretismSection(c, w, y);

        // ── ☄️ 災厄 ──
        if (EraSystem.CrisisActive)
        {
            var ch2 = Text(c, EraSystem.CrisisPolicy < 0
                ? "<color=#e05a5a>◆ 災厄 ― 時代の終わりが近い。負の政策を1つ選ばなければならない</color>"
                : "<color=#e08a3c>◆ 災厄『" + EraSystem.Crisis(EraSystem.CrisisPolicy).jpName + "』" + EraSystem.Crisis(EraSystem.CrisisPolicy).desc + "</color>",
                12.5f, CRIMSON, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(ch2.rectTransform, 4, y, w - 8, 18); y += 22;
            if (EraSystem.CrisisPolicy >= 0 && !EraSystem.CrisisMitigated)
            {
                var mb = PrimaryButton(c, "対抗策『" + EraSystem.MitigateName(EraSystem.CrisisPolicy) + "』 " + EraSystem.MitigateCost + "DP",
                    PANEL2, C("#e3a94a"), () => { if (EraSystem.TryMitigate()) RefreshSurfacePanel(); });
                Place((RectTransform)mb.transform, 0, y, 320, 28);
                var mn = Text(c, "<size=90%><color=#9c95b4>手を打つと災厄の影響が半分になり、凌ぎ切ると次の時代に文化の属性+1</color></size>",
                    11f, MUTED, TextAlignmentOptions.TopLeft);
                Place(mn.rectTransform, 328, y + 6, w - 336, 18);
                y += 34;
            }
            else if (EraSystem.CrisisMitigated)
            {
                var mn = Text(c, "<color=#5cc47c>◆ 対抗策『" + EraSystem.MitigateName(EraSystem.CrisisPolicy) + "』を打った（影響は半分）</color>",
                    11.5f, GREEN, TextAlignmentOptions.TopLeft);
                Place(mn.rectTransform, 4, y, w - 8, 18); y += 24;
            }
            if (EraSystem.CrisisPolicy < 0)
                for (int i = 0; i < EraSystem.CrisisCount; i++)
                {
                    int ci = i; var cd = EraSystem.Crisis(i);
                    var card = Panel(c, "C_" + i, CARD);
                    Place(card.rectTransform, 0, y, w - 6, 38); Outline(card, C("#e05a5a"));
                    var n1 = Text(card.rectTransform, "<color=#e05a5a>" + cd.jpName + "</color>　<size=92%><color=#9c95b4>" + cd.desc + "</color></size>",
                        12f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                    Place(n1.rectTransform, 12, 9, w - 30, 20);
                    var bt = card.gameObject.AddComponent<Button>(); bt.targetGraphic = card;
                    bt.onClick.AddListener(() => { if (EraSystem.TryChooseCrisisPolicy(ci)) RefreshSurfacePanel(); });
                    y += 42;
                }
            y += 8;
        }

        // ── ⛓️ 枷（段6）：時代の頭のターンだけ選べる・その時代は外せない ──
        {
            bool open = FetterSystem.IsOpen;
            var fh = Text(c, "◆ 枷 " + FetterSystem.Chosen.Count + "/" + FetterSystem.MaxChosen + "（この時代に背負う。重さ " + FetterSystem.Weight
                + "。背負い切ると 研究点 +" + (4 * FetterSystem.Weight) + "・DP +" + (300 * FetterSystem.Weight) + "）"
                + (open ? "　<color=#9c95b4>押して背負う／もう一度押して外す</color>" : "　<color=#6f6889>この時代のあいだは外せない</color>"),
                12.5f, CRIMSON, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(fh.rectTransform, 4, y, w - 8, 18); y += 22;
            if (!open && FetterSystem.Chosen.Count == 0)
            {
                var n0 = Text(c, "<color=#6f6889>この時代は枷を背負っていません。次に選べるのは次の時代の頭です。</color>", 11.5f, FAINT, TextAlignmentOptions.TopLeft);
                Place(n0.rectTransform, 8, y, w - 16, 20); y += 26;
            }
            for (int i = 0; i < FetterSystem.Count; i++)
            {
                bool on = FetterSystem.Has(i);
                if (!open && !on) continue;   // 時代の途中は背負っている枷だけ
                int fi = i; var fd = FetterSystem.Get(i);
                var card = Panel(c, "F_" + i, on ? PANEL2 : CARD);
                Place(card.rectTransform, 0, y, w - 6, 38); Outline(card, on ? CRIMSON : LINE);
                string bars = new string('■', fd.weight) + new string('□', 3 - fd.weight);
                var n1 = Text(card.rectTransform, (on ? "◆ " : "・ ") + "<color=" + (on ? "#df5a5a" : "#ece8f5") + ">" + fd.jpName + "</color>"
                    + "　<size=92%><color=#9c95b4>" + fd.desc + "</color></size>　<size=85%><color=#df5a5a>" + bars + "</color></size>",
                    12f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(n1.rectTransform, 12, 9, w - 30, 20);
                if (open)
                {
                    var bt = card.gameObject.AddComponent<Button>(); bt.targetGraphic = card;
                    bt.onClick.AddListener(() =>
                    {
                        string why;
                        if (FetterSystem.TryToggle(fi, out why)) RefreshSurfacePanel();
                        else NotifySystem.Push(why, NotifySystem.Kind.Story);
                    });
                }
                y += 42;
            }
            y += 8;
        }

        // ── 📜 誓約 ──
        var dh = Text(c, "◆ 誓約 " + EraSystem.Chosen.Count + "/" + EraSystem.MaxChosen
            + "（大偉業で解禁。押して選ぶ／もう一度押して外す）", 12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(dh.rectTransform, 4, y, w - 8, 18); y += 22;
        if (EraSystem.Unlocked.Count == 0)
        {
            var n = Text(c, "<color=#6f6889>まだありません。大偉業を達成すると1枚ずつ解禁されます。</color>", 11.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(n.rectTransform, 8, y, w - 16, 20); y += 26;
        }
        foreach (int i in EraSystem.Unlocked)
        {
            int di = i; var d = EraSystem.Dedication(i);
            bool on = EraSystem.HasDedication(i);
            var card = Panel(c, "D_" + i, on ? PANEL2 : CARD);
            Place(card.rectTransform, 0, y, w - 6, 38); Outline(card, on ? C(d.colorHex) : LINE);
            var n1 = Text(card.rectTransform, (on ? "◆ " : "・ ") + "<color=" + d.colorHex + ">" + d.jpName + "</color>"
                + "　<size=92%><color=#9c95b4>" + d.desc + "</color></size>", 12f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(n1.rectTransform, 12, 9, w - 30, 20);
            var bt = card.gameObject.AddComponent<Button>(); bt.targetGraphic = card;
            bt.onClick.AddListener(() => { if (EraSystem.TryChooseDedication(di)) RefreshSurfacePanel(); });
            y += 42;
        }
        y += 8;

        // ── 🏅 偉業 ──
        var th = Text(c, "◆ 偉業（この時代の目標。達成すると時代が進む）", 12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(th.rectTransform, 4, y, w - 8, 18); y += 22;
        foreach (var t in EraSystem.CurrentTriumphs())
        {
            bool done = EraSystem.IsAchieved(t.id);
            var card = Panel(c, "T_" + t.id, CARD);
            Place(card.rectTransform, 0, y, w - 6, 46); Outline(card, done ? C("#5cc47c") : (t.major ? GOLD : LINE));
            var rw2 = new System.Text.StringBuilder();
            if (t.dp > 0) rw2.Append("<color=#e3a94a>+" + t.dp + "DP</color> ");
            if (t.mat > 0) rw2.Append("<color=#57c3ab>+" + t.mat + "素材</color> ");
            if (t.rp > 0) rw2.Append("<color=#8cb8e6>+" + t.rp + "RP</color> ");
            if (t.emo > 0) rw2.Append("<color=#c04a6a>+" + t.emo + "感情</color> ");
            if (t.fame > 0) rw2.Append("<color=#e05a5a>+" + t.fame + "名声</color> ");
            var n1 = Text(card.rectTransform, (done ? "<color=#5cc47c>達成</color> " : (t.major ? "<color=#e3c34a>大偉業</color> " : "<color=#9c95b4>偉業</color> ")) + t.cond,
                12f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(n1.rectTransform, 12, 5, w - 30, 20);
            var n2 = Text(card.rectTransform, "<size=90%>" + rw2 + (t.major ? "<color=#e3c34a>＋誓約が1枚解禁</color>" : "")
                + "　<color=#6f6889>進行+" + EraSystem.ProgressOf(t) + "</color></size>", 11f, MUTED, TextAlignmentOptions.TopLeft);
            Place(n2.rectTransform, 12, 25, w - 30, 18);
            y += 50;
        }
        c.sizeDelta = new Vector2(0f, Mathf.Max(y + 8, 80));
    }

    /// <summary>🗺️ 勢力：自分の拠点と他の魔王の一覧。押すとその場所へ飛ぶ（広い盤で迷子にならないため）。</summary>
    private void RefreshSurfaceStatus()
    {
        var c = statusContainer; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = statusW, y = 0f;

        var h1 = Text(c, "◆ 自分の拠点 " + SettlementSystem.SettlementCount + "/" + SettlementSystem.SettlementLimit, 12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(h1.rectTransform, 4, y, w - 8, 18); y += 22;
        foreach (var r in SurfaceMap.All)
        {
            if (!r.owned || r.settle == SurfaceMap.Settle.None) continue;
            int rid = r.id;
            var card = Panel(c, "S_" + rid, CARD);
            Place(card.rectTransform, 0, y, w - 6, 46); Outline(card, LINE2);
            int net = SettlementSystem.NetHappy(rid);
            var t = Text(card.rectTransform,
                (r.settle == SurfaceMap.Settle.City ? "<color=#e3c34a>都市</color> " : "<color=#8cb8e6>拠点</color> ") + r.name
                + "\n<size=90%><color=#9c95b4>人口" + r.pop + "／版図" + SettlementSystem.TerritoryCount(rid) + "／"
                + (net < 0 ? "<color=#e05a5a>不満" + (-net) + "</color>" : "幸福+" + net)
                + (r.celebrateTurns > 0 ? " <color=#5cc47c>祝祭</color>" : "") + "</color></size>",
                12f, TEXT, TextAlignmentOptions.TopLeft);
            Place(t.rectTransform, 12, 5, w - 30, 38);
            var bt = card.gameObject.AddComponent<Button>(); bt.targetGraphic = card;
            bt.onClick.AddListener(() =>
            {
                selectedRegionId = rid;
                if (surfaceView != null) { surfaceView.SetSelected(rid); surfaceView.CenterOn(rid); }
                RefreshSurfacePanel();
            });
            y += 50;
        }
        if (SettlementSystem.SettlementCount == 0)
        {
            var n = Text(c, "<color=#6f6889>まだ拠点がありません。領域を支配して『拠点を築く』と、ここに並びます。</color>", 11.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(n.rectTransform, 8, y, w - 16, 32); y += 36;
        }

        y += 10;
        var h2 = Text(c, "◆ 他の魔王 " + RivalLords.AliveCount + "/" + RivalLords.Count + " 存命", 12.5f, CRIMSON, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(h2.rectTransform, 4, y, w - 8, 18); y += 22;
        for (int i = 0; i < RivalLords.Count; i++)
        {
            var rv = RivalLords.Get(i);
            int home = RivalLords.HomeOf(i);
            var card = Panel(c, "R_" + i, CARD);
            Place(card.rectTransform, 0, y, w - 6, 46); Outline(card, C(rv.colorHex));
            var t = Text(card.rectTransform,
                "<color=" + rv.colorHex + ">" + rv.name + "</color> <size=88%><color=#9c95b4>" + rv.title + "</color></size>"
                + (rv.defeated ? " <color=#5cc47c>[排除]</color>" : "")
                + "\n<size=90%><color=#9c95b4>力 " + rv.power.ToString("0") + "／" + RivalLords.TerritoryOf(i) + "領</color></size>",
                12f, TEXT, TextAlignmentOptions.TopLeft);
            Place(t.rectTransform, 12, 5, w - 30, 38);
            if (home >= 0)
            {
                var bt = card.gameObject.AddComponent<Button>(); bt.targetGraphic = card;
                bt.onClick.AddListener(() =>
                {
                    selectedRegionId = home;
                    if (surfaceView != null) { surfaceView.SetSelected(home); surfaceView.CenterOn(home); }
                    RefreshSurfacePanel();
                });
            }
            y += 50;
        }
        c.sizeDelta = new Vector2(0f, Mathf.Max(y + 8, 80));
    }

    private void RefreshSurfaceHeader()
    {
        // ⏳ いまが何ターンの後半なのかを地上側にも出す（迷宮の上部バーは畳まれていて見えない）
        if (surfaceTurnText != null && turn != null)
            SetTxt(surfaceTurnText, "地上　<size=80%><color=#8cb8e6>第" + turn.CurrentTurn + "ターン 後半</color></size>");
        {
            var y = SurfaceMap.YieldSummary();
            var dy = DistrictCatalog.TotalYields();
            // 💰 段G：総量は `RefreshSurfaceResChips` が数え上げる。ここは**括弧の増分（地上から毎ターン入る量）**と小札だけ。
            //   ⚠ 色は `UITheme` の産出の色と同じ意味で使う。ここだけ違う色にすると読み方が分かれる。
            int happyNow = 0;
            foreach (var rg2 in SurfaceMap.All)
                if (rg2.owned && rg2.settle != SurfaceMap.Settle.None) happyNow += SettlementSystem.HappyOf(rg2.id);
            SetDelta(surfDpDelta, y.dp + dy.dp);
            SetDelta(surfRpDelta, y.rp + dy.rp);
            SetDelta(surfMatDelta, y.mat + dy.mat);
            SetDelta(surfFameDelta, y.fame);
            if (surfDomText != null) SetTxt(surfDomText, SurfaceMap.OwnedCount + "/" + (SurfaceMap.Count - 1));
            if (surfProdText != null) SetTxt(surfProdText, ProductionSystem.TotalProduction.ToString());
            if (surfHappyText != null)
            {
                SetTxt(surfHappyText, (happyNow > 0 ? "+" : "") + happyNow);
                surfHappyText.color = happyNow < 0 ? CRIMSON : UITheme.Happy;
            }
            if (surfSettleText != null)
            {
                SetTxt(surfSettleText, SettlementSystem.SettlementCount + "/" + SettlementSystem.SettlementLimit);
                surfSettleText.color = SettlementSystem.OverLimit > 0 ? CRIMSON : UITheme.Influence;
            }
            if (surfSettleTip != null) surfSettleTip.tip = "拠点の数／上限\n" + SettlementSystem.HeaderLine();
        }
        if (surfaceRivalText != null)
        {
            int unassigned = 0;
            foreach (var rg in SurfaceMap.All)
                if (rg.owned && !rg.isOcean && rg.type != SurfaceMap.RegionType.Gate && SettlementSystem.SettlementOf(rg.id) < 0) unassigned++;
            var rivalTxt = new System.Text.StringBuilder();
            for (int i = 0; i < RivalLords.Count; i++)
            {
                var rv = RivalLords.Get(i);
                rivalTxt.Append("  <color=" + rv.colorHex + ">" + rv.name + "</color>");
                rivalTxt.Append(rv.defeated ? "<color=#5cc47c>[排除]</color>"
                    : "<size=88%>(力" + rv.power.ToString("0") + "/" + RivalLords.TerritoryOf(i) + "領)</size>");
            }
            // ⚠ 大事な順に左から（切れても頭は読める）：未編入の警告 → 時代 → 他の魔王 → 政策…
            string warn = unassigned > 0 ? "<color=#e08a3c>未編入の辺境 " + unassigned + "（産出しない）</color>　" : "";
            string rivals = "<color=#e05a5a>◆他の魔王 " + RivalLords.AliveCount + "/" + RivalLords.Count + "</color>" + rivalTxt;
            SetTxt(surfaceRivalText, warn + EraSystem.HeaderLine() + "　" + rivals + "　" + PolicySystem.HeaderLine()
                + "　" + AttributeSystem.HeaderLine() + "　" + DiplomacySystem.HeaderLine() + "　" + VictorySystem.HeaderLine()
                + "　" + NarrativeSystem.HeaderLine());
            if (surfStatusTip != null)
                surfStatusTip.tip = warn + EraSystem.HeaderLine() + "\n" + rivals + "\n" + PolicySystem.HeaderLine()
                    + "\n" + AttributeSystem.HeaderLine() + "\n" + DiplomacySystem.HeaderLine() + "\n" + VictorySystem.HeaderLine()
                    + "\n" + NarrativeSystem.HeaderLine() + "\n" + SettlementSystem.HeaderLine();
        }
    }

    // 🗺️ 地上ツリー（Civの社会制度に相当。地上を耕すと天啓が付いて安くなる）
    /// <summary>🌳 地上ツリーの中身。迷宮ツリーと同じ `BuildTreeGraph` を呼ぶ（見た目が分岐しない）。</summary>
    private void RefreshSurfaceTree()
    {
        if (surfaceTreeGraph == null) return;
        if (surfaceTreeStatus != null) surfaceTreeStatus.text = TreeStatusLine();
        SetTreeChips(surfaceTreeChips);
        BuildTreeGraph(surfaceTreeGraph, surfaceTreeGraphW,
            new[] { ResearchField.Surface, ResearchField.Art },
            () => { RefreshSurfaceTree(); RefreshSurfacePanel(); });   // 産出や上限が変わるので帯も更新する
    }

    /// <summary>左の窓に出す「ツリーの入口」。中身は全画面（狭い窓では前提の線が引けない）。</summary>
    private void RefreshSurfaceTreeGate()
    {
        var c = surfaceTreeRoot; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = surfaceTreeW, y = 0f;
        var head = Text(c, "◆ 地上ツリー　<size=88%><color=#9c95b4>" + TreeStatusLine() + "</color></size>",
            14, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(head.rectTransform, 4, y, w - 8, 40); y += 48;

        var sur = ResearchCatalog.ByField(ResearchField.Surface);
        var art = ResearchCatalog.ByField(ResearchField.Art);
        int doneS = 0, doneA = 0;
        foreach (var n in sur) if (ResearchState.IsResearched(n.id)) doneS++;
        foreach (var n in art) if (ResearchState.IsResearched(n.id)) doneA++;
        var body = Text(c, "地上研究 <b>" + doneS + "/" + sur.Count + "</b>　業の研究 <b>" + doneA + "/" + art.Count + "</b>\n"
            + "<size=88%><color=#9c95b4>地上を耕すほど天啓が付いて40%引きになる。深い段は<b>危険度</b>が要る。</color></size>",
            12.5f, TEXT, TextAlignmentOptions.TopLeft);
        Place(body.rectTransform, 4, y, w - 8, 46); y += 56;

        var b2 = PrimaryButton(c, "ツリーを開く", PANEL2, GOLD, OpenSurfaceTree);
        Place((RectTransform)b2.transform, 4, y, w - 8, 34); y += 44;
        c.sizeDelta = new Vector2(0f, y + 12);
    }

    // ============ ⬡ ヘクス盤の描画（厚みのある板＝2Dのまま奥行きを出す） ============
    // Civ の盤に寄せるため、各ヘクスを「天面＋側面」の2枚で描き、縦を圧縮して俯瞰にする。
    // 地形ごとに高さ(lift)が違うので、平面のまま起伏として読める。
    private const float HexSquash = 0.76f;   // 縦の圧縮＝俯瞰の傾き
    private const float HexDepth = 13f;      // 板の厚み

    private static int TerrainLift(SurfaceMap.Terrain t)
    {
        switch (t)
        {
            case SurfaceMap.Terrain.Mountain: return 22;
            case SurfaceMap.Terrain.Hills: return 12;
            case SurfaceMap.Terrain.Forest: return 8;
            case SurfaceMap.Terrain.Plains: return 3;
            case SurfaceMap.Terrain.Marsh: return 1;
            default: return 4;
        }
    }
    private static string TerrainSide(SurfaceMap.Terrain t)
    {
        switch (t)
        {
            case SurfaceMap.Terrain.Plains: return "#4a5433";
            case SurfaceMap.Terrain.Forest: return "#284630";
            case SurfaceMap.Terrain.Hills: return "#54462c";
            case SurfaceMap.Terrain.Mountain: return "#4c4a5c";
            case SurfaceMap.Terrain.Marsh: return "#2c4746";
            default: return "#3d3543";
        }
    }

    private void RefreshHexMap()
    {
        var root = hexMapRoot; if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--) { var g = root.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }

        // ⚠ W1の暫定描画。uGUIは**1タイルにつきGameObject 16個**を作るので、盤を広げると破綻する
        //    （実測: 271タイル=4,441個/61ms、1万タイル=約16万個/2.3秒。しかもクリックのたびに作り直す）。
        //    W2でHexagonal Tilemapへ移すまでは、**選択中のタイルを中心に窓を切って**その中だけ描く。
        //    → [[civ7-roadmap]] の W2。
        int mw = SurfaceMap.MapW, mh = SurfaceMap.MapH;
        int wc = Mathf.Min(mw, 30), wr = Mathf.Min(mh, 22);
        var focus = SurfaceMap.Get(selectedRegionId >= 0 ? selectedRegionId : SurfaceMap.IndexOfCenter());
        int cc = focus.col, cr = focus.row;

        float size = Mathf.Clamp(root.rect.height / ((wr * 1.5f * HexSquash) + 2.4f), 20f, 78f);
        float cx = root.rect.width * 0.5f, cy = root.rect.height * 0.5f;
        // ※rootは中心ピボット。Placeは左上原点なので、そのまま中心オフセットで並べる。
        var sel = selectedKinId >= 0 ? KinRoster.Of(selectedKinId) : null;

        // 窓の中のタイルを集める（行が奥＝画面上から手前へ＝画家のアルゴリズム）
        var order = new List<SurfaceMap.Region>(wc * wr);
        for (int row = cr - wr / 2; row <= cr + wr / 2; row++)
        {
            if (row < 0 || row >= mh) continue;
            for (int dc = -wc / 2; dc <= wc / 2; dc++)
            {
                int id = SurfaceMap.IdAt(cc + dc, row);
                if (id >= 0) order.Add(SurfaceMap.Get(id));
            }
        }

        foreach (var r in order)
        {
            int rid = r.id;
            // 東西がループするので、中心からの**符号つき最短の列差**で並べる
            int dcol = r.col - cc;
            if (mw > 0) { while (dcol > mw / 2) dcol -= mw; while (dcol < -mw / 2) dcol += mw; }
            float px = size * 1.7320508f * (dcol + 0.5f * (r.row & 1) - 0.5f * (cr & 1));
            float py = size * 1.5f * (r.row - cr) * HexSquash;
            bool disc = SurfaceMap.IsDiscovered(rid);
            float lift = disc ? TerrainLift(r.terrain) : 2f;
            float hw = size * 1.7320508f, hh = size * 2f * HexSquash;
            float x = cx + px - hw * 0.5f, y = cy + py - hh * 0.5f - lift;

            var cell = NewRect("Hex_" + rid, root);
            Place(cell, x, y, hw, hh + HexDepth + lift);

            // 側面（板の厚み）：天面と同じ六角形を下にずらして暗く塗る
            var side = new GameObject("Side", typeof(RectTransform), typeof(Image));
            side.transform.SetParent(cell, false);
            var sr = (RectTransform)side.transform;
            Place(sr, 0, HexDepth + lift, hw, hh);
            var si = side.GetComponent<Image>();
            si.sprite = MarkerArt.Hexagon();
            si.color = disc ? C(TerrainSide(r.terrain)) : C("#100d18");
            si.raycastTarget = false;

            // 天面
            var top = new GameObject("Top", typeof(RectTransform), typeof(Image));
            top.transform.SetParent(cell, false);
            Place((RectTransform)top.transform, 0, 0, hw, hh);
            var ti = top.GetComponent<Image>();
            ti.sprite = MarkerArt.Hexagon();
            ti.color = disc ? C(SurfaceMap.TerrainColor(r.terrain)) : C("#171325");

            // 所有者の縁取り
            var ring = new GameObject("Ring", typeof(RectTransform), typeof(Image));
            ring.transform.SetParent(cell, false);
            Place((RectTransform)ring.transform, 0, 0, hw, hh);
            var ri = ring.GetComponent<Image>();
            ri.sprite = MarkerArt.HexRing();
            ri.raycastTarget = false;
            ri.color = !disc ? C("#241d33")
                : (selectedRegionId == rid ? GOLD : C(SurfaceMap.OwnerColor(r.owner)));

            if (!disc)
            {
                var q = Text(cell, "<color=#3a3350>?</color>", 20, FAINT, TextAlignmentOptions.Center, FontStyles.Bold);
                Place(q.rectTransform, 0, hh * 0.34f, hw, 26);
                continue;
            }

            if (r.isOcean)
            {
                var sn = Text(cell, "<color=#4a7ba8>" + r.name + "</color>", 9f, MUTED, TextAlignmentOptions.Center);
                Place(sn.rectTransform, 4, hh * 0.38f, hw - 8, 13);
                continue;
            }
            // 名前・所有者・守り（天面の中に収める）
            var nm = Text(cell, r.name, 10.5f, TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
            // ⚠ 折り返すと下の行（所有者・守り・拠点・マーク）に食い込んで重なる。
            //    折り返しは残したまま**自動縮小**にすると、14pxの枠に収まるまで字が縮んで実質1行になる。
            //    （wrapping=false + autoSize だと縮まずに横へはみ出してヘクスの外に文字が出る＝実測で確認）
            nm.enableAutoSizing = true; nm.fontSizeMin = 6f; nm.fontSizeMax = 10.5f;
            Place(nm.rectTransform, 2, hh * 0.26f, hw - 4, 13);
            var ow = Text(cell, "<color=" + SurfaceMap.OwnerColor(r.owner) + ">" + SurfaceMap.OwnerName(r.owner) + "</color>"
                + " <size=88%><color=#9c95b4>" + SurfaceMap.TerrainName(r.terrain) + "</color></size>", 9, MUTED, TextAlignmentOptions.Center);
            Place(ow.rectTransform, 2, hh * 0.26f + 12, hw - 4, 11);
            // 🏙️ 拠点／都市／未編入（C2）。行を増やすとヘクスの天面から溢れるので守り行に畳んで入れる。
            //    ⚠ ヘクスの幅は実測で約57pxしかない。1行に詰め込むと折り返して下のヘクスに文字が落ちるので、
            //      人口はバッジに畳んで「■都5」の形にしてある（詳細は右のパネルとツールチップで見せる）。
            string stag = "";
            if (r.owned)
            {
                // ※記号は使わない。UIフォントに無い記号は Fix() で置換されるか □ になる（■ は ◆ に化ける）。
                stag = r.settle == SurfaceMap.Settle.City ? " <color=#e3c34a>都" + r.pop + "</color>"
                    : r.settle == SurfaceMap.Settle.Town ? " <color=#8cb8e6>拠" + r.pop + "</color>"
                    : SettlementSystem.SettlementOf(rid) < 0 ? " <color=#c08a4a>未</color>" : "";
            }
            var df = Text(cell, (r.owned ? "守" : "防") + SurfaceMap.DefenseOf(rid) + stag, 9.5f,
                r.owned ? GREEN : CRIMSON, TextAlignmentOptions.Center, FontStyles.Bold);
            df.enableAutoSizing = true; df.fontSizeMin = 6.5f; df.fontSizeMax = 9.5f;
            Place(df.rectTransform, 2, hh * 0.26f + 23, hw - 4, 11);

            // 資源・川・施設・砦・不満・街区・専門家・祝祭
            string marks = "";
            if (r.resource != SurfaceMap.Resource.None) marks += "<color=#e3c34a>" + SurfaceMap.ResourceName(r.resource) + "</color> ";
            if (r.river) marks += "<color=#5aa8e0>川</color> ";
            if (r.fortLevel > 0) marks += "<color=#b478e6>砦" + r.fortLevel + "</color> ";
            if (r.district >= 0) marks += "<color=" + DistrictCatalog.Get(r.district).colorHex + ">" + DistrictCatalog.Get(r.district).jpName + "</color> ";
            if (r.district2 >= 0) marks += "<color=" + DistrictCatalog.Get(r.district2).colorHex + ">" + DistrictCatalog.Get(r.district2).jpName + "</color> ";
            if (r.specialist) marks += "<color=#57c3ab>専</color> ";
            if (r.celebrateTurns > 0) marks += "<color=#5cc47c>祝祭" + r.celebrateTurns + "</color> ";
            if (r.owned && r.settle != SurfaceMap.Settle.None && SettlementSystem.NetHappy(rid) < 0)
                marks += "<color=#e05a5a>不満" + (-SettlementSystem.NetHappy(rid) * 5) + "%</color> ";
            if (marks.Length > 0)
            {
                var mk = Text(cell, marks, 8.5f, MUTED, TextAlignmentOptions.Center);
                mk.enableAutoSizing = true; mk.fontSizeMin = 6f; mk.fontSizeMax = 8.5f;
                Place(mk.rectTransform, 2, hh * 0.26f + 34, hw - 4, 11);
            }

            // 🏔️ 自然の驚異
            if (r.naturalWonder >= 0)
            {
                var nw = SurfaceGen.NaturalWonders[r.naturalWonder];
                var nt = Text(cell, "<color=" + nw.colorHex + ">▲" + nw.jpName + "</color>", 9f, GREEN, TextAlignmentOptions.Center, FontStyles.Bold);
                Place(nt.rectTransform, 2, hh * 0.26f - 38, hw - 4, 12);
            }
            // ★ 遺産（天面の上に大きく）
            if (r.wonderIndex >= 0)
            {
                var wd = WonderCatalog.Get(r.wonderIndex);
                var wt = Text(cell, "<color=" + wd.colorHex + ">◆" + wd.jpName + "</color>", 9.5f, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
                Place(wt.rectTransform, 2, hh * 0.26f - 14, hw - 4, 13);
            }
            // 他魔王の本拠地
            if (r.rivalHome >= 0)
            {
                var ht = Text(cell, "<color=#ff6a4a>◆迷宮核</color>", 9.5f, CRIMSON, TextAlignmentOptions.Center, FontStyles.Bold);
                Place(ht.rectTransform, 2, hh * 0.26f - 26, hw - 4, 13);
            }
            // 駐留・進軍
            int gar = KinRoster.GarrisonAt(rid).Count;
            if (gar > 0)
            {
                var gt = Text(cell, "<color=#8cb8e6>駐留" + gar + "</color>", 9, MUTED, TextAlignmentOptions.Center, FontStyles.Bold);
                Place(gt.rectTransform, 2, hh * 0.26f - 14, hw - 4, 12);
            }
            if (sel != null && sel.marchTarget == rid)
            {
                var at = Text(cell, "<color=#e05a5a>→進軍</color>", 9.5f, CRIMSON, TextAlignmentOptions.Center, FontStyles.Bold);
                Place(at.rectTransform, 2, hh * 0.26f - 26, hw - 4, 12);
            }

            var btn = top.AddComponent<Button>(); btn.targetGraphic = ti;
            btn.onClick.AddListener(() =>
            {
                if (mapPanZoom != null && mapPanZoom.DraggedThisPress) return;   // 盤を掴んで動かしただけなら選択しない
                selectedRegionId = rid; RefreshSurfacePanel();
            });
            string tip = r.name + "（" + SurfaceMap.TypeName(r.type) + "・" + SurfaceMap.TerrainName(r.terrain) + "）\n"
                + "所有: " + SurfaceMap.OwnerName(r.owner) + "／守り " + SurfaceMap.DefenseOf(rid);
            if (r.owned)
            {
                if (r.settle == SurfaceMap.Settle.City) tip += "\n■都市 人口" + r.pop + "／版図" + SettlementSystem.TerritoryCount(rid) + "タイル";
                else if (r.settle == SurfaceMap.Settle.Town) tip += "\n▪拠点〈" + SettlementSystem.FocusName(r.focus) + "〉人口" + r.pop;
                else
                {
                    int hm = SettlementSystem.SettlementOf(rid);
                    tip += hm >= 0 ? "\n版図: " + SurfaceMap.Get(hm).name + " の領土"
                                   : "\n未編入の辺境 ― どの拠点からも遠く、DP/素材/RPを産まない";
                }
                if (r.settle != SurfaceMap.Settle.None)
                {
                    int nh = SettlementSystem.NetHappy(rid);
                    tip += nh < 0 ? "\n不満 " + (-nh) + " ＝ 産出 " + (nh * 5) + "%" : "\n幸福 +" + nh;
                }
            }
            if (r.wonderIndex >= 0) tip += "\n◆遺産〈" + WonderCatalog.Get(r.wonderIndex).jpName + "〉" + WonderCatalog.Get(r.wonderIndex).desc;
            AddTooltip(top, tip);
        }
    }

    // ============ 選択中ヘクスの詳細（施設の建設もここで） ============
    private void RefreshRegionDetail()
    {
        var c = regionListContainer; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = regionListW, y = 0f;

        if (selectedRegionId < 0 || !SurfaceMap.IsDiscovered(selectedRegionId))
        {
            var h = Text(c, "<color=#9c95b4>左のヘクスをクリックすると、その領域の詳細と操作がここに出ます。</color>", 12, MUTED, TextAlignmentOptions.TopLeft);
            Place(h.rectTransform, 4, 6, w - 8, 40);
            c.sizeDelta = new Vector2(0f, 80); return;
        }
        var r = SurfaceMap.Get(selectedRegionId);
        var sel = selectedKinId >= 0 ? KinRoster.Of(selectedKinId) : null;
        int defNow = SurfaceMap.DefenseOf(r.id);

        var head = Panel(c, "Head", CARD); Outline(head, LINE2);
        float hy = 8f;   // head の中の縦カーソル（行ごとに足していく）
        var t1 = Text(head.rectTransform, "<color=" + SurfaceMap.OwnerColor(r.owner) + ">[" + SurfaceMap.OwnerName(r.owner) + "]</color> "
            + "<color=" + SurfaceMap.TypeColor(r.type) + ">" + r.name + "</color>"
            + (r.settle == SurfaceMap.Settle.City ? " <color=#e3c34a>■都市</color>" : r.settle == SurfaceMap.Settle.Town ? " <color=#8cb8e6>▪拠点</color>" : "")
            + (r.celebrateTurns > 0 ? " <color=#5cc47c>◆祝祭" + r.celebrateTurns + "</color>" : "")
            + (r.rivalHome >= 0 ? " <color=#ff6a4a>◆迷宮核</color>" : ""), 15, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(t1.rectTransform, 12, hy, w - 30, 20); hy += 23;
        var t2 = Text(head.rectTransform, SurfaceMap.TypeName(r.type) + "／地形 <color=#8cb8e6>" + SurfaceMap.TerrainName(r.terrain) + "</color>"
            + (r.resource != SurfaceMap.Resource.None ? "／資源 <color=#e3c34a>" + SurfaceMap.ResourceName(r.resource) + "</color>" : "")
            + (r.river ? "／<color=#5aa8e0>川</color>" : "") + (r.wonder ? "／<color=#5cc47c>自然の驚異</color>" : ""),
            11.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(t2.rectTransform, 12, hy, w - 30, 18); hy += 19;
        var t3 = Text(head.rectTransform, (r.owned ? "守り <color=#5cc47c>" : "防衛 <color=#e05a5a>") + defNow + "</color>"
            + (r.fortLevel > 0 ? "　<color=#b478e6>砦Lv" + r.fortLevel + "</color>" : "")
            + "　産出 <color=#e3a94a>+" + r.dpYield + "DP</color> <color=#57c3ab>+" + r.matYield + "素材</color>"
            + (r.rpYield > 0 ? " <color=#8cb8e6>+" + r.rpYield + "RP</color>" : "") + " <color=#e05a5a>+" + r.fameYield + "名声</color>",
            11.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(t3.rectTransform, 12, hy, w - 30, 18); hy += 19;
        if (r.wonderIndex >= 0)
        {
            var wd = WonderCatalog.Get(r.wonderIndex);
            var wt = Text(head.rectTransform, "<color=" + wd.colorHex + ">◆遺産〈" + wd.jpName + "〉</color> <size=90%>" + wd.desc + "</size>",
                11.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(wt.rectTransform, 12, hy, w - 30, 18); hy += 19;
        }

        // 🏙️ 拠点・都市・版図（C2）
        if (r.owned && !r.isOcean)
        {
            int home = SettlementSystem.SettlementOf(r.id);
            if (r.settle == SurfaceMap.Settle.None)
            {
                string belong = home >= 0
                    ? "版図（<color=#8cb8e6>" + SurfaceMap.Get(home).name + "</color> の領土 ― 産出はこの拠点の人口と不満で決まる）"
                    : "<color=#e08a3c>未編入の辺境 ― どの拠点からも遠く、DP/素材/RPを産まない</color>";
                var bt = Text(head.rectTransform, belong, 11.5f, MUTED, TextAlignmentOptions.TopLeft);
                Place(bt.rectTransform, 12, hy, w - 30, 18); hy += 21;
            }
            else
            {
                int gov = SurfaceMap.GovernanceOf(r.id);
                int net = SettlementSystem.NetHappy(r.id);
                string hd, ud;
                int hp = SettlementSystem.HappyOf(r.id, out hd);
                int up = SettlementSystem.UnhappyOf(r.id, out ud);
                var pt = Text(head.rectTransform,
                    "人口 <color=#e3c34a>" + r.pop + "/" + SurfaceMap.MaxPopOf(r.id) + "</color>"
                    + "　食料 <color=#5cc47c>" + (SurfaceMap.FoodIncome(r.id) >= 0 ? "+" : "") + SurfaceMap.FoodIncome(r.id) + "</color>"
                    + " <size=88%><color=#9c95b4>(" + r.foodStock + "/" + (8 * Mathf.Max(1, r.pop)) + ")</color></size>"
                    + "　統治力 <color=#57c3ab>" + gov + "</color>"
                    + "　版図 <color=#8cb8e6>" + SettlementSystem.TerritoryCount(r.id) + "</color>タイル"
                    + ResourceUsageText(r.id)
                    + "　産出×<color=#e3c34a>" + SurfaceMap.PopMult(r.id).ToString("0.00") + "</color>",
                    11f, MUTED, TextAlignmentOptions.TopLeft);
                Place(pt.rectTransform, 12, hy, w - 30, 18); hy += 19;
                var ht = Text(head.rectTransform,
                    "幸福 <color=#5cc47c>" + hp + "</color> − 不満 <color=#e05a5a>" + up + "</color> ＝ "
                    + (net < 0 ? "<color=#e05a5a>" + net + "（産出 " + (net * 5) + "%）</color>"
                                : "<color=#5cc47c>+" + net + "（祝祭ゲージ " + r.happyStock + "/" + SettlementSystem.CelebrateNeed(r.id) + "）</color>"),
                    11f, MUTED, TextAlignmentOptions.TopLeft);
                Place(ht.rectTransform, 12, hy, w - 30, 18); hy += 17;
                var hb = Text(head.rectTransform, "<size=88%><color=#6f6889>幸福: " + hd + "　／　不満: " + ud + "</color></size>", 10f, FAINT, TextAlignmentOptions.TopLeft);
                Place(hb.rectTransform, 12, hy, w - 30, 16); hy += 18;
                var wk = new System.Text.StringBuilder("耕作: ");
                foreach (var t in SurfaceMap.WorkedTiles(r.id)) wk.Append(t.name + "(食" + SurfaceMap.FoodOf(t) + ") ");
                var wl = Text(head.rectTransform, "<size=90%><color=#6f6889>" + wk + "</color></size>", 10f, FAINT, TextAlignmentOptions.TopLeft);
                Place(wl.rectTransform, 12, hy, w - 30, 16); hy += 20;
            }
        }

        if (!string.IsNullOrEmpty(r.lastResult))
        {
            var t4 = Text(head.rectTransform, "<color=#6f6889>前回: " + r.lastResult + "</color>", 11, FAINT, TextAlignmentOptions.TopLeft);
            Place(t4.rectTransform, 12, hy, w - 30, 16); hy += 18;
        }

        // 🗡️ 自領を見ているときも「いま何を獲れるか」を出す。
        // ⚠ 通しプレイで**序盤に完全に手が止まった**原因がここだった：
        //   戦力と勝率は「敵領を選んだとき」しか出ないが、序盤は敵領が遠くて選べない。
        //   結果、進言が「進軍させろ」と言うのに**進軍先が1つも見つからない**状態になる。
        //   正解は「まず前線の自領へ移動 → 次のターンに隣を攻める」だが、どこにも書いていなかった。
        if (r.owned && sel != null && sel.injuryTurns <= 0) ShowNextConquestHint(head, ref hy, w, sel);

        // 操作ボタン
        // ══════════ 🕳️ 盤の上のダンジョン（④・→ [[NestSystem]] [[Expedition]]）══════════
        // ⚠ 難度は**冒険者と同じ G〜S の物差し**で出す。巣専用の★を作ると、
        //   プレイヤーは「強さ」の目盛りを2つ覚え直すことになる。
        // ⚠ **勝率は出さない**（→ [[readiness-and-trade]]）。出すのは相手の事実だけ。
        {
            int nestIdx = NestSystem.IndexOfRegion(r.id);
            if (nestIdx >= 0)
            {
                var nest = NestSystem.At(nestIdx);
                var sn = nest.snap;
                var nt = Text(head.rectTransform,
                    "<color=" + sn.KindColor + ">◆" + sn.name + "</color>　<size=90%>" + sn.KindName
                    + "・<color=#d45ba8>難度 " + AdventurerAI.RankLetter(sn.tier) + "</color></size>",
                    13.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(nt.rectTransform, 12, hy, w - 30, 20); hy += 22;
                var nd = Text(head.rectTransform,
                    (nest.conquered ? "<color=#6f6889>制覇済み</color>　" : "")
                    + "<color=#9c95b4>" + sn.FloorCount + "層／守り " + sn.TotalGuards + "体／罠 " + sn.trapKind.Count
                    + "　最深部の主 <color=#e05a5a>" + MinionCatalog.Get(sn.lordIndex).jpName + "</color> Lv" + sn.lordLevel + "</color>",
                    11.5f, MUTED, TextAlignmentOptions.TopLeft);
                Place(nd.rectTransform, 12, hy, w - 30, 18); hy += 21;

                if (!nest.conquered && !Expedition.Active)
                {
                    // 立っている眷属を探す（選択中を優先し、無ければその場に居る者）
                    int leader = -1;
                    if (sel != null && SurfaceMap.HexDist(SurfaceMap.Get(sel.regionId), r) <= 1) leader = sel.individualId;
                    else { var here = KinRoster.KinAt(r.id); if (here != null) leader = here.individualId; }

                    string whyE = "入口まで進軍してください（隣接するか、その上に立つ）";
                    bool canE = leader >= 0 && Expedition.CanDeclare(leader, nestIdx, out whyE);
                    int lead2 = leader;
                    var eb = PrimaryButton(head, "遠征を宣言", canE ? BLOOD : PANEL, canE ? C("#f0d9a0") : C("#4a4560"),
                        () => { if (Expedition.Declare(lead2, nestIdx)) { OpenExpeditionWindow(); RefreshSurfacePanel(); } }, canE);
                    Place((RectTransform)eb.transform, 12, hy, 150, 28);
                    var ew = Text(head.rectTransform, canE
                        ? "<size=88%><color=#9c95b4>『" + KinRoster.Of(leader).trueName + "』が入口に立っている</color></size>"
                        : "<size=88%><color=#e08a3c>" + whyE + "</color></size>", 10.5f, FAINT, TextAlignmentOptions.TopLeft);
                    Place(ew.rectTransform, 170, hy + 6, w - 190, 18);
                    hy += 34;
                }
                else if (Expedition.Active && Expedition.Current.nestIndex == nestIdx)
                {
                    var ob = PrimaryButton(head, Expedition.Descending ? "遠征の様子を見る" : "編成を続ける",
                        PANEL2, C("#e3a94a"), () => { OpenExpeditionWindow(); RefreshSurfacePanel(); });
                    Place((RectTransform)ob.transform, 12, hy, 170, 28); hy += 34;
                }
            }
        }

        if (r.owned && r.type != SurfaceMap.RegionType.Gate)
        {
            float bx = 12f;
            // 🏘️ 拠点を築く／🏙️ 都市へ昇格
            if (r.settle == SurfaceMap.Settle.None)
            {
                string why; bool can = SettlementSystem.CanFound(r.id, out why);
                int fc2 = SettlementSystem.FoundCost();
                var nb = PrimaryButton(head, "拠点を築く " + fc2 + "DP", can ? PANEL2 : PANEL, can ? C("#e3c34a") : C("#4a4560"),
                    () => { if (SettlementSystem.TryFound(r.id)) RefreshSurfacePanel(); });
                Place((RectTransform)nb.transform, bx, hy, 168, 26); bx += 176;
                if (!can)
                {
                    var wt2 = Text(head.rectTransform, "<size=88%><color=#e08a3c>" + why + "</color></size>", 10f, FAINT, TextAlignmentOptions.TopLeft);
                    Place(wt2.rectTransform, bx, hy + 6, w - bx - 20, 16);
                }
                else if (SettlementSystem.OverLimit > 0 || SettlementSystem.SettlementCount >= SettlementSystem.SettlementLimit)
                {
                    var wt2 = Text(head.rectTransform, "<size=88%><color=#e08a3c>支配上限 " + SettlementSystem.SettlementCount + "/"
                        + SettlementSystem.SettlementLimit + " ― これ以上は全拠点に不満+1</color></size>", 10f, FAINT, TextAlignmentOptions.TopLeft);
                    Place(wt2.rectTransform, bx, hy + 6, w - bx - 20, 16);
                }
                hy += 32;
            }
            else if (r.settle == SurfaceMap.Settle.Town)
            {
                int pc = SettlementSystem.PromoteCost();
                bool ok = r.pop >= 2;
                var pb = PrimaryButton(head, "都市へ昇格 " + pc + "DP", ok ? PANEL2 : PANEL, ok ? C("#e3c34a") : C("#4a4560"),
                    () => { if (SettlementSystem.TryPromote(r.id)) RefreshSurfacePanel(); });
                Place((RectTransform)pb.transform, bx, hy, 168, 26); bx += 176;
                var pn = Text(head.rectTransform, "<size=88%><color=#9c95b4>都市になると施設と専門家を置け、版図が広がる（人口2以上が要る）</color></size>",
                    10f, FAINT, TextAlignmentOptions.TopLeft);
                Place(pn.rectTransform, bx, hy + 6, w - bx - 20, 16);
                hy += 32;
            }
            if (r.fortLevel < SurfaceMap.MaxFort)
            {
                int fc = SurfaceMap.FortCost(r.fortLevel);
                var fb = PrimaryButton(head, "砦化 " + fc + "DP", PANEL2, C("#b478e6"), () => { if (SurfaceMap.TryFortify(r.id)) RefreshSurfacePanel(); });
                Place((RectTransform)fb.transform, 12, hy, 140, 26);
            }
            if (sel != null && sel.injuryTurns <= 0 && sel.regionId != r.id)
            {
                var gb = PrimaryButton(head, "ここを守らせる", PANEL2, C("#8cb8e6"), () => { KinRoster.SetGarrison(selectedKinId, r.id); RefreshSurfacePanel(); });
                Place((RectTransform)gb.transform, 160, hy, 160, 26);
            }
            hy += 34;
        }
        else if (sel != null && sel.injuryTurns <= 0)
        {
            float ratio = defNow > 0 ? KinRoster.ArmyPower(sel) / defNow : 99f;
            string odds = ratio >= 1.25f ? "<color=#5cc47c>完勝圏</color>" : ratio >= 1.0f ? "<color=#e3a94a>辛勝圏</color>"
                : ratio >= 0.7f ? "<color=#e08a3c>敗走の恐れ</color>" : "<color=#e05a5a>壊滅の恐れ</color>";
            var od = Text(head.rectTransform, "選択中の眷属の戦力 " + KinRoster.ArmyPower(sel).ToString("0") + " → " + odds, 11.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(od.rectTransform, 12, hy, w - 30, 18); hy += 20;
            bool marching = sel.marchTarget == r.id;
            var b = PrimaryButton(head, marching ? "進軍中（取消）" : "ここへ進軍", marching ? PANEL2 : BLOOD, TEXT,
                () => { if (marching) KinRoster.SetMarchTarget(selectedKinId, -1); else KinRoster.SetMarchTarget(selectedKinId, r.id); RefreshSurfacePanel(); });
            Place((RectTransform)b.transform, 12, hy, 180, 26); hy += 34;
        }
        float headH = hy + 8;
        Place(head.rectTransform, 0, y, w - 6, headH);
        y += headH + 8;

        // 🎯 拠点の特化（Civ VII の Town Focus 9種。都市になると施設を建てるので特化は無くなる）
        if (r.owned && r.settle == SurfaceMap.Settle.Town)
        {
            var fh = Text(c, "◆ 特化（拠点は生産の代わりに1つだけ性格を選ぶ。いつでも変えられる）", 12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(fh.rectTransform, 4, y, w - 8, 18); y += 22;
            for (int i = 0; i < SettlementSystem.FocusCount; i++)
            {
                int fi = i; var fd = SettlementSystem.Focus(i);
                bool on = r.focus == i;
                var card = Panel(c, "F_" + i, on ? PANEL2 : CARD);
                Place(card.rectTransform, 0, y, w - 6, 40); Outline(card, on ? C(fd.colorHex) : LINE);
                var n1 = Text(card.rectTransform, (on ? "◆ " : "・ ") + "<color=" + fd.colorHex + ">" + fd.jpName + "</color>",
                    12.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(n1.rectTransform, 12, 6, 120, 18);
                var n2 = Text(card.rectTransform, "<size=92%><color=#9c95b4>" + fd.desc + "</color></size>", 11f, MUTED, TextAlignmentOptions.TopLeft);
                Place(n2.rectTransform, 132, 8, w - 150, 28);
                var fb2 = card.gameObject.AddComponent<Button>(); fb2.targetGraphic = card;
                fb2.onClick.AddListener(() => { if (SettlementSystem.TrySetFocus(r.id, fi)) RefreshSurfacePanel(); });
                y += 44;
            }
            y += 6;
        }

        // 🏛️ 施設（Civの地区）：**都市の版図**にだけ建てられる。隣接ボーナスを事前に見せる。
        if (r.owned && !r.isOcean)
        {
            bool asQuarter; string whyBuild;
            bool canBuild = DistrictCatalog.CanBuild(r.id, out asQuarter, out whyBuild);
            var dh = Text(c, "◆ 施設（都市の版図にのみ・置く場所で隣接ボーナスが変わる）", 12.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(dh.rectTransform, 4, y, w - 8, 18); y += 22;
            if (!canBuild && r.district < 0)
            {
                var nb2 = Text(c, "<color=#e08a3c>" + whyBuild + "</color>", 11.5f, MUTED, TextAlignmentOptions.TopLeft);
                Place(nb2.rectTransform, 8, y, w - 16, 18); y += 24;
            }
            // ⚠⚠ **人口が耕していないタイルの施設は1も産まない。**
            //   通しプレイで、これを知らずに施設5つを約3,400DP掛けて建て、産出表示が**1も動かなかった**。
            //   建てられる＝効く、ではないので、払う前にここで言う。
            if (canBuild && !IsTileWorked(r))
            {
                var warn = Text(c, "<color=#e05a5a>⚠ このタイルは人口が耕していないので、建てても産出しません。</color>"
                    + "<color=#9c95b4>　先に拠点の人口を増やすか、人口が届いているタイルに建ててください。</color>",
                    11.5f, MUTED, TextAlignmentOptions.TopLeft);
                Place(warn.rectTransform, 8, y, w - 16, 32); y += 36;
            }

            // 既にある施設（1つ目・2つ目＝街区）
            for (int slot = 0; slot < 2; slot++)
            {
                int di2 = slot == 0 ? r.district : r.district2;
                if (di2 < 0) continue;
                var d = DistrictCatalog.Get(di2);
                string detail; int adj = DistrictCatalog.Adjacency(di2, r.id, out detail);
                int sl = slot;
                bool old2 = DistrictCatalog.IsObsoleteAt(r.id, sl);   // ⏳ 古い施設は隣接ボーナスを失っている
                int eff = old2 ? 0 : adj;
                int shown = r.specialist ? eff * 2 : eff;
                var card = Panel(c, "Built" + slot, CARD);
                Place(card.rectTransform, 0, y, w - 6, 76); Outline(card, old2 ? C("#e08a3c") : C(d.colorHex));
                var n1 = Text(card.rectTransform, "<color=" + d.colorHex + ">" + d.jpName + "</color> 建設済み"
                    + (slot == 1 ? " <color=#e3c34a>［街区］</color>" : "") + (r.specialist ? " <color=#57c3ab>［専門家］</color>" : "")
                    + (old2 ? " <color=#e08a3c>［陳腐化：隣接ボーナスを失った］</color>" : ""),
                    13.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(n1.rectTransform, 12, 8, w - 30, 18);
                var n2 = Text(card.rectTransform, DistrictCatalog.YieldName(d.yield) + " <color=#5cc47c>+" + (1 + shown) + "</color>"
                    + "　<size=88%><color=#9c95b4>基礎1 ＋ 隣接" + eff + (old2 ? "（本来" + adj + "）" : "") + (r.specialist ? " ×2(専門家)" : "") + "</color></size>",
                    11.5f, MUTED, TextAlignmentOptions.TopLeft);
                Place(n2.rectTransform, 12, 30, w - 30, 18);
                if (old2)
                {
                    int rc = DistrictCatalog.RenovateCost(r.id, sl);
                    var rb = PrimaryButton(card, "改築 " + rc + "DP", PANEL2, C("#e08a3c"),
                        () => { if (DistrictCatalog.TryRenovate(r.id, sl)) RefreshSurfacePanel(); });
                    Place((RectTransform)rb.transform, w - 164, 8, 150, 26);
                    AddTooltip(((RectTransform)rb.transform).gameObject,
                        "時代が変わって古くなった施設を、今の時代の建て方に直す。隣接ボーナス+" + adj + " が戻る（専門家の出力も戻る）。");
                }
                var n3 = Text(card.rectTransform, "<size=90%><color=#6f6889>" + detail + "</color></size>", 10.5f, FAINT, TextAlignmentOptions.TopLeft);
                Place(n3.rectTransform, 12, 50, w - 180, 20);
                // 👷 専門家（1タイル1人。隣接ボーナスが2倍になる代わりに食料2と不満1）
                if (slot == 0)
                {
                    string whySp = "";
                    bool canSp = r.specialist ? true : SettlementSystem.CanPlaceSpecialist(r.id, out whySp);
                    var sb2 = PrimaryButton(card, r.specialist ? "専門家を戻す" : "専門家を置く", canSp ? PANEL2 : PANEL,
                        canSp ? C("#57c3ab") : C("#4a4560"), () => { if (SettlementSystem.TryToggleSpecialist(r.id)) RefreshSurfacePanel(); });
                    Place((RectTransform)sb2.transform, w - 164, 44, 150, 26);
                    AddTooltip(((RectTransform)sb2.transform).gameObject,
                        canSp ? "この施設の隣接ボーナスが2倍になる。維持費は食料2＋不満1。" : whySp);
                }
                y += 84;
            }

            if (canBuild)
            {
                if (asQuarter)
                {
                    var qh = Text(c, "<color=#e3c34a>◆ 街区：このタイルに2つ目を重ねられる（両方に+2・費用1.5倍）</color>", 11.5f, GOLD, TextAlignmentOptions.TopLeft);
                    Place(qh.rectTransform, 8, y, w - 16, 18); y += 22;
                }
                // ⏳ 時代順に並べる。⚠ カタログ自体は並べ替えない（indexがセーブに載っている）。
                foreach (int i in DistrictCatalog.SortedForUI())
                {
                    int di = i; var d = DistrictCatalog.Get(i);
                    bool unlocked = DistrictCatalog.IsUnlocked(i);
                    string detail; int adj = DistrictCatalog.Adjacency(i, r.id, out detail);
                    int cost = Mathf.RoundToInt(DistrictCatalog.Cost(i) * (asQuarter ? 1.5f : 1f));
                    bool cheap = DistrictCatalog.IsLeastBuilt(i);
                    var card = Panel(c, "D_" + i, CARD);
                    Place(card.rectTransform, 0, y, w - 6, 76); Outline(card, unlocked ? LINE2 : LINE);
                    var n1 = Text(card.rectTransform, "<color=" + (unlocked ? d.colorHex : "#4a4560") + ">" + d.jpName + "</color>"
                        + " <size=86%><color=#9c95b4>" + DistrictCatalog.YieldName(d.yield) + "</color></size>"
                        + " <size=80%><color=#6f6889>" + EraSystem.EraName(d.era) + "</color></size>", 13.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                    Place(n1.rectTransform, 12, 8, w - 160, 18);
                    var n2 = Text(card.rectTransform, "ここに建てると <color=#5cc47c>+" + (1 + adj) + "</color> <size=88%><color=#9c95b4>(基礎1＋隣接" + adj + ")</color></size>",
                        11.5f, MUTED, TextAlignmentOptions.TopLeft);
                    Place(n2.rectTransform, 12, 30, w - 160, 18);
                    var n3 = Text(card.rectTransform, "<size=90%><color=#6f6889>" + detail + "</color></size>", 10.5f, FAINT, TextAlignmentOptions.TopLeft);
                    Place(n3.rectTransform, 12, 50, w - 160, 20);
                    // ⚓ 港は沿岸だけ。建てられない理由は「時代 → 研究 → 地形」の順に1つだけ出す。
                    bool coastOK = d.id != "harbor" || DistrictCatalog.IsCoastal(r.id);
                    if (unlocked && coastOK)
                    {
                        // 🔨 K-1：ここは**DPで即建てる**ボタンではなくなった。生産の待ち行列に積むだけ。
                        //    ⚠ 「建設 380DP」のままだと嘘になる（費用は取らない）。所要ターンを出す。
                        int pcost = ProductionSystem.CostOf(ProductionSystem.Kind.District, di);
                        int pper = Mathf.Max(1, ProductionSystem.ProductionAt(r.id));
                        var bb = PrimaryButton(card, "生産に積む <size=85%>約" + Mathf.CeilToInt(pcost / (float)pper) + "T</size>", PANEL2, C(d.colorHex),
                            () => { if (DistrictCatalog.TryBuild(r.id, di)) { surfaceMenuTab = 1; RefreshSurfacePanel(); } });
                        AddTooltip(bb.gameObject, d.jpName + " を待ち行列に積む（" + pcost + " 生産力）。すぐ欲しければ『生産』タブの購入で "
                            + (pcost * ProductionSystem.DpPerProduction) + "DP。");
                        Place((RectTransform)bb.transform, w - 152, 24, 138, 28);
                    }
                    else
                    {
                        string lock1 = !unlocked ? DistrictCatalog.LockReason(i) : "海に面していない";
                        var no = Text(card.rectTransform, "<color=#4a4560>" + lock1 + "</color>", 10.5f, FAINT, TextAlignmentOptions.TopRight);
                        Place(no.rectTransform, w - 152, 32, 138, 16);
                    }
                    AddTooltip(card.gameObject, d.jpName + "（" + EraSystem.EraName(d.era) + "）：" + d.desc + "\n" + detail);
                    y += 84;
                }
            }
        }

        // 🏋️ 訓練所：ここに配下を送り込んで育てる（③）
        if (TrainingSystem.HasCamp(r.id))
        {
            int here = TrainingSystem.CountAt(r.id);
            var th3 = Text(c, "◆ 訓練所　<size=88%><color=#9c95b4>" + here + "/" + TrainingSystem.PerCamp
                + "体　" + TrainingSystem.TrainTurns + "ターンで +" + (TrainingSystem.ExpPerTurnAt(r.id) * TrainingSystem.TrainTurns)
                + "exp（毎ターン+" + TrainingSystem.ExpPerTurnAt(r.id) + "）</color></size>", 12.5f, C("#e08a3c"), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(th3.rectTransform, 4, y, w - 8, 18); y += 22;
            var note = Text(c, "<size=88%><color=#6f6889>訓練中は隊にもボスにも使えません（防衛を削って将来に投資する判断）。</color></size>",
                10.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(note.rectTransform, 8, y, w - 16, 16); y += 20;

            foreach (var t in new List<TrainingSystem.Trainee>(TrainingSystem.All))
            {
                if (t.regionId != r.id) continue;
                int tid = t.individualId; var tv = MinionRoster.Get(tid); if (tv == null) continue;
                var card = Panel(c, "TR_" + tid, PANEL2);
                Place(card.rectTransform, 0, y, w - 6, 32); Outline(card, C("#e08a3c"));
                var n1 = Text(card.rectTransform, MinionCatalog.Get(tv.catalogIndex).jpName + " Lv" + tv.level
                    + "　<size=88%><color=#9c95b4>あと" + t.turnsLeft + "ターン</color></size>", 11.5f, TEXT, TextAlignmentOptions.TopLeft);
                Place(n1.rectTransform, 12, 8, w - 120, 18);
                var rb2 = PrimaryButton(card, "呼び戻す", PANEL, MUTED, () => { TrainingSystem.Recall(tid); RefreshSurfacePanel(); });
                Place((RectTransform)rb2.transform, w - 110, 4, 96, 24);
                y += 36;
            }
            if (here < TrainingSystem.PerCamp)
            {
                int shown = 0;
                foreach (var cand in MinionRoster.All)
                {
                    if (shown >= 8) break;
                    string whyT;
                    if (!TrainingSystem.CanSend(cand.id, r.id, out whyT)) continue;
                    int cid2 = cand.id; var cd2 = MinionCatalog.Get(cand.catalogIndex);
                    var card = Panel(c, "TS_" + cid2, CARD);
                    Place(card.rectTransform, 0, y, w - 6, 30); Outline(card, LINE);
                    var n1 = Text(card.rectTransform, cd2.jpName + " Lv" + cand.level + " <size=80%><color=#6f6889>#" + cid2 + "</color></size>",
                        11.5f, RoleColor(cd2.role), TextAlignmentOptions.TopLeft);
                    Place(n1.rectTransform, 12, 7, w - 110, 18);
                    var sb3 = PrimaryButton(card, "送る", PANEL2, C("#e08a3c"),
                        () => { if (TrainingSystem.TrySend(cid2, r.id)) RefreshSurfacePanel(); });
                    Place((RectTransform)sb3.transform, w - 100, 3, 86, 24);
                    y += 34; shown++;
                }
                if (shown == 0)
                {
                    var n = Text(c, "<color=#6f6889>送れる配下がいません（隊・ボス・眷属に就いていない個体だけ送れます）。</color>",
                        11f, FAINT, TextAlignmentOptions.TopLeft);
                    Place(n.rectTransform, 8, y, w - 16, 18); y += 22;
                }
            }
            y += 8;
        }

        c.sizeDelta = new Vector2(0f, Mathf.Max(y + 8, 80));
    }

    /// <summary>
    /// ⚔️ 軍団タブ（U-2）。上から「上限と維持費」「生産中」「盤にいる軍団」「新規着工」の4段。
    ///
    /// ⚠ この画面が無いと軍団はコードからしか作れない（U-1の状態）。
    ///   **「何を・どこで・あと何ターンで」が1枚で読めること**が、戦線を組む判断の前提になる。
    /// </summary>
    /// <summary>
    /// 🎯 軍団を選び、**盤の視点をその軍団へ寄せる**（一覧と盤を結びつける唯一の導線）。
    /// もう一度同じ軍団を押したら選択を外す。
    /// </summary>
    private void SelectLegion(int legionId)
    {
        var l = LegionRoster.Get(legionId);
        if (l == null) { selectedLegionId = -1; RefreshSurfacePanel(); return; }
        if (selectedLegionId == legionId) { selectedLegionId = -1; RefreshSurfacePanel(); return; }
        selectedLegionId = legionId;
        selectedRegionId = l.regionId;
        surfaceActionMsg = "<color=#8cb8e6>" + LegionRoster.NameOf(l) + " を選びました。盤のタイルを押すと進軍・攻撃できます。</color>";
        if (surfaceView != null) { surfaceView.SetSelected(l.regionId); surfaceView.CenterOn(l.regionId); }
        RefreshSurfacePanel();
    }

    /// <summary>麾下ボタンの巡回：独立 → 眷属を順に → 独立。眷属が0人なら常に独立。</summary>
    private static int NextCommanderFor(int currentKinId)
    {
        var ks = KinRoster.All;
        if (ks.Count == 0) return -1;
        if (currentKinId < 0) return ks[0].individualId;
        for (int i = 0; i < ks.Count; i++)
            if (ks[i].individualId == currentKinId)
                return (i + 1 < ks.Count) ? ks[i + 1].individualId : -1;
        return -1;   // 司令官が失われていた
    }

    private void RefreshLegionPanel()
    {
        var c = legionContainer; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = legionW, y = 0f;

        // ① 見出し：上限と維持費
        int cap = LegionRoster.Cap, now = LegionRoster.Count, making = LegionRoster.Builds.Count;
        int mats = res != null ? res.CraftMaterials : 0;
        int up = LegionRoster.TotalUpkeep;
        string capCol = (now + making) >= cap ? "#e05a5a" : "#5cc47c";
        string upCol = up > mats ? "#e05a5a" : "#9c95b4";
        var head = Text(c, "軍団 <color=" + capCol + "><b>" + now + "</b>/" + cap + "</color>　生産中 " + making
            + "　<color=" + upCol + ">維持費 " + up + " 素材/ターン（所持 " + mats + "）</color>",
            12.5f, TEXT, TextAlignmentOptions.TopLeft);
        Place(head.rectTransform, 4, y, w - 8, 18); y += 22;
        var hint = Text(c, "<color=#6f6889>上限は拠点を増やすと伸びる。維持費を払えないと軍団が痩せる。</color>",
            11f, MUTED, TextAlignmentOptions.TopLeft);
        Place(hint.rectTransform, 4, y, w - 8, 16); y += 22;

        // ② 生産中
        if (LegionRoster.Builds.Count > 0)
        {
            var t2 = Text(c, "◆ 生産中", 12f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(t2.rectTransform, 4, y, w - 8, 16); y += 20;
            foreach (var b in LegionRoster.Builds)
            {
                var bb = b;
                var rg = SurfaceMap.Get(bb.regionId);
                int need = LegionRoster.BuildCostOf(bb.catalogIndex);
                int per = Mathf.Max(1, LegionRoster.ProductionAt(bb.regionId));
                int left = Mathf.CeilToInt((need - bb.progress) / (float)per);
                var row = Panel(c, "B" + bb.regionId, CARD);
                Place(row.rectTransform, 2, y, w - 6, 34); Outline(row, LINE);
                var tx = Text(row.rectTransform, MinionCatalog.Get(bb.catalogIndex).jpName + "軍団　<color=#9c95b4>"
                    + (rg != null ? rg.name : "?") + "　" + bb.progress + "/" + need + "　あと" + left + "ターン</color>",
                    11.5f, TEXT, TextAlignmentOptions.Left);
                Place(tx.rectTransform, 8, 9, w - 100, 16);
                var cancel = PrimaryButton(row, "中止", PANEL2, TEXT, () => { LegionRoster.CancelBuild(bb.regionId); RefreshLegionPanel(); });
                Place((RectTransform)cancel.transform, w - 74, 5, 62, 24);
                y += 38;
            }
            y += 6;
        }

        // ③ 盤にいる軍団
        var t3 = Text(c, "◆ 盤にいる軍団", 12f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(t3.rectTransform, 4, y, w - 8, 16); y += 18;
        // ⚔️ 三すくみを常に見せる。相性表をどこかに隠すと、並べ方の判断そのものが起きない。
        var cnt = Text(c, "<color=#6f6889>相性：<color=" + LegionRoster.ClassHex(LegionRoster.Cls.Assault) + ">突撃</color>→後衛"
            + "　<color=" + LegionRoster.ClassHex(LegionRoster.Cls.Van) + ">前衛</color>→突撃"
            + "　<color=" + LegionRoster.ClassHex(LegionRoster.Cls.Archer) + ">射手/術者</color>→前衛"
            + "（射手は距離2から一方的に撃てる）</color>", 10.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(cnt.rectTransform, 4, y, w - 8, 28); y += 30;
        if (LegionRoster.Count == 0)
        {
            var e = Text(c, "<color=#9c95b4>まだ軍団がいません。下の『新規着工』から拠点で造ってください。</color>", 11.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(e.rectTransform, 4, y, w - 8, 32); y += 36;
        }
        foreach (var lg in LegionRoster.All)
        {
            var l2 = lg;
            var cls = LegionRoster.ClassOf(l2);
            var rg = SurfaceMap.Get(l2.regionId);
            bool selNow = l2.id == selectedLegionId;
            var row = Panel(c, "L" + l2.id, selNow ? SEL : CARD);
            Place(row.rectTransform, 2, y, w - 6, 60); Outline(row, selNow ? GOLD : LINE);
            // 🎖️ 指揮（届いている司令官）と麾下（付いていく相手）は別物なので、両方出す。
            float cmdMult = LegionRoster.CommandMultAt(l2.regionId);
            var cmdK = LegionRoster.CommanderAt(l2.regionId);
            var boss = l2.commanderKinId >= 0 ? KinRoster.Of(l2.commanderKinId) : null;
            string cmdTxt = cmdK != null
                ? "<color=#57c3ab>指揮 " + cmdK.trueName + " ×" + cmdMult.ToString("0.00") + "</color>"
                : "<color=#6f6889>指揮の外</color>";
            string bossTxt = boss != null
                ? "　<color=#ffd24a>麾下：" + boss.trueName + "</color>"
                : "　<color=#6f6889>麾下：独立</color>";
            // 🏰 補給：戻る量か、戻らない理由（前線に置きっぱなしにできないことが読めるように）
            string healWhy = LegionRoster.HealBlockReason(l2);
            string healTxt = l2.strength >= 100 ? ""
                : string.IsNullOrEmpty(healWhy)
                    ? "　<color=#5cc47c>補給 +" + LegionRoster.HealRateAt(l2.regionId) + "%/T</color>"
                    : "　<color=#e08a3c>補給なし（" + healWhy + "）</color>";
            var tx = Text(row.rectTransform,
                "<color=" + LegionRoster.ClassHex(cls) + ">■</color> " + LegionRoster.NameOf(l2)
                + "　<color=#9c95b4>" + LegionRoster.ClassName(cls) + "・Lv" + l2.level
                + "<size=88%>(" + l2.exp + "/" + LegionRoster.ExpNeed(l2.level) + ")</size>"
                + "・戦力" + LegionRoster.PowerOf(l2).ToString("F0") + "・残兵" + l2.strength + "%"
                + "・移動" + LegionRoster.MpOf(l2) + "/" + LegionRoster.MovementOf(l2) + "</color>\n"
                + "<color=#6f6889>" + (rg != null ? rg.name : "?")
                + (l2.marchTarget >= 0 && SurfaceMap.Get(l2.marchTarget) != null
                    ? "　→ " + SurfaceMap.Get(l2.marchTarget).name + " へ進軍中" : "") + "</color>" + healTxt + "\n"
                + cmdTxt + bossTxt,
                11.5f, TEXT, TextAlignmentOptions.TopLeft);
            Place(tx.rectTransform, 8, 4, w - 190, 52);
            AddTooltip(row.gameObject, LegionRoster.ClassName(cls) + "：" + LegionRoster.CounterHint(cls)
                + "\n攻城の得手不得手 ×" + LegionRoster.SiegeMult(cls).ToString("0.00")
                + "（射手は城攻めに弱い）／側面 ×" + LegionRoster.FlankBonusAt(l2.regionId, l2.id).ToString("0.00")
                + "（隣に並べた味方1体につき+8%・3体まで）"
                + "\n指揮は一番強い司令官のぶんだけ乗る（重ならない・上限×1.20）。"
                + "\n麾下に入れると、行き先を指示していないターンは司令官に付いて動く。"
                + "\n残兵は**自領で休んだターンだけ**戻る（戦ったターンは戻らない）。");
            // 🎯 押したら**盤の視点をその軍団へ飛ばす**。一覧を見ても盤のどれか分からない、が最大の不満だった。
            var pick = PrimaryButton(row, selNow ? "◆選択中" : "選ぶ", PANEL2, GOLD,
                () => { SelectLegion(l2.id); });
            Place((RectTransform)pick.transform, w - 176, 4, 58, 24);
            AddTooltip(pick.gameObject, "盤の視点をこの軍団へ移し、そのタイルを選択します。\nそのあと盤のタイルを押せば、進軍や攻撃ができます。");
            var go = PrimaryButton(row, "ここへ", PANEL2, TEXT,
                () => { LegionRoster.SetMarchTarget(l2.id, selectedRegionId); RefreshLegionPanel(); });
            Place((RectTransform)go.transform, w - 114, 4, 58, 24);
            AddTooltip(go.gameObject, "選択中のタイルへ進軍させる（毎ターン移動力のぶん近づく）");
            var dis = PrimaryButton(row, "解散", PANEL2, MUTED,
                () => { LegionRoster.Disband(l2.id); if (selectedLegionId == l2.id) selectedLegionId = -1; RefreshLegionPanel(); });
            Place((RectTransform)dis.transform, w - 52, 4, 44, 24);
            // 🎖️ 麾下の付け替え。眷属が何人もいることは稀なので、押すたびに次の司令官へ回す
            //    （専用の選択画面を出すほどの操作ではない）。
            var att = PrimaryButton(row, boss != null ? "麾下を替える" : "麾下に入れる", PANEL2, C("#57c3ab"),
                () => { LegionRoster.AttachTo(l2.id, NextCommanderFor(l2.commanderKinId)); RefreshLegionPanel(); });
            Place((RectTransform)att.transform, w - 176, 31, 100, 24);
            var alb = att.GetComponentInChildren<TMP_Text>(); if (alb != null) alb.fontSize = 10f;
            AddTooltip(att.gameObject, "押すたびに『独立 → 眷属A → 眷属B → …→ 独立』と回ります。");
            // 🏰 攻める（選択中のタイルが隣の敵領・中立領のときだけ出す）
            string awhy;
            if (LegionRoster.CanAssault(l2, selectedRegionId, out awhy))
            {
                int defV = SurfaceMap.DefenseOf(selectedRegionId);
                float pw = LegionRoster.SiegePowerOf(l2);
                // ⚠ ラベルは短く。44pxに「攻める 23→88」を入れたら2行に折れて潰れた（実測）。
                var ab = PrimaryButton(row, "攻 " + pw.ToString("F0") + "/" + defV, PANEL2,
                    pw >= defV * 1.15f ? C("#5cc47c") : pw >= defV * 0.9f ? GOLD : C("#e05a5a"),
                    () =>
                    {
                        string w2;
                        LegionRoster.TryAssault(l2.id, selectedRegionId, out w2);
                        RefreshSurfacePanel();
                    });
                Place((RectTransform)ab.transform, w - 72, 31, 64, 24);
                var lb2 = ab.GetComponentInChildren<TMP_Text>();
                if (lb2 != null) { lb2.fontSize = 9.5f; lb2.enableWordWrapping = false; }
                AddTooltip(ab.gameObject, SurfaceMap.Get(selectedRegionId).name + " を攻める\n"
                    + "攻め手 " + pw.ToString("F0") + "（攻城×" + LegionRoster.SiegeMult(cls).ToString("0.00")
                    + "・側面×" + LegionRoster.FlankBonusAt(l2.regionId, l2.id).ToString("0.00") + "） vs 守り " + defV + "\n"
                    + "1.15倍で制圧（-15%）／0.9倍で辛勝（-35%）／届かなければ撃退されて -40〜60%。");
            }
            y += 64;
        }
        y += 8;

        // ④ 新規着工
        var selR = SurfaceMap.Get(selectedRegionId);
        var t4 = Text(c, "◆ 新規着工　<color=#9c95b4>選択中のタイル：" + (selR != null ? selR.name : "未選択") + "</color>",
            12f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(t4.rectTransform, 4, y, w - 8, 16); y += 20;
        if (selR == null || !selR.owned || selR.settle == SurfaceMap.Settle.None)
        {
            var e = Text(c, "<color=#9c95b4>盤で<b>自分の拠点</b>を選んでください。拠点の人口が多いほど早く造れます。</color>", 11.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(e.rectTransform, 4, y, w - 8, 32); y += 36;
        }
        else
        {
            int prod = LegionRoster.ProductionAt(selectedRegionId);
            var pl = Text(c, "<color=#9c95b4>この拠点の生産力 <b>" + prod + "</b>/ターン（人口" + selR.pop + "）</color>",
                11.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(pl.rectTransform, 4, y, w - 8, 16); y += 20;
            for (int k = 0; k < MinionCatalog.Count; k++)
            {
                int ci = k;
                if (!MinionEvolution.IsUnlocked(ci)) continue;
                var d = MinionCatalog.Get(ci);
                var cls = LegionRoster.ClassOf(ci);
                string why; bool ok = LegionRoster.CanStartBuild(selectedRegionId, ci, out why);
                int need = LegionRoster.BuildCostOf(ci);
                int turns = prod > 0 ? Mathf.CeilToInt(need / (float)prod) : 99;
                var row = Panel(c, "N" + ci, CARD);
                Place(row.rectTransform, 2, y, w - 6, 32); Outline(row, LINE);
                var tx = Text(row.rectTransform,
                    "<color=" + LegionRoster.ClassHex(cls) + ">■</color> " + d.jpName + "　<color=#9c95b4>"
                    + LegionRoster.ClassName(cls) + "・" + LegionRoster.DpCostOf(ci) + "DP・生産" + need
                    + "（約" + turns + "ターン）</color>", 11.5f, ok ? TEXT : FAINT, TextAlignmentOptions.Left);
                Place(tx.rectTransform, 8, 8, w - 100, 16);
                if (ok)
                {
                    var bt = PrimaryButton(row, "着工", PANEL2, GOLD,
                        () => { LegionRoster.TryStartBuild(selectedRegionId, ci); RefreshLegionPanel(); });
                    Place((RectTransform)bt.transform, w - 74, 4, 62, 24);
                }
                else AddTooltip(row.gameObject, why);
                y += 36;
            }
        }
        c.sizeDelta = new Vector2(0f, Mathf.Max(200f, y + 12f));
    }

    private void RefreshKinList()
    {
        var c = kinListContainer; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = kinListW, y = 0f;
        var kins = KinRoster.All;
        if (kins.Count == 0)
        {
            var h = Text(c, "<color=#9c95b4>まだ眷属が居ません。図鑑の『個体』タブで Lv" + KinRoster.MinLevelToName + "以上・進化Ⅰ以上の個体を『眷属化』してください。\n眷属になった個体とその配下は、ダンジョンの隊・ボスには使えなくなります（防衛を削って地上に投資する判断）。</color>",
                12, MUTED, TextAlignmentOptions.TopLeft);
            Place(h.rectTransform, 4, 6, w - 8, 60);
            c.sizeDelta = new Vector2(0f, 80);
            return;
        }
        foreach (var k in kins)
        {
            var kk = k;
            var v = MinionRoster.Get(k.individualId);
            if (v == null) continue;
            var d = MinionCatalog.Get(v.catalogIndex);
            bool sel = selectedKinId == k.individualId;
            float rowH = sel ? 104f + 34f + 34f * Mathf.CeilToInt(KinPromotion.Count / 4f) : 104f;
            var row = Panel(c, "Kin_" + k.individualId, sel ? SEL : CARD);
            Place(row.rectTransform, 0, y, w - 6, rowH - 6); Outline(row, sel ? GOLD : LINE);
            var btnSel = row.gameObject.AddComponent<Button>(); btnSel.targetGraphic = row;
            btnSel.onClick.AddListener(() => { selectedKinId = kk.individualId; RefreshSurfacePanel(); });

            var nm = Text(row.rectTransform, "◆<color=#ffd24a>" + k.trueName + "</color>　" + d.jpName + " <size=86%>#" + v.id + " Lv" + v.level + "</size>",
                14, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm.rectTransform, 12, 8, w - 220, 20);

            int lpU = KinRoster.LPUsed(k), lpM = KinRoster.LPMax(k);
            var st = Text(row.rectTransform, "統率 <color=#57c3ab>" + lpU + "/" + lpM + "</color>　戦力 <color=#e05a5a>" + KinRoster.ArmyPower(k).ToString("0")
                + "</color>　移動 <color=#e3a94a>" + KinRoster.MovementOf(k) + "</color>　攻略 " + k.conquests
                + "　<color=#ffd24a>武勲 " + k.merit + "</color>（次の昇進 " + KinPromotion.CostFor(k) + "）",
                11.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(st.rectTransform, 12, 30, w - 220, 18);

            var stt = Text(row.rectTransform, KinRoster.StateText(k), 11.5f,
                k.injuryTurns > 0 ? CRIMSON : (k.marchTarget >= 0 ? GOLD : FAINT), TextAlignmentOptions.TopRight, FontStyles.Bold);
            Place(stt.rectTransform, w - 250, 30, 236, 18);

            // 率いている配下（クリックで外す）
            var fl = Text(row.rectTransform, "配下:", 11, FAINT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(fl.rectTransform, 12, 54, 40, 16);
            float fx = 52f;
            foreach (var fid in new List<int>(k.followers))
            {
                int ff = fid; var fv = MinionRoster.Get(fid); if (fv == null) continue;
                var fd = MinionCatalog.Get(fv.catalogIndex);
                var chip = Panel(row.rectTransform, "F_" + fid, PANEL2);
                Place(chip.rectTransform, fx, 50, 116, 22); Outline(chip, LINE);
                var ct = Text(chip.rectTransform, fd.jpName + " Lv" + fv.level, 9.5f, RoleColor(fd.role), TextAlignmentOptions.Center, FontStyles.Bold);
                StretchFull(ct.rectTransform);
                var cb = chip.gameObject.AddComponent<Button>(); cb.targetGraphic = chip;
                cb.onClick.AddListener(() => { KinRoster.RemoveFollower(kk.individualId, ff); RefreshSurfacePanel(); });
                AddTooltip(chip.gameObject, "クリックで部隊から外す（LP " + KinRoster.LPCost(fid) + "）");
                fx += 120f;
                if (fx > w - 130) break;
            }

            // 追加できる個体（未編成・未配置・眷属でない）＝選択中の眷属にだけ出す
            if (sel)
            {
                var al = Text(row.rectTransform, "＋連れて行く:", 11, FAINT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(al.rectTransform, 12, 78, 84, 16);
                float ax = 98f;
                foreach (var cand in MinionRoster.All)
                {
                    if (KinRoster.IsAwayFromDungeon(cand.id)) continue;
                    if (featureMgr != null && (featureMgr.IsIndividualInAnySquad(cand.id) || featureMgr.IsIndividualBoss(cand.id))) continue;
                    int cid = cand.id; var cd = MinionCatalog.Get(cand.catalogIndex);
                    int cost = KinRoster.LPCost(cid);
                    bool fits = lpU + cost <= lpM;
                    var chip = Panel(row.rectTransform, "A_" + cid, CARD);
                    Place(chip.rectTransform, ax, 74, 124, 22); Outline(chip, LINE);
                    var ct = Text(chip.rectTransform, cd.jpName + " Lv" + cand.level + " <size=80%>LP" + cost + "</size>", 9.5f,
                        fits ? RoleColor(cd.role) : FAINT, TextAlignmentOptions.Center, FontStyles.Bold);
                    StretchFull(ct.rectTransform);
                    if (fits)
                    {
                        var cb = chip.gameObject.AddComponent<Button>(); cb.targetGraphic = chip;
                        cb.onClick.AddListener(() => { KinRoster.AddFollower(kk.individualId, cid); RefreshSurfacePanel(); });
                    }
                    ax += 128f;
                    if (ax > w - 280) break;
                }

                // 🎖️ 昇進（4系統×3段。武勲で取る。時代を越えても残る）
                var ph = Text(row.rectTransform, "◆ 昇進　<size=88%><color=#9c95b4>武勲 " + kk.merit
                    + " ／ 次に必要 " + KinPromotion.CostFor(kk) + "</color></size>", 11.5f, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(ph.rectTransform, 12, 102, w - 30, 16);
                float px = 12f, py = 122f;
                for (int pi2 = 0; pi2 < KinPromotion.Count; pi2++)
                {
                    int pidx = pi2; var pd = KinPromotion.Get(pi2);
                    bool got = KinPromotion.Has(kk, pi2);
                    string whyP = "修得済み";
                    bool canP = !got && KinPromotion.CanTake(kk, pi2, out whyP);
                    var chip = Panel(row.rectTransform, "PR_" + pi2, got ? PANEL2 : CARD);
                    Place(chip.rectTransform, px, py, 132, 28); Outline(chip, got ? C(pd.colorHex) : (canP ? LINE2 : LINE));
                    var ct2 = Text(chip.rectTransform, (got ? "◆ " : "") + "<color=" + (got || canP ? pd.colorHex : "#4a4560") + ">"
                        + KinPromotion.LineName(pd.line) + (pd.tier + 1) + " " + pd.jpName + "</color>", 9.5f, TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
                    StretchFull(ct2.rectTransform);
                    if (canP)
                    {
                        var pb2 = chip.gameObject.AddComponent<Button>(); pb2.targetGraphic = chip;
                        pb2.onClick.AddListener(() => { if (KinPromotion.TryTake(kk, pidx)) RefreshSurfacePanel(); });
                    }
                    AddTooltip(chip.gameObject, pd.jpName + "\n" + pd.desc + (got ? "" : "\n" + whyP));
                    px += 136f;
                    if (px > w - 140f) { px = 12f; py += 34f; }
                }
            }

            if (k.marchTarget >= 0)
            {
                var cancel = PrimaryButton(row, "進軍中止", PANEL2, MUTED, () => { KinRoster.SetMarchTarget(kk.individualId, -1); RefreshSurfacePanel(); });
                Place((RectTransform)cancel.transform, w - 232, 74, 100, 24);
            }
            var dis = PrimaryButton(row, "真名を返上", PANEL2, FAINT, () => { KinRoster.Dissolve(kk.individualId); if (selectedKinId == kk.individualId) selectedKinId = -1; RefreshSurfacePanel(); RefreshMinionCodex(); });
            Place((RectTransform)dis.transform, w - 124, 74, 106, 24);
            AddTooltip(dis.gameObject, "眷属をやめてダンジョン防衛に戻す（配下は解散）");

            y += rowH;
        }
        c.sizeDelta = new Vector2(0f, Mathf.Max(y + 8, 80));
    }

    /// <summary>
    /// 🌾 そのタイルを、どこかの拠点の人口が実際に「耕している」か。
    ///
    /// ⚠ 産出は支配タイルではなく **`SurfaceMap.WorkedTiles`（人口が耕すタイル）** からしか出ない。
    ///   これを見ずに施設を建てると、金だけ消えて産出が1も動かない（実際に3,400DP溶かした）。
    /// </summary>
    private static bool IsTileWorked(SurfaceMap.Region r)
    {
        if (r == null) return false;
        int home = r.homeSettlement;
        if (home < 0) return false;
        var worked = SurfaceMap.WorkedTiles(home);
        if (worked == null) return false;
        foreach (var t in worked) if (t != null && t.id == r.id) return true;
        return false;
    }

    /// <summary>
    /// 🗡️ 自領を選んでいるときに「その眷属がいま何を獲れるか」を1行で出す。
    ///
    /// **なぜ要るか**：戦力と勝率の表示は「敵領を選んだとき」の分岐にしか無い。
    /// ところが序盤は自領が3タイル分に広がっていて、**敵領は移動力の外**にある。
    /// つまり「勝てるかどうかを見る画面」に一度も辿り着けないまま、
    /// 進言だけが「眷属を進軍させろ」と言い続ける。実測でT1〜T3が完全に手詰まりになった。
    ///
    /// ここでは **①いまの戦力 ②隣接していて勝てる敵領 ③無ければ向かうべき前線と距離** を出す。
    /// ⚠ 表示は必ず `ArmyPower`（随行を含む実際の判定値）を使う。単体の力を出すと過小に見える。
    /// </summary>
    private void ShowNextConquestHint(Image head, ref float hy, float w, KinRoster.Kin sel)
    {
        float myPow = KinRoster.ArmyPower(sel);

        // ① 隣に、勝てる相手がいるか
        int bestAdj = -1, bestAdjDef = int.MaxValue;
        foreach (var nb in SurfaceMap.Neighbors(sel.regionId))
        {
            string w2;
            if (!KinRoster.CanAttackNow(sel, nb.id, out w2)) continue;
            int d = SurfaceMap.DefenseOf(nb.id);
            if (d < bestAdjDef) { bestAdjDef = d; bestAdj = nb.id; }
        }

        string line;
        if (bestAdj >= 0)
        {
            var t = SurfaceMap.Get(bestAdj);
            float ratio = bestAdjDef > 0 ? myPow / bestAdjDef : 99f;
            string odds = ratio >= 1.25f ? "<color=#5cc47c>完勝圏</color>" : ratio >= 1.0f ? "<color=#e3a94a>辛勝圏</color>"
                        : ratio >= 0.7f ? "<color=#e08a3c>敗走の恐れ</color>" : "<color=#e05a5a>壊滅の恐れ</color>";
            line = "戦力 <color=#e05a5a>" + myPow.ToString("0") + "</color>　隣の <b>" + t.name + "</b>（守り " + bestAdjDef + "）を攻められます → " + odds;
        }
        else
        {
            // ② 隣に無いなら、いちばん近い「勝てる前線」を探して、そこへ向かうよう促す
            int frontier = -1, frontierDef = int.MaxValue, frontierSteps = 99;
            var reach = KinRoster.ReachableNow(sel);
            foreach (var rid in reach)
            {
                foreach (var nb in SurfaceMap.Neighbors(rid))
                {
                    if (nb.owner == SurfaceMap.OwnerSelf || nb.isOcean) continue;
                    int d = SurfaceMap.DefenseOf(nb.id);
                    if (d >= myPow * 0.9f) continue;              // 勝ち目が無い所は薦めない
                    int steps = KinRoster.StepsTo(sel, rid);
                    if (steps < frontierSteps || (steps == frontierSteps && d < frontierDef))
                    { frontier = rid; frontierDef = d; frontierSteps = steps; }
                }
            }
            if (frontier >= 0)
                line = "戦力 <color=#e05a5a>" + myPow.ToString("0") + "</color>　隣に敵領はありません。"
                     + "<b>" + SurfaceMap.Get(frontier).name + "</b> まで出れば、守り " + frontierDef + " の地を獲れます（" + frontierSteps + "歩）";
            else
                line = "戦力 <color=#e05a5a>" + myPow.ToString("0") + "</color>　いま勝てる相手が届く範囲にいません。"
                     + "<color=#9c95b4>配下を随行させると戦力が上がります（『眷属』タブ）</color>";
        }

        var tx = Text(head.rectTransform, line, 11.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(tx.rectTransform, 12, hy, w - 30, 18);
        hy += 22;
    }
}
