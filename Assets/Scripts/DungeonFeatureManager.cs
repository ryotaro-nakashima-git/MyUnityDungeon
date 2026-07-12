using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 生成済み迷宮の上に「主要要素（トーテム/スポナー/ボス/特殊敵）」を手動配置するマネージャ。
/// - 歩けるマスに色マーカーで配置（歩行判定は変えない＝AIはそのまま通る）
/// - トーテム：隣接部屋の魅力を強化 / スポナー：戦闘中に防衛ゾンビを定期湧き
///   ボス：そのマスをBossCellにして戦闘開始時に強化防衛体 / 特殊敵：戦闘開始時に精鋭防衛体
/// </summary>
public class DungeonFeatureManager : MonoBehaviour
{
    public enum FeatureType { Totem, Spawner, Boss, SpecialEnemy, Squad, Trap, BaitChest }

    [Header("Costs")]
    [SerializeField] private int totemCostDP = 150;
    [SerializeField] private int spawnerCostDP = 250;
    [SerializeField] private int bossCostDP = 400;
    [SerializeField] private int specialMaterialCost = 3;

    [Header("Effects")]
    [Tooltip("トーテムが隣接部屋の魅力に加える値")]
    [SerializeField] private float totemAttractionBonus = 20f;
    [Tooltip("スポナーが防衛ゾンビを湧かせる間隔(秒)")]
    [SerializeField] private float spawnerInterval = 6f;
    [Tooltip("スポナー1基が1ウェーブで湧かせる最大数")]
    [SerializeField] private int spawnerMaxPerWave = 5;

    [Header("Defender Empower")]
    [SerializeField] private float bossHpMult = 3.0f, bossAtkMult = 2.0f;
    [SerializeField] private float specialHpMult = 1.8f, specialAtkMult = 1.5f;
    [Tooltip("防衛体が配置セルから徘徊できる半径（冒険者を追ってスポーン地点へ行かないための制限）")]
    [SerializeField] private int defenderLeashRadius = 3;

    [Header("Totem Combat Buff (3層バフ・範囲層)")]
    [Tooltip("トーテムが防衛体を強化する半径（マンハッタン距離）")]
    [SerializeField] private int totemBuffRadius = 4;
    [Tooltip("範囲内トーテム1基ごとの防衛体強化率")]
    [SerializeField] private float totemDefenderBuffPer = 0.15f;
    [Tooltip("トーテム強化の最大重ね掛け数")]
    [SerializeField] private int totemBuffMaxStack = 2;

    // 🧟 配下選択：ロスター(MinionCatalog)のインデックスで管理。配置要素にこのindexが記録され、
    //     召喚時に Def(hp/atk/spd/役割) ＋ 家系プロファイル ＋ 魔王相性 が層で乗る。
    private int selectedMinionIndex = 0; // 既定＝カタログ先頭(スケルトン)
    public int SelectedMinionIndex => selectedMinionIndex;
    public MinionCatalog.MinionDef SelectedMinion => MinionCatalog.Get(selectedMinionIndex);
    public ZombieAI.Species SelectedSpecies => MinionCatalog.Get(selectedMinionIndex).family; // 家系(相性/リグ)はindexから導出

    // 🗂️ 図鑑から個体を直接選ぶ（将来のBloodlines図鑑UI用）
    public void SetSelectedMinion(int index)
    {
        selectedMinionIndex = Mathf.Clamp(index, 0, MinionCatalog.Count - 1);
        var d = MinionCatalog.Get(selectedMinionIndex);
        Debug.Log($"🧟【配下】{d.jpName}（{SpeciesName(d.family)}/{MinionCatalog.RoleName(d.role)}・T{d.tierCP}）を選択");
    }
    // 後方互換：既存の種族ボタン(不死0/獣1/魔族2)は、そのファミリーの代表(先頭)種を選ぶ
    public void SetSelectedSpecies(int i)
    {
        var fam = (ZombieAI.Species)Mathf.Clamp(i, 0, 2);
        for (int k = 0; k < MinionCatalog.Count; k++)
            if (MinionCatalog.Get(k).family == fam) { SetSelectedMinion(k); return; }
    }

