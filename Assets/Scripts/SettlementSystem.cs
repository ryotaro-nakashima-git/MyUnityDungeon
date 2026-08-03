using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🏙️ 拠点と都市（Civ VII の Settlement 系をまるごと持ち込む層）。
///
/// **C1までの問題**: 支配したヘクスが全部「人口を持つ都市」だった。271タイル盤だと拠点が数十個になり、
/// Civの「少数の拠点が周囲を耕す」という形になっていなかった。
///
/// **C2の形**（Civ VII 1.4.0 準拠）:
/// - 支配した領域は既定で **版図**（＝最寄りの拠点の領土）。人口も施設も持たない。
/// - DPを払って **拠点(Town)** を築く。拠点は生産キューを持たず、代わりに **特化を1つ**選ぶ。
/// - さらにDPで **都市(City)** へ昇格。都市だけが **施設を建てられ**、**専門家**を置け、版図が広い。
/// - **支配上限**を超えると、超過1つにつき全拠点に不満+1（Civ VIIの -5 Happiness/超過）。
/// - **不満1点につき産出 -5%（最大 -80%）**。※C1までの「不穏＝産出×0.5」の崖を置き換える。
///   → 崖を無くすのは [[difficulty-curve-orders]] の方針そのもの。
/// - **幸福の余剰が貯まると祝祭**（一定ターン産出+25%）。
///
/// 純static・実行時保持（ドメインリロードで初期化）。関連: [[SurfaceMap]] [[DistrictCatalog]] [[civ7-roadmap]]。
/// </summary>
public static class SettlementSystem
{
    // ============ 🎯 拠点の特化（Civ VII の Town Focus 9種） ============
    // Civ VII では Town は生産キューを持たず、代わりに特化を選ぶ。都市は施設が建つので特化は無い。
    public struct FocusDef
    {
        public string jpName, desc, colorHex;
    }

    private static readonly FocusDef[] focuses =
    {
        F("成長の町",   "食料 +50%。人口が速く増える。",                                   "#5cc47c"),
        F("農耕の町",   "耕作しているタイルの食料 +1。",                                   "#8ec46a"),
        F("鉱山の町",   "素材 +2。耕作している丘陵・山岳1つにつき さらに素材 +1。",        "#9aa3b0"),
        F("交易前哨",   "DP +18。さらに幸福 +2（人が集まる）。",                           "#e3a94a"),
        F("中枢の町",   "研究点 +2。版図の施設1つにつき さらに +1。",                      "#8cb8e6"),
        F("砦の町",     "この拠点の守り +120／統治力 +1。",                                "#b478e6"),
        F("供犠の町",   "感情 +6。祭壇が版図にあると さらに +4。",                         "#c04a6a"),
        F("中継の町",   "隣接する自領1つにつき 名声 +1。",                                 "#57c3ab"),
        F("工廠の町",   "素材 +3。装備の鍛造費 -5%（工廠の数だけ・最大 -25%）。",          "#e08a3c"),
    };

    private static FocusDef F(string n, string d, string c) => new FocusDef { jpName = n, desc = d, colorHex = c };

    public static int FocusCount => focuses.Length;
    public static FocusDef Focus(int i) => focuses[Mathf.Clamp(i, 0, focuses.Length - 1)];
    public static string FocusName(int i) => i < 0 ? "未指定" : Focus(i).jpName;

    // ============ 📏 支配上限（Civ VII の Settlement Limit） ============
    /// <summary>
    /// 拠点＋都市をいくつまで持てるか。超過すると全拠点に不満が乗る。
    /// **盤の大きさに比例**させる（固定3だと4,500タイルの盤で身動きが取れない）。
    /// ※Civでも Standard 4,536タイルで1文明が持つのは250〜350タイル程度。
    ///   拠点10×版図37 ≒ 370タイルなので、本家と同じ密度になる。
    /// </summary>
    public static int SettlementLimit
    {
        get
        {
            int n = 4 + SurfaceMap.Count / 700;
            if (ResearchState.IsResearched("s_settle")) n += 1;
            if (ResearchState.IsResearched("s_govern")) n += 1;
            if (ResearchState.IsResearched("s_charter")) n += 2;
            n += AttributeSystem.SettlementLimitBonus;   // 🎖️ 属性『開拓令』『大遷都』
            return n;
        }
    }

