using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 👾 ユニーク魔物（旧「特殊敵」）。**ガチャでしか出ない、一体もので別格の個体**。
///
/// **なぜ別カタログか**：34種の配下は「召喚して並べる兵隊」で、進化ツリーの上を移動する。
/// ユニークはそこに属さない ―― 進化もしないし、量産もできない。**引き当てた1体がそのまま資産**になる。
/// 種としては別だが、**個体としては配下とまったく同じ扱い**にする
/// （個体ID・レベル・経験値・装備・図鑑での管理が全部そのまま効く）。
///
/// ⚠⚠ **`MinionRoster.Individual.catalogIndex` に載るのは `UniqueBase + i`**。
///   セーブに載る値なので **`UniqueBase` と defs の並び順は絶対に変えない**。
///   新しいユニークは必ず末尾に足すこと（→ [[districts-b1]] で同じ罠を踏んだ）。
///
/// 旧仕様では `GddMap.Special` の6種を**見た目だけ差し替えて**置いていたので、
/// レベルも装備も図鑑も効かず、育成の幹から切れていた。それをここで繋ぐ。
/// 関連: [[MinionCatalog]] [[MinionRoster]] [[DungeonFeatureManager]]。
/// </summary>
public static class UniqueCatalog
{
    /// <summary>
    /// 個体の `catalogIndex` に足すオフセット。これ以上ならユニーク。
    /// ⚠ 34種の後ろに素直に並べると、配下を増やしたときに衝突する。大きく空けておく。
    /// </summary>
    public const int UniqueBase = 1000;

    public static bool IsUnique(int catalogIndex) => catalogIndex >= UniqueBase;
    public static int LocalOf(int catalogIndex) => catalogIndex - UniqueBase;
    public static int GlobalOf(int localIndex) => UniqueBase + localIndex;

    /// <summary>ガチャでの出やすさ。**大きいほど出る**（相対重み）。</summary>
    public struct UniqueDef
    {
        public string id, jpName, desc;
        public ZombieAI.Species family;
        public MinionCatalog.Role role;
        public MinionCatalog.Rank rank;
        public int tierCP;
        public float hpMult, atkMult, spdMult;
        public CharacterVisual.AttackStyle style;
        public int weight;          // ガチャの重み（小さいほど激レア）
    }

    // ⚠⚠ 並び順を変えない。新しいユニークは末尾に足す。
    private static readonly UniqueDef[] defs =
    {
        U("koboild",   "コボイルド",   ZombieAI.Species.Demonkin, MinionCatalog.Role.Melee,  MinionCatalog.Rank.B, 26,
          2.2f, 2.4f, 1.30f, CharacterVisual.AttackStyle.Swing, 34,
          "群れの記憶を継いだ一体。数で押す種のはずが、単騎で戦列を割る。"),
        U("phantom",   "ファントム",   ZombieAI.Species.Undead,   MinionCatalog.Role.Debuff, MinionCatalog.Rank.A, 34,
          1.8f, 2.6f, 1.55f, CharacterVisual.AttackStyle.Cast,  22,
          "掴めぬもの。傷は通りにくく、触れられた者は力を失う。"),
        U("puppeteer", "パペッティア", ZombieAI.Species.Demonkin, MinionCatalog.Role.Buff,   MinionCatalog.Rank.A, 36,
          2.0f, 2.2f, 1.20f, CharacterVisual.AttackStyle.Cast,  20,
          "糸を引く者。周りの配下が別物のように動き出す。"),
        U("rattles",   "ラトルズ",     ZombieAI.Species.Undead,   MinionCatalog.Role.Tank,   MinionCatalog.Rank.B, 28,
          3.2f, 1.8f, 0.85f, CharacterVisual.AttackStyle.Punch, 30,
          "鳴り続ける骨。倒れても倒れても、また組み上がって前に立つ。"),
        U("speckle",   "スペックル",   ZombieAI.Species.Beast,    MinionCatalog.Role.Ranged, MinionCatalog.Rank.A, 32,
          1.7f, 2.8f, 1.75f, CharacterVisual.AttackStyle.Claw,  18,
          "斑の影。見えたときにはもう間合いの内にいる。"),
        U("valkyrie",  "ヴァルキリー", ZombieAI.Species.Demonkin, MinionCatalog.Role.Melee,  MinionCatalog.Rank.S, 46,
          2.8f, 3.4f, 1.45f, CharacterVisual.AttackStyle.Stab,   8,
          "堕ちた戦乙女。迷宮に降りた者の中で、最も戦を知っている。"),

        // ── ⛓️ 転向者（→ [[Prison]]）──
        // ⚠ **`weight = 0`**。ガチャからは絶対に出ない。牢で折れた冒険者だけがここへ来る。
        //   （`SummonGacha` の抽選は `acc += weight; if (roll < acc)` なので、0のものは選ばれない）
        // ⚠ 一体ものである点はユニークと同じなので、あえて同じカタログに置いて幹に乗せている。
        //   別カタログを作ると Lv・装備・図鑑・盤の絵の配線を全部もう一度書くことになる。
        // ⚠⚠ 並び順＝セーブに載る `catalogIndex`。**この4つより後ろにしか足さない**。
        U("turncoat_knight", "堕ちた騎士",   ZombieAI.Species.Demonkin, MinionCatalog.Role.Tank,   MinionCatalog.Rank.A, 34,
          2.6f, 2.0f, 0.95f, CharacterVisual.AttackStyle.Swing, 0,
          "剣を捧げた元戦士。守るものが変わっただけで、腕は変わらない。"),
        U("turncoat_hand",   "影の手",       ZombieAI.Species.Beast,    MinionCatalog.Role.Melee,  MinionCatalog.Rank.B, 28,
          1.6f, 2.6f, 1.70f, CharacterVisual.AttackStyle.Stab,  0,
          "牢を三度破ろうとした盗賊。四度目に、こちら側から鍵を開けることにした。"),
        U("turncoat_priest", "背教の司祭",   ZombieAI.Species.Undead,   MinionCatalog.Role.Buff,   MinionCatalog.Rank.A, 32,
          1.9f, 1.9f, 1.00f, CharacterVisual.AttackStyle.Cast,  0,
          "祈る相手を替えた聖職者。同じ言葉が、逆向きに効く。"),
        U("turncoat_scholar","禁書の徒",     ZombieAI.Species.Demonkin, MinionCatalog.Role.Ranged, MinionCatalog.Rank.A, 33,
          1.6f, 2.9f, 1.15f, CharacterVisual.AttackStyle.Cast,  0,
          "地上では焼かれる本を、ここでは読ませてもらえる。それだけで足りた。"),
    };

