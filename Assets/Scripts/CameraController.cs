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