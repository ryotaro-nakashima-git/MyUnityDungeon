using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 🤖 **通しプレイの自動運転（一時的な検証用ハーネス。リポジトリに残さない）**。
///
/// <para>
/// ⚠⚠ **進言に従わせる。** 前の2周は「進言を無視した結果」を『ゲームの壁』と読んで
///   診断を2回外した。ここでは毎ターン `GuideSystem` の進言を重み順に実行し、
///   **実行できなかった進言も全部記録する**（＝ゲーム側の穴か、ボット側の穴かを後で分けられる）。
/// ⚠ ループはすべて回数上限つき（上限なしの while で Unity を固めたことがある）。
/// </para>
/// </summary>
public class _AutoPlayHarness : MonoBehaviour
{
    public int maxTurns = 40;
    public string logPath = "docs/playlog_run7.md";

    private int lastLoggedTurn = -1;
    private int prepTurnDone = -1;
    private float battleActTimer;
    private bool finished;
    private readonly List<string> doneTitles = new List<string>();
    private readonly List<string> skipTitles = new List<string>();
    private readonly HashSet<string> triedThisTurn = new HashSet<string>();
    private int fallbackPlaced;   // 進言が全部使えなかったターンに、代わりに置いた数
    private int watchTurn = -1; private float watchClock;   // 見張り（同じターンで固まったら止める）

