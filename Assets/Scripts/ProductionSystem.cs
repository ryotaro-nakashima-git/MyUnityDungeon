using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🔨 **生産力と待ち行列**（K-1）＝ Civ の「生産」をこの作品に入れる層。
///
/// <para>
/// **なぜ要るか**（実測）。この作品は建造物も配下も**すべて魔力点(DP)で即時購入**だった。
/// その結果：
///   ① DPの使い道が**配置枠の数**でしか制限されず、1周で **7,284 余る**
///   ② 「何を先に作るか」という**順番の判断が一度も発生しない**
///   ③ 拠点・人口・施設が育つ**時間の実感**が無い
/// Civ VII は建造物もユニットも**ターンをかけて作り**、金は「**順番を追い越して買う**」ための
/// 別口でしかない（⚠ 実画面で確認：**購入タブで買えるのは物だけで、科学力は絶対に買えない**）。
/// → [[civ7-actual-screens]]
/// </para>
///
/// <para>
/// ⚠⚠ **既にあった物を捨てない。** `LegionRoster` には
/// `ProductionAt`（3＋人口×2）と1件だけの `Build` が既にあった。生産力の式はそのまま使い、
/// **1件しか持てなかったところを待ち行列にし、施設も同じ列に載せる**のがこの層の仕事。
/// </para>
///
/// <para>
/// ⚠ **軍団の着工が DP を取ったうえにターンもかけていた**（二重取り）。Civ はどちらか一方なので、
/// 着工は無料にして、DPは**購入**にだけ効かせる。
/// ⚠ **購入は1ターンに1件まで。** レートだけで縛ると、DPが余る後半に全部買えてしまい
/// 「順番を選ぶ」という遊びが消える（→ [[research-wiring-gap]] の「両替機を作らない」）。
/// </para>
///
/// 純static・実行時保持（`SaveSystem.StaticTypes` に登録済み）。
/// 関連: [[LegionRoster]] [[DistrictCatalog]] [[SurfaceMap]] [[SettlementSystem]]。
/// </summary>
public static class ProductionSystem
{
    public enum Kind { District = 0, Legion = 1 }

    [System.Serializable]
    public class Item
    {
        public Kind kind;
        public int index;       // District＝DistrictCatalog の index ／ Legion＝MinionCatalog の index
        public int regionId;
        public int progress;    // 積んだ生産力
    }

    /// <summary>
    /// 💰 生産力1点あたりの魔力点。
    /// ⚠ 施設の旧DP価格（380〜720）を**据え置く**ように決めてある
    ///   （例：交易所 380DP → 生産48点 → 買えば 384DP）。値を触ると地上の物価が丸ごと動く。
    /// </summary>
    public const int DpPerProduction = 8;

    /// <summary>⏳ 1ターンに購入できる件数。⚠ **ここを増やすと余ったDPで全部買えてしまう。**</summary>
    public const int PurchasesPerTurn = 1;

    private static List<Item> queue;
    private static int purchasedThisTurn;
    private static bool migrated;

    private static void EnsureInit() { if (queue == null) queue = new List<Item>(); }
    public static void Reset() { queue = new List<Item>(); purchasedThisTurn = 0; migrated = false; }
    public static IReadOnlyList<Item> All { get { EnsureInit(); return queue; } }
    public static int PurchasesLeft => Mathf.Max(0, PurchasesPerTurn - purchasedThisTurn);

    // ============ 生産力 ============

    /// <summary>その拠点が1ターンに積む生産力。⚠ 式は `LegionRoster` に一本化してある（2箇所に書かない）。</summary>
    public static int ProductionAt(int regionId) => LegionRoster.ProductionAt(regionId);

    /// <summary>🔨 全拠点の生産力の合計（上部バーに出す）。</summary>
    public static int TotalProduction
    {
        get
        {
            int p = 0;
            foreach (var r in SurfaceMap.All) if (r.owned && r.settle != SurfaceMap.Settle.None) p += ProductionAt(r.id);
            return p;
        }
    }

    // ============ 品目 ============

    /// <summary>完成に要る生産力。</summary>
    public static int CostOf(Kind kind, int index)
    {
        if (kind == Kind.Legion) return LegionRoster.BuildCostOf(index);
        // 🏛️ 施設：旧DP価格を `DpPerProduction` で割って生産力に直す（物価を変えないため）
        return Mathf.Max(10, Mathf.RoundToInt(DistrictCatalog.Cost(index) / (float)DpPerProduction));
    }

    public static string NameOf(Item it)
        => it.kind == Kind.Legion ? MinionCatalog.Get(it.index).jpName + "軍団" : DistrictCatalog.Get(it.index).jpName;

