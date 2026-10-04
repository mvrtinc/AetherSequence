using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using AetherSequence.Art;
using AetherSequence.Core;

namespace AetherSequence.World;

internal enum Tile : byte
{
    Floor,
    Wall,
    Pillar,
    Rubble,
}

internal sealed class Level
{
    public const int TileSize = 16;

    private static readonly Color[] WallTones =
    {
        Color.FromArgb(40, 35, 58),
        Color.FromArgb(45, 39, 65),
        Color.FromArgb(36, 32, 53),
        Color.FromArgb(49, 42, 71),
    };

    public int W { get; private set; }

    public int H { get; private set; }

    public Tile[] Tiles { get; private set; } = Array.Empty<Tile>();

    public byte[] Decor { get; private set; } = Array.Empty<byte>();

    public Vector2 Spawn { get; set; }

    /// <summary>Вторая точка спавна - для дуэли.</summary>
    public Vector2 Spawn2 { get; set; }

    public Vector2 ExitPos { get; set; }

    public bool ExitOpen { get; set; }

    public bool IsBoss { get; set; }

    /// <summary>Тема пещеры - задаётся один раз при генерации, дальше только читается.</summary>
    public LevelTheme Theme { get; set; } = LevelTheme.Crystal;

    /// <summary>Палитра темы. Всегда соответствует Theme.</summary>
    public ThemeColors Colors => LevelThemes.Of(Theme);

    public int Depth { get; set; }

    public Vector2 PixelSize => new(W * TileSize, H * TileSize);

    private Bitmap? _staticMap;

    /// <summary>
    /// Пол и стены не меняются в течение забега, поэтому весь уровень
    /// рисуется один раз в bitmap, а в кадре берётся только окно камеры.
    /// </summary>
    public void DrawStaticMap(Graphics g, Vector2 cam, Vector2 view)
    {
        Bitmap bmp = EnsureStaticMap();
        int sx = GameMath.ClampI((int)MathF.Round(cam.X), 0, Math.Max(0, bmp.Width - 1));
        int sy = GameMath.ClampI((int)MathF.Round(cam.Y), 0, Math.Max(0, bmp.Height - 1));
        int sw = Math.Min((int)MathF.Round(view.X), bmp.Width - sx);
        int sh = Math.Min((int)MathF.Round(view.Y), bmp.Height - sy);
        if (sw <= 0 || sh <= 0) return;
        g.DrawImage(bmp, new RectangleF(sx, sy, sw, sh), new Rectangle(sx, sy, sw, sh), GraphicsUnit.Pixel);
    }

    private Bitmap EnsureStaticMap()
    {
        if (_staticMap is not null) return _staticMap;
        Vector2 size = PixelSize;
        int w = Math.Max(1, (int)size.X);
        int h = Math.Max(1, (int)size.Y);
        Bitmap bmp = new(w, h, PixelFormat.Format32bppPArgb);
        using Graphics mg = Graphics.FromImage(bmp);
        mg.CompositingMode = CompositingMode.SourceCopy;
        mg.Clear(Color.Transparent);
        mg.CompositingMode = CompositingMode.SourceOver;
        mg.PixelOffsetMode = PixelOffsetMode.Half;
        DrawFloor(mg, Vector2.Zero, new Vector2(w, h));
        DrawWalls(mg, Vector2.Zero, new Vector2(w, h));
        _staticMap = bmp;
        return bmp;
    }

    /// <summary>Цвет стены на миникарте.</summary>
    public Color MinimapWallColor => Colors.MinimapWall;

    /// <summary>Цвет пола на миникарте - заметно светлее стен, иначе планировка не читается.</summary>
    public Color MinimapFloorColor => Colors.MinimapFloor;

    /// <summary>Цвет колонны на миникарте.</summary>
    public Color MinimapPillarColor => Colors.MinimapPillar;

    private static Level Create(int w, int h)
    {
        Level level = new()
        {
            W = w,
            H = h,
            Tiles = new Tile[w * h],
            Decor = new byte[w * h],
        };
        for (int i = 0; i < level.Tiles.Length; i++) level.Tiles[i] = Tile.Wall;
        return level;
    }

