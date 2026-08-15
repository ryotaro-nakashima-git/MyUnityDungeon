using UnityEngine;

/// <summary>
/// 📯 魔王の号令（Phase D-16）。**戦闘フェーズ中に打てる手**。
///
/// **なぜ要るか**：これまで『侵略開始』を押したあとにできることが実質何も無かった
/// （実測で14箇所が「準備フェーズのみ」で禁止）。3分間ただ眺めるだけでは、
/// どれだけ内政を作り込んでも**ゲームとしては薄い**。
///
/// 設計：
/// - **DPを払い、クールダウンで待つ**。連打できないので「いつ切るか」が判断になる。
/// - 効果は既存の仕組みに乗せる（回復・退却・ダメージ）＝新しい戦闘ルールを増やさない。
/// - ⚠ クールダウンは `Time.deltaTime` で進める。**倍速なら早く回復する**のが直感に合う
///   （UIの演出が unscaled なのとは逆。→ [[game-polish-plan]]）。
///
/// 純static・実行時保持。関連: [[DungeonTurnManager]] [[DemonLord]]。
/// </summary>
public static class CommandSystem
{
    public struct Def
    {
        public string jpName, desc;
        public int dp;
        public float cd;
        public string colorHex;
    }

    private static readonly Def[] defs =
    {
        D("治癒の号令", "全ての防衛体のHPを30%回復する。", 300, 45f, "#5cc47c"),
        D("落石",       "冒険者が最も密集している所に大きなダメージ。", 350, 35f, "#e08a3c"),
        D("魔王の一撃", "最も強い冒険者に魔力に応じた一撃を叩き込む。", 500, 70f, "#b0202b"),
        D("恐慌の波",   "侵入中の冒険者を**全員その場から帰らせる**（感情は清算される）。", 450, 60f, "#b48be6"),
    };
    private static Def D(string n, string d, int dp, float cd, string c)
        => new Def { jpName = n, desc = d, dp = dp, cd = cd, colorHex = c };

    /// <summary>🜲 5枠目＝**種族の権能**（[[LordAuthority]]）。中身は魔王の種族で変わる。</summary>
    public const int AuthorityIndex = 4;

    public static int Count { get { return defs.Length + 1; } }
    public static Def Get(int i)
    {
        if (i == AuthorityIndex) return LordAuthority.CurrentDef();
        return defs[Mathf.Clamp(i, 0, defs.Length - 1)];
    }

    private static float[] ready;    // 各号令の残りクールダウン（秒）
    private static void EnsureInit() { if (ready == null || ready.Length != Count) ready = new float[Count]; }
    public static void Reset() { ready = null; EnsureInit(); LordAuthority.Reset(); }

    public static float CooldownLeft(int i) { EnsureInit(); return ready[Mathf.Clamp(i, 0, Count - 1)]; }
    public static bool IsReady(int i) { return CooldownLeft(i) <= 0f; }

    /// <summary>戦闘中だけ進む。倍速なら早く回復する。</summary>
    public static void Tick(float dt)
    {
        EnsureInit();
        for (int i = 0; i < ready.Length; i++) if (ready[i] > 0f) ready[i] = Mathf.Max(0f, ready[i] - dt);
    }

    public static bool CanUse(int i, out string why)
    {
        EnsureInit();
        why = "";
        var turn = DungeonTurnManager.Instance;
        if (turn == null || !turn.IsBattlePhase) { why = "号令は戦闘中だけ"; return false; }
        // 🜲 人種のうちは権能を持たない＝進化する理由になる
        if (i == AuthorityIndex && !LordAuthority.Available) { why = "種族進化が必要"; return false; }
        if (!IsReady(i)) { why = "あと " + Mathf.CeilToInt(CooldownLeft(i)) + " 秒"; return false; }
        var res = DungeonResourceManager.Instance;
        // ⚠ ここの文言は号令カードの**幅134px・高さ14pxの1行**にそのまま出る。
        //   旧「DPが足りない（要350）」は長すぎて折り返し、**すぐ上の「350 DP」の行と重なって読めなかった**。
        //   必要額はカードに既に出ているので、ここでは理由だけを短く言う。
        if (res != null && res.DungeonPoints < Get(i).dp) { why = "DP不足"; return false; }
        return true;
    }

