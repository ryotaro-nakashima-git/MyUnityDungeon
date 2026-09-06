using UnityEngine;

/// <summary>
/// 魔法システム（原作資料『魔法大辞典』の 属性 × 階級 を実装）。
///
/// - 属性(MagicElement)：**基本9＋派生7＝16種**。属性ごとに状態異常と相性（種族耐性）が違う。
///     基本：火/氷/雷/土/光/闇/水/風/無　派生：影/血/木/神聖/空間/時間/重力
///   ⚠ 派生は「2つの基本属性の合流」で解禁する（研究 `g_der_*` の前提が2つある＝合流ノード）。
///   ⚠⚠ **enumの並びはセーブに載りうるので末尾に足すこと。** 6種の時代の並びは動かさない。
/// - 階級(MagicRank)：最下級→下級→中級→上級→最上級（威力が段階的に跳ね上がる）。
/// - 使い手：
///     防衛側＝術者ロール(Buff/Debuff かつ Cast)の眷属。使える属性/階級は**魔法研究**で解禁する。
///     侵略側＝冒険者の魔法使い/聖職者。こちらは研究ではなく**冒険者ランク**で階級が上がる（世界が育つほど強い魔法）。
/// - 罠の状態異常(TrapKind)と同じ効果系に落として統合する（炎=DoT/氷=凍結/雷=麻痺…）。
/// 関連: [[TrapCatalog]] [[Research]] / ZombieAI(眷属の詠唱) / AdventurerAI(勇者の魔法)。
/// </summary>
public enum MagicElement
{
    Fire, Ice, Thunder, Earth, Light, Dark,   // ← ここまでが最初の6種。**並びを変えない**
    Water, Wind, Void,                        // 基本（追加）
    Shadow, Blood, Wood, Holy, Space, Time, Gravity   // 派生
}
public enum MagicRank { Lowest, Low, Mid, High, Highest } // 最下級/下級/中級/上級/最上級

/// <summary>
/// 🌀 <b>形（呪法）</b>── 属性・階級に次ぐ<b>3本目の軸</b>。範囲と効果の種類をまとめて持つ。
///
/// ⚠⚠ <b>なぜ足したか。</b> エンジンは 属性×階級 でしか呪文を持っていなかったので、
///   研究ツリーにある『蒸気』『溶岩』『転移』『停止』『黒の特異点』といった<b>個別の呪文</b>に
///   置き場所が無く、12件すべて `MagicPower +8%` のような数字に潰されていた（しかも未参照）。
///   ここに畳むことで <b>16属性 × 5階級 × 11形</b> になり、
///   <b>個別の呪文を1つずつ書き足す必要がそもそも無くなる</b>。
///
/// ⚠ <b>並び（index）を変えない。</b> セーブに載りうる。足すときは末尾。
/// 関連: [[MagicCatalog]] [[SpellField]] [[Research]] / ZombieAI(詠唱)。
/// </summary>
public enum SpellForm
{
    Single = 0,       // 単撃    1体
    Pierce = 1,       // 貫き    直線3
    HeatWave = 2,     // 熱波    3×3・継続        ← 蒸気
    Scorch = 3,       // 灼野    3×3・その場に残る ← 溶岩
    Sweep = 4,        // 薙ぎ    扇5              ← 火炎嵐
    Mire = 5,         // 泥沼    3×3・残る・束縛   ← 泥濘／重圧
    Shackle = 6,      // 縛鎖    3×3・停止        ← 停止
    Blink = 7,        // 跳躍    自分             ← 転移
    Bulwark = 8,      // 隔壁    押し返す          ← 空間壁
    Singularity = 9,  // 特異点  5×5・引き寄せ     ← 黒の特異点
    Inferno = 10,     // 八熱／八寒 階層全体       ← 八熱地獄／八寒地獄
}

public static class MagicCatalog
{
    public struct Spell
    {
        public MagicElement element;
        public MagicRank rank;
        public SpellForm form;     // 🌀 形（範囲と効果の種類）
        public string jpName;      // 表示名（例: 中級 火炎・熱波）
        public float power;        // 基礎攻撃力への倍率（階級 × 形 × 属性相性）
        public int trapStatus;     // 付与する状態異常（TrapKind と共通。-1=なし）
        public string colorHex;
        public float radius;       // 影響半径（ワールド単位／0＝単体）
        public float castTime;     // 詠唱にかかる秒数（攻撃間隔に足す）
        public int manaCost;       // 1波で使える魔力の消費
    }

