using UnityEngine;

/// <summary>
/// 🔥 <b>魔王の第二形態（バーサーカー）</b>。＝ <b>即死をやめて、そこから始まる時間をつくる</b>。
///
/// <para>
/// ⚠⚠ <b>なぜ要るか（実測）</b>：記録した<b>72波すべてで魔王HPは100%</b>のまま、そして次の波で 0 だった。
///   守りが崩れた瞬間にはもう味方が全滅していて、逆転も再起も起こりようがない。
///   ＝ <b>「少し破られた」という状態がゲームに存在しない</b>のが壁の形だった（→ [[wall-is-placement-cap]]）。
///   容疑者（装備水準／配置枠／頭数／道のり／脅威度／倒れたまま）は6つとも外れている。
/// </para>
///
/// <para>
/// <b>形</b>：魔王のHPを2ゲージにする。
/// <b>ゲージ1（殻）＝いまの <c>maxHP</c> そのまま</b>で、合計は増やさない。
/// ⚠ こうすると「これまで死んでいた瞬間」がそのまま突入点になり、<b>効いたかを実測と直接くらべられる</b>。
/// </para>
///
/// <para>
/// ⚠ <b>歯止め3つ</b>（救済が強すぎると負けようがなくなる）：
///   ① <b>逓減</b>（1回目100% → 2回目70% → 3回目40%…）。
///      ⚠⚠ 「n回で打ち切り」にしない ―― 打ち切りだとその回まで実質無敵で、次に唐突に死ぬ＝<b>また二値</b>。
///   ② <b>ゲージ1は波ごとに 25% しか戻らない</b>。代償であると同時に、
///      <b>「追い込まれている」が盤の外から見える唯一の警告</b>でもある。
///   ③ <b>持ち越しを分ける</b>（次の1波に 40%／恒久に 10%・上限つき・<b>加算のみ</b>）。
///      ⚠ 全部持ち越すと「わざとゲージ1を割る」が最適解になる。倍率にすると捕食ビルドが暴れる。
/// ⚠ <b>DP消費は代償にしない</b>（実測で<b>死ぬ瞬間まで DP を 1237〜3425 抱えている</b>）。
/// ⚠ <b>突入で脅威度を上げない</b>。脅威度は `LureEconomy.OnHeroEscaped` ＝<b>逃げ延びた者だけ</b>が
///   広める仕組みで、全滅させれば噂は出ない ―― 既存の因果がそのまま正しい。
/// </para>
///
/// 関連: [[DemonLord]] [[LordStance]]（既存の捕食は<b>味方を喰う</b>・別物） [[wall-is-placement-cap]]。
/// </summary>
public static class LordBerserk
{
    // ⚠⚠ readonly にしない。この作品のセーブは静的フィールドを丸ごと写す方式で、
    //   `readonly` は「カタログ＝保存しない」の合図として扱われる（→ [[save-sound-settings]]）。
    //   ここは全部**状態**なので、保存しないと「割れた殻がロードで満タンに戻る」。

    /// <summary>殻（ゲージ1）の残量比 0..1。</summary>
    private static float shell = 1f;
    /// <summary>これまでバーサーカーに入った回数（記録用）。⚠ 逓減には使わない（→ `fatigue`）。</summary>
    private static int entries;
    /// <summary>
    /// 😮‍💨 第二形態の「疲れ」（数理設計 P2・系4）。割れるたびに +1、入らなかった波ごとに抜ける。
    /// ⚠⚠ 旧式は入った回数で**一方的に**細らせていた（100→70→40→25→15%、二度と戻らない）。
    ///   4回入ると以後は割れるたびに15%で、そこで死ぬ＝**吸い込み状態**の片棒（P1 の実測）。
    /// </summary>
    private static float fatigue;
    /// <summary>殻の回復が止まっているあいだのターン数（重傷の代償）。</summary>
    private static int recoveryBlockTurns;
    /// <summary>いまバーサーカー中か。⚠ 波の中だけ真。</summary>
    private static bool active;
    /// <summary>次の1波にだけ乗る、喰らって得た力（0で無し）。</summary>
    private static float carryNextWave;
    /// <summary>恒久に積んだぶん（上限つき・加算のみ）。</summary>
    private static float permanent;

