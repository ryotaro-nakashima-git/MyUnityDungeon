using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class AdventurerAI : MonoBehaviour
{
    private DungeonGridSystem gridSystem;

    [Header("Adventurer Status (Base)")]
    private float moveSpeed = 3f;
    private float maxHP = 100f;
    private float currentHP;

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

        DetermineAdventurerRankByFame();

        TargetNextDestination();
    }

    private void DetermineAdventurerRankByFame()
    {
        int fame = 0;
        if (DungeonResourceManager.Instance != null)
        {
            fame = DungeonResourceManager.Instance.DungeonFame;
        }

        float normalChance = 100f;
        float proChance = 0f;
        float bossChance = 0f;

        if (fame < 500)
        {
            proChance = (fame / 500f) * 60f; 
            bossChance = 0f; 
            normalChance = 100f - proChance;
        }
        else
        {
            bossChance = 10f + ((fame - 500f) * 0.06f);
            bossChance = Mathf.Min(bossChance, 50f); 

            float proTarget = 60f - ((fame - 500f) * 0.03f);
            proChance = Mathf.Max(35f, proTarget);

            normalChance = 100f - proChance - bossChance;
        }

        // 🎲 ダイスロール
        float dieRoll = Random.Range(0f, 100f);

        if (dieRoll < bossChance)
        {
            maxHP = 200f;
            moveSpeed = 4.2f;
            GetComponent<SpriteRenderer>().color = new Color(1f, 0.2f, 0.2f); // 🟥真っ赤
            PopUpEmotionText("👑BOSS!");
            Debug.Log($"<color=red>🚨【BOSS降臨!!】</color> 現在のFame:{fame} (確率: {bossChance:F1}%) ➡ BOSS襲来！(HP:{maxHP})");
        }
        else if (dieRoll < bossChance + proChance)
        {
            maxHP = 140f;
            moveSpeed = 3.6f;
            GetComponent<SpriteRenderer>().color = new Color(0.2f, 0.5f, 1f); // 🟦青色
            PopUpEmotionText("⚔️PRO!");
            Debug.Log($"<color=cyan>⚔️【PRO侵入】</color> 現在のFame:{fame} (確率: {proChance:F1}%) ➡ PRO侵入！(HP:{maxHP})");
        }
        else
        {
            maxHP = 100f;
            moveSpeed = 3.0f;
            GetComponent<SpriteRenderer>().color = Color.white; // ⬜白色
            Debug.Log($"🏃【NORMAL進入】 現在のFame:{fame} (確率: {normalChance:F1}%) ➡ 新人進入。(HP:{maxHP})");
        }

        currentHP = maxHP; 
    }

    private void Update()
    {
        // 🔥【最重要バグ修正】「&& !isRetreating」のロック制限を完全撤廃！
        // 退却中であっても、万が一パスが途切れたり空（0マス）になった場合は、
        // 0.5秒のタイマーで自動的に「入り口への帰り道」を再計算して動き出す自己修復機能を搭載。
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

    private void TargetNextDestination()
    {
        if (gridSystem == null) return;

        // 🔥【バグ修正連動】すでに退却モード（isRetreating）の際も、ここを通過した場合は確実に帰り道を再計算させる
        if (currentHP <= maxHP * 0.3f || isRetreating)
        {
            if (!isRetreating)
            {
                isRetreating = true;
                Debug.Log($"😱【AI思考:逃走】体力が危険（残りHP: {currentHP}/{maxHP}）！ 入り口へ逃げ帰ります！");
            }
            CalculatePathTo(startPos);
            return;
        }

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

        if (bestTarget.x != -1)
        {
            CalculatePathTo(bestTarget);
        }
        else
        {
            if (currentGridPos != startPos && !isRetreating)
            {
                isRetreating = true;
                Debug.Log($"💤【AI思考:帰還】満足したか、狙える宝箱がなくなりました。入り口に戻ります。");
                CalculatePathTo(startPos);
            }
        }
    }

    private void CalculatePathTo(Vector2Int target)
    {
        if (currentGridPos == target) return;

        // すでに移動中かつ、次の1歩が目的地に向かっているなら再計算をスキップして軽量化
        if (currentPath.Count > 0 && pathIndex < currentPath.Count && currentPath[currentPath.Count - 1] == target)
        {
            return; 
        }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        queue.Enqueue(currentGridPos);
        cameFrom[currentGridPos] = currentGridPos;

        bool found = false;
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (current == target)
            {
                found = true;
                break;
            }

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
        else
        {
            // 💡万一見失った場合も、一時的に空にしてタイマーによる次フレーム以降の最速復旧に委ねる
            currentPath.Clear();
        }
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

            if (pathIndex >= currentPath.Count)
            {
                OnReachedDestination();
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
                data.ExecuteEffect(); 

                currentJoy += data.joyValue;
                currentFear += data.fearValue;

                if (data.roomType == RoomData.RoomType.TreasureChest && data.joyValue > 0) PopUpEmotionText("JOY!");
                else if (data.roomType == RoomData.RoomType.Trap && data.fearValue > 0) PopUpEmotionText("FEAR!");

                if (data.damageValue > 0)
                {
                    TakeDamage(data.damageValue);
                }
            }
        }
    }

    private void OnReachedDestination()
    {
        if (isRetreating && currentGridPos == startPos)
        {
            int earnedDP = Mathf.RoundToInt(currentJoy + currentFear);
            int earnedFame = 10;
            if (DungeonResourceManager.Instance != null)
            {
                DungeonResourceManager.Instance.AddDP(earnedDP);
                DungeonResourceManager.Instance.AddFame(earnedFame);
            }
            Debug.Log($"🏁【生還清算】冒険者がダンジョンから無事に脱出しました！ 獲得DP:+{earnedDP} / 知名度:+{earnedFame}");
            Destroy(gameObject);
            return;
        }

        TargetNextDestination();
    }

    public void TakeDamage(float damage)
    {
        currentHP -= damage;
        
        Debug.Log($"💥【ダメージ発生】冒険者が {damage} のダメージを受けた！ 残りHP: {currentHP}/{maxHP}");
        PopUpEmotionText($"💥HP:{Mathf.Max(0, Mathf.RoundToInt(currentHP))}");

        if (currentHP <= 0)
        {
            int killBonusDP = 50;
            int droppedMaterials = 1;
            if (DungeonResourceManager.Instance != null)
            {
                DungeonResourceManager.Instance.AddDP(killBonusDP);
                DungeonResourceManager.Instance.AddMaterial(droppedMaterials);
            }
            Debug.Log($"💀【死亡清算】冒険者が力尽きました... 撃破DP:+{killBonusDP} / 素材:+{droppedMaterials} を獲得！");
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
        float duration = 1.0f; 
        Vector3 startLocalPos = new Vector3(0f, 0.8f, -1f); 

        while (timer < duration)
        {
            timer += Time.deltaTime;
            startLocalPos.y += 0.4f * Time.deltaTime;
            emotionTextMesh.transform.localPosition = startLocalPos;
            yield return null;
        }

        emotionTextMesh.gameObject.SetActive(false);
    }
}