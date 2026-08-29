using UnityEngine;

/// <summary>
/// 🗣️ **流言**（S-2）。地上に噂を撒いて、**次に来る冒険者の顔ぶれを寄せる**。
///
/// <para>
/// ⚠⚠ **なぜ要るか（実装を読んで分かったこと）**：地上と迷宮は**繋がっていなかった**。
///   `WaveRoster.RollCount` の入力に地上由来のものは1つも無く、職に至っては
///   `e.job = (Job)Random.Range(0, 4)` の**完全な乱数**だった。
///   地上研究18ノードも全部が地上のことしか解禁しない。
///   ＝ 地上を耕しても、**迷宮の盤の上では何も変わらなかった**。
///   これが地上と迷宮のあいだの**最初の直通路**になる。
/// </para>
///
/// <para>
/// ⚠⚠ **強さには触らない。** 変えるのは**顔ぶれ（職の比率）だけ**で、
///   人数もレベルもランクも既存の式のまま。掛け算の軸を増やさない
///   → [[difficulty-curve-orders]]。
/// </para>
///
/// <para>
/// ⚠ 対価は **威名**（地上の資源）。地上を耕した者だけが撒ける、という形にすることで
///   「地上をやる理由」が迷宮側に生まれる。相場は交易路25／和平40なので、20 は少し安め
///   ―― 毎ターン撒けるが、他の外交を諦める必要はある、くらいの位置。
/// ⚠ 撒いたら**その場で名簿を引き直す**（大招集と同じ）。先触れがすぐ更新され、
///   それを見てから『備え』を選べる ―― **仕込んで、待ち構える**という一連の流れになる。
/// ⚠ そのターン限り（`OnTurnStart` で解除）。
/// </para>
///
/// 関連: [[WaveRoster]] [[DiplomacySystem]]（威名） [[omen-and-ward]]（先触れと備え）。
/// </summary>
public static class RumorSystem
{
    // ⚠ 状態＝セーブに載る（`readonly` にしない → [[SaveSystem]]）
    private static int job = -1;

    /// <summary>撒いた噂の職（-1＝撒いていない）。</summary>
    public static int Job { get { return job; } }
    public static bool Active { get { return job >= 0; } }

    // ── ノブ ──
    /// <summary>対価（威名）。</summary>
    public const int Cost = 20;
    /// <summary>名簿のうち、この割合が指した職になる。⚠ 1.0 にしない（全員同じ職は読み合いが消える）。</summary>
    public const float Bias = 0.6f;

    public static void Reset() { job = -1; }
    /// <summary>ターンの頭で解除。⚠ **名簿を引く前**に呼ぶこと（そのターン限りを守る唯一の場所）。</summary>
    public static void OnTurnStart() { job = -1; }

    public static string RumorName(int j)
    {
        switch (j)
        {
            case 0: return "魔物が暴れている";
            case 1: return "財宝が眠っている";
            case 2: return "呪いが広がっている";
            default: return "秘術の遺構がある";
        }
    }

    public static string JobName(int j)
    {
        switch (j)
        {
            case 0: return "戦士";
            case 1: return "盗賊";
            case 2: return "僧侶";
            default: return "魔術師";
        }
    }

    /// <summary>その噂が呼ぶものの「うまみと厄介さ」。⚠ 押す前に読めるように。</summary>
    public static string JobNote(int j)
    {
        switch (j)
        {
            case 0: return "硬いが、宝には目もくれない";
            case 1: return "宝箱を漁る ― <b>戦利品を多く抱えて帰ろうとする</b>（奪還の的）";
            case 2: return "仲間を癒す ― 長引くが、術が尽きれば脆い";
            default: return "遠くから焼く ― 脆いが、魔力が続くかぎり削られる";
        }
    }

    public static bool CanCast(out string why)
    {
        why = "";
        var turn = DungeonTurnManager.Instance;
        if (turn == null || !turn.IsPreparePhase) { why = "戦闘中には撒けない"; return false; }
        if (Active) { why = "この波にはもう噂を撒いてある"; return false; }
        if (DiplomacySystem.Influence < Cost) { why = "威名が足りない（要 " + Cost + "）"; return false; }
        return true;
    }

    /// <summary>🗣️ 噂を撒く。⚠ **その場で名簿を引き直す**ので、先触れがすぐ変わる。</summary>
    public static bool TryCast(int j, out string why)
    {
        if (!CanCast(out why)) return false;
        if (!DiplomacySystem.TrySpend(Cost)) { why = "威名が足りない"; return false; }
        job = Mathf.Clamp(j, 0, 3);
        var turn = DungeonTurnManager.Instance;
        WaveRoster.Roll(turn.CurrentTurn);   // ⚠ 引き直して初めて「寄る」
        NotifySystem.Push("<b>流言</b> ― 『" + RumorName(job) + "』と囁いた。<b>" + JobName(job) + "</b>が多く降りてくる",
            NotifySystem.Kind.Story);
        SoundSystem.Play(SoundSystem.Sfx.Story);
        Debug.Log("🗣️『流言』" + RumorName(job) + " → " + JobName(job) + " が寄る（-" + Cost + "威名）");
        return true;
    }

    /// <summary>名簿を引くときの職。⚠ `WaveRoster` から呼ぶ。撒いていなければ従来どおりの乱数。</summary>
    public static AdventurerAI.Job PickJob()
    {
        if (job >= 0 && Random.value < Bias) return (AdventurerAI.Job)job;
        return (AdventurerAI.Job)Random.Range(0, 4);
    }
}
