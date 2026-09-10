using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🐺 <b>魔物の詳細</b>（UI刷新 B-3）。ツリーで選んだ1種を、右側で1枚に開く。
///
/// <para>
/// ⚠⚠ <b>なぜ分けるか</b>：一覧のノードに出すのは<b>絵とランクと費用の3つだけ</b>と決めた。
///   いままで「スケルトン 近接 F T3 HP… 群れ 個体数 最高Lv」と<b>文字が7つ</b>並んでいて、
///   絵にした意味が消えていた。残りは hover と<b>ここ</b>が引き受ける。
/// ⚠⚠ <b>召喚ボタンもここに置く。</b>一覧の上に置くと、また文字が並ぶ（→ 承認済みの画面案）。
/// </para>
///
/// <para>
/// ⚠ <b>ツリーの右に固定で貼る</b>（別画面に飛ばさない）。選ぶ→読む→押す、が同じ画面で完結する。
/// ⚠ 何も選んでいないときは<b>案内だけ</b>出して枠は残す ―― 出たり消えたりすると一覧の幅が動く。
/// </para>
///
/// 関連: [[GameUIManager.Codex]]（ツリー本体） [[MinionSprite]]（絵） [[MinionRoster]]。
/// </summary>
public partial class GameUIManager
{
    /// <summary>いまツリーで選んでいる種（-1＝未選択）。⚠ 盤に置く種の選択とは別物。</summary>
    private int codexPick = -1;
    private RectTransform codexDetail;
    /// <summary>詳細の幅。⚠ `codexContentW` はここを引いた残り。</summary>
    private const float CodexDetailW = 260f;

    /// <summary>詳細の器を1度だけ作る（`BuildMinionPanel` から呼ぶ）。</summary>
    private void BuildCodexDetail(Image panel, float x, float y, float h)
    {
        var p = Panel(panel, "CodexDetail", CARD);
        Place(p.rectTransform, x, y, CodexDetailW, h);
        Outline(p, LINE2);
        codexDetail = p.rectTransform;
    }

