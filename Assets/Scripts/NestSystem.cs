using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🕳️ <b>盤の上のダンジョン</b>（④）── 野良の巣・他魔王の迷宮・他プレイヤーの迷宮。
///
/// <b>三種類</b>
///   - <b>野良の巣</b>   … 盤の広さに追従（中で6〜10）。2〜4層・自動生成。
///     ⚠ <b>地上に軍を出さない。</b>脅威をこれ以上増やさず、「行く理由のある場所」と
///     <b>格を上げる練習場</b>に徹させる（→ [[MinionRank]] 段6の門）。
///   - <b>魔王の迷宮</b> … 既存の3人。5〜8層。地上へは今までどおり軍を出す。
///   - <b>他プレイヤー</b> … マルチ用。器だけ用意してある。
///
/// ⚠⚠ <b>渡すものは1つだけ。</b> どの種類でも <see cref="DungeonSnapshot"/> しか渡さない。
///   ボット専用の近道を作らないための決まり（→ [[DungeonSnapshot]]）。
///
/// 純static・実行時保持。関連: [[SurfaceMap]] [[DungeonGenerator]] [[RivalLords]]。
/// </summary>
public static class NestSystem
{
    public class Nest
    {
        public int regionId = -1;
        public DungeonSnapshot snap;
        /// <summary>制覇済みか。⚠ 制覇しても盤からは消さない（跡地として残す）。</summary>
        public bool conquered;
        /// <summary>何ターン目に制覇したか（-1＝まだ）。</summary>
        public int conqueredTurn = -1;
        /// <summary>他魔王の迷宮なら、その index（-1＝野良）。</summary>
        public int rivalIndex = -1;
    }

    private static List<Nest> all;
    private static void EnsureInit() { if (all == null) all = new List<Nest>(); }

    public static void Reset() { all = null; EnsureInit(); }
    public static IReadOnlyList<Nest> All { get { EnsureInit(); return all; } }
    public static int Count { get { EnsureInit(); return all.Count; } }
    public static Nest At(int i) { EnsureInit(); return (i >= 0 && i < all.Count) ? all[i] : null; }

    public static Nest AtRegion(int regionId)
    {
        EnsureInit();
        foreach (var n in all) if (n.regionId == regionId) return n;
        return null;
    }
    public static int IndexOfRegion(int regionId)
    {
        EnsureInit();
        for (int i = 0; i < all.Count; i++) if (all[i].regionId == regionId) return i;
        return -1;
    }
    public static int RemainingWild
    {
        get { EnsureInit(); int n = 0; foreach (var x in all) if (!x.conquered && x.rivalIndex < 0) n++; return n; }
    }

