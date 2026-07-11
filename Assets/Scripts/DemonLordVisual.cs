using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 魔王の見た目（手続き生成）。進化段階(Race)ごとに配色・角/王冠/翼などが変化。
/// 反撃演出(PlayReprisal)、門番による無敵オーラ(SetGuarded)、討伐演出(PlayDeath)。
/// timeScale=0(ゲームオーバー)でも動くよう unscaled で駆動。
/// </summary>
public class DemonLordVisual : MonoBehaviour
{
    private Transform rig, bob, guardRing;
    private SpriteRenderer hpFill, auraSR;
    private readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
    private readonly List<Color> baseCols = new List<Color>();
    private float t, reprisalT = 99f, deadT = -1f;
    private bool guarded;
    private const float HP_W = 0.6f;

    private static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

    private SpriteRenderer P(Transform p, string n, Sprite s, Color c, Vector3 pos, Vector2 sc, int o)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        go.transform.localPosition = pos; go.transform.localScale = new Vector3(sc.x, sc.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.color = c; sr.sortingOrder = o;
        parts.Add(sr); baseCols.Add(c);
        return sr;
    }

    public void BuildStage(DemonLord.Race race)
    {
        if (rig != null) Destroy(rig.gameObject);
        parts.Clear(); baseCols.Clear();
        deadT = -1f;
        var ci = PrimitiveSprites.Circle(); var sq = PrimitiveSprites.Square(); var rr = PrimitiveSprites.RoundRect(); var tri = PrimitiveSprites.Triangle();

        Color body, head, aura, eye, horn, accent;
        bool crown = false, wings = false, beard = false, blob = false, antler = false, bigHorn = false;
        switch (race)
        {
            case DemonLord.Race.Oni: body = C("#b83f34"); head = C("#d0664a"); aura = C("#e0704a"); eye = C("#ffe14a"); horn = C("#f0e6d0"); accent = C("#7a2a22"); bigHorn = true; break;
            case DemonLord.Race.Demon: body = C("#4a2c6e"); head = C("#6d3aa8"); aura = C("#b04ad0"); eye = C("#ff4a4a"); horn = C("#1e0a26"); accent = C("#2a1030"); wings = true; bigHorn = true; break;
            case DemonLord.Race.Elf: body = C("#2f6e5a"); head = C("#cfe0c0"); aura = C("#57c3a0"); eye = C("#a8f0d0"); horn = C("#dfeacf"); accent = C("#1f4a3a"); antler = true; break;
            case DemonLord.Race.Dwarf: body = C("#7a5a34"); head = C("#d0a878"); aura = C("#e3a94a"); eye = C("#ffe14a"); horn = C("#8a8f9a"); accent = C("#4a3720"); beard = true; bigHorn = true; break;
            case DemonLord.Race.Slime: body = C("#4aa85a"); head = C("#4aa85a"); aura = C("#6fd07a"); eye = C("#ffffff"); horn = C("#4aa85a"); accent = C("#2f7a3e"); blob = true; break;
            case DemonLord.Race.Vampire: body = C("#332633"); head = C("#e0dce6"); aura = C("#c04a5a"); eye = C("#ff3a4a"); horn = C("#e3a94a"); accent = C("#5a1a2a"); crown = true; wings = true; break;
            default: body = C("#5a4a8a"); head = C("#e6b98f"); aura = C("#8a6ad0"); eye = C("#ffd24a"); horn = C("#e3a94a"); accent = C("#3a2c60"); crown = true; break; // Human
        }

        rig = new GameObject("Rig").transform; rig.SetParent(transform, false);
        // オーラ（背後・脈動）
        auraSR = P(rig, "Aura", ci, new Color(aura.r, aura.g, aura.b, 0.18f), new Vector3(0, 0.15f, 0.1f), new Vector2(1.5f, 1.5f), 56);
        // 無敵オーラ（門番生存中のみ表示）
        guardRing = P(rig, "Guard", ci, new Color(0.4f, 0.8f, 1f, 0.25f), new Vector3(0, 0.15f, 0.09f), new Vector2(1.25f, 1.25f), 58).transform;
        guardRing.gameObject.SetActive(false);

        bob = new GameObject("Bob").transform; bob.SetParent(rig, false);

        if (wings)
        {
            P(bob, "WingL", tri, accent, new Vector3(-0.42f, 0.25f, 0.05f), new Vector2(0.5f, 0.6f), 57);
            P(bob, "WingR", tri, accent, new Vector3(0.42f, 0.25f, 0.05f), new Vector2(-0.5f, 0.6f), 57);
        }

        if (blob)
        {
            // スライム：粘液の塊＋目玉
            P(bob, "Blob", ci, new Color(body.r, body.g, body.b, 0.92f), new Vector3(0, 0.02f, 0), new Vector2(0.95f, 0.80f), 60);
            P(bob, "BlobHi", ci, new Color(1, 1, 1, 0.15f), new Vector3(-0.15f, 0.18f, -0.01f), new Vector2(0.30f, 0.22f), 61);
            P(bob, "EyeL", ci, eye, new Vector3(-0.12f, 0.08f, -0.02f), new Vector2(0.13f, 0.13f), 63);
            P(bob, "EyeR", ci, eye, new Vector3(0.14f, 0.10f, -0.02f), new Vector2(0.11f, 0.11f), 63);
            P(bob, "PupL", ci, C("#222"), new Vector3(-0.12f, 0.06f, -0.03f), new Vector2(0.05f, 0.05f), 64);
            P(bob, "PupR", ci, C("#222"), new Vector3(0.14f, 0.08f, -0.03f), new Vector2(0.045f, 0.045f), 64);
        }
        else
        {
            // ローブ下部＋胴＋腕（爪）＋頭
            P(bob, "Robe", rr, body, new Vector3(0, -0.28f, 0), new Vector2(0.72f, 0.5f), 59);
            P(bob, "Torso", rr, body, new Vector3(0, 0.10f, 0), new Vector2(0.52f, 0.5f), 60);
            P(bob, "TorsoShade", rr, accent, new Vector3(0.12f, 0.10f, -0.005f), new Vector2(0.16f, 0.5f), 60);
            // 腕＋爪
            for (int side = -1; side <= 1; side += 2)
            {
                var arm = P(bob, "Arm", rr, body, new Vector3(0.30f * side, 0.06f, 0), new Vector2(0.14f, 0.34f), 60);
                P(bob, "Claw", tri, C("#e8e0cc"), new Vector3(0.32f * side, -0.14f, -0.01f), new Vector2(0.16f, -0.16f), 61);
            }
            // 頭
            P(bob, "Head", ci, head, new Vector3(0, 0.50f, 0), new Vector2(0.40f, 0.40f), 62);
            if (beard) P(bob, "Beard", rr, C("#d8d0c0"), new Vector3(0, 0.40f, -0.01f), new Vector2(0.30f, 0.26f), 63);
            // 目
            P(bob, "EyeL", ci, eye, new Vector3(-0.08f, 0.52f, -0.02f), new Vector2(0.08f, 0.08f), 63);
            P(bob, "EyeR", ci, eye, new Vector3(0.08f, 0.52f, -0.02f), new Vector2(0.08f, 0.08f), 63);

            // 角・王冠
            if (antler)
            {
                P(bob, "AntlerL", tri, horn, new Vector3(-0.12f, 0.66f, -0.005f), new Vector2(0.10f, 0.22f), 63);
                P(bob, "AntlerR", tri, horn, new Vector3(0.12f, 0.66f, -0.005f), new Vector2(0.10f, 0.22f), 63);
                P(bob, "AntlerL2", tri, horn, new Vector3(-0.20f, 0.66f, -0.005f), new Vector2(0.07f, 0.15f), 63);
                P(bob, "AntlerR2", tri, horn, new Vector3(0.20f, 0.66f, -0.005f), new Vector2(0.07f, 0.15f), 63);
            }
            else if (bigHorn)
            {
                P(bob, "HornL", tri, horn, new Vector3(-0.13f, 0.66f, -0.005f), new Vector2(0.14f, 0.26f), 63);
                P(bob, "HornR", tri, horn, new Vector3(0.13f, 0.66f, -0.005f), new Vector2(0.14f, 0.26f), 63);
            }
            if (crown)
            {
                P(bob, "CrownBand", sq, C("#e3a94a"), new Vector3(0, 0.66f, -0.02f), new Vector2(0.34f, 0.06f), 64);
                P(bob, "CrownM", tri, C("#e3a94a"), new Vector3(0, 0.74f, -0.02f), new Vector2(0.10f, 0.13f), 64);
                P(bob, "CrownL", tri, C("#e3a94a"), new Vector3(-0.12f, 0.72f, -0.02f), new Vector2(0.09f, 0.10f), 64);
                P(bob, "CrownR", tri, C("#e3a94a"), new Vector3(0.12f, 0.72f, -0.02f), new Vector2(0.09f, 0.10f), 64);
            }
        }

        // HPバー
        P(rig, "HPbg", sq, C("#2a2233"), new Vector3(0, 0.92f, 0f), new Vector2(HP_W + 0.03f, 0.08f), 66);
        hpFill = P(rig, "HPfill", sq, C("#df5a5a"), new Vector3(0, 0.92f, -0.01f), new Vector2(HP_W, 0.06f), 67);
        SetHP(1f);
    }

