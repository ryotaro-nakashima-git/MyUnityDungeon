using UnityEngine;

/// <summary>
/// ⚔️ **攻撃の手応え**。殴った／殴られた瞬間に、**攻撃の種類ごとに違う絵**を出す。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：これまで演出は**倒したときだけ**だった（→ [[KillFeedback]]）。
///   1体倒すまでに十数回殴り合うので、**戦闘時間の大半に手応えが無い**状態だった。
///   しかも誰が何をしているのか盤から読めない ―― 盗賊の刺突も戦士のなぎ払いも
///   術者の魔法も、画面上では**まったく同じ「何も起きない」**だった。
/// </para>
///
/// <para>
/// ⚠⚠ **全部同じ絵にしない。** 種類が読めないなら出す意味が半分無い。
///   分け方は2軸：
///   ① **形**（`Kind`）＝ 何をしたか。なぎ払い／刺突／打撃／爪／魔法。
///   ② **色** ＝ 誰の何か。魔法は**属性16色**、物理は陣営（冒険者＝鋼／配下＝紅）。
///   形は少数でよい ―― 16属性ぶんの絵を作るのではなく、**1つの形を属性色で染める**。
/// </para>
///
/// <para>
/// ⚠ 段は3つ：**買ったパーティクル → 手続きの形 → 何もしない**（→ [[FxPrefabs]]）。
///   素材が増えたら `PrefabFor` を1行足すだけで、その種類だけ本物に差し替わる。
/// ⚠ **強さには一切触らない。** ここは見せるだけ（→ [[difficulty-curve-orders]]）。
/// ⚠ 時間は `Time.deltaTime`（戦闘の一部なので倍速に乗る → [[ui-conventions]]）。
/// </para>
/// 関連: [[KillFeedback]]（倒した瞬間） [[BattleVfx]] [[MagicCatalog]]（属性の色）。
/// </summary>
public class AttackFx : MonoBehaviour
{
    /// <summary>⚠ 末尾に足す。</summary>
    public enum Kind
    {
        Slash,   // 🪓 なぎ払い（戦士）＝横に薙ぐ弧
        Pierce,  // 🗡️ 刺突（盗賊）＝鋭い一直線
        Blunt,   // 🔨 打撃（鈍器・素手）＝重い衝撃の輪
        Claw,    // 🐾 爪（配下の物理）＝3本の裂傷
        Magic,   // 🔮 魔法の着弾＝属性色の弾ける輪
    }

    // ── 陣営の色（物理のとき）──
    /// <summary>冒険者の物理＝鋼。</summary>
    public static readonly Color HeroSteel = new Color(0.82f, 0.88f, 1f);
    /// <summary>配下の物理＝紅。</summary>
    public static readonly Color MinionRed = new Color(1f, 0.45f, 0.42f);

    // ============ 出す ============
    /// <summary>
    /// ⚔️ 攻撃の絵を出す。
    /// </summary>
    /// <param name="kind">何をしたか。</param>
    /// <param name="at">当たった位置（＝殴られた側）。</param>
    /// <param name="from">殴った側の位置。向きを決めるのに使う。</param>
    /// <param name="col">色（魔法は属性色、物理は陣営色）。</param>
    public static void Play(Kind kind, Vector3 at, Vector3 from, Color col)
    {
        // ✨ 買ったパーティクルがある種類はそちらを優先（→ [[FxPrefabs]]）
        // ⚠ 色は必ず渡す。魔法は属性で色が変わるので、渡さないと16属性が同じ絵になる。
        string prefab = PrefabFor(kind);
        if (prefab != null && FxPrefabs.Play(prefab, at, ScaleFor(kind), col)) return;

        Vector2 dir = at - from;
        float ang = dir.sqrMagnitude > 0.0001f ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg : 0f;
        Spawn(kind, at, ang, col);
    }

    /// <summary>
    /// この種類に当てる買った素材（無ければ null）。⚠ 増えたらここに1行足す。
    ///
    /// <para>
    /// ⚠⚠ **色が意味を持つ演出には買った素材を使わない。**
    ///   買ったパーティクルは `Color over Lifetime` を持っていることが多く、
    ///   `startColor` を差し替えても**その色に上書きされて元の色のまま出る**（実測：
    ///   火炎も氷結も同じ金色になった）。
    ///   → **色が固定でよい演出**（斬撃＝鋼の武器）だけ買った素材、
    ///     **色が意味そのもの**の演出（魔法＝属性16色）は手続きで描く。
    /// </para>
    /// </summary>
    private static string PrefabFor(Kind k)
    {
        switch (k)
        {
            case Kind.Slash: return FxPrefabs.Slash;   // ✨ 斬撃は色が固定でよい（鋼の武器）
            default: return null;                       // 刺突・打撃・爪・魔法は手続きの形
        }
    }

