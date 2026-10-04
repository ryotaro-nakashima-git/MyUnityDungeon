using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 📒 **数値の台帳**（数理設計 P0・形式仕様 §8）。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：調整する数字が 95ファイルに332個、`const` で散らばっていて、
///   どれが測って決めた値で、どれが仮置きかを一覧する手段が無かった。
///   ここに集め、1つの数字に **単位・範囲・状態（仮／測定済み／導出）・理由** を持たせる。
///   人も AI も同じファイル（`StreamingAssets/Balance/params.json`）を見て触る。
/// </para>
///
/// <para>
/// 使い方：<c>Balance.F("wave.batch.max", 7f)</c> ― 第2引数は**台帳が無いときの既定値**。
/// ⚠ 既定値は**移す前のコードの値そのまま**にする（P0 は規則を変えない）。
/// ⚠ 同じ id をコードの2か所で**別の既定値**で呼ぶと警告する（写し間違いの検出）。
/// ⚠ 台帳が読めない・値が範囲外のときは既定値で動き、警告を1回だけ出す（遊べなくはしない）。
/// ⚠ エディタでは、ファイルが書き換わったら読み直す（1秒に1回だけ更新日時を見る）。
/// </para>
/// </summary>
public static class Balance
{
    [Serializable]
    public class Param
    {
        public string id;
        public float value;
        public string unit;
        public float[] range;
        public string state;     // 仮 ／ 測定済み ／ 導出
        public string system;    // 系1〜系6・計測
        public string why;
        public string evidence;
        public string updated;
    }

    [Serializable]
    private class FileBody { public Param[] @params; }

    /// <summary>📏 計測用の実行ファイルから、書き出し時の写しではなくプロジェクトの台帳を直接読むための上書き（→ [[MeasureBoot]]）。</summary>
    public static string PathOverride;
    public static string FilePath => !string.IsNullOrEmpty(PathOverride) ? PathOverride
        : Path.Combine(Application.streamingAssetsPath, "Balance", "params.json");

    private static Dictionary<string, Param> map;
    private static readonly Dictionary<string, float> defaultsSeen = new Dictionary<string, float>();
    private static readonly HashSet<string> warned = new HashSet<string>();
    private static DateTime loadedStamp;
    private static float nextCheck;
    private static string hash = "";

    /// <summary>いまの台帳の中身を表す短い印（CSV に載せて、どの台帳で測ったかを残す）。</summary>
    public static string Hash { get { EnsureLoaded(); return hash; } }
    public static int Count { get { EnsureLoaded(); return map.Count; } }
    public static IEnumerable<Param> All { get { EnsureLoaded(); return map.Values; } }

    public static float F(string id, float def)
    {
        EnsureLoaded();
        float seen;
        if (defaultsSeen.TryGetValue(id, out seen))
        {
            if (!Mathf.Approximately(seen, def)) WarnOnce("def:" + id, "📒 台帳『" + id + "』がコードの2か所で別の既定値（" + seen + " と " + def + "）で呼ばれている");
        }
        else defaultsSeen[id] = def;

        Param p;
        if (map == null || !map.TryGetValue(id, out p))
        {
            WarnOnce("miss:" + id, "📒 台帳に『" + id + "』が無い ― 既定値 " + def + " で動く");
            return def;
        }
        if (p.range != null && p.range.Length == 2 && (p.value < p.range[0] || p.value > p.range[1]))
        {
            WarnOnce("range:" + id, "📒 台帳『" + id + "』= " + p.value + " は範囲 [" + p.range[0] + ", " + p.range[1] + "] の外 ― 既定値 " + def + " で動く");
            return def;
        }
        return p.value;
    }

    public static int I(string id, int def) => Mathf.RoundToInt(F(id, def));

    /// <summary>読み直す（エディタで台帳を書き換えたとき、計測の周の頭）。</summary>
    public static void Reload()
    {
        map = new Dictionary<string, Param>();
        hash = "none";
        try
        {
            string p = FilePath;
            if (!File.Exists(p)) { WarnOnce("nofile", "📒 台帳ファイルが無い（" + p + "）― すべて既定値で動く"); return; }
            string json = File.ReadAllText(p);
            var body = JsonUtility.FromJson<FileBody>(json);
            if (body != null && body.@params != null)
                foreach (var q in body.@params)
                    if (q != null && !string.IsNullOrEmpty(q.id)) map[q.id] = q;
            loadedStamp = File.GetLastWriteTimeUtc(p);
            hash = ShortHash(json);
            Debug.Log("📒『台帳』" + map.Count + " 項目を読んだ（" + hash + "）");
        }
        catch (Exception e)
        {
            WarnOnce("readerr", "📒 台帳を読めない ― すべて既定値で動く：" + e.Message);
        }
    }

    private static void EnsureLoaded()
    {
        if (map == null) { Reload(); return; }
#if UNITY_EDITOR
        // 書き換わったら読み直す（1秒に1回だけ見る）
        float now = Time.realtimeSinceStartup;
        if (now < nextCheck) return;
        nextCheck = now + 1f;
        try
        {
            string p = FilePath;
            if (File.Exists(p) && File.GetLastWriteTimeUtc(p) != loadedStamp) Reload();
        }
        catch { }
#endif
    }

    private static void WarnOnce(string key, string msg)
    {
        if (warned.Add(key)) Debug.LogWarning(msg);
    }

    private static string ShortHash(string s)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h.ToString("x8");
        }
    }
}
