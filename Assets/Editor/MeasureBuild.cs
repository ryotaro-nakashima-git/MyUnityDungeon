using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 📏 計測用の実行ファイルを書き出す（→ [[MeasureBoot]]・tools/measure/run_parallel.py）。
/// ⚠ 書き出し先 `Builds/Measure/` は git の対象外。プレイ中は書き出せない。
/// ⚠ シーンに `_AutoPlayHarness` が残っていると、実行ファイルが既定の設定で勝手に走る ―― 書き出す前に外すこと。
/// </summary>
public static class MeasureBuild
{
    public const string ExePath = "Builds/Measure/Dangeon.exe";

    [MenuItem("Tools/計測/計測用の実行ファイルを書き出す")]
    public static void Build()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/SampleScene.unity" };
        var opt = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = ExePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        BuildReport r = BuildPipeline.BuildPlayer(opt);
        Debug.LogWarning("📏『計測用の書き出し』" + r.summary.result + " " + r.summary.totalTime + " エラー" + r.summary.totalErrors
            + " → " + ExePath);
    }
}
