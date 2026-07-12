using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 配下ロスター（原作『ダンジョンバトルロワイヤル』のCPティア × CDO2の役割編成 × 3ファミリー）。
///
/// 設計の位置づけ:
/// - これは「幅を広げる」ためのデータ土台。純staticなので既存シーン/コードに一切触れず単体でコンパイルできる。
/// - ファミリー(Family)は既存の ZombieAI.Species(不死/獣/魔族) にそのまま対応＝魔王の種族相性/リグ選択を流用。
/// - 役割(Role)は CDO2 の編成(Tank/Melee/Ranged/Buff/Debuff)。1部屋の役割コンプ・同役割上限に将来使う。
/// - ティア(TierCP)は原作の配下創造CP(スライム1/ラット2/ウルフ3/ゴブリン5/コボルト10/オーク20/ダークエルフ50…)を踏襲。
///   ＝配置コストと強さの序列。ビジュアルは当面 CharacterVisual の手続きリグ、後で SPUM/Dungeon Tale スプライトへ差替(SpumHint)。
/// - ランク(Rank G〜S)＝資料n4282fqの魔物ランク。表示/強さの目安（進化段階と概ね連動）。
///
/// 進化ツリー(PM 強さ拡張): 基本(depth0)→進化Ⅰ(1)→上位Ⅱ(2)→最上位Ⅲ(3)。分岐は MinionEvolution.EvoFrom が持ち、
///   各段階は研究 m_evo1/2/3 でゲート。ここは各形態の"データ"（強さ/役割/見た目当たり）だけを定義する。
/// 関連: [[MinionEvolution]] / ZombieAI.Species / CharacterVisual.RigType / DemonLord.AffinitySpecies。
/// </summary>
public static class MinionCatalog
{
    // 🎭 役割（CDO2編成）。1部屋の中で役割を散らすと人海戦術ボーナス、同役割の重複は制限する想定。
    public enum Role { Tank, Melee, Ranged, Buff, Debuff }

    // 🏅 ランク（資料n4282fq: 魔物ランクS〜G）。進化段階が上がるほど高ランク。表示と強さの目安。
    public enum Rank { G, F, E, D, C, B, A, S }

    // 🐺 ファミリー由来の"機械的個性"（原作/CDO2）。倍率だけでなく戦い方を変える将来フック。
    //   Undead(不死) = とどめを刺すと弱い骸を1体再生成／数と粘り
    //   Beast(獣)    = 被弾/攻撃のたびに加速（stack）／後半に伸びる
    //   Demonkin(魔族)= 与ダメの一部を吸収（lifesteal）／単体性能
    public enum FamilyTrait { UndeadRaise, BeastFrenzy, DemonLifesteal }

    // 配下1種の定義（純データ）。
    public struct MinionDef
    {
        public string id;            // 内部ID（英字）
        public string jpName;        // 表示名（日本語）
        public ZombieAI.Species family;
        public Role role;
        public Rank rank;            // 魔物ランク（G〜S）
        public int tierCP;           // 配置コストの基準（原作CPティア）
        public float hpMult;         // ファミリー基準に対する個体倍率
        public float atkMult;
        public float spdMult;
        public CharacterVisual.RigType rig;        // 当面の手続きビジュアル（ファミリーリグ）
        public CharacterVisual.AttackStyle style;  // 攻撃モーション
        public string spumHint;      // 後でSPUM/Dungeon Taleスプライトに差替える際の当たり（プレハブ種別）
        public string note;          // 役割/個性の短い説明（UIツールチップ用）
    }

    // ファミリーの機械的個性（当面はデータのみ。ZombieAI側で参照して実挙動化する）。
    public static FamilyTrait TraitOf(ZombieAI.Species family)
    {
        switch (family)
        {
            case ZombieAI.Species.Beast: return FamilyTrait.BeastFrenzy;
            case ZombieAI.Species.Demonkin: return FamilyTrait.DemonLifesteal;
            default: return FamilyTrait.UndeadRaise;
        }
    }

