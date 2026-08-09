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
        // ⚠ 本拠地は **SurfaceMap 側の手続き生成が決める**（PlaceRivalHomes）。
        //    ここで固定IDを割り当てると、生成された盤の海タイルに本拠地が乗ってしまう（実際に踏んだ）。
        //    homeRegion は都度 SurfaceMap.HomeRegionOfRival(i) から引く。
    }

    public static int Count { get { EnsureInit(); return rivals.Count; } }
    public static Rival Get(int i) { EnsureInit(); return rivals[Mathf.Clamp(i, 0, rivals.Count - 1)]; }
    public static IReadOnlyList<Rival> All { get { EnsureInit(); return rivals; } }
    public static string NameOf(int i) { EnsureInit(); return (i >= 0 && i < rivals.Count) ? rivals[i].name : "?"; }
    public static string ColorOf(int i) { EnsureInit(); return (i >= 0 && i < rivals.Count) ? rivals[i].colorHex : "#9c95b4"; }
    public static int AliveCount { get { EnsureInit(); int n = 0; foreach (var r in rivals) if (!r.defeated) n++; return n; } }

    /// <summary>本拠地の領域id（盤の生成側が決める）。</summary>
    public static int HomeOf(int i) => SurfaceMap.HomeRegionOfRival(i);

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
        NotifySystem.Push($"<b>{rv.title}{rv.name} を排除</b>した（+{dp}DP +{mat}素材 +{rp}RP）", NotifySystem.Kind.Story);
    }

    // ============ ターン処理 ============
    // ⚖️ 序盤は動かない（原作の『擬似的平和』＝立ち上がりの猶予）。またある程度広げたら守りに入る
    //    ＝盤面を食い尽くさせない（食い尽くされると拡張の意味が消える）。
    public const int PeaceTurns = 4;      // このターンまでは他魔王は動かない
    public const int ConsolidateAt = 5;   // これだけ領域を持ったら侵攻をやめて固める

    /// <summary>
    /// 毎ターン：成長 → **軍を出す**。
    /// ⚔️ U2以前は「一番手薄な自領を遠隔から一撃で奪う」だったので、**防ぎようも読みようも無かった**。
    /// いまは本拠地から軍が出て盤の上を歩いてくる（進軍と攻城は [[EnemyForce]] が担う）。
    /// </summary>
    public static void ResolveTurn(int turn)
    {
        EnsureInit();
        if (turn <= PeaceTurns) return;
        foreach (var rv in rivals)
        {
            int idx = rivals.IndexOf(rv);
            if (rv.defeated) { rv.lastAction = "排除済み"; continue; }
            // 🕊️ 不可侵の盟約を結んでいるあいだは動かない（C5）
            if (DiplomacySystem.PeaceLeft(idx) > 0) { rv.power += rv.growth * 0.5f; rv.lastAction = "不可侵の盟約中"; continue; }
            if (TerritoryOf(idx) >= ConsolidateAt) { rv.power += rv.growth; rv.lastAction = "領地を固めている"; continue; }
            rv.power += rv.growth * EraSystem.RivalPowerMult * AttributeSystem.RivalPowerMult
                        * Difficulty.RivalGrowMult * NarrativeSystem.RivalGrowMult;   // ☄️ 災厄／🎖️ 属性／⚖️ 難易度／🕯️ 形見『暴君の玉座』

            // 力が溜まったら軍を切り出す（出したぶん本体は減るので、際限なく湧かない）
            if (EnemyForce.CountOf(idx) < EnemyForce.MaxPerRival && rv.power >= 200f)
            {
                EnemyForce.SpawnFromRival(idx);
                rv.lastAction = "軍を進発させた";
            }
            else rv.lastAction = EnemyForce.CountOf(idx) > 0 ? "軍が進んでいる" : "力を蓄えている";
        }
    }

    /// <summary>
    /// 人間側の奪還軍：世界水準が高いほど強い軍が来る。
    /// ⚔️ U2：**自領に接した中立の土地に湧いて歩いてくる**（どこから来るかが見える）。
    /// </summary>
    public static void ResolveHumanReclaim(int turn)
    {
        EnemyForce.TickHumanCooldown();
        // ⏳ 撃退した直後に次が湧くと息継ぎができない（実測：毎ターン奪われ続ける）
        if (EnemyForce.HumanCooldown > 0) return;
        // ⏳ 前の軍がまだ集まっている最中なら、次は出さない（2つ同時に押し寄せさせない）
        if (EnemyForce.AnyHumanMustering()) return;

        float tier = AdventurerAI.WorldTierNow();
        int fame = DungeonResourceManager.Instance != null ? DungeonResourceManager.Instance.DungeonFame : 0;
        // 奪還軍の強さ：世界水準＋知名度。序盤は来ない。
        // ⚠ 閾値が100だと **T2〜3で条件が成立していた**。世界水準には『領地数』のバイアス
        //   (min(1.2, 0.5×ln(1+領地数))) が入るので、**版図を広げた瞬間に湧く**のが早すぎた。
        //   人間が体勢を立て直すには時間が要る、という理屈で 160 に上げる。
        float army = 90f * tier + Mathf.Log(1f + fame / 50f) * 60f;
        if (army < 160f) return;
        EnemyForce.SpawnHuman(army);
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
