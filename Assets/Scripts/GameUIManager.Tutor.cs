using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🗣️ 案内役（段E）。最初の3ターン、腹心が顔と声つきで画面の下から話しかけ、**押す場所を光らせて**案内する。
///
/// - 1手ずつ：言う → 光らせる → プレイヤーがその手を打つ（または吹き出しを押す）→ 次へ。
/// - 字幕は漢字まじり、声は `Resources/Audio/Voice/v_tut_NN`（**読み上げ原稿はひらがなだけ** → [[tts-hiragana-only]]）。
///   声のファイルが無ければ字幕だけで進む（`SoundSystem.PlayVoice` は無ければ黙る）。
/// - 口は、喋っている間（声があれば声の長さ、無ければ文字を出している間）だけ動かす。ときどき瞬きする。
/// - 案内が出ているあいだ（3ターンまで）は『腹心の報告』の窓を自動では開かない（同じ人が二度話す形になる）。
/// - 一度最後まで見たら、次の周からは出さない（PlayerPrefs `tutor.done`）。設定で切れる／もう一度見られる。
/// ⚠ 計測中は出さない。⚠ 時間は unscaled（戦闘の倍速・一時停止に引きずられない）。
/// <para>`GameUIManager` の partial。</para>
/// </summary>
public partial class GameUIManager
{
    // ============ 設定（周を越える＝PlayerPrefs） ============
    private const string TutorDonePref = "tutor.done", TutorOnPref = "tutor.enabled";
    public static bool TutorEnabled
    {
        get { return PlayerPrefs.GetInt(TutorOnPref, 1) == 1; }
        set { PlayerPrefs.SetInt(TutorOnPref, value ? 1 : 0); PlayerPrefs.Save(); }
    }
    private static bool TutorDone
    {
        get { return PlayerPrefs.GetInt(TutorDonePref, 0) == 1; }
        set { PlayerPrefs.SetInt(TutorDonePref, value ? 1 : 0); PlayerPrefs.Save(); }
    }
    /// <summary>設定の「もう一度見る」。次に始めた周（またはいまの周の3ターン目まで）で最初から出す。</summary>
    public void ReplayTutor() { TutorDone = false; tutorIndex = 0; tutorShowing = false; HideTutor(); }

    /// <summary>案内がまだ続いているか（報告の窓を自動で開かないため）。</summary>
    private bool TutorActive => TutorEnabled && !TutorDone && !MeasureMode.On && tutorIndex < tutorSteps.Count
                                && turn != null && turn.CurrentTurn <= 3;

    // ============ 手順 ============
    private class TutorStep
    {
        public int turn;                              // このターンのうちに出す（過ぎたら飛ばす）
        public string text, voice;                    // 字幕（漢字まじり）／声の id
        public System.Func<bool> when;                // 出してよい場面か
        public System.Func<RectTransform> target;     // 光らせる所（null＝光らせない）
        public System.Func<bool> done;                // これが真になったら次へ（null＝押して次へだけ）
        public float autoClose;                       // >0 なら、その秒数で自動で次へ（戦闘開始の一言）
        public System.Action onShow;                  // 出した瞬間に覚えておくこと
    }
    private readonly List<TutorStep> tutorSteps = new List<TutorStep>();
    private int tutorIndex;
    private bool tutorShowing;
    private float tutorShownAt;
    private int tutorBaseInt; private string tutorBaseStr;

    private RectTransform MenuTarget(string label)
    {
        Button b;
        if (menuButtons.TryGetValue(label, out b) && b != null && b.gameObject.activeInHierarchy) return (RectTransform)b.transform;
        return strategyGrp != null ? strategyGrp.rectTransform : null;   // 戦略の帯の中に畳まれているときは「戦略」を指す
    }

