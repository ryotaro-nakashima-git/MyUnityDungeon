using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🔦 発見（Civ VII の探索地イベント：ケルン・焚き火・沈没船…）。S4。
///
/// **未踏の地に足を踏み入れた瞬間**に、たまに何かを見つける。見つけたら**選択肢**が出て、
/// どちらを取るかで報酬が変わる。Civ VII と同じく報酬は**小刻み**（DP+120／素材+8／研究点+6 くらい）。
///
/// 設計の芯：**地上を歩く理由**を作ること。
/// U1で手動移動を入れ、S4で地形の重みを入れたので、「どこを通るか」に意味が要る。
/// 発見はその報酬側で、**通ったことのないタイルにしか湧かない**（同じ所を往復しても出ない）。
///
/// 純static・実行時保持。関連: [[surface-units-u1]] [[civ7-gap-plan]] [[NarrativeSystem]]。
/// </summary>
public static class DiscoverySystem
{
    public struct Choice { public string label, result; public int dp, mat, rp, emo, fame; public int vision; }
    public struct Def
    {
        public string id, title, story;
        public Choice a, b;
    }

    private static readonly Def[] defs =
    {
        D("cairn", "石塚", "誰が積んだとも知れない石が、道の脇にきちんと積まれている。\n風はここだけ、少し静かだ。",
          C("崩して素材にする", "石の下から古い鉄器が出た", 0, 10, 0, 0, 0, 0),
          C("そのままにする", "配下たちが手を合わせた。妙に士気が上がっている", 0, 0, 4, 12, 0, 0)),
        D("fire", "消えかけの焚き火", "まだ温い。誰かが、つい先ほどまでここにいた。",
          C("追跡する", "隊商の荷を置き去りに逃げていった", 140, 0, 0, 0, 0, 0),
          C("火を囲んで休む", "見張りが遠くの地形を覚えて帰ってきた", 0, 0, 0, 0, 0, 3)),
        D("ruin", "崩れた祠", "地上の神を祀っていたらしい。屋根はもう無い。",
          C("石材を剥がす", "使える石を持ち帰った", 90, 6, 0, 0, 0, 0),
          C("中を検める", "読めない碑文を写し取った", 0, 0, 8, 0, 0, 0)),
        D("bones", "獣の骨", "大型の何かが、ここで死んでいる。骨は妙に新しい。",
          C("牙と爪を剥ぐ", "良い素材が採れた", 0, 12, 0, 0, 0, 0),
          C("何が殺したのか調べる", "この一帯の危うさが分かった", 0, 0, 5, 0, 0, 2)),
        D("well", "涸れた井戸", "底に何かが落ちている。縄はもう無い。",
          C("配下を降ろす", "袋いっぱいの古銭を引き上げた", 180, 0, 0, 0, 0, 0),
          C("埋め戻す", "近隣の噂が少しだけ静まった", 0, 0, 0, 0, -12, 0)),
        D("camp", "打ち捨てられた野営地", "冒険者のものだ。装備が散らばっている。",
          C("装備を回収する", "鍛え直せる金属が手に入った", 60, 9, 0, 0, 0, 0),
          C("罠を仕掛けて待つ", "戻ってきた一団が悲鳴を上げて逃げた", 0, 0, 0, 20, 8, 0)),
        D("stone", "苔むした道標", "文字は読めないが、矢印だけは分かる。",
          C("矢印の先を見に行く", "丘の上から遠くまで見渡せた", 0, 0, 0, 0, 0, 4),
          C("道標を自分の紋に彫り直す", "この地の者が魔王の名を口にし始めた", 0, 0, 0, 0, 14, 0)),
        D("spring", "澱んだ泉", "水面が、ときおり自分から波立つ。",
          C("汲んで持ち帰る", "魔素を含んだ水だった", 0, 0, 10, 0, 0, 0),
          C("配下に飲ませる", "何体かが妙に元気になった", 0, 0, 0, 18, 0, 0)),
    };
    private static Def D(string id, string t, string st, Choice a, Choice b)
        => new Def { id = id, title = t, story = st, a = a, b = b };
    private static Choice C(string label, string result, int dp, int mat, int rp, int emo, int fame, int vision)
        => new Choice { label = label, result = result, dp = dp, mat = mat, rp = rp, emo = emo, fame = fame, vision = vision };

