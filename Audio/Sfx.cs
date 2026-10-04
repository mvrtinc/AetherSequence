namespace AetherSequence.Audio;

internal enum Sfx
{
    Spark,
    CastFire,
    CastWind,
    CastWater,
    CastEarth,
    CastDark,
    CastLight,
    Fusion,
    Dash,
    PlayerHurt,
    EnemyHurt,
    EnemyDeath,
    LevelUp,
    CardPick,
    Pickup,
    PickupMana,
    PickupHealth,
    Portal,
    Telegraph,
    BossRoar,
    BossPhase,
    Victory,
    Death,
    UiMove,
    UiSelect,
    UiBack,
}

internal static class SfxBank
{
    public static float[] Render(Sfx id) => id switch
    {
        Sfx.Spark => Spark(),
        Sfx.CastFire => Fire(),
        Sfx.CastWind => Wind(),
        Sfx.CastWater => Water(),
        Sfx.CastEarth => Earth(),
        Sfx.CastDark => Dark(),
        Sfx.CastLight => Light(),
        Sfx.Fusion => Fusion(),
        Sfx.Dash => Dash(),
        Sfx.PlayerHurt => PlayerHurt(),
        Sfx.EnemyHurt => EnemyHurt(),
        Sfx.EnemyDeath => EnemyDeath(),
        Sfx.LevelUp => LevelUp(),
        Sfx.CardPick => CardPick(),
        Sfx.Pickup => Blip(880f, 0.09f, 0.35f, 3f),
        Sfx.PickupMana => Blip(1320f, 0.11f, 0.3f, 2.5f),
        Sfx.PickupHealth => Blip(660f, 0.16f, 0.3f, 2f),
        Sfx.Portal => Portal(),
        Sfx.Telegraph => Telegraph(),
        Sfx.BossRoar => BossRoar(),
        Sfx.BossPhase => BossPhase(),
        Sfx.Victory => Victory(),
        Sfx.Death => Death(),
        Sfx.UiMove => Blip(1600f, 0.035f, 0.18f, 6f),
        Sfx.UiSelect => TwoNotes(660f, 990f, 0.06f, 0.3f),
        Sfx.UiBack => TwoNotes(660f, 440f, 0.07f, 0.28f),
        _ => Spark(),
    };

