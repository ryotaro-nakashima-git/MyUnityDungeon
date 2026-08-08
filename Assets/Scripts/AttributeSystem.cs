using UnityEngine;

/// <summary>
/// 🎖️ 属性ツリー（Civ VII の Attributes）。S2＋S3。
///
/// Civ VII の骨格をそのまま持ち込む：
///   **レガシーの道（＝偉業を6つの軸に整理）を達成する → その軸の属性ポイントが入る → 属性ツリーで恒久強化**
/// 属性は**時代をまたいで残る**。だから「この時代は何の道を通ったか」が次の時代の強さになる。
///
/// 軸は Civ VII と同じ6本：軍事・拡張・経済・科学・文化・外交。
/// 各軸4段。段は前段を取ってから。**ポイントは軸ごとに別**なので、
/// 通った道の分しか伸びない＝**やったことがそのまま形になる**（万能ビルドにならない）。
///
/// 合計ポイントは 小偉業1点＋大偉業2点 ×18偉業 ＝ **24点**、ツリーも24ノード。
/// ただし軸ごとに偏るので全部は取れない（＝選択になる）。
///
/// ⚠ 効果は既存の getter に**加算で**乗せる（乗算軸を増やさない → [[difficulty-curve-orders]]）。
/// 純static・実行時保持。関連: [[civ7-gap-plan]] [[EraSystem]] [[PolicySystem]]。
/// </summary>
public static class AttributeSystem
{
    public enum Axis { War = 0, Expand = 1, Wealth = 2, Science = 3, Culture = 4, Diplo = 5 }
    public const int AxisCount = 6;
    public const int Tiers = 4;

    public static string AxisName(Axis a)
    {
        switch (a)
        {
            case Axis.War: return "軍事";
            case Axis.Expand: return "拡張";
            case Axis.Wealth: return "経済";
            case Axis.Science: return "科学";
            case Axis.Culture: return "文化";
            default: return "外交";
        }
    }
    public static string AxisColor(Axis a)
    {
        switch (a)
        {
            case Axis.War: return "#df5a5a";
            case Axis.Expand: return "#8ec46a";
            case Axis.Wealth: return "#e3c34a";
            case Axis.Science: return "#8cb8e6";
            case Axis.Culture: return "#b48be6";
            default: return "#57c3ab";
        }
    }
    public static string AxisDesc(Axis a)
    {
        switch (a)
        {
            case Axis.War: return "殺して守る道。迷宮の硬さと侵攻の勝率。";
            case Axis.Expand: return "広げる道。拠点の数と版図の伸び。";
            case Axis.Wealth: return "稼ぐ道。DPと素材、召喚の安さ。";
            case Axis.Science: return "識る道。研究点と天啓、配下の伸び。";
            case Axis.Culture: return "魅せる道。感情と祝祭、名声。";
            default: return "結ぶ道。威名と独立勢力、眷属の器。";
        }
    }

    // ============ ツリー（軸×4段） ============
    public struct NodeDef { public string jpName, desc; }
    private static readonly NodeDef[,] nodes = new NodeDef[AxisCount, Tiers]
    {
        // 軍事
        { N("常在の備え", "防衛体のHP +5%"), N("進撃", "侵攻の戦力 +10%"), N("練度", "侵攻で失う配下 -20%"), N("軍制", "部隊の枠 +1") },
        // 拡張
        { N("開拓令", "支配できる拠点 +1"), N("辺境の鍬", "国境の拡張 +20%"), N("入植の理", "拠点の食料 +1"), N("大遷都", "支配できる拠点 +1") },
        // 経済
        { N("徴税", "領域のDP +10%"), N("交易網", "得る素材 +15%"), N("鋳造", "配下の召喚コスト -10%"), N("商圏", "交易路の上限 +1") },
        // 科学
        { N("書庫", "毎ターン 研究点 +2"), N("学統", "研究のコスト -10%"), N("天啓の座", "天啓の割引 +10%"), N("魔素学", "配下の獲得経験値 +15%") },
        // 文化
        { N("祭祀", "毎ターン 感情 +6"), N("祝祭の作法", "祝祭に要る幸福 -15%"), N("慰撫の詔", "全拠点の不満 -1"), N("伝承", "得る名声 +20%") },
        // 外交
        { N("使者", "毎ターン 威名 +3"), N("盟約の術", "独立勢力への働きかけ -25%"), N("号令", "眷属のLP +4"), N("威圧", "他の魔王の力 -10%") },
    };
    private static NodeDef N(string n, string d) { return new NodeDef { jpName = n, desc = d }; }
    public static NodeDef Node(Axis a, int tier) { return nodes[(int)a, Mathf.Clamp(tier, 0, Tiers - 1)]; }

    // ============ ポイントと取得状況 ============
    private static int[] points;      // 軸ごとの手持ち
    private static int[] earned;      // 軸ごとの累計（表示用）
    private static bool[,] taken;

    private static void EnsureInit()
    {
        if (points != null) return;
        points = new int[AxisCount]; earned = new int[AxisCount];
        taken = new bool[AxisCount, Tiers];
    }
    public static void Reset() { points = null; EnsureInit(); }

    public static int Points(Axis a) { EnsureInit(); return points[(int)a]; }
    public static int Earned(Axis a) { EnsureInit(); return earned[(int)a]; }
    public static bool Taken(Axis a, int tier) { EnsureInit(); return taken[(int)a, Mathf.Clamp(tier, 0, Tiers - 1)]; }
    public static int TotalPoints { get { EnsureInit(); int n = 0; for (int i = 0; i < AxisCount; i++) n += points[i]; return n; } }
    public static int TakenCount
    {
        get
        {
            EnsureInit(); int n = 0;
            for (int a = 0; a < AxisCount; a++) for (int t = 0; t < Tiers; t++) if (taken[a, t]) n++;
            return n;
        }
    }

