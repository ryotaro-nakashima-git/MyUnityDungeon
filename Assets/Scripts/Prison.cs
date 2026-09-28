using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⛓️ **囚牢 ― 捕虜と処遇**。
///
/// <para>
/// **なぜ要るか（実測）**：T5時点で <b>DP +1,200/turn : RP +3/turn（400倍）</b>。
/// DPには捨て場所が無く、配下を28体召喚して22体を死蔵した。研究ノードは51本あるのに進まない。
/// ここに「DP→RPの交換レート」を1本足せば数字の穴は塞がるが、それは**付け替えであって遊びではない**。
/// → <b>倒し方そのものを経済にする</b>。殺せばDP、生かして捕らえれば、尋問でRPが出る。
/// </para>
///
/// <para>
/// ⚠⚠ **捕らえると撃破DPも素材も入らない。** ここが天秤の支点。
///   「今日はDPが要るから殺す」「あの魔術師は知識を持っているから捕らえる」を毎波選ばせる。
/// </para>
///
/// <para>
/// 処遇は4つ。**どれもDPを食う**＝これが本命のDPシンクでもある。
/// <list type="bullet">
/// <item><b>尋問</b> … RPを得る。気力を削るので何度も出来ない。ゼロにすると牢で死ぬ。</item>
/// <item><b>調伏</b> … 反抗心が折れた者を<b>転向者</b>として配下に加える（→ [[UniqueCatalog]] の weight 0 の4種）。</item>
/// <item><b>供物</b> … 魔王に喰わせる。捕食値＋大量のDP。</item>
/// <item><b>解放</b> … 身代金は入るが、<b>必ず恨みを持って戻る</b>（→ [[Nemesis]]）。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠ 維持費を払えないと**勝手に脱走する**。脱走した者は名を得て強くなって戻る。
///   「捕らえたまま放置」を無料にしないための歯止め。
/// </para>
/// 関連: [[Nemesis]] [[AdventurerAI]] [[Research]]。
/// </summary>
public static class Prison
{
    /// <summary>捕虜1人。⚠ セーブに丸ごと載る（`readonly` にしないこと → [[SaveSystem]]）。</summary>
    public class Captive
    {
        public int id;
        public int nemesisId;                // 0 = 無名の冒険者
        public string name = "";
        public AdventurerAI.Job job;
        public int rank, level;
        public bool hasSpell;
        public MagicCatalog.Spell spell;

        public int vigor = 100;              // 気力。尋問で減り、毎ターン少し戻る。0で衰弱死
        public int defiance = 100;           // 反抗心。0まで折れると調伏できる
        public int interrogated;             // 尋問した回数（表示用）
        public int capturedTurn;
    }

    // ⚠ readonly にしない（セーブの対象＝状態）
    private static List<Captive> all = new List<Captive>();
    private static int nextId = 1;
    /// <summary>生け捕り方針。⚠ **既定はOFF**（解禁した瞬間に撃破DPが消えると事故になる）。</summary>
    private static bool takeAlive = false;
    /// <summary>このターンに尋問した回数。⚠ セーブ対象（ターン内の状態）。</summary>
    private static int interrogatedThisTurn = 0;

    public static IReadOnlyList<Captive> All { get { return all; } }
    public static int Count { get { return all.Count; } }
    public static bool TakeAlive { get { return takeAlive; } }

    public static void Reset() { all = new List<Captive>(); nextId = 1; takeAlive = false; interrogatedThisTurn = 0; }

    /// <summary>
    /// ⚠⚠ **1ターンに尋問できる人数の上限**。これが研究点の蛇口の太さそのもの。
    ///
    /// 実測で最初これが無く、枠7 × 1人25RP を毎ターン回せてしまい **+175RP/周期**（基礎は+3/turn）だった。
    /// 収容枠を増やす研究が「同時に絞れる人数」まで増やしてしまうのが原因。
    /// → **枠は『誰を残すか』の幅**、**尋問回数は『今日は誰から聞くか』の選択**、と役割を分ける。
    /// </summary>
    public static int InterrogationsPerTurn { get { return ResearchState.IsResearched("d_capture3") ? 2 : 1; } }
    public static int InterrogationsLeft { get { return Mathf.Max(0, InterrogationsPerTurn - interrogatedThisTurn); } }

