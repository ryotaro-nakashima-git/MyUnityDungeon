using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🛒 行商人（CDO2の「歩行者」＝限定ショップ）。**品揃えがターンで変わり、逃したものは戻ってこない**。
///
/// **なぜ「限定」なのか**：いつでも全部買える店にすると、DPの使い道が
/// 「いま必要なものを買う」だけになり、**判断が消える**。
/// 並んでいる3つが今回きりなら、「これは今買うべきか」を毎ターン考えることになる。
///
/// **設計**
/// - 品揃えは **3枠**。ターンが変わると引き直す（買った枠は売り切れのまま残る）。
/// - ⚠ 引き直しは**ターンの頭で1回だけ**。毎フレーム引くと画面を開くたびに変わってしまう。
/// - 希少度で重みを変える（並60% / 上物30% / 稀少10%）。
/// - ⚠ 値段は `AccessoryCatalog` の `price` をそのまま使う。店側で割増すると、
///   同じものが場所によって違う値段になり、価値の基準が二重になる。
///
/// 関連: [[AccessoryCatalog]] [[SummonGacha]]（もう一つの入手経路）。
/// </summary>
public static class MerchantShop
{
    /// <summary>
    /// 🛒 棚の数。⚠⚠ **`const` にしてはいけない**（研究で伸びるのに一生反映されない ―― 4度目の罠）。
    /// 『強欲の刻印』で 3→4。→ [[research-dead-nodes]]
    /// </summary>
    public static int Slots => 3 + (ResearchState.IsResearched("h_greed") ? 1 : 0);

    /// <summary>並んでいる品（-1＝売り切れ）。</summary>
    private static int[] stock;
    /// <summary>
    /// 💸 その枠の値引き率（%・0＝定価）。
    /// ⚠⚠ **その回の品揃えと一緒に消える。**引き直せば無くなるので、値引きは必ず一時的になる
    ///   ―― 恒久的に安い品が生まれると、「いま買うべきか」の判断が「安くなるまで待つ」に化ける。
    /// ⚠ 上限は `MaxDiscount`（3割）。半額を許すと定価の方が例外になる。
    /// </summary>
    private static int[] discount;
    /// <summary>
    /// 🔁 前の回に並んでいた品。⚠ **連続して並べないため**に覚えておく。
    ///   逃した品はいつか戻ってくる（再入荷）が、**次の回にそのまま居座ることはない**
    ///   ―― 居座ると「限定」が嘘になるし、毎ターン同じ棚を見ることになる。
    /// </summary>
    private static int[] prevStock;
    private static int stockedTurn = -1;

    /// <summary>💸 値引きの上限（%）。⚠ ユーザー指定：最大でも3割。</summary>
    public const int MaxDiscount = 30;
    /// <summary>💸 1回の品揃えで値引きが起きる確率。⚠ 毎回付けない（付くと「安い日」が消える）。</summary>
    private const float DiscountChance = 0.35f;

    private static void EnsureInit()
    {
        // ⚠ 棚が増えたら配列を伸ばす（中身は引き継ぐ）。伸ばさないと添字外で落ちる。
        if (stock == null || stock.Length != Slots) stock = Grow(stock, -1);
        if (discount == null || discount.Length != Slots) discount = Grow(discount, 0);
    }
    private static int[] Grow(int[] old, int fill)
    {
        var a = new int[Slots];
        for (int i = 0; i < a.Length; i++) a[i] = (old != null && i < old.Length) ? old[i] : fill;
        return a;
    }

    public static void Reset() { stock = null; discount = null; prevStock = null; stockedTurn = -1; EnsureInit(); }

    public static int SlotItem(int i)
    {
        EnsureInit();
        return (i < 0 || i >= Slots) ? -1 : stock[i];
    }
    public static bool SoldOut(int i) => SlotItem(i) < 0;

