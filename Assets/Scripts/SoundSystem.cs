using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🔊 音（Phase E-20）。**このプロジェクトには音のファイルが1つも無かった**（AudioClip 0件）。
///
/// ## なぜ手続き生成か
/// [[UIIcons]] と同じ判断。素材の調達を待つと**いつまでも無音のまま**になる。
/// 波形を計算で作れば、今すぐ全部の操作に手応えが付く。差し替えたくなったら
/// `Clip()` の中身をファイル読み込みに変えるだけで済むよう、鳴らす側とは切ってある。
///
/// ## 作り
/// - **効果音**：短い波形をその場で焼いてキャッシュ（`AudioClip.Create` ＋ `SetData`）。
/// - **BGM**：`PCMReaderCallback` で**流しながら合成**する。16秒ループを何百回も繰り返すと
///   耳が死ぬので、和音の進行とアルペジオを鳴らし続ける方式にした。メモリも食わない。
///   ⚠ コールバックは**音のスレッド**で走る。中で `new` しない・Unity API を触らない。
/// - 音量は master/bgm/se の3つ。`PlayerPrefs` に持つので**設定画面([[SoundSystem]] E-21)から即反映**できる。
///
/// ⚠ 時間は `Time.timeScale` の影響を受けない（音は倍速に引きずられない）。
/// 関連: [[NotifySystem]]（通知の種類ごとに鳴らす） [[CommandSystem]] [[game-polish-plan]]。
/// </summary>
public static class SoundSystem
{
    public enum Sfx
    {
        Click, Confirm, Cancel, Place, Remove, Error,
        Gain, Loss, Danger, Story, Turn,
        Hit, Kill, Wave, Command, Discover, Save,
    }

    public enum Bgm { None, Prepare, Battle, Surface }

    private const int SR = 44100;        // 効果音の標本化周波数
    private const int BGM_SR = 44100;

    // ============ 音量（PlayerPrefs） ============
    private static float master = -1f, bgmVol, seVol;
    private static void EnsurePrefs()
    {
        if (master >= 0f) return;
        master = PlayerPrefs.GetFloat("vol_master", 0.8f);
        bgmVol = PlayerPrefs.GetFloat("vol_bgm", 0.5f);
        seVol = PlayerPrefs.GetFloat("vol_se", 0.7f);
    }
    public static float Master { get { EnsurePrefs(); return master; } set { EnsurePrefs(); master = Mathf.Clamp01(value); PlayerPrefs.SetFloat("vol_master", master); ApplyVolumes(); } }
    public static float BgmVolume { get { EnsurePrefs(); return bgmVol; } set { EnsurePrefs(); bgmVol = Mathf.Clamp01(value); PlayerPrefs.SetFloat("vol_bgm", bgmVol); ApplyVolumes(); } }
    public static float SeVolume { get { EnsurePrefs(); return seVol; } set { EnsurePrefs(); seVol = Mathf.Clamp01(value); PlayerPrefs.SetFloat("vol_se", seVol); ApplyVolumes(); } }

    private static void ApplyVolumes()
    {
        EnsureRoot();
        if (bgmSrc != null) bgmSrc.volume = master * bgmVol * 0.32f;   // BGMは控えめに敷く
        if (seSrc != null) seSrc.volume = master * seVol;
    }

    // ============ 土台 ============
    private static AudioSource seSrc, bgmSrc;
    private static readonly Dictionary<Sfx, AudioClip> cache = new Dictionary<Sfx, AudioClip>();

    private static void EnsureRoot()
    {
        if (seSrc != null) return;
        var go = new GameObject("SoundSystem");
        Object.DontDestroyOnLoad(go);
        seSrc = go.AddComponent<AudioSource>();
        seSrc.playOnAwake = false; seSrc.spatialBlend = 0f;
        bgmSrc = go.AddComponent<AudioSource>();
        bgmSrc.playOnAwake = false; bgmSrc.spatialBlend = 0f; bgmSrc.loop = true;
        EnsurePrefs(); ApplyVolumes();
    }

