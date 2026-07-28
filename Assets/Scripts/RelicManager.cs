using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🏺 遺物（レリック）＝3層バフの最上位『全体パッシブ』層。
/// - **実績で1つずつ解放**される（自由に全部使えると選択の悩みが無いため）。解放は撃破/踏破/研究などの達成で発火。
/// - 解放済みの中から **スロット数だけ** 装備できる。スロットは領域研究 d_relic2 / d_relic3 で 1→2→3。
/// - 効果は各システム（防衛体/罠/撃破DP/感情/研究/錬成/魔王/誘導経済）が getter を参照して乗算する。
/// 関連: [[dangeon-3-current-code]] TotemCatalog(範囲層) / MinionRoster(点の層)。
/// </summary>
public class RelicManager : MonoBehaviour
{
    public static RelicManager Instance { get; private set; }

    public enum Effect
    {
        DefenderHp, DefenderAtk, TrapDamage, KillDP,        // 基礎
        FamilyUndead, FamilyBeast, FamilyDemonkin,          // 家系特化（編成と絡む）
        DeepFloor,          // 最下層の配下を強化（深度と絡む）
        DepthBonus,         // 深度報酬倍率を上乗せ（深く潜らせるほど旨い）
        KillDPRisky,        // 撃破DP大幅＋だが脅威度上昇も増える（誘導経済のトレードオフ）
        QuietBell,          // 集客-だが冒険者が弱くなる
        ResearchRP,         // 毎ターン研究点
        ForgeCost,          // 鍛造費割引
        StatusDuration,     // 罠の状態異常時間
        EmotionGain,        // 感情獲得
        DemonLordCore,      // 魔王HP＋反撃魔法の階級+1
    }

    public class Relic
    {
        public string name; public string desc; public Effect effect; public float value;
        public string howTo;                 // 解放条件の表示文
        public System.Func<bool> condition;  // 解放条件の判定
    }

    [SerializeField] private int baseSlotCount = 1;   // 基礎1。研究で最大3。（旧slotCountから改名＝シーンの旧値を引き継がない）

    private List<Relic> catalog;
    private int[] slots;                          // 各スロットのカタログindex（未装備=-1）
    private bool[] unlocked;                      // 実績で解放済みか

    // ── 実績カウンタ（ウェーブ中に各所から加算される）──
    private static int bestFloorHeld = 0;      // 守り切った最深フロア(1始まり)
    private static int topHeroRankBeaten = -1; // 撃破した冒険者の最高ランク
    private static int trapKills = 0;          // 罠で削り切った数（罠ダメージでとどめ）
    private static int flawlessWaves = 0;      // 防衛体が1体も落ちなかったウェーブ数
    private static int bossAppointed = 0;      // ボス任命した回数
    private static int rivalsDefeated = 0;     // 🔥 真核を奪って排除した他魔王の数
    private static int defenderLostThisWave = 0;

