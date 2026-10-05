using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using AetherSequence.Art;
using AetherSequence.Combat;
using AetherSequence.Core;
using AetherSequence.Net;

namespace AetherSequence;

/// <summary>
/// Замер настоящего кадра: рисуем в буфер 640x360, растягиваем на экран
/// 1920x1080 и поверх рисуем HUD - ровно то, что делает GameForm.OnPaint.
/// Синтетические пробники меряли только части пути и показывали 15-20 FPS,
/// хотя в игре кадр укладывался в бюджет; этот замер закрывает весь путь.
/// </summary>
internal static class FrameProbe
{
    public static void Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== real frame probe (весь путь кадра) ===");

        Game game = DevTools.NewGame();
        game.Settings.ShowFps = true;
        game.StartRun(20260804u);

        DuelSession duel = new(game) { Nick = "МАГ" };
        duel.StartHosting(withBot: true);
        if (duel.Host is null)
        {
            Console.WriteLine("[frame] не удалось поднять хост - порты заняты?");
            return;
        }

        duel.StartDuelWithBot();
        game.State = GameState.Playing;

        // Наполняем сцену: кастуем за обоих бойцов.
        for (int i = 0; i < 300; i++)
        {
            if (i % 10 == 0 && Spells().Count > 0)
            {
                SpellDef def = Spells()[i / 10 % Spells().Count];
                CombatUtil.Cast(game, game.Player, def, false);
                if (game.Foe is not null) CombatUtil.Cast(game, game.Foe, def, false);
            }

            game.UpdateDuel(1f / 60f);
            duel.Tick(1f / 60f);
        }

        Console.WriteLine($"[frame] сцена: пуль={game.Projectiles.Count} частиц={game.Effects.Particles.Count}");

        GameRenderer renderer = new();
        using Bitmap world = new(
            (int)GameRenderer.Width, (int)GameRenderer.Height, PixelFormat.Format32bppRgb);
        using Bitmap screen = new(1920, 1080, PixelFormat.Format32bppPArgb);
        using Graphics sg = Graphics.FromImage(screen);

Measure("весь кадр (как в игре)", () =>
   {
     FrameDrawImage(renderer, game, duel, world, sg);
        }, 150);

        // Без пыли и без света - сколько они реально стоят.
renderer.DustDensity = 0f;
        Measure("кадр без пыли", () => FrameDrawImage(renderer, game, duel, world, sg), 100);
        renderer.DustDensity = 1f;

        Palette.GlowsDisabled = true;
  Measure("кадр без свечений", () => FrameDrawImage(renderer, game, duel, world, sg), 100);
        Palette.GlowsDisabled = false;

        Console.WriteLine("[frame] бюджет кадра при 60 FPS = 16,7ms");

   // Разложение кадра по частям: где именно уходит время.
        Measure("часть A: только мир в буфер", () =>
        {
        using Graphics wg = Graphics.FromImage(world);
  wg.CompositingMode = CompositingMode.SourceCopy;
            wg.Clear(Color.Black);
      wg.CompositingMode = CompositingMode.SourceOver;
            renderer.DrawWorldOnly(wg, game);
        }, 120);

        Measure("часть B: только растяжка на экран", () =>
        {
            sg.CompositingMode = CompositingMode.SourceCopy;
            sg.InterpolationMode = InterpolationMode.NearestNeighbor;
            sg.CompositingQuality = CompositingQuality.HighSpeed;
         sg.DrawImage(world, new RectangleF(0f, 0f, 1920f, 1080f),
   new RectangleF(0f, 0f, (float)world.Width, (float)world.Height),
    GraphicsUnit.Pixel);
        }, 120);

        Measure("часть C: только HUD", () => renderer.DrawHudOnly(sg, game, 3f, true, PointF.Empty), 120);

Measure("часть D: обновление игры", () =>
        {
     game.UpdateDuel(1f / 60f);
        duel.Tick(1f / 60f);
  }, 120);

        // Отдельный замер одиночной игры с темнотой. Пробник выше меряет
        // дуэль, а в дуэли темноты нет по решению - без этого замера
        // цена маски света осталась бы незамеченной.
        DarkFrameMeasure(renderer, world, sg);

        BlitCompare(world, screen);

        Console.WriteLine("=== done ===");

        duel.Dispose();

        static List<SpellDef> Spells() => SpellDB.All;

