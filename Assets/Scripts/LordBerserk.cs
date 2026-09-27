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
    /// <summary>これまでバーサーカーに入った回数（逓減に使う）。</summary>
    private static int entries;
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

    public static float ShellRecoverPerWave => Balance.F("lord.shell.recover_per_wave", 0.34f);
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
    public static bool Active => active;
    public static bool RecoveryBlocked => recoveryBlockTurns > 0;
    public static int RecoveryBlockLeft => recoveryBlockTurns;

    /// <summary>
    /// 🔻 <b>逓減</b>。何度目のバーサーカーかで、ゲージ2の大きさが決まる。
    /// ⚠ 0 にはしない（0にすると突入した瞬間に死ぬ＝崖が戻る）。細く長く痩せさせる。
    /// </summary>
    public static float Phase2Ratio
    {
        get
        {
            switch (entries)
            {
                case 0: return 1.00f;   // 1回目（＝これから入る回）
                case 1: return 0.70f;
                case 2: return 0.40f;
                case 3: return 0.25f;
                default: return 0.15f;
            }
        }
    }

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
        return ratio;
    }

    /// <summary>
    /// 波を凌いだ。`wiped`＝逃した者が1人も居ないか。
    /// ⚠ <b>軽傷と重傷はここで分かれる</b>。噂（脅威度）は既存の仕組みに任せる ―― ここでは触らない。
    /// </summary>
    public static void OnWaveEnd(bool wiped)
    {
        bool enteredBerserk = active;
        active = false;
        // 🍽️ 次の1波ぶんは1回使ったら消える（貯め込ませない）
        carryNextWave = 0f;

        // ⚠ 重傷を負った波では通常の回復をしない。**その波では減らさない**（減らすと表示が1つずれる）。
        if (enteredBerserk && !wiped)
        {
            recoveryBlockTurns = GraveBlockTurns;
            shell = Mathf.Clamp01(shell + ShellRecoverWhenGrave);   // ⚠ 詰ませないための最低限
            return;
        }
        if (recoveryBlockTurns > 0)
        {
            recoveryBlockTurns--;
            shell = Mathf.Clamp01(shell + ShellRecoverWhenGrave);   // 同上
            return;
        }
        shell = Mathf.Clamp01(shell + ShellRecoverPerWave);
    }

    /// <summary>🔄 新しい周のために畳む。</summary>
    public static void Reset()
    {
        shell = 1f; entries = 0; recoveryBlockTurns = 0; active = false;
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
        if (recoveryBlockTurns > 0) s += "・修復 +" + Mathf.RoundToInt(ShellRecoverWhenGrave * 100f)
            + "% のみ（あと" + recoveryBlockTurns + "T）";
        else if (shell < 1f) s += "・毎波 +" + Mathf.RoundToInt(ShellRecoverPerWave * 100f) + "%";
        if (entries > 0) s += "　次の第二形態 " + NextPhaseText;
        return s;
    }
}
