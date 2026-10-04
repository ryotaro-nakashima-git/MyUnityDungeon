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

    // 🏺 遺物の実績用：撃破で得たDPの累計（プレイセッション内）
    private static int totalKillDP = 0;
    public static int TotalKillDP => totalKillDP;
    public static void AddKillDPRecord(int amount) { totalKillDP += Mathf.Max(0, amount); }
    public static void ResetKillDPRecord() { totalKillDP = 0; }

    /// <summary>
    /// 🔄 **新しい周のために資源を畳む**（`StartNewGame` から呼ぶ）。
    /// ⚠ DP はこのあと `SetDP(GameSetup.StartDP)` が入れるので、ここでは 0 にするだけでよい。
    /// ⚠ これが無いと、前の周の**素材と名声がそのまま持ち越される**（実測：素材66・名声つき で2周目が始まった）。
    /// </summary>
    public void ResetRun()
    {
        ResourceLedger.Note("DP", -dungeonPoints, "Run", "Reset"); ResourceLedger.Note("Material", -craftMaterials, "Run", "Reset");
        ResourceLedger.Note("Fame", -dungeonFame, "Run", "Reset");
        dungeonPoints = 0; craftMaterials = 0; dungeonFame = 0;
        ResetKillDPRecord();
    }

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        UpdateResourceUIDisplay();
    }

    // 🎬 開始時の初期DPを直接セットする（タイトル画面の世界設定から）
    public void SetDP(int amount, [System.Runtime.CompilerServices.CallerMemberName] string cm = "", [System.Runtime.CompilerServices.CallerFilePath] string cf = "")
    {
        ResourceLedger.Note("DP", Mathf.Max(0, amount) - dungeonPoints, cf, cm);   // 💧 台帳：置き換えも差分として記録
        dungeonPoints = Mathf.Max(0, amount);
        UpdateResourceUIDisplay();
    }

    public void AddDP(int amount, [System.Runtime.CompilerServices.CallerMemberName] string cm = "", [System.Runtime.CompilerServices.CallerFilePath] string cf = "")
    {
        dungeonPoints += amount;
        ResourceLedger.Note("DP", amount, cf, cm);   // 💧 台帳（呼び出し元を自動で区分）
        RunStats.NoteDp(amount);      // 📊 戦績：稼いだDPの累計（返金は数えない）
        WaveReport.NoteDp(amount);    // 📜 波の決算：戦闘中の入りだけ数える（→ [[WaveReport]]）
        UpdateResourceUIDisplay();
    }

    // 🛠️『新機能』解体時にお金を払い戻す専用関数
    public void RefundDP(int originalCost, bool isHalfRefund, [System.Runtime.CompilerServices.CallerMemberName] string cm = "", [System.Runtime.CompilerServices.CallerFilePath] string cf = "")
    {
        int refundAmount = isHalfRefund ? Mathf.RoundToInt(originalCost * 0.5f) : originalCost;
        dungeonPoints += refundAmount;
        ResourceLedger.Note("DP", refundAmount, cf, cm);
        UpdateResourceUIDisplay();
        
        if (refundAmount > 0)
        {
            Debug.Log($"♻️『解体リサイクル』タイルの解体により {refundAmount} DP が払い戻されました。{(isHalfRefund ? "(戦闘中ペナルティ: 50%返金)" : "(内政中: 100%全額返金)")}");
        }
    }

    public void AddFame(int amount, [System.Runtime.CompilerServices.CallerMemberName] string cm = "", [System.Runtime.CompilerServices.CallerFilePath] string cf = "")
    {
        dungeonFame += amount;
        ResourceLedger.Note("Fame", amount, cf, cm);
        WaveReport.NoteFame(amount);
        UpdateResourceUIDisplay();
    }

    public void AddMaterial(int amount, [System.Runtime.CompilerServices.CallerMemberName] string cm = "", [System.Runtime.CompilerServices.CallerFilePath] string cf = "")
    {
        // 🔬 K-3：研究『分解』『錬金術』を本当に効かせた（説明にあるのに誰も読んでいなかった）
        float rMat = 1f
            + (ResearchState.IsResearched("r_recycle") ? 0.15f : 0f)
            + (ResearchState.IsResearched("r_alchemy") ? 0.25f : 0f);
        if (amount > 0) amount = Mathf.RoundToInt(amount * PolicySystem.MaterialMult * AttributeSystem.MaterialMult * NarrativeSystem.MaterialMult * rMat);   // 🏛️ 政策『遺物市場』／🎖️ 属性『交易網』／🕯️ 形見『坑夫の鶴嘴』
        craftMaterials += amount;
        ResourceLedger.Note("Material", amount, cf, cm);   // 💧 倍率を掛けた後の実際の入り
        WaveReport.NoteMaterial(amount);
        UpdateResourceUIDisplay();
    }

    public bool TrySpendDP(int amount, [System.Runtime.CompilerServices.CallerMemberName] string cm = "", [System.Runtime.CompilerServices.CallerFilePath] string cf = "")
    {
        if (dungeonPoints >= amount)
        {
            dungeonPoints -= amount;
            ResourceLedger.Note("DP", -amount, cf, cm);
            // 📜 波の決算：**入りと出を別々に**数える。号令は戦闘中にDPを払うので、
            //   差だけ見せると「押した手が損」に見える（→ [[WaveReport]]）。
            WaveReport.NoteDpSpent(amount);
            UpdateResourceUIDisplay(); 
            return true; 
        }
        else
        {
            Debug.LogWarning($"❌『資金不足』 建築または拡張に必要なDPが足りません！ 必要: {amount} / 所持: {dungeonPoints}");
            return false; 
        }
    }

    public bool TrySpendMaterial(int amount, [System.Runtime.CompilerServices.CallerMemberName] string cm = "", [System.Runtime.CompilerServices.CallerFilePath] string cf = "")
    {
        if (craftMaterials >= amount)
        {
            craftMaterials -= amount;
            ResourceLedger.Note("Material", -amount, cf, cm);
            UpdateResourceUIDisplay(); 
            return true; 
        }
        else
        {
            Debug.LogWarning($"❌『素材不足』 ゾンビの錬成に必要なクラフト素材が足りません！ 必要: {amount} / 所持: {craftMaterials}");
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