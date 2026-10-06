using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using AetherSequence.Art;
using AetherSequence.Combat;
using AetherSequence.Core;
using AetherSequence.Entities;
using AetherSequence.Net;
using AetherSequence.Progression;
using AetherSequence.World;

namespace AetherSequence;

internal sealed class GameRenderer
{
    public const float Width = 640f;

    public const float Height = 360f;

    private const float HudTop = 316f;

    public void Draw(Graphics g, Game game)
    {
        g.CompositingMode = CompositingMode.SourceOver;
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        Text.Prepare(g);

        if (game.State == GameState.Title)
        {
            DrawTitle(g, game);
            return;
        }

        DrawWorld(g, game);
DrawScreenEffects(g, game);
   DrawHud(g, game);
    DrawOverlays(g, game);
    }

  public void DrawWorldOnly(Graphics g, Game game)
    {
        DrawWorldOnly(g, game, true);
    }

public void DrawWorldOnly(Graphics g, Game game, bool includeDuelLayer)
    {
      GraphicsState state = g.Save();
g.CompositingMode = CompositingMode.SourceOver;
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        Text.Prepare(g);
        bool duelFighting = game.DuelMode
            && game.Duel is { Phase: DuelPhase.Fighting or DuelPhase.RoundOver or DuelPhase.MatchOver };

        if (game.State == GameState.Title || (game.State == GameState.Duel && !duelFighting))
        {
            DrawTitle(g, game);
        }
        else
        {
            DrawWorld(g, game);
            if (duelFighting && includeDuelLayer) DrawDuelWorldLayer(g, game);
            DrawScreenEffects(g, game);
        }
        g.Restore(state);
    }

    /// <summary>Рисует второго игрока и его снаряды по данным из сети (для гостя).</summary>
    private float _duelClock;

    /// <summary>Плотность пыли в воздухе: 0 - выключена, 1 - обычная.</summary>
    public float DustDensity = 1f;

    /// <summary>Рисует соперника из сетевых снапшотов (для клиента).</summary>
    public void DrawDuelWorldLayer(Graphics g, Game game)
    {
        if (!game.DuelMode) return;
        DuelSession? duel = game.Duel;
        if (duel is null) return;
        _duelClock += 1f / 30f;
        DrawRemotePlayers(g, game, duel);
    }

    private void DrawRemotePlayers(Graphics g, Game game, DuelSession duel)
    {
        // Соперника в мире уже рисует DrawWorld (game.Foe), поэтому здесь
        // только снаряды - раньше они рисовались дважды, зря съедая кадр.
        Vector2 shotCam = game.Camera.Position;
        foreach (Net.ProjectileSnapshot shot in duel.RenderShots)
        {
            Vector2 pos = new Vector2(shot.X, shot.Y) - shotCam;
            float s = shot.Size;
            Palette.AddGlow(g, pos, s * 2.2f, Color.FromArgb(shot.ColorArgb), 0.5f);
            Palette.Fill(g, Color.FromArgb(shot.ColorArgb), pos.X - s * 0.5f, pos.Y - s * 0.5f, s, s);
        }

        DrawPeerLabel(g, game, duel);
    }

    /// <summary>Подпись над соперником: он рисуется как обычный игрок в DrawWorld.</summary>
    private void DrawPeerLabel(Graphics g, Game game, DuelSession duel)
    {
        Player? foe = game.Foe;
        if (foe is null || !foe.Alive) return;

        bool localIsHost = duel.LocalId == 0;
        string label = localIsHost ? "СОПЕРНИК" : "ХОСТ";
        Color tint = localIsHost ? Palette.Health : Palette.Flow;

        Vector2 screen = foe.Pos - game.Camera.Position - game.Camera.Offset;
        Text.DrawCentered(g, label, Text.Tiny, Palette.Fade(tint, 0.85f), screen.X, screen.Y - 14f);
    }

    public void DrawHudOnly(Graphics g, Game game, float scale, bool native = true, PointF origin = default)
    {
        if (game.State == GameState.Title) return;

        // В дуэли игровой HUD не рисуем: у duel своя панель со счётом.
        // Проверка именно на "не в бою" - иначе интерфейс пропадал на самом бою.
        if (game.DuelMode && game.Duel is not { Phase: DuelPhase.Fighting or DuelPhase.RoundOver or DuelPhase.MatchOver })
        {
            return;
        }
        GraphicsState state = g.Save();
        g.CompositingMode = CompositingMode.SourceOver;
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        if (native) Text.PrepareNative(g); else Text.Prepare(g);
        if (origin.X != 0f || origin.Y != 0f) g.TranslateTransform(origin.X, origin.Y);
        if (scale > 0.01f) g.ScaleTransform(scale, scale);

        // В дуэли своя панель: здоровье обоих и счёт. Игровой HUD с миникартой
        // там не рисуется - иначе поверх лезет панель одиночной игры.
        bool duelCombat = game.Duel is { Phase: DuelPhase.Fighting or DuelPhase.RoundOver or DuelPhase.MatchOver }
            && game.DuelMode;

        // Меню дуэли тоже без игрового HUD: DuelMode там ещё false,
        // а State == Duel, и одиночная полоска здоровья лезла поверх меню.
        bool duelMenu = game.State == GameState.Duel && !duelCombat;

        if (duelCombat)
        {
            DrawDuelHud(g, game);
        }
        else if (!duelMenu)
        {
            DrawHud(g, game);
        }

        DrawOverlays(g, game);
        g.Restore(state);
    }

    private void DrawWorld(Graphics g, Game game)
    {
        Vector2 cam = game.Camera.Position + game.Camera.Offset;
        Vector2 view = new(Width, Height);
        float ox = MathF.Round(cam.X);
        float oy = MathF.Round(cam.Y);

GraphicsState state = g.Save();
        g.TranslateTransform(-ox, -oy);

        // Маска считается до отрисовки: по ней решается, кого видно.
  BeginDarkness(game, cam);

   game.Level.DrawStaticMap(g, cam, view);

        // Качество графики из настроек: свет, пыль и мягкие тени.
   int quality = game.Settings.GraphicsQuality;
        if (quality > 0)
{
    Lighting.DrawPools(g, game, cam, _mask);
            Lighting.DrawPlayerPool(g, game, cam);
     }

     if (quality >= 2 && game.Settings.Dust)
        {
    Lighting.DrawDust(g, game, cam, DustDensity);
        }

        game.Level.DrawExit(g, game.Time, game.Level.ExitOpen);
      game.Effects.Draw(g, game.Time);

           // Кристаллы стоят на полу и тоже прячутся в темноте: неактивный
     // кристалл видно только там, куда дотягивается ореол героя.
        for (int i = 0; i < game.Crystals.Count; i++)
        {
  Crystal c = game.Crystals[i];
            bool canCharge = game.ChargeActive || c.Activated;
            if (!IsLitFor(c.Pos, cam) && !canCharge) continue;
            c.Draw(g, game.Time, !c.Activated && Vector2.Distance(game.Player.Pos, c.Pos) < Crystal.Reach);
        }

foreach (Pickup p in game.Pickups)
     {
       // В темноте бонусы не видны: иначе они светятся сквозь стены.
      if (!IsLitFor(p.Pos, cam)) continue;
      p.Draw(g, game.Time);
     }

foreach (Enemy e in game.Enemies)
        {
     // Главное правило темноты: за пределами света врага не видно
       // вообще. Раньше он просто рисовался, и в тёмной комнате был
        // виден так же хорошо, как в освещённой.
      //
       // Исключение - замах: атакующий должен быть виден всегда, иначе
  // удар из темноты выглядит как баг, а не как опасность.
     bool telegraphing = e.State == 1 && !e.IsBoss;
  bool visible = telegraphing || e.Dead || IsLitFor(e.Pos, cam);
     if (!visible) continue;
       e.Draw(g, game.Time);
        }
        if (game.Player.Alive) game.Player.Draw(g, game.Time);

        // В дуэли соперник - такой же игрок, его надо видеть в пещере.
        if (game.DuelMode && game.Foe is { } foe && foe.Alive) foe.Draw(g, game.Time);

foreach (Projectile pr in game.Projectiles) pr.Draw(g, game.Time);

g.Restore(state);

      // Темнота ложится на мир и сущности, но не на прицел и цифры урона.
        EndDarkness(g);

        game.Effects.DrawTexts(g, cam);
        DrawTargetLock(g, game);
        DrawCrosshair(g, game);

        // Виньетка, вспышка и подсветка неуязвимости. Раньше они жили
        // только в Draw(), а живая игра рисует мир через DrawWorldOnly -
        // из-за чего кадр оставался плоским и пересвеченным.
   DrawScreenEffects(g, game);
    }

