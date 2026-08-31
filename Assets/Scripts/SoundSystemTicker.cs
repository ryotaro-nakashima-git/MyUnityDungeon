using UnityEngine;

/// <summary>
/// 🎵 `SoundSystem` の曲の**淡い入れ替え**を毎フレーム進めるだけの係。
///
/// <para>
/// ⚠ `SoundSystem` は純 static なので、自分では毎フレームの処理を持てない。
///   音の GameObject に1つだけ付けて、そこから呼び戻す。
/// ⚠ **`unscaledDeltaTime`** を渡す。戦闘の4倍速に曲の入れ替えが引きずられると、
///   速度を上げた瞬間に曲が飛ぶ（→ [[ui-conventions]] の時間の使い分け）。
/// </para>
/// </summary>
public class SoundSystemTicker : MonoBehaviour
{
    private void Update() { SoundSystem.TickMusic(Time.unscaledDeltaTime); }
}
