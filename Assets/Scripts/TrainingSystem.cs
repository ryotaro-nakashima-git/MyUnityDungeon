using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🏋️ 配下を育てる2つの道。上位階層のレベル問題の続き（③④）。
///
/// **なぜ要るか**: 経験値は「冒険者と当たった階層」に入るので、放っておくと交戦の多い浅い階だけが育つ。
/// 魔素濃度（`MinionRoster.ExpForFloor`）で既定の向きは直したが、
/// **プレイヤーが能動的に下層を仕上げる手段**が無いと、取り残された個体を救えない。
///
/// - **③ 訓練所**（地上の施設）… 占領した土地に建て、配下を送り込むと毎ターン鍛えられて帰ってくる。
///   訓練中は**隊にもボスにも使えない**＝防衛を削って将来に投資する判断になる。
///   ※「配下は迷宮を出られない」という原作の縛りは、**自陣にした土地なら自由**という整理で通す。
/// - **④ 実戦の反芻**（素材を注ぐ）… **冒険者が到達しなかった階層に置いた個体にだけ**使える。
///   近道ではなく「戦えなかったぶんを埋める」取り返しの手段に限定する。
///
/// 純static・実行時保持。関連: [[MinionRoster]] [[DistrictCatalog]] [[deep-floor-leveling]]。
/// </summary>
public static class TrainingSystem
{
    // ============ ③ 訓練所 ============
    public class Trainee
    {
        public int individualId;
        public int regionId;      // 送り込んだ訓練所のタイル
        public int turnsLeft;
    }

    public const int TrainTurns = 4;        // 帰ってくるまで
    public const int PerCamp = 3;           // 1つの訓練所が預かれる数

    private static List<Trainee> trainees;
    private static void EnsureInit() { if (trainees == null) trainees = new List<Trainee>(); }
    public static void Reset() { trainees = null; EnsureInit(); }
    public static List<Trainee> All { get { EnsureInit(); return trainees; } }

    public static bool IsTraining(int individualId)
    {
        EnsureInit();
        foreach (var t in trainees) if (t.individualId == individualId) return true;
        return false;
    }
    public static Trainee Of(int individualId)
    {
        EnsureInit();
        foreach (var t in trainees) if (t.individualId == individualId) return t;
        return null;
    }
    public static int CountAt(int regionId)
    {
        EnsureInit();
        int n = 0; foreach (var t in trainees) if (t.regionId == regionId) n++; return n;
    }

    /// <summary>その領域に訓練所が建っているか。</summary>
    public static bool HasCamp(int regionId)
    {
        var r = SurfaceMap.Get(regionId);
        if (!r.owned) return false;
        if (r.district >= 0 && DistrictCatalog.Get(r.district).yield == DistrictCatalog.Yield.Training) return true;
        if (r.district2 >= 0 && DistrictCatalog.Get(r.district2).yield == DistrictCatalog.Yield.Training) return true;
        return false;
    }

    /// <summary>その訓練所の1ターンあたりの経験値（隣接ボーナスで伸びる）。</summary>
    public static int ExpPerTurnAt(int regionId)
    {
        var r = SurfaceMap.Get(regionId);
        int adj = 0;
        for (int slot = 0; slot < 2; slot++)
        {
            int di = slot == 0 ? r.district : r.district2;
            if (di < 0 || DistrictCatalog.Get(di).yield != DistrictCatalog.Yield.Training) continue;
            adj = Mathf.Max(adj, DistrictCatalog.Adjacency(di, regionId));
        }
        return 40 + 15 * adj;
    }

    public static bool CanSend(int individualId, int regionId, out string why)
    {
        why = "";
        var v = MinionRoster.Get(individualId);
        if (v == null) { why = "個体が存在しない"; return false; }
        if (v.level >= MinionRoster.MaxLevel) { why = "既に上限Lv"; return false; }
        if (IsTraining(individualId)) { why = "既に訓練中"; return false; }
        if (!HasCamp(regionId)) { why = "その領域に訓練所が無い"; return false; }
        if (CountAt(regionId) >= PerCamp) { why = "その訓練所は満員（" + PerCamp + "体まで）"; return false; }
        if (KinRoster.IsAwayFromDungeon(individualId)) { why = "眷属またはその配下は送れない"; return false; }
        var fm = DungeonFeatureManager.Instance;
        if (fm != null && fm.IsIndividualInAnySquad(individualId)) { why = "隊に編成中（先に外す）"; return false; }
        if (fm != null && fm.IsIndividualBoss(individualId)) { why = "ボスに任命中（先に解く）"; return false; }
        return true;
    }

