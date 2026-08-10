using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// UIの土台（フォント／スキン／Canvas／ツールチップ／フェード／カウントアップ）と、
/// [[UIKit]] への転送。
///
/// **なぜ転送を残すか**：素の部品は `UIKit` へ出したが、呼び出し側は200箇所以上ある。
/// 全部を `UIKit.` 付きに書き換えると差分が巨大になり、移動と書き換えが混ざって
/// 事故が見えなくなる。ここに1行の転送を置けば、**呼び出し側は1行も変えずに済む**。
/// 新しく書くコードは `UIKit.` を直接呼んでよい。
///
/// ⚠ ここに残っているものは**アプリの状態を持つ**もの（開いているパネル、
///   ツールチップの実体、カウントアップの途中値）。状態を持たない部品は `UIKit` へ。
/// <para>`GameUIManager` の partial。</para>
/// </summary>
public partial class GameUIManager
{
    private TMP_FontAsset uiFont;

    // ===== 🩸 Bloodlines スキン =====
    // ⚠ 以前は `[SerializeField]` でインスペクタ割当を待っていたが、**誰も割り当てていなかった**ので
    //    `SkinPanel`/`SkinButton` は18箇所で呼ばれながら**全部素通り**していた（＝フラット色のまま）。
    //    このプロジェクトの他の素材と同じく **Resources から自分で読む**方式にして、確実に効かせる。
    [Header("Bloodlines Skin（未指定ならResourcesから自動で読む）")]
    [SerializeField] private Sprite skinFrame;   // 大枠(9スライス)：側面パネル用
    [SerializeField] private Sprite skinBar;     // HUD帯/小枠(9スライス)
    [SerializeField] private Sprite btnGray, btnGrayHover, btnGrayPressed, btnGrayDisabled;
    [SerializeField] private Sprite btnRed, btnRedHover, btnRedPressed, btnRedDisabled;
    [SerializeField] private Sprite barFill, barTrack;

    private void LoadSkin()
    {
        if (skinFrame == null) skinFrame = LoadUISprite("Frame_outline");
        if (skinBar == null) skinBar = LoadUISprite("Frame_outline");
        if (btnGray == null) btnGray = LoadUISprite("Btn_Grey");
        if (btnGrayHover == null) btnGrayHover = LoadUISprite("Btn_GreyHover");
        if (btnGrayPressed == null) btnGrayPressed = LoadUISprite("Btn_Pressed");
        if (btnGrayDisabled == null) btnGrayDisabled = LoadUISprite("Btn_Disabled");
        if (btnRed == null) btnRed = LoadUISprite("Btn_Red");
        if (btnRedHover == null) btnRedHover = LoadUISprite("Btn_RedHover");
        if (btnRedPressed == null) btnRedPressed = LoadUISprite("Btn_Pressed");
        if (btnRedDisabled == null) btnRedDisabled = LoadUISprite("Btn_Disabled");
        Debug.Log("🩸『スキン』枠=" + (skinFrame != null) + " ボタン=" + (btnGray != null));
    }

    /// <summary>[[UIKit]] に、このマネージャが使っているフォント・スキン・パレットを渡す。
    /// ⚠ **UIを組む前に呼ぶ**。組んだ後だと当たらない（スキンで一度踏んだ）。</summary>
    private void ConfigureKit()
    {
        UIKit.Configure(uiFont, skinFrame,
            btnGray, btnGrayHover, btnGrayPressed, btnGrayDisabled,
            btnRed, btnRedHover, btnRedPressed, btnRedDisabled);
        UIKit.SetPalette(CARD, LINE, LINE2, TEXT, MUTED, BLOOD, HUD_BG);
    }

