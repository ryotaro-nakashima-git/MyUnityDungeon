using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🎁 <b>撒く等級</b>の窓（装備水準の作り直し・UI）。
///
/// <para>
/// <b>この窓がやることは1つ ―― 「何を撒くと、世界がどこまで武装するか」を先に見せる。</b>
/// </para>
///
/// <para>
/// ⚠⚠ <b>全階を1枚に並べる（案B）。</b>階ごとの窓にすると2つ壊れる：
///   ① <b>その階を表示していないと設定できない</b>（階層タブを回らないと迷宮全体を組めない）
///   ② <b>「どこかの階の最高等級 ＝ 世界水準の上限」が階を回らないと分からない</b>。
///   決めているのは1マスの中身ではなく<b>迷宮全体の勾配</b>なので、勾配が一望できる形でないと判断材料にならない。
/// ⚠ <b>10×階層数の表を触らせない。</b>つまみは階ごとに「基準の等級」と「ばらつき」の2つだけ。
/// ⚠ <b>見返りを必ず一緒に出す。</b>等級を上げる痛みだけ見せると「上げない」が無条件の最適解になる。
/// ⚠ 勝率は出さない（→ [[readiness-and-trade]]）。出すのは撒く等級と、その結果の上限という事実だけ。
/// </para>
///
/// 関連: [[TreasureGrades]] [[LureEconomy]] [[GameUIManager.Omen]]（相手が着てくる等級）。
/// </summary>
public partial class GameUIManager
{
    private bool chestGradeOpen;
    private GameObject chestGradePanel;
    private RectTransform chestGradeBody;

    public void OpenChestGradeWindow() { chestGradeOpen = true; RefreshChestGradeWindow(); }
    public void CloseChestGradeWindow() { chestGradeOpen = false; RefreshChestGradeWindow(); }

    /// <summary>⚠ 起動時に1度だけ組む（他の窓と同じ作り）。</summary>
    private void BuildChestGradePanel(RectTransform root)
    {
        var p = Panel(root, "ChestGradeWindow", PANEL);
        chestGradePanel = p.gameObject;
        Anchor(p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        p.rectTransform.sizeDelta = new Vector2(720, 520);
        p.rectTransform.anchoredPosition = Vector2.zero;
        Outline(p, LINE2);

        var body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
        body.SetParent(p.rectTransform, false);
        Anchor(body, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1));
        body.sizeDelta = new Vector2(720, 520);
        body.anchoredPosition = Vector2.zero;
        chestGradeBody = body;
        chestGradePanel.SetActive(false);
    }

