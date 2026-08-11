using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🧬 **世界の変異**（M）。後半の難易度を**倍率ではなく「対策の要求」で作る**。
///
/// **なぜ要るか**：うちの後半は「冒険者が強くなる／増える」しか無い。
/// ところが [[difficulty-curve-orders]] のとおり**5つの入力はすべて飽和させてある**ので、
/// T60 を過ぎると盤面が平坦になる。数を増やせば重くなり、倍率を増やせば二次曲線になる。
/// **この2つのどちらでもない軸**が要る。
///
/// `Dungeon Defense: IoH` の**変異**がその答えだった（→ [[cdo2-and-dd-ioh-research]]）。
/// 向こうの終盤は「敵が強い」のではなく **`-75%物理` `-75%魔法` `敵防御+` といった
/// “いま組んでいるビルドを無効化する条件”** が積み上がる。プレイヤーは数字を伸ばすのではなく
/// **編成を組み替える**ことで応じる。対抗値 MGI は `効果 = 100% ÷ (1 + MGI%)`。
///
/// ## 設計
/// - T{FirstTurn} から**新しい変異が1つずつ**現れ、以後 {NewEvery} ターンごとに増える（全10種）。
/// - 各変異は**段**を持ち、時間とともに 1→5 まで濃くなる。
/// - 効き＝`1段あたりの量 × 段 ÷ (1 + 抑制)`。**抑制**は研究（順応／変異抑制＝反復可）で買う。
///
/// ⚠⚠ **新しい倍率の軸を増やしていない**。変異は既存の値を**削る**方向にしか働かず、
///   しかも段で上限が付いている。積の爆発が起きないのはそのため。
/// ⚠ **魔王は変異の影響を受けない**（守りも鎧化も彼には効かない）。
///   ＝ 物理も魔法も封じられたときの逃げ道が『親征』になる（→ [[lord-stance-devour]]）。
/// ⚠ 難易度で新しい掛け算を作らない。ペースだけ `Difficulty.AdvPowerMult` に相乗りさせる。
///
/// 純static・**セーブに載せる**。関連: [[Research]]（抑制の出どころ） [[AdventurerAI]] [[ZombieAI]]。
/// </summary>
public static class MutationSystem
{
    /// <summary>⚠ 並び順は `active` にそのまま保存される。**足すときは末尾へ**。</summary>
    public enum Kind
    {
        PhysWard,    // 物理の守り：物理攻撃が通りにくい
        MagicWard,   // 魔法の守り：魔法攻撃が通りにくい
        Ironhide,    // 鉄化：冒険者のHPが増える
        Curse,       // 呪詛：配下のHPが減る
        Swarm,       // 群れ：来る人数が増える
        Wary,        // 看破：罠が効きにくい
        Blight,      // 蝕み：回復が効かない
        Silence,     // 静寂：号令が重くなる
        Fleetfoot,   // 韋駄天：冒険者が速い
        Steadfast,   // 不屈：状態異常が短い
    }

    public struct Def
    {
        public Kind kind;
        public string jpName, desc, counter, colorHex;
        public float per;      // 1段あたりの効き（割合）
    }

    private static Def D(Kind k, string n, string d, string c, string col, float per)
        => new Def { kind = k, jpName = n, desc = d, counter = c, colorHex = col, per = per };

    // ⚠ `readonly` ＝ カタログの目印（[[SaveSystem]] は保存しない）。
    private static readonly Def[] defs =
    {
        D(Kind.PhysWard,  "物理の守り", "冒険者が**魔法でない攻撃**から受ける傷が減る。", "術者を混ぜる／罠と魔王で削る", "#c8ccd8", 0.12f),
        D(Kind.MagicWard,  "魔法の守り", "冒険者が**魔法**から受ける傷が減る。",           "物理の配下を前に出す",           "#8cb8e6", 0.12f),
        D(Kind.Ironhide,   "鉄化",       "冒険者の最大HPが増える。",                       "最大HP比で削る罠（貫通機構）",   "#b08040", 0.10f),
        D(Kind.Curse,      "呪詛",       "配下の最大HPが減る。",                           "防具の鍛造／装飾品『石守り』",   "#8f5fa8", 0.07f),
        D(Kind.Swarm,      "群れ",       "ウェーブに来る人数が増える。",                   "範囲の罠／自爆・咆哮",           "#e08a3c", 0.08f),
        D(Kind.Wary,       "看破",       "罠の威力が下がる。",                             "配下の頭数で殴る",               "#7ec46a", 0.14f),
        D(Kind.Blight,     "蝕み",       "配下の回復量が下がる。",                         "硬い前衛／不屈で耐える",         "#5f8f6a", 0.15f),
        D(Kind.Silence,    "静寂",       "号令のクールダウンが伸びる。",                   "号令に頼らない盤を組む",         "#9aa0b0", 0.15f),
        D(Kind.Fleetfoot,  "韋駄天",     "冒険者の移動が速くなる（罠と射程の中に居る時間が減る）。", "足止め（凍結・麻痺）",  "#5ad2e0", 0.08f),
        D(Kind.Steadfast,  "不屈",       "状態異常の持続が短くなる。",                     "持続でなく一撃で削る",           "#ffd24a", 0.14f),
    };

