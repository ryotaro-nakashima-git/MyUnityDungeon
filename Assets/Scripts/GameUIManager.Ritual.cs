using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ✦🛒 **召喚の儀と行商人の専用画面**（UI刷新 B-5）。
///
/// <para>
/// ⚠⚠ **なぜ独立させたか**：どちらも独立した入手経路なのに、
///   『魔物』→『個体』タブの中の小さな箱に畳まれていた。ガチャは行が1つ書き換わるだけ、
///   店は3行のリスト ―― <b>「そこへ行った」感じがまったく無い</b>。
///   2つとも**場所**なので、場所として作る（背景があり、そこに物が並んでいる）。
/// </para>
///
/// <para>
/// ⚠ **確率・値段・排出内容には一切触らない。**ここは見せ方だけ
///   （→ [[SummonGacha]] [[MerchantShop]]）。
/// ⚠ 背景は PixelLab の1枚絵。**中央と棚には何も描かれていない**ので、
///   回る陣も品物もこちらが上に重ねる（→ [[SceneArt]]）。
/// ⚠ 時間は `unscaledDeltaTime`（UIの演出なので倍速や一時停止に引きずられない）
///   → [[ui-conventions]]。
/// </para>
/// </summary>
public partial class GameUIManager
{
    // ══ ✦ 召喚の儀 ══
    private GameObject ritualPanel;
    private RectTransform ritualRing, ritualRing2;
    private TextMeshProUGUI ritualOdds, ritualPity, ritualLast, ritualCollect;
    private Image ritualPityFill, ritualGlow;
    private Button ritualOne, ritualTen;
    private const float RIT_W = 720f, RIT_H = 540f;

    // ══ 🛒 行商人 ══
    private GameObject shopPanel;
    private RectTransform shopShelf;
    private TextMeshProUGUI shopSub, shopFootL, shopFootR;
    private const float SHOP_W = 860f, SHOP_H = 520f;

    // ─────────────────────────── 召喚の儀 ───────────────────────────

