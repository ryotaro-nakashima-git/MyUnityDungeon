using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 複数フロア（階層）の生成・保持・切替を司る。
/// アクティブなフロアだけをグリッドに構築し、切替時に配置要素を退避/復元する。
/// 魔王は最下層(B{N}F)のみに実在（それ以外のフロアでは不在化）。
/// </summary>
public class DungeonFloorManager : MonoBehaviour
{
    public static DungeonFloorManager Instance { get; private set; }

    [Header("Floors")]
    [Tooltip("生成する階層数（1〜3）")]
    [SerializeField] private int floorCount = 2;

    // ⚠ `readonly` を付けない。[[SaveSystem]] は **readonly を「カタログ＝保存しない」の目印**に使っているので、
    //    readonly のままだと**迷宮そのものがセーブに乗らない**（実際にそれで復元後 0層になった）。
    private List<FloorData> floors = new List<FloorData>();
    private int current = 0;

    private DungeonGenerator gen;
    private DungeonGridSystem grid;
    private DungeonFeatureManager fm;
    private DungeonAdventurerSpawner spawner;
    private GameUIManager ui;
    private GameObject stairsMarker; // ▼ 下り階段マーカー（非最下層のボスセルに表示）

    // ===== descent（階層踏破）状態 =====
    private bool battleActive = false;
    private int deepestReached = -1; // このウェーブで冒険者が到達した最深フロア（-1=侵略していない）
    private int lastDeepestReached = -1;
    /// <summary>🔁 直近のウェーブで冒険者が到達した最深フロア（-1＝まだ侵略が無い）。実戦の反芻の判定に使う。</summary>
    public int LastDeepestReached => lastDeepestReached;
    public bool BattleActive => battleActive;

    // ===== 🕳️ 奈落に落ちた者（→ [[DungeonFeatureManager]] の落とし穴）=====
    //
    // 🕳️ 奈落の控え。「1人だけ下の階へ移す」を、実体を**眠らせて控えに置く**形で表す。
    // ⚠ F-2以降、階層は**同時に存在する**（この控えは「まだ降下が起きていないので、
    //   下の階のどこに着地するかが決まっていない」から眠らせているのであって、
    //   盤が無いからではない）。落ちた階は `fallenFrom` が持つ。
    //   ・降下が起きたら、下の階の**穴の真下**で目を覚ます（＝入口の守りを飛ばして着地する）
    //   ・降下が起きないまま波が終われば、**這い上がって逃げる**（名声＋略奪装備を持ち帰る）
    //   ＝「落とすこと」は「倒すこと」ではない。落とし穴が万能の削除ボタンにならないようにする線。
    // ⚠ readonly＝セーブに乗せない。戦闘中だけの状態で、保存は準備フェーズにしか起きないので正しい。
    private readonly List<AdventurerAI> fallen = new List<AdventurerAI>();
    private readonly List<Vector2Int> fallenCells = new List<Vector2Int>();
    // 🏢 **どの階から落ちたか**（降下が階ごとに独立したので必須）。
    // ⚠ これが無いと、B2Fの穴に落ちた者が「B1F→B2Fの降下」で目を覚ましてしまう
    //   （落ちた先はB3Fのはずなのに1つ浅い階に湧く）。
    private readonly List<int> fallenFrom = new List<int>();
    public int FallenCount => fallen.Count;

    /// <summary>🕳️ 奈落へ落ちた。この階からは退場し、下の階で目を覚ます（か、這い上がって逃げる）。</summary>
    public void SendBelow(AdventurerAI a, Vector2Int cell)
    {
        if (a == null) return;
        fallen.Add(a); fallenCells.Add(cell); fallenFrom.Add(a.MyFloor);
        a.NoteAbyss();                 // 🗡️ 這い上がって逃げたら必ず名がつく（→ [[Nemesis]]）
        a.gameObject.SetActive(false);
        Debug.Log($"🕳️『奈落』{cell} の穴から1体が下の階へ落ちた（控え {fallen.Count} 体）");
        NotifySystem.Push("落とし穴が1体を<b>下の階</b>へ落とした。降りるまで戻ってこない", NotifySystem.Kind.Story);
    }

    /// <summary>
    /// 🕳️ 穴の真下に着地させる。各階は別々に生成されるので**真下が壁のことの方が多い**。
    /// そのときは**いちばん近い床**へ寄せる（入口に戻すと「下に落ちた」意味が消えるため）。
    /// どこも駄目なら入口。
    /// </summary>
    private Vector2Int NearestFloorCell(DungeonGridSystem grid, Vector2Int want, Vector2Int fallback)
    {
        if (grid == null) return fallback;
        int size = grid.CurrentPlayableSize;
        for (int r = 0; r <= size; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;   // その半径の輪だけ見る
                    int x = want.x + dx, y = want.y + dy;
                    if (x < 0 || y < 0 || x >= size || y >= size) continue;
                    if (grid.GetTileType(x, y) != DungeonGridSystem.TileType.None) return new Vector2Int(x, y);
                }
        return fallback;
    }

    /// <summary>波が終わった時点でまだ下に居る者＝這い上がって逃げた扱い。</summary>
    private void ReleaseFallenAsEscaped()
    {
        int n = 0;
        for (int i = 0; i < fallen.Count; i++)
        {
            var a = fallen[i]; if (a == null) continue;
            a.gameObject.SetActive(true);
            a.ForceDespawnWithReward();   // ＝逃がした扱い（名声↑・略奪装備の持ち逃げ）
            n++;
        }
        fallen.Clear(); fallenCells.Clear(); fallenFrom.Clear();
        if (n > 0)
        {
            Debug.Log($"🕳️『這い上がり』下に落としたまま波が終わり、{n} 体が穴から出て逃げた（倒したことにはならない）");
            NotifySystem.Push($"穴に落とした <b>{n} 体</b>が這い上がって逃げた。落とすことは倒すことではない", NotifySystem.Kind.Loss);
        }
    }

    public int PlannedFloorCount => Mathf.Clamp(floorCount, 1, 3);
    public int BuiltFloorCount => floors.Count;
    public int CurrentFloorIndex => current;
    public bool IsDeepest(int i) => i == floors.Count - 1;
    public FloorData CurrentFloor => (floors.Count > 0 && current < floors.Count) ? floors[current] : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Refs()
    {
        if (gen == null) gen = Object.FindFirstObjectByType<DungeonGenerator>();
        if (grid == null) grid = DungeonGridSystem.Active;
        if (fm == null) fm = Object.FindFirstObjectByType<DungeonFeatureManager>();
        if (ui == null) ui = Object.FindFirstObjectByType<GameUIManager>();
    }

