using System.Drawing;
using System.Numerics;
using System.Threading;
using AetherSequence.Audio;
using AetherSequence.Core;
using AetherSequence.Entities;
using AetherSequence.Net;

namespace AetherSequence;

/// <summary>
/// Сессия дуэли. Есть два режима: хост (считает обеих игроков) и гость
/// (только рисует присланные снапшоты). Общая логика меню, раундов и HUD.
/// </summary>
internal enum DuelPhase
{
    Main,
    Nickname,
    DirectIp,
    Hosting,
    Browsing,
    InRoom,
    Loading,
    Fighting,
    RoundOver,
    MatchOver,
}

internal sealed class DuelSession : IDisposable
{
    private readonly Game _game;

    /// <summary>Игра, которой принадлежит эта сессия.</summary>
    public Game Game => _game;

    public DuelPhase Phase = DuelPhase.Main;
    public int Row;
    public string Nick = "МАГ";
    public string NickDraft = string.Empty;
    public bool EditingNick = false;
    public string RoomName = "ПЕЩЕРА";

    /// <summary>Ручной ввод адреса хоста - запасной путь, если broadcast заблокирован.</summary>
    public string IpDraft = string.Empty;
    public string IpStatus = string.Empty;

    /// <summary>Мигание курсора в поле никнейма.</summary>
    public float Caret;
    public string Status = string.Empty;
    public float StatusTimer;

    public DuelHost? Host;
    public DuelClient? Client;
    public LanDiscovery? Discovery;

    public bool IsHost => Host is not null;

    public int RoundNumber = 1;
    public int WinsHost;
    public int WinsGuest;
    public int RoundWinner = -1;
    public int MatchWinner = -1;
    public float RoundEndTimer;
    public float CountdownTimer = 3f;

    public byte LocalId;
    public string HostNick = string.Empty;
    public string GuestNick = string.Empty;

    /// <summary>Класс, которым играет локальный игрок. Задаётся из настроек.</summary>
    public PlayerClass MyClass = PlayerClass.Mage;

    /// <summary>Класс соперника: у хоста - с его пира, у гостя - из присланного списка.</summary>
    public PlayerClass OpponentClass = PlayerClass.Mage;

    /// <summary>Класс бота в тренировке. Рандомизируется при старте матча.</summary>
    public PlayerClass BotClass = PlayerClass.Archer;

    public readonly List<RoomListing> VisibleRooms = new();
    public int RoomRow;
    public float DiscoverTimer;

    /// <summary>Снапшоты для отрисовки: у хоста — живые игроки, у гостя — присланные.</summary>
    public void CollectHostState()
    {
        _renderPlayers.Clear();
        _renderPlayers.Add(SnapshotOf(0, _game.Player));
        if (_game.Foe is not null) _renderPlayers.Add(SnapshotOf(1, _game.Foe));
        _renderShots.Clear();
        foreach (AetherSequence.Combat.Projectile pr in _game.Projectiles)
        {
            _renderShots.Add(new ProjectileSnapshot
            {
                X = pr.Pos.X,
                Y = pr.Pos.Y,
                Vx = pr.Vel.X,
                Vy = pr.Vel.Y,
                Radius = pr.Radius,
                Damage = pr.Damage,
                Element = (byte)pr.Element,
                Style = (byte)pr.Style,
                Hostile = pr.Hostile,
                Owner = (byte)Math.Max(0, pr.Owner),
                Life = pr.Life,
                Size = pr.Size,
                Length = pr.Length,
                ColorArgb = pr.Color.ToArgb(),
            });
        }
    }

    private static PeerSnapshot SnapshotOf(byte id, Player p) => new()
    {
        Id = id,
        X = p.Pos.X,
        Y = p.Pos.Y,
        Hp = p.Hp,
        MaxHp = p.MaxHp,
        Mana = p.Mana,
        Aim = p.AimAngle,
        Alive = p.Alive,
        Dashing = p.Dashing,
        Rune = (byte)GameMath.ClampI(p.SelectedRune, 0, 5),
        HitFlash = p.HitFlash,
        Flow = GameMath.ClampI((int)(p.Flow * 10f), 0, 10),
        PlayerClass = (byte)CharacterClasses.Clamp((int)p.Class),
    };

