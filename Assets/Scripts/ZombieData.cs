using UnityEngine;
using System.Collections.Generic;

public class ZombieData : MonoBehaviour
{
    private DungeonGridSystem gridSystem;

    [Header("Zombie Status")]
    [SerializeField] private float maxHP = 50f;
    private float currentHP;
    [SerializeField] private float moveSpeed = 2f; 
    [SerializeField] private float attackDamage = 20f; 
    
    // ⭐ クールダウンのスパンを部屋（8秒）より短い「1.0秒」に設定
    [SerializeField] private float attackInterval = 1.0f; 

    [Header("AI Patrol Settings")]
    [SerializeField] private int searchRange = 5; 
    
    private Vector2Int spawnGridPos; 
    private Vector2Int currentGridPos;
    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int pathIndex = 0;

    private AdventurerAI targetAdventurer; 
    private float attackTimer = 0f; // 💡現在のクールダウン経過時間
    private float searchTimer = 0f;
    private float searchInterval = 0.3f; 

    private void Start()
    {
        currentHP = maxHP;

        gridSystem = GameObject.FindAnyObjectByType<DungeonGridSystem>();
        if (gridSystem == null) return;

        currentGridPos = gridSystem.WorldToGrid(transform.position);
        transform.position = gridSystem.GridToWorld(currentGridPos.x, currentGridPos.y);
        
        spawnGridPos = currentGridPos;

        // 起動時は最初から攻撃可能な状態（タイマー満タン）にしておく
        attackTimer = attackInterval;
    }

    private void Update()
    {
        if (gridSystem == null) return;

        // 🔥【新ロジック】攻撃のクールタイム（リチャージ）を、移動中であっても裏で常に進める！
        if (attackTimer < attackInterval)
        {
            attackTimer += Time.deltaTime;
        }

        // 1. 定期的に周囲の冒険者をスキャンする
        searchTimer += Time.deltaTime;
        if (searchTimer >= searchInterval)
        {
            searchTimer = 0f;
            FindNearestAdventurer();
        }

        // 2. 行動分岐
        if (targetAdventurer != null)
        {
            Vector2Int advGridPos = gridSystem.WorldToGrid(targetAdventurer.transform.position);
            float dist = Vector2Int.Distance(currentGridPos, advGridPos);

            // 隣接マスまたは同じマスにいるなら攻撃チャンス
            if (dist <= 1.0f)
            {
                currentPath.Clear(); // 足を止めて攻撃に集中
                
                // 🔥クールダウンが完了（1秒経過）している時だけ確実に殴る！
                if (attackTimer >= attackInterval)
                {
                    ExecuteAttack();
                }
            }
            else
            {
                // 🔥【バグ修正】遠くに離れても、ここで「attackTimer = 0」とリセットするのを完全廃止！
                CalculatePathTo(advGridPos);
                HandleMovement();
            }
        }
        else
        {
            if (currentGridPos != spawnGridPos)
            {
                CalculatePathTo(spawnGridPos);
                HandleMovement();
            }
        }
    }

    private void FindNearestAdventurer()
    {
        AdventurerAI[] allAdventurers = Object.FindObjectsByType<AdventurerAI>();
        AdventurerAI closest = null;
        float minDistance = searchRange;

        foreach (var adv in allAdventurers)
        {
            if (adv == null) continue;
            
            Vector2Int advGridPos = gridSystem.WorldToGrid(adv.transform.position);
            float dist = Vector2Int.Distance(currentGridPos, advGridPos);

            if (dist <= searchRange && dist < minDistance)
            {
                minDistance = dist;
                closest = adv;
            }
        }

        targetAdventurer = closest;
    }

    // 🔥【新機能】実際に攻撃を繰り出す処理
    private void ExecuteAttack()
    {
        if (targetAdventurer == null) return;

        attackTimer = 0f; // 💥攻撃したその瞬間に、タイマーを0にしてクールダウンを開始する！
        
        targetAdventurer.TakeDamage(attackDamage);
        Debug.Log($"🧟【ゾンビの迎撃】冒険者に噛みついた！ {attackDamage} ダメージ！（次の攻撃までクールタイム突入）");
    }

    private void CalculatePathTo(Vector2Int target)
    {
        if (currentGridPos == target) return;

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
            Vector2Int curr = target;
            while (curr != currentGridPos)
            {
                currentPath.Add(curr);
                curr = cameFrom[curr];
            }
            currentPath.Reverse();
            pathIndex = 0;
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
        }
    }

    public void TakeDamage(float damage)
    {
        currentHP -= damage;
        Debug.Log($"🧟 ゾンビが {damage} ダメージを受けた！ 残りHP: {currentHP}/{maxHP}");

        if (currentHP <= 0)
        {
            Debug.Log("💀 ゾンビが戦闘不能になり、消滅しました。");
            Destroy(gameObject);
        }
    }
}