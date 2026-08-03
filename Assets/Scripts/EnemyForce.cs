using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⚔️ 盤の上を歩く敵の軍（U2）。他魔王の軍と、人間の奪還軍。
///
/// **これまでは数値だけで解決していた**：他魔王は「一番手薄な自領」を遠隔から一撃で奪い、
/// 奪還軍も同じだった。突然もぎ取られるので、**防ぎようも読みようも無かった**。
///
/// U2では Civ と同じく、敵も**盤の上に立って歩いてくる**：
/// - 本拠地（人間は中立の町）から軍が湧き、**毎ターン移動力ぶん**近づく
/// - **支配地域(ZoC)**：こちらの眷属に隣接したら足を止める＝壁になる
/// - 目標に隣り合ってから**攻城**する＝**来るのが見えるので迎え撃てる**
/// - こちらから**迎撃**もできる（眷属で軍そのものを叩く。タイルは取らない）
///
/// 純static・実行時保持。関連: [[RivalLords]] [[KinRoster]] [[surface-units-u1]]。
/// </summary>
public static class EnemyForce
{
    public class Army
    {
        public int id;
        public int owner;        // -1＝人間の奪還軍／0..＝他魔王のindex
        public string name;
        public float power;
        public int regionId;
        public int targetId = -1;
        public int mp;
        public int idleTurns;    // 目標に届かないまま経った回数（長いと引き上げる）
    }

    public const int Movement = 2;          // 1ターンに歩けるタイル数（重い地形は入れない）
    public const int MaxPerRival = 2;       // 1人の魔王が同時に出せる軍
    public const int MaxHuman = 2;

    private static List<Army> all;
    private static int nextId = 1;
    private static void EnsureInit() { if (all == null) all = new List<Army>(); }
    public static void Reset() { all = new List<Army>(); nextId = 1; }

    public static IReadOnlyList<Army> All { get { EnsureInit(); return all; } }
    public static int Count { get { EnsureInit(); return all.Count; } }
    public static Army At(int regionId)
    {
        EnsureInit();
        foreach (var a in all) if (a.regionId == regionId) return a;
        return null;
    }
    public static int CountOf(int owner)
    {
        EnsureInit(); int n = 0;
        foreach (var a in all) if (a.owner == owner) n++;
        return n;
    }
    public static string ColorOf(Army a)
    {
        return a.owner < 0 ? "#e8e2d0" : RivalLords.ColorOf(a.owner);
    }
    public static string OwnerName(Army a)
    {
        return a.owner < 0 ? "人間の奪還軍" : RivalLords.NameOf(a.owner) + "の軍";
    }

    // ============ 湧く ============
    /// <summary>他魔王が本拠地から軍を切り出す。出したぶんだけ本体の力は減る（際限なく湧かない）。</summary>
    public static void SpawnFromRival(int rivalIndex)
    {
        EnsureInit();
        var rv = RivalLords.Get(rivalIndex);
        if (rv.defeated) return;
        if (CountOf(rivalIndex) >= MaxPerRival) return;
        int home = RivalLords.HomeOf(rivalIndex);
        if (home < 0) return;
        float take = rv.power * 0.45f;
        if (take < 60f) return;
        rv.power -= take;
        var a = new Army
        {
            id = nextId++, owner = rivalIndex, name = RivalLords.NameOf(rivalIndex) + "の軍",
            power = take, regionId = home, mp = Movement,
        };
        all.Add(a);
        Debug.Log($"⚔️『進発』{a.name}（戦力{take:0}）が {SurfaceMap.Get(home).name} を発った");
    }

    /// <summary>人間の奪還軍が、こちらの版図に接した中立の土地から湧く。</summary>
    public static void SpawnHuman(float army)
    {
        EnsureInit();
        if (CountOf(-1) >= MaxHuman) return;
        // 自領に隣接する中立の陸から湧く（＝どこから来るかが見える）
        var cands = new List<SurfaceMap.Region>();
        foreach (var r in SurfaceMap.All)
        {
            if (r.isOcean || r.owner != SurfaceMap.OwnerNeutral) continue;
            if (!SurfaceMap.IsPassable(r)) continue;
            foreach (var l in r.links) if (SurfaceMap.Get(l).owned) { cands.Add(r); break; }
        }
        if (cands.Count == 0) return;
        var from = cands[Random.Range(0, cands.Count)];
        var a = new Army
        {
            id = nextId++, owner = -1, name = "奪還軍", power = army,
            regionId = from.id, mp = Movement,
        };
        all.Add(a);
        Debug.Log($"⚔️『奪還軍』（戦力{army:0}）が {from.name} に現れた");
    }

