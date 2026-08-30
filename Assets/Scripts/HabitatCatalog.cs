using UnityEngine;

/// <summary>
/// 🌿 **環境**（生態系）。巣（旧スポナー）の**隣に置く**と、湧き方が変わる。
///
/// <para>
/// ⚠⚠ **なぜ要るか（実測）**：スポナーは強かった ―― 同じT1・同じ大招集の波で
///   「罠14＋隊5」が撃破9・取り逃がし1・DP+637 だったのに対し、
///   「スポナー9＋隊5」は **撃破15・取り逃がし0・DP+1,632・防衛体の損失0**。
///   なのに私も進言も一度も置かなかった。**強いのに浅い**からだ ――
///   置いた瞬間から永久に5体/波で、判断は「置くか置かないか」の一度きり。
///   湧いた個体はロスターに載らず、鍛造も進化も気性も乗らない。
///   ＝ この作品の芯（配下を育てる）と**1本も繋がっていなかった**。
/// </para>
///
/// <para>
/// <b>そこで、強さを足すのではなく素を下げて置き方で戻す。</b>
/// 巣の素は **5体/波 → 2体/波**。環境を隣に置くと戻る。
/// ⚠⚠ 環境も**配置枠を食う**ので、罠・トーテム・隊とゼロサム。
///   満額（巣1＋水源2）で 6体/波だが、そのために枠を3つ使う ―― **枠あたりの効率はむしろ下がる**。
///   得をするのは**盤を広げて巣を環境で囲めた者だけ**。これが「広さの報酬」。
/// ⚠ 掛け算の軸は増やしていない。動かすのは既にある3つ
///   （波あたりの上限・湧きの間隔・湧く個体のレベル）だけ。
/// </para>
///
/// <para>
/// ⚠ 効き方は**隣接**（マンハッタン距離 `Reach` 以内）。重ねがけは **2つまで**
///   （トーテムの `totemBuffMaxStack` と揃える。3つ目が効くと「並べるほど強い」の嘘になる）。
/// ⚠ index は `Feature.habitatKind` としてセーブに載る。**末尾にだけ足すこと。**
/// </para>
///
/// 関連: [[DungeonFeatureManager]]（巣の湧き） [[TotemCatalog]]（同じ隣接の作り）。
/// </summary>
public static class HabitatCatalog
{
    public enum Kind { Moss = 0, Spring = 1, Feed = 2 }

    /// <summary>効く範囲（マンハッタン距離）。⚠ 2なら巣の周りに置き場所が要る＝広さが要る。</summary>
    public const int Reach = 2;
    /// <summary>同じ種類の重ねがけ上限。⚠ トーテムと揃える。</summary>
    public const int MaxStack = 2;

    public struct Def
    {
        public Kind kind;
        public string jpName;
        public string desc;
        public string colorHex;
        public int dpCost;
    }

    private static readonly Def[] defs =
    {
        D(Kind.Moss,   "苔床", "湧きの<b>間隔が 25% 縮む</b>。波の早いうちから頭数が並ぶ。", "#6ecf8e", 120),
        D(Kind.Spring, "水源", "波あたりの<b>湧きの上限が +2</b>。長い波ほど効く。",         "#5aa8e0", 160),
        D(Kind.Feed,   "餌場", "湧く個体の<b>レベルが +25%</b>。数ではなく質。",             "#e0a05a", 180),
    };

    private static Def D(Kind k, string n, string d, string c, int dp)
        => new Def { kind = k, jpName = n, desc = d, colorHex = c, dpCost = dp };

    public static int Count { get { return defs.Length; } }
    public static Def Get(int i) { return defs[Mathf.Clamp(i, 0, defs.Length - 1)]; }
    public static Def Get(Kind k) { return defs[(int)k]; }
    public static string Name(int i) { return Get(i).jpName; }
    public static Color ColorOf(int i)
    {
        Color c; ColorUtility.TryParseHtmlString(Get(i).colorHex, out c); return c;
    }

    // ============ 効き目（既にある3つの数だけを動かす）============
    /// <summary>湧きの間隔の倍率。⚠ 苔床1つで 0.75、2つで 0.5625。</summary>
    public static float IntervalMult(int moss)
    {
        return Mathf.Pow(0.75f, Mathf.Clamp(moss, 0, MaxStack));
    }
    /// <summary>波あたりの湧きの上限に足す数。</summary>
    public static int ExtraPerWave(int spring) { return Mathf.Clamp(spring, 0, MaxStack) * 2; }
    /// <summary>湧く個体のレベル倍率。⚠ レベルそのものではなく、レベルの係数に掛ける。</summary>
    public static float LevelMult(int feed) { return 1f + 0.25f * Mathf.Clamp(feed, 0, MaxStack); }

    /// <summary>巣のツールチップに出す1文。⚠ 環境が0なら「素の巣」と正直に書く。</summary>
    public static string NestLine(int moss, int spring, int feed, int nestLevel, int perWave, float interval)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("🪺 巣 Lv").Append(nestLevel).Append(" ― <b>").Append(perWave).Append(" 体/波</b>・")
          .Append(interval.ToString("0.0")).Append("秒おき");
        if (moss + spring + feed == 0) { sb.Append("　<color=#6f6889>（環境なし＝素の巣）</color>"); return sb.ToString(); }
        sb.Append("　<color=#9c95b4>環境</color>");
        if (moss > 0) sb.Append(" <color=#6ecf8e>苔床×").Append(Mathf.Min(moss, MaxStack)).Append("</color>");
        if (spring > 0) sb.Append(" <color=#5aa8e0>水源×").Append(Mathf.Min(spring, MaxStack)).Append("</color>");
        if (feed > 0) sb.Append(" <color=#e0a05a>餌場×").Append(Mathf.Min(feed, MaxStack)).Append("</color>");
        return sb.ToString();
    }
}
