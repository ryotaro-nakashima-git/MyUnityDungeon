using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 💥 **撃破の手応え**（D-4）。倒した瞬間に、画面が揺れ・弾け・数字が飛ぶ。
///
/// <para>
/// ⚠⚠ **なぜ要るか（実測の裏付け）**：通しプレイ T1〜T30 で DP は **104,205** 動いたのに、
///   撃破の瞬間に起きることは「静かに数字が増える」だけだった。
///   30ターンで数百体倒しても、**倒した実感がどこにも無い**。
///   ゲームの手応えは「盤面が良くなること」ではなく「**押した指に返ってくること**」で出る。
/// </para>
///
/// <para>
/// ⚠ **強さには一切触らない。** ここは演出だけ。報酬の値は `AdventurerAI` が決め、
///   この system は**決まった値を見せるだけ**。演出のついでに報酬を足すと、
///   難易度カーブの軸が1本増える → [[difficulty-curve-orders]]。
/// </para>
///
/// <para>
/// ⚠ 時間は `Time.deltaTime`（`unscaled` ではない）。ここは戦闘の一部なので、
///   倍速なら速く、一時停止なら止まるのが正しい → [[ui-conventions]]。
/// </para>
///
/// 関連: [[playtest-t1-t30]]（この機能の根拠） [[FloatText]] [[BattleVfx]] [[ScreenShake]]。
/// </summary>
public static class KillFeedback
{
    // ── ノブ（見た目だけ。強さには効かない）──
    /// <summary>連撃が途切れる間隔（秒）。⚠ 波の湧きの間隔より短くする（→ [[combat-math]]）。</summary>
    public const float ComboWindow = 2.6f;
    /// <summary>連撃が「囃される」ようになる本数。ここから文字が出る。</summary>
    public const int ComboShout = 3;
    private const float ShakeBase = 0.055f;
    private const float ShakeMax = 0.34f;

    private static int combo;
    private static float comboUntil;
    private static int waveBest;
    /// <summary>この波で一番伸びた連撃。⚠ 波の終わりに読んでから `NewWave` で畳む。</summary>
    public static int WaveBest { get { return waveBest; } }
    /// <summary>いまの連撃数（0＝途切れている）。UIから覗ける。</summary>
    public static int Combo { get { return Time.time <= comboUntil ? combo : 0; } }

