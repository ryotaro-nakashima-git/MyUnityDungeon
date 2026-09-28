using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 🗺️ 迷宮の見た目を Tilemap で描く（Phase C の仕上げ）。
///
/// ## 何が変わったか
/// 以前は **1マス1GameObject＋手続き生成の石畳**で、**壁が存在しなかった**。
/// 床でない所は何も無く、カメラの青が透けて「タイルが宙に浮いている」絵になっていた。
/// ここでは
/// - **床**（部屋4種／通路は土）
/// - **壁**（`RuleTile` が18ルールで自動的に角・面・天を選ぶ＝オートタイルの宿題がこれで終わり）
/// - **デカール**（床の汚れ・ひび）と**小物**（壺・骨・蜘蛛の巣…）
/// - **壁の足元の影**
/// を4枚の Tilemap に分けて描く。
///
/// ## 既存の仕組みには触らない
/// 当たり判定も入力も `DungeonGridSystem` の**配列の計算**でやっている（Raycastではない）ので、
/// 見た目だけ差し替えれば済む。マスの GameObject（`RoomData` を持つ）は**残したまま**、
/// 床/通路の `SpriteRenderer` だけ切って、宝箱と罠はアトラスの絵に差し替える。
/// ⚠ 宝箱と罠を Tilemap にしないのは、`RoomData` がクールダウン中に**色を変える**ため
///    （Tilemap だと1マスだけ色を変えるのが面倒＝機能が消える）。
///
/// ⚠ 1マス＝1ワールドユニット、素材は16PPUなので**そのまま等倍**で合う。
/// 関連: [[DungeonTale]] [[DungeonGridSystem]]。
/// </summary>
[DisallowMultipleComponent]
public class DungeonTilemapView : MonoBehaviour
{
    public static DungeonTilemapView Instance { get; private set; }

    private Grid grid;
    private Tilemap floorMap, wallMap, decalMap, propMap;
    private Tile[] floorTiles, decalTiles, propTiles;   // 名前→Tile の使い回し（毎回作らない）

    /// <summary>壁を外側へ何マス伸ばすか。画面の縁まで岩で埋めて「地中にいる」感じを出す。</summary>
    private const int WallPad = 14;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public static DungeonTilemapView Ensure()
    {
        if (Instance != null) return Instance;
        var found = FindFirstObjectByType<DungeonTilemapView>();
        if (found != null) { Instance = found; return found; }
        var go = new GameObject("DungeonTilemapView");
        return go.AddComponent<DungeonTilemapView>();
    }

    private void Build()
    {
        if (grid != null) return;
        // ⚠⚠ **作り直す前に、既にあるものを拾う。**
        //   再生中に再コンパイルが走ると **フィールドだけ null に戻り、GameObject と
        //   コンポーネントはそのまま残る**（→ [[k0-era-length]] の「静的が飛ぶ」と同じ形）。
        //   `Grid` は1つしか付けられないので `AddComponent` が **null を返し**、
        //   次の行で NullReference になっていた（遠征の盤を建てた瞬間に落ちた）。
        grid = GetComponent<Grid>();
        if (grid == null) grid = gameObject.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);
        // ⚠ Tilemap のセル (0,0) は「左下が原点の1x1」。GridToWorld はマスの**中心**を返すので半マスずらす。
        transform.position = new Vector3(-0.5f, -0.5f, 0f);

        floorMap = NewLayer("Floor", -40);
        decalMap = NewLayer("Decal", -35);
        wallMap = NewLayer("Wall", -30);
        propMap = NewLayer("Prop", -25);

