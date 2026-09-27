using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// タイトル画面（開始／戦績／続きから／遊び方／世界設定）と新規開始。
/// <para>`GameUIManager` の partial。フィールドの本体は GameUIManager.cs 側にある。</para>
/// </summary>
public partial class GameUIManager
{

    // ================= 🎬 タイトル画面／世界設定 =================
    //  起動時はここで止め、『地上の広さ・宝箱の量・階層数・迷宮タイプ』を選んでから世界を作る。
    //  初期DPは **開始予算 − 初期迷宮の建造費**（GameSetup）。豪華に始めるほど手元が乏しくなる。
    private void BuildTitleScreen()
    {
        var tRoot = MakeCanvas("TitleCanvas", 300);
        titleRoot = tRoot.gameObject;
        titlePages[0] = BuildTitlePage(tRoot).gameObject;
        titlePages[1] = BuildSetupPage(tRoot).gameObject;
        titlePages[2] = BuildHelpPage(tRoot).gameObject;
        titlePages[3] = BuildLoadPage(tRoot).gameObject;
        titlePages[4] = BuildRecordPage(tRoot).gameObject;

        if (!showTitleOnStart) { titleRoot.SetActive(false); return; }
        if (dungeonCanvas != null) dungeonCanvas.enabled = false;   // 背後のHUDを止める
        if (GameSetup.Seed == 0) GameSetup.Seed = Random.Range(1, int.MaxValue);
        SoundSystem.PlayBgm(SoundSystem.Bgm.Prepare);   // 🔊 タイトルから曲を敷く
        ShowTitlePage(0);
    }

    private void ShowTitlePage(int page)
    {
        for (int i = 0; i < titlePages.Length; i++)
            if (titlePages[i] != null) titlePages[i].SetActive(i == page);
        if (page == 0) { RefreshTitleMenu(); if (titleFx != null) titleFx.Replay(); }
        if (page == 1) RefreshTitleSel();
        if (page == 3) FillSaveRows(titleLoadBody, SAVE_W, true);
        if (page == 4) RefreshRecordPage();
    }

    // ══ 🎬 タイトル（承認済みの画面案 `CnrWAB93`・案A）══
    //   ⚠⚠ 前は黒一色の中央に文字とボタンが並ぶだけで、**何のゲームか画面から伝わらなかった**
    //     （note にスクショを貼って気づいた、というユーザーの指摘）。
    //   絵を全面に敷き、左に題字と操作、下に「築く／侵す／統べる」の三本柱。動きは `TitleScreenFx`。
    private TitleScreenFx titleFx;
    private Image titleBgImg;
    private RectTransform titleMenuBox;
    private TextMeshProUGUI titleCorner;
    private int titleStaggerFixed = -1;   // 作り直さない部品（題字・戦績など）の数

    /// <summary>
    /// 🔤 題字の書体。`Resources/Fonts/TitleMincho SDF` があればそれを使い、無ければ本文の太字。
    /// ⚠ 明朝体（Shippori Mincho B1・OFL）の取り込みはユーザーの判断待ち。置けばそのまま差し替わる。
    /// </summary>
    private static TMP_FontAsset TitleFont()
    {
        var f = Resources.Load<TMP_FontAsset>("Fonts/TitleMincho SDF");
        return f;
    }

    /// <summary>横方向のα勾配（左が濃い）。⚠ 画像を足さずに暗幕を作るため、その場で焼く。</summary>
    private static Sprite ShadeSprite()
    {
        var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int x = 0; x < 256; x++)
        {
            float u = x / 255f, a;
            if (u < 0.43f) a = Mathf.Lerp(0.94f, 0.82f, u / 0.43f);
            else if (u < 0.78f) a = Mathf.Lerp(0.82f, 0.22f, (u - 0.43f) / 0.35f);
            else a = Mathf.Lerp(0.22f, 0f, (u - 0.78f) / 0.22f);
            tex.SetPixel(x, 0, new Color(0.03f, 0.024f, 0.047f, a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 256, 1), new Vector2(0.5f, 0.5f));
    }

    /// <summary>縦方向のα勾配（下が濃い）。</summary>
    private static Sprite FootShadeSprite()
    {
        var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 64; y++)
        {
            float u = y / 63f;   // 0＝下端
            tex.SetPixel(0, y, new Color(0.03f, 0.024f, 0.047f, Mathf.Lerp(0.9f, 0f, Mathf.SmoothStep(0f, 1f, u))));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
    }

