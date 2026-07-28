using UnityEngine;

/// <summary>
/// 🗿 トーテム図鑑（CDO2の設置バフに相当）。範囲内に効果を撒く『面の層』。
/// 3層バフの構成: 遺物=全体パッシブ / トーテム=範囲 / 個体装備・Lv=点。
/// 罠と同じく静的カタログ方式。種類は Feature.trapKind に格納して階層退避/復元に乗せる。
/// 関連: [[dangeon-3-current-code]] DungeonFeatureManager(配置と効果適用) / Research(領域研究で解禁)。
/// </summary>
public static class TotemCatalog
{
    public enum Kind
    {
        Lure,          // 誘惑の灯：集客（従来のトーテム）
        Mace,          // 戦棍の柱：配下の攻撃
        Bedrock,       // 巌の碑：配下のHP
        Gale,          // 疾風の風車：配下の手数（攻撃間隔短縮）
        AltarUndead,   // 屍の祭壇：不死系だけ大幅強化
        FangBeast,     // 獣牙の柱：獣系だけ大幅強化
        SpireDemon,    // 魔導の尖塔：魔族系だけ大幅強化
        Curse,         // 呪詛の像：範囲内の冒険者の攻撃を下げる
        Mire,          // 泥濘の碑：範囲内の冒険者の移動を遅くする
        Panic,         // 恐慌の面：範囲内の冒険者の満足が早く貯まる＝早く帰る（泳がせ運用）
        Forge,         // 業火の炉：範囲内の罠のダメージ
        Censer,        // 血の香炉：範囲内で冒険者を倒すと感情が増える
        LifeTree,      // 生命の樹：範囲内の配下を継続回復
    }

    public struct Def
    {
        public Kind kind; public string jpName; public string desc; public string icon;
        public int dpCost; public int radius; public float value; public string research; public string colorHex;
        // 家系限定（AltarUndead/FangBeast/SpireDemon のみ使用）
        public bool familyOnly; public ZombieAI.Species family;
    }

    private static readonly Def[] defs =
    {
        D(Kind.Lure,        "誘惑の灯",   "周囲マスの集客+20。冒険者が寄ってくる。", "icon_fire_hand",   150, 3, 20f,   "",               "#e3a94a"),
        D(Kind.Mace,        "戦棍の柱",   "範囲内の配下の攻撃 +20%",                  "icon_hammer",      180, 4, 0.20f, "",               "#e05a5a"),
        D(Kind.Bedrock,     "巌の碑",     "範囲内の配下のHP +25%",                    "icon_shield",      180, 4, 0.25f, "",               "#8cb8e6"),
        D(Kind.Gale,        "疾風の風車", "範囲内の配下の攻撃間隔 -15%（手数が増える）", "icon_dual_sword", 260, 4, 0.15f, "d_totem_ritual", "#57c3ab"),
        D(Kind.AltarUndead, "屍の祭壇",   "範囲内の『不死』の配下のみ HP・攻撃 +40%", "icon_skull",       300, 4, 0.40f, "d_totem_blood",  "#73d68c"),
        D(Kind.FangBeast,   "獣牙の柱",   "範囲内の『獣』の配下のみ HP・攻撃 +40%",   "icon_axe",         300, 4, 0.40f, "d_totem_blood",  "#e08a3c"),
        D(Kind.SpireDemon,  "魔導の尖塔", "範囲内の『魔族』の配下のみ HP・攻撃 +40%", "icon_fireball",    300, 4, 0.40f, "d_totem_blood",  "#b478e6"),
        D(Kind.Curse,       "呪詛の像",   "範囲内の冒険者の攻撃 -20%",                "icon_skull",       280, 4, 0.20f, "d_totem_curse",  "#9c6ad6"),
        D(Kind.Mire,        "泥濘の碑",   "範囲内の冒険者の移動 -25%（罠に長く晒す）", "icon_trap_spikes", 280, 4, 0.25f, "d_totem_curse",  "#7a6a4a"),
        D(Kind.Panic,       "恐慌の面",   "範囲内の冒険者の満足 +60%（早く帰す＝泳がせ）", "icon_bow",     260, 4, 0.60f, "d_totem_curse",  "#d65f8a"),
        D(Kind.Forge,       "業火の炉",   "範囲内の罠のダメージ +50%",                "icon_fireball",    300, 4, 0.50f, "d_totem_ritual", "#e0703c"),
        D(Kind.Censer,      "血の香炉",   "範囲内で冒険者を倒すと感情 +50%",          "icon_trap_spears", 320, 4, 0.50f, "d_totem_ritual", "#c04a6a"),
        D(Kind.LifeTree,    "生命の樹",   "範囲内の配下を毎秒 最大HPの2% 回復",       "icon_crossbow",    340, 4, 0.02f, "d_totem_ritual", "#6ad68a"),
    };

    private static Def D(Kind k, string n, string d, string ic, int cost, int rad, float v, string res, string col)
    {
        var x = new Def { kind = k, jpName = n, desc = d, icon = ic, dpCost = cost, radius = rad, value = v, research = res, colorHex = col };
        if (k == Kind.AltarUndead) { x.familyOnly = true; x.family = ZombieAI.Species.Undead; }
        else if (k == Kind.FangBeast) { x.familyOnly = true; x.family = ZombieAI.Species.Beast; }
        else if (k == Kind.SpireDemon) { x.familyOnly = true; x.family = ZombieAI.Species.Demonkin; }
        return x;
    }

    public static int Count => defs.Length;
    public static Def Get(int i) => defs[Mathf.Clamp(i, 0, defs.Length - 1)];
    public static Def Get(Kind k) => defs[(int)k];
    public static bool IsUnlocked(int i)
    {
        var d = Get(i);
        return string.IsNullOrEmpty(d.research) || ResearchState.IsResearched(d.research);
    }
    public static string Name(int i) => Get(i).jpName;
}
