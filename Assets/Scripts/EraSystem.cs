using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⏳ 時代・偉業・誓約・災厄（Civ VII の Age / Triumph / Dedication / Crisis）。C3。
///
/// Civ VII 1.4.0 の形をそのまま持ち込む:
/// - 世界は **3つの時代** を進む。時代は「ターン数」ではなく **偉業(Triumph)の達成** で進む。
/// - **小偉業** ＝ 達成すると即時報酬。**大偉業** ＝ 次の時代へ持ち込む **誓約(Dedication)** が1枚解禁される。
/// - 時代の変わり目に、解禁した誓約から **3枚だけ** 選ぶ。全部が等価なので「どれを取っても強い」。
/// - 時代の終盤には **災厄(Crisis)** が来て、**必ず負の政策を1枚選ぶ**。逃げ道は無い。
///
/// この作品では、時代＝**魔王がどれだけ世に知られたか**。進むほど冒険者の水準が上がる（諸刃）。
/// 純static・実行時保持。関連: [[civ7-roadmap]] [[difficulty-curve-orders]]。
/// </summary>
public static class EraSystem
{
    public enum Era { Dawn = 0, Growth = 1, End = 2 }

    public static Era Current { get; private set; }
    public static int Progress { get; private set; }         // 0..100
    /// <summary>
    /// ⏳ 時代を1つ進めるのに要る進行度。
    ///
    /// ⚠ 旧仕様は **100**、しかも偉業の配点が「小12×4＋大26×2 ＝ ちょうど100」だったので、
    ///   **その時代の偉業を全部やらないと進まないが、全部やれば即進む**という設計になっていた。
    ///   胎動の6つ（冒険者20体／階層3／罠15／拠点3／真名1／版図30）は実測でT8前後に全部埋まり、
    ///   胎動→T8・伸長→T13・終焉で閾値1.5倍 → **T18で決着**していた。
    ///
    /// Civ VII は 1時代 **120〜160ターン**、偉業は1時代 **30個**あって**全部やる必要はない**。
    /// 同じ形にするため、進行を「偉業だけ」から **偉業＋毎ターンの自然進行** に変える。
    /// 210 ＝ 自然進行(+5/T)だけなら42ターン／偉業を4つ拾えば約30ターン／全部やれば22ターン。
    /// 3時代で **概ね 70〜110ターン**（狙いは80〜100）。→ [[civ7-gap-plan]]
    /// </summary>
    public const int Need = 210;
    /// <summary>偉業を取らなくても時代は進む（Civの Age Progress に相当する下限）。</summary>
    public const int ProgressPerTurn = 5;
    public const int CrisisAt = 160;                         // ここを超えると災厄が始まる（Need の約3/4）

    /// <summary>
    /// 🧱 偉業から入る進行度の**上限**（1時代あたり）。
    ///
    /// ⚠ 偉業を30個に増やすと、片っ端から埋める遊び方をしたときに時代が一瞬で過ぎてしまう。
    ///   Civ VII が「全部やる必要はない」と言えるのは、**偉業が進行の一部でしかない**から。
    ///   ここで頭を打たせることで、**どんなに偉業を取っても1時代は最低 (210-126)/5 = 17ターン**になる。
    ///   偉業は「早める手段」であって「飛ばす手段」ではない。
    /// </summary>
    public static int TriumphProgressCap => Mathf.RoundToInt(Need * 0.6f);   // 126
    private static int triumphProgressThisEra;

    public static string EraName(Era e) => e == Era.Dawn ? "胎動の時代" : e == Era.Growth ? "伸長の時代" : "終焉の時代";
    public static string EraDesc(Era e) => e == Era.Dawn ? "まだ誰も、この迷宮を脅威とは思っていない。"
        : e == Era.Growth ? "名が知れ渡り、ギルドと国家が本腰を入れ始めた。"
        : "勇者が立ち、世界が総力で潰しにかかる。";
    /// <summary>時代が進むほど来る冒険者が強い（諸刃）。※対数でも上限でもない直接の底上げなので小さく。</summary>
    public static float TierBias => Current == Era.Dawn ? 0f : Current == Era.Growth ? 0.6f : 1.2f;

    // ============ 🏅 偉業（Triumph） ============
    /// <summary>
    /// 偉業の達成条件。**判定を90件ぶん手書きすると必ずどこかを取り違える**ので、
    /// 「何を・いくつ」だけをデータに持たせて、判定は1箇所の switch にまとめる。
    /// 新しい偉業を足すときは、ここに種類を1つ足して `Value()` に1行書けばよい。
    /// </summary>
    public enum Cond
    {
        Kill, TrapKill, MagicKill, Boss, Celebrate, Forge, ForgeHigh, Districts, Discoveries,
        Floors, Owned, Territory, Scouts, Settlements, Cities,
        Research, Evolved, MinionLevel, LordLevel,
        Materials, Dp, Relics, Wonders, EmotionSpent, AttrPoints,
        Influence, Suzerain, KinNamed, KinCount, RivalsDead,
        /// <summary>⚠️ 危険度（1=三級 … 5=特級）。研究の深いノードの解放条件に使う → [[DangerRank]]。</summary>
        Danger,
    }