    public void SetFloorCount(int n) { floorCount = Mathf.Clamp(n, 1, 3); }

    /// <summary>全階層を生成し、最上階(B1F)を表示する。生成のたびに要素はリセット。</summary>
    public void GenerateAllFloors()
    {
        Refs();
        if (gen == null || grid == null) { Debug.LogError("DungeonFloorManager: 参照が見つかりません。"); return; }

        floors.Clear();
        int n = PlannedFloorCount;
        for (int i = 0; i < n; i++)
        {
            var fd = gen.BuildFloorData(10); // 🗺️ 生成時は各階10×10から。拡張は領域研究で階層ごとに
            fd.isDeepest = (i == n - 1); // 最下層のみ魔王
            floors.Add(fd);
        }
        EnsureBoards(true);        // 🏢 新規生成なので全階を組む（F-2）
        current = 0;
        ActivateFloor(0);
        Debug.Log($"🏢『階層生成』{floors.Count}層を生成（最下層 B{floors.Count}F に魔王）／盤 {DungeonGridSystem.Boards.Count} 枚");
    }

    /// <summary>
    /// 🏢 **階層ぶんの盤を実体として用意する**（F-2の中心）。
    ///
    /// ⚠⚠ 旧仕様は盤が1枚で、`ActivateFloor` が**盤ごと作り直して**いた。
    ///   つまり「表示していない階は存在しない」＝上の階の守りも冒険者も居なかった。
    ///   縦の迷宮では全階が同時に生きるので、盤も階層ぶん実体で持つ。
    ///
    /// ⚠ 2枚目以降は**シーンにある B1F の盤を複製**して作る。プレハブ参照（タイル・ガイド）を
    ///   インスペクタから引き継ぐ必要があるため、`new GameObject` では作れない。
    /// </summary>
    /// <param name="buildAll">
    /// true＝全階の地形を組み直す（新規生成・ロード）。
    /// ⚠⚠ false のときは**新しく作った盤だけ**を組む。
    ///   `BuildFromMap` はその階の配置を消すので、`buildAll:true` で呼ぶと
    ///   **階層を1つ足しただけで既存の階の配置が全部消える**（実プレイで踏んだ）。
    /// </param>
    private void EnsureBoards(bool buildAll)
    {
        var b1 = DungeonGridSystem.Of(0);
        if (b1 == null) b1 = DungeonGridSystem.Active;
        if (b1 == null) { Debug.LogError("🏢 B1Fの盤が見つからない（シーンの GridManager）"); return; }
        b1.SetFloorIndex(0);

        var created = new List<int>();
        for (int i = 1; i < floors.Count; i++)
        {
            if (DungeonGridSystem.Of(i) != null) continue;
            var clone = Instantiate(b1.gameObject, b1.transform.parent);
            clone.name = "GridManager_B" + (i + 1) + "F";
            var g = clone.GetComponent<DungeonGridSystem>();
            g.SetFloorIndex(i);
            // ⚠ 複製元が持っていたタイル/ガイドの実体は B1F の座標に生えている。
            //   `BuildFromMap` の前に消しておかないと、B1Fの絵がこの階に重なって残る。
            g.ClearAllTilesAndGuides();
            created.Add(i);
            Debug.Log($"🏢『盤を増設』B{i + 1}F の盤を作成（原点 y={g.FloorOrigin.y}）");
        }

        if (buildAll) { for (int i = 0; i < floors.Count; i++) BuildBoard(i); }
        else { foreach (int i in created) BuildBoard(i); }
    }

    /// <summary>その階の盤に地形を組む（`ActivateFloor` から切り離した＝表示とは無関係）。</summary>
    private void BuildBoard(int i)
    {
        var g = DungeonGridSystem.Of(i);
        if (g == null || i < 0 || i >= floors.Count) return;
        var fd = floors[i];
        g.SetPlayableSize(fd.size);
        g.BuildFromMap(fd.map, fd.entrance, fd.boss, fd.tint, IsLordFloor(i));
    }

    /// <summary>
    /// 表示フロアを切り替える。
    /// ⚠⚠ **F-2以降は「見る階を変える」だけ**。盤も配置も全階ぶん実体で存在しているので、
    ///   退避も復元も要らない（旧仕様はここで Export/Import していた）。
    ///   ⚠ 戦闘中の切替も許す ―― 縦の迷宮では他の階でも戦いが続いているため。
    /// </summary>
    public void SwitchTo(int i)
    {
        Refs();
        if (i < 0 || i >= floors.Count || i == current) return;
        current = i;
        if (ui != null) ui.PlayFloorTransition(); // 切替の暗転フェード
        ActivateFloor(i);
    }

    /// <summary>
    /// その階を「見ている階」にする。⚠ **盤は作り直さない**（F-2）。
    /// やるのは `Active` の付け替えとカメラ移動だけ。
    /// </summary>
    private void ActivateFloor(int i)
    {
        Refs();
        var g = DungeonGridSystem.Of(i);
        if (g == null) { Debug.LogWarning($"🏢 B{i + 1}F の盤が無い"); return; }
        // 🏢 **未完了の設置は階をまたがせない**（縦の迷宮）。
        // ⚠ 落とし穴の行き先待ち／掘りかけは「そのマスで続きをする」前提の状態なので、
        //   別の階へ移った時点で意味を失う。畳まないと**別の階の同じ座標**を触りに行く。
        if (fm != null && fm.HasPendingPitAnywhere) fm.CancelPendingPit();
        if (Excavation.HasPendingDigAnywhere) Excavation.CancelPendingDig();
        DungeonGridSystem.SetActive(g);
        grid = g;
        RefreshLordPresence();
        var cam = Object.FindFirstObjectByType<CameraController>();
        if (cam != null) cam.FitToDungeon();
        UpdateStairsMarker();
        Debug.Log($"🔽『フロア表示』B{i + 1}F へ（{(IsLordFloor(i) ? "魔王在陣" : "通常")}／原点 y={g.FloorOrigin.y}）");
    }

    /// <summary>
    /// 👁️ いま操作している階の眺めに戻す（遠征先を覗いたあとに使う → [[RaidBoard]]）。
    /// ⚠ `SwitchTo` は「同じ階なら何もしない」ので、覗いたあとの復帰には使えない。
    /// </summary>
    public void ReturnView() { ActivateFloor(current); }

