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

    // ===== 魔王の成長（ステータス/レベル/種族進化）=====
    public enum Stat { Body, Magic, Knowledge, Creation, Refine } // 肉体/魔力/知識/創造/錬成
    public enum Race { Human, Oni, Demon, Elf, Dwarf, Slime, Vampire } // 人/鬼/魔族/エルフ/ドワーフ/スライム/吸血

    [Header("Growth")]
    [SerializeField] private int bpPerWave = 4;
    [SerializeField] private float hpPerBodyRank = 130f;
    [SerializeField] private float baseAttackPower = 20f;
    [SerializeField] private float atkPerMagicRank = 6f;

    private int[] statRanks = new int[5]; // 0=E,1=D,2=C,3=B,4=A,5=S
    private int level = 1;
    private int bp = 10;
    private Race race = Race.Human;
    private float effectiveAttack = 20f;
    private static readonly int[] rankUpCost = { 2, 5, 10, 18, 30 }; // E→D, D→C, C→B, B→A, A→S

    public static readonly string[] StatNames = { "肉体", "魔力", "知識", "創造", "錬成" };
    public int Level => level;
    public int BP => bp;
    public Race CurrentRace => race;
    public int GetStatRank(int i) => statRanks[Mathf.Clamp(i, 0, 4)];
    public string StatRankLabel(int i) => "EDCBAS"[Mathf.Clamp(GetStatRank(i), 0, 5)].ToString();
    public string RaceName => RaceNameOf(race);
    public bool CanEvolve => race == Race.Human && level >= 3;
    public float DefenderCostMult
    {
        get { switch (race) { case Race.Vampire: return 0.8f; case Race.Dwarf: return 0.7f; case Race.Elf: return 0.9f; default: return 1f; } }
    }

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

        alive = true;
        RecomputeCombatStats();  // ステータス/種族を反映して最大HP・攻撃力を算出
        currentHP = maxHP;       // 満タンで再配置
        if (sr != null) sr.color = new Color(0.55f, 0.20f, 0.78f);
        UpdateHPText();
    }

    // ステータス・種族からmaxHP/攻撃力を再計算
    private void RecomputeCombatStats()
    {
        int turn = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 1;
        maxHP = (baseMaxHP + hpPerTurn * (turn - 1) + hpPerBodyRank * statRanks[(int)Stat.Body]) * RaceHpMult();
        effectiveAttack = (baseAttackPower + atkPerMagicRank * statRanks[(int)Stat.Magic]) * RaceAtkMult();
        if (currentHP > maxHP) currentHP = maxHP;
    }
    private float RaceHpMult()
    {
        switch (race) { case Race.Oni: return 1.3f; case Race.Slime: return 1.6f; case Race.Dwarf: return 1.15f; case Race.Demon: return 1.1f; case Race.Elf: return 1.2f; default: return 1f; }
    }
    private float RaceAtkMult()
    {
        switch (race) { case Race.Oni: return 1.3f; case Race.Vampire: return 1.4f; case Race.Demon: return 1.25f; case Race.Elf: return 1.1f; default: return 1f; }
    }

    // ⬆️ 防衛戦を1ウェーブ耐えるごとにレベルアップ＆BP獲得（DungeonTurnManager.EndBattlePhaseから）
    public void OnWaveDefended()
    {
        level++;
        bp += bpPerWave;
        RecomputeCombatStats(); currentHP = maxHP;
        Debug.Log($"⬆️【魔王成長】Lv{level} / BP +{bpPerWave}（所持 {bp}）");
    }

    // 🔧 BPを消費してステータスを1ランク上げる（UIから）
    public bool TrySpendBPOnStat(int statIndex)
    {
        if (statIndex < 0 || statIndex > 4) return false;
        int r = statRanks[statIndex];
        if (r >= 5) { Debug.Log("ℹ️ 既に最大ランク(S)です。"); return false; }
        int cost = rankUpCost[r];
        if (bp < cost) { Debug.LogWarning($"❌ BP不足（必要 {cost} / 所持 {bp}）"); return false; }
        bp -= cost; statRanks[statIndex]++;
        RecomputeCombatStats(); currentHP = maxHP;
        UpdateHPText();
        return true;
    }

    // 🧬 種族進化
    public bool IsRaceAvailable(Race r)
    {
        if (!CanEvolve) return false;
        switch (r)
        {
            case Race.Oni: return statRanks[(int)Stat.Body] >= 2;      // 肉体C以上
            case Race.Demon: return statRanks[(int)Stat.Magic] >= 2;   // 魔力C以上
            case Race.Elf: return statRanks[(int)Stat.Knowledge] >= 2; // 知識C以上
            case Race.Dwarf: return statRanks[(int)Stat.Refine] >= 2;  // 錬成C以上
            case Race.Slime: return level >= 3;
            case Race.Vampire: return level >= 5;
            default: return false;
        }
    }
    public bool EvolveTo(Race r)
    {
        if (!IsRaceAvailable(r)) return false;
        race = r;
        RecomputeCombatStats(); currentHP = maxHP;
        UpdateHPText();
        Debug.Log($"🧬【進化】魔王が {RaceNameOf(r)} へ進化しました！");
        return true;
    }
    public static string RaceNameOf(Race r)
    {
        switch (r) { case Race.Oni: return "鬼種"; case Race.Demon: return "魔族種"; case Race.Elf: return "エルフ種"; case Race.Dwarf: return "ドワーフ種"; case Race.Slime: return "スライム種"; case Race.Vampire: return "吸血種"; default: return "人種"; }
    }

    private void Update()
    {
        if (!alive) return;
        var turn = DungeonTurnManager.Instance;
        if (turn == null || !turn.IsBattlePhase) return;

        // 🛡 門番ボス生存中は無敵表示（色＋ラベル）
        bool shielded = ZombieAI.GetLivingGuardian() != null;
        if (sr != null) sr.color = shielded ? new Color(0.40f, 0.50f, 0.95f) : new Color(0.55f, 0.20f, 0.78f);
        if (hpText != null)
        {
            if (shielded) { hpText.text = "GUARDED"; hpText.color = new Color(0.5f, 0.85f, 1f); }
            else UpdateHPText();
        }

        // 隣接した冒険者へ反撃（無敵中でも反撃はする）
        attackTimer += Time.deltaTime;
        if (attackTimer >= attackInterval)
        {
            attackTimer = 0f;
            foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None))
            {
                if (a == null) continue;
                if (Vector3.Distance(transform.position, a.transform.position) <= attackRange)
                    a.TakeDamage(effectiveAttack);
            }
        }
    }

    public void TakeDamage(float dmg)
    {
        if (!alive) return;
        if (ZombieAI.GetLivingGuardian() != null) return; // 🛡 門番生存中は無敵（保険）
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
        if (hpText != null) { hpText.text = "DEFEATED"; hpText.color = Color.gray; }
        Debug.Log("💀【ゲームオーバー】魔王が討伐されました！");

        var ui = Object.FindFirstObjectByType<GameUIManager>();
        if (ui != null) ui.ShowGameOver();
        Time.timeScale = 0f; // ゲーム停止
    }

    private void UpdateHPText()
    {
        if (hpText != null && alive) { hpText.text = "HP " + Mathf.CeilToInt(currentHP); hpText.color = Color.red; }
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
