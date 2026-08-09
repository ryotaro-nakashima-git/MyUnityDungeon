using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 📖 物語事件と形見（Civ VII の Narrative Events / Memento）。C7。
///
/// - **物語事件** … 状況に応じて起き、**2〜3択**を迫る。どれも一長一短で、
///   「名声を稼ぐ＝冒険者が強くなる」というこの作品の両刃と噛み合わせてある。
///   一度起きた事件は二度と起きない（Civと同じく使い切り）。
/// - **形見(Memento)** … **周回を越えて持ち越す** 2枠の装備。実績で解禁され、`PlayerPrefs` に残る。
///   この作品には保存機能がまだ無いので、**形見だけはディスクに残す**（Civの Memento と同じ役割）。
///
/// 純static・実行時保持（形見の解禁だけ永続）。関連: [[EraSystem]] [[civ7-roadmap]]。
/// </summary>
public static class NarrativeSystem
{
    // ============ 📖 物語事件 ============
    public struct Choice
    {
        public string label, desc;
        public int effect;                 // Apply() の分岐
    }
    public struct EventDef
    {
        public string id, title, body;
        public Choice[] choices;
    }

    private static Choice C(string label, string desc, int effect)
        => new Choice { label = label, desc = desc, effect = effect };

    private static readonly EventDef[] events =
    {
        E("n_child", "迷い込んだ子供",
          "冒険者ではない。武器も持たない子供が、入口の暗がりで震えている。配下たちが指示を待っている。",
          C("帰してやる", "名声 -20（世に知られる速さが少し落ちる）", 0),
          C("喰わせる", "感情 +40／名声 +60", 1),
          C("配下にする", "配下を1体、無償で召喚する", 2)),

        E("n_merchant", "商人の申し出",
          "迷宮の噂を聞きつけた行商が、入口の外で待っている。「取引をしませんか。悪い話じゃない」",
          C("取引する", "DP +900／名声 +40", 3),
          C("追い返す", "何も起きない（名声も動かない）", 4),
          C("喰らう", "素材 +35／名声 +70", 5)),

        E("n_rumor", "裏切りの噂",
          "拠点のひとつで、こちらを裏切ろうという囁きがあるという。",
          C("見せしめにする", "全拠点の祝祭ゲージ +25／名声 +50", 6),
          C("懐柔する", "DP -700／全拠点の祝祭ゲージ +40", 7),
          C("放置する", "何もしない（噂は勝手に消える）", 4)),

        E("n_envoy", "他魔王の使者",
          "鬼種の使者が、不可侵の打診を持ってきた。「互いに人間を相手にしたほうが得だろう」",
          C("受ける", "威名 +50／その魔王と8ターンの不可侵", 8),
          C("断る", "全眷属の武勲 +3（士気が上がる）", 9),
          C("使者を殺す", "名声 +90／その魔王の力 +25%", 10)),

        E("n_stele", "古い石碑",
          "版図の外れに、読めない文字の刻まれた石碑が立っている。",
          C("研究する", "研究点 +28", 11),
          C("壊して使う", "素材 +45／名声 +30", 12),
          C("祀る", "感情 +70", 13)),

        E("n_hunger", "飢えた眷属",
          "真名を持つ者が、いつになく苛立っている。力が渇いているのだという。",
          C("配下を与える", "配下を1体失う／全眷属の武勲 +6", 14),
          C("我慢させる", "眷属が2ターン動けなくなる", 15),
          C("冒険者を与える", "名声 +60／感情 +40", 16)),

        E("n_hero", "勇者の噂",
          "国が『勇者』を立てたという噂が流れてきた。まだ姿は見えない。",
          C("迎え撃つ支度", "DP -1200／全自領の砦がひとつ進む", 17),
          C("潜む", "名声 -90（世に知られる速さを大きく抑える）", 18),
          C("挑発する", "名声 +150／DP +1800", 19)),

        E("n_vein", "鉱脈の発見",
          "版図の地下に、太い鉱脈が見つかった。",
          C("掘る", "素材 +70", 20),
          C("売る", "DP +1400／威名 +20", 21),
          C("封じる", "研究点 +22（危険な鉱脈だった）", 22)),

        E("n_plague", "疫病",
          "拠点に病が広がっている。人が減れば産出も落ちる。",
          C("隔離する", "全拠点の食料の蓄えを失う", 23),
          C("感情の糧にする", "感情 +90／人口が1つ減る", 24),
          C("放置する", "全拠点の祝祭ゲージを失う", 25)),

        E("n_indep", "自治都市の使者",
          "独立勢力の使者が、こちらの出方をうかがっている。",
          C("贈り物をする", "威名 -30／未従属の相手すべてに好意 +30", 26),
          C("脅す", "未従属の相手すべてに好意 +50／名声 +50", 27),
          C("無視する", "何も起きない", 4)),

        E("n_voice", "地下の声",
          "最下層の、さらに下から音がする。掘れば何かがあるのは確かだ。",
          C("掘り進む", "DP -900／時代の進行 +10", 28),
          C("塞ぐ", "素材 +35", 29),
          C("耳を澄ます", "研究点 +20／感情 +35", 30)),

        E("n_traitor", "裏切り者の冒険者",
          "パーティを見捨てて逃げた男が、取引を持ちかけてきた。「情報を売る。命だけは」",
          C("迎え入れる", "威名 +35／名声 +25", 31),
          C("殺す", "感情 +60", 32),
          C("泳がせる", "DP +700／名声 +50", 33)),
    };

