using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 💰 **戦利品が飛んでHUDに吸い込まれる**（J-1）。倒した場所から硬貨と塊が弾け、
/// 上のチップへ吸い寄せられて消える。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：D-4 で「撃破の場に数字を出す」と「チップが膨らむ」は入れたが、
///   その2つが**繋がっていなかった**。盤で起きたことと、上の数字が増えたことが
///   別々の出来事に見える。あいだを**物が飛ぶ**ことで、1つの出来事になる。
/// </para>
///
/// <para>
/// ⚠⚠ **絵はHUDのチップと同じものを使う**（`UIIcons` の硬貨と塊）。
///   ここで別のきれいな絵を描くと、**飛んでいく物と着く場所が別物に見えて**、
///   繋ぐという狙いそのものが消える。目が追えることが全て。
/// </para>
///
/// <para>
/// ⚠ **報酬には触らない。** DPと素材はもう `AdventurerAI` が渡している。ここは見せるだけ。
/// ⚠ 時間は `Time.deltaTime`（戦闘の一部なので倍速に乗る → [[ui-conventions]]）。
/// ⚠ 28体の波で1体6個も出すと170個になるので、**1回の上限**と**同時の上限**を両方置く。
/// </para>
///
/// 関連: [[KillFeedback]]（呼ぶ側） [[UIIcons]]（絵） [[GameUIManager]]（行き先）。
/// </summary>
public static class LootBurst
{
    // ── ノブ（見た目だけ）──
    /// <summary>硬貨1つぶんのDP。⚠ 少額でも1つは出す（何も出ないと「入らなかった」に見える）。</summary>
    private const int DpPerCoin = 45;
    private const int MaxCoins = 5;
    private const int MaxIngots = 4;
    /// <summary>同時に飛んでいられる数。⚠ 乱戦で画面が埋まらないようにする栓。</summary>
    private const int MaxLive = 90;

    private const float BurstTime = 0.22f;   // 弾ける時間
    private const float FlyTime = 0.55f;    // 吸い込まれる時間

    private class Item
    {
        public SpriteRenderer sr;
        public Vector3 from, burstTo;
        public float t;
        public bool material;
    }

    private class Runner : MonoBehaviour { private void LateUpdate() { Tick(Time.deltaTime); } }

    private static Transform root;
    private static readonly List<Item> live = new List<Item>();
    private static readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
    private static float lastChink;

    private static void EnsureRoot()
    {
        if (root != null) return;
        var go = new GameObject("LootBurst");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Runner>();
        root = go.transform;
    }

    /// <summary>💰 倒した場所から実りを弾けさせる。⚠ `KillFeedback` から1回だけ呼ぶ。</summary>
    public static void Spawn(Vector3 world, int dp, int mats)
    {
        int coins = dp > 0 ? Mathf.Clamp(dp / DpPerCoin, 1, MaxCoins) : 0;
        int ingots = mats > 0 ? Mathf.Clamp(mats, 1, MaxIngots) : 0;
        if (coins + ingots <= 0) return;
        EnsureRoot();
        for (int i = 0; i < coins; i++) One(world, false);
        for (int i = 0; i < ingots; i++) One(world, true);
    }

    private static void One(Vector3 world, bool material)
    {
        if (live.Count >= MaxLive) return;   // ⚠ 栓。ここを外すと乱戦で画面が埋まる
        SpriteRenderer sr;
        if (pool.Count > 0) { sr = pool.Pop(); sr.gameObject.SetActive(true); }
        else
        {
            var go = new GameObject("Loot");
            go.transform.SetParent(root, false);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 450;     // 🎭 爆発(400)より手前、ダメージ数字(500)より奥
        }
        sr.sprite = UIIcons.Get(material ? "material" : "dp");
        sr.color = material ? UITheme.Material : UITheme.DP;
        var a = Random.Range(0f, Mathf.PI * 2f);
        float r = Random.Range(0.45f, 1.05f);
        live.Add(new Item
        {
            sr = sr,
            material = material,
            from = world,
            burstTo = world + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.7f + 0.25f, 0f),
            t = 0f,
        });
        sr.transform.position = world;
        sr.transform.localScale = Vector3.one * 0.22f;
    }

    private static void Tick(float dt)
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var it = live[i];
            if (it.sr == null) { live.RemoveAt(i); continue; }
            it.t += dt;

            if (it.t < BurstTime)
            {
                // 弾ける（外へ、少し上に）
                float k = it.t / BurstTime;
                it.sr.transform.position = Vector3.Lerp(it.from, it.burstTo, 1f - (1f - k) * (1f - k));
                it.sr.transform.localScale = Vector3.one * Mathf.Lerp(0.10f, 0.26f, k);
                continue;
            }

            // 吸い込まれる。⚠ 行き先は**毎フレーム引き直す**（カメラが動くとズレるため）
            Vector3 target = GameUIManager.ChipWorldTarget(it.material, it.sr.transform.position);
            float p = Mathf.Clamp01((it.t - BurstTime) / FlyTime);
            // 後半ほど速い（吸い込まれている感じ）
            float ease = p * p * p;
            it.sr.transform.position = Vector3.Lerp(it.burstTo, target, ease);
            it.sr.transform.localScale = Vector3.one * Mathf.Lerp(0.26f, 0.11f, p);
            var c = it.sr.color; c.a = p > 0.75f ? Mathf.InverseLerp(1f, 0.75f, p) : 1f; it.sr.color = c;

            if (p >= 1f)
            {
                // 🔊 ジャラジャラ。⚠ 1つずつ全部鳴らすと割れるので**最短間隔**を置き、
                //    連なるほど音を上げる（束で入っている感じが出る）。
                if (Time.unscaledTime - lastChink > 0.045f)
                {
                    lastChink = Time.unscaledTime;
                    SoundSystem.Play(SoundSystem.Sfx.Gain, 0.30f, Random.Range(1.35f, 1.85f));
                }
                it.sr.gameObject.SetActive(false);
                pool.Push(it.sr);
                live.RemoveAt(i);
            }
        }
    }
}
