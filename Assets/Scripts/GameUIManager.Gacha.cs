using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🎰 召喚の儀の**結果画面**（D-3）。引いた個体が1枚ずつめくれて出る。
///
/// <para>
/// ⚠⚠ **なぜ要るか（実測の裏付け）**：通しプレイ T1〜T30 で、ガチャを引いても
///   起きるのは「行の文字が書き換わる」だけだった。**払った瞬間と得た瞬間が同じ**なので、
///   引いた記憶がまったく残らない。ガチャの快感は結果そのものではなく、
///   **結果が出るまでの間**と**出た瞬間の差**にある。
/// </para>
///
/// <para>
/// ⚠ 確率・値段・排出内容には**一切触らない**。ここは見せ方だけ（→ [[SummonGacha]]）。
/// ⚠ 時間は `unscaledDeltaTime`（UIの演出なので、倍速や一時停止に引きずられない）
///   → [[ui-conventions]]。
/// </para>
/// </summary>
public partial class GameUIManager
{
    private GameObject gachaPanel;
    private RectTransform gachaCards;
    private Image gachaCircle;
    private TextMeshProUGUI gachaTitle;
    private Coroutine gachaCo;
    private readonly List<Image> gachaCardImgs = new List<Image>();
    private readonly List<bool> gachaCardUnique = new List<bool>();

    private const float GACHA_W = 980f, GACHA_H = 560f;

