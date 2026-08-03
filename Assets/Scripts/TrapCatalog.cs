using UnityEngine;

/// <summary>
/// 罠の種類（領域研究で解禁）。通常罠は最初から、5種の状態異常罠は領域研究の各ノードで解禁。
/// 処理はStep1どおり RoomData タイル（盗賊のMP解除・クールダウン）を流用し、踏んだ冒険者に状態異常を付与する。
///
/// ⚖️ バランス設計（2026-07-28 改修）:
///  - **固定ダメージだけだと冒険者HPの伸びに置いていかれて死に要素になる**ため、
///    全ての罠に『対象の最大HPの◯%』成分(hpFrac / dotHpFrac)を持たせた。これで後半も腐らない。
///  - 逆に**毒などのDoTは持続時間の倍率(感情『呪縛』×遺物『呪縛の鎖』= 最大2.25倍)がそのまま総ダメージ倍率**
///    になっていて強すぎた。DoTの基礎dpsを下げ、瞬間ダメージ側に重心を移して差を詰めた。
///  - 研究 d_trap_pow1/2/3 で全罠のダメージが伸びる（＝罠に投資する道が生まれる）。
/// 関連: [[Research]] (d_trap_*) / RoomData.trapKind / AdventurerAI(状態異常) / DungeonFeatureManager(配置・永続化)。
/// </summary>
public enum TrapKind { Basic, Poison, Fire, Ice, Electric, Bleed }

public static class TrapCatalog
{
    public struct Def
    {
        public TrapKind kind;
        public string name;
        public string desc;
        public Color color;
        public int dpCost;
        public float damage;      // 踏んだ瞬間の固定ダメージ
        public float hpFrac;      // 踏んだ瞬間に加算される『対象の最大HP比』ダメージ（後半で腐らせないため）
        public float statusPower; // 状態異常の強さ（DoTのdps / 凍結・麻痺は未使用）
        public float dotHpFrac;   // DoTの毎秒ダメージに加算される『対象の最大HP比』
        public float statusDur;   // 状態異常の持続秒
        public string researchId; // 解禁研究ノード（""=最初から）
    }

    private static readonly Def[] _all = new Def[]
    {
        //                                                                       cost  dmg  hp%   dps  dot%   dur
        D(TrapKind.Basic,    "通常の罠", "踏むと大ダメージ（即死性が高い）",   new Color(0.60f,0.60f,0.66f), 150, 24f, 0.060f, 0f,   0f,     0f,   ""),
        D(TrapKind.Poison,   "毒沼",     "毒＝継続ダメージ（長く効く）",       new Color(0.45f,0.80f,0.35f), 200,  6f, 0.015f, 3.0f, 0.010f, 5f,   "d_trap_poison"),
        D(TrapKind.Fire,     "炎の罠",   "やけど＝短く強い継続ダメージ",       new Color(0.95f,0.55f,0.25f), 200, 10f, 0.030f, 5.0f, 0.014f, 4f,   "d_trap_fire"),
        D(TrapKind.Ice,      "氷の罠",   "凍結＝一定時間動けない（足止め）",   new Color(0.45f,0.80f,0.95f), 260, 12f, 0.035f, 0f,   0f,     2.5f, "d_trap_ice"),
        D(TrapKind.Electric, "電気の罠", "麻痺＝周期的に短く停止",             new Color(0.95f,0.85f,0.35f), 260, 10f, 0.030f, 0f,   0f,     4f,   "d_trap_shock"),
        D(TrapKind.Bleed,    "針の罠",   "出血＝継続ダメージ",                 new Color(0.87f,0.35f,0.40f), 220, 10f, 0.030f, 3.5f, 0.011f, 4f,   "d_trap_bleed"),
    };

    private static Def D(TrapKind k, string n, string desc, Color c, int cost, float dmg, float hpf, float sp, float dotf, float sd, string rid)
        => new Def { kind = k, name = n, desc = desc, color = c, dpCost = cost, damage = dmg, hpFrac = hpf, statusPower = sp, dotHpFrac = dotf, statusDur = sd, researchId = rid };

    public static int Count => _all.Length;
    public static Def Get(int kind) => _all[Mathf.Clamp(kind, 0, _all.Length - 1)];
    public static Def Get(TrapKind kind) => _all[(int)kind];

    // 通常罠は常時、状態異常罠は領域研究で解禁済みか
    public static bool IsUnlocked(int kind)
    {
        var d = Get(kind);
        return string.IsNullOrEmpty(d.researchId) || ResearchState.IsResearched(d.researchId);
    }

    // ============ ⚖️ 研究による罠の強化 ============
    /// <summary>研究 d_trap_pow1/2/3 による全罠のダメージ倍率。</summary>
    public static float PowerMult()
    {
        float m = 1f;
        if (ResearchState.IsResearched("d_trap_pow1")) m *= 1.35f;
        if (ResearchState.IsResearched("d_trap_pow2")) m *= 1.35f;
        if (ResearchState.IsResearched("d_trap_pow3")) m *= 1.40f;
        m *= WonderCatalog.TrapDamageMult;   // ★ 遺産『囁きの迷路』
        m *= PolicySystem.TrapDamageMult;    // 🏛️ 政策『罠の刻印』
        return m;
    }
    /// <summary>『最大HP比』成分の倍率。d_trap_pow3（貫通機構）で大きく伸び、高HPの相手に刺さるようになる。</summary>
    public static float HpFracMult() => ResearchState.IsResearched("d_trap_pow3") ? 1.8f : 1f;

    /// <summary>踏んだ瞬間のダメージ（固定＋最大HP比）。研究倍率込み。</summary>
    public static float InstantDamage(int kind, float targetMaxHP)
    {
        var d = Get(kind);
        return (d.damage + targetMaxHP * d.hpFrac * HpFracMult()) * PowerMult();
    }
    /// <summary>DoTの毎秒ダメージ（固定＋最大HP比）。研究倍率込み。</summary>
    public static float DotPerSecond(int kind, float targetMaxHP)
    {
        var d = Get(kind);
        if (d.statusPower <= 0f && d.dotHpFrac <= 0f) return 0f;
        return (d.statusPower + targetMaxHP * d.dotHpFrac * HpFracMult()) * PowerMult();
    }
}
