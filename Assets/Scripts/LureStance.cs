using UnityEngine;

/// <summary>
/// 🕸️ **泳がせの構え**（E-1）。そのターンだけ「深追いしない」と決める。
///
/// <para>
/// ⚠⚠ **なぜ要るか（実測の裏付け）**：通しプレイ T1〜T30 は **逃走0** で終わった。
///   その結果、次の3つが**一度も動かなかった**：
///   脅威度 1.00 のまま／装備水準 0.0 のまま／**因縁 0人**。
///   原作の核である「泳がせて世界を育てる」層が、丸ごと眠っていた。
/// </para>
///
/// <para>
/// ⚠ 根っこは「逃がすのは失敗」だったこと。上手いプレイヤーほど逃がさないので、
///   **受け身のリスク**では絶対に起きない。だから **能動の構え**にして、
///   「今日は狩る／今日は泳がせる」をターンごとに選ばせる（大招集と対になる手）。
/// </para>
///
/// <para>
/// ⚠⚠ **倍率を足さないこと。** ここで脅威度の上がり方や報酬に係数を掛けると、
///   難易度カーブの軸が1本増える → [[difficulty-curve-orders]]。
///   この構えが変えるのは **「逃げ切る人数」＝頻度だけ**。既存の式はそのまま使う。
/// </para>
///
/// <para>
/// 効くところは3つ。ぜんぶ既にある仕掛けに乗るだけ：
/// ① 半分より下まで削った相手を**それ以上叩かない**（＝退却させる）
/// ② 生きて還ると `LureEconomy` が脅威度と装備水準を上げる（既存）
/// ③ `Nemesis` は「HP半分以下で生還した者」に名を与える（既存・条件は向こうが持つ）
/// </para>
///
/// 関連: [[playtest-t1-t30]]（この機能の根拠） [[LureEconomy]] [[Nemesis]] [[FeverSystem]]（対になる手）。
/// </summary>
public static class LureStance
{
    // ⚠ 状態＝セーブに載る（`readonly` にしない → [[SaveSystem]]）
    private static bool active;
    private static int spared;
    private static int rpPaidThisTurn;

    /// <summary>いま泳がせているか（そのターン限り）。</summary>
    public static bool Active { get { return active; } }
    /// <summary>このターン見逃した人数（UIに出す）。</summary>
    public static int Spared { get { return spared; } }

    // ── ノブ（ここだけ触ればよい）──
    /// <summary>これ以下まで削ったら手を止める。⚠ `Nemesis` の「半分以下で名がつく」より**下**にする。</summary>
    public const float SpareBelow = 0.35f;
    /// <summary>見逃して生還1人につき入る研究点。⚠ RPが唯一の欠乏資源なので、ここが見返りの本体。</summary>
    public const int RpPerSpared = 1;
    /// <summary>
    /// 1ターンに泳がせで入る研究点の上限。
    /// ⚠⚠ **ここが蛇口の栓。** 後半は波が20体になるので、上限が無いと 20RP/turn 入り、
    ///   牢の尋問（1回/turn）が意味を失って研究ツリーが一気に溶ける → [[nemesis-and-prison]]。
    ///   「1ターンぶんの波を丸ごと譲って、安い節が1つ買える」くらいが釣り合う。
    /// </summary>
    public const int RpCapPerTurn = 3;

    public static void Reset() { active = false; spared = 0; rpPaidThisTurn = 0; }

    /// <summary>ターンの頭で解除する。⚠ **そのターン限り**を守る唯一の場所。</summary>
    public static void OnTurnStart() { active = false; spared = 0; rpPaidThisTurn = 0; }

    public static bool CanToggle(out string why)
    {
        why = "";
        var turn = DungeonTurnManager.Instance;
        if (turn == null || !turn.IsPreparePhase) { why = "準備フェーズにだけ決められる"; return false; }
        return true;
    }

    /// <summary>構えを切り替える。⚠ 準備フェーズだけ。戦闘に入ってからは変えられない。</summary>
    public static bool Toggle(out string why)
    {
        if (!CanToggle(out why)) return false;
        active = !active;
        if (active)
        {
            NotifySystem.Push("<b>泳がせる</b> ― 深追いはしない。生きて還った者が噂を運ぶ", NotifySystem.Kind.Story);
            Debug.Log("🕸️『泳がせ』構え ON");
        }
        else
        {
            NotifySystem.Push("<b>殲滅</b> ― 一人も帰さない", NotifySystem.Kind.Info);
            Debug.Log("🕸️『泳がせ』構え OFF");
        }
        SoundSystem.Play(SoundSystem.Sfx.Command);
        return true;
    }

    /// <summary>1人見逃した（`AdventurerAI` から）。</summary>
    public static void NoteSpared() { spared++; }

    /// <summary>見逃した相手が入口まで帰り着いた（`AdventurerAI.GrantReturnReward` から1回だけ）。</summary>
    public static void OnSparedReturned(int level)
    {
        int rp = Mathf.Min(RpPerSpared, Mathf.Max(0, RpCapPerTurn - rpPaidThisTurn));
        if (rp > 0) { ResearchState.AddRP(rp); rpPaidThisTurn += rp; }
        Debug.Log("🕸️『泳がせ』Lv" + level + " が生きて還った → 研究点 +" + rp
            + "（このターン " + rpPaidThisTurn + "/" + RpCapPerTurn + "）");
    }

    /// <summary>ボタンに出す見込み（押す前に何が起きるか分かるように）。</summary>
    public static string Forecast()
    {
        return "半分より下（HP " + Mathf.RoundToInt(SpareBelow * 100f) + "%）まで削った相手は<b>それ以上叩かない</b>。"
             + "\n生きて還った1人につき<b>研究点 +" + RpPerSpared + "</b>（1ターン最大 " + RpCapPerTurn + "）。"
             + "\n還った者は世界の<b>脅威度</b>と<b>装備水準</b>を押し上げ、<b>名を得て</b>また来る。"
             + "\n<color=#df5a5a>⚠ この構えのあいだは<b>誰も倒せない</b>。撃破DP・素材・時代の進みは入らず、無傷で奥まで来た者は<b>魔王に届く</b>。</color>";
    }
}