    /// <summary>
    /// 🈶 UIの日本語フォント。
    ///
    /// **まずプロジェクトが持っている Noto Sans JP を使う**（Phase B の宿題）。
    /// 以前はOSのフォント（Yu Gothic UI など）から動的に作っていたが、それだと
    /// **配った先のPCで別の字になる／そもそも入っていない**という、製品として通らない状態だった。
    /// ⚠ 日本語は7,000字を超えるので**静的アトラスにしない**（巨大になる）。
    ///    `AtlasPopulationMode.Dynamic` で、実際に使った字だけアトラスへ足す。
    /// OSフォントは**もう見つからないとき用の保険**として残してある。
    /// ライセンス: SIL OFL（`Assets/Fonts/OFL.txt`）。
    /// </summary>
    private TMP_FontAsset FindUIFont()
    {
        var own = Resources.Load<TMP_FontAsset>("Fonts/NotoSansJP-Regular SDF");
        if (own != null)
        {
            own.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            Debug.Log("🈶『UIフォント』Noto Sans JP（同梱）を使用");
            return own;
        }
        Debug.LogWarning("🈶 同梱フォントが見つからないのでOSのフォントへ退避する");
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

    private Canvas dungeonCanvas;

    // 👑🪤🛡️ 配置系ストリップ（部隊/ボス/罠）は選択ツールに応じて1つだけ表示する。
    /// <summary>
    /// 🗂️ 全画面パネルは**1枚だけ開く**。開くときに他を畳む。
    /// ※以前は各ボタンが自分のパネルをトグルするだけだったので、裏に開きっぱなしのパネルが積もり、
    ///   いちいち元のタブへ戻って閉じる必要があった。
    /// </summary>
    /// <summary>
    /// ✨ 開いた瞬間だけ薄く→濃く（Phase B）。**開閉が一瞬で切り替わると安っぽく見える**ので、
    /// 0.14秒だけかける。⚠ `unscaledDeltaTime` で動かす（戦闘の倍速/一時停止に引きずられないため）。
    /// </summary>
    private readonly List<CanvasGroup> fadingIn = new List<CanvasGroup>();
    private void PlayFadeIn(GameObject go)
    {
        if (go == null) return;
        var cg = go.GetComponent<CanvasGroup>(); if (cg == null) cg = go.AddComponent<CanvasGroup>();
        // ⚠ 0 から始めない。**フェードが進まなかったときにパネルが透明のまま残る**（実際に踏んだ）。
        //    0.25 から始めれば、最悪でも「薄いが見えている」で済む。
        cg.alpha = 0.25f;
        if (!fadingIn.Contains(cg)) fadingIn.Add(cg);
    }
    private void TickFades()
    {
        for (int i = fadingIn.Count - 1; i >= 0; i--)
        {
            var cg = fadingIn[i];
            if (cg == null) { fadingIn.RemoveAt(i); continue; }
            // ⚠ unscaledDeltaTime が 0 を返す状況がある（エディタが描画を進めていないときなど）。
            //    そのままだと**永久に薄いまま**なので、最低でも1フレームぶんは進める。
            float step = Mathf.Max(Time.unscaledDeltaTime, 1f / 120f);
            cg.alpha = Mathf.Min(1f, cg.alpha + step / UITheme.FadeIn);
            if (cg.alpha >= 1f) fadingIn.RemoveAt(i);
        }
    }

    private void OpenExclusive(GameObject panel)
    {
        var all = new GameObject[] { demonPanel, emotionPanel, relicPanel, researchPanel, expandPanel, minionPanel };
        bool open = panel != null && !panel.activeSelf;
        foreach (var g in all) if (g != null && g != panel) g.SetActive(false);
        if (panel != null)
        {
            panel.SetActive(open);
            if (open) { panel.transform.SetAsLastSibling(); PlayFadeIn(panel); }
        }
        dlSig = null; emoSig = null;   // 署名を無効化して次のUpdateで作り直させる
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

    // 💰 数値はいきなり書き換えず**カウントアップ**する（Phase B）。増えた実感が出る。
    private readonly Dictionary<TextMeshProUGUI, float> shownValues = new Dictionary<TextMeshProUGUI, float>();
    private void SetNumber(TextMeshProUGUI t, int target)
    {
        if (t == null) return;
        float cur;
        if (!shownValues.TryGetValue(t, out cur)) cur = target;      // 初回は即決め（開幕に0から数え上げない）
        if (Mathf.Abs(cur - target) < 0.5f) cur = target;
        else cur = Mathf.MoveTowards(cur, target, Mathf.Max(1f, Mathf.Abs(target - cur)) / UITheme.CountUp * Time.unscaledDeltaTime);
        shownValues[t] = cur;
        SetTxt(t, UITheme.Num(Mathf.RoundToInt(cur)));
    }

    private void SetSel(Image img, bool on)
    {
        if (img == null) return;
        img.color = on ? SEL : CARD;
        var outline = img.GetComponent<Outline>();
        if (outline != null) outline.effectColor = on ? GOLD : LINE;
    }

    // ================= [[UIKit]] への転送 =================
    // 中身は UIKit にある。ここは呼び出し側を書き換えずに済ませるための1行だけ。
    private static Sprite LoadUISprite(string name) => UIKit.LoadUISprite(name);
    private RectTransform MakeCanvas(string name, int order) => UIKit.MakeCanvas(name, order);
    private RectTransform NewRect(string name, Transform parent) => UIKit.NewRect(name, parent);
    private Image Panel(Transform parent, string name, Color color) => UIKit.Panel(parent, name, color);
    private Image Panel(Graphic parent, string name, Color color) => UIKit.Panel(parent, name, color);
    private TextMeshProUGUI Text(Transform parent, string txt, float size, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        => UIKit.Label(parent, txt, size, color, align, style);
    private TextMeshProUGUI Text(Graphic parent, string txt, float size, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        => UIKit.Label(parent, txt, size, color, align, style);
    private string Fix(string s) => UIKit.Fix(s);
    private void SetTxt(TextMeshProUGUI t, string s) => UIKit.SetTxt(t, s);
    private void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot) => UIKit.Anchor(rt, min, max, pivot);
    private void Anchor(Graphic g, Vector2 min, Vector2 max, Vector2 pivot) => UIKit.Anchor(g, min, max, pivot);
    private void Place(RectTransform rt, float x, float y, float w, float h) => UIKit.Place(rt, x, y, w, h);
    private void SizeElem(GameObject go, float w, float h) => UIKit.SizeElem(go, w, h);
    private void Spacer(Transform parent) => UIKit.Spacer(parent);
    private void Spacer(Graphic parent) => UIKit.Spacer(parent);
    private void Outline(Graphic g, Color col) => UIKit.AddOutline(g, col);
    private void Round(Image img, float r = 12) => UIKit.Round(img, r);
    private void AddBottomBorder(Image bar) => UIKit.AddBottomBorder(bar);
    private void AddTopBorder(Image bar) => UIKit.AddTopBorder(bar);
    private void LineRect(RectTransform parent, float x, float y, float w, float h) => UIKit.LineRect(parent, x, y, w, h);
    private void LineRect(RectTransform parent, float x, float y, float w, float h, Color color) => UIKit.LineRect(parent, x, y, w, h, color);
    private Image Card(Graphic panel, float x, float y, float w, float h, string name, string desc, UnityAction onClick)
        => UIKit.CardBox(panel, x, y, w, h, name, desc, onClick);
    private Image Chip(Graphic panel, float x, float y, float w, float h, string name, Color accent, UnityAction onClick)
        => UIKit.ChipBox(panel, x, y, w, h, name, accent, onClick);
    private Button PrimaryButton(Transform parent, string label, Color bg, Color fg, UnityAction onClick, bool red = false)
        => UIKit.PrimaryButton(parent, label, bg, fg, onClick, red);
    private Button PrimaryButton(Graphic parent, string label, Color bg, Color fg, UnityAction onClick, bool red = false)
        => UIKit.PrimaryButton(parent, label, bg, fg, onClick, red);
    private void ApplyFrame(Image img, Sprite s, Color tint) => UIKit.ApplyFrame(img, s, tint);
    private void SkinPanel(Image panel) => UIKit.SkinPanel(panel);
    private void SkinButton(Button btn, Image img, bool red) => UIKit.SkinButton(btn, img, red);
    private void StretchFull(RectTransform rt) => UIKit.StretchFull(rt);
    private void StretchOffset(RectTransform rt, float l, float t, float r, float b) => UIKit.StretchOffset(rt, l, t, r, b);
    private RectTransform MakeVScroll(Image parent, float x, float y, float w, float h) => UIKit.MakeVScroll(parent, x, y, w, h);
    private RectTransform MakeHScroll(Image parent, float x, float y, float w, float h) => UIKit.MakeHScroll(parent, x, y, w, h);
    private RectTransform MakeScroll2D(Image parent, float x, float y, float w, float h) => UIKit.MakeScroll2D(parent, x, y, w, h);
    private Sprite Icon(string name) => UIKit.Icon(name);
    private Image IconImg(Transform parent, string iconName, float x, float y, float size, Color tint)
        => UIKit.IconImg(parent, iconName, x, y, size, tint);
}