    public struct TriumphDef
    {
        public string id, jpName, cond;
        public Era era;
        public bool major;                 // 大偉業＝誓約が1枚解禁される
        public Cond kind; public int need; // 達成条件（データ駆動）
        public int dp, mat, rp, emo, fame;
        // 🎖️ レガシーの道（Civ VII）：偉業は6つの軸のどれかに属し、達成でその軸の**属性ポイント**が入る。
        //    小偉業=1点／大偉業=2点。→ [[AttributeSystem]]
        public AttributeSystem.Axis axis;
    }

    private static readonly TriumphDef[] triumphs =
    {
        // ══════════ 胎動の時代（30件・うち大偉業6） ══════════
        // ── War ──
        T("t0_kill10", Era.Dawn, false, AttributeSystem.Axis.War, "冒険者を10体倒す", Cond.Kill, 10),
        T("t0_kill25", Era.Dawn, false, AttributeSystem.Axis.War, "冒険者を25体倒す", Cond.Kill, 25),
        T("t0_trapkill10", Era.Dawn, false, AttributeSystem.Axis.War, "罠でとどめを10回さす", Cond.TrapKill, 10),
        T("t0_boss1", Era.Dawn, false, AttributeSystem.Axis.War, "ボスを任命する", Cond.Boss, 1),
        T("t0_kill50", Era.Dawn, true , AttributeSystem.Axis.War, "冒険者を50体倒す", Cond.Kill, 50),
        // ── Expand ──
        T("t0_floors2", Era.Dawn, false, AttributeSystem.Axis.Expand, "階層を2つ作る", Cond.Floors, 2),
        T("t0_owned8", Era.Dawn, false, AttributeSystem.Axis.Expand, "版図を8タイルにする", Cond.Owned, 8),
        T("t0_owned20", Era.Dawn, false, AttributeSystem.Axis.Expand, "版図を20タイルにする", Cond.Owned, 20),
        T("t0_scouts1", Era.Dawn, false, AttributeSystem.Axis.Expand, "斥候を放つ", Cond.Scouts, 1),
        T("t0_territory30", Era.Dawn, true , AttributeSystem.Axis.Expand, "拠点の支配を30タイルに広げる", Cond.Territory, 30),
        // ── Science ──
        T("t0_research3", Era.Dawn, false, AttributeSystem.Axis.Science, "研究を3つ修める", Cond.Research, 3),
        T("t0_research8", Era.Dawn, false, AttributeSystem.Axis.Science, "研究を8つ修める", Cond.Research, 8),
        T("t0_magickill5", Era.Dawn, false, AttributeSystem.Axis.Science, "魔法でとどめを5回さす", Cond.MagicKill, 5),
        T("t0_evolved1", Era.Dawn, false, AttributeSystem.Axis.Science, "配下を1体進化させる", Cond.Evolved, 1),
        T("t0_research14", Era.Dawn, true , AttributeSystem.Axis.Science, "研究を14修める", Cond.Research, 14),
        // ── Wealth ──
        T("t0_settlements2", Era.Dawn, false, AttributeSystem.Axis.Wealth, "拠点を2つ持つ", Cond.Settlements, 2),
        T("t0_districts2", Era.Dawn, false, AttributeSystem.Axis.Wealth, "施設を2つ建てる", Cond.Districts, 2),
        T("t0_forge2", Era.Dawn, false, AttributeSystem.Axis.Wealth, "武具を2つ鍛える", Cond.Forge, 2),
        T("t0_materials40", Era.Dawn, false, AttributeSystem.Axis.Wealth, "素材を40貯める", Cond.Materials, 40),
        T("t0_settlements3", Era.Dawn, true , AttributeSystem.Axis.Wealth, "拠点を3つ持つ", Cond.Settlements, 3),
        // ── Culture ──
        T("t0_kinnamed1", Era.Dawn, false, AttributeSystem.Axis.Culture, "眷属に真名を与える", Cond.KinNamed, 1),
        T("t0_emotionspent3", Era.Dawn, false, AttributeSystem.Axis.Culture, "感情ツリーを3つ開く", Cond.EmotionSpent, 3),
        T("t0_discoveries2", Era.Dawn, false, AttributeSystem.Axis.Culture, "発見を2つ得る", Cond.Discoveries, 2),
        T("t0_celebrate1", Era.Dawn, false, AttributeSystem.Axis.Culture, "祝祭を1度起こす", Cond.Celebrate, 1),
        T("t0_kincount2", Era.Dawn, true , AttributeSystem.Axis.Culture, "眷属を2人にする", Cond.KinCount, 2),
        // ── Diplo ──
        T("t0_influence30", Era.Dawn, false, AttributeSystem.Axis.Diplo, "威名を30貯める", Cond.Influence, 30),
        T("t0_suzerain1", Era.Dawn, false, AttributeSystem.Axis.Diplo, "独立勢力を1つ従える", Cond.Suzerain, 1),
        T("t0_attrpoints3", Era.Dawn, false, AttributeSystem.Axis.Diplo, "属性ポイントを3得る", Cond.AttrPoints, 3),
        T("t0_dp2000", Era.Dawn, false, AttributeSystem.Axis.Diplo, "DPを2,000貯める", Cond.Dp, 2000),
        T("t0_influence80", Era.Dawn, true , AttributeSystem.Axis.Diplo, "威名を80貯める", Cond.Influence, 80),
        // ══════════ 伸長の時代（30件・うち大偉業6） ══════════
        // ── War ──
        T("t1_kill100", Era.Growth, false, AttributeSystem.Axis.War, "冒険者を100体倒す", Cond.Kill, 100),
        T("t1_trapkill40", Era.Growth, false, AttributeSystem.Axis.War, "罠でとどめを40回さす", Cond.TrapKill, 40),
        T("t1_magickill30", Era.Growth, false, AttributeSystem.Axis.War, "魔法でとどめを30回さす", Cond.MagicKill, 30),
        T("t1_rivalsdead1", Era.Growth, false, AttributeSystem.Axis.War, "他の魔王を1人排除する", Cond.RivalsDead, 1),
        T("t1_kill200", Era.Growth, true , AttributeSystem.Axis.War, "冒険者を200体倒す", Cond.Kill, 200),
        // ── Expand ──
        T("t1_floors4", Era.Growth, false, AttributeSystem.Axis.Expand, "階層を4つ作る", Cond.Floors, 4),
        T("t1_owned45", Era.Growth, false, AttributeSystem.Axis.Expand, "版図を45タイルにする", Cond.Owned, 45),
        T("t1_territory60", Era.Growth, false, AttributeSystem.Axis.Expand, "支配を60タイルに広げる", Cond.Territory, 60),
        T("t1_scouts3", Era.Growth, false, AttributeSystem.Axis.Expand, "斥候を3人放つ", Cond.Scouts, 3),
        T("t1_floors5", Era.Growth, true , AttributeSystem.Axis.Expand, "階層を5つ作る", Cond.Floors, 5),
        // ── Science ──
        T("t1_research20", Era.Growth, false, AttributeSystem.Axis.Science, "研究を20修める", Cond.Research, 20),
        T("t1_research28", Era.Growth, false, AttributeSystem.Axis.Science, "研究を28修める", Cond.Research, 28),
        T("t1_evolved6", Era.Growth, false, AttributeSystem.Axis.Science, "配下を6体進化させる", Cond.Evolved, 6),
        T("t1_minionlevel30", Era.Growth, false, AttributeSystem.Axis.Science, "配下をLv30まで育てる", Cond.MinionLevel, 30),
        T("t1_research36", Era.Growth, true , AttributeSystem.Axis.Science, "研究を36修める", Cond.Research, 36),
        // ── Wealth ──
        T("t1_districts8", Era.Growth, false, AttributeSystem.Axis.Wealth, "施設を8つ建てる", Cond.Districts, 8),
        T("t1_forge6", Era.Growth, false, AttributeSystem.Axis.Wealth, "武具を6つ鍛える", Cond.Forge, 6),
        T("t1_forgehigh1", Era.Growth, false, AttributeSystem.Axis.Wealth, "上位の武具を1つ鍛える", Cond.ForgeHigh, 1),
        T("t1_materials200", Era.Growth, false, AttributeSystem.Axis.Wealth, "素材を200貯める", Cond.Materials, 200),
        T("t1_cities2", Era.Growth, true , AttributeSystem.Axis.Wealth, "都市を2つ持つ", Cond.Cities, 2),
        // ── Culture ──
        T("t1_wonders1", Era.Growth, false, AttributeSystem.Axis.Culture, "遺産のある領域を支配する", Cond.Wonders, 1),
        T("t1_emotionspent12", Era.Growth, false, AttributeSystem.Axis.Culture, "感情ツリーを12開く", Cond.EmotionSpent, 12),
        T("t1_relics3", Era.Growth, false, AttributeSystem.Axis.Culture, "遺物を3つ集める", Cond.Relics, 3),
        T("t1_celebrate6", Era.Growth, false, AttributeSystem.Axis.Culture, "祝祭を6度起こす", Cond.Celebrate, 6),
        T("t1_relics5", Era.Growth, true , AttributeSystem.Axis.Culture, "遺物を5つ集める", Cond.Relics, 5),
        // ── Diplo ──
        T("t1_influence250", Era.Growth, false, AttributeSystem.Axis.Diplo, "威名を250貯める", Cond.Influence, 250),
        T("t1_suzerain2", Era.Growth, false, AttributeSystem.Axis.Diplo, "独立勢力を2つ従える", Cond.Suzerain, 2),
        T("t1_attrpoints10", Era.Growth, false, AttributeSystem.Axis.Diplo, "属性ポイントを10得る", Cond.AttrPoints, 10),
        T("t1_kincount3", Era.Growth, false, AttributeSystem.Axis.Diplo, "眷属を3人にする", Cond.KinCount, 3),
        T("t1_suzerain3", Era.Growth, true , AttributeSystem.Axis.Diplo, "独立勢力を3つ従える", Cond.Suzerain, 3),
        // ══════════ 終焉の時代（30件・うち大偉業6） ══════════
        // ── War ──
        T("t2_kill400", Era.End, false, AttributeSystem.Axis.War, "冒険者を400体倒す", Cond.Kill, 400),
        T("t2_trapkill120", Era.End, false, AttributeSystem.Axis.War, "罠でとどめを120回さす", Cond.TrapKill, 120),
        T("t2_magickill150", Era.End, false, AttributeSystem.Axis.War, "魔法でとどめを150回さす", Cond.MagicKill, 150),
        T("t2_rivalsdead2", Era.End, false, AttributeSystem.Axis.War, "他の魔王を2人排除する", Cond.RivalsDead, 2),
        T("t2_rivalsdead3", Era.End, true , AttributeSystem.Axis.War, "他の魔王を全員排除する", Cond.RivalsDead, 3),
        // ── Expand ──
        T("t2_floors6", Era.End, false, AttributeSystem.Axis.Expand, "階層を6つ作る", Cond.Floors, 6),
        T("t2_owned110", Era.End, false, AttributeSystem.Axis.Expand, "版図を110タイルにする", Cond.Owned, 110),
        T("t2_territory150", Era.End, false, AttributeSystem.Axis.Expand, "支配を150タイルに広げる", Cond.Territory, 150),
        T("t2_floors7", Era.End, false, AttributeSystem.Axis.Expand, "階層を7つ作る", Cond.Floors, 7),
        T("t2_owned160", Era.End, true , AttributeSystem.Axis.Expand, "版図を160タイルにする", Cond.Owned, 160),
        // ── Science ──
        T("t2_research42", Era.End, false, AttributeSystem.Axis.Science, "研究を42修める", Cond.Research, 42),
        T("t2_research48", Era.End, false, AttributeSystem.Axis.Science, "研究を48修める", Cond.Research, 48),
        T("t2_evolved14", Era.End, false, AttributeSystem.Axis.Science, "配下を14体進化させる", Cond.Evolved, 14),
        T("t2_minionlevel50", Era.End, false, AttributeSystem.Axis.Science, "配下をLv50まで育てる", Cond.MinionLevel, 50),
        T("t2_research54", Era.End, true , AttributeSystem.Axis.Science, "研究を54修める", Cond.Research, 54),
        // ── Wealth ──
        T("t2_districts20", Era.End, false, AttributeSystem.Axis.Wealth, "施設を20建てる", Cond.Districts, 20),
        T("t2_forgehigh6", Era.End, false, AttributeSystem.Axis.Wealth, "上位の武具を6つ鍛える", Cond.ForgeHigh, 6),
        T("t2_materials600", Era.End, false, AttributeSystem.Axis.Wealth, "素材を600貯める", Cond.Materials, 600),
        T("t2_dp40000", Era.End, false, AttributeSystem.Axis.Wealth, "DPを40,000貯める", Cond.Dp, 40000),
        T("t2_cities4", Era.End, true , AttributeSystem.Axis.Wealth, "都市を4つ持つ", Cond.Cities, 4),
        // ── Culture ──
        T("t2_wonders3", Era.End, false, AttributeSystem.Axis.Culture, "遺産を3つ支配する", Cond.Wonders, 3),
        T("t2_emotionspent30", Era.End, false, AttributeSystem.Axis.Culture, "感情ツリーを30開く", Cond.EmotionSpent, 30),
        T("t2_relics10", Era.End, false, AttributeSystem.Axis.Culture, "遺物を10集める", Cond.Relics, 10),
        T("t2_discoveries12", Era.End, false, AttributeSystem.Axis.Culture, "発見を12得る", Cond.Discoveries, 12),
        T("t2_relics14", Era.End, true , AttributeSystem.Axis.Culture, "遺物を14集める", Cond.Relics, 14),
        // ── Diplo ──
        T("t2_influence900", Era.End, false, AttributeSystem.Axis.Diplo, "威名を900貯める", Cond.Influence, 900),
        T("t2_suzerain4", Era.End, false, AttributeSystem.Axis.Diplo, "独立勢力を4つ従える", Cond.Suzerain, 4),
        T("t2_attrpoints24", Era.End, false, AttributeSystem.Axis.Diplo, "属性ポイントを24得る", Cond.AttrPoints, 24),
        T("t2_lordlevel40", Era.End, false, AttributeSystem.Axis.Diplo, "魔王をLv40まで育てる", Cond.LordLevel, 40),
        T("t2_lordlevel55", Era.End, true , AttributeSystem.Axis.Diplo, "魔王をLv55まで育てる", Cond.LordLevel, 55),
    };