    // 階級ごとの威力と表示名
    private static readonly float[] rankPower = { 0.7f, 1.0f, 1.45f, 2.0f, 2.8f };
    private static readonly string[] rankName = { "最下級", "下級", "中級", "上級", "最上級" };
    public static string RankName(MagicRank r) => rankName[(int)r];
    public static float RankPower(MagicRank r) => rankPower[(int)r];

    // 属性ごとの名前・色・状態異常（TrapKind と統一：1=毒,2=炎,3=氷,4=電気,5=出血／-1=なし）
    // ⚠ 3つの配列は**必ず同じ長さ**。`Make` が index で引くので、片方だけ足すと添字外で落ちる。
    private static readonly string[] elemName =
    { "火炎", "氷結", "雷撃", "地砕", "聖光", "呪詛", "水流", "疾風", "虚無", "影蝕", "血魔", "樹縛", "神聖", "空間", "時間", "重力" };
    private static readonly string[] elemColor =
    { "#e8622e", "#7fd3e6", "#ffd24a", "#a9754a", "#fff3c4", "#b048d0",
      "#4a8ce6", "#a0e6c8", "#6f6889", "#4a3f66", "#c04a6a", "#5aa84a", "#fff8e0", "#8c6ae6", "#d8c04a", "#7a6a8c" };
    // 火=炎DoT 氷=凍結 雷=麻痺 土=なし 光=なし 闇=毒(呪い)
    // 水=なし(押し流す) 風=なし 無=なし(耐性を無視するのが特性) 影=なし 血=出血 木=凍結(蔓で足止め)
    // 神聖=なし 空間=なし 時間=凍結(止める) 重力=麻痺(押し潰して鈍らせる)
    private static readonly int[] elemStatus =
    { 2, 3, 4, -1, -1, 1,  -1, -1, -1,  -1, 5, 3, -1, -1, 3, 4 };

    public static string ElementName(MagicElement e) => elemName[(int)e];
    public static string ElementColor(MagicElement e) => elemColor[(int)e];
    public static int ElementCount => elemName.Length;

    // ================= 🌀 形（呪法）=================
    /// <summary>
    /// 形ごとの中身。⚠ <b>並びは `SpellForm` と1対1。</b>片方だけ足すと添字外で落ちる。
    ///
    /// ⚠ <b>広い形ほど1体あたりの威力を落とし、詠唱と魔力を重くする。</b>
    ///   これをやらないと「広い形を覚えた瞬間に単撃を撃つ理由が消える」＝形が軸にならない。
    ///   広さの代償を持つのが、宙に浮いていた **詠唱3件・魔力3件** の役目。
    /// </summary>
    public struct FormDef
    {
        public string jpName, desc;
        public float radius;      // 影響半径（ワールド単位。1タイル＝1）
        public float powerMult;   // 1体あたりの威力倍率
        public float castTime;    // 詠唱の秒数（研究で短くなる）
        public int manaCost;      // 1波の魔力消費
        public int status;        // 上書きする状態異常（-2＝属性のまま／-1＝なし／0..＝TrapKind）
        public string research;   // 解禁する研究（空＝最初から）
        public bool lingers;      // その場に残る（→ [[SpellField]]）
    }

    private static FormDef FD(string n, string d, float rad, float pw, float ct, int mana, int st, string res, bool ling)
        => new FormDef { jpName = n, desc = d, radius = rad, powerMult = pw, castTime = ct,
                         manaCost = mana, status = st, research = res, lingers = ling };

