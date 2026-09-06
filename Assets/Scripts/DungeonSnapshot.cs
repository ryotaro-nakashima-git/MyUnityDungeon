using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🗿 <b>他所のダンジョン1つぶんの写し</b>（④盤の上のダンジョン）。
///
/// ⚠⚠ <b>これがマルチプレイの器そのもの。</b> 遠征に必要な入力を
/// <b>この構造体1つだけ</b>に絞ってある ―― 野良の巣も、ボットの魔王も、他プレイヤーの迷宮も、
/// <b>渡すものは同じ</b>。ここでボット専用の近道（その場で生成する・グローバルを直接読む等）を
/// 作ると、マルチを入れるときに全部やり直しになる。
///
/// ⚠ <b>地形は持たない。</b> `seed` と `floorSizes` から <see cref="DungeonGenerator"/> が
///   同じ盤を組み直せるので、タイルの配列を持ち歩く必要がない
///   （セーブも軽くなり、通信で送るのも seed と数個の数字で済む）。
///
/// 関連: [[NestSystem]] [[DungeonGenerator]] [[RivalLords]] [[rank-realm-nest-magic]]。
/// </summary>
public class DungeonSnapshot
{
    /// <summary>種別。⚠ 値はセーブに載る。末尾に足すこと。</summary>
    public enum Kind { Wild = 0, RivalLord = 1, Player = 2 }

    public string name = "";
    public Kind kind = Kind.Wild;
    /// <summary>地形を組み直すための種。⚠ これが同じなら誰の環境でも同じ盤になる。</summary>
    public int seed;
    /// <summary>各階の広さ（10〜50）。長さ＝階層数。</summary>
    public List<int> floorSizes = new List<int>();
    /// <summary>各階に置かれている配下（`MinionCatalog` の index）。階ごとにまとめて持つ。</summary>
    public List<int> guardIndex = new List<int>();
    /// <summary>その配下がどの階にいるか（`guardIndex` と同じ長さ）。</summary>
    public List<int> guardFloor = new List<int>();
    /// <summary>その配下のレベル（`guardIndex` と同じ長さ）。</summary>
    public List<int> guardLevel = new List<int>();
    /// <summary>各階の罠の種類（`TrapKind`）。階ごとに0〜数個。</summary>
    public List<int> trapKind = new List<int>();
    public List<int> trapFloor = new List<int>();
    /// <summary>最深部の主（`MinionCatalog` の index）。これを討つと制覇。</summary>
    public int lordIndex;
    public int lordLevel = 1;
    public float lordHpMult = 1f;
    /// <summary>難度の目安（UIに出す）。冒険者ランクと同じ 0〜7 の物差し。</summary>
    public int tier;
    /// <summary>他プレイヤーのものなら、その持ち主の名（マルチ用・いまは空）。</summary>
    public string ownerName = "";

    public int FloorCount => floorSizes.Count;

    /// <summary>その階に置かれている配下を数える（UIと生成で使う）。</summary>
    public int GuardCountOn(int floor)
    {
        int n = 0;
        for (int i = 0; i < guardFloor.Count; i++) if (guardFloor[i] == floor) n++;
        return n;
    }

    public int TotalGuards => guardIndex.Count;

    /// <summary>
    /// 攻める側の目安になる強さ。⚠ <b>式で勝敗を決めるためのものではない</b>
    ///（強さを式で予想しない → [[readiness-and-trade]]）。UIに「手応え」を出すためだけに使う。
    /// </summary>
    public float ThreatScore()
    {
        float s = 0f;
        for (int i = 0; i < guardIndex.Count; i++)
        {
            var d = MinionCatalog.Get(guardIndex[i]);
            s += d.tierCP * (1f + guardLevel[i] * 0.04f);
        }
        s += MinionCatalog.Get(lordIndex).tierCP * lordHpMult * 3f;
        return s;
    }

    public string KindName => kind == Kind.Wild ? "野良の巣" : kind == Kind.RivalLord ? "魔王の迷宮" : "他プレイヤーの迷宮";
    public string KindColor => kind == Kind.Wild ? "#8ec46a" : kind == Kind.RivalLord ? "#e05a5a" : "#8cb8e6";
}