    /// <summary>
    /// 偉業を1件つくる。**報酬は時代と大小から機械的に決める**（90件ぶん手で書くと必ずばらつく）。
    /// 軸ごとに少しだけ色を付ける：軍=感情／文化=名声／科学=研究点／富=素材。
    /// </summary>
    private static TriumphDef T(string id, Era e, bool major, AttributeSystem.Axis axis, string cond, Cond kind, int need)
    {
        int tier = (int)e;                       // 0/1/2
        float m = major ? 2.4f : 1f;
        float scale = (1f + tier * 2.2f) * m;    // 胎動小=1.0 … 終焉大=13.0
        var t = new TriumphDef
        {
            id = id, era = e, major = major, axis = axis, cond = cond, jpName = cond,
            kind = kind, need = need,
            // ⚠ DPは終盤に桁が増える（終焉では4万DPを貯める偉業がある）ので、報酬も時代で桁を上げる。
            //    据え置くと「終焉の大偉業の報酬が序盤の小偉業と同じ体感」になる。
            dp = Mathf.RoundToInt(300 * scale * (1f + tier)),
            rp = Mathf.RoundToInt(4 * scale),
        };
        if (axis == AttributeSystem.Axis.War) t.emo = Mathf.RoundToInt(30 * scale);
        if (axis == AttributeSystem.Axis.Culture) t.fame = Mathf.RoundToInt(25 * scale);
        if (axis == AttributeSystem.Axis.Wealth) t.mat = Mathf.RoundToInt(10 * scale);
        if (axis == AttributeSystem.Axis.Science) t.rp = Mathf.RoundToInt(8 * scale);
        return t;
    }

