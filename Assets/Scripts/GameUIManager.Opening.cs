using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🎬 オープニング（段F）。『開始』のあと約44秒で、世界と目的を見せてから迷宮へ降りる。
///
/// - 6場面のドット絵（384×216・PixelLab）をカメラの寄せ・引き・横移動で動かし、腹心が語る。
///   地上 → 迷宮核と魔王の目覚め → 三人の魔王 → 地上の人々 → 迷宮は育つ → 玉座（跪く腹心）→ 題字。
/// - 最後の「お目覚めですか、我が主」が、そのまま案内役（[[GameUIManager.Tutor]]）の最初の一言につながる。
/// - 語りは `Resources/Audio/Voice/v_op_N`（**原稿はひらがな** → [[tts-hiragana-only]]）。無ければ字幕だけ。
/// - 曲は `Resources/Audio/Bgm/opening` があればそれ、無ければ手続き生成の遅く低い曲（`SoundSystem.Bgm.Opening`）。
/// - 流すのは**最初の1回だけ**（PlayerPrefs `opening.seen`）。タイトルの「オープニングを見る」から見返せる。
/// - 押す・Space・Esc・Enter で飛ばせる。⚠ 計測中は流さない。⚠ 時間は unscaled。
/// <para>`GameUIManager` の partial。</para>
/// </summary>
public partial class GameUIManager
{
    private const string OpeningSeenPref = "opening.seen";

    private class OpScene
    {
        public string image, voice, sub;
        public float dur;
        public float s0, x0, y0, s1, x1, y1;   // カメラ：倍率と注目点（384×216 の絵の座標）
    }

    private static readonly OpScene[] OpScenes =
    {
        new OpScene { image = "Scenes/interlude_world", voice = "v_op_1", dur = 6.5f,
            sub = "地の底には、迷宮核と呼ばれる石が眠っている。", s0 = 1.0f, x0 = 192, y0 = 108, s1 = 1.22f, x1 = 192, y1 = 100 },
        new OpScene { image = "Scenes/op_2", voice = "v_op_2", dur = 6.5f,
            sub = "核は、ときに主を生む。――魔王を。", s0 = 1.08f, x0 = 192, y0 = 140, s1 = 1.22f, x1 = 192, y1 = 96 },
        new OpScene { image = "Scenes/op_3", voice = "v_op_3", dur = 7.0f,
            sub = "同じ時代に目覚めた魔王は、ほかに三人。", s0 = 1.25f, x0 = 140, y0 = 108, s1 = 1.25f, x1 = 244, y1 = 108 },
        new OpScene { image = "Scenes/op_4", voice = "v_op_4", dur = 7.5f,
            sub = "地上の人々は迷宮を宝の山と呼び、命を賭けて潜り込む。", s0 = 1.22f, x0 = 236, y0 = 112, s1 = 1.22f, x1 = 150, y1 = 112 },
        new OpScene { image = "Scenes/op_5", voice = "v_op_5", dur = 6.0f,
            sub = "訪れる者を喰らい、迷宮は育つ。", s0 = 1.0f, x0 = 192, y0 = 108, s1 = 1.35f, x1 = 200, y1 = 104 },
        new OpScene { image = "Scenes/op_6", voice = "v_op_6", dur = 7.5f,
            sub = "最後に立つ魔王が、世界を統べる。\n――お目覚めですか、我が主。", s0 = 1.7f, x0 = 192, y0 = 76, s1 = 1.04f, x1 = 192, y1 = 108 },
    };
    private const float OpFade = 0.8f, OpTitle = 2.6f;

    private RectTransform opRoot;
    private CanvasGroup opGroup;
    private Image opPicA, opPicB;
    private TextMeshProUGUI opSub, opTitle, opTitleEn;
    private Coroutine opCo;
    private bool opSkip;

    public bool OpeningPlaying => opCo != null;
    public void SkipOpening() { if (opCo != null) opSkip = true; }

