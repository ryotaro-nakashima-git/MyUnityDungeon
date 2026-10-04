using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// 🖼️ タイトル画面の壁紙（承認済みの画面案 `CnrWAB93`）。
///
/// ⚠ 既定は **案A 断面図**（地上の村と、その下に重なる迷宮の層＝「迷宮を統べ、地上を侵す」がそのまま絵）。
///   ユーザーの希望で **案B 玉座の間** も設定画面から選べる。
/// ⚠ 選んだ壁紙は**周を越えて残る好み**なので `PlayerPrefs`（セーブには載せない）。
///   → [[replayability-phase-f]] 「周を越える層とセーブの層を混ぜない」。
/// </summary>
public static class TitleWallpaper
{
    public const int Count = 2;
    private const string Key = "title.wallpaper";

    public static string Name(int i) => i == 1 ? "玉座の間" : "断面図";
    public static string Desc(int i) => i == 1 ? "魔王と跪く配下、天井の穴から燃える地上の町" : "地上の村と、その下に重なる迷宮の層";

    public static int Current
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, Count - 1); }
        set { PlayerPrefs.SetInt(Key, Mathf.Clamp(value, 0, Count - 1)); PlayerPrefs.Save(); }
    }

    public static Sprite SpriteOf(int i) => SceneArt.Get(i == 1 ? "title_b" : "title_a");
}

/// <summary>
/// 🎬 タイトル画面の動き。**タイトルのページに付けて、ページが出ている間だけ動く**
/// （非アクティブなら `Update` は呼ばれない＝ゲーム中に余計な処理が走らない）。
///
/// <para>
/// 起動の流れ：黒 → 絵が浮かぶ → 英字 → 題字 → 線とひと言 → メニューが上から順に → 三本柱と戦績（約2.5秒）。
/// ⚠ **どのキー・クリックでも飛ばせる**（毎回2.5秒待たされるのは、慣れた人には邪魔）。
/// ⚠ 時間は `unscaledDeltaTime`（タイトル中に timeScale が 0 でも動く）→ [[ui-conventions]]。
/// </para>
/// </summary>
public class TitleScreenFx : MonoBehaviour
{
    // ── GameUIManager が組み立てて渡す部品 ──
    public RectTransform bg;               // 背景（少し大きめに置いて、ゆっくり寄る・流れる）
    public RectTransform emberLayer;       // 火の粉を置く層
    public Image veil;                     // 起動時の黒幕
    public TextMeshProUGUI logoAccent;     // 「統魔録」だけを脈打たせる
    public readonly List<CanvasGroup> stagger = new List<CanvasGroup>();   // 順に浮かぶもの
    public readonly List<float> staggerAt = new List<float>();
    public readonly List<Graphic> pillarLines = new List<Graphic>();
    public readonly List<CanvasGroup> pillars = new List<CanvasGroup>();
    public readonly List<TitleMenuRow> rows = new List<TitleMenuRow>();
    /// <summary>メニュー操作を止める条件（設定の窓が上に出ているときなど）。</summary>
    public System.Func<bool> blocked;

    private float t;
    private bool skipped;
    private int sel;
    private int pillarOn;
    private float pillarT;

    private struct Ember { public RectTransform rt; public Image img; public float v, w, a; }
    private readonly List<Ember> embers = new List<Ember>();

    private static readonly Color EmberCol = new Color(0.94f, 0.62f, 0.28f, 1f);

    /// <summary>起動の流れを頭から（タイトルへ戻ってきたときにも呼ぶ）。</summary>
    public void Replay()
    {
        t = 0f; skipped = false; pillarT = 0f; pillarOn = 0;
        Select(0);
        Apply();
    }

    private void OnEnable() { Replay(); }

