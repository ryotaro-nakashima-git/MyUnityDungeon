using UnityEngine;
using System.Collections.Generic;

public class AdventurerAI : MonoBehaviour
{
    private DungeonGridSystem gridSystem;

    public enum Job { Warrior, Thief, Cleric, Mage }
    public enum Purpose { Explore, Conquer }

    [Header("Adventurer Job & Purpose")]
    private Job adventurerJob;
    private Purpose adventurerPurpose;

    [Header("Adventurer Status (Base)")]
    private float moveSpeed = 3f;
    private float maxHP = 100f;
    private float currentHP;

    private int adventurerLevel = 1;
    private int adventurerRank = 2;            // 🏅 冒険者ランク G(0)〜S(7)。世界の育ち(知名度＋脅威度)で上がる。
    public int AdventurerRank => adventurerRank;
    private int weaponGrade = -1, armorGrade = -1; // ⚔️🛡️ 装備グレード(EquipmentCatalog)。ランク＋世界装備水準で決定。
    public int WeaponGrade => weaponGrade;
    public int ArmorGrade => armorGrade;
    public Job CurrentJob => adventurerJob;
    // 🔮 この冒険者が使う魔法（魔法使い/聖職者のみ）。階級はランクで上がる。
    private MagicCatalog.Spell mySpell; private bool hasSpell;
    public string SpellLabel => hasSpell ? mySpell.jpName : "";
    private float regenPerSecond = 1.0f;

    [Header("Mana (MP) System")]
    private float maxMana = 100f;
    private float currentMana = 100f;
    private float manaRegenPerSecond = 0.75f; 

    [Header("Combat Settings")]
    private float attackTimer = 0f;
    private float attackInterval = 1.0f;
    private float threatAtkMult = 1f; // 🕸️ 誘導経済：脅威度による攻撃倍率（Startで設定）
    private float carriedGear = 0f;   // 🎁 略奪した装備量（逃げ切ると敵陣を武装／倒すと回収）

    // 🪤 罠の状態異常：DoT(毒/炎/出血)・凍結(氷)・麻痺(電気=周期的な短停止)
    private float dotTimer, dotDps, dotTick;
    private float frozenTimer;
    private float paralyzeTimer, paralyzePulse;
    private bool isFighting = false;      

    private float healTimer = 0f;
    private float healInterval = 2.0f;

    [Header("Emotion Pool")]
    [SerializeField] private float currentJoy = 0f;
    [SerializeField] private float currentFear = 0f;

    [Header("Satisfaction (満足したら帰還)")]
    [Tooltip("通常部屋を探索したときの満足の微増")]
    [SerializeField] private float satisfyRoomGain = 1f;
    [Tooltip("宝箱を開けたときの満足")]
    [SerializeField] private float satisfyChestGain = 4f;
    [Tooltip("罠にかかったときの満足(恐怖体験)")]
    [SerializeField] private float satisfyTrapGain = 3f;
    [Tooltip("感情値(喜び/恐怖)からの満足への寄与係数")]
    [SerializeField] private float satisfyEmotionFactor = 0.1f;
    [Tooltip("満足の閾値レンジ(個体差)。超えると帰還する")]
    [SerializeField] private Vector2 satisfyThresholdRange = new Vector2(28f, 52f);
    private float satisfaction = 0f;
    private float satisfactionThreshold = 10f;
    [Tooltip("探索先を選ぶときの距離ペナルティ（大きいほど近場から順に食う＝広い階層ほど長く滞在する）")]
    [SerializeField] private float distanceFalloff = 0.08f;

    [Header("Visual Effects")]
    [SerializeField] private TextMesh emotionTextMesh; 
    private Coroutine emotionCoroutine;

    [Header("AI Logic Settings")]
    private Vector2Int startPos; 
    private Vector2Int currentGridPos;
    public Vector2Int CurrentGridPos => currentGridPos;
    public Purpose AdventurerPurpose => adventurerPurpose; // 🏢 降下判定用
    public bool IsRetreating => isRetreating;
    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int pathIndex = 0;

    private bool isRetreating = false;
    private bool assaultingCore = false; // 👑 魔王の間で討伐中か
    private float conquerCoreAttraction = 200f; // 踏破者が門番排除後に核/階段へ向かう優先度（部屋/宝箱より高く）
    private float searchTimer = 0f;
    private float searchInterval = 0.5f; 

    private Vector2Int lastTriggeredTrapPos = new Vector2Int(-1, -1);

    private void Start()
    {
        gridSystem = GameObject.FindAnyObjectByType<DungeonGridSystem>();
        if (gridSystem == null) return;

        currentGridPos = gridSystem.WorldToGrid(transform.position);
        transform.position = gridSystem.GridToWorld(currentGridPos.x, currentGridPos.y);
        startPos = currentGridPos;

        if (emotionTextMesh != null)
        {
            emotionTextMesh.GetComponent<Renderer>().sortingOrder = 100;
            emotionTextMesh.gameObject.SetActive(false);
        }

        DetermineAdventurerStatus();
        TargetNextDestination();

        // 🎭 手続きキャラビジュアル（ジョブ別リグ）を生成し、旧スプライトは隠す
        var oldSr = GetComponent<SpriteRenderer>();
        if (oldSr != null) oldSr.enabled = false;
        var vgo = new GameObject("Visual");
        vgo.transform.SetParent(transform, false);
        visual = vgo.AddComponent<CharacterVisual>();
        // 🎨 SPUM完成スプライト（職×ランクで装備が良くなる）。ロード失敗時は手続きリグ
        visual.InitSpum(SpumMap.AdventurerPath(adventurerJob, adventurerRank), RigOf(adventurerJob));
        visual.SetHP(maxHP > 0 ? currentHP / maxHP : 1f);
    }