    // ================= ロスター本体 =================
    // 原作のティア序列を守りつつ、3ファミリー × 4段階(基本→進化Ⅰ→上位Ⅱ→最上位Ⅲ) × 役割 で"幅"を作る。
    // 進化の親子は MinionEvolution.EvoFrom が持つ（ここは定義のみ）。ビジュアルは手持ち在庫に寄せる。
    private static readonly List<MinionDef> _all = new List<MinionDef>
    {
        // ═══════════ 🦴 不死 Undead（数・粘り・とどめ再生成） ═══════════
        // -- 基本(depth0) --
        Def("skeleton",       "スケルトン",           ZombieAI.Species.Undead,   Role.Melee,  Rank.F,  3,  1.00f, 1.00f, 1.00f, CharacterVisual.AttackStyle.Claw,  "SPUM_Skelton",       "不死の標準兵。安価で数を並べる基本形。"),
        Def("zombie",         "ゾンビ",               ZombieAI.Species.Undead,   Role.Tank,   Rank.F,  4,  1.45f, 0.80f, 0.80f, CharacterVisual.AttackStyle.Claw,  "SPUM_Skelton",       "鈍いが硬い壁役。前線で敵を足止め。"),
        Def("ghost",          "ゴースト",             ZombieAI.Species.Undead,   Role.Debuff, Rank.E,  8,  0.70f, 0.85f, 1.20f, CharacterVisual.AttackStyle.Cast,  "DungeonTale_Ghost",  "冒険者を怯ませ足を鈍らせる妨害役。"),
        // -- 進化Ⅰ(depth1) --
        Def("skeleton_archer","スケルトンアーチャー", ZombieAI.Species.Undead,   Role.Ranged, Rank.E,  6,  0.80f, 1.10f, 1.00f, CharacterVisual.AttackStyle.Stab,  "SPUM_Skelton",       "遠距離から射る。柔らかいが手数。"),
        Def("skeleton_soldier","スケルトンソルジャー", ZombieAI.Species.Undead,  Role.Tank,   Rank.D,  7,  1.60f, 1.05f, 0.90f, CharacterVisual.AttackStyle.Swing, "SPUM_Skelton",       "盾を持つ骸兵。硬く前線を支える。"),
        Def("ghoul",          "グール",               ZombieAI.Species.Undead,   Role.Melee,  Rank.D,  9,  1.20f, 1.35f, 1.10f, CharacterVisual.AttackStyle.Claw,  "SPUM_Skelton",       "喰らって回復する狂乱の屍。"),
        Def("wraith",         "レイス",               ZombieAI.Species.Undead,   Role.Debuff, Rank.C, 13,  0.95f, 1.20f, 1.30f, CharacterVisual.AttackStyle.Cast,  "DungeonTale_Ghost",  "呪詛で冒険者を弱らせる上位の霊。"),
        // -- 上位Ⅱ(depth2) --
        Def("skeleton_knight","スケルトンナイト",     ZombieAI.Species.Undead,   Role.Tank,   Rank.C, 15,  2.10f, 1.30f, 0.90f, CharacterVisual.AttackStyle.Swing, "SPUM_Skelton",       "重装の不死騎士。鉄壁の要。"),
        Def("bone_sniper",    "ボーンスナイパー",     ZombieAI.Species.Undead,   Role.Ranged, Rank.C, 14,  0.95f, 1.60f, 1.05f, CharacterVisual.AttackStyle.Stab,  "SPUM_Skelton",       "急所を射抜く不死の狙撃手。"),
        Def("lich",           "リッチ",               ZombieAI.Species.Undead,   Role.Buff,   Rank.B, 20,  1.05f, 1.30f, 0.95f, CharacterVisual.AttackStyle.Cast,  "SPUM_Skelton",       "周囲の不死を強化・再生成を早める術者。"),
        // -- 最上位Ⅲ(depth3) --
        Def("death_knight",   "デスナイト",           ZombieAI.Species.Undead,   Role.Melee,  Rank.A, 28,  2.20f, 2.00f, 1.05f, CharacterVisual.AttackStyle.Swing, "SPUM_Skelton",       "不死の王の剣。圧倒的な攻守を誇る英雄種。"),
        Def("elder_lich",     "エルダーリッチ",       ZombieAI.Species.Undead,   Role.Buff,   Rank.S, 38,  1.40f, 1.90f, 1.00f, CharacterVisual.AttackStyle.Cast,  "SPUM_Skelton",       "死霊術の極致。軍勢を統べ蘇らせる大魔導。"),

        // ═══════════ 🐺 獣 Beast（速い・被弾で加速・後半型） ═══════════
        // -- 基本(depth0) --
        Def("rat",            "ラット",               ZombieAI.Species.Beast,    Role.Melee,  Rank.G,  1,  0.55f, 0.70f, 1.35f, CharacterVisual.AttackStyle.Claw,  "", "極安の群れ。数で押す最下級。"),
        Def("bat",            "バット",               ZombieAI.Species.Beast,    Role.Melee,  Rank.G,  2,  0.50f, 0.80f, 1.55f, CharacterVisual.AttackStyle.Claw,  "", "素早く飛び回り撹乱する。"),
        // -- 進化Ⅰ(depth1) --
        Def("wolf",           "ウルフ",               ZombieAI.Species.Beast,    Role.Melee,  Rank.F,  3,  0.90f, 1.20f, 1.40f, CharacterVisual.AttackStyle.Claw,  "", "俊足の狩人。加速して急所を刺す。"),
        Def("harpy",          "ハーピー",             ZombieAI.Species.Beast,    Role.Ranged, Rank.E,  8,  0.80f, 1.10f, 1.30f, CharacterVisual.AttackStyle.Stab,  "", "空から急襲する遠距離獣。"),
        // -- 上位Ⅱ(depth2) --
        Def("great_beast",    "大獣",                 ZombieAI.Species.Beast,    Role.Tank,   Rank.D, 10,  1.80f, 1.30f, 0.75f, CharacterVisual.AttackStyle.Claw,  "", "巨躯の獣。硬く重い一撃を持つ壁。"),
        Def("dire_wolf",      "ダイアウルフ",         ZombieAI.Species.Beast,    Role.Melee,  Rank.C, 12,  1.20f, 1.75f, 1.55f, CharacterVisual.AttackStyle.Claw,  "", "群れを率いる巨狼。疾さと牙が跳ね上がる。"),
        Def("siren",          "セイレーン",           ZombieAI.Species.Beast,    Role.Debuff, Rank.C, 14,  0.95f, 1.30f, 1.25f, CharacterVisual.AttackStyle.Cast,  "", "歌声で冒険者を惑わせ足止めする妖鳥。"),
        // -- 最上位Ⅲ(depth3) --
        Def("behemoth",       "ベヒーモス",           ZombieAI.Species.Beast,    Role.Tank,   Rank.A, 26,  3.00f, 1.80f, 0.70f, CharacterVisual.AttackStyle.Swing, "", "山の如き巨獣。並の攻撃を寄せ付けぬ絶壁。"),
        Def("fenrir",         "フェンリル",           ZombieAI.Species.Beast,    Role.Melee,  Rank.S, 32,  1.90f, 2.30f, 1.70f, CharacterVisual.AttackStyle.Claw,  "", "神狼。加速しきれば誰も追えぬ牙の化身。"),

        // ═══════════ 😈 魔族 Demonkin（単体性能・吸血／ゴブリン職ツリー） ═══════════
        // -- 基本(depth0) --
        Def("goblin",         "ゴブリン",             ZombieAI.Species.Demonkin, Role.Melee,  Rank.F,  5,  0.90f, 1.00f, 1.05f, CharacterVisual.AttackStyle.Swing, "SPUM_Devil", "魔族の基幹兵。職を得て多彩に分化する。"),
        Def("imp",            "インプ",               ZombieAI.Species.Demonkin, Role.Buff,   Rank.E,  9,  0.70f, 0.95f, 1.20f, CharacterVisual.AttackStyle.Cast,  "SPUM_Devil", "味方魔族を鼓舞する小悪魔の術者。"),
        // -- 進化Ⅰ(depth1)＝基本職 --
        Def("goblin_archer",  "ゴブリンアーチャー",   ZombieAI.Species.Demonkin, Role.Ranged, Rank.E,  8,  0.80f, 1.05f, 1.00f, CharacterVisual.AttackStyle.Stab,  "SPUM_Devil", "弓を取ったゴブリン。手数の遠距離。"),
        Def("hobgoblin",      "ホブゴブリン",         ZombieAI.Species.Demonkin, Role.Melee,  Rank.E, 10,  1.15f, 1.20f, 1.05f, CharacterVisual.AttackStyle.Swing, "SPUM_Devil", "武芸を修めた戦士ゴブリン。吸血で粘る。"),
        Def("goblin_shaman",  "ゴブリンシャーマン",   ZombieAI.Species.Demonkin, Role.Buff,   Rank.E, 10,  0.85f, 1.10f, 1.10f, CharacterVisual.AttackStyle.Cast,  "SPUM_Devil", "呪術を操るゴブリン。味方を鼓舞する。"),
        Def("kobold",         "コボルト",             ZombieAI.Species.Demonkin, Role.Melee,  Rank.E, 10,  1.05f, 1.15f, 1.10f, CharacterVisual.AttackStyle.Swing, "SPUM_Devil", "統率された魔族戦士。吸血で粘る。"),
        // -- 上位Ⅱ(depth2)＝上位職 --
        Def("goblin_ranger",  "ゴブリンレンジャー",   ZombieAI.Species.Demonkin, Role.Ranged, Rank.C, 16,  1.00f, 1.55f, 1.15f, CharacterVisual.AttackStyle.Stab,  "SPUM_Devil", "森を駆ける狙撃兵。急所を的確に射抜く。"),
        Def("goblin_soldier", "ゴブリンソルジャー",   ZombieAI.Species.Demonkin, Role.Tank,   Rank.C, 16,  1.90f, 1.45f, 1.00f, CharacterVisual.AttackStyle.Swing, "SPUM_Devil", "統率された重装兵。隊列を組んで押す。"),
        Def("goblin_mage",    "ゴブリンメイジ",       ZombieAI.Species.Demonkin, Role.Debuff, Rank.C, 18,  0.95f, 1.55f, 1.15f, CharacterVisual.AttackStyle.Cast,  "SPUM_Devil", "魔術を修めたゴブリン。呪いで敵を削ぐ。"),
        Def("orc",            "オーク",               ZombieAI.Species.Demonkin, Role.Tank,   Rank.C, 20,  1.65f, 1.40f, 0.80f, CharacterVisual.AttackStyle.Swing, "SPUM_Devil", "魔族の重装。高HP高火力の主戦力。"),
        Def("dark_elf",       "ダークエルフ",         ZombieAI.Species.Demonkin, Role.Debuff, Rank.B, 22,  1.10f, 1.55f, 1.20f, CharacterVisual.AttackStyle.Cast,  "SPUM_Devil", "精鋭の妨害術士。呪いで冒険者を削ぐ。"),
        // -- 最上位Ⅲ(depth3)＝最上位職 --
        Def("goblin_general", "ゴブリンジェネラル",   ZombieAI.Species.Demonkin, Role.Tank,   Rank.A, 30,  2.60f, 1.90f, 1.05f, CharacterVisual.AttackStyle.Swing, "SPUM_Devil", "軍団を統べる将。味方を束ね鉄壁を築く。"),
        Def("goblin_wizard",  "ゴブリンウィザード",   ZombieAI.Species.Demonkin, Role.Debuff, Rank.S, 32,  1.30f, 2.20f, 1.20f, CharacterVisual.AttackStyle.Cast,  "SPUM_Devil", "魔道の頂に至ったゴブリン。呪詛の権化。"),
    };

