using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 🖱️📱 **マウスとタッチを1つの窓口にまとめる**（V-4）。
///
/// **なぜ要るか**：盤の操作が `Mouse.current` を直に読んでいたので、**タッチでは何も動かなかった**。
/// 触る側が2種類の入力を場所ごとに書き分けると必ず片方を忘れるので、ここで1本にする。
///
/// ## 決まり
/// - **押す/離す/位置**は「マウスの左ボタン」と「1本目の指」を同じものとして扱う。
/// - **ズームはホイールとピンチを `ZoomStep` に正規化する**（＋で寄る／−で引く、だいたい ±0.4）。
///   ⚠ ホイールの生値は環境で ±1 だったり ±120 だったりする。ピンチは画素差。
///   **単位が違うものを呼ぶ側で吸収させない**。ここで吸収する。
/// - **指が2本のときは「押している」と言わない**。ピンチ中に盤が掴まれて飛ぶのを防ぐ。
///
/// ⚠ 状態（前フレームの指の間隔）を持つので、**1フレームに1回だけ**計算する。
///   誰かが `Tick()` を呼ぶ形にすると呼び忘れるので、**読まれたときに自分で1回だけ**更新する。
///
/// 関連: [[SurfaceView]] [[CameraController]] [[Hotkeys]]。
/// </summary>
public static class PointerInput
{
    private static int lastFrame = -1;
    private static Vector2 pos;
    private static bool pressed, held, released;
    private static float zoomStep;
    private static int touchCount;
    private static bool isTouch;
    private static float prevPinchDist = -1f;
    private static bool prevDown;      // 前フレームに1本目の指が下りていたか（離した判定の変わり目に使う）

    /// <summary>いま画面のどこを指しているか（画面座標）。</summary>
    public static Vector2 Position { get { Ensure(); return pos; } }
    /// <summary>このフレームに押し始めたか。</summary>
    public static bool Pressed { get { Ensure(); return pressed; } }
    /// <summary>押し続けているか。</summary>
    public static bool Held { get { Ensure(); return held; } }
    /// <summary>このフレームに離したか。</summary>
    public static bool Released { get { Ensure(); return released; } }
    /// <summary>寄る/引くの量。＋で寄る。ホイールとピンチをここで同じ単位にしてある。</summary>
    public static float ZoomStep { get { Ensure(); return zoomStep; } }
    /// <summary>触れている指の数（マウスなら0）。</summary>
    public static int TouchCount { get { Ensure(); return touchCount; } }
    /// <summary>直近の操作がタッチだったか（ツールチップの出し方を変えるのに使う）。</summary>
    public static bool IsTouch { get { Ensure(); return isTouch; } }

    private static void Ensure()
    {
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        Recompute();
    }

    private static void Recompute()
    {
        pressed = held = released = false;
        zoomStep = 0f;
        touchCount = 0;

        var ts = Touchscreen.current;
        if (ts != null)
        {
            var t0 = ts.touches.Count > 0 ? ts.touches[0] : null;
            var t1 = ts.touches.Count > 1 ? ts.touches[1] : null;
            bool d0 = t0 != null && IsDown(t0);
            bool d1 = t1 != null && IsDown(t1);
            touchCount = (d0 ? 1 : 0) + (d1 ? 1 : 0);

            if (d0)
            {
                isTouch = true;
                pos = t0.position.ReadValue();
                // ⚠ 2本目が触れているあいだは「押している」と言わない（ピンチ中に盤が飛ぶ）
                if (!d1)
                {
                    var ph = t0.phase.ReadValue();
                    pressed = ph == UnityEngine.InputSystem.TouchPhase.Began;
                    held = true;
                }
            }
            // ⚠⚠ **離した判定は「指が下りていた→下りていない」の変わり目で取る。**
            //   `phase == Ended` を見ると、次の指が触れるまで **Ended が残り続ける**ので
            //   毎フレーム「離した」が立ち、しかもその下のマウス処理へ行かなくなって
            //   **一度タッチするとマウスが永久に効かなくなる**（実際にそうなった）。
            else if (prevDown)
            {
                released = true;
                if (t0 != null) pos = t0.position.ReadValue();
            }
            prevDown = d0;

            // 🤏 ピンチ：指の間隔の変化を「寄る量」に直す
            if (d0 && d1)
            {
                float dist = Vector2.Distance(t0.position.ReadValue(), t1.position.ReadValue());
                if (prevPinchDist > 0f)
                    zoomStep = Mathf.Clamp((dist - prevPinchDist) / Mathf.Max(120f, Screen.height * 0.25f), -0.4f, 0.4f);
                prevPinchDist = dist;
                pos = (t0.position.ReadValue() + t1.position.ReadValue()) * 0.5f;
            }
            else prevPinchDist = -1f;

            // ⚠⚠ **指を離したフレームもここで打ち切る。**
            //   `touchCount > 0` だけで判定すると、離した瞬間は 0 になるので下のマウス処理へ落ちて、
            //   `released = マウスのボタン` で **せっかく立てた Released が False に上書きされる**（実際に踏んだ）。
            if (touchCount > 0 || released || zoomStep != 0f) return;
            // 指が完全に離れて何も起きていないフレームだけ、マウスへ戻す
        }

        var m = Mouse.current;
        if (m == null) return;
        pos = m.position.ReadValue();
        // マウスを触った合図（クリック・移動・ホイール）で「タッチ操作中」を降ろす。
        // ⚠ ホイールも入れる。入れないと、タッチしたあとマウスに戻したとき
        //   「離した瞬間に置く」タッチ流儀のまま残る（→ [[GridInputHandler]]）。
        if (m.leftButton.wasPressedThisFrame || m.delta.ReadValue().sqrMagnitude > 0.01f
            || Mathf.Abs(m.scroll.ReadValue().y) > 0.01f) isTouch = false;
        pressed = m.leftButton.wasPressedThisFrame;
        held = m.leftButton.isPressed;
        released = m.leftButton.wasReleasedThisFrame;

        // 🖱️ ホイール：環境によって ±1 だったり ±120 だったりするので、ここで正規化する
        float scroll = m.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
            zoomStep = Mathf.Clamp(scroll * (Mathf.Abs(scroll) > 10f ? 0.0016f : 0.16f), -0.4f, 0.4f);
    }

    private static bool IsDown(UnityEngine.InputSystem.Controls.TouchControl t)
    {
        var ph = t.phase.ReadValue();
        return ph == UnityEngine.InputSystem.TouchPhase.Began
            || ph == UnityEngine.InputSystem.TouchPhase.Moved
            || ph == UnityEngine.InputSystem.TouchPhase.Stationary;
    }

    /// <summary>
    /// 🧪 テスト用。「次に読まれたらもう一度計算する」だけ。
    /// ⚠ **ピンチの基準（前フレームの指の間隔）は消さない。** 消すと2本指を動かしても
    ///   毎回「初回」扱いになり、`ZoomStep` が永久に 0 のままになる（テストでそう見えた）。
    ///   基準は指が2本未満になった時点で自然に捨てられる。
    /// </summary>
    public static void Invalidate() { lastFrame = -1; }
}
