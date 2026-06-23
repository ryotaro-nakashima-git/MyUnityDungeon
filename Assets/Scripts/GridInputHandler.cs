using UnityEngine;
using UnityEngine.InputSystem;

public class GridInputHandler : MonoBehaviour
{
    [SerializeField] private DungeonGridSystem gridSystem;
    
    [Header("Preview Settings")]
    [SerializeField] private SpriteRenderer previewRenderer;
    [SerializeField] private GameObject corridorPrefab;
    [SerializeField] private GameObject roomPrefab;
    [SerializeField] private GameObject treasurePrefab;
    [SerializeField] private GameObject trapPrefab;
    [SerializeField] private GameObject adventurerPrefab;
    [SerializeField] private GameObject zombiePrefab; 

    private enum ToolMode { Corridor, Room, TreasureChest, Trap, SpawnAdventurer, SpawnZombie }
    private ToolMode currentMode = ToolMode.Corridor;

    private void Awake()
    {
        if (corridorPrefab == null || roomPrefab == null || treasurePrefab == null || trapPrefab == null || adventurerPrefab == null)
        {
            Debug.LogWarning($"ℹ️ [セーフティ] プレハブ未設定の GridInputHandler を [{gameObject.name}] で検出しました。活動を停止します。");
            this.enabled = false; 
            return;
        }

        if (previewRenderer != null)
        {
            previewRenderer.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (gridSystem == null) return;

        Mouse mouse = Mouse.current;
        if (mouse == null || Camera.main == null) return;

        Vector2 screenPosition = mouse.position.ReadValue();
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0));
        mouseWorldPos.z = 0;

        Vector2Int gridPos = gridSystem.WorldToGrid(mouseWorldPos);

        // 🛑【拡張連動】50x50ではなく、現在の「有効領土サイズ（最初は10）」のエリア外ならプレビューを非表示にする
        if (gridPos.x < 0 || gridPos.x >= gridSystem.CurrentPlayableSize || gridPos.y < 0 || gridPos.y >= gridSystem.CurrentPlayableSize)
        {
            if (previewRenderer != null) previewRenderer.gameObject.SetActive(false);
        }
        else
        {
            // エリア内ならプレビューを表示して追従
            if (previewRenderer != null)
            {
                previewRenderer.gameObject.SetActive(true);
                previewRenderer.transform.position = new Vector3(gridPos.x, gridPos.y, 0);
                UpdatePreviewVisual(gridPos);
            }
        }

