using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// キャラの見た目を手続きパーツで組み、コードでアニメする。
/// ジョブ別リグ（戦士/盗賊/聖職者/魔法使い）＋攻撃スタイル（斬/刺/詠唱/素手）。
/// 歩行/待機は移動自動検知、攻撃/被弾/回復は一発、死亡は切り離して自壊。向きは進行方向/対象で反転。
/// </summary>
public class CharacterVisual : MonoBehaviour
{
    public enum RigType { Warrior, Thief, Cleric, Mage }
    public enum AttackStyle { Swing, Stab, Cast, Punch }
    private enum OneShot { None, Attack, Hurt, Heal }

    private Transform flip, bob, torso, hipL, hipR, weaponPivot;
    private SpriteRenderer shadowSR, slashSR, hpFill;
    private readonly List<SpriteRenderer> srs = new List<SpriteRenderer>();
    private readonly List<Color> baseCols = new List<Color>();
    private readonly List<bool> tintable = new List<bool>();

    private RigType rig;
    private AttackStyle attackStyle = AttackStyle.Swing;
    private OneShot oneShot = OneShot.None;
    private float oneShotT, time, deadT;
    private bool dead, built;
    private float facing = 1f, faceRefX, facingHold;
    private Vector3 prevPos;
    private const float HP_W = 0.34f;

    private static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

    private void Awake() { prevPos = transform.position; faceRefX = transform.position.x; }

    // ======== 生成ヘルパ ========
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

    /// <summary>ジョブに応じてリグを構築する（AddComponent後にAdventurerAIが呼ぶ）。</summary>
    public void Init(RigType type)
    {
        if (built) return;
        rig = type;
        var sq = PrimitiveSprites.Square(); var ci = PrimitiveSprites.Circle(); var rr = PrimitiveSprites.RoundRect(); var tri = PrimitiveSprites.Triangle();
        Color gold = C("#e3a94a"), skin = C("#e6b98f"), blade = C("#cdd2dd"), dark = C("#2f2a38"), dark2 = C("#3a3444");

        Color body, bodyDk, legc;
        switch (type)
        {
            case RigType.Thief: body = C("#4f6b45"); bodyDk = C("#3c5335"); legc = C("#2c3a28"); break;
            case RigType.Cleric: body = C("#d8dbe6"); bodyDk = C("#b9bfd0"); legc = C("#8f95a8"); break;
            case RigType.Mage: body = C("#4a54a0"); bodyDk = C("#3a4382"); legc = C("#2c3466"); break;
            default: body = C("#c0453f"); bodyDk = C("#a8352f"); legc = dark; break; // Warrior
        }

        // 影・HPバー（反転・揺れなし）
        shadowSR = P(transform, "Shadow", ci, new Color(0, 0, 0, 0.35f), new Vector3(0, -0.46f, 0.02f), new Vector2(0.52f, 0.17f), 40, false);
        P(transform, "HPbg", sq, C("#2a2233"), new Vector3(0, 0.46f, 0f), new Vector2(HP_W + 0.02f, 0.06f), 48, false);
        hpFill = P(transform, "HPfill", sq, C("#5cc47c"), new Vector3(0, 0.46f, -0.01f), new Vector2(HP_W, 0.045f), 49, false);

        flip = Node(transform, "Flip", Vector3.zero);
        bob = Node(flip, "Bob", Vector3.zero);

        // 脚
        hipL = Node(bob, "HipL", new Vector3(-0.06f, -0.24f, 0));
        P(hipL, "LegL", rr, legc, new Vector3(0, -0.10f, 0), new Vector2(0.09f, 0.20f), 41, true);
        hipR = Node(bob, "HipR", new Vector3(0.06f, -0.24f, 0));
        P(hipR, "LegR", rr, Color.Lerp(legc, Color.white, 0.08f), new Vector3(0, -0.10f, 0), new Vector2(0.09f, 0.20f), 41, true);

        // 胴（腰ピボット）
        torso = Node(bob, "Torso", new Vector3(0, -0.24f, 0));
        P(torso, "Body", rr, body, new Vector3(0, 0.19f, 0), new Vector2(0.28f, 0.34f), 44, true);
        P(torso, "BodyShade", rr, bodyDk, new Vector3(0.07f, 0.19f, -0.005f), new Vector2(0.10f, 0.34f), 45, true);
        P(torso, "Head", ci, skin, new Vector3(0, 0.44f, 0), new Vector2(0.22f, 0.22f), 46, true);

        weaponPivot = Node(torso, "WeaponPivot", new Vector3(0.17f, 0.18f, -0.02f));

        switch (type)
        {
            case RigType.Warrior: BuildWarriorGear(sq, ci, rr, gold, blade, C("#8a8f9a"), C("#6d7581"), C("#5f6672"), body); break;
            case RigType.Thief: BuildThiefGear(sq, ci, rr, tri, gold, blade, C("#33463b"), C("#6d5535")); break;
            case RigType.Cleric: BuildClericGear(sq, ci, rr, gold, C("#eef1f7"), C("#57c3d0"), C("#7a5a34")); break;
            case RigType.Mage: BuildMageGear(sq, ci, rr, tri, C("#5b3f9e"), C("#452f7a"), C("#8bd0ff"), C("#6d5535")); break;
        }

        // 斬撃軌跡（刺/斬で使用）
        slashSR = P(bob, "Slash", rr, new Color(1, 1, 1, 0f), new Vector3(0.30f, 0.05f, -0.03f), new Vector2(0.06f, 0.44f), 51, false);
        slashSR.transform.localRotation = Quaternion.Euler(0, 0, -35f);

        built = true;
        SetHP(1f);
    }

