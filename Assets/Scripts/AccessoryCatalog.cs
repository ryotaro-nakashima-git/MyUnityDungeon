using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 💍 装飾品（CDO2の「装備」にあたる第3の枠）。武器・防具とは別に、個体へ**1つだけ**着ける。
///
/// **なぜ要るか**：いまの装備は武器/防具の**グレードを上げるだけ**で、
/// 何を着けても「同じ個体が少し強くなる」しか起きない。CDO2 の装備は 70〜80種あって、
/// **どれを誰に着けるか**が編成の判断になっている。ここではその役を装飾品に持たせる。
///
/// **設計の芯**
/// - ⚠ 効果は **既に実装済みの魔物スキル（`MinionSkillKind`）を1つ付与する**形にした。
///   新しい効果の仕組みを作ると「押せるのに何も起きない装飾品」ができる（→ 習熟で立てた原則）。
///   スキルは `ZombieAI.ApplySkillsOnSpawn` が既に全部解釈するので、配線は付与の1本で済む。
/// - 小さな数値（HP/攻撃/速度）も併せて持つが、**主役はスキル**。数値はグレード装備の役目。
/// - ⚠ グレード（下級〜最高級）は持たせない。武器防具のグレードと二重になるうえ、
///   「同じ装飾品の上位版」が増えると選ぶ意味が薄れる。**種類で選ばせる**。
///
/// 入手（CDO2に倣う）：**行商人から買う**／**ターンクリア報酬から選ぶ**の2経路。
/// ⚠⚠ `defs` の並び順（index）は `Individual.accessory` としてセーブに載る。
///   **絶対に変えない。新しい装飾品は末尾に足す。**
/// 関連: [[MinionSkill]] [[EquipmentCatalog]] [[RelicManager]]（層の役割分担）。
/// </summary>
public static class AccessoryCatalog
{
    public struct Def
    {
        public string id, jpName, desc, colorHex;
        public MinionSkillKind grant;   // 付与する魔物スキル（None なら数値だけ）
        public float hpMult, atkMult, spdMult;
        public int price;               // 行商人での値段（DP）
        public int rarity;              // 0=よく出る 1=たまに 2=稀
    }