    private void BuildOpening()
    {
        // ⚠ タイトル（300）より上：タイトルから見返すときも上に出す
        opRoot = MakeCanvas("OpeningCanvas", 310);
        opGroup = opRoot.gameObject.AddComponent<CanvasGroup>();
        var view = NewRect("View", opRoot); StretchFull(view);
        var black = Panel(view, "Black", Color.black); StretchFull(black.rectTransform);
        var skip = black.gameObject.AddComponent<Button>(); skip.transition = Selectable.Transition.None;
        skip.onClick.AddListener(SkipOpening);
        view.gameObject.AddComponent<RectMask2D>();
        opPicA = OpImage(view, "PicA");
        opPicB = OpImage(view, "PicB");
        // 映画の黒帯（上下 9%）
        for (int i = 0; i < 2; i++)
        {
            var bar = Panel(view, "Bar" + i, Color.black); bar.raycastTarget = false;
            var r = bar.rectTransform;
            r.anchorMin = new Vector2(0f, i == 0 ? 0.91f : 0f); r.anchorMax = new Vector2(1f, i == 0 ? 1f : 0.09f);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
        opSub = Text(view, "", 28f, C("#ece8f5"), TextAlignmentOptions.Bottom, FontStyles.Bold);
        opSub.raycastTarget = false;
        var sr = opSub.rectTransform; sr.anchorMin = new Vector2(0.08f, 0.10f); sr.anchorMax = new Vector2(0.92f, 0.30f); sr.offsetMin = sr.offsetMax = Vector2.zero;
        var sh = opSub.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.9f); sh.effectDistance = new Vector2(2f, -2f);
        opTitle = Text(view, "迷宮統魔録", 76f, C("#e3a94a"), TextAlignmentOptions.Center, FontStyles.Bold);
        opTitle.raycastTarget = false; StretchOffset(opTitle.rectTransform, 0, 0, 0, 60);
        opTitleEn = Text(view, "CHRONICLE OF THE LABYRINTH LORD", 18f, MUTED, TextAlignmentOptions.Center);
        opTitleEn.raycastTarget = false; opTitleEn.characterSpacing = 6f;
        var tr = opTitleEn.rectTransform; tr.anchorMin = new Vector2(0f, 0.38f); tr.anchorMax = new Vector2(1f, 0.44f); tr.offsetMin = tr.offsetMax = Vector2.zero;
        var hint = Text(view, "<color=#9c95b4>押すと飛ばせる　[Space]</color>", 13f, MUTED, TextAlignmentOptions.BottomRight);
        hint.raycastTarget = false; StretchOffset(hint.rectTransform, 0, 0, 24, 14);
        opRoot.gameObject.SetActive(false);
    }

    private Image OpImage(RectTransform parent, string name)
    {
        var img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.raycastTarget = false;
        img.gameObject.SetActive(false);
        return img;
    }

    private void OpShot(Image img, Sprite sp, float scale, float fx, float fy, float alpha)
    {
        if (sp == null || alpha <= 0.001f) { img.gameObject.SetActive(false); return; }
        if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
        img.sprite = sp;
        var area = opRoot.rect;
        float k = Mathf.Max(area.width / 384f, area.height / 216f) * scale;
        img.rectTransform.sizeDelta = new Vector2(384f * k, 216f * k);
        img.rectTransform.anchoredPosition = new Vector2(-(fx - 192f) * k, (fy - 108f) * k);
        var c = img.color; c.a = alpha; img.color = c;
    }

    /// <summary>新しく始めたとき（まだ一度も見ていなければ）。流したら true。</summary>
    private bool TryPlayOpeningFirstTime()
    {
        if (MeasureMode.On || PlayerPrefs.GetInt(OpeningSeenPref, 0) == 1) return false;
        PlayOpening();
        return true;
    }

    /// <summary>オープニングを流す（タイトルの「オープニングを見る」からも）。</summary>
    public void PlayOpening()
    {
        if (opRoot == null || MeasureMode.On) return;
        if (opCo != null) StopCoroutine(opCo);
        opCo = StartCoroutine(OpeningRun());
    }

