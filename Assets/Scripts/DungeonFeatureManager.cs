using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 生成済み迷宮の上に「主要要素（トーテム/スポナー/ボス/特殊敵）」を手動配置するマネージャ。
/// - 歩けるマスに色マーカーで配置（歩行判定は変えない＝AIはそのまま通る）
/// - トーテム：隣接部屋の魅力を強化 / スポナー：戦闘中に防衛ゾンビを定期湧き
///   ボス：そのマスをBossCellにして戦闘開始時に強化防衛体 / 特殊敵：戦闘開始時に精鋭防衛体
/// </summary>
public class DungeonFeatureManager : MonoBehaviour
{
    public enum FeatureType { Totem, Spawner, Boss, SpecialEnemy }

    [Header("Costs")]
    [SerializeField] private int totemCostDP = 150;
    [SerializeField] private int spawnerCostDP = 250;
    [SerializeField] private int bossCostDP = 400;
    [SerializeField] private int specialMaterialCost = 3;

    [Header("Effects")]
    [Tooltip("トーテムが隣接部屋の魅力に加える値")]
    [SerializeField] private float totemAttractionBonus = 20f;
    [Tooltip("スポナーが防衛ゾンビを湧かせる間隔(秒)")]
    [SerializeField] private float spawnerInterval = 6f;
    [Tooltip("スポナー1基が1ウェーブで湧かせる最大数")]
    [SerializeField] private int spawnerMaxPerWave = 5;

    [Header("Defender Empower")]
    [SerializeField] private float bossHpMult = 3.0f, bossAtkMult = 2.0f;
    [SerializeField] private float specialHpMult = 1.8f, specialAtkMult = 1.5f;
    [Tooltip("防衛体が配置セルから徘徊できる半径（冒険者を追ってスポーン地点へ行かないための制限）")]
    [SerializeField] private int defenderLeashRadius = 3;

    [Header("Totem Combat Buff (3層バフ・範囲層)")]
    [Tooltip("トーテムが防衛体を強化する半径（マンハッタン距離）")]
    [SerializeField] private int totemBuffRadius = 4;
    [Tooltip("範囲内トーテム1基ごとの防衛体強化率")]
    [SerializeField] private float totemDefenderBuffPer = 0.15f;
    [Tooltip("トーテム強化の最大重ね掛け数")]
    [SerializeField] private int totemBuffMaxStack = 2;

    // 🐺 眷属の種族選択（配置バーで切替）。配置した要素にこの種族が記録され、召喚時に相性が乗る
    private ZombieAI.Species selectedSpecies = ZombieAI.Species.Undead;
    public ZombieAI.Species SelectedSpecies => selectedSpecies;
    public void SetSelectedSpecies(int i) { selectedSpecies = (ZombieAI.Species)Mathf.Clamp(i, 0, 2); Debug.Log($"🐺【眷属種族】{SpeciesName(selectedSpecies)} を選択"); }

    private DungeonGridSystem grid;
    private readonly System.Collections.Generic.List<GameObject> spawnedDefenders = new System.Collections.Generic.List<GameObject>();
    private GameObject zombiePrefab;
    private bool wasBattle = false;

    private class Feature
    {
        public FeatureType type;
        public Vector2Int cell;
        public GameObject marker;
        public float spawnTimer;
        public int spawnedThisWave;
        public List<Vector2Int> buffedNeighbors;
        public ZombieAI.Species species; // 🐺 この要素が召喚する眷属の種族
    }
    private readonly Dictionary<Vector2Int, Feature> features = new Dictionary<Vector2Int, Feature>();

    private static readonly Color TEAL = new Color(0.34f, 0.76f, 0.67f);
    private static readonly Color VIOLET = new Color(0.71f, 0.55f, 0.90f);
    private static readonly Color CRIMSON = new Color(0.87f, 0.35f, 0.35f);
    private static readonly Color GOLD = new Color(0.89f, 0.66f, 0.29f);

    private void Start()
    {
        grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        var input = Object.FindFirstObjectByType<GridInputHandler>();
        if (input != null) zombiePrefab = input.ZombiePrefab;
    }

