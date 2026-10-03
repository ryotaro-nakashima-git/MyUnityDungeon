using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 🕹️ 地上のユニットの札（段B）。**ユニットを選ぶ → 札から命令する → 盤で行き先を押す**。
///
/// **なぜ作り直したか**：前は「タイルを押す → そのタイルでできることを帯に並べる」形で、
/// 動かすユニットは `ActiveKin()` が**動ける最初の1体を勝手に選んでいた**。誰に命令しているのかが
/// 画面のどこにも無く、大ボタンの「眷属を動かす」も眷属の一覧を開くだけだった（導線が切れていた）。
///
/// - 盤のユニットか、左下の列を押すと選ぶ。右クリックで外す。
/// - 選ぶと、今ターンに歩ける所が水色（数字＝着いたら残る移動力）、攻められる所が赤になる。
/// - 塗られたマスを押せば移動／攻撃。遠くのマスなら進軍（何ターンで着くかを指した時点で出す）。
/// - 命令を待っているユニットは大ボタン「命令を待つユニット」と Tab で順に案内する（[[UnitOrders]]）。
/// - タイルの情報は右の小さな札（前の帯）に分けた。拠点を築く・斥候を出すはタイルの操作なのでそちらに残す。
/// <para>`GameUIManager` の partial。</para>
/// </summary>
public partial class GameUIManager
{
    private UnitOrders.Unit selUnit = new UnitOrders.Unit(UnitOrders.Kind.None, -1);
    /// <summary>
    /// ⚔️ 攻める場面。⚠ 無主の土地は**歩いて入れるし、攻めて取ることもできる**。ふだんの押し方は「歩く」にして、
    ///   札の「攻撃」を押したときだけ赤いマスを押すと攻める（そうしないと隣の無主の土地を取る手段が消える）。
    ///   敵の領地・敵の軍は歩いて入れないので、ふだんから赤で出して押せば攻める。
    /// </summary>
    private bool unitAttackMode;
    private Image unitCard, unitRoster, unitHintBg;
    private Image unitFace;
    private TextMeshProUGUI unitKindText, unitNameText, unitStateText, unitStatsText, unitMsgText, unitHintText;
    private RectTransform unitActs;
    private string unitCardSig, unitRosterSig;
    private const float UnitCardW = 780f, UnitCardH = 176f, UnitRosterH = 62f;