    /// <summary>💸 その枠の値引き率（%）。0なら定価。</summary>
    public static int DiscountOf(int i)
    {
        EnsureInit();
        return (i < 0 || i >= Slots || stock[i] < 0) ? 0 : discount[i];
    }
    /// <summary>定価（値引き前）。</summary>
    public static int ListPriceOf(int i)
    {
        int item = SlotItem(i);
        return item < 0 ? 0 : AccessoryCatalog.Get(item).price;
    }
    /// <summary>
    /// 💸 実際に払う額。⚠ **支払いも表示もここだけ**を通す（2か所で計算すると必ずずれる）。
    /// </summary>
    public static int PriceOf(int i)
    {
        int lp = ListPriceOf(i);
        if (lp <= 0) return 0;
        int d = DiscountOf(i);
        return d <= 0 ? lp : Mathf.Max(1, Mathf.RoundToInt(lp * (100 - d) / 100f));
    }

    /// <summary>ターンの頭に呼ぶ。⚠ 同じターンに2度呼んでも引き直さない。</summary>
    public static void OnTurnStart(int turn)
    {
        EnsureInit();
        if (stockedTurn == turn) return;
        stockedTurn = turn;

        // 🔁 引き直す。⚠ **前回の品と、この回で既に並べた品は除く**
        //   （同じ物が2枠に出る／次の回も居座る、のどちらも「限定」を嘘にする）。
        var taken = new List<int>();
        for (int i = 0; i < Slots; i++) { stock[i] = RollItem(taken); discount[i] = 0; if (stock[i] >= 0) taken.Add(stock[i]); }

        // 💸 値引きは**多くても1枠**。毎回付けると「安い日」という出来事が消える。
        if (Random.value < DiscountChance)
        {
            var live = new List<int>();
            for (int i = 0; i < Slots; i++) if (stock[i] >= 0) live.Add(i);
            if (live.Count > 0)
            {
                int at = live[Random.Range(0, live.Count)];
                float r = Random.value;                      // 小さい値引きほど出やすい
                discount[at] = r < 0.55f ? 10 : r < 0.85f ? 20 : MaxDiscount;
            }
        }

        // 次の回のために、いま並べた品を覚える
        prevStock = new int[Slots];
        for (int i = 0; i < Slots; i++) prevStock[i] = stock[i];

        var sb = new System.Text.StringBuilder();
        int sale = -1;
        for (int i = 0; i < Slots; i++)
        {
            if (i > 0) sb.Append("／");
            sb.Append(Name(stock[i]));
            if (discount[i] > 0) { sb.Append("<color=#5cc47c>-").Append(discount[i]).Append("%</color>"); sale = i; }
        }
        Debug.Log("🛒『行商人』" + sb.ToString().Replace("<color=#5cc47c>", "").Replace("</color>", "") + " を並べた");
        NotifySystem.Push("<b>行商人</b>が来ている ― " + sb,
            sale >= 0 ? NotifySystem.Kind.Gain : NotifySystem.Kind.Info);
    }
    private static string Name(int i) => i < 0 ? "―" : AccessoryCatalog.Name(i);

    /// <summary>希少度で重みを変えて1つ引く。⚠ `exclude` と**前回の品**は避ける。</summary>
    private static int RollItem(List<int> exclude)
    {
        int r = Random.Range(0, 100);
        int wantRarity = r < 60 ? 0 : r < 90 ? 1 : 2;
        // ⚠ K-3：**研究で解禁した物しか並ばない**。研究の見返りが行商人の品揃えに出る。
        var pool = Pool(wantRarity, exclude, true);
        // その希少度に無ければ希少度を外す → それでも無ければ「前回避け」も外す（棚を空にしない）
        if (pool.Count == 0) pool = Pool(-1, exclude, true);
        if (pool.Count == 0) pool = Pool(-1, exclude, false);
        if (pool.Count == 0) return -1;
        return pool[Random.Range(0, pool.Count)];
    }

    private static List<int> Pool(int rarity, List<int> exclude, bool avoidPrev)
    {
        var pool = new List<int>();
        for (int i = 0; i < AccessoryCatalog.Count; i++)
        {
            if (!AccessoryCatalog.IsUnlocked(i)) continue;
            if (rarity >= 0 && AccessoryCatalog.Get(i).rarity != rarity) continue;
            if (exclude != null && exclude.Contains(i)) continue;
            if (avoidPrev && WasOnShelf(i)) continue;
            pool.Add(i);
        }
        return pool;
    }