    // ⚠ 並びは SpellForm と同じ。研究IDは**既存のノードをそのまま使う**（ツリーの形を変えない）。
    private static readonly FormDef[] forms =
    {
        FD("単撃",   "狙った1体に撃つ。詠唱が要らず、魔力も食わない。",             0f,   1.00f, 0f,   0, -2, "",             false),
        FD("貫き",   "直線に3マス貫く。並んだ相手をまとめて撃ち抜く。",             1.6f, 0.70f, 0.2f, 1, -2, "g_pierce",     false),
        FD("熱波",   "3×3を焼く。当たった相手は<b>燃え続ける</b>。",                1.5f, 0.45f, 0.5f, 2, (int)TrapKind.Fire,  "g_fus_steam",  false),
        FD("灼野",   "3×3の地面が<b>焼けたまま残る</b>。踏んだ者を灼く。",          1.5f, 0.35f, 0.6f, 3, (int)TrapKind.Fire,  "g_fus_lava",   true),
        FD("薙ぎ",   "正面の扇状5マスを薙ぎ払う。",                                 3.0f, 0.50f, 0.4f, 2, -2, "g_fus_storm",  false),
        FD("泥沼",   "3×3が<b>沼のまま残る</b>。踏んだ者の足を奪う。",              1.5f, 0.25f, 0.5f, 3, (int)TrapKind.Ice,   "g_fus_mud",    true),
        FD("縛鎖",   "3×3の相手を<b>完全に止める</b>。威力は低い。",                1.5f, 0.30f, 0.8f, 4, (int)TrapKind.Ice,   "g_time_stop",  false),
        FD("跳躍",   "術者が跳ぶ。囲まれても抜けられる（傷は与えない）。",           0f,   0.00f, 0.2f, 1, -1, "g_space_tele", false),
        FD("隔壁",   "空間を歪めて、周りの相手を<b>入口の側へ押し返す</b>。",        1.8f, 0.25f, 0.5f, 3, -2, "g_space_wall", false),
        FD("特異点", "5×5を<b>一点に引き寄せてから圧壊させる</b>。最上位。",        2.9f, 0.60f, 1.5f, 6, -2, "g_grav_hole",  false),
        FD("八熱",   "<b>階層のすべて</b>を灼く／凍てつかせる禁呪。",               99f,  0.35f, 2.5f, 8, (int)TrapKind.Fire,  "g_fus_hell8",  false),
    };

    // ⚠⚠ **半径は「角まで届くか」で決める。** タイルの数で書くと必ず足りない ――
    //   3×3 の角は中心から 1.41、5×5 の角は 2.83 離れている。
    //   実測で 薙ぎ(2.2) が扇のはずなのに 3×3 より狭く、特異点(2.5) は 5×5 の角に届いていなかった。
    //   → 3×3＝1.5 ／ 5×5＝2.9 ／ 扇＝3.0（正面60°なので半径は大きくてよい）。
    //
    // ⚠⚠ **`powerMult` を上げるときは必ず「1体あたり」で考えること。**
    //   最初 熱波を 0.70 で置いたら、相性(×1.25)と噛み合って **単撃の87%を3×3にばら撒く**形になり、
    //   相手が2体いる時点で単撃を撃つ理由が消えていた（実測: 上級火炎 単撃2.00 / 熱波1.75）。
    //   広い形は **1体あたり半分以下** が正しい。詠唱と魔力だけでは代償として足りない
    //   ―― 研究が進むと詠唱は 0.35 倍まで縮み、代償がほぼ消えるため。

    public static int FormCount => forms.Length;
    public static FormDef Form(SpellForm f) => forms[Mathf.Clamp((int)f, 0, forms.Length - 1)];
    public static string FormName(SpellForm f) => Form(f).jpName;
    public static string FormResearchId(SpellForm f) => Form(f).research;
    public static bool IsFormUnlocked(SpellForm f)
    {
        string r = Form(f).research;
        return string.IsNullOrEmpty(r) || ResearchState.IsResearched(r);
    }

