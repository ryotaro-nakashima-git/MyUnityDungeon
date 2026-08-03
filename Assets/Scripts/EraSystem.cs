using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⏳ 時代・偉業・誓約・災厄（Civ VII の Age / Triumph / Dedication / Crisis）。C3。
///
/// Civ VII 1.4.0 の形をそのまま持ち込む:
/// - 世界は **3つの時代** を進む。時代は「ターン数」ではなく **偉業(Triumph)の達成** で進む。
/// - **小偉業** ＝ 達成すると即時報酬。**大偉業** ＝ 次の時代へ持ち込む **誓約(Dedication)** が1枚解禁される。
/// - 時代の変わり目に、解禁した誓約から **3枚だけ** 選ぶ。全部が等価なので「どれを取っても強い」。
/// - 時代の終盤には **災厄(Crisis)** が来て、**必ず負の政策を1枚選ぶ**。逃げ道は無い。
///
/// この作品では、時代＝**魔王がどれだけ世に知られたか**。進むほど冒険者の水準が上がる（諸刃）。
/// 純static・実行時保持。関連: [[civ7-roadmap]] [[difficulty-curve-orders]]。
/// </summary>
public static class EraSystem
{
    public enum Era { Dawn = 0, Growth = 1, End = 2 }

    public static Era Current { get; private set; }
    public static int Progress { get; private set; }         // 0..100
    public const int Need = 100;
    public const int CrisisAt = 75;                          // ここを超えると災厄が始まる

    public static string EraName(Era e) => e == Era.Dawn ? "胎動の時代" : e == Era.Growth ? "伸長の時代" : "終焉の時代";
    public static string EraDesc(Era e) => e == Era.Dawn ? "まだ誰も、この迷宮を脅威とは思っていない。"
        : e == Era.Growth ? "名が知れ渡り、ギルドと国家が本腰を入れ始めた。"
        : "勇者が立ち、世界が総力で潰しにかかる。";
    /// <summary>時代が進むほど来る冒険者が強い（諸刃）。※対数でも上限でもない直接の底上げなので小さく。</summary>
    public static float TierBias => Current == Era.Dawn ? 0f : Current == Era.Growth ? 0.6f : 1.2f;

    // ============ 🏅 偉業（Triumph） ============
    public struct TriumphDef
    {
        public string id, jpName, cond;
        public Era era;
        public bool major;                 // 大偉業＝誓約が1枚解禁される
        public int dp, mat, rp, emo, fame;
        // 🎖️ レガシーの道（Civ VII）：偉業は6つの軸のどれかに属し、達成でその軸の**属性ポイント**が入る。
        //    小偉業=1点／大偉業=2点。→ [[AttributeSystem]]
        public AttributeSystem.Axis axis;
    }

    private static readonly TriumphDef[] triumphs =
    {
        // ── 胎動の時代 ──
        T("t0_kill",   Era.Dawn,   false, AttributeSystem.Axis.War,     "冒険者を20体倒す",           300, 0, 0, 40, 0),
        T("t0_floor",  Era.Dawn,   false, AttributeSystem.Axis.Expand,  "階層を3つ作る",              500, 0, 0, 0, 0),
        T("t0_trap",   Era.Dawn,   false, AttributeSystem.Axis.Science, "罠でとどめを15回さす",         0, 10, 0, 0, 0),
        T("t0_settle", Era.Dawn,   false, AttributeSystem.Axis.Wealth,  "拠点を3つ持つ",                0, 0, 8, 0, 0),
        T("t0_kin",    Era.Dawn,   true,  AttributeSystem.Axis.Culture, "眷属に真名を与える",           0, 0, 0, 0, 30),
        T("t0_terr",   Era.Dawn,   true,  AttributeSystem.Axis.Expand,  "版図を30タイルにする",         0, 0, 0, 0, 40),
        // ── 伸長の時代 ──
        T("t1_dist",   Era.Growth, false, AttributeSystem.Axis.Wealth,  "施設を5つ建てる",           1200, 0, 0, 0, 0),
        T("t1_wonder", Era.Growth, false, AttributeSystem.Axis.Culture, "遺産のある領域を支配する",     0, 0, 0, 0, 50),
        T("t1_magic",  Era.Growth, false, AttributeSystem.Axis.Science, "魔法でとどめを30回さす",       0, 0, 14, 0, 0),
        T("t1_city",   Era.Growth, false, AttributeSystem.Axis.Diplo,   "都市を2つ持つ",                0, 25, 0, 0, 0),
        T("t1_rival",  Era.Growth, true,  AttributeSystem.Axis.War,     "他の魔王を1人排除する",        0, 0, 0, 60, 0),
        T("t1_level",  Era.Growth, true,  AttributeSystem.Axis.Science, "配下をLv50まで育てる",         0, 0, 20, 0, 0),
        // ── 終焉の時代 ──
        T("t2_kill",   Era.End,    false, AttributeSystem.Axis.War,     "冒険者を300体倒す",            0, 0, 0, 200, 0),
        T("t2_relic",  Era.End,    false, AttributeSystem.Axis.Culture, "遺物を8つ集める",           4000, 0, 0, 0, 0),
        T("t2_terr",   Era.End,    false, AttributeSystem.Axis.Expand,  "版図を150タイルにする",        0, 60, 0, 0, 0),
        T("t2_lord",   Era.End,    false, AttributeSystem.Axis.Diplo,   "魔王をLv35まで育てる",         0, 0, 30, 0, 0),
        T("t2_conq",   Era.End,    true,  AttributeSystem.Axis.War,     "他の魔王を全員排除する",       0, 0, 0, 0, 200),
        T("t2_deep",   Era.End,    true,  AttributeSystem.Axis.Wealth,  "階層を6つ作る",             6000, 0, 0, 0, 0),
    };