    /// <summary>⛓️ 職から転向者の local index を引く（→ [[Prison]]）。⚠ 上の並びと1対1。</summary>
    public static int TurncoatLocalFor(AdventurerAI.Job job)
    {
        switch (job)
        {
            case AdventurerAI.Job.Warrior: return IndexOf("turncoat_knight");
            case AdventurerAI.Job.Thief: return IndexOf("turncoat_hand");
            case AdventurerAI.Job.Cleric: return IndexOf("turncoat_priest");
            default: return IndexOf("turncoat_scholar");
        }
    }
    /// <summary>ガチャに出ない種か（＝転向者）。一覧や確率表示から外すのに使う。</summary>
    public static bool IsTurncoat(int local) => local >= 0 && local < defs.Length && defs[local].weight <= 0;

    private static UniqueDef U(string id, string jp, ZombieAI.Species fam, MinionCatalog.Role role, MinionCatalog.Rank rank,
        int tier, float hp, float atk, float spd, CharacterVisual.AttackStyle style, int weight, string desc)
        => new UniqueDef
        {
            id = id, jpName = jp, family = fam, role = role, rank = rank, tierCP = tier,
            hpMult = hp, atkMult = atk, spdMult = spd, style = style, weight = weight, desc = desc,
        };

    public static int Count => defs.Length;
    public static UniqueDef Get(int local) => defs[Mathf.Clamp(local, 0, defs.Length - 1)];
    public static UniqueDef GetByGlobal(int catalogIndex) => Get(LocalOf(catalogIndex));
    public static int IndexOf(string id)
    {
        for (int i = 0; i < defs.Length; i++) if (defs[i].id == id) return i;
        return -1;
    }
    public static int TotalWeight
    {
        get { int n = 0; foreach (var d in defs) n += d.weight; return n; }
    }

    /// <summary>
    /// `MinionCatalog.MinionDef` に変換する。
    /// ⚠ 既存のコードは全部 `MinionCatalog.Get(index)` を通って強さも名前も見ている。
    ///   ここで同じ型に変換して返せば、**呼ぶ側を1行も変えずに**ユニークが幹に乗る。
    /// </summary>
    public static MinionCatalog.MinionDef AsMinionDef(int local)
    {
        var d = Get(local);
        return new MinionCatalog.MinionDef
        {
            id = d.id, jpName = d.jpName, family = d.family, role = d.role, rank = d.rank,
            tierCP = d.tierCP, hpMult = d.hpMult, atkMult = d.atkMult, spdMult = d.spdMult,
            rig = MinionCatalog.RigOfFamily(d.family), style = d.style, spumHint = "", note = d.desc,
        };
    }
}
