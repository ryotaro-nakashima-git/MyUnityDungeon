using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🎬 配下のコマ送りアニメ（[[MinionSprite]] の1枚絵に対する動きの層）。
///
/// **なぜ Animator を使わないか**：`AnimatorController` は種類×状態の数だけアセットを作る必要があり、
/// 34種×8状態＝272個の資産管理が発生する。ここでやりたいのは
/// **「PNGの並びを順番に差し替える」**だけなので、`Resources` からコマを読んで自前で回す方が軽い。
///
/// 置き場: `Resources/DungeonTale/Anim/&lt;id&gt;/&lt;state&gt;/&lt;n&gt;.png`（0始まりの連番）
/// ⚠ コマ数は状態ごとに違う（idle 4／walk 6／death 7／air 9…）ので、**読めるところまで**読む。
/// ⚠ 絵が無い種・状態は `null` を返す。呼ぶ側は**1枚絵のまま**にすること（作りかけでも壊れない）。
/// 関連: [[CharacterVisual]] [[MinionCatalog]]／対応表 docs/sprite-manifest.json。
/// </summary>
public static class MinionAnim
{
    public const string Idle = "idle";
    public const string Walk = "walk";
    public const string Run = "run";
    public const string Hit = "hit";
    public const string Death = "death";
    public const string Crouch = "crouch";
    public const string Air = "air";
    public const string Turn = "turn";

    private const int MaxFrames = 24;
    private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

    /// <summary>種のidと状態名でコマ列を引く。無ければ null。</summary>
    public static Sprite[] Get(string id, string state)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(state)) return null;
        string key = id + "/" + state;
        Sprite[] arr;
        if (cache.TryGetValue(key, out arr)) return arr;

        var list = new List<Sprite>();
        for (int i = 0; i < MaxFrames; i++)
        {
            var s = Resources.Load<Sprite>("DungeonTale/Anim/" + id + "/" + state + "/" + i);
            if (s == null) break;      // 連番が途切れたら終わり
            list.Add(s);
        }
        arr = list.Count > 0 ? list.ToArray() : null;
        cache[key] = arr;
        return arr;
    }

    public static bool Has(string id, string state) { return Get(id, state) != null; }

    /// <summary>状態ごとの再生速度（コマ/秒）。歩きは遅く、被弾は速く。</summary>
    public static float FpsOf(string state)
    {
        switch (state)
        {
            case Run: return 12f;
            case Walk: return 8f;
            case Hit: return 14f;
            case Death: return 9f;
            case Turn: return 14f;
            case Air: return 10f;
            case Crouch: return 8f;
            default: return 6f;        // idle はゆっくり
        }
    }

    /// <summary>繰り返すか。死亡と被弾と跳躍は1回きり（最後のコマで止める）。</summary>
    public static bool LoopsOf(string state)
    {
        return state != Death && state != Hit && state != Air && state != Turn;
    }
}