    /// <summary>四隅を落とす暗がり（中心は素通し）。</summary>
    private static Sprite VignetteSprite()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x / (N - 1f) - 0.6f) / 0.62f, dy = (y / (N - 1f) - 0.52f) / 0.55f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((d - 0.6f) / 0.5f) * 0.72f;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
    }

    /// <summary>左から伸びて消える赤い光の帯（メニューの選択）。</summary>
    private static Sprite GlowSprite()
    {
        var tex = new Texture2D(128, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int x = 0; x < 128; x++)
            tex.SetPixel(x, 0, new Color(0.69f, 0.13f, 0.17f, Mathf.Lerp(0.34f, 0f, x / 127f)));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 128, 1), new Vector2(0.5f, 0.5f));
    }

    /// <summary>順に浮かぶ部品の入れ物。⚠ 入れ物は置いた場所に固定、動くのは中の子だけ。</summary>
    private RectTransform Stagger(RectTransform parent, string name, float x, float y, float w, float h, float at)
    {
        var box = NewRect(name, parent);
        Place(box, x, y, w, h);
        var cg = box.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        var inner = NewRect("In", box);
        Place(inner, 0, 0, w, h);
        if (titleFx != null) { titleFx.stagger.Add(cg); titleFx.staggerAt.Add(at); }
        return inner;
    }

    private Image BuildTitlePage(RectTransform root)
    {
        var page = Panel(root, "TitlePage", C("#07060a"));
        StretchFull(page.rectTransform);
        titleFx = page.gameObject.AddComponent<TitleScreenFx>();
        titleFx.blocked = () => settingsPanel != null && settingsPanel.activeSelf;

        // ── 背景（少し大きめに置いて、ゆっくり寄る・流れる）──
        var bgBox = NewRect("BgBox", page.rectTransform);
        StretchFull(bgBox);
        var mask = bgBox.gameObject.AddComponent<RectMask2D>();
        titleBgImg = Panel(bgBox, "Bg", Color.white);
        titleBgImg.raycastTarget = false;
        titleBgImg.preserveAspect = false;
        var bgRt = titleBgImg.rectTransform;
        bgRt.anchorMin = new Vector2(-0.03f, -0.03f); bgRt.anchorMax = new Vector2(1.03f, 1.03f);
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero; bgRt.pivot = new Vector2(0.5f, 0.5f);
        titleFx.bg = bgRt;
        ApplyTitleWallpaper();

        // 左の暗幕と四隅の暗がり（文字を読ませる）
        var shade = Panel(page, "Shade", Color.white);
        shade.sprite = ShadeSprite(); shade.raycastTarget = false;
        shade.rectTransform.anchorMin = new Vector2(0f, 0f); shade.rectTransform.anchorMax = new Vector2(0.72f, 1f);
        shade.rectTransform.offsetMin = Vector2.zero; shade.rectTransform.offsetMax = Vector2.zero;
        var vig = Panel(page, "Vignette", Color.white);
        vig.sprite = VignetteSprite(); vig.raycastTarget = false;
        StretchFull(vig.rectTransform);
        // 下端の暗がり（三本柱が絵の明るいところ＝結晶の光に重なって読めなかった）
        var foot = Panel(page, "FootShade", Color.white);
        foot.sprite = FootShadeSprite(); foot.raycastTarget = false;
        foot.rectTransform.anchorMin = new Vector2(0f, 0f); foot.rectTransform.anchorMax = new Vector2(1f, 0f);
        foot.rectTransform.pivot = new Vector2(0.5f, 0f);
        foot.rectTransform.sizeDelta = new Vector2(0f, 260f); foot.rectTransform.anchoredPosition = Vector2.zero;

        // 火の粉の層
        var em = NewRect("Embers", page.rectTransform);
        StretchFull(em);
        titleFx.emberLayer = em;

        var tf = TitleFont();
        var R = page.rectTransform;

        // ── 左：英字・題字・線・ひと言 ──
        float L = 120f;
        var e0 = Stagger(R, "Eyebrow", L, 176, 900, 24, 0.5f);
        var eyebrow = Text(e0, "CHRONICLE  OF  THE  LABYRINTH  LORD", 16, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        StretchFull(eyebrow.rectTransform); eyebrow.characterSpacing = 18; eyebrow.alpha = 0.85f;

        var e1 = Stagger(R, "Logo", L - 6, 204, 900, 150, 0.75f);
        var logo = Text(e1, "迷宮", 124, C("#ece6f4"), TextAlignmentOptions.Left, FontStyles.Bold);
        if (tf != null) logo.font = tf;
        logo.enableWordWrapping = false; logo.characterSpacing = 6;
        Place(logo.rectTransform, 0, 0, 280, 150);
        var accent = Text(e1, "統魔録", 124, C("#b0202b"), TextAlignmentOptions.Left, FontStyles.Bold);
        if (tf != null) accent.font = tf;
        accent.enableWordWrapping = false; accent.characterSpacing = 6;
        Place(accent.rectTransform, 262, 0, 460, 150);
        titleFx.logoAccent = accent;

        var e2 = Stagger(R, "Rule", L, 366, 260, 3, 1.1f);
        var rule = Panel(e2, "line", Color.white);
        rule.sprite = GlowSprite(); rule.color = new Color(1f, 1f, 1f, 1f);
        StretchFull(rule.rectTransform);
        var rule2 = Panel(e2, "line2", new Color(0.69f, 0.13f, 0.17f, 0.9f));
        Place(rule2.rectTransform, 0, 0, 120, 3);

        var e3 = Stagger(R, "Tag", L, 384, 900, 34, 1.2f);
        var tag = Text(e3, "迷宮を統べ、地上を侵す。", 25, C("#a79fbd"), TextAlignmentOptions.Left);
        StretchFull(tag.rectTransform); tag.characterSpacing = 8;

        // ── メニュー（セーブの有無で先頭が変わるので、出すたびに作り直す）──
        titleMenuBox = NewRect("Menu", R);
        Place(titleMenuBox, L, 452, 640, 420);

        // ── 右上：戦績の要約 ──
        var cBox = NewRect("Corner", R);
        Anchor(cBox, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        cBox.sizeDelta = new Vector2(620, 60); cBox.anchoredPosition = new Vector2(-40, -32);
        var cg0 = cBox.gameObject.AddComponent<CanvasGroup>(); cg0.alpha = 0f;
        titleFx.stagger.Add(cg0); titleFx.staggerAt.Add(2.0f);
        var cIn = NewRect("In", cBox); StretchFull(cIn);
        titleCorner = Text(cIn, "", 16, C("#a79fbd"), TextAlignmentOptions.TopRight);
        StretchFull(titleCorner.rectTransform);

        // ── 下：三本柱 ──
        var pBox = NewRect("Pillars", R);
        pBox.anchorMin = new Vector2(0f, 0f); pBox.anchorMax = new Vector2(1f, 0f); pBox.pivot = new Vector2(0.5f, 0f);
        pBox.offsetMin = new Vector2(L, 44); pBox.offsetMax = new Vector2(-L, 44 + 78);
        string[,] pl =
        {
            { "築く", "罠と魔物で迷宮を組み、押し寄せる冒険者を退ける" },
            { "侵す", "眷属を地上へ放ち、村を落として版図を広げる" },
            { "統べる", "四つの道の条件を満たし、仕上げの儀で世界を取る" },
        };
        for (int i = 0; i < 3; i++)
        {
            var col = NewRect("P" + i, pBox);
            col.anchorMin = new Vector2(i / 3f, 0f); col.anchorMax = new Vector2((i + 1) / 3f, 1f);
            col.offsetMin = new Vector2(i == 0 ? 0 : 14, 0); col.offsetMax = new Vector2(i == 2 ? 0 : -14, 0);
            var cg = col.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0f;
            titleFx.pillars.Add(cg);
            var ln = Panel(col, "Line", new Color(0.92f, 0.89f, 0.95f, 0.18f));
            ln.rectTransform.anchorMin = new Vector2(0f, 1f); ln.rectTransform.anchorMax = new Vector2(1f, 1f);
            ln.rectTransform.pivot = new Vector2(0.5f, 1f);
            ln.rectTransform.sizeDelta = new Vector2(0f, 2f); ln.rectTransform.anchoredPosition = Vector2.zero;
            titleFx.pillarLines.Add(ln);
            var h = Text(col, pl[i, 0], 27, C("#ece6f4"), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            if (tf != null) h.font = tf;
            h.characterSpacing = 14;
            Place(h.rectTransform, 0, 12, 400, 34);
            var d = Text(col, pl[i, 1], 17, C("#a79fbd"), TextAlignmentOptions.TopLeft);
            d.enableWordWrapping = true;
            Place(d.rectTransform, 0, 48, 520, 28);
        }

        // ── 右下：版 ──
        var ver = Text(R, "v" + Application.version + " · 2026.09", 13, C("#6f6889"), TextAlignmentOptions.BottomRight);
        Anchor(ver.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
        ver.rectTransform.sizeDelta = new Vector2(300, 20); ver.rectTransform.anchoredPosition = new Vector2(-40, 14);

        // ── 起動の黒幕（いちばん上）──
        var veil = Panel(page, "Veil", Color.black);
        StretchFull(veil.rectTransform); veil.raycastTarget = false;
        titleFx.veil = veil;

        RefreshTitleMenu();
        return page;
    }

    /// <summary>🖼️ 壁紙を差し替える（設定から呼ぶ）。</summary>
    private void ApplyTitleWallpaper()
    {
        if (titleBgImg == null) return;
        var sp = TitleWallpaper.SpriteOf(TitleWallpaper.Current);
        titleBgImg.sprite = sp;
        titleBgImg.color = sp != null ? Color.white : C("#0b0910");
    }

    /// <summary>
    /// 📜 メニューと戦績の要約を作り直す。⚠ タイトルへ戻るたびに呼ぶ（保存したあとで中身が変わる）。
    /// ⚠ **セーブがあれば「続きから」を先頭**に置き、直近のセーブの中身を添える
    ///   （前は押すまでどこまで遊んだか分からなかった）。無ければ「新しい世界を始める」が先頭。
    /// </summary>
    private void RefreshTitleMenu()
    {
        var box = titleMenuBox; if (box == null || titleFx == null) return;
        for (int i = box.childCount - 1; i >= 0; i--) { var g = box.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        titleFx.rows.Clear();
        // 前に作った行の出方の記録も捨てる（メニューの行は titleFx.stagger の**後ろ**に積まれている）
        // ⚠ Destroy はフレームの終わりまで効かないので「null になったものを消す」では拾えない。数で切る。
        if (titleStaggerFixed < 0) titleStaggerFixed = titleFx.stagger.Count;
        else if (titleFx.stagger.Count > titleStaggerFixed)
        {
            titleFx.stagger.RemoveRange(titleStaggerFixed, titleFx.stagger.Count - titleStaggerFixed);
            titleFx.staggerAt.RemoveRange(titleStaggerFixed, titleFx.staggerAt.Count - titleStaggerFixed);
        }

        // 直近のセーブ（保存日時の新しいもの）
        SaveSystem.Slot latest = new SaveSystem.Slot(); bool any = false;
        for (int s = 0; s <= SaveSystem.SlotCount; s++)
        {
            var info = SaveSystem.Peek(s);
            if (!info.exists) continue;
            if (!any || string.CompareOrdinal(info.savedAt, latest.savedAt) > 0) { latest = info; any = true; }
        }

        float y = 0f; int n = 0;
        if (any)
            y = TitleRow(box, y, n++, "続きから",
                "第 " + latest.turn + " ターン　" + latest.era + "　" + latest.floors + "層　<color=#6f6889>" + latest.savedAt + "</color>",
                2, () => ShowTitlePage(3));
        y = TitleRow(box, y, n++, "新しい世界を始める", null, any ? 1 : 2, () => ShowTitlePage(1));
        y = TitleRow(box, y, n++, "遊び方", null, 1, () => ShowTitlePage(2));
        y = TitleRow(box, y, n++, "戦績・実績", null, 1, () => ShowTitlePage(4));
        y = TitleRow(box, y, n++, "設定", null, 0, OpenSettings);
        y = TitleRow(box, y, n++, "終了", null, 0, QuitGame);
        titleFx.Select(0);

        if (titleCorner != null)
        {
            SetTxt(titleCorner, "通算 <b><color=#ece6f4>" + RunStats.Runs + "</color></b> 周　勝ち切った <b><color=#ece6f4>" + RunStats.Wins + "</color></b> 回"
                + "\n最速の勝利 <b><color=#ece6f4>" + (RunStats.BestTurn > 0 ? "T" + RunStats.BestTurn : "―") + "</color></b>"
                + "　実績 <b><color=#ece6f4>" + Achievements.UnlockedCount + " / " + Achievements.Count + "</color></b>");
        }
    }

    /// <summary>メニュー1行。weight：2＝主（金・大）／1＝ふつう／0＝控えめ。</summary>
    private float TitleRow(RectTransform box, float y, int index, string label, string sub, int weight, System.Action pick)
    {
        float h = sub != null ? 76f : weight == 0 ? 46f : 54f;
        float size = weight == 2 ? 34f : weight == 1 ? 29f : 24f;
        var wrap = NewRect("Row_" + index, box);
        Place(wrap, 0, y, 640, h);
        var cg = wrap.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0f;
        titleFx.stagger.Add(cg); titleFx.staggerAt.Add(1.4f + index * 0.1f);
        var inner = NewRect("In", wrap); Place(inner, 0, 0, 640, h);

        var hit = Panel(inner, "Hit", new Color(0, 0, 0, 0));
        StretchFull(hit.rectTransform);
        var glow = Panel(inner, "Glow", Color.white);
        glow.sprite = GlowSprite(); glow.raycastTarget = false; StretchFull(glow.rectTransform);
        var bar = Panel(inner, "Bar", C("#e0424e"));
        bar.raycastTarget = false; Place(bar.rectTransform, 0, 0, 3, h);
        var mk = Text(inner, "◆", 15, C("#e0424e"), TextAlignmentOptions.MidlineLeft);
        mk.raycastTarget = false; Place(mk.rectTransform, 14, sub != null ? 10 : 0, 20, sub != null ? 34 : h);

        var lab = Text(inner, label, size, weight == 2 ? C("#e8c46a") : weight == 0 ? C("#a79fbd") : C("#ece6f4"),
            TextAlignmentOptions.MidlineLeft, weight == 0 ? FontStyles.Normal : FontStyles.Bold);
        lab.raycastTarget = false; lab.enableWordWrapping = false; lab.characterSpacing = 6;
        Place(lab.rectTransform, 40, sub != null ? 6 : 0, 580, sub != null ? 40 : h);
        if (sub != null)
        {
            var st = Text(inner, sub, 17, C("#a79fbd"), TextAlignmentOptions.TopLeft);
            st.raycastTarget = false; st.enableWordWrapping = false;
            Place(st.rectTransform, 40, 46, 580, 24);
        }

        var btn = hit.gameObject.AddComponent<Button>(); btn.targetGraphic = hit;
        btn.transition = Selectable.Transition.None;   // 見た目は TitleMenuRow が持つ
        btn.onClick.AddListener(() => { SoundSystem.Play(SoundSystem.Sfx.Click); pick(); });

        var row = hit.gameObject.AddComponent<TitleMenuRow>();
        row.owner = titleFx; row.glow = glow; row.bar = bar; row.mark = mk;
        row.label = lab.rectTransform; row.labelX = 40f;
        row.onPick = () => { SoundSystem.Play(SoundSystem.Sfx.Click); pick(); };
        titleFx.rows.Add(row);
        row.SetSelected(false);
        return y + h + 6f;
    }

    private RectTransform titleLoadBody, recordStatBody, recordAchBody;

    /// <summary>
    /// 🏅 戦績（Phase F-23/F-24/F-25）。**周を越えて残るもの**をここに集める：
    /// 通算記録・実績・形見。⚠ セーブ([[SaveSystem]])は1周の中身しか持たないので、
    /// こちらは `PlayerPrefs` 側（[[RunStats]] [[Achievements]] [[NarrativeSystem]]）を見る。
    /// </summary>
    private Image BuildRecordPage(RectTransform root)
    {
        var page = Panel(root, "RecordPage", C("#0b0910"));
        StretchFull(page.rectTransform);
        var t = Text(page, "戦績", 30, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(t.rectTransform, 300, 110, 600, 40);
        recordStatBody = NewRect("StatBody", page.rectTransform);
        Place(recordStatBody, 300, 164, 520, 280);

        var t2 = Text(page, "実績", 20, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(t2.rectTransform, 300, 470, 520, 28);
        var ach = MakeVScroll(page, 300, 506, 1320, 330);
        recordAchBody = ach;

        var back = PrimaryButton(page, "戻る", PANEL2, TEXT, () => ShowTitlePage(0));
        Place((RectTransform)back.transform, 300, 860, 220, 50);
        page.gameObject.SetActive(false);
        return page;
    }

    private void RefreshRecordPage()
    {
        if (recordStatBody != null)
        {
            for (int i = recordStatBody.childCount - 1; i >= 0; i--) Destroy(recordStatBody.GetChild(i).gameObject);
            string[,] rows =
            {
                { "遊んだ周", RunStats.Runs + " 周" },
                { "勝ち切った回数", RunStats.Wins + " 回" },
                { "最高スコア", UITheme.Num(RunStats.BestScore) },
                { "最速の勝利", RunStats.BestTurn > 0 ? RunStats.BestTurn + " ターン" : "-" },
                { "通算の撃破", UITheme.Num(RunStats.TotalKills) },
                { "通算の波", UITheme.Num(RunStats.TotalWaves) },
                { "通算の時間", SaveSystem.PlayTimeText(RunStats.TotalSeconds) },
                { "実績", Achievements.UnlockedCount + " / " + Achievements.Count },
                { "形見", NarrativeSystem.UnlockedCount + " / " + NarrativeSystem.MementoCount
                    + "（枠 " + NarrativeSystem.Slots + "）" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                var k = Text(recordStatBody, rows[i, 0], 13, MUTED, TextAlignmentOptions.Left);
                Place(k.rectTransform, 0, i * 28, 280, 22);
                var v = Text(recordStatBody, rows[i, 1], 14, TEXT, TextAlignmentOptions.Right, FontStyles.Bold);
                Place(v.rectTransform, 280, i * 28, 240, 22);
            }
        }

        var c = recordAchBody; if (c == null) return;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        float w = 1320 - 14, cw = (w - 20) / 3f, y = 0;
        for (int i = 0; i < Achievements.Count; i++)
        {
            var a = Achievements.Get(i);
            bool got = Achievements.IsUnlocked(i);
            float cx = (i % 3) * (cw + 10), cy = (i / 3) * 62;
            var card = Panel(c, "A" + i, got ? CARD : C("#100e18"));
            Place(card.rectTransform, cx, cy, cw, 54); Outline(card, got ? GOLD_DK : LINE);
            // 🏅 未達成の隠し実績は中身を伏せる（探す楽しみを残す）
            bool veil = a.hidden && !got;
            var nm = Text(card.rectTransform, veil ? "??????" : a.jpName, 13.5f, got ? GOLD : MUTED,
                TextAlignmentOptions.Left, FontStyles.Bold);
            Place(nm.rectTransform, 12, 7, cw - 24, 20);
            var how = Text(card.rectTransform, veil ? "<color=#3a3550>隠し実績</color>" : a.how, 11, FAINT, TextAlignmentOptions.Left);
            Place(how.rectTransform, 12, 29, cw - 24, 18);
            y = cy + 62;
        }
        c.sizeDelta = new Vector2(0f, Mathf.Max(y + 8, 80));
    }

    /// <summary>💾 タイトルの『続きから』。ゲーム中の保存画面と同じ行を、読込だけにして並べる。</summary>
    private Image BuildLoadPage(RectTransform root)
    {
        var page = Panel(root, "LoadPage", C("#0b0910"));
        StretchFull(page.rectTransform);
        var t = Text(page, "続きから", 30, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(t.rectTransform, 630, 150, 800, 40);
        var s = Text(page, "<color=#6f6889>保存した記録を読み込みます。オートはターンの頭に書かれたものです。</color>",
            13, FAINT, TextAlignmentOptions.Left);
        Place(s.rectTransform, 630, 194, 800, 20);
        titleLoadBody = NewRect("LoadBody", page.rectTransform);
        Place(titleLoadBody, 630, 232, SAVE_W, 400);
        var back = PrimaryButton(page, "戻る", PANEL2, TEXT, () => ShowTitlePage(0));
        Place((RectTransform)back.transform, 630, 660, 220, 50);
        page.gameObject.SetActive(false);
        return page;
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private Image BuildHelpPage(RectTransform root)
    {
        var page = Panel(root, "HelpPage", C("#0b0910"));
        StretchFull(page.rectTransform);
        var t = Text(page, "遊び方", 30, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(t.rectTransform, 360, 120, 1200, 40);
        // ⚠ 通しプレイで**この文章が古くなっていた**（「上部の『地上』から」＝そのボタンはもう無い）。
        //   フェーズ制に変わった時点で書き換えるべきだった。**仕様を変えたら遊び方も直す。**
        //   あわせて、実際に詰まった所（隣接してからでないと攻められない／産出は人口が耕すぶん／
        //   強化はすべて図鑑の中）を明記した。
        string body =
            "<b><color=#e3a94a>1ターンは「前半＝迷宮」と「後半＝地上」でできている。</color></b>\n\n"
            + "<b><color=#e3a94a>1. 準備（前半）</color></b>\n"
            + "  DPを払って罠・スポナー・トーテムを敷き、配下を召喚して各階に配置する。部隊を組み、1体をボスに任命できる。\n"
            + "  <color=#9c95b4>部隊は5〜6枠。役割（近接/壁/遠隔/支援/妨害）を散らすほど全員が強くなり、満員でさらに上乗せ。</color>\n\n"
            + "<b><color=#e3a94a>2. 防衛戦（前半）</color></b>\n"
            + "  『侵略開始』で冒険者のウェーブが突入する。倒す・怖がらせる・宝箱を漁らせる、どれもDPと感情になる。\n"
            + "  最下層の魔王が討たれたら敗北。戦闘中は<b>号令</b>が打てる（一時停止しながらでよい）。\n\n"
            + "<b><color=#e3a94a>3. 強くする ― すべて『図鑑』の中にある</color></b>\n"
            + "  <b>進化・鍛造（武器/防具）・装飾品・召喚の儀・行商人</b>は、上部の『図鑑』からまとめて触れる。\n"
            + "  配置枠が埋まってDPが余ったら、<b>枠ではなく個体そのもの</b>に注ぐ。\n"
            + "  経験値は深い階層ほど多く入る（魔素濃度）。冒険者は自分の格に合う深さまでしか降りてこない。\n\n"
            + "<b><color=#e3a94a>4. 地上（後半・4X）</color></b>\n"
            + "  防衛戦が終わると自動で世界地図に出る。眷属を動かし、領域を獲り、拠点を築く。\n"
            + "  <color=#9c95b4>敵領は<b>隣接してからでないと攻められない</b>。遠いときはまず前線の自領まで移動し、次のターンに攻める。</color>\n"
            + "  <color=#9c95b4>産出は「人口が耕せるタイル」から出る。版図を広げる前に、拠点の<b>人口</b>を増やすこと。</color>\n"
            + "  時代・偉業・政策・属性・外交・勝利条件は左端のメニューから。\n\n"
            + "<b><color=#e3a94a>操作</color></b>  左クリック＝配置 ／ 右クリック＝撤去 ／ ホイール＝ズーム ／ ドラッグ＝移動\n"
            + "  <color=#9c95b4>[Space]＝フェーズを進める　[Esc]＝閉じる　[1]〜[8]＝配置ツール　[Z][X][C][R][T]＝図鑑/研究/魔王/遺物/拡張</color>";
        var b = Text(page, body, 15, TEXT, TextAlignmentOptions.TopLeft);
        Place(b.rectTransform, 360, 176, 1200, 620);
        var back = PrimaryButton(page, "戻る", PANEL2, TEXT, () => ShowTitlePage(0));
        Place((RectTransform)back.transform, 360, 830, 220, 50);
        return page;
    }

    private static readonly SurfaceGen.Size[] TitleWorldSizes =
        { SurfaceGen.Size.Tiny, SurfaceGen.Size.Small, SurfaceGen.Size.Medium, SurfaceGen.Size.Large };

    private Image BuildSetupPage(RectTransform root)
    {
        var page = Panel(root, "SetupPage", C("#0b0910"));
        StretchFull(page.rectTransform);

        var eye = Text(page, "世界設定", 12, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(eye.rectTransform, 360, 62, 600, 18); eye.characterSpacing = 8;
        var t = Text(page, "この世界の始まりを決める", 30, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(t.rectTransform, 360, 82, 900, 40);
        var sub = Text(page, "選んだ迷宮はそのまま建造されます。<b>開始予算から建造費を引いた残りが初期DP</b>です。豪華に始めるほど手元は乏しくなります。",
            13, MUTED, TextAlignmentOptions.Left);
        Place(sub.rectTransform, 360, 126, 1200, 22);

        float lx = 360, rx = 980, cw = 580;

        // ---- 左：迷宮タイプ ----
        var l1 = Text(page, "迷宮タイプ（形の性格。得と損がセット）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(l1.rectTransform, lx, 168, cw, 16);
        string[] tNames = { "標準", "迷路", "大空洞", "蟻の巣" };
        string[] tDesc = {
            "配置枠+2 ／ 癖なし",
            "冒険者が長居+35% ／ 宝箱-25%",
            "部隊+10%・徘徊+1 ／ 集客-15%",
            "宝箱+50%・集客+20% ／ トーテム半径-1" };
        tTypeBtns.Clear();
        float tcw = (cw - 10) / 2f;
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            var b = Card(page, lx + (i % 2) * (tcw + 10), 188 + (i / 2) * 62, tcw, 54, tNames[i], tDesc[i],
                () => { GameSetup.DungeonTypeIdx = idx; RefreshTitleSel(); });
            var cost = Text(b.rectTransform, "+" + GameSetup.TypeCost(i) + " DP", 10.5f, GOLD, TextAlignmentOptions.Right);
            Place(cost.rectTransform, tcw - 76, 7, 66, 16);
            tTypeBtns.Add(b);
        }

        // ---- 左：空間タイプ ----
        var l2 = Text(page, "空間タイプ（属性の性格。費用はかかりません）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(l2.rectTransform, lx, 322, cw, 16);
        string[] sNames = { "洞窟", "遺跡", "城塞", "溶岩", "氷雪" };
        Color[] sCols = { C("#5a5560"), C("#5c6446"), C("#4e5674"), C("#7a3a30"), C("#4a6480") };
        tSpaceBtns.Clear();
        float scw = (cw - 20) / 3f;
        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            var b = Chip(page, lx + (i % 3) * (scw + 10), 342 + (i / 3) * 40, scw, 32, sNames[i], sCols[i],
                () => { GameSetup.SpaceTypeIdx = idx; RefreshTitleSel(); });
            tSpaceBtns.Add(b);
        }
        titleSpaceEffText = Text(page, "", 11.5f, MUTED, TextAlignmentOptions.Left);
        Place(titleSpaceEffText.rectTransform, lx, 424, cw, 18);

        // ---- 左：宝箱の量 ----
        var l3 = Text(page, "宝箱の量（多いほど集客と収入が増えるが、建造費も嵩む）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(l3.rectTransform, lx, 456, cw, 16);
        string[] cNames = { "少", "中", "多" };
        tChestBtns.Clear();
        float ccw = (cw - 20) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var b = Chip(page, lx + i * (ccw + 10), 476, ccw, 34, cNames[i] + "  (" + GameSetup.ChestCost(i) + "DP/層)", GOLD,
                () => { GameSetup.ChestIdx = idx; RefreshTitleSel(); });
            tChestBtns.Add(b);
        }

        // ---- 右：階層数 ----
        var r1 = Text(page, "初期階層数（深いほど守りは厚いが、器のぶん建造費も増える）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(r1.rectTransform, rx, 168, cw, 16);
        string[] fNames = { "1層", "2層", "3層" };
        tFloorBtns.Clear();
        float fcw = (cw - 20) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int n = i + 1;
            var b = Chip(page, rx + i * (fcw + 10), 188, fcw, 34, fNames[i], VIOLET,
                () => { GameSetup.FloorCount = n; RefreshTitleSel(); });
            tFloorBtns.Add(b);
        }
        var r1n = Text(page, "予算は +400/層 しか増えないので、深く始めるなら宝箱は少なめに。魔王は最下層にのみ実在します。",
            11.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(r1n.rectTransform, rx, 228, cw, 34);

        // ---- 右：地上の広さ ----
        var r2 = Text(page, "地上の広さ（毎回ちがう地形が生成されます）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(r2.rectTransform, rx, 274, cw, 16);
        tWorldBtns.Clear();
        float wcw = (cw - 30) / 4f;
        for (int i = 0; i < 4; i++)
        {
            int wi = i;
            var b = Panel(page, "TWorld_" + i, CARD);
            Place(b.rectTransform, rx + i * (wcw + 10), 294, wcw, 34); Outline(b, LINE);
            var nm = Text(b.rectTransform, SurfaceGen.NameOf(TitleWorldSizes[i]), 12.5f, TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(nm.rectTransform, 0, 3, wcw, 16);
            var ct = Text(b.rectTransform, SurfaceGen.TileCount(TitleWorldSizes[i]) + "タイル", 10, MUTED, TextAlignmentOptions.Center);
            Place(ct.rectTransform, 0, 19, wcw, 14);
            var bt = b.gameObject.AddComponent<Button>(); bt.targetGraphic = b;
            bt.onClick.AddListener(() => { GameSetup.WorldSize = TitleWorldSizes[wi]; RefreshTitleSel(); });
            tWorldBtns.Add(b);
        }
        var r2n = Text(page, "広いほど攻める先も守る先も増えます。東西はループします（初期DPには影響しません）。",
            11.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(r2n.rectTransform, rx, 334, cw, 20);

        // ---- 右：シード ----
        var r3 = Text(page, "世界の種（同じ数字なら同じ地形になります）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(r3.rectTransform, rx, 366, cw, 16);
        var seedBox = Panel(page, "SeedBox", CARD);
        Place(seedBox.rectTransform, rx, 386, cw - 150, 34); Outline(seedBox, LINE);
        titleSeedText = Text(seedBox.rectTransform, "-", 13, TEXT, TextAlignmentOptions.Center); StretchFull(titleSeedText.rectTransform);
        var reroll = PrimaryButton(page, "引き直す", PANEL2, TEXT, () =>
            { GameSetup.Seed = Random.Range(1, int.MaxValue); GameSetup.DailySeed = false; RefreshTitleSel(); });
        Place((RectTransform)reroll.transform, rx + cw - 140, 386, 140, 34);

        // ---- 右：難易度（F-22）----
        var r4 = Text(page, "難易度（仕組みは変わりません。世の本気度と取り分だけが動きます）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(r4.rectTransform, rx, 430, cw, 16);
        tDiffBtns.Clear();
        float dcw = (cw - 30) / 4f;
        for (int i = 0; i < Difficulty.Count; i++)
        {
            int di = i;
            var d = Difficulty.Get(i);
            var b = Panel(page, "TDiff_" + i, CARD);
            Place(b.rectTransform, rx + i * (dcw + 10), 450, dcw, 34); Outline(b, LINE);
            var nm = Text(b.rectTransform, d.jpName, 13, C(d.colorHex), TextAlignmentOptions.Center, FontStyles.Bold);
            Place(nm.rectTransform, 0, 3, dcw, 16);
            var ct = Text(b.rectTransform, "スコア ×" + d.score.ToString("0.0"), 10, MUTED, TextAlignmentOptions.Center);
            Place(ct.rectTransform, 0, 19, dcw, 14);
            var bt = b.gameObject.AddComponent<Button>(); bt.targetGraphic = b;
            bt.onClick.AddListener(() => { GameSetup.DifficultyIdx = di; RefreshTitleSel(); });
            tDiffBtns.Add(b);
        }
        titleDiffText = Text(page, "", 11.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(titleDiffText.rectTransform, rx, 490, cw, 34);

        // ---- 右：日替わりの世界（F-26）----
        //  同じ日なら誰がやっても同じ世界。腕の比べどころになる。
        titleDailyBtn = PrimaryButton(page, "今日の世界に挑む", PANEL2, TEXT, () =>
        {
            GameSetup.DailySeed = !GameSetup.DailySeed;
            if (GameSetup.DailySeed)
            {
                GameSetup.Seed = GameSetup.TodaySeed;
                GameSetup.WorldSize = SurfaceGen.Size.Medium;
                GameSetup.DifficultyIdx = 2;              // 日替わりは条件を固定する（記録を比べるため）
                GameSetup.DungeonTypeIdx = GameSetup.TodaySeed % 4;
                GameSetup.SpaceTypeIdx = (GameSetup.TodaySeed / 4) % 5;
                GameSetup.ChestIdx = 1; GameSetup.FloorCount = 2;
            }
            RefreshTitleSel();
        });
        Place((RectTransform)titleDailyBtn.transform, rx, 530, cw - 150, 36);
        titleDailyText = Text(page, "", 11.5f, FAINT, TextAlignmentOptions.Left);
        Place(titleDailyText.rectTransform, rx + cw - 140, 540, 140, 20);

        // ---- 下：初期DPの内訳 ----
        var box = Panel(page, "BudgetBox", PANEL); Place(box.rectTransform, lx, 660, 1200, 116);
        Outline(box, LINE2); SkinPanel(box);
        // ⚠ 全角のマイナス(−)はUIフォントに無く、サニタイズで**消える**。半角ハイフンを使う。
        var bl = Text(box, "初期DP（開始予算 - 初期迷宮の建造費）", 12, FAINT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(bl.rectTransform, 20, 14, 700, 16);
        titleBudgetText = Text(box, "", 22, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(titleBudgetText.rectTransform, 20, 36, 1160, 30);
        titleNoteText = Text(box, "", 11.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(titleNoteText.rectTransform, 20, 72, 1160, 34);

        // ---- 決定 ----
        var back = PrimaryButton(page, "戻る", PANEL2, TEXT, () => ShowTitlePage(0));
        Place((RectTransform)back.transform, lx, 812, 220, 56);
        titleStartBtn = PrimaryButton(page, "この世界で始める", BLOOD, C("#f0d9a0"), StartNewGame, true);
        Place((RectTransform)titleStartBtn.transform, lx + 780, 812, 420, 56);

        page.gameObject.SetActive(false);
        return page;
    }

    private void RefreshTitleSel()
    {
        for (int i = 0; i < tTypeBtns.Count; i++) SetSel(tTypeBtns[i], i == GameSetup.DungeonTypeIdx);
        for (int i = 0; i < tSpaceBtns.Count; i++) SetSel(tSpaceBtns[i], i == GameSetup.SpaceTypeIdx);
        for (int i = 0; i < tChestBtns.Count; i++) SetSel(tChestBtns[i], i == GameSetup.ChestIdx);
        for (int i = 0; i < tFloorBtns.Count; i++) SetSel(tFloorBtns[i], i == GameSetup.FloorCount - 1);
        for (int i = 0; i < tWorldBtns.Count; i++) SetSel(tWorldBtns[i], TitleWorldSizes[i] == GameSetup.WorldSize);
        for (int i = 0; i < tDiffBtns.Count; i++) SetSel(tDiffBtns[i], i == GameSetup.DifficultyIdx);
        if (titleDiffText != null)
        {
            var d = Difficulty.Current;
            SetTxt(titleDiffText, d.desc + "\n<color=#6f6889>冒険者の伸び ×" + d.advPower.ToString("0.00")
                + "／人数 ×" + d.advCount.ToString("0.00") + "／他魔王 ×" + d.rivalGrow.ToString("0.00")
                + "／取り分 ×" + d.reward.ToString("0.00") + "</color>");
        }
        if (titleDailyText != null)
        {
            int b = RunStats.DailyBest(GameSetup.TodaySeed);
            SetTxt(titleDailyText, GameSetup.TodayLabel + (b > 0 ? "\n<color=#e3a94a>最高 " + UITheme.Num(b) + "</color>" : "\n<color=#6f6889>未挑戦</color>"));
        }
        if (titleDailyBtn != null)
        {
            var img = titleDailyBtn.targetGraphic as Image;
            if (img != null) img.color = GameSetup.DailySeed ? BLOOD : PANEL2;
        }

        if (titleSpaceEffText != null)
            SetTxt(titleSpaceEffText, DungeonTheme.SpaceName((DungeonGenerator.SpaceType)GameSetup.SpaceTypeIdx)
                + "：" + DungeonTheme.SpaceEffect((DungeonGenerator.SpaceType)GameSetup.SpaceTypeIdx));
        if (titleSeedText != null) SetTxt(titleSeedText, GameSetup.Seed.ToString("N0"));
        if (titleBudgetText != null)
            SetTxt(titleBudgetText, "開始予算 <color=#9c95b4>" + GameSetup.Budget.ToString("N0") + "</color>  -  建造費 <color=#df5a5a>"
                + GameSetup.BuildCost.ToString("N0") + "</color>  ＝  初期DP <color=#e3a94a>" + GameSetup.StartDP.ToString("N0") + "</color>");
        if (titleNoteText != null)
        {
            string s = "建造費の内訳： 基本 300 ＋ 宝箱 " + GameSetup.ChestCost(GameSetup.ChestIdx) + " ＝ "
                + (300 + GameSetup.ChestCost(GameSetup.ChestIdx)) + " × " + GameSetup.FloorCount + "層"
                + "  ＋  タイプ " + GameSetup.TypeCost(GameSetup.DungeonTypeIdx);
            if (GameSetup.OverBudget)
                s += "\n<color=#df5a5a>予算を超えています。</color>最低 " + GameSetup.MinStartDP + " DP は残りますが、序盤は宝箱と魔王だけで凌ぐことになります。";
            SetTxt(titleNoteText, s);
        }
    }

    /// <summary>世界設定を各システムへ流し、迷宮と地上を生成してゲームを始める。</summary>
    private void StartNewGame()
    {
        // 迷宮側の設定（生成パネルの選択状態も揃えておく）
        selType = GameSetup.DungeonTypeIdx; selSpace = GameSetup.SpaceTypeIdx;
        selChest = GameSetup.ChestIdx; selFloors = GameSetup.FloorCount - 1;
        if (generator != null)
        {
            generator.SetDungeonType(GameSetup.DungeonTypeIdx);
            generator.SetSpaceType(GameSetup.SpaceTypeIdx);
            generator.SetChestAmount(GameSetup.ChestIdx);
        }
        if (floorMgr != null) floorMgr.SetFloorCount(GameSetup.FloorCount);

        // 🧹 **前の周の盤を空にする。** ⚠ これが無いと同じセッションの2周目で T1 の波が終わらない
        //    （実測：自動運転の2〜4周目が全部 T1 の戦闘で停止した）。→ [[DungeonAdventurerSpawner]]
        {
            var sp = Object.FindFirstObjectByType<DungeonAdventurerSpawner>();
            if (sp != null) sp.AbortAndClear();
        }

        // 🌍 地上を作り直す（広さと種）。迷宮のあるタイルを選び直させる。
        SurfaceMap.Regenerate(GameSetup.WorldSize, GameSetup.Seed);
        selectedRegionId = -1;

        // ⚠⚠ **周をまたいで残っていた系統をここで畳む。**
        //   実測（通しプレイ2周目）：`Reset()` を持っているのに**一度も呼ばれていない**ものが7つあり、
        //   新しい周が **脅威1.06・装備水準13.8・時代69/210・素材66・研究済みノードつき**で始まっていた。
        //   ＝ 周回（→ [[replayability-phase-f]]）が成立していなかった。
        //   ⚠ 資源の初期化は **`SetDP` より前**（後ろに置くと初期DPを0にしてしまう）。
        LureEconomy.Reset();          // 🕸️ 脅威度と世界の装備水準
        TreasureGrades.Reset();       // 🎁 撒く等級のつまみ（階層ごと）
        LordBerserk.Reset();          // 🔥 魔王の殻と第二形態
        EraSystem.Reset();            // ⏳ 時代
        ResearchState.Reset();        // 🔬 研究点と研究済み
        MinionEvolution.ResetToBase();// 🧬 解禁済みの配下（基本形だけに戻す）
        TrainingSystem.Reset();       // 🏋️ 訓練中の個体
        DiplomacySystem.Reset();      // 🏛️ 威名と独立勢力
        RelicManager.ResetProgress(); // 🏺 遺物の解放条件の進み
        if (res != null) res.ResetRun();

        // 💰 初期DP＝予算−建造費。**建造費はここで前払い済み**なので、生成そのものは無料で行う。
        if (res != null) res.SetDP(GameSetup.StartDP);

        GameSetup.WaitForTitle = false; GameSetup.Started = true;
        // 📊 周の記録をまっさらにする（⚠ ここを忘れると前の周の数字が混ざる）
        RunStats.ResetRun();
        if (turn != null) turn.ResetRun();   // 🔄 ⚠ ターン番号とフェーズを戻す（これが無いと前の周の続きから始まる）
        VictorySystem.Reset();
        // 🧹 ⚠ `ResetRunCounters` だけでは**置いた物が残る**。全階層の配置も空にする。
        if (featureMgr != null) { featureMgr.ClearAllRunFeatures(); featureMgr.ResetRunCounters(); }
        ProductionSystem.Reset();                         // 🔨 生産の待ち行列も持ち越さない
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (generator != null) generator.GenerateAndBuild();
        PolicySystem.Reset(); AttributeSystem.Reset(); DiscoverySystem.Reset(); ScoutSystem.Reset();
        EnemyForce.Reset(); NotifySystem.Reset(); LegionRoster.Reset(); SyncretismSystem.Reset(); SummonGacha.Reset(); MerchantShop.Reset(); AccessoryInventory.Reset();
        LordStance.Reset();                               // 👑 構え・捕食は周を越えて持ち越さない
        MutationSystem.Reset();                           // 🧬 世界の変異もまっさらに戻す
        WardSystem.Reset(); WaveRoster.Reset();           // 🔭🛡️ 前の周の名簿と備えを持ち越さない
        Excavation.Reset();                               // ⛏️ 工事の回数と掘りかけも持ち越さない
        IncidentSystem.Reset();                           // ⚡ 異変も周を越えない
        Nemesis.Reset(); Prison.Reset();                  // 🗡️⛓️ 因縁と捕虜も周を越えない（前の周の恨みは無い）
        FeverSystem.Reset();                              // 🔥 大招集の宣言も持ち越さない
        KillFeedback.Reset();                             // 💥 連撃も持ち越さない
        LureStance.Reset();                               // 🕸️ 泳がせの構えも持ち越さない
        RumorSystem.Reset();                              // 🗣️ 流言も持ち越さない
        HarvestBurst.Clear();                             // 🌾 前の周の収穫の溜めも持ち越さない
        WaveReport.Reset();                               // 📜 波の決算の集計も持ち越さない
        Decoy.Reset();                                    // 🔔 誘引/過負荷の回数も持ち越さない
        EmotionHarvest.Reset(); CommandCharge.Reset();    // 🩸📯 刈り取りと号令ゲージも持ち越さない
        ClaimFx.Reset();                                  // 🚩 版図の演出待ちも持ち越さない
        Proclamation.Reset();                             // 📜 ギルドの布告も持ち越さない
        KinRoster.GrantStarterKin();                      // 🌅 初手から地上に出られるよう眷属を1体
        // 🔮 **第1ターンの名簿をここで引く。** ⚠ Roll はターンの切り替わりでしか呼ばれないので、
        //    ここが無いと開幕だけ名簿が空になり、①先触れが空 ②報告が人数を語れない
        //    ③スポナーが `Max(1, Count)` で **1体しか湧かない** ④大招集の見込みが「0→0体」になる。
        WaveRoster.Roll(1);
        GuideSystem.Reset(); GuideSystem.OnTurnStart(1);   // 📖 第1ターンの報告（開幕の手引き）

        if (titleRoot != null) titleRoot.SetActive(false);
        if (dungeonCanvas != null) dungeonCanvas.enabled = true;
        RefreshSelections(); RefreshCost(); RefreshFloorTabs(); RefreshSurfaceSizeBtns();

        Debug.Log($"🎬『開始』{DungeonTheme.TypeName((DungeonGenerator.DungeonType)GameSetup.DungeonTypeIdx)}／"
            + $"{DungeonTheme.SpaceName((DungeonGenerator.SpaceType)GameSetup.SpaceTypeIdx)}／宝箱{GameSetup.ChestIdx}／"
            + $"{GameSetup.FloorCount}層／地上{SurfaceMap.Count}タイル(seed {SurfaceMap.MapSeed})／初期DP {GameSetup.StartDP}"
            + $"（予算{GameSetup.Budget} − 建造費{GameSetup.BuildCost}）");
    }
}