    private void Update()
    {
        var turn = DungeonTurnManager.Instance;
        bool nowBattle = turn != null && turn.IsBattlePhase;

        if (nowBattle && !wasBattle) OnBattleStart();
        if (!nowBattle && wasBattle) OnBattleEnd();
        if (nowBattle) TickSpawners();
        wasBattle = nowBattle;
    }

    // ============ 配置 / 撤去 ============
    public bool TryPlaceFeature(Vector2Int cell, FeatureType type)
    {
        if (grid == null) grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (grid == null) return false;

        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase)
        {
            Debug.LogWarning("⚠️ 要素の配置は準備フェーズのみ可能です。");
            return false;
        }
        if (grid.GetTileType(cell.x, cell.y) == DungeonGridSystem.TileType.None)
        {
            Debug.LogWarning("⚠️ 壁には配置できません（歩けるマスに配置してください）。");
            return false;
        }
        if (features.ContainsKey(cell))
        {
            Debug.LogWarning("⚠️ そのマスには既に要素があります。");
            return false;
        }
        if (type == FeatureType.Boss && HasBoss())
        {
            Debug.LogWarning("⚠️ ボスエリアは1つまでです（将来は1階層につき1つ）。");
            return false;
        }

        // コスト支払い
        var res = DungeonResourceManager.Instance;
        if (type == FeatureType.SpecialEnemy)
        {
            if (res != null && !res.TrySpendMaterial(specialMaterialCost)) return false;
        }
        else
        {
            if (res != null && !res.TrySpendDP(CostOf(type))) return false;
        }

