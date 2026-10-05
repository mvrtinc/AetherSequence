using System.Drawing.Drawing2D;
using System.Numerics;
using AetherSequence.Core;
using AetherSequence.Entities;
using AetherSequence.World;

namespace AetherSequence.Art;

/// <summary>
/// Миникарта уровня: показывает планировку пещеры, портал, бонусы,
/// всех врагов и героя. Статичная часть (стены/пол/портал) кэшируется
/// один раз на уровень, каждый кадр рисуется только один блит и точки.
/// </summary>
internal sealed class Minimap
{
    public const float X = 494f;
    public const float Y = 46f;
    public const float W = 138f;
    public const float H = 78f;

    private Bitmap? _layout;
    private Level? _cachedLevel;
    private Color _wallColor;
    private Color _floorColor;

    private float _scale = 1f;
    private float _offsetX;
    private float _offsetY;

    /// <summary>Размер видимой области мира для рамки поля зрения.</summary>
    private float _viewW;
    private float _viewH;

    private int _exitTileX;
    private int _exitTileY;
    private bool _exitKnown;

public void Draw(Graphics g, Game game)
   {
   Level level = game.Level;

   // Уровень может быть ещё не сгенерирован (W/H = 0) - тогда рисовать нечего.
   // Без этой проверки GDI+ падал на создании битмапа нулевого размера.
   if (level.W <= 0 || level.H <= 0) return;

   BuildLayout(level);

g.CompositingMode = CompositingMode.SourceOver;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        // Полупрозрачная подложка без толстой рамки: миникарта должна
        // читаться как часть интерфейса, а не как отдельное окно поверх игры.
   Palette.Fill(g, Palette.Fade(System.Drawing.Color.Black, 0.42f), X - 1f, Y - 1f, W + 2f, H + 2f);
        if (_layout is not null)
        {
            g.DrawImage(
       _layout,
         new RectangleF(X, Y, level.W * _scale, level.H * _scale),
 new RectangleF(0f, 0f, (float)_layout.Width, (float)_layout.Height),
    GraphicsUnit.Pixel);
        }

        // рамка текущего поля зрения, вписана в границы карты
        float mapW = level.W * _scale;
        float mapH = level.H * _scale;
        float vx = X + _offsetX + (game.Camera.Position.X / level.PixelSize.X) * mapW;
        float vy = Y + _offsetY + (game.Camera.Position.Y / level.PixelSize.Y) * mapH;
        float vw = (_viewW / level.PixelSize.X) * mapW;
        float vh = (_viewH / level.PixelSize.Y) * mapH;

        float clipL = X + _offsetX;
        float clipT = Y + _offsetY;
        float clipR = clipL + mapW;
        float clipB = clipT + mapH;

        float boxL = MathF.Max(vx, clipL);
        float boxT = MathF.Max(vy, clipT);
        float boxR = MathF.Min(vx + vw, clipR);
        float boxB = MathF.Min(vy + vh, clipB);
if (boxR - boxL > 1f && boxB - boxT > 1f)
     {
  // Тонкая рамка поля зрения: одна линия в полпикселя вместо толстой обводки.
     Palette.Stroke(g, Palette.Fade(Palette.Paper, 0.3f), boxL, boxT, boxR - boxL, boxB - boxT);
        }

        // портал
        if (_exitKnown)
        {
            float ex = X + _offsetX + _exitTileX * _scale;
            float ey = Y + _offsetY + _exitTileY * _scale;
            Color ec = level.ExitOpen ? Palette.Fade(Palette.ExitOpen, 0.95f) : Palette.Fade(Palette.Muted, 0.8f);
            Palette.Fill(g, ec, ex - 1f, ey - 1f, Math.Max(2f, _scale), Math.Max(2f, _scale));
        }

        // бонусы
        foreach (Pickup p in game.Pickups)
        {
            Palette.Fill(g, Palette.Fade(Palette.Xp, 0.9f),
                X + _offsetX + (p.Pos.X / level.PixelSize.X) * (level.W * _scale) - 0.5f,
                Y + _offsetY + (p.Pos.Y / level.PixelSize.Y) * (level.H * _scale) - 0.5f, 1.5f, 1.5f);
        }

// Враги. Раньше точки рисовались для всех, поэтому враг за стеной
        // светился на миникарте и выглядел так, будто стоит в стене.
        // Теперь видны только те, до кого есть линия видимости.
        Vector2 eye = game.Player.Pos;
        foreach (Enemy e in game.Enemies)
        {
            if (e.Dead) continue;

            bool boss = e.IsBoss;
            // Босса видно всегда: потерять его из виду в толпе - плохо.
            if (!boss && !level.LineClear(eye, e.Pos)) continue;

      float r = boss ? 2.5f : 1.5f;
  Color c = boss ? Palette.Fade(Palette.Gold, 0.95f) : Palette.Fade(Palette.Danger, 0.9f);
      float ex = X + _offsetX + (e.Pos.X / level.PixelSize.X) * (level.W * _scale);
float ey = Y + _offsetY + (e.Pos.Y / level.PixelSize.Y) * (level.H * _scale);
     Palette.Fill(g, c, ex - r, ey - r, r * 2f, r * 2f);
        }

        // В дуэли соперник должен читаться так же, как свои: точка ярче и
        // с нимпульсирующим ореолом, иначе его легко спутать с врагом.
        if (game.DuelMode && game.Foe is { Alive: true } foe)
        {
            float fx = X + _offsetX + (foe.Pos.X / level.PixelSize.X) * (level.W * _scale);
    float fy = Y + _offsetY + (foe.Pos.Y / level.PixelSize.Y) * (level.H * _scale);
      float pulse = 0.5f + 0.5f * MathF.Sin((float)game.Time * 6f);
   Palette.Fill(g, Palette.Fade(Palette.ExitOpen, 0.35f + pulse * 0.45f), fx - 2.5f, fy - 2.5f, 5f, 5f);
      Palette.Fill(g, Palette.Fade(Color.White, 0.95f), fx - 1.5f, fy - 1.5f, 3f, 3f);
        }

// Герой: конус прицела + белая точка
        if (game.Player.Alive)
        {
 float px = X + _offsetX + (game.Player.Pos.X / level.PixelSize.X) * (level.W * _scale);
   float py = Y + _offsetY + (game.Player.Pos.Y / level.PixelSize.Y) * (level.H * _scale);

    float len = 7f;
     Vector2 dir = GameMath.FromAngle(game.Player.AimAngle);
      for (int i = 3; i <= (int)len; i++)
      {
            float t = i / len;
      Palette.Fill(g, Palette.Fade(Palette.Paper, 0.28f * (1f - t)),
   px + dir.X * i * 0.6f - 0.5f, py + dir.Y * i * 0.6f - 0.5f, 1.2f, 1.2f);
  }

 // Свой герой виден всегда, даже прижатый к стене, поэтому рисуем его
  // в последнюю очередь и с тёмной обводкой - точка не потеряется
   // на плитах и линиях комнат.
Palette.Fill(g, Palette.Fade(System.Drawing.Color.Black, 0.75f), px - 2.5f, py - 2.5f, 5f, 5f);
 Palette.Fill(g, Palette.Fade(System.Drawing.Color.White, 0.95f), px - 1.5f, py - 1.5f, 3f, 3f);
    }

        // Тонкая рамка в цвет неона темы - миникарта вписывается в общий стиль.
        Color edge = level.Colors.Neon;
        Palette.Stroke(g, Palette.Fade(edge, 0.35f), X - 1f, Y - 1f, W + 2f, H + 2f);
        Palette.Fill(g, Palette.Fade(edge, 0.5f), X - 1f, Y - 1f, W + 2f, 1f);
    }