    public Tile TileAt(int tx, int ty)
        => tx < 0 || ty < 0 || tx >= W || ty >= H ? Tile.Wall : Tiles[ty * W + tx];

    public bool Solid(int tx, int ty)
    {
        Tile t = TileAt(tx, ty);
        return t == Tile.Wall || t == Tile.Pillar;
    }

    public bool SolidAt(Vector2 p) => Solid((int)MathF.Floor(p.X / TileSize), (int)MathF.Floor(p.Y / TileSize));

    public bool OverlapsSolid(float x, float y, float r)
    {
        int minTx = (int)MathF.Floor((x - r) / TileSize);
        int maxTx = (int)MathF.Floor((x + r) / TileSize);
        int minTy = (int)MathF.Floor((y - r) / TileSize);
        int maxTy = (int)MathF.Floor((y + r) / TileSize);
        for (int ty = minTy; ty <= maxTy; ty++)
        {
            for (int tx = minTx; tx <= maxTx; tx++)
            {
                if (Solid(tx, ty)) return true;
            }
        }
        return false;
    }

    public Vector2 Move(Vector2 pos, Vector2 delta, float radius)
    {
        float nx = pos.X + delta.X;
        if (!OverlapsSolid(nx, pos.Y, radius)) pos.X = nx;
        float ny = pos.Y + delta.Y;
        if (!OverlapsSolid(pos.X, ny, radius)) pos.Y = ny;
        return pos;
    }

    public bool LineClear(Vector2 from, Vector2 to)
    {
        float dist = Vector2.Distance(from, to);
        int steps = Math.Max(1, (int)(dist / 6f));
        for (int i = 1; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
            if (SolidAt(p)) return false;
        }
        return true;
    }

    public float RayLength(Vector2 from, Vector2 dir, float max)
    {
        for (float d = 2f; d <= max; d += 2f)
        {
            if (SolidAt(from + dir * d)) return d;
        }
        return max;
    }