    private CharacterVisual.RigType RigOf(Job j)
    {
        switch (j)
        {
            case Job.Thief: return CharacterVisual.RigType.Thief;
            case Job.Cleric: return CharacterVisual.RigType.Cleric;
            case Job.Mage: return CharacterVisual.RigType.Mage;
            default: return CharacterVisual.RigType.Warrior;
        }
    }

    private CharacterVisual visual;

    // ===== 🌍 世界の育ち具合（式はここに一本化。UIの『世界水準』表示も同じものを使う） =====
    //  fame は逃がした人数の累積で、ウェーブ人数が turn に比例するため O(turn^2) で伸びる。
    //  そのまま線形に使うと強さが O(turn^2)〜O(turn^3) になり崖ができるので、対数で圧縮して
    //  ダンジョン側の伸び（個体Lv +1/戦＝turn線形）とオーダーを揃える。
    private static float RenownLog(int fame) => Mathf.Log(1f + Mathf.Max(0, fame) / 50f);

    public static float WorldTier(int turn, int fame, float threat)
        => Mathf.Clamp(turn * 0.10f + RenownLog(fame) * 0.9f + (threat - 1f) * 0.5f
            + DungeonFloorManager.RenownHeroRankBias
            + SurfaceMap.WorldTierBias             // 🗺️ 地上を広げるほど強い者が討伐に来る（対数＋上限1.2）
            + EraSystem.TierBias, 0f, 7f);         // ⏳ 時代が進むほど世が本気になる（胎動0／伸長+0.6／終焉+1.2）

    public static float LevelBase(int turn, int fame) => 1f + turn * 0.8f + RenownLog(fame) * 4f;

    // UI表示用：いまの世界水準と、来る冒険者の目安レベル
    public static float WorldTierNow()
    {
        int turn = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 1;
        int fame = DungeonResourceManager.Instance != null ? DungeonResourceManager.Instance.DungeonFame : 0;
        return WorldTier(turn, fame, LureEconomy.Threat);
    }
    public static int ExpectedLevelNow()
    {
        int turn = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 1;
        int fame = DungeonResourceManager.Instance != null ? DungeonResourceManager.Instance.DungeonFame : 0;
        return Mathf.RoundToInt(LevelBase(turn, fame) * 0.925f);
    }
    public static string RankLetter(int i) { string[] l = { "G", "F", "E", "D", "C", "B", "A", "S" }; return l[Mathf.Clamp(i, 0, 7)]; }

