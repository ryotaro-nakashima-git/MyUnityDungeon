using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🗡️ **名のある冒険者（因縁）**。
///
/// <para>
/// **なぜ要るか**：毎ターン来るのが「C級戦士Lv12」という属性の束でしかなかった。
/// 誰を倒したのかも、誰に逃げられたのかも残らないので、**波と波のあいだに線が引かれない**。
/// → **取り逃がした者が名前を持って戻ってくる**ようにする。相手に顔がつき、
///   「今日の波」ではなく「あいつをどうするか」がターンをまたぐ目的になる。
/// </para>
///
/// <para>
/// ⚠⚠ **名は「取り逃がした」ときにしか生まれない。** 逃げた全員に名を付けると
/// ただの騒音になり、名前の価値が消える。条件は<b>半分以上削られてなお生還した者</b>
/// （＝こちらが仕留めそこねた者）と、<b>奈落から這い上がった者</b>だけ。
/// </para>
///
/// <para>
/// ⚠ 強化には**上限**を置く（`GrowthCap`）。因縁は難易度カーブの外側にある掛け算の軸なので、
///   無制限に伸ばすと [[difficulty-curve-orders]] の「掛け算の軸を増やすな」に抵触する。
///   伸びるのは <b>プレイヤーが逃がしたぶんだけ</b>＝自分で作った難易度である、という形にしてある。
/// </para>
///
/// 関連: [[WaveRoster]]（名簿に混ぜる） [[Prison]]（捕らえる／解き放つ） [[AdventurerAI]]。
/// </summary>
public static class Nemesis
{
    /// <summary>名のある冒険者1人。⚠ セーブに丸ごと載る（`readonly` にしないこと → [[SaveSystem]]）。</summary>
    public class Hero
    {
        public int id;
        public int nameSeed;                 // 名前はここから決定的に作る（文字列も保存するが、復元の保険）
        public string name = "";
        public AdventurerAI.Job job;
        public int rank;                     // 素のランク（0..7）。因縁で最大+2される
        public int level;                    // 最後に見たときのLv。再来時は世界水準と比べて高いほうを使う
        public bool hasSpell;
        public MagicCatalog.Spell spell;

        public int escapes;                  // 逃げ切った回数（＝こちらの取りこぼし）
        public int grudge;                   // 恨みの深さ。解放・尋問・同胞を殺されると増える
        /// <summary>
        /// 🎁 **こいつが持ち逃げした戦利品の合計**（G-2）。
        /// ⚠ 逃げ切った時点で `LureEconomy` の装備水準には**もう乗っている**。
        ///   ここに覚えておくのは「討ち取れば**世界から回収できる**」ようにするため。
        ///   ＝ 装備水準を**下げる唯一の道**（→ [[LureEconomy]]）。
        /// </summary>
        public float hoard;
        public int captures;                 // 捕虜になった回数
        public int releases;                 // 解放された回数
        public bool fromAbyss;               // 奈落から這い上がった経験がある

        /// <summary>0=野に在り／1=出撃中／2=捕虜／3=討伐された／4=こちらに転向した</summary>
        public int state;
        public int bornTurn;
        public int lastSeenTurn = -99;
    }

    public const int State_AtLarge = 0, State_Deployed = 1, State_Captive = 2, State_Slain = 3, State_Turned = 4;

    /// <summary>⚠ 強化はここで頭打ちにする。逃がし続けても青天井にはしない。</summary>
    private const int GrowthCap = 6;
    private const int GrudgeCap = 8;
    /// <summary>同時に野に在る名のある者の上限。これを超えると新しい名は生まれない（騒音防止）。</summary>
    private const int AtLargeCap = 8;
    /// <summary>一度出たら何ターン空けるか。毎ターン同じ顔だと因縁が日常になる。</summary>
    private const int Cooldown = 2;

    // ⚠ readonly にしない（セーブの対象＝状態）
    private static List<Hero> all = new List<Hero>();
    private static int nextId = 1;

    public static IReadOnlyList<Hero> All { get { return all; } }
    public static int Count { get { return all.Count; } }

    public static void Reset() { all = new List<Hero>(); nextId = 1; }

    public static Hero Get(int id)
    {
        if (id <= 0) return null;
        for (int i = 0; i < all.Count; i++) if (all[i].id == id) return all[i];
        return null;
    }

    public static int AtLargeCount
    {
        get { int n = 0; for (int i = 0; i < all.Count; i++) if (all[i].state == State_AtLarge) n++; return n; }
    }
    public static int SlainCount
    {
        get { int n = 0; for (int i = 0; i < all.Count; i++) if (all[i].state == State_Slain) n++; return n; }
    }

