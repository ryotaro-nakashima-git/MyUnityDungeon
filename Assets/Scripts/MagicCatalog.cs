using UnityEngine;

/// <summary>
/// 魔法システム（原作資料【魔法大辞典】の 属性 × 階級 を実装）。
///
/// - 属性(MagicElement)：火/氷/雷/土/光/闇。属性ごとに状態異常と相性（種族耐性）が違う。
/// - 階級(MagicRank)：最下級→下級→中級→上級→最上級（威力が段階的に跳ね上がる）。
/// - 使い手：
///     防衛側＝術者ロール(Buff/Debuff かつ Cast)の眷属。使える属性/階級は**魔法研究**で解禁する。
///     侵略側＝冒険者の魔法使い/聖職者。こちらは研究ではなく**冒険者ランク**で階級が上がる（世界が育つほど強い魔法）。
/// - 罠の状態異常(TrapKind)と同じ効果系に落として統合する（炎=DoT/氷=凍結/雷=麻痺…）。
/// 関連: [[TrapCatalog]] [[Research]] / ZombieAI(眷属の詠唱) / AdventurerAI(勇者の魔法)。
/// </summary>
public enum MagicElement { Fire, Ice, Thunder, Earth, Light, Dark }
public enum MagicRank { Lowest, Low, Mid, High, Highest } // 最下級/下級/中級/上級/最上級

public static class MagicCatalog
{
    public struct Spell
    {
        public MagicElement element;
        public MagicRank rank;
        public string jpName;      // 表示名（例: 中級 火炎）
        public float power;        // 基礎攻撃力への倍率
        public int trapStatus;     // 付与する状態異常（TrapKind と共通。-1=なし）
        public string colorHex;
    }

    // 階級ごとの威力と表示名
    private static readonly float[] rankPower = { 0.7f, 1.0f, 1.45f, 2.0f, 2.8f };
    private static readonly string[] rankName = { "最下級", "下級", "中級", "上級", "最上級" };
    public static string RankName(MagicRank r) => rankName[(int)r];
    public static float RankPower(MagicRank r) => rankPower[(int)r];

    // 属性ごとの名前・色・状態異常（TrapKind と統一：1=毒,2=炎,3=氷,4=電気,5=出血）
    private static readonly string[] elemName = { "火炎", "氷結", "雷撃", "地砕", "聖光", "呪詛" };
    private static readonly string[] elemColor = { "#e8622e", "#7fd3e6", "#ffd24a", "#a9754a", "#fff3c4", "#b048d0" };
    private static readonly int[] elemStatus = { 2, 3, 4, -1, -1, 1 }; // 火=炎DoT 氷=凍結 雷=麻痺 土=なし 光=なし 闇=毒(呪い)

    public static string ElementName(MagicElement e) => elemName[(int)e];
    public static string ElementColor(MagicElement e) => elemColor[(int)e];
    public static int ElementCount => elemName.Length;

    public static Spell Make(MagicElement e, MagicRank r)
    {
        var s = new Spell();
        s.element = e; s.rank = r;
        s.jpName = rankName[(int)r] + " " + elemName[(int)e];
        s.power = rankPower[(int)r];
        s.trapStatus = elemStatus[(int)e];
        s.colorHex = elemColor[(int)e];
        return s;
    }

    // ================= 相性（属性 × 眷属ファミリー）=================
    // 不死は聖光に弱く呪詛に強い／獣は火炎と雷撃に弱い／魔族は聖光に弱く火炎と呪詛に強い。
    public static float ResistMultVsMinion(MagicElement e, ZombieAI.Species fam)
    {
        switch (fam)
        {
            case ZombieAI.Species.Undead:
                if (e == MagicElement.Light) return 1.7f;
                if (e == MagicElement.Dark) return 0.4f;
                if (e == MagicElement.Ice) return 0.8f;
                return 1f;
            case ZombieAI.Species.Beast:
                if (e == MagicElement.Fire) return 1.35f;
                if (e == MagicElement.Thunder) return 1.25f;
                if (e == MagicElement.Earth) return 0.85f;
                return 1f;
            default: // Demonkin
                if (e == MagicElement.Light) return 1.5f;
                if (e == MagicElement.Fire) return 0.75f;
                if (e == MagicElement.Dark) return 0.55f;
                return 1f;
        }
    }

    // 冒険者側の耐性（職で決まる）。聖職者は呪詛に強く、魔法使いは属性全般に少し強い。
    public static float ResistMultVsHero(MagicElement e, AdventurerAI.Job job)
    {
        if (job == AdventurerAI.Job.Cleric && e == MagicElement.Dark) return 0.5f;
        if (job == AdventurerAI.Job.Mage && e != MagicElement.Light) return 0.85f;
        if (job == AdventurerAI.Job.Warrior && e == MagicElement.Earth) return 0.8f;
        return 1f;
    }