    private void Awake()
    {
        Application.runInBackground = true;
        Append("\n\n## 通しプレイ 7周目（拡張が配置を引き継ぐようになった）\n\n"
             + "| T | 来襲 | 撃破 | 逃 | DP | 素材 | 持逃 | 装備水準 | 魔王HP | 枠 | 巣 | 環境 | 決算の一言 |\n"
             + "|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
    }

    private void Update()
    {
        if (finished) return;
        var turn = DungeonTurnManager.Instance;
        if (turn == null || !GameSetup.Started) return;

        if (VictorySystem.Decided) { Finish("勝敗が決した"); return; }
        var dl = DemonLord.Instance;
        if (dl != null && !dl.IsAlive) { Finish("魔王が討たれた"); return; }
        if (turn.CurrentTurn > maxTurns) { Finish("上限 T" + maxTurns + " に到達"); return; }

        // ⚠ 見張り。同じターンで固まったら黙って回り続けず、理由を書いて止まる
        //   （異変の未回答で 8 分溶かした）。
        if (turn.CurrentTurn != watchTurn) { watchTurn = turn.CurrentTurn; watchClock = 0f; }
        else
        {
            watchClock += Time.unscaledDeltaTime;
            if (watchClock > 180f)
            { Finish("T" + turn.CurrentTurn + " で停止（" + turn.CurrentPhase + "・異変=" + IncidentSystem.HasPending + "）"); return; }
        }

        var ui = GameUIManager.Instance;
        if (ui != null && ui.ReportOpen) { LogWave(turn.CurrentTurn); ui.CloseReport(); return; }

        switch (turn.CurrentPhase)
        {
            case DungeonTurnManager.Phase.Prepare: DoPrepare(turn); break;
            case DungeonTurnManager.Phase.Battle: DoBattle(); break;
            case DungeonTurnManager.Phase.Surface: turn.EndSurfacePhase(); break;
        }
    }

    // ============ 前半：進言に従う ============
    private void DoPrepare(DungeonTurnManager turn)
    {
        if (prepTurnDone == turn.CurrentTurn) { Launch(turn); return; }
        prepTurnDone = turn.CurrentTurn;
        doneTitles.Clear(); skipTitles.Clear(); triedThisTurn.Clear(); fallbackPlaced = 0;

        // ⚠ 上限つき。進言を実行すると条件が変わるので、作り直して繰り返す（最大8巡）。
        for (int pass = 0; pass < 8; pass++)
        {
            var list = FreshAdvices(turn.CurrentTurn);
            if (list == null || list.Count == 0) break;
            list.Sort((a, b) => b.weight.CompareTo(a.weight));
            bool any = false;
            for (int i = 0; i < list.Count && i < 12; i++)
            {
                string t = list[i].title;
                if (triedThisTurn.Contains(t)) continue;
                bool ok = Execute(t);
                if (ok) { doneTitles.Add(t); any = true; break; }   // 1つ実行したら作り直す
                triedThisTurn.Add(t);
                if (!skipTitles.Contains(t)) skipTitles.Add(t);
            }
            if (!any) break;
        }

        // ⚠⚠ 進言は**上位3件しか出ない**（`Build` が3件で打ち切る）。
        //   その3件が全部「ボットに出来ない手」だと、DPが余っているのに1ターン何もしないことになる
        //   （実測：T3 に DP 519 を持ったまま何もしなかった）。人間ならそんなことはしない。
        //   → 手が無い turn だけ**枠を埋める**。⚠ 記録は分けて残す（進言の成果と混ぜない）。
        for (int i = 0; i < 6; i++)
        {
            if (doneTitles.Count > 0) break;
            if (!PlaceOne(false, false)) break;
            fallbackPlaced++;
        }
        Launch(turn);
    }

    private List<GuideSystem.Advice> FreshAdvices(int t)
    {
        // ⚠ `Build` は private かつ「前ターンからの増減」を覚える静的フィールドを書き換える。
        //   検証のために何度も呼ぶので、その4つは呼ぶ前後で必ず戻す（画面の数字を壊さない）。
        var ty = typeof(GuideSystem);
        var bf = BindingFlags.NonPublic | BindingFlags.Static;
        var m = ty.GetMethod("Build", bf);
        if (m == null) return GuideSystem.Latest != null ? GuideSystem.Latest.advices : null;
        string[] keep = { "prevDp", "prevMat", "prevFame", "prevRp" };
        var fields = new FieldInfo[keep.Length];
        var vals = new object[keep.Length];
        for (int i = 0; i < keep.Length; i++)
        { fields[i] = ty.GetField(keep[i], bf); vals[i] = fields[i] != null ? fields[i].GetValue(null) : null; }
        var b = m.Invoke(null, new object[] { t }) as GuideSystem.Brief;
        for (int i = 0; i < keep.Length; i++) if (fields[i] != null) fields[i].SetValue(null, vals[i]);
        return b != null ? b.advices : null;
    }

    /// <summary>進言1件を実行する。⚠ 出来なかったら false（＝記録に残る）。</summary>
    private bool Execute(string title)
    {
        var fmgr = DungeonFeatureManager.Instance;
        var flr = DungeonFloorManager.Instance;
        var res = DungeonResourceManager.Instance;
        var dl = DemonLord.Instance;
        if (fmgr == null || flr == null) return false;

        if (title.Contains("罠を1つ置く") || title.Contains("配置枠を埋める") || title.Contains("守りを厚くする"))
            return PlaceOne(false, false);
        if (title.Contains("巣を置く")) return PlaceOne(true, false);
        if (title.Contains("環境")) return PlaceOne(false, true);

        if (title.Contains("錬成")) return dl != null && dl.TrySpendBPOnStat(4);
        if (title.Contains("BPを振る"))
        {
            if (dl == null) return false;
            // 🔨 錬成を優先（鍛造の上限を開ける唯一の第2の口 → [[EquipmentCatalog]]）、次に肉体
            return dl.TrySpendBPOnStat(4) || dl.TrySpendBPOnStat(0) || dl.TrySpendBPOnStat(1);
        }

        if (title.Contains("研究を進める"))
        {
            int a = title.IndexOf('（'), b = title.LastIndexOf('）');
            if (a < 0 || b <= a) return false;
            return ResearchState.TryResearch(title.Substring(a + 1, b - a - 1));
        }
        // ⚠ 名指しの研究は『』の中が**和名**（idではない）。全分野から名前で引く。
        if (title.Contains("を研究する"))
        {
            int a = title.IndexOf('『'), b = title.IndexOf('』');
            if (a < 0 || b <= a) return false;
            string jp = title.Substring(a + 1, b - a - 1);
            for (int f = 0; f < 7; f++)
            {
                var nodes = ResearchCatalog.ByField((ResearchField)f);
                for (int i = 0; i < nodes.Count; i++)
                    if (nodes[i].jpName == jp && ResearchState.TryResearch(nodes[i].id)) return true;
            }
            return false;
        }

        if (title.Contains("遺物を装備"))
        {
            var rel = RelicManager.Instance;
            if (rel == null) return false;
            for (int i = 0; i < 64; i++)
                if (rel.IsUnlocked(i) && !rel.IsEquipped(i)) { rel.Toggle(i); return rel.IsEquipped(i); }
            return false;
        }

        if (title.Contains("感情ツリー"))
        {
            var emo = EmotionTreeManager.Instance;
            if (emo == null) return false;
            for (int r = 0; r < 4; r++)
                for (int tier = 1; tier <= 4; tier++)
                    if (emo.TryUnlock((EmotionTreeManager.Route)r, tier)) return true;
            return false;
        }

        if (title.Contains("備えを1つ張る"))
        {
            string w;
            for (int i = 0; i < WardSystem.Count; i++) if (WardSystem.TrySelect(i, out w)) return true;
            return false;
        }
        if (title.Contains("泳がせ")) { string w; return LureStance.Toggle(out w); }

        if (title.Contains("召喚して数を増やす") || title.Contains("配下そのものに注ぐ")) return SummonBest();
        if (title.Contains("階層をもう1つ増やす")) return flr.TryAddFloor();
        if (title.Contains("を広げる"))
        {
            for (int i = 0; i < flr.BuiltFloorCount; i++)
                if (flr.CanExpandFloor(i) && flr.TryExpandFloor(i)) return true;
            return false;
        }
        if (title.Contains("鍛える")) return ForgeOne();
        if (title.Contains("進化させて")) return EvolveOne();
        if (title.Contains("大招集") && !title.Contains("厚くしてから"))
        { string why; return FeverSystem.TryCall(out why); }

        return false;   // 未対応（＝地上の手や、ボットが触らない手）
    }

    // 🧱 経路に沿って1つだけ置く（罠 / 巣 / 環境）。⚠ 置けたら true。
    private bool PlaceOne(bool nest, bool habitat)
    {
        var fmgr = DungeonFeatureManager.Instance;
        var flr = DungeonFloorManager.Instance;
        for (int fi = 0; fi < flr.BuiltFloorCount; fi++)
        {
            flr.SwitchTo(fi);
            var g = DungeonGridSystem.Of(fi);
            if (g == null) continue;
            var path = AutoDeploy.PathToGoal(g);
            if (path == null || path.Count < 4) continue;

            // 🛡️ まず隊（＝育つ頭数）を出し切る。次に頼まれた物。
            if (!nest && !habitat && fmgr.CurrentSquad.Count > 0)
                for (int k = 1; k < path.Count - 1; k++) if (fmgr.TryPlaceSquadMember(path[k])) return true;

            if (habitat)
            {
                // 🌿 巣の2マス以内にしか効かない（→ [[HabitatCatalog]]）
                var nests = CellsOf(fmgr, fi, DungeonFeatureManager.FeatureType.Spawner);
                if (nests.Count == 0) return false;
                fmgr.SetSelectedHabitatKind(Random.Range(0, 3));
                for (int n = 0; n < nests.Count; n++)
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dy = -2; dy <= 2; dy++)
                        {
                            if (Mathf.Abs(dx) + Mathf.Abs(dy) > 2 || (dx == 0 && dy == 0)) continue;
                            var c = new Vector2Int(nests[n].x + dx, nests[n].y + dy);
                            if (fmgr.TryPlaceHabitat(c)) return true;
                        }
                return false;
            }

            for (int k = 1; k < path.Count - 1; k++)
            {
                if (nest) { if (fmgr.TryPlaceFeature(path[k], DungeonFeatureManager.FeatureType.Spawner)) return true; }
                else { if (fmgr.TryPlaceTrap(path[k])) return true; }
            }

            // ⚠⚠ **経路の上だけでは枠を使い切れない。** 10×10 の経路は十数マスしかないので、
            //   4周目は 9/14 で「配置枠を埋める」が**実行できない手**になったまま成長枠を占め続けた。
            //   人間なら経路の外にも置く（隊は部屋を守れる）ので、道が埋まったら盤の空きへ回す。
            for (int x = 0; x < g.MapWidth; x++)
                for (int y = 0; y < g.MapHeight; y++)
                {
                    if (g.GetTileType(x, y) == DungeonGridSystem.TileType.None) continue;
                    var c = new Vector2Int(x, y);
                    if (nest) { if (fmgr.TryPlaceFeature(c, DungeonFeatureManager.FeatureType.Spawner)) return true; }
                    else if (fmgr.CurrentSquad.Count > 0 && fmgr.TryPlaceSquadMember(c)) return true;
                    else if (fmgr.TryPlaceTrap(c)) return true;
                }
        }
        return false;
    }

