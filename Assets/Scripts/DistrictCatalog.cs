using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🏛️ 施設（Civ VI の「地区(District)」に相当）＝地上の中心システム。
///
/// Civ VI の要点をそのまま持ち込んでいる:
/// - **1ヘクスに1施設**。どこに建てるかが最大の意思決定になる（都市のアンスタック）。
/// - **隣接ボーナス**は major +2 / standard +1 / **minor +0.5（合計してから切り捨て）**。
///   例: 魔泉は 山岳+2 / 魔石+2 / 森+1 / 隣の施設+0.5。
/// - **建てていない種類ほど安い**（Civの「一番建てていない地区は40%割引」）＝多様性を促す。
///
/// 産出はすべて**ダンジョン側の資源**に流れ込む（DP/素材/研究点/感情/眷属の戦力）ので、
/// 地上を耕すことがそのまま迷宮の強化になる＝2つの層が噛み合う。
/// 関連: [[SurfaceMap]] [[kin-surface-4x]] / Research(s_district* で解禁)。
/// </summary>
public static class DistrictCatalog
{
    public enum Yield { RP, Emotion, DP, Material, Defense, Warehouse }

    public struct Def
    {
        public string id, jpName, desc, icon, colorHex;
        public Yield yield;
        public int baseCost;
        public string research;      // 解禁研究（""=最初から）
    }

    private static readonly Def[] defs =
    {
        D("manafount", "魔泉",   "研究点を産む。山岳と魔石が力の源。",         "icon_fireball",    "#8cb8e6", Yield.RP,       420, "s_district2"),
        D("altar",     "祭壇",   "感情を産む。森と自然の驚異に惹かれる。",     "icon_skull",       "#c04a6a", Yield.Emotion,  420, "s_district2"),
        D("market",    "交易所", "DPを産む。川と街道が富を運ぶ。",             "icon_crossbow",    "#e3a94a", Yield.DP,       380, ""),
        D("forge",     "鉱錬所", "素材を産む。山岳・丘陵・鉄が要る。",         "icon_hammer",      "#9aa3b0", Yield.Material, 380, ""),
        D("barracks",  "兵舎",   "この領域の防衛と、駐留する眷属の戦力を上げる。", "icon_shield",   "#b478e6", Yield.Defense,  460, "s_district3"),
        // 📦 倉庫（Civ VII の Warehouse building）＝隣接ではなく「同じ都市の版図にある資源の数」で伸びる。
        D("warehouse", "倉庫",   "この都市の版図にある資源タイル1つにつき 素材+1・食料+1。",
                                                                     "icon_hammer",      "#c9a86a", Yield.Warehouse, 440, "s_warehouse"),
    };

    private static Def D(string id, string jp, string desc, string icon, string col, Yield y, int cost, string res)
        => new Def { id = id, jpName = jp, desc = desc, icon = icon, colorHex = col, yield = y, baseCost = cost, research = res };

    public static int Count => defs.Length;
    public static Def Get(int i) => defs[Mathf.Clamp(i, 0, defs.Length - 1)];
    public static bool IsUnlocked(int i)
    {
        var d = Get(i);
        return string.IsNullOrEmpty(d.research) || ResearchState.IsResearched(d.research);
    }
    public static string YieldName(Yield y)
    {
        switch (y)
        {
            case Yield.RP: return "研究点";
            case Yield.Emotion: return "感情";
            case Yield.DP: return "DP";
            case Yield.Material: return "素材";
            case Yield.Warehouse: return "備蓄";
            default: return "防衛";
        }
    }

