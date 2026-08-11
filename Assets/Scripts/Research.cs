using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 研究ツリー（Civの第2の木／CDO2の研究）。感情ツリー(文化系)と対の技術系ツリー。
/// - 分野: 魔物研究/領域研究/錬成研究/魔王研究。ノードは前提(prereq)＋研究点(RP)で解禁。
/// - RPは知識ランクのレート＋Eureka(後続)で貯まる。解禁効果は各systemが ResearchState.IsResearched(id) を参照。
/// カタログ(不変データ)＝ResearchCatalog、実行時状態＝ResearchState。関連: [[internal-affairs-design]]。
/// </summary>
public enum ResearchField { Monster, Domain, Refine, DemonLord, Magic, Surface, Art }

/// <summary>
/// 🔧 ノードが持つ「効果」。
///
/// **なぜ要るか**：ツリーを150ノードに広げるとき、1ノードずつ効果を手で配線していたら
/// 配線漏れが必ず出る（＝押せるのに何も起きないノードができる）。
/// **効果の種類と量をデータに持たせ、参照側は `ResearchState.Sum(kind)` を1回読む**形にすれば、
/// ノードを足すのはデータ1行で済み、配線は増えない。
/// ⚠ 「解禁」型（罠の種類・進化段階など、量ではなく可否）は従来どおり `IsResearched(id)` を見る。
/// </summary>
public enum ResEffect
{
    None,
    DefenderHp, DefenderAtk, DefenderSpeed,   // 配下の底上げ（倍率・加算値は「+割合」）
    TrapDamage, MagicPower, ExpGain,          // 罠／魔法／育ち
    DpYield, MaterialYield, RpYield, EmotionGain,   // 産出
    KinPower, SurfaceDefense, SurfaceYield,   // 地上
    ResistAll, LordPower,                     // 耐性・魔王
    // ⚠ 末尾に足すこと（`sums` は再構築されるが、読み手が index で見ている所を増やさないため）
    MutationSuppress,                         // 🧬 世界の変異の抑制（→ [[MutationSystem]]）
}

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

    // ── ここから拡張（G-3）──
    /// <summary>属する時代。その時代に入るまで研究できない（Civ VIIの「技術は時代ごと」）。</summary>
    public EraSystem.Era era;
    /// <summary>
    /// 🔒 深いノードの**解放条件**（RPと前提だけでは開かない）。Civ VII の Mastery に相当。
    /// `gateNeed &lt;= 0` なら条件なし。
    /// </summary>
    public EraSystem.Cond gate;
    public int gateNeed;
    /// <summary>ツリーの段（0が根）。UIの縦位置に使う。前提から自動計算せず明示するのは、合流ノードが揃うため。</summary>
    public int tier;
    public ResEffect effect;
    public float amount;        // 効果量（割合なら 0.10 = +10%）

    /// <summary>
    /// 🔒 排他グループ（Civ VII の排他イデオロギー）。**同じ名前のノードは1つしか取れない**。
    /// 1つ研究した瞬間、同じグループの他は永久に閉じる。空文字なら排他なし。
    /// ⚠ これがあると1周で見られる終盤が変わる＝周回する理由になる。
    /// </summary>
    public string exclusive;
    /// <summary>
    /// ♾️ 反復可能（Civ VII の未来研究）。何度でも研究でき、**取るたびに効果が乗り、コストが上がる**。
    /// ツリーを掘り切ったあとにRPの行き先が無くなるのを防ぐ。
    /// </summary>
    public bool repeatable;
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
        N("s_influence", ResearchField.Surface, "威名の術", "毎ターンの威名 +4。外交の手数が増える。", 8, 12, "s_district1"),
        N("s_trade", ResearchField.Surface, "交易の道", "交易路の上限 +2。拠点どうしを結ぶとDPと食料が入る。", 11, 13, "s_settle"),
        N("s_training", ResearchField.Surface, "練兵の地", "施設『訓練所』を解禁。占領した土地に配下を送り込んで育てられる。", 10, 15, "s_district3"),
        N("s_accord", ResearchField.Surface, "盟約", "独立勢力への働きかけの費用 -30%。", 14, 14, "s_influence"),
        N("s_specialist", ResearchField.Surface, "専門家の登用", "都市の施設タイルに**専門家**を置ける。その施設の隣接ボーナスが2倍になる（維持費 食料2＋不満1）。", 14, 11, "s_district2"),
        // 🏛️ S1：政体と政策スロット（→ [[PolicySystem]]）
        N("p_slot", ResearchField.Surface, "統治の刷新", "政策の**自由スロット +1**（色を問わずカードを差せる枠が増える）。", 12, 16, "s_govern"),
        N("p_edict", ResearchField.Surface, "布告の権", "**戦闘中でも政策を差し替えられる**ようになる（通常は準備フェーズのみ）。", 15, 17, "p_slot"),

        // ── 錬成研究の追加（装備グレードの上限解放）──
        N("r_grade_mithril",  ResearchField.Refine, "ミスリル鍛造", "配下の武具をミスリル以上に鍛えられるようになる。", 9, 2, "r_baitchest"),
        N("r_grade_orichal",  ResearchField.Refine, "オリハルコン鍛造", "最高位(アダマンタイト/オリハルコン)の鍛造を解禁。", 16, 3, "r_grade_mithril"),

        // ══════════════ G-3b：原作資料から起こした拡張ノード（145件） ══════════════
        // ───── Magic ─────
        R("g_elem_water", ResearchField.Magic, EraSystem.Era.Dawn, 1, "水流の魔法", "水属性を解禁。手数が多く、燃えている相手に強い。", 5, ResEffect.MagicPower, 0.05f, EraSystem.Cond.Kill, 0, "g_elem_fire"),
        R("g_elem_wind", ResearchField.Magic, EraSystem.Era.Dawn, 1, "疾風の魔法", "風属性を解禁。射程が伸び、相手を吹き飛ばす。", 5, ResEffect.MagicPower, 0.05f, EraSystem.Cond.Kill, 0, "g_elem_dark"),
        R("g_elem_void", ResearchField.Magic, EraSystem.Era.Growth, 2, "無の魔法", "無属性を解禁。属性耐性を無視して通る。", 11, ResEffect.MagicPower, 0.08f, EraSystem.Cond.Research, 20, "g_elem_water", "g_elem_wind"),
        R("g_der_shadow", ResearchField.Magic, EraSystem.Era.Growth, 3, "影の魔法", "闇＋風の派生。姿を薄れさせ、初撃を必中にする。", 12, ResEffect.MagicPower, 0.07f, EraSystem.Cond.Kill, 0, "g_elem_dark", "g_elem_wind"),
        R("g_der_blood", ResearchField.Magic, EraSystem.Era.Growth, 3, "血の魔法", "闇＋水の派生。与えたダメージの一部を吸収する。", 12, ResEffect.DefenderHp, 0.06f, EraSystem.Cond.Kill, 0, "g_elem_dark", "g_elem_water"),
        R("g_der_wood", ResearchField.Magic, EraSystem.Era.Growth, 3, "木の魔法", "土＋水の派生。蔓で足を止める。", 12, ResEffect.MagicPower, 0.07f, EraSystem.Cond.Kill, 0, "g_elem_earth", "g_elem_water"),
        R("g_der_holy", ResearchField.Magic, EraSystem.Era.End, 4, "神聖の魔法", "光＋無の派生。治癒と浄化。味方の回復量が上がる。", 18, ResEffect.DefenderHp, 0.08f, EraSystem.Cond.MagicKill, 60, "g_elem_light", "g_elem_void"),
        R("g_der_space", ResearchField.Magic, EraSystem.Era.End, 4, "空間の魔法", "無＋風の派生。三大魔法のひとつ。", 20, ResEffect.MagicPower, 0.1f, EraSystem.Cond.Research, 34, "g_elem_void", "g_elem_wind"),
        R("g_der_time", ResearchField.Magic, EraSystem.Era.End, 4, "時間の魔法", "無＋光の派生。三大魔法のひとつ。", 22, ResEffect.DefenderSpeed, 0.1f, EraSystem.Cond.Research, 38, "g_elem_void", "g_elem_light"),
        R("g_der_gravity", ResearchField.Magic, EraSystem.Era.End, 4, "重力の魔法", "土＋無の派生。三大魔法のひとつ。", 22, ResEffect.MagicPower, 0.12f, EraSystem.Cond.Research, 38, "g_elem_earth", "g_elem_void"),
        R("g_fus_steam", ResearchField.Magic, EraSystem.Era.Growth, 4, "蒸気", "火＋水の融合。範囲に持続する熱波。", 14, ResEffect.MagicPower, 0.08f, EraSystem.Cond.Kill, 0, "g_elem_fire", "g_elem_water"),
        R("g_fus_lava", ResearchField.Magic, EraSystem.Era.Growth, 4, "溶岩", "火＋土の融合。地面が焼け、通った者を灼く。", 14, ResEffect.TrapDamage, 0.1f, EraSystem.Cond.Kill, 0, "g_elem_fire", "g_elem_earth"),
        R("g_fus_storm", ResearchField.Magic, EraSystem.Era.Growth, 4, "火炎嵐", "火＋風の融合。広く薙ぎ払う。", 15, ResEffect.MagicPower, 0.09f, EraSystem.Cond.Kill, 0, "g_elem_fire", "g_elem_wind"),
        R("g_fus_mud", ResearchField.Magic, EraSystem.Era.Growth, 4, "泥濘", "水＋土の融合。足を取り、動きを鈍らせる。", 13, ResEffect.TrapDamage, 0.09f, EraSystem.Cond.Kill, 0, "g_elem_water", "g_elem_earth"),
        R("g_fus_hell8", ResearchField.Magic, EraSystem.Era.End, 5, "八熱地獄", "蒸気・溶岩・火炎嵐の極み。階層全体を灼く禁呪。", 34, ResEffect.MagicPower, 0.2f, EraSystem.Cond.MagicKill, 150, "g_fus_steam", "g_fus_lava", "g_fus_storm"),
        R("g_fus_cold8", ResearchField.Magic, EraSystem.Era.End, 5, "八寒地獄", "氷・血・泥濘の極み。すべてを凍てつかせる禁呪。", 34, ResEffect.MagicPower, 0.2f, EraSystem.Cond.MagicKill, 150, "g_elem_ice", "g_der_blood", "g_fus_mud"),
        R("g_space_tele", ResearchField.Magic, EraSystem.Era.End, 5, "転移", "術者が階層内を跳ぶ。囲まれても抜けられる。", 24, ResEffect.DefenderSpeed, 0.12f, EraSystem.Cond.Kill, 0, "g_der_space"),
        R("g_space_wall", ResearchField.Magic, EraSystem.Era.End, 5, "空間壁", "通路を塞ぐ壁を張る。冒険者の進路を折る。", 26, ResEffect.DefenderHp, 0.1f, EraSystem.Cond.Kill, 0, "g_der_space"),
        R("g_time_haste", ResearchField.Magic, EraSystem.Era.End, 5, "加速", "味方の手数が増える。", 26, ResEffect.DefenderSpeed, 0.15f, EraSystem.Cond.Kill, 0, "g_der_time"),
        R("g_time_stop", ResearchField.Magic, EraSystem.Era.End, 6, "停止", "短時間、相手を完全に止める。", 38, ResEffect.MagicPower, 0.18f, EraSystem.Cond.Research, 46, "g_time_haste"),
        R("g_grav_press", ResearchField.Magic, EraSystem.Era.End, 5, "重圧", "範囲の相手を押し潰し、移動を奪う。", 26, ResEffect.MagicPower, 0.12f, EraSystem.Cond.Kill, 0, "g_der_gravity"),
        R("g_grav_hole", ResearchField.Magic, EraSystem.Era.End, 6, "黒の特異点", "一点に引き寄せて圧壊させる。最上位の攻撃魔法。", 40, ResEffect.MagicPower, 0.22f, EraSystem.Cond.MagicKill, 200, "g_grav_press"),
        R("g_cast1", ResearchField.Magic, EraSystem.Era.Dawn, 1, "詠唱短縮", "魔法の発動が速くなる。", 6, ResEffect.MagicPower, 0.05f, EraSystem.Cond.Kill, 0, "g_elem_dark"),
        R("g_cast2", ResearchField.Magic, EraSystem.Era.Growth, 2, "詠唱破棄", "詠唱を切り上げて撃てる。発動がさらに速い。", 13, ResEffect.MagicPower, 0.08f, EraSystem.Cond.Kill, 0, "g_cast1"),
        R("g_cast3", ResearchField.Magic, EraSystem.Era.End, 3, "無詠唱", "詠唱そのものが要らなくなる。", 25, ResEffect.MagicPower, 0.12f, EraSystem.Cond.MagicKill, 90, "g_cast2"),
        R("g_mana1", ResearchField.Magic, EraSystem.Era.Dawn, 1, "魔力操作", "魔力の扱いが安定し、威力が上がる。", 6, ResEffect.MagicPower, 0.05f, EraSystem.Cond.Kill, 0, "g_elem_fire"),
        R("g_mana2", ResearchField.Magic, EraSystem.Era.Growth, 2, "魔力制御", "無駄が消え、続けて撃てる。", 13, ResEffect.MagicPower, 0.08f, EraSystem.Cond.Kill, 0, "g_mana1"),
        R("g_mana3", ResearchField.Magic, EraSystem.Era.End, 3, "魔力支配", "魔力そのものを従える。", 25, ResEffect.MagicPower, 0.14f, EraSystem.Cond.Research, 40, "g_mana2"),
        // ───── Monster ─────
        R("m_evo4", ResearchField.Monster, EraSystem.Era.End, 4, "配下進化Ⅳ 開放（王種）", "4段階目『<b>王種</b>』への進化を解禁。最上位Ⅲの6形態それぞれに頂点がある。", 26, ResEffect.None, 0f, EraSystem.Cond.Evolved, 10, "m_evo3"),
        R("m_evo5", ResearchField.Monster, EraSystem.Era.End, 5, "配下進化Ⅴ 開放（古代種）", "5段階目『<b>古代種</b>』への進化を解禁。最果ての形態で、ここより先は無い。", 40, ResEffect.None, 0f, EraSystem.Cond.MinionLevel, 45, "m_evo4"),
        R("m_rank_high", ResearchField.Monster, EraSystem.Era.Dawn, 1, "ハイの格", "配下すべての HP+6%／攻撃+6%。", 6, ResEffect.DefenderHp, 0.06f, EraSystem.Cond.Kill, 0, "m_evo1"),
        R("m_rank_greater", ResearchField.Monster, EraSystem.Era.Growth, 2, "グレーターの格", "配下すべての HP+8%／攻撃+8%。", 12, ResEffect.DefenderAtk, 0.08f, EraSystem.Cond.Kill, 0, "m_rank_high"),
        R("m_rank_arch", ResearchField.Monster, EraSystem.Era.End, 3, "アークの格", "配下すべての HP+10%。", 22, ResEffect.DefenderHp, 0.1f, EraSystem.Cond.MinionLevel, 30, "m_rank_greater"),
        R("m_rank_tyrant", ResearchField.Monster, EraSystem.Era.End, 4, "タイラントの格", "配下すべての 攻撃+14%。原作の最上位接頭語。", 32, ResEffect.DefenderAtk, 0.14f, EraSystem.Cond.MinionLevel, 40, "m_rank_arch"),
        R("m_crown_lord", ResearchField.Monster, EraSystem.Era.Growth, 2, "ロードの位", "ボスに任命した個体が さらに強くなる。", 11, ResEffect.DefenderAtk, 0.05f, EraSystem.Cond.Kill, 0, "m_evo2"),
        R("m_crown_king", ResearchField.Monster, EraSystem.Era.Growth, 3, "キングの位", "ボスの HP+12%。", 18, ResEffect.DefenderHp, 0.07f, EraSystem.Cond.Boss, 3, "m_crown_lord"),
        R("m_crown_queen", ResearchField.Monster, EraSystem.Era.End, 4, "クイーンの位", "ボスが周囲の配下を鼓舞する。", 26, ResEffect.DefenderAtk, 0.08f, EraSystem.Cond.Boss, 5, "m_crown_king"),
        R("m_crown_emperor", ResearchField.Monster, EraSystem.Era.End, 5, "エンペラーの位", "王権の極み。ボスの全能力が大きく伸びる。", 42, ResEffect.DefenderHp, 0.15f, EraSystem.Cond.Evolved, 14, "m_crown_queen"),
        R("m_fam_undead1", ResearchField.Monster, EraSystem.Era.Dawn, 1, "屍の理", "不死の配下 HP+10%。", 7, ResEffect.DefenderHp, 0.04f, EraSystem.Cond.Kill, 0, "m_evo1"),
        R("m_fam_undead2", ResearchField.Monster, EraSystem.Era.Growth, 2, "死霊術の深化", "不死がとどめを刺されたとき、より強い骸が起き上がる。", 15, ResEffect.DefenderHp, 0.05f, EraSystem.Cond.Kill, 0, "m_fam_undead1"),
        R("m_fam_beast1", ResearchField.Monster, EraSystem.Era.Dawn, 1, "獣の理", "獣の配下 速度+12%。", 7, ResEffect.DefenderSpeed, 0.06f, EraSystem.Cond.Kill, 0, "m_evo1"),
        R("m_fam_beast2", ResearchField.Monster, EraSystem.Era.Growth, 2, "狂乱の血", "獣が被弾するほど速くなる度合いが増す。", 15, ResEffect.DefenderSpeed, 0.08f, EraSystem.Cond.Kill, 0, "m_fam_beast1"),
        R("m_fam_demon1", ResearchField.Monster, EraSystem.Era.Dawn, 1, "魔族の理", "魔族の配下 攻撃+10%。", 7, ResEffect.DefenderAtk, 0.04f, EraSystem.Cond.Kill, 0, "m_evo1"),
        R("m_fam_demon2", ResearchField.Monster, EraSystem.Era.Growth, 2, "吸命の深化", "魔族の吸収量が増える。", 15, ResEffect.DefenderAtk, 0.06f, EraSystem.Cond.Kill, 0, "m_fam_demon1"),
        R("m_sk_awe", ResearchField.Monster, EraSystem.Era.Growth, 2, "威圧", "配下が冒険者を怯ませる。", 10, ResEffect.DefenderAtk, 0.03f, EraSystem.Cond.Kill, 0, "m_skill2"),
        R("m_sk_endure", ResearchField.Monster, EraSystem.Era.Growth, 2, "不屈", "致命傷を一度だけ耐える。", 14, ResEffect.DefenderHp, 0.05f, EraSystem.Cond.Kill, 0, "m_skill2"),
        R("m_sk_burst", ResearchField.Monster, EraSystem.Era.Growth, 3, "自爆", "倒れる瞬間に大きな爆発を残す。", 16, ResEffect.DefenderAtk, 0.04f, EraSystem.Cond.Kill, 0, "m_sk_endure"),
        R("m_sk_petrify", ResearchField.Monster, EraSystem.Era.End, 3, "石化", "一定確率で相手を石に変える。", 24, ResEffect.MagicPower, 0.06f, EraSystem.Cond.Kill, 150, "m_sk_awe"),
        R("m_sk_heal", ResearchField.Monster, EraSystem.Era.Growth, 3, "治癒", "味方を癒す配下が現れる。", 18, ResEffect.DefenderHp, 0.06f, EraSystem.Cond.Kill, 0, "m_sk_endure"),
        R("m_sk_roar", ResearchField.Monster, EraSystem.Era.End, 4, "咆哮", "範囲の冒険者の攻撃を鈍らせる。", 28, ResEffect.DefenderHp, 0.07f, EraSystem.Cond.Kill, 220, "m_sk_petrify"),
        R("m_slot2", ResearchField.Monster, EraSystem.Era.Growth, 2, "部隊枠 +2", "部隊編成の枠をさらに1つ増やす。", 16, ResEffect.None, 0f, EraSystem.Cond.Kill, 0, "m_slot"),
        R("m_train", ResearchField.Monster, EraSystem.Era.Growth, 2, "魔素の反芻", "配下の経験値取得 +20%。", 13, ResEffect.ExpGain, 0.2f, EraSystem.Cond.Kill, 0, "m_evo2"),
        R("m_train2", ResearchField.Monster, EraSystem.Era.End, 3, "魔素の奔流", "配下の経験値取得 さらに +30%。", 24, ResEffect.ExpGain, 0.3f, EraSystem.Cond.MinionLevel, 35, "m_train"),
        // ───── Art ─────
        R("a_body1", ResearchField.Art, EraSystem.Era.Dawn, 0, "体術", "配下の近接攻撃 +5%。すべての武術の入口。", 4, ResEffect.DefenderAtk, 0.05f, EraSystem.Cond.Kill, 0),
        R("a_body2", ResearchField.Art, EraSystem.Era.Dawn, 1, "拳闘術", "近接攻撃 +6%／手数が増える。", 8, ResEffect.DefenderAtk, 0.06f, EraSystem.Cond.Kill, 0, "a_body1"),
        R("a_body3", ResearchField.Art, EraSystem.Era.Growth, 2, "格闘術", "近接攻撃 +8%。", 14, ResEffect.DefenderAtk, 0.08f, EraSystem.Cond.Kill, 0, "a_body2"),
        R("a_blade1", ResearchField.Art, EraSystem.Era.Dawn, 1, "剣術", "剣を持つ配下の攻撃 +7%。", 7, ResEffect.DefenderAtk, 0.05f, EraSystem.Cond.Kill, 0, "a_body1"),
        R("a_blade2", ResearchField.Art, EraSystem.Era.Growth, 2, "双剣術", "手数が増える。", 13, ResEffect.DefenderSpeed, 0.06f, EraSystem.Cond.Kill, 0, "a_blade1"),
        R("a_blade3", ResearchField.Art, EraSystem.Era.End, 3, "二刀流", "攻撃 +12%。剣の極み。", 24, ResEffect.DefenderAtk, 0.12f, EraSystem.Cond.Kill, 180, "a_blade2"),
        R("a_bow1", ResearchField.Art, EraSystem.Era.Dawn, 1, "弓術", "遠距離の配下の攻撃 +7%。", 7, ResEffect.DefenderAtk, 0.05f, EraSystem.Cond.Kill, 0, "a_body1"),
        R("a_bow2", ResearchField.Art, EraSystem.Era.Growth, 2, "大弩術", "射程と威力が伸びる。", 13, ResEffect.DefenderAtk, 0.07f, EraSystem.Cond.Kill, 0, "a_bow1"),
        R("a_spear1", ResearchField.Art, EraSystem.Era.Dawn, 1, "槍術", "間合いが伸びる。", 7, ResEffect.DefenderAtk, 0.05f, EraSystem.Cond.Kill, 0, "a_body1"),
        R("a_spear2", ResearchField.Art, EraSystem.Era.Growth, 2, "薙刀術", "範囲を薙ぐ。", 13, ResEffect.DefenderAtk, 0.07f, EraSystem.Cond.Kill, 0, "a_spear1"),
        R("a_str1", ResearchField.Art, EraSystem.Era.Dawn, 1, "怪力", "配下の HP+5%／攻撃+5%。", 6, ResEffect.DefenderHp, 0.05f, EraSystem.Cond.Kill, 0, "a_body1"),
        R("a_str2", ResearchField.Art, EraSystem.Era.Growth, 2, "豪腕", "HP+7%／攻撃+7%。", 13, ResEffect.DefenderAtk, 0.07f, EraSystem.Cond.Kill, 0, "a_str1"),
        R("a_str3", ResearchField.Art, EraSystem.Era.End, 3, "金剛", "HP+12%。肉体強化の極み。", 24, ResEffect.DefenderHp, 0.12f, EraSystem.Cond.Kill, 160, "a_str2"),
        R("a_spd1", ResearchField.Art, EraSystem.Era.Dawn, 1, "疾駆", "配下の速度 +6%。", 6, ResEffect.DefenderSpeed, 0.06f, EraSystem.Cond.Kill, 0, "a_body1"),
        R("a_spd2", ResearchField.Art, EraSystem.Era.Growth, 2, "豪脚", "速度 +8%。", 13, ResEffect.DefenderSpeed, 0.08f, EraSystem.Cond.Kill, 0, "a_spd1"),
        R("a_spd3", ResearchField.Art, EraSystem.Era.End, 3, "韋駄天", "速度 +14%。", 24, ResEffect.DefenderSpeed, 0.14f, EraSystem.Cond.Kill, 160, "a_spd2"),
        R("a_fus_god", ResearchField.Art, EraSystem.Era.End, 4, "闘神術", "金剛＋韋駄天の合一。HP+15%／攻撃+15%。", 40, ResEffect.DefenderAtk, 0.15f, EraSystem.Cond.MinionLevel, 40, "a_str3", "a_spd3"),
        R("a_fus_move", ResearchField.Art, EraSystem.Era.Growth, 3, "立体機動", "豪脚＋拳闘術。配下が壁を蹴って回り込む。", 20, ResEffect.DefenderSpeed, 0.1f, EraSystem.Cond.Kill, 0, "a_spd2", "a_body2"),
        R("a_fus_assassin", ResearchField.Art, EraSystem.Era.End, 4, "暗殺術", "格闘術＋大弩術。背後からの一撃が大きく伸びる。", 32, ResEffect.DefenderAtk, 0.13f, EraSystem.Cond.Kill, 200, "a_body3", "a_bow2"),
        R("a_res_poison", ResearchField.Art, EraSystem.Era.Dawn, 1, "毒耐性", "配下が毒を受けにくくなる。", 6, ResEffect.ResistAll, 0.05f, EraSystem.Cond.Kill, 0, "a_body1"),
        R("a_res_poison2", ResearchField.Art, EraSystem.Era.Growth, 2, "毒無効", "毒を完全に防ぐ。", 14, ResEffect.ResistAll, 0.07f, EraSystem.Cond.Kill, 0, "a_res_poison"),
        R("a_res_para", ResearchField.Art, EraSystem.Era.Dawn, 1, "麻痺耐性", "麻痺を受けにくくなる。", 6, ResEffect.ResistAll, 0.05f, EraSystem.Cond.Kill, 0, "a_body1"),
        R("a_res_para2", ResearchField.Art, EraSystem.Era.Growth, 2, "麻痺無効", "麻痺を完全に防ぐ。", 14, ResEffect.ResistAll, 0.07f, EraSystem.Cond.Kill, 0, "a_res_para"),
        R("a_res_phys", ResearchField.Art, EraSystem.Era.Growth, 2, "物理耐性", "物理ダメージを軽減する。", 15, ResEffect.DefenderHp, 0.07f, EraSystem.Cond.Kill, 0, "a_str1"),
        R("a_res_phys2", ResearchField.Art, EraSystem.Era.End, 3, "物理無効", "物理ダメージを大きく軽減する。", 30, ResEffect.DefenderHp, 0.12f, EraSystem.Cond.Kill, 200, "a_res_phys"),
        R("a_res_magic", ResearchField.Art, EraSystem.Era.Growth, 2, "魔法耐性", "魔法ダメージを軽減する。", 15, ResEffect.ResistAll, 0.08f, EraSystem.Cond.Kill, 0, "a_str1"),
        R("a_res_magic2", ResearchField.Art, EraSystem.Era.End, 3, "魔法無効", "魔法ダメージを大きく軽減する。", 30, ResEffect.ResistAll, 0.14f, EraSystem.Cond.MagicKill, 80, "a_res_magic"),
        R("a_eye_petrify", ResearchField.Art, EraSystem.Era.End, 4, "石化の魔眼", "見た者を石に変える。", 34, ResEffect.MagicPower, 0.1f, EraSystem.Cond.Research, 44, "a_res_magic2"),
        R("a_eye_hypno", ResearchField.Art, EraSystem.Era.End, 4, "催眠の魔眼", "冒険者どうしを同士討ちさせる。", 34, ResEffect.MagicPower, 0.1f, EraSystem.Cond.Research, 44, "a_eye_petrify"),
        R("a_eye_death", ResearchField.Art, EraSystem.Era.End, 5, "死神の瞳", "一定確率で即死させる。魔眼の極み。", 48, ResEffect.DefenderAtk, 0.18f, EraSystem.Cond.Kill, 320, "a_eye_hypno"),

        // ══════════════ 👑 覇道（終焉の排他分岐）══════════════
        // Civ VII の「政治理論のあと1つ選び、他は永久ロック」を、原作の**大罪之刻印**で表す。
        // ⚠ 3本のうち1本しか通れない＝1周で見られる終盤が変わる。周回する理由はここに置く。
        R("h_mark", ResearchField.Art, EraSystem.Era.End, 6, "大罪之刻印", "魔王の魂に大罪を刻む。ここから先は<b>一つの道しか選べない</b>。", 40, ResEffect.LordPower, 0.1f, EraSystem.Cond.Danger, 4, "a_fus_god", "a_eye_death"),

        X("h_glut1", ResearchField.Art, EraSystem.Era.End, 7, "暴食の刻印", "喰らうほど強くなる道。配下の攻撃 +12%。他の刻印は永久に閉じる。", 44, ResEffect.DefenderAtk, 0.12f, EraSystem.Cond.Danger, 4, "hado", "h_mark"),
        R("h_glut2", ResearchField.Art, EraSystem.Era.End, 8, "貪り喰らう軍", "撃破のたびに配下が肥える。配下HP +15%。", 52, ResEffect.DefenderHp, 0.15f, EraSystem.Cond.Kill, 400, "h_glut1"),
        R("h_glut3", ResearchField.Art, EraSystem.Era.End, 9, "万魔の胃", "喰らったものを迷宮そのものが吸う。撃破の素材 +40%。", 64, ResEffect.MaterialYield, 0.4f, EraSystem.Cond.Danger, 5, "h_glut2"),

        X("h_greed1", ResearchField.Art, EraSystem.Era.End, 7, "強欲の刻印", "溜め込むほど強くなる道。DP産出 +15%。他の刻印は永久に閉じる。", 44, ResEffect.DpYield, 0.15f, EraSystem.Cond.Danger, 4, "hado", "h_mark"),
        R("h_greed2", ResearchField.Art, EraSystem.Era.End, 8, "蒐集の理", "遺物と装備の価値が増す。素材産出 +25%・地上産出 +15%。", 52, ResEffect.SurfaceYield, 0.15f, EraSystem.Cond.Materials, 400, "h_greed1"),
        R("h_greed3", ResearchField.Art, EraSystem.Era.End, 9, "黄金の檻", "富そのものが檻になる。DP産出 +35%。", 64, ResEffect.DpYield, 0.35f, EraSystem.Cond.Danger, 5, "h_greed2"),

        X("h_wrath1", ResearchField.Art, EraSystem.Era.End, 7, "憤怒の刻印", "怒りを撒く道。罠の威力 +25%。他の刻印は永久に閉じる。", 44, ResEffect.TrapDamage, 0.25f, EraSystem.Cond.Danger, 4, "hado", "h_mark"),
        R("h_wrath2", ResearchField.Art, EraSystem.Era.End, 8, "燃ゆる憎悪", "恐怖が感情に変わる。感情 +30%。", 52, ResEffect.EmotionGain, 0.3f, EraSystem.Cond.EmotionSpent, 300, "h_wrath1"),
        R("h_wrath3", ResearchField.Art, EraSystem.Era.End, 9, "終焉の咆哮", "迷宮が吼える。配下の速度 +20%・魔法威力 +20%。", 64, ResEffect.MagicPower, 0.2f, EraSystem.Cond.Danger, 5, "h_wrath2"),

        // ♾️ 未来研究（Civ VII の Future Tech）。ツリーを掘り切ってもRPの行き先が残る。
        F("h_future", ResearchField.Art, EraSystem.Era.End, 10, "果ての探究", "反復して研究できる。取るたびに配下HPが +4% ずつ積み上がり、コストが45%重くなる。", 70, ResEffect.DefenderHp, 0.04f, EraSystem.Cond.Research, 120, "h_mark"),
        // ───── Domain ─────
        R("d_floor6", ResearchField.Domain, EraSystem.Era.Growth, 2, "第6層拡張", "第6層の追加を解禁（『拡張』から足せるようになる）。", 14, ResEffect.None, 0f, EraSystem.Cond.Floors, 5, "d_floor5"),
        R("d_floor7", ResearchField.Domain, EraSystem.Era.End, 3, "第7層拡張", "第7層の追加を解禁。深いほど魔素が濃い（最大7層）。", 24, ResEffect.None, 0f, EraSystem.Cond.Floors, 6, "d_floor6"),
        R("d_danger2", ResearchField.Domain, EraSystem.Era.Dawn, 1, "危険度『二級』", "迷宮が二級に格上げされる。来る者は強くなるが、実入りも増える。", 8, ResEffect.DpYield, 0.1f, EraSystem.Cond.Kill, 0, "d_floor4"),
        R("d_danger15", ResearchField.Domain, EraSystem.Era.Growth, 2, "危険度『準一級』", "準一級。Sランクの出現が噂され始める。", 16, ResEffect.DpYield, 0.12f, EraSystem.Cond.Kill, 120, "d_danger2"),
        R("d_danger1", ResearchField.Domain, EraSystem.Era.End, 3, "危険度『一級』", "一級。S級冒険者以上しか入れない迷宮になる。", 28, ResEffect.DpYield, 0.15f, EraSystem.Cond.Kill, 240, "d_danger15"),
        R("d_danger0", ResearchField.Domain, EraSystem.Era.End, 4, "危険度『特級』", "特級＝進入禁止指定。世界が総力で潰しに来るが、報酬は桁が変わる。", 46, ResEffect.DpYield, 0.25f, EraSystem.Cond.Kill, 400, "d_danger1"),
        // 🧬 世界の変異への対抗（→ [[MutationSystem]]）。⚠ 効きは `量 ÷ (1+抑制)` なので**0にはならない**。
        R("d_adapt1", ResearchField.Domain, EraSystem.Era.Growth, 2, "順応", "世界の変異に迷宮が慣れる。<b>抑制 +40%</b>（変異の効きが 1/1.4 になる）。", 18, ResEffect.MutationSuppress, 0.40f, EraSystem.Cond.Kill, 60, "d_floor4"),
        R("d_adapt2", ResearchField.Domain, EraSystem.Era.End, 3, "異相の解剖", "変異そのものを研究する。<b>抑制 さらに +60%</b>。", 36, ResEffect.MutationSuppress, 0.60f, EraSystem.Cond.Danger, 3, "d_adapt1"),
        F("d_adapt3", ResearchField.Domain, EraSystem.Era.End, 4, "変異抑制", "反復して研究できる。取るたびに<b>抑制 +35%</b>、コストは45%重くなる。", 50, ResEffect.MutationSuppress, 0.35f, EraSystem.Cond.Danger, 4, "d_adapt2"),
        R("d_slot1", ResearchField.Domain, EraSystem.Era.Dawn, 1, "広間の設計", "配置枠 +2。", 7, ResEffect.None, 0f, EraSystem.Cond.Kill, 0, "d_floor4"),
        R("d_slot2", ResearchField.Domain, EraSystem.Era.Growth, 2, "大広間の設計", "配置枠 さらに +2。", 15, ResEffect.None, 0f, EraSystem.Cond.Floors, 4, "d_slot1"),
        R("d_trap_chain", ResearchField.Domain, EraSystem.Era.Growth, 3, "連鎖の仕掛け", "罠が隣の罠を誘発するようになる。", 18, ResEffect.TrapDamage, 0.15f, EraSystem.Cond.Kill, 0, "d_trap_pow2"),
        R("d_trap_pow4", ResearchField.Domain, EraSystem.Era.End, 4, "殲滅機構", "罠のダメージ +45%。", 34, ResEffect.TrapDamage, 0.45f, EraSystem.Cond.TrapKill, 140, "d_trap_pow3"),
        R("d_totem_range", ResearchField.Domain, EraSystem.Era.Growth, 2, "共鳴の彫像", "トーテムの効果範囲が1マス広がる。", 14, ResEffect.DefenderHp, 0.05f, EraSystem.Cond.Kill, 0, "d_totem_curse"),
        R("d_theme", ResearchField.Domain, EraSystem.Era.Growth, 2, "空間の深化", "空間タイプの効果が1.5倍になる。", 16, ResEffect.DefenderHp, 0.06f, EraSystem.Cond.Kill, 0, "d_slot1"),
        R("d_relic4", ResearchField.Domain, EraSystem.Era.End, 4, "遺物の霊廟", "遺物スロットを4つに増やす。", 30, ResEffect.None, 0f, EraSystem.Cond.Relics, 8, "d_relic3"),
        // ───── Refine ─────
        R("r_grade_epic", ResearchField.Refine, EraSystem.Era.Growth, 3, "叙事詩級の鍛造", "叙事詩《エピック》級の武具を鍛えられる。", 22, ResEffect.DefenderAtk, 0.05f, EraSystem.Cond.Kill, 0, "r_grade_mithril"),
        R("r_grade_legend", ResearchField.Refine, EraSystem.Era.End, 4, "伝説級の鍛造", "伝説《レジェンダリー》級。", 32, ResEffect.DefenderAtk, 0.06f, EraSystem.Cond.ForgeHigh, 3, "r_grade_epic"),
        R("r_grade_ultima", ResearchField.Refine, EraSystem.Era.End, 5, "究極級の鍛造", "究極《アルテマ》級。", 42, ResEffect.DefenderAtk, 0.07f, EraSystem.Cond.ForgeHigh, 6, "r_grade_legend"),
        R("r_grade_phantasm", ResearchField.Refine, EraSystem.Era.End, 6, "幻想級の鍛造", "幻想《ファンタズマ》級。", 52, ResEffect.DefenderHp, 0.08f, EraSystem.Cond.Materials, 400, "r_grade_ultima"),
        R("r_grade_world", ResearchField.Refine, EraSystem.Era.End, 7, "世界級の鍛造", "世界《ワールド》級。", 64, ResEffect.DefenderAtk, 0.1f, EraSystem.Cond.Materials, 600, "r_grade_phantasm"),
        R("r_grade_god", ResearchField.Refine, EraSystem.Era.End, 8, "神級の鍛造", "神級《ゴッド》。", 78, ResEffect.DefenderHp, 0.12f, EraSystem.Cond.Relics, 10, "r_grade_world"),
        R("r_grade_genesis", ResearchField.Refine, EraSystem.Era.End, 9, "創世級の鍛造", "創世《ジェネシス》。等級の頂。", 96, ResEffect.DefenderAtk, 0.16f, EraSystem.Cond.Research, 56, "r_grade_god"),
        R("r_recycle", ResearchField.Refine, EraSystem.Era.Dawn, 1, "分解", "不要な装備を素材に戻せる。素材の取得 +15%。", 7, ResEffect.MaterialYield, 0.15f, EraSystem.Cond.Kill, 0, "r_baitchest"),
        R("r_extract", ResearchField.Refine, EraSystem.Era.Growth, 2, "抽出", "素材から魔力を取り出す。研究点 +10%。", 14, ResEffect.RpYield, 0.1f, EraSystem.Cond.Kill, 0, "r_recycle"),
        R("r_alchemy", ResearchField.Refine, EraSystem.Era.Growth, 3, "錬金術", "素材の取得 +25%／DP +10%。", 22, ResEffect.MaterialYield, 0.25f, EraSystem.Cond.Kill, 0, "r_extract"),
        // ───── DemonLord ─────
        R("k_reprisal2", ResearchField.DemonLord, EraSystem.Era.Growth, 2, "反撃の極み", "魔王の反撃ダメージがさらに上がる。", 14, ResEffect.LordPower, 0.1f, EraSystem.Cond.Kill, 0, "k_reprisal"),
        R("k_regen2", ResearchField.DemonLord, EraSystem.Era.Growth, 2, "不滅の核", "魔王の毎ターン回復量が増える。", 16, ResEffect.LordPower, 0.08f, EraSystem.Cond.Kill, 0, "k_regen"),
        R("k_core", ResearchField.DemonLord, EraSystem.Era.End, 3, "真核の守り", "真核が破られるまでの猶予が延びる。", 28, ResEffect.LordPower, 0.15f, EraSystem.Cond.LordLevel, 25, "k_regen2"),
        R("k_sin_gluttony", ResearchField.DemonLord, EraSystem.Era.End, 3, "暴食の刻印", "倒した冒険者から得るDPが +25%。", 26, ResEffect.DpYield, 0.25f, EraSystem.Cond.Kill, 200, "k_core"),
        R("k_sin_greed", ResearchField.DemonLord, EraSystem.Era.End, 3, "強欲の刻印", "素材の取得 +30%。", 26, ResEffect.MaterialYield, 0.3f, EraSystem.Cond.Materials, 300, "k_core"),
        R("k_sin_envy", ResearchField.DemonLord, EraSystem.Era.End, 4, "嫉妬の刻印", "他の魔王の力が伸びにくくなる。", 30, ResEffect.LordPower, 0.1f, EraSystem.Cond.RivalsDead, 1, "k_sin_greed"),
        R("k_sin_sloth", ResearchField.DemonLord, EraSystem.Era.End, 4, "怠惰の刻印", "準備フェーズが1ターンぶん長くなる（研究点 +20%）。", 30, ResEffect.RpYield, 0.2f, EraSystem.Cond.Research, 40, "k_sin_gluttony"),
        R("k_sin_wrath", ResearchField.DemonLord, EraSystem.Era.End, 4, "憤怒の刻印", "配下の攻撃 +12%。", 32, ResEffect.DefenderAtk, 0.12f, EraSystem.Cond.Kill, 260, "k_sin_gluttony"),
        R("k_sin_pride", ResearchField.DemonLord, EraSystem.Era.End, 5, "傲慢の刻印", "魔王自身の全能力が大きく伸びる。", 44, ResEffect.LordPower, 0.25f, EraSystem.Cond.LordLevel, 40, "k_sin_wrath"),
        R("k_sin_lust", ResearchField.DemonLord, EraSystem.Era.End, 5, "色欲の刻印", "感情の獲得 +35%。", 44, ResEffect.EmotionGain, 0.35f, EraSystem.Cond.EmotionSpent, 24, "k_sin_pride"),
        // ───── Surface ─────
        R("s_town_prod", ResearchField.Surface, EraSystem.Era.Growth, 2, "生産の町", "町を『生産』に特化できる。素材の産出 +20%。", 13, ResEffect.MaterialYield, 0.2f, EraSystem.Cond.Kill, 0, "s_settle"),
        R("s_town_food", ResearchField.Surface, EraSystem.Era.Growth, 2, "農の町", "町を『農』に特化できる。人口の伸びが速くなる。", 13, ResEffect.SurfaceYield, 0.15f, EraSystem.Cond.Kill, 0, "s_settle"),
        R("s_town_resort", ResearchField.Surface, EraSystem.Era.Growth, 3, "保養の町", "町を『保養』に特化できる。不満が減り、威名が入る。", 18, ResEffect.SurfaceYield, 0.12f, EraSystem.Cond.Kill, 0, "s_town_food"),
        R("s_town_fort", ResearchField.Surface, EraSystem.Era.Growth, 3, "要塞の町", "町を『要塞』に特化できる。守り +120。", 18, ResEffect.SurfaceDefense, 0.25f, EraSystem.Cond.Kill, 0, "s_town_prod"),
        R("s_navy1", ResearchField.Surface, EraSystem.Era.Growth, 2, "造船", "海を渡る船を出せる。沿岸の産出 +10%。", 12, ResEffect.SurfaceYield, 0.1f, EraSystem.Cond.Kill, 0, "s_voyage"),
        R("s_navy2", ResearchField.Surface, EraSystem.Era.Growth, 3, "海戦術", "海上の戦力 +25%。", 19, ResEffect.KinPower, 0.1f, EraSystem.Cond.Kill, 0, "s_navy1"),
        R("s_navy3", ResearchField.Surface, EraSystem.Era.End, 4, "遠洋航海", "海を2マス越えられる。遠き地の遺産に手が届く。", 30, ResEffect.SurfaceYield, 0.15f, EraSystem.Cond.Owned, 70, "s_navy2"),
        R("s_cmd1", ResearchField.Surface, EraSystem.Era.Growth, 2, "指揮官", "眷属が『指揮官』として周囲の眷属を強化する。", 14, ResEffect.KinPower, 0.08f, EraSystem.Cond.Kill, 0, "s_logistics"),
        R("s_cmd2", ResearchField.Surface, EraSystem.Era.Growth, 3, "昇進の理", "指揮官の昇進が1段速くなる。", 20, ResEffect.KinPower, 0.1f, EraSystem.Cond.Kill, 0, "s_cmd1"),
        R("s_cmd3", ResearchField.Surface, EraSystem.Era.End, 4, "大将軍", "指揮下の眷属すべての戦力 +20%。", 34, ResEffect.KinPower, 0.2f, EraSystem.Cond.KinCount, 4, "s_cmd2"),
        R("s_road", ResearchField.Surface, EraSystem.Era.Dawn, 1, "街道", "眷属と斥候の移動力 +1。", 8, ResEffect.None, 0f, EraSystem.Cond.Kill, 0, "s_district1"),
        R("s_border", ResearchField.Surface, EraSystem.Era.Growth, 2, "国境の理", "支配領域が自動で1マス広がる。", 15, ResEffect.SurfaceYield, 0.08f, EraSystem.Cond.Kill, 0, "s_govern"),
        R("s_market", ResearchField.Surface, EraSystem.Era.Growth, 2, "市場", "領域のDP産出 +20%。", 14, ResEffect.DpYield, 0.2f, EraSystem.Cond.Kill, 0, "s_warehouse"),
        R("s_food", ResearchField.Surface, EraSystem.Era.Dawn, 1, "農法", "拠点の食料 +2。", 7, ResEffect.SurfaceYield, 0.08f, EraSystem.Cond.Kill, 0, "s_district1"),
        R("s_festival", ResearchField.Surface, EraSystem.Era.Growth, 2, "祝祭法", "祝祭が起きやすくなり、効果も伸びる。", 13, ResEffect.EmotionGain, 0.15f, EraSystem.Cond.Kill, 0, "s_district2"),
        R("s_spy", ResearchField.Surface, EraSystem.Era.Growth, 3, "諜報", "他の魔王の版図と軍が見えるようになる。", 17, ResEffect.None, 0f, EraSystem.Cond.Kill, 0, "s_influence"),
        R("s_wonder", ResearchField.Surface, EraSystem.Era.Growth, 3, "遺産の造営", "遺産を自分で建てられるようになる。", 22, ResEffect.SurfaceYield, 0.1f, EraSystem.Cond.Districts, 8, "s_charter"),
        R("s_influence2", ResearchField.Surface, EraSystem.Era.End, 4, "覇者の名", "毎ターンの威名 +10。", 26, ResEffect.None, 0f, EraSystem.Cond.Influence, 300, "s_influence"),
        R("s_trade2", ResearchField.Surface, EraSystem.Era.End, 4, "交易帝国", "交易路の上限 +3／交易のDP +30%。", 28, ResEffect.DpYield, 0.3f, EraSystem.Cond.Cities, 2, "s_trade"),
        R("s_govern2", ResearchField.Surface, EraSystem.Era.End, 4, "統治の極み", "全ての領域の統治力 +4。", 26, ResEffect.SurfaceYield, 0.1f, EraSystem.Cond.Settlements, 5, "s_govern"),
        R("s_settle2", ResearchField.Surface, EraSystem.Era.End, 4, "版図の理", "支配領域の産出 さらに +30%。", 32, ResEffect.SurfaceYield, 0.3f, EraSystem.Cond.Owned, 90, "s_settle"),
        R("s_charter2", ResearchField.Surface, EraSystem.Era.End, 5, "帝国法", "支配上限 +4／街区をもう1つ置ける。", 38, ResEffect.SurfaceYield, 0.15f, EraSystem.Cond.Cities, 3, "s_charter"),
    };

    private static ResearchNode N(string id, ResearchField f, string jp, string desc, int cost, int row, params string[] prereq)
        => new ResearchNode { id = id, field = f, jpName = jp, desc = desc, cost = cost, row = row, prereq = prereq, eureka = EurekaText(id) };

    /// <summary>
    /// 拡張版のノード定義（G-3）。時代・段・効果・解放条件まで1行で書く。
    /// `gateNeed` が 0 なら解放条件なし。`prereq` を2つ以上渡すと**合流ノード**になる。
    /// </summary>
    private static ResearchNode R(string id, ResearchField f, EraSystem.Era era, int tier, string jp, string desc,
        int cost, ResEffect eff, float amt, EraSystem.Cond gate, int gateNeed, params string[] prereq)
        => new ResearchNode
        {
            id = id, field = f, era = era, tier = tier, jpName = jp, desc = desc, cost = cost,
            row = tier * 100, prereq = prereq, eureka = EurekaText(id),
            effect = eff, amount = amt, gate = gate, gateNeed = gateNeed,
        };

    /// <summary>🔒 排他ノード（同じ `group` は1つしか取れない）。覇道の3分岐に使う。</summary>
    private static ResearchNode X(string id, ResearchField f, EraSystem.Era era, int tier, string jp, string desc,
        int cost, ResEffect eff, float amt, EraSystem.Cond gate, int gateNeed, string group, params string[] prereq)
    {
        var n = R(id, f, era, tier, jp, desc, cost, eff, amt, gate, gateNeed, prereq);
        n.exclusive = group;
        return n;
    }

    /// <summary>♾️ 反復可能ノード（未来研究）。</summary>
    private static ResearchNode F(string id, ResearchField f, EraSystem.Era era, int tier, string jp, string desc,
        int cost, ResEffect eff, float amt, EraSystem.Cond gate, int gateNeed, params string[] prereq)
    {
        var n = R(id, f, era, tier, jp, desc, cost, eff, amt, gate, gateNeed, prereq);
        n.repeatable = true;
        return n;
    }

    /// <summary>
    /// 既存57ノードに時代を割り当てる表。
    /// ⚠ ノード定義そのものを書き換えず**あとから塗る**のは、既存のidが各所から
    ///   `IsResearched("m_evo1")` の形で参照されているため（定義行を触ると差分が読みにくくなる）。
    /// ここに無いidは胎動のまま。
    /// </summary>
    private static readonly Dictionary<string, EraSystem.Era> LegacyEra = new Dictionary<string, EraSystem.Era>
    {
        { "m_evo2", EraSystem.Era.Growth }, { "m_skill2", EraSystem.Era.Growth }, { "m_evo3", EraSystem.Era.End },
        { "d_floor5", EraSystem.Era.Growth }, { "d_trap_ice", EraSystem.Era.Growth }, { "d_trap_shock", EraSystem.Era.Growth },
        { "d_trap_pow2", EraSystem.Era.Growth }, { "d_totem_curse", EraSystem.Era.Growth }, { "d_totem_blood", EraSystem.Era.Growth },
        { "d_relic2", EraSystem.Era.Growth }, { "d_trap_pow3", EraSystem.Era.End }, { "d_totem_ritual", EraSystem.Era.End },
        { "d_relic3", EraSystem.Era.End },
        { "r_baitquality", EraSystem.Era.Growth }, { "r_grade_mithril", EraSystem.Era.Growth }, { "r_grade_orichal", EraSystem.Era.End },
        { "k_slot1", EraSystem.Era.Growth }, { "k_emotion", EraSystem.Era.Growth }, { "k_slot2", EraSystem.Era.End }, { "k_slot3", EraSystem.Era.End },
        { "g_elem_ice", EraSystem.Era.Growth }, { "g_elem_thunder", EraSystem.Era.Growth }, { "g_elem_earth", EraSystem.Era.Growth },
        { "g_rank1", EraSystem.Era.Growth }, { "g_elem_light", EraSystem.Era.End }, { "g_rank2", EraSystem.Era.End }, { "g_rank3", EraSystem.Era.End },
        { "s_district3", EraSystem.Era.Growth }, { "s_logistics", EraSystem.Era.Growth }, { "s_settle", EraSystem.Era.Growth },
        { "s_voyage", EraSystem.Era.Growth }, { "s_charter", EraSystem.Era.Growth }, { "s_trade", EraSystem.Era.Growth },
        { "s_specialist", EraSystem.Era.Growth }, { "s_training", EraSystem.Era.Growth }, { "p_slot", EraSystem.Era.Growth },
        { "s_conquer", EraSystem.Era.End }, { "s_accord", EraSystem.Era.End }, { "p_edict", EraSystem.Era.End },
    };

    static ResearchCatalog()
    {
        for (int i = 0; i < _all.Count; i++)
        {
            var n = _all[i];
            EraSystem.Era e;
            if (LegacyEra.TryGetValue(n.id, out e)) { n.era = e; }
            if (n.tier == 0) n.tier = (n.prereq == null || n.prereq.Length == 0) ? 0 : 1;   // 旧ノードの段はざっくり
            // ⚠️ 深い段で**条件が空いているノード**には危険度の鍵を自動で掛ける。
            //    データ側に1行ずつ書くと必ず書き忘れが出る（tier5が5件、tier6以上が5件ある）。
            //    段から導けば、ノードを何件足しても穴が開かない。
            if (n.gateNeed <= 0 && n.tier >= 4)
            {
                n.gate = EraSystem.Cond.Danger;
                n.gateNeed = Mathf.Clamp(n.tier - 2, 2, DangerRank.Max);
            }
            _all[i] = n;
        }
    }

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
            case "s_influence": return "拠点を2つ持つ";
            case "s_trade": return "都市を1つ持つ";
            case "s_accord": return "独立勢力を1つ従える";
            case "s_training": return "配下を8体そろえる";
            case "s_conquer": return "他の魔王を1人排除する";
            case "p_slot": return "拠点で祝祭を1度起こす";
            case "p_edict": return "政策を3枚同時に差す";
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
        switch (f) { case ResearchField.Monster: return "魔物研究"; case ResearchField.Domain: return "領域研究"; case ResearchField.Refine: return "錬成研究"; case ResearchField.Magic: return "魔法研究"; case ResearchField.Surface: return "地上研究"; case ResearchField.Art: return "業の研究"; default: return "魔王研究"; }
    }
}

