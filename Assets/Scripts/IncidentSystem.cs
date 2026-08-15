using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⚡ **迷宮の異変**（選択肢つきの事件）。
///
/// <para>
/// ⚠ 選択肢つきの事件は**既に2つある**。同じものを3つ目に作らないために、住み分けを決めてある：
/// </para>
/// <list type="bullet">
/// <item><b>[[NarrativeSystem]] 物語事件</b>＝**一度きり**。世界と方針の話。報酬は資源。</item>
/// <item><b>[[DiscoverySystem]] 発見</b>＝**地上を歩いた褒美**。未踏タイルでしか出ない。</item>
/// <item><b>[[ManaSurge]] 奔流</b>＝**選択の無い跳ね**。</item>
/// <item><b>ここ（異変）</b>＝**毎回選ぶ・そのターンの戦い方が変わる・自分の行動から生まれる**。</item>
/// </list>
///
/// <para>
/// **この事件の芯は「自分が積んだものが跳ね返ってくる」こと。**
/// 掘れば落盤が起き、奈落へ落とせば穴の底から声がし、罠を並べれば罠師の亡霊が来る。
/// だから条件は**プレイヤーの行動**から引く（→ C/A/B/D と繋がる）。
/// </para>
///
/// <para>
/// ⚠⚠ **常時効いているならそれは倍率であって、事件ではない**（→ [[difficulty-curve-orders]]）。
///   効果は必ず**そのターン限り**、発生は `Cooldown` ターンに1回まで。
///   そして**どの選択肢も一長一短**にする。片方が明らかに得なら、それは選択ではなく作業になる。
/// </para>
/// </summary>
public static class IncidentSystem
{
    /// <summary>事件と事件のあいだ。短くすると「毎ターン出るポップアップ」になって邪魔になる。</summary>
    public const int Cooldown = 4;

    /// <summary>効果の種類。⚠ 末尾にだけ足すこと（index はセーブに載る）。</summary>
    public enum Eff
    {
        None,
        MinionAtk, MinionHp,      // 配下（そのターンの防衛体に乗る）
        TrapPower, TrapFizzle,    // 罠の威力／不発になる数
        WaveNext,                 // 次の波の人数 ±
        ScoutDeeper,              // 先触れの読みが +1 段
        ExcavateOps,              // 工事の回数 +
        HeroSlow,                 // 冒険者の足が鈍る（%）
        Dp, Material, Emotion, Fame,
        LevelLoss,                // 配下を1体選んで -Lv
        Bench,                    // 配下を1体、このターン出せない
        FreeMinion,               // 配下を1体、無償で召喚
        SealCorridor,             // 通路が1本ふさがる（道のりが変わる）
    }

    public struct Choice
    {
        public string label, desc;
        public Eff e1; public float a1;
        public Eff e2; public float a2;   // ⚠ 2つ目は**代償**を書く場所。片方だけの選択肢は作らない
    }

    public struct Def
    {
        public string id, title, body;
        public Choice[] choices;
        public System.Func<bool> when;    // 出る条件（プレイヤーの行動から引く）
        public int weight;
    }

    private static Choice C(string label, string desc, Eff e1, float a1, Eff e2 = Eff.None, float a2 = 0f)
    { var c = new Choice(); c.label = label; c.desc = desc; c.e1 = e1; c.a1 = a1; c.e2 = e2; c.a2 = a2; return c; }

    private static Def D(string id, string title, string body, System.Func<bool> when, int weight, params Choice[] cs)
    { var d = new Def(); d.id = id; d.title = title; d.body = body; d.when = when; d.weight = weight; d.choices = cs; return d; }

