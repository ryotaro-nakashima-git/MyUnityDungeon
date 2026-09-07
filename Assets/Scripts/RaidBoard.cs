using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🗿 <b>遠征先の盤</b>（④-c）── 他所のダンジョンを、こちらの迷宮とは別の場所に建てる。
///
/// ⚠⚠ <b>こちらの盤には絶対に触らない。</b>
///   `DungeonFloorManager.BuildBoard` → `DungeonGridSystem.BuildFromMap` は
///   <b>その階の配置を消す</b>（あちらのコードにも「階層を1つ足しただけで既存の階の配置が全部消える」
///   という警告が残っている）。遠征のたびにプレイヤーが組んだ迷宮が消えては話にならない。
///   → 階層が世界Yで `FloorSpacing`(=200) ずつ離れている仕組みをそのまま使い、
///     <b>遠く離れた階層 index（<see cref="FloorBase"/> 以降）に建てる</b>。
///     こちらの階は 0〜数枚しか無いので、100 以降とは決して重ならない。
///
/// ⚠ <b>敵の守りにこちらの強化を掛けない。</b> `DungeonFeatureManager.SpawnDefender` は
///   トーテム・遺物・魔王の格・政策・興奮ツリーを全部掛けている（＝<b>こちらの迷宮の守り</b>用）。
///   遠征先の守りにあれを使うと、<b>敵がこちらの投資で強くなる</b>。ここでは
///   スナップショットが持っている値だけで組む。
///
/// 関連: [[DungeonSnapshot]] [[Expedition]] [[NestSystem]] [[DungeonGridSystem]]。
/// </summary>
public static class RaidBoard
{
    /// <summary>遠征用の盤を建てる階層 index の起点。⚠ こちらの階と重ならない値にすること。</summary>
    public const int FloorBase = 100;

    private static readonly List<GameObject> boards = new List<GameObject>();
    private static readonly List<GameObject> actors = new List<GameObject>();
    private static DungeonSnapshot built;

    public static bool Active => built != null;
    public static DungeonSnapshot Built => built;
    public static int FloorIndexOf(int snapFloor) => FloorBase + snapFloor;

    /// <summary>
    /// スナップショットから盤を建てる。⚠ <b>地形は seed から組み直す</b>ので、
    /// スナップショットはタイルを持たなくてよい（セーブも通信も軽い）。
    /// </summary>
    public static bool Build(DungeonSnapshot s)
    {
        Teardown();
        if (s == null || s.FloorCount <= 0) return false;

        var gen = Object.FindFirstObjectByType<DungeonGenerator>();
        var b1 = DungeonGridSystem.Of(0) ?? DungeonGridSystem.Active;
        if (gen == null || b1 == null)
        { Debug.LogWarning("⚠️ 遠征の盤を建てられない（生成器かB1Fの盤が見つからない）"); return false; }

        // ⚠ 盤の生成は `Random.InitState` で全体の乱数を固定してしまうので、必ず戻す
        var st = Random.state;
        for (int f = 0; f < s.FloorCount; f++)
        {
            int fi = FloorIndexOf(f);
            // ⚠ B1Fの盤を**複製**して作る（タイルやガイドのプレハブ参照をインスペクタから引き継ぐため。
            //   `new GameObject` では作れない ―― `DungeonFloorManager.EnsureBoards` と同じ理由）。
            var clone = Object.Instantiate(b1.gameObject, b1.transform.parent);
            clone.name = "RaidBoard_F" + (f + 1);
            var g = clone.GetComponent<DungeonGridSystem>();
            g.SetFloorIndex(fi);
            g.ClearAllTilesAndGuides();   // ⚠ 複製元の絵が B1F の座標に残っているので必ず消す
            DungeonGridSystem.SetActive(g);   // 登録（`Of(fi)` で引けるようにする）

            gen.SetSeed(s.seed + f * 977);    // 階ごとに違う地形／同じ seed なら誰の環境でも同じ
            var fd = gen.BuildFloorData(s.floorSizes[f]);
            g.SetPlayableSize(fd.size);
            g.BuildFromMap(fd.map, fd.entrance, fd.boss, fd.tint, false);   // ⚠ 魔王の実体は置かない（主は自前で置く）
            boards.Add(clone);
        }
        gen.SetSeed(0);                      // 種を使わない状態に戻す（こちらの迷宮の生成を固定しないため）
        Random.state = st;

        built = s;
        Debug.Log("🗿『遠征の盤』" + s.name + " を " + s.FloorCount + "層ぶん建てた（階層 index "
            + FloorBase + "〜" + (FloorBase + s.FloorCount - 1) + "／こちらの盤には触れていない）");
        return true;
    }