    // ============ 🏷️ 名前と二つ名 ============

    // ⚠ 絵文字を使わない（□になる → [[ui-conventions]]）。
    private static readonly string[] Given =
    {
        "アルド", "ベネット", "カイル", "ディーン", "エルサ", "フィオナ", "ガレス", "ハンナ",
        "イグナス", "ジェイス", "カレン", "ルシア", "マルコ", "ノエル", "オーウェン", "パトリス",
        "クインテ", "ローサ", "セルジュ", "ティナ", "ユリス", "ヴェラ", "ウィルド", "ゼノ",
    };
    private static readonly string[] Family =
    {
        "灰塚", "銀枝", "赤縄", "石橋", "霜原", "鉄鎖", "月見", "白樺",
        "黒沼", "遠雷", "麦穂", "紺碧", "旅笠", "薪割", "鷹匠", "夜明",
    };

    /// <summary>seed から決定的に決まる名。同じ個体なら何度読んでも同じ。</summary>
    public static string NameFromSeed(int seed)
    {
        int s = Mathf.Abs(seed);
        return Family[(s / 97) % Family.Length] + "の" + Given[s % Given.Length];
    }

    /// <summary>その名（または同じ家名）が既に居るか。討伐済の者とも当てない（記録に2人並ぶため）。</summary>
    private static bool NameTaken(string candidate)
    {
        int sep = candidate.IndexOf('の');
        string fam = sep > 0 ? candidate.Substring(0, sep) : candidate;
        string given = sep > 0 ? candidate.Substring(sep + 1) : "";
        for (int i = 0; i < all.Count; i++)
        {
            string other = all[i].name;
            if (other == candidate) return true;
            if (other.StartsWith(fam + "の")) return true;                       // 家名のかぶり
            if (given.Length > 0 && other.EndsWith("の" + given)) return true;   // 名のかぶり
        }
        return false;
    }

    /// <summary>二つ名は**保存しない**（履歴から毎回計算する）。増やすときはここだけ触る。</summary>
    public static string Epithet(Hero h)
    {
        if (h == null) return "";
        if (h.state == State_Turned) return "転向した";
        if (h.releases >= 2) return "二度赦された";
        if (h.releases >= 1) return "解き放たれた";
        if (h.fromAbyss) return "奈落帰りの";
        if (h.escapes >= 5) return "不死身の";
        if (h.escapes >= 3) return "執念の";
        if (h.captures >= 1) return "牢を知る";
        if (h.escapes >= 2) return "二度戻った";
        return "生き延びた";
    }

    public static string DisplayName(Hero h)
    {
        if (h == null) return "";
        return "《" + Epithet(h) + "》" + h.name;
    }
    public static string DisplayName(int id) { return DisplayName(Get(id)); }

    public static string StateName(int state)
    {
        switch (state)
        {
            case State_Deployed: return "襲来中";
            case State_Captive: return "捕虜";
            case State_Slain: return "討伐済";
            case State_Turned: return "転向";
            default: return "野に在り";
        }
    }

    // ============ 📈 因縁による伸び ============

    private static int G(Hero h) { return Mathf.Min(h.escapes, GrowthCap); }
    private static int Gr(Hero h) { return Mathf.Min(h.grudge, GrudgeCap); }

    public static float HpMult(Hero h) { return h == null ? 1f : 1f + G(h) * 0.15f + Gr(h) * 0.03f; }
    public static float AtkMult(Hero h) { return h == null ? 1f : 1f + G(h) * 0.12f + Gr(h) * 0.025f; }
    /// <summary>因縁で上がるランク（最大+2）。素のランクと足して 0..7 にClampして使う。</summary>
    public static int RankBonus(Hero h) { return h == null ? 0 : Mathf.Min(2, G(h) / 2); }
    public static int LevelBonus(Hero h) { return h == null ? 0 : G(h) * 2 + Gr(h); }

    /// <summary>UI用：どれだけ育っているか（0=まだ素、1=上限）。</summary>
    public static float GrowthFrac(Hero h) { return h == null ? 0f : Mathf.Clamp01((float)G(h) / GrowthCap); }

    // ============ 🎲 波に混ぜる ============

    /// <summary>
    /// このターン出てくる名のある者を選ぶ。⚠ `WaveRoster.Roll` から呼ぶ（名簿と同時に確定させる）。
    /// 恨みの深い順。上限は世が育つほど少し増えるが、**波の大半は無名のまま**にする。
    /// </summary>
    public static List<int> PickForWave(int turn, int waveSize) { return PickForWave(turn, waveSize, false); }

