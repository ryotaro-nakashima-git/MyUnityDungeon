using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 🗂️ 段A：**タブを切り替えたら中身を横から滑り込ませる**（0.18秒）。
/// 右のタブへ移ったら右から、左のタブへ戻ったら左から入る ―― どちらへ動いたかが手に残る。
///
/// ⚠ タブはそれぞれの窓が独自に作っている（共通の部品が無い）。なので「タブの番号」だけを見張る：
///   `TabChanged(key, idx)` が前回と違えば向き（±1）を返し、呼んだ側が滑らせる物を渡す。
/// ⚠ 滑らせ方は2つ：
///   - 窓の中身まるごと（`SlideWhole`）：タブが中身の外にある窓（図鑑の家系・地上の左の窓）
///   - タブの行より下の子だけ（`SlideChildren`）：タブが中身の中に作り直される窓（生産・研究の時代）
/// ⚠ 時間は unscaled（一時停止・倍速に引きずられない）。演出だけで、状態は何も変えない。
/// <para>`GameUIManager` の partial。</para>
/// </summary>
public partial class GameUIManager
{
    private const float SlideDur = 0.18f, SlideDist = 40f;

    private struct SlideItem { public RectTransform rt; public Vector2 basePos; public CanvasGroup cg; }
    private readonly List<SlideItem> slideItems = new List<SlideItem>();
    private readonly Dictionary<string, int> tabLast = new Dictionary<string, int>();
    private float slideT = -1f;
    private Vector2 slideFrom;

    /// <summary>タブの番号が変わったか。変わっていれば向き（新しい方が後ろ＝+1）、同じ・初回・開閉なら 0。</summary>
    private int TabChanged(string key, int idx)
    {
        int prev;
        bool had = tabLast.TryGetValue(key, out prev);
        tabLast[key] = idx;
        if (!had || prev == idx || prev < 0 || idx < 0) return 0;
        return idx > prev ? 1 : -1;
    }

    /// <summary>中身まるごと滑らせる。<paramref name="vertical"/>＝縦に並んだタブ（地上の左の列）は縦に。</summary>
    private void SlideWhole(RectTransform rt, int dir, bool vertical = false)
    {
        if (rt == null || dir == 0) return;
        BeginSlide(dir, vertical);
        AddSlide(rt);
    }

    /// <summary>
    /// タブの行より下の子だけ滑らせる（⚠ 子は Place の左上基準＝y は下向きに正）。
    /// タブの行そのものは動かさない ―― 押した所が逃げると、押せたのか分からなくなる。
    /// </summary>
    private void SlideChildren(RectTransform content, float belowY, int dir)
    {
        if (content == null || dir == 0) return;
        BeginSlide(dir, false);
        for (int i = 0; i < content.childCount; i++)
        {
            var c = content.GetChild(i) as RectTransform;
            if (c == null || !c.gameObject.activeSelf) continue;
            if (-c.anchoredPosition.y < belowY - 0.5f) continue;
            AddSlide(c);
        }
    }

    private void BeginSlide(int dir, bool vertical)
    {
        FinishSlide();   // ⚠ 続けて押したら前の動きは終わらせてから（位置がずれたまま残らないように）
        slideFrom = vertical ? new Vector2(0f, -dir * SlideDist * 0.6f) : new Vector2(dir * SlideDist, 0f);
        slideT = 0f;
    }

    private void AddSlide(RectTransform rt)
    {
        var cg = rt.GetComponent<CanvasGroup>();
        if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
        slideItems.Add(new SlideItem { rt = rt, basePos = rt.anchoredPosition, cg = cg });
        rt.anchoredPosition += slideFrom;
        cg.alpha = 0f;
    }

    private void FinishSlide()
    {
        foreach (var s in slideItems)
        {
            if (s.rt == null) continue;   // ⚠ 作り直しで消えた子は飛ばす
            s.rt.anchoredPosition = s.basePos;
            if (s.cg != null) s.cg.alpha = 1f;
        }
        slideItems.Clear();
        slideT = -1f;
    }

    /// <summary>毎フレーム（Hud の Update から）。</summary>
    private void TickTabSlide()
    {
        if (slideT < 0f) return;
        slideT += Time.unscaledDeltaTime;
        float p = Mathf.Clamp01(slideT / SlideDur);
        if (p >= 1f) { FinishSlide(); return; }
        float e = 1f - Mathf.Pow(1f - p, 3f);   // 速く入って、ゆっくり止まる
        var off = slideFrom * (1f - e);
        foreach (var s in slideItems)
        {
            if (s.rt == null) continue;
            s.rt.anchoredPosition = s.basePos + off;
            if (s.cg != null) s.cg.alpha = Mathf.Clamp01(e * 1.4f);
        }
    }
}
