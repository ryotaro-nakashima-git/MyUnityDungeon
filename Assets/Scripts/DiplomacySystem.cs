using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🕊️ 外交・独立勢力・交易（Civ VII の Influence / Independent Powers / Trade Routes / War Support）。C5。
///
/// Civ VII の形をそのまま持ち込む:
/// - **威名(Influence)** ＝ 外交の通貨。毎ターン貯まり、働きかけ・不可侵・交易路に使う。
/// - **独立勢力** ＝ 盤に散る自治都市。**威名を注いで従属(Suzerain)**させると恵みが入る。
///   他の魔王も同じ相手に注いでくるので、**取り合い**になる。
/// - **交易路** ＝ 自分の拠点どうし（または従属した独立勢力）を結ぶと、両端に産出が乗る。
/// - **戦争支持/厭戦** ＝ 同時に多くの相手と戦っていると全拠点に不満が乗る。威名で不可侵を買える。
///
/// 名声(fame)とは別物。名声は「世に知られた度合い＝冒険者が強くなる諸刃」だが、
/// 威名は「他勢力を動かす力」で、難易度には直接効かない。
/// 純static・実行時保持。関連: [[RivalLords]] [[SettlementSystem]] [[civ7-roadmap]]。
/// </summary>
public static class DiplomacySystem
{
    // ============ 💠 威名（Influence） ============
    private static int influence;
    public static int Influence { get { EnsureInit(); return influence; } }

    /// <summary>毎ターン入る威名。拠点・中継の町・遺産・時代から。</summary>
    public static int IncomePerTurn
    {
        get
        {
            int n = 2;
            foreach (var r in SurfaceMap.All)
            {
                if (!r.owned || r.settle == SurfaceMap.Settle.None) continue;
                n += r.settle == SurfaceMap.Settle.City ? 2 : 1;
                if (r.settle == SurfaceMap.Settle.Town && r.focus == 7) n += 3;   // 🎯 中継の町
                if (r.wonderIndex >= 0) n += 2;
            }
            if (ResearchState.IsResearched("s_influence")) n += 4;
            n += (int)EraSystem.Current * 2;                                       // 時代が進むほど声が通る
            foreach (var p in Powers) if (p.suzerain == 0 && p.kind == 5) n += 3;  // 隠れ里
            return n;
        }
    }
    public static void AddInfluence(int n) { EnsureInit(); influence = Mathf.Max(0, influence + n); }
    public static bool TrySpend(int n)
    {
        EnsureInit();
        if (influence < n) { Debug.LogWarning($"⚠️ 威名が足りません（要{n}・所持{influence}）。"); return false; }
        influence -= n; return true;
    }

    // ============ 🏛️ 独立勢力（Independent Powers） ============
    public class Power
    {
        public int regionId;
        public string name;
        public int kind;                 // 恵みの種類
        public int favor;                // 自分の好意 0..100
        public int[] rivalFavor;         // 他魔王の好意
        public int suzerain = -1;        // -1＝独立 / 0＝自分 / 1..3＝他魔王
    }

    public struct PowerKind { public string jpName, desc, colorHex; }
    private static readonly PowerKind[] kinds =
    {
        K("傭兵都市", "従属すると 眷属の戦力 +15%",        "#e05a5a"),
        K("交易都市", "従属すると 毎ターン DP +120",       "#e3c34a"),
        K("学都",     "従属すると 毎ターン 研究点 +5",     "#8cb8e6"),
        K("聖堂都市", "従属すると 毎ターン 感情 +10",      "#c04a6a"),
        K("鍛冶都市", "従属すると 毎ターン 素材 +6",       "#9aa3b0"),
        K("隠れ里",   "従属すると 毎ターン 威名 +3",       "#57c3ab"),
    };
    private static PowerKind K(string n, string d, string c) => new PowerKind { jpName = n, desc = d, colorHex = c };
    public static PowerKind Kind(int i) => kinds[Mathf.Clamp(i, 0, kinds.Length - 1)];

    private static List<Power> powers;
    public static List<Power> Powers { get { EnsureInit(); return powers; } }
    public const int FavorNeed = 100;

    /// <summary>働きかけの費用（Civと同じく、既に従えている数だけ高くなる）。</summary>
    public static int CourtCost()
    {
        int held = 0;
        foreach (var p in Powers) if (p.suzerain == 0) held++;
        float c = 12 + 10 * held;
        if (ResearchState.IsResearched("s_accord")) c *= 0.7f;    // 🤝 盟約
        return Mathf.RoundToInt(c);
    }
    public const int CourtGain = 22;

