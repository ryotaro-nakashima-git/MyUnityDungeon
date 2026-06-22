using UnityEngine;
using UnityEngine.InputSystem;

public class DungeonUpgradeManager : MonoBehaviour
{
    // シングルトン化して、どこからでも開発状況を覗けるようにする
    public static DungeonUpgradeManager Instance { get; private set; }

    [Header("Upgrade Status (開発状況)")]
    [Tooltip("罠部屋がアンロックされているかフラグ")]
    public bool isTrapUnlocked = false; 

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

    private void Update()
    {
        // ⭐【テスト機能】ゲーム中にキーボードの「U」キーを押したら技術開発をテスト実行する
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.uKey.wasPressedThisFrame)
        {
            TryUnlockTrap();
        }
    }

    /// <summary>
    /// 100 DPを消費して「罠部屋」のアンロックを試みます
    /// </summary>
    public void TryUnlockTrap()
    {
        if (isTrapUnlocked)
        {
            Debug.Log("ℹ️ 【技術開発】「罠部屋」の設置権限はすでに解放されています！");
            return;
        }

        int cost = 100; // 開発に必要なコスト：100 DP

        if (DungeonResourceManager.Instance != null)
        {
            // 銀行（ResourceManager）にお金を支払えるかチェックしてもらう
            if (DungeonResourceManager.Instance.TrySpendDP(cost))
            {
                // 支払いが成功したらアンロック！
                isTrapUnlocked = true;
                Debug.Log("<color=magenta>🔓【技術開発成功】</color> 100 DPを消費して「罠部屋」の開発が完了しました！");
            }
        }
    }
}