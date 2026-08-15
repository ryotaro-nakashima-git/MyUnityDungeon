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
        if (res0 != null) { prevDp = res0.DungeonPoints; prevMat = res0.CraftMaterials; prevFame = res0.DungeonFame; }
        prevRp = ResearchState.RP;
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

        if (dl != null && dl.BP > 0)
            list.Add(new Advice { title = "魔王のステータスにBPを振る", why = $"BPが {dl.BP} 眠っています。振らないぶんは丸ごと損です。", weight = 70 });

        if (fm != null && fm.PlacedCount < fm.PlacementCap && dp >= 200)
            list.Add(new Advice
            {
                title = "配置枠を埋める（罠・スポナー・トーテム）",
                why = $"枠が {fm.PlacementCap - fm.PlacedCount} 空いていて、DPは {dp} あります。空き枠は稼がない枠です。",
                weight = 66
            });

        string rid = FirstAffordableResearch();
        if (rid != null)
            list.Add(new Advice { title = "研究を進める（" + rid + "）", why = $"研究点が {ResearchState.RP} 貯まっています。天啓が付いているものは4割引です。", weight = 64 });

        int nameable = FirstNameableIndividual();
        if (nameable >= 0 && KinRoster.Count == 0)
            list.Add(new Advice
            {
                title = "真名を与えて眷属をつくる",
                why = "条件を満たした個体がいます。眷属がいないと地上へ一歩も出られません。",
                weight = 95
            });
        else if (nameable >= 0)
            list.Add(new Advice { title = "もう1体、眷属をつくる", why = "条件を満たした個体がいます。侵攻と防衛を同時に回せるようになります。", weight = 50 });

        int idle = IdleKinCount();
        if (idle > 0)
            list.Add(new Advice
            {
                // ⚠ この進言はターン頭に出るが、**地上へ出られるのは防衛戦のあと（後半）**。
                //   旧文は「進軍させる」とだけ言っていたので、探しても行き先が無く手が止まった。
                //   いつ・どうやるのかまで書く。
                title = "眷属を進軍させる（防衛戦のあと・地上）",
                why = idle + "体が待機したままです。敵領は<b>隣接してからでないと攻められない</b>ので、"
                    + "届かないときは<b>まず前線の自領まで移動</b>し、次のターンに攻めます。",
                weight = 72
            });

        if (KinRoster.Count > 0 && CanFoundSomewhere())
            list.Add(new Advice
            {
                title = "拠点を築いて版図を広げる",
                why = "拠点は周囲のタイルを自領に変え、人口が増えると版図がさらに広がります。",
                weight = 60
            });

        if (AttributeSystem.TotalPoints > 0)
            list.Add(new Advice
            {
                title = "属性ポイントを使う（地上メニュー『属性』）",
                why = "偉業で得た点が " + AttributeSystem.TotalPoints + " 残っています。属性は時代をまたいで残る恒久強化です。",
                weight = 74
            });

        int freeSlots = EmptyPolicySlots();
        if (freeSlots > 0)
            list.Add(new Advice
            {
                title = "政策を差す（地上メニュー『政策』）",
                why = "スロットが " + freeSlots + " 空いています。差し替えは準備フェーズなら無料です。",
                weight = 68
            });

        if (dp >= 600)
            list.Add(new Advice { title = "配下を召喚して数を増やす", why = $"DPが {dp} あります。数はそのまま各階の耐久です。", weight = 55 });

        if (mat >= 40)
            list.Add(new Advice { title = "装備を鍛える／実戦の反芻に素材を使う", why = $"素材が {mat} あります。抱えていても強くなりません。", weight = 45 });

        if (fm != null && fm.PlacedCount == 0)
            list.Add(new Advice { title = "まず罠を1つ置く", why = "何も置かないまま迎えると、冒険者は無傷でボスに届きます。", weight = 99 });

        if (hp < 0.5f)
            list.Add(new Advice { title = "最下層の守りを厚くする", why = "魔王の傷が深い。討たれた時点で終わりです。", weight = 90 });

        if (AnyFreeTrainingSlot())
            list.Add(new Advice { title = "訓練所に配下を送る", why = "空きがあります。4ターン預ければ、戦えなかった個体も追いつきます。", weight = 42 });

        // 🧬 世界の変異：抑制が置いていかれると、盤を組み替えても追いつかなくなる
        if (MutationSystem.ActiveCount >= 2 && MutationSystem.Suppress <= 0f)
            list.Add(new Advice
            {
                title = "領域研究『順応』を取る",
                why = $"世界の変異が {MutationSystem.ActiveCount} 種。抑制が 0% のままだと、変異は書いてある量そのままで効きます。",
                weight = 85
            });
        else if (MutationSystem.ActiveCount >= 5 && MutationSystem.Suppress < 1.0f)
            list.Add(new Advice
            {
                title = "抑制を積む（異相の解剖／変異抑制）",
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
            var emo = EmotionTreeManager.Instance;
            if (emo != null && emo.TotalSpent == 0 && turn >= 3)
                list.Add(new Advice
                {
                    title = "感情ツリーを開く（上部『感情』）",
                    why = "まだ1つも開いていません。感情は貯めても何も起きません。開けば配下すべてが恒久的に強くなります。",
                    weight = 86
                });

            if (EurekaTracker.Count("forge") == 0 && dp >= 400)
                list.Add(new Advice
                {
                    title = "武具を鍛える（『図鑑』→ 個体の武器・防具）",
                    why = "まだ1つも鍛えていません。1段でおよそ +22%（レベル5〜6ぶん）。DPの最も確実な使い道です。",
                    weight = 84
                });

            if (AccessoryInventory.TotalCount == 0 && dp >= 800)
                list.Add(new Advice
                {
                    title = "行商人から装飾品を買う（『図鑑』の商いの欄）",
                    why = "装飾品は1個体につき1つ、魔物スキルを丸ごと付けられます。品揃えはターンごとに変わり、買った枠は戻りません。",
                    weight = 82
                });

            if (string.IsNullOrEmpty(SummonGacha.LastResult) && dp >= SummonGacha.Cost * 2)
                list.Add(new Advice
                {
                    title = "召喚の儀を引く（『図鑑』の召喚の儀）",
                    why = $"DPが {dp} あります。ここでしか出ないユニーク個体がいて、外しても解禁済みの配下が必ず1体は付いてきます。",
                    weight = 78
                });

            var rel = RelicManager.Instance;
            if (rel != null && rel.UnlockedCount > 0 && !AnyRelicEquipped())
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

        list.Sort((x, y) => y.weight.CompareTo(x.weight));
        for (int i = 0; i < list.Count && b.advices.Count < 3; i++) b.advices.Add(list[i]);

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
}