    public static bool TrySend(int individualId, int regionId)
    {
        EnsureInit();
        string why;
        if (!CanSend(individualId, regionId, out why)) { Debug.LogWarning("⚠️ " + why); return false; }
        trainees.Add(new Trainee { individualId = individualId, regionId = regionId, turnsLeft = TrainTurns });
        var v = MinionRoster.Get(individualId);
        Debug.Log($"🏋️『訓練へ』{MinionCatalog.Get(v.catalogIndex).jpName} 個体#{individualId}(Lv{v.level}) を "
            + $"{SurfaceMap.Get(regionId).name} の訓練所へ（{TrainTurns}ターン・毎ターン+{ExpPerTurnAt(regionId)}exp／その間は隊・ボスに使えない）");
        return true;
    }

    /// <summary>途中で呼び戻す（経験はそこまでのぶんが既に入っている）。</summary>
    public static bool Recall(int individualId)
    {
        EnsureInit();
        var t = Of(individualId);
        if (t == null) return false;
        trainees.Remove(t);
        Debug.Log($"🏋️『呼び戻し』個体#{individualId} を訓練から戻した");
        return true;
    }

    /// <summary>毎ターン：訓練中の個体に経験を入れ、期間が終われば戻す。</summary>
    public static void TickTurn()
    {
        EnsureInit();
        var done = new List<Trainee>();
        int n = 0;
        foreach (var t in trainees)
        {
            // 訓練所が壊された／領域を奪われたら中断
            if (!HasCamp(t.regionId)) { done.Add(t); continue; }
            MinionRoster.AddExp(t.individualId, ExpPerTurnAt(t.regionId));
            n++;
            t.turnsLeft--;
            if (t.turnsLeft <= 0) done.Add(t);
        }
        foreach (var t in done)
        {
            trainees.Remove(t);
            var v = MinionRoster.Get(t.individualId);
            if (v != null) Debug.Log($"🏋️『訓練を終えた』{MinionCatalog.Get(v.catalogIndex).jpName} 個体#{t.individualId} が Lv{v.level} で戻った");
        }
        if (n > 0) Debug.Log($"🏋️『訓練』{n} 体が鍛えられている");
    }

    // ============ ④ 実戦の反芻（素材を注ぐ・未到達階層限定） ============
    /// <summary>反芻に要る素材（レベルが上がるほど高くなる）。</summary>
    public static int DrillCost(int individualId)
    {
        var v = MinionRoster.Get(individualId);
        if (v == null) return 0;
        return 4 + v.level / 3;
    }
    public const int DrillExp = 90;

    /// <summary>
    /// 使えるのは「直近のウェーブで**実戦経験が入らなかった個体**」だけ。
    /// 戦えなかったぶんを埋めるための手段で、レベルを買う近道ではない。
    ///
    /// ⚠ 旧仕様は「冒険者が到達した階層(`floor &lt;= deepest`)なら一律で禁止」だった。
    ///   しかし到達された階でも**実際には戦っていない個体**（隊に居ない・その波で湧く前に終わった等）が
    ///   いるうえ、B1Fの実戦経験は1波0.8Lvぶんしかない。結果として
    ///   **「到達されるまでは反芻でしか埋められない／到達された瞬間に反芻が禁止される」**という、
    ///   両方塞がる時期ができていた。→ 判定を**個体が戦ったか**に変える。
    /// </summary>
    public static bool CanDrill(int individualId, out string why)
    {
        why = "";
        var v = MinionRoster.Get(individualId);
        if (v == null) { why = "個体が存在しない"; return false; }
        if (v.level >= MinionRoster.MaxLevel) { why = "既に上限Lv"; return false; }
        if (IsTraining(individualId)) { why = "訓練中"; return false; }
        var fm = DungeonFeatureManager.Instance;
        var fl = DungeonFloorManager.Instance;
        if (fm == null || fl == null) { why = "準備中"; return false; }
        int floor = fm.SquadFloorOfIndividual(individualId);
        if (floor < 0) floor = fm.BossFloorOfIndividual(individualId);
        if (floor < 0) { why = "階層に配置されていない（隊かボスに置く）"; return false; }
        if (fl.LastDeepestReached < 0) { why = "まだ侵略が起きていない"; return false; }
        if (v.foughtLastWave) { why = "前のウェーブで実戦経験が入っている（戦えなかった個体だけが対象）"; return false; }
        int cost = DrillCost(individualId);
        var res = DungeonResourceManager.Instance;
        if (res != null && res.CraftMaterials < cost) { why = "素材が足りない（要" + cost + "）"; return false; }
        return true;
    }

    public static bool TryDrill(int individualId)
    {
        string why;
        if (!CanDrill(individualId, out why)) { Debug.LogWarning("⚠️ " + why); return false; }
        int cost = DrillCost(individualId);
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendMaterial(cost)) { Debug.LogWarning("⚠️ 素材が足りません。"); return false; }
        MinionRoster.AddExp(individualId, DrillExp);
        var v = MinionRoster.Get(individualId);
        Debug.Log($"🔁『実戦の反芻』{MinionCatalog.Get(v.catalogIndex).jpName} 個体#{individualId} に +{DrillExp}exp（-{cost}素材 → Lv{v.level}）");
        return true;
    }
}
