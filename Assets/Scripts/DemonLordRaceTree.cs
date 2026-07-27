using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 魔王の種族ツリー（原作『ダンジョンバトルロワイヤル』の種族進化を3段階に拡張）。
///
/// 人種(基本) → 第1進化(鬼/魔族/エルフ/ドワーフ/スライム/獣) → 第2進化(羅刹/龍/堕天/吸血/妖精/ハイエルフ/巨人/変幻/獣王)
/// - 進化条件は原作準拠：鬼=肉体, 魔族=魔力, エルフ=知識, ドワーフ=錬成 のステータス＋レベル。
///   スライム/獣は「その系統の配下を多用したか」を条件に加える（原作の“多用で進化”）。
/// - 各種族に **魔法属性**(MagicCatalog) と **魔王スキル**(MinionSkillKindを流用) を与えて特色を出す。
/// - 配下コスト補正：同系統の眷属は安く、非同系は高い（原作の「同種は安い/非同種は倍」）。
/// 関連: [[DemonLord]] [[MagicCatalog]] [[MinionSkill]] [[novel-canon]]。
/// </summary>
public static class DemonLordRaceTree
{
    // ※ DemonLord.Race の並びと1対1で対応させること
    public struct RaceDef
    {
        public string jpName;
        public int stage;                 // 0=基本 1=第1進化 2=第2進化
        public DemonLord.Race parent;     // 進化元
        public float hpMult, atkMult;
        public MagicElement element;      // 得意属性（魔王の攻撃に乗る）
        public MinionSkillKind skill;     // 魔王が持つスキル
        public ZombieAI.Species affinity; // 親和する眷属ファミリー
        public float allyCostMult;        // 同系統の配下コスト倍率
        public float otherCostMult;       // 非同系の配下コスト倍率
        public int reqStat;               // 必要ステ(-1=不問) DemonLord.Stat
        public int reqRank;               // 必要ランク(0=E..5=S)
        public int reqLevel;
        public string note;
    }

    private static RaceDef R(string jp, int stage, DemonLord.Race parent, float hp, float atk,
        MagicElement el, MinionSkillKind sk, ZombieAI.Species aff, float allyC, float otherC,
        int reqStat, int reqRank, int reqLv, string note)
    {
        var d = new RaceDef();
        d.jpName = jp; d.stage = stage; d.parent = parent; d.hpMult = hp; d.atkMult = atk;
        d.element = el; d.skill = sk; d.affinity = aff; d.allyCostMult = allyC; d.otherCostMult = otherC;
        d.reqStat = reqStat; d.reqRank = reqRank; d.reqLevel = reqLv; d.note = note;
        return d;
    }

