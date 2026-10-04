using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 💧 **資源の台帳**（数理設計 P0・形式仕様 §3）。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：DP の入り口は17ファイル、使い道は28ファイルに散らばっていて、
///   全体の収支を誰も見ていなかった（実測：T36 で 137,688 が使われずに余った）。
/// </para>
/// <para>
/// 形：`DungeonResourceManager` の出入り口が、**呼び出し元（ファイル.関数）を自動で**ここへ知らせる
///   （`[CallerFilePath]`／`[CallerMemberName]`）。呼び出し側の74か所は書き換えていない＝**漏れが出ない**。
/// ⚠ ゲームの数字には一切触れない。数えるだけ。
/// </para>
/// </summary>
public static class ResourceLedger
{
    // (資源, 区分キー) → そのターンの合計。正＝入り、負＝出。
    private static readonly Dictionary<string, int> acc = new Dictionary<string, int>();

    public static void Note(string resource, int amount, string file, string member)
    {
        if (amount == 0) return;
        string key = resource + "|" + (amount > 0 ? "source" : "sink") + "|" + Path.GetFileNameWithoutExtension(file) + "." + member;
        int v; acc.TryGetValue(key, out v);
        acc[key] = v + amount;
    }

    /// <summary>溜めた分を取り出して空にする（ターンの区切りで呼ぶ）。</summary>
    public static Dictionary<string, int> Drain()
    {
        var d = new Dictionary<string, int>(acc);
        acc.Clear();
        return d;
    }

    public static void Clear() { acc.Clear(); }
}

/// <summary>
/// 📈 **流れの記録**（数理設計 P0・形式仕様 §10）。CSV を `docs/measure/<実行名>/` に書く。
///
/// ⚠ 自動運転（計測）のときだけ動く（`Begin` を呼んだときだけ）。人が遊ぶときは何も書かない。
/// ⚠ 記録するのは**ゲーム内時間**（`Time.deltaTime` の積算）。計測専用モードで速くしても変わらない量。
/// </summary>
public static class Telemetry
{
    public static bool Active { get; private set; }
    private static string dir;
    private static int run;

    // ── 波の中の観測 ──
    private static float clock;        // その波のゲーム内時間
    private static float busy;         // 盤上に冒険者が1人でもいた時間
    private static float engaged;      // 1人でも交戦中だった時間（P1：歩いている時間を除く）
    private static int engMax;
    private static int frames;         // その波のフレーム数（1フレームのゲーム内時間＝clock/frames）
    private static float contactSum; private static int contactN;   // 湧いてから初めて交戦するまで
    private static int qMax;
    private static float firstArrival = -1f, lastArrival = -1f;
    private static int arrivals;
    private static float lordHpStart, sigmaStart;
    private static int burnedStart;

    // ── 資源の検算 ──
    private static readonly Dictionary<string, int> lastStock = new Dictionary<string, int>();
    public static int ConservationErrors { get; private set; }

    public static void Begin(string outDir)
    {
        dir = outDir;
        Directory.CreateDirectory(dir);
        WriteHeader("runs.csv", "run,version,params_hash,seed,end_turn,outcome,censored,winner,path,fingerprint");
        WriteHeader("waves.csv", "run,turn,N,b,g,A,D,E,busy_sec,lambda,mu,rho,q_max,L,K,lord_hp_start,lord_hp_end,sigma_start,sigma_end,grave,burned,engaged_sec,mu_eng,rho_eng,eng_max,contact_sec,dt_mean,fatigue_end,adv_power,adv_power_max,def_power,def_count");
        WriteHeader("economy.csv", "run,turn,resource,kind,key,amount");
        WriteHeader("advs.csv", "run,turn,deepest,floors,lord_floor,outcome,why,level,rank,conquer,job,need_next,hit_lord");
        WriteHeader("turns.csv", "run,turn,era,era_progress,dp,materials,rp,fame,researched,floors,placed,cap,minions,owned_tiles,met_dominion,met_dread,met_economy,met_innovation,rite,tiles,evo_depth,gear_mean,path_len");
        Active = true;
        Debug.Log("📈『流れの記録』" + dir);
    }

    public static void End() { Active = false; }

