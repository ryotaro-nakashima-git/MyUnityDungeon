using UnityEngine;

/// <summary>
/// 🎧 **音の目録**。ゲームが欲しがっている音を1か所に並べ、
/// 「どのファイルを」「どこに置くと」「何のときに鳴るか」を1枚で分かるようにする。
///
/// <para>
/// ⚠⚠ **ここは名前と説明だけを持つ。鳴らす処理は持たない。**
///   `SoundSystem` は**まずファイルを探し、無ければ手続き生成に落とす**。
///   だから目録が埋まっていなくてもゲームは無音にならないし、
///   1つ置くごとにその音だけが本物に差し替わる（全部揃うまで待たなくてよい）。
/// </para>
///
/// <para>
/// ⚠ 置き場所は <c>Assets/Resources/Audio/{Sfx,Bgm,Voice}/&lt;id&gt;.mp3</c>。
///   `Resources.Load` は**拡張子を書かない**ので、mp3 でも wav でも同じ id で拾える。
///   ⚠ `Resources` の外に置くと**ビルドに含まれない**（実行時に読めない）。
/// </para>
///
/// <para>
/// ⚠ `readonly` ＝ カタログ＝セーブに載せない（→ [[SaveSystem]]）。
/// </para>
/// 関連: [[SoundSystem]]（鳴らす側） [[game-polish-plan]]。
/// </summary>
public static class AudioAssets
{
    public const string SfxDir = "Audio/Sfx/";
    public const string BgmDir = "Audio/Bgm/";
    public const string VoiceDir = "Audio/Voice/";
    /// <summary>
    /// 🌬️ **環境音のベッド**（曲の下に敷く5秒ループ）。
    /// ⚠⚠ 曲の**置き換えではない**。無料枠では曲そのものが作れなかった（Music API は有料）ので、
    ///   効果音APIの5秒ループで「その場の空気」だけ先に用意した。
    ///   本物の曲が `Bgm/` に入っても、こちらはそのまま下に残ってよい。
    /// </summary>
    public const string AmbDir = "Audio/Amb/";

    public struct Spec
    {
        public string id;       // ファイル名（拡張子なし）
        public string when;     // いつ鳴るか（日本語・目録を読む人のため）
        public string prompt;   // 生成に使う指示（英語のほうが通りが良い）
        public float seconds;   // 目安の長さ
    }

    // ============ 効果音 ============
    // ⚠ 並びは `SoundSystem.Sfx` と**同じ順**にしてある（`SpecOf` が index で引く）。
    private static readonly Spec[] sfx =
    {
        new Spec { id = "click",    seconds = 0.12f, when = "ボタン・タブを押した",
                   prompt = "tiny dry UI click, dark fantasy interface, soft wooden tick, no reverb" },
        new Spec { id = "confirm",  seconds = 0.35f, when = "決定した（購入・研究・確定）",
                   prompt = "short confirming chime, two ascending notes, warm bell, dark fantasy UI" },
        new Spec { id = "cancel",   seconds = 0.30f, when = "取り消した・閉じた",
                   prompt = "short descending two-note cancel blip, muted, dark fantasy UI" },
        new Spec { id = "place",    seconds = 0.35f, when = "盤に物を置いた",
                   prompt = "heavy stone block set down on dungeon floor, short thud with small dust" },
        new Spec { id = "remove",   seconds = 0.30f, when = "盤から物を撤去した",
                   prompt = "stone object lifted away, short scrape and low thump" },
        new Spec { id = "error",    seconds = 0.40f, when = "できない操作をした",
                   prompt = "low buzzing rejection sound, two short dull pulses, dark fantasy UI" },
        new Spec { id = "gain",     seconds = 0.55f, when = "資源が入った・良い知らせ",
                   prompt = "bright three-note ascending arpeggio, small glass bells, rewarding, short" },
        new Spec { id = "loss",     seconds = 0.60f, when = "失った・悪い知らせ",
                   prompt = "sombre two-note descending motif, low strings, brief, ominous" },
        new Spec { id = "danger",   seconds = 0.90f, when = "危険の警告（大招集・魔王の被弾）",
                   prompt = "ominous low pulsing alarm, distant war drum, dark fantasy danger cue" },
        new Spec { id = "story",    seconds = 1.60f, when = "物語の報せ（腹心の報告など）",
                   prompt = "soft mysterious chord swell, dark ambient pad, gentle, storytelling cue" },
        new Spec { id = "turn",     seconds = 1.40f, when = "ターンが変わった",
                   prompt = "single deep bell toll with long tail, cavern reverb, marks passage of time" },
        new Spec { id = "hit",      seconds = 0.18f, when = "殴った・当たった",
                   prompt = "short blunt melee impact, flesh and armour, dry, no music" },
        new Spec { id = "kill",     seconds = 0.45f, when = "冒険者を倒した",
                   prompt = "finishing blow, bone crunch with low whoosh, brief dark fantasy kill" },
        new Spec { id = "wave",     seconds = 2.00f, when = "波が来る（侵略開始）",
                   prompt = "distant war horn call echoing through a cavern, approaching invaders, dread" },
        new Spec { id = "command",  seconds = 0.90f, when = "号令を撃った",
                   prompt = "commanding low brass stab with dark magic shimmer, authoritative, short" },
        new Spec { id = "discover", seconds = 1.10f, when = "新しい物を見つけた・解禁",
                   prompt = "magical discovery chime, shimmering bright bells rising, wondrous, short" },
        new Spec { id = "save",     seconds = 0.50f, when = "保存した",
                   prompt = "quiet quill scratch on parchment then a soft seal press, brief" },
    };

