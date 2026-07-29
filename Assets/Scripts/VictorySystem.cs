using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🏆 勝利条件（Civ VII の Victory）。C4。
///
/// Civ VII 1.4.0 の形をそのまま持ち込む:
/// - 勝ち筋は4本あり、**すべてスコア制**。誰かが一方的に達成するのではなく順位で競う。
/// - 閾値は **2位のスコア × 倍率**。倍率は時代が進むほど下がる（6倍 → 3倍 → 1.5倍）＝終盤ほど決着が近い。
/// - 閾値に届いてから **5ターン保持**して初めて勝ち。**相手に反撃の窓を与える**のが肝。
/// - 決着しないまま最後の時代が終われば、**総合スコア**（4本の合計）で決まる。
///
/// この作品では競うのは **自分／他の魔王3人／人間側** の5勢力。
/// 他の勢力が勝ち切ると**こちらの敗北**になる（放っておけない）。
/// 純static・実行時保持。関連: [[EraSystem]] [[RivalLords]] [[civ7-roadmap]]。
/// </summary>
public static class VictorySystem
{
    public enum Path { Dominion = 0, Dread = 1, Economy = 2, Innovation = 3 }
    public const int PathCount = 4;

    public static string PathName(Path p)
        => p == Path.Dominion ? "制圧" : p == Path.Dread ? "恐怖" : p == Path.Economy ? "経済" : "革新";
    public static string PathDesc(Path p)
        => p == Path.Dominion ? "領土と、他の魔王をどれだけ排除したか"
         : p == Path.Dread ? "名声と感情 ― どれだけ世界を畏怖で染めたか"
         : p == Path.Economy ? "DPと素材の産出、施設と遺産の厚み"
         : "研究の到達点と、魔王自身の練度";
    public static string PathColor(Path p)
        => p == Path.Dominion ? "#e05a5a" : p == Path.Dread ? "#c04a6a" : p == Path.Economy ? "#e3c34a" : "#8cb8e6";

    // 勢力：0=自分 / 1..3=他魔王 / 4=人間側
    public const int FactionCount = 5;
    public const int Self = 0, HumanIndex = 4;
    public static string FactionName(int f)
        => f == Self ? "自分" : f == HumanIndex ? "人間側" : RivalLords.NameOf(f - 1);
    public static string FactionColor(int f)
        => f == Self ? "#5cc47c" : f == HumanIndex ? "#c9c2e0" : RivalLords.ColorOf(f - 1);

    /// <summary>閾値の倍率。時代が進むほど下がる＝終盤ほど決着が近い（Civ VIIと同じ考え方）。</summary>
    public static float Multiplier => EraSystem.Current == EraSystem.Era.Dawn ? 6f
                                    : EraSystem.Current == EraSystem.Era.Growth ? 3f : 1.5f;
    public const int HoldNeed = 5;             // 閾値を保ったまま5ターンで勝ち

    // 保持ターン数 [勢力, 勝ち筋]
    private static int[,] hold;
    private static int turnCount;
    private static void EnsureInit() { if (hold == null) hold = new int[FactionCount, PathCount]; }
    public static void Reset() { hold = null; turnCount = 0; Winner = -1; WinPath = Path.Dominion; Decided = false; EnsureInit(); }

    /// <summary>
    /// 人間側の「世界が動員してくる速さ」。
    /// ⚠ ここを**未支配の土地の広さ**で測ってはいけない。盤の9割は最初から中立なので、
    ///   実測で人間側が制圧289／経済1674と桁違いになり、**開始5ターンで人間が勝ってしまった**。
    ///   人間側は領土の大きさではなく「こちらへ向けてくる圧力」＝ターン・時代・世界水準で伸ばす。
    /// </summary>
    private static int HumanScore(int baseValue, float perTurn, float tierW, float eraW)
        => baseValue + Mathf.RoundToInt(turnCount * perTurn + SurfaceMap.WorldTierBias * tierW + EraSystem.TierBias * eraW);

    public static int Winner { get; private set; } = -1;      // -1＝まだ
    public static Path WinPath { get; private set; }
    public static bool Decided { get; private set; }
    public static int HoldOf(int faction, Path p) { EnsureInit(); return hold[faction, (int)p]; }

    // ============ スコア ============
    public static int Score(int faction, Path p)
    {
        switch (p)
        {
            case Path.Dominion: return DominionScore(faction);
            case Path.Dread: return DreadScore(faction);
            case Path.Economy: return EconomyScore(faction);
            default: return InnovationScore(faction);
        }
    }

    private static int DominionScore(int f)
    {
        if (f == Self)
        {
            int s = SurfaceMap.OwnedCount
                  + SettlementSystem.SettlementCount * 8 + SettlementSystem.CityCount * 12
                  + (RivalLords.Count - RivalLords.AliveCount) * 80;
            return s;
        }
        if (f == HumanIndex) return HumanScore(12, 1.4f, 15f, 25f);
        int i = f - 1;
        var rv = RivalLords.Get(i);
        if (rv.defeated) return 0;
        return RivalLords.TerritoryOf(i) * 6 + Mathf.RoundToInt(rv.power / 20f);
    }

    private static int DreadScore(int f)
    {
        if (f == Self)
        {
            int fame = DungeonResourceManager.Instance != null ? DungeonResourceManager.Instance.DungeonFame : 0;
            var et = EmotionTreeManager.Instance;
            int emo = et != null ? et.TotalSpent : 0;
            return fame / 10 + emo * 3 + EurekaTracker.Count("kill") / 2;
        }
        if (f == HumanIndex) return HumanScore(8, 1.8f, 20f, 30f);
        int i = f - 1;
        var rv = RivalLords.Get(i);
        return rv.defeated ? 0 : Mathf.RoundToInt(rv.power / 12f);
    }

