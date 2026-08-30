using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🔔💥 **誘引と過負荷**（①の1本目）。戦闘中に**盤の罠をクリックする**という手を作る。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：この迷宮は「置いたら終わり」だった。罠も配下も、置いたあとは
///   勝手に働くだけで、戦闘中にプレイヤーがすることが**ほぼ何も無い**（実測：号令は
///   DP300〜500・CD35〜70秒で、20〜60秒の波では1回撃てるかどうか）。
///   さらに `RoomData.IsTargetable()` は**罠を目的地から外している**ので、
///   丹精込めて作った殺戮部屋を冒険者が素通りする ―― これが一番の徒労だった。
/// </para>
///
/// <para>
/// 2段の手にしてある。
/// <list type="bullet">
/// <item><b>誘引（おとり）</b>：罠を鳴らす。範囲内の冒険者が数秒だけそこへ向かう。
///   ⚠ 踏破目的の直行も**上書きする**（魔王への一直線から引き剥がせる＝時間を買える）。</item>
/// <item><b>過負荷</b>：その罠をいま爆発させる。範囲内の全員に罠のダメージ×3と状態異常。
///   ⚠ 撃った罠は**その波のあいだ黙る**（＝残りの発動回数と引き換えの一撃）。</item>
/// </list>
/// 「引きつけて…今だ！」の**待つ時間**がここで生まれる。
/// </para>
///
/// <para>
/// ⚠⚠ **掛け算の軸は増やしていない**（→ [[difficulty-curve-orders]]）。
///   過負荷は「罠が波の残りで刻むはずだった単発を、1回の面に束ね直す」だけ。
///   波あたりの回数を固定（誘引2・過負荷2）にしてあるので、盤を広げても増えない。
/// ⚠ **DPを取らない。** 通しプレイで2周とも資源を余らせて負けた＝DPは制限にならない
///   （→ [[playthrough-wall-t11]]）。制限は**波あたりの回数**で掛ける。
/// ⚠ 置くのは準備フェーズのまま。ここで足しているのは**既に置いた物の使い方**だけで、
///   「戦闘中に置ける」（C-2）は採らないという判断は変えていない。
/// </para>
///
/// 関連: [[AdventurerAI]]（目的地の上書き） [[TrapCatalog]] [[GridInputHandler]]（クリックの入口）。
/// </summary>
public static class Decoy
{
    // ── ノブ ──
    /// <summary>おとりが効いている秒数。⚠ 息継ぎ（5〜9秒）より短くする＝次の塊が来る前に決着させる。</summary>
    public const float Duration = 5.5f;
    /// <summary>誘い出す範囲（マンハッタン距離・マス）。</summary>
    public const int LureRadius = 8;
    public const int LurePerWave = 2;
    public const int OverloadPerWave = 2;
    /// <summary>連打止め。⚠ 誘引と過負荷で**共有**する（同じ手の2段なので別枠にすると連打できてしまう）。</summary>
    public const float Cooldown = 4f;
    public const float OverloadMult = 3f;
    /// <summary>過負荷の巻き込み半径（マス・ユークリッド）。</summary>
    public const float OverloadRadius = 2.4f;

    // ── 状態（波ごと）──
    private static int lureLeft, overloadLeft;
    private static float cd;
    private static Vector2Int cell;
    private static int floorIdx = -1;
    private static float life;
    /// <summary>この波で撃ち尽くした罠（floor,x,y）。⚠ 波が変われば戻る。</summary>
    private static readonly HashSet<Vector3Int> spent = new HashSet<Vector3Int>();
    private static int lureMoved, overloadHit;   // 📜 決算に出す累計（→ [[WaveReport]]）

    public static bool Active { get { return life > 0f; } }
    public static Vector2Int Cell { get { return cell; } }
    public static int FloorIndex { get { return floorIdx; } }
    public static float Life { get { return life; } }
    public static int LureLeft { get { return lureLeft; } }
    public static int OverloadLeft { get { return overloadLeft; } }
    public static float CooldownLeft { get { return Mathf.Max(0f, cd); } }
    public static bool IsSpent(int floor, Vector2Int c) { return spent.Contains(new Vector3Int(c.x, c.y, floor)); }

