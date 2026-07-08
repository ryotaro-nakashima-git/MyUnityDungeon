using UnityEngine;
using TMPro; 

public class DungeonResourceManager : MonoBehaviour
{
    public static DungeonResourceManager Instance { get; private set; }

    [Header("Dungeon Resources")]
    [SerializeField] private int dungeonPoints = 1000; // 🛠️テストしやすいように初期DPを1000に調整
    [SerializeField] private int dungeonFame = 0;    
    [SerializeField] private int craftMaterials = 0;  

    [Header("UI Display Settings")]
    [SerializeField] private TextMeshProUGUI resourceDisplayText; 

    public int CraftMaterials => craftMaterials;
    public int DungeonFame => dungeonFame;
    public int DungeonPoints => dungeonPoints;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        UpdateResourceUIDisplay();
    }

    public void AddDP(int amount)
    {
        dungeonPoints += amount;
        UpdateResourceUIDisplay();
    }

    // 🛠️【新機能】解体時にお金を払い戻す専用関数
    public void RefundDP(int originalCost, bool isHalfRefund)
    {
        int refundAmount = isHalfRefund ? Mathf.RoundToInt(originalCost * 0.5f) : originalCost;
        dungeonPoints += refundAmount;
        UpdateResourceUIDisplay();
        
        if (refundAmount > 0)
        {
            Debug.Log($"♻️【解体リサイクル】タイルの解体により {refundAmount} DP が払い戻されました。{(isHalfRefund ? "(戦闘中ペナルティ: 50%返金)" : "(内政中: 100%全額返金)")}");
        }
    }

    public void AddFame(int amount)
    {
        dungeonFame += amount;
        UpdateResourceUIDisplay();
    }

    public void AddMaterial(int amount)
    {
        craftMaterials += amount;
        UpdateResourceUIDisplay();
    }

    public bool TrySpendDP(int amount)
    {
        if (dungeonPoints >= amount)
        {
            dungeonPoints -= amount;
            UpdateResourceUIDisplay(); 
            return true; 
        }
        else
        {
            Debug.LogWarning($"❌【資金不足】 建築または拡張に必要なDPが足りません！ 必要: {amount} / 所持: {dungeonPoints}");
            return false; 
        }
    }

    public bool TrySpendMaterial(int amount)
    {
        if (craftMaterials >= amount)
        {
            craftMaterials -= amount;
            UpdateResourceUIDisplay(); 
            return true; 
        }
        else
        {
            Debug.LogWarning($"❌【素材不足】 ゾンビの錬成に必要なクラフト素材が足りません！ 必要: {amount} / 所持: {craftMaterials}");
            return false; 
        }
    }

    public void UpdateResourceUIDisplay()
    {
        int currentTurn = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 1;
        bool isPrepare = DungeonTurnManager.Instance == null || DungeonTurnManager.Instance.IsPreparePhase;
        string phaseStr = isPrepare ? "<color=#00FF00>準備中</color>" : "<color=#FF3333>戦闘中!</color>";

        if (resourceDisplayText != null)
        {
            resourceDisplayText.text = $"⏳ <b>Turn:</b> {currentTurn} ({phaseStr})   |   💰 <b>DP:</b> {dungeonPoints}   |   🌟 <b>Fame:</b> {dungeonFame}   |   📦 <b>Materials:</b> {craftMaterials}";
        }
    }
}