    public static int TriumphCount => triumphs.Length;
    public static TriumphDef Triumph(int i) => triumphs[Mathf.Clamp(i, 0, triumphs.Length - 1)];
    /// <summary>偉業1つぶんの進行度。1時代30件（小24＋大6）＝最大 24×6+6×14 = 228 だが、
    /// **`TriumphProgressCap`(126) で頭を打つ**ので「全部やって時代を飛ばす」はできない。</summary>
    public static int ProgressOf(TriumphDef t) => t.major ? 14 : 6;

    private static HashSet<string> achieved;
    private static void EnsureInit()
    {
        if (achieved != null) return;
        achieved = new HashSet<string>();
        unlockedDedications = new List<int>();
        chosenDedications = new List<int>();
        crisisPolicy = -1; crisisMitigated = false;
    }
    public static void Reset() { achieved = null; Current = Era.Dawn; Progress = 0; triumphProgressThisEra = 0; CrisisActive = false; EnsureInit(); }
    public static bool IsAchieved(string id) { EnsureInit(); return achieved.Contains(id); }

    /// <summary>外から時代の進行を足す（物語事件など）。</summary>
    public static void AddProgress(int n)
    {
        EnsureInit();
        Progress = Mathf.Clamp(Progress + n, 0, Need);
        Debug.Log($"⏳『時代の進行』+{n} → {Progress}/{Need}");
    }

