using UnityEngine;

/// <summary>
/// ゴエティア（原作資料『ソロモン72柱』）。ボスに任命した個体へ『魔神の名と階級』を継がせる。
///
/// - 72柱それぞれに階級（王/公爵/侯爵/伯爵/君主/総裁/騎士）があり、階級ごとに授かる加護が違う。
/// - 個体IDから決定的に割り当てるので、同じ個体は常に同じ魔神名を名乗る（付け替わらない）。
/// - ボス（各階1体）と特殊エネミーに適用。単なる『ボス』だった存在に固有名と個性を与える。
/// 関連: [[MinionRoster]] [[DungeonFeatureManager]] [[MagicCatalog]]。
/// </summary>
public static class GoetiaCatalog
{
    // 階級（授かる加護の型）
    public enum Rank { King, Duke, Marquis, Earl, Prince, President, Knight }

    public struct Pillar { public string jpName; public Rank rank; }
    private static Pillar P(string n, Rank r) { var p = new Pillar(); p.jpName = n; p.rank = r; return p; }

    // ソロモン72柱（序列順）
    private static readonly Pillar[] pillars =
    {
        P("バエル", Rank.King),           P("アガレス", Rank.Duke),        P("ウァサゴ", Rank.Prince),
        P("サミギナ", Rank.Marquis),      P("マルバス", Rank.President),   P("ウァレフォル", Rank.Duke),
        P("アモン", Rank.Marquis),        P("バルバトス", Rank.Duke),      P("パイモン", Rank.King),
        P("ブエル", Rank.President),      P("グシオン", Rank.Duke),        P("シトリー", Rank.Prince),
        P("ベレト", Rank.King),           P("レラジェ", Rank.Marquis),     P("エリゴス", Rank.Duke),
        P("ゼパル", Rank.Duke),           P("ボティス", Rank.Earl),        P("バティン", Rank.Duke),
        P("サレオス", Rank.Duke),         P("プルソン", Rank.King),        P("マラクス", Rank.Earl),
        P("イポス", Rank.Earl),           P("アイム", Rank.Duke),          P("ナベリウス", Rank.Marquis),
        P("グラシャ＝ラボラス", Rank.Earl), P("ブネ", Rank.Duke),          P("ロノウェ", Rank.Marquis),
        P("ベリス", Rank.Duke),           P("アスタロト", Rank.Duke),      P("フォルネウス", Rank.Marquis),
        P("フォラス", Rank.President),    P("アスモデウス", Rank.King),    P("ガープ", Rank.Prince),
        P("フルフル", Rank.Earl),         P("マルコシアス", Rank.Marquis), P("ストラス", Rank.Prince),
        P("フェニックス", Rank.Marquis),  P("ハルファス", Rank.Earl),      P("マルファス", Rank.President),
        P("ラウム", Rank.Earl),           P("フォカロル", Rank.Duke),      P("ウェパル", Rank.Duke),
        P("サブナック", Rank.Marquis),    P("シャックス", Rank.Marquis),   P("ヴィネ", Rank.King),
        P("ビフロンス", Rank.Earl),       P("ウヴァル", Rank.Duke),        P("ハーゲンティ", Rank.President),
        P("クロケル", Rank.Duke),         P("フルカス", Rank.Knight),      P("バラム", Rank.King),
        P("アロケス", Rank.Duke),         P("カイム", Rank.President),     P("ムルムル", Rank.Duke),
        P("オロバス", Rank.Prince),       P("グレモリー", Rank.Duke),      P("オセ", Rank.President),
        P("アミー", Rank.President),      P("オリアス", Rank.Marquis),     P("ヴァプラ", Rank.Duke),
        P("ザガン", Rank.King),           P("ウァラク", Rank.Prince),      P("アンドラス", Rank.Marquis),
        P("フラウロス", Rank.Duke),       P("アンドレアルフス", Rank.Marquis), P("キメリエス", Rank.Marquis),
        P("アムドゥスキアス", Rank.Duke), P("ベリアル", Rank.King),        P("デカラビア", Rank.Marquis),
        P("セーレ", Rank.Prince),         P("ダンタリオン", Rank.Duke),    P("アンドロマリウス", Rank.Earl),
    };

    public static int Count => pillars.Length;
    public static Pillar Get(int i) => pillars[Mathf.Abs(i) % pillars.Length];

    public static string RankName(Rank r)
    {
        switch (r)
        {
            case Rank.King: return "王";
            case Rank.Duke: return "公爵";
            case Rank.Marquis: return "侯爵";
            case Rank.Earl: return "伯爵";
            case Rank.Prince: return "君主";
            case Rank.President: return "総裁";
            default: return "騎士";
        }
    }
    public static string RankColor(Rank r)
    {
        switch (r)
        {
            case Rank.King: return "#ffd24a";
            case Rank.Duke: return "#b48be6";
            case Rank.Marquis: return "#df5a5a";
            case Rank.Earl: return "#57c3ab";
            case Rank.Prince: return "#8cb8e6";
            case Rank.President: return "#5cc47c";
            default: return "#9c95b4";
        }
    }

    // 階級ごとの加護（ボスに乗る追加倍率）
    public static float HpMult(Rank r)
    {
        switch (r)
        {
            case Rank.King: return 1.45f;
            case Rank.Duke: return 1.25f;
            case Rank.Knight: return 1.35f;
            case Rank.Earl: return 1.10f;
            default: return 1.15f;
        }
    }
    public static float AtkMult(Rank r)
    {
        switch (r)
        {
            case Rank.King: return 1.40f;
            case Rank.Marquis: return 1.35f;
            case Rank.Duke: return 1.20f;
            case Rank.Prince: return 1.15f;   // 魔法寄り（術者なら魔法威力にも乗る）
            case Rank.President: return 1.10f;
            default: return 1.15f;
        }
    }
    public static float SpeedMult(Rank r) => r == Rank.Earl ? 1.30f : r == Rank.Marquis ? 1.12f : 1f;

    public static string Blessing(Rank r)
    {
        switch (r)
        {
            case Rank.King: return "王の威光（HP+45% 攻+40%）";
            case Rank.Duke: return "公爵の統率（HP+25% 攻+20%）";
            case Rank.Marquis: return "侯爵の武（攻+35% 速+12%）";
            case Rank.Earl: return "伯爵の疾さ（速+30% HP+10%）";
            case Rank.Prince: return "君主の秘術（攻+15%・魔法が伸びる）";
            case Rank.President: return "総裁の智慧（HP+15% 攻+10%）";
            default: return "騎士の守り（HP+35%）";
        }
    }

    /// <summary>個体IDから決定的に柱を割り当てる（同じ個体は常に同じ魔神名）。</summary>
    public static int PillarIndexFor(int individualId) => Mathf.Abs(individualId * 7919 + 13) % pillars.Length;

    /// <summary>表示用『バエル〈王〉』。</summary>
    public static string TitleOf(int individualId)
    {
        var p = Get(PillarIndexFor(individualId));
        return p.jpName + "〈" + RankName(p.rank) + "〉";
    }
    public static string RichTitleOf(int individualId)
    {
        var p = Get(PillarIndexFor(individualId));
        return "<color=" + RankColor(p.rank) + ">" + p.jpName + "〈" + RankName(p.rank) + "〉</color>";
    }
    public static Pillar PillarOf(int individualId) => Get(PillarIndexFor(individualId));
}