    // ============ 🛡️ 部隊(Squad)編成（CDO2の部屋スロット編成×Civ隣接） ============
    // 図鑑から最大 SquadMaxSlots 体を編成し、1セルに「部隊」として配置。役割が多様なほど部隊全体にバフ。
    public const int SquadMaxSlots = 5;
    [Header("Undead Raise (不死の再生成)")]
    [SerializeField] private float raisedHpMult = 0.4f, raisedAtkMult = 0.4f;
    private int skeletonCatalogIndex = -1;

    [Header("Squad (部隊編成)")]
    [Tooltip("編成のティア合計DPに掛ける係数")]
    [SerializeField] private float squadCostPerTier = 10f;
    [Tooltip("役割1種ごとの部隊バフ（distinct-1 に乗算）")]
    [SerializeField] private float squadRoleBonusPer = 0.10f;
    [Tooltip("満員(SquadMaxSlots)時の人海戦術ボーナス")]
    [SerializeField] private float squadFullBonus = 0.15f;

    private readonly List<int> currentSquad = new List<int>();
    public IReadOnlyList<int> CurrentSquad => currentSquad;

    // 🎯 配置する隊員（currentSquadのスロット）。「部隊」ツール＋ストリップで選択、マスクリックで個別配置。
    private int squadPlaceSlot = 0;
    public int SquadPlaceSlot => squadPlaceSlot;
    public void SetSquadPlaceSlot(int i) { squadPlaceSlot = Mathf.Max(0, i); }

    public bool SquadAdd(int catalogIndex)
    {
        if (currentSquad.Count >= SquadMaxSlots) { Debug.LogWarning($"⚠️ 部隊は最大{SquadMaxSlots}枠です。"); return false; }
        currentSquad.Add(Mathf.Clamp(catalogIndex, 0, MinionCatalog.Count - 1));
        return true;
    }
    public void SquadRemoveAt(int slot)
    {
        if (slot >= 0 && slot < currentSquad.Count) currentSquad.RemoveAt(slot);
        if (squadPlaceSlot >= currentSquad.Count) squadPlaceSlot = Mathf.Max(0, currentSquad.Count - 1);
    }
    public void SquadClear() { currentSquad.Clear(); squadPlaceSlot = 0; }

    // 編成合計コスト（目安表示用）
    public int SquadCost(IReadOnlyList<int> squad = null)
    {
        var s = squad ?? currentSquad; int sum = 0;
        for (int i = 0; i < s.Count; i++) sum += SquadMemberCost(s[i]);
        return sum;
    }
    // 隊員1体あたりの配置コスト（ティア×係数×種族コスト補正）
    public int SquadMemberCost(int catalogIndex)
    {
        float mult = DemonLord.Instance != null ? DemonLord.Instance.DefenderCostMult : 1f;
        return Mathf.RoundToInt(MinionCatalog.Get(catalogIndex).tierCP * squadCostPerTier * mult);
    }
    public int SquadDistinctRoles(IReadOnlyList<int> squad = null)
    {
        var s = squad ?? currentSquad;
        var roles = new HashSet<MinionCatalog.Role>();
        for (int i = 0; i < s.Count; i++) roles.Add(MinionCatalog.Get(s[i]).role);
        return roles.Count;
    }
    // 役割多様性バフ：distinct役割ごと +squadRoleBonusPer、満員で +squadFullBonus
    public float SquadCompMult(IReadOnlyList<int> squad = null)
    {
        var s = squad ?? currentSquad;
        if (s == null || s.Count == 0) return 1f;
        float mult = 1f + squadRoleBonusPer * (SquadDistinctRoles(s) - 1);
        if (s.Count >= SquadMaxSlots) mult += squadFullBonus;
        return mult;
    }

    private DungeonGridSystem grid;
    private readonly System.Collections.Generic.List<GameObject> spawnedDefenders = new System.Collections.Generic.List<GameObject>();
    private GameObject zombiePrefab;
    private bool wasBattle = false;

