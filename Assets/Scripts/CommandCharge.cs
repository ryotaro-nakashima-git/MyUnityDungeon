using UnityEngine;

/// <summary>
/// 📯⚡ **号令ゲージ**（①の3本目）。撃破でゲージが溜まり、満ちたら**号令のクールダウンが全部戻る**。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：号令は DP300〜500・CD35〜70秒。実測で1波は 20〜60秒なので、
///   <b>1波に1回撃てるかどうか</b>しかない。5枠あるのに、事実上「1枠を選ぶ」ゲームだった。
///   しかも DP は余る（2周とも資源を余らせて負けた → [[playthrough-wall-t11]]）ので、
///   値段は制限として働いていない。**効いている制限はクールダウンだけ**。
///   ならば、そのクールダウンを**盤の上の働きで取り返せる**ようにする ―― それがこのゲージ。
/// </para>
///
/// <para>
/// 波の終盤に「溜めた物を解き放つ」瞬間を作るのが狙い。
/// ⚠⚠ **正のループなので上限が要る。** 「倒す→撃てる→もっと倒せる」は放っておくと発散する。
///   <b>1波に1回だけ</b>にしてある（解放したらその波では二度と溜まらない）。
/// ⚠ 溜まりは**等級で重みを付ける**。後半の波は人数が多いので、頭数だけで数えると
///   何もしなくても毎回満ちる（＝ただの時限ボーナスになって、判断が消える）。
/// ⚠ **報酬は1つも足さない。** 増えるのは撃てる回数だけで、号令の威力にも実りにも触らない。
/// </para>
///
/// 関連: [[CommandSystem]]（戻す先） [[Decoy]] [[EmotionHarvest]]（同じ層の手） [[WaveReport]]。
/// </summary>
public static class CommandCharge
{
    /// <summary>満ちるのに必要な点。⚠ G級1体＝1点なので、**中堅を10体前後**で満ちる目安。</summary>
    public const float Full = 12f;
    /// <summary>1波に解放できる回数。⚠ ここを2以上にすると正のループが発散する。</summary>
    public const int PerWave = 1;

    private static float charge;
    private static int left;

    public static float Charge { get { return charge; } }
    public static float Ratio { get { return Mathf.Clamp01(charge / Full); } }
    public static bool ReadyToRelease { get { return left > 0 && charge >= Full; } }
    public static int Left { get { return left; } }

    public static void BeginWave() { charge = 0f; left = PerWave; }
    public static void Reset() { BeginWave(); }

    /// <summary>💥 撃破で溜まる。⚠ 等級で重みを付ける（頭数だけだと後半は勝手に満ちる）。</summary>
    public static void OnKill(int rank, bool named)
    {
        if (left <= 0) return;                      // 解放済みの波では溜めない（＝上限）
        float w = 1f + Mathf.Clamp(rank, 0, 7) * 0.35f;
        if (named) w *= 2f;                          // 🗡️ 名のある相手は重い
        charge = Mathf.Min(Full, charge + w);
    }

    public static bool CanRelease(out string why)
    {
        why = "";
        if (left <= 0) { why = "この波ではもう解き放った"; return false; }
        if (charge < Full) { why = "まだ満ちていない（" + Mathf.RoundToInt(Ratio * 100f) + "%）"; return false; }
        return true;
    }

    /// <summary>📯 解き放つ：号令のクールダウンを全部戻す。⚠ 威力にも値段にも触らない。</summary>
    public static bool TryRelease(out string why)
    {
        if (!CanRelease(out why)) return false;
        left--; charge = 0f;
        CommandSystem.ClearCooldowns();

        var dl = DemonLord.Instance;
        Vector3 at = dl != null ? dl.transform.position : Vector3.zero;
        FloatText.Spawn(at + new Vector3(0f, 0.8f, 0f), "号令が冴える", UITheme.Fame, 3.2f, 1.1f, 1.1f);
        BattleVfx.Burst(at, UITheme.Fame, 1.4f);
        SoundSystem.Play(SoundSystem.Sfx.Command, 1f, 0.7f);
        ScreenShake.Kick(0.22f, 0.34f);
        NotifySystem.Push("<b>号令が冴える</b> ― すべての号令が撃てるようになった", NotifySystem.Kind.Gain);
        WaveReport.NoteChoice("号令ゲージ", "撃破で溜めて、号令を撃ち直した");
        return true;
    }
}
