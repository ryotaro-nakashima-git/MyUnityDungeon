using UnityEngine;

public class RoomData : MonoBehaviour
{
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
    [SerializeField] private float regenTime = 8f; // 💡とりあえず8秒で設定。自由に変えてね！
    
    private bool isReady = true; // 現在機能しているか（宝箱の中身があるか）
    private float regenTimer = 0f;
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

    private void Update()
    {
        // 罠部屋は常に作動可能（クールダウンなし）
        if (roomType == RoomType.Trap)
        {
            isReady = true;
            return;
        }

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

    // AIが目的地として狙えるかどうかの判定
    public bool IsTargetable()
    {
        if (roomType == RoomType.Trap) return false; // 💡罠は自分からは目指さない（罠に自ら特攻するのを防ぐ）
        return isReady; // 宝箱や部屋は、中身がある（Ready）の時だけ目的地になる
    }

    // 部屋の効果を発動できるかチェック
    public bool CanExecuteEffect()
    {
        if (roomType == RoomType.Trap) return true; // 罠はいつでも何度でも作動OK！
        return isReady; // 宝箱などはReadyの時だけ
    }

    // 効果を発動した（踏まれた）時の処理
    public void ExecuteEffect()
    {
        if (roomType == RoomType.Trap) return; // 罠は踏まれても空っぽにならない

        isReady = false;
        regenTimer = 0f;

        // 【演出】中身が空になったことを表すために、見た目を少し暗くする
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor * 0.4f; 
        }
    }

    // 部屋が自動復活した時の処理
    private void ResetRoom()
    {
        isReady = true;
        regenTimer = 0f;

        // 【演出】見た目の明るさを元に戻す
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        Debug.Log($"🔄【ダンジョン環境】宝箱/部屋が再チャージされ、復活しました！冒険者のターゲットに再登録されます。");
    }
}