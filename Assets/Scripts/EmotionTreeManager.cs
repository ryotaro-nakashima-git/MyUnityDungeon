using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 感情ツリー（歓喜/興奮/絶望/殺戮）＋Eurekaブースト＋複合ノード＋研究連携。
///
/// 設計:
/// - 冒険者の体験で感情が貯まり、4ルート×4段のノードを解禁して効果を得る（文化系ツリー）。
/// - **複合ノード**: 2つのルートを一定段まで進めると解禁できる上位ノード（歓喜×絶望＝甘い罠 など）。
/// - **研究連携**: 感情ノードの一部が毎ターンの研究点(RP)を生み、逆に研究側は感情獲得量を増やす。
///   ＝ Civの『文化ツリー×技術ツリー』の相互作用を再現。
/// - Eureka: お題を達成しているノードはコスト-40%。
/// 関連: [[Research]] [[internal-affairs-design]] / DungeonTurnManager(RP) / AdventurerAI(感情獲得)。
/// </summary>
public class EmotionTreeManager : MonoBehaviour, SaveSystem.ISaveHook
{
    public static EmotionTreeManager Instance { get; private set; }

    public enum Route { Joy, Thrill, Despair, Slaughter } // 歓喜/興奮/絶望/殺戮
    public static readonly string[] RouteNames = { "歓喜", "興奮", "絶望", "殺戮" };
    public static readonly string[] RouteColors = { "#e3a94a", "#e08a3c", "#b48be6", "#df5a5a" };

    public class Node
    {
        public Route route; public int tier; public string name; public string desc; public int baseCost;
        public bool unlocked; public System.Func<bool> eureka; public string eurekaHint;
        // 複合ノード用（2ルートの前提）
        public bool isFusion; public Route reqRouteA, reqRouteB; public int reqTierA, reqTierB;
    }

