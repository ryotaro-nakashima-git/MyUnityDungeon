using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🎁 **撒く等級**（装備水準の作り直し・③④）。宝箱に入れる装備の等級を、階層ごとに決める。
///
/// <para>
/// ⚠⚠ <b>なぜ要るか</b>：世界の装備水準が跳ねる原因は係数ではなく、
///   持ち出された物に<b>等級が無く量だけ</b>だったこと（`gearLevel += 量` の和）。
///   宝箱に等級を持たせて初めて「<b>こちらが撒いた等級が、世界が到達できる上限になる</b>」が成立する。
///   → [[gear-level-rework]]／`dev_log.md` 続き36。
/// </para>
///
/// <para>
/// ⚠ <b>配置は生成時のまま。等級は開けた瞬間に決める（遅延解決）。</b>
///   生成器には一切手を入れない。つまみを動かせば次に開けられたぶんから効く。
/// ⚠ <b>10×階層数の表を触らせない。</b>つまみは階ごとに「基準の等級」と「ばらつき」の2つだけ。
/// ⚠ <b>見返りは等級に比例させる</b>（`RewardMult`）。しないと「等級1だけ撒く」が
///   無条件の最適解になり、このつまみ自体が意味を失う。
/// ⚠ <b>この値はセーブに載る</b>（＝状態）。`readonly` にしないこと（→ [[save-sound-settings]]）。
/// </para>
///
/// 関連: [[LureEconomy]]（世界水準の追従） [[AdventurerAI]]（個体の等級と points） [[EquipmentCatalog]]。
/// </summary>
public static class TreasureGrades
{
    /// <summary>プレイヤーが目にする等級は 1〜10。カタログ索引は「等級 − 1」。</summary>
    public const int MinGrade = 1, TopGrade = 10;
    /// <summary>研究を1つも取っていないときに撒ける上限。</summary>
    public const int BaseUnlocked = 4;

    // ⚠ セーブに載る状態。階が増えたら遅延で伸ばす（`Ensure`）。
    private static List<int> baseGrade = new List<int>();
    private static List<int> spread = new List<int>();

    // ============ 解禁 ============

    /// <summary>いま撒ける最高等級（1〜10）。⚠ 研究で伸びる（領域研究・罠と宝箱と同じ枝）。</summary>
    public static int MaxUnlocked
    {
        get
        {
            if (ResearchState.IsResearched("d_chest_g10")) return 10;
            if (ResearchState.IsResearched("d_chest_g8")) return 8;
            if (ResearchState.IsResearched("d_chest_g6")) return 6;
            return BaseUnlocked;
        }
    }

    // ============ つまみ ============

    private static void Ensure(int floor)
    {
        while (baseGrade.Count <= floor)
        {
            int f = baseGrade.Count;
            // 既定は「深いほど良い物」。⚠ 既定でいきなり上限を撒かない（撒くのは選択であるべき）。
            baseGrade.Add(Mathf.Clamp(1 + f, MinGrade, BaseUnlocked));
            spread.Add(f == 0 ? 0 : 1);
        }
    }

    public static int BaseOf(int floor) { floor = Mathf.Max(0, floor); Ensure(floor); return baseGrade[floor]; }
    public static int SpreadOf(int floor) { floor = Mathf.Max(0, floor); Ensure(floor); return spread[floor]; }

    public static void SetBase(int floor, int v)
    {
        floor = Mathf.Max(0, floor); Ensure(floor);
        baseGrade[floor] = Mathf.Clamp(v, MinGrade, MaxUnlocked);
    }
    public static void SetSpread(int floor, int v)
    {
        floor = Mathf.Max(0, floor); Ensure(floor);
        spread[floor] = Mathf.Clamp(v, 0, 3);
    }

    public static int MaxSpread => 3;

    /// <summary>その階で実際に出うる等級の幅（解禁で頭打ちになる）。</summary>
    public static int LowOf(int floor) => Mathf.Clamp(BaseOf(floor) - SpreadOf(floor), MinGrade, MaxUnlocked);
    public static int HighOf(int floor) => Mathf.Clamp(BaseOf(floor) + SpreadOf(floor), MinGrade, MaxUnlocked);

    /// <summary>この迷宮で撒く最高等級（1〜10）＝<b>世界の装備水準が到達できる上限</b>。</summary>
    public static int SeededMaxGrade
    {
        get
        {
            int n = FloorCount, m = MinGrade;
            for (int i = 0; i < n; i++) { int h = HighOf(i); if (h > m) m = h; }
            return m;
        }
    }

    /// <summary>同上をカタログ索引で（0〜9）。`EquipmentCatalog` の等級と揃える。</summary>
    public static int SeededMaxCatalog => Mathf.Clamp(SeededMaxGrade - 1, 0, EquipmentCatalog.MaxGrade);

    /// <summary>いま盤にある階の数（1以上）。</summary>
    public static int FloorCount
    {
        get
        {
            var fm = DungeonFloorManager.Instance;
            int n = fm != null ? Mathf.Max(fm.BuiltFloorCount, fm.PlannedFloorCount) : 1;
            return Mathf.Max(1, n);
        }
    }

    // ============ 開けた瞬間に決める ============

    /// <summary>
    /// 🎲 その階の宝箱1つぶんの等級を引く（<b>開けた瞬間に呼ぶ</b>）。返すのは<b>カタログ索引</b>（0〜9）。
    /// ⚠ 盤の配置は触らない。同じ宝箱でも開けるたびに引き直す＝つまみが即座に効く。
    /// </summary>
    public static int RollCatalog(int floor)
    {
        floor = Mathf.Max(0, floor);
        int b = BaseOf(floor), s = SpreadOf(floor);
        int g = s <= 0 ? b : b + Random.Range(-s, s + 1);
        g = Mathf.Clamp(g, MinGrade, MaxUnlocked);
        return Mathf.Clamp(g - 1, 0, EquipmentCatalog.MaxGrade);
    }

    // ============ 見返り ============

    /// <summary>
    /// 💰 その等級の宝箱1つぶんの見返り倍率。
    /// ⚠⚠ <b>索引に比例させない。</b>等級段(7-13)は1段+6%しか強くならないので、
    ///   索引で払うと「等級8〜10＝ほとんど武装させずに大金」という抜け道になる。
    ///   <b>実際に相手へ渡る強さ（攻撃倍率と硬さの平均）で払う</b>と、両刃が両刃のまま保たれる。
    /// </summary>
    public static float RewardMult(int catalogGrade)
    {
        int g = Mathf.Clamp(catalogGrade, 0, EquipmentCatalog.MaxGrade);
        return 0.5f * (EquipmentCatalog.WeaponAtkMult(g) + EquipmentCatalog.ArmorHpMult(g));
    }

    /// <summary>宝箱1つを開けられたときに積む『喜び』（逃げ切られたときDPになる）。</summary>
    public static float JoyOf(int catalogGrade) => 20f * RewardMult(catalogGrade);
    /// <summary>同・感情ツリーへの供給。</summary>
    public static int EmotionOf(int catalogGrade) => Mathf.Max(1, Mathf.RoundToInt(2f * RewardMult(catalogGrade)));

    // ============ 表示 ============

    /// <summary>「等級3（鋼）」の形。UIと『先触れ』で共通に使う。</summary>
    public static string Label(int catalogGrade)
        => "等級" + (Mathf.Clamp(catalogGrade, 0, EquipmentCatalog.MaxGrade) + 1) + "（" + EquipmentCatalog.Name(catalogGrade) + "）";

    public static void Reset() { baseGrade = new List<int>(); spread = new List<int>(); }
}
