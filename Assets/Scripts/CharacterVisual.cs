using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// キャラの見た目を手続きパーツで組み、コードでアニメする（プロト＝戦士）。
/// - 歩行/待機は自動（移動を検知）。攻撃/被弾は一発再生。死亡は切り離して自壊。
/// AdventurerAI等が生成し、PlayAttack/PlayHurt/Die/SetHP を呼ぶ。
/// </summary>
public class CharacterVisual : MonoBehaviour
{
    private enum OneShot { None, Attack, Hurt, Heal }

    private Transform flip, bob, torso, hipL, hipR, swordPivot;
    private SpriteRenderer shadowSR, slashSR, hpFill;
    private readonly List<SpriteRenderer> srs = new List<SpriteRenderer>();
    private readonly List<Color> baseCols = new List<Color>();
    private readonly List<bool> tintable = new List<bool>();

    private OneShot oneShot = OneShot.None;
    private float oneShotT, time, deadT;
    private bool dead;
    private float facing = 1f, faceRefX, facingHold;
    private Vector3 prevPos;

    public float Facing => facing;
    /// <summary>攻撃/詠唱の発射元（手・武器）のワールド座標。</summary>
    public Vector3 MuzzlePos() => transform.position + new Vector3(0.16f * facing, 0.14f, 0f);
    /// <summary>対象の方向を向く（左右反転）。攻撃中は移動で向きが上書きされないよう少し保持。</summary>
    public void FaceTowards(float worldX)
    {
        float d = worldX - transform.position.x;
        if (Mathf.Abs(d) > 0.001f) facing = d < 0f ? -1f : 1f;
        facingHold = 0.55f;
    }
    private float hpRatio = 1f;
    private const float HP_W = 0.34f;

    private static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

    private void Awake()
    {
        prevPos = transform.position; faceRefX = transform.position.x;
        BuildWarrior();
        SetHP(1f);
    }

    // ======== リグ構築（戦士）========
    private Transform Node(Transform p, string name, Vector3 pos)
    {
        var go = new GameObject(name); go.transform.SetParent(p, false); go.transform.localPosition = pos;
        return go.transform;
    }
    private SpriteRenderer P(Transform p, string name, Sprite spr, Color col, Vector3 pos, Vector2 scale, int order, bool canTint)
    {
        var go = new GameObject(name); go.transform.SetParent(p, false);
        go.transform.localPosition = pos; go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = spr; sr.color = col; sr.sortingOrder = order;
        srs.Add(sr); baseCols.Add(col); tintable.Add(canTint);
        return sr;
    }