    /// <summary>
    /// その階の守りを立てる。⚠ <b>スナップショットの値だけ</b>で組む
    ///（こちらのトーテム・遺物・政策を掛けない）。
    /// </summary>
    public static int SpawnGuards(DungeonSnapshot s, int snapFloor)
    {
        if (s == null) return 0;
        int fi = FloorIndexOf(snapFloor);
        var g = DungeonGridSystem.Of(fi);
        if (g == null) return 0;

        var input = Object.FindFirstObjectByType<GridInputHandler>();
        var prefab = input != null ? input.ZombiePrefab : null;
        if (prefab == null) { Debug.LogWarning("⚠️ 配下のプレハブが見つからない"); return 0; }

        // 置ける床を集める（入口の周りは避ける＝入った瞬間に囲まれない）
        var cells = new List<Vector2Int>();
        for (int x = 0; x < g.MapWidth; x++)
            for (int y = 0; y < g.MapHeight; y++)
            {
                if (g.GetTileType(x, y) == DungeonGridSystem.TileType.None) continue;
                if (Mathf.Abs(x - g.EntranceCell.x) + Mathf.Abs(y - g.EntranceCell.y) < 3) continue;
                cells.Add(new Vector2Int(x, y));
            }
        if (cells.Count == 0) return 0;

        var st = Random.state;
        Random.InitState(s.seed + snapFloor * 31 + 5);
        int n = 0;
        for (int i = 0; i < s.guardIndex.Count; i++)
        {
            if (s.guardFloor[i] != snapFloor) continue;
            var cell = cells[Random.Range(0, cells.Count)];
            if (SpawnOne(prefab, g, cell, s.guardIndex[i], s.guardLevel[i], 1f, false) != null) n++;
        }
        // 👑 最深部の主
        if (snapFloor == s.FloorCount - 1)
        {
            if (SpawnOne(prefab, g, g.BossCell, s.lordIndex, s.lordLevel, s.lordHpMult, true) != null) n++;
        }
        Random.state = st;
        Debug.Log("🗿『遠征先の守り』" + (snapFloor + 1) + "層に " + n + " 体を立てた");
        return n;
    }

    private static ZombieAI SpawnOne(GameObject prefab, DungeonGridSystem g, Vector2Int cell,
                                     int minionIndex, int level, float hpMult, bool isLord)
    {
        var def = MinionCatalog.Get(minionIndex);
        var go = Object.Instantiate(prefab, g.GridToWorld(cell.x, cell.y), Quaternion.identity);
        var z = go.GetComponent<ZombieAI>();
        if (z == null) { Object.Destroy(go); return null; }
        z.BindFloor(g.FloorIndex);
        z.species = def.family;
        z.minionIndex = minionIndex;
        z.role = def.role;
        // ⚠ **こちらの強化は一切掛けない。** 掛かる値は「種の倍率 × その個体のLv × 主の硬さ」だけ。
        float lv = MinionRoster.LevelMult(level);
        z.hpMult = def.hpMult * lv * hpMult;
        z.atkMult = def.atkMult * lv * (isLord ? 1.4f : 1f);
        z.speedMult = def.spdMult;
        z.isGuardian = false;
        z.anchored = true; z.anchorCell = cell; z.leashRadius = isLord ? 3 : 5;
        z.overrideTint = true;
        z.tintColor = isLord ? new Color(1f, 0.55f, 0.45f) : Color.white;
        if (isLord) go.transform.localScale = go.transform.localScale * 1.6f;
        actors.Add(go);
        return z;
    }

