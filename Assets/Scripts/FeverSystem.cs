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
    /// <summary>撃破**何体ごと**に研究点1。⚠ RPが唯一の欠乏資源なので、ここが本命の報酬。</summary>
    public const int KillsPerRp = 3;

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
             + "／研究点 " + KillsPerRp + "体につき+1／時代の進みも速くなる";
    }
}
