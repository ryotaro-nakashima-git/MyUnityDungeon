using UnityEngine;

public class RoomData : MonoBehaviour
{
    // 💡元のコードの enum（Normal, TreasureChest, Trap）に準拠
    public enum RoomType { Normal, TreasureChest, Trap }

    [Header("Room Settings")]
    public RoomType roomType = RoomType.Normal;
    [Tooltip("部屋の魅力度。復活するとこの数値が再びAIを惹きつけます")]
    public float attraction = 10f;

    [Header("Emotion & Effect Values")]
    public float joyValue = 0f;
    public float fearValue = 0f;
    public float damageValue = 0f;

    [Header("Cooldown Settings")]
    [Tooltip("一度踏まれてから、宝箱や部屋が復活するまでの時間（秒）")]
    [SerializeField] private float regenTime = 8f; // 💡元の8秒設定を継承！
    
    private bool isReady = true; // 現在機能しているか（宝箱の中身があるか）
    private float regenTimer = 0f;

    // ⚙️【新機能マージ】シーフによる一時無効化タイマー（罠用）
    private float disableTimer = 0f;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    private void Start()
    {
        // 💰 タイプに応じたデフォルト魅力度の自動初期設定
        if (roomType == RoomType.TreasureChest) attraction = 50f;
        else if (roomType == RoomType.Trap) attraction = 15f;
        else attraction = 10f;
    }

    private void Update()
    {
        // ⏱️ 1. シーフによる罠の一時機能停止タイマーの進行
        if (disableTimer > 0f)
        {
            disableTimer -= Time.deltaTime;
            if (disableTimer <= 0f)
            {
                Debug.Log($"⚙️【罠トラップ再起動】一時停止していた罠が再稼働しました！");
            }
        }

        // ⏱️ 2. 元のコードの自動復活タイマーロジック
        if (roomType == RoomType.Trap)
        {
            // 罠部屋は常に作動可能（空っぽによるクールダウンはなし）
            isReady = true;
        }
        else
        {
            // 宝箱部屋・普通の部屋が空っぽの時は、タイマーを回して自動復活させる
            if (!isReady)
            {
                regenTimer += Time.deltaTime;
                if (regenTimer >= regenTime)
                {
                    ResetRoom();
                }
            }
        }

        // 🎨 3.【色管理の自動統合システム】毎フレーム、状態に合わせて色を上書き制御
        UpdateVisualColor();
    }

    private void UpdateVisualColor()
    {
        if (spriteRenderer == null) return;

        if (roomType == RoomType.Trap && disableTimer > 0f)
        {
            // 🛑 状態A：シーフによる罠機能停止中 ➡ 25%の薄さ（半透明）
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.25f);
        }
        else if (!isReady)
        {
            // ⏳ 状態B：普通の部屋・宝箱が空っぽ ➡ 元のコードの演出（originalColor * 0.4f で暗くする）
            spriteRenderer.color = originalColor * 0.4f;
        }
        else
        {
            // ✨ 状態C：通常時（準備完了） ➡ 元のハッキリした色に戻す
            spriteRenderer.color = originalColor;
        }
    }

    // AIが目的地として狙えるかどうかの判定
    public bool IsTargetable()
    {
        if (roomType == RoomType.Trap) return false; // 💡罠は自分からは目指さない
        
        // 宝箱や普通の部屋は、中身があり（Ready）、かつ魅力度が生きている時だけ目的地になる
        return isReady && attraction > 0f; 
    }

    // 部屋の効果を発動できるかチェック
    public bool CanExecuteEffect()
    {
        if (roomType == RoomType.Trap)
        {
            // 罠の場合、シーフに無効化されていなければ（タイマーが0以下なら）作動OK！
            return disableTimer <= 0f;
        }
        return isReady; // 宝箱などはReadyの時だけ
    }

    // 効果を発動した（踏まれた）時の処理
    public void ExecuteEffect()
    {
        if (roomType == RoomType.Trap) return; // 罠は踏まれても空っぽにならない

        isReady = false;
        regenTimer = 0f;

        // 💰【経済連動】宝箱が空の間は一時的に魅力度を0にして、リチャージまでAIに狙わせない
        if (roomType == RoomType.TreasureChest)
        {
            StaticAttractionCooldown();
        }
    }

    // 宝箱がリチャージされるまでAIのターゲットから完全に隠す非同期制御
    private async void StaticAttractionCooldown()
    {
        float oldAttraction = attraction;
        attraction = 0f;
        while (!isReady) { await System.Threading.Tasks.Task.Yield(); }
        attraction = oldAttraction;
    }

    // 部屋が自動復活した時の処理
    private void ResetRoom()
    {
        isReady = true;
        regenTimer = 0f;
        Debug.Log($"🔄【ダンジョン環境】宝箱/部屋が再チャージされ、復活しました！冒険者のターゲットに再登録されます。");
    }

    // 🎭 シーフ（盗賊）が罠解除に成功した時に、冒険者AI側から呼び出される窓口関数
    public void DisableTrapTemporarily(float duration)
    {
        if (roomType == RoomType.Trap)
        {
            disableTimer = duration;
        }
    }
}