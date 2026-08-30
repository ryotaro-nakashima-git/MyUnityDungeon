using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 📜 **波の決算**（③）。1つの防衛戦のあいだに何が起きたかを数え、終わったときに1枚にまとめる。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：波が終わると画面はそのまま地上へ切り替わり、
///   起きたことは**右のトーストが数枚流れて消えるだけ**だった。
///   何体倒したのか、何を持ち逃げされたのか、押した手が効いたのか ―― どれも残らない。
///   ＝**余韻がゼロ**で、次の波に持っていける学びも無い（→ [[dopamine-plan-and-how-tree]] の③）。
/// </para>
///
/// <para>
/// ⚠⚠ **報酬は1つも足さない。** ここは既に払われた物を**数え直して見せるだけ**。
///   演出の層に報いを持たせると倍率の軸が1本増える（→ [[difficulty-curve-orders]]）。
/// ⚠ 資源は**入りと出を別々に**数える。号令は戦闘中にDPを払うので、差だけ見ると
///   「稼ぎが少ない」ように見えて、**押した手が損に見える**。
/// ⚠ 数え始めは `StartBattlePhase`、締めは `EndBattlePhase` の**いちばん最後**。
///   大招集の見返りや研究点はウェーブの終わりに払われるので、そこまで数える。
/// </para>
///
/// 関連: [[DungeonTurnManager]]（開始と締め） [[RunStats]]（周ぜんたいの記録。こちらは1波だけ）。
/// </summary>
public static class WaveReport
{
    /// <summary>数えている最中か。⚠ これが偽のあいだは全部の Note が素通りする。</summary>
    public static bool Recording { get; private set; }
    /// <summary>1枚にまとまった直後だけ真（決算パネルを出してよいか）。</summary>
    public static bool Ready { get; private set; }

    // ── 見出し ──
    public static int Turn;
    public static float Seconds;
    public static int Came;              // 名簿の人数
    public static int DeepestFloor;      // 踏み込まれた最深（1始まり）

    // ── 人 ──
    public static int Killed, Escaped, Captured, Spared, NemesisSlain;

    // ── 実り（入り）と支払い（出）──
    public static int DpGained, DpSpent, MatGained, FameGained, RpGained, EmotionGained;

    // ── 代償 ──
    public static int GearLooted;        // 持ち逃げされた戦利品の合計
    public static float GearBefore, GearAfter;
    public static int DefendersLost;
    public static float LordHpBefore, LordHpAfter;

    // ── 手応え ──
    public static int BestCombo;
    /// <summary>撃った号令の回数と、最後に撃った名。⚠ 号令だけは**回数が意味を持つ**ので別に数える。</summary>
    public static int Commands; public static string LastCommand;

    /// <summary>この波で選んだ手（見出し／効き目）。⚠ 押した順に積む。</summary>
    public static readonly List<KeyValuePair<string, string>> Choices = new List<KeyValuePair<string, string>>();

    private static float startTime;
    private static int rpAtStart, emoAtStart;

    // ============ 開始と締め ============
    public static void BeginWave(int turn)
    {
        Turn = turn;
        Came = WaveRoster.Count;
        Killed = Escaped = Captured = Spared = NemesisSlain = 0;
        DpGained = DpSpent = MatGained = FameGained = RpGained = EmotionGained = 0;
        GearLooted = DefendersLost = BestCombo = Commands = 0;
        LastCommand = "";
        DeepestFloor = 1;
        GearBefore = GearAfter = LureEconomy.GearLevel;
        LordHpBefore = LordHpAfter = DemonLord.Instance != null ? DemonLord.Instance.HPRatio : 1f;
        Choices.Clear();
        startTime = Time.time;
        rpAtStart = ResearchState.RP;
        emoAtStart = EmotionSum();
        Seconds = 0f;
        Ready = false;
        Recording = true;

        // 🎯 いま立っている構えを最初に書き留める（押したのは準備フェーズなので、ここでしか拾えない）
        if (FeverSystem.Active) NoteChoice("大招集", "名簿を膨らませ、実りを厚くした");
        if (LureStance.Active) NoteChoice("泳がせ", "深手の相手を見逃して研究点に換える");
        if (RumorSystem.Active) NoteChoice("流言", RumorSystem.JobName(RumorSystem.Job) + "を寄せた");
        if (WardSystem.Selected >= 0) NoteChoice("備え", WardSystem.Get(WardSystem.Selected).jpName);
        if (LordStance.IsExpedition) NoteChoice("親征", "魔王が前へ出た");
    }

