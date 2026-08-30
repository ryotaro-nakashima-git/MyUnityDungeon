using UnityEngine;

/// <summary>
/// 🔥 **大招集**（D-2）。準備フェーズに**自分から**「今すぐ大きな波を呼ぶ」宣言をする。
///
/// <para>
/// ⚠⚠ **なぜ要るか（実測の裏付け）**：通しプレイ T1〜T30 で **逃走0のまま完封**できてしまった。
///   その結果、次の5つが**丸ごと眠った**：
///   脅威度／装備水準（1.00・0.0 のまま一度も動かない）・因縁（0人）・
///   B3F〜B5F（5層作って未到達）・囚牢（満杯のまま放置）・地上（放置しても負けない）。
///   根っこは「関所を1つ固めれば勝てる」＝**リスクを取る理由がどこにも無い**こと。
/// </para>
///
/// <para>
/// ⚠ 直し方として「逃がすと得」にする案もあるが、それは**受け身のリスク**で、
///   上手いプレイヤーほど避けられてしまう（実際、私は30ターン一度も逃がさなかった）。
///   → **能動のリスク**にする。「呼ばなければ安全、呼べば嵐」をプレイヤーの手に握らせる。
/// </para>
///
/// <para>
/// ⚠⚠ **常時倍率にしないこと。** これは押した**そのターンだけ**効く。
///   常時効く倍率は難易度カーブの軸を1本増やすことと同じで、
///   手当て済みの比（T10 0.80／T90 3.00）を崩す → [[difficulty-curve-orders]]。
/// </para>
///
/// 関連: [[playtest-t1-t30]]（この機能の根拠） [[WaveRoster]] [[EraSystem]]。
/// </summary>
public static class FeverSystem
{
    // ⚠ 状態＝セーブに載る（`readonly` にしない → [[SaveSystem]]）
    private static bool active;
    private static int calledTurn = -1;
    private static int killsAtCall;

    /// <summary>いま大招集を宣言しているか（そのターン限り）。</summary>
    public static bool Active { get { return active; } }
    public static int CalledTurn { get { return calledTurn; } }

    // ── ノブ（ここだけ触ればよい）──
    /// <summary>波の人数の倍率。⚠ 20体の上限も**この宣言のときだけ**外す。</summary>
    public const float WaveMult = 2.5f;
    /// <summary>撃破DPと素材の倍率。リスクに見合う「旨さ」。</summary>
    public const float LootMult = 2.0f;
    /// <summary>時代の進行の倍率。中盤の「ただ待つ区間」を自分で飛ばせるようにする。</summary>
    public const float EraMult = 2.0f;
    /// <summary>撃破**何体ごと**に研究点1（大招集の上乗せぶん）。⚠ RPが唯一の欠乏資源なので、ここが本命の報酬。</summary>
    public const int KillsPerRp = 6;
    /// <summary>
    /// 🔬 **大招集を切っていない波でも**、撃破何体ごとに研究点1入るか。
    ///
    /// <para>
    /// ⚠⚠ **これが無かったのが3周目の敗因の半分。** 研究点は大招集を切ったときだけ入る作りで、
    ///   安全に守るプレイでは戦闘から1点も入らなかった（撃破71・戦闘由来RP 0）。
    ///   しかも W-2 の「捌く用意」が大招集を止めたので、**止めた瞬間に経済も止まった**
    ///   ―― 無謀な死を貧しい死に置き換えただけだった（→ [[playthrough-run3-t14]]）。
    /// </para>
    ///
    /// <para>
    /// ⚠ 直すのに**倍率は使わない**。同じ「撃破数」という数え方を大招集の外に出しただけで、
    ///   `KillsPerRp` を 3→6 にしてこちらと同額にしてあるので、
    ///   **大招集を切った波の合計は前と変わらない**（6分の1 ＋ 6分の1 ＝ 3分の1）。
    ///   大招集は 2.5倍の人数を連れてくるので、そのぶんは自然に多く払われる。
    /// </para>
    /// </summary>
    public const int BaseKillsPerRp = 6;
    /// <summary>
    /// 呼んだあと、次に呼べるようになるまでの休み（ターン）。
    ///
    /// ⚠⚠ **これが無いと毎ターン呼ぶのが常に正解になる。** Phase F の実測（T8・3層・各階5体）：
    ///   殲滅 DP+937 ／ 大招集 DP+4,436（**4.7倍**）・素材3.7倍・RP+7・時代+5。
    ///   代償は魔王HPが 1.00→**0.42** まで削られたことだが、
    ///   `DemonLord.OnWaveDefended` は波の終わりに **HPを満タンに戻す**ので、
    ///   死ななければ**痛みが1ターンも残らない**。＝「勝てるうちは毎ターン押す」だけの手になる。
    ///
    /// ⚠ 直し方に**倍率は使わない**（軸が太る → [[difficulty-curve-orders]]）。
    ///   牢の尋問（1回/turn）や泳がせのRP上限と同じ **回数の制限**で効かせる。
    ///   ＝「いつ切るか」を選ばせる手に変える。
    /// </summary>
    public const int CooldownTurns = 2;