    // ⚠ `readonly` ＝カタログ。セーブには載らない（→ [[save-sound-settings]]）。
    private static readonly Def[] defs =
    {
        D("i_leyline", "地脈の乱れ",
          "石壁の継ぎ目から、青い光が漏れている。魔素が渦を巻き、配下たちが落ち着かない。\n乗りこなせば力になる。呑まれれば、何かを失う。",
          null, 100,
          C("乗りこなす", "配下の攻撃 +25%。ただし渦に呑まれ、いちばん育った1体が <b>1レベル落ちる</b>。", Eff.MinionAtk, 0.25f, Eff.LevelLoss, 1f),
          C("鎮める", "DP 400 を注いで流れを整える。何も起きない。", Eff.Dp, -400f),
          C("放っておく", "渦が仕掛けを狂わせる。罠が3つ不発になる。", Eff.TrapFizzle, 3f)),

        D("i_spy", "ギルドの密偵",
          "冒険者ギルドの遣いが、荷運びに紛れて入口の様子を写している。\n泳がせれば向こうの手も読めるが、こちらの中身も知られる。",
          () => ResearchState.IsResearched("d_omen1"), 100,
          C("泳がせる", "この波の<b>先触れが1段深く読める</b>。ただし噂が広まり、次の波が 3体 増える。", Eff.ScoutDeeper, 1f, Eff.WaveNext, 3f),
          C("狩る", "配下を1体、追跡に出す（このターンは配置できない）。", Eff.Bench, 1f),
          C("偽の図面を掴ませる", "DP 300。誤った地図を持ち帰り、次の波が 3体 減る。", Eff.Dp, -300f, Eff.WaveNext, -3f)),

        D("i_cavein", "落盤",
          "掘ったばかりの坑道が鳴っている。土がぱらぱらと落ちてきた。\n迷宮を彫るということは、迷宮に傷をつけるということだ。",
          () => EurekaTracker.Count("excavate") > 0, 120,
          C("掘り直す", "このターンの工事の回数が 1 増える。", Eff.ExcavateOps, 1f),
          C("そのまま埋める", "通路が1本ふさがる。道のりが変わる。", Eff.SealCorridor, 1f),
          C("坑木で支える", "DP 350。崩れかけた岩が罠の重しになる（罠の威力 +30%）。", Eff.Dp, -350f, Eff.TrapPower, 0.30f)),

        D("i_abyss", "穴の底の声",
          "奈落へ落とした者が、まだ下で息をしている。呻きが石を伝ってくる。\n配下たちが、あなたの言葉を待っている。",
          () => EurekaTracker.Count("pit") > 0, 110,
          C("引き上げて喰らわせる", "恐怖が濃く採れる。感情 +60。", Eff.Emotion, 60f),
          C("放っておく", "呻きは数日続き、噂になる。名声 +40／DP +300。", Eff.Fame, 40f, Eff.Dp, 300f),
          C("止めを刺す", "静かになった。装備を剥いで 素材 +12。", Eff.Material, 12f)),

        D("i_quarrel", "配下の諍い",
          "気性の合わない二体が、通路の真ん中で睨み合っている。\n止めるか、焚きつけるか。",
          () => MinionRoster.All.Count >= 4, 100,
          C("罰する", "1体が 2レベル落ちる。代わりに他が引き締まり、配下の攻撃 +10%。", Eff.LevelLoss, 2f, Eff.MinionAtk, 0.10f),
          C("好きにさせる", "1体はこのターン出てこない。", Eff.Bench, 1f),
          C("焚きつける", "殺気が満ちる。配下の攻撃 +20%／HP -10%。", Eff.MinionAtk, 0.20f, Eff.MinionHp, -0.10f)),

        D("i_hunger", "迷宮の飢え",
          "壁が脈打っている。この穴は生き物で、あなたが与えるものを食べて育つ。\n今日は、ずいぶん腹を空かせているようだ。",
          () => DungeonResourceManager.Instance != null && DungeonResourceManager.Instance.DungeonPoints >= 900, 90,
          C("供物を捧げる", "DP 800。壁が厚みを増し、配下のHP +20%。", Eff.Dp, -800f, Eff.MinionHp, 0.20f),
          C("断つ", "飢えた牙が仕掛けに宿る。罠の威力 +45%／配下のHP -10%。", Eff.TrapPower, 0.45f, Eff.MinionHp, -0.10f)),

        D("i_envoy", "地上からの使者",
          "旗も持たない男が、ひとりで入口に立っている。\n「我らは貴方と争いたくない」――手には革袋。",
          null, 90,
          C("受け取る", "DP 600。だが金の出所が噂を呼び、次の波が 4体 増える。", Eff.Dp, 600f, Eff.WaveNext, 4f),
          C("追い返す", "何も起きない。名声が 30 下がる（世に知られるのが遅くなる）。", Eff.Fame, -30f),
          C("喰らう", "使者は帰らない。感情 +40／次の波が 2体 増える。", Eff.Emotion, 40f, Eff.WaveNext, 2f)),

        // ⚠ 条件は「魔王のHP」では引けない。階を組み直すたびに満タンに戻る（`DemonLord.PlaceAt`）ので、
        //   準備フェーズには必ず全快している。**前の波でどこまで攻め込まれたか**なら本当に残っている。
        D("i_nightmare", "玉座の夢",
          "目を閉じると、昨日の足音がまだ聞こえる。あそこまで来られた。\n備えを固めるか、怒りのまま前に出るか。",
          () => DungeonFloorManager.Instance != null && DungeonFloorManager.Instance.LastDeepestReached >= 1, 130,
          C("守りを固める", "配下のHP +25%／攻撃 -15%。", Eff.MinionHp, 0.25f, Eff.MinionAtk, -0.15f),
          C("怒りのまま迎える", "配下の攻撃 +20%／HP -12%。", Eff.MinionAtk, 0.20f, Eff.MinionHp, -0.12f),
          C("道を鈍らせる", "DP 300。床に油を敷く。冒険者の足 -25%。", Eff.Dp, -300f, Eff.HeroSlow, 0.25f)),

        D("i_beast", "迷い込んだ獣",
          "冒険者ではない。傷ついた獣が、奥の暗がりで丸くなっている。",
          null, 80,
          C("手懐ける", "配下が1体、無償で加わる。", Eff.FreeMinion, 1f),
          C("喰らわせる", "配下が生き血を啜る。感情 +35／配下の攻撃 +10%。", Eff.Emotion, 35f, Eff.MinionAtk, 0.10f),
          C("追い出す", "獣は地上へ逃げた。血の跡が道を教える（次の波が 2体 増える）。DP +250。", Eff.Dp, 250f, Eff.WaveNext, 2f)),

        D("i_trapper", "罠師の亡霊",
          "誰も置いていない場所に、真新しい仕掛けがひとつ。\n死んだはずの職人が、まだこの穴で働いている。",
          () => DungeonFeatureManager.Instance != null && DungeonFeatureManager.Instance.TrapsEverPlaced >= 8, 100,
          C("教えを請う", "罠の威力 +50%。ただし配下が怯え、攻撃 -10%。", Eff.TrapPower, 0.50f, Eff.MinionAtk, -0.10f),
          C("祓う", "DP 400。静かになった。", Eff.Dp, -400f),
          C("放っておく", "仕掛けが増えていく。罠の威力 +20%／罠が2つ勝手に暴発する。", Eff.TrapPower, 0.20f, Eff.TrapFizzle, 2f)),
    };

