using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 📖 ターン頭の『腹心の報告』（CDO2の助言役にあたる層）。
///
/// 準備フェーズに入るたびに、
///   ① いまの情勢を **物語調** で1〜2行
///   ② **推奨行動** を最大3つ（なぜそれをするのかも1行つける）
///   ③ そのターン初めて意味を持ったシステムの **説明**（一度きり）
/// を組み立てる。UIは GameUIManager が開く。
///
/// 設計の芯：**盤面から機械的に読み取れる事実だけを根拠にする**。
/// 「余っているDP」「空いている配置枠」「眠っている眷属」のように、
/// *プレイヤーが取りこぼしている選択肢* を拾って順位づけする。飾りの文章は後付け。
///
/// 純static・実行時保持（ドメインリロードで初期化）。関連: [[DungeonTurnManager]] [[GameUIManager]]。
/// </summary>
public static class GuideSystem
{
    public struct Advice
    {
        public string title;   // 何をするか
        public string why;     // なぜ今それなのか
        public int weight;     // 大きいほど優先
        /// <summary>
        /// 🌱 **盤を大きくする手**（頭数・器・階層）。
        ///
        /// ⚠⚠ **3件枠のうち1つをこれに必ず割く。** 3周の通しプレイで、盤は毎回 T4 で成長を止めた
        ///   ―― 拡張の進言がそもそも無く、召喚は weight 55（最下位）で
        ///   **14ターン一度も画面に出なかった**（→ [[playthrough-run3-t14]]）。
        ///   重みを上げるだけでは 76〜99 の渋滞に飲まれるので、**枠の割り当て**で解く。
        /// </summary>
        public bool grow;

        /// <summary>
        /// 📜 **宣言した道の進言**（K-6 A-4）。宣言しているときだけ1件、必ず出す。
        /// ⚠ 重みで competing させない ―― `grow` と同じ理由で 76〜99 の渋滞に負ける。
        ///   こちらは**枠を1つ増やして**（3→4）席を用意する。宣言した人にだけ増える。
        /// </summary>
        public bool path;

        /// <summary>
        /// ▶ <b>『そこへ開く』の行き先</b>（空＝ボタンを出さない）。→ [[k6-and-ui-plan]] A-1
        ///
        /// ⚠⚠ <b>デリゲートにしてはいけない。</b>`GuideSystem` は `SaveSystem.StaticTypes` に載っていて、
        ///   静的フィールドを丸ごと写す方式なので、`System.Action` を持たせると保存で壊れる。
        ///   <b>文字列のキー</b>にして、UI 側で「キー → どの画面を開くか」を解く。
        ///
        /// ⚠ <b>なぜ要るか（実測）</b>：進言は正しく出ていたのに一度も実行されず、
        ///   DPを 3,425 抱えたまま死んでいた。助言と手のあいだに画面遷移が挟まっている限り、
        ///   文章をいくら良くしても届かない。
        ///
        /// キーの形： <c>tool:巣</c>（下部ツールを選ぶ）／<c>panel:研究</c>（全画面を開く）／
        /// <c>floor:deepest</c>（最下層へ移る）／<c>surface:生産</c>（地上のタブ）。
        /// </summary>
        public string go;
        /// <summary>ボタンの文字（空なら「▶ 開く」）。</summary>
        public string goLabel;
    }

    public class Brief
    {
        public int turn;
        public string headline = "";
        public string story = "";
        // ⚠ `readonly` を付けない（[[SaveSystem]] が readonly を「保存しない」の目印にしているため）
        public List<Advice> advices = new List<Advice>();
        public List<string> lessons = new List<string>();
        /// <summary>⏪ 前のターンに起きたこと（Phase A-2）。地上の解決は1フレームで終わるので、ここで見せる。</summary>
        public List<NotifySystem.Notice> results = new List<NotifySystem.Notice>();
        public int gainedDp, gainedMat, gainedRp, gainedFame;   // 前ターンに増えた分
    }

    /// <summary>プレイヤーが「今後は出さない」を選んだか。</summary>
    public static bool Enabled = true;
    /// <summary>まだ開いていない報告があるか。</summary>
    public static bool Unread;
    public static Brief Latest;

    private static HashSet<string> taught;
    private static int lastOwned = -1, lastExpectedLv = -1, lastEra = -1, lastMutCount = -1;

    private static void EnsureInit() { if (taught == null) taught = new HashSet<string>(); }

    public static void Reset()
    {
        taught = new HashSet<string>(); Latest = null; Unread = false;
        lastOwned = -1; lastExpectedLv = -1; lastEra = -1; lastMutCount = -1;
        prevDp = 0; prevMat = 0; prevFame = 0; prevRp = -1;
    }

    /// <summary>
    /// 🌱 **盤そのものを大きくする進言**（W-4 の反省で新設）。
    ///
    /// <para>
    /// ⚠⚠ **これまで1件も無かった。** 見出し34件のどれも「広げろ」「階を足せ」と言わない。
    ///   その結果、3周の通しプレイで**階層は3周とも1のまま**、配置枠は **T4 で頭打ちのまま不動**、
    ///   波は 4人→19人 に増え続けた（→ [[playthrough-run3-t14]]）。
    ///   ＝ 2周目の診断「**呼ぶ人数は青天井、捌く頭数は誰も勧めない**」の後半そのもの。
    /// </para>
    ///
    /// <para>
    /// ⚠ **得だけ書かない。** 広げると敵の人数と質も増え、地形は作り直しになる。
    ///   W-1 で作った `ExpandGainLine` / `ExpandCostLine` を**そのまま**使う（数字を二重に持たない）。
    /// </para>
    /// </summary>
    /// <summary>
    /// 📜 **宣言した道の目標を進言に混ぜる**（K-6 A-4）。
    ///
    /// <para>
    /// ⚠⚠ **どれが得かを式で決めない。**出すのは数えられる事実だけ ――
    ///   いまの点／要る点／**いちばん手を付けていない項目**。
    ///   「この項目を伸ばせば効率がいい」は予想であって事実ではない（→ [[readiness-and-trade]]）。
    /// ⚠ 内訳は `VictorySystem.Breakdown` から取る。**ここで数えない**
    ///   （式が2か所にあると、達成したのに点が動かないように見える）。
    /// ⚠ `why` は**2行まで**。進言カードの枠は 24px しか無く、TMPは枠が足りないと1文字も描かない。
    /// </para>
    /// </summary>
    private static void AddDeclaredPathAdvice(List<Advice> list)
    {
        if (VictorySystem.Decided) return;

        // ◆ 他の魔王の儀 ―― **宣言していなくても出す**（放っておくと負ける事実）。
        int rf = VictorySystem.MostUrgentRivalRite();
        if (rf >= 0)
        {
            int rp = VictorySystem.RitePathOf(rf), pg = VictorySystem.RiteProgressOf(rf);
            list.Add(new Advice
            {
                title = VictorySystem.FactionName(rf) + "の儀を止める",
                why = "『" + VictorySystem.RiteName((VictorySystem.Path)rp) + "』が " + pg + "/" + VictorySystem.RiteTurns
                    + "。成ればこちらの敗北。巣へ攻め込み階を落とすと " + VictorySystem.RiteSetback + " ターン押し戻せる。",
                weight = 95,
                path = true,
                go = "surface:外交",
                goLabel = "▶ 外交"
            });
        }

        // ◆ こちらの儀が開いている（まだ始めていない）
        if (VictorySystem.RitePathOf(VictorySystem.Self) < 0)
            for (int p = 0; p < VictorySystem.PathCount; p++)
            {
                if (!VictorySystem.RiteUnlocked(VictorySystem.Self, (VictorySystem.Path)p)) continue;
                list.Add(new Advice
                {
                    title = "『" + VictorySystem.RiteName((VictorySystem.Path)p) + "』を始める",
                    why = VictorySystem.PathName((VictorySystem.Path)p) + "の道の条件が " + VictorySystem.CondNeed
                        + " つ満ちた。生産の列で " + VictorySystem.RiteTurns + " ターン ―― 成れば勝ち。",
                    weight = 90,
                    path = true,
                    go = "surface:勝利",
                    goLabel = "▶ 勝利"
                });
                break;
            }

        if (!VictorySystem.HasDeclared) return;
        var path = (VictorySystem.Path)VictorySystem.DeclaredPath;
        if (VictorySystem.RitePathOf(VictorySystem.Self) == (int)path) return;   // もう儀をやっている
        var conds = VictorySystem.Conditions(VictorySystem.Self, path);
        int met = VictorySystem.MetCount(VictorySystem.Self, path);
        if (met >= VictorySystem.CondNeed) return;                               // 上の「始める」が出ている

        // まだ満ちていない条件のうち、**いちばん遠い**もの（いま/要る の比が小さい）。行き先を持つものだけ。
        // ⚠ 「どれが得か」ではなく「どれがいちばん手付かずか」という事実だけ（→ [[readiness-and-trade]]）。
        int weak = -1; float wr = 2f;
        for (int i = 0; i < conds.Count; i++)
        {
            if (conds[i].Met || string.IsNullOrEmpty(conds[i].go)) continue;
            float r = conds[i].have / (float)Mathf.Max(1, conds[i].need);
            if (r < wr) { wr = r; weak = i; }
        }

        string why = "宣言した道です。条件は " + met + "/" + conds.Count + "（" + VictorySystem.CondNeed + " つで『"
                   + VictorySystem.RiteName(path) + "』）。";
        if (weak >= 0)
            why += "いちばん遠いのは『" + conds[weak].label + "』 ― " + conds[weak].HaveText + " / " + conds[weak].NeedText + "。";

        list.Add(new Advice
        {
            title = VictorySystem.PathName(path) + "の道を進める",
            why = why,
            weight = 80,
            path = true,
            go = weak >= 0 ? conds[weak].go : "surface:勝利",
            // ⚠ ボタンの幅は 128px しかない。**項目名をそのまま入れない**
            //   （『感情に注いだ数へ』は入りきらず、TMPは枠が足りないと1文字も描かない）。
            goLabel = "▶ " + ShortGo(weak >= 0 ? conds[weak].go : "surface:勝利")
        });
    }

