using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🔥 他の魔王（原作: 1都市に約60人の魔王が居て互いに真核を奪い合う）＝4Xの eXterminate。
///
/// - 3人の魔王がそれぞれ**本拠地（真核のある支配領域）**を持ち、毎ターン成長しながら領域を広げる。
/// - **こちらの領域も奪いに来る**（領域の逆襲）。放置すると産出が削られ、最後は迷宮前まで押し込まれる。
/// - **本拠地を落とすと真核を奪える**＝その魔王を排除。持っていた領域は中立に戻り、大きな戦利品が入る。
/// - 原作準拠: 真核を奪えるのは魔王（カオス）だけ。人間側は領域を『奪還』するだけで真核までは取れない。
///
/// 純static・実行時保持（ドメインリロードで初期化）。関連: [[SurfaceMap]] [[KinRoster]] [[novel-canon]]。
/// </summary>
public static class RivalLords
{
    public class Rival
    {
        public string name;
        public string title;         // 種族＋二つ名（原作の『鬼種の魔王カンタ』のような呼び方）
        public string colorHex;
        public float power;          // 現在の軍事力（毎ターン成長）
        public float growth;         // 1ターンの成長量
        public int aggression;       // 侵攻の積極性（1ターンに仕掛ける回数の目安）
        public bool defeated;
        public int homeRegion = -1;
        public string lastAction = "";
    }

    private static List<Rival> rivals;
    private static void EnsureInit() { if (rivals == null) Build(); }

    public static void Reset() { rivals = null; EnsureInit(); }

    private static void Build()
    {
        rivals = new List<Rival>
        {
            new Rival { name = "カンタ",  title = "鬼種の魔王",   colorHex = "#e05a5a", power = 240f, growth = 20f, aggression = 1 },
            new Rival { name = "アリサ",  title = "妖精種の魔王", colorHex = "#57c3ab", power = 400f, growth = 28f, aggression = 1 },
            new Rival { name = "ヴェルグ", title = "龍種の魔王",  colorHex = "#b478e6", power = 680f, growth = 38f, aggression = 1 },
        };
        // 本拠地を割り当てる（SurfaceMap 側の Domain 領域）
        int[] homes = { 16, 17, 18 };
        for (int i = 0; i < rivals.Count; i++)
        {
            rivals[i].homeRegion = homes[i];
            SurfaceMap.AssignRivalHome(homes[i], i);
        }
    }

    public static int Count { get { EnsureInit(); return rivals.Count; } }
    public static Rival Get(int i) { EnsureInit(); return rivals[Mathf.Clamp(i, 0, rivals.Count - 1)]; }
    public static IReadOnlyList<Rival> All { get { EnsureInit(); return rivals; } }
    public static string NameOf(int i) { EnsureInit(); return (i >= 0 && i < rivals.Count) ? rivals[i].name : "?"; }
    public static string ColorOf(int i) { EnsureInit(); return (i >= 0 && i < rivals.Count) ? rivals[i].colorHex : "#9c95b4"; }
    public static int AliveCount { get { EnsureInit(); int n = 0; foreach (var r in rivals) if (!r.defeated) n++; return n; } }

    /// <summary>その魔王の領域数（本拠地含む）。</summary>
    public static int TerritoryOf(int i) => SurfaceMap.CountOwnedBy(SurfaceMap.OwnerRivalBase + i);

    /// <summary>本拠地を落としたときの処理＝真核を奪う。領域は中立へ戻り、戦利品が入る。</summary>
    public static void OnHomeConquered(int rivalIndex)
    {
        EnsureInit();
        if (rivalIndex < 0 || rivalIndex >= rivals.Count) return;
        var rv = rivals[rivalIndex];
        if (rv.defeated) return;
        rv.defeated = true;

        // 保有していた領域は中立へ（真核を失った魔王の配下は霧散する）
        int freed = 0;
        foreach (var r in SurfaceMap.All)
            if (r.owner == SurfaceMap.OwnerRivalBase + rivalIndex) { SurfaceMap.SetOwner(r.id, SurfaceMap.OwnerNeutral); freed++; }

        // 真核の戦利品
        int dp = Mathf.RoundToInt(rv.power * 3f);
        int mat = 30 + rivalIndex * 15;
        int rp = 8 + rivalIndex * 4;
        var res = DungeonResourceManager.Instance;
        if (res != null) { res.AddDP(dp); res.AddMaterial(mat); }
        ResearchState.AddRP(rp);
        RelicManager.ReportRivalDefeated();
        rv.lastAction = "真核を奪われ消滅";
        Debug.Log($"🔥『真核を奪取』{rv.title}{rv.name} を排除した（+{dp}DP +{mat}素材 +{rp}RP・保有{freed}領域が中立化）");
    }

    // ============ ターン処理 ============
    // ⚖️ 序盤は動かない（原作の『擬似的平和』＝立ち上がりの猶予）。またある程度広げたら守りに入る
    //    ＝盤面を食い尽くさせない（食い尽くされると拡張の意味が消える）。
    public const int PeaceTurns = 4;      // このターンまでは他魔王は動かない
    public const int ConsolidateAt = 5;   // これだけ領域を持ったら侵攻をやめて固める

