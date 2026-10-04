using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🎯 **一括布陣**（D-1）。隊の未配置メンバーを、その階の「関所」へまとめて置く。
///
/// <para>
/// ⚠⚠ **なぜ入れるか（実測の裏付け）**：通しプレイ T1〜T30 を、この30行と同じBFSで
///   自動配置したまま**完封できてしまった**。つまり1マスずつ置く操作に
///   **判断の価値がもう残っていない**（作業になっている）。
///   → 定石はボタン1つで済ませ、プレイヤーの時間を「どこを厚くするか」の判断に返す。
/// </para>
///
/// <para>
/// ⚠ **手で置く道を潰さない。** これは「おすすめを一発で敷く」であって、
///   置き換えではない。押したあとに個別に動かせることが前提。
/// </para>
///
/// 関連: [[playtest-t1-t30]]（この機能の根拠） [[DungeonFeatureManager]]。
/// </summary>
public static class AutoDeploy
{
    private static readonly int[] DX4 = { 1, -1, 0, 0 };
    private static readonly int[] DY4 = { 0, 0, 1, -1 };
    private static readonly int[] DX8 = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] DY8 = { 0, 0, 1, -1, 1, -1, 1, -1 };

    /// <summary>
    /// 入口から階段（最下層なら魔王）までの最短経路。
    /// ⚠ 冒険者と**同じ「歩ける＝壁でない」基準**で引くこと。基準が違うと
    ///   彼らが通らない場所を「関所」と呼んでしまう。
    /// </summary>
    public static List<Vector2Int> PathToGoal(DungeonGridSystem g)
    {
        var path = new List<Vector2Int>();
        if (g == null) return path;
        int n = g.CurrentPlayableSize;
        var start = g.EntranceCell;
        if (g.GetTileType(start.x, start.y) == DungeonGridSystem.TileType.None) return path;

        var prev = new Dictionary<Vector2Int, Vector2Int>();
        var dist = new int[n, n];
        for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) dist[x, y] = -1;
        var q = new Queue<Vector2Int>();
        dist[start.x, start.y] = 0; q.Enqueue(start);
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int nx = p.x + DX4[k], ny = p.y + DY4[k];
                if (nx < 0 || ny < 0 || nx >= n || ny >= n || dist[nx, ny] >= 0) continue;
                if (g.GetTileType(nx, ny) == DungeonGridSystem.TileType.None) continue;
                dist[nx, ny] = dist[p.x, p.y] + 1;
                prev[new Vector2Int(nx, ny)] = p;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }
        var cur = g.BossCell;
        while (cur != start && prev.ContainsKey(cur)) { path.Add(cur); cur = prev[cur]; }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// 🔒 経路上の**関所**（歩ける隣が2以下＝そこしか通れない1マス）。奥から順に返す。
    /// ここに罠を置くと必ず全員が踏む。
    /// </summary>
    public static List<Vector2Int> ChokePoints(DungeonGridSystem g)
    {
        var list = new List<Vector2Int>();
        if (g == null) return list;
        int n = g.CurrentPlayableSize;
        var path = PathToGoal(g);
        for (int i = path.Count - 1; i >= 0; i--)
        {
            var c = path[i];
            if (c == g.EntranceCell || c == g.BossCell) continue;
            int nb = 0;
            for (int k = 0; k < 4; k++)
            {
                int nx = c.x + DX4[k], ny = c.y + DY4[k];
                if (nx >= 0 && ny >= 0 && nx < n && ny < n && g.GetTileType(nx, ny) != DungeonGridSystem.TileType.None) nb++;
            }
            if (nb <= 2) list.Add(c);
        }
        return list;
    }

    /// <summary>
    /// おすすめの立ち位置。**経路の終盤（目標の手前）から**、経路に接する床を拾う。
    /// ⚠ 入口寄りに置かない ―― 手前で削り切ると、深い階も魔王の反撃も出番が無くなる。
    /// </summary>
    public static List<Vector2Int> SuggestedSpots(DungeonGridSystem g, int want)
    {
        var spots = new List<Vector2Int>();
        if (g == null || want <= 0) return spots;
        var fm = DungeonFeatureManager.Instance;
        int n = g.CurrentPlayableSize;
        var path = PathToGoal(g);
        for (int i = path.Count - 1; i >= 0 && spots.Count < want; i--)
            for (int k = 0; k < 8 && spots.Count < want; k++)
            {
                int nx = path[i].x + DX8[k], ny = path[i].y + DY8[k];
                if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                var c = new Vector2Int(nx, ny);
                if (g.GetTileType(nx, ny) == DungeonGridSystem.TileType.None) continue;
                if (c == g.BossCell || c == g.EntranceCell) continue;
                if (fm != null && fm.HasFeatureAt(c)) continue;
                if (spots.Contains(c)) continue;
                spots.Add(c);
            }
        return spots;
    }

    /// <summary>
    /// 🎯 表示している階に、隊の**未配置メンバーだけ**を一括で置く。置けた数を返す。
    /// ⚠ 既に置いてあるものは動かさない（手で調整したものを壊さないため）。
    /// </summary>
    public static int DeployCurrentFloor(out string message)
    {
        message = "";
        var fm = DungeonFeatureManager.Instance;
        var g = DungeonGridSystem.Active;
        if (fm == null || g == null) { message = "盤が無い"; return 0; }
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { message = "布陣は準備フェーズだけ"; return 0; }

        var squad = fm.CurrentSquad;
        if (squad.Count == 0) { message = "この階の隊が空（図鑑の『個体』タブで＋隊）"; return 0; }

        // 未配置のスロットを拾う
        var need = new List<int>();
        for (int i = 0; i < squad.Count; i++) if (!fm.IsIndividualPlaced(squad[i])) need.Add(i);
        if (need.Count == 0) { message = "この階の隊は全員もう置いてある"; return 0; }

        var spots = SuggestedSpots(g, need.Count);
        int placed = 0;
        for (int i = 0; i < need.Count && i < spots.Count; i++)
        {
            fm.SetSquadPlaceSlot(need[i]);
            if (fm.TryPlaceSquadMember(spots[i])) placed++;
        }
        if (placed == 0) message = "置ける空きマスが無かった";
        else message = placed + " 体を関所へ配した";
        return placed;
    }
}
