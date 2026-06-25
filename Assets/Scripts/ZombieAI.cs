using UnityEngine;
using UnityEngine.InputSystem;

public class ZombieAI : MonoBehaviour
{
    private DungeonGridSystem gridSystem;
    private SpriteRenderer spriteRenderer;

    [Header("Zombie Status")]
    [SerializeField] private float maxHP = 120f; // 🧱 冒険者を足止めするためタフに設定
    private float currentHP;
    [SerializeField] private float attackPower = 12f;
    [SerializeField] private float attackInterval = 1.2f;
    private float attackTimer = 0f;

    [Header("Resurrect Cost")]
    [SerializeField] private int resurrectCostDP = 100; // ♻️ 復活に必要なDP

    private Vector2Int myGridPos;
    public Vector2Int MyGridPos => myGridPos;

    private bool isDead = false;
    public bool IsDead => isDead;

    private TextMesh hpTextMesh;

    private void Start()
    {
        gridSystem = GameObject.FindAnyObjectByType<DungeonGridSystem>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHP = maxHP;

        if (gridSystem != null)
        {
            myGridPos = gridSystem.WorldToGrid(transform.position);
            // グリッドの真ん中にピタッと吸着
            transform.position = gridSystem.GridToWorld(myGridPos.x, myGridPos.y);
        }

        // 簡易的なHP表示テキストを足元に動的生成
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
        // 💀【復活待機モードの処理】
        if (isDead)
        {
            HandleResurrectClick();
            return;
        }

        // ⚔️【戦闘モードの処理】同じマスにいる冒険者を全スキャンして自動攻撃
        attackTimer += Time.deltaTime;
        if (attackTimer >= attackInterval)
        {
            if (AttackAdventurersInMyTile())
            {
                attackTimer = 0f;
            }
        }
    }

    private bool AttackAdventurersInMyTile()
    {
        // マップ上の全冒険者を検索
        AdventurerAI[] adventurers = Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None);
        bool attacked = false;

        foreach (AdventurerAI adv in adventurers)
        {
            // 同じマスにいる冒険者に一斉に噛みつく（範囲攻撃）
            if (adv.CurrentGridPos == myGridPos)
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

        // 💀 ゾンビ死亡
        if (currentHP <= 0)
        {
            isDead = true;
            currentHP = 0;
            hpTextMesh.text = "☠️復活待機\n(100DP)";
            hpTextMesh.color = Color.red;
            
            // 見た目を黒く半透明にして「墓標」状態にする
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }
            Debug.Log($"💀【防衛線突破】座標 ({myGridPos.x}, {myGridPos.y}) のゾンビが倒されました！準備フェーズ中にクリックで復活可能です。");
        }
    }

    // ♻️【新機能：手動クリック即時復活】
    private void HandleResurrectClick()
    {
        // 時間が止まっている「内政（準備）フェーズ」中のみ復活可能
        if (DungeonTurnManager.Instance == null || !DungeonTurnManager.Instance.IsPreparePhase) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 screenPosition = Mouse.current.position.ReadValue();
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0));
            mouseWorldPos.z = 0;

            if (gridSystem != null)
            {
                Vector2Int mouseGrid = gridSystem.WorldToGrid(mouseWorldPos);
                // 自分の「墓」のマスがクリックされたら
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
            // サイフから100DP引き落とし
            if (DungeonResourceManager.Instance.TrySpendDP(resurrectCostDP))
            {
                isDead = false;
                currentHP = maxHP;
                attackTimer = 0f;

                if (spriteRenderer != null) spriteRenderer.color = Color.white; // 見た目を元に戻す
                UpdateHPText();

                Debug.Log($"<color=lime>🧟【不死者蘇生】</color> {resurrectCostDP} DPを消費し、ゾンビがその場に完全復活しました！");
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