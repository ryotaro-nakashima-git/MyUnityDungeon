using UnityEngine;

/// <summary>
/// 🏷️ <b>アイコンの説明</b>（UI刷新 B-1）。hover で出す1枚ぶんの文章をここだけで持つ。
///
/// <para>
/// ⚠⚠ <b>絵と説明は分担する。</b>絵だけで完全に伝わる必要はない ――
///   <b>絵は「思い出す手がかり」、hover が「説明」</b>。だから絵を無理に情報量で膨らませず、
///   代わりに hover は<b>必ず4つ</b>を出す：<b>名前・一行の説明・費用/条件・ホットキー</b>。
///   4つのうち無いものは省くが、<b>順番は変えない</b>（毎回同じ場所に同じ物があるのが、覚えなくてよさの正体）。
/// </para>
///
/// <para>
/// ⚠ <b>ここに数字を書かない。</b>費用や残りは<b>その時の実際の値</b>を呼び側が差し込む
///   （`Tip(name, extra)` の `extra`）。ここに固定値を書くと、バランスを変えたときに
///   <b>説明だけが古いまま残る</b> ―― この作品で何度も起きた形。
/// </para>
///
/// 関連: [[IconFactory]]（絵） [[GameUIManager.Kit]] `AddTooltip`（出す側）。
/// </summary>
public static class IconCatalog
{
    /// <summary>hover に出す1枚。`extra` は「いまの値」（残り枠・費用・個数など）。</summary>
    public static string Tip(string name, string extra = null)
    {
        string body = Body(name);
        string key = Key(name);
        var sb = new System.Text.StringBuilder();
        sb.Append("<b>").Append(name).Append("</b>");
        if (!string.IsNullOrEmpty(body)) sb.Append("\n").Append(body);
        if (!string.IsNullOrEmpty(extra)) sb.Append("\n<color=#9c95b4>").Append(extra).Append("</color>");
        if (!string.IsNullOrEmpty(key)) sb.Append("\n<color=#6f6889>[").Append(key).Append("]</color>");
        return sb.ToString();
    }

    /// <summary>一行の説明。⚠ <b>1文で終える。</b>2文になったら、それは hover ではなく画面の仕事。</summary>
    private static string Body(string n)
    {
        switch (n)
        {
            case "戦略": return "迷宮を育てる窓をまとめて開く（魔王・感情・遺物・研究・拡張・先触れ・因縁）。";
            case "配置": return "盤に置くものをまとめて開く（罠・巣・環境・トーテム・部隊…）。";

            case "魔王": return "格・構え・捕食。BPを振って3つのステータスを伸ばす。";
            case "感情": return "冒険者から集めた感情を、4つの道に注ぐ木。";
            case "遺物": return "実績で手に入る持ち物。付け替えは準備フェーズだけ。";
            case "研究": return "配下・罠・トーテム・装備を<b>作れるようにする</b>木。時代ごとに中身が入れ替わる。";
            case "拡張": return "階層を足す／広げる。⚠ 広げると名声が上がり、来る者が<b>人数も質も</b>増える。";
            case "先触れ": return "次の波に誰が来るか。読める深さは領域研究で伸びる。";
            case "因縁": return "取り逃がして名がついた冒険者。討つと世界に撒いた装備を取り返せる。";
            case "報告": return "腹心の報告 ―― 情勢と、いま打てる手。";
            case "記録": return "この周の戦績と、狙っている道の進み。";
            case "保存": return "いまの状態を書き出す／読み込む。";
            case "設定": return "音・速さ・表示。";

            case "トーテム": return "まわりのマスに効果を撒く。強化・弱体・罠や感情との連携。";
            case "罠": return "踏んだ相手にダメージと状態異常。盗賊はMPを払って解除してくる。";
            case "巣": return "戦闘中に配下を湧かせ続ける。<b>湧いた配下は配置枠を食わない</b>。";
            case "環境": return "巣の<b>2マス以内</b>に置くと湧き方が変わる（速く・多く・強く）。";
            case "巨大": return "5×5の空いた床が要る大構造。『練兵場』はその階の隊の枠を+1。";
            case "ボス": return "各階1体だけ。強化して大きく出る。倒されるまで魔王に手が届かない。";
            case "特殊敵": return "素材を払って置く、強力な単体。";
            case "宝箱": return "冒険者を呼び、開けられると等級ぶんの見返りが入る。⚠ 相手の装備も上がる。";
            case "部隊": return "この階の隊員を1体ずつ、好きなマスへ置く。";
            case "塞ぐ": return "通路の区間を壁に戻して<b>道のりを伸ばす</b>。";
            case "掘る": return "2つのマスの間をまっすぐ掘り抜く。";
            case "消去": return "置いたものを撤去する（右クリックでも可）。";
            case "等級": return "宝箱に入れる装備の等級を階ごとに決める。<b>撒いた最高等級が世界の上限</b>。";
            case "布陣": return "未配置の隊員を、入口から最深部への<b>関所</b>へ一括で置く。";
            case "魔物": return "図鑑・系統樹・個体・隊・召喚の儀。召喚と進化と装備はここ。";

            case "DP": return "魔力点。配置・召喚・鍛造に使う。";
            case "素材": return "鍛造と『実戦の反芻』に使う。";
            case "研究点": return "研究を進める。毎ターン、魔王の知識ランクぶん貯まる。";
            case "名声": return "迷宮の名。高いほど強い者が大挙して来る（旨いが危険）。";
            case "生産": return "拠点がターンをかけて物を作る力。";
            case "枠": return "この階に置ける物の総数。配下も罠もトーテムも<b>同じ枠</b>を使う。";
            case "脅威": return "世界の脅威度。<b>逃げ延びた者だけ</b>が広める。人数と強さに効く。";
        }
        return null;
    }

    /// <summary>ホットキー。⚠ `Hotkeys` の割り当てと必ず揃える（ずれると嘘になる）。</summary>
    private static string Key(string n)
    {
        switch (n)
        {
            case "魔物": return "Z";
            case "研究": return "X";
            case "魔王": return "C";
            case "遺物": return "R";
            case "拡張": return "T";
            case "先触れ": return "V";
        }
        return null;
    }
}
