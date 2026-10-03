using UnityEngine;

/// <summary>
/// 🗺️ <b>迷宮の噂</b>（数理設計 P2・系1③・甲「地図と格」の A1 と C1。仕様 §2.6）。
///
/// <para>
/// 逃げ帰った冒険者は、<b>見てきたものを持ち帰る</b>。
/// <list type="bullet">
/// <item>A1 <b>地図</b>：通った階ごとに 0〜1。地図のある階では、冒険者は<b>満足して帰らず</b>（その階の見どころはもう知っている）、
///   <b>知られた罠を踏まずに避ける</b>。＝その階を抜ける確率 s_f を上げる。</item>
/// <item>C1 <b>見たもの</b>：深い階や魔王を見た者が多いほど、次の波は<b>深くを狙う者が増え</b>（目標の深さの γ が下がる）、
///   罠を見た者が多いほど<b>盗人</b>（罠外し）、配下の群れを見た者が多いほど<b>重装</b>が増える。</item>
/// </list>
/// </para>
/// <para>
/// ⚠⚠ <b>なぜ要るか</b>：基準プレイヤー（人並みに育てる自動運転）は 10周すべて T69 まで生き、魔王は一度も削られなかった。
///   冒険者の6割は<b>B1F で満足して帰り</b>、深い階へは届かない（実測 `p3s0_reach` `p3s1_a2`）。
///   逃がしても魔王は痛くない（清算で DP まで入る）＝後半に緊張が無い。
///   ここで逃走の跳ね返りを「人数」から<b>「深さを抜けてくる力」</b>へ付け替える。
/// </para>
/// <para>
/// 対抗手段：<b>全員倒す</b>（持ち帰る者がいなければ埋まらない）／<b>広げる・掘る</b>（その階の地図が白紙・薄くなる）／
///   <b>時間</b>（毎ターン薄れる）。⚠ 倍率は足さない（確率と目盛りだけを動かす）。
/// </para>
/// ⚠ 静的フィールドはセーブに載る（readonly にしない）。新しい周では `Reset`。
/// </summary>
public static class DungeonIntel
{
    private const int MaxFloors = 8;
    private static float[] map = new float[MaxFloors];
    private static float seenDeep, seenTrap, seenHorde;   // 0〜1（逃げた者の割合の移動平均）
    // 波ごとの集計（ターン末に移動平均へ畳む）
    private static int waveEscaped, waveDeep, waveTrap, waveHorde;

    public static float Map(int floor) => floor >= 0 && floor < MaxFloors ? map[floor] : 0f;
    public static float SeenDeep => seenDeep;
    public static float SeenTrap => seenTrap;
    public static float SeenHorde => seenHorde;

    private static float Gain => Balance.F("reach.map.gain", 0.04f) * FetterSystem.MapGainMult;   // ⛓️ 漏洩の枷
    private static float Decay => Balance.F("reach.map.decay", 0.10f) * FetterSystem.MapDecayMult;
    private static float DigForget => Balance.F("reach.map.dig_forget", 0.20f);
    private static float TrapAvoid => Balance.F("reach.map.trap_avoid", 0.5f);
    private static float Ema => Balance.F("reach.seen.ema", 0.3f);
    private static float DeepK => Balance.F("reach.seen.deep_k", 1.0f);
    private static float JobK => Balance.F("reach.seen.job_k", 0.5f);

