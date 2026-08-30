using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 📊 戦績（Phase F-23）。**1回の周（run）の記録**と、**通算の記録**。
///
/// ## なぜ要るか
/// これまでゲームが終わっても `GAME OVER` の4文字が出るだけで、**何をどこまでやったのかが残らなかった**
/// （しかもボタンが無く、閉じることすらできなかった）。次の周を始める理由が、どこにも無い状態だった。
///
/// ## 作り
/// - **今の周**：数える必要があるものだけ数える。残りは終わった瞬間に各systemから読む
///   （領地・研究・眷属・スコアは既にそれぞれの持ち主が正しく持っているので、二重に数えない）。
/// - **通算**：`PlayerPrefs`（周を越えて残る。セーブとは別。→ [[SaveSystem]] は1周の中身だけを持つ）。
/// - スコアは `VictorySystem.TotalScore(自分) × 難易度倍率 × 早さ` 。
///   ⚠ 早さの係数を入れないと「延々と粘るほど高い」になり、**勝ち急ぐ理由が消える**。
/// 関連: [[Achievements]] [[Difficulty]] [[VictorySystem]]。
/// </summary>
public static class RunStats
{
    // ============ 今の周（数えないと分からないものだけ） ============
    public static int Kills;              // 倒した冒険者
    public static int Escapes;            // 逃がした数
    public static int WavesSurvived;      // 凌いだ波
    public static int DeepestHeld;        // 守り切った最深フロア(1始まり)
    public static int DpEarned;           // 得たDPの累計
    public static int PeakRegions;        // 最大版図
    public static int CommandsUsed;       // 撃った号令
    public static int NemesisSlain;       // 🗡️ 討ち取った『名のある冒険者』（→ [[Nemesis]]）
    public static int Captured;           // ⛓️ 生け捕りにした数（→ [[Prison]]）
    public static int Converted;          // ⛓️ 転向させた数
    public static bool AnyDefenderLost;   // 一度でも防衛体を失ったか

    // ============ 🛡️ 捌く用意（W-2）============
    // ⚠⚠ **「いまの守りで捌けるか」を model で予想しない。** 攻撃力を足し合わせた強さ指標を作ると
    //   それは掛け算の軸を1本増やすのと同じで、しかも当たらない（→ [[difficulty-curve-orders]]）。
    //   代わりに **実際に捌いた事実**だけを覚えておき、次の見込みと比べる。
    //   ＝ 大招集の見込み「38体」に対して「あなたが一人も通さず凌いだ最大は 14 体」と並べれば、
    //     プレイヤーは自分で判断できる。予想ではなく**自分の戦績**だから外れない。
    /// <summary>**一人も通さず**（逃走0・魔王無傷・防衛体の損失0）凌いだ波の**最大来襲人数**。</summary>
    public static int BestWaveHeld;
    /// <summary>取り逃がしはあったが凌いだ波も含めた**最大来襲人数**。</summary>
    public static int BiggestWaveSurvived;
    /// <summary>直前の波の実績（来襲／撃破／逃走）。</summary>
    public static int LastWaveCame, LastWaveKilled, LastWaveEscaped;

    /// <summary>
    /// ⚠⚠ **直近の波で実際に捌いた人数**（新しい方の物差し・W-4 の反省）。
    ///
    /// <para>
    /// `BestWaveHeld`（＝一人も通さず凌いだ最大）は **片道の指標**だった。逃走が常態になると
    /// 二度と更新されず、3周目は **T4 の 7 から T14 まで一度も動かなかった**。
    /// その結果「捌く用意」が**永久に危険判定**になり、大招集が14ターン一度も出なかった
    /// ―― 無謀な死を、貧しい死に置き換えただけだった（→ [[playthrough-run3-t14]]）。
    /// </para>
    ///
    /// <para>
    /// ⚠ 直し方：**完璧さを要求しない**（逃走0を条件にしない）＝ 実際に倒した数で測る。
    ///   そして**窓で見る**（直近 `RecentWindow` 波）＝ 弱くなれば下がる。上下**両方**に動く。
    /// </para>
    /// </summary>
    public const int RecentWindow = 5;
    public static List<int> RecentKilled = new List<int>();
    public static List<int> RecentCame = new List<int>();

    /// <summary>直近の波で最も多く捌いた人数（＝いま確実に捌ける実績値）。</summary>
    public static int HeldRecently
    {
        get
        {
            int m = 0;
            if (RecentKilled != null) for (int i = 0; i < RecentKilled.Count; i++) if (RecentKilled[i] > m) m = RecentKilled[i];
            return m;
        }
    }
    /// <summary>直近の波の平均の来襲人数（見込みを言うときの足場）。</summary>
    public static int CameRecently
    {
        get
        {
            if (RecentCame == null || RecentCame.Count == 0) return 0;
            int s = 0; for (int i = 0; i < RecentCame.Count; i++) s += RecentCame[i];
            return Mathf.RoundToInt(s / (float)RecentCame.Count);
        }
    }

