using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 生成済み迷宮の上に『主要要素（トーテム/スポナー/ボス/特殊敵）』を手動配置するマネージャ。
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
    [Tooltip("同種トーテムの最大重ね掛け数（半径と効果量は TotemCatalog 側で種類ごとに定義）")]
    [SerializeField] private int totemBuffMaxStack = 2;

    // 🧟 配下選択：ロスター(MinionCatalog)のインデックスで管理。配置要素にこのindexが記録され、
    //     召喚時に Def(hp/atk/spd/役割) ＋ 家系プロファイル ＋ 魔王相性 が層で乗る。
    private int selectedMinionIndex = 0; // 既定＝カタログ先頭(スケルトン)
    public int SelectedMinionIndex => selectedMinionIndex;

    // 👾 特殊エネミーの種類(GddMap.Special index 0-5)。特殊敵ツールのストリップで選択。
    // 👾 特殊敵＝ユニーク魔物。**種類ではなく「持っている個体」を選んで置く**。
    //    ⚠ 旧仕様は GddMap の見た目6種から選ぶだけで、レベルも装備も持てなかった。
    //      いまは所持している個体を置くので、育てた1体がそのまま盤に立つ。
    private int selectedUniqueId = -1;
    public int SelectedUniqueId => selectedUniqueId;
    public void SetSelectedUniqueId(int individualId) { selectedUniqueId = individualId; }
    /// <summary>置ける（＝未配置・隊に入っていない）ユニーク個体の先頭。無ければ -1。</summary>
    public int FirstPlaceableUnique()
    {
        foreach (var v in MinionRoster.Uniques())
            if (!IsIndividualPlaced(v.id) && !IsIndividualInAnySquad(v.id) && !KinRoster.IsAwayFromDungeon(v.id)) return v.id;
        return -1;
    }
    public MinionCatalog.MinionDef SelectedMinion => MinionCatalog.Get(selectedMinionIndex);
    public ZombieAI.Species SelectedSpecies => MinionCatalog.Get(selectedMinionIndex).family; // 家系(相性/リグ)はindexから導出

    // 🗂️ 図鑑から個体を直接選ぶ（将来のBloodlines図鑑UI用）
    public void SetSelectedMinion(int index)
    {
        selectedMinionIndex = Mathf.Clamp(index, 0, MinionCatalog.Count - 1);
        var d = MinionCatalog.Get(selectedMinionIndex);
        Debug.Log($"🧟『配下』{d.jpName}（{SpeciesName(d.family)}/{MinionCatalog.RoleName(d.role)}・T{d.tierCP}）を選択");
    }
    // 後方互換：既存の種族ボタン(不死0/獣1/魔族2)は、そのファミリーの代表(先頭)種を選ぶ
    public void SetSelectedSpecies(int i)
    {
        var fam = (ZombieAI.Species)Mathf.Clamp(i, 0, 2);
        for (int k = 0; k < MinionCatalog.Count; k++)
            if (MinionCatalog.Get(k).family == fam) { SetSelectedMinion(k); return; }
    }

    // ============ 🛡️ 部隊(Squad)編成（CDO2の部屋スロット編成×Civ隣接） ============
    // 図鑑から最大 SquadMaxSlots 体を編成し、1セルに『部隊』として配置。役割が多様なほど部隊全体にバフ。
    /// <summary>
    /// 隊の枠。⚠ **const だったので研究『部隊枠 +1』(m_slot) が一生反映されていなかった。**
    /// 研究で伸びる値を const にしてはいけない（コンパイル時に焼き込まれる）。
    /// </summary>
    public static int SquadMaxSlots => 5 + (ResearchState.IsResearched("m_slot") ? 1 : 0)
                                      + (ResearchState.IsResearched("m_slot2") ? 1 : 0)   // ⚠ こちらも配線漏れだった
                                      + PolicySystem.SquadSlotBonus + AttributeSystem.SquadSlotBonus;   // 🏛️ 政策『総動員』／🎖️ 属性『軍制』
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

    // 🏢 階層ごとの部隊編成。中身は『個体ID(MinionRoster.Individual.id)』＝種類ではなく実体で組む。
    //    1個体は1つの隊にしか所属できない（実体が1つしかないため）。フロア切替でCurrentSquadが切り替わる。
    //    ⚠ `readonly` を外してあるのは意図的。[[SaveSystem]] は **readonly を「カタログ＝保存しない」の目印**に
    //       使っているので、readonly のままだと部隊編成がセーブに乗らない。
    private Dictionary<int, List<int>> squadByFloor = new Dictionary<int, List<int>>();
    private static DungeonFloorManager _floorMgrCache;
    private static DungeonFloorManager FloorMgr
    {
        get
        {
            if (DungeonFloorManager.Instance != null) return DungeonFloorManager.Instance;
            if (_floorMgrCache == null) _floorMgrCache = Object.FindFirstObjectByType<DungeonFloorManager>();
            return _floorMgrCache;
        }
    }
    private static int ActiveFloorIndex { get { var fm = FloorMgr; return fm != null ? fm.CurrentFloorIndex : 0; } }
    private List<int> SquadOf(int floor)
    {
        if (!squadByFloor.TryGetValue(floor, out var l)) { l = new List<int>(); squadByFloor[floor] = l; }
        return l;
    }
    private List<int> CurrentSquadList => SquadOf(ActiveFloorIndex);
    public IReadOnlyList<int> CurrentSquad => CurrentSquadList;   // ← 個体IDのリスト

    // 🎯 配置する隊員（現フロア隊のスロット）。『部隊』ツール＋ストリップで選択、マスクリックで配置。
    private int squadPlaceSlot = 0;
    public int SquadPlaceSlot => squadPlaceSlot;
    public void SetSquadPlaceSlot(int i) { squadPlaceSlot = Mathf.Max(0, i); }

    // 現在選択中の隊員の個体ID（スロット→個体）。UI表示用。
    public int SelectedIndividualId
    {
        get { var s = CurrentSquadList; return (squadPlaceSlot >= 0 && squadPlaceSlot < s.Count) ? s[squadPlaceSlot] : -1; }
    }

    // その個体がいずれかの階の隊に編成済みか（1個体=1隊のため二重編成を防ぐ）
    public bool IsIndividualInAnySquad(int id)
    {
        if (id < 0) return false;
        foreach (var kv in squadByFloor) if (kv.Value.Contains(id)) return true;
        return false;
    }
    // その個体が編成されている階層index（未編成なら-1）
    public int SquadFloorOfIndividual(int id)
    {
        foreach (var kv in squadByFloor) if (kv.Value.Contains(id)) return kv.Key;
        return -1;
    }

    // 指定個体が既にどこかに配置済みか（重複配置防止・ストリップの淡色表示に使う）。
    //   個体は唯一の実体なので、現フロアだけでなく他フロア(退避済み)も横断チェックする。隊員/ボス両方が対象。
    public bool IsIndividualPlaced(int id)
    {
        if (id < 0) return false;
        foreach (var f in features.Values) if (f.individualId == id) return true; // アクティブ層(ライブ)
        var fm = DungeonFloorManager.Instance;
        if (TrainingSystem.IsTraining(id)) return true;                            // 🏋️ 訓練所へ送っている
        if (fm != null && fm.IsIndividualPlacedOnOtherFloors(id)) return true;     // 他フロア(退避済み)
        return false;
    }
    // その種類の『未配置』個体の先頭ID（自動割当用）。無ければ-1。
    public int FirstUnplacedIndividual(int catalogIndex)
    {
        foreach (var v in MinionRoster.ByType(catalogIndex)) if (!IsIndividualPlaced(v.id)) return v.id;
        return -1;
    }
    // 👑 ボスに任命できる先頭ID（未配置かつ どの隊にも入っていない）。無ければ-1。
    public int FirstBossEligibleIndividual(int catalogIndex)
    {
        foreach (var v in MinionRoster.ByType(catalogIndex))
            if (!IsIndividualPlaced(v.id) && !IsIndividualInAnySquad(v.id) && !KinRoster.IsAwayFromDungeon(v.id)) return v.id;
        return -1;
    }

    // 👑 その個体がボスとして任命されている階層index（未任命なら-1）。アクティブ層＋退避済みの他フロアを横断。
    public int BossFloorOfIndividual(int id)
    {
        if (id < 0) return -1;
        foreach (var f in features.Values)
            if (f.type == FeatureType.Boss && f.individualId == id) return ActiveFloorIndex;
        var fm = DungeonFloorManager.Instance;
        return fm != null ? fm.BossFloorOfIndividual(id) : -1;
    }
    // その個体が『ボスに任命されている』か（UIの編成可否表示に使う）
    public bool IsIndividualBoss(int id) => BossFloorOfIndividual(id) >= 0;

    // 👑 ボス任命で選択中の個体（ボスストリップ専用。隊の選択とは独立）
    private int bossPickIndividualId = -1;
    public int BossPickIndividualId => bossPickIndividualId;
    public void SetPlaceIndividual(int id) { bossPickIndividualId = id; }

    // 👑 ボス任命UI用：このフロアにボスが居るか／そのボスの個体ID（無ければ-1）。
    public bool FloorHasBoss() => HasBoss();
    public int CurrentBossIndividualId()
    {
        foreach (var f in features.Values) if (f.type == FeatureType.Boss) return f.individualId;
        return -1;
    }

    // 🧬 個体を現フロアの隊に編成（1個体=1隊）。
    public bool SquadAdd(int individualId)
    {
        var v = MinionRoster.Get(individualId);
        if (v == null) { Debug.LogWarning("⚠️ その個体は存在しません。"); return false; }
        var squad = CurrentSquadList;
        if (squad.Count >= SquadMaxSlots) { Debug.LogWarning($"⚠️ この階の部隊は最大{SquadMaxSlots}枠です。"); return false; }
        int already = SquadFloorOfIndividual(individualId);
        if (already >= 0)
        {
            Debug.LogWarning($"⚠️ {MinionCatalog.Get(v.catalogIndex).jpName} 個体#{individualId} は B{already + 1}F の隊に編成済みです（1個体は1隊のみ）。");
            return false;
        }
        // 🗺️ 眷属／その配下は地上に出ているのでダンジョンの隊には入れられない
        if (KinRoster.IsAwayFromDungeon(individualId))
        {
            Debug.LogWarning($"⚠️ {MinionCatalog.Get(v.catalogIndex).jpName} 個体#{individualId} は地上に出ています（眷属またはその配下）。");
            return false;
        }
        // 👑 ボスに任命済みの個体は隊に入れられない（実体は1つなので役割も1つ）
        int bf = BossFloorOfIndividual(individualId);
        if (bf >= 0)
        {
            Debug.LogWarning($"⚠️ {MinionCatalog.Get(v.catalogIndex).jpName} 個体#{individualId} は B{bf + 1}F のボスです（ボスは隊に編成できません）。");
            return false;
        }
        squad.Add(individualId);
        return true;
    }
    /// <summary>
    /// 編成トレイのスロットから抜く。
    /// ⚠ **必ず `SquadRemoveIndividual` を通す**。ここで `RemoveAt` だけしていたので、
    ///   トレイから抜いたときにマップの配置が残っていた（「隊から外したのに盤にいる」の正体）。
    /// </summary>
    public void SquadRemoveAt(int slot)
    {
        var squad = CurrentSquadList;
        if (slot < 0 || slot >= squad.Count) return;
        SquadRemoveIndividual(squad[slot]);
    }
    // 個体IDで隊から外す（個体タブから使う）
    public void SquadRemoveIndividual(int individualId)
    {
        foreach (var kv in squadByFloor) { int i = kv.Value.IndexOf(individualId); if (i >= 0) { kv.Value.RemoveAt(i); break; } }
        var squad = CurrentSquadList;
        if (squadPlaceSlot >= squad.Count) squadPlaceSlot = Mathf.Max(0, squad.Count - 1);
        RemovePlacedOfIndividual(individualId);   // 🗺️ 隊から外したらマップの配置も解く（置きっぱなしを防ぐ）
    }

    /// <summary>
    /// その個体がマップに置かれていたら撤去する（隊から外したときなど）。
    /// ⚠ **いま開いている階と、退避してある他の階の両方**を消す。
    ///   片方だけだと「1階に置いた個体を3階から外しても1階に残る」→
    ///   さらに別の階の隊に入れられる、という矛盾が起きる（実際に起きた）。
    /// </summary>
    public void RemovePlacedOfIndividual(int individualId)
    {
        if (individualId < 0) return;
        var hit = new List<Vector2Int>();
        foreach (var kv in features)
            if (kv.Value.individualId == individualId
                && (kv.Value.type == FeatureType.Squad || kv.Value.type == FeatureType.Boss)) hit.Add(kv.Key);
        foreach (var cell in hit)
        {
            var f = features[cell];
            if (f.marker != null) Destroy(f.marker);
            features.Remove(cell);
            Debug.Log($"🧩『配置も解除』個体#{individualId} を {cell} から外した（隊から外れたため）");
        }
        var fm = DungeonFloorManager.Instance;
        if (fm != null) fm.RemoveIndividualFromOtherFloors(individualId);
    }
    public void SquadClear() { CurrentSquadList.Clear(); squadPlaceSlot = 0; }

    // 隊員1体あたりの参考コスト（ティア×係数×種族コスト補正）・配置は無償、表示用に残す
    public int SquadMemberCost(int catalogIndex)
    {
        float mult = DemonLord.Instance != null ? DemonLord.Instance.DefenderCostMult : 1f;
        return Mathf.RoundToInt(MinionCatalog.Get(catalogIndex).tierCP * squadCostPerTier * mult);
    }
    // 隊(個体IDリスト)の役割の種類数
    public int SquadDistinctRoles(IReadOnlyList<int> squad = null)
    {
        var s = squad ?? CurrentSquadList;
        var roles = new HashSet<MinionCatalog.Role>();
        for (int i = 0; i < s.Count; i++)
        {
            var v = MinionRoster.Get(s[i]); if (v == null) continue;
            roles.Add(MinionCatalog.Get(v.catalogIndex).role);
        }
        return roles.Count;
    }
    // 役割多様性バフ：distinct役割ごと +squadRoleBonusPer、満員で +squadFullBonus
    public float SquadCompMult(IReadOnlyList<int> squad = null)
    {
        var s = squad ?? CurrentSquadList;
        if (s == null || s.Count == 0) return 1f;
        float mult = 1f + squadRoleBonusPer * (SquadDistinctRoles(s) - 1);
        if (s.Count >= SquadMaxSlots) mult += squadFullBonus;
        mult += DungeonTheme.SquadCompBonus;   // 🏔️ 大空洞は広くて隊が組みやすい
        return mult;
    }

    private DungeonGridSystem grid;
    [System.NonSerialized]   // 💾 場に居る実体。セーブは FloorData の配置記録から組み直す（[[SaveSystem]]）
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
        public int minionIndex; // 🧟 この要素が召喚する配下ロスターのindex（種類）
        public float squadComp = 1f; // 🛡️ Squad隊員型のみ：編成の役割コンプ倍率スナップショット
        public int trapKind;    // 🪤 Trap型のみ：罠の種類(TrapKind)
        public int individualId = -1; // 🧬 Squad隊員型のみ：配置した個体(MinionRoster)のID。Lv育成/重複配置防止に使う
        /// <summary>🕳️ 落とし穴のみ：落とす先。`(-1,-1)`＝**下の階へ**（奈落）。`(-2,-2)`＝行き先未定（配置直後）。</summary>
        public Vector2Int link = PitUnset;
    }
    /// <summary>🕳️ 落とし穴の行き先の特別な値。⚠ セーブに載るので意味を変えない。</summary>
    public static readonly Vector2Int PitBelow = new Vector2Int(-1, -1);
    public static readonly Vector2Int PitUnset = new Vector2Int(-2, -2);
    [System.NonSerialized]   // 💾 マーカー(GameObject)を持つので保存しない。ExportFeatures/ImportFeatures で往復する
    private readonly Dictionary<Vector2Int, Feature> features = new Dictionary<Vector2Int, Feature>();

    private static readonly Color TEAL = new Color(0.34f, 0.76f, 0.67f);
    private static readonly Color VIOLET = new Color(0.71f, 0.55f, 0.90f);
    private static readonly Color CRIMSON = new Color(0.87f, 0.35f, 0.35f);
    private static readonly Color GOLD = new Color(0.89f, 0.66f, 0.29f);
    private static readonly Color STEEL = new Color(0.55f, 0.72f, 0.90f); // 🛡️ 部隊

    // 🗿 トーテムの範囲問い合わせを各所（冒険者/罠/感情）から安く行うための実体キャッシュ
    private static DungeonFeatureManager instance;
    public static DungeonFeatureManager Instance
    {
        get
        {
            if (instance == null) instance = Object.FindFirstObjectByType<DungeonFeatureManager>();
            return instance;
        }
    }

    private void Awake() { instance = this; }

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
        if (type == FeatureType.Totem && !TotemCatalog.IsUnlocked(selectedTotemKind))
        {
            Debug.LogWarning("⚠️ そのトーテムは領域研究で未解禁です。"); return false;
        }
        if (!CheckPlacementCap()) return false;

        // コスト支払い
        var res = DungeonResourceManager.Instance;
        // 👾 ユニークの配置は**隊員と同じく無償**（引き当てた時点で対価は払っている）。
        //    ⚠ 旧仕様は素材を取っていたが、隊員は無償なのに特殊敵だけ有償という不揃いだった。
        int uniqueId = -1;
        if (type == FeatureType.SpecialEnemy)
        {
            uniqueId = selectedUniqueId >= 0 ? selectedUniqueId : FirstPlaceableUnique();
            if (uniqueId < 0) { Debug.LogWarning("⚠️ 置けるユニーク魔物がいません（ガチャで引き当ててください）。"); return false; }
            if (IsIndividualPlaced(uniqueId)) { Debug.LogWarning("⚠️ その個体は既に盤に出ています。"); return false; }
        }
        else
        {
            int cost = type == FeatureType.Totem ? TotemCatalog.Get(selectedTotemKind).dpCost : CostOf(type);
            if (res != null && !res.TrySpendDP(cost)) return false;
        }

        // トーテムは選択中の種類を trapKind に保持（効果に使用）
        int kind = type == FeatureType.Totem ? selectedTotemKind : 0;
        int mi = type == FeatureType.SpecialEnemy ? MinionRoster.Get(uniqueId).catalogIndex : selectedMinionIndex;
        AddFeature(cell, type, mi, 1f, kind, type == FeatureType.SpecialEnemy ? uniqueId : -1);
        string sub = type == FeatureType.SpecialEnemy ? "『" + MinionCatalog.Get(mi).jpName + " #" + uniqueId + "』"
                   : type == FeatureType.Totem ? "『" + TotemCatalog.Name(selectedTotemKind) + "』" : "";
        Debug.Log($"🧩『配置』{TypeName(type)}{sub} を {cell} に配置しました。（{PlacedCount}/{PlacementCap} 枠）");
        return true;
    }

    // 🗿 トーテムの種類選択（配置バー）。基礎3種は常時、それ以外は領域研究で解禁。
    private int selectedTotemKind = 0;
    public int SelectedTotemKind => selectedTotemKind;
    public void SetSelectedTotemKind(int k) { selectedTotemKind = Mathf.Clamp(k, 0, TotemCatalog.Count - 1); }

    // 🏛️ 配置スロット上限（広さ＝防衛の器）。この階層に置ける要素の総数。
    public int PlacedCount => features.Count;
    private int trapsEverPlaced;
    /// <summary>🏅 この周で罠を1つでも置いたか（実績『素手の防衛』）。→ [[Achievements]]</summary>
    public int TrapsEverPlaced => trapsEverPlaced;
    public void ResetRunCounters() { trapsEverPlaced = 0; }
    /// <summary>配置済み個体の並び（UIが「置いたら即暗くする」判定に使う署名）。</summary>
    public string PlacedIndividualsSig()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kv in features) if (kv.Value.individualId >= 0) sb.Append(kv.Value.individualId).Append('.');
        return sb.ToString();
    }
    // 💡 天啓の判定用：この階に置いてあるトーテムの数
    public int TotemCount { get { int n = 0; foreach (var f in features.Values) if (f.type == FeatureType.Totem) n++; return n; } }
    public int PlacementCap => DungeonFloorManager.CurrentPlacementCap;
    private bool CheckPlacementCap()
    {
        int cap = PlacementCap;
        if (features.Count < cap) return true;
        Debug.LogWarning($"⚠️ この階層の配置枠が上限です（{features.Count}/{cap}）。階層を広げると枠が増えます（+10で+4枠）。");
        return false;
    }

    // 🛡️ 選択中の隊員(squadPlaceSlot)を1セルに個別配置。役割コンプは編成全体から算出しスナップショット。
    public bool TryPlaceSquadMember(Vector2Int cell)
    {
        if (grid == null) grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (grid == null) return false;
        var squad = CurrentSquadList;
        if (squad.Count == 0) { Debug.LogWarning("⚠️ この階の部隊が空です。図鑑の『個体』タブで＋隊してください。"); return false; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 配置は準備フェーズのみ可能です。"); return false; }
        if (grid.GetTileType(cell.x, cell.y) == DungeonGridSystem.TileType.None) { Debug.LogWarning("⚠️ 壁には配置できません。"); return false; }
        if (features.ContainsKey(cell)) { Debug.LogWarning("⚠️ そのマスには既に要素があります。"); return false; }
        if (!CheckPlacementCap()) return false;

        // 🧬 隊のスロットはそのまま『個体』を指す（種類ではない）。
        int slot = Mathf.Clamp(squadPlaceSlot, 0, squad.Count - 1);
        int indId = squad[slot];
        var chosen = MinionRoster.Get(indId);
        if (chosen == null) { Debug.LogWarning("⚠️ その隊員の個体が見つかりません。"); return false; }
        if (IsIndividualPlaced(indId))
        {
            Debug.LogWarning($"⚠️ {MinionCatalog.Get(chosen.catalogIndex).jpName} 個体#{indId} は既に配置済みです（個体は1体のみ）。");
            return false;
        }

        // 配置は無償（DP消費は召喚時のみ）
        float comp = SquadCompMult(); // 編成全体の役割コンプを各隊員に付与
        AddFeature(cell, FeatureType.Squad, chosen.catalogIndex, comp, 0, indId);
        Debug.Log($"🛡️『隊員配置』{MinionCatalog.Get(chosen.catalogIndex).jpName} 個体#{indId}(Lv{chosen.level})（部隊バフ×{comp:0.00}）を {cell} に配置");
        // 次の未配置スロットへ自動で送る（連続配置しやすく）
        for (int i = 0; i < squad.Count; i++) { int s2 = (slot + 1 + i) % squad.Count; if (!IsIndividualPlaced(squad[s2])) { squadPlaceSlot = s2; break; } }
        return true;
    }

    // 👑 ボス任命：召喚した個体を、各階層に1体だけ『ボス』として配置。強化率(bossHp/AtkMult)＋大型化。
    //   隊とは別枠。配置は無償（召喚時にDP消費済）。個体は唯一なので全フロア横断で重複配置不可。
    public bool TryPlaceBoss(Vector2Int cell)
    {
        if (grid == null) grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (grid == null) return false;
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 配置は準備フェーズのみ可能です。"); return false; }
        if (grid.GetTileType(cell.x, cell.y) == DungeonGridSystem.TileType.None) { Debug.LogWarning("⚠️ 壁には配置できません。"); return false; }
        if (features.ContainsKey(cell)) { Debug.LogWarning("⚠️ そのマスには既に要素があります。"); return false; }
        if (HasBoss()) { Debug.LogWarning("⚠️ このフロアのボスは1体までです。"); return false; }
        if (!CheckPlacementCap()) return false;

        // 任命する個体：ボスストリップで選択した個体（未選択/配置済みなら図鑑選択中の種類から未配置先頭）。
        int indId = bossPickIndividualId;
        var chosen = MinionRoster.Get(indId);
        int type;
        if (chosen != null && !IsIndividualPlaced(indId) && !IsIndividualInAnySquad(indId) && !KinRoster.IsAwayFromDungeon(indId)) type = chosen.catalogIndex;
        else { type = selectedMinionIndex; indId = FirstBossEligibleIndividual(type); }
        if (indId < 0)
        {
            Debug.LogWarning($"⚠️ {MinionCatalog.Get(type).jpName} のボスにできる個体がありません（隊に編成済みの個体は任命できません）。図鑑で『召喚』してください。");
            return false;
        }
        if (IsIndividualInAnySquad(indId))
        {
            int sf = SquadFloorOfIndividual(indId);
            Debug.LogWarning($"⚠️ {MinionCatalog.Get(type).jpName} 個体#{indId} は B{sf + 1}F の隊に編成済みです。先に隊から外してください。");
            return false;
        }
        if (KinRoster.IsAwayFromDungeon(indId))
        {
            Debug.LogWarning($"⚠️ {MinionCatalog.Get(type).jpName} 個体#{indId} は地上に出ています（眷属またはその配下）。");
            return false;
        }
        AddFeature(cell, FeatureType.Boss, type, 1f, 0, indId);
        bossPickIndividualId = -1;
        RelicManager.ReportBossAppointed(); EurekaTracker.OnBossAppointed(); // 🏺実績＋💡天啓
        int blv = MinionRoster.LevelOf(indId);
        Debug.Log($"👑『ボス任命』{MinionCatalog.Get(type).jpName} 個体#{indId}(Lv{blv}) をこのフロアのボスに（強化×HP{bossHpMult}/ATK{bossAtkMult}・大型化）");
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
        if (!CheckPlacementCap()) return false;
        int cost = TrapCatalog.Get(selectedTrapKind).dpCost;
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(cost)) return false;
        var nf = AddFeature(cell, FeatureType.Trap, 0, 1f, selectedTrapKind);
        Debug.Log($"🪤『罠配置』{TrapCatalog.Get(selectedTrapKind).name} を {cell} に配置（-{cost}DP）");
        // 🕳️ 落とし穴は**置いただけでは完成しない**。次のクリックで行き先を決める。
        if (selectedTrapKind == (int)TrapKind.Pit)
        {
            nf.link = PitUnset;
            RefreshPitMarker(nf);      // ⚠ 置いた瞬間に「行き先は?」の印を出す（付け忘れが一目で分かる）
            pendingPit = cell;
            NotifySystem.Push("落とし穴の<b>行き先</b>を選んでください（同じ階のマスをクリック／穴自身をクリックで『下の階へ』）", NotifySystem.Kind.Story);
        }
        return true;
    }

    // ============ 🕳️ 落とし穴の行き先（2段階の配置） ============
    //
    // ⚠⚠ **階層は同時に1つしか存在しない。** `DungeonFloorManager.ActivateFloor` が盤ごと作り直すので、
    //   「1人だけ下の階へ移す」は素直には書けない。そこで落とし穴は
    //     ・同じ階のセルへ運ぶ（縦穴）＝経路の付け替え
    //     ・下の階へ落とす（奈落）＝**その階から退場させ、降下が起きたときに下で復帰させる**
    //   の2択にした。奈落で消えた者は、降下が起きないまま波が終われば**這い上がって逃げる**（名声＋装備）。
    //   ＝「落とすこと」は「倒すこと」ではない、という線を残す。→ [[DungeonFloorManager]]

    private Vector2Int pendingPit = new Vector2Int(-9999, -9999);
    public bool AwaitingPitLink { get { return pendingPit.x > -9999; } }
    public Vector2Int PendingPitCell { get { return pendingPit; } }

    /// <summary>行き先を決める。穴自身をクリックしたら『下の階へ』（研究が要る）。</summary>
    public bool TrySetPitLink(Vector2Int cell)
    {
        if (!AwaitingPitLink) return false;
        Feature f;
        if (!features.TryGetValue(pendingPit, out f)) { pendingPit = new Vector2Int(-9999, -9999); return false; }

        if (cell == pendingPit)
        {
            if (!ResearchState.IsResearched("d_trap_abyss"))
            { NotifySystem.Push("『下の階へ落とす』には領域研究<b>『奈落』</b>が要る", NotifySystem.Kind.Loss); return false; }
            if (DungeonFloorManager.CurrentFloorIsDeepest)
            { NotifySystem.Push("最下層より下は無い。同じ階のマスを選んでください", NotifySystem.Kind.Loss); return false; }
            f.link = PitBelow;
            Debug.Log("🕳️『奈落』" + pendingPit + " の落とし穴は下の階へ通じた");
        }
        else
        {
            if (grid == null) grid = Object.FindFirstObjectByType<DungeonGridSystem>();
            if (grid == null || grid.GetTileType(cell.x, cell.y) == DungeonGridSystem.TileType.None)
            { NotifySystem.Push("壁の中へは落とせない", NotifySystem.Kind.Loss); return false; }
            f.link = cell;
            Debug.Log("🕳️『縦穴』" + pendingPit + " → " + cell + " へ通じた");
        }
        RefreshPitMarker(f);
        pendingPit = new Vector2Int(-9999, -9999);
        EurekaTracker.OnPitLinked();
        SoundSystem.Play(SoundSystem.Sfx.Place);
        return true;
    }

    /// <summary>行き先を決めずにやめる＝穴ごと撤去して全額返す。</summary>
    public void CancelPendingPit()
    {
        if (!AwaitingPitLink) return;
        var c = pendingPit;
        pendingPit = new Vector2Int(-9999, -9999);
        RemoveFeature(c);
        NotifySystem.Push("落とし穴の設置をやめた（DPは戻した）", NotifySystem.Kind.Info);
    }

    /// <summary>そのマスに何か置いてあるか（掘削が塞いでよいかの判定に使う → [[Excavation]]）。</summary>
    public bool HasFeatureAt(Vector2Int cell) { return features.ContainsKey(cell); }

    /// <summary>🕳️ 踏んだマスの落とし穴はどこへ通じているか。`PitUnset` なら未完成＝何も起きない。</summary>
    public static bool TryGetPitLink(Vector2Int cell, out Vector2Int dest)
    {
        dest = PitUnset;
        var inst = Instance; if (inst == null) return false;
        Feature f;
        if (!inst.features.TryGetValue(cell, out f)) return false;
        if (f.type != FeatureType.Trap || f.trapKind != (int)TrapKind.Pit) return false;
        dest = f.link;
        return dest != PitUnset;
    }

    // 罠タイルを敷いて RoomData に種類/ダメージを設定（配置・復元共通）
    private void StampTrapTile(Feature f)
    {
        var go = grid.StampTile(f.cell.x, f.cell.y, DungeonGridSystem.TileType.Trap);
        if (go == null) return;
        var rd = go.GetComponent<RoomData>();
        if (rd != null) { var d = TrapCatalog.Get(f.trapKind); rd.damageValue = d.damage; rd.trapKind = f.trapKind; }
    }

    // 🎣 錬成研究『宝箱の任意配置』：拾得装備(素材)＋DPで、任意の場所に集客の高いbait宝箱を作る。
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
        if (!CheckPlacementCap()) return false;
        var res = DungeonResourceManager.Instance;
        if (res != null)
        {
            if (res.CraftMaterials < baitChestMaterialCost) { Debug.LogWarning($"⚠️ 素材(拾得装備)が不足（要{baitChestMaterialCost}）。"); return false; }
            if (!res.TrySpendDP(baitChestDPCost)) return false;
            res.TrySpendMaterial(baitChestMaterialCost);
        }
        AddFeature(cell, FeatureType.BaitChest, 0);
        Debug.Log($"🎣『宝箱配置』誘導用の宝箱を {cell} に作成（-{baitChestDPCost}DP -{baitChestMaterialCost}素材）");
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
    private Feature AddFeature(Vector2Int cell, FeatureType type, int minionIndex, float squadComp = 1f, int trapKind = 0, int individualId = -1)
    {
        var f = new Feature { type = type, cell = cell, minionIndex = minionIndex, squadComp = squadComp, trapKind = trapKind, individualId = individualId };
        if (type == FeatureType.Trap) StampTrapTile(f);          // 🪤 罠はタイル自体が見た目（マーカーなし）
        else if (type == FeatureType.BaitChest) StampBaitChest(f); // 🎣 宝箱もタイル自体が見た目
        else f.marker = CreateMarker(cell, type, trapKind, individualId);
        if (type == FeatureType.Totem) ApplyTotem(f);
        if (type == FeatureType.Boss) grid.SetBossCell(cell);
        if (type == FeatureType.Trap) trapsEverPlaced++;   // 🏅 実績『素手の防衛』の判定用
        features[cell] = f;
        SoundSystem.Play(SoundSystem.Sfx.Place);   // 🔊 置いた手応え
        return f;
    }

    // ============ フロア切替用：要素の退避/復元 ============
    // ⚠ セーブは**フィールド名で**突き合わせるので、末尾に足すのは安全（古いセーブでは既定値になる）。
    //   `link` が既定の (0,0) になった古い落とし穴は「行き先＝(0,0)」ではなく**未指定**として扱う（下の Normalize）。
    public struct FeatureRecord { public FeatureType type; public Vector2Int cell; public int minionIndex; public float squadComp; public int trapKind; public int individualId; public Vector2Int link; }

    public List<FeatureRecord> ExportFeatures()
    {
        var list = new List<FeatureRecord>();
        foreach (var f in features.Values)
            list.Add(new FeatureRecord { type = f.type, cell = f.cell, minionIndex = f.minionIndex, squadComp = f.squadComp, trapKind = f.trapKind, individualId = f.individualId, link = f.link });
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
            var f = AddFeature(r.cell, r.type, r.minionIndex, r.squadComp <= 0f ? 1f : r.squadComp, r.trapKind, r.individualId);
            if (f != null && r.type == FeatureType.Trap && r.trapKind == (int)TrapKind.Pit)
            {
                f.link = (r.link == Vector2Int.zero) ? PitBelow : r.link;   // 古いセーブの保険
                RefreshPitMarker(f);
            }
        }
    }

    public void RemoveFeature(Vector2Int cell)
    {
        if (!features.TryGetValue(cell, out var f)) return;
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) return; // 撤去も準備中のみ

        if (cell == pendingPit) pendingPit = new Vector2Int(-9999, -9999);   // 🕳️ 行き先待ちの穴を消したら待機も解く
        if (f.type == FeatureType.Totem) UndoTotem(f);
        if (f.type == FeatureType.Trap || f.type == FeatureType.BaitChest) grid.StampTile(f.cell.x, f.cell.y, DungeonGridSystem.TileType.Room); // 🪤🎣 タイルを床へ戻す
        if (f.marker != null) Destroy(f.marker);

        // 💰 **準備中の置き直しは全額返金**（素材要素は返金なし）。
        // ⚠ 旧コメントは「50%返金」だったが実装は**全額**で、`RefundRecords`（階層拡張で強制的に壊すとき）
        //   だけが50%だった。**意図してこの2つは率が違う**：
        //     ここ＝プレイヤーが自分で置き直す操作なので、罰を付けると「置いてみる」ができなくなる。
        //     `RefundRecords`＝拡張で巻き込まれる破壊なので、half にして拡張を軽率にしない。
        //   （コメントだけが嘘だったので直した。数値は変えていない）
        var res = DungeonResourceManager.Instance;
        if (res != null && f.type != FeatureType.SpecialEnemy)
        {
            int refund = (f.type == FeatureType.Squad || f.type == FeatureType.Boss) ? 0 // 隊員/ボスは配置無償（召喚時にDP消費済・個体はロスターに残る）
                : f.type == FeatureType.Trap ? TrapCatalog.Get(f.trapKind).dpCost
                : f.type == FeatureType.Totem ? TotemCatalog.Get(f.trapKind).dpCost   // 🗿 トーテムは種類ごとに価格が違う
                : f.type == FeatureType.BaitChest ? baitChestDPCost
                : CostOf(f.type);
            if (refund > 0) res.RefundDP(refund, true);
        }

        features.Remove(cell);
        SoundSystem.Play(SoundSystem.Sfx.Remove);
        Debug.Log($"🧩『撤去』{TypeName(f.type)} を {cell} から撤去しました。");
    }

    // 🗺️ 階層拡張で配置を破棄する際の返金（各要素の50%DP。素材要素は返金なし）
    public void RefundRecords(List<FeatureRecord> recs)
    {
        if (recs == null || DungeonResourceManager.Instance == null) return;
        int refund = 0;
        foreach (var r in recs)
        {
            if (r.type == FeatureType.SpecialEnemy || r.type == FeatureType.Squad || r.type == FeatureType.Boss) continue; // 隊員/ボスは配置無償＝返金なし
            int cost = r.type == FeatureType.Trap ? TrapCatalog.Get(r.trapKind).dpCost
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
            if (f.type == FeatureType.Boss)
            {
                // 👑 ボス：強化率 × 🧬個体Lv × ⚔️装備 × 🜏ゴエティアの加護、大型化。出撃で+1Lv。
                int blv = MinionRoster.LevelOf(f.individualId);
                var pil = GoetiaCatalog.PillarOf(f.individualId);
                float gHp = GoetiaCatalog.HpMult(pil.rank), gAtk = GoetiaCatalog.AtkMult(pil.rank);
                var zb = SpawnDefender(f.cell, bossHpMult, bossAtkMult, CRIMSON, f.minionIndex, true, MinionRoster.LevelMult(blv), 1.7f,
                    MinionRoster.EquipHpMult(f.individualId) * gHp,
                    MinionRoster.EquipAtkMult(f.individualId) * MinionRoster.TypeAtkMult(f.individualId) * gAtk);
                if (zb != null)
                {
                    zb.goetiaName = GoetiaCatalog.TitleOf(f.individualId);
                    zb.speedMult *= GoetiaCatalog.SpeedMult(pil.rank);
                    zb.accessoryOwnerId = f.individualId;   // 💍 装飾品のスキルを引く
                    zb.speedMult *= MinionRoster.AccessorySpdMult(f.individualId);
                    zb.weaponIntervalMult = MinionRoster.TypeIntervalMult(f.individualId);
                    zb.weaponRangeBonus = MinionRoster.TypeRangeBonus(f.individualId);
                    ApplyTemper(zb, f.individualId);   // 🧠 気性。⚠ weaponIntervalMult は上で"代入"されるので必ずこの後
                    Debug.Log($"🜏『ボス降臨』{MinionCatalog.Get(f.minionIndex).jpName} は {GoetiaCatalog.TitleOf(f.individualId)} の名を継いだ（{GoetiaCatalog.Blessing(pil.rank)}）");
                }
                if (f.individualId >= 0) MinionRoster.AddFloorExp(f.individualId, ActiveFloorIndex, true);   // 🧪 魔素濃度 + 🐢 追いつき補正
            }
            else if (f.type == FeatureType.SpecialEnemy)
            {
                // 👾 ユニーク：**個体のLvと装備がそのまま乗る**（隊員と同じ扱い）。
                //    種の倍率が別格なので、ここで追加の下駄は履かせない。
                int ulv = MinionRoster.LevelOf(f.individualId);
                var zsp = SpawnDefender(f.cell, 1f, 1f, null, f.minionIndex, false,
                    MinionRoster.LevelMult(ulv), 1.15f,
                    MinionRoster.EquipHpMult(f.individualId),
                    MinionRoster.EquipAtkMult(f.individualId) * MinionRoster.TypeAtkMult(f.individualId));
                if (zsp != null)
                {
                    zsp.accessoryOwnerId = f.individualId;   // 💍
                    zsp.speedMult *= MinionRoster.AccessorySpdMult(f.individualId);
                    zsp.weaponIntervalMult = MinionRoster.TypeIntervalMult(f.individualId);
                    zsp.weaponRangeBonus = MinionRoster.TypeRangeBonus(f.individualId);
                    ApplyTemper(zsp, f.individualId);        // 🧠 気性（⚠ 間隔の代入より後）
                }
                if (f.individualId >= 0) MinionRoster.AddFloorExp(f.individualId, ActiveFloorIndex, true);
            }
            else if (f.type == FeatureType.Squad)
            {
                // ⚡ 異変で追跡に出した個体はこの波に出てこない（→ [[IncidentSystem]]）
                if (IncidentSystem.IsBenched(f.individualId)) continue;
                // 🛡️ 隊員：役割コンプ × 🧬 個体Lv × ⚔️装備(グレード×種別)。出撃した個体は+1Lv（使うと育つ）。
                int lv = MinionRoster.LevelOf(f.individualId);
                var zq = SpawnDefender(f.cell, 1f, 1f, STEEL, f.minionIndex, false, f.squadComp * MinionRoster.LevelMult(lv), 1f,
                    MinionRoster.EquipHpMult(f.individualId),
                    MinionRoster.EquipAtkMult(f.individualId) * MinionRoster.TypeAtkMult(f.individualId));
                if (zq != null)
                {
                    zq.accessoryOwnerId = f.individualId;   // 💍
                    zq.speedMult *= MinionRoster.AccessorySpdMult(f.individualId);
                    zq.weaponIntervalMult = MinionRoster.TypeIntervalMult(f.individualId); // ⚔️ 武器種：手数
                    zq.weaponRangeBonus = MinionRoster.TypeRangeBonus(f.individualId);     // ⚔️ 武器種：間合い
                    ApplyTemper(zq, f.individualId);        // 🧠 気性（⚠ 間隔の代入より後）
                }
                if (f.individualId >= 0) MinionRoster.AddFloorExp(f.individualId, ActiveFloorIndex, true);   // 🧪 魔素濃度 + 🐢 追いつき補正
            }
        }
    }

    /// <summary>
    /// 🧠 気性を体に移す（→ [[MinionTemperament]]）。
    /// ⚠ **数値の取引はここで1回だけ掛ける。** `ZombieAI` 側でも掛けると二重になる。
    ///   向こうが持つのは「誰を狙うか」「瀕死でどうなるか」という**挙動**だけ。
    /// </summary>
    private void ApplyTemper(ZombieAI z, int individualId)
    {
        if (z == null || individualId < 0) return;
        int t = MinionRoster.TemperOf(individualId);
        var d = MinionTemperament.Get(t);
        z.temper = t;
        z.hpMult *= d.hpMult;
        z.atkMult *= d.atkMult;
        z.speedMult *= d.spdMult;
        z.weaponIntervalMult *= d.intervalMult;
        // 🐾 徘徊の広さ：『忠実』は置いたマスに貼りつき、『奔放』はどこまでも追う
        if (d.leash >= 0) { z.anchored = true; z.leashRadius = d.leash; }
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
                // 🧟 スポナーは**置いたときに選んでいた種**を湧かせる（見た目も34種の1枚絵で揃う）。
                //    ⚠ 旧仕様は GddMap の4種からランダムな見た目にしていたので、
                //      「何が湧くのか」が盤から読めず、育成の幹（進化・図鑑）とも繋がっていなかった。
                //    ⚠ 湧いた個体は使い捨て（ロスターには載らない）。載せると無限に個体が増える。
                //      そのぶん強さは「その種の素の強さ × 世界水準のレベル」に抑える。
                float slv = MinionRoster.LevelMult(MinionRoster.SummonLevel());
                SpawnDefender(f.cell, 1f, 1f, null, f.minionIndex, false, slv);
            }
        }
    }

    private ZombieAI SpawnDefender(Vector2Int cell, float hpMult, float atkMult, Color? tint, int minionIndex, bool guardian = false, float squadMult = 1f, float scale = 1f, float extraHpMult = 1f, float extraAtkMult = 1f)
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
            // 🗿 トーテム（範囲の層）：汎用強化＋家系限定＋手数
            float totemHp = 1f + TotemSum(cell, TotemCatalog.Kind.Bedrock) + FamilyTotem(cell, species);
            float totemAtk = 1f + TotemSum(cell, TotemCatalog.Kind.Mace) + FamilyTotem(cell, species);
            float totemInterval = Mathf.Max(0.4f, 1f - TotemSum(cell, TotemCatalog.Kind.Gale));
            var prof = SpeciesProfile(species);                                                                 // 🐺 家系プロファイル
            float aff = DemonLord.Instance != null ? DemonLord.Instance.DefenderAffinityMult(species) : 1f;     // 🧬 種族相性
            // 🏺 遺物：家系特化 ＋ 最下層限定（深淵の鏡）
            float relicFam = RelicManager.Instance != null ? RelicManager.Instance.FamilyMult(species) : 1f;
            float relicDeep = RelicManager.Instance != null ? RelicManager.Instance.DeepFloorMult(DungeonFloorManager.CurrentFloorIsDeepest) : 1f;

            z.species = species;
            z.minionIndex = minionIndex;             // 🗂️ 図鑑index（部屋編成/種族個性で将来使用）
            z.role = def.role;
            // 家系プロファイル(family) × 個体Def × 部隊コンプ を層で合成（二重計上でなく意図的な階層）
            // squadMult=対称(部隊コンプ×個体Lv)、extra*=非対称(⚔️武器→atk / 🛡️防具→hp)
            // 🏔️ 空間タイプ：家系ごとの相性＋城砦の硬さ
            float themeFam = DungeonTheme.FamilyMult(species);
            // 👑 魔王の格（払わなくても効く／ターン線形）× 🧬 進化段階（投資で効く）。
            //    どちらも「配下1体ごとにDPを払わないと伸びない」状態を崩すための軸。→ [[DemonLord.MinionPowerMult]]
            float dlMult = DemonLord.Instance != null ? DemonLord.Instance.MinionPowerMult : 1f;
            float evoMult = MinionEvolution.DepthMult(minionIndex);
            // ⚡ 異変（そのターン限り／→ [[IncidentSystem]]）
            z.hpMult = IncidentSystem.MinionHpMult * hpMult * pm * relicHp * relicFam * relicDeep * totemHp * prof.hp * aff * def.hpMult * squadMult * extraHpMult * themeFam * dlMult * evoMult * DungeonTheme.DefenderHpMult * PolicySystem.DefenderHpTotal * AttributeSystem.DefenderHpMult;   // 🏛️ 政策『肉の壁』／政体『恐怖政治』
            z.atkMult = IncidentSystem.MinionAtkMult * atkMult * pm * relicAtk * relicFam * relicDeep * totemAtk * prof.atk * aff * def.atkMult * squadMult * extraAtkMult * themeFam * dlMult * evoMult;
            z.speedMult = def.spdMult;
            z.weaponIntervalMult *= totemInterval;                       // 🌀 疾風の風車：手数が増える
            z.regenPerSec = TotemSum(cell, TotemCatalog.Kind.LifeTree);  // 🌳 生命の樹：毎秒回復
            z.isGuardian = guardian;
            // 🛡️ 配置セルをアンカーにしたガードモード（スポーン地点まで追わない）
            z.anchored = true; z.anchorCell = cell; z.leashRadius = defenderLeashRadius + DungeonTheme.LeashBonus;
            // 色：ボス/特殊敵は識別色を優先、スポナーは種族色
            z.overrideTint = true; z.tintColor = tint ?? prof.tint;
        }
        if (scale != 1f) go.transform.localScale = go.transform.localScale * scale; // 👑 ボス等の大型化
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

    // 🧬 家系限定トーテム（屍の祭壇/獣牙の柱/魔導の尖塔）：その家系の配下にだけ乗る
    private float FamilyTotem(Vector2Int cell, ZombieAI.Species s)
    {
        switch (s)
        {
            case ZombieAI.Species.Beast: return TotemSum(cell, TotemCatalog.Kind.FangBeast);
            case ZombieAI.Species.Demonkin: return TotemSum(cell, TotemCatalog.Kind.SpireDemon);
            default: return TotemSum(cell, TotemCatalog.Kind.AltarUndead);
        }
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

    // ============ 🗿 トーテム効果（TotemCatalog駆動・範囲の層） ============
    // 『誘惑の灯』だけがタイルの集客を直接いじる。それ以外は各所からの問い合わせ(TotemQuery)で効く。
    private void ApplyTotem(Feature f)
    {
        f.buffedNeighbors = new List<Vector2Int>();
        if (f.trapKind != (int)TotemCatalog.Kind.Lure) return;
        float bonus = TotemCatalog.Get((int)TotemCatalog.Kind.Lure).value;
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in dirs)
        {
            Vector2Int n = f.cell + d;
            var obj = grid.GetGridObject(n.x, n.y);
            if (obj == null) continue;
            var rd = obj.GetComponent<RoomData>();
            if (rd != null)
            {
                rd.attraction += bonus;
                f.buffedNeighbors.Add(n);
            }
        }
    }
    private void UndoTotem(Feature f)
    {
        if (f.buffedNeighbors == null) return;
        float bonus = TotemCatalog.Get((int)TotemCatalog.Kind.Lure).value;
        foreach (var n in f.buffedNeighbors)
        {
            var obj = grid.GetGridObject(n.x, n.y);
            if (obj == null) continue;
            var rd = obj.GetComponent<RoomData>();
            if (rd != null) rd.attraction -= bonus;
        }
        f.buffedNeighbors = null;
    }

    /// <summary>指定セルの範囲内にある、その種類のトーテムの合計値（重ねがけ上限 totemBuffMaxStack）。</summary>
    public float TotemSum(Vector2Int cell, TotemCatalog.Kind kind)
    {
        int n = 0; float v = 0f;
        foreach (var f in features.Values)
        {
            if (f.type != FeatureType.Totem || f.trapKind != (int)kind) continue;
            var d = TotemCatalog.Get(f.trapKind);
            int radius = Mathf.Max(1, d.radius + DungeonTheme.TotemRadiusBonus);   // 🏔️ 蟻の巣は狭くて届きにくい
            if (Mathf.Abs(f.cell.x - cell.x) + Mathf.Abs(f.cell.y - cell.y) > radius) continue;
            if (++n > totemBuffMaxStack) break;
            v += d.value;
        }
        return v;
    }

    /// <summary>ワールド座標から問い合わせる静的窓口（冒険者・罠・感情から使う）。</summary>
    public static float TotemSumAt(Vector3 world, TotemCatalog.Kind kind)
    {
        var fm = Instance;
        if (fm == null) return 0f;
        if (fm.grid == null) fm.grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (fm.grid == null) return 0f;
        return fm.TotemSum(fm.grid.WorldToGrid(world), kind);
    }

    // ============ ヘルパー ============
    public int CostOf(FeatureType type)
    {
        int baseCost;
        switch (type)
        {
            case FeatureType.Totem: baseCost = totemCostDP; break;
            case FeatureType.Spawner: baseCost = spawnerCostDP; break;
            // ⚠ ボスは**配置も撤去も無償**（DPは召喚時に払い済み・返金対象からも除外）。
            //   ここに値が入っていると「376DPかかる」と読めてしまうので 0 を返す。
            //   `bossCostDP` は使っていない（消すとインスペクタの既存値が飛ぶので残してある）。
            case FeatureType.Boss: baseCost = 0; break;
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

    // ============ 🎨 配置マーカーの見た目（MarkerArt の手続きスプライト） ============
    // 隊/ボス＝主張を抑えた『四隅のかぎ括弧』（キャラを隠さない）。ボスは小さな王冠を追加。
    // トーテム＝石柱＋種類ごとの色とアイコン。スポナー＝渦。特殊敵＝菱形。
    private GameObject CreateMarker(Vector2Int cell, FeatureType type) => CreateMarker(cell, type, 0, -1);

    private GameObject CreateMarker(Vector2Int cell, FeatureType type, int kind, int individualId)
    {
        var go = new GameObject("Feature_" + type);
        go.transform.SetParent(transform, false);
        go.transform.position = grid.GridToWorld(cell.x, cell.y) + new Vector3(0, 0, -0.5f);

        switch (type)
        {
            case FeatureType.Squad:
            case FeatureType.Boss:
                BuildGarrisonMarker(go, type, individualId, cell);
                break;
            case FeatureType.Totem:
                BuildTotemMarker(go, kind, cell);
                break;
            case FeatureType.Spawner:
                AddSprite(go, MarkerArt.Portal(), VIOLET, 0.62f, 30, Vector3.zero);
                break;
            default: // SpecialEnemy
                AddSprite(go, MarkerArt.Rhombus(), GOLD, 0.60f, 30, Vector3.zero);
                break;
        }
        return go;
    }

    // 🛡️👑 駐留マーカー：かぎ括弧＋（ボスなら王冠）＋誰が居るかのラベル
    private void BuildGarrisonMarker(GameObject go, FeatureType type, int individualId, Vector2Int cell)
    {
        bool boss = type == FeatureType.Boss;
        var col = boss ? CRIMSON : STEEL;
        col.a = boss ? 0.85f : 0.65f;                                   // 目印なので控えめ
        AddSprite(go, MarkerArt.Bracket(), col, 0.92f, 29, Vector3.zero);
        if (boss) AddSprite(go, MarkerArt.Crown(), new Color(0.95f, 0.80f, 0.35f, 0.95f), 0.34f, 31, new Vector3(0f, 0.46f, -0.05f));

        // 🧬 誰が配置されているのか（種類・Lv）をマスの下に小さく出す
        var v = MinionRoster.Get(individualId);
        if (v == null) return;
        string nm = MinionCatalog.Get(v.catalogIndex).jpName;
        string gname = boss ? GoetiaCatalog.Get(GoetiaCatalog.PillarIndexFor(individualId)).jpName : null;

        // ⚠⚠ ラベルの重なりは通しプレイで**いちばん困った**問題。
        //   1マス＝ワールド1.0 に対し「スケルトンソルジャー #3 Lv1」は3マスぶんの幅があり、
        //   隣り合うマスに置いた瞬間に文字が団子になって**どちらも読めなくなる**。
        //   対策は2つ重ねる：
        //     ① **個体#を出さない**（内部IDでプレイヤーには意味が無い）＋名前を6文字で切る
        //     ② **列ごとに上下へずらす**（横に並んだマスとは必ず段が違う）
        //   ⚠ ずらしの判定は **x だけ**で取る。`(x+y)` の市松にすると、
        //     縦に隣り合うラベルが 1.0 → 0.78 に**近づいてしまう**（縦は元から離れていて問題が無い）。
        //     重なるのは横方向だけなので、横方向にだけ効く分け方を使う。
        string shortName = nm.Length > 6 ? nm.Substring(0, 6) : nm;
        // 🧠 気性は**盤の上で読めないと意味が無い**（どこに誰を置くかの判断材料そのもの）。
        //    名前は6文字で切ってあるので、気性の2文字を足しても団子にならない。
        string label = (boss && !string.IsNullOrEmpty(gname) ? "◆" + gname + "\n" : "")
                     + shortName + " Lv" + v.level + "\n" + MinionTemperament.Name(v.temper);
        bool lower = (cell.x & 1) == 1;
        AddLabel(go, label, boss ? new Color(1f, 0.72f, 0.62f) : new Color(0.80f, 0.90f, 1f),
                 new Vector3(0f, lower ? -0.62f : -0.40f, -0.2f));
    }

    /// <summary>
    /// 🕳️ 落とし穴の見た目：穴の上の印と、**行き先まで引いた線**。
    /// ⚠ 線が無いと「どこへ通じているか」が盤の上で分からず、経路を設計する道具にならない。
    /// </summary>
    private void RefreshPitMarker(Feature f)
    {
        if (grid == null) grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (grid == null) return;
        if (f.marker != null) Destroy(f.marker);
        var go = new GameObject("Feature_Pit");
        go.transform.SetParent(transform, false);
        go.transform.position = grid.GridToWorld(f.cell.x, f.cell.y) + new Vector3(0, 0, -0.5f);
        f.marker = go;

        var col = new Color(0.72f, 0.64f, 0.95f, 1f);
        // ⚠ 罠タイルの絵は全種類で共通（緑の棘）なので、**穴に見える黒い面**を必ず重ねる。
        //   これが無いと落とし穴が「ただの緑の罠」に見えて、運ぶ罠だと分からない。
        AddSprite(go, MarkerArt.Pixel(), new Color(0.04f, 0.03f, 0.08f, 0.88f), 0.74f, 33, Vector3.zero);

        if (f.link == PitBelow)
        {
            AddSprite(go, MarkerArt.Stairs(), col, 0.50f, 35, Vector3.zero);
            AddLabel(go, "奈落", col, new Vector3(0f, -0.44f, -0.2f));
            return;
        }
        if (f.link == PitUnset)
        {
            AddSprite(go, MarkerArt.HexRing(), new Color(1f, 0.85f, 0.4f, 1f), 0.62f, 35, Vector3.zero);
            AddLabel(go, "行き先は?", new Color(1f, 0.85f, 0.4f), new Vector3(0f, -0.44f, -0.2f));
            return;
        }

        AddSprite(go, MarkerArt.HexRing(), col, 0.58f, 35, Vector3.zero);
        // 行き先までの線（1本の板を伸ばして回す）＋着地点の輪
        Vector3 a = grid.GridToWorld(f.cell.x, f.cell.y);
        Vector3 b = grid.GridToWorld(f.link.x, f.link.y);
        Vector3 d = b - a; float len = d.magnitude;
        if (len > 0.01f)
        {
            var line = new GameObject("Link");
            line.transform.SetParent(go.transform, false);
            line.transform.localPosition = new Vector3(d.x * 0.5f, d.y * 0.5f, 0.05f);
            line.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            line.transform.localScale = new Vector3(len, 0.07f, 1f);
            var sr = line.AddComponent<SpriteRenderer>();
            sr.sprite = MarkerArt.Pixel(); sr.color = new Color(col.r, col.g, col.b, 0.55f); sr.sortingOrder = 33;
        }
        AddSprite(go, MarkerArt.HexRing(), new Color(col.r, col.g, col.b, 0.85f), 0.48f, 35, new Vector3(d.x, d.y, 0f));
        AddLabel(go, "落ちる先", new Color(col.r, col.g, col.b, 0.9f), new Vector3(d.x, d.y - 0.44f, -0.2f));
    }

    // 🗿 トーテム：石柱を種類色で塗り、上に Turbo Disk のアイコンを重ねる（種類が一目で分かる）
    private void BuildTotemMarker(GameObject go, int kind, Vector2Int cell)
    {
        var d = TotemCatalog.Get(kind);
        Color c; if (!ColorUtility.TryParseHtmlString(d.colorHex, out c)) c = TEAL;
        AddSprite(go, MarkerArt.Obelisk(), c, 0.74f, 30, Vector3.zero);
        // アイコンはPPUがまちまちなので『ワールド高さ0.26に揃える』形でスケールを決める
        var icon = Resources.Load<Sprite>("Icons/" + d.icon);
        if (icon != null)
        {
            float h = icon.bounds.size.y;
            float k = h > 0.0001f ? 0.26f / h : 1f;
            AddSprite(go, icon, Color.white, k, 32, new Vector3(0f, 0.02f, -0.05f));
        }
        // ⚠ 配下のラベルと同じ理由で列ごとにずらす（トーテムの名前も隣とぶつかっていた）
        AddLabel(go, d.jpName, c, new Vector3(0f, (cell.x & 1) == 1 ? -0.62f : -0.40f, -0.2f));
    }

    private static SpriteRenderer AddSprite(GameObject parent, Sprite sp, Color col, float scale, int order, Vector3 localPos)
    {
        var go = new GameObject("Art");
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp; sr.color = col; sr.sortingOrder = order;
        return sr;
    }

    private static void AddLabel(GameObject parent, string text, Color col, Vector3 localPos)
    {
        var t = new GameObject("Label");
        t.transform.SetParent(parent.transform, false);
        t.transform.localPosition = localPos;
        t.transform.localScale = Vector3.one * 0.045f;
        var tm = t.AddComponent<TextMesh>();
        tm.text = text; tm.anchor = TextAnchor.UpperCenter; tm.alignment = TextAlignment.Center;
        tm.fontSize = 60; tm.characterSize = 0.5f; tm.color = col; tm.fontStyle = FontStyle.Bold;
        var mr = tm.GetComponent<MeshRenderer>(); if (mr != null) mr.sortingOrder = 62; // キャラより前に出す
    }
}