/// <summary>研究の実行時状態（研究点RP＋解禁集合）。静的保持（セッション内、ドメインリロードで初期化）。</summary>
public static class ResearchState
{
    private static int rp = 0;
    private static HashSet<string> researched;
    private static HashSet<string> mastered;   // 📚 習熟（第2段階）。researched の部分集合。
    private const int BaseRPPerTurn = 1;   // 毎ターンの基礎研究点
    private const int RPPerKnowledge = 1;  // 知識ランク1あたりの追加研究点

    private static void EnsureInit()
    {
        if (researched == null) researched = new HashSet<string>();
        if (mastered == null) mastered = new HashSet<string>();
    }

    public static int RP { get { return rp; } }
    public static void Reset()
    { rp = 0; researched = new HashSet<string>(); mastered = new HashSet<string>(); repeats = new Dictionary<string, int>(); sums = null; }
    public static void AddRP(int amount) { rp = Mathf.Max(0, rp + amount); }
    public static bool TrySpendRP(int amount) { EnsureInit(); if (rp < amount) return false; rp -= amount; return true; }

    public static bool IsResearched(string id) { EnsureInit(); return researched.Contains(id); }
    public static int ResearchedCount { get { EnsureInit(); return researched.Count; } }

    // 毎ターン終了時：知識ランクのレートでRPを得る（DungeonTurnManagerから）＋Eurekaは後続で加算
    public static void OnTurnEnd(int knowledgeRank)
    {
        // 🜏 習合『妖精種の理』で毎ターンのRPが増える
        AddRP(Mathf.RoundToInt((BaseRPPerTurn + Mathf.Max(0, knowledgeRank) * RPPerKnowledge) * SyncretismSystem.RpMult));
    }