    // ============ 🔓 解禁と枠 ============

    public static bool Unlocked { get { return ResearchState.IsResearched("d_capture"); } }

    /// <summary>収容できる数。⚠ ここが遊びの幅そのもの（枠の奪い合いが処遇を選ばせる）。</summary>
    public static int Capacity
    {
        get
        {
            if (!Unlocked) return 0;
            int c = 2;
            if (ResearchState.IsResearched("d_capture2")) c += 2;
            if (ResearchState.IsResearched("d_capture3")) c += 3;
            if (ResearchState.IsResearched("h_lust")) c += 2;   // 👑 色欲の刻印：留める道
            return c;
        }
    }
    public static bool HasRoom { get { return Unlocked && all.Count < Capacity; } }

    public static void SetTakeAlive(bool on)
    {
        if (!Unlocked) { takeAlive = false; return; }
        takeAlive = on;
        NotifySystem.Push(on ? "方針を<b>生け捕り</b>にした（撃破DPと素材は入らなくなる）"
                             : "方針を<b>殲滅</b>に戻した", NotifySystem.Kind.Info);
    }

    /// <summary>⚠ バーの満量。UI がこれを見る（別々に持つと表示と実体がずれる）。</summary>
    public const int VigorMax = 100;
    public const int DefianceMax = 110;

    // ============ ⛓️ 捕らえる ============

    /// <summary>
    /// HPが尽きた冒険者を捕らえられるか。⚠ **`AdventurerAI.TakeDamage` の死亡分岐の頭で呼ぶ**。
    /// true を返したら、呼ぶ側は**撃破報酬を一切出さずに**退場させること。
    /// </summary>
    public static bool TryCapture(int nemesisId, AdventurerAI.Job job, int rank, int level,
        bool hasSpell, MagicCatalog.Spell spell)
    {
        if (!Unlocked || !takeAlive || !HasRoom) return false;

        var c = new Captive();
        c.id = nextId++;
        c.nemesisId = nemesisId;
        c.job = job; c.rank = rank; c.level = level;
        c.hasSpell = hasSpell; c.spell = spell;
        c.capturedTurn = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 1;
        c.vigor = 100;
        // 🧱 反抗心：格が高いほど折れにくい。名のある者はさらに固い（因縁は簡単には裏返らない）
        // ⚠ 実測で最初 `55 + rank*7 + level/2 + 25` にしたら**折れるまで24ターン**かかり、
        //   胎動（18ターン）より長くて調伏が机上の存在になった。折れるのは6〜10ターンを狙う。
        c.defiance = Mathf.Clamp(32 + rank * 6 + level / 3 + (nemesisId > 0 ? 20 : 0), 25, DefianceMax);

        var nh = Nemesis.Get(nemesisId);
        c.name = nh != null ? Nemesis.DisplayName(nh)
                            : AdventurerAI.RankLetter(rank) + "級の" + WaveRoster.JobName(job);
        all.Add(c);

        if (nemesisId > 0) Nemesis.OnCaptured(nemesisId);
        // 🕸️ 生きたまま消えた者は噂を運ばない（脅威度が少し下がる）
        LureEconomy.CalmDown(0.02f);
        RunStats.NoteCaptured();
        WaveReport.NoteCaptured();   // 📜 波の決算

        NotifySystem.Push("<b>" + c.name + "</b> を生け捕りにした（牢 " + all.Count + "/" + Capacity + "）", NotifySystem.Kind.Gain);
        SoundSystem.Play(SoundSystem.Sfx.Discover);
        Debug.Log("⛓️『捕縛』" + c.name + " Lv" + level + " を捕らえた（撃破DPは入らない）");
        return true;
    }

    public static Captive Get(int id)
    {
        for (int i = 0; i < all.Count; i++) if (all[i].id == id) return all[i];
        return null;
    }

    // ============ 💰 毎ターンの維持 ============

    public static int UpkeepPerCaptive(Captive c) { return 18 + c.rank * 8 + Mathf.RoundToInt(c.level * 1.5f); }
    public static int UpkeepTotal
    {
        get { int n = 0; for (int i = 0; i < all.Count; i++) n += UpkeepPerCaptive(all[i]); return n; }
    }