    // ⚠ `readonly` を外してあるのは意図的。[[SaveSystem]] は **readonly を「カタログ＝保存しない」の目印**に使う。
    private int[] pool = new int[4];
    // 🪝 ノードは『解放フラグ』と『天啓の判定 Func』が同居していて、そのままでは保存できない。
    //    保存からは外し（＝生きている中身を残す）、解放フラグだけ unlockedSave に写して持ち運ぶ。
    [System.NonSerialized] private List<Node> nodes;
    [System.NonSerialized] private List<Node> fusions;
    private List<int> unlockedSave;      // 💾 「ルート*100+段」の一覧／複合は 10000+index
    // Eureka用カウンタ
    private int chestsOpened, trapsTriggered, kills, bossHits, escapes;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildNodes();
    }

    private Node N(Route r, int tier, string name, string desc, int cost, System.Func<bool> eu, string hint)
    { return new Node { route = r, tier = tier, name = name, desc = desc, baseCost = cost, eureka = eu, eurekaHint = hint }; }

    private Node F(string name, string desc, int cost, Route a, int ta, Route b, int tb)
    { return new Node { route = a, tier = 99, name = name, desc = desc, baseCost = cost, isFusion = true, reqRouteA = a, reqTierA = ta, reqRouteB = b, reqTierB = tb }; }

    private void BuildNodes()
    {
        nodes = new List<Node>
        {
            // 🎁 歓喜＝集客（お客を呼ぶ）
            N(Route.Joy, 0, "歓待",   "冒険者の来訪が+1人。",                         20, ()=>chestsOpened>=10, "宝箱10回"),
            N(Route.Joy, 1, "宝物庫", "来訪がさらに+2人。",                           50, ()=>IsUnlocked(Route.Joy,0), "歓待解禁"),
            N(Route.Joy, 2, "祝祭",   "来訪+2人／宝箱の魅力が上がる。",               110, ()=>chestsOpened>=30, "宝箱30回"),
            N(Route.Joy, 3, "楽園",   "来訪+3人／毎ターン研究点+1（研究連携）。",     200, ()=>IsUnlocked(Route.Joy,2), "祝祭解禁"),

            // ⚔️ 興奮＝防衛体の強化
            N(Route.Thrill, 0, "闘技",     "配下の攻防+20%。",                        20, ()=>bossHits>=20, "魔王攻撃20回"),
            N(Route.Thrill, 1, "死闘",     "配下の攻防+20%。",                        50, ()=>IsUnlocked(Route.Thrill,0), "闘技解禁"),
            N(Route.Thrill, 2, "闘争本能", "配下の攻防+25%。",                        110, ()=>bossHits>=60, "魔王攻撃60回"),
            N(Route.Thrill, 3, "修羅場",   "配下の攻防+30%／毎ターン研究点+1。",      200, ()=>IsUnlocked(Route.Thrill,2), "闘争本能解禁"),

            // 💀 絶望＝罠の強化
            N(Route.Despair, 0, "恐怖",     "罠のダメージ×1.5。",                     20, ()=>trapsTriggered>=10, "罠10回"),
            N(Route.Despair, 1, "絶望の淵", "罠のダメージ×1.5。",                     50, ()=>IsUnlocked(Route.Despair,0), "恐怖解禁"),
            N(Route.Despair, 2, "呪縛",     "罠の状態異常が長引く(×1.5)。",           110, ()=>trapsTriggered>=30, "罠30回"),
            N(Route.Despair, 3, "深淵",     "罠のダメージ×1.6／毎ターン研究点+1。",   200, ()=>IsUnlocked(Route.Despair,2), "呪縛解禁"),

            // 🩸 殺戮＝撃破報酬
            N(Route.Slaughter, 0, "処刑", "撃破DP×1.5。",                             20, ()=>kills>=10, "撃破10体"),
            N(Route.Slaughter, 1, "屠殺", "撃破で素材+1。",                           50, ()=>IsUnlocked(Route.Slaughter,0), "処刑解禁"),
            N(Route.Slaughter, 2, "血宴", "撃破DP×1.8／素材+1。",                     110, ()=>kills>=30, "撃破30体"),
            N(Route.Slaughter, 3, "殲滅", "撃破DP×2.2／毎ターン研究点+1。",           200, ()=>IsUnlocked(Route.Slaughter,2), "血宴解禁"),
        };

        // ✨ 複合ノード（2ルートを進めると解禁できる）
        fusions = new List<Node>
        {
            F("甘い罠",   "歓喜×絶望：宝箱の周囲の罠が威力×1.4、来訪+1。",      160, Route.Joy, 1, Route.Despair, 1),
            F("闘技場",   "興奮×殺戮：配下の攻防+15%、撃破DP×1.3。",            160, Route.Thrill, 1, Route.Slaughter, 1),
            F("恐怖支配", "絶望×殺戮：脅威度の上昇が緩やかになり、撃破素材+1。", 240, Route.Despair, 2, Route.Slaughter, 2),
            F("享楽の園", "歓喜×興奮：来訪+2、配下の攻防+15%。",                240, Route.Joy, 2, Route.Thrill, 2),
        };
    }

    // ---- 感情/カウンタの獲得 ----
    public void AddEmotion(Route r, int amt)
    {
        // 🔬 研究連携：研究『感情増幅』を取ると感情の入りが増える ／ 🏺 遺物『収穫の鎌』
        float m = ResearchState.IsResearched("k_emotion") ? 1.35f : 1f;
        if (RelicManager.Instance != null) m *= RelicManager.Instance.EmotionGainMult;
        m *= WonderCatalog.EmotionMult;    // ★ 遺産『嘆きの大樹』
        pool[(int)r] += Mathf.Max(1, Mathf.RoundToInt(amt * m));
    }
    // 💡 天啓の判定用：これまでに感情を何点使ったか（＝どれだけ文化に投資したか）
    private int totalSpent;
    public int TotalSpent => totalSpent;

    // ============ 💾 セーブ / ロード（[[SaveSystem]]） ============
    /// <summary>解放フラグだけを保存できる形へ写す。</summary>
    public void OnBeforeSave()
    {
        unlockedSave = new List<int>();
        if (nodes != null) foreach (var n in nodes) if (n.unlocked) unlockedSave.Add((int)n.route * 100 + n.tier);
        if (fusions != null) for (int i = 0; i < fusions.Count; i++) if (fusions[i].unlocked) unlockedSave.Add(10000 + i);
    }

    /// <summary>写した解放フラグを、生きているノードへ戻す。</summary>
    public void OnAfterLoad()
    {
        if (nodes != null) foreach (var n in nodes) n.unlocked = false;
        if (fusions != null) foreach (var n in fusions) n.unlocked = false;
        if (unlockedSave == null) return;
        foreach (int k in unlockedSave)
        {
            if (k >= 10000) { int i = k - 10000; if (fusions != null && i < fusions.Count) fusions[i].unlocked = true; }
            else { var n = Get((Route)(k / 100), k % 100); if (n != null) n.unlocked = true; }
        }
    }

    public void CountChest() { chestsOpened++; }
    public void CountTrap() { trapsTriggered++; }
    public void CountKill() { kills++; }
    public void CountBossHit() { bossHits++; }
    public void CountEscape() { escapes++; }
    public int Pool(Route r) => pool[(int)r];

    // ---- ノード参照 ----
    public IReadOnlyList<Node> Nodes => nodes;
    public IReadOnlyList<Node> Fusions => fusions;
    public Node Get(Route r, int tier) => nodes.Find(n => n.route == r && n.tier == tier);
    public Node GetFusion(int i) => (i >= 0 && i < fusions.Count) ? fusions[i] : null;
    public bool IsUnlocked(Route r, int tier) { var n = Get(r, tier); return n != null && n.unlocked; }
    public bool IsFusionUnlocked(int i) { var n = GetFusion(i); return n != null && n.unlocked; }

    public int EffectiveCost(Node n) => n.eureka != null && n.eureka() ? Mathf.RoundToInt(n.baseCost * 0.6f) : n.baseCost;
    public bool EurekaReady(Node n) => n.eureka != null && n.eureka();

    /// <summary>複合ノードは両ルートの前提段を満たす必要があり、支払いは両ルートから半分ずつ。</summary>
    public bool FusionPrereqMet(Node n) => n.isFusion && IsUnlocked(n.reqRouteA, n.reqTierA) && IsUnlocked(n.reqRouteB, n.reqTierB);

    public bool CanUnlock(Node n)
    {
        if (n == null || n.unlocked) return false;
        if (n.isFusion)
        {
            if (!FusionPrereqMet(n)) return false;
            int half = EffectiveCost(n) / 2;
            return pool[(int)n.reqRouteA] >= half && pool[(int)n.reqRouteB] >= half;
        }
        if (n.tier > 0 && !IsUnlocked(n.route, n.tier - 1)) return false; // 上位は下位が前提
        return pool[(int)n.route] >= EffectiveCost(n);
    }

    public bool TryUnlock(Node n)
    {
        if (!CanUnlock(n)) return false;
        if (n.isFusion)
        {
            int half = EffectiveCost(n) / 2;
            pool[(int)n.reqRouteA] -= half; pool[(int)n.reqRouteB] -= half; totalSpent += half * 2;
            n.unlocked = true;
            Debug.Log($"✨『複合解禁』{n.name}（{RouteNames[(int)n.reqRouteA]}×{RouteNames[(int)n.reqRouteB]}）");
            return true;
        }
        pool[(int)n.route] -= EffectiveCost(n); totalSpent += EffectiveCost(n);
        n.unlocked = true;
        Debug.Log($"🌟『感情ツリー』{RouteNames[(int)n.route]}『{n.name}』を解禁！");
        return true;
    }
    public bool TryUnlock(Route r, int tier) => TryUnlock(Get(r, tier));

    // ---- 効果（各システムが参照）----
    public int BonusAdventurers
    {
        get
        {
            int n = 0;
            if (IsUnlocked(Route.Joy, 0)) n += 1;
            if (IsUnlocked(Route.Joy, 1)) n += 2;
            if (IsUnlocked(Route.Joy, 2)) n += 2;
            if (IsUnlocked(Route.Joy, 3)) n += 3;
            if (IsFusionUnlocked(0)) n += 1; // 甘い罠
            if (IsFusionUnlocked(3)) n += 2; // 享楽の園
            return n;
        }
    }
    public float DefenderPowerMult
    {
        get
        {
            float m = 1f;
            if (IsUnlocked(Route.Thrill, 0)) m += 0.20f;
            if (IsUnlocked(Route.Thrill, 1)) m += 0.20f;
            if (IsUnlocked(Route.Thrill, 2)) m += 0.25f;
            if (IsUnlocked(Route.Thrill, 3)) m += 0.30f;
            if (IsFusionUnlocked(1)) m += 0.15f; // 闘技場
            if (IsFusionUnlocked(3)) m += 0.15f; // 享楽の園
            return m;
        }
    }
    public float TrapDamageMult
    {
        get
        {
            float m = 1f;
            if (IsUnlocked(Route.Despair, 0)) m *= 1.5f;
            if (IsUnlocked(Route.Despair, 1)) m *= 1.5f;
            if (IsUnlocked(Route.Despair, 3)) m *= 1.6f;
            if (IsFusionUnlocked(0)) m *= 1.4f; // 甘い罠
            return m;
        }
    }
    /// <summary>絶望『呪縛』：罠の状態異常の持続倍率。</summary>
    public float TrapStatusDurMult => IsUnlocked(Route.Despair, 2) ? 1.5f : 1f;
    public float KillDPMult
    {
        get
        {
            float m = 1f;
            if (IsUnlocked(Route.Slaughter, 3)) m = 2.2f;
            else if (IsUnlocked(Route.Slaughter, 2)) m = 1.8f;
            else if (IsUnlocked(Route.Slaughter, 0)) m = 1.5f;
            if (IsFusionUnlocked(1)) m *= 1.3f; // 闘技場
            return m;
        }
    }
    public int KillMaterialBonus
    {
        get
        {
            int n = 0;
            if (IsUnlocked(Route.Slaughter, 1)) n += 1;
            if (IsUnlocked(Route.Slaughter, 2)) n += 1;
            if (IsFusionUnlocked(2)) n += 1; // 恐怖支配
            return n;
        }
    }
    /// <summary>『恐怖支配』：泳がせても脅威度の上がり方が緩やか（誘導経済の緩和）。</summary>
    public float ThreatGrowthMult => IsFusionUnlocked(2) ? 0.7f : 1f;

    /// <summary>🔬 研究連携：各ルート最終段が毎ターンの研究点を生む。</summary>
    public int ResearchPointBonus
    {
        get
        {
            int n = 0;
            if (IsUnlocked(Route.Joy, 3)) n++;
            if (IsUnlocked(Route.Thrill, 3)) n++;
            if (IsUnlocked(Route.Despair, 3)) n++;
            if (IsUnlocked(Route.Slaughter, 3)) n++;
            return n;
        }
    }
}
