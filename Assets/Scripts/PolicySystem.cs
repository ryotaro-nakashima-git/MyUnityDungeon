using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🏛️ 政体と政策スロット（Civ VII の政府＋社会政策／スロットの色は Civ VI 式）。S1。
///
/// 既存の **誓約(Dedication)** とは役割を分けてある：
///   誓約 … 大偉業で解禁し、**時代の変わり目にだけ**選ぶ長期の枠（[[EraSystem]]）
///   政策 … **準備フェーズならいつでも無料で差し替えられる**短期の枠（ここ）
///
/// 仕組み：
/// - **政体**を1つ選ぶ。政体は「常時効果」と「色つきスロットの構成」と「祝祭中のボーナス2択」を持つ。
///   時代の変わり目は無料で選び直せる。途中で変えるにはDPが要る（＝乗り換えの決断）。
/// - **政策カード**は4系統（■戦 ■富 ■秘 ■民）。**同じ色のスロットにしか差せない**。
///   時代が進むと新しいカードが解禁され、**古い時代のカードは効果が半減**する（Civ VII の陳腐化）。
/// - スロットは 政体の色つき枠 ＋ 自由枠（時代・研究・**祝祭中**で増える）。
///
/// ⚠ 効果は**加算を主**にして乗算軸を増やしすぎない（[[difficulty-curve-orders]] の原則）。
/// 純static・実行時保持。関連: [[civ7-gap-plan]] [[EraSystem]] [[SettlementSystem]]。
/// </summary>
public static class PolicySystem
{
    // ============ 系統（スロットの色） ============
    public enum Kind { War = 0, Wealth = 1, Arcane = 2, Civic = 3, Wild = 4 }
    public static string KindName(Kind k)
    {
        switch (k)
        {
            case Kind.War: return "戦";
            case Kind.Wealth: return "富";
            case Kind.Arcane: return "秘";
            case Kind.Civic: return "民";
            default: return "自由";
        }
    }
    public static string KindColor(Kind k)
    {
        switch (k)
        {
            case Kind.War: return "#df5a5a";
            case Kind.Wealth: return "#e3c34a";
            case Kind.Arcane: return "#8cb8e6";
            case Kind.Civic: return "#5cc47c";
            default: return "#b48be6";
        }
    }

    // ============ 政体 ============
    public struct GovDef
    {
        public string jpName, desc;
        public int war, wealth, arcane, civic;   // 色つきスロットの数
        public string colorHex;
        public string festA, festB;              // 祝祭中のボーナス2択（説明）
    }

    private static readonly GovDef[] govs =
    {
        G("恐怖政治", "迷宮に籠もって殺す形。防衛体が硬くなる。", 2, 0, 0, 1, "#df5a5a",
          "祝祭中：自領の守り +120", "祝祭中：毎ターン 感情 +30"),
        G("収奪王政", "地上から吸い上げる形。領域の実入りが増える。", 1, 2, 0, 0, "#e3c34a",
          "祝祭中：領域のDP +25%", "祝祭中：毎ターン 素材 +8"),
        G("秘儀結社", "知を積む形。研究が速い。", 0, 0, 2, 1, "#8cb8e6",
          "祝祭中：毎ターン 研究点 +6", "祝祭中：配下の獲得経験値 +25%"),
        G("群狼同盟", "外へ出て獲る形。眷属が軽い。", 1, 1, 1, 0, "#5cc47c",
          "祝祭中：侵攻の戦力 +20%", "祝祭中：眷属の移動力 +1"),
    };
    private static GovDef G(string n, string d, int w, int g, int a, int c, string col, string fa, string fb)
        => new GovDef { jpName = n, desc = d, war = w, wealth = g, arcane = a, civic = c, colorHex = col, festA = fa, festB = fb };

    public static int GovCount { get { return govs.Length; } }
    public static GovDef Gov(int i) { return govs[Mathf.Clamp(i, 0, govs.Length - 1)]; }

    private static int govIndex = 0;
    public static int GovIndex { get { return govIndex; } }
    public static GovDef CurrentGov { get { return Gov(govIndex); } }
    /// <summary>祝祭中に効くボーナスの選択（0=A / 1=B）。いつでも切り替えられる。</summary>
    public static int FestivalChoice = 0;

