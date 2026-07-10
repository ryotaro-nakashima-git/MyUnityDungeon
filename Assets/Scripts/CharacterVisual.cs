using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// キャラの見た目を手続きパーツで組み、コードでアニメする。
/// ジョブ別リグ(戦士/盗賊/聖職者/魔法使い)＋眷属リグ(不死/獣/魔族)＋門番(拡大＋王冠)。
/// 攻撃スタイル(斬/刺/詠唱/素手/爪)。歩行/待機は移動自動検知、攻撃/被弾/回復は一発。
/// 死亡: 冒険者はDie()で切離し自壊、眷属はSetDowned()で倒れ状態(復活可)。向きは進行方向/対象で反転。
/// </summary>
public class CharacterVisual : MonoBehaviour
{
    public enum RigType { Warrior, Thief, Cleric, Mage, Undead, Beast, Demonkin }
    public enum AttackStyle { Swing, Stab, Cast, Punch, Claw }
    private enum OneShot { None, Attack, Hurt, Heal }

    private Transform flip, bob, torso, hipL, hipR, weaponPivot;
    private SpriteRenderer shadowSR, slashSR, hpFill;
    private readonly List<SpriteRenderer> srs = new List<SpriteRenderer>();
    private readonly List<Color> baseCols = new List<Color>();
    private readonly List<bool> tintable = new List<bool>();

    private RigType rig;
    private AttackStyle attackStyle = AttackStyle.Swing;
    private OneShot oneShot = OneShot.None;
    private float oneShotT, time, deadT, baseLean;
    private bool dead, built, downed;
    private float facing = 1f, faceRefX, facingHold;
    private Vector3 prevPos;
    private Color slashColor = Color.white;
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

    /// <summary>リグ構築（AddComponent後にAI側が呼ぶ）。scale=門番など拡大、crown=王冠。</summary>
    public void Init(RigType type, float scale = 1f, bool crown = false)
    {
        if (built) return;
        rig = type;
        transform.localScale = Vector3.one * scale;
        var sq = PrimitiveSprites.Square(); var ci = PrimitiveSprites.Circle(); var rr = PrimitiveSprites.RoundRect(); var tri = PrimitiveSprites.Triangle();
        Color gold = C("#e3a94a"), skin = C("#e6b98f"), blade = C("#cdd2dd");

        bool monster = type >= RigType.Undead;
        Color body, bodyDk, legc, head;
        switch (type)
        {
            case RigType.Thief: body = C("#4f6b45"); bodyDk = C("#3c5335"); legc = C("#2c3a28"); head = skin; break;
            case RigType.Cleric: body = C("#d8dbe6"); bodyDk = C("#b9bfd0"); legc = C("#8f95a8"); head = skin; break;
            case RigType.Mage: body = C("#4a54a0"); bodyDk = C("#3a4382"); legc = C("#2c3466"); head = skin; break;
            case RigType.Undead: body = C("#5f8f4f"); bodyDk = C("#4a7040"); legc = C("#3a5a32"); head = C("#6fa85a"); break;
            case RigType.Beast: body = C("#d07a3a"); bodyDk = C("#a85f28"); legc = C("#7a4520"); head = C("#d88a45"); break;
            case RigType.Demonkin: body = C("#8a4fd0"); bodyDk = C("#6d3aa8"); legc = C("#4a2c7a"); head = C("#9a5fd8"); break;
            default: body = C("#c0453f"); bodyDk = C("#a8352f"); legc = C("#2f2a38"); head = skin; break; // Warrior
        }
        slashColor = monster ? C("#ff6a5a") : Color.white;
        baseLean = type == RigType.Undead ? 9f : type == RigType.Beast ? 7f : type == RigType.Demonkin ? 5f : 0f;

        // 影・HPバー
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

        // 胴・頭
        torso = Node(bob, "Torso", new Vector3(0, -0.24f, 0));
        P(torso, "Body", rr, body, new Vector3(0, 0.19f, 0), new Vector2(0.28f, 0.34f), 44, true);
        P(torso, "BodyShade", rr, bodyDk, new Vector3(0.07f, 0.19f, -0.005f), new Vector2(0.10f, 0.34f), 45, true);
        P(torso, "Head", ci, head, new Vector3(0, 0.44f, 0), new Vector2(0.22f, 0.22f), 46, true);
        weaponPivot = Node(torso, "WeaponPivot", new Vector3(0.17f, 0.18f, -0.02f));

        switch (type)
        {
            case RigType.Warrior: BuildWarriorGear(sq, ci, rr, gold, blade, C("#8a8f9a"), C("#6d7581"), C("#5f6672"), body); break;
            case RigType.Thief: BuildThiefGear(sq, ci, rr, tri, gold, blade, C("#33463b"), C("#6d5535")); break;
            case RigType.Cleric: BuildClericGear(sq, ci, rr, gold, C("#eef1f7"), C("#57c3d0"), C("#7a5a34")); break;
            case RigType.Mage: BuildMageGear(sq, ci, rr, tri, C("#5b3f9e"), C("#452f7a"), C("#8bd0ff"), C("#6d5535")); break;
            case RigType.Undead: BuildMonster(sq, ci, tri, body, C("#f2ec66"), C("#dfe6cf"), false, false); break;
            case RigType.Beast: BuildMonster(sq, ci, tri, body, C("#ffd15a"), C("#f0e6cf"), true, false); break;
            case RigType.Demonkin: BuildMonster(sq, ci, tri, body, C("#ff5a6a"), C("#2a1030"), true, true); break;
        }
        if (crown) BuildCrown(sq, tri, gold);

        built = true;
        SetHP(1f);
    }

