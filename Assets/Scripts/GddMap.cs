using UnityEngine;

/// <summary>
/// GDD(Pixel Art Character Pack)キャラの割当。10体を「特殊エネミー6種」と「スポナー敵4種」に振り分け（ユーザー指定）。
/// 各キャラは色バリアントごとにAnimator Controller(param無し・状態名Play)を持つ。Resources用プレハブは Assets/Resources/GDD/*.prefab。
/// 関連: [[BeastMap]] [[SpumMap]] / CharacterVisual.InitGdd / DungeonFeatureManager(特殊敵/スポナー)。
/// </summary>
public static class GddMap
{
    public struct Def { public string prefab; public float scale; public bool faceLeft; }
    private static Def D(string p, float s, bool fl = false) => new Def { prefab = "GDD/" + p, scale = s, faceLeft = fl };

    // ---- 特殊エネミー（6種・プレイヤーが種類を選んで配置）----
    public static readonly string[] SpecialNames = { "コボイルド", "ファントム", "パペッティア", "ラトルズ", "スペックル", "ヴァルキリー" };
    private static readonly Def[] specials =
    {
        D("Koboiled", 0.52f), D("Phantom", 0.56f), D("Puppeteer", 0.56f),
        D("Rattles", 0.52f),  D("Speckle", 0.52f), D("Valkyrie", 0.60f),
    };
    public static int SpecialCount => specials.Length;
    public static Def Special(int i) => specials[Mathf.Clamp(i, 0, specials.Length - 1)];
    public static string SpecialName(int i) => SpecialNames[Mathf.Clamp(i, 0, SpecialNames.Length - 1)];

    // ---- スポナー敵（4種・スポナーから湧く。ランダムで見た目が変わる）----
    private static readonly Def[] spawners = { D("Addergul", 0.50f), D("Deton", 0.50f), D("Frank", 0.55f), D("Goop", 0.50f) };
    public static int SpawnerCount => spawners.Length;
    public static Def Spawner(int i) => spawners[Mathf.Clamp(i, 0, spawners.Length - 1)];
}
