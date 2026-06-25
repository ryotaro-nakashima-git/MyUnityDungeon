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
        if (startBattleButton != null) startBattleButton.SetActive(false); // 戦闘中は開始ボタンを隠す

        Debug.Log($"<color=red>⚔️【第 {currentTurn} ターン 防衛戦開始】</color> 冒険者ウェーブがダンジョンに突入します！");

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

        // ⏱️ 戦闘フェーズ中は、定期的に画面内の冒険者の残数をチェックする
        checkTimer += Time.deltaTime;
        if (checkTimer >= checkInterval)
        {
            checkTimer = 0f;
            CheckWaveEndCondition();
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