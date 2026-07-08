using UnityEngine;

public class DungeonGridSystem : MonoBehaviour
{
    public enum TileType { None, Corridor, Room, TreasureChest, Trap }

    private int mapWidth = 50;  
    private int mapHeight = 50; 
    [SerializeField] private float tileSize = 1.0f;

    [Header("Tile Prefabs")]
    [SerializeField] private GameObject corridorPrefab;
    [SerializeField] private GameObject roomPrefab;
    [SerializeField] private GameObject treasurePrefab;
    [SerializeField] private GameObject trapPrefab;

    [Header("Visual Guide Settings")]
    [SerializeField] private GameObject gridGuidePrefab; 

    private int currentPlayableSize = 10; 
    public int CurrentPlayableSize => currentPlayableSize;

    // 👑【バグ修正解決のプロパティ】
    // AIがエラーを起こさないよう、現在の有効プレイサイズを幅・高さとして安全に公開
    public int MapWidth => currentPlayableSize;
    public int MapHeight => currentPlayableSize;

    private TileType[,] gridTypes;
    private GameObject[,] gridObjects;
    private GameObject[,] guideObjects;

    // 🏰【自動生成用】入口セルとボスセル（DungeonGeneratorが設定）
    private Vector2Int entranceCell = new Vector2Int(0, 0);
    private Vector2Int bossCell = new Vector2Int(9, 9);
    public Vector2Int EntranceCell => entranceCell;
    public Vector2Int BossCell => bossCell;
    public void SetBossCell(Vector2Int cell) { bossCell = cell; } // ボスエリア配置で上書き

    public int GetTileCost(TileType type)
    {
        switch (type)
        {
            case TileType.Corridor: return 20;       
            case TileType.Room: return 50;           
            case TileType.TreasureChest: return 200; 
            case TileType.Trap: return 150;          
            default: return 0;
        }
    }

    private void Awake()
    {
        InitializeArrays();
        GenerateGridGuides(0, 0, currentPlayableSize, currentPlayableSize);
    }

    private void InitializeArrays()
    {
        mapWidth = 50;
        mapHeight = 50;
        if (gridTypes == null || gridTypes.GetLength(0) != mapWidth) gridTypes = new TileType[mapWidth, mapHeight];
        if (gridObjects == null || gridObjects.GetLength(0) != mapWidth) gridObjects = new GameObject[mapWidth, mapHeight];
        if (guideObjects == null || guideObjects.GetLength(0) != mapWidth) guideObjects = new GameObject[mapWidth, mapHeight];
    }

    private void GenerateGridGuides(int startX, int startY, int endX, int endY)
    {
        InitializeArrays();
        if (gridGuidePrefab == null) return;

        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) continue;
                if (guideObjects[x, y] != null) continue;

                Vector3 position = GridToWorld(x, y);
                position.z = 0.1f; 

