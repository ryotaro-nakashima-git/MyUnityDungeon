using UnityEngine;

/// <summary>
/// 🛡️ **備え**（このターンだけ効く、編成への一手）。
///
/// <para>
/// 『先触れ』(<see cref="WaveRoster"/>) で相手の編成が読めても、**打つ手が無ければ情報は飾り**になる。
/// 備えは「読んだ結果に対して1つだけ選ぶ」層で、**毎ターンの正解が相手によって変わる**。
/// ＝準備フェーズにようやく『今回はどうするか』という入力が生まれる。
/// </para>
///
/// <para>
/// ⚠ **数値を盛る道具にしない。** どれも「相手の得意を1つ潰す」だけで、
///   選び間違えれば効果はほぼ無い。掛け算の軸を増やさないための約束
///   （→ [[difficulty-curve-orders]]）。
/// </para>
///
/// <para>⚠ `Kind` の並びとインデックスは**セーブに載る**。**末尾にだけ足すこと。**</para>
/// </summary>
public static class WardSystem
{
    public enum Kind { Barrier = 0, Mist = 1, Creak = 2, Watch = 3, Narrow = 4, Illusion = 5 }

    /// <summary>⚠ 絵文字は持たせない。UIフォントに無い字は □ になるか丸ごと落ちる（→ [[ui-conventions]]）。
    /// 見分けは <see cref="colorHex"/> の色帯でつける。</summary>
    public struct Def
    {
        public string jpName, colorHex, desc, against;
        public int costDp;
    }

    private static readonly Def[] defs =
    {
        D("魔封じの結界", "#b48ce6", "冒険者の魔法の威力が <b>半減</b>する。術者が多い波に。", "魔術師・聖職者", 200),
        D("静謐の霧",    "#6ecf8e", "治癒が届かなくなる。<b>広域ヒールが不発</b>になり、自己回復も 1/4 に。", "聖職者", 240),
        D("軋む床",      "#e0a05a", "重装の足が鈍る。<b>戦士の移動 -40%</b>＝罠と配下の間合いに長く留まる。", "戦士", 180),
        D("見張りの目",  "#8cb8e6", "宝を持ち出せなくなる。<b>略奪がほぼ止まり</b>、装備水準の上昇を抑える。", "盗賊", 160),
        D("狭き門",      "#e3a94a", "入口を狭める。<b>一度に雪崩れ込む数が半分</b>になり、捌く余裕ができる。", "大人数の波", 260),
        D("偽りの気配",  "#e05a5a", "最下層の匂いを消す。<b>踏破目的の者が階段を見失い</b>、探索者のように彷徨う。", "踏破目的", 300),
    };

    private static Def D(string n, string col, string ds, string ag, int c)
    { var d = new Def(); d.jpName = n; d.colorHex = col; d.desc = ds; d.against = ag; d.costDp = c; return d; }

    public static int Count { get { return defs.Length; } }
    public static Def Get(int i) { return defs[Mathf.Clamp(i, 0, defs.Length - 1)]; }

    /// <summary>いま張ってある備え。-1 ＝ 無し。⚠ static の値なのでセーブに載る。</summary>
    private static int selected = -1;
    public static int Selected { get { return selected; } }
    public static bool IsOn(Kind k) { return selected == (int)k; }
    public static bool Unlocked { get { return ResearchState.IsResearched("d_ward"); } }

    /// <summary>ターンの頭で剥がれる（毎ターン選び直す＝毎ターンの判断になる）。</summary>
    public static void OnTurnStart() { selected = -1; }

    /// <summary>周をまたがない（新しい周で前の備えが張られたままにならないように）。</summary>
    public static void Reset() { selected = -1; }

    /// <summary>
    /// 備えを張る／張り替える。⚠ **準備フェーズのみ。** 張り替えは前のぶんを全額返す
    /// （選び直せないと「読んだのに間違えた」で1ターン丸ごと死ぬ）。
    /// </summary>
    public static bool TrySelect(int i, out string why)
    {
        why = "";
        if (!Unlocked) { why = "研究『備えの心得』が要る"; return false; }
        var tm = DungeonTurnManager.Instance;
        if (tm != null && tm.IsBattlePhase) { why = "戦闘中は張れない"; return false; }
        var rm = DungeonResourceManager.Instance;
        if (rm == null) return false;

        if (i == selected) { Cancel(); return true; }          // もう一度押したら剥がす
        i = Mathf.Clamp(i, 0, defs.Length - 1);
        int back = selected >= 0 ? defs[selected].costDp : 0;
        if (back > 0) rm.AddDP(back);                           // 先に返してから
        if (!rm.TrySpendDP(defs[i].costDp))
        {
            if (back > 0) rm.TrySpendDP(back);                  // 払えないなら元に戻す
            why = "DP不足"; return false;
        }
        bool first = selected < 0;
        selected = i;
        if (first) EurekaTracker.OnWard();   // 💡 張るほど『先触れ』が安くなる。⚠ 張り替えでは数えない
        SoundSystem.Play(SoundSystem.Sfx.Place);
        Debug.Log("🛡️『備え』" + defs[i].jpName + " を張った（-" + defs[i].costDp + "DP）");   // ← ログは絵文字OK
        return true;
    }

    public static void Cancel()
    {
        if (selected < 0) return;
        var rm = DungeonResourceManager.Instance;
        if (rm != null) rm.AddDP(defs[selected].costDp);
        selected = -1;
    }

    // ============ 効果（各systemはここだけを読む） ============

    /// <summary>🜁 冒険者の魔法の威力。</summary>
    public static float HeroMagicMult { get { return IsOn(Kind.Barrier) ? 0.5f : 1f; } }
    /// <summary>🌫️ 冒険者の自己回復。</summary>
    public static float HeroRegenMult { get { return IsOn(Kind.Mist) ? 0.25f : 1f; } }
    /// <summary>🌫️ 聖職者の広域ヒールが通るか。</summary>
    public static bool HealBlocked { get { return IsOn(Kind.Mist); } }
    /// <summary>🜃 戦士の移動速度。</summary>
    public static float WarriorSpeedMult { get { return IsOn(Kind.Creak) ? 0.6f : 1f; } }
    /// <summary>👁️ 持ち出される装備の量。</summary>
    public static float LootMult { get { return IsOn(Kind.Watch) ? 0.1f : 1f; } }
    /// <summary>🚪 一度に雪崩れ込む塊の大きさ。</summary>
    public static float BatchMult { get { return IsOn(Kind.Narrow) ? 0.5f : 1f; } }
    /// <summary>🕯️ 踏破目的が『探索』にすり替わるか。</summary>
    public static bool ConquerBlinded { get { return IsOn(Kind.Illusion); } }

    /// <summary>HUDや報告で1行に出すとき用。</summary>
    public static string Label
    {
        get { return selected < 0 ? "備えなし" : defs[selected].jpName; }
    }
}