    /// <summary>
    /// ⚔️ 侵入者（こちらの遠征隊）をその階の入口に立たせる。
    /// ⚠ `AdventurerAI` をそのまま使う ―― 役が入れ替わるだけなので、戦闘の中身は書き換えない
    ///   （→ [[AdventurerAI]] の侵入者モード）。
    /// </summary>
    public static int SpawnRaiders(DungeonSnapshot s, int snapFloor, List<int> individualIds)
    {
        if (s == null || individualIds == null) return 0;
        int fi = FloorIndexOf(snapFloor);
        var g = DungeonGridSystem.Of(fi);
        if (g == null) return 0;

        var spawner = Object.FindFirstObjectByType<DungeonAdventurerSpawner>();
        var prefab = spawner != null ? spawner.AdventurerPrefab : null;
        if (prefab == null) { Debug.LogWarning("⚠️ 冒険者のプレハブが見つからない（侵入者を立てられない）"); return 0; }

        var ent = g.EntranceCell;
        int n = 0;
        for (int i = 0; i < individualIds.Count; i++)
        {
            var v = MinionRoster.Get(individualIds[i]);
            if (v == null) continue;
            // 入口の周りに散らす（全員同じマスに重ねない）
            var cell = ent;
            for (int tries = 0; tries < 12; tries++)
            {
                var c = new Vector2Int(ent.x + Random.Range(-2, 3), ent.y + Random.Range(-2, 3));
                if (c.x < 0 || c.y < 0 || c.x >= g.MapWidth || c.y >= g.MapHeight) continue;
                if (g.GetTileType(c.x, c.y) == DungeonGridSystem.TileType.None) continue;
                cell = c; break;
            }
            var go = Object.Instantiate(prefab, g.GridToWorld(cell.x, cell.y), Quaternion.identity);
            var ai = go.GetComponent<AdventurerAI>();
            if (ai == null) { Object.Destroy(go); continue; }
            // ⚠ `Start` より前に決める（階と侵入者モードのどちらも）
            ai.BindFloor(fi);
            ai.MakeRaider(individualIds[i]);
            actors.Add(go);
            n++;
        }
        Debug.Log("⚔️『突入』" + n + " 体が " + (snapFloor + 1) + "層の入口に立った");
        return n;
    }

    /// <summary>
    /// 🏁 その階を抜けたか＝<b>侵入者の誰かが最深部（ボス地点）に着いた</b>か。
    ///
    /// ⚠⚠ <b>「守りを全滅させたら」にしてはいけない。</b> 守りはアンカーで散らばっていて、
    ///   侵入者は最深部を目指して歩く。実測で <b>隅に1体だけ残った守りに誰も近づかず、
    ///   3,500フレーム経っても永久に終わらなかった</b>。
    ///   こちらの迷宮でも「冒険者が階段に着いたら降りる」であって「守りを全滅させたら」ではない。
    ///   守りは<b>関所であって、消化すべき一覧ではない</b>。
    /// </summary>
    public static bool FloorCleared(int snapFloor)
    {
        int fi = FloorIndexOf(snapFloor);
        var g = DungeonGridSystem.Of(fi);
        if (g == null) return false;
        var goal = g.BossCell;
        for (int i = 0; i < actors.Count; i++)
        {
            var go = actors[i];
            if (go == null) continue;
            var a = go.GetComponent<AdventurerAI>();
            if (a == null || !a.IsRaider) continue;
            if (DungeonGridSystem.FloorAtWorld(go.transform.position) != fi) continue;
            if (Mathf.Abs(a.CurrentGridPos.x - goal.x) + Mathf.Abs(a.CurrentGridPos.y - goal.y) <= 1) return true;
        }
        return false;
    }

