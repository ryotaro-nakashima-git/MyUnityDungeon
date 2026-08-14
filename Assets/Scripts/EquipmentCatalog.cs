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

    // ⚖️ **1段階ごとに約 +22%**。旧表は +10〜15%で、Lv+4%/Lv の 2〜3レベル分しかなく、
    //    数百DPを払う意味が体感できなかった（ユーザー指摘）。いまは **1段階 ≒ 5〜6レベル分**。
    //    そのぶんコストを引き上げ、ミスリル以上は素材も要る（下の ForgeCost/ForgeMaterial）。
    private static readonly Grade[] grades =
    {
        // ── 素材の段（0-6）：手に入る鉱石で決まる。冒険者もここまでは持ってくる。──
        G("銅",           0.85f, 0.88f, "#a9754a"),
        G("鉄",           1.00f, 1.00f, "#b8b8c0"),
        G("鋼",           1.22f, 1.18f, "#9aa3b0"),
        G("銀",           1.50f, 1.42f, "#d8dde6"),
        G("ミスリル",     1.85f, 1.72f, "#7fd3e6"),
        G("アダマンタイト", 2.30f, 2.10f, "#8b7fd6"),
        G("オリハルコン", 2.85f, 2.55f, "#ffd24a"),
        // ── 等級の段（7-13）：素材ではなく**錬成研究で到達する位**。⚔️ ここから先は魔王だけのもの。──
        // ⚠⚠ **ここは1段 +6% しか伸ばさない**（素材段は+22%）。理由は2つ：
        //   ① 旧仕様ではこの7つは研究ノードの「配下の攻撃+X%」という**無条件の全体倍率**だった
        //      （攻撃 計+44% / HP 計+20%）。それを**個体ごとに払う**形へ移しただけで、総量は変えない。
        //      段5(古代種)で計算すると 力(HPx攻) は旧比 ×1.05 ＝**ほぼ据え置き**。
        //   ② ここを素材段と同じ+22%にすると、直したばかりのカーブが終盤だけ再び跳ねる
        //      （→ [[curve-measurement-t100]]）。**等級は"強さの追加"ではなく"支払い方の変更"。**
        // ⚠ 索引は `Individual.weaponGrade/armorGrade` としてセーブに載る。**必ず末尾に足す。**
        G("叙事詩",       3.02f, 2.70f, "#7fe0a0"),
        G("伝説",         3.20f, 2.87f, "#8cd0ff"),
        G("究極",         3.39f, 3.04f, "#b48cff"),
        G("幻想",         3.60f, 3.22f, "#ff8cd0"),
        G("世界",         3.81f, 3.41f, "#ffb45a"),
        G("神",           4.04f, 3.62f, "#fff3c4"),
        G("創世",         4.29f, 3.84f, "#ff5a5a"),
    };
    private static Grade G(string jp, float a, float h, string c) => new Grade { jp = jp, atkMult = a, hpMult = h, colorHex = c };

    public static int Count => grades.Length;
    public static int MaxGrade => grades.Length - 1;
    /// <summary>🏅 冒険者が持ってくる最高等級＝オリハルコン。等級段(7-13)は世界に流通していない。</summary>
    public const int HeroMaxGrade = 6;
    /// <summary>素材の段(0-6)と等級の段(7-13)の境目。UIの見出しに使う。</summary>
    public const int RankGradeStart = 7;
    public static Grade Get(int g) => grades[Mathf.Clamp(g, 0, grades.Length - 1)];
    public static string Name(int g) => g < 0 ? "なし" : Get(g).jp;
    public static string ColorHex(int g) => g < 0 ? "#6f6889" : Get(g).colorHex;

    public static float WeaponAtkMult(int g) => g < 0 ? 1f : Get(g).atkMult; // g<0=素手
    public static float ArmorHpMult(int g) => g < 0 ? 1f : Get(g).hpMult;    // g<0=素肌

    /// <summary>
    /// 🔬 錬成研究で到達できる等級の上限。**ここ1箇所に集約する**
    /// （旧は `MinionRoster.TryForge` と `DemonLord.ForgeGradeCap` に同じ式が2つあり、
    ///  等級を足すたびに片方だけ直す事故が待っていた）。
    /// 既定=銀(3)／ミスリル鍛造=4／オリハルコン鍛造=6／そこから叙事詩〜創世で1段ずつ。
    /// </summary>
    private static readonly string[] gradeResearch =
    { "r_grade_epic", "r_grade_legend", "r_grade_ultima", "r_grade_phantasm", "r_grade_world", "r_grade_god", "r_grade_genesis" };
    public static int ResearchGradeCap()
    {
        int cap = ResearchState.IsResearched("r_grade_orichal") ? 6
                : ResearchState.IsResearched("r_grade_mithril") ? 4 : 3;
        if (cap < 6) return cap;                                   // オリハルコンに届く前は等級段に触れない
        for (int i = 0; i < gradeResearch.Length; i++)
        {
            if (!ResearchState.IsResearched(gradeResearch[i])) break;
            cap = 7 + i;
        }
        return Mathf.Min(cap, MaxGrade);
    }
    /// <summary>次に必要な錬成研究の名前（UIの「これ以上は研究が要る」表示用）。</summary>
    public static string NextGradeResearchName(int cap)
    {
        if (cap < 4) return "ミスリル鍛造";
        if (cap < 6) return "オリハルコン鍛造";
        int i = cap - 6;
        if (i < 0 || i >= gradeResearch.Length) return "";
        return ResearchCatalog.TryGet(gradeResearch[i], out var n) ? n.jpName : "";
    }

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

    // 🔨 そのグレードの武具を鍛造するコスト。**強化幅を大きくしたぶん、値段も跳ねる**。
    // ⚠ 等級段(7-13)は伸びが+6%と小さいのに値段は跳ね上がる＝**全員には配れない**。
    //   これは意図的で、「誰に創世級を持たせるか」を選ばせるための沼にしてある。
    //   全個体に配れる値段にすると、ただの全体倍率に戻る（それが旧仕様だった）。
    private static readonly int[] forgeDP  = { 140, 300, 560, 950, 1600, 2600, 4000, 4800, 6000, 7200, 8500, 10000, 11500, 13000 };
    private static readonly int[] forgeMat = {   0,   0,   0,   2,    8,   18,   32,   42,   55,   70,   90,   115,   145,   180 };
    public static int ForgeCost(int grade) => forgeDP[Mathf.Clamp(grade, 0, forgeDP.Length - 1)];
    /// <summary>🪨 ミスリル以上は**素材**も要る（DPだけでは最上位に届かない）。</summary>
    public static int ForgeMaterial(int grade) => forgeMat[Mathf.Clamp(grade, 0, forgeMat.Length - 1)];

    /// <summary>1段上げると倍率がどれだけ動くか（UIに出す）。</summary>
    public static string StepText(int from, EquipmentCatalog.Slot slot)
    {
        int to = Mathf.Clamp(from + 1, 0, MaxGrade);
        float a = from < 0 ? 1f : (slot == Slot.Weapon ? Get(from).atkMult : Get(from).hpMult);
        float b = slot == Slot.Weapon ? Get(to).atkMult : Get(to).hpMult;
        return "×" + a.ToString("0.00") + " → ×" + b.ToString("0.00");
    }

    // ランク(0..7)＋世界装備水準(gearLevel 0-100)から等級を選ぶ。逃がして装備水準が上がるほど高グレード。
    public static int GradeFromWorld(int rankIdx, float gearLevel, float variance = 1f)
    {
        // ⚖️ ランク・Lv・脅威度と掛け算になるため控えめに（旧: rank*0.55 + gear/22 で最大グレードに届きすぎた）
        // ⚠ グレードの倍率を広げた（1段+22%）ので、**冒険者側は少し下げて釣り合いを取る**。
        //    ここを据え置くと、同じ世界装備水準でも敵だけが一気に硬く・重くなる。
        // ⚖️ さらに下げた（0.40/42 → 0.34/50）。装備は**冒険者にとって4本目の掛け算の軸**で、
        //    ランク×Lv×脅威度と積まれると終盤だけが跳ねる。序盤(rank0-1)はほぼ動かず、
        //    伸び切ったときの最大グレードだけが1段下がる＝**削るのは終盤の伸びだけ**。
        float baseF = rankIdx * 0.34f + gearLevel / 50f; // rank0-7→0-2.38, gear0-100→0-2.0
        int g = Mathf.RoundToInt(baseF + Random.Range(-variance, variance * 0.6f));
        // ⚠⚠ **冒険者はオリハルコン(6)止まり**。等級段(7-13)は錬成研究で到達する魔王だけのもので、
        //   世界に流通している素材ではない。`grades.Length-1` で締めると、等級を足すたびに
        //   相手の上限まで一緒に上がる＝直したカーブが黙って戻る（→ [[difficulty-curve-orders]]）。
        return Mathf.Clamp(g, 0, HeroMaxGrade);
    }
}
