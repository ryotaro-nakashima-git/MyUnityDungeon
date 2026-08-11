using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🎰 召喚の儀（ガチャ）。原作の「何が呼べるかは選べない」召喚を、入手経路として形にしたもの。
///
/// **なぜ要るか**：いまの入手経路は「一覧から選んで召喚」だけで、**毎周まったく同じ手順**になる。
/// 引くたびに手持ちの偏りが変わると、その周の組み立てが変わる。
/// そして👾**ユニーク魔物はここでしか手に入らない**（→ [[UniqueCatalog]]）。
///
/// **設計**
/// - 出るのは「解禁済みの通常種」か「ユニーク」。⚠ 未解禁の種は出さない
///   （進化ツリーで解禁する意味が消えるため。ガチャは**幅**を作るもので、近道ではない）。
/// - ユニークの確率は低く、**外れても通常種が必ず1体**手に入る（空引きにしない）。
/// - 対価はDP。⚠ 通常召喚より**割高**にする。安いと一覧から選ぶ意味が消える。
///
/// 関連: [[MinionRoster]] [[MinionEvolution]] [[DungeonResourceManager]]。
/// </summary>
public static class SummonGacha
{
    /// <summary>1回の対価（DP）。世界が育つほど強い個体が出るので、召喚と同じく世界水準で上がる。</summary>
    public static int Cost
    {
        get
        {
            float m = DemonLord.Instance != null ? DemonLord.Instance.DefenderCostMult : 1f;
            int lv = MinionRoster.SummonLevel();
            return Mathf.RoundToInt(900f * m * (1f + (lv - 1) * 0.10f));
        }
    }

    /// <summary>ユニークが出る確率（0..1）。⚠ ここを上げると「ユニークで固める」が最適手になる。</summary>
    public const float UniqueChance = 0.06f;

    /// <summary>直近の結果（UIに出す）。</summary>
    public static string LastResult { get; private set; } = "";
    public static int LastIndividualId { get; private set; } = -1;
    public static bool LastWasUnique { get; private set; }

    /// <summary>🔁 天井。外し続けたぶんだけユニークの確率が上がる（引くほど近づく）。</summary>
    private static int missStreak;
    public static int MissStreak => missStreak;
    public static float CurrentUniqueChance => Mathf.Min(0.5f, UniqueChance + missStreak * 0.02f);

    public static void Reset() { missStreak = 0; LastResult = ""; LastIndividualId = -1; LastWasUnique = false; }

    public static bool CanRoll(out string why)
    {
        why = "";
        var res = DungeonResourceManager.Instance;
        if (res != null && res.DungeonPoints < Cost) { why = "DPが足りない（要" + Cost + "）"; return false; }
        if (UnlockedNormals().Count == 0) { why = "召喚できる種がまだ無い"; return false; }
        return true;
    }

    private static List<int> UnlockedNormals()
    {
        var l = new List<int>();
        for (int i = 0; i < MinionCatalog.Count; i++) if (MinionEvolution.IsUnlocked(i)) l.Add(i);
        return l;
    }

    public static bool TryRoll()
    {
        string why;
        if (!CanRoll(out why)) { Debug.LogWarning("⚠️ " + why); return false; }
        var res = DungeonResourceManager.Instance;
        int cost = Cost;
        if (res != null && !res.TrySpendDP(cost)) return false;

        if (Random.value < CurrentUniqueChance)
        {
            // 👾 当たり：重みつきで1種
            int roll = Random.Range(0, UniqueCatalog.TotalWeight), acc = 0, pick = 0;
            for (int i = 0; i < UniqueCatalog.Count; i++)
            {
                acc += UniqueCatalog.Get(i).weight;
                if (roll < acc) { pick = i; break; }
            }
            var v = MinionRoster.GrantUnique(pick);
            missStreak = 0;
            LastWasUnique = true; LastIndividualId = v.id;
            LastResult = "👾 " + UniqueCatalog.Get(pick).jpName + " #" + v.id;
            Debug.Log($"🎰『召喚の儀』ユニーク {UniqueCatalog.Get(pick).jpName} を引き当てた（-{cost}DP）");
            return true;
        }

        // 外れ：解禁済みの通常種から1体。⚠ 空引きにしない（払ったのに何も無いのは体験として悪い）
        var pool = UnlockedNormals();
        int ci = pool[Random.Range(0, pool.Count)];
        var n = MinionRoster.TrySummonFree(ci);
        missStreak++;
        LastWasUnique = false; LastIndividualId = n.id;
        LastResult = MinionCatalog.Get(ci).jpName + " #" + n.id;
        Debug.Log($"🎰『召喚の儀』{MinionCatalog.Get(ci).jpName} 個体#{n.id}（-{cost}DP／次のユニーク確率 {CurrentUniqueChance * 100f:0.0}%）");
        NotifySystem.Push($"召喚の儀：<b>{MinionCatalog.Get(ci).jpName}</b> を得た", NotifySystem.Kind.Gain);
        return true;
    }
}