    private class Feature
    {
        public FeatureType type;
        public Vector2Int cell;
        public GameObject marker;
        public float spawnTimer;
        public int spawnedThisWave;
        public List<Vector2Int> buffedNeighbors;
        public int minionIndex; // 🧟 この要素が召喚する配下ロスターのindex
        public float squadComp = 1f; // 🛡️ Squad隊員型のみ：編成の役割コンプ倍率スナップショット
        public int trapKind;    // 🪤 Trap型のみ：罠の種類(TrapKind)
    }
    private readonly Dictionary<Vector2Int, Feature> features = new Dictionary<Vector2Int, Feature>();

    private static readonly Color TEAL = new Color(0.34f, 0.76f, 0.67f);
    private static readonly Color VIOLET = new Color(0.71f, 0.55f, 0.90f);
    private static readonly Color CRIMSON = new Color(0.87f, 0.35f, 0.35f);
    private static readonly Color GOLD = new Color(0.89f, 0.66f, 0.29f);
    private static readonly Color STEEL = new Color(0.55f, 0.72f, 0.90f); // 🛡️ 部隊

    private void Start()
    {
        grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        var input = Object.FindFirstObjectByType<GridInputHandler>();
        if (input != null) zombiePrefab = input.ZombiePrefab;
    }

    private void Update()
    {
        var turn = DungeonTurnManager.Instance;
        bool nowBattle = turn != null && turn.IsBattlePhase;

        if (nowBattle && !wasBattle) OnBattleStart();
        if (!nowBattle && wasBattle) OnBattleEnd();
        if (nowBattle) TickSpawners();
        wasBattle = nowBattle;
    }

    // ============ 配置 / 撤去 ============
    public bool TryPlaceFeature(Vector2Int cell, FeatureType type)
    {
        if (grid == null) grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (grid == null) return false;

        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase)
        {
            Debug.LogWarning("⚠️ 要素の配置は準備フェーズのみ可能です。");
            return false;
        }
        if (grid.GetTileType(cell.x, cell.y) == DungeonGridSystem.TileType.None)
        {
            Debug.LogWarning("⚠️ 壁には配置できません（歩けるマスに配置してください）。");
            return false;
        }
        if (features.ContainsKey(cell))
        {
            Debug.LogWarning("⚠️ そのマスには既に要素があります。");
            return false;
        }
        if (type == FeatureType.Boss && HasBoss())
        {
            Debug.LogWarning("⚠️ ボスエリアは1つまでです（将来は1階層につき1つ）。");
            return false;
        }

        // コスト支払い
        var res = DungeonResourceManager.Instance;
        if (type == FeatureType.SpecialEnemy)
        {
            if (res != null && !res.TrySpendMaterial(specialMaterialCost)) return false;
        }
        else
        {
            if (res != null && !res.TrySpendDP(CostOf(type))) return false;
        }

