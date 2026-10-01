using System.Collections.Generic;
using UnityEngine;

/// <summary>固定の刻みで動く部品（→ [[SimRunner]]）。</summary>
public interface ISimTick { void SimTick(); }

/// <summary>
/// ⏱️ <b>途中で生まれる部品（冒険者・配下・魔法の場）を、1か所でまとめて刻む</b>（→ [[SimClock]]）。
///
/// <para>
/// ⚠⚠ <b>なぜ要るか</b>（2026-10-01・速さの等価性の実測 `eqf_x1/x4/x16`）：Unity は生まれたばかりの部品に
///   <b>次の画面の描き替えまで</b> <c>Start</c> も <c>FixedUpdate</c> も呼ばない。倍速では1画面の間に何回も刻むので、
///   途中で湧いた冒険者・巣から湧いた配下・起こした配下が<b>その間ずっと止まったまま</b>になる。
///   16倍では1画面に何十回も刻むため、守りが出遅れて「倒した数 3.2→1.9・魔王HP 0.65→0.55」とずれた。
/// </para>
/// <para>
/// ここでは、部品が<b>生まれた瞬間（Awake）に名簿へ載せ</b>、次の刻みから必ず動かす。初期化（<c>Start</c> の中身）は
/// 最初の刻みの頭で済ませる（`EnsureInit`）。⇒ 速さに関係なく、生まれてから動き出すまでの刻み数が同じになる。
/// </para>
/// ⚠ 管理役（ターン・階層・湧き・配置・魔王）は場面の最初からいるので、各自の <c>FixedUpdate</c> のまま。
///   ここは管理役のあとに回る（`DefaultExecutionOrder`）＝同じ刻みの中で湧いた者は、その刻みから動く。
/// </summary>
[DefaultExecutionOrder(100)]
public class SimRunner : MonoBehaviour
{
    private static readonly List<ISimTick> list = new List<ISimTick>();
    private static readonly List<ISimTick> snap = new List<ISimTick>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        list.Clear();
        var go = new GameObject("_SimRunner");
        go.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(go);
        go.AddComponent<SimRunner>();
    }

    public static void Register(ISimTick t) { if (t != null && !list.Contains(t)) list.Add(t); }
    public static void Unregister(ISimTick t) { list.Remove(t); }

    private void FixedUpdate()
    {
        // ⚠ 刻みの途中で生まれる・消える者がいるので、写しを回す（生まれた者は次の刻みから）
        snap.Clear();
        snap.AddRange(list);
        for (int i = 0; i < snap.Count; i++)
        {
            var b = snap[i] as MonoBehaviour;
            if (b == null || !b.isActiveAndEnabled) continue;
            snap[i].SimTick();
        }
    }
}
