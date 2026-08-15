using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 💡 天啓（Civ VI の Eureka / 霊感）＝**やったことが研究を進める**仕組み。
///
/// Civでは「弓兵を3体作る」といったテーマに沿った行動でそのノードのコストの約40%が即座に入る。
/// この作品は研究に進捗の概念が無い（RPを一括で払う）ので、同じ効果を
/// **「条件を満たすとそのノードが40%引きになる」**として実装している。
///
/// これが**ダンジョン層と地上層を噛み合わせる要**になっている:
///   罠で敵を倒す → 罠研究が安くなる ／ 領域を広げる → 地上研究が安くなる ／
///   感情を貯める → 魔王研究が安くなる ／ 個体を育てる → 魔物研究が安くなる。
/// 「遊んでいるうちに次の研究が見えてくる」＝learn-by-doing。
///
/// 純static・実行時保持。関連: [[Research]] [[difficulty-curve-orders]]。
/// </summary>
public static class EurekaTracker
{
    // ⚠ ここを const にすると政策『天啓の記録』が**一生反映されない**（constはコンパイル時に焼き込まれる）。
    public static float Discount
    {
        get
        {
            float off = Mathf.Max(0.4f, PolicySystem.EurekaDiscount > 0f ? PolicySystem.EurekaDiscount : 0.4f);
            off += AttributeSystem.EurekaExtra;   // 🎖️ 属性『天啓の座』
            if (SyncretismSystem.HasId("fae")) off += 0.12f;   // 🜏 習合『妖精種の理』
            return 1f - Mathf.Clamp(off, 0.1f, 0.85f);
        }
    }

    private static HashSet<string> achieved;
    private static void EnsureInit() { if (achieved == null) achieved = new HashSet<string>(); }
    public static void Reset() { achieved = new HashSet<string>(); counters = new Dictionary<string, int>(); }

    public static bool Has(string nodeId) { EnsureInit(); return achieved.Contains(nodeId); }

    // ── 進行度カウンタ（各所から加算される）──
    private static Dictionary<string, int> counters;
    private static void EnsureCounters() { if (counters == null) counters = new Dictionary<string, int>(); }
    public static int Count(string key) { EnsureCounters(); int v; return counters.TryGetValue(key, out v) ? v : 0; }
    /// <summary>🎉 拠点で祝祭が起きた（政策研究『統治の刷新』の天啓）。</summary>
    public static void OnCelebrate() { Add("celebrate"); }
    private static void Add(string key, int n = 1)
    {
        EnsureCounters();
        counters[key] = Count(key) + n;
    }

    // ── 各システムから呼ばれるフック ──
    public static void OnTrapKill() { Add("trapKill"); }
    public static void OnMagicKill() { Add("magicKill"); }
    public static void OnDistrictBuilt() { Add("district"); }
    public static void OnForge(int grade) { Add("forge"); if (grade >= 4) Add("forgeHigh"); }
    public static void OnBossAppointed() { Add("boss"); }
    public static void OnKinNamed() { Add("kin"); }
    public static void OnSettlementFounded() { Add("settlement"); }
    public static void OnAdventurerDefeated() { Add("kill"); }   // ⏳ 時代の偉業が参照する
    /// <summary>🛡️ 備えを張った（→ [[WardSystem]]）。張るほど『先触れ』の研究が安くなる。</summary>
    public static void OnWard() { Add("ward"); }
    /// <summary>🕳️ 落とし穴の行き先を決めた（＝1つ完成させた）。進言を止める判定に使う。</summary>
    public static void OnPitLinked() { Add("pit"); }

