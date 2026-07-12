using UnityEngine;

/// <summary>
/// 誘導経済（原作シオンの情報操作／CDO2の集客）の心臓部＝「世界の脅威度」を持つ。
///
/// 泳がせfarmingの核: 冒険者を"逃がす"と噂が広まり(Fame↑)、世界の脅威度が上がる。
/// 脅威度が上がるほど、来る勇者は多く・強くなり、撃破報酬も増える（＝需要を作るほど儲かるが敵も育つ両刃）。
/// - 全滅させる = 安全だが脅威度が上がらず稼ぎも伸びない
/// - 泳がせる   = 稼げるが脅威度が上がりウェーブが厳しくなる
///
/// 静的保持（プレイセッション内。ドメインリロードで初期化＝新規プレイは脅威度1.0）。装備ドロップ両刃(gear)は後続で拡張。
/// 関連: [[internal-affairs-design]] AdventurerAI(逃走/撃破フック) / DungeonAdventurerSpawner(ウェーブ数) / GameUIManager(HUD)。
/// </summary>
public static class LureEconomy
{
    private static float threat = 1f;      // 世界の脅威度（1.0スタート）
    private static float gearLevel = 0f;   // 世界の装備水準（0スタート。逃がした略奪者が広める）
    private const float MinThreat = 1f, MaxThreat = 6f, MaxGear = 100f;

    // チューニング
    private const float EscapeThreatBase = 0.05f; // 逃走1体あたりの脅威度上昇（基礎）
    private const int   EscapeFame = 25;          // 逃走で広まる噂（Fame加算）
    private const float AtkPerThreat = 0.5f;      // 脅威度→勇者攻撃倍率の伸び
    private const float WavePerThreat = 3f;       // 脅威度→追加ウェーブ数
    private const float RevenuePerThreat = 0.5f;  // 脅威度→撃破DP倍率の伸び
    private const float GearSpreadFrac = 0.5f;    // 逃走時、持ち出した装備が世界水準に加わる割合
    private const float HpPerGear = 0.02f;        // 装備水準1あたり勇者HP+2%
    private const float AtkPerGear = 0.03f;       // 装備水準1あたり勇者攻撃+3%

    public static float Threat => threat;
    public static string ThreatLabel => threat.ToString("0.00");
    public static float GearLevel => gearLevel;
    public static string GearLabel => gearLevel.ToString("0.0");

    public static void Reset() { threat = MinThreat; gearLevel = 0f; }

    /// <summary>冒険者が"逃走"して生還したとき（＝噂を広め、次はより強く戻る）。</summary>
    public static void OnHeroEscaped(int heroLevel)
    {
        threat = Mathf.Min(MaxThreat, threat + EscapeThreatBase * (1f + heroLevel * 0.01f)); // 高レベルほど噂が大きい
        if (DungeonResourceManager.Instance != null) DungeonResourceManager.Instance.AddFame(EscapeFame);
    }

    /// <summary>略奪した装備を持って逃げ切ったとき＝敵陣の装備水準が上がる（両刃）。</summary>
    public static void OnGearEscaped(float carriedGear)
    {
        if (carriedGear <= 0f) return;
        gearLevel = Mathf.Min(MaxGear, gearLevel + carriedGear * GearSpreadFrac);
    }

    /// <summary>略奪者を"倒した"とき＝戦利品を素材として回収できる（武装拡散を防ぐ）。</summary>
    public static int GearRecoverMaterials(float carriedGear) => Mathf.Max(0, Mathf.RoundToInt(carriedGear));

    // 脅威度→勇者強度（スポーン時に適用）。※装備水準(gearLevel)の効果は EquipmentCatalog の武具グレードで表現するため、
    //   ここは脅威度のみ（二重計上を防ぐ）。gearLevel は AdventurerAI が装備グレード選択に使う。
    public static float HeroHpMult => threat;
    public static float HeroAtkMult => (1f + (threat - 1f) * AtkPerThreat);
    // 脅威度→ウェーブ増員
    public static int ExtraWaveCount => Mathf.FloorToInt((threat - 1f) * WavePerThreat);
    // 脅威度→撃破報酬（強い勇者ほど旨味）
    public static float RevenueMult => 1f + (threat - 1f) * RevenuePerThreat;
}
