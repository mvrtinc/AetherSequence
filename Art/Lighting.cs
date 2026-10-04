using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using AetherSequence.Combat;
using AetherSequence.Core;
using AetherSequence.Entities;

namespace AetherSequence.Art;

/// <summary>
/// Свет на уровне. Тёмный фон + пятна света от источников дают тот самый
/// эффект "свет окрашивает ближайшие тайлы": пол под игроком и врагами
/// светлеет и подкрашивается их цветом, а дальние углы остаются тёмными.
///
/// Пятна рисуются аддитивно и только в пределах камеры, поэтому стоят
/// дёшево: пятно - это несколько эллипсов, а не пиксельный проход.
/// </summary>
internal static class Lighting
{
    /// <summary>Сколько источников берём за кадр. Больше - уже не видно.</summary>
    private const int MaxLights = 20;

    /// <summary>Кольц в одном пятне: больше - мягче, но дороже.</summary>
    private const int Rings = 4;

    /// <summary>
    /// Пятна света на полу. Вызывается после статичной карты и до сущностей,
    /// чтобы свет лежал под игроком, а не поверх него.
    /// </summary>
    public static void DrawPools(Graphics g, Game game, Vector2 cam)
    {
        float w = GameRenderer.Width;
        float h = GameRenderer.Height;

        if (game.Level.ExitOpen)
        {
            Vector2 exit = game.Level.ExitPos - cam;
            float pulse = 0.85f + 0.15f * MathF.Sin(game.Time * 2.6f);
            Pool(g, exit.X, exit.Y, 44f * pulse, game.Level.Colors.ExitOpen, 0.42f, 1f);
        }

        // Враги - главные постоянные источники: зелёный обычный,
        // красный агрессивный, золотой босс.
        for (int i = 0; i < game.Enemies.Count && i < MaxLights; i++)
        {
            Enemy e = game.Enemies[i];
            if (e.Dead) continue;
            Vector2 p = e.Pos - cam;
            if (p.X < -60f || p.Y < -60f || p.X > w + 60f || p.Y > h + 60f) continue;

Color c = e.IsBoss ? Palette.Gold : e.Tint.A > 0 ? e.Tint : Palette.Xp;
 float k = e.IsBoss ? 0.42f : 0.20f;
float r = e.IsBoss ? 52f : e.Radius * 2.5f;
    float flick = 0.9f + 0.1f * MathF.Sin(game.Time * 3.1f + e.Wave);
    Pool(g, p.X, p.Y, r * flick, c, k, 0.82f);
        }

        // Портал закрыт - тусклая подсветка, чтобы его всё равно было видно.
        if (!game.Level.ExitOpen)
        {
            Vector2 exit = game.Level.ExitPos - cam;
            Pool(g, exit.X, exit.Y, 22f, game.Level.Colors.ExitClosed, 0.22f, 1f);
        }
    }

    /// <summary>
    /// Свет вокруг игрока: подсвечивает пол вокруг, чтобы персонаж
    /// читался как визуальный центр сцены.
    /// </summary>
    public static void DrawPlayerPool(Graphics g, Game game, Vector2 cam)
    {
        if (!game.Player.Alive) return;
        Vector2 p = game.Player.Pos - cam;
        Color tint = Elements.Color(game.Player.LastElement);
        // Тёплый красноватый ореол + лёгкий подмес стихии игрока.
        Color c = GameMath.Mix(Palette.Health, tint, 0.4f);
        Pool(g, p.X, p.Y, 34f + game.Player.Flow * 8f, c, 0.3f + game.Player.Flow * 0.14f, 0.88f);
    }

/// <summary>Мягкое пятно света. Несколько эллипсов с падающей к центру альфой.</summary>
    private static void Pool(Graphics g, float cx, float cy, float radius, Color color, float intensity, float squash)
    {
        if (radius <= 1f || intensity <= 0.02f) return;

        // Пятна не должны поднимать общую яркость сцены: их задача - подсветить
        // плитки под источником, а не превратить пол в светящийся прямоугольник.
        for (int ring = Rings; ring >= 1; ring--)
        {
float s = ring / (float)Rings;
   // Альфа растёт к центру пятна.
            float a = intensity * (1f - s) * 0.34f;
     if (a <= 0.015f) continue;
float rx = radius * s;
      float ry = radius * squash * s;
   // Кисть из кэша: покадровое создание SolidBrush дороже самой заливки.
g.FillEllipse(Palette.SoftBrush(color, a), cx - rx, cy - ry, rx * 2f, ry * 2f);
        }
    }

    /// <summary>
    /// Пыль в воздухе. Медленно дрейфующие точки, которые ловят свет -
    /// они делают статичную картинку живой и почти ничего не стоят.
    /// </summary>
    public static void DrawDust(Graphics g, Game game, Vector2 cam, float density)
    {
        if (density <= 0.01f) return;
        ThemeColors t = game.Level.Colors;
        int count = GameMath.ClampI((int)(34f * density), 0, 40);

        for (int i = 0; i < count; i++)
        {
            // Позиция пыли выводится из хеша индекса и медленного времени,
            // поэтому она не прыгает между кадрами и не требует хранения.
            uint h = (uint)(i * 2654435761u) ^ 0x9E3779B9u;
            h ^= h >> 15;
            float px = (h & 0x3FF) + MathF.Sin(game.Time * 0.21f + i * 1.7f) * 9f;
            float py = ((h >> 10) & 0x3FF) + game.Time * (2.2f + (i & 3) * 0.5f);
            px = (px - cam.X * 0.35f) % GameRenderer.Width;
            py %= GameRenderer.Height;
            if (px < 0f) px += GameRenderer.Width;
            if (py < 0f) py += GameRenderer.Height;

float twinkle = 0.35f + 0.65f * MathF.Abs(MathF.Sin(game.Time * 1.6f + i));
     float a = 0.10f * twinkle * density;
  if (a < 0.01f) continue;
            g.FillRectangle(Palette.SoftBrush(t.PanelLit, a), px, py, 1f, 1f);
        }
    }
}
