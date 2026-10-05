using System.Drawing;
using AetherSequence.Core;
using AetherSequence.Entities;

namespace AetherSequence.Art;

internal sealed class Sprite
{
    private readonly int[] _pixels;
    private readonly SolidBrush[] _brushes;
    private readonly int[] _spans;

    public int Width { get; }

    public int Height { get; }

    private Sprite(int w, int h, int[] pixels, Color[] colors)
    {
        Width = w;
        Height = h;
        _pixels = pixels;
        _brushes = new SolidBrush[colors.Length];
        for (int i = 0; i < colors.Length; i++) _brushes[i] = new SolidBrush(colors[i]);
        _spans = BuildSpans(pixels, w, h);
    }

    public static Sprite Parse(string[] rows, Dictionary<char, Color> palette)
    {
        int h = rows.Length;
        int w = 0;
        foreach (string row in rows) w = Math.Max(w, row.Length);

        Dictionary<int, int> colorIndex = new();
        List<Color> colors = new();
        int[] pixels = new int[w * h];

        for (int y = 0; y < h; y++)
        {
            string row = rows[y];
            for (int x = 0; x < w; x++)
            {
                char c = x < row.Length ? row[x] : '.';
                if (c == '.' || !palette.TryGetValue(c, out Color col))
                {
                    pixels[y * w + x] = -1;
                    continue;
                }
                if (!colorIndex.TryGetValue(col.ToArgb(), out int idx))
                {
                    idx = colors.Count;
                    colors.Add(col);
                    colorIndex[col.ToArgb()] = idx;
                }
                pixels[y * w + x] = idx;
            }
        }

        return new Sprite(w, h, pixels, colors.ToArray());
    }

    private int[] BuildSpans(int[] pixels, int w, int h)
    {
        List<int> spans = new();
        for (int y = 0; y < h; y++)
        {
            int x = 0;
            while (x < w)
            {
                int p = pixels[y * w + x];
                if (p < 0)
                {
                    x++;
                    continue;
                }
                int start = x;
                while (x < w && pixels[y * w + x] == p) x++;
                spans.Add(y * w + start);
                spans.Add(x - start);
                spans.Add(p);
            }
        }
        return spans.ToArray();
    }

    public void Draw(Graphics g, float x, float y, bool flip = false, float scale = 1f, float bob = 0f)
    {
        for (int i = 0; i < _spans.Length; i += 3)
        {
            int offset = _spans[i];
            int len = _spans[i + 1];
            int colorIdx = _spans[i + 2];
            int sx = offset % Width;
            int sy = offset / Width;
            float dx = flip ? (Width - sx - len) * scale : sx * scale;
            g.FillRectangle(_brushes[colorIdx], x + dx, y + (sy * scale) + bob, len * scale, scale);
        }
    }

    public void DrawSilhouette(Graphics g, float x, float y, Color color, float alpha, bool flip = false, float scale = 1f, float bob = 0f)
    {
        SolidBrush brush = Palette.Brush(Palette.Fade(color, alpha));
        for (int y2 = 0; y2 < Height; y2++)
        {
            int x2 = 0;
            while (x2 < Width)
            {
                if (_pixels[y2 * Width + x2] < 0)
                {
                    x2++;
                    continue;
                }
                int start = x2;
                while (x2 < Width && _pixels[y2 * Width + x2] >= 0) x2++;
                int len = x2 - start;
                float dx = flip ? (Width - start - len) * scale : start * scale;
                g.FillRectangle(brush, x + dx, y + y2 * scale + bob, len * scale, scale);
            }
        }
    }
}

internal readonly struct BossPart
{
    public readonly RectangleF Rect;
    public readonly char Key;
    public readonly int Group;

    public BossPart(float x, float y, float w, float h, char key, int group = 0)
    {
        Rect = new RectangleF(x, y, w, h);
        Key = key;
        Group = group;
    }
}