    /// <summary>時代の変わり目は無料。途中で乗り換えるならDPが要る。</summary>
    public static int SwitchCost { get { return 400 + 200 * (int)EraSystem.Current; } }
    private static int govChosenEra = -1;
    public static bool IsFreeSwitch { get { return govChosenEra != (int)EraSystem.Current; } }

    public static bool TrySetGov(int i)
    {
        EnsureInit();
        i = Mathf.Clamp(i, 0, govs.Length - 1);
        if (i == govIndex) return false;
        if (!IsFreeSwitch)
        {
            var res = DungeonResourceManager.Instance;
            if (res != null && !res.TrySpendDP(SwitchCost))
            {
                Debug.LogWarning("⚠️ 政体の乗り換えにDPが足りません（要" + SwitchCost + "／時代の変わり目なら無料）。");
                return false;
            }
        }
        govIndex = i; govChosenEra = (int)EraSystem.Current;
        PruneSlots();
        Debug.Log("🏛️『政体』" + CurrentGov.jpName + " ― " + CurrentGov.desc + "（スロット " + SlotSummary() + "）");
        return true;
    }

    // ============ 政策カード ============
    public struct PolicyDef
    {
        public string id, jpName, desc;
        public Kind kind;
        public EraSystem.Era era;      // この時代から解禁。古い時代のカードは効果が半減する
    }

    private static readonly PolicyDef[] policies =
    {
        // ── ■戦（迷宮の守りと侵攻）──
        P("w_trap",   Kind.War,    EraSystem.Era.Dawn,   "罠の刻印",   "罠のダメージ +15%"),
        P("w_flesh",  Kind.War,    EraSystem.Era.Dawn,   "肉の壁",     "防衛体のHP +8%"),
        P("w_loot",   Kind.War,    EraSystem.Era.Growth, "略奪の作法", "侵攻で失う配下 -30%"),
        P("w_fort",   Kind.War,    EraSystem.Era.Growth, "城塞化",     "自領すべての守り +60"),
        P("w_levy",   Kind.War,    EraSystem.Era.End,    "総動員",     "部隊の枠 +1"),
        // ── ■富（実入り）──
        P("g_tax",    Kind.Wealth, EraSystem.Era.Dawn,   "徴発",       "領域のDP +15%"),
        P("g_chest",  Kind.Wealth, EraSystem.Era.Dawn,   "撒き餌",     "生還した冒険者から得るDP +20%"),
        P("g_road",   Kind.Wealth, EraSystem.Era.Growth, "隊商路",     "交易路の上限 +1"),
        P("g_relic",  Kind.Wealth, EraSystem.Era.Growth, "遺物市場",   "得る素材 +25%"),
        P("g_gold",   Kind.Wealth, EraSystem.Era.End,    "黄金律",     "配下の召喚コスト -15%"),
        // ── ■秘（研究と魔）──
        P("a_codex",  Kind.Arcane, EraSystem.Era.Dawn,   "写本の蒐集", "毎ターン 研究点 +3"),
        P("a_eureka", Kind.Arcane, EraSystem.Era.Dawn,   "天啓の記録", "天啓の割引 40% → 55%"),
        P("a_mana",   Kind.Arcane, EraSystem.Era.Growth, "魔素の精製", "配下の獲得経験値 +20%"),
        P("a_magic",  Kind.Arcane, EraSystem.Era.Growth, "秘儀の伝授", "魔法の威力 +10%"),
        P("a_evo",    Kind.Arcane, EraSystem.Era.End,    "進化の秘術", "進化のコスト -20%"),
        // ── ■民（拠点と版図）──
        P("c_calm",   Kind.Civic,  EraSystem.Era.Dawn,   "慰撫",       "全拠点の不満 -1"),
        P("c_farm",   Kind.Civic,  EraSystem.Era.Dawn,   "開墾",       "拠点の食料 +1"),
        P("c_border", Kind.Civic,  EraSystem.Era.Growth, "版図の拡張", "国境の拡張 +25%"),
        P("c_fest",   Kind.Civic,  EraSystem.Era.Growth, "祝祭の準備", "祝祭に要る幸福 -20%"),
        P("c_faith",  Kind.Civic,  EraSystem.Era.End,    "万民の帰依", "眷属のLP +6"),
    };
    private static PolicyDef P(string id, Kind k, EraSystem.Era e, string n, string d)
        => new PolicyDef { id = id, kind = k, era = e, jpName = n, desc = d };