    // ── つまみ ──
    /// <summary>殻が1波で戻る割合。⚠ 4波無傷で満タン。</summary>
    // ══ 📏 D-1 の実測（2026-09-13・自動運転4周）══
    // ⚠⚠ **救済が強すぎる、ではなかった。救済になっていなかった。**
    //   4周のうち2周がまったく同じ形で終わった：
    //     殻 100% が続く → **1波で割れる** → 殻が **10% に貼り付いて3ターン連続で燃える** → T21で終わり。
    //   つまり第二形態は「3ターンの猶予」を足しただけで、**再起の道が無かった**
    //   ―― 直そうとした「T12まで無傷→1波で即死」が、一段ずらして再現していた。
    //   原因は2つとも**このファイルの数字**だった：
    //     ① `ShellRecoverWhenGrave` 0.10 ＝ 10%の殻は次の波で即割れる＝**実質ゼロ**
    //     ② `GraveBlockTurns` 2 ＝ その実質ゼロが**2ターン続く**
    //   → 凌いだ波のぶんだけ戻るようにした（下記）。⚠ ゲージ2は痩せ続ける（100/70/40/25/15%）ので、
    //     何度も燃えれば結局終わる＝「負けようがない」にはならない。

    public static float ShellRecoverPerWave => Balance.F("lord.shell.recover_per_wave", 0.34f);   // ⚠ P2 以降は使わない（記録用）
    /// <summary>
    /// 🩹 殻の回復＝ <c>r₀ + r₁ × その波で倒した割合</c>（数理設計 P2・系4・仕様 §5.2.2）。
    /// ⚠⚠ 旧式は二値だった（逃がしゼロ +34%／1人でも逃がせば +22%）。後半は逃走率5割なのでほぼ毎回 +22% になり、
    ///   22% の殻は次の波で必ず割れる ―― 殻 30% 以下から 50% 以上へ戻れたのは 37回中1回だった。
    ///   **どれだけ上手く守ったか**に比例して戻るようにする（全滅 +50%／半分 +30%／全員に逃げられても +10%）。
    /// </summary>
    public static float RecoverBase => Balance.F("lord.shell.recover_base", 0.10f);
    public static float RecoverByKill => Balance.F("lord.shell.recover_by_kill", 0.40f);
    /// <summary>第二形態に入らなかった波ごとに抜ける疲れ。</summary>
    public static float FatigueRecover => Balance.F("lord.berserk.fatigue_recover", 0.25f);
    /// <summary>次の波での殻の戻り（倒した割合 x のとき）。</summary>
    public static float RecoverFor(float killRatio) => (RecoverBase + RecoverByKill * Mathf.Clamp01(killRatio)) * FetterSystem.ShellRecoverMult;   // ⛓️ 薄殻の枷
    /// <summary>重傷（逃した者がいた）のとき、通常の回復が止まるターン数。</summary>
    public static int GraveBlockTurns => Balance.I("lord.shell.grave_block_turns", 1);
    /// <summary>
    /// ⚠⚠ <b>止まっているあいだでも、これだけは戻る。</b>実測で踏んだ穴：殻が 0% になると
    ///   毎波そこから第二形態に入り、毎波「逃した者がいる」＝重傷が再発して<b>永久に 0% のまま</b>になった
    ///   （T16〜T21 の6波すべて 0%）。＝ <b>吸い込み状態</b>で、「何度も追い込まれたら意味がない」に逆戻りする。
    ///   止まるのは<b>通常の回復（25%）</b>だけで、最低限は必ず戻す。
    /// </summary>
    public static float ShellRecoverWhenGrave => Balance.F("lord.shell.recover_when_grave", 0.22f);
    /// <summary>喰らった力のうち、次の1波に持ち越す割合。</summary>
    public static float CarryToNextWave => Balance.F("lord.devour.carry_next_wave", 0.40f);
    /// <summary>同・恒久に積む割合。</summary>
    public static float CarryPermanent => Balance.F("lord.devour.carry_permanent", 0.10f);
    /// <summary>恒久ぶんの上限（魔王の基礎HPに対する加算の上限）。⚠ 頭打ちにしないと積み上がる。</summary>
    public static float PermanentCap => Balance.F("lord.devour.permanent_cap", 900f);

    public static float Shell => shell;
    public static int Entries => entries;
    public static float Fatigue => fatigue;
    public static bool Active => active;
    public static bool RecoveryBlocked => false;   // ⚠ P2 で回復の停止をやめた（連続の回復に置き換え）
    public static int RecoveryBlockLeft => recoveryBlockTurns;

    /// <summary>
    /// 🔻 <b>逓減</b>。何度目のバーサーカーかで、ゲージ2の大きさが決まる。
    /// ⚠ 0 にはしない（0にすると突入した瞬間に死ぬ＝崖が戻る）。細く長く痩せさせる。
    /// </summary>
    public static float Phase2Ratio
    {
        get
        {
            // ⚠ 表は旧式と同じ値。**疲れ（実数）で線形に補間**する＝休めば戻る（P2・系4）。
            float f = Mathf.Max(0f, fatigue);
            int i = Mathf.FloorToInt(f);
            if (i >= Phase2Table.Length - 1) return Phase2Table[Phase2Table.Length - 1];
            return Mathf.Lerp(Phase2Table[i], Phase2Table[i + 1], f - i);
        }
    }
    private static readonly float[] Phase2Table = { 1.00f, 0.70f, 0.40f, 0.25f, 0.15f };

