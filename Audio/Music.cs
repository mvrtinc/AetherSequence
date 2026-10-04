namespace AetherSequence.Audio;

internal enum MusicTrack
{
    None,
    Menu,
    Dungeon,
    Boss,
    Victory,
}

internal static class MusicBank
{
    public static float[] RenderStereo(MusicTrack track, int rate = Synth.Rate)
    {
        float[] mono = Render(track, rate);
        int frames = mono.Length;
        float[] stereo = new float[frames * 2];
        int delay = (int)(0.008f * rate);
        for (int i = 0; i < frames; i++)
        {
            stereo[i * 2] += mono[i];
            int shifted = i - delay;
            stereo[(i * 2) + 1] += shifted >= 0 ? mono[shifted] * 0.92f : 0f;
        }
        return stereo;
    }

    private static float[] Render(MusicTrack track, int rate)
    {
        switch (track)
        {
            case MusicTrack.Menu: return Menu(rate);
            case MusicTrack.Dungeon: return Dungeon(rate);
            case MusicTrack.Boss: return Boss(rate);
            case MusicTrack.Victory: return VictoryLoop(rate);
            default: return new float[1];
        }
    }

    private static readonly int[][] AmProgression = { new[] { 0, 3, 7 }, new[] { -4, 0, 5 }, new[] { 3, 7, 10 }, new[] { -2, 2, 7 } };

    private static float[] Menu(int rate)
    {
        const int bpm = 76;
        int bar = BeatSamples(bpm, rate);
        float[] data = new float[bar * 4];

        for (int b = 0; b < 4; b++)
        {
            int[] chord = AmProgression[b];
            AddPad(data, b * bar, bar, chord, 0.32f, rate);
            for (int step = 0; step < 8; step++)
            {
                int semi = chord[(step + b) % chord.Length] + (step >= 4 ? 12 : 0);
                float amp = step % 2 == 0 ? 0.2f : 0.12f;
                AddPluck(data, b * bar + (step * bar / 8), bar / 6, (float)Synth.Semi(semi + 24), amp, 3.2f);
            }
            if (b % 2 == 1)
            {
                AddBell(data, b * bar + bar / 2, (float)Synth.Semi(chord[0] + 36), 0.16f);
            }
        }

        Finish(data, 0.5f, 0.12f);
        return data;
    }

    private static float[] Dungeon(int rate)
    {
        const int bpm = 142;
        int bar = BeatSamples(bpm, rate);
        float[] data = new float[bar * 4];

        for (int b = 0; b < 4; b++)
        {
            int[] chord = AmProgression[b];
            int baseIndex = b * bar;
            AddBass(data, baseIndex, bar / 8, (float)Synth.Semi(chord[0] - 12), 0.5f);
            AddBass(data, baseIndex + bar / 4, bar / 8, (float)Synth.Semi(chord[0] - 12), 0.42f);
            AddBass(data, baseIndex + bar / 2, bar / 8, (float)Synth.Semi(chord[0] - 12), 0.5f);
            AddBass(data, baseIndex + (bar * 3) / 4, bar / 8, (float)Synth.Semi(chord[0] - 5), 0.4f);

            for (int step = 0; step < 16; step++)
            {
                int semi = chord[step % chord.Length] + ((step / 4) % 2 == 0 ? 12 : 24);
                AddPluck(data, baseIndex + (step * bar / 16), bar / 12, (float)Synth.Semi(semi), step % 4 == 0 ? 0.18f : 0.1f, 4.5f);
            }

            AddKick(data, baseIndex, 0.75f);
            AddKick(data, baseIndex + bar / 2, 0.7f);
            AddSnare(data, baseIndex + bar / 4, 0.32f);
            AddSnare(data, baseIndex + (bar * 3) / 4, 0.32f);
            for (int step = 0; step < 8; step++)
            {
                AddHat(data, baseIndex + (step * bar / 8), step % 2 == 0 ? 0.14f : 0.08f);
            }
        }

        Finish(data, 0.62f, 0.05f);
        return data;
    }

