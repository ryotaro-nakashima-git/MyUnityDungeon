using UnityEngine;

public class DungeonGridSystem : MonoBehaviour
{
    public enum TileType { None, Corridor, Room, TreasureChest, Trap }

    // ═══════════════ 🏢 縦の迷宮（F）の土台 ═══════════════
    //
    // ⚠⚠ **なぜこれが要るか**：この作品は長く「盤は1枚」で書かれてきた。各システムは盤を
    //   `FindFirstObjectByType<DungeonGridSystem>()` で掴んでいる（実測20箇所以上）。
    //   1枚のうちは正しく動くが、**2枚目を置いた瞬間に「どちらを掴むか不定」になる**。
    //   掘削・落とし穴・気性・異変は全部この盤を触るので、そこが最初に壊れる。
    //   → 掴む先を **`Active` に一本化**してから、階層ぶんの盤を増やす。
    //
    // ⚠ 階層は**ワールド座標をずらして同時に存在**させる（floorIndex × FloorSpacing）。
    //   同じ座標に重ねると、当たり判定も `WorldToGrid` も階をまたいで混ざる。

    /// <summary>階層1つぶんの世界座標の間隔。盤の最大幅(50)より十分大きく取る。</summary>
    public const float FloorSpacing = 200f;

    private static DungeonGridSystem active;
    private static readonly System.Collections.Generic.List<DungeonGridSystem> boards
        = new System.Collections.Generic.List<DungeonGridSystem>();

    /// <summary>
    /// いま操作・表示している階の盤。⚠ **`FindFirstObjectByType` の代わりに必ずこれを使う。**
    /// 切り替えるのは <see cref="DungeonFloorManager"/> だけ。
    ///
    /// ⚠⚠ **自分で直す仕掛けが要る。** エディタで再コンパイルするとドメインリロードで静的が飛ぶが、
    ///   `Awake` は**再実行されない**ので、登録し直す機会が無いまま null になる（実測でこれを踏んだ）。
    ///   → 空だったら盤を数え直す。⚠ このとき**いちばん浅い階を選ぶ**こと。
    ///     `FindFirstObjectByType` をそのまま返すと、階層が複数あるときに不定になる。
    /// </summary>
    public static DungeonGridSystem Active
    {
        get { if (active == null) RebuildRegistry(); return active; }
    }

    /// <summary>存在している盤（階層ぶん）。</summary>
    public static System.Collections.Generic.IReadOnlyList<DungeonGridSystem> Boards
    {
        get { if (boards.Count == 0) RebuildRegistry(); return boards; }
    }