    private void BuildWarriorGear(Sprite sq, Sprite ci, Sprite rr, Color gold, Color blade, Color helmet, Color helmetDk, Color shield, Color crest)
    {
        P(torso, "Shield", ci, shield, new Vector3(-0.17f, 0.16f, 0.01f), new Vector2(0.23f, 0.23f), 42, true);
        P(torso, "ShieldBoss", ci, gold, new Vector3(-0.17f, 0.16f, 0f), new Vector2(0.07f, 0.07f), 43, false);
        P(torso, "Belt", sq, gold, new Vector3(0, 0.10f, -0.01f), new Vector2(0.28f, 0.05f), 45, false);
        P(torso, "Nose", sq, helmetDk, new Vector3(0, 0.44f, -0.01f), new Vector2(0.05f, 0.14f), 47, true);
        P(torso, "Helmet", ci, helmet, new Vector3(0, 0.51f, -0.005f), new Vector2(0.25f, 0.16f), 47, true);
        P(torso, "Crest", sq, crest, new Vector3(0, 0.60f, -0.01f), new Vector2(0.05f, 0.09f), 47, true);
        // 剣
        P(weaponPivot, "Hilt", sq, gold, new Vector3(0, 0.02f, 0), new Vector2(0.05f, 0.10f), 50, false);
        P(weaponPivot, "Guard", sq, gold, new Vector3(0, 0.06f, 0), new Vector2(0.13f, 0.035f), 50, false);
        P(weaponPivot, "Blade", rr, blade, new Vector3(0, 0.28f, 0), new Vector2(0.05f, 0.42f), 49, true);
    }