    // 同じ音が同じフレームに何十発も鳴ると割れるので、種類ごとに最短間隔を設ける
    private static readonly Dictionary<Sfx, float> lastAt = new Dictionary<Sfx, float>();
    private static float MinGap(Sfx s)
    {
        switch (s) { case Sfx.Hit: return 0.045f; case Sfx.Kill: return 0.07f; case Sfx.Click: return 0.03f; default: return 0.02f; }
    }

    public static void Play(Sfx s, float volume = 1f, float pitch = 1f)
    {
        EnsureRoot();
        if (master <= 0.001f || seVol <= 0.001f) return;
        float now = Time.unscaledTime, prev;
        if (lastAt.TryGetValue(s, out prev) && now - prev < MinGap(s)) return;
        lastAt[s] = now;

        AudioClip c;
        if (!cache.TryGetValue(s, out c)) { c = Bake(s); cache[s] = c; }
        if (c == null) return;
        // 少しだけ音程を散らす（同じ音が続いても機械的に聞こえない）
        seSrc.pitch = pitch * Random.Range(0.97f, 1.03f);
        seSrc.PlayOneShot(c, Mathf.Clamp01(volume));
    }

    // ============ 効果音の合成 ============
    private static AudioClip Bake(Sfx s)
    {
        switch (s)
        {
            case Sfx.Click:    return Make("click", 0.055f, (t, d) => Sq(t, 900f) * Env(t, d, 0.004f, 1.6f) * 0.20f);
            case Sfx.Confirm:  return Make("ok", 0.20f, (t, d) => (Tri(t, t < 0.07f ? 659f : 988f)) * Env(t, d, 0.006f, 1.1f) * 0.26f);
            case Sfx.Cancel:   return Make("ng", 0.16f, (t, d) => Tri(t, t < 0.06f ? 440f : 330f) * Env(t, d, 0.006f, 1.3f) * 0.22f);
            case Sfx.Place:    return Make("place", 0.18f, (t, d) =>
                                    Sin(t, 180f - 60f * t / d) * Env(t, d, 0.003f, 2.2f) * 0.35f
                                  + Noise(t) * Env(t, 0.05f, 0.001f, 5f) * 0.10f);
            case Sfx.Remove:   return Make("remove", 0.16f, (t, d) => Sin(t, 260f - 120f * t / d) * Env(t, d, 0.004f, 2.4f) * 0.24f);
            case Sfx.Error:    return Make("err", 0.24f, (t, d) => Sq(t, t % 0.09f < 0.045f ? 190f : 150f) * Env(t, d, 0.004f, 1.0f) * 0.20f);
            case Sfx.Gain:     return Make("gain", 0.30f, (t, d) =>
                                    Tri(t, t < 0.08f ? 1046f : t < 0.15f ? 1318f : 1568f) * Env(t, d, 0.005f, 1.4f) * 0.24f);
            case Sfx.Loss:     return Make("loss", 0.36f, (t, d) =>
                                    Tri(t, t < 0.12f ? 392f : 294f) * Env(t, d, 0.008f, 1.4f) * 0.24f);
            case Sfx.Danger:   return Make("danger", 0.46f, (t, d) =>
                                    (Sq(t, 150f) * 0.5f + Sin(t, 75f)) * Env(t, d, 0.01f, 1.0f) * Pulse(t, 0.15f) * 0.22f);
            case Sfx.Story:    return Make("story", 0.9f, (t, d) =>
                                    (Sin(t, 261.6f) + Sin(t, 311.1f) * 0.8f + Sin(t, 392f) * 0.7f) * Env(t, d, 0.09f, 1.0f) * 0.11f);
            case Sfx.Turn:     return Make("turn", 0.85f, (t, d) =>
                                    (Sin(t, 523.3f) + Sin(t, 1046.5f) * 0.35f + Sin(t, 1568f) * 0.14f) * Env(t, d, 0.004f, 2.6f) * 0.20f);
            case Sfx.Hit:      return Make("hit", 0.11f, (t, d) =>
                                    Noise(t) * Env(t, 0.055f, 0.001f, 4.5f) * 0.26f + Sin(t, 120f) * Env(t, d, 0.002f, 3.5f) * 0.30f);
            case Sfx.Kill:     return Make("kill", 0.30f, (t, d) =>
                                    Noise(t) * Env(t, 0.14f, 0.002f, 3.0f) * 0.22f + Sin(t, 220f - 150f * t / d) * Env(t, d, 0.003f, 2.2f) * 0.30f);
            case Sfx.Wave:     return Make("wave", 1.15f, (t, d) =>
                                    Saw(t, 110f * (1f + 0.02f * Mathf.Sin(t * 9f))) * Env(t, d, 0.12f, 0.8f) * 0.20f
                                  + Saw(t, 165f) * Env(t, d, 0.30f, 0.8f) * 0.13f);
            case Sfx.Command:  return Make("cmd", 0.55f, (t, d) =>
                                    Saw(t, 82f) * Env(t, d, 0.006f, 1.6f) * 0.28f
                                  + Sin(t, 330f - 120f * t / d) * Env(t, d, 0.004f, 2.0f) * 0.16f);
            case Sfx.Discover: return Make("disc", 0.7f, (t, d) =>
                                    (Sin(t, 587.3f) + Sin(t, 880f) * 0.6f) * Env(t, d, 0.02f, 1.8f) * 0.18f);
            case Sfx.Save:     return Make("save", 0.34f, (t, d) => Tri(t, t < 0.1f ? 784f : 1046f) * Env(t, d, 0.006f, 1.6f) * 0.20f);
        }
        return null;
    }

