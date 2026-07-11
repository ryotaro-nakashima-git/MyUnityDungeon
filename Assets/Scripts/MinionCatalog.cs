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
///
/// 配線は後段(Unity MCP再接続後): DungeonFeatureManager.SelectedSpecies → SelectedMinion(MinionType) に拡張し、
/// SpawnDefender で Def.hp/atk/spd と Role を適用。Family由来の相性/興奮/遺物/トーテム乗算は現行のまま生かす。
/// 関連: ZombieAI.Species / CharacterVisual.RigType / DemonLord.AffinitySpecies。
/// </summary>
public static class MinionCatalog
{
    // 🎭 役割（CDO2編成）。1部屋の中で役割を散らすと人海戦術ボーナス、同役割の重複は制限する想定。
    public enum Role { Tank, Melee, Ranged, Buff, Debuff }

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
    // 原作のティア序列を守りつつ、3ファミリー × 5役割を埋める。ビジュアルは手持ち在庫(SPUM Skelton/Devil, Dungeon Tale slime/ghost)に寄せる。
    private static readonly List<MinionDef> _all = new List<MinionDef>
    {
        // ---- 不死 Undead（数・粘り・とどめ再生成） ----
        Def("skeleton",      "スケルトン",         ZombieAI.Species.Undead,   Role.Melee,  3,  1.00f, 1.00f, 1.00f, CharacterVisual.AttackStyle.Claw,  "SPUM_Skelton", "不死の標準兵。安価で数を並べる。"),
        Def("skeleton_archer","スケルトンアーチャー",ZombieAI.Species.Undead,  Role.Ranged, 6,  0.75f, 1.10f, 1.00f, CharacterVisual.AttackStyle.Stab,  "SPUM_Skelton", "遠距離から射る。柔らかいが手数。"),
        Def("zombie",        "ゾンビ",             ZombieAI.Species.Undead,   Role.Tank,   4,  1.45f, 0.80f, 0.80f, CharacterVisual.AttackStyle.Claw,  "SPUM_Skelton", "鈍いが硬い壁役。前線で敵を足止め。"),
        Def("ghost",         "ゴースト",           ZombieAI.Species.Undead,   Role.Debuff, 8,  0.70f, 0.85f, 1.20f, CharacterVisual.AttackStyle.Cast,  "DungeonTale_Ghost", "冒険者を怯ませ足を鈍らせる妨害役。"),
        Def("lich",          "リッチ",             ZombieAI.Species.Undead,   Role.Buff,  20,  0.90f, 1.20f, 0.90f, CharacterVisual.AttackStyle.Cast,  "SPUM_Skelton", "周囲の不死を強化・再生成を早める術者。"),

        // ---- 獣 Beast（速い・被弾で加速・後半型） ----
        Def("rat",           "ラット",             ZombieAI.Species.Beast,    Role.Melee,  1,  0.55f, 0.70f, 1.35f, CharacterVisual.AttackStyle.Claw,  "", "極安の群れ。数で押す最下級。"),
        Def("bat",           "バット",             ZombieAI.Species.Beast,    Role.Melee,  2,  0.50f, 0.80f, 1.55f, CharacterVisual.AttackStyle.Claw,  "", "素早く飛び回り撹乱する。"),
        Def("wolf",          "ウルフ",             ZombieAI.Species.Beast,    Role.Melee,  3,  0.90f, 1.20f, 1.40f, CharacterVisual.AttackStyle.Claw,  "", "俊足の狩人。加速して急所を刺す。"),
        Def("harpy",         "ハーピー",           ZombieAI.Species.Beast,    Role.Ranged, 8,  0.80f, 1.10f, 1.30f, CharacterVisual.AttackStyle.Stab,  "", "空から急襲する遠距離獣。"),
        Def("great_beast",   "大獣",               ZombieAI.Species.Beast,    Role.Tank,  10,  1.80f, 1.30f, 0.75f, CharacterVisual.AttackStyle.Claw,  "", "巨躯の獣。硬く重い一撃を持つ壁。"),

        // ---- 魔族 Demonkin（単体性能・吸血） ----
        Def("goblin",        "ゴブリン",           ZombieAI.Species.Demonkin, Role.Melee,  5,  0.90f, 1.00f, 1.05f, CharacterVisual.AttackStyle.Swing, "SPUM_Devil", "魔族の基幹兵。進化で分化する。"),
        Def("goblin_archer", "ゴブリンアーチャー", ZombieAI.Species.Demonkin, Role.Ranged, 8,  0.80f, 1.05f, 1.00f, CharacterVisual.AttackStyle.Stab,  "SPUM_Devil", "ゴブリンの遠距離進化。"),
        Def("kobold",        "コボルト",           ZombieAI.Species.Demonkin, Role.Melee, 10,  1.05f, 1.15f, 1.10f, CharacterVisual.AttackStyle.Swing, "SPUM_Devil", "統率された魔族戦士。吸血で粘る。"),
        Def("imp",           "インプ",             ZombieAI.Species.Demonkin, Role.Buff,  15,  0.85f, 1.10f, 1.10f, CharacterVisual.AttackStyle.Cast,  "SPUM_Devil", "味方魔族を鼓舞する小悪魔の術者。"),
        Def("orc",           "オーク",             ZombieAI.Species.Demonkin, Role.Tank,  20,  1.65f, 1.40f, 0.80f, CharacterVisual.AttackStyle.Swing, "SPUM_Devil", "魔族の重装。高HP高火力の主戦力。"),
        Def("dark_elf",      "ダークエルフ",       ZombieAI.Species.Demonkin, Role.Debuff,50,  1.00f, 1.30f, 1.15f, CharacterVisual.AttackStyle.Cast,  "SPUM_Devil", "精鋭の妨害術士。呪いで冒険者を削ぐ。"),
    };

    // rig はファミリーから自動決定（Undead/Beast/Demonkin リグを流用）
    private static MinionDef Def(string id, string jp, ZombieAI.Species fam, Role role, int tier,
                                 float hp, float atk, float spd, CharacterVisual.AttackStyle style, string spum, string note)
    {
        return new MinionDef
        {
            id = id, jpName = jp, family = fam, role = role, tierCP = tier,
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
}