internal static class Sprites
{
    public static readonly Sprite Mage = Sprite.Parse(new[]
    {
        "....KKKK....",
        "...KhhhhK.GG",
        "..Khhhhhk.gg",
        "..KhSSSSKh.T",
        "..KhSEsESKh.T",
        "..KhSSSSKh.T",
        "...KSSSSK..T",
        "..KKrrrrKK.T",
        ".KrrBBrrrK.T",
        ".KrRbBbrrK.T",
        ".KrrrrrrrK.T",
        "..KKrrrKK..T",
        "...KK.KK...T",
        "...KK.KK....",
    }, new Dictionary<char, Color>
    {
        ['K'] = GameMath.Rgb(14, 11, 24),
        ['h'] = GameMath.Rgb(46, 33, 74),
        ['k'] = GameMath.Rgb(32, 23, 54),
        ['H'] = GameMath.Rgb(66, 48, 104),
        ['S'] = GameMath.Rgb(238, 198, 160),
        ['s'] = GameMath.Rgb(214, 170, 134),
        ['E'] = GameMath.Rgb(120, 240, 255),
        ['r'] = GameMath.Rgb(92, 58, 140),
        ['R'] = GameMath.Rgb(126, 84, 186),
        ['B'] = GameMath.Rgb(216, 179, 74),
        ['G'] = GameMath.Rgb(140, 240, 255),
        ['g'] = GameMath.Rgb(70, 150, 220),
        ['T'] = GameMath.Rgb(138, 90, 43),
    });

    public static readonly Sprite MageCape = Sprite.Parse(new[]
    {
        "...KKKKK..",
        "..KrrrrrK.",
        ".KrRrrrrK.",
        ".KrrrrrrK.",
        ".KrrRrrrK.",
        "..KrrrrK..",
        "...KKKK...",
    }, new Dictionary<char, Color>
    {
        ['K'] = GameMath.Rgb(14, 11, 24),
        ['r'] = GameMath.Rgb(70, 40, 108),
        ['R'] = GameMath.Rgb(104, 62, 158),
    });

    public static readonly Sprite Slime = Sprite.Parse(new[]
    {
        "....KKKKKK....",
        "..KKrrrrrrKK..",
        ".KrrRRrrrrrrK.",
        "KrrRRRRRRRRrrK",
        "KrRRssssssRRrK",
        "KrrSEsssSEsrK",
        "KrrssssssssrK",
        ".KrrrrrrrrrrK.",
        "..KKrrrrrrKK..",
        "...KKKKKKKK...",
    }, new Dictionary<char, Color>
    {
        ['K'] = GameMath.Rgb(12, 26, 18),
        ['r'] = GameMath.Rgb(46, 132, 84),
        ['R'] = GameMath.Rgb(78, 184, 118),
        ['s'] = GameMath.Rgb(150, 236, 190),
        ['S'] = GameMath.Rgb(18, 26, 24),
        ['E'] = GameMath.Rgb(255, 255, 255),
    });

    public static readonly Sprite SlimeMini = Sprite.Parse(new[]
    {
        "..KKKK..",
        ".KrrrrK.",
        "KrRRrrRK",
        "KrSssSrK",
        ".KrrrrK.",
        "..KKKK..",
    }, new Dictionary<char, Color>
    {
        ['K'] = GameMath.Rgb(12, 26, 18),
        ['r'] = GameMath.Rgb(46, 132, 84),
        ['R'] = GameMath.Rgb(78, 184, 118),
        ['s'] = GameMath.Rgb(150, 236, 190),
        ['S'] = GameMath.Rgb(18, 26, 24),
    });

    public static readonly Sprite Knight = Sprite.Parse(new[]
    {
        "...KKKKKKKK...",
        "..KaaaaaaaaK..",
        ".KaaaaaaaaaaK.",
        ".KaVVVVVVVVaK.",
        ".KaVaaaaaaVaK.",
        ".KaaaaaaaaaaK.",
        "..KaaaMMaaaK..",
        "..KcAAAAAAcK..",
        ".KCcAAAAAAcCK.",
        ".KCcAAAAAAscK.",
        "KCCCcAAAAcCCCK",
        "K.CCcAAAaCcCCK",
        "K..KCcAAAaCcKK",
        "...KCcAAAAcCCK.",
        "....KCccCcK...",
        "....KK...KK...",
    }, new Dictionary<char, Color>
    {
        ['K'] = GameMath.Rgb(12, 14, 20),
        ['A'] = GameMath.Rgb(56, 64, 84),
        ['a'] = GameMath.Rgb(104, 118, 146),
        ['V'] = GameMath.Rgb(88, 255, 216),
        ['M'] = GameMath.Rgb(206, 232, 255),
        ['C'] = GameMath.Rgb(168, 50, 74),
        ['c'] = GameMath.Rgb(104, 28, 46),
    });