    private static float[] Spark()
    {
        int n = (int)(0.18f * Synth.Rate);
        float[] b = new float[n];
        uint r = 12345u;
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float freq = 900f - 620f * t;
            phase += Synth.Tau * freq / Synth.Rate;
            float noise = Synth.Noise(ref r) * 0.35f;
            b[i] = (Synth.Sine(phase) * 0.8f + noise) * Synth.Env(i, n, 60f, 3.2f);
        }
        Synth.Normalize(b, 0.55f);
        return b;
    }

    private static float[] Fire()
    {
        int n = (int)(0.55f * Synth.Rate);
        float[] b = new float[n];
        uint r = 777u;
        double p1 = 0;
        double p2 = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            p1 += Synth.Tau * (420f - 340f * t) / Synth.Rate;
            p2 += Synth.Tau * (110f - 40f * t) / Synth.Rate;
            float noise = Synth.Noise(ref r);
            float body = Synth.Sine(p1) * 0.5f + Synth.Saw(p2) * 0.35f;
            float crackle = noise * noise * 0.5f * (1f - t);
            b[i] = (body + crackle) * Synth.Env(i, n, 120f, 2.4f);
        }
        Synth.FadeEdges(b, 200, (int)(0.12f * Synth.Rate));
        Synth.Normalize(b, 0.8f);
        return b;
    }

    private static float[] Wind()
    {
        int n = (int)(0.5f * Synth.Rate);
        float[] b = new float[n];
        uint r = 4242u;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            b[i] = Synth.Noise(ref r) * (0.35f + 0.65f * MathF.Sin(MathF.PI * t));
        }
        float state = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float cutoff = 0.04f + 0.32f * MathF.Sin(MathF.PI * t);
            state += cutoff * (b[i] - state);
            b[i] = state;
        }
        Synth.FadeEdges(b, (int)(0.08f * Synth.Rate), (int)(0.14f * Synth.Rate));
        Synth.Normalize(b, 0.7f);
        return b;
    }

    private static float[] Water()
    {
        int n = (int)(0.42f * Synth.Rate);
        float[] b = new float[n];
        double p1 = 0;
        double p2 = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float freq = 1500f * (float)Math.Pow(0.22, t);
            p1 += Synth.Tau * freq / Synth.Rate;
            p2 += Synth.Tau * (freq * 1.5f + 40f * MathF.Sin(t * 40f)) / Synth.Rate;
            float body = Synth.Sine(p1);
            float bubble = Synth.Sine(p2) * (1f - t) * 0.5f;
            b[i] = (body * 0.7f + bubble) * Synth.Env(i, n, 40f, 2.6f);
        }
        Synth.FadeEdges(b, 100, (int)(0.1f * Synth.Rate));
        Synth.Normalize(b, 0.7f);
        return b;
    }

    private static float[] Earth()
    {
        int n = (int)(0.6f * Synth.Rate);
        float[] b = new float[n];
        uint r = 99u;
        double p1 = 0;
        double p2 = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            p1 += Synth.Tau * (150f - 90f * t) / Synth.Rate;
            p2 += Synth.Tau * (75f - 25f * t) / Synth.Rate;
            float rumble = Synth.Sine(p1) * 0.5f + Synth.Sine(p2) * 0.8f;
            float grit = Synth.Noise(ref r) * 0.3f * (1f - t);
            b[i] = (rumble + grit) * Synth.Env(i, n, 200f, 1.9f);
        }
        Synth.FadeEdges(b, 300, (int)(0.16f * Synth.Rate));
        Synth.Normalize(b, 0.9f);
        return b;
    }

    private static float[] Dark()
    {
        int n = (int)(0.62f * Synth.Rate);
        float[] b = new float[n];
        double p1 = 0;
        double p2 = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float f = 620f - 480f * t;
            p1 += Synth.Tau * f / Synth.Rate;
            p2 += Synth.Tau * (f * 1.013f) / Synth.Rate;
            float detune = Synth.Saw(p1) * 0.5f + Synth.Saw(p2) * 0.5f;
            float sub = Synth.Sine(p1 * 0.5f) * 0.4f;
            b[i] = (detune + sub) * Synth.Env(i, n, 80f, 2.2f);
        }
        Synth.FadeEdges(b, 120, (int)(0.2f * Synth.Rate));
        Synth.Normalize(b, 0.75f);
        return b;
    }

    private static float[] Light()
    {
        int n = (int)(0.85f * Synth.Rate);
        float[] b = new float[n];
        double p1 = 0;
        double p2 = 0;
        double p3 = 0;
        for (int i = 0; i < n; i++)
        {
            p1 += Synth.Tau * 1174.66f / Synth.Rate;
            p2 += Synth.Tau * 1760f / Synth.Rate;
            p3 += Synth.Tau * 2637f / Synth.Rate;
            float bell = Synth.Sine(p1) * 0.5f + Synth.Sine(p2) * 0.3f + Synth.Sine(p3) * 0.15f;
            b[i] = bell * Synth.Env(i, n, 30f, 2.4f);
        }
        Synth.FadeEdges(b, 60, (int)(0.25f * Synth.Rate));
        Synth.Normalize(b, 0.65f);
        return b;
    }

    private static float[] Fusion()
    {
        int n = (int)(1.15f * Synth.Rate);
        float[] b = new float[n];
        uint r = 31337u;
        double p1 = 0;
        double p2 = 0;
        double p3 = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float rise = 1f - t;
            p1 += Synth.Tau * (180f + 900f * (1f - rise) * (1f - rise)) / Synth.Rate;
            p2 += Synth.Tau * (268f + 620f * (1f - rise)) / Synth.Rate;
            p3 += Synth.Tau * (402f) / Synth.Rate;
            float swell = Synth.Sine(p1) * 0.55f + Synth.Sine(p2) * 0.35f + Synth.Sine(p3) * 0.2f;
            float noise = Synth.Noise(ref r) * noiseWeight(t);
            b[i] = (swell + noise * 0.45f) * Synth.Env(i, n, 220f, 1.6f);
        }
        Synth.FadeEdges(b, 400, (int)(0.3f * Synth.Rate));
        Synth.Normalize(b, 1f);
        return b;
    }

    private static float noiseWeight(float t) => MathF.Sin(MathF.PI * MathF.Min(1f, t * 1.6f)) * 0.9f;

    private static float[] Dash()
    {
        int n = (int)(0.3f * Synth.Rate);
        float[] b = new float[n];
        uint r = 8080u;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            b[i] = Synth.Noise(ref r) * MathF.Sin(MathF.PI * t);
        }
        float state = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float cutoff = 0.5f - 0.42f * t;
            state += cutoff * (b[i] - state);
            b[i] = state;
        }
        Synth.Normalize(b, 0.6f);
        return b;
    }

    private static float[] PlayerHurt()
    {
        int n = (int)(0.45f * Synth.Rate);
        float[] b = new float[n];
        uint r = 5150u;
        double p = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            p += Synth.Tau * (220f - 130f * t) / Synth.Rate;
            float body = Synth.Square(p, 0.35f) * 0.4f + Synth.Sine(p * 0.5f) * 0.6f;
            float grit = Synth.Noise(ref r) * 0.35f * (1f - t);
            b[i] = (body + grit) * Synth.Env(i, n, 40f, 2.2f);
        }
        Synth.FadeEdges(b, 60, (int)(0.12f * Synth.Rate));
        Synth.Normalize(b, 0.85f);
        return b;
    }

    private static float[] EnemyHurt()
    {
        int n = (int)(0.11f * Synth.Rate);
        float[] b = new float[n];
        double p = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            p += Synth.Tau * (760f - 320f * t) / Synth.Rate;
            b[i] = Synth.Square(p, 0.4f) * Synth.Env(i, n, 20f, 3.5f);
        }
        Synth.Normalize(b, 0.4f);
        return b;
    }

    private static float[] EnemyDeath()
    {
        int n = (int)(0.5f * Synth.Rate);
        float[] b = new float[n];
        uint r = 246810u;
        double p = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            p += Synth.Tau * (480f - 400f * t) / Synth.Rate;
            float crunch = Synth.Noise(ref r) * 0.75f;
            float body = Synth.Square(p, 0.2f) * 0.35f;
            b[i] = (crunch + body) * Synth.Env(i, n, 30f, 2.2f);
        }
        float state = 0f;
        Synth.OnePole(b, 0, state, 0.35f, 0, n);
        Synth.Normalize(b, 0.75f);
        return b;
    }

    private static float[] LevelUp()
    {
        int n = (int)(1.1f * Synth.Rate);
        float[] b = new float[n];
        int[] semis = { 0, 3, 7, 12, 19 };
        for (int note = 0; note < semis.Length; note++)
        {
            int start = note * (int)(0.11f * Synth.Rate);
            float freq = (float)Synth.Semi(semis[note] + 24);
            int len = n - start;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float v = Synth.Sine(Synth.Tau * freq * i / Synth.Rate) * 0.6f
                    + Synth.Sine(Synth.Tau * freq * 2f * i / Synth.Rate) * 0.25f;
                b[start + i] += v * Synth.Env(i, len, 60f, 2.6f);
            }
        }
        Synth.FadeEdges(b, 80, (int)(0.2f * Synth.Rate));
        Synth.Normalize(b, 0.6f);
        return b;
    }

    private static float[] CardPick()
    {
        return TwoNotes(784f, 1175f, 0.13f, 0.3f);
    }

    private static float[] TwoNotes(float first, float second, float length, float amp)
    {
        int n = (int)(length * 2.2f * Synth.Rate);
        float[] b = new float[n];
        int half = n / 2;
        AddTone(b, 0, half, first, amp);
        AddTone(b, half, n - half, second, amp);
        Synth.FadeEdges(b, 60, (int)(0.1f * Synth.Rate));
        Synth.Normalize(b, 0.6f);
        return b;
    }

    private static void AddTone(float[] data, int start, int length, float freq, float amp)
    {
        for (int i = 0; i < length; i++)
        {
            int index = start + i;
            if (index >= data.Length) break;
            double p = Synth.Tau * freq * i / Synth.Rate;
            data[index] += (Synth.Sine(p) * 0.7f + Synth.Sine(p * 2.0) * 0.3f) * Synth.Env(i, length, 40f, 2.4f) * amp;
        }
    }

    private static float[] Blip(float freq, float length, float amp, float decay)
    {
        int n = Math.Max(64, (int)(length * Synth.Rate));
        float[] b = new float[n];
        double p = 0;
        for (int i = 0; i < n; i++)
        {
            p += Synth.Tau * freq * i / Synth.Rate;
            b[i] = Synth.Tri(p) * Synth.Env(i, n, 20f, decay) * amp;
        }
        Synth.Normalize(b, amp);
        return b;
    }

    private static float[] Portal()
    {
        int n = (int)(1.4f * Synth.Rate);
        float[] b = new float[n];
        double p1 = 0;
        double p2 = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float f = 220f + 660f * t;
            p1 += Synth.Tau * f / Synth.Rate;
            p2 += Synth.Tau * f * 1.5f / Synth.Rate;
            float shimmer = Synth.Sine(p1) * 0.5f + Synth.Sine(p2) * 0.3f + Synth.Sine(p1 * 2f) * 0.2f;
            b[i] = shimmer * Synth.Env(i, n, 400f, 1.2f);
        }
        Synth.FadeEdges(b, 500, (int)(0.3f * Synth.Rate));
        Synth.Normalize(b, 0.55f);
        return b;
    }

    private static float[] Telegraph()
    {
        int n = (int)(0.7f * Synth.Rate);
        float[] b = new float[n];
        double p = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            p += Synth.Tau * 104f / Synth.Rate;
            float tremolo = 0.55f + 0.45f * MathF.Sin(t * 40f);
            b[i] = (Synth.Saw(p) * 0.35f + Synth.Sine(p * 2.0) * 0.4f) * tremolo * Synth.Env(i, n, 200f, 1.1f);
        }
        Synth.FadeEdges(b, 250, (int)(0.18f * Synth.Rate));
        Synth.Normalize(b, 0.7f);
        return b;
    }

    private static float[] BossRoar()
    {
        int n = (int)(1.3f * Synth.Rate);
        float[] b = new float[n];
        uint r = 6161u;
        double p1 = 0;
        double p2 = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float vib = 1f + 0.02f * MathF.Sin(t * 60f);
            p1 += Synth.Tau * 68f * vib / Synth.Rate;
            p2 += Synth.Tau * 103f * vib / Synth.Rate;
            float growl = Synth.Saw(p1) * 0.5f + Synth.Saw(p2) * 0.4f;
            float breath = Synth.Noise(ref r) * 0.25f;
            float env = MathF.Min(1f, t * 6f) * (1f - t * t);
            b[i] = (growl + breath) * env;
        }
        Synth.FadeEdges(b, 200, (int)(0.3f * Synth.Rate));
        Synth.Normalize(b, 0.9f);
        return b;
    }

    private static float[] BossPhase()
    {
        int n = (int)(1.1f * Synth.Rate);
        float[] b = new float[n];
        int[] semis = { 0, 1, 6, 12 };
        for (int note = 0; note < semis.Length; note++)
        {
            int start = note * (int)(0.16f * Synth.Rate);
            float freq = (float)Synth.Semi(semis[note] + 12);
            int len = n - start;
            for (int i = 0; i < len; i++)
            {
                double p = Synth.Tau * freq * i / Synth.Rate;
                b[start + i] += (Synth.Saw(p) * 0.5f + Synth.Square(p, 0.3f) * 0.3f) * Synth.Env(i, len, 80f, 1.8f) * 0.5f;
            }
        }
        Synth.FadeEdges(b, 100, (int)(0.25f * Synth.Rate));
        Synth.Normalize(b, 0.85f);
        return b;
    }

    private static float[] Victory()
    {
        int n = (int)(2.2f * Synth.Rate);
        float[] b = new float[n];
        int[] melody = { 0, 4, 7, 12, 16, 19 };
        for (int note = 0; note < melody.Length; note++)
        {
            int start = note * (int)(0.17f * Synth.Rate);
            float freq = (float)Synth.Semi(melody[note] + 24);
            int len = Math.Min(n - start, (int)(0.75f * Synth.Rate));
            for (int i = 0; i < len; i++)
            {
                double p = Synth.Tau * freq * i / Synth.Rate;
                b[start + i] += (Synth.Sine(p) * 0.6f + Synth.Tri(p) * 0.25f) * Synth.Env(i, len, 50f, 2.2f) * 0.55f;
            }
        }
        for (int i = 0; i < n; i++)
        {
            double p = Synth.Tau * (float)Synth.Semi(12) * i / Synth.Rate;
            float pad = Synth.Sine(p) * 0.3f + Synth.Sine(p * 1.5f) * 0.2f;
            b[i] += pad * MathF.Min(1f, i / 4000f) * (1f - i / (float)n * 0.3f);
        }
        Synth.FadeEdges(b, 100, (int)(0.4f * Synth.Rate));
        Synth.Normalize(b, 0.7f);
        return b;
    }

    private static float[] Death()
    {
        int n = (int)(2.4f * Synth.Rate);
        float[] b = new float[n];
        uint r = 13u;
        double p1 = 0;
        double p2 = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float f = 180f * (1f - 0.62f * t);
            p1 += Synth.Tau * f / Synth.Rate;
            p2 += Synth.Tau * f * 1.011f / Synth.Rate;
            float drone = Synth.Saw(p1) * 0.4f + Synth.Saw(p2) * 0.35f;
            float air = Synth.Noise(ref r) * 0.2f * (1f - t);
            b[i] = (drone + air) * Synth.Env(i, n, 300f, 0.9f);
        }
        Synth.FadeEdges(b, 300, (int)(0.6f * Synth.Rate));
        Synth.Normalize(b, 0.8f);
        return b;
    }
}
