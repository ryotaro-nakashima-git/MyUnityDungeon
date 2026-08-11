using UnityEngine;

/// <summary>
/// 魔王（ダンジョンコアの役割）。CDO2の『守るべき魔王＝倒されたらゲームオーバー』と
/// 小説の『真核＝最深部の核』をハイブリッド。1ダンジョンに1体、最深部(DemonLordCell)に配置。
/// </summary>
public class DemonLord : MonoBehaviour
{
    public static DemonLord Instance { get; private set; }

    [Header("Demon Lord Status")]
    [SerializeField] private float baseMaxHP = 600f;
    [SerializeField] private float hpPerTurn = 120f;   // ターン毎に増える最大HP
    [SerializeField] private float attackInterval = 1.0f;
    [SerializeField] private float attackRange = 1.6f;

    private float maxHP, currentHP;
    private bool alive = true;
    private float attackTimer = 0f;
    private DungeonGridSystem grid;
    private SpriteRenderer sr;
    private TextMesh hpText;
    private DemonLordVisual dlv;

    private bool present = true; // 🏢 このフロア(最下層)に魔王が実在するか
    public bool IsAlive => alive;
    public bool IsPresent => present;
    public float HPRatio => maxHP > 0 ? currentHP / maxHP : 0f;

    /// <summary>複数フロアで最下層以外を表示中は魔王を不在化（非表示＋無敵無効＋反撃なし）。</summary>
    public void SetPresent(bool p)
    {
        present = p;
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = p;
    }

    // ===== 魔王の成長（ステータス/レベル/種族進化）=====
    public enum Stat { Body, Magic, Knowledge, Creation, Refine } // 肉体/魔力/知識/創造/錬成
    // 🧬 種族（3段階）。・ DemonLordRaceTree の定義順と1対1で対応させること
    public enum Race
    {
        Human,                                        // 基本
        Oni, Demon, Elf, Dwarf, Slime, Beast,         // 第1進化
        Rakshasa, Dragon, Fallen, Vampire, Fairy, HighElf, Giant, Mimic, BeastKing // 第2進化
    }

    [Header("Growth")]
    [SerializeField] private int bpPerWave = 4;
    [SerializeField] private float hpPerBodyRank = 130f;
    [SerializeField] private float baseAttackPower = 20f;
    [SerializeField] private float atkPerMagicRank = 6f;

    private int[] statRanks = new int[5]; // 0=E,1=D,2=C,3=B,4=A,5=S
    private int level = 1;
    private int bp = 10;
    private Race race = Race.Human;
    private float effectiveAttack = 20f;
    private static readonly int[] rankUpCost = { 2, 5, 10, 18, 30 }; // E→D, D→C, C→B, B→A, A→S

    public static readonly string[] StatNames = { "肉体", "魔力", "知識", "創造", "錬成" };
    public int Level => level;
    public int BP => bp;
    public Race CurrentRace => race;
    public int GetStatRank(int i) => statRanks[Mathf.Clamp(i, 0, 4)];
    public string StatRankLabel(int i) => "EDCBAS"[Mathf.Clamp(GetStatRank(i), 0, 5)].ToString();
    public string RaceName => RaceNameOf(race);
    // 🧬 進化可能か＝進化先がまだ存在する（第2進化まで）
    public bool CanEvolve => DemonLordRaceTree.ChildrenOf(race).Count > 0;
    public int RaceStage => DemonLordRaceTree.StageOf(race);

    // ⚔️🛡️ 魔王の装備（個体と同じ EquipmentCatalog を使う）。錬成ランクで鍛造が安く・上限が上がる。
    private int weaponGrade = -1, armorGrade = -1;
    private int weaponType = (int)EquipmentCatalog.WeaponType.Sword;
    public int WeaponGrade => weaponGrade;
    public int ArmorGrade => armorGrade;
    public int WeaponType => weaponType;