    public static readonly Sprite Shade = Sprite.Parse(new[]
    {
        "...KKKKKK...",
        "..KhhhhhhK..",
        ".KhEVhVEhK..",
        ".KhEVhVEhK..",
        ".KhhhhhhhhK.",
        ".KhwwwwwwhK.",
        "..KwwwwwwK..",
        "..KwwwwwwK..",
        "...KwwwwK...",
        "...KwK.Kw...",
        "..Kww..wwK..",
        ".Kw......wK.",
        ".K........K.",
        "..K......K..",
    }, new Dictionary<char, Color>
    {
        ['K'] = GameMath.Rgb(8, 6, 16),
        ['h'] = GameMath.Rgb(38, 28, 64),
        ['w'] = GameMath.Rgb(58, 42, 96),
        ['E'] = GameMath.Rgb(255, 224, 102),
        ['V'] = GameMath.Rgb(255, 242, 176),
    });

    public static readonly Sprite Sentinel = Sprite.Parse(new[]
    {
        ".....KKKK.....",
        "...KKaaaaKK...",
        "..KaaaaaaaaK..",
        ".KaaVVVVVVaaK.",
        ".KaVVEEEEVVaK.",
        ".KaaVVVVVVaaK.",
        "..KaaaaaaaaK..",
        "...KaaaaaaK...",
        "....KaEEaK....",
        "...KaaaaaaK...",
        "..KaK....KaK..",
        ".KaK......KaK.",
        "K.K........K.K",
    }, new Dictionary<char, Color>
    {
        ['K'] = GameMath.Rgb(12, 16, 22),
        ['a'] = GameMath.Rgb(74, 106, 138),
        ['V'] = GameMath.Rgb(34, 48, 76),
        ['E'] = GameMath.Rgb(102, 255, 224),
    });

    /// <summary>
    /// Кристалл освещения. Тёмно-синий каменный постамент со светящимися
 /// жилами и высокий циановый кристалл с белой сердцевиной.
    /// </summary>
    public static readonly Sprite SpriteCrystal = Sprite.Parse(new[]
    {
   "..............K..............",
        ".............KSK.............",
        "............KSCSK............",
        "...........KSCCCSK...........",
        "..........KSCCCCCSK..........",
   ".........KSCccccCCS K.........",
        ".........KSCcfffcCSK.........",
     "........KSCCffWWffCCSK........",
        "........KSCCffWWffCCSK........",
      "........KSCcfffcCSK..........",
        ".........KSCccccCSK..........",
     ".........KSCCccCCSK...........",
      "........KSCCCCCCSK...........",
        ".......KSCCCCCCCCSK..........",
        "......KSCCCffCCCCCSK.........",
        ".....KSCCCffWWffCCCcK........",
        "....KSCCCCffWWffCCCCSK.......",
        "...KSCCCCCffWWffCCCCCSK......",
        "..KSCCCCCCffWWffCCCCCCSK.....",
        "..KSCCCCCCffffCCCCCCCCSK.....",
        ".KSCCCCCCCCCCCccccCCCCCSK....",
        ".KSCCCCCCCCCCSSSSCCCCCCSK....",
        ".KsSSSSSSSSSSSSSSSSSSSsCK....",
        ".KsPPPPPPPPPPPPPPPPPPPPsCK....",
   ".KsPPaPPaPPaPPaPPaPPaPPsCK....",
        ".KsPPaPPaPPaPPaPPaPPaPPsCK....",
        ".KsPPPPPPPPPPPPPPPPPPPPsCK....",
        ".KssPPPPPPPPPPPPPPPPPPssK.....",
        ".KsssPPPPPPPPPPPPPPPPsssK.....",
 "..KssssPPPPPPPPPPPPPPssssK.....",
        "..KsssssPPPPPPPPPPPPssssK......",
        "...KsssssPPPPPPPPPPssssK......",
      "....KsssssPPPPPPPPPssssK......",
      ".....KsssssPPPPPPPssssK.......",
        "......KsssssPPPPPPssssK.......",
      ".......KKssssssssssssKK.......",
    }, new Dictionary<char, Color>
    {
        ['K'] = GameMath.Rgb(6, 7, 18),
     ['S'] = GameMath.Rgb(28, 34, 66),
        ['s'] = GameMath.Rgb(22, 26, 52),
  ['C'] = GameMath.Rgb(48, 96, 178),
   ['c'] = GameMath.Rgb(96, 176, 244),
        ['f'] = GameMath.Rgb(150, 220, 255),
        ['W'] = GameMath.Rgb(226, 246, 255),
        ['P'] = GameMath.Rgb(30, 34, 62),
        ['a'] = GameMath.Rgb(58, 108, 190),
        ['d'] = GameMath.Rgb(20, 24, 48),
    });