    /// <summary>次に呼べるターン（`calledTurn + 1 + CooldownTurns`）。まだ一度も呼んでいなければ 0。</summary>
    public static int ReadyTurn { get { return calledTurn < 0 ? 0 : calledTurn + 1 + CooldownTurns; } }

    public static void Reset() { active = false; calledTurn = -1; killsAtCall = 0; }

    /// <summary>ターンの頭で解除する。⚠ **そのターン限り**を守る唯一の場所。</summary>
    public static void OnTurnStart() { active = false; killsAtCall = 0; }

    public static float WaveCountMult { get { return active ? WaveMult : 1f; } }
    public static float KillLootMult { get { return active ? LootMult : 1f; } }

    /// <summary>大招集を宣言できるか。</summary>
    public static bool CanCall(out string why)
    {
        why = "";
        var turn = DungeonTurnManager.Instance;
        if (turn == null || !turn.IsPreparePhase) { why = "準備フェーズにだけ呼べる"; return false; }
        if (active) { why = "もう呼んである（取り消せない）"; return false; }
        var dl = DemonLord.Instance;
        if (dl == null || !dl.IsAlive) { why = "魔王がいない"; return false; }
        // 🕰️ 休み。⚠ 噂は撒いたそばから広がるものではない（毎ターン押せる手にしない）
        if (turn.CurrentTurn < ReadyTurn)
        { why = "噂がまだ届いていない（あと " + (ReadyTurn - turn.CurrentTurn) + " ターン）"; return false; }
        return true;
    }

    /// <summary>
    /// 🔥 宣言する。**名簿を引き直して人数を増やす**（先触れの表示もそのまま更新される）。
    /// ⚠ **取り消せない。** 取り消せると「見てから決める」ができてしまい、賭けにならない。
    /// </summary>
    public static bool TryCall(out string why)
    {
        if (!CanCall(out why)) return false;
        var turn = DungeonTurnManager.Instance;
        active = true;
        calledTurn = turn.CurrentTurn;
        killsAtCall = RunStats.Kills;
        WaveRoster.Roll(turn.CurrentTurn);   // ⚠ 倍率を効かせた人数で引き直す
        NotifySystem.Push("<b>大招集</b> ― 地上へ噂を撒いた。<b>" + WaveRoster.Count
            + " 体</b>が来る。倒すほど旨いが、抜かれれば終わりだ", NotifySystem.Kind.Danger);
        SoundSystem.Play(SoundSystem.Sfx.Wave);
        Debug.Log("🔥『大招集』宣言（T" + calledTurn + "）→ 名簿 " + WaveRoster.Count + " 体");
        return true;
    }

    /// <summary>
    /// 🔬 **どの波でも**入る研究点（大招集の有無に関わらず）。
    /// ⚠ `EndBattlePhase` から**`OnWaveEnd` より前に**1回だけ呼ぶ（決算に載せるため）。
    /// </summary>
    public static void PayBaseKillRp()
    {
        int killed = WaveReport.Killed;
        int rp = killed / Mathf.Max(1, BaseKillsPerRp);
        if (rp <= 0) return;
        ResearchState.AddRP(rp);
        Debug.Log("🔬『戦いの収穫』撃破" + killed + " → 研究点+" + rp);
    }

    /// <summary>
    /// 波が終わったときの見返り。⚠ `DungeonTurnManager.EndBattlePhase` から1回だけ呼ぶ。
    /// </summary>
    public static void OnWaveEnd()
    {
        if (!active) return;
        int killed = Mathf.Max(0, RunStats.Kills - killsAtCall);
        int rp = killed / Mathf.Max(1, KillsPerRp);
        if (rp > 0) ResearchState.AddRP(rp);
        int eraBonus = Mathf.RoundToInt(EraSystem.ProgressPerTurn * (EraMult - 1f));
        if (eraBonus > 0) EraSystem.AddProgress(eraBonus);
        NotifySystem.Push("<b>大招集を凌いだ</b> ― " + killed + " 体を退けた（研究点 +" + rp + "）", NotifySystem.Kind.Gain);
        Debug.Log("🔥『大招集』終了 撃破" + killed + " → RP+" + rp + " 時代+" + eraBonus);
        active = false;
    }