    public static int SettlementCount
    {
        get
        {
            int n = 0;
            foreach (var r in SurfaceMap.All) if (r.owned && r.settle != SurfaceMap.Settle.None) n++;
            return n;
        }
    }
    public static int CityCount
    {
        get
        {
            int n = 0;
            foreach (var r in SurfaceMap.All) if (r.owned && r.settle == SurfaceMap.Settle.City) n++;
            return n;
        }
    }
    /// <summary>支配上限の超過ぶん。1つにつき全拠点に不満+1。</summary>
    public static int OverLimit => Mathf.Max(0, SettlementCount - SettlementLimit);

    // ============ 🗺️ 版図（どの拠点がどのタイルを持つか） ============
    /// <summary>拠点の版図の半径。都市は広い。人口が育つと国境が広がる（Civの文化拡張に相当）。</summary>
    public static int TerritoryRadius(SurfaceMap.Region s)
    {
        int rad = s.settle == SurfaceMap.Settle.City ? 2 : 1;
        if (s.pop >= 4) rad += 1;
        return Mathf.Min(rad, 3);
    }

    /// <summary>
    /// 版図を割り当て直す。全拠点から同時にBFSして、**近い拠点が取る**（同距離なら都市が優先）。
    /// 支配していても、どの拠点からも届かないタイルは『未編入の辺境』になり、産出しない。
    /// </summary>
    public static void ReassignTerritory()
    {
        foreach (var r in SurfaceMap.All) r.homeSettlement = -1;

        // 🚩 同じ距離のタイルは**先に手を伸ばした拠点**が取る。種を並べる順がそのまま優先順位になるので、
        //    都市 → 人口の多い順 に並べる。
        //    ※これをしないと、あとから近くに拠点を建てるだけで首都が隣接タイルを全部取られる
        //      （実測で首都の版図が1タイルまで削られた）。
        var seeds = new List<SurfaceMap.Region>();
        foreach (var r in SurfaceMap.All) if (r.owned && r.settle != SurfaceMap.Settle.None) seeds.Add(r);
        seeds.Sort(delegate (SurfaceMap.Region a, SurfaceMap.Region b)
        {
            if (a.settle != b.settle) return a.settle == SurfaceMap.Settle.City ? -1 : 1;
            return b.pop.CompareTo(a.pop);
        });
        var frontier = new List<int>();
        foreach (var r in seeds) { r.homeSettlement = r.id; frontier.Add(r.id); }
        int step = 0;
        while (frontier.Count > 0 && step < 4)
        {
            step++;
            var next = new List<int>();
            foreach (var id in frontier)
            {
                int home = SurfaceMap.Get(id).homeSettlement;
                var hs = SurfaceMap.Get(home);
                if (step > TerritoryRadius(hs)) continue;
                foreach (var n in SurfaceMap.Neighbors(id))
                {
                    if (!n.owned || n.isOcean) continue;
                    if (n.homeSettlement >= 0) continue;      // 先に近い（か優先度の高い）拠点が取っている
                    n.homeSettlement = home; next.Add(n.id);
                }
            }
            frontier = next;
        }
    }

    /// <summary>そのタイルが属する拠点のid（拠点自身なら自分・未編入なら-1）。</summary>
    public static int SettlementOf(int regionId)
    {
        var r = SurfaceMap.Get(regionId);
        if (!r.owned) return -1;
        return r.homeSettlement;
    }

    /// <summary>その拠点の版図（拠点自身を含む）。</summary>
    public static List<SurfaceMap.Region> TerritoryOf(int settlementId)
    {
        var l = new List<SurfaceMap.Region>();
        foreach (var r in SurfaceMap.All) if (r.homeSettlement == settlementId) l.Add(r);
        return l;
    }
    /// <summary>そのタイルが属する拠点の大きさによる倍率（施設だけに掛ける）。</summary>
    public static float PopBonus(int regionId)
    {
        int s = SettlementOf(regionId);
        if (s < 0) return 0f;
        return 1f + 0.12f * Mathf.Max(0, SurfaceMap.Get(s).pop - 1);
    }

