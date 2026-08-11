using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 👑 眷属（けんぞく）＝**真名を与えて格上げした配下**。原作の『支配領域を増やすポイントは眷属化』を実装する層。
///
/// 配下はダンジョンから出られないが、眷属は配下を率いて地上へ出られる。
/// - 眷属化: Lv10以上＋進化Ⅰ以上の個体に DP を払って真名を与える。真名は候補から選ぶ（引き直し可）。
/// - **LP(統率力)**: 眷属が率いられる配下の総量。配下1体のコストは tierCP（強い配下ほど重い）。
/// - **トレードオフ**: 眷属とその配下は隊にもボスにも置けない＝防衛戦力を削って地上に投資する判断になる。
/// - 侵攻はターン終了時に自動解決。負けると**配下個体はロスト**（ロスターから消える）、眷属は数ターン負傷で動けない。
///
/// 純static・実行時保持（ドメインリロードで初期化）。関連: [[SurfaceMap]] [[MinionRoster]] / DungeonTurnManager(解決)。
/// </summary>
public static class KinRoster
{
    public class Kin
    {
        public int individualId;                       // 元になった配下個体
        public string trueName;                        // 与えた真名
        public List<int> followers = new List<int>();  // 率いている配下（個体ID）
        // 現在地。⚠ 既定を 0 にしていたら **id0＝盤の左上の隅（海）** になり、迷宮から53タイルも離れていた。
        //    そのせいで進軍指示は毎ターン「道が無い」で取り消され、ETAも 99歩ぶん（＝約25ターン）と表示されていた。
        //    -1 で作り、生成時に HomeRegion（迷宮のあるタイル）へ置く。→ [[SurfaceMap.IndexOfCenter]]
        public int regionId = -1;
        public int marchTarget = -1;                   // 進軍先（-1＝待機）
        public int mp = -1;                            // 今ターンに残っている移動力（-1＝満タン）
        public int injuryTurns = 0;                    // 負傷で動けない残りターン
        public int conquests = 0;                      // 攻略数
        // 🎖️ 指揮官（C6）。武勲を貯めて昇進を選ぶ。時代をまたいでも残る。→ [[KinPromotion]]
        public int merit = 0;
        public List<int> promotions = new List<int>();
    }

    // 真名の候補（原作の眷属＝人格を持つ存在。引き直しで別候補が出る）
    private static readonly string[] names =
    {
        "クロエ", "カノン", "リナ", "セレネ", "ヴァイス", "ノクス", "ミラ", "グレン",
        "アイリス", "ザイン", "ルーナ", "ディーン", "エルザ", "カイム", "シオン", "ベルナ",
        "ティア", "ラウル", "ネフィス", "オルガ", "ユーリ", "サーシャ", "レイン", "ドロテア",
    };

    /// <summary>個体IDと引き直し回数から真名の候補を決める（決定的＝同じ個体は同じ順で候補が出る）。</summary>
    public static string NameCandidate(int individualId, int roll)
        => names[Mathf.Abs(individualId * 31 + roll * 7 + 5) % names.Length];

    public const int MinLevelToName = 10;   // 眷属化に必要なレベル

    private static List<Kin> all;
    private static void EnsureInit() { if (all == null) all = new List<Kin>(); }

    public static void Reset() { all = new List<Kin>(); }
    public static IReadOnlyList<Kin> All { get { EnsureInit(); return all; } }
    public static int Count { get { EnsureInit(); return all.Count; } }

    /// <summary>
    /// 🌅 開始時に**最初の眷属を1体だけ**与える（Civの初期ユニットに相当）。
    ///
    /// ⚠ これが無いと、眷属化の条件（Lv10＋進化Ⅰ）を満たすまで**10ターンほど地上で何もできない**のに、
    ///    他の魔王だけが版図を広げていく。**こちらが指をくわえて見ている時間**は仕組みとして良くない。
    ///    そこで開始時だけ「既に真名を持つ配下」を1体配る。以降の眷属は従来どおり条件を満たして作る。
    /// </summary>
    public static void GrantStarterKin()
    {
        EnsureInit();
        if (all.Count > 0) return;
        var v = MinionRoster.TrySummonFree(0);      // 費用なしで1体（初期ユニット）
        if (v == null) return;
        MinionRoster.AddExp(v.id, (MinLevelToName - 1) * MinionRoster.ExpPerLevel);   // Lv10相当まで底上げ
        var k = new Kin
        {
            individualId = v.id,
            trueName = NameCandidate(v.id, 0),
            regionId = HomeRegion,
        };
        all.Add(k);
        EurekaTracker.OnKinNamed();
        Debug.Log($"🌅『最初の眷属』{MinionCatalog.Get(v.catalogIndex).jpName} に真名『{k.trueName}』を与えた"
            + $"（Lv{MinionRoster.LevelOf(v.id)}／初手から地上に出られる）");
    }

    /// <summary>🏠 眷属の本拠＝迷宮のあるタイル。盤を作り直すと id が変わるので**その都度引く**。</summary>
    public static int HomeRegion { get { return SurfaceMap.IndexOfCenter(); } }

    /// <summary>盤の作り直しなどで居場所が無効になった眷属を本拠へ戻す（海や範囲外に取り残さない）。</summary>
    public static void FixStrayPositions()
    {
        EnsureInit();
        int home = HomeRegion;
        foreach (var k in all)
        {
            if (k.regionId < 0 || k.regionId >= SurfaceMap.Count || SurfaceMap.Get(k.regionId).isOcean)
            {
                k.regionId = home; k.marchTarget = -1;
            }
        }
    }

    public static Kin Of(int individualId)
    {
        EnsureInit();
        foreach (var k in all) if (k.individualId == individualId) return k;
        return null;
    }
    public static bool IsKin(int individualId) => Of(individualId) != null;