    /// <summary>
    /// ターンの締めに呼ぶ（→ `DungeonTurnManager.EndSurfacePhase`）。
    /// 維持費 → 気力の回復 → 反抗心の摩耗、の順。⚠ 払えなければ**反抗心の強い者から脱走**する。
    /// </summary>
    public static void TickTurn(int turn)
    {
        interrogatedThisTurn = 0;              // ⚠ 捕虜が0でも戻す（下の早期returnより前に置くこと）
        if (all.Count == 0) return;
        var res = DungeonResourceManager.Instance;

        int need = UpkeepTotal;
        bool paid = res != null && res.TrySpendDP(need);
        if (!paid && res != null)
        {
            // 一番手強い者が最初に破る（弱い者から逃げると「強い捕虜ほど安全」になって逆になる）
            Captive worst = null;
            for (int i = 0; i < all.Count; i++) if (worst == null || all[i].defiance > worst.defiance) worst = all[i];
            if (worst != null)
            {
                NotifySystem.Push("牢の維持費 " + need + "DP を払えず、<b>" + worst.name + "</b> に破られた", NotifySystem.Kind.Loss);
                SoundSystem.Play(SoundSystem.Sfx.Loss);
                Escape(worst, turn);
            }
        }

        for (int i = 0; i < all.Count; i++)
        {
            var c = all[i];
            c.vigor = Mathf.Min(VigorMax, c.vigor + 10); // 気力は戻る＝尋問は「間を空ければまた出来る」
            c.defiance = Mathf.Max(0, c.defiance - DefianceDecayPerTurn);
        }
    }

    /// <summary>
    /// ⏳ この捕虜が膝を折るまでの毎ターンの減りぶん（→ [[Foretell]] が残りターンを出すのに使う）。
    /// ⚠ いまは全員同じだが、個体差を入れるならここが窓口になる（呼ぶ側は捕虜を渡している）。
    /// </summary>
    public static int DefianceDecayOf(Captive c) { return DefianceDecayPerTurn; }

    /// <summary>毎ターン折れる量。⚠ 魔王の威圧が強いほど速い（魔王のステが牢に効く）。</summary>
    private static int DefianceDecayPerTurn
    {
        get
        {
            int mightRank = DemonLord.Instance != null
                ? DemonLord.Instance.GetStatRank((int)DemonLord.Stat.Body) : 0;
            return 8 + Mathf.Max(0, mightRank) * 2;
        }
    }

    /// <summary>脱走。⚠ **必ず名を得て戻る**（放置のコストを「金」ではなく「敵」で払わせる）。</summary>
    private static void Escape(Captive c, int turn)
    {
        int nid = c.nemesisId;
        if (nid > 0) Nemesis.OnReleased(nid, turn);
        else nid = Nemesis.Birth(c.job, c.rank, c.level, c.hasSpell, c.spell, turn, false, 1, 3);
        LureEconomy.OnHeroEscaped(c.level);
        all.Remove(c);
        Debug.Log("⛓️『脱走』" + c.name + " が牢を破った");
    }

    // ============ 🗣️ 処遇①：尋問（RP） ============

    public static int InterrogateDpCost(Captive c) { return 90 + c.level * 6; }
    /// <summary>⚠ 気力100・回復10/ターンに対して45 ＝ **1人からは3回まで**。ここが実りの総量を決めている。</summary>
    public static int InterrogateVigorCost { get { return 45; } }

    /// <summary>
    /// 尋問で得られるRP。⚠ **名のある者は倍**（何度も潜った者ほど迷宮の外の事情を知っている）。
    /// ここが DP→RP の唯一の橋。レートではなく**気力と枠に律速される**のが肝。
    /// </summary>
    /// <summary>
    /// ⚠⚠ **この尋問で事切れるか。** 気力が足りているうちは何度でも聞けるが、最後の1回は殺す。
    /// 実測で最初これを黙って起こしていた（4回目で死ぬのに、ボタンの見た目は3回目までと同じ）。
    /// **押す前に分かること**にして、そのぶん実りを倍にする＝事故ではなく<b>選択</b>にする。
    /// </summary>
    public static bool WillBreak(Captive c) { return c != null && c.vigor - InterrogateVigorCost <= 0; }

