using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 🧰 画面に依存しないUI部品の道具箱（Phase B-6）。
///
/// **なぜ要るか**：これらの部品は `GameUIManager` の中に閉じ込められていたので、
/// 他のスクリプト（結果表示・盤の吹き出しなど）が同じ見た目を作りたくても呼べず、
/// その都度 `new GameObject` から書き直すことになっていた。ここへ出せば誰でも使える。
///
/// **使い方**：`Configure` をUIの組み立て前に1回だけ呼ぶ（フォント・スキン・パレットを渡す）。
/// 呼ばなくても既定値で動くが、日本語フォントとBloodlines枠は当たらない。
/// ⚠ 値は [[UITheme]] の規則に従うこと。ここで `C("#xxxxxx")` を増やさない。
/// 関連: [[GameUIManager]]（`GameUIManager.Kit.cs` から転送している）。
/// </summary>
public static class UIKit
{
    // ============ 設定（GameUIManager.LoadSkin から1回だけ渡す） ============
    public static TMP_FontAsset Font;
    public static Sprite Frame;                                               // 9スライスの大枠
    public static Sprite BtnGray, BtnGrayHover, BtnGrayPressed, BtnGrayDisabled;
    public static Sprite BtnRed, BtnRedHover, BtnRedPressed, BtnRedDisabled;

    // 既定値は GameUIManager が持っていたパレットと同じ（設定前でも見た目が変わらないように）
    public static Color Card  = UITheme.C("#14121d");
    public static Color Line  = UITheme.C("#332e49");
    public static Color Line2 = UITheme.C("#4a4268");
    public static Color Text  = UITheme.C("#ece8f5");
    public static Color Muted = UITheme.C("#9c95b4");
    public static Color Blood = UITheme.C("#b0202b");
    public static Color HudBg = UITheme.C("#0e0a0c");

    public static void Configure(TMP_FontAsset font, Sprite frame,
        Sprite btnGray, Sprite btnGrayHover, Sprite btnGrayPressed, Sprite btnGrayDisabled,
        Sprite btnRed, Sprite btnRedHover, Sprite btnRedPressed, Sprite btnRedDisabled)
    {
        Font = font; Frame = frame;
        BtnGray = btnGray; BtnGrayHover = btnGrayHover; BtnGrayPressed = btnGrayPressed; BtnGrayDisabled = btnGrayDisabled;
        BtnRed = btnRed; BtnRedHover = btnRedHover; BtnRedPressed = btnRedPressed; BtnRedDisabled = btnRedDisabled;
    }

    public static void SetPalette(Color card, Color line, Color line2, Color text, Color muted, Color blood, Color hudBg)
    { Card = card; Line = line; Line2 = line2; Text = text; Muted = muted; Blood = blood; HudBg = hudBg; }

    /// <summary>スプライトモードが Multiple の素材があるので `LoadAll` の先頭を取る（`Load&lt;Sprite&gt;` だと null になる）。</summary>
    public static Sprite LoadUISprite(string name)
    {
        var all = Resources.LoadAll<Sprite>("UI/" + name);
        return (all != null && all.Length > 0) ? all[0] : null;
    }

    // ================= 土台 =================
    public static RectTransform MakeCanvas(string name, int order)
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

    // ================= 素の生成 =================
    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    public static Image Panel(Transform parent, string name, Color color)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    // Imageを親に取るオーバーロード（Panelが返すImageをそのまま親にできる）
    public static Image Panel(Graphic parent, string name, Color color) => Panel(parent.transform, name, color);

