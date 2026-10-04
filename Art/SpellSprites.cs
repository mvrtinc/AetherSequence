using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
using AetherSequence.Core;

namespace AetherSequence.Art;

/// <summary>
/// Растровые анимации заклинаний. Каждый спрайт-лист нарезается на кадры
/// один раз при первой загрузке, дальше только блитится нужный кадр.
///
/// Мир рисуется в 640x360, а кадры в листах по 128 px, поэтому кадр
/// масштабируется под размер эффекта. Пропорции листа сохраняются.
/// </summary>
internal static class SpellSprites
{
    /// <summary>Раскладка одного листа: сколько кадров и как они разложены.</summary>
    private sealed record Sheet(string File, int Columns, int Rows, int CellW, int CellH);

    private static readonly Dictionary<string, Sheet> Sheets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["arcane-nova"] = new("arcane-nova.png", 5, 2, 128, 128),
        ["claw-bolt"] = new("claw-bolt.png", 5, 2, 128, 128),
        ["void-field"] = new("void-field.png", 5, 3, 128, 128),
        ["ring-nova"] = new("ring-nova.png", 8, 1, 128, 128),
        ["shard-rain"] = new("shard-rain.png", 6, 1, 128, 128),
        ["smoke-bolt"] = new("smoke-bolt.png", 7, 1, 128, 128),
        ["fire-bolt"] = new("fire-bolt.png", 5, 2, 128, 128),
        ["comet-rain"] = new("comet-rain.png", 5, 3, 192, 128),
        ["shock-ring"] = new("shock-ring.png", 5, 3, 128, 128),
    };

    private sealed class Clip
    {
        public readonly Bitmap[] Frames;
        public readonly int CellW;
        public readonly int CellH;

        public Clip(Bitmap[] frames, int cellW, int cellH)
        {
            Frames = frames;
            CellW = cellW;
            CellH = cellH;
        }
    }

    private static readonly Dictionary<string, Clip?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static bool _searched;
    private static string? _folder;

    /// <summary>Папка с ассетами: ищем рядом с exe и в корне проекта (для отладки).</summary>
    private static string? Folder
    {
        get
        {
            if (_searched) return _folder;
            _searched = true;

            string baseDir = AppContext.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "Assets", "Spells"),
                Path.Combine(baseDir, "sprites"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Assets", "Spells")),
            };
            foreach (string c in candidates)
            {
                if (File.Exists(Path.Combine(c, "fire-bolt.png")))
                {
                    _folder = c;
                    return _folder;
                }
            }
            return null;
        }
    }

    /// <summary>Ассеты вообще нашлись - от этого зависит, будем ли мы что-то рисовать.</summary>
    public static bool Available => Folder is not null;

    /// <summary>Сколько кадров в листе, либо 0 если листа нет.</summary>
    public static int FrameCount(string key) => Ensure(key)?.Frames.Length ?? 0;

    /// <summary>
    /// Рисует кадр анимации. <paramref name="t01"/> - прогресс 0..1,
    /// <paramref name="size"/> - ширина эффекта в мировых пикселях.
    /// </summary>
    public static void Draw(Graphics g, string key, Vector2 center, float size, float t01)
    {
        Clip? clip = Ensure(key);
        if (clip is null || clip.Frames.Length == 0) return;

        float t = GameMath.Clamp01(t01);
        int index = Math.Clamp((int)(t * clip.Frames.Length), 0, clip.Frames.Length - 1);
        Bitmap frame = clip.Frames[index];

        // Эффект слегка разрастается за время жизни - читается как удар.
        float grow = 0.82f + 0.18f * MathF.Sqrt(t);
        float w = clip.CellW * (size / clip.CellW) * grow;
        float h = clip.CellH * (size / clip.CellW) * grow;
        RectangleF dest = new(center.X - w * 0.5f, center.Y - h * 0.5f, w, h);

        GraphicsState state = g.Save();
        g.CompositingMode = CompositingMode.SourceOver;
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(frame, dest);
        g.Restore(state);
    }

    private static Clip? Ensure(string key)
    {
        if (Cache.TryGetValue(key, out Clip? cached)) return cached;

        Clip? clip = Load(key);
        Cache[key] = clip;
        return clip;
    }

    private static Clip? Load(string key)
    {
        if (!Sheets.TryGetValue(key, out Sheet? sheet)) return null;
        string? folder = Folder;
        if (folder is null) return null;
        string path = Path.Combine(folder, sheet.File);
        if (!File.Exists(path)) return null;

        try
        {
            using Bitmap source = new(path);
            var frames = new Bitmap[sheet.Columns * sheet.Rows];
            int i = 0;
            for (int ry = 0; ry < sheet.Rows; ry++)
            {
                for (int cx = 0; cx < sheet.Columns; cx++)
                {
                    Rectangle cell = new(cx * sheet.CellW, ry * sheet.CellH, sheet.CellW, sheet.CellH);
                    cell.Intersect(new Rectangle(0, 0, source.Width, source.Height));

                    Bitmap frame = new(sheet.CellW, sheet.CellH, PixelFormat.Format32bppPArgb);
                    using Graphics fg = Graphics.FromImage(frame);
                    fg.CompositingMode = CompositingMode.SourceCopy;
                    fg.Clear(Color.Transparent);
                    fg.CompositingMode = CompositingMode.SourceOver;
                    fg.PixelOffsetMode = PixelOffsetMode.Half;
                    fg.DrawImage(source, new Rectangle(0, 0, sheet.CellW, sheet.CellH), cell, GraphicsUnit.Pixel);
                    frames[i++] = frame;
                }
            }
            return new Clip(frames, sheet.CellW, sheet.CellH);
        }
        catch (Exception)
        {
            // Битый или отсутствующий ассет не должен ронять игру - просто не рисуем.
            return null;
        }
    }
}