    // ============ 隣接ボーナス（Civ VI と同じ major+2 / standard+1 / minor+0.5） ============
    /// <summary>その施設をこの領域に建てたときの隣接ボーナス。内訳テキストも返す。</summary>
    public static int Adjacency(int districtIndex, int regionId, out string detail)
    {
        float sum = 0f;
        var parts = new List<string>();
        var neigh = SurfaceMap.Neighbors(regionId);

        // 📦 倉庫だけは別勘定：隣接ではなく **所属する都市の版図にある資源タイルの数** で伸びる。
        //    （Civ VII の Warehouse building が「同種の改良の数だけ」効くのと同じ考え方）
        if (Get(districtIndex).yield == Yield.Warehouse)
        {
            int s = SettlementSystem.SettlementOf(regionId);
            int n = 0;
            if (s >= 0)
                foreach (var t in SettlementSystem.TerritoryOf(s))
                    if (t.resource != SurfaceMap.Resource.None) n++;
            n = Mathf.Min(6, n);
            detail = n > 0 ? "版図の資源タイル×" + n : "版図に資源タイルが無い";
            return n;
        }

        // ⚠ Civ VI と同じく **隣接する6タイルだけ**を数える（自分のタイルは数えない）。
        //    自タイルも数えると値が跳ね上がり、施設が一瞬で元を取ってしまう。
        var tiles = neigh;

        foreach (var t in tiles)
        {
            string where = "";
            switch ((Yield)(int)Get(districtIndex).yield)
            {
                case Yield.RP: // 魔泉：山岳+2 / 魔石+2 / 森+1
                    if (t.terrain == SurfaceMap.Terrain.Mountain) { sum += 2f; parts.Add(where + "山岳+2"); }
                    if (t.resource == SurfaceMap.Resource.Manastone) { sum += 2f; parts.Add(where + "魔石+2"); }
                    if (t.terrain == SurfaceMap.Terrain.Forest) { sum += 1f; parts.Add(where + "森+1"); }
                    break;
                case Yield.Emotion: // 祭壇：自然の驚異+2 / 森+1 / 湿地+1
                    if (t.wonder) { sum += 2f; parts.Add(where + "自然の驚異+2"); }
                    if (t.terrain == SurfaceMap.Terrain.Forest) { sum += 1f; parts.Add(where + "森+1"); }
                    if (t.terrain == SurfaceMap.Terrain.Marsh) { sum += 1f; parts.Add(where + "湿地+1"); }
                    break;
                case Yield.DP: // 交易所：川+2 / 宝石+2 / 穀物・家畜+1
                    //   ※平地は数えない。ありふれた地形を数えるとどこに建てても同じ値になり、
                    //     「置く場所を選ぶ」というCivの肝が消えてしまう（実測で+8固定になった）。
                    if (t.river) { sum += 2f; parts.Add(where + "川+2"); }
                    if (t.resource == SurfaceMap.Resource.Gem) { sum += 2f; parts.Add(where + "宝石+2"); }
                    if (t.resource == SurfaceMap.Resource.Grain || t.resource == SurfaceMap.Resource.Livestock)
                    { sum += 1f; parts.Add(where + SurfaceMap.ResourceName(t.resource) + "+1"); }
                    break;
                case Yield.Material: // 鉱錬所：山岳+2 / 鉄+2 / 丘陵+1 / 良材+1
                    if (t.terrain == SurfaceMap.Terrain.Mountain) { sum += 2f; parts.Add(where + "山岳+2"); }
                    if (t.resource == SurfaceMap.Resource.Iron) { sum += 2f; parts.Add(where + "鉄+2"); }
                    if (t.terrain == SurfaceMap.Terrain.Hills) { sum += 1f; parts.Add(where + "丘陵+1"); }
                    if (t.resource == SurfaceMap.Resource.Timber) { sum += 1f; parts.Add(where + "良材+1"); }
                    break;
                default: // 兵舎：丘陵+2 / 山岳+1 / 砦Lv+1ずつ
                    if (t.terrain == SurfaceMap.Terrain.Hills) { sum += 2f; parts.Add(where + "丘陵+2"); }
                    if (t.terrain == SurfaceMap.Terrain.Mountain) { sum += 1f; parts.Add(where + "山岳+1"); }
                    if (t.fortLevel > 0) { sum += t.fortLevel; parts.Add(where + "砦+" + t.fortLevel); }
                    break;
            }
        }

        // 🏛️ 隣の施設は minor(+0.5)。2つ隣接して初めて+1になる＝施設をまとめて置く動機（Civと同じ）
        int adjD = 0;
        foreach (var t in neigh) if (t.district >= 0 && t.owned) adjD++;
        if (adjD > 0) { sum += adjD * 0.5f; parts.Add("隣の施設×" + adjD + "(+0.5ずつ)"); }

        // 🏙️ 街区(Quarter)：同じタイルに施設が2つ揃うと両方が+2（Civ VII の Quarter そのまま）
        var me = SurfaceMap.Get(regionId);
        if (me.district >= 0 && me.district2 >= 0) { sum += 2f; parts.Add("街区+2"); }

        detail = parts.Count == 0 ? "隣接ボーナスなし" : string.Join(" ／ ", parts.ToArray());
        return Mathf.FloorToInt(sum);   // Civと同じく最後に切り捨て
    }
    public static int Adjacency(int districtIndex, int regionId) { string _; return Adjacency(districtIndex, regionId, out _); }

