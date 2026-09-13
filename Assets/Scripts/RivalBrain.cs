using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🧠⚔️ <b>ライバル魔王に迷宮を経営させる</b>（敵も経営する・段①）。
///
/// <para>
/// ⚠⚠ <b>なぜ要るか（実測）</b>：ライバルは <c>power</c> という float が毎ターン
///   <b>+20/+28/+38 されるだけ</b>だった。21ターン後で <c>660/20 = 33点</c>。
///   実測した2位のスコア 24〜39点はこの直線そのもので、
///   <b>勝利の閾値（＝2位×倍率）が形骸化する直接の原因</b>だった（T22で決着）。
///   さらに<b>攻め込む先も開始時に1回生成した固定物</b>で、bot が作った物ではなかった。
/// </para>
///
/// <para>
/// ⚠⚠ <b>成長曲線を書かない。</b>ここが一番の罠 ――
///   「毎ターンいい感じに強くなる式」を書くと、結局 <c>power += growth</c> の複雑版にしかならない。
///   bot は<b>プレイヤーと同じ形で迷う</b>：重み比べではなく
///   <b>上から順に見て、最初に当てはまった1つだけ</b>を実行する（→ [[NextAction]]）。
///   資源が足りなければ<b>打てない</b>。
/// ⚠ <b>bot にだけ制約を免除しない。</b>階ごとの守りには上限があり、DPを払う。
///   免除すると「こちらは広げると死ぬのに、あちらは広げて強くなる」という
///   不公平が生まれる（→ [[growth-is-a-trap]]）。
/// ⚠ <b>盤は描画しない。</b>打った手は `DungeonSnapshot` に積むだけで、
///   実際に歩ける盤になるのは<b>攻め込んだときだけ</b>（`RaidBoard.Build` が既にやっている）。
/// </para>
///
/// 関連: [[RivalLords]]（誰か） [[NestSystem]]（どこに在るか） [[DungeonSnapshot]]（何が在るか）
///       [[Expedition]]（攻め込む） [[VictorySystem]]（スコア）。
/// </summary>
public static class RivalBrain
{
    // ⚠ 値はすべて**仮**。①の目的は「2位が伸びるか」を測ることで、当てることではない。
    //   測ってから直す（→ `dev_log` の段①）。

    /// <summary>1ターンの実入りの底（迷宮も領地も無いときでも少しは貯まる）。</summary>
    private const float EarnBase = 120f;
    /// <summary>階層1つあたりの実入り。深いほど稼ぐ＝プレイヤーの深度倍率と同じ考え方。</summary>
    private const float EarnPerFloor = 30f;
    /// <summary>自領1タイルあたりの実入り。</summary>
    private const float EarnPerTile = 6f;

    private const float CostGuard = 200f;   // 守りを1体置く
    private const float CostTrap = 150f;    // 罠を1つ置く
    private const float CostFloor = 1200f;  // 階層を1つ増やす
    private const float CostLord = 400f;    // 魔王のLvを1つ上げる

    /// <summary>階ごとの守りの上限。⚠ プレイヤーの『配置枠』にあたる歯止め。</summary>
    private static int MaxGuardsOn(int floor) { return 4 + floor; }
    /// <summary>階ごとの罠の上限。</summary>
    private static int MaxTrapsOn(int floor) { return 2 + floor / 2; }
    /// <summary>迷宮の深さの上限。⚠ 無いと際限なく潜って手が付けられなくなる。</summary>
    private const int MaxFloors = 10;

    // ── bot ごとの財布と履歴（`RivalLords.Rival` を汚さずここで持つ）──
    // ⚠ `RivalBrain` は `SaveSystem.StaticTypes` に載せること（静的フィールドが保存される）。
    //   ⚠ デリゲートを持たせない。
    private static float[] dp;
    private static string[] lastAct;
    private static int[] lostFloorAt;   // 直近で攻め落とされた階（-1＝無し）

