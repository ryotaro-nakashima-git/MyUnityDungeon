using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🏆 勝利条件。C4 → **絶対条件＋仕上げの儀**（百年の決着・2026-09-27）。
///
/// <para>
/// ⚠⚠ **旧仕様（相対式）を捨てた理由**：`閾値 = 2位のスコア × 倍率` を 8ターン保てば勝ち、だった。
///   実測で毎回 **T22 で決着**し、それは「勝てる最速の時刻ちょうど」だった（2位が伸びない）。
///   敵にも経営させて 2位を 24→126 に伸ばしても噛み合わなかった ――
///   ① 材料が違う（こちらの革新＝研究×6・青天井／bot＝層×8＋主Lv×2）
///   ② 倍率が時代とともに下がる＝**先へ進むほど勝ちやすい**。数字いじりでは直らない。
/// </para>
///
/// <para>
/// いまの形（Civ VII のレガシーの道と同じ「絶対」）：
/// - 勝ち筋は4本。道ごとに **4つの絶対条件**。**3つ満たすと『儀』が解禁**される。
/// - 儀は **生産の列に積み、8ターン** で完成する（→ [[ProductionSystem]] `Kind.Rite`）。完成＝勝ち。
///   ⚠ 条件を3つ未満に割ると、儀は**止まる**（積んだぶんは残る）。
/// - 他の魔王も**同じ形**で儀を始める。先に終えられるとこちらの敗北。
///   こちらが遠征でその迷宮の階を落とすと、儀は押し戻される（→ `SetBackRite`）。
/// - ⚠ **人間側は儀を持たない。**人間側の勝ち方は「魔王を討つ」で、それは既にある。
///   人間側の点は時間で伸びる合成値なので、そこに条件を置くと**ただの時限装置**になる。
/// - 決着しないまま終焉の時代が終われば、**総合スコア**（4本の合計）で決まる（据え置き）。
/// </para>
///
/// <para>
/// ⚠⚠ **新しい数字を作らない。**こちらの条件は `Breakdown` の項目そのもの（4つ目が無い道だけ
///   既存の数字を足す：恐怖＝`DangerRank`、革新＝`MinionEvolution.UnlockedCount`）。
///   bot の条件は `RivalBrain` の実体（自領・守り・段・名声・主Lv・手強さ）から。
/// ⚠ 条件の値は**当てずっぽう**（いまの到達量の3〜5倍）。入れてから自動運転で測って直す。
/// </para>
///
/// 純static・実行時保持（`SaveSystem.StaticTypes` 登録済み）。
/// 関連: [[EraSystem]] [[RivalLords]] [[RivalBrain]] [[ProductionSystem]] [[century-plan]]。
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

    /// <summary>儀が解禁されるのに要る条件の数（4つのうち）。</summary>
    public const int CondNeed = 3;
    /// <summary>儀が完成するまでのターン数。</summary>
    public const int RiteTurns = 8;
    /// <summary>こちらが遠征で bot の階を1つ落としたときに押し戻す儀のターン数。</summary>
    public const int RiteSetback = 3;

    public static string RiteName(Path p)
        => p == Path.Dominion ? "覇王の宣布" : p == Path.Dread ? "畏怖の戴冠"
         : p == Path.Economy ? "黄金の契約" : "深淵の開扉";

    // 他の魔王の儀 [勢力]。⚠ こちらの儀は生産の列（`ProductionSystem`）が持つ。
    private static int[] ritePath;       // -1＝やっていない
    private static int[] riteProgress;
    private static bool[] riteStalled;   // 前のターン、条件を割って止まっていた（知らせを1回だけ出す）
    private static int turnCount;
    private static void EnsureInit()
    {
        if (ritePath == null || ritePath.Length != FactionCount)
        {
            ritePath = new int[FactionCount];
            for (int i = 0; i < FactionCount; i++) ritePath[i] = -1;
        }
        if (riteProgress == null || riteProgress.Length != FactionCount) riteProgress = new int[FactionCount];
        if (riteStalled == null || riteStalled.Length != FactionCount) riteStalled = new bool[FactionCount];
    }
    public static void Reset()
    {
        ritePath = null; riteProgress = null; riteStalled = null; turnCount = 0;
        Winner = -1; WinPath = Path.Dominion; Decided = false; declared = -1; EnsureInit();
    }

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

    // ============ 📐 絶対条件 ============
    /// <summary>条件1つ。`have` と `need` は同じ単位（危険度は等級 1〜5）。</summary>
    public struct Cond
    {
        public string label;
        public int have;
        public int need;
        public string go;         // `GameUIManager.GoToAdvice` のキー（空なら行き先なし）
        public bool rank;         // 危険度（数ではなく等級の名前で見せる）
        public bool Met { get { return have >= need; } }
        public string HaveText { get { return rank ? DangerRank.NameOf(have) : have.ToString("#,0"); } }
        public string NeedText { get { return rank ? DangerRank.NameOf(need) : need.ToString("#,0"); } }
    }
    private static Cond Cd(string label, int have, int need, string go)
    { var q = new Cond(); q.label = label; q.have = have; q.need = need; q.go = go; return q; }

    /// <summary>
    /// 📐 その勢力のその道の4条件。人間側は空（儀を持たない）。
    /// ⚠ こちらの分は `Breakdown` の `amount` をそのまま使う ―― 数え方を2か所に書かない。
    /// </summary>
    public static List<Cond> Conditions(int faction, Path p)
    {
        var list = new List<Cond>();
        if (faction == HumanIndex) return list;
        if (faction == Self)
        {
            var parts = Breakdown(p);
            // ⚠⚠ **1回測って直した値**（`docs/playlog_rite1.md`・2周）。最初の値は T40 に恐怖の儀で勝った：
            //   名声6,000 は T24〜31、撃破の天啓120 は **T3〜T15**、危険度 特級 は T30 に届いていた
            //   （名声は O(ターン²) で伸びる）。魔王Lv25 は T5、遺物12 は T5 で既に満ちていた。
            //   ⇒ 伸び方の実測から「T75〜90 あたりで届く」所へ置き直した。⚠ これもまだ仮。
            int[] need;
            switch (p)
            {
                case Path.Dominion: need = new[] { 120, 6, 3, 2 }; break;           // 自領20・拠点1（T39）＝まだ遠い
                case Path.Dread: need = new[] { 40000, 40, 2000 }; break;           // 名声 11,171／撃破 753（T39）
                case Path.Economy: need = new[] { 400, 6000, 12, 3 }; break;        // 素材 2,439（T39・+65/T）
                default: need = new[] { 150, 120, 16 }; break;                      // 研究78／Lv66／遺物10（T39）
            }
            for (int i = 0; i < parts.Count && i < need.Length; i++)
                list.Add(Cd(parts[i].label, parts[i].amount, need[i], parts[i].go));
            if (p == Path.Dread)
            {
                var q = Cd("迷宮の危険度", DangerRank.Level, DangerRank.Max, "dungeon:");
                q.rank = true; list.Add(q);
            }
            else if (p == Path.Innovation)
                list.Add(Cd("解禁した種", MinionEvolution.UnlockedCount(), 30, "panel:研究"));
            return list;
        }

        // ⚔️ 他の魔王：`RivalBrain` の実体から。
        // ⚠⚠ **どの道も「早く満ちる条件」は2つまで**にしてある（自領・階層・守り・罠・手強さ）。
        //   3つ入れると、強い bot は T25 前後で儀を始めてしまう（実測で守り91・10層に T24 で届いた）。
        //   残りは遅い軸（段9・主Lv・名声）＝ bot が枠を埋めきってから DP が流れ込む先。
        // ⚠ 主Lv の開始値は `3 + 段×4`（段7 のヴェルグは 31）。30 に置くと最初から満ちている。
        // ⚠ 名声は最初 300 だったが、周によって伸び方が5倍違った（T25 で 131 と 334）―― 波の強さが
        //   bot 自身の名声で決まるので複利で伸びる。300 は T25 で満ちたので 1,000 に（まだ仮）。
        //   ⚠ 実測：40ターンで bot は**どの道も 2/4 まで**。段は1つも上がらなかった（ヴェルグ 段7 のまま）。
        int i2 = faction - 1;
        var s = RivalBrain.DungeonOf(i2);
        int terr = RivalLords.TerritoryOf(i2);
        int guards = RivalBrain.GuardsOf(i2);
        int floors = RivalBrain.FloorsOf(i2);
        int fame = RivalBrain.FameOf(i2);
        int tier = s != null ? s.tier : 0;
        int lv = s != null ? s.lordLevel : 0;
        int traps = s != null ? s.trapFloor.Count : 0;
        int threat = Mathf.RoundToInt(RivalBrain.ThreatOf(i2));
        switch (p)
        {
            case Path.Dominion:
                list.Add(Cd("自領", terr, 5, "")); list.Add(Cd("守り", guards, 80, ""));
                list.Add(Cd("眷属の段", tier, 9, "")); list.Add(Cd("名声", fame, 1000, "")); break;
            case Path.Dread:
                list.Add(Cd("名声", fame, 1000, "")); list.Add(Cd("迷宮の手強さ", threat, 10000, ""));
                list.Add(Cd("罠", traps, 25, "")); list.Add(Cd("主のLv", lv, 50, "")); break;
            case Path.Economy:
                list.Add(Cd("自領", terr, 5, "")); list.Add(Cd("階層", floors, 10, ""));
                list.Add(Cd("主のLv", lv, 50, "")); list.Add(Cd("名声", fame, 1000, "")); break;
            default:
                list.Add(Cd("眷属の段", tier, 9, "")); list.Add(Cd("主のLv", lv, 50, ""));
                list.Add(Cd("階層", floors, 10, "")); list.Add(Cd("迷宮の手強さ", threat, 10000, "")); break;
        }
        return list;
    }

    public static int MetCount(int faction, Path p)
    {
        var l = Conditions(faction, p);
        int n = 0;
        for (int i = 0; i < l.Count; i++) if (l[i].Met) n++;
        return n;
    }
    public static bool RiteUnlocked(int faction, Path p)
    {
        if (faction == HumanIndex) return false;
        if (faction != Self && RivalLords.Get(faction - 1).defeated) return false;
        return MetCount(faction, p) >= CondNeed;
    }

    // ============ ◆ 儀 ============
    /// <summary>いま儀をやっている道（-1＝無し）。こちらは生産の列から読む。</summary>
    public static int RitePathOf(int faction)
    {
        EnsureInit();
        if (faction == Self) { var it = ProductionSystem.RiteItem; return it != null ? it.index : -1; }
        return (faction >= 0 && faction < FactionCount) ? ritePath[faction] : -1;
    }
    public static int RiteProgressOf(int faction)
    {
        EnsureInit();
        if (faction == Self) { var it = ProductionSystem.RiteItem; return it != null ? it.progress : 0; }
        return (faction >= 0 && faction < FactionCount) ? riteProgress[faction] : 0;
    }
    /// <summary>儀が止まっているか（条件を3つ未満に割った）。</summary>
    public static bool RiteStalled(int faction)
    {
        int p = RitePathOf(faction);
        return p >= 0 && !RiteUnlocked(faction, (Path)p);
    }

    /// <summary>◆ こちらの儀が完成した（`ProductionSystem` から呼ぶ）。</summary>
    public static void CompleteRite(Path p)
    {
        if (Decided) return;
        Debug.Log("<color=#e3c34a>◆『" + RiteName(p) + "』が成った。</color>");
        Decide(Self, p);
    }

    /// <summary>
    /// 💥 こちらが遠征で bot の階を落とした ―― 儀を押し戻す。
    /// ⚠ **これが「止める手」の本体。**無いと、bot が儀を始めた時点で見ているしかなくなる。
    /// </summary>
    public static void SetBackRite(int rivalIndex)
    {
        EnsureInit();
        int f = rivalIndex + 1;
        if (f <= Self || f >= HumanIndex || ritePath[f] < 0) return;
        int before = riteProgress[f];
        riteProgress[f] = Mathf.Max(0, before - RiteSetback);
        Debug.Log("💥『儀を押し戻した』" + FactionName(f) + " の『" + RiteName((Path)ritePath[f]) + "』 "
            + before + " → " + riteProgress[f] + "/" + RiteTurns);
        NotifySystem.Push("<b>" + FactionName(f) + "</b> の儀を押し戻した（" + riteProgress[f] + "/" + RiteTurns + "）",
            NotifySystem.Kind.Gain);
    }

    // ============ 毎ターン ============
    public static void TickTurn()
    {
        EnsureInit();
        if (Decided) return;
        turnCount++;

        // ⚔️ 他の魔王の儀。⚠ こちらの儀は `ProductionSystem.Tick` が進める（生産の列にあるので）。
        for (int f = 1; f < HumanIndex; f++)
        {
            var rv = RivalLords.Get(f - 1);
            if (rv.defeated) { ritePath[f] = -1; riteProgress[f] = 0; continue; }
            if (ritePath[f] < 0)
            {
                // 上から順に最初の1本（bot の手と同じ決め方）
                for (int p = 0; p < PathCount; p++)
                {
                    if (!RiteUnlocked(f, (Path)p)) continue;
                    ritePath[f] = p; riteProgress[f] = 0; riteStalled[f] = false;
                    Debug.Log("◆『儀が始まった』" + FactionName(f) + " が『" + RiteName((Path)p) + "』（" + PathName((Path)p)
                        + "の道）を始めた ― <color=#e05a5a>" + RiteTurns + "ターンで完成する。止めなければこちらの敗北</color>");
                    NotifySystem.Push("<b>" + FactionName(f) + "</b> が『" + RiteName((Path)p) + "』を始めた　"
                        + RiteTurns + "ターンで完成 ― 巣へ攻め込めば押し戻せる", NotifySystem.Kind.Danger);
                    break;
                }
                continue;
            }
            var path = (Path)ritePath[f];
            if (!RiteUnlocked(f, path))
            {
                if (!riteStalled[f])
                    Debug.Log("◆『儀が止まった』" + FactionName(f) + " の『" + RiteName(path) + "』（条件を割った・"
                        + riteProgress[f] + "/" + RiteTurns + "）");
                riteStalled[f] = true;
                continue;
            }
            riteStalled[f] = false;
            riteProgress[f]++;
            if (riteProgress[f] >= RiteTurns) { Decide(f, path); return; }
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

    /// <summary>いちばん進んでいる他の魔王の儀（無ければ -1）。警告に使う。</summary>
    public static int MostUrgentRivalRite()
    {
        EnsureInit();
        int best = -1, bp = -1;
        for (int f = 1; f < HumanIndex; f++)
            if (ritePath[f] >= 0 && riteProgress[f] > bp) { bp = riteProgress[f]; best = f; }
        return best;
    }

    /// <summary>ヘッダ用の一行（いちばん切迫している儀を出す）。</summary>
    public static string HeaderLine()
    {
        EnsureInit();
        if (Decided) return Winner == Self
            ? "<color=#e3c34a>🏆 " + PathName(WinPath) + "の勝利</color>"
            : "<color=#e05a5a>🏆 敗北 ― " + FactionName(Winner) + "の" + PathName(WinPath) + "</color>";
        int rf = MostUrgentRivalRite();
        if (rf >= 0)
            return "<color=#e05a5a>◆ " + FactionName(rf) + "の『" + RiteName((Path)ritePath[rf]) + "』 "
                 + riteProgress[rf] + "/" + RiteTurns + "</color>";
        int mp = RitePathOf(Self);
        if (mp >= 0)
            return "<color=#e3c34a>◆ 『" + RiteName((Path)mp) + "』 " + RiteProgressOf(Self) + "/" + RiteTurns + "</color>";
        int best = 0, bestP = 0;
        for (int p = 0; p < PathCount; p++) { int m = MetCount(Self, (Path)p); if (m > best) { best = m; bestP = p; } }
        return "<color=#6f6889>勝利 ― いちばん近いのは" + PathName((Path)bestP) + "の道（条件 " + best + "/4）</color>";
    }
}
