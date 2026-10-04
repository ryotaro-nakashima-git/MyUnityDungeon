using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 📏 **計測専用モード**（数理設計 P0・形式仕様 §9）。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：自動運転は戦闘を**等速**で回していて、1周に約50分かかっていた。
///   数字を1つ変えて確かめるのに1〜2時間。生存曲線に要る20周は回せない。
/// </para>
///
/// <para>
/// ⚠⚠ **変えるのは「見え方と速さ」だけ。**ゲームの規則・乱数・判定の順番は1つも変えない。
///   - 戦闘の速さ：`DungeonTurnManager.ApplySpeed` がここを見る（既定 16 倍・台帳 `measure.battle_speed`）
///   - 描画：カメラの cullingMask を 0 に（⚠ `enabled` は地上モードの切り替えが書き換えるので触らない）
///   - 音：鳴らさない／フレーム上限：外す
/// ⚠ **人が遊ぶときには絶対に入らない**。入口は `_AutoPlayHarness` だけ。
/// ⚠ 速く回すと結果が変わる危険があるので、等価性の検証（§9.4）を通してから使う。
/// </para>
/// </summary>
public static class MeasureMode
{
    public static bool On { get; private set; }
    public static float BattleSpeed => SpeedOverride > 0f ? SpeedOverride : Balance.F("measure.battle_speed", 16f);
    /// <summary>自動運転から速さを直接指定する（等価性の検証で 4 倍と 16 倍を比べるため）。0＝台帳の値。</summary>
    public static float SpeedOverride;

    private static readonly Dictionary<Camera, int> savedMasks = new Dictionary<Camera, int>();
    private static int savedFrameRate, savedVSync;
    private static float savedVolume;

    private static ILogHandler savedLogHandler;
    /// <summary>
    /// 📝 普通のログだけを捨て、警告・エラー・**例外**は残す。
    /// ⚠⚠ 以前は `filterLogType = Warning` にしていたが、Unity は `filterLogType` が Exception 以外だと
    ///   **例外のログまで止める**。計測の周が T28〜30 で止まった原因の例外が、1行も残っていなかった（2026-10-02）。
    /// </summary>
    private class DropPlainLogs : ILogHandler
    {
        private readonly ILogHandler inner;
        public DropPlainLogs(ILogHandler inner) { this.inner = inner; }
        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        { if (logType != LogType.Log) inner.LogFormat(logType, context, format, args); }
        public void LogException(System.Exception exception, Object context) { inner.LogException(exception, context); }
    }
    public static void Enter()
    {
        if (On) return;
        On = true;
        savedFrameRate = Application.targetFrameRate; savedVSync = QualitySettings.vSyncCount; savedVolume = AudioListener.volume;
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
        AudioListener.volume = 0f;
        // 📝 ⚠ 普通のログは書かない（警告とエラーだけ残す）。40周の計測で Editor.log が 72GB まで膨れ、エディタが応答しなくなった（2026-09-30）。
        savedLogHandler = Debug.unityLogger.logHandler;
        Debug.unityLogger.logHandler = new DropPlainLogs(savedLogHandler);
        HideCameras();
        Debug.Log("📏『計測専用モード』戦闘 " + BattleSpeed + " 倍・描画なし・無音（規則は変えていない）");
    }

    /// <summary>新しく作られたカメラも隠す（周の頭で呼ぶ）。</summary>
    public static void HideCameras()
    {
        if (!On) return;
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam == null || savedMasks.ContainsKey(cam)) continue;
            savedMasks[cam] = cam.cullingMask;
            cam.cullingMask = 0;
        }
    }

    public static void Exit()
    {
        if (!On) return;
        On = false;
        Application.targetFrameRate = savedFrameRate;
        QualitySettings.vSyncCount = savedVSync;
        AudioListener.volume = savedVolume;
        if (savedLogHandler != null) { Debug.unityLogger.logHandler = savedLogHandler; savedLogHandler = null; }
        foreach (var kv in savedMasks) if (kv.Key != null) kv.Key.cullingMask = kv.Value;
        savedMasks.Clear();
        Debug.Log("📏『計測専用モード』を抜けた");
    }
}