    /// <summary>
    /// ⛏️ いま盤に出ている地形を `FloorData.map` に写し戻す（→ [[Excavation]]）。
    /// ⚠⚠ **これを呼ばないと工事が消える。** `ActivateFloor` は `fd.map` から盤を作り直すので、
    ///   盤だけ書き換えても階を切り替えた瞬間に元の形に戻る。
    /// </summary>
    public void WriteBackCurrentMap() { WriteBackMap(current); }

    /// <param name="i">🏢 書き戻す階。⚠ 表示していない階を触ることがある（異変など）ので指定できるようにした。</param>
    public void WriteBackMap(int i)
    {
        Refs();
        if (i < 0 || i >= floors.Count) return;
        var fd = floors[i];
        var g = DungeonGridSystem.Of(i);
        if (fd == null || g == null || fd.map == null) return;
        int size = Mathf.Min(fd.map.GetLength(0), g.CurrentPlayableSize);
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                fd.map[x, y] = g.GetTileType(x, y);
    }

    public string FloorLabel(int i) => "B" + (i + 1) + "F";

    /// <summary>👑 その階に魔王が立つか（鎮座＝最下層／親征＝選んだ階）。盤・タブ・階段の表示はここを見る。</summary>
    public bool IsLordFloor(int i) => i == LordStance.LordFloorIndex(Mathf.Max(1, floors.Count));

    /// <summary>
    /// 👑 魔王を**自分の階**（鎮座＝最下層／親征＝選んだ階）へ置き直す。
    ///
    /// ⚠⚠ **表示中の階とは無関係**（縦の迷宮 F-2以降）。旧仕様は盤が1枚だったので
    ///   「最下層を見ていないなら魔王は不在」で正しかったが、全階が同時に生きるいまは
    ///   それだと①別の階を見ている間 魔王がどこにも居ない ②降下の判定が全階で止まる
    ///   ③別の階を見ている間 魔王が無敵、になる（ユーザー報告）。
    ///
    /// ⚠ **`PlaceAt` を使わないこと。** あれはHPを満タンに戻すので、
    ///   階を切り替えるたびに魔王が全回復する（F-2で実際にそうなっていた）。
    /// </summary>
    public void RefreshLordPresence()
    {
        Refs();
        var dl = DemonLord.Instance;
        if (dl == null || floors.Count == 0) return;
        int lf = Mathf.Clamp(LordStance.LordFloorIndex(Mathf.Max(1, floors.Count)), 0, floors.Count - 1);
        var g = DungeonGridSystem.Of(lf);
        if (g == null) return;
        dl.MoveTo(g.DemonLordCell, lf);   // ⚠ HPは維持
        UpdateStairsMarker();
    }

    // ============ 💾 セーブ / ロード（[[SaveSystem]]） ============
    /// <summary>表示中フロアの配置は FeatureManager 側に居るので、保存前に FloorData へ書き戻す。</summary>
    public void SyncCurrentFloorFeatures()
    {
        Refs();
        if (fm == null) return;
        // 🏢 **全階ぶん書き戻す**（F-2）。旧仕様は表示中の階だけで足りた（他の階は記録側にしか無かった）が、
        //   いまは全階が実体なので、表示中だけ書き戻すと**他の階の配置がセーブから漏れる**。
        for (int i = 0; i < floors.Count; i++) floors[i].features = fm.ExportFeatures(i);
    }

    /// <summary>ロード直後。復元された floors から迷宮を組み直す（地形・配置・魔王の実体）。</summary>
    public void RebuildAfterLoad()
    {
        Refs();
        if (floors == null || floors.Count == 0) { Debug.LogWarning("💾 復元した階層が空だった"); return; }
        current = Mathf.Clamp(current, 0, floors.Count - 1);
        // 🏢 盤を階層ぶん用意し直し、**全階の地形と配置を復元する**（F-2）。
        //   ⚠ `ActivateFloor` はもう盤を組まないので、ここで組まないと空の盤のままになる。
        EnsureBoards(true);
        if (fm != null)
            for (int i = 0; i < floors.Count; i++) fm.ImportFeatures(i, floors[i].features);
        ActivateFloor(current);
    }

    // ============ 🗺️ 横拡張（階層ごとの広さ：研究点RP＋DP） ============
    private static readonly int[] ExpandRP = { 3, 5, 8, 12 };          // →20/30/40/50
    private static readonly int[] ExpandDP = { 400, 800, 1500, 2500 };

    // 🧬 指定個体が『アクティブ層以外』のいずれかのフロアに配置済みか（個体の重複配置防止・全フロア横断）。
    //    アクティブ層はライブのfeaturesで判定するため除外（退避済みスナップショットとの二重計上を防ぐ）。
    public bool IsIndividualPlacedOnOtherFloors(int id)
    {
        if (id < 0) return false;
        for (int i = 0; i < floors.Count; i++)
        {
            if (i == current) continue;
            var recs = floors[i].features;
            if (recs == null) continue;
            foreach (var r in recs) if (r.individualId == id) return true;
        }
        return false;
    }

    /// <summary>
    /// 🧹 『アクティブ層以外』のフロアに置かれているその個体を撤去する（隊から外したとき）。
    /// ⚠ `DungeonFeatureManager.RemovePlacedOfIndividual` は**いま開いている階しか見ない**。
    ///   他の階の配置はここのスナップショットにあるので、両方を消さないと
    ///   「隊から外したのに盤に残る」個体ができる（実際に起きた）。
    /// </summary>
    public int RemoveIndividualFromOtherFloors(int id)
    {
        if (id < 0) return 0;
        int n = 0;
        for (int i = 0; i < floors.Count; i++)
        {
            if (i == current) continue;
            var recs = floors[i].features;
            if (recs == null) continue;
            for (int k = recs.Count - 1; k >= 0; k--)
                if (recs[k].individualId == id) { recs.RemoveAt(k); n++; }
        }
        if (n > 0) Debug.Log($"🧩『他階の配置も解除』個体#{id} を {n} か所から外した");
        return n;
    }

    // 👑 指定個体が『アクティブ層以外』のフロアでボスに任命されているか（そのフロアindex／無ければ-1）。
    public int BossFloorOfIndividual(int id)
    {
        if (id < 0) return -1;
        for (int i = 0; i < floors.Count; i++)
        {
            if (i == current) continue;
            var recs = floors[i].features;
            if (recs == null) continue;
            foreach (var r in recs)
                if (r.type == DungeonFeatureManager.FeatureType.Boss && r.individualId == id) return i;
        }
        return -1;
    }