    // ================= 組み立て =================
    private void BuildUnitCard(Image panel, float x)
    {
        float pad = 22f;
        float y = FS_H - pad - UnitCardH;

        // 🧑‍🤝‍🧑 命令待ちの列（札の上）
        unitRoster = Panel(panel, "UnitRoster", new Color(0.08f, 0.07f, 0.10f, 0.88f));
        Place(unitRoster.rectTransform, x, y - 10f - UnitRosterH, 200f, UnitRosterH);
        Outline(unitRoster, LINE2);

        // 🃏 札
        unitCard = Panel(panel, "UnitCard", PANEL);
        Place(unitCard.rectTransform, x, y, UnitCardW, UnitCardH);
        Outline(unitCard, LINE2); SkinPanel(unitCard);

        var faceBg = Panel(unitCard, "FaceBg", C("#120f1c"));
        Place(faceBg.rectTransform, 12, 12, 124, 124); Outline(faceBg, LINE);
        faceBg.raycastTarget = false;
        unitFace = new GameObject("Face", typeof(RectTransform)).AddComponent<Image>();
        unitFace.rectTransform.SetParent(faceBg.rectTransform, false);
        unitFace.raycastTarget = false; unitFace.preserveAspect = true;
        Place(unitFace.rectTransform, 10, 10, 104, 104);
        unitKindText = Text(unitCard, "", 12f, MUTED, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(unitKindText.rectTransform, 6, 142, 136, 26);

        float bx = 150f, bw = UnitCardW - bx - 14f;
        unitNameText = Text(unitCard, "", 21f, C("#ffd24a"), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Place(unitNameText.rectTransform, bx, 10, bw - 220f, 30);
        unitStateText = Text(unitCard, "", 13f, MUTED, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        Place(unitStateText.rectTransform, bx + bw - 260f, 12, 260f, 26);
        unitStatsText = Text(unitCard, "", 13f, TEXT, TextAlignmentOptions.MidlineLeft);
        Place(unitStatsText.rectTransform, bx, 44, bw, 24);
        unitMsgText = Text(unitCard, "", 12.5f, MUTED, TextAlignmentOptions.TopLeft);
        Place(unitMsgText.rectTransform, bx, 70, bw, 40);
        unitActs = NewRect("UnitActs", unitCard.rectTransform);
        Place(unitActs, bx, 114, bw, 50);

        // 💬 指した先の見込み（マウスの横に出す）。⚠ 盤の上の不透明な板として数えられないよう、
        //   `SurfaceInner` の**外**（外側の器）に置き、当たり判定も切る（`PointerOverSurfaceUI` 参照）。
        unitHintBg = Panel(surfacePanel.transform, "UnitHint", new Color(0.04f, 0.035f, 0.06f, 0.95f));
        unitHintBg.raycastTarget = false;
        Outline(unitHintBg, GOLD_DK);
        var hr = unitHintBg.rectTransform;
        hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.5f); hr.pivot = new Vector2(0f, 1f);
        unitHintText = Text(unitHintBg, "", 13f, TEXT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        unitHintText.raycastTarget = false; unitHintText.enableWordWrapping = false;
        StretchOffset(unitHintText.rectTransform, 10, 4, 10, 4);
        unitHintBg.gameObject.SetActive(false);

        unitCard.gameObject.SetActive(false);
        unitRoster.gameObject.SetActive(false);
    }

    // ================= 選ぶ =================
    private bool UnitExists(UnitOrders.Unit u) => !u.IsNone && UnitOrders.RegionOf(u) >= 0;

    private void SelectUnit(UnitOrders.Unit u, bool center)
    {
        selUnit = u;
        unitAttackMode = false;
        selectedLegionId = u.kind == UnitOrders.Kind.Legion ? u.id : -1;
        selectedScoutId = u.kind == UnitOrders.Kind.Scout ? u.id : -1;
        if (u.kind == UnitOrders.Kind.Kin) selectedKinId = u.id;
        int rid = UnitOrders.RegionOf(u);
        if (rid >= 0)
        {
            selectedRegionId = rid;
            if (surfaceView != null) { surfaceView.SetSelected(rid); if (center) surfaceView.CenterOn(rid); }
        }
        surfaceActionMsg = "";
        RefreshSurfacePanel();
    }

    private void ClearUnitSelection()
    {
        selUnit = new UnitOrders.Unit(UnitOrders.Kind.None, -1);
        unitAttackMode = false;
        selectedLegionId = -1; selectedScoutId = -1;
        HideUnitHint();
        RefreshSurfacePanel();
    }

    /// <summary>▶ 命令を待っている次の1体へ（大ボタン・Tab・札の「次へ」）。</summary>
    public void SelectNextWaitingUnit()
    {
        var n = UnitOrders.NextWaiting(selUnit);
        if (n.IsNone)
        {
            surfaceActionMsg = "<color=#9c95b4>命令を待っているユニットはいません。</color>";
            RefreshSurfacePanel();
            return;
        }
        SelectUnit(n, true);
    }

    /// <summary>⌨️ Tab。地上を見ているときだけ（迷宮では何もしない）。</summary>
    public void NextUnitByHotkey()
    {
        if (!surfaceModeOn || turn == null || !turn.IsSurfacePhase) return;
        SelectNextWaitingUnit();
    }

    /// <summary>そのタイルに立っている自分のユニット（眷属 → 軍団 → 斥候）。</summary>
    private List<UnitOrders.Unit> MyUnitsAt(int rid)
    {
        var l = new List<UnitOrders.Unit>();
        foreach (var u in UnitOrders.All()) if (UnitOrders.RegionOf(u) == rid) l.Add(u);
        return l;
    }

    // ================= 盤の操作 =================
    /// <summary>盤のタイルを押した。①自分のユニットなら選ぶ ②選んでいるユニットがあれば命令 ③それ以外はタイルを見る。</summary>
    private void OnBoardPick(int id)
    {
        surfaceActionMsg = "";
        var here = MyUnitsAt(id);
        if (here.Count > 0)
        {
            int at = here.FindIndex(u => u.kind == selUnit.kind && u.id == selUnit.id);
            // 同じタイルに何体もいれば、押すたびに次の1体へ
            var pick = at < 0 ? here[0] : here[(at + 1) % here.Count];
            if (at < 0 || here.Count > 1) { SelectUnit(pick, false); return; }
        }
        if (UnitExists(selUnit) && id != UnitOrders.RegionOf(selUnit))
        {
            var before = selUnit;
            bool done = CommandSelectedUnit(id);
            if (done) unitAttackMode = false;
            selectedRegionId = id;
            if (surfaceView != null) { surfaceView.SetSelected(id); surfaceView.MarkDirty(); }
            // 🕹️ 動き終えたら次のユニットへ（Civ と同じ）。⚠ 移動力が残っているあいだは選んだまま
            if (done && UnitExists(before) && !UnitOrders.IsWaiting(before) && UnitOrders.WaitingCount() > 0)
            {
                string keep = surfaceActionMsg;
                SelectNextWaitingUnit();
                surfaceActionMsg = keep;
            }
            RefreshSurfacePanel();
            return;
        }
        selectedRegionId = id;
        if (surfaceView != null) surfaceView.SetSelected(id);
        RefreshSurfacePanel();
    }

    /// <summary>選んでいるユニットに「そのタイルへ」の命令を出す。出せたら true（出せない理由は札に書く）。</summary>
    private bool CommandSelectedUnit(int id)
    {
        var r = SurfaceMap.Get(id);
        int turnNow = turn != null ? turn.CurrentTurn : 1;
        switch (selUnit.kind)
        {
            case UnitOrders.Kind.Kin:
                {
                    var k = KinRoster.Of(selUnit.id); if (k == null) return false;
                    string kn = k.trueName;
                    if (k.injuryTurns > 0) { surfaceActionMsg = "<color=#e05a5a>負傷中（あと" + k.injuryTurns + "ターン）</color>"; return false; }
                    // ⚔️ 敵の軍が立っているなら、まず軍を叩く（タイルは取らない）
                    var enemy = EnemyForce.At(id);
                    if (enemy != null)
                    {
                        bool adj = SurfaceMap.HexDist(SurfaceMap.Get(k.regionId), r) <= 1;
                        if (!adj || KinRoster.MpOf(k) < 1) { surfaceActionMsg = "<color=#e05a5a>" + (adj ? "移動力が残っていない" : "隣にいない（まず近づく）") + "</color>"; return false; }
                        k.mp = KinRoster.MpOf(k) - 1;
                        bool won = EnemyForce.ResolveIntercept(k, enemy);
                        if (surfaceView != null) surfaceView.PopText(id, won ? "撃破！" : "押し返された", won ? "#5cc47c" : "#e05a5a");
                        surfaceActionMsg = won ? "<color=#5cc47c>『" + kn + "』が " + enemy.name + " を撃ち破った。</color>"
                                               : "<color=#e05a5a>『" + kn + "』は押し返された（2ターン負傷）。</color>";
                        UnitOrders.Released(UnitOrders.Kind.Kin, k.individualId);
                        return true;
                    }
                    int cost; string why;
                    string awhy;
                    bool canAtk = KinRoster.CanAttackNow(k, id, out awhy);
                    bool hostile = !r.owned && r.owner != SurfaceMap.OwnerNeutral;
                    if (canAtk && (unitAttackMode || hostile))
                    {
                        if (!KinRoster.TryAttack(k.individualId, id, turnNow)) return false;
                        var rr = SurfaceMap.Get(id);
                        if (surfaceView != null) surfaceView.PopText(id, rr.lastResult, rr.owned ? "#5cc47c" : "#e05a5a");
                        surfaceActionMsg = "<color=#e3a94a>" + rr.name + "：" + rr.lastResult + "</color>";
                        UnitOrders.Released(UnitOrders.Kind.Kin, k.individualId);
                        return true;
                    }
                    if (unitAttackMode) { surfaceActionMsg = "<color=#e05a5a>" + (string.IsNullOrEmpty(awhy) ? "そこは攻められない" : awhy) + "</color>"; return false; }
                    if (KinRoster.CanMoveNow(k, id, out cost, out why))
                    {
                        if (!KinRoster.TryMoveTo(k.individualId, id)) return false;
                        if (surfaceView != null) surfaceView.PopText(id, "-" + cost, "#8ce0a8");
                        surfaceActionMsg = "<color=#5cc47c>『" + kn + "』が移動した（残り移動力 " + KinRoster.MpOf(k) + "）。</color>";
                        UnitOrders.Released(UnitOrders.Kind.Kin, k.individualId);
                        return true;
                    }
                    if (!r.owned && !r.isOcean && SurfaceMap.IsDiscovered(id))
                    {
                        if (KinRoster.SetMarchTarget(k.individualId, id))
                        {
                            int steps = KinRoster.StepsTo(k, id);
                            int eta = Mathf.Max(1, Mathf.CeilToInt((steps - 1) / (float)KinRoster.MovementOf(k)));
                            surfaceActionMsg = "<color=#ffd24a>『" + kn + "』を " + r.name + " へ進軍させる（約" + eta + "ターン）。ターンを終えると動く。</color>";
                            UnitOrders.Released(UnitOrders.Kind.Kin, k.individualId);
                            return true;
                        }
                        surfaceActionMsg = "<color=#e05a5a>そこへは進軍できない（道が塞がれている／まだ見えていない）。</color>";
                        return false;
                    }
                    surfaceActionMsg = "<color=#e05a5a>" + (string.IsNullOrEmpty(why) ? awhy : why) + "</color>";
                    return false;
                }
            case UnitOrders.Kind.Legion:
                {
                    var l = LegionRoster.Get(selUnit.id); if (l == null) return false;
                    string why;
                    bool lHostile = !r.owned && r.owner != SurfaceMap.OwnerNeutral;
                    bool canAs = LegionRoster.CanAssault(l, id, out why);
                    if (unitAttackMode && !canAs) { surfaceActionMsg = "<color=#e05a5a>" + why + "</color>"; return false; }
                    if (canAs && (unitAttackMode || lHostile))
                    {
                        string w2;
                        bool ok = LegionRoster.TryAssault(l.id, id, out w2);
                        surfaceActionMsg = ok ? "<color=#5cc47c>制圧した。</color>" : "<color=#e05a5a>" + w2 + "</color>";
                        if (surfaceView != null) surfaceView.PopText(id, ok ? "制圧" : "攻めきれず", ok ? "#5cc47c" : "#e05a5a");
                        UnitOrders.Released(UnitOrders.Kind.Legion, l.id);
                        return true;
                    }
                    var reach = LegionRoster.ReachableNow(l);
                    if (reach.ContainsKey(id))
                    {
                        string mw;
                        if (!LegionRoster.TryMoveTo(l.id, id, out mw)) { surfaceActionMsg = "<color=#e05a5a>" + mw + "</color>"; return false; }
                        surfaceActionMsg = "<color=#5cc47c>" + LegionRoster.NameOf(l) + " が移動した（残り移動力 " + LegionRoster.MpOf(l) + "）。</color>";
                        UnitOrders.Released(UnitOrders.Kind.Legion, l.id);
                        return true;
                    }
                    if (SurfaceMap.IsPassable(r) && (r.owned || r.owner == SurfaceMap.OwnerNeutral) && LegionRoster.At(id) == null)
                    {
                        LegionRoster.SetMarchTarget(l.id, id);
                        surfaceActionMsg = "<color=#ffd24a>" + LegionRoster.NameOf(l) + " を " + r.name + " へ進軍させる。ターンを終えると動く。</color>";
                        UnitOrders.Released(UnitOrders.Kind.Legion, l.id);
                        return true;
                    }
                    surfaceActionMsg = "<color=#e05a5a>" + why + "</color>";
                    return false;
                }
            case UnitOrders.Kind.Scout:
                {
                    var s = ScoutSystem.Of(selUnit.id); if (s == null) return false;
                    int cost; string why;
                    if (!ScoutSystem.CanMoveNow(s, id, out cost, out why)) { surfaceActionMsg = "<color=#e05a5a>" + why + "</color>"; return false; }
                    if (!ScoutSystem.TryMoveTo(s.id, id)) return false;
                    surfaceActionMsg = "<color=#8cb8e6>斥候が進んだ（残り移動力 " + ScoutSystem.MpOf(s) + "）。</color>";
                    UnitOrders.Released(UnitOrders.Kind.Scout, s.id);
                    return true;
                }
        }
        return false;
    }

    // ================= 盤の色 =================
    /// <summary>選んでいるユニットの「歩ける所（水色＋残る移動力）」「攻められる所（赤）」を盤へ渡す。</summary>
    private void ApplyUnitOverlays()
    {
        if (surfaceView == null) return;
        HashSet<int> mv = null, atk = null; Dictionary<int, int> left = null;
        if (UnitExists(selUnit)) ComputeUnitRanges(selUnit, out left, out atk);
        if (unitAttackMode) left = null;   // ⚔️ 攻める場面では水色を消して赤だけにする（どこを押せば攻めるかを一目に）
        if (left != null && left.Count > 0) mv = new HashSet<int>(left.Keys);
        surfaceView.moveRange = mv;
        surfaceView.moveLeft = left;
        surfaceView.attackRange = atk;
        HashSet<int> po = null;
        if (atk != null)
            foreach (int a in atk)
            {
                var ra = SurfaceMap.Get(a);
                if (ra != null && ra.IsHuman && !HumanRealm.IsCapturable(ra) && EnemyForce.At(a) == null)
                { if (po == null) po = new HashSet<int>(); po.Add(a); }
            }
        surfaceView.pillageOnly = po;
        if (!UnitExists(selUnit)) surfaceView.marchPath = null;
        surfaceView.MarkDirty();
    }

    private void ComputeUnitRanges(UnitOrders.Unit u, out Dictionary<int, int> left, out HashSet<int> atk)
    {
        left = new Dictionary<int, int>(); atk = new HashSet<int>();
        int rid = UnitOrders.RegionOf(u);
        switch (u.kind)
        {
            case UnitOrders.Kind.Kin:
                {
                    var k = KinRoster.Of(u.id);
                    if (k == null || k.injuryTurns > 0) return;
                    int mp = KinRoster.MpOf(k);
                    foreach (int id in KinRoster.ReachableNow(k))
                        left[id] = Mathf.Max(0, mp - KinRoster.PathCost(KinRoster.PathTo(k, id)));
                    // 🐾 Civ と同じ「移動力が1でも残っていれば隣へは入れる」（`CanMoveNow` と合わせる）
                    foreach (var n in SurfaceMap.Neighbors(rid))
                    {
                        int c; string w;
                        if (!left.ContainsKey(n.id) && KinRoster.CanMoveNow(k, n.id, out c, out w)) left[n.id] = Mathf.Max(0, mp - c);
                        string aw;
                        bool foe = EnemyForce.At(n.id) != null || (!n.owned && n.owner != SurfaceMap.OwnerNeutral);
                        if (!(unitAttackMode || foe)) continue;   // 無主の土地は「攻撃」を押したときだけ赤
                        if (EnemyForce.At(n.id) != null ? mp >= 1 : KinRoster.CanAttackNow(k, n.id, out aw)) atk.Add(n.id);
                    }
                    break;
                }
            case UnitOrders.Kind.Legion:
                {
                    var l = LegionRoster.Get(u.id); if (l == null) return;
                    left = LegionRoster.ReachableNow(l);
                    foreach (var n in SurfaceMap.Neighbors(rid))
                    {
                        string w;
                        bool foe = !n.owned && n.owner != SurfaceMap.OwnerNeutral;
                        if ((unitAttackMode || foe) && LegionRoster.CanAssault(l, n.id, out w)) atk.Add(n.id);
                    }
                    break;
                }
            case UnitOrders.Kind.Scout:
                {
                    var s = ScoutSystem.Of(u.id); if (s == null) return;
                    int mp = ScoutSystem.MpOf(s);
                    // 🔭 地形の重みを無視して歩数だけで広げる（`ScoutSystem.PathTo` と同じ決まり）
                    var frontier = new List<int> { rid }; var seen = new HashSet<int> { rid };
                    for (int step = 1; step <= mp && frontier.Count > 0; step++)
                    {
                        var next = new List<int>();
                        foreach (int f in frontier)
                            foreach (var n in SurfaceMap.Neighbors(f))
                            {
                                if (seen.Contains(n.id) || !SurfaceMap.IsPassable(n)) continue;
                                if (!n.owned && n.owner != SurfaceMap.OwnerNeutral) continue;
                                seen.Add(n.id); left[n.id] = mp - step; next.Add(n.id);
                            }
                        frontier = next;
                    }
                    break;
                }
        }
    }

    // ================= 指した先の見込み =================
    private void OnBoardHover(int id)
    {
        if (surfaceView == null) return;
        surfaceView.marchPath = null;
        if (id < 0 || !UnitExists(selUnit) || !SurfaceMap.IsDiscovered(id)) { HideUnitHint(); surfaceView.MarkDirty(); return; }
        string msg = UnitHintFor(id);
        if (string.IsNullOrEmpty(msg)) HideUnitHint();
        else { SetTxt(unitHintText, msg); unitHintBg.gameObject.SetActive(true); unitHintBg.transform.SetAsLastSibling(); }
        surfaceView.MarkDirty();
    }

    private string UnitHintFor(int id)
    {
        var here = MyUnitsAt(id);
        if (here.Count > 0 && !(here.Count == 1 && here[0].kind == selUnit.kind && here[0].id == selUnit.id))
            return "選ぶ：" + UnitOrders.NameOf(here[0]) + (here.Count > 1 ? " ほか" + (here.Count - 1) : "");
        if (id == UnitOrders.RegionOf(selUnit)) return null;
        Dictionary<int, int> left; HashSet<int> atk;
        ComputeUnitRanges(selUnit, out left, out atk);
        var r = SurfaceMap.Get(id);
        int lv;
        if (unitAttackMode && !atk.Contains(id)) return "<color=#6f6889>そこは攻められない（右クリックで攻撃をやめる）</color>";
        if (!unitAttackMode && left.TryGetValue(id, out lv) && !atk.Contains(id)) return "<color=#8cd7ff>移動</color>　着いたら残る移動力 " + lv;
        switch (selUnit.kind)
        {
            case UnitOrders.Kind.Kin:
                {
                    var k = KinRoster.Of(selUnit.id);
                    float pw = KinRoster.ArmyPower(k);
                    if (atk.Contains(id))
                    {
                        var en = EnemyForce.At(id);
                        if (en != null)
                            return "<color=#e05a5a>迎撃</color>　戦力 " + pw.ToString("0") + " vs " + en.power.ToString("0")
                                + "　" + Odds(pw / Mathf.Max(1f, en.power)) + FollowerNote(k);
                        int dk; float ak = KinRoster.AttackPowerVs(k, r, out dk);
                        return AttackHint(r, ak, dk, true) + FollowerNote(k);
                    }
                    if (!r.owned && !r.isOcean)
                    {
                        int steps = KinRoster.StepsTo(k, id);
                        if (steps >= 99) return "<color=#6f6889>そこへは行けない（道が塞がれている）</color>";
                        surfaceView.marchPath = KinRoster.PathTo(k, id);
                        int eta = Mathf.Max(1, Mathf.CeilToInt((steps - 1) / (float)KinRoster.MovementOf(k)));
                        return "<color=#ffd24a>進軍</color>　約" + eta + "ターンで着く　戦力 " + pw.ToString("0") + " vs 防衛 " + SurfaceMap.DefenseOf(id);
                    }
                    return "<color=#6f6889>今ターンには届かない</color>";
                }
            case UnitOrders.Kind.Legion:
                {
                    var l = LegionRoster.Get(selUnit.id);
                    if (atk.Contains(id))
                    {
                        float pw = LegionRoster.SiegePowerOf(l); int def = SurfaceMap.DefenseOf(id);
                        return AttackHint(r, pw, def, false);
                    }
                    if (SurfaceMap.IsPassable(r) && (r.owned || r.owner == SurfaceMap.OwnerNeutral))
                        return "<color=#ffd24a>進軍</color>　ターンを終えると移動力 " + LegionRoster.MovementOf(l) + " ずつ近づく";
                    return "<color=#6f6889>敵領へは隣まで近づいてから攻める</color>";
                }
            case UnitOrders.Kind.Scout:
                return "<color=#6f6889>今ターンには届かない（斥候は敵領に入れない）</color>";
        }
        return null;
    }

    /// <summary>
    /// ⚔️ J3：攻める前に「取れる／荒らすだけ／城砦を削る」と勝ち目を出す。
    /// ⚠ 通しプレイでは、人類の版図（取れない）を眷属で4回攻めて4回とも負傷し、支配は15のまま止まった。
    ///   赤いマスはどれも同じに見え、勝っても取れないことが攻める前に分からなかった。
    /// </summary>
    private static string AttackHint(SurfaceMap.Region r, float pw, int def, bool kin)
    {
        float ratio = pw / Mathf.Max(1f, def);
        string vs = "戦力 " + pw.ToString("0") + " vs 守り " + def;
        if (r.IsHuman && !HumanRealm.IsCapturable(r))
        {
            bool ok = ratio >= (kin ? 1.0f : 0.9f);
            return "<color=#ffa040>荒らすだけ（人類の版図は取れない）</color>　" + vs + "\n"
                + (ok ? "<color=#e3c34a>勝てば踏み越えて立ち、集落の産出を止める（敵対される）</color>"
                      : "<color=#e05a5a>押し返される見込み" + (kin ? "（負傷して動けなくなる）" : "（軍団が傷む）") + "</color>")
                + "\n<color=#9c95b4>取れるのは集落の中心だけ。落とせば版図が丸ごと手に入る</color>";
        }
        if (r.IsHuman)
        {
            int walls = HumanRealm.WallsLeft(r.id);
            return "<color=#e05a5a>集落の中心を攻める</color>　" + vs + "　" + (kin ? Odds(ratio) : LegionOdds(ratio))
                + "\n<color=#9c95b4>" + (walls > 1 ? "城砦が残り " + walls + " 区画（勝つたびに1つ破る）" : "勝てば陥落し、版図が丸ごと手に入る") + "</color>";
        }
        return "<color=#e05a5a>攻めて取る</color>　" + vs + "　" + (kin ? Odds(ratio) : LegionOdds(ratio));
    }

    private static string LegionOdds(float ratio)
        => ratio >= 1.15f ? "<color=#5cc47c>制圧の見込み</color>"
         : ratio >= 0.9f ? "<color=#e3c34a>辛勝（軍団が大きく傷む）</color>"
         : "<color=#e05a5a>落とせない見込み</color>";

    /// <summary>配下を連れていない眷属には一言添える（1体で殴り込むと負けやすい）。</summary>
    private static string FollowerNote(KinRoster.Kin k)
        => k != null && k.followers.Count == 0 ? "\n<color=#e08a3c>配下を連れていない ― 『眷属』で配下を付けると戦力が上がる</color>" : "";

    private static string Odds(float ratio)
        => ratio >= 1.25f ? "<color=#5cc47c>完勝の見込み</color>"
         : ratio >= 1.0f ? "<color=#e3c34a>辛勝（配下を失う）</color>"
         : ratio >= 0.7f ? "<color=#e08a3c>負ける見込み</color>"
         : "<color=#e05a5a>壊滅する見込み</color>";

    private void HideUnitHint() { if (unitHintBg != null && unitHintBg.gameObject.activeSelf) unitHintBg.gameObject.SetActive(false); }

    /// <summary>毎フレーム：見込みの札をマウスの右下に付いて行かせる。</summary>
    private void TickUnitHint()
    {
        if (unitHintBg == null || !unitHintBg.gameObject.activeSelf) return;
        if (!surfaceModeOn) { HideUnitHint(); return; }
        var parent = unitHintBg.rectTransform.parent as RectTransform;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, PointerInput.Position, null, out local)) return;
        Vector2 pref = unitHintText.GetPreferredValues(unitHintText.text);
        var rt = unitHintBg.rectTransform;
        rt.sizeDelta = new Vector2(pref.x + 20f, pref.y + 8f);
        float halfW = parent.rect.width * 0.5f;
        float x = local.x + 22f;
        if (x + rt.sizeDelta.x > halfW - 8f) x = local.x - 22f - rt.sizeDelta.x;   // 右端では左に出す
        // ⚠ J3 で札が4行になり、下の方を指すと画面の下へはみ出した → 下端では指の上に出す
        float y = local.y - 18f;
        float halfH = parent.rect.height * 0.5f;
        if (y - rt.sizeDelta.y < -halfH + 8f) y = local.y + 18f + rt.sizeDelta.y;
        rt.anchoredPosition = new Vector2(x, y);
    }

    // ================= 札 =================
    private void RefreshUnitCard()
    {
        if (unitCard == null) return;
        if (!UnitExists(selUnit)) selUnit = new UnitOrders.Unit(UnitOrders.Kind.None, -1);
        bool menu = surfaceMenuTab >= 0;
        bool showCard = surfaceModeOn && !menu && UnitExists(selUnit);
        if (unitCard.gameObject.activeSelf != showCard) unitCard.gameObject.SetActive(showCard);
        RefreshUnitRoster(surfaceModeOn && !menu);
        if (!showCard) return;

        string sig = selUnit + "|" + UnitOrders.RegionOf(selUnit) + "|" + UnitSigOf(selUnit) + "|" + surfaceActionMsg
                   + "|" + UnitOrders.IsStood(selUnit.kind, selUnit.id) + UnitOrders.IsFortified(selUnit.kind, selUnit.id)
                   + "|" + (turn != null ? turn.CurrentTurn : 0) + "|" + unitAttackMode;
        if (sig == unitCardSig) return;
        unitCardSig = sig;

        for (int i = unitActs.childCount - 1; i >= 0; i--) { var g = unitActs.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        var acts = new List<UnitAct>();
        Dictionary<int, int> left; HashSet<int> atk;
        ComputeUnitRanges(selUnit, out left, out atk);
        bool stood = UnitOrders.IsStood(selUnit.kind, selUnit.id), fort = UnitOrders.IsFortified(selUnit.kind, selUnit.id);
        var u = selUnit;
        string state; string stateHex;

        switch (selUnit.kind)
        {
            case UnitOrders.Kind.Kin:
                {
                    var k = KinRoster.Of(selUnit.id);
                    var v = MinionRoster.Get(k.individualId);
                    unitFace.sprite = (v != null ? MinionSprite.ByIndex(v.catalogIndex) : null) ?? IconFactory.Get("眷属");
                    SetTxt(unitKindText, "眷属・司令官");
                    SetTxt(unitNameText, "◆ " + k.trueName);
                    int mp = KinRoster.MpOf(k), mx = KinRoster.MovementOf(k);
                    SetTxt(unitStatsText, "移動力 " + Pips(mp, mx) + " " + mp + "/" + mx
                        + "　　戦力 <b>" + KinRoster.ArmyPower(k).ToString("0") + "</b>"
                        + "　　Lv " + MinionRoster.LevelOf(k.individualId)
                        + "　　率いる配下 " + k.followers.Count
                        + "　　<color=#9c95b4>" + SurfaceMap.Get(k.regionId).name + "</color>");
                    state = k.injuryTurns > 0 ? "負傷中（あと" + k.injuryTurns + "ターン）"
                          : k.marchTarget >= 0 ? "進軍中 → " + SurfaceMap.Get(k.marchTarget).name : null;
                    stateHex = k.injuryTurns > 0 ? "#e05a5a" : "#ffd24a";
                    bool home = SurfaceMap.Get(k.regionId).owned;
                    string dwhy; bool canDrill = KinRoster.CanDrill(k, out dwhy) && home;
                    acts.Add(Info("移動", left.Count > 0, mp <= 0 ? "移動力が残っていない" : "歩ける所がない", "水色のマスを押すと、そこへ歩きます（数字＝着いたら残る移動力）。"));
                    acts.Add(AttackAct("攻撃", k.injuryTurns <= 0 && mp >= 1 && HasAttackTarget(selUnit), "隣に攻められる土地も相手もいない（まず近づく）"));
                    if (k.marchTarget >= 0)
                        acts.Add(Act("進軍をやめる", true, "", () => { KinRoster.SetMarchTarget(k.individualId, -1); surfaceActionMsg = "<color=#9c95b4>進軍を取りやめた。</color>"; RefreshSurfacePanel(); }));
                    else acts.Add(Info("進軍", true, "", "遠くのマスを押すと、何ターンかけてそこへ向かいます（指すと日数が出ます）。"));
                    if (fort) acts.Add(Act("守りを解く", true, "", () => { UnitOrders.Released(u.kind, u.id); RefreshSurfacePanel(); }));
                    else acts.Add(Act("守らせる", home, "自領の上にいない", () =>
                    {
                        KinRoster.SetGarrison(k.individualId, k.regionId);
                        UnitOrders.Fortify(u.kind, u.id);
                        surfaceActionMsg = "<color=#5cc47c>『" + k.trueName + "』がここを守る（解くまで命令を聞かない）。</color>";
                        AfterOrder(u);
                    }));
                    int ddp = KinRoster.DrillCost(k), dmat = KinRoster.DrillMaterial(k);
                    acts.Add(Act("鍛錬 -" + ddp + "DP", canDrill, home ? dwhy : "自領の上でしか鍛えられない", () =>
                    {
                        if (KinRoster.TryDrill(k.individualId))
                        {
                            surfaceActionMsg = "<color=#5cc47c>『" + k.trueName + "』を鍛えた（Lv" + MinionRoster.LevelOf(k.individualId) + "）。</color>";
                            if (surfaceView != null) surfaceView.PopText(k.regionId, "+" + KinRoster.DrillExp + " exp", "#8ce0a8");
                            UnitOrders.Stand(u.kind, u.id);
                        }
                        AfterOrder(u);
                    }, "自領で腰を据えて鍛える（+" + KinRoster.DrillExp + "exp・-" + ddp + "DP・-" + dmat + "素材）。このターンは動けなくなる。"));
                    break;
                }
            case UnitOrders.Kind.Legion:
                {
                    var l = LegionRoster.Get(selUnit.id);
                    var cls = LegionRoster.ClassOf(l);
                    unitFace.sprite = MinionSprite.ByIndex(l.catalogIndex) ?? IconFactory.Get("軍団");
                    SetTxt(unitKindText, "<color=" + LegionRoster.ClassHex(cls) + ">軍団・" + LegionRoster.ClassName(cls) + "</color>");
                    SetTxt(unitNameText, LegionRoster.NameOf(l));
                    int mp = LegionRoster.MpOf(l), mx = LegionRoster.MovementOf(l);
                    SetTxt(unitStatsText, "移動力 " + Pips(mp, mx) + " " + mp + "/" + mx
                        + "　　戦力 <b>" + LegionRoster.PowerOf(l).ToString("0") + "</b>"
                        + "　　残兵 " + l.strength + "%　　練度 Lv" + l.level
                        + "　　<color=#9c95b4>" + SurfaceMap.Get(l.regionId).name + "</color>");
                    state = l.marchTarget >= 0 ? "進軍中 → " + SurfaceMap.Get(l.marchTarget).name
                          : l.foughtThisTurn ? "このターンは戦った" : null;
                    stateHex = "#ffd24a";
                    acts.Add(Info("移動", left.Count > 0, mp <= 0 ? "移動力が残っていない" : "歩ける所がない", "水色のマスを押すと、そこへ歩きます。"));
                    acts.Add(AttackAct("攻める", HasAttackTarget(selUnit), "隣に攻められる土地がない（まず近づく）"));
                    if (l.marchTarget >= 0)
                        acts.Add(Act("進軍をやめる", true, "", () => { LegionRoster.SetMarchTarget(l.id, -1); surfaceActionMsg = "<color=#9c95b4>進軍を取りやめた。</color>"; RefreshSurfacePanel(); }));
                    else acts.Add(Info("進軍", true, "", "遠くの自領・無主のマスを押すと、ターンを終えるたびにそこへ近づきます。"));
                    if (fort) acts.Add(Act("守りを解く", true, "", () => { UnitOrders.Released(u.kind, u.id); RefreshSurfacePanel(); }));
                    else acts.Add(Act("守らせる", true, "", () => { UnitOrders.Fortify(u.kind, u.id); surfaceActionMsg = "<color=#5cc47c>ここを守る（解くまで命令を聞かない）。</color>"; AfterOrder(u); }));
                    acts.Add(Act("解散", true, "", () =>
                    {
                        LegionRoster.Disband(l.id);
                        surfaceActionMsg = "<color=#9c95b4>軍団を解散した。</color>";
                        selUnit = new UnitOrders.Unit(UnitOrders.Kind.None, -1); selectedLegionId = -1;
                        RefreshSurfacePanel();
                    }, "この軍団を解散します（取り消せません）。", true));
                    break;
                }
            default:
                {
                    var s = ScoutSystem.Of(selUnit.id);
                    unitFace.sprite = IconFactory.Get("先触れ");
                    SetTxt(unitKindText, "<color=#8cb8e6>斥候</color>");
                    SetTxt(unitNameText, "斥候 #" + s.id);
                    int mp = ScoutSystem.MpOf(s), mx = ScoutSystem.Movement;
                    SetTxt(unitStatsText, "移動力 " + Pips(mp, mx) + " " + mp + "/" + mx + "　　視界 " + ScoutSystem.Vision
                        + "　　<color=#9c95b4>戦えない・地形の重さを無視・" + SurfaceMap.Get(s.regionId).name + "</color>");
                    state = null; stateHex = "#ffd24a";
                    acts.Add(Info("移動", left.Count > 0, mp <= 0 ? "移動力が残っていない" : "歩ける所がない", "水色のマスを押すと、そこまで進みます（通った道の周りが見えるようになる）。"));
                    if (fort) acts.Add(Act("見張りを解く", true, "", () => { UnitOrders.Released(u.kind, u.id); RefreshSurfacePanel(); }));
                    else acts.Add(Act("見張る", true, "", () => { UnitOrders.Fortify(u.kind, u.id); surfaceActionMsg = "<color=#8cb8e6>ここで見張る（解くまで命令を聞かない）。</color>"; AfterOrder(u); }));
                    break;
                }
        }
        acts.Add(Act("待機", !stood, "このターンは既に待機している", () =>
        {
            UnitOrders.Stand(u.kind, u.id);
            surfaceActionMsg = "<color=#9c95b4>このターンは動かさない。</color>";
            AfterOrder(u);
        }, "このターンは動かさない（次のターンにまた命令を待つ）。"));
        acts.Add(Act("次へ  <size=75%><color=#9c95b4>[Tab]</color></size>", true, "", SelectNextWaitingUnit, "命令を待っている次のユニットへ。"));

        // 状態の札
        if (state == null)
        {
            if (fort) { state = "守っている"; stateHex = "#5cc47c"; }
            else if (stood) { state = "このターンは待機"; stateHex = "#9c95b4"; }
            else if (UnitOrders.IsWaiting(selUnit)) { state = "命令を待っている"; stateHex = "#e05a5a"; }
            else { state = "命令済み"; stateHex = "#9c95b4"; }
        }
        SetTxt(unitStateText, "<color=" + stateHex + ">" + state + "</color>");
        SetTxt(unitMsgText, string.IsNullOrEmpty(surfaceActionMsg)
            ? "<color=#6f6889>盤の水色のマスで移動、赤いマスで攻撃、遠くのマスで進軍。無主の土地を取るときは「攻撃」。右クリックで選択を外す。</color>"
            : surfaceActionMsg);

        // 並べる（幅は等分）
        float aw = unitActs.rect.width > 10f ? unitActs.rect.width : UnitCardW - 164f;
        float gap = 6f, w = (aw - gap * (acts.Count - 1)) / acts.Count;
        for (int i = 0; i < acts.Count; i++)
        {
            var a = acts[i];
            var b = PrimaryButton(unitActs, a.label, a.danger ? BLOOD_DK : PANEL2, a.ok ? (a.info ? C("#8ce0a8") : TEXT) : FAINT, a.run);
            Place((RectTransform)b.transform, i * (w + gap), 0, w, 46);
            b.interactable = a.ok;
            var lb = b.GetComponentInChildren<TMP_Text>();
            if (lb != null) { lb.fontSize = 13f; lb.enableWordWrapping = false; lb.enableAutoSizing = true; lb.fontSizeMin = 9f; lb.fontSizeMax = 13f; }
            string tip = a.ok ? a.tip : a.why;
            if (!string.IsNullOrEmpty(tip)) AddTooltip(b.gameObject, tip);
        }
    }

    /// <summary>「攻撃」：押すと攻める場面に入る（もう一度押すと戻る）。</summary>
    private UnitAct AttackAct(string label, bool ok, string why)
        => new UnitAct
        {
            label = unitAttackMode ? label + "をやめる" : label, ok = ok || unitAttackMode, why = why, danger = unitAttackMode,
            tip = "押すと、隣の攻められるマスが赤くなります。赤いマスを押すと攻めます（無主の土地を取るときもこれ）。",
            run = () =>
            {
                unitAttackMode = !unitAttackMode;
                surfaceActionMsg = unitAttackMode ? "<color=#e05a5a>攻める先の赤いマスを押す（右クリックでやめる）。マスを指すと勝ち目が出ます。</color>" : "";
                unitCardSig = null;
                RefreshSurfacePanel();
            }
        };

    /// <summary>隣に「攻撃」で攻められる先が1つでもあるか（無主の土地も含む）。</summary>
    private bool HasAttackTarget(UnitOrders.Unit u)
    {
        bool keep = unitAttackMode; unitAttackMode = true;
        Dictionary<int, int> l; HashSet<int> a;
        ComputeUnitRanges(u, out l, out a);
        unitAttackMode = keep;
        return a.Count > 0;
    }

    private struct UnitAct { public string label, why, tip; public bool ok, info, danger; public UnityAction run; }

    private UnitAct Act(string label, bool ok, string why, UnityAction run, string tip = null, bool danger = false)
        => new UnitAct { label = label, ok = ok, why = why, run = run, tip = tip, danger = danger };

    /// <summary>説明だけのボタン（移動・攻撃・進軍は盤のマスを押して決める＝札では手順を教えるだけ）。</summary>
    private UnitAct Info(string label, bool ok, string why, string tip)
        => new UnitAct
        {
            label = label, ok = ok, why = why, tip = tip, info = true,
            run = () => { surfaceActionMsg = "<color=#8ce0a8>" + tip + "</color>"; RefreshSurfacePanel(); }
        };

    /// <summary>命令を出したあと：次に命令を待つユニットがいればそちらへ（Civ と同じ）。</summary>
    private void AfterOrder(UnitOrders.Unit u)
    {
        string keep = surfaceActionMsg;
        if (UnitOrders.WaitingCount() > 0) SelectNextWaitingUnit();
        surfaceActionMsg = keep;
        RefreshSurfacePanel();
    }

    private static string Pips(int mp, int mx)
    {
        var sb = new System.Text.StringBuilder("<color=#8ce0a8>");
        for (int i = 0; i < mx && i < 8; i++) sb.Append(i < mp ? "●" : "○");
        return sb.Append("</color>").ToString();
    }

    private static string UnitSigOf(UnitOrders.Unit u)
    {
        switch (u.kind)
        {
            case UnitOrders.Kind.Kin: { var k = KinRoster.Of(u.id); return k == null ? "" : k.mp + "," + k.injuryTurns + "," + k.marchTarget + "," + k.followers.Count + "," + MinionRoster.LevelOf(k.individualId); }
            case UnitOrders.Kind.Legion: { var l = LegionRoster.Get(u.id); return l == null ? "" : l.mp + "," + l.strength + "," + l.marchTarget + "," + l.foughtThisTurn + "," + l.level; }
            case UnitOrders.Kind.Scout: { var s = ScoutSystem.Of(u.id); return s == null ? "" : s.mp.ToString(); }
        }
        return "";
    }

    // ================= 命令待ちの列 =================
    private void RefreshUnitRoster(bool show)
    {
        if (unitRoster == null) return;
        var all = UnitOrders.All();
        show = show && all.Count > 0;
        if (unitRoster.gameObject.activeSelf != show) unitRoster.gameObject.SetActive(show);
        if (!show) return;

        var sb = new System.Text.StringBuilder();
        sb.Append(selUnit).Append('|');
        foreach (var u in all) sb.Append(u).Append(UnitOrders.IsWaiting(u) ? "!" : ".").Append(',');
        string sig = sb.ToString();
        if (sig == unitRosterSig) return;
        unitRosterSig = sig;

        var rt = unitRoster.rectTransform;
        for (int i = rt.childCount - 1; i >= 0; i--) { var g = rt.GetChild(i).gameObject; g.SetActive(false); Destroy(g); }
        int waiting = UnitOrders.WaitingCount();
        var lbl = Text(unitRoster, "ユニット\n<color=" + (waiting > 0 ? "#e05a5a" : "#9c95b4") + ">" + waiting + " 体が命令待ち</color>",
            11.5f, MUTED, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Place(lbl.rectTransform, 10, 4, 112, UnitRosterH - 8);

        const int maxShown = 14;
        float x = 124f, s = 48f, gap = 6f;
        for (int i = 0; i < all.Count && i < maxShown; i++)
        {
            var u = all[i];
            bool sel = u.kind == selUnit.kind && u.id == selUnit.id;
            bool wait = UnitOrders.IsWaiting(u);
            var cell = Panel(unitRoster, "Unit_" + u, sel ? SEL : PANEL2);
            Place(cell.rectTransform, x, 7, s, s);
            Outline(cell, sel ? C("#ffd24a") : LINE);
            var art = new GameObject("Art", typeof(RectTransform)).AddComponent<Image>();
            art.rectTransform.SetParent(cell.rectTransform, false);
            art.raycastTarget = false; art.preserveAspect = true;
            art.sprite = FaceOf(u);
            art.color = wait || sel ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            Place(art.rectTransform, 4, 4, s - 8, s - 8);
            if (wait)
            {
                var bang = Panel(cell, "Bang", BLOOD);
                Place(bang.rectTransform, s - 14, -6, 18, 18);
                bang.raycastTarget = false;
                var bt = Text(bang, "!", 12f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                StretchOffset(bt.rectTransform, 0, 0, 0, 0);
            }
            var btn = cell.gameObject.AddComponent<Button>(); btn.targetGraphic = cell;
            var uu = u;
            btn.onClick.AddListener(() => SelectUnit(uu, true));
            AddTooltip(cell.gameObject, UnitOrders.NameOf(u) + (wait ? "（命令を待っている）" : ""));
            x += s + gap;
        }
        if (all.Count > maxShown)
        {
            var more = Text(unitRoster, "+" + (all.Count - maxShown), 13f, MUTED, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(more.rectTransform, x, 7, 40, s); x += 46f;
        }
        rt.sizeDelta = new Vector2(x + 6f, UnitRosterH);
    }

    private Sprite FaceOf(UnitOrders.Unit u)
    {
        switch (u.kind)
        {
            case UnitOrders.Kind.Kin:
                {
                    var k = KinRoster.Of(u.id); var v = k != null ? MinionRoster.Get(k.individualId) : null;
                    return (v != null ? MinionSprite.ByIndex(v.catalogIndex) : null) ?? IconFactory.Get("眷属");
                }
            case UnitOrders.Kind.Legion:
                {
                    var l = LegionRoster.Get(u.id);
                    return (l != null ? MinionSprite.ByIndex(l.catalogIndex) : null) ?? IconFactory.Get("軍団");
                }
        }
        return IconFactory.Get("先触れ");
    }
}