    // ⚠ 盤を走査せず、置いてある物の一覧から引く（`ExportFeatures` は表示中でない階も見られる）
    private List<Vector2Int> CellsOf(DungeonFeatureManager fmgr, int floor, DungeonFeatureManager.FeatureType type)
    {
        var outp = new List<Vector2Int>();
        var recs = fmgr.ExportFeatures(floor);
        for (int i = 0; i < recs.Count; i++) if (recs[i].type == type) outp.Add(recs[i].cell);
        return outp;
    }

    private bool SummonBest()
    {
        var res = DungeonResourceManager.Instance;
        if (res == null) return false;
        int best = -1, bestCost = -1;
        for (int i = 0; i < MinionCatalog.Count; i++)
        {
            if (!MinionEvolution.IsUnlocked(i)) continue;
            int c = MinionRoster.SummonCost(i);
            if (c <= res.DungeonPoints && c > bestCost) { best = i; bestCost = c; }
        }
        if (best < 0) return false;
        var v = MinionRoster.TrySummon(best);
        if (v == null) return false;
        // 🧬 召喚したら隊へ（入らなければ召喚だけで成功扱い）
        var fmgr = DungeonFeatureManager.Instance;
        var flr = DungeonFloorManager.Instance;
        if (fmgr != null && flr != null)
            for (int fi = 0; fi < flr.BuiltFloorCount; fi++)
            { flr.SwitchTo(fi); if (fmgr.SquadAdd(v.id)) break; }
        return true;
    }

