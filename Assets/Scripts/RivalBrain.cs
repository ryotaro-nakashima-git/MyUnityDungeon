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

    /// <summary>
    /// 1ターンに打てる手の数。
    /// ⚠⚠ 最初これを <b>1</b> にしていたが、それは<b>私が勝手に課した縛り</b>だった ――
    ///   プレイヤーは自動運転のログを見ても<b>1ターンに10手ほど</b>打っている。
    ///   1手だけだと、1階ぶんの守りを失ったとき**取り戻すのに何ターンもかかり**、
    ///   DPだけが積み上がって弱っていく（実測：守り 27→9→1・DP 4,306 が遊ぶ）。
    /// </summary>
    private const int ActionsPerTurn = 4;

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
    /// <summary>
    /// 📚 段を1つ上げる（＝プレイヤーの<b>研究</b>にあたる）。上げるほど高い。
    /// ⚠⚠ <b>これが無いと bot は頭打ちになる</b>（実測：層も守りも上限に当たり、40ターンでDPが余った）。
    ///   プレイヤーは研究233ノードという<b>伸び続ける軸</b>を持っているので、
    ///   同じ物を持たせないと 2位のスコアだけが止まる（実測：革新 112 対 462）。
    /// </summary>
    private static float CostTier(int tier) { return 2500f + tier * 1500f; }
    /// ⚠⚠ <b>開始時の段は 3 / 5 / 7</b>（`NestSystem.Build` が `3 + i*2` で建てる）。
    ///   最初これを 5 にしていたので、**アリサとヴェルグは一度も段を上げられなかった**（実測）。
    ///   上限は開始値より上に置くこと。
    private const int MaxTier = 9;
    /// <summary>🔁 段に見合わない古い守りを1体入れ替える。⚠ 枠が埋まったあとのDPの行き先。</summary>
    private const float CostUpgrade = 350f;

    /// <summary>どの階にも最低これだけは立たせる（ここまでは育てるより先）。</summary>
    private const int MinGuardsOn = 2;
    /// <summary>
    /// 💰 次の大きな買い物の何割まで貯まったら、安い手を止めて待つか。
    /// ⚠ これが無いと bot は毎ターン安い守りで使い切り、階層にも段にも永久に届かない（実測）。
    /// </summary>
    private const float SaveThreshold = 0.45f;

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
    /// <summary>
    /// 🕸️ 名声。⚠ <b>人間側の波を凌ぐと増える</b>（段③）＝プレイヤーと同じ増え方。
    ///   ここが無いと bot の「恐怖」は迷宮の手強さしか材料が無く、プレイヤーと単位が合わない。
    /// </summary>
    private static float[] fame;

    private static void EnsureInit()
    {
        int n = RivalLords.Count;
        if (dp == null || dp.Length != n) { dp = new float[n]; }
        if (lastAct == null || lastAct.Length != n) { lastAct = new string[n]; for (int i = 0; i < n; i++) lastAct[i] = ""; }
        if (lostFloorAt == null || lostFloorAt.Length != n) { lostFloorAt = new int[n]; for (int i = 0; i < n; i++) lostFloorAt[i] = -1; }
        if (fame == null || fame.Length != n) { fame = new float[n]; }
    }

    public static void Reset() { dp = null; lastAct = null; lostFloorAt = null; fame = null; EnsureInit(); }

    /// <summary>🕸️ その魔王の名声（段③で凌いだぶん積み上がる）。</summary>
    public static int FameOf(int i) { EnsureInit(); return (i >= 0 && i < fame.Length) ? Mathf.RoundToInt(fame[i]) : 0; }

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
            // 🗡️ **先に人間側の波を受ける**（段③）。凌げば名声、凌げなければ守りを失う。
            //   ⚠ 順番が大事 ―― 受けてから打たせると、失った階を次の手で埋めにいける。
            string hit = ResolveHumanWave(turn, i, snap);
            if (rv.defeated) { lastAct[i] = hit; continue; }
            string acts = "";
            for (int k = 0; k < ActionsPerTurn; k++)
            {
                string a = Act(i, snap);
                if (a == "力を蓄えている") break;      // 打てる手が無い＝これ以上は貯めるだけ
                acts += (acts.Length > 0 ? "／" : "") + a;
            }
            if (acts.Length == 0) acts = "力を蓄えている";
            lastAct[i] = acts + (string.IsNullOrEmpty(hit) ? "" : "　／　" + hit);
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

        // ③ どの階も**最低限**（2体）は立たせる。ここまでは何より先。
        for (int f = 0; f < s.FloorCount; f++)
            if (s.GuardCountOn(f) < MinGuardsOn)
            {
                string r = TryGuard(i, s, f, (f + 1) + "層の守りを最低限まで戻した");
                if (r != null) return r;
                return "力を蓄えている";
            }

        // ⚠⚠ ここから下は「育てる手」。**貯金をさせる。**
        //   最初これを入れていなかったので、bot は毎ターン安い守りで使い切り、
        //   階層(1,200)にも段(13,000)にも**永久に届かなかった**（実測：80ターン経っても 5/6/7層のまま）。
        //   一定まで貯まったら、安い手を打たずに待つ ―― これは**間違えうる判断**でもある
        //   （貯めている最中に攻められれば薄いまま受けることになる）。

        // ④ 迷宮を深くする（貯める）
        if (s.FloorCount < MaxFloors)
        {
            if (dp[i] >= CostFloor)
            {
                dp[i] -= CostFloor;
                int nf = s.FloorCount;
                s.floorSizes.Add(Mathf.Clamp(10 + nf * 4 + s.tier, 10, 30));
                return "迷宮を " + (nf + 1) + " 層まで掘り下げた";
            }
            if (dp[i] >= CostFloor * SaveThreshold) return "力を蓄えている";
        }

        // ⑤ 段を上げる＝プレイヤーの研究にあたる「伸び続ける軸」（貯める）
        if (s.tier < MaxTier)
        {
            float ct = CostTier(s.tier);
            if (dp[i] >= ct) { dp[i] -= ct; s.tier += 1; return "眷属の格を上げた（段 " + s.tier + "）"; }
            if (dp[i] >= ct * SaveThreshold) return "力を蓄えている";
        }

        // ⑥ 余ったぶんで枠を埋める
        for (int f = 0; f < s.FloorCount; f++)
            if (s.GuardCountOn(f) < MaxGuardsOn(f))
            {
                string r = TryGuard(i, s, f, (f + 1) + "層に守りを足した");
                if (r != null) return r;
                break;   // ⚠ DPが足りないなら下の階を見ても同じ
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

        // ⑧ 段に見合わない古い守りを入れ替える ―― 数ではなく質を上げる
        if (dp[i] >= CostUpgrade)
        {
            int worst = -1;
            int maxTierCp = 6 + s.tier * 8;
            for (int g = 0; g < s.guardIndex.Count; g++)
            {
                var d = MinionCatalog.Get(s.guardIndex[g]);
                if (d.tierCP >= maxTierCp - 6) continue;           // もう十分な格
                if (worst < 0 || d.tierCP < MinionCatalog.Get(s.guardIndex[worst]).tierCP) worst = g;
            }
            if (worst >= 0)
            {
                dp[i] -= CostUpgrade;
                s.guardIndex[worst] = PickGuard(s);
                s.guardLevel[worst] = Mathf.Max(s.guardLevel[worst], 1 + s.tier * 3 + s.guardFloor[worst] * 2);
                return (s.guardFloor[worst] + 1) + "層の守りを鍛え直した";
            }
        }

        // ⑨ それでも余る ―― 主を鍛える
        if (dp[i] >= CostLord)
        {
            dp[i] -= CostLord;
            s.lordLevel += 1;
            s.lordHpMult += 0.15f;
            return "自らを鍛えた（Lv" + s.lordLevel + "）";
        }

        return "力を蓄えている";
    }

    // ══ 🗡️ 人間側が bot の迷宮も攻める（段③）══
    // ⚠⚠ <b>これが無いと bot は「増えるだけ」に戻る。</b>失う道と、名声の源がここにしか無い。
    // ⚠ <b>盤は建てない。</b>こちらが見ていない戦闘を実時間で回す必要はないので、抽象的に判定する。
    // ⚠ 使う式は<b>プレイヤーとまったく同じ</b>（`AdventurerAI.WorldTier` / `LevelBase`）。
    //   入れる名声だけが bot 自身のもの ―― 別の式を書くと、そこから難易度が二重になる。

    /// <summary>波1つぶんの力。⚠ プレイヤーの湧きと同じ入力から作る。</summary>
    private static float WavePower(int turn, int i)
    {
        float tier = AdventurerAI.WorldTier(turn, FameOf(i), 1f);
        float lv = AdventurerAI.LevelBase(turn, FameOf(i));
        // ⚠ 最後の係数＝1波の頭数の目安。3.2 にしたら bot が痩せ続けた（守り 39→2）ので 1.8 に。
        //   ⚠ これも当てずっぽう。自動運転で「bot が伸びも痩せもする」ところを探す。
        return (6f + tier * 8f) * (1f + lv * 0.04f) * 1.8f;
    }

    /// <summary>その階の守りの力（`ThreatScore` と同じ数え方を1階ぶんに切ったもの）。</summary>
    private static float FloorPower(DungeonSnapshot s, int floor)
    {
        float p = 0f;
        for (int g = 0; g < s.guardIndex.Count; g++)
        {
            if (s.guardFloor[g] != floor) continue;
            var d = MinionCatalog.Get(s.guardIndex[g]);
            p += d.tierCP * (1f + s.guardLevel[g] * 0.04f);
        }
        // 罠は守りの足し
        for (int t = 0; t < s.trapFloor.Count; t++) if (s.trapFloor[t] == floor) p += 6f;
        // 最下層は主が立つ
        if (floor == s.FloorCount - 1)
            p += MinionCatalog.Get(s.lordIndex).tierCP * s.lordHpMult * 3f;
        return p;
    }

    /// <summary>
    /// 🗡️ 1ターンぶんの波を受ける。上の階から順に、止められなければ**その階の守りを失って**下へ。
    /// ⚠ 全階抜かれたら魔王が討たれる（bot も滅びうる ―― 世界が動く）。
    /// </summary>
    private static string ResolveHumanWave(int turn, int i, DungeonSnapshot s)
    {
        if (s.FloorCount == 0) return "";
        float wave = WavePower(turn, i);
        int broke = 0;
        for (int f = 0; f < s.FloorCount; f++)
        {
            float def = FloorPower(s, f);
            if (def >= wave)
            {
                // 凌いだ。⚠ 名声は**受けた波の大きさ**に応じて（強い波を止めるほど名が上がる）
                fame[i] += wave * 0.06f;
                return broke > 0
                    ? "人間側に " + broke + "層まで通したが止めた（名声 " + Mathf.RoundToInt(wave * 0.06f) + "）"
                    : "人間側の波を凌いだ（名声 +" + Mathf.RoundToInt(wave * 0.06f) + "）";
            }
            wave -= def;
            broke++;
            // ⚠⚠ <b>通された＝全滅ではない。</b>最初は `OnFloorFallen`（全部消す）を呼んでいたが、
            //   それは<b>こちらが遠征で実際に殲滅したとき</b>の話。人間側の抽象判定で毎ターン
            //   1階まるごと消すと、bot は取り戻せずに痩せ続ける（実測：守り 39→2）。
            //   ここでは**半分を失う**。
            LoseSome(i, f, 0.5f);
        }
        // 全部抜かれた ―― 主が討たれた
        fame[i] = Mathf.Max(0f, fame[i] - 200f);
        var rv = RivalLords.Get(i);
        rv.defeated = true;
        Debug.Log("💀『" + RivalLords.NameOf(i) + "』人間側に迷宮を抜かれて討たれた（T" + turn + "）");
        NotifySystem.Push("<b>" + RivalLords.NameOf(i) + "</b> が人間側に討たれた", NotifySystem.Kind.Story);
        return "討たれた";
    }

    /// <summary>💢 その階の守りを一定割合だけ失う（人間側に通されたとき）。</summary>
    private static void LoseSome(int i, int floor, float frac)
    {
        EnsureInit();
        var s = DungeonOf(i);
        if (s == null) return;
        var on = new List<int>();
        for (int g = 0; g < s.guardFloor.Count; g++) if (s.guardFloor[g] == floor) on.Add(g);
        int lose = Mathf.Max(1, Mathf.RoundToInt(on.Count * frac));
        for (int k = on.Count - 1; k >= 0 && lose > 0; k--)
        {
            int g = on[k];
            s.guardFloor.RemoveAt(g); s.guardIndex.RemoveAt(g); s.guardLevel.RemoveAt(g);
            lose--;
        }
        lostFloorAt[i] = floor;
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
