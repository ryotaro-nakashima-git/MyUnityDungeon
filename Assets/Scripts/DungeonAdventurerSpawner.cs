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
    /// ⏩ 控えを突入させる（階層が抜かれたときに呼ばれる）。
    ///
    /// ⚠⚠ **いま出している塊のぶんだけ**にする。
    ///   旧仕様は「残り全部」を一気に吐いていた。1体ずつの点滴だった頃はそれで良かったが、
    ///   波（塊）に変えたあとで全部吐くと、**塊と塊のあいだの息継ぎが消えて元に戻る**。
    ///   息継ぎは「立て直しと号令の窓」としてわざと空けているので、ここで潰してはいけない。
    ///   ただし塊の途中で止めると入口に取り残しが出るので、**その塊は必ず出し切る**。
    /// </summary>
    public void FlushRemaining()
    {
        if (!isSpawning) return;
        int n = Mathf.Min(Remaining, Mathf.Max(0, batchSize - spawnedInBatch));
        for (int i = 0; i < n; i++) { SpawnAdventurerWaveUnit(); spawnedInBatch++; }
        if (n > 0) Debug.Log($"⏩『雪崩れ込み』入口に控えていた {n} 体が一斉に突入した（階層は既に抜かれている）");
    }

    // 🔴 DungeonTurnManagerから戦闘フェーズ開始時に呼ばれるトリガー関数
    public void StartWaveForThisTurn(int turnNumber)
    {
        isSpawning = true;
        currentSpawnedCount = 0;

        // 🔮 人数も中身も **準備フェーズの頭で確定済み**（→ [[WaveRoster]]）。
        //    ⚠ 以前はこの場で人数を決め、各冒険者は湧いた瞬間に自分で職とランクを引いていた。
        //      それでは『先触れ』で予告できない（引く前だから誰も知らない）ので、名簿を先に作る形にした。
        //      人数の式そのものは WaveRoster.RollCount へ**そのまま**移してある。
        WaveRoster.EnsureRolled(turnNumber);
        totalSpawnCountForThisTurn = Mathf.Max(1, WaveRoster.Count);

        // ⚔️⚔️ **束ねて送り込む（波）**。ここが「防衛戦が20秒で終わる」の正体だった。
        //
        // 旧仕様：`max(4.0 - turn*0.2, 1.5)` 秒おきに**1体ずつ**。T12なら15体を1.6秒おき＝24秒。
        //   実際にT1〜T12を通しで遊んだところ、**画面に居る冒険者は常に2〜4体**しかいなかった。
        //   湧くそばから溶けるので、群れにならず、圧力にもならない。
        //   制限時間180秒に対して毎ターン20秒で片付き、号令を押す場面すら来なかった。
        //
        // 新仕様：同じ人数を**数回の塊**に分けて送る。
        //   - 塊の中は 0.35秒おき＝ほぼ同時に着弾するので、**群れとして戦線を作る**
        //     （聖職者が回復し、魔術師が撃つ。1体ずつ来るときには起きなかったことが起きる）
        //   - 塊と塊のあいだは息継ぎになり、**そこが号令と立て直しの窓**になる
        // ⚠⚠ **総人数も個々の強さも1ミリも変えていない。** 変えたのは届き方だけ。
        //   カーブ（→ [[curve-measurement-t100]]）に手を入れずに密度だけを上げるのが狙い。
        // 🚪 備え『狭き門』：入口を狭めると塊が半分になる（→ [[WardSystem]]）
        batchSize = Mathf.Clamp(Mathf.CeilToInt(totalSpawnCountForThisTurn / 3f * WardSystem.BatchMult), 2, 7);
        currentSpawnInterval = 0.35f;                                  // 塊の中（ほぼ同時）
        // ⚠ 息継ぎは**戦闘より短く**する。最初 16秒にしたら、塊が5秒で溶けたあと
        //   **11秒間だれも居ない**時間ができて、密度が上がるどころか「待ち」が増えた（実測）。
        //   前の塊を捌いている最中に次が着く長さにして、圧力が途切れないようにする。
        batchGap = Mathf.Max(5f, 9f - turnNumber * 0.2f);
        spawnedInBatch = 0;
        spawnTimer = currentSpawnInterval;                             // 最初の1体は即座に
    }

    // 🌊 波の刻み（StartWaveForThisTurn で決める）
    private int batchSize = 4;
    private int spawnedInBatch = 0;
    private float batchGap = 14f;

    private void Update()
    {
        if (!isSpawning) return;

        spawnTimer += Time.deltaTime;
        // 塊を吐き切ったら、次の塊まで待つ
        float wait = (spawnedInBatch >= batchSize) ? batchGap : currentSpawnInterval;
        if (spawnTimer >= wait)
        {
            spawnTimer = 0f;
            if (spawnedInBatch >= batchSize) spawnedInBatch = 0;       // 息継ぎ明け
            SpawnAdventurerWaveUnit();
            spawnedInBatch++;
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