    // ============ 🏛️ 領域（Domain）＝ 拡張の見返り ============
    // 『深さ』と『広さ』をそれぞれ別の見返りに変換する。ここが階層拡張の存在理由。
    //  ・深さ → 深部で倒すほど撃破DP/感情/素材が増える（＝浅い階で皆殺しにせず深く誘い込む＝原作の泳がせ）
    //  ・広さ → 置ける要素数の上限（防衛の器）＋ 名声（集客と冒険者の質）
    /// <summary>
    /// 🏢 階層の上限。⚠ 5 で固定していたせいで、領域研究『第6層拡張』『第7層拡張』を
    ///   取っても**6層目を足せなかった**（＝RPを払っても何も起きない死に研究になっていた）。
    /// </summary>
    public static int MaxFloors =>
        ResearchState.IsResearched("d_floor7") ? 7 :
        ResearchState.IsResearched("d_floor6") ? 6 : 5;

    private const float DepthRewardPerFloor = 0.15f;   // 1階下るごとの報酬倍率
    private const int PlaceCapBase = 12;               // 10×10 のときの配置上限（罠・トーテムも枠を食うので戦力が残る数に）
    private const int PlaceCapPerStep = 4;             // 広さ1段(＋10)ごとの上限増

    /// <summary>B{n}F の報酬倍率（撃破DP・感情・素材に乗る）。B1F=1.00、以降+0.15/階。遺物『深度の王冠』で増える。</summary>
    public float DepthRewardMult(int floorIndex)
    {
        float per = DepthRewardPerFloor + (RelicManager.Instance != null ? RelicManager.Instance.DepthBonusExtra : 0f);
        return 1f + Mathf.Max(0, floorIndex) * per;
    }
    /// <summary>現在戦闘中のフロアの報酬倍率（各所から手軽に参照するための静的窓口）。</summary>
    public static float CurrentDepthRewardMult
        => Instance != null ? Instance.DepthRewardMult(Instance.current) : 1f;
    public static bool CurrentFloorIsDeepest => Instance != null && Instance.IsDeepest(Instance.current);

    /// <summary>その階層に置ける要素数の上限（広さ＝防衛の器）。</summary>
    public int PlacementCap(int i)
    {
        int size = FloorSize(i);
        if (size <= 0) return PlaceCapBase;
        // 🏛️ 領域研究『広間の設計』『大広間の設計』（配線漏れだった＝説明の +2 が効いていなかった）
        int byResearch = (ResearchState.IsResearched("d_slot1") ? 2 : 0)
                       + (ResearchState.IsResearched("d_slot2") ? 2 : 0)
                       + (ResearchState.IsResearched("h_sloth") ? 2 : 0);   // 👑 怠惰の刻印：積む道
        return PlaceCapBase + Mathf.Max(0, (size - 10) / 10) * PlaceCapPerStep
             + DungeonTheme.PlacementCapBonus + byResearch;
    }
    public static int CurrentPlacementCap => Instance != null ? Instance.PlacementCap(Instance.current) : 99;

    /// <summary>領域の名声＝Σ(各階の広さ段階)。広く深いほど有名になり、強い冒険者が大挙して来る（旨いが危険）。</summary>
    public int DomainRenown { get { int n = 0; for (int i = 0; i < floors.Count; i++) n += Mathf.Max(1, floors[i].size / 10); return n; } }
    /// <summary>拡張ぶんの名声（階層数を引いた分＝実際に広げた段数の合計）。</summary>
    public int ExpandedRenown => Mathf.Max(0, DomainRenown - floors.Count);
    /// <summary>名声によるウェーブ増員（2段の拡張ごとに+1人）。</summary>
    public static int RenownBonusAdventurers => Instance != null ? Instance.ExpandedRenown / 2 : 0;
    /// <summary>名声による冒険者の質の上振れ（ランク抽選に加算される確率的な押し上げ）。</summary>
    public static float RenownHeroRankBias => Instance != null ? Instance.ExpandedRenown * 0.06f : 0f;

    // ============ 🗺️ 拡張の「取引」を見せる（W-1）============
    // ⚠⚠ **これまで得しか書いていなかった。** 拡張ボタンの横にあったのは「(枠+4)」と値段だけ。
    //   実際にはこの1段で **名声が上がり、来る冒険者が人数も質も増える**（旨いが危険）。
    //   さらに**地形が作り直される**（配置は引き継ぐが、置けなくなった物だけ返金）。
    //   ＝ 払う側が3つあるのに1つも書いていなかった。
    //   このプロジェクトで繰り返し出た病気（**あるのに見えていない**）の、こちらは裏返し
    //   ―― **代償が見えていない**。どちらも「選んだ気になれない」という同じ結果になる。
    // ⚠ 数字を新しく作らない。ここは `RenownBonusAdventurers` / `RenownHeroRankBias` /
    //   `TryExpandFloor` の返金処理を**そのまま言葉にしているだけ**。

    /// <summary>拡張で増える側（1段）。</summary>
    public string ExpandGainLine(int i)
    {
        int ns = NextFloorSize(i);
        return "<color=#5cc47c>枠 +" + PlaceCapPerStep + "</color>（" + PlacementCap(i) + "→" + (PlacementCap(i) + PlaceCapPerStep)
             + "）・<color=#5cc47c>経路が伸びる</color>　<size=92%>" + ns + "×" + ns + "</size>";
    }

    /// <summary>拡張で払う側（1段）。⚠ 得と**同じ行に並べて**初めて取引になる。</summary>
    public string ExpandCostLine(int i)
    {
        int before = ExpandedRenown, after = before + 1;
        int addMen = after / 2 - before / 2;
        string men = addMen > 0 ? "人数 <b>+" + addMen + "人</b>・" : "";
        string soon = addMen > 0 ? "" : "<color=#9c95b4>（次の段で人数+1人）</color>";
        // ⚠ 以前ここは「配置は全部クリア（50%返金）」だった。**仕様を直したので文言も直す**
        //   （→ `TryExpandFloor` / `DungeonFeatureManager.RestoreAfterResize`）。
        //   嘘の代償を書き続けると、直したことがプレイヤーに伝わらない。
        return "<color=#e08a8a>敵 " + men + "質 <b>+6%</b></color>" + soon
             + "　<color=#9c95b4>地形は作り直し（配置は引き継ぎ・置けない物だけ返金）</color>";
    }