    public static int Remaining(Item it) => Mathf.Max(0, CostOf(it.kind, it.index) - it.progress);

    /// <summary>あと何ターンで完成するか。⚠ 生産力0の拠点では -1（＝いつまでも終わらない）を返す。</summary>
    public static int TurnsLeft(Item it)
    {
        int p = ProductionAt(it.regionId);
        if (p <= 0) return -1;
        return Mathf.CeilToInt(Remaining(it) / (float)p);
    }

    /// <summary>💰 いま買い切るのに要る魔力点。⚠ **残りぶんだけ**（積んだ生産力は無駄にならない）。</summary>
    public static int PurchaseCost(Item it) => Remaining(it) * DpPerProduction;

    // ============ 待ち行列 ============

    /// <summary>その拠点の列（先頭が建造中）。</summary>
    public static List<Item> QueueAt(int regionId)
    {
        EnsureInit();
        var l = new List<Item>();
        foreach (var it in queue) if (it.regionId == regionId) l.Add(it);
        return l;
    }

    public static Item BuildingAt(int regionId)
    {
        EnsureInit();
        foreach (var it in queue) if (it.regionId == regionId) return it;
        return null;
    }

    public static int CountAt(int regionId)
    {
        EnsureInit(); int n = 0;
        foreach (var it in queue) if (it.regionId == regionId) n++;
        return n;
    }

    public static bool CanEnqueue(int regionId, Kind kind, int index, out string why)
    {
        why = "";
        EnsureInit();
        var r = SurfaceMap.Get(regionId);
        if (r == null || !r.owned || r.settle == SurfaceMap.Settle.None) { why = "拠点でないと生産できない"; return false; }
        if (ProductionAt(regionId) <= 0) { why = "この拠点は生産力が0（人口を増やすこと）"; return false; }
        if (CountAt(regionId) >= 6) { why = "この拠点の列がいっぱい（6件まで）"; return false; }

        if (kind == Kind.Legion)
        {
            if (!MinionEvolution.IsUnlocked(index)) { why = "その種はまだ解禁されていない"; return false; }
            // ⚠ 上限は「盤にいる数＋作っている数」で見る（作り置きで上限を越えられないように）
            if (LegionRoster.Count + LegionQueued >= LegionRoster.Cap)
            { why = "軍団の上限（" + LegionRoster.Cap + "）に届いている。拠点を増やすこと"; return false; }
            return true;
        }

        if (!DistrictCatalog.IsUnlocked(index))
        { why = DistrictCatalog.Get(index).jpName + " はまだ建てられない（" + DistrictCatalog.LockReason(index) + "）"; return false; }
        bool asQuarter;
        if (!DistrictCatalog.CanBuild(regionId, out asQuarter, out why)) return false;
        if (DistrictCatalog.Get(index).id == "harbor" && !DistrictCatalog.IsCoastal(regionId))
        { why = "港は海に面したタイルにしか建てられない"; return false; }
        // ⚠ 同じ施設を列に2つ積ませない（建ててみたら1つしか置けなかった、が起きる）
        foreach (var it in queue)
            if (it.regionId == regionId && it.kind == Kind.District && it.index == index)
            { why = "同じ施設が既に列に入っている"; return false; }
        return true;
    }

    private static int LegionQueued
    {
        get { EnsureInit(); int n = 0; foreach (var it in queue) if (it.kind == Kind.Legion) n++; return n; }
    }

    public static bool TryEnqueue(int regionId, Kind kind, int index)
    {
        string why;
        if (!CanEnqueue(regionId, kind, index, out why)) { Debug.LogWarning("⚠️ " + why); return false; }
        var it = new Item { kind = kind, index = index, regionId = regionId, progress = 0 };
        queue.Add(it);
        int t = TurnsLeft(it);
        Debug.Log($"🔨『生産に積んだ』{SurfaceMap.Get(regionId).name} ─ {NameOf(it)}"
            + $"（{CostOf(kind, index)} 生産力・約{(t < 0 ? "？" : t.ToString())}ターン／列 {CountAt(regionId)} 件目）");
        return true;
    }

    public static bool Cancel(Item it)
    {
        EnsureInit();
        if (it == null || !queue.Contains(it)) return false;
        queue.Remove(it);
        Debug.Log("🛑『取りやめ』" + NameOf(it) + " の生産を止めた（積んだ生産力は戻らない）");
        return true;
    }

    /// <summary>⬆️ 同じ拠点の列の中で1つ前へ。⚠ 先頭に割り込むと、積んだ進捗の持ち主が変わって見えるので順序だけ入れ替える。</summary>
    public static bool MoveUp(Item it)
    {
        EnsureInit();
        var l = QueueAt(it.regionId);
        int i = l.IndexOf(it);
        if (i <= 0) return false;
        int a = queue.IndexOf(l[i - 1]), b = queue.IndexOf(it);
        var tmp = queue[a]; queue[a] = queue[b]; queue[b] = tmp;
        return true;
    }