    // ============ コスト（建てていない種類ほど安い＝Civの多様性ボーナス） ============
    public static int BuiltCount(int districtIndex)
    {
        int n = 0;
        foreach (var r in SurfaceMap.All)
        {
            if (!r.owned) continue;
            if (r.district == districtIndex) n++;
            if (r.district2 == districtIndex) n++;
        }
        return n;
    }
    /// <summary>最も建てていない種類か（＝40%割引の対象）。</summary>
    public static bool IsLeastBuilt(int districtIndex)
    {
        int mine = BuiltCount(districtIndex);
        for (int i = 0; i < defs.Length; i++)
            if (IsUnlocked(i) && BuiltCount(i) < mine) return false;
        return true;
    }
    public static int Cost(int districtIndex)
    {
        var d = Get(districtIndex);
        float c = d.baseCost * (1f + 0.5f * BuiltCount(districtIndex));
        if (IsLeastBuilt(districtIndex)) c *= 0.6f;                       // 一番建てていない種類は40%引き
        if (DemonLord.Instance != null) c *= DemonLord.Instance.DomainCostMult; // 創造ランクで割引
        return Mathf.RoundToInt(c);
    }

    // ============ 建設 ============
    /// <summary>
    /// 施設を建てられる場所か。**都市の版図だけ**（Civ VII で Town が生産キューを持たないのと同じ）。
    /// 1つ目は空きタイルに、2つ目（＝街区）は既に施設のあるタイルに、地上研究『都市法』があれば建てられる。
    /// </summary>
    public static bool CanBuild(int regionId, out bool asQuarter, out string why)
    {
        var r = SurfaceMap.Get(regionId);
        asQuarter = false; why = "";
        if (!r.owned) { why = "自領にしか建てられない"; return false; }
        if (r.isOcean) { why = "海には建てられない"; return false; }
        int s = SettlementSystem.SettlementOf(regionId);
        if (s < 0) { why = "どの拠点の版図でもない（先に拠点を築く）"; return false; }
        if (SurfaceMap.Get(s).settle != SurfaceMap.Settle.City) { why = "拠点(Town)では施設を建てられない ― 都市へ昇格が要る"; return false; }
        if (r.district < 0) return true;
        if (r.district2 >= 0) { why = "このタイルは街区が埋まっている"; return false; }
        if (!ResearchState.IsResearched("s_charter")) { why = "2つ目を重ねるには地上研究『都市法』が要る"; return false; }
        asQuarter = true; return true;
    }

    public static bool TryBuild(int regionId, int districtIndex)
    {
        var r = SurfaceMap.Get(regionId);
        bool asQuarter; string why;
        if (!CanBuild(regionId, out asQuarter, out why)) { Debug.LogWarning("⚠️ " + why); return false; }
        if (!IsUnlocked(districtIndex)) { Debug.LogWarning("⚠️ その施設は地上研究で未解禁です。"); return false; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 建設は準備フェーズのみ可能です。"); return false; }
        int cost = Mathf.RoundToInt(Cost(districtIndex) * (asQuarter ? 1.5f : 1f));   // 街区は割高
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(cost)) { Debug.LogWarning($"⚠️ DP不足で建設できません（要{cost}DP）。"); return false; }
        if (asQuarter) r.district2 = districtIndex; else r.district = districtIndex;
        string detail;
        int adj = Adjacency(districtIndex, regionId, out detail);
        Debug.Log($"🏛️『建設』{r.name} に {Get(districtIndex).jpName} を建てた（-{cost}DP・隣接ボーナス+{adj}／{detail}）"
            + (asQuarter ? " ― <color=#e3c34a>街区が成立（両方+2）</color>" : ""));
        EurekaTracker.OnDistrictBuilt();
        return true;
    }

