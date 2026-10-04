using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⚔️ <b>遠征</b>（④-b）── こちらの魔物を、他所のダンジョンへ攻め入らせる。
///
/// <b>ユーザーが決めたこと（2026-09-07）</b>
///   「そもそも遠征に行く魔物を選択制にすればいい。防衛用の魔物は遠征に生かせないっていう
///     選択肢も生まれる。遠征は基本総力戦なので、遠征に生かせることのできる数に制限なし。」
///
///   → <b>連れて行ける数に上限は無い</b>（統率LPは効かせない）。
///     その代わり <b>連れて行った個体は迷宮の防衛に立てない</b>。
///     だから「この個体は遠征に出す／この個体は守りに残す」が<b>編成そのもの</b>になり、
///     遠征中に迷宮が攻められても<b>中断や自動撤退という特別扱いは要らない</b>
///     ―― 留守が薄いのは<b>プレイヤーが決めたこと</b>だから。
///
/// ⚠⚠ <b>「防衛に立てない」は `KinRoster.IsAwayFromDungeon` に流し込む。</b>
///   隊・ボス・在陣・反芻など<b>11か所</b>が既にあの1つの問いを見ているので、
///   ここに乗せれば全部が自動的に正しくなる。別の判定を新しく撒くと必ず片方だけ古くなる。
///
/// ⚠ 同時に走る遠征は<b>1つだけ</b>（「総力戦」なので分ける意味がない）。
///
/// 純static・実行時保持。関連: [[NestSystem]] [[DungeonSnapshot]] [[KinRoster]] [[MinionRank]]。
/// </summary>
public static class Expedition
{
    public enum Phase { None = 0, Forming = 1, Descending = 2 }

    public class Party
    {
        public int nestIndex = -1;
        /// <summary>率いる眷属の個体ID。⚠ 眷属だけが地上を歩けるので、遠征も眷属が率いる。</summary>
        public int leaderId = -1;
        /// <summary>連れて行く個体（<b>上限なし</b>）。</summary>
        public List<int> members = new List<int>();
        public Phase phase = Phase.None;
        /// <summary>いま何層目にいるか（0＝最上階）。</summary>
        public int floor;
        public int startedTurn = -1;
        /// <summary>この遠征で失った個体の数（報告用）。</summary>
        public int lost;
        /// <summary>
        /// 同じ階を抜けられずに終わった波の数。
        /// ⚠ これが無いと、抜けられない隊が<b>永久に同じ階へ挑み続ける</b>（撤退も制覇もしないので
        ///   遠征が終わらず、出した個体が二度と迷宮の守りに戻らない）。
        /// </summary>
        public int stalled;
    }

    /// <summary>何波足踏みしたら引き上げるか。</summary>
    public const int MaxStall = 3;

    private static Party current;
    public static Party Current => current;
    public static bool Active => current != null && current.phase != Phase.None;
    public static bool Descending => current != null && current.phase == Phase.Descending;

    public static void Reset() { current = null; }

    /// <summary>
    /// 🗺️ その個体は遠征に出ているか。
    /// ⚠ `KinRoster.IsAwayFromDungeon` から呼ばれる（＝迷宮の編成から外れる唯一の道）。
    /// </summary>
    public static bool IsOnExpedition(int individualId)
    {
        if (current == null || current.phase == Phase.None) return false;
        if (current.leaderId == individualId) return true;
        return current.members.Contains(individualId);
    }

    // ============ 宣言 ============
    /// <summary>遠征を宣言できるか。⚠ 理由は必ず返す（押せないボタンの理由が見えないのが一番困る）。</summary>
    public static bool CanDeclare(int kinIndividualId, int nestIndex, out string why)
    {
        why = "";
        if (Active) { why = "すでに遠征中です（同時に走らせられるのは1つ）"; return false; }
        var k = KinRoster.Of(kinIndividualId);
        if (k == null) { why = "眷属がいません"; return false; }
        if (k.injuryTurns > 0) { why = "負傷中（あと" + k.injuryTurns + "ターン）"; return false; }
        var n = NestSystem.At(nestIndex);
        if (n == null) { why = "そのダンジョンはありません"; return false; }
        if (n.conquered) { why = "すでに制覇済みです"; return false; }
        var c = SurfaceMap.Get(n.regionId);
        var here = SurfaceMap.Get(k.regionId);
        if (c == null || here == null) { why = "場所が不明です"; return false; }
        if (SurfaceMap.HexDist(here, c) > 1) { why = "入口まで進軍してください（隣接するか、その上に立つ）"; return false; }
        return true;
    }

