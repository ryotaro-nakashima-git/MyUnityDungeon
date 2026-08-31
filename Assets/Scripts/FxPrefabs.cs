using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ✨ **買ってきたパーティクルを鳴らす層**（Eric VFX Studio）。
///
/// <para>
/// ⚠⚠ **音と同じ作りにしてある。** `Resources/Fx/&lt;id&gt;` にプレハブがあれば出し、
///   無ければ**何もしない**（既存の手続き演出 `FxSprite` / `BattleVfx` がそのまま残る）。
///   ＝ 素材が1つ増えるごとに、その演出だけが本物に差し替わる。
/// </para>
///
/// <para>
/// ⚠ **2Dの盤に3Dのパーティクルを出す**ので、置いただけでは見えない。ここで必ず直す：
///   ① `sortingOrder` を配下より手前・UIより奥に（`FxSprite` と同じ 400 帯）
///   ② z を少し手前に出す
///   ③ 使い終わったら消す（`ParticleSystem` は放っておくと盤に残り続ける）
/// ⚠ プールしない。1波で数十回しか出ないうえ、`ParticleSystem` の使い回しは
///   `Clear()` の抜けで前の粒が残る事故が起きやすい（作って捨てる方が読みやすい）。
/// </para>
///
/// <para>
/// ⚠ 時間は `Time.deltaTime` 系（戦闘の一部なので倍速に乗る）。`ParticleSystem` の
///   `useUnscaledTime` は既定 false なのでそのままでよい。
/// </para>
/// 関連: [[KillFeedback]]（呼ぶ側） [[FxSprite]] [[BattleVfx]] [[SoundSystem]]（同じ「あれば使う」型）。
/// </summary>
public static class FxPrefabs
{
    public const string Dir = "Fx/";

    // ── 目録（末尾に足す）──
    public const string Hit = "fx_hit";              // 命中
    public const string Explosion = "fx_explosion";  // 撃破
    public const string Burst = "fx_burst";          // 大物の撃破・過負荷
    public const string Circle = "fx_circle";        // 召喚・湧き・巨大施設
    public const string Slash = "fx_slash";          // 号令

    /// <summary>⚠ 見つからなかった id も覚える（毎回 `Resources.Load` を叩かない）。</summary>
    private static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
    private static Transform root;

    public static bool Has(string id) { return Load(id) != null; }

    private static GameObject Load(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        GameObject p;
        if (cache.TryGetValue(id, out p)) return p;
        p = Resources.Load<GameObject>(Dir + id);
        cache[id] = p;
        return p;
    }

    /// <summary>🔄 差し替えたあとに探し直す（エディタで作り直したとき用）。</summary>
    public static void Reload() { cache.Clear(); }

    /// <summary>
    /// ✨ その場に出す。⚠ 無ければ **false**（呼んだ側が手続き演出に落とせるように）。
    /// </summary>
    /// <param name="scale">1.0 が素の大きさ。盤の1マス＝ワールド1なので、たいてい 0.5〜1.5。</param>
    public static bool Play(string id, Vector3 pos, float scale = 1f, float tintAlpha = 1f)
    {
        var prefab = Load(id);
        if (prefab == null) return false;
        if (root == null)
        {
            var g = GameObject.Find("FxPrefabRoot");
            if (g == null) { g = new GameObject("FxPrefabRoot"); Object.DontDestroyOnLoad(g); }
            root = g.transform;
        }

        var go = Object.Instantiate(prefab, new Vector3(pos.x, pos.y, pos.z - 0.8f), Quaternion.identity, root);
        go.transform.localScale = Vector3.one * Mathf.Max(0.05f, scale);

        // 🎭 2Dの盤で見えるところに出す。⚠ これをやらないと**タイルの裏に隠れる**。
        float longest = 0.6f;
        var systems = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var r = systems[i].GetComponent<ParticleSystemRenderer>();
            if (r != null) { r.sortingLayerName = "Default"; r.sortingOrder = 420; }
            var main = systems[i].main;
            float life = main.duration + main.startLifetime.constantMax;
            if (life > longest) longest = life;
            if (tintAlpha < 0.999f)
            {
                var c = main.startColor.color; c.a *= tintAlpha;
                var sc = main.startColor; sc.color = c; main.startColor = sc;
            }
        }
        // 🧹 放っておくと盤に残り続ける。いちばん長い粒が消えるまで待って捨てる。
        Object.Destroy(go, longest + 0.4f);
        return true;
    }
}