    private static TriumphDef T(string id, Era e, bool major, AttributeSystem.Axis axis, string cond, int dp, int mat, int rp, int emo, int fame)
        => new TriumphDef { id = id, era = e, major = major, axis = axis, cond = cond, jpName = cond, dp = dp, mat = mat, rp = rp, emo = emo, fame = fame };

    public static int TriumphCount => triumphs.Length;
    public static TriumphDef Triumph(int i) => triumphs[Mathf.Clamp(i, 0, triumphs.Length - 1)];
    public static int ProgressOf(TriumphDef t) => t.major ? 26 : 12;   // 小4＋大2でちょうど100

    private static HashSet<string> achieved;
    private static void EnsureInit()
    {
        if (achieved != null) return;
        achieved = new HashSet<string>();
        unlockedDedications = new List<int>();
        chosenDedications = new List<int>();
        crisisPolicy = -1;
    }
    public static void Reset() { achieved = null; Current = Era.Dawn; Progress = 0; CrisisActive = false; EnsureInit(); }
    public static bool IsAchieved(string id) { EnsureInit(); return achieved.Contains(id); }

    /// <summary>外から時代の進行を足す（物語事件など）。</summary>
    public static void AddProgress(int n)
    {
        EnsureInit();
        Progress = Mathf.Clamp(Progress + n, 0, Need);
        Debug.Log($"⏳『時代の進行』+{n} → {Progress}/{Need}");
    }

    /// <summary>いまの時代の偉業を並べる。</summary>
    public static List<TriumphDef> CurrentTriumphs()
    {
        var l = new List<TriumphDef>();
        foreach (var t in triumphs) if (t.era == Current) l.Add(t);
        return l;
    }

    // ============ 📜 誓約（Dedication）＝大偉業で解禁し、時代の変わり目に3枚選ぶ ============
    public struct DedicationDef { public string jpName, desc; public string colorHex; }
    private static readonly DedicationDef[] dedications =
    {
        D("叡智の誓い", "毎ターン 研究点 +5",                        "#8cb8e6"),
        D("熱狂の誓い", "毎ターン 感情 +8",                          "#c04a6a"),
        D("豊穣の誓い", "拠点の食料 +2",                             "#5cc47c"),
        D("城塞の誓い", "自領すべての守り +80",                      "#b478e6"),
        D("簒奪の誓い", "他の魔王への侵攻で戦力 +25%",               "#e05a5a"),
        D("静謐の誓い", "全拠点の不満 -2",                           "#57c3ab"),
        D("軍旅の誓い", "眷属の移動力 +1",                           "#e3a94a"),
        D("黄金の誓い", "領域のDP +20%",                             "#e3c34a"),
        D("開墾の誓い", "国境の拡張 +40%",                           "#8ec46a"),
        D("秘匿の誓い", "得る名声 -20%（世に知られる速さを抑える）", "#9c95b4"),
    };
    private static DedicationDef D(string n, string d, string c) => new DedicationDef { jpName = n, desc = d, colorHex = c };
    public static int DedicationCount => dedications.Length;
    public static DedicationDef Dedication(int i) => dedications[Mathf.Clamp(i, 0, dedications.Length - 1)];

    private static List<int> unlockedDedications, chosenDedications;
    public static IReadOnlyList<int> Unlocked { get { EnsureInit(); return unlockedDedications; } }
    public static IReadOnlyList<int> Chosen { get { EnsureInit(); return chosenDedications; } }
    public const int MaxChosen = 3;
    public static bool HasDedication(int i) { EnsureInit(); return chosenDedications.Contains(i); }