    /// <summary>その個体がいずれかの眷属の配下になっているか。</summary>
    public static Kin LeaderOfFollower(int individualId)
    {
        EnsureInit();
        foreach (var k in all) if (k.followers.Contains(individualId)) return k;
        return null;
    }
    /// <summary>眷属本人 or その配下＝ダンジョン内の編成/配置に使えない。</summary>
    public static bool IsAwayFromDungeon(int individualId)
        => IsKin(individualId) || LeaderOfFollower(individualId) != null;

    // ============ 眷属化 ============
    /// <summary>眷属化の条件を1つずつ返す（UIでチェックリストとして見せるため）。</summary>
    public struct Req { public string label; public bool met; }
    public static List<Req> NameRequirements(int individualId)
    {
        var l = new List<Req>();
        var v = MinionRoster.Get(individualId);
        if (v == null) return l;
        l.Add(new Req { label = "Lv" + MinLevelToName + "以上（現在 Lv" + v.level + "）", met = v.level >= MinLevelToName });
        l.Add(new Req { label = "進化Ⅰ以上の形態（現在 " + MinionCatalog.Get(v.catalogIndex).jpName + "）", met = MinionEvolution.Depth(v.catalogIndex) >= 1 });
        var fm = DungeonFeatureManager.Instance;
        bool inSquad = fm != null && fm.IsIndividualInAnySquad(individualId);
        bool isBoss = fm != null && fm.IsIndividualBoss(individualId);
        l.Add(new Req { label = inSquad ? "隊から外す（編成中）" : isBoss ? "ボス任命を解く（任命中）" : "隊・ボスに就いていない", met = !inSquad && !isBoss });
        int cost = NameCost(individualId);
        var res = DungeonResourceManager.Instance;
        l.Add(new Req { label = "DP " + cost, met = res == null || res.DungeonPoints >= cost });
        return l;
    }
    /// <summary>条件をすべて満たしているか（DPは除く＝ボタンは出すが押すと不足警告）。</summary>
    public static bool MeetsNameRequirements(int individualId)
    {
        foreach (var r in NameRequirements(individualId)) if (!r.met && !r.label.StartsWith("DP ")) return false;
        return !IsKin(individualId);
    }

    public static bool CanName(int individualId, out string reason)
    {
        reason = "";
        var v = MinionRoster.Get(individualId);
        if (v == null) { reason = "個体が存在しません"; return false; }
        if (IsKin(individualId)) { reason = "すでに眷属です"; return false; }
        if (v.level < MinLevelToName) { reason = "Lv" + MinLevelToName + "以上が必要（現在Lv" + v.level + "）"; return false; }
        if (MinionEvolution.Depth(v.catalogIndex) < 1) { reason = "進化Ⅰ以上の形態が必要"; return false; }
        var fm = DungeonFeatureManager.Instance;
        if (fm != null && fm.IsIndividualInAnySquad(individualId)) { reason = "隊に編成中（先に隊から外す）"; return false; }
        if (fm != null && fm.IsIndividualBoss(individualId)) { reason = "ボスに任命中（先に任命を解く）"; return false; }
        return true;
    }

    public static int NameCost(int individualId)
    {
        var v = MinionRoster.Get(individualId);
        if (v == null) return 0;
        float mult = DemonLord.Instance != null ? DemonLord.Instance.DefenderCostMult : 1f;
        return Mathf.RoundToInt(MinionCatalog.Get(v.catalogIndex).tierCP * 45f * mult * NarrativeSystem.KinNameCostMult);   // 🕯️ 形見『折れた真名の刻印』
    }

    /// <summary>真名を与えて眷属にする。DPを消費。</summary>
    public static bool TryName(int individualId, int roll)
    {
        EnsureInit();
        string reason;
        if (!CanName(individualId, out reason)) { Debug.LogWarning("⚠️ 眷属化できません：" + reason); return false; }
        int cost = NameCost(individualId);
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(cost)) { Debug.LogWarning($"⚠️ DP不足で眷属化できません（要{cost}DP）。"); return false; }

