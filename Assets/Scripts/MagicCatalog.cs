using UnityEngine;

/// <summary>
/// 魔法システム（原作資料『魔法大辞典』の 属性 × 階級 を実装）。
///
/// - 属性(MagicElement)：**基本9＋派生7＝16種**。属性ごとに状態異常と相性（種族耐性）が違う。
///     基本：火/氷/雷/土/光/闇/水/風/無　派生：影/血/木/神聖/空間/時間/重力
///   ⚠ 派生は「2つの基本属性の合流」で解禁する（研究 `g_der_*` の前提が2つある＝合流ノード）。
///   ⚠⚠ **enumの並びはセーブに載りうるので末尾に足すこと。** 6種の時代の並びは動かさない。
/// - 階級(MagicRank)：最下級→下級→中級→上級→最上級（威力が段階的に跳ね上がる）。
/// - 使い手：
///     防衛側＝術者ロール(Buff/Debuff かつ Cast)の眷属。使える属性/階級は**魔法研究**で解禁する。
///     侵略側＝冒険者の魔法使い/聖職者。こちらは研究ではなく**冒険者ランク**で階級が上がる（世界が育つほど強い魔法）。
/// - 罠の状態異常(TrapKind)と同じ効果系に落として統合する（炎=DoT/氷=凍結/雷=麻痺…）。
/// 関連: [[TrapCatalog]] [[Research]] / ZombieAI(眷属の詠唱) / AdventurerAI(勇者の魔法)。
/// </summary>
public enum MagicElement
{
    Fire, Ice, Thunder, Earth, Light, Dark,   // ← ここまでが最初の6種。**並びを変えない**
    Water, Wind, Void,                        // 基本（追加）
    Shadow, Blood, Wood, Holy, Space, Time, Gravity   // 派生
}
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

    // 属性ごとの名前・色・状態異常（TrapKind と統一：1=毒,2=炎,3=氷,4=電気,5=出血／-1=なし）
    // ⚠ 3つの配列は**必ず同じ長さ**。`Make` が index で引くので、片方だけ足すと添字外で落ちる。
    private static readonly string[] elemName =
    { "火炎", "氷結", "雷撃", "地砕", "聖光", "呪詛", "水流", "疾風", "虚無", "影蝕", "血魔", "樹縛", "神聖", "空間", "時間", "重力" };
    private static readonly string[] elemColor =
    { "#e8622e", "#7fd3e6", "#ffd24a", "#a9754a", "#fff3c4", "#b048d0",
      "#4a8ce6", "#a0e6c8", "#6f6889", "#4a3f66", "#c04a6a", "#5aa84a", "#fff8e0", "#8c6ae6", "#d8c04a", "#7a6a8c" };
    // 火=炎DoT 氷=凍結 雷=麻痺 土=なし 光=なし 闇=毒(呪い)
    // 水=なし(押し流す) 風=なし 無=なし(耐性を無視するのが特性) 影=なし 血=出血 木=凍結(蔓で足止め)
    // 神聖=なし 空間=なし 時間=凍結(止める) 重力=麻痺(押し潰して鈍らせる)
    private static readonly int[] elemStatus =
    { 2, 3, 4, -1, -1, 1,  -1, -1, -1,  -1, 5, 3, -1, -1, 3, 4 };

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
        // 🕳️ 虚無：**あらゆる耐性を無視して等倍で通る**（研究『無の魔法』の説明どおり）。
        //    相性表を読む前に返すのが肝で、ここが属性を16に増やす一番の見返り。
        if (e == MagicElement.Void) return 1f;
        switch (fam)
        {
            case ZombieAI.Species.Undead:
                if (e == MagicElement.Holy) return 2.0f;    // 神聖＝聖光の上位。不死には天敵
                if (e == MagicElement.Light) return 1.7f;
                if (e == MagicElement.Wood) return 1.15f;   // 生命の側
                if (e == MagicElement.Dark) return 0.4f;
                if (e == MagicElement.Shadow) return 0.4f;
                if (e == MagicElement.Blood) return 0.35f;  // 流す血が無い
                if (e == MagicElement.Ice) return 0.8f;
                return 1f;
            case ZombieAI.Species.Beast:
                if (e == MagicElement.Fire) return 1.35f;
                if (e == MagicElement.Thunder) return 1.25f;
                if (e == MagicElement.Blood) return 1.3f;
                if (e == MagicElement.Gravity) return 1.25f; // 図体が大きいほど重みに弱い
                if (e == MagicElement.Water) return 1.15f;
                if (e == MagicElement.Earth) return 0.85f;
                if (e == MagicElement.Wood) return 0.7f;     // 森が住処
                if (e == MagicElement.Wind) return 0.85f;
                return 1f;
            default: // Demonkin
                if (e == MagicElement.Holy) return 1.8f;
                if (e == MagicElement.Light) return 1.5f;
                if (e == MagicElement.Water) return 1.15f;
                if (e == MagicElement.Fire) return 0.75f;
                if (e == MagicElement.Dark) return 0.55f;
                if (e == MagicElement.Shadow) return 0.5f;
                if (e == MagicElement.Blood) return 0.8f;
                return 1f;
        }
    }

    // 冒険者側の耐性（職で決まる）。聖職者は呪詛に強く、魔法使いは属性全般に少し強い。
    public static float ResistMultVsHero(MagicElement e, AdventurerAI.Job job)
    {
        if (e == MagicElement.Void) return 1f;                                        // 🕳️ 虚無は耐性を見ない
        if (job == AdventurerAI.Job.Cleric && (e == MagicElement.Dark || e == MagicElement.Shadow)) return 0.5f;
        if (job == AdventurerAI.Job.Cleric && e == MagicElement.Holy) return 0.6f;    // 神聖は同じ側の力
        if (job == AdventurerAI.Job.Warrior && e == MagicElement.Blood) return 1.25f; // 前に出る者ほど血を流す
        if (job == AdventurerAI.Job.Warrior && (e == MagicElement.Water || e == MagicElement.Gravity)) return 1.2f; // 重装は水と重みに弱い
        if (job == AdventurerAI.Job.Thief && e == MagicElement.Wood) return 1.25f;    // 速さで避ける者を縛る
        if (job == AdventurerAI.Job.Thief && e == MagicElement.Wind) return 0.8f;
        if (job == AdventurerAI.Job.Mage && e != MagicElement.Light) return 0.85f;
        if (job == AdventurerAI.Job.Warrior && e == MagicElement.Earth) return 0.8f;
        return 1f;
    }

    // ================= 研究による解禁（防衛側＝眷属の魔法）=================
    //  m_elem_* で属性を解禁、m_rank1/2 で使える階級の上限が上がる（既定は下級まで）。
    // ⚠ 属性を足したら**ここに必ず1行足す**。書き忘れると `default` の呪詛に落ちて、
    //   「研究したのに使えない／研究していないのに使える」が同時に起きる。
    private static readonly string[] elemResearch =
    { "g_elem_fire", "g_elem_ice", "g_elem_thunder", "g_elem_earth", "g_elem_light", "g_elem_dark",
      "g_elem_water", "g_elem_wind", "g_elem_void",
      "g_der_shadow", "g_der_blood", "g_der_wood", "g_der_holy", "g_der_space", "g_der_time", "g_der_gravity" };
    public static string ElementResearchId(MagicElement e)
    {
        int i = (int)e;
        return (i >= 0 && i < elemResearch.Length) ? elemResearch[i] : "g_elem_dark";
    }
    /// <summary>その属性が派生（2つの基本属性の合流で開く）かどうか。図鑑/研究UIの見出しに使う。</summary>
    public static bool IsDerived(MagicElement e) => (int)e >= (int)MagicElement.Shadow;
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
        // 👑 王種・古代種は**派生属性**を得意にする（段が上がる見返りを属性でも見せる）。
        //    ⚠ 得意属性が未解禁なら下の共通処理で解禁済みのものに落ちるので、ここは"希望"でよい。
        if (id == "bone_sovereign") return MagicElement.Shadow;
        if (id == "archmage") return MagicElement.Void;
        if (id == "ancient_ossuary") return MagicElement.Gravity;
        if (id == "ancient_weaver") return MagicElement.Time;    // 運命を糸として編む者
        if (id == "ghost" || id == "wraith" || id == "lich" || id == "elder_lich") return MagicElement.Dark;
        if (id == "siren") return MagicElement.Water;            // 水の妖。氷から水へ寄せた
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
        // ⚠⚠ **冒険者に派生属性・虚無を持たせない。** 派生は研究で開く"こちら側の到達点"で、
        //   相手にも配ると軸が1本増えて終盤だけ跳ねる（→ [[difficulty-curve-orders]]）。
        //   世界が育つ表現は既にランク・Lv・脅威度・装備でやっている。
        MagicElement e;
        if (job == AdventurerAI.Job.Cleric) e = MagicElement.Light;
        else
        {
            int pick = Random.Range(0, rankIdx >= 6 ? 4 : rankIdx >= 4 ? 3 : rankIdx >= 2 ? 2 : 1);
            e = pick == 0 ? MagicElement.Fire : pick == 1 ? MagicElement.Ice
              : pick == 2 ? MagicElement.Thunder : MagicElement.Water;   // 上位は水流まで
        }
        // G..S(0-7) → 最下級..最上級
        MagicRank r = rankIdx >= 7 ? MagicRank.Highest : rankIdx >= 5 ? MagicRank.High : rankIdx >= 3 ? MagicRank.Mid : rankIdx >= 1 ? MagicRank.Low : MagicRank.Lowest;
        spell = Make(e, r);
        return true;
    }
}
