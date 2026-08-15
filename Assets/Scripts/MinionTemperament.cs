using UnityEngine;

/// <summary>
/// 🧠 **配下の気性**（個体ごとの性格）。召喚したときに1つ決まり、**戦い方そのものが変わる**。
///
/// <para>
/// **なぜ要るか**：同じ種類・同じLvの配下は、これまで完全に同じ動きをしていた。
/// だから「どの個体を置くか」は Lv と装備を見るだけの作業で、**盤の上に人格が無かった**。
/// 気性は「誰を狙うか」「どこまで追うか」「どう殴るか」を1体ずつ変える。
/// </para>
///
/// <para>
/// ⚠⚠ **強さの軸を増やさない。** 気性は必ず**取引**（一方が上がれば一方が下がる）で、
///   12種の平均は HP ×0.99・攻撃 ×1.01 ＝ **ほぼ 1.0** に揃えてある。
///   ここを崩すと「当たりの気性を引くまで召喚し直す」ゲームになる（→ [[difficulty-curve-orders]]）。
/// </para>
///
/// <para>
/// ⚠ `Kind` の並びは `Individual.temper` としてセーブに載る。**末尾にだけ足すこと。**
/// </para>
///
/// 関連: [[MinionRoster]] [[ZombieAI]]／読みと噛み合う相手は [[omen-and-ward]]。
/// </summary>
public static class MinionTemperament
{
    public enum Kind
    {
        Brave = 0, Timid, Tenacious, Cunning, Loyal, Wild,
        Ferocious, Sluggish, Undaunted, Frantic, Serene, Greedy,
    }

    /// <summary>誰を狙うか。ここが「1体ずつ違う戦い方」のいちばん大きい部分。</summary>
    public enum Aim
    {
        Nearest,    // いちばん近い（既定）
        Strongest,  // いちばん強い相手へ突っ込む
        Weakest,    // いちばん弱った相手にとどめを刺す
        Caster,     // 術者（魔術師・聖職者）を先に潰す
        Sticky,     // 一度狙った相手を倒すまで変えない
    }

    public struct Def
    {
        public string jpName, desc, colorHex;
        public float hpMult, atkMult, spdMult, intervalMult;
        public Aim aim;
        public int leash;          // -1＝既定のまま。徘徊の半径（配置マスからどこまで離れるか）
        public float regenFrac;    // 毎秒 最大HPのこの割合を回復
        public float lowHpAtk;     // HPが0に近いほど攻撃に足される最大値（0.35＝最大+35%）
        public float lowHpSpeed;   // 同じく速度
        public float killDpMult;   // 倒したときのDP倍率
    }

    // ⚠ 数値は**取引**。片方を上げたらもう片方を下げる。平均が1.0から離れたら気性が「強化」になる。
    private static readonly Def[] defs =
    {
        T("勇猛", "いちばん<b>強い</b>相手へ真っ先に突っ込む。攻撃+10%／HP-8%。", "#e05a5a",
          0.92f, 1.10f, 1f, 1f, Aim.Strongest),
        T("臆病", "いちばん<b>弱った</b>相手を狙ってとどめを刺す。速度+15%／攻撃-8%。", "#8cb8e6",
          1.00f, 0.92f, 1.15f, 1f, Aim.Weakest),
        T("執念", "一度狙った相手を<b>倒すまで変えない</b>。HP+12%／速度-10%。", "#b48ce6",
          1.12f, 1.00f, 0.90f, 1f, Aim.Sticky),
        T("狡猾", "<b>術者</b>（魔術師・聖職者）を先に潰しに行く。攻撃+8%／HP-6%。", "#6ecf8e",
          0.94f, 1.08f, 1f, 1f, Aim.Caster),
        T("忠実", "置かれたマスから<b>ほとんど離れない</b>。関所向き。HP+15%／速度-15%。", "#e3a94a",
          1.15f, 1.00f, 0.85f, 1f, Aim.Nearest, 1),
        T("奔放", "<b>どこまでも追う</b>。逃がしたくないときに。速度+20%／HP-10%。", "#e0a05a",
          0.90f, 1.00f, 1.20f, 1f, Aim.Nearest, 7),
        T("獰猛", "手数で押す。攻撃間隔-18%／一撃-10%。", "#cf6e6e",
          1.00f, 0.90f, 1f, 0.82f, Aim.Nearest),
        T("鈍重", "遅いが重い。一撃+28%／攻撃間隔+22%・速度-8%。", "#9c95b4",
          1.00f, 1.28f, 0.92f, 1.22f, Aim.Nearest),
        T("不屈", "追い詰められるほど強くなる（瀕死で攻撃+35%）。素の攻撃-12%。", "#d8c98a",
          1.00f, 0.88f, 1f, 1f, Aim.Nearest, -1, 0f, 0.35f, 0f),
        T("狂騒", "傷つくほど速くなる（瀕死で速度+50%）。HP-10%。", "#e69ccf",
          0.90f, 1.00f, 1f, 1f, Aim.Nearest, -1, 0f, 0f, 0.50f),
        T("静謐", "毎秒 最大HPの0.6%を取り戻す。攻撃-10%。", "#57c3ab",
          1.00f, 0.90f, 1f, 1f, Aim.Nearest, -1, 0.006f),
        T("貪婪", "倒した相手から得るDPが+35%。HP-8%。", "#e3c94a",
          0.92f, 1.00f, 1f, 1f, Aim.Nearest, -1, 0f, 0f, 0f, 1.35f),
    };

    private static Def T(string n, string d, string col, float hp, float atk, float spd, float itv, Aim aim,
        int leash = -1, float regen = 0f, float lowAtk = 0f, float lowSpd = 0f, float killDp = 1f)
    {
        var x = new Def();
        x.jpName = n; x.desc = d; x.colorHex = col;
        x.hpMult = hp; x.atkMult = atk; x.spdMult = spd; x.intervalMult = itv;
        x.aim = aim; x.leash = leash; x.regenFrac = regen;
        x.lowHpAtk = lowAtk; x.lowHpSpeed = lowSpd; x.killDpMult = killDp;
        return x;
    }

    public static int Count { get { return defs.Length; } }
    public static Def Get(int i) { return defs[Mathf.Clamp(i, 0, defs.Length - 1)]; }
    public static string Name(int i) { return Get(i).jpName; }
    public static string Color(int i) { return Get(i).colorHex; }

    /// <summary>召喚のときに引く。</summary>
    public static int Roll() { return Random.Range(0, defs.Length); }

    /// <summary>
    /// 🔍 研究『見極め』：召喚のとき **2つ提示して選べる**。
    /// ⚠ 引き直しではなく**選択**にするのが肝。引き直せると「当たりが出るまで回す」になり、
    ///   平均1.0で釣り合わせた意味が消える。
    /// </summary>
    public static bool CanChoose { get { return ResearchState.IsResearched("m_temper1"); } }
    /// <summary>🪢 研究『調教』：既にいる個体の気性を振り直せる（DPを払う）。</summary>
    public static bool CanRetrain { get { return ResearchState.IsResearched("m_temper2"); } }
    public static int RetrainCost { get { return 450; } }
}