    public static int InterrogateRpGain(Captive c)
    {
        // ⚠ 実測で最初 `2 + rank/2 + level/9` に「名あり×2・搾る×2」を重ねたら、
        //   **捕虜1人が5ターンで60RP**（基礎+3/turnの20ターンぶん）になり研究が崩壊した。
        //   倍率を重ねない。狙いは 無名で10〜14RP／名ありで18〜24RP（＝1人＝ノード1〜2本）。
        float rp = 2 + c.rank * 0.5f + c.level / 12f;
        if (c.hasSpell) rp += 1f;                          // 術者は理屈を知っている
        if (c.nemesisId > 0) rp *= 1.4f;                   // 何度も潜った者は迷宮の外の事情まで知っている
        if (WillBreak(c)) rp *= 1.5f;                      // 搾り切るぶんだけ実る（そして失う）
        return Mathf.Max(1, Mathf.RoundToInt(rp));
    }

    public static bool CanInterrogate(Captive c, out string why)
    {
        why = "";
        if (c == null) { why = "その捕虜はもういない"; return false; }
        if (InterrogationsLeft <= 0) { why = "このターンはもう尋問できない（次のターンに1人）"; return false; }
        if (c.vigor <= 0) { why = "気力が尽きている（数ターン休ませる）"; return false; }
        var res = DungeonResourceManager.Instance;
        if (res != null && res.DungeonPoints < InterrogateDpCost(c)) { why = "DPが足りない"; return false; }
        return true;
    }

    public static bool TryInterrogate(int id, out string why)
    {
        var c = Get(id);
        if (!CanInterrogate(c, out why)) return false;
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(InterrogateDpCost(c))) { why = "DPが足りない"; return false; }

        int rp = InterrogateRpGain(c);
        ResearchState.AddRP(rp);
        interrogatedThisTurn++;
        c.vigor = Mathf.Max(0, c.vigor - InterrogateVigorCost);
        c.defiance = Mathf.Max(0, c.defiance - 12);        // 尋問は折るのも早める
        c.interrogated++;
        EurekaTracker.OnInterrogate();

        NotifySystem.Push("<b>" + c.name + "</b> を尋問した（+" + rp + " RP）", NotifySystem.Kind.Gain);
        SoundSystem.Play(SoundSystem.Sfx.Confirm);
        Debug.Log("⛓️『尋問』" + c.name + " → +" + rp + "RP（気力 " + c.vigor + "／反抗 " + c.defiance + "）");