    // ============ 状態（⚠ static の値なのでセーブに載る） ============
    private static int cd = 2;                 // 最初の事件は少し早く来てよい
    private static int pendingIndex = -1;      // いま答えを待っている事件（-1＝無し）
    private static readonly List<string> seen = new List<string>();   // ⚠ readonly＝保存しない（同じ周で偏ってもよい）

    // そのターン限りの効果
    private static float minionAtk, minionHp, trapPower, heroSlow;
    private static int trapFizzle, scoutDeeper, excavateOps, benchedId = -1;
    // 次のターンに効くもの
    private static int waveNextDelta, waveThisDelta;
    // ⚠ レベル低下は**その場で**払う（`Apply` の中で `TakeLevels`）。選択肢の説明もそう書くこと。
    private static int pendingLevelLoss;

    public static bool HasPending { get { return pendingIndex >= 0; } }
    public static Def Pending { get { return defs[Mathf.Clamp(pendingIndex, 0, defs.Length - 1)]; } }

    public static float MinionAtkMult { get { return 1f + minionAtk; } }
    public static float MinionHpMult { get { return 1f + minionHp; } }
    public static float TrapPowerMult { get { return 1f + trapPower; } }
    public static float HeroSpeedMult { get { return Mathf.Max(0.4f, 1f - heroSlow); } }
    public static int ScoutDeeper { get { return scoutDeeper; } }
    public static int ExtraExcavateOps { get { return excavateOps; } }
    /// <summary>この波の人数の増減（前のターンに選んだ結果がここに来る）。</summary>
    public static int WaveDelta { get { return waveThisDelta; } }
    public static bool IsBenched(int individualId) { return benchedId >= 0 && benchedId == individualId; }

