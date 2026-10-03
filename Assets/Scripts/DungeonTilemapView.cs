using System.Collections.Generic;
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
    private Tilemap floorMap, wallMap, decalMap, propMap, decorMap;
    private Tile[] floorTiles, decalTiles, propTiles;   // 名前→Tile の使い回し（毎回作らない）
    private Tile[] flatDecor, objDecor; private Tile cobwebTile;   // 🪨 段D：PixelLab の小物（Resources/DungeonTale/Extra）

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
        decorMap = NewLayer("Decor", -34);   // 🪨 床に貼る小物（ひび・水たまり・苔・床の陣）
        decorMap.color = new Color(1f, 1f, 1f, 0.6f);   // 床に馴染ませる（くっきり出すと床の上の物に見える）
        wallMap = NewLayer("Wall", -30);
        propMap = NewLayer("Prop", -25);

        // 🎨 素材の色をこのゲームの世界観へ寄せる（→ [[DungeonTale]] の Tint）
        // ⚠ 段D：床は**マスごとに**色を入れる（揺らぎ・壁ぎわの影）ので、層の色は白にしておく（掛け算が二重になる）
        floorMap.color = Color.white;
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
        // ⚠ 床はマスごとに色を入れる（Paint）。ここでは小物の層だけ
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
            flatDecor = BakeExtra(DungeonTale.ExtraFlat);
            objDecor = BakeExtra(DungeonTale.ExtraObjects);
            cobwebTile = MakeTile(DungeonTale.S(DungeonTale.Cobweb));
        }
        var corridorTile = MakeTile(DungeonTale.S(DungeonTale.FloorCorridor));
        var wallRule = DungeonTale.WallRule;

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
                // 🎨 段D：床の揺らぎ（±10%）と壁ぎわの影。⚠ 前は全マス同じ色の板で、平らに見えていた
                floorMap.SetTileFlags(p, TileFlags.None);
                floorMap.SetColor(p, FloorColor(x, y, corridor, !isFloor(x, y + 1)));

                // 🩸 血の跡。⚠ 22%で撒いたら**赤い記号だらけ**になったので5%まで落とした
                int hh = DungeonTale.Hash(x, y, 23 + floorIndex);
                if (hh % 100 < 5 && decalTiles.Length > 0) decalMap.SetTile(p, decalTiles[hh % decalTiles.Length]);   // p は帯つき
            }

        // ---- 壁（RuleTile が形を選ぶ）----
        //  ⚠ **RuleTile は色をロックしている**ので `tilemap.color` が効かない（無視される）。
        //     マスごとに `SetTileFlags(None)` してから `SetColor` を入れないと、素材の砂色のまま。
        //     これに気づかず「色が効かない」を何度も追いかけた。
        // 🎨 段D：壁に奥行き。**床からの距離**で明るさを4段に分け、部屋の上の壁（正面のレンガ）は明るくする。
        //   ⚠ 前は全部の壁が同じ暗い色で、一面ほぼ黒＝通路との境しか読めなかった。
        int[,] dist = WallDistance(isFloor, size);
        if (wallRule != null)
            for (int x = -WallPad; x < size + WallPad; x++)
                for (int y = -WallPad; y < size + WallPad; y++)
                {
                    if (isFloor(x, y)) continue;
                    var wp = new Vector3Int(x, y + oy, 0);   // 🏢 この階の帯へ
                    wallMap.SetTile(wp, wallRule);
                    wallMap.SetTileFlags(wp, TileFlags.None);
                    wallMap.SetColor(wp, WallColor(dist[x + WallPad, y + WallPad], isFloor(x, y - 1)));
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
                // 🪨 段D：半分は新しい小物（結晶・骨・瓦礫・きのこ…）。隅には蜘蛛の巣
                bool corner = !isFloor(x, y + 1) && (!isFloor(x - 1, y) || !isFloor(x + 1, y));
                Tile pick = propTiles[hh % propTiles.Length];
                if (corner && cobwebTile != null && (hh >> 8) % 3 == 0) pick = cobwebTile;
                else if (objDecor != null && objDecor.Length > 0 && (hh >> 4) % 2 == 0) pick = objDecor[(hh >> 6) % objDecor.Length];
                propMap.SetTile(new Vector3Int(x, y + oy, 0), pick);
            }

        // 🪨 段D：床に貼る小物（ひび・水たまり・苔・床の陣）。部屋にも通路にも、ごく少なく
        if (flatDecor != null && flatDecor.Length > 0)
            for (int x = 0; x < size && x < w; x++)
                for (int y = 0; y < size && y < h; y++)
                {
                    if (!isFloor(x, y)) continue;
                    int hh = DungeonTale.Hash(x, y, 59 + floorIndex);
                    if (hh % 100 >= 5) continue;
                    var dp = new Vector3Int(x, y + oy, 0);
                    decorMap.SetTile(dp, flatDecor[(hh >> 7) % flatDecor.Length]);
                    // ⚠ 1マスいっぱいだと「床の上に置いた物」に見えるので、小さくずらして床の模様に寄せる
                    float sc = 0.6f + ((hh >> 11) % 20) / 100f;
                    var off = new Vector3(((hh >> 13) % 30 - 15) / 100f, ((hh >> 17) % 30 - 15) / 100f, 0f);
                    decorMap.SetTransformMatrix(dp, Matrix4x4.TRS(off, Quaternion.identity, new Vector3(sc, sc, 1f)));
                }

        // 🔥 段D：松明と光だまり
        PlaceTorches(types, isFloor, size, floorIndex, oy);
    }

    // ================= 段D：色 =================
    private Color Tinted(Color c) => themeTint.HasValue ? Mul(c, themeTint.Value) : c;

    /// <summary>床の色。部屋（石）と通路（土）で分け、マスごとに ±10% 揺らす。上が壁なら影を落とす。</summary>
    private Color FloorColor(int x, int y, bool corridor, bool wallAbove)
    {
        float j = 0.90f + (DungeonTale.Hash(x, y, 5) % 100) / 100f * 0.20f;
        if (wallAbove) j *= 0.78f;
        var c = corridor ? new Color(0.62f, 0.50f, 0.44f) : new Color(0.60f, 0.54f, 0.74f);
        c = Tinted(c);
        return new Color(c.r * j, c.g * j, c.b * j, 1f);
    }

    /// <summary>壁の色。床からの距離で4段（近いほど明るい）。床のすぐ上＝正面のレンガはいちばん明るい。</summary>
    private Color WallColor(int d, bool face)
    {
        Color c = face ? new Color(0.66f, 0.54f, 0.74f)
                : d <= 1 ? new Color(0.46f, 0.39f, 0.60f)
                : d == 2 ? new Color(0.32f, 0.27f, 0.44f)
                : d == 3 ? new Color(0.22f, 0.19f, 0.32f)
                : new Color(0.15f, 0.13f, 0.22f);
        return Tinted(c);
    }

    /// <summary>壁のマスから、いちばん近い床までの距離（8方向・4で打ち切り）。配列は [x+WallPad, y+WallPad]。</summary>
    private static int[,] WallDistance(System.Func<int, int, bool> isFloor, int size)
    {
        int n = size + WallPad * 2;
        var d = new int[n, n];
        var q = new Queue<Vector2Int>();
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                if (isFloor(i - WallPad, j - WallPad)) { d[i, j] = 0; q.Enqueue(new Vector2Int(i, j)); }
                else d[i, j] = 99;
            }
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            if (d[c.x, c.y] >= 4) continue;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = c.x + dx, ny = c.y + dy;
                    if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                    if (d[nx, ny] <= d[c.x, c.y] + 1) continue;
                    d[nx, ny] = d[c.x, c.y] + 1;
                    q.Enqueue(new Vector2Int(nx, ny));
                }
        }
        return d;
    }

    // ================= 段D：松明 =================
    private class Torch { public SpriteRenderer flame, glow; public float phase; public Color glowCol; }
    private readonly Dictionary<int, GameObject> torchRoots = new Dictionary<int, GameObject>();
    private readonly List<Torch> torches = new List<Torch>();
    private Sprite[] torchFrames;
    private Sprite glowSprite;
    private Material unlitMat;
    private const float GlowAlpha = 0.17f;

    /// <summary>
    /// 部屋の上の壁（正面のレンガ）に、2マスおきに松明を掛け、暖かい光の輪を重ねる。
    /// ⚠ URP の 2D の灯り（Light2D）はこのプロジェクトのカメラでは効かなかった（試した）。
    ///   描き方の設定は他にも響くので触らず、**光の輪を重ねる**だけにしてある（Sprite-Unlit・薄いα）。
    /// ⚠ 光の色は空間テーマで変える（溶岩＝赤・氷雪＝青白）。
    /// </summary>
    private void PlaceTorches(DungeonGridSystem.TileType[,] types, System.Func<int, int, bool> isFloor, int size, int floorIndex, int oy)
    {
        GameObject root;
        if (torchRoots.TryGetValue(floorIndex, out root) && root != null)
        {
            torches.RemoveAll(t => t == null || t.flame == null || t.flame.transform.parent == root.transform);
            Destroy(root);
        }
        if (torchFrames == null) torchFrames = Resources.LoadAll<Sprite>("DungeonTale/Torch");
        if (torchFrames == null || torchFrames.Length == 0) return;
        if (glowSprite == null) glowSprite = MakeGlow();
        if (unlitMat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh != null) unlitMat = new Material(sh);
        }
        root = new GameObject("Torches_" + floorIndex);
        root.transform.SetParent(transform, false);
        torchRoots[floorIndex] = root;
        var light = LightColor();
        int w = types.GetLength(0), h = types.GetLength(1);
        int lastX = -9, lastY = -9;
        for (int y = 0; y <= size; y++)
            for (int x = 0; x < size && x < w; x++)
            {
                if (isFloor(x, y) || !isFloor(x, y - 1)) continue;               // 床のすぐ上の壁だけ
                var below = types[x, y - 1];
                if (below == DungeonGridSystem.TileType.Corridor) continue;       // 通路の壁には掛けない（狭い所が眩しくなる）
                // 部屋の上の壁に**3マスおき**（乱数だと部屋によって1本も無かった）
                if ((x + y * 7 + floorIndex) % 3 != 0) continue;
                if (y == lastY && x - lastX < 2) continue;                        // 隣どうしに並べない
                lastX = x; lastY = y;
                var cell = new Vector3Int(x, y + oy, 0);
                var pos = wallMap.CellToWorld(cell) + new Vector3(0.5f, 0.45f, 0f);
                var t = new GameObject("Torch"); t.transform.SetParent(root.transform, false); t.transform.position = pos;
                var fr = t.AddComponent<SpriteRenderer>();
                fr.sprite = torchFrames[0]; fr.sortingOrder = -24;
                if (unlitMat != null) fr.sharedMaterial = unlitMat;
                var g = new GameObject("Glow"); g.transform.SetParent(t.transform, false);
                g.transform.localPosition = new Vector3(0f, -0.6f, 0f);
                g.transform.localScale = Vector3.one * 0.72f;
                var gr = g.AddComponent<SpriteRenderer>();
                gr.sprite = glowSprite; gr.sortingOrder = -27;
                if (unlitMat != null) gr.sharedMaterial = unlitMat;
                gr.color = new Color(light.r, light.g, light.b, GlowAlpha);
                torches.Add(new Torch { flame = fr, glow = gr, phase = (DungeonTale.Hash(x, y, 3) % 100) / 10f, glowCol = light });
            }
    }

    /// <summary>空間テーマから灯りの色を決める（ふだん＝橙／溶岩＝赤／氷雪・城砦＝青白／遺跡＝黄緑がかった灯）。</summary>
    private Color LightColor()
    {
        var warm = new Color(1f, 0.66f, 0.36f);
        if (!themeTint.HasValue) return warm;
        var t = themeTint.Value;
        if (t.b - t.r > 0.15f) return new Color(0.62f, 0.82f, 1f);     // 氷雪
        if (t.r - t.b > 0.3f) return new Color(1f, 0.42f, 0.22f);      // 溶岩
        if (t.g - t.b > 0.1f) return new Color(0.92f, 0.88f, 0.50f);   // 遺跡
        return warm;
    }

    private static Sprite MakeGlow()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x - 63.5f) / 63.5f, dy = (y - 63.5f) / 63.5f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)); a *= a;
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N / 7f);   // 直径7マス
    }

    /// <summary>炎を8コマで揺らし、光を少し明滅させる。⚠ 時間は unscaled（一時停止中も灯は揺れる）。</summary>
    private void Update()
    {
        if (torches.Count == 0) return;
        float now = Time.unscaledTime;
        for (int i = 0; i < torches.Count; i++)
        {
            var t = torches[i];
            if (t == null || t.flame == null) continue;
            float k = now * 9f + t.phase;
            t.flame.sprite = torchFrames[((int)k) % torchFrames.Length];
            float a = GlowAlpha * (0.86f + 0.14f * Mathf.Sin(now * 7.3f + t.phase * 2f));
            t.glow.color = new Color(t.glowCol.r, t.glowCol.g, t.glowCol.b, a);
        }
    }

    private static Tile[] BakeExtra(string[] names)
    {
        var list = new List<Tile>();
        foreach (var n in names)
        {
            var sp = Resources.Load<Sprite>("DungeonTale/Extra/" + n);
            var t = MakeTile(sp);
            if (t != null) list.Add(t);
        }
        return list.ToArray();
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
        decorMap.SetTilesBlock(bounds, empty);
        propMap.SetTilesBlock(bounds, empty);
    }

    public void Clear()
    {
        if (grid == null) return;
        floorMap.ClearAllTiles(); wallMap.ClearAllTiles();
        decalMap.ClearAllTiles(); propMap.ClearAllTiles(); decorMap.ClearAllTiles();
        foreach (var kv in torchRoots) if (kv.Value != null) Destroy(kv.Value);
        torchRoots.Clear(); torches.Clear();
    }
}
