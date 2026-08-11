using UnityEngine;

/// <summary>
/// 🜲 **種族の権能**（L-3）。魔王の号令の5枠目だけは、**種族によって中身が変わる**。
///
/// **なぜ要るか**：16種族あるのに、違いが「HP/攻撃の倍率」と「パッシブ1つ」だけだった。
/// 進化先を選ぶ判断が数値比較にしかならない。戦闘中に押すボタンが変われば、
/// 「どの種族になるか」がプレイの形そのものを変える選択になる。
///
/// **配線**：種族ごとに書かずに `DemonLordRaceTree` が既に持っている
/// `skill`(`MinionSkillKind`) で引く。⚠ 16通り書くと種族を足すたびに書き足す羽目になる。
/// 人種(`None`)だけは権能が無い＝**進化する理由**になる。
///
/// ⚠ 効果は**既にある動詞だけ**で組む（ダメージ／回復／状態異常／攻撃倍率）。
///   新しい戦闘ルールを増やすと「押せるのに何も起きない」ができる。
/// ⚠ 攻撃強化は `RallyAtkMult` 1本に集約し、`ZombieAI` の与ダメ計算に**1箇所だけ**掛ける。
///
/// 関連: [[CommandSystem]]（5枠目としてここを呼ぶ） [[DemonLordRaceTree]] [[LordStance]]。
/// </summary>
public static class LordAuthority
{
    // ── 鬨の声などの一時強化（戦闘中だけ・保存しない）──
    private static float rallyMult = 1f;
    private static float rallyLeft = 0f;

    /// <summary>防衛体の与ダメに掛かる一時倍率。⚠ 参照は `ZombieAI` の1箇所だけ。</summary>
    public static float RallyAtkMult { get { return rallyLeft > 0f ? rallyMult : 1f; } }
    public static float RallyLeft { get { return rallyLeft; } }

    public static void Reset() { rallyMult = 1f; rallyLeft = 0f; }
    public static void Tick(float dt) { if (rallyLeft > 0f) rallyLeft = Mathf.Max(0f, rallyLeft - dt); }

    private static void Rally(float mult, float sec)
    {
        // 重ね掛けはしない（強い方・長い方で上書き＝連打の意味を消す）
        rallyMult = Mathf.Max(rallyMult > 1f && rallyLeft > 0f ? rallyMult : 1f, mult);
        rallyLeft = Mathf.Max(rallyLeft, sec);
    }

    // ============ 定義 ============
    private static MinionSkillKind Kind
    {
        get
        {
            var dl = DemonLord.Instance;
            return dl != null ? dl.RaceSkill : MinionSkillKind.None;
        }
    }

    public static bool Available { get { return Kind != MinionSkillKind.None; } }

    /// <summary>いまの種族の権能を `CommandSystem.Def` の形で返す（号令バーがそのまま描ける）。</summary>
    public static CommandSystem.Def CurrentDef()
    {
        var d = new CommandSystem.Def();
        switch (Kind)
        {
            case MinionSkillKind.Roar:
                d.jpName = "鬨の声"; d.dp = 420; d.cd = 55f; d.colorHex = "#e0623c";
                d.desc = "14秒間、**全ての防衛体の攻撃が+50%**になる。"; break;
            case MinionSkillKind.Swift:
                d.jpName = "疾風の令"; d.dp = 400; d.cd = 60f; d.colorHex = "#5ad2e0";
                d.desc = "侵入中の**全ての冒険者を凍らせて足止め**する。"; break;
            case MinionSkillKind.Lifedrain:
                d.jpName = "血の饗宴"; d.dp = 480; d.cd = 65f; d.colorHex = "#a3202e";
                d.desc = "全ての冒険者を裂き、**与えた傷の30%を魔王が飲む**。"; break;
            case MinionSkillKind.HealAura:
                d.jpName = "生命の泉"; d.dp = 360; d.cd = 50f; d.colorHex = "#5cc47c";
                d.desc = "全ての防衛体のHPを**55%回復**し、魔王も癒える。"; break;
            case MinionSkillKind.Thorns:
                d.jpName = "大地の棘"; d.dp = 430; d.cd = 55f; d.colorHex = "#b08040";
                d.desc = "地を割り、**全ての冒険者を貫く**。"; break;
            case MinionSkillKind.Regen:
                d.jpName = "満ちる潮"; d.dp = 420; d.cd = 60f; d.colorHex = "#6fb7e6";
                d.desc = "全ての防衛体を35%回復し、10秒間 攻撃+25%。"; break;
            case MinionSkillKind.Undying:
                d.jpName = "不滅の誓い"; d.dp = 460; d.cd = 70f; d.colorHex = "#ffd24a";
                d.desc = "全ての防衛体を40%回復し、12秒間 攻撃+30%。**落ちない盾**。"; break;
            case MinionSkillKind.Intimidate:
                d.jpName = "畏怖の眼"; d.dp = 440; d.cd = 60f; d.colorHex = "#b48be6";
                d.desc = "全ての冒険者を毒に侵し、10秒間 味方の攻撃+25%。"; break;
            case MinionSkillKind.PackTactics:
                d.jpName = "群狼の令"; d.dp = 400; d.cd = 55f; d.colorHex = "#8cc84a";
                d.desc = "16秒間、**生きている防衛体1体につき攻撃+6%**（最大+60%）。"; break;
            case MinionSkillKind.PoisonBody:
                d.jpName = "瘴気の令"; d.dp = 400; d.cd = 55f; d.colorHex = "#7ec46a";
                d.desc = "全ての冒険者を毒に侵し、傷を負わせる。"; break;
            case MinionSkillKind.PetrifyGaze:
                d.jpName = "石化の睨み"; d.dp = 460; d.cd = 65f; d.colorHex = "#9aa0b0";
                d.desc = "全ての冒険者を凍らせ、同時に傷を負わせる。"; break;
            case MinionSkillKind.SelfDestruct:
                d.jpName = "焦土の令"; d.dp = 500; d.cd = 70f; d.colorHex = "#ff7a3c";
                d.desc = "全てを焼く。**魔王自身も最大HPの8%を失う**。"; break;
            default:
                d.jpName = "種族の権能"; d.dp = 0; d.cd = 0f; d.colorHex = "#6f6889";
                d.desc = "まだ持っていない。**種族進化**すると、その種族だけの権能が使えるようになる。"; break;
        }
        return d;
    }

