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

        // 🔥【超絶パワーアップ】ご要望の確率グラデーションでランクを抽選
        DetermineAdventurerRankByFame();

        TargetNextDestination();
    }

    // 🎯 ユーザー様の理想のプレイ感覚を100%数式化した、なだらかな確率ガチャ関数
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

        // 📈【フェーズA：Fame 0 〜 499（序盤〜中盤）】
        if (fame < 500)
        {
            // PROの確率は、Fame 0で0% ➡ Fame 500で60% に向かって1ごとに「0.12%ずつ」なだらかに上昇
            proChance = (fame / 500f) * 60f; 
            bossChance = 0f; // 500未満の時はBOSSは絶対に配属されない
            normalChance = 100f - proChance;

            /* * 【この数式による実際の確率シミュレーション】
             * • Fame 0   ➡ NORMAL: 100%  /  PRO: 0%   (完全な初期状態)
             * • Fame 100 ➡ NORMAL:  88%  /  PRO: 12%  (ほぼ通常新人、運が良ければPRO！)
             * • Fame 400 ➡ NORMAL:  52%  /  PRO: 48%  (それなりにPROが混ざっている状態)
             */
        }
        // 📉【フェーズB：Fame 500以上〜（終盤・上限なしの無限スケール）】
        else
        {
            // BOSSの確率は、Fame 500の瞬間に「10%」からスタート！
            // そこからFameが100上がるごとに6%ずつ、なだらかに上昇していく（最大50%でストップ）
            bossChance = 10f + ((fame - 500f) * 0.06f);
            bossChance = Mathf.Min(bossChance, 50f); 

            // PROの確率は、Fame 500時点で60%。そこからBOSSに枠をなだらかに譲るため、
            // Fameが100上がるごとに3%ずつ、ゆっくりと減衰していく（最低35%キープ）
            float proTarget = 60f - ((fame - 500f) * 0.03f);
            proChance = Mathf.Max(35f, proTarget);

            // 残った枠がNORMALになる
            normalChance = 100f - proChance - bossChance;

            /* * 【この数式による実際の確率シミュレーション】
             * • Fame 500 ➡ NORMAL: 30%  /  PRO: 60%  /  BOSS: 10% (ほぼ新人orPRO、運が良ければBOSS。新人も30%とそれなりにいる)
             * • Fame 1000➡ NORMAL: 15%  /  PRO: 45%  /  BOSS: 40% (さらにFameが上がると、BOSSとPROの比率がなだらかに逆転していく)
             */
        }

        // 🎲 運命の100分率ダイスロール (0.0 〜 100.0)
        float dieRoll = Random.Range(0f, 100f);

        // 3. ダイスの着地地点に応じてステータスとカラーを確定
        if (dieRoll < bossChance)
        {
            // 👑 BOSS（英雄級）
            maxHP = 200f;
            moveSpeed = 4.2f;
            GetComponent<SpriteRenderer>().color = new Color(1f, 0.2f, 0.2f); // 🟥真っ赤
            PopUpEmotionText("👑BOSS!");
            Debug.Log($"<color=red>🚨【BOSS降臨!!】</color> 現在のFame:{fame} (ガチャ確率: {bossChance:F1}%) ➡ 見事引き当てボスが襲来！ (HP:{maxHP})");
        }
        else if (dieRoll < bossChance + proChance)
        {
            // ⚔️ PRO（玄人・ベテラン級）
            maxHP = 140f;
            moveSpeed = 3.6f;
            GetComponent<SpriteRenderer>().color = new Color(0.2f, 0.5f, 1f); // 🟦鮮やかな青
            PopUpEmotionText("⚔️PRO!");
            Debug.Log($"<color=cyan>⚔️【PRO侵入】</color> 現在のFame:{fame} (ガチャ確率: {proChance:F1}%) ➡ PROのベテランが侵入！ (HP:{maxHP})");
        }
        else
        {
            // 🏃 NORMAL（駆け出しの新人冒険者）
            maxHP = 100f;
            moveSpeed = 3.0f;
            GetComponent<SpriteRenderer>().color = Color.white; // ⬜通常の白
            Debug.Log($"🏃【NORMAL進入】 現在のFame:{fame} (ガチャ確率: {normalChance:F1}%) ➡ 通常の新人冒険者が進入。 (HP:{maxHP})");
        }

        currentHP = maxHP; 
    }

    private void Update()
    {
        if ((currentPath == null || currentPath.Count == 0) && !isRetreating)
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

        if (currentHP <= maxHP * 0.3f)
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