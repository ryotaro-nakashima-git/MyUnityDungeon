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
        // ⚔️ 武器の種別（剣/斧/槍/弓/杖/双剣/鎚）。攻撃間隔・射程・威力の"戦い方"が変わる。
        public int weaponType = (int)EquipmentCatalog.WeaponType.Sword;
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
    // 🧬 ファミリー(不死/獣/魔族)ごとの所持数。魔王の種族進化条件（原作の「その系統を多用」）に使う。
    public static int CountOfFamily(ZombieAI.Species fam)
    {
        EnsureInit(); int n = 0;
        foreach (var v in all) if (MinionCatalog.Get(v.catalogIndex).family == fam) n++;
        return n;
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
        ind.weaponType = (int)EquipmentCatalog.DefaultTypeForRole(MinionCatalog.Get(catalogIndex).role); // ⚔️ 役割に合う初期武器種
        all.Add(ind);
        Debug.Log($"🧬【召喚】{MinionCatalog.Get(catalogIndex).jpName} 個体#{ind.id} を召喚（-{cost}DP）");
        return ind;
    }

    // 🧬 育てた個体をそのまま進化させる（CDO2の魔物進化）。Lv・装備は引き継ぎ、種類だけ上位形態へ。
    //    条件：進化先がその個体の種類の子＆研究段階が解禁済み＆DP。※「進化済みを新規召喚」も従来どおり可能。
    public static bool TryEvolveIndividual(int id, int targetCatalogIndex)
    {
        var v = Get(id); if (v == null) return false;
        // 進化先が現在の種類の直系の子か
        bool isChild = false;
        foreach (var c in MinionEvolution.ChildrenOf(v.catalogIndex)) if (c == targetCatalogIndex) { isChild = true; break; }
        if (!isChild) { Debug.LogWarning("⚠️ その形態へは進化できません（直系の進化先ではありません）。"); return false; }
        if (!MinionEvolution.CanIndividualEvolveTo(targetCatalogIndex))
        {
            Debug.LogWarning($"⚠️ 『{MinionEvolution.TierResearchName(targetCatalogIndex)}』の研究が未完了です。");
            return false;
        }
        int cost = MinionEvolution.EvolveCost(targetCatalogIndex);
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(cost)) { Debug.LogWarning($"⚠️ DP不足で進化できません（要{cost}DP）。"); return false; }

        string beforeName = MinionCatalog.Get(v.catalogIndex).jpName;
        v.catalogIndex = targetCatalogIndex;               // Lv・装備はそのまま引き継ぐ
        MinionEvolution.MarkUnlocked(targetCatalogIndex);  // 図鑑でもこの形態を解禁扱いに
        Debug.Log($"🧬【個体進化】{beforeName} 個体#{id}(Lv{v.level}) → {MinionCatalog.Get(targetCatalogIndex).jpName}（-{cost}DP）");
        return true;
    }

    // 戦闘に出した個体を+1Lv（上限MaxLevel）。使うと育つ。
    public static void LevelUp(int id)
    {
        var v = Get(id);
        if (v != null && v.level < MaxLevel) v.level++;
    }

    // ⚔️ 武器種別（剣/斧/…）。切替は無償＝"戦い方"の選択であって強さの購入ではない。
    public static int WeaponTypeOf(int id) { var v = Get(id); return v == null ? 0 : v.weaponType; }
    public static void SetWeaponType(int id, int type)
    {
        var v = Get(id); if (v == null) return;
        v.weaponType = Mathf.Clamp(type, 0, EquipmentCatalog.WeaponTypeCount - 1);
    }
    public static void CycleWeaponType(int id)
    {
        var v = Get(id); if (v == null) return;
        v.weaponType = (v.weaponType + 1) % EquipmentCatalog.WeaponTypeCount;
        Debug.Log($"⚔️【武器種】{MinionCatalog.Get(v.catalogIndex).jpName} 個体#{id} → {EquipmentCatalog.WeaponTypeName(v.weaponType)}");
    }
    // 武器種による 攻撃/間隔/射程（スポーン時にZombieAIへ適用）
    public static float TypeAtkMult(int id) { var v = Get(id); return v == null ? 1f : EquipmentCatalog.WType(v.weaponType).atkMult; }
    public static float TypeIntervalMult(int id) { var v = Get(id); return v == null ? 1f : EquipmentCatalog.WType(v.weaponType).intervalMult; }
    public static float TypeRangeBonus(int id) { var v = Get(id); return v == null ? 0f : EquipmentCatalog.WType(v.weaponType).rangeBonus; }

    // ⚔️🛡️ 個体の装備倍率（PE：装着中の武器/防具グレードから）。未装着(-1)は×1.0。スポーン時に適用。
    public static float EquipAtkMult(int id) { var v = Get(id); return v == null ? 1f : EquipmentCatalog.WeaponAtkMult(v.weaponGrade); }
    public static float EquipHpMult(int id) { var v = Get(id); return v == null ? 1f : EquipmentCatalog.ArmorHpMult(v.armorGrade); }
    // 装着/解除（PEのスロットUIから呼ぶ）。
    public static void Equip(int id, EquipmentCatalog.Slot slot, int grade)
    {
        var v = Get(id); if (v == null) return;
        if (slot == EquipmentCatalog.Slot.Weapon) v.weaponGrade = grade; else v.armorGrade = grade;
    }
    public static int GradeOf(int id, EquipmentCatalog.Slot slot)
    {
        var v = Get(id); if (v == null) return -1;
        return slot == EquipmentCatalog.Slot.Weapon ? v.weaponGrade : v.armorGrade;
    }
    public static void Unequip(int id, EquipmentCatalog.Slot slot) { Equip(id, slot, -1); }

    // 🔨 スロットを1段グレードアップして鍛造・装着（DP消費）。最高グレード/DP不足なら失敗。
    public static bool TryForge(int id, EquipmentCatalog.Slot slot)
    {
        var v = Get(id); if (v == null) return false;
        int cur = slot == EquipmentCatalog.Slot.Weapon ? v.weaponGrade : v.armorGrade;
        int next = cur + 1;
        if (next > EquipmentCatalog.MaxGrade) { Debug.LogWarning("⚠️ 既に最高グレードです。"); return false; }
        // 🔬 錬成研究による上限（既定=銀まで／ミスリル鍛造→ミスリル／オリハルコン鍛造→最高位）＋魔王の錬成ランク補正
        int cap = ResearchState.IsResearched("r_grade_orichal") ? EquipmentCatalog.MaxGrade
                : ResearchState.IsResearched("r_grade_mithril") ? 4 : 3;
        if (DemonLord.Instance != null) cap = Mathf.Min(EquipmentCatalog.MaxGrade, cap + DemonLord.Instance.ForgeGradeBonus);
        if (next > cap)
        {
            Debug.LogWarning("⚠️ これ以上は錬成研究が必要です（" + (cap < 4 ? "ミスリル鍛造" : "オリハルコン鍛造") + "）。");
            return false;
        }
        // 🔨 錬成ランクで鍛造費が安くなる（魔王の錬成ステが活きる）
        float fm = DungeonResourceManager.Instance != null && DemonLord.Instance != null ? DemonLord.Instance.ForgeCostMult : 1f;
        int cost = Mathf.RoundToInt(EquipmentCatalog.ForgeCost(next) * fm);
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(cost)) { Debug.LogWarning($"⚠️ DP不足で鍛造できません（要{cost}DP）。"); return false; }
        if (slot == EquipmentCatalog.Slot.Weapon) v.weaponGrade = next; else v.armorGrade = next;
        string sname = slot == EquipmentCatalog.Slot.Weapon ? "武器" : "防具";
        Debug.Log($"🔨【鍛造】{MinionCatalog.Get(v.catalogIndex).jpName} 個体#{id} の{sname}を『{EquipmentCatalog.Name(next)}』に（-{cost}DP）");
        return true;
    }
}
