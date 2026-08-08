using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 💢 戦闘のダメージ数字（Phase C-15）。ワールド空間に浮かんで消える文字を**プールで**出す。
///
/// **なぜ要るか**：これまで冒険者は `💥HP:1234`（＝残りHP）を1つの TextMesh で出していただけで、
/// - **与えたダメージが分からない**（罠が効いているのか、殴りが通っているのかが読めない）
/// - 1体につき1つしか出せず、**連続で殴ると前の表示が消える**
/// - 絵文字はフォントに無いと □ になる
/// 防衛体（こちら側）に至っては**何も出ていなかった**ので、戦闘がただの棒立ちに見えていた。
///
/// ⚠ `unscaledDeltaTime` では**なく** `deltaTime` で動かす。ここは戦闘の一部なので、
///    倍速なら速く、一時停止なら止まるのが正しい（UIのトーストとは逆）。
/// 関連: [[game-polish-plan]] [[NotifySystem]]（あちらは画面右のUI通知）。
/// </summary>
public static class FloatText
{
    public static TMP_FontAsset Font;      // GameUIManager が日本語フォントを渡す

    private class Item
    {
        public TextMeshPro tmp;
        public float life, total;
        public Vector3 from;
        public float rise;
    }

    private class Runner : MonoBehaviour
    {
        private void LateUpdate() { Tick(Time.deltaTime); }
    }

    private static Transform root;
    private static readonly List<Item> live = new List<Item>();
    private static readonly Stack<TextMeshPro> pool = new Stack<TextMeshPro>();

    private static void EnsureRoot()
    {
        if (root != null) return;
        var go = new GameObject("FloatText");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Runner>();
        root = go.transform;
    }

    /// <summary>数字や短い語を浮かせる。size はワールド単位のフォントサイズ。</summary>
    public static void Spawn(Vector3 worldPos, string text, Color color, float size = 2.6f, float rise = 0.9f, float life = 0.85f)
    {
        EnsureRoot();
        TextMeshPro t;
        if (pool.Count > 0) { t = pool.Pop(); t.gameObject.SetActive(true); }
        else
        {
            var go = new GameObject("Ft");
            go.transform.SetParent(root, false);
            t = go.AddComponent<TextMeshPro>();
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.raycastTarget = false;
            t.fontStyle = FontStyles.Bold;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) { mr.sortingOrder = 500; mr.sortingLayerName = "Default"; }
        }
        if (Font != null && t.font != Font) t.font = Font;
        t.fontSize = size;
        t.color = color;
        t.text = text;
        t.transform.position = worldPos;
        t.transform.localScale = Vector3.one;
        live.Add(new Item { tmp = t, life = life, total = life, from = worldPos, rise = rise });
    }

    /// <summary>💥 ダメージ（赤系）。大きいほど文字も大きい＝効いているのが一目で分かる。</summary>
    public static void Damage(Vector3 pos, float amount, bool crit = false)
    {
        int v = Mathf.Max(1, Mathf.RoundToInt(amount));
        float size = Mathf.Clamp(2.2f + Mathf.Log10(1f + v) * 0.9f, 2.2f, 5.0f);
        Spawn(pos, (crit ? "" : "") + v.ToString(),
            crit ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.42f, 0.38f), size);
    }

    /// <summary>🩹 回復（緑）。</summary>
    public static void Heal(Vector3 pos, float amount)
    {
        Spawn(pos, "+" + Mathf.Max(1, Mathf.RoundToInt(amount)), new Color(0.42f, 0.85f, 0.5f), 2.4f);
    }

    private static void Tick(float dt)
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var it = live[i];
            if (it.tmp == null) { live.RemoveAt(i); continue; }
            it.life -= dt;
            if (it.life <= 0f)
            {
                it.tmp.gameObject.SetActive(false);
                pool.Push(it.tmp);
                live.RemoveAt(i);
                continue;
            }
            float k = 1f - it.life / it.total;                  // 0→1
            it.tmp.transform.position = it.from + new Vector3(0f, it.rise * k, -0.2f);
            // 出るときに少し弾ませ、消えるときに薄くする
            float pop = k < 0.18f ? Mathf.Lerp(0.6f, 1.12f, k / 0.18f) : Mathf.Lerp(1.12f, 1f, Mathf.InverseLerp(0.18f, 0.4f, k));
            it.tmp.transform.localScale = Vector3.one * pop;
            var c = it.tmp.color; c.a = k > 0.6f ? Mathf.InverseLerp(1f, 0.6f, k) : 1f; it.tmp.color = c;
        }
    }
}