    /// <summary>Страж Эфира: заострённые наплечники, парящие обломки, кристалл-глаз.</summary>
    public static readonly BossPart[] BossParts =
    {
        new(6f, 0f, 2f, 4f, 'H', 1),
        new(18f, 0f, 2f, 4f, 'H', 1),
        new(9f, 0f, 8f, 7f, 'a', 1),
        new(10f, 0f, 6f, 2f, 'h', 1),
        new(9f, 4f, 8f, 2f, 'V', 1),
        new(2f, 6f, 22f, 5f, 'A', 0),
        new(2f, 6f, 22f, 1f, 'h', 0),
        new(0f, 7f, 4f, 10f, 'a', 2),
        new(0f, 7f, 4f, 2f, 'h', 2),
        new(22f, 7f, 4f, 10f, 'a', 3),
        new(22f, 7f, 4f, 2f, 'h', 3),
        new(5f, 10f, 16f, 8f, 'A', 0),
        new(10f, 12f, 6f, 6f, 'E', 4),
        new(11f, 13f, 4f, 4f, 'e', 4),
        new(6f, 18f, 14f, 3f, 'A', 0),
        new(6f, 21f, 5f, 4f, 'a', 0),
        new(15f, 21f, 5f, 4f, 'a', 0),
    };

    /// <summary>Гнилой Король: рваный силуэт, корона из костей, трон в глазу.</summary>
    public static readonly BossPart[] RotKingParts =
    {
        new(6f, 0f, 3f, 3f, 'B', 5),
        new(10f, 0f, 3f, 3f, 'B', 5),
        new(14f, 0f, 3f, 3f, 'B', 5),
        new(3f, 2f, 2f, 2f, 'B', 6),
        new(19f, 2f, 2f, 2f, 'B', 6),
        new(8f, 3f, 10f, 6f, 'E', 1),
        new(9f, 3f, 3f, 2f, 'G', 0),
        new(14f, 3f, 3f, 2f, 'G', 0),
        new(11f, 4f, 4f, 3f, 'V', 4),
        new(10f, 6f, 6f, 2f, 'G', 0),
        new(1f, 9f, 24f, 4f, 'c', 0),
        new(3f, 9f, 20f, 1f, 'G', 0),
        new(0f, 11f, 4f, 8f, 'g', 2),
        new(2f, 11f, 2f, 2f, 'G', 2),
        new(22f, 11f, 4f, 8f, 'g', 3),
        new(20f, 11f, 2f, 2f, 'G', 3),
        new(6f, 13f, 14f, 6f, 'c', 0),
        new(8f, 13f, 2f, 6f, 'B', 0),
        new(12f, 13f, 2f, 6f, 'B', 0),
        new(16f, 13f, 2f, 6f, 'B', 0),
        new(10f, 15f, 6f, 2f, 'B', 0),
        new(3f, 19f, 20f, 4f, 'c', 0),
        new(5f, 21f, 3f, 2f, 'B', 0),
        new(11f, 22f, 4f, 1f, 'B', 0),
        new(18f, 21f, 3f, 2f, 'B', 0),
    };