    /// <summary>毎ターン：成長 → 侵攻（中立を取る／こちらを攻める）。</summary>
    public static void ResolveTurn(int turn)
    {
        EnsureInit();
        if (turn <= PeaceTurns) return;
        foreach (var rv in rivals)
        {
            if (rv.defeated) { rv.lastAction = "排除済み"; continue; }
            if (TerritoryOf(rivals.IndexOf(rv)) >= ConsolidateAt) { rv.power += rv.growth; rv.lastAction = "領地を固めている"; continue; }
            rv.power += rv.growth;
            rv.lastAction = "";

            int myOwner = SurfaceMap.OwnerRivalBase + rivals.IndexOf(rv);
            for (int a = 0; a < rv.aggression; a++)
            {
                // 自分の領域に隣接する『自分以外の』領域を候補にする
                var cands = new List<SurfaceMap.Region>();
                foreach (var r in SurfaceMap.All)
                {
                    if (r.owner != myOwner || r.type == SurfaceMap.RegionType.Gate) continue;
                    foreach (var l in r.links)
                    {
                        var n = SurfaceMap.Get(l);
                        if (n.owner == myOwner || n.type == SurfaceMap.RegionType.Gate) continue;
                        if (!cands.Contains(n)) cands.Add(n);
                    }
                }
                if (cands.Count == 0) break;

                // 一番手薄なところを狙う（プレイヤー領は少し優先＝存在を脅かしてくる）
                SurfaceMap.Region target = null; float best = float.MaxValue;
                foreach (var c in cands)
                {
                    float d = SurfaceMap.DefenseOf(c.id) * (c.owned ? 0.85f : 1f);
                    if (d < best) { best = d; target = c; }
                }
                if (target == null) break;

                float atk = rv.power * Random.Range(0.85f, 1.15f);
                float def = SurfaceMap.DefenseOf(target.id);
                target.lastResultTurn = turn;
                if (atk > def)
                {
                    bool wasMine = target.owned;
                    SurfaceMap.SetOwner(target.id, myOwner);
                    target.lastResult = rv.name + "に奪われた";
                    rv.lastAction = target.name + " を制圧";
                    if (wasMine)
                    {
                        KinRoster.OnRegionLost(target.id, rv.name);
                        Debug.Log($"🔥『領域を奪われた』{rv.title}{rv.name} が {target.name} を制圧（敵{atk:0} vs 守り{def:0}）");
                    }
                    else Debug.Log($"🔥『他魔王の伸長』{rv.name} が {target.name} を制圧（{TerritoryOf(rivals.IndexOf(rv))}領域）");
                    rv.power *= 0.75f; // 侵攻で大きく消耗（連続で攻め続けられない）
                }
                else
                {
                    target.lastResult = rv.name + "の侵攻を撃退";
                    rv.lastAction = target.name + " の攻略に失敗";
                    rv.power *= 0.85f;
                    if (target.owned) Debug.Log($"🛡️『防衛成功』{target.name} が {rv.name} の侵攻を退けた（敵{atk:0} vs 守り{def:0}）");
                    break;
                }
            }
        }
    }

    /// <summary>人間側の奪還軍：世界水準が高いほど強い軍が自領を取り返しに来る。</summary>
    public static void ResolveHumanReclaim(int turn)
    {
        float tier = AdventurerAI.WorldTierNow();
        int fame = DungeonResourceManager.Instance != null ? DungeonResourceManager.Instance.DungeonFame : 0;
        // 奪還軍の強さ：世界水準＋知名度。序盤は来ない。
        float army = 90f * tier + Mathf.Log(1f + fame / 50f) * 60f;
        if (army < 100f) return;

        // 中立(人間側)に隣接している自領のうち、一番手薄なところが狙われる
        SurfaceMap.Region target = null; float best = float.MaxValue;
        foreach (var r in SurfaceMap.All)
        {
            if (!r.owned || r.type == SurfaceMap.RegionType.Gate) continue;
            bool border = false;
            foreach (var l in r.links) if (SurfaceMap.Get(l).owner == SurfaceMap.OwnerNeutral) { border = true; break; }
            if (!border) continue;
            float d = SurfaceMap.DefenseOf(r.id);
            if (d < best) { best = d; target = r; }
        }
        if (target == null) return;

        float atk = army * Random.Range(0.85f, 1.15f);
        target.lastResultTurn = turn;
        if (atk > best)
        {
            SurfaceMap.SetOwner(target.id, SurfaceMap.OwnerNeutral);
            target.lastResult = "奪還された";
            KinRoster.OnRegionLost(target.id, "人間の奪還軍");
            Debug.Log($"⚔️『領域を奪還された』{target.name} が人間側に奪い返された（奪還軍{atk:0} vs 守り{best:0}）");
        }
        else
        {
            target.lastResult = "奪還軍を撃退";
            Debug.Log($"🛡️『防衛成功』{target.name} が人間の奪還軍を退けた（奪還軍{atk:0} vs 守り{best:0}）");
        }
    }

    /// <summary>全部の解決が終わったあとに産出を回収する（奪われた領域は当然ぶんが入らない）。</summary>
    public static void CollectAfterAll() { SurfaceMap.CollectYields(); }

    public static string StateText(int i)
    {
        var rv = Get(i);
        if (rv.defeated) return "◆排除済み（真核を奪取）";
        return "軍事力 " + rv.power.ToString("0") + "　領域 " + TerritoryOf(i)
             + (string.IsNullOrEmpty(rv.lastAction) ? "" : "　前ターン: " + rv.lastAction);
    }
}