    /// <summary>▶ ボタン用の短い行き先名。`GoToAdvice` のキーの後ろ半分だけを使う。</summary>
    private static string ShortGo(string key)
    {
        if (string.IsNullOrEmpty(key)) return "開く";
        int i = key.IndexOf(':');
        if (i < 0) return key;
        string kind = key.Substring(0, i), what = key.Substring(i + 1);
        if (kind == "dungeon") return "迷宮へ";
        if (kind == "tool") return what + "を置く";
        return string.IsNullOrEmpty(what) ? "開く" : what;
    }

    private static void AddGrowthAdvices(List<Advice> list, int dp)
    {
        var flr = DungeonFloorManager.Instance;
        var fm = DungeonFeatureManager.Instance;
        if (flr == null || fm == null) return;

        // ⚠⚠⚠ **測った結論：いまの作りでは「広げる」ほど早く死ぬ。**（4〜6周目）
        //
        //   | 周 | 拡張の進言 | 決着 |
        //   |---|---|---|
        //   | 4 | 出なかった（重みで負けた） | **T15** |
        //   | 5 | 声を大きくして毎回通した | T12 |
        //   | 6 | 「器を満たしてから」の門つきで通した | **T11** |
        //
        //   理由は3つとも実測できていた：
        //   ①**拡張はその階の配置を全部消していた**（50%返金）＝直後の波を空の盤で迎える
        //     （5周目 T12 は 枠1/54・巣0、6周目 T6 は 14/14 → 7/18 で魔王HP 32%まで削られた）。
        //   ②**階を足すと空の階ができる**。素通りされる（5周目 T7「来襲10・撃破0・逃10」）。
        //   ③広げるDPは**置く物に使えたDP**。10×10 では器より中身が足りない。
        //
        //   ✅ ①は直した（`DungeonFeatureManager.RestoreAfterResize`＝配置を引き継ぐ）。
        //   ⚠ ②③はまだ残っている。**声を上げるのは、次の通しプレイで壁が動いてから。**

        // ⚠⚠⚠ **器を増やす前に、いまの器を満たす。**
        //   5周目の実測：この門が無かったせいで T4/T5 に階層を2つ足し、枠が 14→42 になったまま
        //   **中身は 11 個のまま T11 まで動かなかった**。空の階を素通りされて T7 は
        //   「来襲10・撃破0・逃10」。さらに T12 の拡張で B1F の配置が全部消え（W-1 の代償）、
        //   **枠 1/54・巣 0** で押し切られた ―― **T15 → T12 に悪化した**。
        //   ＝ 広げること自体は害ではなく、**満たせないまま広げること**が害。
        //   ⚠ 「波に追い抜かれている」だけでは足りない。それは*広げる*理由であって、
        //     *いま広げてよい*理由ではない。
        // 🏛️ **巨大施設は「広げた者にだけ見える報酬」**（X-1）。
        //   ⚠ ここは器の門より**前**に置く。建てられる場所が既にあるなら、
        //     それは「広げろ」ではなく「広げた成果を受け取れ」という別の話だから。
        //   ⚠⚠ 練兵場は **面積を隊枠に変える唯一の道**。通しプレイ7周で壁を動かしたのは
        //     恒久的な頭数だけだった（配下2→7で T14→T17）→ [[growth-is-a-trap]]。
        for (int i = 0; i < flr.BuiltFloorCount; i++)
        {
            if (fm.CountGreatWork(i, GreatWorkCatalog.Kind.DrillGround) > 0) continue;
            if (!fm.AnyGreatWorkSpot(i)) continue;
            int gwCost = GreatWorkCatalog.Get((int)GreatWorkCatalog.Kind.DrillGround).dpCost;
            if (dp < gwCost) continue;
            list.Add(new Advice
            {
                title = "B" + (i + 1) + "F に『練兵場』を建てる（下部『巨大』）",
                go = "tool:巨大", goLabel = "▶ 巨大を置く",
                why = $"広げた B{i + 1}F に <b>{GreatWorkCatalog.Size}×{GreatWorkCatalog.Size} の空き</b>ができています。"
                    + $"練兵場はその階の<b>隊の枠 +1</b> ―― <b>面積を、周を通して育つ頭数に変える唯一の建物</b>です"
                    + $"（DP {gwCost}／所持 {dp}）。",
                weight = 93,
                grow = true
            });
            break;
        }

        int used, cap, nests;
        fm.TotalPlacement(out used, out cap, out nests);
        if (used < cap - 2) return;

        // 🏢 階層を足す ―― 枠が丸ごと1階ぶん増える（`PlacementCap` は階ごとに立つ）
        if (flr.CanAddFloor())
        {
            int cost = flr.AddFloorDPCost();
            if (dp >= cost)
                list.Add(new Advice
                {
                    title = "階層をもう1つ増やす（上部『拡張』→ ＋第" + (flr.BuiltFloorCount + 1) + "層）",
                go = "panel:拡張", goLabel = "▶ 拡張を開く",
                    why = $"次の波は <b>{WaveRoster.Count} 人</b>、直近で捌けたのは <b>{FeverSystem.Held} 人</b>です。"
                        + "階を足すと<b>置ける枠が丸ごと1階ぶん増え</b>、"
                        + "冒険者が魔王に届くまでの道のりも1階ぶん伸びます。"
                        + $"深い階ほど撃破の実りも増えます（DP {cost}／所持 {dp}）。",
                    // ⚠⚠ **声を大きくしない。** 6周目の実測では、この進言を上位に押し上げた周ほど
                    //   **早く死んだ**（T15 → T12 → T11）。詳しくは `AddGrowthAdvices` の頭。
                    weight = 74,
                    grow = true
                });
        }

        // 🗺️ いまの階を広げる。
        //   ⚠ 以前は拡張がその階の配置を全部消していたので、置き直すDPが無いまま広げると
        //     盤が空のまま次の波を迎えた（5周目 T12・6周目 T6）。**その仕様は直した**
        //     （→ `DungeonFeatureManager.RestoreAfterResize`）ので、DPの門は緩めてよい。
        //   ⚠ それでも地形は作り直しなので、置けない物は出る。多少の余裕は要る。
        if (dp < 400) return;
        for (int i = 0; i < flr.BuiltFloorCount; i++)
        {
            if (!flr.CanExpandFloor(i)) continue;
            int rp = flr.ExpandRPCost(i), dpc = flr.ExpandDPCost(i);
            if (ResearchState.RP < rp || dp < dpc) continue;
            list.Add(new Advice
            {
                title = "B" + (i + 1) + "F を広げる（上部『拡張』）",
                go = "panel:拡張", goLabel = "▶ 拡張を開く",
                why = $"置ける枠が {used}/{cap} で埋まっています。広げないとこれ以上厚くできません。<br>"
                    + flr.ExpandGainLine(i) + $"　<color=#8cb8e6>{rp} RP</color> <color=#e3a94a>{dpc} DP</color><br>"
                    + flr.ExpandCostLine(i),
                weight = 76,
                grow = true
            });
            break;   // ⚠ 1件だけ（全階ぶん並べると3枠を拡張だけで埋めてしまう）
        }
    }