    /// <summary>
    /// 🌍 盤に巣を撒く。
    /// ⚠ <b>盤の広さに追従させる</b>（独立勢力と同じ `SurfaceMap.Count / 500`）。
    ///   固定数にすると、極小1,160タイルでは密集し、大6,958タイルでは見つからなくなる。
    /// ⚠ <b>迷宮の目の前には置かない</b>（depth 2 以上）。初手で殴れる練習場は練習にならない。
    /// </summary>
    public static void Build()
    {
        all = new List<Nest>();
        if (SurfaceMap.Count <= 0) return;

        // ── 🔥 他魔王の本拠地を「入れるダンジョン」にする ──
        for (int i = 0; i < RivalLords.Count; i++)
        {
            int home = SurfaceMap.HomeRegionOfRival(i);
            if (home < 0) continue;
            var n = new Nest { regionId = home, rivalIndex = i };
            n.snap = BuildSnapshot(DungeonSnapshot.Kind.RivalLord, RivalLords.NameOf(i) + "の迷宮",
                                   home * 7919 + 13, 5 + i, 3 + i * 2);
            all.Add(n);
        }

        // ── 🕳️ 野良の巣 ──
        int want = Mathf.Clamp(SurfaceMap.Count / 500, 4, 10);
        var cand = new List<SurfaceMap.Region>();
        foreach (var r in SurfaceMap.All)
        {
            if (r.isOcean || !SurfaceMap.IsPassable(r)) continue;
            if (r.owner != SurfaceMap.OwnerNeutral) continue;    // 無主の荒野にだけ湧く
            if (r.rivalHome >= 0 || r.type == SurfaceMap.RegionType.Gate) continue;
            if (r.wonderIndex >= 0 || r.naturalWonder >= 0) continue;
            if (r.depth < 2f) continue;                           // 迷宮の目の前は避ける
            cand.Add(r);
        }
        for (int i = 0; i < cand.Count; i++)
        { int j = Random.Range(i, cand.Count); var t = cand[i]; cand[i] = cand[j]; cand[j] = t; }
        // ⚠ 近いものを1つは置く（人類の集落と同じ理屈。全部遠いと1周のあいだ一度も行けない）
        if (cand.Count > 1)
        {
            int near = 0;
            for (int i = 1; i < cand.Count; i++) if (cand[i].depth < cand[near].depth) near = i;
            var t0 = cand[0]; cand[0] = cand[near]; cand[near] = t0;
        }

        int placed = 0;
        foreach (var c in cand)
        {
            if (placed >= want) break;
            bool tooClose = false;
            foreach (var o in all)
            {
                var oc = SurfaceMap.Get(o.regionId);
                if (oc != null && SurfaceMap.HexDist(c, oc) < 5) { tooClose = true; break; }
            }
            if (tooClose) continue;
            // 深いほど手強い巣にする（盤の奥へ行く理由になる）
            int floors = Mathf.Clamp(2 + Mathf.FloorToInt(c.depth / 3f), 2, 4);
            int tier = Mathf.Clamp(Mathf.FloorToInt(c.depth / 1.6f), 0, 5);
            var n = new Nest { regionId = c.id };
            n.snap = BuildSnapshot(DungeonSnapshot.Kind.Wild, NameFor(c, all), c.id * 6151 + 7, floors, tier);
            all.Add(n);
            c.type = SurfaceMap.RegionType.Nest;
            placed++;
        }
        Debug.Log("🕳️『盤の上のダンジョン』野良の巣 " + placed + " ／ 魔王の迷宮 " + RivalLords.Count
            + "（盤 " + SurfaceMap.Count + " タイル）");
    }

    private static readonly string[] nestPre = { "朽ちた", "苔むした", "忘れられた", "喰らいの", "囁く", "血錆の", "骨の", "黒い", "灰の", "淀んだ" };
    private static readonly string[] nestSuf = { "巣穴", "坑", "洞", "穴倉", "窖", "塚" };
    /// <summary>
    /// 巣の名。⚠ <b>重複させない。</b> id のハッシュだけだと 10×6＝60通りしかなく、
    /// 実測で「淀んだ塚」「囁く洞」が同じ盤に2つずつ出た。盤の上で指して呼べない名前は名前ではない。
    /// </summary>
    private static string NameFor(SurfaceMap.Region c, List<Nest> placedSoFar)
    {
        int a = Mathf.Abs(c.id * 31), b = Mathf.Abs(c.id * 17);
        for (int k = 0; k < nestPre.Length * nestSuf.Length; k++)
        {
            string cand = nestPre[(a + k) % nestPre.Length] + nestSuf[(b + k / nestPre.Length) % nestSuf.Length];
            bool taken = false;
            for (int i = 0; i < placedSoFar.Count; i++)
                if (placedSoFar[i].snap != null && placedSoFar[i].snap.name == cand) { taken = true; break; }
            if (!taken) return cand;
        }
        return nestPre[a % nestPre.Length] + nestSuf[b % nestSuf.Length] + "・第" + (placedSoFar.Count + 1);
    }