    // ============ 🈶 グリフのサニタイズ ============
    // UIフォントに無い記号は **□ になる**（同梱フォントでも絵文字などは持っていない）。
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
        // ⚠ ローマ数字は必ず ASCII に寄せる。フォントで落ちると
        //   「配下進化 I/II/III 開放」が 3 つとも同じ名前になる（実際になっていた）。
        { '\u2160', "I" },    { '\u2161', "II" },   { '\u2162', "III" },
        { '\u2163', "IV" },   { '\u2164', "V" },    { '\u2165', "VI" },
        { '\u2166', "VII" },  { '\u2167', "VIII" }, { '\u2168', "IX" },  { '\u2169', "X" },
    };

    /// <summary>
    /// この文字をこのフォントで出せるか。
    /// ⚠ **`HasCharacter(ch)` の1引数版を使ってはいけない**。同梱フォントは動的アトラスなので、
    ///   1引数版は「まだアトラスに焼かれていない」だけの字にも false を返す。
    ///   その結果 → や ― や ◆ が**フォントに入っているのに全部消えていた**
    ///   （『基本形→進化形』が『基本形進化形』になっていた）。
    ///   `tryAddCharacter:true` にすると、出せる字はその場でアトラスへ足して true を返す。
    /// </summary>
    private static bool Has(char ch)
        => Font != null && Font.HasCharacter(ch, true, true);

    /// <summary>
    /// `**強調**` を `&lt;b&gt;` に直す。
    /// ⚠ このコードベースはコメントで `**` を使う癖があり、**画面に出す文字列にもそのまま混ざる**
    ///   （研究ノードの説明に4件、他2件あった。画面には `**街区**` と生で出ていた）。
    ///   書き手を直すより、出口で1回変換するほうが漏れない。
    /// 閉じ忘れは自分で閉じる（`&lt;b&gt;` が開きっぱなしだと**その先の文が全部太字になる**）。
    /// </summary>
    private static string Bold(string s)
    {
        if (s.IndexOf("**", System.StringComparison.Ordinal) < 0) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        bool open = false;
        for (int i = 0; i < s.Length; )
        {
            if (i + 1 < s.Length && s[i] == '*' && s[i + 1] == '*')
            { sb.Append(open ? "</b>" : "<b>"); open = !open; i += 2; continue; }
            sb.Append(s[i]); i++;
        }
        if (open) sb.Append("</b>");
        return sb.ToString();
    }

    /// <summary>UIに出す前に、フォントに無い記号を使える記号へ寄せる（無ければ落とす）。</summary>
    public static string Fix(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = Bold(s);
        var sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            string rep;
            if (GlyphMap.TryGetValue(ch, out rep))
            {
                // 置換先すらフォントに無ければ捨てる
                if (Font == null || rep.Length == 0 || Has(rep[0])) sb.Append(rep);
                continue;
            }
            // 記号帯・絵文字(サロゲートペア)でフォントに無いものは落とす。かな/漢字/英数はそのまま。
            if (char.IsHighSurrogate(ch)) { i++; continue; }                       // 絵文字は丸ごと除去
            if (ch >= 0x2000 && ch <= 0x2BFF && Font != null && !Has(ch)) continue;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>TMPテキストの差し替え（サニタイズ込み）。`.text =` の代わりにこれを使う。</summary>
    public static void SetTxt(TextMeshProUGUI t, string s) { if (t != null) t.text = Fix(s); }

    public static TextMeshProUGUI Label(Transform parent, string txt, float size, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var rt = NewRect("Text", parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.font = Font; t.richText = true; t.enableWordWrapping = true; t.overflowMode = TextOverflowModes.Overflow;
        SetTxt(t, Fix(txt));
        return t;
    }

    public static TextMeshProUGUI Label(Graphic parent, string txt, float size, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        => Label(parent.transform, txt, size, color, align, style);

    // ================= 配置 =================
    public static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
    { rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot; }

    public static void Anchor(Graphic g, Vector2 min, Vector2 max, Vector2 pivot) => Anchor(g.rectTransform, min, max, pivot);

    /// <summary>パネル内で左上原点の絶対配置。</summary>
    public static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
    }

    public static void SizeElem(GameObject go, float w, float h)
    {
        var le = go.GetComponent<LayoutElement>(); if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = w; le.preferredHeight = h; le.minWidth = w; le.minHeight = h;
    }

    public static void Spacer(Transform parent)
    {
        var rt = NewRect("Spacer", parent);
        var le = rt.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1;
    }

    public static void Spacer(Graphic parent) => Spacer(parent.transform);

    public static void StretchFull(RectTransform rt)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }

    public static void StretchOffset(RectTransform rt, float l, float t, float r, float b)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t); }

    // ================= 装飾 =================
    // ⚠ メソッド名を `Outline` にしない。UnityEngine.UI.Outline（型）と同じ名前になり、
    //   同じクラスの中で `AddComponent<Outline>()` を書いたときに読み手が混乱する。
    public static void AddOutline(Graphic g, Color col)
    {
        var o = g.gameObject.AddComponent<Outline>();
        o.effectColor = col; o.effectDistance = new Vector2(1, -1); o.useGraphicAlpha = false;
    }

    public static void Round(Image img, float _ = 12) { /* スプライト無しのため角丸は省略（色面で表現）*/ }

    public static void AddBottomBorder(Image bar)
    {
        var b = Panel(bar.rectTransform, "border", Blood);
        Anchor(b, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        b.rectTransform.sizeDelta = new Vector2(0, 2); b.rectTransform.anchoredPosition = Vector2.zero;
    }

    public static void AddTopBorder(Image bar)
    {
        var b = Panel(bar.rectTransform, "border", Blood);
        Anchor(b, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        b.rectTransform.sizeDelta = new Vector2(0, 2); b.rectTransform.anchoredPosition = Vector2.zero;
    }

    public static void LineRect(RectTransform parent, float x, float y, float w, float h)
    {
        var img = Panel(parent, "Line", Line2); img.raycastTarget = false;
        Place(img.rectTransform, x, y, w, h);
    }

    /// <summary>色つきの線（ツリーの接続線で「済み」と「合流」を描き分けるため）。</summary>
    public static void LineRect(RectTransform parent, float x, float y, float w, float h, Color color)
    {
        var img = Panel(parent, "Line", color); img.raycastTarget = false;
        Place(img.rectTransform, x, y, w, h);
    }

    // ================= 部品 =================
    /// <summary>タイプカード（見出し＋説明の四角いボタン）。</summary>
    public static Image CardBox(Graphic panel, float x, float y, float w, float h, string name, string desc, UnityAction onClick)
    {
        var img = Panel(panel, "Card_" + name, Card);
        Place(img.rectTransform, x, y, w, h); AddOutline(img, Line);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(() => SoundSystem.Play(SoundSystem.Sfx.Click));   // 🔊 押した手応え（全ボタン共通）
        btn.onClick.AddListener(onClick);
        var n = Label(img.rectTransform, name, 13, Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(n.rectTransform, 10, 7, w - 16, 18);
        var d = Label(img.rectTransform, desc, 10.5f, Muted, TextAlignmentOptions.TopLeft);
        Place(d.rectTransform, 10, 27, w - 16, 16);
        return img;
    }

    /// <summary>チップ（色の点＋短い名前）。</summary>
    public static Image ChipBox(Graphic panel, float x, float y, float w, float h, string name, Color accent, UnityAction onClick)
    {
        var img = Panel(panel, "Chip_" + name, Card);
        Place(img.rectTransform, x, y, w, h); AddOutline(img, Line);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(() => SoundSystem.Play(SoundSystem.Sfx.Click));   // 🔊 押した手応え（全ボタン共通）
        btn.onClick.AddListener(onClick);
        var dot = Panel(img.rectTransform, "dot", accent); Place(dot.rectTransform, 9, (h - 11) / 2f, 11, 11);
        var n = Label(img.rectTransform, name, 12, Text, TextAlignmentOptions.Left);
        Place(n.rectTransform, 26, (h - 16) / 2f, w - 30, 16);
        return img;
    }

    /// <summary>主要ボタン（生成/侵略）。red=trueで血の赤ボタン、既定は灰ボタン。スプライト未割当ならフラット色。</summary>
    public static Button PrimaryButton(Transform parent, string label, Color bg, Color fg, UnityAction onClick, bool red = false)
    {
        var img = Panel(parent, "Primary_" + label, bg);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(() => SoundSystem.Play(SoundSystem.Sfx.Click));   // 🔊 押した手応え（全ボタン共通）
        btn.onClick.AddListener(onClick);
        var cb = btn.colors; cb.highlightedColor = Color.Lerp(bg, Color.white, 0.12f); cb.pressedColor = Color.Lerp(bg, Color.black, 0.12f);
        cb.disabledColor = Color.Lerp(bg, Color.gray, 0.5f); btn.colors = cb;
        SkinButton(btn, img, red); // 🩸 Bloodlinesボタンへ（割当済のときだけ）
        var t = Label(img.rectTransform, label, 14.5f, fg, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(t.rectTransform);
        return btn;
    }

    public static Button PrimaryButton(Graphic parent, string label, Color bg, Color fg, UnityAction onClick, bool red = false)
        => PrimaryButton(parent.transform, label, bg, fg, onClick, red);

    // 🩸 9スライス枠スプライトを適用（未割当なら何もしない＝フラット色のまま）
    public static void ApplyFrame(Image img, Sprite s, Color tint)
    {
        if (img == null || s == null) return;
        img.sprite = s; img.type = Image.Type.Sliced; img.color = tint;
        var o = img.GetComponent<Outline>(); if (o != null) o.enabled = false; // スプライト枠を使うのでOutlineは無効化
    }

    // 🩸 パネルをBloodlinesの装飾フレームでスキン（不透明の暗い下地＋フレーム重ね）。
    //     フレームは最背面の子として敷くので、以降に追加される中身は枠の上に描かれる。
    public static void SkinPanel(Image panel)
    {
        if (panel == null || Frame == null) return;
        panel.color = HudBg; // 不透明の暗い下地（中央が透ける枠でも背景が黒に）
        var o = panel.GetComponent<Outline>(); if (o != null) o.enabled = false;
        var frame = Panel(panel.rectTransform, "Frame", Color.white);
        StretchFull(frame.rectTransform);
        frame.sprite = Frame; frame.type = Image.Type.Sliced; frame.raycastTarget = false;
        frame.rectTransform.SetAsFirstSibling(); // 中身より背面へ
    }

    // 🩸 BloodlinesボタンスプライトをSpriteSwapで適用（未割当ならフラット色のまま）
    public static void SkinButton(Button btn, Image img, bool red)
    {
        Sprite def = red ? BtnRed : BtnGray;
        if (def == null || img == null) return;
        img.sprite = def; img.type = Image.Type.Sliced; img.color = Color.white;
        var o = img.GetComponent<Outline>(); if (o != null) o.enabled = false;
        btn.transition = Selectable.Transition.SpriteSwap;
        var ss = btn.spriteState;
        ss.highlightedSprite = (red ? BtnRedHover : BtnGrayHover) ?? def;
        ss.pressedSprite = (red ? BtnRedPressed : BtnGrayPressed) ?? def;
        ss.selectedSprite = (red ? BtnRedHover : BtnGrayHover) ?? def;
        ss.disabledSprite = (red ? BtnRedDisabled : BtnGrayDisabled) ?? def;
        btn.spriteState = ss;
    }

    // ================= スクロール =================
    /// <summary>縦スクロール領域を作り、中身を入れる Content を返す。既存UI基盤にScrollRectが無いのでここで組む。</summary>
    public static RectTransform MakeVScroll(Image parent, float x, float y, float w, float h)
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

    /// <summary>横スクロール領域。項目数が所持数で伸びるストリップ（ボス任命など）が画面外に見切れないようにする。
    /// Content は縦ストレッチにして、幅だけコードで指定する（MakeVScroll と対称）。
    /// ⚠ Content は横ストレッチではないので、中身の配置に `rect.width` を当てにしないこと。</summary>
    public static RectTransform MakeHScroll(Image parent, float x, float y, float w, float h)
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

    /// <summary>
    /// 縦横どちらにも動かせるスクロール領域（Civの技術ツリーのように**盤をパンする**もの向け）。
    ///
    /// ⚠ <b>なぜ要るか</b>：研究ツリーは実測で 2,880×4,000px あり、窓は 1,768px しかない。
    ///   `MakeVScroll` は横に動かないので、**tier5以降の列が丸ごと見切れて存在しないのと同じ**になっていた。
    ///   縦だけのスクロールに、横に伸びる中身を入れてはいけない。
    ///
    /// ⚠ Content は**左上固定でストレッチしない**。中身を置く前に必ず
    ///   `content.sizeDelta = new Vector2(実際の幅, 実際の高さ)` を入れること
    ///   （`rect.width` は当てにできない。→ [[ui-conventions]]）。
    /// </summary>
    public static RectTransform MakeScroll2D(Image parent, float x, float y, float w, float h)
    {
        var view = Panel(parent, "Viewport", new Color(0f, 0f, 0f, 0.001f));
        Place(view.rectTransform, x, y, w, h);
        view.gameObject.AddComponent<RectMask2D>();
        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = true; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 34f;
        var content = NewRect("Content", view.rectTransform);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(0f, 1f); content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(w, h);
        sr.viewport = view.rectTransform;
        sr.content = content;
        return content;
    }

    // ================= アイコン =================
    // 🖼️ Turbo Diskアイコン読込（キャッシュ）。無ければnull。
    private static readonly Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();

    public static Sprite Icon(string name)
    {
        Sprite s;
        if (_iconCache.TryGetValue(name, out s)) return s;
        s = Resources.Load<Sprite>("Icons/" + name);
        _iconCache[name] = s; return s;
    }

    public static Image IconImg(Transform parent, string iconName, float x, float y, float size, Color tint)
    {
        var spr = Icon(iconName);
        var img = Panel(parent, "Icon_" + iconName, spr != null ? tint : new Color(0, 0, 0, 0));
        if (spr != null) { img.sprite = spr; img.type = Image.Type.Simple; img.preserveAspect = true; }
        Place(img.rectTransform, x, y, size, size);
        img.raycastTarget = false;
        return img;
    }
}