    /// <summary>
    /// この波に出す『名のある者』。
    /// ⚠ `force`＝ギルドの布告『賞金首』の日だけ**休みを無視**する（→ [[Proclamation]]）。
    ///   先に告げた以上、来ないことがあってはならない。
    /// </summary>
    public static List<int> PickForWave(int turn, int waveSize, bool force)
    {
        var picked = new List<int>();
        int max = Mathf.Clamp(1 + waveSize / 8, 1, 3);
        var pool = new List<Hero>();
        for (int i = 0; i < all.Count; i++)
        {
            var h = all[i];
            if (h.state != State_AtLarge) continue;
            if (!force && turn - h.lastSeenTurn < Cooldown) continue;   // 出たばかりの顔は少し休ませる
            pool.Add(h);
        }
        pool.Sort((a, b) => (b.grudge + b.escapes * 2).CompareTo(a.grudge + a.escapes * 2));
        for (int i = 0; i < pool.Count && picked.Count < max; i++) picked.Add(pool[i].id);
        return picked;
    }

    /// <summary>名簿に載った＝この波に出る。</summary>
    public static void MarkDeployed(int id, int turn)
    {
        var h = Get(id); if (h == null) return;
        h.state = State_Deployed; h.lastSeenTurn = turn;
    }

    /// <summary>波が終わっても決着がつかなかった者を野に戻す（＝取り逃がし扱いにはしない）。</summary>
    public static void ReleaseDeployedAtWaveEnd()
    {
        for (int i = 0; i < all.Count; i++)
            if (all[i].state == State_Deployed) all[i].state = State_AtLarge;
    }

    // ============ 🩸 出来事 ============

    /// <summary>
    /// 生還した冒険者を名のある者にする（または既に名のある者の記録を更新する）。
    /// ⚠ **名が生まれる条件はここだけ**。呼ぶ側（`AdventurerAI.GrantReturnReward`）が条件を持たない。
    /// </summary>
    /// <param name="id">既に名のある者ならその id、無名なら 0</param>
    /// <param name="hpFrac">生還時の残りHP割合。半分以上削れていたら「取り逃がした」</param>
    /// <returns>名のある者の id（生まれなければ 0）</returns>
    public static int OnEscaped(int id, float hpFrac, bool viaAbyss,
        AdventurerAI.Job job, int rank, int level, bool hasSpell, MagicCatalog.Spell spell, int turn,
        float carriedGear = 0f)
    {
        var h = Get(id);
        if (h != null)
        {
            h.escapes++;
            h.grudge += 1;
            h.hoard += carriedGear;   // 🎁 奪ったぶんを溜める（次はそれを抱えて現れる）
            if (viaAbyss) h.fromAbyss = true;
            h.level = Mathf.Max(h.level, level);
            h.state = State_AtLarge; h.lastSeenTurn = turn;
            NotifySystem.Push("<b>" + DisplayName(h) + "</b> がまた生きて還った（逃走 " + h.escapes + " 回目）", NotifySystem.Kind.Loss);
            SoundSystem.Play(SoundSystem.Sfx.Loss);
            Debug.Log("🗡️『因縁』" + DisplayName(h) + " が生還 ― 逃走" + h.escapes + "回／恨み" + h.grudge);
            return h.id;
        }

        // ── 新しく名がつくか ──
        bool worthy = viaAbyss || hpFrac <= 0.5f;
        if (!worthy) return 0;
        if (AtLargeCount >= AtLargeCap) return 0;

        int born = Birth(job, rank, level, hasSpell, spell, turn, viaAbyss, 1, 1);
        var nh = Get(born);
        if (nh != null) nh.hoard += carriedGear;   // 🎁 最初の逃走で奪ったぶんも覚える
        return born;
    }

    /// <summary>名のある者を1人生む。⚠ 通常は `OnEscaped` から。解放（→ [[Prison]]）からも使う。</summary>
    public static int Birth(AdventurerAI.Job job, int rank, int level, bool hasSpell, MagicCatalog.Spell spell,
        int turn, bool viaAbyss, int escapes, int grudge)
    {
        var h = new Hero();
        h.id = nextId++;
        // ⚠ 名がかぶると「同じ家名ばかり出る」ように見えて、名前が記号に戻ってしまう。
        //   （実測：2人生んだら2人とも同じ家名になった）→ 既出と当たらない seed を引き直す。
        h.nameSeed = Random.Range(1, 1000000);
        for (int tries = 0; tries < 24 && NameTaken(NameFromSeed(h.nameSeed)); tries++)
            h.nameSeed = Random.Range(1, 1000000);
        h.name = NameFromSeed(h.nameSeed);
        h.job = job; h.rank = rank; h.level = level;
        h.hasSpell = hasSpell; h.spell = spell;
        h.escapes = escapes; h.grudge = grudge; h.fromAbyss = viaAbyss;
        h.state = State_AtLarge;
        h.bornTurn = turn; h.lastSeenTurn = turn;
        all.Add(h);

        NotifySystem.Push("<b>" + DisplayName(h) + "</b> が名を得た ― 取り逃がした者は、強くなって戻る", NotifySystem.Kind.Loss);
        SoundSystem.Play(SoundSystem.Sfx.Danger);
        Debug.Log("🗡️『因縁が生まれた』" + DisplayName(h) + "（" + WaveRoster.JobName(job) + " " + AdventurerAI.RankLetter(rank) + "級 Lv" + level + "）");
        return h.id;
    }

