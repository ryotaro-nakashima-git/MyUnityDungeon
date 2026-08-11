using UnityEngine;

public class DungeonAdventurerSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject adventurerPrefab;
    [SerializeField] private Vector3 spawnPosition = Vector3.zero;

    private float spawnTimer = 0f;
    private float currentSpawnInterval = 3.0f;

    // ウェーブの内部状態管理
    private bool isSpawning = false;
    private int totalSpawnCountForThisTurn = 0;
    private int currentSpawnedCount = 0;

    public bool IsSpawning => isSpawning;
    public int Remaining => Mathf.Max(0, totalSpawnCountForThisTurn - currentSpawnedCount);

    /// <summary>
    /// ⏩ 残りを一気に突入させる。
    /// ⚠ 降下は「全員が入り切るまで待つ」設計だが、湧く間隔は最短でも1.5秒なので
    ///   T15なら湧き切るのに約27秒かかる。階層を早く片付けると**何も起きない時間**ができていた。
    ///   片付いた時点で控えを雪崩れ込ませることで、待ちを消しつつ「全員がその階層を通る」形は保つ。
    /// </summary>
    public void FlushRemaining()
    {
        if (!isSpawning) return;
        int n = Remaining;
        for (int i = 0; i < n; i++) SpawnAdventurerWaveUnit();
        if (n > 0) Debug.Log($"⏩『雪崩れ込み』入口に控えていた {n} 体が一斉に突入した（階層は既に抜かれている）");
    }

    // 🔴 DungeonTurnManagerから戦闘フェーズ開始時に呼ばれるトリガー関数
    public void StartWaveForThisTurn(int turnNumber)
    {
        isSpawning = true;
        currentSpawnedCount = 0;

        // 📈 ターンが進むほど、突入してくる冒険者の数が増える（例: ターン1なら4体、ターン2なら6体...）
        // ⚖️ 防衛側は『配置枠』で頭打ちになるのに攻撃側だけ O(turn) で増え続けるとオーダーが合わない。
        //    旧: 3+turn*2 → T11で25体。配置枠8（うち戦力は5-6）に対して4倍の物量になり、
        //    個々が弱くても数で押し切られていた＝『急に瞬殺される』の主因。
        //    伸びを ×1.0 に落として上限20で飽和させ、以降の圧力は"人数"ではなく"質"で上げる。
        totalSpawnCountForThisTurn = Mathf.Min(20, 3 + turnNumber)
            + (EmotionTreeManager.Instance != null ? EmotionTreeManager.Instance.BonusAdventurers : 0) // 🌟 歓喜ツリー=集客
            + LureEconomy.ExtraWaveCount                       // 🕸️ 誘導経済：脅威度が高いほど大挙して押し寄せる
            + DungeonFloorManager.RenownBonusAdventurers;      // 🏛️ 領域の名声：広い迷宮ほど噂を呼ぶ
        // 🏺 静寂の鈴：集客を捨てて敵を弱くする ／ 🏔️ 迷宮・空間タイプの集客補正
        float lure = DungeonTheme.LureMult * Difficulty.AdvCountMult * NarrativeSystem.LureMult;   // ⚖️ 難易度／🕯️ 形見『静寂の遺灰』
        if (RelicManager.Instance != null) lure *= RelicManager.Instance.LureMult;
        lure *= MutationSystem.WaveCountMult;   // 🧬 世界の変異『群れ』
        totalSpawnCountForThisTurn = Mathf.Max(1, Mathf.RoundToInt(totalSpawnCountForThisTurn * lure));

        // ⚡ ターンが進むほど、ギルドの出撃間隔が縮まり、一気に押し寄せてくる（最短1.5秒間隔）
        currentSpawnInterval = Mathf.Max(4.0f - (turnNumber * 0.2f), 1.5f);
        
        spawnTimer = currentSpawnInterval; // 最初は即座に1体目を湧かせる
    }

    private void Update()
    {
        if (!isSpawning) return;

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= currentSpawnInterval)
        {
            spawnTimer = 0f;
            SpawnAdventurerWaveUnit();
        }
    }

    private void SpawnAdventurerWaveUnit()
    {
        if (adventurerPrefab == null) return;

        // 🏰 自動生成された迷宮の『入口セル』から湧かせる（未生成時はInspectorのspawnPositionにフォールバック）
        Vector3 spawnPos = spawnPosition;
        DungeonGridSystem gridSystem = GameObject.FindAnyObjectByType<DungeonGridSystem>();
        if (gridSystem != null)
        {
            Vector2Int entrance = gridSystem.EntranceCell;
            if (gridSystem.GetTileType(entrance.x, entrance.y) == DungeonGridSystem.TileType.None)
            {
                return; // 入口がまだ床でない（未生成）なら安全にスキップ
            }
            spawnPos = gridSystem.GridToWorld(entrance.x, entrance.y);
        }

        // 生成
        Instantiate(adventurerPrefab, spawnPos, Quaternion.identity);
        currentSpawnedCount++;

        Debug.Log($"📢『ギルドの進撃』冒険者がダンジョンを急襲！ウェーブ進行度: ({currentSpawnedCount}/{totalSpawnCountForThisTurn})");

        // 今回のターンの規定数に達したら、このターンの『湧き（召喚）』自体は終了
        if (currentSpawnedCount >= totalSpawnCountForThisTurn)
        {
            isSpawning = false;
            Debug.Log("🏁『湧き完了』今ターンのすべての冒険者がダンジョン内に進入しました。あとは防衛線の結果を待ちます。");
        }
    }
}