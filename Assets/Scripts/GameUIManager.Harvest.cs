using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🌾 **地上の収穫を見せる**（④）のUI側。地上の画面に資源チップを置き、
/// ターンの締めで入った産出を**盤から飛ばしてそこに着地させる**（→ [[HarvestBurst]]）。
///
/// <para>
/// ⚠⚠ **実測：地上の画面には資源が1つも出ていなかった。**
///   地上モードでは迷宮Canvasごと `enabled=false` にしているので、上部バーの
///   DP/素材/名声チップは**まるごと消えている**。つまり地上で何をしても、
///   持ち物がいくつ増えたのか**その場では確かめようがなかった**。
///   飛ばす先が無ければジャラジャラも作れないので、まず**着地点**を置く。
/// </para>
///
/// <para>
/// ⚠ 収穫の演出のあいだだけ、画面の切り替えを待たせる（`ShowTime` ぶん）。
///   ⚠⚠ 待たせるのは**画面だけ**。ターンの解決はすべて済んでいる（`currentPhase` はもう Prepare）。
///     ここで解決を遅らせると、セーブや報告と順番が入れ替わって壊れる。
/// ⚠ 時間は `WaitForSecondsRealtime`（地上に倍速は無いが、`timeScale` を跨いでも止まらないように）。
/// </para>
/// </summary>
public partial class GameUIManager
{
    private GameObject surfaceChipRow;
    private TextMeshProUGUI surfDpText, surfMatText, surfRpText, surfFameText;
    private TextMeshProUGUI surfHarvestText;
    private float surfHarvestLife, chipRowRight;
    private bool harvestHolding;

    /// <summary>💰 地上の画面の資源チップ（右上・盤の邪魔にならない位置）。</summary>
    private void BuildSurfaceResChips(Image panel, float barH, float pad)
    {
        var row = Panel(panel, "SurfaceRes", C("#0e0b16"));
        surfaceChipRow = row.gameObject;
        float w = 4 * 86f + 3 * 8f + 16f;
        // ⚠⚠ **右上の角は使えない。** そこはトーストが積む場所で、
        //   トーストは order 200 の別Canvasに居るので**必ず上に被る**（実測：4つ中3つが隠れた）。
        //   しかも収穫が入る瞬間は通知が一番多い瞬間なので、よりによってそこで見えなくなる。
        //   トーストの帯（画面右端から約 400px）を避けて、その左に置く。
        float toastBand = 420f;
        Place(row.rectTransform, FS_W - pad - toastBand - w, barH + 8f, w, 58f);
        chipRowRight = FS_W - pad - toastBand;
        Outline(row, LINE2);
        row.color = new Color(row.color.r, row.color.g, row.color.b, 0.92f);

        var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(8, 8, 8, 8); hl.spacing = 8;
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childControlWidth = true; hl.childControlHeight = true;
        hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

        surfDpText = ResChip(row, UITheme.DP, "DP", "0", "dp");
        surfMatText = ResChip(row, UITheme.Material, "素材", "0", "material");
        surfRpText = ResChip(row, UITheme.Research, "研究点", "0", "research");
        surfFameText = ResChip(row, UITheme.Fame, "名声", "0", "fame");

        // 🌾 収穫の一行（着地の直前に出して、少しだけ残す）
        var line = NewRect("HarvestLine", panel.rectTransform);
        Place(line, chipRowRight - 620f, barH + 70f, 620f, 26f);   // ⚠ チップの真下に右揃えで並べる
        surfHarvestText = Text(line, "", 15f, GOLD, TextAlignmentOptions.Right, FontStyles.Bold);
        surfHarvestText.enableWordWrapping = false;
        surfHarvestText.raycastTarget = false;
        StretchFull(surfHarvestText.rectTransform);
        SetTxt(surfHarvestText, "");
    }

