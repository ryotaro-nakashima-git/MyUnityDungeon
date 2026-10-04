using UnityEngine;

/// <summary>
/// ⏱️ <b>ゲームの動きの刻み</b>（2026-10-01・「速さで難しさと精度が変わる」問題の根本の直し）。
///
/// <para>
/// 冒険者・配下・魔王・湧き・配置物・罠・魔法の場・階層とターンの管理は、すべて <c>FixedUpdate</c> で動く。
/// ここで刻みを <b>ゲーム内 1/60 秒</b>に固定する。倍速（<c>Time.timeScale</c>）は「刻みを長くする」のではなく
/// <b>1画面の間に刻む回数を増やす</b> ―― だから等速でも4倍でも16倍でも、同じ種なら同じ動きになる。
/// </para>
/// <para>
/// ⚠⚠ <b>なぜ要るか</b>：旧来は <c>Update</c> で「1画面の長さ×速さ」ずつ進めていた。
///   倍速ほど1回で進む距離・攻撃の間隔・マスごとの判定・射程の出入りが粗くなり、<b>結果が速さで変わっていた</b>
///   （16倍は4倍より守りに甘く、群の比べ方まで逆に見えた ―― P1 の実測）。端数の持ち越し（`FrameTimer.Carry`）は症状を減らしただけだった。
/// </para>
/// ⚠ 画面の演出と入力は <c>Update</c> に残す（見た目の滑らかさは画面の刻みで決まる）。
/// ⚠ 1画面で刻める上限（<c>maximumDeltaTime</c>）を超えると、ゲームは遅れる（壊れはしない）。計測では広げる（→ [[MeasureBoot]]）。
/// </summary>
public static class SimClock
{
    public const float Step = 1f / 60f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        Time.fixedDeltaTime = Step;
        // 4倍速を 30fps でも遅れずに刻める幅（4/30 ≒ 0.134 秒ぶん＝8回）＋余裕
        Time.maximumDeltaTime = 0.25f;
    }
}
