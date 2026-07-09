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
            var fd = gen.BuildFloorData();
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
        ActivateFloor(i);
    }

    private void ActivateFloor(int i)
    {
        Refs();
        var fd = floors[i];
        grid.BuildFromMap(fd.map, fd.entrance, fd.boss, fd.tint, fd.isDeepest); // 魔王は最下層のみ実在
        if (fm != null) fm.ImportFeatures(fd.features);                          // このフロアの要素を復元
        var cam = Object.FindFirstObjectByType<CameraController>();
        if (cam != null) cam.FitToDungeon();
        Debug.Log($"🔽【フロア切替】B{i + 1}F を表示（{(fd.isDeepest ? "最下層・魔王在" : "通常")}）");
    }

    public string FloorLabel(int i) => "B" + (i + 1) + "F";
}
