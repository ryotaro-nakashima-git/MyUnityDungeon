using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 遺物（レリック）＝3層バフの最上位「全体パッシブ」層。
/// カタログから最大 slotCount 個を装備し、装備中は効果がダンジョン全域に適用される。
/// 効果は各システム（防衛体強化/罠/撃破DP）が getter を参照して乗算する。CDO2の遺物に相当。
/// </summary>
public class RelicManager : MonoBehaviour
{
    public static RelicManager Instance { get; private set; }

    public enum Effect { DefenderHp, DefenderAtk, TrapDamage, KillDP }

    public class Relic
    {
        public string name; public string desc; public Effect effect; public float value;
    }

    [SerializeField] private int slotCount = 2;

    private List<Relic> catalog;
    private int[] slots; // 各スロットに入っているカタログindex（未装備=-1）

    public int SlotCount => slotCount;
    public IReadOnlyList<Relic> Catalog => catalog;
    public int SlotAt(int i) => (slots != null && i >= 0 && i < slots.Length) ? slots[i] : -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildCatalog();
        slots = new int[slotCount];
        for (int i = 0; i < slotCount; i++) slots[i] = -1;
    }

    private void BuildCatalog()
    {
        catalog = new List<Relic>
        {
            new Relic{ name="不死の王笏", desc="全防衛体のHP +25%", effect=Effect.DefenderHp, value=0.25f },
            new Relic{ name="獣爪の紋章", desc="全防衛体の攻撃 +25%", effect=Effect.DefenderAtk, value=0.25f },
            new Relic{ name="業火の宝珠", desc="罠のダメージ +60%", effect=Effect.TrapDamage, value=0.60f },
            new Relic{ name="強欲の金貨", desc="撃破時のDP +40%", effect=Effect.KillDP, value=0.40f },
        };
    }

    public bool IsEquipped(int catalogIdx)
    {
        if (slots == null) return false;
        foreach (var s in slots) if (s == catalogIdx) return true;
        return false;
    }

    /// <summary>トグル装備：装備済みなら外す／未装備なら空きスロットへ／空き無しなら先頭を置換。</summary>
    public void Toggle(int catalogIdx)
    {
        if (catalog == null || catalogIdx < 0 || catalogIdx >= catalog.Count) return;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == catalogIdx) { slots[i] = -1; Debug.Log($"🏺【遺物】『{catalog[catalogIdx].name}』を外しました"); return; }
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == -1) { slots[i] = catalogIdx; Debug.Log($"🏺【遺物】『{catalog[catalogIdx].name}』を装備しました"); return; }
        slots[0] = catalogIdx; // 空き無し→先頭スロットを置換
        Debug.Log($"🏺【遺物】『{catalog[catalogIdx].name}』を装備（スロット1を置換）");
    }

    private float Sum(Effect e)
    {
        float v = 0f;
        if (slots == null) return v;
        foreach (var s in slots) if (s >= 0 && catalog[s].effect == e) v += catalog[s].value;
        return v;
    }

    // ---- 効果（各システムが参照）----
    public float DefenderHpMult => 1f + Sum(Effect.DefenderHp);
    public float DefenderAtkMult => 1f + Sum(Effect.DefenderAtk);
    public float TrapDamageMult => 1f + Sum(Effect.TrapDamage);
    public float KillDPMult => 1f + Sum(Effect.KillDP);
}