    public static int TerritoryCount(int settlementId)
    {
        int n = 0;
        foreach (var r in SurfaceMap.All) if (r.homeSettlement == settlementId) n++;
        return n;
    }

    // ============ 😊 幸福と不満（Civ VII 1.4.0：不満1点＝産出-5%、最大-80%） ============
    public static int UnhappyOf(int id, out string detail)
    {
        var s = SurfaceMap.Get(id);
        var parts = new List<string>();
        int u = 0;
        int over = s.pop - SurfaceMap.GovernanceOf(id);
        if (over > 0) { u += over; parts.Add("人口過密+" + over); }
        if (OverLimit > 0) { u += OverLimit; parts.Add("支配上限の超過+" + OverLimit); }
        int spec = 0;
        foreach (var t in TerritoryOf(id)) if (t.specialist) spec++;
        if (spec > 0) { u += spec; parts.Add("専門家の維持+" + spec); }
        // 敵に睨まれている（Civの厭戦に相当）
        bool front = false;
        foreach (var t in TerritoryOf(id))
            foreach (var n in SurfaceMap.Neighbors(t.id)) if (n.IsRival) { front = true; break; }
        if (front) { u += 1; parts.Add("敵魔王領に接する+1"); }
        int weary = DiplomacySystem.WarWeariness;   // ⚔️ 厭戦（同時に多くの魔王と戦っている）
        if (weary > 0) { u += weary; parts.Add("厭戦+" + weary); }
        int eraU = EraSystem.UnhappyDelta + PolicySystem.UnhappyDelta + AttributeSystem.UnhappyDelta;   // 📜 誓約『静謐』／☄災厄『叛乱』／🏛️政策『慰撫』
        if (eraU != 0) { u += eraU; parts.Add((eraU > 0 ? "災厄" : "誓約") + (eraU > 0 ? "+" : "") + eraU); }
        u = Mathf.Max(0, u);
        detail = parts.Count == 0 ? "なし" : string.Join(" ／ ", parts.ToArray());
        return u;
    }
    public static int UnhappyOf(int id) { string _; return UnhappyOf(id, out _); }

    public static int HappyOf(int id, out string detail)
    {
        var s = SurfaceMap.Get(id);
        var parts = new List<string>();
        int h = 0;
        if (s.settle == SurfaceMap.Settle.City) { h += 1; parts.Add("都市+1"); }
        int dis = 0, won = 0, res = 0;
        foreach (var t in TerritoryOf(id))
        {
            if (t.district >= 0) dis++;
            if (t.district2 >= 0) dis++;
            if (t.wonderIndex >= 0) won++;
            if (t.resource != SurfaceMap.Resource.None && t.resourceAssigned) res++;   // 💎 割り当てた資源だけ幸福に効く
        }
        if (dis > 0) { h += dis; parts.Add("施設×" + dis); }
        if (won > 0) { h += won * 2; parts.Add("遺産×" + won + "(+2ずつ)"); }
        res = Mathf.Min(3, res);      // ※資源は+3で頭打ち（版図が広いだけで無限に幸福になるのを防ぐ）
        if (res > 0) { h += res; parts.Add("資源×" + res); }
        if (s.fortLevel > 0) { h += s.fortLevel; parts.Add("砦+" + s.fortLevel); }
        if (s.focus == 3) { h += 2; parts.Add("交易前哨+2"); }
        if (ResearchState.IsResearched("s_settle")) { h += 1; parts.Add("拠点化+1"); }
        detail = parts.Count == 0 ? "なし" : string.Join(" ／ ", parts.ToArray());
        return h;
    }
    public static int HappyOf(int id) { string _; return HappyOf(id, out _); }

    public static int NetHappy(int id) => HappyOf(id) - UnhappyOf(id);

    /// <summary>
    /// 不満による産出倍率。**1点につき -5%、最大 -80%**（Civ VII 1.4.0 そのまま）。
    /// 段階関数ではなく線形なので、C1までの「不穏で一気に半減」という崖が消える。
    /// </summary>
    public static float HappinessMult(int id)
    {
        int deficit = Mathf.Max(0, -NetHappy(id));
        return Mathf.Clamp(1f - 0.05f * deficit, 0.20f, 1f);
    }

