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
    public static bool AnyDefenderLost;   // 一度でも防衛体を失ったか

    public static void ResetRun()
    {
        Kills = Escapes = WavesSurvived = DeepestHeld = DpEarned = PeakRegions = CommandsUsed = 0;
        AnyDefenderLost = false;
        SaveSystem.PlaySeconds = 0f;
        committed = false;
    }

    public static void NoteKill() { Kills++; }
    public static void NoteEscape() { Escapes++; }
    public static void NoteDp(int amount) { if (amount > 0) DpEarned += amount; }
    public static void NoteCommand() { CommandsUsed++; }
    public static void NoteDefenderLost() { AnyDefenderLost = true; }
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
