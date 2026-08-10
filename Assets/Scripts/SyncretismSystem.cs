using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🜏 シンクレティズム（習合）＝**時代の変わり目に、他の魔王の系統を1つ取り込む**（G-5）。
///
/// **なぜ要るか**：Civ VII は時代が変わるとき文明そのものを乗り換える。
/// ここでは魔王を変えるわけにはいかないので、**他の魔王の血脈を取り込む**形にした。
/// 原作の「真核を奪う」＝相手の在りようを自分のものにする、という筋にも合う。
///
/// **設計**
/// - 取り込めるのは**時代が変わった直後の1回だけ**（見送ってもよい）。同じ系統は一度きり。
///   ＝1周で取れるのは最大2つ（胎動→伸長、伸長→終焉）。何を継ぐかで終盤の色が変わる。
/// - 対価は**威名**。⚠ ただし**排除した魔王の系統は半額**にしてある。
///   倒した相手のものを継ぐほうが安い＝「排除」と「習合」が同じ盤の上で繋がる。
/// - ⚠ 効果は**既にある軸に薄く乗せる**。新しい掛け算の軸を作らない（→ [[difficulty-curve-orders]]）。
///
/// 関連: [[RivalLords]] [[EraSystem]] [[DiplomacySystem]]。
/// </summary>
public static class SyncretismSystem
{
    public struct Lineage
    {
        public string id, jpName, desc, colorHex;
        public int rivalIndex;     // 対応する他魔王（RivalLords の index）
    }

    private static readonly Lineage[] defs =
    {
        new Lineage { id = "oni",    jpName = "鬼種の血",   colorHex = "#e05a5a", rivalIndex = 0,
            desc = "力そのものを継ぐ。配下すべての強さ +8%／軍団の攻城 +10%。" },
        new Lineage { id = "fae",    jpName = "妖精種の理", colorHex = "#57c3ab", rivalIndex = 1,
            desc = "理を継ぐ。毎ターンの研究点 +25%／天啓の割引が 40%→52%。" },
        new Lineage { id = "dragon", jpName = "龍種の威",   colorHex = "#b478e6", rivalIndex = 2,
            desc = "威を継ぐ。毎ターンの威名 +5／脅威度の上がり方が 20% 緩やかになる。" },
    };

    public static int Count => defs.Length;
    public static Lineage Get(int i) => defs[Mathf.Clamp(i, 0, defs.Length - 1)];

    // ⚠ readonly にしない（[[SaveSystem]] は readonly を「カタログ＝保存しない」の目印に使う）
    private static List<int> adopted;      // 取り込んだ系統の index
    private static bool pending;           // 時代が変わって、まだ選んでいない
    private static void EnsureInit() { if (adopted == null) adopted = new List<int>(); }

    public static void Reset() { adopted = new List<int>(); pending = false; }

    /// <summary>いま選べる状態か（時代が変わった直後）。</summary>
    public static bool Pending { get { return pending; } }
    public static int AdoptedCount { get { EnsureInit(); return adopted.Count; } }
    public static bool Has(int i) { EnsureInit(); return adopted.Contains(i); }
    public static bool HasId(string id)
    {
        for (int i = 0; i < defs.Length; i++) if (defs[i].id == id) return Has(i);
        return false;
    }

    /// <summary>時代が変わったときに `EraSystem.Advance` から呼ぶ。</summary>
    public static void OnEraChanged()
    {
        EnsureInit();
        if (adopted.Count >= defs.Length) return;      // もう継ぐものが無い
        pending = true;
        Debug.Log("🜏『習合の機』時代が変わった。他の魔王の系統を1つ継げる（地上メニュー『時代』から）");
        NotifySystem.Push("<b>習合の機</b>　他の魔王の系統を1つ継げる（『時代』から選ぶ／見送りも可）", NotifySystem.Kind.Story);
    }