    private void DetermineAdventurerStatus()
    {
        int fame = 0;
        if (DungeonResourceManager.Instance != null) fame = DungeonResourceManager.Instance.DungeonFame;

        int turn = 1;
        if (DungeonTurnManager.Instance != null) turn = DungeonTurnManager.Instance.CurrentTurn;

        // ⚖️『成長オーダーの整合』ここが難易度カーブの心臓部。
        //  問題だったこと: fame は『逃がした人数 × 35』の累積で、ウェーブ人数自体が turn に比例して
        //  増えるため fame は O(turn^2) で伸びる。それを fame/40 のように *線形* に使うと
        //  冒険者の強さが O(turn^2)、さらに ランク×Lv×脅威度×装備 と掛け算で積まれて O(turn^3) 相当になり、
        //  『昨日まで余裕だったのに fame が伸びた途端に瞬殺される』という崖ができていた。
        //  ダンジョン側は 個体Lv+1/戦（＝turnに線形）なので、冒険者側も **turnに線形** へ揃える。
        //  → fame は対数で圧縮する（逓減）。fame 120→1.22 / 250→1.79 / 1800→3.64 / 5000→4.61。
        // レベル：turn線形 ＋ fame対数。振れ幅は基準値に比例させ、分散が turn とともに爆発しないようにする。
        float lvBase = LevelBase(turn, fame);
        adventurerLevel = Mathf.Clamp(Mathf.RoundToInt(lvBase * Random.Range(0.70f, 1.15f)), 1, 100);

        adventurerPurpose = (Random.Range(0, 2) == 0) ? Purpose.Explore : Purpose.Conquer;
        adventurerJob = (Job)Random.Range(0, 4);

        // 😌 満足閾値：探索目的は高め(長く楽しむ)、踏破目的は低め(早く帰る)
        satisfactionThreshold = Random.Range(satisfyThresholdRange.x, satisfyThresholdRange.y)
                                * ((adventurerPurpose == Purpose.Explore) ? 1.25f : 0.8f)
                                * DungeonTheme.SatisfyThresholdMult;   // 🏔️ 迷路は長居する

        // 🏅 冒険者ランク G〜S（8段）：世界が育つ(知名度Fame＋脅威度＋ターン)ほど高ランクが出やすい。
        //    ＝原作/CDO2の『冒険者がだんだん強くなる』を段階化。脅威度(誘導経済)とも連動＝泳がせるほど強敵が来る。
        float worldTier = WorldTier(turn, fame, LureEconomy.Threat);
        int rankIdx = Mathf.Clamp(Mathf.RoundToInt(worldTier + Random.Range(-1.6f, 1.1f)), 0, 7);
        adventurerRank = rankIdx;

        string[] rankLetter = { "G", "F", "E", "D", "C", "B", "A", "S" };
        // ⚖️ ランク差は『掛け算の軸のひとつ』でしかないので、以前(0.70〜3.30＝4.7倍差)は効きすぎだった。
        //    Lv・脅威度・装備と積み重なるため、ここは 2.8倍差 に圧縮する。
        float[] rankHp  = { 0.80f, 0.90f, 1.00f, 1.15f, 1.35f, 1.60f, 1.90f, 2.25f };
        float[] rankAtk = { 0.80f, 0.90f, 1.00f, 1.15f, 1.35f, 1.60f, 1.90f, 2.25f };
        float[] rankSpd = { 0.90f, 0.95f, 1.00f, 1.05f, 1.10f, 1.15f, 1.20f, 1.25f };
        Color[] rankCol =
        {
            new Color(0.78f, 0.78f, 0.80f), new Color(0.88f, 0.88f, 0.90f), // G/F 白灰
            new Color(0.45f, 0.85f, 0.55f), new Color(0.34f, 0.74f, 0.52f), // E/D 緑
            new Color(0.30f, 0.60f, 1.00f), new Color(0.56f, 0.55f, 1.00f), // C/B 青
            new Color(1.00f, 0.62f, 0.25f), new Color(1.00f, 0.25f, 0.25f)  // A/S 橙/赤
        };
        maxHP = 100f * rankHp[rankIdx];
        if (RelicManager.Instance != null) maxHP *= RelicManager.Instance.HeroHpMult; // 🏺 静寂の鈴：静かな迷宮ほど来る者が弱い
        moveSpeed = 3.0f * rankSpd[rankIdx];
        float rankAtkMult = rankAtk[rankIdx];
        var sr = GetComponent<SpriteRenderer>(); if (sr != null) sr.color = rankCol[rankIdx];
        string rankTitle = rankLetter[rankIdx] + "級";

        // 🎭 職＝4アーキタイプ(挙動/リグは不変)。表示名は『階級ラダー』でランクに応じ基本職→上位職→最上位職へ変化。
        int nameTier = rankIdx <= 1 ? 0 : rankIdx <= 3 ? 1 : rankIdx <= 4 ? 2 : rankIdx <= 5 ? 3 : 4;
        string[][] classLadder =
        {
            new[] { "見習い戦士⚔️", "戦士⚔️", "剣士⚔️", "騎士⚔️", "英雄⚔️" },   // Warrior
            new[] { "こそ泥🎭", "盗賊🎭", "シーフ🎭", "暗殺者🎭", "アサシン🎭" }, // Thief
            new[] { "祈祷師🌿", "聖職者🌿", "司祭🌿", "聖騎士🌿", "大司教🌿" },   // Cleric
            new[] { "術見習い🔮", "魔術師🔮", "魔導士🔮", "賢者🔮", "大賢者🔮" }  // Mage
        };
        string jobName = classLadder[(int)adventurerJob][nameTier];
        switch (adventurerJob)
        {
            case Job.Warrior: maxHP *= 1.3f; break;  // 戦士系は硬い
            case Job.Mage: moveSpeed *= 1.1f; break; // 魔術系は素早い
        }

        // ⚔️🛡️ 装備グレード（素材ラダー）：ランク＋世界装備水準(gearLevel)で武器/防具の素材が決まる。
        //    逃がして装備を奪われるほど gearLevel が上がり、高グレードの武具を持つ勇者が来る（両刃の具体化）。
        float gl = LureEconomy.GearLevel;
        weaponGrade = EquipmentCatalog.GradeFromWorld(rankIdx, gl);
        armorGrade = EquipmentCatalog.GradeFromWorld(rankIdx, gl);

        float levelMultiplier = 1.0f + (adventurerLevel - 1) * 0.03f;
        maxHP *= levelMultiplier;
        maxHP *= LureEconomy.HeroHpMult;                              // 🕸️ 脅威度で硬く
        maxHP *= EquipmentCatalog.ArmorHpMult(armorGrade);           // 🛡️ 防具グレードで硬く
        // 🕸️🏅⚔️ 攻撃力＝脅威度×ランク×武器グレード（baseDmg/魔王ダメに乗算）
        threatAtkMult = LureEconomy.HeroAtkMult * rankAtkMult * EquipmentCatalog.WeaponAtkMult(weaponGrade);
        currentHP = maxHP;

        regenPerSecond = (1.0f + (adventurerLevel * 0.04f)) * 0.4f; // ⚖️ 高Lvでの自己回復が過剰だったので緩和

        // 🔮 魔法：魔法使い/聖職者はランク相応の階級の魔法を修得（世界が育つほど高階級）
        hasSpell = MagicCatalog.TryPickHeroSpell(adventurerJob, rankIdx, out mySpell);

        string purposeStr = (adventurerPurpose == Purpose.Explore) ? "探索" : "踏破";
        string equipStr = $"武器{EquipmentCatalog.Name(weaponGrade)}/防具{EquipmentCatalog.Name(armorGrade)}"
                        + (hasSpell ? "/魔法" + mySpell.jpName : "");
        PopUpEmotionText($"{rankTitle} {jobName}[{purposeStr}] Lv.{adventurerLevel}");

        Debug.Log($"📢『パーティ突入』第 {turn} ターン ➡ <color=yellow>{rankTitle} {jobName} Lv.{adventurerLevel} ({purposeStr}目的) {equipStr}</color> が侵入！");
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        TickStatus(dt);                   // 🪤 罠の状態異常（DoT/凍結/麻痺）
        bool immobile = frozenTimer > 0f; // 凍結/麻痺中は行動不能

        if (currentHP < maxHP) currentHP = Mathf.Min(maxHP, currentHP + regenPerSecond * dt);
        if (adventurerJob == Job.Mage || adventurerJob == Job.Cleric || adventurerJob == Job.Thief)
            if (currentMana < maxMana) currentMana = Mathf.Min(maxMana, currentMana + manaRegenPerSecond * dt);

        if (immobile) return; // 凍結中は攻撃/移動/回復を停止（HP回復とMP再生は上で継続）

        HandleTacticalCombat();

        if (assaultingCore && !isRetreating) HandleCoreAssault();

        if (adventurerJob == Job.Cleric && !isRetreating)
        {
            healTimer += dt;
            if (healTimer >= healInterval) { healTimer = 0f; ExecuteAreaHeal(); }
        }

        if (!isFighting)
        {
            if (currentPath == null || currentPath.Count == 0 || pathIndex >= currentPath.Count)
            {
                searchTimer += dt;
                if (searchTimer >= searchInterval) { searchTimer = 0f; TargetNextDestination(); }
            }
            HandleMovement();
        }
    }