    // ============ 動く ============
    /// <summary>狙う先＝こちらの領域のうち一番手薄なところ（無ければ中立を広げに行く）。</summary>
    private static int PickTarget(Army a)
    {
        SurfaceMap.Region best = null; float bestScore = float.MaxValue;
        foreach (var r in SurfaceMap.All)
        {
            if (r.isOcean || r.type == SurfaceMap.RegionType.Gate) continue;
            bool hostile = a.owner < 0 ? r.owned : (r.owned || (r.owner != SurfaceMap.OwnerRivalBase + a.owner && r.owner != SurfaceMap.OwnerNeutral));
            if (!hostile) continue;
            float d = SurfaceMap.DefenseOf(r.id) + SurfaceMap.HexDist(SurfaceMap.Get(a.regionId), r) * 25f;
            if (d < bestScore) { bestScore = d; best = r; }
        }
        return best != null ? best.id : -1;
    }

    /// <summary>次の1歩（通れる隣で、目標に一番近づくもの）。</summary>
    private static int NextStep(Army a, int target)
    {
        var cur = SurfaceMap.Get(a.regionId);
        var tgt = SurfaceMap.Get(target);
        int bestId = -1; int bestDist = SurfaceMap.HexDist(cur, tgt);
        foreach (var n in SurfaceMap.Neighbors(a.regionId))
        {
            if (!SurfaceMap.IsPassable(n)) continue;
            if (n.id == target) continue;                      // 目標には「攻める」ので踏み込まない
            if (At(n.id) != null) continue;                    // 味方の軍と重ならない
            if (n.owned) continue;                             // こちらの領域は攻城してからでないと入れない
            int d = SurfaceMap.HexDist(n, tgt);
            if (d < bestDist) { bestDist = d; bestId = n.id; }
        }
        return bestId;
    }

    /// <summary>🚧 こちらの眷属に隣接しているか（Civの支配地域＝足が止まる）。</summary>
    public static bool InKinZoC(int regionId)
    {
        foreach (var n in SurfaceMap.Neighbors(regionId))
        {
            var k = KinRoster.KinAt(n.id);
            if (k != null && k.injuryTurns <= 0) return true;
        }
        return false;
    }

    // ============ ターンの解決 ============
    public static void ResolveTurn(int turn)
    {
        EnsureInit();
        for (int i = all.Count - 1; i >= 0; i--)
        {
            var a = all[i];
            if (a.owner >= 0 && RivalLords.Get(a.owner).defeated) { all.RemoveAt(i); continue; }
            a.mp = Movement;

            if (a.targetId < 0 || !IsStillHostile(a, a.targetId)) a.targetId = PickTarget(a);
            if (a.targetId < 0) { Retreat(a, i, "狙う先が無くなった"); continue; }

            var tgt = SurfaceMap.Get(a.targetId);
            while (a.mp > 0)
            {
                // 隣り合ったら攻城
                if (SurfaceMap.HexDist(SurfaceMap.Get(a.regionId), tgt) <= 1) { Assault(a, i, turn); break; }
                // 🚧 支配地域：眷属の隣では足が止まる
                if (InKinZoC(a.regionId))
                {
                    Debug.Log($"🚧『足止め』{a.name} は {SurfaceMap.Get(a.regionId).name} で眷属に睨まれて動けない");
                    break;
                }
                int nxt = NextStep(a, a.targetId);
                if (nxt < 0) { a.idleTurns++; break; }
                int cost = SurfaceMap.MoveCost(SurfaceMap.Get(nxt));
                if (cost > a.mp) break;
                a.regionId = nxt; a.mp -= cost; a.idleTurns = 0;
            }
            if (a.idleTurns >= 3 && i < all.Count && all.Contains(a)) Retreat(a, all.IndexOf(a), "道が塞がれた");
        }
    }