    /// <summary>次に入ったら何%の第二形態になるか（UIと『先触れ』に出す）。</summary>
    public static string NextPhaseText => Mathf.RoundToInt(Phase2Ratio * 100f) + "%";

    // ============ 力の持ち越し ============

    /// <summary>🍽️ 喰らった力を受け取る（`amount` は吸った強さ）。⚠ 呼ぶのはバーサーカー中だけ。</summary>
    public static void Devoured(float amount)
    {
        if (amount <= 0f) return;
        carryNextWave += amount * CarryToNextWave;
        permanent = Mathf.Min(PermanentCap, permanent + amount * CarryPermanent);
    }

    /// <summary>この波に乗る上乗せ（次の1波ぶん＋恒久ぶん）。⚠ <b>加算</b>で使うこと（倍率にしない）。</summary>
    public static float BonusThisWave => carryNextWave + permanent;
    public static float Permanent => permanent;
    public static float CarryPending => carryNextWave;

    // ============ 波の区切り ============

    /// <summary>波が始まる。⚠ 殻の残量から始める（満タンにしない）。</summary>
    public static void OnWaveBegin()
    {
        active = false;
    }

    /// <summary>
    /// 🔥 殻が割れた ―― 第二形態へ。返り値＝ゲージ2の大きさの比。
    /// ⚠ `entries` はここで進める（＝次に入るときはもっと細い）。
    /// </summary>
    public static float OnShellBroken()
    {
        float ratio = Phase2Ratio;
        shell = 0f;
        active = true;
        entries++;
        fatigue += 1f;
        return ratio;
    }

    /// <summary>
    /// 波を凌いだ。`wiped`＝逃した者が1人も居ないか。
    /// ⚠ <b>軽傷と重傷はここで分かれる</b>。噂（脅威度）は既存の仕組みに任せる ―― ここでは触らない。
    /// </summary>
    public static void OnWaveEnd(bool wiped, float killRatio)
    {
        bool enteredBerserk = active;
        active = false;
        // 🍽️ 次の1波ぶんは1回使ったら消える（貯め込ませない）
        carryNextWave = 0f;
        // 😮‍💨 第二形態に入らなかった波は、疲れが抜ける
        if (!enteredBerserk) fatigue = Mathf.Max(0f, fatigue - FatigueRecover);
        // 🩹 殻は「どれだけ上手く守ったか」に比例して戻る（二値の重傷をやめた・仕様 §5.2.2）
        recoveryBlockTurns = 0;
        shell = Mathf.Clamp01(shell + RecoverFor(killRatio));
    }

    /// <summary>🔄 新しい周のために畳む。</summary>
    public static void Reset()
    {
        shell = 1f; entries = 0; fatigue = 0f; recoveryBlockTurns = 0; active = false;
        carryNextWave = 0f; permanent = 0f;
    }

    /// <summary>
    /// 殻の残量を書き戻す（`DemonLord` がダメージを受けたときに呼ぶ）。
    ///
    /// ⚠⚠ <b>下がる方にしか動かさない。</b>実測で踏んだ穴：戦闘中の自然回復（研究『自然回復』・
    ///   種族スキル『再生』）が殻まで押し戻してしまい、<b>0% だった殻が次の波で 61% に戻っていた</b>。
    ///   それでは「削られたまま次に行く」という唯一の警告が消える。
    ///   癒しは<b>その波を戦うHP</b>を戻すが、<b>傷跡は残る</b> ―― 殻が戻るのは `OnWaveEnd` の 25% だけ。
    /// </summary>
    public static void NoteShell(float ratio01) { shell = Mathf.Min(shell, Mathf.Clamp01(ratio01)); }

    /// <summary>HUD・先触れ用の1行。</summary>
    public static string StatusLine()
    {
        if (active) return "第二形態（" + NextPhaseText + "）";
        string s = "殻 " + Mathf.RoundToInt(shell * 100f) + "%";
        if (shell < 1f) s += "・次の波で +" + Mathf.RoundToInt(RecoverFor(0f) * 100f) + "〜" + Mathf.RoundToInt(RecoverFor(1f) * 100f)
            + "%（倒した割合しだい）";
        if (fatigue > 0.01f) s += "　次の第二形態 " + NextPhaseText;
        return s;
    }
}
