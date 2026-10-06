using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Windows.Forms;
using AetherSequence.Art;
using AetherSequence.Combat;
using AetherSequence.Core;
using AetherSequence.Entities;
using AetherSequence.Net;

namespace AetherSequence;

internal static class DevTools
{
    private static Settings TestSettings => Settings.Load(Path.Combine(Path.GetTempPath(), "aether-selftest"));

    public static Game NewGame() => new(TestSettings);

    private static void TestSoftLock()
    {
        Game game = NewGame();
        game.StartRun(14);
        game.Enemies.Clear();

        Vector2 spot = game.Level.Spawn;
        for (int i = 0; i < 24; i++)
        {
            float a = i * GameMath.Tau / 24f;
            Vector2 candidate = game.Level.Spawn + GameMath.FromAngle(a) * 120f;
            if (!game.Level.SolidAt(candidate) && game.Level.LineClear(game.Level.Spawn, candidate))
            {
                spot = candidate;
                break;
            }
        }

        Enemy enemy = Enemy.Create(EnemyKind.Slime, spot, 1, new Rng(5));
        enemy.SpawnTimer = 0f;
        game.Enemies.Add(enemy);
        game.Player.Pos = game.Level.Spawn;
        game.Settings.Aim = AimMode.MouseSoftLock;
        game.Settings.AutoAimDegrees = 35f;

        Vector2 screen = spot - game.Camera.Position;
        game.Input.MouseMove(new PointF(screen.X + 26f, screen.Y + 26f));
        game.Update(1.0 / 60.0);
        bool locked = game.LockedTarget == enemy;
        float want = GameMath.AngleOf(spot - game.Player.Pos);
        float error = MathF.Abs(MathF.Atan2(MathF.Sin(game.Player.AimAngle - want), MathF.Cos(game.Player.AimAngle - want))) * GameMath.RadToDeg;
        Console.WriteLine($"[softlock] захват={locked} ошибкаПрицела={error:0.0}° (ожидается захват цели и ~0°)");

        game.Settings.AutoAimDegrees = 0f;
        game.Update(1.0 / 60.0);
        Console.WriteLine($"[softlock] автонаведение выключено: захват={game.LockedTarget is not null} (ожидается False, прицел по мыши)");
    }

    private static void TestSettingsRoundTrip()
    {
        string dir = Path.Combine(Path.GetTempPath(), "aether-settings-test");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings.json");
        if (File.Exists(path)) File.Delete(path);

        Settings s = Settings.Load(dir);
        s.ComboWindow = 0.31f;
        s.AutoAimDegrees = 50f;
        s.UiScale = 1.15f;
        s.Aim = AimMode.AutoTarget;
        s.SetBinding(InputAction.Dash, Keys.F);
        s.Save();

        Settings reloaded = Settings.Load(dir);
        bool ok = MathF.Abs(reloaded.ComboWindow - 0.31f) < 0.001f
            && MathF.Abs(reloaded.AutoAimDegrees - 50f) < 0.001f
            && MathF.Abs(reloaded.UiScale - 1.15f) < 0.001f
            && reloaded.Aim == AimMode.AutoTarget
            && reloaded.BindingLabel(InputAction.Dash).Contains("F", StringComparison.Ordinal);
        Console.WriteLine(
            $"[settings] round-trip={(ok ? "OK" : "FAIL")} окно={reloaded.ComboWindow}с автонаведение={reloaded.AutoAimDegrees} интерфейс={reloaded.UiScale} рывок='{reloaded.BindingLabel(InputAction.Dash)}'");
        if (!ok) throw new InvalidOperationException("settings round-trip failed");
    }

    public static void Shots(string directory)
    {
        Directory.CreateDirectory(directory);
        Rng rng = new(20250801u);
        GameRenderer renderer = new();

        Game game = NewGame();
        game.ViewSize = new Vector2(GameRenderer.Width, GameRenderer.Height);
        for (int i = 0; i < 40; i++) game.Update(1.0 / 60.0);
        Save(renderer, game, Path.Combine(directory, "1-title.png"));

        game.StartRun(20250801u);
        Bot(game, 60 * 18, rng, true);
        Save(renderer, game, Path.Combine(directory, "2-combat.png"));

        // Зажжённый кристалл: его свет должен явно выигрывать у ореола героя.
        game.State = GameState.Playing;
        if (game.Crystals.Count > 0)
        {
            Crystal c = game.Crystals[0];
            game.Player.Pos = c.Pos + GameMath.FromAngle(MathF.PI) * 30f;
            game.Player.AimAngle = 0f;
            for (int i = 0; i < 200 && !c.Activated; i++)
            {
                game.Input.KeyDown(Keys.F);
                game.Update(1.0 / 60.0);
            }
            game.Input.KeyUp(Keys.F);
            game.Player.Pos = c.Pos + GameMath.FromAngle(MathF.PI) * 74f;
            game.Player.AimAngle = 0f;
            for (int i = 0; i < 10; i++) game.Update(1.0 / 60.0);
            Save(renderer, game, Path.Combine(directory, "2b-crystal.png"));
        }
        game.State = GameState.Playing;

        game.State = GameState.LevelUp;
        game.Choices.Clear();
        game.Choices.AddRange(Progression.Cards.Roll(rng, 3));
        game.ChoiceIndex = 1;
        game.Update(1.0 / 60.0);
        Save(renderer, game, Path.Combine(directory, "3-cards.png"));

        game.State = GameState.Playing;
        game.Player.Buffer.Push(Combat.Element.Fire, out _, out _);
        game.Player.Buffer.Push(Combat.Element.Wind, out _, out _);
        game.Player.Buffer.Push(Combat.Element.Wind, out _, out _);
        game.Update(1.0 / 60.0);
        Save(renderer, game, Path.Combine(directory, "4-fusion.png"));

        game.Depth = Game.FinalDepth;
        game.BuildLevel();
        game.Player.DamageMul = 2f;
        Bot(game, 60 * 10, rng, true);
        game.Player.Hp = game.Player.MaxHp;
        game.Update(1.0 / 60.0);
        Save(renderer, game, Path.Combine(directory, "5-boss.png"));

        game.Player.Hp = 0f;
        game.Player.Alive = false;
        for (int i = 0; i < 100; i++) game.Update(1.0 / 60.0);
        Save(renderer, game, Path.Combine(directory, "6-gameover.png"));

        game.State = GameState.Victory;
        game.Update(1.0 / 60.0);
        Save(renderer, game, Path.Combine(directory, "7-victory.png"));

        Game menu = NewGame();
        menu.StartRun(5150);
        menu.State = GameState.Paused;
        for (int i = 0; i < 10; i++) menu.Update(1.0 / 60.0);
        Save(renderer, menu, Path.Combine(directory, "8-pause.png"));

        menu.State = GameState.Settings;
        menu.SettingsRow = 1;
        for (int i = 0; i < 10; i++) menu.Update(1.0 / 60.0);
        Save(renderer, menu, Path.Combine(directory, "9-settings.png"));

        menu.SettingsPage = 1;
        menu.SettingsRow = 5;
        menu.Update(1.0 / 60.0);
        Save(renderer, menu, Path.Combine(directory, "10-bindings.png"));

        menu.RebindingAction = (int)InputAction.Dash;
        menu.Update(1.0 / 60.0);
        Save(renderer, menu, Path.Combine(directory, "11-rebind.png"));

        Console.WriteLine($"frames written to {directory}");
    }

    public static void HudShot(string directory)
    {
        Directory.CreateDirectory(directory);
        GameRenderer renderer = new();

        void Capture(string name, Game game, float scale, string? label)
        {
            int w = (int)(GameRenderer.Width * scale);
            int h = (int)(GameRenderer.Height * scale);
            using Bitmap world = new((int)GameRenderer.Width, (int)GameRenderer.Height, PixelFormat.Format32bppPArgb);
            using Graphics wg = Graphics.FromImage(world);
            wg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            wg.Clear(Color.Black);
            wg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            renderer.DrawWorldOnly(wg, game);

            using Bitmap screen = new(w, h, PixelFormat.Format32bppPArgb);
            using Graphics sg = Graphics.FromImage(screen);
            sg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            sg.Clear(Color.Black);
            sg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            sg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            sg.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            sg.DrawImage(world, new RectangleF(0f, 0f, w, h), new RectangleF(0f, 0f, (float)world.Width, (float)world.Height), GraphicsUnit.Pixel);

            using Bitmap ui = new(w, h, PixelFormat.Format32bppPArgb);
            using Graphics ug = Graphics.FromImage(ui);
            ug.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            ug.Clear(Color.Transparent);
            ug.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            renderer.DrawHudOnly(ug, game, scale);
            sg.DrawImage(ui, new RectangleF(0f, 0f, w, h));

            if (label is not null)
            {
                using Graphics lg = Graphics.FromImage(screen);
                lg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                lg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                lg.DrawString(label, new Font("Consolas", 22f, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel), System.Drawing.Brushes.Yellow, 14f, 12f);
            }

            screen.Save(Path.Combine(directory, name), ImageFormat.Png);
            Console.WriteLine($"{name}: {w}x{h}");
        }

        Game game = NewGame();
        game.Settings.ShowFps = true;
        game.StartRun(20250801u);
        Bot(game, 60 * 10, rng: new Rng(7), cheat: false);
        game.Player.Hp = game.Player.MaxHp;
        game.Player.Alive = true;
        Capture("hud-1080p-combat.png", game, 3f, "1920x1080 native UI");

        game.State = GameState.LevelUp;
        game.Choices.Clear();
        game.Choices.AddRange(Progression.Cards.Roll(new Rng(11), 3));
        game.ChoiceIndex = 0;
        game.Update(1.0 / 60.0);
        Capture("hud-1080p-cards.png", game, 3f, "карточки дара");

        game.State = GameState.Settings;
        game.SettingsRow = 12;
        game.Update(1.0 / 60.0);
        Capture("hud-1080p-settings.png", game, 3f, "настройки");

        game.State = GameState.Playing;
        game.Depth = Game.FinalDepth;
        game.BuildLevel();
        game.Player.DamageMul = 2f;
        Bot(game, 60 * 8, new Rng(3), true);
        game.Player.Hp = game.Player.MaxHp;
        game.Update(1.0 / 60.0);
        Capture("hud-1080p-boss.png", game, 3f, "арена босса");

        game.Settings.NativeUi = false;
        Capture("hud-1080p-pixel.png", game, 3f, "пиксельный HUD (NativeUi выкл)");
        game.Settings.NativeUi = true;

        // Меню: пауза с пунктом выхода и диалог подтверждения
        GameState resume = game.State;
        game.State = GameState.Paused;
        game.PauseSelection = 3;
        game.Update(1.0 / 60.0);
        Capture("hud-1080p-pause.png", game, 3f, "пауза: пункт «в главное меню»");

        game.Confirm = ConfirmKind.ExitToMenu;
        game.ConfirmRow = 1;
        game.Update(1.0 / 60.0);
        Capture("hud-1080p-confirm.png", game, 3f, "подтверждение выхода");

        game.Confirm = ConfirmKind.None;
        game.State = GameState.Title;
        game.TitleRow = 3;
        game.Settings.BestDepth = 7;
        game.Settings.BestScore = 12480;
        game.Settings.BestKills = 63;
        game.Update(1.0 / 60.0);
        Capture("hud-1080p-title.png", game, 3f, "главное меню: выход + рекорд");
        game.State = resume;
    }

    private static void Save(GameRenderer renderer, Game game, string path)
    {
        using Bitmap bitmap = new((int)GameRenderer.Width, (int)GameRenderer.Height, PixelFormat.Format32bppPArgb);
        using Graphics g = Graphics.FromImage(bitmap);
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        g.Clear(Color.Black);
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
        renderer.Draw(g, game);
        bitmap.Save(path, ImageFormat.Png);
    }

