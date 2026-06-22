using UnityEngine;

public class DungeonGridSystem : MonoBehaviour
{
    public enum TileType { None, Corridor, Room, TreasureChest, Trap }

    [Header("Grid Map Size")]
    [SerializeField] private int mapWidth = 50;  // ⭐ 50マスの広大なエリアに拡張
    [SerializeField] private int mapHeight = 50; // ⭐ 50マスの広大なエリアに拡張
    [SerializeField] private float tileSize = 1.0f;

    [Header("Tile Prefabs")]
    [SerializeField] private GameObject corridorPrefab;
    [SerializeField] private GameObject roomPrefab;
    [SerializeField] private GameObject treasurePrefab;
    [SerializeField] private GameObject trapPrefab;

    [Header("Visual Guide Settings")]
    [SerializeField] private GameObject gridGuidePrefab; // ⭐ 追加：配置可能エリアを示す薄い背景ガイドプレハブ

    private TileType[,] gridTypes;
    private GameObject[,] gridObjects;

    private void Awake()
    {
        // インスペクターで設定されたサイズ（50x50）で配列を初期化
        gridTypes = new TileType[mapWidth, mapHeight];
        gridObjects = new GameObject[mapWidth, mapHeight];

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                gridTypes[x, y] = TileType.None;
                gridObjects[x, y] = null;
            }
        }
    }

    private void Start()
    {
        // ⭐【新機能】ゲーム起動時に、50x50の配置可能エリアへ自動的に薄いグリッドガイドを敷き詰める
        if (gridGuidePrefab != null)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    Vector3 pos = GridToWorld(x, y);
                    GameObject guide = Instantiate(gridGuidePrefab, pos, Quaternion.identity, transform);
                    
                    // ガイドが通路や冒険者の邪魔をしないよう、描画レイヤーを圧倒的奥（マイナス値）にする
                    SpriteRenderer sr = guide.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.sortingOrder = -10; 
                    }
                }
            }
            Debug.Log($"🗺️【エリア可視化】{mapWidth}x{mapHeight} の配置可能エリアに背景ガイドを敷き詰めました。");
        }

        // 画面に元からあった部屋を自動登録するセーフティ
        RoomData[] existingRooms = Object.FindObjectsByType<RoomData>();
        foreach (var room in existingRooms)
{
            if (!room.gameObject.name.Contains("(Clone)"))
            {
                Vector2Int gridPos = WorldToGrid(room.transform.position);
                if (gridPos.x >= 0 && gridPos.x < mapWidth && gridPos.y >= 0 && gridPos.y < mapHeight)
                {
                    if (room.roomType == RoomData.RoomType.TreasureChest) gridTypes[gridPos.x, gridPos.y] = TileType.TreasureChest;
                    else if (room.roomType == RoomData.RoomType.Trap) gridTypes[gridPos.x, gridPos.y] = TileType.Trap;
                    else gridTypes[gridPos.x, gridPos.y] = TileType.Room;

                    gridObjects[gridPos.x, gridPos.y] = room.gameObject;
                    room.transform.position = GridToWorld(gridPos.x, gridPos.y);
                }
            }
        }
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        // ⭐ タイルのスプライトpivotがCenterのため、GridToWorld(x,y)はセルの「中心」を返す。
        //    つまりセル(x,y)はワールド空間で (x-0.5 〜 x+0.5) を占める。
        //    FloorToIntだけだと境界が半マスずれるので、RoundToIntで中心に最も近いセルを選ぶ。
        int x = Mathf.RoundToInt(worldPos.x / tileSize);
        int y = Mathf.RoundToInt(worldPos.y / tileSize);
        return new Vector2Int(x, y);
    }

    public Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(x * tileSize, y * tileSize, 0);
    }

    public void PlaceTile(int x, int y, TileType type)
    {
        if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return;
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

        if (prefabToSpawn != null)
        {
            Vector3 position = GridToWorld(x, y);
            GameObject spawnedObject = Instantiate(prefabToSpawn, position, Quaternion.identity);
            spawnedObject.transform.SetParent(transform);
            gridObjects[x, y] = spawnedObject;
        }

        gridTypes[x, y] = type;
    }

    public TileType GetTileType(int x, int y)
    {
        if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return TileType.None;
        return gridTypes[x, y];
    }

    public GameObject GetGridObject(int x, int y)
    {
        if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return null;
        return gridObjects[x, y];
    }

    public int MapWidth => mapWidth;
    public int MapHeight => mapHeight;
}