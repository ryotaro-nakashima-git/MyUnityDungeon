using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🏘️ <b>人類の集落</b> ── 地上にいる「人間側」の実体（③地上の作り直し）。
///
/// ⚠⚠ <b>作り直す前の姿（実測）。</b>
///   - 「人類側」という勢力は<b>コードの中に存在していなかった</b>。あるのは中立タイルと、
///     `EnemyForce.SpawnHuman` が<b>自領に隣接する中立タイルからランダムに湧かせる匿名の奪還軍</b>だけ。
///   - 敵は「一番守りの薄い<b>1タイル</b>」を狙い、勝つと<b>そのタイル1枚</b>の持ち主を書き換えていた
///     ＝<b>虫食い</b>。都市も村も関係なく端から削られる。
///   - `RegionType.Village/Town/City` は地形と深度から決まる<b>ただの呼び名</b>で、中身が無かった。
///
/// <b>Civ VII の実仕様に合わせた形</b>
///   ① <b>タイルの所有は集落に属する。</b>国境は中心から最大3ヘクス。1枚ずつ取り合うものではない。
///   ② 集落は<b>城砦区画をすべて翻して初めて陥落</b>する。中心は常に城砦区画。
///      町は中心にしか城壁が無いので落ちやすく、都市は区画の数だけ手数がかかる。
///   ③ ユニットは<b>集落で生産して湧く</b>。独立勢力は中心をほとんど動かない守備兵で固め、
///      <b>敵対しているものだけ</b>が軍を送る。
///
/// <b>この層の決まり</b>
///   - 版図タイルは<b>攻めても取れない</b>（攻撃＝略奪＝産出を止めるだけ）。
///   - <b>中心を落とすと、その集落の版図が丸ごと移る。</b>
///   - <b>無主の荒野</b>は今までどおり、拠点を建てて国境が伸びることで1枚ずつ増える
///     （＝こちらの拡張の作法は何も変えない）。
///
/// 実体は <see cref="DiplomacySystem.Power"/> に相乗りしている（盤に散る自治都市がまさにこの役）。
/// 純static・実行時保持。関連: [[SurfaceMap]] [[SettlementSystem]] [[EnemyForce]] [[DiplomacySystem]]。
/// </summary>
public static class HumanRealm
{
    // ============ 格 ============
    public const int Village = 0, Town = 1, City = 2;

    /// <summary>版図の半径。⚠ Civ VII の「中心から最大3ヘクス」に合わせてある。</summary>
    public static int RadiusOf(int grade) => grade >= City ? 3 : grade >= Town ? 2 : 1;
    public static string GradeName(int grade) => grade >= City ? "都市" : grade >= Town ? "町" : "村";

    /// <summary>
    /// 🏯 城砦区画の数（Civ VII）。<b>全部落として初めて陥落</b>する。
    /// 中心は常に1つ。都市はさらに版図の中に区画を持つので、<b>落とすのに手数がかかる</b>。
    /// </summary>
    public static int FortifiedDistricts(int grade) => grade >= City ? 3 : grade >= Town ? 2 : 1;

    /// <summary>同時に持てる守備の数。</summary>
    public static int GarrisonCap(int grade) => grade >= City ? 4 : grade >= Town ? 2 : 1;

    /// <summary>兵を1体出すのに要るゲージ（格が上がるほど速い）。</summary>
    public static int MusterNeed(int grade) => grade >= City ? 3 : grade >= Town ? 4 : 6;

    /// <summary>👁️ 警戒圏＝版図の外に哨戒が出る距離。⚠ 敵対していない集落は<b>ここから出ない</b>。</summary>
    public static int WatchRing(int grade) => grade >= City ? 2 : 1;

    // ============ 態度 ============
    public const int Indifferent = 0, Wary = 1, Hostile = 2;
    public static string PostureName(int p) => p >= Hostile ? "敵対" : p >= Wary ? "警戒" : "無関心";
    public static string PostureColor(int p) => p >= Hostile ? "#e05a5a" : p >= Wary ? "#e3a94a" : "#9c95b4";

    public static string NameOf(int index)
    {
        var p = At(index);
        return p == null ? "人類の集落" : p.name;
    }
    /// <summary>色は態度で変わる（敵対＝赤）。盤の上で「どこが動き出したか」が一目で分かるように。</summary>
    public static string ColorOf(int index)
    {
        var p = At(index);
        return p == null ? "#c9c2d8" : p.posture >= Hostile ? "#d0603c" : p.posture >= Wary ? "#c99a4a" : "#b9b2c8";
    }