    /// <summary>
    /// Темнота и свет. Маска пересчитывается раз в кадр и накрывает мир,
 /// а прицел и всплывающие цифры рисуются поверх неё - иначе ими
    /// невозможно целиться в темноте.
    ///
    /// В дуэли и на аренах боссов темноты нет: там своя механика и
    /// читаемость важнее.
 /// </summary>
/// <summary>
    /// Начинает темноту: собирает источники и считает маску.
    /// Это происходит ДО отрисовки сущностей, чтобы по маске можно было
 /// решить, кого вообще видно. Сама темнота накладывается в конце.
    /// </summary>
    private void BeginDarkness(Game game, Vector2 cam)
    {
        _gameRef = game;
   MaskCam = cam;

        if (!DarknessEnabled(game))
        {
            _dark = false;
       if (_mask is not null) _mask.Begin();
   return;
    }

        _dark = true;
        if (_mask is null) _mask = new LightMask((int)Width, (int)Height);

        _mask.Bind(game.Level, game.Level.Colors.Vignette);
        _mask.Darkness = 0.9f;
        _mask.Begin();

CollectLights(game, _mask, cam);
        _mask.Build(cam);

      // Туман войны: отмечаем то, куда падает свет. Планировка исследованного
        // остаётся на миникарте навсегда, а содержимое - нет.
        if (game.Player.Alive)
        {
   game.Level.MarkExplored(game.Player.Pos, game.ChargeActive ? 44f : 62f);

   // Зажжённые кристаллы тоже освещают комнату, поэтому исследованной
            // считается всё, что попало в их свет.
        for (int i = 0; i < game.Crystals.Count; i++)
       {
     Crystal c = game.Crystals[i];
    if (c.Activated) game.Level.MarkExplored(c.Pos, Crystal.LightRadius);
        }
      }
    }

    /// <summary>Накладывает темноту на уже нарисованный мир.</summary>
    private void EndDarkness(Graphics g)
    {
        if (!_dark || _mask is null) return;
        _mask.Apply(g, (int)Width, (int)Height);
    }

    private bool _dark;

    /// <summary>Темнота применяется только в одиночной игре на обычных уровнях.</summary>
    private static bool DarknessEnabled(Game game)
    {
        if (game.State != GameState.Playing) return false;
        if (game.DuelMode) return false;
        if (game.Level.IsBoss) return false;
    return game.Settings.GraphicsQuality > 0;
    }

    /// <summary>
    /// Наполняет маску источниками света: посох, аура игрока, снаряды
    /// (свет и огонь светят), портал и активированные кристаллы.
 /// </summary>
    private static void CollectLights(Game game, LightMask mask, Vector2 cam)
    {
        Player p = game.Player;

if (p.Alive)
        {
   // От героя идёт только ореол вокруг персонажа - фонарика нет.
       // Раньше свет шёл конусом по направлению прицела, и это читалось
       // как отдельный источник, а не как присутствие самого мага.
            Color aura = GameMath.Mix(Palette.Health, Elements.Color(p.LastElement), 0.35f);

        // Основной ореол. Во время зарядки кристалла он сжимается:
 // игрок в этот момент не должен хорошо видеть.
// Радиус подобран так, чтобы освещённая площадь сравнялась с бывшим
      // конусом: ореол должен уходить дальше по бокам, раз конуса больше нет.
  float radius = game.ChargeActive ? 34f : 58f;
            float intensity = game.ChargeActive ? 0.72f : 1.1f;
            mask.Add(LightSource.Circle(p.Pos, radius, aura, intensity, 0.85f));

     // Второй, более плотный слой у самых ног: отделяет фигуру от пола.
     mask.Add(LightSource.Circle(p.Pos, 24f, aura, intensity * 0.85f, 0.7f));
        }

    // Снаряды свет и огонь дают малый ореол - иначе в темноте не видно,
     // куда летят собственные огненные шары.
        for (int i = 0; i < game.Projectiles.Count; i++)
   {
         Projectile pr = game.Projectiles[i];
            if (pr.Dead) continue;

      bool glows = pr.Element == Element.Light || pr.Element == Element.Fire;
       if (!glows) continue;

    float r = pr.Element == Element.Light ? 34f : 26f;
            mask.Add(LightSource.Circle(pr.Pos, r, pr.Color, 0.62f, 0.8f));
        }

  if (game.Level.ExitOpen)
     {
    Vector2 exit = game.Level.ExitPos - cam;
     mask.Add(LightSource.Circle(exit + cam, 52f, game.Level.Colors.ExitOpen, 0.75f, 0.7f));
        }

// Активированные кристаллы светят постоянно и освещают комнату.
     // Цвет берётся из темы, чтобы кристалл был частью пещеры, а не
        // белым пятном поверх неё, и подмешивается с кристаллом для
   // насыщенности: серый неон в тёмной комнате выглядит вялым.
        for (int i = 0; i < game.Crystals.Count; i++)
        {
Crystal c = game.Crystals[i];
     if (!c.Activated) continue;

            Vector2 cp = c.Pos - cam;
            Color hot = GameMath.Mix(game.Level.Colors.Neon, Palette.ExitOpen, 0.45f);

            // Ядро: маленькое, но плотное - это и есть сам кристалл.
            mask.Add(LightSource.Circle(cp + cam, 34f, hot, 1.5f, 0.55f));

  // Основное пятно заполняет комнату.
            mask.Add(LightSource.Circle(cp + cam, Crystal.LightRadius, hot, 1.25f, 0.8f));

    // Мягкий подсвет по краю, чтобы комната не кончалась кругом.
   mask.Add(LightSource.Circle(cp + cam, Crystal.LightRadius * 1.5f, hot, 0.5f, 0.95f));
        }
    }

/// <summary>
    /// Освещена ли точка прямо сейчас. В дуэли и на аренах темноты нет,
    /// поэтому там ответ всегда "да".
    /// </summary>
    private bool IsLitFor(Vector2 world, Vector2 cam)
    {
        if (_mask is null || !DarknessEnabled(_gameRef!)) return true;
        return _mask.IsLit(world, cam);
    }

    /// <summary>Уровень темноты в мировой точке: 0 - тьма, 1 - полный свет.</summary>
    public float LightAt(Vector2 world)
    {
        if (_mask is null || !DarknessEnabled(_gameRef!)) return 1f;
        return _mask.LevelAt(world, MaskCam);
    }

    /// <summary>Освещена ли точка настолько, чтобы её было видно.</summary>
    public bool IsLit(Vector2 world, float threshold = 0.3f)
   => LightAt(world) >= threshold;

  private Game? _gameRef;

    /// <summary>
    /// Рисует содержимое маски в отдельный bitmap - используется
  /// только в тестах, чтобы проверить темноту без запуска окна.
    /// </summary>
    public System.Drawing.Bitmap? MaskBitmap => _mask?.Snapshot();

