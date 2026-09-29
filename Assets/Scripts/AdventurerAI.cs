using UnityEngine;
using System.Collections.Generic;

public class AdventurerAI : MonoBehaviour
{
    private DungeonGridSystem gridSystem;

    public enum Job { Warrior, Thief, Cleric, Mage }
    public enum Purpose { Explore, Conquer }

    [Header("Adventurer Job & Purpose")]
    private Job adventurerJob;
    private Purpose adventurerPurpose;

    [Header("Adventurer Status (Base)")]
    private float moveSpeed = 3f;
    private float maxHP = 100f;
    private float currentHP;

    private int adventurerLevel = 1;
    private int adventurerRank = 2;            // 🏅 冒険者ランク G(0)〜S(7)。世界の育ち(知名度＋脅威度)で上がる。
    public int AdventurerRank => adventurerRank;
    private int weaponGrade = -1, armorGrade = -1; // ⚔️🛡️ 装備グレード(EquipmentCatalog)。世界装備水準＋ランクで決定。
    public int WeaponGrade => weaponGrade;
    public int ArmorGrade => armorGrade;
    /// <summary>
    /// 🎁 **この個体の装備水準上昇ポイント**（→ [[gear-level-rework]]）。
    /// 自分より良い等級の宝箱を開けたぶんだけ差が積もり、<b>10 たまると等級が1つ上がる</b>（端数は持ち越し）。
    /// ⚠⚠ 差で積むので、追いついた瞬間に入りが 0 になる ＝ <b>個体の等級は撒いた等級を超えられない</b>。
    /// ⚠ 10 なのは「1ターンに何度も宝箱を開ける個体がいる」ため。**世界のプールではなく個体ごと**。
    /// </summary>
    private float gearPoints = 0f;
    private const float GearPointsPerGrade = 10f;
    public int GearGrade => weaponGrade;
    public Job CurrentJob => adventurerJob;
    // 🔮 この冒険者が使う魔法（魔法使い/聖職者のみ）。階級はランクで上がる。
    private MagicCatalog.Spell mySpell; private bool hasSpell;
    public string SpellLabel => hasSpell ? mySpell.jpName : "";
    private float regenPerSecond = 1.0f;

    [Header("Mana (MP) System")]
    private float maxMana = 100f;
    private float currentMana = 100f;
    // ⚠ 旧 0.75。魔術師は1撃20マナ・間隔1.0秒なので **5秒で枯れ、次の1発まで26.7秒** かかっていた。
    //   ＝戦闘開始5秒で敵の脅威が半減し、そこから一方的になる。→ [[CombatMath]] の実測
    private float manaRegenPerSecond = 1.7f;

    [Header("Combat Settings")]
    private float attackTimer = 0f;
    private float attackInterval = 1.0f;
    private float threatAtkMult = 1f; // 🕸️ 誘導経済：脅威度による攻撃倍率（Startで設定）
    private float carriedGear = 0f;   // 🎁 略奪した装備量（逃げ切ると敵陣を武装／倒すと回収）
    /// <summary>🎁 いま抱えている戦利品（→ [[LureEconomy]]）。頭上表示と『奪還』に使う。</summary>
    public float CarriedGear { get { return carriedGear; } }
    private TMPro.TextMeshPro lootLabel;   // 💰 頭上の「戦利品 ×N」。⚠ 中身が変わったときだけ書き換える
    private string lootSig;

    // 🪤 罠の状態異常：DoT(毒/炎/出血)・凍結(氷)・麻痺(電気=周期的な短停止)
    private float dotTimer, dotDps, dotTick;
    private float frozenTimer;
    private float paralyzeTimer, paralyzePulse;
    private bool isFighting = false;      

    private float healTimer = 0f;
    private float healInterval = 2.0f;

    [Header("Emotion Pool")]
    [SerializeField] private float currentJoy = 0f;
    [SerializeField] private float currentFear = 0f;

    [Header("Satisfaction (満足したら帰還)")]
    [Tooltip("通常部屋を探索したときの満足の微増")]
    [SerializeField] private float satisfyRoomGain = 1f;
    [Tooltip("宝箱を開けたときの満足")]
    [SerializeField] private float satisfyChestGain = 4f;
    [Tooltip("罠にかかったときの満足(恐怖体験)")]
    [SerializeField] private float satisfyTrapGain = 3f;
    [Tooltip("感情値(喜び/恐怖)からの満足への寄与係数")]
    [SerializeField] private float satisfyEmotionFactor = 0.1f;
    [Tooltip("満足の閾値レンジ(個体差)。超えると帰還する")]
    [SerializeField] private Vector2 satisfyThresholdRange = new Vector2(28f, 52f);
    private float satisfaction = 0f;
    private float satisfactionThreshold = 10f;
    [Tooltip("探索先を選ぶときの距離ペナルティ（大きいほど近場から順に食う＝広い階層ほど長く滞在する）")]
    [SerializeField] private float distanceFalloff = 0.08f;

    [Header("Visual Effects")]
    [SerializeField] private TextMesh emotionTextMesh; 
    private Coroutine emotionCoroutine;

    [Header("AI Logic Settings")]
    private Vector2Int startPos; 
    private Vector2Int currentGridPos;
    public Vector2Int CurrentGridPos => currentGridPos;
    public Purpose AdventurerPurpose => adventurerPurpose; // 🏢 降下判定用
    public bool IsRetreating => isRetreating;
    /// <summary>🕸️ 泳がせの構えで見逃した個体か（→ [[LureStance]]）。生還時の見返りに使う。</summary>
    private bool spared;
    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int pathIndex = 0;

    private bool isRetreating = false;
    private bool assaultingCore = false; // 👑 魔王の間で討伐中か
    private float conquerCoreAttraction = 200f; // 踏破者が門番排除後に核/階段へ向かう優先度（部屋/宝箱より高く）
    private float searchTimer = 0f;
    private float searchInterval = 0.5f; 

    private Vector2Int lastTriggeredTrapPos = new Vector2Int(-1, -1);

    // 🏢 縦の迷宮：この冒険者が居る階（→ [[DungeonGridSystem]]）。
    // ⚠⚠ `Active` を読んではいけない。降りなかった者は上の階に残って戦い続けるので、
    //   「表示している階」と「自分が居る階」は**別物**になる。
    private int myFloor = -1;
    // 🧭 **届く確率の記録**（数理設計 P2・系1③・段0）。⚠ 計測だけ。挙動は変えない。
    //   どこまで降りたか・なぜ退いたか・魔王を叩いたかを、終わりに `Telemetry.NoteAdventurerEnd` へ渡す。
    private int deepestFloor;
    private string retreatWhy = "";
    private bool hitLord;
    // 🗺️ 迷宮の噂（→ [[DungeonIntel]]）：何を見て帰ったか・地図のある階での振る舞い
    private bool sawTrap; private int minionHits;
    private int mapBoldFloor = -1; private bool mapBold;         // この階では満足して帰らない（地図で見どころを知っている）
    private Vector2Int mapRolledCell = new Vector2Int(-9, -9), mapAvoidCell = new Vector2Int(-9, -9);
    private void MarkRetreat(string why) { if (retreatWhy.Length == 0) retreatWhy = why; }
    private void NoteEnd(string outcome)
    {
        if (IsRaider) return;
        var fl = DungeonFloorManager.Instance;
        var dl = DemonLord.Instance;
        Telemetry.NoteAdventurerEnd(deepestFloor, fl != null ? fl.BuiltFloorCount : 1, dl != null ? dl.MyFloor : -1,
            outcome, outcome == "escaped" ? (retreatWhy.Length > 0 ? retreatWhy : "unknown") : "",
            adventurerLevel, adventurerRank, adventurerPurpose == Purpose.Conquer, adventurerJob.ToString(),
            DescendLevelNeed(deepestFloor + 1), hitLord);
    }
    public int MyFloor { get { return myFloor >= 0 ? myFloor : DungeonGridSystem.FloorAtWorld(transform.position); } }

    /// <summary>階を移す／教える。⚠ `RelocateTo` より**前**に呼ぶこと（盤が切り替わってから座標を置く）。</summary>
    public void BindFloor(int floor)
    {
        myFloor = Mathf.Max(0, floor);
        if (myFloor > deepestFloor) deepestFloor = myFloor;   // 🧭 どこまで降りたか（系1③・段0）
        var g = DungeonGridSystem.Of(myFloor);
        if (g != null) gridSystem = g;
    }

    private DungeonGridSystem ResolveMyGrid()
    {
        var g = DungeonGridSystem.Of(MyFloor);
        return g != null ? g : DungeonGridSystem.Active;
    }

    /// <summary>🏢 魔王がこの冒険者と同じ階に立っているか（＝ここが終着点か）。</summary>
    private bool LordIsHere
    {
        get
        {
            var dl = DemonLord.Instance;
            return dl != null && dl.IsAlive && dl.MyFloor == MyFloor;
        }
    }

    // 🗡️ 因縁（→ [[Nemesis]]）。0＝無名。名のある者は逃がすたびに強くなって戻る。
    private int nemesisId = 0;
    private bool fellIntoAbyss = false;   // 🕳️ 奈落を経験したか（這い上がって逃げると必ず名がつく）
    public int NemesisId => nemesisId;
    /// <summary>🕳️ 奈落へ落とされた印（`DungeonFloorManager.SendBelow` から）。</summary>
    public void NoteAbyss() { fellIntoAbyss = true; }

    // ══════════════ ⚔️ 侵入者モード（④-c2・遠征）══════════════
    /// <summary>
    /// 🗿 <b>遠征でこちらが送り込んだ配下</b>のとき、その個体ID（-1＝ふつうの冒険者）。
    ///
    /// ⚠⚠ <b>なぜ `AdventurerAI` を使い回すのか。</b>
    ///   `ZombieAI` は既に `AdventurerAI` を敵として殴り、`AdventurerAI` は既に `ZombieAI` を殴る。
    ///   遠征は<b>役が入れ替わるだけ</b>なので、この2つをそのまま向かい合わせれば
    ///   <b>戦闘の中身を1行も書き換えずに</b>成立する。侵入者用の新しいAIを別に書くと、
    ///   狙い・射程・気性・魔法・罠…と<b>同じものを2セット</b>持つことになり、必ず片方が古くなる。
    ///
    /// ⚠ 遠征の盤は階層 index 100 以降にあり、シーン全体を走査している処理の
    ///   ほとんどは `MyFloor` で絞ってあるので、こちらの迷宮の勘定には入らない。
    /// </summary>
    [HideInInspector] public int raiderIndividualId = -1;
    public bool IsRaider => raiderIndividualId >= 0;

    /// <summary>侵入者として立たせる。⚠ `Start` より前に呼ぶこと（`BindFloor` と同じ）。</summary>
    public void MakeRaider(int individualId) { raiderIndividualId = individualId; }

    /// <summary>
    /// 📈 盤上にいる冒険者の数（数理設計 P0：波の「稼働時間」を測るため）。
    /// ⚠ 遠征の侵入者（こちらの配下）は数えない。`MakeRaider` は Instantiate の後に呼ばれるので、
    ///   数えるのは `Start`（OnEnable では侵入者か分からない）。
    /// </summary>
    public static int LiveCount => Live.Count;
    /// <summary>📈 盤上の冒険者（侵入者を除く）。計測が「交戦中の数」を数えるのに使う。</summary>
    public static readonly List<AdventurerAI> Live = new List<AdventurerAI>();
    private bool countedLive;
    private void OnDestroy() { if (countedLive) { Live.Remove(this); countedLive = false; } }

    // ── 📈 交戦の観測（数理設計 P1・仕様 §2.2）──
    //   「交戦中」＝近接で戦っている、または直近1秒以内にダメージを受けた（罠・射手・魔法は歩いている相手にも当たる）。
    //   ⚠ 計測のためだけの値。ゲームの判定には使わない。
    private float lastHitAt = -99f;
    [System.NonSerialized] public float SpawnedAt;
    [System.NonSerialized] public bool ContactNoted;
    public bool Engaged => isFighting || Time.time - lastHitAt < 1f;

