using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⛏️ **迷宮の掘削**（通路を塞ぐ／掘る）。
///
/// <para>
/// ⚠⚠ **ここは「タイルを1枚ずつ描かせない」ことが設計の第一条件。**
/// この作品はもともと手動タイル配置だったが、**作業感が強すぎて遊んでいて楽しくなかった**ので
/// 自動生成に切り替えた経緯がある（迷宮は生成器が作る）。同じ失敗を繰り返さないために：
/// </para>
///
/// <list type="number">
/// <item>**クリック数＝判断の数**にする。塞ぐは1クリックで<b>通路の区間まるごと</b>、
///   掘るは2クリックで<b>その間の道を自動で</b>。プレイヤーは意図だけを言い、線は game が引く。</item>
/// <item>**1ターンの回数を絞る**（既定3回・研究で5回）。無制限だと「盤を描き直す作業」に戻る。</item>
/// <item>**結果を数字で即返す**（<see cref="PathLength"/>＝入口から階段までの道のり）。
///   「14 → 21 マス」と出るから、実験になる。出ないなら、ただの落書きになる。</item>
/// </list>
///
/// <para>
/// **何のために掘るのか**：塞げば<b>道のりが伸びる</b>＝制限時間の中で敵が奥へ届きにくく、
/// 罠と配下に晒される時間が増える。掘れば<b>袋小路</b>を作って誘導宝箱を置ける（探索者が
/// 寄り道して満足し、奥まで来ずに帰る）。どちらも「盤の形そのもの」で戦う手。
/// </para>
///
/// 関連: [[DungeonFloorManager]]（編集は `fd.map` に書き戻さないと消える）／[[pit-and-descent]]。
/// </summary>
public static class Excavation
{
    /// <summary>1ターンに何回まで手を入れられるか。⚠ **const にしない**（研究で伸びる）。</summary>
    public static int OpsPerTurn { get { return (ResearchState.IsResearched("d_excavate2") ? 5 : 3) + IncidentSystem.ExtraExcavateOps; } }
    public static bool Unlocked { get { return ResearchState.IsResearched("d_excavate"); } }

    /// <summary>このターンに使った回数。⚠ static の値なのでセーブに載る。</summary>
    private static int usedThisTurn;
    public static int Remaining { get { return Mathf.Max(0, OpsPerTurn - usedThisTurn); } }
    public static void OnTurnStart() { usedThisTurn = 0; }
    public static void Reset() { usedThisTurn = 0; pendingDig = NoCell; }

    public const int SealCostPerTile = 60;   // 塞ぐ：1マスあたり
    public const int DigCostPerTile = 110;   // 掘る：1マスあたり（塞ぐより高い＝縮めるのは贅沢）

    private static readonly Vector2Int NoCell = new Vector2Int(-9999, -9999);
    private static Vector2Int pendingDig = NoCell;
    public static bool AwaitingDigTarget { get { return pendingDig.x > -9999; } }
    public static Vector2Int PendingDigFrom { get { return pendingDig; } }
    public static void CancelPendingDig()
    {
        if (!AwaitingDigTarget) return;
        pendingDig = NoCell;
        NotifySystem.Push("掘る先の指定をやめた", NotifySystem.Kind.Info);
    }

    private static DungeonGridSystem Grid { get { return Object.FindFirstObjectByType<DungeonGridSystem>(); } }

    // ============ 📏 道のり（この機能の手応えそのもの） ============

    /// <summary>
    /// 入口から階段（最下層なら魔王の間）までの最短の道のり。届かないなら -1。
    /// ⚠ これを出さないと、掘削は「線を引くだけ」の作業になる。
    /// </summary>
    public static int PathLength()
    {
        var g = Grid; if (g == null) return -1;
        return Distance(g, g.EntranceCell, g.BossCell);
    }

    /// <summary>
    /// 入口→階段の道のり。`blocked` を壁として扱い、`opened` を床として扱う（**盤を触らずに**先読みする）。
    /// ⚠ 先読みのために本当にタイルを置き換えてはいけない。`StampTile` は GameObject を作り直して
    ///   タイルマップを描き直すので、カーソルを動かすたびに盤がちらつく。
    /// </summary>
    public static int PathLengthWith(HashSet<Vector2Int> blocked, HashSet<Vector2Int> opened)
    {
        var g = Grid; if (g == null) return -1;
        return Distance(g, g.EntranceCell, g.BossCell, blocked, opened);
    }