    // ======== API ========
    public void SetGuarded(bool g) { guarded = g; if (guardRing != null) guardRing.gameObject.SetActive(g); }
    public void PlayReprisal()
    {
        reprisalT = 0f;
        BattleVfx.Burst(transform.position + new Vector3(0, 0.1f, 0), new Color(0.7f, 0.2f, 0.5f, 1f), 1.1f); // 暗い衝撃波
    }
    public void PlayDeath() { if (deadT < 0f) deadT = 0f; }
    public void SetHP(float r)
    {
        r = Mathf.Clamp01(r);
        if (hpFill == null) return;
        hpFill.transform.localScale = new Vector3(HP_W * r, 0.06f, 1f);
        hpFill.transform.localPosition = new Vector3(-HP_W * 0.5f + HP_W * r * 0.5f, 0.92f, -0.01f);
    }

    private void Update()
    {
        // リグ未構築 or パーツ未生成（BuildStage前/フロア切替の一瞬）ならアニメを止める。
        // baseCols[0] 等のインデックスアクセスがある行を空リストで踏まないためのガード。
        if (rig == null || bob == null || parts.Count == 0 || baseCols.Count == 0) return;
        float dt = Time.unscaledDeltaTime; t += dt;

        // 討伐演出（timeScale=0でも進む）
        if (deadT >= 0f)
        {
            deadT += dt; float p = Mathf.Clamp01(deadT / 1.1f); float e = 1f - Mathf.Pow(1f - p, 3f);
            rig.localRotation = Quaternion.Euler(0, 0, 18f * Mathf.Sin(p * 30f) * (1f - p));
            rig.localScale = Vector3.one * (1f - 0.4f * e);
            rig.localPosition = new Vector3(0, -0.2f * e, 0);
            for (int i = 0; i < parts.Count; i++) { var c = baseCols[i]; c.a *= (1f - e); parts[i].color = c; }
            return;
        }

        // 待機：浮遊＋オーラ脈動
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 1.6f);
        bob.localPosition = new Vector3(0, 0.03f * Mathf.Sin(t * 1.6f), 0);
        if (auraSR != null) { auraSR.transform.localScale = Vector3.one * (1.5f + 0.12f * pulse); var c = baseCols[0]; c.a = 0.12f + 0.10f * pulse; auraSR.color = c; }
        if (guarded && guardRing != null) { guardRing.localScale = Vector3.one * (1.2f + 0.10f * pulse); }

        // 反撃：前傾＋スケールの一撃
        if (reprisalT < 0.35f)
        {
            reprisalT += dt; float p = reprisalT / 0.35f; float s = Mathf.Sin(p * Mathf.PI);
            bob.localPosition += new Vector3(0, -0.08f * s, 0);
            bob.localScale = new Vector3(1f + 0.10f * s, 1f - 0.06f * s, 1f);
        }
        else bob.localScale = Vector3.one;
    }
}