    // ⚠⚠ 並び順を変えない（個体のセーブに載る）。新しいものは末尾へ。
    private static readonly Def[] defs =
    {
        A("thorn_mail",  "棘の胴当て",   MinionSkillKind.Thorns,      1.10f, 1.00f, 1.00f, 900,  0, "#9aa3b0",
          "殴られるたびに棘が返す。前で受ける個体ほど働く。"),
        A("venom_fang",  "毒牙の首飾り", MinionSkillKind.PoisonBody,  1.00f, 1.05f, 1.00f, 950,  0, "#5cc47c",
          "殴ってきた相手を毒に侵す。硬い相手を時間で削る。"),
        A("swift_anklet","疾風の足環",   MinionSkillKind.Swift,       0.95f, 1.00f, 1.20f, 1000, 0, "#e3a94a",
          "動きと手数が上がる。数を捌く役に向く。"),
        A("regen_moss",  "再生の苔",     MinionSkillKind.Regen,       1.05f, 1.00f, 1.00f, 1100, 0, "#57c3ab",
          "少しずつ傷が塞がる。波と波のあいだに立て直せる。"),
        A("pack_totem",  "群れの護符",   MinionSkillKind.PackTactics, 1.00f, 1.05f, 1.00f, 1200, 1, "#e08a3c",
          "周りに味方が多いほど強い。固めて置く編成の要。"),
        A("dread_mask",  "威圧の面",     MinionSkillKind.Intimidate,  1.05f, 1.00f, 0.95f, 1300, 1, "#b478e6",
          "周囲の冒険者の手を鈍らせる。数で来る波に効く。"),
        A("undying_seal","不屈の刻印",   MinionSkillKind.Undying,     1.00f, 1.00f, 1.00f, 1800, 2, "#ffd24a",
          "致命の一撃を一度だけ耐える。落としたくない1体に。"),
        A("blast_core",  "自爆の核",     MinionSkillKind.SelfDestruct,0.90f, 1.10f, 1.00f, 1400, 1, "#e05a5a",
          "倒れる瞬間に大きく爆ぜる。捨て石が捨て石で終わらない。"),
        A("gaze_eye",    "石化の義眼",   MinionSkillKind.PetrifyGaze, 1.00f, 1.00f, 0.95f, 1900, 2, "#9c95b4",
          "攻撃のたびに相手が止まることがある。強敵の足を奪う。"),
        A("heal_bell",   "治癒の鈴",     MinionSkillKind.HealAura,    1.05f, 0.95f, 1.00f, 1600, 1, "#8ce0a8",
          "周期的に周りの味方を癒す。後衛に1つあると崩れにくい。"),
        A("war_horn",    "戦の角笛",     MinionSkillKind.Roar,        1.00f, 1.05f, 1.05f, 1500, 1, "#e3c34a",
          "戦いの始めに周りを奮い立たせる。先頭に置く1体へ。"),
        A("drain_ring",  "吸命の指輪",   MinionSkillKind.Lifedrain,   1.00f, 1.10f, 1.00f, 2000, 2, "#c04a6a",
          "与えた傷のぶんだけ己が癒える。単騎で粘る個体に。"),
        // 🔧 スキルを持たない「素直に強い」枠。⚠ これが無いと装飾品が全部トリッキーになり、
        //    「とりあえず硬くしたい」に応える選択肢が消える。
        A("stone_charm", "石守りの護符", MinionSkillKind.None,        1.30f, 1.00f, 0.95f, 800,  0, "#7a6a4a",
          "ただ硬くなる。小細工の要らない場面のために。"),
        A("keen_charm",  "鋭牙の護符",   MinionSkillKind.None,        0.95f, 1.25f, 1.00f, 800,  0, "#e05a5a",
          "ただ鋭くなる。一撃で仕留めたい個体に。"),
    };

    private static Def A(string id, string jp, MinionSkillKind grant, float hp, float atk, float spd,
        int price, int rarity, string col, string desc)
        => new Def
        {
            id = id, jpName = jp, grant = grant, hpMult = hp, atkMult = atk, spdMult = spd,
            price = price, rarity = rarity, colorHex = col, desc = desc,
        };

    public static int Count => defs.Length;
    public static Def Get(int i) => defs[Mathf.Clamp(i, 0, defs.Length - 1)];
    public static string Name(int i) => i < 0 ? "なし" : Get(i).jpName;
    public static string ColorHex(int i) => i < 0 ? "#6f6889" : Get(i).colorHex;
    public static int IndexOf(string id)
    {
        for (int i = 0; i < defs.Length; i++) if (defs[i].id == id) return i;
        return -1;
    }

    /// <summary>希少度の名前（値段と出やすさの目安）。</summary>
    public static string RarityName(int r) => r >= 2 ? "稀少" : r == 1 ? "上物" : "並";

    /// <summary>効果の1行（UIとツールチップで同じ文を使う）。</summary>
    public static string EffectLine(int i)
    {
        if (i < 0) return "";
        var d = Get(i);
        var sb = new System.Text.StringBuilder();
        if (d.grant != MinionSkillKind.None) sb.Append("<b>" + MinionSkill.Name(d.grant) + "</b>");
        if (d.hpMult != 1f) { if (sb.Length > 0) sb.Append("／"); sb.Append("HP " + Pct(d.hpMult)); }
        if (d.atkMult != 1f) { if (sb.Length > 0) sb.Append("／"); sb.Append("攻撃 " + Pct(d.atkMult)); }
        if (d.spdMult != 1f) { if (sb.Length > 0) sb.Append("／"); sb.Append("速度 " + Pct(d.spdMult)); }
        return sb.ToString();
    }
    private static string Pct(float m)
    {
        int p = Mathf.RoundToInt((m - 1f) * 100f);
        return (p >= 0 ? "+" : "") + p + "%";
    }
}
