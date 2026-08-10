using UnityEngine;
using TMPro;

public class DungeonTurnManager : MonoBehaviour
{
    public static DungeonTurnManager Instance { get; private set; }

    /// <summary>
    /// ⏳ 1ターンは **前半＝迷宮／後半＝地上** に分かれる。
    ///
    /// `Prepare`（迷宮の準備）→ `Battle`（防衛戦）→ `Surface`（地上フェーズ）→ 次のターンの `Prepare`。
    ///
    /// **なぜ分けたか**：以前は迷宮の準備中も戦闘中も地上を触れたので、
    /// どちらにも集中できず、地上の操作そのものを忘れる／面倒に感じる状態だった。
    /// 前半と後半で画面ごと切り替われば、そのフェーズのことだけ考えればよくなる。
    /// </summary>
    public enum Phase { Prepare, Battle, Surface }
    private Phase currentPhase = Phase.Prepare;

    private int currentTurn = 1;
    public int CurrentTurn => currentTurn;
    /// <summary>
    /// 「戦闘中ではない」＝操作を受け付けてよい時間。
    /// ⚠ 各systemの「準備フェーズのみ」ガードはすべてこれを見ている。
    ///   地上フェーズを別扱いにすると**地上フェーズ中に施設が建てられなくなる**ので、ここには含める。
    ///   迷宮と地上の切り分けは**そのフェーズでどちらの画面を出すか**で担保する。
    /// </summary>
    public bool IsPreparePhase => currentPhase != Phase.Battle;
    public bool IsBattlePhase => currentPhase == Phase.Battle;
    /// <summary>前半：迷宮の準備（配置・研究・図鑑）。</summary>
    public bool IsDungeonPhase => currentPhase == Phase.Prepare;
    /// <summary>後半：地上フェーズ（盤・軍団・眷属・施設・政策）。</summary>
    public bool IsSurfacePhase => currentPhase == Phase.Surface;
    public Phase CurrentPhase => currentPhase;
    /// <summary>「1ターン目 前半・迷宮」のような1行（HUDと地上の帯で同じものを使う）。</summary>
    public string PhaseLabel => currentPhase == Phase.Prepare ? "前半・迷宮"
        : currentPhase == Phase.Battle ? "前半・防衛戦" : "後半・地上";

    [Header("Wave Time Limit (Ⅲ 安全網)")]
    [Tooltip("戦闘フェーズの基本制限時間(秒)。序盤は3分=180")]
    [SerializeField] private float baseWaveSeconds = 180f;
    [Tooltip("延長1回あたりの秒数")]
    [SerializeField] private float extendSecondsPerUnlock = 60f;
    [Tooltip("延長1回のDPコスト")]
    [SerializeField] private int extendCostDP = 300;
    [Tooltip("時間切れ後、強制退場までの猶予(秒)")]
    [SerializeField] private float graceSeconds = 15f;
    private float waveBonusSeconds = 0f;
    private float battleElapsed = 0f;
    private bool forcedRetreatIssued = false;

    public float WaveTimeLimit => baseWaveSeconds + waveBonusSeconds;
    public float RemainingWaveTime => Mathf.Max(0f, WaveTimeLimit - battleElapsed);

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI turnDisplayText;
    [SerializeField] private GameObject startBattleButton; // 侵略開始ボタンのUI

    private float checkTimer = 0f;
    private float checkInterval = 0.5f; // 冒険者数の確認間隔（秒）