    private void Start()
    {
        if (!IsRaider) { Live.Add(this); countedLive = true; SpawnedAt = Time.time; }
        // 🏢 自分の階の盤（湧いた座標から逆引き。`BindFloor` 済みならそれを尊重）
        if (gridSystem == null) gridSystem = ResolveMyGrid();
        if (gridSystem == null) return;

        currentGridPos = gridSystem.WorldToGrid(transform.position);
        transform.position = gridSystem.GridToWorld(currentGridPos.x, currentGridPos.y);
        startPos = currentGridPos;

        if (emotionTextMesh != null)
        {
            emotionTextMesh.GetComponent<Renderer>().sortingOrder = 100;
            emotionTextMesh.gameObject.SetActive(false);
        }

        if (IsRaider) SetupAsRaider();
        else DetermineAdventurerStatus();
        TargetNextDestination();

        // 🎭 手続きキャラビジュアル（ジョブ別リグ）を生成し、旧スプライトは隠す
        var oldSr = GetComponent<SpriteRenderer>();
        if (oldSr != null) oldSr.enabled = false;
        var vgo = new GameObject("Visual");
        vgo.transform.SetParent(transform, false);
        visual = vgo.AddComponent<CharacterVisual>();
        if (IsRaider) InitRaiderVisual();
        else
            // 🎨 SPUM完成スプライト（職×ランクで装備が良くなる）。ロード失敗時は手続きリグ
            visual.InitSpum(SpumMap.AdventurerPath(adventurerJob, adventurerRank), RigOf(adventurerJob));
        visual.SetHP(maxHP > 0 ? currentHP / maxHP : 1f);
    }

    /// <summary>
    /// ⚔️ 侵入者の中身を、こちらの配下個体から作る。
    /// ⚠⚠ <b>`DetermineAdventurerStatus` を通してはいけない。</b>
    ///   あれは <see cref="WaveRoster"/> から<b>1件取り出す</b>ので、遠征に出すたびに
    ///   <b>こちらの迷宮に来るはずだった冒険者が1人消える</b>＝『先触れ』で予告した波と食い違う。
    /// </summary>
    private void SetupAsRaider()
    {
        var v = MinionRoster.Get(raiderIndividualId);
        var def = MinionCatalog.Get(v != null ? v.catalogIndex : 0);
        int lv = v != null ? v.level : 1;
        float lvMult = MinionRoster.LevelMult(lv);

        adventurerLevel = lv;
        adventurerPurpose = Purpose.Conquer;      // 🏯 最深部を目指す（＝主を討ちに行く）
        adventurerRank = Mathf.Clamp(Mathf.RoundToInt(def.tierCP / 8f), 0, 7);
        // 職は「殴り方」を決めるだけ。役割から素直に写す。
        adventurerJob = def.role == MinionCatalog.Role.Ranged ? Job.Thief
                      : (def.style == CharacterVisual.AttackStyle.Cast) ? Job.Mage : Job.Warrior;
        satisfactionThreshold = float.MaxValue;   // ⚠ 侵入者は「満足して帰る」をしない（討つか、倒れるか）
        nemesisId = 0;

        maxHP = 60f * def.hpMult * lvMult * MinionRoster.EquipHpMult(raiderIndividualId);
        currentHP = maxHP;
        // ⚠ 冒険者の火力は `attackPower` ではなく **`threatAtkMult`** を通る
        //   （与傷は `(10 + Lv×0.5) × threatAtkMult`）。Lv は既に基礎の項に入っているので、
        //   ここに掛けるのは**種の強さと装備**だけ。二重に Lv を掛けない。
        threatAtkMult = def.atkMult
                      * MinionRoster.EquipAtkMult(raiderIndividualId) * MinionRoster.TypeAtkMult(raiderIndividualId);
        moveSpeed = 3f * def.spdMult;
    }

    private void InitRaiderVisual()
    {
        var v = MinionRoster.Get(raiderIndividualId);
        int mi = v != null ? v.catalogIndex : 0;
        var def = MinionCatalog.Get(mi);
        var rt = def.family == ZombieAI.Species.Beast ? CharacterVisual.RigType.Beast
               : def.family == ZombieAI.Species.Demonkin ? CharacterVisual.RigType.Demonkin
               : CharacterVisual.RigType.Undead;
        // 🎨 見た目の優先順は `ZombieAI` と同じ（1枚絵 → 獣 → SPUM）。揃えないと
        //    同じ配下が迷宮では骸骨、遠征では別人という事故になる。
        var dt = MinionSprite.ByIndex(mi);
        if (dt != null)
        {
            visual.InitDungeonTale(dt, rt, 1f, false, SpumMap.MinionAlpha(mi));
            visual.SetDungeonTaleId(def.id);
        }
        else if (def.family == ZombieAI.Species.Beast && BeastMap.TryGet(mi, out var bd))
            visual.InitBeast(bd.prefab, rt, bd.scale, bd.faceLeft, false);
        else
            visual.InitSpum(SpumMap.MinionPath(mi), rt, 1f, false, SpumMap.MinionAlpha(mi));
    }

    private CharacterVisual.RigType RigOf(Job j)
    {
        switch (j)
        {
            case Job.Thief: return CharacterVisual.RigType.Thief;
            case Job.Cleric: return CharacterVisual.RigType.Cleric;
            case Job.Mage: return CharacterVisual.RigType.Mage;
            default: return CharacterVisual.RigType.Warrior;
        }
    }

    private CharacterVisual visual;

    // ===== 🌍 世界の育ち具合（式はここに一本化。UIの『世界水準』表示も同じものを使う） =====
    //  fame は逃がした人数の累積で、ウェーブ人数が turn に比例するため O(turn^2) で伸びる。
    //  そのまま線形に使うと強さが O(turn^2)〜O(turn^3) になり崖ができるので、対数で圧縮して
    //  ダンジョン側の伸び（個体Lv +1/戦＝turn線形）とオーダーを揃える。
    private static float RenownLog(int fame) => Mathf.Log(1f + Mathf.Max(0, fame) / 50f);

    public static float WorldTier(int turn, int fame, float threat)
        => Mathf.Clamp(turn * 0.10f + RenownLog(fame) * 0.9f + (threat - 1f) * 0.5f
            + DungeonFloorManager.RenownHeroRankBias
            + SurfaceMap.WorldTierBias             // 🗺️ 地上を広げるほど強い者が討伐に来る（対数＋上限1.2）
            + EraSystem.TierBias, 0f, 7f);         // ⏳ 時代が進むほど世が本気になる（胎動0／伸長+0.6／終焉+1.2）

    // ⚖️ 難易度は**伸びにだけ**掛ける（初期値の1は動かさない）。序盤から別ゲームにしないため。→ [[Difficulty]]
    public static float LevelBase(int turn, int fame) => 1f + (turn * 0.8f + RenownLog(fame) * 4f) * Difficulty.AdvPowerMult;

    // UI表示用：いまの世界水準と、来る冒険者の目安レベル
    public static float WorldTierNow()
    {
        int turn = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 1;
        int fame = DungeonResourceManager.Instance != null ? DungeonResourceManager.Instance.DungeonFame : 0;
        return WorldTier(turn, fame, LureEconomy.Threat);
    }
    public static int ExpectedLevelNow()
    {
        int turn = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 1;
        int fame = DungeonResourceManager.Instance != null ? DungeonResourceManager.Instance.DungeonFame : 0;
        return Mathf.RoundToInt(LevelBase(turn, fame) * 0.925f);
    }
    public static string RankLetter(int i) { string[] l = { "G", "F", "E", "D", "C", "B", "A", "S" }; return l[Mathf.Clamp(i, 0, 7)]; }

    // ===== 🪜 適性深度（C7後の改修）=====
    //  旧仕様は「湧いた全員が同じ階層を順に降りる」だったので、**強い冒険者も必ずB1Fで戦っていた**。
    //  そのせいで B1F が抜かれた瞬間に「B1Fでも止められなかった相手」が育っていない下層へ雪崩れ込み、
    //  落差が最大のところに最弱がいる、という構造になっていた。
    //  → 強い者は格下を**素通り**して潜り、弱い者は階段の前で**引き返す**。
    //    これで「浅い＝弱者を捌く関所／深い＝強者だけの本陣」に役割が分かれる。
    public int Level => adventurerLevel;
    /// <summary>残りHPの割合（0〜1）。気性『臆病』のとどめ狙いが見る（→ [[MinionTemperament]]）。</summary>
    public float HpFrac => maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;
    public float MaxHP => maxHP;

    // ============ 🩸 感情の刈り取り（→ [[EmotionHarvest]]）============
    //  ⚠⚠ この2つの値は **帰り着いたときにしか清算されない**（`GrantReturnReward`）。
    //    つまり倒すと丸ごと消える＝盤の上に「まだ誰の物でもない報酬」が歩いている。
    //    その正体を外から見えるようにして、いま取れるようにするのが刈り取り。
    public float JoyPool => currentJoy;
    public float FearPool => currentFear;
    public float EmotionPool => currentJoy + currentFear;

    /// <summary>いま刈ったら何DPか。⚠ 実際の清算（`ReapEmotion`）と**同じ式**を使う。</summary>
    public int PeekReapDp() { return Mathf.RoundToInt(EmotionPool * ReapBonus * PolicySystem.ChestDpMult); }
    private float ReapBonus { get { return 1.0f + (adventurerLevel * 0.03f); } }

    /// <summary>
    /// 🩸 溜まった感情をいま清算して 0 に戻す。⚠ **0 に戻すのを忘れると帰還時に二重取りになる。**
    /// ⚠ 帰還時（`GrantReturnReward`）と同じ式・同じ配り先にする（式が2箇所に散らない）。
    /// </summary>
    public int ReapEmotion()
    {
        int dp = PeekReapDp();
        if (DungeonResourceManager.Instance != null) DungeonResourceManager.Instance.AddDP(dp);
        var et = EmotionTreeManager.Instance;
        if (et != null)
        {
            if (currentJoy >= 1f) et.AddEmotion(EmotionTreeManager.Route.Joy, Mathf.RoundToInt(currentJoy * 0.25f));
            if (currentFear >= 1f) et.AddEmotion(EmotionTreeManager.Route.Despair, Mathf.RoundToInt(currentFear * 0.25f));
        }
        currentJoy = 0f; currentFear = 0f;
        return dp;
    }

    /// <summary>😱 見せしめの恐怖が伝わる。⚠ 増えた恐怖は**そのまま実り**になる（新しい数字は作らない）。</summary>
    public void AddFear(float amount)
    {
        if (amount <= 0f) return;
        currentFear += amount;
        PopUpEmotionText("恐怖…");
    }

    /// <summary>盤の帯に出す名前（等級・職・Lv）。</summary>
    public string Label
    {
        get { return RankLetter(adventurerRank) + "級 " + WaveRoster.JobName(adventurerJob) + " Lv" + adventurerLevel; }
    }
    /// <summary>🔔 おとりが鳴った：いまの経路を捨てて選び直す（→ [[Decoy]]）。
    /// ⚠ これを呼ばないと次の定期探索まで数秒動かず、「押したのに何も起きない」に見える。</summary>
    public void RetargetNow() { if (!isRetreating) TargetNextDestination(); }
    /// <summary>🗡️ 戦力の目安（HP×攻撃）。防衛体の CombatPower と同じ尺度。</summary>
    public float CombatPower => Mathf.Max(1f, maxHP * (12f * threatAtkMult * (1f + adventurerLevel * 0.05f)) * 0.01f);

    /// <summary>その階層へ降りるのに要るレベル。深いほど上位だけが降りる。</summary>
    public static int DescendLevelNeed(int floorIndex)
        => Mathf.Max(1, Mathf.RoundToInt(ExpectedLevelNow() * (0.85f + 0.25f * Mathf.Max(0, floorIndex - 1))));

