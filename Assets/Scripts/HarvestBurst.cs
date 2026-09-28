using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🌾 **地上の収穫を見せる**（④）。ターンの締めで入ってくる産出を、
/// 産んだ領域から**物として飛ばして**地上HUDのチップに吸い込ませる。J-1 を地上に持ってきたもの。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：地上はDPの過半を出しているのに、**払い出しが1フレームも見えなかった**。
///   『ターンを終える』を押すと画面が迷宮に切り替わり、次に資源を見たときには
///   もう数字が変わっている ―― 行為と報いが完全に切れていた。
///   通しプレイで「地上は何をしても手応えが無い」と感じたのはこれ（→ [[playthrough-wall-t11]]）。
/// </para>
///
/// <para>
/// ⚠ **報酬には触らない。** DP/素材/RP/名声はもう各系統が加算している。ここは見せるだけ。
///   ここで足したら地上の産出が二重になる。
/// ⚠ 出所は**領域ごとの重み**で散らす。施設や遺産のぶんも同じ重みに乗せる
///   （施設は自領の上にしか建たないので、自領から飛ぶのは嘘ではない）。
/// ⚠ 絵はHUDのチップと**同じもの**を使う（`UIIcons`）。別の絵にすると繋ぐ狙いが消える → [[LootBurst]]。
/// ⚠ 地上カメラは専用レイヤーだけを描くので、**Surface レイヤーに置かないと映らない**。
/// </para>
///
/// 関連: [[LootBurst]]（迷宮側の同じ役目） [[SurfaceMap]]（産出の元） [[GameUIManager.Surface]]（行き先）。
/// </summary>
public static class HarvestBurst
{
    // ── ノブ（見た目だけ）──
    private const int DpPerCoin = 220;    // ⚠ 地上は1ターンで千単位が入るので、迷宮(45)より粗く刻む
    private const int MatPerIcon = 4;
    private const int RpPerIcon = 2;
    private const int FamePerIcon = 6;
    private const int MaxPerKind = 14;    // 種類ごとの上限
    private const int MaxLive = 120;

    private const float BurstTime = 0.20f;
    private const float FlyTime = 0.62f;
    /// <summary>盤の上での大きさ（ワールド単位）。⚠ ヘクスの外接円は 0.5 なので、これ以上大きいと盤が隠れる。</summary>
    private const float SizeBurst = 0.22f, SizeArrive = 0.10f;
    private const float SpreadTime = 0.45f;   // 出発を散らす時間（一斉に飛ぶと束に見えて数が伝わらない）
    /// <summary>演出が終わるまでの目安。画面を切り替えずに待つ長さ。</summary>
    public const float ShowTime = BurstTime + FlyTime + SpreadTime + 0.25f;

    // ── 溜め（ターンの締めのあいだに集める）──
    private static int dpSum, matSum, rpSum, fameSum;
    private static readonly List<int> srcRegion = new List<int>();
    private static readonly List<float> srcWeight = new List<float>();
    private static float weightSum;

    public static int PendingDp { get { return dpSum; } }
    public static int PendingMat { get { return matSum; } }
    public static int PendingRp { get { return rpSum; } }
    public static int PendingFame { get { return fameSum; } }
    public static bool HasAny { get { return dpSum + matSum + rpSum + fameSum > 0; } }

    public static void Clear()
    {
        dpSum = matSum = rpSum = fameSum = 0;
        srcRegion.Clear(); srcWeight.Clear(); weightSum = 0f;
    }

    /// <summary>🚩 産んだ場所を1つ覚える（重みはその領域のDP産出でよい）。</summary>
    public static void NoteSource(int regionId, float weight)
    {
        if (regionId < 0 || weight <= 0f) return;
        for (int i = 0; i < srcRegion.Count; i++)
            if (srcRegion[i] == regionId) { srcWeight[i] += weight; weightSum += weight; return; }
        srcRegion.Add(regionId); srcWeight.Add(weight); weightSum += weight;
    }

    /// <summary>💰 入ってきた量を足す。⚠ **加算はしない**。すでに入った物を数えているだけ。</summary>
    public static void Add(int dp, int mat, int rp, int fame)
    {
        if (dp > 0) dpSum += dp;
        if (mat > 0) matSum += mat;
        if (rp > 0) rpSum += rp;
        if (fame > 0) fameSum += fame;
    }

    // ============ 演出 ============
    private class Item
    {
        public SpriteRenderer sr;
        public Vector3 from, burstTo;
        public float delay, t;
        public float burstScale, arriveScale;
        public int kind;     // 0=DP 1=素材 2=研究点 3=名声
    }

    private class Runner : MonoBehaviour { private void LateUpdate() { Tick(Time.unscaledDeltaTime); } }

    private static Transform root;
    private static readonly List<Item> live = new List<Item>();
    private static readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
    private static float lastChink;
    private static int layer;
    private static Material unlit;

    /// <summary>
    /// ⚠⚠ **SpriteRenderer の既定マテリアルを使ってはいけない。**
    ///   URP の 2D レンダラでは既定が `Sprite-Lit-Default` で、光が当たらないと**真っ黒**に描かれる。
    ///   地上カメラは専用レイヤーしか映さないので迷宮側の光が届かず、
    ///   実測で**収穫が黒い塊になって盤を覆った**。盤のメッシュと同じ `Sprites/Default`（不変色）を使う。
    /// </summary>
    private static Material Unlit
    {
        get
        {
            if (unlit == null)
            {
                var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
                unlit = new Material(sh);
            }
            return unlit;
        }
    }

