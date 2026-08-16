using UnityEngine;

/// <summary>
/// ⚔️ **戦闘の芯（軽減）**。両陣営が通る**唯一の**ダメージの入口。
///
/// <para>
/// ⚠⚠ **なぜ要るか（実測）**：この作品には**ダメージ軽減がどこにも無かった**。
/// `ZombieAI.TakeDamageFromAdventurer` も `AdventurerAI.TakeDamage` も `currentHP -= damage;` だけで、
/// 防具グレードは**HP倍率にしか効いていなかった**。
/// 軽減が無いと戦闘は純粋な「HP ÷ 相手DPS」の競争になり、**優劣が決まった瞬間に終わる**。
/// </para>
///
/// <para>実測（T1・配下はLv1〜2のスケルトン3体・装備なし）：</para>
/// <code>
/// 冒険者4体： 総HP 365   総DPS 64 → 配下を全滅させるのに 19.0秒
/// 配下3体  ： 総HP 1,218 総DPS 51 → 冒険者を全滅させるのに  7.2秒
/// </code>
/// <para>HPが3.3倍違うのにDPSはほぼ同じ。だから一方的になり、防衛戦が20秒で終わっていた。</para>
///
/// <para>
/// ⚠⚠ **比を動かさないための決まり。** カーブは手当て済み（防衛÷攻撃 T10 0.80／T90 3.00
/// → [[curve-measurement-t100]]）。**そこを崩さずに時間だけ伸ばす**必要がある。だから軽減は
/// </para>
/// <list type="number">
/// <item>**両陣営に同じ式**で入れる（片側だけ増やすと比が動く）</item>
/// <item>入力は **レベルと役割/職だけ**。⚠ **装備グレードを入れない** ―― 装備は既にHP倍率として
///   効いており、しかも配下側が高くなりやすいので、入れると軽減が防衛側に偏って比が動く</item>
/// <item>**上限を必ず置く**（`MaxMitigation`）。上限の無い軽減は掛け算の軸そのもの
///   （→ [[difficulty-curve-orders]]）</item>
/// </list>
///
/// <para>
/// 数学的には「両側の実効HPが同じ倍率で増える」＝**戦闘時間だけが伸び、勝敗の比は変わらない**。
/// </para>
/// </summary>
public static class CombatMath
{
    /// <summary>軽減の上限。⚠ これを外すと掛け算の軸が1本増える。</summary>
    /// ⚠ 0.45・K=34 では **Lv35で全職が上限に張り付き、職による差が消えた**（実測）。
    ///   上限は「差が残る高さ」に置くこと。
    public const float MaxMitigation = 0.50f;
    /// <summary>`def / (def + K)` の K。小さいほど早く効く。</summary>
    private const float K = 42f;
    /// <summary>レベル1あたりの防御。両陣営で同じ値を使うこと。</summary>
    private const float PerLevel = 0.45f;

    /// <summary>防御値 → 軽減率（0〜`MaxMitigation`）。</summary>
    public static float Mitigation(float defense)
    {
        if (defense <= 0f) return 0f;
        return Mathf.Min(MaxMitigation, defense / (defense + K));
    }

    /// <summary>受けたダメージに軽減を掛ける。**両陣営ともここを通す。**</summary>
    public static float Apply(float damage, float defense)
    {
        return damage * (1f - Mitigation(defense));
    }

    // ============ 冒険者側 ============
    /// <summary>
    /// 🗡️ 冒険者の防御。前に出る職ほど厚い。
    /// ⚠ 装備グレードは入れない（上の決まり②）。
    /// </summary>
    public static float HeroDefense(AdventurerAI.Job job, int level)
    {
        float b;
        switch (job)
        {
            case AdventurerAI.Job.Warrior: b = 12f; break;   // 前衛：厚い
            case AdventurerAI.Job.Cleric: b = 8f; break;     // 中衛
            case AdventurerAI.Job.Thief: b = 5f; break;      // 避ける側なので薄い
            default: b = 4f; break;                          // 魔術師：紙
        }
        return b + level * PerLevel;
    }

    // ============ 配下側 ============
    /// <summary>
    /// 🧟 配下の防御。役割で厚みが変わる（盾が厚い）。
    /// ⚠ こちらも装備グレードは入れない。冒険者と**同じ規模**に保つことが目的。
    /// </summary>
    public static float MinionDefense(MinionCatalog.Role role, int level)
    {
        float b;
        switch (role)
        {
            case MinionCatalog.Role.Tank: b = 12f; break;    // 盾：厚い
            case MinionCatalog.Role.Melee: b = 8f; break;    // 近接
            case MinionCatalog.Role.Buff: b = 5f; break;     // 支援
            default: b = 4f; break;                          // 遠隔・妨害：薄い
        }
        return b + level * PerLevel;
    }

    // ============ ⏱️ 戦闘のテンポ ============
    /// <summary>
    /// ⏱️ **攻撃間隔に両陣営とも掛ける倍率**。大きいほど戦闘が長くなる。
    ///
    /// <para>
    /// ⚠⚠ **なぜ軽減とは別に要るか。** 戦闘の長さは「冒険者が全滅するまで」で決まり、
    ///   比は「配下の生存時間 ÷ 冒険者の生存時間」。**比を保ったまま長さだけ3倍にするには、
    ///   両方の生存時間を3倍にするしかない。** 軽減でそれをやると 67% が必要で上限を超える
    ///   （実測：いまの軽減で伸びるのは **1.2倍** まで）。
    ///   だから「長さ」は専用のノブに分ける。**両陣営に同じ値を掛けるので比は動かない。**
    /// </para>
    ///
    /// <para>⚠ 上げすぎると棒立ちに見える。素の間隔が 1.0〜1.2秒なので、1.6 で 1.6〜1.9秒。</para>
    /// </summary>
    public const float TempoScale = 1.6f;

    /// <summary>UIやデバッグ用の1行。</summary>
    public static string Label(float defense)
    {
        return "防御" + defense.ToString("0") + "（軽減" + Mathf.RoundToInt(Mitigation(defense) * 100f) + "%）";
    }
}