    private void DrawTargetLock(Graphics g, Game game)
    {
        if (!game.Settings.ShowTargetLock) return;
        Enemy? target = game.LockedTarget;
        if (target is null || target.Dead) return;

        Vector2 screen = target.Pos - game.Camera.Position - game.Camera.Offset;
        float r = target.Radius + 7f + MathF.Sin(game.Time * 6f) * 1.5f;
        Color c = Palette.Fade(Palette.Danger, 0.9f);
        float arm = 5f;
        float t = 1.5f;
        Palette.Fill(g, c, screen.X - r, screen.Y - r, arm, t);
        Palette.Fill(g, c, screen.X - r, screen.Y - r, t, arm);
        Palette.Fill(g, c, screen.X + r - arm, screen.Y - r, arm, t);
        Palette.Fill(g, c, screen.X + r - t, screen.Y - r, t, arm);
        Palette.Fill(g, c, screen.X - r, screen.Y + r - t, arm, t);
        Palette.Fill(g, c, screen.X - r, screen.Y + r - arm, t, arm);
        Palette.Fill(g, c, screen.X + r - arm, screen.Y + r - t, arm, t);
        Palette.Fill(g, c, screen.X + r - t, screen.Y + r - arm, t, arm);
    }

/// <summary>
    /// Свой прицел. Раньше это были две тонкие линии - на фоне пещеры
    /// он терялся и путался с системным курсором. Теперь это ретиска:
    /// четыре засечки с разрывом по центру, рамка и точка в центре, плюс
    /// дуга вокруг, которая сжимается при наведении на цель.
    ///
    /// Рисуется в мировом слое (640x360) и растягивается вместе с игрой,
    /// поэтому толщина линий кратна пикселю мира и не мылится.
    /// </summary>
    private void DrawCrosshair(Graphics g, Game game)
    {
        if (!game.Settings.OwnCrosshair || !game.Input.MouseInside) return;
        if (game.State != GameState.Playing && game.State != GameState.LevelUp) return;

        float x = MathF.Round(game.Input.Mouse.X) + 0.5f;
        float y = MathF.Round(game.Input.Mouse.Y) + 0.5f;
        Color tint = game.Player.ElementTint;
        Color c = Palette.Fade(tint, 0.9f);
        Color dim = Palette.Fade(tint, 0.35f);

   // Разрыв по центру, чтобы не затыкать точку прицеливания.
        const float gap = 3f;
        const float len = 5f;

   // Четыре засечки: ближняя часть яркая, дальняя - приглушённая.
        Palette.Fill(g, c, x - gap - len, y - 0.5f, len, 1f);
        Palette.Fill(g, dim, x + gap, y - 0.5f, len, 1f);
        Palette.Fill(g, c, x - 0.5f, y - gap - len, 1f, len);
        Palette.Fill(g, dim, x - 0.5f, y + gap, 1f, len);

   // Угловые скобки - они читаются как прицел даже на пёстром фоне.
        const float br = 2f;
  Palette.Fill(g, Palette.Fade(Color.White, 0.35f), x - gap - br, y - gap - br, 1f, br);
    Palette.Fill(g, Palette.Fade(Color.White, 0.35f), x - gap - br, y - gap, br, 1f);
        Palette.Fill(g, Palette.Fade(Color.White, 0.35f), x + gap, y - gap - br, 1f, br);
        Palette.Fill(g, Palette.Fade(Color.White, 0.35f), x + gap + br - 1f, y - gap, br, 1f);
        Palette.Fill(g, Palette.Fade(Color.White, 0.35f), x - gap - br, y + gap, 1f, br);
    Palette.Fill(g, Palette.Fade(Color.White, 0.35f), x - gap, y + gap + br - 1f, br, 1f);
        Palette.Fill(g, Palette.Fade(Color.White, 0.35f), x + gap, y + gap, 1f, br);
        Palette.Fill(g, Palette.Fade(Color.White, 0.35f), x + gap + br - 1f, y + gap + br - 1f, br, 1f);

   // Центральная точка - главный ориентир.
        Palette.Fill(g, Palette.Fade(Color.Black, 0.55f), x - 1f, y - 1f, 3f, 3f);
        Palette.Fill(g, Palette.Fade(Color.White, 0.95f), x, y, 1f, 1f);

   // Дуга вокруг: наведение на цель подсвечивается ярче.
        Enemy? lockTarget = game.Settings.ShowTargetLock ? game.LockedTarget : null;
        bool onTarget = lockTarget is not null && !lockTarget.Dead;
        float ring = onTarget ? 9f + MathF.Sin(game.Time * 7f) * 0.8f : 9f;
        Color rc = onTarget ? Palette.Fade(Palette.Danger, 0.95f) : Palette.Fade(tint, 0.5f);
        Palette.Stroke(g, rc, x - ring, y - ring, ring * 2f, ring * 2f);

   // Глиф стихии - какая руна сейчас выбрана.
  Text.DrawCentered(g, Elements.Glyph(game.Player.SelectedElement), Text.Tiny, Palette.Fade(c, 0.95f), x, y - 19f);
    }

    private void DrawScreenEffects(Graphics g, Game game)
    {
        Palette.DrawVignette(g, 0f, 0f, Width, Height, 0.9f);
        if (game.FlashAmount > 0.01f)
        {
            Palette.Fill(g, Palette.Fade(game.FlashColor, game.FlashAmount), 0f, 0f, Width, Height);
        }
        if (game.Player.Invuln > 0f && game.State == GameState.Playing)
        {
            // Инвил может быть любым (тесты ставят 30), поэтому альфу обязательно
            // ограничиваем - иначе красная заливка закрывает весь кадр.
            Palette.Fill(g, Palette.Fade(Palette.Health, 0.1f * MathF.Min(1f, game.Player.Invuln)), 0f, 0f, Width, Height);
        }
    }

    private readonly Minimap _minimap = new();

    /// <summary>Маска темноты и света. Пересчитывается раз в кадр.</summary>
    private LightMask? _mask;

    /// <summary>Текущая освещённость для ИИ и проверок видимости.</summary>
    public LightMask? Mask => _mask;

    /// <summary>Положение камеры на момент последнего расчёта маски.</summary>
    public Vector2 MaskCam { get; private set; }

private void DrawHud(Graphics g, Game game)
    {
    Player p = game.Player;

  // Нижняя панель: единый блок вместо россыпи полосок. Полупрозрачный фон
        // с тонкой верхней линией - он читается как часть интерфейса, а не
    // как наклейка поверх игры.
   Palette.Fill(g, Palette.Fade(Palette.Panel, 0.82f), 0f, HudTop, Width, Height - HudTop);
        Palette.Fill(g, Palette.Fade(Palette.PanelEdge, 0.5f), 0f, HudTop, Width, 1f);
    Palette.Fill(g, Palette.Fade(p.ElementTint, 0.25f), 0f, HudTop, Width, 1f);

        DrawVitals(g, p);
        DrawRuneBuffer(g, game);
        DrawRuneKeys(g, game);
        DrawMeters(g, p);
        DrawTopStats(g, game);

   // Миникарта нужна и в дуэли: без неё не видно, где соперник.
     if (game.Settings.Minimap && game.State != GameState.Title) _minimap.Draw(g, game);

  Enemy? boss = game.Boss;
        if (boss is not null)
        {
  const float bw = 320f;
      Bar(g, (Width - bw) * 0.5f, 10f, bw, 9f, boss.Hp / boss.MaxHp, Palette.Danger, GameMath.Rgb(40, 14, 18));
  Text.DrawCentered(g, boss.BossName, Text.Tiny, Palette.Fade(Palette.Danger, 0.95f), Width * 0.5f, 21f);
    }

        if (game.SpellTimer > 0f)
        {
   float a = GameMath.Clamp01(game.SpellTimer / 0.5f);
   string prefix = game.SpellFusion ? "РЕЗОНАНС: " : string.Empty;
    float y = 244f;
            float w = Text.Width(g, prefix + game.SpellAnnouncement, Text.SmallBold) + 14f;
   Palette.Fill(g, Palette.Fade(Color.Black, 0.5f * a), (Width - w) * 0.5f, y - 2f, w, 19f);
            Text.DrawCentered(g, prefix + game.SpellAnnouncement, Text.SmallBold, Palette.Fade(game.SpellColor, a), Width * 0.5f, y);
        }

        if (game.BannerTimer > 0f)
      {
     float a = GameMath.Clamp01(game.BannerTimer / 0.6f);
 Text.DrawCentered(g, game.Banner, Text.Large, Palette.Fade(Palette.Paper, a), Width * 0.5f, 130f);
     Text.DrawCentered(
    g,
       game.Level.IsBoss ? "не дай Стражу дотронуться до тебя" : "портал ждёт в глубине зала",
        Text.Small,
    Palette.Fade(Palette.Muted, a),
      Width * 0.5f,
    166f);
        }
    }

    /// <summary>
    /// Жизнь, мана и опыт слева внизу. Раньше это были три тонких полоски
    /// без иконок - теперь у каждой есть знак, и полоски стали толще.
    /// </summary>
    private static void DrawVitals(Graphics g, Player p)
    {
  const float barX = 22f;
        const float barW = 108f;

        // Значок сердца перед полосой жизни.
        Palette.Disc(g, Palette.Fade(Palette.Health, 0.9f), 10f, 326f, 4f);
        Palette.Fill(g, Palette.Fade(Palette.Health, 0.9f), 8f, 326f, 4f, 2f);

        Bar(g, barX, 321f, barW, 9f, p.Hp / p.MaxHp, Palette.Health, GameMath.Rgb(44, 18, 26));
        Text.Draw(g, $"{MathF.Ceiling(p.Hp)}/{MathF.Ceiling(p.MaxHp)}", Text.Tiny, Palette.Paper, 134f, 322f);

    // Значок молнии перед полосой маны.
    Palette.Fill(g, Palette.Fade(Palette.Mana, 0.9f), 9f, 336f, 3f, 5f);
        Palette.Fill(g, Palette.Fade(Palette.Mana, 0.9f), 11f, 335f, 2f, 2f);

        Bar(g, barX, 333f, barW, 7f, p.Mana / p.MaxMana, Palette.Mana, GameMath.Rgb(18, 28, 50));
      Text.Draw(g, $"{MathF.Ceiling(p.Mana)}/{MathF.Ceiling(p.MaxMana)}", Text.Tiny, Palette.Paper, 134f, 333f);

        Bar(g, barX, 343f, barW, 3f, p.Xp / p.XpToNextLevel, Palette.Xp, GameMath.Rgb(24, 34, 24));
        Text.Draw(g, $"УР. {p.Level}", Text.Tiny, Palette.Fade(Palette.Xp, 0.95f), 134f, 341f);
    }

