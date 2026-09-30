using System.Globalization;
using UnityEngine;

/// <summary>
/// 📏 <b>計測用の実行ファイルの入口</b>（数理設計・計測の高速化 2026-10-01）。
///
/// <para>
/// ⚠⚠ <b>なぜ要るか</b>：エディタの中で1周ずつ回すと、12周に4〜5時間かかっていた（CPU は16スレッドのうち約1本しか使っていない）。
///   ゲームを画面なしの実行ファイルに書き出し、<b>6〜8本を同時に</b>走らせる（→ tools/measure/run_parallel.py）。
/// </para>
/// <para>
/// 起動引数に <c>-measure</c> があるときだけ動く（普通に遊ぶ起動では何もしない）。エディタでは動かない。
/// <code>
/// Dangeon.exe -batchmode -nographics -measure -runs 2 -arms ref -seed 1000 -seedOffset 0
///             -maxTurns 130 -out docs/measure/X/_parts/ref_0 -log docs/measure/X/_parts/ref_0.md
///             -balance C:/…/Assets/StreamingAssets/Balance/params.json
/// </code>
/// ⚠ ゲームの規則には触らない。自動運転（<see cref="_AutoPlayHarness"/>）を置いて、引数を渡すだけ。
///   既定は<b>時間の刻みを固定</b>（1フレーム＝ゲーム内1/60秒）＝等速と同じ精度。`-noFixedStep -speed 4` で旧来の4倍。
/// </para>
/// </summary>
public static class MeasureBoot
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isEditor) return;
        var args = System.Environment.GetCommandLineArgs();
        if (!Has(args, "-measure")) return;

        string bal = Str(args, "-balance", "");
        if (!string.IsNullOrEmpty(bal)) { Balance.PathOverride = bal; Balance.Reload(); }

        Application.runInBackground = true;
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;

        // ⚠ 置いた瞬間に Awake が走り、既定の logPath に書き込んでしまう ―― 眠らせたまま設定してから起こす
        var go = new GameObject("_AutoPlayHarness");
        go.SetActive(false);
        var h = go.AddComponent<_AutoPlayHarness>();
        h.runs = Int(args, "-runs", 2);
        h.maxTurns = Int(args, "-maxTurns", 130);
        h.logPath = Str(args, "-log", "docs/playlog_headless.md");
        h.measureDir = Str(args, "-out", "docs/measure/headless");
        h.armPlan = Str(args, "-arms", "");
        h.seedBase = Int(args, "-seed", 0);
        h.seedOffset = Int(args, "-seedOffset", 0);
        h.measureMode = true;
        h.fixedStep = !Has(args, "-noFixedStep");
        h.measureSpeed = Flt(args, "-speed", h.fixedStep ? 1f : 4f);
        h.quitWhenDone = true;
        Object.DontDestroyOnLoad(go);
        go.SetActive(true);
        Debug.LogWarning("📏『計測用の実行ファイル』runs=" + h.runs + " arms=" + h.armPlan + " seed=" + h.seedBase + "+" + h.seedOffset
            + " out=" + h.measureDir + (h.fixedStep ? "（刻み固定）" : "（速さ " + h.measureSpeed + "）"));
    }

    private static bool Has(string[] a, string key) { for (int i = 0; i < a.Length; i++) if (a[i] == key) return true; return false; }
    private static string Str(string[] a, string key, string def)
    { for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1]; return def; }
    private static int Int(string[] a, string key, int def)
    { int v; return int.TryParse(Str(a, key, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : def; }
    private static float Flt(string[] a, string key, float def)
    { float v; return float.TryParse(Str(a, key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def; }
}
