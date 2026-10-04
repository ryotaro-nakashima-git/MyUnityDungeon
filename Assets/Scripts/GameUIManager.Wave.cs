using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🫁 **波の呼吸を見せる**（②）。戦闘中、画面の上・中央に細い帯を出す。
///
/// <para>
/// ⚠⚠ **仕組みは前からあった。無かったのは表示だけ。**
///   `DungeonAdventurerSpawner` は1ターンの人数を **3つの塊**に割って送り、
///   塊と塊のあいだに「息継ぎ」を空けている（そこが号令と立て直しの窓）。
///   ところが**画面には1文字も出ていなかった**ので、遊ぶ側からは
///   「なんとなく途切れる時間がある」だけで、溜めにも合図にもなっていなかった。
///   ここは <b>その既にある状態を読んで描くだけ</b>。人数・間隔・強さには一切触らない。
/// </para>
///
/// <para>
/// ⚠ **報酬を足さない。** 演出の層に報いを持たせると、倍率の軸が1本増える
///   （→ [[difficulty-curve-orders]]）。この帯が配るのは**情報だけ**。
/// ⚠ 時間は `Time.deltaTime`。息継ぎの残り秒は戦闘の倍速に乗っている数字なので、
///   帯だけ実時間で動かすと**表示と実際がズレる**（→ [[ui-conventions]]）。
/// </para>
///
/// 関連: [[DungeonAdventurerSpawner]]（読む先） [[Foretell]]（ターン単位の予告。こちらは秒単位）。
/// </summary>
public partial class GameUIManager
{
    private const float WAVE_W = 392f;
    private const int WavePipMax = 7;          // batchSize の下限が2なので塊は最大7つ

    private GameObject wavePanel;
    private Image wavePanelBg, waveFill;
    private TextMeshProUGUI waveLabel, waveHint, waveShout;
    private readonly List<Image> wavePips = new List<Image>();
    private int waveLastBatch = -1, waveLastCount = -1;
    private float waveFlash, waveShoutLife;
    private DungeonAdventurerSpawner spawnerRef;

    private float waveShoutBaseY = -142f;