    /// <summary>偉業（レガシーの道）を達成したときに呼ぶ。</summary>
    public static void AddPoint(Axis a, int n, string reason)
    {
        EnsureInit();
        points[(int)a] += n; earned[(int)a] += n;
        Debug.Log("🎖️『属性』" + AxisName(a) + " +" + n + "（" + reason + "）／手持ち " + points[(int)a]);
        NotifySystem.Push("属性 <b>" + AxisName(a) + " +" + n + "</b>（" + reason + "）", NotifySystem.Kind.Story);
    }

    public static bool CanTake(Axis a, int tier, out string why)
    {
        EnsureInit();
        why = "";
        if (tier < 0 || tier >= Tiers) { why = "そんな段はありません"; return false; }
        if (taken[(int)a, tier]) { why = "すでに取っています"; return false; }
        if (tier > 0 && !taken[(int)a, tier - 1]) { why = "先に『" + Node(a, tier - 1).jpName + "』を取ってください"; return false; }
        if (points[(int)a] < 1) { why = AxisName(a) + "の属性ポイントがありません（" + AxisName(a) + "の偉業で得られます）"; return false; }
        return true;
    }

    public static bool TryTake(Axis a, int tier)
    {
        string why;
        if (!CanTake(a, tier, out why)) { Debug.LogWarning("⚠️ 属性を取れません：" + why); return false; }
        points[(int)a]--; taken[(int)a, tier] = true;
        Debug.Log("🎖️『属性を得た』" + AxisName(a) + "・" + Node(a, tier).jpName + " ― " + Node(a, tier).desc);
        return true;
    }

    private static bool T(Axis a, int tier) { EnsureInit(); return taken[(int)a, tier]; }

    // ============ 効果（各systemはここを見る） ============
    // ── 軍事 ──
    public static float DefenderHpMult { get { return T(Axis.War, 0) ? 1.05f : 1f; } }
    public static float KinPowerMult { get { return T(Axis.War, 1) ? 1.10f : 1f; } }
    public static float KinLossMult { get { return T(Axis.War, 2) ? 0.80f : 1f; } }
    public static int SquadSlotBonus { get { return T(Axis.War, 3) ? 1 : 0; } }
    // ── 拡張 ──
    public static int SettlementLimitBonus { get { return (T(Axis.Expand, 0) ? 1 : 0) + (T(Axis.Expand, 3) ? 1 : 0); } }
    public static float BorderMult { get { return T(Axis.Expand, 1) ? 1.20f : 1f; } }
    public static int FoodBonus { get { return T(Axis.Expand, 2) ? 1 : 0; } }
    // ── 経済 ──
    public static float RegionDpMult { get { return T(Axis.Wealth, 0) ? 1.10f : 1f; } }
    public static float MaterialMult { get { return T(Axis.Wealth, 1) ? 1.15f : 1f; } }
    public static float SummonCostMult { get { return T(Axis.Wealth, 2) ? 0.90f : 1f; } }
    public static int TradeRouteBonus { get { return T(Axis.Wealth, 3) ? 1 : 0; } }
    // ── 科学 ──
    public static int RpPerTurn { get { return T(Axis.Science, 0) ? 2 : 0; } }
    public static float ResearchCostMult { get { return T(Axis.Science, 1) ? 0.90f : 1f; } }
    /// <summary>天啓の割引の上乗せ（0.10＝さらに10%引き）。</summary>
    public static float EurekaExtra { get { return T(Axis.Science, 2) ? 0.10f : 0f; } }
    public static float ExpMult { get { return T(Axis.Science, 3) ? 1.15f : 1f; } }
    // ── 文化 ──
    public static int EmotionPerTurn { get { return T(Axis.Culture, 0) ? 6 : 0; } }
    public static float CelebrateNeedMult { get { return T(Axis.Culture, 1) ? 0.85f : 1f; } }
    public static int UnhappyDelta { get { return T(Axis.Culture, 2) ? -1 : 0; } }
    public static float FameMult { get { return T(Axis.Culture, 3) ? 1.20f : 1f; } }
    // ── 外交 ──
    public static int InfluencePerTurn { get { return T(Axis.Diplo, 0) ? 3 : 0; } }
    public static float IndependentCostMult { get { return T(Axis.Diplo, 1) ? 0.75f : 1f; } }
    public static int KinLpBonus { get { return T(Axis.Diplo, 2) ? 4 : 0; } }
    public static float RivalPowerMult { get { return T(Axis.Diplo, 3) ? 0.90f : 1f; } }

    /// <summary>毎ターンの配り物。</summary>
    public static void TickTurn()
    {
        EnsureInit();
        if (RpPerTurn > 0) ResearchState.AddRP(RpPerTurn);
        var et = EmotionTreeManager.Instance;
        if (et != null && EmotionPerTurn > 0)
            for (int i = 0; i < 4; i++) et.AddEmotion((EmotionTreeManager.Route)i, Mathf.Max(1, EmotionPerTurn / 4));
        if (InfluencePerTurn > 0) DiplomacySystem.AddInfluence(InfluencePerTurn);
    }

    /// <summary>ヘッダ用の一行（手持ちのある軸だけ出す）。</summary>
    public static string HeaderLine()
    {
        EnsureInit();
        string s = "";
        for (int i = 0; i < AxisCount; i++)
            if (points[i] > 0) s += " <color=" + AxisColor((Axis)i) + ">" + AxisName((Axis)i) + points[i] + "</color>";
        return s.Length > 0 ? "<color=#9c95b4>◆属性</color>" + s : "";
    }
}
