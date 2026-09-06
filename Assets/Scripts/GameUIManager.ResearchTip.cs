using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 🔍 **研究ノード専用のツールチップ**（K-3）。
///
/// <para>
/// ⚠⚠ **汎用の `AddTooltip` を使ってはいけない。** あれは画面下の **560×30 の固定箱**で、
///   研究の説明（前提の一覧・解放条件・天啓・コスト）を流し込むと**枠からはみ出て読めない**。
///   Civ VII のツールチップは
///     ①中央に名前＋**状態の副題** ②貰える物ごとに1行（絵・名前・産出・条件・配置制約）
///     ③末尾にコストと操作キー
///   という別物なので、専用のパネルを立てる。
/// </para>
///
/// <para>
/// ⚠ 高さは**中身から測って決める**（固定にすると長い説明で必ず溢れる）。
///   TMP の `GetPreferredValues(text, width, 0)` はレイアウト待ちなしで測れる。
/// ⚠ 出る位置はカーソル基準。**画面の外へ出さない**ように必ず内側へ寄せる。
/// </para>
/// 関連: [[GameUIManager.Research]] [[ui-conventions]]。
/// </summary>
public partial class GameUIManager
{
    /// <summary>分野ごとのアイコン。⚠ 絵文字は使わない（フォントに無く黙って消える）。→ [[UIIcons]]</summary>
    private static string ResearchFieldIcon(ResearchField f)
    {
        switch (f)
        {
            case ResearchField.Monster: return "pop";
            case ResearchField.Domain: return "slot";
            case ResearchField.Refine: return "material";
            case ResearchField.DemonLord: return "danger";
            case ResearchField.Magic: return "emotion";
            case ResearchField.Surface: return "influence";
            default: return "research";
        }
    }

    private GameObject researchTipGO;
    private RectTransform researchTipBody;
    private Image researchTipPanel;

    private const float RTIP_W = 452f;
    private const float RTIP_PAD = 14f;
    private const float RTIP_ICON = 38f;