    /// <summary>遠征を宣言する。まだ突入はせず、<b>誰を連れて行くかを選ぶ段</b>に入る。</summary>
    public static bool Declare(int kinIndividualId, int nestIndex)
    {
        string why;
        if (!CanDeclare(kinIndividualId, nestIndex, out why))
        { Debug.LogWarning("⚠️ 遠征できません：" + why); return false; }

        var n = NestSystem.At(nestIndex);
        current = new Party { nestIndex = nestIndex, leaderId = kinIndividualId, phase = Phase.Forming };
        // 🗺️ 率いる眷属の麾下は、そのまま遠征隊の初期メンバーになる
        var k = KinRoster.Of(kinIndividualId);
        foreach (var f in k.followers) if (!current.members.Contains(f)) current.members.Add(f);

        Debug.Log("⚔️『遠征を宣言』『" + k.trueName + "』が " + n.snap.name + "（" + n.snap.KindName
            + "・" + n.snap.FloorCount + "層）へ。連れて行く個体を選ぶ");
        NotifySystem.Push("<b>" + n.snap.name + " への遠征</b>を宣言 ― 連れて行く個体を選ぶ"
            + "（<b>上限なし。連れて行った個体は迷宮の守りに立てない</b>）", NotifySystem.Kind.Story, n.regionId);
        return true;
    }

    /// <summary>宣言を取り消す（まだ突入していないあいだだけ）。</summary>
    public static bool Cancel()
    {
        if (current == null || current.phase != Phase.Forming) return false;
        Debug.Log("↩️『遠征を取りやめた』");
        current = null;
        return true;
    }

    // ============ 編成 ============
    public static bool CanAddMember(int individualId, out string why)
    {
        why = "";
        if (current == null || current.phase != Phase.Forming) { why = "編成できる段ではありません"; return false; }
        var v = MinionRoster.Get(individualId);
        if (v == null) { why = "その個体はいません"; return false; }
        if (current.members.Contains(individualId)) { why = "すでに連れて行きます"; return false; }
        if (individualId == current.leaderId) { why = "率いる眷属です"; return false; }
        // 🗺️ 他の眷属とその麾下は連れて行けない（別の隊として地上に出ている）
        if (KinRoster.IsKin(individualId)) { why = "他の眷属です"; return false; }
        var lead = KinRoster.LeaderOfFollower(individualId);
        if (lead != null && lead.individualId != current.leaderId) { why = "『" + lead.trueName + "』の配下です"; return false; }
        return true;
    }

    /// <summary>
    /// 連れて行く個体を足す。⚠ <b>上限は無い。</b>
    /// ⚠ 迷宮の隊やボスに就いている個体は、ここで<b>自動的に外す</b>
    ///   （「先に外してください」と突き返すと、20体連れて行くのに20回往復させることになる）。
    /// </summary>
    public static bool AddMember(int individualId)
    {
        string why;
        if (!CanAddMember(individualId, out why)) { Debug.LogWarning("⚠️ 連れて行けません：" + why); return false; }
        var fm = DungeonFeatureManager.Instance;
        if (fm != null)
        {
            if (fm.SquadFloorOfIndividual(individualId) >= 0) fm.SquadRemoveIndividual(individualId);
            if (fm.IsIndividualBoss(individualId)) fm.RemovePlacedOfIndividual(individualId);
        }
        current.members.Add(individualId);
        return true;
    }

    public static bool RemoveMember(int individualId)
    {
        if (current == null || current.phase != Phase.Forming) return false;
        return current.members.Remove(individualId);
    }

    /// <summary>連れて行ける候補（迷宮に居て、まだ選んでいない個体）。</summary>
    public static List<int> Candidates()
    {
        var l = new List<int>();
        if (current == null || current.phase != Phase.Forming) return l;
        string why;
        foreach (var v in MinionRoster.All) if (CanAddMember(v.id, out why)) l.Add(v.id);
        return l;
    }

    /// <summary>遠征隊の総戦力。⚠ 地上の野戦と<b>同じ物差し</b>を使う（別の式を作らない）。</summary>
    public static float PartyPower()
    {
        if (current == null) return 0f;
        float p = KinRoster.UnitPower(current.leaderId) * 1.6f;
        foreach (var id in current.members) p += KinRoster.UnitPower(id);
        var k = KinRoster.Of(current.leaderId);
        if (k != null) p *= KinPromotion.PowerMult(k);
        return p;
    }