    public const int FirstTurn = 16;   // 最初の変異
    public const int NewEvery = 8;     // 何ターンごとに新しい変異が増えるか
    public const int StageEvery = 10;  // 何ターンごとに段が上がるか
    public const int MaxStage = 5;

    public static int Count => defs.Length;
    public static Def Get(Kind k) => defs[Mathf.Clamp((int)k, 0, defs.Length - 1)];
    public static Def GetAt(int i) => defs[Mathf.Clamp(i, 0, defs.Length - 1)];

    // ── 状態（セーブ対象）──
    private static List<int> active;        // 現れた変異（Kind の int）。現れた順
    private static List<int> appearTurn;    // それぞれが現れたターン
    private static int lastTurn = -1;

    private static void EnsureInit()
    {
        if (active == null) active = new List<int>();
        if (appearTurn == null) appearTurn = new List<int>();
    }
    public static void Reset() { active = new List<int>(); appearTurn = new List<int>(); lastTurn = -1; }

    public static int ActiveCount { get { EnsureInit(); return active.Count; } }
    public static Kind ActiveAt(int i) { EnsureInit(); return (Kind)active[Mathf.Clamp(i, 0, active.Count - 1)]; }
    public static bool IsActive(Kind k) { EnsureInit(); return active.Contains((int)k); }

    /// <summary>その変異の段（0＝出ていない）。時間で 1→{MaxStage} まで濃くなる。</summary>
    public static int StageOf(Kind k)
    {
        EnsureInit();
        int i = active.IndexOf((int)k);
        if (i < 0) return 0;
        int turn = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 1;
        // ⚖️ 難易度は**新しい掛け算を作らず**、段が上がる速さに相乗りさせるだけ
        float step = Mathf.Max(4f, StageEvery / Mathf.Max(0.5f, Difficulty.AdvPowerMult));
        return Mathf.Clamp(1 + Mathf.FloorToInt((turn - appearTurn[i]) / step), 1, MaxStage);
    }

    // ============ 🛡️ 抑制（MGI 相当） ============
    /// <summary>抑制率。研究『順応』『変異抑制（反復可）』で買う。0 なら変異はそのまま効く。</summary>
    public static float Suppress => Mathf.Max(0f, ResearchState.Sum(ResEffect.MutationSuppress));
    public static string SuppressLabel => "+" + Mathf.RoundToInt(Suppress * 100f) + "%";

    /// <summary>
    /// その変異の**実際に効いている量**（割合）。
    /// `量 = 1段あたり × 段 ÷ (1 + 抑制)`。⚠ 割り算にするのは、抑制をいくら積んでも
    /// **0 にはならない**から（引き算だと抑制を積むだけで変異が消え、判断が消える）。
    /// </summary>
    public static float Magnitude(Kind k)
    {
        int st = StageOf(k);
        if (st <= 0) return 0f;
        return Get(k).per * st / (1f + Suppress);
    }
    /// <summary>表示用（%）。</summary>
    public static int MagnitudePercent(Kind k) => Mathf.RoundToInt(Magnitude(k) * 100f);

    // ============ 📅 ターン処理 ============
    public static void OnTurnStart(int turn)
    {
        EnsureInit();
        if (lastTurn == turn) return;
        int prevTurn = lastTurn;
        lastTurn = turn;
        if (turn < FirstTurn) return;

        // 現れるべき数（T16で1つ、以後 NewEvery ごとに1つ）
        int should = Mathf.Min(defs.Length, 1 + (turn - FirstTurn) / NewEvery);
        while (active.Count < should)
        {
            int pick = PickNew(turn);
            if (pick < 0) break;
            active.Add(pick); appearTurn.Add(turn);
            var d = GetAt(pick);
            Debug.Log($"🧬『世界の変異』{d.jpName} が現れた ― {d.desc}");
            NotifySystem.Push("<b>世界の変異：" + d.jpName + "</b> ― " + StripBold(d.desc), NotifySystem.Kind.Story);
        }
        // 段が上がったものを知らせる（気づかないうちに効きが倍になっているのが一番よくない）
        if (prevTurn > 0)
            for (int i = 0; i < active.Count; i++)
            {
                var k = (Kind)active[i];
                if (StageOf(k) > StageAtTurn(i, prevTurn))
                    NotifySystem.Push("世界の変異 <b>" + Get(k).jpName + "</b> が第" + StageOf(k) + "段に濃くなった", NotifySystem.Kind.Info);
            }
    }