    /// <summary>
    /// 🗿 中身を組み立てる。⚠ <b>すべて seed から決める</b>（同じ seed なら誰の環境でも同じ巣）。
    /// ⚠ 置く配下は<b>その段までの形態から選ぶ</b>。カタログの端から取ると、
    ///   浅い巣に古代種が出て practice にならない。
    /// </summary>
    public static DungeonSnapshot BuildSnapshot(DungeonSnapshot.Kind kind, string name, int seed, int floors, int tier)
    {
        var st = Random.state;              // ⚠ 盤の生成の乱数を汚さない（種を固定して使うので必ず戻す）
        Random.InitState(seed);

        var s = new DungeonSnapshot { name = name, kind = kind, seed = seed, tier = tier };
        // その段に見合う形態だけを候補にする
        var pool = new List<int>();
        int maxTierCp = 6 + tier * 8;
        for (int i = 0; i < MinionCatalog.Count; i++)
            if (MinionCatalog.Get(i).tierCP <= maxTierCp) pool.Add(i);
        if (pool.Count == 0) pool.Add(0);

        for (int f = 0; f < floors; f++)
        {
            int size = Mathf.Clamp(10 + f * 4 + tier, 10, 30);
            s.floorSizes.Add(size);
            int guards = 2 + f + tier / 2;
            for (int g = 0; g < guards; g++)
            {
                s.guardIndex.Add(pool[Random.Range(0, pool.Count)]);
                s.guardFloor.Add(f);
                s.guardLevel.Add(Mathf.Max(1, 1 + tier * 3 + f * 2 + Random.Range(0, 4)));
            }
            int traps = Random.Range(0, 2 + tier / 2);
            for (int t = 0; t < traps; t++)
            {
                s.trapKind.Add(Random.Range(1, 6));   // 毒/炎/氷/電気/出血（Basicは置かない）
                s.trapFloor.Add(f);
            }
        }
        // 主＝候補のうち一番強い形態から（深い巣ほど格上）
        int best = pool[0];
        for (int i = 0; i < pool.Count; i++) if (MinionCatalog.Get(pool[i]).tierCP > MinionCatalog.Get(best).tierCP) best = pool[i];
        s.lordIndex = best;
        s.lordLevel = Mathf.Max(1, 3 + tier * 4);
        s.lordHpMult = 2.0f + tier * 0.5f + (kind == DungeonSnapshot.Kind.RivalLord ? 2f : 0f);

        Random.state = st;
        return s;
    }

    /// <summary>🏆 制覇した。⚠ 見返りは<b>ここ1箇所</b>で配る（呼び出し側に散らさない）。</summary>
    public static void OnConquered(int nestIndex, int turn, int byIndividualId)
    {
        var n = At(nestIndex);
        if (n == null || n.conquered) return;
        n.conquered = true; n.conqueredTurn = turn;

        var res = DungeonResourceManager.Instance;
        int dp = Mathf.RoundToInt(n.snap.ThreatScore() * 6f);
        int mat = 15 + n.snap.tier * 8;
        int rp = 3 + n.snap.tier * 2;
        if (res != null) { res.AddDP(dp); res.AddMaterial(mat); }
        ResearchState.AddRP(rp);

        // 👑 段6『キング／クイーン』の門（→ [[MinionRank]]）
        var v = MinionRoster.Get(byIndividualId);
        if (v != null)
        {
            v.deedFlags |= MinionRank.FlagRaidedNest;
            MinionRank.AddDeed(byIndividualId, 40, "他のダンジョンを制覇した");
        }

        if (n.rivalIndex >= 0)
        {
            // 🔥 魔王の迷宮を制覇＝真核を奪う（地上で本拠地を落としたのと同じ扱い）
            RivalLords.OnHomeConquered(n.rivalIndex);
            if (v != null) v.deedFlags |= MinionRank.FlagSlewLord;
        }
        else
        {
            // 🕳️ 野良の巣：その巣の主を図鑑に載せる（＝行く理由）
            var lord = MinionCatalog.Get(n.snap.lordIndex);
            Debug.Log("🏆『制覇』" + n.snap.name + " を落とした ― " + lord.jpName + " を図鑑に記録"
                + "（+" + dp + "DP +" + mat + "素材 +" + rp + "RP）");
        }
        NotifySystem.Push("<b>" + n.snap.name + " を制覇</b>（+" + dp + "DP +" + mat + "素材 +" + rp + "RP）",
            NotifySystem.Kind.Gain, n.regionId);
    }

    /// <summary>UIの1行。</summary>
    public static string Line(int i)
    {
        var n = At(i); if (n == null) return "";
        var s = n.snap;
        string head = "<color=" + s.KindColor + ">" + s.name + "</color>";
        if (n.conquered) return "<color=#6f6889>" + s.name + "（制覇済み）</color>";
        return head + " <size=88%>" + s.KindName + "・" + s.FloorCount + "層・守り" + s.TotalGuards
             + "体・難度" + AdventurerAI.RankLetter(s.tier) + "</size>";
    }
}