    /// <summary>🔁 前の回に並んでいたか（買われて -1 になった枠は数えない）。</summary>
    private static bool WasOnShelf(int item)
    {
        if (prevStock == null) return false;
        for (int i = 0; i < prevStock.Length; i++) if (prevStock[i] == item) return true;
        return false;
    }

    public static bool CanBuy(int slot, out string why)
    {
        why = "";
        int item = SlotItem(slot);
        if (item < 0) { why = "売り切れ"; return false; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { why = "戦闘中は買えない"; return false; }
        int p = PriceOf(slot);
        var res = DungeonResourceManager.Instance;
        if (res != null && res.DungeonPoints < p) { why = "DPが足りない（要" + p + "）"; return false; }
        return true;
    }

    /// <summary>買う。買った装飾品は**手持ち**に入る（誰に着けるかは図鑑で決める）。</summary>
    public static bool TryBuy(int slot)
    {
        string why;
        if (!CanBuy(slot, out why)) { Debug.LogWarning("⚠️ " + why); return false; }
        int item = stock[slot];
        int p = PriceOf(slot);
        int d = DiscountOf(slot);
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(p)) return false;
        stock[slot] = -1; discount[slot] = 0;   // ⚠ 売り切れにする（同じ枠から何度も買えない）
        AccessoryInventory.Add(item);
        Debug.Log($"🛒『購入』{AccessoryCatalog.Name(item)}（-{p}DP{(d > 0 ? " ／ " + d + "%引き" : "")}）");
        NotifySystem.Push($"<b>{AccessoryCatalog.Name(item)}</b> を買った"
            + (d > 0 ? " <color=#5cc47c>（" + d + "%引き）</color>" : ""), NotifySystem.Kind.Gain);
        return true;
    }
}

/// <summary>
/// 💍 装飾品の手持ち。**種類ごとの個数**で持つ（同じ物を複数持てる）。
/// ⚠ 個体に着けたぶんは手持ちから減る。減らさないと1つの指輪を全員に着けられてしまう。
/// </summary>
public static class AccessoryInventory
{
    private static Dictionary<int, int> owned;   // 種類index → 個数
    private static void EnsureInit() { if (owned == null) owned = new Dictionary<int, int>(); }
    public static void Reset() { owned = new Dictionary<int, int>(); }

    public static int CountOf(int item)
    {
        EnsureInit(); int n; return owned.TryGetValue(item, out n) ? n : 0;
    }
    public static void Add(int item, int n = 1)
    {
        EnsureInit();
        if (item < 0) return;
        owned[item] = CountOf(item) + n;
    }
    public static bool Take(int item)
    {
        EnsureInit();
        if (CountOf(item) <= 0) return false;
        owned[item] = CountOf(item) - 1;
        return true;
    }
    /// <summary>手持ちにある種類の一覧（個数1以上）。</summary>
    public static List<int> Items()
    {
        EnsureInit();
        var l = new List<int>();
        for (int i = 0; i < AccessoryCatalog.Count; i++) if (CountOf(i) > 0) l.Add(i);
        return l;
    }
    public static int TotalCount
    {
        get { EnsureInit(); int n = 0; foreach (var kv in owned) n += kv.Value; return n; }
    }

    /// <summary>
    /// 個体に着ける（手持ちから1つ減る）。既に着けていたものは手持ちへ戻る。
    /// ⚠ 「着け替え」で消えると、試して戻すことができなくなる。
    /// </summary>
    public static bool Equip(int individualId, int item)
    {
        var v = MinionRoster.Get(individualId);
        if (v == null) return false;
        if (item >= 0 && !Take(item)) { Debug.LogWarning("⚠️ その装飾品を持っていません。"); return false; }
        if (v.accessory >= 0) Add(v.accessory);        // 外したぶんは手持ちへ戻す
        MinionRoster.SetAccessory(individualId, item);
        Debug.Log($"💍『装着』個体#{individualId} に {AccessoryCatalog.Name(item)}");
        return true;
    }
    public static bool Unequip(int individualId) => Equip(individualId, -1);
}
