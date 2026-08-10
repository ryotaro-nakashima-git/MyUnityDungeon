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
    public enum Yield { RP, Emotion, DP, Material, Defense, Warehouse, Training, Food, Influence, Production }

    public struct Def
    {
        public string id, jpName, desc, icon, colorHex;
        public Yield yield;
        public int baseCost;
        public string research;      // 解禁研究（""=最初から）
        /// <summary>
        /// ⏳ その施設が現れる時代（B-1）。**その時代に入るまで建てられない**。
        /// Civ VII が時代ごとに建造物を入れ替えるのと同じで、
        /// 「いま何が建てられるか」が時代ごとに変わるから、地上の見え方が時代で変わる。
        /// </summary>
        public EraSystem.Era era;
    }

    // ⚠⚠ **この配列の並び順を絶対に変えないこと。新しい施設は必ず末尾に足す。**
    //    `SurfaceMap.Region.district` は**この配列のindexを保存している**ので、
    //    並べ替えると既存のセーブで交易所が魔泉に化ける。時代順に見せたいのはUIの都合なので、
    //    表示側でソートする（`SortedForUI`）。
    private static readonly Def[] defs =
    {
        // ── 既存の7種（順番固定）──
        D("manafount", "魔泉",   "研究点を産む。山岳と魔石が力の源。",         "icon_fireball",    "#8cb8e6", Yield.RP,       420, "s_district2", EraSystem.Era.Dawn),
        D("altar",     "祭壇",   "感情を産む。森と自然の驚異に惹かれる。",     "icon_skull",       "#c04a6a", Yield.Emotion,  420, "s_district2", EraSystem.Era.Dawn),
        D("market",    "交易所", "DPを産む。川と街道が富を運ぶ。",             "icon_crossbow",    "#e3a94a", Yield.DP,       380, "", EraSystem.Era.Dawn),
        D("forge",     "鉱錬所", "素材を産む。山岳・丘陵・鉄が要る。",         "icon_hammer",      "#9aa3b0", Yield.Material, 380, "", EraSystem.Era.Dawn),
        D("barracks",  "兵舎",   "この領域の防衛と、駐留する眷属の戦力を上げる。", "icon_shield",   "#b478e6", Yield.Defense,  460, "s_district3", EraSystem.Era.Dawn),
        // 📦 倉庫（Civ VII の Warehouse building）＝隣接ではなく「同じ都市の版図にある資源の数」で伸びる。
        D("warehouse", "倉庫",   "この都市の版図にある資源タイル1つにつき 素材+1・食料+1。",
                                                                     "icon_hammer",      "#c9a86a", Yield.Warehouse, 440, "s_warehouse", EraSystem.Era.Dawn),
        // 🏋️ 訓練所：配下を送り込んで育てる。産出はせず、代わりに迷宮の下層を仕上げる手段になる。
        D("training",  "訓練所", "配下を送り込むと毎ターン鍛えられる（3体まで・4ターン）。丘陵と兵舎が捗る。",
                                                                     "icon_shield",      "#e08a3c", Yield.Training,  520, "s_training", EraSystem.Era.Growth),

        // ── B-1 で追加（ここから末尾に足していく）──
        D("farm",      "農場",   "食料を産む。川と穀物・家畜のそばが実る。",   "icon_axe",         "#5cc47c", Yield.Food,     300, "s_food", EraSystem.Era.Dawn),
        D("harbor",    "港",     "沿岸にしか建たない。食料を産み、海際の街を支える。",
                                                                     "icon_bow",         "#5aa8e0", Yield.Food,     460, "s_navy1", EraSystem.Era.Growth),
        D("bazaar",    "大市場", "交易所の上位。川・宝石・隣の交易所が富を膨らませる。",
                                                                     "icon_crossbow",    "#e3c34a", Yield.DP,       620, "s_market", EraSystem.Era.Growth),
        D("shrine",    "祝祭堂", "感情を産む。遺産と自然の驚異のそばで熱が高まる。",
                                                                     "icon_skull",       "#c04a6a", Yield.Emotion,  600, "s_festival", EraSystem.Era.Growth),
        D("masonry",   "石工場", "素材を産む。山岳と遺産のそばが捗る。",       "icon_hammer",      "#9aa3b0", Yield.Material, 600, "s_wonder", EraSystem.Era.Growth),
        D("embassy",   "使節館", "毎ターンの威名を産む。遺産と川・海のそばが箔になる。",
                                                                     "icon_fire_hand",   "#b48be6", Yield.Influence, 560, "s_influence", EraSystem.Era.Growth),
        D("arsenal",   "造兵廠", "この拠点の軍団の生産力を上げる。丘陵・鉄・隣の兵舎。",
                                                                     "icon_hammer",      "#e05a5a", Yield.Production, 720, "s_cmd2", EraSystem.Era.End),
        D("academy",   "学院",   "研究点を産む。魔石と隣の魔泉が学を深める。", "icon_fireball",    "#8cb8e6", Yield.RP,       760, "s_govern2", EraSystem.Era.End),
        D("hideout",   "隠れ家", "威名を産む。森と湿地に紛れる。",             "icon_dual_sword",  "#6f6889", Yield.Influence, 680, "s_spy", EraSystem.Era.End),
    };

    /// <summary>
    /// UIに並べる順（時代 → コスト）。
    /// ⚠ `defs` そのものを並べ替えてはいけない（indexがセーブに載っている）。並べ替えはここだけ。
    /// </summary>
    public static List<int> SortedForUI()
    {
        var list = new List<int>();
        for (int i = 0; i < defs.Length; i++) list.Add(i);
        list.Sort((a, b) =>
        {
            int c = ((int)defs[a].era).CompareTo((int)defs[b].era);
            if (c != 0) return c;
            return defs[a].baseCost.CompareTo(defs[b].baseCost);
        });
        return list;
    }

    private static Def D(string id, string jp, string desc, string icon, string col, Yield y, int cost, string res, EraSystem.Era era)
        => new Def { id = id, jpName = jp, desc = desc, icon = icon, colorHex = col, yield = y, baseCost = cost, research = res, era = era };

    public static int Count => defs.Length;
    public static Def Get(int i) => defs[Mathf.Clamp(i, 0, defs.Length - 1)];

    /// <summary>研究で解禁されているか（時代は別に見る。UIで「まだ先の時代」と「研究が要る」を書き分けるため）。</summary>
    public static bool IsResearched(int i)
    {
        var d = Get(i);
        return string.IsNullOrEmpty(d.research) || ResearchState.IsResearched(d.research);
    }
    public static bool EraMet(int i) => (int)EraSystem.Current >= (int)Get(i).era;
    public static bool IsUnlocked(int i) => IsResearched(i) && EraMet(i);

    /// <summary>建てられない理由（空なら建てられる）。</summary>
    public static string LockReason(int i)
    {
        if (!EraMet(i)) return EraSystem.EraName(Get(i).era) + "から";
        if (!IsResearched(i)) return "研究が要る";
        return "";
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
            case Yield.Training: return "訓練";
            case Yield.Food: return "食料";
            case Yield.Influence: return "威名";
            case Yield.Production: return "生産力";
            default: return "防衛";
        }
    }

    /// <summary>そのタイルに指定idの施設が建っているか（2つ目の街区スロットも見る）。</summary>
    public static bool HasDistrict(SurfaceMap.Region t, string id)
    {
        if (t == null) return false;
        if (t.district >= 0 && Get(t.district).id == id) return true;
        if (t.district2 >= 0 && Get(t.district2).id == id) return true;
        return false;
    }

    /// <summary>海に面しているか（隣に海がある陸）。⚓ 港の建設条件。</summary>
    public static bool IsCoastal(int regionId)
    {
        var r = SurfaceMap.Get(regionId);
        if (r == null || r.isOcean) return false;
        foreach (var n in SurfaceMap.Neighbors(regionId)) if (n.isOcean) return true;
        return false;
    }

    /// <summary>indexをidで引く（-1＝無い）。</summary>
    public static int IndexOf(string id)
    {
        for (int i = 0; i < defs.Length; i++) if (defs[i].id == id) return i;
        return -1;
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
                    if (t.resource != SurfaceMap.Resource.None && t.resourceAssigned) n++;
            n = Mathf.Min(6, n);
            detail = n > 0 ? "割り当て済みの資源×" + n : "割り当てた資源が無い";
            return n;
        }

        // ⚠ Civ VI と同じく **隣接する6タイルだけ**を数える（自分のタイルは数えない）。
        //    自タイルも数えると値が跳ね上がり、施設が一瞬で元を取ってしまう。
        var tiles = neigh;

        // ⚠ 分岐は **産出(Yield)ではなく施設のid** で切る。
        //   同じ産出の施設が複数できたとき（交易所と大市場、魔泉と学院）、Yieldで切ると
        //   上位の施設が下位とまったく同じ隣接条件になり、置く場所を選び直す意味が消える。
        string me1 = Get(districtIndex).id;
        foreach (var t in tiles)
        {
            switch (me1)
            {
                case "manafount": // 魔泉：山岳+2 / 魔石+2 / 森+1
                    if (t.terrain == SurfaceMap.Terrain.Mountain) { sum += 2f; parts.Add("山岳+2"); }
                    if (t.resource == SurfaceMap.Resource.Manastone) { sum += 2f; parts.Add("魔石+2"); }
                    if (t.terrain == SurfaceMap.Terrain.Forest) { sum += 1f; parts.Add("森+1"); }
                    break;
                case "academy":   // 学院：魔石+2 / 隣の魔泉+2 / 山岳+1
                    if (t.resource == SurfaceMap.Resource.Manastone) { sum += 2f; parts.Add("魔石+2"); }
                    if (HasDistrict(t, "manafount")) { sum += 2f; parts.Add("魔泉+2"); }
                    if (t.terrain == SurfaceMap.Terrain.Mountain) { sum += 1f; parts.Add("山岳+1"); }
                    break;
                case "altar":     // 祭壇：自然の驚異+2 / 森+1 / 湿地+1
                    if (t.wonder) { sum += 2f; parts.Add("自然の驚異+2"); }
                    if (t.terrain == SurfaceMap.Terrain.Forest) { sum += 1f; parts.Add("森+1"); }
                    if (t.terrain == SurfaceMap.Terrain.Marsh) { sum += 1f; parts.Add("湿地+1"); }
                    break;
                case "shrine":    // 祝祭堂：遺産+2 / 自然の驚異+2 / 森+1
                    if (t.wonderIndex >= 0) { sum += 2f; parts.Add("遺産+2"); }
                    if (t.wonder) { sum += 2f; parts.Add("自然の驚異+2"); }
                    if (t.terrain == SurfaceMap.Terrain.Forest) { sum += 1f; parts.Add("森+1"); }
                    break;
                case "market":    // 交易所：川+2 / 宝石+2 / 穀物・家畜+1
                    //   ※平地は数えない。ありふれた地形を数えるとどこに建てても同じ値になり、
                    //     「置く場所を選ぶ」というCivの肝が消えてしまう（実測で+8固定になった）。
                    if (t.river) { sum += 2f; parts.Add("川+2"); }
                    if (t.resource == SurfaceMap.Resource.Gem) { sum += 2f; parts.Add("宝石+2"); }
                    if (t.resource == SurfaceMap.Resource.Grain || t.resource == SurfaceMap.Resource.Livestock)
                    { sum += 1f; parts.Add(SurfaceMap.ResourceName(t.resource) + "+1"); }
                    break;
                case "bazaar":    // 大市場：川+2 / 宝石+2 / 隣の交易所+2 / 沿岸+1
                    if (t.river) { sum += 2f; parts.Add("川+2"); }
                    if (t.resource == SurfaceMap.Resource.Gem) { sum += 2f; parts.Add("宝石+2"); }
                    if (HasDistrict(t, "market")) { sum += 2f; parts.Add("交易所+2"); }
                    if (t.isOcean) { sum += 1f; parts.Add("沿岸+1"); }
                    break;
                case "harbor":    // 港：沿岸+2 / 隣の港+1 / 川+1（沿岸にしか建たない）
                    if (t.isOcean) { sum += 2f; parts.Add("沿岸+2"); }
                    if (HasDistrict(t, "harbor")) { sum += 1f; parts.Add("港+1"); }
                    if (t.river) { sum += 1f; parts.Add("川+1"); }
                    break;
                case "farm":      // 農場：川+2 / 穀物+2 / 家畜+2 / 湿地+1
                    if (t.river) { sum += 2f; parts.Add("川+2"); }
                    if (t.resource == SurfaceMap.Resource.Grain) { sum += 2f; parts.Add("穀物+2"); }
                    if (t.resource == SurfaceMap.Resource.Livestock) { sum += 2f; parts.Add("家畜+2"); }
                    if (t.terrain == SurfaceMap.Terrain.Marsh) { sum += 1f; parts.Add("湿地+1"); }
                    break;
                case "forge":     // 鉱錬所：山岳+2 / 鉄+2 / 丘陵+1 / 良材+1
                    if (t.terrain == SurfaceMap.Terrain.Mountain) { sum += 2f; parts.Add("山岳+2"); }
                    if (t.resource == SurfaceMap.Resource.Iron) { sum += 2f; parts.Add("鉄+2"); }
                    if (t.terrain == SurfaceMap.Terrain.Hills) { sum += 1f; parts.Add("丘陵+1"); }
                    if (t.resource == SurfaceMap.Resource.Timber) { sum += 1f; parts.Add("良材+1"); }
                    break;
                case "masonry":   // 石工場：山岳+2 / 遺産+2 / 丘陵+1
                    if (t.terrain == SurfaceMap.Terrain.Mountain) { sum += 2f; parts.Add("山岳+2"); }
                    if (t.wonderIndex >= 0) { sum += 2f; parts.Add("遺産+2"); }
                    if (t.terrain == SurfaceMap.Terrain.Hills) { sum += 1f; parts.Add("丘陵+1"); }
                    break;
                case "embassy":   // 使節館：遺産+2 / 川+1 / 沿岸+1（人と船が集まるところ）
                    if (t.wonderIndex >= 0) { sum += 2f; parts.Add("遺産+2"); }
                    if (t.river) { sum += 1f; parts.Add("川+1"); }
                    if (t.isOcean) { sum += 1f; parts.Add("沿岸+1"); }
                    break;
                case "hideout":   // 隠れ家：森+2 / 湿地+1 / 山岳+1（隠れられる地形）
                    if (t.terrain == SurfaceMap.Terrain.Forest) { sum += 2f; parts.Add("森+2"); }
                    if (t.terrain == SurfaceMap.Terrain.Marsh) { sum += 1f; parts.Add("湿地+1"); }
                    if (t.terrain == SurfaceMap.Terrain.Mountain) { sum += 1f; parts.Add("山岳+1"); }
                    break;
                case "arsenal":   // 造兵廠：丘陵+2 / 鉄+2 / 隣の兵舎+2
                    if (t.terrain == SurfaceMap.Terrain.Hills) { sum += 2f; parts.Add("丘陵+2"); }
                    if (t.resource == SurfaceMap.Resource.Iron) { sum += 2f; parts.Add("鉄+2"); }
                    if (HasDistrict(t, "barracks")) { sum += 2f; parts.Add("兵舎+2"); }
                    break;
                case "training":  // 訓練所：丘陵+2 / 山岳+1 / 隣の兵舎+2（鍛える場は険しい地形が良い）
                    if (t.terrain == SurfaceMap.Terrain.Hills) { sum += 2f; parts.Add("丘陵+2"); }
                    if (t.terrain == SurfaceMap.Terrain.Mountain) { sum += 1f; parts.Add("山岳+1"); }
                    if (HasDistrict(t, "barracks")) { sum += 2f; parts.Add("兵舎+2"); }
                    break;
                default:          // 兵舎：丘陵+2 / 山岳+1 / 砦Lv+1ずつ
                    if (t.terrain == SurfaceMap.Terrain.Hills) { sum += 2f; parts.Add("丘陵+2"); }
                    if (t.terrain == SurfaceMap.Terrain.Mountain) { sum += 1f; parts.Add("山岳+1"); }
                    if (t.fortLevel > 0) { sum += t.fortLevel; parts.Add("砦+" + t.fortLevel); }
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

    // ============ ⏳ 陳腐化と改築（S5：Civ VII の Obsolete / Overbuild） ============
    /// <summary>その施設が古いか（建てた時代が今より前）。古いと**隣接ボーナスを失う**。</summary>
    public static bool IsObsoleteAt(int regionId, int slot)
    {
        var r = SurfaceMap.Get(regionId);
        int di = slot == 0 ? r.district : r.district2;
        if (di < 0) return false;
        int era = slot == 0 ? r.districtEra : r.district2Era;
        return era < (int)EraSystem.Current;
    }

    /// <summary>実効の隣接ボーナス（陳腐化していれば0）。産出・倉庫・兵舎はすべてこれを見る。</summary>
    public static int EffAdjacency(int regionId, int slot)
    {
        var r = SurfaceMap.Get(regionId);
        int di = slot == 0 ? r.district : r.district2;
        if (di < 0) return 0;
        if (IsObsoleteAt(regionId, slot)) return 0;      // ⏳ 古い施設は隣接ボーナスが消える
        return Adjacency(di, regionId);
    }

    /// <summary>改築の費用（建て直しの半額）。</summary>
    public static int RenovateCost(int regionId, int slot)
    {
        var r = SurfaceMap.Get(regionId);
        int di = slot == 0 ? r.district : r.district2;
        if (di < 0) return 0;
        return Mathf.Max(50, Mathf.RoundToInt(Cost(di) * 0.5f));
    }

    /// <summary>改築＝今の時代の建て方に直す。隣接ボーナスが戻る（専門家の出力も戻る）。</summary>
    public static bool TryRenovate(int regionId, int slot)
    {
        if (!IsObsoleteAt(regionId, slot)) { Debug.LogWarning("⚠️ その施設はまだ古くなっていません。"); return false; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 改築は準備フェーズのみ可能です。"); return false; }
        int cost = RenovateCost(regionId, slot);
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(cost)) { Debug.LogWarning($"⚠️ DP不足で改築できません（要{cost}DP）。"); return false; }
        var r = SurfaceMap.Get(regionId);
        if (slot == 0) r.districtEra = (int)EraSystem.Current; else r.district2Era = (int)EraSystem.Current;
        int di = slot == 0 ? r.district : r.district2;
        Debug.Log($"🔨『改築』{r.name} の {Get(di).jpName} を今の時代の建て方に直した（-{cost}DP・隣接ボーナス+{Adjacency(di, regionId)}が戻った）");
        return true;
    }

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
        if (!IsUnlocked(districtIndex)) { Debug.LogWarning("⚠️ " + Get(districtIndex).jpName + " はまだ建てられません（" + LockReason(districtIndex) + "）。"); return false; }
        // ⚓ 港は沿岸にしか建たない。⚠ ここを緩めると内陸に港が並んで沿岸ボーナスの意味が消える。
        if (Get(districtIndex).id == "harbor" && !IsCoastal(regionId))
        { Debug.LogWarning("⚠️ 港は海に面したタイルにしか建てられません。"); return false; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 建設は準備フェーズのみ可能です。"); return false; }
        int cost = Mathf.RoundToInt(Cost(districtIndex) * (asQuarter ? 1.5f : 1f));   // 街区は割高
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(cost)) { Debug.LogWarning($"⚠️ DP不足で建設できません（要{cost}DP）。"); return false; }
        if (asQuarter) { r.district2 = districtIndex; r.district2Era = (int)EraSystem.Current; }
        else { r.district = districtIndex; r.districtEra = (int)EraSystem.Current; }
        string detail;
        int adj = Adjacency(districtIndex, regionId, out detail);
        Debug.Log($"🏛️『建設』{r.name} に {Get(districtIndex).jpName} を建てた（-{cost}DP・隣接ボーナス+{adj}／{detail}）"
            + (asQuarter ? " ― <color=#e3c34a>街区が成立（両方+2）</color>" : ""));
        EurekaTracker.OnDistrictBuilt();
        return true;
    }

    // ============ 産出の集計（毎ターン） ============
    /// <summary>
    /// 自領の施設が生む産出の合計。基礎1＋隣接ボーナス。
    /// ⚠ **食料と生産力はここには入らない**。どちらも「どの拠点の」という所属が要る値なので、
    ///   食料は <see cref="DistrictFoodAt"/>（→ `SurfaceMap.FoodIncome`）、
    ///   生産力は <see cref="ProductionBonusAt"/>（→ `LegionRoster.ProductionAt`）で拾う。
    ///   全体に足すと、産まない拠点でも軍団が早く出てしまう。
    /// </summary>
    public static (int rp, int emotion, int dp, int mat, int def, int inf) TotalYields()
    {
        int rp = 0, emo = 0, dp = 0, mat = 0, def = 0, inf = 0;
        foreach (var r in SurfaceMap.All)
        {
            if (!r.owned) continue;
            for (int slot = 0; slot < 2; slot++)
            {
                int di = slot == 0 ? r.district : r.district2;
                if (di < 0) continue;
                var d = Get(di);
                int adj = EffAdjacency(r.id, slot);   // ⏳ 古い施設は隣接ボーナスが消える
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
                    case Yield.Influence: inf += Mathf.CeilToInt(v * 0.5f); break;   // 🤝 威名
                    case Yield.Food: break;                  // 🌾 食料は拠点ごと（DistrictFoodAt）
                    case Yield.Production: break;            // 🔨 生産力は拠点ごと（ProductionBonusAt）
                    case Yield.Training: break;              // 🏋️ 訓練所は産出しない（配下を育てるのが役目）
                    default: def += v * 35; break;           // 兵舎は防衛に加算
                }
            }
        }
        return (rp, emo, dp, mat, def, inf);
    }

    /// <summary>📦 倉庫と 🌾 農場・港による、その拠点の食料の上乗せ。</summary>
    public static int WarehouseFoodAt(int settlementId)
    {
        int f = 0;
        foreach (var t in SettlementSystem.TerritoryOf(settlementId))
            for (int slot = 0; slot < 2; slot++)
            {
                int di = slot == 0 ? t.district : t.district2;
                if (di < 0) continue;
                var y = Get(di).yield;
                if (y != Yield.Warehouse && y != Yield.Food) continue;
                int adj = EffAdjacency(t.id, slot);
                if (t.specialist) adj *= 2;
                // 倉庫は隣接ぶんだけ、食料施設は基礎1込み（建てた時点で少しは実る）
                f += y == Yield.Warehouse ? adj : 1 + adj;
            }
        return f;
    }

    /// <summary>🔨 造兵廠による、その拠点の軍団生産力の上乗せ（→ `LegionRoster.ProductionAt`）。</summary>
    public static int ProductionBonusAt(int regionId)
    {
        var r = SurfaceMap.Get(regionId);
        if (r == null || !r.owned) return 0;
        int p = 0;
        for (int slot = 0; slot < 2; slot++)
        {
            int di = slot == 0 ? r.district : r.district2;
            if (di < 0 || Get(di).yield != Yield.Production) continue;
            int adj = EffAdjacency(regionId, slot);
            if (r.specialist) adj *= 2;
            p += (1 + adj) * 2;
        }
        return p;
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
        if (y.rp == 0 && y.emotion == 0 && y.dp == 0 && y.mat == 0 && y.inf == 0) return;
        var res = DungeonResourceManager.Instance;
        if (res != null) { res.AddDP(y.dp); res.AddMaterial(y.mat); }
        if (y.rp > 0) ResearchState.AddRP(y.rp);
        if (y.inf > 0) DiplomacySystem.AddInfluence(y.inf);
        var et = EmotionTreeManager.Instance;
        if (et != null && y.emotion > 0)
        {
            // 感情はルートに均等に振る（施設が生むのは「熱狂そのもの」）
            for (int i = 0; i < 4; i++) et.AddEmotion((EmotionTreeManager.Route)i, Mathf.Max(1, y.emotion / 4));
        }
        Debug.Log($"🏛️『施設の産出』+{y.dp}DP +{y.mat}素材 +{y.rp}RP +{y.emotion}感情 +{y.inf}威名");
    }
}