    /// <summary>🔄 波の頭で戻す（`StartBattlePhase` から）。</summary>
    public static void BeginWave()
    {
        lureLeft = LurePerWave; overloadLeft = OverloadPerWave;
        cd = 0f; life = 0f; floorIdx = -1; spent.Clear();
        lureMoved = overloadHit = 0;
        View.Refresh();
    }

    /// <summary>
    /// 🔚 波の終わり（`EndBattlePhase` から）。
    /// ⚠⚠ **これが無いと盤の印が消えない。** 描き直しは `Tick` の中でしか起きず、
    ///   `Tick` は戦闘中しか回らないので、最後に描いた菱形と輪が**準備フェーズに残り続ける**。
    /// </summary>
    public static void EndWave()
    {
        life = 0f; floorIdx = -1;
        View.HideAll();
    }

    /// <summary>🔄 周をまたがない。</summary>
    public static void Reset() { BeginWave(); EndWave(); }

    /// <summary>⏱️ 戦闘中だけ進む。⚠ `Time.deltaTime`（倍速に乗る＝表示の秒と実際が一致する）。</summary>
    public static void Tick(float dt)
    {
        if (cd > 0f) cd -= dt;
        if (life > 0f)
        {
            life -= dt;
            if (life <= 0f) { life = 0f; floorIdx = -1; }
        }
        View.Tick(dt);
    }

    // ============ 冒険者からの問い合わせ ============
    /// <summary>
    /// 🔔 おとりが自分に効いているか。効いていれば向かう先を返す。
    /// ⚠ 退却中の相手には効かせない（帰る者を引き戻せると、逃走そのものが無意味になる）。
    /// </summary>
    public static bool LureTarget(int floor, Vector2Int from, out Vector2Int target)
    {
        target = cell;
        if (life <= 0f || floor != floorIdx) return false;
        return Mathf.Abs(from.x - cell.x) + Mathf.Abs(from.y - cell.y) <= LureRadius;
    }

    // ============ 押す ============
    /// <summary>
    /// 🖱️ 戦闘中に盤を左クリックしたときの唯一の入口。
    /// おとりが**その罠に**点いていれば過負荷、そうでなければ誘引。
    /// </summary>
    public static bool Click(Vector2Int c, int floor, bool overloadDirect, out string why)
    {
        why = "";
        var fm = DungeonFeatureManager.Instance;
        if (fm == null) return false;
        // ⚠ 罠でないマスは**黙って無視する**。ここで理由を返すと、戦闘中に盤のどこを押しても
        //   赤い通知が出て、画面が「押すな」と言い続けることになる。
        if (!IsTrapAt(floor, c)) return false;
        bool onDecoy = life > 0f && floor == floorIdx && c == cell;
        if (overloadDirect || onDecoy) return TryOverload(c, floor, out why);
        return TryLure(c, floor, out why);
    }