    public static void Reset()
    {
        cd = 2; pendingIndex = -1; seen.Clear();
        ClearTurnEffects(); waveNextDelta = 0; waveThisDelta = 0; pendingLevelLoss = 0;
    }

    private static void ClearTurnEffects()
    {
        minionAtk = 0f; minionHp = 0f; trapPower = 0f; heroSlow = 0f;
        trapFizzle = 0; scoutDeeper = 0; excavateOps = 0; benchedId = -1;
    }

    /// <summary>
    /// ターンの頭に呼ぶ。⚠ **効果はそのターン限り**なので、まず前ターンぶんを消す。
    /// ⚠ `WaveRoster.Roll` より**前**に呼ぶこと（人数の増減が名簿に乗る必要がある）。
    /// </summary>
    public static void TickTurn()
    {
        ClearTurnEffects();
        waveThisDelta = waveNextDelta; waveNextDelta = 0;   // 前ターンに選んだ「次の波」がこのターンぶん
        pendingIndex = -1;

        if (cd > 0) { cd--; return; }

        // 条件を満たすものから重みで1つ引く。同じものが続かないよう、既出は重みを1/3に。
        int total = 0;
        var pool = new List<int>();
        for (int i = 0; i < defs.Length; i++)
        {
            if (defs[i].when != null && !defs[i].when()) continue;
            pool.Add(i); total += Weight(i);
        }
        if (pool.Count == 0 || total <= 0) return;
        int pick = Random.Range(0, total);
        foreach (int i in pool) { pick -= Weight(i); if (pick < 0) { pendingIndex = i; break; } }
        if (pendingIndex < 0) return;

        cd = Cooldown;
        NotifySystem.Push("<b>異変</b>　" + defs[pendingIndex].title + " ― 選ばなければ先へ進めない", NotifySystem.Kind.Story);
        SoundSystem.Play(SoundSystem.Sfx.Story);
    }

    private static int Weight(int i) { return seen.Contains(defs[i].id) ? Mathf.Max(1, defs[i].weight / 3) : defs[i].weight; }

    /// <summary>選択肢を選ぶ。UIから呼ぶ。</summary>
    public static void Choose(int choiceIndex)
    {
        if (!HasPending) return;
        var d = defs[pendingIndex];
        var c = d.choices[Mathf.Clamp(choiceIndex, 0, d.choices.Length - 1)];
        if (!seen.Contains(d.id)) seen.Add(d.id);
        pendingIndex = -1;

        Apply(c.e1, c.a1);
        Apply(c.e2, c.a2);
        Debug.Log("⚡『異変』" + d.title + " → 「" + c.label + "」");
        NotifySystem.Push("<b>" + d.title + "</b>　―　" + c.label, NotifySystem.Kind.Gain);
        SoundSystem.Play(SoundSystem.Sfx.Confirm);
    }

