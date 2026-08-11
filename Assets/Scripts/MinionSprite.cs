using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🧟 配下の見た目（種類ごとの1枚絵）。`Resources/DungeonTale/Chars/char_&lt;id&gt;.png` を引く。
///
/// **なぜ要るか**：`MinionCatalog` は34種あるのに、**不死12種は全部同じ骸骨**、
/// **獣10種は割当なし**（手続きリグ）で、名前と姿が一致していなかった。
/// 種類ごとの絵を用意したので、`MinionCatalog` の id をそのまま鍵にして引けるようにする。
///
/// ⚠ 絵が無い種は `null` を返す。呼ぶ側は**従来の見た目へフォールバック**すること
///   （作りかけの段階でも絵が無い種が消えない）。
/// 関連: [[MinionCatalog]] [[CharacterVisual]] [[DungeonTale]]／対応表 docs/sprite-manifest.json。
/// </summary>
public static class MinionSprite
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// <summary>種のid（例 "death_knight"）で引く。無ければ null。</summary>
    public static Sprite ById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        Sprite s;
        if (cache.TryGetValue(id, out s)) return s;
        s = Resources.Load<Sprite>("DungeonTale/Chars/char_" + id);
        cache[id] = s;
        return s;
    }

    /// <summary>`MinionCatalog` の index で引く。</summary>
    public static Sprite ByIndex(int minionIndex)
    {
        // 👾 ユニークは `Chars/char_<id>` を同じ規則で引く（絵が無ければ null＝呼ぶ側が素体に落ちる）
        if (UniqueCatalog.IsUnique(minionIndex)) return ById(UniqueCatalog.GetByGlobal(minionIndex).id);
        if (minionIndex < 0 || minionIndex >= MinionCatalog.Count) return null;
        return ById(MinionCatalog.Get(minionIndex).id);
    }

    /// <summary>絵が用意できている種の数（進捗の確認用）。</summary>
    public static int ReadyCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < MinionCatalog.Count; i++) if (ByIndex(i) != null) n++;
            return n;
        }
    }
}
