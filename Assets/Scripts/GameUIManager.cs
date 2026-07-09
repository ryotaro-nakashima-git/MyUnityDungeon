using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// ゲームUIをプログラムで一括構築するマネージャ。
/// ①迷宮生成パネル ②上部HUD ③下部コマンドバー を、CDO2/Civを意識したダークファンタジー調で組む。
/// 旧Canvas(簡素UI)は非表示化して置き換える。
/// </summary>
public class GameUIManager : MonoBehaviour
{
    // 参照
    private DungeonGenerator generator;
    private DungeonResourceManager res;
    private DungeonTurnManager turn;
    private GridInputHandler input;

    private TMP_FontAsset uiFont;

    // ライブ更新するUI要素
    private TextMeshProUGUI dpText, fameText, matText, turnText, phaseText, costText;
    private Image phasePill;
    private Button generateBtn, invadeBtn;
    private GameObject genPanel;
    private GameObject gameOverPanel;

    // 魔王パネル
    private GameObject demonPanel;
    private TextMeshProUGUI dlLevelText, dlBpText, dlRaceText;
    private readonly TextMeshProUGUI[] statRankTexts = new TextMeshProUGUI[5];
    private readonly Button[] statPlusBtns = new Button[5];
    private readonly List<(Button btn, DemonLord.Race race)> evolveBtns = new List<(Button, DemonLord.Race)>();

    // 感情ツリーパネル
    private GameObject emotionPanel;
    private readonly TextMeshProUGUI[] emoPoolTexts = new TextMeshProUGUI[4];
    private readonly List<(Button btn, TextMeshProUGUI label, EmotionTreeManager.Route route, int tier)> emoNodeBtns = new List<(Button, TextMeshProUGUI, EmotionTreeManager.Route, int)>();

    // 選択状態
    private int selType = 0, selSpace = 0, selChest = 1;
    private readonly List<Image> typeBtns = new List<Image>();
    private readonly List<Image> spaceBtns = new List<Image>();
    private readonly List<Image> chestBtns = new List<Image>();

    // ---- パレット（モックアップ準拠）----
    static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    Color BG      = C("#12101c");
    Color PANEL   = C("#191726");
    Color PANEL2  = C("#211f31");
    Color CARD    = C("#14121d");
    Color LINE    = C("#332e49");
    Color LINE2   = C("#4a4268");
    Color TEXT    = C("#ece8f5");
    Color MUTED   = C("#9c95b4");
    Color FAINT   = C("#6f6889");
    Color GOLD    = C("#e3a94a");
    Color GOLD_DK = C("#8a6a24");
    Color VIOLET  = C("#b48be6");
    Color TEAL    = C("#57c3ab");
    Color CRIMSON = C("#df5a5a");
    Color GREEN   = C("#5cc47c");
    Color SEL     = C("#2a2233");

    private void Start()
    {
        generator = Object.FindFirstObjectByType<DungeonGenerator>();
        res = Object.FindFirstObjectByType<DungeonResourceManager>();
        turn = Object.FindFirstObjectByType<DungeonTurnManager>();
        input = Object.FindFirstObjectByType<GridInputHandler>();

        uiFont = FindUIFont();
        HideLegacyCanvas();
        BuildUI();
        RefreshCost();
    }

    // 日本語対応のTMPフォントを用意する。
    // まずOSの日本語フォントから動的TMPフォントを生成（グリフを持つ）。だめなら既存/デフォルトへ。
    private TMP_FontAsset FindUIFont()
    {
        // ※ CreateFontAsset(Font) はOS動的フォントだとnullになるため、
        //   システムフォント名を直接指定するoverloadを使う（グリフはDynamicで随時追加される）。
        string[] jpFonts = { "Yu Gothic UI", "Yu Gothic", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Hiragino Kaku Gothic ProN" };
        foreach (var name in jpFonts)
        {
            try
            {
                var fa = TMP_FontAsset.CreateFontAsset(name, "Regular", 90);
                if (fa != null)
                {
                    fa.atlasPopulationMode = AtlasPopulationMode.Dynamic; // 使う文字を随時アトラスへ追加
                    Debug.Log($"🈶【UIフォント】システムフォント『{name}』から動的TMPフォントを生成");
                    return fa;
                }
            }
            catch { /* 次の候補へ */ }
        }
        // フォールバック：既存TMPテキストのフォント → デフォルト
        var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in texts) if (t != null && t.font != null) return t.font;
        return TMP_Settings.defaultFontAsset;
    }

