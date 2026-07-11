using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 区画分割法（BSP）で迷宮（部屋＋通路）を自動生成し、DungeonGridSystemへ流し込むクラス。
/// 出力は TileType(None=壁 / Corridor / Room) なので、既存の冒険者BFS徘徊・接敵はそのまま動く。
/// </summary>
public class DungeonGenerator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("未設定なら実行時に自動で探します")]
    [SerializeField] private DungeonGridSystem gridSystem;

    [Header("Generation Trigger")]
    [Tooltip("ゲーム開始時に自動生成するか")]
    [SerializeField] private bool generateOnStart = true;
    [Tooltip("デバッグ：このキーで再生成")]
    [SerializeField] private bool enableRegenerateKey = true;

    // 迷宮タイプ＝レイアウト傾向 / 空間タイプ＝テーマ（見た目の色調）
    public enum DungeonType { Standard, Labyrinth, Cavern, Warren }
    public enum SpaceType { Cave, Ruins, Fortress, Lava, Ice }

    [Header("Dungeon Style（タイプ/空間）")]
    [Tooltip("迷宮のレイアウト傾向。生成時に下のBSP値を上書きします")]
    [SerializeField] private DungeonType dungeonType = DungeonType.Standard;
    [Tooltip("空間テーマ。タイルの色調に反映")]
    [SerializeField] private SpaceType spaceType = SpaceType.Cave;
    public DungeonType CurrentDungeonType => dungeonType;
    public SpaceType CurrentSpaceType => spaceType;

    // 宝箱の配置量（多いほど生成コスト大／だが得られるDPも増える）
    public enum ChestAmount { Small, Medium, Large }
    [Tooltip("宝箱の量。多いほど生成DPコスト大＝ただし冒険者から得られるDPも増える")]
    [SerializeField] private ChestAmount chestAmount = ChestAmount.Medium;
    public ChestAmount CurrentChestAmount => chestAmount;

    [Header("BSP Settings（区画分割）")]
    [Tooltip("これ以上分割しない区画の最小辺サイズ（小さいほど部屋が増える）")]
    [SerializeField] private int minLeafSize = 4;
    [Tooltip("部屋の最小辺サイズ")]
    [SerializeField] private int roomMinSize = 2;
    [Tooltip("区画の縁に残す壁の余白（マス）")]
    [SerializeField] private int roomMargin = 1;
    [Tooltip("追加のループ通路を掘る確率（迷路に周回路を作る）")]
    [Range(0f, 1f)]
    [SerializeField] private float extraLoopChance = 0.25f;
    [Tooltip("0以外なら固定シードで再現生成（デバッグ用）")]
    [SerializeField] private int seed = 0;

    // BSPの区画（葉ノード）
    private class Leaf
    {
        public int x, y, w, h;
        public Leaf left, right;
        public RectInt room;
        public bool hasRoom;
        public Leaf(int x, int y, int w, int h) { this.x = x; this.y = y; this.w = w; this.h = h; }
        public bool IsLeaf => left == null && right == null;
    }

    private DungeonGridSystem.TileType[,] map;
    private int size;
    private readonly List<Leaf> allLeaves = new List<Leaf>();

    private void Start()
    {
        if (gridSystem == null) gridSystem = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (generateOnStart) GenerateAndBuild();
    }

    private void Update()
    {
        if (!enableRegenerateKey) return;
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.bKey.wasPressedThisFrame)
        {
            Debug.Log("🔁 デバッグ再生成（Bキー）");
            GenerateAndBuild();
        }
    }

    /// <summary>
    /// 迷宮を生成してグリッドへ反映する（外部からも呼べる）。
    /// フロアマネージャがあれば全階層を生成、無ければ単一フロアとして即構築（後方互換）。
    /// </summary>
    public void GenerateAndBuild()
    {
        if (gridSystem == null) gridSystem = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (gridSystem == null)
        {
            Debug.LogError("DungeonGenerator: DungeonGridSystem が見つかりません。");
            return;
        }

        if (DungeonFloorManager.Instance != null)
        {
            DungeonFloorManager.Instance.GenerateAllFloors(); // 複数フロア生成＋B1F構築
            return;
        }

        // ---- 後方互換：単一フロアを生成して即構築 ----
        var fd = BuildFloorData();
        gridSystem.BuildFromMap(fd.map, fd.entrance, fd.boss, fd.tint, true);
        var camCtrl = Object.FindFirstObjectByType<CameraController>();
        if (camCtrl != null) camCtrl.FitToDungeon();
    }

    /// <summary>
    /// 迷宮を1フロア分「生成のみ」して FloorData で返す（グリッドには反映しない）。
    /// DungeonFloorManager が階層ごとに呼ぶ。
    /// </summary>
    public FloorData BuildFloorData(int targetSize = 0)
    {
        if (gridSystem == null) gridSystem = Object.FindFirstObjectByType<DungeonGridSystem>();
        size = targetSize > 0 ? Mathf.Clamp(targetSize, 10, 50) : gridSystem.CurrentPlayableSize; // 🗺️ 階層ごとの広さ指定に対応
        if (seed != 0) Random.InitState(seed);
        ApplyTypePresets(); // 迷宮タイプに応じてBSPパラメータを設定

        // 1) 全面を壁(None)で初期化
        map = new DungeonGridSystem.TileType[size, size];
        allLeaves.Clear();

        // 2) 区画分割（ルート区画から二分木状に分割）
        Leaf root = new Leaf(0, 0, size, size);
        var toSplit = new Queue<Leaf>();
        toSplit.Enqueue(root);
        while (toSplit.Count > 0)
        {
            Leaf leaf = toSplit.Dequeue();
            if (TrySplit(leaf))
            {
                toSplit.Enqueue(leaf.left);
                toSplit.Enqueue(leaf.right);
            }
        }

        // 3) 各葉区画に部屋を掘り、兄弟区画を通路で接続
        CreateRoomsAndCorridors(root);

        // 4) ループ通路（周回路）を少し足して一本道を避ける
        AddExtraLoops();

        // 5) 入口・ボスを決定（入口=原点に最も近い部屋、ボス=入口から最遠の部屋）
        Vector2Int entrance, boss;
        DecideEntranceAndBoss(out entrance, out boss);

        // 5.5) 宝箱をランダム配置（量に応じて数が変わる。入口/ボスは除外）
        PlaceChests(entrance, boss);

        return new FloorData { map = map, entrance = entrance, boss = boss, tint = GetSpaceTint(), isDeepest = false, size = size };
    }

    // 外部（UIボタン等）からタイプ/空間/宝箱量を切り替える
    public void SetDungeonType(int i) { dungeonType = (DungeonType)Mathf.Clamp(i, 0, 3); }
    public void SetSpaceType(int i) { spaceType = (SpaceType)Mathf.Clamp(i, 0, 4); }
    public void SetChestAmount(int i) { chestAmount = (ChestAmount)Mathf.Clamp(i, 0, 2); }

    // 生成コスト＝基本 + 宝箱量サーチャージ（多いほど高い）。UIのコスト表示・DP消費に使う。
    public int GetGenerationCost()
    {
        int baseCost = 500;
        int chestSurcharge = chestAmount == ChestAmount.Small ? 0 : (chestAmount == ChestAmount.Medium ? 300 : 700);
        int floors = DungeonFloorManager.Instance != null ? DungeonFloorManager.Instance.PlannedFloorCount : 1;
        return (baseCost + chestSurcharge) * floors; // 🏢 階層数に比例（深いほど高コスト）
    }

    // DPを消費して生成（UIの「生成」ボタン用）。不足なら生成せずfalse。
    public bool TryGenerateWithCost()
    {
        int cost = GetGenerationCost();
        if (DungeonResourceManager.Instance != null && !DungeonResourceManager.Instance.TrySpendDP(cost))
        {
            Debug.LogWarning($"❌【迷宮生成】DP不足（必要 {cost}）");
            return false;
        }
        GenerateAndBuild();
        return true;
    }

    // 宝箱をランダム配置する（Room セルの一部を TreasureChest に変換）
    private void PlaceChests(Vector2Int entrance, Vector2Int boss)
    {
        int roomCount = Mathf.Max(1, allLeaves.Count);
        float ratio = chestAmount == ChestAmount.Small ? 0.15f : (chestAmount == ChestAmount.Medium ? 0.30f : 0.50f);
        // マップが小さく部屋数が少なくても量差が出るよう下限を設ける（大>中>小を保証）
        int minTier = chestAmount == ChestAmount.Small ? 2 : (chestAmount == ChestAmount.Medium ? 3 : 4);
        int chestCount = Mathf.Max(minTier, Mathf.RoundToInt(roomCount * ratio));

        // 配置候補＝Roomセル（入口/ボスは除外）
        var candidates = new List<Vector2Int>();
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                if (map[x, y] != DungeonGridSystem.TileType.Room) continue;
                if ((x == entrance.x && y == entrance.y) || (x == boss.x && y == boss.y)) continue;
                candidates.Add(new Vector2Int(x, y));
            }

        // シャッフルして先頭 chestCount 個を宝箱に
        for (int i = 0; i < candidates.Count; i++)
        {
            int j = Random.Range(i, candidates.Count);
            var tmp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = tmp;
        }
        int placed = Mathf.Min(chestCount, candidates.Count);
        for (int i = 0; i < placed; i++)
            map[candidates[i].x, candidates[i].y] = DungeonGridSystem.TileType.TreasureChest;

        Debug.Log($"💰【宝箱配置】{placed}個（量:{chestAmount} / 部屋{roomCount}）");
    }

    // 迷宮タイプごとのBSPプリセット（生成時に適用）
    private void ApplyTypePresets()
    {
        switch (dungeonType)
        {
            case DungeonType.Standard:  minLeafSize = 6; roomMinSize = 3; roomMargin = 1; extraLoopChance = 0.20f; break;
            case DungeonType.Labyrinth: minLeafSize = 4; roomMinSize = 2; roomMargin = 1; extraLoopChance = 0.55f; break; // 通路多め・小部屋・周回路多
            case DungeonType.Cavern:    minLeafSize = 8; roomMinSize = 5; roomMargin = 1; extraLoopChance = 0.15f; break; // 大部屋・開けた空間
            case DungeonType.Warren:    minLeafSize = 4; roomMinSize = 2; roomMargin = 0; extraLoopChance = 0.30f; break; // 小部屋密集（蟻の巣）
        }

        // マップが小さいと分割できず1部屋(入口=ボス)に潰れるので、確実に分割できる範囲へクランプ
        int cap = Mathf.Max(3, size / 2 - 1);
        minLeafSize = Mathf.Clamp(minLeafSize, 3, cap);
        roomMinSize = Mathf.Clamp(roomMinSize, 2, Mathf.Max(2, size / 3));
    }

    // 空間テーマの色調（タイルへ乗算）
    public Color GetSpaceTint()
    {
        switch (spaceType)
        {
            case SpaceType.Cave:     return new Color(0.80f, 0.78f, 0.82f);
            case SpaceType.Ruins:    return new Color(0.78f, 0.85f, 0.70f);
            case SpaceType.Fortress: return new Color(0.80f, 0.83f, 0.92f);
            case SpaceType.Lava:     return new Color(0.95f, 0.60f, 0.50f);
            case SpaceType.Ice:      return new Color(0.78f, 0.88f, 1.00f);
        }
        return Color.white;
    }

    // ---- BSP分割 ----
    private bool TrySplit(Leaf leaf)
    {
        if (!leaf.IsLeaf) return false;

        // 分割方向を決める（細長い区画は長辺で割る）
        bool splitHorizontally;
        if (leaf.w > leaf.h && leaf.w / (float)leaf.h >= 1.25f) splitHorizontally = false;
        else if (leaf.h > leaf.w && leaf.h / (float)leaf.w >= 1.25f) splitHorizontally = true;
        else splitHorizontally = Random.value > 0.5f;

        int maxLength = (splitHorizontally ? leaf.h : leaf.w) - minLeafSize;
        if (maxLength <= minLeafSize) return false; // これ以上割れない（葉として確定）

        int split = Random.Range(minLeafSize, maxLength + 1);
        if (splitHorizontally)
        {
            leaf.left = new Leaf(leaf.x, leaf.y, leaf.w, split);
            leaf.right = new Leaf(leaf.x, leaf.y + split, leaf.w, leaf.h - split);
        }
        else
        {
            leaf.left = new Leaf(leaf.x, leaf.y, split, leaf.h);
            leaf.right = new Leaf(leaf.x + split, leaf.y, leaf.w - split, leaf.h);
        }
        return true;
    }

    // ---- 部屋と通路 ----
    private void CreateRoomsAndCorridors(Leaf leaf)
    {
        if (leaf.IsLeaf)
        {
            CarveRoom(leaf);
            return;
        }
        if (leaf.left != null) CreateRoomsAndCorridors(leaf.left);
        if (leaf.right != null) CreateRoomsAndCorridors(leaf.right);
        // 兄弟区画の部屋同士をL字通路で接続
        if (leaf.left != null && leaf.right != null)
        {
            Vector2Int a = GetRoomCenter(leaf.left);
            Vector2Int b = GetRoomCenter(leaf.right);
            CarveCorridor(a, b);
        }
    }

    private void CarveRoom(Leaf leaf)
    {
        int availW = leaf.w - roomMargin * 2;
        int availH = leaf.h - roomMargin * 2;
        if (availW < roomMinSize || availH < roomMinSize)
        {
            // 余白が取れない極小区画は、そのまま最小部屋を埋める
            availW = Mathf.Max(roomMinSize, leaf.w);
            availH = Mathf.Max(roomMinSize, leaf.h);
        }

        int rw = Random.Range(roomMinSize, availW + 1);
        int rh = Random.Range(roomMinSize, availH + 1);
        int rx = leaf.x + roomMargin + Random.Range(0, Mathf.Max(1, (leaf.w - roomMargin * 2) - rw + 1));
        int ry = leaf.y + roomMargin + Random.Range(0, Mathf.Max(1, (leaf.h - roomMargin * 2) - rh + 1));

        leaf.room = new RectInt(rx, ry, rw, rh);
        leaf.hasRoom = true;
        allLeaves.Add(leaf);

        for (int x = rx; x < rx + rw; x++)
            for (int y = ry; y < ry + rh; y++)
                SetCell(x, y, DungeonGridSystem.TileType.Room);
    }

    private Vector2Int GetRoomCenter(Leaf leaf)
    {
        if (leaf.hasRoom)
            return new Vector2Int(leaf.room.x + leaf.room.width / 2, leaf.room.y + leaf.room.height / 2);
        // 子から拾う
        Vector2Int? c = null;
        if (leaf.left != null) c = GetRoomCenter(leaf.left);
        if (c == null && leaf.right != null) c = GetRoomCenter(leaf.right);
        return c ?? new Vector2Int(leaf.x + leaf.w / 2, leaf.y + leaf.h / 2);
    }

    private void CarveCorridor(Vector2Int a, Vector2Int b)
    {
        // L字：水平→垂直、または垂直→水平（ランダム）
        if (Random.value > 0.5f)
        {
            CarveHLine(a.x, b.x, a.y);
            CarveVLine(a.y, b.y, b.x);
        }
        else
        {
            CarveVLine(a.y, b.y, a.x);
            CarveHLine(a.x, b.x, b.y);
        }
    }

    private void CarveHLine(int x0, int x1, int y)
    {
        int from = Mathf.Min(x0, x1), to = Mathf.Max(x0, x1);
        for (int x = from; x <= to; x++) SetCorridorIfEmpty(x, y);
    }

    private void CarveVLine(int y0, int y1, int x)
    {
        int from = Mathf.Min(y0, y1), to = Mathf.Max(y0, y1);
        for (int y = from; y <= to; y++) SetCorridorIfEmpty(x, y);
    }

    private void AddExtraLoops()
    {
        if (allLeaves.Count < 2) return;
        for (int i = 0; i < allLeaves.Count; i++)
        {
            if (Random.value > extraLoopChance) continue;
            Leaf a = allLeaves[i];
            Leaf b = allLeaves[Random.Range(0, allLeaves.Count)];
            if (a == b) continue;
            CarveCorridor(GetRoomCenter(a), GetRoomCenter(b));
        }
    }

    private void DecideEntranceAndBoss(out Vector2Int entrance, out Vector2Int boss)
    {
        // 入口 = 原点(0,0)に最も近い部屋の中心
        entrance = new Vector2Int(0, 0);
        boss = new Vector2Int(size - 1, size - 1);
        if (allLeaves.Count == 0) return;

        Leaf entLeaf = allLeaves[0];
        float bestDist = float.MaxValue;
        foreach (var leaf in allLeaves)
        {
            Vector2Int c = GetRoomCenter(leaf);
            float d = c.x + c.y; // 原点からのマンハッタン距離（近さ）
            if (d < bestDist) { bestDist = d; entLeaf = leaf; }
        }
        entrance = GetRoomCenter(entLeaf);

        // ボス = 入口から最も遠い部屋の中心
        Leaf bossLeaf = entLeaf;
        float far = -1f;
        foreach (var leaf in allLeaves)
        {
            Vector2Int c = GetRoomCenter(leaf);
            float d = Mathf.Abs(c.x - entrance.x) + Mathf.Abs(c.y - entrance.y);
            if (d > far) { far = d; bossLeaf = leaf; }
        }
        boss = GetRoomCenter(bossLeaf);
    }

    // ---- マップ書き込みユーティリティ ----
    private void SetCell(int x, int y, DungeonGridSystem.TileType type)
    {
        if (x < 0 || x >= size || y < 0 || y >= size) return;
        map[x, y] = type;
    }

    private void SetCorridorIfEmpty(int x, int y)
    {
        if (x < 0 || x >= size || y < 0 || y >= size) return;
        // 部屋は上書きしない（通路は壁だった所だけ掘る）
        if (map[x, y] == DungeonGridSystem.TileType.None)
            map[x, y] = DungeonGridSystem.TileType.Corridor;
    }
}
