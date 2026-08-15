using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🔮 **次の波の名簿（pre-roll）と『先触れ』**。
///
/// <para>
/// **なぜ要るか**：毎ターンが同じに感じる最大の原因は、準備フェーズに
/// 『今回はどうするか』という入力が無かったこと（毎回おなじ最適解を置き直すだけ）だった。
/// 相手の編成が事前に分かれば、**毎ターン盤を組み替える理由**が生まれる。
/// </para>
///
/// <para>
/// ⚠⚠ 旧仕様は「戦闘開始時に人数を決め、**各冒険者が湧いた瞬間に自分で職とランクを引く**」だった。
/// これでは予告のしようがない（引く前だから誰も知らない）。
/// → **準備フェーズの頭で名簿を確定**させ、スポナーはそれを順に出すだけにする。
/// </para>
///
/// <para>
/// ⚠ 名簿は**準備の頭で固まる**。準備中に階層を足しても、その噂が届くのは次のターン。
///   （その場で敵が増えると「建てたら即罰される」ことになるので、遅れて効くほうが正しい）
/// </para>
///
/// 見せ方は『先触れ』パネル（→ <see cref="WardSystem"/> の備えと同じ画面）。
/// 見える深さは研究 <c>d_omen1〜4</c> で伸びる。関連: [[new-content-plan-cabde]]。
/// </summary>
public static class WaveRoster
{
    /// <summary>名簿の1人ぶん。**乱数はここで全部引き終えている**（AdventurerAIは引き直さない）。</summary>
    public struct Entry
    {
        public int level;
        public int rank;                       // 0..7 = G..S
        public AdventurerAI.Job job;
        public AdventurerAI.Purpose purpose;
        public bool hasSpell;
        public MagicCatalog.Spell spell;
        public float satisfyRoll;              // 満足閾値の素の乱数（個体差）
    }

    // ⚠⚠ **readonly にしてはいけない。** この作品のセーブは静的フィールドを丸ごと写す方式で、
    //   `readonly` は「カタログ＝保存しない」の合図として扱われる（→ [[save-sound-settings]]）。
    //   名簿は**状態**なので、readonly にするとロード後に引き直され、
    //   セーブ前に『先触れ』で見せた波と違う波が来る（＝予告が嘘になる／やり直しで引き直せてしまう）。
    private static List<Entry> roster = new List<Entry>();
    private static int cursor;
    private static int rolledTurn = -1;

    public static int Count { get { return roster.Count; } }
    public static List<Entry> All { get { return roster; } }
    public static int RolledTurn { get { return rolledTurn; } }

    // ============ 名簿を作る ============

    /// <summary>
    /// このターンの名簿を（まだなら）確定させる。ターン頭・先触れの表示・戦闘開始のどこから呼んでもよい。
    /// ⚠ セーブから戻ったときは `roster` が空なので、`rolledTurn` が一致していても引き直す。
    /// </summary>
    public static void EnsureRolled(int turn)
    {
        if (rolledTurn == turn && roster.Count > 0) return;
        Roll(turn);
    }

    /// <summary>周をまたがない。次に読まれたとき `EnsureRolled` が引き直す。</summary>
    public static void Reset() { roster.Clear(); cursor = 0; rolledTurn = -1; }

    /// <summary>名簿を作り直す。⚠ 通常は `EnsureRolled` を使うこと。</summary>
    public static void Roll(int turn)
    {
        roster.Clear();
        cursor = 0;
        rolledTurn = turn;

        int n = RollCount(turn);
        int fame = DungeonResourceManager.Instance != null ? DungeonResourceManager.Instance.DungeonFame : 0;
        float lvBase = AdventurerAI.LevelBase(turn, fame);
        float worldTier = AdventurerAI.WorldTier(turn, fame, LureEconomy.Threat);

        for (int i = 0; i < n; i++)
        {
            var e = new Entry();
            e.level = Mathf.Clamp(Mathf.RoundToInt(lvBase * Random.Range(0.70f, 1.15f)), 1, 100);
            e.purpose = (Random.Range(0, 2) == 0) ? AdventurerAI.Purpose.Explore : AdventurerAI.Purpose.Conquer;
            e.job = (AdventurerAI.Job)Random.Range(0, 4);
            e.rank = Mathf.Clamp(Mathf.RoundToInt(worldTier + Random.Range(-1.6f, 1.1f)), 0, 7);
            e.satisfyRoll = Random.Range(0f, 1f);
            e.hasSpell = MagicCatalog.TryPickHeroSpell(e.job, e.rank, out e.spell);
            roster.Add(e);
        }
    }

