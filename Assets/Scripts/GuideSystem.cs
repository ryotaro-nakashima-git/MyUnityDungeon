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
        public readonly List<Advice> advices = new List<Advice>();
        public readonly List<string> lessons = new List<string>();
    }

    /// <summary>プレイヤーが「今後は出さない」を選んだか。</summary>
    public static bool Enabled = true;
    /// <summary>まだ開いていない報告があるか。</summary>
    public static bool Unread;
    public static Brief Latest;

    private static HashSet<string> taught;
    private static int lastOwned = -1, lastExpectedLv = -1, lastEra = -1;

    private static void EnsureInit() { if (taught == null) taught = new HashSet<string>(); }

    public static void Reset()
    {
        taught = new HashSet<string>(); Latest = null; Unread = false;
        lastOwned = -1; lastExpectedLv = -1; lastEra = -1;
    }

    /// <summary>準備フェーズに入った瞬間に呼ぶ（DungeonTurnManager／開始時）。</summary>
    public static void OnTurnStart(int turn)
    {
        EnsureInit();
        Latest = Build(turn);
        if (Enabled) Unread = true;
    }

    // ============ 組み立て ============
    private static Brief Build(int turn)
    {
        var b = new Brief { turn = turn };
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
                title = "眷属を進軍させる（地上）",
                why = idle + "体が待機したままです。地上の領域は毎ターンDP・素材・研究点を生みます。",
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
            "上部の『地上』で世界地図に出られます。タイルを選んで進軍させ、勝てばその領域が自領になります。自領は毎ターン産出し、拠点を築くと周囲まで版図が広がります。");
        if (DungeonFloorManager.Instance != null && DungeonFloorManager.Instance.BuiltFloorCount >= 2) Teach(b, "floors",
            "階層が深いほど魔素が濃く、そこで戦った配下は速く育ちます。冒険者は自分の格に合う深さまでしか降りてこないので、<b>強い個体ほど下に置く</b>のが基本です。");
        if (mat >= 30) Teach(b, "material",
            "素材は装備の鍛造と『実戦の反芻』に使います。反芻は<b>冒険者が到達しなかった階層</b>に置いた個体だけが使える、取り残しを埋める手段です。");
        if (AttributeSystem.TotalPoints > 0) Teach(b, "attr",
            "偉業は6つの軸（軍事・拡張・経済・科学・文化・外交）に分かれていて、達成すると<b>その軸の属性ポイント</b>が入ります（小1点／大2点）。『属性』から4段のツリーを伸ばせます。<b>点は軸ごとに別</b>なので、通った道のぶんだけ強くなります。時代をまたいでも残ります。");
        Teach(b, "policy",
            "地上メニューの『政策』で<b>政体</b>を選び、<b>政策カード</b>をスロットに差せます。スロットには色（■戦■富■秘■民）があり、同じ色のカードしか差せません。差し替えは準備フェーズなら無料、時代が進むと新しいカードが増え、古いカードは効果が半分になります。");
        if (turn >= 8) Teach(b, "victory",
            "勝利は4本のスコア（征服・信仰・技術・経済）で競います。地上メニューの『勝利』で、人間側と他の魔王の伸びも見られます。");

        lastOwned = owned; lastExpectedLv = expLv; lastEra = era;
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
