using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🎬 迷宮⇄地上の幕間（段C）。ターンの前半と後半が入れ替わるときに、**どこへ移るのか**を絵で見せる。
///
/// - 迷宮 → 地上：書斎 → 机の地図へ寄る（虫眼鏡）→ レンズの中で城の外観へ → 引いて地上の広い眺め → 本物の盤
/// - 地上 → 迷宮：地上の広い眺め → 城へ寄る（夕暮れ）→ 城門 → 階段を下る（B1F → 最深階を数える）→ 書斎 → 迷宮の盤
///   ⚠ 逆再生にしない（ユーザーの指定）。戻りは「下っていく」別の場面。
///
/// 一枚絵（384×216 のドット絵・PixelLab）を重ね、カメラの寄せ・引きで動かす。
/// ⚠ 切り替えは1ターンに2回ある（69ターンで約140回）。本編（約6秒）は**最初の1回と時代が変わったとき**だけ、
///   ふだんは短縮（約1.6秒）。押す・Space・Esc でいつでも飛ばせる。設定で「なし／毎回本編／短縮のみ」も選べる。
/// ⚠ 付けるのはフェーズの切り替え（`OnPhaseChanged`）だけ。メニューから地上や迷宮を開く操作には付けない。
/// ⚠ 時間は unscaled（戦闘の倍速・一時停止に引きずられない）。計測中は流さない。
/// <para>`GameUIManager` の partial。</para>
/// </summary>
public partial class GameUIManager
{
    // ============ 設定（周を越える好み＝PlayerPrefs） ============
    public enum InterludeMode { Off = 0, Milestone = 1, AlwaysFull = 2, ShortOnly = 3 }
    private const string InterludePref = "interlude.mode";
    public static InterludeMode InterludeSetting
    {
        get { return (InterludeMode)PlayerPrefs.GetInt(InterludePref, (int)InterludeMode.Milestone); }
        set { PlayerPrefs.SetInt(InterludePref, (int)value); PlayerPrefs.Save(); }
    }
    public static string InterludeModeName(InterludeMode m)
        => m == InterludeMode.Off ? "なし" : m == InterludeMode.Milestone ? "節目だけ本編"
         : m == InterludeMode.AlwaysFull ? "毎回本編" : "短縮のみ";

    // ============ 絵の座標（384×216 の絵の中の注目点） ============
    private const float PicW = 384f, PicH = 216f;
    private static readonly Vector2 PicMid = new Vector2(192f, 108f);
    private static readonly Vector2 StudyMap = new Vector2(205f, 160f);   // 書斎：机の上の地図
    private static readonly Vector2 CastleGate = new Vector2(218f, 132f); // 城：城門
    private static readonly Vector2 WorldHome = new Vector2(192f, 100f);  // 広い眺め：魔王城

    private RectTransform ilRoot;
    private CanvasGroup ilGroup;
    private Image ilPicA, ilPicB, ilLensMask, ilLensPic, ilRing, ilShade;
    private TextMeshProUGUI ilFloor, ilHint;
    private Sprite ilStudy, ilCastle, ilWorld, ilStairs;
    private Coroutine ilCo;
    private bool ilSkip, ilSwitched;
    private System.Action ilSwitchView;
    /// <summary>画面を本当に切り替える（1回だけ）。⚠ 2回呼ぶと地上に入り直して曲が鳴り直す。</summary>
    private void IlSwitch() { if (ilSwitched) return; ilSwitched = true; if (ilSwitchView != null) ilSwitchView(); }
    private int ilLastEraUp = -1, ilLastEraDown = -1, ilLastTurn = -1;

    public bool InterludePlaying => ilCo != null;
    public void SkipInterlude() { if (ilCo != null) ilSkip = true; }

