using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🔭 斥候（Civ の Scout）。S4。
///
/// 眷属は「戦う指揮官」で高くつくが、斥候は **安い・速い・地形を無視する・戦えない** 専門職。
/// 役割は3つだけ：**霧を剥がす／発見を拾う／進軍先の下見をする**。
///
/// Civ VII の斥候と同じく、森や荒地の踏破コストを無視して動ける（＝道なき道を行くのが仕事）。
/// 戦闘力を持たないので敵領には入れず、奪われた土地に取り残されると失われる。
///
/// 純static・実行時保持。関連: [[surface-units-u1]] [[DiscoverySystem]] [[civ7-gap-plan]]。
/// </summary>
public static class ScoutSystem
{
    public class Scout
    {
        public int id;
        public int regionId = -1;
        public int mp = -1;          // -1＝満タン
    }

    public const int Cost = 150;     // DP
    /// <summary>
    /// 1ターンに進めるタイル数。
    /// ⚠ **`const` にしてはいけない**（研究で伸びる値。コンパイル時に焼き込まれて一生反映されない
    ///   ＝ `SquadMaxSlots` で一度踏んだのと同じ罠 → [[handoff-status]]）。
    /// </summary>
    public static int Movement => 4 + (ResearchState.IsResearched("s_road") ? 1 : 0);   // 🛣️『街道』
    public const int Vision = 3;

    private static List<Scout> all;
    private static int nextId = 1;
    private static void EnsureInit() { if (all == null) all = new List<Scout>(); }
    public static void Reset() { all = new List<Scout>(); nextId = 1; }

    public static IReadOnlyList<Scout> All { get { EnsureInit(); return all; } }
    public static int Count { get { EnsureInit(); return all.Count; } }
    public static Scout Of(int id)
    {
        EnsureInit();
        foreach (var s in all) if (s.id == id) return s;
        return null;
    }
    public static Scout At(int regionId)
    {
        EnsureInit();
        foreach (var s in all) if (s.regionId == regionId) return s;
        return null;
    }
    public static int MpOf(Scout s) { return s == null ? 0 : (s.mp < 0 ? Movement : s.mp); }

    /// <summary>上限（多すぎても意味が無いので抑える）。研究『斥候』で増える。</summary>
    public static int Limit { get { return 2 + (ResearchState.IsResearched("s_scout") ? 2 : 0); } }

    public static bool CanSpawn(int regionId, out string why)
    {
        why = "";
        var r = SurfaceMap.Get(regionId);
        if (!r.owned) { why = "自領からしか送り出せません"; return false; }
        if (Count >= Limit) { why = "斥候はこれ以上出せません（上限" + Limit + "／研究『斥候』で+2）"; return false; }
        var res = DungeonResourceManager.Instance;
        if (res != null && res.DungeonPoints < Cost) { why = "DPが足りません（要" + Cost + "）"; return false; }
        return true;
    }

    public static bool TrySpawn(int regionId)
    {
        EnsureInit();
        string why;
        if (!CanSpawn(regionId, out why)) { Debug.LogWarning("⚠️ 斥候を出せません：" + why); return false; }
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(Cost)) return false;
        var s = new Scout { id = nextId++, regionId = regionId };
        all.Add(s);
        SurfaceMap.MarkSeen(regionId, Vision);
        Debug.Log($"🔭『斥候』{SurfaceMap.Get(regionId).name} から斥候#{s.id}を送り出した（-{Cost}DP・移動力{Movement}・視界{Vision}）");
        return true;
    }

    /// <summary>斥候の道順。**地形の重みを無視**して最短のタイル数で進む（森も荒地も1）。</summary>
    public static List<int> PathTo(Scout s, int target)
    {
        if (s == null || s.regionId == target) return null;
        var prev = new Dictionary<int, int>();
        var q = new Queue<int>();
        prev[s.regionId] = -1; q.Enqueue(s.regionId);
        bool found = false; int guard = 0;
        while (q.Count > 0 && !found && guard++ < 4000)
        {
            int cur = q.Dequeue();
            foreach (var n in SurfaceMap.Neighbors(cur))
            {
                if (!SurfaceMap.IsPassable(n) || prev.ContainsKey(n.id)) continue;
                prev[n.id] = cur;
                if (n.id == target) { found = true; break; }
                if (n.owner == SurfaceMap.OwnerNeutral || n.owned) q.Enqueue(n.id);   // 敵領は通れない
            }
        }
        if (!found) return null;
        var path = new List<int>();
        int step = target;
        while (step != s.regionId) { path.Add(step); step = prev[step]; }
        path.Reverse();
        return path;
    }

    public static bool CanMoveNow(Scout s, int target, out int cost, out string why)
    {
        cost = 0; why = "";
        if (s == null) { why = "斥候がいません"; return false; }
        var r = SurfaceMap.Get(target);
        if (!SurfaceMap.IsPassable(r)) { why = "そこへは入れません"; return false; }
        if (!r.owned && r.owner != SurfaceMap.OwnerNeutral) { why = "斥候は敵領に入れません（戦えません）"; return false; }
        var path = PathTo(s, target);
        if (path == null) { why = "道がありません"; return false; }
        cost = path.Count;                       // 🔭 地形の重みは無視
        if (cost > MpOf(s)) { why = "移動力が足りません（要" + cost + "・残り" + MpOf(s) + "）"; return false; }
        return true;
    }

    public static bool TryMoveTo(int scoutId, int target)
    {
        var s = Of(scoutId); if (s == null) return false;
        int cost; string why;
        if (!CanMoveNow(s, target, out cost, out why)) { Debug.LogWarning("⚠️ 斥候を動かせません：" + why); return false; }
        var path = PathTo(s, target);
        s.mp = MpOf(s) - cost;
        // 通り道の1マスずつ視界を開け、発見も拾う（Civの斥候と同じで「歩いた線」が見える）
        foreach (int id in path)
        {
            s.regionId = id;
            SurfaceMap.MarkSeen(id, Vision);
            DiscoverySystem.OnEnter(id);
        }
        Debug.Log($"🔭『斥候』#{s.id} が {SurfaceMap.Get(target).name} まで進んだ（-{cost}・残り{s.mp}）");
        return true;
    }

    /// <summary>毎ターン：移動力を戻し、視界を更新し、敵に取り込まれた斥候は失われる。</summary>
    public static void TickTurn()
    {
        EnsureInit();
        for (int i = all.Count - 1; i >= 0; i--)
        {
            var s = all[i];
            var r = SurfaceMap.Get(s.regionId);
            if (s.regionId < 0 || r.isOcean || (!r.owned && r.owner != SurfaceMap.OwnerNeutral))
            {
                Debug.Log($"🔭『斥候を失った』#{s.id} は {r.name} で消息を絶った");
                all.RemoveAt(i); continue;
            }
            s.mp = Movement;
            SurfaceMap.MarkSeen(s.regionId, Vision);
        }
    }
}
