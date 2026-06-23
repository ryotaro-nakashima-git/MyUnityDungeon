using UnityEngine;

public class DungeonGridSystem : MonoBehaviour
{
    public enum TileType { None, Corridor, Room, TreasureChest, Trap }

    // 🔒 インスペクターの上書きに負けないよう、内部で50x50の絶対サイズを強制固定
    private int mapWidth = 50;  
    private int mapHeight = 50; 
    [SerializeField] private float tileSize = 1.0f;

    [Header("Tile Prefabs (GridManager側にもすべてハメ込んでください)")]
    [SerializeField] private GameObject corridorPrefab;
    [SerializeField] private GameObject roomPrefab;
    [SerializeField] private GameObject treasurePrefab;
    [SerializeField] private GameObject trapPrefab;

    [Header("Visual Guide Settings")]
    [SerializeField] private GameObject gridGuidePrefab; 

    private int currentPlayableSize = 10; // 最初は 10x10 マスのみ有効
    public int CurrentPlayableSize => currentPlayableSize;

    public int MapWidth => mapWidth;
    public int MapHeight => mapHeight;

    private TileType[,] gridTypes;
    private GameObject[,] gridObjects;
    private GameObject[,] guideObjects; 

    private void Awake()
    {
        InitializeArrays();

        // 🗺️ 最初は「0〜9 (10x10)」の範囲だけにガイド床を敷き詰める
        GenerateGridGuides(0, 0, currentPlayableSize, currentPlayableSize);
    }

    // 🔥【超絶安全化】インスペクターの数値がどうなっていても、必ず50x50の器を強制確保する
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

        if (gridGuidePrefab == null)
        {
            Debug.LogWarning("⚠️ [設定漏れ警告] GridManagerの DungeonGridSystem に 'Grid Guide Prefab' がセットされていません！");
            return;
        }

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
        if (currentPlayableSize >= 50)
        {
            Debug.Log("ℹ️ ダンジョン領域はすでに最大サイズ（50x50）に達しています！");
            return;
        }

        int nextSize = currentPlayableSize + 10;
        int sizeLevel = currentPlayableSize / 10; 
        int requiredDP = 5000 * Mathf.RoundToInt(Mathf.Pow(4, sizeLevel - 1)); 

        if (DungeonResourceManager.Instance != null)
        {
            if (DungeonResourceManager.Instance.TrySpendDP(requiredDP))
            {
                int oldSize = currentPlayableSize;
                currentPlayableSize = nextSize;

                // 🗺️ 新エリアにガイド床を自動生成
                GenerateGridGuides(0, 0, currentPlayableSize, currentPlayableSize);

                Debug.Log($"<color=lime>🗺️【領土創造成功】</color> {requiredDP} DPを消費し、ダンジョン領域が {oldSize}x{oldSize} から <color=yellow>{currentPlayableSize}x{currentPlayableSize}</color> に拡大しました！");
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

        // 💡データ上は配置を成功させるが、もしプレハブが未登録なら見た目の生成だけスキップする安全ガード
        if (prefabToSpawn != null)
        {
            Vector3 position = GridToWorld(x, y);
            GameObject spawnedObject = Instantiate(prefabToSpawn, position, Quaternion.identity);
            spawnedObject.transform.SetParent(transform);
            gridObjects[x, y] = spawnedObject;
        }
        else
        {
            Debug.LogWarning($"ℹ️ [配置ログ] データ上にタイルを配置しました。もし画面に反映されない場合は、GridManagerのインスペクターにプレハブをハメ込んでください。");
        }

        gridTypes[x, y] = type;
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