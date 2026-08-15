using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 配下ロスター（原作『ダンジョンバトルロワイヤル』のCPティア × CDO2の役割編成 × 3ファミリー）。
///
/// 設計の位置づけ:
/// - これは『幅を広げる』ためのデータ土台。純staticなので既存シーン/コードに一切触れず単体でコンパイルできる。
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
        // ⚠ atk 1.05 だと スケルトン(1.00) から **+5% しか動かず「進化した実感が無い」**。盾役でも一段ぶんは動かす。
        Def("skeleton_soldier","スケルトンソルジャー", ZombieAI.Species.Undead,  Role.Tank,   Rank.D,  7,  1.60f, 1.25f, 0.90f, CharacterVisual.AttackStyle.Swing, "SPUM_Skelton",       "盾を持つ骸兵。硬く前線を支える。"),
        // ⚠ hp を 1.20 → 1.55 に上げた。ゾンビ(1.45)からの進化なのに**HPが下がっていた**（通しプレイで判明）。
        //    役割が Tank→Melee に移るぶん hp の伸びは小さくてよいが、**進化で下がるステータスを作ってはいけない**。
        Def("ghoul",          "グール",               ZombieAI.Species.Undead,   Role.Melee,  Rank.D,  9,  1.55f, 1.35f, 1.10f, CharacterVisual.AttackStyle.Claw,  "SPUM_Skelton",       "喰らって回復する狂乱の屍。"),
        Def("wraith",         "レイス",               ZombieAI.Species.Undead,   Role.Debuff, Rank.C, 13,  0.95f, 1.20f, 1.30f, CharacterVisual.AttackStyle.Cast,  "DungeonTale_Ghost",  "呪詛で冒険者を弱らせる上位の霊。"),
        // -- 上位Ⅱ(depth2) --
        Def("skeleton_knight","スケルトンナイト",     ZombieAI.Species.Undead,   Role.Tank,   Rank.C, 15,  2.10f, 1.50f, 0.90f, CharacterVisual.AttackStyle.Swing, "SPUM_Skelton",       "重装の不死騎士。鉄壁の要。"),
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
        // ⚠ hp/atk を 0.90/1.00 → 1.05/1.10 に上げた。スケルトン(CP3・1.00/1.00)より高い CP5 なのに
        //    **完全な下位互換**で、買う理由がまったく無かった（通しプレイで判明）。
        //    分化の幅（4形態へ進化）が魔族の売りなので、素の値も少しだけ上に置く。
        Def("goblin",         "ゴブリン",             ZombieAI.Species.Demonkin, Role.Melee,  Rank.F,  5,  1.05f, 1.10f, 1.05f, CharacterVisual.AttackStyle.Swing, "SPUM_Devil", "魔族の基幹兵。職を得て多彩に分化する。"),
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

        // ══════════════════════════════════════════════════════════════════
        // 👑 王種(depth4) / 🦴 古代種(depth5)
        // ⚠⚠ **ここから下は必ず末尾に足すこと。** `Individual.catalogIndex` はセーブに載るので、
        //    途中に挿すと既存のセーブで別の魔物に化ける（施設カタログで一度やりかけた事故と同じ）。
        // ⚠ 研究 m_evo4/m_evo5 でゲートされる（`MinionEvolution.TierResearchId`）。
        //    段階そのものの倍率 `DepthMult` も乗るので（depth4=×1.38 / depth5=×1.42・飽和済み）、
        //    ここの hp/atk は**depth3から素直に一段ぶん**だけ伸ばす。掛け算を二重に効かせない。
        // -- 👑 王種(depth4) --
        Def("doom_lord",      "破軍王",               ZombieAI.Species.Undead,   Role.Melee,  Rank.S, 46,  2.85f, 2.60f, 1.05f, CharacterVisual.AttackStyle.Swing, "SPUM_Skelton", "デスナイトが玉座を得た姿。振るう刃の前に隊列は意味を失う。"),
        Def("bone_sovereign", "骸骨王",               ZombieAI.Species.Undead,   Role.Buff,   Rank.S, 52,  1.85f, 2.45f, 1.00f, CharacterVisual.AttackStyle.Cast,  "SPUM_Skelton", "骨の冠を戴いた死霊王。倒れた者を残らず起こし直す。"),
        Def("titanbeast",     "巨獣王",               ZombieAI.Species.Beast,    Role.Tank,   Rank.S, 44,  3.90f, 2.30f, 0.70f, CharacterVisual.AttackStyle.Swing, "", "ベヒーモスが山と見紛うまで育った姿。道そのものを塞ぐ。"),
        Def("wolf_king",      "狼王",                 ZombieAI.Species.Beast,    Role.Melee,  Rank.S, 48,  2.45f, 2.95f, 1.75f, CharacterVisual.AttackStyle.Claw,  "", "フェンリルが群れの頂に立った姿。速さのまま牙になる。"),
        Def("warlord",        "覇王",                 ZombieAI.Species.Demonkin, Role.Tank,   Rank.S, 48,  3.35f, 2.45f, 1.05f, CharacterVisual.AttackStyle.Swing, "SPUM_Devil", "ゴブリンの将が一族の覇を握った姿。旗の下に全軍が硬くなる。"),
        Def("archmage",       "大呪王",               ZombieAI.Species.Demonkin, Role.Debuff, Rank.S, 50,  1.70f, 2.85f, 1.20f, CharacterVisual.AttackStyle.Cast,  "SPUM_Devil", "呪いを体系にまで高めた魔道の王。触れる前に相手が崩れる。"),
        // -- 🦴 古代種(depth5)：最果て。**ここより先は無い** --
        Def("ancient_revenant", "太古の亡霊王",       ZombieAI.Species.Undead,   Role.Melee,  Rank.S, 62,  3.60f, 3.30f, 1.05f, CharacterVisual.AttackStyle.Swing, "SPUM_Skelton", "滅びた王国ごと残った怨念。斬られた記憶しか残らない。"),
        Def("ancient_ossuary",  "太古の骸神",         ZombieAI.Species.Undead,   Role.Buff,   Rank.S, 70,  2.40f, 3.10f, 1.00f, CharacterVisual.AttackStyle.Cast,  "SPUM_Skelton", "無数の骨が神を象った塊。死そのものを配下として扱う。"),
        Def("ancient_colossus", "太古の巨獣",         ZombieAI.Species.Beast,    Role.Tank,   Rank.S, 60,  5.00f, 2.90f, 0.68f, CharacterVisual.AttackStyle.Swing, "", "地形と見分けが付かなくなった獣。動くまで誰も気づかない。"),
        Def("ancient_fenrir",   "太古の魔狼",         ZombieAI.Species.Beast,    Role.Melee,  Rank.S, 64,  3.10f, 3.80f, 1.80f, CharacterVisual.AttackStyle.Claw,  "", "神代から生き延びた狼。影が本体に追いつかない。"),
        Def("ancient_conqueror","太古の征王",         ZombieAI.Species.Demonkin, Role.Tank,   Rank.S, 64,  4.30f, 3.10f, 1.05f, CharacterVisual.AttackStyle.Swing, "SPUM_Devil", "かつて地上を統べた征服王の遺体。旗はまだ倒れていない。"),
        Def("ancient_weaver",   "太古の織手",         ZombieAI.Species.Demonkin, Role.Debuff, Rank.S, 66,  2.20f, 3.60f, 1.20f, CharacterVisual.AttackStyle.Cast,  "SPUM_Devil", "運命を糸として編む者。結末の方を先に決めてしまう。"),
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

    /// <summary>
    /// 種の定義を引く。
    /// ⚠ **`index >= UniqueCatalog.UniqueBase` ならユニーク魔物**を同じ型で返す。
    ///   既存のコードは全部ここを通って名前も強さも見ているので、
    ///   この1箇所で分岐させれば**呼ぶ側を変えずに**ユニークが幹（Lv・装備・図鑑・盤の絵）に乗る。
    ///   ⚠ 一覧（`All` / `Count` / `ByFamily` / `ByRole`）にはユニークを**含めない**。
    ///     含めると「召喚できる種」の一覧にガチャ限定の種が並んでしまう。
    /// </summary>
    public static MinionDef Get(int index)
    {
        if (UniqueCatalog.IsUnique(index)) return UniqueCatalog.AsMinionDef(UniqueCatalog.LocalOf(index));
        return _all[Mathf.Clamp(index, 0, _all.Count - 1)];
    }

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
