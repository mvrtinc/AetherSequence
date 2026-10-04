using System.Diagnostics;
using AetherSequence.Combat;
using AetherSequence.Core;
using AetherSequence.Net;

namespace AetherSequence;

internal static class PerfProbe
{
    public static void Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== render cost probe ===");

        Game game = DevTools.NewGame();
        game.Settings.ShowFps = true;
        game.StartRun(4242);
        DevTools.Bot(game, 60 * 8, new Rng(1), false);
        game.Player.Hp = game.Player.MaxHp;
        game.Player.Alive = true;

        GameRenderer renderer = new();
        using System.Drawing.Bitmap world = new((int)GameRenderer.Width, (int)GameRenderer.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using System.Drawing.Graphics worldG = System.Drawing.Graphics.FromImage(world);
        worldG.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        worldG.Clear(System.Drawing.Color.Black);
        worldG.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;

        using System.Drawing.Bitmap ui = new(1920, 1080, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using System.Drawing.Graphics uiG = System.Drawing.Graphics.FromImage(ui);

        Measure("мир 640x360 (текущий)", () => renderer.DrawWorldOnly(worldG, game), 200);

        Game empty = DevTools.NewGame();
        empty.StartRun(4242);
        Measure("мир 640x360 (пустая карта)", () => renderer.DrawWorldOnly(worldG, empty), 200);
        Console.WriteLine($"[probe] сущностей: врагов={game.Enemies.Count} пуль={game.Projectiles.Count} бонусов={game.Pickups.Count}");

        AetherSequence.Art.Palette.GlowsDisabled = true;
        Measure("мир БЕЗ свечений", () => renderer.DrawWorldOnly(worldG, game), 200);
        AetherSequence.Art.Palette.GlowsDisabled = false;

        Measure("интерфейс нативно 1920x1080", () => renderer.DrawHudOnly(uiG, game, 3f), 120);
        Measure("ИТОГО двухслойный 1920x1080", () =>
        {
            renderer.DrawWorldOnly(worldG, game);
            renderer.DrawHudOnly(uiG, game, 3f);
        }, 150);

        Console.WriteLine("[probe] бюджет кадра при 60 FPS = 16,7ms");
        BlitProbe(world, ui);
        DuelProbe(renderer, worldG, uiG);
        Console.WriteLine("=== done ===");
    }

    /// <summary>
    /// Финальные блиты буферов на окно. В обычном пробнике их нет, а в живой
    /// игре именно они рисуются дважды на весь экран - и с HighQualityBilinear.
    /// </summary>
    private static void BlitProbe(System.Drawing.Bitmap world, System.Drawing.Bitmap ui)
    {
        Console.WriteLine();

        using System.Drawing.Bitmap screen = new(1920, 1080, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using System.Drawing.Graphics sg = System.Drawing.Graphics.FromImage(screen);
        sg.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;

        RectangleF full = new(0f, 0f, 1920f, 1080f);
        RectangleF src = new(0f, 0f, (float)world.Width, (float)world.Height);

        Measure("блит мира 640->1920 HighQualityBilinear", () =>
        {
            sg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            sg.DrawImage(world, full, src, System.Drawing.GraphicsUnit.Pixel);
        }, 60);

        Measure("блит мира 640->1920 Bilinear", () =>
        {
            sg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            sg.DrawImage(world, full, src, System.Drawing.GraphicsUnit.Pixel);
        }, 60);

        Measure("блит мира 640->1920 NearestNeighbor", () =>
        {
            sg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            sg.DrawImage(world, full, src, System.Drawing.GraphicsUnit.Pixel);
        }, 60);

        Measure("блит панели 1:1 HighQualityBilinear", () =>
        {
            sg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            sg.DrawImage(ui, full);
        }, 60);

        Measure("блит панели 1:1 NearestNeighbor", () =>
        {
            sg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            sg.DrawImage(ui, full);
        }, 60);

        // Мир непрозрачен, поэтому ему не нужна альфа: буфер без неё + SourceCopy.
        using System.Drawing.Bitmap opaque = new(world.Width, world.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        using (System.Drawing.Graphics og = System.Drawing.Graphics.FromImage(opaque))
        {
            og.DrawImage(world, new Rectangle(0, 0, opaque.Width, opaque.Height));
        }

        Measure("блит мира RGB+SourceCopy NearestNeighbor", () =>
        {
            sg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            sg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            sg.DrawImage(opaque, full, src, System.Drawing.GraphicsUnit.Pixel);
            sg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
        }, 60);

        Measure("блит мира RGB+SourceCopy HighQuality", () =>
        {
            sg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            sg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            sg.DrawImage(opaque, full, src, System.Drawing.GraphicsUnit.Pixel);
            sg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
        }, 60);
    }

    /// <summary>
    /// Замер именно дуэльного боя: в обычном пробнике его нет, поэтому
    /// просадка кадров в дуэли оставалась незамеченной.
    /// </summary>
    private static void DuelProbe(GameRenderer renderer, System.Drawing.Graphics worldG, System.Drawing.Graphics uiG)
    {
        Console.WriteLine();

        Game game = DevTools.NewGame();
        game.Settings.ShowFps = true;
        game.StartRun(777);
        DuelSession duel = new(game) { Nick = "МАГ" };
        duel.StartHosting(withBot: true);
        if (duel.Host is null || !duel.IsHost || duel.PeerCount == 0)
        {
            // Раньше этот случай падал молча и пробник мерил пустую сцену:
            // порты могли быть заняты уже запущенной игрой.
            Console.WriteLine("[probe] дуэль: НЕ УДАЛОСЬ поднять хост - вероятно, порты 47800/47801 заняты");
            return;
        }

        duel.StartDuelWithBot();
        if (!game.DuelMode || duel.Phase != DuelPhase.Fighting)
        {
            Console.WriteLine($"[probe] дуэль: бой не начался DuelMode={game.DuelMode} Phase={duel.Phase}");
            return;
        }

        duel.Host.Foe = game.Foe;
        game.State = GameState.Playing;

        // Разогрев реальной боевой сцены. Бот сам стреляет редко, поэтому кастуем
        // за обоих бойцов - иначе пробник меряет почти пустой мир.
        List<SpellDef> duelSpells = SpellDB.All;
        int duelSpellIx = 0;
        for (int i = 0; i < 300; i++)
        {
            if (i % 10 == 0 && duelSpells.Count > 0)
            {
                SpellDef def = duelSpells[duelSpellIx++ % duelSpells.Count];
                CombatUtil.Cast(game, game.Player, def, false);
                if (game.Foe is not null) CombatUtil.Cast(game, game.Foe, def, false);
            }

            DuelStep();
        }

        Console.WriteLine($"[probe] дуэль: пуль={game.Projectiles.Count} частиц={game.Effects.Particles.Count} " +
            $"снарядов в сети={duel.RenderShots.Count} тик={game.DuelTick}");

        Measure("дуэль: мир 640x360", () => renderer.DrawWorldOnly(worldG, game), 200);
        Measure("дуэль: панель 1920x1080", () => renderer.DrawHudOnly(uiG, game, 3f), 200);
        Measure("дуэль: мир + панель", () =>
        {
            renderer.DrawWorldOnly(worldG, game);
            renderer.DrawHudOnly(uiG, game, 3f);
        }, 200);

        Measure("дуэль: обновление игры", () => DuelStep(), 200);

        // Полный кадр ровно как в GameForm.OnPaint после правок.
        using System.Drawing.Bitmap worldBuf = new(
            (int)GameRenderer.Width, (int)GameRenderer.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        using System.Drawing.Bitmap screenBuf = new(1920, 1080, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using System.Drawing.Graphics screenG = System.Drawing.Graphics.FromImage(screenBuf);
        Measure("дуэль: КАДР ЦЕЛИКОМ (как в игре)", () =>
        {
            using (System.Drawing.Graphics wg = System.Drawing.Graphics.FromImage(worldBuf))
            {
                wg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                wg.Clear(System.Drawing.Color.Black);
                wg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                renderer.DrawWorldOnly(wg, game);
            }

            screenG.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            screenG.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            screenG.DrawImage(worldBuf, new System.Drawing.RectangleF(0f, 0f, 1920f, 1080f),
                new System.Drawing.RectangleF(0f, 0f, (float)worldBuf.Width, (float)worldBuf.Height),
                System.Drawing.GraphicsUnit.Pixel);
            screenG.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            renderer.DrawHudOnly(screenG, game, 3f, true, System.Drawing.PointF.Empty);
            DuelStep();
        }, 150);

        // Альтернатива буферу: рисовать мир сразу на экран с масштабом.
        Measure("дуэль: мир сразу на экран (scale 3)", () =>
        {
            screenG.ResetTransform();
            screenG.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            screenG.Clear(System.Drawing.Color.Black);
            screenG.ScaleTransform(3f, 3f);
            renderer.DrawWorldOnly(screenG, game);
            screenG.ResetTransform();
        }, 150);

        AetherSequence.Art.Palette.GlowsDisabled = true;
        Measure("дуэль: мир без свечений", () => renderer.DrawWorldOnly(worldG, game), 200);
        AetherSequence.Art.Palette.GlowsDisabled = false;

        // Свет и пыль - единственное, что добавилось к миру и не кэшируется.
        Measure("дуэль: только свет (без мира)", () =>
        {
            AetherSequence.Art.Lighting.DrawPools(worldG, game, game.Camera.Position);
            AetherSequence.Art.Lighting.DrawPlayerPool(worldG, game, game.Camera.Position);
        }, 200);

        Measure("дуэль: только пыль", () =>
            AetherSequence.Art.Lighting.DrawDust(worldG, game, game.Camera.Position, 1f), 200);

        duel.Dispose();

        void DuelStep()
        {
            game.UpdateDuel(1f / 60f);
            duel.Tick(1f / 60f);
        }
    }

    private static void Measure(string label, Action action, int frames)
    {
        try
        {
            for (int i = 0; i < 25; i++) action();
        }
        catch (Exception e)
        {
            Console.WriteLine($"[probe] {label,-26} ОШИБКА: {e.GetType().Name} {e.Message}");
            return;
        }
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < frames; i++) action();
        sw.Stop();
        double ms = sw.Elapsed.TotalMilliseconds / frames;
        Console.WriteLine($"[probe] {label,-26} {ms,6:0.00}ms/кадр");
    }
}