    /// <summary>
    /// 🧭 冒険者1人の終わり（数理設計 P2・系1③・段0）。どこまで降りて、どう終わったか。
    /// ⚠ 階ごとの「入った・抜けた・倒された・逃げた」はここから数える（→ tools/analyze/reach.py）。
    /// </summary>
    public static void NoteAdventurerEnd(int deepest, int floors, int lordFloor, string outcome, string why,
        int level, int rank, bool conquer, string job, int needNext, bool hitLord)
    {
        if (!Active) return;
        int t = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 0;
        Append("advs.csv", run + "," + t + "," + deepest + "," + floors + "," + lordFloor + "," + outcome + "," + why
            + "," + level + "," + rank + "," + (conquer ? 1 : 0) + "," + job + "," + needNext + "," + (hitLord ? 1 : 0));
    }

    public static void BeginRun(int runIndex)
    {
        run = runIndex;
        lastStock.Clear();
        ResourceLedger.Clear();
        ConservationErrors = 0;
    }

    // ============ 波 ============
    public static void BeginWave()
    {
        advPower = 0f; advPowerMax = 0f;
        clock = 0f; busy = 0f; qMax = 0; engaged = 0f; engMax = 0; frames = 0; contactSum = 0f; contactN = 0; firstArrival = -1f; lastArrival = -1f; arrivals = 0;
        var dl = DemonLord.Instance;
        lordHpStart = dl != null ? dl.HPRatio : 0f;
        sigmaStart = LordBerserk.Shell;
        burnedStart = LordBerserk.Entries;
    }

    /// <summary>戦闘中に毎フレーム（`DungeonTurnManager.Update`）。</summary>
    public static void TickBattle(float dt)
    {
        if (!Active) return;
        clock += dt; frames++;
        int q = AdventurerAI.LiveCount;
        if (q > 0) busy += dt;
        if (q > qMax) qMax = q;
        // ⚔️ 交戦中の数（P1）。⚠ 歩いている冒険者は数えない＝窓口に着いている人だけ
        int e = 0;
        var live = AdventurerAI.Live;
        for (int i = 0; i < live.Count; i++)
        {
            var a = live[i];
            if (a == null || !a.Engaged) continue;
            e++;
            if (!a.ContactNoted) { a.ContactNoted = true; contactSum += Time.time - a.SpawnedAt; contactN++; }
        }
        if (e > 0) engaged += dt;
        if (e > engMax) engMax = e;
    }

    /// <summary>冒険者が1人湧いた（スポナーから）。</summary>
    // 💪 強さの物差し（系3・強さの曲線）：冒険者と守りを同じ「HP×攻撃」（CombatPower）で数える
    private static float advPower, advPowerMax;
    /// <summary>冒険者が1人、初期化を終えた（その強さを足す）。</summary>
    public static void NoteAdventurerPower(float p)
    {
        if (!Active) return;
        advPower += p;
        if (p > advPowerMax) advPowerMax = p;
    }

    public static void NoteArrival()
    {
        if (!Active) return;
        if (firstArrival < 0f) firstArrival = clock;
        lastArrival = clock;
        arrivals++;
    }