    /// <summary>準備フェーズに入った瞬間に呼ぶ（DungeonTurnManager／開始時）。</summary>
    public static void OnTurnStart(int turn)
    {
        EnsureInit();
        Latest = Build(turn);
        if (Enabled) Unread = true;
    }

    // ============ 組み立て ============
    /// <summary>前ターン終わりの資源（差分を出すために覚えておく）。</summary>
    private static int prevDp, prevMat, prevFame, prevRp = -1;

    /// <summary>
    /// ⏪ 戦闘の頭（準備で使い終えたあと）を起点にする。⚠ 以前はターンの頭どうしの差で、
    ///   準備で使った分まで引かれ、稼いだのに「DP −2,778・研究点 −24」と出ていた（通しプレイで発見）。
    /// </summary>
    public static void NoteBattleStart()
    {
        var r = DungeonResourceManager.Instance;
        if (r == null) return;
        prevDp = r.DungeonPoints; prevMat = r.CraftMaterials; prevFame = r.DungeonFame; prevRp = ResearchState.RP;
    }

    private static Brief Build(int turn)
    {
        var b = new Brief { turn = turn };

        // ⏪ 前のターンに起きたことを拾う（重要度 Info 以外）。多すぎると読めないので8件まで。
        var res0 = DungeonResourceManager.Instance;
        foreach (var n in NotifySystem.OfTurn(turn - 1))
        {
            if (b.results.Count >= 8) break;
            b.results.Add(n);
        }
        if (res0 != null && prevRp >= 0)
        {
            b.gainedDp = res0.DungeonPoints - prevDp;
            b.gainedMat = res0.CraftMaterials - prevMat;
            b.gainedFame = res0.DungeonFame - prevFame;
            b.gainedRp = ResearchState.RP - prevRp;
        }
        prevRp = -1;   // ⚠ 次の起点は次の戦闘の頭（戦わずに進んだターンは出さない）
        var res = DungeonResourceManager.Instance;
        var dl = DemonLord.Instance;
        var fm = DungeonFeatureManager.Instance;
        int dp = res != null ? res.DungeonPoints : 0;
        int mat = res != null ? res.CraftMaterials : 0;
        int owned = SurfaceMap.OwnedCount;
        int expLv = AdventurerAI.ExpectedLevelNow();
        int era = (int)EraSystem.Current;
        float hp = dl != null ? dl.HPRatio : 1f;

        // ---- ① 情勢（物語調）----
        if (turn <= 1)
        {
            b.headline = "はじまりの静けさ";
            b.story = "地の底に穿たれた穴は、まだ誰にも知られていない。\n"
                    + "けれど噂は水のように低いところへ流れる。――間もなく、最初の足音が来る。";
        }
        else if (hp < 0.4f)
        {
            b.headline = "玉座に届いた刃";
            b.story = "あなたの体には、まだ塞がらない傷がある。\n"
                    + "次の波を同じように迎えれば、この階は墓所になる。";
        }
        else if (MutationSystem.ActiveCount > lastMutCount && lastMutCount >= 0)
        {
            var nk = MutationSystem.ActiveAt(MutationSystem.ActiveCount - 1);
            b.headline = "世界が形を変えた";
            b.story = $"降りてくる者たちの様子が変わった。――『{MutationSystem.Get(nk).jpName}』。\n"
                    + MutationSystem.Get(nk).desc.Replace("**", "") + "\n"
                    + $"同じ盤のままでは、昨日ほど通らない。対策：{MutationSystem.Get(nk).counter}。";
        }
        else if (lastEra >= 0 && era > lastEra)
        {
            b.headline = "時代が変わった";
            b.story = "地上の人々が語る言葉が変わった。祈りの形も、鋼の鍛え方も。\n"
                    + "彼らが強くなるということは、あなたも古い手を捨てるということだ。";
        }
        else if (lastOwned >= 0 && owned > lastOwned)
        {
            b.headline = "版図が伸びた";
            b.story = $"あなたの旗は {owned} の地に立った。\n"
                    + "獲った土地は富を生むが、同じだけ守る手が要る。";
        }
        else if (lastExpectedLv >= 0 && expLv > lastExpectedLv)
        {
            b.headline = "強い者が来る";
            b.story = $"門の外の噂が変わった。次に降りてくるのは Lv{expLv} 前後の腕利きだ。\n"
                    + "昨日と同じ備えは、今日の備えではない。";
        }
        else
        {
            switch (turn % 3)
            {
                case 0:
                    b.headline = "灯りの下で";
                    b.story = "配下たちが石を積み、罠の歯を研いでいる。\n"
                            + "静かな時間こそが、次の勝敗を決めている。";
                    break;
                case 1:
                    b.headline = "地上の風";
                    b.story = "地上では、まだ誰かがこの穴を「宝の山」と呼んでいる。\n"
                            + "その勘違いこそが、あなたの糧だ。";
                    break;
                default:
                    b.headline = "深いところへ";
                    b.story = "下へ行くほど魔素は濃い。濃いところで戦った者だけが、速く強くなる。\n"
                            + "誰をどこに立たせるかは、あなたの筆一本にかかっている。";
                    break;
            }
        }

        // ---- ② 推奨行動 ----
        var list = new List<Advice>();

        // ⚠⚠ **余っている資源の進言は、余っている量で重みを変える**（通しプレイの実測）。
        //   2周とも **資源を余らせたまま**壁に当たった：
        //   1周目 DP 40,210・素材 636 ／ 4周目 DP 17,120・素材 281・BP 76 が未使用。
        //   これらの進言は前からあったが、weight 45〜70 が固定で、
        //   88〜92 の進言（進化・遺物・感情）に**一度も勝てなかった**。
        //   → 「40くらい貯まっている」は小声、「300貯まっている」は**画面で一番大きい声**にする。
        //   ⚠ 一律に上げない。少量のときまで叫ぶと、今度はこれが他を潰す。
        if (dl != null && dl.BP > 0)
            list.Add(new Advice
            {
                title = "魔王のステータスにBPを振る",
                go = "panel:魔王", goLabel = "▶ 魔王を開く",
                why = $"BPが {dl.BP} 眠っています。振らないぶんは丸ごと損です。",
                weight = 70 + Mathf.Min(26, dl.BP)          // BP20で90／BP26以上で96
            });

        if (fm != null && fm.PlacedCount < fm.PlacementCap && dp >= 200)
        {
            int empty = fm.PlacementCap - fm.PlacedCount;
            list.Add(new Advice
            {
                // ⚠ 旧称『スポナー』のまま残っていた（下部ツールの表示は 🪺巣 / 🌿環境）。
                //   進言と画面で名が違うと「どれのことか」が分からず、押されない → [[nest-and-habitat]]
                title = "配置枠を埋める（罠・巣・トーテム）",
                go = "tool:罠", goLabel = "▶ 置く",
                why = $"枠が {empty} 空いていて、DPは {dp} あります。空き枠は稼がない枠です。"
                    + (dp >= 3000 ? "　<color=#e05a5a>DPは足りています。足りないのは置いた物です。</color>" : ""),
                weight = 66 + Mathf.Min(30, empty * 3),     // 10空きで96
                grow = true
            });
        }

        // 🌱 **盤そのものを大きくする**（3周の通しプレイで一度も起きなかった → [[playthrough-run3-t14]]）
        AddGrowthAdvices(list, dp);

        string rid = FirstAffordableResearch();
        if (rid != null)
            list.Add(new Advice { title = "研究を進める（" + rid + "）", why = $"研究点が {ResearchState.RP} 貯まっています。天啓が付いているものは4割引です。", weight = 64,
                go = "panel:研究", goLabel = "▶ 研究を開く" });

        int nameable = FirstNameableIndividual();
        if (nameable >= 0 && KinRoster.Count == 0)
            list.Add(new Advice
            {
                title = "真名を与えて眷属をつくる",
                go = "panel:魔物", goLabel = "▶ 魔物を開く",
                why = "条件を満たした個体がいます。眷属がいないと地上へ一歩も出られません。",
                weight = 95
            });
        else if (nameable >= 0)
            list.Add(new Advice { title = "もう1体、眷属をつくる", why = "条件を満たした個体がいます。侵攻と防衛を同時に回せるようになります。", weight = 50,
                go = "panel:魔物", goLabel = "▶ 魔物を開く" });

        int idle = IdleKinCount();
        if (idle > 0)
            list.Add(new Advice
            {
                // ⚠ この進言はターン頭に出るが、**地上へ出られるのは防衛戦のあと（後半）**。
                //   旧文は「進軍させる」とだけ言っていたので、探しても行き先が無く手が止まった。
                //   いつ・どうやるのかまで書く。
                title = "眷属を進軍させる（防衛戦のあと・地上）",
                go = "surface:眷属", goLabel = "▶ 眷属へ",
                why = idle + "体が待機したままです。敵領は<b>隣接してからでないと攻められない</b>ので、"
                    + "届かないときは<b>まず前線の自領まで移動</b>し、次のターンに攻めます。",
                weight = 72
            });

        if (KinRoster.Count > 0 && CanFoundSomewhere())
            list.Add(new Advice
            {
                title = "拠点を築いて版図を広げる",
                go = "surface:領域", goLabel = "▶ 地上へ",
                why = "拠点は周囲のタイルを自領に変え、人口が増えると版図がさらに広がります。",
                weight = 60
            });

        if (AttributeSystem.TotalPoints > 0)
            list.Add(new Advice
            {
                title = "属性ポイントを使う（地上メニュー『属性』）",
                go = "surface:属性", goLabel = "▶ 属性へ",
                why = "偉業で得た点が " + AttributeSystem.TotalPoints + " 残っています。属性は時代をまたいで残る恒久強化です。",
                weight = 74
            });

        int freeSlots = EmptyPolicySlots();
        if (freeSlots > 0)
            list.Add(new Advice
            {
                title = "政策を差す（地上メニュー『政策』）",
                go = "surface:政策", goLabel = "▶ 政策へ",
                why = "スロットが " + freeSlots + " 空いています。差し替えは準備フェーズなら無料です。",
                weight = 68
            });

        // 🧟 **頭数**。⚠ 元は weight 55（全進言の最下位）で、3周目は **14ターン一度も画面に出なかった**。
        //   波は 4人→19人 に増えるのに、配下は 2 体のままだった（→ [[playthrough-run3-t14]]）。
        //   → **どれだけ数負けしているか**で重みを決める（人数の差という事実に基づく）。
        if (dp >= 600)
        {
            int mine = MinionRoster.All.Count;
            int gap = Mathf.Max(0, WaveRoster.Count - mine);
            list.Add(new Advice
            {
                title = "配下を召喚して数を増やす",
                why = $"次の波は <b>{WaveRoster.Count} 人</b>、こちらの配下は <b>{mine} 体</b>です。"
                    + $"DPが {dp} あります。"
                    // ⚠ 枠の残りを必ず添える。実測で配下を41体に増やしても盤に立てるのは枠までで、
                    //   撃破はむしろ減った（裸の41体になる）→ [[wall-is-placement-cap]]
                    + (fm != null
                        ? $"<color=#9c95b4>⚠ 置ける枠は残り <b>{Mathf.Max(0, fm.PlacementCap - fm.PlacedCount)}</b>。"
                          + "超えて召喚しても盤には立ちません。</color>"
                        : ""),
                weight = 62 + Mathf.Min(30, gap * 3),
                grow = true,
                go = "panel:魔物", goLabel = "▶ 魔物を開く"
            });
        }

        // ⚠⚠ **上限に達しているときは言わない。** 全員が上限なのに「鍛えろ」と言い続けると、
        //   **できないことを1位で指し続ける**ことになる（実測：上限で 97 のまま居座った）。
        //   その場合は下の「錬成を上げて上限を開く」が引き継ぐ。
        if (mat >= 40 && !AllPlacedAtForgeCap())
            list.Add(new Advice
            {
                title = "装備を鍛える（『図鑑』→ 個体の武器・防具）",
                go = "panel:魔物", goLabel = "▶ 魔物を開く",
                why = $"素材が {mat} 眠っています。抱えていても強くなりません。"
                    + "<b>1段でおよそ +22%</b>（レベル5〜6ぶん）。"
                    + (mat >= 200 ? "　<color=#e05a5a>数を増やすより、いま居る配下を鍛えるほうが効きます。</color>" : ""),
                // ⚠⚠ 実測で足りなかった：**素材182 のとき 45+182/6 = 75** にしかならず、
                //   常設の 88（遺物）/87（研究）/86（感情）に負けて**一度も出なかった**。
                //   その結果 T9 の配下は **武器も防具もグレード0**（素手）のまま壁に当たった。
                //   素材は撃破からしか出ないので、100 も貯まっていれば「使っていない」証拠。傾きを立てる。
                weight = 45 + Mathf.Min(52, mat / 3)        // 素材100で78／**156で97**
            });

        // 🔓 **鍛造の上限に当たっているのに素材が余っている**（→ [[playthrough-t14-era-wall]]）。
        //   ⚠⚠ 通しプレイ T14 の壁の正体。「装備を鍛える」と言い続けた末に上限へ着いても、
        //     これまで**次に何をすればいいか誰も言わなかった**ので、素材341とDP552を
        //     抱えたまま負けた。上限のときは「上限を開ける手」を名指しする。
        //   ⚠ 錬成の道は**時代に縛られない**ので、こちらを先に言う。
        if (mat >= 60 && dl != null && AllPlacedAtForgeCap())
        {
            int refine = dl.GetStatRank((int)DemonLord.Stat.Refine);
            if (refine < 5)
            {
                int need = refine < 3 ? 3 : 5;
                int bpNeed = 0;
                for (int r = refine; r < need; r++) bpNeed += RankUpCostOf(r);
                list.Add(new Advice
                {
                    title = "魔王の『錬成』を上げて、鍛造の上限を開く",
                go = "panel:魔王", goLabel = "▶ 魔王を開く",
                    why = $"配下は全員が鍛造の上限（{EquipmentCatalog.Name(EquipmentCatalog.ResearchGradeCap() + dl.ForgeGradeBonus)}）で、"
                        + $"素材が {mat} 余っています。<b>錬成 {"EDCBAS"[need]} まで上げると上限が1段開き</b>、"
                        + $"その素材が力に変わります（BP {bpNeed} ／所持 {dl.BP}）。"
                        + "　<color=#9c95b4>研究『" + EquipmentCatalog.NextGradeResearchName(EquipmentCatalog.ResearchGradeCap())
                        + "』でも開きますが、そちらは時代が進むまで取れません。</color>",
                    weight = 92
                });
            }
        }

        // 🪩 **巣を置いていない**。⚠⚠ 実測：同じ波で「罠14＋隊5」が撃破9・DP+637 だったのに対し
        //   「スポナー9＋隊5」は撃破15・取り逃がし0・DP+1,632・防衛体の損失0。
        //   **強いのに誰も置かなかった**（私も進言も）。「配置枠を埋める」と3つ並べるだけでは
        //   どれが効くか伝わらない → [[playthrough-t14-era-wall]]。
        // ⚠⚠ **`== 0` をやめた。** 1つ置いた瞬間にこの進言が**永久に消えて**いたので、
        //   実測ではどの周も**巣1・環境1のまま**終わっていた ―― 巣4つで壁が T13→T17 動いた軸なのに。
        //   条件は「足りているか」にする：**1つの巣が捌けるのは 5〜6体/波**なので、
        //   次の波の人数を 6 で割ったぶんが要る（→ [[wall-is-placement-cap]]）。
        if (fm != null && fm.PlacedCount > 0 && dp >= 300)
        {
            int nests = fm.NestCount;
            int want = Mathf.Max(1, Mathf.CeilToInt(WaveRoster.Count / 6f));
            if (nests < want && fm.PlacedCount < fm.PlacementCap)
                list.Add(new Advice
                {
                    title = nests == 0 ? "巣を置く" : "巣をもう1つ置く",
                    why = "この階の巣は <b>" + nests + " つ</b>。巣から湧いた配下は<b>配置枠を食わない</b>ので、"
                        + "枠1つを巣にすると <b>5〜6体/波</b>になります（配下を置けば1体）。"
                        + "次の波は <b>" + WaveRoster.Count + " 人</b>、"
                        + "直近で捌けたのは <b>" + FeverSystem.Held + " 人</b>。",
                    // ⚠ どれだけ足りないかで重みを決める（事実に基づく）。満たしたら出ない。
                    weight = 84 + Mathf.Min(12, (want - nests) * 4),
                    grow = true,
                    go = "tool:巣", goLabel = "▶ 巣を置く"
                });
        }

        // 🌿 巣はあるのに環境が無い。⚠ 巣を置いた人にだけ出す（順番に意味がある）
        // 🌿 環境も同じ。⚠ 重ねがけは巣1つにつき `HabitatCatalog.MaxStack`（2つ）まで効く。
        //   3つ目は効かないので、そこで進言も止まる（＝「並べるほど強い」の嘘をつかない）。
        if (fm != null && fm.NestCount > 0 && dp >= 200 && fm.PlacedCount < fm.PlacementCap)
        {
            int hab = fm.HabitatCount, wantH = fm.NestCount * HabitatCatalog.MaxStack;
            if (hab < wantH)
                list.Add(new Advice
                {
                    title = hab == 0 ? "巣の隣に『環境』を置く" : "環境をもう1つ置く",
                    why = "巣 <b>" + fm.NestCount + " つ</b>に対して環境は <b>" + hab + " つ</b>。"
                        + "<b>2マス以内</b>の苔床（速く）・水源（多く）・餌場（強く）で湧き方が変わります。"
                        + "<color=#9c95b4>効くのは巣1つにつき2つまで。</color>",
                    weight = 82 + Mathf.Min(10, (wantH - hab) * 3),
                    grow = true,
                    go = "tool:環境", goLabel = "▶ 環境を置く"
                });
        }

        if (fm != null && fm.PlacedCount == 0)
            list.Add(new Advice { title = "まず罠を1つ置く", why = "何も置かないまま迎えると、冒険者は無傷でボスに届きます。", weight = 99,
                go = "tool:罠", goLabel = "▶ 罠を置く" });

        // ⏳ 時代が満ちているのに止まっている（→ [[EraSystem]]）。
        //   ⚠ 通しプレイで **210/210 のまま3ターン**動かなかった。罠より上に置く
        //     ―― 罠が無いのは「弱い」だけだが、これは**ゲームが進んでいない**。
        if (EraSystem.BlockedOnCrisisPolicy)
            list.Add(new Advice
            {
                title = "災厄の政策を選ぶ（地上メニュー『時代』）",
                go = "surface:時代", goLabel = "▶ 時代へ",
                why = EraSystem.EraName(EraSystem.Current) + "は満ちています（" + EraSystem.Progress + "/"
                    + EraSystem.Need + "）。政策を1つ選ぶまで時代は進みません ―― このターンは何も進んでいません。",
                weight = 100
            });

        // 🔥 ⚠ **殻で見る。** 魔王のHPは波ごとに殻の残量から始まるので、`hp` を見ると
        //   準備フェーズの一瞬の値に振り回される。削られたまま残るのは殻のほう（→ [[LordBerserk]]）。
        if (LordBerserk.Shell < 0.5f)
            list.Add(new Advice { title = "最下層の守りを厚くする",
                why = "魔王の殻が <b>" + Mathf.RoundToInt(LordBerserk.Shell * 100f) + "%</b> まで削れています。"
                    + "殻は<b>次の波で倒した割合に応じて " + Mathf.RoundToInt(LordBerserk.RecoverFor(0f) * 100f) + "〜" + Mathf.RoundToInt(LordBerserk.RecoverFor(1f) * 100f) + "% 戻ります</b>（逃がすほど戻らない）。"
                    + "もう一度割られると第二形態（次は " + LordBerserk.NextPhaseText + "）に入ります。", weight = 90,
                go = "floor:deepest", goLabel = "▶ 最下層へ" });

        if (AnyFreeTrainingSlot())
            list.Add(new Advice { title = "訓練所に配下を送る", why = "空きがあります。4ターン預ければ、戦えなかった個体も追いつきます。", weight = 42,
                go = "panel:魔物", goLabel = "▶ 魔物を開く" });

        // 🧬 世界の変異：抑制が置いていかれると、盤を組み替えても追いつかなくなる
        if (MutationSystem.ActiveCount >= 2 && MutationSystem.Suppress <= 0f)
            list.Add(new Advice
            {
                title = "領域研究『順応』を取る",
                go = "panel:研究", goLabel = "▶ 研究を開く",
                why = $"世界の変異が {MutationSystem.ActiveCount} 種。抑制が 0% のままだと、変異は書いてある量そのままで効きます。",
                weight = 85
            });
        else if (MutationSystem.ActiveCount >= 5 && MutationSystem.Suppress < 1.0f)
            list.Add(new Advice
            {
                title = "抑制を積む（異相の解剖／変異抑制）",
                go = "panel:研究", goLabel = "▶ 研究を開く",
                why = $"変異 {MutationSystem.ActiveCount} 種に対して抑制 {MutationSystem.SuppressLabel}。効きは 量÷(1+抑制) なので、積むほど全部が薄まります。",
                weight = 72
            });

        // ============ 🔰 まだ一度も触っていない系統を優先で出す ============
        // ⚠⚠ 通しプレイ12ターンで、**感情ツリー・遺物・装飾品・鍛造・ガチャ・行商人に一度も触らなかった**。
        //   どれも実装済みで、しかも全部『図鑑』パネルの中にあり、常時使える。
        //   触らなかったのは難しいからではなく、**進言が一度も指さなかったから**。
        //   その結果 DP が 6,800 余った（＝使い道が無いのではなく、使い道を知らなかった）。
        // ⚠ 「初めての1回」だけ強く押す。一度でも使った系統は、以降ここから出さない（うるさくなる）。
        //   weight は既存の最上位（進軍72・属性74）より少し上に置き、**必ず3枠のどれかに入る**ようにする。
        {
            // ⚠⚠ **窓を切る。** ここの進言は「初めての1回だけ強く押す」つもりで 82〜86 に置いてあるが、
            //   **押しても押さなくても条件が変わらない**ので、無視され続けると**永久に上位に居座る**。
            //   実測：T9 まで遺物88/研究87/感情86 が3枠を占め続け、
            //   「素材が182眠っている」がその下に沈んで**一度も見えなかった**。
            //   → T18 を過ぎたら引っ込む。18ターン見せて触らないなら、知らないのではなく選んでいる。
            bool firstTimeWindow = turn <= 18;

            var emo = EmotionTreeManager.Instance;
            if (firstTimeWindow && emo != null && emo.TotalSpent == 0 && turn >= 3)
                list.Add(new Advice
                {
                    title = "感情ツリーを開く（上部『感情』）",
                go = "panel:感情", goLabel = "▶ 感情を開く",
                    why = "まだ1つも開いていません。感情は貯めても何も起きません。開けば配下すべてが恒久的に強くなります。",
                    weight = 86
                });

            if (firstTimeWindow && EurekaTracker.Count("forge") == 0 && dp >= 400)
                list.Add(new Advice
                {
                    title = "武具を鍛える（『図鑑』→ 個体の武器・防具）",
                go = "panel:魔物", goLabel = "▶ 魔物を開く",
                    why = "まだ1つも鍛えていません。1段でおよそ +22%（レベル5〜6ぶん）。DPの最も確実な使い道です。",
                    weight = 84
                });

            if (firstTimeWindow && AccessoryInventory.TotalCount == 0 && dp >= 800)
                list.Add(new Advice
                {
                    title = "行商人から装飾品を買う（『図鑑』の商いの欄）",
                go = "panel:魔物", goLabel = "▶ 魔物を開く",
                    why = "装飾品は1個体につき1つ、魔物スキルを丸ごと付けられます。品揃えはターンごとに変わり、買った枠は戻りません。",
                    weight = 82
                });

            // 🌱 手札が広がらないまま進んでいる（→ [[MinionEvolution]]）。
            //   ⚠ 通しプレイ T1〜T30 で研究を94節も進めたのに、召喚できる種類は**7のまま**だった。
            //     進化は安い（段×25DP）ので詰まりは値段ではなく、**一度も指さされないこと**。
            int evolvable = MinionEvolution.EvolvableCount();
            if (evolvable > 0)
                list.Add(new Advice
                {
                    title = "配下を進化させて手札を広げる（『図鑑』の進化）",
                go = "panel:魔物", goLabel = "▶ 魔物を開く",
                    why = "いま " + evolvable + " 種類を解禁できます。召喚できるのは "
                        + MinionEvolution.UnlockedCount() + " 種類のまま。役割が偏ったままでは部隊バフも伸びません。",
                    weight = 88
                });

            // 🕸️ 世界がまったく動いていない（→ [[LureStance]]）。
            //   ⚠ 通しプレイ T1〜T30 は逃走0で、脅威度・装備水準・因縁が一度も動かなかった。
            // ⚠ 窓を切る。使わないままだと条件が永久に真なので、上限が無いと**毎ターン居座る**。
            //   20ターンも見せて使わないなら、それは知らないのではなく選んでいない。
            if (turn >= 4 && turn <= 20 && LureEconomy.Threat < 1.02f && Nemesis.AtLargeCount == 0)
                list.Add(new Advice
                {
                    title = "一度『泳がせて』みる（下部バー『◇ 泳がせ』）",
                    why = "外の世界がまだ一度も動いていません。生きて還った者が噂を運び、脅威度と装備水準が上がり、"
                        + "名のある冒険者が生まれます。強い相手ほど倒したときの実りも大きくなります。",
                    // ⚠ 88台の進言が渋滞していて weight 80 では**3枠に一度も入らなかった**（実測）。
                    //   条件そのものが「世界が一度も動いていない」という異常なので、上に置いてよい。
                    weight = 92
                });

            // 🔥 波が軽いまま進んでいる（→ [[FeverSystem]]）。
            // ⚠⚠ **「捌く用意」を言わない進言は無責任**（W-2）。旨さ（Forecast）だけ並べて
            //   90 で1位に置き続けると、守りが薄い側にも同じ強さで勧めてしまう。
            //   ＝このプロジェクトで4回やった失敗（できないことを上位で指し続ける）と同じ形。
            //   → 危ない見込みのときは**言葉と重みの両方を変える**。禁止はしない（賭けは取り上げない）。
            if (turn >= 5 && turn <= 20 && FeverSystem.CalledTurn < 0 && dl != null && dl.IsAlive)
            {
                var rd = FeverSystem.ReadinessOf(FeverSystem.ForecastCount);
                list.Add(new Advice
                {
                    title = rd == FeverSystem.Ready3.Risky
                        ? "『◆ 大招集』は守りを厚くしてから"
                        : "『◆ 大招集』で自分から波を呼ぶ",
                    why = (rd == FeverSystem.Ready3.Risky
                            ? "波は自分で重くできますが、いまの守りでは重すぎます。"
                            : "守り切れているなら、波は自分で重くできます。")
                        + FeverSystem.Forecast() + "　" + FeverSystem.ReadinessLine(),
                    weight = rd == FeverSystem.Ready3.Risky ? 62 : (rd == FeverSystem.Ready3.Tight ? 84 : 90)
                });
            }

            if (string.IsNullOrEmpty(SummonGacha.LastResult) && dp >= SummonGacha.Cost * 2)
                list.Add(new Advice
                {
                    title = "召喚の儀を引く（『図鑑』の召喚の儀）",
                    why = $"DPが {dp} あります。ここでしか出ないユニーク個体がいて、外しても解禁済みの配下が必ず1体は付いてきます。",
                    weight = 78
                });

            var rel = RelicManager.Instance;
            // ⚠ 遺物も同じ窓の中だけ。**挿すまで条件が消えない**ので、放っておくと永久に88で居座る
            //   （実測：T9まで3枠を遺物88/研究87/感情86が占め、素材182の鍛造が沈んでいた）。
            if (firstTimeWindow && rel != null && rel.UnlockedCount > 0 && !AnyRelicEquipped())
                list.Add(new Advice
                {
                    title = "遺物を装備する（上部『遺物』）",
                    why = $"手に入れた遺物が {rel.UnlockedCount} 個、棚に置いたままです。挿さないと効果は出ません。",
                    weight = 88
                });
        }

        // ============ 🔭 先触れと備え（毎ターンの判断） ============
        // ⚠ ここは**毎ターン**出してよい唯一の系統。相手が毎ターン変わるので、
        //   「一度触ったからもう出さない」にすると判断そのものが習慣にならない。
        //   ただし**張り終えたら黙る**（済んだことを言い続けない）。
        if (!ResearchState.IsResearched("d_omen1") && turn >= 2)
            list.Add(new Advice
            {
                title = "『耳を澄ます』を研究する（領域研究）",
                why = "次に何体来るかも分からないまま迎えています。読めれば、その波に合わせて盤を組み替えられます。",
                weight = 87
            });
        else if (!WardSystem.Unlocked && ResearchState.IsResearched("d_omen1"))
            list.Add(new Advice
            {
                title = "『備えの心得』を研究する（領域研究）",
                why = "読めても打つ手が無ければ情報は飾りです。備えは相手の得意を1つ潰す、そのターン限りの一手です。",
                weight = 85
            });
        else if (WardSystem.Unlocked && WardSystem.Selected < 0 && dp >= 300)
            list.Add(new Advice
            {
                title = "備えを1つ張る（上部『先触れ』／[V]）",
                why = OmenWhy(),
                weight = 80
            });

        // 🧠 気性：先触れで読んだ相手に合わせて盤を組み替える、という遊びの入口。
        //   ⚠ `d_omen2`（職の内訳が読める）を持っているのに調教を知らない人にだけ出す。
        //     「読めるようになった直後」が、気性を意識する意味がいちばん立つ瞬間だから。
        if (ResearchState.IsResearched("d_omen2") && !ResearchState.IsResearched("m_temper1"))
            list.Add(new Advice
            {
                title = "『見極め』を研究する（魔物研究）",
                why = "相手の職まで読めるようになりました。配下の<b>気性</b>を選べれば、術者が多い波には『狡猾』、重装には『鈍重』と、盤の組み方で応えられます。",
                weight = 81
            });

        // ⛏️ 掘削：**掘ってから塞ぐ**が分からないと「塞げない」しか出ない道具になる。最初の1回だけ教える。
        if (Excavation.Unlocked && EurekaTracker.Count("excavate") == 0)
            list.Add(new Advice
            {
                title = "迷宮の形を変える（下部の『塞ぐ』『掘る』）",
                why = "1本道は塞げません（階段に届かなくなるため）。<b>先に『掘る』で迂回路を作り、それから近道を『塞ぐ』</b>——これで道のりが伸び、敵が奥へ届きにくくなります。カーソルを合わせれば結果が先に見えます。",
                weight = 79
            });

        // 🕳️ 落とし穴＝**倒す罠ではなく運ぶ罠**。使い方が他の罠と違うので、最初の1回だけ強く押す。
        if (ResearchState.IsResearched("d_trap_pit") && EurekaTracker.Count("pit") == 0)
            list.Add(new Advice
            {
                title = "落とし穴を置いて、行き先を決める",
                why = "落とし穴は削る罠ではありません。<b>踏んだ相手を運ぶ</b>罠です。殺し部屋へ直送するか、入口へ戻して時間を奪うか——置いたあと、行き先のマスをもう一度クリックして決めます。",
                weight = 83
            });

        // 💰 DPが余っていること自体を知らせる（余っているのに気づかないのが一番もったいない）
        if (dp >= 3000)
            list.Add(new Advice
            {
                title = "余ったDPを配下そのものに注ぐ",
                why = $"DPが {dp} 余っています。配置枠が埋まっていても、<b>鍛造・進化・装飾品・召喚の儀</b>は個体に直接効きます（すべて『図鑑』から）。",
                weight = 76
            });

        AddDeclaredPathAdvice(list);   // 📜 宣言した道（K-6 A-4）

        list.Sort((x, y) => y.weight.CompareTo(x.weight));
        // 📜 **宣言した道は席を1つ増やして必ず出す**（K-6 A-4）。
        //   ⚠ 既存の3件を押しのけない ―― 宣言は「他を捨てる」ことではないので、枠ごと増やす。
        //     宣言していない人の画面は今までどおり3件のまま。
        int pathAt = -1;
        for (int i = 0; i < list.Count; i++) if (list[i].path) { pathAt = i; break; }
        if (pathAt >= 0) { var q = list[pathAt]; list.RemoveAt(pathAt); b.advices.Add(q); }

        // 🌱 **3件のうち1件は「盤を大きくする」に必ず割く。**
        //   ⚠ 重みで competing させると 76〜99 の渋滞に負ける（実測：召喚 weight 55 は14ターン一度も出なかった）。
        //   ⚠ 順番は重みどおり ―― 予約するのは**枠**であって、順位ではない。
        int growAt = -1;
        for (int i = 0; i < list.Count; i++) if (list[i].grow) { growAt = i; break; }
        if (growAt >= 0)
        {
            var g = list[growAt];
            list.RemoveAt(growAt);
            b.advices.Add(g);
        }
        int cap = pathAt >= 0 ? 4 : 3;   // 📜 宣言していれば1席ぶん広い
        for (int i = 0; i < list.Count && b.advices.Count < cap; i++) b.advices.Add(list[i]);
        b.advices.Sort((x, y) => y.weight.CompareTo(x.weight));

        // ---- ③ 初出のシステム説明（一度きり）----
        if (turn <= 1) Teach(b, "basic",
            "『準備』で罠や配下を置き、『侵略開始』で冒険者の波を迎えます。倒す・怖がらせる・宝箱を漁らせる、どれもDPと感情になります。最下層の魔王が討たれたら敗北です。");
        if (ResearchState.RP >= 3) Teach(b, "research",
            "研究点(RP)は毎ターン貯まります。上部の『研究』から、罠の種類・部隊枠・地上の施設などを解禁できます。条件を満たすと『天啓』が付いて4割引になります。");
        if (nameable >= 0) Teach(b, "kin",
            "Lv10以上・進化Ⅰ以上の個体には<b>真名</b>を与えられます。眷属になった個体は配下を率いて地上へ出られますが、そのあいだ迷宮の防衛には使えません。");
        if (KinRoster.Count > 0) Teach(b, "surface",
            "1ターンは<b>前半＝迷宮／後半＝地上</b>に分かれています。防衛戦が終わると自動で世界地図に出るので、そこで眷属を動かします。"
          + "敵領は<b>隣接してから</b>しか攻められないので、遠いときは<b>まず前線の自領まで移動</b>し、次のターンに攻めます。"
          + "産出は「人口が耕せるタイル」から出るので、<b>版図を広げるより先に拠点の人口</b>が要ります。");
        if (DungeonFloorManager.Instance != null && DungeonFloorManager.Instance.BuiltFloorCount >= 2) Teach(b, "floors",
            "階層が深いほど魔素が濃く、そこで戦った配下は速く育ちます。冒険者は自分の格に合う深さまでしか降りてこないので、<b>強い個体ほど下に置く</b>のが基本です。");
        if (mat >= 30) Teach(b, "material",
            "素材は装備の鍛造と『実戦の反芻』に使います。反芻は<b>冒険者が到達しなかった階層</b>に置いた個体だけが使える、取り残しを埋める手段です。");
        if (AttributeSystem.TotalPoints > 0) Teach(b, "attr",
            "偉業は6つの軸（軍事・拡張・経済・科学・文化・外交）に分かれていて、達成すると<b>その軸の属性ポイント</b>が入ります（小1点／大2点）。『属性』から4段のツリーを伸ばせます。<b>点は軸ごとに別</b>なので、通った道のぶんだけ強くなります。時代をまたいでも残ります。");
        Teach(b, "policy",
            "地上メニューの『政策』で<b>政体</b>を選び、<b>政策カード</b>をスロットに差せます。スロットには色（■戦■富■秘■民）があり、同じ色のカードしか差せません。差し替えは準備フェーズなら無料、時代が進むと新しいカードが増え、古いカードは効果が半分になります。");
        if (MutationSystem.ActiveCount > 0) Teach(b, "mutation",
            "第" + MutationSystem.FirstTurn + "ターンから<b>世界の変異</b>が始まりました。これは「敵が強くなる」のではなく、"
            + "<b>いま組んでいる盤を効きにくくする条件</b>が積み上がっていく仕組みです（例：物理の守りが濃いなら術者を混ぜる）。"
            + "上部の『変異』にホバーすると、出ている変異と対策が読めます。"
            + "効きは <b>量÷(1+抑制)</b> で、抑制は領域研究『順応』『異相の解剖』『変異抑制（反復可）』で買えます。"
            + "⚠ 抑制をいくら積んでも0にはならないので、<b>編成を組み替える</b>のが本命の対策です。");
        if (turn >= 8) Teach(b, "victory",
            "勝利は4本のスコア（征服・信仰・技術・経済）で競います。地上メニューの『勝利』で、人間側と他の魔王の伸びも見られます。");

        lastOwned = owned; lastExpectedLv = expLv; lastEra = era; lastMutCount = MutationSystem.ActiveCount;
        return b;
    }

