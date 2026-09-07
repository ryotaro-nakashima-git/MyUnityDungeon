using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 誘導経済（原作シオンの情報操作／CDO2の集客）の心臓部＝『世界の脅威度』を持つ。
///
/// 泳がせfarmingの核: 冒険者を"逃がす"と噂が広まり(Fame↑)、世界の脅威度が上がる。
/// 脅威度が上がるほど、来る勇者は多く・強くなり、撃破報酬も増える（＝需要を作るほど儲かるが敵も育つ両刃）。
/// - 全滅させる = 安全だが脅威度が上がらず稼ぎも伸びない
/// - 泳がせる   = 稼げるが脅威度が上がりウェーブが厳しくなる
///
/// 静的保持（プレイセッション内。ドメインリロードで初期化＝新規プレイは脅威度1.0）。装備ドロップ両刃(gear)は後続で拡張。
/// 関連: [[internal-affairs-design]] AdventurerAI(逃走/撃破フック) / DungeonAdventurerSpawner(ウェーブ数) / GameUIManager(HUD)。
/// </summary>
public static class LureEconomy
{
    private static float threat = 1f;      // 世界の脅威度（1.0スタート）
    private static float gearLevel = 0f;   // 世界の装備水準（0スタート。逃げ切った装備を追いかける遅い値）
    private const float MinThreat = 1f, MaxThreat = 6f;

    // 🎁 装備水準の作り直し（→ [[gear-level-rework]]／`dev_log.md` 続き36）
    // ⚠⚠ **旧仕様は「逃げた人数ぶんの和」**（`gearLevel += 持ち出し量 × 0.5`）だった。
    //   和なので逃げた人数に比例し、人数はターンに比例する ＝ **係数を下げても同じ壁が来る**
    //   （実測 26 → 53.8 → 59.3 → 99.3 の3ターンで死亡）。
    //   いまは「逃げ切った者の等級の**第3四分位数**」を目標にして、**歩幅を上限に追いかける**。
    /// <summary>目盛りの換算。`gearLevel / GearPerGrade` が世界の等級。</summary>
    public const float GearPerGrade = 50f;
    /// <summary>1波で動ける幅＝**0.12 等級／波**。等級+1 に約8波（旧は1〜2波）。</summary>
    private const float StepPerWave = 0.12f * GearPerGrade;
    /// <summary>🌍 世界が勝手に武装する下限の上限＝等級4（銀）。これ以上は**撒いた者にしか届かない**。</summary>
    private const float FloorCapGrade = 4f;

    /// <summary>この波で逃げ切った者の装備等級（カタログ索引）。⚠ 波の終わりに畳む。</summary>
    private static List<int> escapedGrades = new List<int>();
    /// <summary>この波に入場した人数。⚠ 速さは人数ではなく**割合**で決まる（下の `SettleWave`）。</summary>
    private static int enteredThisWave;
    /// <summary>📜 布告『静穏』などが積み上げる、下限への上乗せ（＝ギルドが自前で整えた支度）。</summary>
    private static float floorBonus;

    // チューニング
    // ⚖️ 脅威度は『量(人数)と質(ランク)と旨味(報酬)』を動かす軸にする。
    //    以前は HeroHpMult が脅威度そのもの(最大×6)で、ランク・Lv・装備と掛け算になり
    //    ひとつの操作(逃がす)が4つの倍率を同時に跳ね上げていた＝崖の主因。直接倍率は大きく削る。
    private const float EscapeThreatBase = 0.03f; // 逃走1体あたりの脅威度上昇（基礎）
    private const int   EscapeFame = 25;          // 逃走で広まる噂（Fame加算）
    private const float HpPerThreat = 0.15f;      // 脅威度→勇者HP倍率の伸び（旧: 脅威度そのもの＝1.0）
    private const float AtkPerThreat = 0.20f;     // 脅威度→勇者攻撃倍率の伸び
    private const float WavePerThreat = 2f;       // 脅威度→追加ウェーブ数
    private const float RevenuePerThreat = 0.7f;  // 脅威度→撃破DP倍率の伸び（リスクを下げた分、旨味は上げる）
    private const float GearSpreadFrac = 0.5f;    // 🗡️ 因縁を討ったときの引き下げ（撒いたときと同じ係数で戻す）
    private const float HpPerGear = 0.02f;        // 装備水準1あたり勇者HP+2%
    private const float AtkPerGear = 0.03f;       // 装備水準1あたり勇者攻撃+3%

    public static float Threat => threat;
    public static string ThreatLabel => threat.ToString("0.00");
    public static float GearLevel => gearLevel;
    /// <summary>⚠ 表示は**等級**に統一する。0-200 の生の目盛りはプレイヤーの語彙ではない。</summary>
    public static string GearLabel => GradeText(gearLevel);
    /// <summary>その水準を「等級2.3」の形で読む。</summary>
    public static string GradeText(float level) => "等級" + (level / GearPerGrade).ToString("0.0");