        var k = new Kin { individualId = individualId, trueName = NameCandidate(individualId, roll), regionId = HomeRegion };
        all.Add(k);
        EurekaTracker.OnKinNamed();
        var v = MinionRoster.Get(individualId);
        Debug.Log($"👑『眷属化』{MinionCatalog.Get(v.catalogIndex).jpName} 個体#{individualId}(Lv{v.level}) に真名『{k.trueName}』を与えた（-{cost}DP・LP{LPMax(k)}）");
        return true;
    }

    // ============ LP（統率力）と戦力 ============
    /// <summary>眷属が率いられる配下の総量。レベルとランクで伸びる。</summary>
    public static int LPMax(Kin k)
    {
        var v = MinionRoster.Get(k.individualId);
        if (v == null) return 0;
        var d = MinionCatalog.Get(v.catalogIndex);
        int logistics = ResearchState.IsResearched("s_logistics") ? 6 : 0;   // 🚚 地上研究『兵站』
        logistics += PolicySystem.KinLpBonus;                                // 🏛️ 政策『万民の帰依』
        logistics += AttributeSystem.KinLpBonus;                             // 🎖️ 属性『号令』
        return Mathf.RoundToInt(8f + v.level * 0.6f + (int)d.rank * 2f) + logistics + WonderCatalog.KinLPBonus
             + KinPromotion.LpBonus(k);                                     // 🎖️ 昇進『号令』
    }
    /// <summary>配下1体のLPコスト＝そのティア（強い配下ほど重い）。</summary>
    public static int LPCost(int individualId)
    {
        var v = MinionRoster.Get(individualId);
        return v == null ? 0 : Mathf.Max(1, MinionCatalog.Get(v.catalogIndex).tierCP);
    }
    public static int LPUsed(Kin k)
    {
        int n = 0; foreach (var f in k.followers) n += LPCost(f); return n;
    }

    /// <summary>個体1体の地上での戦力。ティア＋レベル＋装備で決まる。</summary>
    public static float UnitPower(int individualId)
    {
        var v = MinionRoster.Get(individualId);
        if (v == null) return 0f;
        var d = MinionCatalog.Get(v.catalogIndex);
        float basePower = 14f + d.tierCP * 9f;
        float equip = 0.5f * (EquipmentCatalog.WeaponAtkMult(v.weaponGrade) + EquipmentCatalog.ArmorHpMult(v.armorGrade));
        return basePower * MinionRoster.LevelMult(v.level) * equip;
    }

    /// <summary>🛡️ その領域に駐留している眷属の守備力の合計（進軍中/負傷中は守りに数えない）。</summary>
    public static float GarrisonPowerAt(int regionId)
    {
        EnsureInit();
        float p = 0f;
        foreach (var k in all)
        {
            if (k.injuryTurns > 0 || k.marchTarget >= 0) continue;
            if (k.regionId == regionId) p += ArmyPower(k) * GarrisonBonus;
        }
        return p;
    }
    /// <summary>守りに就いているときの補正（地の利。攻めるより守るほうが有利）。</summary>
    public const float GarrisonBonus = 1.25f;

    /// <summary>⚔️ 野戦（迎撃）の結果で経験値を入れる。EnemyForce から呼ぶ。</summary>
    public static void ReportFieldBattle(Kin k, float enemyPower, bool won)
    {
        GainExp(k, BattleExp(enemyPower, won), won ? "野戦に勝った" : "野戦で押し返された");
    }

    /// <summary>そのタイルに立っている眷属（いなければ null）。盤のクリックから引くのに使う。</summary>
    public static Kin KinAt(int regionId)
    {
        EnsureInit();
        foreach (var k in all) if (k.regionId == regionId) return k;
        return null;
    }

    /// <summary>その領域に駐留している眷属を列挙する（UI表示用）。</summary>
    public static List<Kin> GarrisonAt(int regionId)
    {
        EnsureInit();
        var l = new List<Kin>();
        foreach (var k in all) if (k.regionId == regionId && k.marchTarget < 0 && k.injuryTurns <= 0) l.Add(k);
        return l;
    }

    /// <summary>🏳️ 領域を奪われたとき：駐留していた眷属は敗走して迷宮前へ戻り、配下を失い負傷する。</summary>
    public static void OnRegionLost(int regionId, string byWhom)
    {
        EnsureInit();
        foreach (var k in all)
        {
            if (k.regionId != regionId) continue;
            int lost = LoseFollowers(k, Mathf.Max(1, k.followers.Count / 2));
            k.injuryTurns = Mathf.Max(k.injuryTurns, 2);
            k.marchTarget = -1;
            k.regionId = HomeRegion;   // 迷宮前まで押し戻される
            Debug.Log($"🏳️『敗走』{k.trueName} は {SurfaceMap.Get(regionId).name} を {byWhom} に奪われ後退（配下{lost}体ロスト・2ターン負傷）");
            NotifySystem.Push($"『{k.trueName}』が {SurfaceMap.Get(regionId).name} を追われた（配下{lost}体ロスト）", NotifySystem.Kind.Loss, regionId);
        }
    }

    /// <summary>🛡️ 駐留先を変える（自領のみ・進軍は取りやめ）。</summary>
    public static bool SetGarrison(int kinIndividualId, int regionId)
    {
        var k = Of(kinIndividualId); if (k == null) return false;
        if (k.injuryTurns > 0) { Debug.LogWarning($"⚠️ 『{k.trueName}』は負傷中です（あと{k.injuryTurns}ターン）。"); return false; }
        var r = SurfaceMap.Get(regionId);
        if (!r.owned) { Debug.LogWarning("⚠️ 駐留できるのは自領だけです。"); return false; }
        k.marchTarget = -1;
        k.regionId = regionId;
        Debug.Log($"🛡️『駐留』{k.trueName} を {r.name} に配置（守備+{ArmyPower(k) * GarrisonBonus:0}）");
        return true;
    }

    /// <summary>部隊の総戦力（眷属本人は真名の力で1.6倍）。</summary>
    public static float ArmyPower(Kin k)
    {
        float p = UnitPower(k.individualId) * 1.6f;
        foreach (var f in k.followers) p += UnitPower(f);
        return p * KinPromotion.PowerMult(k) * NarrativeSystem.KinFieldPowerMult;   // 🎖️昇進『軍旗』／🕯️形見『竜骨の欠片』
    }

    // ============ 編成 ============
    public static bool AddFollower(int kinIndividualId, int followerId)
    {
        var k = Of(kinIndividualId);
        if (k == null) return false;
        if (followerId == kinIndividualId) return false;
        if (IsKin(followerId)) { Debug.LogWarning("⚠️ 眷属を別の眷属の配下にはできません。"); return false; }
        if (LeaderOfFollower(followerId) != null) { Debug.LogWarning("⚠️ その個体は既に別の眷属に率いられています。"); return false; }
        var fm = DungeonFeatureManager.Instance;
        if (fm != null)
        {
            if (fm.IsIndividualInAnySquad(followerId)) { Debug.LogWarning("⚠️ 隊に編成済みの個体は連れて行けません（先に隊から外してください）。"); return false; }
            if (fm.IsIndividualBoss(followerId)) { Debug.LogWarning("⚠️ ボスに任命した個体は連れて行けません。"); return false; }
        }
        int cost = LPCost(followerId);
        if (LPUsed(k) + cost > LPMax(k)) { Debug.LogWarning($"⚠️ LPが足りません（{LPUsed(k)}+{cost} > {LPMax(k)}）。"); return false; }
        k.followers.Add(followerId);
        return true;
    }
    public static void RemoveFollower(int kinIndividualId, int followerId)
    {
        var k = Of(kinIndividualId); if (k == null) return;
        k.followers.Remove(followerId);
    }

    public static bool SetMarchTarget(int kinIndividualId, int regionId)
    {
        var k = Of(kinIndividualId); if (k == null) return false;
        if (k.injuryTurns > 0) { Debug.LogWarning($"⚠️ 『{k.trueName}』は負傷中です（あと{k.injuryTurns}ターン）。"); return false; }
        if (regionId < 0) { k.marchTarget = -1; return true; }
        var r = SurfaceMap.Get(regionId);
        if (r.isOcean) { Debug.LogWarning("⚠️ 海には進軍できません。"); return false; }
        if (r.owned) { Debug.LogWarning("⚠️ そこは既に自領です（守らせるなら『守る』を使ってください）。"); return false; }
        if (!SurfaceMap.IsDiscovered(regionId)) { Debug.LogWarning("⚠️ そこはまだ到達できません（自領に隣接していません）。"); return false; }
        int steps = StepsTo(k, regionId);
        // 🚧 届かない先を受け付けると「指示は通ったのに毎ターン取り消される」ことになる。ここで断る。
        if (steps >= 99)
        {
            Debug.LogWarning($"⚠️ 『{r.name}』への道がありません（海や敵領で塞がれている／遠すぎる）。手前の領域から順に獲ってください。");
            return false;
        }
        k.marchTarget = regionId;
        Debug.Log($"🗺️『進軍指示』『{k.trueName}』→ {r.name}（戦力{ArmyPower(k):0} vs 防衛{SurfaceMap.DefenseOf(regionId)}"
            + (steps > 1 ? $"・移動力{MovementOf(k)}で {Mathf.CeilToInt((steps - 1) / (float)MovementOf(k))} ターンかけて接近" : "") + "）");
        return true;
    }

    // ============ 🐾 移動力（Civのユニットと同じく、遠くへは何ターンかけて向かう） ============
    /// <summary>1ターンに進めるタイル数。盤が数千タイルになったので「隣にしか行けない」では動けない。</summary>
    public static int MovementOf(Kin k)
    {
        int m = 2;
        if (ResearchState.IsResearched("s_road")) m += 1;         // 🛣️『街道』（配線漏れだった）
        if (ResearchState.IsResearched("s_logistics")) m += 1;   // 兵站
        if (ResearchState.IsResearched("s_scout")) m += 1;       // 斥候
        if (k != null && k.followers.Count == 0) m += 1;         // 身軽（配下を連れていない）
        m += EraSystem.MoveBonus;                                // 📜 誓約『軍旅の誓い』
        m += PolicySystem.KinMoveBonus;                          // 🏛️ 政体『群狼同盟』／祝祭
        m += KinPromotion.MoveBonus(k);                          // 🎖️ 昇進『疾駆』『電撃戦』
        m += NarrativeSystem.KinExtraMp;                         // 🕯️ 形見『測量士の羅針』
        return m;
    }

    /// <summary>現在地から目的地までの**総移動コスト**（地形の重み込み。届かなければ99）。</summary>
    public static int StepsTo(Kin k, int target)
    {
        if (k == null) return 99;
        if (k.regionId == target) return 0;
        var path = PathTo(k, target);
        return path == null ? 99 : PathCost(path);
    }
    /// <summary>タイル数（隣接判定などに使う）。</summary>
    public static int TilesTo(Kin k, int target)
    {
        var path = PathTo(k, target);
        return path == null ? 99 : path.Count;
    }

    // ============ 🕹️ 手動移動（U1：Civのように自分でユニットを動かす） ============
    /// <summary>今ターンに残っている移動力。-1＝まだ初期化していない＝満タン。</summary>
    public static int MpOf(Kin k) { return k == null ? 0 : (k.mp < 0 ? MovementOf(k) : k.mp); }

    /// <summary>👁️ そのユニットが見通せる範囲（タイル）。斥候の研究で伸びる。</summary>
    public static int VisionOf(Kin k)
    {
        int v = 2;
        if (ResearchState.IsResearched("s_scout")) v += 1;
        return v;
    }

    /// <summary>眷属の周りを『見た』ことにする（一度見た土地は覚えている＝Civと同じ）。</summary>
    public static void UpdateVision()
    {
        EnsureInit();
        foreach (var k in all)
            if (k.regionId >= 0) SurfaceMap.MarkSeen(k.regionId, VisionOf(k));
    }

    /// <summary>
    /// 現在地から target までの道順（自分のタイルを含まない）。通れなければ null。
    /// 🐾 S4：**地形の踏破コスト**を重みにした最短路（森・荒地は重い／自領は1）。
    ///    敵領は素通りできない（Civの支配地域）。目的地としてだけ選べる。
    /// </summary>
    public static List<int> PathTo(Kin k, int target)
    {
        if (k == null || k.regionId == target) return null;
        var dist = new Dictionary<int, int>();
        var prev = new Dictionary<int, int>();
        var open = new List<int>();
        dist[k.regionId] = 0; prev[k.regionId] = -1; open.Add(k.regionId);
        bool found = false;
        int guard = 0;
        while (open.Count > 0 && !found && guard++ < 4000)
        {
            // 未確定のうち最小コストを取り出す（盤は広いが探索範囲は打ち切るので線形で足りる）
            int bi = 0;
            for (int i = 1; i < open.Count; i++) if (dist[open[i]] < dist[open[bi]]) bi = i;
            int cur = open[bi]; open.RemoveAt(bi);
            if (dist[cur] > 40) break;                       // 遠すぎるものは探さない
            foreach (var n in SurfaceMap.Neighbors(cur))
            {
                if (!SurfaceMap.IsPassable(n)) continue;
                int nd = dist[cur] + SurfaceMap.MoveCost(n);
                if (dist.ContainsKey(n.id) && dist[n.id] <= nd) continue;
                dist[n.id] = nd; prev[n.id] = cur;
                if (n.id == target) { found = true; break; }
                if (n.owner == SurfaceMap.OwnerNeutral || n.owned) open.Add(n.id);
            }
        }
        if (!found) return null;
        var path = new List<int>();
        int step = target;
        while (step != k.regionId) { path.Add(step); step = prev[step]; }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// 🐾 今ターンに行けるタイルの集合（Civの移動プレビュー）。
    /// 地形の重みで幅優先に広げる。**敵領は通れない**ので、そこから先へは伸びない。
    /// </summary>
    public static HashSet<int> ReachableNow(Kin k)
    {
        var set = new HashSet<int>();
        if (k == null || k.injuryTurns > 0) return set;
        int budget = MpOf(k);
        var dist = new Dictionary<int, int>();
        var open = new List<int>();
        dist[k.regionId] = 0; open.Add(k.regionId);
        int guard = 0;
        while (open.Count > 0 && guard++ < 3000)
        {
            int bi = 0;
            for (int i = 1; i < open.Count; i++) if (dist[open[i]] < dist[open[bi]]) bi = i;
            int cur = open[bi]; open.RemoveAt(bi);
            foreach (var n in SurfaceMap.Neighbors(cur))
            {
                if (!SurfaceMap.IsPassable(n)) continue;
                if (!n.owned && n.owner != SurfaceMap.OwnerNeutral) continue;   // 敵領は通れない
                int nd = dist[cur] + SurfaceMap.MoveCost(n);
                if (nd > budget) continue;
                if (dist.ContainsKey(n.id) && dist[n.id] <= nd) continue;
                dist[n.id] = nd; set.Add(n.id); open.Add(n.id);
            }
        }
        return set;
    }

    /// <summary>道順の総移動コスト（地形の重みの合計）。</summary>
    public static int PathCost(List<int> path)
    {
        if (path == null) return 99;
        int c = 0;
        foreach (int id in path) c += SurfaceMap.MoveCost(SurfaceMap.Get(id));
        return c;
    }

    /// <summary>『いまこのターンに』そこまで歩けるか。歩ける場合 cost に消費する移動力を返す。</summary>
    public static bool CanMoveNow(Kin k, int target, out int cost, out string why)
    {
        cost = 0; why = "";
        if (k == null) { why = "眷属がいません"; return false; }
        if (k.injuryTurns > 0) { why = "負傷中（あと" + k.injuryTurns + "ターン）"; return false; }
        var r = SurfaceMap.Get(target);
        if (r.isOcean) { why = "海には入れません"; return false; }
        if (!r.owned && r.owner != SurfaceMap.OwnerNeutral) { why = "敵領には踏み込めません（攻撃してください）"; return false; }
        var path = PathTo(k, target);
        if (path == null) { why = "道がありません"; return false; }
        cost = PathCost(path);
        // 🐾 Civと同じ「移動力が1でも残っていれば隣へは必ず入れる」（重い地形で詰まないため）
        if (path.Count == 1 && MpOf(k) >= 1) { cost = Mathf.Min(cost, MpOf(k)); return true; }
        if (cost > MpOf(k)) { why = "移動力が足りません（要" + cost + "・残り" + MpOf(k) + "／" + SurfaceMap.TerrainName(r.terrain) + "は重い）"; return false; }
        return true;
    }

    /// <summary>その場で歩かせる（移動力を消費）。歩いた先で視界が開ける。</summary>
    public static bool TryMoveTo(int kinIndividualId, int target)
    {
        var k = Of(kinIndividualId); if (k == null) return false;
        int cost; string why;
        if (!CanMoveNow(k, target, out cost, out why)) { Debug.LogWarning("⚠️ 移動できません：" + why); return false; }
        k.mp = MpOf(k) - cost;
        k.regionId = target;
        k.marchTarget = -1;                       // 手で動かしたら自動進軍は取り消す
        SurfaceMap.MarkSeen(target, VisionOf(k));
        DiscoverySystem.OnEnter(target);          // 🔦 未踏の地で何かを見つけることがある
        Debug.Log($"🐾『移動』『{k.trueName}』が {SurfaceMap.Get(target).name} へ（-{cost}・残り移動力{k.mp}）");
        return true;
    }

    /// <summary>隣接した相手に『いま』仕掛けられるか。</summary>
    public static bool CanAttackNow(Kin k, int target, out string why)
    {
        why = "";
        if (k == null) { why = "眷属がいません"; return false; }
        if (k.injuryTurns > 0) { why = "負傷中（あと" + k.injuryTurns + "ターン）"; return false; }
        var r = SurfaceMap.Get(target);
        if (r.isOcean) { why = "海は攻められません"; return false; }
        if (r.owned) { why = "すでに自領です"; return false; }
        if (SurfaceMap.HexDist(SurfaceMap.Get(k.regionId), r) > 1) { why = "隣接していません（まず移動）"; return false; }
        if (MpOf(k) < 1) { why = "今ターンの移動力を使い切っています"; return false; }
        return true;
    }

    /// <summary>手動で攻撃する（隣接・移動力1消費）。解決は自動進軍と同じ計算。</summary>
    public static bool TryAttack(int kinIndividualId, int target, int turn)
    {
        var k = Of(kinIndividualId); if (k == null) return false;
        string why;
        if (!CanAttackNow(k, target, out why)) { Debug.LogWarning("⚠️ 攻撃できません：" + why); return false; }
        k.mp = MpOf(k) - 1;
        k.marchTarget = -1;
        ResolveAttack(k, SurfaceMap.Get(target), turn);
        UpdateVision();
        return true;
    }

    /// <summary>目的地へ1歩近づく次のタイル（届かなければ -1）。</summary>
    private static int NextStep(Kin k, int target)
    {
        var prev = new Dictionary<int, int>();
        var q = new Queue<int>();
        prev[k.regionId] = -1; q.Enqueue(k.regionId);
        int found = -1;
        while (q.Count > 0 && found < 0)
        {
            int cur = q.Dequeue();
            foreach (var n in SurfaceMap.Neighbors(cur))
            {
                if (!SurfaceMap.IsPassable(n) || prev.ContainsKey(n.id)) continue;
                prev[n.id] = cur;
                if (n.id == target) { found = n.id; break; }
                if (n.owner == SurfaceMap.OwnerNeutral || n.owned) q.Enqueue(n.id);
            }
        }
        if (found < 0) return -1;
        int step = found;
        while (prev[step] != k.regionId && prev[step] != -1) step = prev[step];
        return step;
    }

    /// <summary>🎖️ 昇進で海を越えられる眷属がいるか（越えられるマス数の最大）。</summary>
    public static int AnySeaCross()
    {
        EnsureInit();
        int m = 0;
        foreach (var k in all) { int c = KinPromotion.SeaCross(k); if (c > m) m = c; }
        return m;
    }

    /// <summary>
    /// ⏳ 時代が変わったときの指揮官の扱い（Civ VIIで司令官が時代を越えるのと同じ）。
    /// 昇進はそのまま残り、負傷は癒え、これまでの働きに武勲が入る。
    /// </summary>
    public static void OnEraChanged()
    {
        EnsureInit();
        foreach (var k in all)
        {
            k.injuryTurns = 0;
            KinPromotion.AddMerit(k, 3, "時代を越えた");
        }
    }

    // ============ ターン終了時の解決 ============
    /// <summary>侵攻を解決する。勝てば支配、負ければ配下ロスト＋眷属は負傷。</summary>
    public static void ResolveTurn(int turn)
    {
        EnsureInit();
        FixStrayPositions();
        foreach (var k in all)
        {
            if (k.injuryTurns > 0) { k.injuryTurns--; if (k.injuryTurns == 0) Debug.Log($"👑『{k.trueName}』が復帰しました。"); continue; }
            if (k.marchTarget < 0) continue;
            var r = SurfaceMap.Get(k.marchTarget);
            if (r.owned) { k.marchTarget = -1; continue; }

            // 🐾 まず**残っている移動力のぶんだけ近づく**。隣に着くまでは戦わない（Civのユニットと同じ）。
            //    ※これが無いと「隣のタイルしか攻められない」ので、数千タイルの盤で身動きが取れない。
            //    ⚠ 手で動かしたぶんはここから引かれている（同じ移動力の財布を使う）。
            bool arrived = false;
            for (int step = 0; step < 12; step++)
            {
                if (SurfaceMap.HexDist(SurfaceMap.Get(k.regionId), r) <= 1) { arrived = true; break; }
                int nxt = NextStep(k, k.marchTarget);
                if (nxt < 0 || nxt == k.marchTarget) break;
                int cost = SurfaceMap.MoveCost(SurfaceMap.Get(nxt));
                if (cost > MpOf(k)) break;                       // 🐾 重い地形は入れるだけの移動力が要る
                k.regionId = nxt; k.mp = Mathf.Max(0, MpOf(k) - cost);
                DiscoverySystem.OnEnter(nxt);                    // 🔦 歩いた先で何かを見つけることがある
                // 🚧 支配地域(ZoC)：敵の拠点の隣に踏み込んだら、そのターンはそこで止まる
                if (KinPromotion.InEnemyZoC(k.regionId))
                {
                    Debug.Log($"🚧『支配地域』『{k.trueName}』は敵の拠点に睨まれて {SurfaceMap.Get(k.regionId).name} で足を止めた");
                    break;
                }
            }
            if (!arrived && SurfaceMap.HexDist(SurfaceMap.Get(k.regionId), r) > 1)
            {
                if (NextStep(k, k.marchTarget) < 0)
                {
                    Debug.LogWarning($"⚠️『{k.trueName}』は {r.name} への道が無く進軍を取り消した（海や敵領で塞がれている）");
                    k.marchTarget = -1;
                }
                else Debug.Log($"🐾『行軍』『{k.trueName}』が {SurfaceMap.Get(k.regionId).name} まで進んだ（{r.name} へ）");
                continue;
            }

            ResolveAttack(k, r, turn);
        }

        // 📈 地上に出ているだけでも少しずつ伸びる（進軍中は多め・駐留は少なめ）。
        //    ⚠ 微量に留める。ここを厚くすると「送り出して放置」が最適手になり、迷宮を疎かにできてしまう。
        foreach (var k in all)
        {
            if (k.injuryTurns > 0) continue;
            GainExp(k, k.marchTarget >= 0 ? 12 : 6, "地上での活動");
        }

        // 🔁 次のターンぶんの移動力を配り直し、見えている範囲を更新する
        foreach (var k in all) k.mp = MovementOf(k);
        UpdateVision();
    }

    /// <summary>
    /// ⚔️ 1回の戦闘を解決する。自動進軍（ターン終了時）と手動攻撃の**両方から呼ぶ**ので、
    /// 判定を1箇所にまとめてある（分けると片方だけ仕様が古くなる）。
    /// </summary>
    private static void ResolveAttack(Kin k, SurfaceMap.Region r, int turn)
    {
        {
            float power = ArmyPower(k);
            if (r.IsRival && ResearchState.IsResearched("s_conquer")) power *= 1.2f;  // ⚔️『簒奪の作法』
            if (r.IsRival) power *= EraSystem.ConquerMult;                              // 📜 誓約『簒奪の誓い』
            if (!r.IsRival) power *= KinPromotion.AssaultMult(k);                       // 🎖️ 昇進『強襲』
            power *= DiplomacySystem.KinPowerMult;                                      // 🏛️ 従属『傭兵都市』
            power *= PolicySystem.KinPowerMult;                                        // 🏛️ 政体『群狼同盟』の祝祭
            power *= AttributeSystem.KinPowerMult;                                     // 🎖️ 属性『進撃』
            float flank = KinPromotion.FlankBonus(k, r.id);                             // 🗡️ 側面（隣の味方眷属）
            power *= flank;
            int def = SurfaceMap.DefenseOf(r.id);          // 🔥 他魔王領/砦化された領域はここが上がる
            int siege = KinPromotion.SiegeReduction(k, r);                              // 🎖️ 攻城（砦・硬さを無視）
            if (siege > 0) def = Mathf.Max(1, def - siege);
            float ratio = def > 0 ? power / def : 99f;
            int wasRival = r.IsRival ? r.RivalIndex : -1;
            r.lastResultTurn = turn;

            if (ratio >= 1.25f)
            {
                SurfaceMap.SetOwner(r.id, SurfaceMap.OwnerSelf); k.regionId = r.id; k.marchTarget = -1; k.conquests++;
                r.lastResult = "完勝"; AfterConquer(r, wasRival);
                KinPromotion.AddMerit(k, wasRival >= 0 ? 6 : 3, "完勝");
                GainExp(k, BattleExp(def, true), "完勝");
                Debug.Log($"🗺️『制圧』『{k.trueName}』が {r.name} を完勝で支配（戦力{power:0} vs {def}）");
                NotifySystem.Push($"『{k.trueName}』が {r.name} を<b>完勝</b>で制圧", NotifySystem.Kind.Gain, r.id);
            }
            else if (ratio >= 1.0f)
            {
                SurfaceMap.SetOwner(r.id, SurfaceMap.OwnerSelf); k.regionId = r.id; k.marchTarget = -1; k.conquests++;
                int lost = LoseFollowers(k, Mathf.Max(1, Mathf.RoundToInt(1 * KinPromotion.LossMult(k))));
                r.lastResult = "辛勝"; AfterConquer(r, wasRival);
                KinPromotion.AddMerit(k, wasRival >= 0 ? 5 : 2, "辛勝");
                GainExp(k, Mathf.RoundToInt(BattleExp(def, true) * 1.2f), "辛勝（きわどい戦いほど糧になる）");
                Debug.Log($"🗺️『辛勝』『{k.trueName}』が {r.name} を支配（戦力{power:0} vs {r.defense}・配下{lost}体を失った）");
                NotifySystem.Push($"『{k.trueName}』が {r.name} を<b>辛勝</b>で制圧（配下{lost}体を失った）", NotifySystem.Kind.Gain, r.id);
            }
            else if (ratio >= 0.7f)
            {
                int lost = LoseFollowers(k, Mathf.Max(1, Mathf.RoundToInt(k.followers.Count / 2f * KinPromotion.LossMult(k))));
                k.injuryTurns = Mathf.Max(1, Mathf.RoundToInt(2 * KinPromotion.InjuryMult(k))); k.marchTarget = -1;
                KinPromotion.AddMerit(k, 1, "敗走したが戦った");
                GainExp(k, BattleExp(def, false), "敗走");
                r.lastResult = "敗走";
                Debug.Log($"🗺️『敗走』『{k.trueName}』は {r.name} で退けられた（戦力{power:0} vs {r.defense}・配下{lost}体ロスト・2ターン負傷）");
                NotifySystem.Push($"『{k.trueName}』が {r.name} で<b>敗走</b>（配下{lost}体ロスト・2ターン負傷）", NotifySystem.Kind.Loss, r.id);
            }
            else
            {
                int lost = LoseFollowers(k, Mathf.Max(1, Mathf.RoundToInt(k.followers.Count * KinPromotion.LossMult(k))));
                k.injuryTurns = Mathf.Max(1, Mathf.RoundToInt(4 * KinPromotion.InjuryMult(k))); k.marchTarget = -1;
                r.lastResult = "壊滅";
                Debug.Log($"🗺️『壊滅』『{k.trueName}』の部隊は {r.name} で壊滅（戦力{power:0} vs {r.defense}・配下{lost}体ロスト・4ターン負傷）");
                NotifySystem.Push($"『{k.trueName}』の部隊が {r.name} で<b>壊滅</b>（配下{lost}体ロスト・4ターン負傷）", NotifySystem.Kind.Loss, r.id);
            }
        }
    }

    /// <summary>
    /// 🔥 制圧直後の処理を外からも呼べる口（U-4：軍団も土地を取るようになった）。
    /// ⚠ 眷属と軍団で**別々に書かない**。片方だけ真核や独立勢力の粉砕が漏れる。
    /// </summary>
    public static void OnRegionConquered(SurfaceMap.Region r, int wasRivalIndex) => AfterConquer(r, wasRivalIndex);

    // 🔥 制圧直後の処理：他魔王の本拠地だったなら真核を奪って排除する
    private static void AfterConquer(SurfaceMap.Region r, int wasRivalIndex)
    {
        DiplomacySystem.OnRegionConquered(r.id);   // 💥 独立勢力の土地なら粉砕（軍事の属性＋素材）
        if (r.rivalHome >= 0) RivalLords.OnHomeConquered(r.rivalHome);
        else if (wasRivalIndex >= 0) Debug.Log($"🔥 {RivalLords.NameOf(wasRivalIndex)} から {r.name} を奪った");
    }

    /// <summary>配下を失う（個体はロスターから完全に消える＝育てたものを賭ける重み）。</summary>
    ///  ⚠ 損耗の軽減（🏛️政策『略奪の作法』／🎖️属性『練度』）は**ここで一括**して掛ける。
    ///     呼び出し側（勝敗の3分岐）に散らすと、片方だけ直して食い違う。
    private static int LoseFollowers(Kin k, int n)
    {
        n = Mathf.Max(0, Mathf.RoundToInt(n * PolicySystem.KinLossMult * AttributeSystem.KinLossMult));
        if (n <= 0) return 0;
        int lost = 0;
        for (int i = 0; i < n && k.followers.Count > 0; i++)
        {
            int id = k.followers[k.followers.Count - 1];
            k.followers.RemoveAt(k.followers.Count - 1);
            MinionRoster.Remove(id);
            lost++;
        }
        return lost;
    }

    // ============ 📈 地上での成長（送り出した後も伸びる） ============
    //  ⚠ これが無いと「眷属にした時点でレベルが固定」になり、**格上の敵に一生勝てない**。
    //     迷宮の配下は戦って伸びるのに、地上へ出した瞬間に伸びが止まるのは片手落ちだった。
    //     経験値は**眷属本人と、連れている配下**の両方に入る（部隊ごと育つ）。
    private static void GainExp(Kin k, int amount, string why)
    {
        if (k == null || amount <= 0) return;
        int before = MinionRoster.LevelOf(k.individualId);
        MinionRoster.AddExp(k.individualId, amount);
        foreach (var f in k.followers) MinionRoster.AddExp(f, Mathf.Max(1, amount / 2));   // 配下は半分
        int after = MinionRoster.LevelOf(k.individualId);
        if (after > before)
        {
            Debug.Log($"📈『{k.trueName} が育った』Lv{before} → Lv{after}（{why}）");
            NotifySystem.Push($"『{k.trueName}』が <b>Lv{after}</b> になった（{why}）", NotifySystem.Kind.Gain, k.regionId);
        }
    }

    /// <summary>戦って得る経験値。相手が強いほど多い（格上に挑む意味を作る）。</summary>
    private static int BattleExp(float enemyPower, bool won)
    {
        int e = Mathf.RoundToInt(20f + enemyPower * 0.08f);
        return won ? e : Mathf.RoundToInt(e * 0.4f);   // 負けても少しは糧になる
    }

    /// <summary>🏕️ 地上での鍛錬。自領にいるあいだ、素材とDPを注いで鍛える（訓練所の地上版）。</summary>
    public static int DrillCost(Kin k)
    {
        int lv = MinionRoster.LevelOf(k.individualId);
        return 200 + lv * 30;
    }
    public static int DrillMaterial(Kin k) { return 4 + MinionRoster.LevelOf(k.individualId) / 5; }
    public const int DrillExp = 120;

    public static bool CanDrill(Kin k, out string why)
    {
        why = "";
        if (k == null) { why = "眷属がいません"; return false; }
        if (k.injuryTurns > 0) { why = "負傷中（あと" + k.injuryTurns + "ターン）"; return false; }
        if (MinionRoster.LevelOf(k.individualId) >= MinionRoster.MaxLevel) { why = "既に最高レベル"; return false; }
        var r = SurfaceMap.Get(k.regionId);
        if (!r.owned) { why = "自領でしか鍛えられない（腰を据える場所が要る）"; return false; }
        if (k.mp < MovementOf(k) && k.mp >= 0) { why = "今ターンはもう動いている（移動力を残しておく）"; return false; }
        return true;
    }

    public static bool TryDrill(int kinIndividualId)
    {
        var k = Of(kinIndividualId); if (k == null) return false;
        string why;
        if (!CanDrill(k, out why)) { Debug.LogWarning("⚠️ 鍛錬できません：" + why); return false; }
        int dp = DrillCost(k), mat = DrillMaterial(k);
        var res = DungeonResourceManager.Instance;
        if (res != null && res.CraftMaterials < mat) { Debug.LogWarning($"⚠️ 素材不足（要{mat}）。"); return false; }
        if (res != null && !res.TrySpendDP(dp)) { Debug.LogWarning($"⚠️ DP不足（要{dp}）。"); return false; }
        if (res != null) res.TrySpendMaterial(mat);
        k.mp = 0;                                   // 鍛錬に1ターンを使う（動けない）
        GainExp(k, DrillExp, "鍛錬");
        Debug.Log($"🏕️『鍛錬』{k.trueName} を鍛えた（-{dp}DP -{mat}素材・+{DrillExp}exp）");
        return true;
    }

    /// <summary>眷属を解任（真名を返上）＝ダンジョン防衛に戻す。配下も解散。</summary>
    public static void Dissolve(int individualId)
    {
        var k = Of(individualId); if (k == null) return;
        k.followers.Clear();
        all.Remove(k);
        Debug.Log($"👑『{k.trueName}』の真名を返上（ダンジョン防衛に戻れます）");
    }

    public static string StateText(Kin k)
    {
        if (k.injuryTurns > 0) return "負傷（あと" + k.injuryTurns + "ターン）";
        if (k.marchTarget >= 0)
        {
            int steps = StepsTo(k, k.marchTarget);
            if (steps >= 99) return "進軍中 → " + SurfaceMap.Get(k.marchTarget).name + "（道が塞がれています）";
            int eta = steps <= 1 ? 0 : Mathf.CeilToInt((steps - 1) / (float)MovementOf(k));
            return "進軍中 → " + SurfaceMap.Get(k.marchTarget).name
                + (eta > 0 ? "（あと" + eta + "ターンで到着・移動力" + MovementOf(k) + "）" : "（今ターン交戦）");
        }
        var r = SurfaceMap.Get(k.regionId);
        return "駐留 " + r.name + "（守備+" + (ArmyPower(k) * GarrisonBonus).ToString("0") + "・移動力" + MovementOf(k) + "）";
    }
}