    private static int StageAtTurn(int idx, int turn)
    {
        float step = Mathf.Max(4f, StageEvery / Mathf.Max(0.5f, Difficulty.AdvPowerMult));
        return Mathf.Clamp(1 + Mathf.FloorToInt((turn - appearTurn[idx]) / step), 1, MaxStage);
    }

    /// <summary>
    /// 次に出す変異を選ぶ。⚠ **同じ順で並べない**（毎周まったく同じ順だと対策が定型化する）。
    /// 決定的にしたいのでターン数から乱数を作る（セーブ/ロードで変わらない）。
    /// </summary>
    private static int PickNew(int turn)
    {
        var pool = new List<int>();
        for (int i = 0; i < defs.Length; i++) if (!active.Contains(i)) pool.Add(i);
        if (pool.Count == 0) return -1;
        int seed = GameSetup.Seed * 31 + turn * 7919 + active.Count;
        var st = Random.state;
        Random.InitState(seed);
        int pick = pool[Random.Range(0, pool.Count)];
        Random.state = st;
        return pick;
    }

    private static string StripBold(string s) => s.Replace("**", "");

    // ============ 🔌 各所から読む窓口（参照はここだけ） ============

    /// <summary>⚔️ 防衛体が冒険者に与えるダメージの倍率。魔法かどうかで守りが変わる。</summary>
    public static float DefenderDamageMult(bool isMagic)
        => 1f - Magnitude(isMagic ? Kind.MagicWard : Kind.PhysWard);

    /// <summary>🛡️ 冒険者の最大HP倍率（鉄化）。</summary>
    public static float HeroHpMult => 1f + Magnitude(Kind.Ironhide);
    /// <summary>🏃 冒険者の移動速度倍率（韋駄天）。</summary>
    public static float HeroSpeedMult => 1f + Magnitude(Kind.Fleetfoot);
    /// <summary>💀 配下の最大HP倍率（呪詛）。</summary>
    public static float DefenderHpMult => 1f - Magnitude(Kind.Curse);
    /// <summary>👥 ウェーブ人数の倍率（群れ）。</summary>
    public static float WaveCountMult => 1f + Magnitude(Kind.Swarm);
    /// <summary>🪤 罠の威力倍率（看破）。</summary>
    public static float TrapPowerMult => 1f - Magnitude(Kind.Wary);
    /// <summary>💚 配下の回復量の倍率（蝕み）。</summary>
    public static float HealMult => 1f - Magnitude(Kind.Blight);
    /// <summary>📯 号令のクールダウン倍率（静寂）。</summary>
    public static float CommandCdMult => 1f + Magnitude(Kind.Silence);
    /// <summary>🧪 状態異常の持続倍率（不屈）。</summary>
    public static float StatusDurMult => 1f - Magnitude(Kind.Steadfast);

    // ============ 🖥️ 表示 ============
    public static string ShortLine(Kind k)
        => "<color=" + Get(k).colorHex + ">" + Get(k).jpName + "</color> 第" + StageOf(k) + "段 " + MagnitudePercent(k) + "%";

    /// <summary>ツールチップ／報告に出す一覧。</summary>
    public static string FullText()
    {
        EnsureInit();
        if (active.Count == 0)
            return "まだ世界は変異していない。<color=#9c95b4>第" + FirstTurn + "ターンから始まる。</color>";
        var sb = new System.Text.StringBuilder();
        sb.Append("<b>世界の変異</b>　<color=#9c95b4>抑制 ").Append(SuppressLabel).Append("（研究『順応』『変異抑制』で買える）</color>\n");
        for (int i = 0; i < active.Count; i++)
        {
            var k = (Kind)active[i];
            var d = Get(k);
            sb.Append("<color=").Append(d.colorHex).Append(">").Append(d.jpName).Append("</color> 第").Append(StageOf(k)).Append("段 ")
              .Append("<b>").Append(MagnitudePercent(k)).Append("%</b>　").Append(StripBold(d.desc))
              .Append("\n<size=88%><color=#6f6889>　対策：").Append(d.counter).Append("</color></size>");
            if (i < active.Count - 1) sb.Append("\n");
        }
        return sb.ToString();
    }
}