    public static int PolicyCount { get { return policies.Length; } }
    public static PolicyDef Policy(int i) { return policies[Mathf.Clamp(i, 0, policies.Length - 1)]; }
    public static int IndexOf(string id)
    {
        for (int i = 0; i < policies.Length; i++) if (policies[i].id == id) return i;
        return -1;
    }
    /// <summary>その時代に到達していれば手札に入る。</summary>
    public static bool IsUnlocked(int i) { return (int)Policy(i).era <= (int)EraSystem.Current; }
    /// <summary>⏳ 古い時代のカードは効果が半減する（Civ VII の陳腐化）。</summary>
    public static bool IsObsolete(int i) { return (int)Policy(i).era < (int)EraSystem.Current; }
    public static float PowerOf(int i) { return IsObsolete(i) ? 0.5f : 1f; }

    // ============ スロット ============
    private static List<int> slotted;     // スロット番号 → 政策index（-1＝空）
    private static void EnsureInit() { if (slotted == null) slotted = new List<int>(); }

    public static void Reset()
    {
        slotted = new List<int>(); govIndex = 0; govChosenEra = -1; FestivalChoice = 0;
    }

    /// <summary>研究『統治の刷新』で自由枠 +1。</summary>
    private static int ResearchSlots { get { return ResearchState.IsResearched("p_slot") ? 1 : 0; } }
    /// <summary>🎉 どこかの拠点が祝祭中なら自由枠 +1（Civ VII の祝宴＝政策スロット+1）。</summary>
    public static bool AnyCelebrating
    {
        get
        {
            foreach (var r in SurfaceMap.All) if (r.owned && r.celebrateTurns > 0) return true;
            return false;
        }
    }
    /// <summary>時代で増える自由枠（胎動0 / 伸長+1 / 終焉+2）。</summary>
    private static int EraSlots { get { return (int)EraSystem.Current; } }

    /// <summary>スロットの並び（色つき→自由）。</summary>
    public static List<Kind> SlotLayout()
    {
        var g = CurrentGov;
        var l = new List<Kind>();
        for (int i = 0; i < g.war; i++) l.Add(Kind.War);
        for (int i = 0; i < g.wealth; i++) l.Add(Kind.Wealth);
        for (int i = 0; i < g.arcane; i++) l.Add(Kind.Arcane);
        for (int i = 0; i < g.civic; i++) l.Add(Kind.Civic);
        int wild = EraSlots + ResearchSlots + (AnyCelebrating ? 1 : 0);
        for (int i = 0; i < wild; i++) l.Add(Kind.Wild);
        return l;
    }
    public static int SlotCount { get { return SlotLayout().Count; } }
    public static string SlotSummary()
    {
        var l = SlotLayout();
        int w = 0, g = 0, a = 0, c = 0, wi = 0;
        foreach (var k in l)
        {
            if (k == Kind.War) w++; else if (k == Kind.Wealth) g++; else if (k == Kind.Arcane) a++;
            else if (k == Kind.Civic) c++; else wi++;
        }
        return "戦" + w + "・富" + g + "・秘" + a + "・民" + c + (wi > 0 ? "・自由" + wi : "");
    }

    /// <summary>スロットに入っている政策index（-1＝空）。</summary>
    public static int SlottedAt(int slot)
    {
        EnsureInit();
        return (slot >= 0 && slot < slotted.Count) ? slotted[slot] : -1;
    }
    public static bool IsActive(int policyIndex)
    {
        EnsureInit();
        foreach (int p in slotted) if (p == policyIndex) return true;
        return false;
    }