    // ============ 💎 資源の割り当て（S5：Civ VII の Resource Assignment） ============
    //  資源タイルは**版図にあるだけでは効かない**。拠点の「資源枠」に割り当てて初めて、
    //  食料・幸福・倉庫の隣接ボーナスに乗る。枠は 町1／都市2（＋研究で増える）なので、
    //  **都市に昇格させる・研究を進める**ことが資源を活かす鍵になる。
    /// <summary>その拠点が抱えられる資源の数。</summary>
    public static int ResourceSlots(int settlementId)
    {
        var s = SurfaceMap.Get(settlementId);
        int n = s.settle == SurfaceMap.Settle.City ? 2 : 1;
        if (ResearchState.IsResearched("s_warehouse")) n += 1;   // 📦 倉庫術
        if (ResearchState.IsResearched("s_trade")) n += 1;       // 🛤️ 交易の道
        return n;
    }

    /// <summary>資源の価値（割り当ての優先度）。希少なものから枠に入れる。</summary>
    private static int ResourceValue(SurfaceMap.Resource r)
    {
        switch (r)
        {
            case SurfaceMap.Resource.Manastone: return 5;
            case SurfaceMap.Resource.Gem: return 4;
            case SurfaceMap.Resource.Iron: return 3;
            case SurfaceMap.Resource.Grain: return 2;
            case SurfaceMap.Resource.Livestock: return 2;
            case SurfaceMap.Resource.Timber: return 1;
        }
        return 0;
    }

    /// <summary>拠点ごとに、枠の数だけ価値の高い資源へ自動で割り当てる。</summary>
    public static void ReassignResources()
    {
        foreach (var r in SurfaceMap.All) r.resourceAssigned = false;
        foreach (var s in SurfaceMap.All)
        {
            if (!s.owned || s.settle == SurfaceMap.Settle.None) continue;
            var list = new List<SurfaceMap.Region>();
            foreach (var t in TerritoryOf(s.id))
                if (t.resource != SurfaceMap.Resource.None) list.Add(t);
            list.Sort((a, b) => ResourceValue(b.resource).CompareTo(ResourceValue(a.resource)));
            int slots = ResourceSlots(s.id);
            for (int i = 0; i < list.Count && i < slots; i++) list[i].resourceAssigned = true;
        }
    }

    /// <summary>その拠点の資源の使用状況（UI表示用）。</summary>
    public static void ResourceUsage(int settlementId, out int used, out int slots, out int total)
    {
        used = 0; total = 0; slots = ResourceSlots(settlementId);
        foreach (var t in TerritoryOf(settlementId))
        {
            if (t.resource == SurfaceMap.Resource.None) continue;
            total++;
            if (t.resourceAssigned) used++;
        }
    }

    // ============ 🎉 祝祭（Civ VII の Celebration） ============
    public const int CelebrateSpan = 4;      // 続くターン数
    public const float CelebrateMult = 1.15f;
    /// <summary>
    /// 祝祭に必要な幸福の余剰。人口でゆるやかに重くなる。
    /// ※実測メモ: 12固定だと 4/6ターン＝ほぼ常時発動で「産出+25%の常時バフ」になった。
    ///   逆に 16+10*pop にすると、幸福の余剰は人口では増えない（施設と遺産で増える）ので
    ///   人口が育つほど遠のいて**一度も起きなかった**。20+4*pop で7〜9ターンに1回に落ち着く。
    ///   → 掛け算の軸を増やさない [[difficulty-curve-orders]]。
    /// </summary>
    public static int CelebrateNeed(int id)
        => Mathf.Max(8, Mathf.RoundToInt((20 + 4 * Mathf.Max(1, SurfaceMap.Get(id).pop))
            * PolicySystem.CelebrateNeedMult * AttributeSystem.CelebrateNeedMult));   // 🏛️ 政策『祝祭の準備』／🎖️ 属性『祝祭の作法』

