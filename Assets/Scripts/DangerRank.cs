using UnityEngine;

/// <summary>
/// ⚠️ 危険度（原作の迷宮等級：三級 → 二級 → 準一級 → 一級 → 特級）。
///
/// <para><b>なぜ要るか</b>：研究ツリーの深いノードに「RPさえ貯めれば開く」以外の壁が要る。
/// Civ VII の Mastery が「その時代を実際に生ききったか」を要求するのと同じ役割を、
/// 原作にある**迷宮の等級**で表す。ギルドがこちらをどう格付けしているか、という筋も通る。</para>
///
/// <para><b>設計</b>：0〜100点の合成。**5つの入力すべてが飽和する**ので、
/// ひとつの軸（名声など）を伸ばしただけでは特級にならない。
/// → [[difficulty-curve-orders]]「産出は必ず飽和する量に紐づける」と同じ考え方。
/// ⚠ 名声は O(turn²) で伸びるので**対数で**入れる。線形で入れると等級が一瞬で振り切れる。</para>
///
/// 参照側は <see cref="Level"/>（1〜5）だけ見ればよい。研究の解放条件は
/// <c>EraSystem.Cond.Danger</c> 経由でこの値を読む。
/// </summary>
public static class DangerRank
{
    public const int Max = 5;

    private static readonly string[] Names = { "三級", "二級", "準一級", "一級", "特級" };
    /// <summary>
    /// 等級の下限点（Level=1 は 0点から）。
    /// 実測の到達目安：三級T1／二級T10／準一級T20／一級T35／**特級T65前後**。
    /// ⚠ 上を 80 にすると T50 で特級に届いてしまい、80〜100ターンの後半で等級が動かなくなる。
    /// </summary>
    private static readonly int[] Thresholds = { 0, 20, 42, 64, 88 };

    /// <summary>いまの等級（1=三級 … 5=特級）。</summary>
    public static int Level
    {
        get
        {
            int s = Score;
            int lv = 1;
            for (int i = Max - 1; i >= 0; i--) if (s >= Thresholds[i]) { lv = i + 1; break; }
            return lv;
        }
    }

    public static string Name => Names[Mathf.Clamp(Level - 1, 0, Max - 1)];
    public static string NameOf(int level) => Names[Mathf.Clamp(level - 1, 0, Max - 1)];

    /// <summary>次の等級まであと何点か（最高位なら0）。</summary>
    public static int ToNext
    {
        get { int lv = Level; return lv >= Max ? 0 : Mathf.Max(0, Thresholds[lv] - Score); }
    }

    // ── 内訳（UIで「何が効いているか」を見せるため、個別に取れるようにしておく）──
    public static int FamePoints
    {
        get
        {
            var res = DungeonResourceManager.Instance;
            int fame = res != null ? res.DungeonFame : 0;
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(1f + fame / 50f) * 9f), 0, 30);
        }
    }
    public static int ThreatPoints
        => Mathf.Clamp(Mathf.RoundToInt((LureEconomy.Threat - 1f) * 5f), 0, 20);
    public static int FloorPoints
    {
        get
        {
            var fm = DungeonFloorManager.Instance;
            return Mathf.Clamp((fm != null ? fm.BuiltFloorCount : 0) * 5, 0, 25);
        }
    }
    public static int KillPoints
        => Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(1f + EurekaTracker.Count("kill") / 20f) * 6f), 0, 15);
    public static int RealmPoints
        => Mathf.Clamp(Mathf.RoundToInt(SurfaceMap.OwnedCount * 0.7f), 0, 10);

    public static int Score => FamePoints + ThreatPoints + FloorPoints + KillPoints + RealmPoints;

    /// <summary>UIの1行表示。「特級 92点」。</summary>
    public static string Short => Name + " <size=85%>" + Score + "点</size>";

    /// <summary>内訳のツールチップ用。</summary>
    public static string Detail
    {
        get
        {
            return "危険度 <b>" + Name + "</b>（" + Score + "/100点）\n"
                 + "名声 " + FamePoints + "／脅威度 " + ThreatPoints + "／階層 " + FloorPoints
                 + "／撃破 " + KillPoints + "／版図 " + RealmPoints + "\n"
                 + (Level >= Max ? "これ以上の等級はない。" : "次の『" + NameOf(Level + 1) + "』まであと " + ToNext + " 点。");
        }
    }
}
