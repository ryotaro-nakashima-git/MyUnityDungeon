using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 📜 **ギルドの布告**（⑥）。ギルドが「**何ターン後に何をするか**」を先に言う。
///
/// <para>
/// ⚠⚠ **なぜ要るか（実測）**：『次に起きること』の枠（S-1）は作ってあるのに、
///   **T8 の普通の状態で予定が0件**だった。読み込める予定が
///   敵軍の集結・時代の終わり・牢・訓練・大招集の休み ―― どれも**中盤以降にしか存在しない**ため。
///   Civ の「あと1ターン」が無いのは表示の問題ではなく、**序盤に予定された出来事が無いから**。
/// </para>
///
/// <para>
/// ⚠⚠ **強さの掛け算は1本も足さない**（→ [[difficulty-curve-orders]]）。
///   布告が変えるのは <b>人数（頻度）</b>と<b>顔ぶれ（構成）</b>だけで、
///   レベルにも装備にも係数を掛けない。すべて既にある操作口を使う：
///   `WaveRoster` の人数、`RumorSystem` と同じ職の寄せ方、`Nemesis` の抽選。
/// ⚠ **必ず先に言う**（2〜4ターン前）。言わずに起きるのはただの理不尽で、
///   言うから**備え・流言・大招集・掘削が「その日に向けた準備」になる**。
/// ⚠ 一度に抱えるのは1件だけ。2件並ぶと、どちらに備えるかではなく「もう無理」になる。
/// </para>
///
/// 関連: [[Foretell]]（出す先） [[WaveRoster]]（効く先） [[WardSystem]] [[RumorSystem]] [[Nemesis]]。
/// </summary>
public static class Proclamation
{
    public enum Kind
    {
        /// <summary>総力戦：その波の人数が増える（頻度の軸だけ）。</summary>
        AllOut = 0,
        /// <summary>一色の隊：その波の職がひとつに寄る（構成の軸だけ）。備えが刺さる日。</summary>
        Uniform = 1,
        /// <summary>賞金首：野に在る『名のある者』が必ず出る。</summary>
        Bounty = 2,
        /// <summary>静穏：その波は小さい。ただしギルドは支度を整える（世界の装備水準が上がる）。</summary>
        Lull = 3,
    }

    // ── ノブ ──
    /// <summary>最初の布告が出るターン。⚠ 序盤に予定が無いのが問題なので、早く出す。</summary>
    public const int FirstTurn = 2;
    /// <summary>次の布告までの間隔（ターン）。</summary>
    public const int Every = 4;
    public const int LeadMin = 2, LeadMax = 4;
    /// <summary>総力戦の人数。⚠ 大招集(×2.5)より弱く、しかも**こちらは選べない**ので控えめに。</summary>
    public const float AllOutMult = 1.6f;
    public const float LullMult = 0.55f;
    /// <summary>静穏の代償＝ギルドの支度。⚠ 新しい数字ではなく、既にある装備水準を動かすだけ。</summary>
    public const float LullGearGain = 6f;

    private static int dueTurn = -1;
    private static int kind = -1;
    private static int job = -1;       // Uniform のときの職
    private static int lastIssued = -100;
    /// <summary>静穏の代償を払ったターン。⚠ `Roll` は1ターンに何度も呼ばれうる（流言を撒くと引き直す）。</summary>
    private static int lullPaidTurn = -1;

    public static bool Pending { get { return dueTurn > 0; } }
    public static int DueTurn { get { return dueTurn; } }
    public static Kind Current { get { return (Kind)Mathf.Clamp(kind, 0, 3); } }
    /// <summary>いまのターンが布告の日か。⚠ `WaveRoster.Roll` より前に `TickTurn` が呼ばれている前提。</summary>
    public static bool IsToday(int turn) { return dueTurn == turn && kind >= 0; }

    public static void Reset() { dueTurn = -1; kind = -1; job = -1; lastIssued = -100; lullPaidTurn = -1; }

