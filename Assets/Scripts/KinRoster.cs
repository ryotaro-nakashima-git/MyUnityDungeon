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
        public int regionId = 0;                       // 現在地（0＝迷宮前の荒れ地）
        public int marchTarget = -1;                   // 進軍先（-1＝待機）
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
        return Mathf.RoundToInt(MinionCatalog.Get(v.catalogIndex).tierCP * 45f * mult);
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

        var k = new Kin { individualId = individualId, trueName = NameCandidate(individualId, roll) };
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
            k.regionId = 0;   // 迷宮前まで押し戻される
            Debug.Log($"🏳️『敗走』{k.trueName} は {SurfaceMap.Get(regionId).name} を {byWhom} に奪われ後退（配下{lost}体ロスト・2ターン負傷）");
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
        return p * KinPromotion.PowerMult(k);          // 🎖️ 昇進『軍旗』
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
        k.marchTarget = regionId;
        int steps = StepsTo(k, regionId);
        Debug.Log($"🗺️『進軍指示』『{k.trueName}』→ {r.name}（戦力{ArmyPower(k):0} vs 防衛{SurfaceMap.DefenseOf(regionId)}"
            + (steps > 1 ? $"・移動力{MovementOf(k)}で {Mathf.CeilToInt((steps - 1) / (float)MovementOf(k))} ターンかけて接近" : "") + "）");
        return true;
    }

    // ============ 🐾 移動力（Civのユニットと同じく、遠くへは何ターンかけて向かう） ============
    /// <summary>1ターンに進めるタイル数。盤が数千タイルになったので「隣にしか行けない」では動けない。</summary>
    public static int MovementOf(Kin k)
    {
        int m = 2;
        if (ResearchState.IsResearched("s_logistics")) m += 1;   // 兵站
        if (ResearchState.IsResearched("s_scout")) m += 1;       // 斥候
        if (k != null && k.followers.Count == 0) m += 1;         // 身軽（配下を連れていない）
        m += EraSystem.MoveBonus;                                // 📜 誓約『軍旅の誓い』
        m += KinPromotion.MoveBonus(k);                          // 🎖️ 昇進『疾駆』『電撃戦』
        return m;
    }

    /// <summary>現在地から目的地までの歩数（陸だけを通る。届かなければ大きい値）。</summary>
    public static int StepsTo(Kin k, int target)
    {
        if (k == null) return 99;
        if (k.regionId == target) return 0;
        var dist = new Dictionary<int, int>();
        var q = new Queue<int>();
        dist[k.regionId] = 0; q.Enqueue(k.regionId);
        while (q.Count > 0)
        {
            int cur = q.Dequeue();
            int d = dist[cur];
            if (d > 24) break;                                   // 遠すぎるものは探さない（盤が広いので打ち切る）
            foreach (var n in SurfaceMap.Neighbors(cur))
            {
                if (n.isOcean || dist.ContainsKey(n.id)) continue;
                dist[n.id] = d + 1;
                if (n.id == target) return d + 1;
                // 目的地以外は「通れる」場所だけ辿る（敵領は素通りできない＝Civの支配地域）
                if (n.owner == SurfaceMap.OwnerNeutral || n.owned) q.Enqueue(n.id);
            }
        }
        return 99;
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
                if (n.isOcean || prev.ContainsKey(n.id)) continue;
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
        foreach (var k in all)
        {
            if (k.injuryTurns > 0) { k.injuryTurns--; if (k.injuryTurns == 0) Debug.Log($"👑『{k.trueName}』が復帰しました。"); continue; }
            if (k.marchTarget < 0) continue;
            var r = SurfaceMap.Get(k.marchTarget);
            if (r.owned) { k.marchTarget = -1; continue; }

            // 🐾 まず**移動力のぶんだけ近づく**。隣に着くまでは戦わない（Civのユニットと同じ）。
            //    ※これが無いと「隣のタイルしか攻められない」ので、数千タイルの盤で身動きが取れない。
            int move = MovementOf(k);
            bool arrived = false;
            for (int step = 0; step < move; step++)
            {
                if (SurfaceMap.HexDist(SurfaceMap.Get(k.regionId), r) <= 1) { arrived = true; break; }
                int nxt = NextStep(k, k.marchTarget);
                if (nxt < 0 || nxt == k.marchTarget) break;
                k.regionId = nxt;
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

            float power = ArmyPower(k);
            if (r.IsRival && ResearchState.IsResearched("s_conquer")) power *= 1.2f;  // ⚔️『簒奪の作法』
            if (r.IsRival) power *= EraSystem.ConquerMult;                              // 📜 誓約『簒奪の誓い』
            if (!r.IsRival) power *= KinPromotion.AssaultMult(k);                       // 🎖️ 昇進『強襲』
            power *= DiplomacySystem.KinPowerMult;                                      // 🏛️ 従属『傭兵都市』
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
                Debug.Log($"🗺️『制圧』『{k.trueName}』が {r.name} を完勝で支配（戦力{power:0} vs {def}）");
            }
            else if (ratio >= 1.0f)
            {
                SurfaceMap.SetOwner(r.id, SurfaceMap.OwnerSelf); k.regionId = r.id; k.marchTarget = -1; k.conquests++;
                int lost = LoseFollowers(k, Mathf.Max(1, Mathf.RoundToInt(1 * KinPromotion.LossMult(k))));
                r.lastResult = "辛勝"; AfterConquer(r, wasRival);
                KinPromotion.AddMerit(k, wasRival >= 0 ? 5 : 2, "辛勝");
                Debug.Log($"🗺️『辛勝』『{k.trueName}』が {r.name} を支配（戦力{power:0} vs {r.defense}・配下{lost}体を失った）");
            }
            else if (ratio >= 0.7f)
            {
                int lost = LoseFollowers(k, Mathf.Max(1, Mathf.RoundToInt(k.followers.Count / 2f * KinPromotion.LossMult(k))));
                k.injuryTurns = Mathf.Max(1, Mathf.RoundToInt(2 * KinPromotion.InjuryMult(k))); k.marchTarget = -1;
                KinPromotion.AddMerit(k, 1, "敗走したが戦った");
                r.lastResult = "敗走";
                Debug.Log($"🗺️『敗走』『{k.trueName}』は {r.name} で退けられた（戦力{power:0} vs {r.defense}・配下{lost}体ロスト・2ターン負傷）");
            }
            else
            {
                int lost = LoseFollowers(k, Mathf.Max(1, Mathf.RoundToInt(k.followers.Count * KinPromotion.LossMult(k))));
                k.injuryTurns = Mathf.Max(1, Mathf.RoundToInt(4 * KinPromotion.InjuryMult(k))); k.marchTarget = -1;
                r.lastResult = "壊滅";
                Debug.Log($"🗺️『壊滅』『{k.trueName}』の部隊は {r.name} で壊滅（戦力{power:0} vs {r.defense}・配下{lost}体ロスト・4ターン負傷）");
            }
        }
    }

    // 🔥 制圧直後の処理：他魔王の本拠地だったなら真核を奪って排除する
    private static void AfterConquer(SurfaceMap.Region r, int wasRivalIndex)
    {
        if (r.rivalHome >= 0) RivalLords.OnHomeConquered(r.rivalHome);
        else if (wasRivalIndex >= 0) Debug.Log($"🔥 {RivalLords.NameOf(wasRivalIndex)} から {r.name} を奪った");
    }

    /// <summary>配下を失う（個体はロスターから完全に消える＝育てたものを賭ける重み）。</summary>
    private static int LoseFollowers(Kin k, int n)
    {
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
            int eta = steps <= 1 ? 0 : Mathf.CeilToInt((steps - 1) / (float)MovementOf(k));
            return "進軍中 → " + SurfaceMap.Get(k.marchTarget).name
                + (eta > 0 ? "（あと" + eta + "ターンで到着・移動力" + MovementOf(k) + "）" : "（今ターン交戦）");
        }
        var r = SurfaceMap.Get(k.regionId);
        return "駐留 " + r.name + "（守備+" + (ArmyPower(k) * GarrisonBonus).ToString("0") + "・移動力" + MovementOf(k) + "）";
    }
}
