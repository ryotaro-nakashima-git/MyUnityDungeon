using UnityEngine;

/// <summary>
/// 🏔️ 迷宮タイプ／空間タイプの「実効果」を一箇所に集めた窓口。
///
/// これまでこの2つは **BSPの分割パラメータと色味しか変えておらず、選ぶ理由が無かった**。
/// Civ の地形・地区のように「選択に損得がある」ようにするため、各タイプへ
/// **得と損をセットで**割り当て、各システムはここの getter を掛けるだけにする。
///
/// 迷宮タイプ（形の性格）
///   標準    : 配置枠+2（癖がない代わりに器が広い）
///   迷路    : 冒険者が長居する(満足の閾値+35%＝罠が効く) / 宝箱-25%
///   大空洞  : 部隊バフ+10%・防衛体の徘徊範囲+1 / 集客-15%
///   蟻の巣  : 宝箱+50%・集客+20% / トーテムの半径-1
///
/// 空間タイプ（属性の性格）
///   洞窟    : 不死系+15% / 冒険者の与ダメ-5%
///   遺跡    : 宝箱の価値+30% / 罠のクールダウン+20%（＝効きが悪い）
///   城砦    : 防衛体HP+15% / 集客-10%
///   溶岩    : 火+25%・氷-20%
///   氷雪    : 冒険者の移動-15% / 獣系-10%
///
/// 関連: [[DungeonGenerator]] [[civ-surface-districts]]。
/// </summary>
public static class DungeonTheme
{
    private static DungeonGenerator gen;
    private static DungeonGenerator Gen
    {
        get
        {
            if (gen == null) gen = Object.FindFirstObjectByType<DungeonGenerator>();
            return gen;
        }
    }
    public static DungeonGenerator.DungeonType Type
        => Gen != null ? Gen.CurrentDungeonType : DungeonGenerator.DungeonType.Standard;
    public static DungeonGenerator.SpaceType Space
        => Gen != null ? Gen.CurrentSpaceType : DungeonGenerator.SpaceType.Cave;

    private static bool T(DungeonGenerator.DungeonType t) => Type == t;
    private static bool S(DungeonGenerator.SpaceType s) => Space == s;

    // ---- 迷宮タイプ ----
    /// <summary>配置枠の加算（標準の強み）。</summary>
    public static int PlacementCapBonus => T(DungeonGenerator.DungeonType.Standard) ? 2 : 0;
    /// <summary>満足の閾値倍率。大きいほど長く居座る＝罠と防衛体に晒される。</summary>
    public static float SatisfyThresholdMult => T(DungeonGenerator.DungeonType.Labyrinth) ? 1.35f : 1f;
    /// <summary>宝箱の数の倍率。</summary>
    public static float ChestCountMult =>
        T(DungeonGenerator.DungeonType.Labyrinth) ? 0.75f :
        T(DungeonGenerator.DungeonType.Warren) ? 1.50f : 1f;
    /// <summary>部隊コンプの加算（大空洞は広いので隊が組みやすい）。</summary>
    public static float SquadCompBonus => T(DungeonGenerator.DungeonType.Cavern) ? 0.10f : 0f;
    /// <summary>防衛体が配置セルから離れられる距離の加算。</summary>
    public static int LeashBonus => T(DungeonGenerator.DungeonType.Cavern) ? 1 : 0;
    /// <summary>トーテムの効果半径の加算（蟻の巣は狭くて届きにくい）。</summary>
    public static int TotemRadiusBonus => T(DungeonGenerator.DungeonType.Warren) ? -1 : 0;

    // ---- 集客（迷宮タイプ＋空間タイプの合成）----
    /// <summary>ウェーブ人数に掛かる集客倍率。</summary>
    public static float LureMult
    {
        get
        {
            float m = 1f;
            if (T(DungeonGenerator.DungeonType.Cavern)) m *= 0.85f;
            if (T(DungeonGenerator.DungeonType.Warren)) m *= 1.20f;
            if (S(DungeonGenerator.SpaceType.Fortress)) m *= 0.90f;
            return m;
        }
    }