    /// <summary>
    /// 🎯 形と属性の相性。<b>乗る属性なら ×1.25、外れれば ×0.8。</b>
    /// ⚠ これが <b>16属性を全部取る意味</b>そのもの。いまは耐性表にしか差が無かった。
    /// ⚠ 単撃だけは相性を持たない（既定の形なので、外れ扱いにすると全員が弱くなる）。
    /// </summary>
    public static float FormAffinity(SpellForm f, MagicElement e)
    {
        if (f == SpellForm.Single) return 1f;
        bool fit;
        switch (f)
        {
            case SpellForm.Pierce:      fit = e == MagicElement.Thunder || e == MagicElement.Light
                                            || e == MagicElement.Holy; break;                               // 神聖の矢
            case SpellForm.HeatWave:    fit = e == MagicElement.Fire || e == MagicElement.Water; break;      // ＝蒸気
            case SpellForm.Scorch:      fit = e == MagicElement.Fire || e == MagicElement.Earth; break;      // ＝溶岩
            case SpellForm.Sweep:       fit = e == MagicElement.Wind || e == MagicElement.Fire; break;       // ＝火炎嵐
            case SpellForm.Mire:        fit = e == MagicElement.Water || e == MagicElement.Earth
                                            || e == MagicElement.Wood || e == MagicElement.Blood; break;    // 血の沼
            case SpellForm.Shackle:     fit = e == MagicElement.Time || e == MagicElement.Ice
                                            || e == MagicElement.Shadow; break;                             // 影の鎖
            case SpellForm.Blink:       fit = e == MagicElement.Space; break;
            case SpellForm.Bulwark:     fit = e == MagicElement.Space || e == MagicElement.Dark; break;      // 闇の壁
            case SpellForm.Singularity: fit = e == MagicElement.Gravity || e == MagicElement.Void; break;
            default:                    fit = e == MagicElement.Fire || e == MagicElement.Ice; break;        // 八熱／八寒
        }
        return fit ? 1.25f : 0.8f;
    }

    // ⚠⚠ **16属性すべてが、少なくとも1つの形に乗ること。**
    //   最初 呪詛・影蝕・血魔・神聖 は<b>どの形にも乗らなかった</b>ので、
    //   その属性の術者だけが**何を撃っても常に ×0.8**という隠れた罰を受けていた
    //   （実測: ゴースト「下級 呪詛・熱波」威力0.36）。属性を足したら**ここも必ず足す**。

    /// <summary>
    /// 🕯️ 詠唱の短縮（研究）。⚠ **これが『詠唱短縮／破棄／無詠唱／加速』の実体**（旧: 未参照の +5/8/12%）。
    /// ⚠ 段は**重ねず一番上だけ**（0.8→0.65→0.5）。掛け算で重ねると終盤だけ跳ねる → [[difficulty-curve-orders]]
    /// </summary>
    public static float CastTimeMult
    {
        get
        {
            float m = 1f;
            if (ResearchState.IsResearched("g_cast3")) m = 0.5f;
            else if (ResearchState.IsResearched("g_cast2")) m = 0.65f;
            else if (ResearchState.IsResearched("g_cast1")) m = 0.8f;
            if (ResearchState.IsResearched("g_time_haste")) m -= 0.15f;   // ⏩ 加速（時間の枝から合流）
            return Mathf.Max(0.25f, m);
        }
    }

    /// <summary>🔷 1波で使える魔力。⚠ **これが『魔力操作／制御／支配』の実体**（旧: 未参照の +5/8/14%）。</summary>
    public static int ManaPool
    {
        get
        {
            int n = 6;
            if (ResearchState.IsResearched("g_mana1")) n += 4;
            if (ResearchState.IsResearched("g_mana2")) n += 8;
            if (ResearchState.IsResearched("g_mana3")) n += 14;
            // 🧊🕳️ 八寒・重圧は「形」を重複させない代わりに器を広げる側に回した（→ [[Research]]）
            if (ResearchState.IsResearched("g_fus_cold8")) n += 6;
            if (ResearchState.IsResearched("g_grav_press")) n += 6;
            return n;
        }
    }