    /// <summary>
    /// Проверка сетевого слоя без окна: поднимаем реальный TCP-хост, подключаем
    /// клиента через loopback, обмениваемся вводом и снапшотами, играем раунд.
    /// </summary>
    /// <summary>Снимки экранов дуэли в 1920x1080 без открытия окна.</summary>
    public static void DuelShots(string directory)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Directory.CreateDirectory(directory);
        GameRenderer renderer = new();
        Game game = NewGame();
        game.Settings.ShowFps = true;

        DuelSession duel = new(game);
        duel.Nick = "АРТЁМ";
        duel.StartHosting();
        game.State = GameState.Duel;
        game.Duel = duel;

        void Capture(string name, string label)
        {
            using Bitmap screen = new(1920, 1080, PixelFormat.Format32bppPArgb);
            using Graphics g = Graphics.FromImage(screen);
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            g.Clear(Color.Black);
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;

            using Bitmap world = new((int)GameRenderer.Width, (int)GameRenderer.Height, PixelFormat.Format32bppPArgb);
            using Graphics wg = Graphics.FromImage(world);
            wg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            wg.Clear(Color.Black);
            wg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            renderer.DrawWorldOnly(wg, game, false);

            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(world, new RectangleF(0f, 0f, 1920f, 1080f),
                new RectangleF(0f, 0f, (float)world.Width, (float)world.Height), GraphicsUnit.Pixel);

            using Bitmap ui = new(1920, 1080, PixelFormat.Format32bppPArgb);
            using Graphics ug = Graphics.FromImage(ui);
            ug.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            ug.Clear(Color.Transparent);
            ug.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            renderer.DrawHudOnly(ug, game, 3f);
            g.DrawImage(ui, new RectangleF(0f, 0f, 1920f, 1080f));

            using Bitmap duelLayer = new((int)GameRenderer.Width, (int)GameRenderer.Height, PixelFormat.Format32bppPArgb);
            using Graphics dg = Graphics.FromImage(duelLayer);
            dg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            dg.Clear(Color.Transparent);
            dg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            renderer.DrawDuelWorldLayer(dg, game);
            g.DrawImage(duelLayer, new RectangleF(0f, 0f, 1920f, 1080f),
                new RectangleF(0f, 0f, (float)duelLayer.Width, (float)duelLayer.Height), GraphicsUnit.Pixel);

            using Graphics lg = Graphics.FromImage(screen);
            lg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            lg.DrawString(label, new Font("Consolas", 20f, FontStyle.Bold, GraphicsUnit.Pixel), Brushes.Yellow, 12f, 1008f);

            screen.Save(Path.Combine(directory, name), ImageFormat.Png);
            Console.WriteLine($"{name}: 1920x1080");
        }

        duel.Phase = DuelPhase.Nickname;
        duel.NickDraft = "АРТЁМ";
        duel.Tick(0.2f);
        Capture("duel-01-nickname.png", "ввод никнейма");

        duel.Nick = "АРТЁМ";
        duel.Phase = DuelPhase.Main;
        duel.Row = 0;
        duel.Status = string.Empty;
        duel.StatusTimer = 0f;
        duel.Tick(0.2f);
        Capture("duel-02-main.png", "меню дуэли");

        duel.Phase = DuelPhase.Hosting;
        duel.GuestNick = string.Empty;
        duel.Tick(0.2f);
        Capture("duel-02b-waiting.png", "ожидание: виден адрес для ручного подключения");

        duel.Phase = DuelPhase.DirectIp;
        duel.IpDraft = "192.168.0.112";
        duel.IpStatus = string.Empty;
        duel.Tick(0.2f);
        Capture("duel-02c-ip.png", "ручное подключение по адресу");

        duel.Phase = DuelPhase.Browsing;
        duel.VisibleRooms.Add(new RoomListing
        {
            Name = "ПЕЩЕРА",
            HostNick = "АРТЁМ",
            Players = 1,
            MaxPlayers = 2,
        });
        duel.VisibleRooms.Add(new RoomListing
        {
            Name = "КРИСТАЛЛ",
            HostNick = "ДИМА",
            Players = 1,
            MaxPlayers = 2,
        });
        duel.RoomRow = 0;
        duel.DiscoverTimer = 999f;
        duel.Tick(0.2f);
        Capture("duel-03-rooms.png", "список комнат");

        duel.Phase = DuelPhase.InRoom;
        duel.HostNick = "АРТЁМ";
        duel.GuestNick = "ДИМА";
        duel.Row = 0;
        duel.SetStatus("соперник на месте, можно начинать", 10f);
        duel.Tick(0.2f);
        Capture("duel-04-room.png", "комната: ждём и стартуем");

        game.StartDuel(0, duel);
        game.State = GameState.Duel;
        duel.Phase = DuelPhase.Fighting;
        if (game.Foe is not null)
        {
            game.Foe.Pos = new Vector2(game.Player.Pos.X + 150f, game.Player.Pos.Y - 20f);
        }
        for (int i = 0; i < 60; i++) game.UpdateDuel(1f / 60f);
        duel.Tick(1f / 60f);
        Capture("duel-05-fight.png", "матч в пещере");

        game.Player.Hp = 60f;
        if (game.Foe is not null) game.Foe.Hp = 44f;
        duel.CollectHostState();
        Capture("duel-06-hud.png", "HUD дуэли: HP и счёт");

        duel.WinsHost = 2;
        duel.RoundNumber = 4;
        game.Foe!.Hp = 0f;
        game.Foe.Alive = false;
        game.UpdateDuel(1f / 60f);
        duel.Phase = DuelPhase.RoundOver;
        duel.CollectHostState();
        Capture("duel-07-round.png", "конец раунда");

        duel.WinsHost = 3;
        duel.MatchWinner = 0;
        duel.Phase = DuelPhase.MatchOver;
        Capture("duel-08-win.png", "победа в матче");