    /// <summary>Поток и рывок справа внизу.</summary>
    private static void DrawMeters(Graphics g, Player p)
    {
        float dashRatio = p.DashCooldown <= 0f ? 1f : 1f - p.DashCooldownTimer / p.DashCooldown;

        Text.DrawRight(g, "ПОТОК", Text.Tiny, Palette.Fade(Palette.Flow, 0.9f), 626f, 321f);
     Bar(g, 490f, 332f, 132f, 6f, p.Flow, Palette.Flow, GameMath.Rgb(40, 34, 24));

  Text.DrawRight(g, "РЫВОК", Text.Tiny, Palette.Fade(Palette.ExitOpen, 0.85f), 626f, 343f);
    Bar(g, 490f, 354f, 132f, 4f, dashRatio, dashRatio >= 1f ? Palette.ExitOpen : GameMath.Rgb(120, 120, 160), GameMath.Rgb(26, 26, 36));
    }

    /// <summary>
    /// Верхняя статистика. Слева - состояние забега, справа - счёт.
    /// Всё полупрозрачное и мелкое: раньше панель спорила с игрой по весу.
 /// </summary>
    private void DrawTopStats(Graphics g, Game game)
    {
Player p = game.Player;

        Text.Draw(g, $"ГЛУБИНА {game.Depth}", Text.Tiny, Palette.Fade(Palette.Paper, 0.9f), 8f, 6f);
 Text.Draw(g, FormatTime(game.Elapsed), Text.Tiny, Palette.Fade(Palette.Muted, 0.8f), 8f, 19f);

     if (game.Settings.ShowFps)
        {
Text.Draw(g, $"{game.Fps:0} FPS", Text.Tiny, Palette.Fade(Palette.Xp, 0.75f), 8f, 32f);
        }

        if (game.Level.IsBoss && !game.Level.ExitOpen)
     {
            Text.Draw(g, "АРЕНА СТРАЖА", Text.Tiny, Palette.Fade(Palette.Danger, 0.9f), 8f, 32f);
        }
  else if (game.Level.ExitOpen)
    {
            Text.Draw(g, "ПОРТАЛ ОТКРЫТ", Text.Tiny, Palette.Fade(Palette.ExitOpen, 0.85f), 8f, 32f);
        }
        else
  {
         Text.Draw(g, $"ВРАГОВ: {game.EnemiesLeft}", Text.Tiny, Palette.Fade(Palette.Danger, 0.85f), 8f, 32f);
        }

        Text.Draw(g, $"РУНЫ {p.UnlockedCount}/6", Text.Tiny, Palette.Fade(Palette.Muted, 0.8f), 8f, 45f);

        Text.DrawRight(g, $"ОЧКИ {game.Score}", Text.TinyBold, Palette.Fade(Palette.Gold, 0.95f), 632f, 6f);
        Text.DrawRight(g, $"УБИЙСТВ {game.Kills}", Text.Tiny, Palette.Fade(Palette.Muted, 0.8f), 632f, 19f);
    }

    private void DrawRuneBuffer(Graphics g, Game game)
    {
        SpellBuffer buffer = game.Player.Buffer;
        const float slot = 16f;
        const float gap = 3f;
        int count = Math.Max(4, buffer.Sequence.Count);
        float totalW = count * slot + (count - 1) * gap;
        float x = (Width - totalW) * 0.5f;
        const float y = 324f;

        Text.Draw(g, "РЕЗОНАНС", Text.Tiny, Palette.Muted, 206f, 318f);
        Color resColor = buffer.LastElement.HasValue ? Elements.Color(buffer.LastElement.Value) : Palette.Muted;
        Bar(g, 206f, 330f, 70f, 4f, buffer.ResonanceRatio, Palette.Fade(resColor, 0.9f), GameMath.Rgb(28, 26, 40));

        for (int i = 0; i < count; i++)
        {
            float sx = x + i * (slot + gap);
            bool filled = i < buffer.Sequence.Count;
            Color col = filled ? Elements.Color(buffer.Sequence[i]) : GameMath.Rgb(38, 34, 54);
            Palette.Fill(g, Palette.Fade(col, filled ? 0.55f : 0.35f), sx, y, slot, slot);
            Palette.Stroke(g, Palette.Fade(col, filled ? 0.95f : 0.5f), sx, y, slot, slot);
            if (filled)
            {
                Text.DrawCentered(g, Elements.Glyph(buffer.Sequence[i]), Text.Tiny, Palette.Fade(Color.White, 0.95f), sx + slot * 0.5f, y + 2f);
            }
            else
            {
                Palette.Fill(g, Palette.Fade(Palette.Muted, 0.3f), sx + 6f, y + 6f, 4f, 4f);
            }
        }

        if (buffer.CommitLeft > 0f)
        {
            Palette.Fill(g, Palette.Fade(Palette.Paper, 0.25f), x, y + slot, totalW, 2f);
            Palette.Fill(g, Palette.Fade(Palette.Paper, 0.85f), x, y + slot, totalW * buffer.CommitRatio, 2f);
        }

        if (buffer.PendingSpell is not null && buffer.CommitLeft > 0f)
        {
            Text.DrawRight(g, buffer.PendingSpell.Name, Text.Tiny, Palette.Fade(Palette.Paper, 0.8f), 444f, 318f);
        }
    }

    private void DrawRuneKeys(Graphics g, Game game)
    {
        const float slot = 15f;
        const float gap = 3f;
        float totalW = 6 * slot + 5 * gap;
        float x = (Width - totalW) * 0.5f;
        const float y = 344f;

        for (int i = 0; i < 6; i++)
        {
            float sx = x + i * (slot + gap);
            bool unlocked = game.Player.RuneUnlocked[i];
            bool selected = game.Player.SelectedRune == i;
            Color col = Elements.Color(Runes.Order[i]);
            Palette.Fill(g, unlocked ? Palette.Fade(col, selected ? 0.55f : 0.28f) : GameMath.Rgb(28, 26, 38), sx, y, slot, slot);
            Palette.Stroke(g, selected ? Palette.Fade(Color.White, 0.95f) : unlocked ? Palette.Fade(col, 0.85f) : GameMath.Rgb(58, 54, 74), sx, y, slot, slot, selected ? 2f : 1f);
            if (unlocked)
            {
                Text.DrawCentered(g, (i + 1).ToString(), Text.Tiny, Palette.Fade(Color.White, 0.95f), sx + slot * 0.5f, y + 2f);
            }
            else
            {
                Palette.Fill(g, GameMath.Rgb(70, 66, 90), sx + 5f, y + 6f, 5f, 4f);
            }
        }
    }

    private void DrawOverlays(Graphics g, Game game)
    {
        // Пока открыт модальный диалог, меню под ним не рисуем.
        if (game.Confirm == ConfirmKind.None)
        {
            switch (game.State)
            {
                case GameState.LevelUp:
                    DrawLevelUp(g, game);
                    break;
                case GameState.Paused:
                    DrawDim(g, 0.85f);
                    DrawPauseMenu(g, game);
                    break;
                case GameState.Settings:
                    DrawDim(g, 0.88f);
                    DrawSettings(g, game);
                    break;
                case GameState.GameOver:
                    DrawDim(g, 0.8f);
                    DrawEndScreen(g, game, "МАГ ПАЛ", Palette.Health, "забег окончен");
                    break;
                case GameState.Victory:
                    DrawDim(g, 0.8f);
                    DrawEndScreen(g, game, "ЭФИР ПОКОРЁН", Palette.Gold, "подземелье пройдено");
                    break;
            }

// Дуэльный HUD рисуется в DrawHudOnly отдельной панелью, когда идёт бой.
    }

        DrawConfirm(g, game);
    }

private void DrawDuelHud(Graphics g, Game game)
    {
        DuelSession duel = game.Duel!;
        float top = HudTop;

   // Миникарта и в дуэли: без неё соперника не найти в пещере.
     // Раньше она рисовалась только в обычном HUD и в дуэли пропадала.
        if (game.Settings.Minimap) _minimap.Draw(g, game);

    Palette.Fill(g, Palette.Fade(Palette.Panel, 0.94f), 0f, top, Width, Height - top);
        Palette.Stroke(g, Palette.Fade(Palette.PanelEdge, 0.7f), 0f, top, Width, Height - top);

        float w = 168f;
        float h = 8f;
        float y = top + 16f;
        int mine = duel.LocalId;

        DrawDuelFighterBar(g, duel, duel.HostNick, duel.WinsHost, y, w, h, true);
        DrawDuelFighterBar(g, duel, duel.GuestNick, duel.WinsGuest, y, w, h, false);

        int myWins = mine == 0 ? duel.WinsHost : duel.WinsGuest;
        int theirWins = mine == 0 ? duel.WinsGuest : duel.WinsHost;
        Text.DrawCentered(g, myWins + " : " + theirWins, Text.SmallBold, Palette.Paper, Width * 0.5f, y - 2f);
        Text.DrawCentered(g, "РАУНД " + duel.RoundNumber + "   ДО " + DuelRules.WinsRequired + " ПОБЕД",
            Text.Tiny, Palette.Fade(Palette.Muted, 0.95f), Width * 0.5f, y + 12f);

        if (duel.Phase == DuelPhase.RoundOver)
        {
            bool won = duel.RoundWinner == mine;
            string text = duel.RoundWinner < 0 ? "НИЧЬЯ" : won ? "РАУНД ВАШ" : "РАУНД СОПЕРНИКА";
            DrawDuelBanner(g, text, won ? Palette.ExitOpen : Palette.Health, y + 30f);
        }
        else if (duel.Phase == DuelPhase.MatchOver)
        {
            bool won = duel.MatchWinner == mine;
            DrawDuelBanner(g, won ? "ПОБЕДА" : "ПОРАЖЕНИЕ", won ? Palette.Gold : Palette.Health, y + 30f);
            Text.DrawCentered(g, "ENTER - ПРОДОЛЖИТЬ", Text.Tiny, Palette.Muted, Width * 0.5f, y + 52f);
        }
    }