    /// <summary>⚠ 毎回作り直す（つまみが動くので差分更新にすると必ずずれる）。</summary>
    private void RefreshChestGradeWindow()
    {
        if (chestGradePanel == null) return;
        if (!chestGradeOpen) { chestGradePanel.SetActive(false); return; }
        chestGradePanel.SetActive(true);

        var c = chestGradeBody;
        for (int i = c.childCount - 1; i >= 0; i--) { var g = c.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }

        float W = 720f, y = 0f;
        int unlocked = TreasureGrades.MaxUnlocked;
        int floors = TreasureGrades.FloorCount;

        // ── 見出し ──
        var bar = Panel(c, "Bar", HUD_BG); Place(bar.rectTransform, 0, 0, W, 40); Outline(bar, LINE);
        var ttl = Text(bar.rectTransform, "🎁 撒く等級", 16, TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Place(ttl.rectTransform, 16, 10, 160, 22);
        var sub = Text(bar.rectTransform, "解禁 <color=#d45ba8><b>等級1〜" + unlocked + "</b></color>"
            + (unlocked < TreasureGrades.TopGrade ? "　<color=#9c95b4>領域研究『宝物庫 → 秘蔵の品 → 禁書の宝物』で10まで</color>" : ""),
            12, MUTED, TextAlignmentOptions.MidlineLeft);
        Place(sub.rectTransform, 176, 11, W - 270, 20);
        var xb = PrimaryButton(bar, "閉じる", PANEL2, MUTED, () => { chestGradeOpen = false; RefreshChestGradeWindow(); });
        Place((RectTransform)xb.transform, W - 86, 7, 72, 26);
        y = 48;

        // ── 等級のチップ（解禁の見取り図）──
        for (int g = 1; g <= TreasureGrades.TopGrade; g++)
        {
            bool on = g <= unlocked;
            var chip = Panel(c, "G" + g, on ? C("#2a1a26") : CARD);
            Place(chip.rectTransform, 12 + (g - 1) * 40, y, 36, 30);
            Outline(chip, on ? C("#d45ba8") : LINE);
            var t = Text(chip.rectTransform, g.ToString(), 12, on ? C("#f0b6dc") : C("#463f5c"),
                TextAlignmentOptions.Center);
            Place(t.rectTransform, 0, 5, 36, 20);
        }
        var chipNote = Text(c, "薄い＝まだ解禁していない等級", 11, FAINT, TextAlignmentOptions.MidlineLeft);
        Place(chipNote.rectTransform, 12 + TreasureGrades.TopGrade * 40 + 10, y + 6, 220, 18);
        y += 40;

        // ── 見出し行 ──
        var hd = Panel(c, "Head", PANEL2); Place(hd.rectTransform, 12, y, W - 24, 24); Outline(hd, LINE);
        HeadCell(hd.rectTransform, "階", 10, 46);
        HeadCell(hd.rectTransform, "基準の等級", 60, 190);
        HeadCell(hd.rectTransform, "ばらつき", 256, 160);
        HeadCell(hd.rectTransform, "撒く等級", 422, 120);
        HeadCell(hd.rectTransform, "1つの見返り", 546, 140);
        y += 26;

        // ── 1行＝1階（全階を1枚に）──
        for (int f = 0; f < floors; f++)
        {
            int floor = f;   // ⚠ クロージャに入れる前に確定させる（全行が最後の階を指す事故）
            BuildFloorRow(c, W, y, floor);
            y += 40;
        }
        y += 6;

        // ── 結果の帯：この迷宮の最高等級＝世界の上限 ──
        int seedHi = TreasureGrades.SeededMaxGrade;
        var outp = Panel(c, "Out", C("#1a1420")); Place(outp.rectTransform, 12, y, W - 24, 62); Outline(outp, C("#d45ba8"));
        var o1 = Text(outp.rectTransform,
            "この迷宮で撒く最高等級は <b><color=#f0b6dc>等級" + seedHi + "（" + EquipmentCatalog.Name(seedHi - 1) + "）</color></b>"
            + "　＝　<b>世界の装備水準が到達できる上限</b>",
            13, TEXT, TextAlignmentOptions.Left);
        Place(o1.rectTransform, 14, 8, W - 52, 20);
        var o2 = Text(outp.rectTransform,
            "いまの世界の装備水準 <b>" + TreasureGrades.Label(Mathf.FloorToInt(LureEconomy.GearGrade)) + "</b>"
            + "　<color=#9c95b4>／ 世界が自前で武装する下限 "
            + TreasureGrades.Label(Mathf.FloorToInt(LureEconomy.FloorLevel / LureEconomy.GearPerGrade)) + "</color>"
            + "　<color=#9c95b4>― 追いつくと差が0になり、それ以上は上がらない</color>",
            11.5f, MUTED, TextAlignmentOptions.Left);
        Place(o2.rectTransform, 14, 32, W - 52, 20);
        y += 70;

        var warn = Text(c,
            "<color=#e3a94a>⚠</color> 良い物を撒くほど<b>見返りは大きく</b>、そのぶん<b>開けた者の装備が上がる</b>。"
            + "上がった装備は逃げ切られると世界に広まり、次に来る者が着てくる。",
            11.5f, FAINT, TextAlignmentOptions.TopLeft);
        Place(warn.rectTransform, 14, y, W - 28, 34);
        // ⚠ 窓の大きさは固定にする（他の窓と同じ）。階は最大5つで、それでも y は 440 に収まる。
        //   毎回 sizeDelta を書き換えると、上寄せの body と中央寄せの panel でずれが出る。
    }

    private void HeadCell(RectTransform parent, string s, float x, float w)
    {
        var t = Text(parent, s, 11, MUTED, TextAlignmentOptions.MidlineLeft);
        Place(t.rectTransform, x, 3, w, 18);
    }

    /// <summary>1階ぶんの行。⚠ つまみは2つだけ（10×階層数の表にしない）。</summary>
    private void BuildFloorRow(RectTransform c, float W, float y, int floor)
    {
        int unlocked = TreasureGrades.MaxUnlocked;
        int b = TreasureGrades.BaseOf(floor), sp = TreasureGrades.SpreadOf(floor);
        int lo = TreasureGrades.LowOf(floor), hi = TreasureGrades.HighOf(floor);

        var row = Panel(c, "F" + floor, CARD); Place(row.rectTransform, 12, y, W - 24, 36); Outline(row, LINE);
        var fl = Text(row.rectTransform, "B" + (floor + 1) + "F", 13, TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Place(fl.rectTransform, 10, 8, 46, 20);

        // 基準の等級（− ＋）
        Stepper(row.rectTransform, 60, b.ToString(), b > TreasureGrades.MinGrade, b < unlocked,
            () => { TreasureGrades.SetBase(floor, TreasureGrades.BaseOf(floor) - 1); RefreshChestGradeWindow(); },
            () => { TreasureGrades.SetBase(floor, TreasureGrades.BaseOf(floor) + 1); RefreshChestGradeWindow(); });

        // ばらつき（− ＋）
        Stepper(row.rectTransform, 256, "±" + sp, sp > 0, sp < TreasureGrades.MaxSpread,
            () => { TreasureGrades.SetSpread(floor, TreasureGrades.SpreadOf(floor) - 1); RefreshChestGradeWindow(); },
            () => { TreasureGrades.SetSpread(floor, TreasureGrades.SpreadOf(floor) + 1); RefreshChestGradeWindow(); });

        // 撒く等級の帯（解禁で頭打ちになるのが見えるように、届かないぶんは薄く出す）
        int wantHi = b + sp;
        float cx = 422;
        for (int g = lo; g <= Mathf.Min(wantHi, TreasureGrades.TopGrade); g++)
        {
            bool reach = g <= unlocked;
            var chip = Panel(row.rectTransform, "c" + g, reach ? C("#2a1a26") : PANEL2);
            Place(chip.rectTransform, cx, 8, 22, 20); Outline(chip, reach ? C("#d45ba8") : LINE);
            var t = Text(chip.rectTransform, g.ToString(), 11, reach ? C("#f0b6dc") : C("#463f5c"), TextAlignmentOptions.Center);
            Place(t.rectTransform, 0, 2, 22, 16);
            cx += 24;
        }

        // 見返り（等級に比例。⚠ 索引ではなく実際に渡る強さで払う → TreasureGrades.RewardMult）
        float dpLo = TreasureGrades.JoyOf(lo - 1), dpHi = TreasureGrades.JoyOf(hi - 1);
        var rw = Text(row.rectTransform,
            "DP <color=#e3a94a>" + Mathf.RoundToInt(dpLo) + (hi > lo ? "〜" + Mathf.RoundToInt(dpHi) : "") + "</color>"
            + "　感情 <color=#e3a94a>" + TreasureGrades.EmotionOf(lo - 1)
            + (hi > lo ? "〜" + TreasureGrades.EmotionOf(hi - 1) : "") + "</color>",
            11.5f, MUTED, TextAlignmentOptions.MidlineLeft);
        Place(rw.rectTransform, 546, 8, 150, 20);
    }

    /// <summary>− 値 ＋ の1組。⚠ 押せない側は色を落とす（押せる手だけが見えていること）。</summary>
    private void Stepper(RectTransform parent, float x, string val, bool canDown, bool canUp,
        System.Action onDown, System.Action onUp)
    {
        var mb = PrimaryButton(parent, "−", PANEL2, canDown ? TEXT : FAINT, () => { if (canDown) onDown(); });
        Place((RectTransform)mb.transform, x, 6, 26, 24);
        var t = Text(parent, val, 15, GOLD, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(t.rectTransform, x + 28, 7, 44, 22);
        var pb = PrimaryButton(parent, "＋", PANEL2, canUp ? TEXT : FAINT, () => { if (canUp) onUp(); });
        Place((RectTransform)pb.transform, x + 74, 6, 26, 24);
    }
}