    public static bool TryUse(int i)
    {
        EnsureInit();
        string why;
        if (!CanUse(i, out why)) { Debug.LogWarning("⚠️ " + Get(i).jpName + "：" + why); return false; }
        var res = DungeonResourceManager.Instance;
        if (res != null && !res.TrySpendDP(Get(i).dp)) return false;
        ready[i] = Get(i).cd * MutationSystem.CommandCdMult;   // 🧬 世界の変異『静寂』で号令が重くなる
        SoundSystem.Play(SoundSystem.Sfx.Command);   // 🔊 号令の重み
        RunStats.NoteCommand();

        int magic = DemonLord.Instance != null ? DemonLord.Instance.GetStatRank((int)DemonLord.Stat.Magic) : 0;
        switch (i)
        {
            case 0: Rally(); break;
            case 1: Rockfall(70f * (magic + 1)); break;
            case 2: Smite(180f * (magic + 1)); break;
            case 3: Panic(); break;
            case AuthorityIndex: LordAuthority.Invoke(); break;   // 🜲 種族の権能
        }
        return true;
    }

    // ============ 効果 ============
    private static void Rally()
    {
        int n = 0;
        foreach (var z in Object.FindObjectsByType<ZombieAI>(FindObjectsInactive.Exclude))
        {
            if (z.CommandHeal(0.30f)) n++;
        }
        NotifySystem.Push("📯『治癒の号令』防衛体 " + n + " 体を癒やした", NotifySystem.Kind.Gain);
        Debug.Log("📯『治癒の号令』" + n + "体を回復");
    }

    private static void Rockfall(float dmg)
    {
        var advs = Object.FindObjectsByType<AdventurerAI>(FindObjectsInactive.Exclude);
        if (advs.Length == 0) { NotifySystem.Push("📯『落石』誰もいなかった", NotifySystem.Kind.Info); return; }
        // 一番人が集まっている所を中心にする
        Vector3 best = advs[0].transform.position; int bestN = -1;
        foreach (var a in advs)
        {
            int c = 0;
            foreach (var b in advs) if (Vector3.Distance(a.transform.position, b.transform.position) < 2.5f) c++;
            if (c > bestN) { bestN = c; best = a.transform.position; }
        }
        int hit = 0;
        foreach (var a in advs)
            if (Vector3.Distance(a.transform.position, best) < 2.5f) { a.TakeDamage(dmg); hit++; }
        FloatText.Spawn(best + new Vector3(0f, 0.9f, 0f), "落石！", new Color(1f, 0.62f, 0.24f), 3.4f, 1.1f, 1.1f);
        NotifySystem.Push("📯『落石』" + hit + " 人に " + Mathf.RoundToInt(dmg) + " ダメージ", NotifySystem.Kind.Gain);
    }

    private static void Smite(float dmg)
    {
        AdventurerAI target = null; float best = -1f;
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsInactive.Exclude))
            if (a.CombatPower > best) { best = a.CombatPower; target = a; }
        if (target == null) { NotifySystem.Push("📯『魔王の一撃』標的がいない", NotifySystem.Kind.Info); return; }
        FloatText.Spawn(target.transform.position + new Vector3(0f, 1.1f, 0f), "魔王の一撃", new Color(1f, 0.4f, 0.4f), 3.6f, 1.2f, 1.2f);
        target.TakeDamage(dmg);
        NotifySystem.Push("📯『魔王の一撃』Lv" + target.Level + " に " + Mathf.RoundToInt(dmg) + " ダメージ", NotifySystem.Kind.Gain);
    }

    private static void Panic()
    {
        int n = 0;
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsInactive.Exclude)) { a.ForceRetreat(); n++; }
        NotifySystem.Push("📯『恐慌の波』" + n + " 人が逃げ帰る（感情を清算）", NotifySystem.Kind.Gain);
        Debug.Log("📯『恐慌の波』" + n + "人を退却させた");
    }
}