    // ---- 人型ジョブ ----
    private void BuildWarriorGear(Sprite sq, Sprite ci, Sprite rr, Color gold, Color blade, Color helmet, Color helmetDk, Color shield, Color crest)
    {
        P(torso, "Shield", ci, shield, new Vector3(-0.17f, 0.16f, 0.01f), new Vector2(0.23f, 0.23f), 42, true);
        P(torso, "ShieldBoss", ci, gold, new Vector3(-0.17f, 0.16f, 0f), new Vector2(0.07f, 0.07f), 43, false);
        P(torso, "Belt", sq, gold, new Vector3(0, 0.10f, -0.01f), new Vector2(0.28f, 0.05f), 45, false);
        P(torso, "Nose", sq, helmetDk, new Vector3(0, 0.44f, -0.01f), new Vector2(0.05f, 0.14f), 47, true);
        P(torso, "Helmet", ci, helmet, new Vector3(0, 0.51f, -0.005f), new Vector2(0.25f, 0.16f), 47, true);
        P(torso, "Crest", sq, crest, new Vector3(0, 0.60f, -0.01f), new Vector2(0.05f, 0.09f), 47, true);
        P(weaponPivot, "Hilt", sq, gold, new Vector3(0, 0.02f, 0), new Vector2(0.05f, 0.10f), 50, false);
        P(weaponPivot, "Guard", sq, gold, new Vector3(0, 0.06f, 0), new Vector2(0.13f, 0.035f), 50, false);
        P(weaponPivot, "Blade", rr, blade, new Vector3(0, 0.28f, 0), new Vector2(0.05f, 0.42f), 49, true);
    }
    private void BuildThiefGear(Sprite sq, Sprite ci, Sprite rr, Sprite tri, Color gold, Color blade, Color hood, Color hilt)
    {
        P(torso, "Hood", ci, hood, new Vector3(0, 0.47f, 0.005f), new Vector2(0.27f, 0.26f), 45, true);
        P(torso, "HoodPeak", tri, hood, new Vector3(-0.10f, 0.55f, 0.006f), new Vector2(0.14f, 0.16f), 45, true);
        P(torso, "Sash", sq, C("#2c3a28"), new Vector3(0, 0.12f, -0.01f), new Vector2(0.28f, 0.04f), 45, false);
        P(weaponPivot, "Hilt", sq, hilt, new Vector3(0, 0.02f, 0), new Vector2(0.05f, 0.08f), 50, false);
        P(weaponPivot, "Guard", sq, gold, new Vector3(0, 0.05f, 0), new Vector2(0.10f, 0.03f), 50, false);
        P(weaponPivot, "Blade", rr, blade, new Vector3(0, 0.16f, 0), new Vector2(0.045f, 0.22f), 49, true);
    }
    private void BuildClericGear(Sprite sq, Sprite ci, Sprite rr, Color gold, Color cowl, Color trim, Color shaft)
    {
        P(torso, "Cowl", ci, cowl, new Vector3(0, 0.47f, 0.005f), new Vector2(0.27f, 0.25f), 45, true);
        P(torso, "Band", sq, trim, new Vector3(0, 0.36f, -0.01f), new Vector2(0.22f, 0.03f), 47, false);
        P(torso, "Sash", sq, trim, new Vector3(0, 0.12f, -0.01f), new Vector2(0.28f, 0.04f), 45, false);
        P(weaponPivot, "Shaft", rr, shaft, new Vector3(0, 0.18f, 0), new Vector2(0.045f, 0.50f), 49, true);
        P(weaponPivot, "Orb", ci, gold, new Vector3(0, 0.43f, -0.01f), new Vector2(0.12f, 0.12f), 50, false);
        P(weaponPivot, "CrossV", sq, trim, new Vector3(0, 0.43f, -0.02f), new Vector2(0.03f, 0.10f), 51, false);
        P(weaponPivot, "CrossH", sq, trim, new Vector3(0, 0.45f, -0.02f), new Vector2(0.08f, 0.03f), 51, false);
    }
    private void BuildMageGear(Sprite sq, Sprite ci, Sprite rr, Sprite tri, Color hat, Color hatDk, Color orb, Color shaft)
    {
        P(torso, "Brim", rr, hatDk, new Vector3(0, 0.50f, -0.004f), new Vector2(0.30f, 0.05f), 47, true);
        P(torso, "Hat", tri, hat, new Vector3(-0.02f, 0.63f, -0.005f), new Vector2(0.26f, 0.30f), 47, true);
        P(torso, "HatTip", ci, orb, new Vector3(-0.08f, 0.76f, -0.006f), new Vector2(0.05f, 0.05f), 48, false);
        P(weaponPivot, "Shaft", rr, shaft, new Vector3(0, 0.18f, 0), new Vector2(0.045f, 0.50f), 49, true);
        P(weaponPivot, "OrbGlow", ci, new Color(orb.r, orb.g, orb.b, 0.35f), new Vector3(0, 0.45f, 0.01f), new Vector2(0.22f, 0.22f), 49, false);
        P(weaponPivot, "Orb", ci, orb, new Vector3(0, 0.45f, -0.01f), new Vector2(0.13f, 0.13f), 50, false);
    }