    /// <summary>いまの世界の装備等級（小数）。`GradeFromWorld` と『先触れ』が読む。</summary>
    public static float GearGrade => gearLevel / GearPerGrade;
    /// <summary>🎁 世界の装備水準が到達できる<b>上限の等級</b>（カタログ索引）。
    /// ＝「**撒いた最高等級**」。ただし世界が自前で武装する下限ぶんは常に届く。</summary>
    public static int WorldGradeCap
        => Mathf.Clamp(Mathf.Max(Mathf.RoundToInt(FloorCapGrade) - 1, TreasureGrades.SeededMaxCatalog),
                       0, EquipmentCatalog.MaxGrade);

    public static void Reset()
    {
        threat = MinThreat; gearLevel = 0f;
        escapedGrades = new List<int>(); enteredThisWave = 0; floorBonus = 0f;
    }

    /// <summary>冒険者が"逃走"して生還したとき（＝噂を広め、次はより強く戻る）。</summary>
    public static void OnHeroEscaped(int heroLevel)
    {
        // ✨ 感情の複合ノード『恐怖支配』：噂の広がり(脅威度上昇)が緩やかになる
        float growth = EmotionTreeManager.Instance != null ? EmotionTreeManager.Instance.ThreatGrowthMult : 1f;
        if (RelicManager.Instance != null) growth *= RelicManager.Instance.ThreatGrowthMult; // 🏺 英雄の首飾り＝旨いが噂も速い
        growth *= SyncretismSystem.ThreatRiseMult;   // 🜏 習合『龍種の威』＝噂が広がりにくい
        threat = Mathf.Min(MaxThreat, threat + EscapeThreatBase * (1f + heroLevel * 0.01f) * growth); // 高レベルほど噂が大きい
        if (DungeonResourceManager.Instance != null) DungeonResourceManager.Instance.AddFame(EscapeFame);
    }

    /// <summary>
    /// 🗡️⛓️ 噂を鎮める。因縁の相手を**討ち取った**／**生きたまま消した**ときに呼ぶ。
    /// 「還れなかった者がいる」という事実だけが、広まった噂を打ち消せる（→ [[Nemesis]] [[Prison]]）。
    /// ⚠ 下限は `MinThreat`。ここを割ると誘導経済の式が意味を失う。
    /// </summary>
    public static void CalmDown(float amount)
    {
        if (amount <= 0f) return;
        threat = Mathf.Max(MinThreat, threat - amount);
    }

    // ============ 🎁 波ごとの決算（和をやめた本体） ============

    /// <summary>この波に1人入った。⚠ 侵入者（遠征先の守り）は数えない。</summary>
    public static void NoteEntered() { enteredThisWave++; }

    /// <summary>波の頭で数え直す。⚠ 敗北などで `SettleWave` を通らずに波が終わると、
    /// 前の波の入場者が残って**割合（逃げ切り÷入場）が薄まる**。</summary>
    public static void ResetWaveCounters() { escapedGrades.Clear(); enteredThisWave = 0; }

    /// <summary>逃げ切った者が着ていた装備の等級（カタログ索引）を控える。</summary>
    public static void NoteEscapedGrade(int catalogGrade)
    {
        escapedGrades.Add(Mathf.Clamp(catalogGrade, 0, EquipmentCatalog.MaxGrade));
    }

    /// <summary>『先触れ』などが読む、この波の入場人数と逃げ切り人数。</summary>
    public static int EnteredThisWave => enteredThisWave;
    public static int EscapedThisWave => escapedGrades.Count;

    /// <summary>
    /// 🌍 <b>世界が勝手に武装する下限</b>（名声と時代）。
    /// ⚠⚠ 無いと「**撒かない**」が無条件の最適解になる（撒かなければ永久に敵が強くならない）。
    ///   撒くのは「この下限を**自分から追い越す**」行為でなければ選択にならない。
    /// ⚠ 伸びは旧仕様のおよそ 1/5。名声は対数（→ [[difficulty-curve-orders]]）。
    /// </summary>
    public static float FloorLevel
    {
        get
        {
            int fame = DungeonResourceManager.Instance != null ? DungeonResourceManager.Instance.DungeonFame : 0;
            int era = (int)EraSystem.Current;
            float f = 14f * Mathf.Log(1f + Mathf.Max(0, fame) / 50f) + era * 12f + floorBonus;
            return Mathf.Clamp(f, 0f, FloorCapGrade * GearPerGrade);
        }
    }

    /// <summary>📜 布告『静穏』＝ギルドが自前で支度を整えた。<b>下限そのものを持ち上げる</b>。</summary>
    public static void RaiseFloor(float amount)
    {
        if (amount <= 0f) return;
        floorBonus = Mathf.Min(FloorCapGrade * GearPerGrade, floorBonus + amount);
    }

