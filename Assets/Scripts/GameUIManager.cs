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
    private TextMeshProUGUI surfaceSummaryText, surfaceRivalText, surfaceSettleText;
    private float kinListW, regionListW;     // スクロール内の実効幅（Contentは横ストレッチなのでrect.widthは使えない）
    private int selectedKinId = -1;          // 進軍/編成の対象になっている眷属（個体ID）
    private RectTransform hexMapRoot;        // ⬡ ヘクス盤の親
    private int selectedRegionId = 0;        // ⬡ 選択中のヘクス
    private bool surfaceModeOn;              // 🌍 地上モード中か
    private HexMapPanZoom mapPanZoom;        // 🖱️ 盤のパン/ズーム
    // 迷宮側のカメラ（地上モードのあいだ enabled=false にするだけ＝状態は保つ）
    private readonly List<Camera> foldedCameras = new List<Camera>();
    private GameObject bottomBar;            // 下部ツールバー（地上では隠す）
    private int surfaceTab;                  // 0=盤 / 1=地上ツリー
    private Image surfaceRightBg, surfaceKinBg, surfaceTreeBg;   // 盤の上に敷くUIの板
    private SurfaceView surfaceView;              // 🌍 ワールド空間の盤（W2）
    private readonly List<Image> surfaceTabBtns = new List<Image>();
    private RectTransform surfaceTreeRoot; private float surfaceTreeW;
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
        // ・ CreateFontAsset(Font) はOS動的フォントだとnullになるため、
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
                    Debug.Log($"🈶『UIフォント』システムフォント『{name}』から動的TMPフォントを生成");
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
        BuildSurfacePanel(root);
        BuildMinionCodex(root);
        BuildBottomBar(root);
        BuildSquadStrip(root);
        BuildBossStrip(root);
        BuildSpecialStrip(root);
        BuildTrapStrip(root);
        BuildTotemStrip(root);
        BuildDescentFX(root);
        BuildTooltip(root);   // 💬 ツール説明（最前面に出す）
        BuildGameOverOverlay(root);
    }

    // ---------- 魔王パネル（成長/進化） ----------
    private void BuildDemonPanel(RectTransform root)
    {
        var panel = Panel(root, "DemonPanel", PANEL);
        demonPanel = panel.gameObject;
        Anchor(panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        panel.rectTransform.sizeDelta = new Vector2(520, 560);
        panel.rectTransform.anchoredPosition = new Vector2(16, -72);
        Outline(panel, LINE2); SkinPanel(panel);

        float pad = 16f, w = 520 - pad * 2;
        var eyebrow = Text(panel, "魔王の成長（BPでステ強化 → 条件を満たすと種族進化）", 11, GOLD, TextAlignmentOptions.Left, FontStyles.Bold); Place(eyebrow.rectTransform, pad, 12, w, 16);
        dlLevelText = Text(panel, "Lv 1", 18, TEXT, TextAlignmentOptions.Left, FontStyles.Bold); Place(dlLevelText.rectTransform, pad, 30, 140, 24);
        dlBpText = Text(panel, "BP 10", 14, VIOLET, TextAlignmentOptions.Right, FontStyles.Bold); Place(dlBpText.rectTransform, pad + w - 130, 33, 130, 20);
        dlRaceText = Text(panel, "種族: 人種", 12.5f, MUTED, TextAlignmentOptions.Left); Place(dlRaceText.rectTransform, pad, 58, w, 18);

        // 📊 ステータス（各ランクの"意味"を右に表示）
        var sl = Text(panel, "ステータス（BPで強化・S=最大）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold); Place(sl.rectTransform, pad, 82, w, 16);
        for (int i = 0; i < 5; i++)
        {
            int idx = i; float y = 102 + i * 30;
            var nm = Text(panel, DemonLord.StatNames[i], 13, TEXT, TextAlignmentOptions.Left); Place(nm.rectTransform, pad, y, 60, 22);
            var rk = Text(panel, "E", 15, GOLD, TextAlignmentOptions.Center, FontStyles.Bold); Place(rk.rectTransform, pad + 60, y, 30, 22); statRankTexts[i] = rk;
            var eff = Text(panel, "", 10.5f, MUTED, TextAlignmentOptions.Left); Place(eff.rectTransform, pad + 96, y + 3, w - 160, 18);
            statEffectTexts[i] = eff;
            var plus = PrimaryButton(panel, "＋", GOLD, C("#231704"), () => { DemonLord.Instance?.TrySpendBPOnStat(idx); RefreshDemonPanel(); });
            Place((RectTransform)plus.transform, pad + w - 48, y, 48, 24); statPlusBtns[i] = plus;
        }

        // ⚔️🛡️ 魔王の装備（錬成ランクで割引・上限UP）
        var eq = Text(panel, "魔王の武具（錬成ランクで鍛造が安く・上限が上がる）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(eq.rectTransform, pad, 256, w, 16);
        dlEquipRow = NewRect("DLEquip", panel.rectTransform);
        Place(dlEquipRow, pad, 274, w, 62);

        // 🧬 進化（現在の種族からの分岐のみ表示）
        var el = Text(panel, "種族進化（分岐・3段階）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold); Place(el.rectTransform, pad, 344, w, 16);
        dlEvolveRow = NewRect("DLEvolve", panel.rectTransform);
        Place(dlEvolveRow, pad, 362, w, 180);

        RefreshDemonPanel();
        demonPanel.SetActive(false);
    }

    // 魔王パネルの『見た目が変わる条件』だけを拾った署名。変化した時だけ作り直す。
    private string DemonPanelSig()
    {
        var dl = DemonLord.Instance; if (dl == null) return "";
        var sb = new System.Text.StringBuilder();
        sb.Append(dl.Level).Append('/').Append(dl.BP).Append('/').Append((int)dl.CurrentRace)
          .Append('/').Append(dl.WeaponGrade).Append('/').Append(dl.ArmorGrade).Append('/').Append((int)dl.WeaponType)
          .Append('/').Append(dl.ForgeGradeCap);
        for (int i = 0; i < 5; i++) sb.Append('/').Append(dl.GetStatRank(i));
        foreach (var r in DemonLordRaceTree.ChildrenOf(dl.CurrentRace)) sb.Append(dl.IsRaceAvailable(r) ? '1' : '0');
        return sb.ToString();
    }

    // 感情ツリーの署名（解禁状態＋各ノードの購入可否）。所持感情そのものは RefreshEmotionPools で軽量更新する。
    private string EmotionPanelSig()
    {
        var et = EmotionTreeManager.Instance; if (et == null) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var n in et.Nodes) sb.Append(n.unlocked ? '2' : et.CanUnlock(n) ? '1' : '0');
        sb.Append('|');
        foreach (var n in et.Fusions) sb.Append(n.unlocked ? '2' : et.CanUnlock(n) ? '1' : '0');
        return sb.Append('|').Append(et.ResearchPointBonus).ToString();
    }

    private void RefreshEmotionPools()
    {
        var et = EmotionTreeManager.Instance; if (et == null) return;
        for (int r = 0; r < 4; r++)
            if (emoRouteHeads[r] != null) emoRouteHeads[r].text = EmotionRouteHeadText(r, et);
    }

    private string EmotionRouteHeadText(int r, EmotionTreeManager et)
        => "▍<color=" + EmotionTreeManager.RouteColors[r] + ">" + EmotionTreeManager.RouteNames[r] + "</color>"
         + "　<size=88%><color=#9c95b4>所持 " + et.Pool((EmotionTreeManager.Route)r) + "</color></size>";

    private void RefreshDemonPanel()
    {
        var dl = DemonLord.Instance; if (dl == null) return;
        var rd = DemonLordRaceTree.Get(dl.CurrentRace);
        if (dlLevelText != null) dlLevelText.text = "Lv " + dl.Level;
        if (dlBpText != null) dlBpText.text = "BP " + dl.BP;
        if (dlRaceText != null)
            dlRaceText.text = "種族: <color=#e3a94a>" + dl.RaceName + "</color>（第" + rd.stage + "形態）　"
                + "<color=" + MagicCatalog.ElementColor(rd.element) + ">◆" + MagicCatalog.ElementName(rd.element) + "</color>"
                + (rd.skill != MinionSkillKind.None ? "　<color=#57c3ab>◆" + MinionSkill.Name(rd.skill) + "</color>" : "");

        // 📊 各ステの効果を実数で表示（死にステを無くす）
        string[] eff = {
            "最大HP +" + (130 * dl.GetStatRank(0)),
            "攻撃力 +" + (6 * dl.GetStatRank(1)) + "・魔法階級↑",
            "研究RP +" + dl.KnowledgeRank + "/ターン・研究費 ×" + dl.ResearchCostMult.ToString("0.00"),
            "配下コスト ×" + dl.DefenderCostMult.ToString("0.00") + "・拡張 ×" + dl.DomainCostMult.ToString("0.00"),
            "鍛造費 ×" + dl.ForgeCostMult.ToString("0.00") + "・鍛造上限+" + dl.ForgeGradeBonus + "・戦利品+" + dl.RefineLootBonus,
        };
        for (int i = 0; i < 5; i++)
        {
            if (statRankTexts[i] != null) statRankTexts[i].text = dl.StatRankLabel(i);
            if (statEffectTexts[i] != null) statEffectTexts[i].text = eff[i];
            if (statPlusBtns[i] != null) statPlusBtns[i].interactable = dl.GetStatRank(i) < 5 && dl.BP > 0;
        }

        // ⚔️🛡️ 装備行を作り直す
        if (dlEquipRow != null)
        {
            for (int i = dlEquipRow.childCount - 1; i >= 0; i--) { var c = dlEquipRow.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
            BuildDLEquipSlot(EquipmentCatalog.Slot.Weapon, dl, 0f);
            BuildDLEquipSlot(EquipmentCatalog.Slot.Armor, dl, 30f);
        }

        // 🧬 進化分岐（現在の種族からの子のみ）
        if (dlEvolveRow != null)
        {
            for (int i = dlEvolveRow.childCount - 1; i >= 0; i--) { var c = dlEvolveRow.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
            evolveBtns.Clear();
            var kids = DemonLordRaceTree.ChildrenOf(dl.CurrentRace);
            if (kids.Count == 0)
            {
                var t = Text(dlEvolveRow, "<color=#ffd24a>最終形態に到達している（これ以上の進化は無い）</color>", 12, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(t.rectTransform, 2, 4, 460, 20);
            }
            else
            {
                float cw = 152f, ch = 56f;
                for (int i = 0; i < kids.Count; i++)
                {
                    var r = kids[i]; var d = DemonLordRaceTree.Get(r);
                    bool ok = dl.IsRaceAvailable(r);
                    float cx = (i % 3) * (cw + 6), cy = (i / 3) * (ch + 6);
                    var card = Panel(dlEvolveRow, "Evo_" + r, CARD);
                    Place(card.rectTransform, cx, cy, cw, ch); Outline(card, ok ? GOLD : LINE);
                    var nm = Text(card.rectTransform, d.jpName, 12.5f, ok ? TEXT : FAINT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                    Place(nm.rectTransform, 8, 4, cw - 16, 16);
                    var sub = Text(card.rectTransform,
                        "<color=" + MagicCatalog.ElementColor(d.element) + ">◆" + MagicCatalog.ElementName(d.element) + "</color>"
                        + (d.skill != MinionSkillKind.None ? " <color=#57c3ab>◆" + MinionSkill.Name(d.skill) + "</color>" : ""), 9.5f, MUTED, TextAlignmentOptions.TopLeft);
                    Place(sub.rectTransform, 8, 21, cw - 16, 14);
                    var req = Text(card.rectTransform, ok ? "<color=#5cc47c>進化できる</color>" : "<color=#9c95b4>" + DemonLordRaceTree.RequirementText(r) + "</color>", 9.5f, FAINT, TextAlignmentOptions.TopLeft);
                    Place(req.rectTransform, 8, 36, cw - 16, 14);
                    AddTooltip(card.gameObject, d.jpName + "：" + d.note);
                    if (ok)
                    {
                        var rr = r;
                        var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
                        btn.onClick.AddListener(() => { if (DemonLord.Instance != null && DemonLord.Instance.EvolveTo(rr)) RefreshDemonPanel(); });
                    }
                }
            }
        }
    }

    // ⚔️🛡️ 魔王の装備スロット1つ分（アイコン＋グレード＋鍛造＋武器種切替）
    private void BuildDLEquipSlot(EquipmentCatalog.Slot slot, DemonLord dl, float y)
    {
        bool isW = slot == EquipmentCatalog.Slot.Weapon;
        int g = isW ? dl.WeaponGrade : dl.ArmorGrade;
        Color tint = g >= 0 ? Color.Lerp(Color.white, C(EquipmentCatalog.ColorHex(g)), 0.5f) : new Color(0.6f, 0.6f, 0.66f, 1f);
        IconImg(dlEquipRow, isW ? EquipmentCatalog.WeaponTypeIcon(dl.WeaponType) : "icon_shield", 0, y, 24, tint);
        var lbl = Text(dlEquipRow, isW ? EquipmentCatalog.WeaponTypeName(dl.WeaponType) : "防具", 11, isW ? TEXT : MUTED, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(lbl.rectTransform, 28, y + 4, 44, 16);
        var chip = Panel(dlEquipRow, "dlg_" + slot, C("#0f0d16"));
        Place(chip.rectTransform, 74, y, 118, 24); Outline(chip, LINE);
        var gt = Text(chip.rectTransform, "<color=" + EquipmentCatalog.ColorHex(g) + ">" + EquipmentCatalog.Name(g) + "</color>", 11.5f, TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(gt.rectTransform);
        if (g < dl.ForgeGradeCap)
        {
            int cost = dl.NextForgeCost(slot);
            var fb = PrimaryButton(dlEquipRow, "鍛造 -" + cost, BLOOD, TEXT, () => { if (DemonLord.Instance.TryForge(slot)) RefreshDemonPanel(); }, true);
            Place((RectTransform)fb.transform, 198, y, 116, 24);
        }
        else
        {
            var mx = Text(dlEquipRow, g >= EquipmentCatalog.MaxGrade ? "<color=#ffd24a>最高位</color>" : "<color=#8cb8e6>錬成ランク/研究が必要</color>", 10, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(mx.rectTransform, 198, y + 4, 116, 18);
        }
        if (isW)
        {
            var tb = PrimaryButton(dlEquipRow, "種別→" + EquipmentCatalog.WeaponTypeName((dl.WeaponType + 1) % EquipmentCatalog.WeaponTypeCount), PANEL2, TEAL,
                () => { DemonLord.Instance.CycleWeaponType(); RefreshDemonPanel(); });
            Place((RectTransform)tb.transform, 320, y, 120, 24);
            var d = EquipmentCatalog.WType(dl.WeaponType);
            AddTooltip(tb.gameObject, d.jpName + "：" + d.note + string.Format("（攻×{0:0.00} 射程+{1:0.0}）", d.atkMult, d.rangeBonus));
        }
    }

    // ---------- 感情ツリーパネル ----------
    private void BuildEmotionPanel(RectTransform root)
    {
        var panel = Panel(root, "EmotionPanel", PANEL);
        emotionPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(FS_W, FS_H);
        panel.rectTransform.anchoredPosition = new Vector2(0, 0);
        Outline(panel, LINE2); SkinPanel(panel);

        float pad = 26f;
        var title = Text(panel, "感情ツリー（冒険者の体験で感情が貯まる／◆=Eurekaでコスト-40%／◆=2ルートの複合）", 16, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 16, FS_W - 120, 24);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => emotionPanel.SetActive(false));
        Place((RectTransform)close.transform, FS_W - pad - 32, 14, 32, 30);

        emotionNodeContainer = MakeVScroll(panel, pad, 62f, FS_W - pad * 2, FS_H - 62f - pad);

        RefreshEmotionPanel();
        emotionPanel.SetActive(false);
    }

    private void RefreshEmotionPanel()
    {
        var et = EmotionTreeManager.Instance; if (et == null || emotionNodeContainer == null) return;
        for (int i = emotionNodeContainer.childCount - 1; i >= 0; i--)
        { var c = emotionNodeContainer.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }

        float W = FS_W - 52f;
        float cellW = 236f, cellH = 74f, hGap = 40f, vGap = 14f;
        float y = 4f;

        // ── 4ルートを行として並べ、段(tier)を列にする（Civ風の横ツリー）──
        for (int r = 0; r < 4; r++)
        {
            var route = (EmotionTreeManager.Route)r;
            var head = Text(emotionNodeContainer, EmotionRouteHeadText(r, et), 15, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(head.rectTransform, 2, y, W - 4, 20);
            emoRouteHeads[r] = head;
            float rowY = y + 24f;
            for (int t = 0; t < 4; t++)
            {
                var n = et.Get(route, t); if (n == null) continue;
                float cx = t * (cellW + hGap);
                if (t > 0) LineRect(emotionNodeContainer, cx - hGap, rowY + cellH / 2f - 1f, hGap, 2f); // 段の接続線
                AddEmotionCell(n, cx, rowY, cellW, cellH, et);
            }
            y = rowY + cellH + vGap + 6f;
        }

        // ── ✨ 複合ノード ──
        var fh = Text(emotionNodeContainer, "◆ 複合（2つのルートを進めると解禁・感情は両方から半分ずつ支払う）", 15, C("#8cb8e6"), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(fh.rectTransform, 2, y, W - 4, 20); y += 26f;
        for (int i = 0; i < et.Fusions.Count; i++)
        {
            var n = et.Fusions[i];
            float cx = (i % 4) * (cellW + hGap), cy = y + (i / 4) * (cellH + vGap);
            AddEmotionCell(n, cx, cy, cellW, cellH, et);
        }
        y += ((et.Fusions.Count + 3) / 4) * (cellH + vGap) + 10f;

        // 研究連携の説明
        var link = Text(emotionNodeContainer,
            "<color=#5cc47c>研究連携</color>：各ルートの最終段は毎ターン研究点+1（現在 +" + et.ResearchPointBonus + "）／魔王研究『感情増幅』で感情+35%",
            12, MUTED, TextAlignmentOptions.TopLeft);
        Place(link.rectTransform, 2, y, W - 4, 20); y += 26f;

        emotionNodeContainer.sizeDelta = new Vector2(0f, y + 12f);
    }

    // 感情ノード1セル（通常/複合の両対応）
    private void AddEmotionCell(EmotionTreeManager.Node n, float x, float y, float w, float h, EmotionTreeManager et)
    {
        bool can = et.CanUnlock(n);
        bool prereqOK = n.isFusion ? et.FusionPrereqMet(n) : (n.tier == 0 || et.IsUnlocked(n.route, n.tier - 1));
        var cell = Panel(emotionNodeContainer, "emo_" + n.name, CARD);
        Place(cell.rectTransform, x, y, w, h);
        Outline(cell, n.unlocked ? GREEN : (can ? GOLD : LINE));

        string star = et.EurekaReady(n) && !n.unlocked ? " <color=#f5c56b>◆</color>" : "";
        var nm = Text(cell.rectTransform, (n.isFusion ? "◆" : "") + n.name + star, 13, n.unlocked ? GREEN : (prereqOK ? TEXT : FAINT), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(nm.rectTransform, 9, 5, w - 18, 17);

        int cost = et.EffectiveCost(n);
        string state;
        if (n.unlocked) state = "解禁済";
        else if (!prereqOK) state = n.isFusion
            ? "― " + EmotionTreeManager.RouteNames[(int)n.reqRouteA] + (n.reqTierA + 1) + "段 ＋ " + EmotionTreeManager.RouteNames[(int)n.reqRouteB] + (n.reqTierB + 1) + "段"
            : "― 前段が必要";
        else state = n.isFusion
            ? EmotionTreeManager.RouteNames[(int)n.reqRouteA] + "/" + EmotionTreeManager.RouteNames[(int)n.reqRouteB] + " 各" + (cost / 2)
            : "コスト " + cost + (n.eurekaHint != null ? "　<size=85%><color=#6f6889>(" + n.eurekaHint + ")</color></size>" : "");
        var st = Text(cell.rectTransform, state, 10.5f, n.unlocked ? GREEN : (can ? GOLD : MUTED), TextAlignmentOptions.TopLeft);
        Place(st.rectTransform, 9, 24, w - 18, 15);

        var ds = Text(cell.rectTransform, n.desc, 9.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(ds.rectTransform, 9, 41, w - 18, h - 44);

        if (can)
        {
            var node = n;
            var btn = cell.gameObject.AddComponent<Button>(); btn.targetGraphic = cell;
            btn.onClick.AddListener(() => { if (EmotionTreeManager.Instance.TryUnlock(node)) RefreshEmotionPanel(); });
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
            SetTxt(floorTabs[i].label, "B" + (i + 1) + "F" + (deepest ? "魔" : ""));
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
        panel.rectTransform.sizeDelta = new Vector2(FS_W, FS_H);
        panel.rectTransform.anchoredPosition = new Vector2(0, 0);
        Outline(panel, LINE2); SkinPanel(panel);

        float pad = 22f, w = FS_W - pad * 2;
        var title = Text(panel, "遺物（全体パッシブ・実績で獲得 → スロットぶんだけ装備）", 16, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 14, w - 60, 24);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => relicPanel.SetActive(false));
        Place((RectTransform)close.transform, FS_W - pad - 32, 12, 32, 30);

        relicSlotText = Text(panel, "装備スロット: ―", 12.5f, VIOLET, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(relicSlotText.rectTransform, pad, 44, w, 18);

        var rm = RelicManager.Instance;
        int count = rm != null ? rm.Catalog.Count : 0;
        float cw = (w - 3 * 12) / 4f, ch = 96f;
        for (int i = 0; i < count; i++)
        {
            int idx = i; var rel = rm.Catalog[i];
            float cx = pad + (i % 4) * (cw + 12);
            float cy = 72 + (i / 4) * (ch + 10);
            var card = Panel(panel, "Relic_" + i, CARD);
            Place(card.rectTransform, cx, cy, cw, ch); Outline(card, LINE);
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
            btn.onClick.AddListener(() => { RelicManager.Instance?.Toggle(idx); RefreshRelicPanel(); });
            var nm = Text(card.rectTransform, rel.name, 13.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm.rectTransform, 10, 8, cw - 16, 18);
            var ds = Text(card.rectTransform, rel.desc, 10.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(ds.rectTransform, 10, 28, cw - 16, 30);
            var st = Text(card.rectTransform, "", 10.5f, GREEN, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(st.rectTransform, 10, 62, cw - 16, 28);
            AddTooltip(card.gameObject, rel.name + "：" + rel.desc + "\n<color=#9c95b4>獲得条件: " + rel.howTo + "</color>");
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
            relicSlotText.text = "装備スロット(" + rm.SlotCount + "): " + string.Join(" / ", parts)
                + "　<color=#9c95b4>― 獲得 " + rm.UnlockedCount + "/" + rm.Catalog.Count
                + "　スロットは領域研究『遺物の祭壇/宝物庫』で3つまで増える</color>";
        }
        foreach (var c in relicCards)
        {
            bool got = rm.IsUnlocked(c.idx);
            bool eq = rm.IsEquipped(c.idx);
            if (c.label != null)
            {
                c.label.text = !got ? "<color=#6f6889>― 未獲得: " + rm.Catalog[c.idx].howTo + "</color>"
                             : eq ? "装備中" : "未装備";
                c.label.color = eq ? GREEN : FAINT;
            }
            if (c.card != null)
            {
                c.card.color = eq ? SEL : CARD;
                var o = c.card.GetComponent<Outline>(); if (o != null) o.effectColor = eq ? GOLD : (got ? LINE2 : LINE);
                // 未獲得は淡色
                var g = c.card.GetComponent<CanvasGroup>(); if (g == null) g = c.card.gameObject.AddComponent<CanvasGroup>();
                g.alpha = got ? 1f : 0.5f;
            }
        }
    }

    // ---------- 配下図鑑（全画面・家系タブ＋段階グループのカードグリッド／CDO2風） ----------
    private void BuildMinionCodex(RectTransform root)
    {
        var panel = Panel(root, "MinionCodex", PANEL);
        minionPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(FS_W, FS_H);
        panel.rectTransform.anchoredPosition = new Vector2(0, 0);
        Outline(panel, LINE2); SkinPanel(panel);

        float pad = 26f;
        var title = Text(panel, "配下図鑑（家系タブ→段階で選ぶ／進化の系統を一覧）", 17, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 16, FS_W - 240, 24);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => minionPanel.SetActive(false));
        Place((RectTransform)close.transform, FS_W - pad - 32, 14, 32, 30);

        // 左：家系タブ（全体/不死/獣/魔族）＋個体(装備)タブ 縦並び
        codexTabBtns.Clear();
        string[] fam = { "全体", "不死", "獣", "魔族", "個体" };
        Color[] famCol = { TEXT, GREEN, GOLD, VIOLET, C("#8cb8e6") };
        float tabX = pad, tabY0 = 66f, tabW = 128f, tabH = 46f, tabGap = 8f;
        for (int i = 0; i < fam.Length; i++)
        {
            int idx = i;
            var b = Panel(panel, "CodexTab_" + i, CARD);
            Place(b.rectTransform, tabX, tabY0 + i * (tabH + tabGap), tabW, tabH); Outline(b, LINE);
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
            btn.onClick.AddListener(() => { codexFamilyTab = idx; RefreshMinionCodex(); });
            var tt = Text(b.rectTransform, fam[i], 14, famCol[i], TextAlignmentOptions.Center, FontStyles.Bold); StretchFull(tt.rectTransform);
            codexTabBtns.Add(b);
        }

        // 右：スクロールするカードグリッド
        float contentX = tabX + tabW + 18f;
        codexContentW = FS_W - contentX - pad;
        float footerH = 116f;
        float contentH = FS_H - 66f - footerH - 10f;
        minionListContainer = MakeVScroll(panel, contentX, 66f, codexContentW, contentH);

        // 下：部隊編成トレイ（固定フッタ）
        float footTop = FS_H - footerH;
        var trayLabel = Text(panel, "部隊編成（役割を散らすほど部隊バフ↑）／＋隊で追加 → 図鑑を閉じ『部隊』ツールで個別配置", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(trayLabel.rectTransform, contentX, footTop + 8, codexContentW, 16);
        var slots = NewRect("SquadSlots", panel.rectTransform);
        Place(slots, contentX, footTop + 30, 5 * 100, 32);
        squadSlotContainer = slots;
        var clearBtn = PrimaryButton(panel, "クリア", PANEL2, TEXT, () => { featureMgr?.SquadClear(); RefreshSquadTray(); RefreshMinionCodex(); });
        Place((RectTransform)clearBtn.transform, contentX + 5 * 100 + 12, footTop + 30, 120, 32);
        squadInfoText = Text(panel, "", 12.5f, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(squadInfoText.rectTransform, contentX, footTop + 72, codexContentW, 18);

        RefreshMinionCodex();
        RefreshSquadTray();
        minionPanel.SetActive(false);
    }

    // 縦スクロール領域を作り、中身を入れる Content(RectTransform) を返す。既存UI基盤にScrollRectが無いのでここで組む。
    private RectTransform MakeVScroll(Image parent, float x, float y, float w, float h)
    {
        var view = Panel(parent, "Viewport", new Color(0f, 0f, 0f, 0.001f)); // ほぼ透明だがドラッグ受け付け
        Place(view.rectTransform, x, y, w, h);
        view.gameObject.AddComponent<RectMask2D>();
        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 28f;
        var content = NewRect("Content", view.rectTransform);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0f, 0f); content.offsetMax = new Vector2(0f, 0f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, h);
        sr.viewport = view.rectTransform;
        sr.content = content;
        return content;
    }

    // 横スクロール領域。項目数が所持数で伸びるストリップ（ボス任命など）が画面外に見切れないようにする。
    // Content は縦ストレッチにして、幅だけコードで指定する（MakeVScroll と対称）。
    private RectTransform MakeHScroll(Image parent, float x, float y, float w, float h)
    {
        var view = Panel(parent, "Viewport", new Color(0f, 0f, 0f, 0.001f));
        Place(view.rectTransform, x, y, w, h);
        view.gameObject.AddComponent<RectMask2D>();
        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = true; sr.vertical = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 40f;
        var content = NewRect("Content", view.rectTransform);
        content.anchorMin = new Vector2(0f, 0f); content.anchorMax = new Vector2(0f, 1f); content.pivot = new Vector2(0f, 0.5f);
        content.offsetMin = new Vector2(0f, 0f); content.offsetMax = new Vector2(0f, 0f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(w, 0f);
        sr.viewport = view.rectTransform;
        sr.content = content;
        return content;
    }

    // 🛡️ 編成トレイの再描画（5枠：個体名/空、クリックで抜く）＋コスト/コンプ表示
    private void RefreshSquadTray()
    {
        if (squadSlotContainer == null || featureMgr == null) return;
        for (int i = squadSlotContainer.childCount - 1; i >= 0; i--)
        {
            var c = squadSlotContainer.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        var squad = featureMgr.CurrentSquad; // 🧬 個体IDのリスト（この階の隊）
        float slotW = 108, slotH = 30;
        for (int i = 0; i < DungeonFeatureManager.SquadMaxSlots; i++)
        {
            int slot = i;
            var chip = Panel(squadSlotContainer, "Slot_" + i, CARD); Place(chip.rectTransform, i * slotW, 0, slotW - 6, slotH); Outline(chip, LINE);
            bool filled = i < squad.Count;
            var v = filled ? MinionRoster.Get(squad[i]) : null;
            string label = v != null ? MinionCatalog.Get(v.catalogIndex).jpName + " <size=76%>Lv" + v.level + "</size>" : "空";
            var col = v != null ? RoleColor(MinionCatalog.Get(v.catalogIndex).role) : FAINT;
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
            int n = squad.Count;
            var fmgr = DungeonFloorManager.Instance;
            string floorLbl = "B" + ((fmgr != null ? fmgr.CurrentFloorIndex : 0) + 1) + "F";
            squadInfoText.text = n == 0 ? "<color=#9c95b4>" + floorLbl + " の隊は空です。『個体』タブで＋隊 → 下部バー『部隊』で配置</color>"
                : string.Format("<color=#8cb8e6>{0}</color> の隊　役割{1}種　部隊バフ <color=#5cc47c>×{2:0.00}</color>　<size=88%><color=#9c95b4>（階層ごとに別の隊を編成できます）</color></size>", floorLbl, roles, comp);
        }
        RefreshSquadStrip();
    }

    // 🎯 隊員配置ストリップ（下部バー上・2段）：上=種類(隊)を選ぶ、下=その種類の個体(Lv)を選ぶ→マスクリックで個別配置。
    private void BuildSquadStrip(RectTransform root)
    {
        var panel = Panel(root, "SquadStrip", C("#0e0b16"));
        Anchor(panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        panel.rectTransform.sizeDelta = new Vector2(700, 44);
        panel.rectTransform.anchoredPosition = new Vector2(0, 66);
        Outline(panel, LINE2);
        squadStrip = panel.gameObject;
        RefreshSquadStrip();
        squadStrip.SetActive(false); // 表示は『部隊』ツールで制御（ShowStripFor）
    }

    // 👑🪤🛡️ 配置系ストリップ（部隊/ボス/罠）は選択ツールに応じて1つだけ表示する。
    private void ShowStripFor(int mode)
    {
        if (squadStrip != null) squadStrip.SetActive(mode == 11);
        if (bossStrip != null) bossStrip.SetActive(mode == 8);
        if (trapStrip != null) trapStrip.SetActive(mode == 3);
        if (totemStrip != null) totemStrip.SetActive(mode == 6);
        if (specialStrip != null) specialStrip.SetActive(mode == 9);
        if (mode == 11) RefreshSquadStrip();
        else if (mode == 8) RefreshBossStrip();
        else if (mode == 3) RefreshTrapStrip();
        else if (mode == 6) RefreshTotemStrip();
        else if (mode == 9) RefreshSpecialStrip();
    }

    private void RefreshSquadStrip()
    {
        if (squadStrip == null || featureMgr == null) return;
        for (int i = squadStrip.transform.childCount - 1; i >= 0; i--)
        {
            var c = squadStrip.transform.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        var strip = (RectTransform)squadStrip.transform;
        var squad = featureMgr.CurrentSquad; // 🧬 個体IDのリスト
        var fmgr = DungeonFloorManager.Instance;
        string floorLbl = "B" + ((fmgr != null ? fmgr.CurrentFloorIndex : 0) + 1) + "F";
        var lbl = Text(strip, floorLbl + " の隊員 →", 10.5f, C("#8cb8e6"), TextAlignmentOptions.Left, FontStyles.Bold);
        Place(lbl.rectTransform, 12, 12, 92, 15);
        if (squad.Count == 0)
        {
            var h = Text(strip, "<color=#9c95b4>図鑑の『個体』タブで『＋隊』して編成してください（隊は階層ごと）</color>", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(h.rectTransform, 108, 12, 460, 16);
            strip.sizeDelta = new Vector2(580, 44);
            return;
        }
        int sel = Mathf.Clamp(featureMgr.SquadPlaceSlot, 0, squad.Count - 1);

        // 隊員＝個体そのもの。配置済みは淡色、未配置のみ選択可。
        float bw = 128, x0 = 108;
        for (int i = 0; i < squad.Count; i++)
        {
            int slot = i; int id = squad[i];
            var v = MinionRoster.Get(id);
            var b = Panel(strip, "Member_" + i, CARD);
            Place(b.rectTransform, x0 + i * (bw + 4), 7, bw, 28); Outline(b, LINE);
            bool placed = featureMgr.IsIndividualPlaced(id);
            string nm = v != null ? MinionCatalog.Get(v.catalogIndex).jpName + " <size=76%>Lv" + v.level + "</size>" : "?";
            var col = v != null ? RoleColor(MinionCatalog.Get(v.catalogIndex).role) : FAINT;
            var tt = Text(b.rectTransform, nm, 10f, placed ? FAINT : col, TextAlignmentOptions.Center, FontStyles.Bold);
            StretchFull(tt.rectTransform);
            if (!placed)
            {
                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
                btn.onClick.AddListener(() => { featureMgr.SetSquadPlaceSlot(slot); input?.SetToolMode(11); RefreshSquadStrip(); });
                SetSel(b, i == sel);
            }
            else b.color = C("#0f0d16"); // 配置済は暗く
        }
        strip.sizeDelta = new Vector2(x0 + squad.Count * (bw + 4) + 8, 44);
    }

    // 👑 ボス任命ストリップ（『ボス』ツールで表示）：召喚した全個体から1体を選び、マスをクリックでこのフロアのボスに。
    private void BuildBossStrip(RectTransform root)
    {
        var panel = Panel(root, "BossStrip", C("#0e0b16"));
        Anchor(panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        panel.rectTransform.sizeDelta = new Vector2(BossStripW, 46);
        panel.rectTransform.anchoredPosition = new Vector2(0, 66);
        Outline(panel, LINE2);
        bossStrip = panel.gameObject;

        // 見出し（固定）＋ 個体リスト（横スクロール）。所持個体が増えても見切れないようにする。
        bossStripLabel = Text(panel, "", 11, CRIMSON, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(bossStripLabel.rectTransform, 12, 4, BossStripW - 24, 16);
        bossStripContent = MakeHScroll(panel, 8, 21, BossStripW - 16, 24);

        RefreshBossStrip();
        bossStrip.SetActive(false);
    }

    private void RefreshBossStrip()
    {
        if (bossStrip == null || featureMgr == null || bossStripContent == null) return;
        var c = bossStripContent;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }

        // 見出し＋このフロアの現ボス状態
        int bossId = featureMgr.CurrentBossIndividualId();
        string status = "未設定";
        if (bossId >= 0)
        {
            var bi = MinionRoster.Get(bossId);
            status = bi != null ? ("現ボス " + MinionCatalog.Get(bi.catalogIndex).jpName + " Lv" + bi.level) : "設定済";
        }
        var allInd = MinionRoster.All;
        SetTxt(bossStripLabel, "◆ボス任命：個体を選び→マスをクリックでこの階のボスに → <color=#9c95b4>(" + status + ")</color>"
            + "  <size=90%><color=#6f6889>所持 " + allInd.Count + "体・横にスクロールできます</color></size>");

        float bw = 130, gap = 4;
        if (allInd.Count == 0)
        {
            var hint = Text(c, "<color=#6f6889>図鑑で『召喚』して個体を作成してください</color>", 11, FAINT, TextAlignmentOptions.MidlineLeft);
            Place(hint.rectTransform, 4, 4, 360, 18);
            c.sizeDelta = new Vector2(380, 0f);
            return;
        }
        int curInd = featureMgr.SelectedIndividualId;
        int shown = 0;
        for (int i = 0; i < allInd.Count; i++)
        {
            var v = allInd[i]; int id = v.id;
            bool placed = featureMgr.IsIndividualPlaced(id);
            int inSquad = featureMgr.SquadFloorOfIndividual(id);   // 👑 隊に居る個体はボスにできない（実体は1つ）
            bool away = KinRoster.IsAwayFromDungeon(id);           // 🗺️ 地上に出ている個体もボスにできない
            bool busy = placed || inSquad >= 0 || away;
            var d = MinionCatalog.Get(v.catalogIndex);
            var b = Panel(c, "BI_" + id, CARD);
            Place(b.rectTransform, shown * (bw + gap), 1, bw, 22); Outline(b, LINE);
            string sfx = inSquad >= 0 ? " <size=80%><color=#6f6889>B" + (inSquad + 1) + "F隊</color></size>"
                       : away ? " <size=80%><color=#6f6889>地上</color></size>" : "";
            var tt = Text(b.rectTransform, d.jpName + " Lv" + v.level + sfx, 9.5f, busy ? FAINT : RoleColor(d.role), TextAlignmentOptions.Center, FontStyles.Bold);
            StretchFull(tt.rectTransform);
            if (inSquad >= 0) AddTooltip(b.gameObject, "B" + (inSquad + 1) + "F の隊に編成済み。先に隊から外すとボスに任命できます。");
            else if (away) AddTooltip(b.gameObject, "眷属またはその配下として地上に出ています。");
            if (!busy)
            {
                int cat = v.catalogIndex;
                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
                btn.onClick.AddListener(() => { featureMgr.SetSelectedMinion(cat); featureMgr.SetPlaceIndividual(id); input?.SetToolMode(8); RefreshBossStrip(); });
                SetSel(b, id == curInd);
                // 🜏 任命したら継ぐ魔神の名と加護
                AddTooltip(b.gameObject, GoetiaCatalog.TitleOf(id) + " を継ぐ ／ " + GoetiaCatalog.Blessing(GoetiaCatalog.PillarOf(id).rank));
            }
            else b.color = C("#0f0d16");
            shown++;
        }
        c.sizeDelta = new Vector2(shown * (bw + gap) + 8, 0f);
    }

    // 👾 特殊エネミー種類ストリップ（『特殊敵』ツールで表示）：6種のGDDから選んでマスに配置。
    private void BuildSpecialStrip(RectTransform root)
    {
        var panel = Panel(root, "SpecialStrip", C("#0e0b16"));
        Anchor(panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        panel.rectTransform.sizeDelta = new Vector2(760, 40);
        panel.rectTransform.anchoredPosition = new Vector2(0, 66);
        Outline(panel, LINE2);
        specialStrip = panel.gameObject;
        RefreshSpecialStrip();
        specialStrip.SetActive(false);
    }

    private void RefreshSpecialStrip()
    {
        if (specialStrip == null || featureMgr == null) return;
        for (int i = specialStrip.transform.childCount - 1; i >= 0; i--)
        {
            var c = specialStrip.transform.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        var strip = (RectTransform)specialStrip.transform;
        var lbl = Text(strip, "特殊敵の種類 →", 11, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(lbl.rectTransform, 12, 12, 100, 16);
        int sel = featureMgr.SelectedSpecialType;
        float bw = 100, x0 = 116;
        for (int k = 0; k < GddMap.SpecialCount; k++)
        {
            int kk = k;
            var b = Panel(strip, "Sp_" + k, CARD);
            Place(b.rectTransform, x0 + k * (bw + 4), 5, bw, 30); Outline(b, LINE);
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
            btn.onClick.AddListener(() => { featureMgr.SetSelectedSpecialType(kk); input?.SetToolMode(9); RefreshSpecialStrip(); });
            var tt = Text(b.rectTransform, GddMap.SpecialName(k), 10.5f, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
            StretchFull(tt.rectTransform);
            SetSel(b, k == sel);
        }
        strip.sizeDelta = new Vector2(x0 + GddMap.SpecialCount * (bw + 4) + 8, 40);
    }

    // 🪤 罠の種類ストリップ（『罠』ツールで種類を選ぶ。ロック=領域研究で未解禁）
    private void BuildTrapStrip(RectTransform root)
    {
        var panel = Panel(root, "TrapStrip", C("#0e0b16"));
        Anchor(panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        panel.rectTransform.sizeDelta = new Vector2(780, 40);
        panel.rectTransform.anchoredPosition = new Vector2(0, 150);
        Outline(panel, LINE2);
        var lbl = Text(panel, "罠の種類 →", 11, CRIMSON, TextAlignmentOptions.Left, FontStyles.Bold);
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
            // 🖼️ 罠アイコン（該当あるもののみ：通常=棘/炎=火球/出血=槍）
            string ticon = k == 0 ? "icon_trap_spikes" : k == 2 ? "icon_fireball" : k == 5 ? "icon_trap_spears" : null;
            if (ticon != null) IconImg(b.rectTransform, ticon, 5, 5, 20, unlocked ? d.color : FAINT);
            var tt = Text(b.rectTransform, d.name + (unlocked ? " <size=78%><color=#9c95b4>" + d.dpCost + "</color></size>" : " ×"), 10.5f, unlocked ? d.color : FAINT, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(tt.rectTransform, ticon != null ? 26 : 4, 0, bw - (ticon != null ? 28 : 6), 30); tt.alignment = TextAlignmentOptions.Center;
            if (unlocked)
            {
                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
                btn.onClick.AddListener(() => { featureMgr.SetSelectedTrapKind(kk); input?.SetToolMode(3); RefreshTrapStrip(); });
            }
            SetSel(b, k == sel && unlocked);
        }
        ((RectTransform)trapStrip.transform).sizeDelta = new Vector2(x0 + TrapCatalog.Count * (bw + 4) + 8, 40);
    }

    // 🗿 トーテムストリップ（『トーテム』ツールで表示）：13種から選んで配置する。
    private void BuildTotemStrip(RectTransform root)
    {
        var panel = Panel(root, "TotemStrip", C("#0e0b16"));
        Anchor(panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        panel.rectTransform.sizeDelta = new Vector2(1400, 40);
        panel.rectTransform.anchoredPosition = new Vector2(0, 66);
        Outline(panel, LINE2);
        totemStrip = panel.gameObject;
        RefreshTotemStrip();
        totemStrip.SetActive(false);
    }

    private void RefreshTotemStrip()
    {
        if (totemStrip == null || featureMgr == null) return;
        for (int i = totemStrip.transform.childCount - 1; i >= 0; i--)
        {
            var c = totemStrip.transform.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        int sel = featureMgr.SelectedTotemKind;
        float bw = 104, x0 = 6;
        for (int k = 0; k < TotemCatalog.Count; k++)
        {
            int kk = k; var d = TotemCatalog.Get(k);
            var col = C(d.colorHex);
            bool unlocked = TotemCatalog.IsUnlocked(k);
            var b = Panel(totemStrip.transform, "Totem_" + k, CARD);
            Place(b.rectTransform, x0 + k * (bw + 4), 5, bw, 30); Outline(b, LINE);
            IconImg(b.rectTransform, d.icon, 5, 6, 18, unlocked ? col : FAINT);
            var tt = Text(b.rectTransform, d.jpName + (unlocked ? "\n<size=76%><color=#9c95b4>" + d.dpCost + "DP</color></size>" : "\n<size=76%>― 未解禁</size>"),
                9.5f, unlocked ? col : FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(tt.rectTransform, 26, 0, bw - 28, 30);
            AddTooltip(b.gameObject, d.jpName + "：" + d.desc + "（半径" + d.radius + "・重ねがけ2まで）"
                + (unlocked ? "" : "\n<color=#e05a5a>領域研究が必要</color>"));
            if (unlocked)
            {
                var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
                btn.onClick.AddListener(() => { featureMgr.SetSelectedTotemKind(kk); input?.SetToolMode(6); RefreshTotemStrip(); });
            }
            SetSel(b, k == sel && unlocked);
        }
        ((RectTransform)totemStrip.transform).sizeDelta = new Vector2(x0 + TotemCatalog.Count * (bw + 4) + 8, 40);
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

    // ランク色（S/A=高位=金赤、B/C=青、D以下=淡色）。リッチテキスト用の16進。
    private static string RankHex(MinionCatalog.Rank r)
    {
        switch (r)
        {
            case MinionCatalog.Rank.S: return "#ffd24a";
            case MinionCatalog.Rank.A: return "#e88a4a";
            case MinionCatalog.Rank.B: return "#8cb8e6";
            case MinionCatalog.Rank.C: return "#79a9d6";
            default: return "#9c95b4"; // D/E/F/G
        }
    }

    private void RefreshMinionCodex()
    {
        if (minionListContainer == null) return;
        for (int i = 0; i < codexTabBtns.Count; i++) SetSel(codexTabBtns[i], i == codexFamilyTab);
        // 既存を破棄して作り直し（Destroyは遅延実行なので、まず非表示化して同フレームの重なりを防ぐ）
        for (int i = minionListContainer.childCount - 1; i >= 0; i--)
        {
            var c = minionListContainer.GetChild(i).gameObject; c.SetActive(false); Destroy(c);
        }
        float W = codexContentW; if (W < 60f) W = 1400f;
        int selIdx = featureMgr != null ? featureMgr.SelectedMinionIndex : -1;

        // 🧬 個体タブ：召喚した個体ごとに武器/防具スロットを装備（PE）
        if (codexFamilyTab == 4) { RefreshCodexIndividuals(W); return; }

        // 表示する家系（全体=3家系スタック、個別=1家系）
        var fams = new List<ZombieAI.Species>();
        if (codexFamilyTab <= 0) { fams.Add(ZombieAI.Species.Undead); fams.Add(ZombieAI.Species.Beast); fams.Add(ZombieAI.Species.Demonkin); }
        else fams.Add((ZombieAI.Species)(codexFamilyTab - 1));
        bool showFamHead = fams.Count > 1;

        string[] stageNames = { "基本", "進化Ⅰ", "上位Ⅱ", "最上位Ⅲ" };
        string[] famNames = { "不死", "獣", "魔族" };
        Color[] famCols = { GREEN, GOLD, VIOLET };
        float cardW = 224f, cardH = 126f, gap = 12f;
        int cols = Mathf.Max(1, (int)((W + gap) / (cardW + gap)));
        float y = 4f;

        foreach (var famv in fams)
        {
            if (showFamHead)
            {
                int fi = (int)famv;
                var fh = Text(minionListContainer, "◆ " + famNames[fi] + " 系統", 15, famCols[fi], TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(fh.rectTransform, 2, y, W - 4, 22); y += 30f;
            }
            for (int stage = 0; stage < 4; stage++)
            {
                var idxs = new List<int>();
                for (int k = 0; k < MinionCatalog.Count; k++)
                {
                    var d = MinionCatalog.Get(k);
                    if (d.family != famv || MinionEvolution.Depth(k) != stage) continue;
                    idxs.Add(k);
                }
                if (idxs.Count == 0) continue;
                var sh = Text(minionListContainer, stageNames[stage] + "  <size=80%><color=#6f6889>(" + idxs.Count + ")</color></size>", 12.5f, MUTED, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(sh.rectTransform, 6, y, W - 8, 18); y += 24f;
                for (int n = 0; n < idxs.Count; n++)
                {
                    int col = n % cols, rr = n / cols;
                    AddCodexCard(minionListContainer, idxs[n], col * (cardW + gap), y + rr * (cardH + gap), cardW, cardH, selIdx);
                }
                int rows = (idxs.Count + cols - 1) / cols;
                y += rows * (cardH + gap) + 8f;
            }
            y += 12f;
        }
        minionListContainer.sizeDelta = new Vector2(0f, y + 12f);
    }

    // 図鑑カード1枚（種類＝MinionCatalog index）。名前/役割/ランク/ステータス/個体情報＋＋隊/召喚/進化。
    private void AddCodexCard(RectTransform parent, int kk, float x, float y, float w, float h, int selIdx)
    {
        var d = MinionCatalog.Get(kk);
        bool unlocked = MinionEvolution.IsUnlocked(kk);
        var card = Panel(parent, "Card_" + d.id, CARD);
        Place(card.rectTransform, x, y, w, h); Outline(card, LINE);
        var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
        btn.onClick.AddListener(() => { if (unlocked) { featureMgr?.SetSelectedMinion(kk); UpdateMinionBarLabel(); } RefreshMinionCodex(); });

        var nm = Text(card.rectTransform, d.jpName, 14, unlocked ? TEXT : FAINT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(nm.rectTransform, 10, 7, w - 20, 18);
        var role = Text(card.rectTransform, "[" + MinionCatalog.RoleName(d.role) + "] <color=" + RankHex(d.rank) + ">" + MinionCatalog.RankName(d.rank) + "</color>", 11, RoleColor(d.role), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(role.rectTransform, 10, 27, w - 20, 15);
        var stat = Text(card.rectTransform, string.Format("T{0}  HP×{1:0.00} ATK×{2:0.00} SPD×{3:0.00}", d.tierCP, d.hpMult, d.atkMult, d.spdMult), 10, MUTED, TextAlignmentOptions.TopLeft);
        Place(stat.rectTransform, 10, 45, w - 20, 14);
        // 💫 スキル／🔮 魔法（術者のみ）
        string skl = MinionSkill.Label(kk);
        MagicCatalog.Spell msp;
        if (MagicCatalog.TryPickMinionSpell(kk, out msp))
            skl += "<color=" + msp.colorHex + ">◆" + msp.jpName + "</color>";
        else if (d.style == CharacterVisual.AttackStyle.Cast)
            skl += "<color=#6f6889>・魔法未解禁</color>";
        var sk = Text(card.rectTransform, skl, 9.5f, TEXT, TextAlignmentOptions.TopLeft);
        Place(sk.rectTransform, 10, 59, w - 20, 14);
        var note = Text(card.rectTransform, "", 9.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(note.rectTransform, 10, 74, w - 20, 16);

        if (unlocked)
        {
            // 🧬 個体情報（数＋最高Lv）
            int cnt = MinionRoster.CountOfType(kk); int top = MinionRoster.TopLevelOfType(kk);
            note.text = cnt > 0
                ? "<color=#8cb8e6>個体 " + cnt + " 体 ・ 最高Lv " + top + "</color>"
                : "<color=#6f6889>未召喚（召喚で個体を作成）</color>";
            // ・ 隊の編成は『個体』タブで個体ごとに行う（同じ個体を二重に置けないようにするため）
            // 召喚（DPで個体を1体追加）
            int scost = MinionRoster.SummonCost(kk);
            var sumBtn = PrimaryButton(card, "召喚 -" + scost, BLOOD, TEXT, () => { if (MinionRoster.TrySummon(kk) != null) { RefreshMinionCodex(); RefreshSquadStrip(); } }, true);
            Place((RectTransform)sumBtn.transform, w - 116, h - 28, 106, 22);
        }
        else
        {
            string pn = MinionEvolution.PrereqName(kk);
            if (MinionEvolution.CanEvolve(kk))
                SetTxt(note, "<color=#e3a94a>◆ " + pn + " から進化可 ・ " + MinionEvolution.EvolveCost(kk) + "DP</color>");
            else if (MinionEvolution.TierResearchNeeded(kk))
                SetTxt(note, "<color=#8cb8e6>・ 研究で開放（" + MinionEvolution.TierResearchName(kk) + "）</color>");
            else
                SetTxt(note, "<color=#9c95b4>― " + pn + " の解禁が必要</color>");
            if (MinionEvolution.CanEvolve(kk))
            {
                var evoBtn = PrimaryButton(card, "進化", BLOOD, TEXT, () => { if (MinionEvolution.TryEvolve(kk)) RefreshMinionCodex(); }, true);
                Place((RectTransform)evoBtn.transform, w - 62, h - 28, 52, 22);
            }
        }
        SetSel(card, kk == selIdx);
    }

    private void UpdateMinionBarLabel()
    {
        if (minionBarLabel == null || featureMgr == null) return;
        var d = featureMgr.SelectedMinion;
        SetTxt(minionBarLabel, d.jpName + " <size=78%><color=#9c95b4>[" + MinionCatalog.RoleName(d.role) + "/T" + d.tierCP + "]</color></size>");
    }

    // 🧬⚔️🛡️ 個体タブ：召喚した個体ごとに武器/防具スロットを鍛造・装着（PE）。
    private void RefreshCodexIndividuals(float W)
    {
        var all = MinionRoster.All;
        float y = 4f;
        if (all.Count == 0)
        {
            var h = Text(minionListContainer, "<color=#9c95b4>図鑑で種類を『召喚』すると、ここで個体ごとに武器/防具を装備できます。</color>", 13, MUTED, TextAlignmentOptions.TopLeft);
            Place(h.rectTransform, 6, y, W - 12, 24);
            minionListContainer.sizeDelta = new Vector2(0f, 60f);
            return;
        }
        var fmgr = DungeonFloorManager.Instance;
        string floorLbl = "B" + ((fmgr != null ? fmgr.CurrentFloorIndex : 0) + 1) + "F";
        int squadN = featureMgr != null ? featureMgr.CurrentSquad.Count : 0;
        var head = Text(minionListContainer,
            "◆ 個体の管理　<color=#8cb8e6>＋隊＝" + floorLbl + " の隊に編成(" + squadN + "/" + DungeonFeatureManager.SquadMaxSlots + ")</color>"
            + "　<color=#e3a94a>進化＝Lv/装備を保ったまま上位形態へ</color>　<color=#9c95b4>装備＝DPで1段ずつ鍛造</color>",
            14, C("#8cb8e6"), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(head.rectTransform, 2, y, W - 4, 22); y += 30f;
        float rowH = 104f;
        for (int i = 0; i < all.Count; i++)
        {
            AddIndividualEquipRow(all[i].id, y, W, rowH);
            y += rowH + 8f;
        }
        minionListContainer.sizeDelta = new Vector2(0f, y + 12f);
    }

    private void AddIndividualEquipRow(int id, float y, float W, float h)
    {
        var v = MinionRoster.Get(id); if (v == null) return;
        var d = MinionCatalog.Get(v.catalogIndex);
        bool placed = featureMgr != null && featureMgr.IsIndividualPlaced(id);
        var row = Panel(minionListContainer, "IndRow_" + id, CARD);
        Place(row.rectTransform, 0, y, W, h); Outline(row, LINE);

        // 左：種類名 / Lv / 合計効果 / 配置状態
        var nm = Text(row.rectTransform, d.jpName + " <size=76%><color=#9c95b4>#" + id + "</color></size>", 14, RoleColor(d.role), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(nm.rectTransform, 12, 8, 236, 20);
        float totalAtk = MinionRoster.EquipAtkMult(id) * MinionRoster.TypeAtkMult(id);
        string expTxt = v.level >= MinionRoster.MaxLevel ? " <color=#ffd24a>MAX</color>"
            : " <size=88%><color=#6f6889>exp " + v.exp + "/" + MinionRoster.ExpPerLevel + "</color></size>";
        var lv = Text(row.rectTransform, "Lv " + v.level + expTxt + "  <color=#8cb8e6>攻×" + totalAtk.ToString("0.00") + " 硬×" + MinionRoster.EquipHpMult(id).ToString("0.00") + "</color>", 11.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(lv.rectTransform, 12, 32, 236, 18);
        // 🜏 ボスに任命したときに継ぐ魔神の名（個体ごとに固定）
        var go = Text(row.rectTransform, "◆" + GoetiaCatalog.RichTitleOf(id), 10.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(go.rectTransform, 12, 52, 246, 16);
        AddTooltip(row.gameObject, "ボス任命時: " + GoetiaCatalog.TitleOf(id) + " ／ " + GoetiaCatalog.Blessing(GoetiaCatalog.PillarOf(id).rank));
        // 所属：この個体がどの階の隊にいるか（1個体=1隊）／ボスに任命されているか（ボスは隊に入れない）
        int squadFloor = featureMgr != null ? featureMgr.SquadFloorOfIndividual(id) : -1;
        int bossFloor = featureMgr != null ? featureMgr.BossFloorOfIndividual(id) : -1;
        var myKin = KinRoster.Of(id);                      // 🗺️ 自身が眷属か
        var myLeader = KinRoster.LeaderOfFollower(id);     // 🗺️ どこかの眷属に率いられているか
        string belong = myKin != null ? "<color=#ffd24a>眷属『" + myKin.trueName + "』</color>"
            : myLeader != null ? "<color=#e3a94a>" + myLeader.trueName + "の配下</color>"
            : bossFloor >= 0 ? "<color=#e07a7a>B" + (bossFloor + 1) + "Fボス</color>"
            : squadFloor >= 0 ? "<color=#57c3ab>B" + (squadFloor + 1) + "F隊</color>" : "<color=#6f6889>未編成</color>";
        var st = Text(row.rectTransform, belong + "　" + (placed ? "<color=#e3a94a>配置中</color>" : "<color=#6f6889>待機</color>"), 11, FAINT, TextAlignmentOptions.TopLeft);
        Place(st.rectTransform, 130, 32, 130, 16);

        // 右：武器スロット（上）／防具スロット（下）
        AddEquipSlot(row, id, EquipmentCatalog.Slot.Weapon, "武器", 262, 10);
        AddEquipSlot(row, id, EquipmentCatalog.Slot.Armor, "防具", 262, 44);

        // 下段：🛡️隊編成（この階の隊へ）＋ 🧬個体進化（Lv/装備を保ったまま上位形態へ）
        float by = h - 30f;
        if (myKin != null || myLeader != null)
        {
            // 🗺️ 地上に出ている：ダンジョンの編成には使えない。操作は『地上』パネルで行う。
            string t = myKin != null ? "<color=#ffd24a>◆ 眷属『" + myKin.trueName + "』</color><size=84%><color=#6f6889>（『地上』パネルで編成・進軍）</color></size>"
                                     : "<color=#e3a94a>◆ " + myLeader.trueName + " の配下として地上に出ています</color>";
            var kt = Text(row.rectTransform, t, 10.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(kt.rectTransform, 12, by + 4, 300, 18);
        }
        else if (bossFloor >= 0)
        {
            // 👑 ボス任命中：実体は1つなので隊には入れない。外したいときはマップ上で撤去する。
            var bt = Text(row.rectTransform, "<color=#e07a7a>◆ B" + (bossFloor + 1) + "F のボス</color><size=84%><color=#6f6889>（隊には編成できません）</color></size>",
                10.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(bt.rectTransform, 12, by + 4, 240, 18);
        }
        else if (squadFloor >= 0)
        {
            var rmBtn = PrimaryButton(row, "隊から外す", PANEL2, MUTED, () => { featureMgr.SquadRemoveIndividual(id); RefreshMinionCodex(); RefreshSquadTray(); });
            Place((RectTransform)rmBtn.transform, 12, by, 100, 24);
        }
        else
        {
            var addBtn = PrimaryButton(row, "＋隊 (" + floorLabelNow() + ")", PANEL2, TEAL, () => { if (featureMgr != null && featureMgr.SquadAdd(id)) { RefreshMinionCodex(); RefreshSquadTray(); } });
            Place((RectTransform)addBtn.transform, 12, by, 116, 24);
        }

        // 🗺️ 眷属化：**条件を満たしていなくても常に欄を出し**、何が足りないかをチェックリストで見せる。
        //    （以前は条件を全部満たすまでボタン自体が現れず「どうすれば出るのか」が分からなかった）
        if (myKin == null && myLeader == null)
        {
            var reqs = KinRoster.NameRequirements(id);
            bool can = KinRoster.MeetsNameRequirements(id);
            int roll = nameRolls.ContainsKey(id) ? nameRolls[id] : 0;
            string cand = KinRoster.NameCandidate(id, roll);
            int kcost = KinRoster.NameCost(id);

            var kb = PrimaryButton(row, can ? ("眷属化：" + cand) : "眷属化", PANEL2, can ? GOLD : FAINT, () =>
            {
                int rr = nameRolls.ContainsKey(id) ? nameRolls[id] : 0;
                if (KinRoster.TryName(id, rr)) { RefreshMinionCodex(); RefreshSurfacePanel(); }
            });
            float kx = W - 210f;   // 行の右端に寄せる（装備スロット列と重ならないように）
            Place((RectTransform)kb.transform, kx, by, 152, 24);
            kb.interactable = can;

            // 条件のチェックリスト（満たしたものは緑の◆、未達は灰の・）
            var sb2 = new System.Text.StringBuilder();
            foreach (var q in reqs)
                sb2.Append(q.met ? "<color=#5cc47c>◆" + q.label + "</color>  " : "<color=#6f6889>・" + q.label + "</color>  ");
            var chk = Text(row.rectTransform, sb2.ToString(), 9f, FAINT, TextAlignmentOptions.TopRight);
            chk.enableWordWrapping = false;
            Place(chk.rectTransform, kx - 500f, by - 15, 690f, 14);

            AddTooltip(kb.gameObject, "真名『" + cand + "』を与えて眷属にする（-" + kcost + "DP）。\n"
                + "眷属は配下を率いて地上へ出られるが、ダンジョンの隊・ボスには使えなくなる。\n"
                + (can ? "" : "※ 上の条件をすべて満たすと押せるようになります。"));

            if (can)
            {
                var rb = PrimaryButton(row, "↻", PANEL2, MUTED, () =>
                {
                    nameRolls[id] = (nameRolls.ContainsKey(id) ? nameRolls[id] : 0) + 1;
                    RefreshMinionCodex();
                });
                Place((RectTransform)rb.transform, kx + 156f, by, 30, 24);
                AddTooltip(rb.gameObject, "別の真名の候補を出す");
            }
        }

        // 進化先（直系の子）を並べる。研究段階が未解禁なら理由を表示。
        float ex = 136f;
        var children = MinionEvolution.ChildrenOf(v.catalogIndex);
        if (children.Count == 0)
        {
            var mx = Text(row.rectTransform, "<color=#6f6889>これ以上進化しない（最終形態）</color>", 10.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(mx.rectTransform, ex, by + 4, 300, 18);
        }
        else
        {
            foreach (var ci in children)
            {
                int target = ci;
                var cd = MinionCatalog.Get(ci);
                bool ok = MinionEvolution.CanIndividualEvolveTo(ci);
                int cost = MinionEvolution.EvolveCost(ci);
                if (ok)
                {
                    var eb = PrimaryButton(row, "◆進化 " + cd.jpName + " -" + cost, BLOOD, TEXT,
                        () => { if (MinionRoster.TryEvolveIndividual(id, target)) { RefreshMinionCodex(); RefreshSquadTray(); } }, true);
                    Place((RectTransform)eb.transform, ex, by, 168, 24);
                }
                else
                {
                    var lk = Panel(row.rectTransform, "evolk_" + id + "_" + ci, C("#0f0d16"));
                    Place(lk.rectTransform, ex, by, 168, 24); Outline(lk, LINE);
                    var lt = Text(lk.rectTransform, "<color=#8cb8e6>・" + cd.jpName + "（研究）</color>", 10, FAINT, TextAlignmentOptions.Center, FontStyles.Bold);
                    StretchFull(lt.rectTransform);
                }
                ex += 172f;
            }
        }
    }

    private string floorLabelNow()
    {
        var fmgr = DungeonFloorManager.Instance;
        return "B" + ((fmgr != null ? fmgr.CurrentFloorIndex : 0) + 1) + "F";
    }

    // ============ 💬 ツールチップ（下部バーの上に説明を出す） ============
    private GameObject tooltipGO; private TextMeshProUGUI tooltipText;
    private void BuildTooltip(RectTransform root)
    {
        var p = Panel(root, "Tooltip", C("#0b0910"));
        Anchor(p, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        p.rectTransform.sizeDelta = new Vector2(560, 30);
        p.rectTransform.anchoredPosition = new Vector2(0, 62);
        Outline(p, GOLD_DK);
        p.raycastTarget = false;
        tooltipText = Text(p, "", 12, TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchOffset(tooltipText.rectTransform, 10, 4, 10, 4);
        tooltipText.raycastTarget = false;
        tooltipGO = p.gameObject; tooltipGO.SetActive(false);
    }
    private void ShowTooltip(string s)
    {
        if (tooltipGO == null) return;
        SetTxt(tooltipText, s); tooltipGO.SetActive(true); tooltipGO.transform.SetAsLastSibling();
    }
    private void HideTooltip() { if (tooltipGO != null) tooltipGO.SetActive(false); }

    // ホバーで説明を出す（UITooltipTrigger＝Pointer系のみ実装。EventTriggerだとスクロールを食う）
    private void AddTooltip(GameObject go, string tip)
    {
        // ⚠ EventTrigger は使わない。EventTrigger は IScrollHandler/IDragHandler も実装しているため、
        //   ツールチップを付けた要素の上でホイール/ドラッグが吸われ、親の ScrollRect に届かなくなる
        //   （＝カードの上ではスクロールできない、という操作性の不具合になる）。→ [[UITooltipTrigger]]
        var tt = go.GetComponent<UITooltipTrigger>();
        if (tt == null) tt = go.AddComponent<UITooltipTrigger>();
        tt.tip = tip;
        tt.onShow = ShowTooltip;
        tt.onHide = HideTooltip;
    }

    // 🖼️ Turbo Diskアイコン読込（キャッシュ）。無ければnull。
    private static readonly Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();
    private Sprite Icon(string name)
    {
        if (_iconCache.TryGetValue(name, out var s)) return s;
        s = Resources.Load<Sprite>("Icons/" + name);
        _iconCache[name] = s; return s;
    }
    private Image IconImg(Transform parent, string iconName, float x, float y, float size, Color tint)
    {
        var spr = Icon(iconName);
        var img = Panel(parent, "Icon_" + iconName, spr != null ? tint : new Color(0, 0, 0, 0));
        if (spr != null) { img.sprite = spr; img.type = Image.Type.Simple; img.preserveAspect = true; }
        Place(img.rectTransform, x, y, size, size);
        img.raycastTarget = false;
        return img;
    }

    private void AddEquipSlot(Image row, int id, EquipmentCatalog.Slot slot, string label, float x, float yy)
    {
        int g = MinionRoster.GradeOf(id, slot);
        bool isWeapon = slot == EquipmentCatalog.Slot.Weapon;
        int wt = MinionRoster.WeaponTypeOf(id);
        // 🖼️ アイコン：武器は"種別"のアイコン（剣/斧/弓/杖…）、防具は盾。素材グレード色で着色。
        Color tint = g >= 0 ? Color.Lerp(Color.white, C(EquipmentCatalog.ColorHex(g)), 0.5f) : new Color(0.6f, 0.6f, 0.66f, 1f);
        IconImg(row.rectTransform, isWeapon ? EquipmentCatalog.WeaponTypeIcon(wt) : "icon_shield", x + 4, yy - 1, 26, tint);
        var lbl = Text(row.rectTransform, isWeapon ? EquipmentCatalog.WeaponTypeName(wt) : label, 11, isWeapon ? TEXT : MUTED, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(lbl.rectTransform, x + 32, yy + 3, 30, 16);
        // 現在グレードのチップ
        var chip = Panel(row.rectTransform, "g_" + slot + "_" + id, C("#0f0d16"));
        Place(chip.rectTransform, x + 64, yy, 150, 24); Outline(chip, LINE);
        var gt = Text(chip.rectTransform, "<color=" + EquipmentCatalog.ColorHex(g) + ">" + EquipmentCatalog.Name(g) + "</color>", 12, TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(gt.rectTransform);
        // 強化＋（次グレードへ鍛造・DP消費）
        if (g < EquipmentCatalog.MaxGrade)
        {
            int cost = EquipmentCatalog.ForgeCost(g + 1);
            var fb = PrimaryButton(row, "強化＋ -" + cost, BLOOD, TEXT, () => { if (MinionRoster.TryForge(id, slot)) RefreshMinionCodex(); }, true);
            Place((RectTransform)fb.transform, x + 222, yy, 132, 24);
        }
        else
        {
            var mx = Text(row.rectTransform, "<color=#ffd24a>最高グレード</color>", 11, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(mx.rectTransform, x + 222, yy + 3, 132, 18);
        }
        // 外す
        if (g >= 0)
        {
            var rb = PrimaryButton(row, "外す", PANEL2, MUTED, () => { MinionRoster.Unequip(id, slot); RefreshMinionCodex(); });
            Place((RectTransform)rb.transform, x + 360, yy, 56, 24);
        }
        // ⚔️ 武器種の切替（無償＝"戦い方"の選択）。次の種別を予告表示。
        if (isWeapon)
        {
            int nextT = (wt + 1) % EquipmentCatalog.WeaponTypeCount;
            var d = EquipmentCatalog.WType(wt);
            var tb = PrimaryButton(row, "種別→" + EquipmentCatalog.WeaponTypeName(nextT), PANEL2, TEAL,
                () => { MinionRoster.CycleWeaponType(id); RefreshMinionCodex(); });
            Place((RectTransform)tb.transform, x + 422, yy, 112, 24);
            AddTooltip(tb.gameObject, EquipmentCatalog.WeaponTypeName(wt) + "：" + d.note
                + string.Format("（攻×{0:0.00} 間隔×{1:0.00} 射程+{2:0.0}）", d.atkMult, d.intervalMult, d.rangeBonus));
        }
    }

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
    private void LineRect(RectTransform parent, float x, float y, float w, float h)
    {
        var img = Panel(parent, "Line", LINE2); img.raycastTarget = false;
        Place(img.rectTransform, x, y, w, h);
    }

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
        panel = inner;

        float pad = 22f, w = FS_W - pad * 2;

        // ── UIを載せる不透明な板（これより後に作るものが上に乗る）──
        var headBg = Panel(panel, "HeadBg", PANEL);
        Place(headBg.rectTransform, 0, 0, FS_W, 124); Outline(headBg, LINE2); SkinPanel(headBg);
        surfaceRightBg = Panel(panel, "RightBg", PANEL);
        Outline(surfaceRightBg, LINE2); SkinPanel(surfaceRightBg);
        surfaceKinBg = Panel(panel, "KinBg", PANEL);
        Outline(surfaceKinBg, LINE2); SkinPanel(surfaceKinBg);
        surfaceTreeBg = Panel(panel, "TreeBg", PANEL);
        Place(surfaceTreeBg.rectTransform, 0, 124, FS_W, FS_H - 124);
        Outline(surfaceTreeBg, LINE2); SkinPanel(surfaceTreeBg);
        var title = Text(panel, "地上（六角の盤に領域が並ぶ。真名を与えた眷属が配下を率いて広げる）", 16, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 14, w - 60, 24);
        var close = PrimaryButton(panel, "× 迷宮へ戻る", PANEL2, TEXT, () => SetSurfaceMode(false));
        Place((RectTransform)close.transform, FS_W - pad - 132, 12, 132, 30);
        surfaceSummaryText = Text(panel, "", 11.5f, C("#8cb8e6"), TextAlignmentOptions.Left, FontStyles.Bold);
        surfaceSummaryText.enableWordWrapping = false;
        Place(surfaceSummaryText.rectTransform, pad, 40, w, 16);
        // 🏙️ 拠点と都市（C2）
        surfaceSettleText = Text(panel, "", 11.5f, C("#e3c34a"), TextAlignmentOptions.Left, FontStyles.Bold);
        surfaceSettleText.enableWordWrapping = false;
        Place(surfaceSettleText.rectTransform, pad, 58, w, 16);
        surfaceRivalText = Text(panel, "", 11.5f, C("#e05a5a"), TextAlignmentOptions.Left, FontStyles.Bold);
        surfaceRivalText.enableWordWrapping = false;
        Place(surfaceRivalText.rectTransform, pad, 76, w, 16);

        // 🗂️ タブ（盤／地上ツリー）
        surfaceTabBtns.Clear(); boardOnlyLabels.Clear();
        string[] stabs = { "盤", "地上ツリー" };
        for (int i = 0; i < stabs.Length; i++)
        {
            int ti = i;
            var tb = Panel(panel, "STab_" + i, PANEL2);
            Place(tb.rectTransform, pad + i * 132, 96, 128, 26); Outline(tb, LINE);
            var tlab = Text(tb.rectTransform, stabs[i], 12, TEXT, TextAlignmentOptions.Center, FontStyles.Bold); StretchFull(tlab.rectTransform);
            var tbn = tb.gameObject.AddComponent<Button>(); tbn.targetGraphic = tb;
            tbn.onClick.AddListener(() => { surfaceTab = ti; RefreshSurfacePanel(); });
            surfaceTabBtns.Add(tb);
        }

        // ⬡ 左上：ヘクス盤（クリックで領域を選ぶ）
        float mapW = 900f, mapH = 610f, mapTop = 130f;
        var mapBg = Panel(panel, "HexMap", C("#0c0a12"));
        Place(mapBg.rectTransform, pad, mapTop, mapW, mapH); Outline(mapBg, LINE);
        mapBg.gameObject.AddComponent<RectMask2D>();                   // 盤が枠からはみ出さないように
        // 🖱️ 掴んで動かす／ホイールで寄る（盤が1画面に収まらないため）
        var pz = mapBg.gameObject.AddComponent<HexMapPanZoom>();
        hexMapRoot = NewRect("Hexes", mapBg.rectTransform);
        hexMapRoot.anchorMin = hexMapRoot.anchorMax = new Vector2(0.5f, 0.5f);
        hexMapRoot.pivot = new Vector2(0.5f, 0.5f);
        hexMapRoot.sizeDelta = new Vector2(mapW, mapH);
        hexMapRoot.anchoredPosition = Vector2.zero;
        pz.content = hexMapRoot;
        mapPanZoom = pz;

        // 左下：眷属リスト（盤はシーンで描くので、UIは下端の帯だけに畳む）
        float kinH = 178f, kinTop = FS_H - kinH - pad;
        Place(surfaceKinBg.rectTransform, 0, kinTop - 10, mapW + pad * 2, kinH + 18);
        var kl = Text(panel, "◆ 眷属（図鑑の個体タブで『眷属化』すると現れます）", 12.5f, TEAL, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(kl.rectTransform, pad, kinTop, mapW, 16);
        boardOnlyLabels.Add(kl.gameObject);
        kinListContainer = MakeVScroll(panel, pad, kinTop + 20, mapW, kinH - 20); kinListW = mapW;

        // 右：選択中ヘクスの詳細
        float rx = pad + mapW + 18f, rw = w - mapW - 18f;
        Place(surfaceRightBg.rectTransform, rx - 12, mapTop - 26, rw + 24, FS_H - mapTop + 12);
        var dl = Text(panel, "◆ 選択中の領域（左のヘクスをクリックで切替）", 12.5f, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(dl.rectTransform, rx, mapTop - 18, rw, 16);
        boardOnlyLabels.Add(dl.gameObject);
        regionListContainer = MakeVScroll(panel, rx, mapTop, rw, FS_H - mapTop - pad); regionListW = rw;

        // 🗺️ 地上ツリー（盤と切り替えて表示）
        surfaceTreeRoot = MakeVScroll(panel, pad, 130, w, FS_H - 130 - pad); surfaceTreeW = w;

        RefreshSurfacePanel();
        surfacePanel.SetActive(false);
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
    private readonly List<GameObject> foldedDungeonUI = new List<GameObject>();

    private void SetSurfaceMode(bool on)
    {
        if (surfacePanel == null) return;
        surfaceModeOn = on;                 // ※先に立てる（RefreshSurfacePanel がこの値で盤の表示を決めるため）
        surfacePanel.SetActive(on);

        // 🗂️ 迷宮側のUIを**丸ごと畳む**。
        //    以前は下部ツールバーとフロアタブだけを隠していたので、地上盤の縁から迷宮のパネルが覗いて
        //    雰囲気を壊していた。Canvas直下の兄弟を全部畳めば、パネルが増えても勝手に追従する。
        //    ※もともと閉じているものは触らない（戻すときに勝手に開かないように）。
        var canvasRoot = surfacePanel.transform.parent;
        if (on)
        {
            foldedDungeonUI.Clear();
            for (int i = 0; i < canvasRoot.childCount; i++)
            {
                var g = canvasRoot.GetChild(i).gameObject;
                if (g == surfacePanel || g == tooltipGO || !g.activeSelf) continue;
                g.SetActive(false); foldedDungeonUI.Add(g);
            }
        }
        else
        {
            foreach (var g in foldedDungeonUI) if (g != null) g.SetActive(true);
            foldedDungeonUI.Clear();
        }
        HideTooltip();

        // 🎥 迷宮のカメラを止めて、地上のカメラに渡す。
        //    ⚠ 迷宮の GameObject は**消さない**（enabled を落とすだけ）ので、階層・配置・個体・進行は
        //      そのままメモリに残る＝戻ったときに完全に元通りになる。畳む＝壊す ではない。
        //    ⚠ `Camera.main` 1台だけを見ると取りこぼす（タグ付けや2台目のカメラ次第）。**有効なカメラを全部畳む**。
        if (on)
        {
            if (surfaceView == null)
            {
                surfaceView = SurfaceView.Create(uiFont);
                surfaceView.onPick = id => { selectedRegionId = id; surfaceView.SetSelected(id); RefreshSurfacePanel(); };
            }
            if (selectedRegionId < 0) selectedRegionId = SurfaceMap.IndexOfCenter();
            foldedCameras.Clear();
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (c == surfaceView.cam || !c.enabled) continue;
                c.enabled = false; foldedCameras.Add(c);
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
        if (surfacePanel == null || kinListContainer == null) return;
        for (int i = 0; i < surfaceTabBtns.Count; i++) SetSel(surfaceTabBtns[i], i == surfaceTab);
        bool board = surfaceTab == 0;
        // 🌍 盤は SurfaceView（ワールド空間）が描くので、uGUIのヘクス盤は畳んだまま使わない
        if (hexMapRoot != null) hexMapRoot.parent.gameObject.SetActive(false);
        if (kinListContainer != null) kinListContainer.parent.gameObject.SetActive(board);
        if (regionListContainer != null) regionListContainer.parent.gameObject.SetActive(board);
        if (surfaceRightBg != null) surfaceRightBg.gameObject.SetActive(board);
        if (surfaceKinBg != null) surfaceKinBg.gameObject.SetActive(board);
        if (surfaceTreeRoot != null) surfaceTreeRoot.parent.gameObject.SetActive(!board);
        if (surfaceTreeBg != null) surfaceTreeBg.gameObject.SetActive(!board);
        foreach (var g in boardOnlyLabels) if (g != null) g.SetActive(board);
        // ⚠ タブが「地上ツリー」でも地上カメラは**止めない**。止めると迷宮のカメラも止まったままで
        //    有効なカメラが0台になり、前のフレーム（迷宮）が残って見える。ツリーは板で隠す。
        if (surfaceView != null) { surfaceView.SetActiveView(surfaceModeOn); surfaceView.MarkDirty(); }
        if (!board) { RefreshSurfaceTree(); RefreshSurfaceHeader(); return; }
        RefreshKinList();
        RefreshRegionDetail();
        RefreshSurfaceHeader();
    }

    private void RefreshSurfaceHeader()
    {
        if (surfaceSummaryText != null)
        {
            var y = SurfaceMap.YieldSummary();
            var dy = DistrictCatalog.TotalYields();
            SetTxt(surfaceSummaryText, string.Format(
                "支配 <color=#5cc47c>{0}/{1}</color> 領域　領域産出 <color=#e3a94a>+{2}DP</color> <color=#57c3ab>+{3}素材</color> <color=#8cb8e6>+{4}RP</color> <color=#e05a5a>+{5}名声</color>"
                + "　／　施設産出 <color=#e3a94a>+{6}DP</color> <color=#57c3ab>+{7}素材</color> <color=#8cb8e6>+{8}RP</color> <color=#c04a6a>+{9}感情</color>"
                + "　<size=88%><color=#9c95b4>世界水準+{10:0.00}</color></size>",
                SurfaceMap.OwnedCount, SurfaceMap.Count - 1, y.dp, y.mat, y.rp, y.fame,
                dy.dp, dy.mat, dy.rp, dy.emotion, SurfaceMap.WorldTierBias));
        }
        if (surfaceSettleText != null)
        {
            int unassigned = 0;
            foreach (var rg in SurfaceMap.All)
                if (rg.owned && !rg.isOcean && rg.type != SurfaceMap.RegionType.Gate && SettlementSystem.SettlementOf(rg.id) < 0) unassigned++;
            SetTxt(surfaceSettleText, SettlementSystem.HeaderLine()
                + (unassigned > 0 ? "　<color=#e08a3c>未編入の辺境 " + unassigned + " ― 産出しない（拠点を築くか、拠点の人口を育てて国境を広げる）</color>" : "")
                + "　<size=88%><color=#9c95b4>不満1点＝産出-5%（最大-80%）／幸福が貯まると祝祭</color></size>");
        }
        if (surfaceRivalText != null)
        {
            var rivalTxt = new System.Text.StringBuilder();
            for (int i = 0; i < RivalLords.Count; i++)
            {
                var rv = RivalLords.Get(i);
                rivalTxt.Append("  <color=" + rv.colorHex + ">" + rv.name + "</color>");
                rivalTxt.Append(rv.defeated ? "<color=#5cc47c>[排除]</color>"
                    : "<size=88%>(力" + rv.power.ToString("0") + "/" + RivalLords.TerritoryOf(i) + "領)</size>");
            }
            SetTxt(surfaceRivalText, "◆他の魔王 " + RivalLords.AliveCount + "/" + RivalLords.Count + " 存命" + rivalTxt
                + "　<size=88%><color=#9c95b4>本拠地を落とすと真核を奪える。彼らも毎ターン領域を広げ、こちらにも攻めてくる。</color></size>");
        }
    }

    // 🗺️ 地上ツリー（Civの社会制度に相当。地上を耕すと天啓が付いて安くなる）
    private void RefreshSurfaceTree()
    {
        var c = surfaceTreeRoot; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = surfaceTreeW, y = 0f;
        var head = Text(c, "◆ 地上ツリー　<size=88%><color=#9c95b4>研究点 " + ResearchState.RP
            + " RP ／ 地上を耕すほど天啓が付いて40%引きになる</color></size>", 14, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(head.rectTransform, 4, y, w - 8, 20); y += 30;

        var nodes = ResearchCatalog.ByField(ResearchField.Surface);
        nodes.Sort((a, b) => a.row.CompareTo(b.row));
        float cw = (w - 3 * 14) / 3f, ch = 122f;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            float x = 4 + (i % 3) * (cw + 14);
            float cy = y + (i / 3) * (ch + 12);
            bool done = ResearchState.IsResearched(n.id);
            bool can = ResearchState.CanResearch(n.id);
            bool prereqOK = ResearchState.PrereqMet(n);
            var card = Panel(c, "ST_" + n.id, CARD);
            Place(card.rectTransform, x, cy, cw, ch); Outline(card, done ? GREEN : (can ? GOLD : LINE));
            var nm = Text(card.rectTransform, n.jpName, 14, done ? GREEN : (prereqOK ? TEXT : FAINT), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm.rectTransform, 12, 8, cw - 24, 18);
            int eff = ResearchState.EffectiveCost(n);
            string stxt = done ? "研究済"
                : prereqOK ? ("コスト " + eff + " RP" + (eff < n.cost ? " <size=82%><color=#5cc47c>(-" + (n.cost - eff) + ")</color></size>" : ""))
                : ("― 前提: " + (n.prereq != null && n.prereq.Length > 0 ? NodeName(n.prereq[0]) : ""));
            var st = Text(card.rectTransform, stxt, 11, done ? GREEN : (can ? GOLD : MUTED), TextAlignmentOptions.TopLeft);
            Place(st.rectTransform, 12, 28, cw - 24, 16);
            var ds = Text(card.rectTransform, n.desc, 10.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(ds.rectTransform, 12, 48, cw - 24, 46);
            if (!string.IsNullOrEmpty(n.eureka))
            {
                bool got = EurekaTracker.Has(n.id);
                var eu = Text(card.rectTransform, got ? "<color=#ffd24a>◆天啓達成 40%引き</color>" : "<color=#6f6889>天啓: " + n.eureka + "</color>",
                    10, got ? GOLD : FAINT, TextAlignmentOptions.TopLeft, got ? FontStyles.Bold : FontStyles.Normal);
                Place(eu.rectTransform, 12, ch - 22, cw - 24, 16);
            }
            if (can)
            {
                string nid = n.id;
                var b = card.gameObject.AddComponent<Button>(); b.targetGraphic = card;
                b.onClick.AddListener(() => { if (ResearchState.TryResearch(nid)) RefreshSurfacePanel(); });
            }
        }
        int rows = (nodes.Count + 2) / 3;
        c.sizeDelta = new Vector2(0f, y + rows * (ch + 12) + 20);
    }
    private static string NodeName(string id) { ResearchNode n; return ResearchCatalog.TryGet(id, out n) ? n.jpName : id; }

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
                var ht = Text(cell, "<color=#ff6a4a>◆真核</color>", 9.5f, CRIMSON, TextAlignmentOptions.Center, FontStyles.Bold);
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
            + (r.rivalHome >= 0 ? " <color=#ff6a4a>◆真核</color>" : ""), 15, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
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

        // 操作ボタン
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

            // 既にある施設（1つ目・2つ目＝街区）
            for (int slot = 0; slot < 2; slot++)
            {
                int di2 = slot == 0 ? r.district : r.district2;
                if (di2 < 0) continue;
                var d = DistrictCatalog.Get(di2);
                string detail; int adj = DistrictCatalog.Adjacency(di2, r.id, out detail);
                int shown = r.specialist ? adj * 2 : adj;
                var card = Panel(c, "Built" + slot, CARD);
                Place(card.rectTransform, 0, y, w - 6, 76); Outline(card, C(d.colorHex));
                var n1 = Text(card.rectTransform, "<color=" + d.colorHex + ">" + d.jpName + "</color> 建設済み"
                    + (slot == 1 ? " <color=#e3c34a>［街区］</color>" : "") + (r.specialist ? " <color=#57c3ab>［専門家］</color>" : ""),
                    13.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Place(n1.rectTransform, 12, 8, w - 30, 18);
                var n2 = Text(card.rectTransform, DistrictCatalog.YieldName(d.yield) + " <color=#5cc47c>+" + (1 + shown) + "</color>"
                    + "　<size=88%><color=#9c95b4>基礎1 ＋ 隣接" + adj + (r.specialist ? " ×2(専門家)" : "") + "</color></size>", 11.5f, MUTED, TextAlignmentOptions.TopLeft);
                Place(n2.rectTransform, 12, 30, w - 30, 18);
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
                for (int i = 0; i < DistrictCatalog.Count; i++)
                {
                    int di = i; var d = DistrictCatalog.Get(i);
                    bool unlocked = DistrictCatalog.IsUnlocked(i);
                    string detail; int adj = DistrictCatalog.Adjacency(i, r.id, out detail);
                    int cost = Mathf.RoundToInt(DistrictCatalog.Cost(i) * (asQuarter ? 1.5f : 1f));
                    bool cheap = DistrictCatalog.IsLeastBuilt(i);
                    var card = Panel(c, "D_" + i, CARD);
                    Place(card.rectTransform, 0, y, w - 6, 76); Outline(card, unlocked ? LINE2 : LINE);
                    var n1 = Text(card.rectTransform, "<color=" + (unlocked ? d.colorHex : "#4a4560") + ">" + d.jpName + "</color>"
                        + " <size=86%><color=#9c95b4>" + DistrictCatalog.YieldName(d.yield) + "</color></size>", 13.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                    Place(n1.rectTransform, 12, 8, w - 160, 18);
                    var n2 = Text(card.rectTransform, "ここに建てると <color=#5cc47c>+" + (1 + adj) + "</color> <size=88%><color=#9c95b4>(基礎1＋隣接" + adj + ")</color></size>",
                        11.5f, MUTED, TextAlignmentOptions.TopLeft);
                    Place(n2.rectTransform, 12, 30, w - 160, 18);
                    var n3 = Text(card.rectTransform, "<size=90%><color=#6f6889>" + detail + "</color></size>", 10.5f, FAINT, TextAlignmentOptions.TopLeft);
                    Place(n3.rectTransform, 12, 50, w - 160, 20);
                    if (unlocked)
                    {
                        var bb = PrimaryButton(card, "建設 " + cost + "DP" + (cheap ? " <size=80%>(40%引)</size>" : ""), PANEL2, C(d.colorHex),
                            () => { if (DistrictCatalog.TryBuild(r.id, di)) RefreshSurfacePanel(); });
                        Place((RectTransform)bb.transform, w - 152, 24, 138, 28);
                    }
                    else
                    {
                        var no = Text(card.rectTransform, "<color=#4a4560>地上研究が必要</color>", 10.5f, FAINT, TextAlignmentOptions.TopRight);
                        Place(no.rectTransform, w - 152, 32, 138, 16);
                    }
                    AddTooltip(card.gameObject, d.jpName + "：" + d.desc + "\n" + detail);
                    y += 84;
                }
            }
        }
        c.sizeDelta = new Vector2(0f, Mathf.Max(y + 8, 80));
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
            float rowH = 104f;
            var row = Panel(c, "Kin_" + k.individualId, sel ? SEL : CARD);
            Place(row.rectTransform, 0, y, w - 6, rowH - 6); Outline(row, sel ? GOLD : LINE);
            var btnSel = row.gameObject.AddComponent<Button>(); btnSel.targetGraphic = row;
            btnSel.onClick.AddListener(() => { selectedKinId = kk.individualId; RefreshSurfacePanel(); });

            var nm = Text(row.rectTransform, "◆<color=#ffd24a>" + k.trueName + "</color>　" + d.jpName + " <size=86%>#" + v.id + " Lv" + v.level + "</size>",
                14, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm.rectTransform, 12, 8, w - 220, 20);

            int lpU = KinRoster.LPUsed(k), lpM = KinRoster.LPMax(k);
            var st = Text(row.rectTransform, "統率 <color=#57c3ab>" + lpU + "/" + lpM + "</color>　戦力 <color=#e05a5a>" + KinRoster.ArmyPower(k).ToString("0") + "</color>　武勲 " + k.conquests,
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

    private void BuildExpandPanel(RectTransform root)
    {
        var panel = Panel(root, "ExpandPanel", PANEL);
        expandPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(720, 470);
        panel.rectTransform.anchoredPosition = new Vector2(0, 10);
        Outline(panel, LINE2); SkinPanel(panel);

        float pad = 22f, w = 720 - pad * 2;
        var title = Text(panel, "領域（広さ＝配置枠と名声／深さ＝報酬倍率）", 14.5f, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, pad, 14, w - 40, 22);
        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => expandPanel.SetActive(false));
        Place((RectTransform)close.transform, 720 - pad - 28, 12, 28, 26);
        var sub = Text(panel, "広げる＝その階に置ける要素が+4枠／名声が上がり客が増える。深くする＝その階の撃破報酬が上がる。", 11, MUTED, TextAlignmentOptions.Left);
        Place(sub.rectTransform, pad, 38, w, 16);
        domainSummaryText = Text(panel, "", 11.5f, C("#8cb8e6"), TextAlignmentOptions.Left, FontStyles.Bold);
        Place(domainSummaryText.rectTransform, pad, 56, w, 16);

        var cont = NewRect("Rows", panel.rectTransform);
        Place(cont, pad, 80, w, 470 - 80 - pad);
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
        if (domainSummaryText != null)
            domainSummaryText.text = "名声 " + floorMgr.DomainRenown + "（拡張 " + floorMgr.ExpandedRenown + "段）"
                + " → ウェーブ増員 +" + DungeonFloorManager.RenownBonusAdventurers
                + "・冒険者ランク +" + DungeonFloorManager.RenownHeroRankBias.ToString("0.00")
                + "　<color=#9c95b4>広く深いほど強い客が来る＝旨いが危険</color>";
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
            var nm = Text(row.rectTransform, "B" + (i + 1) + "F" + (deepest ? " 魔" : "") + "  <size=112%>" + size + "×" + size + "</size>", 13, deepest ? CRIMSON : TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(nm.rectTransform, 12, 6, 170, 20);
            // 🏛️ この階が今いくらの器と報酬を持っているか
            var gain = Text(row.rectTransform,
                "<color=#57c3ab>配置枠 " + floorMgr.PlacementCap(i) + "</color>　<color=#e3a94a>報酬 ×" + floorMgr.DepthRewardMult(i).ToString("0.00") + "</color>",
                10.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(gain.rectTransform, 12, 26, 200, 16);
            if (floorMgr.CanExpandFloor(i))
            {
                int ns = floorMgr.NextFloorSize(i), rp = floorMgr.ExpandRPCost(i), dp = floorMgr.ExpandDPCost(i);
                var info = Text(row.rectTransform,
                    "→ " + ns + "×" + ns + " <color=#5cc47c>(枠+4)</color>    <color=#8cb8e6>" + rp + " RP</color>  <color=#e3a94a>" + dp + " DP</color>",
                    12, MUTED, TextAlignmentOptions.Left);
                Place(info.rectTransform, 216, 13, w - 326, 20);
                var btn = PrimaryButton(row, "拡張", BLOOD, TEXT, () => { if (floorMgr.TryExpandFloor(fi)) { RefreshExpandPanel(); RefreshFloorTabs(); } }, true);
                Place((RectTransform)btn.transform, w - 98, 8, 86, 30);
                btn.interactable = prep && ResearchState.RP >= rp && (res == null || res.DungeonPoints >= dp);
            }
            else
            {
                var mx = Text(row.rectTransform, "<color=#5cc47c>最大 (50×50)</color>", 12, GREEN, TextAlignmentOptions.Left);
                Place(mx.rectTransform, 216, 15, 200, 16);
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
                : (need != "" && ResearchCatalog.TryGet(need, out var rn) ? "<color=#8cb8e6>🔬 研究『" + rn.jpName + "』が必要</color>" : "—");
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
        SetTxt(descentToastText, $"{floorLabel} へ降下！　<size=60%><color=#9c95b4>生存者 {survivors}</color></size>");
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
        var dlBtn = PrimaryButton(bar, "魔王", PANEL2, TEXT, () => { if (demonPanel != null) { demonPanel.SetActive(!demonPanel.activeSelf); dlSig = null; } });
        SizeElem(dlBtn.gameObject, 66, 34);
        var emoBtn = PrimaryButton(bar, "感情", PANEL2, TEXT, () => { if (emotionPanel != null) { emotionPanel.SetActive(!emotionPanel.activeSelf); emoSig = null; } });
        SizeElem(emoBtn.gameObject, 66, 34);
        var relBtn = PrimaryButton(bar, "遺物", PANEL2, TEXT, () => { if (relicPanel != null) { relicPanel.SetActive(!relicPanel.activeSelf); RefreshRelicPanel(); } });
        SizeElem(relBtn.gameObject, 66, 34);
        var rsBtn = PrimaryButton(bar, "研究", PANEL2, TEXT, () => { if (researchPanel != null) { bool now = !researchPanel.activeSelf; researchPanel.SetActive(now); if (now) researchPanel.transform.SetAsLastSibling(); RefreshResearchPanel(); } });
        SizeElem(rsBtn.gameObject, 66, 34);
        var exBtn = PrimaryButton(bar, "拡張", PANEL2, TEXT, () => { if (expandPanel != null) { expandPanel.SetActive(!expandPanel.activeSelf); RefreshExpandPanel(); } });
        SizeElem(exBtn.gameObject, 66, 34);
        var surBtn = PrimaryButton(bar, "地上", PANEL2, TEXT, () => SetSurfaceMode(surfacePanel == null || !surfacePanel.activeSelf));
        SizeElem(surBtn.gameObject, 66, 34);

        // 🩸 魔王HPバー（討伐＝ゲームオーバーの核。常時可視）
        BuildDemonLordHpBar(bar);

        // 伸縮スペーサ
        Spacer(bar);

        // 資源
        dpText = ResChip(bar, GOLD, "DP", "0");
        fameText = ResChip(bar, VIOLET, "名声", "0");
        matText = ResChip(bar, TEAL, "素材", "0");
        threatText = ResChip(bar, BLOOD, "脅威度", "1.00"); // 🕸️ 誘導経済：世界の脅威度
        slotText = ResChip(bar, TEAL, "配置枠", "0/8");    // 🏛️ 領域：この階に置ける要素数（広げると増える）
        worldText = ResChip(bar, GOLD, "世界水準", "G Lv1"); // 🌍 次に来る冒険者の目安（急に強くならないか事前に読めるように）
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
        panel.rectTransform.sizeDelta = new Vector2(360, 612);
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
        // 🏔️ 形の説明ではなく **得と損** を出す（選ぶ理由が見えるように）
        string[] tDesc = {
            "配置枠+2 ／ 癖なし",
            "冒険者が長居+35% ／ 宝箱-25%",
            "部隊+10%・徘徊+1 ／ 集客-15%",
            "宝箱+50%・集客+20% ／ トーテム半径-1" };
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
            var b = Chip(panel, cx, cy, chipW, chipH, sNames[i], sCols[i], () => { selSpace = idx; generator?.SetSpaceType(idx); RefreshSelections(); RefreshThemeEffect(); });
            AddTooltip(b.gameObject, sNames[i] + "：" + DungeonTheme.SpaceEffect((DungeonGenerator.SpaceType)idx));
            spaceBtns.Add(b);
        }
        // 🏔️ 選択中の空間タイプの効果（チップだけでは分からないので明示する）
        spaceEffectText = Text(panel, "", 10.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(spaceEffectText.rectTransform, pad, 322, w, 16);
        RefreshThemeEffect();

        // 🌍 地上の広さ（Civのマップサイズ相当）。盤は手続き生成なので毎回違う地形になる。
        var gl = Text(panel, "地上の広さ（Civ準拠。毎回違う地形が生成されます）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(gl.rectTransform, pad, 344, w, 16);
        var gSizes = new[] { SurfaceGen.Size.Tiny, SurfaceGen.Size.Small, SurfaceGen.Size.Medium, SurfaceGen.Size.Large };
        var gNames = new string[4];
        for (int i = 0; i < 4; i++) gNames[i] = SurfaceGen.NameOf(gSizes[i]) + " " + SurfaceGen.TileCount(gSizes[i]);
        surfaceSizeBtns.Clear();
        float gw = (w - 24) / 4f;
        for (int i = 0; i < 4; i++)
        {
            int gi = i;
            var b = Panel(panel, "GSize_" + i, PANEL2);
            Place(b.rectTransform, pad + i * (gw + 8), 362, gw, 26); Outline(b, LINE);
            var tx = Text(b.rectTransform, gNames[i], 11.5f, TEXT, TextAlignmentOptions.Center, FontStyles.Bold); StretchFull(tx.rectTransform);
            var bt = b.gameObject.AddComponent<Button>(); bt.targetGraphic = b;
            bt.onClick.AddListener(() =>
            {
                SurfaceMap.Regenerate(gSizes[gi], Random.Range(1, int.MaxValue));
                selectedRegionId = SurfaceMap.IndexOfCenter();
                if (surfaceView != null) { surfaceView.FitToBoard(); surfaceView.CenterOn(selectedRegionId); }
                RefreshSurfaceSizeBtns(); RefreshSurfacePanel();
            });
            AddTooltip(b.gameObject, gNames[i] + "タイル（幅" + SurfaceGen.WidthOf(gSizes[gi]) + "×高さ" + SurfaceGen.HeightOf(gSizes[gi])
                + "・東西がループします）\n引き切ると世界がちょうど1つ収まります。");
            surfaceSizeBtns.Add(b);
        }
        RefreshSurfaceSizeBtns();

        // 宝箱量
        var cl = Text(panel, "宝箱の量（階層の広さに比例して増えます）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(cl.rectTransform, pad, 400, w, 16);
        string[] cNames = { "少", "中", "多" };
        float ccw = (w - 16) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            float cx = pad + i * (ccw + 8);
            var b = Chip(panel, cx, 420, ccw, 30, cNames[i], GOLD, () => { selChest = idx; generator?.SetChestAmount(idx); RefreshSelections(); RefreshCost(); });
            chestBtns.Add(b);
        }

        // 階層数（多いほどコスト大・魔王まで遠い＝防御が深くなる）
        var fl = Text(panel, "階層数（深いほどコスト大・防御が深くなる）", 11, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(fl.rectTransform, pad, 458, w, 16);
        string[] fNames = { "1層", "2層", "3層" };
        float fcw = (w - 16) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            float cx = pad + i * (fcw + 8);
            var b = Chip(panel, cx, 478, fcw, 30, fNames[i], VIOLET, () => { selFloors = idx; floorMgr?.SetFloorCount(idx + 1); RefreshSelections(); RefreshCost(); });
            floorCountBtns.Add(b);
        }

        // コスト表示
        costText = Text(panel, "生成コスト  500 DP", 12.5f, MUTED, TextAlignmentOptions.Left);
        Place(costText.rectTransform, pad, 516, w, 18);

        // 生成ボタン
        generateBtn = PrimaryButton(panel, "迷宮を生成する", BLOOD, C("#f0d9a0"), () =>
        {
            if (generator == null) return;
            if (floorMgr != null) floorMgr.SetFloorCount(selFloors + 1);
            bool ok = generator.TryGenerateWithCost();
            RefreshCost();
            RefreshFloorTabs();
        }, true);
        Place((RectTransform)generateBtn.transform, pad, 540, w, 44);

        RefreshSelections();
    }

    // ---------- ③下部コマンドバー ----------
    private void BuildBottomBar(RectTransform root)
    {
        var bar = Panel(root, "BottomBar", HUD_BG);
        bottomBar = bar.gameObject;   // 🌍 地上モードでは隠す
        Anchor(bar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        bar.rectTransform.sizeDelta = new Vector2(0, 60); bar.rectTransform.anchoredPosition = Vector2.zero;
        AddTopBorder(bar);
        var h = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(16, 16, 9, 9); h.spacing = 10; h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;

        var hint = Text(bar, "配置ツール", 11, FAINT, TextAlignmentOptions.Left);
        SizeElem(hint.gameObject, 68, 40);

        ToolButton(bar, "トーテム", TEAL, () => { input?.SetToolMode(6); ShowStripFor(6); }, 6, "トーテム：範囲に効果を撒く『面の層』。13種（強化/家系特化/冒険者弱体/罠・感情連携/回復）。種類は領域研究で解禁。");
        ToolButton(bar, "罠", CRIMSON, () => { input?.SetToolMode(3); ShowStripFor(3); }, 3, "罠：踏んだ冒険者にダメージと状態異常。種類は領域研究で解禁（盗賊はMPで解除）。");
        ToolButton(bar, "スポナー", VIOLET, () => { input?.SetToolMode(7); ShowStripFor(7); }, 7, "スポナー：戦闘中に雑魚を湧かせ続ける。数で消耗させる。");
        ToolButton(bar, "ボス", CRIMSON, () => { input?.SetToolMode(8); ShowStripFor(8); }, 8, "ボス任命：召喚した個体を各階1体だけボスに。強化＋大型化して出現する。");
        ToolButton(bar, "特殊敵", GOLD, () => { input?.SetToolMode(9); ShowStripFor(9); }, 9, "特殊敵：素材を払って6種から配置。強力な単体戦力。");
        ToolButton(bar, "宝箱", GREEN, () => { input?.SetToolMode(12); ShowStripFor(12); }, 12, "宝箱(誘導)：拾得装備を素材に錬成。集客を上げるが装備を奪われる両刃。錬成研究で解禁。");
        ToolButton(bar, "部隊", C("#8cb8e6"), () => { input?.SetToolMode(11); ShowStripFor(11); }, 11, "部隊：この階の隊員(個体)を1体ずつ好きなマスへ配置する。");
        ToolButton(bar, "消去", MUTED, () => { input?.SetToolMode(10); ShowStripFor(10); }, 10, "消去：配置した要素を撤去する（準備フェーズのみ・右クリックでも可）。");
        ToolButton(bar, "冒険者(検証)", GOLD, () => { input?.SetToolMode(4); ShowStripFor(4); }, 4, "デバッグ：冒険者を1体その場に湧かせる（動作確認用）。");

        // 🧟 配下セレクタ（図鑑を開いてロスター16種から選ぶ）
        var sp = Text(bar, "配下", 11, FAINT, TextAlignmentOptions.Center);
        SizeElem(sp.gameObject, 40, 40);
        var codexBtn = PrimaryButton(bar, "図鑑 →", PANEL2, TEXT, () => { if (minionPanel != null) { bool now = !minionPanel.activeSelf; minionPanel.SetActive(now); if (now) minionPanel.transform.SetAsLastSibling(); RefreshMinionCodex(); RefreshSquadTray(); } });
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
        if (slotText != null && featureMgr != null) slotText.text = featureMgr.PlacedCount + "/" + featureMgr.PlacementCap;
        if (worldText != null)
        {
            float wt = AdventurerAI.WorldTierNow();
            SetTxt(worldText, AdventurerAI.RankLetter(Mathf.RoundToInt(wt)) + " Lv" + AdventurerAI.ExpectedLevelNow());
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
                    SetTxt(phaseText, $"戦闘 {mm}:{ss:00}"); phaseText.color = CRIMSON;
                }
            }
            if (phasePill != null) phasePill.color = prep ? C("#183726") : C("#3a1a1a");
            if (genPanel != null && genPanel.activeSelf != prep) genPanel.SetActive(prep);
            if (invadeBtn != null) invadeBtn.interactable = prep;
        }
        if (demonPanel != null && demonPanel.activeSelf)
        {
            string s = DemonPanelSig();
            if (s != dlSig) { dlSig = s; RefreshDemonPanel(); }
        }
        if (emotionPanel != null && emotionPanel.activeSelf)
        {
            string s = EmotionPanelSig();
            if (s != emoSig) { emoSig = s; RefreshEmotionPanel(); }
            else RefreshEmotionPools();
        }
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
        SetTxt(costText, "生成コスト  <b><color=#e3a94a>" + cost.ToString("N0") + " DP</color></b>");
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
    // ============ 🈶 グリフのサニタイズ ============
    // UIフォントはシステムフォントから動的生成しているため、**記号や絵文字の多くが欠落して □ になる**。
    // 実測(HasCharacter)で使えるのは ◆ □ → ・ … ― ＋ × 『 』 程度しかなく、
    // ・ ◆ ・ ◆ ・ ◆ ・ ▼ ◆ ☆ ◆ ・ ← ↑ ↓ → 『』『』 などは全て欠落する。
    // 個々の文字列を直すのは漏れるので、**UIテキストを作る/差し替える一箇所で機械的に置換**する。
    // ⚠ この表のキーと値は **必ず \uXXXX のエスケープで書く**。生の記号で書くと、
    //   「フォントに無い記号を一括置換」する保守作業をしたときにこの表自身が書き換わり、
    //   キー重複(ArgumentException)でUI生成が丸ごと落ちる（実際に一度やらかした）。
    private static readonly Dictionary<char, string> GlyphMap = new Dictionary<char, string>
    {
        { '\u25C8', "◆" },   // U+25C8 diamond-with-dot
        { '\u25CF', "◆" },   // U+25CF black circle
        { '\u25A0', "◆" },   // U+25A0 black square
        { '\u2605', "◆" },   // U+2605 black star
        { '\u25B2', "◆" },   // U+25B2 black up triangle
        { '\u25CE', "◆" },   // U+25CE bullseye
        { '\u2666', "◆" },   // U+2666 diamond suit
        { '\u2662', "◆" },   // U+2662 white diamond suit
        { '\u2726', "◆" },   // U+2726 black four-pointed star
        { '\u2727', "◆" },   // U+2727 white four-pointed star
        { '\u25C7', "・" },   // U+25C7 white diamond
        { '\u25CB', "・" },   // U+25CB white circle
        { '\u3007', "・" },   // U+3007 ideographic zero
        { '\u25B3', "・" },   // U+25B3 white up triangle
        { '\u25BD', "・" },   // U+25BD white down triangle
        { '\u203B', "・" },   // U+203B reference mark
        { '\u25B6', "→" },   // U+25B6 black right triangle
        { '\u25B8', "→" },   // U+25B8 small right triangle
        { '\u25BA', "→" },   // U+25BA right pointer
        { '\u25BC', "↓" },   // U+25BC black down triangle
        { '\u2014', "―" },   // U+2014 em dash
        { '\u2010', "-" },   // U+2010 hyphen
        { '\uFF0D', "-" },   // U+FF0D fullwidth hyphen-minus
        { '\u226A', "<" },   // U+226A much-less-than
        { '\u226B', ">" },   // U+226B much-greater-than
        { '\u300C', "『" },   // U+300C left corner bracket
        { '\u300D', "』" },   // U+300D right corner bracket
        { '\u3010', "『" },   // U+3010 left lenticular bracket
        { '\u3011', "』" },   // U+3011 right lenticular bracket
    };

    /// <summary>UIに出す前に、フォントに無い記号を使える記号へ寄せる（無ければ落とす）。</summary>
    private string Fix(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            string rep;
            if (GlyphMap.TryGetValue(ch, out rep))
            {
                // 置換先すらフォントに無ければ捨てる
                if (uiFont == null || rep.Length == 0 || uiFont.HasCharacter(rep[0])) sb.Append(rep);
                continue;
            }
            // 記号帯・絵文字(サロゲートペア)でフォントに無いものは落とす。かな/漢字/英数はそのまま。
            if (char.IsHighSurrogate(ch)) { i++; continue; }                       // 絵文字は丸ごと除去
            if (ch >= 0x2000 && ch <= 0x2BFF && uiFont != null && !uiFont.HasCharacter(ch)) continue;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>TMPテキストの差し替え（サニタイズ込み）。`.text =` の代わりにこれを使う。</summary>
    private void SetTxt(TextMeshProUGUI t, string s) { if (t != null) t.text = Fix(s); }

    private TextMeshProUGUI Text(Transform parent, string txt, float size, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var rt = NewRect("Text", parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.font = uiFont; t.richText = true; t.enableWordWrapping = true; t.overflowMode = TextOverflowModes.Overflow;
        SetTxt(t, Fix(txt));
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
    // 🔧 選択中ツールのハイライト管理（mode → チップ）
    private readonly List<(Image img, int mode)> toolChips = new List<(Image, int)>();
    private int activeToolMode = -1;
    private void SetActiveTool(int mode)
    {
        activeToolMode = mode;
        foreach (var t in toolChips)
        {
            bool on = t.mode == mode;
            t.img.color = on ? SEL : CARD;
            var o = t.img.GetComponent<Outline>();
            if (o != null) { o.effectColor = on ? GOLD : LINE; o.effectDistance = on ? new Vector2(2, -2) : new Vector2(1, -1); }
        }
    }

    // ツールボタン（mode>=0 でハイライト対象／tip でツールチップ）
    private void ToolButton(Graphic bar, string label, Color accent, UnityAction onClick, int mode = -1, string tip = null)
    {
        var img = Panel(bar, "Tool_" + label, CARD); SizeElem(img.gameObject, 108, 40); Outline(img, LINE);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
        if (mode >= 0)
        {
            toolChips.Add((img, mode));
            int m = mode;
            btn.onClick.AddListener(() => SetActiveTool(m));
        }
        if (!string.IsNullOrEmpty(tip)) AddTooltip(img.gameObject, tip);
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
    // Transform(RectTransform)を親に取れるオーバーロード
    private Button PrimaryButton(Transform parent, string label, Color bg, Color fg, UnityAction onClick, bool red = false)
    {
        var img = Panel(parent, "Primary_" + label, bg);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
        var cb = btn.colors; cb.highlightedColor = Color.Lerp(bg, Color.white, 0.12f); cb.pressedColor = Color.Lerp(bg, Color.black, 0.12f);
        cb.disabledColor = Color.Lerp(bg, Color.gray, 0.5f); btn.colors = cb;
        SkinButton(btn, img, red);
        var t = Text(img.rectTransform, label, 14.5f, fg, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(t.rectTransform);
        return btn;
    }

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
