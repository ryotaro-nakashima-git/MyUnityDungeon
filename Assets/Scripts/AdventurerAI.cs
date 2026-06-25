using UnityEngine;
using System.Collections.Generic;

public class AdventurerAI : MonoBehaviour
{
    private DungeonGridSystem gridSystem;

    // ⚔️ ジョブ（職業）と 🎯 目的の定義
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

    // 🌿 クラリック（聖職者）の回復タイマー
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

        // 🎯 レベル・ランク・ジョブ・目的をすべて全自動ガチャ抽選！
        DetermineAdventurerStatus();

        TargetNextDestination();
    }

    private void DetermineAdventurerStatus()
    {
        int fame = 0;
        if (DungeonResourceManager.Instance != null) fame = DungeonResourceManager.Instance.DungeonFame;

        int turn = 1;
        if (DungeonTurnManager.Instance != null) turn = DungeonTurnManager.Instance.CurrentTurn;

        // レベル決定
        int minLevel = Mathf.Clamp(1 + (turn * 1) + (fame / 30), 1, 80);
        int maxLevel = Mathf.Clamp(3 + (turn * 3) + (fame / 10), 1, 100);
        adventurerLevel = Random.Range(minLevel, maxLevel + 1);

        // 🎲 目的の抽選（50%の確率でダンジョン踏破ガチ勢になる）
        adventurerPurpose = (Random.Range(0, 2) == 0) ? Purpose.Explore : Purpose.Conquer;

        // 🎲 ジョブ（職業）のランダム抽選
        adventurerJob = (Job)Random.Range(0, 4);

        // 従来通りのランク（NORMAL/PRO/BOSS）の確率テーブル算出
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
            GetComponent<SpriteRenderer>().color = new Color(1f, 0.2f, 0.2f); // 🟥赤
        }
        else if (dieRoll < bossChance + proChance)
        {
            maxHP = 140f; moveSpeed = 3.6f; rankTitle = "PRO";
            GetComponent<SpriteRenderer>().color = new Color(0.2f, 0.5f, 1f); // 🟦青
        }
        else
        {
            maxHP = 100f; moveSpeed = 3.0f; rankTitle = "新人";
            GetComponent<SpriteRenderer>().color = Color.white; // ⬜白
        }

        // 🛡️【ジョブ特性によるステータス個別補正】
        string jobName = "";
        switch (adventurerJob)
        {
            case Job.Warrior:
                maxHP *= 1.3f; // 戦士はHPが1.3倍タフ！
                jobName = "戦士⚔️";
                break;
            case Job.Thief:
                jobName = "盗賊🎭"; // シーフは罠を解除できる！
                break;
            case Job.Cleric:
                jobName = "聖職者🌿"; // クラリックは周囲を癒せる！
                break;
            case Job.Mage:
                moveSpeed *= 1.1f; // 魔法使いは少し足が速い！
                jobName = "魔術師🔮";
                break;
        }

        // レベルによるHP乗算強化
        float levelMultiplier = 1.0f + (adventurerLevel - 1) * 0.03f;
        maxHP *= levelMultiplier;
        currentHP = maxHP;

        // 自然回復量
        regenPerSecond = 1.0f + (adventurerLevel * 0.1f);

        // 頭上に「Lv.〇〇 ジョブ[目的]」を表示
        string purposeStr = (adventurerPurpose == Purpose.Explore) ? "探索" : "踏破";
        PopUpEmotionText($"Lv.{adventurerLevel} {jobName}[{purposeStr}]");

        Debug.Log($"📢【パーティ突入】第 {turn} ターン ➡ <color=yellow>Lv.{adventurerLevel} {rankTitle} {jobName} ({purposeStr}目的)</color> が侵入！ (HP: {Mathf.RoundToInt(maxHP)})");
    }

    private void Update()
    {
        // 🌿 じわじわHP自動回復
        if (currentHP < maxHP)
        {
            currentHP = Mathf.Min(maxHP, currentHP + regenPerSecond * Time.deltaTime);
        }

        // 🌿【クラリック固有スキル：周囲の広域ヒール】
        if (adventurerJob == Job.Cleric && !isRetreating)
        {
            healTimer += Time.deltaTime;
            if (healTimer >= healInterval)
            {
                healTimer = 0f;
                ExecuteAreaHeal();
            }
        }

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

    // 🌿 聖職者による周囲2マス以内の味方の一斉回復ロジック
    private void ExecuteAreaHeal()
    {
        AdventurerAI[] allAdventurers = Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None);
        bool playedEffect = false;

        foreach (AdventurerAI ally in allAdventurers)
        {
            if (ally == this) continue; // 自分は除外

            // グリッド上の距離（マンハッタン距離）が2マス以内かチェック
            int dist = Mathf.Abs(ally.currentGridPos.x - this.currentGridPos.x) + Mathf.Abs(ally.currentGridPos.y - this.currentGridPos.y);
            if (dist <= 2 && ally.currentHP < ally.maxHP)
            {
                ally.Heal(20f); // 味方のHPを20回復！
                playedEffect = true;
            }
        }

        if (playedEffect)
        {
            PopUpEmotionText("✨エリアヒール!");
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

        if (currentHP <= maxHP * 0.3f || isRetreating)
        {
            if (!isRetreating)
            {
                isRetreating = true;
                Debug.Log($"😱【退却】Lv.{adventurerLevel} {adventurerJob} が入り口へ逃走！");
            }
            CalculatePathTo(startPos);
            return;
        }

        // 🎯【2大目的AIの分岐】
        // もし「踏破目的」のガチ勢なら、現在の有効サイズの一番右下奥（魔王の部屋・仮）をターゲットにする！
        if (adventurerPurpose == Purpose.Conquer)
        {
            int maxIndex = gridSystem.CurrentPlayableSize - 1;
            Vector2Int bossRoomPos = new Vector2Int(maxIndex, maxIndex);

            // すでに最奥の目の前にいる場合は、周囲の宝箱を漁る
            if (currentGridPos != bossRoomPos)
            {
                CalculatePathTo(bossRoomPos);
                return;
            }
        }

        // 「探索目的」、または踏破目的だが最奥に辿り着いてやることがない場合は、一番魅力的な部屋を探す
        Vector2Int bestTarget = new Vector2Int(-1, -1);
        float highestAttraction = -1f;

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
            if (currentGridPos != startPos && !isRetreating)
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
                // 🎭【シーフ（盗賊）の固有スキル：罠解除】
                if (data.roomType == RoomData.RoomType.Trap && adventurerJob == Job.Thief)
                {
                    if (Random.Range(0, 100) < 50) // 50%の確率で罠解除
                    {
                        Debug.Log($"🎭【シーフの神技】Lv.{adventurerLevel} 盗賊が座標 ({gridPos.x}, {gridPos.y}) の罠を見事に解体・消滅させた！");
                        PopUpEmotionText("⚙️罠解除成功!");
                        
                        // マップから完全に罠を消し去る（通路に戻す、あるいはNoneにする）
                        gridSystem.PlaceTile(gridPos.x, gridPos.y, DungeonGridSystem.TileType.None);
                        return; // ダメージも効果も完全に無効化して処理を抜ける！
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
                DungeonResourceManager.Instance.AddMaterial(earnedFame);
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
        else
        {
            TargetNextDestination();
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
        float duration = 1.4f; // 盛りだくさんなので少し長めに表示
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