    /// <summary>ボタンに出す見込み（押す前に何が起きるか分かるように）。</summary>
    public static string Forecast()
    {
        int now = WaveRoster.Count;
        int after = Mathf.RoundToInt(now * WaveMult);
        return "およそ <b>" + now + " → " + after + " 体</b>／撃破の実り ×" + LootMult.ToString("0.0")
             + "／研究点が倍（" + KillsPerRp + "体につき+1 の上乗せ）／時代の進みも速くなる";
    }

    /// <summary>大招集を切ったときの見込み人数。</summary>
    public static int ForecastCount { get { return Mathf.RoundToInt(WaveRoster.Count * WaveMult); } }

    // ============ 🛡️ 捌く用意（W-2）============
    /// <summary>捌けそうかの三段階。</summary>
    public enum Ready3 { Fine = 0, Tight = 1, Risky = 2 }

    /// <summary>
    /// 🛡️ **「いまの守りで捌けるか」**。
    ///
    /// <para>
    /// ⚠⚠ **強さを式で予想しない。** 配下の攻撃力や罠のダメージを足し合わせた「防衛力」を作ると、
    ///   それは掛け算の軸を1本増やすのと同じで（→ [[difficulty-curve-orders]]）、しかも当たらない。
    ///   代わりに **プレイヤー自身の戦績**（`Held` ＝直近の波で実際に捌いた最大の人数）と
    ///   見込み人数を並べるだけにする。
    ///   予想ではなく事実なので外れようがなく、しかも
    ///   「あと何体ぶん厚くすればよいか」という**次の一手**にそのまま繋がる。
    /// </para>
    ///
    /// <para>
    /// ⚠ **これは禁止ではない。** 危なくても押せる（賭けを取り上げない）。見せるだけ。
    /// </para>
    /// 関連: [[RunStats]]（実績を積む場所） [[WaveReport]]（積むタイミング）。
    /// </summary>
    public static Ready3 ReadinessOf(int projected)
    {
        int held = Held;
        if (held <= 0) return Ready3.Risky;                                   // まだ一度も波を凌いでいない
        if (projected <= held) return Ready3.Fine;
        if (projected <= Mathf.RoundToInt(held * 1.5f)) return Ready3.Tight;
        return Ready3.Risky;
    }

    /// <summary>
    /// ⚠⚠ **物差しは「直近の波で実際に捌いた人数」**（W-4 の反省で差し替えた）。
    ///
    /// 前は `RunStats.BestWaveHeld`（＝**一人も通さず**凌いだ最大）を使っていたが、
    /// あれは**片道**だった ―― 逃走が常態になると二度と更新されず、3周目は
    /// **T4 の 7 から T14 まで一度も動かず**、大招集が14ターン一度も進言に出なかった。
    /// ＝「無謀な死」を「貧しい死」に置き換えただけ（→ [[playthrough-run3-t14]]）。
    ///
    /// ⚠ 新しい物差しは**完璧さを求めず**（逃走0を条件にしない）、**窓で見る**ので
    ///   守りが痩せれば下がる。上下**両方**に動くことが肝。
    /// </summary>
    public static int Held { get { return RunStats.HeldRecently; } }

    /// <summary>その判定を1行の言葉に。⚠ 色は3段階と必ず揃える（緑＝内側／橙＝はみ出す／赤＝危ない）。</summary>
    public static string ReadinessLine(int projected)
    {
        int held = Held;
        string thick = "";
        var fm = DungeonFeatureManager.Instance;
        if (fm != null)
        {
            int used, cap, nests;
            fm.TotalPlacement(out used, out cap, out nests);
            thick = "　<color=#9c95b4>守り " + used + "/" + cap + " 枠"
                  + (nests > 0 ? "・巣 " + nests : "") + "</color>";
        }
        string basis = "直近" + RunStats.RecentWindow + "波で捌いた最大は <b>" + held + " 体</b>";
        switch (ReadinessOf(projected))
        {
            case Ready3.Fine:
                return "<color=#5cc47c>捌ける見込み</color> ― " + basis + "。" + projected + " 体はその内側。" + thick;
            case Ready3.Tight:
                return "<color=#e3a94a>やや重い</color> ― " + basis + "。" + projected + " 体はそれを超える。" + thick;
            default:
                if (held <= 0)
                    return "<color=#e05a5a>まだ波を凌いだ実績が無い</color> ― 先に守りを厚くしたい。" + thick;
                return "<color=#e05a5a>いまの守りでは危ない</color> ― " + basis
                     + "。その約 <b>" + (projected / (float)held).ToString("0.0") + " 倍</b>が来る。" + thick;
        }
    }

    /// <summary>大招集を切った場合の「捌く用意」。</summary>
    public static string ReadinessLine() { return ReadinessLine(ForecastCount); }
}