    public static bool TryChooseDedication(int i)
    {
        EnsureInit();
        if (!unlockedDedications.Contains(i)) { Debug.LogWarning("⚠️ その誓約はまだ解禁されていません（大偉業で解禁）。"); return false; }
        if (chosenDedications.Contains(i)) { chosenDedications.Remove(i); Debug.Log($"📜『誓約を解く』{Dedication(i).jpName}"); return true; }
        if (chosenDedications.Count >= MaxChosen) { Debug.LogWarning($"⚠️ 誓約は{MaxChosen}枚までです（どれかを解いてください）。"); return false; }
        chosenDedications.Add(i);
        Debug.Log($"📜『誓約』{Dedication(i).jpName} ― {Dedication(i).desc}");
        return true;
    }

    private static void UnlockDedication()
    {
        // まだ解禁していないものから1つ（順に解禁して、選ぶ楽しみは「3枚に絞る」ほうに置く）
        for (int i = 0; i < dedications.Length; i++)
            if (!unlockedDedications.Contains(i))
            {
                unlockedDedications.Add(i);
                Debug.Log($"📜『誓約が解禁された』{Dedication(i).jpName} ― {Dedication(i).desc}（時代の変わり目に3枚まで選べます）");
                return;
            }
    }

    // ============ ☄️ 災厄（Crisis）＝時代の終盤に必ず負の政策を1枚選ぶ ============
    public struct CrisisDef { public string jpName, desc; }
    private static readonly CrisisDef[] crises =
    {
        Cr("飢饉", "拠点の食料 -2"),
        Cr("叛乱", "全拠点の不満 +2"),
        Cr("枯渇", "領域のDP -25%"),
        Cr("侵攻", "他の魔王の力 +30%"),
        Cr("停滞", "国境の拡張 -50%"),
    };
    private static CrisisDef Cr(string n, string d) => new CrisisDef { jpName = n, desc = d };
    public static int CrisisCount => crises.Length;
    public static CrisisDef Crisis(int i) => crises[Mathf.Clamp(i, 0, crises.Length - 1)];

    public static bool CrisisActive { get; private set; }
    private static int crisisPolicy = -1;
    public static int CrisisPolicy { get { EnsureInit(); return crisisPolicy; } }

    public static bool TryChooseCrisisPolicy(int i)
    {
        EnsureInit();
        if (!CrisisActive) { Debug.LogWarning("⚠️ いまは災厄ではありません。"); return false; }
        if (crisisPolicy >= 0) { Debug.LogWarning("⚠️ 災厄の政策は変えられません。"); return false; }
        crisisPolicy = Mathf.Clamp(i, 0, crises.Length - 1);
        Debug.Log($"☄️『災厄の政策』{Crisis(crisisPolicy).jpName} ― {Crisis(crisisPolicy).desc} を選んだ（この時代のあいだ続く）");
        return true;
    }

    // ============ 効果（各systemはここを見る） ============
    public static int RpPerTurn => HasDedication(0) ? 5 : 0;
    public static int EmotionPerTurn => HasDedication(1) ? 8 : 0;
    public static int FoodBonus => (HasDedication(2) ? 2 : 0) + (CrisisPolicy == 0 ? -2 : 0);
    public static int DefenseBonus => HasDedication(3) ? 80 : 0;
    public static float ConquerMult => HasDedication(4) ? 1.25f : 1f;
    public static int UnhappyDelta => (HasDedication(5) ? -2 : 0) + (CrisisPolicy == 1 ? 2 : 0);
    public static int MoveBonus => HasDedication(6) ? 1 : 0;
    public static float RegionDpMult => (HasDedication(7) ? 1.2f : 1f) * (CrisisPolicy == 2 ? 0.75f : 1f);
    public static float BorderMult => (HasDedication(8) ? 1.4f : 1f) * (CrisisPolicy == 4 ? 0.5f : 1f);
    public static float FameMult => HasDedication(9) ? 0.8f : 1f;
    public static float RivalPowerMult => CrisisPolicy == 3 ? 1.3f : 1f;