                GameObject guide = Instantiate(gridGuidePrefab, position, Quaternion.identity);
                guide.transform.SetParent(transform);
                guideObjects[x, y] = guide;
            }
        }
    }

    public void TryExpandDungeonArea()
    {
        if (currentPlayableSize >= 50) return;

        int nextSize = currentPlayableSize + 10;
        int sizeLevel = currentPlayableSize / 10; 
        int requiredDP = 5000 * Mathf.RoundToInt(Mathf.Pow(4, sizeLevel - 1)); 

        if (DungeonResourceManager.Instance != null)
        {
            if (DungeonResourceManager.Instance.TrySpendDP(requiredDP))
            {
                int oldSize = currentPlayableSize;
                currentPlayableSize = nextSize;
                GenerateGridGuides(0, 0, currentPlayableSize, currentPlayableSize);
                Debug.Log($"领土拡大成功: {oldSize}x{oldSize} -> {currentPlayableSize}x{currentPlayableSize}");
            }
        }
    }

    public Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(x * tileSize, y * tileSize, 0);
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt(worldPosition.x / tileSize + 0.5f);
        int y = Mathf.FloorToInt(worldPosition.y / tileSize + 0.5f);
        return new Vector2Int(x, y);
    }

    public void PlaceTile(int x, int y, TileType type)
    {
        InitializeArrays(); 
        if (x < 0 || x >= currentPlayableSize || y < 0 || y >= currentPlayableSize) return;
        if (gridTypes[x, y] == type) return;

        TileType oldType = gridTypes[x, y];
        int oldTileCost = GetTileCost(oldType);
        int newTileCost = GetTileCost(type);

        if (type != TileType.None)
        {
            if (DungeonResourceManager.Instance != null && !DungeonResourceManager.Instance.TrySpendDP(newTileCost))
            {
                return; 
            }
        }
        else
        {
            if (DungeonResourceManager.Instance != null && oldTileCost > 0)
            {
                bool isBattleNow = DungeonTurnManager.Instance != null && !DungeonTurnManager.Instance.IsPreparePhase;
                DungeonResourceManager.Instance.RefundDP(oldTileCost, isBattleNow);
            }
        }

        if (gridObjects[x, y] != null)
        {
            Destroy(gridObjects[x, y]);
            gridObjects[x, y] = null;
        }

        GameObject prefabToSpawn = null;
        if (type == TileType.Corridor) prefabToSpawn = corridorPrefab;
        else if (type == TileType.Room) prefabToSpawn = roomPrefab;
        else if (type == TileType.TreasureChest) prefabToSpawn = treasurePrefab;
        else if (type == TileType.Trap) prefabToSpawn = trapPrefab;

        if (prefabToSpawn != null)
        {
            Vector3 position = GridToWorld(x, y);
            GameObject spawnedObject = Instantiate(prefabToSpawn, position, Quaternion.identity);
            spawnedObject.transform.SetParent(transform);
            gridObjects[x, y] = spawnedObject;
        }

        gridTypes[x, y] = type;
        if (DungeonResourceManager.Instance != null) DungeonResourceManager.Instance.UpdateResourceUIDisplay();
    }

    public TileType GetTileType(int x, int y)
    {
        InitializeArrays(); 
        if (x < 0 || x >= currentPlayableSize || y < 0 || y >= currentPlayableSize) return TileType.None;
        return gridTypes[x, y];
    }

    public GameObject GetGridObject(int x, int y)
    {
        InitializeArrays();
        if (x < 0 || x >= currentPlayableSize || y < 0 || y >= currentPlayableSize) return null;
        return gridObjects[x, y];
    }

    // ================= 🏰 自動生成（DungeonGenerator）用 API =================

    /// <summary>
    /// 自動生成された迷宮マップ（None=壁/Corridor/Room…）を一括反映する。
    /// 既存タイルはクリアしてから配置し直す。DPは消費しない（生成は無料/別コスト管理）。
    /// </summary>
    private Color currentBuildTint = Color.white;

    public void BuildFromMap(TileType[,] generatedMap, Vector2Int entrance, Vector2Int boss, Color spaceTint)
    {
        InitializeArrays();
        currentBuildTint = spaceTint;
        int size = currentPlayableSize;

        // 🧩 再生成時は手動配置した要素(トーテム/スポナー/ボス/特殊敵)も一旦クリア
        var featureMgr = Object.FindFirstObjectByType<DungeonFeatureManager>();
        if (featureMgr != null) featureMgr.ClearAllFeatures();

        // 既存タイルを全消去
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (gridObjects[x, y] != null)
                {
                    Destroy(gridObjects[x, y]);
                    gridObjects[x, y] = null;
                }
                gridTypes[x, y] = TileType.None;
            }
        }

        // 生成マップを反映
        int genW = generatedMap.GetLength(0);
        int genH = generatedMap.GetLength(1);
        for (int x = 0; x < size && x < genW; x++)
        {
            for (int y = 0; y < size && y < genH; y++)
            {
                TileType t = generatedMap[x, y];
                if (t == TileType.None) continue;
                gridTypes[x, y] = t;
                SpawnTileVisual(x, y, t);
            }
        }

        entranceCell = entrance;
        bossCell = boss;

        if (DungeonResourceManager.Instance != null) DungeonResourceManager.Instance.UpdateResourceUIDisplay();
        Debug.Log($"🏰【迷宮自動生成】size {size}x{size} / 入口 {entrance} / ボス {boss}");
    }

    // DP消費なしでタイルの見た目だけを生成する内部ヘルパー（BuildFromMap専用）
    private void SpawnTileVisual(int x, int y, TileType type)
    {
        GameObject prefabToSpawn = null;
        if (type == TileType.Corridor) prefabToSpawn = corridorPrefab;
        else if (type == TileType.Room) prefabToSpawn = roomPrefab;
        else if (type == TileType.TreasureChest) prefabToSpawn = treasurePrefab;
        else if (type == TileType.Trap) prefabToSpawn = trapPrefab;
        if (prefabToSpawn == null) return;

        Vector3 position = GridToWorld(x, y);
        GameObject spawnedObject = Instantiate(prefabToSpawn, position, Quaternion.identity);
        spawnedObject.transform.SetParent(transform);
        gridObjects[x, y] = spawnedObject;

        // 🎨 空間テーマの色調を反映（部屋はRoomData経由、通路等は直接）
        RoomData room = spawnedObject.GetComponent<RoomData>();
        if (room != null)
        {
            room.ApplyThemeTint(currentBuildTint);
        }
        else
        {
            SpriteRenderer sr = spawnedObject.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = sr.color * currentBuildTint;
        }
    }
}