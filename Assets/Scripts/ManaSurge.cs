using UnityEngine;

/// <summary>
/// 🌊 魔素の奔流 ── ターンの頭にたまに起きる、配下の成長にまつわる出来事。
///
/// **なぜ要るか**：こちら側でターンに応じて自動で伸びる軸は個体Lvだけで、しかも1波0.8Lvぶん。
/// 冒険者は +0.8Lv/ターン に fame の項が乗るので、平時の伸びだけでは差が開く一方だった。
/// ここは「**平時の倍率ではなく、たまに来る跳ね**」で埋める。跳ねなので、来たターンに
/// 何を置くか・どこを厚くするかという**判断**が生まれる。
///
/// ⚠ **常時効いているならそれは倍率であって、イベントではない**（→ [[difficulty-curve-orders]]）。
///   だから必ず `Cooldown` ターンに1回しか起きないようにし、効果は**そのターン限り**にする。
///   実測の目安：6ターンに1回＝稼働率 1/6。
///
/// 2種類ある：
/// - **覚醒**：いま持っている配下すべてが +1〜3 Lv（数に効く＝置いていない個体にも効く）
/// - **奔流**：そのターンに入る経験値が**深い階ほど増える**（B1F ×1.0 → B5F ×4.0）
///   ＝「深い階に置くと育つ」を跳ねで強調する。深い階が弱くなる問題への直接の手当て。
/// 関連: [[MinionRoster]] [[DungeonTurnManager]]。
/// </summary>
public static class ManaSurge
{
    public enum Kind { None, Awakening, Flood }

    /// <summary>事件と事件のあいだ（これを短くすると"イベント"ではなく"倍率"になる）。</summary>
    public const int Cooldown = 6;
    /// <summary>奔流のとき、1階層深くなるごとに経験値が何倍ずつ増えるか。</summary>
    public const float FloodPerFloor = 0.75f;

    private static int cd = 3;          // 初回は少し早めに来てよい
    private static Kind current = Kind.None;
    private static int lastAwakenGain;

    public static Kind Current => current;
    public static bool Active => current != Kind.None;
    public static int LastAwakenGain => lastAwakenGain;

    public static void Reset() { cd = 3; current = Kind.None; lastAwakenGain = 0; }

    /// <summary>🌊 奔流のときだけ、深い階の経験値を増やす倍率。平時は 1.0。</summary>
    public static float FloorExpMult(int floorIndex)
        => current == Kind.Flood ? 1f + FloodPerFloor * Mathf.Max(0, floorIndex) : 1f;

    /// <summary>ターンの頭に呼ぶ。効果は**このターン限り**なので、まず前ターンぶんを消す。</summary>
    public static void TickTurn()
    {
        current = Kind.None;                 // ⏱️ 前のターンの効果はここで切れる
        if (cd > 0) { cd--; return; }
        cd = Cooldown;

        if (Random.Range(0, 2) == 0) Awaken();
        else Flood();
    }

    private static void Awaken()
    {
        current = Kind.Awakening;
        lastAwakenGain = Random.Range(1, 4);          // 1〜3
        int n = 0;
        foreach (var v in MinionRoster.All)
        {
            if (v.level >= MinionRoster.MaxLevel) continue;
            MinionRoster.AddExp(v.id, MinionRoster.ExpPerLevel * lastAwakenGain);
            n++;
        }
        if (n == 0) { current = Kind.None; cd = 1; return; }   // 配下がいないなら不発（次のターンに持ち越す）
        Debug.Log($"🌊『魔素の覚醒』配下 {n} 体が +{lastAwakenGain}Lv");
        NotifySystem.Push($"<b>魔素の覚醒</b>　配下 {n} 体が <b>+{lastAwakenGain}Lv</b>", NotifySystem.Kind.Gain);
    }

    private static void Flood()
    {
        current = Kind.Flood;
        Debug.Log("🌊『魔素の奔流』このターンは深い階ほど経験値が増える（1階ごとに +75%）");
        NotifySystem.Push("<b>魔素の奔流</b>　このターンは<b>深い階ほど経験値が増える</b>（1階ごとに +75%）",
            NotifySystem.Kind.Gain);
    }

    /// <summary>UIに出す一言（ターン頭の報告に載せる）。</summary>
    public static string Headline()
    {
        switch (current)
        {
            case Kind.Awakening: return $"魔素の覚醒 ── 配下すべてが +{lastAwakenGain}Lv";
            case Kind.Flood: return "魔素の奔流 ── このターンは深い階ほど経験値が増える";
            default: return "";
        }
    }
}