    // ---- 眷属モンスター（不死/獣/魔族）----
    private void BuildMonster(Sprite sq, Sprite ci, Sprite tri, Color body, Color eye, Color accent, bool horns, bool wings)
    {
        // 目（光る）
        P(torso, "EyeL", ci, eye, new Vector3(-0.05f, 0.45f, -0.02f), new Vector2(0.05f, 0.05f), 47, false);
        P(torso, "EyeR", ci, eye, new Vector3(0.05f, 0.45f, -0.02f), new Vector2(0.05f, 0.05f), 47, false);
        if (horns)
        {
            P(torso, "HornL", tri, accent, new Vector3(-0.08f, 0.56f, -0.006f), new Vector2(0.08f, 0.13f), 47, true);
            P(torso, "HornR", tri, accent, new Vector3(0.08f, 0.56f, -0.006f), new Vector2(0.08f, 0.13f), 47, true);
        }
        else
        {
            // 不死：牙/裂けた口
            P(torso, "Fang", tri, C("#dfe6cf"), new Vector3(0, 0.40f, -0.02f), new Vector2(0.10f, -0.07f), 47, false);
        }
        if (wings)
        {
            P(torso, "WingL", tri, accent, new Vector3(-0.18f, 0.22f, 0.02f), new Vector2(0.20f, 0.24f), 41, true);
            P(torso, "WingR", tri, accent, new Vector3(0.18f, 0.22f, 0.02f), new Vector2(-0.20f, 0.24f), 41, true);
        }
        // 尻尾（後方・下）
        P(torso, "Tail", ci, body, new Vector3(-0.16f, 0.02f, 0.02f), new Vector2(0.12f, 0.07f), 42, true);
        // 爪（武器ピボット＝手）
        P(weaponPivot, "Fist", ci, body, new Vector3(0, 0.04f, 0), new Vector2(0.12f, 0.12f), 50, true);
        P(weaponPivot, "ClawA", tri, C("#e8e0cc"), new Vector3(-0.04f, 0.12f, -0.01f), new Vector2(0.05f, 0.09f), 51, false);
        P(weaponPivot, "ClawB", tri, C("#e8e0cc"), new Vector3(0.00f, 0.13f, -0.01f), new Vector2(0.05f, 0.10f), 51, false);
        P(weaponPivot, "ClawC", tri, C("#e8e0cc"), new Vector3(0.04f, 0.12f, -0.01f), new Vector2(0.05f, 0.09f), 51, false);
    }