    /// <summary>冒険者1人が逃げ帰った。⚠ 侵入者（遠征の配下）は呼ばない。</summary>
    public static void OnEscaped(int deepest, int floors, bool sawTrap, bool sawHorde, bool sawLord)
    {
        float g = Gain;
        int top = Mathf.Clamp(deepest, 0, MaxFloors - 1);
        for (int f = 0; f <= top; f++)
        {
            float before = map[f];
            map[f] = Mathf.Clamp01(map[f] + g * (1f - map[f]));
            // 📣 半分を越えた瞬間だけ知らせる（プレイヤーに「漏れている」ことを見せる）
            if (before < 0.5f && map[f] >= 0.5f)
                NotifySystem.Push("<b>B" + (f + 1) + "F の地図</b>が冒険者の間に出回っている ― 満足して帰らず、知られた罠を避けてくる"
                    + "（全員倒すか、広げる・掘ると薄れる）", NotifySystem.Kind.Loss);
        }
        waveEscaped++;
        if (sawLord || (floors > 1 && deepest >= floors - 2)) waveDeep++;
        if (sawTrap) waveTrap++;
        if (sawHorde) waveHorde++;
    }

    /// <summary>ターン末：地図が薄れ、波の「見たもの」を移動平均へ畳む。</summary>
    public static void TickTurn()
    {
        float d = Decay;
        for (int f = 0; f < MaxFloors; f++) map[f] = Mathf.Max(0f, map[f] * (1f - d));
        float a = Ema;
        if (waveEscaped > 0)
        {
            seenDeep = Mathf.Lerp(seenDeep, (float)waveDeep / waveEscaped, a);
            seenTrap = Mathf.Lerp(seenTrap, (float)waveTrap / waveEscaped, a);
            seenHorde = Mathf.Lerp(seenHorde, (float)waveHorde / waveEscaped, a);
        }
        else
        {
            // 誰も帰らなかった＝噂が更新されない。少しずつ忘れられる
            seenDeep *= 1f - a; seenTrap *= 1f - a; seenHorde *= 1f - a;
        }
        waveEscaped = waveDeep = waveTrap = waveHorde = 0;
    }

    /// <summary>その階を作り直した（拡張）＝地図は白紙。</summary>
    public static void OnFloorRemade(int floor) { if (floor >= 0 && floor < MaxFloors) map[floor] = 0f; }
    /// <summary>その階を掘った・塞いだ＝地図が一部古くなる。</summary>
    public static void OnFloorDug(int floor) { if (floor >= 0 && floor < MaxFloors) map[floor] *= 1f - DigForget; }

    /// <summary>その階で知られた罠を避ける確率。</summary>
    public static float TrapAvoidChance(int floor) => Map(floor) * TrapAvoid;

    /// <summary>目標の深さの γ に掛ける値（深くを見た者が多いほど小さく＝深く狙う者が増える）。</summary>
    public static float DepthGammaMult => 1f / (1f + DeepK * seenDeep);

    /// <summary>見たものに応じて職を寄せる（罠→盗人／群れ→重装）。⚠ 布告で職が決まっている日は呼ばない。</summary>
    public static AdventurerAI.Job BiasJob(AdventurerAI.Job j)
    {
        float k = JobK;
        float r = Random.value;
        float pThief = k * seenTrap * 0.5f, pWarrior = k * seenHorde * 0.5f;
        if (r < pThief) return AdventurerAI.Job.Thief;
        if (r < pThief + pWarrior) return AdventurerAI.Job.Warrior;
        return j;
    }

    /// <summary>1行の要約（助言や計測の記録用）。</summary>
    public static string Line(int floors)
    {
        var s = new System.Text.StringBuilder("地図");
        for (int f = 0; f < Mathf.Min(floors, MaxFloors); f++) s.Append(" B").Append(f + 1).Append("F ").Append(Mathf.RoundToInt(map[f] * 100f)).Append("%");
        s.Append("／深く見た ").Append(Mathf.RoundToInt(seenDeep * 100f)).Append("% 罠 ").Append(Mathf.RoundToInt(seenTrap * 100f))
         .Append("% 群れ ").Append(Mathf.RoundToInt(seenHorde * 100f)).Append("%");
        return s.ToString();
    }

    public static void Reset()
    {
        map = new float[MaxFloors];
        seenDeep = seenTrap = seenHorde = 0f;
        waveEscaped = waveDeep = waveTrap = waveHorde = 0;
    }
}