    private static EventDef E(string id, string title, string body, params Choice[] cs)
        => new EventDef { id = id, title = title, body = body, choices = cs };

    public static int EventCount => events.Length;
    public static EventDef Event(int i) => events[Mathf.Clamp(i, 0, events.Length - 1)];

    private static HashSet<string> seen;
    private static int pending = -1;          // いま選択を待っている事件
    private static int cooldown;
    public const int Cooldown = 3;            // 事件と事件のあいだ

    public static int Pending { get { EnsureInit(); return pending; } }
    public static bool HasPending => Pending >= 0;

    private static void EnsureInit()
    {
        if (slot == null || slot.Length < MaxSlots) slot = new int[] { -1, -1, -1 };   // 枠が増えても壊れない
        if (seen != null) return;
        seen = new HashSet<string>();
        pending = -1; cooldown = 2;
        LoadMementos();
    }
    public static void Reset() { seen = null; pending = -1; EnsureInit(); }

    /// <summary>その事件が起きる状況か。</summary>
    private static bool Fits(string id)
    {
        switch (id)
        {
            case "n_rumor": return SettlementSystem.SettlementCount >= 2;
            case "n_envoy": return RivalLords.AliveCount > 0;
            case "n_stele": return SurfaceMap.OwnedCount >= 4;
            case "n_hunger": return KinRoster.Count >= 1;
            case "n_hero": return EraSystem.Current != EraSystem.Era.Dawn;
            case "n_vein": return SettlementSystem.SettlementCount >= 3;
            case "n_plague": { int pop = 0; foreach (var r in SurfaceMap.All) if (r.owned) pop += r.pop; return pop >= 8; }
            case "n_indep": return DiplomacySystem.Powers.Count > 0 && DiplomacySystem.SuzerainCount < DiplomacySystem.Powers.Count;
            case "n_voice": return DungeonFloorManager.Instance != null && DungeonFloorManager.Instance.BuiltFloorCount >= 3;
            case "n_traitor": return EurekaTracker.Count("kill") >= 15;
            default: return true;
        }
    }

    public static void TickTurn()
    {
        EnsureInit();
        CheckMementoUnlocks();
        if (pending >= 0) return;                 // 選ぶまで次は起きない
        if (cooldown > 0) { cooldown--; return; }

        var cand = new List<int>();
        for (int i = 0; i < events.Length; i++)
            if (!seen.Contains(events[i].id) && Fits(events[i].id)) cand.Add(i);
        if (cand.Count == 0) return;
        pending = cand[Random.Range(0, cand.Count)];
        Debug.Log($"📖『{events[pending].title}』── {events[pending].body}　（物語パネルで選んでください）");
    }