    /// <summary>毎ターン：幸福の余剰を貯め、貯まったら祝祭を始める。祝祭中はターンを減らす。</summary>
    public static void TickCelebrations()
    {
        foreach (var r in SurfaceMap.All)
        {
            if (!r.owned || r.settle == SurfaceMap.Settle.None) continue;
            if (r.celebrateTurns > 0)
            {
                r.celebrateTurns--;
                if (r.celebrateTurns == 0) Debug.Log($"🎉『祝祭の終わり』{r.name} の祝祭が終わった");
                continue;
            }
            int net = NetHappy(r.id);
            if (net <= 0) { r.happyStock = Mathf.Max(0, r.happyStock - 1); continue; }
            r.happyStock += net;
            if (r.happyStock >= CelebrateNeed(r.id))
            {
                r.happyStock -= CelebrateNeed(r.id);
                r.celebrateTurns = CelebrateSpan;
                EurekaTracker.OnCelebrate();   // 💡 天啓／🏛️ 祝祭のあいだは政策スロット +1
                Debug.Log($"🎉『祝祭』{r.name} で祝祭が始まった（{CelebrateSpan}ターン・産出×{CelebrateMult}）");
            }
        }
    }

    // ============ 🌱 国境の自動拡張（Civの文化圏拡張） ============
    // Civ では都市が文化で自動的に国境を広げる。1タイルずつ征服して回るゲームではない。
    // ここでも拠点が毎ターン「拡張ポイント」を貯め、貯まったら**版図の半径の内側にある中立タイル**を
    // 1つ併合する。他魔王の領域は取れない（そこは眷属が戦って奪う）。
    // ※半径は 拠点1／都市2／人口4以上で+1（最大3）なので、1拠点が広げられるのは最大37タイル＝Civの3リング。

    /// <summary>その拠点が1ターンに貯める拡張ポイント。</summary>
    public static int BorderGrowth(int id)
    {
        var s = SurfaceMap.Get(id);
        if (s.settle == SurfaceMap.Settle.None) return 0;
        int g = 3 + s.pop * 2;
        if (s.settle == SurfaceMap.Settle.City) g += 4;
        foreach (var t in TerritoryOf(id)) if (t.district >= 0) g += 1;
        if (ResearchState.IsResearched("s_settle")) g += 3;
        int net = NetHappy(id);
        if (net < 0) g = Mathf.Max(0, g + net);      // 不満だと広がらない
        if (s.celebrateTurns > 0) g += 3;            // 🎉 祝祭のあいだは速い
        return Mathf.RoundToInt(g * EraSystem.BorderMult * PolicySystem.BorderMult * AttributeSystem.BorderMult);   // 📜 誓約『開墾』／☄災厄『停滞』／🏛️政策『版図の拡張』
    }
    /// <summary>
    /// 次の1タイルに必要な拡張ポイント（Civと同じく既に取ったぶんだけ高くなる）。
    /// ※実測メモ: 12+6×n だと30ターンで自領13タイルにしかならず、4,500タイルの盤では止まって見えた。
    ///   10+4×n で 1タイルあたり3〜5ターン＝Civの都市の広がり方に近くなる。
    /// </summary>
    public static int BorderCost(int id) => 10 + 4 * Mathf.Max(0, TerritoryCount(id) - 1);

    /// <summary>毎ターン、拠点が国境を1つずつ広げる。</summary>
    public static void GrowBorders()
    {
        var claimed = new List<string>();
        foreach (var s in SurfaceMap.All)
        {
            if (!s.owned || s.settle == SurfaceMap.Settle.None) continue;
            s.borderStock += BorderGrowth(s.id);
            int need = BorderCost(s.id);
            if (s.borderStock < need) continue;

            int rad = TerritoryRadius(s);
            SurfaceMap.Region best = null; int bestScore = int.MinValue;
            foreach (var t in TerritoryOf(s.id))
                foreach (var n in SurfaceMap.Neighbors(t.id))
                {
                    if (n.owner != SurfaceMap.OwnerNeutral || n.isOcean) continue;   // 中立の陸だけ
                    if (SurfaceMap.HexDist(s, n) > rad) continue;                    // 版図の半径の内側だけ
                    // 食料と資源のあるところから取る（Civの「良いタイルから伸びる」）
                    int score = SurfaceMap.FoodOf(n) * 2
                        + (n.resource != SurfaceMap.Resource.None ? 4 : 0)
                        + (n.wonderIndex >= 0 || n.naturalWonder >= 0 ? 6 : 0)
                        + (n.river ? 2 : 0) - SurfaceMap.HexDist(s, n);
                    if (score > bestScore) { bestScore = score; best = n; }
                }
            if (best == null) { s.borderStock = need; continue; }   // 伸ばす先が無いなら貯めたまま待つ
            s.borderStock -= need;
            SurfaceMap.SetOwner(best.id, SurfaceMap.OwnerSelf);
            claimed.Add(s.name + "→" + best.name);
        }
        if (claimed.Count > 0)
            Debug.Log($"🌱『国境の拡張』{claimed.Count}タイルを併合（{string.Join(" ／ ", claimed.ToArray())}）");
    }