    public static bool MoveDown(Item it)
    {
        EnsureInit();
        var l = QueueAt(it.regionId);
        int i = l.IndexOf(it);
        if (i < 0 || i >= l.Count - 1) return false;
        int a = queue.IndexOf(l[i + 1]), b = queue.IndexOf(it);
        var tmp = queue[a]; queue[a] = queue[b]; queue[b] = tmp;
        return true;
    }

    // ============ 購入（Civ のゴールド購入） ============

    public static bool CanPurchase(Item it, out string why)
    {
        why = "";
        if (it == null) { why = "対象がない"; return false; }
        if (PurchasesLeft <= 0) { why = "このターンはもう買えない（1ターン1件）"; return false; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { why = "購入は準備フェーズだけ"; return false; }
        var res = DungeonResourceManager.Instance;
        int c = PurchaseCost(it);
        if (res != null && res.DungeonPoints < c) { why = "魔力点が足りない（要" + c + "）"; return false; }
        return true;
    }

    /// <summary>💰 残りを魔力点で埋めて即座に完成させる。⚠ **買えるのは物だけ。** 研究点も時間も買えない。</summary>
    public static bool TryPurchase(Item it)
    {
        string why;
        if (!CanPurchase(it, out why)) { Debug.LogWarning("⚠️ " + why); return false; }
        int c = PurchaseCost(it);
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(c)) return false;
        it.progress = CostOf(it.kind, it.index);
        purchasedThisTurn++;
        Debug.Log($"💰『購入』{NameOf(it)} を {c}DP で買った（順番を追い越した）");
        NotifySystem.Push($"<b>{NameOf(it)}</b> を購入（-{c}DP）", NotifySystem.Kind.Gain, it.regionId);
        TryComplete(it);
        return true;
    }

    // ============ 毎ターンの進行 ============

    /// <summary>ターンの解決から呼ぶ。⚠ 拠点ごとに**先頭の1件だけ**が伸びる（Civと同じ）。</summary>
    public static void Tick()
    {
        EnsureInit();
        MigrateLegacyBuilds();
        purchasedThisTurn = 0;

        var done = new List<Item>();
        var seen = new HashSet<int>();
        for (int i = 0; i < queue.Count; i++)
        {
            var it = queue[i];
            var r = SurfaceMap.Get(it.regionId);
            // 🏳️ 拠点を失ったら生産も消える（奪われた土地では造れない）
            if (r == null || !r.owned || r.settle == SurfaceMap.Settle.None)
            { done.Add(it); continue; }
            if (!seen.Add(it.regionId)) continue;   // その拠点の2件目以降は待ち
            it.progress += ProductionAt(it.regionId);
        }
        for (int i = 0; i < done.Count; i++)
        {
            Debug.Log("🛑『生産中止』拠点を失ったため " + NameOf(done[i]) + " の生産が止まった");
            queue.Remove(done[i]);
        }

        // 完成の判定。⚠ 逆順に見る（完成で列から抜けるため）
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            var it = queue[i];
            if (it.progress < CostOf(it.kind, it.index)) continue;
            TryComplete(it);
        }
    }

    /// <summary>
    /// 完成させて列から抜く。⚠ **置けなかったら列に残す**（進捗はそのまま）。
    /// 軍団は盤が埋まっていることがあり、施設は街区の条件が変わっていることがある。
    /// </summary>
    private static void TryComplete(Item it)
    {
        EnsureInit();
        bool ok;
        if (it.kind == Kind.Legion) ok = LegionRoster.SpawnBuilt(it.regionId, it.index);
        else ok = DistrictCatalog.PlaceBuilt(it.regionId, it.index);
        if (!ok) return;
        queue.Remove(it);
    }

    /// <summary>
    /// 🔁 旧 `LegionRoster.builds`（1拠点1件だった頃の生産）をこの列へ移す。
    /// ⚠ 古いセーブから読み込んだときに、作りかけの軍団が消えないようにするため。
    /// </summary>
    private static void MigrateLegacyBuilds()
    {
        if (migrated) return;
        migrated = true;
        var old = LegionRoster.TakeLegacyBuilds();
        if (old == null || old.Count == 0) return;
        for (int i = 0; i < old.Count; i++)
            queue.Add(new Item { kind = Kind.Legion, index = old[i].catalogIndex, regionId = old[i].regionId, progress = old[i].progress });
        Debug.Log("🔁『生産の引き継ぎ』作りかけの軍団 " + old.Count + " 件を待ち行列へ移した");
    }
}