    /// <summary>選んだ種を1枚に開く。⚠ 毎回作り直す（費用も個体数も動くので差分更新はずれる）。</summary>
    private void RefreshCodexDetail()
    {
        if (codexDetail == null) return;
        for (int i = codexDetail.childCount - 1; i >= 0; i--)
        { var g = codexDetail.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }

        float W = CodexDetailW;
        if (codexPick < 0 || codexPick >= MinionCatalog.Count)
        {
            var hint = Text(codexDetail, "<color=#6f6889>ツリーの魔物を押すと、ここに詳しい数字と『召喚』が出ます。</color>",
                            12, MUTED, TextAlignmentOptions.TopLeft);
            Place(hint.rectTransform, 14, 16, W - 28, 60);
            return;
        }

        int kk = codexPick;
        var d = MinionCatalog.Get(kk);
        bool unlocked = MinionEvolution.IsUnlocked(kk);
        float yy = 12f;

        // ── 顔（絵）と名前 ──
        var art = new GameObject("Art", typeof(RectTransform)).AddComponent<Image>();
        art.rectTransform.SetParent(codexDetail, false);
        art.raycastTarget = false; art.preserveAspect = true;
        var sp = MinionSprite.ByIndex(kk);
        art.sprite = sp != null ? sp : IconFactory.Get("魔物");
        art.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        Place(art.rectTransform, 14, yy, 64, 64);

        var nm = Text(codexDetail, d.jpName, 16, unlocked ? TEXT : FAINT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(nm.rectTransform, 86, yy + 4, W - 100, 22);
        var sub = Text(codexDetail, "<color=" + RankHex(d.rank) + ">" + MinionCatalog.RankName(d.rank) + "</color>"
                       + " <color=#9c95b4>" + MinionCatalog.RoleName(d.role) + "・T" + d.tierCP + "</color>",
                       11.5f, MUTED, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(sub.rectTransform, 86, yy + 28, W - 100, 18);
        yy += 74f;

        // ── 数字（ここが「ノードに出さなかったもの」の置き場）──
        var stat = Text(codexDetail, string.Format("HP ×{0:0.00}　　攻 ×{1:0.00}　　速 ×{2:0.00}",
                        d.hpMult, d.atkMult, d.spdMult), 12, TEXT, TextAlignmentOptions.TopLeft);
        Place(stat.rectTransform, 14, yy, W - 28, 18); yy += 24f;

        string skl = MinionSkill.Label(kk);
        MagicCatalog.Spell msp;
        if (MagicCatalog.TryPickMinionSpell(kk, out msp))
            skl += "<color=" + msp.colorHex + ">◆" + msp.jpName + "</color>";
        else if (d.style == CharacterVisual.AttackStyle.Cast)
            skl += "<color=#6f6889>・魔法未解禁</color>";
        if (!string.IsNullOrEmpty(skl))
        {
            var sk = Text(codexDetail, skl, 11.5f, TEXT, TextAlignmentOptions.TopLeft);
            Place(sk.rectTransform, 14, yy, W - 28, 32); yy += 36f;
        }
        if (!string.IsNullOrEmpty(d.note))
        {
            var nt = Text(codexDetail, "<color=#9c95b4>" + d.note + "</color>", 11, FAINT, TextAlignmentOptions.TopLeft);
            Place(nt.rectTransform, 14, yy, W - 28, 40); yy += 44f;
        }

        // ── 手持ち ──
        if (unlocked)
        {
            int cnt = MinionRoster.CountOfType(kk), top = MinionRoster.TopLevelOfType(kk);
            var own = Text(codexDetail, cnt > 0
                ? "<color=#8cb8e6>個体 " + cnt + " 体　最高 Lv" + top + "</color>"
                : "<color=#6f6889>まだ1体も居ない</color>", 12, MUTED, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(own.rectTransform, 14, yy, W - 28, 18); yy += 26f;
        }

        // ── ここが「押す場所」。⚠ 一覧の上には置かない ──
        if (unlocked)
        {
            int scost = MinionRoster.SummonCost(kk);
            var sumBtn = PrimaryButton(codexDetail, "召喚する（" + scost + " DP）", BLOOD, TEXT, () =>
            {
                // 🧠 研究『見極め』があると、召喚は**気性の2択**になる（→ [[MinionTemperament]]）
                if (MinionTemperament.CanChoose) { OpenTemperChoiceForSummon(kk); return; }
                if (MinionRoster.TrySummon(kk) != null) { RefreshMinionCodex(); RefreshSquadStrip(); }
            }, true);
            Place((RectTransform)sumBtn.transform, 14, yy, W - 28, 34); yy += 40f;
        }
        else if (MinionEvolution.CanEvolve(kk))
        {
            var why = Text(codexDetail, "<color=#e3a94a>◆ " + MinionEvolution.PrereqName(kk) + " から進化できる</color>",
                           11.5f, GOLD, TextAlignmentOptions.TopLeft);
            Place(why.rectTransform, 14, yy, W - 28, 18); yy += 24f;
            var evoBtn = PrimaryButton(codexDetail, "進化させる（" + MinionEvolution.EvolveCost(kk) + " DP）", BLOOD, TEXT,
                () => { if (MinionEvolution.TryEvolve(kk)) RefreshMinionCodex(); }, true);
            Place((RectTransform)evoBtn.transform, 14, yy, W - 28, 34); yy += 40f;
        }
        else
        {
            string why = MinionEvolution.TierResearchNeeded(kk)
                ? "<color=#8cb8e6>研究『" + MinionEvolution.TierResearchName(kk) + "』で開く</color>"
                : "<color=#9c95b4>― " + MinionEvolution.PrereqName(kk) + " の解禁が必要</color>";
            var t = Text(codexDetail, why, 11.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(t.rectTransform, 14, yy, W - 28, 36); yy += 40f;
        }

        // ⚠ 選んだ種は「盤に置く種」でもある（押した瞬間に `SetSelectedMinion` 済み）。
        //   ここでもう一度言い直しておく ―― 押した結果が2つあることを隠さない。
        if (unlocked)
        {
            var pick = Text(codexDetail, "<color=#6f6889>下部バーの『部隊』で置くのは、いまこの種です。</color>",
                            10.5f, FAINT, TextAlignmentOptions.TopLeft);
            Place(pick.rectTransform, 14, yy, W - 28, 30);
        }
    }
}
