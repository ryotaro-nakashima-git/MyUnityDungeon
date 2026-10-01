using UnityEngine;

/// <summary>
/// 🩸 **感情の刈り取り**（①の2本目）。盤の上に**溜まったまま未回収の報酬**を、いま取る手。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：冒険者は宝箱で喜び、罠で怯えながら `currentJoy` / `currentFear` を溜める。
///   ところがそれが払われるのは **`GrantReturnReward`（生きて帰り着いたとき）だけ**。
///   つまり <b>倒すと、その冒険者が溜めた感情は丸ごと消える</b>。
///   盤の上には常に「まだ誰の物でもない報酬」が歩き回っているのに、
///   プレイヤーにはそれが見えず、触ることもできなかった。
/// </para>
///
/// <para>
/// これが賭けになる。
/// <list type="bullet">
/// <item><b>早く刈る</b>：確実。ただし少ない。</item>
/// <item><b>待つ</b>：宝箱と罠を踏むほど増える。ただし**帰られたら**（＝装備水準が上がる）、
///   **倒してしまえば**（＝感情は消える）、どちらも取り逃がす。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠ **見せしめの効き目**（Gemini案4の採った半分）：刈った相手の恐怖が周りに散る。
///   ⚠⚠ ただし「パニックで敵の火力が上がる」は**採らない**。押すと相手が強くなる仕掛けは
///   掛け算の軸を1本増やす（→ [[difficulty-curve-orders]]）。散らすのは**既にある恐怖の値だけ**で、
///   増えた恐怖はそのまま**次に刈る/帰す時の実り**になる ―― 新しい数字を1つも作っていない。
/// ⚠ 刈った相手の溜めは **0 に戻す**（帰っても二度は払われない）。ここを忘れると二重取りになる。
/// ⚠ 制限は**波あたりの回数**。DPは余る資源なので対価にならない（→ [[playthrough-wall-t11]]）。
/// </para>
///
/// 関連: [[AdventurerAI]]（溜めの持ち主） [[Decoy]]（同じ「戦闘中に押す物」の層） [[WaveReport]]。
/// </summary>
public static class EmotionHarvest
{
    // ── ノブ ──
    public const int PerWave = 2;
    public const float Cooldown = 4f;
    /// <summary>恐怖が散る半径（マス）。</summary>
    public const float FearRadius = 3.2f;
    /// <summary>散った恐怖の量＝刈った相手の恐怖のこの割合。⚠ 定数を新設せず、ある値から配る。</summary>
    public const float FearSpread = 0.5f;
    /// <summary>刈るのに必要な最低の溜め（これ未満は押しても意味が無いので断る）。</summary>
    public const float MinPool = 8f;

    private static int left;
    private static float cd;
    private static int reaped, dpTaken;

    public static int Left { get { return left; } }
    public static float CooldownLeft { get { return Mathf.Max(0f, cd); } }

    public static void BeginWave() { left = PerWave; cd = 0f; reaped = 0; dpTaken = 0; }
    public static void Reset() { BeginWave(); }
    public static void Tick(float dt) { if (cd > 0f) cd -= dt; }

    /// <summary>刈れるか（理由つき）。⚠ 押せない理由は**押す前に**見せる。</summary>
    public static bool CanReap(AdventurerAI a, out string why)
    {
        why = "";
        if (a == null) { return false; }
        if (left <= 0) { why = "この波の刈り取りはもう無い"; return false; }
        if (cd > 0f) { why = "まだ間が空かない（あと " + cd.ToString("0.0") + "）"; return false; }
        if (a.EmotionPool < MinPool) { why = "まだ溜まっていない（宝箱と罠を踏ませてから）"; return false; }
        return true;
    }

    /// <summary>🩸 刈る。⚠ 実りは `AdventurerAI` 側で清算する（式が2箇所に散らないように）。</summary>
    public static bool TryReap(AdventurerAI a, out string why)
    {
        if (!CanReap(a, out why)) return false;

        float fearWas = a.FearPool;
        int dp = a.ReapEmotion();          // ← 清算して 0 に戻す
        left--; cd = Cooldown;
        reaped++; dpTaken += dp;

        // 😱 見せしめ：散るのは**刈った相手の恐怖の半分**だけ。新しい数字は作らない。
        int scared = 0;
        float share = fearWas * FearSpread;
        if (share >= 1f)
        {
            foreach (var o in AdventurerAI.ActiveArray())
            {
                if (o == null || o == a || o.MyFloor != a.MyFloor) continue;
                if (Vector3.Distance(o.transform.position, a.transform.position) > FearRadius) continue;
                o.AddFear(share);
                scared++;
            }
        }

        FloatText.Spawn(a.transform.position + new Vector3(0f, 0.5f, 0f),
            "刈り取り +" + UITheme.Num(dp), UITheme.DP, 3.0f, 1.0f, 1.0f);
        BattleVfx.Burst(a.transform.position, new Color(0.75f, 0.29f, 0.42f), 0.9f);
        SoundSystem.Play(SoundSystem.Sfx.Gain, 0.8f, 0.9f);
        ScreenShake.Kick(0.12f, 0.22f);
        NotifySystem.Push("<b>刈り取り</b> ― <color=#e3a94a>+" + UITheme.Num(dp) + " DP</color>"
            + (scared > 0 ? "　周りの <b>" + scared + "</b> 人が怯えた（実りが増える）" : ""), NotifySystem.Kind.Gain);
        WaveReport.NoteChoice("刈り取り", reaped + " 回・のべ +" + UITheme.Num(dpTaken) + " DP を先に取った");
        return true;
    }

    /// <summary>そのマスに立っている冒険者（同じ階）。⚠ 盤のクリックは**人が先、罠が後**。</summary>
    public static AdventurerAI At(int floor, Vector2Int cell)
    {
        foreach (var a in AdventurerAI.ActiveArray())
        {
            if (a == null || a.MyFloor != floor) continue;
            if (a.CurrentGridPos == cell) return a;
        }
        return null;
    }

    /// <summary>下部の帯に出す1行（冒険者に乗せたときだけ）。</summary>
    public static string HoverLine(AdventurerAI a)
    {
        if (a == null) return "";
        string why;
        bool can = CanReap(a, out why);
        string pool = "<b>" + Mathf.RoundToInt(a.EmotionPool) + "</b>";
        if (can)
            return "🩸 " + a.Label + " ― <b>クリックで刈り取り</b>（溜め " + pool + " ＝およそ "
                 + UITheme.Num(a.PeekReapDp()) + " DP を先に取る）　<color=#9c95b4>残り " + left + "</color>";
        return "🩸 " + a.Label + " ― 溜め " + pool + "　<color=#e08a3c>" + why + "</color>";
    }
}