    /// <summary>ノードごとの天啓条件。満たしていれば true。</summary>
    private static bool Check(string id)
    {
        var dl = DemonLord.Instance;
        var et = EmotionTreeManager.Instance;
        switch (id)
        {
            // ── 魔物研究：配下を実際に育てる ──
            case "m_evo1": return MinionRoster.All.Count >= 5;
            case "m_evo2": return TopLevel() >= 15;
            case "m_evo3": return TopLevel() >= 30;
            case "m_slot": return DungeonFeatureManager.Instance != null && DungeonFeatureManager.Instance.CurrentSquad.Count >= 4;
            case "m_skill2": return TopLevel() >= 20;

            // ── 領域研究：迷宮を実際に作り込む ──
            case "d_floor4": return DungeonFloorManager.Instance != null && DungeonFloorManager.Instance.BuiltFloorCount >= 3;
            case "d_floor5": return DungeonFloorManager.Instance != null && DungeonFloorManager.Instance.BuiltFloorCount >= 4;
            case "d_trap_poison": return Count("trapKill") >= 5;
            case "d_trap_fire": return Count("trapKill") >= 10;
            case "d_trap_ice": return Count("trapKill") >= 20;
            case "d_trap_shock": return Count("trapKill") >= 30;
            case "d_trap_bleed": return Count("trapKill") >= 15;
            case "d_trap_pow1": return Count("trapKill") >= 25;
            case "d_trap_pow2": return Count("trapKill") >= 50;
            case "d_trap_pow3": return Count("trapKill") >= 90;
            case "d_totem_curse": return TotemsPlaced() >= 2;
            case "d_totem_blood": return TotemsPlaced() >= 4;
            case "d_totem_ritual": return TotemsPlaced() >= 6;
            // 🧠 気性：数がそろうほど「1体ずつ違う」ことに意味が出る
            case "m_temper1": return MinionRoster.All.Count >= 6;
            case "m_temper2": return TopLevel() >= 12;
            // 🕳️ 落とし穴：罠を使っているほど「運ぶ罠」に手が届く
            case "d_trap_pit": return Count("trapKill") >= 12;
            case "d_trap_abyss": return DungeonFloorManager.Instance != null && DungeonFloorManager.Instance.BuiltFloorCount >= 3;
            // 🔭 先触れ：**備えを実際に張る**ほど読みが深くなる（読む→張る→もっと読める）
            case "d_omen1": return Count("kill") >= 20;
            case "d_ward": return Count("kill") >= 30;
            case "d_omen2": return Count("ward") >= 2;
            case "d_omen3": return Count("ward") >= 5;
            case "d_omen4": return Count("ward") >= 10;
            case "d_relic2": return RelicManager.Instance != null && RelicManager.Instance.UnlockedCount >= 4;
            case "d_relic3": return RelicManager.Instance != null && RelicManager.Instance.UnlockedCount >= 8;

            // ── 錬成研究：実際に鍛える ──
            case "r_baitchest": return DungeonResourceManager.Instance != null && DungeonResourceManager.Instance.CraftMaterials >= 20;
            case "r_baitquality": return Count("forge") >= 3;
            case "r_grade_mithril": return Count("forge") >= 6;
            case "r_grade_orichal": return Count("forgeHigh") >= 2;

            // ── 魔王研究：魔王を実際に成長させる ──
            case "k_reprisal": return dl != null && dl.Level >= 5;
            case "k_regen": return dl != null && dl.Level >= 8;
            case "k_slot1": return dl != null && dl.GetStatRank((int)DemonLord.Stat.Knowledge) >= 2;
            case "k_slot2": return dl != null && dl.GetStatRank((int)DemonLord.Stat.Knowledge) >= 4;
            case "k_slot3": return dl != null && dl.GetStatRank((int)DemonLord.Stat.Knowledge) >= 5;
            case "k_emotion": return et != null && et.TotalSpent >= 40;

            // ── 魔法研究：魔法で実際に倒す ──
            case "g_elem_dark": return Count("magicKill") >= 3;
            case "g_elem_fire": return Count("magicKill") >= 6;
            case "g_elem_ice": return Count("magicKill") >= 12;
            case "g_elem_thunder": return Count("magicKill") >= 12;
            case "g_elem_earth": return Count("magicKill") >= 20;
            case "g_elem_light": return Count("magicKill") >= 30;
            case "g_rank1": return Count("magicKill") >= 15;
            case "g_rank2": return Count("magicKill") >= 40;
            case "g_rank3": return Count("magicKill") >= 70;

            // ── 🗺️ 地上研究：地上を実際に耕す ──
            case "s_district1": return SurfaceMap.OwnedCount >= 1;
            case "s_district2": return Count("district") >= 1;
            case "s_district3": return SurfaceMap.OwnedCount >= 3;
            case "s_logistics": return Count("kin") >= 1;
            case "s_settle": return Count("district") >= 3;
            case "s_scout": return SurfaceMap.OwnedCount >= 2;
            case "s_govern": { foreach (var rg in SurfaceMap.All) if (rg.owned && rg.pop >= 3) return true; return false; }
            case "s_voyage": { int n2 = 0; foreach (var rg in SurfaceMap.All) if (rg.owned && rg.isCoast) n2++; return n2 >= 2; }
            case "s_conquer": return RivalLords.AliveCount < RivalLords.Count;
            // 🏛️ S1：政体と政策
            case "p_slot": return Count("celebrate") >= 1;
            case "p_edict": { int ns = 0; for (int i = 0; i < PolicySystem.SlotCount; i++) if (PolicySystem.SlottedAt(i) >= 0) ns++; return ns >= 3; }
            // 🏙️ C2：拠点と都市
            case "s_charter": return SettlementSystem.SettlementCount >= 3;
            case "s_warehouse": { int n3 = 0; foreach (var rg in SurfaceMap.All) if (rg.owned && rg.resource != SurfaceMap.Resource.None) n3++; return n3 >= 3; }
            case "s_specialist": { foreach (var rg in SurfaceMap.All) if (rg.owned && rg.settle == SurfaceMap.Settle.City && rg.pop >= 4) return true; return false; }
            // 🕊️ C5：外交
            case "s_influence": return SettlementSystem.SettlementCount >= 2;
            case "s_trade": return SettlementSystem.CityCount >= 1;
            case "s_accord": return DiplomacySystem.SuzerainCount >= 1;
            case "s_training": return MinionRoster.All.Count >= 8;
        }
        return false;
    }

    private static int TopLevel()
    {
        int m = 0; foreach (var v in MinionRoster.All) if (v.level > m) m = v.level; return m;
    }
    private static int TotemsPlaced()
    {
        var fm = DungeonFeatureManager.Instance;
        return fm != null ? fm.TotemCount : 0;
    }

    /// <summary>毎ターン、達成した天啓を確定させる（達成後は条件が崩れても取り消さない）。</summary>
    public static int Evaluate()
    {
        EnsureInit();
        int n = 0;
        foreach (var node in ResearchCatalog.All)
        {
            if (achieved.Contains(node.id)) continue;
            if (string.IsNullOrEmpty(node.eureka)) continue;
            bool ok = false;
            try { ok = Check(node.id); } catch { ok = false; }
            if (!ok) continue;
            achieved.Add(node.id); n++;
            Debug.Log($"💡『天啓』{node.jpName} ― {node.eureka}（研究コスト40%引き）");
        }
        return n;
    }
}
