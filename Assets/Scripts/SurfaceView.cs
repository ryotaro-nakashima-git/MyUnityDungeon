using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 🌍 地上盤を **Unity のシーンそのもの**（ワールド空間の1枚メッシュ）で描く層。W2。
///
/// **なぜ作り直したか**: uGUIで描いていたとき、1タイルにつき GameObject 16個（Graphic13/TMP3.5）を作り、
/// しかも領域を1つ選ぶたびに全部Destroyして作り直していた。実測で 271タイル=61ms、
/// 1万タイル換算で**16万GameObject / 約2.3秒**。データ層は1万タイルでも0〜17msで動くので、
/// 壁はここだけだった。→ [[civ7-roadmap]] の W2。
///
/// **なぜ Tilemap ではなく自前メッシュか**:
/// - Unityのヘクス Tilemap は cellSwizzle と point-top/flat-top の対応が紛らわしく、[[HexGrid]] の
///   座標をそのまま使えない。自前なら `HexGrid.WorldPos` をそのまま置ける。
/// - **厚み（側面）の重なり順**を三角形の並び順で確実に制御できる（奥の行から順に積む＝画家のアルゴリズム）。
/// - 1タイル4頂点なので、画面に入る数百〜数千タイルでも1メッシュに収まる。
///
/// 描くのは**画面に入っているタイルだけ**。カメラを動かすたびに作り直すが、頂点を詰め直すだけなので速い。
/// 文字は Civ と同じく「**寄っているときだけ・画面内だけ**」に絞ってプールから貸し出す。
/// </summary>
public class SurfaceView : MonoBehaviour
{
    public const float TileSize = 0.5f;                 // ヘクスの外接円半径（ワールド単位）
    private const float Squash = HexTileArt.Squash;
    private const float RowStep = TileSize * 1.5f * Squash;
    private const float ColStep = TileSize * 1.7320508f;
    private const float QuadW = ColStep;
    private static float QuadH => TileSize * 2f * Squash + TileSize * 2f * Squash * HexTileArt.Depth / HexTileArt.HexH;

    public Camera cam;
    public System.Action<int> onPick;                    // タイルを選んだ
    public System.Action onViewChanged;
    /// <summary>⚠ UIと同じフォントを渡すこと。TMPの既定フォントは日本語を持たず、地名が全部□になる。</summary>
    public TMP_FontAsset font;

    private Mesh mesh;
    private MeshRenderer mr;
    private Material mat;
    private Transform labelRoot;
    private readonly List<TextMeshPro> labelPool = new List<TextMeshPro>();
    private int labelUsed;

    private readonly List<Vector3> verts = new List<Vector3>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<Color32> cols = new List<Color32>();
    private readonly List<int> tris = new List<int>();

    private Vector3 dragOrigin; private bool dragging, dragged;
    // 初期ズーム。引きすぎると「少し動かすだけで世界を一周する」感じになるので寄り気味で始める
    // （zoom7だと中の盤で画面2.7枚ぶんで一周、5.5なら3.5枚ぶん）。
    private float zoom = 5.5f;                           // orthographicSize
    public const float ZoomMin = 3.5f;

    public static float WorldWidth => SurfaceMap.MapW * ColStep;
    public static float WorldHeight => SurfaceMap.MapH * RowStep;
    /// <summary>
    /// 引ける上限。**引き切ったとき世界がちょうど1つ収まる**ところで止める。
    /// ※これが無いと、東西ループのせいで同じ世界が横に何個も並ぶ（実測で最大9.4周ぶん映っていた）。
    /// </summary>
    private float MaxZoom
    {
        get
        {
            float a = (cam != null && cam.aspect > 0.01f) ? cam.aspect : 16f / 9f;
            return Mathf.Max(ZoomMin + 1f, Mathf.Min(WorldWidth / (2f * a), WorldHeight * 0.5f) * 1.02f);
        }
    }
    private int selectedId = -1;
    private bool dirty = true;

    // ============ 生成 ============
    public static SurfaceView Create(TMP_FontAsset uiFont)
    {
        var go = new GameObject("SurfaceView");
        var v = go.AddComponent<SurfaceView>();
        v.font = uiFont;
        v.Init();
        return v;
    }

    private int surfaceLayer;