    public int FloorSize(int i) => (i >= 0 && i < floors.Count) ? floors[i].size : 0;
    public bool CanExpandFloor(int i) => i >= 0 && i < floors.Count && floors[i].size < 50;
    public int NextFloorSize(int i) => Mathf.Min(50, floors[i].size + 10);
    private static int CostIndex(int targetSize) => Mathf.Clamp(targetSize / 10 - 2, 0, 3);
    public int ExpandRPCost(int i) => CanExpandFloor(i) ? ExpandRP[CostIndex(NextFloorSize(i))] : 0;
    // 🏗️ 創造ランクで領域拡張のDPが安くなる（魔王の創造ステが活きる）
    private static float DomainMult => DemonLord.Instance != null ? DemonLord.Instance.DomainCostMult : 1f;
    public int ExpandDPCost(int i) => CanExpandFloor(i) ? Mathf.RoundToInt(ExpandDP[CostIndex(NextFloorSize(i))] * DomainMult) : 0;

    // 指定階層を1段(10)拡張。準備フェーズのみ。RP＋DPを消費し、その階層を新サイズで再生成。
    // 🗺️ 配置は**引き継ぐ**（同じマス→駄目なら近い床へずらす／置けない物だけ返金）。
    public bool TryExpandFloor(int i)
    {
        Refs();
        if (i < 0 || i >= floors.Count || gen == null) return false;
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 階層拡張は準備フェーズのみ可能です。"); return false; }
        var fd = floors[i];
        if (fd.size >= 50) { Debug.LogWarning("⚠️ 既に最大(50×50)です。"); return false; }
        int nextSize = fd.size + 10;
        int rpCost = ExpandRP[CostIndex(nextSize)], dpCost = ExpandDP[CostIndex(nextSize)];
        var res = DungeonResourceManager.Instance;
        if (ResearchState.RP < rpCost) { Debug.LogWarning($"⚠️ 研究点が不足（要{rpCost}RP）。"); return false; }
        if (res != null && res.DungeonPoints < dpCost) { Debug.LogWarning($"⚠️ DPが不足（要{dpCost}DP）。"); return false; }
        ResearchState.TrySpendRP(rpCost);
        if (res != null) res.TrySpendDP(dpCost);

        // 🗺️ **配置は捨てない。** 地形は作り直すので、いったん退避しておいて後で戻す。
        //   ⚠⚠ 以前はここで全部返金して消していた。そのせいで広げた**直後の波を空の盤で迎える**ことになり、
        //     通しプレイでは広げるほど早く死んだ（T15 → T12 → **T11**）。
        //     ＝「広さの報酬」を用意しても、受け取る前に守りが消えるので誰も広げられなかった
        //     （→ [[growth-is-a-trap]] ／ `DungeonFeatureManager.RestoreAfterResize`）。
        var saved = fm != null ? fm.ExportFeatures(i) : null;
        if (fm != null) fm.ClearAllFeatures(i);

        var nfd = gen.BuildFloorData(nextSize);
        nfd.isDeepest = fd.isDeepest;
        nfd.features = new List<DungeonFeatureManager.FeatureRecord>();
        floors[i] = nfd;

        // ⚠⚠ **盤を組み直すのはここ。** `ActivateFloor` は F-2 で「見る階を変えるだけ」になったので、
        //   あれを呼んでも地形は 10×10 のまま変わらない（ユーザー報告で発覚）。
        BuildBoard(i);

        // 🗺️ 配置を戻す（同じマス → 駄目なら近い床へずらす）。戻せなかったぶんだけ返金。
        int kept = 0, moved = 0, lostN = 0;
        if (fm != null && saved != null)
        {
            List<DungeonFeatureManager.FeatureRecord> lost;
            kept = fm.RestoreAfterResize(i, saved, out moved, out lost);
            lostN = lost.Count;
            if (lostN > 0) fm.RefundRecords(lost);
        }

        if (i == current) ActivateFloor(i);   // 表示中ならカメラも合わせ直す
        if (saved != null && saved.Count > 0)
            NotifySystem.Push("<b>B" + (i + 1) + "F を広げた</b> ― 配置 <b>" + kept + "/" + saved.Count + "</b> をそのまま引き継いだ"
                + (moved > 0 ? "（" + moved + " 個は近くへずらした）" : "")
                + (lostN > 0 ? "　<color=#e05a5a>" + lostN + " 個は置けず返金</color>" : ""),
                NotifySystem.Kind.Gain);
        Debug.Log($"🗺️『階層拡張』B{i + 1}F を {fd.size}×{fd.size} → {nextSize}×{nextSize} に拡張"
            + $"（-{rpCost}RP -{dpCost}DP・配置 {kept}/{(saved != null ? saved.Count : 0)} 引継ぎ・ずらし {moved}・返金 {lostN}）");
        return true;
    }

    // ============ 🏢 縦拡張（階層の追加：準備中のみ・削除不可・4層以降は領域研究ゲート） ============
    // 生成時は1〜3層。準備中に下へ追加できる（3層まではDPのみ、4層目以降は領域研究が要る）。最大7層。
    /// <summary>次の1層を足すのに要る研究id（要らなければ空）。⚠ ここと `MaxFloors` を必ず揃える。</summary>
    public string AddFloorResearchNeeded()
    {
        switch (floors.Count)
        {
            case 3: return "d_floor4";
            case 4: return "d_floor5";
            case 5: return "d_floor6";
            case 6: return "d_floor7";
            default: return "";
        }
    }
    public bool CanAddFloor()
    {
        if (floors.Count >= MaxFloors) return false;
        string need = AddFloorResearchNeeded();
        return string.IsNullOrEmpty(need) || ResearchState.IsResearched(need);
    }
    public int AddFloorDPCost()
        => Mathf.RoundToInt((floors.Count < 3 ? 800 : 1000 * (floors.Count - 1)) * DomainMult);

    /// <summary>
    /// 🏗️ 階層を増やす。⚠ `free` は**生産（大工事）で作ったとき**に立てる ―― DPを取らない。
    /// → [[ProductionSystem]]
    /// </summary>
    public bool TryAddFloor(bool free)
    {
        addFloorFree = free;
        try { return TryAddFloor(); }
        finally { addFloorFree = false; }
    }
    private bool addFloorFree;