    /// <summary>
    /// 📜 波の終わりに1回だけ呼ぶ（`WaveReport.EndWave` の頭）。⚠ ここが唯一の「上げ」の入口。
    ///
    /// <para>
    /// 目標＝<b>逃げ切った者の等級の第3四分位数</b>（nearest-rank）。
    /// ⚠ 最高値だと**外れ値1人**に引きずられて「1人も逃がすな」に逆戻りする。
    ///   平均だと弱い逃走者が薄め、しかも人数が効いて**和の病気に戻る**。
    /// </para>
    /// <para>
    /// 速さ＝<b>歩幅 ×（逃げ切った人数 ÷ 入場した人数）</b>。
    /// ⚠⚠ 絶対数にすると、波が大きくなる終盤ほど自動的に全速になり、また「ターンが上げている」に戻る。
    /// ⚠⚠ <b>割合を掛けるのは上げるときだけ。</b>下げにも掛けると全滅させた波が `0 ÷ n = 0` で
    ///   世界が凍り、「逃がさなければ下がる」という唯一の自然な下げ道が死ぬ。
    /// </para>
    /// </summary>
    public static void SettleWave()
    {
        float floorLv = FloorLevel;
        float cap = (WorldGradeCap + 1) * GearPerGrade;   // 撒いた最高等級ぶんまで

        // ① 逃げ切りが作る目標＝第3四分位
        float chase = 0f;
        if (escapedGrades.Count > 0)
        {
            escapedGrades.Sort();
            int idx = Mathf.Clamp(Mathf.CeilToInt(0.75f * escapedGrades.Count) - 1, 0, escapedGrades.Count - 1);
            chase = escapedGrades[idx] * GearPerGrade;
        }
        chase = Mathf.Min(chase, cap);

        // ② 世界が自前で武装する下限。⚠ 上限（撒いた等級）より優先する。
        float target = Mathf.Max(chase, floorLv);

        // ⚠⚠ **割合で絞るのは「逃げ切りが押し上げているとき」だけ。**
        //   下限に追いつく動きにまで割合を掛けると、1人も逃がさない波では速さが 0 になり、
        //   **下限に永久に届かない**＝「撒かず・逃がさず」が無条件の最適解に戻る（実測で踏んだ）。
        //   下限は誰が何をしようとギルドが勝手に整えるものなので、満速で追いつく。
        bool pushedByFloor = floorLv >= chase;
        float ratio = enteredThisWave > 0 ? escapedGrades.Count / (float)enteredThisWave : 0f;
        float speed = pushedByFloor ? StepPerWave : StepPerWave * Mathf.Clamp01(ratio);

        if (target > gearLevel) gearLevel = Mathf.Min(target, gearLevel + speed);
        else                    gearLevel = Mathf.Max(target, gearLevel - StepPerWave);   // ⚠ 下げに割合は掛けない
        gearLevel = Mathf.Max(0f, gearLevel);

        escapedGrades.Clear();
        enteredThisWave = 0;
    }

    /// <summary>略奪者を"倒した"とき＝戦利品を素材として回収できる（武装拡散を防ぐ）。</summary>
    public static int GearRecoverMaterials(float carriedGear) => Mathf.Max(0, Mathf.RoundToInt(carriedGear));

    /// <summary>
    /// 🎁 **撒かれた装備を世界から回収する**（G-2）。返り値＝実際に下がったぶん。
    ///
    /// ⚠⚠ `gearLevel` は**これまで上がる一方だった**（`OnGearEscaped` と `Reset` しか無い）。
    ///   取り逃がしが積み上がるだけで、**取り返す道が1本も無かった**。
    ///   因縁を討ち取る＝そいつが世界に撒いた装備を回収する、という形で唯一の下げ道を通す。
    /// ⚠ 撒いたときと**同じ係数** `GearSpreadFrac` で戻す（撒いた量より多く回収しない）。
    ///
    /// ⚠⚠ <b>下限は割れない。</b>回収できるのは「こちらの迷宮から出ていった物」だけで、
    ///   世界が自前で整えた支度（名声と時代の下限）まで取り返せるわけではない。
    ///   実測で踏んだ穴：ここが 0 まで下げられたので、**全員討ち取って因縁も討つと世界水準が
    ///   永久に 0 のまま**になり、下限が一度も効かなかった（＝「撒かず・逃がさず」が無条件の最適解）。
    ///   ⚠ 既に下限を下回っているとき（追いつく途中）に、この関数が値を**上げてはいけない**ので
    ///     床は `min(いまの値, 下限)`。
    /// </summary>
    public static float RecoverGear(float hoard)
    {
        if (hoard <= 0f) return 0f;
        float before = gearLevel;
        float bottom = Mathf.Min(gearLevel, FloorLevel);
        gearLevel = Mathf.Max(bottom, gearLevel - hoard * GearSpreadFrac);
        return before - gearLevel;
    }

    // 脅威度→勇者強度（スポーン時に適用）。・装備水準(gearLevel)の効果は EquipmentCatalog の武具グレードで表現するため、
    //   ここは脅威度のみ（二重計上を防ぐ）。gearLevel は AdventurerAI が装備グレード選択に使う。
    public static float HeroHpMult => (1f + (threat - 1f) * HpPerThreat);
    public static float HeroAtkMult => (1f + (threat - 1f) * AtkPerThreat);
    // 脅威度→ウェーブ増員
    public static int ExtraWaveCount => Mathf.FloorToInt((threat - 1f) * WavePerThreat);
    // 脅威度→撃破報酬（強い勇者ほど旨味）
    public static float RevenueMult => 1f + (threat - 1f) * RevenuePerThreat;
}