    // ============ 🏗️ 拠点を築く／都市へ昇格 ============
    public static int FoundCost() => 220 + 120 * SettlementCount;
    public static int PromoteCost()
    {
        // Civ VII と同じく既存の都市数でコストが上がる
        float c = 480 + 320 * CityCount;
        if (ResearchState.IsResearched("s_charter")) c *= 0.75f;
        return Mathf.RoundToInt(c);
    }

    /// <summary>
    /// 拠点を築ける場所か。**他の拠点から3マス以上**離す（Civ の最小都市間距離）。
    /// 2マスだと隣の拠点が版図を食い合って、どちらも痩せた拠点になってしまう。
    /// </summary>
    public static bool CanFound(int id, out string why)
    {
        var r = SurfaceMap.Get(id);
        why = "";
        if (r.isOcean) { why = "海には築けない"; return false; }
        // 🧭 Civの開拓者と同じで、**まだ支配していない土地にも築ける**（見えていれば送り込める）。
        //    ※自領限定にしていたら、国境が広がるのを待つしかなく拠点が3つまでしか建たなかった（実測）。
        if (!r.owned)
        {
            if (r.owner != SurfaceMap.OwnerNeutral) { why = "他の魔王の領域には築けない（まず奪う）"; return false; }
            if (!SurfaceMap.IsDiscovered(id)) { why = "そこはまだ見えていない"; return false; }
        }
        if (r.settle != SurfaceMap.Settle.None) { why = "既に拠点がある"; return false; }
        foreach (var o in SurfaceMap.All)
        {
            if (o.settle == SurfaceMap.Settle.None || !o.owned) continue;
            if (SurfaceMap.HexDist(o, r) < 3) { why = "他の拠点に近すぎる（3マス以上離す）"; return false; }
        }
        return true;
    }