        /// <summary>Тот же кадр, но растяжкой через обычный DrawImage GDI+.</summary>
        static void FrameDrawImage(GameRenderer renderer, Game game, DuelSession duel, Bitmap world, Graphics sg)
     {
      using Graphics wg = Graphics.FromImage(world);
    wg.CompositingMode = CompositingMode.SourceCopy;
    wg.Clear(Color.Black);
 wg.CompositingMode = CompositingMode.SourceOver;
            renderer.DrawWorldOnly(wg, game);

   sg.CompositingMode = CompositingMode.SourceCopy;
     sg.InterpolationMode = InterpolationMode.NearestNeighbor;
         sg.CompositingQuality = CompositingQuality.HighSpeed;
     sg.DrawImage(world,
    new RectangleF(0f, 0f, 1920f, 1080f),
        new RectangleF(0f, 0f, (float)world.Width, (float)world.Height),
  GraphicsUnit.Pixel);
            sg.CompositingMode = CompositingMode.SourceOver;
    renderer.DrawHudOnly(sg, game, 3f, true, PointF.Empty);

            game.UpdateDuel(1f / 60f);
            duel.Tick(1f / 60f);
        }
    }

/// <summary>
    /// Замер кадра одиночной игры: тут включается темнота и маска света.
    /// Именно этот сценарий решает, уложится ли игра в бюджет кадра -
 /// пробник выше меряет дуэль, а в дуэли темноты нет.
    /// </summary>
    private static void DarkFrameMeasure(GameRenderer renderer, Bitmap world, Graphics sg)
    {
Console.WriteLine("[frame] --- одиночная игра с темнотой ---");

        Game solo = DevTools.NewGame();
  solo.Settings.ShowFps = true;
        solo.Settings.GraphicsQuality = 2;
        solo.StartRun(20260806u);
        solo.State = GameState.Playing;

        // Наполняем сцену: снаряды, частицы, враги в движении.
        for (int i = 0; i < 240; i++)
    {
            if (i % 9 == 0 && SpellDB.All.Count > 0)
            {
     SpellDef def = SpellDB.All[i / 9 % SpellDB.All.Count];
    CombatUtil.Cast(solo, solo.Player, def, false);
}
            solo.Update(1f / 60f);
        }

        Console.WriteLine($"[frame] сцена: врагов={solo.Enemies.Count} пуль={solo.Projectiles.Count} " +
  $"частиц={solo.Effects.Particles.Count} кристаллов={solo.Crystals.Count}");

        Measure("темнота: весь кадр", () => FrameSolo(renderer, solo, world, sg), 150);

        Console.WriteLine($"[frame] маска света: накопление={LightMaskTimings.LastAccumulateMs:0.00}ms " +
            $"заливка bitmap={LightMaskTimings.LastPaintMs:0.00}ms " +
 $"наложение={LightMaskTimings.LastApplyMs:0.00}ms");

        // Тот же кадр, но темнота выключена - честное сравнение.
        solo.Settings.GraphicsQuality = 0;
Measure("без темноты: весь кадр", () => FrameSolo(renderer, solo, world, sg), 150);
solo.Settings.GraphicsQuality = 2;
    }

    private static void FrameSolo(GameRenderer renderer, Game solo, Bitmap world, Graphics sg)
    {
   using Graphics wg = Graphics.FromImage(world);
        wg.CompositingMode = CompositingMode.SourceCopy;
        wg.Clear(Color.Black);
        wg.CompositingMode = CompositingMode.SourceOver;
        renderer.DrawWorldOnly(wg, solo);

        sg.CompositingMode = CompositingMode.SourceCopy;
        sg.InterpolationMode = InterpolationMode.NearestNeighbor;
        sg.CompositingQuality = CompositingQuality.HighSpeed;
sg.DrawImage(world, new RectangleF(0f, 0f, 1920f, 1080f),
      new RectangleF(0f, 0f, (float)world.Width, (float)world.Height), GraphicsUnit.Pixel);
     sg.CompositingMode = CompositingMode.SourceOver;
        renderer.DrawHudOnly(sg, solo, 3f, true, PointF.Empty);

 solo.Update(1f / 60f);
    }

    /// <summary>
    /// Растяжка 640x360 -> 1920x1080. GDI+ DrawImage тратит на это ~13 мс,
    /// то есть почти весь кадр, и это потолок именно этой машины.
    /// </summary>
    private static void BlitCompare(Bitmap src, Bitmap dst)
    {
Measure("растяжка GDI+ DrawImage", () =>
   {
      using Graphics g = Graphics.FromImage(dst);
  g.CompositingMode = CompositingMode.SourceCopy;
      g.InterpolationMode = InterpolationMode.NearestNeighbor;
g.DrawImage(src, new Rectangle(0, 0, dst.Width, dst.Height));
    }, 60);
    }

    private static void Measure(string label, Action action, int frames)
    {
        for (int i = 0; i < 20; i++) action();
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < frames; i++) action();
        sw.Stop();
        double ms = sw.Elapsed.TotalMilliseconds / frames;
        double fps = 1000f / ms;
        string verdict = ms <= 16.7f ? "ok" : "ПРОБЛЕМА";
        Console.WriteLine($"[frame] {label,-28} {ms,6:0.00}ms  {fps,5:0} FPS  {verdict}");
    }
}