    // ============ 組み立て ============
    private void BuildInterlude()
    {
        ilStudy = Resources.Load<Sprite>("Scenes/interlude_study");
        ilCastle = Resources.Load<Sprite>("Scenes/interlude_castle");
        ilWorld = Resources.Load<Sprite>("Scenes/interlude_world");
        ilStairs = Resources.Load<Sprite>("Scenes/interlude_stairs");

        // ⚠ 地上(110)・説明(200)より上、タイトル(300)より下
        ilRoot = MakeCanvas("InterludeCanvas", 250);
        ilGroup = ilRoot.gameObject.AddComponent<CanvasGroup>();

        var view = NewRect("View", ilRoot); StretchFull(view);
        var black = Panel(view, "Black", Color.black); StretchFull(black.rectTransform);
        // ⏭️ 画面のどこを押しても飛ばせる
        var skipBtn = black.gameObject.AddComponent<Button>();
        skipBtn.transition = Selectable.Transition.None;
        skipBtn.onClick.AddListener(SkipInterlude);
        view.gameObject.AddComponent<RectMask2D>();

        ilPicA = PicImage(view, "PicA");
        ilPicB = PicImage(view, "PicB");

        ilLensMask = Panel(view, "LensMask", Color.white);
        ilLensMask.sprite = CircleSprite(false);
        ilLensMask.raycastTarget = false;
        ilLensMask.rectTransform.anchorMin = ilLensMask.rectTransform.anchorMax = ilLensMask.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        var mask = ilLensMask.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
        ilLensPic = PicImage(ilLensMask.rectTransform, "LensPic");

        ilRing = Panel(view, "Ring", Color.white);
        ilRing.sprite = CircleSprite(true); ilRing.raycastTarget = false;
        ilRing.rectTransform.anchorMin = ilRing.rectTransform.anchorMax = ilRing.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        ilShade = Panel(view, "Shade", new Color(0, 0, 0, 0)); StretchFull(ilShade.rectTransform); ilShade.raycastTarget = false;

        ilFloor = Text(view, "", 64f, C("#ece8f5"), TextAlignmentOptions.Bottom, FontStyles.Bold);
        StretchOffset(ilFloor.rectTransform, 0, 0, 0, 70); ilFloor.raycastTarget = false;
        ilHint = Text(view, "<color=#9c95b4>押すと飛ばせる　[Space]</color>", 13f, MUTED, TextAlignmentOptions.BottomRight);
        StretchOffset(ilHint.rectTransform, 0, 0, 24, 18); ilHint.raycastTarget = false;

        ilRoot.gameObject.SetActive(false);
    }

    private Image PicImage(RectTransform parent, string name)
    {
        var img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.raycastTarget = false;
        img.gameObject.SetActive(false);
        return img;
    }