    /// <summary>
    /// 🎯 <b>その一撃が当たるか。</b>形ごとに当たり方が違うのは<b>この1メソッドだけ</b>
    ///（散らすと「なぜこの相手に当たったのか」が二度と追えなくなる）。
    ///
    /// `caster` から `primary`（狙った1体）へ向かって撃ち、`target` に当たるかを返す。
    /// ⚠ 3×3・5×5 は<b>狙った相手を中心に</b>広がる（術者中心ではない）。
    ///   術者中心にすると、離れた相手に撃ったつもりが自分の周りだけ焼く形になる。
    /// </summary>
    public static bool CoversTarget(Spell sp, Vector3 caster, Vector3 primary, Vector3 target)
    {
        switch (sp.form)
        {
            case SpellForm.Single:
                return (target - primary).sqrMagnitude < 0.0001f;
            case SpellForm.Inferno:
                // 🔥 階層全体。⚠ 階層は世界Yで 200 離れているので、半径99なら他の階に漏れない
                return Vector3.Distance(caster, target) <= sp.radius;
            case SpellForm.Pierce:
            {
                Vector3 dir = primary - caster; dir.z = 0f;
                if (dir.sqrMagnitude < 0.0001f) return (target - primary).sqrMagnitude < 0.0001f;
                dir.Normalize();
                Vector3 rel = target - caster; rel.z = 0f;
                float along = Vector3.Dot(rel, dir);
                float side = (rel - dir * along).magnitude;
                return along >= -0.3f && along <= 3.2f && side <= 0.7f;
            }
            case SpellForm.Sweep:
            {
                Vector3 dir = primary - caster; dir.z = 0f;
                if (dir.sqrMagnitude < 0.0001f) return (target - primary).sqrMagnitude < 0.0001f;
                dir.Normalize();
                Vector3 rel = target - caster; rel.z = 0f;
                if (rel.magnitude > sp.radius) return false;
                if (rel.sqrMagnitude < 0.0001f) return true;
                return Vector3.Dot(dir, rel.normalized) >= 0.5f;   // 正面のおよそ60°
            }
            default:
                return Vector3.Distance(target, primary) <= sp.radius;
        }
    }

    public static Spell Make(MagicElement e, MagicRank r) => Make(e, r, SpellForm.Single);

    public static Spell Make(MagicElement e, MagicRank r, SpellForm f)
    {
        var fd = Form(f);
        var s = new Spell();
        s.element = e; s.rank = r; s.form = f;
        // 🏷️ 名前は組み上がる ―― 〈階級〉〈属性〉・〈形〉
        //    例: 「上級 火炎・熱波」「最上級 重力・特異点」。単撃だけは形を書かない（既定なので冗長）。
        s.jpName = rankName[(int)r] + " " + elemName[(int)e]
                 + (f == SpellForm.Single ? "" : "・" + fd.jpName);
        s.power = rankPower[(int)r] * fd.powerMult * FormAffinity(f, e);
        s.trapStatus = fd.status == -2 ? elemStatus[(int)e] : fd.status;
        s.colorHex = elemColor[(int)e];
        s.radius = fd.radius;
        s.castTime = fd.castTime * CastTimeMult;
        s.manaCost = fd.manaCost;
        return s;
    }

    // ================= 相性（属性 × 眷属ファミリー）=================
    // 不死は聖光に弱く呪詛に強い／獣は火炎と雷撃に弱い／魔族は聖光に弱く火炎と呪詛に強い。
    public static float ResistMultVsMinion(MagicElement e, ZombieAI.Species fam)
    {
        // 🕳️ 虚無：**あらゆる耐性を無視して等倍で通る**（研究『無の魔法』の説明どおり）。
        //    相性表を読む前に返すのが肝で、ここが属性を16に増やす一番の見返り。
        if (e == MagicElement.Void) return 1f;
        switch (fam)
        {
            case ZombieAI.Species.Undead:
                if (e == MagicElement.Holy) return 2.0f;    // 神聖＝聖光の上位。不死には天敵
                if (e == MagicElement.Light) return 1.7f;
                if (e == MagicElement.Wood) return 1.15f;   // 生命の側
                if (e == MagicElement.Dark) return 0.4f;
                if (e == MagicElement.Shadow) return 0.4f;
                if (e == MagicElement.Blood) return 0.35f;  // 流す血が無い
                if (e == MagicElement.Ice) return 0.8f;
                return 1f;
            case ZombieAI.Species.Beast:
                if (e == MagicElement.Fire) return 1.35f;
                if (e == MagicElement.Thunder) return 1.25f;
                if (e == MagicElement.Blood) return 1.3f;
                if (e == MagicElement.Gravity) return 1.25f; // 図体が大きいほど重みに弱い
                if (e == MagicElement.Water) return 1.15f;
                if (e == MagicElement.Earth) return 0.85f;
                if (e == MagicElement.Wood) return 0.7f;     // 森が住処
                if (e == MagicElement.Wind) return 0.85f;
                return 1f;
            default: // Demonkin
                if (e == MagicElement.Holy) return 1.8f;
                if (e == MagicElement.Light) return 1.5f;
                if (e == MagicElement.Water) return 1.15f;
                if (e == MagicElement.Fire) return 0.75f;
                if (e == MagicElement.Dark) return 0.55f;
                if (e == MagicElement.Shadow) return 0.5f;
                if (e == MagicElement.Blood) return 0.8f;
                return 1f;
        }
    }