    private void BuildLayout(Level level)
    {
        _viewW = GameRenderer.Width;
        _viewH = GameRenderer.Height;

        if (ReferenceEquals(_cachedLevel, level) && _layout is not null) return;
        _cachedLevel = level;

        _scale = MathF.Min(W / level.W, H / level.H);
        if (_scale < 1f) _scale = 1f;
        float w = level.W * _scale;
        float h = level.H * _scale;
        _offsetX = (W - w) * 0.5f;
        _offsetY = (H - h) * 0.5f;

        _wallColor = level.MinimapWallColor;
        _floorColor = level.MinimapFloorColor;

        _layout?.Dispose();
        _layout = new Bitmap(level.W, level.H, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics lg = Graphics.FromImage(_layout);
        using System.Drawing.SolidBrush wall = new(_wallColor);
        using System.Drawing.SolidBrush floor = new(_floorColor);
        using System.Drawing.SolidBrush pillar = new(level.MinimapPillarColor);

        for (int y = 0; y < level.H; y++)
        {
            int row = y * level.W;
            for (int x = 0; x < level.W; x++)
            {
                Tile tile = level.Tiles[row + x];
                lg.FillRectangle(tile switch
                {
                    Tile.Wall => wall,
                    Tile.Pillar => pillar,
                    _ => floor,
                }, x, y, 1, 1);
            }
        }

        _exitTileX = GameMath.ClampI((int)(level.ExitPos.X / Level.TileSize), 0, level.W - 1);
        _exitTileY = GameMath.ClampI((int)(level.ExitPos.Y / Level.TileSize), 0, level.H - 1);
        _exitKnown = true;
    }
}
