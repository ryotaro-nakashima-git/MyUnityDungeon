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
    /// <summary>⚠ **末尾に足すこと。** セーブに index が載る。</summary>
    public enum Kind { District = 0, Legion = 1, Work = 2, Project = 3 }

    /// <summary>
    /// 🏗️ **大工事**＝生産力を**迷宮**に注ぐ品目（この作品ならではの分類）。
    ///
    /// ⚠ Civ には無い。**地上の生産が迷宮に効く唯一の道**として新設した。
    ///   これが無いと、生産力は地上だけの話に閉じてしまい、
    ///   「迷宮で遊んでいる人」にとって生産力が他人事のままになる。
    /// ⚠ 巨大施設は**盤のどこに置くかをプレイヤーが選ぶ**ので、完成すると**建造許可**が1枚入る形にした
    ///   （Civ VII の「完了すると祭壇を無償で2回購入できる」と同じ形）。
    /// </summary>
    public static class Works
    {
        public const int NewFloor = 0;      // 新しい階層
        public const int DrillGround = 1;   // 練兵場の建造許可
        public const int GreatNest = 2;     // 大巣の建造許可
        public const int Count = 3;

        public static string Name(int i)
        {
            if (i == NewFloor) return "縦坑の掘削（新しい階層）";
            if (i == DrillGround) return "練兵場の建造許可";
            return "大巣の建造許可";
        }
        public static string Desc(int i)
        {
            if (i == NewFloor) return "迷宮を1層深くする。冒険者の道のりが伸び、配置枠が増える。";
            if (i == DrillGround) return "完成すると練兵場（5×5）を<b>1つ無償で置ける</b>。この階の隊枠 +1。";
            return "完成すると大巣（5×5）を<b>1つ無償で置ける</b>。波あたり4体・射程3。";
        }
        /// <summary>生産力。⚠ 元のDP価格を `DpPerProduction` で割って据え置く。</summary>
        /// <summary>
        /// 🔬 解禁研究（空＝最初から）。
        /// ⚠⚠ **巨大施設に刻印のゲートを付けてはいけない。** 一度そうしたが、刻印は終焉の時代の物で、
        ///   決着が T20 の周では**永久に建てられなくなる**（今まで最初から建てられた物を奪うことになる）。
        ///   既にある入手経路は塞がず、刻印は**新しい物を足す**方向にだけ使う。
        /// </summary>
        public static string Research(int i) => "";

        public static bool IsUnlocked(int i)
        {
            string r = Research(i);
            return string.IsNullOrEmpty(r) || ResearchState.IsResearched(r);
        }

        public static int Cost(int i)
        {
            if (i == NewFloor)
            {
                var fm = DungeonFloorManager.Instance;
                int dp = fm != null ? fm.AddFloorDPCost() : 800;
                return Mathf.Max(20, Mathf.RoundToInt(dp / (float)DpPerProduction));
            }
            int kind = i == DrillGround ? (int)GreatWorkCatalog.Kind.DrillGround : (int)GreatWorkCatalog.Kind.GreatNest;
            return Mathf.Max(20, Mathf.RoundToInt(GreatWorkCatalog.Get(kind).dpCost / (float)DpPerProduction));
        }
    }

    /// <summary>
    /// 🎯 **プロジェクト**＝生産力を「物」でも「建物」でもない**一度きりの見返り**に変える品目。
    ///
    /// Civ VII の科学プロジェクト（完了でイノベーションを得る）の輸入。
    /// ⚠ **産出への両替にはしない**（→ [[research-wiring-gap]] の撤回）。
    ///   渡すのは **頭数** と **状態**（祝祭）＝どちらも「物」であって通貨ではない。
    /// </summary>
    public static class Projects
    {
        public const int Levy = 0;        // 徴募：配下を1体、無償で
        public const int Festival = 1;    // 祝祭の準備：その拠点で祝祭を起こす
        public const int Feast = 2;       // 喰らいの宴（暴食）：捕虜1人 → 配下1体
        public const int Usurp = 3;       // 簒奪（嫉妬）：属性ポイント +1
        public const int Throne = 4;      // 玉座の顕現（傲慢）：魔王に BP
        public const int Pyre = 5;        // 焚刑（憤怒）：世界の装備水準を下げる
        public const int Count = 6;

        public static string Name(int i)
        {
            if (i == Levy) return "徴募";
            if (i == Festival) return "祝祭の準備";
            if (i == Feast) return "喰らいの宴";
            if (i == Usurp) return "簒奪";
            if (i == Throne) return "玉座の顕現";
            return "焚刑";
        }
        public static string Desc(int i)
        {
            if (i == Levy) return "配下を<b>1体、無償で召喚</b>する（DPを使わない）。頭数はそのまま捌ける数になる。";
            if (i == Festival) return "この拠点で<b>祝祭</b>を起こす（" + SettlementSystem.CelebrateSpan + "ターン・産出が伸び、政策の自由枠が1つ開く）。";
            if (i == Feast) return "牢の捕虜を1人<b>喰らい</b>、配下を1体無償で得る。捕らえた者が頭数に変わる。";
            if (i == Usurp) return "他者の力を写し取る。完成すると<b>属性ポイントが1つ</b>入る。";
            if (i == Throne) return "玉座を顕す。完成すると魔王に <b>BP</b> が入る。";
            return "奪われた装備を焼き払う。完成すると<b>世界の装備水準が下がる</b> ―― 来る冒険者の武具が弱くなる。";
        }
        /// <summary>🔬 解禁研究（空＝最初から）。⚠ 大罪の刻印はここで「作れる物」に変わる。</summary>
        public static string Research(int i)
            => i == Feast ? "h_glut" : i == Usurp ? "h_envy" : i == Throne ? "h_pride"
             : i == Pyre ? "h_wrath" : "";
        public static bool IsUnlocked(int i)
        {
            string r = Research(i);
            return string.IsNullOrEmpty(r) || ResearchState.IsResearched(r);
        }
        /// <summary>⚠ 徴募は「頭数を増やす唯一の生産経路」なので安すぎないこと（DP召喚と釣り合わせる）。</summary>
        public static int Cost(int i)
        {
            if (i == Levy) return 40;
            if (i == Festival) return 30;
            if (i == Feast) return 45;
            if (i == Usurp) return 60;
            if (i == Throne) return 55;
            return 50;
        }
    }

    // ============ 🎟️ 建造許可（大工事の完成でもらう） ============
    private static int[] gwVouchers;
    private static void EnsureVouchers() { if (gwVouchers == null) gwVouchers = new int[GreatWorkCatalog.Count]; }
    public static int GreatWorkVouchers(int kind)
    { EnsureVouchers(); return gwVouchers[Mathf.Clamp(kind, 0, gwVouchers.Length - 1)]; }
    public static int TotalVouchers
    { get { EnsureVouchers(); int n = 0; for (int i = 0; i < gwVouchers.Length; i++) n += gwVouchers[i]; return n; } }
    /// <summary>🎟️ 1枚使う。⚠ 無ければ false（呼んだ側がDPで払う）。</summary>
    public static bool TryUseGreatWorkVoucher(int kind)
    {
        EnsureVouchers();
        int k = Mathf.Clamp(kind, 0, gwVouchers.Length - 1);
        if (gwVouchers[k] <= 0) return false;
        gwVouchers[k]--;
        return true;
    }

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
    public static void Reset()
    { queue = new List<Item>(); purchasedThisTurn = 0; migrated = false; gwVouchers = new int[GreatWorkCatalog.Count]; }
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
        if (kind == Kind.Work) return Works.Cost(index);
        if (kind == Kind.Project) return Projects.Cost(index);
        // 🏛️ 施設：旧DP価格を `DpPerProduction` で割って生産力に直す（物価を変えないため）
        return Mathf.Max(10, Mathf.RoundToInt(DistrictCatalog.Cost(index) / (float)DpPerProduction));
    }

    public static string NameOf(Item it)
    {
        if (it.kind == Kind.Legion) return MinionCatalog.Get(it.index).jpName + "軍団";
        if (it.kind == Kind.Work) return Works.Name(it.index);
        if (it.kind == Kind.Project) return Projects.Name(it.index);
        return DistrictCatalog.Get(it.index).jpName;
    }

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

        if (kind == Kind.Work)
        {
            if (!Works.IsUnlocked(index))
            { why = "まだ作れない（研究が要る）"; return false; }
            var fm = DungeonFloorManager.Instance;
            if (index == Works.NewFloor)
            {
                if (fm == null) { why = "迷宮がまだ無い"; return false; }
                if (fm.BuiltFloorCount >= DungeonFloorManager.MaxFloors) { why = "階層は最大 " + DungeonFloorManager.MaxFloors + " 層（領域研究で伸びる）"; return false; }
                string need = fm.AddFloorResearchNeeded();
                if (!string.IsNullOrEmpty(need) && !ResearchState.IsResearched(need))
                { why = "第" + (fm.BuiltFloorCount + 1) + "層には領域研究『" + need + "』が要る"; return false; }
            }
            // ⚠ 許可の積みすぎを止める（置ける場所が無いのに券だけ増える事故）
            if (index != Works.NewFloor)
            {
                int k = index == Works.DrillGround ? (int)GreatWorkCatalog.Kind.DrillGround : (int)GreatWorkCatalog.Kind.GreatNest;
                if (GreatWorkVouchers(k) >= 2) { why = "建造許可がもう2枚ある。先に置くこと"; return false; }
            }
            foreach (var it0 in queue)
                if (it0.regionId == regionId && it0.kind == Kind.Work && it0.index == index)
                { why = "同じ大工事が既に列に入っている"; return false; }
            return true;
        }

        if (kind == Kind.Project)
        {
            if (!Projects.IsUnlocked(index))
            { why = "まだ作れない（研究が要る）"; return false; }
            if (index == Projects.Feast && Prison.Count <= 0)
            { why = "牢に捕虜がいない"; return false; }
            if (index == Projects.Levy)
            {
                if (MinionRoster.PickSummonableIndex() < 0) { why = "呼べる種がまだ無い"; return false; }
            }
            else
            {
                var rg = SurfaceMap.Get(regionId);
                if (rg != null && rg.celebrateTurns > 0) { why = "この拠点はいま祝祭のさなか"; return false; }
            }
            foreach (var it0 in queue)
                if (it0.regionId == regionId && it0.kind == Kind.Project && it0.index == index)
                { why = "同じプロジェクトが既に列に入っている"; return false; }
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
        else if (it.kind == Kind.Work) ok = CompleteWork(it.index);
        else if (it.kind == Kind.Project) ok = CompleteProject(it.index, it.regionId);
        else ok = DistrictCatalog.PlaceBuilt(it.regionId, it.index);
        if (!ok) return;
        queue.Remove(it);
    }

    /// <summary>🏗️ 大工事の完成。⚠ 準備フェーズでないと階層を足せないので、そのときは持ち越す。</summary>
    private static bool CompleteWork(int index)
    {
        if (index == Works.NewFloor)
        {
            var fm = DungeonFloorManager.Instance;
            if (fm == null) return false;
            var turn = DungeonTurnManager.Instance;
            if (turn != null && !turn.IsPreparePhase) return false;   // 戦闘中は持ち越す
            if (!fm.TryAddFloor(true)) return false;
            NotifySystem.Push("<b>縦坑が貫通した</b>　迷宮が1層深くなった", NotifySystem.Kind.Story);
            return true;
        }
        EnsureVouchers();
        int k = index == Works.DrillGround ? (int)GreatWorkCatalog.Kind.DrillGround : (int)GreatWorkCatalog.Kind.GreatNest;
        gwVouchers[k]++;
        Debug.Log("🎟️『建造許可』" + GreatWorkCatalog.Name(k) + " を無償で1つ置けるようになった（所持 " + gwVouchers[k] + "）");
        NotifySystem.Push("<b>" + GreatWorkCatalog.Name(k) + "の建造許可</b>　迷宮の『巨大』から無償で置ける",
            NotifySystem.Kind.Gain);
        return true;
    }

    /// <summary>🎯 プロジェクトの完成。⚠ 渡すのは**物と状態**だけ（産出への両替はしない）。</summary>
    private static bool CompleteProject(int index, int regionId)
    {
        if (index == Projects.Levy)
        {
            int ci = MinionRoster.PickSummonableIndex();
            if (ci < 0) return false;
            var ind = MinionRoster.TrySummonFree(ci);
            if (ind == null) return false;
            Debug.Log("🎺『徴募』" + MinionCatalog.Get(ci).jpName + " が1体、無償で加わった");
            NotifySystem.Push("<b>徴募</b>　" + MinionCatalog.Get(ci).jpName + " が加わった", NotifySystem.Kind.Gain, regionId);
            return true;
        }
        if (index == Projects.Feast)
        {
            // 🍖 暴食：牢の捕虜を1人喰らって配下に変える。⚠ 捕虜がいなければ完成を持ち越す。
            if (Prison.Count <= 0) return false;
            int ci = MinionRoster.PickSummonableIndex();
            if (ci < 0) return false;
            string why;
            var cap = Prison.All[0];
            if (!Prison.TryDevour(cap.id, out why)) { Debug.LogWarning("⚠️ 喰らいの宴：" + why); return false; }
            var ind = MinionRoster.TrySummonFree(ci);
            if (ind == null) return false;
            Debug.Log("🍖『喰らいの宴』捕虜を喰らい、" + MinionCatalog.Get(ci).jpName + " が現れた");
            NotifySystem.Push("<b>喰らいの宴</b>　捕虜が " + MinionCatalog.Get(ci).jpName + " に変わった",
                NotifySystem.Kind.Story, regionId);
            return true;
        }
        if (index == Projects.Usurp)
        {
            AttributeSystem.AddPoint(AttributeSystem.Axis.War, 1, "簒奪");
            Debug.Log("👁️『簒奪』属性ポイントを1つ奪った");
            NotifySystem.Push("<b>簒奪</b>　属性ポイント +1", NotifySystem.Kind.Gain, regionId);
            return true;
        }
        if (index == Projects.Throne)
        {
            var dl = DemonLord.Instance;
            if (dl == null) return false;
            dl.GrantBP(3);
            Debug.Log("👑『玉座の顕現』魔王に BP +3");
            NotifySystem.Push("<b>玉座の顕現</b>　魔王に BP +3", NotifySystem.Kind.Gain, regionId);
            return true;
        }
        if (index == Projects.Pyre)
        {
            // 🔥 憤怒：奪われた装備を焼く＝**世界の装備水準を押し戻す**。
            //   ⚠ いまの死因は装備水準のインフレ（26→99）なので、ここに手が届く道は貴重。
            float dropped = LureEconomy.RecoverGear(12f);
            Debug.Log("🔥『焚刑』世界の装備水準を " + dropped.ToString("0.0") + " 押し戻した（いま "
                + LureEconomy.GearLabel + "）");
            NotifySystem.Push("<b>焚刑</b>　世界の装備水準 -" + dropped.ToString("0.0"), NotifySystem.Kind.Gain, regionId);
            return true;
        }
        var r = SurfaceMap.Get(regionId);
        if (r == null) return false;
        r.celebrateTurns = SettlementSystem.CelebrateSpan;
        EurekaTracker.OnCelebrate();
        Debug.Log("🎉『祝祭』" + r.name + " で祝祭が始まった（" + SettlementSystem.CelebrateSpan + "ターン）");
        NotifySystem.Push("<b>祝祭</b>　" + r.name + " が沸いている", NotifySystem.Kind.Gain, regionId);
        return true;
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