    /// <summary>対価（威名）。⚠ 排除済みの魔王の系統は半額。</summary>
    public static int CostOf(int i)
    {
        var d = Get(i);
        int baseCost = 120 + i * 40;
        bool dead = d.rivalIndex >= 0 && d.rivalIndex < RivalLords.Count && RivalLords.Get(d.rivalIndex).defeated;
        return dead ? baseCost / 2 : baseCost;
    }

    public static bool IsRivalDefeated(int i)
    {
        var d = Get(i);
        return d.rivalIndex >= 0 && d.rivalIndex < RivalLords.Count && RivalLords.Get(d.rivalIndex).defeated;
    }

    public static bool CanAdopt(int i, out string why)
    {
        why = "";
        EnsureInit();
        if (!pending) { why = "継げるのは時代が変わった直後だけ"; return false; }
        if (Has(i)) { why = "既に継いでいる"; return false; }
        int c = CostOf(i);
        if (DiplomacySystem.Influence < c) { why = "威名が足りない（要" + c + "・所持" + DiplomacySystem.Influence + "）"; return false; }
        return true;
    }

    public static bool TryAdopt(int i)
    {
        string why;
        if (!CanAdopt(i, out why)) { Debug.LogWarning("⚠️ " + why); return false; }
        int c = CostOf(i);
        DiplomacySystem.AddInfluence(-c);
        adopted.Add(i);
        pending = false;
        var d = Get(i);
        Debug.Log($"🜏『習合』{d.jpName} を継いだ（-{c}威名）／{d.desc}");
        NotifySystem.Push($"<b>{d.jpName}</b> を継いだ ― {d.desc}", NotifySystem.Kind.Story);
        return true;
    }

    /// <summary>見送る（次の時代まで機会は来ない）。</summary>
    public static void Skip()
    {
        if (!pending) return;
        pending = false;
        Debug.Log("🜏『習合を見送った』この時代は自分の血のまま行く");
        NotifySystem.Push("習合を見送った ― 自分の血のまま行く", NotifySystem.Kind.Info);
    }

    // ============ 効果（既にある軸に薄く乗せるだけ） ============
    /// <summary>🗡️ 鬼種：配下すべての強さ（`DemonLord.MinionPowerMult` に掛かる）。</summary>
    public static float MinionPowerMult => HasId("oni") ? 1.08f : 1f;
    /// <summary>🏰 鬼種：軍団の攻城。</summary>
    public static float SiegeMult => HasId("oni") ? 1.10f : 1f;
    /// <summary>🔬 妖精種：毎ターンの研究点。</summary>
    public static float RpMult => HasId("fae") ? 1.25f : 1f;
    /// <summary>💡 妖精種：天啓の割引（0.60＝40%引き → 0.48＝52%引き）。</summary>
    public static float EurekaDiscountMult => HasId("fae") ? 0.80f : 1f;
    /// <summary>🤝 龍種：毎ターンの威名。</summary>
    public static int InfluencePerTurn => HasId("dragon") ? 5 : 0;
    /// <summary>🕸️ 龍種：脅威度の上がり方。</summary>
    public static float ThreatRiseMult => HasId("dragon") ? 0.80f : 1f;

    /// <summary>毎ターンの効果（`EraSystem.TickTurn` の後で呼ぶ）。</summary>
    public static void TickTurn()
    {
        if (InfluencePerTurn > 0) DiplomacySystem.AddInfluence(InfluencePerTurn);
    }

    /// <summary>UIの1行（「継いだ血：鬼種の血・妖精種の理」）。</summary>
    public static string Summary
    {
        get
        {
            EnsureInit();
            if (adopted.Count == 0) return "<color=#6f6889>継いだ血：なし</color>";
            var s = "継いだ血：";
            for (int i = 0; i < adopted.Count; i++)
            {
                var d = Get(adopted[i]);
                s += (i > 0 ? "・" : "") + "<color=" + d.colorHex + ">" + d.jpName + "</color>";
            }
            return s;
        }
    }
}
