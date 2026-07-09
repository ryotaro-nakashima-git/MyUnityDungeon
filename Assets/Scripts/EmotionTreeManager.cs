using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 感情ツリー（歓喜/興奮/絶望/殺戮）＋Eurekaブースト。
/// 冒険者の体験で感情が貯まり、ノードを解禁して効果を得る。テーマ達成でコスト割引(Eureka)。
/// </summary>
public class EmotionTreeManager : MonoBehaviour
{
    public static EmotionTreeManager Instance { get; private set; }

    public enum Route { Joy, Thrill, Despair, Slaughter } // 歓喜/興奮/絶望/殺戮
    public static readonly string[] RouteNames = { "歓喜", "興奮", "絶望", "殺戮" };

    public class Node
    {
        public Route route; public int tier; public string name; public int baseCost;
        public bool unlocked; public System.Func<bool> eureka; public string eurekaHint;
    }

    private readonly int[] pool = new int[4];
    private List<Node> nodes;
    // Eureka用カウンタ
    private int chestsOpened, trapsTriggered, kills, bossHits;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildNodes();
    }

    private void BuildNodes()
    {
        nodes = new List<Node>
        {
            new Node{ route=Route.Joy, tier=0, name="歓待", baseCost=20, eureka=()=>chestsOpened>=10, eurekaHint="宝箱10回" },
            new Node{ route=Route.Joy, tier=1, name="宝物庫", baseCost=50, eureka=()=>IsUnlocked(Route.Joy,0), eurekaHint="歓待解禁" },
            new Node{ route=Route.Thrill, tier=0, name="闘技", baseCost=20, eureka=()=>bossHits>=20, eurekaHint="魔王攻撃20回" },
            new Node{ route=Route.Thrill, tier=1, name="死闘", baseCost=50, eureka=()=>IsUnlocked(Route.Thrill,0), eurekaHint="闘技解禁" },
            new Node{ route=Route.Despair, tier=0, name="恐怖", baseCost=20, eureka=()=>trapsTriggered>=10, eurekaHint="罠10回" },
            new Node{ route=Route.Despair, tier=1, name="絶望の淵", baseCost=50, eureka=()=>IsUnlocked(Route.Despair,0), eurekaHint="恐怖解禁" },
            new Node{ route=Route.Slaughter, tier=0, name="処刑", baseCost=20, eureka=()=>kills>=10, eurekaHint="撃破10体" },
            new Node{ route=Route.Slaughter, tier=1, name="屠殺", baseCost=50, eureka=()=>IsUnlocked(Route.Slaughter,0), eurekaHint="処刑解禁" },
        };
    }

    // ---- 感情/カウンタの獲得 ----
    public void AddEmotion(Route r, int amt) { pool[(int)r] += amt; }
    public void CountChest() { chestsOpened++; }
    public void CountTrap() { trapsTriggered++; }
    public void CountKill() { kills++; }
    public void CountBossHit() { bossHits++; }
    public int Pool(Route r) => pool[(int)r];

    // ---- ノード解禁 ----
    public IReadOnlyList<Node> Nodes => nodes;
    public Node Get(Route r, int tier) => nodes.Find(n => n.route == r && n.tier == tier);
    public bool IsUnlocked(Route r, int tier) { var n = Get(r, tier); return n != null && n.unlocked; }
    public int EffectiveCost(Node n) => n.eureka != null && n.eureka() ? Mathf.RoundToInt(n.baseCost * 0.6f) : n.baseCost;
    public bool EurekaReady(Node n) => n.eureka != null && n.eureka();
    public bool CanUnlock(Node n)
    {
        if (n == null || n.unlocked) return false;
        if (n.tier == 1 && !IsUnlocked(n.route, 0)) return false; // 上位は下位が前提
        return pool[(int)n.route] >= EffectiveCost(n);
    }
    public bool TryUnlock(Route r, int tier)
    {
        var n = Get(r, tier);
        if (!CanUnlock(n)) return false;
        pool[(int)n.route] -= EffectiveCost(n);
        n.unlocked = true;
        Debug.Log($"🌟【感情ツリー】{RouteNames[(int)r]}『{n.name}』を解禁！");
        return true;
    }

    // ---- 効果（各システムが参照）----
    public int BonusAdventurers => (IsUnlocked(Route.Joy, 0) ? 1 : 0) + (IsUnlocked(Route.Joy, 1) ? 2 : 0); // 歓喜=集客
    public float DefenderPowerMult => 1f + (IsUnlocked(Route.Thrill, 0) ? 0.2f : 0f) + (IsUnlocked(Route.Thrill, 1) ? 0.2f : 0f); // 興奮=防衛体強化
    public float TrapDamageMult { get { float m = 1f; if (IsUnlocked(Route.Despair, 0)) m *= 1.5f; if (IsUnlocked(Route.Despair, 1)) m *= 1.5f; return m; } } // 絶望=罠強化
    public float KillDPMult => IsUnlocked(Route.Slaughter, 0) ? 1.5f : 1f;   // 殺戮=撃破DP
    public int KillMaterialBonus => IsUnlocked(Route.Slaughter, 1) ? 1 : 0;  // 殺戮=素材
}