    public bool TryAddFloor()
    {
        Refs();
        if (gen == null) return false;
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 階層追加は準備フェーズのみ可能です。"); return false; }
        if (floors.Count >= MaxFloors) { Debug.LogWarning($"⚠️ 階層は最大{MaxFloors}層です（さらに増やすには領域研究）。"); return false; }
        {
            string need = AddFloorResearchNeeded();
            if (!string.IsNullOrEmpty(need) && !ResearchState.IsResearched(need))
            { Debug.LogWarning($"⚠️ 第{floors.Count + 1}層の追加には領域研究『{need}』が必要です。"); return false; }
        }
        int cost = AddFloorDPCost();
        var res = DungeonResourceManager.Instance;
        // 🏗️ 生産（大工事）で作ったときはDPを取らない
        if (!addFloorFree && res != null && !res.TrySpendDP(cost)) return false;

        var nfd = gen.BuildFloorData(10);
        if (floors.Count > 0) floors[floors.Count - 1].isDeepest = false;
        nfd.isDeepest = true;
        floors.Add(nfd);
        // ⚠⚠ **増えた階のぶんの盤だけ**を用意する（F-2）。
        //   `EnsureBoards(true)` にすると `BuildFromMap` が各階の配置を消すので、
        //   **階を1つ足しただけで既に置いた配下・罠が全部消える**（実プレイで踏んだ）。
        EnsureBoards(false);
        // 👑 魔王が新しい最下層へ移る。
        // ⚠⚠ **前の最下層を組み直さないこと。** `BuildBoard` は `BuildFromMap` を通り、
        //   その階の配置を消す ―― 実プレイで**階を1つ足した瞬間にB1Fの配下5体が消えた**。
        //   魔王を動かすのは `RefreshLordPresence`（`ActivateFloor` から呼ばれる）の役目。
        ActivateFloor(current);
        RefreshLordPresence();
        Debug.Log($"🏢『階層追加』B{floors.Count}F を最深部に追加（-{cost}DP）");
        return true;
    }

    // ============ descent（階層踏破式の侵略） ============

    /// <summary>侵略開始：最上階(B1F)を構築し、そのフロアの防衛体をスポーンする。</summary>
    public void BeginDescent()
    {
        Refs();
        if (floors.Count == 0) return;
        // 🧩 侵略開始時、今編集中フロアの配置要素を保存してからB1Fへ（他フロアの配置消失バグ修正）
        if (fm != null && CurrentFloor != null) CurrentFloor.features = fm.ExportFeatures();
        battleActive = true;
        current = 0;
        deepestReached = 0;
        for (int i = 0; i < floorTouched.Length; i++) { floorTouched[i] = false; advOnFloor[i] = 0; }
        floorTouched[0] = true;    // B1F には必ず来る
        occTimer = 0f;
        fallen.Clear(); fallenCells.Clear(); fallenFrom.Clear();   // 🕳️ 前の波の控えを持ち越さない
        MinionRoster.ClearFoughtFlags();   // 🔁 前のウェーブの『戦った』印を持ち越さない（反芻の可否に使う）
        ActivateFloor(0);
        // 🏢 **全階の守りを一度に立てる**（F-2）。旧仕様は「降りた先の階だけ」を降下のたびに立てていたが、
        //    縦の迷宮では上の階も同時に戦い続けるので、最初に全部立てておく。
        if (fm != null) fm.SpawnDefendersForAllFloors(floors.Count);
        Debug.Log($"⚔️『侵略開始』B1F から侵攻開始（守りは全 {floors.Count} 層に配備済み）");
    }

    /// <summary>侵略終了：状態をリセットし、表示を最上階へ戻す。</summary>
    public void EndDescent()
    {
        ReleaseFallenAsEscaped();   // 🕳️ 下に落としたまま終わったら、這い上がって逃げる
        RecountOccupancy();         // ⚠ 最後にもう一度数える（終了間際に降りた階を取りこぼさない）
        ReportBreaches();           // 🏢 どの階まで来られたかを1行で報告（F-4）
        GrantWaveExp();
        battleActive = false;
        SpellField.ClearAll();                      // 🔥 灼野・泥沼は波をまたがせない（→ [[SpellField]]）
        if (fm != null) fm.DespawnAllDefenders();   // 🏢 撤収は**波の終わりに全階まとめて**（F-2）
        if (floors.Count > 0) { current = 0; ActivateFloor(0); }
    }

    /// <summary>
    /// 🧬 **経験は波の終わりに、実際に戦った階だけへ配る**（F-4）。
    ///
    /// ⚠⚠ **F-2で前提が壊れていた。** 旧仕様は「降りた先の階の守りを立てる」瞬間に実戦経験を配っており、
    ///   立った＝戦ったが成り立っていた。F-2で**全階の守りを開幕に立てる**ようにしたので、
    ///   そのままだと**冒険者が一度も来ていないB3Fの配下まで満額の実戦経験を貰う**（置くだけでタダ）。
    ///   → 配るのを波の終わりに移し、**その階に冒険者が入ったかどうか**で満額／待機(1/4)を分ける。
    /// </summary>
    private void GrantWaveExp()
    {
        if (deepestReached < 0) return;
        RelicManager.ReportFloorHeld(deepestReached + 1); // 🏺 実績：どこまで攻め込まれて守り切ったか
        int fought = 0, idle = 0;
        for (int i = 0; i < floors.Count; i++)
        {
            bool sawCombat = i < floorTouched.Length && floorTouched[i];
            var recs = fm != null ? fm.ExportFeatures(i) : null;
            if (recs == null) continue;
            foreach (var r in recs)
            {
                if (r.individualId < 0) continue;
                if (r.type != DungeonFeatureManager.FeatureType.Squad && r.type != DungeonFeatureManager.FeatureType.Boss) continue;
                MinionRoster.AddFloorExp(r.individualId, i, sawCombat);   // 🧪 魔素濃度 + 🐢 追いつき補正
                if (sawCombat) fought++; else idle++;
            }
        }
        lastDeepestReached = deepestReached;
        deepestReached = -1;
        Debug.Log($"🧬『経験』実戦 {fought} 体／待機 {idle} 体（待機は実戦の1/4）");
    }

    /// <summary>
    /// 🏢 波の終わりに「どこまで来られたか」を1行で残す（F-4）。
    /// ⚠ 縦の迷宮では**同時に複数の階が破られる**ので、「最深部まで何F」だけでは何が起きたか読めない。
    /// </summary>
    private void ReportBreaches()
    {
        int touched = 0;
        string s = "";
        for (int i = 0; i < floors.Count; i++)
        {
            if (!FloorTouched(i)) continue;
            touched++;
            s += (s.Length > 0 ? "／" : "") + FloorLabel(i);
        }
        if (touched <= 0) return;
        string deep = FloorLabel(Mathf.Clamp(deepestReached, 0, floors.Count - 1));
        Debug.Log($"🏢『戦域』この波で戦いが起きた階＝{s}（最深 {deep}／全 {floors.Count} 層）");
        if (touched > 1)
            NotifySystem.Push($"この波は <b>{touched} 層</b>で同時に戦った（最深 <b>{deep}</b>）", NotifySystem.Kind.Story);
    }