    public static bool PrereqMet(ResearchNode n)
    {
        EnsureInit();
        if (n.prereq != null) foreach (var p in n.prereq) if (!researched.Contains(p)) return false;
        return true;
    }

    /// <summary>その時代に入っているか（Civ VII：技術は時代ごとで、先取りはできない）。</summary>
    public static bool EraMet(ResearchNode n) => (int)EraSystem.Current >= (int)n.era;

    /// <summary>🔒 解放条件を満たしているか。条件なしのノードは常に true。</summary>
    public static bool GateMet(ResearchNode n)
    {
        if (n.gateNeed <= 0) return true;
        int v = 0;
        try { v = EraSystem.CondValue(n.gate); } catch { v = 0; }
        return v >= n.gateNeed;
    }

    /// <summary>🔒 解放条件の進捗テキスト（UIに「あと何が要るか」を出すため）。</summary>
    public static string GateText(ResearchNode n) => CondText(n.gate, n.gateNeed);

    /// <summary>条件1件の進捗文。危険度だけは数字でなく等級名で出す（「危険度 2/3」では読めない）。</summary>
    public static string CondText(EraSystem.Cond c, int need)
    {
        if (need <= 0) return "";
        int v = 0;
        try { v = EraSystem.CondValue(c); } catch { v = 0; }
        if (c == EraSystem.Cond.Danger)
            return "危険度 " + DangerRank.NameOf(v) + "／要 " + DangerRank.NameOf(need);
        return EraSystem.CondName(c) + " " + v + "/" + need;
    }

