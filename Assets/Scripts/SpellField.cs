using UnityEngine;

/// <summary>
/// 🔥 <b>その場に残る呪法</b>（灼野・泥沼）── 撃ったあとも地面に残り、踏んだ者に効き続ける。
///
/// ⚠⚠ <b>なぜ `DungeonFeatureManager` に置かないか。</b>
///   あちらは<b>準備フェーズにプレイヤーが置く構造物</b>（撤去で払い戻しがあり、セーブに載り、
///   配置枠を食う）。戦闘中に湧いて数秒で消えるものを混ぜると、
///   「置いた覚えのない罠が枠を食っている」「セーブに残り続ける」という形で必ず表に出る。
///   <b>寿命が波より短いものは、波と一緒に消える別の器に置く。</b>
///
/// ⚠ 判定は<b>一定間隔</b>で行う（毎フレームだと1秒で60回入る）。`Time.deltaTime` は
///   戦闘の速さに追従してほしいので unscaled にしない（→ [[ui-conventions]]）。
///
/// 関連: [[MagicCatalog]]（SpellForm.Scorch / Mire）[[TrapCatalog]]（状態異常の番号）/ ZombieAI(発生源)。
/// </summary>
public class SpellField : MonoBehaviour, ISimTick
{
    private float radius;
    private float lifeLeft;
    private float tickEvery = 0.5f;
    private float tickTimer;
    private float damagePerTick;
    private int status = -1;
    private Color color = Color.white;
    private SpriteRenderer sr;

    /// <summary>残る秒数。⚠ 波をまたいで残さない（`ClearAll` を波の終わりに呼ぶ）。</summary>
    public const float DefaultLife = 6f;

    /// <summary>灼野・泥沼を1つ置く。ワールド座標の中心と半径で決まる。</summary>
    public static void Spawn(Vector3 center, float radius, float damagePerTick, int status, Color color, float life)
    {
        var go = new GameObject("SpellField");
        go.transform.position = new Vector3(center.x, center.y, 0.2f);
        var f = go.AddComponent<SpellField>();
        f.radius = Mathf.Max(0.5f, radius);
        f.damagePerTick = Mathf.Max(0f, damagePerTick);
        f.status = status;
        f.color = color;
        f.lifeLeft = life > 0f ? life : DefaultLife;
        f.BuildVisual();
    }

    /// <summary>🧹 盤の掃除。⚠ 波の終わり・階層の切り替え・タイトルへ戻るで必ず呼ぶ。</summary>
    public static void ClearAll()
    {
        var all = Object.FindObjectsByType<SpellField>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++) if (all[i] != null) Object.Destroy(all[i].gameObject);
    }

    private void BuildVisual()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = PrimitiveSprites.Circle();
        sr.color = new Color(color.r, color.g, color.b, 0.32f);
        sr.sortingOrder = -5;   // ⚠ 駒より後ろ。手前に出すと配下と冒険者が読めなくなる
        // 半径は「中心からの距離」なので、直径ぶん引き伸ばす
        float d = radius * 2f;
        transform.localScale = new Vector3(d, d, 1f);
    }

    // ⏱️ **固定の刻みで進める**（`FixedUpdate`・1/60秒 → [[SimClock]]）。倍速は「刻みを長くする」のではなく「刻む回数を増やす」。
    //   ⚠ 旧来の `Update` は1フレームの長さ×速さで進み、倍速ほど攻撃・移動・判定が粗くなって**結果が速さで変わっていた**。
    private void Awake() { SimRunner.Register(this); }   // ⏱️ → [[SimRunner]]
    private void OnDestroy() { SimRunner.Unregister(this); }

    public void SimTick()
    {
        lifeLeft -= Time.deltaTime;
        if (lifeLeft <= 0f) { Destroy(gameObject); return; }

        // 消えぎわは薄くなる（いつ切れるかが盤から読める）
        if (sr != null)
        {
            float a = Mathf.Clamp01(lifeLeft / 1.5f) * 0.32f;
            sr.color = new Color(color.r, color.g, color.b, Mathf.Min(0.32f, a + 0.08f));
        }

        tickTimer += Time.deltaTime;
        if (tickTimer < tickEvery) return;
        tickTimer = FrameTimer.Carry(tickTimer, tickEvery);   // ⏱️ 端数を捨てない

        var advs = AdventurerAI.ActiveArray();
        for (int i = 0; i < advs.Length; i++)
        {
            var a = advs[i];
            if (a == null) continue;
            if (Vector3.Distance(a.transform.position, transform.position) > radius) continue;
            if (damagePerTick > 0f) a.TakeDamage(damagePerTick);
            if (status >= 0) a.ApplyTrapStatus(status);
        }
    }
}
