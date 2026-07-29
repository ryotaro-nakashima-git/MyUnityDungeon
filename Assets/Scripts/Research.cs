using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 研究ツリー（Civの第2の木／CDO2の研究）。感情ツリー(文化系)と対の技術系ツリー。
/// - 分野: 魔物研究/領域研究/錬成研究/魔王研究。ノードは前提(prereq)＋研究点(RP)で解禁。
/// - RPは知識ランクのレート＋Eureka(後続)で貯まる。解禁効果は各systemが ResearchState.IsResearched(id) を参照。
/// カタログ(不変データ)＝ResearchCatalog、実行時状態＝ResearchState。関連: [[internal-affairs-design]]。
/// </summary>
public enum ResearchField { Monster, Domain, Refine, DemonLord, Magic, Surface }

public struct ResearchNode
{
    public string id;
    public ResearchField field;
    public string jpName;
    public string desc;
    public int cost;            // 研究点(RP)
    public string[] prereq;     // 前提ノードID（全て解禁済みで研究可）
    public int row;             // UI表示順（分野内）
    public string eureka;       // 💡 天啓の条件テキスト（達成でコスト40%引き）→ [[EurekaTracker]]
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
        // 🪤 罠の威力（罠に投資する道。固定ダメージが後半に腐らないよう最大HP比成分も伸びる）
        N("d_trap_pow1", ResearchField.Domain, "研ぎ澄まされた刃", "全ての罠のダメージ +35%。", 4, 7),
        N("d_trap_pow2", ResearchField.Domain, "精密な仕掛け", "全ての罠のダメージ さらに +35%。", 7, 8, "d_trap_pow1"),
        N("d_trap_pow3", ResearchField.Domain, "貫通機構", "罠のダメージ +40%。さらに『最大HP比』成分が1.8倍になり、高HPの冒険者にも刺さる。", 11, 9, "d_trap_pow2"),
        // 🗿 トーテムの系統（範囲バフの層を広げる）
        N("d_totem_curse", ResearchField.Domain, "呪詛の彫像", "冒険者を弱らせるトーテム3種(呪詛の像/泥濘の碑/恐慌の面)を解禁。", 6, 10),
        N("d_totem_blood", ResearchField.Domain, "血統の祭壇", "家系を特化させるトーテム3種(屍の祭壇/獣牙の柱/魔導の尖塔)を解禁。", 8, 11),
        N("d_totem_ritual", ResearchField.Domain, "儀式の連環", "連携トーテム4種(疾風の風車/業火の炉/血の香炉/生命の樹)を解禁。", 10, 12, "d_totem_blood"),
        // 🏺 遺物スロット（獲得した遺物を同時に使える数）
        N("d_relic2", ResearchField.Domain, "遺物の祭壇", "遺物スロットを2つに増やす。", 7, 13),
        N("d_relic3", ResearchField.Domain, "遺物の宝物庫", "遺物スロットを3つに増やす。", 12, 14, "d_relic2"),

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

        // ── 🗺️ 地上研究（Civの社会制度に相当。地上を耕すほど解禁が進む）──
        N("s_district1", ResearchField.Surface, "開拓の礎", "施設『交易所』『鉱錬所』を建てられるようになる。地上のヘクスに1つずつ建設できる。", 4, 0),
        N("s_district2", ResearchField.Surface, "祈りと探求", "施設『魔泉』（研究点）『祭壇』（感情）を解禁。", 8, 1, "s_district1"),
        N("s_district3", ResearchField.Surface, "軍事拠点", "施設『兵舎』を解禁。領域の防衛と駐留眷属の戦力が上がる。", 10, 2, "s_district1"),
        N("s_scout", ResearchField.Surface, "斥候", "2つ先の領域まで見えるようになる（未到達でも情報が入る）。", 5, 3),
        N("s_logistics", ResearchField.Surface, "兵站", "全ての眷属の統率(LP)+6。より多くの配下を率いられる。", 9, 4, "s_district1"),
        N("s_settle", ResearchField.Surface, "拠点化", "支配領域の産出 +25%。", 12, 5, "s_district2"),
        N("s_govern", ResearchField.Surface, "統治の理", "全ての領域の統治力+2。人口が増えても不穏になりにくい。", 7, 6, "s_district1"),
        N("s_voyage", ResearchField.Surface, "渡航術", "海を1マス越えた先へ進軍できるようになる。海の向こうの『遠き地』が視界に入る。", 11, 7, "s_scout"),
        N("s_conquer", ResearchField.Surface, "簒奪の作法", "他魔王領への侵攻で戦力+20%。真核の戦利品も増える。", 16, 8, "s_district3"),
        // 🏙️ C2：拠点と都市（Civ VIIの Settlement 系）
        N("s_charter", ResearchField.Surface, "都市法", "支配上限 +2／都市への昇格コスト -25%／**街区**（同じタイルに2つ目の施設）を解禁。", 13, 9, "s_settle"),
        N("s_warehouse", ResearchField.Surface, "倉庫術", "施設『倉庫』を解禁。都市の版図にある資源1つにつき 素材+1・食料+1。", 9, 10, "s_district1"),
        N("s_specialist", ResearchField.Surface, "専門家の登用", "都市の施設タイルに**専門家**を置ける。その施設の隣接ボーナスが2倍になる（維持費 食料2＋不満1）。", 14, 11, "s_district2"),