    private void BuildWarrior()
    {
        Color tunic = C("#c0453f"), tunicDk = C("#a8352f"), gold = C("#e3a94a");
        Color helmet = C("#8a8f9a"), helmetDk = C("#6d7581"), blade = C("#cdd2dd");
        Color skin = C("#e6b98f"), dark = C("#2f2a38"), dark2 = C("#3a3444"), shield = C("#5f6672");
        var SQ = PrimitiveSprites.Square(); var CI = PrimitiveSprites.Circle(); var RR = PrimitiveSprites.RoundRect();

        // 影・HPバー（反転しない・揺れない）
        shadowSR = P(transform, "Shadow", CI, new Color(0, 0, 0, 0.35f), new Vector3(0, -0.46f, 0.02f), new Vector2(0.52f, 0.17f), 40, false);
        P(transform, "HPbg", SQ, C("#2a2233"), new Vector3(0, 0.46f, 0f), new Vector2(HP_W + 0.02f, 0.06f), 48, false);
        hpFill = P(transform, "HPfill", SQ, C("#5cc47c"), new Vector3(0, 0.46f, -0.01f), new Vector2(HP_W, 0.045f), 49, false);

        flip = Node(transform, "Flip", Vector3.zero);
        bob = Node(flip, "Bob", Vector3.zero);

        // 脚（股関節ピボットで振る）
        hipL = Node(bob, "HipL", new Vector3(-0.06f, -0.24f, 0));
        P(hipL, "LegL", RR, dark, new Vector3(0, -0.10f, 0), new Vector2(0.09f, 0.20f), 41, true);
        hipR = Node(bob, "HipR", new Vector3(0.06f, -0.24f, 0));
        P(hipR, "LegR", RR, dark2, new Vector3(0, -0.10f, 0), new Vector2(0.09f, 0.20f), 41, true);

        // 胴（腰ピボットで前傾）— 子は腰(char y=-0.24)基準の相対位置
        torso = Node(bob, "Torso", new Vector3(0, -0.24f, 0));
        P(torso, "Shield", CI, shield, new Vector3(-0.17f, 0.16f, 0.01f), new Vector2(0.23f, 0.23f), 42, true);
        P(torso, "ShieldBoss", CI, gold, new Vector3(-0.17f, 0.16f, 0f), new Vector2(0.07f, 0.07f), 43, false);
        P(torso, "Body", RR, tunic, new Vector3(0, 0.19f, 0), new Vector2(0.28f, 0.34f), 44, true);
        P(torso, "BodyShade", RR, tunicDk, new Vector3(0.07f, 0.19f, -0.005f), new Vector2(0.10f, 0.34f), 45, true);
        P(torso, "Belt", SQ, gold, new Vector3(0, 0.10f, -0.01f), new Vector2(0.28f, 0.05f), 45, false);
        P(torso, "Head", CI, skin, new Vector3(0, 0.44f, 0), new Vector2(0.22f, 0.22f), 46, true);
        P(torso, "Nose", SQ, helmetDk, new Vector3(0, 0.44f, -0.01f), new Vector2(0.05f, 0.14f), 47, true);
        P(torso, "Helmet", CI, helmet, new Vector3(0, 0.51f, -0.005f), new Vector2(0.25f, 0.16f), 47, true);
        P(torso, "Crest", SQ, tunic, new Vector3(0, 0.60f, -0.01f), new Vector2(0.05f, 0.09f), 47, true);

        // 剣（手ピボットで振る）
        swordPivot = Node(torso, "SwordPivot", new Vector3(0.17f, 0.18f, -0.02f));
        P(swordPivot, "Hilt", SQ, gold, new Vector3(0, 0.02f, 0), new Vector2(0.05f, 0.10f), 50, false);
        P(swordPivot, "Guard", SQ, gold, new Vector3(0, 0.06f, 0), new Vector2(0.13f, 0.035f), 50, false);
        P(swordPivot, "Blade", RR, blade, new Vector3(0, 0.28f, 0), new Vector2(0.05f, 0.42f), 49, true);

        // 斬撃の軌跡（初期は透明）
        slashSR = P(bob, "Slash", RR, new Color(1, 1, 1, 0f), new Vector3(0.30f, 0.05f, -0.03f), new Vector2(0.06f, 0.44f), 51, false);
        slashSR.transform.localRotation = Quaternion.Euler(0, 0, -35f);
    }

    // ======== 外部API ========
    public void PlayAttack() { if (!dead) { oneShot = OneShot.Attack; oneShotT = 0f; } }
    public void PlayHurt() { if (!dead) { oneShot = OneShot.Hurt; oneShotT = 0f; } }
    public void PlayHeal() { if (!dead) { oneShot = OneShot.Heal; oneShotT = 0f; } }
    public void SetHP(float r)
    {
        hpRatio = Mathf.Clamp01(r);
        if (hpFill == null) return;
        hpFill.transform.localScale = new Vector3(HP_W * hpRatio, 0.045f, 1f);
        hpFill.transform.localPosition = new Vector3(-HP_W * 0.5f + HP_W * hpRatio * 0.5f, 0.46f, -0.01f);
        hpFill.color = hpRatio > 0.5f ? C("#5cc47c") : (hpRatio > 0f ? C("#e3a94a") : C("#df5a5a"));
    }
    /// <summary>死亡アニメ開始。親から切り離し、AIのGameObjectが消えても倒れ演出を完遂して自壊する。</summary>
    public void Die()
    {
        if (dead) return;
        dead = true; deadT = 0f;
        transform.SetParent(null, true);
    }

    // ======== アニメ ========
    private static float Ease(float p) => 1f - Mathf.Pow(1f - p, 3f);