    // rig はファミリーから自動決定（Undead/Beast/Demonkin リグを流用）
    private static MinionDef Def(string id, string jp, ZombieAI.Species fam, Role role, Rank rank, int tier,
                                 float hp, float atk, float spd, CharacterVisual.AttackStyle style, string spum, string note)
    {
        return new MinionDef
        {
            id = id, jpName = jp, family = fam, role = role, rank = rank, tierCP = tier,
            hpMult = hp, atkMult = atk, spdMult = spd,
            rig = RigOfFamily(fam), style = style, spumHint = spum, note = note
        };
    }

    public static CharacterVisual.RigType RigOfFamily(ZombieAI.Species fam)
    {
        switch (fam)
        {
            case ZombieAI.Species.Beast: return CharacterVisual.RigType.Beast;
            case ZombieAI.Species.Demonkin: return CharacterVisual.RigType.Demonkin;
            default: return CharacterVisual.RigType.Undead;
        }
    }

    // ================= 参照ヘルパ =================
    public static IReadOnlyList<MinionDef> All => _all;
    public static int Count => _all.Count;

    public static MinionDef Get(int index) => _all[Mathf.Clamp(index, 0, _all.Count - 1)];

    public static bool TryGet(string id, out MinionDef def)
    {
        foreach (var d in _all) if (d.id == id) { def = d; return true; }
        def = default; return false;
    }

    // idからカタログindex（無ければ-1）
    public static int IndexOf(string id)
    {
        for (int i = 0; i < _all.Count; i++) if (_all[i].id == id) return i;
        return -1;
    }

    public static List<MinionDef> ByFamily(ZombieAI.Species fam)
    {
        var list = new List<MinionDef>();
        foreach (var d in _all) if (d.family == fam) list.Add(d);
        return list;
    }

    public static List<MinionDef> ByRole(Role role)
    {
        var list = new List<MinionDef>();
        foreach (var d in _all) if (d.role == role) list.Add(d);
        return list;
    }

    public static string RoleName(Role r)
    {
        switch (r)
        {
            case Role.Tank: return "盾";
            case Role.Melee: return "近接";
            case Role.Ranged: return "遠隔";
            case Role.Buff: return "支援";
            default: return "妨害";
        }
    }

    public static string RankName(Rank r) => r.ToString(); // G/F/E/D/C/B/A/S
}
