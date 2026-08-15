using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class ZombieAI : MonoBehaviour
{
    private DungeonGridSystem gridSystem;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    [Header("Zombie Status")]
    [SerializeField] private float maxHP = 120f; 
    private float currentHP;
    [SerializeField] private float attackPower = 12f;
    [SerializeField] private float attackInterval = 1.2f;
    private float attackTimer = 0f;

    [SerializeField] private float moveSpeed = 1.8f; 
    private float attackRange = 1.5f; 

    [Header("Resurrect Cost")]
    [SerializeField] private int resurrectCostDP = 100;

    // 🧟 配置元(スポナー/ボス/特殊敵)から生成直後に設定される強化倍率
    [HideInInspector] public float hpMult = 1f;
    [HideInInspector] public float atkMult = 1f;
    [HideInInspector] public float speedMult = 1f;
    [HideInInspector] public bool overrideTint = false;
    [HideInInspector] public Color tintColor = Color.white;
    // 👾 GDD見た目の上書き（特殊敵/スポナー敵）。設定時は種族リグでなくGDDスプライトで描画。SpawnDefender直後に設定。
    [HideInInspector] public string gddVisualPath = null;
    [HideInInspector] public float gddVisualScale = 1f;

    // ⚔️ 武器種別（攻撃間隔/射程）／🜏 ゴエティアの名（ボスのみ）
    [HideInInspector] public float weaponIntervalMult = 1f;
    // 🌳 生命の樹（トーテム）：毎秒 最大HPのこの割合を回復する
    [HideInInspector] public float regenPerSec = 0f;
    [HideInInspector] public float weaponRangeBonus = 0f;
    [HideInInspector] public string goetiaName = null;
    public string DisplayName => string.IsNullOrEmpty(goetiaName)
        ? (minionIndex >= 0 ? MinionCatalog.Get(minionIndex).jpName : name)
        : goetiaName;

    // 🔮 魔法（術者ロールのみ）／💫 スキル
    private MagicCatalog.Spell mySpell; private bool hasSpell;
    private bool skRegen, skPack, skThorns, skPoisonBody, skIntimidate, skUndying, skSelfDestruct, skPetrify, skHealAura, skLifedrain;
    private bool undyingUsed;
    private float regenTick, auraTick, packRecalcTick;
    private float packAtkMult = 1f;
    public bool HasSpell => hasSpell;
    /// <summary>🗡️ 戦力の目安（HP×攻撃）。冒険者が「格下かどうか」を測るのに使う。</summary>
    public float CombatPower => Mathf.Max(1f, maxHP * attackPower * 0.01f);
    public string SpellLabel => hasSpell ? mySpell.jpName : "";
    [HideInInspector] public bool isGuardian = false; // 👑 魔王の門番か（生存中は魔王が無敵）

    // 🐺 眷属の種族（不死/獣/魔族）。魔王の種族との相性でボーナスがかかる（DungeonFeatureManagerが設定）
    public enum Species { Undead, Beast, Demonkin } // 不死/獣/魔族
    [HideInInspector] public Species species = Species.Undead;

    // 🗂️ 配下ロスター(MinionCatalog)のindexと役割。DungeonFeatureManager.SpawnDefenderが設定。
    [HideInInspector] public int minionIndex = -1;
    /// <summary>💍 この体の元になった個体ID（装飾品のスキルを引くため／-1＝個体に紐づかない湧き）。</summary>
    [HideInInspector] public int accessoryOwnerId = -1;
    [HideInInspector] public MinionCatalog.Role role = MinionCatalog.Role.Melee;

    // 🧠 気性（→ [[MinionTemperament]]）。**誰を狙うか**と**瀕死でどうなるか**がここで変わる。
    //    ⚠ 数値の取引（HP/攻撃/速度/間隔）は配置側で既に掛けてある。ここで持つのは**挙動**だけ。
    [HideInInspector] public int temper = -1;                 // -1＝気性なし（スポナーの湧きなど）
    private AdventurerAI stickyTarget;                        // 執念：倒すまで変えない相手
    private float baseAttackPowerForTemper, baseMoveSpeedForTemper;
    private bool temperBaseCaptured;

    // 🐺 種族の機械的個性（FamilyTrait）：不死=とどめで再生成 / 獣=被弾・攻撃で加速 / 魔族=吸血
    [Header("Family Trait")]
    [SerializeField] private float lifestealFrac = 0.25f;   // 魔族：与ダメの何割を回復するか
    [SerializeField] private float frenzyPerStack = 0.08f;  // 獣：1スタックの加速率
    [SerializeField] private int frenzyMaxStacks = 8;       // 獣：加速の上限スタック
    private float baseMoveSpeed, baseAttackInterval;
    private int frenzyStacks = 0;
    [HideInInspector] public bool isRaised = false;         // 不死の再生成体（連鎖再生成を防ぐ）
    private DungeonFeatureManager featureMgr;

    // 🛡️ ガードモード：配置セル(アンカー)周辺のみを徘徊し、接敵したら止まって戦う（冒険者を追ってスポーン地点へ行かない）
    [HideInInspector] public bool anchored = false;
    [HideInInspector] public Vector2Int anchorCell;
    [HideInInspector] public int leashRadius = 3;
    private float patrolTimer = 0f;
    private float patrolInterval = 1.4f;

    private Vector2Int myGridPos;
    public Vector2Int MyGridPos => myGridPos;

    private bool isDead = false;
    public bool IsDead => isDead;

    private TextMesh hpTextMesh;
    private CharacterVisual visual;

    // 🗺️『新設』通路を正しく歩くための経路データ
    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int pathIndex = 0;
    private float pathUpdateTimer = 0f;
    private float pathUpdateInterval = 0.2f; // 0.2秒ごとに動く冒険者への経路を再計算

    // 👑 生存している門番ボスを返す（居なければnull）。魔王の無敵判定・冒険者の標的切替に使う。
    public static ZombieAI GetLivingGuardian()
    {
        foreach (ZombieAI z in Object.FindObjectsByType<ZombieAI>())
            if (z != null && z.isGuardian && !z.IsDead) return z;
        return null;
    }

    public static bool IsDeadZombieAt(Vector2Int gridPos)
    {
        ZombieAI[] allZombies = Object.FindObjectsByType<ZombieAI>();
        foreach (ZombieAI z in allZombies)
        {
            if (z.MyGridPos == gridPos && z.IsDead)
            {
                return true; 
            }
        }
        return false;
    }

    private void Start()
    {
        gridSystem = GameObject.FindAnyObjectByType<DungeonGridSystem>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 🧟 生成元からの強化倍率を反映（currentHP計算の前に）
        maxHP *= hpMult; attackPower *= atkMult; moveSpeed *= speedMult;
        maxHP *= MutationSystem.DefenderHpMult;   // 🧬 世界の変異『呪詛』
        currentHP = maxHP;
        // ⚔️ 武器種別：手数(間隔)と間合い(射程)。攻撃力側は生成元で atkMult に乗せてある。
        attackInterval *= weaponIntervalMult;
        attackRange += weaponRangeBonus;
        baseMoveSpeed = moveSpeed; baseAttackInterval = attackInterval; // 🐺 獣の加速の基準値

        // 🔮 魔法：術者ロールなら解禁済みの属性・階級で詠唱する（研究で強くなる）
        if (minionIndex >= 0 && MagicCatalog.TryPickMinionSpell(minionIndex, out mySpell)) hasSpell = true;
        // 💫 スキル：形態ごとの個性を適用（Tier2は研究解禁が必要）
        ApplySkillsOnSpawn();
        featureMgr = Object.FindFirstObjectByType<DungeonFeatureManager>(); // 🪦 不死の再生成呼び出し用

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        else
        {
            originalColor = new Color(0.5f, 1f, 0.5f);
        }
        if (overrideTint && spriteRenderer != null)
        {
            spriteRenderer.color = tintColor;
            originalColor = tintColor;
        }

        if (gridSystem != null)
        {
            myGridPos = gridSystem.WorldToGrid(transform.position);
            transform.position = gridSystem.GridToWorld(myGridPos.x, myGridPos.y);
        }

        GameObject txtObj = new GameObject("HPText");
        txtObj.transform.SetParent(transform);
        txtObj.transform.localPosition = new Vector3(0f, -0.4f, -1f);
        hpTextMesh = txtObj.AddComponent<TextMesh>();
        hpTextMesh.fontSize = 24;
        hpTextMesh.characterSize = 0.08f;
        hpTextMesh.anchor = TextAnchor.MiddleCenter;
        hpTextMesh.color = Color.green;
        UpdateHPText();

        // 🎭 眷属リグ（種族別／門番は拡大＋王冠）を生成。旧スプライト/HPテキストは隠す
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        if (hpTextMesh != null) hpTextMesh.gameObject.SetActive(false);
        var vgo = new GameObject("Visual"); vgo.transform.SetParent(transform, false);
        visual = vgo.AddComponent<CharacterVisual>();
        CharacterVisual.RigType rt = species == Species.Beast ? CharacterVisual.RigType.Beast
            : species == Species.Demonkin ? CharacterVisual.RigType.Demonkin : CharacterVisual.RigType.Undead;
        // 🎨 見た目の優先順：
        //    ① GDD上書き(特殊敵/スポナー) ② **種類ごとの1枚絵**([[MinionSprite]]) ③ 獣=Enemy Galore ④ SPUM
        //    ②を①の次に置くのは、**34種の名前と姿を一致させる**のが目的だから
        //    （以前は不死12種が全部同じ骸骨、獣10種は割当なしだった）。絵が無い種は自動的に③④へ落ちる。
        var dtSprite = string.IsNullOrEmpty(gddVisualPath) ? MinionSprite.ByIndex(minionIndex) : null;
        if (dtSprite != null)
        {
            visual.InitDungeonTale(dtSprite, rt, isGuardian ? 1.4f : 1f, isGuardian, SpumMap.MinionAlpha(minionIndex));
            visual.SetDungeonTaleId(MinionCatalog.Get(minionIndex).id);   // 🎬 コマ送りが有る種は動き出す
        }
        else if (!string.IsNullOrEmpty(gddVisualPath))
            visual.InitGdd(gddVisualPath, rt, gddVisualScale * (isGuardian ? 1.4f : 1f), false, isGuardian);
        else if (species == Species.Beast && BeastMap.TryGet(minionIndex, out var bd))
            visual.InitBeast(bd.prefab, rt, bd.scale * (isGuardian ? 1.4f : 1f), bd.faceLeft, isGuardian);
        else
            visual.InitSpum(SpumMap.MinionPath(minionIndex), rt, isGuardian ? 1.4f : 1f, isGuardian, SpumMap.MinionAlpha(minionIndex));
        visual.SetHP(1f);
    }

    private void Update()
    {
        if (isDead)
        {
            HandleResurrectClick();
            return;
        }

        TickSkills(Time.deltaTime); // 💫 再生／治癒の波動／群れ
        TickTemper();               // 🧠 気性（静謐の再生／不屈・狂騒の瀕死強化）

        // 🛡️ ガードモード（ボス/特殊敵/スポナー召喚体）：アンカー周辺を徘徊し、接敵時のみ戦う
        if (anchored)
        {
            GuardUpdate();
            return;
        }

        AdventurerAI target = FindClosestAdventurer();
        bool isInRange = false;

        if (target != null)
        {
            float dist = Vector3.Distance(transform.position, target.transform.position);
            
            if (dist > attackRange)
            {
                // ⏱️ 冒険者は動くため、定期的に『通路を通るルート』を再計算する
                pathUpdateTimer += Time.deltaTime;
                if (pathUpdateTimer >= pathUpdateInterval)
                {
                    pathUpdateTimer = 0f;
                    Vector2Int targetGrid = gridSystem.WorldToGrid(target.transform.position);
                    CalculatePathTo(targetGrid);
                }

                // 🗺️ 直線移動ではなく、計算された経路（通路）に沿って移動する
                HandlePathMovement();
            }
            else
            {
                // 敵が射程内（1.5f）に入ったら移動ルートをクリアして足を止める
                isInRange = true;
                currentPath.Clear();
            }
        }

        attackTimer += Time.deltaTime;
        if (attackTimer >= attackInterval && isInRange)
        {
            if (AttackAdventurersInRange())
            {
                attackTimer = 0f;
            }
        }
    }

    // 🛡️ ガードモードの行動：接敵したら止まって戦い、そうでなければアンカー周辺をランダム徘徊
    private void GuardUpdate()
    {
        AdventurerAI target = FindClosestAdventurer();
        bool inRange = target != null && Vector3.Distance(transform.position, target.transform.position) <= attackRange;

        if (inRange)
        {
            currentPath.Clear(); // 接敵したら足を止める（追わない）
            attackTimer += Time.deltaTime;
            if (attackTimer >= attackInterval)
            {
                if (AttackAdventurersInRange()) attackTimer = 0f;
            }
            return;
        }

        // アンカー(配置セル)周辺をランダム徘徊（冒険者を追いかけない）
        patrolTimer += Time.deltaTime;
        if (currentPath == null || pathIndex >= currentPath.Count || patrolTimer >= patrolInterval)
        {
            patrolTimer = 0f;
            CalculatePathTo(PickPatrolCell());
        }
        HandlePathMovement();
    }

    // アンカーから leashRadius 以内で、歩ける（壁でない）ランダムなマスを選ぶ
    private Vector2Int PickPatrolCell()
    {
        if (gridSystem == null) return anchorCell;
        for (int i = 0; i < 10; i++)
        {
            int dx = Random.Range(-leashRadius, leashRadius + 1);
            int dy = Random.Range(-leashRadius, leashRadius + 1);
            if (Mathf.Abs(dx) + Mathf.Abs(dy) > leashRadius) continue;
            Vector2Int c = anchorCell + new Vector2Int(dx, dy);
            if (gridSystem.GetTileType(c.x, c.y) != DungeonGridSystem.TileType.None) return c;
        }
        return anchorCell;
    }

    // 🗺️『新設』壁をすり抜けず、確定した経路に沿って移動する処理
    private void HandlePathMovement()
    {
        if (currentPath == null || pathIndex >= currentPath.Count) return;

        Vector3 targetWorldPos = gridSystem.GridToWorld(currentPath[pathIndex].x, currentPath[pathIndex].y);
        transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetWorldPos) < 0.05f)
        {
            if (gridSystem != null)
            {
                myGridPos = currentPath[pathIndex];
            }
            pathIndex++;
        }
    }

    // 🗺️『新設』None（壁）を避けて歩ける床（通路や部屋）だけを探すアルゴリズム
    private void CalculatePathTo(Vector2Int targetPos)
    {
        if (myGridPos == targetPos) return;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        queue.Enqueue(myGridPos);
        cameFrom[myGridPos] = myGridPos;

        bool found = false;
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == targetPos) { found = true; break; }

            foreach (Vector2Int dir in directions)
            {
                Vector2Int next = current + dir;
                if (cameFrom.ContainsKey(next)) continue;

                // 領土外のチェック
                if (next.x < 0 || next.x >= gridSystem.CurrentPlayableSize || next.y < 0 || next.y >= gridSystem.CurrentPlayableSize) continue;

                DungeonGridSystem.TileType tileType = gridSystem.GetTileType(next.x, next.y);
                
                // 🛑『最重要』床が『None（何もない壁）』ではないタイル（通路や部屋、罠など）だけを歩行可能とする
                bool isWalkable = (tileType != DungeonGridSystem.TileType.None);

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
            Vector2Int curr = targetPos;
            while (curr != myGridPos)
            {
                currentPath.Add(curr);
                curr = cameFrom[curr];
            }
            currentPath.Reverse();
            pathIndex = 0;
        }
    }

    /// <summary>
    /// 🧠 **誰を狙うか**。既定は「いちばん近い」だが、気性で変わる（→ [[MinionTemperament]]）。
    ///
    /// ⚠ どの気性も**距離を完全に無視しない**。無視すると、盤の反対側の相手へ延々歩いて
    ///   1体も殴らないまま波が終わる（＝配置の意味が消える）。
    ///   「近さ」を基準点にして、狙いたい相手に**重みを掛ける**形にしてある。
    /// </summary>
    private AdventurerAI FindClosestAdventurer()
    {
        AdventurerAI[] adventurers = Object.FindObjectsByType<AdventurerAI>();
        var aim = temper >= 0 ? MinionTemperament.Get(temper).aim : MinionTemperament.Aim.Nearest;

        // 執念：狙った相手が生きている限り変えない
        if (aim == MinionTemperament.Aim.Sticky && stickyTarget != null && stickyTarget.gameObject.activeInHierarchy)
            return stickyTarget;

        // 🔎 まず**いちばん近い相手までの距離**を測り、そこから `AimWindow` マス以内を候補にする。
        //
        // ⚠⚠ 最初は「距離に重みを掛けて最小を選ぶ」形で書いたが、**どの気性も『近い順』と
        //   同じ相手を選んだ**（実測）。距離の比（2.0 と 9.0 なら 4.5倍）に対して重みが小さすぎたから。
        //   重みを上げれば今度は盤の端まで歩いて何も殴らなくなる。
        //   → **「近くに何人か居るとき、その中で誰を選ぶか」**という形に変えた。
        //     気性は狙いを変えるが、遠くの相手を無理に追いはしない。これなら両立する。
        float nearest = Mathf.Infinity;
        foreach (AdventurerAI a0 in adventurers)
        { if (a0 == null) continue; float dd = Vector3.Distance(transform.position, a0.transform.position); if (dd < nearest) nearest = dd; }
        if (float.IsInfinity(nearest)) return null;
        float window = nearest + AimWindow;

        AdventurerAI best = null;
        float bestScore = Mathf.Infinity;
        foreach (AdventurerAI adv in adventurers)
        {
            if (adv == null) continue;
            float dist = Vector3.Distance(transform.position, adv.transform.position);
            if (aim != MinionTemperament.Aim.Nearest && dist > window) continue;   // 窓の外は見ない
            float score = dist;
            switch (aim)
            {
                // 窓の中でいちばん強い相手（同点は近い方）
                case MinionTemperament.Aim.Strongest: score = -adv.CombatPower * 1000f + dist; break;
                // 窓の中でいちばん弱った相手（とどめ役）
                case MinionTemperament.Aim.Weakest: score = adv.HpFrac * 1000f + dist; break;
                // 窓の中の術者を優先（居なければ近い順に落ちる）
                case MinionTemperament.Aim.Caster:
                    bool caster = adv.CurrentJob == AdventurerAI.Job.Mage || adv.CurrentJob == AdventurerAI.Job.Cleric;
                    score = (caster ? 0f : 1000f) + dist; break;
            }
            if (score < bestScore) { bestScore = score; best = adv; }
        }
        if (aim == MinionTemperament.Aim.Sticky) stickyTarget = best;
        return best;
    }

    /// <summary>🔎 狙いを変えられる範囲（マス）。いちばん近い相手＋この距離までが候補。</summary>
    private const float AimWindow = 4.5f;

    /// <summary>
    /// 🧠 瀕死で伸びる気性（不屈＝攻撃／狂騒＝速度）。
    /// ⚠ **素の値を1回だけ覚えて、そこから作り直す。** 毎フレーム掛けると指数で膨らむ。
    /// </summary>
    private void TickTemper()
    {
        if (temper < 0) return;
        var d = MinionTemperament.Get(temper);
        if (d.regenFrac > 0f && currentHP > 0f && currentHP < maxHP)
        {
            currentHP = Mathf.Min(maxHP, currentHP + maxHP * d.regenFrac * Time.deltaTime);
            UpdateHPText(); if (visual != null) visual.SetHP(maxHP > 0f ? currentHP / maxHP : 0f);
        }
        if (d.lowHpAtk <= 0f && d.lowHpSpeed <= 0f) return;
        if (!temperBaseCaptured) { baseAttackPowerForTemper = attackPower; temperBaseCaptured = true; }
        float hurt = maxHP > 0f ? Mathf.Clamp01(1f - currentHP / maxHP) : 0f;   // 0＝無傷 1＝瀕死
        if (d.lowHpAtk > 0f) attackPower = baseAttackPowerForTemper * (1f + d.lowHpAtk * hurt);
        if (d.lowHpSpeed > 0f) RecomputeSpeed();   // ⚠ 獣の加速と同じ場所で掛ける
    }

    private bool AttackAdventurersInRange()
    {
        if (isDead) return false;

        AdventurerAI[] adventurers = Object.FindObjectsByType<AdventurerAI>();
        bool attacked = false;

        float dealt = 0f;
        foreach (AdventurerAI adv in adventurers)
        {
            float worldDist = Vector3.Distance(transform.position, adv.transform.position);
            if (worldDist <= attackRange)
            {
                // 🔮 魔法：術者は属性魔法で攻撃（威力＝階級、職の耐性で増減、属性の状態異常を付与）
                // 🜲 種族の権能（鬨の声など）の一時強化はここ1箇所だけに掛ける（→ [[LordAuthority]]）
                // 🧬 世界の変異『物理の守り／魔法の守り』もここで効かせる。**術者かどうかで守りが変わる**
                //    ＝ 片方が濃くなったら編成を組み替える、が対策になる（→ [[MutationSystem]]）。
                float dmg = attackPower * packAtkMult * LordAuthority.RallyAtkMult * MutationSystem.DefenderDamageMult(hasSpell);
                if (hasSpell)
                {
                    dmg *= mySpell.power * MagicCatalog.ResistMultVsHero(mySpell.element, adv.CurrentJob) * PolicySystem.MagicPowerMult;   // 🏛️ 政策『秘儀の伝授』
                    adv.TakeDamage(dmg, temper);   // 🧠 とどめの気性を渡す（貪婪の撃破DP）
                    if (mySpell.trapStatus >= 0) adv.ApplyTrapStatus(mySpell.trapStatus);
                    BattleVfx.Burst(adv.transform.position, HexColor(mySpell.colorHex), 0.8f);
                }
                else adv.TakeDamage(dmg, temper);

                // 💫 毒身：殴った相手を毒に／石化の眼光：確率で停止
                if (skPoisonBody) adv.ApplyTrapStatus((int)TrapKind.Poison);
                if (skPetrify && Random.value < 0.2f) adv.ApplyTrapStatus((int)TrapKind.Ice);
                dealt += dmg;
                attacked = true;
            }
        }
        if (attacked)
        {
            if (visual != null)
            {
                var closest = FindClosestAdventurer();
                if (closest != null) visual.FaceTowards(closest.transform.position.x);
                visual.PlayAttack(hasSpell ? CharacterVisual.AttackStyle.Cast : CharacterVisual.AttackStyle.Claw);
            }
            // 🐺 種族個性（攻撃時）
            if (species == Species.Demonkin && dealt > 0f) Lifesteal(dealt); // 魔族：吸血
            else if (species == Species.Beast) AddFrenzy();                   // 獣：加速スタック
            if (skLifedrain && dealt > 0f) Lifesteal(dealt * 1.5f);           // 💫 吸命（魔族の吸血より強力）
        }
        return attacked;
    }

    // ============ 💫 魔物スキル ============
    private void ApplySkillsOnSpawn()
    {
        if (minionIndex < 0) return;
        // 💍 装飾品でこの個体だけが得ているスキル（→ [[AccessoryCatalog]]）。
        //    ⚠ 種のスキルと**同じ変数**へ流し込む。別系統にすると、あとから増えた効果の
        //      片方だけ実装されるという食い違いが必ず起きる。
        var acc = MinionSkillKind.None;
        if (accessoryOwnerId >= 0) acc = MinionRoster.AccessorySkill(accessoryOwnerId);
        System.Func<MinionSkillKind, bool> has = k => MinionSkill.Has(minionIndex, k) || acc == k;

        skRegen = has(MinionSkillKind.Regen);
        skPack = has(MinionSkillKind.PackTactics);
        skThorns = has(MinionSkillKind.Thorns);
        skPoisonBody = has(MinionSkillKind.PoisonBody);
        skIntimidate = has(MinionSkillKind.Intimidate);
        skUndying = has(MinionSkillKind.Undying);
        skSelfDestruct = has(MinionSkillKind.SelfDestruct);
        skPetrify = has(MinionSkillKind.PetrifyGaze);
        skHealAura = has(MinionSkillKind.HealAura);
        skLifedrain = has(MinionSkillKind.Lifedrain);

        if (has(MinionSkillKind.Swift)) // 俊敏
        {
            moveSpeed *= 1.25f; attackInterval *= 0.8f;
            baseMoveSpeed = moveSpeed; baseAttackInterval = attackInterval;
        }
        if (has(MinionSkillKind.Roar)) // 咆哮：出現時に周囲の味方を強化
        {
            foreach (var z in Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None))
            {
                if (z == this || z.IsDead) continue;
                if (Vector3.Distance(transform.position, z.transform.position) <= 2.5f) z.attackPower *= 1.15f;
            }
            BattleVfx.Burst(transform.position, new Color(1f, 0.8f, 0.3f, 1f), 1.0f);
        }
    }

    // スキルの継続処理（再生／治癒の波動／群れの再計算）
    private void TickSkills(float dt)
    {
        if (isDead) return;
        // 🩹 再生スキル ＋ 🌳 生命の樹（トーテム）の範囲回復
        float regenFrac = (skRegen ? 0.02f : 0f) + regenPerSec;
        if (regenFrac > 0f)
        {
            regenTick += dt;
            if (regenTick >= 1f) { regenTick = 0f; if (currentHP > 0 && currentHP < maxHP) { currentHP = Mathf.Min(maxHP, currentHP + maxHP * regenFrac); RefreshHpUI(); } }
        }
        if (skHealAura)
        {
            auraTick += dt;
            if (auraTick >= 3f)
            {
                auraTick = 0f;
                foreach (var z in Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None))
                {
                    if (z.IsDead) continue;
                    if (Vector3.Distance(transform.position, z.transform.position) <= 2.5f) z.HealFromAlly(z.maxHP * 0.06f);
                }
                BattleVfx.Heal(transform.position);
            }
        }
        if (skPack)
        {
            packRecalcTick += dt;
            if (packRecalcTick >= 1f)
            {
                packRecalcTick = 0f; int n = 0;
                foreach (var z in Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None))
                {
                    if (z == this || z.IsDead) continue;
                    if (Vector3.Distance(transform.position, z.transform.position) <= 2.0f) n++;
                }
                packAtkMult = 1f + 0.12f * Mathf.Min(n, 5); // 最大+60%
            }
        }
    }

    private static Color HexColor(string hex) { Color c; ColorUtility.TryParseHtmlString(hex, out c); return c; }

    /// <summary>💫 威圧：この地点の近くに威圧持ちが居れば、冒険者の与ダメージを下げる倍率を返す。</summary>
    public static float IntimidateMultAt(Vector3 pos)
    {
        foreach (var z in Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None))
        {
            if (z.isDead || !z.skIntimidate) continue;
            if (Vector3.Distance(pos, z.transform.position) <= 2.5f) return 0.8f; // -20%
        }
        return 1f;
    }

    /// <summary>📯 魔王の号令『治癒』：最大HPの割合で回復する。回復できたら true。</summary>
    public bool CommandHeal(float frac)
    {
        if (isDead || currentHP <= 0f || currentHP >= maxHP) return false;
        float before = currentHP;
        currentHP = Mathf.Min(maxHP, currentHP + maxHP * frac * MutationSystem.HealMult);   // 🧬 変異『蝕み』
        RefreshHpUI();
        FloatText.Heal(transform.position + new Vector3(0f, 0.5f, 0f), currentHP - before);
        return true;
    }

    // 味方からの回復（治癒の波動）
    public void HealFromAlly(float amount)
    {
        if (isDead || currentHP <= 0) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount * MutationSystem.HealMult);   // 🧬 変異『蝕み』
        RefreshHpUI();
    }
    private void RefreshHpUI()
    {
        UpdateHPText();
        if (visual != null) visual.SetHP(maxHP > 0 ? currentHP / maxHP : 0f);
    }

    // 🩸 魔族：与ダメの一部を自己回復
    private void Lifesteal(float dealt)
    {
        if (currentHP <= 0) return;
        currentHP = Mathf.Min(maxHP, currentHP + dealt * lifestealFrac);
        UpdateHPText();
        if (visual != null) visual.SetHP(maxHP > 0 ? currentHP / maxHP : 0f);
        BattleVfx.Heal(transform.position);
    }

    // 🐆 獣：攻撃/被弾のたびに移動＆攻撃速度が加速（上限あり）
    private void AddFrenzy()
    {
        if (frenzyStacks >= frenzyMaxStacks) return;
        frenzyStacks++;
        RecomputeSpeed();
    }

    /// <summary>
    /// 速度と攻撃間隔を**素の値から1回で作り直す**。
    /// ⚠⚠ 獣の加速（`AddFrenzy`）と気性『狂騒』は**どちらも moveSpeed を書く**。
    ///   それぞれが自分の基準値から代入すると、獣＋狂騒の個体で毎フレーム上書き合戦になって
    ///   速度がちらつく。**掛ける場所を1つにする**のがここ。
    /// </summary>
    private void RecomputeSpeed()
    {
        float f = 1f + frenzyPerStack * frenzyStacks;                 // 🐆 獣の加速
        float t = 1f;
        if (temper >= 0)
        {
            var d = MinionTemperament.Get(temper);
            if (d.lowHpSpeed > 0f && maxHP > 0f) t = 1f + d.lowHpSpeed * Mathf.Clamp01(1f - currentHP / maxHP);
        }
        moveSpeed = baseMoveSpeed * f * t;
        attackInterval = baseAttackInterval / f;
    }

    public void TakeDamageFromAdventurer(float damage)
    {
        if (isDead) return;

        currentHP -= damage;
        // 💢 こちら側の被弾も数字で出す（これが無いと戦闘が棒立ちに見える）
        FloatText.Spawn(transform.position + new Vector3(0f, 0.5f, 0f),
            Mathf.Max(1, Mathf.RoundToInt(damage)).ToString(), new Color(1f, 0.78f, 0.35f), 2.3f);

        // 💫 不屈：致死ダメージを一度だけHP1で耐える
        if (currentHP <= 0 && skUndying && !undyingUsed)
        {
            undyingUsed = true; currentHP = 1f;
            BattleVfx.Burst(transform.position, new Color(1f, 0.9f, 0.4f, 1f), 1.1f);
        }
        // 💫 棘の皮膚：受けたダメージの25%を反射
        if (skThorns && damage > 0f)
        {
            var back = FindClosestAdventurer();
            if (back != null && Vector3.Distance(transform.position, back.transform.position) <= attackRange + 0.6f)
                back.TakeDamage(damage * 0.25f);
        }

        UpdateHPText();
        if (visual != null) { visual.SetHP(maxHP > 0 ? currentHP / maxHP : 0f); if (currentHP > 0) visual.PlayHurt(); }

        if (species == Species.Beast && currentHP > 0) AddFrenzy(); // 🐆 獣：被弾でも加速

        if (currentHP <= 0)
        {
            // 💫 自爆：死亡時に周囲へ大ダメージ
            if (skSelfDestruct)
            {
                foreach (var adv in Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None))
                    if (Vector3.Distance(transform.position, adv.transform.position) <= 2.2f) adv.TakeDamage(attackPower * 3f);
                BattleVfx.Burst(transform.position, new Color(1f, 0.5f, 0.15f, 1f), 1.6f);
            }
            isDead = true;
            currentHP = 0;
            RelicManager.ReportDefenderLost(); // 🏺 実績『無失点で守り切る』の判定用
            hpTextMesh.text = "☠️復活待機\n(100DP)";
            hpTextMesh.color = Color.red;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }
            if (visual != null) visual.SetDowned(true); // 🪦 倒れ状態（復活可）

            // 🪦 不死：とどめを刺されると弱い骸を1体再生成（連鎖しないよう isRaised はスキップ）
            if (species == Species.Undead && !isRaised && featureMgr != null) featureMgr.RaiseUndead(myGridPos);
        }
    }

    private void HandleResurrectClick()
    {
        if (DungeonTurnManager.Instance == null || !DungeonTurnManager.Instance.IsPreparePhase) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 screenPosition = Mouse.current.position.ReadValue();
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0));
            mouseWorldPos.z = 0;

            if (gridSystem != null)
            {
                Vector2Int mouseGrid = gridSystem.WorldToGrid(mouseWorldPos);
                if (mouseGrid == myGridPos)
                {
                    TryResurrect();
                }
            }
        }
    }

    private void TryResurrect()
    {
        if (DungeonResourceManager.Instance != null)
        {
            if (DungeonResourceManager.Instance.TrySpendDP(resurrectCostDP))
            {
                isDead = false;
                currentHP = maxHP;
                attackTimer = 0f;

                if (spriteRenderer != null) spriteRenderer.color = originalColor;
                if (visual != null) { visual.SetDowned(false); visual.SetHP(1f); } // 🌀 復活で立ち上がる
                UpdateHPText();
            }
        }
    }

    private void UpdateHPText()
    {
        if (hpTextMesh != null && !isDead)
        {
            hpTextMesh.text = $"🧟HP:{Mathf.RoundToInt(currentHP)}";
            hpTextMesh.color = Color.green;
        }
    }
}