    /// <summary>
    /// ⏳ ターンの頭。期日が来ていれば発火し、来ていなければ次の布告を仕込む。
    /// ⚠⚠ **`WaveRoster.Roll` より前に呼ぶこと**（人数と顔ぶれに効くため）。
    /// </summary>
    public static void OnTurnStart(int turn)
    {
        // 期日を過ぎたら畳む（`WaveRoster` はこのターンの Roll で読み終えている）
        if (dueTurn > 0 && turn > dueTurn) { dueTurn = -1; kind = -1; job = -1; }

        if (dueTurn > 0) return;                       // ⚠ 一度に1件だけ
        if (turn < FirstTurn) return;
        if (turn - lastIssued < Every) return;

        lastIssued = turn;
        kind = PickKind(turn);
        job = Random.Range(0, 4);
        dueTurn = turn + Random.Range(LeadMin, LeadMax + 1);

        NotifySystem.Push("<b>ギルドの布告</b> ― " + Headline() + "（" + (dueTurn - turn) + " ターン後）",
            NotifySystem.Kind.Story);
        Debug.Log("📜『ギルドの布告』" + Headline() + " → 第" + dueTurn + "ターン");
    }

    /// <summary>⚠ 序盤は分かりやすい2種だけ。賞金首は野に名がいるときだけ。</summary>
    private static int PickKind(int turn)
    {
        var pool = new List<int> { (int)Kind.AllOut, (int)Kind.Uniform };
        if (turn >= 6) pool.Add((int)Kind.Lull);
        if (Nemesis.AtLargeCount > 0) pool.Add((int)Kind.Bounty);
        return pool[Random.Range(0, pool.Count)];
    }

    // ============ 効き目（すべて既にある操作口）============
    /// <summary>人数の倍率。⚠ ここだけが人数に触る。</summary>
    public static float CountMult(int turn)
    {
        if (!IsToday(turn)) return 1f;
        if (Current == Kind.AllOut) return AllOutMult;
        if (Current == Kind.Lull) return LullMult;
        return 1f;
    }

    /// <summary>顔ぶれの寄せ（-1＝寄せない）。⚠ `RumorSystem` と同じ「職だけ」の操作。</summary>
    public static int UniformJob(int turn)
    {
        return (IsToday(turn) && Current == Kind.Uniform) ? job : -1;
    }

    /// <summary>賞金首の日か（`Nemesis` の抽選を必ず当てる）。</summary>
    public static bool IsBountyDay(int turn) { return IsToday(turn) && Current == Kind.Bounty; }

    /// <summary>
    /// 🌫️ 静穏の代償。⚠ `WaveRoster.Roll` の中から**その日1回だけ**呼ぶ。
    /// 新しい数字は作らず、既にある世界の装備水準を上げるだけ。
    /// </summary>
    public static void ApplyLullCost(int turn)
    {
        if (!IsToday(turn) || Current != Kind.Lull) return;
        // ⚠⚠ **1ターンに1回だけ。** `WaveRoster.Roll` は流言を撒くと引き直されるので、
        //   守らないと撒くたびに装備水準が上がる（＝押すほど損をする、意味の分からない罰になる）。
        if (lullPaidTurn == turn) return;
        lullPaidTurn = turn;
        LureEconomy.OnGearEscaped(LullGearGain);
        NotifySystem.Push("<b>静穏</b> ― 波は小さいが、ギルドは支度を整えている（世界の装備水準が上がった）",
            NotifySystem.Kind.Loss);
    }

    // ============ 言葉 ============
    public static string Headline()
    {
        switch (Current)
        {
            case Kind.AllOut: return "総力戦 ― 大挙して押し寄せる";
            case Kind.Uniform: return WaveRoster.JobName((AdventurerAI.Job)job) + "の隊が編まれる";
            case Kind.Bounty: return "賞金首の狩り ― 名のある者が出る";
            default: return "静穏 ― 波は小さい（ギルドは支度を整える）";
        }
    }

    /// <summary>『次に起きること』と『先触れ』に出す1行。</summary>
    public static string Line()
    {
        switch (Current)
        {
            case Kind.AllOut: return "布告『総力戦』― 人数が <b>×" + AllOutMult.ToString("0.0") + "</b>";
            case Kind.Uniform: return "布告『" + WaveRoster.JobName((AdventurerAI.Job)job) + "の隊』― 顔ぶれが偏る（備えが刺さる）";
            case Kind.Bounty: return "布告『賞金首』― 名のある者が必ず出る";
            default: return "布告『静穏』― 人数が半分（ただし装備水準が上がる）";
        }
    }

    public static Foretell.Tone LineTone
    {
        get { return Current == Kind.Uniform ? Foretell.Tone.Neutral
                   : Current == Kind.Lull ? Foretell.Tone.Boon : Foretell.Tone.Danger; }
    }
}