    private static float[] Boss(int rate)
    {
        const int bpm = 162;
        int bar = BeatSamples(bpm, rate);
        float[] data = new float[bar * 4];
        int[] roots = { 0, 6, 0, -1 };

        for (int b = 0; b < 4; b++)
        {
            int root = roots[b];
            int baseIndex = b * bar;
            for (int step = 0; step < 8; step++)
            {
                float freq = (float)Synth.Semi(root - 12 + (step == 5 ? 6 : 0));
                AddBass(data, baseIndex + (step * bar / 8), bar / 10, freq, step % 2 == 0 ? 0.6f : 0.45f);
            }
            for (int step = 0; step < 4; step++)
            {
                int semi = root + (step % 2 == 0 ? 12 : 18);
                AddPluck(data, baseIndex + (step * bar / 4), bar / 5, (float)Synth.Semi(semi), 0.16f, 3.8f);
                AddPluck(data, baseIndex + (step * bar / 4) + (bar / 8), bar / 6, (float)Synth.Semi(semi + 1), 0.09f, 4.2f);
            }
            for (int beat = 0; beat < 4; beat++)
            {
                AddKick(data, baseIndex + (beat * bar / 4), beat == 0 ? 0.85f : 0.6f);
            }
            AddSnare(data, baseIndex + bar / 4, 0.38f);
            AddSnare(data, baseIndex + (bar * 3) / 4, 0.4f);
            for (int step = 0; step < 16; step++)
            {
                AddHat(data, baseIndex + (step * bar / 16), step % 4 == 0 ? 0.1f : 0.05f);
            }
        }

        Finish(data, 0.66f, 0.04f);
        return data;
    }

    private static float[] VictoryLoop(int rate)
    {
        const int bpm = 112;
        int bar = BeatSamples(bpm, rate);
        float[] data = new float[bar * 4];
        int[] melody = { 0, 4, 7, 12, 7, 4, 0, 4 };

        for (int b = 0; b < 4; b++)
        {
            int[] chord = AmProgression[b];
            AddPad(data, b * bar, bar, chord, 0.3f, rate);
            for (int step = 0; step < 8; step++)
            {
                int semi = melody[(b * 8 + step) % melody.Length] + 12;
                AddPluck(data, b * bar + (step * bar / 8), bar / 5, (float)Synth.Semi(semi + 12), step % 2 == 0 ? 0.24f : 0.16f, 3f);
            }
            AddKick(data, b * bar, 0.5f);
            AddKick(data, b * bar + bar / 2, 0.4f);
            AddSnare(data, b * bar + bar / 4, 0.2f);
            AddSnare(data, b * bar + (bar * 3) / 4, 0.2f);
        }

        Finish(data, 0.58f, 0.06f);
        return data;
    }

    private static int BeatSamples(int bpm, int rate) => (int)(60.0 / bpm * rate * 4.0);

