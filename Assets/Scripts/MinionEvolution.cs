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
    // 段階: 基本(depth0)→進化Ⅰ(1)→上位Ⅱ(2)→最上位Ⅲ(3)。分岐＝1つの親から複数の子。
    // depthは親を辿った段数で自動計算され、研究 m_evo1/2/3 でゲートされる（下 Depth()）。
    private static readonly Dictionary<string, string> EvoFrom = new Dictionary<string, string>
    {
        // ── 🦴 不死 ──
        // 進化Ⅰ
        { "skeleton_archer",  "skeleton" },
        { "skeleton_soldier", "skeleton" },
        { "ghoul",            "zombie" },
        { "wraith",           "ghost" },
        // 上位Ⅱ
        { "skeleton_knight",  "skeleton_soldier" },
        { "bone_sniper",      "skeleton_archer" },
        { "lich",             "wraith" },
        // 最上位Ⅲ
        { "death_knight",     "skeleton_knight" },
        { "elder_lich",       "lich" },

        // ── 🐺 獣 ──
        // 進化Ⅰ
        { "wolf",         "rat" },
        { "harpy",        "bat" },
        // 上位Ⅱ
        { "great_beast",  "wolf" },
        { "dire_wolf",    "wolf" },
        { "siren",        "harpy" },
        // 最上位Ⅲ
        { "behemoth",     "great_beast" },
        { "fenrir",       "dire_wolf" },

        // ── 😈 魔族（ゴブリン職ツリー） ──
        // 進化Ⅰ＝基本職
        { "goblin_archer", "goblin" },
        { "hobgoblin",     "goblin" },
        { "goblin_shaman", "goblin" },
        { "kobold",        "goblin" },
        { "dark_elf",      "imp" },
        // 上位Ⅱ＝上位職
        { "goblin_ranger",  "goblin_archer" },
        { "goblin_soldier", "hobgoblin" },
        { "goblin_mage",    "goblin_shaman" },
        { "orc",            "kobold" },
        // 最上位Ⅲ＝最上位職
        { "goblin_general", "goblin_soldier" },
        { "goblin_wizard",  "goblin_mage" },
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

    // 進化段階の深さ（基本形=0、進化形=進化元まで辿った段数）。研究ゲート(配下進化Ⅰ/Ⅱ/Ⅲ)に対応。
    public static int Depth(int catalogIndex)
    {
        string id = MinionCatalog.Get(catalogIndex).id;
        int d = 0;
        while (EvoFrom.TryGetValue(id, out var from)) { d++; id = from; }
        return d;
    }
    public static string TierResearchId(int catalogIndex) => "m_evo" + Mathf.Clamp(Depth(catalogIndex), 1, 3);
    public static bool TierResearched(int catalogIndex) => ResearchState.IsResearched(TierResearchId(catalogIndex));
    public static string TierResearchName(int catalogIndex)
        => ResearchCatalog.TryGet(TierResearchId(catalogIndex), out var n) ? n.jpName : "";

    // 今この配下を解禁できるか（未解禁＆進化元解禁済み＆該当段階が研究で開放済み）
    public static bool CanEvolve(int catalogIndex)
    {
        EnsureInit();
        if (IsUnlocked(catalogIndex)) return false;
        var from = PrereqId(catalogIndex);
        if (from == "" || !unlocked.Contains(from)) return false;
        return TierResearched(catalogIndex); // 🔬 魔物研究で進化段階が開放されて初めて可能
    }

    // 前提(進化元)は満たすが、研究段階が未開放で進化できない状態（図鑑UIの「研究で開放」表示用）
    public static bool TierResearchNeeded(int catalogIndex)
    {
        if (IsUnlocked(catalogIndex)) return false;
        var from = PrereqId(catalogIndex);
        if (from == "" || !unlocked.Contains(from)) return false;
        return !TierResearched(catalogIndex);
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