    private void DrawDuelBanner(Graphics g, string text, Color color, float y)
    {
        float w = Text.Width(g, text, Text.Medium) + 40f;
        float x = (Width - w) * 0.5f;
        Palette.Fill(g, Palette.Fade(Palette.Panel, 0.92f), x, y, w, 24f);
        Palette.Stroke(g, Palette.Fade(color, 0.9f), x, y, w, 24f);
        Text.DrawCentered(g, text, Text.Medium, Palette.Fade(color, 0.95f), Width * 0.5f, y + 3f);
    }

    private void DrawDuelFighterBar(Graphics g, DuelSession duel, string nick, int wins, float y, float w, float h, bool left)
    {
        float x = left ? 8f : Width - w - 8f;
        float ratio = 1f;
        byte wanted = left ? (byte)0 : (byte)1;

        foreach (Net.PeerSnapshot snapshot in duel.RenderPlayers)
        {
            if (snapshot.Id != wanted) continue;
            ratio = snapshot.MaxHp > 0f ? GameMath.Clamp01(snapshot.Hp / snapshot.MaxHp) : 0f;
        }

        if (left == (duel.LocalId == 0))
        {
            foreach (Net.PeerSnapshot snapshot in duel.RenderPlayers)
            {
                if (snapshot.Id != (byte)duel.LocalId) continue;
                ratio = snapshot.MaxHp > 0f ? GameMath.Clamp01(snapshot.Hp / snapshot.MaxHp) : 0f;
            }
        }

        Bar(g, x, y, w, h, ratio, Palette.Health, Palette.Fade(Color.Black, 0.5f));

        if (left) Text.Draw(g, nick, Text.Tiny, Palette.Paper, x, y - 11f);
        else Text.DrawRight(g, nick, Text.Tiny, Palette.Paper, x + w, y - 11f);

        for (int i = 0; i < DuelRules.WinsRequired; i++)
        {
            float px = left ? x + w - 8f - i * 8f : x + i * 8f;
            Color c = i < wins ? Palette.Gold : Palette.Fade(Palette.PanelEdge, 0.7f);
            Palette.Fill(g, c, px, y + h + 3f, 6f, 4f);
        }
    }

    private void DrawPauseMenu(Graphics g, Game game)
    {
        Text.DrawCentered(g, "ПАУЗА", Text.Large, Palette.Paper, Width * 0.5f, 74f);
        string[] items = game.DuelMode
            ? new[] { "ПРОДОЛЖИТЬ", "НАСТРОЙКИ", "В ГЛАВНОЕ МЕНЮ" }
            : new[] { "ПРОДОЛЖИТЬ", "НАСТРОЙКИ", "НОВЫЙ ЗАБЕГ", "В ГЛАВНОЕ МЕНЮ" };
        for (int i = 0; i < items.Length; i++)
        {
            float y = 128f + i * 26f;
            bool selected = game.PauseSelection == i;
            bool danger = i == items.Length - 1;
            if (selected)
            {
                float w = Text.Width(g, items[i], Text.SmallBold) + 28f;
                Palette.Fill(g, Palette.Fade(Palette.PanelEdge, 0.35f), (Width - w) * 0.5f, y - 2f, w, 20f);
                Palette.Fill(g, Palette.Fade(danger ? Palette.Danger : Palette.ExitOpen, 0.9f), (Width - w) * 0.5f - 12f, y + 7f, 6f, 3f);
            }
            Color color = selected ? Palette.Paper : danger ? Palette.Fade(Palette.Danger, 0.8f) : Palette.Muted;
            Text.DrawCentered(g, items[i], selected ? Text.SmallBold : Text.Small, color, Width * 0.5f, y);
        }
        Text.DrawCentered(g, "ENTER - ВЫБРАТЬ    ESC - В ИГРУ    R - НОВЫЙ ЗАБЕГ", Text.Tiny, Palette.Muted, Width * 0.5f, 248f);
    }

    /// <summary>Модальный диалог подтверждения выхода из забега.</summary>
    private void DrawConfirm(Graphics g, Game game)
    {
        if (game.Confirm != ConfirmKind.ExitToMenu) return;

        DrawDim(g, 0.7f);

        const float w = 300f;
        const float h = 78f;
        float x = (Width - w) * 0.5f;
        float y = 120f;

        Palette.Fill(g, Palette.Fade(Palette.Panel, 0.97f), x, y, w, h);
        Palette.Stroke(g, Palette.Fade(Palette.Danger, 0.85f), x, y, w, h);

        Text.DrawCentered(g, "ПОКИНУТЬ ЗАБЕГ?", Text.SmallBold, Palette.Paper, Width * 0.5f, y + 12f);
        Text.DrawCentered(g, "Прогресс не сохраняется", Text.Tiny, Palette.Fade(Palette.Danger, 0.95f), Width * 0.5f, y + 30f);

        string[] answers = { "ДА, ВЫЙТИ", "ОТМЕНА" };
        for (int i = 0; i < answers.Length; i++)
        {
            float aw = 108f;
            float ax = Width * 0.5f + (i == 0 ? -aw - 6f : 6f);
            float ay = y + 48f;
            bool selected = game.ConfirmRow == i;
            if (selected)
            {
                Palette.Fill(g, Palette.Fade(i == 0 ? Palette.Danger : Palette.PanelEdge, 0.45f), ax, ay, aw, 18f);
            }
            Text.DrawCentered(g, answers[i], selected ? Text.TinyBold : Text.Tiny,
                selected ? Palette.Paper : Palette.Muted, ax + aw * 0.5f, ay + 4f);
        }

        Text.DrawCentered(g, "UP/DOWN - ВЫБОР    ENTER - ПОДТВЕРДИТЬ    ESC - ОТМЕНА", Text.Tiny, Palette.Fade(Palette.Muted, 0.85f), Width * 0.5f, y + h + 14f);
    }

    private void DrawSettings(Graphics g, Game game)
    {
        Text.DrawCentered(g, "НАСТРОЙКИ", Text.Large, Palette.Paper, Width * 0.5f, 18f);

        if (game.SettingsPage == 0) DrawSettingsMain(g, game);
        else DrawSettingsBindings(g, game);

        if (game.RebindingAction >= 0)
        {
            DrawDim(g, 0.6f);
            string label = BindingName((InputAction)game.RebindingAction);
            Palette.Fill(g, Palette.Fade(Palette.Panel, 0.95f), 140f, 140f, Width - 280f, 74f);
            Palette.Stroke(g, Palette.Fade(Palette.ExitOpen, 0.95f), 140f, 140f, Width - 280f, 74f, 2f);
            Text.DrawCentered(g, "НАЖМИТЕ КЛАВИШУ ИЛИ КНОПКУ МЫШИ", Text.Small, Palette.Paper, Width * 0.5f, 156f);
            Text.DrawCentered(g, label, Text.Medium, Palette.Fade(Palette.ExitOpen, 0.95f), Width * 0.5f, 180f);
            Text.DrawCentered(g, "ESC - ОТМЕНА", Text.Tiny, Palette.Muted, Width * 0.5f, 232f);
            return;
        }

        Text.DrawCentered(g, "UP/DOWN - ВЫБОР    LEFT/RIGHT - ИЗМЕНИТЬ    ENTER - ПРИМЕНИТЬ    ESC - НАЗАД", Text.Tiny, Palette.Muted, Width * 0.5f, 310f);
    }

