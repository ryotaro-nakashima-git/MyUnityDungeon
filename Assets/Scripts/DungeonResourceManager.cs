using UnityEngine;
using TMPro; 

public class DungeonResourceManager : MonoBehaviour
{
    public static DungeonResourceManager Instance { get; private set; }

    [Header("Dungeon Resources")]
    [SerializeField] private int dungeonPoints = 0;  
    [SerializeField] private int dungeonFame = 0;    
    [SerializeField] private int craftMaterials = 0;  

    [Header("UI Display Settings")]
    [SerializeField] private TextMeshProUGUI resourceDisplayText; 

    public int CraftMaterials => craftMaterials;

    // 🔥【フェーズ4・ステップ3追加】現在の知名度を外部から確認できるようにする
    public int DungeonFame => dungeonFame;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }
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

    private void UpdateResourceUIDisplay()
    {
        if (resourceDisplayText != null)
        {
            resourceDisplayText.text = $"💰 <b>DP:</b> {dungeonPoints}   |   🌟 <b>Fame:</b> {dungeonFame}   |   📦 <b>Materials:</b> {craftMaterials}";
        }
    }
}