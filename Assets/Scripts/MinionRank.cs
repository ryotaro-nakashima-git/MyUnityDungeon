using UnityEngine;

/// <summary>
/// 👑 <b>格と位</b> ── 原作の接頭語（ハイ／グレーター／アーク／タイラント）と位（ロード…）。
///
/// ⚠⚠ <b>進化との違いがこの層の全部。</b>
///   <b>進化</b>＝研究とDPを払って<b>買う</b>。姿と種が変わる。買い直せる。プレイヤーが枝を選ぶ。
///   <b>格</b>  ＝事績と武功で<b>使って</b>上がる。同じ個体のまま名前に称号がつく。<b>選べない</b>。
///            そして <b>その個体が死ぬと全部消える</b>。
///   ここが効くと「育てた1体を地上へ出すか、迷宮で守らせるか」が毎ターンの判断になる。
///
/// <b>三つの鍵で上がる</b>
///   ① <b>研究</b>     … その格の天井を開ける（全体に1回）
///   ② <b>開放条件</b> … <b>その個体が</b>果たした事績（段3から）
///   ③ <b>武功</b>     … 実際に使って貯める。⚠ <b>待機では1点も入らない</b>（経験値との決定的な違い）
///
/// ⚠⚠ <b>いまの天井は段5（ロード）。</b> 段6（キング／クイーン）と段7（エンペラー）の開放条件は
///   「他のダンジョンを制覇した」「他の魔王を討った」で、<b>その仕組みがまだ無い</b>。
///   先に段を開けると<b>永久に取れないノード</b>になる（大罪の刻印を終末時代のゲートに
///   繋ぎかけて止めたのと同じ罠）。盤の上のダンジョンを作ったら <see cref="Cap"/> を 7 に上げる。
///
/// 関連: [[MinionRoster]] [[MinionEvolution]] [[KinRoster]] [[MinionSkill]] [[rank-realm-nest-magic]]。
/// </summary>
public static class MinionRank
{
    // ============ 段 ============
    public const int None = 0;
    public const int High = 1;      // ハイ
    public const int Greater = 2;   // グレーター
    public const int Arch = 3;      // アーク
    public const int Tyrant = 4;    // タイラント
    public const int Lord = 5;      // ロード（ここからが「位」）
    public const int King = 6;      // キング／クイーン（排他）
    public const int Emperor = 7;   // エンペラー

    /// <summary>
    /// いま到達できる一番上の段。⚠ <b>③④が出来るまで 5 のまま。</b>
    /// 上げるのは「盤の上のダンジョン」を実装したときだけ。
    /// </summary>
    public const int Cap = Lord;
    public const int MaxDefined = Emperor;

    /// <summary>段の呼び名。</summary>
    public static string Name(int step)
    {
        switch (step)
        {
            case High: return "ハイ";
            case Greater: return "グレーター";
            case Arch: return "アーク";
            case Tyrant: return "タイラント";
            case Lord: return "ロード";
            case King: return "キング";
            case Emperor: return "エンペラー";
            default: return "";
        }
    }

    /// <summary>段5以上は「位」＝<b>名前の後ろ</b>に付く（ゴブリン・ロード）。段4までは接頭語。</summary>
    public static bool IsCrown(int step) => step >= Lord;

    /// <summary>その段を開ける研究。</summary>
    public static string ResearchOf(int step)
    {
        switch (step)
        {
            case High: return "m_rank_high";
            case Greater: return "m_rank_greater";
            case Arch: return "m_rank_arch";
            case Tyrant: return "m_rank_tyrant";
            case Lord: return "m_crown_lord";
            case King: return "m_crown_king";
            case Emperor: return "m_crown_emperor";
            default: return "";
        }
    }

    /// <summary>その段に要る武功。取るほど重くなる。</summary>
    public static int DeedNeed(int step)
    {
        switch (step)
        {
            case High: return 10;
            case Greater: return 30;
            case Arch: return 70;
            case Tyrant: return 140;
            case Lord: return 240;
            case King: return 380;
            default: return 560;
        }
    }