    private static void EnsureInit()
    {
        int n = RivalLords.Count;
        if (dp == null || dp.Length != n) { dp = new float[n]; }
        if (lastAct == null || lastAct.Length != n) { lastAct = new string[n]; for (int i = 0; i < n; i++) lastAct[i] = ""; }
        if (lostFloorAt == null || lostFloorAt.Length != n) { lostFloorAt = new int[n]; for (int i = 0; i < n; i++) lostFloorAt[i] = -1; }
    }

    public static void Reset() { dp = null; lastAct = null; lostFloorAt = null; EnsureInit(); }

    public static float DpOf(int i) { EnsureInit(); return (i >= 0 && i < dp.Length) ? dp[i] : 0f; }
    public static string LastActOf(int i) { EnsureInit(); return (i >= 0 && i < lastAct.Length) ? lastAct[i] : ""; }

    /// <summary>⚔️ その魔王の迷宮（＝盤に在る巣の中身）。無ければ null。</summary>
    public static DungeonSnapshot DungeonOf(int rivalIndex)
    {
        for (int i = 0; i < NestSystem.Count; i++)
        {
            var n = NestSystem.At(i);
            if (n != null && n.rivalIndex == rivalIndex) return n.snap;
        }
        return null;
    }

    /// <summary>⚔️ その魔王の巣の index（遠征の行き先）。無ければ -1。</summary>
    public static int NestIndexOf(int rivalIndex)
    {
        for (int i = 0; i < NestSystem.Count; i++)
        {
            var n = NestSystem.At(i);
            if (n != null && n.rivalIndex == rivalIndex) return i;
        }
        return -1;
    }

    /// <summary>
    /// 🧠 毎ターン：全員が「稼ぐ → 1手だけ打つ」。
    /// ⚠ `RivalLords.ResolveTurn`（地上の軍）とは別の話。あちらは地上、こちらは迷宮。
    /// </summary>
    public static void ResolveTurn(int turn)
    {
        EnsureInit();
        for (int i = 0; i < RivalLords.Count; i++)
        {
            var rv = RivalLords.Get(i);
            if (rv.defeated) { lastAct[i] = "排除済み"; continue; }
            var snap = DungeonOf(i);
            if (snap == null) { lastAct[i] = "迷宮が無い"; continue; }
            var nest = NestSystem.At(NestIndexOf(i));
            if (nest != null && nest.conquered) { lastAct[i] = "本拠を失っている"; continue; }

            dp[i] += EarnBase + snap.FloorCount * EarnPerFloor + RivalLords.TerritoryOf(i) * EarnPerTile;
            lastAct[i] = Act(i, snap);
        }
    }

    /// <summary>
    /// 🧠 1手だけ打つ。⚠⚠ <b>上から順に見て、最初に当てはまったものだけ。</b>
    /// 重み比べにしない ―― プレイヤー側の実測で「重みだと上位が居座って他が一生出てこない」
    /// と分かっている（→ [[NextAction]]）。
    /// </summary>
    private static string Act(int i, DungeonSnapshot s)
    {
        // ① 守りが1体も居ない階がある ―― これだけは強さの話ではなく「素通りされる」
        for (int f = 0; f < s.FloorCount; f++)
            if (s.GuardCountOn(f) == 0)
                return TryGuard(i, s, f, "守りの居ない " + (f + 1) + "層に1体置いた");

        // ② 直近で攻め落とされた階を厚くする
        if (lostFloorAt[i] >= 0 && lostFloorAt[i] < s.FloorCount)
        {
            int f = lostFloorAt[i];
            if (s.GuardCountOn(f) < MaxGuardsOn(f))
            {
                string r = TryGuard(i, s, f, "破られた " + (f + 1) + "層を厚くした");
                if (r != null) { lostFloorAt[i] = -1; return r; }
            }
            else lostFloorAt[i] = -1;
        }

        // ③ 枠が空いている階に守りを足す（浅い階から埋める＝入口を固める）
        for (int f = 0; f < s.FloorCount; f++)
            if (s.GuardCountOn(f) < MaxGuardsOn(f))
            {
                string r = TryGuard(i, s, f, (f + 1) + "層に守りを足した");
                if (r != null) return r;
                break;   // ⚠ DPが足りないなら下の階を見ても同じ。貯める
            }

        // ④ 罠を足す
        for (int f = 0; f < s.FloorCount; f++)
            if (TrapCountOn(s, f) < MaxTrapsOn(f))
            {
                if (dp[i] < CostTrap) break;
                dp[i] -= CostTrap;
                s.trapKind.Add(Random.Range(1, 6));
                s.trapFloor.Add(f);
                return (f + 1) + "層に罠を仕掛けた";
            }

        // ⑤ 全部埋まった ―― 深くする
        if (s.FloorCount < MaxFloors && dp[i] >= CostFloor)
        {
            dp[i] -= CostFloor;
            int f = s.FloorCount;
            s.floorSizes.Add(Mathf.Clamp(10 + f * 4 + s.tier, 10, 30));
            // ⚠ 新しい階は空のまま。次のターン以降に①が拾って埋める（一気に完成させない）
            return "迷宮を " + (f + 1) + " 層まで掘り下げた";
        }

        // ⑥ それでも余る ―― 主を鍛える
        if (dp[i] >= CostLord)
        {
            dp[i] -= CostLord;
            s.lordLevel += 1;
            s.lordHpMult += 0.15f;
            return "自らを鍛えた（Lv" + s.lordLevel + "）";
        }

        return "力を蓄えている";
    }

