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
public partial class GameUIManager : MonoBehaviour
{
    // 参照
    private DungeonGenerator generator;
    private DungeonResourceManager res;
    private DungeonTurnManager turn;
    private GridInputHandler input;
    private DungeonFeatureManager featureMgr;



    // 魔王HPバー（上部HUD）
    private Image dlHpFill; private TextMeshProUGUI dlHpLabel; private GameObject dlHpBar;
    private const float DL_HP_TRACK_W = 118f;

    // ライブ更新するUI要素
    private TextMeshProUGUI dpText, fameText, matText, turnText, phaseText, costText, threatText, slotText, worldText;
    private Image phasePill;
    private Button generateBtn, invadeBtn;
    private GameObject genPanel;
    private GameObject gameOverPanel;

    // 魔王パネル
    private GameObject demonPanel;
    private TextMeshProUGUI dlLevelText, dlBpText, dlRaceText;
    private readonly TextMeshProUGUI[] statRankTexts = new TextMeshProUGUI[5];
    private readonly TextMeshProUGUI[] statEffectTexts = new TextMeshProUGUI[5]; // 📊 各ステの効果説明
    private readonly Button[] statPlusBtns = new Button[5];
    private readonly List<(Button btn, DemonLord.Race race)> evolveBtns = new List<(Button, DemonLord.Race)>();
    private RectTransform dlEquipRow, dlEvolveRow; // ⚔️装備行 / 🧬進化分岐

    // 感情ツリーパネル
    private GameObject emotionPanel;
    private RectTransform emotionNodeContainer; // 🌟 感情ツリー（全画面・ルート×段のツリー＋複合）
    private readonly TextMeshProUGUI[] emoRouteHeads = new TextMeshProUGUI[4]; // 所持感情は毎フレーム更新（再構築せずに）

    // 🖱️ 中身を作り直すパネルは『表示内容が変わったときだけ』再構築する。
    //    毎フレーム作り直すと押下中にボタンが破棄され、クリックが成立しない。
    private string dlSig, emoSig;

    // 遺物パネル
    private GameObject relicPanel;
    private TextMeshProUGUI relicSlotText;
    private readonly List<(Image card, TextMeshProUGUI label, int idx)> relicCards = new List<(Image, TextMeshProUGUI, int)>();

    // 眷属種族セレクタ（下部バー・旧）
    private int selSpecies = 0;
    private readonly List<Image> speciesBtns = new List<Image>();