    // 冒険者側の耐性（職で決まる）。聖職者は呪詛に強く、魔法使いは属性全般に少し強い。
    public static float ResistMultVsHero(MagicElement e, AdventurerAI.Job job)
    {
        if (e == MagicElement.Void) return 1f;                                        // 🕳️ 虚無は耐性を見ない
        if (job == AdventurerAI.Job.Cleric && (e == MagicElement.Dark || e == MagicElement.Shadow)) return 0.5f;
        if (job == AdventurerAI.Job.Cleric && e == MagicElement.Holy) return 0.6f;    // 神聖は同じ側の力
        if (job == AdventurerAI.Job.Warrior && e == MagicElement.Blood) return 1.25f; // 前に出る者ほど血を流す
        if (job == AdventurerAI.Job.Warrior && (e == MagicElement.Water || e == MagicElement.Gravity)) return 1.2f; // 重装は水と重みに弱い
        if (job == AdventurerAI.Job.Thief && e == MagicElement.Wood) return 1.25f;    // 速さで避ける者を縛る
        if (job == AdventurerAI.Job.Thief && e == MagicElement.Wind) return 0.8f;
        if (job == AdventurerAI.Job.Mage && e != MagicElement.Light) return 0.85f;
        if (job == AdventurerAI.Job.Warrior && e == MagicElement.Earth) return 0.8f;
        return 1f;
    }

    // ================= 研究による解禁（防衛側＝眷属の魔法）=================
    //  m_elem_* で属性を解禁、m_rank1/2 で使える階級の上限が上がる（既定は下級まで）。
    // ⚠ 属性を足したら**ここに必ず1行足す**。書き忘れると `default` の呪詛に落ちて、
    //   「研究したのに使えない／研究していないのに使える」が同時に起きる。
    private static readonly string[] elemResearch =
    { "g_elem_fire", "g_elem_ice", "g_elem_thunder", "g_elem_earth", "g_elem_light", "g_elem_dark",
      "g_elem_water", "g_elem_wind", "g_elem_void",
      "g_der_shadow", "g_der_blood", "g_der_wood", "g_der_holy", "g_der_space", "g_der_time", "g_der_gravity" };
    public static string ElementResearchId(MagicElement e)
    {
        int i = (int)e;
        return (i >= 0 && i < elemResearch.Length) ? elemResearch[i] : "g_elem_dark";
    }
    /// <summary>その属性が派生（2つの基本属性の合流で開く）かどうか。図鑑/研究UIの見出しに使う。</summary>
    public static bool IsDerived(MagicElement e) => (int)e >= (int)MagicElement.Shadow;
    public static bool IsElementUnlocked(MagicElement e) => ResearchState.IsResearched(ElementResearchId(e));

    // 眷属が使える最高階級（研究で上がる）
    public static MagicRank MinionRankCap()
    {
        if (ResearchState.IsResearched("g_rank3")) return MagicRank.Highest;
        if (ResearchState.IsResearched("g_rank2")) return MagicRank.High;
        if (ResearchState.IsResearched("g_rank1")) return MagicRank.Mid;
        return MagicRank.Low; // 既定＝下級まで
    }

    /// <summary>眷属術者が使う魔法を決める。解禁属性が無ければ false（＝通常攻撃のまま）。</summary>
    public static bool TryPickMinionSpell(int catalogIndex, out Spell spell)
        => TryPickMinionSpell(catalogIndex, -1, out spell);

