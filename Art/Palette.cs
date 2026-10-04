using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using AetherSequence.Core;

namespace AetherSequence.Art;

internal static class Palette
{
    public static readonly Color Void = Color.FromArgb(8, 7, 14);
    public static readonly Color Floor = Color.FromArgb(44, 39, 62);
    public static readonly Color FloorAlt = Color.FromArgb(52, 46, 74);
    public static readonly Color FloorLine = Color.FromArgb(34, 29, 50);
    public static readonly Color WallBody = Color.FromArgb(40, 35, 58);
    public static readonly Color WallTop = Color.FromArgb(88, 76, 122);
    public static readonly Color WallEdge = Color.FromArgb(116, 102, 164);
    public static readonly Color WallShadow = Color.FromArgb(18, 15, 28);
    public static readonly Color Pillar = Color.FromArgb(58, 50, 84);
    public static readonly Color PillarTop = Color.FromArgb(90, 80, 130);
    public static readonly Color ExitClosed = Color.FromArgb(60, 40, 80);
    public static readonly Color ExitOpen = Color.FromArgb(150, 220, 255);

    public static readonly Color Health = Color.FromArgb(226, 74, 92);
    public static readonly Color Mana = Color.FromArgb(86, 156, 255);
    public static readonly Color Xp = Color.FromArgb(140, 226, 130);
    public static readonly Color Flow = Color.FromArgb(255, 214, 120);
    public static readonly Color Gold = Color.FromArgb(255, 208, 96);
    public static readonly Color Panel = Color.FromArgb(20, 17, 30);
    public static readonly Color PanelEdge = Color.FromArgb(96, 84, 138);
    public static readonly Color Paper = Color.FromArgb(232, 228, 244);
    public static readonly Color Muted = Color.FromArgb(150, 144, 178);
    public static readonly Color Danger = Color.FromArgb(255, 96, 72);

    private static readonly ConcurrentDictionary<int, SolidBrush> BrushCache = new();

    /// <summary>
    /// Кисть для полупрозрачной заливки с квантованием альфы.
    /// Свет и пыль рисуются десятками эллипсов за кадр; если создавать для
    /// каждого новую SolidBrush, на кадре получаются сотни аллокаций.
    /// Альфа округляется до шагов, поэтому количество кистей ограничено.
    /// </summary>
    private static readonly ConcurrentDictionary<int, SolidBrush> SoftBrushCache = new();

    private const int SoftAlphaSteps = 24;

    public static SolidBrush SoftBrush(Color color, float alpha)
    {
        int step = GameMath.ClampI((int)(alpha * SoftAlphaSteps) + 1, 1, SoftAlphaSteps);
        int key = (color.ToArgb() & 0x00FFFFFF) | (step << 28);
        return SoftBrushCache.GetOrAdd(key, _ => new SolidBrush(Color.FromArgb(step * 255 / SoftAlphaSteps, color)));
    }
    private static readonly ConcurrentDictionary<int, Pen> PenCache = new();
    private static readonly ConcurrentDictionary<int, PathGradientBrush> GlowBrushCache = new();
    private static PathGradientBrush? _vignetteBrush;
    private const int GlowSteps = 10;

    public static Color Fade(Color c, float alpha) => GameMath.Fade(c, alpha);

    public static Color Mix(Color a, Color b, float t) => GameMath.Mix(a, b, t);

    public static Color Shade(Color c, float factor) => GameMath.Shade(c, factor);

    public static Color Rgb(int r, int g, int b) => GameMath.Rgb(r, g, b);

    public static PointF ToPointF(Vector2 p) => new(p.X, p.Y);

    public static SolidBrush Brush(Color c) => BrushCache.GetOrAdd(c.ToArgb(), _ => new SolidBrush(c));

    public static Pen Pen(Color c) => PenCache.GetOrAdd(c.ToArgb(), _ => new Pen(new SolidBrush(c), 1f));

    public static void Fill(Graphics g, Color c, float x, float y, float w, float h)
        => g.FillRectangle(Brush(c), x, y, w, h);

    public static void Fill(Graphics g, Color c, RectangleF r) => g.FillRectangle(Brush(c), r);

    public static void Circle(Graphics g, Color c, float cx, float cy, float r)
    {
        if (r <= 0f) return;
        g.DrawEllipse(Pen(c), cx - r, cy - r, r * 2f, r * 2f);
    }

    public static void Disc(Graphics g, Color c, float cx, float cy, float r)
    {
        if (r <= 0f) return;
        g.FillEllipse(Brush(c), cx - r, cy - r, r * 2f, r * 2f);
    }

    public static void Stroke(Graphics g, Color c, float x, float y, float w, float h, float thickness = 1f)
    {
        float t = MathF.Max(0.6f, thickness);
        Fill(g, c, x, y, w, t);
        Fill(g, c, x, y + h - t, w, t);
        Fill(g, c, x, y + t, t, h - t * 2f);
        Fill(g, c, x + w - t, y + t, t, h - t * 2f);
    }

