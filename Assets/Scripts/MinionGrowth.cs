using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🌱 <b>育った手応え</b>（H3・承認済みの改善案）。数値は変えず、<b>見せる場面</b>だけを足す。
///
/// - 波の頭に全個体の Lv・撃破・格を書き留め、決算で差分を「配下の活躍」として並べる（`Diff`）。
/// - Lv が上がった瞬間、盤の駒の上に「Lv5↑」を浮かべる（`NoteLevelUp`。経験は波の終わりにまとめて入る）。
/// - 格が上がった・進化したときは、短い場面（札を大きく出す）を順番待ちに積む（`Moments`）。
///
/// ⚠ 以前は Lv が上がっても何の知らせも無く、格上げは通知の1行だけだった（ユーザー指摘「満足できない点」）。
/// ⚠ 計測中（`MeasureMode`）は何も出さない・積まない（並列計測の速さを落とさない）。
/// </summary>
public static class MinionGrowth
{
    private struct Snap { public int level, kills, rank, cat; }
    private static readonly Dictionary<int, Snap> snap = new Dictionary<int, Snap>();

    public class Entry
    {
        public int id;
        public int kills;
        public int lvFrom, lvTo;
        public int rankFrom, rankTo;
        public bool evolved;
        public int Score => kills * 10 + (lvTo - lvFrom) * 4 + (rankTo - rankFrom) * 20 + (evolved ? 20 : 0);
    }

    /// <summary>波の頭（`WaveReport.BeginWave`）。</summary>
    public static void BeginWave()
    {
        snap.Clear();
        foreach (var v in MinionRoster.All)
            snap[v.id] = new Snap { level = v.level, kills = v.kills, rank = v.rank, cat = v.catalogIndex };
    }

    /// <summary>この波で何かが動いた個体（活躍の大きい順）。⚠ 決算を開いたときに呼ぶ（経験の配り終わりの後）。</summary>
    public static List<Entry> Diff()
    {
        var list = new List<Entry>();
        foreach (var v in MinionRoster.All)
        {
            Snap s;
            if (!snap.TryGetValue(v.id, out s)) continue;   // 波の途中で増えた個体は数えない
            var e = new Entry
            {
                id = v.id, kills = Mathf.Max(0, v.kills - s.kills),
                lvFrom = s.level, lvTo = v.level, rankFrom = s.rank, rankTo = v.rank, evolved = v.catalogIndex != s.cat,
            };
            if (e.Score > 0) list.Add(e);
        }
        list.Sort((a, b) => b.Score.CompareTo(a.Score));
        return list;
    }

    // ============ その場の見せ方 ============
    /// <summary>Lv が上がった（`MinionRoster.AddExp` から）。盤に駒が居れば頭上に浮かべる。</summary>
    public static void NoteLevelUp(int id, int from, int to)
    {
        if (MeasureMode.On || to <= from) return;
        var fm = DungeonFeatureManager.Instance;
        if (fm == null) return;
        Vector3 p;
        if (!fm.TryWorldPosOfIndividual(id, out p)) return;
        FloatText.Spawn(p + new Vector3(0f, 0.9f, 0f), "Lv" + to + "↑", new Color(0.36f, 0.86f, 0.52f), 2.4f, 1.1f, 1.3f, 0.5f);
    }

    public struct Moment { public int id; public string title; public string line; }
    /// <summary>格上げ・進化の場面の順番待ち（UI が1つずつ出す → `GameUIManager.Growth`）。</summary>
    public static readonly Queue<Moment> Moments = new Queue<Moment>();

    public static void NoteMoment(int id, string title, string line)
    {
        if (MeasureMode.On) return;
        if (Moments.Count > 6) return;   // ⚠ 一度に積みすぎない（まとめて上がったときに延々と続かないように）
        Moments.Enqueue(new Moment { id = id, title = title, line = line });
    }
}