    /// <summary>
    /// この冒険者はその階層まで潜る気があるか。
    /// ⚠⚠ **旧式（Lvの関所）は4層目より下を絶対に通さなかった**（2026-09-30・数理設計 P2・系1③）。
    ///   Lv＝基準×U(0.70,1.15) に対し、必要Lv＝基準×0.925×(0.85+0.25(f−1)) なので
    ///   降りられる割合は B2F 81%／B3F 30%／**B4F 以下 0%**。階を4層以上にした瞬間、魔王は届かれなくなり、
    ///   基準プレイヤーの「魔王の階まで届いた冒険者」は T31 以降 0.1%（実測 `p3s0_reach`・7,549人）。
    /// ⇒ **目標の深さ**に置き換える：強さの引きが上の者ほど深く狙い、因縁と討伐隊は最下層まで来る。
    ///   弱い者も目標までは降りる。そこで倒されるか逃げるかは**守りが決める**（関所で門前払いしない）。
    ///   台帳 `reach.depth_by_roll`＝0 で旧式に戻る（比較用）。
    /// </summary>
    public bool WillDescendTo(int floorIndex)
    {
        if (Balance.I("reach.depth_by_roll", 1) == 0) return adventurerLevel >= DescendLevelNeed(floorIndex);
        return floorIndex <= TargetFloor();
    }
    /// <summary>🧭 目標の階（0始まり）＝ floor( depthRoll^γ × 階数 )。γ が大きいほど深く狙う者が減る。</summary>
    public int TargetFloor()
    {
        var fl = DungeonFloorManager.Instance;
        int floors = fl != null ? fl.BuiltFloorCount : 1;
        float g = Balance.F("reach.depth_gamma", 1.5f) * DungeonIntel.DepthGammaMult;   // 🗺️ 深くを見た者が多いほど深く狙う
        int t = Mathf.FloorToInt(Mathf.Pow(Mathf.Clamp01(depthRoll), g) * floors);
        return Mathf.Clamp(t, 0, floors - 1);
    }
    private float depthRoll = 1f;   // 🧭 名簿から受け取る（名簿が無いときは最下層まで）