    /// <summary>
    /// 👑 個体つき。<b>アークの格を持つ術者は階級が1つ上がる</b>（→ [[MinionRank]]）。
    /// ⚠ 個体が無い（巣から湧いた名も無い配下）ときは `individualId = -1` で呼ぶ。
    /// </summary>
    public static bool TryPickMinionSpell(int catalogIndex, int individualId, out Spell spell)
    {
        spell = default(Spell);
        var def = MinionCatalog.Get(catalogIndex);
        // 術者ロール（支援/妨害）かつ詠唱スタイルのみ魔法を使う
        if (def.style != CharacterVisual.AttackStyle.Cast) return false;

        // 種族の得意属性を優先し、無ければ解禁済みの中から選ぶ
        var pref = PreferredElement(def.family, catalogIndex);
        MagicElement chosen = pref; bool found = IsElementUnlocked(pref);
        if (!found)
        {
            for (int i = 0; i < ElementCount; i++)
            {
                var e = (MagicElement)i;
                if (IsElementUnlocked(e)) { chosen = e; found = true; break; }
            }
        }
        if (!found) return false;

        // 階級＝研究上限とその個体のティアで決まる（強い種ほど高階級を扱える）
        int tier = def.tierCP;
        MagicRank byTier = tier >= 30 ? MagicRank.Highest : tier >= 20 ? MagicRank.High : tier >= 10 ? MagicRank.Mid : tier >= 5 ? MagicRank.Low : MagicRank.Lowest;
        MagicRank cap = MinionRankCap();
        // 👑 アークの術者は天井そのものが1つ上がる（＝研究の上限を1段だけ超えられる）
        int archBonus = MinionRank.MagicRankBonus(individualId, def.role);
        MagicRank r = (MagicRank)Mathf.Clamp(Mathf.Min((int)byTier + archBonus, (int)cap + archBonus),
                                             0, (int)MagicRank.Highest);
        spell = Make(chosen, r, PickForm(catalogIndex, chosen));
        return true;
    }

    /// <summary>
    /// 🌀 その個体が身につけている形を1つ選ぶ（属性と同じく<b>湧いたときに1度だけ</b>決まる）。
    ///
    /// ⚠ ランダムにしない。**同じ種の術者はいつも同じ形を使う**ほうが、盤の上で読める。
    ///   選び方：解禁済みの形のうち、<b>その術者の得意属性に乗る</b>ものを優先し、
    ///   同点なら<b>ティアが高い個体ほど重い形</b>を持つ。
    ///
    /// ⚠ 使える形の数（枠）は本来<b>格</b>で増える予定 ―― いまは全員1つ。
    ///   格を実装したら `rankSlots` を渡して上位から複数持たせる。→ [[rank-realm-nest-magic]]
    /// </summary>
    public static SpellForm PickForm(int catalogIndex, MagicElement e)
    {
        var def = MinionCatalog.Get(catalogIndex);
        // ⚠ 弱い個体に重い形を持たせない（詠唱と魔力で何もできなくなる）。
        int weightCap = def.tierCP >= 30 ? 8 : def.tierCP >= 20 ? 4 : def.tierCP >= 10 ? 3 : 2;

        // ⚠⚠ **相性は「重み」ではなく「門」にする。**
        //   点数で混ぜると、`powerMult` の差が相性を押し切ってしまう ―― 実測で2度踏んだ:
        //     ① `相性×10 + 魔力` … 魔力の重さが勝ち、雷撃も影蝕も揃って八熱（相性外れ）を覚えた（20体中7体）
        //     ② `威力×範囲`     … 貫き(0.70) が重く、呪詛の術者が**自分に乗る隔壁を捨てて**貫きを選んだ
        //   → **まず相性で候補を絞り、その中で扱える一番重い形を採る。**
        //     こうすると「その属性らしい形」が必ず出て、段が上がるほど重い形に移る。
        SpellForm best = SpellForm.Single;
        for (int pass = 0; pass < 2 && best == SpellForm.Single; pass++)
        {
            int bestMana = -1; float bestPow = -1f;
            for (int i = 0; i < forms.Length; i++)
            {
                var f = (SpellForm)i;
                if (f == SpellForm.Single) continue;
                if (f == SpellForm.Blink) continue;   // 🌀 跳躍は"持ち技"ではなく**囲まれたときの反射**（→ ZombieAI）
                if (!IsFormUnlocked(f)) continue;
                var fd = forms[i];
                if (fd.manaCost > weightCap) continue;
                // 1周目＝相性が乗る形だけ／2周目＝乗る形が無かったので全部から選ぶ
                if (pass == 0 && FormAffinity(f, e) <= 1f) continue;
                if (fd.manaCost > bestMana || (fd.manaCost == bestMana && fd.powerMult > bestPow))
                { bestMana = fd.manaCost; bestPow = fd.powerMult; best = f; }
            }
        }
        return best;
    }