    private void BuildCrown(Sprite sq, Sprite tri, Color gold)
    {
        P(torso, "CrownBand", sq, gold, new Vector3(0, 0.55f, -0.02f), new Vector2(0.20f, 0.04f), 52, false);
        P(torso, "CrownM", tri, gold, new Vector3(0, 0.60f, -0.02f), new Vector2(0.07f, 0.09f), 52, false);
        P(torso, "CrownL", tri, gold, new Vector3(-0.08f, 0.59f, -0.02f), new Vector2(0.06f, 0.07f), 52, false);
        P(torso, "CrownR", tri, gold, new Vector3(0.08f, 0.59f, -0.02f), new Vector2(0.06f, 0.07f), 52, false);
    }

    // ======== 外部API ========
    public void PlayAttack(AttackStyle style = AttackStyle.Swing) { if (built && !dead && !downed) { oneShot = OneShot.Attack; attackStyle = style; oneShotT = 0f; } }
    public void PlayHurt() { if (built && !dead && !downed) { oneShot = OneShot.Hurt; oneShotT = 0f; } }
    public void PlayHeal() { if (built && !dead && !downed) { oneShot = OneShot.Heal; oneShotT = 0f; } }
    public float Facing => facing;
    public Vector3 MuzzlePos() => transform.position + new Vector3(0.17f * facing * transform.localScale.x, 0.26f * transform.localScale.y, 0f);
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
    /// <summary>冒険者用: 倒れて切り離し自壊。</summary>
    public void Die()
    {
        if (dead) return;
        dead = true; deadT = 0f;
        transform.SetParent(null, true);
    }
    /// <summary>眷属用: 倒れ状態(復活可)。true=ダウン, false=復帰。</summary>
    public void SetDowned(bool v)
    {
        if (!built) return;
        downed = v;
        oneShot = OneShot.None;
        if (v)
        {
            flip.localRotation = Quaternion.Euler(0, 0, -72f * facing);
            flip.localPosition = new Vector3(0, -0.10f, 0);
            for (int i = 0; i < srs.Count; i++) { var b = baseCols[i]; float g = (b.r + b.g + b.b) / 3f; srs[i].color = new Color(Mathf.Lerp(g, b.r, 0.4f), Mathf.Lerp(g, b.g, 0.4f), Mathf.Lerp(g, b.b, 0.4f), b.a * (srs[i] == shadowSR ? 0.5f : 0.6f)); }
        }
        else
        {
            flip.localRotation = Quaternion.identity; flip.localPosition = Vector3.zero;
            for (int i = 0; i < srs.Count; i++) srs[i].color = baseCols[i];
        }
    }

    // ======== アニメ ========
    private static float Ease(float p) => 1f - Mathf.Pow(1f - p, 3f);

    private void Update()
    {
        if (!built) return;
        float dt = Time.deltaTime; time += dt;
        if (dead) { DeathUpdate(dt); return; }
        if (downed) return; // 倒れ状態は固定

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
            float dur = attackStyle == AttackStyle.Stab ? 0.30f : attackStyle == AttackStyle.Punch ? 0.28f : attackStyle == AttackStyle.Cast ? 0.45f : attackStyle == AttackStyle.Claw ? 0.34f : 0.40f;
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
                    case AttackStyle.Claw: wAng = -55f + e * 95f; localX = 0.11f * s; lean = 10f * s; bobY -= 0.02f * s; slashOn = p > 0.25f && p < 0.65f; break;
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
        torso.localRotation = Quaternion.Euler(0, 0, lean + baseLean);
        weaponPivot.localRotation = Quaternion.Euler(0, 0, wAng);
        hipL.localRotation = Quaternion.Euler(0, 0, legA);
        hipR.localRotation = Quaternion.Euler(0, 0, -legA);
        if (slashSR != null) { var c = slashColor; c.a = slashOn ? 0.85f : 0f; slashSR.color = c; }
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
