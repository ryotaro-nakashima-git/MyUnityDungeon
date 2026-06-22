using UnityEngine;

public class DungeonAdventurerSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("自動スポーンさせる冒険者のプレハブ")]
    [SerializeField] private GameObject adventurerPrefab;

    [Tooltip("スポーンさせる初期位置（入り口座標: (0,0)など）")]
    [SerializeField] private Vector3 spawnPosition = Vector3.zero;

    [Header("Timer Settings")]
    [Tooltip("最初の自動スポーン間隔（秒）")]
    [SerializeField] private float baseSpawnInterval = 10f; 
    
    private float spawnTimer = 0f;

    private void Start()
    {
        // 最初の1秒で1体目を自動で呼ぶ親切設計
        spawnTimer = baseSpawnInterval - 1.0f; 
    }

    private void Update()
    {
        int fame = 0;
        if (DungeonResourceManager.Instance != null)
        {
            fame = DungeonResourceManager.Instance.DungeonFame;
        }

        // 🔥【新ギルドシステム】Fameが1上がるごとにスポーン間隔が0.1秒短くなる！
        // ただし、一瞬で湧きすぎると処理がパンクするので、最短でも「3.5秒に1回」に制限(Clamp)します。
        float currentInterval = Mathf.Max(baseSpawnInterval - (fame * 0.1f), 3.5f);

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= currentInterval)
        {
            spawnTimer = 0f;
            SpawnAdventurer();
        }
    }

    private void SpawnAdventurer()
    {
        if (adventurerPrefab == null) return;

        // 念のため、プレイヤーが入り口の床（None）を消していないかチェック
        DungeonGridSystem gridSystem = GameObject.FindAnyObjectByType<DungeonGridSystem>();
        if (gridSystem != null)
        {
            Vector2Int gridPos = gridSystem.WorldToGrid(spawnPosition);
            if (gridSystem.GetTileType(gridPos.x, gridPos.y) == DungeonGridSystem.TileType.None)
            {
                // 入り口に床がない場合は、迷子になるのを防ぐため自動湧きを安全にスキップ
                return;
            }
        }

        // 冒険者をインスタンス化（生成）
        Instantiate(adventurerPrefab, spawnPosition, Quaternion.identity);
        Debug.Log($"<color=yellow>📢【ギルドの噂】</color> ダンジョンの噂を聞きつけた冒険者が、自動的に進入してきました！");
    }
}