    private void DetermineAdventurerStatus()
    {
        int fame = 0;
        if (DungeonResourceManager.Instance != null) fame = DungeonResourceManager.Instance.DungeonFame;

        int turn = 1;
        if (DungeonTurnManager.Instance != null) turn = DungeonTurnManager.Instance.CurrentTurn;

        // ⚖️『成長オーダーの整合』ここが難易度カーブの心臓部。
        //  問題だったこと: fame は『逃がした人数 × 35』の累積で、ウェーブ人数自体が turn に比例して
        //  増えるため fame は O(turn^2) で伸びる。それを fame/40 のように *線形* に使うと
        //  冒険者の強さが O(turn^2)、さらに ランク×Lv×脅威度×装備 と掛け算で積まれて O(turn^3) 相当になり、
        //  『昨日まで余裕だったのに fame が伸びた途端に瞬殺される』という崖ができていた。
        //  ダンジョン側は 個体Lv+1/戦（＝turnに線形）なので、冒険者側も **turnに線形** へ揃える。
        //  → fame は対数で圧縮する（逓減）。fame 120→1.22 / 250→1.79 / 1800→3.64 / 5000→4.61。
        // レベル：turn線形 ＋ fame対数。振れ幅は基準値に比例させ、分散が turn とともに爆発しないようにする。
        // 🔮 **名簿から受け取る**（→ [[WaveRoster]]）。乱数は準備フェーズの頭で引き終えているので、
        //    『先触れ』で予告した通りの相手がそのまま出てくる。
        //    ⚠ 名簿が無い場合（ロード直後・デバッグ生成）だけ、その場で引く旧来の道に落ちる。
        WaveRoster.Entry pre; bool fromRoster = WaveRoster.TryTake(out pre);
        float satRoll; int preGearGrade = 0;
        if (fromRoster)
        {
            adventurerLevel = pre.level;
            adventurerPurpose = pre.purpose;
            adventurerJob = pre.job;
            adventurerRank = pre.rank;
            satRoll = pre.satisfyRoll;
            depthRoll = pre.depthRoll;      // 🧭 目標の深さ（→ `WillDescendTo`）
            nemesisId = pre.nemesisId;      // 🗡️ 名のある者か（→ [[Nemesis]]）
            preGearGrade = pre.gearGrade;   // 🎁 『先触れ』で見せた等級をそのまま着てくる
        }
        else
        {
            float lvBase = LevelBase(turn, fame);
            adventurerLevel = Mathf.Clamp(Mathf.RoundToInt(lvBase * Random.Range(0.70f, 1.15f)), 1, 100);
            adventurerPurpose = (Random.Range(0, 2) == 0) ? Purpose.Explore : Purpose.Conquer;
            adventurerJob = (Job)Random.Range(0, 4);
            // 🏅 冒険者ランク G〜S（8段）：世界が育つ(知名度Fame＋脅威度＋ターン)ほど高ランクが出やすい。
            //    ＝原作/CDO2の『冒険者がだんだん強くなる』を段階化。脅威度(誘導経済)とも連動＝泳がせるほど強敵が来る。
            float worldTier = WorldTier(turn, fame, LureEconomy.Threat);
            adventurerRank = Mathf.Clamp(Mathf.RoundToInt(worldTier + Random.Range(-1.6f, 1.1f)), 0, 7);
            satRoll = Random.Range(0f, 1f);
        }
        // 🕯️ 備え『偽りの気配』：最下層の匂いが消え、踏破目的の者が階段を見失う（→ [[WardSystem]]）
        if (WardSystem.ConquerBlinded) adventurerPurpose = Purpose.Explore;
        int rankIdx = adventurerRank;

        // 😌 満足閾値：探索目的は高め(長く楽しむ)、踏破目的は低め(早く帰る)
        satisfactionThreshold = Mathf.Lerp(satisfyThresholdRange.x, satisfyThresholdRange.y, satRoll)
                                * ((adventurerPurpose == Purpose.Explore) ? 1.25f : 0.8f)
                                * DungeonTheme.SatisfyThresholdMult;   // 🏔️ 迷路は長居する

        string[] rankLetter = { "G", "F", "E", "D", "C", "B", "A", "S" };
        // ⚖️ ランク差は『掛け算の軸のひとつ』でしかないので、以前(0.70〜3.30＝4.7倍差)は効きすぎだった。
        //    Lv・脅威度・装備と積み重なるため、ここは 2.8倍差 に圧縮する。
        float[] rankHp  = { 0.80f, 0.90f, 1.00f, 1.15f, 1.35f, 1.60f, 1.90f, 2.25f };
        float[] rankAtk = { 0.80f, 0.90f, 1.00f, 1.15f, 1.35f, 1.60f, 1.90f, 2.25f };
        float[] rankSpd = { 0.90f, 0.95f, 1.00f, 1.05f, 1.10f, 1.15f, 1.20f, 1.25f };
        Color[] rankCol =
        {
            new Color(0.78f, 0.78f, 0.80f), new Color(0.88f, 0.88f, 0.90f), // G/F 白灰
            new Color(0.45f, 0.85f, 0.55f), new Color(0.34f, 0.74f, 0.52f), // E/D 緑
            new Color(0.30f, 0.60f, 1.00f), new Color(0.56f, 0.55f, 1.00f), // C/B 青
            new Color(1.00f, 0.62f, 0.25f), new Color(1.00f, 0.25f, 0.25f)  // A/S 橙/赤
        };
        maxHP = 100f * rankHp[rankIdx];
        if (RelicManager.Instance != null) maxHP *= RelicManager.Instance.HeroHpMult; // 🏺 静寂の鈴：静かな迷宮ほど来る者が弱い
        maxHP *= MutationSystem.HeroHpMult;                                           // 🧬 世界の変異『鉄化』
        moveSpeed = 3.0f * rankSpd[rankIdx] * MutationSystem.HeroSpeedMult             // 🧬 世界の変異『韋駄天』
                    * IncidentSystem.HeroSpeedMult;                                  // ⚡ 異変『道を鈍らせる』
        float rankAtkMult = rankAtk[rankIdx];
        var sr = GetComponent<SpriteRenderer>(); if (sr != null) sr.color = rankCol[rankIdx];
        string rankTitle = rankLetter[rankIdx] + "級";

        // 🎭 職＝4アーキタイプ(挙動/リグは不変)。表示名は『階級ラダー』でランクに応じ基本職→上位職→最上位職へ変化。
        int nameTier = rankIdx <= 1 ? 0 : rankIdx <= 3 ? 1 : rankIdx <= 4 ? 2 : rankIdx <= 5 ? 3 : 4;
        string[][] classLadder =
        {
            new[] { "見習い戦士⚔️", "戦士⚔️", "剣士⚔️", "騎士⚔️", "英雄⚔️" },   // Warrior
            new[] { "こそ泥🎭", "盗賊🎭", "シーフ🎭", "暗殺者🎭", "アサシン🎭" }, // Thief
            new[] { "祈祷師🌿", "聖職者🌿", "司祭🌿", "聖騎士🌿", "大司教🌿" },   // Cleric
            new[] { "術見習い🔮", "魔術師🔮", "魔導士🔮", "賢者🔮", "大賢者🔮" }  // Mage
        };
        string jobName = classLadder[(int)adventurerJob][nameTier];
        switch (adventurerJob)
        {
            case Job.Warrior: maxHP *= 1.3f; moveSpeed *= WardSystem.WarriorSpeedMult; break;  // 戦士系は硬い／🜃 備え『軋む床』
            case Job.Mage: moveSpeed *= 1.1f; break; // 魔術系は素早い
        }

        // ⚔️🛡️ 装備グレード（素材ラダー）：ランク＋世界装備水準(gearLevel)で武器/防具の素材が決まる。
        //    逃がして装備を奪われるほど gearLevel が上がり、高グレードの武具を持つ勇者が来る（両刃の具体化）。
        // 🎁 ⚠ **武器と防具で別々に引かない。**「その個体の装備水準」は1つの数で、
        //    宝箱で上がるのもこの1つ（→ [[gear-level-rework]]）。2つあると points の行き先が決まらない。
        weaponGrade = fromRoster ? preGearGrade : EquipmentCatalog.GradeFromWorld(rankIdx, LureEconomy.GearLevel);
        armorGrade = weaponGrade;
        LureEconomy.NoteEntered();   // 🎁 波の決算の分母（速さ＝逃げ切り÷入場）

        float levelMultiplier = 1.0f + (adventurerLevel - 1) * 0.03f;
        maxHP *= levelMultiplier;
        maxHP *= LureEconomy.HeroHpMult;                              // 🕸️ 脅威度で硬く
        maxHP *= EquipmentCatalog.ArmorHpMult(armorGrade);           // 🛡️ 防具グレードで硬く
        // 🕸️🏅⚔️ 攻撃力＝脅威度×ランク×武器グレード（baseDmg/魔王ダメに乗算）
        threatAtkMult = LureEconomy.HeroAtkMult * rankAtkMult * EquipmentCatalog.WeaponAtkMult(weaponGrade);

        // 🗡️ **因縁の上乗せ**（→ [[Nemesis]]）。⚠ これは難易度カーブの外にある軸だが、
        //   伸びるのは**プレイヤーが取り逃がしたぶんだけ**で、上限もある（`GrowthCap`）。
        //   ＝勝手に難しくなるのではなく「自分が育てた敵」。1波に最大3体まで（→ [[WaveRoster]]）。
        var nem = nemesisId > 0 ? Nemesis.Get(nemesisId) : null;
        if (nem != null)
        {
            maxHP *= Nemesis.HpMult(nem);
            threatAtkMult *= Nemesis.AtkMult(nem);
            // 🎁 **奪ったものを抱えて現れる**（G-2）。⚠ 強さは1ミリも足していない ―― これは
            //   「討ち取れば取り返せる」という**見返りの持ち込み**であって、敵の性能ではない。
            //   頭上に「戦利品 N」が最初から出るので、大物だと**一目で分かる**（→ G-1）。
            carriedGear = nem.hoard;
            // 盤の上で一目で分かるようにする（ランク色より優先）。名は絵ではなく文字で出す。
            if (sr != null) sr.color = new Color(1.00f, 0.84f, 0.35f);
        }
        currentHP = maxHP;

        // ⚖️ 自己回復は**Lvから切り離す**。Lv40で毎秒1.04まで伸びていたので、
        //    低Lvの配下が削っても回復で戻り、「削れているのに倒せない」状態を作っていた。
        //    ランクだけに紐づけて、伸びの軸をひとつ減らす（→ [[difficulty-curve-orders]]）。
        regenPerSecond = 0.35f * (0.8f + rankIdx * 0.08f)            // G 0.30 → S 1.34 の 1/2.7 に圧縮
                         * WardSystem.HeroRegenMult;                 // 🌫️ 備え『静謐の霧』
        // ⏱️ 戦闘のテンポ（両陣営に同じ倍率）。→ [[CombatMath]]
        attackInterval *= CombatMath.TempoScale;
        healInterval *= CombatMath.TempoScale;
        // ⚠ 間隔を伸ばしたぶん、1秒あたりのマナ消費も減る。回復側も合わせないと
        //   「テンポを緩めたら術者が枯れなくなった」という別の歪みになるので、回復は据え置き。

        // 🔮 魔法：魔法使い/聖職者はランク相応の階級の魔法を修得（世界が育つほど高階級）
        //    ⚠ 名簿から来た場合は**名簿が引いた魔法**をそのまま使う（先触れの表示と食い違わせない）。
        if (fromRoster) { hasSpell = pre.hasSpell; mySpell = pre.spell; }
        else hasSpell = MagicCatalog.TryPickHeroSpell(adventurerJob, rankIdx, out mySpell);

        string purposeStr = (adventurerPurpose == Purpose.Explore) ? "探索" : "踏破";
        string equipStr = $"武器{EquipmentCatalog.Name(weaponGrade)}/防具{EquipmentCatalog.Name(armorGrade)}"
                        + (hasSpell ? "/魔法" + mySpell.jpName : "");
        if (nem != null)
        {
            PopUpEmotionText(Nemesis.DisplayName(nem) + " Lv." + adventurerLevel);
            NotifySystem.Push("<b>" + Nemesis.DisplayName(nem) + "</b> が戻ってきた（"
                + rankTitle + " " + jobName + " Lv." + adventurerLevel + "）", NotifySystem.Kind.Loss);
            SoundSystem.Play(SoundSystem.Sfx.Danger);
            Debug.Log($"🗡️『再来』<color=#e3a94a>{Nemesis.DisplayName(nem)}</color> {rankTitle} {jobName} Lv.{adventurerLevel}"
                + $"（逃走{nem.escapes}／恨み{nem.grudge}／HP×{Nemesis.HpMult(nem):0.00} 攻×{Nemesis.AtkMult(nem):0.00}）");
        }
        else
        {
            PopUpEmotionText($"{rankTitle} {jobName}[{purposeStr}] Lv.{adventurerLevel}");
            Debug.Log($"📢『パーティ突入』第 {turn} ターン ➡ <color=yellow>{rankTitle} {jobName} Lv.{adventurerLevel} ({purposeStr}目的) {equipStr}</color> が侵入！");
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        TickStatus(dt);                   // 🪤 罠の状態異常（DoT/凍結/麻痺）
        bool immobile = frozenTimer > 0f; // 凍結/麻痺中は行動不能

        RefreshLootLabel();   // 💰 誰が何を抱えて帰ろうとしているかを見せる（→ [[LureEconomy]]）

        if (currentHP < maxHP) currentHP = Mathf.Min(maxHP, currentHP + regenPerSecond * dt);
        if (adventurerJob == Job.Mage || adventurerJob == Job.Cleric || adventurerJob == Job.Thief)
            if (currentMana < maxMana) currentMana = Mathf.Min(maxMana, currentMana + manaRegenPerSecond * dt);

        if (immobile) return; // 凍結中は攻撃/移動/回復を停止（HP回復とMP再生は上で継続）

        HandleTacticalCombat();

        if (assaultingCore && !isRetreating) HandleCoreAssault();

        if (adventurerJob == Job.Cleric && !isRetreating)
        {
            healTimer += dt;
            if (healTimer >= healInterval) { healTimer = FrameTimer.Carry(healTimer, healInterval); ExecuteAreaHeal(); }
        }

        if (!isFighting)
        {
            if (currentPath == null || currentPath.Count == 0 || pathIndex >= currentPath.Count)
            {
                searchTimer += dt;
                if (searchTimer >= searchInterval) { searchTimer = FrameTimer.Carry(searchTimer, searchInterval); TargetNextDestination(); }
            }
            HandleMovement();
        }
    }

    // 🪤 罠の状態異常の進行
    private void TickStatus(float dt)
    {
        if (dotTimer > 0f)
        {
            dotTimer -= dt; dotTick += dt;
            if (dotTick >= 0.5f) { dotTick = FrameTimer.Carry(dotTick, 0.5f); TakeDamage(dotDps * 0.5f); } // 0.5秒ごとにDoT
        }
        if (frozenTimer > 0f) frozenTimer -= dt;
        if (paralyzeTimer > 0f)
        {
            paralyzeTimer -= dt; paralyzePulse += dt;
            if (paralyzePulse >= 1.0f) { paralyzePulse = FrameTimer.Carry(paralyzePulse, 1.0f); frozenTimer = Mathf.Max(frozenTimer, 0.35f); } // 周期的に短く停止
        }
    }

    // 🪤 踏んだ罠の種類に応じて状態異常を付与
    public void ApplyTrapStatus(int trapKind)
    {
        var d = TrapCatalog.Get(trapKind);
        // 🌟 感情『呪縛』 × 🏺 遺物『呪縛の鎖』で状態異常が長引く
        // ⚖️ DoTでは『持続倍率＝総ダメージ倍率』になるため、感情×遺物の掛け算をそのまま乗せると毒が突出する。
        //    加算で合成し、上限1.8倍に丸める（凍結/麻痺の足止めが長くなりすぎるのも防ぐ）。
        float durBonus = 0f;
        if (EmotionTreeManager.Instance != null) durBonus += EmotionTreeManager.Instance.TrapStatusDurMult - 1f;
        if (RelicManager.Instance != null) durBonus += RelicManager.Instance.StatusDurationMult - 1f;
        float dur = d.statusDur * Mathf.Min(1.8f, 1f + durBonus) * MutationSystem.StatusDurMult;   // 🧬 変異『不屈』
        switch ((TrapKind)trapKind)
        {
            case TrapKind.Poison: case TrapKind.Fire: case TrapKind.Bleed:
                dotDps = TrapCatalog.DotPerSecond(trapKind, maxHP); dotTimer = dur; dotTick = 0f; PopUpEmotionText(d.name + "!"); break;
            case TrapKind.Ice:
                frozenTimer = Mathf.Max(frozenTimer, dur); PopUpEmotionText("凍結!"); break;
            case TrapKind.Electric:
                paralyzeTimer = Mathf.Max(paralyzeTimer, dur); PopUpEmotionText("麻痺!"); break;
            case TrapKind.Pit:
                frozenTimer = Mathf.Max(frozenTimer, dur); PopUpEmotionText("転倒!"); break;   // 🕳️ 落ちて起き上がるまでの間
        }
    }

    /// <summary>
    /// 🕳️ 落とし穴で運ばれる。⚠ `RelocateTo` と違い **`startPos`（退却先＝入口）を書き換えない**。
    /// 落ちても帰り道は入口のままでないと、穴の底が「家」になってしまう。
    /// </summary>
    public void FallTo(Vector2Int cell)
    {
        if (gridSystem == null) gridSystem = ResolveMyGrid();
        if (gridSystem == null) return;
        currentGridPos = cell;
        transform.position = gridSystem.GridToWorld(cell.x, cell.y);
        currentPath.Clear(); pathIndex = 0;
        assaultingCore = false; isFighting = false;
        lastTriggeredTrapPos = cell;      // ⚠ 着地した先がまた罠でも、その場で二度踏みしない
        TargetNextDestination();
    }

    // 🏢 descent：突破時に次フロア入口へ再配置し、状態をリセットして侵攻を継続する
    public void RelocateTo(Vector2Int cell)
    {
        if (gridSystem == null) gridSystem = ResolveMyGrid();
        if (gridSystem == null) return;
        currentGridPos = cell;
        startPos = cell; // 退却先は新フロアの入口に更新
        transform.position = gridSystem.GridToWorld(cell.x, cell.y);
        currentPath.Clear();
        pathIndex = 0;
        assaultingCore = false;
        isRetreating = false;
        isFighting = false;
        TargetNextDestination();
    }

    // 👑 魔王の間に到達した踏破者が、魔王を攻撃する処理
    private void HandleCoreAssault()
    {
        // 門番ボスが(復)存在する場合は魔王討伐を中断（先に門番を倒す）
        // 🏢 門番は**自分の階**のものだけを見る（F-2）。他の階の門番で足止めされない
        if (ZombieAI.GetLivingGuardianOnFloor(MyFloor) != null) { assaultingCore = false; return; }

        // 🏢 **魔王が自分と同じ階に居るか**で判定する（F-2以降）。
        // ⚠ `IsPresent` は「盤の上に居るか」なので、どの階に居ても真になる。
        if (DemonLord.Instance == null || !LordIsHere) { assaultingCore = false; return; }

        if (!DemonLord.Instance.IsAlive)
        {
            assaultingCore = false;
            isRetreating = true; MarkRetreat("lord_down");
            PopUpEmotionText("👑討伐成功!");
            RetreatHome();
            return;
        }
        isFighting = true; // その場に留まって魔王を攻撃
        attackTimer += Time.deltaTime;
        if (attackTimer >= attackInterval)
        {
            attackTimer = FrameTimer.Carry(attackTimer, attackInterval);   // ⏱️ 端数を捨てない
            if (visual != null && DemonLord.Instance != null)
            {
                visual.FaceTowards(DemonLord.Instance.transform.position.x);
                if (adventurerJob == Job.Mage)
                {
                    visual.PlayAttack(CharacterVisual.AttackStyle.Cast);
                    BattleVfx.Projectile(visual.MuzzlePos(), DemonLord.Instance.transform.position, new Color(0.95f, 0.55f, 0.25f));
                }
                else visual.PlayAttack(adventurerJob == Job.Thief ? CharacterVisual.AttackStyle.Stab : CharacterVisual.AttackStyle.Swing);
            }
            float dmg = (15f + adventurerLevel * 0.8f) * threatAtkMult;
            // ⚔️ 玉座への一撃も職ごとに違う形で（ここが**いちばん見せ場**なので必ず出す）
            AttackFx.Play(
                adventurerJob == Job.Mage ? AttackFx.Kind.Magic
                : adventurerJob == Job.Thief ? AttackFx.Kind.Pierce
                : adventurerJob == Job.Cleric ? AttackFx.Kind.Blunt
                : AttackFx.Kind.Slash,
                DemonLord.Instance.transform.position, transform.position,
                adventurerJob == Job.Mage && hasSpell ? SpellColor() : AttackFx.HeroSteel);
            DemonLord.Instance.TakeDamage(dmg);
            hitLord = true;   // 🧭 計測
            PopUpEmotionText("⚔魔王討伐!");
            var et = EmotionTreeManager.Instance;
            if (et != null) { et.AddEmotion(EmotionTreeManager.Route.Thrill, 1); et.CountBossHit(); } // 興奮ツリー
        }
    }

    private void HandleTacticalCombat()
    {
        if (gridSystem == null) return;

        currentGridPos = gridSystem.WorldToGrid(transform.position);

        ZombieAI[] allZombies = Object.FindObjectsByType<ZombieAI>();
        List<ZombieAI> targetsInRange = new List<ZombieAI>();

        // ⚔️『射程調整』魔術師（遠距離）は 2.0f、それ以外の近接職は 1.0f に設定！
        float attackRange = (adventurerJob == Job.Mage) ? 2.0f : 1.0f;    

        foreach (ZombieAI zombie in allZombies)
        {
            if (zombie.IsDead) continue; 

            float worldDist = Vector3.Distance(transform.position, zombie.transform.position);
            if (worldDist > attackRange) continue;
            // 🏃 素通り：踏破目的で、相手が門番でなく、自分が十分に格上なら足を止めない。
            //    （殴られはするので素通りにコストは残る＝浅い階も無意味ではない）
            if (adventurerPurpose == Purpose.Conquer && !zombie.isGuardian
                && CombatPower > zombie.CombatPower * 2.2f) continue;
            targetsInRange.Add(zombie);
        }

        if (targetsInRange.Count > 0 && !isRetreating)
        {
            isFighting = true; 
            attackTimer += Time.deltaTime;
            if (attackTimer >= attackInterval)
            {
                attackTimer = FrameTimer.Carry(attackTimer, attackInterval);   // ⏱️ 端数を捨てない
                ExecuteJobSpecificAttack(targetsInRange);
            }
        }
        else
        {
            isFighting = false;
        }
    }

    private void ExecuteJobSpecificAttack(List<ZombieAI> targets)
    {
        // 💫 威圧：近くに威圧持ちの眷属が居ると与ダメージが下がる
        // 🗿 呪詛の像（トーテム）：範囲内の冒険者の攻撃が落ちる
        float curse = Mathf.Max(0.3f, 1f - DungeonFeatureManager.TotemSumAt(transform.position, TotemCatalog.Kind.Curse));
        float baseDmg = (10f + (adventurerLevel * 0.5f)) * threatAtkMult * curse
                        * DungeonTheme.HeroDamageMult                      // 🏔️ 洞窟は暗くて当たらない
                        * ZombieAI.IntimidateMultAt(transform.position);
        ZombieAI target = targets[0];
        Vector3 tp = target.transform.position;
        if (visual != null) visual.FaceTowards(tp.x); // 🎯 対象の方向を向く
        Color fire = hasSpell ? SpellColor() : new Color(0.95f, 0.55f, 0.25f);

        switch (adventurerJob)
        {
            case Job.Warrior:
                if (visual != null) visual.PlayAttack(CharacterVisual.AttackStyle.Swing);
                PopUpEmotionText("🪓なぎ払い!");
                // ⚔️ 範囲攻撃なので**当たった全員に**出す（誰が巻き込まれたかが読める）
                foreach (ZombieAI z in targets)
                {
                    AttackFx.Play(AttackFx.Kind.Slash, z.transform.position, transform.position, AttackFx.HeroSteel);
                    z.TakeDamageFromAdventurer(baseDmg);
                }
                break;

            case Job.Mage:
                if (currentMana >= 20f)
                {
                    currentMana -= 20f;
                    if (visual != null) visual.PlayAttack(CharacterVisual.AttackStyle.Cast);
                    PopUpEmotionText((hasSpell ? mySpell.jpName + "!" : "爆魔術!") + $"(MP:{Mathf.RoundToInt(currentMana)})");
                    // 🔮 各対象へ属性魔法弾（威力＝階級、眷属ファミリーの耐性で増減）
                    foreach (ZombieAI z in targets)
                    {
                        if (visual != null) BattleVfx.Projectile(visual.MuzzlePos(), z.transform.position, fire);
                        // 🔮 着弾は**属性の色**で染める（16属性ぶんの絵は作らない → [[AttackFx]]）
                        AttackFx.Play(AttackFx.Kind.Magic, z.transform.position, transform.position, fire);
                        z.TakeDamageFromAdventurer(baseDmg * SpellMultVs(z, 1.3f));
                    }
                }
                else
                {
                    // 🥊 MP切れ → 素手の弱攻撃（パンチモーション）
                    // ⚠ 旧 0.3。マナ切れの魔術師が**ほぼ無害**になって戦線が崩れる原因だった
                    if (visual != null) visual.PlayAttack(CharacterVisual.AttackStyle.Punch);
                    PopUpEmotionText("🥊素手(MP切れ)");
                    AttackFx.Play(AttackFx.Kind.Blunt, tp, transform.position, new Color(0.75f, 0.75f, 0.8f));
                    target.TakeDamageFromAdventurer(baseDmg * 0.55f);
                }
                break;

            case Job.Thief:
                if (visual != null) visual.PlayAttack(CharacterVisual.AttackStyle.Stab);
                PopUpEmotionText("🗡️バックスタブ!");
                // 🗡️ 刺突は**倍率が高い一撃**なので、色も鋭く（鋼ではなく紅寄り）
                AttackFx.Play(AttackFx.Kind.Pierce, tp, transform.position, new Color(1f, 0.72f, 0.72f));
                target.TakeDamageFromAdventurer(baseDmg * 2.2f);
                break;

            case Job.Cleric:
                // 🔮 聖光：不死・魔族に特効。MPがあれば聖句、無ければ鈍器で殴る。
                if (hasSpell && currentMana >= 15f)
                {
                    currentMana -= 15f;
                    if (visual != null) { visual.PlayAttack(CharacterVisual.AttackStyle.Cast); BattleVfx.Projectile(visual.MuzzlePos(), tp, fire); }
                    PopUpEmotionText(mySpell.jpName + "!");
                    AttackFx.Play(AttackFx.Kind.Magic, tp, transform.position, fire);
                    target.TakeDamageFromAdventurer(baseDmg * SpellMultVs(target, 1f));
                }
                else
                {
                    if (visual != null) visual.PlayAttack(CharacterVisual.AttackStyle.Swing);
                    PopUpEmotionText("叩き潰す!");
                    // 🔨 鈍器はなぎ払いと**別の形**にする（同じ Swing モーションでも武器が違う）
                    AttackFx.Play(AttackFx.Kind.Blunt, tp, transform.position, new Color(1f, 0.92f, 0.7f));
                    target.TakeDamageFromAdventurer(baseDmg);
                }
                break;
        }
    }

    // 🔮 魔法の対眷属倍率（階級の威力 × ファミリー耐性）。魔法を持たない場合は素の倍率。
    private float SpellMultVs(ZombieAI z, float fallback)
    {
        // 🜁 備え『魔封じの結界』：冒険者の魔法だけを半減させる（素手や斬撃には効かない）
        if (!hasSpell) return fallback;
        return mySpell.power * MagicCatalog.ResistMultVsMinion(mySpell.element, z.species) * WardSystem.HeroMagicMult;
    }
    private Color SpellColor()
    {
        Color c; ColorUtility.TryParseHtmlString(mySpell.colorHex, out c); return c;
    }

    private void ExecuteAreaHeal()
    {
        if (gridSystem == null) return;
        if (currentMana < 30f) return;
        // 🌫️ 備え『静謐の霧』：癒やしの声が届かない（詠唱そのものが空を切る）
        if (WardSystem.HealBlocked)
        {
            currentMana -= 10f;
            PopUpEmotionText("…声が届かない");
            return;
        }

        AdventurerAI[] allAdventurers = Object.FindObjectsByType<AdventurerAI>();
        bool playedEffect = false;

        foreach (AdventurerAI ally in allAdventurers)
        {
            if (ally == this) continue;
            // ⚠⚠ **セル座標は階をまたいで衝突する**（(5,5)は全階に在る）。階を見ないと
            //   B1Fの聖職者が B3F の仲間を回復してしまう。→ [[DungeonGridSystem]]
            if (ally.MyFloor != MyFloor) continue;
            int dist = Mathf.Abs(ally.currentGridPos.x - this.currentGridPos.x) + Mathf.Abs(ally.currentGridPos.y - this.currentGridPos.y);
            if (dist <= 2 && ally.currentHP < ally.maxHP)
            {
                ally.Heal(20f);
                playedEffect = true;
            }
        }

        if (playedEffect)
        {
            currentMana -= 30f;
            PopUpEmotionText($"✨広域ヒール!(MP:{Mathf.RoundToInt(currentMana)})");
            if (visual != null) { visual.PlayHeal(); BattleVfx.Burst(transform.position, new Color(0.5f, 0.9f, 0.5f), 0.5f); } // 詠者：回復モーション＋光輪
        }
    }

    public void Heal(float amount)
    {
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        PopUpEmotionText($"✨HP+{Mathf.RoundToInt(amount)}");
        if (visual != null) visual.SetHP(maxHP > 0 ? currentHP / maxHP : 1f);
        BattleVfx.Heal(transform.position); // 🌿 回復される側にエフェクト
    }

    /// <summary>
    /// 🔮 魔力が尽きているか（＝次の1発が撃てない）。術者だけが該当する。
    /// ⚠ 閾値は「1発ぶん」より少し上にする。ぴったりだと汲んだ直後にまた渇いて往復し続ける。
    /// </summary>
    private bool NeedsMana()
    {
        if (adventurerJob != Job.Mage && adventurerJob != Job.Cleric) return false;
        return currentMana < 35f;
    }

    private void TargetNextDestination()
    {
        if (gridSystem == null) return;

        if (currentHP <= maxHP * 0.3f)
        {
            if (!isRetreating)
            {
                isRetreating = true; MarkRetreat("hp");
                isFighting = false;
                assaultingCore = false;
                Debug.Log($"😱『退却』入り口へ逃走！");
            }
            RetreatHome();
            return;
        }

        if (isRetreating)
        {
            RetreatHome();
            return;
        }

        // 🔔 **おとり**：範囲内なら、どんな目的よりも優先してそこへ向かう（→ [[Decoy]]）。
        //   ⚠ 退却の判定より**後**に置く（帰る者を引き戻せると逃走が無意味になる）。
        //   ⚠ 踏破目的の直行も上書きする ―― 魔王への一直線から引き剥がせることが、この手の値打ち。
        {
            Vector2Int lureCell;
            if (Decoy.LureTarget(MyFloor, currentGridPos, out lureCell) && currentGridPos != lureCell)
            {
                assaultingCore = false;
                CalculatePathTo(lureCell);
                return;
            }
        }

        // 👑 踏破目的：門番ボス生存中はまず門番を、撃破後(or不在)は目標セルへ
        // 🏢 どちらも**自分の階**で判定する（F-2）
        ZombieAI guardian = ZombieAI.GetLivingGuardianOnFloor(MyFloor);
        bool corePresent = LordIsHere;   // 🏢 魔王が同じ階に居るか
        // 🎯 目標セル：最下層は魔王(DemonLordCell)、非最下層は下り階段(=BossCell)。
        //    ・ボス要素を置くとBossCellだけ更新されDemonLordCellと乖離するため、
        //      降下判定(FloorManagerはBossCellを見る)と必ず一致させる。ここがズレると
        //      『ボス撃破後に別セルへ向かって降下しない＝スタック』になる。
        Vector2Int coreCell = corePresent ? gridSystem.DemonLordCell : gridSystem.BossCell;

        if (adventurerPurpose == Purpose.Conquer && corePresent && guardian == null && currentGridPos == coreCell)
        {
            assaultingCore = true; // 門番不在/撃破 → 魔王の間で討伐開始
            currentPath.Clear();
            return;
        }

        Vector2Int bestTarget = new Vector2Int(-1, -1);
        float highestAttraction = -1f;

        if (adventurerPurpose == Purpose.Conquer)
        {
            if (guardian != null)
            {
                assaultingCore = false;          // 門番生存中は魔王を狙わない
                bestTarget = guardian.MyGridPos; // まず門番ボスを倒しに行く
                highestAttraction = 999f;        // 最優先
            }
            else
            {
                // 門番撃破後/不在 → 最下層なら魔王の間、それ以外は下り階段(=ボスセル)を最優先で目指す。
                // ⚠ ここを宝箱(魅力50)や部屋より低くすると、踏破者が寄り道して満足→退却し、
                //    『ボス撃破→次フロア』が発火しない。門番を排除したら核/階段へ確実に向かわせる。
                bestTarget = coreCell;
                highestAttraction = conquerCoreAttraction; // 部屋/宝箱を上回る高優先度
            }
        }

        // 🔮 **マナが尽きた術者は、踏破目的でも宝箱へ魔力を汲みに寄り道する。**
        //   ⚠ 旧仕様はマナが切れても素手で殴り続けるだけで、そこから戦線が一方的に傾いていた。
        //     寄り道させると術者が保ち、戦闘が最後まで拮抗する（→ [[CombatMath]]）。
        //     同時に「宝箱を置く＝敵を回復させる」という両刃が生まれる。
        bool thirsty = NeedsMana();

        // 探索目的の部屋/宝箱選び。踏破目的で門番排除後(core優先)は上書きしない。
        //   ⚠ ただし魔力を切らした術者だけは例外（上の thirsty）。
        bool conquerCommitted = (adventurerPurpose == Purpose.Conquer && guardian == null) && !thirsty;
        if (!conquerCommitted)
        {
            // 🗺️ 『魅力 ÷ 距離』で選ぶ＝近い順に食っていく。
            //    以前は全マップから最大魅力へ直行していたため、広い階層でも往復するだけで
            //    広さが滞在時間に化けなかった。距離を効かせることで、階層を広げるほど巡回が長くなる。
            for (int x = 0; x < gridSystem.MapWidth; x++)
            {
                for (int y = 0; y < gridSystem.MapHeight; y++)
                {
                    DungeonGridSystem.TileType t = gridSystem.GetTileType(x, y);
                    if (t == DungeonGridSystem.TileType.Room || t == DungeonGridSystem.TileType.TreasureChest || t == DungeonGridSystem.TileType.Trap)
                    {
                        GameObject roomObj = gridSystem.GetGridObject(x, y);
                        if (roomObj != null)
                        {
                            RoomData data = roomObj.GetComponent<RoomData>();
                            if (data == null || !data.IsTargetable()) continue;
                            int dist = Mathf.Abs(x - currentGridPos.x) + Mathf.Abs(y - currentGridPos.y);
                            float att = data.attraction;
                            // 🔮 渇いた術者には、魔力の入った宝箱が核より魅力的に見える
                            if (thirsty && data.roomType == RoomData.RoomType.TreasureChest && data.manaRestore > 0f)
                                att += conquerCoreAttraction + 60f;
                            float score = att / (1f + dist * distanceFalloff);
                            if (score > highestAttraction)
                            {
                                highestAttraction = score;
                                bestTarget = new Vector2Int(x, y);
                            }
                        }
                    }
                }
            }
        }

        if (bestTarget.x != -1) CalculatePathTo(bestTarget);
        else
        {
            if (currentGridPos != startPos)
            {
                isRetreating = true; MarkRetreat("no_target");
                CalculatePathTo(startPos);
            }
        }
    }

    /// <summary>
    /// 🏃 入口へ帰る。
    /// ⚠⚠ **すでに入口の上に立っている場合は、その場で清算して退場する。**
    ///   `CalculatePathTo` は `currentGridPos == target` なら**何もせずに返る**ので、
    ///   経路が張られず `OnReachedDestination` も呼ばれない ―― つまり**永久に突っ立つ**。
    ///   縦の迷宮で階を下りた直後（`RelocateTo` が `startPos` を新しい階の入口に書き換え、
    ///   本人はその入口に立っている）に退却を決めると必ずこれに嵌り、
    ///   波が制限時間いっぱいまで終わらなくなっていた（実測：1波あたり数十秒の空白）。
    /// </summary>
    private void RetreatHome()
    {
        if (currentGridPos == startPos)
        {
            GrantReturnReward();
            Destroy(gameObject);
            return;
        }
        CalculatePathTo(startPos);
    }

    private void CalculatePathTo(Vector2Int target)
    {
        if (currentGridPos == target) return;
        if (currentPath.Count > 0 && pathIndex < currentPath.Count && currentPath[currentPath.Count - 1] == target) return; 

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        queue.Enqueue(currentGridPos);
        cameFrom[currentGridPos] = currentGridPos;

        bool found = false;
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == target) { found = true; break; }

            foreach (Vector2Int dir in directions)
            {
                Vector2Int next = current + dir;
                if (cameFrom.ContainsKey(next)) continue;

                DungeonGridSystem.TileType tileType = gridSystem.GetTileType(next.x, next.y);
                bool isWalkable = (tileType != DungeonGridSystem.TileType.None || next == startPos);

                if (isWalkable)
                {
                    queue.Enqueue(next);
                    cameFrom[next] = current;
                }
            }
        }

