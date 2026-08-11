using UnityEngine;

/// <summary>
/// 👑 魔王の**構え**（鎮座／親征）と**捕食**（L-1・L-2）。
///
/// **なぜ要るか**：魔王はステを振って進化するだけで、**戦闘中に立っているだけ**だった。
/// 育てる対象なのに、戦場での判断が一つも無い。ここで「どこに立つか」と「何を喰うか」を作る。
///
/// **二つの構え**（ユーザー決定：どちらかに置き換えるのではなく**選べる**）
/// - **鎮座**：最下層から動かない。安全。その代わり**配下を喰らえる**（暇がある）。
///   ウェーブを凌ぐと BP が余分に入る。
/// - **親征**：**立つ階層を選べる**。魔王が立った階で侵攻は止まる（＝彼が壁になる）。
///   前に出るほど多くの魂を喰らえるが、浅い階で立てば**深度報酬を捨てる**ことになり、
///   討たれればその場でゲームオーバー。＝誘導経済とまったく同じ構造。
///
/// ⚠⚠ **新しい倍率を作らない**（→ [[difficulty-curve-orders]]）。
///   捕食の見返りは `DemonLord` の**基礎HP/基礎攻撃への加算**だけ。倍率に乗せると
///   CDO2 の「捕食ビルドで魔王が単騎で全滅させる」が再現される（あれは向こうで一番壊れている）。
///
/// ⚠ 捕食できるのは**鎮座のときだけ・1ターンに2体まで**。この2つが唯一の歯止め。
/// ⚠ ユニーク魔物は喰えない（引き当てた1体が資産なので、誤操作で消えると取り返しがつかない）。
///
/// 純static。**セーブに載せる**（`SaveSystem.StaticTypes` に登録済み）。
/// 関連: [[DemonLord]] [[LordAuthority]] [[DungeonFloorManager]] [[upgrade-plan-nislv]]。
/// </summary>
public static class LordStance
{
    public enum Stance { Enthroned = 0, Expedition = 1 }   // 鎮座 / 親征

    // ⚠ セーブはリフレクションで写す。enum ではなく int で持つ（型の取り違えを避ける）。
    private static int stance = (int)Stance.Enthroned;
    private static int stationFloor = 0;      // 親征で立つ階（0-based）
    private static int devourExp;             // 捕食値
    private static int devourRank;            // 喰らいの段
    private static int devouredThisTurn;      // このターンに喰った数
    private static int stockedTurn = -1;

    public const int DevourPerTurn = 2;       // ⚠ 歯止め。ここを緩めると捕食ビルドが暴れる

    public static void Reset()
    {
        stance = (int)Stance.Enthroned; stationFloor = 0;
        devourExp = 0; devourRank = 0; devouredThisTurn = 0; stockedTurn = -1;
    }

    // ============ 構え ============
    public static Stance Current => (Stance)Mathf.Clamp(stance, 0, 1);
    public static bool IsExpedition => Current == Stance.Expedition;
    public static string CurrentName => IsExpedition ? "親征" : "鎮座";
    public static int StationFloor => Mathf.Max(0, stationFloor);

    public static string StanceName(Stance s) => s == Stance.Expedition ? "親征" : "鎮座";
    public static string StanceDesc(Stance s) => s == Stance.Expedition
        ? "立つ階層を選ぶ。**魔王が立った階で侵攻は止まる**。前に出るほど多くの魂を喰らえるが、深度報酬は捨てることになる。討たれれば負け。"
        : "最下層から動かない。**配下を喰らえる**（1ターンに" + DevourPerTurn + "体まで）。ウェーブを凌ぐと BP が余分に入る。";

    /// <summary>構えを変える（準備フェーズのみ＝戦いが始まってから後ろに下がれてはいけない）。</summary>
    public static bool SetStance(Stance s)
    {
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 構えを変えられるのは準備フェーズだけです。"); return false; }
        if (Current == s) return false;
        stance = (int)s;
        ClampStation();
        Debug.Log($"👑『構え』魔王は {CurrentName} を選んだ" + (IsExpedition ? $"（B{StationFloor + 1}F に立つ）" : ""));
        NotifySystem.Push("魔王の構えを <b>" + CurrentName + "</b> にした", NotifySystem.Kind.Info);
        return true;
    }

    /// <summary>親征で立つ階を選ぶ（準備フェーズのみ）。</summary>
    public static bool SetStationFloor(int i)
    {
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { Debug.LogWarning("⚠️ 布陣を変えられるのは準備フェーズだけです。"); return false; }
        stationFloor = Mathf.Max(0, i);
        ClampStation();
        Debug.Log($"👑『布陣』魔王は B{StationFloor + 1}F に立つ");
        return true;
    }

    private static void ClampStation()
    {
        int n = DungeonFloorManager.Instance != null ? DungeonFloorManager.Instance.BuiltFloorCount : 1;
        stationFloor = Mathf.Clamp(stationFloor, 0, Mathf.Max(0, n - 1));
    }

