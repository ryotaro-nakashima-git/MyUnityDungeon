using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 配下の『個体』ロスター（CDO2の魔物召喚方式）。
/// - 図鑑で種類を選び『召喚』→ DPを消費して Lv1 の個体を1体ロスターに追加。同じ種類を何体でも持てる。
/// - マップ配置時は DP消費なし（配置=どの個体を出すか選ぶだけ）。編成上限コスト（1部屋◯コスト等）は採用しない。
/// - 個体は Lv と 経験値 を持つ。冒険者と実際に戦った階層＝+100exp(=+1Lv)、冒険者が到達しなかった階層で待機＝+25exp(1/4)。
/// - 純static・実行時保持（セーブ未実装＝ドメインリロードでリセット）。関連: [[MinionCatalog]] [[MinionEvolution]] / DungeonFeatureManager(配置)。
/// </summary>
public static class MinionRoster
{
    public class Individual
    {
        public int id;            // 一意な個体ID
        public int catalogIndex;  // 種類（MinionCatalog index）
        public int level = 1;     // 個体レベル（1..MaxLevel）
        public int exp;           // 次のLvまでの経験値（0..ExpPerLevel）
        // ⚔️🛡️ 装備スロット（PE：CDO2風の武器/防具装着。-1=素手/素肌）。装着UIは後続、データ土台とスポーン適用は先に用意。
        public int weaponGrade = -1;
        public int armorGrade = -1;
        // 💍 装飾品（[[AccessoryCatalog]] の index／-1=なし）。武器防具とは別枠で1つだけ。
        public int accessory = -1;
        // ⚔️ 武器の種別（剣/斧/槍/弓/杖/双剣/鎚）。攻撃間隔・射程・威力の"戦い方"が変わる。
        public int weaponType = (int)EquipmentCatalog.WeaponType.Sword;
        // 🔁 直近のウェーブで冒険者と同じ階層に立ったか（＝実戦経験が入ったか）。
        //    反芻(TrainingSystem)の可否判定に使う。「階層が到達された」ではなく「この個体が戦った」で見る。
        //    ⚠ セーブ対象（[NonSerialized]にしない）。落として読み直すと反芻し放題になるため。
        public bool foughtLastWave;
    }

    public const int MaxLevel = 50;
    public const float PerLevel = 0.04f;      // Lvあたりの HP/ATK 上昇率（+4%/Lv）
    public const int ExpPerLevel = 100;       // 1レベルに必要な経験値
    public const int BattleExp = 100;         // （旧）平坦な実戦経験。ExpForFloor に置き換え済み
    public const int GarrisonExp = 25;        // （旧）平坦な待機経験。ExpForFloor に置き換え済み

    /// <summary>
    /// 🧪 魔素濃度：**迷宮の核に近いほど魔素が濃く、配下が速く育つ**。
    ///
    /// ⚠ これは難易度カーブの要。旧仕様は「戦った階層+100／未到達+25」の平坦な値だったため、
    ///   **経験値は"戦った回数"に比例するのに、必要な強さは"そこまで来た冒険者の質"に比例する**という
    ///   逆向きのフィードバックができていた。回数は浅いほど多く、質は深いほど高いので、
    ///   実測で 20ウェーブ後に B1F Lv21(×1.80) / B3F Lv6(×1.20) ＝ **深いほど弱い**盤面になっていた。
    ///   → 基礎値を深度で決め、戦闘は倍率にする。これで「上の階層ほど強い配下」が既定になる。
    /// </summary>
    public static int ExpForFloor(int floorIndex, bool fought)
    {
        int baseExp = 40 + 35 * Mathf.Max(0, floorIndex);
        return fought ? baseExp * 2 : baseExp;
    }

    /// <summary>
    /// 🎯 その階層の配下が"いてほしい"レベル。冒険者の目安Lvから逆算する。
    /// 浅い階は弱者を捌く関所なので低め、深い階は強者だけが来るので高め。
    /// </summary>
    public static int TargetLevelFor(int floorIndex)
    {
        float exp = AdventurerAI.ExpectedLevelNow();
        return Mathf.Clamp(Mathf.RoundToInt(exp * (0.55f + 0.15f * Mathf.Max(0, floorIndex))), 1, MaxLevel);
    }

