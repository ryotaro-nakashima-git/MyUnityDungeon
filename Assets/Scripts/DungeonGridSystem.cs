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

    // 🚨【復旧】削ぎ落とされてしまっていた、最大サイズを外部のAIに教える窓口
    public int MapWidth => mapWidth;
    public int MapHeight => mapHeight;

    private TileType[,] gridTypes;
    private GameObject[,] gridObjects;
    private GameObject[,] guideObjects; 

    // 💰【新経済仕様】各タイルの基本建築コスト
    public int GetTileCost(TileType type)
    {
        switch (type)
        {
            case TileType.Corridor: return 20;       // 通路: 20 DP
            case TileType.Room: return 50;           // 普通の部屋: 50 DP
            case TileType.TreasureChest: return 200; // 宝箱部屋: 200 DP
            case TileType.Trap: return 150;          // 罠部屋: 150 DP
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
                Debug.Log($"🗺️【領土創造成功】 {requiredDP} DPを消費し、ダンジョン領域が {oldSize}x{oldSize} から {currentPlayableSize}x{currentPlayableSize} に拡大しました！");
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
}