using UnityEngine;

/// <summary>
/// ⚖️ 難易度（Phase F-22）。**世界設定と一緒に、始める前に選ぶ**（[[GameSetup]]）。
///
/// ## 何を動かすか（＝何を動かさないか）
/// 難易度で**仕組みそのものは変えない**。動かすのは4つの掛け算だけにしてある。
/// - 冒険者の**質**（レベルの伸び）と**量**（湧く人数）
/// - 他魔王の**伸び**
/// - こちらの**取り分**（撃破DP・名声）
/// これ以外（研究の値段・建造費・配置枠）は据え置く。**同じ攻略が同じように通じる**ようにしたいから。
/// ⚠ [[difficulty-curve-orders]] の原則どおり、**掛け算の軸を増やさない**。
///
/// ## スコア倍率
/// 難しいほど戦績([[RunStats]])のスコアが伸びる。**低難易度で稼いだ記録が上位を占めない**ようにするため。
/// </summary>
public static class Difficulty
{
    public struct Def
    {
        public string jpName, desc, colorHex;
        public float advPower;    // 冒険者のレベルの伸び
        public float advCount;    // 湧く人数
        public float rivalGrow;   // 他魔王の伸び
        public float reward;      // 撃破DP・名声の取り分
        public float score;       // 戦績のスコア倍率
    }

    private static readonly Def[] defs =
    {
        D("安寧", "腰を据えて仕組みを覚えたいとき。世は緩やかにしか本気にならない。", "#5cc47c", 0.80f, 0.80f, 0.70f, 1.15f, 0.6f),
        D("標準", "設計どおりの手応え。迷えばこれ。",                                 "#e3a94a", 1.00f, 1.00f, 1.00f, 1.00f, 1.0f),
        D("苛烈", "世が早く本気になる。泳がせる余裕は減り、判断が要る。",             "#e08a3c", 1.18f, 1.15f, 1.25f, 1.00f, 1.5f),
        D("絶望", "最初から追われている。取り分は増えるが、間違えれば戻せない。",     "#b0202b", 1.38f, 1.30f, 1.55f, 1.12f, 2.2f),
    };
    private static Def D(string n, string d, string c, float ap, float ac, float rg, float rw, float sc)
        => new Def { jpName = n, desc = d, colorHex = c, advPower = ap, advCount = ac, rivalGrow = rg, reward = rw, score = sc };

    public static int Count { get { return defs.Length; } }
    public static Def Get(int i) { return defs[Mathf.Clamp(i, 0, defs.Length - 1)]; }
    public static Def Current { get { return Get(GameSetup.DifficultyIdx); } }
    public static string CurrentName { get { return Current.jpName; } }

    // 各systemはここだけを見る
    public static float AdvPowerMult { get { return Current.advPower; } }
    public static float AdvCountMult { get { return Current.advCount; } }
    public static float RivalGrowMult { get { return Current.rivalGrow; } }
    public static float RewardMult { get { return Current.reward; } }
    public static float ScoreMult { get { return Current.score; } }
}
