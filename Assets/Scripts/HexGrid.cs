using UnityEngine;

/// <summary>
/// ⬡ ヘクス盤の座標そのもの。**Civ と同じ「幅W×高さHの長方形＋東西ループ」**。
///
/// C1までは axial 半径R の六角形（3R(R+1)+1タイル）だったが、Civ は横長の長方形で
/// **東の端と西の端がつながる**（南北の端だけが極地で閉じる）。世界を広くするなら形も本家に合わせる。
///
/// 配置は **odd-r offset（pointy-top）** ＝ Unity の Hexagonal Point Top Tilemap がそのまま使う座標系。
/// W2でTilemapへ移すときに変換が要らないよう、最初からこの並びで持つ。
///
/// - `col` は 0..W-1 で **ラップする**（-1 は W-1、W は 0）。`row` は 0..H-1 でラップしない。
/// - 近傍は行の偶奇で変わる（offset座標の宿命）。距離は cube 座標に直してから測る。
///
/// 関連: [[SurfaceMap]] [[SurfaceGen]] [[civ7-roadmap]]。
/// </summary>
public static class HexGrid
{
    /// <summary>odd-r の6方向。[row&amp;1][dir] → (dcol, drow)。</summary>
    private static readonly int[][,] Dirs =
    {
        // 偶数行（左に寄っている行）
        new int[,] { { 1, 0 }, { 0, -1 }, { -1, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 } },
        // 奇数行（右に半マスずれた行）
        new int[,] { { 1, 0 }, { 1, -1 }, { 0, -1 }, { -1, 0 }, { 0, 1 }, { 1, 1 } },
    };

    /// <summary>列を東西にラップさせる（負でも正しく回る）。</summary>
    public static int WrapCol(int col, int w)
    {
        if (w <= 0) return col;
        col %= w;
        return col < 0 ? col + w : col;
    }

    /// <summary>d番目の隣。盤の外（南北）に出たら false。</summary>
    public static bool Neighbor(int col, int row, int d, int w, int h, out int ncol, out int nrow)
    {
        var t = Dirs[row & 1];
        ncol = col + t[d, 0];
        nrow = row + t[d, 1];
        if (nrow < 0 || nrow >= h) { ncol = nrow = -1; return false; }
        ncol = WrapCol(ncol, w);
        return true;
    }

    // ── cube 座標（距離を測るため）──
    public static void ToCube(int col, int row, out int x, out int y, out int z)
    {
        x = col - ((row - (row & 1)) >> 1);
        z = row;
        y = -x - z;
    }

    /// <summary>東西のラップを考えた最短距離。</summary>
    public static int Distance(int c1, int r1, int c2, int r2, int w)
    {
        int best = int.MaxValue;
        // 東回り・そのまま・西回り の3通りを試して最短を採る
        for (int k = -1; k <= 1; k++)
        {
            int cc = c2 + k * w;
            int x1, y1, z1, x2, y2, z2;
            ToCube(c1, r1, out x1, out y1, out z1);
            ToCube(cc, r2, out x2, out y2, out z2);
            int d = (Mathf.Abs(x1 - x2) + Mathf.Abs(y1 - y2) + Mathf.Abs(z1 - z2)) / 2;
            if (d < best) best = d;
        }
        return best;
    }

    /// <summary>描画用の中心座標（pointy-top）。size＝外接円の半径。</summary>
    public static Vector2 WorldPos(int col, int row, float size)
        => new Vector2(size * 1.7320508f * (col + 0.5f * (row & 1)), size * 1.5f * row);

    /// <summary>ワールド座標から一番近いセルを逆算する（クリック判定用。Buttonが要らなくなる）。</summary>
    public static void CellAt(Vector2 pos, float size, int w, int h, out int col, out int row)
    {
        row = Mathf.RoundToInt(pos.y / (size * 1.5f));
        row = Mathf.Clamp(row, 0, Mathf.Max(0, h - 1));
        col = Mathf.RoundToInt(pos.x / (size * 1.7320508f) - 0.5f * (row & 1));
        col = WrapCol(col, w);
    }
}