    private void Update()
    {
        float dt = Time.deltaTime; time += dt;
        if (dead) { DeathUpdate(dt); return; }

        Vector3 wp = transform.position;
        float sp = (wp - prevPos).magnitude / Mathf.Max(dt, 1e-4f); prevPos = wp;
        bool moving = sp > 0.4f;
        // 向き：攻撃直後は保持、それ以外は進行方向（水平）で反転
        if (facingHold > 0f) facingHold -= dt;
        else { float hdx = wp.x - faceRefX; if (Mathf.Abs(hdx) > 0.02f) { facing = hdx < 0f ? -1f : 1f; faceRefX = wp.x; } }
        flip.localScale = new Vector3(facing, 1f, 1f);

        float bobY = 0, lean = 0, swordAng = 0, legA = 0, recoilX = 0; bool slashOn = false;
        if (moving) { float w = time * 7f; bobY = -0.02f * Mathf.Abs(Mathf.Sin(w)); legA = 16f * Mathf.Sin(w); lean = 3f; swordAng = 8f * Mathf.Sin(w); }
        else { float w = time * 2.2f; bobY = 0.012f * Mathf.Sin(w); swordAng = 3f * Mathf.Sin(w); }

        float hurtI = 0;
        if (oneShot == OneShot.Attack)
        {
            oneShotT += dt; float p = oneShotT / 0.4f;
            if (p >= 1f) oneShot = OneShot.None;
            else { float e = Ease(p); swordAng = -100f + e * 165f; lean = 7f * Mathf.Sin(p * Mathf.PI); bobY -= 0.015f * Mathf.Sin(p * Mathf.PI); slashOn = p > 0.32f && p < 0.68f; }
        }
        else if (oneShot == OneShot.Hurt)
        {
            oneShotT += dt; float p = oneShotT / 0.35f;
            if (p >= 1f) oneShot = OneShot.None;
            else { hurtI = 1f - p; lean = -6f * hurtI; recoilX = -0.03f * facing * hurtI; }
        }
        else if (oneShot == OneShot.Heal)
        {
            oneShotT += dt; float p = oneShotT / 0.6f;
            if (p >= 1f) oneShot = OneShot.None;
            else { float s = Mathf.Sin(p * Mathf.PI); swordAng = -70f - 30f * s; bobY += 0.03f * s; lean = -3f * s; } // 杖/武器を掲げる
        }

        bob.localPosition = new Vector3(recoilX, bobY, 0f);
        torso.localRotation = Quaternion.Euler(0, 0, lean);
        swordPivot.localRotation = Quaternion.Euler(0, 0, swordAng);
        hipL.localRotation = Quaternion.Euler(0, 0, legA);
        hipR.localRotation = Quaternion.Euler(0, 0, -legA);
        if (slashSR != null) { var c = slashSR.color; c.a = slashOn ? 0.85f : 0f; slashSR.color = c; }
        if (shadowSR != null) { float s = 1f + bobY * 0.5f; shadowSR.transform.localScale = new Vector3(0.52f * s, 0.17f * s, 1f); }
        ApplyTint(hurtI);
    }

    private void DeathUpdate(float dt)
    {
        deadT += dt; float p = Mathf.Clamp01(deadT / 0.7f); float e = Ease(p);
        flip.localRotation = Quaternion.Euler(0, 0, -80f * e * facing);
        flip.localPosition = new Vector3(0, -0.12f * e, 0);
        float a = 1f - e * 0.65f;
        for (int i = 0; i < srs.Count; i++) { if (srs[i] == shadowSR) continue; var c = baseCols[i]; c.a *= a; srs[i].color = c; }
        if (shadowSR != null) { var c = shadowSR.color; c.a = 0.35f * (1f - e); shadowSR.color = c; }
        if (hpFill != null) SetHP(0f);
        if (p >= 1f) Destroy(gameObject);
    }

    private void ApplyTint(float i)
    {
        for (int k = 0; k < srs.Count; k++)
        {
            if (!tintable[k]) continue;
            var b = baseCols[k];
            srs[k].color = i > 0.01f ? Color.Lerp(b, new Color(1, 1, 1, b.a), i * 0.85f) : b;
        }
    }
}