    // ===== 📊 ステータスの意味（知識=研究 / 創造=配下・領域 / 錬成=装備・お宝）=====
    /// <summary>創造：配下召喚・隊員配置などのDPコスト倍率（ランクごとに-6%、種族補正も乗る）。</summary>
    public float DefenderCostMult
    {
        get
        {
            float byStat = 1f - 0.06f * statRanks[(int)Stat.Creation];      // S(5)で -30%
            return Mathf.Max(0.4f, byStat) * DemonLordRaceTree.Get(race).allyCostMult;
        }
    }
    /// <summary>創造：領域拡張(横/縦)のDPコスト倍率。</summary>
    public float DomainCostMult => Mathf.Max(0.5f, 1f - 0.05f * statRanks[(int)Stat.Creation]);
    /// <summary>錬成：装備の鍛造コスト倍率（ランクごとに-8%、ドワーフ系はさらに安い）。</summary>
    public float ForgeCostMult
    {
        get
        {
            float m = Mathf.Max(0.3f, 1f - 0.08f * statRanks[(int)Stat.Refine]);
            if (race == Race.Dwarf) m *= 0.85f; else if (race == Race.Giant) m *= 0.7f;
            return m;
        }
    }
    /// <summary>錬成：研究が無くても鍛えられるグレード上限の底上げ（錬成B以上で+1、S で+2）。</summary>
    public int ForgeGradeBonus => statRanks[(int)Stat.Refine] >= 5 ? 2 : statRanks[(int)Stat.Refine] >= 3 ? 1 : 0;
    /// <summary>錬成：宝箱(誘導)の質・撃破素材のボーナス。</summary>
    public int RefineLootBonus => statRanks[(int)Stat.Refine] / 2;
    /// <summary>知識：研究コスト割引（ランクごとに-5%）。RPレートは DungeonTurnManager が別途参照。</summary>
    public float ResearchCostMult => Mathf.Max(0.5f, 1f - 0.05f * statRanks[(int)Stat.Knowledge]);
    public int KnowledgeRank => statRanks[(int)Stat.Knowledge];

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildVisual();
    }

    private void Start()
    {
        grid = Object.FindFirstObjectByType<DungeonGridSystem>();
    }

    private void BuildVisual()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = MakeSquare();
        sr.color = new Color(0.55f, 0.20f, 0.78f); // 紫
        sr.sortingOrder = 60;
        transform.localScale = Vector3.one * 0.82f;

        var label = new GameObject("Label");
        label.transform.SetParent(transform, false);
        label.transform.localPosition = new Vector3(0, 0.05f, -0.1f);
        label.transform.localScale = Vector3.one * 0.13f;
        var tm = label.AddComponent<TextMesh>();
        tm.text = "DL"; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.fontSize = 48; tm.characterSize = 0.5f; tm.color = new Color(1f, 0.9f, 0.4f); tm.fontStyle = FontStyle.Bold;
        var mr = tm.GetComponent<MeshRenderer>(); if (mr != null) mr.sortingOrder = 61;

        var hp = new GameObject("HP");
        hp.transform.SetParent(transform, false);
        hp.transform.localPosition = new Vector3(0, -0.5f, -0.1f);
        hp.transform.localScale = Vector3.one * 0.1f;
        hpText = hp.AddComponent<TextMesh>();
        hpText.anchor = TextAnchor.MiddleCenter; hpText.alignment = TextAlignment.Center;
        hpText.fontSize = 40; hpText.characterSize = 0.5f; hpText.color = Color.red;
        var mr2 = hpText.GetComponent<MeshRenderer>(); if (mr2 != null) mr2.sortingOrder = 61;

        // 🎭 魔王リグ（進化段階別）を生成し、旧マーカー(四角/DL/HPテキスト)は隠す
        var vgo = new GameObject("DLVisual"); vgo.transform.SetParent(transform, false);
        dlv = vgo.AddComponent<DemonLordVisual>();
        dlv.BuildStage(race);
        if (sr != null) sr.enabled = false;
        label.SetActive(false);
        hp.SetActive(false);
    }

    /// <summary>迷宮生成時に最深部へ配置し、HPをリセットする（DungeonGridSystemから呼ばれる）。</summary>
    public void PlaceAt(Vector2Int cell)
    {
        if (grid == null) grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (grid != null) transform.position = grid.GridToWorld(cell.x, cell.y) + new Vector3(0, 0, -0.6f);

        alive = true;
        present = true;
        SetPresent(true);
        RecomputeCombatStats();  // ステータス/種族を反映して最大HP・攻撃力を算出
        currentHP = maxHP;       // 満タンで再配置
        if (sr != null) sr.enabled = false; // 旧紫マーカーはリグ表示中は常に隠す（SetPresentが全Rendererを復活させるため）
        if (dlv != null) { dlv.BuildStage(race); dlv.SetHP(1f); } // 進化段階のリグを反映
        UpdateHPText();
    }

    /// <summary>💾 ロード直後。復元したステータス/種族/装備から戦闘値と見た目を作り直す。→ [[SaveSystem]]</summary>
    public void RefreshAfterLoad()
    {
        alive = true;
        RecomputeCombatStats();
        if (currentHP <= 0f || currentHP > maxHP) currentHP = maxHP;   // 準備フェーズ＝満タンで始まる
        if (dlv == null) dlv = GetComponent<DemonLordVisual>();
        if (dlv != null) { dlv.BuildStage(race); dlv.SetHP(currentHP / Mathf.Max(1f, maxHP)); }
        UpdateHPText();
    }

    // ステータス・種族からmaxHP/攻撃力を再計算
    private void RecomputeCombatStats()
    {
        int turn = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 1;
        var rd = DemonLordRaceTree.Get(race);
        // 🛡️ 防具グレードでHP、⚔️ 武器グレード×種別で攻撃力（個体と同じ体系）
        float armor = EquipmentCatalog.ArmorHpMult(armorGrade);
        float weapon = EquipmentCatalog.WeaponAtkMult(weaponGrade) * EquipmentCatalog.WType(weaponType).atkMult;
        float relicCore = RelicManager.Instance != null ? RelicManager.Instance.DemonLordHpMult : 1f; // 🏺 魔王の心臓
        // 🍽️ 捕食の段位は**基礎値への加算**として入れる。⚠ 倍率にしない（→ [[LordStance]]）
        maxHP = (baseMaxHP + hpPerTurn * (turn - 1) + hpPerBodyRank * statRanks[(int)Stat.Body] + LordStance.BonusHP) * rd.hpMult * armor * relicCore;
        effectiveAttack = (baseAttackPower + atkPerMagicRank * statRanks[(int)Stat.Magic] + LordStance.BonusAtk) * rd.atkMult * weapon;
        if (currentHP > maxHP) currentHP = maxHP;
    }
    private float RaceHpMult() => DemonLordRaceTree.Get(race).hpMult;
    private float RaceAtkMult()
    {
        switch (race) { case Race.Oni: return 1.3f; case Race.Vampire: return 1.4f; case Race.Demon: return 1.25f; case Race.Elf: return 1.1f; default: return 1f; }
    }

    // ⬆️ 防衛戦を1ウェーブ耐えるごとにレベルアップ＆BP獲得（DungeonTurnManager.EndBattlePhaseから）
    public void OnWaveDefended()
    {
        level++;
        // 👑 鎮座は盤に関与しないぶん、思索の時間が BP になる（親征は魂＝捕食値で報われる）
        int gain = bpPerWave + (LordStance.IsExpedition ? 0 : 2);
        bp += gain;
        RecomputeCombatStats(); currentHP = maxHP;
        Debug.Log($"⬆️『魔王成長』Lv{level} / BP +{gain}（所持 {bp}／構え {LordStance.CurrentName}）");
    }

    /// <summary>🜲 権能で癒える（上限を超えない）。</summary>
    public void Heal(float amount)
    {
        if (!alive || amount <= 0f) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        UpdateHPText();
        if (dlv != null) dlv.SetHP(HPRatio);
    }

    /// <summary>🜲 焦土の令：自分も焼ける（最大HPの割合。⚠ これで死なないよう1は残す）。</summary>
    public void SelfBurn(float frac)
    {
        if (!alive) return;
        currentHP = Mathf.Max(1f, currentHP - maxHP * Mathf.Max(0f, frac));
        UpdateHPText();
        if (dlv != null) dlv.SetHP(HPRatio);
    }

    // 🔧 BPを消費してステータスを1ランク上げる（UIから）
    public bool TrySpendBPOnStat(int statIndex)
    {
        if (statIndex < 0 || statIndex > 4) return false;
        int r = statRanks[statIndex];
        if (r >= 5) { Debug.Log("ℹ️ 既に最大ランク(S)です。"); return false; }
        int cost = rankUpCost[r];
        if (bp < cost) { Debug.LogWarning($"❌ BP不足（必要 {cost} / 所持 {bp}）"); return false; }
        bp -= cost; statRanks[statIndex]++;
        RecomputeCombatStats(); currentHP = maxHP;
        UpdateHPText();
        return true;
    }

    // 🧬 種族進化（3段階・分岐。条件は DemonLordRaceTree が持つ）
    public bool IsRaceAvailable(Race r)
    {
        if (DemonLordRaceTree.Get(r).parent != race) return false; // 直系の子のみ
        string why;
        return DemonLordRaceTree.MeetsRequirement(r, this, out why);
    }
    public string RaceUnavailableReason(Race r)
    {
        if (DemonLordRaceTree.Get(r).parent != race) return "進化元が違います";
        string why; DemonLordRaceTree.MeetsRequirement(r, this, out why);
        return why;
    }
    public bool EvolveTo(Race r)
    {
        if (!IsRaceAvailable(r)) return false;
        var from = race;
        race = r;
        RecomputeCombatStats(); currentHP = maxHP;
        if (dlv != null) { dlv.BuildStage(race); dlv.SetHP(1f); } // 🧬 進化段階のリグへ差し替え
        UpdateHPText();
        var d = DemonLordRaceTree.Get(r);
        Debug.Log($"🧬『進化』魔王が {RaceNameOf(from)} → {RaceNameOf(r)} へ！（{MagicCatalog.ElementName(d.element)}／{MinionSkill.Name(d.skill)}）");
        return true;
    }
    public static string RaceNameOf(Race r) => DemonLordRaceTree.NameOf(r);

    // 🔮💫 種族由来の属性とスキル（魔王の攻撃・耐久に反映）
    public MagicElement RaceElement => DemonLordRaceTree.Get(race).element;
    public MinionSkillKind RaceSkill => DemonLordRaceTree.Get(race).skill;

    // ⚔️🛡️ 装備の鍛造（錬成ランクでコスト割引・上限が上がる）
    public int ForgeGradeCap
    {
        get
        {
            int byResearch = ResearchState.IsResearched("r_grade_orichal") ? EquipmentCatalog.MaxGrade
                           : ResearchState.IsResearched("r_grade_mithril") ? 4 : 3;
            return Mathf.Min(EquipmentCatalog.MaxGrade, byResearch + ForgeGradeBonus);
        }
    }
    public int NextForgeCost(EquipmentCatalog.Slot slot)
    {
        int cur = slot == EquipmentCatalog.Slot.Weapon ? weaponGrade : armorGrade;
        return Mathf.RoundToInt(EquipmentCatalog.ForgeCost(cur + 1) * ForgeCostMult * 1.5f); // 魔王の武具は割高
    }
    public bool TryForge(EquipmentCatalog.Slot slot)
    {
        int cur = slot == EquipmentCatalog.Slot.Weapon ? weaponGrade : armorGrade;
        int next = cur + 1;
        if (next > EquipmentCatalog.MaxGrade) { Debug.LogWarning("⚠️ 既に最高グレードです。"); return false; }
        if (next > ForgeGradeCap) { Debug.LogWarning("⚠️ これ以上は錬成ランクか錬成研究が必要です。"); return false; }
        int cost = NextForgeCost(slot);
        int mat = Mathf.RoundToInt(EquipmentCatalog.ForgeMaterial(next) * 1.5f);   // 🪨 魔王の武具は素材も割高
        var res = DungeonResourceManager.Instance;
        if (res != null && mat > 0 && res.CraftMaterials < mat) { Debug.LogWarning($"⚠️ 素材不足（要{mat}素材）"); return false; }
        if (res != null && !res.TrySpendDP(cost)) { Debug.LogWarning($"⚠️ DP不足（要{cost}DP）"); return false; }
        if (res != null && mat > 0) res.TrySpendMaterial(mat);
        if (slot == EquipmentCatalog.Slot.Weapon) weaponGrade = next; else armorGrade = next;
        RecomputeCombatStats(); currentHP = Mathf.Min(currentHP, maxHP);
        UpdateHPText();
        Debug.Log($"🔨『魔王の武具』{(slot == EquipmentCatalog.Slot.Weapon ? "武器" : "防具")}を『{EquipmentCatalog.Name(next)}』に鍛造"
            + $"（-{cost}DP{(mat > 0 ? " -" + mat + "素材" : "")}／{EquipmentCatalog.StepText(cur, slot)}）");
        return true;
    }
    public void CycleWeaponType()
    {
        weaponType = (weaponType + 1) % EquipmentCatalog.WeaponTypeCount;
        RecomputeCombatStats();
        Debug.Log($"⚔️『魔王の武器種』{EquipmentCatalog.WeaponTypeName(weaponType)} に変更");
    }

    // 🐺 眷属種族との相性：魔王の種族と親和する眷属を配置すると強化倍率(1.2)がかかる（3層バフの土台）
    public ZombieAI.Species AffinitySpecies => DemonLordRaceTree.Get(race).affinity;
    public float DefenderAffinityMult(ZombieAI.Species s) => s == AffinitySpecies ? 1.2f : 1f;

    /// <summary>
    /// 👑 魔王の格が配下全体を底上げする（原作の「魔王が強くなると配下も強くなる」）。
    ///
    /// **なぜ要るか**：冒険者は ランク×Lv×武器×防具×脅威度 の5軸が**ターンとfameで勝手に**伸びるのに、
    /// こちら側でターンに応じて自動で伸びるのは**個体Lvの1軸だけ**だった（実測 T3→T30 で
    /// 冒険者の総圧力 ×27 に対しB1F配下 ×2.4）。装備や進化はDPを**個体ごとに**払う必要があるので
    /// 数には効かない。ここは**払わなくても効く2本目の軸**として置く。
    ///
    /// ⚠ Lvは1ウェーブ耐えるごとに+1＝ターンに線形。個体Lvと同じ入力で駆動されるので、
    ///   両方に大きな係数を持たせると二次になる。**係数は小さく、上限も付ける**。
    /// </summary>
    // 🜏 習合『鬼種の血』もここに乗せる。⚠ 新しい軸を作らず、**既にある倍率に掛ける**（→ [[difficulty-curve-orders]]）
    public float MinionPowerMult => (1f + Mathf.Min(level, 40) * 0.03f) * SyncretismSystem.MinionPowerMult;   // Lv40で×2.2が上限
    /// <summary>種族による配下コスト補正（同系は安く、非同系は高い＝原作準拠）。</summary>
    public float RaceCostMultFor(ZombieAI.Species s)
    {
        var d = DemonLordRaceTree.Get(race);
        return s == d.affinity ? d.allyCostMult : d.otherCostMult;
    }

    private void Update()
    {
        if (!alive || !present) return;
        var turn = DungeonTurnManager.Instance;
        if (turn == null || !turn.IsBattlePhase) return;

        // 🔬 魔王研究『自然回復』：戦闘中も少しずつHPを回復（毎ターン全回復とは別）
        if (ResearchState.IsResearched("k_regen") && currentHP < maxHP)
        {
            currentHP = Mathf.Min(maxHP, currentHP + maxHP * 0.01f * Time.deltaTime); // 1%/秒
        }
        // 💫 種族スキル『再生』（スライム/変幻種など）：さらに自己回復
        if (RaceSkill == MinionSkillKind.Regen && currentHP < maxHP)
            currentHP = Mathf.Min(maxHP, currentHP + maxHP * 0.015f * Time.deltaTime);

        // 🛡 門番ボス生存中は無敵（オーラ表示）
        bool shielded = ZombieAI.GetLivingGuardian() != null;
        if (dlv != null) { dlv.SetGuarded(shielded); dlv.SetHP(HPRatio); }

        // 隣接した冒険者へ反撃（無敵中でも反撃はする）
        attackTimer += Time.deltaTime;
        if (attackTimer >= attackInterval)
        {
            attackTimer = 0f;
            float reprisal = effectiveAttack * (ResearchState.IsResearched("k_reprisal") ? 1.6f : 1f); // 🔬 魔王研究『反撃強化』
            // 🔮 種族の属性魔法：魔力ランクに応じた階級で、職の耐性を通して当てる
            var spell = MagicCatalog.Make(RaceElement, RankFromMagicStat());
            bool hit = false;
            foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None))
            {
                if (a == null) continue;
                if (Vector3.Distance(transform.position, a.transform.position) <= attackRange + EquipmentCatalog.WType(weaponType).rangeBonus)
                {
                    a.TakeDamage(reprisal * spell.power * MagicCatalog.ResistMultVsHero(spell.element, a.CurrentJob));
                    if (spell.trapStatus >= 0) a.ApplyTrapStatus(spell.trapStatus); // 属性の状態異常
                    hit = true;
                }
            }
            if (hit && dlv != null) dlv.PlayReprisal(); // 💥 反撃演出
        }
    }

    // 🔮 魔力ランク → 使える魔法の階級（E下級 …… S最上級）
    private MagicRank RankFromMagicStat()
    {
        int m = statRanks[(int)Stat.Magic];
        if (RelicManager.Instance != null) m += RelicManager.Instance.DemonLordSpellRankBonus; // 🏺 魔王の心臓：反撃魔法の階級+1
        return m >= 5 ? MagicRank.Highest : m >= 4 ? MagicRank.High : m >= 2 ? MagicRank.Mid : m >= 1 ? MagicRank.Low : MagicRank.Lowest;
    }

    private bool undyingUsed; // 💫 種族スキル『不屈』の使用済みフラグ

    public void TakeDamage(float dmg)
    {
        if (!alive || !present) return; // 🏢 不在フロアでは無敵（誤ゲームオーバー防止）
        if (ZombieAI.GetLivingGuardian() != null) return; // 🛡 門番生存中は無敵（保険）

        // 💫 種族スキル『棘の皮膚』（ドワーフ/巨人種）：受けたダメージを近くの冒険者へ反射
        if (RaceSkill == MinionSkillKind.Thorns && dmg > 0f)
        {
            foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None))
                if (a != null && Vector3.Distance(transform.position, a.transform.position) <= attackRange + 0.6f)
                { a.TakeDamage(dmg * 0.25f); break; }
        }

        currentHP -= dmg;

        // 💫 種族スキル『不屈』（羅刹/変幻種）：致死を一度だけHP1で耐える
        if (currentHP <= 0f && RaceSkill == MinionSkillKind.Undying && !undyingUsed)
        {
            undyingUsed = true; currentHP = 1f;
            BattleVfx.Burst(transform.position, new Color(1f, 0.9f, 0.4f, 1f), 1.4f);
            Debug.Log("💫『不屈』魔王が致死の一撃に耐えた！");
        }

        UpdateHPText();
        if (dlv != null) dlv.SetHP(HPRatio);
        if (currentHP <= 0f)
        {
            currentHP = 0f;
            alive = false;
            Die();
        }
    }

    private void Die()
    {
        if (sr != null) sr.color = Color.gray;
        if (hpText != null) { hpText.text = "DEFEATED"; hpText.color = Color.gray; }
        if (dlv != null) dlv.PlayDeath(); // 💀 討伐演出（unscaledで停止中も再生）
        Debug.Log("💀『ゲームオーバー』魔王が討伐されました！");

        var ui = Object.FindFirstObjectByType<GameUIManager>();
        if (ui != null) ui.ShowGameOver();
        Time.timeScale = 0f; // ゲーム停止
    }

    private void UpdateHPText()
    {
        if (hpText != null && alive) { hpText.text = "HP " + Mathf.CeilToInt(currentHP); hpText.color = Color.red; }
    }

    private static Sprite _square;
    private Sprite MakeSquare()
    {
        if (_square == null)
        {
            var tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.white); tex.Apply();
            _square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        }
        return _square;
    }
}