    // ============ 毎ターンの判定 ============
    public static void TickTurn()
    {
        EnsureInit();
        // 誓約の毎ターン効果
        if (RpPerTurn > 0) ResearchState.AddRP(RpPerTurn);
        var et = EmotionTreeManager.Instance;
        if (et != null && EmotionPerTurn > 0)
            for (int i = 0; i < 4; i++) et.AddEmotion((EmotionTreeManager.Route)i, Mathf.Max(1, EmotionPerTurn / 4));

        // 偉業の判定
        foreach (var t in triumphs)
        {
            if (t.era != Current || achieved.Contains(t.id)) continue;
            bool ok = false;
            try { ok = Check(t.id); } catch { ok = false; }
            if (!ok) continue;
            achieved.Add(t.id);
            Progress = Mathf.Min(Need, Progress + ProgressOf(t));
            var res = DungeonResourceManager.Instance;
            if (res != null) { if (t.dp > 0) res.AddDP(t.dp); if (t.mat > 0) res.AddMaterial(t.mat); if (t.fame > 0) res.AddFame(t.fame); }
            if (t.rp > 0) ResearchState.AddRP(t.rp);
            if (et != null && t.emo > 0) for (int i = 0; i < 4; i++) et.AddEmotion((EmotionTreeManager.Route)i, Mathf.Max(1, t.emo / 4));
            AttributeSystem.AddPoint(t.axis, t.major ? 2 : 1, t.cond);   // 🎖️ レガシーの道 → 属性ポイント
            Debug.Log($"🏅『{(t.major ? "大偉業" : "偉業")}』{t.cond} を達成（時代の進行 +{ProgressOf(t)} → {Progress}/{Need}／{AttributeSystem.AxisName(t.axis)}+{(t.major ? 2 : 1)}）");
            if (t.major) UnlockDedication();
        }

        // 災厄の始まり
        if (!CrisisActive && Progress >= CrisisAt && Current != Era.End)
        {
            CrisisActive = true;
            Debug.Log($"☄️『災厄』{EraName(Current)}の終わりが近い。**負の政策を1つ選ばなければならない**（時代パネルから）。");
        }
        // 時代の移り変わり（災厄の政策を選ぶまで進まない）
        if (Progress >= Need && Current != Era.End)
        {
            if (CrisisActive && crisisPolicy < 0) return;   // 政策を選ぶまで足止め
            Advance();
        }
    }

    private static void Advance()
    {
        Current = (Era)((int)Current + 1);
        Progress = 0; CrisisActive = false; crisisPolicy = -1;
        KinRoster.OnEraChanged();   // 🎖️ 指揮官は時代を越える（昇進は残り、傷は癒える）
        Debug.Log($"⏳『時代が変わった』── {EraName(Current)} ──　{EraDesc(Current)}"
            + $"（世界水準+{TierBias:0.0}／誓約は{chosenDedications.Count}/{MaxChosen}枚）");
    }

    /// <summary>偉業ごとの達成条件。</summary>
    private static bool Check(string id)
    {
        var dl = DemonLord.Instance;
        var fm = DungeonFloorManager.Instance;
        switch (id)
        {
            case "t0_kill": return EurekaTracker.Count("kill") >= 20;
            case "t0_floor": return fm != null && fm.BuiltFloorCount >= 3;
            case "t0_trap": return EurekaTracker.Count("trapKill") >= 15;
            case "t0_settle": return SettlementSystem.SettlementCount >= 3;
            case "t0_kin": return EurekaTracker.Count("kin") >= 1;
            case "t0_terr": return TerritoryTotal() >= 30;

            case "t1_dist": return EurekaTracker.Count("district") >= 5;
            case "t1_wonder": { foreach (var r in SurfaceMap.All) if (r.owned && r.wonderIndex >= 0) return true; return false; }
            case "t1_magic": return EurekaTracker.Count("magicKill") >= 30;
            case "t1_city": return SettlementSystem.CityCount >= 2;
            case "t1_rival": return RivalLords.AliveCount < RivalLords.Count;
            case "t1_level": return TopMinionLevel() >= 50;

            case "t2_kill": return EurekaTracker.Count("kill") >= 300;
            case "t2_relic": return RelicManager.Instance != null && RelicManager.Instance.UnlockedCount >= 8;
            case "t2_terr": return TerritoryTotal() >= 150;
            case "t2_lord": return dl != null && dl.Level >= 35;
            case "t2_conq": return RivalLords.AliveCount == 0;
            case "t2_deep": return fm != null && fm.BuiltFloorCount >= 6;
        }
        return false;
    }

    private static int TerritoryTotal()
    {
        int n = 0;
        foreach (var r in SurfaceMap.All) if (SettlementSystem.SettlementOf(r.id) >= 0) n++;
        return n;
    }
    private static int TopMinionLevel()
    {
        int m = 0; foreach (var v in MinionRoster.All) if (v.level > m) m = v.level; return m;
    }

    /// <summary>ヘッダ用の一行。</summary>
    public static string HeaderLine()
    {
        EnsureInit();
        string s = "<color=#c9a8ff>" + EraName(Current) + "</color> <size=90%>" + Progress + "/" + Need + "</size>";
        if (CrisisActive) s += crisisPolicy < 0
            ? "　<color=#e05a5a>☄災厄 ― 政策を選んでください</color>"
            : "　<color=#e08a3c>☄" + Crisis(crisisPolicy).jpName + "</color>";
        if (chosenDedications.Count > 0)
        {
            s += "　<color=#9c95b4>誓約</color>";
            foreach (int i in chosenDedications) s += " <color=" + Dedication(i).colorHex + ">" + Dedication(i).jpName + "</color>";
        }
        return s;
    }
}