    public static bool Choose(int choiceIndex)
    {
        EnsureInit();
        if (pending < 0) return false;
        var ev = events[pending];
        if (choiceIndex < 0 || choiceIndex >= ev.choices.Length) return false;
        var ch = ev.choices[choiceIndex];
        seen.Add(ev.id);
        pending = -1; cooldown = Cooldown;
        Debug.Log($"📖『{ev.title}』→「{ch.label}」を選んだ ― {ch.desc}");
        Apply(ch.effect);
        return true;
    }

    private static void Apply(int e)
    {
        var res = DungeonResourceManager.Instance;
        var et = EmotionTreeManager.Instance;
        switch (e)
        {
            case 0: if (res != null) res.AddFame(-20); break;
            case 1: Emo(40); if (res != null) res.AddFame(60); break;
            case 2: MinionRoster.TrySummon(Random.Range(0, 6)); break;
            case 3: if (res != null) { res.AddDP(900); res.AddFame(40); } break;
            case 4: break;
            case 5: if (res != null) { res.AddMaterial(35); res.AddFame(70); } break;
            case 6: Celebrate(25); if (res != null) res.AddFame(50); break;
            case 7: if (res != null) res.TrySpendDP(700); Celebrate(40); break;
            case 8: DiplomacySystem.AddInfluence(50); DiplomacySystem.TryMakePeaceFree(0); break;
            case 9: Merit(3); break;
            case 10: if (res != null) res.AddFame(90); RivalPower(0, 1.25f); break;
            case 11: ResearchState.AddRP(28); break;
            case 12: if (res != null) { res.AddMaterial(45); res.AddFame(30); } break;
            case 13: Emo(70); break;
            case 14: LoseOneFollower(); Merit(6); break;
            case 15: foreach (var k in KinRoster.All) k.injuryTurns = Mathf.Max(k.injuryTurns, 2); break;
            case 16: if (res != null) res.AddFame(60); Emo(40); break;
            case 17: if (res != null) res.TrySpendDP(1200); FortifyAll(); break;
            case 18: if (res != null) res.AddFame(-90); break;
            case 19: if (res != null) { res.AddFame(150); res.AddDP(1800); } break;
            case 20: if (res != null) res.AddMaterial(70); break;
            case 21: if (res != null) res.AddDP(1400); DiplomacySystem.AddInfluence(20); break;
            case 22: ResearchState.AddRP(22); break;
            case 23: foreach (var r in SurfaceMap.All) if (r.owned) r.foodStock = 0; break;
            case 24: Emo(90); LosePop(); break;
            case 25: foreach (var r in SurfaceMap.All) if (r.owned) r.happyStock = 0; break;
            case 26: DiplomacySystem.AddInfluence(-30); Favor(30); break;
            case 27: Favor(50); if (res != null) res.AddFame(50); break;
            case 28: if (res != null) res.TrySpendDP(900); EraSystem.AddProgress(10); break;
            case 29: if (res != null) res.AddMaterial(35); break;
            case 30: ResearchState.AddRP(20); Emo(35); break;
            case 31: DiplomacySystem.AddInfluence(35); if (res != null) res.AddFame(25); break;
            case 32: Emo(60); break;
            case 33: if (res != null) { res.AddDP(700); res.AddFame(50); } break;
        }
    }

    private static void Emo(int n)
    {
        var et = EmotionTreeManager.Instance;
        if (et == null) return;
        for (int i = 0; i < 4; i++) et.AddEmotion((EmotionTreeManager.Route)i, Mathf.Max(1, n / 4));
    }
    private static void Celebrate(int n) { foreach (var r in SurfaceMap.All) if (r.owned && r.settle != SurfaceMap.Settle.None) r.happyStock += n; }
    private static void Merit(int n) { foreach (var k in KinRoster.All) KinPromotion.AddMerit(k, n, "物語事件"); }
    private static void RivalPower(int i, float m) { var rv = RivalLords.Get(i); if (!rv.defeated) rv.power *= m; }
    private static void Favor(int n) { foreach (var p in DiplomacySystem.Powers) if (p.suzerain < 0) p.favor = Mathf.Min(DiplomacySystem.FavorNeed, p.favor + n); }
    private static void FortifyAll() { foreach (var r in SurfaceMap.All) if (r.owned && r.fortLevel < SurfaceMap.MaxFort) r.fortLevel++; }
    private static void LosePop() { foreach (var r in SurfaceMap.All) if (r.owned && r.pop > 1) { r.pop--; return; } }
    private static void LoseOneFollower()
    {
        foreach (var k in KinRoster.All)
            if (k.followers.Count > 0) { MinionRoster.Remove(k.followers[0]); k.followers.RemoveAt(0); return; }
    }