    private static float ScaleFor(Kind k)
    {
        // ⚠ 盤は1マス＝ワールド1。素の大きさだと2マス分を覆う（→ [[FxPrefabs]]）
        switch (k) { case Kind.Slash: return 0.42f; case Kind.Magic: return 0.38f; default: return 0.4f; }
    }

    // ============ 🎬 コマ送り（PixelLab で作った絵があれば使う）============
    // ⚠⚠ **白〜淡色で作ってある**のが肝。`SpriteRenderer.color` は**乗算**なので、
    //   白い絵なら陣営色にも属性色にも染まる。色を焼き込むと染まらない
    //   （買ったパーティクルで実際にそれをやって失敗した → `PrefabFor` の注記）。
    // 置き場: `Resources/Fx/atk_<kind>/<n>.png`（0始まりの連番）。
    // ⚠ `MinionAnim` と同じ「読めるところまで読む」方式。作りかけでも壊れない。
    private const int MaxFrames = 16;
    private static readonly System.Collections.Generic.Dictionary<Kind, Sprite[]> seqCache
        = new System.Collections.Generic.Dictionary<Kind, Sprite[]>();

    private static Sprite[] Frames(Kind k)
    {
        Sprite[] arr;
        if (seqCache.TryGetValue(k, out arr)) return arr;
        var list = new System.Collections.Generic.List<Sprite>();
        string dir = "Fx/atk_" + k.ToString().ToLowerInvariant() + "/";
        for (int i = 0; i < MaxFrames; i++)
        {
            var sp = Resources.Load<Sprite>(dir + i);
            if (sp == null) break;
            list.Add(sp);
        }
        arr = list.Count > 0 ? list.ToArray() : null;   // ⚠ 無い場合も覚える（毎回探さない）
        seqCache[k] = arr;
        return arr;
    }

    /// <summary>🔄 差し替えたあとに探し直す。</summary>
    public static void ReloadFrames() { seqCache.Clear(); }

    /// <summary>
    /// 🎬 コマ送りのときの大きさ。⚠ **手続きの形とは別に持つ。**
    ///   絵は 64px＝1マス（PPU64）で取り込んであるので、1.0 が「ちょうど1マス」。
    ///   ⚠ 0.5 前後だと**盤の模様に埋もれて見えない**（実測でそれを踏んだ）。
    /// </summary>
    private static float FrameScale(Kind k)
    {
        switch (k) { case Kind.Pierce: return 1.25f; case Kind.Claw: return 1.15f; default: return 1.1f; }
    }

    /// <summary>
    /// 🎬 コマ送りの絵が**もともと向いている角度**。
    /// ⚠⚠ PixelLab の絵は斜め（右上向き）に描かれてくるので、そのまま攻撃方向へ回すと
    ///   45度ずれる。ここで引いて打ち消す。
    /// </summary>
    private static float FrameAngleOffset(Kind k)
    {
        switch (k) { case Kind.Pierce: return -45f; case Kind.Claw: return -45f; default: return 0f; }
    }

    // ============ 手続きの形 ============
    private Kind kind;
    private float t, dur;
    private Color col;
    private SpriteRenderer sr;
    private float baseScale, spin;
    private Sprite[] frames;   // 🎬 あればコマ送り、無ければ1枚の形を伸縮させる

    private static void Spawn(Kind kind, Vector3 pos, float angleDeg, Color col)
    {
        var seq = Frames(kind);
        var go = new GameObject("AtkFx_" + kind);
        go.transform.position = new Vector3(pos.x, pos.y, pos.z - 0.55f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg + (seq != null ? FrameAngleOffset(kind) : 0f));

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = seq != null ? seq[0] : SpriteFor(kind);
        sr.color = col;
        sr.sortingOrder = 410;   // 🎭 配下より手前・UIより奥（`FxSprite` の 400 帯）

        var v = go.AddComponent<AttackFx>();
        v.kind = kind; v.sr = sr; v.col = col; v.t = 0f; v.frames = seq;
        switch (kind)
        {
            case Kind.Pierce: v.dur = 0.16f; v.baseScale = 0.85f; break;   // 速い＝鋭さ
            case Kind.Blunt:  v.dur = 0.26f; v.baseScale = 0.55f; break;   // 遅い＝重さ
            case Kind.Claw:   v.dur = 0.22f; v.baseScale = 0.70f; v.spin = Random.Range(-18f, 18f); break;
            case Kind.Slash:  v.dur = 0.20f; v.baseScale = 0.90f; break;
            default:          v.dur = 0.24f; v.baseScale = 0.60f; break;
        }
        go.transform.localScale = Vector3.one * (seq != null ? FrameScale(kind) : v.baseScale * 0.6f);
    }

