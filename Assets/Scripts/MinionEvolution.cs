using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 配下進化ツリー（原作の配下進化 × CDO2のアンロック進行）。
///
/// - 基本形（進化元を持たない配下）は最初から解禁。進化形はロックされ、前提（進化元が解禁済み）＋DPで解禁する。
/// - MinionCatalog はデータを汚さないよう不変のまま。進化パスと解禁状態はこのクラスが保持（静的・実行時）。
///   ※セーブ機構は未実装なのでプレイセッション内で保持（ドメインリロードでリセット＝新規プレイは基本形のみ）。
/// - 解禁されていない配下は図鑑で「進化」ボタン表示、部隊には追加不可。
/// 関連: [[MinionCatalog]] / GameUIManager(図鑑UI) / DungeonResourceManager(DPコスト)。
/// </summary>
public static class MinionEvolution
{
    // 進化形ID → 進化元ID（進化元が解禁済みなら、DPを払って解禁できる）
    private static readonly Dictionary<string, string> EvoFrom = new Dictionary<string, string>
    {
        // 不死
        { "skeleton_archer", "skeleton" },
        { "lich",            "ghost" },
        // 獣
        { "wolf",         "rat" },
        { "harpy",        "bat" },
        { "great_beast",  "wolf" },
        // 魔族
        { "goblin_archer", "goblin" },
        { "kobold",        "goblin" },
        { "orc",           "kobold" },
        { "dark_elf",      "imp" },
    };

    [Tooltip("進化解禁のDPコスト＝ティア×この係数")]
    public const float EvolveCostPerTier = 25f;

    private static HashSet<string> unlocked;

    private static void EnsureInit()
    {
        if (unlocked != null) return;
        unlocked = new HashSet<string>();
        // 進化元を持たない＝基本形は最初から解禁
        for (int i = 0; i < MinionCatalog.Count; i++)
        {
            string id = MinionCatalog.Get(i).id;
            if (!EvoFrom.ContainsKey(id)) unlocked.Add(id);
        }
    }

    // 新規プレイ用：基本形のみに戻す
    public static void ResetToBase() { unlocked = null; EnsureInit(); }

    public static bool IsUnlocked(int catalogIndex)
    {
        EnsureInit();
        return unlocked.Contains(MinionCatalog.Get(catalogIndex).id);
    }

    // 進化形か（進化元を持つか）
    public static bool IsEvolved(int catalogIndex) => EvoFrom.ContainsKey(MinionCatalog.Get(catalogIndex).id);

    // 進化元の配下ID（無ければ空文字）
    public static string PrereqId(int catalogIndex)
    {
        string id = MinionCatalog.Get(catalogIndex).id;
        return EvoFrom.TryGetValue(id, out var from) ? from : "";
    }

    // 進化元の表示名（UI用。無ければ空）
    public static string PrereqName(int catalogIndex)
    {
        var from = PrereqId(catalogIndex);
        return (from != "" && MinionCatalog.TryGet(from, out var d)) ? d.jpName : "";
    }

    // 今この配下を解禁できるか（未解禁＆進化元が解禁済み）
    public static bool CanEvolve(int catalogIndex)
    {
        EnsureInit();
        if (IsUnlocked(catalogIndex)) return false;
        var from = PrereqId(catalogIndex);
        return from != "" && unlocked.Contains(from);
    }

    public static int EvolveCost(int catalogIndex)
    {
        return Mathf.RoundToInt(MinionCatalog.Get(catalogIndex).tierCP * EvolveCostPerTier);
    }

    // 進化解禁（前提＆DPを満たせば解禁してtrue）
    public static bool TryEvolve(int catalogIndex)
    {
        EnsureInit();
        if (!CanEvolve(catalogIndex)) return false;
        int cost = EvolveCost(catalogIndex);
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(cost)) return false;
        unlocked.Add(MinionCatalog.Get(catalogIndex).id);
        Debug.Log($"🧬【配下進化】{MinionCatalog.Get(catalogIndex).jpName} を解禁（-{cost}DP）");
        return true;
    }
}
