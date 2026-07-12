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
    private DungeonFeatureManager featureMgr;

    private TMP_FontAsset uiFont;

    // ===== Bloodlines スキン（スプライトはMCP/インスペクタで割当。未割当ならフラット色にフォールバック）=====
    [Header("Bloodlines Skin")]
    [SerializeField] private Sprite skinFrame;   // 大枠(9スライス)：側面パネル用
    [SerializeField] private Sprite skinBar;     // HUD帯/小枠(9スライス)
    [SerializeField] private Sprite btnGray, btnGrayHover, btnGrayPressed, btnGrayDisabled;
    [SerializeField] private Sprite btnRed, btnRedHover, btnRedPressed, btnRedDisabled;
    [SerializeField] private Sprite barFill, barTrack;

    // 魔王HPバー（上部HUD）
    private Image dlHpFill; private TextMeshProUGUI dlHpLabel; private GameObject dlHpBar;
    private const float DL_HP_TRACK_W = 118f;

    // ライブ更新するUI要素
    private TextMeshProUGUI dpText, fameText, matText, turnText, phaseText, costText, threatText;
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

    // 遺物パネル
    private GameObject relicPanel;
    private TextMeshProUGUI relicSlotText;
    private readonly List<(Image card, TextMeshProUGUI label, int idx)> relicCards = new List<(Image, TextMeshProUGUI, int)>();

    // 眷属種族セレクタ（下部バー・旧）
    private int selSpecies = 0;
    private readonly List<Image> speciesBtns = new List<Image>();

    // 🧟 配下図鑑（下部バーの「図鑑」ボタン→パネル。MinionCatalog16種を家系→役割→個体で選ぶ）
    private GameObject minionPanel;
    private RectTransform minionListContainer;
    private TextMeshProUGUI minionBarLabel;
    private int codexFamilyTab = 0;
    private readonly List<Image> codexTabBtns = new List<Image>();
    // 🛡️ 部隊編成トレイ（図鑑下部）
    private RectTransform squadSlotContainer;
    private TextMeshProUGUI squadInfoText;
    // 🎯 隊員配置ストリップ（下部バー上・「部隊」ツールで隊員を選んで個別配置）
    private GameObject squadStrip;
    // 🪤 罠の種類ストリップ（「罠」ツールで種類を選ぶ）
    private GameObject trapStrip;

    // 🔬 研究ツリーパネル
    private GameObject researchPanel;
    private RectTransform researchNodeContainer;
    private TextMeshProUGUI researchRpText;

    // 🗺️ 階層拡張トラック
    private GameObject expandPanel;
    private RectTransform expandRowsContainer;

    // descent演出
    private CanvasGroup descentToastCg;
    private TextMeshProUGUI descentToastText;
    private float descentToastTimer;
    private CanvasGroup floorFadeCg;
    private float floorFadeTimer;
    private const float FADE_DUR = 0.35f;

    // フロア（階層）
    private DungeonFloorManager floorMgr;
    private int selFloors = 1; // 0=1層,1=2層,2=3層
    private readonly List<Image> floorCountBtns = new List<Image>();
    private GameObject floorTabsPanel;
    private readonly List<(Image img, TextMeshProUGUI label, int idx)> floorTabs = new List<(Image, TextMeshProUGUI, int)>();

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
    // 🩸 Bloodlines: 黒×血の赤（帯/枠/主要アクションのアクセント）
    Color BLOOD   = C("#b0202b");
    Color BLOOD_DK= C("#3a0d12");
    Color HUD_BG  = C("#0e0a0c");

    private void Start()
    {
        generator = Object.FindFirstObjectByType<DungeonGenerator>();
        res = Object.FindFirstObjectByType<DungeonResourceManager>();
        turn = Object.FindFirstObjectByType<DungeonTurnManager>();
        input = Object.FindFirstObjectByType<GridInputHandler>();
        featureMgr = Object.FindFirstObjectByType<DungeonFeatureManager>();
        floorMgr = Object.FindFirstObjectByType<DungeonFloorManager>();

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
        BuildFloorTabs(root);
        BuildGenPanel(root);
        BuildDemonPanel(root);
        BuildEmotionPanel(root);
        BuildRelicPanel(root);
        BuildResearchPanel(root);
        BuildExpandPanel(root);
        BuildMinionCodex(root);
        BuildBottomBar(root);
        BuildSquadStrip(root);
        BuildTrapStrip(root);
        BuildDescentFX(root);
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
        Outline(panel, LINE2); SkinPanel(panel);

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
        Outline(panel, LINE2); SkinPanel(panel);

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
            bool deepest = floorMgr.IsDeepest(i);
            floorTabs[i].label.text = "B" + (i + 1) + "F" + (deepest ? "魔" : "");
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
        panel.rectTransform.sizeDelta = new Vector2(460, 296);
        panel.rectTransform.anchoredPosition = new Vector2(0, 20);
        Outline(panel, LINE2); SkinPanel(panel);

        float pad = 18f, w = 460 - pad * 2;
        var title = Text(panel, "遺物（全体パッシブ・スロット制）", 15, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 12, w - 40, 22);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => relicPanel.SetActive(false));
        Place((RectTransform)close.transform, 460 - pad - 28, 12, 28, 26);

        relicSlotText = Text(panel, "装備スロット: ―", 12, VIOLET, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(relicSlotText.rectTransform, pad, 40, w, 18);

        var rm = RelicManager.Instance;
        int count = rm != null ? rm.Catalog.Count : 0;
        for (int i = 0; i < count; i++)
        {
            int idx = i; var rel = rm.Catalog[i];
            float cw = (w - 10) / 2f;
            float cx = pad + (i % 2) * (cw + 10);
            float cy = 66 + (i / 2) * 78;
            var card = Panel(panel, "Relic_" + i, CARD);
            Place(card.rectTransform, cx, cy, cw, 70); Outline(card, LINE);
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
            btn.onClick.AddListener(() => { RelicManager.Instance?.Toggle(idx); RefreshRelicPanel(); });
            var nm = Text(card.rectTransform, rel.name, 13, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm.rectTransform, 10, 8, cw - 16, 18);
            var ds = Text(card.rectTransform, rel.desc, 11, MUTED, TextAlignmentOptions.TopLeft);
            Place(ds.rectTransform, 10, 28, cw - 16, 16);
            var st = Text(card.rectTransform, "", 11, GREEN, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(st.rectTransform, 10, 47, cw - 16, 16);
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
            relicSlotText.text = "装備スロット: " + string.Join(" / ", parts);
        }
        foreach (var c in relicCards)
        {
            bool eq = rm.IsEquipped(c.idx);
            if (c.label != null) c.label.text = eq ? "装備中" : "未装備";
            if (c.label != null) c.label.color = eq ? GREEN : FAINT;
            if (c.card != null)
            {
                c.card.color = eq ? SEL : CARD;
                var o = c.card.GetComponent<Outline>(); if (o != null) o.effectColor = eq ? GOLD : LINE;
            }
        }
    }

    // ---------- 配下図鑑（ロスター選択） ----------
    private void BuildMinionCodex(RectTransform root)
    {
        var panel = Panel(root, "MinionCodex", PANEL);
        minionPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(620, 520);
        panel.rectTransform.anchoredPosition = new Vector2(0, 6);
        Outline(panel, LINE2); SkinPanel(panel);

        float pad = 22f, w = 620 - pad * 2;
        var title = Text(panel, "配下図鑑（配置する防衛体を選ぶ）", 15, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 14, w - 40, 22);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => minionPanel.SetActive(false));
        Place((RectTransform)close.transform, 620 - pad - 28, 12, 28, 26);

        // 家系タブ（不死/獣/魔族）
        string[] fam = { "不死", "獣", "魔族" };
        Color[] famCol = { GREEN, GOLD, VIOLET };
        float tabW = 96;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var b = Panel(panel, "CodexTab_" + i, CARD); Place(b.rectTransform, pad + i * (tabW + 8), 44, tabW, 28); Outline(b, LINE);
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
            btn.onClick.AddListener(() => { codexFamilyTab = idx; RefreshMinionCodex(); });
            var tt = Text(b.rectTransform, fam[i], 12.5f, famCol[i], TextAlignmentOptions.Center, FontStyles.Bold); StretchFull(tt.rectTransform);
            codexTabBtns.Add(b);
        }

        // 個体リストのコンテナ（下部の編成トレイ分を残す）
        var list = NewRect("List", panel.rectTransform);
        Place(list, pad, 82, w, 520 - 82 - 92);
        minionListContainer = list;

        // 🛡️ 編成トレイ（5枠＋コスト/コンプ表示＋クリア）
        float trayY = 520 - 84;
        var trayLabel = Text(panel, "部隊編成（役割を散らすほど部隊バフ↑）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(trayLabel.rectTransform, pad, trayY, w, 14);
        var slots = NewRect("SquadSlots", panel.rectTransform);
        Place(slots, pad, trayY + 18, 5 * 96, 30);
        squadSlotContainer = slots;
        var clearBtn = PrimaryButton(panel, "クリア", PANEL2, TEXT, () => { featureMgr?.SquadClear(); RefreshSquadTray(); RefreshMinionCodex(); });
        Place((RectTransform)clearBtn.transform, pad + 5 * 96 + 8, trayY + 18, w - (5 * 96 + 8), 30);
        squadInfoText = Text(panel, "", 11.5f, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(squadInfoText.rectTransform, pad, trayY + 52, w, 16);

        RefreshMinionCodex();
        RefreshSquadTray();
        minionPanel.SetActive(false);
    }

    // 🛡️ 編成トレイの再描画（5枠：個体名/空、クリックで抜く）＋コスト/コンプ表示
    private void RefreshSquadTray()
    {
        if (squadSlotContainer == null || featureMgr == null) return;
        for (int i = squadSlotContainer.childCount - 1; i >= 0; i--)
        {
            var c = squadSlotContainer.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        var squad = featureMgr.CurrentSquad;
        float slotW = 96, slotH = 30;
        for (int i = 0; i < DungeonFeatureManager.SquadMaxSlots; i++)
        {
            int slot = i;
            var chip = Panel(squadSlotContainer, "Slot_" + i, CARD); Place(chip.rectTransform, i * slotW, 0, slotW - 6, slotH); Outline(chip, LINE);
            bool filled = i < squad.Count;
            string label = filled ? MinionCatalog.Get(squad[i]).jpName : "空";
            var col = filled ? RoleColor(MinionCatalog.Get(squad[i]).role) : FAINT;
            var tt = Text(chip.rectTransform, label, 10.5f, col, TextAlignmentOptions.Center, FontStyles.Bold); StretchFull(tt.rectTransform);
            if (filled)
            {
                var b = chip.gameObject.AddComponent<Button>(); b.targetGraphic = chip;
                b.onClick.AddListener(() => { featureMgr.SquadRemoveAt(slot); RefreshSquadTray(); RefreshMinionCodex(); });
            }
        }
        if (squadInfoText != null)
        {
            int roles = featureMgr.SquadDistinctRoles(); float comp = featureMgr.SquadCompMult();
            int n = featureMgr.CurrentSquad.Count;
            squadInfoText.text = n == 0 ? "<color=#9c95b4>配下を＋隊で追加 → 図鑑を閉じ、下部バー「部隊」で隊員を個別配置</color>"
                : string.Format("役割{0}種　部隊バフ <color=#5cc47c>×{1:0.00}</color>　<size=88%><color=#9c95b4>（各隊員を「部隊」ツールで好きな場所へ）</color></size>", roles, comp);
        }
        RefreshSquadStrip();
    }

    // 🎯 隊員配置ストリップ（下部バー上）：編成した隊員を並べ、選択→マスクリックで個別配置。
    private void BuildSquadStrip(RectTransform root)
    {
        var panel = Panel(root, "SquadStrip", C("#0e0b16"));
        Anchor(panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        panel.rectTransform.sizeDelta = new Vector2(700, 40);
        panel.rectTransform.anchoredPosition = new Vector2(0, 66);
        Outline(panel, LINE2);
        var lbl = Text(panel, "配置する隊員 ▸", 11, C("#8cb8e6"), TextAlignmentOptions.Left, FontStyles.Bold);
        Place(lbl.rectTransform, 12, 12, 96, 16);
        squadStrip = panel.gameObject;
        RefreshSquadStrip();
    }

    private void RefreshSquadStrip()
    {
        if (squadStrip == null || featureMgr == null) return;
        // ラベル(子0)を残して隊員ボタンを破棄
        for (int i = squadStrip.transform.childCount - 1; i >= 1; i--)
        {
            var c = squadStrip.transform.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        var squad = featureMgr.CurrentSquad;
        squadStrip.SetActive(squad.Count > 0);
        if (squad.Count == 0) return;

        int sel = featureMgr.SquadPlaceSlot;
        float bw = 108, x0 = 114;
        for (int i = 0; i < squad.Count; i++)
        {
            int slot = i; var d = MinionCatalog.Get(squad[i]);
            var b = Panel(squadStrip.transform, "Member_" + i, CARD);
            Place(b.rectTransform, x0 + i * (bw + 4), 5, bw, 30); Outline(b, LINE);
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
            btn.onClick.AddListener(() => { featureMgr.SetSquadPlaceSlot(slot); input?.SetToolMode(11); RefreshSquadStrip(); });
            var tt = Text(b.rectTransform, d.jpName + " <size=76%><color=#9c95b4>T" + d.tierCP + "</color></size>", 10.5f, RoleColor(d.role), TextAlignmentOptions.Center, FontStyles.Bold);
            StretchFull(tt.rectTransform);
            SetSel(b, i == sel);
        }
        ((RectTransform)squadStrip.transform).sizeDelta = new Vector2(x0 + squad.Count * (bw + 4) + 8, 40);
    }

    // 🪤 罠の種類ストリップ（「罠」ツールで種類を選ぶ。ロック=領域研究で未解禁）
    private void BuildTrapStrip(RectTransform root)
    {
        var panel = Panel(root, "TrapStrip", C("#0e0b16"));
        Anchor(panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        panel.rectTransform.sizeDelta = new Vector2(780, 40);
        panel.rectTransform.anchoredPosition = new Vector2(0, 110);
        Outline(panel, LINE2);
        var lbl = Text(panel, "罠の種類 ▸", 11, CRIMSON, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(lbl.rectTransform, 12, 12, 84, 16);
        trapStrip = panel.gameObject;
        RefreshTrapStrip();
        trapStrip.SetActive(false);
    }

    private void RefreshTrapStrip()
    {
        if (trapStrip == null || featureMgr == null) return;
        for (int i = trapStrip.transform.childCount - 1; i >= 1; i--)
        {
            var c = trapStrip.transform.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        int sel = featureMgr.SelectedTrapKind;
        float bw = 110, x0 = 100;
        for (int k = 0; k < TrapCatalog.Count; k++)
        {
            int kk = k; var d = TrapCatalog.Get(k);
            bool unlocked = TrapCatalog.IsUnlocked(k);
            var b = Panel(trapStrip.transform, "Trap_" + k, CARD);
            Place(b.rectTransform, x0 + k * (bw + 4), 5, bw, 30); Outline(b, LINE);
            var tt = Text(b.rectTransform, d.name + (unlocked ? " <size=78%><color=#9c95b4>" + d.dpCost + "</color></size>" : " 🔒"), 10.5f, unlocked ? d.color : FAINT, TextAlignmentOptions.Center, FontStyles.Bold);
            StretchFull(tt.rectTransform);
            if (unlocked)
            {
                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
                btn.onClick.AddListener(() => { featureMgr.SetSelectedTrapKind(kk); input?.SetToolMode(3); RefreshTrapStrip(); });
            }
            SetSel(b, k == sel && unlocked);
        }
        ((RectTransform)trapStrip.transform).sizeDelta = new Vector2(x0 + TrapCatalog.Count * (bw + 4) + 8, 40);
    }

    private static Color RoleColor(MinionCatalog.Role r)
    {
        switch (r)
        {
            case MinionCatalog.Role.Tank: return C("#57c3ab");
            case MinionCatalog.Role.Melee: return C("#df5a5a");
            case MinionCatalog.Role.Ranged: return C("#b48be6");
            case MinionCatalog.Role.Buff: return C("#e3a94a");
            default: return C("#5cc47c"); // Debuff
        }
    }

    private void RefreshMinionCodex()
    {
        if (minionListContainer == null) return;
        for (int i = 0; i < codexTabBtns.Count; i++) SetSel(codexTabBtns[i], i == codexFamilyTab);
        // 既存行を破棄して作り直し（Destroyは遅延実行なので、まず非表示化して同フレームの重なりを防ぐ）
        for (int i = minionListContainer.childCount - 1; i >= 0; i--)
        {
            var c = minionListContainer.GetChild(i).gameObject;
            c.SetActive(false);
            Destroy(c);
        }

        var famEnum = (ZombieAI.Species)codexFamilyTab;
        int selIdx = featureMgr != null ? featureMgr.SelectedMinionIndex : -1;
        float y = 0f, rowH = 56f, listW = minionListContainer.rect.width;
        for (int k = 0; k < MinionCatalog.Count; k++)
        {
            var d = MinionCatalog.Get(k);
            if (d.family != famEnum) continue;
            int kk = k;
            bool unlocked = MinionEvolution.IsUnlocked(kk); // 🧬 進化解禁済みか
            var row = Panel(minionListContainer, "Row_" + d.id, CARD);
            Place(row.rectTransform, 0, y, listW, rowH - 6); Outline(row, LINE);
            var btn = row.gameObject.AddComponent<Button>(); btn.targetGraphic = row;
            btn.onClick.AddListener(() => { if (unlocked) { featureMgr?.SetSelectedMinion(kk); UpdateMinionBarLabel(); } RefreshMinionCodex(); });

            var nm = Text(row.rectTransform, d.jpName, 13.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm.rectTransform, 12, 6, 150, 18);
            var role = Text(row.rectTransform, "[" + MinionCatalog.RoleName(d.role) + "]", 11, RoleColor(d.role), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(role.rectTransform, 12, 26, 150, 16);
            var stat = Text(row.rectTransform, string.Format("T{0}   HP×{1:0.00}  ATK×{2:0.00}  SPD×{3:0.00}", d.tierCP, d.hpMult, d.atkMult, d.spdMult), 11, MUTED, TextAlignmentOptions.TopLeft);
            Place(stat.rectTransform, 172, 6, listW - 250, 16);
            var note = Text(row.rectTransform, d.note, 10.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(note.rectTransform, 172, 26, listW - 250, 22);

            // 🧬 進化ロック状態に応じてアクション（＋隊 / 進化 / 🔒）
            if (!unlocked)
            {
                nm.color = FAINT; // 名前を淡色に
                string pn = MinionEvolution.PrereqName(kk);
                if (MinionEvolution.CanEvolve(kk))
                    note.text = "<color=#e3a94a>🔓 " + pn + " から進化可 ・ " + MinionEvolution.EvolveCost(kk) + "DP</color>";
                else if (MinionEvolution.TierResearchNeeded(kk))
                    note.text = "<color=#8cb8e6>🔬 研究で開放（" + MinionEvolution.TierResearchName(kk) + "）</color>";
                else
                    note.text = "<color=#9c95b4>🔒 " + pn + " の解禁が必要</color>";
            }
            if (unlocked)
            {
                var addBtn = PrimaryButton(row, "＋隊", PANEL2, TEAL, () => { if (featureMgr != null && featureMgr.SquadAdd(kk)) RefreshSquadTray(); });
                Place((RectTransform)addBtn.transform, listW - 68, 13, 58, 24);
            }
            else if (MinionEvolution.CanEvolve(kk))
            {
                var evoBtn = PrimaryButton(row, "進化", BLOOD, TEXT, () => { if (MinionEvolution.TryEvolve(kk)) RefreshMinionCodex(); }, true);
                Place((RectTransform)evoBtn.transform, listW - 72, 13, 62, 24);
            }

            SetSel(row, k == selIdx);
            y += rowH;
        }
    }

    private void UpdateMinionBarLabel()
    {
        if (minionBarLabel == null || featureMgr == null) return;
        var d = featureMgr.SelectedMinion;
        minionBarLabel.text = d.jpName + " <size=78%><color=#9c95b4>[" + MinionCatalog.RoleName(d.role) + "/T" + d.tierCP + "]</color></size>";
    }

    // ---------- 研究ツリー（Civ第2の木／CDO2研究） ----------
    private void BuildResearchPanel(RectTransform root)
    {
        var panel = Panel(root, "ResearchPanel", PANEL);
        researchPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(936, 600);
        panel.rectTransform.anchoredPosition = new Vector2(0, 6);
        Outline(panel, LINE2); SkinPanel(panel);

        float pad = 22f, w = 936 - pad * 2;
        var title = Text(panel, "研究ツリー（知識でRP蓄積・前提＋RPで解禁）", 15, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 14, w - 220, 22);
        researchRpText = Text(panel, "", 14, C("#8cb8e6"), TextAlignmentOptions.Right, FontStyles.Bold);
        Place(researchRpText.rectTransform, pad + w - 240, 14, 190, 22);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => researchPanel.SetActive(false));
        Place((RectTransform)close.transform, 936 - pad - 28, 12, 28, 26);

        var cont = NewRect("Nodes", panel.rectTransform);
        Place(cont, pad, 46, w, 600 - 46 - pad);
        researchNodeContainer = cont;

        RefreshResearchPanel();
        researchPanel.SetActive(false);
    }

    private void RefreshResearchPanel()
    {
        if (researchNodeContainer == null) return;
        if (researchRpText != null) researchRpText.text = "研究点 <color=#8cb8e6>" + ResearchState.RP + " RP</color>";
        for (int i = researchNodeContainer.childCount - 1; i >= 0; i--)
        {
            var c = researchNodeContainer.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        float contW = researchNodeContainer.rect.width;
        float colGap = 10f, colW = (contW - colGap * 3) / 4f, cellH = 52f;
        var fields = new ResearchField[] { ResearchField.Monster, ResearchField.Domain, ResearchField.Refine, ResearchField.DemonLord };
        for (int c = 0; c < 4; c++)
        {
            float cx = c * (colW + colGap);
            var head = Text(researchNodeContainer, ResearchCatalog.FieldName(fields[c]), 12.5f, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(head.rectTransform, cx, 0, colW, 18);
            var nodes = ResearchCatalog.ByField(fields[c]);
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                float cy = 24 + i * (cellH + 6);
                bool done = ResearchState.IsResearched(node.id);
                bool prereqOK = ResearchState.PrereqMet(node);
                bool can = ResearchState.CanResearch(node.id);
                var cell = Panel(researchNodeContainer, "R_" + node.id, CARD);
                Place(cell.rectTransform, cx, cy, colW, cellH); Outline(cell, done ? GREEN : (can ? GOLD : LINE));
                var nm = Text(cell.rectTransform, node.jpName, 11.5f, done ? GREEN : (prereqOK ? TEXT : FAINT), TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(nm.rectTransform, 8, 5, colW - 16, 15);
                string state = done ? "研究済" : (prereqOK ? ("コスト " + node.cost + " RP") : "🔒 前提未達");
                var st = Text(cell.rectTransform, state, 10, done ? GREEN : (can ? GOLD : MUTED), TextAlignmentOptions.TopLeft);
                Place(st.rectTransform, 8, 22, colW - 16, 13);
                var ds = Text(cell.rectTransform, node.desc, 9f, FAINT, TextAlignmentOptions.TopLeft);
                Place(ds.rectTransform, 8, 35, colW - 16, cellH - 37);
                if (can)
                {
                    var btn = cell.gameObject.AddComponent<Button>(); btn.targetGraphic = cell;
                    btn.onClick.AddListener(() => { if (ResearchState.TryResearch(node.id)) { RefreshResearchPanel(); RefreshMinionCodex(); } });
                }
            }
        }
    }

    // ---------- 階層拡張トラック（横拡張：研究点＋DP） ----------
    private void BuildExpandPanel(RectTransform root)
    {
        var panel = Panel(root, "ExpandPanel", PANEL);
        expandPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(600, 384);
        panel.rectTransform.anchoredPosition = new Vector2(0, 10);
        Outline(panel, LINE2); SkinPanel(panel);

        float pad = 22f, w = 600 - pad * 2;
        var title = Text(panel, "階層拡張（各階を10→50へ・階段は入口から最遠）", 14.5f, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 14, w - 40, 22);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => expandPanel.SetActive(false));
        Place((RectTransform)close.transform, 600 - pad - 28, 12, 28, 26);
        var sub = Text(panel, "研究点RP＋DPを消費して1段拡張。拡張時は配置クリア＋50%返金（準備中のみ）。", 11, MUTED, TextAlignmentOptions.Left);
        Place(sub.rectTransform, pad, 40, w, 16);

        var cont = NewRect("Rows", panel.rectTransform);
        Place(cont, pad, 66, w, 384 - 66 - pad);
        expandRowsContainer = cont;

        RefreshExpandPanel();
        expandPanel.SetActive(false);
    }

    private void RefreshExpandPanel()
    {
        if (expandRowsContainer == null || floorMgr == null) return;
        for (int i = expandRowsContainer.childCount - 1; i >= 0; i--)
        {
            var c = expandRowsContainer.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        bool prep = turn == null || turn.IsPreparePhase;
        int n = floorMgr.BuiltFloorCount;
        float rowH = 52f, y = 0f, w = expandRowsContainer.rect.width;
        if (n == 0)
        {
            var none = Text(expandRowsContainer, "<color=#9c95b4>まず迷宮を生成してください。</color>", 12, MUTED, TextAlignmentOptions.Left);
            Place(none.rectTransform, 0, 4, w, 18);
            return;
        }
        for (int i = 0; i < n; i++)
        {
            int fi = i;
            var row = Panel(expandRowsContainer, "ExRow_" + i, CARD);
            Place(row.rectTransform, 0, y, w, rowH - 6); Outline(row, LINE);
            int size = floorMgr.FloorSize(i);
            bool deepest = floorMgr.IsDeepest(i);
            var nm = Text(row.rectTransform, "B" + (i + 1) + "F" + (deepest ? " 魔" : "") + "  <size=115%>" + size + "×" + size + "</size>", 13, deepest ? CRIMSON : TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm.rectTransform, 12, 13, 180, 20);
            if (floorMgr.CanExpandFloor(i))
            {
                int ns = floorMgr.NextFloorSize(i), rp = floorMgr.ExpandRPCost(i), dp = floorMgr.ExpandDPCost(i);
                var info = Text(row.rectTransform, "→ " + ns + "×" + ns + "    <color=#8cb8e6>" + rp + " RP</color>  <color=#e3a94a>" + dp + " DP</color>", 12, MUTED, TextAlignmentOptions.Left);
                Place(info.rectTransform, 190, 13, w - 300, 20);
                var btn = PrimaryButton(row, "拡張", BLOOD, TEXT, () => { if (floorMgr.TryExpandFloor(fi)) { RefreshExpandPanel(); RefreshFloorTabs(); } }, true);
                Place((RectTransform)btn.transform, w - 98, 8, 86, 30);
                btn.interactable = prep && ResearchState.RP >= rp && (res == null || res.DungeonPoints >= dp);
            }
            else
            {
                var mx = Text(row.rectTransform, "<color=#5cc47c>最大 (50×50)</color>", 12, GREEN, TextAlignmentOptions.Left);
                Place(mx.rectTransform, 190, 15, 200, 16);
            }
            y += rowH;
        }

        // 🏢 縦拡張（階層追加）行：準備中のみ・削除不可・4層以降は領域研究(d_floor4/5)ゲート
        if (n < 5)
        {
            var addRow = Panel(expandRowsContainer, "AddFloorRow", CARD);
            Place(addRow.rectTransform, 0, y, w, rowH - 6); Outline(addRow, BLOOD_DK);
            bool can = floorMgr.CanAddFloor();
            int cost = floorMgr.AddFloorDPCost();
            string need = floorMgr.AddFloorResearchNeeded();
            var nm2 = Text(addRow.rectTransform, "＋ 第" + (n + 1) + "層を追加（最下層に）", 13, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm2.rectTransform, 12, 13, 220, 20);
            string info = can ? ("<color=#e3a94a>" + cost + " DP</color>")
                : (need != "" && ResearchCatalog.TryGet(need, out var rn) ? "<color=#8cb8e6>🔬 研究「" + rn.jpName + "」が必要</color>" : "—");
            var inf = Text(addRow.rectTransform, info, 12, MUTED, TextAlignmentOptions.Left);
            Place(inf.rectTransform, 248, 13, w - 350, 20);
            var abtn = PrimaryButton(addRow, "追加", BLOOD, TEXT, () => { if (floorMgr.TryAddFloor()) { RefreshExpandPanel(); RefreshFloorTabs(); } }, true);
            Place((RectTransform)abtn.transform, w - 98, 8, 86, 30);
            abtn.interactable = prep && can && (res == null || res.DungeonPoints >= cost);
        }
    }

    // ---------- descent演出（フェード＋降下トースト） ----------
    private void BuildDescentFX(RectTransform root)
    {
        // フロア切替フェード（全画面・黒・最前面）
        var fade = Panel(root, "FloorFade", Color.black);
        StretchFull(fade.rectTransform);
        floorFadeCg = fade.gameObject.AddComponent<CanvasGroup>();
        floorFadeCg.alpha = 0f; floorFadeCg.blocksRaycasts = false; floorFadeCg.interactable = false;
        fade.rectTransform.SetAsLastSibling();

        // 降下トースト（中央上寄りバナー）
        var toast = Panel(root, "DescentToast", C("#0e0b16"));
        Anchor(toast, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        toast.rectTransform.sizeDelta = new Vector2(540, 96);
        toast.rectTransform.anchoredPosition = new Vector2(0, 130);
        Outline(toast, GOLD);
        descentToastText = Text(toast, "", 30, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(descentToastText.rectTransform);
        descentToastCg = toast.gameObject.AddComponent<CanvasGroup>();
        descentToastCg.alpha = 0f; descentToastCg.blocksRaycasts = false; descentToastCg.interactable = false;
        toast.rectTransform.SetAsLastSibling();
    }

    /// <summary>降下トーストを表示（DungeonFloorManager.Descentから呼ばれる）。</summary>
    public void ShowDescentToast(string floorLabel, int survivors)
    {
        if (descentToastText == null) return;
        descentToastText.text = $"{floorLabel} へ降下！　<size=60%><color=#9c95b4>生存者 {survivors}</color></size>";
        descentToastTimer = 1.7f;
        if (descentToastCg != null) descentToastCg.alpha = 1f;
    }

    /// <summary>フロア切替の暗転フェードを再生。</summary>
    public void PlayFloorTransition()
    {
        floorFadeTimer = FADE_DUR;
        if (floorFadeCg != null) floorFadeCg.alpha = 1f;
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
        var bar = Panel(root, "TopBar", HUD_BG);
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
        var relBtn = PrimaryButton(bar, "遺物", PANEL2, TEXT, () => { if (relicPanel != null) { relicPanel.SetActive(!relicPanel.activeSelf); RefreshRelicPanel(); } });
        SizeElem(relBtn.gameObject, 66, 34);
        var rsBtn = PrimaryButton(bar, "研究", PANEL2, TEXT, () => { if (researchPanel != null) { researchPanel.SetActive(!researchPanel.activeSelf); RefreshResearchPanel(); } });
        SizeElem(rsBtn.gameObject, 66, 34);
        var exBtn = PrimaryButton(bar, "拡張", PANEL2, TEXT, () => { if (expandPanel != null) { expandPanel.SetActive(!expandPanel.activeSelf); RefreshExpandPanel(); } });
        SizeElem(exBtn.gameObject, 66, 34);

        // 🩸 魔王HPバー（討伐＝ゲームオーバーの核。常時可視）
        BuildDemonLordHpBar(bar);

        // 伸縮スペーサ
        Spacer(bar);

        // 資源
        dpText = ResChip(bar, GOLD, "DP", "0");
        fameText = ResChip(bar, VIOLET, "名声", "0");
        matText = ResChip(bar, TEAL, "素材", "0");
        threatText = ResChip(bar, BLOOD, "脅威度", "1.00"); // 🕸️ 誘導経済：世界の脅威度
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

    // 🩸 魔王HPバー（上部HUD・Bloodlinesバー）
    private void BuildDemonLordHpBar(Graphic bar)
    {
        var wrap = Panel(bar, "DLHpBar", HUD_BG); SizeElem(wrap.gameObject, 176, 40); Outline(wrap, BLOOD_DK);
        dlHpBar = wrap.gameObject;
        dlHpLabel = Text(wrap.rectTransform, "魔王 Lv1", 10.5f, BLOOD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(dlHpLabel.rectTransform, 10, 5, 156, 14);

        var track = Panel(wrap.rectTransform, "track", C("#241014"));
        Place(track.rectTransform, 10, 21, DL_HP_TRACK_W, 12);
        ApplyFrame(track, barTrack, Color.white);

        dlHpFill = Panel(track.rectTransform, "fill", BLOOD);
        dlHpFill.rectTransform.anchorMin = new Vector2(0, 0.5f);
        dlHpFill.rectTransform.anchorMax = new Vector2(0, 0.5f);
        dlHpFill.rectTransform.pivot = new Vector2(0, 0.5f);
        dlHpFill.rectTransform.anchoredPosition = Vector2.zero;
        dlHpFill.rectTransform.sizeDelta = new Vector2(DL_HP_TRACK_W, 12);
        if (barFill != null)
        {
            dlHpFill.sprite = barFill; dlHpFill.color = Color.white;
            dlHpFill.type = Image.Type.Filled; dlHpFill.fillMethod = Image.FillMethod.Horizontal; dlHpFill.fillOrigin = 0;
        }
    }

    // ---------- ①迷宮生成パネル ----------
    private void BuildGenPanel(RectTransform root)
    {
        var panel = Panel(root, "GenPanel", PANEL);
        genPanel = panel.gameObject;
        Anchor(panel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        panel.rectTransform.sizeDelta = new Vector2(360, 524);
        panel.rectTransform.anchoredPosition = new Vector2(-16, -76);
        Outline(panel, LINE2); Round(panel, 14); SkinPanel(panel);

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

        // 階層数（多いほどコスト大・魔王まで遠い＝防御が深くなる）
        var fl = Text(panel, "階層数（深いほどコスト大・防御が深くなる）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(fl.rectTransform, pad, 392, w, 16);
        string[] fNames = { "1層", "2層", "3層" };
        float fcw = (w - 16) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            float cx = pad + i * (fcw + 8);
            var b = Chip(panel, cx, 412, fcw, 30, fNames[i], VIOLET, () => { selFloors = idx; floorMgr?.SetFloorCount(idx + 1); RefreshSelections(); RefreshCost(); });
            floorCountBtns.Add(b);
        }

        // コスト表示
        costText = Text(panel, "生成コスト  500 DP", 12.5f, MUTED, TextAlignmentOptions.Left);
        Place(costText.rectTransform, pad, 450, w, 18);

        // 生成ボタン
        generateBtn = PrimaryButton(panel, "迷宮を生成する", BLOOD, C("#f0d9a0"), () =>
        {
            if (generator == null) return;
            if (floorMgr != null) floorMgr.SetFloorCount(selFloors + 1);
            bool ok = generator.TryGenerateWithCost();
            RefreshCost();
            RefreshFloorTabs();
        }, true);
        Place((RectTransform)generateBtn.transform, pad, 472, w, 44);

        RefreshSelections();
    }

    // ---------- ③下部コマンドバー ----------
    private void BuildBottomBar(RectTransform root)
    {
        var bar = Panel(root, "BottomBar", HUD_BG);
        Anchor(bar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        bar.rectTransform.sizeDelta = new Vector2(0, 60); bar.rectTransform.anchoredPosition = Vector2.zero;
        AddTopBorder(bar);
        var h = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(16, 16, 9, 9); h.spacing = 10; h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;

        var hint = Text(bar, "配置ツール", 11, FAINT, TextAlignmentOptions.Left);
        SizeElem(hint.gameObject, 68, 40);

        ToolButton(bar, "トーテム", TEAL, () => input?.SetToolMode(6));
        ToolButton(bar, "罠", CRIMSON, () => { input?.SetToolMode(3); if (trapStrip != null) { trapStrip.SetActive(!trapStrip.activeSelf); RefreshTrapStrip(); } });
        ToolButton(bar, "スポナー", VIOLET, () => input?.SetToolMode(7));
        ToolButton(bar, "ボス", CRIMSON, () => input?.SetToolMode(8));
        ToolButton(bar, "特殊敵", GOLD, () => input?.SetToolMode(9));
        ToolButton(bar, "宝箱", GREEN, () => input?.SetToolMode(12)); // 🎣 誘導宝箱（錬成研究で解禁）
        ToolButton(bar, "部隊", C("#8cb8e6"), () => input?.SetToolMode(11));
        ToolButton(bar, "消去", MUTED, () => input?.SetToolMode(10));
        ToolButton(bar, "冒険者(検証)", GOLD, () => input?.SetToolMode(4));

        // 🧟 配下セレクタ（図鑑を開いてロスター16種から選ぶ）
        var sp = Text(bar, "配下", 11, FAINT, TextAlignmentOptions.Center);
        SizeElem(sp.gameObject, 40, 40);
        var codexBtn = PrimaryButton(bar, "図鑑 ▸", PANEL2, TEXT, () => { if (minionPanel != null) { minionPanel.SetActive(!minionPanel.activeSelf); RefreshMinionCodex(); RefreshSquadTray(); } });
        SizeElem(codexBtn.gameObject, 76, 42);
        minionBarLabel = Text(bar, "", 12, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        SizeElem(minionBarLabel.gameObject, 168, 42);
        UpdateMinionBarLabel();

        Spacer(bar);

        var extendBtn = PrimaryButton(bar, "戦闘時間 +1分", PANEL2, TEXT, () => turn?.ExtendWaveLimit());
        SizeElem(extendBtn.gameObject, 150, 42);

        invadeBtn = PrimaryButton(bar, "⚔ 侵略開始", BLOOD, TEXT, () => turn?.StartBattlePhase(), true);
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
        if (threatText != null) threatText.text = LureEconomy.ThreatLabel;
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
        if (relicPanel != null && relicPanel.activeSelf) RefreshRelicPanel();
        RefreshFloorTabs();

        // 🩸 魔王HPバーのライブ更新
        if (dlHpFill != null)
        {
            var dl = DemonLord.Instance;
            float r = dl != null ? Mathf.Clamp01(dl.HPRatio) : 1f;
            if (dlHpFill.type == Image.Type.Filled) dlHpFill.fillAmount = r;
            else dlHpFill.rectTransform.sizeDelta = new Vector2(DL_HP_TRACK_W * r, dlHpFill.rectTransform.sizeDelta.y);
            if (dlHpLabel != null && dl != null) dlHpLabel.text = "魔王 Lv" + dl.Level;
            if (dlHpBar != null)
            {
                var cg = dlHpBar.GetComponent<CanvasGroup>(); if (cg == null) cg = dlHpBar.AddComponent<CanvasGroup>();
                cg.alpha = (dl != null && !dl.IsPresent) ? 0.35f : 1f; // 不在フロアでは淡色
            }
        }

        // descent演出のフェード制御（timeScaleに依存しないunscaledで動かす）
        if (descentToastTimer > 0f && descentToastCg != null)
        {
            descentToastTimer -= Time.unscaledDeltaTime;
            descentToastCg.alpha = descentToastTimer >= 0.5f ? 1f : Mathf.Clamp01(descentToastTimer / 0.5f);
            if (descentToastTimer <= 0f) descentToastCg.alpha = 0f;
        }
        if (floorFadeTimer > 0f && floorFadeCg != null)
        {
            floorFadeTimer -= Time.unscaledDeltaTime;
            floorFadeCg.alpha = Mathf.Clamp01(floorFadeTimer / FADE_DUR);
        }
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
        for (int i = 0; i < floorCountBtns.Count; i++) SetSel(floorCountBtns[i], i == selFloors);
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
        var b = Panel(bar.rectTransform, "border", BLOOD);
        Anchor(b, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        b.rectTransform.sizeDelta = new Vector2(0, 2); b.rectTransform.anchoredPosition = Vector2.zero;
    }
    private void AddTopBorder(Image bar)
    {
        var b = Panel(bar.rectTransform, "border", BLOOD);
        Anchor(b, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        b.rectTransform.sizeDelta = new Vector2(0, 2); b.rectTransform.anchoredPosition = Vector2.zero;
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
    // 眷属種族ボタン（選択ハイライト付き・コンパクト）
    private Image SpeciesButton(Graphic bar, string label, Color accent, UnityAction onClick)
    {
        var img = Panel(bar, "Species_" + label, CARD); SizeElem(img.gameObject, 54, 40); Outline(img, LINE);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
        var t = Text(img.rectTransform, label, 12, accent, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(t.rectTransform);
        return img;
    }
    private void RefreshSpecies()
    {
        for (int i = 0; i < speciesBtns.Count; i++) SetSel(speciesBtns[i], i == selSpecies);
    }

    private void ToolButtonDisabled(Graphic bar, string label)
    {
        var img = Panel(bar, "Tool_" + label, C("#141220")); SizeElem(img.gameObject, 108, 40); Outline(img, C("#252036"));
        var t = Text(img.rectTransform, label, 11.5f, FAINT, TextAlignmentOptions.Center);
        StretchFull(t.rectTransform);
    }
    // 主要ボタン（生成/侵略）。red=trueで血の赤ボタン、既定は灰ボタン。スプライト未割当ならフラット色。
    private Button PrimaryButton(Graphic parent, string label, Color bg, Color fg, UnityAction onClick, bool red = false)
    {
        var img = Panel(parent, "Primary_" + label, bg);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
        var cb = btn.colors; cb.highlightedColor = Color.Lerp(bg, Color.white, 0.12f); cb.pressedColor = Color.Lerp(bg, Color.black, 0.12f);
        cb.disabledColor = Color.Lerp(bg, Color.gray, 0.5f); btn.colors = cb;
        SkinButton(btn, img, red); // 🩸 Bloodlinesボタンへ（割当済のときだけ）
        var t = Text(img.rectTransform, label, 14.5f, fg, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(t.rectTransform);
        return btn;
    }

    // 🩸 9スライス枠スプライトを適用（未割当なら何もしない＝フラット色のまま）
    private void ApplyFrame(Image img, Sprite s, Color tint)
    {
        if (img == null || s == null) return;
        img.sprite = s; img.type = Image.Type.Sliced; img.color = tint;
        var o = img.GetComponent<Outline>(); if (o != null) o.enabled = false; // スプライト枠を使うのでOutlineは無効化
    }

    // 🩸 パネルをBloodlinesの装飾フレームでスキン（不透明の暗い下地＋フレーム重ね）。
    //     フレームは最背面の子として敷くので、以降に追加される中身は枠の上に描かれる。
    private void SkinPanel(Image panel)
    {
        if (panel == null || skinFrame == null) return;
        panel.color = HUD_BG; // 不透明の暗い下地（中央が透ける枠でも背景が黒に）
        var o = panel.GetComponent<Outline>(); if (o != null) o.enabled = false;
        var frame = Panel(panel.rectTransform, "Frame", Color.white);
        StretchFull(frame.rectTransform);
        frame.sprite = skinFrame; frame.type = Image.Type.Sliced; frame.raycastTarget = false;
        frame.rectTransform.SetAsFirstSibling(); // 中身より背面へ
    }

    // 🩸 BloodlinesボタンスプライトをSpriteSwapで適用（未割当ならフラット色のまま）
    private void SkinButton(Button btn, Image img, bool red)
    {
        Sprite def = red ? btnRed : btnGray;
        if (def == null || img == null) return;
        img.sprite = def; img.type = Image.Type.Sliced; img.color = Color.white;
        var o = img.GetComponent<Outline>(); if (o != null) o.enabled = false;
        btn.transition = Selectable.Transition.SpriteSwap;
        var ss = btn.spriteState;
        ss.highlightedSprite = (red ? btnRedHover : btnGrayHover) ?? def;
        ss.pressedSprite = (red ? btnRedPressed : btnGrayPressed) ?? def;
        ss.selectedSprite = (red ? btnRedHover : btnGrayHover) ?? def;
        ss.disabledSprite = (red ? btnRedDisabled : btnGrayDisabled) ?? def;
        btn.spriteState = ss;
    }
    private void StretchFull(RectTransform rt)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
    private void StretchOffset(RectTransform rt, float l, float t, float r, float b)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t); }
}