    private static bool IsStillHostile(Army a, int id)
    {
        var r = SurfaceMap.Get(id);
        if (a.owner < 0) return r.owned;
        return r.owned || (r.owner != SurfaceMap.OwnerRivalBase + a.owner && r.owner != SurfaceMap.OwnerNeutral);
    }

    /// <summary>攻城：隣り合った目標を攻める。勝てば占領してそこへ入り、負ければ削れて下がる。</summary>
    private static void Assault(Army a, int index, int turn)
    {
        var tgt = SurfaceMap.Get(a.targetId);
        // 🏯 迷宮の入口だけは地上の軍では落とせない（そこは迷宮側の防衛戦で決着する）
        if (tgt.type == SurfaceMap.RegionType.Gate) { a.targetId = -1; return; }
        float atk = a.power * Random.Range(0.85f, 1.15f);
        float def = SurfaceMap.DefenseOf(tgt.id);
        tgt.lastResultTurn = turn;
        if (atk > def)
        {
            bool wasMine = tgt.owned;
            SurfaceMap.SetOwner(tgt.id, a.owner < 0 ? SurfaceMap.OwnerNeutral : SurfaceMap.OwnerRivalBase + a.owner);
            tgt.lastResult = (a.owner < 0 ? "奪還された" : RivalLords.NameOf(a.owner) + "に奪われた");
            a.regionId = tgt.id; a.targetId = -1;
            a.power *= 0.75f;
            if (wasMine)
            {
                KinRoster.OnRegionLost(tgt.id, OwnerName(a));
                Debug.Log($"🔥『領域を奪われた』{OwnerName(a)} が {tgt.name} を落とした（敵{atk:0} vs 守り{def:0}）");
            }
            else Debug.Log($"⚔️『{OwnerName(a)}』が {tgt.name} を制圧");
        }
        else
        {
            tgt.lastResult = (a.owner < 0 ? "奪還軍" : RivalLords.NameOf(a.owner)) + "の攻撃を撃退";
            a.power *= 0.7f;
            a.targetId = -1;
            Debug.Log($"🛡️『防衛成功』{tgt.name} が {OwnerName(a)} を退けた（敵{atk:0} vs 守り{def:0}）");
            if (a.power < 50f) Retreat(a, index, "壊滅寸前");
        }
    }

    private static void Retreat(Army a, int index, string why)
    {
        EnsureInit();
        int at = all.IndexOf(a);
        if (at < 0) return;
        if (a.owner >= 0) RivalLords.Get(a.owner).power += a.power * 0.6f;   // 本体に還る
        all.RemoveAt(at);
        Debug.Log($"↩️『引き上げ』{a.name} が退いた（{why}）");
    }

    // ============ 迎撃（こちらから叩く） ============
    /// <summary>眷属が軍を叩く。勝てば軍は消え、負ければ眷属が傷つく。タイルは取らない。</summary>
    public static bool ResolveIntercept(KinRoster.Kin k, Army a)
    {
        EnsureInit();
        float mine = KinRoster.ArmyPower(k) * KinPromotion.AssaultMult(k);
        float theirs = a.power * Random.Range(0.9f, 1.1f);
        int at = all.IndexOf(a);
        if (mine > theirs)
        {
            if (at >= 0) all.RemoveAt(at);
            var res = DungeonResourceManager.Instance;
            int loot = Mathf.RoundToInt(a.power * 1.2f);
            if (res != null) { res.AddDP(loot); res.AddMaterial(6); }
            KinPromotion.AddMerit(k, 3, "野戦で軍を破った");
            Debug.Log($"⚔️『迎撃成功』{k.trueName} が {a.name} を撃ち破った（{mine:0} vs {theirs:0}・+{loot}DP）");
            return true;
        }
        a.power *= 0.8f;
        k.injuryTurns = Mathf.Max(k.injuryTurns, 2);
        Debug.Log($"⚔️『迎撃失敗』{k.trueName} は {a.name} に押し返された（{mine:0} vs {theirs:0}・2ターン負傷）");
        return false;
    }
}