    /// <summary>Пепельный Голем: широкий блок, раскалённые трещины, жар в груди.</summary>
    public static readonly BossPart[] AshGolemParts =
    {
        new(1f, 0f, 26f, 6f, 'L', 0),
        new(3f, 1f, 22f, 2f, 'C', 0),
        new(9f, 1f, 8f, 1f, 'C', 0),
        new(0f, 6f, 30f, 7f, 'L', 0),
        new(4f, 7f, 22f, 1f, 'C', 0),
        new(0f, 13f, 6f, 11f, 'L', 2),
        new(24f, 13f, 6f, 11f, 'L', 3),
        new(4f, 13f, 22f, 12f, 'L', 0),
        new(7f, 14f, 16f, 1f, 'C', 0),
        new(9f, 16f, 12f, 1f, 'C', 0),
        new(7f, 19f, 16f, 1f, 'C', 0),
        new(11f, 15f, 8f, 7f, 'E', 4),
        new(13f, 17f, 4f, 3f, 'e', 4),
        new(4f, 25f, 22f, 3f, 'L', 0),
        new(8f, 25f, 14f, 1f, 'C', 0),
        new(6f, 4f, 3f, 2f, 'S', 5),
        new(21f, 4f, 3f, 2f, 'S', 5),
        new(15f, 2f, 2f, 2f, 'S', 6),
    };

    public static readonly Color[] BossColors =
    {
        GameMath.Rgb(14, 12, 20),
        GameMath.Rgb(64, 70, 96),
        GameMath.Rgb(104, 116, 150),
        GameMath.Rgb(76, 88, 116),
        GameMath.Rgb(150, 110, 255),
        GameMath.Rgb(255, 240, 200),
        GameMath.Rgb(255, 120, 90),
        GameMath.Rgb(255, 196, 120),
    };

    /// <summary>Палитра Короля: гнилая плоть, кость, мшистая зелень.</summary>
    public static readonly Color[] RotKingColors =
    {
        GameMath.Rgb(22, 30, 22),
        GameMath.Rgb(58, 88, 54),
        GameMath.Rgb(150, 158, 132),
        GameMath.Rgb(38, 56, 36),
        GameMath.Rgb(150, 255, 190),
        GameMath.Rgb(238, 234, 210),
        GameMath.Rgb(120, 200, 96),
        GameMath.Rgb(226, 222, 196),
    };

    /// <summary>Палитра Голема: обсидиан, тлеющие трещины, угли.</summary>
    public static readonly Color[] AshGolemColors =
    {
        GameMath.Rgb(16, 10, 8),
        GameMath.Rgb(92, 56, 46),
        GameMath.Rgb(132, 84, 66),
        GameMath.Rgb(108, 66, 54),
        GameMath.Rgb(255, 208, 120),
        GameMath.Rgb(255, 236, 190),
        GameMath.Rgb(255, 140, 60),
        GameMath.Rgb(255, 176, 92),
    };

    private static Color BossColor(char key) => key switch
    {
        'K' => BossColors[0],
        'A' => BossColors[1],
        'a' => BossColors[2],
        'h' => BossColors[3],
        'V' => BossColors[4],
        'E' => BossColors[5],
        'e' => BossColors[6],
        'H' => BossColors[7],
        _ => BossColors[1],
    };

    private static Color RotKingColor(char key) => key switch
    {
        'c' => RotKingColors[1],
        'g' => RotKingColors[2],
        'G' => RotKingColors[3],
        'V' => RotKingColors[4],
        'E' => RotKingColors[5],
        'e' => RotKingColors[6],
        'B' => RotKingColors[7],
        _ => RotKingColors[0],
    };

    private static Color AshGolemColor(char key) => key switch
    {
        'L' => AshGolemColors[1],
        'C' => AshGolemColors[7],
        'E' => AshGolemColors[5],
        'e' => AshGolemColors[6],
        'V' => AshGolemColors[4],
        'S' => AshGolemColors[6],
        _ => AshGolemColors[0],
    };

    public static readonly float BossWidth = 26f;

    /// <summary>Ширина спрайта выбранного босса - нужна для попаданий и отрисовки.</summary>
    public static float BossWidthFor(BossKind kind) => kind switch
    {
        BossKind.RotKing => 26f,
        BossKind.AshGolem => 30f,
        _ => BossWidth,
    };

    /// <summary>Высота спрайта выбранного босса.</summary>
    public static float BossHeightFor(BossKind kind) => kind switch
    {
        BossKind.RotKing => 24f,
        BossKind.AshGolem => 28f,
        _ => BossHeight,
    };

