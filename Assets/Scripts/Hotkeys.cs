using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ⌨️ **ホットキー**（V-4）。PCで手が止まらないようにする。
///
/// **なぜ要るか**：1ターンのあいだに「配置ツールを選ぶ → 置く → パネルを開く → 閉じる → 進める」を
/// 何度も繰り返すのに、**全部マウスで下端と上端を往復**していた。
///
/// ## 割り当て（⚠ 既存のキーを避けてある）
/// | キー | 何が起きるか |
/// |---|---|
/// | `1`〜`8` | 配置ツール（トーテム/罠/スポナー/ボス/特殊敵/宝箱/部隊/消去＝下部バーの並び順） |
/// | `Esc` | 開いているパネルを閉じる（無ければツールを解除） |
/// | `Space` | 前半なら『侵略開始』／後半なら『ターンを終える』 |
/// | `Z X C R T` | 図鑑／研究／魔王／遺物／拡張 |
///
/// ⚠ `W A S D` と矢印はカメラ移動（[[CameraController]]）、`G` は領域拡張、
///   `B` `U` はデバッグで既に使われている。**そこには割り当てない**。
/// ⚠ 文字入力中は効かせない…という場面はまだ無いが、将来入力欄を足すならここで見る。
///
/// パネルを開く操作は**上部メニューのボタンをそのまま押す**（`Button.onClick.Invoke`）。
/// ⚠ 開き方を二重に書かない。パネルごとの作法（Refresh の有無・排他）が必ずずれる。
/// </summary>
public class Hotkeys : MonoBehaviour
{
    private GameUIManager ui;
    private GridInputHandler grid;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (!GameSetup.Started) return;                 // タイトル中は効かせない
        if (ui == null) ui = GameUIManager.Instance;
        if (ui == null) return;
        if (grid == null) grid = Object.FindFirstObjectByType<GridInputHandler>();

        // 🚪 Esc：開いているものを閉じる → 何も開いていなければツールを解除
        // 🚪 Esc：①落とし穴の行き先選び中ならやめる ②開いているパネルを閉じる ③ツールを外す
        if (kb.escapeKey.wasPressedThisFrame)
        {
            var fm = Object.FindFirstObjectByType<DungeonFeatureManager>();
            if (fm != null && fm.AwaitingPitLink) fm.CancelPendingPit();
            else if (!ui.CloseTopPanel()) ui.SelectToolByHotkey(-1);
        }

        // ▶ Space：フェーズを進める
        if (kb.spaceKey.wasPressedThisFrame) ui.AdvancePhaseByHotkey();

        // 🔧 1〜8＝下部バーの配置ツール（左から順）
        var digits = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key,
                             kb.digit5Key, kb.digit6Key, kb.digit7Key, kb.digit8Key };
        for (int i = 0; i < digits.Length; i++)
            if (digits[i].wasPressedThisFrame) ui.SelectToolByHotkey(i);

        // 📖 パネル
        if (kb.zKey.wasPressedThisFrame) ui.OpenPanelByHotkey("図鑑");
        if (kb.xKey.wasPressedThisFrame) ui.OpenPanelByHotkey("研究");
        if (kb.cKey.wasPressedThisFrame) ui.OpenPanelByHotkey("魔王");
        if (kb.rKey.wasPressedThisFrame) ui.OpenPanelByHotkey("遺物");
        if (kb.tKey.wasPressedThisFrame) ui.OpenPanelByHotkey("拡張");
        if (kb.vKey.wasPressedThisFrame) ui.OpenPanelByHotkey("先触れ");   // 🔭 次の波と備え
    }
}
