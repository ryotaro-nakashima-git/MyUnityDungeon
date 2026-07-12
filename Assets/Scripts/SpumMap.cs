using System.Collections.Generic;

/// <summary>
/// SPUM prefab 割当マップ（配下34種＋冒険者 職×ランク → Resources パス）。
///
/// - パスは Resources.Load 用（Assets/SPUM/Resources/ 起点）。無い種（獣/ゴースト特殊）は null → CharacterVisual が手続きリグへフォールバック。
/// - 割当は各prefabの実装備（武器/盾スプライト実測: 剣/弓/杖Ward/斧/二刀/盾）に基づく。同装備が1体しか無い場合は重複割当。
/// - ghost/wraith は骸骨術者を半透明化して幽体を表現（alpha）。
/// 関連: fable5-visual-brief.md §5 / [[MinionCatalog]] / CharacterVisual.InitSpum。
/// </summary>
public static class SpumMap
{
    private const string SK = "Addons/BasicPack/2_Prefab/Skelton/SPUM_20240911";
    private const string DV = "Addons/BasicPack/2_Prefab/Devil/SPUM_20240911";
    private const string HU = "Addons/BasicPack/2_Prefab/Human/SPUM_20240911";
    private const string EL = "Addons/BasicPack/2_Prefab/Elf/SPUM_20240911";

    // ---- 配下：MinionCatalog.id → prefabパス（null=手続きリグ据え置き）----
    private static readonly Dictionary<string, string> minion = new Dictionary<string, string>
    {
        // 🦴 不死（Skelton）
        { "skeleton",        SK + "215639833" }, // 剣
        { "zombie",          SK + "215640005" }, // 両盾＝鈍重な壁
        { "ghost",           SK + "215640091" }, // 杖・半透明(下のAlphaで)
        { "skeleton_archer", SK + "215639920" }, // 弓
        { "skeleton_soldier",SK + "215640266" }, // 盾＋剣
        { "ghoul",           SK + "222954869" }, // 斧＝狂乱
        { "wraith",          SK + "215640179" }, // 杖・半透明
        { "skeleton_knight", SK + "222907638" }, // 重武器
        { "bone_sniper",     SK + "215639920" }, // 弓（重複）
        { "lich",            SK + "215640091" }, // 杖（ghostの不透明版）
        { "death_knight",    SK + "222823174" }, // 二刀
        { "elder_lich",      SK + "215640179" }, // 杖上位（wraithの不透明版）

        // 😈 魔族（Devil）
        { "goblin",          DV + "215637961" }, // 素手＝基本形
        { "imp",             DV + "215641087" }, // 軽装
        { "goblin_archer",   DV + "222030968" }, // 弓
        { "hobgoblin",       DV + "215640476" }, // 戦士
        { "goblin_shaman",   DV + "215637772" }, // 杖
        { "kobold",          DV + "215640967" }, // 近接
        { "goblin_ranger",   DV + "222030968" }, // 弓（重複）
        { "goblin_soldier",  DV + "215640719" }, // 盾＋剣
        { "goblin_mage",     DV + "215637772" }, // 杖（重複）
        { "orc",             DV + "215640602" }, // 盾＋重武器
        { "dark_elf",        EL + "222451694" }, // 細身術士（Elf借用）
        { "goblin_general",  DV + "215640838" }, // 二刀＝将
        { "goblin_wizard",   DV + "215637878" }, // 二刀上位（杖が枯渇のため威圧重視）

        // 🐺 獣：SPUMに獣型なし → null（手続きリグ）。Dungeon Tale統合は後段。
        // rat/bat/wolf/harpy/great_beast/dire_wolf/siren/behemoth/fenrir → 未登録=null
    };

    // 幽体など半透明で出す種（id → alpha）
    private static readonly Dictionary<string, float> minionAlpha = new Dictionary<string, float>
    {
        { "ghost", 0.55f }, { "wraith", 0.6f },
    };

    public static string MinionPath(int catalogIndex)
    {
        var id = MinionCatalog.Get(catalogIndex).id;
        return minion.TryGetValue(id, out var p) ? p : null;
    }
    public static float MinionAlpha(int catalogIndex)
    {
        var id = MinionCatalog.Get(catalogIndex).id;
        return minionAlpha.TryGetValue(id, out var a) ? a : 1f;
    }

    // ---- 魔王：種族(Race) → prefabパス（未使用prefab優先で配下と差別化）。Slime=null→手続き粘体を維持 ----
    public static string DemonLordPath(DemonLord.Race race)
    {
        switch (race)
        {
            case DemonLord.Race.Oni: return DV + "215637878";     // 二刀＝鬼の豪腕
            case DemonLord.Race.Demon: return DV + "215640838";   // 二刀＝魔の威圧
            case DemonLord.Race.Elf: return EL + "222346858";     // 特殊武具のエルフ卿
            case DemonLord.Race.Dwarf: return HU + "215638981";   // 斧＝ドワーフ王
            case DemonLord.Race.Slime: return null;               // 粘体はSPUM不可→手続きblob
            case DemonLord.Race.Vampire: return EL + "222150076"; // 細剣の貴公子
            default: return HU + "215640352";                     // 人種＝盾の君主
        }
    }

    // ---- 冒険者：職×ランク(0..7 G..S) → prefabパス。3帯（G-D/C-B/A-S）で装備が良くなる ----
    public static string AdventurerPath(AdventurerAI.Job job, int rank)
    {
        int tier = rank <= 3 ? 0 : rank <= 5 ? 1 : 2; // 0=G-D, 1=C-B, 2=A-S
        switch (job)
        {
            case AdventurerAI.Job.Warrior:
                return tier == 0 ? HU + "215638389"   // 木盾＋剣1
                     : tier == 1 ? HU + "215638558"   // 木盾＋剣4
                     : HU + "215639580";              // 鋼盾＋剣5
            case AdventurerAI.Job.Thief:
                return tier == 0 ? HU + "215638643"   // 剣2軽装
                     : tier == 1 ? EL + "215638224"   // エルフ剣2
                     : EL + "222547665";              // 二刀
            case AdventurerAI.Job.Cleric:
                return tier == 0 ? HU + "215639405"   // 杖
                     : tier == 1 ? HU + "215639493"   // 杖
                     : HU + "215639748";              // 杖上位
            default: // Mage
                return tier == 0 ? EL + "222451694"   // 細身術士
                     : tier == 1 ? EL + "215638048"   // 盾＋杖
                     : EL + "222235923";              // 上位
        }
    }
}