    private static void AddPad(float[] data, int start, int length, int[] chord, float amp, int rate)
    {
        int attack = length / 3;
        int release = length / 2;
        for (int note = 0; note < chord.Length; note++)
        {
            double freq = Synth.Semi(chord[note] - 12);
            double detune = Synth.Semi(chord[note] - 12) * 1.004;
            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index >= data.Length) break;
                double p1 = Synth.Tau * freq * i / Synth.Rate;
                double p2 = Synth.Tau * detune * i / Synth.Rate;
                float v = (Synth.Saw(p1) * 0.5f + Synth.Saw(p2) * 0.5f) * 0.5f;
                data[index] += v * amp * Env(attack, release, i, length);
            }
        }
    }

    private static float Env(int attack, int release, int i, int length)
    {
        float a = attack <= 0 ? 1f : MathF.Min(1f, i / (float)attack);
        int fromEnd = length - i;
        float r = release <= 0 ? 1f : MathF.Min(1f, fromEnd / (float)release);
        return a * r * r;
    }

    private static void AddBass(float[] data, int start, int length, float freq, float amp)
    {
        double phase = 0;
        for (int i = 0; i < length; i++)
        {
            int index = start + i;
            if (index >= data.Length) return;
            phase += Synth.Tau * freq / Synth.Rate;
            float v = Synth.Saw(phase) * 0.6f + Synth.Sine(phase * 0.5f) * 0.6f;
            data[index] += v * amp * Synth.Env(i, length, 30f, 1.6f);
        }
        int tail = Math.Min(length, data.Length - start);
        Synth.OnePole(data, 0, 0f, 0.25f, start, start + tail);
    }

    private static void AddPluck(float[] data, int start, int length, float freq, float amp, float decay)
    {
        double phase = 0;
        for (int i = 0; i < length; i++)
        {
            int index = start + i;
            if (index >= data.Length) return;
            phase += Synth.Tau * freq / Synth.Rate;
            float v = Synth.Saw(phase) * 0.35f + Synth.Tri(phase) * 0.5f + Synth.Sine(phase * 2.0) * 0.15f;
            data[index] += v * amp * Synth.Env(i, length, 20f, decay);
        }
    }

    private static void AddBell(float[] data, int start, float freq, float amp)
    {
        int length = Math.Min((int)(1.2f * Synth.Rate), data.Length - start);
        for (int i = 0; i < length; i++)
        {
            int index = start + i;
            if (index < 0 || index >= data.Length) break;
            double p = Synth.Tau * freq * i / Synth.Rate;
            data[index] += (Synth.Sine(p) * 0.7f + Synth.Sine(p * 2.02f) * 0.2f) * amp * Synth.Env(i, length, 40f, 2.6f);
        }
    }

    private static void AddKick(float[] data, int start, float amp)
    {
        int length = Math.Min((int)(0.28f * Synth.Rate), data.Length - start);
        double phase = 0;
        for (int i = 0; i < length; i++)
        {
            int index = start + i;
            if (index < 0 || index >= data.Length) break;
            float t = i / (float)length;
            float freq = 150f * (float)Math.Pow(0.12, t);
            phase += Synth.Tau * freq / Synth.Rate;
            data[index] += Synth.Sine(phase) * amp * Synth.Env(i, length, 10f, 1.4f);
        }
    }

    private static void AddSnare(float[] data, int start, float amp)
    {
        int length = Math.Min((int)(0.22f * Synth.Rate), data.Length - start);
        uint r = 1234u;
        double phase = 0;
        for (int i = 0; i < length; i++)
        {
            int index = start + i;
            if (index < 0 || index >= data.Length) break;
            phase += Synth.Tau * 190f / Synth.Rate;
            float noise = Synth.Noise(ref r);
            data[index] += (noise * 0.75f + Synth.Sine(phase) * 0.3f) * amp * Synth.Env(i, length, 10f, 2.2f);
        }
    }

    private static void AddHat(float[] data, int start, float amp)
    {
        int length = Math.Min((int)(0.07f * Synth.Rate), data.Length - start);
        uint r = 555u;
        float previous = 0f;
        for (int i = 0; i < length; i++)
        {
            int index = start + i;
            if (index < 0 || index >= data.Length) break;
            float noise = Synth.Noise(ref r);
            float high = noise - previous;
            previous = noise;
            data[index] += high * amp * Synth.Env(i, length, 5f, 3f);
        }
    }

    private static void Finish(float[] data, float peak, float fade)
    {
        int fadeSamples = (int)(fade * Synth.Rate);
        for (int i = 0; i < fadeSamples; i++)
        {
            float k = i / (float)fadeSamples;
            data[i] *= k;
            data[data.Length - 1 - i] *= k;
        }
        Synth.Normalize(data, peak);
    }
}