    private static string TryGuard(int i, DungeonSnapshot s, int floor, string msg)
    {
        if (dp[i] < CostGuard) return null;
        if (s.GuardCountOn(floor) >= MaxGuardsOn(floor)) return null;
        dp[i] -= CostGuard;
        // 置ける形態は段（tier）で決まる ―― プレイヤーの解禁にあたる歯止め
        int pick = PickGuard(s);
        s.guardIndex.Add(pick);
        s.guardFloor.Add(floor);
        s.guardLevel.Add(Mathf.Max(1, 1 + s.tier * 3 + floor * 2 + Random.Range(0, 4)));
        return msg;
    }

    private static int PickGuard(DungeonSnapshot s)
    {
        int maxTierCp = 6 + s.tier * 8;
        var pool = new List<int>();
        for (int i = 0; i < MinionCatalog.Count; i++)
            if (MinionCatalog.Get(i).tierCP <= maxTierCp) pool.Add(i);
        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : 0;
    }

    private static int TrapCountOn(DungeonSnapshot s, int floor)
    {
        int n = 0;
        for (int i = 0; i < s.trapFloor.Count; i++) if (s.trapFloor[i] == floor) n++;
        return n;
    }

    /// <summary>
    /// 💥 その階が攻め落とされた。⚠ <b>守りは実際に減って、そのまま残る</b>
    /// （いままでは次に来ると元通りだった）。次のターンに②が拾って厚くしにくる。
    /// </summary>
    public static void OnFloorFallen(int rivalIndex, int floor)
    {
        EnsureInit();
        if (rivalIndex < 0 || rivalIndex >= lostFloorAt.Length) return;
        lostFloorAt[rivalIndex] = floor;
        var s = DungeonOf(rivalIndex);
        if (s == null) return;
        // その階の守りを**全部**消す（抜かれた＝そこは通された）
        for (int i = s.guardFloor.Count - 1; i >= 0; i--)
            if (s.guardFloor[i] == floor)
            { s.guardFloor.RemoveAt(i); s.guardIndex.RemoveAt(i); s.guardLevel.RemoveAt(i); }
        Debug.Log("💥『" + RivalLords.NameOf(rivalIndex) + "の迷宮』" + (floor + 1) + "層の守りが失われた");
    }

    /// <summary>📏 いまの迷宮の手強さ（スコアと表示に使う）。</summary>
    public static float ThreatOf(int rivalIndex)
    {
        var s = DungeonOf(rivalIndex);
        return s != null ? s.ThreatScore() : 0f;
    }
    public static int FloorsOf(int rivalIndex)
    {
        var s = DungeonOf(rivalIndex);
        return s != null ? s.FloorCount : 0;
    }
    public static int GuardsOf(int rivalIndex)
    {
        var s = DungeonOf(rivalIndex);
        return s != null ? s.TotalGuards : 0;
    }
}