    private void BuildWaveBreath(RectTransform root)
    {
        var panel = Panel(root, "WaveBreath", C("#0e0b16"));
        wavePanel = panel.gameObject; wavePanelBg = panel;
        // ⚠ 上部バー(60px)の**すぐ下・中央**。右上は『次に起きること』が使っているので取り合わない。
        Anchor(panel, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
        panel.rectTransform.sizeDelta = new Vector2(WAVE_W, 66);
        panel.rectTransform.anchoredPosition = new Vector2(0, -68);
        Outline(panel, LINE2);
        panel.color = new Color(panel.color.r, panel.color.g, panel.color.b, 0.9f);

        // ◆ 塊の数だけ点を並べる。出し終えた塊は光り、これから来る塊は沈む。
        wavePips.Clear();
        for (int i = 0; i < WavePipMax; i++)
        {
            var pip = Panel(panel.rectTransform, "Pip" + i, C("#332e49"));
            Place(pip.rectTransform, 12 + i * 15, 10, 11, 11);
            wavePips.Add(pip);
        }
        waveLabel = Text(panel.rectTransform, "", 12f, TEXT, TextAlignmentOptions.Left, FontStyles.Bold);
        waveLabel.enableWordWrapping = false;
        Place(waveLabel.rectTransform, 12 + WavePipMax * 15 + 6, 6, WAVE_W - (12 + WavePipMax * 15 + 6) - 12, 18);

        // 息継ぎのゲージ。⚠ 伸び切る＝次の塊が着く。**満ちる方向**にする（減る方向だと猶予に見える）
        var track = Panel(panel.rectTransform, "track", C("#1b1828"));
        Place(track.rectTransform, 12, 28, WAVE_W - 24, 8);
        waveFill = Panel(track.rectTransform, "fill", CRIMSON);
        waveFill.rectTransform.anchorMin = new Vector2(0, 0); waveFill.rectTransform.anchorMax = new Vector2(0, 1);
        waveFill.rectTransform.pivot = new Vector2(0, 0.5f);
        waveFill.rectTransform.anchoredPosition = Vector2.zero;
        waveFill.rectTransform.sizeDelta = new Vector2(0, 0);

        // ⚠ **見出しと同じ行に置かない。** 最初は右揃えで同じ行に載せたが、
        //   「息継ぎ ― 第2波まで 1.5　いま立て直す」は 12pt でも 250px を超えるので
        //   「第1波 / 全2波」と**重なって両方読めなくなった**（実測）。ゲージの下に1行を割く。
        waveHint = Text(panel.rectTransform, "", 11f, MUTED, TextAlignmentOptions.Right);
        waveHint.enableWordWrapping = false;
        Place(waveHint.rectTransform, 12, 40, WAVE_W - 24, 18);

        // 📣 塊が着いた瞬間の一声。⚠ 帯の**外**（少し下）に出す。帯の中で光らせるだけでは、
        //    盤を見ている目には入らない（降下トーストと同じ理由で、大きく短く出す）。
        var shout = NewRect("WaveShout", root);
        Anchor(shout, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
        shout.sizeDelta = new Vector2(520, 46);
        shout.anchoredPosition = new Vector2(0, -142);
        waveShout = Text(shout, "", 30f, CRIMSON, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(waveShout.rectTransform);
        waveShout.raycastTarget = false;

        wavePanel.SetActive(false);
        SetTxt(waveShout, "");
    }

    /// <summary>🫁 毎フレーム。⚠ 中身は作り直さず**値だけ**書き換える。</summary>
    private void RefreshWaveBreath()
    {
        if (wavePanel == null) return;
        var t = DungeonTurnManager.Instance;
        bool show = t != null && t.IsBattlePhase && !surfaceModeOn;
        if (!show)
        {
            if (wavePanel.activeSelf) { wavePanel.SetActive(false); waveLastBatch = -1; }
            if (waveShout != null && waveShoutLife > 0f) { waveShoutLife = 0f; SetTxt(waveShout, ""); }
            return;
        }
        if (spawnerRef == null) spawnerRef = Object.FindAnyObjectByType<DungeonAdventurerSpawner>();
        var sp = spawnerRef;
        if (sp == null) { if (wavePanel.activeSelf) wavePanel.SetActive(false); return; }

        if (!wavePanel.activeSelf) { wavePanel.SetActive(true); waveLastBatch = -1; }

        int batches = sp.BatchCount, idx = sp.BatchIndex;
        // 🔔 塊が変わった瞬間＝波が着いた瞬間。⚠ 波が始まった直後の1回目も鳴らす（そこも「来た」）
        if (idx != waveLastBatch || batches != waveLastCount)
        {
            ShoutWave(idx, batches);
            waveLastBatch = idx; waveLastCount = batches;
        }

        // ◆ 点：出し終えた塊は明るく、次に来る塊は**息継ぎの進みぶんだけ**明るくなる
        float breath = sp.BreathRatio;
        for (int i = 0; i < wavePips.Count; i++)
        {
            bool on = i < batches;
            wavePips[i].gameObject.SetActive(on);
            if (!on) continue;
            if (i < idx) wavePips[i].color = CRIMSON;
            else if (i == idx && sp.Breathing) wavePips[i].color = Color.Lerp(C("#332e49"), CRIMSON, breath);
            else wavePips[i].color = C("#332e49");
        }

        // 🏢 階層タブ（上の真ん中・高さ34）が出ているときは、その下へずらす（⚠ 同じ場所に重なっていた・ユーザー指摘）
        float tabOff = (floorTabsPanel != null && floorTabsPanel.activeInHierarchy) ? 40f : 0f;
        ((RectTransform)wavePanel.transform).anchoredPosition = new Vector2(0, -68f - tabOff);
        waveShoutBaseY = -142f - tabOff;
        SetTxt(waveLabel, "第 <b>" + idx + "</b> 波 <size=80%><color=#6f6889>/ 全 " + batches + " 波</color></size>");

        float ratio; Color fillCol; string hint;
        if (sp.Breathing)
        {
            // 🫁 息継ぎ。**ここが号令と立て直しの窓**だと言葉で書く（書かないと、ただの空白時間）
            ratio = breath; fillCol = Color.Lerp(C("#e08a3c"), CRIMSON, breath);
            hint = "息継ぎ ― 第 " + Mathf.Min(idx + 1, batches) + " 波まで <b>"
                 + sp.NextBatchIn.ToString("0.0") + "</b>　<color=#e3a94a>いま立て直す</color>";
        }
        else if (sp.IsSpawning)
        {
            ratio = 1f; fillCol = CRIMSON;
            hint = "<color=#e05a5a>第 " + idx + " 波 突入中</color>　<size=90%><color=#6f6889>"
                 + sp.SpawnedThisTurn + "/" + sp.TotalThisTurn + " 人</color></size>";
        }
        else
        {
            // 🏁 全部送り込んだあと。残っているのは盤の上の掃討だけ＝**次は来ない**と明言する
            ratio = 1f; fillCol = C("#57c3ab");
            hint = "<color=#57c3ab>最終波を送り終えた</color>　<size=90%><color=#6f6889>あとは掃討</color></size>";
        }
        float trackW = WAVE_W - 24f;
        waveFill.rectTransform.sizeDelta = new Vector2(trackW * Mathf.Clamp01(ratio), 0);
        waveFill.color = fillCol;
        SetTxt(waveHint, hint);

        // 💥 着弾の残り火：帯が一瞬ふくらんで縁が光る
        if (waveFlash > 0f)
        {
            waveFlash = Mathf.Max(0f, waveFlash - Time.deltaTime * 2.6f);
            wavePanel.transform.localScale = Vector3.one * (1f + 0.07f * waveFlash);
            var ol = wavePanelBg.GetComponent<Outline>();
            if (ol != null) ol.effectColor = Color.Lerp(LINE2, CRIMSON, waveFlash);
        }
        if (waveShoutLife > 0f)
        {
            waveShoutLife -= Time.deltaTime;
            var c = waveShout.color; c.a = Mathf.Clamp01(waveShoutLife / 0.45f); waveShout.color = c;
            waveShout.rectTransform.anchoredPosition =
                new Vector2(0, waveShoutBaseY - (1f - Mathf.Clamp01(waveShoutLife)) * 10f);
            if (waveShoutLife <= 0f) SetTxt(waveShout, "");
        }
    }

    // ============ ⚔️ 戦闘中の手（①）============
    //  ⚠⚠ **押す物は押す物の隣にまとめる。** 号令バーのすぐ上に置いて、
    //    「いま何ができるか」が1か所で読めるようにする。散らすと結局どれも見つけてもらえない。
    private const float ACT_W = 720f;
    private GameObject actionBar;
    private TextMeshProUGUI actLure, actOverload, actReap, actGaugeLabel;
    private Image actGaugeFill;
    private Button actReleaseBtn;

    private void BuildActionBar(RectTransform root)
    {
        var bar = Panel(root, "ActionBar", C("#0e0b16"));
        actionBar = bar.gameObject;
        Anchor(bar, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        bar.rectTransform.sizeDelta = new Vector2(ACT_W, 38);
        bar.rectTransform.anchoredPosition = new Vector2(0, UITheme.BarH + 92f);   // 号令バー(74高)の上
        Outline(bar, C("#8a5a24"));
        bar.color = new Color(bar.color.r, bar.color.g, bar.color.b, 0.92f);

        // ⚠ 1行の枠は文字の大きさ×1.5 より高く（TMPは足りないと1文字も描かない → [[ui-conventions]]）
        actLure = Text(bar, "", 12.5f, C("#e3a94a"), TextAlignmentOptions.Left, FontStyles.Bold);
        actLure.enableWordWrapping = false;
        Place(actLure.rectTransform, 12, 9, 130, 20);
        actOverload = Text(bar, "", 12.5f, C("#e08a3c"), TextAlignmentOptions.Left, FontStyles.Bold);
        actOverload.enableWordWrapping = false;
        Place(actOverload.rectTransform, 146, 9, 130, 20);
        actReap = Text(bar, "", 12.5f, C("#c04a6a"), TextAlignmentOptions.Left, FontStyles.Bold);
        actReap.enableWordWrapping = false;
        Place(actReap.rectTransform, 280, 9, 140, 20);

        // 📯 号令ゲージ：溜まっているのが見えないと「溜める」が行為にならない
        var track = Panel(bar.rectTransform, "GaugeTrack", C("#1b1828"));
        Place(track.rectTransform, 428, 12, 190, 14);
        // ⚠ ゲージの上に文字を重ねるので、**塗りは沈んだ色**にする。
        //   実測：鮮やかな赤で塗ったら「号令ゲージ」の1文字目が読めなくなった。
        actGaugeFill = Panel(track.rectTransform, "fill", C("#7a2740"));
        actGaugeFill.rectTransform.anchorMin = new Vector2(0, 0); actGaugeFill.rectTransform.anchorMax = new Vector2(0, 1);
        actGaugeFill.rectTransform.pivot = new Vector2(0, 0.5f);
        actGaugeFill.rectTransform.anchoredPosition = Vector2.zero;
        actGaugeFill.rectTransform.sizeDelta = new Vector2(0, 0);
        actGaugeLabel = Text(bar, "", 11f, TEXT, TextAlignmentOptions.Center, FontStyles.Bold);
        actGaugeLabel.enableWordWrapping = false;
        Place(actGaugeLabel.rectTransform, 428, 10, 190, 18);

        actReleaseBtn = PrimaryButton(bar, "解き放つ", BLOOD, TEXT, () =>
        {
            string why;
            if (!CommandCharge.TryRelease(out why))
            { NotifySystem.Push(why, NotifySystem.Kind.Loss); SoundSystem.Play(SoundSystem.Sfx.Error); }
        }, true);
        Place((RectTransform)actReleaseBtn.transform, ACT_W - 12 - 88, 6, 88, 26);
        AddTooltip(((RectTransform)actReleaseBtn.transform).gameObject,
            "撃破で溜まったゲージを解き放ち、<b>すべての号令のクールダウンを戻す</b>（1波に1回）　<color=#9c95b4>[Q]</color>");

        actionBar.SetActive(false);
    }

    private void RefreshActionBar()
    {
        if (actionBar == null) return;
        var t = DungeonTurnManager.Instance;
        bool show = t != null && t.IsBattlePhase && !surfaceModeOn;
        if (actionBar.activeSelf != show) actionBar.SetActive(show);
        if (!show) return;

        bool cool = Decoy.CooldownLeft > 0f;
        SetTxt(actLure, Decoy.Active
            ? "◆ おとり " + Decoy.Life.ToString("0.0")
            : "◆ 誘引 " + Decoy.LureLeft + (cool ? " <size=80%><color=#6f6889>" + Decoy.CooldownLeft.ToString("0.0") + "</color></size>" : ""));
        actLure.color = Decoy.Active ? C("#ffd24a") : (Decoy.LureLeft > 0 && !cool ? C("#e3a94a") : FAINT);

        SetTxt(actOverload, "◆ 過負荷 " + Decoy.OverloadLeft);
        actOverload.color = Decoy.OverloadLeft > 0 && !cool ? C("#e08a3c") : FAINT;

        bool reapCool = EmotionHarvest.CooldownLeft > 0f;
        SetTxt(actReap, "◆ 刈り取り " + EmotionHarvest.Left
            + (reapCool ? " <size=80%><color=#6f6889>" + EmotionHarvest.CooldownLeft.ToString("0.0") + "</color></size>" : ""));
        actReap.color = EmotionHarvest.Left > 0 && !reapCool ? C("#c04a6a") : FAINT;

        float r = CommandCharge.Ratio;
        actGaugeFill.rectTransform.sizeDelta = new Vector2(190f * r, 0f);
        actGaugeFill.color = CommandCharge.ReadyToRelease ? C("#a8761a") : C("#7a2740");
        SetTxt(actGaugeLabel, CommandCharge.Left <= 0 ? "号令ゲージ ― 解き放った"
            : CommandCharge.ReadyToRelease ? "<b>号令ゲージ 満</b>"
            : "号令ゲージ " + Mathf.RoundToInt(r * 100f) + "%");
        actReleaseBtn.interactable = CommandCharge.ReadyToRelease;
        actReleaseBtn.gameObject.SetActive(CommandCharge.Left > 0);
    }

    /// <summary>⌨️ [Q] 号令ゲージを解き放つ。⚠ 戦闘中だけ。</summary>
    public void ReleaseChargeByHotkey()
    {
        var t = DungeonTurnManager.Instance;
        if (t == null || !t.IsBattlePhase) return;
        string why;
        if (!CommandCharge.TryRelease(out why))
        { NotifySystem.Push(why, NotifySystem.Kind.Loss); SoundSystem.Play(SoundSystem.Sfx.Error); }
    }

    private void ShoutWave(int idx, int batches)
    {
        waveFlash = 1f;
        waveShoutLife = 1.15f;
        if (waveShout == null) return;
        // ⚠⚠ **一番手前に出し直す。** この帯は `BuildUI` の途中で作られるので、
        //   あとから作られるパネル（報告・先触れ・因縁…）が兄弟として上に乗る。
        //   実測で、腹心の報告が開いているあいだ**一声がその裏に隠れて見えなかった**。
        //   触れない文字なので手前に出しても操作の邪魔にはならない。
        waveShout.transform.parent.SetAsLastSibling();
        // ⚠ 最後の塊は「最終波」と呼ぶ。ここを「第3波」で済ますと、
        //   まだ来るのか終わりなのかが分からず、**息継ぎの重みが最後まで同じ**になる。
        bool last = idx >= batches && batches > 1;
        SetTxt(waveShout, last ? "最 終 波" : "第 " + idx + " 波");
        waveShout.color = last ? C("#e3a94a") : CRIMSON;
        var col = waveShout.color; col.a = 1f; waveShout.color = col;
        SoundSystem.Play(SoundSystem.Sfx.Wave, 0.75f, last ? 0.85f : 1f);
        ScreenShake.Kick(last ? 0.16f : 0.10f, 0.22f);
    }
}