    /// <summary>締め。⚠ `EndBattlePhase` の**いちばん最後**で呼ぶ（波末の払い出しまで数えるため）。</summary>
    public static void EndWave()
    {
        if (!Recording) return;
        Recording = false;
        Seconds = Mathf.Max(0f, Time.time - startTime);
        GearAfter = LureEconomy.GearLevel;
        LordHpAfter = DemonLord.Instance != null ? DemonLord.Instance.HPRatio : 1f;
        RpGained = Mathf.Max(0, ResearchState.RP - rpAtStart);
        EmotionGained = Mathf.Max(0, EmotionSum() - emoAtStart);
        BestCombo = KillFeedback.WaveBest;
        var fm = DungeonFloorManager.Instance;
        DeepestFloor = fm != null ? fm.LastDeepestReached + 1 : 1;
        // 🛡️ 「捌く用意」の材料はここでだけ積む（→ [[RunStats]]・W-2）。
        //   ⚠ `Flawless` は上の3行（GearAfter/LordHpAfter/DefendersLost）が入ったあとでしか正しくない。
        RunStats.NoteWaveOutcome(Came, Killed, Escaped, Flawless);
        Ready = true;
    }

    /// <summary>🔄 新しい周のために畳む（`StartNewGame` から）。⚠ 敗北で波が途中終了すると
    /// `Recording` が立ったまま残るので、周をまたぐ前に必ず落とす。</summary>
    public static void Reset() { Recording = false; Ready = false; Choices.Clear(); }

    /// <summary>決算を見せ終えた。⚠ 見せないまま次の波に入ってもここを通る（残しておくと古い数字が出る）。</summary>
    public static void Consume() { Ready = false; }

    private static int EmotionSum()
    {
        var et = EmotionTreeManager.Instance;
        if (et == null) return 0;
        int s = 0;
        for (int i = 0; i < 4; i++) s += et.Pool((EmotionTreeManager.Route)i);
        return s;
    }

    // ============ 数える口（すべて Recording 中だけ効く）============
    public static void NoteKill(bool named) { if (!Recording) return; Killed++; if (named) NemesisSlain++; }
    public static void NoteEscape(float gear, bool wasSpared)
    {
        if (!Recording) return;
        Escaped++;
        if (wasSpared) Spared++;
        if (gear >= 1f) GearLooted += Mathf.RoundToInt(gear);
    }
    public static void NoteCaptured() { if (Recording) Captured++; }
    public static void NoteCommand(string jpName)
    {
        if (!Recording) return;
        Commands++; LastCommand = jpName;
        NoteChoice("号令", Commands == 1 ? jpName + " を撃った" : jpName + " ほか <b>" + Commands + "</b> 回");
    }
    public static void NoteDefenderLost() { if (Recording) DefendersLost++; }
    public static void NoteDp(int amount) { if (Recording && amount > 0) DpGained += amount; }
    public static void NoteDpSpent(int amount) { if (Recording && amount > 0) DpSpent += amount; }
    public static void NoteMaterial(int amount) { if (Recording && amount > 0) MatGained += amount; }
    public static void NoteFame(int amount) { if (Recording && amount > 0) FameGained += amount; }

    /// <summary>🎯 この波で効いた手を1行足す。⚠ 同じ見出しは重ねない（号令は回数だけ増やす）。</summary>
    public static void NoteChoice(string head, string note)
    {
        for (int i = 0; i < Choices.Count; i++)
            if (Choices[i].Key == head) { Choices[i] = new KeyValuePair<string, string>(head, note); return; }
        Choices.Add(new KeyValuePair<string, string>(head, note));
    }

    // ============ まとめの言葉 ============
    /// <summary>この波を守り切ったか（魔王に届いていない＝無傷）。</summary>
    public static bool Flawless { get { return LordHpAfter >= LordHpBefore - 0.001f && DefendersLost == 0; } }

    public static int DpNet { get { return DpGained - DpSpent; } }

    /// <summary>一言。⚠ **数字の言い換えにしない**。次の波で何を変えるかに繋がる言葉にする。</summary>
    public static string Verdict()
    {
        if (Came > 0 && Killed == 0) return "1体も倒せていない ― 置く物が足りないか、質が追いついていない";
        if (GearLooted >= 30) return "持ち逃げが多い ― 入口の手前で仕留めないと、次の波が強くなる";
        if (LordHpAfter < LordHpBefore - 0.15f) return "魔王まで届かれた ― 経路を伸ばすか、途中を厚くする";
        if (DefendersLost > 0) return "防衛体を失った ― 配下の質（鍛造・進化）に投資する頃合い";
        if (Escaped == 0 && Killed > 0) return "取り逃がしなし ― 世界の装備水準は上がらない";
        // ⚠ ここで「凌いだ」とだけ返すと**見出しと同じ言葉が2行続く**。
        //   何も起きなかった波こそ、まだ押していない手を指す（→ [[dopamine-plan-and-how-tree]]）。
        return "危なげなし ― もっと呼び込んでよい頃合い（大招集・流言）";
    }
}