    /// <summary>スロットが減ったとき／色が変わったときに、差せないカードを押し出す。</summary>
    private static void PruneSlots()
    {
        EnsureInit();
        var layout = SlotLayout();
        while (slotted.Count < layout.Count) slotted.Add(-1);
        while (slotted.Count > layout.Count) slotted.RemoveAt(slotted.Count - 1);
        for (int i = 0; i < slotted.Count; i++)
        {
            int p = slotted[i];
            if (p < 0) continue;
            if (!IsUnlocked(p) || (layout[i] != Kind.Wild && layout[i] != Policy(p).kind)) slotted[i] = -1;
        }
    }

    public static bool CanSlot(int slot, int policyIndex, out string why)
    {
        why = "";
        PruneSlots();
        var layout = SlotLayout();
        if (slot < 0 || slot >= layout.Count) { why = "そのスロットはありません"; return false; }
        if (policyIndex < 0) return true;                                   // 空にするのは常にできる
        if (!IsUnlocked(policyIndex)) { why = "まだ解禁されていません（" + EraSystem.EraName(Policy(policyIndex).era) + "から）"; return false; }
        if (IsActive(policyIndex)) { why = "すでに差してあります"; return false; }
        if (layout[slot] != Kind.Wild && layout[slot] != Policy(policyIndex).kind)
        { why = "色が合いません（" + KindName(layout[slot]) + "のスロット）"; return false; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase && !ResearchState.IsResearched("p_edict"))
        { why = "差し替えは準備フェーズだけ（研究『布告の権』で戦闘中も可）"; return false; }
        return true;
    }

    public static bool TrySlot(int slot, int policyIndex)
    {
        string why;
        if (!CanSlot(slot, policyIndex, out why)) { Debug.LogWarning("⚠️ 政策を差せません：" + why); return false; }
        slotted[slot] = policyIndex;
        if (policyIndex >= 0)
            Debug.Log("🏛️『政策』" + Policy(policyIndex).jpName + " ― " + Policy(policyIndex).desc
                + (IsObsolete(policyIndex) ? "（陳腐化：効果は半分）" : ""));
        return true;
    }

    /// <summary>毎ターン：スロットの整合を取り、毎ターン効果を配る。</summary>
    public static void TickTurn()
    {
        PruneSlots();
        if (RpPerTurnTotal > 0) ResearchState.AddRP(RpPerTurnTotal);
        var res = DungeonResourceManager.Instance;
        if (res != null && MaterialPerTurn > 0) res.AddMaterial(MaterialPerTurn);
        var et = EmotionTreeManager.Instance;
        if (et != null && EmotionPerTurn > 0)
            for (int i = 0; i < 4; i++) et.AddEmotion((EmotionTreeManager.Route)i, Mathf.Max(1, EmotionPerTurn / 4));
    }

    // ============ 効果（各systemはここを見る） ============
    // 差してあれば value、陳腐化なら半分。乗算は「1 + 効果」の形にして掛け算軸を増やさない。
    private static float V(string id, float value)
    {
        int i = IndexOf(id);
        return (i >= 0 && IsActive(i)) ? value * PowerOf(i) : 0f;
    }
    private static int Vi(string id, int value)
    {
        int i = IndexOf(id);
        if (i < 0 || !IsActive(i)) return 0;
        return IsObsolete(i) ? Mathf.Max(value > 0 ? 1 : -1, Mathf.RoundToInt(value * 0.5f)) : value;
    }
    private static bool On(string id) { int i = IndexOf(id); return i >= 0 && IsActive(i); }
    /// <summary>🎉 祝祭中で、かつその選択をしているか。</summary>
    private static bool Fest(int choice) { return AnyCelebrating && FestivalChoice == choice; }

    // ── ■戦 ──
    public static float TrapDamageMult { get { return 1f + V("w_trap", 0.15f); } }
    public static float DefenderHpMult { get { return 1f + V("w_flesh", 0.08f); } }
    public static float KinLossMult { get { return 1f - V("w_loot", 0.30f); } }
    public static int TerritoryDefense
    {
        get
        {
            int d = Vi("w_fort", 60);
            if (govIndex == 0 && Fest(0)) d += 120;      // 恐怖政治の祝祭A
            return d;
        }
    }
    public static int SquadSlotBonus { get { return Vi("w_levy", 1); } }