    public static int Count { get { return defs.Length; } }
    public static Def Get(int i) { return defs[Mathf.Clamp(i, 0, defs.Length - 1)]; }

    /// <summary>踏破するたびに出ては煩いので、この確率でだけ湧く。</summary>
    public const float Chance = 0.30f;

    private static HashSet<int> visited;      // 一度でも踏んだタイル（同じ所では二度と湧かない）
    private static void EnsureInit() { if (visited == null) visited = new HashSet<int>(); }
    public static void Reset() { visited = null; Pending = -1; PendingRegion = -1; EnsureInit(); }

    /// <summary>未読の発見（-1＝なし）。UIがこれを見てモーダルを開く。</summary>
    public static int Pending = -1;
    public static int PendingRegion = -1;
    public static string LastResult = "";

    /// <summary>ユニットがタイルに入ったときに呼ぶ。初めての土地なら、たまに何かを見つける。</summary>
    public static void OnEnter(int regionId)
    {
        EnsureInit();
        if (regionId < 0 || visited.Contains(regionId)) return;
        visited.Add(regionId);
        if (Pending >= 0) return;                       // 未読が残っているうちは重ねない
        var r = SurfaceMap.Get(regionId);
        if (r.isOcean || r.owner != SurfaceMap.OwnerNeutral) return;   // 自領・敵領・海では起きない（誰かの土地）
        if (Random.value > Chance) return;
        Pending = Random.Range(0, defs.Length);
        PendingRegion = regionId;
        Debug.Log($"🔦『発見』{r.name} で『{Get(Pending).title}』を見つけた");
    }

    /// <summary>選択（0=A / 1=B）。報酬を配って未読を消す。</summary>
    public static bool Choose(int which)
    {
        if (Pending < 0) return false;
        var d = Get(Pending);
        var c = which == 0 ? d.a : d.b;
        var res = DungeonResourceManager.Instance;
        if (res != null)
        {
            if (c.dp != 0) res.AddDP(c.dp);
            if (c.mat != 0) res.AddMaterial(c.mat);
            if (c.fame != 0) res.AddFame(c.fame);
        }
        if (c.rp != 0) ResearchState.AddRP(c.rp);
        var et = EmotionTreeManager.Instance;
        if (et != null && c.emo != 0)
            for (int i = 0; i < 4; i++) et.AddEmotion((EmotionTreeManager.Route)i, Mathf.Max(1, c.emo / 4));
        if (c.vision > 0 && PendingRegion >= 0) SurfaceMap.MarkSeen(PendingRegion, c.vision);   // 👁️ 遠くが見える

        LastResult = c.result;
        Debug.Log($"🔦『{d.title}』{c.label} → {c.result}"
            + Reward(c));
        Pending = -1; PendingRegion = -1;
        return true;
    }

    /// <summary>報酬の一行（UIにも使う）。</summary>
    public static string Reward(Choice c)
    {
        string s = "";
        if (c.dp != 0) s += "　DP" + (c.dp > 0 ? "+" : "") + c.dp;
        if (c.mat != 0) s += "　素材" + (c.mat > 0 ? "+" : "") + c.mat;
        if (c.rp != 0) s += "　研究点" + (c.rp > 0 ? "+" : "") + c.rp;
        if (c.emo != 0) s += "　感情" + (c.emo > 0 ? "+" : "") + c.emo;
        if (c.fame != 0) s += "　名声" + (c.fame > 0 ? "+" : "") + c.fame;
        if (c.vision > 0) s += "　周囲" + c.vision + "タイルが見える";
        return s;
    }
}