    public static bool TryCourt(int i)
    {
        EnsureInit();
        if (i < 0 || i >= powers.Count) return false;
        var p = powers[i];
        if (p.suzerain == 0) { Debug.LogWarning("⚠️ 既に従えています。"); return false; }
        if (!SurfaceMap.IsDiscovered(p.regionId)) { Debug.LogWarning("⚠️ まだ見えていない相手には働きかけられません。"); return false; }
        int cost = CourtCost();
        if (!TrySpend(cost)) return false;
        p.favor = Mathf.Min(FavorNeed, p.favor + CourtGain);
        Debug.Log($"🕊️『働きかけ』{p.name} への好意 {p.favor}/{FavorNeed}（-{cost}威名）");
        if (p.favor >= FavorNeed) BecomeSuzerain(i, 0);
        return true;
    }

    private static void BecomeSuzerain(int i, int who)
    {
        var p = powers[i];
        if (p.suzerain == who) return;
        p.suzerain = who;
        // 従属した独立勢力のタイルは、その勢力の色に染まる（支配とは別だが盤で分かるように）
        if (who == 0) SurfaceMap.SetOwner(p.regionId, SurfaceMap.OwnerSelf);
        else SurfaceMap.SetOwner(p.regionId, SurfaceMap.OwnerRivalBase + (who - 1));
        string byWhom = who == 0 ? "自分" : RivalLords.NameOf(who - 1);
        Debug.Log($"🏛️『従属』{p.name}（{Kind(p.kind).jpName}）が {byWhom} に従った ― {Kind(p.kind).desc}");
    }

    /// <summary>いま自分が従えている数。</summary>
    public static int SuzerainCount { get { int n = 0; foreach (var p in Powers) if (p.suzerain == 0) n++; return n; } }
    public static bool HasKind(int kind) { foreach (var p in Powers) if (p.suzerain == 0 && p.kind == kind) return true; return false; }

    // 従属の恵み（各systemはここを見る）
    public static float KinPowerMult => HasKind(0) ? 1.15f : 1f;
    public static int DpPerTurn => HasKind(1) ? 120 : 0;
    public static int RpPerTurn => HasKind(2) ? 5 : 0;
    public static int EmotionPerTurn => HasKind(3) ? 10 : 0;
    public static int MaterialPerTurn => HasKind(4) ? 6 : 0;

    // ============ 🛤️ 交易路（Trade Routes） ============
    public class Route { public int a, b; }
    private static List<Route> routes;
    public static List<Route> Routes { get { EnsureInit(); return routes; } }
    public static int RouteLimit => 1 + SettlementSystem.CityCount + (ResearchState.IsResearched("s_trade") ? 2 : 0);
    public const int RouteCost = 25;
    public const int RouteRange = 10;

    public static bool TryOpenRoute(int a, int b)
    {
        EnsureInit();
        if (a == b) return false;
        var ra = SurfaceMap.Get(a); var rb = SurfaceMap.Get(b);
        if (!ra.owned || !rb.owned || ra.settle == SurfaceMap.Settle.None || rb.settle == SurfaceMap.Settle.None)
        { Debug.LogWarning("⚠️ 交易路は自分の拠点どうしを結びます。"); return false; }
        if (routes.Count >= RouteLimit) { Debug.LogWarning($"⚠️ 交易路の上限です（{routes.Count}/{RouteLimit}・都市を増やすか『交易の道』を研究）。"); return false; }
        if (SurfaceMap.HexDist(ra, rb) > RouteRange) { Debug.LogWarning($"⚠️ 遠すぎます（{RouteRange}マスまで）。"); return false; }
        foreach (var r in routes) if ((r.a == a && r.b == b) || (r.a == b && r.b == a)) { Debug.LogWarning("⚠️ 既に結ばれています。"); return false; }
        if (!TrySpend(RouteCost)) return false;
        routes.Add(new Route { a = a, b = b });
        Debug.Log($"🛤️『交易路』{ra.name} ― {rb.name} を結んだ（-{RouteCost}威名・両端に産出）");
        return true;
    }
    public static void CloseRoute(int index)
    {
        EnsureInit();
        if (index < 0 || index >= routes.Count) return;
        var r = routes[index];
        Debug.Log($"🛤️『交易路を閉じる』{SurfaceMap.Get(r.a).name} ― {SurfaceMap.Get(r.b).name}");
        routes.RemoveAt(index);
    }
    /// <summary>その拠点につながっている交易路の本数。</summary>
    public static int RoutesAt(int settlementId)
    {
        int n = 0;
        foreach (var r in Routes) if (r.a == settlementId || r.b == settlementId) n++;
        return n;
    }