    /// <summary>
    /// 討伐した。⚠ 報酬は呼ぶ側（`AdventurerAI.TakeDamage`）ではなくここで出す（1箇所にまとめる）。
    /// <para>
    /// ⚠⚠ `worldPos` を受けるのは**この見返りを撃破の場に出すため**（G-2）。
    ///   ここで渡すDP（Lv20・逃走3回で約860）は、`AdventurerAI` の `killBonusDP`（数十）とは**別枠**で、
    ///   これまで**画面右の通知にしか出ていなかった**。決着の一番大きい数字が盤の上に出ないので、
    ///   「やっと討ち取った」という山が立たなかった。
    /// </para>
    /// </summary>
    public static void OnSlain(int id, Vector3 worldPos)
    {
        var h = Get(id); if (h == null || h.state == State_Slain) return;
        h.state = State_Slain;

        // 因縁が深いほど、決着の見返りが大きい
        int dp = Mathf.RoundToInt((180 + h.level * 12) * (1f + G(h) * 0.35f));
        int mat = 3 + G(h) * 2;
        var res = DungeonResourceManager.Instance;
        if (res != null) { res.AddDP(dp); res.AddMaterial(mat); }
        // 🕸️ 因縁の相手を仕留めると、噂は「あそこは還れない」に変わる（脅威度が少し下がる）
        LureEconomy.CalmDown(0.04f + G(h) * 0.01f);
        RunStats.NoteNemesisSlain();
        EurekaTracker.OnNemesisSlain();

        // 🎁 こいつが世界に撒いた装備を回収する（→ [[LureEconomy]]）。**装備水準を下げる唯一の道**
        float gearBack = LureEconomy.RecoverGear(h.hoard);
        h.hoard = 0f;

        // 💥 決着を**盤の上に**出す（→ [[KillFeedback]]）。⚠ 数字は上で確定したものをそのまま見せるだけ
        KillFeedback.OnNemesisSlain(worldPos, DisplayName(h), dp, mat, gearBack);

        NotifySystem.Push("<b>" + DisplayName(h) + "</b> を討ち取った（+" + dp + "DP／+" + mat + "素材"
            + (gearBack > 0.05f ? "／世界の装備水準 -" + gearBack.ToString("0.0") : "") + "）", NotifySystem.Kind.Gain);
        Debug.Log("🗡️『決着』" + DisplayName(h) + " を討伐（+" + dp + "DP／装備水準 -" + gearBack.ToString("0.0") + "）");
    }

    public static void OnCaptured(int id)
    {
        var h = Get(id); if (h == null) return;
        h.state = State_Captive; h.captures++;
    }

    /// <summary>牢から解き放った。⚠ **必ず恨みを持って戻る**（これが解放を「安い出口」にしない歯止め）。</summary>
    public static void OnReleased(int id, int turn)
    {
        var h = Get(id); if (h == null) return;
        h.state = State_AtLarge; h.releases++; h.grudge += 3; h.lastSeenTurn = turn;
    }

    /// <summary>こちらに転向した（＝もう波には出てこない）。</summary>
    public static void OnTurned(int id)
    {
        var h = Get(id); if (h == null) return;
        h.state = State_Turned;
    }

    /// <summary>牢の中で死んだ／喰われた。討伐と同じ「もういない」だが、報酬は呼ぶ側が出す。</summary>
    public static void OnPerished(int id)
    {
        var h = Get(id); if (h == null) return;
        h.state = State_Slain;
    }

    /// <summary>同胞を目の前で殺された者は恨みを深める（野に在る者全員）。</summary>
    public static void DeepenGrudgeAll(int amount)
    {
        for (int i = 0; i < all.Count; i++)
            if (all[i].state == State_AtLarge || all[i].state == State_Deployed)
                all[i].grudge += amount;
    }
}