    private IEnumerator OpeningRun()
    {
        opSkip = false;
        var prevBgm = SoundSystem.CurrentBgm;
        SoundSystem.PlayBgm(SoundSystem.Bgm.Opening);
        var sprites = new Sprite[OpScenes.Length];
        for (int i = 0; i < OpScenes.Length; i++) sprites[i] = Resources.Load<Sprite>(OpScenes[i].image);
        opRoot.gameObject.SetActive(true);
        opRoot.SetAsLastSibling();
        opGroup.alpha = 1f;
        opTitle.gameObject.SetActive(false); opTitleEn.gameObject.SetActive(false);
        SetTxt(opSub, "");

        for (int i = 0; i < OpScenes.Length && !opSkip; i++)
        {
            var s = OpScenes[i];
            SoundSystem.PlayVoice(s.voice);
            for (float t = 0f; t < s.dur && !opSkip; t += IlDt())
            {
                float p = t / s.dur, e = p < 0.5f ? 2f * p * p : 1f - Mathf.Pow(-2f * p + 2f, 2f) / 2f;
                OpShot(opPicA, sprites[i], Mathf.Lerp(s.s0, s.s1, e), Mathf.Lerp(s.x0, s.x1, e), Mathf.Lerp(s.y0, s.y1, e), 1f);
                // 次の場面へ溶ける
                float left = s.dur - t;
                if (left < OpFade && i + 1 < OpScenes.Length)
                {
                    var n = OpScenes[i + 1];
                    OpShot(opPicB, sprites[i + 1], n.s0, n.x0, n.y0, 1f - left / OpFade);
                }
                else opPicB.gameObject.SetActive(false);
                // 字幕：少し遅れて出て、終わる少し前に消える
                float a = Mathf.Clamp01((t - 0.4f) / 0.5f) * Mathf.Clamp01((left - 0.2f) / 0.4f);
                SetTxt(opSub, s.sub);
                var sc = opSub.color; sc.a = a; opSub.color = sc;
                // 最初の場面は黒から明ける
                if (i == 0) opGroup.alpha = Mathf.Clamp01(t / 0.8f);
                yield return null;
            }
        }
        // 題字
        if (!opSkip)
        {
            opPicA.gameObject.SetActive(false); opPicB.gameObject.SetActive(false);
            SetTxt(opSub, "");
            opTitle.gameObject.SetActive(true); opTitleEn.gameObject.SetActive(true);
            for (float t = 0f; t < OpTitle && !opSkip; t += IlDt())
            {
                float a = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((OpTitle - t) / 0.6f);
                var c1 = opTitle.color; c1.a = a; opTitle.color = c1;
                var c2 = opTitleEn.color; c2.a = a * 0.8f; opTitleEn.color = c2;
                yield return null;
            }
        }
        // 幕を上げる（迷宮／タイトルへ）
        if (opSkip) SoundSystem.StopVoice();   // ⚠ 飛ばしたら語りも止める（次の画面にかぶる）
        PlayerPrefs.SetInt(OpeningSeenPref, 1); PlayerPrefs.Save();
        // 🎚️ 曲を絞りながら幕を上げる。⚠ 曲（Gemini の opening.mp3 は約65秒）がオープニングより長いので、
        //   そのまま次の曲へ切り替えるとプツッと切れた。絞り切ってから次の曲へ渡す
        float a0 = opGroup.alpha;
        const float fadeOut = 1.2f;
        for (float t = 0f; t < fadeOut; t += IlDt())
        {
            float k = t / fadeOut;
            opGroup.alpha = Mathf.Lerp(a0, 0f, Mathf.Clamp01(k * 1.6f));
            SoundSystem.MusicDuck = 1f - k;
            yield return null;
        }
        opRoot.gameObject.SetActive(false);
        SoundSystem.PlayBgm(SoundSystem.Bgm.None);
        SoundSystem.MusicDuck = 1f;
        SoundSystem.PlayBgm(prevBgm == SoundSystem.Bgm.Opening || prevBgm == SoundSystem.Bgm.None ? SoundSystem.Bgm.Prepare : prevBgm);
        opCo = null;
    }
}