    private void BuildThiefGear(Sprite sq, Sprite ci, Sprite rr, Sprite tri, Color gold, Color blade, Color hood, Color hilt)
    {
        // フード（頭の後ろ）＋とがり
        P(torso, "Hood", ci, hood, new Vector3(0, 0.47f, 0.005f), new Vector2(0.27f, 0.26f), 45, true);
        P(torso, "HoodPeak", tri, hood, new Vector3(-0.10f, 0.55f, 0.006f), new Vector2(0.14f, 0.16f), 45, true);
        P(torso, "Sash", sq, C("#2c3a28"), new Vector3(0, 0.12f, -0.01f), new Vector2(0.28f, 0.04f), 45, false);
        // 短剣（逆手気味・短い刃）
        P(weaponPivot, "Hilt", sq, hilt, new Vector3(0, 0.02f, 0), new Vector2(0.05f, 0.08f), 50, false);
        P(weaponPivot, "Guard", sq, gold, new Vector3(0, 0.05f, 0), new Vector2(0.10f, 0.03f), 50, false);
        P(weaponPivot, "Blade", rr, blade, new Vector3(0, 0.16f, 0), new Vector2(0.045f, 0.22f), 49, true);
    }

    private void BuildClericGear(Sprite sq, Sprite ci, Sprite rr, Color gold, Color cowl, Color trim, Color shaft)
    {
        // カウル（頭の後ろ）＋額当て
        P(torso, "Cowl", ci, cowl, new Vector3(0, 0.47f, 0.005f), new Vector2(0.27f, 0.25f), 45, true);
        P(torso, "Band", sq, trim, new Vector3(0, 0.36f, -0.01f), new Vector2(0.22f, 0.03f), 47, false);
        P(torso, "Sash", sq, trim, new Vector3(0, 0.12f, -0.01f), new Vector2(0.28f, 0.04f), 45, false);
        // 杖（先端に十字＋玉）
        P(weaponPivot, "Shaft", rr, shaft, new Vector3(0, 0.18f, 0), new Vector2(0.045f, 0.50f), 49, true);
        P(weaponPivot, "Orb", ci, gold, new Vector3(0, 0.43f, -0.01f), new Vector2(0.12f, 0.12f), 50, false);
        P(weaponPivot, "CrossV", sq, trim, new Vector3(0, 0.43f, -0.02f), new Vector2(0.03f, 0.10f), 51, false);
        P(weaponPivot, "CrossH", sq, trim, new Vector3(0, 0.45f, -0.02f), new Vector2(0.08f, 0.03f), 51, false);
    }

    private void BuildMageGear(Sprite sq, Sprite ci, Sprite rr, Sprite tri, Color hat, Color hatDk, Color orb, Color shaft)
    {
        // とんがり帽子＋つば
        P(torso, "Brim", rr, hatDk, new Vector3(0, 0.50f, -0.004f), new Vector2(0.30f, 0.05f), 47, true);
        P(torso, "Hat", tri, hat, new Vector3(-0.02f, 0.63f, -0.005f), new Vector2(0.26f, 0.30f), 47, true);
        P(torso, "HatTip", ci, orb, new Vector3(-0.08f, 0.76f, -0.006f), new Vector2(0.05f, 0.05f), 48, false);
        // 杖（先端に光る玉）
        P(weaponPivot, "Shaft", rr, shaft, new Vector3(0, 0.18f, 0), new Vector2(0.045f, 0.50f), 49, true);
        P(weaponPivot, "OrbGlow", ci, new Color(orb.r, orb.g, orb.b, 0.35f), new Vector3(0, 0.45f, 0.01f), new Vector2(0.22f, 0.22f), 49, false);
        P(weaponPivot, "Orb", ci, orb, new Vector3(0, 0.45f, -0.01f), new Vector2(0.13f, 0.13f), 50, false);
    }

