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
    public const int Slots = 3;

    /// <summary>並んでいる品（-1＝売り切れ）。</summary>
    private static int[] stock;
    private static int stockedTurn = -1;

    private static void EnsureInit()
    {
        if (stock == null) stock = new int[Slots] { -1, -1, -1 };
    }

    public static void Reset() { stock = null; stockedTurn = -1; EnsureInit(); }

    public static int SlotItem(int i)
    {
        EnsureInit();
        return (i < 0 || i >= Slots) ? -1 : stock[i];
    }
    public static bool SoldOut(int i) => SlotItem(i) < 0;

    /// <summary>ターンの頭に呼ぶ。⚠ 同じターンに2度呼んでも引き直さない。</summary>
    public static void OnTurnStart(int turn)
    {
        EnsureInit();
        if (stockedTurn == turn) return;
        stockedTurn = turn;
        for (int i = 0; i < Slots; i++) stock[i] = RollItem();
        Debug.Log($"🛒『行商人』{Name(stock[0])}／{Name(stock[1])}／{Name(stock[2])} を並べた");
        NotifySystem.Push("<b>行商人</b>が来ている ― " + Name(stock[0]) + "／" + Name(stock[1]) + "／" + Name(stock[2]),
            NotifySystem.Kind.Info);
    }
    private static string Name(int i) => i < 0 ? "―" : AccessoryCatalog.Name(i);

    /// <summary>希少度で重みを変えて1つ引く。</summary>
    private static int RollItem()
    {
        int r = Random.Range(0, 100);
        int wantRarity = r < 60 ? 0 : r < 90 ? 1 : 2;
        var pool = new List<int>();
        for (int i = 0; i < AccessoryCatalog.Count; i++)
            if (AccessoryCatalog.Get(i).rarity == wantRarity) pool.Add(i);
        if (pool.Count == 0) return Random.Range(0, AccessoryCatalog.Count);
        return pool[Random.Range(0, pool.Count)];
    }

    public static bool CanBuy(int slot, out string why)
    {
        why = "";
        int item = SlotItem(slot);
        if (item < 0) { why = "売り切れ"; return false; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { why = "戦闘中は買えない"; return false; }
        int p = AccessoryCatalog.Get(item).price;
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
        int p = AccessoryCatalog.Get(item).price;
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(p)) return false;
        stock[slot] = -1;                       // ⚠ 売り切れにする（同じ枠から何度も買えない）
        AccessoryInventory.Add(item);
        Debug.Log($"🛒『購入』{AccessoryCatalog.Name(item)}（-{p}DP）");
        NotifySystem.Push($"<b>{AccessoryCatalog.Name(item)}</b> を買った", NotifySystem.Kind.Gain);
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