    /// <summary>
    /// 💰 チップの値。⚠ **地上モードでなくても毎フレーム回す。**
    ///   止めると、次に地上へ来たときに古い値から数え上げ直して「いま増えた」ように見える。
    /// </summary>
    private void RefreshSurfaceResChips()
    {
        if (surfDpText == null) return;
        if (res != null)
        {
            SetNumber(surfDpText, res.DungeonPoints);
            SetNumber(surfMatText, res.CraftMaterials);
            SetNumber(surfFameText, res.DungeonFame);
        }
        SetNumber(surfRpText, ResearchState.RP);

        if (surfHarvestLife > 0f)
        {
            surfHarvestLife -= Time.unscaledDeltaTime;
            var c = surfHarvestText.color; c.a = Mathf.Clamp01(surfHarvestLife / 0.6f); surfHarvestText.color = c;
            if (surfHarvestLife <= 0f) SetTxt(surfHarvestText, "");
        }
    }

    /// <summary>
    /// 🌾 **収穫を見せてから画面を渡す**（`DungeonTurnManager.EndSurfacePhase` から呼ぶ）。
    /// 収穫が無いときや地上を見ていないときは、そのまま今までどおり切り替える。
    /// </summary>
    public void OnPhaseChangedAfterHarvest()
    {
        if (harvestHolding) return;
        if (!surfaceModeOn || surfaceView == null || !HarvestBurst.HasAny) { HarvestBurst.Clear(); OnPhaseChanged(); return; }
        StartCoroutine(HarvestThenLeave());
    }

    private IEnumerator HarvestThenLeave()
    {
        harvestHolding = true;
        // ⚠ 文字は撒く**前**に作る（`Play` は溜めを空にするので、あとからでは0になる）
        var sb = new System.Text.StringBuilder("収穫　");
        if (HarvestBurst.PendingDp > 0) sb.Append("<color=#e3a94a>+" + UITheme.Num(HarvestBurst.PendingDp) + " DP</color>　");
        if (HarvestBurst.PendingMat > 0) sb.Append("<color=#57c3ab>+" + HarvestBurst.PendingMat + " 素材</color>　");
        if (HarvestBurst.PendingRp > 0) sb.Append("<color=#8cb8e6>+" + HarvestBurst.PendingRp + " 研究点</color>　");
        if (HarvestBurst.PendingFame > 0) sb.Append("<color=#e05a5a>+" + HarvestBurst.PendingFame + " 名声</color>");
        SetTxt(surfHarvestText, sb.ToString());
        var hc = surfHarvestText.color; hc.a = 1f; surfHarvestText.color = hc;
        surfHarvestLife = HarvestBurst.ShowTime + 0.6f;

        HarvestBurst.Play(surfaceView.Layer);
        yield return new WaitForSecondsRealtime(HarvestBurst.ShowTime);
        harvestHolding = false;
        OnPhaseChanged();
    }

    /// <summary>
    /// 🎯 **地上の収穫の行き先**。地上チップの位置をワールド座標で返す（0=DP 1=素材 2=研究点 3=名声）。
    /// ⚠ 地上カメラは `Camera.main` ではないので、`surfaceView.cam` を直に使う
    ///   （`Camera.main` を見ると、畳まれた迷宮カメラを掴んで**見当違いの方向へ吸われる**）。
    /// </summary>
    public static Vector3 SurfaceChipWorldTarget(int kind, Vector3 fallbackFrom)
    {
        var ui = Instance;
        if (ui == null || ui.surfaceView == null || ui.surfaceView.cam == null)
            return fallbackFrom + new Vector3(0f, 3f, 0f);
        var t = kind == 0 ? ui.surfDpText : kind == 1 ? ui.surfMatText
              : kind == 2 ? ui.surfRpText : ui.surfFameText;
        if (t == null || t.transform.parent == null) return fallbackFrom + new Vector3(0f, 3f, 0f);
        var chip = (RectTransform)t.transform.parent;
        var cam = ui.surfaceView.cam;
        var w = cam.ScreenToWorldPoint(new Vector3(chip.position.x, chip.position.y, Mathf.Abs(cam.transform.position.z)));
        w.z = fallbackFrom.z;
        return w;
    }
}
