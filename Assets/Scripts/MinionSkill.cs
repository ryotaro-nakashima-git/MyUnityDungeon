using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 魔物スキル（原作資料『魔物スキル一覧』/アビリティ一覧 を実装）。
///
/// - 形態(34種)ごとに 1〜2 個のスキルを持たせ、『倍率違いだけ』だった配下に個性を与える。
/// - Tier1 は常時有効、Tier2 は魔物研究『魔物スキル解禁』で有効になる（研究ツリーと連動）。
/// - 効果は ZombieAI 側のフックで実挙動化する（再生/棘/群れ/威圧/不屈/自爆/石化/治癒…）。
/// 関連: [[MinionCatalog]] [[Research]] [[MagicCatalog]] / ZombieAI。
/// </summary>
public enum MinionSkillKind
{
    None,
    Regen,        // 再生：毎秒わずかに自己回復
    PackTactics,  // 群れ：周囲の味方が多いほど攻撃力上昇
    Thorns,       // 棘の皮膚：被弾時に反射ダメージ
    PoisonBody,   // 毒身：殴ってきた相手を毒に侵す
    Intimidate,   // 威圧：周囲の冒険者の与ダメを下げる
    Swift,        // 俊敏：移動と攻撃が速い
    Undying,      // 不屈：致死ダメージを一度だけ耐える
    SelfDestruct, // 自爆：死亡時に周囲へ大ダメージ
    PetrifyGaze,  // 石化の眼光：攻撃時に確率で相手を停止させる
    HealAura,     // 治癒の波動：周期的に周囲の味方を回復
    Roar,         // 咆哮：戦闘開始時に周囲の味方を強化
    Lifedrain,    // 吸命：与ダメの一部を吸収（魔族の吸血より強力）
}

public static class MinionSkill
{
    public struct Def { public MinionSkillKind kind; public string jpName; public string desc; public bool tier2; }

    private static Def D(MinionSkillKind k, string n, string d, bool t2) { var x = new Def(); x.kind = k; x.jpName = n; x.desc = d; x.tier2 = t2; return x; }

    // スキル定義（表示名・説明・Tier）
    private static readonly Dictionary<MinionSkillKind, Def> defs = new Dictionary<MinionSkillKind, Def>
    {
        { MinionSkillKind.Regen,        D(MinionSkillKind.Regen,        "再生",       "毎秒 最大HPの2%を回復する。",                     false) },
        { MinionSkillKind.PackTactics,  D(MinionSkillKind.PackTactics,  "群れ",       "周囲の味方1体につき攻撃+12%（最大+60%）。",        false) },
        { MinionSkillKind.Thorns,       D(MinionSkillKind.Thorns,       "棘の皮膚",   "被弾時、受けたダメージの25%を相手に返す。",        false) },
        { MinionSkillKind.PoisonBody,   D(MinionSkillKind.PoisonBody,   "毒身",       "殴ってきた冒険者を毒状態にする。",                 false) },
        { MinionSkillKind.Swift,        D(MinionSkillKind.Swift,        "俊敏",       "移動速度+25%・攻撃間隔-20%。",                     false) },
        { MinionSkillKind.Lifedrain,    D(MinionSkillKind.Lifedrain,    "吸命",       "与えたダメージの25%を自己回復する。",              false) },
        { MinionSkillKind.Intimidate,   D(MinionSkillKind.Intimidate,   "威圧",       "周囲の冒険者の与ダメージを20%下げる。",            true) },
        { MinionSkillKind.Undying,      D(MinionSkillKind.Undying,      "不屈",       "致死ダメージを一度だけHP1で耐える。",              true) },
        { MinionSkillKind.SelfDestruct, D(MinionSkillKind.SelfDestruct, "自爆",       "死亡時、周囲の冒険者へ大ダメージ。",               true) },
        { MinionSkillKind.PetrifyGaze,  D(MinionSkillKind.PetrifyGaze,  "石化の眼光", "攻撃時20%で相手を短時間停止させる。",              true) },
        { MinionSkillKind.HealAura,     D(MinionSkillKind.HealAura,     "治癒の波動", "3秒ごとに周囲の味方を回復する。",                  true) },
        { MinionSkillKind.Roar,         D(MinionSkillKind.Roar,         "咆哮",       "出現時、周囲の味方の攻撃を15%強化する。",          true) },
    };

    public static Def Get(MinionSkillKind k) => defs.ContainsKey(k) ? defs[k] : D(MinionSkillKind.None, "―", "", false);
    public static string Name(MinionSkillKind k) => Get(k).jpName;

