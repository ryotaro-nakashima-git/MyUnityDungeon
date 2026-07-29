using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🎖️ 眷属＝指揮官（Civ VII の Commander）。C6。
///
/// Civ VII では司令官が **昇進を持ち、それが時代を越えて持ち越される**。ここでも同じで、
/// 眷属は戦うたびに **武勲(Merit)** を貯め、**4系統×3段の昇進**から選んで恒久的に強くなる。
/// 眷属は時代をまたいで生き続けるので、昇進もそのまま残る（＝育てた指揮官が資産になる）。
///
/// 併せて Civ の戦闘の作法も入れる:
/// - **支配地域(ZoC)** … 敵の拠点の隣で足が止まる（素通りできない）
/// - **側面(Flanking)** … 目標に隣接している味方の眷属の数だけ攻撃力が上がる
/// - **攻城(Siege)** … 砦や城壁の防衛を無視できるのは攻城の昇進を持つ者だけ
///
/// 関連: [[KinRoster]] [[EraSystem]] [[civ7-roadmap]]。
/// </summary>
public static class KinPromotion
{
    public struct Def
    {
        public string jpName, desc, colorHex;
        public int line;      // 系統 0進撃 / 1攻城 / 2統率 / 3渡航
        public int tier;      // 段 0..2（下の段を取ってから）
    }

    public static string LineName(int l) => l == 0 ? "進撃" : l == 1 ? "攻城" : l == 2 ? "統率" : "渡航";

    private static readonly Def[] defs =
    {
        // ── 進撃 ──
        P("疾駆",     "移動力 +1",                                   "#e3a94a", 0, 0),
        P("強襲",     "中立の領域への侵攻で 戦力 +20%",              "#e3a94a", 0, 1),
        P("電撃戦",   "移動力 さらに +2",                            "#e3a94a", 0, 2),
        // ── 攻城 ──
        P("破城槌",   "相手の砦による防衛を 50% 無視する",           "#b478e6", 1, 0),
        P("城塞破り", "遺産・自治都市の硬さ（+120以上の加算）を無視", "#b478e6", 1, 1),
        P("総攻め",   "側面（隣の味方眷属）の効果が 2倍になる",      "#b478e6", 1, 2),
        // ── 統率 ──
        P("号令",     "統率(LP) +8。より多くの配下を率いられる",     "#57c3ab", 2, 0),
        P("鼓舞",     "敗れたときに失う配下が半分になる",            "#57c3ab", 2, 1),
        P("軍旗",     "戦力 +15%",                                   "#57c3ab", 2, 2),
        // ── 渡航 ──
        P("沿岸航行", "研究がなくても 海を1マス越えられる",          "#8cb8e6", 3, 0),
        P("遠洋",     "海を2マス越えられる（遠き地へ届く）",         "#8cb8e6", 3, 1),
        P("不屈",     "負傷で動けないターンが半分になる",            "#8cb8e6", 3, 2),
    };

    private static Def P(string n, string d, string c, int line, int tier)
        => new Def { jpName = n, desc = d, colorHex = c, line = line, tier = tier };

    public static int Count => defs.Length;
    public static Def Get(int i) => defs[Mathf.Clamp(i, 0, defs.Length - 1)];

    // ============ 武勲と昇進 ============
    /// <summary>次の昇進に必要な武勲（取るほど高くなる）。</summary>
    public static int CostFor(KinRoster.Kin k) => 5 + 4 * (k.promotions != null ? k.promotions.Count : 0);

    public static bool Has(KinRoster.Kin k, int i) => k.promotions != null && k.promotions.Contains(i);

    /// <summary>その昇進を取れるか（同じ系統の1つ下の段が要る）。</summary>
    public static bool CanTake(KinRoster.Kin k, int i, out string why)
    {
        why = "";
        var d = Get(i);
        if (Has(k, i)) { why = "既に修めている"; return false; }
        if (d.tier > 0)
        {
            int prev = -1;
            for (int j = 0; j < defs.Length; j++) if (defs[j].line == d.line && defs[j].tier == d.tier - 1) prev = j;
            if (prev >= 0 && !Has(k, prev)) { why = "先に『" + Get(prev).jpName + "』を修める"; return false; }
        }
        if (k.merit < CostFor(k)) { why = "武勲が足りない（要" + CostFor(k) + "・所持" + k.merit + "）"; return false; }
        return true;
    }

