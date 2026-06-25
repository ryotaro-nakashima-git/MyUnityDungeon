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

    [Header("Combat Settings")]
    private float attackTimer = 0f;
    private float attackInterval = 1.0f; // 1秒に1回攻撃
    private bool isFighting = false;      // 現在戦闘で足止めされているか

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
    public Vector2Int CurrentGridPos => currentGridPos; // ゾンビ側から見えるように公開
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

        // 🛡️【最重要：タクティカル戦闘検知】
        // 自分のジョブの攻撃射程内に「生きているゾンビ」がいるか毎フレームスキャンする
        HandleTacticalCombat();

        if (adventurerJob == Job.Cleric && !isRetreating)
        {
            healTimer += Time.deltaTime;
            if (healTimer >= healInterval)
            {
                healTimer = 0f;
                ExecuteAreaHeal();
            }
        }

        // 🛑 戦闘中で足止めされていない場合のみ、移動パトロールAIを動かす
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

    // ⚔️ ジョブの特徴を完全に再現した戦闘システム
    private void HandleTacticalCombat()
    {
        ZombieAI[] allZombies = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
        List<ZombieAI> targetsInRange = new List<ZombieAI>();

        // 🔍 ジョブごとの攻撃射程（マンハッタン距離）の設定
        int attackRange = 0;
        if (adventurerJob == Job.Warrior) attackRange = 1; // 戦士は上下左右1マスまで届く近接
        if (adventurerJob == Job.Mage) attackRange = 2;    // 魔法使いは2マス先まで届く遠隔
        // シーフとクラリックは同じマス（0マス）のみ

        foreach (ZombieAI zombie in allZombies)
        {
            if (zombie.IsDead) continue; // 死んでいるゾンビは無視

            int dist = Mathf.Abs(zombie.MyGridPos.x - currentGridPos.x) + Mathf.Abs(zombie.MyGridPos.y - currentGridPos.y);
            if (dist <= attackRange)
            {
                targetsInRange.Add(zombie);
            }
        }

        // 🛑 射程内に敵がいれば、足を止めて戦闘モードに突入
        if (targetsInRange.Count > 0 && !isRetreating)
        {
            isFighting = true;

            // 🛠️ 攻撃タイマーの進行
            attackTimer += Time.deltaTime;
            if (attackTimer >= attackInterval)
            {
                attackTimer = 0f;
                ExecuteJobSpecificAttack(targetsInRange);
            }
        }
        else
        {
            // 敵が全滅したら、足止めを解除して進軍再開
            isFighting = false;
        }
    }

    // 🔥 ジョブ別の攻撃アルゴリズム
    private void ExecuteJobSpecificAttack(List<ZombieAI> targets)
    {
        // レベルに応じた基本攻撃力計算
        float baseDmg = 10f + (adventurerLevel * 0.5f);

        switch (adventurerJob)
        {
            case Job.Warrior:
                // ⚔️【戦士：近距離範囲攻撃】射程1マスの敵「全員」になぎ払いダメージ！
                PopUpEmotionText("🪓なぎ払い!");
                foreach (ZombieAI z in targets) z.TakeDamageFromAdventurer(baseDmg);
                break;

            case Job.Mage:
                // 🔮【魔法使い：遠距離範囲攻撃】2マス先の敵「全員」に爆風魔術ダメージ！
                PopUpEmotionText("🔥エクスプロージョン!");
                foreach (ZombieAI z in targets) z.TakeDamageFromAdventurer(baseDmg * 0.8f); // 範囲が広い分少し控えめ
                break;

            case Job.Thief:
                // 🎭【シーフ：近距離単体攻撃】同じマスの敵「1体」に強烈な急所クリティカル！
                PopUpEmotionText("🗡️バックスタブ!");
                targets[0].TakeDamageFromAdventurer(baseDmg * 2.2f); // 単体特化の2.2倍！
                break;

            case Job.Cleric:
                // 🌿【聖職者：近距離単体攻撃】同じマスの敵「1体」に通常攻撃
                PopUpEmotionText("🔨叩き潰す!");
                targets[0].TakeDamageFromAdventurer(baseDmg);
                break;
        }
    }

    private void ExecuteAreaHeal()
    {
        if (gridSystem == null) return;
        AdventurerAI[] allAdventurers = Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None);
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
        if (playedEffect) PopUpEmotionText("✨エリアヒール!");
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
                isFighting = false; // 逃げる時は足止めを強制解除
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
            Debug.Log($"👑【踏破成功】Lv.{adventurerLevel} {adventurerJob} が最奥完全踏破！入り口へ直帰します！");
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
                if (data.roomType == RoomData.RoomType.Trap && adventurerJob == Job.Thief)
                {
                    if (Random.Range(0, 100) < 50) 
                    {
                        Debug.Log($"🎭【シーフの神技】Lv.{adventurerLevel} 盗賊が罠を解除！普通の通路に作り変えました！");
                        PopUpEmotionText("⚙️罠解除成功!");
                        gridSystem.PlaceTile(gridPos.x, gridPos.y, DungeonGridSystem.TileType.Corridor);
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