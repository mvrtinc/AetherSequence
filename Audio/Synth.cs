using System.Numerics;

namespace AetherSequence.Audio;

internal static class Synth
{
    public const int Rate = 44100;

    public const float Tau = MathF.PI * 2f;

    public static float[] Buffer(float seconds) => new float[Math.Max(1, (int)(seconds * Rate))];

    public static double Semi(int semitones) => 55.0 * Math.Pow(2.0, semitones / 12.0);

    public static float Sine(double phase) => (float)Math.Sin(phase);

    public static float Tri(double phase)
    {
        double t = phase / (Math.PI * 2.0);
        t -= (float)Math.Floor(t);
        return (float)(t < 0.5 ? 4.0 * t - 1.0 : 3.0 - 4.0 * t);
    }

    public static float Saw(double phase)
    {
        double t = phase / (Math.PI * 2.0);
        t -= (float)Math.Floor(t);
        return (float)(2.0 * t - 1.0);
    }

    public static float Square(double phase, double width = 0.5)
    {
        double t = phase / (Math.PI * 2.0);
        t -= (float)Math.Floor(t);
        return t < width ? 1f : -1f;
    }

    public static float Noise(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state & 0xFFFFFF) / 8388608f - 1f;
    }

    public static float Env(int index, int total, float attack, float curve)
    {
        if (index < 0 || index >= total) return 0f;
        float t = index / (float)total;
        float a = attack <= 0f ? 1f : MathF.Min(1f, index / attack);
        return a * (float)Math.Pow(1f - t, curve);
    }

    public static float OnePole(float[] data, int index, float state, float coefficient, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            state += coefficient * (data[i] - state);
            data[i] = state;
        }
        return state;
    }

    public static void Add(float[] data, int index, float value)
    {
        if (index >= 0 && index < data.Length) data[index] += value;
    }

    public static void AddStereo(float[] data, int index, float left, float right)
    {
        if (index < 0 || index >= data.Length) return;
        data[index] += left;
        data[index + 1] += right;
    }

    public static void FadeEdges(float[] data, int fadeIn, int fadeOut)
    {
        int n = data.Length;
        for (int i = 0; i < n; i++)
        {
            float g = 1f;
            if (i < fadeIn) g *= i / (float)Math.Max(1, fadeIn);
            if (i > n - fadeOut) g *= (n - i) / (float)Math.Max(1, fadeOut);
            data[i] *= g;
        }
    }

    public static void Normalize(float[] data, float peak)
    {
        float max = 0.0001f;
        for (int i = 0; i < data.Length; i++) max = MathF.Max(max, MathF.Abs(data[i]));
        if (max < 0.0001f) return;
        float k = peak / max;
        for (int i = 0; i < data.Length; i++) data[i] *= k;
    }
}