    public static bool TryTake(KinRoster.Kin k, int i)
    {
        string why;
        if (!CanTake(k, i, out why)) { Debug.LogWarning("⚠️ " + why); return false; }
        k.merit -= CostFor(k);
        if (k.promotions == null) k.promotions = new List<int>();
        k.promotions.Add(i);
        Debug.Log($"🎖️『昇進』{k.trueName} が〈{Get(i).jpName}〉を修めた ― {Get(i).desc}");
        return true;
    }

    public static void AddMerit(KinRoster.Kin k, int n, string reason)
    {
        if (k == null || n <= 0) return;
        n = Mathf.Max(1, Mathf.RoundToInt(n * NarrativeSystem.MeritMult));   // 🕯️ 形見『旗手の遺品』
        k.merit += n;
        Debug.Log($"🎖️『武勲』{k.trueName} +{n}（{reason}）― 計{k.merit}／次の昇進に{CostFor(k)}");
    }

    // ============ 効果（KinRoster から参照する） ============
    public static int MoveBonus(KinRoster.Kin k) => (Has(k, 0) ? 1 : 0) + (Has(k, 2) ? 2 : 0);
    public static int LpBonus(KinRoster.Kin k) => Has(k, 6) ? 8 : 0;
    public static float PowerMult(KinRoster.Kin k) => Has(k, 8) ? 1.15f : 1f;
    public static float AssaultMult(KinRoster.Kin k) => Has(k, 1) ? 1.2f : 1f;   // 中立への侵攻
    public static float LossMult(KinRoster.Kin k) => Has(k, 7) ? 0.5f : 1f;
    public static float InjuryMult(KinRoster.Kin k) => Has(k, 11) ? 0.5f : 1f;
    public static int SeaCross(KinRoster.Kin k) => Has(k, 10) ? 2 : (Has(k, 9) ? 1 : 0);
    public static float FlankMult(KinRoster.Kin k) => Has(k, 5) ? 2f : 1f;

    /// <summary>⚔️ 攻城：相手の防衛のうち「砦」と「硬さの加算」をどれだけ無視できるか。</summary>
    public static int SiegeReduction(KinRoster.Kin k, SurfaceMap.Region target)
    {
        int cut = 0;
        if (Has(k, 3) && target.fortLevel > 0)
        {
            int[] fort = { 0, 120, 300, 560 };
            cut += Mathf.RoundToInt(fort[Mathf.Clamp(target.fortLevel, 0, 3)] * 0.5f);
        }
        if (Has(k, 4))
        {
            if (target.wonderIndex >= 0) cut += WonderCatalog.Get(target.wonderIndex).defenseBonus;
            foreach (var p in DiplomacySystem.Powers) if (p.regionId == target.id) { cut += 120; break; }
        }
        return cut;
    }

    /// <summary>
    /// 🗡️ 側面：目標に隣接している**味方の眷属の数**だけ強くなる（Civの Flanking）。
    /// 1体につき +12%、『総攻め』で倍。単騎で殴るより、寄ってたかるほうが強い。
    /// </summary>
    public static float FlankBonus(KinRoster.Kin k, int targetRegion)
    {
        int n = 0;
        foreach (var o in KinRoster.All)
        {
            if (o == k || o.injuryTurns > 0) continue;
            if (SurfaceMap.HexDist(SurfaceMap.Get(o.regionId), SurfaceMap.Get(targetRegion)) <= 1) n++;
        }
        if (n == 0) return 1f;
        return 1f + Mathf.Min(3, n) * 0.12f * FlankMult(k);
    }

    /// <summary>
    /// 🚧 支配地域(ZoC)：敵の拠点・本拠地の隣は素通りできない（Civの Zone of Control）。
    /// そこへ踏み込んだターンは足が止まる。
    /// </summary>
    public static bool InEnemyZoC(int regionId)
    {
        foreach (var n in SurfaceMap.Neighbors(regionId))
            if (n.IsRival && (n.settle != SurfaceMap.Settle.None || n.rivalHome >= 0)) return true;
        return false;
    }
}