    // DemonLord.Race の順に定義（Human, Oni, Demon, Elf, Dwarf, Slime, Vampire, Beast,
    //   Rakshasa, Dragon, Fallen, Fairy, HighElf, Giant, Mimic, BeastKing）
    private static readonly RaceDef[] defs =
    {
        // ── 基本 ──
        R("人種",       0, DemonLord.Race.Human, 1.00f, 1.00f, MagicElement.Light, MinionSkillKind.None,        ZombieAI.Species.Undead,   1.00f, 1.00f, -1, 0, 1,
          "万能だが特色なし。まずは肉体/魔力/知識/錬成のどれかを伸ばして進化先を決める。"),

        // ── 第1進化（原作のステータス条件）──
        R("鬼種",       1, DemonLord.Race.Human, 1.30f, 1.25f, MagicElement.Fire,    MinionSkillKind.Roar,       ZombieAI.Species.Demonkin, 0.85f, 1.15f, (int)DemonLord.Stat.Body,      2, 3,
          "肉体C以上。屈強な鬼。咆哮で味方を鼓舞し、火の魔法を操る。"),
        R("魔族種",     1, DemonLord.Race.Human, 1.10f, 1.40f, MagicElement.Dark,    MinionSkillKind.Lifedrain,  ZombieAI.Species.Demonkin, 0.80f, 1.20f, (int)DemonLord.Stat.Magic,     2, 3,
          "魔力C以上。闇の魔法と吸命を操る魔の王。魔族の配下が安い。"),
        R("エルフ種",   1, DemonLord.Race.Human, 1.20f, 1.15f, MagicElement.Thunder, MinionSkillKind.HealAura,   ZombieAI.Species.Undead,   0.90f, 1.10f, (int)DemonLord.Stat.Knowledge, 2, 3,
          "知識C以上。雷と治癒を操る。研究が進みやすい。"),
        R("ドワーフ種", 1, DemonLord.Race.Human, 1.15f, 1.20f, MagicElement.Earth,   MinionSkillKind.Thorns,     ZombieAI.Species.Demonkin, 0.85f, 1.15f, (int)DemonLord.Stat.Refine,    2, 3,
          "錬成C以上。鍛冶の王。装備の鍛造が安く、棘の鎧を纏う。"),
        R("スライム種", 1, DemonLord.Race.Human, 1.60f, 0.95f, MagicElement.Ice,     MinionSkillKind.Regen,      ZombieAI.Species.Beast,    0.95f, 1.05f, -1, 0, 3,
          "Lv3以上。粘体の王。異常に硬く、常に再生する。"),
        R("獣種",       1, DemonLord.Race.Human, 1.25f, 1.30f, MagicElement.Thunder, MinionSkillKind.Swift,      ZombieAI.Species.Beast,    0.80f, 1.20f, -1, 0, 4,
          "Lv4以上＋獣の配下を多用。俊敏で獣の配下が安い。"),

        // ── 第2進化（各系統の上位種）──
        R("羅刹種",     2, DemonLord.Race.Oni,     1.65f, 1.60f, MagicElement.Fire,    MinionSkillKind.Undying,    ZombieAI.Species.Demonkin, 0.75f, 1.25f, (int)DemonLord.Stat.Body,      4, 8,
          "肉体A以上。鬼の頂。不屈で致死の一撃にも耐える。"),
        R("龍種",       2, DemonLord.Race.Oni,     1.90f, 1.85f, MagicElement.Fire,    MinionSkillKind.Roar,       ZombieAI.Species.Beast,    0.85f, 1.15f, (int)DemonLord.Stat.Body,      5, 15,
          "肉体S＋Lv15。伝説の龍。全てを焼く火炎と圧倒的な体躯。"),
        R("堕天種",     2, DemonLord.Race.Demon,   1.35f, 1.90f, MagicElement.Light,   MinionSkillKind.Intimidate, ZombieAI.Species.Demonkin, 0.75f, 1.25f, (int)DemonLord.Stat.Magic,     4, 8,
          "魔力A以上。堕ちた天使。聖光を歪めて操り、威圧で敵を挫く。"),
        R("吸血種",     2, DemonLord.Race.Demon,   1.45f, 1.70f, MagicElement.Dark,    MinionSkillKind.Lifedrain,  ZombieAI.Species.Undead,   0.70f, 1.30f, (int)DemonLord.Stat.Magic,     3, 6,
          "魔力B以上。夜の王。吸命が強力で不死の配下が安い。"),
        R("妖精種",     2, DemonLord.Race.Elf,     1.30f, 1.45f, MagicElement.Thunder, MinionSkillKind.Swift,      ZombieAI.Species.Beast,    0.85f, 1.15f, (int)DemonLord.Stat.Knowledge, 4, 8,
          "知識A以上。妖精の王。俊敏で雷を操り、研究がさらに進む。"),
        R("ハイエルフ", 2, DemonLord.Race.Elf,     1.40f, 1.55f, MagicElement.Light,   MinionSkillKind.HealAura,   ZombieAI.Species.Undead,   0.85f, 1.15f, (int)DemonLord.Stat.Knowledge, 5, 12,
          "知識S＋Lv12。叡智の頂。聖光と強力な治癒を操る。"),
        R("巨人種",     2, DemonLord.Race.Dwarf,   1.85f, 1.55f, MagicElement.Earth,   MinionSkillKind.Thorns,     ZombieAI.Species.Demonkin, 0.80f, 1.20f, (int)DemonLord.Stat.Refine,    4, 8,
          "錬成A以上。山の如き巨人。鍛造がさらに安く、反射も強力。"),
        R("変幻種",     2, DemonLord.Race.Slime,   2.10f, 1.25f, MagicElement.Ice,     MinionSkillKind.Undying,    ZombieAI.Species.Beast,    0.90f, 1.10f, -1, 0, 10,
          "Lv10以上。究極の粘体。膨大なHPと不屈で決して沈まない。"),
        R("獣王種",     2, DemonLord.Race.Beast,   1.60f, 1.75f, MagicElement.Thunder, MinionSkillKind.PackTactics,ZombieAI.Species.Beast,    0.70f, 1.30f, -1, 0, 12,
          "Lv12以上。獣の王。群れの力で味方が多いほど強くなる。"),
    };

    public static int Count => defs.Length;
    public static RaceDef Get(DemonLord.Race r) => defs[Mathf.Clamp((int)r, 0, defs.Length - 1)];
    public static string NameOf(DemonLord.Race r) => Get(r).jpName;
    public static int StageOf(DemonLord.Race r) => Get(r).stage;

    /// <summary>その種族から直接進化できる種族（分岐）。</summary>
    public static List<DemonLord.Race> ChildrenOf(DemonLord.Race r)
    {
        var list = new List<DemonLord.Race>();
        for (int i = 0; i < defs.Length; i++)
        {
            if (i == (int)r) continue;                    // 自分自身は除く（人種の親も人種のため）
            if (defs[i].parent == r) list.Add((DemonLord.Race)i);
        }
        return list;
    }

    /// <summary>進化条件を満たすか（ステータス/レベル/配下の使用実績）。</summary>
    public static bool MeetsRequirement(DemonLord.Race target, DemonLord dl, out string reason)
    {
        var d = Get(target);
        reason = "";
        if (dl == null) { reason = "魔王が居ません"; return false; }
        if (dl.Level < d.reqLevel) { reason = "Lv" + d.reqLevel + "以上が必要"; return false; }
        if (d.reqStat >= 0 && dl.GetStatRank(d.reqStat) < d.reqRank)
        {
            reason = DemonLord.StatNames[d.reqStat] + " " + "EDCBAS"[Mathf.Clamp(d.reqRank, 0, 5)] + "以上が必要";
            return false;
        }
        // 原作準拠：獣種は「獣の配下を多用」していること
        if (target == DemonLord.Race.Beast && MinionRoster.CountOfFamily(ZombieAI.Species.Beast) < 3)
        {
            reason = "獣の配下を3体以上召喚していること";
            return false;
        }
        if (target == DemonLord.Race.Slime && MinionRoster.All.Count < 2)
        {
            reason = "配下を2体以上召喚していること";
            return false;
        }
        return true;
    }

    /// <summary>進化条件の表示用テキスト。</summary>
    public static string RequirementText(DemonLord.Race target)
    {
        var d = Get(target);
        string s = "Lv" + d.reqLevel;
        if (d.reqStat >= 0) s += " ・ " + DemonLord.StatNames[d.reqStat] + "" + "EDCBAS"[Mathf.Clamp(d.reqRank, 0, 5)];
        if (target == DemonLord.Race.Beast) s += " ・ 獣の配下3体";
        if (target == DemonLord.Race.Slime) s += " ・ 配下2体";
        return s;
    }
}