    /// <summary>いまの時代の偉業を並べる。</summary>
    public static List<TriumphDef> CurrentTriumphs()
    {
        var l = new List<TriumphDef>();
        foreach (var t in triumphs) if (t.era == Current) l.Add(t);
        return l;
    }

    // ============ 📜 誓約（Dedication）＝大偉業で解禁し、時代の変わり目に3枚選ぶ ============
    public struct DedicationDef { public string jpName, desc; public string colorHex; }
    private static readonly DedicationDef[] dedications =
    {
        D("叡智の誓い", "毎ターン 研究点 +5",                        "#8cb8e6"),
        D("熱狂の誓い", "毎ターン 感情 +8",                          "#c04a6a"),
        D("豊穣の誓い", "拠点の食料 +2",                             "#5cc47c"),
        D("城塞の誓い", "自領すべての守り +80",                      "#b478e6"),
        D("簒奪の誓い", "他の魔王への侵攻で戦力 +25%",               "#e05a5a"),
        D("静謐の誓い", "全拠点の不満 -2",                           "#57c3ab"),
        D("軍旅の誓い", "眷属の移動力 +1",                           "#e3a94a"),
        D("黄金の誓い", "領域のDP +20%",                             "#e3c34a"),
        D("開墾の誓い", "国境の拡張 +40%",                           "#8ec46a"),
        D("秘匿の誓い", "得る名声 -20%（世に知られる速さを抑える）", "#9c95b4"),
    };
    private static DedicationDef D(string n, string d, string c) => new DedicationDef { jpName = n, desc = d, colorHex = c };
    public static int DedicationCount => dedications.Length;
    public static DedicationDef Dedication(int i) => dedications[Mathf.Clamp(i, 0, dedications.Length - 1)];

    private static List<int> unlockedDedications, chosenDedications;
    public static IReadOnlyList<int> Unlocked { get { EnsureInit(); return unlockedDedications; } }
    public static IReadOnlyList<int> Chosen { get { EnsureInit(); return chosenDedications; } }
    public const int MaxChosen = 3;
    public static bool HasDedication(int i) { EnsureInit(); return chosenDedications.Contains(i); }

    public static bool TryChooseDedication(int i)
    {
        EnsureInit();
        if (!unlockedDedications.Contains(i)) { Debug.LogWarning("⚠️ その誓約はまだ解禁されていません（大偉業で解禁）。"); return false; }
        if (chosenDedications.Contains(i)) { chosenDedications.Remove(i); Debug.Log($"📜『誓約を解く』{Dedication(i).jpName}"); return true; }
        if (chosenDedications.Count >= MaxChosen) { Debug.LogWarning($"⚠️ 誓約は{MaxChosen}枚までです（どれかを解いてください）。"); return false; }
        chosenDedications.Add(i);
        Debug.Log($"📜『誓約』{Dedication(i).jpName} ― {Dedication(i).desc}");
        return true;
    }

    private static void UnlockDedication()
    {
        // まだ解禁していないものから1つ（順に解禁して、選ぶ楽しみは「3枚に絞る」ほうに置く）
        for (int i = 0; i < dedications.Length; i++)
            if (!unlockedDedications.Contains(i))
            {
                unlockedDedications.Add(i);
                Debug.Log($"📜『誓約が解禁された』{Dedication(i).jpName} ― {Dedication(i).desc}（時代の変わり目に3枚まで選べます）");
                return;
            }
    }