    /// <summary>結果画面を出す。`rs` が null なら直前の1回ぶんを出す。</summary>
    private void ShowGachaResult(List<SummonGacha.Result> rs)
    {
        if (rs == null)
        {
            rs = new List<SummonGacha.Result>();
            if (!string.IsNullOrEmpty(SummonGacha.LastResult))
                rs.Add(new SummonGacha.Result { name = SummonGacha.LastResult, unique = SummonGacha.LastWasUnique, individualId = SummonGacha.LastIndividualId });
        }
        if (rs.Count == 0) return;
        CloseGuide();          // ⚠ 報告パネルが開いたままだと結果がその下に潜る
        EnsureGachaPanel();
        gachaPanel.SetActive(true);
        gachaPanel.transform.SetAsLastSibling();

        int uq = 0; for (int i = 0; i < rs.Count; i++) if (rs[i].unique) uq++;
        SetTxt(gachaTitle, "召喚の儀　<size=76%><color=#9c95b4>" + rs.Count + " 体が応えた"
            + (uq > 0 ? "　<color=#ffd24a>うちユニーク " + uq + " 体</color>" : "") + "</color></size>");

        // 前回のカードを片付ける
        for (int i = gachaCards.childCount - 1; i >= 0; i--) Destroy(gachaCards.GetChild(i).gameObject);
        gachaCardImgs.Clear(); gachaCardUnique.Clear();

        // 5列×2段。⚠ 1枚のときも同じ大きさで出す（枚数で見え方を変えない）
        const float cw = 158f, chh = 196f, gx = 12f, gy = 14f;
        int cols = Mathf.Min(5, rs.Count);
        int rows = Mathf.CeilToInt(rs.Count / 5f);
        float totalW = cols * cw + (cols - 1) * gx;
        float totalH = rows * chh + (rows - 1) * gy;
        float x0 = (GACHA_W - totalW) * 0.5f;
        float y0 = 96f + Mathf.Max(0f, (GACHA_H - 150f - totalH) * 0.5f);

        for (int i = 0; i < rs.Count; i++)
        {
            int col = i % 5, row = i / 5;
            var r = rs[i];
            var card = Panel(gachaCards, "GCard" + i, r.unique ? C("#3b2d16") : CARD);
            Place(card.rectTransform, x0 + col * (cw + gx), y0 + row * (chh + gy), cw, chh);
            Outline(card, r.unique ? GOLD : LINE);

            if (r.unique)
            {
                // ✨ 光条は**顔の後ろだけ**に置く（名前の上に被せると金の字が金に埋もれる）
                var rays = Panel(card.rectTransform, "rays", new Color(1f, 0.85f, 0.45f, 0.22f));
                rays.sprite = Resources.Load<Sprite>("Fx/burst_rays");
                rays.type = Image.Type.Simple; rays.preserveAspect = true; rays.raycastTarget = false;
                Place(rays.rectTransform, 2, 20, cw - 4, cw - 4);
                rays.transform.SetAsFirstSibling();
            }

            var tag = Text(card.rectTransform, r.unique ? "ユニーク" : "配下", 11,
                r.unique ? GOLD : MUTED, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(tag.rectTransform, 6, 10, cw - 12, 14);

            // 🎨 上半分は**役割の色**で塗り、中央に等級を大きく置く。
            //    ⚠ ここが空白だと「10枚めくったのに全部同じ黒い板」に見えて、引いた実感が消える。
            // ⚠ `MinionDef` は構造体なので `?:` で null と混ぜられない。有無は個体の側で持つ。
            var v0 = MinionRoster.Get(r.individualId);
            bool hasDef = v0 != null;
            var d0 = MinionCatalog.Get(hasDef ? v0.catalogIndex : 0);
            var rc = hasDef ? RoleColor(d0.role) : GOLD;
            // ⚠ **透明度で薄めない。** `Panel` の下地は暗いので、アルファを下げても色は沈まず
            //   ベタ塗りに見えた。暗い下地との**混色**で作ると狙った濃さになる。
            var face = Panel(card.rectTransform, "face", Color.Lerp(C("#141120"), rc, r.unique ? 0.34f : 0.22f));
            Place(face.rectTransform, 10, 28, cw - 20, chh - 100);
            Outline(face, Color.Lerp(C("#141120"), rc, 0.62f));
            // 等級は**face と同じ色で書かない**（同色は読めない）。明るく抜く。
            var grade = Text(face.rectTransform, hasDef ? MinionCatalog.RankName(d0.rank) : "★", 46,
                Color.Lerp(rc, Color.white, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
            StretchFull(grade.rectTransform);
            if (hasDef)
            {
                var role = Text(card.rectTransform, MinionCatalog.RoleName(d0.role), 10.5f, rc, TextAlignmentOptions.Center);
                Place(role.rectTransform, 8, chh - 96, cw - 16, 14);
            }

            var nm = Text(card.rectTransform, r.name, 15, r.unique ? GOLD : TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
            nm.enableAutoSizing = true; nm.fontSizeMin = 10f; nm.fontSizeMax = 15f;
            Place(nm.rectTransform, 8, chh - 62, cw - 16, 38);

            var lv = Text(card.rectTransform, "#" + r.individualId + "　Lv" + MinionRoster.LevelOf(r.individualId),
                11, FAINT, TextAlignmentOptions.Center);
            Place(lv.rectTransform, 8, chh - 24, cw - 16, 14);

            card.transform.localScale = Vector3.zero;   // ⚠ めくる演出のため最初は畳んでおく
            gachaCardImgs.Add(card); gachaCardUnique.Add(r.unique);
        }

        if (gachaCo != null) StopCoroutine(gachaCo);
        gachaCo = StartCoroutine(RevealGachaCards(uq));
    }

    /// <summary>1枚ずつ順にめくる。⚠ ユニークだけ**一拍おいて**開く（そこが山になる）。</summary>
    private IEnumerator RevealGachaCards(int uniqueCount)
    {
        float t = 0f;
        while (t < 0.18f) { t += Time.unscaledDeltaTime; if (gachaCircle != null) gachaCircle.transform.Rotate(0, 0, 220f * Time.unscaledDeltaTime); yield return null; }

        for (int i = 0; i < gachaCardImgs.Count; i++)
        {
            var card = gachaCardImgs[i];
            if (card == null) continue;
            bool gold = i < gachaCardUnique.Count && gachaCardUnique[i];
            if (gold) { yield return WaitU(0.22f); SoundSystem.Play(SoundSystem.Sfx.Discover, 1f, 0.85f); }
            else SoundSystem.Play(SoundSystem.Sfx.Click, 0.5f, 1.25f);

            float k = 0f;
            while (k < 1f)
            {
                k += Time.unscaledDeltaTime * 7f;
                float s = k < 0.7f ? Mathf.Lerp(0f, 1.10f, k / 0.7f) : Mathf.Lerp(1.10f, 1f, (k - 0.7f) / 0.3f);
                if (card != null) card.transform.localScale = Vector3.one * s;
                if (gachaCircle != null) gachaCircle.transform.Rotate(0, 0, 120f * Time.unscaledDeltaTime);
                yield return null;
            }
            if (card != null) card.transform.localScale = Vector3.one;
            yield return WaitU(0.06f);
        }
        gachaCo = null;
    }

    private IEnumerator WaitU(float sec)
    {
        float t = 0f;
        while (t < sec) { t += Time.unscaledDeltaTime; if (gachaCircle != null) gachaCircle.transform.Rotate(0, 0, 120f * Time.unscaledDeltaTime); yield return null; }
    }

    private void EnsureGachaPanel()
    {
        if (gachaPanel != null) return;
        // ⚠ 図鑑と同じCanvasに建てる。別Canvasにすると図鑑の下に潜る。
        var parent = minionPanel != null ? minionPanel.transform.parent as RectTransform : null;
        if (parent == null) return;

        var panel = Panel(parent, "GachaResult", PANEL);
        gachaPanel = panel.gameObject;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(GACHA_W, GACHA_H);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        Outline(panel, GOLD); SkinPanel(panel);

        // 🔮 背景で回る魔法陣（PixelLab生成 → [[pixellab-pipeline]]）
        gachaCircle = Panel(panel.rectTransform, "circle", new Color(1f, 1f, 1f, 0.30f));
        gachaCircle.sprite = Resources.Load<Sprite>("Fx/summon_circle");
        gachaCircle.type = Image.Type.Simple; gachaCircle.preserveAspect = true; gachaCircle.raycastTarget = false;
        Place(gachaCircle.rectTransform, (GACHA_W - 460f) * 0.5f, (GACHA_H - 460f) * 0.5f, 460, 460);

        gachaTitle = Text(panel, "召喚の儀", 20, GOLD, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(gachaTitle.rectTransform, 26, 20, GACHA_W - 120, 26);

        var close = PrimaryButton(panel, "×", PANEL2, TEXT, () => gachaPanel.SetActive(false));
        Place((RectTransform)close.transform, GACHA_W - 58, 18, 32, 30);

        gachaCards = NewRect("Cards", panel.rectTransform);
        StretchFull(gachaCards);

        var hint = Text(panel, "引いた個体は『個体』タブから隊に入れられます。", 11.5f, FAINT, TextAlignmentOptions.Center);
        Place(hint.rectTransform, 26, GACHA_H - 34, GACHA_W - 52, 18);

        gachaPanel.SetActive(false);
    }
}
