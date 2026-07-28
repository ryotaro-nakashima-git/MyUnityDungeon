using UnityEngine;

/// <summary>
/// ★ 遺産（Civ の世界遺産に相当）。ただし**建てるのではなく、盤に「まれに在る」**。
///
/// Civ の世界遺産は「1つしか建たない」希少さが肝。この作品では盤の生成時に
/// **外周寄りのタイルへ2〜4個だけランダムに湧く**ことで、その希少さを「1つしか無い」に読み替える。
/// 遺産タイルは防衛が固く、支配すると**強い常時効果**が入る。効果はすべて迷宮側の数値に返るので、
/// 「地上を取る理由が地上で閉じない」。
///
/// 関連: [[SurfaceMap]] [[civ-surface-districts]]。
/// </summary>
public static class WonderCatalog
{
    public enum Kind { Kin, Research, Emotion, Defense, DP, Material, Trap, Forge }

    public struct Def
    {
        public string jpName, desc, colorHex;
        public Kind kind;
        public float value;
        public int defenseBonus;   // 遺産タイル自体の防衛加算（守りが固い）
    }

    private static readonly Def[] defs =
    {
        W("竜骨の尖塔",   "全ての眷属の統率(LP) +10",        Kind.Kin,      10f, 260, "#e0a94c"),
        W("星詠みの環",   "毎ターン 研究点 +4",              Kind.Research,  4f, 220, "#8cb8e6"),
        W("嘆きの大樹",   "感情の獲得 +25%",                 Kind.Emotion,  0.25f, 240, "#c04a6a"),
        W("不落の城壁",   "自領すべての守り +120",           Kind.Defense, 120f, 320, "#b478e6"),
        W("黄金の秤",     "領域の産出DP +40%",               Kind.DP,      0.40f, 200, "#e3c34a"),
        W("巨人の鉄床",   "毎ターン 素材 +6",                Kind.Material,  6f, 240, "#9aa3b0"),
        W("囁きの迷路",   "罠のダメージ +35%",               Kind.Trap,    0.35f, 210, "#e0703c"),
        W("賢者の炉",     "武具の鍛造費 -35%",               Kind.Forge,   0.35f, 230, "#57c3ab"),
    };

    private static Def W(string n, string d, Kind k, float v, int def, string col)
        => new Def { jpName = n, desc = d, kind = k, value = v, defenseBonus = def, colorHex = col };

    public static int Count => defs.Length;
    public static Def Get(int i) => defs[Mathf.Clamp(i, 0, defs.Length - 1)];

    /// <summary>自領として支配している遺産の、その種類の合計値。</summary>
    public static float OwnedSum(Kind k)
    {
        float v = 0f;
        foreach (var r in SurfaceMap.All)
            if (r.owned && r.wonderIndex >= 0 && Get(r.wonderIndex).kind == k) v += Get(r.wonderIndex).value;
        return v;
    }
    public static bool OwnsAny(Kind k) => OwnedSum(k) > 0f;

    // ---- 各システムが参照する窓口 ----
    public static int KinLPBonus => Mathf.RoundToInt(OwnedSum(Kind.Kin));
    public static int ResearchPerTurn => Mathf.RoundToInt(OwnedSum(Kind.Research));
    public static int MaterialPerTurn => Mathf.RoundToInt(OwnedSum(Kind.Material));
    public static float EmotionMult => 1f + OwnedSum(Kind.Emotion);
    public static int DefenseBonusAll => Mathf.RoundToInt(OwnedSum(Kind.Defense));
    public static float RegionDPMult => 1f + OwnedSum(Kind.DP);
    public static float TrapDamageMult => 1f + OwnedSum(Kind.Trap);
    public static float ForgeCostMult => Mathf.Max(0.3f, 1f - OwnedSum(Kind.Forge));

    /// <summary>毎ターンの直接産出（研究点・素材）。</summary>
    public static void Collect()
    {
        int rp = ResearchPerTurn, mat = MaterialPerTurn;
        if (rp > 0) ResearchState.AddRP(rp);
        if (mat > 0 && DungeonResourceManager.Instance != null) DungeonResourceManager.Instance.AddMaterial(mat);
        if (rp > 0 || mat > 0) Debug.Log($"★『遺産の恵み』+{rp}RP +{mat}素材");
    }
}
