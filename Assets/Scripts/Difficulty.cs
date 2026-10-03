using UnityEngine;

/// <summary>
/// ⚖️ 難易度（Phase F-22）。**世界設定と一緒に、始める前に選ぶ**（[[GameSetup]]）。
///
/// ## 何を動かすか（＝何を動かさないか）
/// 難易度で**仕組みそのものは変えない**。動かすのは4つの掛け算だけにしてある。
/// - 冒険者の**質**（レベルの伸び）と**量**（湧く人数）
/// - 他魔王の**伸び**
/// - こちらの**取り分**（撃破DP・名声）
/// これ以外（研究の値段・建造費・配置枠）は据え置く。**同じ攻略が同じように通じる**ようにしたいから。
/// ⚠ [[difficulty-curve-orders]] の原則どおり、**掛け算の軸を増やさない**。
///
/// ## スコア倍率
/// 難しいほど戦績([[RunStats]])のスコアが伸びる。**低難易度で稼いだ記録が上位を占めない**ようにするため。
/// </summary>
public static class Difficulty
{
    public struct Def
    {
        public string jpName, desc, colorHex;
        public float advPower;    // 冒険者のレベルの伸び
        public float advCount;    // 湧く人数
        public float rivalGrow;   // 他魔王の伸び
        public float reward;      // 撃破DP・名声の取り分
        public float score;       // 戦績のスコア倍率
    }

    private static readonly Def[] defs =
    {
        // ⚠⚠ 2026-10-03：冒険者の強さ（advPower）と人数（advCount）の倍率は**全段 1.0**にそろえた。
        //   難しさは**節目の試練の強さ**（`TrialScale`・台帳 `diff.trial_scale.N`）1本で分ける。
        //   実測：人数を増やすと撃破のDPと名声も増え、負荷率2.5では基準がかえって早く勝った（人数は難しさのつまみとして効きが悪い）。
        //   各段は「基準プレイヤー（人並みに育てる自動運転）が10周中何周生き残るか」で定義する：安寧9〜10／標準7〜8／苛烈4〜6／絶望2〜3。
        D("安寧", "腰を据えて仕組みを覚えたいとき。節目の試練は穏やか。",             "#5cc47c", 1.00f, 1.00f, 0.70f, 1.15f, 0.6f),
        D("標準", "設計どおりの手応え。迷えばこれ。",                                 "#e3a94a", 1.00f, 1.00f, 1.00f, 1.00f, 1.0f),
        D("苛烈", "節目の試練が重い。深さと魔王の備えが本気で試される。",             "#e08a3c", 1.00f, 1.00f, 1.25f, 1.00f, 1.5f),
        D("絶望", "節目ごとに精鋭が来る。一度の取りこぼしが命取りになる。",           "#b0202b", 1.00f, 1.00f, 1.55f, 1.12f, 2.2f),
    };
    private static Def D(string n, string d, string c, float ap, float ac, float rg, float rw, float sc)
        => new Def { jpName = n, desc = d, colorHex = c, advPower = ap, advCount = ac, rivalGrow = rg, reward = rw, score = sc };

    public static int Count { get { return defs.Length; } }
    public static Def Get(int i) { return defs[Mathf.Clamp(i, 0, defs.Length - 1)]; }
    public static Def Current { get { return Get(GameSetup.DifficultyIdx); } }
    public static string CurrentName { get { return Current.jpName; } }

    // 各systemはここだけを見る
    public static float AdvPowerMult { get { return Current.advPower; } }
    public static float AdvCountMult { get { return Current.advCount; } }
    public static float RivalGrowMult { get { return Current.rivalGrow; } }
    public static float RewardMult { get { return Current.reward; } }
    public static float ScoreMult { get { return Current.score; } }

    private static readonly float[] TrialScaleDefault = { 0.4f, 1.0f, 1.6f, 2.6f };
    /// <summary>⚔️ 節目の試練の強さの倍率（標準＝1.0）。台帳 `diff.trial_scale.N`（→ [[WaveRoster]]）。</summary>
    public static float TrialScaleOf(int i)
    {
        i = Mathf.Clamp(i, 0, defs.Length - 1);
        return Balance.F("diff.trial_scale." + i, TrialScaleDefault[i]);
    }
    public static float TrialScale { get { return TrialScaleOf(GameSetup.DifficultyIdx); } }

    // ============ 🔓 解禁（CPU対戦・周を越えて残る＝PlayerPrefs） ============
    // 最初は安寧・標準だけ。標準で勝つと苛烈、苛烈で勝つと絶望が開く（ユーザー決定 2026-09-30）。
    private const string UnlockKey = "diff.unlocked";
    public static int UnlockedMax { get { return Mathf.Clamp(PlayerPrefs.GetInt(UnlockKey, 1), 1, defs.Length - 1); } }
    public static bool IsUnlocked(int i) { return i <= UnlockedMax; }
    /// <summary>この段を開くにはどこで勝てばよいか（表示用）。</summary>
    public static string UnlockHint(int i) { return i <= 0 ? "" : Get(i - 1).jpName + "で勝つと解禁"; }
    /// <summary>勝った。⚠ 計測中は開かない（遊ぶ人の記録を書き換えない）。</summary>
    public static void OnWin(int playedIdx)
    {
        if (MeasureMode.On) return;
        int next = playedIdx + 1;
        if (next >= defs.Length || next <= UnlockedMax) return;
        PlayerPrefs.SetInt(UnlockKey, next);
        PlayerPrefs.Save();
        NotifySystem.Push("<b>難易度『" + Get(next).jpName + "』が解禁された</b>", NotifySystem.Kind.Gain);
    }
}