        // ── 錬成研究の追加（装備グレードの上限解放）──
        N("r_grade_mithril",  ResearchField.Refine, "ミスリル鍛造", "配下の武具をミスリル以上に鍛えられるようになる。", 9, 2, "r_baitchest"),
        N("r_grade_orichal",  ResearchField.Refine, "オリハルコン鍛造", "最高位(アダマンタイト/オリハルコン)の鍛造を解禁。", 16, 3, "r_grade_mithril"),
    };

    private static ResearchNode N(string id, ResearchField f, string jp, string desc, int cost, int row, params string[] prereq)
        => new ResearchNode { id = id, field = f, jpName = jp, desc = desc, cost = cost, row = row, prereq = prereq, eureka = EurekaText(id) };

    // 💡 天啓の条件文（実際の判定は EurekaTracker 側。表示と判定を同じidで引く）
    private static string EurekaText(string id)
    {
        switch (id)
        {
            case "m_evo1": return "配下を5体そろえる";
            case "m_evo2": return "個体をLv15まで育てる";
            case "m_evo3": return "個体をLv30まで育てる";
            case "m_slot": return "隊を4体以上で編成する";
            case "m_skill2": return "個体をLv20まで育てる";
            case "d_floor4": return "3層まで掘り下げる";
            case "d_floor5": return "4層まで掘り下げる";
            case "d_trap_poison": return "罠で5体倒す";
            case "d_trap_fire": return "罠で10体倒す";
            case "d_trap_ice": return "罠で20体倒す";
            case "d_trap_shock": return "罠で30体倒す";
            case "d_trap_bleed": return "罠で15体倒す";
            case "d_trap_pow1": return "罠で25体倒す";
            case "d_trap_pow2": return "罠で50体倒す";
            case "d_trap_pow3": return "罠で90体倒す";
            case "d_totem_curse": return "トーテムを2基置く";
            case "d_totem_blood": return "トーテムを4基置く";
            case "d_totem_ritual": return "トーテムを6基置く";
            case "d_relic2": return "遺物を4種そろえる";
            case "d_relic3": return "遺物を8種そろえる";
            case "r_baitchest": return "素材を20ためる";
            case "r_baitquality": return "武具を3回鍛造する";
            case "r_grade_mithril": return "武具を6回鍛造する";
            case "r_grade_orichal": return "ミスリル以上を2回鍛造する";
            case "k_reprisal": return "魔王がLv5になる";
            case "k_regen": return "魔王がLv8になる";
            case "k_slot1": return "知識をCランクにする";
            case "k_slot2": return "知識をAランクにする";
            case "k_slot3": return "知識をSランクにする";
            case "k_emotion": return "感情を累計40消費する";
            case "g_elem_dark": return "魔法で3体倒す";
            case "g_elem_fire": return "魔法で6体倒す";
            case "g_elem_ice": return "魔法で12体倒す";
            case "g_elem_thunder": return "魔法で12体倒す";
            case "g_elem_earth": return "魔法で20体倒す";
            case "g_elem_light": return "魔法で30体倒す";
            case "g_rank1": return "魔法で15体倒す";
            case "g_rank2": return "魔法で40体倒す";
            case "g_rank3": return "魔法で70体倒す";
            case "s_district1": return "領域を1つ支配する";
            case "s_district2": return "施設を1つ建てる";
            case "s_district3": return "領域を3つ支配する";
            case "s_logistics": return "眷属を1体つくる";
            case "s_settle": return "施設を3つ建てる";
            case "s_scout": return "領域を2つ支配する";
            case "s_govern": return "人口が3以上の領域を持つ";
            case "s_voyage": return "海に面した領域を2つ支配する";
            case "s_charter": return "拠点を3つ持つ";
            case "s_warehouse": return "資源タイルを3つ支配する";
            case "s_specialist": return "人口4以上の都市を持つ";
            case "s_conquer": return "他の魔王を1人排除する";
        }
        return "";
    }

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
        switch (f) { case ResearchField.Monster: return "魔物研究"; case ResearchField.Domain: return "領域研究"; case ResearchField.Refine: return "錬成研究"; case ResearchField.Magic: return "魔法研究"; case ResearchField.Surface: return "地上研究"; default: return "魔王研究"; }
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
        if (EurekaTracker.Has(n.id)) m *= EurekaTracker.Discount;   // 💡 天啓＝40%引き
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
        Debug.Log($"🔬『研究完了』{n.jpName}（-{cost}RP）");
        return true;
    }
}