        // 左クリック時の処理
        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (currentMode == ToolMode.SpawnAdventurer)
            {
                DungeonGridSystem.TileType footTile = gridSystem.GetTileType(gridPos.x, gridPos.y);
                if (footTile != DungeonGridSystem.TileType.None) SpawnAdventurerAt(gridPos);
            }
            else if (currentMode == ToolMode.SpawnZombie)
            {
                DungeonGridSystem.TileType footTile = gridSystem.GetTileType(gridPos.x, gridPos.y);
                if (footTile != DungeonGridSystem.TileType.None)
                {
                    if (DungeonResourceManager.Instance != null && DungeonResourceManager.Instance.TrySpendMaterial(1))
                    {
                        SpawnZombieAt(gridPos);
                    }
                }
                else
                {
                    Debug.LogWarning("❌ タイルのない空中にはゾンビを配置できません！");
                }
            }
            else
            {
                HandleTilePlacement(gridPos);
            }
        }
        else if (mouse.rightButton.isPressed)
        {
            gridSystem.PlaceTile(gridPos.x, gridPos.y, DungeonGridSystem.TileType.None);
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // 🔥【テスト用拡張ショートカット】ゲーム中に「G」キーを押すと、領土拡張を試みる
        if (keyboard.gKey.wasPressedThisFrame)
        {
            gridSystem.TryExpandDungeonArea();
        }

        if (keyboard.digit1Key.wasPressedThisFrame) { currentMode = ToolMode.Corridor; Debug.Log("🔨 1:【通路】"); }
        if (keyboard.digit2Key.wasPressedThisFrame) { currentMode = ToolMode.Room; Debug.Log("🔨 2:【普通の部屋】"); }
        if (keyboard.digit3Key.wasPressedThisFrame) { currentMode = ToolMode.TreasureChest; Debug.Log("🔨 3:【宝箱部屋】"); }
        if (keyboard.digit4Key.wasPressedThisFrame) { currentMode = ToolMode.Trap; Debug.Log("🔨 4:【罠部屋】"); }
        if (keyboard.digit5Key.wasPressedThisFrame) { currentMode = ToolMode.SpawnAdventurer; Debug.Log("🏃 5:【冒険者スポーン】"); }
        if (keyboard.digit6Key.wasPressedThisFrame) { currentMode = ToolMode.SpawnZombie; Debug.Log("🧟 6:【ゾンビ錬成モード】"); }
    }

    private void HandleTilePlacement(Vector2Int gridPos)
    {
        if (currentMode == ToolMode.Corridor) gridSystem.PlaceTile(gridPos.x, gridPos.y, DungeonGridSystem.TileType.Corridor);
        else if (currentMode == ToolMode.Room) gridSystem.PlaceTile(gridPos.x, gridPos.y, DungeonGridSystem.TileType.Room);
        else if (currentMode == ToolMode.TreasureChest) gridSystem.PlaceTile(gridPos.x, gridPos.y, DungeonGridSystem.TileType.TreasureChest);
        else if (currentMode == ToolMode.Trap)
        {
            bool isUnlocked = (DungeonUpgradeManager.Instance != null && DungeonUpgradeManager.Instance.isTrapUnlocked);
            if (!isUnlocked)
            {
                Debug.LogWarning("🔒「罠部屋」の技術開発が完了していないため、配置できません！(Uキーで開発してください)");
                return; 
            }
            gridSystem.PlaceTile(gridPos.x, gridPos.y, DungeonGridSystem.TileType.Trap);
        }
    }

    private void SpawnAdventurerAt(Vector2Int gridPos)
    {
        if (adventurerPrefab == null) return;
        Vector3 spawnPos = gridSystem.GridToWorld(gridPos.x, gridPos.y);
        Instantiate(adventurerPrefab, spawnPos, Quaternion.identity);
    }

    private void SpawnZombieAt(Vector2Int gridPos)
    {
        if (zombiePrefab == null) return;
        Vector3 spawnPos = gridSystem.GridToWorld(gridPos.x, gridPos.y);
        Instantiate(zombiePrefab, spawnPos, Quaternion.identity);
        Debug.Log($"🧟 座標 ({gridPos.x}, {gridPos.y}) にゾンビを1体錬成しました！");
    }

    private void UpdatePreviewVisual(Vector2Int gridPos)
    {
        GameObject targetPrefab = null;
        Color previewColor = Color.white;

        switch (currentMode)
        {
            case ToolMode.Corridor:
                targetPrefab = corridorPrefab;
                previewColor = new Color(0.6f, 0.6f, 0.6f, 0.6f); 
                break;
            case ToolMode.Room:
                targetPrefab = roomPrefab;
                previewColor = new Color(1f, 0.6f, 0f, 0.6f);   
                break;
            case ToolMode.TreasureChest:
                targetPrefab = treasurePrefab;
                previewColor = new Color(0.0f, 0.8f, 0.2f, 0.6f); 
                break;
            case ToolMode.Trap:
                targetPrefab = trapPrefab;
                bool isTrapUnlocked = (DungeonUpgradeManager.Instance != null && DungeonUpgradeManager.Instance.isTrapUnlocked);
                if (!isTrapUnlocked)
                    previewColor = new Color(1f, 0f, 0f, 0.6f); 
                else
                    previewColor = new Color(0.6f, 0.0f, 0.8f, 0.6f); 
                break;
            case ToolMode.SpawnAdventurer:
                targetPrefab = adventurerPrefab;
                previewColor = new Color(1f, 0.92f, 0.016f, 0.6f); 
                
                if (gridSystem.GetTileType(gridPos.x, gridPos.y) == DungeonGridSystem.TileType.None)
                    previewColor = new Color(1f, 0f, 0f, 0.6f); 
                break;
            case ToolMode.SpawnZombie:
                targetPrefab = zombiePrefab;
                previewColor = new Color(0.0f, 0.6f, 0.6f, 0.6f); 

                DungeonGridSystem.TileType zombieFoot = gridSystem.GetTileType(gridPos.x, gridPos.y);
                bool isMaterialShortage = (DungeonResourceManager.Instance != null && DungeonResourceManager.Instance.CraftMaterials < 1);
                
                if (zombieFoot == DungeonGridSystem.TileType.None || isMaterialShortage)
                {
                    previewColor = new Color(1f, 0f, 0f, 0.6f); 
                }
                break;
        }

        if (targetPrefab == null) return;

        if (previewRenderer != null)
        {
            SpriteRenderer prefabRenderer = targetPrefab.GetComponent<SpriteRenderer>();
            if (prefabRenderer != null && prefabRenderer.sprite != null)
            {
                previewRenderer.sprite = prefabRenderer.sprite;
            }

            if (previewRenderer.sprite == null)
            {
                previewRenderer.sprite = GetFallbackSprite();
            }

            previewRenderer.color = previewColor;
            previewRenderer.sortingOrder = 100; 
        }
    }

    private static Sprite _fallbackSprite;
    private Sprite GetFallbackSprite()
    {
        if (_fallbackSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        }
        return _fallbackSprite;
    }
}