using UnityEngine;

/// <summary>
/// 装備グレード（素材ラダー：銅→鉄→鋼→銀→ミスリル→アダマンタイト→オリハルコン）。原作資料n4282fqの武具素材段階。
///
/// 共有システム：冒険者(PA2)と、将来の魔物個体の武器/防具スロット(PE)の両方で使う。
/// - 武器グレード → 攻撃倍率(atkMult)。防具グレード → 実効HP倍率(hpMult＝硬さ)。grade<0 = 素手/素肌(×1.0)。
/// - 冒険者は『世界の装備水準(LureEconomy.gearLevel)』＋ランクから等級が決まる＝逃がして装備を奪われるほど高グレードの勇者が来る(両刃)。
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

    // ================= ⚔️ 武器の『種別』（原作資料『武器図鑑』）=================
    // 素材(グレード)が"強さ"なら、種別は"戦い方"。攻撃間隔・射程・威力のバランスが変わる。
    public enum WeaponType { Sword, Axe, Spear, Bow, Staff, DualBlade, Hammer }

    public struct WeaponTypeDef
    {
        public string jpName;
        public float atkMult;      // 一撃の重さ
        public float intervalMult; // 攻撃間隔（小さいほど手数が多い）
        public float rangeBonus;   // 射程の加算（マス）
        public string icon;        // Resources/Icons のアイコン名
        public string note;
    }

    private static readonly WeaponTypeDef[] wtypes =
    {
        W("剣",   1.00f, 1.00f, 0.0f, "icon_sword",      "標準。癖が無く扱いやすい。"),
        W("斧",   1.35f, 1.30f, 0.0f, "icon_axe",        "一撃が重いが振りが遅い。"),
        W("槍",   1.10f, 1.05f, 0.6f, "icon_trap_spears","間合いが広く、離れて刺せる。"),
        W("弓",   0.85f, 0.95f, 2.2f, "icon_bow",        "遠距離から射る。一撃は軽い。"),
        W("杖",   1.15f, 1.15f, 1.4f, "icon_fire_hand",  "魔法の威力を高める術者向け。"),
        W("双剣", 0.70f, 0.62f, 0.0f, "icon_dual_sword", "手数で押す。手練れ向け。"),
        W("鎚",   1.50f, 1.45f, 0.0f, "icon_hammer",     "最も重い一撃。硬い敵に有効。"),
    };
    private static WeaponTypeDef W(string n, float a, float i, float r, string ic, string note)
    { var d = new WeaponTypeDef(); d.jpName = n; d.atkMult = a; d.intervalMult = i; d.rangeBonus = r; d.icon = ic; d.note = note; return d; }

    public static int WeaponTypeCount => wtypes.Length;
    public static WeaponTypeDef WType(int t) => wtypes[Mathf.Clamp(t, 0, wtypes.Length - 1)];
    public static WeaponTypeDef WType(WeaponType t) => WType((int)t);
    public static string WeaponTypeName(int t) => WType(t).jpName;
    public static string WeaponTypeIcon(int t) => WType(t).icon;

    // 役割に合う既定の武器種（召喚時の初期装備）
    public static WeaponType DefaultTypeForRole(MinionCatalog.Role role)
    {
        switch (role)
        {
            case MinionCatalog.Role.Tank: return WeaponType.Hammer;
            case MinionCatalog.Role.Ranged: return WeaponType.Bow;
            case MinionCatalog.Role.Buff: return WeaponType.Staff;
            case MinionCatalog.Role.Debuff: return WeaponType.Staff;
            default: return WeaponType.Sword; // Melee
        }
    }
    // 冒険者の職に合う武器種（表示＋実挙動）
    public static WeaponType TypeForJob(AdventurerAI.Job job)
    {
        switch (job)
        {
            case AdventurerAI.Job.Thief: return WeaponType.DualBlade;
            case AdventurerAI.Job.Cleric: return WeaponType.Hammer;
            case AdventurerAI.Job.Mage: return WeaponType.Staff;
            default: return WeaponType.Sword;
        }
    }

    // 🔨 そのグレードの武具を鍛造するDPコスト（グレードが高いほど高い）。魔物個体への装着に使う。
    public static int ForgeCost(int grade) => (Mathf.Clamp(grade, 0, grades.Length - 1) + 1) * 150; // 銅150 … オリハルコン1050

    // ランク(0..7)＋世界装備水準(gearLevel 0-100)から等級を選ぶ。逃がして装備水準が上がるほど高グレード。
    public static int GradeFromWorld(int rankIdx, float gearLevel, float variance = 1f)
    {
        // ⚖️ ランク・Lv・脅威度と掛け算になるため控えめに（旧: rank*0.55 + gear/22 で最大グレードに届きすぎた）
        float baseF = rankIdx * 0.45f + gearLevel / 35f; // rank0-7→0-3.15, gear0-100→0-2.9
        int g = Mathf.RoundToInt(baseF + Random.Range(-variance, variance * 0.6f));
        return Mathf.Clamp(g, 0, grades.Length - 1);
    }
}
