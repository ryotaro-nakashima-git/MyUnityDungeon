using UnityEngine;
using TMPro;

public class DungeonTurnManager : MonoBehaviour
{
    public static DungeonTurnManager Instance { get; private set; }

    public enum Phase { Prepare, Battle }
    private Phase currentPhase = Phase.Prepare;

    private int currentTurn = 1;
    public int CurrentTurn => currentTurn;
    public bool IsPreparePhase => currentPhase == Phase.Prepare;
    public bool IsBattlePhase => currentPhase == Phase.Battle;

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

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        UpdateTurnUI();
    }

    // 🔴 画面下の「侵略開始」ボタンから呼ばれる関数
    public void StartBattlePhase()
    {
        if (currentPhase != Phase.Prepare) return;

        currentPhase = Phase.Battle;
        battleElapsed = 0f; forcedRetreatIssued = false; // ⏱️ ウェーブタイマーをリセット
        if (startBattleButton != null) startBattleButton.SetActive(false); // 戦闘中は開始ボタンを隠す

        Debug.Log($"<color=red>⚔️【第 {currentTurn} ターン 防衛戦開始】</color> 冒険者ウェーブがダンジョンに突入します！");

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

        // ⏱️ 時間切れ：まず全員を強制退却させる（歩いて帰り、感情DPを清算）
        if (battleElapsed >= WaveTimeLimit && !forcedRetreatIssued)
        {
            forcedRetreatIssued = true;
            ForceRetreatAllAdventurers();
            Debug.Log("⏰【時間切れ】ウェーブ制限時間に到達 → 全冒険者を強制退却させます");
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
            Debug.Log($"⏱️【戦闘時間延長】+{extendSecondsPerUnlock}s（現在の制限 {WaveTimeLimit}s / コスト {extendCostDP}DP）");
        }
    }

    private void CheckWaveEndCondition()
    {
        // マップ内のアクティブな冒険者を全検索
        AdventurerAI[] activeAdventurers = Object.FindObjectsByType<AdventurerAI>(FindObjectsInactive.Exclude);
        
        // 💡【A案の採用】スポナーが召喚を終えており、かつ画面内の冒険者が0になったら自動終了
        DungeonAdventurerSpawner spawner = Object.FindAnyObjectByType<DungeonAdventurerSpawner>();
        bool isSpawningFinished = (spawner == null || !spawner.IsSpawning);

        if (activeAdventurers.Length == 0 && isSpawningFinished)
        {
            EndBattlePhase();
        }
    }

    private void EndBattlePhase()
    {
        currentPhase = Phase.Prepare;
        currentTurn++;

        // 🏢 descent状態を終了し、表示を最上階へ戻す（内政しやすく）
        if (DungeonFloorManager.Instance != null) DungeonFloorManager.Instance.EndDescent();

        // ⬆️ ウェーブを守り切った＝魔王が成長（レベル＋BP）
        if (DemonLord.Instance != null) DemonLord.Instance.OnWaveDefended();

        if (startBattleButton != null) startBattleButton.SetActive(true); // 内政に戻ったら開始ボタンを復活
        UpdateTurnUI();

        Debug.Log($"<color=green>💤【第 {currentTurn} ターン 内政フェーズ開始】</color> 防衛戦が自動終了しました。ダンジョンを補強してください。");
    }

    private void UpdateTurnUI()
    {
        if (turnDisplayText != null)
        {
            turnDisplayText.text = $"⏳ <b>Turn:</b> {currentTurn} <color=#00FF00>(準備中)</color>";
            if (currentPhase == Phase.Battle)
            {
                turnDisplayText.text = $"⚔️ <b>Turn:</b> {currentTurn} <color=#FF3333>(戦闘中!)</color>";
            }
        }
    }
}