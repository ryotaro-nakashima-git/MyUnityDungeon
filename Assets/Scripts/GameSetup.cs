using UnityEngine;

/// <summary>
/// 🎬 ゲーム開始時の設定（タイトル画面で選ぶ「世界設定」）と、そこから決まる**初期DP**。
///
/// 設計：**開始予算から初期迷宮の建造費を引いた残りが初期DP**。
///   予算   = 1000 + 400 ×(階層数-1)          … 深い迷宮は器が大きいぶん予算も出る（ただし建造費の伸びより小さい）
///   建造費 = (300 + 宝箱費) × 階層数 + タイプ費
///   初期DP = max(100, 予算 - 建造費)
/// 例：1層・宝箱『中』・標準 → 1000 -(300+300) -200 = **200 DP**。
///
/// つまり「豪華な迷宮で始める＝手元が乏しい／質素に始める＝軍資金が厚い」という
/// 最初の一手のトレードオフになる。宝箱を多くすれば集客と収入は増えるので、
/// 開始資金の薄さは**序盤を耐えられるか**という賭けになる。
///
/// ⚠ 値は研究や選択で変わるので **const にしない**（[[deep-floor-leveling]] の教訓）。
/// 関連: [[DungeonGenerator]] [[DungeonResourceManager]] [[SurfaceMap]]。
/// </summary>
public static class GameSetup
{
    /// <summary>タイトル画面の入力待ちか（true の間は迷宮を自動生成しない）。GameUIManager.Awake で立てる。</summary>
    public static bool WaitForTitle = false;
    /// <summary>『この世界で始める』が押されたか。</summary>
    public static bool Started = false;

    // ---- 選択内容（タイトル画面が書き、開始時に各システムへ流す）----
    public static int DungeonTypeIdx = 0;   // 0標準 1迷路 2大空洞 3蟻の巣
    public static int SpaceTypeIdx = 0;     // 0洞窟 1遺跡 2城砦 3溶岩 4氷雪
    public static int ChestIdx = 1;         // 0少 1中 2多
    public static int FloorCount = 1;       // 1〜3
    public static SurfaceGen.Size WorldSize = SurfaceGen.Size.Medium;
    public static int Seed = 0;             // 0＝生成時にランダム
    public static int DifficultyIdx = 1;    // ⚖️ 0安寧 1標準 2苛烈 3絶望 → [[Difficulty]]
    public static bool DailySeed = false;   // 📅 今日の日付から種を決めた周か（戦績で別扱いにする）

    /// <summary>どれだけ豪華に始めても、これだけは手元に残す。</summary>
    public static int MinStartDP { get { return 100; } }

    /// <summary>📅 今日の種。同じ日なら誰がやっても同じ世界になる（[[RunStats]] の日替わり記録に使う）。</summary>
    public static int TodaySeed
    {
        get
        {
            var d = System.DateTime.Now;
            return d.Year * 10000 + d.Month * 100 + d.Day;
        }
    }
    public static string TodayLabel { get { return System.DateTime.Now.ToString("yyyy/MM/dd"); } }

    /// <summary>
    /// 開始予算。階層を増やすと器が大きくなるので予算も出るが、**伸びは建造費より小さい**（+400/層 対 +400〜900/層）。
    /// 結果として「深く始めるなら宝箱は少なく」というトレードオフになる：
    ///   宝箱『少』なら深さはほぼ無料 ／『中』以上で深くすると手元が下限まで削れる。
    /// </summary>
    public static int Budget { get { return 1000 + 400 * (Mathf.Clamp(FloorCount, 1, 3) - 1); } }

    /// <summary>宝箱の量ごとの1階層あたりの費用。</summary>
    public static int ChestCost(int chestIdx)
    {
        if (chestIdx <= 0) return 100;
        if (chestIdx == 1) return 300;
        return 600;
    }

    /// <summary>迷宮タイプの費用（強い性格ほど高い）。</summary>
    public static int TypeCost(int typeIdx)
    {
        switch (typeIdx)
        {
            case 1: return 0;    // 迷路：宝箱が減るぶん安い
            case 2: return 100;  // 大空洞
            case 3: return 250;  // 蟻の巣：宝箱+50%・集客+20%
            default: return 200; // 標準：配置枠+2
        }
    }

    /// <summary>初期迷宮の建造費（＝予算から引かれる額）。</summary>
    public static int BuildCost
    {
        get
        {
            int f = Mathf.Clamp(FloorCount, 1, 3);
            return (300 + ChestCost(ChestIdx)) * f + TypeCost(DungeonTypeIdx);
        }
    }

    /// <summary>初期DP（＝予算 − 建造費、下限あり）。</summary>
    public static int StartDP { get { return Mathf.Max(MinStartDP, Budget - BuildCost); } }

    /// <summary>下限に張り付いている＝予算を超えた構成を選んでいる。</summary>
    public static bool OverBudget { get { return Budget - BuildCost < MinStartDP; } }

    /// <summary>ドメインリロードを跨いだ残骸を消す（Play開始のたびに呼ぶ）。</summary>
    public static void ResetForNewSession()
    {
        WaitForTitle = false; Started = false;
        DungeonTypeIdx = 0; SpaceTypeIdx = 0; ChestIdx = 1; FloorCount = 1;
        WorldSize = SurfaceGen.Size.Medium; Seed = 0;
    }
}
