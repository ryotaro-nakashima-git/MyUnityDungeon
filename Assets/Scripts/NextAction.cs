using UnityEngine;

/// <summary>
/// ▶ <b>次の一手</b>（K-6 A-2）。「いま何をすべきか」に<b>変わり続ける</b>大ボタンの中身。
///
/// <para>
/// ⚠⚠ <b>なぜ要るか（実測）</b>：『侵略開始』と『ターンを終える』は最初から最後まで同じ顔で置いてあり、
///   <b>まだ打てる手が3つ残っていても同じように押せた</b>。その結果、<b>DPを 1,237〜3,425 抱えたまま
///   波に入って死ぬ</b>のが常態になっていた。
/// </para>
///
/// <para>
/// ⚠⚠ <b>重み比べにしない。上から順に見て、最初に当てはまった1つだけを出す。</b>
///   重みで competing させると、実測どおり `装備を鍛える`(weight 97) のような常時上位が居座って
///   <b>他が一生出てこない</b>（→ [[wall-is-placement-cap]]）。順番は固定、規則は単純。
/// ⚠⚠ <b>条件は「数えられる事実」だけ</b> ―― 空いている／足りていない／余っている。
///   <b>強さを式で予想して「まだ弱い」とは言わない</b>（→ [[readiness-and-trade]]）。
/// ⚠ <b>通せんぼはしない。</b>ここが何を返しても『侵略開始』『ターンを終える』は押せる。
///   出すのは事実だけで、急ぎたい人を止めない。
/// </para>
///
/// 関連: [[GuideSystem]]（同じ事実を進言としても出す） [[GameUIManager.Hud]]（大ボタン）。
/// </summary>
public static class NextAction
{
    /// <summary>1つぶんの提案。`go` は `GameUIManager.GoToAdvice` が解くキー。</summary>
    public struct Step
    {
        public string label;   // 大ボタンの文字
        public string go;      // 行き先
        public string note;    // 小さく添える一言（費用など）。無ければ空
        public bool none;      // 打てる手が尽きた
    }

    // ============ 迷宮（準備フェーズ）============

    /// <summary>いま押すべき1つ。⚠ 上から順、最初に当てはまったものを返す。</summary>
    public static Step Dungeon()
    {
        var fm = DungeonFeatureManager.Instance;
        var res = DungeonResourceManager.Instance;
        int dp = res != null ? res.DungeonPoints : 0;

        // ① 何も置いていない ―― これだけは強さの話ではなく「始まってすらいない」
        if (fm != null && fm.PlacedCount == 0)
            return Mk("罠を置く", "tool:罠", "まだ盤に何も無い");

        // ② 巣が足りない（1つの巣が捌けるのは 5〜6体/波）
        if (fm != null && dp >= 300 && fm.PlacedCount < fm.PlacementCap)
        {
            int want = Mathf.Max(1, Mathf.CeilToInt(WaveRoster.Count / 6f));
            if (fm.NestCount < want)
                return Mk("巣を置く", "tool:巣", "300 DP／いま " + fm.NestCount + " つ・要 " + want);
        }

        // ③ 環境が足りない（巣1つにつき2つまで効く）
        if (fm != null && fm.NestCount > 0 && dp >= 200 && fm.PlacedCount < fm.PlacementCap
            && fm.HabitatCount < fm.NestCount * HabitatCatalog.MaxStack)
            return Mk("環境を置く", "tool:環境", "200 DP／巣のまわり2マス");

        // ④ 配置枠が空いている
        if (fm != null && fm.PlacedCount < fm.PlacementCap && dp >= 200)
            return Mk("守りを置く", "tool:罠", "残り枠 " + (fm.PlacementCap - fm.PlacedCount));

        // ⑤ 殻が削れている（→ [[LordBerserk]]）
        if (LordBerserk.Shell < 0.5f)
            return Mk("最下層を厚くする", "floor:deepest",
                      "殻 " + Mathf.RoundToInt(LordBerserk.Shell * 100f) + "%");

        // ⑥ 研究点が余っている
        if (ResearchState.RP > 0 && CheapestResearchCost() <= ResearchState.RP)
            return Mk("研究する", "panel:研究", "研究点 " + ResearchState.RP);

        // ⑦ DPが召喚1体ぶんを超えて余っている
        if (dp >= 600)
            return Mk("配下を召喚", "panel:魔物", "DP " + dp);

        return Done();
    }

    // ============ 地上（後半）============

    public static Step Surface()
    {
        // ⓪ 儀が開いているのに始めていない ―― 勝ちへの最後の一手
        if (!VictorySystem.Decided && VictorySystem.RitePathOf(VictorySystem.Self) < 0)
            for (int p = 0; p < VictorySystem.PathCount; p++)
                if (VictorySystem.RiteUnlocked(VictorySystem.Self, (VictorySystem.Path)p))
                    return Mk("儀を始める", "surface:勝利", VictorySystem.RiteName((VictorySystem.Path)p));

        // ① 生産の列が空いている拠点がある
        foreach (var rg in SurfaceMap.All)
        {
            if (rg == null || !rg.owned || rg.settle == SurfaceMap.Settle.None) continue;
            if (ProductionSystem.CountAt(rg.id) == 0)
                return Mk("生産を選ぶ", "surface:生産", rg.name + " の列が空");
        }

        // ② 命令を待っているユニットがいる（段B：ユニットの札）
        //   ⚠ 以前は「眷属を動かす」→眷属の一覧を開くだけで、**どのユニットに何をさせるか**へ繋がっていなかった。
        //     押すとカメラがその1体へ寄り、札が開く（→ [[GameUIManager.Units]]）。
        int waiting = UnitOrders.WaitingCount();
        if (waiting > 0)
        {
            var nx = UnitOrders.NextWaiting(new UnitOrders.Unit(UnitOrders.Kind.None, -1));
            return Mk("命令を待つユニット（" + waiting + "）", "unit:next", "次：" + UnitOrders.NameOf(nx));
        }

        // ③ 属性ポイントが余っている
        if (AttributeSystem.TotalPoints > 0)
            return Mk("属性を使う", "surface:属性", AttributeSystem.TotalPoints + " 点");

        // ④ 政策スロットが空いている
        for (int i = 0; i < PolicySystem.SlotCount; i++)
            if (PolicySystem.SlottedAt(i) < 0)
                return Mk("政策を差す", "surface:政策", "空きスロット");

        return Done();
    }

    // ============ 小道具 ============

    private static Step Mk(string label, string go, string note)
        => new Step { label = label, go = go, note = note, none = false };
    private static Step Done() => new Step { none = true };

    private static int CheapestResearchCost()
    {
        int best = int.MaxValue;
        var all = ResearchCatalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            var n = all[i];
            if (ResearchState.IsResearched(n.id)) continue;
            if (!ResearchState.PrereqMet(n) || !ResearchState.EraMet(n) || !ResearchState.GateMet(n)) continue;
            int c = ResearchState.EffectiveCost(n);
            if (c < best) best = c;
        }
        return best;
    }

}