        AddFeature(cell, type, selectedSpecies);
        Debug.Log($"🧩【配置】{TypeName(type)} を {cell} に配置しました。");
        return true;
    }

    // 実際の配置処理（マーカー生成/トーテム効果/ボスセル更新/辞書登録）。コスト・フェーズ判定は呼び出し側。
    private Feature AddFeature(Vector2Int cell, FeatureType type, ZombieAI.Species species)
    {
        var f = new Feature { type = type, cell = cell, species = species };
        f.marker = CreateMarker(cell, type);
        if (type == FeatureType.Totem) ApplyTotem(f);
        if (type == FeatureType.Boss) grid.SetBossCell(cell);
        features[cell] = f;
        return f;
    }

    // ============ フロア切替用：要素の退避/復元 ============
    public struct FeatureRecord { public FeatureType type; public Vector2Int cell; public ZombieAI.Species species; }

    public List<FeatureRecord> ExportFeatures()
    {
        var list = new List<FeatureRecord>();
        foreach (var f in features.Values) list.Add(new FeatureRecord { type = f.type, cell = f.cell, species = f.species });
        return list;
    }

    public void ImportFeatures(List<FeatureRecord> recs)
    {
        ClearAllFeatures();
        if (recs == null) return;
        if (grid == null) grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        foreach (var r in recs)
        {
            if (grid != null && grid.GetTileType(r.cell.x, r.cell.y) == DungeonGridSystem.TileType.None) continue; // 壁化したマスはスキップ
            AddFeature(r.cell, r.type, r.species);
        }
    }

    public void RemoveFeature(Vector2Int cell)
    {
        if (!features.TryGetValue(cell, out var f)) return;
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) return; // 撤去も準備中のみ

        if (f.type == FeatureType.Totem) UndoTotem(f);
        if (f.marker != null) Destroy(f.marker);

        // 50%返金（素材要素は返金なし）
        var res = DungeonResourceManager.Instance;
        if (res != null && f.type != FeatureType.SpecialEnemy) res.RefundDP(CostOf(f.type), true);

        features.Remove(cell);
        Debug.Log($"🧩【撤去】{TypeName(f.type)} を {cell} から撤去しました。");
    }

    public void ClearAllFeatures()
    {
        foreach (var kv in features)
        {
            if (kv.Value.type == FeatureType.Totem) UndoTotem(kv.Value);
            if (kv.Value.marker != null) Destroy(kv.Value.marker);
        }
        features.Clear();
    }

    // ============ 戦闘連動 ============
    private void OnBattleStart()
    {
        foreach (var f in features.Values)
        {
            f.spawnTimer = 0f;
            f.spawnedThisWave = 0;
            if (f.type == FeatureType.Boss) SpawnDefender(f.cell, bossHpMult, bossAtkMult, CRIMSON, f.species, true); // 門番
            else if (f.type == FeatureType.SpecialEnemy) SpawnDefender(f.cell, specialHpMult, specialAtkMult, GOLD, f.species);
        }
    }

    private void TickSpawners()
    {
        foreach (var f in features.Values)
        {
            if (f.type != FeatureType.Spawner) continue;
            if (f.spawnedThisWave >= spawnerMaxPerWave) continue;
            f.spawnTimer += Time.deltaTime;
            if (f.spawnTimer >= spawnerInterval)
            {
                f.spawnTimer = 0f;
                f.spawnedThisWave++;
                SpawnDefender(f.cell, 1f, 1f, null, f.species);
            }
        }
    }

    private void SpawnDefender(Vector2Int cell, float hpMult, float atkMult, Color? tint, ZombieAI.Species species, bool guardian = false)
    {
        if (zombiePrefab == null)
        {
            var input = Object.FindFirstObjectByType<GridInputHandler>();
            if (input != null) zombiePrefab = input.ZombiePrefab;
        }
        if (zombiePrefab == null || grid == null) return;

        var go = Instantiate(zombiePrefab, grid.GridToWorld(cell.x, cell.y), Quaternion.identity);
        var z = go.GetComponent<ZombieAI>();
        if (z != null)
        {
            // 🧱 3層バフを合成：興奮ツリー × 遺物(全体) × トーテム(範囲) × 種族プロファイル × 魔王との相性
            float pm = EmotionTreeManager.Instance != null ? EmotionTreeManager.Instance.DefenderPowerMult : 1f; // 🌟 興奮ツリー
            float relicHp = RelicManager.Instance != null ? RelicManager.Instance.DefenderHpMult : 1f;          // 🏺 遺物
            float relicAtk = RelicManager.Instance != null ? RelicManager.Instance.DefenderAtkMult : 1f;
            float totem = TotemDefenderBuff(cell);                                                              // 🗿 トーテム範囲
            var prof = SpeciesProfile(species);                                                                 // 🐺 種族プロファイル
            float aff = DemonLord.Instance != null ? DemonLord.Instance.DefenderAffinityMult(species) : 1f;     // 🧬 種族相性

            z.species = species;
            z.hpMult = hpMult * pm * relicHp * totem * prof.hp * aff;
            z.atkMult = atkMult * pm * relicAtk * totem * prof.atk * aff;
            z.speedMult = 1f;
            z.isGuardian = guardian;
            // 🛡️ 配置セルをアンカーにしたガードモード（スポーン地点まで追わない）
            z.anchored = true; z.anchorCell = cell; z.leashRadius = defenderLeashRadius;
            // 色：ボス/特殊敵は識別色を優先、スポナーは種族色
            z.overrideTint = true; z.tintColor = tint ?? prof.tint;
        }
        spawnedDefenders.Add(go);
    }

    // 🗿 配置セルの周囲 totemBuffRadius 内にあるトーテム基数から強化倍率を算出（最大 totemBuffMaxStack 重ね）
    private float TotemDefenderBuff(Vector2Int cell)
    {
        int n = 0;
        foreach (var f in features.Values)
        {
            if (f.type != FeatureType.Totem) continue;
            if (Mathf.Abs(f.cell.x - cell.x) + Mathf.Abs(f.cell.y - cell.y) <= totemBuffRadius) n++;
        }
        n = Mathf.Min(n, totemBuffMaxStack);
        return 1f + totemDefenderBuffPer * n;
    }

    // 🐺 種族プロファイル（不死=硬い/獣=攻撃的/魔族=バランス）＋識別色
    private (float hp, float atk, Color tint) SpeciesProfile(ZombieAI.Species s)
    {
        switch (s)
        {
            case ZombieAI.Species.Beast: return (0.90f, 1.25f, new Color(0.90f, 0.55f, 0.25f));   // 獣＝橙
            case ZombieAI.Species.Demonkin: return (1.05f, 1.10f, new Color(0.70f, 0.45f, 0.90f)); // 魔族＝紫
            default: return (1.25f, 0.90f, new Color(0.45f, 0.85f, 0.55f));                         // 不死＝緑
        }
    }
    public static string SpeciesName(ZombieAI.Species s)
    {
        switch (s) { case ZombieAI.Species.Beast: return "獣"; case ZombieAI.Species.Demonkin: return "魔族"; default: return "不死"; }
    }

    // ⏱️ ターン終了(戦闘→準備)で、この防衛体を消滅させる（次ターン開始時に初期位置へ再配置＝位置リセット/重複防止）
    private void OnBattleEnd()
    {
        foreach (var go in spawnedDefenders) if (go != null) Destroy(go);
        spawnedDefenders.Clear();
    }

    // ============ トーテム効果 ============
    private void ApplyTotem(Feature f)
    {
        f.buffedNeighbors = new List<Vector2Int>();
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in dirs)
        {
            Vector2Int n = f.cell + d;
            var obj = grid.GetGridObject(n.x, n.y);
            if (obj == null) continue;
            var rd = obj.GetComponent<RoomData>();
            if (rd != null)
            {
                rd.attraction += totemAttractionBonus;
                f.buffedNeighbors.Add(n);
            }
        }
    }
    private void UndoTotem(Feature f)
    {
        if (f.buffedNeighbors == null) return;
        foreach (var n in f.buffedNeighbors)
        {
            var obj = grid.GetGridObject(n.x, n.y);
            if (obj == null) continue;
            var rd = obj.GetComponent<RoomData>();
            if (rd != null) rd.attraction -= totemAttractionBonus;
        }
        f.buffedNeighbors = null;
    }

    // ============ ヘルパー ============
    public int CostOf(FeatureType type)
    {
        int baseCost;
        switch (type)
        {
            case FeatureType.Totem: baseCost = totemCostDP; break;
            case FeatureType.Spawner: baseCost = spawnerCostDP; break;
            case FeatureType.Boss: baseCost = bossCostDP; break;
            default: baseCost = 0; break;
        }
        // 🧬 種族進化の相性でコスト補正（例：ドワーフ0.7 / 吸血0.8）
        float mult = DemonLord.Instance != null ? DemonLord.Instance.DefenderCostMult : 1f;
        return Mathf.RoundToInt(baseCost * mult);
    }
    public int SpecialMaterialCost => specialMaterialCost;
    private bool HasBoss()
    {
        foreach (var f in features.Values) if (f.type == FeatureType.Boss) return true;
        return false;
    }
    private string TypeName(FeatureType t)
    {
        switch (t) { case FeatureType.Totem: return "トーテム"; case FeatureType.Spawner: return "スポナー"; case FeatureType.Boss: return "ボスエリア"; default: return "特殊エネミー"; }
    }
    private Color ColorOf(FeatureType t)
    {
        switch (t) { case FeatureType.Totem: return TEAL; case FeatureType.Spawner: return VIOLET; case FeatureType.Boss: return CRIMSON; default: return GOLD; }
    }
    private string LetterOf(FeatureType t)
    {
        switch (t) { case FeatureType.Totem: return "T"; case FeatureType.Spawner: return "S"; case FeatureType.Boss: return "B"; default: return "E"; }
    }

    private static Sprite _square;
    private Sprite SquareSprite()
    {
        if (_square == null)
        {
            var tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.white); tex.Apply();
            _square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        }
        return _square;
    }
    private GameObject CreateMarker(Vector2Int cell, FeatureType type)
    {
        var go = new GameObject("Feature_" + type);
        go.transform.SetParent(transform, false);
        go.transform.position = grid.GridToWorld(cell.x, cell.y) + new Vector3(0, 0, -0.5f);
        go.transform.localScale = Vector3.one * 0.64f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSprite(); sr.color = ColorOf(type); sr.sortingOrder = 50;

        var txt = new GameObject("Letter");
        txt.transform.SetParent(go.transform, false);
        txt.transform.localPosition = new Vector3(0, 0, -0.1f);
        txt.transform.localScale = Vector3.one * 0.12f;
        var tm = txt.AddComponent<TextMesh>();
        tm.text = LetterOf(type); tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.fontSize = 48; tm.characterSize = 0.5f; tm.color = Color.white; tm.fontStyle = FontStyle.Bold;
        var mr = tm.GetComponent<MeshRenderer>(); if (mr != null) mr.sortingOrder = 51;
        return go;
    }
}
