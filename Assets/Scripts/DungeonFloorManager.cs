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
    // ⚠⚠ **階層は同時に1つしか存在しない**（`ActivateFloor` が盤ごと作り直す）。
    //   だから「1人だけ下の階へ移す」は、実体を**眠らせて控えに置く**形で表す。
    //   ・降下が起きたら、下の階の**穴の真下**で目を覚ます（＝入口の守りを飛ばして着地する）
    //   ・降下が起きないまま波が終われば、**這い上がって逃げる**（名声＋略奪装備を持ち帰る）
    //   ＝「落とすこと」は「倒すこと」ではない。落とし穴が万能の削除ボタンにならないようにする線。
    // ⚠ readonly＝セーブに乗せない。戦闘中だけの状態で、保存は準備フェーズにしか起きないので正しい。
    private readonly List<AdventurerAI> fallen = new List<AdventurerAI>();
    private readonly List<Vector2Int> fallenCells = new List<Vector2Int>();
    public int FallenCount => fallen.Count;

    /// <summary>🕳️ 奈落へ落ちた。この階からは退場し、下の階で目を覚ます（か、這い上がって逃げる）。</summary>
    public void SendBelow(AdventurerAI a, Vector2Int cell)
    {
        if (a == null) return;
        fallen.Add(a); fallenCells.Add(cell);
        a.gameObject.SetActive(false);
        Debug.Log($"🕳️『奈落』{cell} の穴から1体が下の階へ落ちた（控え {fallen.Count} 体）");
        NotifySystem.Push("落とし穴が1体を<b>下の階</b>へ落とした。降りるまで戻ってこない", NotifySystem.Kind.Story);
    }

    /// <summary>
    /// 🕳️ 穴の真下に着地させる。各階は別々に生成されるので**真下が壁のことの方が多い**。
    /// そのときは**いちばん近い床**へ寄せる（入口に戻すと「下に落ちた」意味が消えるため）。
    /// どこも駄目なら入口。
    /// </summary>
    private Vector2Int NearestFloorCell(Vector2Int want, Vector2Int fallback)
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
        fallen.Clear(); fallenCells.Clear();
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
        if (grid == null) grid = Object.FindFirstObjectByType<DungeonGridSystem>();
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
        current = 0;
        ActivateFloor(0);
        Debug.Log($"🏢『階層生成』{floors.Count}層を生成（最下層 B{floors.Count}F に魔王）");
    }

    /// <summary>表示フロアを切り替える（準備フェーズのみ）。現フロアの要素を退避し、対象フロアを構築・復元。</summary>
    public void SwitchTo(int i)
    {
        Refs();
        if (i < 0 || i >= floors.Count || i == current) return;
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ フロア切替は準備フェーズのみ可能です。"); return; }

        if (fm != null && CurrentFloor != null) CurrentFloor.features = fm.ExportFeatures(); // 現フロアの要素を退避
        current = i;
        if (ui != null) ui.PlayFloorTransition(); // 切替の暗転フェード
        ActivateFloor(i);
    }

    private void ActivateFloor(int i)
    {
        Refs();
        var fd = floors[i];
        if (grid != null) grid.SetPlayableSize(fd.size); // 🗺️ この階層の広さに合わせる（AI境界/カメラ）
        // 👑 魔王が居るのは**構えが決めた階**（鎮座＝最下層／親征＝選んだ階）。
        //    ⚠ `fd.isDeepest` を直接見ないこと。ここが唯一の判断元（→ [[LordStance]]）。
        grid.BuildFromMap(fd.map, fd.entrance, fd.boss, fd.tint, IsLordFloor(i));
        if (fm != null) fm.ImportFeatures(fd.features);                          // このフロアの要素を復元
        var cam = Object.FindFirstObjectByType<CameraController>();
        if (cam != null) cam.FitToDungeon();
        UpdateStairsMarker(); // ▼ 下り階段マーカー（非最下層のみ表示、ImportFeatures後のBossCellに合わせる）
        Debug.Log($"🔽『フロア切替』B{i + 1}F を表示（{(IsLordFloor(i) ? "魔王在陣" : "通常")}）");
    }

    public string FloorLabel(int i) => "B" + (i + 1) + "F";

    /// <summary>👑 その階に魔王が立つか（鎮座＝最下層／親征＝選んだ階）。盤・タブ・階段の表示はここを見る。</summary>
    public bool IsLordFloor(int i) => i == LordStance.LordFloorIndex(Mathf.Max(1, floors.Count));

    /// <summary>
    /// 👑 構えを変えたときに、魔王の実体だけを移す。
    /// ⚠ `ActivateFloor` を呼び直してはいけない。あれは `fd.features`（退避済みスナップショット）で
    ///   上書きするので、**このターンに置いたばかりの配置が消える**。
    /// </summary>
    public void RefreshLordPresence()
    {
        Refs();
        if (grid == null || DemonLord.Instance == null) return;
        if (IsLordFloor(current)) DemonLord.Instance.PlaceAt(grid.DemonLordCell);
        else DemonLord.Instance.SetPresent(false);
        UpdateStairsMarker();
    }

    // ============ 💾 セーブ / ロード（[[SaveSystem]]） ============
    /// <summary>表示中フロアの配置は FeatureManager 側に居るので、保存前に FloorData へ書き戻す。</summary>
    public void SyncCurrentFloorFeatures()
    {
        Refs();
        if (fm != null && CurrentFloor != null) CurrentFloor.features = fm.ExportFeatures();
    }

    /// <summary>ロード直後。復元された floors から迷宮を組み直す（地形・配置・魔王の実体）。</summary>
    public void RebuildAfterLoad()
    {
        Refs();
        if (floors == null || floors.Count == 0) { Debug.LogWarning("💾 復元した階層が空だった"); return; }
        current = Mathf.Clamp(current, 0, floors.Count - 1);
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
                       + (ResearchState.IsResearched("d_slot2") ? 2 : 0);
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

    public int FloorSize(int i) => (i >= 0 && i < floors.Count) ? floors[i].size : 0;
    public bool CanExpandFloor(int i) => i >= 0 && i < floors.Count && floors[i].size < 50;
    public int NextFloorSize(int i) => Mathf.Min(50, floors[i].size + 10);
    private static int CostIndex(int targetSize) => Mathf.Clamp(targetSize / 10 - 2, 0, 3);
    public int ExpandRPCost(int i) => CanExpandFloor(i) ? ExpandRP[CostIndex(NextFloorSize(i))] : 0;
    // 🏗️ 創造ランクで領域拡張のDPが安くなる（魔王の創造ステが活きる）
    private static float DomainMult => DemonLord.Instance != null ? DemonLord.Instance.DomainCostMult : 1f;
    public int ExpandDPCost(int i) => CanExpandFloor(i) ? Mathf.RoundToInt(ExpandDP[CostIndex(NextFloorSize(i))] * DomainMult) : 0;

    // 指定階層を1段(10)拡張。準備フェーズのみ。RP＋DPを消費し、その階層を新サイズで再生成（配置はクリア＋50%返金）。
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

        // 既存配置を返金してクリア（アクティブ階はライブ要素、非アクティブは退避済みrecord）
        if (fm != null)
        {
            if (i == current) fm.RefundRecords(fm.ExportFeatures());
            else fm.RefundRecords(fd.features);
        }

        var nfd = gen.BuildFloorData(nextSize);
        nfd.isDeepest = fd.isDeepest;
        nfd.features = new List<DungeonFeatureManager.FeatureRecord>();
        floors[i] = nfd;

        if (i == current) ActivateFloor(i); // 新サイズで再構築＋カメラフィット（要素は空）
        Debug.Log($"🗺️『階層拡張』B{i + 1}F を {fd.size}×{fd.size} → {nextSize}×{nextSize} に拡張（-{rpCost}RP -{dpCost}DP・階段は入口から最遠）");
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
        if (res != null && !res.TrySpendDP(cost)) return false;

        // 現フロアの要素を退避してから、新フロアを最深部として追加（魔王が移る）
        if (fm != null && CurrentFloor != null) CurrentFloor.features = fm.ExportFeatures();
        var nfd = gen.BuildFloorData(10);
        if (floors.Count > 0) floors[floors.Count - 1].isDeepest = false;
        nfd.isDeepest = true;
        floors.Add(nfd);
        ActivateFloor(current); // 表示中フロアを再構築（魔王present/最下層フラグ更新）
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
        fallen.Clear(); fallenCells.Clear();   // 🕳️ 前の波の控えを持ち越さない
        MinionRoster.ClearFoughtFlags();   // 🔁 前のウェーブの『戦った』印を持ち越さない（反芻の可否に使う）
        ActivateFloor(0);
        if (fm != null) fm.SpawnDefendersForActiveFloor();
        Debug.Log("⚔️『侵略開始』最上階 B1F から侵攻開始");
    }

    /// <summary>侵略終了：状態をリセットし、表示を最上階へ戻す。</summary>
    public void EndDescent()
    {
        ReleaseFallenAsEscaped();   // 🕳️ 下に落としたまま終わったら、這い上がって逃げる
        GrantGarrisonExp();
        battleActive = false;
        if (floors.Count > 0) { current = 0; ActivateFloor(0); }
    }

    // 🧬 冒険者が到達しなかった階層の配下にも『待機経験』を与える（実戦の1/4）。
    //    到達した階層の配下は SpawnDefendersForActiveFloor で実戦経験を得ている。
    private void GrantGarrisonExp()
    {
        if (deepestReached < 0) return;
        RelicManager.ReportFloorHeld(deepestReached + 1); // 🏺 実績：どこまで攻め込まれて守り切ったか
        int n = 0;
        for (int i = deepestReached + 1; i < floors.Count; i++)
        {
            var recs = floors[i].features; if (recs == null) continue;
            foreach (var r in recs)
            {
                if (r.individualId < 0) continue;
                if (r.type != DungeonFeatureManager.FeatureType.Squad && r.type != DungeonFeatureManager.FeatureType.Boss) continue;
                MinionRoster.AddFloorExp(r.individualId, i, false);   // 🧪 魔素濃度 + 🐢 追いつき補正
                n++;
            }
        }
        lastDeepestReached = deepestReached;
        deepestReached = -1;
        if (n > 0) Debug.Log($"🧬『待機経験』冒険者が到達しなかった階層の配下 {n} 体に +{MinionRoster.GarrisonExp}exp（実戦の1/4）");
    }

    private void Update()
    {
        if (!battleActive) return;
        var turn = DungeonTurnManager.Instance;
        if (turn == null || !turn.IsBattlePhase) { battleActive = false; return; }
        if (IsDeepest(current)) return; // 最下層は魔王討伐で決着（降下なし）
        // 👑 親征：**魔王が立っている階で侵攻は止まる**。彼が壁になる。
        //    ⚠ この行が無いと、冒険者が魔王(=DemonLordCell)を殴りながら同時に降りてしまう
        //      （魔王を置いていない階では DemonLordCell と BossCell が同じセルになるため）。
        if (DemonLord.Instance != null && DemonLord.Instance.IsPresent && DemonLord.Instance.IsAlive) return;

        Refs();
        if (spawner == null) spawner = Object.FindFirstObjectByType<DungeonAdventurerSpawner>();
        if (ZombieAI.GetLivingGuardian() != null) return;       // 門番生存中は突破不可

        // 下り階段(=このフロアのボスセル)に踏破者が到達したか
        Vector2Int stairs = grid.BossCell;
        bool atStairs = false;
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None))
        {
            if (a == null || a.IsRetreating) continue;
            if (a.AdventurerPurpose != AdventurerAI.Purpose.Conquer) continue;
            if (grid.WorldToGrid(a.transform.position) == stairs) { atStairs = true; break; }
        }
        if (!atStairs) return;

        // ⏩ まだ控えが居るなら、待たずに雪崩れ込ませてから降りる（湧き待ちの空白時間をなくす）
        if (spawner != null && spawner.IsSpawning) { spawner.FlushRemaining(); return; }
        Descend();
    }

    private void Descend()
    {
        Refs();
        int next = current + 1;
        if (next >= floors.Count) return;

        // 🪜 適性深度：**降りるのは次の階層に見合う者だけ**。見合わない者は階段の前で引き返す。
        //    （旧仕様は「退却中でない全員」が降りていたので、弱い者まで下層へ雪崩れ込んでいた）
        var survivors = new List<AdventurerAI>();
        int turnedBack = 0;
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None))
        {
            if (a == null) continue;
            if (a.IsRetreating) { a.ForceDespawnWithReward(); continue; }
            if (!a.WillDescendTo(next)) { a.ForceDespawnWithReward(); turnedBack++; continue; }
            survivors.Add(a);
        }
        if (turnedBack > 0)
            Debug.Log($"🪜『引き返す』B{next + 1}F には手が届かないと見て {turnedBack} 体が階段の前で戻った"
                + $"（必要Lv{AdventurerAI.DescendLevelNeed(next)}）");

        if (fm != null) fm.DespawnDefenders();  // 現フロアの防衛体を撤収
        current = next;
        if (next > deepestReached) deepestReached = next;
        if (ui != null) ui.PlayFloorTransition();   // 🎬 降下の暗転フェード
        ActivateFloor(next);                        // 次フロアを構築（最下層なら魔王が実在）

        Vector2Int ent = grid.EntranceCell;
        foreach (var a in survivors) if (a != null) a.RelocateTo(ent); // 生存者を次フロア入口へ

        // 🕳️ 奈落で先に落ちていた者は**穴の真下**で目を覚ます（＝入口の守りを飛ばして着地する）。
        //    穴の真下が壁なら入口に回す。⚠ ここで起こさないと、彼らは永久に眠ったままになる。
        int woke = 0;
        for (int i = 0; i < fallen.Count; i++)
        {
            var a = fallen[i]; if (a == null) continue;
            var c = NearestFloorCell(fallenCells[i], ent);
            a.gameObject.SetActive(true);
            a.RelocateTo(c);
            woke++;
        }
        fallen.Clear(); fallenCells.Clear();
        if (woke > 0) Debug.Log($"🕳️『先着』奈落で先に落ちていた {woke} 体が、穴の真下で待ち構えていた");

        if (fm != null) fm.SpawnDefendersForActiveFloor();             // 次フロアの防衛体をスポーン

        if (ui != null) ui.ShowDescentToast(FloorLabel(current), survivors.Count + woke); // 🎬 降下トースト
        Debug.Log($"🚶⬇『突破』B{current + 1}F へ降下（生存者 {survivors.Count}＋奈落 {woke} / {(IsDeepest(current) ? "最下層・魔王" : "通常")}）");
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
