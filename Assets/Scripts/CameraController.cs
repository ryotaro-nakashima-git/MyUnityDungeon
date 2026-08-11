using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 10f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minSize = 3f;
    [SerializeField] private float maxSize = 15f;

    [Header("Auto Fit Settings")]
    [Tooltip("フィット時の余白倍率")]
    [SerializeField] private float fitPadding = 1.15f;
    [Tooltip("右の生成パネル分、迷宮を左へ寄せる割合(画面幅比)")]
    [SerializeField] private float rightPanelFraction = 0.16f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null || !cam.orthographic)
        {
            Debug.LogError("CameraControllerは Orthographic（平行投影）の Camera にアタッチしてください。");
        }
        // 🌑 既定のままだと **Unityの青**が背景に出て「地中の迷宮」に見えない。
        //    壁(Tilemap)が画面の縁まで岩で埋めるので、その隙間に見えるのは黒に近い色でよい。
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.043f, 0.035f, 0.055f);
        }
    }

    private void Update()
    {
        HandleMovement();
        HandleZoom();
        HandleTouchPan();   // 📱 タッチで盤を掴んで動かす
    }

    // 🎥 生成した迷宮全体が収まるようにカメラをズーム＆センタリングする（生成時に呼ばれる）
    public void FitToDungeon()
    {
        if (cam == null) cam = GetComponent<Camera>();
        var grid = Object.FindFirstObjectByType<DungeonGridSystem>();
        if (cam == null || grid == null) return;

        int size = grid.CurrentPlayableSize;
        if (size <= 0) return;

        float tile = grid.GridToWorld(1, 0).x - grid.GridToWorld(0, 0).x;
        if (tile <= 0f) tile = 1f;

        Vector3 origin = grid.GridToWorld(0, 0);
        Vector3 center = origin + new Vector3((size - 1) * tile * 0.5f, (size - 1) * tile * 0.5f, 0f);

        float span = size * tile; // マス数ぶん（端の余白込み）
        float aspect = Mathf.Max(0.1f, cam.aspect);
        float need = Mathf.Max(span * 0.5f, (span * 0.5f) / aspect) * fitPadding;

        // ホイールズームの上限も自動フィットに合わせて広げる（大きい迷宮でも引ける）
        if (need > maxSize) maxSize = need;
        cam.orthographicSize = Mathf.Clamp(need, minSize, maxSize);

        // 右の生成パネルに隠れないよう、カメラを少し右へ（＝迷宮が左に寄る）
        float shiftX = cam.orthographicSize * aspect * rightPanelFraction;
        transform.position = new Vector3(center.x + shiftX, center.y, transform.position.z);

        Debug.Log($"🎥『カメラ自動フィット』size {size} / ortho {cam.orthographicSize:F1}");
    }

    // WASD / 矢印キーによるカメラ移動
    private void HandleMovement()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector3 moveDirection = Vector3.zero;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveDirection.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveDirection.y -= 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveDirection.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveDirection.x += 1f;

        // フレームレートに依存しないように Time.deltaTime を掛ける
        transform.position += moveDirection.normalized * moveSpeed * Time.deltaTime;
    }

    // 🔍 ホイール／ピンチによるズームイン・アウト
    private void HandleZoom()
    {
        if (cam == null) return;

        // ⚠ UIの上ではホイールを盤に渡さない。図鑑や研究ツリーをスクロールしただけで
        //   迷宮の拡大縮小まで起きてしまう（地上盤でも同じ穴があった → [[SurfaceView]]）。
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null && es.IsPointerOverGameObject()) return;

        // 🖱️📱 ホイールでもピンチでも同じ値が来る（＋で寄る）→ [[PointerInput]]
        float step = PointerInput.ZoomStep;
        if (Mathf.Abs(step) > 0.0001f)
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * (1f - step), minSize, maxSize);
    }

    /// <summary>
    /// 📱 1本指で迷宮の盤を掴んで動かす（PCの WASD にあたる操作）。
    /// ⚠ **UIの上と、2本指（ピンチ）のときは動かさない**。
    /// ⚠ 掴んで動かしたあとの指離しを「タップ」にしない責任は、拾う側（[[GridInputHandler]]）にある。
    /// </summary>
    private bool panning; private Vector3 panOrigin;
    private void HandleTouchPan()
    {
        if (cam == null) return;
        if (PointerInput.TouchCount != 1) { panning = false; return; }
        var es = UnityEngine.EventSystems.EventSystem.current;
        Vector3 sp = PointerInput.Position; sp.z = 10f;
        if (PointerInput.Pressed)
        {
            if (es != null && es.IsPointerOverGameObject()) { panning = false; return; }
            panning = true; panOrigin = cam.ScreenToWorldPoint(sp);
            return;
        }
        if (!panning || !PointerInput.Held) return;
        var now = cam.ScreenToWorldPoint(sp);
        var d = panOrigin - now;
        transform.position += new Vector3(d.x, d.y, 0f);
    }
}