    private static void Teach(Brief b, string key, string text)
    {
        EnsureInit();
        if (taught.Contains(key)) return;
        taught.Add(key);
        b.lessons.Add(text);
    }

    // ============ 盤面から事実を拾う ============
    private static string FirstAffordableResearch()
    {
        foreach (var n in ResearchCatalog.All)
            if (ResearchState.CanResearch(n.id)) return n.jpName;
        return null;
    }

    private static int FirstNameableIndividual()
    {
        string why;
        foreach (var v in MinionRoster.All)
            if (KinRoster.CanName(v.id, out why)) return v.id;
        return -1;
    }

    /// <summary>🏺 遺物をスロットに1つでも挿しているか（手に入れただけでは効かないため）。</summary>
    /// <summary>
    /// 🔭 備えを勧める理由を、**いま読めている名簿から**書く（→ [[WaveRoster]]）。
    /// ⚠ 読めていないことまで語らない。読みの深さが浅いときは浅いなりの言い方をする。
    /// </summary>
    private static string OmenWhy()
    {
        int turn = DungeonTurnManager.Instance != null ? DungeonTurnManager.Instance.CurrentTurn : 1;
        WaveRoster.EnsureRolled(turn);
        if (WaveRoster.ScoutLevel >= 2)
        {
            var c = WaveRoster.JobCounts();
            int n = Mathf.Max(1, WaveRoster.Count);
            if (c[(int)AdventurerAI.Job.Cleric] * 4 >= n) return $"次は {n} 体、うち聖職者が {c[(int)AdventurerAI.Job.Cleric]}。削っても戻されます。『静謐の霧』が効きます。";
            if (c[(int)AdventurerAI.Job.Mage] * 4 >= n) return $"次は {n} 体、うち術者が {c[(int)AdventurerAI.Job.Mage]}。遠間から焼かれます。『魔封じの結界』が効きます。";
            if (c[(int)AdventurerAI.Job.Warrior] * 3 >= n) return $"次は {n} 体、重装が {c[(int)AdventurerAI.Job.Warrior]}。『軋む床』で足を止めれば罠が乗ります。";
            if (c[(int)AdventurerAI.Job.Thief] * 4 >= n) return $"次は {n} 体、盗人が {c[(int)AdventurerAI.Job.Thief]}。『見張りの目』で持ち逃げを止められます。";
            return $"次は {n} 体。偏りはありません。数が多いなら『狭き門』で捌く余裕を作れます。";
        }
        return "備えは毎ターン剥がれます。張らないターンは、そのぶん素で受けることになります。";
    }

