using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// UIの共通部品と土台（フォント／スキン／Canvas／スクロール／ツールチップ／アイコン／フェード）。
/// 画面に依存しない道具だけを置く。個別画面の組み立てはここに書かないこと。
/// <para>`GameUIManager` の partial。フィールドの本体は GameUIManager.cs 側にある。</para>
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

    /// <summary>スプライトモードが Multiple の素材があるので `LoadAll` の先頭を取る（`Load&lt;Sprite&gt;` だと null になる）。</summary>
    private static Sprite LoadUISprite(string name)
    {
        var all = Resources.LoadAll<Sprite>("UI/" + name);
        return (all != null && all.Length > 0) ? all[0] : null;
    }

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

    private RectTransform MakeCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var cv = go.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = order;
        var sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go.GetComponent<RectTransform>();
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

    private void LineRect(RectTransform parent, float x, float y, float w, float h)
    {
        var img = Panel(parent, "Line", LINE2); img.raycastTarget = false;
        Place(img.rectTransform, x, y, w, h);
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
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(() => SoundSystem.Play(SoundSystem.Sfx.Click));   // 🔊 押した手応え（全ボタン共通）
        btn.onClick.AddListener(onClick);
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
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(() => SoundSystem.Play(SoundSystem.Sfx.Click));   // 🔊 押した手応え（全ボタン共通）
        btn.onClick.AddListener(onClick);
        var dot = Panel(img.rectTransform, "dot", accent); Place(dot.rectTransform, 9, (h - 11) / 2f, 11, 11);
        var n = Text(img.rectTransform, name, 12, TEXT, TextAlignmentOptions.Left);
        Place(n.rectTransform, 26, (h - 16) / 2f, w - 30, 16);
        return img;
    }

    // 主要ボタン（生成/侵略）。red=trueで血の赤ボタン、既定は灰ボタン。スプライト未割当ならフラット色。
    // Transform(RectTransform)を親に取れるオーバーロード
    private Button PrimaryButton(Transform parent, string label, Color bg, Color fg, UnityAction onClick, bool red = false)
    {
        var img = Panel(parent, "Primary_" + label, bg);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(() => SoundSystem.Play(SoundSystem.Sfx.Click));   // 🔊 押した手応え（全ボタン共通）
        btn.onClick.AddListener(onClick);
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
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(() => SoundSystem.Play(SoundSystem.Sfx.Click));   // 🔊 押した手応え（全ボタン共通）
        btn.onClick.AddListener(onClick);
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
