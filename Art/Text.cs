using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using AetherSequence.Core;

namespace AetherSequence.Art;

internal static class Text
{
    private const string Family = "Consolas";

    private static readonly StringFormat LeftFormat = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
    private static readonly StringFormat CenterFormat = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
    private static readonly StringFormat RightFormat = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near };

    public static Font Tiny { get; private set; } = Create(10f, FontStyle.Regular);

    public static Font TinyBold { get; private set; } = Create(10f, FontStyle.Bold);

    public static Font Small { get; private set; } = Create(11f, FontStyle.Regular);

    public static Font SmallBold { get; private set; } = Create(11f, FontStyle.Bold);

    public static Font Medium { get; private set; } = Create(17f, FontStyle.Bold);

    public static Font Large { get; private set; } = Create(30f, FontStyle.Bold);

    public static Font Huge { get; private set; } = Create(44f, FontStyle.Bold);

    public static Font Title { get; private set; } = Create(60f, FontStyle.Bold);

    public static float UiScale { get; private set; } = 1f;

    public static void SetUiScale(float scale)
    {
        scale = GameMath.Clamp(scale, 0.8f, 1.6f);
        if (MathF.Abs(scale - UiScale) < 0.001f) return;
        UiScale = scale;
        Tiny = Create(10f * scale, FontStyle.Regular);
        TinyBold = Create(10f * scale, FontStyle.Bold);
        Small = Create(11f * scale, FontStyle.Regular);
        SmallBold = Create(11f * scale, FontStyle.Bold);
        Medium = Create(17f * scale, FontStyle.Bold);
        Large = Create(30f * scale, FontStyle.Bold);
        Huge = Create(44f * scale, FontStyle.Bold);
        Title = Create(60f * scale, FontStyle.Bold);
    }

    private static Font Create(float size, FontStyle style) => new(Family, size, style, GraphicsUnit.Pixel);

    public static void Prepare(Graphics g)
    {
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode = SmoothingMode.None;
    }

    public static void PrepareNative(Graphics g)
    {
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = SmoothingMode.None;
    }

    public static void Draw(Graphics g, string value, Font font, Color color, float x, float y)
        => g.DrawString(value, font, Palette.Brush(color), x, y, LeftFormat);

    public static void DrawCentered(Graphics g, string value, Font font, Color color, float centerX, float y)
        => g.DrawString(value, font, Palette.Brush(color), centerX, y, CenterFormat);

    public static void DrawRight(Graphics g, string value, Font font, Color color, float rightX, float y)
        => g.DrawString(value, font, Palette.Brush(color), rightX, y, RightFormat);

    public static void DrawCenteredBox(Graphics g, string value, Font font, Color color, float centerX, float y, float boxWidth)
        => g.DrawString(value, font, Palette.Brush(color), new RectangleF(centerX - boxWidth * 0.5f, y, boxWidth, 400f), CenterFormat);

    public static float Width(Graphics g, string value, Font font) => g.MeasureString(value, font, int.MaxValue, LeftFormat).Width;
}
