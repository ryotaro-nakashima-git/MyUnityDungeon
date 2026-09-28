using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🚩 **版図が増える瞬間**（⑤）。タイルの持ち主が変わったことを、盤の上で1マスずつ見せる。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：`SurfaceMap.SetOwner` は **真偽値を書き換えるだけ**だった。
///   眷属を進軍させ、軍団を戦わせ、勝った ―― その結果が「次に盤を見たら色が変わっている」
///   でしか伝わらない。4Xで一番気持ちのいい瞬間（**版図が広がる**）が、丸ごと無音だった。
/// </para>
///
/// <para>
/// ⚠ **報酬は1つも足さない。** ここは既に起きたことを見せるだけ。
/// ⚠ 1マスずつ**ずらして**出す。同時に10マス光らせても「10増えた」とは読めない。
///   ⚠ ただし数えるのは全部（見せる上限を超えた分も `Gained` には入る）。
/// ⚠ 時間は `unscaledDeltaTime`（地上に倍速は無いが、`timeScale` を跨いでも止まらないように）。
/// ⚠ ターンの締めで奪ったぶんは、`EndSurfacePhase` の解決中に起きる。
///   そのため④の**収穫の間**（画面を待たせているあいだ）にそのまま流れる ―― 意図してそう並べてある。
/// </para>
///
/// 関連: [[SurfaceMap]]（唯一の関所） [[SurfaceView]]（描く先） [[HarvestBurst]]（同じ間に流れる）。
/// </summary>
public static class ClaimFx
{
    private const float Stagger = 0.13f;   // 1マスずつの間
    private const int MaxShown = 14;       // 見せる上限（数えるのは全部）

    private struct Item { public int region; public bool gained; }

    private static readonly Queue<Item> queue = new Queue<Item>();
    private static float timer;

    /// <summary>このターンに増えた／失った数（④の収穫の行に添える）。</summary>
    public static int Gained { get; private set; }
    public static int Lost { get; private set; }

    public static void BeginTurn() { Gained = 0; Lost = 0; }
    public static void Reset() { queue.Clear(); timer = 0f; BeginTurn(); }

    /// <summary>🚩 持ち主が変わった。⚠ `SurfaceMap.SetOwner` から**1箇所だけ**呼ぶ。</summary>
    public static void Note(int regionId, bool gained, bool lost)
    {
        if (!gained && !lost) return;
        if (gained) Gained++; else Lost++;
        if (queue.Count < MaxShown) queue.Enqueue(new Item { region = regionId, gained = gained });
    }

    /// <summary>⏱️ 地上が見えているあいだだけ流す。⚠ 見えていないときは溜めたまま待つ。</summary>
    public static void Tick(float dt, SurfaceView view)
    {
        if (view == null || queue.Count == 0) return;
        timer -= dt;
        if (timer > 0f) return;
        timer = Stagger;

        var it = queue.Dequeue();
        if (it.region < 0 || it.region >= SurfaceMap.Count) return;
        var r = SurfaceMap.Get(it.region);

        if (it.gained)
        {
            // 🌾 その土地が**何を産むか**を添える。数が増えただけでは値打ちが伝わらない。
            string worth = r.dpYield > 0 ? "+" + r.dpYield + " DP/T" : "版図 +1";
            view.PopText(it.region, worth, "#5cc47c");
            view.Flash(it.region, new Color(0.36f, 0.77f, 0.49f));
            SoundSystem.Play(SoundSystem.Sfx.Gain, 0.5f, 1.15f);
        }
        else
        {
            view.PopText(it.region, "奪われた", "#e05a5a");
            view.Flash(it.region, new Color(0.88f, 0.35f, 0.35f));
            SoundSystem.Play(SoundSystem.Sfx.Loss, 0.5f, 1.0f);
        }
        view.MarkDirty();
    }

    /// <summary>まだ見せ終えていないか（④の待ち時間を伸ばすのに使う）。</summary>
    public static bool Pending { get { return queue.Count > 0; } }

    /// <summary>収穫の行に添える1文（増減が無ければ空）。</summary>
    public static string Line()
    {
        if (Gained == 0 && Lost == 0) return "";
        var sb = new System.Text.StringBuilder();
        if (Gained > 0) sb.Append("<color=#5cc47c>版図 +" + Gained + "</color>");
        if (Lost > 0) { if (sb.Length > 0) sb.Append("　"); sb.Append("<color=#e05a5a>-" + Lost + "</color>"); }
        return sb.ToString();
    }
}