    /// <summary>その階の侵入者を引き上げさせる（次の階へ降ろすとき）。</summary>
    public static void ClearRaidersOn(int snapFloor)
    {
        int fi = FloorIndexOf(snapFloor);
        for (int i = actors.Count - 1; i >= 0; i--)
        {
            var go = actors[i];
            if (go == null) { actors.RemoveAt(i); continue; }
            var a = go.GetComponent<AdventurerAI>();
            if (a == null || !a.IsRaider) continue;
            if (DungeonGridSystem.FloorAtWorld(go.transform.position) != fi) continue;
            Kill(go); actors.RemoveAt(i);
        }
    }

    /// <summary>その階に生き残っている侵入者の数。</summary>
    public static int RaidersAlive(int snapFloor)
    {
        int fi = FloorIndexOf(snapFloor);
        int n = 0;
        for (int i = 0; i < actors.Count; i++)
        {
            var go = actors[i];
            if (go == null) continue;
            var a = go.GetComponent<AdventurerAI>();
            if (a == null || !a.IsRaider) continue;
            if (DungeonGridSystem.FloorAtWorld(go.transform.position) == fi) n++;
        }
        return n;
    }

    /// <summary>その階にまだ立っている守りの数。</summary>
    public static int GuardsAlive(int snapFloor)
    {
        int fi = FloorIndexOf(snapFloor);
        int n = 0;
        for (int i = 0; i < actors.Count; i++)
        {
            var go = actors[i];
            if (go == null) continue;
            var z = go.GetComponent<ZombieAI>();
            if (z == null || z.IsDead) continue;
            if (DungeonGridSystem.FloorAtWorld(go.transform.position) == fi) n++;
        }
        return n;
    }

    /// <summary>
    /// 🧹 片付ける。⚠ <b>盤ごと消す。</b>残すと次の遠征で階層 index がぶつかり、
    /// `Of(100)` が古い盤を返して「前の遠征先の地形の上で戦う」ことになる。
    /// </summary>
    public static void Teardown()
    {
        for (int i = 0; i < actors.Count; i++) Kill(actors[i]);
        actors.Clear();

        // ⚠⚠ **控えた一覧だけでは足りない。** 遠征の盤の上では、こちらが立てた以外にも駒が生まれる ――
        //   不死がとどめを刺されると `DungeonFeatureManager.RaiseUndead` が骸を1体起こすし、
        //   倒れた守りは「復活できる」状態で実体が残る。
        //   実測：盤を片付けたのに **守りが9体、盤の無い階に残り続けた**。
        //   → 一覧ではなく **階層で掃く**。遠征の盤の階（100以降）に居るものは全部片付ける。
        var zs = Object.FindObjectsByType<ZombieAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < zs.Length; i++)
            if (zs[i] != null && DungeonGridSystem.FloorAtWorld(zs[i].transform.position) >= FloorBase)
                Kill(zs[i].gameObject);
        var advs = Object.FindObjectsByType<AdventurerAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < advs.Length; i++)
            if (advs[i] != null && (advs[i].IsRaider
                || DungeonGridSystem.FloorAtWorld(advs[i].transform.position) >= FloorBase))
                Kill(advs[i].gameObject);

        for (int i = 0; i < boards.Count; i++) Kill(boards[i]);
        boards.Clear();
        built = null;
    }

    /// <summary>
    /// ⚠⚠ <b>エディタ（再生していないとき）では `Destroy` が効かない。</b>
    ///   次のフレームまで遅延するのに、編集中はフレームが進まないので**消えないまま残る**。
    ///   実測：編集中に建てた遠征の盤が片付かず、<b>そのまま再生に持ち込まれた</b>
    ///   （floor 100/101 の盤が再生開始時に居座っていた）。道具から叩くコードでは必ず分ける。
    /// </summary>
    private static void Kill(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Object.Destroy(go);
        else Object.DestroyImmediate(go);
    }
}