    public static void DrawVignette(Graphics g, float x, float y, float w, float h, float strength = 0.85f)
    {
        // Виньетка статична, поэтому рендерим её один раз в bitmap.
        // Радиальный градиент на весь кадр каждый кадр стоил слишком дорого.
        int iw = (int)MathF.Round(w);
        int ih = (int)MathF.Round(h);
        if (iw <= 0 || ih <= 0) return;
        int key = ((iw * 397) ^ ih) * 31 + (int)(strength * 100f);
        Bitmap? bmp = VignetteCache.GetOrAdd(key, _ => RenderVignette(iw, ih, strength));
        if (x == 0f && y == 0f) g.DrawImageUnscaled(bmp, 0, 0);
        else g.DrawImage(bmp, new RectangleF(x, y, w, h));
    }

    private static readonly ConcurrentDictionary<int, Bitmap> VignetteCache = new();

    private static Bitmap RenderVignette(int w, int h, float strength)
    {
        Bitmap bmp = new(w, h, PixelFormat.Format32bppPArgb);
        using Graphics vg = Graphics.FromImage(bmp);
        vg.CompositingMode = CompositingMode.SourceCopy;
        vg.Clear(Color.Transparent);
        vg.CompositingMode = CompositingMode.SourceOver;
        SmoothingMode previous = vg.SmoothingMode;
        vg.SmoothingMode = SmoothingMode.AntiAlias;
        vg.FillRectangle(VignetteBrush(strength), 0f, 0f, w, h);
        vg.SmoothingMode = previous;
        return bmp;
    }

    private static PathGradientBrush VignetteBrush(float strength)
    {
        if (_vignetteBrush is null)
        {
            _vignetteBrush = BuildRadialBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(215, 4, 3, 8), 1f);
        }
        return strength > 0.9f ? _vignetteBrush : ScaleVignette(_vignetteBrush, strength);
    }

    private static PathGradientBrush ScaleVignette(PathGradientBrush source, float strength)
    {
        int key = -(1000 + (int)(strength * 100f));
        return GlowBrushCache.GetOrAdd(key, _ =>
        {
            PathGradientBrush brush = (PathGradientBrush)source.Clone();
            Color[] surround = new Color[brush.SurroundColors.Length];
            for (int i = 0; i < surround.Length; i++)
            {
                Color c = source.SurroundColors[i];
                surround[i] = Color.FromArgb(GameMath.ClampI((int)(c.A * strength), 0, 255), c);
            }
            brush.SurroundColors = surround;
            return brush;
        });
    }

    public static void AddGlow(Graphics g, Vector2 pos, float radius, Color color, float alpha)
    {
        if (radius <= 0.5f || alpha <= 0.02f) return;
        BlitGlow(g, pos.X - radius, pos.Y - radius, radius * 2f, radius * 2f, color, alpha);
    }

    public static void AddGlowEllipse(Graphics g, Vector2 center, Vector2 radius, Color color, float alpha)
    {
        if (radius.X <= 0.5f || radius.Y <= 0.5f || alpha <= 0.02f) return;
        BlitGlow(g, center.X - radius.X, center.Y - radius.Y, radius.X * 2f, radius.Y * 2f, color, alpha);
    }

    private static void BlitGlow(Graphics g, float x, float y, float w, float h, Color color, float alpha)
    {
        if (GlowsDisabled) return;
        float cx = x + w * 0.5f;
        float cy = y + h * 0.5f;
        for (int i = 1; i <= GlowRings; i++)
        {
            float s = i / (float)GlowRings;
            float a = alpha * (1f - s);
            if (a <= 0.02f) continue;
            float rx = w * 0.5f * s;
            float ry = h * 0.5f * s;
            g.FillEllipse(GlowBrush(color, a), cx - rx, cy - ry, rx * 2f, ry * 2f);
        }
    }

    internal static bool GlowsDisabled;

    private static readonly ConcurrentDictionary<int, SolidBrush> GlowSolidCache = new();

    private const int GlowRings = 4;

    private static SolidBrush GlowBrush(Color color, float alpha)
    {
        int step = GameMath.ClampI((int)(alpha * GlowSteps) + 1, 1, GlowSteps);
        int key = (color.ToArgb() & 0x00FFFFFF) | (step << 28);
        return GlowSolidCache.GetOrAdd(key, _ => new SolidBrush(
            Palette.Fade(Color.FromArgb(255, color), step / (float)GlowSteps)));
    }

    private static PathGradientBrush BuildRadialBrush(Color center, Color surround, float centerScale)
    {
        GraphicsPath path = new();
        path.AddEllipse(-32f, -32f, 64f, 64f);
        PathGradientBrush brush = new(path);
        brush.CenterPoint = new PointF(0f, 0f);
        brush.CenterColor = center;
        brush.SurroundColors = new[] { surround };
        brush.FocusScales = new PointF(centerScale, centerScale);
        return brush;
    }

}
