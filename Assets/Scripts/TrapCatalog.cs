using UnityEngine;

/// <summary>
/// 罠の種類（領域研究で解禁）。通常罠は最初から、5種の状態異常罠は領域研究の各ノードで解禁。
/// 処理はStep1どおり RoomData タイル（盗賊のMP解除・クールダウン）を流用し、踏んだ冒険者に状態異常を付与する。
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
        public float damage;      // 踏んだ瞬間のダメージ
        public float statusPower; // 状態異常の強さ（DoTのdps / 凍結・麻痺の秒数）
        public float statusDur;   // 状態異常の持続秒
        public string researchId; // 解禁研究ノード（""=最初から）
    }

    private static readonly Def[] _all = new Def[]
    {
        D(TrapKind.Basic,    "通常の罠", "踏むとダメージ",                 new Color(0.60f,0.60f,0.66f), 150, 20f, 0f,  0f,  ""),
        D(TrapKind.Poison,   "毒沼",     "毒＝継続ダメージ",               new Color(0.45f,0.80f,0.35f), 200,  6f, 5f,  5f,  "d_trap_poison"),
        D(TrapKind.Fire,     "炎の罠",   "やけど＝強めの継続ダメージ",     new Color(0.95f,0.55f,0.25f), 200,  8f, 9f,  4f,  "d_trap_fire"),
        D(TrapKind.Ice,      "氷の罠",   "凍結＝一定時間動けない",         new Color(0.45f,0.80f,0.95f), 260, 10f, 0f,  2.5f,"d_trap_ice"),
        D(TrapKind.Electric, "電気の罠", "麻痺＝周期的に短く停止",         new Color(0.95f,0.85f,0.35f), 260,  8f, 0f,  4f,  "d_trap_shock"),
        D(TrapKind.Bleed,    "針の罠",   "出血＝継続ダメージ",             new Color(0.87f,0.35f,0.40f), 220,  8f, 6f,  4f,  "d_trap_bleed"),
    };

    private static Def D(TrapKind k, string n, string desc, Color c, int cost, float dmg, float sp, float sd, string rid)
        => new Def { kind = k, name = n, desc = desc, color = c, dpCost = cost, damage = dmg, statusPower = sp, statusDur = sd, researchId = rid };

    public static int Count => _all.Length;
    public static Def Get(int kind) => _all[Mathf.Clamp(kind, 0, _all.Length - 1)];
    public static Def Get(TrapKind kind) => _all[(int)kind];

    // 通常罠は常時、状態異常罠は領域研究で解禁済みか
    public static bool IsUnlocked(int kind)
    {
        var d = Get(kind);
        return string.IsNullOrEmpty(d.researchId) || ResearchState.IsResearched(d.researchId);
    }
}