    public static int Count => DiplomacySystem.Powers.Count;
    public static DiplomacySystem.Power At(int index)
    {
        var l = DiplomacySystem.Powers;
        return (index >= 0 && index < l.Count) ? l[index] : null;
    }
    public static int IndexOfRegion(int regionId)
    {
        var l = DiplomacySystem.Powers;
        for (int i = 0; i < l.Count; i++) if (!l[i].destroyed && l[i].regionId == regionId) return i;
        return -1;
    }
    /// <summary>そのタイルを版図に持つ集落（-1＝どこにも属さない＝無主の荒野）。</summary>
    public static int RealmOfTile(int regionId)
    {
        var r = SurfaceMap.Get(regionId);
        return r != null && r.IsHuman ? r.HumanIndex : -1;
    }

    // ============ 🌍 盤に据える ============
    /// <summary>
    /// 集落に格と版図を与える。⚠ <b>盤を作った直後と、セーブを読んだ直後に呼ぶ。</b>
    /// 何度呼んでも同じ結果になる（既に版図があるなら触らない）。
    /// </summary>
    public static void EnsureSeeded()
    {
        var l = DiplomacySystem.Powers;
        if (l == null || l.Count == 0) return;
        bool any = false;
        for (int i = 0; i < l.Count; i++) if (l[i].grade > 0) { any = true; break; }

        for (int i = 0; i < l.Count; i++)
        {
            var p = l[i];
            if (p.destroyed) continue;
            var c = SurfaceMap.Get(p.regionId);
            if (c == null || c.isOcean) continue;

            if (!any)
            {
                // 🏙️ 格は**盤が決めた地形の呼び名**から取る（City/Town タイルにしか置かれていない）。
                //    ⚠ ランダムにしない。地名（〜городの町／〜都市）と格が食い違うと盤が読めなくなる。
                p.grade = c.type == SurfaceMap.RegionType.City ? City
                        : c.type == SurfaceMap.RegionType.Town ? Town : Village;
                p.posture = Indifferent;
                p.muster = 0;
            }
            ClaimTerritory(i);
        }
        if (!any) Debug.Log("🏘️『人類の集落』" + l.Count + "つに版図を与えた（村1／町2／都市3ヘクス）");
    }

    /// <summary>
    /// その集落の版図を盤に描く。⚠ <b>自分と他魔王の土地は取らない</b>
    /// （生成のたびに人類が版図を広げ返すと、こちらの拡張が巻き戻される）。
    /// </summary>
    public static void ClaimTerritory(int index)
    {
        var p = At(index); if (p == null || p.destroyed) return;
        var c = SurfaceMap.Get(p.regionId); if (c == null) return;
        int rad = RadiusOf(p.grade);
        int mine = SurfaceMap.OwnerHumanBase + index;
        c.owner = mine;
        foreach (var n in SurfaceMap.WithinRange(p.regionId, rad))
        {
            if (n.isOcean || n.id == p.regionId) continue;
            if (n.type == SurfaceMap.RegionType.Gate) continue;       // 🏯 迷宮の入口は誰にも渡さない
            if (n.owner != SurfaceMap.OwnerNeutral) continue;          // 既に主のいる土地は奪わない
            n.owner = mine;
        }
    }

    /// <summary>その集落の版図タイル（中心を含む）。</summary>
    public static List<SurfaceMap.Region> TilesOf(int index)
    {
        var list = new List<SurfaceMap.Region>();
        int mine = SurfaceMap.OwnerHumanBase + index;
        foreach (var r in SurfaceMap.All) if (r.owner == mine) list.Add(r);
        return list;
    }

    // ============ 🔥 陥落と略奪 ============
    /// <summary>
    /// そのタイルが「攻めれば取れる」か。
    /// ⚠⚠ <b>人類の版図タイルは false。</b>中心を落とさない限り取れない ―― これが虫食いを止める要。
    /// </summary>
    public static bool IsCapturable(SurfaceMap.Region r)
    {
        if (r == null) return false;
        if (!r.IsHuman) return true;                 // 荒野・他魔王領は今までどおり
        return IndexOfRegion(r.id) >= 0;             // 人類の土地は**中心だけ**
    }

    /// <summary>
    /// 🔥 版図タイルへの攻撃＝<b>略奪</b>。取れないが、産出を止めて集落を弱らせる。
    /// ⚠ 略奪は<b>敵対を招く</b>（Civ で領土を荒らせば当然そうなる）。
    /// </summary>
    public static void Pillage(int regionId, string byWhom)
    {
        int idx = RealmOfTile(regionId);
        var p = At(idx); if (p == null) return;
        p.pillaged++;
        SetPosture(idx, Hostile, byWhom + " に版図を荒らされた");
        var r = SurfaceMap.Get(regionId);
        Debug.Log("🔥『略奪』" + byWhom + " が " + r.name + " を荒らした（" + p.name + " の産出が止まる）");
        NotifySystem.Push("<b>" + r.name + "</b> を略奪した ― " + p.name + " の産出を止めた（<b>敵対された</b>）",
            NotifySystem.Kind.Gain, regionId);
    }

