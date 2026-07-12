using System.Collections.Generic;

/// <summary>
/// 獣ファミリーの見た目割当（Enemy Galore の完成スプライト／Animator）。
/// SPUMは人型のみなので獣はこちら。犬/狼スプライトが無いため、狼系は近いクリーチャーで代用（ユーザー合意）。
/// パスは Resources.Load 用（Assets/Resources/EnemyGalore/*.prefab = SpriteRenderer＋Animator＋Controller）。
/// 関連: [[SpumMap]] / CharacterVisual.InitBeast / ZombieAI。
/// </summary>
public static class BeastMap
{
    public struct BeastDef { public string prefab; public float scale; public bool faceLeft; }

    private static BeastDef B(string p, float s, bool fl = false) => new BeastDef { prefab = "EnemyGalore/" + p, scale = s, faceLeft = fl };

    // MinionCatalog.id → Enemy Galore クリーチャー。size感＝ティア相応にscale。
    private static readonly Dictionary<string, BeastDef> map = new Dictionary<string, BeastDef>
    {
        { "rat",         B("Rat", 1.7f) },              // 完全一致・小型
        { "bat",         B("Bat", 1.8f) },              // 完全一致（飛行）
        { "wolf",        B("SpikedSlime", 2.1f) },      // 速い/棘＝狼系代用
        { "harpy",       B("Bat", 2.0f) },              // 飛行遠隔＝コウモリ流用
        { "great_beast", B("Golem", 3.0f) },            // 巨躯の壁・大型
        { "dire_wolf",   B("Crab", 2.4f) },             // 獰猛な近接＝蟹で代用
        { "siren",       B("Skull", 2.3f) },            // 浮遊する妖＝スカル
        { "behemoth",    B("GolemReinforced", 3.6f) },  // 最大の装甲巨獣
        { "fenrir",      B("Golem", 3.2f) },            // 神狼＝大型ゴーレム(大きめ)で代用
    };

    public static bool TryGet(int catalogIndex, out BeastDef def)
    {
        var id = MinionCatalog.Get(catalogIndex).id;
        return map.TryGetValue(id, out def);
    }
}