    // ============ 🏅 開放条件（その個体が果たした事績） ============
    // ⚠ ビットはセーブに載る（`Individual.deedFlags`）。**値を変えない／末尾に足す。**
    public const int FlagSurfaceKill = 1 << 0;   // 地上で敵ユニットを倒した
    public const int FlagRazedTown = 1 << 1;   // 敵の集落を滅ぼした
    public const int FlagRaidedNest = 1 << 2;   // 他のダンジョンを制覇した（③④で立つ）
    public const int FlagSlewLord = 1 << 3;   // 他の魔王を討ち取った（④で立つ）

    /// <summary>迷宮で倒した冒険者の数（段3の門）。</summary>
    public const int KillsForArch = 10;

    /// <summary>その段の開放条件を満たしているか。段1・2は条件なし。</summary>
    public static bool GateMet(MinionRoster.Individual v, int step)
    {
        if (v == null) return false;
        switch (step)
        {
            case Arch: return v.kills >= KillsForArch;
            case Tyrant: return (v.deedFlags & FlagSurfaceKill) != 0;
            case Lord: return (v.deedFlags & FlagRazedTown) != 0;
            case King: return (v.deedFlags & FlagRaidedNest) != 0;
            case Emperor: return (v.deedFlags & FlagSlewLord) != 0;
            default: return true;
        }
    }

    /// <summary>開放条件を人の言葉で（UIとツールチップ用）。</summary>
    public static string GateText(int step)
    {
        switch (step)
        {
            case Arch: return "迷宮で冒険者を" + KillsForArch + "体倒す";
            case Tyrant: return "地上で敵ユニットを1体以上倒す";
            case Lord: return "敵の集落を1つ以上滅ぼす";
            case King: return "他のダンジョンを1つ制覇する";
            case Emperor: return "他の魔王を1人討ち取る";
            default: return "";
        }
    }

    /// <summary>その段で<b>何が増えるか</b>。⚠ 割合ではなく「できることが1つ増える」形で書く。</summary>
    public static string GrantText(int step, MinionCatalog.Role role)
    {
        switch (step)
        {
            case High: return "装飾品をもう1つ着けられる";
            case Greater: return "研究を待たずに、自分の種族技（第2段階）が使える";
            case Arch:
                switch (role)
                {
                    case MinionCatalog.Role.Ranged: return "射程 +1";
                    case MinionCatalog.Role.Tank: return "技『不屈』（致死を一度だけ耐える）";
                    case MinionCatalog.Role.Melee: return "技『吸命』（与えた傷の一部を吸う）";
                    default: return "使える魔法の階級 +1";
                }
            case Tyrant: return "眷属化のレベル条件が外れる（地上へ出られる）";
            case Lord: return "統率 +12（率いられる配下が増える）";
            case King: return "麾下の軍団が兵科で常に有利側に立つ";
            case Emperor: return "この1体だけの固有の権能";
            default: return "";
        }
    }

    // ============ 判定（その個体がその段に届いているか） ============
    public static bool Has(MinionRoster.Individual v, int step) => v != null && v.rank >= step;
    public static bool Has(int individualId, int step) => Has(MinionRoster.Get(individualId), step);

    /// <summary>研究でその段が開いているか。</summary>
    public static bool Unlocked(int step)
    {
        string r = ResearchOf(step);
        return !string.IsNullOrEmpty(r) && ResearchState.IsResearched(r);
    }

    // ============ 🎖️ 武功を積む ============
    /// <summary>
    /// 武功を足し、届いていれば段を上げる。
    /// ⚠ <b>ここが唯一の入口。</b>呼び出し側に「足す→上がったか見る」を書かせない
    ///   （散らすと昇格の通知が二重に出たり、片方だけ天井を見忘れる）。
    /// </summary>
    public static void AddDeed(int individualId, int amount, string why)
    {
        var v = MinionRoster.Get(individualId);
        if (v == null || amount < 0) return;
        v.deed += amount;
        TryPromote(v, why);
    }