    // ============ 🕯️ 形見（Memento）＝周回を越えて持ち越す2枠 ============
    public struct MementoDef { public string jpName, desc, unlock, colorHex; }
    private static readonly MementoDef[] mementos =
    {
        M("折れた真名の刻印", "眷属化のDPが -30%",        "眷属を3体つくる",        "#ffd24a"),
        M("初代の鍵",         "開始時のDP +2500",          "10ターン生き延びる",     "#e3a94a"),
        M("血染めの首飾り",   "冒険者を倒したDPが +12%",   "冒険者を100体倒す",      "#c04a6a"),
        M("灰の懐中時計",     "得る名声 -15%",             "名声を3000貯める",       "#9c95b4"),
        M("竜骨の欠片",       "地上での配下の戦力 +10%",   "配下をLv40まで育てる",   "#b478e6"),
        M("賢者の遺稿",       "開始時の研究点 +40",        "研究を20ノード進める",   "#8cb8e6"),
        M("商人の割符",       "開始時の威名 +80",          "独立勢力を1つ従える",    "#57c3ab"),
        M("旗手の遺品",       "眷属の武勲の獲得 +50%",     "眷属の昇進を6つ修める",  "#e05a5a"),
        // ── F-25 で追加（実績と噛み合わせて「次の周でやること」を増やす）──
        M("静寂の遺灰",       "冒険者の来訪 -15%（質は変わらない）", "10波を無傷で凌ぐ",  "#8c98b4"),
        M("坑夫の鶴嘴",       "素材の獲得 +20%",           "遺物を3つ解放する",      "#c9a06a"),
        M("測量士の羅針",     "地上の移動力 +1",           "領地を40タイル持つ",     "#6ab4c9"),
        M("教条の写本",       "研究の値段 -10%",           "研究を40ノード進める",   "#8cb8e6"),
        M("暴君の玉座",       "他魔王の伸び -12%",         "他の魔王を1人排除する",  "#b0202b"),
        M("時読みの砂",       "開始時のターンが 3 進む",   "40ターン以内に勝ち切る",  "#d8c890"),
        M("宴の面",           "感情の獲得 +15%",           "都市を1つ持つ",          "#e6a0c8"),
        M("先駆者の書付",     "開始時のDP +1200／名声 +300", "5周遊ぶ",              "#a0e6b4"),
    };
    private static MementoDef M(string n, string d, string u, string c)
        => new MementoDef { jpName = n, desc = d, unlock = u, colorHex = c };

    public static int MementoCount => mementos.Length;
    public static MementoDef Memento(int i) => mementos[Mathf.Clamp(i, 0, mementos.Length - 1)];
    public const int MaxSlots = 3;
    /// <summary>
    /// 🕯️ 形見の枠。**実績12個で2枠→3枠**に増える（周回の見返り）。→ [[Achievements]]
    /// ⚠ ここは以前 `const int Slots = 2` だった。**状態で変わる値を const にすると一生反映されない**
    ///    （このプロジェクトで3度目。`SquadMaxSlots` / `EurekaTracker.Discount` に続く）。
    /// </summary>
    public static int Slots { get { return Mathf.Clamp(Achievements.MementoSlots, 2, MaxSlots); } }

