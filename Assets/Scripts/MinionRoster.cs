using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 配下の「個体」ロスター（CDO2の魔物召喚方式）。
/// - 図鑑で種類を選び「召喚」→ DPを消費して Lv1 の個体を1体ロスターに追加。同じ種類を何体でも持てる。
/// - マップ配置時は DP消費なし（配置=どの個体を出すか選ぶだけ）。編成上限コスト（1部屋◯コスト等）は採用しない。
/// - 個体は Lv を持ち、戦闘に出す（=使う）と +1Lv 育つ。Lvで配置時の HP/ATK が上昇。
/// - 純static・実行時保持（セーブ未実装＝ドメインリロードでリセット）。関連: [[MinionCatalog]] [[MinionEvolution]] / DungeonFeatureManager(配置)。
/// </summary>
public static class MinionRoster
{
    public class Individual
    {
        public int id;            // 一意な個体ID
        public int catalogIndex;  // 種類（MinionCatalog index）
        public int level = 1;     // 個体レベル（1..MaxLevel）
        // ⚔️🛡️ 装備スロット（PE：CDO2風の武器/防具装着。-1=素手/素肌）。装着UIは後続、データ土台とスポーン適用は先に用意。
        public int weaponGrade = -1;
        public int armorGrade = -1;
    }

    public const int MaxLevel = 50;
    public const float PerLevel = 0.04f;      // Lvあたりの HP/ATK 上昇率（+4%/Lv）
    private const float SummonDpPerTier = 15f; // 召喚DP = ティア × これ（ランクが高い＝ティアが高いほど高コスト）

    private static List<Individual> all;
    private static int nextId = 1;
    private static void EnsureInit() { if (all == null) all = new List<Individual>(); }

    public static void Reset() { all = new List<Individual>(); nextId = 1; }
    public static IReadOnlyList<Individual> All { get { EnsureInit(); return all; } }

    public static List<Individual> ByType(int catalogIndex)
    {
        EnsureInit(); var l = new List<Individual>();
        foreach (var v in all) if (v.catalogIndex == catalogIndex) l.Add(v);
        return l;
    }
    public static int CountOfType(int catalogIndex)
    {
        EnsureInit(); int n = 0; foreach (var v in all) if (v.catalogIndex == catalogIndex) n++; return n;
    }
    public static int TopLevelOfType(int catalogIndex)
    {
        EnsureInit(); int m = 0; foreach (var v in all) if (v.catalogIndex == catalogIndex && v.level > m) m = v.level; return m;
    }
    public static Individual Get(int id)
    {
        EnsureInit(); foreach (var v in all) if (v.id == id) return v; return null;
    }
    public static int LevelOf(int id) { var v = Get(id); return v != null ? v.level : 1; }

    // 個体レベル → 配置時の倍率（HP/ATK）。Lv1=×1.0、Lv50≈×2.96。
    public static float LevelMult(int level) { return 1f + (Mathf.Clamp(level, 1, MaxLevel) - 1) * PerLevel; }

    // 召喚コスト（DP）。ティア（＝ランク）が高いほど高い。創造ランクの DefenderCostMult も反映。
    public static int SummonCost(int catalogIndex)
    {
        float mult = DemonLord.Instance != null ? DemonLord.Instance.DefenderCostMult : 1f;
        return Mathf.RoundToInt(MinionCatalog.Get(catalogIndex).tierCP * SummonDpPerTier * mult);
    }

    // 召喚（DP消費して Lv1 個体を追加）。未解禁/DP不足なら null。
    public static Individual TrySummon(int catalogIndex)
    {
        EnsureInit();
        if (!MinionEvolution.IsUnlocked(catalogIndex)) { Debug.LogWarning("⚠️ 未解禁の種類は召喚できません（先に進化で解禁）。"); return null; }
        int cost = SummonCost(catalogIndex);
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(cost)) { Debug.LogWarning($"⚠️ DP不足で召喚できません（要{cost}DP）。"); return null; }
        var ind = new Individual { id = nextId++, catalogIndex = catalogIndex, level = 1 };
        all.Add(ind);
        Debug.Log($"🧬【召喚】{MinionCatalog.Get(catalogIndex).jpName} 個体#{ind.id} を召喚（-{cost}DP）");
        return ind;
    }

    // 戦闘に出した個体を+1Lv（上限MaxLevel）。使うと育つ。
    public static void LevelUp(int id)
    {
        var v = Get(id);
        if (v != null && v.level < MaxLevel) v.level++;
    }

    // ⚔️🛡️ 個体の装備倍率（PE：装着中の武器/防具グレードから）。未装着(-1)は×1.0。スポーン時に適用。
    public static float EquipAtkMult(int id) { var v = Get(id); return v == null ? 1f : EquipmentCatalog.WeaponAtkMult(v.weaponGrade); }
    public static float EquipHpMult(int id) { var v = Get(id); return v == null ? 1f : EquipmentCatalog.ArmorHpMult(v.armorGrade); }
    // 装着/解除（PEのスロットUIから呼ぶ）。
    public static void Equip(int id, EquipmentCatalog.Slot slot, int grade)
    {
        var v = Get(id); if (v == null) return;
        if (slot == EquipmentCatalog.Slot.Weapon) v.weaponGrade = grade; else v.armorGrade = grade;
    }
}