    // 🪤 罠の状態異常の進行
    private void TickStatus(float dt)
    {
        if (dotTimer > 0f)
        {
            dotTimer -= dt; dotTick += dt;
            if (dotTick >= 0.5f) { dotTick = 0f; TakeDamage(dotDps * 0.5f); } // 0.5秒ごとにDoT
        }
        if (frozenTimer > 0f) frozenTimer -= dt;
        if (paralyzeTimer > 0f)
        {
            paralyzeTimer -= dt; paralyzePulse += dt;
            if (paralyzePulse >= 1.0f) { paralyzePulse = 0f; frozenTimer = Mathf.Max(frozenTimer, 0.35f); } // 周期的に短く停止
        }
    }

    // 🪤 踏んだ罠の種類に応じて状態異常を付与
    public void ApplyTrapStatus(int trapKind)
    {
        var d = TrapCatalog.Get(trapKind);
        // 🌟 感情『呪縛』 × 🏺 遺物『呪縛の鎖』で状態異常が長引く
        // ⚖️ DoTでは『持続倍率＝総ダメージ倍率』になるため、感情×遺物の掛け算をそのまま乗せると毒が突出する。
        //    加算で合成し、上限1.8倍に丸める（凍結/麻痺の足止めが長くなりすぎるのも防ぐ）。
        float durBonus = 0f;
        if (EmotionTreeManager.Instance != null) durBonus += EmotionTreeManager.Instance.TrapStatusDurMult - 1f;
        if (RelicManager.Instance != null) durBonus += RelicManager.Instance.StatusDurationMult - 1f;
        float dur = d.statusDur * Mathf.Min(1.8f, 1f + durBonus);
        switch ((TrapKind)trapKind)
        {
            case TrapKind.Poison: case TrapKind.Fire: case TrapKind.Bleed:
                dotDps = TrapCatalog.DotPerSecond(trapKind, maxHP); dotTimer = dur; dotTick = 0f; PopUpEmotionText(d.name + "!"); break;
            case TrapKind.Ice:
                frozenTimer = Mathf.Max(frozenTimer, dur); PopUpEmotionText("凍結!"); break;
            case TrapKind.Electric:
                paralyzeTimer = Mathf.Max(paralyzeTimer, dur); PopUpEmotionText("麻痺!"); break;
        }
    }

    // 🏢 descent：突破時に次フロア入口へ再配置し、状態をリセットして侵攻を継続する
    public void RelocateTo(Vector2Int cell)
    {
        if (gridSystem == null) gridSystem = GameObject.FindAnyObjectByType<DungeonGridSystem>();
        if (gridSystem == null) return;
        currentGridPos = cell;
        startPos = cell; // 退却先は新フロアの入口に更新
        transform.position = gridSystem.GridToWorld(cell.x, cell.y);
        currentPath.Clear();
        pathIndex = 0;
        assaultingCore = false;
        isRetreating = false;
        isFighting = false;
        TargetNextDestination();
    }

    // 👑 魔王の間に到達した踏破者が、魔王を攻撃する処理
    private void HandleCoreAssault()
    {
        // 門番ボスが(復)存在する場合は魔王討伐を中断（先に門番を倒す）
        if (ZombieAI.GetLivingGuardian() != null) { assaultingCore = false; return; }

        // 🏢 このフロアに魔王が居ない（＝最下層でない）場合は討伐扱いにしない
        if (DemonLord.Instance == null || !DemonLord.Instance.IsPresent) { assaultingCore = false; return; }

        if (!DemonLord.Instance.IsAlive)
        {
            assaultingCore = false;
            isRetreating = true;
            PopUpEmotionText("👑討伐成功!");
            CalculatePathTo(startPos);
            return;
        }
        isFighting = true; // その場に留まって魔王を攻撃
        attackTimer += Time.deltaTime;
        if (attackTimer >= attackInterval)
        {
            attackTimer = 0f;
            if (visual != null && DemonLord.Instance != null)
            {
                visual.FaceTowards(DemonLord.Instance.transform.position.x);
                if (adventurerJob == Job.Mage)
                {
                    visual.PlayAttack(CharacterVisual.AttackStyle.Cast);
                    BattleVfx.Projectile(visual.MuzzlePos(), DemonLord.Instance.transform.position, new Color(0.95f, 0.55f, 0.25f));
                }
                else visual.PlayAttack(adventurerJob == Job.Thief ? CharacterVisual.AttackStyle.Stab : CharacterVisual.AttackStyle.Swing);
            }
            float dmg = (15f + adventurerLevel * 0.8f) * threatAtkMult;
            DemonLord.Instance.TakeDamage(dmg);
            PopUpEmotionText("⚔魔王討伐!");
            var et = EmotionTreeManager.Instance;
            if (et != null) { et.AddEmotion(EmotionTreeManager.Route.Thrill, 1); et.CountBossHit(); } // 興奮ツリー
        }
    }