    // 🕯️ 形見は**周を越える持ち物**なので PlayerPrefs 側に属する。
    // ⚠ セーブ([[SaveSystem]])に含めない。含めると、別の周のセーブを読んだときに
    //    いま解禁している形見が上書きされるうえ、枠が2→3に増えた後に**古いセーブの長さ2の配列**が
    //    入り込んで範囲外アクセスになる。
    [System.NonSerialized] private static HashSet<int> unlockedM;
    [System.NonSerialized] private static int[] slot = { -1, -1, -1 };
    public static bool IsUnlocked(int i) { EnsureInit(); return unlockedM.Contains(i); }
    public static int SlotOf(int s) { EnsureInit(); return slot[Mathf.Clamp(s, 0, MaxSlots - 1)]; }
    public static bool Equipped(int i)
    {
        EnsureInit();
        for (int s = 0; s < Slots; s++) if (slot[s] == i) return true;
        return false;
    }
    public static int UnlockedCount { get { EnsureInit(); return unlockedM.Count; } }

    private const string PrefUnlocked = "dangeon3.memento.unlocked";
    private const string PrefSlot = "dangeon3.memento.slot";

    private static void LoadMementos()
    {
        unlockedM = new HashSet<int>();
        string s = PlayerPrefs.GetString(PrefUnlocked, "");
        foreach (var t in s.Split(','))
        { int v; if (int.TryParse(t, out v)) unlockedM.Add(v); }
        for (int i = 0; i < MaxSlots; i++)
        {
            slot[i] = PlayerPrefs.GetInt(PrefSlot + i, -1);
            if (!unlockedM.Contains(slot[i])) slot[i] = -1;
        }
    }
    private static void SaveMementos()
    {
        var l = new List<string>(); foreach (int i in unlockedM) l.Add(i.ToString());
        PlayerPrefs.SetString(PrefUnlocked, string.Join(",", l.ToArray()));
        for (int i = 0; i < MaxSlots; i++) PlayerPrefs.SetInt(PrefSlot + i, slot[i]);
        PlayerPrefs.Save();
    }

    /// <summary>形見の解禁条件（満たしたら永続で解禁される）。</summary>
    private static bool MementoCond(int i)
    {
        var dl = DemonLord.Instance;
        switch (i)
        {
            case 0: return KinRoster.Count >= 3;
            case 1: return DungeonTurnManager.Instance != null && DungeonTurnManager.Instance.CurrentTurn >= 10;
            case 2: return EurekaTracker.Count("kill") >= 100;
            case 3: return DungeonResourceManager.Instance != null && DungeonResourceManager.Instance.DungeonFame >= 3000;
            case 4: { foreach (var v in MinionRoster.All) if (v.level >= 40) return true; return false; }
            case 5: return ResearchState.ResearchedCount >= 20;
            case 6: return DiplomacySystem.SuzerainCount >= 1;
            case 7: { int n = 0; foreach (var k in KinRoster.All) n += k.promotions.Count; return n >= 6; }
            // ── F-25：条件は[[Achievements]]と揃えてある（実績を取れば形見も付いてくる）──
            case 8: return RunStats.WavesSurvived >= 10 && !RunStats.AnyDefenderLost;
            case 9: return RelicUnlockedCount() >= 3;
            case 10: return RunStats.PeakRegions >= 40;
            case 11: return ResearchState.ResearchedCount >= 40;
            case 12: return RivalLords.Count - RivalLords.AliveCount >= 1;
            case 13: return RunStats.Wins >= 1 && RunStats.BestTurn > 0 && RunStats.BestTurn <= 40;
            case 14: return SettlementSystem.CityCount >= 1;
            case 15: return RunStats.Runs >= 5;
        }
        return false;
    }

    private static int RelicUnlockedCount()
    {
        var r = RelicManager.Instance;
        if (r == null || r.Catalog == null) return 0;
        int n = 0;
        for (int i = 0; i < r.Catalog.Count; i++) if (r.IsUnlocked(i)) n++;
        return n;
    }

    private static void CheckMementoUnlocks()
    {
        bool changed = false;
        for (int i = 0; i < mementos.Length; i++)
        {
            if (unlockedM.Contains(i)) continue;
            bool ok = false;
            try { ok = MementoCond(i); } catch { ok = false; }
            if (!ok) continue;
            unlockedM.Add(i); changed = true;
            Debug.Log($"🕯️『形見が解禁された』{mementos[i].jpName} ― {mementos[i].desc}（次の周でも持ち込めます）");
        }
        if (changed) SaveMementos();
    }

