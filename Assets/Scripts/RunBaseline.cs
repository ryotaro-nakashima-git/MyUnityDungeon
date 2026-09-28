using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 🧊 **周の独立**（数理設計 P0・形式仕様 §9.5）。
///
/// <para>
/// ⚠⚠ **なぜ要るか（実測）**：「タイトルへ戻る → 新しい世界を始める」をすると、
///   魔王のLv・遺物・感情ツリーなど**場面に置かれた部品（MonoBehaviour）の中身が前の周のまま**だった。
///   自動運転の3周目は T5 で魔王 Lv66・遺物10 から始まっていた（1周目は Lv6・遺物4）。
///   static 側は `StartNewGame` が1つずつ `Reset()` を呼んでいるが、部品側には初期化の口が無い。
/// </para>
///
/// <para>
/// 形：起動直後（まだ誰も遊んでいない時点）に、周の状態を持つ部品の**フィールドを写し取り**、
///   新しい世界を始めるたびに**その写しへ戻す**。部品ごとに初期化を手で書くと必ず漏れるので、
///   リフレクションで全フィールドを扱う（`SaveSystem` と同じ考え方）。
/// ⚠ **場面の物への参照（UnityEngine.Object）は戻さない**。戻すと、その間に作り直された物を
///   指さなくなる。戻すのは数・文字列・配列・リスト・集合などの**データだけ**。
/// </para>
/// 関連: [[SaveSystem]] [[GameUIManager.Title]]（StartNewGame） docs/math-model/spec.md
/// </summary>
public static class RunBaseline
{
    private class Snap
    {
        public Component comp;
        public List<KeyValuePair<FieldInfo, object>> vals = new List<KeyValuePair<FieldInfo, object>>();
    }

    private static readonly List<Snap> snaps = new List<Snap>();
    public static bool Captured => snaps.Count > 0;
    public static int FieldCount { get { int n = 0; foreach (var s in snaps) n += s.vals.Count; return n; } }

    /// <summary>写し取る。⚠ 1回だけ（2回目以降は無視）。</summary>
    public static void Capture(params Component[] comps)
    {
        if (Captured) return;
        foreach (var c in comps)
        {
            if (c == null) continue;
            var s = new Snap { comp = c };
            foreach (var f in FieldsOf(c.GetType()))
            {
                object v = f.GetValue(c);
                s.vals.Add(new KeyValuePair<FieldInfo, object>(f, DeepCopy(v)));
            }
            snaps.Add(s);
        }
        Debug.Log("🧊『周の写し』" + snaps.Count + " 部品・" + FieldCount + " フィールドを写し取った");
    }

    /// <summary>写しへ戻す（新しい世界を始めるとき）。</summary>
    public static void Restore()
    {
        if (!Captured) return;
        int n = 0;
        foreach (var s in snaps)
        {
            if (s.comp == null) continue;
            foreach (var kv in s.vals) { kv.Key.SetValue(s.comp, DeepCopy(kv.Value)); n++; }
            // 戻したあとで数値を組み直す口があれば呼ぶ（魔王の攻撃力・最大HPなど）
            var m = s.comp.GetType().GetMethod("RecomputeCombatStats",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (m != null) m.Invoke(s.comp, null);
        }
        Debug.Log("🧊『周の写しへ戻した』" + n + " フィールド");
    }

    /// <summary>その型と親の型（MonoBehaviour より上は見ない）の、データのフィールド。</summary>
    private static IEnumerable<FieldInfo> FieldsOf(Type t)
    {
        const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (var cur = t; cur != null && cur != typeof(MonoBehaviour) && cur != typeof(Behaviour); cur = cur.BaseType)
            foreach (var f in cur.GetFields(bf))
            {
                if (f.IsInitOnly || f.IsLiteral) continue;                                   // readonly＝カタログ
                if (typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType)) continue;      // 場面の物は戻さない
                if (typeof(Delegate).IsAssignableFrom(f.FieldType)) continue;                // イベントは戻さない
                if (IsUnityObjectCollection(f.FieldType)) continue;
                yield return f;
            }
    }

    private static bool IsUnityObjectCollection(Type t)
    {
        if (t.IsArray) return typeof(UnityEngine.Object).IsAssignableFrom(t.GetElementType());
        if (t.IsGenericType)
            foreach (var a in t.GetGenericArguments())
                if (typeof(UnityEngine.Object).IsAssignableFrom(a)) return true;
        return false;
    }

    private static readonly MethodInfo Clone =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// データの深い写し。⚠ 配列・List・HashSet・Dictionary は作り直す（写しと本体が同じ物を指さないように）。
    /// その他のクラスは MemberwiseClone（1段だけ）。値型と文字列はそのまま。
    /// </summary>
    private static object DeepCopy(object v)
    {
        if (v == null) return null;
        var t = v.GetType();
        if (t.IsPrimitive || t.IsEnum || v is string || v is decimal) return v;
        if (v is UnityEngine.Object) return v;
        if (t.IsValueType) return v;   // struct は代入で写る（中の参照までは追わない）
        if (v is Array arr)
        {
            var copy = (Array)arr.Clone();
            if (!t.GetElementType().IsValueType && t.GetElementType() != typeof(string))
                for (int i = 0; i < copy.Length; i++) copy.SetValue(DeepCopy(arr.GetValue(i)), i);
            return copy;
        }
        if (t.IsGenericType)
        {
            var g = t.GetGenericTypeDefinition();
            if (g == typeof(List<>) || g == typeof(HashSet<>) || g == typeof(Queue<>))
            {
                var list = (IEnumerable)v;
                var copy = Activator.CreateInstance(t);
                var add = t.GetMethod(g == typeof(Queue<>) ? "Enqueue" : "Add");
                foreach (var e in list) add.Invoke(copy, new[] { DeepCopy(e) });
                return copy;
            }
            if (g == typeof(Dictionary<,>))
            {
                var copy = (IDictionary)Activator.CreateInstance(t);
                foreach (DictionaryEntry e in (IDictionary)v) copy[e.Key] = DeepCopy(e.Value);
                return copy;
            }
        }
        try { return Clone.Invoke(v, null); } catch { return v; }
    }
}
