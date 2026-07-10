using UnityEngine;

/// <summary>
/// 戦闘の手続きエフェクト（外部素材なし）：魔法弾の飛翔＋着弾、被弾フラッシュ、回復エフェクト。
/// static ファクトリで短命の自己アニメGameObjectを生成する。
/// </summary>
public class BattleVfx : MonoBehaviour
{
    private enum Kind { Projectile, Burst, Spark }
    private Kind kind;
    private Vector3 from, to, vel;
    private float t, dur, baseScale;
    private Color col;
    private SpriteRenderer sr;

    // ---- ファクトリ ----
    public static void Projectile(Vector3 from, Vector3 to, Color col)
    {
        var v = New("Vfx_Proj", PrimitiveSprites.Circle(), col, from, 0.22f, 62);
        v.kind = Kind.Projectile; v.from = from; v.to = to;
        v.dur = Mathf.Clamp(Vector3.Distance(from, to) / 9f, 0.10f, 0.5f);
    }
    public static void Burst(Vector3 pos, Color col, float size = 0.55f)
    {
        var v = New("Vfx_Burst", PrimitiveSprites.Circle(), col, pos, 0.15f, 63);
        v.kind = Kind.Burst; v.dur = 0.28f; v.baseScale = size;
    }
    public static void Spark(Vector3 pos, Color col)
    {
        var v = New("Vfx_Spark", PrimitiveSprites.Square(), col, pos + new Vector3(0, 0.05f, 0), 0.10f, 64);
        v.kind = Kind.Spark; v.dur = 0.6f; v.vel = new Vector3(0, 0.5f, 0);
    }
    /// <summary>回復：緑の輪＋上昇スパーク（回復される側に出す）。</summary>
    public static void Heal(Vector3 pos)
    {
        Burst(pos, new Color(0.42f, 0.85f, 0.45f, 1f), 0.62f);
        Spark(pos, new Color(0.55f, 0.95f, 0.55f, 1f));
    }

    private static BattleVfx New(string name, Sprite spr, Color col, Vector3 pos, float scale, int order)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(pos.x, pos.y, pos.z - 0.5f);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = spr; sr.color = col; sr.sortingOrder = order;
        var v = go.AddComponent<BattleVfx>(); v.sr = sr; v.col = col; v.baseScale = scale;
        return v;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float p = dur > 0 ? Mathf.Clamp01(t / dur) : 1f;
        switch (kind)
        {
            case Kind.Projectile:
                transform.position = Vector3.Lerp(from, to, p) + new Vector3(0, 0, -0.5f);
                float pulse = 0.22f * (1f + 0.25f * Mathf.Sin(t * 40f));
                transform.localScale = Vector3.one * pulse;
                if (p >= 1f) { Burst(to, col, 0.5f); Destroy(gameObject); }
                break;
            case Kind.Burst:
                transform.localScale = Vector3.one * Mathf.Lerp(baseScale * 0.3f, baseScale, p);
                SetA(1f - p);
                if (p >= 1f) Destroy(gameObject);
                break;
            case Kind.Spark:
                transform.position += vel * Time.deltaTime;
                transform.localScale = Vector3.one * 0.10f * (1f - p * 0.5f);
                SetA(1f - p);
                if (p >= 1f) Destroy(gameObject);
                break;
        }
    }

    private void SetA(float a) { var c = sr.color; c.a = Mathf.Clamp01(a); sr.color = c; }
}