    private float _snapAccum;
    private int _tick;
    private readonly List<(PeerSnapshot A, PeerSnapshot B, float Time)> _snapBuffer = new();
    private readonly List<PeerSnapshot> _renderPlayers = new();
    private readonly List<ProjectileSnapshot> _renderShots = new();

    public IReadOnlyList<PeerSnapshot> RenderPlayers => _renderPlayers;
    public IReadOnlyList<ProjectileSnapshot> RenderShots => _renderShots;

    public DuelSession(Game game)
    {
        _game = game;
        Nick = Sanitize(Environment.UserName);
    }

    public static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "МАГ";
        string trimmed = value.Trim();
        if (trimmed.Length > NetProtocol.MaxNickLength) trimmed = trimmed[..NetProtocol.MaxNickLength];
        foreach (char c in trimmed)
        {
            if (char.IsControl(c)) return "МАГ";
        }
        return trimmed.ToUpperInvariant();
    }

    public void Tick(float dt)
    {
        Caret += dt * 5f;
        if (StatusTimer > 0f)
        {
            StatusTimer -= dt;
            if (StatusTimer <= 0f) Status = string.Empty;
        }

        switch (Phase)
        {
            case DuelPhase.Browsing:
                DiscoverTimer -= dt;
                if (DiscoverTimer <= 0f)
                {
                    DiscoverTimer = 1f;
                    Client?.RefreshRooms();
                    RefreshRooms();
                }
                break;

            case DuelPhase.Fighting:
                TickFighting(dt);
                break;

            case DuelPhase.RoundOver:
                // Показываем итог раунда, потом идёт следующий раунд
                // либо матч, если уже выиграны три победы.
                RoundEndTimer -= dt;
                if (RoundEndTimer <= 0f) AfterRoundOver();
                break;

            case DuelPhase.MatchOver:
                // Матч окончен: ждём Enter или ESC от игрока.
                break;
        }
    }

    private void RefreshRooms()
    {
        VisibleRooms.Clear();
        if (Client is null) return;
        foreach (RoomListing room in Client.Rooms)
        {
            if (room.Players < room.MaxPlayers) VisibleRooms.Add(room);
        }
        VisibleRooms.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        if (RoomRow >= VisibleRooms.Count) RoomRow = Math.Max(0, VisibleRooms.Count - 1);
    }

    /// <summary>Комнаты, найденные в сети. Нужно для диагностики и тестов.</summary>
    public IReadOnlyList<RoomListing> FoundRooms => Client?.Rooms ?? Array.Empty<RoomListing>();

    /// <summary>Запросить объявления у всех хостов в сети.</summary>
    public void AskForRooms() => Client?.RefreshRooms();

    private void TickFighting(float dt)
    {
        _tick++;
        _snapAccum += dt;

        bool hostSide = Host is not null;
        SendLocalInput();
        DriveBot(dt);

        bool sendNow = _snapAccum >= 1f / 30f;
        if (sendNow) _snapAccum = 0f;

        if (hostSide)
        {
            CollectHostState();
            if (sendNow) Host?.BroadcastSnapshot(_game);
        }
        else
        {
            Interpolate(dt);
        }
    }

    /// <summary>Команды бота, если игра идёт против него.</summary>
    private void DriveBot(float dt)
    {
        DuelBot? bot = Bot;
        if (bot is null || Host is null || _game.Foe is null) return;

        RemoteInputState state = bot.Think(_game.Foe.Pos, _game.Player.Pos, _game.ViewSize, _tick);
        Host.SetBotInput(state);
    }

    private void SendLocalInput()
    {
        Input input = _game.Input;
        uint down = 0u;
        uint pressed = 0u;
        for (int i = 0; i < InputActions.Count; i++)
        {
            InputAction action = (InputAction)i;
            if (input.Down(action)) down |= 1u << i;
            if (input.Pressed(action)) pressed |= 1u << i;
        }
        PointF mouse = input.Mouse;

        if (Host is not null)
        {
            Host.LocalInput = new RemoteInputState
            {
                Down = down,
                Pressed = pressed,
                MouseX = mouse.X,
                MouseY = mouse.Y,
                Any = true,
            };
        }
        else
        {
            Client?.SendInput(_tick, down, pressed, mouse.X, mouse.Y);
        }
    }

    private void Interpolate(float dt)
    {
        DuelClient? client = Client;
        if (client is null) return;

        _renderPlayers.Clear();
        _renderPlayers.AddRange(client.Snapshots);
        _renderShots.Clear();
        _renderShots.AddRange(client.Projectiles);
    }

    public void StartHosting(bool withBot = false, float botSkill = 0.7f)
    {
    Leave();
    MyClass = _game.Settings.PlayerClass;
        Host = new DuelHost(RoomName, Nick) { HostClass = (int)MyClass };
    Discovery = null;
        HostNick = Nick;
        GuestNick = string.Empty;
        LocalId = 0;
        RoundNumber = 1;
        WinsHost = 0;
 WinsGuest = 0;
        MatchWinner = -1;

        // Бот живёт в слоте гостя: отдельный компьютер не нужен. Класс бота
        // случайный: заодно это единственный способ посмотреть на оба
  // класса в дуэли без второй машины.
        if (withBot)
  {
            Bot = new Net.DuelBot(0x51Du, "БОТ", botSkill);
       BotClass = CharacterClasses.Next(MyClass, 1);
    Host.AddBot(Bot.Nick, (int)BotClass);
      GuestNick = Bot.Nick;
            OpponentClass = BotClass;
      }

        Phase = DuelPhase.InRoom;
        SetStatus(withBot
            ? $"тренировка с ботом «{Bot!.Nick}» - жмите СТАРТ"
          : $"комната «{RoomName}» открыта, ждём соперника");
    }

    /// <summary>Бот-соперник, если игра идёт против него.</summary>
    public Net.DuelBot? Bot;

    public void StartBrowsing()
    {
 Leave();
   MyClass = _game.Settings.PlayerClass;
        Client = new DuelClient(Nick) { PlayerClass = (byte)MyClass };
        Client.Rejected += reason =>
        {
            Phase = DuelPhase.Browsing;
            SetStatus($"отказ: {reason}", 5f);
        };
        Client.StartRequested += () =>
        {
            LocalId = Client.LocalId;
       for (int i = 0; i < Client.Peers.Count && i < Client.PeerClasses.Count; i++)
      {
                (byte id, string nick, bool isHost) = Client.Peers[i];
   PlayerClass cls = CharacterClasses.Clamp(Client.PeerClasses[i]);
        if (isHost)
       {
             HostNick = nick;
        OpponentClass = cls;
       }
        else GuestNick = nick;
      }
 BeginMatch();
        };
        Client.RoundFinished += () =>
        {
            RoundNumber = Client.RoundNumber;
            if (Client.RoundWinner == 0) WinsHost++;
            else if (Client.RoundWinner == 1) WinsGuest++;
            if (WinsHost >= DuelRules.WinsRequired || WinsGuest >= DuelRules.WinsRequired)
            {
                MatchWinner = WinsHost >= DuelRules.WinsRequired ? 0 : 1;
                Phase = DuelPhase.MatchOver;
                return;
            }

            // Новый раунд: поднимаем обоих. ResetDuelRound дергает только
            // хост, а гость обязан сделать это сам - иначе он остаётся
            // с нулевым здоровьем и не может участвовать.
            _game.ResetDuelRound();
        };
        Client.MatchFinished += () => MatchWinner = Client.MatchWinner;
        Client.StartDiscovery();
        Discovery = null;
        VisibleRooms.Clear();
        RoomRow = 0;
        DiscoverTimer = 0f;
        Phase = DuelPhase.Browsing;
        SetStatus("ищем комнаты в сети...");
    }

    public bool JoinSelected()
    {
        if (Client is null || RoomRow < 0 || RoomRow >= VisibleRooms.Count) return false;
        RoomListing room = VisibleRooms[RoomRow];
        if (!Client.Connect(room))
        {
            SetStatus(Client.Status.Length > 0 ? Client.Status : "не удалось подключиться", 4f);
            return false;
        }
        Phase = DuelPhase.Loading;
        SetStatus("подключение...");
        return true;
    }

    /// <summary>Подключиться напрямую по адресу (когда обнаружение недоступно).</summary>
    public bool JoinAddress(string address, int port)
    {
        if (Client is null) StartBrowsing();
        if (Client is null) return false;
        if (!System.Net.IPAddress.TryParse(address, out System.Net.IPAddress? ip))
        {
            SetStatus("неверный адрес", 3f);
            return false;
        }

        if (!Client.Connect(new RoomListing
        {
            Name = RoomName,
            HostNick = Nick,
            Address = ip,
            Port = port,
            Players = 1,
            MaxPlayers = DuelRules.MaxPlayers,
        }))
        {
            SetStatus(Client.Status.Length > 0 ? Client.Status : "не удалось подключиться", 4f);
            return false;
        }
        Phase = DuelPhase.Loading;
        SetStatus("подключение...");
        return true;
    }

    /// <summary>Гость получил приглашение начать матч.</summary>
    public void BeginMatch()
    {
        if (IsHost) return;
        _game.StartDuel(LocalId, this);
        Phase = DuelPhase.Fighting;
        CountdownTimer = 3f;
        RoundEndTimer = 0f;
        _snapBuffer.Clear();
    }

    public void BeginRound()
    {
        if (IsHost) _game.ResetDuelRound();
        Phase = DuelPhase.Fighting;
        CountdownTimer = 3f;
        RoundEndTimer = 0f;
        if (Client is not null) Client.RoundNumber = RoundNumber;
    }

    public void EndRound(int winnerId)
    {
        // Проверка смерти вызывается каждый кадр, в том числе на экране
        // итогов. Без этой защиты счёт прыгал с 1:0 сразу на 5:0,
        // матч "заканчивался" сам по себе, и игра зависала намертво.
        if (Phase != DuelPhase.Fighting) return;

        RoundWinner = winnerId;
        if (winnerId == 0) WinsHost++;
        else if (winnerId == 1) WinsGuest++;

        // Сначала всегда показываем итог раунда. Раньше при третьей победе
        // MatchOver ставился сразу же и игрок вообще не видел результат.
        Phase = DuelPhase.RoundOver;
        RoundEndTimer = DuelRules.RoundResetDelay;
        RoundNumber++;

        Host?.Broadcast(MsgKind.RoundEnd, w =>
        {
            w.Write((byte)winnerId);
            w.Write(RoundNumber);
        });
        Client?.RoundNumber = RoundNumber;
    }

    /// <summary>
    /// Вызывается, когда истекло время показа итога раунда: либо начинаем
    /// следующий раунд, либо матч уже выигран.
    /// </summary>
    private void AfterRoundOver()
    {
        if (WinsHost >= DuelRules.WinsRequired || WinsGuest >= DuelRules.WinsRequired)
        {
            MatchWinner = WinsHost >= DuelRules.WinsRequired ? 0 : 1;
            Phase = DuelPhase.MatchOver;
            Host?.Broadcast(MsgKind.MatchEnd, w => w.Write((byte)MatchWinner));
            return;
        }

        BeginRound();
    }

    public void Leave()
    {
        Host?.Dispose();
        Host = null;
        Client?.Dispose();
        Client = null;
        Discovery = null;
        VisibleRooms.Clear();
        _snapBuffer.Clear();
        _renderPlayers.Clear();
        _renderShots.Clear();
        Phase = DuelPhase.Main;
        Row = 0;
    }

    /// <summary>Обработка клавиатуры и мыши во всех меню дуэли.</summary>
    public void UpdateInput(Input input)
    {
        if (Phase == DuelPhase.Fighting)
        {
            if (input.RawPressed(Keys.Escape))
            {
                SetStatus("выход из дуэли", 2f);
                _game.ExitDuel();
            }
            return;
        }

        if (Phase == DuelPhase.Nickname)
        {
            UpdateNickname(input);
            return;
        }

        if (Phase == DuelPhase.DirectIp)
        {
            UpdateDirectIp(input);
            return;
        }

        switch (Phase)
        {
            case DuelPhase.Main: UpdateMainMenu(input); break;
            case DuelPhase.Hosting:
            case DuelPhase.InRoom: UpdateRoom(input); break;
            case DuelPhase.Browsing: UpdateBrowsing(input); break;
            case DuelPhase.Loading: UpdateLoading(input); break;
            case DuelPhase.RoundOver: UpdateRoundOver(input); break;
            case DuelPhase.MatchOver: UpdateMatchOver(input); break;
        }
    }

    /// <summary>
    /// Ручное подключение по IP. Работает там, где не работает поиск комнат:
    /// брандмауэр или изоляция клиентов режут широковещательные пакеты,
    /// а прямое соединение по адресу проходит.
    /// </summary>
    private void UpdateDirectIp(Input input)
    {
        foreach (Keys key in input.PressedKeys)
        {
            if (key == Keys.Back)
            {
                if (IpDraft.Length > 0) IpDraft = IpDraft[..^1];
            }
            else if (key == Keys.Enter)
            {
                if (IpDraft.Length > 0) TryDirectConnect();
                return;
            }
            else if (key == Keys.Escape)
            {
                Phase = DuelPhase.Main;
                IpStatus = string.Empty;
                return;
            }
            else
            {
                char c = Input.TranslateChar(key);
                if (c == '\0') continue;
                if (char.IsDigit(c) || c == '.')
                {
                    // Лимит 20: адрес занимает 15 символов ("255.255.255.255"),
                    // а с портом и пробелом выходит 21 - режем с запасом.
                    if (IpDraft.Length < 20) IpDraft += c;
                }
                else if (c == ' ')
                {
                    // Пробел разделяет адрес и порт: "192.168.1.5 47801"
                    if (IpDraft.Length > 0 && !IpDraft.EndsWith(' ')) IpDraft += ' ';
                }
            }
        }
    }

    private void TryDirectConnect()
    {
        string text = IpDraft.Trim();
        string host = text;
        int port = NetProtocol.GamePort;

        int space = text.IndexOf(' ');
        if (space > 0)
        {
            host = text[..space];
            if (!int.TryParse(text[(space + 1)..].Trim(), out port)) port = NetProtocol.GamePort;
        }

        Nick = Sanitize(Nick.Length > 0 ? Nick : "МАГ");
        IpStatus = $"подключение к {host}:{port}...";
        AudioSystem.Play(Sfx.UiSelect, 0.5f);

        if (JoinAddress(host, port))
        {
            IpDraft = string.Empty;
            IpStatus = string.Empty;
            return;
        }

        IpStatus = "не удалось. Проверь адрес, Wi-Fi и брандмауэр";
    }

    private void UpdateNickname(Input input)
    {
        foreach (Keys key in input.PressedKeys)
        {
            if (key == Keys.Back)
            {
                if (NickDraft.Length > 0) NickDraft = NickDraft[..^1];
            }
            else if (key == Keys.Enter)
            {
                CommitNick();
                return;
            }
            else if (key == Keys.Escape)
            {
                NickDraft = Nick.Length > 0 ? Nick : "МАГ";
                Phase = DuelPhase.Main;
                return;
            }
            else
            {
                char c = Input.TranslateChar(key);
                if (c != '\0' && NickDraft.Length < NetProtocol.MaxNickLength) NickDraft += c;
            }
        }
        if (NickDraft.Length > NetProtocol.MaxNickLength) NickDraft = NickDraft[..NetProtocol.MaxNickLength];
    }

    private void CommitNick()
    {
        Nick = Sanitize(NickDraft);
        NickDraft = Nick;
        Phase = DuelPhase.Main;
        AudioSystem.Play(Sfx.UiSelect, 0.5f);
    }

    private void UpdateMainMenu(Input input)
    {
        switch (Row)
        {
            case 0:
                if (Confirm(input)) { Nick = Sanitize(Nick); Phase = DuelPhase.Hosting; StartHosting(); }
                break;
            case 1:
                if (Confirm(input)) StartBrowsing();
                break;
            case 2:
                if (Confirm(input)) { Nick = Sanitize(Nick); Phase = DuelPhase.Nickname; NickDraft = Nick; Nick = string.Empty; }
                break;
            case 3:
                if (Confirm(input)) { Nick = Sanitize(Nick); Phase = DuelPhase.DirectIp; IpDraft = string.Empty; IpStatus = string.Empty; }
                break;
            case 4:
                // Тренировка: бот в слоте гостя, второй компьютер не нужен.
                if (Confirm(input)) { Nick = Sanitize(Nick); StartHosting(withBot: true); }
                break;
        }

        Navigate(input, 5);
        if (input.RawPressed(Keys.Escape)) _game.ExitDuel();
    }

    /// <summary>Начать матч против бота, минуя меню.</summary>
    public void StartDuelWithBot()
    {
        if (Host is null || Bot is null) return;
        LocalId = 0;
        _game.StartDuel(0, this);
        Host.Foe = _game.Foe;
        Phase = DuelPhase.Fighting;
        CountdownTimer = 3f;
        RoundEndTimer = 0f;
        _snapBuffer.Clear();
    }

    private void UpdateRoom(Input input)
    {
        bool full = Host is not null && Host.Peers.Count >= DuelRules.MaxPlayers;
        int guestId = -1;
        if (Host is not null)
        {
            foreach (DuelPeer peer in Host.Peers)
            {
                if (!peer.IsLocal) guestId = peer.Id;
            }
        }

    if (Host is not null && guestId >= 0)
        {
        foreach (DuelPeer peer in Host.Peers)
      {
   if (peer.Id != guestId) continue;
    GuestNick = peer.Nick;

        // Класс гостя приехал ещё в Hello, поэтому он известен с момента
       // подключения, а не с первого снапшота.
        OpponentClass = CharacterClasses.Clamp(peer.PlayerClass);
            }
        }

        if (Confirm(input) && full && Host is not null)
        {
            LocalId = 0;
            _game.StartDuel(LocalId, this);
            Host.Foe = _game.Foe;
            Host.Broadcast(MsgKind.StartMatch, w =>
            {
                w.Write((byte)0);
                w.Write(_game.Seed);
                w.Write(1);
            });
            Phase = DuelPhase.Fighting;
            CountdownTimer = 3f;
            RoundEndTimer = 0f;
            _snapBuffer.Clear();
        }

        Navigate(input, full ? 2 : 1);
        if (input.RawPressed(Keys.Escape)) LeaveRoomAndBack();
    }

    private void UpdateBrowsing(Input input)
    {
        if (VisibleRooms.Count > 0)
        {
            if (input.RawPressed(Keys.Up)) { RoomRow = GameMath.ClampI(RoomRow - 1, 0, VisibleRooms.Count - 1); AudioSystem.Play(Sfx.UiMove, 0.3f); }
            if (input.RawPressed(Keys.Down)) { RoomRow = GameMath.ClampI(RoomRow + 1, 0, VisibleRooms.Count - 1); AudioSystem.Play(Sfx.UiMove, 0.3f); }
        }

        if (Confirm(input) && VisibleRooms.Count > 0) JoinSelected();

        if (input.RawPressed(Keys.Escape)) LeaveRoomAndBack();
    }

    private void UpdateLoading(Input input)
    {
        if (Client is null || Client.Peers.Count == 0) return;
    LocalId = Client.LocalId;
        for (int i = 0; i < Client.Peers.Count && i < Client.PeerClasses.Count; i++)
        {
     (byte id, string nick, bool isHost) = Client.Peers[i];
      PlayerClass cls = CharacterClasses.Clamp(Client.PeerClasses[i]);
            if (isHost)
            {
                HostNick = nick;
         OpponentClass = cls;
  }
     else GuestNick = nick;
        }
        BeginMatch();
    }

    private void UpdateRoundOver(Input input)
    {
        if (input.RawPressed(Keys.Escape)) LeaveRoomAndBack();
    }

    private void UpdateMatchOver(Input input)
    {
        // После матча уходим в главное меню - и хост, и гость.
        // Раньше хост возвращался в комнату (Phase = InRoom) при State ==
        // Playing: меню комнаты рисуется только в State == Duel, поэтому
        // он оставался без единого экрана и без выхода.
        if (input.RawPressed(Keys.Enter) || input.RawPressed(Keys.Escape))
        {
            LeaveRoomAndBack();
        }
    }

    private void LeaveRoomAndBack()
    {
        Leave();
        _game.ExitDuel();
    }

    private static bool Confirm(Input input) => input.RawPressed(Keys.Enter) || input.Pressed(InputAction.Confirm);

    private void Navigate(Input input, int count)
    {
        if (input.RawPressed(Keys.Up) || input.RawPressed(Keys.W))
        {
            Row = GameMath.ClampI(Row - 1, 0, Math.Max(0, count - 1));
            AudioSystem.Play(Sfx.UiMove, 0.3f);
        }
        if (input.RawPressed(Keys.Down) || input.RawPressed(Keys.S))
        {
            Row = GameMath.ClampI(Row + 1, 0, Math.Max(0, count - 1));
            AudioSystem.Play(Sfx.UiMove, 0.3f);
        }
    }

    public void SetStatus(string text, float seconds = 3f)
    {
        Status = text;
        StatusTimer = seconds;
    }

    /// <summary>Локальный хост ищет комнату: подключается к самому себе через loopback.</summary>
    public bool HostLoopback(out string error)
    {
        error = string.Empty;
        if (Host is null || !Host.Running)
        {
            error = "хост не запущен";
            return false;
        }

        DuelClient probe = new(Nick + "_ПРОБА");
        bool ok = probe.Connect(new Net.RoomListing
        {
            Name = RoomName,
            HostNick = Nick,
            Address = System.Net.IPAddress.Loopback,
            Port = Host.Port,
            Players = 1,
            MaxPlayers = DuelRules.MaxPlayers,
        });

        if (!ok)
        {
            error = probe.Status.Length > 0 ? probe.Status : "подключение не удалось";
            probe.Dispose();
            return false;
        }

        // ждём, пока хост обработает приветствие и выдаст Welcome
        for (int i = 0; i < 40 && probe.Peers.Count == 0; i++) Thread.Sleep(25);
        bool welcomed = probe.Peers.Count > 0;
        probe.Dispose();

        if (!welcomed)
        {
            error = "хост не ответил";
            return false;
        }
        return true;
    }

    /// <summary>Сколько игроков сейчас в комнате (считая хоста).</summary>
    public int PeerCount => Host?.Peers.Count ?? 0;

    public string PeerNick(byte id)
    {
        if (Host is null) return string.Empty;
        foreach (DuelPeer peer in Host.Peers)
        {
            if (peer.Id == id) return peer.Nick;
        }
        return string.Empty;
    }

    public byte GuestId
    {
        get
        {
            if (Host is null) return 0;
            foreach (DuelPeer peer in Host.Peers)
            {
                if (!peer.IsLocal) return peer.Id;
            }
            return 0;
        }
    }

    public void Dispose() => Leave();
}