    // ============ ⚔️ 他魔王との関係（不可侵・讒言・厭戦） ============
    private static int[] peace;          // 不可侵の残りターン
    public static int PeaceLeft(int rival) { EnsureInit(); return rival >= 0 && rival < peace.Length ? peace[rival] : 0; }
    public static bool AtWar(int rival)
    {
        var rv = RivalLords.Get(rival);
        return !rv.defeated && PeaceLeft(rival) <= 0;
    }
    public static int PeaceCost(int rival) => 40 + Mathf.RoundToInt(RivalLords.Get(rival).power / 20f);
    public const int PeaceSpan = 8;

    public static bool TryMakePeace(int rival)
    {
        EnsureInit();
        var rv = RivalLords.Get(rival);
        if (rv.defeated) { Debug.LogWarning("⚠️ 既に排除しています。"); return false; }
        if (peace[rival] > 0) { Debug.LogWarning("⚠️ 既に不可侵です。"); return false; }
        if (!TrySpend(PeaceCost(rival))) return false;
        peace[rival] = PeaceSpan;
        Debug.Log($"🕊️『不可侵』{rv.name} と {PeaceSpan} ターンの盟約を結んだ（向こうからは攻めてこない）");
        return true;
    }

    /// <summary>物語事件などで、費用なしに不可侵を結ぶ。</summary>
    public static void TryMakePeaceFree(int rival)
    {
        EnsureInit();
        if (rival < 0 || rival >= peace.Length) return;
        var rv = RivalLords.Get(rival);
        if (rv.defeated) return;
        peace[rival] = PeaceSpan;
        Debug.Log($"🕊️『不可侵』{rv.name} と {PeaceSpan} ターンの盟約を結んだ");
    }

    public const int InciteCost = 55;
    /// <summary>讒言：他の魔王を焚きつけて、こちらではなく互いに向かわせる（力を削る）。</summary>
    public static bool TryIncite(int rival)
    {
        EnsureInit();
        var rv = RivalLords.Get(rival);
        if (rv.defeated) return false;
        if (!TrySpend(InciteCost)) return false;
        float lost = rv.power * 0.12f + 20f;
        rv.power = Mathf.Max(30f, rv.power - lost);
        rv.lastAction = "内輪もめで消耗した";
        Debug.Log($"🗣️『讒言』{rv.name} を焚きつけた ― 力 -{lost:0}（-{InciteCost}威名）");
        return true;
    }

    /// <summary>⚔️ 厭戦：同時に多くの相手と戦っていると全拠点に不満が乗る（Civの War Weariness）。</summary>
    public static int WarWeariness
    {
        get
        {
            int wars = 0;
            for (int i = 0; i < RivalLords.Count; i++) if (AtWar(i)) wars++;
            return Mathf.Max(0, wars - 1);      // 1相手までは無償。2人目からのしかかる
        }
    }

    // ============ 生成・毎ターン ============
    private static void EnsureInit()
    {
        if (powers != null) return;
        powers = new List<Power>();
        routes = new List<Route>();
        peace = new int[Mathf.Max(1, RivalLords.Count)];
        influence = 20;
        BuildPowers();
    }
    public static void Reset() { powers = null; EnsureInit(); }