    // ============ 📚 習熟（Mastery）============
    // Civ VII の Mastery と同じ役割。**後続ノードの前提には決してしない**。
    // 基礎ノード＝「何が出来るようになるか」、習熟＝「どれだけ効くか」。
    // 前提にしないから、プレイヤーは毎回「先へ急ぐ／深く掘る」を選び続けることになる。

    public static bool IsMastered(string id) { EnsureInit(); return mastered.Contains(id); }
    public static int MasteredCount { get { EnsureInit(); return mastered.Count; } }

    /// <summary>習熟のRPコスト。基礎と同額（＝「1つ深く」と「1つ先へ」が同じ値段で天秤に乗る）。</summary>
    public static int MasteryCost(ResearchNode n) => EffectiveCost(n);

    /// <summary>
    /// 深い段の習熟に要る危険度（0なら不問）。tier4→二級・tier5→準一級・tier6以上→一級。
    /// ⚠ データに条件を書き足すのではなく段から導く。ノードを増やすたびに書き忘れる余地を作らないため。
    /// </summary>
    public static int MasteryDangerNeed(ResearchNode n)
        => n.tier <= 3 ? 0 : Mathf.Min(DangerRank.Max, n.tier - 2);

    public static bool MasteryGateMet(ResearchNode n)
    {
        int need = MasteryDangerNeed(n);
        return need <= 0 || DangerRank.Level >= need;
    }

