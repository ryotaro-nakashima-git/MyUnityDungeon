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
    public int maxTurns = 60;
    /// <summary>🔁 続けて回す周の数。⚠ 1周では判断できない（実測の散らばりが6ターンある）。</summary>
    public int runs = 4;
    private int runIndex;
    public string logPath = "docs/playlog_run9.md";

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
        Append("\n\n# K-1 の計測（生産力と待ち行列／大工事／プロジェクト／購入）\n");
        WriteRunHeader();
    }

    private void Update()
    {
        if (finished) return;
        // 🎬 タイトルで止まらないよう自分で開始を押す。
        //   ⚠ これが無くて **T0 のまま10分溶かした**（表が1行も出ない＝走っていない）。
        if (!GameSetup.Started)
        {
            // ⚠⚠ **プレイ中のドメインリロードをここで検出する。**
            //   再コンパイルが走ると static が全部消える（`GameSetup.Started` も `Instance` も null）が、
            //   MonoBehaviour のフィールドは**シリアライズされて生き残る**ので `autoStartTried` は true のまま。
            //   その結果 `TryAutoStart` が毎フレーム即 return し、**画面は T1 のまま黙って永久に止まる**（実測）。
            notStartedClock += Time.unscaledDeltaTime;
            if (notStartedClock > 90f)
            { Finish("90秒たっても開始しない（プレイ中の再コンパイルで static が消えた可能性。Unityを触らずに走らせ直すこと）"); return; }
            TryAutoStart();
            return;
        }
        notStartedClock = 0f;

        // ☄️ **時代が満ちても、災厄の政策を選ぶまで進まない。**
        //   ⚠ 実測（8周目）：T10 で 75/75 に達したあと **T13 まで胎動のまま止まり**、
        //     4ターンぶんを無駄にして T14 で死んだ。人なら1クリックだが、ボットは押せずに詰まる。
        //   ここで選ぶ。**いちばん被害の軽いものを選ぶのではなく、素直に先頭を選ぶ**
        //   （選び方の巧拙を測りたいのではなく、時代が進む形を測りたいので）。
        if (EraSystem.BlockedOnCrisisPolicy)
        {
            if (EraSystem.TryChooseCrisisPolicy(0))
                Debug.Log("🤖『災厄の政策』を自動で選んだ（時代を進めるため）");
            return;
        }

        var turn = DungeonTurnManager.Instance;
        if (turn == null) return;

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

        // ⚠⚠ **余った研究点と空いた政策枠は、進言を待たずに必ず使う。**
        //   実測（8周目）：RP を 78 貯めたのに**研究したノードは2つだけ**、政策は **0/3** のまま終わった。
        //   進言は上位3件しか出ないので、研究も政策もほとんど勧められない。
        //   これはボット側の穴であって、ゲームの成長が止まっている証拠ではない。
        //   K-0（時代が動くと何が開くか）を測るには、開いた物を実際に使わせないと何も分からない。
        SpendLeftoverRp();
        SlotAnyPolicy();
        KeepQueueFull();
        SpendDpOnPurchase();

        Launch(turn);
    }

    /// <summary>
    /// 🔨 **生産の列を切らさない**（K-1 の計測用）。
    /// ⚠ 進言は生産を勧めないので、これが無いと待ち行列が一度も使われず「変化なし」しか出ない。
    ///   優先順位は **徴募（頭数）→ 建造物 → 大工事**。頭数が壁を動かした唯一の軸だから。
    /// </summary>
    private void KeepQueueFull()
    {
        foreach (var rg in SurfaceMap.All)
        {
            if (!rg.owned || rg.settle == SurfaceMap.Settle.None) continue;
            int rid = rg.id;
            for (int guard = 0; guard < 4; guard++)
            {
                if (ProductionSystem.CountAt(rid) >= 3) break;
                if (TryEnqueueBest(rid)) continue;
                break;
            }
        }
    }

    private bool TryEnqueueBest(int rid)
    {
        string why;
        // ① 徴募（頭数）
        if (ProductionSystem.CanEnqueue(rid, ProductionSystem.Kind.Project, ProductionSystem.Projects.Levy, out why)
            && ProductionSystem.TryEnqueue(rid, ProductionSystem.Kind.Project, ProductionSystem.Projects.Levy))
        { doneTitles.Add("生産『徴募』"); return true; }
        // ② 祝祭
        if (ProductionSystem.CanEnqueue(rid, ProductionSystem.Kind.Project, ProductionSystem.Projects.Festival, out why)
            && ProductionSystem.TryEnqueue(rid, ProductionSystem.Kind.Project, ProductionSystem.Projects.Festival))
        { doneTitles.Add("生産『祝祭の準備』"); return true; }
        // ③ 建造物（いちばん安いもの）
        int bestD = -1, bestC = int.MaxValue;
        for (int i = 0; i < DistrictCatalog.Count; i++)
        {
            if (!ProductionSystem.CanEnqueue(rid, ProductionSystem.Kind.District, i, out why)) continue;
            int c = ProductionSystem.CostOf(ProductionSystem.Kind.District, i);
            if (c < bestC) { bestC = c; bestD = i; }
        }
        if (bestD >= 0 && ProductionSystem.TryEnqueue(rid, ProductionSystem.Kind.District, bestD))
        { doneTitles.Add("生産『" + DistrictCatalog.Get(bestD).jpName + "』"); return true; }
        // ④ 大工事
        for (int i = 0; i < ProductionSystem.Works.Count; i++)
        {
            if (!ProductionSystem.CanEnqueue(rid, ProductionSystem.Kind.Work, i, out why)) continue;
            if (ProductionSystem.TryEnqueue(rid, ProductionSystem.Kind.Work, i))
            { doneTitles.Add("生産『" + ProductionSystem.Works.Name(i) + "』"); return true; }
        }
        return false;
    }

    /// <summary>💰 DPが余っていたら列の先頭を買う（1ターン1件）。⚠ 余剰の行き先ができたかを測るため。</summary>
    private void SpendDpOnPurchase()
    {
        var res = DungeonResourceManager.Instance;
        if (res == null || res.DungeonPoints < 1200) return;   // 手元を空にしない（召喚と配置に要る）
        foreach (var rg in SurfaceMap.All)
        {
            if (!rg.owned || rg.settle == SurfaceMap.Settle.None) continue;
            var it = ProductionSystem.BuildingAt(rg.id);
            if (it == null) continue;
            string why;
            if (!ProductionSystem.CanPurchase(it, out why)) continue;
            if (ProductionSystem.PurchaseCost(it) > res.DungeonPoints - 800) continue;
            if (ProductionSystem.TryPurchase(it)) { doneTitles.Add("購入『" + ProductionSystem.NameOf(it) + "』"); return; }
        }
    }

    /// <summary>🔬 買える中でいちばん安いノードを、買えなくなるまで研究する（1ターン最大6件）。</summary>
    private void SpendLeftoverRp()
    {
        for (int loop = 0; loop < 6; loop++)
        {
            string bestId = null; int bestCost = int.MaxValue;
            var all = ResearchCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                var n = all[i];
                if (ResearchState.IsResearched(n.id)) continue;
                if (!ResearchState.PrereqMet(n) || !ResearchState.EraMet(n) || !ResearchState.GateMet(n)) continue;
                int c = ResearchState.EffectiveCost(n);
                if (c > ResearchState.RP || c >= bestCost) continue;
                bestCost = c; bestId = n.id;
            }
            if (bestId == null) return;
            if (!ResearchState.TryResearch(bestId)) return;
            doneTitles.Add("研究『" + bestId + "』-" + bestCost + "RP");
        }
    }

    /// <summary>🃏 空いている政策枠に、挿せるカードを入れる（1ターン最大3枠）。</summary>
    private void SlotAnyPolicy()
    {
        for (int slot = 0; slot < PolicySystem.SlotCount && slot < 8; slot++)
        {
            if (PolicySystem.SlottedAt(slot) >= 0) continue;
            for (int p = 0; p < PolicySystem.PolicyCount; p++)
            {
                string why;
                // ⚠ `TrySlot` は失敗のたびに警告を出すので、必ず `CanSlot` で先に濾す（コンソールが埋まる）。
                if (!PolicySystem.CanSlot(slot, p, out why)) continue;
                if (PolicySystem.TrySlot(slot, p))
                { doneTitles.Add("政策『" + PolicySystem.Policy(p).jpName + "』を挿した"); break; }
            }
        }
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
        int polUsed = 0;
        for (int i = 0; i < PolicySystem.SlotCount; i++) if (PolicySystem.SlottedAt(i) >= 0) polUsed++;
        string eraCell = EraShort() + " " + EraSystem.Progress + "/" + EraSystem.Need;
        Append("| " + t + " | " + eraCell
            + " | " + WaveReport.Came + " | " + WaveReport.Killed + " | " + WaveReport.Escaped
            + " | " + (res != null ? res.DungeonPoints : 0)
            + " | " + ProductionSystem.TotalProduction + " | " + ProductionSystem.All.Count
            + " | " + MinionRoster.All.Count
            + " | " + ResearchState.RP + " | " + ResearchState.ResearchedCount
            + " | " + polUsed + "/" + PolicySystem.SlotCount + " | " + AttributeSystem.TotalPoints
            + " | " + (res != null ? res.CraftMaterials : 0)
            + " | " + WaveReport.GearLooted + " | " + LureEconomy.GearLevel.ToString("0.0")
            + " | " + Mathf.RoundToInt(WaveReport.LordHpAfter * 100f) + "% | " + used + "/" + cap
            + " | " + nests + " | " + habs
            // 🗺️ ③地上の効きを見る4列：自領タイル／荒らされている数／盤に出ている敵軍／敵対している集落
            + " | " + OwnedTiles() + " | " + PillagedTiles() + " | " + EnemyForce.Count + " | " + HostileRealms()
            + " | " + Strip(WaveReport.Verdict()) + " |\n");
        Append("<!-- T" + t + " 実行: " + Join(doneTitles) + " ／ 出来ず: " + Join(skipTitles)
            + (fallbackPlaced > 0 ? " ／ 進言が尽きたので枠埋め " + fallbackPlaced : "") + " -->\n");
    }

    // ── 🗺️ ③地上を測るための小さな数え役（表に出す4列） ──
    private static int OwnedTiles()
    { int n = 0; foreach (var r in SurfaceMap.All) if (r.owned) n++; return n; }
    private static int PillagedTiles()
    { int n = 0; foreach (var r in SurfaceMap.All) if (r.owned && r.pillagedTurns > 0) n++; return n; }
    private static int HostileRealms()
    {
        int n = 0; var l = DiplomacySystem.Powers;
        for (int i = 0; i < l.Count; i++) if (!l[i].destroyed && l[i].posture >= HumanRealm.Hostile) n++;
        return n;
    }

    private bool autoStartTried;
    private float notStartedClock;

    /// <summary>
    /// 🎬 タイトル画面の『新しい世界を始める』を代わりに押す。
    /// ⚠ `StartNewGame` は private なのでリフレクションで呼ぶ。1度だけ試して、駄目なら理由を書いて止める。
    /// </summary>
    private void TryAutoStart()
    {
        var ui = GameUIManager.Instance;
        if (ui == null) return;                 // まだ生成されていない（次のフレームで再挑戦）
        if (autoStartTried) return;
        autoStartTried = true;
        var mi = typeof(GameUIManager).GetMethod("StartNewGame",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (mi == null) { Finish("StartNewGame が見つからない（自動開始できない）"); return; }
        mi.Invoke(ui, null);
        Debug.Log("🤖『自動開始』タイトルを飛ばして新しい周を始めた");
        if (!GameSetup.Started) Finish("StartNewGame を呼んだが Started にならなかった");
    }

    /// <summary>⏳ 表に入る時代の1文字（K-0 の測定用）。</summary>
    private static string EraShort()
    {
        if (EraSystem.Current == EraSystem.Era.Dawn) return "胎";
        if (EraSystem.Current == EraSystem.Era.Growth) return "伸";
        return "終";
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

    private void WriteRunHeader()
    {
        Append("\n## " + (runIndex + 1) + "周目\n\n"
             + "| T | 時代 | 来襲 | 撃破 | 逃 | DP | 生産 | 列 | 配下 | RP | 研究 | 政策 | 属性 | 素材 | 持逃 | 装備水準 | 魔王HP | 枠 | 巣 | 環境 | 自領 | 荒 | 敵軍 | 敵対 | 決算の一言 |\n"
             + "|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
    }

    /// <summary>
    /// 1周ぶんを閉じる。⚠ **`runs` 回に届くまでは自分で次の周を始める**
    /// （1周ずつ手で回すと1本あたり5分かかり、散らばりを見る前に日が暮れる）。
    /// </summary>
    private void Finish(string why)
    {
        var turn = DungeonTurnManager.Instance;
        int t = turn != null ? turn.CurrentTurn : 0;
        Append("\n**終了：T" + t + " ― " + why + "**"
            + "（撃破 " + RunStats.Kills + "／逃走 " + RunStats.Escapes
            + "／直近で捌いた最大 " + FeverSystem.Held + " 体／一人も通さず " + RunStats.BestWaveHeld
            + "／到達 " + EraSystem.EraName(EraSystem.Current) + " " + EraSystem.Progress + "/" + EraSystem.Need + "）\n");
        Debug.Log("🤖『自動プレイ終了』" + why + " T" + t);

        runIndex++;
        if (runIndex >= runs) { finished = true; enabled = false; Append("\n---\n**全" + runs + "周おわり**\n"); return; }

        // 🔁 次の周へ。
        // ⚠⚠ **`GameSetup.Started = false` だけでは足りない**（実測：2〜4周目が全部 T1 の戦闘で固まった）。
        //   死んだ直後はリザルトのCanvasが出ていて、盤には前の周の冒険者が残り、
        //   ターンは戦闘フェーズのまま。ゲーム自身の後片付けである `BackToTitle` を通す。
        var ui = GameUIManager.Instance;
        if (ui != null)
        {
            var back = typeof(GameUIManager).GetMethod("BackToTitle",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (back != null) back.Invoke(ui, null);
            else Debug.LogWarning("⚠️ BackToTitle が見つからない（次の周が固まる可能性）");
        }
        lastLoggedTurn = -1; prepTurnDone = -1; battleActTimer = 0f;
        watchTurn = -1; watchClock = 0f; notStartedClock = 0f;
        autoStartTried = false;
        GameSetup.Started = false;
        doneTitles.Clear(); skipTitles.Clear(); triedThisTurn.Clear(); fallbackPlaced = 0;
        WriteRunHeader();
    }

    private void Append(string s)
    {
        try { System.IO.File.AppendAllText(logPath, s, System.Text.Encoding.UTF8); }
        catch (System.Exception e) { Debug.LogWarning("log失敗 " + e.Message); }
    }
}
