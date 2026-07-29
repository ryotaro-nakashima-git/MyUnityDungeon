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
    private float zoom = 7f;                             // orthographicSize（寄り気味で始める）
    public const float ZoomMin = 3.5f, ZoomMax = 46f;
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

    private void Init()
    {
        // 🎥 地上専用カメラ。迷宮のカメラは触らず、こちらを有効/無効で切り替える（迷宮の状態は保たれる）。
        var camGO = new GameObject("SurfaceCamera");
        camGO.transform.SetParent(transform, false);
        cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = zoom;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.035f, 0.028f, 0.055f);
        cam.transform.position = new Vector3(0, 0, -50f);
        cam.depth = 1;

        var meshGO = new GameObject("Board");
        meshGO.transform.SetParent(transform, false);
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

    public void CenterOn(int regionId)
    {
        var r = SurfaceMap.Get(regionId);
        var p = PosOf(r.col, r.row);
        // 右半分は詳細パネルで隠れるので、注目タイルが**見えている左側の中央**に来るようずらす
        cam.transform.position = new Vector3(p.x + cam.orthographicSize * cam.aspect * 0.26f, p.y, -50f);
        dirty = true;
    }
    public void SetSelected(int id) { selectedId = id; dirty = true; }
    public void MarkDirty() { dirty = true; }

    // ============ 入力（パン／ズーム／クリック） ============
    private void Update()
    {
        if (cam == null || !cam.enabled) return;
        HandleInput();
        if (dirty) { Rebuild(); dirty = false; }
    }

    private void HandleInput()
    {
        // ※このプロジェクトは new Input System を使う（他のスクリプトと揃える）
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;
        Vector3 mp = mouse.position.ReadValue();
        mp.z = 10f;

        float scroll = mouse.scroll.ReadValue().y;      // 環境によって ±1 だったり ±120 だったりする
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float step = Mathf.Clamp(scroll * (Mathf.Abs(scroll) > 10f ? 0.0016f : 0.16f), -0.4f, 0.4f);
            zoom = Mathf.Clamp(zoom * (1f - step), ZoomMin, ZoomMax);
            cam.orthographicSize = zoom;
            dirty = true;
            if (onViewChanged != null) onViewChanged();
        }

        bool down = mouse.leftButton.wasPressedThisFrame;
        bool held = mouse.leftButton.isPressed;
        bool up = mouse.leftButton.wasReleasedThisFrame;

        if (down)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && es.IsPointerOverGameObject()) return;   // UIの上なら盤は触らない
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

    /// <summary>南北だけ端で止める（東西はループするので止めない）。</summary>
    private void ClampCamera()
    {
        var p = cam.transform.position;
        float top = 0f, bottom = -RowStep * (SurfaceMap.MapH - 1);
        p.y = Mathf.Clamp(p.y, bottom - 2f, top + 2f);
        cam.transform.position = new Vector3(p.x, p.y, -50f);
    }

    // ============ 描画（見えているところだけメッシュに詰める） ============
    private void Rebuild()
    {
        verts.Clear(); uvs.Clear(); cols.Clear(); tris.Clear();
        int mw = SurfaceMap.MapW, mh = SurfaceMap.MapH;
        if (mw <= 0 || mh <= 0) return;

        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        var c = cam.transform.position;
        int row0 = Mathf.Max(0, Mathf.FloorToInt((-c.y - halfH) / RowStep) - 2);
        int row1 = Mathf.Min(mh - 1, Mathf.CeilToInt((-c.y + halfH) / RowStep) + 2);
        int col0 = Mathf.FloorToInt((c.x - halfW) / ColStep) - 2;
        int col1 = Mathf.CeilToInt((c.x + halfW) / ColStep) + 2;

        var sel = SurfaceMap.MapW > 0 && selectedId >= 0 ? SurfaceMap.Get(selectedId) : null;
        labelUsed = 0;
        bool showLabels = zoom <= 20f;             // 引きすぎたら文字は消す（Civと同じ）
        bool showNames = zoom <= 7f;               // うんと寄ったときだけ資源名も出す

        // 奥（row小）から手前（row大）へ積む＝あとの三角形が上に描かれて厚みが正しく重なる
        for (int row = row0; row <= row1; row++)
        {
            for (int col = col0; col <= col1; col++)
            {
                int id = SurfaceMap.IdAt(col, row);
                if (id < 0) continue;
                var r = SurfaceMap.Get(id);
                bool disc = SurfaceMap.IsDiscovered(id);
                var p = PosOf(col, row);           // ← col はラップさせずに置くので、継ぎ目でも途切れない

                Rect uv = disc ? HexTileArt.UvOf(r.terrain) : HexTileArt.UvOf(HexTileArt.FogIndex);
                Color32 tint = TintOf(r, disc, sel != null && sel.id == id);
                AddQuad(p, uv, tint);

                if (showLabels && disc && !r.isOcean) AddLabel(r, p, showNames);
            }
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        for (int i = labelUsed; i < labelPool.Count; i++) labelPool[i].gameObject.SetActive(false);
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
        uvs.Add(new Vector2(uv.xMin, 0)); uvs.Add(new Vector2(uv.xMin, 1));
        uvs.Add(new Vector2(uv.xMax, 1)); uvs.Add(new Vector2(uv.xMax, 0));
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
        // 🏷️ Civと同じ密度にする：**地名は出さない**（全タイルに名前を出すと重なって読めない・実測で確認）。
        //    出すのは「そこに何かある」タイルだけ。寄ったときだけ資源も足す。
        if (r.settle == SurfaceMap.Settle.City) return "<color=#ffe08a>都" + r.pop + "</color>";
        if (r.settle == SurfaceMap.Settle.Town) return "<color=#a8d4ff>拠" + r.pop + "</color>";
        if (r.rivalHome >= 0) return "<color=#ff8a6a>真核</color>";
        if (r.wonderIndex >= 0) return "<color=#ffd24a>遺産</color>";
        if (r.naturalWonder >= 0) return "<color=#8ce0a8>驚異</color>";
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

    public float Zoom { get { return zoom; } set { zoom = Mathf.Clamp(value, ZoomMin, ZoomMax); if (cam != null) cam.orthographicSize = zoom; dirty = true; } }
    public int VisibleTiles { get { return verts.Count / 4; } }
}
