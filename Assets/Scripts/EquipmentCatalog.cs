using UnityEngine;

/// <summary>
/// 装備グレード（素材ラダー：銅→鉄→鋼→銀→ミスリル→アダマンタイト→オリハルコン）。原作資料n4282fqの武具素材段階。
///
/// 共有システム：冒険者(PA2)と、将来の魔物個体の武器/防具スロット(PE)の両方で使う。
/// - 武器グレード → 攻撃倍率(atkMult)。防具グレード → 実効HP倍率(hpMult＝硬さ)。grade<0 = 素手/素肌(×1.0)。
/// - 冒険者は「世界の装備水準(LureEconomy.gearLevel)」＋ランクから等級が決まる＝逃がして装備を奪われるほど高グレードの勇者が来る(両刃)。
/// - 魔物個体は MinionRoster.Individual に weaponGrade/armorGrade を持たせ、スロットUI(PE)で装着する予定。
/// 関連: [[strength-variety-systems]] [[internal-affairs-design]] AdventurerAI / MinionRoster / DungeonFeatureManager。
/// </summary>
public static class EquipmentCatalog
{
    public enum Slot { Weapon, Armor }

    // 素材段階（7段）。索引がそのままグレード。
    public struct Grade
    {
        public string jp;        // 素材名
        public float atkMult;    // 武器としての攻撃倍率
        public float hpMult;     // 防具としての実効HP倍率
        public string colorHex;  // 表示色
    }

    private static readonly Grade[] grades =
    {
        G("銅",           0.90f, 0.95f, "#a9754a"),
        G("鉄",           1.00f, 1.00f, "#b8b8c0"),
        G("鋼",           1.12f, 1.10f, "#9aa3b0"),
        G("銀",           1.28f, 1.25f, "#d8dde6"),
        G("ミスリル",     1.50f, 1.45f, "#7fd3e6"),
        G("アダマンタイト", 1.75f, 1.70f, "#8b7fd6"),
        G("オリハルコン", 2.05f, 2.00f, "#ffd24a"),
    };
    private static Grade G(string jp, float a, float h, string c) => new Grade { jp = jp, atkMult = a, hpMult = h, colorHex = c };

    public static int Count => grades.Length;
    public static int MaxGrade => grades.Length - 1;
    public static Grade Get(int g) => grades[Mathf.Clamp(g, 0, grades.Length - 1)];
    public static string Name(int g) => g < 0 ? "なし" : Get(g).jp;
    public static string ColorHex(int g) => g < 0 ? "#6f6889" : Get(g).colorHex;

    public static float WeaponAtkMult(int g) => g < 0 ? 1f : Get(g).atkMult; // g<0=素手
    public static float ArmorHpMult(int g) => g < 0 ? 1f : Get(g).hpMult;    // g<0=素肌

    // 🔨 そのグレードの武具を鍛造するDPコスト（グレードが高いほど高い）。魔物個体への装着に使う。
    public static int ForgeCost(int grade) => (Mathf.Clamp(grade, 0, grades.Length - 1) + 1) * 150; // 銅150 … オリハルコン1050

    // ランク(0..7)＋世界装備水準(gearLevel 0-100)から等級を選ぶ。逃がして装備水準が上がるほど高グレード。
    public static int GradeFromWorld(int rankIdx, float gearLevel, float variance = 1f)
    {
        float baseF = rankIdx * 0.55f + gearLevel / 22f; // rank0-7→0-3.85, gear0-100→0-4.5
        int g = Mathf.RoundToInt(baseF + Random.Range(-variance, variance * 0.6f));
        return Mathf.Clamp(g, 0, grades.Length - 1);
    }
}
