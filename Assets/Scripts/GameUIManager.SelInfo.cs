using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🎯 段G：**「選んでいる物」を配置の帯の右端に出す**（ユーザー承認：A案＝帯そのものに足す）。
/// 名前・値段・置ける残り・置き方を、帯から目を離さずに読めるようにする。
///
/// ⚠ 帯ごとの Refresh は子を作り直して幅も決め直す（7本それぞれ形が違う）。
///   各 Refresh に手を入れると7か所の早期 return を全部追うことになるので、
///   ここで**毎フレーム見張って**、欄が消えていたら付け直し、幅が戻されていたら広げ直す。
/// ⚠ 欄の中身は表示だけ。置ける・置けないの判定は `DungeonFeatureManager` が持つ（ここで二重に決めない）。
/// </summary>
public partial class GameUIManager
{
    private const float SEL_INFO_W = 310f;
    private TextMeshProUGUI selInfoText;
    private RectTransform selInfoHost;
    private const float SEL_INFO_MAX_W = 1380f;   // ⚠ これより広いと右下の大ボタン（約260px）に被る
    private const float SEL_INFO_ROW = 24f;
    private float selInfoBaseW, selInfoBaseH;
    private bool selInfoWide;

    private void TickSelInfo()
    {
        if (featureMgr == null) return;
        GameObject s = null; int mode = -1;
        if (squadStrip != null && squadStrip.activeInHierarchy)
        {
            s = squadStrip; mode = 11;
            int cm = input != null ? input.CurrentToolMode : 11;
            if (cm == 8 || cm == 9) mode = cm;   // 🛡️ H2：帯は1本、置く物は道具で決まる
        }
        else if (bossStrip != null && bossStrip.activeInHierarchy) { s = bossStrip; mode = 8; }
        else if (trapStrip != null && trapStrip.activeInHierarchy) { s = trapStrip; mode = 3; }
        else if (totemStrip != null && totemStrip.activeInHierarchy) { s = totemStrip; mode = 6; }
        else if (specialStrip != null && specialStrip.activeInHierarchy) { s = specialStrip; mode = 9; }
        else if (habitatStrip != null && habitatStrip.activeInHierarchy) { s = habitatStrip; mode = 16; }
        else if (greatWorkStrip != null && greatWorkStrip.activeInHierarchy) { s = greatWorkStrip; mode = 17; }
        if (s == null) return;

        var rt = (RectTransform)s.transform;
        // 📏 幅の広い帯（トーテム13種・ボス）は右に足すと右下の大ボタンに被るので、**下に1行**足す
        bool wide = selInfoBaseW + SEL_INFO_W > SEL_INFO_MAX_W;
        Vector2 want = wide ? new Vector2(selInfoBaseW, selInfoBaseH + SEL_INFO_ROW)
                            : new Vector2(selInfoBaseW + SEL_INFO_W, selInfoBaseH);
        bool sameHost = selInfoHost == rt;
        // ⚠ Refresh が元の大きさに戻したら、その大きさを新しい基準にして広げ直す
        if (!sameHost || (rt.sizeDelta - want).sqrMagnitude > 0.25f)
        {
            selInfoBaseW = rt.sizeDelta.x; selInfoBaseH = rt.sizeDelta.y;
            wide = selInfoBaseW + SEL_INFO_W > SEL_INFO_MAX_W;
            rt.sizeDelta = wide ? new Vector2(selInfoBaseW, selInfoBaseH + SEL_INFO_ROW)
                                : new Vector2(selInfoBaseW + SEL_INFO_W, selInfoBaseH);
            selInfoHost = rt; selInfoWide = wide;
            if (selInfoText != null) Destroy(selInfoText.transform.parent.gameObject);
            selInfoText = null;
        }
        // ↔ 右端が右下の大ボタン（侵略開始など）に届く帯は左へ寄せる（⚠ トーテム13種は欄を足す前から被っていた）
        {
            float nx = Mathf.Min(0f, UITheme.ScreenW * 0.5f - 300f - rt.sizeDelta.x * 0.5f);
            if (Mathf.Abs(rt.anchoredPosition.x - nx) > 0.5f) rt.anchoredPosition = new Vector2(nx, rt.anchoredPosition.y);
        }
        // 🏷️ 欄：Refresh に消されていたら作り直す（⚠ Destroy は遅れて効くので、親が違う物も作り直す）
        if (selInfoText == null || selInfoText.transform.parent == null || selInfoText.transform.parent.parent != rt)
        {
            var box = NewRect("SelInfo", rt);
            var line = Panel(box, "Div", LINE2);
            line.raycastTarget = false;
            if (selInfoWide)
            {
                // ⚠ 帯は下端が基準（pivot 0）なので、高さを足すと中身は上へ逃げ、下に1行ぶん空く
                Place(box, 0, selInfoBaseH, selInfoBaseW, SEL_INFO_ROW);
                Place(line.rectTransform, 8, 0, selInfoBaseW - 16, 1);
            }
            else
            {
                Place(box, selInfoBaseW, 0, SEL_INFO_W, selInfoBaseH);
                Place(line.rectTransform, 0, 6, 1, selInfoBaseH - 12);
            }
            selInfoText = Text(box, "", 11.5f, TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            selInfoText.raycastTarget = false;
            selInfoText.enableWordWrapping = false;
            selInfoText.overflowMode = TextOverflowModes.Ellipsis;
            if (selInfoWide) Place(selInfoText.rectTransform, 12, 1, selInfoBaseW - 24, SEL_INFO_ROW - 2);
            else Place(selInfoText.rectTransform, 10, 2, SEL_INFO_W - 16, selInfoBaseH - 4);
        }
        string txt = SelInfoLine(mode, selInfoWide ? "　　" : "\n");
        if (selInfoText.text != txt) selInfoText.text = txt;
    }

    /// <summary>選んでいる物の2行（1行目＝名前と値段／2行目＝置ける残りと置き方）。</summary>
    private string SelInfoLine(int mode, string sep)
    {
        string name = null; int cost = 0; string blocked = null;
        switch (mode)
        {
            case 3:
            {
                var d = TrapCatalog.Get(featureMgr.SelectedTrapKind);
                name = d.name; cost = d.dpCost;
                if (!TrapCatalog.IsUnlocked(featureMgr.SelectedTrapKind)) blocked = "未解禁（領域研究）";
                break;
            }
            case 6:
            {
                var d = TotemCatalog.Get(featureMgr.SelectedTotemKind);
                name = d.jpName; cost = d.dpCost;
                if (!TotemCatalog.IsUnlocked(featureMgr.SelectedTotemKind)) blocked = "未解禁（領域研究）";
                break;
            }
            case 16:
            {
                var d = HabitatCatalog.Get(featureMgr.SelectedHabitatKind);
                name = d.jpName; cost = d.dpCost;
                break;
            }
            case 17:
            {
                var d = GreatWorkCatalog.Get(featureMgr.SelectedGreatWorkKind);
                name = d.jpName; cost = d.dpCost;
                var flr = DungeonFloorManager.Instance;
                if (flr != null && !featureMgr.AnyGreatWorkSpot(flr.CurrentFloorIndex)) blocked = "この階に空きが無い";
                break;
            }
            case 11:
            {
                var squad = featureMgr.CurrentSquad;
                int id = -1;
                if (squad.Count > 0)
                {
                    int sel = Mathf.Clamp(featureMgr.SquadPlaceSlot, 0, squad.Count - 1);
                    if (!featureMgr.IsIndividualPlaced(squad[sel])) id = squad[sel];
                    else for (int i = 0; i < squad.Count; i++) if (!featureMgr.IsIndividualPlaced(squad[i])) { id = squad[i]; break; }
                }
                if (id >= 0) name = IndividualName(id);
                else blocked = squad.Count == 0 ? "隊が空" : "隊員はみな配置済み";
                break;
            }
            case 8:
            {
                var flr0 = DungeonFloorManager.Instance;
                int fl = flr0 != null ? flr0.CurrentFloorIndex : 0;
                int id = featureMgr.AppointedBossOf(fl);
                if (id >= 0 && MinionRoster.Get(id) != null) name = IndividualName(id) + " <color=#f0a0a0>ボス</color>";
                cost = featureMgr.CostOf(DungeonFeatureManager.FeatureType.Boss);
                if (featureMgr.BossPlacedOn(fl)) blocked = "この階のボスはもう置いた";
                else if (name == null) blocked = "ボスは「配下」の画面で決める";
                break;
            }
            case 9:
            {
                int id = featureMgr.SelectedUniqueId;
                if (id >= 0 && MinionRoster.Get(id) != null && !featureMgr.IsIndividualPlaced(id)) name = IndividualName(id);
                else blocked = MinionRoster.Uniques().Count == 0 ? "ユニークを持っていない" : "ユニークを選んでください";
                break;
            }
        }

        int dp = res != null ? res.DungeonPoints : 0;
        string costS = cost <= 0 ? "<color=#9c95b4>費用なし</color>"
            : dp >= cost ? "<color=#e3a94a>" + UITheme.Num(cost) + "DP</color>"
            : "<color=#e05a5a>" + UITheme.Num(cost) + "DP（足りない）</color>";
        string l1 = "<color=#9c95b4>選択中</color>　" + (name ?? "―") + (name != null ? "　" + costS : "");

        int placed = featureMgr.PlacedCount, cap = featureMgr.PlacementCap, left = Mathf.Max(0, cap - placed);
        string leftS = left > 0 ? "置ける残り <color=#5cc47c>" + left + "</color>" : "<color=#e05a5a>配置枠が満杯</color>";
        string l2 = leftS + "<size=86%><color=#9c95b4>（枠 " + placed + "/" + cap + "）</color></size>　";
        if (blocked != null) l2 += "<color=#e08a3c>" + blocked + "</color>";
        else if (left > 0) l2 += "<size=86%><color=#6f6889>マスを押す／掴んで運ぶ</color></size>";
        return l1 + sep + l2;
    }

    private static string IndividualName(int id)
    {
        var v = MinionRoster.Get(id);
        if (v == null) return null;
        return MinionCatalog.Get(v.catalogIndex).jpName + " <size=86%>Lv" + v.level + "</size>";
    }
}
