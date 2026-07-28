using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 研究ツリー（Civの第2の木／CDO2の研究）。感情ツリー(文化系)と対の技術系ツリー。
/// - 分野: 魔物研究/領域研究/錬成研究/魔王研究。ノードは前提(prereq)＋研究点(RP)で解禁。
/// - RPは知識ランクのレート＋Eureka(後続)で貯まる。解禁効果は各systemが ResearchState.IsResearched(id) を参照。
/// カタログ(不変データ)＝ResearchCatalog、実行時状態＝ResearchState。関連: [[internal-affairs-design]]。
/// </summary>
public enum ResearchField { Monster, Domain, Refine, DemonLord, Magic }

public struct ResearchNode
{
    public string id;
    public ResearchField field;
    public string jpName;
    public string desc;
    public int cost;            // 研究点(RP)
    public string[] prereq;     // 前提ノードID（全て解禁済みで研究可）
    public int row;             // UI表示順（分野内）
}

public static class ResearchCatalog
{
    private static readonly List<ResearchNode> _all = new List<ResearchNode>
    {
        // ── 魔物研究 ──（進化ゲート＝この回で実挙動化）
        N("m_evo1", ResearchField.Monster, "配下進化Ⅰ 開放", "1段階目の進化(基本形→進化形)を解禁。図鑑で進化が選べるように。", 3, 0),
        N("m_evo2", ResearchField.Monster, "配下進化Ⅱ 開放", "2段階目の進化(進化形→上位)を解禁。", 6, 1, "m_evo1"),
        N("m_evo3", ResearchField.Monster, "配下進化Ⅲ 開放", "3段階目の進化を解禁。", 10, 2, "m_evo2"),
        N("m_slot", ResearchField.Monster, "部隊枠 +1", "部隊編成の枠を1つ増やす。", 5, 3, "m_evo1"),
        N("m_skill2", ResearchField.Monster, "魔物スキル解禁", "配下の高位スキル(威圧/不屈/自爆/石化/治癒/咆哮)が使えるようになる。", 8, 4, "m_evo1"),

        // ── 領域研究 ──（4層以降の拡張／罠種類。効果配線は後続）
        N("d_floor4", ResearchField.Domain, "第4層拡張", "準備中に第4層を追加できるようになる(DP消費・削減不可)。", 5, 0),
        N("d_floor5", ResearchField.Domain, "第5層拡張", "第5層の追加を解禁。", 8, 1, "d_floor4"),
        N("d_trap_poison", ResearchField.Domain, "毒沼の罠", "踏むと毒状態(継続ダメージ)を付与する罠を解禁。", 4, 2),
        N("d_trap_fire", ResearchField.Domain, "炎の罠", "やけど状態を付与する罠を解禁。", 4, 3),
        N("d_trap_ice", ResearchField.Domain, "氷の罠", "一定時間動けなくする罠を解禁。", 6, 4, "d_trap_poison"),
        N("d_trap_shock", ResearchField.Domain, "電気の罠", "周期的に麻痺(微小停止)を付与する罠を解禁。", 6, 5, "d_trap_fire"),
        N("d_trap_bleed", ResearchField.Domain, "針の罠", "出血状態を付与する罠を解禁。", 5, 6),
        // 🗿 トーテムの系統（範囲バフの層を広げる）
        N("d_totem_curse", ResearchField.Domain, "呪詛の彫像", "冒険者を弱らせるトーテム3種(呪詛の像/泥濘の碑/恐慌の面)を解禁。", 6, 7),
        N("d_totem_blood", ResearchField.Domain, "血統の祭壇", "家系を特化させるトーテム3種(屍の祭壇/獣牙の柱/魔導の尖塔)を解禁。", 8, 8),
        N("d_totem_ritual", ResearchField.Domain, "儀式の連環", "連携トーテム4種(疾風の風車/業火の炉/血の香炉/生命の樹)を解禁。", 10, 9, "d_totem_blood"),
        // 🏺 遺物スロット（獲得した遺物を同時に使える数）
        N("d_relic2", ResearchField.Domain, "遺物の祭壇", "遺物スロットを2つに増やす。", 7, 10),
        N("d_relic3", ResearchField.Domain, "遺物の宝物庫", "遺物スロットを3つに増やす。", 12, 11, "d_relic2"),

        // ── 錬成研究 ──（誘導のbait-chest。効果配線は後続）
        N("r_baitchest", ResearchField.Refine, "宝箱の任意配置", "拾得装備を素材に錬成し、任意の場所へ宝箱を配置できるように。", 6, 0),
        N("r_baitquality", ResearchField.Refine, "お宝の質向上", "手動宝箱の集客/装備品質を強化。", 8, 1, "r_baitchest"),

        // ── 魔王研究(統治) ──（反撃/回復／特殊制限スロット。効果配線は後続）
        N("k_reprisal", ResearchField.DemonLord, "反撃強化", "魔王の反撃ダメージを強化。", 4, 0),
        N("k_regen", ResearchField.DemonLord, "自然回復", "魔王が毎ターン少しずつHPを回復。", 6, 1),
        N("k_slot1", ResearchField.DemonLord, "特殊制限スロットⅠ", "特殊制限(政策カード)の枠を1つ開放。", 8, 2, "k_regen"),
        N("k_slot2", ResearchField.DemonLord, "特殊制限スロットⅡ", "特殊制限の枠を2つ目まで開放。", 14, 3, "k_slot1"),
        N("k_slot3", ResearchField.DemonLord, "特殊制限スロットⅢ", "特殊制限の枠を最大3つまで開放。", 20, 4, "k_slot2"),
        N("k_emotion", ResearchField.DemonLord, "感情増幅", "冒険者から得る感情が+35%。感情ツリーの進みが速くなる（研究×文化の連携）。", 9, 5),

        // ── 🔮 魔法研究 ──（属性の解禁＋階級の底上げ。眷属の術者が実際に魔法を撃つようになる）
        N("g_elem_dark",    ResearchField.Magic, "呪詛の魔法", "闇属性を解禁。毒(呪い)を付与し、不死が得意とする。", 4, 0),
        N("g_elem_fire",    ResearchField.Magic, "火炎の魔法", "火属性を解禁。継続ダメージ(炎)を付与。獣に特効。", 4, 1),
        N("g_elem_ice",     ResearchField.Magic, "氷結の魔法", "氷属性を解禁。相手を凍結させる。", 6, 2, "g_elem_fire"),
        N("g_elem_thunder", ResearchField.Magic, "雷撃の魔法", "雷属性を解禁。麻痺を付与。獣に効きやすい。", 6, 3, "g_elem_fire"),
        N("g_elem_earth",   ResearchField.Magic, "地砕の魔法", "土属性を解禁。状態異常は無いが威力が高い。", 5, 4, "g_elem_dark"),
        N("g_elem_light",   ResearchField.Magic, "聖光の魔法", "光属性を解禁。魔族・不死にも通る万能属性。", 10, 5, "g_elem_earth"),
        N("g_rank1", ResearchField.Magic, "魔法階級Ⅰ(中級)", "眷属が中級魔法まで扱えるようになる(威力×1.45)。", 7, 6, "g_elem_dark"),
        N("g_rank2", ResearchField.Magic, "魔法階級Ⅱ(上級)", "上級魔法まで扱えるようになる(威力×2.0)。", 12, 7, "g_rank1"),
        N("g_rank3", ResearchField.Magic, "魔法階級Ⅲ(最上級)", "最上級魔法まで扱えるようになる(威力×2.8)。", 20, 8, "g_rank2"),

        // ── 錬成研究の追加（装備グレードの上限解放）──
        N("r_grade_mithril",  ResearchField.Refine, "ミスリル鍛造", "配下の武具をミスリル以上に鍛えられるようになる。", 9, 2, "r_baitchest"),
        N("r_grade_orichal",  ResearchField.Refine, "オリハルコン鍛造", "最高位(アダマンタイト/オリハルコン)の鍛造を解禁。", 16, 3, "r_grade_mithril"),
    };