    private static Sprite sprImpact, sprRays;
    private static bool loaded;
    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        sprImpact = Resources.Load<Sprite>("Fx/impact_burst");
        sprRays = Resources.Load<Sprite>("Fx/burst_rays");
    }

    /// <summary>周をまたいで連撃を持ち越さない。</summary>
    public static void Reset() { combo = 0; comboUntil = 0f; waveBest = 0; }
    /// <summary>波の頭で最高記録を畳む。⚠ `Reset`（周の頭）と混ぜない。</summary>
    public static void NewWave() { combo = 0; comboUntil = 0f; waveBest = 0; }

    /// <summary>
    /// 💥 冒険者を1体倒したときに1回だけ呼ぶ。
    /// ⚠ **生け捕りでは呼ばない**（捕らえるのは「倒した」ではないので、手応えも別物）。
    /// </summary>
    /// <param name="pos">倒れた位置（ワールド）。</param>
    /// <param name="dp">入った撃破DP。</param>
    /// <param name="mats">入った素材。</param>
    /// <param name="rank">冒険者のランク 0〜7。大物ほど大きく揺れる。</param>
    /// <param name="named">因縁（名のある者）か。</param>
    public static void OnKill(Vector3 pos, int dp, int mats, int rank, bool named)
    {
        Load();

        // 🔗 連撃：**途切れる前に次を倒す**と伸びる。窓は湧きの間隔より短いので、
        //    「まとめて誘い込んで一気に潰す」プレイだけが伸ばせる。
        combo = (Time.time <= comboUntil) ? combo + 1 : 1;
        comboUntil = Time.time + ComboWindow;
        if (combo > waveBest) waveBest = combo;

        // 💢 画面の揺れ。ランクと連撃で強くなるが**必ず頭打ちにする**（乱戦で酔わせない）
        float amp = ShakeBase * (1f + rank * 0.28f) * (1f + Mathf.Min(combo, 8) * 0.10f);
        if (named) amp *= 1.9f;
        ScreenShake.Kick(Mathf.Min(amp, ShakeMax), named ? 0.34f : 0.20f);

        // 💥 弾ける絵。⚠ 段は3つ：**買ったパーティクル → 自作スプライト → 手続きの円**。
        //   上から順に「あれば使う」（→ [[FxPrefabs]]）。素材が増えるほど勝手に良くなる。
        // ⚠ 倍率は**盤の1マス基準**。素で出すと2マス分を覆って何が起きたか読めない（実測）。
        if (!FxPrefabs.Play(named ? FxPrefabs.Burst : FxPrefabs.Explosion, pos, named ? 0.85f : 0.5f))
        {
            if (sprImpact != null) FxSprite.Pop(sprImpact, pos, named ? 3.2f : 1.9f, 0.30f, Color.white);
            else BattleVfx.Burst(pos, new Color(1f, 0.72f, 0.35f), named ? 1.2f : 0.7f);
        }
        if (named && sprRays != null) FxSprite.Pop(sprRays, pos, 5.0f, 0.55f, new Color(1f, 0.86f, 0.45f), true);

        // 🪙 実り。⚠ **DPと素材を別の高さに出す**（重なると読めない）
        //   🎲 同時に2体倒れることがあるので少しばらす。
        //   ⚠ 一時「乱戦では数字を出さない」ようにしたが、**それは私の誤診だった**。
        //     スクショで見た「810028」という塊は重なりではなく、デバッグの一撃(999999)の
        //     軽減後の実値そのもの。実戦の数字は2〜3桁で、重なりは起きていない。
        //     **見えた症状を疑う前に、数字の出どころを確かめること。**
        var j = new Vector3(VisualRandom.Range(-0.22f, 0.22f), VisualRandom.Range(-0.10f, 0.10f), 0f);
        if (dp > 0) FloatText.Spawn(pos + new Vector3(0.28f, 0.30f, 0f) + j, "+" + dp, UITheme.DP, 2.5f, 1.25f, 1.0f);
        if (mats > 0) FloatText.Spawn(pos + new Vector3(-0.30f, 0.06f, 0f) + j, "+" + mats, UITheme.Material, 2.1f, 1.05f, 1.0f);
        // 💰 硬貨と塊が弾けて、上のチップへ吸い込まれる（→ [[LootBurst]]）。
        //    ⚠ これが**盤の出来事とHUDの数字を繋ぐ線**。数字を出すだけだと別々の出来事に見える。
        LootBurst.Spawn(pos, dp, mats);

        // 🔥 連撃の囃し。3本目から出し、以降は**伸びるたびに**大きくなる
        if (combo >= ComboShout)
        {
            float s = Mathf.Min(2.6f + (combo - ComboShout) * 0.35f, 5.4f);
            var c = combo >= 8 ? new Color(1f, 0.55f, 0.30f) : new Color(1f, 0.82f, 0.42f);
            FloatText.Spawn(pos + new Vector3(0f, 0.95f, 0f), combo + " 連", c, s, 0.7f, 0.9f);
            SoundSystem.Play(SoundSystem.Sfx.Kill, Mathf.Min(0.55f + combo * 0.04f, 1f),
                Mathf.Min(0.95f + combo * 0.045f, 1.6f));   // 🔊 伸びるほど音が高くなる
        }
        else SoundSystem.Play(SoundSystem.Sfx.Kill, 0.6f, 1f);
    }

    /// <summary>
    /// 🎁 **奪還**（G-1）。戦利品を抱えた相手を、持ち出される前に仕留めた。
    ///
    /// ⚠ **報酬は足さない。** 素材は `AdventurerAI` の `droppedMaterials` に既に入っている。
    ///   ここは「取り返した」という事実を**見せるだけ**。
    /// ⚠ 逃走中を仕留めたときだけ大きく出す ―― そこが**ぎりぎりの攻防**だから。
    ///   探索中に倒したぶんまで同じ大きさで祝うと、山が平らになる。
    /// </summary>
    public static void OnRecover(Vector3 pos, int materials, bool wasFleeing)
    {
        if (materials <= 0) return;
        Load();
        var gold = new Color(1f, 0.84f, 0.40f);
        if (wasFleeing)
        {
            FloatText.Spawn(pos + new Vector3(0f, 1.35f, 0f), "奪還！ 素材 +" + materials, gold, 3.4f, 1.1f, 1.25f);
            if (sprRays != null) FxSprite.Pop(sprRays, pos, 3.6f, 0.45f, gold, true);
            ScreenShake.Kick(0.18f, 0.26f);
            SoundSystem.Play(SoundSystem.Sfx.Gain, 1f, 1.15f);
        }
        else
        {
            FloatText.Spawn(pos + new Vector3(0f, 1.25f, 0f), "戦利品 +" + materials, gold, 2.3f, 0.95f, 1.0f);
        }
    }

    /// <summary>
    /// 🗡️ **決着**（G-2）。名のある冒険者を討ち取った。
    ///
    /// ⚠⚠ この見返り（Lv20・逃走3回でおよそ 860DP）は `AdventurerAI` の `killBonusDP`（数十）とは
    ///   **別枠**で、これまで**画面右の通知にしか出ていなかった**。1周で一番大きい数字が盤に出ないので、
    ///   「やっと討ち取った」という山が立たなかった。
    /// ⚠ **数字は足していない。** `Nemesis.OnSlain` が確定させた値を見せるだけ。
    /// </summary>
    public static void OnNemesisSlain(Vector3 pos, string name, int dp, int mat, float gearBack)
    {
        Load();
        var gold = new Color(1f, 0.86f, 0.42f);
        if (sprRays != null) FxSprite.Pop(sprRays, pos, 7.0f, 0.75f, gold, true);
        ScreenShake.Kick(0.32f, 0.45f);
        FloatText.Spawn(pos + new Vector3(0f, 1.75f, 0f), name + " 討伐", gold, 4.2f, 1.0f, 1.6f);
        FloatText.Spawn(pos + new Vector3(0.35f, 0.85f, 0f), "+" + dp, UITheme.DP, 4.6f, 1.3f, 1.5f);
        if (mat > 0) FloatText.Spawn(pos + new Vector3(-0.45f, 0.35f, 0f), "+" + mat, UITheme.Material, 3.0f, 1.1f, 1.4f);
        LootBurst.Spawn(pos, dp, mat);   // 💰 決着は一番大きい山なので、ここでも降らせる
        // 🎁 世界から装備を取り返した ―― **装備水準が下がるのはここだけ**なので、必ず見せる
        if (gearBack > 0.05f)
            FloatText.Spawn(pos + new Vector3(0f, -0.25f, 0f), "装備水準 -" + gearBack.ToString("0.0"),
                new Color(0.62f, 0.85f, 1f), 2.6f, 0.9f, 1.5f);
        SoundSystem.Play(SoundSystem.Sfx.Story, 1f, 0.9f);
    }

    /// <summary>🔮 召喚の演出。魔法陣が広がって、そこから出てくる。</summary>
    public static void OnSummon(Vector3 pos, bool rare)
    {
        Load();
        // ✨ 魔法陣は買ったパーティクルを優先（→ [[FxPrefabs]]）
        if (!FxPrefabs.Play(FxPrefabs.Circle, pos, rare ? 0.8f : 0.55f))
        {
            var circle = Resources.Load<Sprite>("Fx/summon_circle");
            if (circle != null) FxSprite.Pop(circle, pos, rare ? 4.2f : 2.8f, rare ? 0.85f : 0.6f,
                rare ? new Color(1f, 0.85f, 0.45f) : Color.white, true);
        }
        if (rare && sprRays != null) FxSprite.Pop(sprRays, pos, 5.5f, 0.7f, new Color(1f, 0.9f, 0.5f), true);
        ScreenShake.Kick(rare ? 0.16f : 0.05f, 0.25f);
        SoundSystem.Play(SoundSystem.Sfx.Discover, rare ? 1f : 0.7f, rare ? 0.9f : 1.15f);
    }
}

