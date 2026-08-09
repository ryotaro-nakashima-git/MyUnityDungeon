using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🏅 実績（Phase F-24）。**周を越えて残る目標**。
///
/// ## なぜ要るか
/// 1周を終えても「次に何を目指すか」が無かった。実績は
/// **まだ触っていない仕組みへ手を伸ばす理由**になる（原作の設定を使い切るための導線でもある）。
///
/// ## 作り
/// - 条件は `Func&lt;bool&gt;`。**毎ターンと周の終わりに見る**だけ（常時監視しない＝安い）。
/// - 解除は `PlayerPrefs`（[[SaveSystem]] は1周の中身しか持たない。実績は周を越える）。
/// - **解除数が形見の枠を増やす**（[[NarrativeSystem]] の2枠 → 最大3枠）。これが周回の見返り。
/// 関連: [[RunStats]] [[Difficulty]]。
/// </summary>
public static class Achievements
{
    public struct Def
    {
        public string id, jpName, how;
        public bool hidden;               // 達成するまで内容を伏せる
        public System.Func<bool> cond;
    }

    private static readonly List<Def> defs = new List<Def>();
    private static void A(string id, string name, string how, System.Func<bool> cond, bool hidden = false)
        => defs.Add(new Def { id = id, jpName = name, how = how, cond = cond, hidden = hidden });

    private static bool built;
    private static void Build()
    {
        if (built) return;
        built = true;

        // ── 迷宮 ──
        A("first_wave", "最初の波", "防衛戦を1回凌ぐ", () => RunStats.WavesSurvived >= 1);
        A("wave10", "十波の主", "1周で10回の波を凌ぐ", () => RunStats.WavesSurvived >= 10);
        A("wave25", "揺るがぬ迷宮", "1周で25回の波を凌ぐ", () => RunStats.WavesSurvived >= 25);
        A("kill100", "血の帳簿", "通算で冒険者を100体倒す", () => RunStats.TotalKills + RunStats.Kills >= 100);
        A("kill1000", "深淵の顎", "通算で冒険者を1000体倒す", () => RunStats.TotalKills + RunStats.Kills >= 1000);
        A("flawless", "無傷の防衛", "防衛体を1体も失わずに10波を凌ぐ",
            () => RunStats.WavesSurvived >= 10 && !RunStats.AnyDefenderLost);
        A("deep3", "三層の底", "B3F まで踏ませて守り切る", () => RunStats.DeepestHeld >= 3);
        A("command", "魔王の号令", "1周で号令を10回撃つ", () => RunStats.CommandsUsed >= 10);
        A("lure", "泳がせの妙", "1周で冒険者を50人逃がす", () => RunStats.Escapes >= 50);

        // ── 地上 ──
        A("first_kin", "真名を与える", "眷属を1体つくる", () => KinRoster.All.Count >= 1);
        A("kin5", "五人の腹心", "眷属を5体つくる", () => KinRoster.All.Count >= 5);
        A("region10", "版図十", "領地を10タイル持つ", () => RunStats.PeakRegions >= 10);
        A("region40", "版図四十", "領地を40タイル持つ", () => RunStats.PeakRegions >= 40);
        A("city", "都市の主", "都市を1つ持つ", () => SettlementSystem.CityCount >= 1);
        A("suzerain", "宗主国", "独立勢力を1つ従える", () => DiplomacySystem.SuzerainCount >= 1);
        A("rival", "魔王殺し", "他の魔王を1人排除する", () => RivalLords.Count - RivalLords.AliveCount >= 1);

        // ── 育てる ──
        A("research20", "書架を満たす", "研究を20ノード進める", () => ResearchState.ResearchedCount >= 20);
        A("research40", "叡智の樹", "研究を40ノード進める", () => ResearchState.ResearchedCount >= 40);
        A("era3", "終焉まで", "終焉の時代に至る", () => EraSystem.Current == EraSystem.Era.End);
        A("policy", "統治の形", "政体を1度変える", () => PolicySystem.GovIndex > 0);
        A("relic3", "遺物収集", "遺物を3つ解放する", () => RelicUnlocked() >= 3);

        // ── 周の結末 ──
        A("clear", "世界を塗り替える", "1度勝ち切る", () => RunStats.Wins >= 1);
        A("clear_hard", "苛烈を制す", "『苛烈』以上で勝ち切る",
            () => RunStats.Wins >= 1 && GameSetup.DifficultyIdx >= 2 && lastWin);
        A("clear_fast", "疾風の統治", "40ターン以内に勝ち切る",
            () => lastWin && RunStats.Turn <= 40);
        A("score5000", "戦績五千", "スコア5000を出す", () => RunStats.BestScore >= 5000);
        A("daily", "今日の世界", "日替わりの世界を1度遊ぶ", () => GameSetup.DailySeed && RunStats.WavesSurvived >= 1);
        A("runs5", "積み重ね", "5周遊ぶ", () => RunStats.Runs >= 5);

        // ── 隠し ──
        A("no_trap", "素手の防衛", "罠を1つも置かずに5波を凌ぐ",
            () => RunStats.WavesSurvived >= 5 && NoTrapPlaced(), true);
        A("despair", "絶望を越えて", "『絶望』で勝ち切る",
            () => lastWin && GameSetup.DifficultyIdx >= 3, true);
    }

    private static bool lastWin;

    private static int RelicUnlocked()
    {
        var r = RelicManager.Instance;
        if (r == null || r.Catalog == null) return 0;
        int n = 0;
        for (int i = 0; i < r.Catalog.Count; i++) if (r.IsUnlocked(i)) n++;
        return n;
    }

    private static bool NoTrapPlaced()
    {
        var fm = DungeonFeatureManager.Instance;
        return fm != null && fm.TrapsEverPlaced == 0;
    }

    public static int Count { get { Build(); return defs.Count; } }
    public static Def Get(int i) { Build(); return defs[Mathf.Clamp(i, 0, defs.Count - 1)]; }

    // ============ 解除（PlayerPrefs） ============
    private const string P = "dangeon3.ach.";
    public static bool IsUnlocked(int i) { Build(); return PlayerPrefs.GetInt(P + defs[i].id, 0) == 1; }
    public static int UnlockedCount
    {
        get { Build(); int n = 0; for (int i = 0; i < defs.Count; i++) if (IsUnlocked(i)) n++; return n; }
    }

    /// <summary>周の途中（ターン頭）と、周の終わりに呼ぶ。</summary>
    public static void CheckAll(bool runEnded = false, bool win = false)
    {
        Build();
        if (runEnded) lastWin = win;
        bool any = false;
        for (int i = 0; i < defs.Count; i++)
        {
            if (IsUnlocked(i)) continue;
            bool ok = false;
            try { ok = defs[i].cond(); } catch { ok = false; }
            if (!ok) continue;
            PlayerPrefs.SetInt(P + defs[i].id, 1);
            any = true;
            NotifySystem.Push("🏅 <b>実績</b> ― " + defs[i].jpName, NotifySystem.Kind.Story);
            Debug.Log("🏅『実績』" + defs[i].jpName + " ― " + defs[i].how);
        }
        if (any) PlayerPrefs.Save();
    }

    /// <summary>🕯️ 形見の枠は実績で増える（周回の見返り）。→ [[NarrativeSystem]]</summary>
    public static int MementoSlots
    {
        get { return UnlockedCount >= 12 ? 3 : 2; }
    }

    public static void ClearAll()
    {
        Build();
        for (int i = 0; i < defs.Count; i++) PlayerPrefs.DeleteKey(P + defs[i].id);
        PlayerPrefs.Save();
    }
}