    /// <summary>
    /// 習熟で得られる効果。数値効果を持つノードは**同じ効果がもう一度**乗る（＝合計2倍）。
    /// 「解禁」型（効果なし）のノードは分野に応じた既定の伸びを与える。
    /// ⚠ ここを空にすると「押せるのに何も起きない習熟」ができる。必ず何かを返す。
    /// </summary>
    public static void MasteryEffectOf(ResearchNode n, out ResEffect e, out float amount)
    {
        if (n.effect != ResEffect.None && n.amount > 0f) { e = n.effect; amount = n.amount; return; }
        amount = 0.04f + Mathf.Clamp(n.tier, 0, 6) * 0.01f;
        switch (n.field)
        {
            case ResearchField.Monster:   e = ResEffect.DefenderHp; break;
            case ResearchField.Domain:    e = ResEffect.TrapDamage; break;
            case ResearchField.Refine:    e = ResEffect.MaterialYield; break;
            case ResearchField.DemonLord: e = ResEffect.LordPower; break;
            case ResearchField.Magic:     e = ResEffect.MagicPower; break;
            case ResearchField.Surface:   e = ResEffect.SurfaceYield; break;
            default:                      e = ResEffect.EmotionGain; break;
        }
    }

    /// <summary>習熟の効き目を人間の言葉で（UIのボタンに出す）。</summary>
    public static string MasteryLabel(ResearchNode n)
    {
        ResEffect e; float a; MasteryEffectOf(n, out e, out a);
        return EffectName(e) + " +" + Mathf.RoundToInt(a * 100f) + "%";
    }