    private void HandleTacticalCombat()
    {
        if (gridSystem == null) return;

        currentGridPos = gridSystem.WorldToGrid(transform.position);

        ZombieAI[] allZombies = Object.FindObjectsByType<ZombieAI>();
        List<ZombieAI> targetsInRange = new List<ZombieAI>();

        // ⚔️『射程調整』魔術師（遠距離）は 2.0f、それ以外の近接職は 1.0f に設定！
        float attackRange = (adventurerJob == Job.Mage) ? 2.0f : 1.0f;    

        foreach (ZombieAI zombie in allZombies)
        {
            if (zombie.IsDead) continue; 

            float worldDist = Vector3.Distance(transform.position, zombie.transform.position);
            if (worldDist <= attackRange)
            {
                targetsInRange.Add(zombie);
            }
        }

        if (targetsInRange.Count > 0 && !isRetreating)
        {
            isFighting = true; 
            attackTimer += Time.deltaTime;
            if (attackTimer >= attackInterval)
            {
                attackTimer = 0f;
                ExecuteJobSpecificAttack(targetsInRange);
            }
        }
        else
        {
            isFighting = false;
        }
    }

    private void ExecuteJobSpecificAttack(List<ZombieAI> targets)
    {
        // 💫 威圧：近くに威圧持ちの眷属が居ると与ダメージが下がる
        // 🗿 呪詛の像（トーテム）：範囲内の冒険者の攻撃が落ちる
        float curse = Mathf.Max(0.3f, 1f - DungeonFeatureManager.TotemSumAt(transform.position, TotemCatalog.Kind.Curse));
        float baseDmg = (10f + (adventurerLevel * 0.5f)) * threatAtkMult * curse
                        * DungeonTheme.HeroDamageMult                      // 🏔️ 洞窟は暗くて当たらない
                        * ZombieAI.IntimidateMultAt(transform.position);
        ZombieAI target = targets[0];
        Vector3 tp = target.transform.position;
        if (visual != null) visual.FaceTowards(tp.x); // 🎯 対象の方向を向く
        Color fire = hasSpell ? SpellColor() : new Color(0.95f, 0.55f, 0.25f);

        switch (adventurerJob)
        {
            case Job.Warrior:
                if (visual != null) visual.PlayAttack(CharacterVisual.AttackStyle.Swing);
                PopUpEmotionText("🪓なぎ払い!");
                foreach (ZombieAI z in targets) z.TakeDamageFromAdventurer(baseDmg);
                break;

            case Job.Mage:
                if (currentMana >= 20f)
                {
                    currentMana -= 20f;
                    if (visual != null) visual.PlayAttack(CharacterVisual.AttackStyle.Cast);
                    PopUpEmotionText((hasSpell ? mySpell.jpName + "!" : "爆魔術!") + $"(MP:{Mathf.RoundToInt(currentMana)})");
                    // 🔮 各対象へ属性魔法弾（威力＝階級、眷属ファミリーの耐性で増減）
                    foreach (ZombieAI z in targets)
                    {
                        if (visual != null) BattleVfx.Projectile(visual.MuzzlePos(), z.transform.position, fire);
                        z.TakeDamageFromAdventurer(baseDmg * SpellMultVs(z, 1.3f));
                    }
                }
                else
                {
                    // 🥊 MP切れ → 素手の弱攻撃（パンチモーション）
                    if (visual != null) visual.PlayAttack(CharacterVisual.AttackStyle.Punch);
                    PopUpEmotionText("🥊素手(MP切れ)");
                    target.TakeDamageFromAdventurer(baseDmg * 0.3f);
                }
                break;

            case Job.Thief:
                if (visual != null) visual.PlayAttack(CharacterVisual.AttackStyle.Stab);
                PopUpEmotionText("🗡️バックスタブ!");
                target.TakeDamageFromAdventurer(baseDmg * 2.2f);
                break;

            case Job.Cleric:
                // 🔮 聖光：不死・魔族に特効。MPがあれば聖句、無ければ鈍器で殴る。
                if (hasSpell && currentMana >= 15f)
                {
                    currentMana -= 15f;
                    if (visual != null) { visual.PlayAttack(CharacterVisual.AttackStyle.Cast); BattleVfx.Projectile(visual.MuzzlePos(), tp, fire); }
                    PopUpEmotionText(mySpell.jpName + "!");
                    target.TakeDamageFromAdventurer(baseDmg * SpellMultVs(target, 1f));
                }
                else
                {
                    if (visual != null) visual.PlayAttack(CharacterVisual.AttackStyle.Swing);
                    PopUpEmotionText("叩き潰す!");
                    target.TakeDamageFromAdventurer(baseDmg);
                }
                break;
        }
    }

    // 🔮 魔法の対眷属倍率（階級の威力 × ファミリー耐性）。魔法を持たない場合は素の倍率。
    private float SpellMultVs(ZombieAI z, float fallback)
    {
        if (!hasSpell) return fallback;
        return mySpell.power * MagicCatalog.ResistMultVsMinion(mySpell.element, z.species);
    }
    private Color SpellColor()
    {
        Color c; ColorUtility.TryParseHtmlString(mySpell.colorHex, out c); return c;
    }

    private void ExecuteAreaHeal()
    {
        if (gridSystem == null) return;
        if (currentMana < 30f) return;

        AdventurerAI[] allAdventurers = Object.FindObjectsByType<AdventurerAI>();
        bool playedEffect = false;

        foreach (AdventurerAI ally in allAdventurers)
        {
            if (ally == this) continue;
            int dist = Mathf.Abs(ally.currentGridPos.x - this.currentGridPos.x) + Mathf.Abs(ally.currentGridPos.y - this.currentGridPos.y);
            if (dist <= 2 && ally.currentHP < ally.maxHP)
            {
                ally.Heal(20f);
                playedEffect = true;
            }
        }

        if (playedEffect)
        {
            currentMana -= 30f;
            PopUpEmotionText($"✨広域ヒール!(MP:{Mathf.RoundToInt(currentMana)})");
            if (visual != null) { visual.PlayHeal(); BattleVfx.Burst(transform.position, new Color(0.5f, 0.9f, 0.5f), 0.5f); } // 詠者：回復モーション＋光輪
        }
    }

