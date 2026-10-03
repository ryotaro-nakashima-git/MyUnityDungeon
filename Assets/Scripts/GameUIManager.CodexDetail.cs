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
    private const float CodexDetailW = 540f;   // 🖥️ 全画面にして 260→540（能力の棒・進化先まで1枚で読む）

    /// <summary>詳細の器を1度だけ作る（`BuildMinionPanel` から呼ぶ）。</summary>
    private void BuildCodexDetail(Image panel, float x, float y, float h)
    {
        var p = Panel(panel, "CodexDetail", CARD);
        Place(p.rectTransform, x, y, CodexDetailW, h);
        Outline(p, LINE2);
        codexDetail = p.rectTransform;
    }

    /// <summary>
    /// 🧬 <b>個体1体ぶんの絵のマス</b>（UI刷新 B-3）。図鑑・隊・ボス任命・部隊配置で<b>同じ形</b>を使う。
    ///
    /// ⚠⚠ <b>同じ物は同じ形で出す。</b>画面が変わるたびに見た目が変わると、そのたびに読み直しになる
    ///   ―― 覚えなくてよさの正体は「毎回同じ場所に同じ物がある」こと（→ [[ui-conventions]]）。
    /// ⚠ マスに出すのは<b>絵と Lv だけ</b>。名前も役割も装備も<b>hover</b>が持つ。
    /// </summary>
    /// <param name="dim">使えない（配置済み・隊に居る・地上に出ている）ときは暗くする。</param>
    /// <param name="badge">隅に小さく添える一言（「B2F隊」「地上」など）。無ければ null。</param>
    private Image IndividualCell(RectTransform parent, int individualId, float x, float y, float size,
                                 bool dim, string badge)
    {
        var v = MinionRoster.Get(individualId);
        var cell = Panel(parent, "Ind_" + individualId, CARD);
        Place(cell.rectTransform, x, y, size, size); Outline(cell, LINE);
        if (v == null) return cell;

        var art = new GameObject("Art", typeof(RectTransform)).AddComponent<Image>();
        art.rectTransform.SetParent(cell.rectTransform, false);
        art.raycastTarget = false; art.preserveAspect = true;
        var sp = MinionSprite.ByIndex(v.catalogIndex);
        art.sprite = sp != null ? sp : IconFactory.Get("魔物");
        art.color = dim ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
        art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        art.rectTransform.anchoredPosition = new Vector2(0f, 3f);   // 下の帯ぶん上に寄せる
        art.rectTransform.sizeDelta = new Vector2(size - 10f, size - 20f);

        // ⚠ **四隅を役割で分ける**。同じ隅に2つ置くと 52px では必ずぶつかる。
        //   左上＝格／右上＝Lv／下＝いま何をしているか（幅いっぱい）。
        var lv = Text(cell.rectTransform, "Lv" + v.level, 9.5f, dim ? FAINT : C("#5cc47c"),
                      TextAlignmentOptions.TopRight, FontStyles.Bold);
        lv.enableWordWrapping = false;
        Place(lv.rectTransform, size - 32, 2, 29, 13);

        // 👑 格が付いた個体は左上に印（冠は盤にも出す予定なので、ここでは文字で足りる）
        string rankName = MinionRank.NameOf(v);
        if (!string.IsNullOrEmpty(rankName))
        {
            var rk = Text(cell.rectTransform, rankName.Substring(0, 1), 9.5f, GOLD,
                          TextAlignmentOptions.TopLeft, FontStyles.Bold);
            rk.enableWordWrapping = false;
            Place(rk.rectTransform, 4, 2, 16, 13);
        }
        if (!string.IsNullOrEmpty(badge))
        {
            // ⚠⚠ **折り返しを切る**。`B1F隊` が 16px 幅で「B1F／隊」と2行になり、絵に重なっていた。
            //   幅は下の辺いっぱいに取り、字が入らなければ縮める（切れるより小さい方がまだ読める）。
            var bd = Text(cell.rectTransform, badge, 8.5f, C("#8a82a4"), TextAlignmentOptions.Bottom, FontStyles.Bold);
            bd.enableWordWrapping = false;
            bd.enableAutoSizing = true; bd.fontSizeMin = 6.5f; bd.fontSizeMax = 8.5f;
            Place(bd.rectTransform, 2, size - 14, size - 4, 12);
        }
        AddTooltip(cell.gameObject, IndividualTip(individualId));
        return cell;
    }

    /// <summary>🖐️ 掴んだときに指に付いてくる絵。⚠ 無い種は魔物の線画に落とす（影が消えない）。</summary>
    private Sprite ArtOfIndividual(int id)
    {
        var v = MinionRoster.Get(id);
        var sp = v != null ? MinionSprite.ByIndex(v.catalogIndex) : null;
        return sp != null ? sp : IconFactory.Get("魔物");
    }

    /// <summary>🧬 個体1体ぶんの hover。⚠ マスに出さなかったものを全部ここが引き受ける。</summary>
    private string IndividualTip(int id)
    {
        var v = MinionRoster.Get(id);
        if (v == null) return "";
        var d = MinionCatalog.Get(v.catalogIndex);
        var sb = new System.Text.StringBuilder();
        sb.Append("<b>").Append(MinionRank.DisplayName(v)).Append("</b>　<color=#5cc47c>Lv")
          .Append(v.level).Append("</color>");
        sb.Append("\n<color=").Append(RankHex(d.rank)).Append(">").Append(MinionCatalog.RankName(d.rank))
          .Append("</color> <color=#9c95b4>").Append(MinionCatalog.RoleName(d.role))
          .Append("・").Append(MinionTemperament.Name(v.temper)).Append("</color>");
        // 個体Lvと装備を入れた**いまの強さ**（種の倍率だけを見ても分からないので）
        float lm = MinionRoster.LevelMult(v.level);
        sb.Append("\nHP ×").Append((d.hpMult * lm * MinionRoster.EquipHpMult(id)).ToString("0.00"))
          .Append("　攻 ×").Append((d.atkMult * lm * MinionRoster.EquipAtkMult(id) * MinionRoster.TypeAtkMult(id)).ToString("0.00"));
        sb.Append("\n<color=#9c95b4>武器 ").Append(EquipmentCatalog.Name(v.weaponGrade))
          .Append("／防具 ").Append(EquipmentCatalog.Name(v.armorGrade)).Append("</color>");
        string skl = MinionSkill.Label(v.catalogIndex);
        if (!string.IsNullOrEmpty(skl)) sb.Append("\n").Append(skl);
        if (v.kills > 0 || v.deed > 0)
            sb.Append("\n<color=#6f6889>撃破 ").Append(v.kills).Append("　武功 ").Append(v.deed).Append("</color>");
        return sb.ToString();
    }

    /// <summary>選んだ種を1枚に開く。⚠ 毎回作り直す（費用も個体数も動くので差分更新はずれる）。</summary>
    private void RefreshCodexDetail()
    {
        if (codexDetail == null) return;
        for (int i = codexDetail.childCount - 1; i >= 0; i--)
        { var g = codexDetail.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }

        float W = CodexDetailW, pad = 22f;
        if (codexPick < 0 || codexPick >= MinionCatalog.Count)
        {
            var hint = Text(codexDetail, "<color=#6f6889>ツリーの魔物を押すと、ここに詳しい数字と『召喚』が出ます。</color>",
                            14, MUTED, TextAlignmentOptions.TopLeft);
            Place(hint.rectTransform, pad, 20, W - pad * 2, 60);
            return;
        }

        int kk = codexPick;
        var d = MinionCatalog.Get(kk);
        bool unlocked = MinionEvolution.IsUnlocked(kk);
        float yy = 20f;

        // ── 顔（絵）と名前・札 ──
        var frame = Panel(codexDetail, "ArtFrame", C("#15131f"));
        Place(frame.rectTransform, pad, yy, 128, 128); Outline(frame, LINE2);
        var art = new GameObject("Art", typeof(RectTransform)).AddComponent<Image>();
        art.rectTransform.SetParent(frame.rectTransform, false);
        art.raycastTarget = false; art.preserveAspect = true;
        var sp = MinionSprite.ByIndex(kk);
        art.sprite = sp != null ? sp : IconFactory.Get("魔物");
        art.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        Place(art.rectTransform, 8, 8, 112, 112);

        float tx = pad + 128 + 18, tw = W - tx - pad;
        var nm = Text(codexDetail, d.jpName, 26, unlocked ? TEXT : FAINT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        nm.enableWordWrapping = false; nm.enableAutoSizing = true; nm.fontSizeMin = 16; nm.fontSizeMax = 26;
        Place(nm.rectTransform, tx, yy + 6, tw, 34);
        string[] famNames = { "不死", "獣", "魔族" };
        var tags = Text(codexDetail,
            "<color=#9c95b4>" + famNames[Mathf.Clamp((int)d.family, 0, 2) ] + "</color>　"
            + "<color=" + RankHex(d.rank) + ">等級 " + MinionCatalog.RankName(d.rank) + "</color>　"
            + "<color=#9c95b4>" + MinionCatalog.RoleName(d.role) + "　配置 " + d.tierCP + "</color>",
            14, MUTED, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Place(tags.rectTransform, tx, yy + 46, tw, 22);
        if (unlocked)
        {
            int cnt = MinionRoster.CountOfType(kk), top = MinionRoster.TopLevelOfType(kk);
            var own = Text(codexDetail, cnt > 0
                ? "<color=#8cb8e6>個体 " + cnt + " 体　最高 Lv" + top + "</color>"
                : "<color=#6f6889>まだ1体も居ない</color>", 14, MUTED, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(own.rectTransform, tx, yy + 74, tw, 22);
        }
        yy += 146f;

        // ── 説明 ──
        if (!string.IsNullOrEmpty(d.note))
        {
            var nt = Text(codexDetail, d.note, 14, C("#c9c2dc"), TextAlignmentOptions.TopLeft);
            nt.enableWordWrapping = true;
            Place(nt.rectTransform, pad, yy, W - pad * 2, 44); yy += 50f;
        }

        // ── 能力（棒で読む）。⚠ 倍率は 1.0＝家系の標準。上位は 3〜4倍まで伸びるので 4 で満杯
        yy = DetailStat(codexDetail, "体力", d.hpMult, C("#e05a5a"), pad, yy, W);
        yy = DetailStat(codexDetail, "攻撃", d.atkMult, GOLD, pad, yy, W);
        yy = DetailStat(codexDetail, "速さ", d.spdMult, C("#8cb8e6"), pad, yy, W);
        yy += 6f;

        string skl = MinionSkill.Label(kk);
        MagicCatalog.Spell msp;
        if (MagicCatalog.TryPickMinionSpell(kk, out msp))
            skl += "<color=" + msp.colorHex + ">◆" + msp.jpName + "</color>";
        else if (d.style == CharacterVisual.AttackStyle.Cast)
            skl += "<color=#6f6889>・魔法未解禁</color>";
        if (!string.IsNullOrEmpty(skl))
        {
            var sk = Text(codexDetail, skl, 13.5f, TEXT, TextAlignmentOptions.TopLeft);
            sk.enableWordWrapping = true;
            Place(sk.rectTransform, pad, yy, W - pad * 2, 40); yy += 44f;
        }

        // ── 進化先（何に、何をすれば）──
        var kids = MinionEvolution.ChildrenOf(kk);
        if (kids.Count > 0)
        {
            var eh = Text(codexDetail, "進化先", 16, GOLD, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(eh.rectTransform, pad, yy, 200, 22); yy += 28f;
            foreach (var c in kids)
            {
                var cd = MinionCatalog.Get(c);
                bool cu = MinionEvolution.IsUnlocked(c);
                string cond = cu ? "<color=#5cc47c>解禁済み</color>"
                    : MinionEvolution.CanEvolve(c) ? "<color=#e3a94a>進化できる " + MinionEvolution.EvolveCost(c) + " DP</color>"
                    : MinionEvolution.TierResearchNeeded(c) ? "<color=#8cb8e6>研究『" + MinionEvolution.TierResearchName(c) + "』</color>"
                    : "<color=#6f6889>この種の解禁が先</color>";
                var row = Panel(codexDetail, "Evo_" + cd.id, C("#1b1928"));
                Place(row.rectTransform, pad, yy, W - pad * 2, 34); Outline(row, LINE);
                var rl = Text(row.rectTransform, cd.jpName + "　<size=82%><color=" + RankHex(cd.rank) + ">" + MinionCatalog.RankName(cd.rank)
                    + "</color><color=#9c95b4>・" + MinionCatalog.RoleName(cd.role) + "</color></size>", 14, cu ? TEXT : MUTED, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                rl.enableWordWrapping = false;
                Place(rl.rectTransform, 10, 0, (W - pad * 2) * 0.55f, 34);
                var rr = Text(row.rectTransform, cond, 13, MUTED, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                rr.enableWordWrapping = false;
                Place(rr.rectTransform, (W - pad * 2) * 0.55f, 0, (W - pad * 2) * 0.45f - 10, 34);
                int ck = c;
                var rb = row.gameObject.AddComponent<Button>(); rb.targetGraphic = row;
                rb.onClick.AddListener(() => { codexPick = ck; RefreshMinionCodex(); });   // 押すとその種へ
                yy += 40f;
            }
            yy += 6f;
        }

        // ── ここが「押す場所」。⚠ 一覧の上には置かない ──
        float by = Mathf.Max(yy + 8f, codexDetail.rect.height - 60f - 34f);
        if (unlocked)
        {
            int scost = MinionRoster.SummonCost(kk);
            var sumBtn = PrimaryButton(codexDetail, "召喚する（" + scost + " DP）", BLOOD, TEXT, () =>
            {
                // 🧠 研究『見極め』があると、召喚は**気性の2択**になる（→ [[MinionTemperament]]）
                if (MinionTemperament.CanChoose) { OpenTemperChoiceForSummon(kk); return; }
                if (MinionRoster.TrySummon(kk) != null) { RefreshMinionCodex(); RefreshSquadStrip(); }
            }, true);
            Place((RectTransform)sumBtn.transform, pad, by, W - pad * 2, 46);
            // ⚠ 選んだ種は「盤に置く種」でもある（押した瞬間に `SetSelectedMinion` 済み）。押した結果が2つあることを隠さない
            var pick = Text(codexDetail, "<color=#6f6889>下部バーの『部隊』で置くのは、いまこの種です。</color>",
                            12, FAINT, TextAlignmentOptions.TopLeft);
            Place(pick.rectTransform, pad, by + 52, W - pad * 2, 20);
        }
        else if (MinionEvolution.CanEvolve(kk))
        {
            var why = Text(codexDetail, "<color=#e3a94a>◆ " + MinionEvolution.PrereqName(kk) + " から進化できる</color>",
                           14, GOLD, TextAlignmentOptions.TopLeft);
            Place(why.rectTransform, pad, by - 26, W - pad * 2, 22);
            var evoBtn = PrimaryButton(codexDetail, "進化させる（" + MinionEvolution.EvolveCost(kk) + " DP）", BLOOD, TEXT,
                () => { if (MinionEvolution.TryEvolve(kk)) RefreshMinionCodex(); }, true);
            Place((RectTransform)evoBtn.transform, pad, by, W - pad * 2, 46);
        }
        else
        {
            string why = MinionEvolution.TierResearchNeeded(kk)
                ? "<color=#8cb8e6>研究『" + MinionEvolution.TierResearchName(kk) + "』で開く</color>"
                : "<color=#9c95b4>― " + MinionEvolution.PrereqName(kk) + " の解禁が必要</color>";
            var t = Text(codexDetail, why, 14, MUTED, TextAlignmentOptions.TopLeft);
            Place(t.rectTransform, pad, by, W - pad * 2, 40);
        }
    }

    /// <summary>能力1本（名前・棒・倍率）。</summary>
    private float DetailStat(RectTransform parent, string label, float mult, Color col, float x, float y, float W)
    {
        var l = Text(parent, label, 14, MUTED, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Place(l.rectTransform, x, y, 60, 22);
        float bw = W - x * 2 - 60 - 70;
        var track = Panel(parent, "Track_" + label, C("#211f31"));
        Place(track.rectTransform, x + 60, y + 6, bw, 10); Outline(track, LINE);
        var fill = Panel(track, "Fill", col);
        Place(fill.rectTransform, 0, 0, bw * Mathf.Clamp01(mult / 4f), 10);
        var v = Text(parent, "×" + mult.ToString("0.00"), 14, TEXT, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        Place(v.rectTransform, W - x - 66, y, 66, 22);
        return y + 28f;
    }
}
