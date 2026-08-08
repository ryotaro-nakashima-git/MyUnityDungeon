using UnityEngine;

/// <summary>
/// 🎨 UIの決まりごとを1箇所に集めたもの（Phase B-1）。
///
/// **なぜ要るか**：UIが素人っぽく見える原因を実測で洗ったら、装飾ではなく**規則の不在**だった。
///   1. 背景・パネル・カードが**ほぼ同じ明度**（#12101c / #191726 / #14121d）→ のっぺりする
///   2. 余白が **7,9,12,14,26…とバラバラ** → 揃っていないものは素人の絵に見える
///   3. 文字が **10〜13ptばかりで全部同じ重み** → どこを読めばいいか分からない
///   4. 色の意味（金=DP、青=研究…）が**場所ごとに違う**
///
/// ここを直せば、以降に作る画面は**何もしなくても揃う**。値は必ずここから引くこと。
/// ⚠ 直接 `C("#xxxxxx")` を書かない。書いた瞬間にまた規則が壊れる。
/// 関連: [[game-polish-plan]]。
/// </summary>
public static class UITheme
{
    public static Color C(string hex) { Color c; ColorUtility.TryParseHtmlString(hex, out c); return c; }

    // ============ 面（明度を3段に分ける＝奥行きが出る） ============
    public static readonly Color Bg      = C("#0b0910");   // 一番奥（画面の地）
    public static readonly Color Panel   = C("#1a1726");   // 窓
    public static readonly Color Panel2  = C("#252036");   // 窓の中の帯
    public static readonly Color Card    = C("#12101b");   // 窓の中のカード（沈める）
    public static readonly Color CardHi  = C("#2c2540");   // 選択中のカード
    public static readonly Color Hud     = C("#0a0810");   // 上下のバー

    // ============ 線 ============
    public static readonly Color Line    = C("#332e49");   // 目立たない仕切り
    public static readonly Color Line2   = C("#4a4268");   // 窓の縁
    public static readonly Color Focus   = C("#e3a94a");   // 選択中の縁

    // ============ 文字（4段。これ以外を使わない） ============
    public static readonly Color Text    = C("#ece8f5");
    public static readonly Color Muted   = C("#9c95b4");
    public static readonly Color Faint   = C("#6f6889");
    public const float H1 = 22f;   // 画面の見出し
    public const float H2 = 16f;   // 節の見出し
    public const float Body = 13.5f;
    public const float Small = 11.5f;

    // ============ 余白（8pxグリッド。半端な数を書かない） ============
    public const float S1 = 4f, S2 = 8f, S3 = 16f, S4 = 24f, S5 = 32f;

    // ============ 寸法 ============
    public const float BarH = 60f;        // 上下のバーの高さ
    public const float BtnH = 34f;        // 標準ボタン
    public const float BtnHS = 26f;       // 小ボタン
    public const float RowH = 34f;        // リストの1行
    public const float ScreenW = 1920f;   // CanvasScaler の基準

    // ============ 意味の色（**必ずここから引く**） ============
    public static readonly Color DP       = C("#e3a94a");   // 金＝ダンジョンポイント
    public static readonly Color Material = C("#57c3ab");   // 青緑＝素材
    public static readonly Color Research = C("#8cb8e6");   // 青＝研究点
    public static readonly Color Emotion  = C("#c04a6a");   // 紅＝感情
    public static readonly Color Fame     = C("#e05a5a");   // 赤＝名声（＝危うさ）
    public static readonly Color Influence= C("#b48be6");   // 紫＝威名
    public static readonly Color Food     = C("#5cc47c");   // 緑＝食料・良いこと
    public static readonly Color Danger   = C("#e08a3c");   // 橙＝警告
    public static readonly Color Blood    = C("#b0202b");   // 主要アクション

    public static string Hex(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }

    // ============ トランジション ============
    public const float FadeIn = 0.14f;      // パネルが開くときのフェード
    public const float CountUp = 0.45f;     // 数値が動くときのカウントアップ
    public const float PopLife = 1.6f;      // フロートテキスト

    /// <summary>数値の見せ方（3桁区切り）。UI全体で揃える。</summary>
    public static string Num(int v) { return v.ToString("N0"); }
    public static string Signed(int v) { return (v > 0 ? "+" : "") + v.ToString("N0"); }
}