    public void Heal(float amount)
    {
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        PopUpEmotionText($"✨HP+{Mathf.RoundToInt(amount)}");
        if (visual != null) visual.SetHP(maxHP > 0 ? currentHP / maxHP : 1f);
        BattleVfx.Heal(transform.position); // 🌿 回復される側にエフェクト
    }

    private void TargetNextDestination()
    {
        if (gridSystem == null) return;

        if (currentHP <= maxHP * 0.3f)
        {
            if (!isRetreating)
            {
                isRetreating = true;
                isFighting = false;
                assaultingCore = false;
                Debug.Log($"😱『退却』入り口へ逃走！");
            }
            CalculatePathTo(startPos);
            return;
        }

        if (isRetreating)
        {
            CalculatePathTo(startPos);
            return;
        }

        // 👑 踏破目的：門番ボス生存中はまず門番を、撃破後(or不在)は目標セルへ
        ZombieAI guardian = ZombieAI.GetLivingGuardian();
        bool corePresent = DemonLord.Instance != null && DemonLord.Instance.IsPresent; // 🏢 最下層のみ魔王が居る
        // 🎯 目標セル：最下層は魔王(DemonLordCell)、非最下層は下り階段(=BossCell)。
        //    ・ボス要素を置くとBossCellだけ更新されDemonLordCellと乖離するため、
        //      降下判定(FloorManagerはBossCellを見る)と必ず一致させる。ここがズレると
        //      『ボス撃破後に別セルへ向かって降下しない＝スタック』になる。
        Vector2Int coreCell = corePresent ? gridSystem.DemonLordCell : gridSystem.BossCell;

        if (adventurerPurpose == Purpose.Conquer && corePresent && guardian == null && currentGridPos == coreCell)
        {
            assaultingCore = true; // 門番不在/撃破 → 魔王の間で討伐開始
            currentPath.Clear();
            return;
        }

        Vector2Int bestTarget = new Vector2Int(-1, -1);
        float highestAttraction = -1f;

        if (adventurerPurpose == Purpose.Conquer)
        {
            if (guardian != null)
            {
                assaultingCore = false;          // 門番生存中は魔王を狙わない
                bestTarget = guardian.MyGridPos; // まず門番ボスを倒しに行く
                highestAttraction = 999f;        // 最優先
            }
            else
            {
                // 門番撃破後/不在 → 最下層なら魔王の間、それ以外は下り階段(=ボスセル)を最優先で目指す。
                // ⚠ ここを宝箱(魅力50)や部屋より低くすると、踏破者が寄り道して満足→退却し、
                //    『ボス撃破→次フロア』が発火しない。門番を排除したら核/階段へ確実に向かわせる。
                bestTarget = coreCell;
                highestAttraction = conquerCoreAttraction; // 部屋/宝箱を上回る高優先度
            }
        }

        // 探索目的の部屋/宝箱選び。踏破目的で門番排除後(core優先)は上書きしない。
        bool conquerCommitted = (adventurerPurpose == Purpose.Conquer && guardian == null);
        if (!conquerCommitted)
        {
            // 🗺️ 『魅力 ÷ 距離』で選ぶ＝近い順に食っていく。
            //    以前は全マップから最大魅力へ直行していたため、広い階層でも往復するだけで
            //    広さが滞在時間に化けなかった。距離を効かせることで、階層を広げるほど巡回が長くなる。
            for (int x = 0; x < gridSystem.MapWidth; x++)
            {
                for (int y = 0; y < gridSystem.MapHeight; y++)
                {
                    DungeonGridSystem.TileType t = gridSystem.GetTileType(x, y);
                    if (t == DungeonGridSystem.TileType.Room || t == DungeonGridSystem.TileType.TreasureChest || t == DungeonGridSystem.TileType.Trap)
                    {
                        GameObject roomObj = gridSystem.GetGridObject(x, y);
                        if (roomObj != null)
                        {
                            RoomData data = roomObj.GetComponent<RoomData>();
                            if (data == null || !data.IsTargetable()) continue;
                            int dist = Mathf.Abs(x - currentGridPos.x) + Mathf.Abs(y - currentGridPos.y);
                            float score = data.attraction / (1f + dist * distanceFalloff);
                            if (score > highestAttraction)
                            {
                                highestAttraction = score;
                                bestTarget = new Vector2Int(x, y);
                            }
                        }
                    }
                }
            }
        }

        if (bestTarget.x != -1) CalculatePathTo(bestTarget);
        else
        {
            if (currentGridPos != startPos)
            {
                isRetreating = true;
                CalculatePathTo(startPos);
            }
        }
    }

    private void CalculatePathTo(Vector2Int target)
    {
        if (currentGridPos == target) return;
        if (currentPath.Count > 0 && pathIndex < currentPath.Count && currentPath[currentPath.Count - 1] == target) return; 

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        queue.Enqueue(currentGridPos);
        cameFrom[currentGridPos] = currentGridPos;

        bool found = false;
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == target) { found = true; break; }