    public static string EffectName(ResEffect e)
    {
        switch (e)
        {
            case ResEffect.DefenderHp: return "配下HP";
            case ResEffect.DefenderAtk: return "配下攻撃";
            case ResEffect.DefenderSpeed: return "配下速度";
            case ResEffect.TrapDamage: return "罠威力";
            case ResEffect.MagicPower: return "魔法威力";
            case ResEffect.ExpGain: return "獲得経験値";
            case ResEffect.DpYield: return "DP産出";
            case ResEffect.MaterialYield: return "素材産出";
            case ResEffect.RpYield: return "研究点";
            case ResEffect.EmotionGain: return "感情";
            case ResEffect.KinPower: return "眷属戦力";
            case ResEffect.SurfaceDefense: return "地上防衛";
            case ResEffect.SurfaceYield: return "地上産出";
            case ResEffect.ResistAll: return "耐性";
            case ResEffect.LordPower: return "魔王の格";
            default: return "効果";
        }
    }

    public static bool CanMaster(string id)
    {
        EnsureInit();
        if (!ResearchCatalog.TryGet(id, out var n)) return false;
        if (!researched.Contains(id) || mastered.Contains(id)) return false;
        return MasteryGateMet(n) && rp >= MasteryCost(n);
    }

    /// <summary>習熟できない理由（UIに出す。空なら可）。</summary>
    public static string MasteryBlockReason(ResearchNode n)
    {
        if (!MasteryGateMet(n)) return "危険度 " + DangerRank.NameOf(MasteryDangerNeed(n)) + " が要る";
        if (rp < MasteryCost(n)) return "RPが足りない";
        return "";
    }