    // ============ ☄️ 災厄（Crisis）＝時代の終盤に必ず負の政策を1枚選ぶ ============
    public struct CrisisDef { public string jpName, desc; }
    private static readonly CrisisDef[] crises =
    {
        Cr("飢饉", "拠点の食料 -2"),
        Cr("叛乱", "全拠点の不満 +2"),
        Cr("枯渇", "領域のDP -25%"),
        Cr("侵攻", "他の魔王の力 +30%"),
        Cr("停滞", "国境の拡張 -50%"),
    };
    private static CrisisDef Cr(string n, string d) => new CrisisDef { jpName = n, desc = d };
    public static int CrisisCount => crises.Length;
    public static CrisisDef Crisis(int i) => crises[Mathf.Clamp(i, 0, crises.Length - 1)];

    public static bool CrisisActive { get; private set; }
    private static int crisisPolicy = -1;
    public static int CrisisPolicy { get { EnsureInit(); return crisisPolicy; } }

    // ============ 🛡️ 危機への対抗策（S6：Civ VII の Crisis 対策） ============
    //  災厄は「必ず負の政策を1枚選ぶ」だが、**代価を払えば半分に和らげられる**。
    //  Civ VII で全員が危機に対処すると次時代に恩恵が出るのと同じで、**耐えたことが報われる**ようにする。
    private static bool crisisMitigated;
    public static bool CrisisMitigated { get { EnsureInit(); return crisisMitigated; } }
    /// <summary>対抗策の費用（DP）。時代が進むほど重い。</summary>
    public static int MitigateCost { get { return 800 + 600 * (int)Current; } }
    /// <summary>和らげたときの倍率（負の効果が半分になる）。</summary>
    public static float CrisisPower { get { return CrisisMitigated ? 0.5f : 1f; } }
    /// <summary>災厄ごとの対抗策の名前。</summary>
    public static string MitigateName(int i)
    {
        switch (i)
        {
            case 0: return "備蓄の放出";
            case 1: return "見せしめと恩赦";
            case 2: return "坑道の再掘削";
            case 3: return "国境の増援";
            default: return "測量のやり直し";
        }
    }
    public static bool TryMitigate()
    {
        EnsureInit();
        if (!CrisisActive || crisisPolicy < 0) { Debug.LogWarning("⚠️ まず災厄の政策を選んでください。"); return false; }
        if (crisisMitigated) { Debug.LogWarning("⚠️ もう手は打ってあります。"); return false; }
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(MitigateCost)) { Debug.LogWarning($"⚠️ DP不足（要{MitigateCost}）。"); return false; }
        crisisMitigated = true;
        Debug.Log($"🛡️『{MitigateName(crisisPolicy)}』手を打った（-{MitigateCost}DP・災厄の影響が半分になる）");
        return true;
    }

    public static bool TryChooseCrisisPolicy(int i)
    {
        EnsureInit();
        if (!CrisisActive) { Debug.LogWarning("⚠️ いまは災厄ではありません。"); return false; }
        if (crisisPolicy >= 0) { Debug.LogWarning("⚠️ 災厄の政策は変えられません。"); return false; }
        crisisPolicy = Mathf.Clamp(i, 0, crises.Length - 1);
        Debug.Log($"☄️『災厄の政策』{Crisis(crisisPolicy).jpName} ― {Crisis(crisisPolicy).desc} を選んだ（この時代のあいだ続く）");
        return true;
    }

    // ============ 効果（各systemはここを見る） ============
    public static int RpPerTurn => HasDedication(0) ? 5 : 0;
    public static int EmotionPerTurn => HasDedication(1) ? 8 : 0;
    public static int FoodBonus => (HasDedication(2) ? 2 : 0) + (CrisisPolicy == 0 ? -Mathf.CeilToInt(2 * CrisisPower) : 0);
    public static int DefenseBonus => HasDedication(3) ? 80 : 0;
    public static float ConquerMult => HasDedication(4) ? 1.25f : 1f;
    public static int UnhappyDelta => (HasDedication(5) ? -2 : 0) + (CrisisPolicy == 1 ? Mathf.CeilToInt(2 * CrisisPower) : 0);
    public static int MoveBonus => HasDedication(6) ? 1 : 0;
    public static float RegionDpMult => (HasDedication(7) ? 1.2f : 1f) * (CrisisPolicy == 2 ? 1f - 0.25f * CrisisPower : 1f);
    public static float BorderMult => (HasDedication(8) ? 1.4f : 1f) * (CrisisPolicy == 4 ? 1f - 0.5f * CrisisPower : 1f);
    public static float FameMult => HasDedication(9) ? 0.8f : 1f;
    public static float RivalPowerMult => CrisisPolicy == 3 ? 1f + 0.3f * CrisisPower : 1f;

    // ============ 毎ターンの判定 ============
    public static void TickTurn()
    {
        EnsureInit();
        // 誓約の毎ターン効果
        if (RpPerTurn > 0) ResearchState.AddRP(RpPerTurn);
        var et = EmotionTreeManager.Instance;
        if (et != null && EmotionPerTurn > 0)
            for (int i = 0; i < 4; i++) et.AddEmotion((EmotionTreeManager.Route)i, Mathf.Max(1, EmotionPerTurn / 4));

        // ⏳ 自然進行：偉業を取らなくても時代は進む（終焉でも進める＝最終判定の時計になる）
        Progress = Mathf.Min(Need, Progress + ProgressPerTurn);

        // 偉業の判定
        foreach (var t in triumphs)
        {
            if (t.era != Current || achieved.Contains(t.id)) continue;
            bool ok = false;
            try { ok = Value(t.kind) >= t.need; } catch { ok = false; }
            if (!ok) continue;
            achieved.Add(t.id);
            // 🧱 偉業から入る進行度は1時代あたり TriumphProgressCap まで（＝時代を飛ばせない）
            int gain = Mathf.Min(ProgressOf(t), Mathf.Max(0, TriumphProgressCap - triumphProgressThisEra));
            triumphProgressThisEra += gain;
            Progress = Mathf.Min(Need, Progress + gain);
            var res = DungeonResourceManager.Instance;
            if (res != null) { if (t.dp > 0) res.AddDP(t.dp); if (t.mat > 0) res.AddMaterial(t.mat); if (t.fame > 0) res.AddFame(t.fame); }
            if (t.rp > 0) ResearchState.AddRP(t.rp);
            if (et != null && t.emo > 0) for (int i = 0; i < 4; i++) et.AddEmotion((EmotionTreeManager.Route)i, Mathf.Max(1, t.emo / 4));
            AttributeSystem.AddPoint(t.axis, t.major ? 2 : 1, t.cond);   // 🎖️ レガシーの道 → 属性ポイント
            Debug.Log($"🏅『{(t.major ? "大偉業" : "偉業")}』{t.cond} を達成（時代の進行 +{ProgressOf(t)} → {Progress}/{Need}／{AttributeSystem.AxisName(t.axis)}+{(t.major ? 2 : 1)}）");
            NotifySystem.Push($"{(t.major ? "大偉業" : "偉業")}『{t.cond}』を達成（{AttributeSystem.AxisName(t.axis)}+{(t.major ? 2 : 1)}）",
                NotifySystem.Kind.Story);
            if (t.major) UnlockDedication();
        }

        // 災厄の始まり
        if (!CrisisActive && Progress >= CrisisAt && Current != Era.End)
        {
            CrisisActive = true;
            Debug.Log($"☄️『災厄』{EraName(Current)}の終わりが近い。**負の政策を1つ選ばなければならない**（時代パネルから）。");
            NotifySystem.Push($"<b>災厄</b>が近い ― {EraName(Current)}の終わり。地上メニュー『時代』で政策を1つ選ぶこと", NotifySystem.Kind.Danger);
        }
        // 時代の移り変わり（災厄の政策を選ぶまで進まない）
        if (Progress >= Need && Current != Era.End)
        {
            if (CrisisActive && crisisPolicy < 0) return;   // 政策を選ぶまで足止め
            Advance();
        }
    }

    private static void Advance()
    {
        // 🛡️ 危機を和らげて越えた時代は、次の時代に恩恵が出る（Civ VIIの「全員が対処すると恩恵」）
        if (crisisMitigated)
        {
            AttributeSystem.AddPoint(AttributeSystem.Axis.Culture, 1, "災厄を凌いだ");
            Debug.Log("🛡️『危機を越えた』手を打って被害を抑えた ― 文化の属性+1");
        }
        Current = (Era)((int)Current + 1);
        Progress = 0; triumphProgressThisEra = 0; CrisisActive = false; crisisPolicy = -1; crisisMitigated = false;
        KinRoster.OnEraChanged();   // 🎖️ 指揮官は時代を越える（昇進は残り、傷は癒える）
        NotifySystem.Push($"<b>── {EraName(Current)} ──</b>　{EraDesc(Current)}", NotifySystem.Kind.Story);
        Debug.Log($"⏳『時代が変わった』── {EraName(Current)} ──　{EraDesc(Current)}"
            + $"（世界水準+{TierBias:0.0}／誓約は{chosenDedications.Count}/{MaxChosen}枚）");
    }

    /// <summary>偉業ごとの達成条件。</summary>
    /// <summary>
    /// 偉業の判定。**種類ごとに「いまの値」を返すだけ**にして、比較は1箇所で行う。
    /// ⚠ ここに `try/catch` を掛けて呼んでいるのは、盤やマネージャがまだ無い瞬間があるため。
    /// </summary>
    /// <summary>条件の表示名（研究の解放条件テキストなどに使う）。</summary>
    public static string CondName(Cond c)
    {
        switch (c)
        {
            case Cond.Kill: return "冒険者の撃破";
            case Cond.TrapKill: return "罠でのとどめ";
            case Cond.MagicKill: return "魔法でのとどめ";
            case Cond.Boss: return "ボスの任命";
            case Cond.Celebrate: return "祝祭";
            case Cond.Forge: return "鍛えた武具";
            case Cond.ForgeHigh: return "上位の武具";
            case Cond.Districts: return "施設";
            case Cond.Discoveries: return "発見";
            case Cond.Floors: return "階層";
            case Cond.Owned: return "版図";
            case Cond.Territory: return "拠点の支配";
            case Cond.Scouts: return "斥候";
            case Cond.Settlements: return "拠点";
            case Cond.Cities: return "都市";
            case Cond.Research: return "修めた研究";
            case Cond.Evolved: return "進化させた配下";
            case Cond.MinionLevel: return "配下の最高Lv";
            case Cond.LordLevel: return "魔王Lv";
            case Cond.Materials: return "素材";
            case Cond.Dp: return "DP";
            case Cond.Relics: return "遺物";
            case Cond.Wonders: return "支配した遺産";
            case Cond.EmotionSpent: return "感情ツリー";
            case Cond.AttrPoints: return "属性ポイント";
            case Cond.Influence: return "威名";
            case Cond.Suzerain: return "従えた勢力";
            case Cond.KinNamed: return "与えた真名";
            case Cond.KinCount: return "眷属";
            case Cond.RivalsDead: return "排除した魔王";
            case Cond.Danger: return "危険度";
            default: return "条件";
        }
    }

    /// <summary>条件の「いまの値」。偉業の判定と、研究の解放条件の**両方**がここを見る。</summary>
    public static int CondValue(Cond c) => Value(c);

    private static int Value(Cond c)
    {
        var dl = DemonLord.Instance;
        var fm = DungeonFloorManager.Instance;
        var rel = RelicManager.Instance;
        var res = DungeonResourceManager.Instance;
        var et = EmotionTreeManager.Instance;
        switch (c)
        {
            case Cond.Kill:         return EurekaTracker.Count("kill");
            case Cond.TrapKill:     return EurekaTracker.Count("trapKill");
            case Cond.MagicKill:    return EurekaTracker.Count("magicKill");
            case Cond.Boss:         return EurekaTracker.Count("boss");
            case Cond.Celebrate:    return EurekaTracker.Count("celebrate");
            case Cond.Forge:        return EurekaTracker.Count("forge");
            case Cond.ForgeHigh:    return EurekaTracker.Count("forgeHigh");
            case Cond.Districts:    return EurekaTracker.Count("district");
            case Cond.KinNamed:     return EurekaTracker.Count("kin");
            case Cond.Discoveries:  return DiscoverySystem.Count;
            case Cond.Floors:       return fm != null ? fm.BuiltFloorCount : 0;
            case Cond.Owned:        return SurfaceMap.OwnedCount;
            case Cond.Territory:    return TerritoryTotal();
            case Cond.Scouts:       return ScoutSystem.Count;
            case Cond.Settlements:  return SettlementSystem.SettlementCount;
            case Cond.Cities:       return SettlementSystem.CityCount;
            case Cond.Research:     return ResearchState.ResearchedCount;
            case Cond.Evolved:      return EvolvedCount();
            case Cond.MinionLevel:  return TopMinionLevel();
            case Cond.LordLevel:    return dl != null ? dl.Level : 0;
            case Cond.Materials:    return res != null ? res.CraftMaterials : 0;
            case Cond.Dp:           return res != null ? res.DungeonPoints : 0;
            case Cond.Relics:       return rel != null ? rel.UnlockedCount : 0;
            case Cond.Wonders:      return WonderOwnedCount();
            case Cond.EmotionSpent: return et != null ? et.TotalSpent : 0;
            case Cond.AttrPoints:   return AttributeSystem.TotalPoints;
            case Cond.Influence:    return DiplomacySystem.Influence;
            case Cond.Suzerain:     return DiplomacySystem.SuzerainCount;
            case Cond.KinCount:     return KinRoster.Count;
            case Cond.RivalsDead:   return RivalLords.Count - RivalLords.AliveCount;
            case Cond.Danger:       return DangerRank.Level;
            default:                return 0;
        }
    }

    private static int EvolvedCount()
    {
        int n = 0;
        foreach (var v in MinionRoster.All) if (MinionEvolution.Depth(v.catalogIndex) > 0) n++;
        return n;
    }
    private static int WonderOwnedCount()
    {
        int n = 0;
        foreach (var r in SurfaceMap.All) if (r.owned && r.wonderIndex >= 0) n++;
        return n;
    }

    private static int TerritoryTotal()
    {
        int n = 0;
        foreach (var r in SurfaceMap.All) if (SettlementSystem.SettlementOf(r.id) >= 0) n++;
        return n;
    }
    private static int TopMinionLevel()
    {
        int m = 0; foreach (var v in MinionRoster.All) if (v.level > m) m = v.level; return m;
    }

    /// <summary>ヘッダ用の一行。</summary>
    public static string HeaderLine()
    {
        EnsureInit();
        string s = "<color=#c9a8ff>" + EraName(Current) + "</color> <size=90%>" + Progress + "/" + Need + "</size>";
        if (CrisisActive) s += crisisPolicy < 0
            ? "　<color=#e05a5a>☄災厄 ― 政策を選んでください</color>"
            : "　<color=#e08a3c>☄" + Crisis(crisisPolicy).jpName + "</color>";
        if (chosenDedications.Count > 0)
        {
            s += "　<color=#9c95b4>誓約</color>";
            foreach (int i in chosenDedications) s += " <color=" + Dedication(i).colorHex + ">" + Dedication(i).jpName + "</color>";
        }
        return s;
    }
}