    // ============ 突入 ============
    public static bool Launch(int turn)
    {
        if (current == null || current.phase != Phase.Forming) return false;
        if (current.members.Count == 0)
        { Debug.LogWarning("⚠️ 連れて行く個体が1体もいません。"); return false; }
        var n = NestSystem.At(current.nestIndex);
        if (n == null) return false;

        current.phase = Phase.Descending;
        current.floor = 0;
        current.startedTurn = turn;
        var k = KinRoster.Of(current.leaderId);
        Debug.Log("🕳️『突入』『" + k.trueName + "』と " + current.members.Count + " 体が " + n.snap.name
            + " へ攻め入った（戦力" + PartyPower().ToString("0") + " vs 守り" + n.snap.ThreatScore().ToString("0") + "）");
        NotifySystem.Push("<b>" + n.snap.name + " へ突入</b> ― " + (current.members.Count + 1) + "体"
            + "（<b>この個体たちは迷宮の守りに立たない</b>）", NotifySystem.Kind.Story, n.regionId);
        return true;
    }

    /// <summary>引き上げる。⚠ 連れて行った個体は<b>失わない</b>（傷ついて帰る）。</summary>
    public static void Retreat(string why)
    {
        if (current == null || current.phase != Phase.Descending) return;
        var n = NestSystem.At(current.nestIndex);
        var k = KinRoster.Of(current.leaderId);
        if (k != null) k.injuryTurns = Mathf.Max(k.injuryTurns, 2);
        Debug.Log("↩️『撤退』" + (n != null ? n.snap.name : "遠征先") + " から引き上げた（" + why
            + "／" + current.floor + "層まで／失った配下 " + current.lost + "体）");
        NotifySystem.Push("<b>撤退</b> ― " + (n != null ? n.snap.name : "") + " の " + current.floor
            + "層まで進んだ（失った配下 " + current.lost + "体）", NotifySystem.Kind.Loss);
        // ⚠ 盤も一緒に畳む。残すと次の遠征で階層 index がぶつかる（→ [[RaidBoard]]）
        RaidBoard.Teardown();
        current = null;
    }

    // ============ 波と一緒に進む ============
    // ⚠⚠ **遠征は「波」と同じ時間で進む。**
    //   盤の上の戦闘は数秒かかるので、ターンの解決の途中で即決できない。
    //   → こちらの迷宮が攻められている**その同じ戦闘フェーズ**で、遠征先の戦いも進む。
    //     「留守が空く」がそのまま画になる（1つの波＝遠征の1層）。

    /// <summary>
    /// ⚔️ 戦闘フェーズの頭。遠征先の盤を用意し、その階の守りと侵入者を立てる。
    /// </summary>
    public static void OnBattleStart()
    {
        if (current == null || current.phase != Phase.Descending) return;
        var n = NestSystem.At(current.nestIndex);
        if (n == null || n.conquered) { EndRaid(); return; }

        // 盤がまだ無ければ建てる（宣言から突入までにセーブ・ロードを挟んでも復帰できる）
        if (!RaidBoard.Active || RaidBoard.Built != n.snap) RaidBoard.Build(n.snap);
        RaidBoard.SpawnGuards(n.snap, current.floor);

        var party = new List<int>(current.members);
        party.Insert(0, current.leaderId);
        int sent = RaidBoard.SpawnRaiders(n.snap, current.floor, party);
        Debug.Log("⚔️『遠征』" + n.snap.name + " の " + (current.floor + 1) + "層へ " + sent + " 体が踏み込んだ");
        NotifySystem.Push("<b>" + n.snap.name + "</b> の " + (current.floor + 1) + "層へ踏み込んだ（"
            + sent + "体）", NotifySystem.Kind.Story, n.regionId);
    }

