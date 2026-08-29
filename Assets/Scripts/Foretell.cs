using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⏳ **次に起きること**（S-1）。何ターン後に何が起きるかを1本のリストに集める。
///
/// <para>
/// ⚠⚠ **なぜ要るか（実装を読んで分かったこと）**：期限のある出来事は既にいくつもあるのに、
///   知らせ方が **ターン頭の通知1回だけ**だった。`EnemyForce` は
///   「カンタの軍が集まりつつある（2ターン後に進発）」を `NotifySystem` に流すが、
///   **右の通知は流れて消える**。だから「あと2ターン」という圧が**その場で消える**。
/// </para>
///
/// <para>
/// ⚠⚠ **迷宮の画面に出す**こと。地上のサイドバーに置いても意味が薄い。
///   狙っている感覚は「この波さえ凌げば研究が終わる」「あの軍が来る前に関所を厚くしないと」
///   ―― つまり**迷宮の判断をしている最中に見えていないと圧にならない**。
/// </para>
///
/// <para>
/// ⚠ ここは**読むだけ**。ターンを進めたり値を変えたりしない（表示のための集約）。
/// ⚠ 近いものから並べる。**0ターン＝もう起きている／止まっている**は最上位に置く。
/// </para>
///
/// 関連: [[EnemyForce]] [[EraSystem]] [[Prison]] [[TrainingSystem]] [[FeverSystem]] [[GuideSystem]]。
/// </summary>
public static class Foretell
{
    /// <summary>危険＝赤／好機＝金／中立＝薄い。色でしか分けないので、種類はこの3つで足りる。</summary>
    public enum Tone { Danger, Boon, Neutral }

    public struct Item
    {
        public int turns;      // 0 = もう起きている／止まっている
        public string text;
        public Tone tone;
    }

    private static readonly List<Item> buf = new List<Item>();

    /// <summary>近いものから最大 `max` 件。⚠ 毎フレーム呼ばれるので、確保は使い回す。</summary>
    public static List<Item> Upcoming(int max)
    {
        buf.Clear();
        var turn = DungeonTurnManager.Instance;
        int now = turn != null ? turn.CurrentTurn : 1;

        // ⚡ 答えていない異変。⚠ **侵略開始が押せない**状態なので、止まっているのと同じ扱い
        if (IncidentSystem.HasPending) Add(0, "異変に答えていない ― 答えるまで侵略を始められない", Tone.Danger);

        // ⏳ 時代。⚠ 止まっているときは**最優先**（ゲームが進んでいない → [[EraSystem]]）
        if (EraSystem.BlockedOnCrisisPolicy)
            Add(0, "時代が止まっている ― 災厄の政策を選ぶ", Tone.Danger);
        else if (EraSystem.Current != EraSystem.Era.End)
        {
            int need = Mathf.Max(0, EraSystem.Need - EraSystem.Progress);
            int t = Mathf.CeilToInt(need / (float)Mathf.Max(1, EraSystem.ProgressPerTurn));
            if (t <= 20) Add(t, EraSystem.EraName(EraSystem.Current) + " が終わる", Tone.Neutral);
        }

        // ⚔️ 集結中の敵軍（→ [[EnemyForce]]）。**これが「あと1ターン」の主役**
        var armies = EnemyForce.All;
        for (int i = 0; i < armies.Count; i++)
        {
            var a = armies[i];
            if (a == null || a.musterTurns <= 0) continue;
            string where = SurfaceMap.Get(a.regionId) != null ? SurfaceMap.Get(a.regionId).name : "遠く";
            Add(a.musterTurns, a.name + " が " + where + " から進発（戦力 " + Mathf.RoundToInt(a.power) + "）", Tone.Danger);
        }

        // 🏯 迷宮そのものへ向かっている軍（→ [[EnemyForce]]）。**いちばん重い予定**
        float apow; string aname;
        int at = EnemyForce.TurnsToAssault(out apow, out aname);
        if (at >= 0)
            Add(at, aname + " が坑道へ雪崩れ込む（戦力 " + Mathf.RoundToInt(apow) + "）", Tone.Danger);
        // ⚠ もう入ってしまったぶん。**この波に加わる**ので「今」として出す
        //   （軍は盤から消えているので `TurnsToAssault` では拾えない）
        if (EnemyForce.PendingAssault > 0f)
            Add(0, "討伐隊がこの波に加わる（戦力 " + Mathf.RoundToInt(EnemyForce.PendingAssault) + "）", Tone.Danger);

        // ⛓️ 牢：反抗心が折れるまで（→ [[Prison]]）
        var caps = Prison.All;
        for (int i = 0; i < caps.Count; i++)
        {
            var c = caps[i];
            if (c == null || c.defiance <= 0) { if (c != null) Add(0, c.name + " は膝を折っている ― 調伏できる", Tone.Boon); continue; }
            int dec = Prison.DefianceDecayOf(c);
            if (dec <= 0) continue;
            int t = Mathf.CeilToInt(c.defiance / (float)dec);
            if (t <= 12) Add(t, c.name + " が膝を折る", Tone.Boon);
        }

        // 🏋️ 訓練（→ [[TrainingSystem]]）
        var tr = TrainingSystem.All;
        int soonest = int.MaxValue, count = 0;
        for (int i = 0; i < tr.Count; i++) { if (tr[i].turnsLeft < soonest) soonest = tr[i].turnsLeft; count++; }
        if (count > 0 && soonest < int.MaxValue)
            Add(Mathf.Max(0, soonest), "訓練が終わる（" + count + " 体）", Tone.Boon);

        // 🔥 大招集の休み（→ [[FeverSystem]]）
        int rest = FeverSystem.ReadyTurn - now;
        if (rest > 0) Add(rest, "大招集が使えるようになる", Tone.Boon);

        buf.Sort((a, b) => a.turns.CompareTo(b.turns));
        if (buf.Count > max) buf.RemoveRange(max, buf.Count - max);
        return buf;
    }

    private static void Add(int turns, string text, Tone tone)
    {
        buf.Add(new Item { turns = Mathf.Max(0, turns), text = text, tone = tone });
    }
}