    private static void Apply(Eff e, float a)
    {
        var res = DungeonResourceManager.Instance;
        switch (e)
        {
            case Eff.None: return;
            case Eff.MinionAtk: minionAtk += a; break;
            case Eff.MinionHp: minionHp += a; break;
            case Eff.TrapPower: trapPower += a; break;
            case Eff.TrapFizzle: trapFizzle += Mathf.RoundToInt(a); break;
            case Eff.WaveNext: waveNextDelta += Mathf.RoundToInt(a); break;
            case Eff.ScoutDeeper: scoutDeeper += Mathf.RoundToInt(a); break;
            case Eff.ExcavateOps: excavateOps += Mathf.RoundToInt(a); break;
            case Eff.HeroSlow: heroSlow += a; break;
            case Eff.Dp: if (res != null) { if (a >= 0) res.AddDP(Mathf.RoundToInt(a)); else res.TrySpendDP(Mathf.RoundToInt(-a)); } break;
            case Eff.Material: if (res != null) res.AddMaterial(Mathf.RoundToInt(a)); break;
            case Eff.Emotion:
                { var et = EmotionTreeManager.Instance; if (et != null) et.AddEmotion(EmotionTreeManager.Route.Despair, Mathf.RoundToInt(a)); }
                break;
            case Eff.Fame: if (res != null) res.AddFame(Mathf.RoundToInt(a)); break;
            case Eff.LevelLoss: pendingLevelLoss += Mathf.RoundToInt(a); TakeLevels(); break;
            case Eff.Bench: benchedId = PickVictim(); break;
            case Eff.FreeMinion: GrantFreeMinion(); break;
            case Eff.SealCorridor: SealOneCorridor(); break;
        }
    }

    // ============ 効果の実体 ============

    /// <summary>いちばんレベルの高い個体を選ぶ（痛くない罰は罰ではない）。</summary>
    private static int PickVictim()
    {
        int id = -1, best = -1;
        foreach (var v in MinionRoster.All) if (v.level > best) { best = v.level; id = v.id; }
        return id;
    }

    private static void TakeLevels()
    {
        if (pendingLevelLoss <= 0) return;
        int id = PickVictim(); var v = MinionRoster.Get(id);
        if (v == null) { pendingLevelLoss = 0; return; }
        int before = v.level;
        v.level = Mathf.Max(1, v.level - pendingLevelLoss);
        pendingLevelLoss = 0;
        NotifySystem.Push(MinionCatalog.Get(v.catalogIndex).jpName + " #" + id + " が <b>Lv" + before + " → Lv" + v.level + "</b> に落ちた", NotifySystem.Kind.Loss);
    }

    private static void GrantFreeMinion()
    {
        // いま解禁されている中からいちばん安い種を1体（強いものを無料で配らない）
        int pick = -1, cost = int.MaxValue;
        for (int i = 0; i < MinionCatalog.Count; i++)
        {
            if (!MinionEvolution.IsUnlocked(i)) continue;
            int c = MinionRoster.SummonCost(i);
            if (c < cost) { cost = c; pick = i; }
        }
        if (pick < 0) return;
        var v = MinionRoster.TrySummonFree(pick);
        if (v != null) NotifySystem.Push(MinionCatalog.Get(pick).jpName + " が加わった（気性『" + MinionTemperament.Name(v.temper) + "』）", NotifySystem.Kind.Gain);
    }

