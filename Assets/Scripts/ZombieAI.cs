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

    private Vector2Int myGridPos;
    public Vector2Int MyGridPos => myGridPos;

    private bool isDead = false;
    public bool IsDead => isDead;

    private TextMesh hpTextMesh;

    // 🗺️【新設】通路を正しく歩くための経路データ
    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int pathIndex = 0;
    private float pathUpdateTimer = 0f;
    private float pathUpdateInterval = 0.2f; // 0.2秒ごとに動く冒険者への経路を再計算

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
    }

    private void Update()
    {
        if (isDead)
        {
            HandleResurrectClick();
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

        foreach (AdventurerAI adv in adventurers)
        {
            float worldDist = Vector3.Distance(transform.position, adv.transform.position);
            if (worldDist <= attackRange)
            {
                adv.TakeDamage(attackPower);
                attacked = true;
            }
        }
        return attacked;
    }

    public void TakeDamageFromAdventurer(float damage)
    {
        if (isDead) return;

        currentHP -= damage;
        UpdateHPText();

        if (currentHP <= 0)
        {
            isDead = true;
            currentHP = 0;
            hpTextMesh.text = "☠️復活待機\n(100DP)";
            hpTextMesh.color = Color.red;
            
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.2f, 0.2f, 0.2f, 0.5f); 
            }
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