    // ⏩ 戦闘の速度（Phase A-5）。3分の防衛戦をただ見ているだけの時間を短くし、
    //    見せ場では止めて考えられるようにする。⚠ UIの演出は unscaledDeltaTime で動かすこと。
    public static readonly float[] Speeds = { 0f, 1f, 2f, 4f };
    public static readonly string[] SpeedNames = { "‖", "1x", "2x", "4x" };
    private int speedIndex = 1;
    public int SpeedIndex => speedIndex;
    public bool IsPaused => speedIndex == 0;
    public void SetSpeed(int i)
    {
        speedIndex = Mathf.Clamp(i, 0, Speeds.Length - 1);
        ApplySpeed();
        Debug.Log("⏩『戦闘速度』" + SpeedNames[speedIndex]);
    }
    private void ApplySpeed()
    {
        // 準備フェーズでは常に等速（止めても意味がないので）
        Time.timeScale = (currentPhase == Phase.Battle) ? Speeds[speedIndex] : 1f;
    }

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        UpdateTurnUI();
    }

    // 🔴 画面下の『侵略開始』ボタンから呼ばれる関数
    public void StartBattlePhase()
    {
        if (currentPhase != Phase.Prepare) return;

        currentPhase = Phase.Battle;
        battleElapsed = 0f; forcedRetreatIssued = false; // ⏱️ ウェーブタイマーをリセット
        ApplySpeed();                                    // ⏩ 選んでいた速度を戦闘に適用
        CommandSystem.Reset();                           // 📯 号令はウェーブごとに撃てる
        RelicManager.BeginWave();                        // 🏺 実績『無失点』の集計を開始
        if (startBattleButton != null) startBattleButton.SetActive(false); // 戦闘中は開始ボタンを隠す
        SoundSystem.Play(SoundSystem.Sfx.Wave);                            // 🔊 角笛
        SoundSystem.PlayBgm(SoundSystem.Bgm.Battle);

        Debug.Log($"<color=red>⚔️『第 {currentTurn} ターン 防衛戦開始』</color> 冒険者ウェーブがダンジョンに突入します！");

        // 🏢 複数フロア：侵略は最上階(B1F)から開始（フロア0を構築＋防衛体スポーン）。入口セルもここで確定。
        if (DungeonFloorManager.Instance != null) DungeonFloorManager.Instance.BeginDescent();

        // スポナーに今週の襲来を開始させる
        DungeonAdventurerSpawner spawner = Object.FindAnyObjectByType<DungeonAdventurerSpawner>();
        if (spawner != null)
        {
            spawner.StartWaveForThisTurn(currentTurn);
        }
    }

    private void Update()
    {
        if (currentPhase != Phase.Battle) return;

        battleElapsed += Time.deltaTime;
        CommandSystem.Tick(Time.deltaTime);   // 📯 号令のクールダウン（倍速なら早く回復する）

        // ⏱️ 時間切れ：まず全員を強制退却させる（歩いて帰り、感情DPを清算）
        if (battleElapsed >= WaveTimeLimit && !forcedRetreatIssued)
        {
            forcedRetreatIssued = true;
            ForceRetreatAllAdventurers();
            Debug.Log("⏰『時間切れ』ウェーブ制限時間に到達 → 全冒険者を強制退却させます");
        }
        // ⏱️ ハード終了：猶予を過ぎてもまだ残っていれば清算して強制終了
        if (battleElapsed >= WaveTimeLimit + graceSeconds)
        {
            HardEndWave();
            return;
        }

        // ⏱️ 戦闘フェーズ中は、定期的に画面内の冒険者の残数をチェックする
        checkTimer += Time.deltaTime;
        if (checkTimer >= checkInterval)
        {
            checkTimer = 0f;
            CheckWaveEndCondition();
        }
    }

    private void ForceRetreatAllAdventurers()
    {
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsInactive.Exclude)) a.ForceRetreat();
    }

    private void HardEndWave()
    {
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsInactive.Exclude)) a.ForceDespawnWithReward();
        EndBattlePhase();
    }

    // ⏱️ DPを消費して戦闘制限時間を永続延長（序盤3分 → 4分,5分…）
    public void ExtendWaveLimit()
    {
        if (DungeonResourceManager.Instance != null && DungeonResourceManager.Instance.TrySpendDP(extendCostDP))
        {
            waveBonusSeconds += extendSecondsPerUnlock;
            Debug.Log($"⏱️『戦闘時間延長』+{extendSecondsPerUnlock}s（現在の制限 {WaveTimeLimit}s / コスト {extendCostDP}DP）");
        }
    }

    private void CheckWaveEndCondition()
    {
        // マップ内のアクティブな冒険者を全検索
        AdventurerAI[] activeAdventurers = Object.FindObjectsByType<AdventurerAI>(FindObjectsInactive.Exclude);
        
        // 💡『A案の採用』スポナーが召喚を終えており、かつ画面内の冒険者が0になったら自動終了
        DungeonAdventurerSpawner spawner = Object.FindAnyObjectByType<DungeonAdventurerSpawner>();
        bool isSpawningFinished = (spawner == null || !spawner.IsSpawning);

        if (activeAdventurers.Length == 0 && isSpawningFinished)
        {
            EndBattlePhase();
        }
    }

    /// <summary>
    /// 防衛戦の終わり＝**ターンの前半の締め**。迷宮側の結果だけを清算し、後半（地上フェーズ）へ渡す。
    /// ⚠ 地上の解決はここでやらない。ここでやると「地上を操作する前に地上のターンが済んでいる」ことになる。
    /// </summary>
    private void EndBattlePhase()
    {
        Time.timeScale = 1f;      // ⏩ 内政に戻ったら等速に（速度の選択自体は覚えておく）

        // 🏢 descent状態を終了し、表示を最上階へ戻す（内政しやすく）
        if (DungeonFloorManager.Instance != null) DungeonFloorManager.Instance.EndDescent();

        // ⬆️ ウェーブを守り切った＝魔王が成長（レベル＋BP）
        if (DemonLord.Instance != null) DemonLord.Instance.OnWaveDefended();

        // 🔬 研究点を獲得（知識ランクがレート源）
        int knowledge = DemonLord.Instance != null ? DemonLord.Instance.GetStatRank((int)DemonLord.Stat.Knowledge) : 0;
        ResearchState.OnTurnEnd(knowledge);

        // 🏺 遺物：無失点の判定など、ウェーブに紐づくものはここで締める
        var relW = RelicManager.Instance;
        if (relW != null) { RelicManager.EndWaveFlawlessCheck(); relW.CheckUnlocks(); }
        // 📊 戦績：波を1つ凌いだ（[[RunStats]]）。⚠ ここが**ウェーブの終わり**の唯一の通り道
        RunStats.NoteWave(DungeonFloorManager.Instance != null ? DungeonFloorManager.Instance.LastDeepestReached + 1 : 1);

        EnterSurfacePhase();
    }

    /// <summary>🌍 後半へ。画面が地上に切り替わり、ここからは地上のことだけを考える。</summary>
    private void EnterSurfacePhase()
    {
        currentPhase = Phase.Surface;
        if (startBattleButton != null) startBattleButton.SetActive(false);
        UpdateTurnUI();
        SoundSystem.Play(SoundSystem.Sfx.Turn);
        Debug.Log($"<color=#8cb8e6>🌍『第 {currentTurn} ターン 後半・地上フェーズ』</color> 盤を動かし、終えたら『ターンを終える』を押してください。");
        NotifySystem.Push($"<b>第{currentTurn}ターン 後半・地上</b>　進軍と建設を済ませて『ターンを終える』", NotifySystem.Kind.Story);
        var ui = GameUIManager.Instance;
        if (ui != null) ui.OnPhaseChanged();
    }

    /// <summary>
    /// 🌍 地上フェーズを終える＝**ターンの締め**。ここで世界が1つ進む。
    /// 地上パネルの『ターンを終える』から呼ばれる。
    /// </summary>
    public void EndSurfacePhase()
    {
        if (currentPhase != Phase.Surface) return;
        currentTurn++;

        // 🗺️ 地上（4X）：①自軍の侵攻 → ②他魔王の行動 → ③人間側の奪還軍。最後に産出を回収する。
        //    ②③が「領域の逆襲」＝広げっぱなしにはできない（守るか砦にするかの判断が要る）。
        KinRoster.ResolveTurn(currentTurn);
        LegionRoster.ResolveTurn(currentTurn);   // ⚔️ 軍団の進軍（U-1）
        RivalLords.ResolveTurn(currentTurn);
        RivalLords.ResolveHumanReclaim(currentTurn);
        EnemyForce.ResolveTurn(currentTurn);   // ⚔️ 敵の軍が盤の上を歩き、隣り合った領域を攻める
        // 🗡️ 会戦は**敵が動いたあと**（動いてきた位置で撃ち合う）。先に撃つと、来ていない相手を叩くことになる。
        LegionRoster.ResolveBattles(currentTurn);   // ⚔️ 戦線の撃ち合い（U-3：兵科の相性と指揮）
        RivalLords.CollectAfterAll();   // 産出は全部の攻防が終わってから（奪われた領域の分は入らない）
        SettlementSystem.TickTurn();    // 🏙️ 版図の引き直し／祝祭／拠点の特化の産出
        SurfaceMap.GrowPopulation();    // 👥 人口の成長（食料が貯まると増える／統治力を超えた分は不満）
        DistrictCatalog.Collect();      // 🏛️ 施設の産出（DP/素材/研究点/感情）
        WonderCatalog.Collect();        // ★ 遺産の恵み（研究点/素材）
        EurekaTracker.Evaluate();       // 💡 天啓の判定（達成した研究が40%引きになる）
        EraSystem.TickTurn();           // ⏳ 時代・偉業・誓約・災厄
        SyncretismSystem.TickTurn();    // 🜏 習合（継いだ血の毎ターン効果）
        PolicySystem.TickTurn();        // 🏛️ 政体と政策（スロットの整合＋毎ターン効果）
        AttributeSystem.TickTurn();     // 🎖️ 属性ツリーの毎ターン効果
        ScoutSystem.TickTurn();         // 🔭 斥候（移動力の回復・視界・取り残されの処理）
        DiplomacySystem.TickTurn();     // 🕊️ 威名・独立勢力・交易路・不可侵
        NarrativeSystem.TickTurn();     // 📖 物語事件・形見の解禁
        ManaSurge.TickTurn();           // 🌊 魔素の奔流／覚醒（6ターンに1回・そのターン限り）
        TrainingSystem.TickTurn();      // 🏋️ 訓練所に送った配下を鍛える
        VictorySystem.TickTurn();       // 🏆 勝利条件（4本のスコア制・5ターン保持）

        var emo = EmotionTreeManager.Instance;
        if (emo != null && emo.ResearchPointBonus > 0) ResearchState.AddRP(emo.ResearchPointBonus);
        // 🏺 遺物：賢者の石（毎ターンRP）。⚠ 無失点の判定は**ウェーブの終わり**に済ませてある。
        var rel = RelicManager.Instance;
        if (rel != null && rel.ResearchRPPerTurn > 0) ResearchState.AddRP(rel.ResearchRPPerTurn);

        // ── ここから次のターンの前半（迷宮）──
        currentPhase = Phase.Prepare;
        if (startBattleButton != null) startBattleButton.SetActive(true);
        RunStats.NoteTurn();
        Achievements.CheckAll();                // 🏅 実績はターンの頭に見る（常時監視しない）
        GuideSystem.OnTurnStart(currentTurn);   // 📖 腹心の報告（情勢・推奨行動・初出システムの説明）
        UpdateTurnUI();
        SoundSystem.Play(SoundSystem.Sfx.Turn);           // 🔊 ターンが変わった合図
        SoundSystem.PlayBgm(SoundSystem.Bgm.Prepare);
        var ui = GameUIManager.Instance;
        if (ui != null) ui.OnPhaseChanged();    // 🌍 画面を迷宮へ戻す
        SaveSystem.AutoSave();                  // 💾 ターンの頭で自動保存（落ちても1ターン以上は戻らない）

        Debug.Log($"<color=green>💤『第 {currentTurn} ターン 前半・迷宮フェーズ』</color> ダンジョンを補強してください。");
    }

    /// <summary>💾 ロード直後。復元したターン数と準備フェーズの見た目を反映する。→ [[SaveSystem]]</summary>
    public void RefreshAfterLoad()
    {
        currentPhase = Phase.Prepare;      // 保存は準備フェーズでしか行わない
        battleElapsed = 0f; forcedRetreatIssued = false;
        SetSpeed(1);
        if (startBattleButton != null) startBattleButton.SetActive(true);
        UpdateTurnUI();
    }

    private void UpdateTurnUI()
    {
        if (turnDisplayText != null)
            turnDisplayText.text = currentPhase == Phase.Battle
                ? $"⚔️ <b>Turn:</b> {currentTurn} <color=#FF3333>(戦闘中!)</color>"
                : currentPhase == Phase.Surface
                    ? $"🌍 <b>Turn:</b> {currentTurn} <color=#8cb8e6>(後半・地上)</color>"
                    : $"⏳ <b>Turn:</b> {currentTurn} <color=#00FF00>(前半・迷宮)</color>";
    }
}