    private void DrawSettingsMain(Graphics g, Game game)
    {
        const float x = 60f;
        const float panelTop = 52f;
        const float panelBottom = 288f;
        const float y0 = 57f;
        int rows = SettingsMenu.Labels.Length;
        float panelH = panelBottom - panelTop;
        float rowH = MathF.Min(17f, (panelH - 10f) / rows);

        Palette.Fill(g, Palette.Fade(Palette.Panel, 0.7f), 40f, panelTop, Width - 80f, panelH);
        Palette.Stroke(g, Palette.Fade(Palette.PanelEdge, 0.7f), 40f, panelTop, Width - 80f, panelH);

        for (int i = 0; i < rows; i++)
        {
            float y = y0 + i * rowH;
            bool selected = game.SettingsRow == i;
            if (selected)
            {
                Palette.Fill(g, Palette.Fade(Palette.PanelEdge, 0.4f), 44f, y - 2f, Width - 88f, rowH);
                Palette.Fill(g, Palette.Fade(Palette.ExitOpen, 0.95f), 46f, y + 4f, 4f, 8f);
            }
            Text.Draw(g, SettingsMenu.Labels[i], selected ? Text.SmallBold : Text.Small, selected ? Palette.Paper : Palette.Muted, x, y);
            string value = SettingsMenu.Value(game.Settings, (SettingsMenu.Row)i);
            if (value.Length > 0)
            {
                Text.DrawRight(g, value, Text.SmallBold, selected ? Palette.Fade(Palette.ExitOpen, 0.95f) : Palette.Muted, Width - 60f, y);
            }
        }
    }

    private static string BindingName(InputAction action)
    {
        foreach ((InputAction a, string label) in SettingsMenu.BindingRows)
        {
            if (a == action) return label;
        }
        foreach ((InputAction a, string label) in SettingsMenu.BindingRows2)
        {
            if (a == action) return label;
        }
        return action.ToString();
    }

    private void DrawSettingsBindings(Graphics g, Game game)
    {
        Text.DrawCentered(g, "КЛАВИШИ И МЫШЬ", Text.Medium, Palette.Paper, Width * 0.5f, 60f);
        Text.DrawCentered(g, "ESC - НАЗАД", Text.Tiny, Palette.Muted, Width * 0.5f, 84f);

        int total = SettingsMenu.BindingRows.Length + SettingsMenu.BindingRows2.Length;
        const float rowH = 19f;
        const float colY = 98f;
        const float col1X = 70f;
        const float col2X = 360f;

        for (int i = 0; i < total; i++)
        {
            bool second = i >= SettingsMenu.BindingRows.Length;
            int index = second ? i - SettingsMenu.BindingRows.Length : i;
            (InputAction action, string label) = second
                ? SettingsMenu.BindingRows2[index]
                : SettingsMenu.BindingRows[index];

            float x = second ? col2X : col1X;
            float y = colY + (second ? index : i) * rowH;
            bool selected = game.SettingsRow == i;
            if (selected)
            {
                Palette.Fill(g, Palette.Fade(Palette.PanelEdge, 0.4f), x - 8f, y - 2f, 250f, rowH);
                Palette.Fill(g, Palette.Fade(Palette.ExitOpen, 0.95f), x - 6f, y + 4f, 4f, 8f);
            }
            Text.Draw(g, label, selected ? Text.SmallBold : Text.Small, selected ? Palette.Paper : Palette.Muted, x, y);
            Text.DrawRight(g, game.Settings.BindingLabel(action), Text.SmallBold, selected ? Palette.Fade(Palette.ExitOpen, 0.95f) : Palette.Muted, x + 236f, y);
        }
    }

    private void DrawEndScreen(Graphics g, Game game, string title, Color color, string subtitle)
    {
        Text.DrawCentered(g, title, Text.Large, color, Width * 0.5f, 74f);
        Text.DrawCentered(g, subtitle, Text.Small, Palette.Muted, Width * 0.5f, 108f);
        const float x = 150f;
        Stat(g, "ГЛУБИНА", $"{game.Depth}", x, 148f);
        Stat(g, "УБИЙСТВ", $"{game.Kills}", x, 170f);
        Stat(g, "ОЧКИ", $"{game.Score}", x, 192f);
        Stat(g, "УРОВЕНЬ", $"{game.Player.Level}", x + 180f, 148f);
        Stat(g, "ВРЕМЯ", FormatTime(game.Elapsed), x + 180f, 170f);
        Stat(g, "РУНЫ", $"{game.Player.UnlockedCount}/6", x + 180f, 192f);
        float blink = 0.6f + 0.4f * MathF.Sin(game.Time * 4f);
        Text.DrawCentered(g, "R - НОВЫЙ ЗАБЕГ    ENTER - В МЕНЮ", Text.Small, Palette.Fade(Palette.Paper, blink), Width * 0.5f, 244f);
    }

    private static void Stat(Graphics g, string label, string value, float x, float y)
    {
        Text.Draw(g, label, Text.Tiny, Palette.Muted, x, y + 2f);
        Text.Draw(g, value, Text.SmallBold, Palette.Paper, x + 96f, y);
    }

    private void DrawLevelUp(Graphics g, Game game)
    {
        DrawDim(g, 0.72f);
        Text.DrawCentered(g, $"УРОВЕНЬ {game.Player.Level}", Text.Medium, Palette.Paper, Width * 0.5f, 46f);
        Text.DrawCentered(g, "выберите дар", Text.Small, Palette.Muted, Width * 0.5f, 70f);

        for (int i = 0; i < game.Choices.Count; i++)
        {
            CardDef card = game.Choices[i];
            RectangleF r = Game.CardRect(i);
            bool selected = game.ChoiceIndex == i;
            bool hovered = game.Input.MouseInside && r.Contains(game.Input.Mouse);
            if (selected || hovered) r = RectangleF.Inflate(r, 2f, 2f);

            Palette.Fill(g, Palette.Fade(Color.FromArgb(28, 24, 44), 0.98f), r.X, r.Y, r.Width, r.Height);
            Palette.Stroke(g, Palette.Fade(selected || hovered ? card.Color : Palette.PanelEdge, 0.95f), r.X, r.Y, r.Width, r.Height, selected || hovered ? 2f : 1f);
            Palette.Fill(g, Palette.Fade(card.Color, 0.16f), r.X + 1f, r.Y + 1f, r.Width - 2f, 22f);
            Text.DrawCentered(g, card.Rarity.ToUpperInvariant(), Text.Tiny, Palette.Fade(card.Color, 0.95f), r.X + r.Width * 0.5f, r.Y + 6f);
            Text.DrawCentered(g, card.Name, Text.SmallBold, Palette.Paper, r.X + r.Width * 0.5f, r.Y + 30f);

            List<string> lines = Wrap(g, card.Desc, Text.Tiny, r.Width - 16f);
            float ly = r.Y + 58f;
            foreach (string line in lines)
            {
                Text.DrawCentered(g, line, Text.Tiny, Palette.Muted, r.X + r.Width * 0.5f, ly);
                ly += 14f;
            }

            Palette.Fill(g, Palette.Fade(Palette.Panel, 0.9f), r.X + 1f, r.Bottom - 20f, r.Width - 2f, 19f);
            Text.DrawCentered(g, $"[{i + 1}]", Text.SmallBold, Palette.Fade(card.Color, 0.95f), r.X + r.Width * 0.5f, r.Bottom - 18f);
        }
    }

    private static void DrawDim(Graphics g, float alpha)
    {
        Palette.Fill(g, Palette.Fade(Color.FromArgb(6, 5, 12), alpha), 0f, 0f, Width, Height);
    }