    public static bool TryEquip(int slotIndex, int mementoIndex)
    {
        EnsureInit();
        slotIndex = Mathf.Clamp(slotIndex, 0, MaxSlots - 1);
        if (slotIndex >= Slots) { Debug.LogWarning("⚠️ その枠はまだ開いていません（実績12個で3枠目）。"); return false; }
        if (mementoIndex >= 0 && !unlockedM.Contains(mementoIndex)) { Debug.LogWarning("⚠️ その形見はまだ解禁されていません。"); return false; }
        if (mementoIndex >= 0 && Equipped(mementoIndex)) { Debug.LogWarning("⚠️ 既に持っています。"); return false; }
        slot[slotIndex] = mementoIndex;
        SaveMementos();
        Debug.Log(mementoIndex < 0 ? $"🕯️『形見を外す』枠{slotIndex + 1}"
            : $"🕯️『形見』枠{slotIndex + 1} に {mementos[mementoIndex].jpName} ― {mementos[mementoIndex].desc}");
        return true;
    }

    // 形見の効果（各systemはここを見る）
    public static float KinNameCostMult => Equipped(0) ? 0.7f : 1f;
    public static float KillDpMult => Equipped(2) ? 1.12f : 1f;
    public static float FameMult => Equipped(3) ? 0.85f : 1f;
    public static float KinFieldPowerMult => Equipped(4) ? 1.1f : 1f;
    public static float MeritMult => Equipped(7) ? 1.5f : 1f;
    // ── F-25 で追加した形見の効果 ──
    public static float LureMult => Equipped(8) ? 0.85f : 1f;              // 🕯️ 静寂の遺灰：来訪を減らす
    public static float MaterialMult => Equipped(9) ? 1.2f : 1f;           // 🕯️ 坑夫の鶴嘴
    public static int KinExtraMp => Equipped(10) ? 1 : 0;                  // 🕯️ 測量士の羅針
    public static float ResearchCostMult => Equipped(11) ? 0.9f : 1f;      // 🕯️ 教条の写本
    public static float RivalGrowMult => Equipped(12) ? 0.88f : 1f;        // 🕯️ 暴君の玉座
    public static int StartTurnBonus => Equipped(13) ? 3 : 0;              // 🕯️ 時読みの砂
    public static float EmotionMult => Equipped(14) ? 1.15f : 1f;          // 🕯️ 宴の面

    /// <summary>ゲーム開始時に一度だけ入る形見（DP/RP/威名）。</summary>
    private static bool granted;
    public static void GrantStartingBonuses()
    {
        EnsureInit();
        if (granted) return;
        granted = true;
        var res = DungeonResourceManager.Instance;
        if (Equipped(1) && res != null) { res.AddDP(2500); Debug.Log("🕯️『初代の鍵』開始DP +2500"); }
        if (Equipped(5)) { ResearchState.AddRP(40); Debug.Log("🕯️『賢者の遺稿』開始RP +40"); }
        if (Equipped(6)) { DiplomacySystem.AddInfluence(80); Debug.Log("🕯️『商人の割符』開始威名 +80"); }
        if (Equipped(15) && res != null) { res.AddDP(1200); res.AddFame(300); Debug.Log("🕯️『先駆者の書付』開始DP +1200／名声 +300"); }
    }

    public static string HeaderLine()
    {
        EnsureInit();
        if (HasPending) return "<color=#ffd24a>📖 " + events[pending].title + " ― 選択を待っています</color>";
        var l = new List<string>();
        for (int s = 0; s < Slots; s++) if (slot[s] >= 0) l.Add("<color=" + Memento(slot[s]).colorHex + ">" + Memento(slot[s]).jpName + "</color>");
        return l.Count == 0 ? "" : "<color=#9c95b4>形見</color> " + string.Join(" ", l.ToArray());
    }
}
