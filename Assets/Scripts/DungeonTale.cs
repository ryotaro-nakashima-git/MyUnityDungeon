using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 🧱 Dungeon Tale（16px ピクセルアート素材）への入口。
///
/// ## なぜ差し替えたか
/// 迷宮は長らく**手続き生成の石畳を1マス1枚**貼るだけで、**壁が1枚も無かった**。
/// 床でない所には何も無い＝カメラの青がそのまま見えていて、「本格的なダンジョン」に見えない
/// 最大の原因がこれだった。手持ちの `Dungeon Tale` は
/// - **Wall が RuleTile（18ルール）**＝オートタイルが**絵を描かずに手に入る**（Phase C の宿題）
/// - 床9種・小物60種以上・デカール30種以上・Item/Char
/// を持っていたので、そのまま採用した。
///
/// ## 使い方
/// `Resources/DungeonTale/Atlas`（185スプライト・16PPU・Point）を名前で引く。
/// ⚠ アトラスは**スライス済み**なので、`Resources.LoadAll&lt;Sprite&gt;` で全部取れる。
/// ⚠ スプライトによって 15px / 16px と大きさが違う（`Arrow_Shot` など）。
///    タイルとして使うのは 16px の物だけにすること。
/// 関連: [[DungeonTilemapView]] [[DungeonGridSystem]] [[game-polish-plan]]。
/// </summary>
public static class DungeonTale
{
    private static Dictionary<string, Sprite> map;
    private static RuleTile wallRule;
    private static bool loaded;

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        map = new Dictionary<string, Sprite>();
        var all = Resources.LoadAll<Sprite>("DungeonTale/Atlas");
        foreach (var s in all) if (s != null && !map.ContainsKey(s.name)) map[s.name] = s;
        wallRule = Resources.Load<RuleTile>("DungeonTale/Wall");
        if (all.Length == 0) Debug.LogWarning("🧱 Dungeon Tale のアトラスが読めない（Resources/DungeonTale/Atlas）");
    }

    public static bool Available { get { EnsureLoaded(); return map != null && map.Count > 0; } }

    /// <summary>名前で1枚引く（無ければ null）。</summary>
    public static Sprite S(string name)
    {
        EnsureLoaded();
        Sprite s;
        return map.TryGetValue(name, out s) ? s : null;
    }

    public static RuleTile WallRule { get { EnsureLoaded(); return wallRule; } }

    // ============ 決まった役どころ ============
    /// <summary>
    /// 🪨 部屋の床。⚠ `Floor_A..D` は**青緑のタイル**で、いくら色を掛けても青緑のまま
    /// （掛け算では色を足せない）。`Floor_Metal` は**灰色**なので、掛けた色がそのまま出る。
    /// 世界観に合わせて色を決めたいときは「灰色の素材を選ぶ」のが近道。
    /// </summary>
    public static readonly string[] FloorRoom = { "Floor_Metal" };
    public const string FloorCorridor = "Floor_Dirt";

    /// <summary>🪑 部屋に散らす小物。踏んでも何も起きない飾りだけを選んである。</summary>
    public static readonly string[] Props =
    {
        "Prop_Vase_A", "Prop_Vase_B", "Prop_Vase_C", "Prop_Vase_D", "Prop_Vase_E",
        "Prop_Bone", "Prop_Skull", "Prop_Web", "Prop_Candles", "Prop_Shrooms",
        "Prop_Root_A", "Prop_Root_B", "Prop_Root_C", "Prop_Root_D",
        "Prop_Chain_A", "Prop_Chain_B", "Prop_Pipe_a", "Prop_Pipe_B",
        "Env_Grave", "Env_GraveA", "Env_GraveB", "Env_Sign", "Env_Chess_Small",
    };

    /// <summary>
    /// 🩸 床に散らすもの。
    /// ⚠ アトラスの `Decal_*` は**汚れではなく血糊とマーカー**（赤いX・矢印・魔法陣）だった。
    ///    床の grunge のつもりで全面に撒いたら**赤い記号だらけ**になったので、
    ///    「冒険者を食う迷宮」に合う血の跡だけを、ごく低い確率で置く。
    ///    `Decal_ShadeA/B` は**キャラの足元の影**であって壁の影ではない（黒い塊が並ぶ）。
    /// </summary>
    public static readonly string[] Bloods = { "Splatter", "Decal_Spot" };

    public const string Chest = "Env_Chess_A";        // 宝箱（アトラスの綴りは Chess）
    public const string ChestOpen = "Env_Chess_C";
    public const string StairsDown = "Env_Ladder_Down";
    public const string StairsUp = "Env_Ladder_Up";
    public const string Altar = "Env_Altar";          // トーテム
    public const string Trap = "Spikes";
    public const string TrapActive = "Spikes_Act";
    public const string Torch = "Prop_Candles";

    // 🎨 素材の色（砂色の壁＋青緑の床）はこのゲームの暗い紫の世界観と合わない。
    //    絵を描き直す代わりに **Tilemap ごと色を掛ける**。空間テーマの色をここに乗せられる。
    public static readonly Color WallTint = new Color(0.30f, 0.26f, 0.42f);   // 岩＝暗く、床より沈める
    public static readonly Color FloorTint = new Color(0.56f, 0.50f, 0.68f);  // 歩ける所＝明るく
    public static readonly Color PropTint = new Color(0.80f, 0.72f, 0.86f);

    /// <summary>🎲 場所で決まる乱数（同じマスは何度組み直しても同じ見た目になる）。</summary>
    public static int Hash(int x, int y, int salt)
    {
        unchecked
        {
            int h = x * 73856093 ^ y * 19349663 ^ salt * 83492791;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) & 0x7fffffff;
        }
    }

    public static Sprite Pick(string[] names, int x, int y, int salt)
    {
        if (names == null || names.Length == 0) return null;
        return S(names[Hash(x, y, salt) % names.Length]);
    }
}