    /// <summary>
    /// 🐢 追いつき補正：目標Lvから**遅れているぶんだけ**経験値が増える（最大2.5倍）。
    ///
    /// ⚠ 「経験値を相手のLvに比例させる」のは**やってはいけない**。相手Lvはターンに線形なので、
    ///   比例させると積算が二次になり、こちらが一方的に追い越す（→ [[difficulty-curve-orders]]）。
    ///   遅れ幅に応じた補正なら、**追いついた瞬間に倍率が1に戻る**のでオーバーシュートしない。
    /// </summary>
    public static float CatchUpMult(int level, int floorIndex)
    {
        int target = TargetLevelFor(floorIndex);
        if (target <= 1 || level >= target) return 1f;
        float behind = 1f - (float)level / target;      // 0（追いついた）〜 1（Lv0相当）
        return 1f + behind * 1.5f;
    }
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
    // 🧬 ファミリー(不死/獣/魔族)ごとの所持数。魔王の種族進化条件（原作の『その系統を多用』）に使う。
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

    /// <summary>個体をロスターから完全に削除する（🗺️ 地上侵攻で失った配下＝育てたものを失う重み）。</summary>
    public static bool Remove(int id)
    {
        EnsureInit();
        for (int i = 0; i < all.Count; i++)
            if (all[i].id == id)
            {
                Debug.Log($"💀『喪失』{MinionCatalog.Get(all[i].catalogIndex).jpName} 個体#{id}(Lv{all[i].level}) を失った");
                all.RemoveAt(i); return true;
            }
        return false;
    }

    // 個体レベル → 配置時の倍率（HP/ATK）。Lv1=×1.0、Lv50≈×2.96。
    public static float LevelMult(int level) { return 1f + (Mathf.Clamp(level, 1, MaxLevel) - 1) * PerLevel; }

    /// <summary>
    /// 🌱 いま召喚したら何レベルで出てくるか。
    ///
    /// ⚠ これは難易度カーブの要。旧仕様は**必ずLv1**だったので、冒険者がLv16の世界に
    ///   Lv1の新兵が出てきていた。しかも2階層以降は解禁が遅い＝そこに置くのは常に新兵なので、
    ///   **深い階ほど弱い**という逆転が起きていた（魔素濃度で直したはずの現象がここから再発していた）。
    ///   → 世界の育ち具合に合わせて出す。強さは召喚コストで払う（[[SummonCost]]）。
    /// </summary>
    public static int SummonLevel()
        => Mathf.Clamp(Mathf.RoundToInt(AdventurerAI.ExpectedLevelNow() * 0.5f), 1, MaxLevel);

    // 召喚コスト（DP）。ティア（＝ランク）が高いほど高い。創造ランクの DefenderCostMult も反映。
    // 🌱 出てくるレベルぶんの割増も乗る＝**世界が育つほど新兵は強いが高い**（安く数を並べるか、高くて即戦力か）。
    public static int SummonCost(int catalogIndex)
    {
        float mult = DemonLord.Instance != null ? DemonLord.Instance.DefenderCostMult : 1f;
        mult *= PolicySystem.SummonCostMult * AttributeSystem.SummonCostMult;   // 🏛️ 政策『黄金律』／🎖️ 属性『鋳造』
        mult *= 1f + (SummonLevel() - 1) * 0.10f;                                // 🌱 世界水準ぶんの割増（ターンに線形）
        return Mathf.RoundToInt(MinionCatalog.Get(catalogIndex).tierCP * SummonDpPerTier * mult);
    }

    /// <summary>
    /// 👾 ユニーク魔物を1体加える（ガチャの当たり）。
    /// ⚠ 通常の召喚と**同じ `Individual`** にする。そうすればレベルも装備も図鑑も
    ///   1行も足さずにそのまま効く（旧「特殊敵」が幹から切れていた原因は、ここを通らなかったこと）。
    /// ⚠ ユニークは強いので、出てくるレベルは通常召喚と同じ規則にとどめる（強さは種の倍率で表す）。
    /// </summary>
    public static Individual GrantUnique(int localIndex)
    {
        EnsureInit();
        int ci = UniqueCatalog.GlobalOf(localIndex);
        var v = new Individual { id = nextId++, catalogIndex = ci, level = SummonLevel(), exp = 0 };
        v.weaponType = (int)EquipmentCatalog.DefaultTypeForRole(MinionCatalog.Get(ci).role);
        all.Add(v);
        var d = UniqueCatalog.Get(localIndex);
        Debug.Log($"👾『ユニーク』{d.jpName} 個体#{v.id} を Lv{v.level} で得た（{d.desc}）");
        NotifySystem.Push($"<b>{d.jpName}</b> を引き当てた ― {d.desc}", NotifySystem.Kind.Story);
        return v;
    }