    private void BuildTutorSteps()
    {
        tutorSteps.Clear();
        System.Func<bool> dungeon = () => turn != null && turn.IsDungeonPhase && !surfaceModeOn;
        System.Func<bool> surface = () => turn != null && turn.IsSurfacePhase && surfaceModeOn;

        tutorSteps.Add(new TutorStep
        {
            turn = 1, voice = "v_tut_01", when = dungeon, target = () => placeGrp != null ? placeGrp.rectTransform : null,
            text = "お目覚めですか、我が主。まずは罠を一つ、通路に仕掛けましょう。\n<size=85%><color=#9c95b4>『配置』から罠を選び、通路のマスを押します。</color></size>",
            onShow = () => tutorBaseInt = featureMgr != null ? featureMgr.PlacedCount : 0,
            done = () => featureMgr != null && featureMgr.PlacedCount > tutorBaseInt
        });
        tutorSteps.Add(new TutorStep
        {
            turn = 1, voice = "v_tut_02", when = dungeon, target = () => MenuTarget("図鑑"),
            text = "配下も一体、入口の近くへ。\n<size=85%><color=#9c95b4>『魔物』から配下を選び、マスに置きます。</color></size>",
            onShow = () => tutorBaseStr = featureMgr != null ? featureMgr.PlacedIndividualsSig() : "",
            done = () => featureMgr != null && featureMgr.PlacedIndividualsSig() != tutorBaseStr
        });
        tutorSteps.Add(new TutorStep
        {
            turn = 1, voice = "v_tut_03", when = dungeon, target = () => dungEndBtnRt,
            text = "整いました。冒険者を迎え入れましょう。\n<size=85%><color=#9c95b4>『侵略開始』で波が始まります。</color></size>",
            done = () => turn != null && !turn.IsDungeonPhase
        });
        tutorSteps.Add(new TutorStep
        {
            turn = 1, voice = "v_wave_start", when = () => turn != null && turn.IsBattlePhase,
            text = "来ます。――迎え撃つご用意を。", autoClose = 3.5f
        });
        tutorSteps.Add(new TutorStep
        {
            turn = 1, voice = "v_tut_04", when = () => ReportOpen,
            target = () => reportPanel != null ? (RectTransform)reportPanel.transform : null,
            text = "凌ぎました。倒した数だけ、DPと研究点が入ります。\n<size=85%><color=#9c95b4>決算を閉じると、地上へ出ます。</color></size>",
            done = () => !ReportOpen
        });
        tutorSteps.Add(new TutorStep
        {
            turn = 1, voice = "v_tut_05", when = surface,
            target = () => unitRoster != null && unitRoster.gameObject.activeInHierarchy ? unitRoster.rectTransform : null,
            text = "地上です。眷属を選び、水色の所へ歩かせてください。\n<size=85%><color=#9c95b4>左下の列か、盤のユニットを押して選びます。</color></size>",
            onShow = () => tutorBaseInt = UnitOrders.WaitingCount(),
            done = () => UnitOrders.WaitingCount() < tutorBaseInt
        });
        tutorSteps.Add(new TutorStep
        {
            turn = 1, voice = "v_tut_06", when = surface,
            target = () => surfNextBtn != null && surfNextBtn.gameObject.activeInHierarchy ? (RectTransform)surfNextBtn.transform : null,
            text = "拠点で作る物を選びましょう。\n<size=85%><color=#9c95b4>右下の大きなボタンが、いま打てる手を案内します。</color></size>",
            done = () => { var st = NextAction.Surface(); return st.none || !st.label.StartsWith("生産"); }
        });
        tutorSteps.Add(new TutorStep
        {
            turn = 1, voice = "v_tut_07", when = surface, target = () => surfEndBtnRt,
            text = "今日はここまで。ターンを終えましょう。",
            done = () => turn != null && turn.CurrentTurn > 1
        });
        tutorSteps.Add(new TutorStep
        {
            turn = 2, voice = "v_tut_08", when = dungeon, target = () => MenuTarget("研究"),
            text = "研究点が貯まりました。研究で、新しい手が増えます。\n<size=85%><color=#9c95b4>『戦略』の中の『研究』です。</color></size>",
            done = () => researchPanel != null && researchPanel.activeSelf
        });
        tutorSteps.Add(new TutorStep
        {
            turn = 2, voice = "v_tut_09", when = dungeon, target = () => MenuTarget("先触れ"),
            text = "先触れで、次に来る者たちを覗けます。\n<size=85%><color=#9c95b4>備えも、ここで選べます。</color></size>",
            done = () => omenPanel != null && omenPanel.activeSelf
        });
        tutorSteps.Add(new TutorStep
        {
            turn = 3, voice = "v_tut_10", when = dungeon, target = () => MenuTarget("魔王"),
            text = "魔王様ご自身も、育てられます。\n<size=85%><color=#9c95b4>BPで力を伸ばし、条件がそろえば種族が進化します。</color></size>",
            done = () => demonPanel != null && demonPanel.activeSelf
        });
        tutorSteps.Add(new TutorStep
        {
            turn = 3, voice = "v_tut_11", when = surface,
            text = "ここからは、主のお心のままに。私は、いつでもおそばに。\n<size=85%><color=#9c95b4>困ったときは、右下の大きなボタンと『報告』を。</color></size>"
        });
    }