        if (found)
        {
            currentPath.Clear();
            Vector2Int curr = target;
            while (curr != currentGridPos)
            {
                currentPath.Add(curr);
                curr = cameFrom[curr];
            }
            currentPath.Reverse();
            pathIndex = 0;
        }
        else currentPath.Clear();
    }

    private float moveCarry;   // ⏱️ マスに着いたフレームで余った移動量（次のフレームへ持ち越す）

    private void HandleMovement()
    {
        if (currentPath == null || pathIndex >= currentPath.Count) return;

        Vector3 targetWorldPos = gridSystem.GridToWorld(currentPath[pathIndex].x, currentPath[pathIndex].y);
        // 🗿 泥濘の碑（トーテム）：範囲内では足が遅くなる＝罠と防衛体に長く晒される
        float mire = Mathf.Max(0.4f, 1f - DungeonFeatureManager.TotemSumAt(transform.position, TotemCatalog.Kind.Mire))
                     * DungeonTheme.HeroSpeedMult;                          // 🏔️ 氷雪は足を取られる
        // ⏱️⚠⚠ **1フレームの移動量を使い切る**（数理設計 P1・2026-09-28）。
        //   前は「マスに着いたらそのフレームの残りの移動を捨てて止まる」形で、1フレームが粗いほど遅く歩いた
        //   （16倍では守りに接触するまでが4倍より約0.7秒長く、それだけで守りが有利になっていた）。
        //   ⚠ マスに着いたフレームは**そこで止まる**（マスごとの戦闘・罠の判定の順番は変えない）。
        //     捨てていた残りの移動量を**次のフレームへ持ち越す**（上限は1フレームぶん）＝平均の速さだけが正しくなる。
        float frameStep = moveSpeed * mire * Time.deltaTime;
        float step = frameStep + moveCarry;
        moveCarry = 0f;
        float dist = Vector3.Distance(transform.position, targetWorldPos);
        if (dist > step) transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, step);
        else
        {
            transform.position = targetWorldPos;
            moveCarry = Mathf.Min(step - dist, frameStep);
        }

        if (dist <= step)
        {
            currentGridPos = currentPath[pathIndex];
            pathIndex++;
            CheckRoomEffectAt(currentGridPos);
            if (pathIndex >= currentPath.Count) { moveCarry = 0f; OnReachedDestination(); }
        }
        else
        {
            Vector2Int instantGrid = gridSystem.WorldToGrid(transform.position);
            if (instantGrid != lastTriggeredTrapPos)
            {
                lastTriggeredTrapPos = new Vector2Int(-1, -1);
            }
        }
    }

    private void CheckRoomEffectAt(Vector2Int gridPos)
    {
        if (gridSystem == null) return;

        GameObject roomObj = gridSystem.GetGridObject(gridPos.x, gridPos.y);
        if (roomObj != null)
        {
            RoomData data = roomObj.GetComponent<RoomData>();
            if (data != null && data.CanExecuteEffect())
            {
                // 🗺️ **知られた罠を避ける**（地図 → [[DungeonIntel]]）。⚠ マスごとに1度だけ引く（毎フレーム引き直さない）。
                if (data.roomType == RoomData.RoomType.Trap && !IsRaider)
                {
                    if (gridPos != mapRolledCell)
                    {
                        mapRolledCell = gridPos;
                        if (Random.value < DungeonIntel.TrapAvoidChance(MyFloor)) { mapAvoidCell = gridPos; PopUpEmotionText("🗺️知っている罠"); }
                    }
                    if (gridPos == mapAvoidCell) return;
                }
                if (data.roomType == RoomData.RoomType.Trap && adventurerJob == Job.Thief)
                {
                    if (gridPos == lastTriggeredTrapPos) return;
                    lastTriggeredTrapPos = gridPos; 

                    if (currentMana >= 25f)
                    {
                        currentMana -= 25f; 

                        if (Random.Range(0, 100) < 80) 
                        {
                            PopUpEmotionText($"⚙️解除成功!(MP:{Mathf.RoundToInt(currentMana)})");
                            data.DisableTrapTemporarily(10.0f); 
                            return; 
                        }
                        else
                        {
                            PopUpEmotionText("💥解除失敗!!");
                        }
                    }
                    else
                    {
                        PopUpEmotionText("❌マナ不足解除不能!");
                    }
                }

                // 🎁 **宝箱の等級は開けた瞬間に決まる**（遅延解決）。盤の配置は生成時のまま触らない。
                //   ⚠ 侵入者（遠征先の盤に立っている側）は**こちらの迷宮の設定を使わない**。
                //     あちらの宝箱はあちらの持ち物で、こちらのつまみが効いてはいけない。
                int chestGrade = -1;
                if (data.roomType == RoomData.RoomType.TreasureChest && !IsRaider)
                {
                    chestGrade = TreasureGrades.RollCatalog(MyFloor);
                    EurekaTracker.OnChestOpened();
                }

                data.ExecuteEffect(); 
                // 💰 ⚠ **見返りは等級に比例させる**。しないと「等級1だけ撒く」が無条件の最適解になり、
                //    つまみそのものが意味を失う（旨い餌ほど敵が強くなる＝原作の誘導経済）。
                currentJoy += data.joyValue + (chestGrade >= 0 ? TreasureGrades.JoyOf(chestGrade) : 0f);
                currentFear += data.fearValue;

                if (data.roomType == RoomData.RoomType.TreasureChest && data.joyValue > 0) PopUpEmotionText("JOY!");
                else if (data.roomType == RoomData.RoomType.Trap && data.fearValue > 0) PopUpEmotionText("FEAR!");

                // 🌟 感情ツリーへ感情/カウンタ供給
                var et = EmotionTreeManager.Instance;
                if (et != null)
                {
                    if (data.roomType == RoomData.RoomType.TreasureChest) { et.AddEmotion(EmotionTreeManager.Route.Joy, chestGrade >= 0 ? TreasureGrades.EmotionOf(chestGrade) : 2); et.CountChest(); }
                    else if (data.roomType == RoomData.RoomType.Trap) { et.AddEmotion(EmotionTreeManager.Route.Despair, 2); et.CountTrap(); }
                }

                if (data.damageValue > 0 || data.roomType == RoomData.RoomType.Trap)
                {
                    float dmg = data.damageValue;
                    if (data.roomType == RoomData.RoomType.Trap)
                    {
                        // ⚖️ 固定値だけだと後半に腐るので『最大HP比』成分を足す（研究 d_trap_pow* で伸びる）
                        dmg = TrapCatalog.InstantDamage(data.trapKind, maxHP);
                        if (et != null) dmg *= et.TrapDamageMult; // 絶望ツリーで罠強化
                        if (RelicManager.Instance != null) dmg *= RelicManager.Instance.TrapDamageMult; // 🏺 遺物で罠強化
                        dmg *= 1f + DungeonFeatureManager.TotemSumAt(transform.position, TotemCatalog.Kind.Forge); // 🗿 業火の炉
                        pendingTrapDamage = true;
                        sawTrap = true;   // 🗺️ 罠を見た
                    }
                    TakeDamage(dmg);
                    if (data.roomType == RoomData.RoomType.Trap) ApplyTrapStatus(data.trapKind); // 🪤 種類に応じた状態異常
                }

                // 🕳️ 落とし穴：**倒すための罠ではなく、運ぶための罠**。ダメージのあとに位置を動かす。
                //   ⚠ 死んでいたら運ばない（`TakeDamage` で消えている可能性がある）。
                if (data.roomType == RoomData.RoomType.Trap && data.trapKind == (int)TrapKind.Pit && currentHP > 0)
                {
                    Vector2Int dest;
                    if (DungeonFeatureManager.TryGetPitLink(gridPos, out dest))
                    {
                        if (dest == DungeonFeatureManager.PitBelow)
                        {
                            PopUpEmotionText("落下…!");
                            var fmgr = DungeonFloorManager.Instance;
                            if (fmgr != null) { fmgr.SendBelow(this, gridPos); return; }   // ⚠ 眠るのでこの先は触らない
                        }
                        else
                        {
                            PopUpEmotionText("滑落!");
                            FallTo(dest);
                        }
                    }
                }

                // 😌『Ⅱ 満足値』部屋は微増、宝箱/罠は大きめ、感情でさらに加算
                float gain = satisfyRoomGain;
                if (data.roomType == RoomData.RoomType.TreasureChest)
                {
                    gain = satisfyChestGain;
                    // 🔮 魔力を汲む（術者でなくても回復はするが、意味があるのは術者だけ）
                    if (data.manaRestore > 0f && currentMana < maxMana)
                    {
                        currentMana = Mathf.Min(maxMana, currentMana + data.manaRestore);
                        PopUpEmotionText("魔力を汲んだ(MP:" + Mathf.RoundToInt(currentMana) + ")");
                    }
                    // 🎁 宝箱の戦利品を持ち出す（richなほど装備量大）／👁️ 備え『見張りの目』で持ち出せなくなる
                    carriedGear += (1f + data.joyValue * 0.05f) * WardSystem.LootMult
                                   * (chestGrade >= 0 ? TreasureGrades.RewardMult(chestGrade) : 1f);
                    // 🎁 **その個体の装備水準が上がる**（差ぶんだけ／10で+1段）。→ [[gear-level-rework]]
                    if (chestGrade >= 0) GainGearPoints(chestGrade);
                }
                else if (data.roomType == RoomData.RoomType.Trap) gain = satisfyTrapGain;
                gain += (data.joyValue + data.fearValue) * satisfyEmotionFactor;
                gain *= 1f + DungeonFeatureManager.TotemSumAt(transform.position, TotemCatalog.Kind.Panic); // 🗿 恐慌の面：早く満足して帰る
                satisfaction += gain;

                // 🗺️ 地図のある階では、見どころを知っているので満足して帰らない（階に入ったときに1度だけ引く）
                if (mapBoldFloor != MyFloor) { mapBoldFloor = MyFloor; mapBold = !IsRaider && Random.value < DungeonIntel.Map(MyFloor); }
                if (!isRetreating && !mapBold && satisfaction >= satisfactionThreshold)
                {
                    isRetreating = true; MarkRetreat("satisfied");
                    isFighting = false;
                    PopUpEmotionText("満足…帰ろう🚶");
                    Debug.Log($"😌『満足帰還』満足値 {satisfaction:F0}/{satisfactionThreshold:F0} 到達 → 入口へ帰還");
                    RetreatHome();
                }
            }
        }
    }

    private void OnReachedDestination()
    {
        if (isRetreating && currentGridPos == startPos)
        {
            GrantReturnReward();
            Destroy(gameObject);
            return;
        }
        TargetNextDestination();
    }

    /// <summary>
    /// 🎁 <b>宝箱を開けて、その個体の装備水準が上がる</b>（装備水準の作り直しの心臓）。
    ///
    /// <para>
    /// 入るのは <c>max(0, 宝箱の等級 − いまの自分の等級)</c>。⚠⚠ <b>差</b>なので、
    /// 等級3の相手が等級1の宝箱を開けても<b>1ポイントも入らない</b>し、
    /// 等級10の宝箱を開けても<b>その場で等級10にはならない</b>（10ポイントで1段ずつ）。
    /// </para>
    /// <para>
    /// ⚠ 等級が動いたら硬さと攻撃も動かす。**比で掛け直す**こと（基準値を持ち回すと、
    ///   途中で掛かった他の倍率［脅威度・因縁・変異］を巻き戻してしまう）。
    /// ⚠ 現在HPも同じ比で伸ばす（最大HPだけ伸ばすと「拾った瞬間に相対的に瀕死」になる）。
    /// </para>
    /// </summary>
    private void GainGearPoints(int chestGrade)
    {
        int diff = chestGrade - weaponGrade;
        if (diff <= 0) return;
        gearPoints += diff;
        if (gearPoints < GearPointsPerGrade) return;

        int up = Mathf.FloorToInt(gearPoints / GearPointsPerGrade);
        gearPoints -= up * GearPointsPerGrade;
        int before = weaponGrade;
        // ⚠ 拾った宝箱の等級は超えない（差で積む式と辻褄を合わせる）
        int after = Mathf.Min(chestGrade, before + up);
        if (after == before) return;

        float hpRatio = EquipmentCatalog.ArmorHpMult(after) / Mathf.Max(0.01f, EquipmentCatalog.ArmorHpMult(before));
        float atkRatio = EquipmentCatalog.WeaponAtkMult(after) / Mathf.Max(0.01f, EquipmentCatalog.WeaponAtkMult(before));
        weaponGrade = armorGrade = after;
        maxHP *= hpRatio;
        currentHP = Mathf.Min(maxHP, currentHP * hpRatio);
        threatAtkMult *= atkRatio;
        PopUpEmotionText("装備が上がった！" + EquipmentCatalog.Name(after));
    }

    // 生還時の感情DP清算（帰還・強制退場で共通利用）。＝"逃がした"扱い→噂拡散で脅威度上昇。
    private void GrantReturnReward()
    {
        NoteEnd("escaped");
        if (!IsRaider)
        {
            var flI = DungeonFloorManager.Instance; var dlI = DemonLord.Instance;
            int floorsI = flI != null ? flI.BuiltFloorCount : 1;
            bool sawLord = dlI != null && dlI.MyFloor >= 0 && deepestFloor >= dlI.MyFloor;
            DungeonIntel.OnEscaped(deepestFloor, floorsI, sawTrap, minionHits >= 3, sawLord);   // 🗺️ 見てきたものを持ち帰る
        }
        float rewardBonus = 1.0f + (adventurerLevel * 0.03f);
        int earnedDP = Mathf.RoundToInt((currentJoy + currentFear) * rewardBonus * PolicySystem.ChestDpMult);   // 🏛️ 政策『撒き餌』
        int earnedFame = 10;
        if (DungeonResourceManager.Instance != null)
        {
            DungeonResourceManager.Instance.AddDP(earnedDP);
            DungeonResourceManager.Instance.AddFame(earnedFame);
        }
        LureEconomy.OnHeroEscaped(adventurerLevel); // 🕸️ 泳がせ：逃がすと噂が広まり脅威度↑＋Fame↑
        // 📊 **逃がした数を数える口はここだけ。** `EmotionTreeManager.CountEscape` は書いてあったのに
        //   どこからも呼ばれておらず、戦績の『逃がした数』が**常に0**だった（実測）。
        {
            var etEsc = EmotionTreeManager.Instance;
            if (etEsc != null) etEsc.CountEscape();
        }
        WaveReport.NoteEscape(carriedGear, spared);   // 📜 波の決算（→ [[WaveReport]]）
        // 🎁 **持ち逃げされたことを見せる**（G-1）。
        //   ⚠ 以前は装備水準が黙って上がるだけで、プレイヤーには**何も起きていないように見えていた**。
        //     取り返せなかったと分かるから、次に入口の手前で狩る意味が生まれる。
        // 🎁⚠⚠ **ここで世界水準を足さない。** 足すと「逃げた人数ぶんの和」に戻る＝直した壁がそのまま帰ってくる。
        //   控えるのは**その個体が着て出た等級**だけで、世界が動くのは波の終わり（`LureEconomy.SettleWave`）。
        LureEconomy.NoteEscapedGrade(weaponGrade);
        if (carriedGear >= 1f)
        {
            NotifySystem.Push("<b>持ち逃げされた</b> ― 戦利品 " + Mathf.RoundToInt(carriedGear)
                + "（" + TreasureGrades.Label(weaponGrade) + " を着て帰った）", NotifySystem.Kind.Loss);
        }
        // 🕸️ 構えで見逃した相手が帰り着いたときだけ研究点（→ [[LureStance]]）。
        //   ⚠ 手が回らずに逃げられたぶんには払わない。**選んだから見返りがある**。
        if (spared) LureStance.OnSparedReturned(adventurerLevel);

        // 🗡️ **取り逃がした者に名がつく**（→ [[Nemesis]]）。
        //   ⚠ 条件（半分以上削った／奈落から這い上がった）は Nemesis 側が持っている。
        //     ここで条件を書くと「名が生まれる規則」が2箇所に散る。
        int turnNow = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 1;
        nemesisId = Nemesis.OnEscaped(nemesisId, HpFrac, fellIntoAbyss,
            adventurerJob, adventurerRank, adventurerLevel, hasSpell, mySpell, turnNow, carriedGear);
    }

    // ⏱️『Ⅲ 安全網』時間切れ時：入口へ強制退却させる（歩いて帰り感情DPを清算）
    public void ForceRetreat() { ForceRetreat("forced"); }
    /// <param name="why">🧭 計測用の理由（stuck＝階段で降りられない／panic＝恐慌の波／timeout＝時間切れ／spared＝見逃し）。</param>
    public void ForceRetreat(string why)
    {
        if (isRetreating) return;
        MarkRetreat(why);
        isRetreating = true;
        isFighting = false;
        RetreatHome();
    }

    // ⏱️『Ⅲ ハード終了』猶予後もまだ残っている冒険者を感情DP清算して退場させる
    public void ForceDespawnWithReward()
    {
        GrantReturnReward();
        Destroy(gameObject);
    }

    private bool lastDamageWasTrap = false, pendingTrapDamage = false; // 🏺 実績『罠でとどめ』判定用
    private int lastKillerTemper = -1;                                 // 🧠 とどめを刺した配下の気性

    /// <param name="killerTemper">🧠 とどめを刺した配下の気性（-1＝配下以外。罠・魔王・号令）。
    /// 『貪婪』の撃破DPを乗せるためだけに要る（→ [[MinionTemperament]]）。</param>
    /// <summary>
    /// ⚠⚠ 撃破の処理を**1回だけ**にする印（2026-09-28・数理設計 P0 で発見）。
    ///   `Destroy` はフレームの終わりまで効かないので、同じフレームに2回目のダメージが来ると
    ///   下の撃破処理（撃破DP・素材・感情・天啓・撃破数・捕食）が**もう一度走っていた**。
    ///   実測：16倍速の428波のうち188波で「倒した数 ＞ 来た数」（超過873体）。4倍速でも起き得る。
    /// </summary>
    private bool deathHandled;

    public void TakeDamage(float damage, int killerTemper = -1)
    {
        if (deathHandled) return;   // ⚠ もう倒れている（同じフレームの2回目の攻撃）
        lastHitAt = Time.time;      // 📈 交戦の観測（計測だけ）
        if (killerTemper >= 0) minionHits++;   // 🗺️ 配下の群れを見た
        lastKillerTemper = killerTemper;
        lastDamageWasTrap = pendingTrapDamage; pendingTrapDamage = false;
        // 🛡️ 軽減（→ [[CombatMath]]）。⚠ **両陣営が同じ式を通る**ことでカーブの比を動かさない。
        damage = CombatMath.Apply(damage, CombatMath.HeroDefense(adventurerJob, adventurerLevel));

        // 🕸️ **泳がせの構え**（→ [[LureStance]]）。半分より下まで削った相手は**それ以上叩かない**。
        //   ⚠ damage を減らす形にする（HPを上げない）。ここで currentHP を代入で持ち上げると回復になる。
        //   ⚠ 入口は `TakeDamage` 1箇所だけ。配下・罠・魔王のどれから来ても同じ扱いにする
        //     （「今日は泳がせる」は迷宮全体の構えであって、誰が手を止めるかの話ではない）。
        if (LureStance.Active && !spared && !isRetreating && currentHP > 0f)
        {
            float floorHp = maxHP * LureStance.SpareBelow;
            if (currentHP - damage <= floorHp)
            {
                damage = Mathf.Max(0f, currentHP - floorHp);
                spared = true; MarkRetreat("spared");
                LureStance.NoteSpared();
                FloatText.Spawn(transform.position + new Vector3(0f, 0.95f, 0f), "見逃す",
                    new Color(0.62f, 0.82f, 1f), 2.4f, 0.8f, 1.0f);
                ForceRetreat();
            }
        }

        currentHP -= damage;
        // 💢 与えたダメージを数字で出す（Phase C-15）。
        //    以前は「残りHP」を1つのTextMeshで出していたので、**効いているのかが読めず**、
        //    連続で殴ると前の表示が消えていた。罠は色を変えて分かるようにする。
        // ⚠ **最大HPを渡す。** 「重い一撃」は乱数ではなく**実際に削った割合**で決まる（→ [[FloatText]]）。
        FloatText.Damage(transform.position + new Vector3(0f, 0.55f, 0f), damage, maxHP, lastDamageWasTrap);
        if (visual != null) { visual.SetHP(maxHP > 0 ? currentHP / maxHP : 0f); if (currentHP > 0) visual.PlayHurt(); }

        if (currentHP <= 0)
        {
            deathHandled = true;   // ⚠ ここから下は1回だけ（上の印を参照）
            // ⚔️⚠⚠ **侵入者（遠征に出したこちらの配下）は、この下の撃破処理を1つも通さない。**
            //   下は全部「**こちらの迷宮で冒険者を倒したときの見返り**」の並び ――
            //   生け捕り・因縁・撃破DP・素材・感情・実績・天啓・捕食・号令ゲージ・波の決算。
            //   自分の配下が他所のダンジョンで倒れたのに、これが走ったら
            //   **殺されるほどこちらが儲かる**という正反対のことが起きる。
            if (IsRaider)
            {
                Expedition.OnRaiderFell(raiderIndividualId);
                if (visual != null) visual.Die();
                Destroy(gameObject);
                return;
            }
            // ⛓️ **生け捕り**（→ [[Prison]]）。⚠ ここが天秤の支点：捕らえた場合は
            //   撃破DPも素材も感情も一切入らない。「今日はDPが要るのか、知識が要るのか」を毎波選ばせる。
            //   ⚠ 早期returnなので、以降の撃破処理（実績・天啓・捕食）も**通らない**。それが正しい。
            if (Prison.TryCapture(nemesisId, adventurerJob, adventurerRank, adventurerLevel, hasSpell, mySpell))
            {
                NoteEnd("captured");
                if (visual != null) visual.Die();
                Destroy(gameObject);
                return;
            }
            // 🗡️ 因縁の相手を仕留めた（報酬と通知は Nemesis 側で出す＝1箇所にまとめる）
            if (nemesisId > 0) Nemesis.OnSlain(nemesisId, transform.position);

            float killBonusMultiplier = 1.0f + (adventurerLevel * 0.05f);
            int killBonusDP = Mathf.RoundToInt(50 * killBonusMultiplier);
            int droppedMaterials = 1 + LureEconomy.GearRecoverMaterials(carriedGear); // 🎁 略奪者を倒すと戦利品を素材で回収（武装拡散を防ぐ）

            // 🌟 殺戮ツリー：撃破DP・素材ボーナス＋感情/カウンタ
            var et = EmotionTreeManager.Instance;
            if (et != null)
            {
                // 🗿 血の香炉：この場所で倒すと感情が濃く採れる
                float censer = 1f + DungeonFeatureManager.TotemSumAt(transform.position, TotemCatalog.Kind.Censer);
                et.AddEmotion(EmotionTreeManager.Route.Slaughter, Mathf.RoundToInt(3 * censer)); et.CountKill();
                killBonusDP = Mathf.RoundToInt(killBonusDP * et.KillDPMult);
                droppedMaterials += et.KillMaterialBonus;
            }
            if (DemonLord.Instance != null) droppedMaterials += DemonLord.Instance.RefineLootBonus; // 🔨 錬成ランクで戦利品が増える
            if (RelicManager.Instance != null) killBonusDP = Mathf.RoundToInt(killBonusDP * RelicManager.Instance.KillDPMult); // 🏺 遺物で撃破DP
            killBonusDP = Mathf.RoundToInt(killBonusDP * LureEconomy.RevenueMult); // 🕸️ 脅威度が高い(強い勇者)ほど撃破DPが旨い
            killBonusDP = Mathf.RoundToInt(killBonusDP * NarrativeSystem.KillDpMult); // 🕯️ 形見『血染めの首飾り』
            killBonusDP = Mathf.RoundToInt(killBonusDP * Difficulty.RewardMult);      // ⚖️ 難易度：厳しいほど取り分も増える
            // 🔥 大招集：自分で呼んだ嵐は旨い（→ [[FeverSystem]]）。⚠ そのターン限り
            killBonusDP = Mathf.RoundToInt(killBonusDP * FeverSystem.KillLootMult);
            droppedMaterials = Mathf.RoundToInt(droppedMaterials * FeverSystem.KillLootMult);
            // 🧠 気性『貪婪』：この個体がとどめを刺したときだけ撃破DPが増える（→ [[MinionTemperament]]）
            if (lastKillerTemper >= 0)
                killBonusDP = Mathf.RoundToInt(killBonusDP * MinionTemperament.Get(lastKillerTemper).killDpMult);

            // 🏢 深度ボーナス：深い階層で倒すほど旨い（浅い階で皆殺しにせず、深く誘い込む理由になる）
            float depth = DungeonFloorManager.CurrentDepthRewardMult;
            killBonusDP = Mathf.RoundToInt(killBonusDP * depth);
            droppedMaterials = Mathf.RoundToInt(droppedMaterials * depth);

            LordStance.OnSoulReaped(adventurerLevel, MyFloor);             // 🩸 魔王が在陣する階なら魂を喰らう（捕食値）
            RelicManager.ReportHeroBeaten(adventurerRank);                 // 🏺 実績：高ランク撃破
            EurekaTracker.OnAdventurerDefeated();                          // ⏳ 時代の偉業のカウント
            if (lastDamageWasTrap) { RelicManager.ReportTrapKill(); EurekaTracker.OnTrapKill(); }   // 🏺実績＋💡天啓：罠でとどめ
            if (hasSpell) EurekaTracker.OnMagicKill();                                              // 💡天啓：魔法持ちを倒した
            DungeonResourceManager.AddKillDPRecord(killBonusDP);           // 🏺 実績：撃破DPの累計

            if (DungeonResourceManager.Instance != null)
            {
                DungeonResourceManager.Instance.AddDP(killBonusDP);
                DungeonResourceManager.Instance.AddMaterial(droppedMaterials);
            }
            // 💥 撃破の手応え（→ [[KillFeedback]]）。⚠ **報酬が確定した後**に呼ぶ ―― 見せる数字と
            //    実際に入る数字がずれないように。⚠ 生け捕り（上の早期return）では呼ばれない。
            NoteEnd("killed");
            KillFeedback.OnKill(transform.position, killBonusDP, droppedMaterials, adventurerRank, nemesisId > 0);
            WaveReport.NoteKill(nemesisId > 0);   // 📜 波の決算（→ [[WaveReport]]）
            CommandCharge.OnKill(adventurerRank, nemesisId > 0);   // 📯 号令ゲージ（→ [[CommandCharge]]）
            // 🎁 **奪還**（G-1）。戦利品を抱えたまま倒した＝世界の装備水準に乗る前に取り返した。
            //   ⚠ 素材は既に `droppedMaterials` に含まれている。**ここでは1つも足さない**（見せるだけ）。
            //     演出のついでに報酬を足すと軸が1本増える → [[difficulty-curve-orders]]。
            // ⚠ 因縁のときは出さない ―― `Nemesis.OnSlain` の決着の帯と**二重になる**
            if (carriedGear >= 1f && nemesisId <= 0)
                KillFeedback.OnRecover(transform.position, LureEconomy.GearRecoverMaterials(carriedGear), isRetreating);
            if (visual != null) visual.Die(); // 🎭 倒れ演出（切り離して自壊。AI本体は即destroyでカウント整合）
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 💰 **頭上に「いま何を持って帰ろうとしているか」を出す**（G-1）。
    ///
    /// ⚠⚠ **なぜ要るか**：この値（`carriedGear`）は前からあったのに**どこにも出ていなかった**。
    ///   逃がせば `LureEconomy.OnGearEscaped` で世界の装備水準が上がり、
    ///   仕留めれば `GearRecoverMaterials` で素材として戻る ―― つまり
    ///   **「見逃すか、入口の手前で狩るか」が毎波の勝負どころ**なのに、
    ///   プレイヤーにはその賭けが**一度も見えていなかった**。
    ///
    /// ⚠ **数字は足していない。** 表示するだけ。ここでバランスは1ミリも動かない。
    /// ⚠ 中身が変わったときだけ書き換える（毎フレーム文字列を作らない → [[ui-conventions]]）。
    /// </summary>
    private void RefreshLootLabel()
    {
        int loot = Mathf.RoundToInt(carriedGear);
        if (loot <= 0)
        {
            if (lootLabel != null && lootLabel.gameObject.activeSelf) lootLabel.gameObject.SetActive(false);
            lootSig = null;
            return;
        }
        string sig = loot + (isRetreating ? "|r" : "|s");
        if (sig == lootSig && lootLabel != null) return;
        lootSig = sig;

        if (lootLabel == null)
        {
            var go = new GameObject("LootLabel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.15f, -1f);
            lootLabel = go.AddComponent<TMPro.TextMeshPro>();
            lootLabel.alignment = TMPro.TextAlignmentOptions.Center;
            lootLabel.enableWordWrapping = false;
            lootLabel.raycastTarget = false;
            lootLabel.fontStyle = TMPro.FontStyles.Bold;
            lootLabel.fontSize = 2.1f;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) { mr.sortingOrder = 480; mr.sortingLayerName = "Default"; }
            if (FloatText.Font != null) lootLabel.font = FloatText.Font;
        }
        lootLabel.gameObject.SetActive(true);
        // 🔴 逃げに入った瞬間から赤くする ―― **持ち出される寸前**であることが一目で分かるように
        lootLabel.color = isRetreating ? new Color(1f, 0.45f, 0.38f) : new Color(0.95f, 0.82f, 0.42f);
        lootLabel.text = (isRetreating ? "逃走 戦利品 " : "戦利品 ") + loot;
        lootLabel.fontSize = isRetreating ? 2.5f : 2.1f;
    }

    private void PopUpEmotionText(string text)
    {
        if (emotionTextMesh == null) return;
        if (emotionCoroutine != null) StopCoroutine(emotionCoroutine);
        emotionCoroutine = StartCoroutine(AnimateEmotion(text));
    }

    private System.Collections.IEnumerator AnimateEmotion(string text)
    {
        emotionTextMesh.text = text;
        emotionTextMesh.gameObject.SetActive(true);

        float timer = 0f;
        float duration = 1.4f; 
        Vector3 startLocalPos = new Vector3(0f, 0.8f, -1f); 

        while (timer < duration)
        {
            timer += Time.deltaTime;
            startLocalPos.y += 0.25f * Time.deltaTime;
            emotionTextMesh.transform.localPosition = startLocalPos;
            yield return null;
        }
        emotionTextMesh.gameObject.SetActive(false);
    }
}