    // ======== 外部API ========
    public void PlayAttack(AttackStyle style = AttackStyle.Swing) { if (built && !dead) { oneShot = OneShot.Attack; attackStyle = style; oneShotT = 0f; } }
    public void PlayHurt() { if (built && !dead) { oneShot = OneShot.Hurt; oneShotT = 0f; } }
    public void PlayHeal() { if (built && !dead) { oneShot = OneShot.Heal; oneShotT = 0f; } }
    public float Facing => facing;
    public Vector3 MuzzlePos() => transform.position + new Vector3(0.17f * facing, 0.26f, 0f);
    public void FaceTowards(float worldX)
    {
        float d = worldX - transform.position.x;
        if (Mathf.Abs(d) > 0.001f) facing = d < 0f ? -1f : 1f;
        facingHold = 0.55f;
    }
    public void SetHP(float r)
    {
        r = Mathf.Clamp01(r);
        if (hpFill == null) return;
        hpFill.transform.localScale = new Vector3(HP_W * r, 0.045f, 1f);
        hpFill.transform.localPosition = new Vector3(-HP_W * 0.5f + HP_W * r * 0.5f, 0.46f, -0.01f);
        hpFill.color = r > 0.5f ? C("#5cc47c") : (r > 0f ? C("#e3a94a") : C("#df5a5a"));
    }
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
        if (!built) return;
        float dt = Time.deltaTime; time += dt;
        if (dead) { DeathUpdate(dt); return; }

        Vector3 wp = transform.position;
        float sp = (wp - prevPos).magnitude / Mathf.Max(dt, 1e-4f); prevPos = wp;
        bool moving = sp > 0.4f;
        if (facingHold > 0f) facingHold -= dt;
        else { float hdx = wp.x - faceRefX; if (Mathf.Abs(hdx) > 0.02f) { facing = hdx < 0f ? -1f : 1f; faceRefX = wp.x; } }
        flip.localScale = new Vector3(facing, 1f, 1f);

        float bobY = 0, lean = 0, wAng = 0, legA = 0, localX = 0; bool slashOn = false;
        if (moving) { float w = time * 7f; bobY = -0.02f * Mathf.Abs(Mathf.Sin(w)); legA = 16f * Mathf.Sin(w); lean = 3f; wAng = 8f * Mathf.Sin(w); }
        else { float w = time * 2.2f; bobY = 0.012f * Mathf.Sin(w); wAng = 3f * Mathf.Sin(w); }

        float hurtI = 0;
        if (oneShot == OneShot.Attack)
        {
            float dur = attackStyle == AttackStyle.Stab ? 0.30f : attackStyle == AttackStyle.Punch ? 0.28f : attackStyle == AttackStyle.Cast ? 0.45f : 0.40f;
            oneShotT += dt; float p = oneShotT / dur;
            if (p >= 1f) oneShot = OneShot.None;
            else
            {
                float e = Ease(p), s = Mathf.Sin(p * Mathf.PI);
                switch (attackStyle)
                {
                    case AttackStyle.Swing: wAng = -100f + e * 165f; lean = 7f * s; bobY -= 0.015f * s; slashOn = p > 0.32f && p < 0.68f; break;
                    case AttackStyle.Stab: wAng = -12f + e * 22f; localX = 0.09f * s; lean = 5f * s; slashOn = p > 0.2f && p < 0.6f; break;
                    case AttackStyle.Cast: wAng = -72f - 26f * s; bobY += 0.02f * s; lean = -3f * s; break;
                    case AttackStyle.Punch: wAng = -8f + e * 16f; localX = 0.08f * s; lean = 6f * s; break;
                }
            }
        }
        else if (oneShot == OneShot.Hurt)
        {
            oneShotT += dt; float p = oneShotT / 0.35f;
            if (p >= 1f) oneShot = OneShot.None;
            else { hurtI = 1f - p; lean = -6f * hurtI; localX = -0.05f * hurtI; }
        }
        else if (oneShot == OneShot.Heal)
        {
            oneShotT += dt; float p = oneShotT / 0.6f;
            if (p >= 1f) oneShot = OneShot.None;
            else { float s = Mathf.Sin(p * Mathf.PI); wAng = -70f - 30f * s; bobY += 0.03f * s; lean = -3f * s; }
        }

        bob.localPosition = new Vector3(localX, bobY, 0f);
        torso.localRotation = Quaternion.Euler(0, 0, lean);
        weaponPivot.localRotation = Quaternion.Euler(0, 0, wAng);
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