    public static bool TryFound(int id)
    {
        string why;
        if (!CanFound(id, out why)) { Debug.LogWarning("⚠️ " + why); return false; }
        int cost = FoundCost();
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(cost)) { Debug.LogWarning($"⚠️ DP不足で拠点を築けません（要{cost}DP）。"); return false; }
        var r = SurfaceMap.Get(id);
        if (!r.owned) SurfaceMap.SetOwner(id, SurfaceMap.OwnerSelf);   // 未支配の土地に築いたら、そこが自領になる
        r.settle = SurfaceMap.Settle.Town;
        r.pop = 1; r.foodStock = 0; r.focus = 0; r.borderStock = 0;
        ReassignTerritory();
        Debug.Log($"🏘️『拠点を築く』{r.name} が拠点になった（-{cost}DP・拠点 {SettlementCount}/{SettlementLimit}）"
            + (OverLimit > 0 ? $" ― <color=#e05a5a>支配上限を{OverLimit}超過：全拠点に不満+{OverLimit}</color>" : ""));
        EurekaTracker.OnSettlementFounded();
        return true;
    }

    public static bool TryPromote(int id)
    {
        var r = SurfaceMap.Get(id);
        if (r.settle != SurfaceMap.Settle.Town) { Debug.LogWarning("⚠️ 昇格できるのは拠点だけです。"); return false; }
        if (r.pop < 2) { Debug.LogWarning("⚠️ 人口が2以上ないと都市にできません。"); return false; }
        int cost = PromoteCost();
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(cost)) { Debug.LogWarning($"⚠️ DP不足で昇格できません（要{cost}DP）。"); return false; }
        r.settle = SurfaceMap.Settle.City;
        r.focus = -1;                 // 都市は施設を建てるので特化は持たない（Civ VIIと同じ）
        r.type = SurfaceMap.RegionType.City;
        ReassignTerritory();
        Debug.Log($"🏙️『都市へ昇格』{r.name} が都市になった（-{cost}DP・施設と専門家を置けるようになった）");
        return true;
    }

    public static bool TrySetFocus(int id, int focus)
    {
        var r = SurfaceMap.Get(id);
        if (r.settle != SurfaceMap.Settle.Town) { Debug.LogWarning("⚠️ 特化を選べるのは拠点（都市になる前）だけです。"); return false; }
        r.focus = Mathf.Clamp(focus, 0, focuses.Length - 1);
        Debug.Log($"🎯『特化』{r.name} を〈{Focus(r.focus).jpName}〉にした ― {Focus(r.focus).desc}");
        return true;
    }

    // ============ 👷 専門家（Civ VII 1.4.0：その施設の隣接ボーナスの100%を追加） ============
    public static int SpecialistLimit(int settlementId) => Mathf.Max(0, SurfaceMap.Get(settlementId).pop / 2);

    public static bool CanPlaceSpecialist(int regionId, out string why)
    {
        var t = SurfaceMap.Get(regionId);
        why = "";
        if (!ResearchState.IsResearched("s_specialist")) { why = "地上研究『専門家の登用』が要る"; return false; }
        if (t.district < 0) { why = "施設のあるタイルにしか置けない"; return false; }
        int s = SettlementOf(regionId);
        if (s < 0 || SurfaceMap.Get(s).settle != SurfaceMap.Settle.City) { why = "都市の版図でないと置けない"; return false; }
        int used = 0;
        foreach (var x in TerritoryOf(s)) if (x.specialist) used++;
        if (used >= SpecialistLimit(s)) { why = "専門家の枠が足りない（人口2につき1人）"; return false; }
        return true;
    }

    public static bool TryToggleSpecialist(int regionId)
    {
        var t = SurfaceMap.Get(regionId);
        if (t.specialist)
        {
            t.specialist = false;
            Debug.Log($"👷『専門家を戻す』{t.name} の専門家を外した");
            return true;
        }
        string why;
        if (!CanPlaceSpecialist(regionId, out why)) { Debug.LogWarning("⚠️ " + why); return false; }
        t.specialist = true;
        Debug.Log($"👷『専門家』{t.name} に専門家を置いた（施設の隣接ボーナス2倍・維持費 食料2＋不満1）");
        return true;
    }

    // ============ 🎯 特化の効果 ============
    /// <summary>特化による食料の補正（拠点のみ）。FoodIncome から呼ばれる。</summary>
    public static int FocusFoodBonus(int id, int baseFood)
    {
        var r = SurfaceMap.Get(id);
        if (r.settle != SurfaceMap.Settle.Town || r.focus < 0) return 0;
        if (r.focus == 0) return Mathf.RoundToInt(Mathf.Max(0, baseFood) * 0.5f);            // 成長の町
        if (r.focus == 1) return SurfaceMap.WorkedTiles(id).Count;                            // 農耕の町
        return 0;
    }

    /// <summary>特化による毎ターンの産出（拠点のみ）。倍率は呼び出し側で掛ける。</summary>
    public static (int dp, int mat, int rp, int emo, int fame) FocusYield(int id)
    {
        var r = SurfaceMap.Get(id);
        if (r.settle != SurfaceMap.Settle.Town || r.focus < 0) return (0, 0, 0, 0, 0);
        int dp = 0, mat = 0, rp = 0, emo = 0, fame = 0;
        switch (r.focus)
        {
            case 2:   // 鉱山の町
                mat += 2;
                foreach (var t in SurfaceMap.WorkedTiles(id))
                    if (t.terrain == SurfaceMap.Terrain.Hills || t.terrain == SurfaceMap.Terrain.Mountain) mat += 1;
                break;
            case 3: dp += 18; break;   // 交易前哨
            case 4:                     // 中枢の町
                rp += 2;
                foreach (var t in TerritoryOf(id)) if (t.district >= 0) rp += 1;
                break;
            case 6:                     // 供犠の町
                emo += 6;
                foreach (var t in TerritoryOf(id))
                    if (t.district >= 0 && DistrictCatalog.Get(t.district).yield == DistrictCatalog.Yield.Emotion) { emo += 4; break; }
                break;
            case 7:                     // 中継の町
                foreach (var n in SurfaceMap.Neighbors(id)) if (n.owned) fame += 1;
                break;
            case 8: mat += 3; break;   // 工廠の町
        }
        return (dp, mat, rp, emo, fame);
    }

    /// <summary>『砦の町』による守りの加算。</summary>
    public static int FocusDefense(int id)
    {
        var r = SurfaceMap.Get(id);
        return (r.settle == SurfaceMap.Settle.Town && r.focus == 5) ? 120 : 0;
    }

    /// <summary>『工廠の町』による鍛造費の割引（最大-25%）。</summary>
    public static float ForgeCostMult
    {
        get
        {
            int n = 0;
            foreach (var r in SurfaceMap.All)
                if (r.owned && r.settle == SurfaceMap.Settle.Town && r.focus == 8) n++;
            return 1f - 0.05f * Mathf.Min(5, n);
        }
    }

    // ============ 毎ターン ============
    public static void TickTurn()
    {
        ReassignTerritory();
        GrowBorders();          // 🌱 国境の自動拡張（Civの文化圏）
        ReassignTerritory();    // 広がったぶんを版図に取り込む
        ReassignResources();    // 💎 資源の割り当て（枠の数だけ効く）
        TickCelebrations();
        // 特化の産出をまとめて回収
        int dp = 0, mat = 0, rp = 0, emo = 0, fame = 0;
        foreach (var r in SurfaceMap.All)
        {
            if (!r.owned || r.settle != SurfaceMap.Settle.Town) continue;
            var f = FocusYield(r.id);
            float m = SurfaceMap.PopMult(r.id);
            dp += Mathf.RoundToInt(f.dp * m); mat += Mathf.RoundToInt(f.mat * m);
            rp += Mathf.RoundToInt(f.rp * m); emo += Mathf.RoundToInt(f.emo * m); fame += f.fame;
        }
        if (dp + mat + rp + emo + fame == 0) return;
        var res = DungeonResourceManager.Instance;
        if (res != null) { res.AddDP(dp); res.AddMaterial(mat); res.AddFame(fame); }
        if (rp > 0) ResearchState.AddRP(rp);
        var et = EmotionTreeManager.Instance;
        if (et != null && emo > 0) for (int i = 0; i < 4; i++) et.AddEmotion((EmotionTreeManager.Route)i, Mathf.Max(1, emo / 4));
        Debug.Log($"🎯『拠点の特化』+{dp}DP +{mat}素材 +{rp}RP +{emo}感情 +{fame}名声");
    }

    /// <summary>状態の一行サマリ（地上パネルのヘッダ用）。</summary>
    public static string HeaderLine()
    {
        int worst = 0; string worstName = "";
        int celeb = 0;
        foreach (var r in SurfaceMap.All)
        {
            if (!r.owned || r.settle == SurfaceMap.Settle.None) continue;
            if (r.celebrateTurns > 0) celeb++;
            int d = -NetHappy(r.id);
            if (d > worst) { worst = d; worstName = r.name; }
        }
        string s = "◆拠点 " + (OverLimit > 0 ? "<color=#e05a5a>" : "<color=#e3c34a>") + SettlementCount + "/" + SettlementLimit + "</color>"
            + "（都市" + CityCount + "）";
        if (OverLimit > 0) s += " <color=#e05a5a>上限超過：全拠点に不満+" + OverLimit + "</color>";
        if (worst > 0) s += " <color=#e08a3c>最悪の不満 " + worstName + " -" + (worst * 5) + "%</color>";
        if (celeb > 0) s += " <color=#5cc47c>祝祭" + celeb + "</color>";
        return s;
    }
}
