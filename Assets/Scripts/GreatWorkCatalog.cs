using UnityEngine;

/// <summary>
/// 🏛️ **巨大施設**（X-1）。**5×5 の空いた床**が要る大構造。
///
/// <para>
/// ⚠⚠ **なぜ 5×5 なのか（実測）**：各サイズで迷宮を **40回生成**し、
///   空いた正方形が1か所でも取れる確率を数えた。
///
///   | 盤 | 3×3 | 4×4 | **5×5** | 6×6 |
///   |---|---|---|---|---|
///   | 10×10 | 100% | 42% | **0%** | 0% |
///   | 20×20 | 100% | 100% | **57%** | 30% |
///   | 30×30 | 100% | 95%↑ | **95%** | 62% |
///
///   ＝ 5×5 だけが **10×10 で 0%**、広げると現れる。
///   ⚠ **4×4 は門にならない**（10×10 でも 42% 置ける）。
///     最初 4×4 にしたのは**1回しか生成を見ていなかったから**で、
///     たまたま 0 か所の盤を引いていた。**確率は複数回まわして測る。**
/// </para>
///
/// <para>
/// ⚠ 20×20 でも 57% ＝ 広げても取れないことがある。これは意図的で、
///   そのときは **『掘る』で自分で空間を作る**（→ [[Excavation]]）。
///   ＝ 面積・地形工事・巨大施設の3つが1本に繋がる。
/// </para>
///
/// <para>
/// ⚠⚠ **見返りは「掛け算」ではなく「数」にする。** 通しプレイ7周で分かったのは、
///   壁を動かしたのは**恒久的な頭数**だった（配下 2体 → 7体 で T14 → T17）。
///   一方で**広さは頭数に一切繋がっていなかった** ―― `SquadMaxSlots` は
///   研究・政策・属性でしか伸びず、面積にも階層にも連動しない
///   （→ [[growth-is-a-trap]] [[nest-and-habitat]]）。
///   だから巨大施設の本命は **『練兵場』＝その階の隊枠 +1**。
///   ＝ **面積を、実際に勝ちに繋がる唯一の軸に変換する装置**。倍率の軸は1本も増えない。
/// </para>
///
/// <para>
/// ⚠ 種類は**末尾に足す**こと（`trapKind` に載ってセーブに入る → [[SaveSystem]]）。
/// </para>
/// 関連: [[DungeonFeatureManager]] [[HabitatCatalog]] [[growth-is-a-trap]]。
/// </summary>
public static class GreatWorkCatalog
{
    public enum Kind { DrillGround = 0, GreatNest = 1 }

    /// <summary>⚠ **一辺のマス数。** 10×10 では置けない値であることに意味がある（上の表）。</summary>
    public const int Size = 5;

    public struct Def
    {
        public string jpName;
        public string desc;
        public int dpCost;
        public string colorHex;
    }

    // ⚠ readonly ＝ カタログ＝セーブに載せない（→ [[SaveSystem]]）
    private static readonly Def[] defs =
    {
        new Def {
            jpName = "練兵場", colorHex = "#8cb8e6", dpCost = 900,
            desc = "この階の<b>隊の枠 +1</b>。広さを、周を通して育つ頭数に変える唯一の建物。"
        },
        new Def {
            jpName = "大巣", colorHex = "#b48ce6", dpCost = 1200,
            desc = "素で <b>4体/波</b> 湧く巣。<b>環境が3マス先まで届く</b>ので、囲んで育てやすい。"
        },
    };

    public static int Count { get { return defs.Length; } }
    public static Def Get(int i) { return defs[Mathf.Clamp(i, 0, defs.Length - 1)]; }
    public static string Name(int i) { return Get(i).jpName; }
    public static Color ColorOf(int i)
    {
        Color c;
        return ColorUtility.TryParseHtmlString(Get(i).colorHex, out c) ? c : Color.white;
    }

    /// <summary>🪺 大巣の素の湧き（→ `DungeonFeatureManager.NestPerWave`）。ふつうの巣は 2。</summary>
    public const int GreatNestBasePerWave = 4;
    /// <summary>🌿 大巣に環境が届く距離（ふつうの巣は `HabitatCatalog.Reach` ＝2）。</summary>
    public const int GreatNestReach = 3;

    /// <summary>盤に乗せたときの1行。</summary>
    public static string Line(int kind)
    {
        var d = Get(kind);
        return "🏛️ <color=" + d.colorHex + ">" + d.jpName + "</color> ― " + d.desc;
    }
}
