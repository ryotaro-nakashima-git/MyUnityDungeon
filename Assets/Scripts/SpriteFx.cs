using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🔥 **1枚絵の連番を1回だけ再生する使い捨てエフェクト**（D-1）。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：既存の `BattleVfx` は手続き生成の円と四角しか出せない。
///   第二形態は<b>この作品でいちばん重い出来事</b>（殻が割れる・敵を喰う）なのに、
///   出ていたのは**オレンジの円がひとつ膨らむだけ**だった。
///   ここが軽いと、2ゲージ制そのものが「数字が増えただけ」に見える。
/// </para>
///
/// <para>
/// ⚠ 絵は `Resources/Fx/&lt;name&gt;/&lt;n&gt;.png`（PixelLab・連番が切れるまで読む）。
///   `MinionAnim` と同じ約束 ―― **絵が無ければ何も出さずに黙って消える**（呼ぶ側は気にしなくていい）。
/// ⚠ 進めるのは **`deltaTime`**（戦闘の一部なので、倍速や一時停止に追従する）。
///   UIの演出とは逆 → [[ui-conventions]] の時間の使い分け。
/// ⚠ シートは**静的にキャッシュ**する（1波で何十回も出るので毎回 `Resources.Load` しない）。
/// </para>
/// </summary>
public class SpriteFx : MonoBehaviour
{
    private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

    /// <summary>連番を読む。⚠ 無ければ null（呼ぶ側は何も出さない）。</summary>
    public static Sprite[] Sheet(string name)
    {
        Sprite[] s;
        if (cache.TryGetValue(name, out s)) return s;
        var list = new List<Sprite>();
        for (int i = 0; i < 32; i++)
        {
            var sp = Resources.Load<Sprite>("Fx/" + name + "/" + i);
            if (sp == null) break;
            list.Add(sp);
        }
        s = list.Count > 0 ? list.ToArray() : null;
        cache[name] = s;
        return s;
    }

    /// <summary>1回だけ再生して消える。`scale` はワールド単位の大きさの目安。</summary>
    public static void Play(string name, Vector3 pos, float scale = 1.6f, float fps = 16f,
                            Color? tint = null, int sortingOrder = 70)
    {
        var sheet = Sheet(name);
        if (sheet == null) return;
        var go = new GameObject("Fx_" + name);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sheet[0];
        sr.sortingOrder = sortingOrder;
        sr.color = tint.HasValue ? tint.Value : Color.white;
        // ⚠ 絵の実寸はバラバラ（48〜80px）。**高さを基準に正規化**して、どの絵でも同じ大きさに見せる
        //   （[[pixellab-pipeline]] で配下にも同じ手当てをしている）。
        float h = sheet[0].bounds.size.y;
        float k = h > 0.001f ? scale / h : 1f;
        go.transform.localScale = Vector3.one * k;
        var fx = go.AddComponent<SpriteFx>();
        fx.sheet = sheet; fx.sr = sr; fx.fps = Mathf.Max(1f, fps);
    }

    private Sprite[] sheet;
    private SpriteRenderer sr;
    private float fps, t;

    private void Update()
    {
        t += Time.deltaTime * fps;
        int i = Mathf.FloorToInt(t);
        if (sheet == null || i >= sheet.Length) { Destroy(gameObject); return; }
        sr.sprite = sheet[i];
        // 終わりぎわは薄くして消す（ぶつ切りにしない）
        float k = 1f - Mathf.Clamp01((t - (sheet.Length - 1.5f)) / 1.5f);
        var c = sr.color; c.a = k; sr.color = c;
    }
}