    /// <summary>波の締めに1回だけ（→ [[WaveReport]] の `EndWave`）。</summary>
    public static void NoteWaveOutcome(int came, int killed, int escaped, bool flawless)
    {
        LastWaveCame = came; LastWaveKilled = killed; LastWaveEscaped = escaped;
        if (came > BiggestWaveSurvived) BiggestWaveSurvived = came;
        if (flawless && escaped == 0 && came > BestWaveHeld) BestWaveHeld = came;

        if (RecentKilled == null) RecentKilled = new List<int>();
        if (RecentCame == null) RecentCame = new List<int>();
        RecentKilled.Add(killed); RecentCame.Add(came);
        while (RecentKilled.Count > RecentWindow) RecentKilled.RemoveAt(0);
        while (RecentCame.Count > RecentWindow) RecentCame.RemoveAt(0);
    }

    public static void ResetRun()
    {
        Kills = Escapes = WavesSurvived = DeepestHeld = DpEarned = PeakRegions = CommandsUsed = 0;
        NemesisSlain = Captured = Converted = 0;
        BestWaveHeld = BiggestWaveSurvived = 0;
        LastWaveCame = LastWaveKilled = LastWaveEscaped = 0;
        RecentKilled = new List<int>(); RecentCame = new List<int>();
        AnyDefenderLost = false;
        SaveSystem.PlaySeconds = 0f;
        committed = false;
    }

    public static void NoteKill() { Kills++; }
    public static void NoteEscape() { Escapes++; }
    public static void NoteDp(int amount) { if (amount > 0) DpEarned += amount; }
    public static void NoteCommand() { CommandsUsed++; }
    public static void NoteDefenderLost() { AnyDefenderLost = true; }
    public static void NoteNemesisSlain() { NemesisSlain++; }
    public static void NoteCaptured() { Captured++; }
    public static void NoteConverted() { Converted++; }
    public static void NoteWave(int deepestHeld1Based)
    {
        WavesSurvived++;
        if (deepestHeld1Based > DeepestHeld) DeepestHeld = deepestHeld1Based;
    }
    public static void NoteTurn()
    {
        int owned = SurfaceMap.CountOwnedBy(SurfaceMap.OwnerSelf);
        if (owned > PeakRegions) PeakRegions = owned;
    }

    // ============ 終わったときの成績 ============
    public static int Turn { get { return DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 1; } }

    /// <summary>⏱️ 早さの係数。25ターン以内なら満点、100ターンで0.6倍まで落ちる。</summary>
    public static float PaceMult
    {
        get { return Mathf.Clamp(1.15f - Turn * 0.006f, 0.6f, 1.0f); }
    }

    public static int BaseScore { get { return VictorySystem.TotalScore(VictorySystem.Self); } }

    public static int FinalScore(bool win)
    {
        float s = BaseScore * Difficulty.ScoreMult * PaceMult;
        if (win) s *= 1.5f;                 // 勝ち切りの上乗せ
        return Mathf.Max(0, Mathf.RoundToInt(s));
    }

    // ============ 通算（PlayerPrefs） ============
    private const string P = "dangeon3.stat.";
    private static bool committed;

    public static int Runs { get { return PlayerPrefs.GetInt(P + "runs", 0); } }
    public static int Wins { get { return PlayerPrefs.GetInt(P + "wins", 0); } }
    public static int BestScore { get { return PlayerPrefs.GetInt(P + "best", 0); } }
    public static int BestTurn { get { return PlayerPrefs.GetInt(P + "bestTurn", 0); } }
    public static int TotalKills { get { return PlayerPrefs.GetInt(P + "kills", 0); } }
    public static int TotalWaves { get { return PlayerPrefs.GetInt(P + "waves", 0); } }
    public static float TotalSeconds { get { return PlayerPrefs.GetFloat(P + "sec", 0f); } }
    public static int DailyBest(int seed) { return PlayerPrefs.GetInt(P + "daily." + seed, 0); }

    /// <summary>周が終わった。⚠ 二重に加算しないよう1周に1度だけ通す。</summary>
    public static void CommitRun(bool win)
    {
        if (committed) return;
        committed = true;
        int score = FinalScore(win);
        PlayerPrefs.SetInt(P + "runs", Runs + 1);
        if (win) PlayerPrefs.SetInt(P + "wins", Wins + 1);
        if (score > BestScore) PlayerPrefs.SetInt(P + "best", score);
        if (win && (BestTurn == 0 || Turn < BestTurn)) PlayerPrefs.SetInt(P + "bestTurn", Turn);
        PlayerPrefs.SetInt(P + "kills", TotalKills + Kills);
        PlayerPrefs.SetInt(P + "waves", TotalWaves + WavesSurvived);
        PlayerPrefs.SetFloat(P + "sec", TotalSeconds + SaveSystem.PlaySeconds);
        if (GameSetup.DailySeed)
        {
            int seed = GameSetup.Seed;
            if (score > DailyBest(seed)) PlayerPrefs.SetInt(P + "daily." + seed, score);
        }
        PlayerPrefs.Save();
        Achievements.CheckAll(true, win);
        Debug.Log("📊『周の終わり』" + (win ? "勝利" : "敗北") + " スコア " + score
            + "（素点" + BaseScore + " × 難易度" + Difficulty.ScoreMult + " × 早さ" + PaceMult.ToString("0.00") + "）");
    }

    public static void ClearAllRecords()
    {
        foreach (var k in new[] { "runs", "wins", "best", "bestTurn", "kills", "waves", "sec" })
            PlayerPrefs.DeleteKey(P + k);
        PlayerPrefs.Save();
    }
}
