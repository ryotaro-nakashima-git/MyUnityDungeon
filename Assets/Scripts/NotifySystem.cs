using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🔔 通知（Phase A-1）。**起きたことをプレイヤーに見せる**ための層。
///
/// これまでゲームの出来事は `Debug.Log` に296件出ていて、**画面には何も出ていなかった**。
/// 領域を制圧しても、眷属が敗走しても、敵軍が進発しても、プレイヤーは気づけない。
/// 見た目を綺麗にする前に、まず「伝わる」ことを作る。
///
/// - **トースト**：右上に数件だけ積む。数秒で消える。**押すとその場所へ飛ぶ**。
/// - **ログ**：直近50件を遡れる（Civのログ相当）。
/// - **ターンごとの仕分け**：`TurnReport` がターン間レポートに使う（[[GuideSystem]]）。
///
/// ⚠ 何でも流すと「何も伝わらない」に戻る。**Kind で重要度を分け、Gain/Loss/Story だけをトーストに出す**。
/// 純static・実行時保持。関連: [[game-polish-plan]]。
/// </summary>
public static class NotifySystem
{
    public enum Kind
    {
        Info,     // 灰：知らせるだけ（トーストに出さない・ログのみ）
        Gain,     // 金：得た（制圧・報酬・レベルアップ・研究完了）
        Loss,     // 赤：失った（領域を奪われた・敗走・壊滅）
        Danger,   // 橙：来ている（敵軍の進発・災厄）
        Story,    // 紫：物語（偉業・時代・発見・形見）
    }

    public class Notice
    {
        public string text;
        public Kind kind;
        public int regionId = -1;   // 押すと飛ぶ先（-1＝飛べない）
        public int turn;
        public float life;          // トーストの残り時間（0以下＝もう出さない）
    }

    public const float ToastLife = 7f;      // トーストが残る秒数
    public const int MaxToasts = 5;         // 同時に出す数
    public const int MaxLog = 50;           // 遡れる数

    private static List<Notice> log;
    private static List<Notice> toasts;
    private static void EnsureInit()
    {
        if (log == null) log = new List<Notice>();
        if (toasts == null) toasts = new List<Notice>();
    }
    public static void Reset() { log = new List<Notice>(); toasts = new List<Notice>(); Dirty = true; }

    public static IReadOnlyList<Notice> Log { get { EnsureInit(); return log; } }
    public static IReadOnlyList<Notice> Toasts { get { EnsureInit(); return toasts; } }
    /// <summary>UIが作り直すべきか（署名方式の代わり。毎フレーム作り直すとボタンが死ぬ）。</summary>
    public static bool Dirty;

    public static string ColorOf(Kind k)
    {
        switch (k)
        {
            case Kind.Gain: return "#e3c34a";
            case Kind.Loss: return "#e05a5a";
            case Kind.Danger: return "#e08a3c";
            case Kind.Story: return "#b48be6";
            default: return "#9c95b4";
        }
    }

    /// <summary>通知を積む。regionId を渡すと、トーストを押したときにそのタイルへ飛べる。</summary>
    public static void Push(string text, Kind kind = Kind.Info, int regionId = -1)
    {
        EnsureInit();
        var n = new Notice
        {
            text = text, kind = kind, regionId = regionId,
            turn = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 0,
            life = kind == Kind.Info ? 0f : ToastLife,
        };
        log.Add(n);
        while (log.Count > MaxLog) log.RemoveAt(0);
        if (n.life > 0f)
        {
            toasts.Add(n);
            while (toasts.Count > MaxToasts) toasts.RemoveAt(0);
        }
        Dirty = true;
    }

    /// <summary>毎フレーム：トーストの寿命を減らす（timeScaleに左右されないよう unscaled）。</summary>
    public static void Tick(float unscaledDelta)
    {
        EnsureInit();
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            toasts[i].life -= unscaledDelta;
            if (toasts[i].life <= 0f) { toasts.RemoveAt(i); Dirty = true; }
        }
    }

    /// <summary>そのターンに起きたことを拾う（ターン間レポート用）。</summary>
    public static List<Notice> OfTurn(int turn)
    {
        EnsureInit();
        var l = new List<Notice>();
        foreach (var n in log) if (n.turn == turn && n.kind != Kind.Info) l.Add(n);
        return l;
    }

    /// <summary>ログの署名（変わったときだけUIを作り直すため）。</summary>
    public static string Signature { get { EnsureInit(); return log.Count + "/" + toasts.Count; } }
}