/// <summary>
/// 💢 カメラの揺れ。⚠ `CameraController` が `Update` で `transform.position` に**足し込む**ので、
/// ここは **LateUpdate の頭で前フレームのぶんを引いてから**新しい offset を足す。
/// そうしないと揺れが座標に residue として溜まり、盤がじわじわ流れていく。
/// </summary>
public class ScreenShake : MonoBehaviour
{
    private static ScreenShake inst;
    private Vector3 applied;
    private float t, dur, amp;

    /// <summary>揺らす。強い方を採用する（弱い揺れが強い揺れを上書きしない）。</summary>
    public static void Kick(float amplitude, float duration)
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (inst == null || inst.gameObject != cam.gameObject)
        {
            inst = cam.GetComponent<ScreenShake>();
            if (inst == null) inst = cam.gameObject.AddComponent<ScreenShake>();
        }
        if (amplitude * duration < inst.amp * inst.t) return;
        inst.amp = amplitude; inst.dur = inst.t = duration;
    }

    private void LateUpdate()
    {
        transform.position -= applied;      // ⚠ 先に前フレームぶんを戻す
        applied = Vector3.zero;
        if (t <= 0f) return;
        t -= Time.deltaTime;
        if (t <= 0f) { t = 0f; return; }
        float k = t / Mathf.Max(0.0001f, dur);              // 1→0（減衰）
        float a = amp * k * k;
        applied = new Vector3(VisualRandom.Range(-a, a), VisualRandom.Range(-a, a), 0f);
        transform.position += applied;
    }
}

