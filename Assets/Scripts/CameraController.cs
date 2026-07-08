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
    }

    private void Update()
    {
        HandleMovement();
        HandleZoom();
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

        Debug.Log($"🎥【カメラ自動フィット】size {size} / ortho {cam.orthographicSize:F1}");
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

    // マウスホイールによるズームイン・アウト
    private void HandleZoom()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || cam == null) return;

        // ホイールの回転量を取得 (上方向ならプラス、下方向ならマイナス)
        float scrollValue = mouse.scroll.ReadValue().y;

        if (Mathf.Abs(scrollValue) > 0.01f)
        {
            // スクロール方向に応じて orthographicSize を増減
            float targetSize = cam.orthographicSize - (scrollValue * 0.001f * zoomSpeed);
            
            // サイズが一定範囲に収まるようにクランプ
            cam.orthographicSize = Mathf.Clamp(targetSize, minSize, maxSize);
        }
    }
}