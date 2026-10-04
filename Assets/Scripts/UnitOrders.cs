using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🕹️ 地上のユニットの「命令済みか」を数える（段B：ユニットの札）。
///
/// **なぜ要るか**：大ボタンが「眷属を動かす」と言っても、**どのユニットのことか**が分からず、
/// 押した先も眷属の一覧を開くだけだった。Civ と同じく「命令を待っているユニット」を1体ずつ
/// 案内するには、**このターンに命令したか**を覚えておく場所が要る。
///
/// - 待機：このターンだけ命令済みにする（次のターンにまた聞く）。
/// - 守り：解くまで聞かない（Civ の「防御を固める」）。動かす・進軍させると自動で解ける。
/// ⚠ ここはセーブに載せない（読み込み直後は全員がもう一度命令を待つ＝困らない）。
/// ⚠ 移動力・負傷・進軍先といった**事実は各名簿が持っている**。ここは「人が決めたこと」だけ。
/// </summary>
public static class UnitOrders
{
    public enum Kind { None, Kin, Legion, Scout }

    public struct Unit
    {
        public Kind kind; public int id;
        public Unit(Kind k, int i) { kind = k; id = i; }
        public bool IsNone => kind == Kind.None;
        public override string ToString() => kind + "#" + id;
    }

    private static readonly HashSet<string> stood = new HashSet<string>();      // このターンだけ
    private static readonly HashSet<string> fortified = new HashSet<string>();  // 解くまで
    private static int stoodTurn = -1;

    private static string Key(Kind k, int id) => (int)k + ":" + id;

    private static void Roll()
    {
        int t = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 0;
        if (t == stoodTurn) return;
        // ⚠ ターンが戻った＝新しい周。守りも持ち越さない（id が別の個体に振り直される）
        if (t < stoodTurn) fortified.Clear();
        stood.Clear();
        stoodTurn = t;
    }

    public static void Stand(Kind k, int id) { Roll(); stood.Add(Key(k, id)); }
    public static void Fortify(Kind k, int id) { Roll(); fortified.Add(Key(k, id)); }
    /// <summary>手で動かした・進軍させた＝守りも待機も解く。</summary>
    public static void Released(Kind k, int id) { Roll(); stood.Remove(Key(k, id)); fortified.Remove(Key(k, id)); }
    public static bool IsStood(Kind k, int id) { Roll(); return stood.Contains(Key(k, id)); }
    public static bool IsFortified(Kind k, int id) { Roll(); return fortified.Contains(Key(k, id)); }

    /// <summary>まだ命令を待っているか（動ける・進軍していない・待機も守りもしていない）。</summary>
    public static bool IsWaiting(Unit u)
    {
        Roll();
        string key = Key(u.kind, u.id);
        if (stood.Contains(key) || fortified.Contains(key)) return false;
        switch (u.kind)
        {
            case Kind.Kin:
                {
                    var k = KinRoster.Of(u.id);
                    return k != null && k.regionId >= 0 && k.injuryTurns <= 0 && k.marchTarget < 0 && KinRoster.MpOf(k) > 0;
                }
            case Kind.Legion:
                {
                    var l = LegionRoster.Get(u.id);
                    return l != null && l.regionId >= 0 && l.marchTarget < 0 && !l.foughtThisTurn && LegionRoster.MpOf(l) > 0;
                }
            case Kind.Scout:
                {
                    var s = ScoutSystem.Of(u.id);
                    return s != null && s.regionId >= 0 && ScoutSystem.MpOf(s) > 0;
                }
        }
        return false;
    }

    /// <summary>盤の上の自分のユニット全部（眷属 → 軍団 → 斥候の順＝札の列と同じ並び）。</summary>
    public static List<Unit> All()
    {
        var list = new List<Unit>();
        foreach (var k in KinRoster.All) if (k != null && k.regionId >= 0) list.Add(new Unit(Kind.Kin, k.individualId));
        foreach (var l in LegionRoster.All) if (l != null && l.regionId >= 0) list.Add(new Unit(Kind.Legion, l.id));
        foreach (var s in ScoutSystem.All) if (s != null && s.regionId >= 0) list.Add(new Unit(Kind.Scout, s.id));
        return list;
    }

    public static int WaitingCount()
    {
        int n = 0;
        foreach (var u in All()) if (IsWaiting(u)) n++;
        return n;
    }

    /// <summary>`after` の次に命令を待っているユニット（いなければ None）。</summary>
    public static Unit NextWaiting(Unit after)
    {
        var all = All();
        int start = -1;
        for (int i = 0; i < all.Count; i++) if (all[i].kind == after.kind && all[i].id == after.id) { start = i; break; }
        for (int n = 1; n <= all.Count; n++)
        {
            var u = all[(start + n + all.Count) % all.Count];
            if (IsWaiting(u)) return u;
        }
        return new Unit(Kind.None, -1);
    }

    public static int RegionOf(Unit u)
    {
        switch (u.kind)
        {
            case Kind.Kin: { var k = KinRoster.Of(u.id); return k != null ? k.regionId : -1; }
            case Kind.Legion: { var l = LegionRoster.Get(u.id); return l != null ? l.regionId : -1; }
            case Kind.Scout: { var s = ScoutSystem.Of(u.id); return s != null ? s.regionId : -1; }
        }
        return -1;
    }

    public static string NameOf(Unit u)
    {
        switch (u.kind)
        {
            case Kind.Kin: { var k = KinRoster.Of(u.id); return k != null ? k.trueName : "?"; }
            case Kind.Legion: { var l = LegionRoster.Get(u.id); return l != null ? LegionRoster.NameOf(l) : "?"; }
            case Kind.Scout: return "斥候 #" + u.id;
        }
        return "";
    }
}