    public static void EndWave(int turn, int N, int b, float g, int L, int K)
    {
        if (!Active) return;
        int A = arrivals;
        int D = WaveReport.Killed + WaveReport.Captured;
        int E = WaveReport.Escaped;
        float window = (firstArrival >= 0f ? lastArrival - firstArrival : 0f) + Balance.F("wave.batch.intra_sec", 0.35f);
        float lambda = window > 0f ? A / window : 0f;
        float mu = busy > 0f ? D / busy : 0f;
        float rho = mu > 0f ? lambda / mu : -1f;
        float muE = engaged > 0f ? D / engaged : 0f;
        float rhoE = muE > 0f ? lambda / muE : -1f;
        float contact = contactN > 0 ? contactSum / contactN : -1f;
        var dl = DemonLord.Instance;
        float hpEnd = dl != null ? dl.HPRatio : 0f;
        // 💪 守りの強さ＝その波に立っていた配下（倒れた者も含む）の CombatPower の合計
        float defPower = 0f; int defCount = 0;
        foreach (var z in Object.FindObjectsByType<ZombieAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { if (z == null) continue; defPower += z.CombatPower; defCount++; }
        Append("waves.csv", Row(run, turn, N, b, F(g), A, D, E, F(busy), F(lambda), F(mu), F(rho), qMax, L, K,
            F(lordHpStart), F(hpEnd), F(sigmaStart), F(LordBerserk.Shell),
            LordBerserk.RecoveryBlocked ? 1 : 0, LordBerserk.Entries - burnedStart,
            F(engaged), F(muE), F(rhoE), engMax, F(contact), F(frames > 0 ? clock / frames : 0f), F(LordBerserk.Fatigue),
            F(advPower), F(advPowerMax), F(defPower), defCount));
    }

    // ============ ターン ============
    /// <summary>
    /// 1ターンぶんを書く。⚠ 資源の出入りは「前にこれを呼んでから今まで」の合計。
    /// 保存則（仕様 §3.1）を検算し、ずれたら記録漏れとして数える。
    /// </summary>
    public static void EndTurn(int turn, string turnRow)
    {
        if (!Active) return;
        var res = DungeonResourceManager.Instance;
        var flows = ResourceLedger.Drain();
        var net = new Dictionary<string, int>();
        var sb = new StringBuilder();
        foreach (var kv in flows)
        {
            var p = kv.Key.Split('|');
            sb.Append(Row(run, turn, p[0], p[1], p[2], kv.Value)).Append('\n');
            int v; net.TryGetValue(p[0], out v); net[p[0]] = v + kv.Value;
        }
        if (res != null)
        {
            Stock(sb, turn, "DP", res.DungeonPoints, net);
            Stock(sb, turn, "Material", res.CraftMaterials, net);
            Stock(sb, turn, "Fame", res.DungeonFame, net);
        }
        if (sb.Length > 0) Append("economy.csv", sb.ToString().TrimEnd('\n'));
        Append("turns.csv", run + "," + turn + "," + turnRow);
    }

    private static void Stock(StringBuilder sb, int turn, string r, int now, Dictionary<string, int> net)
    {
        sb.Append(Row(run, turn, r, "stock", "end", now)).Append('\n');
        int prev, n;
        if (lastStock.TryGetValue(r, out prev))
        {
            net.TryGetValue(r, out n);
            if (prev + n != now)
            {
                ConservationErrors++;
                Debug.LogWarning("💧『保存則のずれ』" + r + " T" + turn + "：前 " + prev + " + 出入り " + n + " ≠ いま " + now + "（記録を通らない出入りがある）");
            }
        }
        lastStock[r] = now;
    }

    public static void EndRun(int endTurn, string outcome, bool censored, string winner, string path, string fingerprint)
    {
        if (!Active) return;
        Append("runs.csv", Row(run, Application.version, Balance.Hash, GameSetup.Seed, endTurn, outcome, censored ? 1 : 0,
            winner, path, "\"" + fingerprint.Replace("\"", "'") + "\""));
    }

    // ============ 書き出し ============
    private static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    private static string Row(params object[] xs)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < xs.Length; i++) { if (i > 0) sb.Append(','); sb.Append(xs[i]); }
        return sb.ToString();
    }
    private static void WriteHeader(string file, string header)
    {
        string p = Path.Combine(dir, file);
        if (!File.Exists(p)) File.WriteAllText(p, header + "\n", new UTF8Encoding(false));
    }
    private static void Append(string file, string line)
    {
        try { File.AppendAllText(Path.Combine(dir, file), line + "\n", new UTF8Encoding(false)); }
        catch (System.Exception e) { Debug.LogWarning("📈 記録を書けない：" + e.Message); }
    }
}

/// <summary>
/// 🗺️ 迷宮の通り道の量（形式仕様 §2.3）。⚠ 自動運転の `PathLenAll/OnPathOccupied` と同じ数え方
///   （入口→最深部の最短経路。入口と最深部のマスは数えない）。
/// </summary>
public static class PathMetrics
{
    /// <summary>通り道の長さ L（全階の合計）。</summary>
    public static int Length()
    {
        var flr = DungeonFloorManager.Instance;
        if (flr == null) return 0;
        int n = 0;
        for (int fi = 0; fi < flr.BuiltFloorCount; fi++)
        {
            var g = DungeonGridSystem.Of(fi);
            if (g == null) continue;
            var path = AutoDeploy.PathToGoal(g);
            if (path != null) n += Mathf.Max(0, path.Count - 2);
        }
        return n;
    }

    /// <summary>通り道の上に置かれた物の数 K。</summary>
    public static int Occupied()
    {
        var fm = DungeonFeatureManager.Instance;
        var flr = DungeonFloorManager.Instance;
        if (fm == null || flr == null) return 0;
        int n = 0;
        for (int fi = 0; fi < flr.BuiltFloorCount; fi++)
        {
            var g = DungeonGridSystem.Of(fi);
            if (g == null) continue;
            var path = AutoDeploy.PathToGoal(g);
            if (path == null) continue;
            for (int k = 1; k < path.Count - 1; k++)
                if (fm.HasFeatureAt(fi, path[k])) n++;
        }
        return n;
    }
}