    public List<Vector2> RandomFloorPoints(Rng rng, int count, Vector2 avoid, float minDist, float maxDist = float.MaxValue)
    {
        List<Vector2> result = new();
        int attempts = count * 60;
        for (int i = 0; i < attempts && result.Count < count; i++)
        {
            float tx = rng.Range(1.5f, W - 1.5f);
            float ty = rng.Range(1.5f, H - 1.5f);
            if (Solid((int)tx, (int)ty)) continue;
            Vector2 p = new(tx * TileSize, ty * TileSize);
            if (Vector2.Distance(p, avoid) < minDist) continue;
            if (maxDist != float.MaxValue && Vector2.Distance(p, avoid) > maxDist) continue;
            bool tooClose = false;
            foreach (Vector2 other in result)
            {
                if (Vector2.Distance(p, other) < 22f)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;
            result.Add(p);
        }
        return result;
    }

    public static Level Generate(Rng rng, int depth)
    {
        LevelTheme theme = LevelThemes.ForDepth(depth);
        bool boss = depth % 5 == 0;
        Level level = boss ? GenerateArena(rng, depth) : GenerateRooms(rng, depth);
        level.IsBoss = boss;
        level.Depth = depth;
        level.Theme = theme;
        return level;
    }

    private static void Carve(Level level, int x0, int y0, int x1, int y1, Tile tile)
    {
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (x < 1 || y < 1 || x >= level.W - 1 || y >= level.H - 1) continue;
                level.Tiles[y * level.W + x] = tile;
            }
        }
    }

    /// <summary>
    /// Пещера для дуэли: один большой зал 80x46 тайлов (1280x736 пикселей).
    /// Пол сплошной, укрытий минимум - только редкие одиночные колонны
    /// у дальних стен, чтобы не ломать линию огня. Две точки спавна по краям.
    /// </summary>
    public static Level GenerateDuelCave(Rng rng, int depth)
    {
        const int w = 80;
        const int h = 46;
        Level level = Create(w, h);
        Carve(level, 1, 1, w - 2, h - 2, Tile.Floor);

        int cx = w / 2;
        int cy = h / 2;

        // укрытия: 4 одиночные колонны, зеркально относительно центра,
        // только в боковых зонах - центральная зона остаётся открытой
        int[] offsetsX = { -22, 22, -14, 14 };
        int[] offsetsY = { -9, -9, 8, 8 };
        for (int i = 0; i < offsetsX.Length; i++)
        {
            int px = cx + offsetsX[i];
            int py = cy + offsetsY[i];
            if (px < 2 || py < 2 || px >= w - 2 || py >= h - 2) continue;
            if (level.TileAt(px, py) != Tile.Floor) continue;
            level.Tiles[py * w + px] = Tile.Pillar;
        }

        level.Spawn = new(6f * TileSize, cy * TileSize);
        level.Spawn2 = new((w - 7) * TileSize, cy * TileSize);
        level.ExitPos = level.Spawn;
        level.ExitOpen = false;
        level.IsBoss = false;
        level.Depth = depth;

        ClearArea(level, level.Spawn);
        ClearArea(level, level.Spawn2);
        ScatterDecor(level, rng);
        return level;
    }

    private static void CarveCorridor(Level level, Vector2 a, Vector2 b, Rng rng)
    {
        int x0 = (int)(a.X / TileSize);
        int y0 = (int)(a.Y / TileSize);
        int x1 = (int)(b.X / TileSize);
        int y1 = (int)(b.Y / TileSize);
        bool horizontalFirst = rng.Chance(0.5f);
        if (horizontalFirst)
        {
            for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++) Carve(level, x, y0, x, y0, Tile.Floor);
            for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++) Carve(level, x1, y, x1, y, Tile.Floor);
        }
        else
        {
            for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++) Carve(level, x0, y, x0, y, Tile.Floor);
            for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++) Carve(level, x, y1, x, y1, Tile.Floor);
        }
    }

    private static Level GenerateRooms(Rng rng, int depth)
    {
        int w = 46 + rng.Next(0, 11);
        int h = 26 + rng.Next(0, 9);
        Level level = Create(w, h);

        const int cols = 4;
        const int rows = 2;
        List<(int X, int Y, int W, int H)> rooms = new();

        for (int cy = 0; cy < rows; cy++)
        {
            for (int cx = 0; cx < cols; cx++)
            {
                int cellX0 = 1 + cx * (w - 2) / cols;
                int cellX1 = 1 + (cx + 1) * (w - 2) / cols - 1;
                int cellY0 = 1 + cy * (h - 2) / rows;
                int cellY1 = 1 + (cy + 1) * (h - 2) / rows - 1;
                int rw = Math.Max(3, cellX1 - cellX0 - rng.Next(0, 3));
                int rh = Math.Max(3, cellY1 - cellY0 - rng.Next(0, 3));
                int rx = GameMath.ClampI(cellX0 + rng.Next(0, Math.Max(1, cellX1 - cellX0 - rw + 1)), cellX0, Math.Max(cellX0, cellX1 - rw + 1));
                int ry = GameMath.ClampI(cellY0 + rng.Next(0, Math.Max(1, cellY1 - cellY0 - rh + 1)), cellY0, Math.Max(cellY0, cellY1 - rh + 1));
                rooms.Add((rx, ry, rw, rh));
                Carve(level, rx, ry, rx + rw - 1, ry + rh - 1, Tile.Floor);
            }
        }

        for (int i = 1; i < rooms.Count; i++)
        {
            CarveCorridor(level, Center(rooms[i - 1]), Center(rooms[i]), rng);
        }
        for (int i = 0; i < 2; i++)
        {
            CarveCorridor(level, Center(rooms[rng.Next(0, rooms.Count)]), Center(rooms[rng.Next(0, rooms.Count)]), rng);
        }

        (int X, int Y, int W, int H) first = rooms[0];
        level.Spawn = Center(first);
        level.ExitPos = level.Spawn;
        float best = -1f;
        foreach ((int X, int Y, int W, int H) r in rooms)
        {
            Vector2 c = Center(r);
            float d = Vector2.Distance(c, level.Spawn);
            if (d > best)
            {
                best = d;
                level.ExitPos = c;
            }
        }

        foreach ((int X, int Y, int W, int H) r in rooms)
        {
            int pillars = rng.Next(0, 4);
            for (int i = 0; i < pillars; i++)
            {
                int px = r.X + 1 + rng.Next(0, Math.Max(1, r.W - 2));
                int py = r.Y + 1 + rng.Next(0, Math.Max(1, r.H - 2));
                if (level.TileAt(px, py) == Tile.Floor) level.Tiles[py * level.W + px] = Tile.Pillar;
            }
        }

        ClearArea(level, level.Spawn);
        ClearArea(level, level.ExitPos);
        ScatterDecor(level, rng);
        return level;
    }

    private static Vector2 Center((int X, int Y, int W, int H) room)
        => new((room.X + room.W * 0.5f) * TileSize, (room.Y + room.H * 0.5f) * TileSize);

    private static void ClearArea(Level level, Vector2 point)
    {
        int tx = (int)MathF.Floor(point.X / TileSize);
        int ty = (int)MathF.Floor(point.Y / TileSize);
        for (int y = ty - 1; y <= ty + 1; y++)
        {
            for (int x = tx - 1; x <= tx + 1; x++)
            {
                if (x < 1 || y < 1 || x >= level.W - 1 || y >= level.H - 1) continue;
                if (level.TileAt(x, y) == Tile.Pillar) level.Tiles[y * level.W + x] = Tile.Floor;
            }
        }
    }

    private static Level GenerateArena(Rng rng, int depth)
    {
        // Арена растёт с глубиной и одевается в тему: на 15 глубине зал уже
        // заметно больше, чем на 5-й, и колонны ставятся по-разному.
        LevelTheme theme = LevelThemes.ForDepth(depth);
        int grow = (depth - 5) / 5;
        int w = 58 + grow * 6;
        int h = 32 + grow * 3;
        Level level = Create(w, h);
        Carve(level, 1, 1, w - 2, h - 2, Tile.Floor);

        int cx = w / 2;
        int spireCount = theme switch
        {
            LevelTheme.Toxic => 4,
            LevelTheme.Bone => 8,
            LevelTheme.Ash => 5,
            _ => 6,
        };
        int spireStep = Math.Max(4, (w - 16) / (spireCount + 1));

        for (int i = 0; i < spireCount; i++)
        {
            int ox = 4 + i * spireStep;
            int oy = 2 + (i % 2) * (h - 8);
            for (int y = oy; y < oy + 2 && y < h - 1; y++)
            {
                for (int x = 2; x < 2 + ox && x < w - 2 - ox; x++)
                {
                    if (level.TileAt(x, y) == Tile.Floor) level.Tiles[y * level.W + x] = Tile.Pillar;
                    int mx = w - 1 - x;
                    if (level.TileAt(mx, y) == Tile.Floor) level.Tiles[y * level.W + mx] = Tile.Pillar;
                }
            }
        }

        // в костяной теме по центру - частокол, в пепльной - редкие обломки
        int loose = theme switch
        {
            LevelTheme.Bone => 14,
            LevelTheme.Ash => 8,
            _ => 10,
        };
        for (int i = 0; i < loose; i++)
        {
            int px = 3 + rng.Next(0, w - 6);
            int py = 2 + rng.Next(0, h - 4);
            if (Math.Abs(px - cx) < 2) continue;
            if (level.TileAt(px, py) == Tile.Floor) level.Tiles[py * level.W + px] = Tile.Pillar;
        }

        level.Spawn = new(cx * TileSize, (h - 4) * TileSize);
        level.ExitPos = new(cx * TileSize, 3.5f * TileSize);
        ClearArea(level, level.Spawn);
        ClearArea(level, level.ExitPos);
        ScatterDecor(level, rng);
        return level;
    }

    private static void ScatterDecor(Level level, Rng rng)
    {
        for (int y = 1; y < level.H - 1; y++)
        {
            for (int x = 1; x < level.W - 1; x++)
            {
                int idx = y * level.W + x;
                if (level.Tiles[idx] != Tile.Floor) continue;
                float roll = rng.NextFloat();
                level.Decor[idx] = roll < 0.05f ? (byte)1 : roll < 0.08f ? (byte)2 : roll < 0.1f ? (byte)3 : roll < 0.115f ? (byte)4 : (byte)0;
            }
        }
    }

    public void DrawFloor(Graphics g, Vector2 cam, Vector2 view)
    {
        ThemeColors t = Colors;
        int tx0 = GameMath.ClampI((int)MathF.Floor(cam.X / TileSize) - 1, 0, W - 1);
        int ty0 = GameMath.ClampI((int)MathF.Floor(cam.Y / TileSize) - 1, 0, H - 1);
        int tx1 = GameMath.ClampI((int)MathF.Ceiling((cam.X + view.X) / TileSize) + 1, 0, W - 1);
        int ty1 = GameMath.ClampI((int)MathF.Ceiling((cam.Y + view.Y) / TileSize) + 1, 0, H - 1);

        for (int ty = ty0; ty <= ty1; ty++)
        {
            float y = ty * TileSize;
            for (int tx = tx0; tx <= tx1; tx++)
            {
                float x = tx * TileSize;
                int idx = ty * W + tx;
                Tile tile = Tiles[idx];
                if (tile == Tile.Wall || tile == Tile.Pillar)
                {
                    // Под стеной всё равно рисуем тёмный слой - иначе сквозь
                    // щели между блоками просвечивал бы чёрный фон карты.
                    Palette.Fill(g, t.Deep, x, y, TileSize, TileSize);
                    continue;
                }

                uint hash = TileHash(tx, ty);
                DrawFloorPanel(g, t, x, y, tx, ty, tile, hash);
                DrawFloorDetail(g, t, x, y, tile, Decor[idx], hash);
            }
        }
    }

    private static uint TileHash(int tx, int ty)
    {
        uint hash = (uint)(tx * 73856093) ^ (uint)(ty * 19349663);
        hash = (hash ^ (hash >> 13)) * 2654435761u;
        return hash ^ (hash >> 16);
    }

    /// <summary>
    /// Панель пола: тело, светлая верхняя кромка и тёмный шов по периметру.
    /// Именно эти три слоя дают ощущение объёма, которого не было в плоской заливке.
    /// </summary>
    private void DrawFloorPanel(Graphics g, ThemeColors t, float x, float y, int tx, int ty, Tile tile, uint hash)
    {
        Color body = (hash & 1) == 0 ? t.Floor : t.FloorAlt;
        if (tile == Tile.Rubble) body = GameMath.Mix(body, t.Deep, 0.35f);

        Palette.Fill(g, body, x, y, TileSize, TileSize);

        // Светлая кромка сверху: панель ловит свет сверху - так она читается объёмной.
        Palette.Fill(g, Palette.Fade(t.PanelLit, 0.85f), x, y, TileSize, 1f);

        // Шов по левой и верхней границам - панели перестают сливаться в заливку.
        Palette.Fill(g, t.Seam, x, y, 1f, TileSize);
        Palette.Fill(g, Palette.Fade(t.Seam, 0.6f), x, y, TileSize, 1f);

        // Тень от блока над тайлом.
        if (!Solid(tx, ty - 1))
        {
            Palette.Fill(g, Palette.Fade(t.WallShadow, 0.5f), x, y, TileSize, 3f);
        }
        else if (!Solid(tx, ty + 1))
        {
            Palette.Fill(g, Palette.Fade(t.WallShadow, 0.45f), x, y + TileSize - 3f, TileSize, 3f);
        }
    }

    /// <summary>
    /// Варианты панели. Не каждый тайл уникален: чистых панелей большинство,
    /// остальные - царапины, трещина, вентиляция, кабель, редкая деталь.
    /// Благодаря этому сетка перестаёт читаться как процедурная.
    /// </summary>
    private static void DrawFloorDetail(Graphics g, ThemeColors t, float x, float y, Tile tile, byte decor, uint hash)
    {
        if (tile == Tile.Rubble)
        {
            DrawRubble(g, t, x, y, hash);
            DrawDecor(g, t, decor, x, y, hash);
            return;
        }

        uint roll = hash % 100u;
        if (roll < 70u)
        {
            // Обычная панель. Лёгкий намёт по краю - почти незаметный, но он
            // убирает ощущение идеально ровной заливки.
            if ((hash & 0x20u) != 0)
            {
                Palette.Fill(g, Palette.Fade(t.Deep, 0.25f), x + 3f, y + 4f, 6f, 1f);
                Palette.Fill(g, Palette.Fade(t.PanelLit, 0.3f), x + 4f, y + 11f, 5f, 1f);
            }
        }
        else if (roll < 85u)
        {
            // Царапины.
            for (int i = 0; i < 3; i++)
            {
                uint h = hash >> (i * 5);
                float sx = x + 2f + (h & 7u);
                float sy = y + 3f + ((h >> 3) & 9u);
                float len = 3f + ((h >> 6) & 4u);
                Palette.Fill(g, Palette.Fade(t.PanelLit, 0.28f), sx, sy, len, 1f);
            }
        }
        else if (roll < 92u)
        {
            // Трещина через всю панель.
            DrawCrack(g, t, x, y, hash);
        }
        else if (roll < 97u)
        {
            // Вентиляционная решётка.
            float vx = x + 4f;
            float vy = y + 4f;
            Palette.Fill(g, Palette.Fade(t.Deep, 0.6f), vx, vy, 8f, 7f);
            for (int i = 0; i < 4; i++)
            {
                Palette.Fill(g, Palette.Fade(t.Cable, 0.55f), vx + 1f, vy + 1f + i * 2f, 6f, 1f);
            }
            Palette.Fill(g, Palette.Fade(t.PanelLit, 0.5f), vx, vy - 1f, 8f, 1f);
        }
        else
        {
            // Кабель с небольшим индикатором - редкая деталь.
            float cx = x + 2f;
            Palette.Fill(g, Palette.Fade(t.Cable, 0.6f), cx, y + 7f, TileSize - 4f, 1f);
            Palette.Fill(g, Palette.Fade(t.Deep, 0.5f), cx + 1f, y + 6f, 2f, 3f);
            if ((hash & 0x8u) != 0)
            {
                Palette.Fill(g, Palette.Fade(t.Indicator, 0.75f), x + 11f, y + 5f, 2f, 2f);
            }
        }

        DrawDecor(g, t, decor, x, y, hash);
    }

    private static void DrawCrack(Graphics g, ThemeColors t, float x, float y, uint hash)
    {
        float px = x + 2f + (hash & 5u);
        Palette.Fill(g, Palette.Fade(t.Deep, 0.85f), px, y + 1f, 1f, TileSize - 2f);
        Palette.Fill(g, Palette.Fade(t.Deep, 0.7f), px + 1f, y + 4f + (hash & 3u), 1f, 5f);
        Palette.Fill(g, Palette.Fade(t.Deep, 0.7f), px - 1f, y + 9f - (hash & 1u), 1f, 4f);
    }

    private static void DrawRubble(Graphics g, ThemeColors t, float x, float y, uint hash)
    {
        Palette.Fill(g, Palette.Fade(t.Deep, 0.4f), x + 2f, y + TileSize - 4f, 12f, 3f);
        for (int i = 0; i < 4; i++)
        {
            uint h = hash >> (i * 3);
            float bx = x + 2f + (h & 9u);
            float by = y + 6f + ((h >> 4) & 5u);
            float s = 2f + ((h >> 6) & 1u);
            Palette.Fill(g, Palette.Fade(t.Pillar, 1.1f), bx, by, s, s);
            Palette.Fill(g, Palette.Fade(t.PanelLit, 0.4f), bx, by, s, 1f);
        }
    }

    private static void DrawDecor(Graphics g, ThemeColors t, byte decor, float x, float y, uint hash)
    {
        switch (decor)
        {
            case 1:
                Palette.Fill(g, t.Decor[0], x + 3, y + 9, 1f, 4f);
                Palette.Fill(g, t.Decor[1], x + 5, y + 7, 1f, 6f);
                Palette.Fill(g, t.Decor[2], x + 9, y + 8, 1f, 5f);
                break;
            case 2:
                Palette.Fill(g, t.Decor[3], x + 2, y + 6 + (hash & 3), 6f, 1f);
                Palette.Fill(g, t.Decor[4], x + 5, y + 9 + (hash & 1), 7f, 1f);
                Palette.Fill(g, t.Decor[5], x + 9, y + 4 + (hash & 3), 4f, 1f);
                break;
            case 3:
                Palette.Fill(g, t.Decor[6], x + 4, y + 10, 3f, 3f);
                Palette.Fill(g, t.Decor[7], x + 5, y + 10, 1f, 1f);
                Palette.Fill(g, t.Decor[6], x + 9, y + 11, 2f, 2f);
                break;
            case 4:
                Palette.Fill(g, t.Decor[9], x + 6, y + 5, 4f, 4f);
                Palette.Fill(g, t.Decor[10], x + 7, y + 6, 2f, 2f);
                break;
        }
    }

    public void DrawWalls(Graphics g, Vector2 cam, Vector2 view)
    {
        ThemeColors t = Colors;
        int tx0 = GameMath.ClampI((int)MathF.Floor(cam.X / TileSize) - 1, 0, W - 1);
        int ty0 = GameMath.ClampI((int)MathF.Floor(cam.Y / TileSize) - 1, 0, H - 1);
        int tx1 = GameMath.ClampI((int)MathF.Ceiling((cam.X + view.X) / TileSize) + 1, 0, W - 1);
        int ty1 = GameMath.ClampI((int)MathF.Ceiling((cam.Y + view.Y) / TileSize) + 1, 0, H - 1);

        for (int ty = ty0; ty <= ty1; ty++)
        {
            float y = ty * TileSize;
            for (int tx = tx0; tx <= tx1; tx++)
            {
                float x = tx * TileSize;
                Tile tile = Tiles[ty * W + tx];
                if (tile == Tile.Pillar)
                {
                    DrawPillar(g, t, x, y, tx, ty);
                    continue;
                }
                if (tile != Tile.Wall) continue;
                DrawWallBlock(g, t, x, y, tx, ty);
            }
        }
    }

    /// <summary>
    /// Блок стены в три слоя: светлая верхняя грань, тёмная лицевая сторона
    /// и неоновая кромка со светом. Именно кромка делает стену объёмной -
    /// раньше блок был плоской заливкой с одной полоской сверху.
    /// </summary>
    private void DrawWallBlock(Graphics g, ThemeColors t, float x, float y, int tx, int ty)
    {
        uint hash = TileHash(tx, ty);
        bool openAbove = !Solid(tx, ty - 1);

        // Тело блока: четыре слегка разных тона дают кладку без визуального шума.
        Color body = t.WallBody[hash % (uint)t.WallBody.Length];

        // Падающая тень под блоком - отделяет стену от пола.
        Palette.Fill(g, Palette.Fade(t.WallShadow, 0.85f), x, y + TileSize - 2f, TileSize, 4f);

        // Масса стены заметно светлее пола - иначе блок не читается как
        // возвышенность и вся карта сливается в тёмное пятно.
        Palette.Fill(g, body, x, y, TileSize, TileSize);

        // Панельные швы: блок выглядит собранным из секций, а не залитым.
        Palette.Fill(g, Palette.Fade(t.WallShadow, 0.5f), x, y + 8f, TileSize, 1f);
        Palette.Fill(g, Palette.Fade(t.WallShadow, 0.4f), x + 5f + (int)(hash & 3u), y + 1f, 1f, TileSize - 2f);
        Palette.Fill(g, Palette.Fade(t.PanelLit, 0.22f), x, y + 9f, TileSize, 1f);

        // Светлая верхняя грань - только если сверху открыто.
        if (openAbove)
        {
            Palette.Fill(g, t.WallTop, x, y, TileSize, 6f);
            Palette.Fill(g, Palette.Fade(t.PanelLit, 0.55f), x, y, TileSize, 1f);
        }
        else
        {
            // Верх стены, уходящий вглубь - чуть темнее, чтобы блок
            // не выглядел плоским в массе.
            Palette.Fill(g, Palette.Fade(body, 0.86f), x, y, TileSize, 4f);
        }

        // Неоновая кромка по верхнему краю блока. Не слишком яркая:
        // это акцент, а не главный объект кадра - иначе стены превращаются
        // в светящиеся линии и перетягивают внимание с персонажей.
        if (openAbove)
        {
            Palette.Fill(g, Palette.Fade(t.Neon, 0.10f), x - 1f, y - 1f, TileSize + 2f, 5f);
            Palette.Fill(g, Palette.Fade(t.Neon, 0.30f), x, y + 1f, TileSize, 3f);
            Palette.Fill(g, Palette.Fade(t.Neon, 0.75f), x, y + 3f, TileSize, 1f);
        }

        // Вертикальные кромки там, где блок соседствует с проходом.
        if (!Solid(tx - 1, ty))
        {
            Palette.Fill(g, Palette.Fade(t.WallEdge, 0.42f), x, y, 1f, TileSize);
            Palette.Fill(g, Palette.Fade(t.Neon, 0.30f), x, y + 2f, 1f, TileSize - 4f);
        }
        if (!Solid(tx + 1, ty))
        {
            Palette.Fill(g, Palette.Fade(t.WallEdge, 0.34f), x + TileSize - 1f, y, 1f, TileSize);
            Palette.Fill(g, Palette.Fade(t.Neon, 0.24f), x + TileSize - 1f, y + 2f, 1f, TileSize - 4f);
        }

        // Индикатор на блоке - редкая деталь, даёт ощущение техно-сооружения.
        uint detail = hash % 100u;
        if (detail < 6u)
        {
            Palette.Fill(g, Palette.Fade(t.Deep, 0.6f), x + 6f, y + 9f, 4f, 3f);
            Palette.Fill(g, Palette.Fade(t.Indicator, 0.7f), x + 7f, y + 10f, 2f, 1f);
        }
        else if (detail < 12u)
        {
            for (int i = 0; i < 3; i++)
            {
                Palette.Fill(g, Palette.Fade(t.Cable, 0.45f), x + 3f, y + 9f + i * 2f, 10f, 1f);
            }
        }
    }

    private static void DrawPillar(Graphics g, ThemeColors t, float x, float y, int tx, int ty)
    {
        uint hash = TileHash(tx, ty);
        Color body = GameMath.Shade(t.Pillar, 0.85f + (hash & 3u) * 0.06f);
        Palette.Fill(g, Palette.Fade(t.WallShadow, 0.8f), x + 1f, y + 4f, TileSize, TileSize);
        Palette.Fill(g, body, x, y + 1f, TileSize, TileSize);
        Palette.Fill(g, t.PillarTop, x + 2f, y + 3f, TileSize - 4f, TileSize - 5f);
        Palette.Fill(g, Palette.Shade(t.PillarTop, 1.15f), x + 2f, y + 3f, TileSize - 4f, 1f);
        Palette.Fill(g, Palette.Shade(t.PillarTop, 0.75f), x + 2f, y + TileSize - 3f, TileSize - 4f, 1f);

        // Неоновая окантовка и контактная полоса - колонна тоже должна светиться.
        Palette.Fill(g, Palette.Fade(t.Neon, 0.55f), x + 2f, y + 6f, TileSize - 4f, 1f);
        Palette.Fill(g, Palette.Fade(t.Neon, 0.3f), x + 4f, y + 4f, 1f, TileSize - 6f);
        Palette.Fill(g, Palette.Fade(t.Neon, 0.3f), x + TileSize - 5f, y + 4f, 1f, TileSize - 6f);
    }

    public void DrawExit(Graphics g, float time, bool open)
    {
        ThemeColors t = Colors;
        Vector2 p = ExitPos;
        if (!open)
        {
            Palette.Circle(g, Palette.Fade(t.ExitClosed, 0.8f), p.X, p.Y, 11f);
            Palette.Circle(g, Palette.Fade(Palette.Muted, 0.5f), p.X, p.Y, 8f);
            return;
        }
        float pulse = 0.6f + 0.4f * MathF.Sin(time * 3.4f);
        Palette.AddGlow(g, p, 30f, t.ExitOpen, 0.5f * pulse);
        Palette.Circle(g, Palette.Fade(t.ExitOpen, 0.9f), p.X, p.Y, 12f);
        Palette.Circle(g, Palette.Fade(t.ExitOpen, 0.6f), p.X, p.Y, 8f * pulse);
        Palette.Fill(g, Palette.Fade(Color.White, 0.9f), p.X - 1f, p.Y - 1f, 2f, 2f);
    }
}