    /// <summary>
    /// 🔬 <b>研究が済んだ直後に呼ぶ。</b>もう条件を満たしている個体をその場で昇格させる。
    /// ⚠ これが無いと「『ハイの格』を研究したのに、次にその配下が誰かを倒すまで何も起きない」
    ///   という手応えの空白ができる（実測：武功50・撃破50 の個体が研究後も段0のままだった）。
    /// </summary>
    public static void RecheckAll()
    {
        var list = MinionRoster.All;
        for (int i = 0; i < list.Count; i++) TryPromote(list[i], "研究が済んだ");
    }

    /// <summary>🗡️ 迷宮で冒険者を倒した。⚠ ランクA以上は +3（強い相手ほど糧になる）。</summary>
    public static void OnDungeonKill(int individualId, int adventurerRank)
    {
        var v = MinionRoster.Get(individualId);
        if (v == null) return;
        v.kills++;
        AddDeed(individualId, adventurerRank >= 6 ? 3 : 1, "冒険者を倒した");
    }

    /// <summary>⚔️ 地上で敵ユニットを倒した（段4の門）。</summary>
    public static void OnSurfaceKill(int individualId)
    {
        var v = MinionRoster.Get(individualId);
        if (v == null) return;
        v.deedFlags |= FlagSurfaceKill;
        AddDeed(individualId, 5, "地上で敵軍を破った");
    }

    /// <summary>🔥 敵の集落を滅ぼした（段5の門）。</summary>
    public static void OnTownRazed(int individualId)
    {
        var v = MinionRoster.Get(individualId);
        if (v == null) return;
        v.deedFlags |= FlagRazedTown;
        AddDeed(individualId, 15, "敵の集落を滅ぼした");
    }

    /// <summary>届いている段まで上げる（1度に何段でも上がりうる）。</summary>
    private static void TryPromote(MinionRoster.Individual v, string why)
    {
        while (v.rank < Cap)
        {
            int next = v.rank + 1;
            if (!Unlocked(next)) break;
            if (v.deed < DeedNeed(next)) break;
            if (!GateMet(v, next)) break;
            v.rank = next;
            string nm = DisplayName(v);
            Debug.Log("👑『格が上がった』" + nm + "（" + why + "・武功" + v.deed + "）― "
                + GrantText(next, MinionCatalog.Get(v.catalogIndex).role));
            NotifySystem.Push("<b>" + nm + "</b> に成った ― " + GrantText(next, MinionCatalog.Get(v.catalogIndex).role),
                NotifySystem.Kind.Story);
        }
    }

    // ============ 🏷️ 名前 ============
    /// <summary>称号のついた呼び名。⚠ 段4までは前に、段5からは後ろに付く。</summary>
    public static string DisplayName(MinionRoster.Individual v)
    {
        if (v == null) return "";
        string baseName = MinionCatalog.Get(v.catalogIndex).jpName;
        return Decorate(baseName, v.rank);
    }
    public static string DisplayName(int individualId) => DisplayName(MinionRoster.Get(individualId));

    public static string Decorate(string baseName, int rank)
    {
        if (rank <= None) return baseName;
        string t = Name(rank);
        return IsCrown(rank) ? baseName + "・" + t : t + "・" + baseName;
    }

    /// <summary>段ごとの色（盤の縁と図鑑で使う）。位に入ると金になる。</summary>
    public static string ColorOf(int rank)
    {
        switch (rank)
        {
            case High: return "#8cb8e6";
            case Greater: return "#5cc47c";
            case Arch: return "#b478e6";
            case Tyrant: return "#e05a5a";
            default: return rank >= Lord ? "#e3a94a" : "#9c95b4";
        }
    }