    /// <summary>
    /// 通路を1本ふさぐ。⚠ **道が切れない場所だけ**（切ると冒険者が階段に届かず波が終わらない）。
    /// ふさげる所が無ければ何も起きない＝それでよい（事件が盤を壊してはいけない）。
    /// </summary>
    private static void SealOneCorridor()
    {
        var g = Object.FindFirstObjectByType<DungeonGridSystem>();
        var fm = DungeonFeatureManager.Instance;
        if (g == null) return;
        int n = g.CurrentPlayableSize;
        var cands = new List<List<Vector2Int>>();
        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            {
                var c = new Vector2Int(x, y);
                if (g.GetTileType(x, y) == DungeonGridSystem.TileType.None) continue;
                var seg = Excavation.SegmentAt(c);
                if (seg.Count == 0 || seg.Count > 4) continue;
                bool bad = false;
                foreach (var s in seg)
                    if (s == g.EntranceCell || s == g.BossCell || s == g.DemonLordCell || (fm != null && fm.HasFeatureAt(s))) { bad = true; break; }
                if (bad) continue;
                if (Excavation.PathLengthWith(new HashSet<Vector2Int>(seg), null) < 0) continue;
                cands.Add(seg);
            }
        if (cands.Count == 0) { NotifySystem.Push("崩れかけたが、幸い道は塞がらなかった", NotifySystem.Kind.Info); return; }
        var use = cands[Random.Range(0, cands.Count)];
        int before = Excavation.PathLength();
        foreach (var s in use) g.StampTile(s.x, s.y, DungeonGridSystem.TileType.None);
        var fmgr = DungeonFloorManager.Instance;
        if (fmgr != null) fmgr.WriteBackCurrentMap();   // ⚠ 書き戻さないと階を切り替えた瞬間に戻る
        NotifySystem.Push("通路が " + use.Count + " マスふさがった　道のり <b>" + before + " → " + Excavation.PathLength() + "</b>", NotifySystem.Kind.Danger);
    }

    /// <summary>
    /// 🪤 罠の不発。戦闘の頭に呼ぶ（→ `DungeonTurnManager.StartBattlePhase`）。
    /// ⚠ 盤に罠タイルが敷かれているのは**戦闘フェーズに入ってから**なので、ここでしか止められない。
    /// </summary>
    public static void ApplyTrapFizzleOnBattleStart()
    {
        if (trapFizzle <= 0) return;
        var g = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (g == null) return;
        // ⚠⚠ `FindObjectsByType<RoomData>` で拾ってはいけない。直前の `ImportFeatures` が
        //   タイルを敷き直しており、**古いタイルは破棄予約されているだけでまだ場に居る**。
        //   拾うと死にかけのオブジェクトを止めてしまい、実測で 3基のはずが 2基しか止まらなかった。
        //   盤に今出ているものは `GetGridObject` で引く（マスごとに1つだけ返る）。
        var list = new List<RoomData>();
        int size = g.CurrentPlayableSize;
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                if (g.GetTileType(x, y) != DungeonGridSystem.TileType.Trap) continue;
                var go = g.GetGridObject(x, y); if (go == null) continue;
                var rd = go.GetComponent<RoomData>();
                if (rd != null && rd.roomType == RoomData.RoomType.Trap) list.Add(rd);
            }
        int n = Mathf.Min(trapFizzle, list.Count);
        for (int i = 0; i < n; i++)
        {
            int k = Random.Range(i, list.Count);
            var t = list[k]; list[k] = list[i]; list[i] = t;
            list[i].DisableTrapTemporarily(9999f);
        }
        if (n > 0) Debug.Log("⚡『異変』罠 " + n + " 基が不発になった");
    }

    /// <summary>報告に出す1行（いま何が効いているか）。空なら何も効いていない。</summary>
    public static string ActiveLabel()
    {
        var s = "";
        if (minionAtk != 0f) s += "配下の攻撃 " + Pct(minionAtk) + "　";
        if (minionHp != 0f) s += "配下のHP " + Pct(minionHp) + "　";
        if (trapPower != 0f) s += "罠の威力 " + Pct(trapPower) + "　";
        if (trapFizzle > 0) s += "罠 " + trapFizzle + "基が不発　";
        if (scoutDeeper > 0) s += "先触れ +" + scoutDeeper + "段　";
        if (excavateOps > 0) s += "工事 +" + excavateOps + "回　";
        if (benchedId >= 0) s += "1体が出られない　";
        if (waveThisDelta != 0) s += "この波 " + (waveThisDelta > 0 ? "+" : "") + waveThisDelta + "体　";
        return s;
    }
    private static string Pct(float v) { return (v > 0 ? "+" : "") + Mathf.RoundToInt(v * 100f) + "%"; }
}