        // 🎨 素材の色をこのゲームの世界観へ寄せる（→ [[DungeonTale]] の Tint）
        floorMap.color = DungeonTale.FloorTint;
        wallMap.color = DungeonTale.WallTint;
        decalMap.color = new Color(1f, 1f, 1f, 0.55f);
        propMap.color = DungeonTale.PropTint;
    }

    private Color? themeTint;

    /// <summary>🏔️ 空間テーマ（洞窟/遺跡/城砦/溶岩/氷雪）の色調。次に描くときから効く。</summary>
    public void SetTheme(Color tint)
    {
        themeTint = tint;
        Build();
        floorMap.color = Mul(DungeonTale.FloorTint, tint);
        propMap.color = Mul(DungeonTale.PropTint, tint);
        // ⚠ 壁は RuleTile が色をロックするので、ここでは決められない（Paint のマスごとに入れる）
    }

    private static Color Mul(Color a, Color b)
        => new Color(Mathf.Clamp01(a.r * b.r * 1.25f), Mathf.Clamp01(a.g * b.g * 1.25f), Mathf.Clamp01(a.b * b.b * 1.25f), 1f);

    private Tilemap NewLayer(string name, int order)
    {
        // ⚠ 同じ理由で、層も**残っていたら拾う**。作り直すと同名の層が二重に積まれ、
        //   古い層が消えずに残って前の階の絵が透けて見える。
        var old = transform.Find(name);
        if (old != null)
        {
            var tmOld = old.GetComponent<Tilemap>();
            if (tmOld != null) return tmOld;
        }
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var tm = go.AddComponent<Tilemap>();
        var r = go.AddComponent<TilemapRenderer>();
        r.sortingOrder = order;
        r.sortingLayerName = "Default";
        return tm;
    }

    private static Tile MakeTile(Sprite s)
    {
        if (s == null) return null;
        var t = ScriptableObject.CreateInstance<Tile>();
        t.sprite = s;
        t.colliderType = Tile.ColliderType.None;
        return t;
    }

    private Tile[] Bake(string[] names)
    {
        var arr = new Tile[names.Length];
        for (int i = 0; i < names.Length; i++) arr[i] = MakeTile(DungeonTale.S(names[i]));
        return arr;
    }

    /// <summary>
    /// 盤ができた/組み直したときに呼ぶ。`types` は playable 範囲のマスの種類。
    ///
    /// ⚠⚠ **階ごとに描く場所をずらす**（縦の迷宮 F-2以降）。
    ///   このビューは**タイルマップを1枚しか持たない**。旧仕様は盤も1枚だったので、
    ///   毎回 `ClearAllTiles()` してセル(0,0)から描き直せばよかった。
    ///   階層ぶんの盤ができた今それをやると、**最後に描いた階の形が全部の階に見える**
    ///   （しかも原点に描かれるので、他の階へ行くと何も無い）。ユーザー報告で発覚。
    ///   → `DungeonGridSystem.FloorOrigin` と同じだけセルをずらし、**その階の帯だけ**を消す。
    /// </summary>
    public void Paint(DungeonGridSystem.TileType[,] types, int size, int floorIndex)
    {
        if (!DungeonTale.Available) return;
        Build();
        // 🏢 この階の帯の原点（セル座標）。⚠ 盤の world オフセットと必ず同じ値にすること。
        int oy = Mathf.RoundToInt(Mathf.Max(0, floorIndex) * DungeonGridSystem.FloorSpacing);

        if (floorTiles == null)
        {
            floorTiles = Bake(DungeonTale.FloorRoom);
            decalTiles = Bake(DungeonTale.Bloods);
            propTiles = Bake(DungeonTale.Props);
        }
        var corridorTile = MakeTile(DungeonTale.S(DungeonTale.FloorCorridor));
        var wallRule = DungeonTale.WallRule;
        var wallColor = themeTint.HasValue ? Mul(DungeonTale.WallTint, themeTint.Value) : DungeonTale.WallTint;

        // ⚠ **この階の帯だけ**を消す（全消しすると他の階の絵まで巻き添えになる）。
        //   拡張で size が増えるので、消す範囲は常に最大(50)＋壁の余白で取る。
        ClearBand(oy);

        int w = types.GetLength(0), h = types.GetLength(1);
        System.Func<int, int, bool> isFloor = (x, y) =>
            x >= 0 && y >= 0 && x < size && y < size && x < w && y < h
            && types[x, y] != DungeonGridSystem.TileType.None;

        // ---- 床 ----
        for (int x = 0; x < size && x < w; x++)
            for (int y = 0; y < size && y < h; y++)
            {
                if (!isFloor(x, y)) continue;
                var p = new Vector3Int(x, y + oy, 0);   // 🏢 この階の帯へ
                bool corridor = types[x, y] == DungeonGridSystem.TileType.Corridor;
                floorMap.SetTile(p, corridor ? corridorTile : floorTiles[DungeonTale.Hash(x, y, 11 + floorIndex) % floorTiles.Length]);

                // 🩸 血の跡。⚠ 22%で撒いたら**赤い記号だらけ**になったので5%まで落とした
                int hh = DungeonTale.Hash(x, y, 23 + floorIndex);
                if (hh % 100 < 5 && decalTiles.Length > 0) decalMap.SetTile(p, decalTiles[hh % decalTiles.Length]);   // p は帯つき
            }

        // ---- 壁（RuleTile が形を選ぶ）----
        //  ⚠ **RuleTile は色をロックしている**ので `tilemap.color` が効かない（無視される）。
        //     マスごとに `SetTileFlags(None)` してから `SetColor` を入れないと、素材の砂色のまま。
        //     これに気づかず「色が効かない」を何度も追いかけた。
        if (wallRule != null)
            for (int x = -WallPad; x < size + WallPad; x++)
                for (int y = -WallPad; y < size + WallPad; y++)
                {
                    if (isFloor(x, y)) continue;
                    var wp = new Vector3Int(x, y + oy, 0);   // 🏢 この階の帯へ
                    wallMap.SetTile(wp, wallRule);
                    wallMap.SetTileFlags(wp, TileFlags.None);
                    wallMap.SetColor(wp, wallColor);
                }

        // ---- 小物（部屋の隅にだけ。通路と入口/ボス周りは避ける）----
        for (int x = 0; x < size && x < w; x++)
            for (int y = 0; y < size && y < h; y++)
            {
                if (!isFloor(x, y)) continue;
                if (types[x, y] != DungeonGridSystem.TileType.Room) continue;
                int hh = DungeonTale.Hash(x, y, 41 + floorIndex);
                if (hh % 100 >= 12) continue;                                      // 12%だけ
                bool nearWall = !isFloor(x + 1, y) || !isFloor(x - 1, y) || !isFloor(x, y - 1);
                if (!nearWall) continue;                                           // 部屋の**縁**にだけ置く（通行の邪魔にしない）
                propMap.SetTile(new Vector3Int(x, y + oy, 0), propTiles[hh % propTiles.Length]);
            }
    }

    /// <summary>
    /// 🏢 その階の帯を消す。⚠ 拡張(10→50)で描く範囲が広がるので、消すのは**常に最大寸法**で行う。
    /// ここを「今の size ぶん」にすると、20×20 から 10×10 に戻したとき外周が消え残る。
    /// </summary>
    private void ClearBand(int oy)
    {
        const int Max = 50;
        var bounds = new BoundsInt(-WallPad, oy - WallPad, 0,
                                   Max + WallPad * 2, Max + WallPad * 2, 1);
        int n = bounds.size.x * bounds.size.y;
        var empty = new TileBase[n];
        floorMap.SetTilesBlock(bounds, empty);
        wallMap.SetTilesBlock(bounds, empty);
        decalMap.SetTilesBlock(bounds, empty);
        propMap.SetTilesBlock(bounds, empty);
    }

    public void Clear()
    {
        if (grid == null) return;
        floorMap.ClearAllTiles(); wallMap.ClearAllTiles();
        decalMap.ClearAllTiles(); propMap.ClearAllTiles();
    }
}