    public static bool TryMaster(string id)
    {
        EnsureInit();
        if (!CanMaster(id)) return false;
        ResearchCatalog.TryGet(id, out var n);
        int cost = MasteryCost(n);
        rp -= cost;
        mastered.Add(id);
        sums = null;
        Debug.Log($"📚『習熟』{n.jpName}（-{cost}RP／{MasteryLabel(n)}）");
        NotifySystem.Push($"『<b>{n.jpName}</b>』に習熟した（{MasteryLabel(n)}）", NotifySystem.Kind.Gain);
        return true;
    }

    // ============ 🔧 効果の集約 ============
    // 参照側はここを1回読むだけでよい（ノードが増えても配線は増えない）。
    private static Dictionary<ResEffect, float> sums;
    private static void RebuildSums()
    {
        sums = new Dictionary<ResEffect, float>();
        EnsureInit();
        foreach (var id in researched)
        {
            ResearchNode n;
            if (!ResearchCatalog.TryGet(id, out n) || n.effect == ResEffect.None) continue;
            // ♾️ 反復ノードは取った回数ぶん積む（1回ぶんしか乗らないと「重ねる意味」が消える）
            float amt = n.repeatable ? n.amount * Mathf.Max(1, RepeatCount(id)) : n.amount;
            float cur; sums.TryGetValue(n.effect, out cur);
            sums[n.effect] = cur + amt;
        }
        // 📚 習熟ぶん。数値ノードは同量をもう一度、解禁ノードは分野の既定値を乗せる。
        foreach (var id in mastered)
        {
            ResearchNode n;
            if (!ResearchCatalog.TryGet(id, out n)) continue;
            ResEffect e; float a; MasteryEffectOf(n, out e, out a);
            if (e == ResEffect.None || a <= 0f) continue;
            float cur; sums.TryGetValue(e, out cur);
            sums[e] = cur + a;
        }
    }
    /// <summary>その種類の効果の合計（研究していなければ0）。割合系はそのまま `1f + Sum(...)` で使う。</summary>
    public static float Sum(ResEffect e)
    {
        if (sums == null) RebuildSums();
        float v; return sums.TryGetValue(e, out v) ? v : 0f;
    }
    /// <summary>`1 + 合計` の倍率として使う場合の糖衣。</summary>
    public static float Mult(ResEffect e) => 1f + Sum(e);
    // 🧠 知識ランクで研究コストが下がる（魔王の知識ステが活きる）
    public static int EffectiveCost(ResearchNode n)
    {
        float m = DemonLord.Instance != null ? DemonLord.Instance.ResearchCostMult : 1f;
        m *= AttributeSystem.ResearchCostMult;   // 🎖️ 属性『学統』
        if (EurekaTracker.Has(n.id)) m *= EurekaTracker.Discount;   // 💡 天啓＝40%引き
        m *= NarrativeSystem.ResearchCostMult;                       // 🕯️ 形見『教条の写本』
        return Mathf.Max(1, Mathf.RoundToInt(n.cost * m));
    }
    // ============ 🔒 排他（覇道）と ♾️ 反復（未来研究） ============
    /// <summary>同じ排他グループの**別のノード**を既に取っているか（＝このノードは永久に閉じた）。</summary>
    public static bool ExclusiveBlocked(ResearchNode n)
    {
        if (string.IsNullOrEmpty(n.exclusive)) return false;
        EnsureInit();
        foreach (var id in researched)
        {
            if (id == n.id) continue;
            ResearchNode o;
            if (ResearchCatalog.TryGet(id, out o) && o.exclusive == n.exclusive) return true;
        }
        return false;
    }