    // ---- 空間タイプ ----
    /// <summary>家系ごとの防衛体HP/攻撃の倍率。</summary>
    public static float FamilyMult(ZombieAI.Species fam)
    {
        float m = 1f;
        if (S(DungeonGenerator.SpaceType.Cave) && fam == ZombieAI.Species.Undead) m *= 1.15f;
        if (S(DungeonGenerator.SpaceType.Ice) && fam == ZombieAI.Species.Beast) m *= 0.90f;
        return m;
    }
    /// <summary>防衛体のHP倍率（城砦は硬い）。</summary>
    public static float DefenderHpMult => S(DungeonGenerator.SpaceType.Fortress) ? 1.15f : 1f;
    /// <summary>冒険者の与ダメージ倍率（洞窟は暗い）。</summary>
    public static float HeroDamageMult => S(DungeonGenerator.SpaceType.Cave) ? 0.95f : 1f;
    /// <summary>冒険者の移動速度倍率（氷雪は足を取られる）。</summary>
    public static float HeroSpeedMult => S(DungeonGenerator.SpaceType.Ice) ? 0.85f : 1f;
    /// <summary>宝箱の価値（喜び）倍率。遺跡はお宝が良い＝集客も上がる。</summary>
    public static float ChestValueMult => S(DungeonGenerator.SpaceType.Ruins) ? 1.30f : 1f;
    /// <summary>罠のクールダウン倍率（大きいほど再作動が遅い＝弱い）。</summary>
    public static float TrapCooldownMult => S(DungeonGenerator.SpaceType.Ruins) ? 1.20f : 1f;
    /// <summary>属性ダメージの倍率（溶岩は火が強く氷が弱い）。</summary>
    public static float ElementMult(MagicElement e)
    {
        if (!S(DungeonGenerator.SpaceType.Lava)) return 1f;
        if (e == MagicElement.Fire) return 1.25f;
        if (e == MagicElement.Ice) return 0.80f;
        return 1f;
    }

    // ---- 表示用 ----
    public static string TypeName(DungeonGenerator.DungeonType t)
    {
        switch (t)
        {
            case DungeonGenerator.DungeonType.Labyrinth: return "迷路";
            case DungeonGenerator.DungeonType.Cavern: return "大空洞";
            case DungeonGenerator.DungeonType.Warren: return "蟻の巣";
            default: return "標準";
        }
    }
    public static string SpaceName(DungeonGenerator.SpaceType s)
    {
        switch (s)
        {
            case DungeonGenerator.SpaceType.Ruins: return "遺跡";
            case DungeonGenerator.SpaceType.Fortress: return "城砦";
            case DungeonGenerator.SpaceType.Lava: return "溶岩";
            case DungeonGenerator.SpaceType.Ice: return "氷雪";
            default: return "洞窟";
        }
    }
    /// <summary>UIに出す「得と損」の説明。</summary>
    public static string TypeEffect(DungeonGenerator.DungeonType t)
    {
        switch (t)
        {
            case DungeonGenerator.DungeonType.Labyrinth: return "<color=#5cc47c>冒険者が長居する(+35%)</color> / <color=#e05a5a>宝箱-25%</color>";
            case DungeonGenerator.DungeonType.Cavern: return "<color=#5cc47c>部隊バフ+10%・徘徊+1</color> / <color=#e05a5a>集客-15%</color>";
            case DungeonGenerator.DungeonType.Warren: return "<color=#5cc47c>宝箱+50%・集客+20%</color> / <color=#e05a5a>トーテム半径-1</color>";
            default: return "<color=#5cc47c>配置枠+2</color> / 癖がない";
        }
    }
    public static string SpaceEffect(DungeonGenerator.SpaceType s)
    {
        switch (s)
        {
            case DungeonGenerator.SpaceType.Ruins: return "<color=#5cc47c>宝箱の価値+30%</color> / <color=#e05a5a>罠の再作動-20%</color>";
            case DungeonGenerator.SpaceType.Fortress: return "<color=#5cc47c>防衛体HP+15%</color> / <color=#e05a5a>集客-10%</color>";
            case DungeonGenerator.SpaceType.Lava: return "<color=#5cc47c>火+25%</color> / <color=#e05a5a>氷-20%</color>";
            case DungeonGenerator.SpaceType.Ice: return "<color=#5cc47c>冒険者の移動-15%</color> / <color=#e05a5a>獣系-10%</color>";
            default: return "<color=#5cc47c>不死系+15%</color> / <color=#e05a5a>冒険者の与ダメ-5%</color>";
        }
    }
}
