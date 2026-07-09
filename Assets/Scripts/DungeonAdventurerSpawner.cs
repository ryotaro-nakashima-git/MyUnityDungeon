using UnityEngine;

public class DungeonAdventurerSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject adventurerPrefab;
    [SerializeField] private Vector3 spawnPosition = Vector3.zero;

    private float spawnTimer = 0f;
    private float currentSpawnInterval = 3.0f;

    // ウェーブの内部状態管理
    private bool isSpawning = false;
    private int totalSpawnCountForThisTurn = 0;
    private int currentSpawnedCount = 0;

    public bool IsSpawning => isSpawning;

    // 🔴 DungeonTurnManagerから戦闘フェーズ開始時に呼ばれるトリガー関数
    public void StartWaveForThisTurn(int turnNumber)
    {
        isSpawning = true;
        currentSpawnedCount = 0;

        // 📈 ターンが進むほど、突入してくる冒険者の数が増える（例: ターン1なら4体、ターン2なら6体...）
        totalSpawnCountForThisTurn = 3 + (turnNumber * 2)
            + (EmotionTreeManager.Instance != null ? EmotionTreeManager.Instance.BonusAdventurers : 0); // 🌟 歓喜ツリー=集客

        // ⚡ ターンが進むほど、ギルドの出撃間隔が縮まり、一気に押し寄せてくる（最短1.5秒間隔）
        currentSpawnInterval = Mathf.Max(4.0f - (turnNumber * 0.2f), 1.5f);
        
        spawnTimer = currentSpawnInterval; // 最初は即座に1体目を湧かせる
    }

    private void Update()
    {
        if (!isSpawning) return;

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= currentSpawnInterval)
        {
            spawnTimer = 0f;
            SpawnAdventurerWaveUnit();
        }
    }

    private void SpawnAdventurerWaveUnit()
    {
        if (adventurerPrefab == null) return;

        // 🏰 自動生成された迷宮の「入口セル」から湧かせる（未生成時はInspectorのspawnPositionにフォールバック）
        Vector3 spawnPos = spawnPosition;
        DungeonGridSystem gridSystem = GameObject.FindAnyObjectByType<DungeonGridSystem>();
        if (gridSystem != null)
        {
            Vector2Int entrance = gridSystem.EntranceCell;
            if (gridSystem.GetTileType(entrance.x, entrance.y) == DungeonGridSystem.TileType.None)
            {
                return; // 入口がまだ床でない（未生成）なら安全にスキップ
            }
            spawnPos = gridSystem.GridToWorld(entrance.x, entrance.y);
        }

        // 生成
        Instantiate(adventurerPrefab, spawnPos, Quaternion.identity);
        currentSpawnedCount++;

        Debug.Log($"📢【ギルドの進撃】冒険者がダンジョンを急襲！ウェーブ進行度: ({currentSpawnedCount}/{totalSpawnCountForThisTurn})");

        // 今回のターンの規定数に達したら、このターンの「湧き（召喚）」自体は終了
        if (currentSpawnedCount >= totalSpawnCountForThisTurn)
        {
            isSpawning = false;
            Debug.Log("🏁【湧き完了】今ターンのすべての冒険者がダンジョン内に進入しました。あとは防衛線の結果を待ちます。");
        }
    }
}