    private void Update()
    {
        t += Time.deltaTime;
        float p = dur > 0f ? Mathf.Clamp01(t / dur) : 1f;

        // 🎬 コマ送りがあるなら**形はいじらない**（絵が動きを持っているので、
        //    さらに伸縮させると二重に動いて気持ち悪くなる）。
        if (frames != null)
        {
            int idx = Mathf.Clamp(Mathf.FloorToInt(p * frames.Length), 0, frames.Length - 1);
            sr.sprite = frames[idx];
            transform.localScale = Vector3.one * FrameScale(kind);
            var cc = col; cc.a = col.a * (p < 0.7f ? 1f : 1f - (p - 0.7f) / 0.3f);
            sr.color = cc;
            if (p >= 1f) Destroy(gameObject);
            return;
        }

        switch (kind)
        {
            case Kind.Pierce:
                // 🗡️ 突き込んで、すっと消える（伸びてから細る）
                transform.localScale = new Vector3(baseScale * (0.5f + p * 1.1f), baseScale * (1f - p * 0.55f), 1f);
                break;
            case Kind.Blunt:
                // 🔨 押し広がる輪（重い）
                transform.localScale = Vector3.one * Mathf.Lerp(baseScale * 0.45f, baseScale * 1.35f, p);
                break;
            case Kind.Claw:
                // 🐾 裂けて開く
                transform.localScale = new Vector3(baseScale * (0.7f + p * 0.6f), baseScale * (0.9f + p * 0.35f), 1f);
                transform.Rotate(0f, 0f, spin * Time.deltaTime * 6f);
                break;
            default:
                transform.localScale = Vector3.one * Mathf.Lerp(baseScale * 0.6f, baseScale * 1.15f, p);
                break;
        }

        // ⚠ 最後まで濃いままだと画面が埋まる。後半だけ急に薄くする（前半は見せたい）
        var c = col; c.a = col.a * (p < 0.45f ? 1f : 1f - (p - 0.45f) / 0.55f);
        sr.color = c;
        if (p >= 1f) Destroy(gameObject);
    }

    // ============ 形（実行時に描く。外部素材を待たない）============
    private static Sprite sprSlash, sprPierce, sprBlunt, sprClaw, sprMagic;

    private static Sprite SpriteFor(Kind k)
    {
        switch (k)
        {
            case Kind.Slash:
                // 🪓 三日月：外側の円から内側の円を抜く
                if (sprSlash == null) sprSlash = Draw((x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    return r > 0.55f && r < 0.95f && x > -0.15f;
                });
                return sprSlash;
            case Kind.Pierce:
                // 🗡️ 細い菱形（進行方向に長い）
                if (sprPierce == null) sprPierce = Draw((x, y) =>
                    Mathf.Abs(y) < 0.22f * (1f - Mathf.Abs(x)) && Mathf.Abs(x) < 0.98f);
                return sprPierce;
            case Kind.Blunt:
                // 🔨 太い輪
                if (sprBlunt == null) sprBlunt = Draw((x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    return r > 0.48f && r < 0.9f;
                });
                return sprBlunt;
            case Kind.Claw:
                // 🐾 3本の裂傷（y をずらした細い弧を3本）
                if (sprClaw == null) sprClaw = Draw((x, y) =>
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        float yy = y - i * 0.42f;
                        // 弧：x に対して少し反った線
                        float line = 0.30f * x * x - 0.10f;
                        if (Mathf.Abs(yy - line) < 0.085f && Mathf.Abs(x) < 0.92f) return true;
                    }
                    return false;
                });
                return sprClaw;
            default:
                // 🔮 弾ける輪＋中心（属性色で染める）
                if (sprMagic == null) sprMagic = Draw((x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    return r < 0.28f || (r > 0.6f && r < 0.88f);
                });
                return sprMagic;
        }
    }

    private const int S = 64;

    /// <summary>⚠ `MarkerArt.Build` と同じ作り（2×2 のスーパーサンプリングでギザギザを抑える）。</summary>
    private static Sprite Draw(System.Func<float, float, bool> inside)
    {
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[S * S];
        for (int j = 0; j < S; j++)
            for (int i = 0; i < S; i++)
            {
                int hit = 0;
                for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        float x = ((i + 0.25f + sx * 0.5f) / S) * 2f - 1f;
                        float y = ((j + 0.25f + sy * 0.5f) / S) * 2f - 1f;
                        if (inside(x, y)) hit++;
                    }
                px[j * S + i] = new Color32(255, 255, 255, (byte)(hit * 255 / 4));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }
}