    private void DrawTitle(Graphics g, Game game)
    {
        Palette.Fill(g, Palette.Void, 0f, 0f, Width, Height);

        for (int i = 0; i < 18; i++)
        {
            Element element = Runes.Order[i % Runes.Order.Length];
            Color c = Elements.Color(element);
            float a = 0.22f + 0.22f * MathF.Sin(game.Time * 1.6f + i);
            float sway = MathF.Sin(game.Time * 0.8f + i * 0.7f) * 2.5f;
            float left = i < 9 ? 26f : Width - 26f;
            float y = 128f + (i % 9) * 14f + sway;
            Palette.AddGlow(g, new Vector2(left, y), 10f, c, a * 0.8f);
            Text.DrawCentered(g, Elements.Glyph(element), Text.Small, Palette.Fade(c, a + 0.35f), left, y - 6f);
        }

        Text.DrawCentered(g, "AETHER SEQUENCE", Text.Title, Palette.Fade(Palette.Paper, 0.97f), Width * 0.5f, 26f);
        Text.DrawCentered(g, "ЭФИРНЫЙ ЗАБЕГ МАГА", Text.Small, Palette.Fade(Palette.Muted, 0.95f), Width * 0.5f, 96f);

        const float panelX = 70f;
        const float panelW = Width - 140f;
        Palette.Fill(g, Palette.Fade(Palette.Panel, 0.75f), panelX, 128f, panelW, 96f);
        Palette.Stroke(g, Palette.Fade(Palette.PanelEdge, 0.8f), panelX, 128f, panelW, 96f);
        Text.DrawCentered(g, "WASD - ХОДЬБА      МЫШЬ - ПРИЦЕЛ      ЛКМ - КАСТ", Text.Tiny, Palette.Paper, Width * 0.5f, 138f);
        Text.DrawCentered(g, "ПКМ - РЫВОК      КОЛЕСО - СМЕНА РУНЫ      1-6 - РУНЫ СТИХИЙ", Text.Tiny, Palette.Paper, Width * 0.5f, 154f);
    Text.DrawCentered(g, "F (держать) - ЗАЖЕЧЬ КРИСТАЛЛ ОСВЕЩЕНИЯ", Text.Tiny, Palette.Fade(Palette.ExitOpen, 0.9f), Width * 0.5f, 170f);
        Text.DrawCentered(g, "Смешивайте стихии подряд - сработает РЕЗОНАНС", Text.Tiny, Palette.Fade(Palette.Flow, 0.95f), Width * 0.5f, 176f);
        Text.DrawCentered(g, "15 глубин. Смерть - это начало.", Text.Tiny, Palette.Muted, Width * 0.5f, 198f);

        if (game.State == GameState.Duel)
        {
            Palette.Fill(g, Palette.Fade(Palette.Void, 0.72f), 0f, 230f, Width, Height - 230f);
            DrawDuelMenu(g, game);
            Palette.DrawVignette(g, 0f, 0f, Width, Height, 0.9f);
            return;
        }

        for (int i = 0; i < TitleMenu.Count; i++)
        {
            float y = Game.TitleItemY(i);
            bool selected = game.TitleRow == i;
            bool danger = (TitleMenu.Row)i == TitleMenu.Row.Quit;
            if (selected)
            {
                float w = Text.Width(g, TitleMenu.Labels[i], Text.SmallBold) + 28f;
                Palette.Fill(g, Palette.Fade(Palette.PanelEdge, 0.35f), (Width - w) * 0.5f, y - 2f, w, 20f);
                Palette.Fill(g, Palette.Fade(danger ? Palette.Danger : Palette.ExitOpen, 0.9f), (Width - w) * 0.5f - 12f, y + 7f, 6f, 3f);
            }
            Color color = selected ? Palette.Paper : danger ? Palette.Fade(Palette.Danger, 0.8f) : Palette.Muted;
            Text.DrawCentered(g, TitleMenu.Labels[i], selected ? Text.SmallBold : Text.Small, color, Width * 0.5f, y);
        }

        DrawBestRecord(g, game);

        Text.DrawCentered(g, "UP/DOWN - ВЫБОР      ENTER - ПОДТВЕРДИТЬ", Text.Tiny, Palette.Fade(Palette.Muted, 0.85f), Width * 0.5f, 350f);
        Palette.DrawVignette(g, 0f, 0f, Width, Height, 0.9f);
    }

    /// <summary>Строка рекордов под меню (скрыта, пока рекордов нет).</summary>
    private static void DrawBestRecord(Graphics g, Game game)
    {
        Settings s = game.Settings;
        if (s.BestDepth <= 0 && s.BestScore <= 0) return;

        string line = s.BestDepth > 0 ? $"РЕКОРД · ГЛУБИНА {s.BestDepth}" : "РЕКОРД";
        if (s.BestScore > 0) line += $" · {s.BestScore} ОЧКОВ";
        Text.DrawCentered(g, line, Text.Tiny, Palette.Fade(Palette.Gold, 0.8f), Width * 0.5f, 234f);
    }

    private void DrawDuelMenu(Graphics g, Game game)
    {
        DuelSession? duel = game.Duel;
        if (duel is null) return;

        const float baseY = 248f;
        Text.DrawCentered(g, "ДУЭЛЬ", Text.Medium, Palette.Fade(Palette.ExitOpen, 0.95f), Width * 0.5f, baseY);
        Text.DrawCentered(g, "Двое в одной пещере. Три победы до конца.", Text.Tiny, Palette.Muted, Width * 0.5f, baseY + 15f);

        switch (duel.Phase)
        {
            case DuelPhase.Nickname:
                DrawNickname(g, duel, baseY + 30f);
                break;
            case DuelPhase.DirectIp:
                DrawDirectIp(g, duel, baseY + 30f);
                break;
            case DuelPhase.Main:
                DrawDuelMain(g, duel, baseY + 30f);
                break;
            case DuelPhase.Hosting:
            case DuelPhase.InRoom:
                DrawDuelRoom(g, duel, baseY + 30f);
                break;
            case DuelPhase.Browsing:
                DrawRoomList(g, duel, baseY + 30f);
                break;
            case DuelPhase.Loading:
                Text.DrawCentered(g, "ПОДКЛЮЧЕНИЕ К КОМНАТЕ...", Text.Small, Palette.Flow, Width * 0.5f, baseY + 60f);
                break;
        }

        if (duel.Status.Length > 0)
        {
            Text.DrawCentered(g, duel.Status, Text.Tiny, Palette.Fade(Palette.Gold, 0.95f), Width * 0.5f, 344f);
        }
        else
        {
            Text.DrawCentered(g, "ESC - НАЗАД", Text.Tiny, Palette.Fade(Palette.Muted, 0.8f), Width * 0.5f, 344f);
        }
    }

    private void DrawDuelSlot(Graphics g, DuelSession duel, float y)
    {
        switch (duel.Phase)
        {
            case DuelPhase.Nickname: DrawNickname(g, duel, y); break;
            case DuelPhase.Main: DrawDuelMain(g, duel, y); break;
            case DuelPhase.Hosting:
            case DuelPhase.InRoom: DrawDuelRoom(g, duel, y); break;
            case DuelPhase.Browsing: DrawRoomList(g, duel, y); break;
        }
    }

    private void DrawNickname(Graphics g, DuelSession duel, float y)
    {
        const float boxX = 150f;
        const float boxW = Width - 300f;
        Text.DrawCentered(g, "ВВЕДИТЕ НИКНЕЙМ", Text.Small, Palette.Paper, Width * 0.5f, y);
        Palette.Fill(g, Palette.Fade(Palette.Panel, 0.9f), boxX, y + 14f, boxW, 20f);
        Palette.Stroke(g, Palette.Fade(Palette.PanelEdge, 0.9f), boxX, y + 14f, boxW, 20f);
        string draft = duel.NickDraft.Length > 0 ? duel.NickDraft : "...";
        Text.Draw(g, draft, Text.Small, Palette.Fade(Palette.ExitOpen, 0.95f), boxX + 8f, y + 17f);
        float caretX = boxX + 8f + Text.Width(g, draft, Text.Small) + 1f;
        if (MathF.Sin(duel.Caret) > 0f) Palette.Fill(g, Palette.Paper, caretX, y + 17f, 5f, 11f);
    }

    /// <summary>Экран ручного подключения: адрес хоста и необязательный порт.</summary>
    private void DrawDirectIp(Graphics g, DuelSession duel, float y)
    {
        const float boxX = 150f;
        const float boxW = Width - 300f;

        Text.DrawCentered(g, "ПОДКЛЮЧИТЬСЯ ПО АДРЕСУ", Text.Small, Palette.Paper, Width * 0.5f, y);
        Text.DrawCentered(g, "если комнаты не находятся - впиши адрес хоста", Text.Tiny, Palette.Muted, Width * 0.5f, y + 12f);

        Palette.Fill(g, Palette.Fade(Palette.Panel, 0.9f), boxX, y + 26f, boxW, 20f);
        Palette.Stroke(g, Palette.Fade(Palette.PanelEdge, 0.9f), boxX, y + 26f, boxW, 20f);
        string draft = duel.IpDraft.Length > 0 ? duel.IpDraft : "192.168.1.5";
        Text.Draw(g, draft, Text.Small, Palette.Fade(Palette.ExitOpen, 0.95f), boxX + 8f, y + 29f);
        float caretX = boxX + 8f + Text.Width(g, duel.IpDraft, Text.Small) + 1f;
        if (MathF.Sin(duel.Caret) > 0f) Palette.Fill(g, Palette.Paper, caretX, y + 29f, 5f, 11f);

        Text.DrawCentered(g, "Enter - подключиться,  Back - стереть,  ESC - назад", Text.Tiny, Palette.Muted, Width * 0.5f, y + 52f);

        if (duel.IpStatus.Length > 0)
        {
            Text.DrawCentered(g, duel.IpStatus, Text.Tiny, Palette.Fade(Palette.Gold, 0.95f), Width * 0.5f, y + 66f);
        }
    }