    private static void RebuildRegistry()
    {
        boards.Clear();
        var found = Object.FindObjectsByType<DungeonGridSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++) boards.Add(found[i]);
        boards.Sort((a, b) => a.floorIndex.CompareTo(b.floorIndex));
        if (active == null && boards.Count > 0) active = boards[0];
    }

    /// <summary>
    /// そのワールド座標がどの階に属するか。⚠ 階は `FloorSpacing` ごとに積んであるので、
    /// Y を割れば階が出る。**冒険者や配下の位置から階を逆引きする唯一の窓口**。
    /// </summary>
    public static int FloorAtWorld(Vector3 world)
    {
        return Mathf.Max(0, Mathf.RoundToInt(world.y / FloorSpacing));
    }

    /// <summary>
    /// 🏢 **いま号令・権能が届く階**（＝プレイヤーが見ている階）。
    ///
    /// ⚠⚠ 縦の迷宮では冒険者も配下も**全階に同時に居る**ので、
    ///   `FindObjectsByType` でシーン全体を拾うと**全部の階に効いてしまう**。
    ///   距離で絞っている処理（接敵・範囲攻撃）は階が200離れているので自然に除外されるが、
    ///   **距離を見ない「全体に効く」系（号令・権能）は明示的に階で絞ること。**
    /// </summary>
    public static int CommandFloor
    {
        get
        {
            var fm = DungeonFloorManager.Instance;
            return fm != null ? fm.CurrentFloorIndex : (Active != null ? Active.FloorIndex : 0);
        }
    }

    /// <summary>その階の盤（無ければ null）。</summary>
    public static DungeonGridSystem Of(int floorIndex)
    {
        for (int i = 0; i < boards.Count; i++)
            if (boards[i] != null && boards[i].floorIndex == floorIndex) return boards[i];
        return null;
    }

    public static void SetActive(DungeonGridSystem g)
    {
        if (g == null) return;
        active = g;
        if (!boards.Contains(g)) boards.Add(g);
    }

    /// <summary>この盤が受け持つ階（0＝B1F）。</summary>
    [SerializeField] private int floorIndex = 0;
    public int FloorIndex => floorIndex;

    /// <summary>この階のワールド原点。⚠ `GridToWorld`/`WorldToGrid` は必ずこれを通す。</summary>
    public Vector3 FloorOrigin => new Vector3(0f, floorIndex * FloorSpacing, 0f);

    public void SetFloorIndex(int i)
    {
        floorIndex = Mathf.Max(0, i);
        if (!boards.Contains(this)) boards.Add(this);
    }

    /// <summary>
    /// 🏢 複製で作った盤の掃除（F-2）。
    /// ⚠⚠ `Instantiate` で増やすと、**複製元の階の座標に生えたタイルとガイドが子として付いてくる**。
    ///   `Awake` は複製の瞬間に走るので `SetFloorIndex` より先で、ガイドは B1F の原点に作られている。
    ///   → 子を全部消してから、自分の原点でガイドを作り直す。
    /// </summary>
    public void ClearAllTilesAndGuides()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            c.SetActive(false); Destroy(c);   // ⚠ 破棄は遅延するので先に非表示（→ [[tooling-traps]]）
        }
        gridTypes = null; gridObjects = null; guideObjects = null;
        InitializeArrays();
        GenerateGridGuides(0, 0, currentPlayableSize, currentPlayableSize);
    }

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

    // 👑『バグ修正解決のプロパティ』
    // AIがエラーを起こさないよう、現在の有効プレイサイズを幅・高さとして安全に公開
    public int MapWidth => currentPlayableSize;
    public int MapHeight => currentPlayableSize;

    private TileType[,] gridTypes;
    private GameObject[,] gridObjects;
    private GameObject[,] guideObjects;

    // 🏰『自動生成用』入口セルとボスセル（DungeonGeneratorが設定）
    private Vector2Int entranceCell = new Vector2Int(0, 0);
    private Vector2Int bossCell = new Vector2Int(9, 9);
    public Vector2Int EntranceCell => entranceCell;
    public Vector2Int BossCell => bossCell;
    public void SetBossCell(Vector2Int cell) { bossCell = cell; } // ボスエリア配置で上書き

    // 👑 魔王(ダンジョンコア)の間＝最深部セル
    private Vector2Int demonLordCell = new Vector2Int(9, 9);
    public Vector2Int DemonLordCell => demonLordCell;

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
        // ⚠ シーンに元から置いてある盤が B1F。以後クローンした盤が自分で `SetFloorIndex` する。
        if (!boards.Contains(this)) boards.Add(this);
        if (active == null) active = this;
        InitializeArrays();
        GenerateGridGuides(0, 0, currentPlayableSize, currentPlayableSize);
    }

    private void OnDestroy()
    {
        boards.Remove(this);
        if (active == this) active = boards.Count > 0 ? boards[0] : null;
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

    // 🗺️ アクティブな広さ(窓)を設定。階層ごとにサイズが違う場合、構築前に呼ぶ。
    public void SetPlayableSize(int n)
    {
        currentPlayableSize = Mathf.Clamp(n, 10, 50);
        GenerateGridGuides(0, 0, currentPlayableSize, currentPlayableSize);
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

    // ⚠⚠ 階層ぶんのオフセットは**この2つだけ**が知っていればよい。
    //   AI・配置・カメラは全部ここを通ってワールド座標を出しているので、
    //   ここに足すだけで「盤が別の場所にある」ことが全体に伝わる（→ [[DungeonFloorManager]]）。
    public Vector3 GridToWorld(int x, int y)
    {
        var o = FloorOrigin;
        return new Vector3(x * tileSize + o.x, y * tileSize + o.y, 0);
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        var o = FloorOrigin;
        int x = Mathf.FloorToInt((worldPosition.x - o.x) / tileSize + 0.5f);
        int y = Mathf.FloorToInt((worldPosition.y - o.y) / tileSize + 0.5f);
        return new Vector2Int(x, y);
    }

    // 🪤 手数料なしでタイルを敷く（罠要素の配置/復元/フロア復元用）。既存オブジェクトは置換し、生成物を返す。
    public GameObject StampTile(int x, int y, TileType type)
    {
        InitializeArrays();
        if (x < 0 || x >= currentPlayableSize || y < 0 || y >= currentPlayableSize) return null;
        if (gridObjects[x, y] != null) { Destroy(gridObjects[x, y]); gridObjects[x, y] = null; }
        GameObject prefab = type == TileType.Corridor ? corridorPrefab
            : type == TileType.Room ? roomPrefab
            : type == TileType.TreasureChest ? treasurePrefab
            : type == TileType.Trap ? trapPrefab : null;
        GameObject go = null;
        if (prefab != null)
        {
            go = Instantiate(prefab, GridToWorld(x, y), Quaternion.identity);
            go.transform.SetParent(transform);
            gridObjects[x, y] = go;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (DungeonTale.Available)
                {
                    if (type == TileType.TreasureChest) { sr.sprite = DungeonTale.S(DungeonTale.Chest); sr.enabled = true; }
                    else if (type == TileType.Trap) { sr.sprite = DungeonTale.S(DungeonTale.Trap); sr.enabled = true; }
                    else sr.enabled = false;
                    sr.color = Color.white;
                }
                else { sr.sprite = TileSpriteFactory.Get(type, currentBuildTint); sr.color = Color.white; }
            }
            var rd = go.GetComponent<RoomData>(); if (rd != null) rd.SetBaseColor(Color.white);
        }
        gridTypes[x, y] = type;
        RepaintTilemap();      // 🗺️ 壁は「床でない所」なので、1マス変えたら描き直す
        return go;
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

            // 🎨 手動配置タイルも手続き生成スプライトで統一（テーマは直近の生成テーマを流用）
            SpriteRenderer sr = spawnedObject.GetComponent<SpriteRenderer>();
            if (sr != null) { sr.sprite = TileSpriteFactory.Get(type, currentBuildTint); sr.color = Color.white; }
            RoomData room = spawnedObject.GetComponent<RoomData>();
            if (room != null) room.SetBaseColor(Color.white);
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

    public void BuildFromMap(TileType[,] generatedMap, Vector2Int entrance, Vector2Int boss, Color spaceTint, bool placeDemonLord = true)
    {
        InitializeArrays();
        currentBuildTint = spaceTint;
        int size = currentPlayableSize;

        // 🧩 再生成時は手動配置した要素(トーテム/スポナー/ボス/特殊敵)も一旦クリア
        // ⚠⚠ **この盤が受け持つ階のぶんだけ**消すこと（F-2）。引数なしで呼ぶと
        //   「いま表示している階」を消すので、他の階の盤を組んでいる最中に無関係な階の配置が消える。
        var featureMgr = Object.FindFirstObjectByType<DungeonFeatureManager>();
        if (featureMgr != null) featureMgr.ClearAllFeatures(floorIndex);

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

        // 🗺️ 床・壁・小物は Tilemap 側で描く（1マス1GameObjectでは壁が描けなかった）→ [[DungeonTilemapView]]
        RepaintTilemap();

        entranceCell = entrance;
        bossCell = boss;

        // 👑 最深部(入口から最遠)を魔王の間として設定。魔王の実在は最下層のみ（placeDemonLord）
        demonLordCell = boss;
        if (DemonLord.Instance != null)
        {
            // ⚠ この盤の階を渡す（魔王は自分の階の盤の座標に立つ）
            if (placeDemonLord) DemonLord.Instance.PlaceAt(demonLordCell, floorIndex);
            else DemonLord.Instance.SetPresent(false);                     // 非最下層は不在化（非表示/無敵無効）
        }

        if (DungeonResourceManager.Instance != null) DungeonResourceManager.Instance.UpdateResourceUIDisplay();
        Debug.Log($"🏰『迷宮構築』size {size}x{size} / 入口 {entrance} / ボス {boss} / 魔王{(placeDemonLord ? "在" : "不在")}");
    }

    /// <summary>🗺️ 床・壁・小物を描き直す。壁を含むのでマスを1つ足し引きしたときも呼ぶ。</summary>
    public void RepaintTilemap()
    {
        if (!DungeonTale.Available) return;
        // ⚠⚠ **この盤が受け持つ階**を渡すこと。`DungeonFloorManager.CurrentFloorIndex`（＝表示中の階）を
        //   渡すと、階層ぶんの盤を順に組むときに**全部が同じ帯に描かれ、最後の階の形が全階に見える**。
        //   （ユーザー報告「1階も2階も同じ形」の原因はこれ）
        int fi = floorIndex;
        var view = DungeonTilemapView.Ensure();
        // 🏔️ 空間テーマ（洞窟/遺跡/城砦/溶岩/氷雪）の色をここで壁と床に流す
        if (currentBuildTint.r > 0.01f || currentBuildTint.g > 0.01f || currentBuildTint.b > 0.01f)
            view.SetTheme(currentBuildTint);
        view.Paint(gridTypes, currentPlayableSize, fi);
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

        // 🎨 見た目の割り当て。
        //  ・床/通路 → **Tilemapが描く**のでこのマスの絵は消す（GameObjectはRoomDataの器として残す）
        //  ・宝箱/罠 → アトラスの絵に差し替え。⚠ Tilemapにしないのは `RoomData` が
        //    クールダウン中に**色を変える**ため（1マスだけ色を変えるのはTilemapだと面倒＝機能が消える）
        SpriteRenderer sr = spawnedObject.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (DungeonTale.Available)
            {
                if (type == TileType.TreasureChest) { sr.sprite = DungeonTale.S(DungeonTale.Chest); sr.enabled = true; }
                else if (type == TileType.Trap) { sr.sprite = DungeonTale.S(DungeonTale.Trap); sr.enabled = true; }
                else sr.enabled = false;                       // 床/通路はTilemapに任せる
                sr.sortingOrder = Mathf.Max(sr.sortingOrder, -20);
                sr.color = Color.white;
            }
            else
            {
                sr.sprite = TileSpriteFactory.Get(type, currentBuildTint);   // 素材が無いときは従来の石畳
                sr.color = Color.white;
            }
        }
        // RoomDataはAwakeでプレハブ色を保持し後で再適用するため、ベース色を白に上書き（テーマはスプライトに焼込済）
        RoomData room = spawnedObject.GetComponent<RoomData>();
        if (room != null) room.SetBaseColor(Color.white);
    }
}