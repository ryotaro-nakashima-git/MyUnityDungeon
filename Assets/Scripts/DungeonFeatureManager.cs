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
    /// <summary>⚠⚠ この index は `FeatureRecord.type` としてセーブに載る。**末尾にだけ足すこと。**
    /// `Spawner` は『巣』に意味を変えた（→ [[HabitatCatalog]]）。名前は互換のため残す。</summary>
    // ⚠ **末尾に足すこと。** この index は `FeatureRecord` に載ってセーブに入る（→ [[SaveSystem]]）。
    public enum FeatureType { Totem, Spawner, Boss, SpecialEnemy, Squad, Trap, BaitChest, Habitat, GreatWork }

    [Header("Costs")]
    [SerializeField] private int totemCostDP = 150;
    [SerializeField] private int spawnerCostDP = 250;
    [SerializeField] private int bossCostDP = 400;
    [SerializeField] private int specialMaterialCost = 3;

    [Header("Effects")]
    [Tooltip("スポナーが防衛ゾンビを湧かせる間隔(秒)")]
    [SerializeField] private float spawnerInterval = 6f;
    [Tooltip("スポナー1基が1ウェーブで湧かせる最大数")]
    /// <summary>
    /// 🪺 **素の巣**が波あたりに湧かせる数。⚠⚠ 5→2 に落とした。
    /// 5のままだと『置けば強い・置き方は関係ない』ままで、環境（→ [[HabitatCatalog]]）が
    /// ただの純増になり掛け算の軸が1本増える。素を下げて**置き方で戻す**のが設計。
    ///
    /// ⚠⚠ **`[SerializeField]` にしてはいけない。** シーンに焼かれた古い値(5)が
    ///   コードの既定値を上書きして、**下げたはずの数が下がらなかった**（実測）。
    ///   これはバランスの決めごとであって、インスペクタで触るノブではない。
    /// </summary>
    private const int NestBasePerWave = 2;
    /// <summary>⚠ 旧『スポナー』のインスペクタ値。**もう読んでいない**（消すと既存シーンの値が飛ぶので残す）。</summary>
    [SerializeField] private int spawnerMaxPerWave = 2;

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

    /// <summary>
    /// 🏛️ **その階の**隊の枠（X-1）。`SquadMaxSlots` に、その階に建てた『練兵場』の数を足す。
    ///
    /// ⚠⚠ **面積が頭数に繋がる唯一の道。** これまで `SquadMaxSlots` は研究・政策・属性でしか伸びず、
    ///   **面積にも階層数にも一切連動していなかった**。一方、通しプレイ7周で壁を動かしたのは
    ///   恒久的な頭数だけだった（配下 2体→7体 で T14→T17）。
    ///   ＝ 広げても勝ちに繋がらないので、誰も広げなかった（→ [[growth-is-a-trap]]）。
    ///   練兵場は **4×4 の空き床**を要求する＝**広げた盤にしか建たない**（→ [[GreatWorkCatalog]]）。
    /// </summary>
    public int SquadMaxSlotsOf(int floor)
    {
        return SquadMaxSlots + CountGreatWork(floor, GreatWorkCatalog.Kind.DrillGround);
    }
    /// <summary>いま編集している階の隊枠。</summary>
    public int CurrentSquadMaxSlots { get { return SquadMaxSlotsOf(ActiveFloorIndex); } }

    public int CountGreatWork(int floor, GreatWorkCatalog.Kind kind)
    {
        int n = 0;
        foreach (var f in FeaturesOf(floor).Values)
            if (f.type == FeatureType.GreatWork && f.trapKind == (int)kind) n++;
        return n;
    }
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
        // 🏢 F-2以降は**全階が同時に生きている**ので、階を横断してそのまま数える
        //    （旧仕様は「アクティブ層＝ライブ／他フロア＝退避済みの記録」の2段構えだった）
        foreach (var kv in featuresByFloor)
            foreach (var f in kv.Value.Values) if (f.individualId == id) return true;
        if (TrainingSystem.IsTraining(id)) return true;                            // 🏋️ 訓練所へ送っている
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
        foreach (var kv in featuresByFloor)
            foreach (var f in kv.Value.Values)
                if (f.type == FeatureType.Boss && f.individualId == id) return kv.Key;
        return -1;
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
        // 🏛️ 練兵場を建てた階は枠が増える（→ `SquadMaxSlotsOf`）
        int cap0 = SquadMaxSlotsOf(ActiveFloorIndex);
        if (squad.Count >= cap0) { Debug.LogWarning($"⚠️ この階の部隊は最大{cap0}枠です。"); return false; }
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
        if (s.Count >= SquadMaxSlotsOf(ActiveFloorIndex)) mult += squadFullBonus;
        mult += DungeonTheme.SquadCompBonus;   // 🏔️ 大空洞は広くて隊が組みやすい
        return mult;
    }

    /// <summary>
    /// いま操作している階の盤。
    /// ⚠⚠ **キャッシュしてはいけない**（縦の迷宮 F-2以降）。
    ///   旧仕様は `Start` で1回だけ `Active` を掴んでいた。盤が1枚の頃はそれで正しかったが、
    ///   階層ぶんの盤ができた今は **ずっと B1F の盤を握り続ける**ことになり、
    ///   B2F以降で「床なのに『壁には配置できません』と言われて置けない」になる
    ///   （判定だけ B1F の座標で行われるため。ユーザー報告）。
    /// ⚠ 配置・撤去は**表示している階**に対して行うので、ここは表示中の盤でよい。
    ///   生成先を指定したいとき（他の階に守りを湧かせる等）は `GridOf(floor)` を使う。
    /// </summary>
    private DungeonGridSystem grid { get { return GridOf(ActiveFloorIndex); } }
    [System.NonSerialized]   // 💾 場に居る実体。セーブは FloorData の配置記録から組み直す（[[SaveSystem]]）
    // 🏢 防衛体も**階層ごと**（F-2）。旧仕様は1本のリストで、降下のたびに全部 Destroy していた
    //   ＝上の階の守りが消えていた。縦の迷宮では上の階の守りは残って戦い続ける。
    private readonly Dictionary<int, List<GameObject>> defendersByFloor = new Dictionary<int, List<GameObject>>();

    private List<GameObject> DefendersOf(int floor)
    {
        List<GameObject> l;
        if (!defendersByFloor.TryGetValue(floor, out l)) { l = new List<GameObject>(); defendersByFloor[floor] = l; }
        return l;
    }

    /// <summary>
    /// いま防衛体を生成している階。⚠ `SpawnDefender` の呼び出しが多い（10箇所以上）ので
    /// 引数を増やさず、生成中だけここに階を立てる形にしてある。-1＝表示中の階。
    /// </summary>
    private int spawnFloor = -1;
    private int SpawnFloorIndex { get { return spawnFloor >= 0 ? spawnFloor : ActiveFloorIndex; } }
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
        public int habitatKind; // 🌿 Habitat型のみ：環境の種類(HabitatCatalog.Kind)
        // 🪺 Spawner(＝巣)型のみ：波をまたいで育つ。⚠ セーブに載せる（育てた物が消えたら意味が無い）
        public int nestLevel = 1;
        public int nutrient;        // 養分。閾値を超えると nestLevel が上がる
        public int bornThisWave;    // この波で湧かせた数（生存数を数えるのに使う）
        public int individualId = -1; // 🧬 Squad隊員型のみ：配置した個体(MinionRoster)のID。Lv育成/重複配置防止に使う
        /// <summary>🕳️ 落とし穴のみ：落とす先。`(-1,-1)`＝**下の階へ**（奈落）。`(-2,-2)`＝行き先未定（配置直後）。</summary>
        public Vector2Int link = PitUnset;
    }
    /// <summary>🕳️ 落とし穴の行き先の特別な値。⚠ セーブに載るので意味を変えない。</summary>
    public static readonly Vector2Int PitBelow = new Vector2Int(-1, -1);
    public static readonly Vector2Int PitUnset = new Vector2Int(-2, -2);
    [System.NonSerialized]   // 💾 マーカー(GameObject)を持つので保存しない。ExportFeatures/ImportFeatures で往復する
    // ═══ 🏢 縦の迷宮（F-2）：配置は**階層ごとに実体を持つ** ═══
    //
    // ⚠⚠ 旧仕様は「盤は1枚・辞書も1つ」で、階を切り替えるたびに `ExportFeatures`/`ImportFeatures` で
    //   丸ごと退避・復元していた。つまり**表示していない階の配置は存在しなかった**（記録だけがあった）。
    //   縦の迷宮では全階が同時に生きるので、辞書も階層ぶん持つ。
    //
    // ⚠ この流儀はこのファイルに既にある（`squadByFloor` / `SquadOf(floor)` / `CurrentSquadList`）。
    //   同じ形に揃えることで、30箇所ある `features` の呼び出しを**1行も変えずに**階層対応になる。
    private readonly Dictionary<int, Dictionary<Vector2Int, Feature>> featuresByFloor
        = new Dictionary<int, Dictionary<Vector2Int, Feature>>();

    private Dictionary<Vector2Int, Feature> FeaturesOf(int floor)
    {
        Dictionary<Vector2Int, Feature> d;
        if (!featuresByFloor.TryGetValue(floor, out d)) { d = new Dictionary<Vector2Int, Feature>(); featuresByFloor[floor] = d; }
        return d;
    }

    /// <summary>いま表示・編集している階の配置。⚠ 既存の呼び出しは全部これを見ている。</summary>
    private Dictionary<Vector2Int, Feature> features { get { return FeaturesOf(ActiveFloorIndex); } }

    /// <summary>その階の盤（無ければ表示中の盤）。⚠ マーカーの座標はこれを通す。</summary>
    private DungeonGridSystem GridOf(int floor)
    {
        var g = DungeonGridSystem.Of(floor);
        return g != null ? g : DungeonGridSystem.Active;
    }

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
        // ⚠ grid はプロパティ（表示中の階の盤）。キャッシュしない
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
        if (CellOccupied(ActiveFloorIndex, cell))
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

    /// <summary>
    /// 🧹 **全階層の配置を空にする（新しい周を始めるときに呼ぶ）。**
    ///
    /// ⚠⚠ `ResetRunCounters` は `trapsEverPlaced` を0にするだけで、**置いた物は残っていた**
    ///   （実測：同じセッションの2周目が T1 の時点で 7/14 枠埋まった状態で始まった）。
    ///   人が「タイトルへ戻る」→「新しい世界を始める」を続けてやっても同じことが起きる。
    /// </summary>
    public void ClearAllRunFeatures()
    {
        var floors = new List<int>(featuresByFloor.Keys);
        int n = 0;
        for (int i = 0; i < floors.Count; i++)
        {
            var d = featuresByFloor[floors[i]];
            if (d != null) n += d.Count;
            ClearAllFeatures(floors[i]);
        }
        squadByFloor.Clear();
        if (n > 0) Debug.Log("🧹『配置を空にした』前の周の設置物 " + n + " 個を片付けた");
    }
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
        if (grid == null) return false;
        var squad = CurrentSquadList;
        if (squad.Count == 0) { Debug.LogWarning("⚠️ この階の部隊が空です。図鑑の『個体』タブで＋隊してください。"); return false; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 配置は準備フェーズのみ可能です。"); return false; }
        if (grid.GetTileType(cell.x, cell.y) == DungeonGridSystem.TileType.None) { Debug.LogWarning("⚠️ 壁には配置できません。"); return false; }
        if (CellOccupied(ActiveFloorIndex, cell)) { Debug.LogWarning("⚠️ そのマスには既に要素があります。"); return false; }
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
        // 🔮 召喚の演出（→ [[KillFeedback]]）。**盤に現れる瞬間**が一番の見せ場なので、
        //    図鑑で買った瞬間ではなくここで出す。段が高い個体ほど派手にする。
        KillFeedback.OnSummon(grid.GridToWorld(cell.x, cell.y), MinionCatalog.Get(chosen.catalogIndex).rank >= MinionCatalog.Rank.B);
        Debug.Log($"🛡️『隊員配置』{MinionCatalog.Get(chosen.catalogIndex).jpName} 個体#{indId}(Lv{chosen.level})（部隊バフ×{comp:0.00}）を {cell} に配置");
        // 次の未配置スロットへ自動で送る（連続配置しやすく）
        for (int i = 0; i < squad.Count; i++) { int s2 = (slot + 1 + i) % squad.Count; if (!IsIndividualPlaced(squad[s2])) { squadPlaceSlot = s2; break; } }
        return true;
    }

    // 👑 ボス任命：召喚した個体を、各階層に1体だけ『ボス』として配置。強化率(bossHp/AtkMult)＋大型化。
    //   隊とは別枠。配置は無償（召喚時にDP消費済）。個体は唯一なので全フロア横断で重複配置不可。
    public bool TryPlaceBoss(Vector2Int cell)
    {
        if (grid == null) return false;
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 配置は準備フェーズのみ可能です。"); return false; }
        if (grid.GetTileType(cell.x, cell.y) == DungeonGridSystem.TileType.None) { Debug.LogWarning("⚠️ 壁には配置できません。"); return false; }
        if (CellOccupied(ActiveFloorIndex, cell)) { Debug.LogWarning("⚠️ そのマスには既に要素があります。"); return false; }
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
    // 🌿 いま置こうとしている環境（→ [[HabitatCatalog]]）
    private int selectedHabitatKind = 0;
    public int SelectedHabitatKind { get { return selectedHabitatKind; } }
    public void SetSelectedHabitatKind(int k) { selectedHabitatKind = Mathf.Clamp(k, 0, HabitatCatalog.Count - 1); }

    /// <summary>🌿 環境を置く。⚠ 巣と同じく**配置枠を食う**（罠・トーテム・隊とゼロサム）。</summary>
    public bool TryPlaceHabitat(Vector2Int cell)
    {
        if (grid == null) return false;
        var turn0 = DungeonTurnManager.Instance;
        if (turn0 != null && !turn0.IsPreparePhase) { Debug.LogWarning("⚠️ 配置は準備フェーズのみ可能です。"); return false; }
        if (grid.GetTileType(cell.x, cell.y) == DungeonGridSystem.TileType.None) { Debug.LogWarning("⚠️ 壁には置けません。"); return false; }
        if (CellOccupied(ActiveFloorIndex, cell)) { Debug.LogWarning("⚠️ そのマスには既に要素があります。"); return false; }
        if (!CheckPlacementCap()) return false;
        int cost = HabitatCatalog.Get(selectedHabitatKind).dpCost;
        var res0 = DungeonResourceManager.Instance;
        if (res0 != null && !res0.TrySpendDP(cost)) return false;
        AddFeature(cell, FeatureType.Habitat, 0, 1f, 0, -1, selectedHabitatKind);
        Debug.Log("🌿『環境』" + HabitatCatalog.Name(selectedHabitatKind) + " を " + cell + " に置いた（-" + cost + "DP）");
        return true;
    }

    // ============ 🏛️ 巨大施設（X-1）============
    // ⚠⚠ **4×4 の空いた床**が要る。10×10 では1か所も取れず、1段広げた瞬間に29か所現れる
    //   （実測 → [[GreatWorkCatalog]]）。＝「広げた者にだけ見える報酬」。
    // ⚠ 実体は**左下の1マス**にだけ登録し、残り15マスは `GreatWorkCovers` で塞ぐ。
    //   16マスぶんの Feature を作ると、配置枠も返金も撤去も全部16倍になっておかしくなる。

    private int selectedGreatWorkKind = 0;
    public int SelectedGreatWorkKind { get { return selectedGreatWorkKind; } }
    public void SetSelectedGreatWorkKind(int k) { selectedGreatWorkKind = Mathf.Clamp(k, 0, GreatWorkCatalog.Count - 1); }

    /// <summary>🏛️ そのマスが巨大施設の敷地に入っているか（左下1マス以外も塞ぐ）。</summary>
    public bool GreatWorkCovers(int floor, Vector2Int c)
    {
        int s = GreatWorkCatalog.Size;
        foreach (var f in FeaturesOf(floor).Values)
        {
            if (f.type != FeatureType.GreatWork) continue;
            if (c.x >= f.cell.x && c.x < f.cell.x + s && c.y >= f.cell.y && c.y < f.cell.y + s) return true;
        }
        return false;
    }

    /// <summary>🏛️ そのマスは何かに使われているか（要素そのもの＋巨大施設の敷地）。</summary>
    private bool CellOccupied(int floor, Vector2Int c)
    {
        return FeaturesOf(floor).ContainsKey(c) || GreatWorkCovers(floor, c);
    }

    /// <summary>🏛️ 左下を `at` とする 4×4 が丸ごと空いた床か。</summary>
    public bool CanPlaceGreatWorkAt(int floor, Vector2Int at, out string why)
    {
        why = "";
        int s = GreatWorkCatalog.Size;
        var g = GridOf(floor);
        if (g == null) { why = "盤がない"; return false; }
        for (int dx = 0; dx < s; dx++)
            for (int dy = 0; dy < s; dy++)
            {
                var c = new Vector2Int(at.x + dx, at.y + dy);
                if (c.x < 0 || c.y < 0 || c.x >= g.MapWidth || c.y >= g.MapHeight)
                { why = s + "×" + s + " が盤からはみ出す"; return false; }
                if (g.GetTileType(c.x, c.y) == DungeonGridSystem.TileType.None)
                { why = s + "×" + s + " ぶんの床が要る（壁が混じっている）"; return false; }
                if (CellOccupied(floor, c)) { why = "敷地に他の物が置いてある"; return false; }
            }
        return true;
    }

    /// <summary>🏛️ その階に 4×4 が取れる場所があるか（進言が読む）。</summary>
    public bool AnyGreatWorkSpot(int floor)
    {
        var g = GridOf(floor);
        if (g == null) return false;
        int s = GreatWorkCatalog.Size;
        string why;
        for (int x = 0; x + s <= g.MapWidth; x++)
            for (int y = 0; y + s <= g.MapHeight; y++)
                if (CanPlaceGreatWorkAt(floor, new Vector2Int(x, y), out why)) return true;
        return false;
    }

    public int GreatWorkCount { get { int n = 0; foreach (var f in features.Values) if (f.type == FeatureType.GreatWork) n++; return n; } }

    /// <summary>🏛️ 建てる。⚠ 左下のマスを指定する（クリックしたマスを左下として扱う）。</summary>
    public bool TryPlaceGreatWork(Vector2Int at)
    {
        if (grid == null) return false;
        var turn0 = DungeonTurnManager.Instance;
        if (turn0 != null && !turn0.IsPreparePhase) { Debug.LogWarning("⚠️ 配置は準備フェーズのみ可能です。"); return false; }
        string why;
        if (!CanPlaceGreatWorkAt(ActiveFloorIndex, at, out why)) { Debug.LogWarning("⚠️ ここには建てられません：" + why); return false; }
        if (!CheckPlacementCap()) return false;
        int cost = GreatWorkCatalog.Get(selectedGreatWorkKind).dpCost;
        var res0 = DungeonResourceManager.Instance;
        if (res0 != null && !res0.TrySpendDP(cost)) return false;
        AddFeature(at, FeatureType.GreatWork, 0, 1f, selectedGreatWorkKind);
        Debug.Log("🏛️『巨大施設』" + GreatWorkCatalog.Name(selectedGreatWorkKind) + " を " + at
            + " に建てた（-" + cost + "DP・" + GreatWorkCatalog.Size + "×" + GreatWorkCatalog.Size + "）");
        return true;
    }

    /// <summary>🏛️ 盤に乗せたときの1行。</summary>
    public string GreatWorkLineAt(int floor, Vector2Int cell)
    {
        Feature f;
        if (!FeaturesOf(floor).TryGetValue(cell, out f) || f.type != FeatureType.GreatWork) return "";
        string line = GreatWorkCatalog.Line(f.trapKind);
        if (f.trapKind == (int)GreatWorkCatalog.Kind.DrillGround)
            line += "　<color=#5cc47c>この階の隊 " + SquadOf(floor).Count + "/" + SquadMaxSlotsOf(floor) + "</color>";
        else
        {
            int moss, spring, feed;
            CountHabitat(floor, cell, out moss, out spring, out feed);
            line += "　<color=#5cc47c>" + NestPerWave(f, spring) + " 体/波</color>"
                  + (moss + spring + feed > 0 ? "　環境 " + (moss + spring + feed) : "　<color=#9c95b4>環境なし</color>");
        }
        return line;
    }

    private int selectedTrapKind = 0;
    public int SelectedTrapKind => selectedTrapKind;
    public void SetSelectedTrapKind(int k) { selectedTrapKind = Mathf.Clamp(k, 0, TrapCatalog.Count - 1); }

    // 🪤 現在選択中の罠を配置。処理はStep1どおりRoomDataタイル（盗賊のMP解除・クールダウン）＋種類で状態異常。
    //     要素として登録するので、フロア切替/侵略開始でexport/importに乗り永続化される（消失バグ修正）。
    public bool TryPlaceTrap(Vector2Int cell)
    {
        if (grid == null) return false;
        if (!TrapCatalog.IsUnlocked(selectedTrapKind)) { Debug.LogWarning("⚠️ その罠は領域研究で未解禁です。"); return false; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 配置は準備フェーズのみ可能です。"); return false; }
        if (grid.GetTileType(cell.x, cell.y) == DungeonGridSystem.TileType.None) { Debug.LogWarning("⚠️ 壁には配置できません。"); return false; }
        if (CellOccupied(ActiveFloorIndex, cell)) { Debug.LogWarning("⚠️ そのマスには既に要素があります。"); return false; }
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
            pendingPit = cell; pendingPitFloor = ActiveFloorIndex;
            NotifySystem.Push("落とし穴の<b>行き先</b>を選んでください（同じ階のマスをクリック／穴自身をクリックで『下の階へ』）", NotifySystem.Kind.Story);
        }
        return true;
    }

    // ============ 🕳️ 落とし穴の行き先（2段階の配置） ============
    //
    // 🕳️ 落とし穴は「倒す罠」ではなく「運ぶ罠」。
    // ⚠ F-2以降、階層は**同時に存在する**が、降下が起きるまで下の階のどこへ着地するかは
    //   決まらない（穴の真下は降りた瞬間に決まる）。そこで落とし穴は
    //     ・同じ階のセルへ運ぶ（縦穴）＝経路の付け替え
    //     ・下の階へ落とす（奈落）＝**その階から退場させ、降下が起きたときに下で復帰させる**
    //   の2択にした。奈落で消えた者は、降下が起きないまま波が終われば**這い上がって逃げる**（名声＋装備）。
    //   ＝「落とすこと」は「倒すこと」ではない、という線を残す。→ [[DungeonFloorManager]]

    private Vector2Int pendingPit = new Vector2Int(-9999, -9999);
    // 🏢 **どの階に置いた穴か**（縦の迷宮）。⚠⚠ セルだけだと、B1Fで穴を置いてから
    //   階を切り替えたときに **B2Fの同じ座標** を見に行く（(5,5)は全階に在る）。
    //   `CancelPendingPit` に至っては**別の階の要素を消して返金**してしまう。
    private int pendingPitFloor = -1;
    /// <summary>⚠ 置いた階を表示しているときだけ「行き先待ち」として扱う。</summary>
    public bool AwaitingPitLink { get { return pendingPit.x > -9999 && pendingPitFloor == ActiveFloorIndex; } }
    /// <summary>階を問わず未完了の穴が在るか（畳むときに使う）。</summary>
    public bool HasPendingPitAnywhere { get { return pendingPit.x > -9999; } }
    public Vector2Int PendingPitCell { get { return pendingPit; } }

    /// <summary>行き先を決める。穴自身をクリックしたら『下の階へ』（研究が要る）。</summary>
    public bool TrySetPitLink(Vector2Int cell)
    {
        if (!AwaitingPitLink) return false;
        Feature f;
        if (!FeaturesOf(pendingPitFloor).TryGetValue(pendingPit, out f)) { ClearPendingPit(); return false; }

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
            if (grid == null || grid.GetTileType(cell.x, cell.y) == DungeonGridSystem.TileType.None)
            { NotifySystem.Push("壁の中へは落とせない", NotifySystem.Kind.Loss); return false; }
            f.link = cell;
            Debug.Log("🕳️『縦穴』" + pendingPit + " → " + cell + " へ通じた");
        }
        RefreshPitMarker(f);
        ClearPendingPit();
        EurekaTracker.OnPitLinked();
        SoundSystem.Play(SoundSystem.Sfx.Place);
        return true;
    }

    /// <summary>行き先を決めずにやめる＝穴ごと撤去して全額返す。</summary>
    public void CancelPendingPit()
    {
        // ⚠ 階を問わず畳む。⚠⚠ **消すのは置いた階のもの**（表示中の階から消すと別の穴を巻き添えにする）
        if (!HasPendingPitAnywhere) return;
        var c = pendingPit; int f = pendingPitFloor;
        ClearPendingPit();
        RemoveFeature(f, c);
        NotifySystem.Push("落とし穴の設置をやめた（DPは戻した）", NotifySystem.Kind.Info);
    }

    private void ClearPendingPit() { pendingPit = new Vector2Int(-9999, -9999); pendingPitFloor = -1; }

    /// <summary>そのマスに何か置いてあるか（掘削が塞いでよいかの判定に使う → [[Excavation]]）。</summary>
    public bool HasFeatureAt(Vector2Int cell) { return CellOccupied(ActiveFloorIndex, cell); }

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
        if (grid == null) return false;
        if (!ResearchState.IsResearched("r_baitchest")) { Debug.LogWarning("⚠️ 宝箱の任意配置は錬成研究で未解禁です。"); return false; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 配置は準備フェーズのみ可能です。"); return false; }
        if (grid.GetTileType(cell.x, cell.y) == DungeonGridSystem.TileType.None) { Debug.LogWarning("⚠️ 壁には配置できません。"); return false; }
        if (CellOccupied(ActiveFloorIndex, cell)) { Debug.LogWarning("⚠️ そのマスには既に要素があります。"); return false; }
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
    private Feature AddFeature(Vector2Int cell, FeatureType type, int minionIndex, float squadComp = 1f, int trapKind = 0, int individualId = -1, int habitatKind = 0)
    {
        var f = new Feature { type = type, cell = cell, minionIndex = minionIndex, squadComp = squadComp, trapKind = trapKind, individualId = individualId, habitatKind = habitatKind };
        if (type == FeatureType.Trap) StampTrapTile(f);          // 🪤 罠はタイル自体が見た目（マーカーなし）
        else if (type == FeatureType.BaitChest) StampBaitChest(f); // 🎣 宝箱もタイル自体が見た目
        else f.marker = CreateMarker(cell, type, type == FeatureType.Habitat ? habitatKind : trapKind, individualId);
        if (type == FeatureType.Totem) ApplyTotem(f);
        if (type == FeatureType.Boss) grid.SetBossCell(cell);
        if (type == FeatureType.Trap) trapsEverPlaced++;   // 🏅 実績『素手の防衛』の判定用
        // 🏢 **生成先の階**の辞書に入れる（`spawnFloor` が立っていればその階、無ければ表示中）。
        // ⚠⚠ ここを `features`（表示中の階）にすると、ロード時の `ImportFeatures(floor, ...)` が
        //   **全階の配置を表示中の1階に積んでしまう**（実測：B1F=1/B3F=3 が B1F=3/B3F=0 になった）。
        FeaturesOf(SpawnFloorIndex)[cell] = f;
        SoundSystem.Play(SoundSystem.Sfx.Place);   // 🔊 置いた手応え
        return f;
    }

    // ============ フロア切替用：要素の退避/復元 ============
    // ⚠ セーブは**フィールド名で**突き合わせるので、末尾に足すのは安全（古いセーブでは既定値になる）。
    //   `link` が既定の (0,0) になった古い落とし穴は「行き先＝(0,0)」ではなく**未指定**として扱う（下の Normalize）。
    public struct FeatureRecord { public FeatureType type; public Vector2Int cell; public int minionIndex; public float squadComp; public int trapKind; public int individualId; public Vector2Int link; public int habitatKind; public int nestLevel; public int nutrient; }

    // ⚠⚠ F-2以降、これは**フロア切替では使わない**（切替は表示を変えるだけ）。
    //   使うのは**セーブ/ロードだけ**。切替のたびに Import すると、
    //   マーカーを毎回作り直すうえ「表示していない階は存在しない」旧構造に戻ってしまう。
    public List<FeatureRecord> ExportFeatures() { return ExportFeatures(ActiveFloorIndex); }

    public List<FeatureRecord> ExportFeatures(int floor)
    {
        var list = new List<FeatureRecord>();
        foreach (var f in FeaturesOf(floor).Values)
            list.Add(new FeatureRecord { type = f.type, cell = f.cell, minionIndex = f.minionIndex, squadComp = f.squadComp, trapKind = f.trapKind, individualId = f.individualId, link = f.link, habitatKind = f.habitatKind, nestLevel = f.nestLevel, nutrient = f.nutrient });
        return list;
    }

    public void ImportFeatures(List<FeatureRecord> recs) { ImportFeatures(ActiveFloorIndex, recs); }

    public void ImportFeatures(int floor, List<FeatureRecord> recs)
    {
        ClearAllFeatures(floor);
        if (recs == null) return;
        var g = GridOf(floor);
        // ⚠ 生成先の階を立ててから作る（マーカーの座標がその階の盤の原点に乗る）
        spawnFloor = floor;
        try
        {
            foreach (var r in recs)
            {
                if (g != null && g.GetTileType(r.cell.x, r.cell.y) == DungeonGridSystem.TileType.None) continue; // 壁化したマスはスキップ
                var f = AddFeature(r.cell, r.type, r.minionIndex, r.squadComp <= 0f ? 1f : r.squadComp, r.trapKind, r.individualId, r.habitatKind);
                // 🪺 育てた巣を戻す。⚠ 古いセーブは nestLevel=0 で来るので 1 に直す（0だと1体も湧かない）
                if (f != null && r.type == FeatureType.Spawner) { f.nestLevel = Mathf.Max(1, r.nestLevel); f.nutrient = Mathf.Max(0, r.nutrient); }
                if (f != null && r.type == FeatureType.Trap && r.trapKind == (int)TrapKind.Pit)
                {
                    f.link = (r.link == Vector2Int.zero) ? PitBelow : r.link;   // 古いセーブの保険
                    RefreshPitMarker(f);
                }
            }
        }
        finally { spawnFloor = -1; }
    }

    public void RemoveFeature(Vector2Int cell) { RemoveFeature(ActiveFloorIndex, cell); }

    /// <param name="floor">🏢 撤去する階。⚠ 表示中と違う階のものを消すことがある
    /// （行き先を決めないまま階を移った落とし穴を畳むときなど）。</param>
    public void RemoveFeature(int floor, Vector2Int cell)
    {
        if (!FeaturesOf(floor).TryGetValue(cell, out var f)) return;
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) return; // 撤去も準備中のみ

        if (cell == pendingPit && pendingPitFloor == floor) ClearPendingPit();   // 🕳️ 行き先待ちの穴を消したら待機も解く
        var fg = GridOf(floor);
        if (f.type == FeatureType.Totem) UndoTotem(f);
        if (f.type == FeatureType.Trap || f.type == FeatureType.BaitChest) fg.StampTile(f.cell.x, f.cell.y, DungeonGridSystem.TileType.Room); // 🪤🎣 タイルを床へ戻す
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
                : f.type == FeatureType.GreatWork ? GreatWorkCatalog.Get(f.trapKind).dpCost
                : f.type == FeatureType.Habitat ? HabitatCatalog.Get(f.habitatKind).dpCost
                : f.type == FeatureType.Trap ? TrapCatalog.Get(f.trapKind).dpCost
                : f.type == FeatureType.Totem ? TotemCatalog.Get(f.trapKind).dpCost   // 🗿 トーテムは種類ごとに価格が違う
                : f.type == FeatureType.BaitChest ? baitChestDPCost
                : CostOf(f.type);
            if (refund > 0) res.RefundDP(refund, true);
        }

        FeaturesOf(floor).Remove(cell);   // ⚠ 指定された階から消す（表示中の階ではない）
        SoundSystem.Play(SoundSystem.Sfx.Remove);
        Debug.Log($"🧩『撤去』{TypeName(f.type)} を B{floor + 1}F {cell} から撤去しました。");
    }

    /// <summary>
    /// 🗺️ **広げたあとに配置を戻す。**（X の前提条件・2026-08-31）
    ///
    /// <para>
    /// ⚠⚠ **なぜ要るか（実測）**：拡張は地形を作り直すので、以前は**その階の配置を全部捨てて
    ///   50%返金**していた。結果、広げた**直後の波を空の盤で迎える**ことになり、
    ///   通しプレイでは広げるほど早く死んだ（T15 → T12 → **T11**）。
    ///   5周目 T12 は **枠 1/54・巣0**、6周目 T6 は 14/14 → **7/18** で次の波に魔王HP 32%。
    ///   ＝「広さの報酬」を用意しても、**受け取る前に守りが消える**ので誰も広げられない
    ///   （→ [[growth-is-a-trap]]）。
    /// </para>
    ///
    /// <para>
    /// ⚠ 地形は作り直しなので**同じマスに戻せるとは限らない**。
    ///   そこで「同じマス → 駄目なら**近い床へずらす**」にする。
    ///   ⚠ ずらすのは**近い順**（`SearchRing` まで）。遠くへ飛ばすと、
    ///     プレイヤーが組んだ配置の意図（関所・巣の周りの環境）が壊れる。
    ///   ⚠ **戻せなかったぶんだけ**返金する（全部捨てない）。
    /// ⚠ 巣のレベルと養分は `FeatureRecord` に乗っているので、ずらしても**育ちは失われない**。
    /// </para>
    /// </summary>
    /// <returns>戻せた数</returns>
    public int RestoreAfterResize(int floor, List<FeatureRecord> saved, out int moved, out List<FeatureRecord> lost)
    {
        moved = 0;
        lost = new List<FeatureRecord>();
        if (saved == null || saved.Count == 0) return 0;
        var g = GridOf(floor);
        if (g == null) { lost.AddRange(saved); return 0; }

        const int SearchRing = 6;   // ⚠ これ以上は探さない（別の部屋へ飛ばさないため）
        var taken = new HashSet<Vector2Int>();
        var kept = new List<FeatureRecord>();

        // ⚠⚠ **順番が意味を持つ。** 巣とトーテムは**範囲で効く錨**なので先に据える。
        //   環境（🌿）はその錨の 2マス以内でないと効かないので、錨が決まったあとに置く。
        //   ここを雑にやると「物は戻ったが、組んだ形は壊れている」ことになる。
        var order = new List<FeatureRecord>(saved);
        order.Sort((x, y) => RestoreRank(x.type).CompareTo(RestoreRank(y.type)));
        var newNests = new List<Vector2Int>();

        foreach (var r in order)
        {
            Vector2Int at;
            if (FreeFloorCell(g, r.cell, taken)) at = r.cell;
            else if (r.type == FeatureType.Habitat && newNests.Count > 0
                     && NearestFreeCell(g, NearestOf(newNests, r.cell), taken, HabitatCatalog.Reach, out at))
            {
                // 🌿 元の場所が使えないなら、**仕えていた巣のそば**へ寄せる（効かない場所に置き直さない）
            }
            else if (!NearestFreeCell(g, r.cell, taken, SearchRing, out at)) { lost.Add(r); continue; }
            if (r.type == FeatureType.Spawner) newNests.Add(at);

            if (at != r.cell) moved++;
            taken.Add(at);
            var rec = r;
            rec.cell = at;
            // 🕳️ 行き先が壁になった落とし穴は『下の階へ』に戻す（黙った罠にしない）
            if (rec.type == FeatureType.Trap && rec.trapKind == (int)TrapKind.Pit
                && rec.link != PitBelow && !FreeFloorCell(g, rec.link, null))
                rec.link = PitBelow;
            kept.Add(rec);
        }

        ImportFeatures(floor, kept);
        return kept.Count;
    }

    /// <summary>戻す順番。小さいほど先（範囲で効く錨 → それに寄り添う物 → 単独で効く物）。</summary>
    private static int RestoreRank(FeatureType t)
    {
        switch (t)
        {
            case FeatureType.Spawner: return 0;   // 🪺 巣＝環境の錨
            case FeatureType.Totem: return 1;     // 🗿 トーテム＝範囲バフの錨
            case FeatureType.Boss: return 2;
            case FeatureType.Squad: return 3;
            case FeatureType.Habitat: return 4;   // 🌿 錨が決まってから
            default: return 5;                    // 罠・宝箱など
        }
    }

    private static Vector2Int NearestOf(List<Vector2Int> pts, Vector2Int from)
    {
        var best = pts[0];
        int bd = int.MaxValue;
        for (int i = 0; i < pts.Count; i++)
        {
            int d = Mathf.Abs(pts[i].x - from.x) + Mathf.Abs(pts[i].y - from.y);
            if (d < bd) { bd = d; best = pts[i]; }
        }
        return best;
    }

    private bool FreeFloorCell(DungeonGridSystem g, Vector2Int c, HashSet<Vector2Int> taken)
    {
        if (c.x < 0 || c.y < 0 || c.x >= g.MapWidth || c.y >= g.MapHeight) return false;
        if (g.GetTileType(c.x, c.y) == DungeonGridSystem.TileType.None) return false;
        return taken == null || !taken.Contains(c);
    }

    /// <summary>いちばん近い空いた床（マンハッタンの輪を外へ広げながら探す）。</summary>
    private bool NearestFreeCell(DungeonGridSystem g, Vector2Int from, HashSet<Vector2Int> taken, int maxRing, out Vector2Int found)
    {
        found = from;
        for (int r = 1; r <= maxRing; r++)
            for (int dx = -r; dx <= r; dx++)
            {
                int dy = r - Mathf.Abs(dx);
                for (int s = 0; s < 2; s++)
                {
                    var c = new Vector2Int(from.x + dx, from.y + (s == 0 ? dy : -dy));
                    if (FreeFloorCell(g, c, taken)) { found = c; return true; }
                    if (dy == 0) break;
                }
            }
        return false;
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
                : r.type == FeatureType.GreatWork ? GreatWorkCatalog.Get(r.trapKind).dpCost
                : r.type == FeatureType.Habitat ? HabitatCatalog.Get(r.habitatKind).dpCost
                : r.type == FeatureType.BaitChest ? baitChestDPCost
                : CostOf(r.type);
            refund += cost / 2;
        }
        if (refund > 0) DungeonResourceManager.Instance.AddDP(refund);
    }

    public void ClearAllFeatures() { ClearAllFeatures(ActiveFloorIndex); }

    public void ClearAllFeatures(int floor)
    {
        var d = FeaturesOf(floor);
        foreach (var kv in d)
        {
            if (kv.Value.type == FeatureType.Totem) UndoTotem(kv.Value);
            if (kv.Value.marker != null) Destroy(kv.Value.marker);
        }
        d.Clear();
    }

    // ============ 戦闘連動 ============
    private void OnBattleStart()
    {
        // 🏢 複数フロア時はフロアマネージャが降下ごとにスポーンを駆動する（ここでは何もしない）
        if (DungeonFloorManager.Instance != null) return;
        SpawnDefendersForActiveFloor();
    }

    /// <summary>
    /// 🏢 **全階の防衛体を一度に立てる**（F-2）。侵略開始で1回だけ呼ぶ。
    /// ⚠ 旧仕様は「降りた先の階だけ」を降下のたびに立てていた。縦の迷宮では
    ///   上の階も同時に戦っているので、最初に全部立てておく必要がある。
    /// </summary>
    public void SpawnDefendersForAllFloors(int floorCount)
    {
        for (int i = 0; i < floorCount; i++) SpawnDefendersForFloor(i);
    }

    /// <summary>現在アクティブなフロアぶん。</summary>
    public void SpawnDefendersForActiveFloor() { SpawnDefendersForFloor(ActiveFloorIndex); }

    /// <summary>指定フロアの配置要素から防衛体をスポーンする。</summary>
    public void SpawnDefendersForFloor(int floor)
    {
        // ⚠ 生成中だけ「どの階に置くか」を立てる。`SpawnDefender` の呼び出しが多いので引数を増やさない。
        spawnFloor = floor;
        try { SpawnDefendersInner(FeaturesOf(floor), floor); }
        finally { spawnFloor = -1; }
    }

    private void SpawnDefendersInner(Dictionary<Vector2Int, Feature> src, int floorIndex)
    {
        foreach (var f in src.Values)
        {
            f.spawnTimer = 0f;
            f.spawnedThisWave = 0;
            f.bornThisWave = 0;
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
                // ⚠ 経験はここで配らない（F-4）。全階の守りを開幕に立てるようになったので、ここで配ると
                //   冒険者が一度も来ない階の配下まで満額を貰う。配るのは DungeonFloorManager.GrantWaveExp。
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
                // ⚠ 経験はここで配らない（F-4）。全階の守りを開幕に立てるようになったので、ここで配ると
                //   冒険者が一度も来ない階の配下まで満額を貰う。配るのは DungeonFloorManager.GrantWaveExp。
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
                // ⚠ 経験はここで配らない（F-4）。全階の守りを開幕に立てるようになったので、ここで配ると
                //   冒険者が一度も来ない階の配下まで満額を貰う。配るのは DungeonFloorManager.GrantWaveExp。
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

    /// <summary>
    /// その階の防衛体を撤収する。
    /// ⚠⚠ **降下では呼ばないこと**（F-2以降）。旧仕様は降りるたびに上の階の守りを消していたが、
    ///   縦の迷宮では上の階は生き続ける。呼ぶのは**波の終わり**だけ。
    /// </summary>
    public void DespawnDefenders(int floor)
    {
        var l = DefendersOf(floor);
        foreach (var go in l) if (go != null) Destroy(go);
        l.Clear();
    }

    /// <summary>全階の防衛体を撤収する（波の終わり）。</summary>
    public void DespawnAllDefenders()
    {
        foreach (var kv in defendersByFloor)
        {
            foreach (var go in kv.Value) if (go != null) Destroy(go);
            kv.Value.Clear();
        }
    }

    /// <summary>その階にまだ生きている防衛体の数（ウェーブ終了判定・UIが見る）。</summary>
    public int LivingDefenderCount(int floor)
    {
        int n = 0;
        foreach (var go in DefendersOf(floor)) if (go != null) n++;
        return n;
    }

    private void TickSpawners()
    {
        // 🏢 **全階のスポナーを回す**（F-2）。⚠ `features`（表示中の階）だけを回すと、
        //   見ていない階のスポナーが止まる＝「見ている間しか働かない設備」になる。
        foreach (var kv in featuresByFloor)
        foreach (var f in kv.Value.Values)
        {
            if (!IsNestLike(f)) continue;   // 🪺 ふつうの巣と 🏛️大巣（→ [[GreatWorkCatalog]]）
            // 🌿 環境と巣レベルで「何体まで・何秒おきに・どれだけ強く」が変わる（→ [[HabitatCatalog]]）
            int moss, spring, feed;
            CountHabitat(kv.Key, f.cell, out moss, out spring, out feed);
            int limit = NestPerWave(f, spring);
            if (f.spawnedThisWave >= limit) continue;
            f.spawnTimer += Time.deltaTime;
            if (f.spawnTimer >= spawnerInterval * HabitatCatalog.IntervalMult(moss))
            {
                f.spawnTimer = 0f;
                f.spawnedThisWave++;
                f.bornThisWave++;
                spawnFloor = kv.Key;   // ⚠ その階に湧かせる（戻すのは下の finally）
                // 🧟 スポナーは**置いたときに選んでいた種**を湧かせる（見た目も34種の1枚絵で揃う）。
                //    ⚠ 旧仕様は GddMap の4種からランダムな見た目にしていたので、
                //      「何が湧くのか」が盤から読めず、育成の幹（進化・図鑑）とも繋がっていなかった。
                //    ⚠ 湧いた個体は使い捨て（ロスターには載らない）。載せると無限に個体が増える。
                //      そのぶん強さは「その種の素の強さ × 世界水準のレベル」に抑える。
                // 🍖 餌場は**レベルの係数**に掛ける（新しい数字を作らない）
                float slv = MinionRoster.LevelMult(MinionRoster.SummonLevel()) * HabitatCatalog.LevelMult(feed);
                try
                {
                    var born = SpawnDefender(f.cell, 1f, 1f, null, f.minionIndex, false, slv);
                    if (born != null) bornByNest[born] = f;   // 🪺 誰の子かを覚える（波末に生存を数える）
                }
                finally { spawnFloor = -1; }
            }
        }
    }

    // ============ 🪺 巣（旧スポナー）と 🌿 環境 ============
    //  ⚠⚠ **巣は「置いて終わり」ではない。** 素は 2体/波と弱く、
    //    ①隣の環境（苔床＝速く／水源＝多く／餌場＝強く）と
    //    ②波をまたいで育つ巣レベル
    //    の2つで湧き方が変わる。どちらも**既にある3つの数**を動かすだけで、軸は増やしていない。
    //  ⚠ 環境は配置枠を食うので、罠・トーテム・隊とゼロサム。狭い盤では囲えない＝**広さの報酬**。

    /// <summary>巣レベルの上限。⚠ 上げすぎると「置いて放置」に戻る。</summary>
    public const int NestMaxLevel = 3;
    /// <summary>次のレベルに要る養分（＝湧かせた子のうち波末に生き残った数の累計）。</summary>
    public static int NestNutrientNeed(int level) { return 6 + (level - 1) * 8; }

    /// <summary>🪺🏛️ 湧かせる物か（ふつうの巣＝`Spawner` と、巨大施設の『大巣』）。</summary>
    private static bool IsNestLike(Feature f)
    {
        return f.type == FeatureType.Spawner
            || (f.type == FeatureType.GreatWork && f.trapKind == (int)GreatWorkCatalog.Kind.GreatNest);
    }
    private static bool IsGreatNest(Feature f)
    {
        return f.type == FeatureType.GreatWork && f.trapKind == (int)GreatWorkCatalog.Kind.GreatNest;
    }
    /// <summary>🌿 その巣に環境が届く距離。大巣だけ 1 マス広い。</summary>
    private static int ReachOf(Feature f)
    {
        return IsGreatNest(f) ? GreatWorkCatalog.GreatNestReach : HabitatCatalog.Reach;
    }

    /// <summary>
    /// 🌿 巣と環境の距離。
    /// ⚠⚠ **巨大施設は敷地の“いちばん近い辺”から測る。** 実体は左下の1マスに登録してあるので、
    ///   素直に中心セルから測ると 5×5 の建物では**左下の角にしか環境を置けない**（対角は8マス先）。
    ///   建物の形が見えているのに、効く場所が角だけなのは盤から読めない。
    /// </summary>
    private static int NestDist(Feature nest, Vector2Int c)
    {
        if (nest.type != FeatureType.GreatWork)
            return Mathf.Abs(nest.cell.x - c.x) + Mathf.Abs(nest.cell.y - c.y);
        int s = GreatWorkCatalog.Size;
        int dx = Mathf.Max(0, Mathf.Max(nest.cell.x - c.x, c.x - (nest.cell.x + s - 1)));
        int dy = Mathf.Max(0, Mathf.Max(nest.cell.y - c.y, c.y - (nest.cell.y + s - 1)));
        return dx + dy;
    }

    /// <summary>🪺 その巣が今の波に湧かせられる数。</summary>
    private int NestPerWave(Feature f, int spring)
    {
        int b = IsGreatNest(f) ? GreatWorkCatalog.GreatNestBasePerWave : NestBasePerWave;
        return b + (Mathf.Clamp(f.nestLevel, 1, NestMaxLevel) - 1) + HabitatCatalog.ExtraPerWave(spring);
    }

    /// <summary>🌿 巣の周り（マンハッタン `Reach` 以内）の環境を数える。⚠ 重ねがけは 2 まで。</summary>
    private void CountHabitat(int floor, Vector2Int at, out int moss, out int spring, out int feed)
    {
        int reach = HabitatCatalog.Reach;
        Feature self;
        bool haveSelf = FeaturesOf(floor).TryGetValue(at, out self);
        if (haveSelf) reach = ReachOf(self);
        moss = spring = feed = 0;
        foreach (var h in FeaturesOf(floor).Values)
        {
            if (h.type != FeatureType.Habitat) continue;
            int d = haveSelf ? NestDist(self, h.cell)
                             : Mathf.Abs(h.cell.x - at.x) + Mathf.Abs(h.cell.y - at.y);
            if (d > reach) continue;
            switch ((HabitatCatalog.Kind)h.habitatKind)
            {
                case HabitatCatalog.Kind.Moss: moss++; break;
                case HabitatCatalog.Kind.Spring: spring++; break;
                default: feed++; break;
            }
        }
        moss = Mathf.Min(moss, HabitatCatalog.MaxStack);
        spring = Mathf.Min(spring, HabitatCatalog.MaxStack);
        feed = Mathf.Min(feed, HabitatCatalog.MaxStack);
    }

    /// <summary>🪺 巣の説明（盤に乗せたときの1行）。→ [[GridInputHandler]]</summary>
    public string NestLineAt(int floor, Vector2Int cell)
    {
        var dict = FeaturesOf(floor);
        Feature f;
        if (!dict.TryGetValue(cell, out f) || f.type != FeatureType.Spawner) return "";
        int moss, spring, feed;
        CountHabitat(floor, cell, out moss, out spring, out feed);
        string line = HabitatCatalog.NestLine(moss, spring, feed, f.nestLevel,
            NestPerWave(f, spring), spawnerInterval * HabitatCatalog.IntervalMult(moss));
        if (f.nestLevel < NestMaxLevel)
            line += "　<color=#6f6889>養分 " + f.nutrient + "/" + NestNutrientNeed(f.nestLevel) + "</color>";
        return line;
    }

    /// <summary>🌿 環境の説明（盤に乗せたときの1行）。⚠ **どの巣に効いているか**まで書く。</summary>
    public string HabitatLineAt(int floor, Vector2Int cell)
    {
        var dict = FeaturesOf(floor);
        Feature f;
        if (!dict.TryGetValue(cell, out f) || f.type != FeatureType.Habitat) return "";
        int served = 0;
        foreach (var n in dict.Values)
        {
            if (!IsNestLike(n)) continue;   // 🏛️ 大巣も数える
            if (NestDist(n, cell) <= ReachOf(n)) served++;
        }
        var d = HabitatCatalog.Get(f.habitatKind);
        return "🌿 <color=" + d.colorHex + ">" + d.jpName + "</color> ― " + d.desc
             + (served > 0 ? "　<color=#5cc47c>巣 " + served + " に効いている</color>"
                           : "　<color=#e05a5a>近くに巣が無い（" + HabitatCatalog.Reach + "マス以内に置く）</color>");
    }

    /// <summary>🪺 誰の巣から湧いたか。⚠ 波の終わりに生存を数えて養分にする。</summary>
    private readonly Dictionary<ZombieAI, Feature> bornByNest = new Dictionary<ZombieAI, Feature>();

    /// <summary>
    /// 🪺 **波の終わり：湧かせた子のうち生き残った数だけ巣が育つ。**
    /// ⚠⚠ 「置いて終わり」を壊すのがこの1手。**湧かせた子が生き延びる盤**を作る動機になる。
    /// ⚠ `EndBattlePhase` から1回だけ呼ぶ。
    /// </summary>
    public void NestGrowAtWaveEnd()
    {
        int grew = 0, survived = 0;
        foreach (var kv in bornByNest)
        {
            if (kv.Key == null || !kv.Key.gameObject.activeInHierarchy) continue;   // 倒された子は数えない
            if (kv.Value == null) continue;
            kv.Value.nutrient++; survived++;
        }
        bornByNest.Clear();
        foreach (var dict in featuresByFloor.Values)
        foreach (var f in dict.Values)
        {
            if (!IsNestLike(f)) continue;   // 🏛️ 大巣も同じように育つ
            f.bornThisWave = 0;
            while (f.nestLevel < NestMaxLevel && f.nutrient >= NestNutrientNeed(f.nestLevel))
            { f.nutrient -= NestNutrientNeed(f.nestLevel); f.nestLevel++; grew++; }
        }
        if (grew > 0)
            NotifySystem.Push("<b>巣が育った</b> ― " + grew + " つの巣が次の波からもっと湧かせる", NotifySystem.Kind.Gain);
        if (survived > 0)
            Debug.Log("🪺『巣の養分』生き残った子 " + survived + " 体ぶん（育った巣 " + grew + "）");
    }

    private ZombieAI SpawnDefender(Vector2Int cell, float hpMult, float atkMult, Color? tint, int minionIndex, bool guardian = false, float squadMult = 1f, float scale = 1f, float extraHpMult = 1f, float extraAtkMult = 1f)
    {
        if (zombiePrefab == null)
        {
            var input = Object.FindFirstObjectByType<GridInputHandler>();
            if (input != null) zombiePrefab = input.ZombiePrefab;
        }
        // 🏢 生成先の階の盤に置く（表示していない階にも湧く。→ [[DungeonGridSystem]]）
        int sf = SpawnFloorIndex;
        var sgrid = GridOf(sf);
        if (zombiePrefab == null || sgrid == null) return null;

        var def = MinionCatalog.Get(minionIndex);   // 🧟 配下個体の定義（役割/hp・atk・spd倍率）
        var species = def.family;                    // 家系（相性/プロファイル/リグ）

        var go = Instantiate(zombiePrefab, sgrid.GridToWorld(cell.x, cell.y), Quaternion.identity);
        var z = go.GetComponent<ZombieAI>();
        // ⚠ 生成した瞬間に自分の階を教える。教えないと `Start` で `Active` を読んでしまい、
        //   「B1Fで生成したのにB2Fの盤で経路を引く」ことになる（→ F-3 で AI 側も同じ形に）。
        if (z != null) z.BindFloor(sf);
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
        DefendersOf(sf).Add(go);
        return z;
    }

    // 🪦 不死の機械的個性：とどめを刺された不死の位置に弱い骸(スケルトン)を1体再生成（連鎖しない）
    /// <param name="floor">🏢 **倒れた不死が居た階**（F-2）。⚠ 省略すると表示中の階に湧く。
    /// 実測：B2Fで倒れた不死が、B3Fを見ているときに**B3Fに蘇っていた**。</param>
    public void RaiseUndead(Vector2Int cell, int floor = -1)
    {
        int rf = floor >= 0 ? floor : ActiveFloorIndex;
        var g = GridOf(rf);
        if (zombiePrefab == null)
        {
            var input = Object.FindFirstObjectByType<GridInputHandler>();
            if (input != null) zombiePrefab = input.ZombiePrefab;
        }
        if (zombiePrefab == null || g == null) return;
        if (skeletonCatalogIndex < 0)
            for (int k = 0; k < MinionCatalog.Count; k++) if (MinionCatalog.Get(k).id == "skeleton") { skeletonCatalogIndex = k; break; }
        if (skeletonCatalogIndex < 0) return;
        spawnFloor = rf;
        ZombieAI z;
        try { z = SpawnDefender(cell, raisedHpMult, raisedAtkMult, new Color(0.5f, 0.9f, 0.6f), skeletonCatalogIndex, false, 1f); }
        finally { spawnFloor = -1; }
        if (z != null) z.isRaised = true; // 再生成体は連鎖再生成しない
        BattleVfx.Burst(g.GridToWorld(cell.x, cell.y), new Color(0.5f, 0.9f, 0.6f, 1f), 0.9f);
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
        // 🏢 全階まとめて撤収（F-2）。⚠ 階を指定して呼ぶと他の階の守りが残り続ける。
        DespawnAllDefenders();
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

    /// <summary>
    /// 🗿 その階に置いてあるトーテムの**位置と種類**を集める（→ [[TotemRangeView]] が範囲を描くのに使う）。
    /// ⚠ `FeaturesOf` を公開せずに済ませるための窓口。渡したリストに追記する（毎フレーム new しない）。
    /// </summary>
    public void CollectTotems(int floor, List<Vector2Int> cells, List<int> kinds)
    {
        if (cells == null) return;
        foreach (var f in FeaturesOf(floor).Values)
        {
            if (f.type != FeatureType.Totem) continue;
            cells.Add(f.cell);
            if (kinds != null) kinds.Add(f.trapKind);
        }
    }

    /// <summary>
    /// 🛑 その階に置いてある要素の数（表示している階でなくても数えられる）。
    /// ⚠ `PlacedCount` は**表示中の階**しか見ない。突入前の点検は全階を見る必要がある
    ///   （→ `DungeonTurnManager.StartBattlePhase`）。
    /// </summary>
    public int PlacedCountOf(int floor) { return FeaturesOf(floor).Count; }
    /// <summary>🪩 いま表示中の階の巣の数（進言が読む）。</summary>
    public int NestCount { get { int n = 0; foreach (var f in features.Values) if (f.type == FeatureType.Spawner) n++; return n; } }
    /// <summary>🌿 同じく環境の数。</summary>
    public int HabitatCount { get { int n = 0; foreach (var f in features.Values) if (f.type == FeatureType.Habitat) n++; return n; } }

    /// <summary>
    /// 🛡️ **全階ぶんの守りの厚み**（W-2「捌く用意」が読む）。
    /// ⚠ `PlacedCount` も `NestCount` も**表示中の階しか見ない**。
    ///   「いまの守りで捌けるか」は迷宮ぜんたいの話なので、必ず全階を足す。
    /// </summary>
    public void TotalPlacement(out int used, out int cap, out int nests)
    {
        used = 0; cap = 0; nests = 0;
        var fm = DungeonFloorManager.Instance;
        int n = fm != null ? fm.BuiltFloorCount : 1;
        for (int i = 0; i < n; i++)
        {
            var list = FeaturesOf(i);
            used += list.Count;
            if (fm != null) cap += fm.PlacementCap(i);
            foreach (var r in list.Values) if (r.type == FeatureType.Spawner) nests++;
        }
    }

    /// <summary>重ねがけの上限（これ以上重ねても効かない）。⚠ 表示の濃さもここで止める。</summary>
    public int TotemMaxStack { get { return totemBuffMaxStack; } }

    /// <summary>指定セルの範囲内にある、その種類のトーテムの合計値（重ねがけ上限 totemBuffMaxStack）。</summary>
    public float TotemSum(Vector2Int cell, TotemCatalog.Kind kind) { return TotemSum(SpawnFloorIndex, cell, kind); }

    /// <summary>⚠ 階を跨がないこと。トーテムは**同じ階の範囲**にしか効かない。</summary>
    public float TotemSum(int floor, Vector2Int cell, TotemCatalog.Kind kind)
    {
        int n = 0; float v = 0f;
        foreach (var f in FeaturesOf(floor).Values)
        {
            if (f.type != FeatureType.Totem || f.trapKind != (int)kind) continue;
            var d = TotemCatalog.Get(f.trapKind);
            // 🗿 盤の広さに追随させる（→ [[TotemCatalog.EffectiveRadius]]）。⚠ 空間タイプの補正も向こうで足す
            var gg = GridOf(floor);
            int radius = TotemCatalog.EffectiveRadius(d.radius, gg != null ? gg.CurrentPlayableSize : 20);
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
        // 🏢 どの階の話かは**座標から逆引きする**（→ [[DungeonGridSystem.FloorAtWorld]]）。
        //   `Active` で引くと、表示していない階の冒険者が「いま見ている階のトーテム」を拾う。
        int floor = DungeonGridSystem.FloorAtWorld(world);
        var g = fm.GridOf(floor);
        if (g == null) return 0f;
        return fm.TotemSum(floor, g.WorldToGrid(world), kind);
    }

    // ============ ヘルパー ============
    public int CostOf(FeatureType type)
    {
        int baseCost;
        switch (type)
        {
            case FeatureType.Totem: baseCost = totemCostDP; break;
            case FeatureType.Spawner: baseCost = spawnerCostDP; break;
            case FeatureType.Habitat: baseCost = HabitatCatalog.Get(selectedHabitatKind).dpCost; break;
            case FeatureType.GreatWork: baseCost = GreatWorkCatalog.Get(selectedGreatWorkKind).dpCost; break;
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
        switch (t) { case FeatureType.Totem: return "トーテム"; case FeatureType.Spawner: return "巣"; case FeatureType.Habitat: return "環境"; case FeatureType.GreatWork: return "巨大施設"; case FeatureType.Boss: return "ボスエリア"; case FeatureType.Squad: return "部隊"; case FeatureType.Trap: return "罠"; case FeatureType.BaitChest: return "宝箱"; default: return "特殊エネミー"; }
    }

    // ============ 🎨 配置マーカーの見た目（MarkerArt の手続きスプライト） ============
    // 隊/ボス＝主張を抑えた『四隅のかぎ括弧』（キャラを隠さない）。ボスは小さな王冠を追加。
    // トーテム＝石柱＋種類ごとの色とアイコン。スポナー＝渦。特殊敵＝菱形。
    private GameObject CreateMarker(Vector2Int cell, FeatureType type) => CreateMarker(cell, type, 0, -1);

    private GameObject CreateMarker(Vector2Int cell, FeatureType type, int kind, int individualId)
    {
        var go = new GameObject("Feature_" + type);
        go.transform.SetParent(transform, false);
        // 🏢 マーカーは**その階の盤の原点**に乗せる（→ [[DungeonGridSystem]] の FloorOrigin）
        go.transform.position = GridOf(SpawnFloorIndex).GridToWorld(cell.x, cell.y) + new Vector3(0, 0, -0.5f);

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
            case FeatureType.Habitat:
                // 🌿 環境は**主張を抑える**（巣と紛れないよう小さく、色だけで見分ける）
                AddSprite(go, MarkerArt.Hexagon(), HabitatCatalog.ColorOf(kind), 0.44f, 28, Vector3.zero);
                break;
            case FeatureType.GreatWork:
                BuildGreatWorkMarker(go, kind);
                break;
            default: // SpecialEnemy
                AddSprite(go, MarkerArt.Rhombus(), GOLD, 0.60f, 30, Vector3.zero);
                break;
        }
        return go;
    }

    /// <summary>
    /// 🏛️ 巨大施設の印。⚠ **敷地ぜんぶを塗る**（4×4）。1マスぶんの印にすると、
    ///   「なぜここに置けないのか」が盤から読めなくなる（残り15マスは見えない壁になる）。
    /// ⚠ 印は左下のマスに立っているので、中心は (Size-1)/2 マスぶん右上へずらす。
    /// </summary>
    private void BuildGreatWorkMarker(GameObject go, int kind)
    {
        int s = GreatWorkCatalog.Size;
        float off = (s - 1) * 0.5f;
        var col = GreatWorkCatalog.ColorOf(kind);
        var fill = col; fill.a = 0.16f;
        AddSprite(go, MarkerArt.Pixel(), fill, s, 24, new Vector3(off, off, 0.1f));
        AddSprite(go, MarkerArt.Bracket(), new Color(col.r, col.g, col.b, 0.75f), s * 0.92f, 27, new Vector3(off, off, 0f));
        var icon = kind == (int)GreatWorkCatalog.Kind.GreatNest ? MarkerArt.Portal() : MarkerArt.Obelisk();
        AddSprite(go, icon, col, 1.05f, 30, new Vector3(off, off, -0.05f));
        AddLabel(go, GreatWorkCatalog.Name(kind), col, new Vector3(off, off - (s * 0.5f) + 0.24f, -0.2f));
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