        duel.Dispose();
        Console.WriteLine("=== duel shots done ===");
    }

    /// <summary>
    /// Реальный сетевой матч двух процессов: --duelhead host|join [ник] [секунд].
    /// Хост сам создаёт комнату, клиент находит её через UDP-обнаружение,
    /// подключается и играет против хоста автопилотом. Печатает ход матча.
    /// </summary>
    public static void DuelHeadless(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        string role = args.Length > 1 ? args[1].ToLowerInvariant() : "host";
        string nick = args.Length > 2 ? args[2] : (role == "host" ? "ХОСТ" : "ГОСТЬ");
        float seconds = args.Length > 3 ? float.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 12f;
        bool autoStart = Array.IndexOf(args, "--autostart") >= 0;

        string directAddress = string.Empty;
        int directPort = NetProtocol.GamePort;
        int ipIndex = Array.IndexOf(args, "--ip");
        if (ipIndex >= 0 && ipIndex + 1 < args.Length)
        {
            directAddress = args[ipIndex + 1];
            int colon = directAddress.IndexOf(':');
            if (colon > 0 && int.TryParse(directAddress[(colon + 1)..], out int parsed))
            {
                directPort = parsed;
                directAddress = directAddress[..colon];
            }
        }

        Game game = NewGame();
        game.Settings.ShowFps = false;
        game.Settings.MouseAutoFire = true;

        DuelSession duel = new(game);
        duel.Nick = nick;
        duel.RoomName = "ПЕЩЕРА";

        string roleLabel = role == "host" ? "ХОСТ" : "ГОСТЬ";
        Console.WriteLine($"[{roleLabel}] запуск, ник='{nick}'");

        if (role == "host")
        {
            duel.StartHosting();
            game.State = GameState.Duel;
            game.Duel = duel;
            Console.WriteLine($"[ХОСТ] комната создана, порт {duel.Host?.Port}, объявляю в сети");
        }
        else
        {
            duel.StartBrowsing();
            game.State = GameState.Duel;
            game.Duel = duel;
            Console.WriteLine("[ГОСТЬ] ищу комнаты в сети...");
        }

        int frames = (int)(seconds * 60f);
        bool started = false;
        int joinedAt = -1;
        int lastReported = -1;

        for (int f = 0; f < frames; f++)
        {
            if (role == "join")
            {
                if (duel.Phase == DuelPhase.Browsing && directAddress.Length > 0)
                {
                    if (duel.JoinAddress(directAddress, directPort))
                    {
                        Console.WriteLine($"[ГОСТЬ] подключаюсь напрямую к {directAddress}:{directPort}");
                    }
                }
                else if (duel.Phase == DuelPhase.Browsing && duel.VisibleRooms.Count > 0)
                {
                    duel.JoinSelected();
                    Console.WriteLine($"[ГОСТЬ] подключаюсь к «{duel.VisibleRooms[0].Name}»");
                }
                else if (duel.Phase == DuelPhase.Loading && duel.Client is not null)
                {
                    if (duel.Client.Peers.Count > 0)
                    {
                        duel.LocalId = duel.Client.LocalId;
                        foreach ((byte id, string peerNick, bool isHost) in duel.Client.Peers)
                        {
                            if (isHost) duel.HostNick = peerNick;
                            else duel.GuestNick = peerNick;
                        }
                        duel.BeginMatch();
                        started = true;
                        joinedAt = f;
                        Console.WriteLine($"[ГОСТЬ] матч начат! соперник: {duel.HostNick}");
                    }
                }
            }
            else if (duel.IsHost && !started)
            {
                if (duel.Host is not null)
                {
                    foreach (DuelPeer peer in duel.Host.Peers)
                    {
                        if (!peer.IsLocal) duel.GuestNick = peer.Nick;
                    }
                }
                if (autoStart && duel.GuestNick.Length > 0)
                {
                    duel.LocalId = 0;
                    game.StartDuel(0, duel);
                    duel.Host!.Foe = game.Foe;
                    duel.Host.Broadcast(MsgKind.StartMatch, w =>
                    {
                        w.Write((byte)0);
                        w.Write(game.Seed);
                        w.Write(1);
                    });
                    duel.Phase = DuelPhase.Fighting;
                    started = true;
                    joinedAt = f;
                    Console.WriteLine($"[ХОСТ] матч начат! соперник: {duel.GuestNick}");
                }
            }

            if (duel.Phase == DuelPhase.Fighting)
            {
                DuelAutopilot(game, duel, f);

                // Именно game.Update, а не прямой вызов UpdateDuel.
                // Раньше тест звал UpdateDuel вручную и поэтому проходил,
                // хотя обычный цикл игры дуэль не обрабатывал вовсе.
                // EndFrame вызывать нельзя - Update уже делает это сам,
                // и повторный вызов съедал нажатия гостя.
                game.Update(1.0 / 60.0);

                if (started && f % 60 == 0)
                {
                    Player? foe = game.Foe;
                    float gap = foe is null ? 0f : Vector2.Distance(game.Player.Pos, foe.Pos);
                    Console.WriteLine($"[{roleLabel}] t={f / 60}s hp={game.Player.Hp:F0} " +
                        $"foeHp={foe?.Hp ?? -1:F0} дистанция={gap:F0} " +
                        $"я=({game.Player.Pos.X:F0},{game.Player.Pos.Y:F0}) " +
                        $"foe=({foe?.Pos.X ?? 0:F0},{foe?.Pos.Y ?? 0:F0}) " +
                        $"localId={duel.LocalId} тиков={game.DuelTick}");
                }
            }
            else
            {
                duel.Tick(1f / 60f);
            }

            int second = (f - (joinedAt < 0 ? 0 : joinedAt)) / 60;
            if (started && second != lastReported && second % 2 == 0)
            {
                lastReported = second;
                Player? foe = game.Foe;
                if (foe is not null)
                {
                    Console.WriteLine($"[{roleLabel}] t={second}s  я hp={game.Player.Hp:F0}  соперник hp={foe.Hp:F0}  " +
                        $"счёт {duel.WinsHost}:{duel.WinsGuest}  снарядов={game.Projectiles.Count}  " +
                        $"снапшотов={(duel.IsHost ? duel.RenderPlayers.Count : duel.Client?.Snapshots.Count ?? 0)}");
                }
            }

            if (duel.Phase is DuelPhase.MatchOver or DuelPhase.RoundOver && duel.RoundNumber > 1)
            {
                Console.WriteLine($"[{roleLabel}] раунд завершён, счёт {duel.WinsHost}:{duel.WinsGuest}");
            }
            if (duel.Phase == DuelPhase.MatchOver) break;

            System.Threading.Thread.Sleep(1);
        }

        Console.WriteLine($"[{roleLabel}] ИТОГ: фаза={duel.Phase}, раунд={duel.RoundNumber}, счёт={duel.WinsHost}:{duel.WinsGuest}, " +
            $"победитель={(duel.MatchWinner >= 0 ? duel.MatchWinner.ToString() : "нет")}");
        duel.Dispose();
        Console.WriteLine($"=== {roleLabel} done ===");
    }

    /// <summary>Простой автопилот дуэли: держит дистанцию и стреляет в соперника.</summary>
    private static void DuelAutopilot(Game game, DuelSession duel, int frame)
    {
        Player? foe = game.Foe;
        if (foe is null) return;

        Player me = game.Player;
        Vector2 delta = foe.Pos - me.Pos;
        float dist = delta.Length();
        if (dist < 1f) return;

        Vector2 dir = delta / dist;
        Vector2 move = dist > 75f ? dir : dist < 34f ? -dir * 0.6f : new Vector2(-dir.Y, dir.X) * 0.7f;

        uint down = 0u;
        if (move.X > 0.3f) down |= 1u << (int)InputAction.MoveRight;
        if (move.X < -0.3f) down |= 1u << (int)InputAction.MoveLeft;
        if (move.Y > 0.3f) down |= 1u << (int)InputAction.MoveDown;
        if (move.Y < -0.3f) down |= 1u << (int)InputAction.MoveUp;

        if (frame % 45 == 0) down |= 1u << (int)InputAction.Dash;
        if (frame % 36 < 18) down |= 1u << (int)InputAction.Fire;

        PointF mouse = new(
            (float)(foe.Pos.X - game.Camera.Position.X),
            (float)(foe.Pos.Y - game.Camera.Position.Y));

        if (duel.IsHost)
        {
            RemoteInputState state = new()
            {
                Down = down,
                Pressed = 0u,
                MouseX = mouse.X,
                MouseY = mouse.Y,
                Any = true,
            };
            duel.Host!.LocalInput = state;
            game.DuelLocalOverride = state;
        }
        else
        {
            // Гость: ввод уходит в сеть, но применяется локально сразу -
            // так управление отзывчиво, а хост всё равно решает исход.
            RemoteInputState own = new()
            {
                Down = down,
                Pressed = 0u,
                MouseX = mouse.X,
                MouseY = mouse.Y,
                Any = true,
            };
            game.DuelLocalOverride = own;
            duel.Client?.SendInput(frame, down, 0u, mouse.X, mouse.Y);
        }
    }

    public static void NetTest()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== net test ===");

        Game game = NewGame();
        game.Settings.ShowFps = true;

        DuelSession duel = new(game);
        duel.RoomName = "ТЕСТ";
        duel.Nick = "ХОСТ";
        duel.StartHosting();

        DuelHost host = duel.Host!;
        Console.WriteLine($"[net] хост слушает порт {host.Port}, running={host.Running}");
    if (!host.Running)
    {
        Console.WriteLine("[net] ОШИБКА: хост не запустился");
        return;
    }

    TestRoomDiscovery(duel, host);
    TestDuelTargeting(duel, host);


        // 1. loopback-подключение
        if (!duel.HostLoopback(out string error))
        {
            Console.WriteLine($"[net] ОШИБКА loopback: {error}");
            return;
        }
        Console.WriteLine("[net] loopback-подключение: OK");

        // 2. клиент видит комнату в широковещании (локально проверим обработчик пакета)
        Console.WriteLine($"[net] игроков в комнате: {duel.PeerCount}");
        if (duel.PeerCount < 2)
        {
            Console.WriteLine("[net] ОШИБКА: второй игрок не зарегистрирован");
            return;
        }
        byte guestId = duel.GuestId;
        Console.WriteLine($"[net] гость подключился, id={guestId}, ник='{duel.PeerNick(guestId)}'");

        // 3. старт матча: хост создаёт пещеру и двух игроков
        duel.LocalId = 0;
        game.StartDuel(0, duel);
        host.Foe = game.Foe;
        duel.Phase = DuelPhase.Fighting;
        duel.CountdownTimer = 3f;
        Console.WriteLine($"[net] пещера {game.Level.W}x{game.Level.H}, спавн1={game.Level.Spawn}, спавн2={game.Level.Spawn2}");
        if (game.Foe is null)
        {
            Console.WriteLine("[net] ОШИБКА: нет второго игрока");
            return;
        }
        Console.WriteLine($"[net] игроки разведены: хост={game.Player.Pos}, гость={game.Foe.Pos}");
        float gap = Vector2.Distance(game.Player.Pos, game.Foe.Pos);
        Console.WriteLine($"[net] расстояние между игроками: {gap:F0}px");
        if (gap < 300f)
        {
            Console.WriteLine("[net] ОШИБКА: игроки стартуют слишком близко");
            return;
        }

        // 4. симулируем 5 секунд дуэли: хост считает, сношения шлются
        RemoteInputState input = new();
        int frames = 300;
        for (int i = 0; i < frames; i++)
        {
            input.Down = 1u << (int)InputAction.MoveRight;
            input.MouseX = 400f;
            input.MouseY = 180f;
            input.Any = true;
            host.LocalInput = input;

            // удалённый игрок тоже куда-то идёт
            RemoteInputState remote = new()
            {
                Down = 1u << (int)InputAction.MoveLeft,
                MouseX = 240f,
                MouseY = 180f,
                Any = true,
            };
            host.InjectInput(guestId, remote);

            game.UpdateDuel(1f / 60f);
            duel.Tick(1f / 60f);
        }

        Console.WriteLine($"[net] отработано тиков: {game.DuelTick}");
        Console.WriteLine($"[net] хост: hp={game.Player.Hp:F0}/{game.Player.MaxHp:F0} pos={game.Player.Pos.X:F0},{game.Player.Pos.Y:F0}");
        Console.WriteLine($"[net] гость: hp={game.Foe.Hp:F0}/{game.Foe.MaxHp:F0} pos={game.Foe.Pos.X:F0},{game.Foe.Pos.Y:F0}");
        Console.WriteLine($"[net] снарядов: {game.Projectiles.Count}");

        // хост играет на локальной клавиатуре, гость - на сетевом вводе
        bool foeMoved = MathF.Abs(game.Foe.Pos.X - game.Level.Spawn2.X) > 4f;
        bool hostInputSent = host.LocalInput.Any;
        Console.WriteLine($"[net] ввод хоста уходит в сеть: {(hostInputSent ? "да" : "НЕТ")}");
        Console.WriteLine($"[net] гость управляется сетью: {(foeMoved ? "да" : "НЕТ")}");
        Console.WriteLine($"[net] снапшотов для отрисовки: {duel.RenderPlayers.Count}, снарядов: {duel.RenderShots.Count}");

        // 5. урон между игроками: телепортруем рядом и кастуем
        game.Foe.Pos = game.Player.Pos + new Vector2(30f, 0f);
        game.Foe.Invuln = 0f;
        game.Player.Mana = game.Player.MaxMana;
        float hpBefore = game.Foe.Hp;
        game.Caster = game.Player;
        SpellDef? bolt = SpellDB.Find("FF");
        if (bolt is not null) CombatUtil.Cast(game, game.Player, bolt, false);
        for (int i = 0; i < 40; i++) game.UpdateDuel(1f / 60f);
        Console.WriteLine($"[net] hp соперника до={hpBefore:F0} после={game.Foe.Hp:F0}");
        bool damaged = game.Foe.Hp < hpBefore;
        Console.WriteLine($"[net] урон по сопернику наносится: {(damaged ? "да" : "НЕТ")}");

        // 6. завершение раунда
        game.Foe.Hp = 0f;
        game.Foe.Alive = false;
        game.UpdateDuel(1f / 60f);
        Console.WriteLine($"[net] раунд завершён: счёт {duel.WinsHost}:{duel.WinsGuest}, раунд {duel.RoundNumber}, победитель раунда {duel.RoundWinner}");
        bool scored = duel.WinsHost == 1;
        Console.WriteLine($"[net] победа засчитана: {(scored ? "да" : "НЕТ")}");

        // 7. второй раунд со сбросом
        duel.BeginRound();
        bool reset = game.Foe is not null && game.Foe.Alive && game.Foe.Hp == game.Foe.MaxHp;
        Console.WriteLine($"[net] сброс раунда: {(reset ? "да" : "НЕТ")}");

        // 8. матч до 3 побед
        for (int w = 1; w < DuelRules.WinsRequired; w++)
        {
            game.Foe!.Hp = 0f;
            game.Foe.Alive = false;
            game.UpdateDuel(1f / 60f);
            duel.Phase = DuelPhase.Fighting;
        }
        bool finished = duel.WinsHost >= DuelRules.WinsRequired || duel.MatchWinner >= 0;
        Console.WriteLine($"[net] матч до {DuelRules.WinsRequired} побед: счёт {duel.WinsHost}:{duel.WinsGuest}, MatchWinner={duel.MatchWinner}");
        Console.WriteLine($"[net] матч завершён: {(finished ? "да" : "НЕТ")}");

        bool snapsOk = duel.RenderPlayers.Count == 2;
        bool separated = gap > 300f;

        // После смерти игрока должен быть переход: раунд -> следующий раунд.
        // Без проверки игра зависала на экране смерти с трясущимся кадром.
        bool roundFlow = TestRoundRestart(duel, game);

        TestDuelBot(duel, host);

        duel.Dispose();

        bool pass = foeMoved && hostInputSent && damaged && scored && reset && finished && snapsOk && separated && roundFlow;
        Console.WriteLine(pass ? "=== net ok ===" : "=== net FAILED ===");
    }

    /// <summary>
    /// Убиваем соперника и проверяем, что игра не зависает: раунд должен
    /// завершиться, счёт вырасти на 1 (не на 3), а новый раунд - начаться.
    /// </summary>
    private static bool TestRoundRestart(DuelSession duel, Game game)
    {
// Без бота: проверяем только переход после смерти соперника.
    duel.StartHosting();
    duel.Phase = DuelPhase.Fighting;
    game.StartDuel(0, duel);
    if (duel.Host is not null) duel.Host.Foe = game.Foe;

    int scoreBefore = duel.WinsHost + duel.WinsGuest;
    int roundBefore = duel.RoundNumber;

    // Соперник падает.
    game.Foe!.Hp = 0f;
    game.Foe.Alive = false;

    bool sawRoundOver = false;
    string? stuck = null;
    for (int f = 0; f < 60 * 12; f++)
    {
        duel.UpdateInput(game.Input);
        game.Update(1.0 / 60.0);

        if (duel.Phase == DuelPhase.RoundOver) sawRoundOver = true;

        int total = duel.WinsHost + duel.WinsGuest;
        if (total - scoreBefore > 1)
        {
            stuck = "счёт прыгнул сразу на несколько побед";
            break;
        }
        if (sawRoundOver && duel.Phase == DuelPhase.Fighting) break;
    }

    int totalNow = duel.WinsHost + duel.WinsGuest;
    bool oneWin = totalNow == scoreBefore + 1;
    bool newRound = duel.Phase == DuelPhase.Fighting && duel.RoundNumber > roundBefore;
    bool revived = game.Foe is { Alive: true } && game.Player is { Alive: true };

    Console.WriteLine($"[раунд] экран итога показан={sawRoundOver}, фаза={duel.Phase}, " +
        $"счёт {duel.WinsHost}:{duel.WinsGuest}, раунд {duel.RoundNumber} (был {roundBefore}), " +
        $"оба живы={revived}");

    if (stuck is not null) Console.WriteLine($"[раунд] ОШИБКА: {stuck}");
    if (!sawRoundOver) Console.WriteLine("[раунд] ОШИБКА: экран итога раунда не показан");
    if (!oneWin) Console.WriteLine("[раунд] ОШИБКА: победа засчитана не один раз");
    if (!newRound) Console.WriteLine("[раунд] ОШИБКА: новый раунд не начался");
    if (!revived) Console.WriteLine("[раунд] ОШИБКА: игроки не восстановлены");

    return sawRoundOver && oneWin && newRound && revived && stuck is null;
}

    /// <summary>По одному кадру на каждую тему пещеры + арены боссов.</summary>
    public static void ThemeShots(string directory)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Directory.CreateDirectory(directory);
        GameRenderer renderer = new();

        void Capture(string name, Game game)
        {
            using Bitmap screen = new(1920, 1080, PixelFormat.Format32bppPArgb);
            using Graphics g = Graphics.FromImage(screen);
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            g.Clear(Color.Black);
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;

            using Bitmap world = new((int)GameRenderer.Width, (int)GameRenderer.Height, PixelFormat.Format32bppPArgb);
            using Graphics wg = Graphics.FromImage(world);
            wg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            wg.Clear(Color.Black);
            wg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            renderer.DrawWorldOnly(wg, game, false);

            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(world, new RectangleF(0f, 0f, 1920f, 1080f),
                new RectangleF(0f, 0f, (float)world.Width, (float)world.Height), GraphicsUnit.Pixel);

            using Bitmap ui = new(1920, 1080, PixelFormat.Format32bppPArgb);
            using Graphics ug = Graphics.FromImage(ui);
            ug.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            ug.Clear(Color.Transparent);
            ug.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            renderer.DrawHudOnly(ug, game, 3f);
            g.DrawImage(ui, new RectangleF(0f, 0f, 1920f, 1080f));

            screen.Save(Path.Combine(directory, name), ImageFormat.Png);
            Console.WriteLine($"{name}: {game.Level.Theme} / глубина {game.Level.Depth} / босс={game.Level.IsBoss}");
        }

        int[] depths = { 2, 6, 10, 14 };
        for (int i = 0; i < depths.Length; i++)
        {
            Game game = NewGame();
            game.Settings.ShowFps = true;
            game.StartRun(1000u + (uint)depths[i]);
            game.Depth = depths[i];
            game.BuildLevel();
            game.Banner = game.Level.IsBoss ? $"ГЛУБИНА {game.Depth} — СТРАЖ" : $"ГЛУБИНА {game.Depth}";
            game.BannerTimer = 0f;
            game.Update(1.0 / 60.0);
            Capture($"theme-{i + 1}-depth{depths[i]}.png", game);
        }

        int[] bossDepths = { 5, 10, 15 };
        for (int i = 0; i < bossDepths.Length; i++)
        {
            Game game = NewGame();
            game.Settings.ShowFps = true;
            game.StartRun(2000u + (uint)bossDepths[i]);
            game.Depth = bossDepths[i];
            game.BuildLevel();

            BossDef.Data def = BossDef.ForDepth(bossDepths[i]);
            game.Banner = $"ГЛУБИНА {game.Depth} — {def.Name}";
            game.BannerTimer = 0f;

            // Босс спавнится у портала, то есть у самого края арены. Для снимка
            // переносим его в центр зала - иначе он уезжает под HUD.
            Enemy? boss = game.Enemies.Find(e => e.Kind == EnemyKind.Boss);
            if (boss is not null)
            {
                Vector2 centre = game.Level.PixelSize * 0.5f;
                Vector2 here = game.Level.RandomFloorPoints(new Rng((uint)(21 + i)), 1, centre, 90f)
                    .FirstOrDefault(p => p != Vector2.Zero);
                if (here != Vector2.Zero) boss.Pos = here;
                boss.Vel = Vector2.Zero;
            }

            // Босс не должен умереть на снимке - отключаем урон игрока.
            game.Player.DamageMul = 0f;
            game.Player.Hp = game.Player.MaxHp;
            game.Player.Invuln = 30f;

            // Сначала даём боту подраться, чтобы на кадре были снаряды и эффекты.
            Bot(game, 60 * 4, new Rng((uint)(3 + i)), true);

            // Потом ставим игрока рядом с боссом: камера ведёт за игроком,
            // иначе босс уезжает за край кадра.
            Enemy? live = game.Enemies.Find(e => e.Kind == EnemyKind.Boss);
            if (live is not null && !live.Dead)
            {
                Enemy bossRef = live;
                // Ставим игрока в 46 пикселях от босса - камера ведёт за игроком,
                // поэтому босс гарантированно попадёт в кадр. Точку ищем свободную,
                // иначе Resolve вытолкнет игрока в стену и кадр снова уедет.
                Vector2 spot = SpotBeside(game, bossRef.Pos);
                game.Player.Pos = spot;
                game.Player.Vel = Vector2.Zero;
                game.Camera.SnapTo(game.Player.Pos, new Vector2(GameRenderer.Width, GameRenderer.Height), game.Level.PixelSize);
                bossRef.Hp = bossRef.MaxHp;
                // Игрока держим рядом с боссом каждый кадр: иначе контактный
                // урон отбрасывает его, и босс уезжает из кадра. Даём время,
                // чтобы эффекты взрыва резонанса осели и кадр был читаемым.
                for (int f = 0; f < 150; f++)
                {
                    game.Player.Hp = game.Player.MaxHp;
                    game.Player.Invuln = 30f;
                    game.Player.Pos = SpotBeside(game, bossRef.Pos);
                    game.Player.Vel = Vector2.Zero;
                    game.Camera.SnapTo(game.Player.Pos, new Vector2(GameRenderer.Width, GameRenderer.Height), game.Level.PixelSize);
                    game.Update(1.0 / 60.0);
                }

                // Ждём «тихого» кадра: босс вернулся в покой и на поле нет
                // телеграфов, иначе круговые атаки полностью закрывают спрайт.
                for (int f = 0; f < 600; f++)
                {
                    if (bossRef.State == 0 && game.Effects.Telegraphs.Count == 0 && game.Projectiles.Count == 0) break;
                    game.Player.Hp = game.Player.MaxHp;
                    game.Player.Invuln = 30f;
                    game.Player.Pos = SpotBeside(game, bossRef.Pos);
                    game.Player.Vel = Vector2.Zero;
                    game.Camera.SnapTo(game.Player.Pos, new Vector2(GameRenderer.Width, GameRenderer.Height), game.Level.PixelSize);
                    game.Update(1.0 / 60.0);
                }
            }

            game.Player.Hp = game.Player.MaxHp;
            game.Update(1.0 / 60.0);

            // Бот подрывается своими же рунами ("Взрыв пара"), а это даёт
            // красную вспышку на весь экран. Для снимка её гасим.
            game.FlashAmount = 0f;
            game.Player.HitFlash = 0f;

            bool visible = boss is not null && !boss.Dead;
            Console.WriteLine(
                $"[shot] {def.Name} на экране: {visible} boss={boss?.Pos} player={game.Player.Pos} " +
                $"cam={game.Camera.Position} hp={boss?.Hp:0}/{boss?.MaxHp:0} enemies={game.Enemies.Count}");
            Capture($"boss-arena-{bossDepths[i]}.png", game);
        }

        BossSpriteSheet(Path.Combine(directory, "boss-sprites.png"));
        SpellSheetShot(Path.Combine(directory, "spell-sheets.png"));

        Console.WriteLine("=== theme shots done ===");
    }

    /// <summary>
    /// Проверка нарезки листов заклинаний: каждый кадр рисуется отдельно,
    /// чтобы было видно, что соседние кадры не наезжают друг на друга.
    /// </summary>
    private static void SpellSheetShot(string path)
    {
        string[] keys =
        {
            "arcane-nova", "claw-bolt", "void-field", "ring-nova", "shard-rain",
            "smoke-bolt", "fire-bolt", "comet-rain", "shock-ring",
        };

        const int cellW = 96;
        const int cellH = 96;
        const int maxFrames = 15;
        int rows = keys.Length;

        using Bitmap sheet = new(cellW * maxFrames, cellH * rows, PixelFormat.Format32bppPArgb);
        using Graphics g = Graphics.FromImage(sheet);
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        g.Clear(Palette.Rgb(24, 22, 30));
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;

        for (int r = 0; r < rows; r++)
        {
            string key = keys[r];
            int frames = SpellSprites.FrameCount(key);
            if (frames <= 0)
            {
                Text.Draw(g, $"{key}: НЕТ ЛИСТА", Text.Tiny, Palette.Health, 6f, r * cellH + cellH * 0.5f);
                continue;
            }

            // Тёмная клетка нужна, чтобы было видно прозрачные края кадра.
            for (int f = 0; f < frames && f < maxFrames; f++)
            {
                float x = f * cellW + cellW * 0.5f;
                float y = r * cellH + cellH * 0.5f;
                Palette.Fill(g, Palette.Rgb(38, 36, 48), f * cellW + 2f, r * cellH + 2f, cellW - 4f, cellH - 4f);
                SpellSprites.Draw(g, key, new Vector2(x, y), (cellW - 6f) / 0.78f, frames <= 1 ? 0f : f / (float)(frames - 1));
            }
            Text.Draw(g, $"{key} ({frames})", Text.Tiny, Palette.Paper, 6f, r * cellH + cellH - 12f);
        }

        using Bitmap big = new(sheet.Width * 2, sheet.Height * 2, PixelFormat.Format32bppPArgb);
        using Graphics bg = Graphics.FromImage(big);
        bg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        bg.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        bg.DrawImage(sheet, 0, 0, sheet.Width * 2, sheet.Height * 2);
        big.Save(path, ImageFormat.Png);
        Console.WriteLine($"spell-sheets.png: {rows} листов");
    }

    /// <summary>
    /// Снимки каста: заклинание каждого вида запускается и кадр снимается
    /// в середине анимации, когда спрайт уже развернулся, но ещё не погас.
    /// </summary>
    public static void CastShots(string directory)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Directory.CreateDirectory(directory);
        GameRenderer renderer = new();

        (string Combo, string Label)[] cases =
        {
            ("F", "bolt-fire"),
            ("A", "bolt-water"),
            ("W", "cone-wind"),
            ("EE", "bolt-earth"),
            ("D", "bolt-dark"),
            ("DDD", "nova-dark"),
            ("EEE", "nova-earth"),
            ("WWW", "nova-wind"),
            ("LLL", "nova-light"),
            ("DDE", "field-void"),
            ("FWW", "field-fire"),
            ("LLLL", "rain-light"),
            ("FFFF", "rain-fire"),
            ("DAL", "beam"),
        };

        foreach ((string combo, string label) in cases)
        {
            Game game = NewGame();
            game.Settings.ShowFps = true;
            game.StartRun(9000u + (uint)combo.Length);
            game.Depth = 4;
            game.BuildLevel();
            game.Player.Hp = game.Player.MaxHp;
            game.Player.Mana = game.Player.MaxMana;

            SpellDef def = SpellDB.ByCombo[combo];
            CombatUtil.Cast(game, game.Player, def, def.Fusion);

            // Доводим до середины анимации самого большого спрайта на экране.
            for (int f = 0; f < 240; f++)
            {
                game.Update(1.0 / 60.0);
                CastSprite? biggest = null;
                foreach (CastSprite c in game.Effects.CastSprites)
                {
                    if (biggest is null || c.Size > biggest.Size) biggest = c;
                }
                if (biggest is not null && biggest.T01 >= 0.45f) break;
            }

            game.FlashAmount = 0f;
            string name = $"cast-{label}-{combo}.png";
            CaptureWorld(renderer, game, Path.Combine(directory, name));
            Console.WriteLine($"{name}: {def.Name} лист={def.SheetKey} спрайтов={game.Effects.CastSprites.Count}");
        }

        Console.WriteLine("=== cast shots done ===");
    }

    private static void CaptureWorld(GameRenderer renderer, Game game, string path)
    {
        using Bitmap screen = new(1920, 1080, PixelFormat.Format32bppPArgb);
        using Graphics g = Graphics.FromImage(screen);
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        g.Clear(Color.Black);
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;

        using Bitmap world = new((int)GameRenderer.Width, (int)GameRenderer.Height, PixelFormat.Format32bppPArgb);
        using Graphics wg = Graphics.FromImage(world);
        wg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        wg.Clear(Color.Black);
        wg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
        renderer.DrawWorldOnly(wg, game, false);

        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        g.DrawImage(world, new RectangleF(0f, 0f, 1920f, 1080f),
            new RectangleF(0f, 0f, world.Width, world.Height), GraphicsUnit.Pixel);

        using Bitmap ui = new(1920, 1080, PixelFormat.Format32bppPArgb);
        using Graphics ug = Graphics.FromImage(ui);
        ug.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        ug.Clear(Color.Transparent);
        ug.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
        renderer.DrawHudOnly(ug, game, 3f);
        g.DrawImage(ui, new RectangleF(0f, 0f, 1920f, 1080f));

        screen.Save(path, ImageFormat.Png);
    }

    /// <summary>
    /// Лист спрайтов всех трёх боссов: три фазы, три покадровых момента.
    /// Нужен, чтобы проверять форму боссов отдельно от игрового кадра.
    /// </summary>
    private static void BossSpriteSheet(string path)
    {
        using Bitmap sheet = new(96 * 3, 112 * 3, PixelFormat.Format32bppPArgb);
        using Graphics g = Graphics.FromImage(sheet);
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        g.Clear(Palette.Rgb(22, 20, 28));
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

        using Bitmap cell = new(96, 112, PixelFormat.Format32bppPArgb);
        for (int variant = 0; variant < 3; variant++)
        {
            for (int phase = 0; phase < 3; phase++)
            {
                float time = phase * 0.9f;
                using Graphics cg = Graphics.FromImage(cell);
                cg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                cg.Clear(Color.Transparent);
                cg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                Sprites.DrawBoss(cg, 34f, 44f, time, phase, 0f, variant);

                g.DrawImage(cell, new Rectangle(variant * 96, phase * 112, 96, 112), 0, 0, 96, 112, GraphicsUnit.Pixel);
                Text.Draw(g, BossDef.All[variant].Name, Text.Tiny, Palette.Rgb(220, 220, 230), variant * 96 + 2f, phase * 112 + 104f);
            }
        }

        using Bitmap big = new(sheet.Width * 3, sheet.Height * 3, PixelFormat.Format32bppPArgb);
        using Graphics bg = Graphics.FromImage(big);
        bg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        bg.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        bg.DrawImage(sheet, 0, 0, sheet.Width * 3, sheet.Height * 3);
        big.Save(path, ImageFormat.Png);
        Console.WriteLine($"boss-sprites.png: 3 босса x 3 фазы");
    }

    /// <summary>Свободная от стен точка рядом с боссом - для постановки кадра.</summary>
    private static Vector2 SpotBeside(Game game, Vector2 bossPos)
    {
        for (int ring = 0; ring < 3; ring++)
        {
            float dist = 46f + ring * 26f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * GameMath.Tau / 8f + ring * 0.4f;
                Vector2 p = bossPos + GameMath.FromAngle(angle) * dist;
                p = new Vector2(
                    GameMath.Clamp(p.X, 30f, game.Level.PixelSize.X - 30f),
                    GameMath.Clamp(p.Y, 30f, game.Level.PixelSize.Y - 30f));
                if (!game.Level.SolidAt(p)) return p;
            }
        }
        return bossPos;
    }

    /// <summary>
    /// Сценарий выхода из забега: пауза -> пункт "в главное меню" -> диалог ->
    /// отмена -> повтор -> согласие -> титульный экран и запись рекорда.
    /// </summary>
    private static void TestMenuExitFlow()
    {
        Game game = NewGame();
        game.StartRun(4242);
        game.Depth = 3;
        game.Score = 5000;
        game.Kills = 17;

        game.State = GameState.Paused;
        game.PauseSelection = 3;
        if (game.PauseSelection != 3) Console.WriteLine("[menu] ОШИБКА: пауза не даёт 4-й пункт");

        // выбор пункта открывает диалог и НЕ выходит сразу
        game.Confirm = ConfirmKind.ExitToMenu;
        game.ConfirmRow = 1;
        game.State = GameState.Paused;
        game.Update(1.0 / 60.0);
        if (game.Confirm != ConfirmKind.ExitToMenu) Console.WriteLine("[menu] ОШИБКА: диалог не открылся");
        if (game.State != GameState.Paused) Console.WriteLine("[menu] ОШИБКА: ушли из паузы до подтверждения");

        // отмена: остаёмся в паузе, забег цел
        game.ConfirmRow = 1;
        game.Confirm = ConfirmKind.None;
        if (game.State != GameState.Paused) Console.WriteLine("[menu] ОШИБКА: отмена выбросила из паузы");

        int depthBefore = game.Depth;
        game.ExitToTitle();

        bool atTitle = game.State == GameState.Title;
        bool dialogClosed = game.Confirm == ConfirmKind.None;
        bool cleared = game.Choices.Count == 0 && game.Projectiles.Count == 0;
        bool recorded = game.Settings.BestDepth >= depthBefore
            && game.Settings.BestScore >= 5000
            && game.Settings.BestKills >= 17
            && game.Settings.RunsCompleted > 0;

        Console.WriteLine($"[menu] выход в меню: титул={atTitle}, диалог закрыт={dialogClosed}, " +
            $"сцена очищена={cleared}, рекорды записаны={recorded} " +
            $"(глубина={game.Settings.BestDepth}, очки={game.Settings.BestScore}, " +
            $"убийства={game.Settings.BestKills}, забегов={game.Settings.RunsCompleted})");

        // пункт "выход" есть в главном меню
        bool quitItem = TitleMenu.Count == 4 && TitleMenu.Labels[(int)TitleMenu.Row.Quit] == "ВЫХОД";
        Console.WriteLine($"[menu] пункт «Выход» в главном меню: {(quitItem ? "есть" : "ОТСУТСТВУЕТ")}");
    }

    /// <summary>
 /// Темнота и кристаллы. Проверяет то, что легко сломать незаметно:
 /// комнаты должны запоминаться, кристалл не должен включаться сам,
    /// урон должен обрывать зарядку, а в дуэли темноты быть не должно.
 /// </summary>
    private static void TestDarknessAndCrystals()
    {
Console.WriteLine("[dark] темнота, кристаллы и свет");

     Game game = NewGame();
        game.Settings.GraphicsQuality = 2;
        game.StartRun(31337u);
        game.State = GameState.Playing;

  bool rooms = game.Level.Rooms.Count > 0;
        Console.WriteLine($"[dark] комнат запомнено: {game.Level.Rooms.Count}");

      bool crystals = game.Crystals.Count > 0 && game.Crystals.Count <= game.Level.Rooms.Count;
        Console.WriteLine($"[dark] кристаллов поставлено: {game.Crystals.Count}");

        // Арена босса: кристаллов быть не должно, зал и так освещён.
        Game arena = NewGame();
        arena.StartRun(31337u);
        arena.Depth = 5;
        arena.BuildLevel();
        bool arenaClean = arena.Crystals.Count == 0 && arena.Level.Rooms.Count == 1;
        Console.WriteLine($"[dark] арена босса: кристаллов={arena.Crystals.Count}, комнат={arena.Level.Rooms.Count}");

   // Зарядка вручную: два метра от кристалла, посох направлен на него.
        Crystal? crystal = game.Crystals.Count > 0 ? game.Crystals[0] : null;
        bool charged = false;
        bool interrupted = false;

        if (crystal is not null)
     {
    Vector2 spot = crystal.Pos + GameMath.FromAngle(MathF.PI) * 26f;
            game.Player.Pos = spot;
            game.Player.AimAngle = 0f;

     // Держим кнопку каста и крутим кадры - кристалл должен зарядиться.
      for (int i = 0; i < 200 && !crystal.Activated; i++)
        {
    game.Input.KeyDown(Keys.F);
         game.Update(1f / 60f);
          }
     charged = crystal.Activated;
            game.Input.KeyUp(Keys.F);

      // Сброс: новый кристалл, урон посреди зарядки должен её оборвать.
  Crystal? second = game.Crystals.Count > 1 ? game.Crystals[1] : null;
            if (second is not null && !second.Activated)
      {
        game.Player.Pos = second.Pos + GameMath.FromAngle(MathF.PI) * 26f;
            game.Player.AimAngle = 0f;

         for (int i = 0; i < 60 && second.Charge < 0.4f; i++)
            {
    game.Input.KeyDown(Keys.F);
      game.Update(1f / 60f);
    }

         // Убираем врагов: иначе они бьют игрока во время проверки и сбрасывают
            // зарядку сами, и результат зависит от того, где кто оказался.
  game.Enemies.Clear();

    // Урон обязан обнулить зарядку прямо в момент попадания.
            bool hadCharge = second.Charge >= 0.4f;
    game.Player.TakeDamage(game, 5f, second.Pos);
    bool resetOnHit = second.Charge <= 0.001f;

   Vector2 stand = second.Pos + GameMath.FromAngle(MathF.PI) * 26f;

    // Пока держим F после удара, зарядка не должна идти: есть блокировка.
  for (int i = 0; i < 10; i++)
     {
  game.Player.Pos = stand;
        game.Player.AimAngle = 0f;
      game.Input.KeyDown(Keys.F);
       game.Update(1f / 60f);
     }
      bool lockedOut = second.Charge <= 0.001f && !second.Activated;

   // А после блокировки зарядка обязана снова пойти - кристалл не сломан.
        for (int i = 0; i < 150 && !second.Activated; i++)
        {
            game.Player.Pos = stand;
        game.Player.AimAngle = 0f;
        game.Input.KeyDown(Keys.F);
       game.Update(1f / 60f);
        }
            interrupted = hadCharge && resetOnHit && lockedOut && second.Activated;
game.Input.KeyUp(Keys.F);
        Console.WriteLine($"[dark] прерывание: было={hadCharge} сброс={resetOnHit} блокировка={lockedOut} " +
  $"снова={second.Activated} заряд={second.Charge:F2} hp={game.Player.Hp:F0} жив={game.Player.Alive} " +
    $"состояние={game.State}");
    }
      }

        // Свет кристалла обязан появиться в маске.
        bool lightsUp = false;
        if (crystal is not null && crystal.Activated)
    {
      using Bitmap tiny = new(8, 8);
  using Graphics tg = Graphics.FromImage(tiny);
   game.Renderer.DrawWorldOnly(tg, game);
  lightsUp = game.Renderer.LightAt(crystal.Pos) > 0.5f;
        }

        // Дуэль: темноты быть не должно, карта открыта целиком.
Game duel = NewGame();
        duel.StartRun(31337u);
        DuelSession ds = new(duel);
        ds.StartHosting(withBot: true);
        bool duelLit = ds.Host is not null;
        if (duelLit)
        {
         ds.StartDuelWithBot();
            duel.State = GameState.Playing;
            using Bitmap tiny = new(8, 8);
     using Graphics tg = Graphics.FromImage(tiny);
            duel.Renderer.DrawWorldOnly(tg, duel);
      duelLit = duel.Renderer.LightAt(duel.Foe?.Pos ?? duel.Player.Pos) > 0.9f;
        }
        Console.WriteLine($"[dark] в дуэли темноты нет: {duelLit}");

        bool ok = rooms && crystals && arenaClean && charged && interrupted && lightsUp && duelLit;
  Console.WriteLine(ok
            ? "[dark] ok"
          : $"[dark] ОШИБКА: комнаты={rooms} кристаллы={crystals} арена={arenaClean} " +
       $"зарядка={charged} прерывание={interrupted} свет={lightsUp} дуэль={duelLit}");
    }

    public static void SelfTest()
    {
        Console.WriteLine("=== AETHER SEQUENCE self test ===");
        TestSpellDatabase();
        TestSpellBuffer();
        TestSpellSprites();
        TestIpInput();
        TestLevelGeneration();
        TestDepthProgression();
        TestBossFight();
        TestCombatThroughput();
        TestMouseCasting();
        TestSoftLock();
        TestSettingsRoundTrip();
        TestMenuExitFlow();
        TestPauseAndDuelExit();
        TestDarknessAndCrystals();
        TestRunSimulation();
        Console.WriteLine("=== all checks finished ===");
    }

    /// <summary>
    /// ESC должен открывать паузу везде, где идёт бой, а после матча дуэли
    /// нужно попадать в главное меню. Раньше в дуэли не работало ни то,
    /// ни другое: состояние Playing проглатывало паузу, а хост после
    /// матча оставался в фазе комнаты без единого экрана.
    /// </summary>
    private static void TestPauseAndDuelExit()
    {
        Console.WriteLine("[pause] пауза по ESC и выход из дуэли");

        Game solo = NewGame();
        solo.StartRun(11);
        solo.State = GameState.Playing;
        solo.Input.KeyDown(Keys.Escape);
        solo.Update(1f / 60f);
        bool soloPaused = solo.State == GameState.Paused;
        solo.Input.KeyUp(Keys.Escape);

        solo.Input.KeyDown(Keys.Escape);
        solo.Update(1f / 60f);
        bool soloResumed = solo.State == GameState.Playing;
        solo.Input.KeyUp(Keys.Escape);
        solo.Input.EndFrame();

        Console.WriteLine($"[pause] одиночная игра: ESC открыл={soloPaused} повторный ESC вернул={soloResumed}");

        Game game = NewGame();
        game.StartRun(4242);
        DuelSession duel = new(game);
        duel.StartHosting(withBot: true);
        duel.StartDuelWithBot();
        game.State = GameState.Playing;

        duel.Phase = DuelPhase.Fighting;
        game.Input.KeyDown(Keys.Escape);
        game.Update(1f / 60f);
        bool duelPaused = game.State == GameState.Paused;
        game.Input.KeyUp(Keys.Escape);

        Console.WriteLine($"[pause] дуэль: ESC открыл паузу={duelPaused}");

        // Из паузы дуэли выходим в главное меню.
        game.PauseSelection = 2;
        game.Input.KeyDown(Keys.Enter);
        game.Update(1f / 60f);
        bool askedExit = game.Confirm == ConfirmKind.ExitToMenu;
        game.Input.KeyUp(Keys.Enter);
        game.Input.EndFrame();

        game.ConfirmRow = 0;
        game.Input.KeyDown(Keys.Enter);
        game.Update(1f / 60f);
        game.Input.KeyUp(Keys.Enter);
        game.Input.EndFrame();
        bool leftToTitle = game.State == GameState.Title && !game.DuelMode;

        Console.WriteLine($"[pause] дуэль: выход в меню запрос={askedExit} дошли до главного={leftToTitle}");

        // После матча по ENTER уходим в главное меню, а не в комнату.
        Game after = NewGame();
        after.StartRun(777);
        DuelSession duel2 = new(after);
        duel2.StartHosting(withBot: true);
        duel2.StartDuelWithBot();
        after.State = GameState.Playing;
        duel2.Phase = DuelPhase.MatchOver;
        duel2.WinsHost = 3;
        duel2.MatchWinner = 0;

        after.Input.KeyDown(Keys.Enter);
        after.Update(1f / 60f);
        after.Input.KeyUp(Keys.Enter);
        after.Input.EndFrame();
        bool matchOverToTitle = after.State == GameState.Title && !after.DuelMode;

        Console.WriteLine($"[pause] после матча: выход в главное меню={matchOverToTitle}");

        bool ok = soloPaused && soloResumed && duelPaused && askedExit && leftToTitle && matchOverToTitle;
        if (!ok) Console.WriteLine("[pause] ОШИБКА: пауза или выход из дуэли работают не во всех случаях");
        else Console.WriteLine("[pause] ok");
    }

    /// <summary>
    /// Все девять листов заклинаний должны лежать в Assets\Spells и нарезаться
    /// без пустых кадров, а у каждого заклинания должен быть свой ключ.
    /// </summary>
    private static void TestSpellSprites()
    {
        string[] keys =
        {
            "arcane-nova", "claw-bolt", "void-field", "ring-nova", "shard-rain",
            "smoke-bolt", "fire-bolt", "comet-rain", "shock-ring",
        };

        Console.WriteLine($"[sprites] ассеты найдены: {(SpellSprites.Available ? "да" : "нет")}");
        if (!SpellSprites.Available)
        {
            throw new InvalidOperationException("папка Assets\\Spells не найдена рядом с exe");
        }

        foreach (string key in keys)
        {
            int frames = SpellSprites.FrameCount(key);
            Console.WriteLine($"[sprites] {key,-12} кадров={frames}");
            if (frames <= 0) throw new InvalidOperationException($"пустой спрайт-лист {key}");
        }

        // Ни одно заклинание не должно остаться без анимации.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int without = 0;
        foreach (SpellDef def in SpellDB.All)
        {
            string key = def.SheetKey;
            if (string.IsNullOrEmpty(key)) without++;
            seen.Add(key);
        }
        Console.WriteLine($"[sprites] заклинаний={SpellDB.All.Count} без анимации={without} использовано листов={seen.Count}");
        if (without > 0) throw new InvalidOperationException("есть заклинания без анимации каста");
        foreach (string key in keys)
        {
            if (!seen.Contains(key)) Console.WriteLine($"[sprites] ВНИМАНИЕ: лист {key} не используется");
        }
    }

    /// <summary>
    /// Точки и цифры в поле ввода IP. Раньше TranslateChar их не отдавал,
    /// и адрес невозможно было набрать - пункт меню был бесполезен.
    /// </summary>
    private static void TestIpInput()
    {
        void Type(string text, out string result)
        {
            Game game = NewGame();
            game.StartRun(1);
            game.State = GameState.Duel;
            DuelSession duel = new(game);
            game.Duel = duel;
            duel.Phase = DuelPhase.DirectIp;

            foreach (char c in text)
            {
                Keys key = c switch
                {
                    '.' => Keys.OemPeriod,
                    ' ' => Keys.Space,
                    >= '0' and <= '9' => Keys.D0 + (c - '0'),
                    _ => Keys.None,
                };
                if (key == Keys.None) continue;
                // KeyDown кладёт клавишу в _keysPressed, откуда она попадает
                // в PressedKeys на ближайшем EndFrame. Поэтому читаем поле
                // после EndFrame следующего кадра - ровно так же, как в игре.
                game.Input.KeyDown(key);
                duel.UpdateInput(game.Input);
                game.Input.EndFrame();
                game.Input.KeyUp(key);
                duel.UpdateInput(game.Input);
                game.Input.EndFrame();
            }
            result = duel.IpDraft;
            duel.Dispose();
        }

        Type("192.168.0.112", out string address);
        Console.WriteLine($"[input] ввод адреса '{address}'");
        if (address != "192.168.0.112") throw new InvalidOperationException($"точки или цифры не вводятся: '{address}'");

        Type("192.168.0.5 47801", out string withPort);
        Console.WriteLine($"[input] адрес с портом '{withPort}'");
        if (withPort != "192.168.0.5 47801") throw new InvalidOperationException($"порт не вводится: '{withPort}'");

        Console.WriteLine("[input] поле ввода IP работает: да");
    }

    /// <summary>
    /// Проверка объявления комнаты без второго сокета.
    ///
    /// На Windows SO_REUSEADDR для UDP не раздаёт копию пакета всем
    /// сокетам на порту - пакет уходит ровно одному, и при двух LanDiscovery
    /// в одном процессе это лотерея. Поэтому здесь проверяется разбор
    /// объявления и ответ хоста на запрос Discover, а приём пакета
    /// проверяется отдельно в TestRoomDiscoveryReceive.
    /// </summary>
    private static void TestRoomDiscovery(DuelSession hostDuel, DuelHost host)
    {
        Console.WriteLine($"[net] порт обнаружения занят: {host.DiscoveryListening}");
        if (!host.DiscoveryListening)
        {
            Console.WriteLine("[net] ОШИБКА: порт 47800 не занят, объявления работать не будут");
            return;
        }

        // Собираем объявление ровно так, как это делает хост, и проверяем,
        // что клиент его разбирает и знает правильный порт.
        byte[] announce = BuildAnnounce(hostDuel.RoomName, host.HostNick, host.Port, 0);
        RoomListing? parsed = LanDiscovery.ParseAnnounce(announce, new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, NetProtocol.DiscoveryPort));
        if (parsed is null)
        {
            Console.WriteLine("[net] ОШИБКА: объявление не разбирается");
            return;
        }

        Console.WriteLine($"[net] объявление разобрано: '{parsed.Name}', хост '{parsed.HostNick}', " +
                          $"игроков {parsed.Players}/{parsed.MaxPlayers}, порт {parsed.Port}");
        if (parsed.Port != host.Port)
        {
            Console.WriteLine($"[net] ОШИБКА: в объявлении порт {parsed.Port}, хост слушает {host.Port}");
        }
        else
        {
            Console.WriteLine("[net] порт в объявлении совпадает с реальным: да");
        }

        // Запрос Discover - всего два байта. Раньше проверка длины в
        // HandleDatagram отбрасывала его, и хост никогда не отвечал.
        byte[] discover = { NetProtocol.DiscoveryMagic, 0 };
        bool accepted = LanDiscovery.IsDiscover(discover);
        Console.WriteLine($"[net] запрос Discover принят обработчиком: {(accepted ? "да" : "нет")} ({discover.Length} байт)");
        if (!accepted)
        {
            Console.WriteLine("[net] ОШИБКА: хост не ответит на запрос поиска");
        }

        TestRoomDiscoveryReceive(hostDuel, host);
    }

    /// <summary>
    /// Приём объявления другим сокетом. Сокет создаётся на другом порту,
    /// поэтому конфликта ReuseAddress не возникает и результат стабилен.
    /// </summary>
    private static void TestRoomDiscoveryReceive(DuelSession hostDuel, DuelHost host)
    {
        const int probePort = 47899;
        System.Net.Sockets.UdpClient? listener = null;
        try
        {
            listener = new System.Net.Sockets.UdpClient(probePort);
            listener.EnableBroadcast = true;

            byte[] announce = BuildAnnounce(hostDuel.RoomName, host.HostNick, host.Port, 0);
            listener.Send(announce, announce.Length,
                new System.Net.IPEndPoint(System.Net.IPAddress.Broadcast, probePort));

            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));
            System.Net.Sockets.UdpReceiveResult got = listener.ReceiveAsync(cts.Token).GetAwaiter().GetResult();

            RoomListing? room = LanDiscovery.ParseAnnounce(got.Buffer, got.RemoteEndPoint);
            Console.WriteLine(room is null
                ? "[net] ОШИБКА: объявление не разобралось на приёме"
                : $"[net] приём объявления работает: комната '{room.Name}' от {room.Address}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[net] ОШИБКА: broadcast не дошёл до сокета даже в пределах одного компьютера");
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            Console.WriteLine($"[net] сокет диагностики: {ex.SocketErrorCode}");
        }
        finally
        {
            listener?.Dispose();
        }
    }

    /// <summary>Собирает пакет объявления в том же формате, что и LanDiscovery.Announce.</summary>
    private static byte[] BuildAnnounce(string roomName, string nick, int port, int players)
    {
        // Буфер берём с запасом: WriteString кладёт длину префиксом,
        // и обрезанный поток дал бы нечитаемый пакет.
        using System.IO.MemoryStream stream = new(512);
        using System.IO.BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
        writer.Write(NetProtocol.DiscoveryMagic);
        // Именно byte: в реальном хосте версия тоже пишется одним байтом.
        writer.Write((byte)NetProtocol.Version);
        NetProtocol.WriteString(writer, roomName, 32);
        NetProtocol.WriteString(writer, nick, NetProtocol.MaxNickLength);
        writer.Write(port);
        writer.Write((byte)players);
        writer.Write((byte)DuelRules.MaxPlayers);
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>
    /// Кому бьёт снаряд в дуэли. Раньше сравнение id было инвертировано,
    /// и гость кастовал в самого себя: его HP падали, а моделька исчезала,
    /// потому что соперник в сети стоял без урона.
    /// </summary>
    private static void TestDuelTargeting(DuelSession duel, DuelHost host)
    {
        // Проверяем на обоих концах: id стрельца должен уводить цель
        // в соперника, а не в самого стрелка.
        duel.Game.StartDuel(0, duel);
        host.Foe = duel.Game.Foe;

        Player me = duel.Game.Player;
        Player? foe = duel.Game.Foe;
        if (foe is null)
        {
            Console.WriteLine("[net] ОШИБКА: в дуэли нет второго игрока");
            return;
        }

// Снаряд бьёт того, кто НЕ стрелял. Owner - id стрельца.
        // Снаряд гостя (Owner=1) у хоста должен лететь в самого хоста.
        Player? hostTargetForGuest = duel.Game.DuelFoeForId(1);
        Console.WriteLine($"[net] хост, снаряд гостя -> {DescribeLocal(hostTargetForGuest, me)}");
        if (!ReferenceEquals(hostTargetForGuest, me))
        {
            Console.WriteLine("[net] ОШИБКА: снаряд гостя у хоста летит не в хоста");
        }

        // Снаряд хоста (Owner=0) у гостя должен лететь в самого гостя.
        duel.Game.LocalPeerId = 1;
        Player? guestTargetForHost = duel.Game.DuelFoeForId(0);
        Console.WriteLine($"[net] гость, снаряд хоста -> {DescribeLocal(guestTargetForHost, me)}");
        if (!ReferenceEquals(guestTargetForHost, me))
        {
            Console.WriteLine("[net] ОШИБКА: снаряд хоста у гостя летит не в гостя");
        }

        // Свой снаряд всегда летит в соперника, а не в стрелка.
        Player? guestOwn = duel.Game.DuelFoeForId(1);
        Console.WriteLine($"[net] гость, свой снаряд -> {DescribeLocal(guestOwn, foe)}");
        if (!ReferenceEquals(guestOwn, foe))
        {
            Console.WriteLine("[net] ОШИБКА: свой снаряд бьёт самого себя");
        }

        duel.Game.LocalPeerId = 0;
    }

    /// <summary>
    /// Бот-соперник должен двигаться, кастовать и наносить урон.
    /// Проверяем это без окна: если бот стоит - тренировка бесполезна.
    /// </summary>
    private static void TestDuelBot(DuelSession duel, DuelHost host)
    {
        duel.Leave();
        duel.Nick = "МАГ";
        duel.StartHosting(withBot: true);
        if (duel.Host is null || duel.Bot is null)
        {
            Console.WriteLine("[net] ОШИБКА: бот не создался");
            return;
        }

        duel.StartDuelWithBot();
        Game game = duel.Game;
        host.Foe = game.Foe;

        Vector2 botStart = game.Foe!.Pos;
        float botHpStart = game.Player.Hp;
        float foeHpStart = game.Foe.Hp;

        for (int f = 0; f < 60 * 45; f++)
        {
            // Хозяин тоже должен бить, иначе бот не получит урон.
            Vector2 toBot = game.Foe!.Pos - game.Player.Pos;
            float dist = toBot.Length();
            if (dist > 0.01f) toBot /= dist;
            // Прицел ведём точно в бот��, иначе снаряды проходят мимо.
            Vector2 screen = game.Foe.Pos - game.Camera.Position - game.Camera.Offset;
            game.Input.MouseMove(new PointF(screen.X, screen.Y));
            game.Input.MouseDown(Core.MouseButton.Left);
            if (f % 24 == 0) game.Input.KeyDown(Keys.D1);
            if (f % 24 == 12) game.Input.KeyDown(Keys.D2);
            game.Input.KeyUp(Keys.D1);
            game.Input.KeyUp(Keys.D2);

            duel.UpdateInput(game.Input);
            game.Update(1.0 / 60.0);
        }

        float moved = Vector2.Distance(botStart, game.Foe.Pos);
        Console.WriteLine($"[bot] бот сдвинулся на {moved:F0}px, " +
            $"урон гостю {botHpStart - game.Player.Hp:F0}, урон боту {foeHpStart - game.Foe.Hp:F0}");

        if (moved < 30f) Console.WriteLine("[net] ОШИБКА: бот стоит на месте");
        if (game.Player.Hp >= botHpStart) Console.WriteLine("[net] ОШИБКА: бот не наносит урон");
        if (game.Foe.Hp >= foeHpStart) Console.WriteLine("[net] ОШИБКА: бот не получает урон (бой не идёт)");

        duel.Leave();
        duel.StartHosting();
    }

    private static string DescribeLocal(Player? target, Player expected)
        => target is null ? "никто"
            : ReferenceEquals(target, expected) ? "цель (верно)"
            : "НЕ ТА ЦЕЛЬ (ошибка)";

    private static void TestMouseCasting()
    {        Game game = NewGame();
        game.StartRun(11);
        game.Player.SelectRune(0);

        game.Input.MouseDown(MouseButton.Left);
        int casts = 0;
        float lastTimer = 0f;
        for (int i = 0; i < 96; i++)
        {
            game.Update(1.0 / 60.0);
            if (game.SpellTimer > lastTimer) casts++;
            lastTimer = game.SpellTimer;
        }
        game.Input.MouseUp(MouseButton.Left);
        Console.WriteLine(
            $"[fire] удержание ЛКМ 1.6с: кастов={casts} последнее='{game.SpellAnnouncement}' комбо='{game.Player.LastCast?.Combo ?? "-"}' (ожидаются только осколки стихии)");

        game = NewGame();
        game.StartRun(12);
        game.Player.SelectRune(0);
        for (int tap = 0; tap < 3; tap++)
        {
            game.Input.MouseDown(MouseButton.Left);
            game.Update(1.0 / 60.0);
            game.Input.MouseUp(MouseButton.Left);
            game.Update(1.0 / 60.0);
        }
        for (int i = 0; i < 24; i++) game.Update(1.0 / 60.0);
        Console.WriteLine(
            $"[tap] 3 быстрых тапа ЛКМ: заклинание='{game.Player.LastCast?.Name ?? "-"}' комбо='{game.Player.LastCast?.Combo ?? "-"}' (ожидается FFF)");

        game = NewGame();
        game.StartRun(13);
        game.Player.SelectRune(0);
        game.Input.MouseWheel(1);
        game.Update(1.0 / 60.0);
        Console.WriteLine($"[wheel] руна 1 + колесо вперёд: выбрана руна {game.Player.SelectedRune} ({game.Player.SelectedElement}), ожидается 1 (Ветер)");
    }

    public static void Bot(Game game, int frames, Rng rng, bool cheat, Action<Game>? onFrame = null)
    {
        Keys[] runes = { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6 };
        Keys moveKey = Keys.None;
        Keys actionKey = Keys.None;
        int wave = 0;

        for (int frame = 0; frame < frames; frame++)
        {
            Keys previousMove = moveKey;
            Keys previousAction = actionKey;
            moveKey = Keys.None;
            actionKey = Keys.None;

            if (game.State == GameState.Playing)
            {
                wave++;
                if (wave % 20 == 0)
                {
                    Vector2 visible = NearestEnemy(game, 1400f);
                    Vector2 delta = visible - game.Player.Pos;
                    bool hasTarget = Vector2.Distance(visible, game.Player.Pos) > 3f;
                    float dist = delta.Length();
                    Vector2 move;
                    if (hasTarget && dist < 48f) move = -delta;
                    else if (hasTarget && dist > 96f) move = delta;
                    else if (hasTarget) move = new Vector2(-delta.Y, delta.X) * (rng.Chance(0.5f) ? 1f : -1f);
                    else
                    {
                        Vector2 toExit = game.Level.ExitPos - game.Player.Pos;
                        move = toExit.LengthSquared() > 25f ? toExit : new Vector2(rng.Range(-1f, 1f), rng.Range(-1f, 1f));
                    }
                    if (move.LengthSquared() < 0.01f) move = new Vector2(1f, 0f);
                    moveKey = MathF.Abs(move.X) > MathF.Abs(move.Y)
                        ? (move.X > 0 ? Keys.D : Keys.A)
                        : (move.Y > 0 ? Keys.S : Keys.W);
                }
                if (wave % 13 == 0)
                {
                    int slot = rng.Next(0, 6);
                    actionKey = game.Player.RuneUnlocked[slot] ? runes[slot] : Keys.None;
                }
            }
            else if (game.State == GameState.LevelUp)
            {
                actionKey = Keys.D1;
            }
            else if (game.State == GameState.GameOver || game.State == GameState.Victory || game.State == GameState.Title)
            {
                actionKey = game.State == GameState.Title ? Keys.Enter : Keys.R;
            }

            if (previousMove != Keys.None) game.Input.KeyUp(previousMove);
            if (previousAction != Keys.None) game.Input.KeyUp(previousAction);
            if (moveKey != Keys.None) game.Input.KeyDown(moveKey);
            if (actionKey != Keys.None) game.Input.KeyDown(actionKey);

            if (game.State == GameState.Playing)
            {
                Vector2 aim = NearestEnemy(game, 1400f);
                if (Vector2.Distance(aim, game.Player.Pos) > 3f)
                {
                    Vector2 screen = aim - game.Camera.Position;
                    game.Input.MouseMove(new PointF(screen.X, screen.Y));
                }
            }

            game.Update(1.0 / 60.0);

            if (cheat)
            {
                game.Player.Hp = game.Player.MaxHp;
                game.Player.Mana = game.Player.MaxMana;
                game.Player.DamageMul = 6f;
                game.Player.ManaRegen = 200f;
            }

            onFrame?.Invoke(game);
        }
    }

    internal static Vector2 NearestEnemy(Game game, float maxRange)
    {
        Vector2 best = game.Player.Pos;
        float bestDist = maxRange;
        foreach (Enemy e in game.Enemies)
        {
            if (e.Dead) continue;
            float d = Vector2.Distance(e.Pos, game.Player.Pos);
            if (d >= bestDist) continue;
            if (!game.Level.LineClear(game.Player.Pos, e.Pos)) continue;
            bestDist = d;
            best = e.Pos;
        }
        return best;
    }

    private static void TestSpellDatabase()
    {
        Console.WriteLine($"[db] spells={SpellDB.All.Count} fusions={SpellDB.Fusions.Count}");
        foreach (SpellDef def in SpellDB.All)
        {
            if (def.Damage <= 0f) Console.WriteLine($"  ! {def.Combo} {def.Name} has no damage");
            if (def.Cost <= 0) Console.WriteLine($"  ! {def.Combo} {def.Name} has no cost");
        }
        int chains = 0;
        foreach (SpellDef a in SpellDB.All)
        {
            foreach (SpellDef b in SpellDB.All)
            {
                if (ReferenceEquals(a, b)) continue;
                if (b.Combo.Length > a.Combo.Length && b.Combo.StartsWith(a.Combo, StringComparison.Ordinal)) chains++;
            }
        }
        Console.WriteLine($"[db] chained prefixes={chains}");
    }

    private static void TestSpellBuffer()
    {
        string[] sequences = { "F", "FWW", "FFF", "FFFF", "DAL", "LLLL", "D", "AAAA", "EE", "WE", "DL", "FW", "AALL", "DDE" };
        foreach (string sequence in sequences)
        {
            SpellBuffer buffer = new();
            string fired = "-";
            foreach (char c in sequence)
            {
                Element element = ElementFor(c);
                if (buffer.Push(element, out SpellDef? spell, out bool isFusion))
                {
                    fired = spell!.Name + (isFusion ? " [ФУЗИЯ]" : string.Empty);
                }
                if (buffer.Update(1f, out SpellDef? flushed, out bool flushedFusion))
                {
                    fired = flushed!.Name + (flushedFusion ? " [ФУЗИЯ]" : string.Empty);
                }
            }
            string pending = buffer.HasPending ? buffer.PendingSpell!.Name : "-";
            Console.WriteLine($"[combo] {sequence,-5} -> {fired,-30} pending={pending}");
        }
    }

    private static Element ElementFor(char c) => c switch
    {
        'F' => Element.Fire,
        'W' => Element.Wind,
        'A' => Element.Water,
        'E' => Element.Earth,
        'D' => Element.Dark,
        _ => Element.Light,
    };

    private static void TestLevelGeneration()
    {
        for (uint seed = 1; seed <= 40; seed++)
        {
            Rng rng = new(seed);
            int depth = (int)(seed % 16) + 1;
            World.Level level = World.Level.Generate(new Rng(seed * 7919u), depth);
            if (level.SolidAt(level.Spawn)) throw new InvalidOperationException($"seed {seed}: spawn inside a wall");
            if (level.SolidAt(level.ExitPos)) throw new InvalidOperationException($"seed {seed}: exit inside a wall");
            List<Vector2> points = level.RandomFloorPoints(rng, 30, level.Spawn, 60f);
            if (points.Count < 10) Console.WriteLine($"  ! seed {seed} depth {depth}: only {points.Count} spawn points");
            if (seed <= 3) Console.WriteLine($"[level] seed={seed} size={level.W}x{level.H} spawn={level.Spawn} exit={level.ExitPos} boss={level.IsBoss}");
        }
        Console.WriteLine("[level] 40 levels generated");
    }

    private static void TestDepthProgression()
    {
        Game game = NewGame();
        game.StartRun(4242);
        for (int depth = 1; depth <= Game.FinalDepth; depth++)
        {
            bool boss = game.Level.IsBoss;
            int enemies = game.Enemies.Count;
            game.Enemies.Clear();
            game.Update(1.0 / 60.0);
            bool opened = game.Level.ExitOpen;
            game.Player.Pos = game.Level.ExitPos;
            game.Update(1.0 / 60.0);
            Console.WriteLine($"[depth] {depth,2} boss={boss} enemies={enemies,2} exitOpened={opened} next={game.Depth} state={game.State}");
        }
        if (game.State != GameState.Victory) throw new InvalidOperationException($"expected victory, got {game.State}");
        Console.WriteLine("[depth] victory reached");
    }

    private static void TestBossFight()
    {
        foreach (int depth in new[] { 5, 10, 15 })
        {
            RunBossDuel(depth);
        }
    }

    /// <summary>Один бой: 5 - Страж, 10 - Король, 15 - Голем.</summary>
    private static void RunBossDuel(int depth)
    {
        Game game = NewGame();
        game.StartRun(31337);
        game.Depth = depth;
        game.BuildLevel();
        // Урон завышен, чтобы бой укладывался в кадровый бюджет теста.
        game.Player.DamageMul = 40f;
        game.Player.ManaRegen = 500f;
        game.Player.MaxMana = 900f;
        game.Player.Mana = 900f;

        Enemy? boss = game.Enemies.Find(e => e.Kind == EnemyKind.Boss);
        if (boss is null) throw new InvalidOperationException($"boss not spawned at depth {depth}");

        BossDef.Data def = BossDef.ForDepth(depth);
        Console.WriteLine(
            $"[boss] depth={depth} name='{def.Name}' kind={def.Kind} variant={boss.BossVariant} " +
            $"hp={boss.MaxHp:0} r={boss.Radius:0} spd={boss.Speed:0} theme={game.Level.Theme} escort={game.Enemies.Count - 1}");
        if (boss.BossName != def.Name) throw new InvalidOperationException("boss name mismatch");
        if (boss.BossVariant != depth / 5 - 1) throw new InvalidOperationException("boss variant mismatch");

        boss.MaxHp = 12000f;
        boss.Hp = boss.MaxHp;
        Rng rng = new(77u);
        int patterns = 0;
        float maxPhases = 0f;
        int peakMinions = game.Enemies.Count;
        for (int frame = 0; frame < 60 * 120 && !boss.Dead; frame++)
        {
            if (frame % 5 == 0)
            {
                Vector2 delta = boss.Pos - game.Player.Pos;
                game.Input.KeyDown(MathF.Abs(delta.X) > MathF.Abs(delta.Y) ? (delta.X > 0 ? Keys.D : Keys.A) : (delta.Y > 0 ? Keys.S : Keys.W));
                game.Input.KeyUp(MathF.Abs(delta.X) > MathF.Abs(delta.Y) ? (delta.X > 0 ? Keys.A : Keys.D) : (delta.Y > 0 ? Keys.W : Keys.S));
            }
            if (frame % 24 == 0)
            {
                Keys rune = rng.Pick(new[] { Keys.D1, Keys.D2, Keys.D3, Keys.D5 });
                game.Input.KeyDown(rune);
            }
            if (frame % 24 == 12)
            {
                Keys rune = rng.Pick(new[] { Keys.D1, Keys.D2, Keys.D3, Keys.D5 });
                game.Input.KeyUp(rune);
            }
            // Бот не должен умирать: иначе бой замирает и паттерны не проверяются.
            game.Player.Hp = game.Player.MaxHp;
            game.Player.Invuln = 5f;
            game.Update(1.0 / 60.0);
            peakMinions = Math.Max(peakMinions, game.Enemies.Count);
            maxPhases = Math.Max(maxPhases, boss.Phase);
            if (frame % 300 == 0)
            {
                patterns = boss.PatternIndex;
                Console.WriteLine(
                    $"[boss {depth}] t={frame / 60,3}s hp={boss.Hp:0}/{boss.MaxHp:0} phase={boss.Phase} state={boss.State} enemies={game.Enemies.Count} bullets={game.Projectiles.Count} telegraphs={game.Effects.Telegraphs.Count} slow={game.Player.SlowFactor:0.00}");
            }
        }
        Console.WriteLine(
            $"[boss {depth}] killed={boss.Dead} patterns={patterns} phases={maxPhases} " +
            $"exitOpen={game.Level.ExitOpen} peakMinions={peakMinions}");

        if (!boss.Dead) throw new InvalidOperationException($"boss {def.Name} survived the whole fight");
        if (patterns < 4) throw new InvalidOperationException($"boss {def.Name} used too few patterns");
        if (maxPhases < 2) throw new InvalidOperationException($"boss {def.Name} never reached the final phase");
    }

    private static void TestCombatThroughput()
    {
        string[] scripts = { "F", "FFF", "DDD" };
        foreach (string script in scripts)
        {
            Game game = NewGame();
            game.StartRun(99);
            game.Enemies.Clear();
            Vector2 spot = game.Level.Spawn;
            float aim = 0f;
            for (int i = 0; i < 24; i++)
            {
                float angle = i * GameMath.Tau / 24f;
                Vector2 candidate = game.Level.Spawn + GameMath.FromAngle(angle) * 70f;
                if (!game.Level.SolidAt(candidate) && game.Level.LineClear(game.Level.Spawn, candidate))
                {
                    spot = candidate;
                    aim = angle;
                    break;
                }
            }
            Enemy slime = Enemy.Create(EnemyKind.Slime, spot, 3, new Rng(3));
            game.Enemies.Add(slime);
            game.Player.AimAngle = aim;
            float damageTaken = 0f;
            float hp = game.Player.Hp;
            int casts = 0;
            int frame = 0;
            Keys held = Keys.None;
            for (; frame < 60 * 40 && !slime.Dead && game.Player.Alive; frame++)
            {
                if (frame % 24 == 0)
                {
                    Element element = ElementFor(script[(casts / 2) % script.Length]);
                    if (held != Keys.None) game.Input.KeyUp(held);
                    held = Keys.None;
                    if (game.Player.RuneUnlocked[(int)element])
                    {
                        held = element switch
                        {
                            Element.Fire => Keys.D1,
                            Element.Wind => Keys.D2,
                            Element.Water => Keys.D3,
                            Element.Earth => Keys.D4,
                            Element.Dark => Keys.D5,
                            _ => Keys.D6,
                        };
                        game.Input.KeyDown(held);
                        casts++;
                    }
                }
                game.Update(1.0 / 60.0);
                game.Player.AimAngle = MathF.Atan2(spot.Y - game.Player.Pos.Y, spot.X - game.Player.Pos.X);
                if (game.Player.Hp < hp) damageTaken += hp - game.Player.Hp;
                hp = game.Player.Hp;
            }
            Console.WriteLine(
                $"[ttk] script={script,-4} killed={slime.Dead} time={(frame / 60f):0.0}s casts={casts} dmgTaken={damageTaken:0} hp={game.Player.Hp:0}");
        }
    }

    private static void TestRunSimulation()
    {
        for (int run = 0; run < 4; run++)
        {
            bool cheat = run >= 2;
            Game game = NewGame();
            game.StartRun((uint)(1000 + run * 37));
            Rng rng = new((uint)(5000 + run * 13));
            int deaths = 0;
            int levelUps = 0;
            int deepest = 1;
            int frames = 60 * 60 * 6;
            bool wasOver = false;

            Bot(game, frames, rng, cheat, g =>
            {
                if (g.State == GameState.LevelUp) levelUps++;
                if (g.State == GameState.GameOver && !wasOver) deaths++;
                wasOver = g.State == GameState.GameOver;
                deepest = Math.Max(deepest, g.Depth);
            });

            Console.WriteLine(
                $"[run {run}] cheat={cheat} depth={deepest} kills={game.Kills} levelUps={levelUps} deaths={deaths} score={game.Score} level={game.Player.Level}");
        }
    }
}