    // 🧟 配下図鑑（下部バーの『図鑑』ボタン→パネル。MinionCatalog16種を家系→役割→個体で選ぶ）
    private GameObject minionPanel;
    private RectTransform minionListContainer;
    private TextMeshProUGUI minionBarLabel;
    private int codexFamilyTab = 0;
    private readonly List<Image> codexTabBtns = new List<Image>();
    // 🛡️ 部隊編成トレイ（図鑑下部）
    private RectTransform squadSlotContainer;
    private TextMeshProUGUI squadInfoText;
    // 🎯 隊員配置ストリップ（下部バー上・『部隊』ツールで隊員を選んで個別配置）
    private GameObject squadStrip;
    // 🪤 罠の種類ストリップ（『罠』ツールで種類を選ぶ）
    private GameObject trapStrip;
    private GameObject totemStrip;
    private const float BossStripW = 1200f;   // 👑 ボス任命ストリップの見た目の幅（中身は横スクロール）
    private RectTransform bossStripContent;
    private TextMeshProUGUI bossStripLabel;
    private TextMeshProUGUI domainSummaryText; // 🏛️ 領域パネルの名声サマリ
    private TextMeshProUGUI spaceEffectText;   // 🏔️ 選択中の空間タイプの効果
    // 🗺️ 地上（4X）パネル
    private GameObject surfacePanel;
    private RectTransform kinListContainer, regionListContainer;
    private RectTransform legionContainer; private float legionW;   // ⚔️ 軍団タブ（U-2）
    private int selectedLegionId = -1;                              // 一覧で選んでいる軍団
    private TextMeshProUGUI surfaceSummaryText, surfaceRivalText, surfaceSettleText;
    private float kinListW, regionListW;     // スクロール内の実効幅（Contentは横ストレッチなのでrect.widthは使えない）
    private int selectedKinId = -1;          // 進軍/編成の対象になっている眷属（個体ID）
    private RectTransform hexMapRoot;        // ⬡ ヘクス盤の親
    // ⬡ 選択中のヘクス。⚠ 既定を 0 にしていたら **id0＝盤の左上の隅（未発見）** が選ばれ、
    //    地上に入るたびに何も無いところを映していた。-1 にして入場時に迷宮のタイルへ寄せる。
    private int selectedRegionId = -1;
    private bool surfaceModeOn;              // 🌍 地上モード中か
    private HexMapPanZoom mapPanZoom;        // 🖱️ 盤のパン/ズーム
    // 迷宮側のカメラ（地上モードのあいだ enabled=false にするだけ＝状態は保つ）
    private readonly List<Camera> foldedCameras = new List<Camera>();
    private GameObject bottomBar;            // 下部ツールバー（地上では隠す）
    private int surfaceTab;                  // 0=盤 / 1=地上ツリー
    // 🗂️ Civ式メニュー：-1＝何も開いていない（既定）／0領域 1勢力 2眷属 3ツリー
    private int surfaceMenuTab = -1;
    private readonly List<Image> surfaceMenuBtns = new List<Image>();
    private Image surfaceWindow, surfaceBanner;
    private RectTransform bannerActions;     // ⚔️ 選択タイルへの操作ボタン（進軍/駐留/拠点）
    private string surfaceActionMsg = "";    // 直前の操作の結果（帯に出す）
    private int selectedScoutId = -1;        // 🔭 選択中の斥候
    private TextMeshProUGUI surfaceWindowTitle, surfaceBannerText;
    private RectTransform statusContainer; private float statusW;
    private RectTransform eraContainer; private float eraW;
    private RectTransform victoryContainer; private float victoryW;
    private RectTransform diploContainer; private float diploW;
    private RectTransform storyContainer; private float storyW;
    private SurfaceView surfaceView;              // 🌍 ワールド空間の盤（W2）
    private readonly List<Image> surfaceTabBtns = new List<Image>();
    private RectTransform surfaceTreeRoot; private float surfaceTreeW;
    private RectTransform policyContainer; private float policyW;   // 🏛️ 政体と政策
    private RectTransform attrContainer; private float attrW;       // 🎖️ 属性ツリー
    private readonly List<GameObject> boardOnlyLabels = new List<GameObject>();   // 盤タブでだけ出す見出し
    private readonly Dictionary<int, int> nameRolls = new Dictionary<int, int>(); // 個体ID→真名の引き直し回数
    // 👑 ボス任命ストリップ（『ボス』ツールで召喚個体から任命する個体を選ぶ）
    private GameObject bossStrip;
    // 👾 特殊エネミー種類ストリップ（『特殊敵』ツールで6種から選ぶ）
    private GameObject specialStrip;

    // 🔬 研究ツリーパネル
    private GameObject researchPanel;
    private RectTransform researchNodeContainer;
    private TextMeshProUGUI researchRpText;

    // 🗺️ 階層拡張トラック
    private GameObject expandPanel;
    private RectTransform expandRowsContainer;

    // 📐 全画面パネル（図鑑/研究）の寸法＋各スクロール内容幅（rect未確定時のフォールバックにも使う）
    private const float FS_W = 1820f, FS_H = 1020f;
    private float codexContentW = 1600f, researchContentW = 1760f;

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

    // 📖 腹心の報告（ターン頭の物語ガイド）
    private GameObject guidePanel;
    private RectTransform guideBody, guideFooter;
    private const float GUIDE_W = 880f;
    // 🔔 通知（Phase A）。迷宮でも地上でも出したいのでツールチップCanvas(order200)に置く。
    private RectTransform toastRoot;      // 右上に積むトースト
    private string toastSig = "";
    private GameObject logPanel; private RectTransform logBody; private float logW;
    private GameObject savePanel; private RectTransform saveBody;   // 💾 セーブ/ロード
    private const float TOAST_W = 380f;
    private readonly List<Image> speedBtns = new List<Image>();   // ⏩ 戦闘速度
    // 📯 魔王の号令（Phase D）。戦闘中だけ画面下中央に出す。
    private GameObject commandBar;
    private readonly List<Image> cmdBtns = new List<Image>();
    private readonly List<TextMeshProUGUI> cmdCdTexts = new List<TextMeshProUGUI>();
    private TextMeshProUGUI dangerText;   // 侵入中の人数・最強レベルなど

    // 🔦 発見（S4）。迷宮でも地上でも出したいのでツールチップCanvas(order200)に置く。
    private GameObject discoveryPanel; private RectTransform discoveryBody;
    private const float DISC_W = 760f;