    public static bool TryLure(Vector2Int c, int floor, out string why)
    {
        why = "";
        if (cd > 0f) { why = "まだ間が空かない（あと " + cd.ToString("0.0") + "）"; return false; }
        if (lureLeft <= 0) { why = "この波の誘引はもう無い"; return false; }
        if (!IsTrapAt(floor, c)) { why = "罠のマスを押してください"; return false; }

        lureLeft--; cd = Cooldown;
        cell = c; floorIdx = floor; life = Duration;

        int moved = 0;
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsInactive.Exclude))
        {
            if (a == null || a.IsRetreating || a.MyFloor != floor) continue;
            if (Mathf.Abs(a.CurrentGridPos.x - c.x) + Mathf.Abs(a.CurrentGridPos.y - c.y) > LureRadius) continue;
            a.RetargetNow();   // ⚠ 経路を張り直させないと、次の探索まで数秒動かない
            moved++;
        }

        var g = DungeonGridSystem.Of(floor);
        Vector3 w = g != null ? g.GridToWorld(c.x, c.y) : new Vector3(c.x, c.y, 0f);
        FloatText.Spawn(w, "おとり", new Color(0.89f, 0.66f, 0.29f), 2.6f, 0.7f, 0.9f);
        SoundSystem.Play(SoundSystem.Sfx.Command, 0.7f, 1.35f);
        NotifySystem.Push("<b>おとり</b>を鳴らした ― <b>" + moved + "</b> 人が向かってくる", NotifySystem.Kind.Gain);
        lureMoved += moved;
        WaveReport.NoteChoice("誘引", (LurePerWave - lureLeft) + " 回・のべ " + lureMoved + " 人を罠へ引き寄せた");
        View.Refresh();
        return true;
    }

    public static bool TryOverload(Vector2Int c, int floor, out string why)
    {
        why = "";
        if (cd > 0f) { why = "まだ間が空かない（あと " + cd.ToString("0.0") + "）"; return false; }
        if (overloadLeft <= 0) { why = "この波の過負荷はもう無い"; return false; }
        if (!IsTrapAt(floor, c)) { why = "罠のマスを押してください"; return false; }
        if (IsSpent(floor, c)) { why = "その罠はもう焼き切れている"; return false; }

        var fm = DungeonFeatureManager.Instance;
        int kind = TrapKindAt(floor, c);
        overloadLeft--; cd = Cooldown;
        spent.Add(new Vector3Int(c.x, c.y, floor));
        // 🪤 撃った罠はこの波のあいだ黙る。⚠ **これが対価**。
        //   ここを無料にすると「毎波ぜんぶの罠を3倍で撃つ」になり、掛け算の軸が1本増える。
        var g = DungeonGridSystem.Of(floor);
        if (g != null)
        {
            var obj = g.GetGridObject(c.x, c.y);
            if (obj != null)
            {
                var rd = obj.GetComponent<RoomData>();
                if (rd != null) rd.DisableTrapTemporarily(9999f);
            }
        }

        Vector3 w = g != null ? g.GridToWorld(c.x, c.y) : new Vector3(c.x, c.y, 0f);
        var col = TrapCatalog.Get(kind).color;
        int hit = 0;
        foreach (var a in Object.FindObjectsByType<AdventurerAI>(FindObjectsInactive.Exclude))
        {
            if (a == null || a.MyFloor != floor) continue;
            float dx = a.CurrentGridPos.x - c.x, dy = a.CurrentGridPos.y - c.y;
            if (dx * dx + dy * dy > OverloadRadius * OverloadRadius) continue;
            hit++;
            a.TakeDamage(TrapCatalog.InstantDamage(kind, a.MaxHP) * OverloadMult);
            if (a != null) a.ApplyTrapStatus(kind);
            BattleVfx.Spark(a.transform.position, col);
        }

        BattleVfx.Burst(w, col, 1.5f);
        FloatText.Spawn(w, "過負荷!", col, 3.2f, 1.1f, 1.0f);
        SoundSystem.Play(SoundSystem.Sfx.Kill, 0.9f, 0.75f);
        ScreenShake.Kick(0.26f, 0.32f);
        NotifySystem.Push("<b>過負荷</b> ― " + TrapCatalog.Get(kind).name + " が <b>" + hit
            + "</b> 人を巻き込んで焼き切れた", NotifySystem.Kind.Gain);
        overloadHit += hit;
        WaveReport.NoteChoice("過負荷", (OverloadPerWave - overloadLeft) + " 回・のべ " + overloadHit
            + " 人を巻き込んだ（" + TrapCatalog.Get(kind).name + "）");
        // おとりが同じ罠に点いていたなら、役目は終わり
        if (life > 0f && floor == floorIdx && c == cell) { life = 0f; floorIdx = -1; }
        View.Refresh();
        return true;
    }

    // ============ 盤を読む ============
    public static bool IsTrapAt(int floor, Vector2Int c)
    {
        return TrapKindAt(floor, c) >= 0;
    }

    /// <summary>そのマスの罠の種類。罠でなければ -1。⚠ 落とし穴は運ぶ罠なので対象外。</summary>
    public static int TrapKindAt(int floor, Vector2Int c)
    {
        var fm = DungeonFeatureManager.Instance;
        if (fm == null) return -1;
        var recs = fm.ExportFeatures(floor);
        for (int i = 0; i < recs.Count; i++)
        {
            if (recs[i].cell != c) continue;
            if (recs[i].type != DungeonFeatureManager.FeatureType.Trap) return -1;
            if (recs[i].trapKind == (int)TrapKind.Pit) return -1;   // 🕳️ 落とし穴は爆発させない
            return recs[i].trapKind;
        }
        return -1;
    }

    /// <summary>下部の帯に出す1行（罠に乗せたときだけ）。→ [[GameUIManager.Hud]]</summary>
    public static string HoverLine(int floor, Vector2Int c)
    {
        int kind = TrapKindAt(floor, c);
        if (kind < 0) return "";
        var d = TrapCatalog.Get(kind);
        if (IsSpent(floor, c)) return "🪤 " + d.name + " ― <color=#6f6889>焼き切れている（この波はもう撃てない）</color>";
        bool onDecoy = life > 0f && floor == floorIdx && c == cell;
        if (onDecoy)
            return "💥 " + d.name + " ― <b>クリックで過負荷</b>（範囲に ×" + OverloadMult.ToString("0")
                 + "／この罠は焼き切れる）　<color=#9c95b4>残り " + overloadLeft + "</color>";
        return "🔔 " + d.name + " ― <b>クリックで誘引</b>（" + LureRadius + "マス以内が " + Duration.ToString("0.0")
             + "秒ここへ向かう）　<color=#9c95b4>残り " + lureLeft + "</color>"
             + "　<color=#6f6889>／右クリックで直に過負荷</color>";
    }

    // ============ 盤に描く ============
    /// <summary>
    /// 🔔 盤に描く3つ。⚠ **押せる物が見えないと、無いのと同じ**（②で学んだこと）。
    /// <list type="bullet">
    /// <item>使える罠の印 ― いま押せる罠すべてに小さな菱形</item>
    /// <item>おとりの輪 ― 作動中の罠で脈打つ</item>
    /// <item>巻き込み範囲 ― 過負荷がどこまで届くか（見えないと待てない）</item>
    /// </list>
    /// ⚠ UIのCanvasではなくワールドのスプライトで描く（`TotemRangeView` と同じ作り）。
    /// ⚠ 盤はキャッシュしない（縦の迷宮では表示中の階が変わる）。
    /// </summary>
    public class View : MonoBehaviour
    {
        private static View inst;
        private static readonly List<SpriteRenderer> area = new List<SpriteRenderer>();
        private static readonly List<SpriteRenderer> pips = new List<SpriteRenderer>();
        private static SpriteRenderer core;
        private static float pulse;
        private static string sig = "";

        private static void Ensure()
        {
            if (inst != null) return;
            var go = new GameObject("DecoyView");
            inst = go.AddComponent<View>();
        }

        /// <summary>状態が変わったので描き直す。</summary>
        public static void Refresh() { sig = ""; Ensure(); }

        /// <summary>盤から全部消す（波が終わったとき）。</summary>
        public static void HideAll()
        {
            sig = "";
            if (core != null) core.gameObject.SetActive(false);
            Hide(area, 0); Hide(pips, 0);
        }

        public static void Tick(float dt)
        {
            Ensure();
            if (inst == null) return;
            pulse += dt;

            var turn = DungeonTurnManager.Instance;
            bool battle = turn != null && turn.IsBattlePhase;
            var g = DungeonGridSystem.Active;
            int shownFloor = g != null ? g.FloorIndex : -1;
            string now = battle + "/" + shownFloor + "/" + spent.Count + "/" + lureLeft + "/" + overloadLeft
                       + "/" + (Active ? cell.ToString() + floorIdx : "-");
            if (now != sig) { sig = now; inst.Rebuild(battle, g, shownFloor); }

            // 🫀 おとりの輪だけ脈打たせる
            if (core != null && core.gameObject.activeSelf)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(pulse * 7f);
                core.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.5f, k);
                var c = core.color; c.a = Mathf.Lerp(0.35f, 0.9f, k); core.color = c;
            }
        }

        private void Rebuild(bool battle, DungeonGridSystem g, int shownFloor)
        {
            if (core == null)
            {
                var go = new GameObject("DecoyCore");
                go.transform.SetParent(transform, false);
                core = go.AddComponent<SpriteRenderer>();
                core.sprite = MarkerArt.HexRing();
                core.sortingOrder = 43;
            }

            bool ringOn = battle && Active && g != null && shownFloor == floorIdx;
            core.gameObject.SetActive(ringOn);
            if (ringOn)
            {
                core.transform.position = g.GridToWorld(cell.x, cell.y);
                core.color = new Color(0.89f, 0.66f, 0.29f, 0.7f);
            }

            PaintArea(ringOn ? g : null);
            PaintPips(battle ? g : null, shownFloor);
        }

        /// <summary>💥 過負荷の巻き込み範囲。どこまで届くか分からないと「引きつける」判断ができない。</summary>
        private void PaintArea(DungeonGridSystem g)
        {
            int used = 0;
            if (g != null)
            {
                int r = Mathf.CeilToInt(OverloadRadius);
                for (int x = cell.x - r; x <= cell.x + r; x++)
                    for (int y = cell.y - r; y <= cell.y + r; y++)
                    {
                        float dx = x - cell.x, dy = y - cell.y;
                        if (dx * dx + dy * dy > OverloadRadius * OverloadRadius) continue;
                        if (x < 0 || y < 0 || x >= g.CurrentPlayableSize || y >= g.CurrentPlayableSize) continue;
                        if (g.GetTileType(x, y) == DungeonGridSystem.TileType.None) continue;
                        var sr = Lend(area, "DecoyCell", 39, ref used);
                        sr.sprite = MarkerArt.Pixel();
                        sr.transform.position = g.GridToWorld(x, y);
                        sr.transform.localScale = Vector3.one;
                        sr.color = new Color(0.89f, 0.45f, 0.25f, 0.16f);
                    }
            }
            Hide(area, used);
        }

        /// <summary>🔶 いま押せる罠に印。⚠ これが無いと「戦闘中に押す物がある」ことに気づけない。</summary>
        private void PaintPips(DungeonGridSystem g, int shownFloor)
        {
            int used = 0;
            var fm = DungeonFeatureManager.Instance;
            if (g != null && fm != null && (lureLeft > 0 || overloadLeft > 0))
            {
                var recs = fm.ExportFeatures(shownFloor);
                for (int i = 0; i < recs.Count; i++)
                {
                    if (recs[i].type != DungeonFeatureManager.FeatureType.Trap) continue;
                    if (recs[i].trapKind == (int)TrapKind.Pit) continue;
                    if (IsSpent(shownFloor, recs[i].cell)) continue;
                    var sr = Lend(pips, "DecoyPip", 41, ref used);
                    sr.sprite = MarkerArt.Rhombus();
                    sr.transform.position = g.GridToWorld(recs[i].cell.x, recs[i].cell.y) + new Vector3(0f, 0.3f, 0f);
                    sr.transform.localScale = Vector3.one * 0.30f;
                    sr.color = new Color(0.89f, 0.66f, 0.29f, 0.55f);
                }
            }
            Hide(pips, used);
        }

        private SpriteRenderer Lend(List<SpriteRenderer> pool, string name, int order, ref int used)
        {
            if (used >= pool.Count)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                var s0 = go.AddComponent<SpriteRenderer>();
                s0.sortingOrder = order;
                pool.Add(s0);
            }
            var sr = pool[used++];
            sr.gameObject.SetActive(true);
            return sr;
        }

        private static void Hide(List<SpriteRenderer> pool, int from)
        {
            for (int i = from; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }
    }
}