    private void Init()
    {
        // 🧅 地上は**専用レイヤー**に置き、地上カメラはそのレイヤーだけを描く。
        //    ⚠ これが無いと cullingMask=-1 のまま迷宮のGameObjectまで描いてしまう。
        //      盤と迷宮は同じ座標帯に重なっているので、地上へ移った直後に**迷宮が映り込む**
        //      （実測で迷宮の描画物130個が地上カメラの視界に入っていた。パンして離れると消えるので
        //       「最初だけ映る」という出方になる）。
        surfaceLayer = LayerMask.NameToLayer("Surface");
        if (surfaceLayer < 0) surfaceLayer = 0;
        gameObject.layer = surfaceLayer;

        // 🎥 地上専用カメラ。迷宮のカメラは触らず、こちらを有効/無効で切り替える（迷宮の状態は保たれる）。
        var camGO = new GameObject("SurfaceCamera");
        camGO.transform.SetParent(transform, false);
        camGO.layer = surfaceLayer;
        cam = camGO.AddComponent<Camera>();
        cam.cullingMask = 1 << surfaceLayer;
        cam.orthographic = true;
        cam.orthographicSize = zoom;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.035f, 0.028f, 0.055f);
        cam.transform.position = new Vector3(0, 0, -50f);
        cam.depth = 1;

        var meshGO = new GameObject("Board");
        meshGO.transform.SetParent(transform, false);
        meshGO.layer = surfaceLayer;
        mesh = new Mesh { name = "SurfaceBoard" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;   // 大きい盤でも頂点数で詰まらない
        meshGO.AddComponent<MeshFilter>().sharedMesh = mesh;
        mr = meshGO.AddComponent<MeshRenderer>();
        var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
        mat = new Material(sh) { mainTexture = HexTileArt.Atlas };
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        labelRoot = new GameObject("Labels").transform;
        labelRoot.SetParent(transform, false);
        labelRoot.gameObject.layer = surfaceLayer;

        CenterOn(SurfaceMap.IndexOfCenter());
    }

    // ============ 座標 ============
    /// <summary>タイルの中心（ワールド）。**行が増えるほど下**へ置く＝奥から手前へ描ける。</summary>
    public static Vector3 PosOf(int col, int row)
        => new Vector3(ColStep * (col + 0.5f * (row & 1)), -RowStep * row, 0f);

    /// <summary>ワールド座標 →（col,row）。クリック判定はこれ1回で済むのでButtonが要らない。</summary>
    public static void CellAt(Vector3 p, out int col, out int row)
    {
        row = Mathf.RoundToInt(-p.y / RowStep);
        row = Mathf.Clamp(row, 0, Mathf.Max(0, SurfaceMap.MapH - 1));
        col = Mathf.RoundToInt(p.x / ColStep - 0.5f * (row & 1));
    }

    /// <summary>UIで隠れているぶん、注目タイルを**見えている側**へ寄せる係数（画面幅に対する割合）。</summary>
    public float FocusOffsetX;

    public void CenterOn(int regionId)
    {
        var r = SurfaceMap.Get(regionId);
        var p = PosOf(r.col, r.row);
        cam.transform.position = new Vector3(p.x + cam.orthographicSize * cam.aspect * FocusOffsetX, p.y, -50f);
        ClampCamera();
        dirty = true;
    }
    public void SetSelected(int id) { selectedId = id; dirty = true; }

    // ============ 💬 フローティングテキスト（Phase A-3） ============
    //  盤の上で「何が起きたか」をその場に出す。迷宮側の PopUpEmotionText と同じ役目。
    //  ⚠ 時間は unscaledDeltaTime で進める（戦闘の倍速/一時停止に引きずられないため）。
    private class Pop { public TextMeshPro t; public float life; public Vector3 from; }
    private readonly List<Pop> pops = new List<Pop>();

    public void PopText(int regionId, string text, string colorHex)
    {
        if (regionId < 0 || regionId >= SurfaceMap.Count) return;
        var r = SurfaceMap.Get(regionId);
        var go = new GameObject("Pop");
        go.transform.SetParent(labelRoot, false);
        go.layer = surfaceLayer;
        var t = go.AddComponent<TextMeshPro>();
        if (font != null) t.font = font;
        t.text = "<color=" + colorHex + ">" + text + "</color>";
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = 1.1f; t.fontStyle = FontStyles.Bold;
        t.enableWordWrapping = false; t.raycastTarget = false;
        var mr2 = go.GetComponent<MeshRenderer>(); if (mr2 != null) mr2.sortingOrder = 200;
        var p = PosOf(r.col, r.row);
        go.transform.position = new Vector3(p.x, p.y + TileSize * 0.3f, -2f);
        pops.Add(new Pop { t = t, life = 1.6f, from = go.transform.position });
    }

