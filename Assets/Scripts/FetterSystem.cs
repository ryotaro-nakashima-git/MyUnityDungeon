using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⛓️ <b>枷</b>（段6・ユーザー承認 2026-10-03・案 https://claude.ai/artifact/KpDbV8Kwg8vTZqhZQ6is52）。
///
/// <para>
/// 難易度（周の始めに一度だけ決める世界の厳しさ）の上に、<b>時代ごとに自分で積む圧</b>。
/// 誓約が「この時代に得る力」なら、枷は「この時代に背負う重さ」。地上の画面の「時代」の板で、災厄と誓約のあいだに並ぶ。
/// </para>
/// <list type="bullet">
/// <item>選べるのは<b>時代の頭のターンだけ</b>（最初の時代はT1）。その時代のあいだは外せない（楽な場面でだけ背負う抜け道を塞ぐ）。</item>
/// <item>最大3枚。1枚ごとに重さ1〜3。</item>
/// <item>見返り（B）：時代を<b>背負い切ったら</b>（魔王が生きて次の時代に入ったら）研究点 +4×W・DP +300×W・スコア倍率 +0.05×W（W＝その時代の重さの合計）。</item>
/// <item>どれも既存のつまみを動かすだけ（新しい仕組みは作らない）。効き目は各systemがここを読む。</item>
/// </list>
/// ⚠ 静的フィールドはセーブに載る（readonly はカタログ＝保存しない）。新しい周では `Reset`。
/// </summary>
public static class FetterSystem
{
    public struct Def { public string jpName, desc; public int weight; }
    private static readonly Def[] defs =
    {
        D("早鐘の枷", "節目の試練が 5ターンごと → 4ターンごと", 2),
        D("精鋭の枷", "試練の一行の強さ ×1.3", 2),
        D("漏洩の枷", "逃げ帰った者の地図の埋まりが1.5倍、薄れ方が半分", 1),
        D("噂火の枷", "名声・脅威度による冒険者の格の上乗せの上限 +1段", 1),
        D("細糧の枷", "冒険者を倒したときの DP −25%", 1),
        D("封土の枷", "この時代、階層を広げられない", 2),
        D("浅瀬の枷", "この時代、階層を足せない", 3),
        D("鈍令の枷", "号令の待ち時間 +50%", 1),
        D("薄殻の枷", "魔王の殻の戻り −30%", 2),
        D("群勢の枷", "来る人数 +20%", 1),
        D("無援の枷", "倒れた配下を起こす費用 ×2", 1),
        D("闇路の枷", "先触れ（次の波の予告）が見えない", 1),
    };
    private static Def D(string n, string d, int w) => new Def { jpName = n, desc = d, weight = w };

    public const int MaxChosen = 3;
    public const int Hasty = 0, Elite = 1, Leak = 2, RumorFire = 3, Lean = 4, NoWiden = 5, NoDeepen = 6,
        SlowCommand = 7, ThinShell = 8, Horde = 9, NoAid = 10, Dark = 11;

    public static int Count => defs.Length;
    public static Def Get(int i) => defs[Mathf.Clamp(i, 0, defs.Length - 1)];

    private static List<int> chosen;
    private static int eraStartTurn = 1;
    /// <summary>周の通算のスコア倍率の上乗せ（背負い切った時代の重さ × 0.05 の合計）。</summary>
    private static float scoreBonus;

    private static void EnsureInit() { if (chosen == null) chosen = new List<int>(); }
    public static IReadOnlyList<int> Chosen { get { EnsureInit(); return chosen; } }
    public static bool Has(int i) { EnsureInit(); return chosen.Contains(i); }
    public static int Weight { get { EnsureInit(); int w = 0; foreach (int i in chosen) w += Get(i).weight; return w; } }
    public static float ScoreBonus => scoreBonus;

    /// <summary>いま枷を選べるか（時代の頭のターンだけ）。</summary>
    public static bool IsOpen
    {
        get
        {
            var t = DungeonTurnManager.Instance;
            return t != null && t.CurrentTurn == eraStartTurn;
        }
    }

    public static bool TryToggle(int i, out string why)
    {
        EnsureInit(); why = "";
        if (i < 0 || i >= defs.Length) { why = "その枷は無い"; return false; }
        if (!IsOpen) { why = "枷を選べるのは時代の頭のターンだけ"; return false; }
        if (chosen.Contains(i)) { chosen.Remove(i); return true; }
        if (chosen.Count >= MaxChosen) { why = "枷は" + MaxChosen + "枚まで（どれかを外す）"; return false; }
        chosen.Add(i);
        return true;
    }

    /// <summary>
    /// ⏳ 時代が変わった（`EraSystem.Advance` から）。前の時代を背負い切った見返りを払い、枷を外して、新しい時代の窓を開く。
    /// </summary>
    public static void OnEraChanged(int newEraStartTurn)
    {
        EnsureInit();
        int w = Weight;
        if (w > 0)
        {
            int rp = Balance.I("fetter.reward_rp_per_weight", 4) * w;
            int dp = Balance.I("fetter.reward_dp_per_weight", 300) * w;
            float sc = Balance.F("fetter.score_per_weight", 0.05f) * w;
            ResearchState.AddRP(rp);
            var res = DungeonResourceManager.Instance;
            if (res != null) res.AddDP(dp);
            scoreBonus += sc;
            NotifySystem.Push("<b>枷を背負い切った</b>（重さ " + w + "）― 研究点 +" + rp + "・DP +" + dp + "・スコア ×+" + sc.ToString("0.00"),
                NotifySystem.Kind.Gain);
        }
        chosen.Clear();
        eraStartTurn = newEraStartTurn;
        NotifySystem.Push("<b>新しい時代の頭</b> ― このターンのうちに枷を選べる（地上の画面の『時代』の板）", NotifySystem.Kind.Story);
    }

    /// <summary>決着したとき、最後の時代の枷もスコアだけは数える（魔王が生きていれば）。</summary>
    public static void OnDecided(bool lordAlive)
    {
        EnsureInit();
        if (lordAlive && Weight > 0) scoreBonus += Balance.F("fetter.score_per_weight", 0.05f) * Weight;
    }

    // ============ 効き目（各systemはここを読む） ============
    public static int TrialEveryDelta => Has(Hasty) ? -1 : 0;
    public static float TrialScaleMult => Has(Elite) ? 1.3f : 1f;
    public static float MapGainMult => Has(Leak) ? 1.5f : 1f;
    public static float MapDecayMult => Has(Leak) ? 0.5f : 1f;
    public static float TierCapBonus => Has(RumorFire) ? 1f : 0f;
    public static float KillDpMult => Has(Lean) ? 0.75f : 1f;
    public static bool BlocksWiden => Has(NoWiden);
    public static bool BlocksDeepen => Has(NoDeepen);
    public static float CommandCdMult => Has(SlowCommand) ? 1.5f : 1f;
    public static float ShellRecoverMult => Has(ThinShell) ? 0.7f : 1f;
    public static float WaveCountMult => Has(Horde) ? 1.2f : 1f;
    public static int ReviveCostMult => Has(NoAid) ? 2 : 1;
    public static bool HidesOmen => Has(Dark);

    public static void Reset()
    {
        chosen = new List<int>();
        eraStartTurn = 1;
        scoreBonus = 0f;
    }
}