    /// <summary>その排他グループで既に選んだノードの名前（UIで「〇〇を選んだ」と出す）。空なら未選択。</summary>
    public static string ExclusiveChosenName(string group)
    {
        if (string.IsNullOrEmpty(group)) return "";
        EnsureInit();
        foreach (var id in researched)
        {
            ResearchNode o;
            if (ResearchCatalog.TryGet(id, out o) && o.exclusive == group) return o.jpName;
        }
        return "";
    }

    // ♾️ 反復可能ノードを何回取ったか（id → 回数）
    private static Dictionary<string, int> repeats;
    public static int RepeatCount(string id)
    {
        if (repeats == null) return 0;
        int v; return repeats.TryGetValue(id, out v) ? v : 0;
    }
    /// <summary>反復ノードのコスト。取るたびに重くなる（無限に安く回らないように）。</summary>
    public static int RepeatCost(ResearchNode n)
        => Mathf.Max(1, Mathf.RoundToInt(EffectiveCost(n) * (1f + RepeatCount(n.id) * 0.45f)));

    public static bool CanResearch(string id)
    {
        EnsureInit();
        if (!ResearchCatalog.TryGet(id, out var n)) return false;
        // ♾️ 反復ノードは「済み」にならない。コストだけが上がっていく。
        if (n.repeatable)
            return EraMet(n) && GateMet(n) && PrereqMet(n) && !ExclusiveBlocked(n) && rp >= RepeatCost(n);
        if (researched.Contains(id)) return false;
        if (ExclusiveBlocked(n)) return false;
        return EraMet(n) && GateMet(n) && PrereqMet(n) && rp >= EffectiveCost(n);
    }
    public static bool TryResearch(string id)
    {
        EnsureInit();
        if (!CanResearch(id)) return false;
        ResearchCatalog.TryGet(id, out var n);
        if (n.repeatable)
        {
            int rc = RepeatCost(n);
            rp -= rc;
            if (repeats == null) repeats = new Dictionary<string, int>();
            repeats[id] = RepeatCount(id) + 1;
            researched.Add(id);      // 前提として参照できるように「取った」印は残す
            sums = null;
            Debug.Log($"♾️『反復研究』{n.jpName} ×{repeats[id]}（-{rc}RP）");
            NotifySystem.Push($"『<b>{n.jpName}</b>』を重ねた（{repeats[id]}回目）", NotifySystem.Kind.Gain);
            return true;
        }
        int cost = EffectiveCost(n);
        rp -= cost;
        researched.Add(id);
        sums = null;                 // 🔧 効果の集約を作り直させる
        Debug.Log($"🔬『研究完了』{n.jpName}（-{cost}RP）");
        NotifySystem.Push($"研究『<b>{n.jpName}</b>』が完了", NotifySystem.Kind.Gain);
        if (!string.IsNullOrEmpty(n.exclusive))
        {
            Debug.Log($"🔒『道が決まった』{n.jpName} を選んだ。同じ分岐の他の道は永久に閉じた。");
            NotifySystem.Push($"<b>{n.jpName}</b> の道を選んだ ― 他の刻印は<b>永久に閉じた</b>", NotifySystem.Kind.Story);
        }
        return true;
    }
}