/// <summary>
/// ✨ 1枚絵を「弾けさせて消す」だけの短命オブジェクト。プールで回す（乱戦で毎回 new しない）。
/// </summary>
public class FxSprite : MonoBehaviour
{
    private static readonly Stack<FxSprite> pool = new Stack<FxSprite>();
    private static Transform root;
    private SpriteRenderer sr;
    private float t, dur, size;
    private bool spin;

    public static void Pop(Sprite spr, Vector3 pos, float size, float dur, Color tint, bool spin = false)
    {
        if (spr == null) return;
        if (root == null) { var g = new GameObject("FxSprites"); Object.DontDestroyOnLoad(g); root = g.transform; }
        FxSprite f;
        if (pool.Count > 0) { f = pool.Pop(); f.gameObject.SetActive(true); }
        else
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(root, false);
            f = go.AddComponent<FxSprite>();
            f.sr = go.AddComponent<SpriteRenderer>();
            f.sr.sortingOrder = 400;        // 🎭 配下やタイルより手前、UIより奥
        }
        f.sr.sprite = spr; f.sr.color = tint;
        f.transform.position = new Vector3(pos.x, pos.y, pos.z - 0.6f);
        f.transform.rotation = Quaternion.identity;
        f.t = 0f; f.dur = dur; f.size = size; f.spin = spin;
        f.transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float p = dur > 0f ? Mathf.Clamp01(t / dur) : 1f;
        // 素早く開いて、ゆっくり消える
        float s = size * (p < 0.25f ? Mathf.Lerp(0.15f, 1.05f, p / 0.25f) : Mathf.Lerp(1.05f, 1.35f, (p - 0.25f) / 0.75f));
        transform.localScale = Vector3.one * s;
        if (spin) transform.Rotate(0f, 0f, 90f * Time.deltaTime);
        var c = sr.color; c.a = 1f - p * p; sr.color = c;
        if (p >= 1f) { gameObject.SetActive(false); pool.Push(this); }
    }
}