    // ── ■富 ──
    public static float RegionDpMult
    {
        get { return 1f + V("g_tax", 0.15f) + ((govIndex == 1 && Fest(0)) ? 0.25f : 0f); }
    }
    public static float ChestDpMult { get { return 1f + V("g_chest", 0.20f); } }
    public static int TradeRouteBonus { get { return Vi("g_road", 1); } }
    public static float MaterialMult { get { return 1f + V("g_relic", 0.25f); } }
    public static float SummonCostMult { get { return 1f - V("g_gold", 0.15f); } }

    // ── ■秘 ──
    public static int RpPerTurn { get { return Vi("a_codex", 3) + ((govIndex == 2 && Fest(0)) ? 6 : 0); } }
    public static float EurekaDiscount { get { return On("a_eureka") ? (IsObsolete(IndexOf("a_eureka")) ? 0.48f : 0.55f) : 0f; } }
    public static float ExpMult
    {
        get { return 1f + V("a_mana", 0.20f) + ((govIndex == 2 && Fest(1)) ? 0.25f : 0f); }
    }
    public static float MagicPowerMult { get { return 1f + V("a_magic", 0.10f); } }
    public static float EvolveCostMult { get { return 1f - V("a_evo", 0.20f); } }

    // ── ■民 ──
    public static int UnhappyDelta { get { return -Vi("c_calm", 1); } }
    public static int FoodBonus { get { return Vi("c_farm", 1); } }
    public static float BorderMult { get { return 1f + V("c_border", 0.25f); } }
    public static float CelebrateNeedMult { get { return 1f - V("c_fest", 0.20f); } }
    public static int KinLpBonus { get { return Vi("c_faith", 6); } }

    // ── 政体の常時効果＋祝祭の残り ──
    /// <summary>防衛体のHP倍率（政体『恐怖政治』の常時効果を含む）。</summary>
    public static float DefenderHpTotal { get { return DefenderHpMult * (govIndex == 0 ? 1.10f : 1f); } }
    /// <summary>領域DPの倍率（政体『収奪王政』の常時効果を含む）。</summary>
    public static float RegionDpTotal { get { return RegionDpMult * (govIndex == 1 ? 1.10f : 1f); } }
    /// <summary>眷属の移動力（政体『群狼同盟』＋祝祭B）。</summary>
    public static int KinMoveBonus { get { return (govIndex == 3 ? 1 : 0) + ((govIndex == 3 && Fest(1)) ? 1 : 0); } }
    /// <summary>侵攻の戦力倍率（群狼同盟の祝祭A）。</summary>
    public static float KinPowerMult { get { return (govIndex == 3 && Fest(0)) ? 1.20f : 1f; } }
    /// <summary>毎ターンの研究点（秘儀結社の常時効果を含む）。</summary>
    public static int RpPerTurnTotal { get { return RpPerTurn + (govIndex == 2 ? 2 : 0); } }
    /// <summary>毎ターンの素材（収奪王政の祝祭B）。</summary>
    public static int MaterialPerTurn { get { return (govIndex == 1 && Fest(1)) ? 8 : 0; } }
    /// <summary>毎ターンの感情（恐怖政治の祝祭B）。</summary>
    public static int EmotionPerTurn { get { return (govIndex == 0 && Fest(1)) ? 30 : 0; } }

    /// <summary>ヘッダ用の一行。</summary>
    public static string HeaderLine()
    {
        PruneSlots();
        var g = CurrentGov;
        string s = "<color=" + g.colorHex + ">" + g.jpName + "</color> <size=90%><color=#9c95b4>" + SlotSummary() + "</color></size>";
        int used = 0;
        foreach (int p in slotted) if (p >= 0) used++;
        s += " <size=90%>" + used + "/" + SlotCount + "</size>";
        if (AnyCelebrating) s += " <color=#5cc47c>祝祭+1</color>";
        return s;
    }
}