    // ============ 🏢 階ごとの在籍（F-4：UI と経験の判定が同じ数字を見る） ============
    // ⚠ どちらも**波のあいだだけ生きる数**。波の頭で全部消してから数え直す（`RecountOccupancy` は4回/秒）。
    //   セーブに乗せる物ではないので `[NonSerialized]` を付けて、セーブの見張りを黙らせる。
    //   （`readonly` なコレクションは「保存し忘れた状態では？」と疑われる ―― ここは疑いが外れる側）
    [System.NonSerialized] private readonly int[] advOnFloor = new int[8];
    [System.NonSerialized] private readonly bool[] floorTouched = new bool[8];
    private float occTimer;

    /// <summary>その階にいま居る冒険者の数（戦闘中のみ意味がある）。⚠ 4回/秒で数え直した値。</summary>
    public int AdventurersOnFloor(int i) => (i >= 0 && i < advOnFloor.Length) ? advOnFloor[i] : 0;
    /// <summary>この波で、その階に冒険者が入ったか。</summary>
    public bool FloorTouched(int i) => (i >= 0 && i < floorTouched.Length) && floorTouched[i];

    /// <summary>
    /// 階ごとの在籍を数え直す。⚠ 毎フレームやらない（`FindObjectsByType` は重い）。
    /// UI も経験の判定も**この1つの数字**を見る（別々に数えると食い違う）。
    /// </summary>
    private void RecountOccupancy()
    {
        for (int i = 0; i < advOnFloor.Length; i++) advOnFloor[i] = 0;
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None))
        {
            if (a == null) continue;
            int f = a.MyFloor;
            if (f < 0 || f >= advOnFloor.Length) continue;
            advOnFloor[f]++;
            floorTouched[f] = true;
        }
    }

    private void Update()
    {
        if (!battleActive) return;
        var turn = DungeonTurnManager.Instance;
        if (turn == null || !turn.IsBattlePhase) { battleActive = false; return; }

        // 🏢 階ごとの在籍を数え直す（F-4）。⚠ **降下の判定より前**（下の早期returnで飛ばさない）。
        //    タブの表示と、波の終わりの経験の判定が同じ数字を見る。
        occTimer += Time.deltaTime;
        if (occTimer >= 0.25f) { occTimer = 0f; RecountOccupancy(); }

        Refs();
        if (spawner == null) spawner = Object.FindFirstObjectByType<DungeonAdventurerSpawner>();

        // 🏢 **降下は階ごとに独立して起きる**（縦の迷宮）。
        // ⚠⚠ 旧仕様は `current`（＝表示中の階）1つだけを見ていた。`current` が
        //   「表示している階」と「侵攻の最前線」の**二役**を兼ねていたためで、
        //   これが原因で次の2つが同時に起きていた（ユーザー報告）：
        //     ・戦闘中に B1F のタブを押すと `current=0` になり、B1Fに残っていた者が
        //       階段の上に居るので**その場でまた降下**（「2階層に侵入」が再表示され、
        //       押しても押しても B2F に戻される）
        //     ・逆に B2F を見ている間は B1F の降下判定が**一度も走らない**ので、
        //       B1F に残った踏破目的の者が階段の前で永久に止まり、**波が終わらない**
        //   → `current` は**表示専用**にし、降下は全ての階について毎tick判定する。
        for (int f = 0; f < floors.Count - 1; f++) TryDescendFrom(f);
    }

    /// <summary>その階から下へ降りられる状況かを見て、条件が揃っていれば降ろす。</summary>
    private void TryDescendFrom(int from)
    {
        if (from < 0 || from >= floors.Count - 1) return;
        if (advOnFloor[Mathf.Clamp(from, 0, advOnFloor.Length - 1)] <= 0) return;   // その階に誰も居ない

        // 🛡️ **階層ボスを倒さないと次へ進めない**（この作品の設計。維持すること）
        if (ZombieAI.GetLivingGuardianOnFloor(from) != null) return;
        // 👑 親征：魔王が立っている階で侵攻は止まる（彼が壁になる）
        if (IsLordFloor(from) && DemonLord.Instance != null && DemonLord.Instance.IsAlive) return;

        var g = DungeonGridSystem.Of(from);
        if (g == null) return;
        Vector2Int stairs = g.BossCell;
        int next = from + 1;
        bool anyCanDescend = false;
        var stuck = new List<AdventurerAI>();
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None))
        {
            if (a == null || a.IsRetreating) continue;
            if (a.MyFloor != from) continue;
            if (a.AdventurerPurpose != AdventurerAI.Purpose.Conquer) continue;
            if (g.WorldToGrid(a.transform.position) != stairs) continue;
            if (a.WillDescendTo(next)) anyCanDescend = true;
            else stuck.Add(a);
        }

        // ⚠⚠ **階段に着いたのに降りられない者を放置しない。**
        //   F-2で「降りられない者はその階に残る」ようにしたが、**踏破目的の彼らには
        //   階段以外の目的が無い**ので、階段の上で永久に立ち尽くす。
        //   その階の守りを倒し切っていると誰にも倒されないため、**波が永遠に終わらない**
        //   （ユーザー報告：2階を殲滅してもターンが終わらない）。
        //   → 手が届かないと悟った者は**諦めて引き返す**（歩いて帰り、感情DPを清算する）。
        //   ＝「取り逃がした」扱いになるので、因縁が生まれる余地も残る（→ [[Nemesis]]）。
        if (stuck.Count > 0 && !anyCanDescend)
        {
            foreach (var a in stuck) if (a != null) a.ForceRetreat();
            Debug.Log($"🪜『断念』B{next + 1}F に手が届かない {stuck.Count} 体が階段の前で引き返した"
                + $"（必要Lv{AdventurerAI.DescendLevelNeed(next)}）");
            return;
        }
        if (!anyCanDescend) return;

        // ⏩ まだ控えが居るなら、待たずに雪崩れ込ませてから降りる（湧き待ちの空白時間をなくす）
        // ⚠ これは B1F（＝湧き口）から降りるときだけの話。下の階の降下を湧きで止めない。
        if (from == 0 && spawner != null && spawner.IsSpawning) { spawner.FlushRemaining(); return; }
        Descend(from);
    }

    /// <summary>
    /// `from` の階から1つ下へ降ろす。
    /// ⚠⚠ **`current`（表示中の階）を書き換えないこと。** 表示と侵攻は別物。
    ///   ただし「いま降りた階を見ていた」ときだけは、視点を一緒に連れていく
    ///   （見ていた戦いが黙って画面外へ消えないように）。
    /// </summary>
    private void Descend(int from)
    {
        Refs();
        int nextF = from + 1;
        if (nextF >= floors.Count) return;
        int next = nextF;
        bool watching = (current == from);   // 見ていた階から降りたか

        // 🪜 適性深度：**降りるのは次の階層に見合う者だけ**。
        // ⚠⚠ **F-2以降、見合わない者は退場させない。** 旧仕様は `ForceDespawnWithReward()` で
        //   その場で帰していたが、縦の迷宮では**その階に残って戦い続ける**。
        //   これが「1階を捨て階にして消耗させ、下で仕留める」を成立させている中心。
        var survivors = new List<AdventurerAI>();
        int stayed = 0;
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None))
        {
            if (a == null) continue;
            if (a.MyFloor != from) continue;                    // 🏢 いま降りようとしている階の者だけが対象
            if (a.IsRetreating) continue;                       // 退却中の者は自分で入口へ帰る
            if (!a.WillDescendTo(next)) { stayed++; continue; } // ← 残る（帰さない）
            survivors.Add(a);
        }
        // ⚠ 誰も降りられないなら何もしない（毎tickここへ来て降下トーストが出続けるのを防ぐ）。
        //   ⚠ 奈落の控えも**この階から落ちた者**だけを数える。
        int waiting = 0;
        for (int i = 0; i < fallenFrom.Count; i++) if (fallenFrom[i] == from) waiting++;
        if (survivors.Count == 0 && waiting == 0) return;

        if (stayed > 0)
            Debug.Log($"🪜『残留』B{next + 1}F には手が届かないと見て {stayed} 体が B{from + 1}F に留まった"
                + $"（必要Lv{AdventurerAI.DescendLevelNeed(next)}／この階の戦いは続く）");

        // ⚠ ここで `DespawnDefenders` を呼ばないこと（F-2）。上の階の守りは残って戦い続ける。
        if (next > deepestReached) deepestReached = next;
        floorTouched[Mathf.Clamp(next, 0, floorTouched.Length - 1)] = true;
        if (watching)
        {
            current = next;                            // 見ていた戦いを追いかける
            if (ui != null) ui.PlayFloorTransition();  // 🎬 降下の暗転フェード
            ActivateFloor(next);
        }

        // 🏢 降りた者は**次の階の盤へ移る**（F-2）。⚠ `RelocateTo` の前に階を教えること。
        //   教えないと座標だけ下の階に飛んで、経路は前の階の盤で引き続ける。
        var nextGrid = DungeonGridSystem.Of(next);
        Vector2Int ent = nextGrid != null ? nextGrid.EntranceCell : grid.EntranceCell;
        foreach (var a in survivors) if (a != null) { a.BindFloor(next); a.RelocateTo(ent); }

        // 🕳️ 奈落で先に落ちていた者は**穴の真下**で目を覚ます（＝入口の守りを飛ばして着地する）。
        //    穴の真下が壁なら入口に回す。⚠ ここで起こさないと、彼らは永久に眠ったままになる。
        // ⚠ 起こすのは**この降下の行き先へ落ちた者だけ**（`fallenFrom == from`）。
        int woke = 0;
        for (int i = fallen.Count - 1; i >= 0; i--)
        {
            if (fallenFrom[i] != from) continue;              // 別の階から落ちた者はまだ眠らせておく
            var a = fallen[i];
            var fellAt = fallenCells[i];                      // ⚠ 消す前に読む
            fallen.RemoveAt(i); fallenCells.RemoveAt(i); fallenFrom.RemoveAt(i);
            if (a == null) continue;
            var c = NearestFloorCell(nextGrid, fellAt, ent);  // 🏢 行き先の盤で探す
            a.gameObject.SetActive(true);
            a.BindFloor(next);
            a.RelocateTo(c);
            woke++;
        }
        if (woke > 0) Debug.Log($"🕳️『先着』奈落で先に落ちていた {woke} 体が、B{next + 1}F の穴の真下で待ち構えていた");

        // ⚠ ここで守りを湧かせないこと（F-2）。全階ぶんは `BeginDescent` で立て済み。
        //   ここで呼ぶと**下の階の守りが二重に湧く**。

        if (ui != null) ui.ShowDescentToast(FloorLabel(next), survivors.Count + woke); // 🎬 降下トースト
        Debug.Log($"🚶⬇『突破』B{from + 1}F → B{next + 1}F（生存者 {survivors.Count}＋奈落 {woke}"
            + $" / 残留 {stayed} / {(IsDeepest(next) ? "最下層・魔王" : "通常")}）");
    }

    // ▼ 下り階段マーカー：非最下層のボスセル(降下地点)に表示、最下層は非表示
    private void UpdateStairsMarker()
    {
        if (grid == null) return;
        if (stairsMarker == null) stairsMarker = BuildStairsMarker();
        // 👑 魔王が立っている階では道はそこで終わる＝階段を見せない（降りられないので）
        bool show = floors.Count > 0 && !IsDeepest(current) && !IsLordFloor(current);
        stairsMarker.SetActive(show);
        if (show)
        {
            var c = grid.BossCell;
            // セル中央からやや右下にオフセット（ボス"B"マーカーと重ならないように）
            stairsMarker.transform.position = grid.GridToWorld(c.x, c.y) + new Vector3(0.28f, -0.28f, -0.6f);
        }
    }

    // ▼ 下り階段：手続き生成の『3段＋下向き矢印』（MarkerArt）。下の階へ続くことが一目で分かる形。
    private GameObject BuildStairsMarker()
    {
        var go = new GameObject("StairsMarker");
        go.transform.SetParent(transform, false);

        var art = new GameObject("Art");
        art.transform.SetParent(go.transform, false);
        art.transform.localScale = Vector3.one * 0.62f;
        var sr = art.AddComponent<SpriteRenderer>();
        sr.sprite = MarkerArt.Stairs(); sr.color = new Color(0.42f, 0.86f, 1f, 0.95f); sr.sortingOrder = 58;

        var t = new GameObject("Label");
        t.transform.SetParent(go.transform, false);
        t.transform.localPosition = new Vector3(0f, -0.40f, -0.2f);
        t.transform.localScale = Vector3.one * 0.055f;
        var tm = t.AddComponent<TextMesh>();
        tm.text = "下り階段"; tm.anchor = TextAnchor.UpperCenter; tm.alignment = TextAlignment.Center;
        tm.fontSize = 60; tm.characterSize = 0.5f; tm.color = new Color(0.62f, 0.92f, 1f); tm.fontStyle = FontStyle.Bold;
        var mr = tm.GetComponent<MeshRenderer>(); if (mr != null) mr.sortingOrder = 62;
        return go;
    }
}
