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
    private float regenPerSecond = 1.0f;

    [Header("Mana (MP) System")]
    private float maxMana = 100f;
    private float currentMana = 100f;
    private float manaRegenPerSecond = 0.75f; 

    [Header("Combat Settings")]
    private float attackTimer = 0f;
    private float attackInterval = 1.0f; 
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
    [SerializeField] private Vector2 satisfyThresholdRange = new Vector2(7f, 13f);
    private float satisfaction = 0f;
    private float satisfactionThreshold = 10f;

    [Header("Visual Effects")]
    [SerializeField] private TextMesh emotionTextMesh; 
    private Coroutine emotionCoroutine;

    [Header("AI Logic Settings")]
    private Vector2Int startPos; 
    private Vector2Int currentGridPos;
    public Vector2Int CurrentGridPos => currentGridPos; 
    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int pathIndex = 0;

    private bool isRetreating = false;
    private bool assaultingCore = false; // 👑 魔王の間で討伐中か
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
    }

    private void DetermineAdventurerStatus()
    {
        int fame = 0;
        if (DungeonResourceManager.Instance != null) fame = DungeonResourceManager.Instance.DungeonFame;

        int turn = 1;
        if (DungeonTurnManager.Instance != null) turn = DungeonTurnManager.Instance.CurrentTurn;

        adventurerLevel = Random.Range(
            Mathf.Clamp(1 + (turn * 1) + (fame / 30), 1, 80),
            Mathf.Clamp(3 + (turn * 3) + (fame / 10), 1, 100)
        );

        adventurerPurpose = (Random.Range(0, 2) == 0) ? Purpose.Explore : Purpose.Conquer;
        adventurerJob = (Job)Random.Range(0, 4);

        // 😌 満足閾値：探索目的は高め(長く楽しむ)、踏破目的は低め(早く帰る)
        satisfactionThreshold = Random.Range(satisfyThresholdRange.x, satisfyThresholdRange.y)
                                * ((adventurerPurpose == Purpose.Explore) ? 1.25f : 0.8f);

        float normalChance = 100f;
        float proChance = 0f;
        float bossChance = 0f;

        if (fame < 500)
        {
            proChance = (fame / 500f) * 60f; 
            normalChance = 100f - proChance;
        }
        else
        {
            bossChance = Mathf.Min(10f + ((fame - 500f) * 0.06f), 50f);
            proChance = Mathf.Max(35f, 60f - ((fame - 500f) * 0.03f));
            normalChance = 100f - proChance - bossChance;
        }

        float dieRoll = Random.Range(0f, 100f);
        string rankTitle = "NORMAL";

        if (dieRoll < bossChance)
        {
            maxHP = 200f; moveSpeed = 4.2f; rankTitle = "BOSS";
            GetComponent<SpriteRenderer>().color = new Color(1f, 0.2f, 0.2f); 
        }
        else if (dieRoll < bossChance + proChance)
        {
            maxHP = 140f; moveSpeed = 3.6f; rankTitle = "PRO";
            GetComponent<SpriteRenderer>().color = new Color(0.2f, 0.5f, 1f); 
        }
        else
        {
            maxHP = 100f; moveSpeed = 3.0f; rankTitle = "新人";
            GetComponent<SpriteRenderer>().color = Color.white; 
        }

        string jobName = "";
        switch (adventurerJob)
        {
            case Job.Warrior: maxHP *= 1.3f; jobName = "戦士⚔️"; break;
            case Job.Thief: jobName = "盗賊🎭"; break;
            case Job.Cleric: jobName = "聖職者🌿"; break;
            case Job.Mage: moveSpeed *= 1.1f; jobName = "魔術師🔮"; break;
        }

        float levelMultiplier = 1.0f + (adventurerLevel - 1) * 0.03f;
        maxHP *= levelMultiplier;
        currentHP = maxHP;

        regenPerSecond = (1.0f + (adventurerLevel * 0.1f)) * 0.5f;

        string purposeStr = (adventurerPurpose == Purpose.Explore) ? "探索" : "踏破";
        PopUpEmotionText($"Lv.{adventurerLevel} {jobName}[{purposeStr}]");

        Debug.Log($"📢【パーティ突入】第 {turn} ターン ➡ <color=yellow>Lv.{adventurerLevel} {rankTitle} {jobName} ({purposeStr}目的)</color> が侵入！");
    }

    private void Update()
    {
        if (currentHP < maxHP)
        {
            currentHP = Mathf.Min(maxHP, currentHP + regenPerSecond * Time.deltaTime);
        }

        if (adventurerJob == Job.Mage || adventurerJob == Job.Cleric || adventurerJob == Job.Thief)
        {
            if (currentMana < maxMana)
            {
                currentMana = Mathf.Min(maxMana, currentMana + manaRegenPerSecond * Time.deltaTime);
            }
        }

        HandleTacticalCombat();

        if (assaultingCore && !isRetreating) HandleCoreAssault();

        if (adventurerJob == Job.Cleric && !isRetreating)
        {
            healTimer += Time.deltaTime;
            if (healTimer >= healInterval)
            {
                healTimer = 0f;
                ExecuteAreaHeal();
            }
        }

        if (!isFighting)
        {
            if (currentPath == null || currentPath.Count == 0 || pathIndex >= currentPath.Count)
            {
                searchTimer += Time.deltaTime;
                if (searchTimer >= searchInterval)
                {
                    searchTimer = 0f;
                    TargetNextDestination();
                }
            }
            HandleMovement();
        }
    }

    // 👑 魔王の間に到達した踏破者が、魔王を攻撃する処理
    private void HandleCoreAssault()
    {
        // 門番ボスが(復)存在する場合は魔王討伐を中断（先に門番を倒す）
        if (ZombieAI.GetLivingGuardian() != null) { assaultingCore = false; return; }

        if (DemonLord.Instance == null || !DemonLord.Instance.IsAlive)
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
            float dmg = 15f + adventurerLevel * 0.8f;
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

        // ⚔️【射程調整】魔術師（遠距離）は 2.0f、それ以外の近接職は 1.0f に設定！
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
        float baseDmg = 10f + (adventurerLevel * 0.5f);

        switch (adventurerJob)
        {
            case Job.Warrior:
                PopUpEmotionText("🪓なぎ払い!");
                foreach (ZombieAI z in targets) z.TakeDamageFromAdventurer(baseDmg);
                break;

            case Job.Mage:
                if (currentMana >= 20f)
                {
                    currentMana -= 20f;
                    PopUpEmotionText($"🔥爆魔術!(MP:{Mathf.RoundToInt(currentMana)})");
                    foreach (ZombieAI z in targets) z.TakeDamageFromAdventurer(baseDmg * 1.3f); 
                }
                else
                {
                    PopUpEmotionText("☄️不発弾(マナ不足)");
                    targets[0].TakeDamageFromAdventurer(baseDmg * 0.3f); 
                }
                break;

            case Job.Thief:
                PopUpEmotionText("🗡️バックスタブ!");
                targets[0].TakeDamageFromAdventurer(baseDmg * 2.2f); 
                break;

            case Job.Cleric:
                PopUpEmotionText("🔨叩き潰す!");
                targets[0].TakeDamageFromAdventurer(baseDmg);
                break;
        }
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
        }
    }

    public void Heal(float amount)
    {
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        PopUpEmotionText($"✨HP+{Mathf.RoundToInt(amount)}");
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
                Debug.Log($"😱【退却】入り口へ逃走！");
            }
            CalculatePathTo(startPos);
            return;
        }

        if (isRetreating)
        {
            CalculatePathTo(startPos);
            return;
        }

        // 👑 踏破目的：門番ボス生存中はまず門番を、撃破後(or不在)は魔王の間を目指す
        Vector2Int coreCell = gridSystem.DemonLordCell;
        ZombieAI guardian = ZombieAI.GetLivingGuardian();

        if (adventurerPurpose == Purpose.Conquer && guardian == null && currentGridPos == coreCell)
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
                bestTarget = coreCell;           // 門番撃破後/不在 → 魔王の間へ
                highestAttraction = 35f;
            }
        }

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
                        if (data != null && data.IsTargetable() && data.attraction > highestAttraction)
                        {
                            highestAttraction = data.attraction;
                            bestTarget = new Vector2Int(x, y);
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
        transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);

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

                if (data.damageValue > 0)
                {
                    float dmg = data.damageValue;
                    if (data.roomType == RoomData.RoomType.Trap)
                    {
                        if (et != null) dmg *= et.TrapDamageMult; // 絶望ツリーで罠強化
                        if (RelicManager.Instance != null) dmg *= RelicManager.Instance.TrapDamageMult; // 🏺 遺物で罠強化
                    }
                    TakeDamage(dmg);
                }

                // 😌【Ⅱ 満足値】部屋は微増、宝箱/罠は大きめ、感情でさらに加算
                float gain = satisfyRoomGain;
                if (data.roomType == RoomData.RoomType.TreasureChest) gain = satisfyChestGain;
                else if (data.roomType == RoomData.RoomType.Trap) gain = satisfyTrapGain;
                gain += (data.joyValue + data.fearValue) * satisfyEmotionFactor;
                satisfaction += gain;

                if (!isRetreating && satisfaction >= satisfactionThreshold)
                {
                    isRetreating = true;
                    isFighting = false;
                    PopUpEmotionText("満足…帰ろう🚶");
                    Debug.Log($"😌【満足帰還】満足値 {satisfaction:F0}/{satisfactionThreshold:F0} 到達 → 入口へ帰還");
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

    // 生還時の感情DP清算（帰還・強制退場で共通利用）
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
    }

    // ⏱️【Ⅲ 安全網】時間切れ時：入口へ強制退却させる（歩いて帰り感情DPを清算）
    public void ForceRetreat()
    {
        if (isRetreating) return;
        isRetreating = true;
        isFighting = false;
        CalculatePathTo(startPos);
    }

    // ⏱️【Ⅲ ハード終了】猶予後もまだ残っている冒険者を感情DP清算して退場させる
    public void ForceDespawnWithReward()
    {
        GrantReturnReward();
        Destroy(gameObject);
    }

    public void TakeDamage(float damage)
    {
        currentHP -= damage;
        PopUpEmotionText($"💥HP:{Mathf.Max(0, Mathf.RoundToInt(currentHP))}");

        if (currentHP <= 0)
        {
            float killBonusMultiplier = 1.0f + (adventurerLevel * 0.05f);
            int killBonusDP = Mathf.RoundToInt(50 * killBonusMultiplier);
            int droppedMaterials = 1;

            // 🌟 殺戮ツリー：撃破DP・素材ボーナス＋感情/カウンタ
            var et = EmotionTreeManager.Instance;
            if (et != null)
            {
                et.AddEmotion(EmotionTreeManager.Route.Slaughter, 3); et.CountKill();
                killBonusDP = Mathf.RoundToInt(killBonusDP * et.KillDPMult);
                droppedMaterials += et.KillMaterialBonus;
            }
            if (RelicManager.Instance != null) killBonusDP = Mathf.RoundToInt(killBonusDP * RelicManager.Instance.KillDPMult); // 🏺 遺物で撃破DP

            if (DungeonResourceManager.Instance != null)
            {
                DungeonResourceManager.Instance.AddDP(killBonusDP);
                DungeonResourceManager.Instance.AddMaterial(droppedMaterials);
            }
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