    // ============ BGM ============
    // ⚠ ループ前提。⚠⚠ **戦闘だけ別の曲にする**のが肝で、準備と地上は同じ世界の顔でよい。
    private static readonly Spec[] bgm =
    {
        new Spec { id = "prepare", seconds = 60f, when = "前半・迷宮（準備フェーズ）",
                   prompt = "dark fantasy dungeon ambience loop, slow minor arpeggio, distant dripping water, quiet and patient, seamless loop" },
        new Spec { id = "battle",  seconds = 60f, when = "防衛戦",
                   prompt = "dark fantasy battle loop, driving percussion, low strings ostinato, urgent but not chaotic, seamless loop" },
        new Spec { id = "surface", seconds = 60f, when = "後半・地上（世界地図）",
                   prompt = "wide strategic map theme loop, airy strings and soft choir, medieval fantasy overworld, calm, seamless loop" },
    };

    // ============ 音声 ============
    // ⚠⚠ **動く文章は喋らせない。** 進言の本文はターンごとに変わるので、
    //   読み上げると生成が終わらない（クレジットも尽きる）。
    //   代わりに**何度も来る決まった場面**だけを、腹心の声で置く。
    private static readonly Spec[] voice =
    {
        new Spec { id = "v_wave_start", when = "侵略開始を押した",
                   prompt = "来ます。――迎え撃つご用意を。" },
        new Spec { id = "v_wave_held",  when = "波を凌いだ（決算）",
                   prompt = "凌ぎました。今日の迷宮は、貴方のものです。" },
        new Spec { id = "v_lord_hurt",  when = "魔王HPが大きく削られた",
                   prompt = "玉座に刃が届きました。次は、ありません。" },
        new Spec { id = "v_fever",      when = "大招集を宣言した",
                   prompt = "噂を撒きました。――嵐が来ます。" },
        new Spec { id = "v_defeat",     when = "敗北",
                   prompt = "……ここまでのようです。またお会いしましょう。" },
        new Spec { id = "v_victory",    when = "勝利",
                   prompt = "地の底から、世界が変わりました。おめでとうございます。" },
    };

    public static int SfxCount { get { return sfx.Length; } }
    public static int BgmCount { get { return bgm.Length; } }
    public static int VoiceCount { get { return voice.Length; } }

    public static Spec SfxAt(int i) { return sfx[Mathf.Clamp(i, 0, sfx.Length - 1)]; }
    public static Spec BgmAt(int i) { return bgm[Mathf.Clamp(i, 0, bgm.Length - 1)]; }
    public static Spec VoiceAt(int i) { return voice[Mathf.Clamp(i, 0, voice.Length - 1)]; }

    /// <summary>効果音の id（`SoundSystem.Sfx` の並びと同じ）。範囲外なら空。</summary>
    public static string SfxId(int i) { return (i >= 0 && i < sfx.Length) ? sfx[i].id : ""; }
    /// <summary>BGM の id（`SoundSystem.Bgm` の `None` を除いた並び）。</summary>
    public static string BgmId(int i) { return (i >= 0 && i < bgm.Length) ? bgm[i].id : ""; }

    public static string VoiceIdOf(string id)
    {
        for (int i = 0; i < voice.Length; i++) if (voice[i].id == id) return voice[i].id;
        return "";
    }
}