    /// <summary>
    /// 🏯 中心を落とした ―― <b>版図が丸ごと移る</b>。
    /// ⚠ Civ VII と同じで、都市は城砦区画を全部落とすまで陥落しない。
    ///   ここでは「1回落とすごとに区画が1つ減り、0になって初めて陥落」という形にしてある
    ///   （＝都市攻めは数ターンかかる）。返り値＝<b>本当に陥落したか</b>。
    /// </summary>
    public static bool StrikeCenter(int regionId, int newOwner, string byWhom)
    {
        int idx = IndexOfRegion(regionId);
        var p = At(idx); if (p == null) return false;
        var c = SurfaceMap.Get(regionId);

        p.grade = Mathf.Max(0, p.grade);
        int left = FortifiedDistricts(p.grade) - p.wallsBroken;
        if (left > 1)
        {
            // まだ城砦区画が残っている＝落ちない。1つ削って次のターンへ。
            p.wallsBroken++;
            SetPosture(idx, Hostile, byWhom + " に城砦を破られた");
            Debug.Log("🏯『城砦を1つ破った』" + p.name + "（残り " + (left - 1) + " 区画）");
            NotifySystem.Push("<b>" + p.name + "</b> の城砦を1つ破った ― <b>残り " + (left - 1) + " 区画</b>",
                NotifySystem.Kind.Gain, regionId);
            return false;
        }

        // 🏳️ 陥落。版図が丸ごと新しい主のものになる。
        var tiles = TilesOf(idx);
        p.destroyed = true; p.suzerain = -1; p.stage = 0; p.posture = Indifferent;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].id == regionId) continue;
            SurfaceMap.SetOwner(tiles[i].id, newOwner);
        }
        SurfaceMap.SetOwner(regionId, newOwner);
        // 🧹 その集落が出していた兵は主を失う
        EnemyForce.DisbandOf(idx);
        Debug.Log("🏳️『陥落』" + byWhom + " が " + p.name + "（" + GradeName(p.grade) + "）を落とした ― 版図 "
            + tiles.Count + " タイルがまとめて移った");
        NotifySystem.Push("<b>" + p.name + " が陥落</b> ― 版図 <b>" + tiles.Count + " タイル</b>がまとめて手に入った",
            NotifySystem.Kind.Gain, regionId);
        return true;
    }

    // ============ 🎭 態度 ============
    public static void SetPosture(int index, int posture, string why)
    {
        var p = At(index); if (p == null || p.destroyed) return;
        if (posture <= p.posture) return;                 // 一度上がった態度は自然には下がらない
        int before = p.posture;
        p.posture = posture;
        if (posture >= Hostile && p.hostileSince < 0)
        {
            p.hostileSince = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 0;
            // ⚠ **敵対は宣言されて表に出る。** これまでの「いつの間にか奪られている」の正反対にする。
            Debug.Log("⚔️『敵対』" + p.name + "（" + GradeName(p.grade) + "）がこちらを敵と定めた ― " + why);
            NotifySystem.Push("<b>" + p.name + " が敵対を宣言</b>（" + GradeName(p.grade) + "）― " + why
                + "。ここを拠点に軍を出してくる", NotifySystem.Kind.Danger, p.regionId);
        }
        else if (posture >= Wary && before < Wary)
        {
            Debug.Log("👁️『警戒』" + p.name + " がこちらを警戒しはじめた ― " + why);
            NotifySystem.Push("<b>" + p.name + "</b> が警戒を強めた ― " + why, NotifySystem.Kind.Info, p.regionId);
        }
    }

    /// <summary>
    /// 毎ターン、態度を見直す。
    /// ⚠ <b>こちらの版図が隣接した／名声が高い</b>だけでは「警戒」まで。敵対には理由が要る
    ///   （略奪されたか、追い詰められたか）。理由なく攻めてこないのが虫食いを止めるもう半分。
    /// </summary>
    public static void TickPostures(int turn)
    {
        var l = DiplomacySystem.Powers;
        for (int i = 0; i < l.Count; i++)
        {
            var p = l[i];
            if (p.destroyed) continue;
            if (p.suzerain == 0) { p.posture = Indifferent; continue; }   // 🤝 従属した相手は敵にならない

            if (p.posture < Wary)
            {
                bool touching = false;
                foreach (var n in SurfaceMap.Neighbors(p.regionId)) if (n.owned) { touching = true; break; }
                if (!touching)
                {
                    foreach (var t in TilesOf(i))
                    {
                        foreach (var n in SurfaceMap.Neighbors(t.id)) if (n.owned) { touching = true; break; }
                        if (touching) break;
                    }
                }
                if (touching) SetPosture(i, Wary, "こちらの版図が国境に届いた");
            }
        }
    }

    /// <summary>
    /// 🏯 <b>敵対する集落が1つも無いとき、一番近い集落が敵対する。</b>
    /// ⚠ これが無いと「地上を全部失ったほうが安全」に戻る（S-3 で塞いだ穴）。
    ///   以前は奪還軍の狙いを迷宮に向けることで塞いでいたが、軍が集落から出る形になったので、
    ///   <b>出どころのほうを立てる</b>形に置き換える。
    /// </summary>
    public static void EnsureSomeoneHostile(int turn)
    {
        var l = DiplomacySystem.Powers;
        int alive = 0;
        for (int i = 0; i < l.Count; i++)
        {
            if (l[i].destroyed || l[i].suzerain == 0) continue;
            alive++;
            if (l[i].posture >= Hostile) return;   // 既に誰か敵対している
        }
        if (alive == 0) return;
        int gate = SurfaceMap.GateId; if (gate < 0) return;
        var g = SurfaceMap.Get(gate);
        int best = -1, bestD = int.MaxValue;
        for (int i = 0; i < l.Count; i++)
        {
            if (l[i].destroyed || l[i].suzerain == 0) continue;
            var c = SurfaceMap.Get(l[i].regionId); if (c == null) continue;
            int d = SurfaceMap.HexDist(c, g);
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best >= 0) SetPosture(best, Hostile, "迷宮を討つべしという声が上がった");
    }

    // ============ ⚔️ 兵を出す ============
    /// <summary>
    /// 毎ターン、集落が兵を蓄える。⚠ <b>湧く場所は必ず集落の中心。</b>
    /// これまでの `EnemyForce.SpawnHuman`（自領に隣接する中立タイルからランダム）を置き換える。
    /// </summary>
    public static void TickMuster(int turn)
    {
        var l = DiplomacySystem.Powers;
        // ⚠⚠ **近い集落から順に兵を出させる。**
        //   リストの順のままだと、進軍の枠（`EnemyForce.MaxMarching`）を
        //   **遠い集落が先に埋めてしまう**。実測：迷宮から18ヘクスの集落が敵対しているのに、
        //   進軍していたのは 48／31／36 ヘクスの3つで、近い脅威が一度も動かなかった。
        int gate = SurfaceMap.GateId;
        var order = new List<int>();
        for (int i = 0; i < l.Count; i++) order.Add(i);
        if (gate >= 0)
        {
            var g = SurfaceMap.Get(gate);
            order.Sort((x, y) =>
            {
                var cx = SurfaceMap.Get(l[x].regionId); var cy = SurfaceMap.Get(l[y].regionId);
                int dx = cx == null ? 9999 : SurfaceMap.HexDist(cx, g);
                int dy = cy == null ? 9999 : SurfaceMap.HexDist(cy, g);
                return dx.CompareTo(dy);
            });
        }

        for (int k = 0; k < order.Count; k++)
        {
            int i = order[k];
            var p = l[i];
            if (p.destroyed || p.suzerain == 0) continue;
            if (p.posture < Wary) continue;                     // 無関心な集落は兵も蓄えない
            int cap = GarrisonCap(p.grade);
            if (EnemyForce.CountOfRealm(i) >= cap) continue;
            p.muster++;
            if (p.muster < MusterNeed(p.grade)) continue;
            p.muster = 0;
            EnemyForce.SpawnFromRealm(i);
        }
    }

    /// <summary>UIの1行（盤のツールチップと『情勢』で使う）。</summary>
    public static string Line(int index)
    {
        var p = At(index); if (p == null) return "";
        if (p.destroyed) return "<color=#6f6889>" + p.name + "（滅亡）</color>";
        return "<color=" + ColorOf(index) + ">" + p.name + "</color> <size=88%>" + GradeName(p.grade)
             + "・<color=" + PostureColor(p.posture) + ">" + PostureName(p.posture) + "</color>"
             + "・城砦" + Mathf.Max(0, FortifiedDistricts(p.grade) - p.wallsBroken) + "/" + FortifiedDistricts(p.grade)
             + "</size>";
    }
}