    // ============ 発動 ============
    public static void Invoke()
    {
        var dl = DemonLord.Instance;
        int magic = dl != null ? dl.GetStatRank((int)DemonLord.Stat.Magic) : 0;
        float unit = 90f * (magic + 1);          // 全体攻撃1発ぶんの目安（単体の『魔王の一撃』の半分）
        string nm = CurrentDef().jpName;

        switch (Kind)
        {
            case MinionSkillKind.Roar: Rally(1.50f, 14f); Say(nm, "防衛体の攻撃が高まった"); break;
            case MinionSkillKind.Swift: Freeze(); Say(nm, "冒険者の足が止まった"); break;
            case MinionSkillKind.Lifedrain:
                {
                    float dealt = Splash(unit * 1.1f);
                    if (dl != null) dl.Heal(dealt * 0.30f);
                    Say(nm, Mathf.RoundToInt(dealt) + " の傷／魔王が " + Mathf.RoundToInt(dealt * 0.30f) + " 回復");
                    break;
                }
            case MinionSkillKind.HealAura:
                {
                    int n = HealAll(0.55f);
                    if (dl != null) dl.Heal(unit * 2f);
                    Say(nm, n + " 体を癒やした");
                    break;
                }
            case MinionSkillKind.Thorns: Say(nm, Mathf.RoundToInt(Splash(unit * 1.25f)) + " の傷"); break;
            case MinionSkillKind.Regen: { int n = HealAll(0.35f); Rally(1.25f, 10f); Say(nm, n + " 体を癒やし、攻撃も高めた"); break; }
            case MinionSkillKind.Undying: { int n = HealAll(0.40f); Rally(1.30f, 12f); Say(nm, n + " 体が立て直した"); break; }
            case MinionSkillKind.Intimidate: Poison(); Rally(1.25f, 10f); Say(nm, "冒険者を毒に侵した"); break;
            case MinionSkillKind.PackTactics:
                {
                    int alive = CountDefenders();
                    Rally(1f + Mathf.Min(10, alive) * 0.06f, 16f);
                    Say(nm, "群れ " + alive + " 体ぶん、攻撃 +" + Mathf.RoundToInt(Mathf.Min(10, alive) * 6) + "%");
                    break;
                }
            case MinionSkillKind.PoisonBody: Poison(); Say(nm, Mathf.RoundToInt(Splash(unit * 0.8f)) + " の傷と毒"); break;
            case MinionSkillKind.PetrifyGaze: Freeze(); Say(nm, Mathf.RoundToInt(Splash(unit * 0.9f)) + " の傷と凍結"); break;
            case MinionSkillKind.SelfDestruct:
                {
                    float dealt = Splash(unit * 1.8f);
                    if (dl != null) dl.SelfBurn(0.08f);
                    Say(nm, Mathf.RoundToInt(dealt) + " の傷（魔王も焼けた）");
                    break;
                }
        }
    }

    private static void Say(string nm, string what)
    {
        NotifySystem.Push("🜲『" + nm + "』" + what, NotifySystem.Kind.Gain);
        Debug.Log("🜲『" + nm + "』" + what);
    }

    // 侵入中の全冒険者へ（範囲ではなく全体＝権能の格）
    private static float Splash(float dmg)
    {
        float total = 0f;
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsInactive.Exclude))
        {
            if (a == null) continue;
            a.TakeDamage(dmg); total += dmg;
        }
        return total;
    }
    private static void Freeze()
    {
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsInactive.Exclude))
            if (a != null) a.ApplyTrapStatus((int)TrapKind.Ice);
    }
    private static void Poison()
    {
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsInactive.Exclude))
            if (a != null) a.ApplyTrapStatus((int)TrapKind.Poison);
    }
    private static int HealAll(float frac)
    {
        int n = 0;
        foreach (var z in Object.FindObjectsByType<ZombieAI>(FindObjectsInactive.Exclude))
            if (z != null && z.CommandHeal(frac)) n++;
        return n;
    }
    private static int CountDefenders()
    {
        int n = 0;
        foreach (var z in Object.FindObjectsByType<ZombieAI>(FindObjectsInactive.Exclude))
            if (z != null && !z.IsDead) n++;
        return n;
    }
}