    private void DrawDuelMain(Graphics g, DuelSession duel, float y)
    {        const float boxX = 190f;
        const float boxW = Width - 380f;
        string label = "НИК: " + duel.Nick;
        Palette.Fill(g, Palette.Fade(Palette.Panel, 0.9f), boxX, y, boxW, 17f);
        Text.DrawCentered(g, label, Text.Tiny, Palette.Paper, Width * 0.5f, y + 3f);

        string[] items = { "СОЗДАТЬ КОМНАТУ", "НАЙТИ КОМНАТЫ", "НИКНЕЙМ", "ПОДКЛЮЧИТЬСЯ ПО IP", "ТРЕНИРОВКА С БОТОМ" };
        for (int i = 0; i < items.Length; i++)
        {
            float iy = y + 20f + i * 16f;
            bool selected = duel.Row == i;
            if (selected)
            {
                float w = Text.Width(g, items[i], Text.Tiny) + 20f;
                Palette.Fill(g, Palette.Fade(Palette.PanelEdge, 0.4f), (Width - w) * 0.5f, iy - 1f, w, 14f);
            }
            Text.DrawCentered(g, items[i], selected ? Text.TinyBold : Text.Tiny,
                selected ? Palette.Paper : Palette.Muted, Width * 0.5f, iy);
        }
    }

    private void DrawDuelRoom(Graphics g, DuelSession duel, float y)
    {
        const float boxX = 90f;
        const float boxW = Width - 180f;
        bool ready = duel.GuestNick.Length > 0;
        float boxH = ready ? 78f : 58f;
        Palette.Fill(g, Palette.Fade(Palette.Panel, 0.9f), boxX, y, boxW, boxH);
        Palette.Stroke(g, Palette.Fade(Palette.PanelEdge, 0.85f), boxX, y, boxW, boxH);

        Text.DrawCentered(g, "КОМНАТА: " + duel.RoomName, Text.Tiny, Palette.Fade(Palette.Gold, 0.95f), Width * 0.5f, y + 4f);

        Text.DrawCentered(g, ready ? "СОПЕРНИК ПОДКЛЮЧЁН" : "ОЖИДАНИЕ СОПЕРНИКА...",
            Text.Tiny, ready ? Palette.Fade(Palette.ExitOpen, 0.95f) : Palette.Muted, Width * 0.5f, y + 18f);

        Text.DrawCentered(g, duel.HostNick + (ready ? "   vs   " + duel.GuestNick : "   (вы)"),
            Text.Tiny, Palette.Paper, Width * 0.5f, y + 32f);

        // Пока гость не пришёл, показываем адрес хоста: если поиск комнат
        // не работает, гость подключится по нему вручную.
        if (!ready)
        {
            Text.DrawCentered(g, "адрес для ручного подключения: " + LocalAddresses(),
                Text.Tiny, Palette.Fade(Palette.Flow, 0.95f), Width * 0.5f, y + 44f);
        }

        if (ready)
        {
            bool selected = duel.Row == 0;
            const string start = "СТАРТ";
            float sy = y + 54f;
            if (selected)
            {
                float w = Text.Width(g, start, Text.Tiny) + 24f;
                Palette.Fill(g, Palette.Fade(Palette.PanelEdge, 0.4f), (Width - w) * 0.5f, sy, w, 15f);
            }
            Text.DrawCentered(g, start, selected ? Text.TinyBold : Text.Tiny,
                selected ? Palette.Paper : Palette.Muted, Width * 0.5f, sy + 1f);
        }
    }

    /// <summary>
    /// Адреса этого компьютера в локальной сети - их гость вводит вручную,
    /// если поиск комнат заблокирован брандмауэром.
    /// </summary>
    public static string LocalAddresses()
    {
        List<string> found = new();
        try
        {
            foreach (System.Net.NetworkInformation.NetworkInterface nic in
                     System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                foreach (System.Net.NetworkInformation.UnicastIPAddressInformation unicast in nic.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;

                    // Виртуальные адаптеры (VPN, Docker, WSL) другу не нужны:
                    // вводить такой адрес бесполезно - туда игра не запущена.
                    if (nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Ethernet
                        && nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
                    {
                        continue;
                    }

                    byte[] bytes = unicast.Address.GetAddressBytes();
                    if (bytes[0] == 127 || bytes[0] == 169) continue;
                    found.Add(unicast.Address.ToString());
                }
            }
        }
        catch (System.Net.NetworkInformation.NetworkInformationException) { }

        return found.Count > 0 ? string.Join("  или  ", found) : "не найден (проверь сеть)";
    }

    /// <summary>Как открыть порты игры в брандмауэре Windows.</summary>
    public const string FirewallHint =
        "Если комнаты не находятся: брандмауэр или Wi-Fi может блокировать поиск.\n" +
        "Пуск -> 'Брандмауэр Windows' -> 'Разрешить приложение' -> Отметить 'Частные'.\n" +
        "Или в дуэли выбери 'ПОДКЛЮЧИТЬСЯ ПО IP' и впиши адрес хоста вручную.";

    private void DrawRoomList(Graphics g, DuelSession duel, float y)
    {
        const float boxX = 90f;
        const float boxW = Width - 180f;
        float boxH = 30f + Math.Max(1, duel.VisibleRooms.Count) * 15f;
        Palette.Fill(g, Palette.Fade(Palette.Panel, 0.9f), boxX, y, boxW, boxH);
        Palette.Stroke(g, Palette.Fade(Palette.PanelEdge, 0.85f), boxX, y, boxW, boxH);

        Text.DrawCentered(g, "КОМНАТЫ В СЕТИ", Text.Tiny, Palette.Fade(Palette.Gold, 0.95f), Width * 0.5f, y + 4f);

        if (duel.VisibleRooms.Count == 0)
        {
            Text.DrawCentered(g, "поиск...", Text.Tiny, Palette.Muted, Width * 0.5f, y + 20f);
            // Пустой список - самая частая беда: подсказываем, что делать.
            Text.DrawCentered(g, "не находишь комнату?  ESC -> 'ПОДКЛЮЧИТЬСЯ ПО IP'", Text.Tiny, Palette.Fade(Palette.Gold, 0.9f), Width * 0.5f, y + 36f);
            Text.DrawCentered(g, "или разреши игру в брандмауэре Windows (частные сети)", Text.Tiny, Palette.Muted, Width * 0.5f, y + 46f);
        }
        else
        {
            for (int i = 0; i < duel.VisibleRooms.Count && i < 6; i++)
            {
                Net.RoomListing room = duel.VisibleRooms[i];
                bool selected = duel.RoomRow == i;
                float ry = y + 20f + i * 15f;
                if (selected)
                {
                    Palette.Fill(g, Palette.Fade(Palette.PanelEdge, 0.35f), boxX + 4f, ry - 1f, boxW - 8f, 14f);
                }
                string line = room.Name + "   хост: " + room.HostNick + "   " + room.Players + "/" + room.MaxPlayers;
                Text.DrawCentered(g, line, selected ? Text.TinyBold : Text.Tiny,
                    selected ? Palette.Paper : Palette.Muted, Width * 0.5f, ry);
            }
        }
    }

    public static void Bar(Graphics g, float x, float y, float w, float h, float ratio, Color color, Color back)
    {
        ratio = GameMath.Clamp01(ratio);
        Palette.Fill(g, back, x, y, w, h);
        Palette.Fill(g, Palette.Fade(color, 0.95f), x + 1f, y + 1f, (w - 2f) * ratio, h - 2f);
        Palette.Fill(g, Palette.Fade(Color.White, 0.25f), x + 1f, y + 1f, (w - 2f) * ratio, 1f);
        Palette.Stroke(g, Palette.Fade(Color.Black, 0.5f), x, y, w, h);
    }

    public static List<string> Wrap(Graphics g, string text, Font font, float maxWidth)
    {
        List<string> lines = new();
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string current = string.Empty;
        foreach (string word in words)
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (Text.Width(g, candidate, font) > maxWidth && current.Length > 0)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }
        if (current.Length > 0) lines.Add(current);
        return lines;
    }

    public static string FormatTime(float seconds)
    {
        int total = (int)seconds;
        return $"{(total / 60):00}:{total % 60:00}";
    }
}