    public int SlotCount => Mathf.Min(3, Mathf.Max(1, baseSlotCount)
        + (ResearchState.IsResearched("d_relic2") ? 1 : 0)
        + (ResearchState.IsResearched("d_relic3") ? 1 : 0));
    public IReadOnlyList<Relic> Catalog => catalog;
    public int SlotAt(int i) => (slots != null && i >= 0 && i < slots.Length) ? slots[i] : -1;
    public bool IsUnlocked(int i) => unlocked != null && i >= 0 && i < unlocked.Length && unlocked[i];
    public int UnlockedCount { get { int n = 0; if (unlocked != null) foreach (var u in unlocked) if (u) n++; return n; } }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildCatalog();
        slots = new int[3];
        for (int i = 0; i < slots.Length; i++) slots[i] = -1;
        unlocked = new bool[catalog.Count];
        CheckUnlocks(); // 開始時点で満たしているもの（最初の1個）を解放
    }

    private void BuildCatalog()
    {
        catalog = new List<Relic>
        {
            R("不死の王笏", "全防衛体のHP +25%", Effect.DefenderHp, 0.25f,
              "最初から所持している", () => true),
            R("獣爪の紋章", "全防衛体の攻撃 +25%", Effect.DefenderAtk, 0.25f,
              "配下を5体以上ロスターに揃える", () => MinionRoster.All.Count >= 5),
            R("業火の宝珠", "罠のダメージ +60%", Effect.TrapDamage, 0.60f,
              "罠で冒険者を10体倒す", () => trapKills >= 10),
            R("強欲の金貨", "撃破時のDP +40%", Effect.KillDP, 0.40f,
              "撃破で得たDPが累計3000を超える", () => DungeonResourceManager.TotalKillDP >= 3000),

            R("屍山の旗", "『不死』の配下 HP・攻撃 +35%", Effect.FamilyUndead, 0.35f,
              "不死系の個体を8体以上所持する", () => MinionRoster.CountOfFamily(ZombieAI.Species.Undead) >= 8),
            R("獣王の毛皮", "『獣』の配下 HP・攻撃 +35%", Effect.FamilyBeast, 0.35f,
              "獣系の個体を8体以上所持する", () => MinionRoster.CountOfFamily(ZombieAI.Species.Beast) >= 8),
            R("魔導書『黒の頁』", "『魔族』の配下 HP・攻撃 +35%", Effect.FamilyDemonkin, 0.35f,
              "魔族系の個体を8体以上所持する", () => MinionRoster.CountOfFamily(ZombieAI.Species.Demonkin) >= 8),

            R("深淵の鏡", "最下層の配下 HP・攻撃 +40%", Effect.DeepFloor, 0.40f,
              "3層以上の迷宮を築く", () => DungeonFloorManager.Instance != null && DungeonFloorManager.Instance.BuiltFloorCount >= 3),
            R("深度の王冠", "階層ごとの報酬倍率 +0.10/階", Effect.DepthBonus, 0.10f,
              "B3F まで到達された上で守り切る", () => bestFloorHeld >= 3),

            R("英雄の首飾り", "撃破DP +60%／ただし脅威度の上昇 +30%", Effect.KillDPRisky, 0.60f,
              "Aランク以上の冒険者を倒す", () => topHeroRankBeaten >= 6),
            R("静寂の鈴", "集客 -20%／冒険者のHP -15%", Effect.QuietBell, 0.15f,
              "防衛体を1体も失わずにウェーブを守り切る", () => flawlessWaves >= 1),

            R("賢者の石", "毎ターン 研究点 +2", Effect.ResearchRP, 2f,
              "研究を10ノード完了する", () => ResearchState.ResearchedCount >= 10),
            R("錬金の坩堝", "鍛造費 -30%", Effect.ForgeCost, 0.30f,
              "武具を『鋼』以上に鍛造する", () => HasForgedGrade(2)),
            R("呪縛の鎖", "罠の状態異常の持続 +50%", Effect.StatusDuration, 0.50f,
              "絶望ルートを3段まで進める", () => EmotionTreeManager.Instance != null && EmotionTreeManager.Instance.IsUnlocked(EmotionTreeManager.Route.Despair, 2)),
            R("収穫の鎌", "感情の獲得 +30%", Effect.EmotionGain, 0.30f,
              "いずれかの感情ルートを最終段まで進める", () => EmotionTreeManager.Instance != null && EmotionTreeManager.Instance.ResearchPointBonus >= 1),
            R("魔王の心臓", "魔王のHP +30%／反撃魔法の階級 +1", Effect.DemonLordCore, 0.30f,
              "配下をボスに任命する（ゴエティアの名を継がせる）", () => bossAppointed >= 1),
            R("簒奪の真核", "全防衛体のHP・攻撃 +30%", Effect.DefenderHp, 0.30f,
              "他の魔王の本拠地を落として真核を奪う", () => rivalsDefeated >= 1),
        };
    }

    private static Relic R(string n, string d, Effect e, float v, string how, System.Func<bool> cond)
        => new Relic { name = n, desc = d, effect = e, value = v, howTo = how, condition = cond };

    private static bool HasForgedGrade(int g)
    {
        foreach (var v in MinionRoster.All) if (v.weaponGrade >= g || v.armorGrade >= g) return true;
        return DemonLord.Instance != null && (DemonLord.Instance.WeaponGrade >= g || DemonLord.Instance.ArmorGrade >= g);
    }

    // ============ 実績フック（各システムから呼ばれる） ============
    public static void ReportFloorHeld(int floorIndex1Based) { if (floorIndex1Based > bestFloorHeld) bestFloorHeld = floorIndex1Based; }
    public static void ReportHeroBeaten(int rank) { if (rank > topHeroRankBeaten) topHeroRankBeaten = rank; }
    public static void ReportTrapKill() { trapKills++; }
    public static void ReportDefenderLost() { defenderLostThisWave++; }
    public static void BeginWave() { defenderLostThisWave = 0; }
    /// <summary>ウェーブ終了時：防衛体を1体も失っていなければ『無失点』を記録。</summary>
    public static void EndWaveFlawlessCheck() { if (defenderLostThisWave == 0) flawlessWaves++; }
    public static void ReportBossAppointed() { bossAppointed++; }
    public static void ReportRivalDefeated() { rivalsDefeated++; }
    public static void ResetProgress() { bestFloorHeld = 0; topHeroRankBeaten = -1; trapKills = 0; flawlessWaves = 0; bossAppointed = 0; rivalsDefeated = 0; defenderLostThisWave = 0; }

    /// <summary>条件を満たした遺物を解放する（ウェーブ終了時などに呼ぶ）。新規解放した数を返す。</summary>
    public int CheckUnlocks()
    {
        if (catalog == null || unlocked == null) return 0;
        int n = 0;
        for (int i = 0; i < catalog.Count; i++)
        {
            if (unlocked[i]) continue;
            bool ok = false;
            try { ok = catalog[i].condition != null && catalog[i].condition(); } catch { ok = false; }
            if (!ok) continue;
            unlocked[i] = true; n++;
            Debug.Log($"🏺『遺物を獲得』『{catalog[i].name}』 ― {catalog[i].desc}");
        }
        return n;
    }

    public bool IsEquipped(int catalogIdx)
    {
        if (slots == null) return false;
        for (int i = 0; i < SlotCount; i++) if (slots[i] == catalogIdx) return true;
        return false;
    }

    /// <summary>トグル装備：装備済みなら外す／未装備なら空きスロットへ／空き無しなら先頭を置換。未解放は不可。</summary>
    public void Toggle(int catalogIdx)
    {
        if (catalog == null || catalogIdx < 0 || catalogIdx >= catalog.Count) return;
        if (!IsUnlocked(catalogIdx))
        {
            Debug.LogWarning($"⚠️ 『{catalog[catalogIdx].name}』は未獲得です（{catalog[catalogIdx].howTo}）。");
            return;
        }
        int cap = SlotCount;
        for (int i = 0; i < cap; i++)
            if (slots[i] == catalogIdx) { slots[i] = -1; Debug.Log($"🏺『遺物』『{catalog[catalogIdx].name}』を外しました"); return; }
        for (int i = 0; i < cap; i++)
            if (slots[i] == -1) { slots[i] = catalogIdx; Debug.Log($"🏺『遺物』『{catalog[catalogIdx].name}』を装備しました"); return; }
        slots[0] = catalogIdx; // 空き無し→先頭スロットを置換
        Debug.Log($"🏺『遺物』『{catalog[catalogIdx].name}』を装備（スロット1を置換）");
    }

    private float Sum(Effect e)
    {
        float v = 0f;
        if (slots == null) return v;
        int cap = SlotCount;
        for (int i = 0; i < cap; i++) if (slots[i] >= 0 && catalog[slots[i]].effect == e) v += catalog[slots[i]].value;
        return v;
    }
    private bool Has(Effect e)
    {
        if (slots == null) return false;
        int cap = SlotCount;
        for (int i = 0; i < cap; i++) if (slots[i] >= 0 && catalog[slots[i]].effect == e) return true;
        return false;
    }

    // ---- 効果（各システムが参照）----
    public float DefenderHpMult => 1f + Sum(Effect.DefenderHp);
    public float DefenderAtkMult => 1f + Sum(Effect.DefenderAtk);
    public float TrapDamageMult => 1f + Sum(Effect.TrapDamage);
    public float KillDPMult => 1f + Sum(Effect.KillDP) + Sum(Effect.KillDPRisky);
    // 🕸️ 誘導経済のトレードオフ：英雄の首飾りは脅威度の上昇も速める
    public float ThreatGrowthMult => 1f + Sum(Effect.KillDPRisky) * 0.5f;
    // 🧬 家系特化（不死/獣/魔族）
    public float FamilyMult(ZombieAI.Species s)
    {
        switch (s)
        {
            case ZombieAI.Species.Beast: return 1f + Sum(Effect.FamilyBeast);
            case ZombieAI.Species.Demonkin: return 1f + Sum(Effect.FamilyDemonkin);
            default: return 1f + Sum(Effect.FamilyUndead);
        }
    }
    // 🏢 最下層だけ強化
    public float DeepFloorMult(bool isDeepest) => isDeepest ? 1f + Sum(Effect.DeepFloor) : 1f;
    public float DepthBonusExtra => Sum(Effect.DepthBonus);
    // 🔔 静寂の鈴：集客を捨てて敵を弱くする
    public float LureMult => 1f - Sum(Effect.QuietBell) * 1.33f;
    public float HeroHpMult => 1f - Sum(Effect.QuietBell);
    public int ResearchRPPerTurn => Mathf.RoundToInt(Sum(Effect.ResearchRP));
    public float ForgeCostMult => Mathf.Max(0.3f, 1f - Sum(Effect.ForgeCost));
    public float StatusDurationMult => 1f + Sum(Effect.StatusDuration);
    public float EmotionGainMult => 1f + Sum(Effect.EmotionGain);
    public float DemonLordHpMult => 1f + Sum(Effect.DemonLordCore);
    public int DemonLordSpellRankBonus => Has(Effect.DemonLordCore) ? 1 : 0;
}
