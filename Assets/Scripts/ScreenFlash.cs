using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ⚡ <b>画面が一瞬光る</b>。号令や魔王の一撃のような「いま、大きいことが起きた」を、
/// 倍速でも取り逃さないようにするための最後の手段。
///
/// <para>
/// ⚠⚠ <b>なぜ要るか</b>：2x/4x で戦闘を回すと、数字もスキル名も一瞬で消えて
///   「いま誰が何で大ダメージを与えたのか」が追えなかった（プレイ動画の指摘）。
///   数字の大きさ（→ [[FloatText]]）だけでは、画面の外を見ていたら気づけない。
/// </para>
///
/// <para>
/// ⚠ <b>使うのは「1波に数回しか起きないこと」だけ</b>。毎回の殴りで光らせると、
///   ただのチカチカになって<b>本当に大きいことが埋もれる</b>（＝いまと同じ問題に戻る）。
/// ⚠ <b>報酬も判定も持たない。</b>ここは見せるだけの層（→ [[feel-pass-d1-d4]] の決まり）。
/// ⚠ 時間は <c>unscaledDeltaTime</c>。倍速でも一時停止でも<b>同じ長さ</b>で見えてほしい
///   ―― 4倍速のとき4倍速く消えたら、速いときほど気づけないという逆のことが起きる。
/// ⚠ 光の強さは控えめ（既定 0.22）。全画面の白飛びは、暗い画面のこの作品では目に痛い。
/// </para>
/// </summary>
public class ScreenFlash : MonoBehaviour
{
    private static ScreenFlash inst;
    private Image img;
    private float life, total;
    private Color tint;

    /// <summary>⚡ 一瞬光らせる。⚠ 重ねて呼ばれたら**強いほうが勝つ**（足し算にしない＝白飛びしない）。</summary>
    public static void Play(Color color, float strength = 0.22f, float seconds = 0.22f)
    {
        if (!Application.isPlaying) return;
        Ensure();
        if (inst == null) return;
        color.a = Mathf.Clamp01(strength);
        // ⚠ まだ光っている最中に弱い光が来たら無視する（連発で暗転しつづけるのを防ぐ）
        if (inst.life > 0f && inst.tint.a >= color.a) return;
        inst.tint = color;
        inst.life = inst.total = Mathf.Max(0.05f, seconds);
    }

    private static void Ensure()
    {
        if (inst != null) return;
        var go = new GameObject("ScreenFlash");
        Object.DontDestroyOnLoad(go);

        // ⚠ 専用の Canvas を立てる。既存の UI Canvas にぶら下げると、
        //   地上/迷宮でCanvasごと切り替わるときに一緒に消える（→ [[UIKit split]]）。
        var cv = go.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 5000;                  // いちばん上
        go.AddComponent<CanvasScaler>();

        var imgGo = new GameObject("Fill", typeof(RectTransform));
        imgGo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var im = imgGo.AddComponent<Image>();
        im.raycastTarget = false;                // ⚠ クリックを吸わない（盤が触れなくなる）
        im.color = new Color(1f, 1f, 1f, 0f);

        inst = go.AddComponent<ScreenFlash>();
        inst.img = im;
    }

    private void LateUpdate()
    {
        if (img == null) return;
        if (life <= 0f)
        {
            if (img.color.a > 0f) img.color = new Color(1f, 1f, 1f, 0f);
            return;
        }
        life -= Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(life / total);
        // 出た瞬間がいちばん強く、すっと引く
        var c = tint; c.a = tint.a * k * k;
        img.color = c;
    }
}