        AddFeature(cell, type, selectedMinionIndex);
        Debug.Log($"🧩【配置】{TypeName(type)} を {cell} に配置しました。");
        return true;
    }

    // 🛡️ 選択中の隊員(squadPlaceSlot)を1セルに個別配置。役割コンプは編成全体から算出しスナップショット。
    public bool TryPlaceSquadMember(Vector2Int cell)
    {
        if (grid == null) grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (grid == null) return false;
        if (currentSquad.Count == 0) { Debug.LogWarning("⚠️ 部隊が空です。図鑑で配下を編成してください。"); return false; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 配置は準備フェーズのみ可能です。"); return false; }
        if (grid.GetTileType(cell.x, cell.y) == DungeonGridSystem.TileType.None) { Debug.LogWarning("⚠️ 壁には配置できません。"); return false; }
        if (features.ContainsKey(cell)) { Debug.LogWarning("⚠️ そのマスには既に要素があります。"); return false; }

        int slot = Mathf.Clamp(squadPlaceSlot, 0, currentSquad.Count - 1);
        int member = currentSquad[slot];
        int cost = SquadMemberCost(member);
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(cost)) return false;

        float comp = SquadCompMult(); // 編成全体の役割コンプを各隊員に付与
        AddFeature(cell, FeatureType.Squad, member, comp);
        Debug.Log($"🛡️【隊員配置】{MinionCatalog.Get(member).jpName}（部隊バフ×{comp:0.00}）を {cell} に配置（-{cost}DP）");
        return true;
    }

    // 🪤 罠の種類選択（配置バー）。通常罠は常時、状態異常罠は領域研究で解禁。
    private int selectedTrapKind = 0;
    public int SelectedTrapKind => selectedTrapKind;
    public void SetSelectedTrapKind(int k) { selectedTrapKind = Mathf.Clamp(k, 0, TrapCatalog.Count - 1); }

    // 🪤 現在選択中の罠を配置。処理はStep1どおりRoomDataタイル（盗賊のMP解除・クールダウン）＋種類で状態異常。
    //     要素として登録するので、フロア切替/侵略開始でexport/importに乗り永続化される（消失バグ修正）。
    public bool TryPlaceTrap(Vector2Int cell)
    {
        if (grid == null) grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (grid == null) return false;
        if (!TrapCatalog.IsUnlocked(selectedTrapKind)) { Debug.LogWarning("⚠️ その罠は領域研究で未解禁です。"); return false; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 配置は準備フェーズのみ可能です。"); return false; }
        if (grid.GetTileType(cell.x, cell.y) == DungeonGridSystem.TileType.None) { Debug.LogWarning("⚠️ 壁には配置できません。"); return false; }
        if (features.ContainsKey(cell)) { Debug.LogWarning("⚠️ そのマスには既に要素があります。"); return false; }
        int cost = TrapCatalog.Get(selectedTrapKind).dpCost;
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(cost)) return false;
        AddFeature(cell, FeatureType.Trap, 0, 1f, selectedTrapKind);
        Debug.Log($"🪤【罠配置】{TrapCatalog.Get(selectedTrapKind).name} を {cell} に配置（-{cost}DP）");
        return true;
    }

    // 罠タイルを敷いて RoomData に種類/ダメージを設定（配置・復元共通）
    private void StampTrapTile(Feature f)
    {
        var go = grid.StampTile(f.cell.x, f.cell.y, DungeonGridSystem.TileType.Trap);
        if (go == null) return;
        var rd = go.GetComponent<RoomData>();
        if (rd != null) { var d = TrapCatalog.Get(f.trapKind); rd.damageValue = d.damage; rd.trapKind = f.trapKind; }
    }

    // 🎣 錬成研究「宝箱の任意配置」：拾得装備(素材)＋DPで、任意の場所に集客の高いbait宝箱を作る。
    [Header("Bait Chest (誘導・宝箱手動配置)")]
    [SerializeField] private int baitChestDPCost = 200;
    [SerializeField] private int baitChestMaterialCost = 2;

    public bool TryPlaceBaitChest(Vector2Int cell)
    {
        if (grid == null) grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (grid == null) return false;
        if (!ResearchState.IsResearched("r_baitchest")) { Debug.LogWarning("⚠️ 宝箱の任意配置は錬成研究で未解禁です。"); return false; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 配置は準備フェーズのみ可能です。"); return false; }
        if (grid.GetTileType(cell.x, cell.y) == DungeonGridSystem.TileType.None) { Debug.LogWarning("⚠️ 壁には配置できません。"); return false; }
        if (features.ContainsKey(cell)) { Debug.LogWarning("⚠️ そのマスには既に要素があります。"); return false; }
        var res = DungeonResourceManager.Instance;
        if (res != null)
        {
            if (res.CraftMaterials < baitChestMaterialCost) { Debug.LogWarning($"⚠️ 素材(拾得装備)が不足（要{baitChestMaterialCost}）。"); return false; }
            if (!res.TrySpendDP(baitChestDPCost)) return false;
            res.TrySpendMaterial(baitChestMaterialCost);
        }
        AddFeature(cell, FeatureType.BaitChest, 0);
        Debug.Log($"🎣【宝箱配置】誘導用の宝箱を {cell} に作成（-{baitChestDPCost}DP -{baitChestMaterialCost}素材）");
        return true;
    }

    private void StampBaitChest(Feature f)
    {
        var go = grid.StampTile(f.cell.x, f.cell.y, DungeonGridSystem.TileType.TreasureChest);
        if (go == null) return;
        var rd = go.GetComponent<RoomData>();
        if (rd != null) { rd.isBait = true; rd.joyValue = 12f; } // 集客(attraction)はStartでisBait→80、richなのでloot/gearも多い
    }

    // 実際の配置処理（マーカー生成/トーテム効果/ボスセル更新/辞書登録）。コスト・フェーズ判定は呼び出し側。
    private Feature AddFeature(Vector2Int cell, FeatureType type, int minionIndex, float squadComp = 1f, int trapKind = 0)
    {
        var f = new Feature { type = type, cell = cell, minionIndex = minionIndex, squadComp = squadComp, trapKind = trapKind };
        if (type == FeatureType.Trap) StampTrapTile(f);          // 🪤 罠はタイル自体が見た目（マーカーなし）
        else if (type == FeatureType.BaitChest) StampBaitChest(f); // 🎣 宝箱もタイル自体が見た目
        else f.marker = CreateMarker(cell, type);
        if (type == FeatureType.Totem) ApplyTotem(f);
        if (type == FeatureType.Boss) grid.SetBossCell(cell);
        features[cell] = f;
        return f;
    }

    // ============ フロア切替用：要素の退避/復元 ============
    public struct FeatureRecord { public FeatureType type; public Vector2Int cell; public int minionIndex; public float squadComp; public int trapKind; }

    public List<FeatureRecord> ExportFeatures()
    {
        var list = new List<FeatureRecord>();
        foreach (var f in features.Values)
            list.Add(new FeatureRecord { type = f.type, cell = f.cell, minionIndex = f.minionIndex, squadComp = f.squadComp, trapKind = f.trapKind });
        return list;
    }

    public void ImportFeatures(List<FeatureRecord> recs)
    {
        ClearAllFeatures();
        if (recs == null) return;
        if (grid == null) grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        foreach (var r in recs)
        {
            if (grid != null && grid.GetTileType(r.cell.x, r.cell.y) == DungeonGridSystem.TileType.None) continue; // 壁化したマスはスキップ
            AddFeature(r.cell, r.type, r.minionIndex, r.squadComp <= 0f ? 1f : r.squadComp, r.trapKind);
        }
    }

    public void RemoveFeature(Vector2Int cell)
    {
        if (!features.TryGetValue(cell, out var f)) return;
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) return; // 撤去も準備中のみ

        if (f.type == FeatureType.Totem) UndoTotem(f);
        if (f.type == FeatureType.Trap || f.type == FeatureType.BaitChest) grid.StampTile(f.cell.x, f.cell.y, DungeonGridSystem.TileType.Room); // 🪤🎣 タイルを床へ戻す
        if (f.marker != null) Destroy(f.marker);

        // 50%返金（素材要素は返金なし）
        var res = DungeonResourceManager.Instance;
        if (res != null && f.type != FeatureType.SpecialEnemy)
        {
            int refund = f.type == FeatureType.Squad ? SquadMemberCost(f.minionIndex)
                : f.type == FeatureType.Trap ? TrapCatalog.Get(f.trapKind).dpCost
                : f.type == FeatureType.BaitChest ? baitChestDPCost
                : CostOf(f.type);
            res.RefundDP(refund, true);
        }

        features.Remove(cell);
        Debug.Log($"🧩【撤去】{TypeName(f.type)} を {cell} から撤去しました。");
    }

    // 🗺️ 階層拡張で配置を破棄する際の返金（各要素の50%DP。素材要素は返金なし）
    public void RefundRecords(List<FeatureRecord> recs)
    {
        if (recs == null || DungeonResourceManager.Instance == null) return;
        int refund = 0;
        foreach (var r in recs)
        {
            if (r.type == FeatureType.SpecialEnemy) continue;
            int cost = r.type == FeatureType.Squad ? SquadMemberCost(r.minionIndex)
                : r.type == FeatureType.Trap ? TrapCatalog.Get(r.trapKind).dpCost
                : r.type == FeatureType.BaitChest ? baitChestDPCost
                : CostOf(r.type);
            refund += cost / 2;
        }
        if (refund > 0) DungeonResourceManager.Instance.AddDP(refund);
    }

    public void ClearAllFeatures()
    {
        foreach (var kv in features)
        {
            if (kv.Value.type == FeatureType.Totem) UndoTotem(kv.Value);
            if (kv.Value.marker != null) Destroy(kv.Value.marker);
        }
        features.Clear();
    }

    // ============ 戦闘連動 ============
    private void OnBattleStart()
    {
        // 🏢 複数フロア時はフロアマネージャが降下ごとにスポーンを駆動する（ここでは何もしない）
        if (DungeonFloorManager.Instance != null) return;
        SpawnDefendersForActiveFloor();
    }

    // 現在アクティブなフロアの配置要素から防衛体をスポーンする（フロアマネージャ/自動検出の両方から呼ばれる）
    public void SpawnDefendersForActiveFloor()
    {
        foreach (var f in features.Values)
        {
            f.spawnTimer = 0f;
            f.spawnedThisWave = 0;
            if (f.type == FeatureType.Boss) SpawnDefender(f.cell, bossHpMult, bossAtkMult, CRIMSON, f.minionIndex, true); // 門番
            else if (f.type == FeatureType.SpecialEnemy) SpawnDefender(f.cell, specialHpMult, specialAtkMult, GOLD, f.minionIndex);
            else if (f.type == FeatureType.Squad) SpawnDefender(f.cell, 1f, 1f, STEEL, f.minionIndex, false, f.squadComp); // 🛡️ 隊員（役割コンプ倍率付き）
        }
    }

    // このフロアの防衛体を全撤収（降下時/戦闘終了時）
    public void DespawnDefenders()
    {
        foreach (var go in spawnedDefenders) if (go != null) Destroy(go);
        spawnedDefenders.Clear();
    }

    private void TickSpawners()
    {
        foreach (var f in features.Values)
        {
            if (f.type != FeatureType.Spawner) continue;
            if (f.spawnedThisWave >= spawnerMaxPerWave) continue;
            f.spawnTimer += Time.deltaTime;
            if (f.spawnTimer >= spawnerInterval)
            {
                f.spawnTimer = 0f;
                f.spawnedThisWave++;
                SpawnDefender(f.cell, 1f, 1f, null, f.minionIndex);
            }
        }
    }

    private ZombieAI SpawnDefender(Vector2Int cell, float hpMult, float atkMult, Color? tint, int minionIndex, bool guardian = false, float squadMult = 1f)
    {
        if (zombiePrefab == null)
        {
            var input = Object.FindFirstObjectByType<GridInputHandler>();
            if (input != null) zombiePrefab = input.ZombiePrefab;
        }
        if (zombiePrefab == null || grid == null) return null;

        var def = MinionCatalog.Get(minionIndex);   // 🧟 配下個体の定義（役割/hp・atk・spd倍率）
        var species = def.family;                    // 家系（相性/プロファイル/リグ）

        var go = Instantiate(zombiePrefab, grid.GridToWorld(cell.x, cell.y), Quaternion.identity);
        var z = go.GetComponent<ZombieAI>();
        if (z != null)
        {
            // 🧱 バフ合成：要素役割(ボス/特殊/スポナー) × 興奮ツリー × 遺物(全体) × トーテム(範囲) × 家系プロファイル × 相性 × 個体Def
            float pm = EmotionTreeManager.Instance != null ? EmotionTreeManager.Instance.DefenderPowerMult : 1f; // 🌟 興奮ツリー
            float relicHp = RelicManager.Instance != null ? RelicManager.Instance.DefenderHpMult : 1f;          // 🏺 遺物
            float relicAtk = RelicManager.Instance != null ? RelicManager.Instance.DefenderAtkMult : 1f;
            float totem = TotemDefenderBuff(cell);                                                              // 🗿 トーテム範囲
            var prof = SpeciesProfile(species);                                                                 // 🐺 家系プロファイル
            float aff = DemonLord.Instance != null ? DemonLord.Instance.DefenderAffinityMult(species) : 1f;     // 🧬 種族相性

            z.species = species;
            z.minionIndex = minionIndex;             // 🗂️ 図鑑index（部屋編成/種族個性で将来使用）
            z.role = def.role;
            // 家系プロファイル(family) × 個体Def × 部隊コンプ を層で合成（二重計上でなく意図的な階層）
            z.hpMult = hpMult * pm * relicHp * totem * prof.hp * aff * def.hpMult * squadMult;
            z.atkMult = atkMult * pm * relicAtk * totem * prof.atk * aff * def.atkMult * squadMult;
            z.speedMult = def.spdMult;
            z.isGuardian = guardian;
            // 🛡️ 配置セルをアンカーにしたガードモード（スポーン地点まで追わない）
            z.anchored = true; z.anchorCell = cell; z.leashRadius = defenderLeashRadius;
            // 色：ボス/特殊敵は識別色を優先、スポナーは種族色
            z.overrideTint = true; z.tintColor = tint ?? prof.tint;
        }
        spawnedDefenders.Add(go);
        return z;
    }

    // 🪦 不死の機械的個性：とどめを刺された不死の位置に弱い骸(スケルトン)を1体再生成（連鎖しない）
    public void RaiseUndead(Vector2Int cell)
    {
        if (grid == null) grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (zombiePrefab == null)
        {
            var input = Object.FindFirstObjectByType<GridInputHandler>();
            if (input != null) zombiePrefab = input.ZombiePrefab;
        }
        if (zombiePrefab == null || grid == null) return;
        if (skeletonCatalogIndex < 0)
            for (int k = 0; k < MinionCatalog.Count; k++) if (MinionCatalog.Get(k).id == "skeleton") { skeletonCatalogIndex = k; break; }
        if (skeletonCatalogIndex < 0) return;
        var z = SpawnDefender(cell, raisedHpMult, raisedAtkMult, new Color(0.5f, 0.9f, 0.6f), skeletonCatalogIndex, false, 1f);
        if (z != null) z.isRaised = true; // 再生成体は連鎖再生成しない
        BattleVfx.Burst(grid.GridToWorld(cell.x, cell.y), new Color(0.5f, 0.9f, 0.6f, 1f), 0.9f);
    }

    // 🗿 配置セルの周囲 totemBuffRadius 内にあるトーテム基数から強化倍率を算出（最大 totemBuffMaxStack 重ね）
    private float TotemDefenderBuff(Vector2Int cell)
    {
        int n = 0;
        foreach (var f in features.Values)
        {
            if (f.type != FeatureType.Totem) continue;
            if (Mathf.Abs(f.cell.x - cell.x) + Mathf.Abs(f.cell.y - cell.y) <= totemBuffRadius) n++;
        }
        n = Mathf.Min(n, totemBuffMaxStack);
        return 1f + totemDefenderBuffPer * n;
    }

    // 🐺 種族プロファイル（不死=硬い/獣=攻撃的/魔族=バランス）＋識別色
    private (float hp, float atk, Color tint) SpeciesProfile(ZombieAI.Species s)
    {
        switch (s)
        {
            case ZombieAI.Species.Beast: return (0.90f, 1.25f, new Color(0.90f, 0.55f, 0.25f));   // 獣＝橙
            case ZombieAI.Species.Demonkin: return (1.05f, 1.10f, new Color(0.70f, 0.45f, 0.90f)); // 魔族＝紫
            default: return (1.25f, 0.90f, new Color(0.45f, 0.85f, 0.55f));                         // 不死＝緑
        }
    }
    public static string SpeciesName(ZombieAI.Species s)
    {
        switch (s) { case ZombieAI.Species.Beast: return "獣"; case ZombieAI.Species.Demonkin: return "魔族"; default: return "不死"; }
    }

    // ⏱️ ターン終了(戦闘→準備)で、この防衛体を消滅させる（次ターン開始時に初期位置へ再配置＝位置リセット/重複防止）
    private void OnBattleEnd()
    {
        DespawnDefenders();
    }

    // ============ トーテム効果 ============
    private void ApplyTotem(Feature f)
    {
        f.buffedNeighbors = new List<Vector2Int>();
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in dirs)
        {
            Vector2Int n = f.cell + d;
            var obj = grid.GetGridObject(n.x, n.y);
            if (obj == null) continue;
            var rd = obj.GetComponent<RoomData>();
            if (rd != null)
            {
                rd.attraction += totemAttractionBonus;
                f.buffedNeighbors.Add(n);
            }
        }
    }
    private void UndoTotem(Feature f)
    {
        if (f.buffedNeighbors == null) return;
        foreach (var n in f.buffedNeighbors)
        {
            var obj = grid.GetGridObject(n.x, n.y);
            if (obj == null) continue;
            var rd = obj.GetComponent<RoomData>();
            if (rd != null) rd.attraction -= totemAttractionBonus;
        }
        f.buffedNeighbors = null;
    }

    // ============ ヘルパー ============
    public int CostOf(FeatureType type)
    {
        int baseCost;
        switch (type)
        {
            case FeatureType.Totem: baseCost = totemCostDP; break;
            case FeatureType.Spawner: baseCost = spawnerCostDP; break;
            case FeatureType.Boss: baseCost = bossCostDP; break;
            default: baseCost = 0; break;
        }
        // 🧬 種族進化の相性でコスト補正（例：ドワーフ0.7 / 吸血0.8）
        float mult = DemonLord.Instance != null ? DemonLord.Instance.DefenderCostMult : 1f;
        return Mathf.RoundToInt(baseCost * mult);
    }
    public int SpecialMaterialCost => specialMaterialCost;
    private bool HasBoss()
    {
        foreach (var f in features.Values) if (f.type == FeatureType.Boss) return true;
        return false;
    }
    private string TypeName(FeatureType t)
    {
        switch (t) { case FeatureType.Totem: return "トーテム"; case FeatureType.Spawner: return "スポナー"; case FeatureType.Boss: return "ボスエリア"; case FeatureType.Squad: return "部隊"; case FeatureType.Trap: return "罠"; case FeatureType.BaitChest: return "宝箱"; default: return "特殊エネミー"; }
    }
    private Color ColorOf(FeatureType t)
    {
        switch (t) { case FeatureType.Totem: return TEAL; case FeatureType.Spawner: return VIOLET; case FeatureType.Boss: return CRIMSON; case FeatureType.Squad: return STEEL; default: return GOLD; }
    }
    private string LetterOf(FeatureType t)
    {
        switch (t) { case FeatureType.Totem: return "T"; case FeatureType.Spawner: return "S"; case FeatureType.Boss: return "B"; case FeatureType.Squad: return "隊"; default: return "E"; }
    }

    private static Sprite _square;
    private Sprite SquareSprite()
    {
        if (_square == null)
        {
            var tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.white); tex.Apply();
            _square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        }
        return _square;
    }
    private GameObject CreateMarker(Vector2Int cell, FeatureType type)
    {
        var go = new GameObject("Feature_" + type);
        go.transform.SetParent(transform, false);
        go.transform.position = grid.GridToWorld(cell.x, cell.y) + new Vector3(0, 0, -0.5f);
        go.transform.localScale = Vector3.one * 0.64f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSprite(); sr.color = ColorOf(type); sr.sortingOrder = 50;

        var txt = new GameObject("Letter");
        txt.transform.SetParent(go.transform, false);
        txt.transform.localPosition = new Vector3(0, 0, -0.1f);
        txt.transform.localScale = Vector3.one * 0.12f;
        var tm = txt.AddComponent<TextMesh>();
        tm.text = LetterOf(type); tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.fontSize = 48; tm.characterSize = 0.5f; tm.color = Color.white; tm.fontStyle = FontStyle.Bold;
        var mr = tm.GetComponent<MeshRenderer>(); if (mr != null) mr.sortingOrder = 51;
        return go;
    }
}