    private static int EconomyScore(int f)
    {
        if (f == Self)
        {
            var y = SurfaceMap.YieldSummary();
            var dy = DistrictCatalog.TotalYields();
            int mats = DungeonResourceManager.Instance != null ? DungeonResourceManager.Instance.CraftMaterials : 0;
            int dist = 0, wonders = 0;
            foreach (var r in SurfaceMap.All)
            {
                if (!r.owned) continue;
                if (r.district >= 0) dist++;
                if (r.district2 >= 0) dist++;
                if (r.wonderIndex >= 0) wonders++;
            }
            return (y.dp + dy.dp) / 8 + mats / 3 + dist * 5 + wonders * 15;
        }
        if (f == HumanIndex) return HumanScore(10, 1.6f, 10f, 25f);
        int i = f - 1;
        var rv = RivalLords.Get(i);
        return rv.defeated ? 0 : RivalLords.TerritoryOf(i) * 4 + Mathf.RoundToInt(rv.power / 30f);
    }

    private static int InnovationScore(int f)
    {
        if (f == Self)
        {
            var dl = DemonLord.Instance;
            int relics = RelicManager.Instance != null ? RelicManager.Instance.UnlockedCount : 0;
            return ResearchState.ResearchedCount * 6 + (dl != null ? dl.Level * 2 : 0) + relics * 4;
        }
        if (f == HumanIndex) return HumanScore(8, 1.5f, 8f, 30f);
        int i = f - 1;
        var rv = RivalLords.Get(i);
        return rv.defeated ? 0 : Mathf.RoundToInt(rv.power / 25f);
    }

    /// <summary>4本の合計＝総合スコア（決着しなかったときの最終判定）。</summary>
    public static int TotalScore(int f)
    {
        int s = 0;
        for (int p = 0; p < PathCount; p++) s += Score(f, (Path)p);
        return s;
    }

    // ============ 順位と閾値 ============
    /// <summary>その勝ち筋の2位のスコア（＝閾値の基準）。</summary>
    public static int SecondScore(Path p, int exclude)
    {
        int best = int.MinValue, second = int.MinValue;
        for (int f = 0; f < FactionCount; f++)
        {
            if (f == exclude) continue;
            int s = Score(f, p);
            if (s > best) { second = best; best = s; }
            else if (s > second) second = s;
        }
        return Mathf.Max(1, best);   // 自分を除いた最上位＝実質の「2位」
    }

    /// <summary>その勢力が勝つのに必要なスコア。</summary>
    public static int ThresholdFor(int faction, Path p) => Mathf.CeilToInt(SecondScore(p, faction) * Multiplier);
    public static bool IsOver(int faction, Path p) => Score(faction, p) >= ThresholdFor(faction, p);

    // ============ 毎ターン ============
    public static void TickTurn()
    {
        EnsureInit();
        if (Decided) return;
        turnCount++;

        for (int f = 0; f < FactionCount; f++)
            for (int p = 0; p < PathCount; p++)
            {
                bool over = IsOver(f, (Path)p);
                int before = hold[f, p];
                hold[f, p] = over ? before + 1 : 0;
                if (over && before == 0)
                    Debug.Log($"🏆『{PathName((Path)p)}の勝利が見えてきた』{FactionName(f)} が閾値に到達（{HoldNeed}ターン保てば決着）"
                        + (f == Self ? "" : " ― <color=#e05a5a>止めなければこちらの敗北</color>"));
                if (hold[f, p] >= HoldNeed) { Decide(f, (Path)p); return; }
            }

        // 最後の時代が終わっても決着しなければ総合スコア
        if (EraSystem.Current == EraSystem.Era.End && EraSystem.Progress >= EraSystem.Need)
        {
            int best = -1, bestS = int.MinValue;
            for (int f = 0; f < FactionCount; f++) { int s = TotalScore(f); if (s > bestS) { bestS = s; best = f; } }
            Debug.Log($"🏆『総合スコアで決着』{FactionName(best)}（{bestS}点）");
            Decide(best, Path.Dominion);
        }
    }

    private static void Decide(int faction, Path p)
    {
        Winner = faction; WinPath = p; Decided = true;
        if (faction == Self)
            Debug.Log($"<color=#e3c34a>🏆『{PathName(p)}の勝利』この世界は魔王のものになった。</color>");
        else
            Debug.Log($"<color=#e05a5a>🏆『敗北』{FactionName(faction)} が『{PathName(p)}』で世界を取った。</color>");
    }

    /// <summary>ヘッダ用の一行（いちばん切迫している勝ち筋を出す）。</summary>
    public static string HeaderLine()
    {
        EnsureInit();
        if (Decided) return Winner == Self
            ? "<color=#e3c34a>🏆 " + PathName(WinPath) + "の勝利</color>"
            : "<color=#e05a5a>🏆 敗北 ― " + FactionName(Winner) + "の" + PathName(WinPath) + "</color>";
        int bf = -1, bp = 0, bh = 0;
        for (int f = 0; f < FactionCount; f++)
            for (int p = 0; p < PathCount; p++)
                if (hold[f, p] > bh) { bh = hold[f, p]; bf = f; bp = p; }
        if (bf < 0) return "<color=#6f6889>勝利 ― まだ誰も抜け出していない（閾値は2位の" + Multiplier.ToString("0.#") + "倍）</color>";
        string c = bf == Self ? "#e3c34a" : "#e05a5a";
        return "<color=" + c + ">🏆 " + FactionName(bf) + "の『" + PathName((Path)bp) + "』が " + bh + "/" + HoldNeed + "ターン</color>";
    }
}
