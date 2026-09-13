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

    /// <summary>
    /// 閾値の倍率。時代が進むほど下がる＝終盤ほど決着が近い（Civ VIIと同じ考え方）。
    ///
    /// ⚠ 旧仕様は 6/3/1.5 の**階段**だった。終焉に入った瞬間に 1.5 まで落ちるので、
    ///   そこから5ターンで決着していた（実測 T18）。Civ VII は 6倍 →**1.25倍**へ
    ///   **時代の中でも連続的に**下がる。同じく時代内の進行で補間する。
    /// </summary>
    public static float Multiplier
    {
        get
        {
            float t = Mathf.Clamp01((float)EraSystem.Progress / EraSystem.Need);
            switch (EraSystem.Current)
            {
                case EraSystem.Era.Dawn:   return 6f;                       // 胎動は判定そのものが止まっている
                case EraSystem.Era.Growth: return Mathf.Lerp(6f, 3f, t);
                default:                   return Mathf.Lerp(3f, 1.25f, t); // 終焉：3.0 → 1.25
            }
        }
    }

    /// <summary>
    /// 🚧 勝利判定そのものの解禁。Civ VII は「**2番目の時代（探検）の半ば**から」勝利閾値に届きうる。
    /// それまでは誰も勝てない＝**土台を作る時間**が保証される。ここも同じにする。
    /// </summary>
    public static bool VictoryOpen
        => EraSystem.Current == EraSystem.Era.End
        || (EraSystem.Current == EraSystem.Era.Growth && EraSystem.Progress >= EraSystem.Need / 2);

    public const int HoldNeed = 8;             // 閾値を保ったまま8ターンで勝ち（反撃の窓を広げる）

    // 保持ターン数 [勢力, 勝ち筋]
    private static int[,] hold;
    private static int turnCount;
    private static void EnsureInit() { if (hold == null) hold = new int[FactionCount, PathCount]; }
    public static void Reset() { hold = null; turnCount = 0; Winner = -1; WinPath = Path.Dominion; Decided = false; declared = -1; EnsureInit(); }

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

    // ============ 📜 宣言（K-6 A-3 ジャーナル）============
    // ⚠⚠ **宣言に報酬も罰も付けない。**付けた瞬間に「宣言＝縛り」になって、選ぶのが怖くなる。
    //   ここが変えるのは**腹心が何を言うか**だけ（→ [[GuideSystem]] A-4）。
    //   ゲームの数字には一切触れない ―― 触れたくなったら、それは別の仕組みとして足すこと。
    // ⚠ `VictorySystem` は `SaveSystem.StaticTypes` に載っている＝静的フィールドは丸ごと保存される。
    //   デリゲートを持たせないこと。int なら安全。
    private static int declared = -1;

    /// <summary>いま狙うと宣言している道。宣言していなければ -1。</summary>
    public static int DeclaredPath { get { return declared; } }
    public static bool HasDeclared { get { return declared >= 0 && declared < PathCount; } }

    /// <summary>宣言する／同じ道をもう一度指定したら取り消す。⚠ いつでも変えられる。</summary>
    public static void Declare(int p)
    {
        declared = (p == declared || p < 0 || p >= PathCount) ? -1 : p;
        Debug.Log(declared < 0 ? "📜『宣言を取り消した』"
            : "📜『" + PathName((Path)declared) + "の道を狙う』と宣言した（報酬も罰もありません）");
    }

    // ============ 📜 スコアの内訳（K-6 A-3）============
    /// <summary>点1つぶんの出どころ。`amount` が素の量、`points` がそれが生む点。</summary>
    public struct Part
    {
        public string label;    // 「名声」
        public string unit;     // 「10ごとに1点」
        public int amount;      // 1240
        public int points;      // 124
        public string go;       // `GameUIManager.GoToAdvice` のキー（空なら行き先なし）
    }
    private static Part Mk(string label, string unit, int amount, int points, string go)
    { var q = new Part(); q.label = label; q.unit = unit; q.amount = amount; q.points = points; q.go = go; return q; }

    /// <summary>
    /// 📜 **自分のその道の点が、何でできているか。**
    ///
    /// ⚠⚠ **新しい数字を作らない。**ここは `Score(Self, …)` が足しているものを分解しただけ。
    ///   逆に言うと、**自分のスコアはここを合計して作る**（→ `SelfScore`）。
    ///   式を2か所に書くと「目標を達成したのに点が動かない」が生まれる。
    /// ⚠ ジャーナルの一覧も、進言（A-4）も、上部バーのチップも**ここだけ**を読むこと。
    /// </summary>
    public static List<Part> Breakdown(Path p)
    {
        var list = new List<Part>();
        switch (p)
        {
            case Path.Dominion:
            {
                int owned = SurfaceMap.OwnedCount;
                int st = SettlementSystem.SettlementCount, ct = SettlementSystem.CityCount;
                int slain = RivalLords.Count - RivalLords.AliveCount;
                list.Add(Mk("自領の広さ", "1タイル1点", owned, owned, "surface:領域"));
                list.Add(Mk("拠点", "1つ8点", st, st * 8, "surface:領域"));
                list.Add(Mk("都市", "1つ12点", ct, ct * 12, "surface:領域"));
                list.Add(Mk("倒した魔王", "1人80点", slain, slain * 80, "surface:外交"));
                break;
            }
            case Path.Dread:
            {
                int fame = DungeonResourceManager.Instance != null ? DungeonResourceManager.Instance.DungeonFame : 0;
                var et = EmotionTreeManager.Instance;
                int emo = et != null ? et.TotalSpent : 0;
                int kill = EurekaTracker.Count("kill");
                list.Add(Mk("名声", "10ごとに1点", fame, fame / 10, "dungeon:"));
                list.Add(Mk("感情に注いだ数", "1つ3点", emo, emo * 3, "panel:感情"));
                list.Add(Mk("撃破の天啓", "2体で1点", kill, kill / 2, "dungeon:"));
                break;
            }
            case Path.Economy:
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
                int dp = y.dp + dy.dp;
                list.Add(Mk("地上の産出DP", "8ごとに1点", dp, dp / 8, "surface:生産"));
                list.Add(Mk("素材", "3ごとに1点", mats, mats / 3, "surface:生産"));
                list.Add(Mk("施設", "1つ5点", dist, dist * 5, "surface:領域"));
                list.Add(Mk("遺産", "1つ15点", wonders, wonders * 15, "surface:領域"));
                break;
            }
            default:
            {
                var dl = DemonLord.Instance;
                int relics = RelicManager.Instance != null ? RelicManager.Instance.UnlockedCount : 0;
                int lv = dl != null ? dl.Level : 0;
                int rn = ResearchState.ResearchedCount;
                list.Add(Mk("研究済みのノード", "1つ6点", rn, rn * 6, "panel:研究"));
                list.Add(Mk("魔王の練度", "1Lv2点", lv, lv * 2, "panel:魔王"));
                list.Add(Mk("解放した遺物", "1つ4点", relics, relics * 4, "panel:遺物"));
                break;
            }
        }
        return list;
    }

    /// <summary>⚠ 自分の点は**内訳の合計**。式をここに書き直さない。</summary>
    private static int SelfScore(Path p)
    {
        var parts = Breakdown(p);
        int s = 0;
        for (int i = 0; i < parts.Count; i++) s += parts[i].points;
        return s;
    }

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
        if (f == Self) return SelfScore(Path.Dominion);
        if (f == HumanIndex) return HumanScore(12, 1.4f, 15f, 25f);
        int i = f - 1;
        var rv = RivalLords.Get(i);
        if (rv.defeated) return 0;
        // ⚔️ **経営の実体から出す**（敵も経営する・段①）。
        //   ⚠ 旧式は `power / 20` だけで、power は毎ターン +20/+28/+38 される直線だった
        //     ＝2位が伸びず、閾値（2位×倍率）が形骸化していた（実測 T22 決着）。
        return RivalLords.TerritoryOf(i) * 6
             + RivalBrain.FloorsOf(i) * 4
             + Mathf.RoundToInt(rv.power / 20f);
    }

    private static int DreadScore(int f)
    {
        if (f == Self) return SelfScore(Path.Dread);
        if (f == HumanIndex) return HumanScore(8, 1.8f, 20f, 30f);
        int i = f - 1;
        var rv = RivalLords.Get(i);
        // 🕸️ 恐怖＝その迷宮がどれだけ手強いか。⚠ `ThreatScore` は守りと主から出る実体の値。
        // ⚠⚠ **除数はプレイヤー側と桁を合わせるためのもの。**
        //   最初 /6 にしたら bot が 1014点になり、同じ時点のプレイヤー（124点）と桁が違った
        //   ―― `ThreatScore` は素の合計で、プレイヤーの恐怖（名声/10＋感情×3＋天啓/2）とは単位が別。
        //   ⚠ この 36 も当てずっぽう。自動運転で並べて測ってから直す。
        return rv.defeated ? 0 : Mathf.RoundToInt(RivalBrain.ThreatOf(i) / 36f);
    }

    private static int EconomyScore(int f)
    {
        if (f == Self) return SelfScore(Path.Economy);
        if (f == HumanIndex) return HumanScore(10, 1.6f, 10f, 25f);
        int i = f - 1;
        var rv = RivalLords.Get(i);
        // 💰 経済＝領地と、迷宮に積めた物の量（守り＋罠）
        return rv.defeated ? 0 : RivalLords.TerritoryOf(i) * 4 + RivalBrain.GuardsOf(i) * 3;
    }

    private static int InnovationScore(int f)
    {
        if (f == Self) return SelfScore(Path.Innovation);
        if (f == HumanIndex) return HumanScore(8, 1.5f, 8f, 30f);
        int i = f - 1;
        var rv = RivalLords.Get(i);
        // 🔬 革新＝迷宮の深さと、主の練度
        var snap = RivalBrain.DungeonOf(i);
        return rv.defeated ? 0
             : RivalBrain.FloorsOf(i) * 8 + (snap != null ? snap.lordLevel * 2 : 0);
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

        // 🚧 解禁前は保持カウントを進めない（積み上がったぶんも捨てる＝解禁後に0から数え直す）
        if (!VictoryOpen)
        {
            for (int f = 0; f < FactionCount; f++)
                for (int p = 0; p < PathCount; p++) hold[f, p] = 0;
            return;
        }

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
        if (!VictoryOpen)
        {
            string when = EraSystem.Current == EraSystem.Era.Dawn
                ? "伸長の半ばまで、勝敗は決しない"
                : "伸長の半ば（進行 " + (EraSystem.Need / 2) + "/" + EraSystem.Need + "）から勝敗が動きだす";
            return "<color=#6f6889>勝利 ― " + when + "</color>";
        }
        int bf = -1, bp = 0, bh = 0;
        for (int f = 0; f < FactionCount; f++)
            for (int p = 0; p < PathCount; p++)
                if (hold[f, p] > bh) { bh = hold[f, p]; bf = f; bp = p; }
        if (bf < 0) return "<color=#6f6889>勝利 ― まだ誰も抜け出していない（閾値は2位の" + Multiplier.ToString("0.#") + "倍）</color>";
        string c = bf == Self ? "#e3c34a" : "#e05a5a";
        return "<color=" + c + ">🏆 " + FactionName(bf) + "の『" + PathName((Path)bp) + "』が " + bh + "/" + HoldNeed + "ターン</color>";
    }
}