    private void HideLegacyCanvas()
    {
        // 自分のCanvas以外で "Canvas" という名の旧UIを非表示に
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cv in canvases)
        {
            if (cv.gameObject.name == "Canvas") cv.gameObject.SetActive(false);
        }
    }

    // ================= 構築 =================
    private void BuildUI()
    {
        // ルートCanvas
        var canvasGO = new GameObject("GameUICanvas", typeof(RectTransform));
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        var root = canvasGO.GetComponent<RectTransform>();

        BuildTopBar(root);
        BuildGenPanel(root);
        BuildDemonPanel(root);
        BuildEmotionPanel(root);
        BuildBottomBar(root);
        BuildGameOverOverlay(root);
    }

    // ---------- 魔王パネル（成長/進化） ----------
    private void BuildDemonPanel(RectTransform root)
    {
        var panel = Panel(root, "DemonPanel", PANEL);
        demonPanel = panel.gameObject;
        Anchor(panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        panel.rectTransform.sizeDelta = new Vector2(320, 372);
        panel.rectTransform.anchoredPosition = new Vector2(16, -72);
        Outline(panel, LINE2);

        float pad = 16f, w = 320 - pad * 2;
        var eyebrow = Text(panel, "魔王の成長", 11, GOLD, TextAlignmentOptions.Left, FontStyles.Bold); Place(eyebrow.rectTransform, pad, 12, w, 16);
        dlLevelText = Text(panel, "Lv 1", 18, TEXT, TextAlignmentOptions.Left, FontStyles.Bold); Place(dlLevelText.rectTransform, pad, 30, 140, 24);
        dlBpText = Text(panel, "BP 10", 14, VIOLET, TextAlignmentOptions.Right, FontStyles.Bold); Place(dlBpText.rectTransform, pad + w - 130, 33, 130, 20);
        dlRaceText = Text(panel, "種族: 人種", 12.5f, MUTED, TextAlignmentOptions.Left); Place(dlRaceText.rectTransform, pad, 58, w, 18);

        var sl = Text(panel, "ステータス（BPで強化）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold); Place(sl.rectTransform, pad, 82, w, 16);
        for (int i = 0; i < 5; i++)
        {
            int idx = i; float y = 104 + i * 30;
            var nm = Text(panel, DemonLord.StatNames[i], 13, TEXT, TextAlignmentOptions.Left); Place(nm.rectTransform, pad, y, 80, 22);
            var rk = Text(panel, "E", 15, GOLD, TextAlignmentOptions.Center, FontStyles.Bold); Place(rk.rectTransform, pad + 86, y, 40, 22); statRankTexts[i] = rk;
            var plus = PrimaryButton(panel, "＋", GOLD, C("#231704"), () => { DemonLord.Instance?.TrySpendBPOnStat(idx); RefreshDemonPanel(); });
            Place((RectTransform)plus.transform, pad + w - 48, y, 48, 24); statPlusBtns[i] = plus;
        }

        var el = Text(panel, "種族進化（Lv3〜・条件達成で解禁）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold); Place(el.rectTransform, pad, 260, w, 16);
        DemonLord.Race[] races = { DemonLord.Race.Oni, DemonLord.Race.Demon, DemonLord.Race.Elf, DemonLord.Race.Dwarf, DemonLord.Race.Slime, DemonLord.Race.Vampire };
        for (int i = 0; i < races.Length; i++)
        {
            var r = races[i]; float cw = (w - 16) / 3f;
            float cx = pad + (i % 3) * (cw + 8); float cy = 280 + (i / 3) * 34;
            var b = PrimaryButton(panel, DemonLord.RaceNameOf(r).Replace("種", ""), PANEL2, TEXT, () => { DemonLord.Instance?.EvolveTo(r); RefreshDemonPanel(); });
            Place((RectTransform)b.transform, cx, cy, cw, 28); evolveBtns.Add((b, r));
        }

        RefreshDemonPanel();
        demonPanel.SetActive(false);
    }

    private void RefreshDemonPanel()
    {
        var dl = DemonLord.Instance; if (dl == null) return;
        if (dlLevelText != null) dlLevelText.text = "Lv " + dl.Level;
        if (dlBpText != null) dlBpText.text = "BP " + dl.BP;
        if (dlRaceText != null) dlRaceText.text = "種族: " + dl.RaceName;
        for (int i = 0; i < 5; i++)
        {
            if (statRankTexts[i] != null) statRankTexts[i].text = dl.StatRankLabel(i);
            if (statPlusBtns[i] != null) statPlusBtns[i].interactable = dl.GetStatRank(i) < 5 && dl.BP > 0;
        }
        bool canEv = dl.CanEvolve;
        foreach (var e in evolveBtns) if (e.btn != null) e.btn.interactable = canEv && dl.IsRaceAvailable(e.race);
    }

    // ---------- 感情ツリーパネル ----------
    private void BuildEmotionPanel(RectTransform root)
    {
        var panel = Panel(root, "EmotionPanel", PANEL);
        emotionPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(544, 320);
        panel.rectTransform.anchoredPosition = new Vector2(0, 20);
        Outline(panel, LINE2);

        float pad = 18f, w = 544 - pad * 2;
        var title = Text(panel, "感情ツリー（Eureka: 条件達成でコスト-40%★）", 15, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 12, w - 40, 22);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => emotionPanel.SetActive(false));
        Place((RectTransform)close.transform, 544 - pad - 28, 12, 28, 26);

        Color[] rc = { GOLD, C("#e08a3c"), VIOLET, CRIMSON };
        var routes = new EmotionTreeManager.Route[] { EmotionTreeManager.Route.Joy, EmotionTreeManager.Route.Thrill, EmotionTreeManager.Route.Despair, EmotionTreeManager.Route.Slaughter };
        float colW = (w - 24) / 4f;
        for (int c = 0; c < 4; c++)
        {
            float cx = pad + c * (colW + 8);
            var rn = Text(panel, EmotionTreeManager.RouteNames[c], 13, rc[c], TextAlignmentOptions.Center, FontStyles.Bold); Place(rn.rectTransform, cx, 48, colW, 18);
            var pt = Text(panel, "感情 0", 11.5f, MUTED, TextAlignmentOptions.Center); Place(pt.rectTransform, cx, 68, colW, 16); emoPoolTexts[c] = pt;
            for (int t = 0; t < 2; t++)
            {
                int tt = t; var route = routes[c];
                var b = Panel(panel, $"emo_{c}_{t}", CARD); Place(b.rectTransform, cx, 92 + t * 74, colW, 66); Outline(b, LINE);
                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
                btn.onClick.AddListener(() => { EmotionTreeManager.Instance?.TryUnlock(route, tt); RefreshEmotionPanel(); });
                var lbl = Text(b.rectTransform, "", 10.5f, TEXT, TextAlignmentOptions.Center); StretchOffset(lbl.rectTransform, 4, 4, 4, 4);
                emoNodeBtns.Add((btn, lbl, route, tt));
            }
        }
        RefreshEmotionPanel();
        emotionPanel.SetActive(false);
    }

    private void RefreshEmotionPanel()
    {
        var et = EmotionTreeManager.Instance; if (et == null) return;
        for (int c = 0; c < 4; c++) if (emoPoolTexts[c] != null) emoPoolTexts[c].text = "感情 " + et.Pool((EmotionTreeManager.Route)c);
        foreach (var e in emoNodeBtns)
        {
            var n = et.Get(e.route, e.tier); if (n == null || e.label == null) continue;
            int cost = et.EffectiveCost(n);
            string eu = et.EurekaReady(n) && !n.unlocked ? " <color=#f5c56b>★</color>" : "";
            e.label.text = n.unlocked ? $"<b>{n.name}</b>\n<color=#5cc47c>解禁済</color>" : $"<b>{n.name}</b>\nコスト {cost}{eu}";
            if (e.btn != null) e.btn.interactable = et.CanUnlock(n);
        }
    }

    private void BuildGameOverOverlay(RectTransform root)
    {
        var panel = Panel(root, "GameOverPanel", new Color(0.05f, 0.02f, 0.06f, 0.9f));
        StretchFull(panel.rectTransform);
        var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.MiddleCenter; v.spacing = 12;
        v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = false;
        var t1 = Text(panel, "GAME OVER", 64, CRIMSON, TextAlignmentOptions.Center, FontStyles.Bold);
        SizeElem(t1.gameObject, 820, 92);
        var t2 = Text(panel, "魔王が討伐された", 24, TEXT, TextAlignmentOptions.Center);
        SizeElem(t2.gameObject, 820, 42);
        panel.gameObject.SetActive(false);
        gameOverPanel = panel.gameObject;
    }

    public void ShowGameOver()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    // ---------- ②上部HUD ----------
    private void BuildTopBar(RectTransform root)
    {
        var bar = Panel(root, "TopBar", PANEL);
        Anchor(bar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        bar.rectTransform.sizeDelta = new Vector2(0, 60); bar.rectTransform.anchoredPosition = Vector2.zero;
        AddBottomBorder(bar);

        var hlg = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(18, 18, 8, 8);
        hlg.spacing = 14; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        // 作品名
        var title = Text(bar, "ダンジョン<color=#e3a94a>バトルロワイヤル</color>", 22, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        SizeElem(title.gameObject, 300, 40);

        // ターン/フェーズ ピル
        var pill = Panel(bar, "TurnPill", C("#0e0b16"));
        SizeElem(pill.gameObject, 250, 34);
        Outline(pill, LINE2);
        var ph = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
        ph.padding = new RectOffset(12, 10, 4, 4); ph.spacing = 8; ph.childAlignment = TextAnchor.MiddleLeft;
        ph.childControlWidth = true; ph.childControlHeight = true; ph.childForceExpandWidth = false;
        turnText = Text(pill, "Turn 1", 15, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        SizeElem(turnText.gameObject, 70, 26);
        phasePill = Panel(pill, "PhaseTag", C("#183726"));
        SizeElem(phasePill.gameObject, 120, 24); Round(phasePill);
        var pt = phasePill.gameObject.AddComponent<HorizontalLayoutGroup>();
        pt.padding = new RectOffset(9, 9, 2, 2); pt.childAlignment = TextAnchor.MiddleCenter;
        pt.childControlWidth = true; pt.childControlHeight = true;
        phaseText = Text(phasePill, "準備フェーズ", 12, GREEN, TextAlignmentOptions.Center, FontStyles.Bold);

        // 魔王パネルの開閉ボタン
        var dlBtn = PrimaryButton(bar, "魔王", PANEL2, TEXT, () => { if (demonPanel != null) demonPanel.SetActive(!demonPanel.activeSelf); });
        SizeElem(dlBtn.gameObject, 66, 34);
        var emoBtn = PrimaryButton(bar, "感情", PANEL2, TEXT, () => { if (emotionPanel != null) emotionPanel.SetActive(!emotionPanel.activeSelf); });
        SizeElem(emoBtn.gameObject, 66, 34);

        // 伸縮スペーサ
        Spacer(bar);

        // 資源
        dpText = ResChip(bar, GOLD, "DP", "0");
        fameText = ResChip(bar, VIOLET, "名声", "0");
        matText = ResChip(bar, TEAL, "素材", "0");
    }

    private TextMeshProUGUI ResChip(Graphic parent, Color accent, string label, string value)
    {
        var chip = Panel(parent, "Res_" + label, C("#1b1828"));
        SizeElem(chip.gameObject, 118, 42); Outline(chip, LINE);
        var h = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(11, 12, 5, 5); h.spacing = 8; h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false;

        var dot = Panel(chip, "dot", accent); SizeElem(dot.gameObject, 10, 10); Round(dot, 5);
        var col = new GameObject("col", typeof(RectTransform)).GetComponent<RectTransform>();
        col.SetParent(chip.transform, false);
        var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 0; v.childAlignment = TextAnchor.MiddleLeft; v.childControlWidth = true; v.childControlHeight = true;
        SizeElem(col.gameObject, 70, 34);
        var lab = Text(col, label, 10.5f, FAINT, TextAlignmentOptions.Left);
        var val = Text(col, value, 16, accent, TextAlignmentOptions.Left, FontStyles.Bold);
        return val;
    }

    // ---------- ①迷宮生成パネル ----------
    private void BuildGenPanel(RectTransform root)
    {
        var panel = Panel(root, "GenPanel", PANEL);
        genPanel = panel.gameObject;
        Anchor(panel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        panel.rectTransform.sizeDelta = new Vector2(360, 470);
        panel.rectTransform.anchoredPosition = new Vector2(-16, -76);
        Outline(panel, LINE2); Round(panel, 14);

        float pad = 16f, w = 360 - pad * 2;

        // ヘッダ
        var eyebrow = Text(panel, "領域創造", 11, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(eyebrow.rectTransform, pad, 14, w, 16); eyebrow.characterSpacing = 8;
        var title = Text(panel, "迷宮を生成する", 19, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 30, w, 26);
        var sub = Text(panel, "タイプ・空間・宝箱量を選ぶと迷路が自動生成されます。生成後に罠やスポナーを手動配置してください。", 11.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(sub.rectTransform, pad, 58, w, 40);

        // 迷宮タイプ（2x2カード）
        var tl = Text(panel, "迷宮タイプ", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(tl.rectTransform, pad, 104, w, 16);
        string[] tNames = { "標準", "迷路", "大空洞", "蟻の巣" };
        string[] tDesc = { "バランス型", "通路が長い", "大部屋中心", "小部屋密集" };
        float cw = (w - 8) / 2f, chH = 50;
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            float cx = pad + (i % 2) * (cw + 8);
            float cy = 124 + (i / 2) * (chH + 8);
            var b = Card(panel, cx, cy, cw, chH, tNames[i], tDesc[i], () => { selType = idx; generator?.SetDungeonType(idx); RefreshSelections(); RefreshCost(); });
            typeBtns.Add(b);
        }

        // 空間タイプ（チップ 3+2）
        var sl = Text(panel, "空間タイプ", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(sl.rectTransform, pad, 240, w, 16);
        string[] sNames = { "洞窟", "遺跡", "城塞", "溶岩", "氷雪" };
        Color[] sCols = { C("#5a5560"), C("#5c6446"), C("#4e5674"), C("#7a3a30"), C("#4a6480") };
        float chipW = (w - 16) / 3f, chipH = 30;
        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            float cx = pad + (i % 3) * (chipW + 8);
            float cy = 260 + (i / 3) * (chipH + 8);
            var b = Chip(panel, cx, cy, chipW, chipH, sNames[i], sCols[i], () => { selSpace = idx; generator?.SetSpaceType(idx); RefreshSelections(); });
            spaceBtns.Add(b);
        }

        // 宝箱量
        var cl = Text(panel, "宝箱の量（多いほどコスト大・報酬大）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(cl.rectTransform, pad, 334, w, 16);
        string[] cNames = { "少", "中", "多" };
        float ccw = (w - 16) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            float cx = pad + i * (ccw + 8);
            var b = Chip(panel, cx, 354, ccw, 30, cNames[i], GOLD, () => { selChest = idx; generator?.SetChestAmount(idx); RefreshSelections(); RefreshCost(); });
            chestBtns.Add(b);
        }

        // コスト表示
        costText = Text(panel, "生成コスト  500 DP", 12.5f, MUTED, TextAlignmentOptions.Left);
        Place(costText.rectTransform, pad, 392, w, 18);

        // 生成ボタン
        generateBtn = PrimaryButton(panel, "迷宮を生成する", GOLD, C("#231704"), () =>
        {
            if (generator == null) return;
            bool ok = generator.TryGenerateWithCost();
            RefreshCost();
        });
        Place((RectTransform)generateBtn.transform, pad, 414, w, 44);

        RefreshSelections();
    }

    // ---------- ③下部コマンドバー ----------
    private void BuildBottomBar(RectTransform root)
    {
        var bar = Panel(root, "BottomBar", PANEL);
        Anchor(bar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        bar.rectTransform.sizeDelta = new Vector2(0, 60); bar.rectTransform.anchoredPosition = Vector2.zero;
        AddTopBorder(bar);
        var h = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(16, 16, 9, 9); h.spacing = 10; h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;

        var hint = Text(bar, "配置ツール", 11, FAINT, TextAlignmentOptions.Left);
        SizeElem(hint.gameObject, 68, 40);

        ToolButton(bar, "トーテム", TEAL, () => input?.SetToolMode(6));
        ToolButton(bar, "罠", CRIMSON, () => input?.SetToolMode(3));
        ToolButton(bar, "スポナー", VIOLET, () => input?.SetToolMode(7));
        ToolButton(bar, "ボス", CRIMSON, () => input?.SetToolMode(8));
        ToolButton(bar, "特殊敵", GOLD, () => input?.SetToolMode(9));
        ToolButton(bar, "消去", MUTED, () => input?.SetToolMode(10));
        ToolButton(bar, "冒険者(検証)", GOLD, () => input?.SetToolMode(4));

        Spacer(bar);

        var extendBtn = PrimaryButton(bar, "戦闘時間 +1分", PANEL2, TEXT, () => turn?.ExtendWaveLimit());
        SizeElem(extendBtn.gameObject, 150, 42);

        invadeBtn = PrimaryButton(bar, "⚔ 侵略開始", CRIMSON, C("#2a0d0d"), () => turn?.StartBattlePhase());
        SizeElem(invadeBtn.gameObject, 170, 42);
    }

    // ================= ライブ更新 =================
    private void Update()
    {
        if (res != null)
        {
            if (dpText != null) dpText.text = res.DungeonPoints.ToString("N0");
            if (fameText != null) fameText.text = res.DungeonFame.ToString("N0");
            if (matText != null) matText.text = res.CraftMaterials.ToString("N0");
        }
        if (turn != null)
        {
            if (turnText != null) turnText.text = "Turn " + turn.CurrentTurn;
            bool prep = turn.IsPreparePhase;
            if (phaseText != null)
            {
                if (prep) { phaseText.text = "準備フェーズ"; phaseText.color = GREEN; }
                else
                {
                    float rem = turn.RemainingWaveTime;
                    int mm = (int)(rem / 60f); int ss = (int)(rem % 60f);
                    phaseText.text = $"戦闘 {mm}:{ss:00}"; phaseText.color = CRIMSON;
                }
            }
            if (phasePill != null) phasePill.color = prep ? C("#183726") : C("#3a1a1a");
            if (genPanel != null && genPanel.activeSelf != prep) genPanel.SetActive(prep);
            if (invadeBtn != null) invadeBtn.interactable = prep;
        }
        if (demonPanel != null && demonPanel.activeSelf) RefreshDemonPanel();
        if (emotionPanel != null && emotionPanel.activeSelf) RefreshEmotionPanel();
    }

    private void RefreshCost()
    {
        if (costText == null || generator == null) return;
        int cost = generator.GetGenerationCost();
        costText.text = "生成コスト  <b><color=#e3a94a>" + cost.ToString("N0") + " DP</color></b>";
        if (generateBtn != null)
        {
            bool afford = res == null || res.DungeonPoints >= cost;
            generateBtn.interactable = afford;
        }
    }

    private void RefreshSelections()
    {
        for (int i = 0; i < typeBtns.Count; i++) SetSel(typeBtns[i], i == selType);
        for (int i = 0; i < spaceBtns.Count; i++) SetSel(spaceBtns[i], i == selSpace);
        for (int i = 0; i < chestBtns.Count; i++) SetSel(chestBtns[i], i == selChest);
    }
    private void SetSel(Image img, bool on)
    {
        if (img == null) return;
        img.color = on ? SEL : CARD;
        var outline = img.GetComponent<Outline>();
        if (outline != null) outline.effectColor = on ? GOLD : LINE;
    }

    // ================= UI生成ヘルパー =================
    private RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }
    private Image Panel(Transform parent, string name, Color color)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }
    // Imageを親に取るオーバーロード（Panelが返すImageをそのまま親にできる）
    private Image Panel(Graphic parent, string name, Color color) => Panel(parent.transform, name, color);
    private TextMeshProUGUI Text(Graphic parent, string txt, float size, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        => Text(parent.transform, txt, size, color, align, style);
    private void Spacer(Graphic parent) => Spacer(parent.transform);
    private void Anchor(Graphic g, Vector2 min, Vector2 max, Vector2 pivot) => Anchor(g.rectTransform, min, max, pivot);
    private TextMeshProUGUI Text(Transform parent, string txt, float size, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var rt = NewRect("Text", parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = txt; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.font = uiFont; t.richText = true; t.enableWordWrapping = true; t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }
    private void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
    { rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot; }
    // パネル内で左上原点の絶対配置
    private void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
    }
    private void SizeElem(GameObject go, float w, float h)
    {
        var le = go.GetComponent<LayoutElement>(); if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = w; le.preferredHeight = h; le.minWidth = w; le.minHeight = h;
    }
    private void Spacer(Transform parent)
    {
        var rt = NewRect("Spacer", parent);
        var le = rt.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1;
    }
    private void Outline(Graphic g, Color col)
    {
        var o = g.gameObject.AddComponent<Outline>();
        o.effectColor = col; o.effectDistance = new Vector2(1, -1); o.useGraphicAlpha = false;
    }
    private void Round(Image img, float _ = 12) { /* スプライト無しのため角丸は省略（色面で表現）*/ }
    private void AddBottomBorder(Image bar)
    {
        var b = Panel(bar.rectTransform, "border", LINE2);
        Anchor(b, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        b.rectTransform.sizeDelta = new Vector2(0, 1); b.rectTransform.anchoredPosition = Vector2.zero;
    }
    private void AddTopBorder(Image bar)
    {
        var b = Panel(bar.rectTransform, "border", LINE2);
        Anchor(b, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        b.rectTransform.sizeDelta = new Vector2(0, 1); b.rectTransform.anchoredPosition = Vector2.zero;
    }

    // タイプカード
    private Image Card(Graphic panel, float x, float y, float w, float h, string name, string desc, UnityAction onClick)
    {
        var img = Panel(panel, "Card_" + name, CARD);
        Place(img.rectTransform, x, y, w, h); Outline(img, LINE);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
        var n = Text(img.rectTransform, name, 13, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(n.rectTransform, 10, 7, w - 16, 18);
        var d = Text(img.rectTransform, desc, 10.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(d.rectTransform, 10, 27, w - 16, 16);
        return img;
    }
    // チップ（空間/宝箱量）
    private Image Chip(Graphic panel, float x, float y, float w, float h, string name, Color accent, UnityAction onClick)
    {
        var img = Panel(panel, "Chip_" + name, CARD);
        Place(img.rectTransform, x, y, w, h); Outline(img, LINE);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
        var dot = Panel(img.rectTransform, "dot", accent); Place(dot.rectTransform, 9, (h - 11) / 2f, 11, 11);
        var n = Text(img.rectTransform, name, 12, TEXT, TextAlignmentOptions.Left);
        Place(n.rectTransform, 26, (h - 16) / 2f, w - 30, 16);
        return img;
    }
    // ツールボタン
    private void ToolButton(Graphic bar, string label, Color accent, UnityAction onClick)
    {
        var img = Panel(bar, "Tool_" + label, CARD); SizeElem(img.gameObject, 108, 40); Outline(img, LINE);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
        var dot = Panel(img.rectTransform, "dot", accent);
        dot.rectTransform.anchorMin = new Vector2(0, 0.5f); dot.rectTransform.anchorMax = new Vector2(0, 0.5f);
        dot.rectTransform.pivot = new Vector2(0, 0.5f); dot.rectTransform.anchoredPosition = new Vector2(10, 0);
        dot.rectTransform.sizeDelta = new Vector2(9, 9);
        var t = Text(img.rectTransform, label, 12, TEXT, TextAlignmentOptions.Center);
        StretchOffset(t.rectTransform, 22, 6, 6, 6);
    }
    private void ToolButtonDisabled(Graphic bar, string label)
    {
        var img = Panel(bar, "Tool_" + label, C("#141220")); SizeElem(img.gameObject, 108, 40); Outline(img, C("#252036"));
        var t = Text(img.rectTransform, label, 11.5f, FAINT, TextAlignmentOptions.Center);
        StretchFull(t.rectTransform);
    }
    // 主要ボタン（生成/侵略）
    private Button PrimaryButton(Graphic parent, string label, Color bg, Color fg, UnityAction onClick)
    {
        var img = Panel(parent, "Primary_" + label, bg);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
        var cb = btn.colors; cb.highlightedColor = Color.Lerp(bg, Color.white, 0.12f); cb.pressedColor = Color.Lerp(bg, Color.black, 0.12f);
        cb.disabledColor = Color.Lerp(bg, Color.gray, 0.5f); btn.colors = cb;
        var t = Text(img.rectTransform, label, 14.5f, fg, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(t.rectTransform);
        return btn;
    }
    private void StretchFull(RectTransform rt)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
    private void StretchOffset(RectTransform rt, float l, float t, float r, float b)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t); }
}