    private static int Distance(DungeonGridSystem g, Vector2Int from, Vector2Int to,
        HashSet<Vector2Int> blocked = null, HashSet<Vector2Int> opened = null)
    {
        int size = g.CurrentPlayableSize;
        if (!Walkable(g, from, blocked, opened)) return -1;
        var dist = new int[size, size];
        for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) dist[x, y] = -1;
        var q = new Queue<Vector2Int>();
        q.Enqueue(from); dist[from.x, from.y] = 0;
        var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            if (c == to) return dist[c.x, c.y];
            foreach (var d in dirs)
            {
                int nx = c.x + d.x, ny = c.y + d.y;
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                if (dist[nx, ny] >= 0) continue;
                if (!Walkable(g, new Vector2Int(nx, ny), blocked, opened)) continue;
                dist[nx, ny] = dist[c.x, c.y] + 1;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }
        return -1;
    }

    private static bool Walkable(DungeonGridSystem g, Vector2Int c, HashSet<Vector2Int> blocked, HashSet<Vector2Int> opened)
    {
        if (blocked != null && blocked.Contains(c)) return false;
        if (opened != null && opened.Contains(c)) return true;
        return g.GetTileType(c.x, c.y) != DungeonGridSystem.TileType.None;
    }

    private static int FloorNeighbors(DungeonGridSystem g, Vector2Int c)
    {
        int n = 0;
        if (g.GetTileType(c.x + 1, c.y) != DungeonGridSystem.TileType.None) n++;
        if (g.GetTileType(c.x - 1, c.y) != DungeonGridSystem.TileType.None) n++;
        if (g.GetTileType(c.x, c.y + 1) != DungeonGridSystem.TileType.None) n++;
        if (g.GetTileType(c.x, c.y - 1) != DungeonGridSystem.TileType.None) n++;
        return n;
    }

    // ============ 🧱 塞ぐ（1クリック＝通路の区間まるごと） ============

    /// <summary>
    /// クリックしたマスを含む**通路の区間**を返す（分岐や部屋に当たるまで）。
    /// 分岐そのものを押した場合はそのマス1つだけ。
    /// ⚠ 1マスずつ塞がせない理由：それは「描く作業」であって判断ではない。
    /// </summary>
    public static List<Vector2Int> SegmentAt(Vector2Int start)
    {
        var g = Grid; var list = new List<Vector2Int>();
        if (g == null) return list;
        if (g.GetTileType(start.x, start.y) == DungeonGridSystem.TileType.None) return list;
        list.Add(start);
        if (FloorNeighbors(g, start) != 2) return list;    // 分岐・行き止まり・部屋の中は1マスだけ

        var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
        var seen = new HashSet<Vector2Int>(); seen.Add(start);
        foreach (var d0 in dirs)
        {
            var c = start + d0;
            var prev = start;
            while (g.GetTileType(c.x, c.y) != DungeonGridSystem.TileType.None
                   && FloorNeighbors(g, c) == 2 && !seen.Contains(c))
            {
                seen.Add(c); list.Add(c);
                Vector2Int next = c;
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (n == prev) continue;
                    if (g.GetTileType(n.x, n.y) != DungeonGridSystem.TileType.None) { next = n; break; }
                }
                if (next == c) break;
                prev = c; c = next;
            }
        }
        return list;
    }

    public static int SealCost(Vector2Int cell) { return SegmentAt(cell).Count * SealCostPerTile; }

    public static bool TrySeal(Vector2Int cell, out string why)
    {
        why = "";
        var g = Grid; if (g == null) { why = "盤が無い"; return false; }
        if (!Guard(out why)) return false;

        var seg = SegmentAt(cell);
        if (seg.Count == 0) { why = "そこは既に壁"; return false; }

        // 塞いではいけないマス
        var fm = DungeonFeatureManager.Instance;
        foreach (var c in seg)
        {
            if (c == g.EntranceCell) { why = "入口は塞げない"; return false; }
            if (c == g.BossCell) { why = "階段（最深部）は塞げない"; return false; }
            if (c == g.DemonLordCell) { why = "魔王の間は塞げない"; return false; }
            if (fm != null && fm.HasFeatureAt(c)) { why = "配置したものがある（先に撤去）"; return false; }
        }

        int cost = seg.Count * SealCostPerTile;
        var res = DungeonResourceManager.Instance;
        if (res != null && res.DungeonPoints < cost) { why = "DP不足（要" + cost + "）"; return false; }

        // ⚠⚠ **塞いだあとに道が残ることを必ず確かめる。** 残らないと冒険者が永遠に到達できず、
        //   波が終わらない（制限時間まで棒立ち）＝ゲームが壊れる。
        int before = PathLength();
        int after = PathLengthWith(new HashSet<Vector2Int>(seg), null);
        if (after < 0) { why = "そこを塞ぐと階段まで辿り着けなくなる"; return false; }

        foreach (var c in seg) g.StampTile(c.x, c.y, DungeonGridSystem.TileType.None);
        if (res != null) res.TrySpendDP(cost);
        Commit();
        Report("塞いだ", seg.Count, before, after, cost);
        return true;
    }

    // ============ ⛏️ 掘る（2クリック＝その間の道を自動で） ============

    public static bool BeginDig(Vector2Int from, out string why)
    {
        why = "";
        var g = Grid; if (g == null) { why = "盤が無い"; return false; }
        if (!Guard(out why)) return false;
        if (g.GetTileType(from.x, from.y) == DungeonGridSystem.TileType.None) { why = "掘り始めは床のマスから"; return false; }
        pendingDig = from;
        NotifySystem.Push("<b>掘り抜く先</b>のマスをクリック（壁を最短で抜いて道を通します）", NotifySystem.Kind.Story);
        return true;
    }

    /// <summary>掘り抜ける長さの上限（マス）。一手で盤を横断させない。</summary>
    public const int MaxDigLength = 14;

    /// <summary>
    /// 掘る経路＝**L字にまっすぐ掘り抜く**（横→縦／縦→横の、壁が少ない方）。返すのは削る壁だけ。
    ///
    /// ⚠⚠ 最初は「壁の枚数が最小になる道」をダイクストラで探していたが、**それは間違いだった**。
    ///   既にどこか遠回りで繋がっていると壁0枚の道が見つかり、「掘る」が何も起きない
    ///   （実測：`既に道が通っている` としか出なかった）。掘るのは**新しい近道を作る**行為なので、
    ///   遠回りが在るかどうかとは無関係に、指した2点を素直に貫く。
    /// ⚠ L字なら結果が目で読める。曲がりくねった最適路は、先読みの色を見ても何が起きるか分からない。
    /// </summary>
    public static List<Vector2Int> DigPath(Vector2Int from, Vector2Int to)
    {
        var g = Grid; if (g == null) return null;
        int size = g.CurrentPlayableSize;
        if (to.x < 0 || to.y < 0 || to.x >= size || to.y >= size) return null;
        if (Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y) > MaxDigLength) return null;

        var a = Route(g, from, to, true);
        var b = Route(g, from, to, false);
        if (a == null) return b;
        if (b == null) return a;
        return CountWalls(g, a) <= CountWalls(g, b) ? a : b;
    }

    /// <summary>L字の1本（`horizFirst` で曲がり方を変える）。通ったマスのうち**壁だけ**を返す。</summary>
    private static List<Vector2Int> Route(DungeonGridSystem g, Vector2Int from, Vector2Int to, bool horizFirst)
    {
        var walls = new List<Vector2Int>();
        int x = from.x, y = from.y;
        int sx = to.x > x ? 1 : -1, sy = to.y > y ? 1 : -1;
        if (horizFirst)
        {
            while (x != to.x) { x += sx; Add(g, walls, x, y); }
            while (y != to.y) { y += sy; Add(g, walls, x, y); }
        }
        else
        {
            while (y != to.y) { y += sy; Add(g, walls, x, y); }
            while (x != to.x) { x += sx; Add(g, walls, x, y); }
        }
        return walls;
    }
    private static void Add(DungeonGridSystem g, List<Vector2Int> walls, int x, int y)
    {
        if (g.GetTileType(x, y) == DungeonGridSystem.TileType.None) walls.Add(new Vector2Int(x, y));
    }
    private static int CountWalls(DungeonGridSystem g, List<Vector2Int> w) { return w.Count; }

    public static bool TryFinishDig(Vector2Int to, out string why)
    {
        why = "";
        if (!AwaitingDigTarget) { why = "掘り始めが無い"; return false; }
        var g = Grid; if (g == null) { why = "盤が無い"; return false; }
        if (!Guard(out why)) return false;

        var from = pendingDig;
        var walls = DigPath(from, to);
        if (walls == null) { why = "遠すぎる（一度に " + MaxDigLength + " マスまで）"; return false; }
        if (walls.Count == 0) { why = "そこは既に地続き（掘る壁が無い）"; return false; }

        int cost = walls.Count * DigCostPerTile;
        var res = DungeonResourceManager.Instance;
        if (res != null && res.DungeonPoints < cost) { why = "DP不足（要" + cost + "／" + walls.Count + "マス）"; return false; }
        if (res != null) res.TrySpendDP(cost);

        int before = PathLength();
        foreach (var c in walls) g.StampTile(c.x, c.y, DungeonGridSystem.TileType.Corridor);
        int after = PathLength();
        pendingDig = NoCell;
        Commit();
        Report("掘った", walls.Count, before, after, cost);
        return true;
    }

    // ============ 共通 ============

    // ============ 👀 先読み（クリックする前に結果が見える） ============
    //
    // ⚠⚠ **ここがこの機能の生命線。** 結果が見えないなら、掘削はただの「線を描く作業」に戻る。
    //   「ここを塞ぐと 道のり 8 → 14」と出るから、盤を読む遊びになる。

    /// <summary>いまカーソルがある場所で何が起きるかの1行。ツールが選ばれていないときは空。</summary>
    public static string Preview(int toolMode, Vector2Int cell, out List<Vector2Int> highlight)
    {
        highlight = null;
        var g = Grid; if (g == null || !Unlocked) return "";
        if (AwaitingDigTarget)
        {
            var walls = DigPath(pendingDig, cell);
            if (walls == null) return "<color=#e05a5a>遠すぎる（一度に " + MaxDigLength + " マスまで）</color>";
            highlight = walls;
            if (walls.Count == 0) return "<color=#9c95b4>そこは既に地続き（掘る壁が無い）</color>";
            int cost0 = walls.Count * DigCostPerTile;
            int after0 = PathLengthWith(null, new HashSet<Vector2Int>(walls));
            return "掘る <b>" + walls.Count + "</b> マス　-" + cost0 + "DP　道のり <b>"
                + PathLength() + " → " + (after0 < 0 ? "?" : after0.ToString()) + "</b>";
        }
        if (toolMode == 14)   // 塞ぐ
        {
            var seg = SegmentAt(cell);
            if (seg.Count == 0) return "<color=#9c95b4>そこは既に壁</color>";
            highlight = seg;
            var fm2 = DungeonFeatureManager.Instance;
            foreach (var c in seg)
            {
                if (c == g.EntranceCell) return "<color=#e05a5a>入口は塞げない</color>";
                if (c == g.BossCell) return "<color=#e05a5a>階段は塞げない</color>";
                if (c == g.DemonLordCell) return "<color=#e05a5a>魔王の間は塞げない</color>";
                if (fm2 != null && fm2.HasFeatureAt(c)) return "<color=#e05a5a>配置したものがある</color>";
            }
            int after1 = PathLengthWith(new HashSet<Vector2Int>(seg), null);
            if (after1 < 0) return "<color=#e05a5a>塞ぐと階段まで辿り着けなくなる</color>";
            int b1 = PathLength();
            string arrow = after1 > b1 ? "<color=#6ecf8e>" + b1 + " → " + after1 + "</color>"
                         : after1 < b1 ? "<color=#e05a5a>" + b1 + " → " + after1 + "</color>"
                         : "<color=#9c95b4>" + b1 + " → " + after1 + "（変わらない）</color>";
            return "塞ぐ <b>" + seg.Count + "</b> マス　-" + (seg.Count * SealCostPerTile) + "DP　道のり " + arrow;
        }
        if (toolMode == 15)   // 掘る（始点を選ぶところ）
        {
            if (g.GetTileType(cell.x, cell.y) == DungeonGridSystem.TileType.None)
                return "<color=#9c95b4>掘り始めは床のマスから（次に掘り抜く先を選びます）</color>";
            return "ここから掘り始める（次のクリックで<b>掘り抜く先</b>を選ぶ）";
        }
        return "";
    }

    private static bool Guard(out string why)
    {
        why = "";
        if (!Unlocked) { why = "研究『掘削』が要る"; return false; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { why = "戦闘中は掘れない"; return false; }
        if (Remaining <= 0) { why = "このターンの工事はもう終わり（残り0）"; return false; }
        return true;
    }

    /// <summary>
    /// ⚠⚠ 編集を `FloorData.map` に書き戻す。**これを忘れると、階を切り替えた瞬間に工事が消える**
    ///   （`ActivateFloor` が `fd.map` から盤を作り直すため）。
    /// </summary>
    private static void Commit()
    {
        usedThisTurn++;
        EurekaTracker.OnExcavate();
        var fmgr = DungeonFloorManager.Instance;
        if (fmgr != null) fmgr.WriteBackCurrentMap();
        SoundSystem.Play(SoundSystem.Sfx.Place);
    }

    /// <summary>📏 手応えの1行。**道のりがどう変わったか**を必ず出す。</summary>
    private static void Report(string verb, int tiles, int before, int after, int cost)
    {
        string road = (before >= 0 && after >= 0 && before != after)
            ? "　道のり <b>" + before + " → " + after + "</b> マス"
            : (after >= 0 ? "　道のり " + after + " マス" : "");
        NotifySystem.Push(verb + " " + tiles + " マス（-" + cost + "DP）" + road
            + "　<color=#9c95b4>残り " + Remaining + " 回</color>", NotifySystem.Kind.Gain);
        Debug.Log("⛏️『掘削』" + verb + " " + tiles + "マス -" + cost + "DP 道のり " + before + "→" + after + " 残り" + Remaining);
    }
}