    /// <summary>ツールチップの器を1つだけ作る。⚠ 研究パネルの**中**に置く（前面に出すため）。</summary>
    private void BuildResearchTip(RectTransform root)
    {
        var p = Panel(root, "ResearchTip", C("#141120"));
        researchTipPanel = p;
        Anchor(p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        p.rectTransform.sizeDelta = new Vector2(RTIP_W, 200);
        Outline(p, C("#7d6438"));
        p.raycastTarget = false;
        researchTipBody = NewRect("Body", p.rectTransform);
        Anchor(researchTipBody, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        researchTipBody.anchoredPosition = Vector2.zero;
        researchTipBody.sizeDelta = new Vector2(RTIP_W, 200);
        researchTipGO = p.gameObject;
        researchTipGO.SetActive(false);
    }

    private void HideResearchTip() { if (researchTipGO != null) researchTipGO.SetActive(false); }

    /// <summary>行を1つ積む。返り値は積んだあとの y。</summary>
    private float RTipRow(float y, string icon, string title, string yieldLabel, string yieldValue,
        string[] bullets, string effect, string requirement, Color accent)
    {
        float bodyW = RTIP_W - RTIP_PAD * 2f;
        float textX = RTIP_PAD + (icon != null ? RTIP_ICON + 11f : 0f);
        float textW = RTIP_W - textX - RTIP_PAD;

        float h = 0f;
        // 中身の高さを先に測る
        float titleH = string.IsNullOrEmpty(title) ? 0f : 20f;
        float yieldH = string.IsNullOrEmpty(yieldLabel) ? 0f : 20f;
        float bulletH = 0f;
        if (bullets != null) bulletH = bullets.Length * 17f;
        float effH = 0f, reqH = 0f;
        var probe = researchTipBody.GetComponentInChildren<TextMeshProUGUI>(true);
        if (!string.IsNullOrEmpty(effect)) effH = MeasureText(effect, textW, 12.5f) + 2f;
        if (!string.IsNullOrEmpty(requirement)) reqH = MeasureText(requirement, textW, 12.5f) + 2f;
        h = 10f + titleH + yieldH + bulletH + effH + reqH + 10f;
        if (icon != null && h < RTIP_ICON + 20f) h = RTIP_ICON + 20f;

        // 絵
        if (icon != null)
        {
            var ic = Panel(researchTipBody, "ic", C("#221c14"));
            Place(ic.rectTransform, RTIP_PAD, y + 10f, RTIP_ICON, RTIP_ICON);
            Outline(ic, C("#5c4d30"));
            var sp = UIIcons.Get(icon);
            if (sp != null)
            {
                var im = Panel(ic.rectTransform, "s", UIIcons.IsArt(icon) ? Color.white : accent);
                im.sprite = sp; im.type = Image.Type.Simple; im.preserveAspect = true; im.raycastTarget = false;
                Place(im.rectTransform, 7, 7, 24, 24);
            }
        }

        float ty = y + 8f;
        if (!string.IsNullOrEmpty(title))
        {
            var t = Text(researchTipBody, title, 13.5f, TEXT, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
            Place(t.rectTransform, textX, ty, textW, 20); ty += titleH;
        }
        if (!string.IsNullOrEmpty(yieldLabel))
        {
            // 産出の行：ラベルは左、値は右。あいだに点線の罫（Civと同じ作り）
            var l = Text(researchTipBody, yieldLabel, 12f, FAINT, TextAlignmentOptions.TopLeft);
            Place(l.rectTransform, textX, ty + 1, textW - 90, 18);
            var v = Text(researchTipBody, yieldValue, 12.5f, TEXT, TextAlignmentOptions.TopRight, FontStyles.Bold);
            Place(v.rectTransform, textX + textW - 90, ty + 1, 90, 18);
            LineRect(researchTipBody, textX, ty + 18, textW, 1, C("#2a2540"));
            ty += yieldH;
        }
        if (bullets != null)
        {
            for (int i = 0; i < bullets.Length; i++)
            {
                var b = Text(researchTipBody, bullets[i], 12f, MUTED, TextAlignmentOptions.TopLeft);
                b.enableWordWrapping = false; b.overflowMode = TextOverflowModes.Ellipsis;
                Place(b.rectTransform, textX + 4, ty, textW - 4, 17);
                ty += 17f;
            }
        }
        if (!string.IsNullOrEmpty(effect))
        {
            var e = Text(researchTipBody, effect, 12.5f, MUTED, TextAlignmentOptions.TopLeft);
            e.enableWordWrapping = true;
            Place(e.rectTransform, textX, ty, textW, effH); ty += effH;
        }
        if (!string.IsNullOrEmpty(requirement))
        {
            var r = Text(researchTipBody, requirement, 12.5f, C("#e0a33a"), TextAlignmentOptions.TopLeft);
            r.enableWordWrapping = true;
            Place(r.rectTransform, textX, ty, textW, reqH); ty += reqH;
        }
        // 仕切り
        LineRect(researchTipBody, 0, y + h, RTIP_W, 1, C("#332e49"));
        return y + h;
    }

    /// <summary>⚠ レイアウト待ちなしで文字の高さを測る（測らないと必ず溢れる）。</summary>
    private float MeasureText(string s, float w, float size)
    {
        if (rtipProbe == null)
        {
            rtipProbe = Text(researchTipBody, "", size, TEXT, TextAlignmentOptions.TopLeft);
            rtipProbe.gameObject.SetActive(false);
        }
        rtipProbe.fontSize = size;
        rtipProbe.enableWordWrapping = true;
        var v = rtipProbe.GetPreferredValues(s, w, 0f);
        return Mathf.Max(size + 6f, v.y + 4f);
    }
    private TextMeshProUGUI rtipProbe;

    /// <summary>
    /// 🔍 研究ノードのツールチップを出す。⚠ 毎回作り直す（中身がノードごとに違うので使い回せない）。
    /// </summary>
    private void ShowResearchTip(ResearchNode n)
    {
        if (researchTipGO == null || researchTipBody == null) return;
        for (int i = researchTipBody.childCount - 1; i >= 0; i--)
        { var g = researchTipBody.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        rtipProbe = null;

        bool done = ResearchState.IsResearched(n.id);
        bool prereqOK = ResearchState.PrereqMet(n);
        bool eraOK = ResearchState.EraMet(n);
        bool gateOK = ResearchState.GateMet(n);
        bool can = ResearchState.CanResearch(n.id);
        bool sealed_ = ResearchState.ExclusiveBlocked(n);

        string sub = sealed_ ? "封印された研究"
            : done ? "研究完了"
            : can ? "いま研究できる"
            : !eraOK ? EraSystem.EraName(n.era) + "から"
            : !prereqOK ? "前提が足りない"
            : !gateOK ? "解放条件が足りない" : "未解除の研究";

        // ── 見出し（名前＋状態の副題）──
        float y = 0f;
        var nm = Text(researchTipBody, n.jpName, 15.5f, TEXT, TextAlignmentOptions.Top, FontStyles.Bold);
        Place(nm.rectTransform, RTIP_PAD, y + 11, RTIP_W - RTIP_PAD * 2, 22);
        var sb2 = Text(researchTipBody, sub, 11.5f,
            sealed_ ? C("#a05a70") : done ? C("#cbbfa0") : can ? GOLD : FAINT, TextAlignmentOptions.Top);
        Place(sb2.rectTransform, RTIP_PAD, y + 32, RTIP_W - RTIP_PAD * 2, 18);
        LineRect(researchTipBody, 0, y + 54, RTIP_W, 1, C("#332e49"));
        y += 55f;

        // ── ① 中身（いまは説明文。K-3後半で「貰える物」の行になる）──
        string effLine = null;
        if (n.effect != ResEffect.None && Mathf.Abs(n.amount) > 0.0001f)
            effLine = ResearchState.EffectName(n.effect) + "　" + (n.amount > 0 ? "+" : "")
                    + Mathf.RoundToInt(n.amount * 100f) + "%";
        y = RTipRow(y, ResearchFieldIcon(n.field), ResearchCatalog.FieldName(n.field) + "・第" + n.tier + "段",
            effLine != null ? ResearchState.EffectName(n.effect) : null,
            effLine != null ? (n.amount > 0 ? "+" : "") + Mathf.RoundToInt(n.amount * 100f) + "%" : null,
            null, n.desc, null, GOLD);

        // ── ② 排他 ──
        if (sealed_)
            y = RTipRow(y, "danger", "封印された", null, null, null,
                "『" + ResearchState.ExclusiveChosenName(n.exclusive) + "』を選んだので、この道は永久に閉じている。",
                null, C("#a05a70"));
        else if (!string.IsNullOrEmpty(n.exclusive))
            y = RTipRow(y, "danger", "排他の刻印", null, null, null,
                "これを取ると、同じ刻印の他の道は永久に閉じる。", null, C("#d47a7a"));

        // ── ③ 前提／時代／解放条件 ──
        if (!eraOK)
            y = RTipRow(y, "world", "まだ来ていない時代", null, null, null,
                EraSystem.EraName(n.era) + "に入るまで研究できない。", null, C("#c9a8ff"));
        else if (!prereqOK && n.prereq != null && n.prereq.Length > 0)
        {
            var bl = new List<string>();
            foreach (var pid in n.prereq)
            {
                ResearchNode pn;
                if (!ResearchCatalog.TryGet(pid, out pn)) continue;
                bool ok = ResearchState.IsResearched(pid);
                bl.Add(ok ? "<color=#5cc47c>✔ " + pn.jpName + "</color>"
                          : "<color=#e0a45a>・ " + pn.jpName + "</color>");
            }
            y = RTipRow(y, "slot", "要る前提", null, null, bl.ToArray(), null, null, MUTED);
        }
        else if (!gateOK)
            y = RTipRow(y, "threat", "解放条件", null, null, null, null,
                ResearchState.GateText(n), C("#e0a45a"));

        // ── ④ 天啓（⚠ いままで画面に一度も出ていなかった）──
        if (!string.IsNullOrEmpty(n.eureka))
        {
            bool got = EurekaTracker.Has(n.id);
            y = RTipRow(y, "research", got ? "天啓を得ている" : "天啓", null, null, null,
                got ? "コストが 40% 引きになっている。"
                    : n.eureka + "　<color=#6f6889>達成するとコストが40%引きになる</color>",
                null, C("#e0b23a"));
        }

        // ── ⑤ 末尾：コストと所要ターン ──
        if (!done)
        {
            int cost = n.repeatable ? ResearchState.RepeatCost(n) : ResearchState.EffectiveCost(n);
            int turns = ResearchTurnsFor(n);
            var pill = Panel(researchTipBody, "cost", C("#0e0c15"));
            float pw = 232f;
            Place(pill.rectTransform, (RTIP_W - pw) / 2f, y + 12, pw, 28);
            Outline(pill, C("#4a4268"));
            // ⚠ 押せないノードに「今すぐ」と出さない（時代や前提で止まっているのに研究できるように見える）。
            string when = !can ? ""
                : turns == 0 ? "　<color=#5cc47c>今すぐ</color>"
                : turns > 0 ? "　<color=#d8c8a0>約" + turns + "T</color>" : "";
            var ct = Text(pill.rectTransform,
                "<color=#8cb8e6><b>コスト " + cost + "</b></color> <size=88%><color=#9c95b4>研究点</color></size>" + when,
                12.5f, TEXT, TextAlignmentOptions.Center);
            StretchFull(ct.rectTransform);
            var have = Text(researchTipBody, "所持 " + ResearchState.RP + " 研究点", 11f, FAINT, TextAlignmentOptions.Top);
            Place(have.rectTransform, RTIP_PAD, y + 42, RTIP_W - RTIP_PAD * 2, 16);
            y += 64f;
        }
        else y += 8f;

        // ── 高さを確定してから、画面の内側に置く ──
        researchTipPanel.rectTransform.sizeDelta = new Vector2(RTIP_W, y);
        researchTipBody.sizeDelta = new Vector2(RTIP_W, y);
        researchTipGO.SetActive(true);
        researchTipGO.transform.SetAsLastSibling();
        PlaceResearchTipAtCursor(y);
    }

    /// <summary>⚠ カーソルの右下に出し、**画面の外へ出さない**（はみ出したら反対側へ寄せる）。</summary>
    private void PlaceResearchTipAtCursor(float h)
    {
        var parent = researchTipPanel.rectTransform.parent as RectTransform;
        if (parent == null) return;
        Vector2 local;
        var cv = researchTipPanel.GetComponentInParent<Canvas>();
        var cam = cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay ? cv.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, PointerInput.Position, cam, out local))
            return;
        // 親は左上原点（Place と同じ座標系）に合わせる
        float px = local.x - parent.rect.xMin + 18f;
        float py = -(local.y - parent.rect.yMax) + 18f;
        px = Mathf.Clamp(px, 6f, Mathf.Max(6f, parent.rect.width - RTIP_W - 6f));
        py = Mathf.Clamp(py, 6f, Mathf.Max(6f, parent.rect.height - h - 6f));
        Place(researchTipPanel.rectTransform, px, py, RTIP_W, h);
    }
}