    // ============ 見た目 ============
    private RectTransform tutorRoot;
    private Image tutorBubble, tutorFace, tutorHiFrame;
    private TextMeshProUGUI tutorText, tutorArrow, tutorFoot;
    private Sprite[] tutorFaces;   // 0=閉じ 1=半開き 2=開き 3=瞬き
    private string tutorFull = "";
    private float tutorTyped;
    private float tutorBlinkAt, tutorMouthAt; private int tutorMouth;

    private void BuildTutor()
    {
        BuildTutorSteps();
        tutorFaces = new[]
        {
            Resources.Load<Sprite>("UI/Guide/aide_closed"), Resources.Load<Sprite>("UI/Guide/aide_half"),
            Resources.Load<Sprite>("UI/Guide/aide_open"), Resources.Load<Sprite>("UI/Guide/aide_blink"),
        };
        // ⚠ 迷宮(100)・地上(110)・説明(200)より上、幕間(250)より下
        tutorRoot = MakeCanvas("TutorCanvas", 240);

        // 光らせる枠と矢印（押す所を指す）
        // ⚠ 枠は細い棒4本で作る（縁取りの部品だと中まで塗られて、押す所が隠れる）
        tutorHiFrame = Panel(tutorRoot, "TutorHi", new Color(0, 0, 0, 0));
        tutorHiFrame.raycastTarget = false;
        tutorHiFrame.rectTransform.anchorMin = tutorHiFrame.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        var gold = new Color(1f, 0.82f, 0.29f, 1f);
        for (int i = 0; i < 4; i++)
        {
            var bar = Panel(tutorHiFrame, "Edge" + i, gold); bar.raycastTarget = false;
            var r = bar.rectTransform; const float th = 4f;
            if (i == 0) { r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(0, th); }
            else if (i == 1) { r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(0.5f, 0); r.sizeDelta = new Vector2(0, th); }
            else if (i == 2) { r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 0.5f); r.sizeDelta = new Vector2(th, 0); }
            else { r.anchorMin = new Vector2(1, 0); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 0.5f); r.sizeDelta = new Vector2(th, 0); }
            r.anchoredPosition = Vector2.zero;
        }
        tutorHiFrame.gameObject.SetActive(false);
        tutorArrow = Text(tutorRoot, "▼", 30f, C("#ffd24a"), TextAlignmentOptions.Center, FontStyles.Bold);
        tutorArrow.raycastTarget = false;
        tutorArrow.rectTransform.anchorMin = tutorArrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        tutorArrow.rectTransform.sizeDelta = new Vector2(40f, 40f);
        tutorArrow.gameObject.SetActive(false);

        // 吹き出し（左下・下の帯の上）
        tutorBubble = Panel(tutorRoot, "TutorBubble", new Color(0.10f, 0.09f, 0.15f, 0.97f));
        var brt = tutorBubble.rectTransform;
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0f, 0f);
        brt.anchoredPosition = new Vector2(24f, 96f);
        brt.sizeDelta = new Vector2(720f, 156f);
        Outline(tutorBubble, GOLD);
        var face = Panel(tutorBubble, "FaceBg", C("#1b1826"));
        Place(face.rectTransform, 12, 12, 132, 132); Outline(face, GOLD_DK); face.raycastTarget = false;
        tutorFace = new GameObject("Face", typeof(RectTransform)).AddComponent<Image>();
        tutorFace.rectTransform.SetParent(face.rectTransform, false);
        tutorFace.raycastTarget = false; tutorFace.preserveAspect = true;
        Place(tutorFace.rectTransform, 2, 2, 128, 128);
        var who = Text(tutorBubble, "腹心", 15f, C("#ffd24a"), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(who.rectTransform, 158, 12, 300, 22);
        tutorText = Text(tutorBubble, "", 16f, TEXT, TextAlignmentOptions.TopLeft);
        Place(tutorText.rectTransform, 158, 38, 548, 86);
        tutorText.raycastTarget = false;
        tutorFoot = Text(tutorBubble, "", 11.5f, FAINT, TextAlignmentOptions.BottomLeft);
        Place(tutorFoot.rectTransform, 158, 126, 360, 20);
        // ⏭️ 吹き出しを押す＝文字を全部出す／次へ
        var btn = tutorBubble.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(TutorClick);
        var skip = PrimaryButton(tutorBubble, "案内を飛ばす", PANEL2, MUTED, () => { TutorDone = true; HideTutor(); });
        Place((RectTransform)skip.transform, 720 - 12 - 130, 120, 130, 26);
        var sl = skip.GetComponentInChildren<TMP_Text>(); if (sl != null) sl.fontSize = 12f;

        tutorRoot.gameObject.SetActive(false);
    }

    private void HideTutor()
    {
        tutorShowing = false;
        if (tutorRoot != null) tutorRoot.gameObject.SetActive(false);
    }

    private void TutorClick()
    {
        if (!tutorShowing) return;
        if (tutorTyped < tutorFull.Length) { tutorTyped = tutorFull.Length; return; }   // まず全部出す
        TutorNext();
    }

    private void TutorNext()
    {
        tutorShowing = false;
        tutorIndex++;
        if (tutorIndex >= tutorSteps.Count) { TutorDone = true; HideTutor(); }
    }

    private void ShowTutorStep(TutorStep s)
    {
        tutorShowing = true;
        tutorShownAt = Time.unscaledTime;
        tutorFull = s.text; tutorTyped = 0f;
        if (s.onShow != null) s.onShow();
        tutorRoot.gameObject.SetActive(true);
        SetTxt(tutorFoot, s.done != null ? "その手を打つと次へ進みます（押すと次へ）" : s.autoClose > 0f ? "" : "押すと次へ");
        SoundSystem.PlayVoice(s.voice);
        tutorBlinkAt = Time.unscaledTime + Random.Range(2.5f, 4.5f);
    }

    /// <summary>毎フレーム（`Update` から）。</summary>
    private void TickTutor()
    {
        if (tutorRoot == null) return;
        if (!TutorActive || !GameSetup.Started || VictorySystem.Decided) { if (tutorShowing || tutorRoot.gameObject.activeSelf) HideTutor(); return; }

        // 過ぎたターンの手は飛ばす（途中から始めた・飛ばした手が残らないように）
        int t = turn.CurrentTurn;
        while (tutorIndex < tutorSteps.Count && tutorSteps[tutorIndex].turn < t) { tutorIndex++; tutorShowing = false; }
        if (tutorIndex >= tutorSteps.Count) { TutorDone = true; HideTutor(); return; }
        var s = tutorSteps[tutorIndex];

        // 他の窓・幕間・決算（決算の手を除く）が出ているあいだは引っ込める
        bool blocked = InterludePlaying || OpeningPlaying || (titleRoot != null && titleRoot.activeSelf)
                       || TopChromePanel() != null || (ReportOpen && s.voice != "v_tut_04") || GachaRevealing;
        bool ok = !blocked && s.turn == t && (s.when == null || s.when());
        if (!ok)
        {
            if (tutorRoot.gameObject.activeSelf) tutorRoot.gameObject.SetActive(false);
            // ⚠ 出ていた手の「済んだか」は、引っ込めている間も見る（窓の中で済ませることがある）
            if (tutorShowing && s.done != null && s.done()) TutorNext();
            return;
        }
        if (!tutorShowing) ShowTutorStep(s);
        else if (!tutorRoot.gameObject.activeSelf) tutorRoot.gameObject.SetActive(true);

        // 文字を1字ずつ
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (tutorTyped < tutorFull.Length) { tutorTyped = Mathf.Min(tutorFull.Length, tutorTyped + dt / 0.035f); }
        SetTxt(tutorText, VisibleText(tutorFull, Mathf.FloorToInt(tutorTyped)));

        // 口と瞬き
        bool talking = SoundSystem.VoiceBusy || tutorTyped < tutorFull.Length;
        float now = Time.unscaledTime;
        if (talking && now >= tutorMouthAt) { tutorMouth = tutorMouth == 0 ? Random.Range(1, 3) : (Random.value < 0.5f ? 0 : 3 - tutorMouth); tutorMouthAt = now + 0.09f; }
        if (!talking) tutorMouth = 0;
        int frame = tutorMouth;
        if (now >= tutorBlinkAt) { if (now < tutorBlinkAt + 0.12f) { if (frame == 0) frame = 3; } else tutorBlinkAt = now + Random.Range(2.5f, 4.5f); }
        if (tutorFaces != null && tutorFaces[frame] != null) tutorFace.sprite = tutorFaces[frame];

        // 押す所を光らせる
        var target = s.target != null ? s.target() : null;
        bool showHi = target != null && target.gameObject.activeInHierarchy && CanvasShown(target.gameObject);
        tutorHiFrame.gameObject.SetActive(showHi);
        tutorArrow.gameObject.SetActive(showHi);
        if (showHi) PlaceHighlight(target, now);
        PlaceBubbleAvoiding(showHi ? target : null);

        // 済んだら次へ／時間で次へ
        if (s.done != null && s.done()) { TutorNext(); return; }
        if (s.autoClose > 0f && now - tutorShownAt > s.autoClose) TutorNext();
    }

    /// <summary>
    /// 💬 吹き出しを、押す場所・開いている棚・配置の帯・選んでいる物の欄と**重ならない位置**へ置く。
    /// ⚠ 左下に固定していたら、「配置」を開いた棚と「部隊」の帯が吹き出しの下に隠れて押せなかった（ユーザー指摘）。
    /// 候補は 左下（いつもの場所）→ 左上（上の帯の下）→ 上の真ん中。どれも塞ぐなら左下のまま。
    /// </summary>
    private void PlaceBubbleAvoiding(RectTransform target)
    {
        if (tutorBubble == null) return;
        var avoid = new List<Rect>();
        System.Action<GameObject> add = go =>
        {
            if (go == null || !go.activeInHierarchy || !CanvasShown(go)) return;
            var rt = go.transform as RectTransform; if (rt == null) return;
            avoid.Add(ScreenRectOf(rt));
        };
        if (target != null) avoid.Add(ScreenRectOf(target));
        add(placeTray); add(squadStrip); add(bossStrip); add(trapStrip); add(totemStrip);
        add(specialStrip); add(habitatStrip); add(greatWorkStrip);
        float H = tutorRoot.rect.height, W = tutorRoot.rect.width;
        var cands = new[] { new Vector2(24f, 96f), new Vector2(96f, H - 80f - 156f), new Vector2((W - 720f) * 0.5f, H - 150f - 156f) };
        var brt = tutorBubble.rectTransform;
        foreach (var c in cands)
        {
            brt.anchoredPosition = c;
            var r = ScreenRectOf(brt);
            bool hit = false;
            foreach (var a in avoid) if (a.Overlaps(r)) { hit = true; break; }
            if (!hit) return;
        }
        brt.anchoredPosition = cands[0];
    }

    private static Rect ScreenRectOf(RectTransform rt)
    {
        var cv = rt.GetComponentInParent<Canvas>();
        Camera cam = (cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay) ? cv.worldCamera : null;
        var c = new Vector3[4]; rt.GetWorldCorners(c);
        var p0 = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
        var p2 = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
        return Rect.MinMaxRect(p0.x, p0.y, p2.x, p2.y);
    }

    /// <summary>リッチテキストのタグを壊さずに、先頭から n 文字だけ見せる。</summary>
    private static string VisibleText(string s, int n)
    {
        if (n >= s.Length) return s;
        var sb = new System.Text.StringBuilder();
        int shown = 0; bool tag = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '<') tag = true;
            if (tag) { sb.Append(c); if (c == '>') tag = false; continue; }
            if (shown >= n) continue;
            sb.Append(c); shown++;
        }
        return sb.ToString();
    }

    private void PlaceHighlight(RectTransform target, float now)
    {
        var cv = target.GetComponentInParent<Canvas>();
        Camera cam = (cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay) ? cv.worldCamera : null;
        var c = new Vector3[4]; target.GetWorldCorners(c);
        Vector2 a, b;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(tutorRoot, RectTransformUtility.WorldToScreenPoint(cam, c[0]), null, out a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(tutorRoot, RectTransformUtility.WorldToScreenPoint(cam, c[2]), null, out b);
        float pulse = 6f + 4f * Mathf.Sin(now * 5f);
        var rt = tutorHiFrame.rectTransform;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x) + pulse * 2f, Mathf.Abs(b.y - a.y) + pulse * 2f);
        // 矢印：上に余白があれば上から下向き、無ければ下から上向き
        float halfH = tutorRoot.rect.height * 0.5f;
        bool fromAbove = b.y + 60f < halfH;
        tutorArrow.text = fromAbove ? "▼" : "▲";
        float bob = Mathf.Sin(now * 6f) * 5f;
        tutorArrow.rectTransform.anchoredPosition = new Vector2((a.x + b.x) * 0.5f, fromAbove ? b.y + 28f + bob : a.y - 28f - bob);
    }
}