    /// <summary>⚠ 絵ごとに解像度が違う（手続き生成は 64px/unit、描き起こしの絵は別）。
    /// そのまま同じ倍率で出すと**片方だけ巨大になる**ので、必ず実寸から割り出す。</summary>
    private static float ScaleFor(Sprite sp, float worldSize)
    {
        if (sp == null) return worldSize;
        float h = sp.bounds.size.y;
        return h <= 0.0001f ? worldSize : worldSize / h;
    }

    private static void EnsureRoot(int surfaceLayer)
    {
        layer = surfaceLayer;
        if (root != null) { root.gameObject.layer = layer; return; }
        var go = new GameObject("HarvestBurst");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Runner>();
        go.layer = layer;
        root = go.transform;
    }

    /// <summary>
    /// 🌾 溜めた収穫を盤の上に撒く。⚠ 呼んだあと `Clear` される（二度撒かない）。
    /// </summary>
    public static void Play(int surfaceLayer)
    {
        if (!HasAny) { Clear(); return; }
        EnsureRoot(surfaceLayer);
        Emit(0, dpSum, DpPerCoin);
        Emit(1, matSum, MatPerIcon);
        Emit(2, rpSum, RpPerIcon);
        Emit(3, fameSum, FamePerIcon);
        Clear();
    }

    private static void Emit(int kind, int amount, int per)
    {
        if (amount <= 0) return;
        int n = Mathf.Clamp(amount / Mathf.Max(1, per), 1, MaxPerKind);
        for (int i = 0; i < n; i++)
        {
            if (live.Count >= MaxLive) return;
            One(kind, PickSource(), Random.Range(0f, SpreadTime));
        }
    }

    /// <summary>🎲 産出の重みで出所を選ぶ。⚠ 出所がひとつも無いときは中央（＝迷宮のある場所）から。</summary>
    private static Vector3 PickSource()
    {
        int id = -1;
        if (weightSum > 0f)
        {
            float r = Random.Range(0f, weightSum), acc = 0f;
            for (int i = 0; i < srcRegion.Count; i++)
            {
                acc += srcWeight[i];
                if (r <= acc) { id = srcRegion[i]; break; }
            }
        }
        if (id < 0) id = SurfaceMap.IndexOfCenter();
        if (id < 0 || id >= SurfaceMap.Count) return Vector3.zero;
        var reg = SurfaceMap.Get(id);
        var p = SurfaceView.PosOf(reg.col, reg.row);
        return new Vector3(p.x, p.y, -2f);
    }

    private static void One(int kind, Vector3 world, float delay)
    {
        SpriteRenderer sr;
        if (pool.Count > 0) { sr = pool.Pop(); sr.gameObject.SetActive(true); }
        else
        {
            var go = new GameObject("Harvest");
            go.transform.SetParent(root, false);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 300;      // 🎭 盤の文字(200)より手前
        }
        sr.gameObject.layer = layer;    // ⚠ プールから借りた物もレイヤーを付け直す
        sr.sharedMaterial = Unlit;
        string icon = kind == 0 ? "dp" : kind == 1 ? "material" : kind == 2 ? "research" : "fame";
        sr.sprite = UIIcons.Get(icon);
        sr.color = KindColor(kind);
        float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(0.18f, 0.42f);
        live.Add(new Item
        {
            sr = sr,
            kind = kind,
            from = world,
            burstTo = world + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.7f + 0.16f, 0f),
            delay = delay,
            t = 0f,
            burstScale = ScaleFor(sr.sprite, SizeBurst),
            arriveScale = ScaleFor(sr.sprite, SizeArrive),
        });
        sr.transform.position = world;
        sr.transform.localScale = Vector3.zero;   // 遅れて出る物は最初は見えない
    }

    private static Color KindColor(int kind)
    {
        return kind == 0 ? UITheme.DP : kind == 1 ? UITheme.Material
             : kind == 2 ? UITheme.Research : UITheme.Fame;
    }

    private static void Tick(float dt)
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var it = live[i];
            if (it.sr == null) { live.RemoveAt(i); continue; }
            if (it.delay > 0f) { it.delay -= dt; continue; }
            it.t += dt;

            if (it.t < BurstTime)
            {
                float k = it.t / BurstTime;
                it.sr.transform.position = Vector3.Lerp(it.from, it.burstTo, 1f - (1f - k) * (1f - k));
                it.sr.transform.localScale = Vector3.one * Mathf.Lerp(0f, it.burstScale, k);
                continue;
            }

            // ⚠ 行き先は**毎フレーム引き直す**（盤をパンしてもズレないように）
            Vector3 target = GameUIManager.SurfaceChipWorldTarget(it.kind, it.sr.transform.position);
            float p = Mathf.Clamp01((it.t - BurstTime) / FlyTime);
            float ease = p * p * p;
            it.sr.transform.position = Vector3.Lerp(it.burstTo, target, ease);
            it.sr.transform.localScale = Vector3.one * Mathf.Lerp(it.burstScale, it.arriveScale, p);
            var c = it.sr.color; c.a = p > 0.8f ? Mathf.InverseLerp(1f, 0.8f, p) : 1f; it.sr.color = c;

            if (p >= 1f)
            {
                if (Time.unscaledTime - lastChink > 0.05f)
                {
                    lastChink = Time.unscaledTime;
                    SoundSystem.Play(SoundSystem.Sfx.Gain, 0.28f, Random.Range(1.25f, 1.75f));
                }
                it.sr.gameObject.SetActive(false);
                pool.Push(it.sr);
                live.RemoveAt(i);
            }
        }
    }
}