    public static readonly float BossHeight = 25f;

    public static void DrawBoss(Graphics g, float x, float y, float time, float phase, float flash)
        => DrawBoss(g, x, y, time, phase, flash, 0);

    /// <summary>
    /// Отрисовка босса. <paramref name="variant"/> - индекс в BossDef.All:
    /// 0 Страж, 1 Король, 2 Голем. У каждого свой набор частей и палитра.
    /// </summary>
    public static void DrawBoss(Graphics g, float x, float y, float time, float phase, float flash, int variant)
    {
        BossPart[] parts = variant switch
        {
            1 => RotKingParts,
            2 => AshGolemParts,
            _ => BossParts,
        };

        float bob = MathF.Sin(time * 2.1f) * 1.4f;
        float swing = MathF.Sin(time * 3.1f + phase) * 2.2f;
        float pulse = 0.6f + 0.4f * MathF.Sin(time * 6f);
        SolidBrush? white = flash > 0.01f ? Palette.Brush(Palette.Fade(Color.White, flash)) : null;

        // Голем дышит медленно и тяжело, Король подрагивает, Страж парит ровно.
        float breathe = variant == 2 ? MathF.Sin(time * 1.3f) * 0.5f : pulse * 0.25f;
        float tremble = variant == 1 ? MathF.Sin(time * 11f) * 0.4f : 0f;

        foreach (BossPart part in parts)
        {
            float ox = tremble;
            float oy = bob;
            float s = 1f + breathe;
            switch (part.Group)
            {
                case 1:
                    oy = bob;
                    break;
                case 2:
                    oy = -swing;
                    ox += -1.5f;
                    break;
                case 3:
                    oy = swing;
                    ox += 1.5f;
                    break;
                case 4:
                {
                    // Кристалл / трон / угол: пульсирует вокруг собственного центра.
                    float grow = (1f - s) * part.Rect.Width * 0.5f;
                    ox += grow;
                    oy += grow;
                    break;
                }
                case 5:
                case 6:
                {
                    // Корона / искры: слегка тянутся к центру при пульсации.
                    ox += (part.Rect.X + part.Rect.Width * 0.5f - 13f) * pulse * 0.25f;
                    oy += (part.Rect.Y + part.Rect.Height * 0.5f) * 0.15f * pulse;
                    break;
                }
            }

            float w = part.Group == 4 ? part.Rect.Width * s : part.Rect.Width;
            float h = part.Group == 4 ? part.Rect.Height * s : part.Rect.Height;
            RectangleF r = new(x + part.Rect.X + ox, y + part.Rect.Y + oy, w, h);
            if (white is not null)
            {
                g.FillRectangle(white, r);
                continue;
            }

            Color c = variant switch
            {
                1 => RotKingColor(part.Key),
                2 => AshGolemColor(part.Key),
                _ => BossColor(part.Key),
            };

            if (part.Group == 4)
            {
                // Глаз босса: от холодного к раскалённому по мере фаз.
                Color cold = variant == 1
                    ? Color.FromArgb(226, 222, 198)
                    : variant == 2
                        ? Color.FromArgb(255, 236, 190)
                        : Color.FromArgb(255, 250, 230);
                Color hot = variant == 1
                    ? Color.FromArgb(150, 255, 190)
                    : Color.FromArgb(255, 130, 60);
                c = part.Key == 'E'
                    ? GameMath.Mix(cold, hot, 0.35f + 0.35f * MathF.Sin(time * 3f + phase) + phase * 0.12f)
                    : hot;
            }
            else if (part.Key == 'V')
            {
                Color cold = variant == 2
                    ? Color.FromArgb(255, 236, 190)
                    : Color.FromArgb(120, 240, 255);
                c = GameMath.Mix(cold, Color.FromArgb(255, 110, 90), phase / 3f);
            }
            else if (variant == 2 && part.Key == 'C')
            {
                // Трещины Голема светятся сильнее к финальной фазе.
                c = GameMath.Mix(c, Color.FromArgb(255, 190, 90), 0.25f + 0.5f * (phase / 3f));
            }

            g.FillRectangle(Palette.Brush(c), r);
        }
    }
}