    private bool ForgeOne()
    {
        var all = MinionRoster.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (MinionRoster.TryForge(all[i].id, EquipmentCatalog.Slot.Weapon)) return true;
            if (MinionRoster.TryForge(all[i].id, EquipmentCatalog.Slot.Armor)) return true;
        }
        return false;
    }

    private bool EvolveOne()
    {
        for (int i = 0; i < MinionCatalog.Count; i++)
            if (MinionEvolution.CanEvolve(i) && MinionEvolution.TryEvolve(i)) return true;
        return false;
    }

    private void Launch(DungeonTurnManager turn)
    {
        // ⚠⚠ **異変に答えないと侵略が始まらない**（`StartBattlePhase` が黙って return する）。
        //   実測：T4 でここに引っかかり、8分間ずっと同じターンを回し続けた。
        if (IncidentSystem.HasPending) { IncidentSystem.Choose(0); return; }
        // ⚠ 空の階があると1度は断られる（事故防止の仕様）。2度押しで通す。
        turn.StartBattlePhase();
        if (!turn.IsBattlePhase) turn.StartBattlePhase();
    }

    // ============ 戦闘：押せる手を押す ============
    private void DoBattle()
    {
        battleActTimer -= Time.unscaledDeltaTime;
        if (battleActTimer > 0f) return;
        battleActTimer = 0.35f;

        string why;
        if (CommandCharge.ReadyToRelease) { CommandCharge.TryRelease(out why); return; }
        for (int i = 0; i < CommandSystem.Count; i++)
            if (CommandSystem.CanUse(i, out why) && CommandSystem.TryUse(i)) return;

        var advs = Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None);
        if (advs.Length == 0) return;

        // 🩸 感情の刈り取り（深手の相手から）
        for (int i = 0; i < advs.Length; i++)
            if (EmotionHarvest.CanReap(advs[i], out why) && EmotionHarvest.TryReap(advs[i], out why)) return;

        // 🔔 誘引／過負荷：冒険者の近くの罠を押す
        var flr = DungeonFloorManager.Instance;
        if (flr == null) return;
        int fi2 = flr.CurrentFloorIndex;
        var fmgr = DungeonFeatureManager.Instance;
        if (fmgr == null) return;
        var traps = CellsOf(fmgr, fi2, DungeonFeatureManager.FeatureType.Trap);
        for (int i = 0; i < traps.Count; i++)
        {
            var c = traps[i];
            if (Decoy.IsSpent(fi2, c)) continue;
            if (Decoy.OverloadLeft > 0 && Decoy.TryOverload(c, fi2, out why)) return;
            if (Decoy.LureLeft > 0 && Decoy.TryLure(c, fi2, out why)) return;
        }
    }

    // ============ 記録 ============
    private void LogWave(int t)
    {
        if (lastLoggedTurn == t) return;
        lastLoggedTurn = t;
        var res = DungeonResourceManager.Instance;
        var fmgr = DungeonFeatureManager.Instance;
        int used = 0, cap = 0, nests = 0, habs = 0;
        if (fmgr != null)
        {
            fmgr.TotalPlacement(out used, out cap, out nests);
            var flr = DungeonFloorManager.Instance;
            if (flr != null) for (int i = 0; i < flr.BuiltFloorCount; i++) habs += HabitatCountOf(fmgr, i);
        }
        Append("| " + t + " | " + WaveReport.Came + " | " + WaveReport.Killed + " | " + WaveReport.Escaped
            + " | " + (res != null ? res.DungeonPoints : 0) + " | " + (res != null ? res.CraftMaterials : 0)
            + " | " + WaveReport.GearLooted + " | " + LureEconomy.GearLevel.ToString("0.0")
            + " | " + Mathf.RoundToInt(WaveReport.LordHpAfter * 100f) + "% | " + used + "/" + cap
            + " | " + nests + " | " + habs + " | " + Strip(WaveReport.Verdict()) + " |\n");
        Append("<!-- T" + t + " 実行: " + Join(doneTitles) + " ／ 出来ず: " + Join(skipTitles)
            + (fallbackPlaced > 0 ? " ／ 進言が尽きたので枠埋め " + fallbackPlaced : "") + " -->\n");
    }

    private int HabitatCountOf(DungeonFeatureManager fmgr, int floor)
    {
        int n = 0;
        var recs = fmgr.ExportFeatures(floor);
        for (int i = 0; i < recs.Count; i++)
            if (recs[i].type == DungeonFeatureManager.FeatureType.Habitat) n++;
        return n;
    }

    private static string Join(List<string> l)
    {
        if (l.Count == 0) return "なし";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < l.Count && i < 10; i++) { if (i > 0) sb.Append(" / "); sb.Append(Strip(l[i])); }
        return sb.ToString();
    }

    private static string Strip(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder();
        bool tag = false;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '<') tag = true;
            else if (s[i] == '>') tag = false;
            else if (!tag && s[i] != '|') sb.Append(s[i]);
        }
        return sb.ToString();
    }

    private void Finish(string why)
    {
        finished = true;
        var turn = DungeonTurnManager.Instance;
        Append("\n**終了：T" + (turn != null ? turn.CurrentTurn : 0) + " ― " + why + "**"
            + "（撃破 " + RunStats.Kills + "／逃走 " + RunStats.Escapes
            + "／直近で捌いた最大 " + FeverSystem.Held + " 体／一人も通さず " + RunStats.BestWaveHeld + " 体）\n");
        Debug.Log("🤖『自動プレイ終了』" + why + " T" + (turn != null ? turn.CurrentTurn : 0));
        enabled = false;
    }

    private void Append(string s)
    {
        try { System.IO.File.AppendAllText(logPath, s, System.Text.Encoding.UTF8); }
        catch (System.Exception e) { Debug.LogWarning("log失敗 " + e.Message); }
    }
}