    // ============ 👑 冠の印（段5〜7） ============
    /// <summary>
    /// 冠の絵。⚠ <b>段5からしか無い</b>（段1〜4は接頭語と縁の色だけで見せる ―― 印を全段に付けると
    /// 盤が記号だらけになり、「位に入った」という段差が消える）。
    /// 素材は PixelLab で描き起こした 64×64。→ `Assets/Resources/DungeonTale/Crowns/`
    /// </summary>
    private static Sprite[] crownCache;
    public static Sprite CrownSprite(int rank)
    {
        if (rank < Lord) return null;
        if (crownCache == null) crownCache = new Sprite[3];
        int i = Mathf.Clamp(rank - Lord, 0, 2);
        if (crownCache[i] == null)
        {
            // ⚠ 段6は キング／クイーン で絵が違うが、どちらを出すかは個体の `crown` で決まる。
            //   ここは既定（キング）を返す。個体つきは `CrownSprite(Individual)` を使うこと。
            string n = i == 0 ? "crown_lord" : i == 1 ? "crown_king" : "crown_emperor";
            crownCache[i] = Resources.Load<Sprite>("DungeonTale/Crowns/" + n);
        }
        return crownCache[i];
    }

    /// <summary>個体つき（段6でクイーンを選んでいれば、そちらの冠を返す）。</summary>
    public static Sprite CrownSprite(MinionRoster.Individual v)
    {
        if (v == null || v.rank < Lord) return null;
        if (v.rank == King && v.crown == 1) return Resources.Load<Sprite>("DungeonTale/Crowns/crown_queen");
        return CrownSprite(v.rank);
    }

    // ============ 🎁 段が配るもの ============
    /// <summary>💍 着けられる装飾品の数（ハイで2つになる）。</summary>
    public static int AccessorySlots(MinionRoster.Individual v) => Has(v, High) ? 2 : 1;

    /// <summary>
    /// 💫 グレーター＝<b>研究を待たずに自分の第2段階の技が使える</b>。
    /// ⚠ 「技の枠を1つ増やす」ではなく「解禁を先取りする」形にしてある。
    ///   枠制にすると、いま全部の技が有効な既存の配下が<b>弱くなる</b>（機能の削除になる）。
    /// </summary>
    public static bool FreesTier2Skill(int individualId) => Has(individualId, Greater);

    /// <summary>🏹 アークの射手＝射程 +1。</summary>
    public static float RangeBonus(int individualId, MinionCatalog.Role role)
        => (role == MinionCatalog.Role.Ranged && Has(individualId, Arch)) ? 1f : 0f;

    /// <summary>🔮 アークの術者＝使える魔法の階級 +1。</summary>
    public static int MagicRankBonus(int individualId, MinionCatalog.Role role)
        => (role != MinionCatalog.Role.Ranged && role != MinionCatalog.Role.Tank
            && role != MinionCatalog.Role.Melee && Has(individualId, Arch)) ? 1 : 0;

    /// <summary>🛡️⚔️ アークの前衛＝不屈／突撃＝吸命。</summary>
    public static MinionSkillKind ArchSkill(int individualId, MinionCatalog.Role role)
    {
        if (!Has(individualId, Arch)) return MinionSkillKind.None;
        if (role == MinionCatalog.Role.Tank) return MinionSkillKind.Undying;
        if (role == MinionCatalog.Role.Melee) return MinionSkillKind.Lifedrain;
        return MinionSkillKind.None;
    }

    /// <summary>👑 タイラント＝眷属化のレベル条件が外れる。</summary>
    public static bool IgnoresNamingLevel(int individualId) => Has(individualId, Tyrant);

    /// <summary>🎖️ ロード＝統率 +12。</summary>
    public static int LeadershipBonus(int individualId) => Has(individualId, Lord) ? 12 : 0;

    // ============ UI ============
    /// <summary>次の段まであといくつか（図鑑の1行）。天井に着いていれば空。</summary>
    public static string ProgressText(MinionRoster.Individual v)
    {
        if (v == null) return "";
        int next = v.rank + 1;
        if (next > Cap) return v.rank >= Cap ? "<color=#e3a94a>ここが今の頂点</color>" : "";
        if (!Unlocked(next)) return "研究『" + Name(next) + "』が要る";
        if (!GateMet(v, next))
        {
            string extra = next == Arch ? "（あと" + Mathf.Max(0, KillsForArch - v.kills) + "体）" : "";
            return Name(next) + "の条件：" + GateText(next) + extra;
        }
        int need = DeedNeed(next);
        return Name(next) + "まで 武功 " + v.deed + " / " + need;
    }
}