    private static AudioClip Make(string name, float dur, System.Func<float, float, float> f)
    {
        int n = Mathf.Max(16, Mathf.RoundToInt(dur * SR));
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            data[i] = Mathf.Clamp(f(t, dur), -1f, 1f);
        }
        // 端のプツッを消す
        int fade = Mathf.Min(160, n / 8);
        for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;
        var c = AudioClip.Create(name, n, 1, SR, false);
        c.SetData(data, 0);
        return c;
    }

    // 波形（0..1 の位相で回す）
    private static float Sin(float t, float f) { return Mathf.Sin(t * f * 2f * Mathf.PI); }
    private static float Sq(float t, float f) { return Mathf.Repeat(t * f, 1f) < 0.5f ? 1f : -1f; }
    private static float Saw(float t, float f) { return Mathf.Repeat(t * f, 1f) * 2f - 1f; }
    private static float Tri(float t, float f) { float p = Mathf.Repeat(t * f, 1f); return (p < 0.5f ? p * 4f - 1f : 3f - p * 4f); }
    private static float Noise(float t) { return Random.value * 2f - 1f; }
    private static float Pulse(float t, float period) { return Mathf.Repeat(t, period) < period * 0.55f ? 1f : 0f; }
    /// <summary>立ち上がり attack 秒、その後 decay の速さで減衰。</summary>
    private static float Env(float t, float dur, float attack, float decay)
    {
        if (t < 0f || t > dur) return 0f;
        float a = attack <= 0f ? 1f : Mathf.Clamp01(t / attack);
        float k = Mathf.Clamp01(1f - t / dur);
        return a * Mathf.Pow(k, decay);
    }

    // ============================================================
    // 🎵 BGM（流しながら合成する）
    // ============================================================
    private static Bgm current = Bgm.None;
    private static AudioClip bgmClip;

    // ⚠ ここから下は**音のスレッド**から触られる。new しない・Unity API を呼ばない。
    private static int trackId;                 // 0=準備 1=戦闘 2=地上
    private static double phasePad0, phasePad1, phasePad2, phaseBass, phaseArp, phaseSub;
    private static long sampleClock;
    private static float arpEnv, kickEnv;
    private static int lastStep = -1;
    private static float arpFreq = 440f;

    // 和音の進行（Aマイナー）。根音の半音差と、三和音の形。
    private static readonly int[] chordRoot = { 0, -4, 3, -2 };        // Am - F - C - G
    private static readonly bool[] chordMinor = { true, false, false, false };
    private static readonly int[] arpSteps = { 0, 3, 7, 12, 7, 3 };    // 上って下りる

    public static void PlayBgm(Bgm b)
    {
        EnsureRoot();
        if (b == current) return;
        current = b;
        if (b == Bgm.None) { bgmSrc.Stop(); return; }
        trackId = b == Bgm.Prepare ? 0 : b == Bgm.Battle ? 1 : 2;
        if (bgmClip == null)
        {
            // 長さは見かけだけ（コールバックで無限に作る）。ループ再生で呼ばれ続ける。
            bgmClip = AudioClip.Create("bgm", BGM_SR * 10, 1, BGM_SR, true, OnPcm);
            bgmSrc.clip = bgmClip;
        }
        if (!bgmSrc.isPlaying) bgmSrc.Play();
        ApplyVolumes();
    }

    public static void StopBgm() { EnsureRoot(); current = Bgm.None; if (bgmSrc != null) bgmSrc.Stop(); }
    public static Bgm CurrentBgm { get { return current; } }

    private static void OnPcm(float[] data)
    {
        int track = trackId;
        // 1拍の長さ。戦闘は速く、地上はゆったり。
        double bpm = track == 1 ? 116.0 : track == 2 ? 74.0 : 62.0;
        double samplesPerStep = BGM_SR * 60.0 / bpm / 2.0;     // 8分音符きざみ
        int stepsPerChord = track == 1 ? 8 : 16;

        for (int i = 0; i < data.Length; i++)
        {
            long t = sampleClock + i;
            int step = (int)(t / samplesPerStep);
            int chord = (step / stepsPerChord) % chordRoot.Length;

            if (step != lastStep)
            {
                lastStep = step;
                int deg = arpSteps[Mathf.Abs(step) % arpSteps.Length];
                if (chordMinor[chord] && deg == 3) deg = 3; else if (!chordMinor[chord] && deg == 3) deg = 4;
                arpFreq = 220f * Pow2((chordRoot[chord] + deg) / 12f);
                // 戦闘は毎歩、他は4歩に1度だけ弾く（音数を減らすと安っぽくならない）
                if (track == 1 || step % 4 == 0) arpEnv = 1f;
                if (track == 1 && step % 4 == 0) kickEnv = 1f;
            }

            float root = 110f * Pow2(chordRoot[chord] / 12f);
            float third = root * Pow2((chordMinor[chord] ? 3 : 4) / 12f);
            float fifth = root * Pow2(7 / 12f);

            phasePad0 += root * 2.0 / BGM_SR; if (phasePad0 > 2.0) phasePad0 -= 2.0;
            phasePad1 += third * 2.0 / BGM_SR; if (phasePad1 > 2.0) phasePad1 -= 2.0;
            phasePad2 += fifth * 2.0 / BGM_SR; if (phasePad2 > 2.0) phasePad2 -= 2.0;
            phaseBass += root * 0.5 * 2.0 / BGM_SR; if (phaseBass > 2.0) phaseBass -= 2.0;
            phaseArp += arpFreq * 2.0 / BGM_SR; if (phaseArp > 2.0) phaseArp -= 2.0;
            phaseSub += 55.0 * 2.0 / BGM_SR; if (phaseSub > 2.0) phaseSub -= 2.0;

            // 敷き音（三和音のパッド）
            float pad = (SinP(phasePad0) + SinP(phasePad1) * 0.75f + SinP(phasePad2) * 0.6f) * 0.16f;
            // 低音
            float bass = SinP(phaseBass) * 0.22f;
            // 旋律（減衰する撥弦）
            float arp = (SinP(phaseArp) + SinP(phaseArp * 2.0) * 0.25f) * arpEnv * (track == 1 ? 0.16f : 0.11f);
            arpEnv *= track == 1 ? 0.99985f : 0.99992f;
            // 戦闘だけ低い鼓動
            float kick = 0f;
            if (track == 1) { kick = SinP(phaseSub) * kickEnv * 0.30f; kickEnv *= 0.9993f; }

            float v = pad + bass + arp + kick;
            // 少しだけ揺らす（機械的な平坦さを消す）
            v *= 0.92f + 0.08f * Mathf.Sin((float)(t / (double)BGM_SR) * 0.35f);
            data[i] = Mathf.Clamp(v, -1f, 1f);
        }
        sampleClock += data.Length;
    }

    private static float SinP(double phase) { return Mathf.Sin((float)(phase * Mathf.PI)); }
    private static float Pow2(float x) { return Mathf.Pow(2f, x); }
}