    // ============ 産出の集計（毎ターン） ============
    /// <summary>自領の施設が生む産出の合計。基礎1＋隣接ボーナス。</summary>
    public static (int rp, int emotion, int dp, int mat, int def) TotalYields()
    {
        int rp = 0, emo = 0, dp = 0, mat = 0, def = 0;
        foreach (var r in SurfaceMap.All)
        {
            if (!r.owned) continue;
            for (int slot = 0; slot < 2; slot++)
            {
                int di = slot == 0 ? r.district : r.district2;
                if (di < 0) continue;
                var d = Get(di);
                int adj = Adjacency(di, r.id);
                // 👷 専門家：Civ VII 1.4.0 と同じく **その施設の隣接ボーナスの100%を追加**
                if (r.specialist) adj *= 2;
                // 施設は「その都市の大きさ」で伸びる（領域の産出は働くタイル側で人口が効くので、
                // ここだけは人口の項を持たせる。施設の数は都市の数で頭打ちなので膨らまない）
                int v = Mathf.RoundToInt((1 + adj) * SurfaceMap.PopMult(r.id) * SettlementSystem.PopBonus(r.id));
                // ⚖️ 換算レート：施設1つが4-6ターンで元を取るくらい。RPと素材は希少なので控えめ。
                switch (d.yield)
                {
                    case Yield.RP: rp += Mathf.CeilToInt(v * 0.5f); break;
                    case Yield.Emotion: emo += v * 2; break;
                    case Yield.DP: dp += v * 14; break;
                    case Yield.Material: mat += Mathf.CeilToInt(v * 0.5f); break;
                    case Yield.Warehouse: mat += v; break;   // 📦 備蓄（食料は FoodIncome 側で加算）
                    default: def += v * 35; break;           // 兵舎は防衛に加算
                }
            }
        }
        return (rp, emo, dp, mat, def);
    }

    /// <summary>📦 倉庫による、その拠点の食料の上乗せ。</summary>
    public static int WarehouseFoodAt(int settlementId)
    {
        int f = 0;
        foreach (var t in SettlementSystem.TerritoryOf(settlementId))
        {
            if (t.district >= 0 && Get(t.district).yield == Yield.Warehouse) f += Adjacency(t.district, t.id);
            if (t.district2 >= 0 && Get(t.district2).yield == Yield.Warehouse) f += Adjacency(t.district2, t.id);
        }
        return f;
    }

    /// <summary>兵舎による、その領域の防衛加算。</summary>
    public static int DefenseBonusAt(int regionId)
    {
        var r = SurfaceMap.Get(regionId);
        if (!r.owned) return 0;
        int d = 0;
        for (int slot = 0; slot < 2; slot++)
        {
            int di = slot == 0 ? r.district : r.district2;
            if (di < 0 || Get(di).yield != Yield.Defense) continue;
            int adj = Adjacency(di, regionId);
            if (r.specialist) adj *= 2;
            d += (1 + adj) * 35;
        }
        return d;
    }

    /// <summary>毎ターンの回収（DP/素材/研究点/感情）。</summary>
    public static void Collect()
    {
        var y = TotalYields();
        if (y.rp == 0 && y.emotion == 0 && y.dp == 0 && y.mat == 0) return;
        var res = DungeonResourceManager.Instance;
        if (res != null) { res.AddDP(y.dp); res.AddMaterial(y.mat); }
        if (y.rp > 0) ResearchState.AddRP(y.rp);
        var et = EmotionTreeManager.Instance;
        if (et != null && y.emotion > 0)
        {
            // 感情はルートに均等に振る（施設が生むのは「熱狂そのもの」）
            for (int i = 0; i < 4; i++) et.AddEmotion((EmotionTreeManager.Route)i, Mathf.Max(1, y.emotion / 4));
        }
        Debug.Log($"🏛️『施設の産出』+{y.dp}DP +{y.mat}素材 +{y.rp}RP +{y.emotion}感情");
    }
}