    /// <summary>
    /// 魔王が実在する階層（0-based）。鎮座＝最下層／親征＝選んだ階。
    /// ⚠ ここが**唯一の判断元**。`fd.isDeepest` を直接見ている所を残すと、構えを変えても盤が付いてこない。
    /// </summary>
    public static int LordFloorIndex(int floorCount)
    {
        if (floorCount <= 0) return 0;
        if (!IsExpedition) return floorCount - 1;
        return Mathf.Clamp(stationFloor, 0, floorCount - 1);
    }

    // ============ 捕食 ============
    public static int DevourExp => devourExp;
    public static int DevourRank => devourRank;
    public static int DevouredThisTurn => devouredThisTurn;
    public static int DevourLeftThisTurn => Mathf.Max(0, DevourPerTurn - devouredThisTurn);

    /// <summary>段位の見返り＝**基礎値への加算**（倍率ではない）。</summary>
    public static float BonusHP => devourRank * 70f;
    public static float BonusAtk => devourRank * 2.5f;

    /// <summary>次の段に必要な捕食値。段が上がるほど高い＝勝手に伸び続けない。</summary>
    public static int NextRankCost => 150 + devourRank * 110;

    public static void OnTurnStart(int turn)
    {
        if (stockedTurn == turn) return;
        stockedTurn = turn;
        devouredThisTurn = 0;
        ClampStation();
    }

    /// <summary>
    /// 🩸 魔王が在陣する階で冒険者が倒れた＝魂を喰らう（構えを問わない）。
    /// ⚠ 深度倍率は掛けない。**強い冒険者ほど深くまで来る**ので、Lv で見れば自然に深いほど旨くなる。
    /// </summary>
    public static void OnSoulReaped(int adventurerLevel)
    {
        var dl = DemonLord.Instance;
        if (dl == null || !dl.IsAlive || !dl.IsPresent) return;
        devourExp += 3 + Mathf.Max(0, adventurerLevel) / 2;
    }

    public static bool CanDevour(int individualId, out string why)
    {
        why = "";
        var turn = DungeonTurnManager.Instance;
        if (turn != null && !turn.IsPreparePhase) { why = "戦闘中は喰えない"; return false; }
        if (IsExpedition) { why = "親征中は喰えない（鎮座のみ）"; return false; }
        if (devouredThisTurn >= DevourPerTurn) { why = "このターンはもう喰えない"; return false; }
        var v = MinionRoster.Get(individualId);
        if (v == null) { why = "その個体が居ない"; return false; }
        if (UniqueCatalog.IsUnique(v.catalogIndex)) { why = "ユニーク魔物は喰えない"; return false; }
        var fm = DungeonFeatureManager.Instance;
        if (fm != null && (fm.IsIndividualPlaced(individualId) || fm.IsIndividualInAnySquad(individualId)))
        { why = "盤・隊に出ている"; return false; }
        if (KinRoster.IsAwayFromDungeon(individualId)) { why = "地上に出ている"; return false; }
        return true;
    }

    /// <summary>喰ったときに入る捕食値（レベルと格で決まる）。</summary>
    public static int DevourValue(int individualId)
    {
        var v = MinionRoster.Get(individualId);
        if (v == null) return 0;
        var d = MinionCatalog.Get(v.catalogIndex);
        return 20 + v.level * 4 + Mathf.RoundToInt(d.tierCP * 6f);
    }

    /// <summary>🍽️ 配下を喰らう。⚠ 装備ごと消える（戻せない）。</summary>
    public static bool TryDevour(int individualId)
    {
        string why;
        if (!CanDevour(individualId, out why)) { Debug.LogWarning("⚠️ 捕食できません：" + why); return false; }
        var v = MinionRoster.Get(individualId);
        int gain = DevourValue(individualId);
        string nm = MinionCatalog.Get(v.catalogIndex).jpName;
        int lv = v.level;
        MinionRoster.Remove(individualId);
        devourExp += gain;
        devouredThisTurn++;
        Debug.Log($"🍽️『捕食』{nm} 個体#{individualId}(Lv{lv}) を喰らった（捕食値 +{gain} ／ 計 {devourExp}）");
        NotifySystem.Push($"魔王が <b>{nm}</b>(Lv{lv}) を喰らった ― 捕食値 +{gain}", NotifySystem.Kind.Gain);
        return true;
    }

    public static bool CanRankUp => devourExp >= NextRankCost;

    /// <summary>🔺 喰らいの段を1つ上げる。基礎HP/基礎攻撃が恒久的に増える。</summary>
    public static bool TryRankUp()
    {
        int cost = NextRankCost;
        if (devourExp < cost) { Debug.LogWarning($"⚠️ 捕食値が足りません（要 {cost}／所持 {devourExp}）"); return false; }
        devourExp -= cost;
        devourRank++;
        var dl = DemonLord.Instance;
        if (dl != null) dl.RefreshAfterLoad();   // 基礎値が変わったので戦闘値を作り直す
        Debug.Log($"🔺『喰らいの段』第{devourRank}段（基礎HP +{BonusHP:0} ／ 基礎攻撃 +{BonusAtk:0.0}）");
        NotifySystem.Push($"魔王が <b>喰らいの段 第{devourRank}段</b> に至った", NotifySystem.Kind.Story);
        return true;
    }
}
