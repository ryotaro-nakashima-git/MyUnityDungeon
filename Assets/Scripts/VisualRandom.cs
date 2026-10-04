/// <summary>
/// 🎲 <b>演出と音のための乱数</b>（ゲームの乱数 <c>UnityEngine.Random</c> とは別の流れ）。
///
/// <para>
/// ⚠⚠ <b>なぜ分けるか</b>（2026-10-01）：ゲームの動きは固定の刻みで進む（→ [[SimClock]]）ので、
///   同じ種なら速さに関係なく同じ結果になるはず。ところが攻撃の火花・数字の揺れ・音の高さが
///   <b>同じ乱数を引いていた</b>ため、倍速で画面の描き替えの回数が変わると乱数の順番がずれ、
///   4倍と16倍で同じ種の周がT1から枝分かれしていた（完全一致 7%）。
///   見た目と音はこちらから引き、ゲームの乱数を1つも消費しない。
/// </para>
/// ⚠ ゲームの結果に関わる乱数（湧き・名簿・命中・行き先・報酬など）には使わないこと。
/// </summary>
public static class VisualRandom
{
    private static readonly System.Random rng = new System.Random(12345);

    public static float Value => (float)rng.NextDouble();
    public static float Range(float min, float max) => min + (max - min) * (float)rng.NextDouble();
}