    /// <summary>
    /// ⚔️ 戦闘フェーズの終わり。その階を抜けたかで進退を決める。
    /// ⚠ <b>抜けた＝最深部に着いた</b>（守りの全滅ではない → [[RaidBoard]]）。
    /// </summary>
    public static void OnBattleEnd(int turn)
    {
        if (current == null || current.phase != Phase.Descending) return;
        var n = NestSystem.At(current.nestIndex);
        if (n == null) { EndRaid(); return; }

        bool cleared = RaidBoard.FloorCleared(current.floor);
        int alive = RaidBoard.RaidersAlive(current.floor);
        RaidBoard.ClearRaidersOn(current.floor);   // 生き残りは次の階へ（傷は癒えて踏み込む）

        if (cleared)
        {
            // 💥 **抜いた階の守りは実際に減って、そのまま残る**（敵も経営する・段②）。
            //   ⚠⚠ いままでは次に来ると元通りだった ―― 削っても何も残らないので、
            //     遠征は「同じ固定ダンジョンを何度も殴る」行為になっていた。
            //   ⚠ 失うのは**魔王の迷宮だけ**。野良の巣には経営する主が居ないので、そのまま。
            if (n.rivalIndex >= 0) { RivalBrain.OnFloorFallen(n.rivalIndex, current.floor); VictorySystem.SetBackRite(n.rivalIndex); }   // ◆ 儀を押し戻す

            bool last = current.floor >= n.snap.FloorCount - 1;
            if (last)
            {
                NestSystem.OnConquered(current.nestIndex, turn, current.leaderId);
                var k = KinRoster.Of(current.leaderId);
                if (k != null) { k.conquests++; KinPromotion.AddMerit(k, 8, "ダンジョンを制覇した"); }
                Debug.Log("🏆『制覇』" + n.snap.name + " の主を討ち取った（失った配下 " + current.lost + "体）");
                EndRaid();
                return;
            }
            current.floor++;
            current.stalled = 0;
            Debug.Log("🕳️『踏破』" + n.snap.name + " の " + current.floor + "層まで降りた");
            return;
        }

        // 抜けられなかった。全滅していれば遠征は終わり、生き残っていれば次の波でもう一度その階を挑む。
        if (alive <= 0 || current.members.Count == 0)
        {
            Retreat("押し返された");
            return;
        }
        current.stalled++;
        if (current.stalled >= MaxStall)
        {
            Retreat("同じ階を" + MaxStall + "波抜けられなかった");
            return;
        }
        Debug.Log("⏳『足踏み』" + n.snap.name + " の " + (current.floor + 1) + "層を抜けられなかった（残り "
            + (current.members.Count + 1) + "体／あと" + (MaxStall - current.stalled) + "波で引き上げ）");
        NotifySystem.Push("<b>" + n.snap.name + "</b> の " + (current.floor + 1) + "層を抜けられなかった"
            + "（あと<b>" + (MaxStall - current.stalled) + "波</b>で引き上げ）", NotifySystem.Kind.Info, n.regionId);
    }

    /// <summary>遠征を畳んで盤を片付ける。⚠ 終わり方は制覇・撤退の両方でここを通す。</summary>
    private static void EndRaid()
    {
        RaidBoard.Teardown();
        current = null;
    }

    /// <summary>
    /// ⚔️ 遠征に出した配下が、遠征先で倒れた（→ [[AdventurerAI]] の侵入者モードから）。
    /// ⚠ <b>個体はロスターから消える。</b>格も装備もその個体に紐づいていたものは一緒に失われる
    ///   ―― それが「育てた1体を出すか、守りに残すか」を重い判断にしている当のもの。
    /// </summary>
    public static void OnRaiderFell(int individualId)
    {
        if (current == null) return;
        if (individualId == current.leaderId)
        {
            // 率いる眷属が倒れたら遠征は終わり（残りは引き上げる）
            Debug.Log("💀『遠征隊の壊滅』率いる眷属が倒れた");
            Retreat("率いる眷属が倒れた");
            return;
        }
        if (current.members.Remove(individualId))
        {
            current.lost++;
            MinionRoster.Remove(individualId);
        }
    }

    /// <summary>UIの1行。</summary>
    public static string StatusLine()
    {
        if (current == null) return "";
        var n = NestSystem.At(current.nestIndex);
        string nm = n != null ? n.snap.name : "?";
        if (current.phase == Phase.Forming)
            return "<color=#e3a94a>遠征の編成中</color> " + nm + "／連れて行く " + current.members.Count + "体";
        return "<color=#e05a5a>遠征中</color> " + nm + " " + (current.floor + 1) + "/"
             + (n != null ? n.snap.FloorCount : 0) + "層／" + (current.members.Count + 1) + "体";
    }
}
