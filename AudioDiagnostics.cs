using AetherSequence.Audio;
using AetherSequence.Combat;
using AetherSequence.Core;

namespace AetherSequence;

internal static class AudioDiagnostics
{
    public static void Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== AETHER SEQUENCE audio diagnostics ===");

        int problems = 0;
        long total = 0;

        foreach (Sfx id in Enum.GetValues<Sfx>())
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            float[] data = AudioSystem.RenderSfx(id);
            long ms = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000 / System.Diagnostics.Stopwatch.Frequency;
            total += ms;

            float peak = 0f;
            double energy = 0;
            for (int i = 0; i < data.Length; i++)
            {
                peak = MathF.Max(peak, MathF.Abs(data[i]));
                energy += data[i] * data[i];
            }
            float rms = (float)Math.Sqrt(energy / Math.Max(1, data.Length));
            string verdict = "ok";
            if (data.Length == 0) { verdict = "EMPTY!"; problems++; }
            else if (peak < 0.05f) { verdict = "SILENT!"; problems++; }
            else if (peak > 1.2f) { verdict = "CLIPPING!"; problems++; }
            else if (rms < 0.01f) { verdict = "weak"; problems++; }
            Console.WriteLine($"[sfx] {id,-12} {data.Length / 44100f,5:0.00}s peak={peak:0.00} rms={rms:0.000} render={ms,3}ms  {verdict}");
        }

        foreach (MusicTrack track in Enum.GetValues<MusicTrack>())
        {
            if (track == MusicTrack.None) continue;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            float[] data = AudioSystem.RenderMusic(track);
            long ms = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000 / System.Diagnostics.Stopwatch.Frequency;

            float peak = 0f;
            double energy = 0;
            for (int i = 0; i < data.Length; i += 2)
            {
                float v = MathF.Max(MathF.Abs(data[i]), MathF.Abs(data[i + 1]));
                peak = MathF.Max(peak, v);
                energy += v * v;
            }
            float rms = (float)Math.Sqrt(energy / Math.Max(1, data.Length / 2));
            string verdict = "ok";
            if (peak < 0.05f || data.Length < 1000) { verdict = "BROKEN!"; problems++; }
            else if (peak > 1.2f) { verdict = "CLIPPING!"; problems++; }
            Console.WriteLine($"[mus] {track,-8} peak={peak:0.00} rms={rms:0.000} render={ms,3}ms ram={data.Length * 4 / 1024}KB  {verdict}");
        }

        Console.WriteLine($"[sum] все SFX отрисованы за {total}ms");
        Console.WriteLine(problems == 0 ? "=== audio ok ===" : $"=== ПРОБЛЕМ: {problems} ===");
    }
}
