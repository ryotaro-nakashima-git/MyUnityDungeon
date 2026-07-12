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

    private readonly List<FloorData> floors = new List<FloorData>();
    private int current = 0;

    private DungeonGenerator gen;
    private DungeonGridSystem grid;
    private DungeonFeatureManager fm;
    private DungeonAdventurerSpawner spawner;
    private GameUIManager ui;
    private GameObject stairsMarker; // ▼ 下り階段マーカー（非最下層のボスセルに表示）

    // ===== descent（階層踏破）状態 =====
    private bool battleActive = false;
    public bool BattleActive => battleActive;

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
        Debug.Log($"🏢【階層生成】{floors.Count}層を生成（最下層 B{floors.Count}F に魔王）");
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
        grid.BuildFromMap(fd.map, fd.entrance, fd.boss, fd.tint, fd.isDeepest); // 魔王は最下層のみ実在
        if (fm != null) fm.ImportFeatures(fd.features);                          // このフロアの要素を復元
        var cam = Object.FindFirstObjectByType<CameraController>();
        if (cam != null) cam.FitToDungeon();
        UpdateStairsMarker(); // ▼ 下り階段マーカー（非最下層のみ表示、ImportFeatures後のBossCellに合わせる）
        Debug.Log($"🔽【フロア切替】B{i + 1}F を表示（{(fd.isDeepest ? "最下層・魔王在" : "通常")}）");
    }

    public string FloorLabel(int i) => "B" + (i + 1) + "F";

    // ============ 🗺️ 横拡張（階層ごとの広さ：研究点RP＋DP） ============
    private static readonly int[] ExpandRP = { 3, 5, 8, 12 };          // →20/30/40/50
    private static readonly int[] ExpandDP = { 400, 800, 1500, 2500 };

    // 🧬 指定個体が「アクティブ層以外」のいずれかのフロアに配置済みか（個体の重複配置防止・全フロア横断）。
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

    public int FloorSize(int i) => (i >= 0 && i < floors.Count) ? floors[i].size : 0;
    public bool CanExpandFloor(int i) => i >= 0 && i < floors.Count && floors[i].size < 50;
    public int NextFloorSize(int i) => Mathf.Min(50, floors[i].size + 10);
    private static int CostIndex(int targetSize) => Mathf.Clamp(targetSize / 10 - 2, 0, 3);
    public int ExpandRPCost(int i) => CanExpandFloor(i) ? ExpandRP[CostIndex(NextFloorSize(i))] : 0;
    public int ExpandDPCost(int i) => CanExpandFloor(i) ? ExpandDP[CostIndex(NextFloorSize(i))] : 0;

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
        Debug.Log($"🗺️【階層拡張】B{i + 1}F を {fd.size}×{fd.size} → {nextSize}×{nextSize} に拡張（-{rpCost}RP -{dpCost}DP・階段は入口から最遠）");
        return true;
    }

    // ============ 🏢 縦拡張（階層の追加：準備中のみ・削除不可・4層以降は領域研究ゲート） ============
    // 生成時は1〜3層。準備中に下へ追加できる（3層まではDPのみ、4層目=d_floor4/5層目=d_floor5が必要）。最大5層。
    public bool CanAddFloor()
    {
        if (floors.Count >= 5) return false;
        if (floors.Count >= 3)
        {
            string need = floors.Count == 3 ? "d_floor4" : "d_floor5";
            if (!ResearchState.IsResearched(need)) return false;
        }
        return true;
    }
    public int AddFloorDPCost() => floors.Count < 3 ? 800 : (floors.Count == 3 ? 2000 : 3000);
    public string AddFloorResearchNeeded() => floors.Count == 3 ? "d_floor4" : (floors.Count == 4 ? "d_floor5" : "");

    public bool TryAddFloor()
    {
        Refs();
        if (gen == null) return false;
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 階層追加は準備フェーズのみ可能です。"); return false; }
        if (floors.Count >= 5) { Debug.LogWarning("⚠️ 階層は最大5層です。"); return false; }
        if (floors.Count >= 3)
        {
            string need = floors.Count == 3 ? "d_floor4" : "d_floor5";
            if (!ResearchState.IsResearched(need)) { Debug.LogWarning($"⚠️ 第{floors.Count + 1}層の追加には領域研究「{need}」が必要です。"); return false; }
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
        Debug.Log($"🏢【階層追加】B{floors.Count}F を最深部に追加（-{cost}DP）");
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
        ActivateFloor(0);
        if (fm != null) fm.SpawnDefendersForActiveFloor();
        Debug.Log("⚔️【侵略開始】最上階 B1F から侵攻開始");
    }

    /// <summary>侵略終了：状態をリセットし、表示を最上階へ戻す。</summary>
    public void EndDescent()
    {
        battleActive = false;
        if (floors.Count > 0) { current = 0; ActivateFloor(0); }
    }

    private void Update()
    {
        if (!battleActive) return;
        var turn = DungeonTurnManager.Instance;
        if (turn == null || !turn.IsBattlePhase) { battleActive = false; return; }
        if (IsDeepest(current)) return; // 最下層は魔王討伐で決着（降下なし）

        Refs();
        if (spawner == null) spawner = Object.FindFirstObjectByType<DungeonAdventurerSpawner>();
        if (spawner != null && spawner.IsSpawning) return;      // 冒険者が入り切るまで待つ
        if (ZombieAI.GetLivingGuardian() != null) return;       // 門番生存中は突破不可

        // 下り階段(=このフロアのボスセル)に踏破者が到達したら降下
        Vector2Int stairs = grid.BossCell;
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None))
        {
            if (a == null || a.IsRetreating) continue;
            if (a.AdventurerPurpose != AdventurerAI.Purpose.Conquer) continue;
            if (grid.WorldToGrid(a.transform.position) == stairs) { Descend(); return; }
        }
    }

    private void Descend()
    {
        Refs();
        int next = current + 1;
        if (next >= floors.Count) return;

        // 生存者(退却中でない)を集め、退却中の者は報酬清算して退場
        var survivors = new List<AdventurerAI>();
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None))
        {
            if (a == null) continue;
            if (a.IsRetreating) a.ForceDespawnWithReward();
            else survivors.Add(a);
        }

        if (fm != null) fm.DespawnDefenders();  // 現フロアの防衛体を撤収
        current = next;
        if (ui != null) ui.PlayFloorTransition();   // 🎬 降下の暗転フェード
        ActivateFloor(next);                        // 次フロアを構築（最下層なら魔王が実在）

        Vector2Int ent = grid.EntranceCell;
        foreach (var a in survivors) if (a != null) a.RelocateTo(ent); // 生存者を次フロア入口へ
        if (fm != null) fm.SpawnDefendersForActiveFloor();             // 次フロアの防衛体をスポーン

        if (ui != null) ui.ShowDescentToast(FloorLabel(current), survivors.Count); // 🎬 降下トースト
        Debug.Log($"🚶⬇【突破】B{current + 1}F へ降下（生存者 {survivors.Count} / {(IsDeepest(current) ? "最下層・魔王" : "通常")}）");
    }

    // ▼ 下り階段マーカー：非最下層のボスセル(降下地点)に表示、最下層は非表示
    private void UpdateStairsMarker()
    {
        if (grid == null) return;
        if (stairsMarker == null) stairsMarker = BuildStairsMarker();
        bool show = floors.Count > 0 && !IsDeepest(current);
        stairsMarker.SetActive(show);
        if (show)
        {
            var c = grid.BossCell;
            // セル中央からやや右下にオフセット（ボス"B"マーカーと重ならないように）
            stairsMarker.transform.position = grid.GridToWorld(c.x, c.y) + new Vector3(0.28f, -0.28f, -0.6f);
        }
    }

    private GameObject BuildStairsMarker()
    {
        var go = new GameObject("StairsMarker");
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * 0.5f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSprite(); sr.color = new Color(0.30f, 0.80f, 0.95f, 0.9f); sr.sortingOrder = 58;
        var t = new GameObject("Arrow");
        t.transform.SetParent(go.transform, false);
        t.transform.localPosition = new Vector3(0, 0, -0.1f);
        t.transform.localScale = Vector3.one * 0.16f;
        var tm = t.AddComponent<TextMesh>();
        tm.text = "▼"; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.fontSize = 48; tm.characterSize = 0.5f; tm.color = Color.white; tm.fontStyle = FontStyle.Bold;
        var mr = tm.GetComponent<MeshRenderer>(); if (mr != null) mr.sortingOrder = 59;
        return go;
    }

    private static Sprite _sq;
    private Sprite SquareSprite()
    {
        if (_sq == null)
        {
            var tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.white); tex.Apply();
            _sq = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        }
        return _sq;
    }
}