    /// <summary>円（レンズの窓）と輪（レンズの縁）。⚠ 素材を増やさないよう、その場で焼く。</summary>
    private static Sprite CircleSprite(bool ring)
    {
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[N * N];
        float c = (N - 1) * 0.5f, R = N * 0.5f - 1f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(R - d);
                if (!ring) { px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255)); continue; }
                a *= Mathf.Clamp01(d - (R - 14f));
                // 縁：外側は暗い枠、内側は真鍮
                var col = d > R - 6f ? new Color32(42, 33, 23, 255) : new Color32(201, 163, 92, 255);
                col.a = (byte)(a * 255);
                px[y * N + x] = col;
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }

    // ============ 絵を「カメラ」で写す ============
    /// <summary>絵の点 <paramref name="focus"/> を画面の真ん中に置き、<paramref name="scale"/> 倍で写す（1＝画面いっぱい）。</summary>
    private void Shot(Image img, Sprite sp, float scale, Vector2 focus, float alpha = 1f)
    {
        if (sp == null || alpha <= 0.001f) { img.gameObject.SetActive(false); return; }
        if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
        if (img.sprite != sp) img.sprite = sp;
        var area = ((RectTransform)ilRoot).rect;
        // ⚠ 画面の縦横比が 16:9 でなくても隙間を出さない（覆うように合わせる）
        float k = Mathf.Max(area.width / PicW, area.height / PicH) * scale;
        img.rectTransform.sizeDelta = new Vector2(PicW * k, PicH * k);
        img.rectTransform.anchoredPosition = new Vector2(-(focus.x - PicMid.x) * k, (focus.y - PicMid.y) * k);
        var c = img.color; c.a = alpha; img.color = c;
    }

    private void Lens(float radius, Sprite inner, float innerScale, Vector2 innerFocus, bool ring)
    {
        bool on = radius > 1f;
        ilLensMask.gameObject.SetActive(on);
        ilRing.gameObject.SetActive(on && ring);
        if (!on) return;
        ilLensMask.rectTransform.sizeDelta = new Vector2(radius * 2f, radius * 2f);
        ilRing.rectTransform.sizeDelta = new Vector2(radius * 2.1f, radius * 2.1f);
        Shot(ilLensPic, inner, innerScale, innerFocus);
    }

    private void Shade(Color c) { ilShade.color = c; }

    /// <summary>1コマで進める時間。⚠ 上限を付ける ―― 地上の盤を作るときなどに一瞬止まると、
    ///   その間の時間がまとめて来て**場面がまるごと飛ぶ**（実測：止まった1.7秒で城の場面を飛ばした）。</summary>
    private static float IlDt() => Mathf.Clamp(Time.unscaledDeltaTime, 1f / 120f, 1f / 15f);

    private static float Ease(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    private static float LogLerp(float a, float b, float t) => Mathf.Exp(Mathf.Lerp(Mathf.Log(a), Mathf.Log(b), t));

    // ============ 流すかどうか ============
    /// <summary>
    /// フェーズが変わって画面を渡すとき（`OnPhaseChanged`）。流したら true（画面の切り替えは幕間の中で行う）。
    /// </summary>
    private bool TryPlayInterlude(bool toSurface)
    {
        if (ilRoot == null || MeasureMode.On || !GameSetup.Started || VictorySystem.Decided) return false;
        if (ilStudy == null || ilCastle == null || ilWorld == null || ilStairs == null) return false;
        var mode = InterludeSetting;
        if (mode == InterludeMode.Off) return false;
        if (ilCo != null) { StopCoroutine(ilCo); ilCo = null; }

        // 新しい周（ターンが戻った）なら「最初の1回」を数え直す
        int t = turn != null ? turn.CurrentTurn : 0;
        if (t < ilLastTurn) { ilLastEraUp = -1; ilLastEraDown = -1; }
        ilLastTurn = t;
        int era = (int)EraSystem.Current;
        bool full;
        if (mode == InterludeMode.AlwaysFull) full = true;
        else if (mode == InterludeMode.ShortOnly) full = false;
        else full = toSurface ? ilLastEraUp != era : ilLastEraDown != era;   // 最初の1回と、時代が変わったとき
        if (full) { if (toSurface) ilLastEraUp = era; else ilLastEraDown = era; }

        ilCo = StartCoroutine(toSurface ? InterludeUp(full) : InterludeDown(full));
        return true;
    }

    private struct Beat { public float dur; public System.Action<float> draw; public System.Action atStart; }

    private IEnumerator RunBeats(List<Beat> beats, System.Action switchView, bool fadeInFirst)
    {
        ilSkip = false;
        ilRoot.gameObject.SetActive(true);
        ilRoot.SetAsLastSibling();
        ilGroup.alpha = fadeInFirst ? 0f : 1f;
        ilFloor.text = "";
        Shade(new Color(0, 0, 0, 0));
        Lens(0f, null, 1f, PicMid, false);
        ilSwitched = false;
        ilSwitchView = switchView;

        foreach (var b in beats)
        {
            if (ilSkip) break;
            if (b.atStart != null) b.atStart();
            if (b.atStart == null && b.dur <= 0f) continue;
            for (float t = 0f; t < b.dur; t += IlDt())
            {
                if (ilSkip) break;
                b.draw(Mathf.Clamp01(t / b.dur));
                yield return null;
            }
            if (!ilSkip) b.draw(1f);
        }
        IlSwitch();   // ⚠ 飛ばしたときも、画面は必ず切り替える（ここを通らないと盤が迷宮のまま止まる）
        // 最後は幕を上げる（本物の盤が下から現れる）
        float a0 = ilGroup.alpha;
        for (float t = 0f; t < 0.35f; t += IlDt())
        {
            ilGroup.alpha = Mathf.Lerp(a0, 0f, t / 0.35f);
            yield return null;
        }
        ilGroup.alpha = 0f;
        ilRoot.gameObject.SetActive(false);
        ilCo = null;
    }

    // ============ 迷宮 → 地上 ============
    private IEnumerator InterludeUp(bool full)
    {
        var beats = new List<Beat>();
        System.Action toSurface = () => SetSurfaceMode(true);
        if (full)
        {
            beats.Add(new Beat { dur = 0.30f, draw = p => { ilGroup.alpha = p; Shot(ilPicA, ilStudy, 1f, PicMid); } });
            beats.Add(new Beat { dur = 1.00f, draw = p => Shot(ilPicA, ilStudy, Mathf.Lerp(1f, 1.06f, p), PicMid) });
            beats.Add(new Beat
            {
                dur = 1.30f, atStart = () => SoundSystem.Play(SoundSystem.Sfx.Discover, 0.5f),
                draw = p =>
                {
                    float e = Ease(p);
                    Shot(ilPicA, ilStudy, Mathf.Lerp(1.06f, 2.6f, e), Vector2.Lerp(PicMid, StudyMap, e));
                    float r = Mathf.Lerp(90f, 120f, e);
                    Lens(r, ilStudy, 2.6f * 1.15f, StudyMap, true);
                }
            });
        }
        else
        {
            beats.Add(new Beat { dur = 0.15f, draw = p => { ilGroup.alpha = p; Shot(ilPicA, ilStudy, 2.6f, StudyMap); Lens(120f, ilStudy, 3f, StudyMap, true); } });
        }
        // レンズが広がり、地図の城の印が本物の城に変わる
        beats.Add(new Beat
        {
            dur = full ? 1.10f : 0.50f,
            draw = p =>
            {
                float e = Ease(p);
                Shot(ilPicA, ilStudy, 2.6f, StudyMap);
                var area = ((RectTransform)ilRoot).rect;
                float r = Mathf.Lerp(120f, Mathf.Max(area.width, area.height), e);
                Lens(r, ilCastle, Mathf.Lerp(1.8f, 1f, e), Vector2.Lerp(CastleGate, PicMid, e), r < area.height * 0.45f);
            }
        });
        if (full)
            beats.Add(new Beat
            {
                dur = 0.80f,
                atStart = () => { Lens(0f, null, 1f, PicMid, false); },
                draw = p => Shot(ilPicA, ilCastle, Mathf.Lerp(1f, 1.04f, p), new Vector2(Mathf.Lerp(186f, 198f, p), 108f))
            });
        // 引いて地上の広い眺めへ（城が地図の1点に縮む）
        beats.Add(new Beat
        {
            dur = full ? 1.60f : 0.70f,
            atStart = () => { Lens(0f, null, 1f, PicMid, false); },
            draw = p =>
            {
                float e = Ease(p);
                Shot(ilPicA, ilWorld, LogLerp(9f, 1f, e), Vector2.Lerp(WorldHome, PicMid, e));
                Shot(ilPicB, ilCastle, Mathf.Lerp(1f, 0.08f, e), PicMid, 1f - Mathf.Clamp01(p / 0.45f));
            }
        });
        // 本物の盤へ（ここで画面を地上に渡し、幕を上げる）
        beats.Add(new Beat
        {
            dur = full ? 0.30f : 0.10f,
            atStart = () => { ilPicB.gameObject.SetActive(false); IlSwitch(); SoundSystem.Play(SoundSystem.Sfx.Turn, 0.6f); },
            draw = p => Shot(ilPicA, ilWorld, Mathf.Lerp(1f, 1.04f, p), PicMid)
        });
        yield return RunBeats(beats, toSurface, true);
    }

    // ============ 地上 → 迷宮 ============
    private IEnumerator InterludeDown(bool full)
    {
        var beats = new List<Beat>();
        System.Action toDungeon = () => SetSurfaceMode(false);
        int floors = floorMgr != null ? Mathf.Max(1, floorMgr.BuiltFloorCount) : 1;
        var dusk = new Color(0.16f, 0.08f, 0.27f, 1f);

        // 盤 → 広い眺め（幕を下ろしながら）→ 城へ寄る。日が暮れる
        beats.Add(new Beat
        {
            dur = full ? 1.50f : 0.60f,
            draw = p =>
            {
                ilGroup.alpha = Mathf.Clamp01(p / 0.25f);
                float e = Ease(p);
                Shot(ilPicA, ilWorld, LogLerp(1f, 9f, e), Vector2.Lerp(PicMid, WorldHome, e));
                float c = Mathf.Clamp01((p - 0.55f) / 0.35f);
                Shot(ilPicB, ilCastle, Mathf.Lerp(0.08f, 1f, Mathf.Clamp01((p - 0.55f) / 0.45f)), PicMid, c);
                Shade(new Color(dusk.r, dusk.g, dusk.b, e * 0.35f));
            }
        });
        if (full)
            beats.Add(new Beat
            {
                dur = 1.10f, atStart = () => SoundSystem.Play(SoundSystem.Sfx.Danger, 0.35f),
                draw = p =>
                {
                    float e = Ease(p);
                    ilPicA.gameObject.SetActive(false);
                    Shot(ilPicB, ilCastle, Mathf.Lerp(1f, 3.2f, e), Vector2.Lerp(PicMid, CastleGate, e));
                    Shade(new Color(dusk.r, dusk.g, dusk.b, 0.35f + e * 0.15f));
                    if (p > 0.75f) Shade(Color.Lerp(new Color(dusk.r, dusk.g, dusk.b, 0.5f), Color.black, (p - 0.75f) / 0.25f));
                }
            });
        // 階段を下る（ここで画面を迷宮に渡す＝幕はもう下りている）
        beats.Add(new Beat
        {
            dur = full ? 1.70f : 0.70f,
            atStart = () => { ilPicB.gameObject.SetActive(false); IlSwitch(); },
            draw = p =>
            {
                Shot(ilPicA, ilStairs, Mathf.Lerp(1f, 1.9f, p), Vector2.Lerp(new Vector2(170f, 100f), new Vector2(200f, 140f), p));
                float fade = Mathf.Max(0f, 1f - p * 5f) + Mathf.Max(0f, (p - 0.85f) / 0.15f);
                Shade(new Color(0, 0, 0, Mathf.Clamp01(fade)));
                int f = Mathf.Min(floors, Mathf.FloorToInt(p * floors) + 1);
                SetTxt(ilFloor, "B" + f + "F");
            }
        });
        if (full)
            beats.Add(new Beat
            {
                dur = 1.00f, atStart = () => SetTxt(ilFloor, ""),
                draw = p =>
                {
                    Shot(ilPicA, ilStudy, Mathf.Lerp(1.15f, 1f, Ease(p)), new Vector2(200f, 112f));
                    Shade(new Color(0, 0, 0, Mathf.Max(0f, 1f - p * 4f)));
                }
            });
        else beats.Add(new Beat { dur = 0f, atStart = () => SetTxt(ilFloor, "") });
        yield return RunBeats(beats, toDungeon, true);
    }
}