    private void EnsureEmbers()
    {
        if (emberLayer == null || embers.Count > 0) return;
        for (int i = 0; i < 46; i++)
        {
            var go = new GameObject("ember", typeof(RectTransform));
            go.transform.SetParent(emberLayer, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var e = new Ember { rt = rt, img = img };
            Respawn(ref e, Random.value);
            embers.Add(e);
        }
    }

    private void Respawn(ref Ember e, float yFrac)
    {
        var size = emberLayer.rect.size;
        float s = Random.Range(2f, 5.5f);
        e.rt.sizeDelta = new Vector2(Mathf.Round(s), Mathf.Round(s));
        e.rt.anchoredPosition = new Vector2(Random.Range(size.x * 0.05f, size.x), yFrac * size.y - 12f);
        e.v = Random.Range(18f, 58f);      // px/秒
        e.w = Random.value * 6.28f;
        e.a = Random.Range(0.3f, 0.9f);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        t += dt;

        // ⏭ どのキー・クリックでも起動の流れを飛ばす
        if (!skipped && t < 2.6f && AnyPressed()) { skipped = true; }

        EnsureEmbers();
        Apply();

        // 🌫 背景：40秒で寄って戻る（往復）
        if (bg != null)
        {
            float k = Mathf.PingPong(Time.unscaledTime / 38f, 1f);
            k = k * k * (3f - 2f * k);
            float sc = Mathf.Lerp(1.02f, 1.08f, k);
            bg.localScale = new Vector3(sc, sc, 1f);
            var sz = bg.rect.size;
            bg.anchoredPosition = new Vector2(Mathf.Lerp(-0.012f, 0.012f, k) * sz.x, Mathf.Lerp(0f, 0.01f, k) * sz.y);
        }

        // 🔥 火の粉：下から舞い上がり、上で消える
        if (emberLayer != null)
        {
            float h = emberLayer.rect.height;
            for (int i = 0; i < embers.Count; i++)
            {
                var e = embers[i];
                var p = e.rt.anchoredPosition;
                e.w += dt * 1.2f;
                p.y += e.v * dt;
                p.x += Mathf.Sin(e.w) * 12f * dt;
                e.rt.anchoredPosition = new Vector2(Mathf.Round(p.x), p.y);
                float fade = Mathf.Clamp01(1f - p.y / (h * 0.95f)) * Mathf.Clamp01(p.y / 60f);
                var c = EmberCol; c.a = e.a * fade;
                e.img.color = c;
                if (p.y > h + 10f) Respawn(ref e, 0f);
                embers[i] = e;
            }
        }

        // 💓 「統魔録」が脈打つ
        if (logoAccent != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.25f);
            logoAccent.color = Color.Lerp(new Color(0.69f, 0.13f, 0.17f), new Color(0.90f, 0.30f, 0.34f), pulse);
        }

        // 🕯 三本柱：6秒ごとに1本ずつ灯る
        if (pillars.Count > 0)
        {
            pillarT += dt;
            if (pillarT >= 6f) { pillarT = 0f; pillarOn = (pillarOn + 1) % pillars.Count; }
            for (int i = 0; i < pillars.Count; i++)
            {
                float target = i == pillarOn ? 1f : 0.5f;
                pillars[i].alpha = Mathf.MoveTowards(pillars[i].alpha, target * Reveal(2.1f), dt * 1.5f);
                if (i < pillarLines.Count)
                    pillarLines[i].color = i == pillarOn ? new Color(0.89f, 0.66f, 0.29f, 0.9f) : new Color(0.92f, 0.89f, 0.95f, 0.18f);
            }
        }

        // ⌨️ メニュー：↑↓で選び、Enter/Space で決める
        if (blocked != null && blocked()) return;
        var kb = Keyboard.current;
        if (kb == null || rows.Count == 0) return;
        if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Select((sel + 1) % rows.Count);
        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Select((sel + rows.Count - 1) % rows.Count);
        if (t > 0.3f && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
        {
            if (sel >= 0 && sel < rows.Count && rows[sel] != null && rows[sel].onPick != null) rows[sel].onPick();
        }
    }

    /// <summary>その部品が出る時刻に対して、いまどこまで出ているか（0〜1）。飛ばしたら全部1。</summary>
    private float Reveal(float at)
    {
        if (skipped) return 1f;
        return Mathf.Clamp01((t - at) / 0.6f);
    }

    private void Apply()
    {
        if (veil != null)
        {
            float a = skipped ? 0f : 1f - Mathf.Clamp01(t / 1.6f);
            veil.color = new Color(0f, 0f, 0f, a);
            veil.raycastTarget = false;
            if (veil.gameObject.activeSelf != a > 0.001f) veil.gameObject.SetActive(a > 0.001f);
        }
        for (int i = 0; i < stagger.Count; i++)
        {
            float r = Reveal(staggerAt[i]);
            stagger[i].alpha = r;
            var rt = (RectTransform)stagger[i].transform;
            // ⚠ 位置は動かさない（置いた場所が正）。浮かぶ感じは中身の子で出す
            if (rt.childCount > 0)
            {
                var ch = rt.GetChild(0) as RectTransform;
                if (ch != null) ch.anchoredPosition = new Vector2(ch.anchoredPosition.x, -(1f - r) * 10f);
            }
        }
    }

    private static bool AnyPressed()
    {
        var kb = Keyboard.current; var ms = Mouse.current;
        return (kb != null && kb.anyKey.wasPressedThisFrame) || (ms != null && ms.leftButton.wasPressedThisFrame);
    }

    public void Select(int i)
    {
        sel = Mathf.Clamp(i, 0, Mathf.Max(0, rows.Count - 1));
        for (int k = 0; k < rows.Count; k++) if (rows[k] != null) rows[k].SetSelected(k == sel);
    }

    public void SelectRow(TitleMenuRow r) { int i = rows.IndexOf(r); if (i >= 0) Select(i); }
}

/// <summary>タイトルのメニュー1行。hover で選択が移る（キーボードと同じ見た目になる）。</summary>
public class TitleMenuRow : MonoBehaviour, IPointerEnterHandler
{
    public TitleScreenFx owner;
    public Image glow;             // 左から伸びる赤い光の帯
    public Image bar;              // 左端の赤い縦線
    public TextMeshProUGUI mark;   // ◆
    public RectTransform label;    // 選ぶと少し右へ
    public float labelX;
    public System.Action onPick;

    public void OnPointerEnter(PointerEventData e) { if (owner != null) owner.SelectRow(this); }

    public void SetSelected(bool on)
    {
        if (glow != null) glow.enabled = on;
        if (bar != null) bar.enabled = on;
        if (mark != null) mark.enabled = on;
        if (label != null) label.anchoredPosition = new Vector2(labelX + (on ? 10f : 0f), label.anchoredPosition.y);
    }
}