    private static ResearchNode N(string id, ResearchField f, string jp, string desc, int cost, int row, params string[] prereq)
        => new ResearchNode { id = id, field = f, jpName = jp, desc = desc, cost = cost, row = row, prereq = prereq };

    public static IReadOnlyList<ResearchNode> All => _all;
    public static int Count => _all.Count;
    public static bool TryGet(string id, out ResearchNode node)
    {
        foreach (var n in _all) if (n.id == id) { node = n; return true; }
        node = default; return false;
    }
    public static List<ResearchNode> ByField(ResearchField f)
    {
        var list = new List<ResearchNode>();
        foreach (var n in _all) if (n.field == f) list.Add(n);
        return list;
    }
    public static string FieldName(ResearchField f)
    {
        switch (f) { case ResearchField.Monster: return "魔物研究"; case ResearchField.Domain: return "領域研究"; case ResearchField.Refine: return "錬成研究"; case ResearchField.Magic: return "魔法研究"; default: return "魔王研究"; }
    }
}

/// <summary>研究の実行時状態（研究点RP＋解禁集合）。静的保持（セッション内、ドメインリロードで初期化）。</summary>
public static class ResearchState
{
    private static int rp = 0;
    private static HashSet<string> researched;
    private const int BaseRPPerTurn = 1;   // 毎ターンの基礎研究点
    private const int RPPerKnowledge = 1;  // 知識ランク1あたりの追加研究点

    private static void EnsureInit() { if (researched == null) researched = new HashSet<string>(); }

    public static int RP { get { return rp; } }
    public static void Reset() { rp = 0; researched = new HashSet<string>(); }
    public static void AddRP(int amount) { rp = Mathf.Max(0, rp + amount); }
    public static bool TrySpendRP(int amount) { EnsureInit(); if (rp < amount) return false; rp -= amount; return true; }

    public static bool IsResearched(string id) { EnsureInit(); return researched.Contains(id); }
    public static int ResearchedCount { get { EnsureInit(); return researched.Count; } }

    // 毎ターン終了時：知識ランクのレートでRPを得る（DungeonTurnManagerから）＋Eurekaは後続で加算
    public static void OnTurnEnd(int knowledgeRank)
    {
        AddRP(BaseRPPerTurn + Mathf.Max(0, knowledgeRank) * RPPerKnowledge);
    }

    public static bool PrereqMet(ResearchNode n)
    {
        EnsureInit();
        if (n.prereq != null) foreach (var p in n.prereq) if (!researched.Contains(p)) return false;
        return true;
    }
    // 🧠 知識ランクで研究コストが下がる（魔王の知識ステが活きる）
    public static int EffectiveCost(ResearchNode n)
    {
        float m = DemonLord.Instance != null ? DemonLord.Instance.ResearchCostMult : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(n.cost * m));
    }
    public static bool CanResearch(string id)
    {
        EnsureInit();
        if (!ResearchCatalog.TryGet(id, out var n)) return false;
        if (researched.Contains(id)) return false;
        return PrereqMet(n) && rp >= EffectiveCost(n);
    }
    public static bool TryResearch(string id)
    {
        EnsureInit();
        if (!CanResearch(id)) return false;
        ResearchCatalog.TryGet(id, out var n);
        int cost = EffectiveCost(n);
        rp -= cost;
        researched.Add(id);
        Debug.Log($"🔬【研究完了】{n.jpName}（-{cost}RP）");
        return true;
    }
}