    private void TickPops()
    {
        for (int i = pops.Count - 1; i >= 0; i--)
        {
            var p = pops[i];
            p.life -= Time.unscaledDeltaTime;
            if (p.t == null || p.life <= 0f)
            {
                if (p.t != null) Destroy(p.t.gameObject);
                pops.RemoveAt(i); continue;
            }
            float k = 1f - p.life / 1.6f;                       // 0→1
            p.t.transform.position = p.from + new Vector3(0, k * TileSize * 0.9f, 0);
            var c = p.t.color; c.a = Mathf.Clamp01(p.life / 0.6f); p.t.color = c;
        }
    }
    public void MarkDirty() { dirty = true; }

    // ============ 入力（パン／ズーム／クリック） ============
    private void Update()
    {
        if (cam == null || !cam.enabled) return;
        HandleInput();
        TickPops();
        if (replayT < 1f)
        {
            replayT = Mathf.Min(1f, replayT + Time.unscaledDeltaTime / ReplayDur);
            dirty = true;      // 動いている間は毎フレーム描き直す
        }
        if (dirty) { Rebuild(); dirty = false; }
    }

    private void HandleInput()
    {
        // ※このプロジェクトは new Input System を使う（他のスクリプトと揃える）
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;
        Vector3 mp = mouse.position.ReadValue();
        mp.z = 10f;

        // ⚠⚠ **UIの上ではホイールを盤に渡さない**。これが無いと、研究ツリーやタブを
        //    スクロールしただけで**同時にマップの拡大縮小まで起きる**（実際に踏んだ）。
        //    掴んで動かす方（down）には元から同じ番があったのに、ホイールだけ素通りしていた。
        // ⚠ `IsPointerOverGameObject` **だけでは足りない**。GraphicRaycaster は
        //   `depth == -1`（まだ描画バッチに乗っていない）を飛ばすので、**開いた直後のパネルを拾えない**。
        //   矩形で直接見る `PointerOverSurfaceUI` を併用する。
        var es0 = UnityEngine.EventSystems.EventSystem.current;
        bool overUI = es0 != null && es0.IsPointerOverGameObject();
        if (!overUI)
        {
            var gui = GameUIManager.Instance;
            if (gui != null && gui.PointerOverSurfaceUI(mp)) overUI = true;
        }

        float scroll = mouse.scroll.ReadValue().y;      // 環境によって ±1 だったり ±120 だったりする
        if (!overUI && Mathf.Abs(scroll) > 0.01f)
        {
            float step = Mathf.Clamp(scroll * (Mathf.Abs(scroll) > 10f ? 0.0016f : 0.16f), -0.4f, 0.4f);
            zoom = Mathf.Clamp(zoom * (1f - step), ZoomMin, MaxZoom);
            cam.orthographicSize = zoom;
            ClampCamera();
            dirty = true;
            if (onViewChanged != null) onViewChanged();
        }

        bool down = mouse.leftButton.wasPressedThisFrame;
        bool held = mouse.leftButton.isPressed;
        bool up = mouse.leftButton.wasReleasedThisFrame;

        if (down)
        {
            if (overUI) return;   // UIの上なら盤は触らない
            dragging = true; dragged = false;
            dragOrigin = cam.ScreenToWorldPoint(mp);
        }
        if (held && dragging)
        {
            var now = cam.ScreenToWorldPoint(mp);
            var d = dragOrigin - now;
            if (d.sqrMagnitude > 0.0004f) dragged = true;
            cam.transform.position += new Vector3(d.x, d.y, 0f);
            ClampCamera();
            dirty = true;
        }
        if (up && dragging)
        {
            dragging = false;
            if (!dragged)      // ドラッグして離したときは選択しない（C1からのガードを踏襲）
            {
                var w = cam.ScreenToWorldPoint(mp);
                int col, row; CellAt(w, out col, out row);
                int id = SurfaceMap.IdAt(col, row);
                if (id >= 0 && onPick != null) onPick(id);
            }
        }
    }

    /// <summary>南北だけ端で止める（東西はループするので止めない＝どこまでも回れる）。</summary>
    private void ClampCamera()
    {
        var p = cam.transform.position;
        float half = TileSize * Squash;
        float top = half, bottom = -RowStep * (SurfaceMap.MapH - 1) - half;
        float halfH = cam.orthographicSize;
        // 盤より視界のほうが高いときは中央に固定（端の外の空白を見せない）
        p.y = (top - bottom <= halfH * 2f) ? (top + bottom) * 0.5f
                                           : Mathf.Clamp(p.y, bottom + halfH, top - halfH);
        cam.transform.position = new Vector3(p.x, p.y, -50f);
    }

    // ============ 描画（見えているところだけメッシュに詰める） ============
    /// <summary>🐾 選択中の眷属が今ターン行ける範囲（GameUIManagerが入れる。null＝出さない）。</summary>
    public HashSet<int> moveRange;