    private void BuildRitualPanel(RectTransform root)
    {
        var panel = Panel(root, "RitualPanel", C("#0b0813"));
        Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(RIT_W, RIT_H);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        Outline(panel, GOLD);
        ritualPanel = panel.gameObject;

        // 🖼️ 祭壇の背景（中央は暗く空けてある）
        var bg = Panel(panel.rectTransform, "bg", Color.white);
        bg.sprite = SceneArt.Ritual; bg.type = Image.Type.Simple;
        bg.preserveAspect = false; bg.raycastTarget = false;
        StretchFull(bg.rectTransform);
        if (bg.sprite == null) bg.color = C("#140f24");

        var title = Text(panel.rectTransform, "召 喚 の 儀", 21f, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(title.rectTransform, 0, 14, RIT_W, 28);
        var sub = Text(panel.rectTransform, "<color=#9c95b4>呼べるものは選べない。応えたものが、そこに立つ。</color>",
            11.5f, MUTED, TextAlignmentOptions.Center);
        Place(sub.rectTransform, 0, 42, RIT_W, 18);

        // ✦ 回る陣。⚠ 絵は1枚しか無いので、**2枚を逆回しで重ねて**動きを作る。
        float cd = 230f, cx = (RIT_W - cd) * 0.5f, cy = 74f;
        float ccy = cy + cd * 0.5f;                      // 陣の中心のy（文字をここに集める）
        ritualGlow = Panel(panel.rectTransform, "glow", new Color(0.89f, 0.76f, 0.29f, 0.16f));
        ritualGlow.sprite = Resources.Load<Sprite>("Fx/burst_rays");
        ritualGlow.preserveAspect = true; ritualGlow.raycastTarget = false;
        Place(ritualGlow.rectTransform, cx - 30, cy - 30, cd + 60, cd + 60);

        ritualRing = MakeCircle(panel.rectTransform, "ring1", cx, cy, cd, new Color(0.89f, 0.76f, 0.29f, 0.85f));
        ritualRing2 = MakeCircle(panel.rectTransform, "ring2", cx + 36, cy + 36, cd - 72, new Color(0.71f, 0.47f, 0.90f, 0.55f));

        // 中央：いまのユニーク確率と天井。⚠ 陣の**中心**に積む（陣の下端ではない）
        // ⚠⚠ **陣の模様の上に直に文字を置かない。**回る絵が背景なので、線と字が噛み合って
        //   数字が読めなくなる（実測：「基礎 6.0%」とゲージが完全に埋もれた）。暗い下敷きを1枚挟む。
        var plate = Panel(panel.rectTransform, "plate", new Color(0.03f, 0.02f, 0.06f, 0.72f));
        Place(plate.rectTransform, cx + 18, ccy - 46, cd - 36, 92);
        plate.raycastTarget = false;
        var k = Text(panel.rectTransform, "<color=#9c95b4>ユニークが応える確率</color>", 10.5f, MUTED, TextAlignmentOptions.Center);
        Place(k.rectTransform, cx, ccy - 40, cd, 16);
        ritualOdds = Text(panel.rectTransform, "―", 30f, C("#ffd24a"), TextAlignmentOptions.Center, FontStyles.Bold);
        Place(ritualOdds.rectTransform, cx, ccy - 24, cd, 38);
        ritualPity = Text(panel.rectTransform, "", 10.5f, MUTED, TextAlignmentOptions.Center);
        Place(ritualPity.rectTransform, cx, ccy + 16, cd, 16);
        var pbar = Panel(panel.rectTransform, "pbar", C("#251f38"));
        Place(pbar.rectTransform, cx + (cd - 120) * 0.5f, ccy + 34, 120, 5);
        Outline(pbar, LINE);
        ritualPityFill = Panel(pbar, "fill", C("#ffd24a"));
        Place(ritualPityFill.rectTransform, 0, 0, 0, 5);

        // 大ボタン2つ
        float bw = 218f, bh = 54f, gap = 16f;
        float bx = (RIT_W - bw * 2 - gap) * 0.5f, by = 344f;
        ritualOne = PrimaryButton(panel, "", C("#2a2038"), GOLD, () => RollRitual(false), true);
        Place((RectTransform)ritualOne.transform, bx, by, bw, bh);
        ritualTen = PrimaryButton(panel, "", C("#2e2044"), C("#c9a0f0"), () => RollRitual(true), true);
        Place((RectTransform)ritualTen.transform, bx + bw + gap, by, bw, bh);

        // ⚠ 足元の2行は**閉じるボタンより上**に置く（右下で重なっていた）。
        //   ⚠ 床の絵の上なので、ここにも暗い帯を敷く（敷かないと薄くて読めない）。
        var foot = Panel(panel.rectTransform, "footBar", new Color(0.03f, 0.02f, 0.06f, 0.62f));
        Place(foot.rectTransform, 12, 418, RIT_W - 24, 48);
        foot.raycastTarget = false;
        ritualLast = Text(panel.rectTransform, "", 11f, MUTED, TextAlignmentOptions.Left);
        Place(ritualLast.rectTransform, 24, 424, RIT_W - 48, 18);
        ritualCollect = Text(panel.rectTransform, "", 11f, MUTED, TextAlignmentOptions.Right);
        Place(ritualCollect.rectTransform, 24, 444, RIT_W - 48, 18);

        var close = PrimaryButton(panel, "閉じる", PANEL2, MUTED, () => OpenExclusive(null));
        Place((RectTransform)close.transform, RIT_W - 108, RIT_H - 44, 92, 30);

        ritualPanel.SetActive(false);
    }

    /// <summary>
    /// ✦ 円環を1つ作る（`Fx/summon_circle`）。
    /// ⚠⚠ **回すのは「入れ物の中で伸ばした子」の方。**`Place` は左上を原点に置くので、
    ///   置いた矩形をそのまま `Rotate` すると**角を軸に公転**して画面の端まで飛んでいく（実測）。
    ///   入れ物を `Place` で置き、中の子を `StretchFull`（中心ピボット）にして、その子を回す。
    /// </summary>
    private RectTransform MakeCircle(RectTransform parent, string name, float x, float y, float d, Color tint)
    {
        var holder = NewRect(name + "_h", parent);
        Place(holder, x, y, d, d);
        var im = Panel(holder, name, tint);
        im.sprite = Resources.Load<Sprite>("Fx/summon_circle");
        im.type = Image.Type.Simple; im.preserveAspect = true; im.raycastTarget = false;
        StretchFull(im.rectTransform);
        im.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        return im.rectTransform;
    }

    /// <summary>✦ 毎フレーム：陣を回し、数字を今の値に合わせる。⚠ 開いているときだけ。</summary>
    private void TickRitual()
    {
        if (ritualPanel == null || !ritualPanel.activeSelf) return;
        float dt = Time.unscaledDeltaTime;
        if (ritualRing != null) ritualRing.Rotate(0f, 0f, -13f * dt);
        if (ritualRing2 != null) ritualRing2.Rotate(0f, 0f, 21f * dt);
        if (ritualGlow != null)
        {
            var c = ritualGlow.color;
            c.a = 0.10f + 0.09f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.9f));
            ritualGlow.color = c;
        }
        RefreshRitual();
    }

    private void RefreshRitual()
    {
        if (ritualPanel == null) return;
        float ch = SummonGacha.CurrentUniqueChance;
        SetTxt(ritualOdds, (ch * 100f).ToString("0.0") + "%");
        SetTxt(ritualPity, "基礎 " + (SummonGacha.UniqueChance * 100f).ToString("0.0") + "%"
            + (SummonGacha.MissStreak > 0 ? "　＋ 外し " + SummonGacha.MissStreak + " 回" : ""));
        if (ritualPityFill != null)
        {
            // 天井は 50% で頭打ち（`CurrentUniqueChance`）。そこまでの道のりを見せる
            float t = Mathf.InverseLerp(SummonGacha.UniqueChance, 0.5f, ch);
            Place(ritualPityFill.rectTransform, 0, 0, 120f * Mathf.Clamp01(t), 5);
        }

        int cost = SummonGacha.Cost;
        string why; bool ok = SummonGacha.CanRoll(out why);
        SetRitualBtn(ritualOne, "儀を執り行う", cost, ok, why);
        SetRitualBtn(ritualTen, "十度 重ねる", cost * 10, ok, why);

        SetTxt(ritualLast, string.IsNullOrEmpty(SummonGacha.LastResult)
            ? "<color=#6f6889>まだ誰も応えていない</color>"
            : "直前に応えたもの ― " + (SummonGacha.LastWasUnique ? "<color=#ffd24a>" : "<color=#8cb8e6>")
              + SummonGacha.LastResult + "</color>");
        int have = 0;
        for (int i = 0; i < UniqueCatalog.Count; i++)
            if (!UniqueCatalog.IsTurncoat(i) && MinionRoster.CountOfType(UniqueCatalog.GlobalOf(i)) > 0) have++;
        int rollable = 0;
        for (int i = 0; i < UniqueCatalog.Count; i++) if (!UniqueCatalog.IsTurncoat(i)) rollable++;
        SetTxt(ritualCollect, "引き当てたユニーク <color=#ffd24a>" + have + "</color> / " + rollable + " 種");
    }

    private void SetRitualBtn(Button b, string label, int cost, bool ok, string why)
    {
        if (b == null) return;
        var t = b.GetComponentInChildren<TextMeshProUGUI>();
        if (t == null) return;
        t.enableWordWrapping = false;
        t.fontSize = 16f;
        SetTxt(t, label + "\n<size=68%><color=" + (ok ? "#9c95b4" : "#7a4a4a") + ">"
            + (ok ? cost.ToString("#,0") + " DP" : why) + "</color></size>");
        b.interactable = ok;
    }

    /// <summary>✦ 引く。⚠ 演出と結果の担当は `ShowGachaResult`（D-3）に任せる。</summary>
    private void RollRitual(bool ten)
    {
        if (ten)
        {
            var rs = SummonGacha.TryRollTen();
            if (rs.Count > 0) { ShowGachaResult(rs); AfterRitual(); }
        }
        else if (SummonGacha.TryRoll()) { ShowGachaResult(null); AfterRitual(); }
    }
    private void AfterRitual()
    {
        RefreshRitual();
        RefreshMinionCodex();
        RefreshSpecialStrip();
    }

    // ─────────────────────────── 行商人 ───────────────────────────

    private void BuildShopPanel(RectTransform root)
    {
        var panel = Panel(root, "ShopPanel", C("#0b0813"));
        Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(SHOP_W, SHOP_H);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        Outline(panel, C("#d9a05b"));
        shopPanel = panel.gameObject;

        // 🖼️ 露店の背景（棚は空で描いてある）
        var bg = Panel(panel.rectTransform, "bg", Color.white);
        bg.sprite = SceneArt.Merchant; bg.type = Image.Type.Simple;
        bg.preserveAspect = false; bg.raycastTarget = false;
        StretchFull(bg.rectTransform);
        if (bg.sprite == null) bg.color = C("#1d1310");
        // ⚠ 背景の上に品物を重ねるので、**少し暗幕を敷く**（そのままだと文字が読めない）
        var veil = Panel(panel.rectTransform, "veil", new Color(0f, 0f, 0f, 0.30f));
        veil.raycastTarget = false; StretchFull(veil.rectTransform);

        var title = Text(panel.rectTransform, "行 商 人", 20f, C("#d9a05b"), TextAlignmentOptions.Center, FontStyles.Bold);
        Place(title.rectTransform, 0, 14, SHOP_W, 26);
        shopSub = Text(panel.rectTransform, "", 11.5f, MUTED, TextAlignmentOptions.Center);
        Place(shopSub.rectTransform, 0, 40, SHOP_W, 18);

        shopShelf = NewRect("Shelf", panel.rectTransform);
        Place(shopShelf, 0, 70, SHOP_W, 340);

        shopFootL = Text(panel.rectTransform, "", 11f, FAINT, TextAlignmentOptions.Left);
        Place(shopFootL.rectTransform, 22, SHOP_H - 34, SHOP_W * 0.6f, 18);
        shopFootR = Text(panel.rectTransform, "", 11f, FAINT, TextAlignmentOptions.Right);
        Place(shopFootR.rectTransform, SHOP_W * 0.4f - 118, SHOP_H - 34, SHOP_W * 0.6f, 18);

        var close = PrimaryButton(panel, "閉じる", PANEL2, MUTED, () => OpenExclusive(null));
        Place((RectTransform)close.transform, SHOP_W - 108, SHOP_H - 40, 92, 28);

        shopPanel.SetActive(false);
        RefreshShopPanel();
    }

    /// <summary>🛒 棚を組み直す。⚠ 開くときと買ったときだけ（毎フレーム作り直さない）。</summary>
    private void RefreshShopPanel()
    {
        if (shopPanel == null || shopShelf == null) return;
        for (int i = shopShelf.childCount - 1; i >= 0; i--)
        { var g = shopShelf.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }

        int slots = MerchantShop.Slots;
        float cw = 188f, chh = 300f, gap = 16f;
        float total = slots * cw + (slots - 1) * gap;
        float x0 = (SHOP_W - total) * 0.5f;

        for (int i = 0; i < slots; i++)
        {
            int si = i;
            int item = MerchantShop.SlotItem(i);
            bool sold = item < 0;
            var card = Panel(shopShelf, "Shelf" + i, C("#1a1220"));
            Place(card.rectTransform, x0 + i * (cw + gap), 0, cw, chh);
            Outline(card, sold ? C("#3a2a20") : C("#4a3220"));
            if (sold) card.color = new Color(0.10f, 0.07f, 0.12f, 0.72f);

            if (sold)
            {
                var e = Text(card.rectTransform, "<color=#4a3a2c>売り切れ</color>", 13f, FAINT, TextAlignmentOptions.Center);
                StretchFull(e.rectTransform);
                // ⚠ 売り切れた枠は**空にせず残す**。「買えたのに買わなかった」が見えないと、
                //   限定である意味が伝わらない。
                AddTooltip(card.gameObject, "この枠の品はもう無い。\n品揃えは次のターンに入れ替わります。");
                continue;
            }

            var d = AccessoryCatalog.Get(item);
            int disc = MerchantShop.DiscountOf(i);
            int list = MerchantShop.ListPriceOf(i);
            int pay = MerchantShop.PriceOf(i);

            // 絵
            var art = Panel(card.rectTransform, "art", C("#241826"));
            Place(art.rectTransform, 10, 10, cw - 20, 96);
            Outline(art, C("#3a2a20"));
            var sp = AccessorySprite.ByIndex(item);
            if (sp != null)
            {
                var im = new GameObject("sp", typeof(RectTransform)).AddComponent<Image>();
                im.rectTransform.SetParent(art.rectTransform, false);
                im.sprite = sp; im.preserveAspect = true; im.raycastTarget = false;
                StretchOffset(im.rectTransform, 8, 6, 8, 6);
            }
            else
            {
                var q = Text(art.rectTransform, "?", 34f, C(d.colorHex), TextAlignmentOptions.Center, FontStyles.Bold);
                StretchFull(q.rectTransform);
            }
            // 💸 値引きの札は**絵の角**に貼る（見つけてほしいので）
            if (disc > 0)
            {
                var tag = Panel(art.rectTransform, "sale", C("#2a4a2e"));
                Place(tag.rectTransform, cw - 20 - 56, 4, 52, 20);
                Outline(tag, C("#5cc47c"));
                var tt2 = Text(tag.rectTransform, "-" + disc + "%", 12f, C("#8ce0a8"), TextAlignmentOptions.Center, FontStyles.Bold);
                StretchFull(tt2.rectTransform);
            }

            var nm = Text(card.rectTransform, d.jpName, 13.5f, C(d.colorHex), TextAlignmentOptions.Left, FontStyles.Bold);
            Place(nm.rectTransform, 12, 112, cw - 24, 20);
            var rr = Text(card.rectTransform, "<color=#6f6889>" + AccessoryCatalog.RarityName(d.rarity) + "</color>",
                10.5f, FAINT, TextAlignmentOptions.Left);
            Place(rr.rectTransform, 12, 132, cw - 24, 16);
            var ds = Text(card.rectTransform, "<color=#9c95b4>" + d.desc + "</color>", 10.5f, MUTED, TextAlignmentOptions.TopLeft);
            Place(ds.rectTransform, 12, 150, cw - 24, 62);
            var ef = Text(card.rectTransform, AccessoryCatalog.EffectLine(item), 10.5f, C("#8cb8e6"), TextAlignmentOptions.TopLeft);
            Place(ef.rectTransform, 12, 212, cw - 24, 32);

            string why; bool ok = MerchantShop.CanBuy(i, out why);
            string money = disc > 0
                ? "<s><color=#6f6889>" + list.ToString("#,0") + "</color></s> <color=#8ce0a8>" + pay.ToString("#,0") + " DP</color>"
                : pay.ToString("#,0") + " DP";
            var b = PrimaryButton(card, ok ? money : why, ok ? C("#251d10") : PANEL, ok ? GOLD : C("#4a4560"),
                () => { if (MerchantShop.TryBuy(si)) { RefreshShopPanel(); RefreshMinionCodex(); } });
            Place((RectTransform)b.transform, 12, chh - 44, cw - 24, 32);
            var bt = b.GetComponentInChildren<TextMeshProUGUI>();
            if (bt != null) { bt.fontSize = 12.5f; bt.enableWordWrapping = false; }

            AddTooltip(card.gameObject, "<b>" + d.jpName + "</b>　<color=#6f6889>"
                + AccessoryCatalog.RarityName(d.rarity) + "</color>\n" + d.desc + "\n"
                + AccessoryCatalog.EffectLine(item)
                + (disc > 0 ? "\n<color=#8ce0a8>いまだけ " + disc + "% 引き（定価 " + list.ToString("#,0") + " DP）</color>" : "")
                + "\n<color=#6f6889>買うと手持ちに入ります。着けるのは『魔物』→『個体』から。</color>");
        }

        var turn = DungeonTurnManager.Instance;
        SetTxt(shopSub, "次の入荷で品揃えが入れ替わります　／　<color=#9c95b4>買わなかった品は、次の回には並びません</color>"
            + (turn != null ? "　<color=#6f6889>（第 " + turn.CurrentTurn + " ターン）</color>" : ""));
        SetTxt(shopFootL, "手持ちの装飾品 <color=#d9a05b>" + AccessoryInventory.TotalCount
            + "</color> 個　―　着けるのは『魔物』→『個体』から");
        var res = DungeonResourceManager.Instance;
        SetTxt(shopFootR, "DP <color=#e3a94a>" + (res != null ? res.DungeonPoints.ToString("#,0") : "0") + "</color>");
    }
}