    private static bool AnyRelicEquipped()
    {
        var rm = RelicManager.Instance; if (rm == null) return true;
        for (int i = 0; i < rm.SlotCount; i++) if (rm.SlotAt(i) >= 0) return true;
        return false;
    }

    private static int IdleKinCount()
    {
        int n = 0;
        foreach (var k in KinRoster.All)
            if (k.injuryTurns <= 0 && k.marchTarget < 0) n++;
        return n;
    }

    /// <summary>🏛️ 空いている政策スロットの数。</summary>
    private static int EmptyPolicySlots()
    {
        int n = 0;
        for (int i = 0; i < PolicySystem.SlotCount; i++) if (PolicySystem.SlottedAt(i) < 0) n++;
        return n;
    }

    /// <summary>自領のどこかに、まだ空きのある訓練所があるか。</summary>
    private static bool AnyFreeTrainingSlot()
    {
        if (KinRoster.Count == 0) return false;
        int n = SurfaceMap.Count;
        for (int i = 0; i < n; i++)
        {
            var r = SurfaceMap.Get(i);
            if (!r.owned) continue;
            if (TrainingSystem.HasCamp(r.id) && TrainingSystem.CountAt(r.id) < TrainingSystem.PerCamp) return true;
        }
        return false;
    }

    private static bool CanFoundSomewhere()
    {
        string why;
        foreach (var k in KinRoster.All)
            if (k.injuryTurns <= 0 && SettlementSystem.CanFound(k.regionId, out why)) return true;
        return false;
    }

    /// <summary>置いてある配下が**全員**鍛造の上限に達しているか（→ [[EquipmentCatalog]]）。</summary>
    private static bool AllPlacedAtForgeCap()
    {
        var fm2 = DungeonFeatureManager.Instance;
        var dl2 = DemonLord.Instance;
        if (fm2 == null) return false;
        int cap = EquipmentCatalog.ResearchGradeCap() + (dl2 != null ? dl2.ForgeGradeBonus : 0);
        cap = Mathf.Min(EquipmentCatalog.MaxGrade, cap);
        int placed = 0;
        foreach (var v in MinionRoster.All)
        {
            if (!fm2.IsIndividualPlaced(v.id)) continue;
            placed++;
            if (v.weaponGrade < cap || v.armorGrade < cap) return false;
        }
        return placed > 0;
    }

    /// <summary>魔王のランクアップ費用（`DemonLord` の表と同じ）。⚠ 表を変えたら両方直す。</summary>
    private static int RankUpCostOf(int rank)
    {
        int[] c = { 2, 5, 10, 18, 30 };
        return c[Mathf.Clamp(rank, 0, 4)];
    }
}