    /// <summary>魔力が尽きたときに落ちる先＝同じ属性・階級の<b>単撃</b>。</summary>
    public static Spell Fallback(Spell s) => Make(s.element, s.rank, SpellForm.Single);

    // 種族/形態ごとの得意属性（フレーバー）
    public static MagicElement PreferredElement(ZombieAI.Species fam, int catalogIndex)
    {
        string id = MinionCatalog.Get(catalogIndex).id;
        // 👑 王種・古代種は**派生属性**を得意にする（段が上がる見返りを属性でも見せる）。
        //    ⚠ 得意属性が未解禁なら下の共通処理で解禁済みのものに落ちるので、ここは"希望"でよい。
        if (id == "bone_sovereign") return MagicElement.Shadow;
        if (id == "archmage") return MagicElement.Void;
        if (id == "ancient_ossuary") return MagicElement.Gravity;
        if (id == "ancient_weaver") return MagicElement.Time;    // 運命を糸として編む者
        if (id == "ghost" || id == "wraith" || id == "lich" || id == "elder_lich") return MagicElement.Dark;
        if (id == "siren") return MagicElement.Water;            // 水の妖。氷から水へ寄せた
        // ⚠ **足した形態にも得意属性を必ず書く。** 書き忘れると `default` に落ち、
        //   セイレーン系が雷撃・影系が火炎になっていた（実測：歌姫3体が雷撃、宵闇3体が火炎）。
        if (id == "song_maiden" || id == "siren_queen" || id == "ancient_song") return MagicElement.Water;
        if (id == "shadow_priest" || id == "dusk_king" || id == "ancient_dusk") return MagicElement.Shadow;
        if (id == "goblin_shaman") return MagicElement.Earth;
        if (id == "goblin_mage" || id == "goblin_wizard") return MagicElement.Fire;
        if (id == "imp") return MagicElement.Fire;
        if (id == "dark_elf") return MagicElement.Thunder;
        return fam == ZombieAI.Species.Undead ? MagicElement.Dark : fam == ZombieAI.Species.Beast ? MagicElement.Thunder : MagicElement.Fire;
    }

    // ================= 冒険者の魔法（研究ではなくランクで階級が上がる）=================
    public static bool TryPickHeroSpell(AdventurerAI.Job job, int rankIdx, out Spell spell)
    {
        spell = default(Spell);
        if (job != AdventurerAI.Job.Mage && job != AdventurerAI.Job.Cleric) return false;
        // 聖職者＝聖光、魔法使い＝火/氷/雷から（ランクが上がるほど多彩）
        // ⚠⚠ **冒険者に派生属性・虚無を持たせない。** 派生は研究で開く"こちら側の到達点"で、
        //   相手にも配ると軸が1本増えて終盤だけ跳ねる（→ [[difficulty-curve-orders]]）。
        //   世界が育つ表現は既にランク・Lv・脅威度・装備でやっている。
        // ⚠⚠ **形も同じ理由で配らない。** 冒険者の魔法は常に `SpellForm.Single`（下の `Make` は単撃版）。
        //   範囲魔法を相手にも持たせると軸が1本増えて終盤だけ跳ねる。
        //   例外は**他のダンジョンの魔王だけ**にする予定（遠征が難しい理由になる）→ [[rank-realm-nest-magic]]
        MagicElement e;
        if (job == AdventurerAI.Job.Cleric) e = MagicElement.Light;
        else
        {
            int pick = Random.Range(0, rankIdx >= 6 ? 4 : rankIdx >= 4 ? 3 : rankIdx >= 2 ? 2 : 1);
            e = pick == 0 ? MagicElement.Fire : pick == 1 ? MagicElement.Ice
              : pick == 2 ? MagicElement.Thunder : MagicElement.Water;   // 上位は水流まで
        }
        // G..S(0-7) → 最下級..最上級
        MagicRank r = rankIdx >= 7 ? MagicRank.Highest : rankIdx >= 5 ? MagicRank.High : rankIdx >= 3 ? MagicRank.Mid : rankIdx >= 1 ? MagicRank.Low : MagicRank.Lowest;
        spell = Make(e, r);
        return true;
    }
}