    // 形態ID → スキル（最大2つ）
    private static readonly Dictionary<string, MinionSkillKind[]> byId = new Dictionary<string, MinionSkillKind[]>
    {
        // 🦴 不死：粘り・呪い・再生
        { "skeleton",         new[]{ MinionSkillKind.PackTactics } },
        { "zombie",           new[]{ MinionSkillKind.Regen, MinionSkillKind.Undying } },
        { "ghost",            new[]{ MinionSkillKind.Intimidate } },
        { "skeleton_archer",  new[]{ MinionSkillKind.PackTactics } },
        { "skeleton_soldier", new[]{ MinionSkillKind.Thorns, MinionSkillKind.Undying } },
        { "ghoul",            new[]{ MinionSkillKind.Lifedrain, MinionSkillKind.Regen } },
        { "wraith",           new[]{ MinionSkillKind.Intimidate, MinionSkillKind.PetrifyGaze } },
        { "skeleton_knight",  new[]{ MinionSkillKind.Thorns, MinionSkillKind.Roar } },
        { "bone_sniper",      new[]{ MinionSkillKind.Swift } },
        { "lich",             new[]{ MinionSkillKind.HealAura, MinionSkillKind.Intimidate } },
        { "death_knight",     new[]{ MinionSkillKind.Lifedrain, MinionSkillKind.Roar } },
        { "elder_lich",       new[]{ MinionSkillKind.HealAura, MinionSkillKind.Undying } },

        // 🐺 獣：速さ・群れ・体当たり
        { "rat",              new[]{ MinionSkillKind.PackTactics } },
        { "bat",              new[]{ MinionSkillKind.Swift } },
        { "wolf",             new[]{ MinionSkillKind.PackTactics, MinionSkillKind.Swift } },
        { "harpy",            new[]{ MinionSkillKind.Swift } },
        { "great_beast",      new[]{ MinionSkillKind.Thorns, MinionSkillKind.Roar } },
        { "dire_wolf",        new[]{ MinionSkillKind.PackTactics, MinionSkillKind.Lifedrain } },
        { "siren",            new[]{ MinionSkillKind.Intimidate, MinionSkillKind.PetrifyGaze } },
        { "behemoth",         new[]{ MinionSkillKind.Thorns, MinionSkillKind.Undying } },
        { "fenrir",           new[]{ MinionSkillKind.Swift, MinionSkillKind.Roar } },

        // 😈 魔族：吸血・毒・術
        { "goblin",           new[]{ MinionSkillKind.PackTactics } },
        { "imp",              new[]{ MinionSkillKind.Swift } },
        { "goblin_archer",    new[]{ MinionSkillKind.PackTactics } },
        { "hobgoblin",        new[]{ MinionSkillKind.Lifedrain } },
        { "goblin_shaman",    new[]{ MinionSkillKind.HealAura } },
        { "kobold",           new[]{ MinionSkillKind.PoisonBody } },
        { "goblin_ranger",    new[]{ MinionSkillKind.Swift, MinionSkillKind.PetrifyGaze } },
        { "goblin_soldier",   new[]{ MinionSkillKind.Thorns, MinionSkillKind.Roar } },
        { "goblin_mage",      new[]{ MinionSkillKind.Intimidate } },
        { "orc",              new[]{ MinionSkillKind.Lifedrain, MinionSkillKind.Undying } },
        { "dark_elf",         new[]{ MinionSkillKind.PoisonBody, MinionSkillKind.Intimidate } },
        { "goblin_general",   new[]{ MinionSkillKind.Roar, MinionSkillKind.Thorns } },
        { "goblin_wizard",    new[]{ MinionSkillKind.HealAura, MinionSkillKind.PetrifyGaze } },
    };

    // Tier2 スキルの解禁研究
    public const string Tier2ResearchId = "m_skill2";
    public static bool Tier2Unlocked => ResearchState.IsResearched(Tier2ResearchId);

    /// <summary>その形態のスキル一覧（研究未解禁のTier2は includeLocked=false で除外）。</summary>
    public static List<MinionSkillKind> Of(int catalogIndex, bool includeLocked = true)
    {
        var list = new List<MinionSkillKind>();
        string id = MinionCatalog.Get(catalogIndex).id;
        if (!byId.ContainsKey(id)) return list;
        foreach (var k in byId[id])
        {
            if (!includeLocked && Get(k).tier2 && !Tier2Unlocked) continue;
            list.Add(k);
        }
        return list;
    }

    /// <summary>実効（＝いま戦闘で効く）スキルを持つか。</summary>
    public static bool Has(int catalogIndex, MinionSkillKind kind)
    {
        foreach (var k in Of(catalogIndex, false)) if (k == kind) return true;
        return false;
    }

    /// <summary>UI表示用の短い文字列（ロック中は淡色マーク）。</summary>
    public static string Label(int catalogIndex)
    {
        var all = Of(catalogIndex, true);
        if (all.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var k in all)
        {
            var d = Get(k);
            bool locked = d.tier2 && !Tier2Unlocked;
            sb.Append(locked ? "<color=#6f6889>・" + d.jpName + "</color> " : "<color=#57c3ab>◆" + d.jpName + "</color> ");
        }
        return sb.ToString();
    }
}