        if (c.vigor <= 0)
        {
            NotifySystem.Push("<b>" + c.name + "</b> は口を割ったまま息絶えた", NotifySystem.Kind.Loss);
            if (c.nemesisId > 0) Nemesis.OnPerished(c.nemesisId);
            all.Remove(c);
        }
        return true;
    }

    // ============ 🙇 処遇②：調伏（転向者にする） ============

    public static int ConvertDpCost(Captive c) { return 700 + c.level * 45 + c.rank * 60; }
    public static int ConvertMaterialCost(Captive c) { return 6 + c.rank * 2; }

    public static bool CanConvert(Captive c, out string why)
    {
        why = "";
        if (c == null) { why = "その捕虜はもういない"; return false; }
        if (c.defiance > 0) { why = "まだ折れていない（反抗心 " + c.defiance + "）"; return false; }
        var res = DungeonResourceManager.Instance;
        if (res != null && res.DungeonPoints < ConvertDpCost(c)) { why = "DPが足りない"; return false; }
        if (res != null && res.CraftMaterials < ConvertMaterialCost(c)) { why = "素材が足りない"; return false; }
        return true;
    }

    public static bool TryConvert(int id, out string why)
    {
        var c = Get(id);
        if (!CanConvert(c, out why)) return false;
        var res = DungeonResourceManager.Instance;
        if (res != null)
        {
            if (!res.TrySpendDP(ConvertDpCost(c))) { why = "DPが足りない"; return false; }
            if (!res.TrySpendMaterial(ConvertMaterialCost(c))) { why = "素材が足りない"; return false; }
        }

        int local = UniqueCatalog.TurncoatLocalFor(c.job);
        var ind = MinionRoster.GrantTurncoat(local, c.level);
        if (c.nemesisId > 0) Nemesis.OnTurned(c.nemesisId);
        RunStats.NoteConverted();
        all.Remove(c);

        NotifySystem.Push("<b>" + c.name + "</b> が膝を折った ― <b>"
            + UniqueCatalog.Get(local).jpName + "</b> として迎え入れた", NotifySystem.Kind.Story);
        SoundSystem.Play(SoundSystem.Sfx.Discover);
        Debug.Log("⛓️『調伏』" + c.name + " → " + UniqueCatalog.Get(local).jpName + " 個体#" + (ind != null ? ind.id : 0));
        return true;
    }

    // ============ 🩸 処遇③：供物（魔王に喰わせる） ============

    public static int DevourDpGain(Captive c) { return 240 + c.level * 18 + c.rank * 40; }

    public static bool CanDevour(Captive c, out string why)
    {
        why = "";
        if (c == null) { why = "その捕虜はもういない"; return false; }
        var dl = DemonLord.Instance;
        if (dl == null || !dl.IsAlive) { why = "魔王がいない"; return false; }
        return true;
    }

    public static bool TryDevour(int id, out string why)
    {
        var c = Get(id);
        if (!CanDevour(c, out why)) return false;

        int dp = DevourDpGain(c);
        var res = DungeonResourceManager.Instance;
        if (res != null) res.AddDP(dp);
        LordStance.OnSoulReaped(c.level + c.rank * 2);
        // ⚠ 生きた人間を喰らうのは伝わる。野に在る者の恨みが深まる（＝次が強くなる）
        Nemesis.DeepenGrudgeAll(1);
        if (c.nemesisId > 0) Nemesis.OnPerished(c.nemesisId);
        all.Remove(c);

        NotifySystem.Push("<b>" + c.name + "</b> を供物にした（+" + dp + "DP／捕食値）", NotifySystem.Kind.Gain);
        SoundSystem.Play(SoundSystem.Sfx.Kill);
        Debug.Log("⛓️『供物』" + c.name + " を喰わせた（+" + dp + "DP）");
        return true;
    }

    // ============ 🕊️ 処遇④：解放（身代金／必ず戻る） ============

    public static int RansomDpGain(Captive c) { return 380 + c.level * 26 + c.rank * 70; }

    public static bool TryRelease(int id, out string why)
    {
        why = "";
        var c = Get(id);
        if (c == null) { why = "その捕虜はもういない"; return false; }
        int turn = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 1;

        int dp = RansomDpGain(c);
        var res = DungeonResourceManager.Instance;
        if (res != null) res.AddDP(dp);

        int nid = c.nemesisId;
        if (nid > 0) Nemesis.OnReleased(nid, turn);
        else nid = Nemesis.Birth(c.job, c.rank, c.level, c.hasSpell, c.spell, turn, false, 0, 3);
        LureEconomy.OnHeroEscaped(c.level);   // 帰った者は必ず喋る
        all.Remove(c);

        NotifySystem.Push("<b>" + c.name + "</b> を解き放った（身代金 +" + dp + "DP）― <b>必ず戻ってくる</b>", NotifySystem.Kind.Story);
        SoundSystem.Play(SoundSystem.Sfx.Story);
        Debug.Log("⛓️『解放』" + c.name + " → 身代金+" + dp + "DP／恨みを持って戻る");
        return true;
    }

    // ============ 表示 ============

    public static string JobLabel(Captive c)
    {
        return AdventurerAI.RankLetter(c.rank) + "級 " + WaveRoster.JobName(c.job) + " Lv" + c.level
             + (c.hasSpell ? "／" + c.spell.jpName : "");
    }

    /// <summary>牢の要約（HUDと腹心の報告用）。</summary>
    public static string Summary
    {
        get
        {
            if (!Unlocked) return "";
            if (all.Count == 0) return "牢は空（" + Capacity + "枠）";
            return "捕虜 " + all.Count + "/" + Capacity + "　維持 " + UpkeepTotal + "DP/ターン";
        }
    }
}
