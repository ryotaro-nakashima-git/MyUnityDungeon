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
    private float manaRegenPerSecond = 1.5f; // 🧪ご要望通り、じわじわとかなりゆっくり回復（毎秒1.5MP）

    [Header("Combat Settings")]
    private float attackTimer = 0f;
    private float attackInterval = 1.0f; 
    private bool isFighting = false;      

    private float healTimer = 0f;
    private float healInterval = 2.0f;

    [Header("Emotion Pool")]
    [SerializeField] private float currentJoy = 0f;
    [SerializeField] private float currentFear = 0f;

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
    private float searchTimer = 0f;
    private float searchInterval = 0.5f; 

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
        regenPerSecond = 1.0f + (adventurerLevel * 0.1f);

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

        // 🧪【新機能】マナのじわじわ超低速自動回復（魔法使いと聖職者のみ）
        if (adventurerJob == Job.Mage || adventurerJob == Job.Cleric)
        {
            if (currentMana < maxMana)
            {
                currentMana = Mathf.Min(maxMana, currentMana + manaRegenPerSecond * Time.deltaTime);
            }
        }

        // 🛡️【戦闘すり抜けバグ完全修正の核心】
        // 毎フレーム、リアルタイムの物理位置から正確なグリッド座標を割り出して戦闘を検知する
        HandleTacticalCombat();

        // 聖職者の広域回復タイマー（マナが必要）
        if (adventurerJob == Job.Cleric && !isRetreating)
        {
            healTimer += Time.deltaTime;
            if (healTimer >= healInterval)
            {
                healTimer = 0f;
                ExecuteAreaHeal();
            }
        }

        // 戦闘に足止めされていない時だけ歩く
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

    private void HandleTacticalCombat()
    {
        if (gridSystem == null) return;

        // 🔥移動中も、現在の物理座標からリアルタイムにマスを特定！
        currentGridPos = gridSystem.WorldToGrid(transform.position);

        // FindObjectsByType overload without FindObjectsSortMode is the non-obsolete API
        ZombieAI[] allZombies = FindObjectsByType<ZombieAI>();
        List<ZombieAI> targetsInRange = new List<ZombieAI>();

        int attackRange = 0;
        if (adventurerJob == Job.Warrior) attackRange = 1; 
        if (adventurerJob == Job.Mage) attackRange = 2;    

        foreach (ZombieAI zombie in allZombies)
            {
            if (zombie.IsDead) continue; 

            int dist = Mathf.Abs(zombie.MyGridPos.x - currentGridPos.x) + Mathf.Abs(zombie.MyGridPos.y - currentGridPos.y);
            if (dist <= attackRange)
            {
                targetsInRange.Add(zombie);
            }
        }

        if (targetsInRange.Count > 0 && !isRetreating)
        {
            isFighting = true; // 🚨リアルタイムで即座に足止めロックがかかる！
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
                // 🧪【新機能】魔法使いのマナ消費魔法
                if (currentMana >= 20f)
                {
                    currentMana -= 20f;
                    PopUpEmotionText($"🔥爆魔術!(MP:{Mathf.RoundToInt(currentMana)})");
                    foreach (ZombieAI z in targets) z.TakeDamageFromAdventurer(baseDmg * 1.3f); // マナを消費するので超高威力
                }
                else
                {
                    // マナが枯渇するとヘロヘロの単体通常攻撃に弱体化！
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

        // 🧪【新機能】聖職者のヒールはマナ30を消費する（足りなければ不発）
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
            currentMana -= 30f; // ヒール成功時のみマナを消費
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
                Debug.Log($"😱【退却】Lv.{adventurerLevel} {adventurerJob} が瀕死のため入り口へ逃走！");
            }
            CalculatePathTo(startPos);
            return;
        }

        if (isRetreating)
        {
            CalculatePathTo(startPos);
            return;
        }

        int maxIndex = gridSystem.CurrentPlayableSize - 1;
        Vector2Int bossRoomPos = new Vector2Int(maxIndex, maxIndex);

        if (adventurerPurpose == Purpose.Conquer && currentGridPos == bossRoomPos)
        {
            isRetreating = true;
            Debug.Log($"👑【踏破成功】入り口へ直帰します！");
            PopUpEmotionText("👑ダンジョン踏破!");
            CalculatePathTo(startPos);
            return;
        }

        Vector2Int bestTarget = new Vector2Int(-1, -1);
        float highestAttraction = -1f;

        if (adventurerPurpose == Purpose.Conquer)
        {
            bestTarget = bossRoomPos;
            highestAttraction = 35f; 
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
                // 🎭【シーフの罠一時無効化スキルへの大改造】
                if (data.roomType == RoomData.RoomType.Trap && adventurerJob == Job.Thief)
                {
                    if (Random.Range(0, 100) < 50) 
                    {
                        Debug.Log($"🎭【シーフの神技】座標 ({gridPos.x}, {gridPos.y}) の罠を10秒間、一時機能停止に追い込みました！");
                        PopUpEmotionText("⚙️罠の機能停止に成功!");
                        
                        // 🔥通路に変えるのではなく、罠を10秒間スリープ（薄色）にする！
                        data.DisableTrapTemporarily(10.0f); 
                        return; 
                    }
                }

                data.ExecuteEffect(); 
                currentJoy += data.joyValue;
                currentFear += data.fearValue;

                if (data.roomType == RoomData.RoomType.TreasureChest && data.joyValue > 0) PopUpEmotionText("JOY!");
                else if (data.roomType == RoomData.RoomType.Trap && data.fearValue > 0) PopUpEmotionText("FEAR!");

                if (data.damageValue > 0) TakeDamage(data.damageValue);
            }
        }
    }

    private void OnReachedDestination()
    {
        if (isRetreating && currentGridPos == startPos)
        {
            float rewardBonus = 1.0f + (adventurerLevel * 0.03f); 
            int earnedDP = Mathf.RoundToInt((currentJoy + currentFear) * rewardBonus);
            int earnedFame = 10;
            if (DungeonResourceManager.Instance != null)
            {
                DungeonResourceManager.Instance.AddDP(earnedDP);
                DungeonResourceManager.Instance.AddFame(earnedFame);
            }
            Destroy(gameObject);
            return;
        }
        TargetNextDestination();
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

            if (DungeonResourceManager.Instance != null)
            {
                zombiePrefab_KillBonus(); // 内部通知用（あれば）
                DungeonResourceManager.Instance.AddDP(killBonusDP);
                DungeonResourceManager.Instance.AddMaterial(droppedMaterials);
            }
            Destroy(gameObject);
        }
    }
    private void zombiePrefab_KillBonus(){}

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