    // ================= 研究による解禁（防衛側＝眷属の魔法）=================
    //  m_elem_* で属性を解禁、m_rank1/2 で使える階級の上限が上がる（既定は下級まで）。
    public static string ElementResearchId(MagicElement e)
    {
        switch (e)
        {
            case MagicElement.Fire: return "g_elem_fire";
            case MagicElement.Ice: return "g_elem_ice";
            case MagicElement.Thunder: return "g_elem_thunder";
            case MagicElement.Earth: return "g_elem_earth";
            case MagicElement.Light: return "g_elem_light";
            default: return "g_elem_dark";
        }
    }
    public static bool IsElementUnlocked(MagicElement e) => ResearchState.IsResearched(ElementResearchId(e));

    // 眷属が使える最高階級（研究で上がる）
    public static MagicRank MinionRankCap()
    {
        if (ResearchState.IsResearched("g_rank3")) return MagicRank.Highest;
        if (ResearchState.IsResearched("g_rank2")) return MagicRank.High;
        if (ResearchState.IsResearched("g_rank1")) return MagicRank.Mid;
        return MagicRank.Low; // 既定＝下級まで
    }

    /// <summary>眷属術者が使う魔法を決める。解禁属性が無ければ false（＝通常攻撃のまま）。</summary>
    public static bool TryPickMinionSpell(int catalogIndex, out Spell spell)
    {
        spell = default(Spell);
        var def = MinionCatalog.Get(catalogIndex);
        // 術者ロール（支援/妨害）かつ詠唱スタイルのみ魔法を使う
        if (def.style != CharacterVisual.AttackStyle.Cast) return false;

        // 種族の得意属性を優先し、無ければ解禁済みの中から選ぶ
        var pref = PreferredElement(def.family, catalogIndex);
        MagicElement chosen = pref; bool found = IsElementUnlocked(pref);
        if (!found)
        {
            for (int i = 0; i < ElementCount; i++)
            {
                var e = (MagicElement)i;
                if (IsElementUnlocked(e)) { chosen = e; found = true; break; }
            }
        }
        if (!found) return false;

        // 階級＝研究上限とその個体のティアで決まる（強い種ほど高階級を扱える）
        int tier = def.tierCP;
        MagicRank byTier = tier >= 30 ? MagicRank.Highest : tier >= 20 ? MagicRank.High : tier >= 10 ? MagicRank.Mid : tier >= 5 ? MagicRank.Low : MagicRank.Lowest;
        MagicRank cap = MinionRankCap();
        MagicRank r = (MagicRank)Mathf.Min((int)byTier, (int)cap);
        spell = Make(chosen, r);
        return true;
    }

    // 種族/形態ごとの得意属性（フレーバー）
    public static MagicElement PreferredElement(ZombieAI.Species fam, int catalogIndex)
    {
        string id = MinionCatalog.Get(catalogIndex).id;
        if (id == "ghost" || id == "wraith" || id == "lich" || id == "elder_lich") return MagicElement.Dark;
        if (id == "siren") return MagicElement.Ice;
        if (id == "goblin_shaman") return MagicElement.Earth;
        if (id == "goblin_mage" || id == "goblin_wizard") return MagicElement.Fire;
        if (id == "imp") return MagicElement.Fire;
        if (id == "dark_elf") return MagicElement.Thunder;
        return fam == ZombieAI.Species.Undead ? MagicElement.Dark : fam == ZombieAI.Species.Beast ? MagicElement.Thunder : MagicElement.Fire;
    }

    // ================= 冒険者の魔法（研究ではなくランクで階級が上がる）=================
    public static bool TryPickHeroSpell(AdventurerAI.Job job, int rankIdx, out Spell spell)
    {
        spell = default(Spell);
        if (job != AdventurerAI.Job.Mage && job != AdventurerAI.Job.Cleric) return false;
        // 聖職者＝聖光、魔法使い＝火/氷/雷から（ランクが上がるほど多彩）
        MagicElement e;
        if (job == AdventurerAI.Job.Cleric) e = MagicElement.Light;
        else
        {
            int pick = Random.Range(0, rankIdx >= 4 ? 3 : rankIdx >= 2 ? 2 : 1);
            e = pick == 0 ? MagicElement.Fire : pick == 1 ? MagicElement.Ice : MagicElement.Thunder;
        }
        // G..S(0-7) → 最下級..最上級
        MagicRank r = rankIdx >= 7 ? MagicRank.Highest : rankIdx >= 5 ? MagicRank.High : rankIdx >= 3 ? MagicRank.Mid : rankIdx >= 1 ? MagicRank.Low : MagicRank.Lowest;
        spell = Make(e, r);
        return true;
    }
}