    /// <summary>いま持っているユニーク個体（図鑑の一覧用）。</summary>
    public static List<Individual> Uniques()
    {
        EnsureInit();
        var l = new List<Individual>();
        foreach (var v in all) if (UniqueCatalog.IsUnique(v.catalogIndex)) l.Add(v);
        return l;
    }

    /// <summary>🌅 費用なしで1体だけ加える（開始時の初期ユニット用）。</summary>
    public static Individual TrySummonFree(int catalogIndex)
    {
        EnsureInit();
        var v = new Individual { id = nextId++, catalogIndex = catalogIndex, level = SummonLevel(), exp = 0 };
        v.weaponType = (int)EquipmentCatalog.DefaultTypeForRole(MinionCatalog.Get(catalogIndex).role);
        all.Add(v);
        return v;
    }

    // 召喚（DP消費して個体を追加）。未解禁/DP不足なら null。
    public static Individual TrySummon(int catalogIndex)
    {
        EnsureInit();
        if (!MinionEvolution.IsUnlocked(catalogIndex)) { Debug.LogWarning("⚠️ 未解禁の種類は召喚できません（先に進化で解禁）。"); return null; }
        int cost = SummonCost(catalogIndex);
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(cost)) { Debug.LogWarning($"⚠️ DP不足で召喚できません（要{cost}DP）。"); return null; }
        var ind = new Individual { id = nextId++, catalogIndex = catalogIndex, level = SummonLevel() };
        ind.weaponType = (int)EquipmentCatalog.DefaultTypeForRole(MinionCatalog.Get(catalogIndex).role); // ⚔️ 役割に合う初期武器種
        all.Add(ind);
        Debug.Log($"🧬『召喚』{MinionCatalog.Get(catalogIndex).jpName} 個体#{ind.id} を Lv{ind.level} で召喚（-{cost}DP）");
        return ind;
    }

    // 🧬 育てた個体をそのまま進化させる（CDO2の魔物進化）。Lv・装備は引き継ぎ、種類だけ上位形態へ。
    //    条件：進化先がその個体の種類の子＆研究段階が解禁済み＆DP。・『進化済みを新規召喚』も従来どおり可能。
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
        Debug.Log($"🧬『個体進化』{beforeName} 個体#{id}(Lv{v.level}) → {MinionCatalog.Get(targetCatalogIndex).jpName}（-{cost}DP）");
        return true;
    }

    // 経験値を加算してレベルアップ判定（上限MaxLevel）。使うと育つ／守っていても少し育つ。
    public static void AddExp(int id, int amount)
    {
        var v = Get(id); if (v == null || amount <= 0) return;
        if (v.level >= MaxLevel) { v.exp = 0; return; }
        amount = Mathf.RoundToInt(amount * PolicySystem.ExpMult * AttributeSystem.ExpMult);   // 🏛️ 政策『魔素の精製』／🎖️ 属性『魔素学』
        v.exp += amount;
        while (v.exp >= ExpPerLevel && v.level < MaxLevel) { v.exp -= ExpPerLevel; v.level++; }
        if (v.level >= MaxLevel) v.exp = 0;
    }
    /// <summary>🧪 階層の魔素濃度＋🐢追いつき補正で経験値を入れる。実戦なら『戦った』印も付ける。</summary>
    public static void AddFloorExp(int id, int floorIndex, bool fought)
    {
        var v = Get(id); if (v == null) return;
        float mult = CatchUpMult(v.level, floorIndex) * ManaSurge.FloorExpMult(floorIndex);   // 🌊 奔流のターンは深い階ほど増える
        AddExp(id, Mathf.RoundToInt(ExpForFloor(floorIndex, fought) * mult));
        if (fought) v.foughtLastWave = true;
    }

    /// <summary>ウェーブの頭で『戦った』印を消す（次のウェーブの判定に持ち越さない）。</summary>
    public static void ClearFoughtFlags()
    {
        EnsureInit();
        foreach (var v in all) v.foughtLastWave = false;
    }

    public static int ExpOf(int id) { var v = Get(id); return v != null ? v.exp : 0; }
    public static float ExpRatio(int id) { var v = Get(id); return v == null || v.level >= MaxLevel ? 0f : (float)v.exp / ExpPerLevel; }

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
        Debug.Log($"⚔️『武器種』{MinionCatalog.Get(v.catalogIndex).jpName} 個体#{id} → {EquipmentCatalog.WeaponTypeName(v.weaponType)}");
    }
    // 武器種による 攻撃/間隔/射程（スポーン時にZombieAIへ適用）
    public static float TypeAtkMult(int id) { var v = Get(id); return v == null ? 1f : EquipmentCatalog.WType(v.weaponType).atkMult; }
    public static float TypeIntervalMult(int id) { var v = Get(id); return v == null ? 1f : EquipmentCatalog.WType(v.weaponType).intervalMult; }
    public static float TypeRangeBonus(int id) { var v = Get(id); return v == null ? 0f : EquipmentCatalog.WType(v.weaponType).rangeBonus; }

    // ⚔️🛡️ 個体の装備倍率（PE：装着中の武器/防具グレードから）。未装着(-1)は×1.0。スポーン時に適用。
    // 💍 装飾品の倍率は**ここに含める**。呼ぶ側（配置・軍団・地上）は既にこの2つを見ているので、
    //    ここに足せば1行も変えずに全部へ効く。⚠ 別の口を作ると必ず片方に配線し忘れる。
    public static float EquipAtkMult(int id)
    { var v = Get(id); return v == null ? 1f : EquipmentCatalog.WeaponAtkMult(v.weaponGrade) * AccMult(v, 1); }
    public static float EquipHpMult(int id)
    { var v = Get(id); return v == null ? 1f : EquipmentCatalog.ArmorHpMult(v.armorGrade) * AccMult(v, 0); }
    /// <summary>装飾品の倍率（0=HP 1=攻撃 2=速度）。着けていなければ1。</summary>
    private static float AccMult(Individual v, int which)
    {
        if (v == null || v.accessory < 0) return 1f;
        var a = AccessoryCatalog.Get(v.accessory);
        return which == 0 ? a.hpMult : which == 1 ? a.atkMult : a.spdMult;
    }
    public static float AccessorySpdMult(int id) { return AccMult(Get(id), 2); }
    /// <summary>💍 その個体が装飾品で得ているスキル（無ければ None）。</summary>
    public static MinionSkillKind AccessorySkill(int id)
    {
        var v = Get(id);
        return (v == null || v.accessory < 0) ? MinionSkillKind.None : AccessoryCatalog.Get(v.accessory).grant;
    }
    /// <summary>装飾品を着け替える（-1 で外す）。1個体1つ。</summary>
    public static bool SetAccessory(int id, int accIndex)
    {
        var v = Get(id); if (v == null) return false;
        v.accessory = Mathf.Clamp(accIndex, -1, AccessoryCatalog.Count - 1);
        return true;
    }
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
        float fm = DemonLord.Instance != null ? DemonLord.Instance.ForgeCostMult : 1f;
        if (RelicManager.Instance != null) fm *= RelicManager.Instance.ForgeCostMult; // 🏺 錬金の坩堝
        fm *= WonderCatalog.ForgeCostMult;                                            // ★ 遺産『賢者の炉』
        fm *= SettlementSystem.ForgeCostMult;                                         // 🎯 拠点の特化『工廠の町』
        int cost = Mathf.RoundToInt(EquipmentCatalog.ForgeCost(next) * fm);
        int mat = EquipmentCatalog.ForgeMaterial(next);   // 🪨 ミスリル以上は素材も要る
        var res = DungeonResourceManager.Instance;
        if (res != null && mat > 0 && res.CraftMaterials < mat)
        { Debug.LogWarning($"⚠️ 素材不足で鍛造できません（要{mat}素材）。"); return false; }
        if (res != null && !res.TrySpendDP(cost)) { Debug.LogWarning($"⚠️ DP不足で鍛造できません（要{cost}DP）。"); return false; }
        if (res != null && mat > 0) res.TrySpendMaterial(mat);
        if (slot == EquipmentCatalog.Slot.Weapon) v.weaponGrade = next; else v.armorGrade = next;
        string sname = slot == EquipmentCatalog.Slot.Weapon ? "武器" : "防具";
        Debug.Log($"🔨『鍛造』{MinionCatalog.Get(v.catalogIndex).jpName} 個体#{id} の{sname}を『{EquipmentCatalog.Name(next)}』に"
            + $"（-{cost}DP{(mat > 0 ? " -" + mat + "素材" : "")}／{EquipmentCatalog.StepText(cur, slot)}）");
        EurekaTracker.OnForge(next);
        return true;
    }
}