    /// <summary>
    /// 今ターン攻めてくる人数。⚠ 元は `DungeonAdventurerSpawner.StartWaveForThisTurn` にあった式を
    /// **そのまま**持ってきたもの（予告するには戦闘開始より前に決まっている必要がある）。
    /// </summary>
    private static int RollCount(int turn)
    {
        // 📈 ターンが進むほど数が増えるが、**配置枠が頭打ちになる以上ここも飽和させる**（上限20）。
        int n = Mathf.Min(20, 3 + turn)
            + (EmotionTreeManager.Instance != null ? EmotionTreeManager.Instance.BonusAdventurers : 0) // 🌟 歓喜ツリー＝集客
            + LureEconomy.ExtraWaveCount                       // 🕸️ 誘導経済：脅威度が高いほど大挙して押し寄せる
            + DungeonFloorManager.RenownBonusAdventurers;      // 🏛️ 領域の名声：広い迷宮ほど噂を呼ぶ
        float lure = DungeonTheme.LureMult * Difficulty.AdvCountMult * NarrativeSystem.LureMult;
        if (RelicManager.Instance != null) lure *= RelicManager.Instance.LureMult;
        lure *= MutationSystem.WaveCountMult;                  // 🧬 世界の変異『群れ』
        n += IncidentSystem.WaveDelta;                         // ⚡ 異変（前のターンに選んだ結果）
        return Mathf.Max(1, Mathf.RoundToInt(n * lure));
    }

    /// <summary>スポナーが1体出すたびに名簿から取り出す。名簿が尽きたら false（＝その場で引かせる）。</summary>
    public static bool TryTake(out Entry e)
    {
        if (cursor >= 0 && cursor < roster.Count) { e = roster[cursor]; cursor++; return true; }
        e = default(Entry); return false;
    }

    // ============ 🔭 先触れ（見える深さ） ============

    /// <summary>
    /// 研究で伸びる『どこまで読めるか』。0＝人数の当たりだけ／4＝1人ずつ全部見える。
    /// ⚠ ノードを足したらここに1行足すこと（書き忘れると押せるのに何も変わらない死にノードになる）。
    /// </summary>
    public static int ScoutLevel
    {
        get
        {
            int lv = 0;
            if (ResearchState.IsResearched("d_omen1")) lv = 1;
            if (lv == 1 && ResearchState.IsResearched("d_omen2")) lv = 2;
            if (lv == 2 && ResearchState.IsResearched("d_omen3")) lv = 3;
            if (lv == 3 && ResearchState.IsResearched("d_omen4")) lv = 4;
            return Mathf.Clamp(lv + IncidentSystem.ScoutDeeper, 0, 4);   // ⚡ 異変『密偵を泳がせる』

        }
    }

    public static readonly string[] ScoutLevelName =
    { "気配のみ", "耳を澄ます", "斥候の目", "魔力の読み", "看破" };

    /// <summary>Lv0でも出す「およそ何人か」。読めていない時は幅を持たせる（嘘はつかないが精度は落とす）。</summary>
    public static string CountText
    {
        get
        {
            if (roster.Count == 0) return "―";
            if (ScoutLevel >= 1) return roster.Count + " 体";
            int lo = Mathf.Max(1, Mathf.FloorToInt(roster.Count * 0.75f));
            int hi = Mathf.CeilToInt(roster.Count * 1.25f);
            return "およそ " + lo + "〜" + hi + " 体";
        }
    }

    /// <summary>職の内訳（Lv2以上）。index は <see cref="AdventurerAI.Job"/> の順。</summary>
    public static int[] JobCounts()
    {
        var c = new int[4];
        for (int i = 0; i < roster.Count; i++) c[(int)roster[i].job]++;
        return c;
    }

    public static int MaxRank
    {
        get { int m = 0; for (int i = 0; i < roster.Count; i++) if (roster[i].rank > m) m = roster[i].rank; return m; }
    }
    public static int AvgLevel
    {
        get { if (roster.Count == 0) return 0; int s = 0; for (int i = 0; i < roster.Count; i++) s += roster[i].level; return s / roster.Count; }
    }
    public static int ConquerCount
    {
        get { int c = 0; for (int i = 0; i < roster.Count; i++) if (roster[i].purpose == AdventurerAI.Purpose.Conquer) c++; return c; }
    }
    public static int CasterCount
    {
        get { int c = 0; for (int i = 0; i < roster.Count; i++) if (roster[i].hasSpell) c++; return c; }
    }