    /// <summary>盤の上の「町/都市」型の中立タイルから、独立勢力を選んで置く。</summary>
    private static void BuildPowers()
    {
        var cand = new List<SurfaceMap.Region>();
        foreach (var r in SurfaceMap.All)
        {
            if (r.isOcean || r.owner != SurfaceMap.OwnerNeutral || r.rivalHome >= 0) continue;
            if (r.type != SurfaceMap.RegionType.Town && r.type != SurfaceMap.RegionType.City) continue;
            if (r.depth < 1.5f) continue;                     // 入口の目の前は避ける
            cand.Add(r);
        }
        for (int i = 0; i < cand.Count; i++) { int j = Random.Range(i, cand.Count); var t = cand[i]; cand[i] = cand[j]; cand[j] = t; }

        int want = Mathf.Clamp(SurfaceMap.Count / 500, 4, 10);
        var placed = new List<SurfaceMap.Region>();
        foreach (var c in cand)
        {
            if (placed.Count >= want) break;
            bool tooClose = false;
            foreach (var o in placed) if (SurfaceMap.HexDist(c, o) < 6) { tooClose = true; break; }
            if (tooClose) continue;
            placed.Add(c);
        }
        for (int i = 0; i < placed.Count; i++)
        {
            var p = new Power
            {
                regionId = placed[i].id,
                kind = i % kinds.Length,
                name = placed[i].name,
                rivalFavor = new int[Mathf.Max(1, RivalLords.Count)],
            };
            placed[i].defense = Mathf.RoundToInt(placed[i].defense * 1.5f + 120);   // 自治都市は硬い
            powers.Add(p);
        }
        if (powers.Count > 0) Debug.Log($"🏛️『独立勢力』{powers.Count}つの自治都市が盤にある（威名で従属させられる）");
    }

    public static void TickTurn()
    {
        EnsureInit();
        influence += IncomePerTurn;

        // 不可侵の残り
        for (int i = 0; i < peace.Length; i++)
            if (peace[i] > 0)
            {
                peace[i]--;
                if (peace[i] == 0) Debug.Log($"🕊️『盟約の終わり』{RivalLords.NameOf(i)} との不可侵が切れた");
            }

        // 他魔王も独立勢力に働きかけてくる（取り合い）
        foreach (var p in powers)
        {
            if (p.suzerain >= 0) continue;
            for (int i = 0; i < RivalLords.Count && i < p.rivalFavor.Length; i++)
            {
                var rv = RivalLords.Get(i);
                if (rv.defeated) continue;
                if (SurfaceMap.HexDist(SurfaceMap.Get(p.regionId), SurfaceMap.Get(Mathf.Max(0, RivalLords.HomeOf(i)))) > 14) continue;
                p.rivalFavor[i] += Random.Range(2, 6);
                if (p.rivalFavor[i] >= FavorNeed) { BecomeSuzerain(powers.IndexOf(p), i + 1); break; }
            }
        }

        // 従属の恵み
        var res = DungeonResourceManager.Instance;
        if (res != null) { if (DpPerTurn > 0) res.AddDP(DpPerTurn); if (MaterialPerTurn > 0) res.AddMaterial(MaterialPerTurn); }
        if (RpPerTurn > 0) ResearchState.AddRP(RpPerTurn);
        var et = EmotionTreeManager.Instance;
        if (et != null && EmotionPerTurn > 0) for (int i = 0; i < 4; i++) et.AddEmotion((EmotionTreeManager.Route)i, Mathf.Max(1, EmotionPerTurn / 4));

        // 🛤️ 交易路の産出（両端の拠点に）
        if (routes.Count > 0)
        {
            int dp = 0, food = 0;
            foreach (var r in routes)
            {
                var ra = SurfaceMap.Get(r.a); var rb = SurfaceMap.Get(r.b);
                if (!ra.owned || !rb.owned) continue;
                int d = SurfaceMap.HexDist(ra, rb);
                dp += 30 + d * 6;                       // 遠いほど旨い（Civの交易路と同じ）
                food += 2;
                ra.foodStock += 1; rb.foodStock += 1;
            }
            if (dp > 0 && res != null) res.AddDP(dp);
            if (dp > 0) Debug.Log($"🛤️『交易』{routes.Count}本の交易路から +{dp}DP（両端に食料+1）");
        }
    }

    /// <summary>ヘッダ用の一行。</summary>
    public static string HeaderLine()
    {
        EnsureInit();
        string s = "<color=#57c3ab>威名 " + influence + "</color> <size=88%><color=#9c95b4>(+" + IncomePerTurn + "/T)</color></size>";
        if (SuzerainCount > 0) s += "　<color=#8cb8e6>従属 " + SuzerainCount + "/" + powers.Count + "</color>";
        if (routes.Count > 0) s += "　<color=#e3c34a>交易 " + routes.Count + "/" + RouteLimit + "</color>";
        if (WarWeariness > 0) s += "　<color=#e05a5a>厭戦 +" + WarWeariness + "不満</color>";
        return s;
    }
}