    // 🎬 タイトル画面（0=タイトル 1=世界設定 2=遊び方）
    [Header("Title")]
    [Tooltip("起動時にタイトル画面を出す（切ると従来どおり即ゲーム開始）")]
    [SerializeField] private bool showTitleOnStart = true;
    private GameObject titleRoot;
    private readonly GameObject[] titlePages = new GameObject[5];   // 0タイトル 1世界設定 2遊び方 3続きから 4戦績
    private readonly List<Image> tTypeBtns = new List<Image>();
    private readonly List<Image> tSpaceBtns = new List<Image>();
    private readonly List<Image> tChestBtns = new List<Image>();
    private readonly List<Image> tFloorBtns = new List<Image>();
    private readonly List<Image> tWorldBtns = new List<Image>();
    private readonly List<Image> tDiffBtns = new List<Image>();      // ⚖️ 難易度（F-22）
    private TextMeshProUGUI titleDiffText, titleDailyText;
    private Button titleDailyBtn;
    private TextMeshProUGUI titleBudgetText, titleSeedText, titleSpaceEffText, titleNoteText;
    private Button titleStartBtn;

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

    private void Awake()
    {
        // ⚠ Awake は**全オブジェクトの Start より前**に走る。ここで「タイトル待ち」を立てておかないと
        //    DungeonGenerator.Start が先に迷宮を作ってしまい、設定した内容で生成できない。
        GameSetup.ResetForNewSession();
        GameSetup.WaitForTitle = showTitleOnStart;
    }

    private void Start()
    {
        LoadSkin();   // 🩸 UIを組む前にスキンを揃える（組んだ後だと当たらない）
        generator = Object.FindFirstObjectByType<DungeonGenerator>();
        res = Object.FindFirstObjectByType<DungeonResourceManager>();
        turn = Object.FindFirstObjectByType<DungeonTurnManager>();
        input = Object.FindFirstObjectByType<GridInputHandler>();
        featureMgr = Object.FindFirstObjectByType<DungeonFeatureManager>();
        floorMgr = Object.FindFirstObjectByType<DungeonFloorManager>();

        uiFont = FindUIFont();
        FloatText.Font = uiFont;      // 💢 ダメージ数字も同じフォントで（既定フォントは日本語を持たない）
        ConfigureKit();               // 🧰 UIKit にフォント/スキン/パレットを渡す（**組む前**に）
        HideLegacyCanvas();
        BuildUI();
        RefreshCost();
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
        // 🗂️ Canvasを3枚に分ける。
        //    迷宮UI(100) ／ 地上UI(110) ／ ツールチップ(200)。
        //    地上モードでは**迷宮Canvasごと enabled=false** にする。1枚ずつ畳む方式だと、
        //    あとから開くパネル（生成パネルなど）を取りこぼして盤の上に残ってしまう（実測で発生）。
        var root = MakeCanvas("GameUICanvas", 100);
        dungeonCanvas = root.GetComponent<Canvas>();
        var surfaceRoot = MakeCanvas("SurfaceUICanvas", 110);
        var topRoot = MakeCanvas("TooltipCanvas", 200);

        BuildTopBar(root);
        BuildFloorTabs(root);
        // 🎬 迷宮生成パネルは**もう出さない**。生成の設定（タイプ/空間/宝箱/階層/地上の広さ）は
        //    タイトルの『世界設定』で開始前に決める形にしたので、ゲーム中に作り直す口は塞ぐ。
        //    ※ BuildGenPanel 自体は残してある（デバッグで作り直したくなったときのため）。
        BuildDemonPanel(root);
        BuildEmotionPanel(root);
        BuildRelicPanel(root);
        BuildResearchPanel(root);
        BuildExpandPanel(root);
        BuildSurfacePanel(surfaceRoot);
        BuildMinionCodex(root);
        BuildBottomBar(root);
        BuildSquadStrip(root);
        BuildBossStrip(root);
        BuildSpecialStrip(root);
        BuildTrapStrip(root);
        BuildTotemStrip(root);
        BuildDescentFX(root);
        BuildTooltip(topRoot);   // 💬 ツール説明（迷宮でも地上でも出したいので独立したCanvasへ）
        BuildDiscoveryPanel(topRoot);   // 🔦 発見（歩いた先の出来事）
        BuildCommandBar(root);          // 📯 魔王の号令（戦闘中の手）
        BuildToasts(topRoot);           // 🔔 通知トースト（迷宮でも地上でも出す）
        BuildLogPanel(topRoot);         // 📜 ログ（遡れる）
        BuildSavePanel(topRoot);        // 💾 セーブ / ロード
        BuildSettingsPanel();           // ⚙️ 設定（音量・表示）※専用Canvas
        BuildGameOverOverlay(root);
        BuildGuidePanel(root);   // 📖 腹心の報告
        BuildTitleScreen();      // 🎬 タイトル（最前面・order 300）
    }
}