    /// <summary>敵が持ち込む属性の一覧（Lv3以上）。「聖光が多い＝不死が焼かれる」まで読める。</summary>
    public static string ElementText()
    {
        var seen = new Dictionary<string, int>();
        for (int i = 0; i < roster.Count; i++)
        {
            if (!roster[i].hasSpell) continue;
            string nm = MagicCatalog.ElementName(roster[i].spell.element);
            if (seen.ContainsKey(nm)) seen[nm]++; else seen[nm] = 1;
        }
        if (seen.Count == 0) return "術者はいない";
        string s = "";
        foreach (var kv in seen) s += (s.Length > 0 ? "／" : "") + kv.Key + "×" + kv.Value;
        return s;
    }

    /// <summary>
    /// 🎯 **こちらが持っている属性のうち、この編成に一番通るもの**（Lv3以上）。
    /// 情報を出すだけでは遊びにならないので、**打つ手**の側まで言い切る。
    /// </summary>
    public static string BestElementAdvice()
    {
        if (roster.Count == 0) return "";
        MagicElement best = MagicElement.Dark; float bestScore = -1f; bool any = false;
        for (int i = 0; i < MagicCatalog.ElementCount; i++)
        {
            var e = (MagicElement)i;
            if (!MagicCatalog.IsElementUnlocked(e)) continue;
            float s = 0f;
            for (int k = 0; k < roster.Count; k++) s += MagicCatalog.ResistMultVsHero(e, roster[k].job);
            s /= roster.Count;
            if (s > bestScore) { bestScore = s; best = e; any = true; }
        }
        if (!any) return "";
        if (bestScore <= 1.001f) return "属性で刺さる相手ではない（数と配置で受ける）";
        return MagicCatalog.ElementName(best) + " が通る（平均 ×" + bestScore.ToString("0.00") + "）";
    }

    /// <summary>
    /// 🗣️ 編成から読み取れる「一言」。ここが**備えを選ぶ根拠**になる。
    /// ⚠ 事実だけを書くこと（数えた結果しか言わない）。読み取りの外挿を混ぜると嘘になる。
    /// </summary>
    public static string Reading()
    {
        if (roster.Count == 0) return "まだ何も聞こえない。";
        if (ScoutLevel <= 0) return "人の気配が近づいている。数までは読めない。";
        var c = JobCounts();
        int n = roster.Count;
        var lines = new List<string>();
        if (ScoutLevel >= 2)
        {
            // ⚠ 「多い」の線は **2体以上かつ35%以上**。1/4を「多い」と言うと、4体の波で
            //   聖職者1体でも警告が出て、**読みが当たらない**（実測で最初にこれをやった）。
            if (Many(c, AdventurerAI.Job.Cleric, n)) lines.Add("聖職者が多い。<b>削っても戻される</b>ぞ。");
            if (Many(c, AdventurerAI.Job.Mage, n)) lines.Add("術者が多い。<b>遠間から焼かれる</b>。");
            if (c[(int)AdventurerAI.Job.Warrior] >= 2 && c[(int)AdventurerAI.Job.Warrior] * 100 >= n * 45)
                lines.Add("重装が主体だ。<b>足を止めれば罠が効く</b>。");
            if (Many(c, AdventurerAI.Job.Thief, n)) lines.Add("盗人が多い。<b>宝を持ち逃げされる</b>。");
        }
        if (ScoutLevel >= 1 && MaxRank >= 5) lines.Add("<color=#e05a5a>" + AdventurerAI.RankLetter(MaxRank) + "級</color>が混じっている。");
        if (ScoutLevel >= 2 && ConquerCount * 2 > n) lines.Add("半数以上が<b>踏破目的</b>。まっすぐ最下層へ来る。");
        if (lines.Count == 0) lines.Add("これといった偏りはない。数で押してくる。");
        string s = "";
        for (int i = 0; i < lines.Count && i < 3; i++) s += (i > 0 ? "\n" : "") + "・" + lines[i];
        return s;
    }

    /// <summary>「その職が多い」の判定。⚠ 少人数の波で誤検知しないよう**体数の下限**も要る。</summary>
    public static bool Many(int[] c, AdventurerAI.Job j, int n)
    { int v = c[(int)j]; return v >= 2 && v * 100 >= n * 35; }

    /// <summary>職の表示名。⚠ 絵文字は使わない（□になる → [[ui-conventions]]）。</summary>
    public static string JobName(AdventurerAI.Job j)
    {
        switch (j)
        {
            case AdventurerAI.Job.Warrior: return "戦士";
            case AdventurerAI.Job.Thief: return "盗賊";
            case AdventurerAI.Job.Cleric: return "聖職者";
            default: return "魔術師";
        }
    }
    public static string JobColor(AdventurerAI.Job j)
    {
        switch (j)
        {
            case AdventurerAI.Job.Warrior: return "#e0a05a";
            case AdventurerAI.Job.Thief: return "#8cb8e6";
            case AdventurerAI.Job.Cleric: return "#6ecf8e";
            default: return "#b48ce6";
        }
    }
}
