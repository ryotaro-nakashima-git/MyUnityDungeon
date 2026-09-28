using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 💍 装飾品の1枚絵。`Resources/Accessories/acc_&lt;id&gt;.png` を引く（PixelLab・48×48・透明背景）。
///
/// <para>
/// ⚠⚠ **なぜ要るか**：14種の装飾品は**絵が1枚も無く**、店でも個体の欄でも
///   「色つきの名前」だけだった。何を買ったのか・何を着けているのかが**見た目で分からない**。
///   店は「並んでいる物を見て選ぶ」場所なので、絵が無い店は棚ではなく表になる。
/// </para>
///
/// <para>
/// ⚠ 絵が無い種は `null` を返す。呼ぶ側は**従来の見た目へフォールバック**すること
///   （作りかけでも枠が空にならない）。`MinionSprite` と同じ約束。
/// ⚠ 鍵は `AccessoryCatalog` の `id`。並び順（index）はセーブに載っているので**鍵にしない**。
/// </para>
///
/// 関連: [[MinionSprite]]（同じ作り） [[AccessoryCatalog]] [[MerchantShop]]／[[pixellab-pipeline]]。
/// </summary>
public static class AccessorySprite
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// <summary>種のid（例 "thorn_mail"）で引く。無ければ null。</summary>
    public static Sprite ById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        Sprite s;
        if (cache.TryGetValue(id, out s)) return s;
        s = Resources.Load<Sprite>("Accessories/acc_" + id);
        cache[id] = s;
        return s;
    }

    /// <summary>`AccessoryCatalog` の index で引く。⚠ -1（なし）は null。</summary>
    public static Sprite ByIndex(int i)
    {
        if (i < 0 || i >= AccessoryCatalog.Count) return null;
        return ById(AccessoryCatalog.Get(i).id);
    }

    /// <summary>絵が用意できている種の数（進捗の確認用）。</summary>
    public static int ReadyCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < AccessoryCatalog.Count; i++) if (ByIndex(i) != null) n++;
            return n;
        }
    }
}

/// <summary>
/// 🖼️ 場面の背景（PixelLab・400×228〜240）。`Resources/Scenes/bg_&lt;id&gt;.png`。
/// ⚠ 中央と棚の位置には**何も描かれていない**。品物も魔法陣もUI側で上に重ねるため
///   ―― 描き込むと二重になる（生成時の指示にも入れてある）。
/// </summary>
public static class SceneArt
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    public static Sprite Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        Sprite s;
        if (cache.TryGetValue(id, out s)) return s;
        s = Resources.Load<Sprite>("Scenes/bg_" + id);
        cache[id] = s;
        return s;
    }
    public static Sprite Ritual { get { return Get("ritual"); } }
    public static Sprite Merchant { get { return Get("merchant"); } }
}
