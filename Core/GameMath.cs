using System.Drawing;
using System.Numerics;

namespace AetherSequence.Core;

internal static class GameMath
{
    public const float Tau = MathF.PI * 2f;

    public const float DegToRad = MathF.PI / 180f;

    public const float RadToDeg = 180f / MathF.PI;

    public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;

    public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    public static int ClampI(int v, int min, int max) => v < min ? min : v > max ? max : v;

    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public static float Approach(float v, float target, float step)
        => v < target ? MathF.Min(v + step, target) : MathF.Max(v - step, target);

    public static Vector2 Normalized(Vector2 v)
    {
        float l = v.Length();
        return l > 0.0001f ? v / l : Vector2.Zero;
    }

    public static Vector2 FromAngle(float a) => new(MathF.Cos(a), MathF.Sin(a));

    public static float AngleOf(Vector2 v) => MathF.Atan2(v.Y, v.X);

    public static Vector2 ClampLength(Vector2 v, float max)
    {
        float l = v.Length();
        return l > max && l > 0.0001f ? v * (max / l) : v;
    }

    public static Vector2 Rotate(Vector2 v, float rad)
    {
        float c = MathF.Cos(rad);
        float s = MathF.Sin(rad);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    public static float ApproachAngle(float from, float to, float maxStep)
    {
        float d = MathF.Atan2(MathF.Sin(to - from), MathF.Cos(to - from));
        return MathF.Abs(d) <= maxStep ? to : from + MathF.Sign(d) * maxStep;
    }

    public static bool CircleRect(Vector2 c, float r, float rx, float ry, float rw, float rh)
        => c.X + r > rx && c.X - r < rx + rw && c.Y + r > ry && c.Y - r < ry + rh;

    public static Color Mix(Color a, Color b, float t)
    {
        t = Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)Lerp(a.A, b.A, t),
            (int)Lerp(a.R, b.R, t),
            (int)Lerp(a.G, b.G, t),
            (int)Lerp(a.B, b.B, t));
    }

    public static Color Shade(Color c, float factor)
        => Color.FromArgb(c.A, ClampI((int)(c.R * factor), 0, 255), ClampI((int)(c.G * factor), 0, 255), ClampI((int)(c.B * factor), 0, 255));

    public static Color Fade(Color c, float alpha)
        => Color.FromArgb(ClampI((int)(c.A * alpha), 0, 255), c);

    public static Color Rgb(int r, int g, int b) => Color.FromArgb(255, r, g, b);
}
