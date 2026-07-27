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
    public string SpellLabel => hasSpell ? mySpell.jpName : "";
    [HideInInspector] public bool isGuardian = false; // 👑 魔王の門番か（生存中は魔王が無敵）

    // 🐺 眷属の種族（不死/獣/魔族）。魔王の種族との相性でボーナスがかかる（DungeonFeatureManagerが設定）
    public enum Species { Undead, Beast, Demonkin } // 不死/獣/魔族
    [HideInInspector] public Species species = Species.Undead;

    // 🗂️ 配下ロスター(MinionCatalog)のindexと役割。DungeonFeatureManager.SpawnDefenderが設定。
    [HideInInspector] public int minionIndex = -1;
    [HideInInspector] public MinionCatalog.Role role = MinionCatalog.Role.Melee;

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

    // 🗺️【新設】通路を正しく歩くための経路データ
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
        // 🎨 見た目：GDD上書き(特殊敵/スポナー)＞獣=Enemy Galore＞不死/魔族=SPUM。未登録は手続きリグへ自動フォールバック。
        if (!string.IsNullOrEmpty(gddVisualPath))
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
                // ⏱️ 冒険者は動くため、定期的に「通路を通るルート」を再計算する
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

    // 🗺️【新設】壁をすり抜けず、確定した経路に沿って移動する処理
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

    // 🗺️【新設】None（壁）を避けて歩ける床（通路や部屋）だけを探すアルゴリズム
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
                
                // 🛑【最重要】床が「None（何もない壁）」ではないタイル（通路や部屋、罠など）だけを歩行可能とする
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

    private AdventurerAI FindClosestAdventurer()
    {
        AdventurerAI[] adventurers = Object.FindObjectsByType<AdventurerAI>();
        AdventurerAI closest = null;
        float minDist = Mathf.Infinity;

        foreach (AdventurerAI adv in adventurers)
        {
            if (adv == null) continue;
            float dist = Vector3.Distance(transform.position, adv.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = adv;
            }
        }
        return closest;
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
                float dmg = attackPower * packAtkMult;
                if (hasSpell)
                {
                    dmg *= mySpell.power * MagicCatalog.ResistMultVsHero(mySpell.element, adv.CurrentJob);
                    adv.TakeDamage(dmg);
                    if (mySpell.trapStatus >= 0) adv.ApplyTrapStatus(mySpell.trapStatus);
                    BattleVfx.Burst(adv.transform.position, HexColor(mySpell.colorHex), 0.8f);
                }
                else adv.TakeDamage(dmg);

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
        skRegen = MinionSkill.Has(minionIndex, MinionSkillKind.Regen);
        skPack = MinionSkill.Has(minionIndex, MinionSkillKind.PackTactics);
        skThorns = MinionSkill.Has(minionIndex, MinionSkillKind.Thorns);
        skPoisonBody = MinionSkill.Has(minionIndex, MinionSkillKind.PoisonBody);
        skIntimidate = MinionSkill.Has(minionIndex, MinionSkillKind.Intimidate);
        skUndying = MinionSkill.Has(minionIndex, MinionSkillKind.Undying);
        skSelfDestruct = MinionSkill.Has(minionIndex, MinionSkillKind.SelfDestruct);
        skPetrify = MinionSkill.Has(minionIndex, MinionSkillKind.PetrifyGaze);
        skHealAura = MinionSkill.Has(minionIndex, MinionSkillKind.HealAura);
        skLifedrain = MinionSkill.Has(minionIndex, MinionSkillKind.Lifedrain);

        if (MinionSkill.Has(minionIndex, MinionSkillKind.Swift)) // 俊敏
        {
            moveSpeed *= 1.25f; attackInterval *= 0.8f;
            baseMoveSpeed = moveSpeed; baseAttackInterval = attackInterval;
        }
        if (MinionSkill.Has(minionIndex, MinionSkillKind.Roar)) // 咆哮：出現時に周囲の味方を強化
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
        if (skRegen)
        {
            regenTick += dt;
            if (regenTick >= 1f) { regenTick = 0f; if (currentHP > 0 && currentHP < maxHP) { currentHP = Mathf.Min(maxHP, currentHP + maxHP * 0.02f); RefreshHpUI(); } }
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

    // 味方からの回復（治癒の波動）
    public void HealFromAlly(float amount)
    {
        if (isDead || currentHP <= 0) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
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
        float f = 1f + frenzyPerStack * frenzyStacks;
        moveSpeed = baseMoveSpeed * f;
        attackInterval = baseAttackInterval / f;
    }

    public void TakeDamageFromAdventurer(float damage)
    {
        if (isDead) return;

        currentHP -= damage;

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