    // ⏭️ 敵軍の動きの再生（Phase C-14）。
    //    ターン解決は一瞬で終わるので、盤を開いたときに**前ターンの移動を1.1秒かけて見せる**。
    //    「じわじわ近づいてくる」のが見えないと、突然領域を奪われたようにしか感じられない。
    private float replayT = 1f;
    public const float ReplayDur = 1.1f;
    public void PlayEnemyReplay()
    {
        bool any = false;
        foreach (var a in EnemyForce.All)
            if (a.prevRegionId >= 0 && a.prevRegionId != a.regionId) { any = true; break; }
        if (!any) return;
        replayT = 0f; dirty = true;
    }
    public bool IsReplaying { get { return replayT < 1f; } }

    /// <summary>🚩 隣に「別の所有者」がいるか＝そこが国境。</summary>
    private static bool IsBorder(SurfaceMap.Region r)
    {
        foreach (var l in r.links)
        {
            var n = SurfaceMap.Get(l);
            if (n.owner != r.owner) return true;
        }
        return false;
    }

    /// <summary>所有者の色（自分＝緑／他魔王＝その色／中立は描かない）。</summary>
    private static Color32 OwnerColor(int owner)
    {
        if (owner == SurfaceMap.OwnerSelf) return new Color32(120, 240, 170, 255);
        Color c;
        if (ColorUtility.TryParseHtmlString(RivalLords.ColorOf(owner - SurfaceMap.OwnerRivalBase), out c))
            return new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255);
        return new Color32(200, 200, 200, 255);
    }

    // 👑 いまタイルの上に立っているもの（Civのユニットと同じで、位置が目で追えるようにする）
    //    ⚠ 文字（◆□×＋）で描いていたが、**フォントに無い字は□になる**うえ小さくて読めなかった。
    //       アトラスに焼いた絵に差し替える。
    // ⚠ 1マーク＝「台座（兵科や役割の記号）＋その上に重ねる姿」の2枚。
    //   別々のマークにすると横に並んでしまい、どの姿がどの台座のものか分からなくなる。
    private class UnitMark { public int atlas; public Color32 col; public int face; public Color32 faceCol; }
    private readonly Dictionary<int, List<UnitMark>> unitsAt = new Dictionary<int, List<UnitMark>>();
    private void AddMark(int regionId, int atlas, Color32 col, int face = -1, Color32 faceCol = default)
    {
        if (regionId < 0) return;
        List<UnitMark> l;
        if (!unitsAt.TryGetValue(regionId, out l)) { l = new List<UnitMark>(); unitsAt[regionId] = l; }
        if (faceCol.a == 0) faceCol = new Color32(255, 255, 255, 255);
        if (l.Count < 3) l.Add(new UnitMark { atlas = atlas, col = col, face = face, faceCol = faceCol });
    }

    private void AddUnits(int id, Vector3 p)
    {
        List<UnitMark> l;
        if (!unitsAt.TryGetValue(id, out l)) return;
        // 1体なら中央、2体以上なら少しずらして並べる
        for (int i = 0; i < l.Count; i++)
        {
            float dx = l.Count == 1 ? 0f : (i - (l.Count - 1) * 0.5f) * QuadW * 0.26f;
            var q = new Vector3(p.x + dx, p.y, p.z);
            AddOverlay(q, l[i].atlas, l[i].col, 0.55f, -TileSize * 0.10f);
            // 🧟 種の姿は台座の**上**に小さく載せる。
            // ⚠ 大きくすると同じタイルの施設を覆い隠す（実測で施設が見えなくなった）。
            if (l[i].face >= 0)
                AddOverlay(q, l[i].face, l[i].faceCol, l.Count == 1 ? 0.34f : 0.28f, TileSize * 0.10f);
        }
    }

    /// <summary>個体IDからその種（catalog index）を引く（-1＝不明）。</summary>
    private static int SpeciesOfIndividual(int individualId)
    {
        var v = MinionRoster.Get(individualId);
        return v != null ? v.catalogIndex : -1;
    }

    private void CollectUnits()
    {
        unitsAt.Clear();
        foreach (var k in KinRoster.All)
        {
            if (k.regionId < 0) continue;
            // 灰=負傷 / 金=進軍中 / 緑=待機
            var col = k.injuryTurns > 0 ? new Color32(156, 149, 180, 255)
                    : k.marchTarget >= 0 ? new Color32(255, 210, 74, 255)
                    : new Color32(140, 224, 168, 255);
            // 👑 眷属は**その配下の姿**で出す（盾の記号だと誰が誰だか分からない）。
            //    盾は状態の色を示す台座として下に残す。
            AddMark(k.regionId, HexTileArt.KinIndex, col, HexTileArt.MinionIndex(SpeciesOfIndividual(k.individualId)));
        }
        // ⚔️ 敵の軍（他魔王＝その色／人間の奪還軍＝白）
        //    ⏭️ 再生中で「動いた軍」は、タイルに紐づけず**補間した位置**に別で描く（DrawMovingArmies）
        foreach (var a in EnemyForce.All)
        {
            if (a.regionId < 0) continue;
            if (replayT < 1f && a.prevRegionId >= 0 && a.prevRegionId != a.regionId) continue;
            Color c;
            ColorUtility.TryParseHtmlString(EnemyForce.ColorOf(a), out c);
            // 👁️ 味方の軍団と同じ作りにする：**兵科の台座**（形＝近接/遠隔、色＝陣営）＋ その上に**姿**。
            //    ⚠ 以前は菱形1種を色だけ変えて出していたので、人間の奪還軍と他魔王の軍が
            //      盤の上で見分けられず、集結中か攻めて来ているのかも読めなかった。
            bool ranged = LegionRoster.RangeOf(a.cls) > 0;
            int pedestal = ranged ? HexTileArt.LegionRangedIndex : HexTileArt.LegionIndex;
            AddMark(a.regionId, pedestal, new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255),
                HexTileArt.FoeIndex(a.owner < 0, ranged));
        }
        // 🔭 斥候（戦えないので青で）
        foreach (var sc in ScoutSystem.All)
        {
            if (sc.regionId < 0) continue;
            AddMark(sc.regionId, HexTileArt.ScoutIndex, new Color32(140, 184, 230, 255));
        }
        // ⚔️ 軍団（U-1）。兵科で形と色を変える＝戦線が「前衛の後ろに射手」と読める
        foreach (var lg in LegionRoster.All)
        {
            if (lg.regionId < 0) continue;
            var cls = LegionRoster.ClassOf(lg);
            Color c; ColorUtility.TryParseHtmlString(LegionRoster.ClassHex(cls), out c);
            // 損耗しているほど暗く（残兵が目で分かる）
            float k2 = 0.45f + 0.55f * Mathf.Clamp01(lg.strength / 100f);
            int atlas = LegionRoster.RangeOf(cls) > 0 ? HexTileArt.LegionRangedIndex : HexTileArt.LegionIndex;
            // 兵科の記号は「台座」として残し、その上に**種の姿**を重ねる。
            // ⚠ 姿だけにすると兵科（前衛か射手か）が読めなくなる。戦線の並べ方はそこで決まるので両方要る。
            AddMark(lg.regionId, atlas, new Color32((byte)(c.r * 255 * k2), (byte)(c.g * 255 * k2), (byte)(c.b * 255 * k2), 255),
                HexTileArt.MinionIndex(lg.catalogIndex), new Color32((byte)(255 * k2), (byte)(255 * k2), (byte)(255 * k2), 255));
        }
    }

    private void Rebuild()
    {
        verts.Clear(); uvs.Clear(); cols.Clear(); tris.Clear();
        CollectUnits();
        int mw = SurfaceMap.MapW, mh = SurfaceMap.MapH;
        if (mw <= 0 || mh <= 0) return;

        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        var c = cam.transform.position;
        int row0 = Mathf.Max(0, Mathf.FloorToInt((-c.y - halfH) / RowStep) - 2);
        int row1 = Mathf.Min(mh - 1, Mathf.CeilToInt((-c.y + halfH) / RowStep) + 2);
        int col0 = Mathf.FloorToInt((c.x - halfW) / ColStep) - 2;
        int col1 = Mathf.CeilToInt((c.x + halfW) / ColStep) + 2;
        // 🌏 **同じタイルを2度描かない**。視界が世界1周より広くなったら、カメラを中心に1周ぶんへ丸める。
        //    （これが無いと東西ループで同じ大陸が横に何個も並ぶ）
        if (col1 - col0 + 1 > mw)
        {
            int mid = Mathf.RoundToInt(c.x / ColStep);
            col0 = mid - mw / 2;
            col1 = col0 + mw - 1;
        }

        var sel = SurfaceMap.MapW > 0 && selectedId >= 0 ? SurfaceMap.Get(selectedId) : null;
        labelUsed = 0;
        bool showLabels = zoom <= 20f;             // 引きすぎたら文字は消す（Civと同じ）
        // 💎 資源は**絵で常に出している**ので、名前はうんと寄ったときだけ添える（覚えるまでの補助）。
        // ⚠ 絵を入れる前と同じ 7f のままにすると、絵と名前が二重に出て盤が文字だらけになる。
        bool showNames = zoom <= 4.5f;

        // 奥（row小）から手前（row大）へ積む＝あとの三角形が上に描かれて厚みが正しく重なる
        for (int row = row0; row <= row1; row++)
        {
            for (int col = col0; col <= col1; col++)
            {
                int id = SurfaceMap.IdAt(col, row);
                if (id < 0) continue;
                var r = SurfaceMap.Get(id);
                bool disc = SurfaceMap.IsSeen(id);   // 👁️ 一度でも見たタイルは霧を剥がす
                var p = PosOf(col, row);           // ← col はラップさせずに置くので、継ぎ目でも途切れない

                Rect uv = disc ? HexTileArt.UvOf(r.terrain) : HexTileArt.UvOf(HexTileArt.FogIndex);
                Color32 tint = TintOf(r, disc, sel != null && sel.id == id);
                AddQuad(p, uv, tint);

                // 🚩 支配の境界線（Civの国境）。**面の色だけでは版図の形が読めない**ので縁を描く。
                if (disc && !r.isOcean && r.owner != SurfaceMap.OwnerNeutral && IsBorder(r))
                    AddOverlay(p, HexTileArt.OutlineIndex, OwnerColor(r.owner), 1f, 0f);

                // 🐾 選択中の眷属が今ターン行ける範囲（Civの移動プレビュー）
                if (disc && moveRange != null && moveRange.Contains(id))
                    AddOverlay(p, HexTileArt.SelectIndex, new Color32(150, 235, 180, 70), 0.94f, 0f);

                if (sel != null && sel.id == id)
                    AddOverlay(p, HexTileArt.SelectIndex, new Color32(255, 220, 120, 255), 1f, 0f);

                // 💎 資源は**タイルの右上に小さな絵**で出す。
                // ⚠ 以前は「うんと寄ったときだけ文字」だったので、引くと資源が盤から消えていた。
                //   どこを取れば旨いかは**引いた状態でこそ**読みたいので、絵は常に出す。
                if (disc && !r.isOcean && r.resource != SurfaceMap.Resource.None)
                {
                    int ri = HexTileArt.ResourceIndex(r.resource);
                    if (ri >= 0)
                        AddOverlay(new Vector3(p.x + QuadW * 0.26f, p.y, p.z), ri,
                            new Color32(255, 255, 255, 255), 0.30f, TileSize * 0.30f);
                }

                // 🏛️🏙️ 施設と拠点は**絵で**出す（Civと同じで、盤を見ただけで何が建っているか分かる）
                if (disc && !r.isOcean) AddBuildings(r, p);

                if (showLabels && disc && !r.isOcean)
                {
                    AddLabel(r, p, showNames);
                    AddUnits(id, p);   // 👑🔭⚔️ 盤の上のユニット（文字ではなく絵）
                }
            }
        }

        DrawMovingArmies();   // ⏭️ 前ターンに動いた軍を、道の途中に描く

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        for (int i = labelUsed; i < labelPool.Count; i++) labelPool[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// 🏛️ タイルの上に「建っているもの」を描く。
    /// 拠点／都市／砦はタイルの真ん中、施設はその手前に少し小さく（街区で2つあれば左右に）。
    /// ⚠ 絵は白のまま出す（色を掛けない）。施設の絵は**それ自体が色で種類を示している**ので、
    ///   所有者の色を掛けると全部同じ色になって見分けが付かなくなる。
    /// </summary>
    private void AddBuildings(SurfaceMap.Region r, Vector3 p)
    {
        var white = new Color32(255, 255, 255, 255);
        // 🏙️ 拠点・都市・砦（真ん中・大きめ）
        int settleCell = r.settle == SurfaceMap.Settle.City ? HexTileArt.SpriteIndex("city")
                       : r.settle == SurfaceMap.Settle.Town ? HexTileArt.SpriteIndex("town")
                       : (r.fortLevel > 0 ? HexTileArt.SpriteIndex("fort") : -1);
        if (settleCell >= 0) AddOverlay(p, settleCell, white, 0.66f, -TileSize * 0.02f);

        // 🏛️ 施設（手前に小さく。街区で2つあるときは左右に振る）
        int d0 = r.district, d1 = r.district2;
        int n = (d0 >= 0 ? 1 : 0) + (d1 >= 0 ? 1 : 0);
        if (n == 0) return;
        int k = 0;
        for (int slot = 0; slot < 2; slot++)
        {
            int di = slot == 0 ? d0 : d1;
            if (di < 0) continue;
            int cell = HexTileArt.SpriteIndex(DistrictCatalog.Get(di).id);
            if (cell < 0) continue;
            float dx = n == 1 ? 0f : (k - 0.5f) * QuadW * 0.34f;
            // ⏳ 陳腐化した施設は暗く出す（隣接ボーナスが消えていることが盤で分かる）
            var col = DistrictCatalog.IsObsoleteAt(r.id, slot)
                ? new Color32(150, 140, 150, 220) : white;
            AddOverlay(new Vector3(p.x + dx, p.y, p.z), cell, col, n == 1 ? 0.50f : 0.40f, -TileSize * 0.26f);
            k++;
        }
    }

    /// <summary>⏭️ 前ターンに動いた敵軍を、出発地→現在地の途中に描く（Phase C-14）。</summary>
    private void DrawMovingArmies()
    {
        if (replayT >= 1f) return;
        // なめらかに（最初と最後をゆるめる）
        float k = replayT * replayT * (3f - 2f * replayT);
        foreach (var a in EnemyForce.All)
        {
            if (a.regionId < 0 || a.prevRegionId < 0 || a.prevRegionId == a.regionId) continue;
            var from = SurfaceMap.Get(a.prevRegionId);
            var to = SurfaceMap.Get(a.regionId);
            if (!SurfaceMap.IsSeen(from.id) && !SurfaceMap.IsSeen(to.id)) continue;   // 見えていない所は見せない
            var p0 = PosOf(from.col, from.row);
            var p1 = PosOf(to.col, to.row);
            var p = Vector3.Lerp(p0, p1, k);
            Color c;
            ColorUtility.TryParseHtmlString(EnemyForce.ColorOf(a), out c);
            // ⚠ 止まっている軍（CollectUnits）と**同じ見た目**にする。ここだけ古い菱形のままだと
            //   「動いた瞬間だけ別のものに化ける」ように見える。
            bool ranged = LegionRoster.RangeOf(a.cls) > 0;
            AddOverlay(p, ranged ? HexTileArt.LegionRangedIndex : HexTileArt.LegionIndex,
                new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255), 0.55f, -TileSize * 0.10f);
            int face = HexTileArt.FoeIndex(a.owner < 0, ranged);
            if (face >= 0) AddOverlay(p, face, new Color32(255, 255, 255, 255), 0.34f, TileSize * 0.10f);
        }
    }

    private Color32 TintOf(SurfaceMap.Region r, bool disc, bool selected)
    {
        if (!disc) return new Color32(255, 255, 255, 255);
        if (selected) return new Color32(255, 236, 170, 255);
        if (r.owner == SurfaceMap.OwnerSelf) return new Color32(150, 235, 175, 255);
        if (r.IsRival)
        {
            var c = ColorUtility.TryParseHtmlString(RivalLords.ColorOf(r.RivalIndex), out var rc) ? rc : Color.red;
            return new Color32((byte)(160 + rc.r * 95), (byte)(150 + rc.g * 80), (byte)(150 + rc.b * 80), 255);
        }
        return new Color32(255, 255, 255, 255);
    }

    /// <summary>🚩 タイルの上に重ねる小さな絵（境界線・ユニット・選択枠）。</summary>
    private void AddOverlay(Vector3 center, int atlasIndex, Color32 col, float scale = 1f, float yOff = 0f)
    {
        var uv = HexTileArt.UvOf(atlasIndex);
        float hw = QuadW * 0.5f * scale;
        float hh = TileSize * Squash * scale;
        float cy = center.y + yOff;
        int b = verts.Count;
        verts.Add(new Vector3(center.x - hw, cy - hh, 0));
        verts.Add(new Vector3(center.x - hw, cy + hh, 0));
        verts.Add(new Vector3(center.x + hw, cy + hh, 0));
        verts.Add(new Vector3(center.x + hw, cy - hh, 0));
        // ⚠ UVの縦は **0/1 の決め打ちにしない**。アトラスをグリッドにした時点で行が増えるので、
        //   決め打ちだと全タイルがアトラス全体を貼ってしまう（実際に盤が壊れた）。
        uvs.Add(new Vector2(uv.xMin, uv.yMin)); uvs.Add(new Vector2(uv.xMin, uv.yMax));
        uvs.Add(new Vector2(uv.xMax, uv.yMax)); uvs.Add(new Vector2(uv.xMax, uv.yMin));
        cols.Add(col); cols.Add(col); cols.Add(col); cols.Add(col);
        tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
        tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
    }

    private void AddQuad(Vector3 center, Rect uv, Color32 col)
    {
        float hw = QuadW * 0.5f, h = QuadH;
        // 天面の中心を center に合わせ、側面はその下へ伸ばす
        float top = center.y + TileSize * Squash;
        int b = verts.Count;
        verts.Add(new Vector3(center.x - hw, top - h, 0));
        verts.Add(new Vector3(center.x - hw, top, 0));
        verts.Add(new Vector3(center.x + hw, top, 0));
        verts.Add(new Vector3(center.x + hw, top - h, 0));
        // ⚠ UVの縦は **0/1 の決め打ちにしない**。アトラスをグリッドにした時点で行が増えるので、
        //   決め打ちだと全タイルがアトラス全体を貼ってしまう（実際に盤が壊れた）。
        uvs.Add(new Vector2(uv.xMin, uv.yMin)); uvs.Add(new Vector2(uv.xMin, uv.yMax));
        uvs.Add(new Vector2(uv.xMax, uv.yMax)); uvs.Add(new Vector2(uv.xMax, uv.yMin));
        cols.Add(col); cols.Add(col); cols.Add(col); cols.Add(col);
        tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
        tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
    }

    // ============ 文字（プールから貸し出す・画面内かつ寄っているときだけ） ============
    private void AddLabel(SurfaceMap.Region r, Vector3 p, bool showNames)
    {
        string s = LabelFor(r, showNames);
        if (string.IsNullOrEmpty(s)) return;
        var t = Rent();
        t.text = s;
        t.transform.position = new Vector3(p.x, p.y - TileSize * 0.08f, -1f);
        // ⚠ 折り返しを残したまま自動縮小する。wrapping を切ると縮まずに横へはみ出して隣のタイルへ被る
        //    （C2でヘクスの中の文字を直したときと同じ罠）。
        t.rectTransform.sizeDelta = new Vector2(QuadW * 0.92f, TileSize * 0.9f);
        t.fontSizeMax = 0.9f;
    }

    private static string LabelFor(SurfaceMap.Region r, bool showNames)
    {
        // 🏯 迷宮の入口は**常に**目立たせる（ここが自分の本拠であることが一目で分かるように）
        if (r.type == SurfaceMap.RegionType.Gate) return "<color=#ffd24a>迷宮</color>";
        // 🏷️ Civと同じ密度にする：**地名は出さない**（全タイルに名前を出すと重なって読めない・実測で確認）。
        //    出すのは「そこに何かある」タイルだけ。寄ったときだけ資源も足す。
        if (r.settle == SurfaceMap.Settle.City) return "<color=#ffe08a>都" + r.pop + "</color>";
        if (r.settle == SurfaceMap.Settle.Town) return "<color=#a8d4ff>拠" + r.pop + "</color>";
        if (r.rivalHome >= 0) return "<color=#ff8a6a>真核</color>";
        if (r.wonderIndex >= 0) return "<color=#ffd24a>遺産</color>";
        if (r.naturalWonder >= 0) return "<color=#8ce0a8>驚異</color>";
        // 💎 資源は右上の絵で常に出している。名前はうんと寄ったときだけ添える。
        if (showNames && r.resource != SurfaceMap.Resource.None)
            return "<color=#e3c34a>" + SurfaceMap.ResourceName(r.resource) + "</color>";
        return null;
    }

    private TextMeshPro Rent()
    {
        if (labelUsed < labelPool.Count)
        {
            var e = labelPool[labelUsed++];
            e.gameObject.SetActive(true);
            return e;
        }
        var go = new GameObject("Lbl");
        go.transform.SetParent(labelRoot, false);
        go.layer = surfaceLayer;
        var t = go.AddComponent<TextMeshPro>();
        if (font != null) t.font = font;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        t.enableAutoSizing = true; t.fontSizeMin = 0.15f; t.fontSizeMax = 0.9f;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        t.fontStyle = FontStyles.Bold;
        var mr2 = go.GetComponent<MeshRenderer>();
        if (mr2 != null) mr2.sortingOrder = 100;
        labelPool.Add(t); labelUsed++;
        return t;
    }

    public void SetActiveView(bool on)
    {
        gameObject.SetActive(on);
        if (cam != null) cam.enabled = on;
        if (on) dirty = true;
    }

    public float Zoom
    {
        get { return zoom; }
        set { zoom = Mathf.Clamp(value, ZoomMin, MaxZoom); if (cam != null) { cam.orthographicSize = zoom; ClampCamera(); } dirty = true; }
    }
    /// <summary>盤を切り替えたときに、引きすぎ・寄りすぎを盤の大きさに合わせ直す。</summary>
    public void FitToBoard()
    {
        zoom = Mathf.Clamp(zoom, ZoomMin, MaxZoom);
        if (cam != null) { cam.orthographicSize = zoom; ClampCamera(); }
        dirty = true;
    }
    public int VisibleTiles { get { return verts.Count / 4; } }
}
