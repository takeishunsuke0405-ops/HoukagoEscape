using UnityEngine;

/// <summary>
/// 音の素材がまだないので、プログラムで仮の効果音を作る。
/// 後で本物の音（.wav など）に置き換える。
/// </summary>
public static class ProceduralAudio
{
    const int SampleRate = 44100;

    /// <summary>
    /// 「トン」「ドン」という低いこもった音。
    /// decay が大きいほど短く、smoothing が小さいほど低くこもった音になる。
    /// </summary>
    public static AudioClip CreateThud(string name, float duration = 0.12f, float decay = 35f, float smoothing = 0.12f, int seed = 1)
    {
        int length = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[length];
        var random = new System.Random(seed);
        float filtered = 0f;
        float gain = 0.5f / Mathf.Sqrt(smoothing);

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            filtered += (noise - filtered) * smoothing;
            data[i] = Mathf.Clamp(filtered * gain * Mathf.Exp(-t * decay), -1f, 1f);
        }

        var clip = AudioClip.Create(name, length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>学校のチャイム「キーンコーンカーンコーン」（ウェストミンスターの鐘）</summary>
    public static AudioClip CreateChime(string name)
    {
        float[] notes = { 659.25f, 523.25f, 587.33f, 392f, 392f, 587.33f, 659.25f, 523.25f };
        const float noteInterval = 0.9f;
        const float phraseGap = 0.5f;
        const float ringTime = 2.5f;

        float duration = noteInterval * notes.Length + phraseGap + ringTime;
        int length = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[length];

        for (int n = 0; n < notes.Length; n++)
        {
            float start = n * noteInterval + (n >= 4 ? phraseGap : 0f);
            int startSample = Mathf.RoundToInt(start * SampleRate);
            int ringSamples = Mathf.RoundToInt(ringTime * SampleRate);
            float f = notes[n];

            for (int i = 0; i < ringSamples && startSample + i < length; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Exp(-t * 1.6f) * Mathf.Min(1f, t * 200f);
                float tone = Mathf.Sin(2f * Mathf.PI * f * t)
                    + 0.4f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * t) * Mathf.Exp(-t * 3f)
                    + 0.2f * Mathf.Sin(2f * Mathf.PI * f * 3f * t) * Mathf.Exp(-t * 5f);
                data[startSample + i] += tone * envelope * 0.22f;
            }
        }

        var clip = AudioClip.Create(name, length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
