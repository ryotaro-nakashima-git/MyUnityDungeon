using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 配下進化ツリー（原作の配下進化 × CDO2のアンロック進行）。
///
/// - 基本形（進化元を持たない配下）は最初から解禁。進化形はロックされ、前提（進化元が解禁済み）＋DPで解禁する。
/// - MinionCatalog はデータを汚さないよう不変のまま。進化パスと解禁状態はこのクラスが保持（静的・実行時）。
///   ・セーブ機構は未実装なのでプレイセッション内で保持（ドメインリロードでリセット＝新規プレイは基本形のみ）。
/// - 解禁されていない配下は図鑑で『進化』ボタン表示、部隊には追加不可。
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

        // ── 👑 王種(depth4)：最上位Ⅲの6形態それぞれの頂点。研究 m_evo4 でゲート ──
        { "doom_lord",      "death_knight" },
        { "bone_sovereign", "elder_lich" },
        { "titanbeast",     "behemoth" },
        { "wolf_king",      "fenrir" },
        { "warlord",        "goblin_general" },
        { "archmage",       "goblin_wizard" },

        // ── 🦴 古代種(depth5)：最果て。研究 m_evo5 でゲート ──
        // ⚠ `EvoFrom` は**子→親が1対1**なので、複数の王種から1つの古代種へ**合流はできない**。
        //   合流させたいなら EvoFrom の型から変える必要がある（今は1:1で通す）。
        { "ancient_revenant",  "doom_lord" },
        { "ancient_ossuary",   "bone_sovereign" },

        // ── 🧬 行き止まりを古代種まで伸ばす（2026-09-06）──
        // ⚠ ゾンビ(1段)・インプ(1段)・バット(2段) は**買っても伸びない**状態だった。
        //   行き止まり6つを、それぞれ段5まで1段ずつ繋ぐ。
        { "greater_ghoul",   "ghoul" },
        { "carnivore",       "greater_ghoul" },
        { "famine_lord",     "carnivore" },
        { "ancient_famine",  "famine_lord" },

        { "bone_ballista",   "bone_sniper" },
        { "skull_marksman",  "bone_ballista" },
        { "ancient_quiver",  "skull_marksman" },

        { "song_maiden",     "siren" },
        { "siren_queen",     "song_maiden" },
        { "ancient_song",    "siren_queen" },

        { "goblin_hunter",   "goblin_ranger" },
        { "hunt_king",       "goblin_hunter" },
        { "ancient_hunter",  "hunt_king" },

        { "orc_warlord",     "orc" },
        { "brawn_king",      "orc_warlord" },
        { "ancient_grip",    "brawn_king" },

        { "dark_assassin",   "dark_elf" },
        { "shadow_priest",   "dark_assassin" },
        { "dusk_king",       "shadow_priest" },
        { "ancient_dusk",    "dusk_king" },
        { "ancient_colossus",  "titanbeast" },
        { "ancient_fenrir",    "wolf_king" },
        { "ancient_conqueror", "warlord" },
        { "ancient_weaver",    "archmage" },
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
        // 👾 ユニークは進化ツリーの外。**引き当てた時点で使える**（研究では解禁しない）。
        if (UniqueCatalog.IsUnique(catalogIndex)) return true;
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
        // 👾 ユニークは進化段階を持たない。⚠ 深度倍率は素の強さに既に織り込んである。
        if (UniqueCatalog.IsUnique(catalogIndex)) return 0;
        string id = MinionCatalog.Get(catalogIndex).id;
        int d = 0;
        while (EvoFrom.TryGetValue(id, out var from)) { d++; id = from; }
        return d;
    }
    /// <summary>
    /// 🧬 進化段階そのものが持つ強化倍率（HP/ATK 共通）。
    ///
    /// **なぜ要るか**：カタログの hpMult/atkMult だけだと、タンク系の進化（スケルトン→ソルジャー）が
    /// hp 1.00→1.60 に対し **atk 1.00→1.05** で、攻撃がほぼ動かず「進化した実感が無い」状態だった。
    /// 段階に一律の倍率を持たせれば、どの分岐へ進んでも**必ず一段ぶんの手応え**がある。
    /// 基本0→最上位3 で ×1.00 → **×1.36**（カタログ側の倍率に上乗せ）。
    ///
    /// ⚠ これは**ターンではなくプレイヤーの投資**で駆動する軸（＝冒険者の装備グレードの対になるもの）。
    ///   ターン駆動の軸（個体Lv・魔王Lv）とは入力が違うので二重計上にはならない。
    ///
    /// ⚠⚠ **直線をやめて飽和させた**（旧 `1 + depth*0.12`＝段5で×1.60）。
    ///   T10〜T100の実測で、こちら側の掛け算の軸が5本とも伸び続けて**T90で16倍**になっていた
    ///   （→ [[curve-measurement-t100]]）。ただし**この倍率を消しはしない**：
    ///   消すと「タンク進化は攻撃がほとんど動かず、進化した実感が無い」という
    ///   元の問題がそのまま戻る。**一段ごとの手応えは残し、積み上がりだけを削る**。
    ///   段1の +12% は据え置き、段5で ×1.42（旧×1.60）。
    /// ⚠ 段を足したら**この表も一緒に伸ばす**（`Clamp` で最後の要素に落ちるので壊れはしないが、
    ///   新しい段が一つ前と同じ倍率になる）。
    /// </summary>
    private static readonly float[] depthMults = { 1.00f, 1.12f, 1.24f, 1.32f, 1.38f, 1.42f };
    public static float DepthMult(int catalogIndex)
        => depthMults[Mathf.Clamp(Depth(catalogIndex), 0, depthMults.Length - 1)];

    // ⚠ 上限を 3 で締めていたので、王種(depth4)・古代種(depth5) が m_evo3 で開いてしまっていた。
    //   段を足したら**ここの上限も一緒に上げる**（`m_evo1`〜`m_evo5` が実在すること）。
    public static string TierResearchId(int catalogIndex) => "m_evo" + Mathf.Clamp(Depth(catalogIndex), 1, 5);
    public static bool TierResearched(int catalogIndex) => ResearchState.IsResearched(TierResearchId(catalogIndex));
    public static string TierResearchName(int catalogIndex)
        => ResearchCatalog.TryGet(TierResearchId(catalogIndex), out var n) ? n.jpName : "";

    // 🧬 指定の種類から直接進化できる『子』のカタログindex一覧（分岐を含む）。個体進化UIで使う。
    public static List<int> ChildrenOf(int catalogIndex)
    {
        string parentId = MinionCatalog.Get(catalogIndex).id;
        var list = new List<int>();
        for (int k = 0; k < MinionCatalog.Count; k++)
        {
            if (EvoFrom.TryGetValue(MinionCatalog.Get(k).id, out var from) && from == parentId) list.Add(k);
        }
        return list;
    }

    // 🧬 個体進化として『その形態へ進化させられるか』＝研究段階が解禁済みか（親は個体自身が満たす）。
    public static bool CanIndividualEvolveTo(int childIndex) => TierResearched(childIndex);

    // 個体進化で新形態に到達したら、その種類も図鑑上で解禁済みにする（召喚可能に）。
    public static void MarkUnlocked(int catalogIndex)
    {
        EnsureInit();
        unlocked.Add(MinionCatalog.Get(catalogIndex).id);
    }

    /// <summary>
    /// 🌱 いま解禁できる（＝前提も研究も満たしている）種類の数。
    /// ⚠ 通しプレイ T1〜T30 で **召喚できる種類が T1 も T30 も 7 のまま**だった。
    ///   進化そのものは安い（段×25DP）ので、詰まっていたのは値段ではなく
    ///   **一度も指さされないこと**。腹心の報告（→ [[GuideSystem]]）がここを読む。
    /// </summary>
    public static int EvolvableCount()
    {
        EnsureInit();
        int n = 0;
        for (int i = 0; i < MinionCatalog.Count; i++) if (CanEvolve(i)) n++;
        return n;
    }

    /// <summary>解禁済みの種類の数（＝いま召喚できる手札の広さ）。</summary>
    public static int UnlockedCount()
    {
        EnsureInit();
        int n = 0;
        for (int i = 0; i < MinionCatalog.Count; i++) if (IsUnlocked(i) && !UniqueCatalog.IsUnique(i)) n++;
        return n;
    }

    // 今この配下を解禁できるか（未解禁＆進化元解禁済み＆該当段階が研究で開放済み）
    public static bool CanEvolve(int catalogIndex)
    {
        EnsureInit();
        if (IsUnlocked(catalogIndex)) return false;
        var from = PrereqId(catalogIndex);
        if (from == "" || !unlocked.Contains(from)) return false;
        return TierResearched(catalogIndex); // 🔬 魔物研究で進化段階が開放されて初めて可能
    }

    // 前提(進化元)は満たすが、研究段階が未開放で進化できない状態（図鑑UIの『研究で開放』表示用）
    public static bool TierResearchNeeded(int catalogIndex)
    {
        if (IsUnlocked(catalogIndex)) return false;
        var from = PrereqId(catalogIndex);
        if (from == "" || !unlocked.Contains(from)) return false;
        return !TierResearched(catalogIndex);
    }

    public static int EvolveCost(int catalogIndex)
    {
        return Mathf.RoundToInt(MinionCatalog.Get(catalogIndex).tierCP * EvolveCostPerTier * PolicySystem.EvolveCostMult);   // 🏛️ 政策『進化の秘術』
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
        Debug.Log($"🧬『配下進化』{MinionCatalog.Get(catalogIndex).jpName} を解禁（-{cost}DP）");
        return true;
    }
}
