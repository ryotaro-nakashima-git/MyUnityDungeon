using UnityEngine;

/// <summary>
/// 魔王（ダンジョンコアの役割）。CDO2の「守るべき魔王＝倒されたらゲームオーバー」と
/// 小説の「真核＝最深部の核」をハイブリッド。1ダンジョンに1体、最深部(DemonLordCell)に配置。
/// </summary>
public class DemonLord : MonoBehaviour
{
    public static DemonLord Instance { get; private set; }

    [Header("Demon Lord Status")]
    [SerializeField] private float baseMaxHP = 600f;
    [SerializeField] private float hpPerTurn = 120f;   // ターン毎に増える最大HP
    [SerializeField] private float attackPower = 20f;  // 隣接冒険者への反撃
    [SerializeField] private float attackInterval = 1.0f;
    [SerializeField] private float attackRange = 1.6f;

    private float maxHP, currentHP;
    private bool alive = true;
    private float attackTimer = 0f;
    private DungeonGridSystem grid;
    private SpriteRenderer sr;
    private TextMesh hpText;

    public bool IsAlive => alive;
    public float HPRatio => maxHP > 0 ? currentHP / maxHP : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildVisual();
    }

    private void Start()
    {
        grid = Object.FindFirstObjectByType<DungeonGridSystem>();
    }

    private void BuildVisual()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = MakeSquare();
        sr.color = new Color(0.55f, 0.20f, 0.78f); // 紫
        sr.sortingOrder = 60;
        transform.localScale = Vector3.one * 0.82f;

        var label = new GameObject("Label");
        label.transform.SetParent(transform, false);
        label.transform.localPosition = new Vector3(0, 0.05f, -0.1f);
        label.transform.localScale = Vector3.one * 0.13f;
        var tm = label.AddComponent<TextMesh>();
        tm.text = "DL"; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.fontSize = 48; tm.characterSize = 0.5f; tm.color = new Color(1f, 0.9f, 0.4f); tm.fontStyle = FontStyle.Bold;
        var mr = tm.GetComponent<MeshRenderer>(); if (mr != null) mr.sortingOrder = 61;

        var hp = new GameObject("HP");
        hp.transform.SetParent(transform, false);
        hp.transform.localPosition = new Vector3(0, -0.5f, -0.1f);
        hp.transform.localScale = Vector3.one * 0.1f;
        hpText = hp.AddComponent<TextMesh>();
        hpText.anchor = TextAnchor.MiddleCenter; hpText.alignment = TextAlignment.Center;
        hpText.fontSize = 40; hpText.characterSize = 0.5f; hpText.color = Color.red;
        var mr2 = hpText.GetComponent<MeshRenderer>(); if (mr2 != null) mr2.sortingOrder = 61;
    }

    /// <summary>迷宮生成時に最深部へ配置し、HPをリセットする（DungeonGridSystemから呼ばれる）。</summary>
    public void PlaceAt(Vector2Int cell)
    {
        if (grid == null) grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (grid != null) transform.position = grid.GridToWorld(cell.x, cell.y) + new Vector3(0, 0, -0.6f);

        int turn = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 1;
        maxHP = baseMaxHP + hpPerTurn * (turn - 1);
        currentHP = maxHP;
        alive = true;
        if (sr != null) sr.color = new Color(0.55f, 0.20f, 0.78f);
        UpdateHPText();
    }

    private void Update()
    {
        if (!alive) return;
        var turn = DungeonTurnManager.Instance;
        if (turn == null || !turn.IsBattlePhase) return;

        // 隣接した冒険者へ反撃
        attackTimer += Time.deltaTime;
        if (attackTimer >= attackInterval)
        {
            attackTimer = 0f;
            foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None))
            {
                if (a == null) continue;
                if (Vector3.Distance(transform.position, a.transform.position) <= attackRange)
                    a.TakeDamage(attackPower);
            }
        }
    }

    public void TakeDamage(float dmg)
    {
        if (!alive) return;
        currentHP -= dmg;
        UpdateHPText();
        if (currentHP <= 0f)
        {
            currentHP = 0f;
            alive = false;
            Die();
        }
    }

    private void Die()
    {
        if (sr != null) sr.color = Color.gray;
        if (hpText != null) { hpText.text = "討伐された"; hpText.color = Color.gray; }
        Debug.Log("💀【ゲームオーバー】魔王が討伐されました！");

        var ui = Object.FindFirstObjectByType<GameUIManager>();
        if (ui != null) ui.ShowGameOver();
        Time.timeScale = 0f; // ゲーム停止
    }

    private void UpdateHPText()
    {
        if (hpText != null && alive) hpText.text = $"魔王HP {Mathf.CeilToInt(currentHP)}";
    }

    private static Sprite _square;
    private Sprite MakeSquare()
    {
        if (_square == null)
        {
            var tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.white); tex.Apply();
            _square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        }
        return _square;
    }
}