            foreach (Vector2Int dir in directions)
            {
                Vector2Int next = current + dir;
                if (cameFrom.ContainsKey(next)) continue;

                DungeonGridSystem.TileType tileType = gridSystem.GetTileType(next.x, next.y);
                bool isWalkable = (tileType != DungeonGridSystem.TileType.None || next == startPos);

                if (isWalkable)
                {
                    queue.Enqueue(next);
                    cameFrom[next] = current;
                }
            }
        }

        if (found)
        {
            currentPath.Clear();
            Vector2Int curr = target;
            while (curr != currentGridPos)
            {
                currentPath.Add(curr);
                curr = cameFrom[curr];
            }
            currentPath.Reverse();
            pathIndex = 0;
        }
        else currentPath.Clear();
    }

    private void HandleMovement()
    {
        if (currentPath == null || pathIndex >= currentPath.Count) return;

        Vector3 targetWorldPos = gridSystem.GridToWorld(currentPath[pathIndex].x, currentPath[pathIndex].y);
        // 🗿 泥濘の碑（トーテム）：範囲内では足が遅くなる＝罠と防衛体に長く晒される
        float mire = Mathf.Max(0.4f, 1f - DungeonFeatureManager.TotemSumAt(transform.position, TotemCatalog.Kind.Mire))
                     * DungeonTheme.HeroSpeedMult;                          // 🏔️ 氷雪は足を取られる
        transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * mire * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetWorldPos) < 0.05f)
        {
            currentGridPos = currentPath[pathIndex];
            pathIndex++;
            CheckRoomEffectAt(currentGridPos);
            if (pathIndex >= currentPath.Count) OnReachedDestination();
        }
        else
        {
            Vector2Int instantGrid = gridSystem.WorldToGrid(transform.position);
            if (instantGrid != lastTriggeredTrapPos)
            {
                lastTriggeredTrapPos = new Vector2Int(-1, -1);
            }
        }
    }

    private void CheckRoomEffectAt(Vector2Int gridPos)
    {
        if (gridSystem == null) return;

        GameObject roomObj = gridSystem.GetGridObject(gridPos.x, gridPos.y);
        if (roomObj != null)
        {
            RoomData data = roomObj.GetComponent<RoomData>();
            if (data != null && data.CanExecuteEffect())
            {
                if (data.roomType == RoomData.RoomType.Trap && adventurerJob == Job.Thief)
                {
                    if (gridPos == lastTriggeredTrapPos) return;
                    lastTriggeredTrapPos = gridPos; 

                    if (currentMana >= 25f)
                    {
                        currentMana -= 25f; 

                        if (Random.Range(0, 100) < 80) 
                        {
                            PopUpEmotionText($"⚙️解除成功!(MP:{Mathf.RoundToInt(currentMana)})");
                            data.DisableTrapTemporarily(10.0f); 
                            return; 
                        }
                        else
                        {
                            PopUpEmotionText("💥解除失敗!!");
                        }
                    }
                    else
                    {
                        PopUpEmotionText("❌マナ不足解除不能!");
                    }
                }

                data.ExecuteEffect(); 
                currentJoy += data.joyValue;
                currentFear += data.fearValue;

                if (data.roomType == RoomData.RoomType.TreasureChest && data.joyValue > 0) PopUpEmotionText("JOY!");
                else if (data.roomType == RoomData.RoomType.Trap && data.fearValue > 0) PopUpEmotionText("FEAR!");

                // 🌟 感情ツリーへ感情/カウンタ供給
                var et = EmotionTreeManager.Instance;
                if (et != null)
                {
                    if (data.roomType == RoomData.RoomType.TreasureChest) { et.AddEmotion(EmotionTreeManager.Route.Joy, 2); et.CountChest(); }
                    else if (data.roomType == RoomData.RoomType.Trap) { et.AddEmotion(EmotionTreeManager.Route.Despair, 2); et.CountTrap(); }
                }

                if (data.damageValue > 0 || data.roomType == RoomData.RoomType.Trap)
                {
                    float dmg = data.damageValue;
                    if (data.roomType == RoomData.RoomType.Trap)
                    {
                        // ⚖️ 固定値だけだと後半に腐るので『最大HP比』成分を足す（研究 d_trap_pow* で伸びる）
                        dmg = TrapCatalog.InstantDamage(data.trapKind, maxHP);
                        if (et != null) dmg *= et.TrapDamageMult; // 絶望ツリーで罠強化
                        if (RelicManager.Instance != null) dmg *= RelicManager.Instance.TrapDamageMult; // 🏺 遺物で罠強化
                        dmg *= 1f + DungeonFeatureManager.TotemSumAt(transform.position, TotemCatalog.Kind.Forge); // 🗿 業火の炉
                        pendingTrapDamage = true;
                    }
                    TakeDamage(dmg);
                    if (data.roomType == RoomData.RoomType.Trap) ApplyTrapStatus(data.trapKind); // 🪤 種類に応じた状態異常
                }

                // 😌『Ⅱ 満足値』部屋は微増、宝箱/罠は大きめ、感情でさらに加算
                float gain = satisfyRoomGain;
                if (data.roomType == RoomData.RoomType.TreasureChest)
                {
                    gain = satisfyChestGain;
                    carriedGear += 1f + data.joyValue * 0.05f; // 🎁 宝箱の戦利品を持ち出す（richなほど装備量大）
                }
                else if (data.roomType == RoomData.RoomType.Trap) gain = satisfyTrapGain;
                gain += (data.joyValue + data.fearValue) * satisfyEmotionFactor;
                gain *= 1f + DungeonFeatureManager.TotemSumAt(transform.position, TotemCatalog.Kind.Panic); // 🗿 恐慌の面：早く満足して帰る
                satisfaction += gain;

                if (!isRetreating && satisfaction >= satisfactionThreshold)
                {
                    isRetreating = true;
                    isFighting = false;
                    PopUpEmotionText("満足…帰ろう🚶");
                    Debug.Log($"😌『満足帰還』満足値 {satisfaction:F0}/{satisfactionThreshold:F0} 到達 → 入口へ帰還");
                    CalculatePathTo(startPos);
                }
            }
        }
    }

    private void OnReachedDestination()
    {
        if (isRetreating && currentGridPos == startPos)
        {
            GrantReturnReward();
            Destroy(gameObject);
            return;
        }
        TargetNextDestination();
    }

    // 生還時の感情DP清算（帰還・強制退場で共通利用）。＝"逃がした"扱い→噂拡散で脅威度上昇。
    private void GrantReturnReward()
    {
        float rewardBonus = 1.0f + (adventurerLevel * 0.03f);
        int earnedDP = Mathf.RoundToInt((currentJoy + currentFear) * rewardBonus);
        int earnedFame = 10;
        if (DungeonResourceManager.Instance != null)
        {
            DungeonResourceManager.Instance.AddDP(earnedDP);
            DungeonResourceManager.Instance.AddFame(earnedFame);
        }
        LureEconomy.OnHeroEscaped(adventurerLevel); // 🕸️ 泳がせ：逃がすと噂が広まり脅威度↑＋Fame↑
        LureEconomy.OnGearEscaped(carriedGear);     // 🎁 両刃：略奪装備を持ち逃げ→敵陣の装備水準↑
    }

    // ⏱️『Ⅲ 安全網』時間切れ時：入口へ強制退却させる（歩いて帰り感情DPを清算）
    public void ForceRetreat()
    {
        if (isRetreating) return;
        isRetreating = true;
        isFighting = false;
        CalculatePathTo(startPos);
    }

    // ⏱️『Ⅲ ハード終了』猶予後もまだ残っている冒険者を感情DP清算して退場させる
    public void ForceDespawnWithReward()
    {
        GrantReturnReward();
        Destroy(gameObject);
    }

    private bool lastDamageWasTrap = false, pendingTrapDamage = false; // 🏺 実績『罠でとどめ』判定用

    public void TakeDamage(float damage)
    {
        lastDamageWasTrap = pendingTrapDamage; pendingTrapDamage = false;
        currentHP -= damage;
        PopUpEmotionText($"💥HP:{Mathf.Max(0, Mathf.RoundToInt(currentHP))}");
        if (visual != null) { visual.SetHP(maxHP > 0 ? currentHP / maxHP : 0f); if (currentHP > 0) visual.PlayHurt(); }

        if (currentHP <= 0)
        {
            float killBonusMultiplier = 1.0f + (adventurerLevel * 0.05f);
            int killBonusDP = Mathf.RoundToInt(50 * killBonusMultiplier);
            int droppedMaterials = 1 + LureEconomy.GearRecoverMaterials(carriedGear); // 🎁 略奪者を倒すと戦利品を素材で回収（武装拡散を防ぐ）

            // 🌟 殺戮ツリー：撃破DP・素材ボーナス＋感情/カウンタ
            var et = EmotionTreeManager.Instance;
            if (et != null)
            {
                // 🗿 血の香炉：この場所で倒すと感情が濃く採れる
                float censer = 1f + DungeonFeatureManager.TotemSumAt(transform.position, TotemCatalog.Kind.Censer);
                et.AddEmotion(EmotionTreeManager.Route.Slaughter, Mathf.RoundToInt(3 * censer)); et.CountKill();
                killBonusDP = Mathf.RoundToInt(killBonusDP * et.KillDPMult);
                droppedMaterials += et.KillMaterialBonus;
            }
            if (DemonLord.Instance != null) droppedMaterials += DemonLord.Instance.RefineLootBonus; // 🔨 錬成ランクで戦利品が増える
            if (RelicManager.Instance != null) killBonusDP = Mathf.RoundToInt(killBonusDP * RelicManager.Instance.KillDPMult); // 🏺 遺物で撃破DP
            killBonusDP = Mathf.RoundToInt(killBonusDP * LureEconomy.RevenueMult); // 🕸️ 脅威度が高い(強い勇者)ほど撃破DPが旨い
            killBonusDP = Mathf.RoundToInt(killBonusDP * NarrativeSystem.KillDpMult); // 🕯️ 形見『血染めの首飾り』

            // 🏢 深度ボーナス：深い階層で倒すほど旨い（浅い階で皆殺しにせず、深く誘い込む理由になる）
            float depth = DungeonFloorManager.CurrentDepthRewardMult;
            killBonusDP = Mathf.RoundToInt(killBonusDP * depth);
            droppedMaterials = Mathf.RoundToInt(droppedMaterials * depth);

            RelicManager.ReportHeroBeaten(adventurerRank);                 // 🏺 実績：高ランク撃破
            EurekaTracker.OnAdventurerDefeated();                          // ⏳ 時代の偉業のカウント
            if (lastDamageWasTrap) { RelicManager.ReportTrapKill(); EurekaTracker.OnTrapKill(); }   // 🏺実績＋💡天啓：罠でとどめ
            if (hasSpell) EurekaTracker.OnMagicKill();                                              // 💡天啓：魔法持ちを倒した
            DungeonResourceManager.AddKillDPRecord(killBonusDP);           // 🏺 実績：撃破DPの累計

            if (DungeonResourceManager.Instance != null)
            {
                DungeonResourceManager.Instance.AddDP(killBonusDP);
                DungeonResourceManager.Instance.AddMaterial(droppedMaterials);
            }
            if (visual != null) visual.Die(); // 🎭 倒れ演出（切り離して自壊。AI本体は即destroyでカウント整合）
            Destroy(gameObject);
        }
    }

    private void PopUpEmotionText(string text)
    {
        if (emotionTextMesh == null) return;
        if (emotionCoroutine != null) StopCoroutine(emotionCoroutine);
        emotionCoroutine = StartCoroutine(AnimateEmotion(text));
    }

    private System.Collections.IEnumerator AnimateEmotion(string text)
    {
        emotionTextMesh.text = text;
        emotionTextMesh.gameObject.SetActive(true);

        float timer = 0f;
        float duration = 1.4f; 
        Vector3 startLocalPos = new Vector3(0f, 0.8f, -1f); 

        while (timer < duration)
        {
            timer += Time.deltaTime;
            startLocalPos.y += 0.25f * Time.deltaTime;
            emotionTextMesh.transform.localPosition = startLocalPos;
            yield return null;
        }
        emotionTextMesh.gameObject.SetActive(false);
    }
}