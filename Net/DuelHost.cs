using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AetherSequence.Core;
using AetherSequence.Entities;

namespace AetherSequence.Net;

/// <summary>
/// Хост дуэли: держит TCP-слушатель, принимает игроков, рассылает снапшоты.
/// Вся симуляция считается здесь, клиент лишь присылает ввод.
/// </summary>
internal sealed class DuelHost : IDisposable
{
    private TcpListener? _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<DuelPeer> _peers = new();
    private readonly object _gate = new();
    private readonly string _roomName;
    private LanDiscovery? _discovery;

    public string HostNick = "ХОСТ";
    public string Status = string.Empty;
    public bool Running { get; private set; }
    public int Port { get; private set; } = NetProtocol.GamePort;
    public bool MatchLive { get; set; }

    /// <summary>Ввод локального игрока (хоста), который он шлёт второму.</summary>
    public RemoteInputState LocalInput;

    /// <summary>Второй игрок симулируется на хосте.</summary>
    public Player? Foe = null;

    public event Action<byte, string>? PlayerJoined;
    public event Action<byte>? PlayerLeft;

    public IReadOnlyList<DuelPeer> Peers
    {
        get { lock (_gate) return _peers.ToList(); }
    }

    public DuelPeer? Local
    {
        get { lock (_gate) return _peers.Find(p => p.IsLocal); }
    }

    public DuelHost(string roomName, string hostNick)
    {
        _roomName = roomName;
        HostNick = hostNick;
        Start();
    }

    public void Start()
    {
        if (Running) return;
        Running = true;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            int port = NetProtocol.GamePort + attempt;
            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                Port = port;
                break;
            }
            catch (SocketException)
            {
                if (attempt == 11)
                {
                    Running = false;
                    Status = "не удалось занять порт";
                    return;
                }
            }
        }

        lock (_gate)
        {
            _peers.Clear();
            _peers.Add(new DuelPeer { Id = 0, Nick = HostNick, IsLocal = true, IsHost = true });
        }

        _discovery = new LanDiscovery(HostNick, Port);
        _discovery.AnnounceRequested += Announce;

        _ = Task.Run(AcceptLoopAsync);
    }

    /// <summary>Занят ли порт обнаружения - иначе поиск комнат работать не будет.</summary>
    public bool DiscoveryListening => _discovery?.Listening ?? false;

    /// <summary>Добавить бота в слот гостя, чтобы играть без второго компьютера.</summary>
    public void AddBot(string nick)
    {
        lock (_gate)
        {
            _peers.RemoveAll(p => !p.IsLocal);
            _peers.Add(new DuelPeer { Id = 1, Nick = nick, IsLocal = false, IsHost = false, IsBot = true });
        }
        PlayerJoined?.Invoke(1, nick);
    }

    /// <summary>Подставить ввод бота вместо сетевого пакета гостя.</summary>
    public void SetBotInput(RemoteInputState state)
    {
        lock (_gate)
        {
            DuelPeer? bot = _peers.Find(p => p.IsBot);
            if (bot is null) return;
            bot.Input = state;
            bot.LastSeen = Environment.TickCount64;
        }
    }

    public void Announce(IPEndPoint? directReply = null)
    {
        lock (_gate)
        {
            int count = _peers.Count(p => p.Connected);
            _discovery?.Announce(_roomName, count, directReply);
        }
    }

    private void AnnounceLoop()
    {
        CancellationToken token = _cts.Token;
        while (!token.IsCancellationRequested)
        {
            Announce();
            try { Task.Delay(1500, token).Wait(); } catch (OperationCanceledException) { return; }
        }
    }

    private async Task AcceptLoopAsync()
    {
        CancellationToken token = _cts.Token;
        _ = Task.Run(AnnounceLoop);
        TcpListener? listener = _listener;
        if (listener is null) return;
        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient incoming = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                _ = Task.Run(() => HandleIncoming(incoming, token));
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (SocketException) { }
    }

    private async Task HandleIncoming(TcpClient tcp, CancellationToken token)
    {
        DuelPeer? pending = null;
        NetChannel? channelRef = null;
        try
        {
            NetChannel channel = new(tcp, (kind, body) =>
            {
                switch (kind)
                {
                    case MsgKind.Hello:
                        using (MemoryStream stream = new(body, writable: false))
                        using (BinaryReader reader = new(stream, Encoding.UTF8))
                        {
                            int version = reader.ReadInt32();
                            string nick = NetProtocol.ReadString(reader, NetProtocol.MaxNickLength);
                            if (version != NetProtocol.Version)
                            {
                                channelRef?.Send(MsgKind.Reject, w => NetProtocol.WriteString(w, "версия игры не совпадает", 64));
                                return;
                            }
                            lock (_gate)
                            {
                                if (_peers.Count(p => p.Connected) >= DuelRules.MaxPlayers)
                                {
                                    channelRef?.Send(MsgKind.Reject, w => NetProtocol.WriteString(w, "комната заполнена", 64));
                                    return;
                                }
                                pending = new DuelPeer
                                {
                                    Id = (byte)_peers.Count,
                                    Nick = nick,
                                    IsLocal = false,
                                    LastSeen = Environment.TickCount64,
                                };
                                _peers.Add(pending);
                                if (channelRef is not null) _channels[pending.Id] = channelRef;
                            }
                            channelRef?.Send(MsgKind.Welcome, w =>
                            {
                                List<DuelPeer> roster;
                                lock (_gate) roster = _peers.ToList();
                                w.Write(pending.Id);
                                w.Write(NetProtocol.Version);
                                w.Write((byte)roster.Count);
                                foreach (DuelPeer peer in roster)
                                {
                                    w.Write(peer.Id);
                                    NetProtocol.WriteString(w, peer.Nick, NetProtocol.MaxNickLength);
                                    w.Write(peer.IsHost);
                                }
                            });
                            PlayerJoined?.Invoke(pending.Id, nick);
                        }
                        break;

                    case MsgKind.InputCmd:
                        if (pending is null) return;
                        using (MemoryStream stream = new(body, writable: false))
                        using (BinaryReader reader = new(stream, Encoding.UTF8))
                        {
                            reader.ReadInt32();
                            RemoteInputState state = new()
                            {
                                Down = reader.ReadUInt32(),
                                Pressed = reader.ReadUInt32(),
                                MouseX = reader.ReadSingle(),
                                MouseY = reader.ReadSingle(),
                                Any = true,
                            };
                            lock (_gate) { pending.Input = state; pending.LastSeen = Environment.TickCount64; }
                        }
                        break;

                    case MsgKind.Ping:
                        channelRef?.Send(MsgKind.Pong, _ => { });
                        break;
                }
            }, autoStart: false);

            // channelRef уже присвоен - теперь можно принимать сообщения
            channelRef = channel;
            channel.Start();

            while (!token.IsCancellationRequested && channel.IsAlive)
            {
                await Task.Delay(200, token).ConfigureAwait(false);
                if (pending is null) break;
                lock (_gate)
                {
                    if (!pending.Connected) break;
                    if (Environment.TickCount64 - pending.LastSeen > 8000)
                    {
                        pending.Connected = false;
                        break;
                    }
                }
            }

            if (pending is not null)
            {
                bool removed;
                lock (_gate)
                {
                    pending.Connected = false;
                    removed = _peers.Remove(pending);
                }
                channel.Dispose();
                _channels.TryRemove(pending.Id, out _);
                if (removed) PlayerLeft?.Invoke(pending.Id);
            }
            else
            {
                channelRef?.Dispose();
            }
        }
        catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException or InvalidDataException)
        {
            Status = e.Message;
            channelRef?.Dispose();
            if (pending is not null)
            {
                lock (_gate) { pending.Connected = false; _peers.Remove(pending); }
                _channels.TryRemove(pending.Id, out _);
                PlayerLeft?.Invoke(pending.Id);
            }
        }
    }

    public void Broadcast(MsgKind kind, Action<BinaryWriter> fill)
    {
        lock (_gate)
        {
            foreach (DuelPeer peer in _peers)
            {
                if (peer.IsLocal || !peer.Connected) continue;
                GetChannel(peer.Id)?.Send(kind, fill);
            }
        }
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<byte, NetChannel> _channels = new();

    private NetChannel? GetChannel(byte id) => _channels.TryGetValue(id, out NetChannel? channel) ? channel : null;

    public void RegisterChannel(byte id, NetChannel channel) => _channels[id] = channel;

    /// <summary>Подставить ввод игрока вручную (для тестов и симуляции).</summary>
    public void InjectInput(byte id, RemoteInputState state)
    {
        lock (_gate)
        {
            DuelPeer? peer = _peers.Find(p => p.Id == id);
            if (peer is null) return;
            peer.Input = state;
            peer.LastSeen = Environment.TickCount64;
        }
    }

    public RemoteInputState GetInput(byte id)
    {
        lock (_gate)
        {
            DuelPeer? peer = _peers.Find(p => p.Id == id);
            return peer?.Input ?? default;
        }
    }

    /// <summary>Разослать снапшот обоих игроков и снарядов подключённым.</summary>
    public void BroadcastSnapshot(Game game)
    {
        Broadcast(MsgKind.Snapshot, w => NetProtocol.WriteSnapshot(w, game));
    }

    public void ResetInputs()
    {
        lock (_gate)
        {
            foreach (DuelPeer peer in _peers) peer.Input.Reset();
        }
    }

    public void Dispose()
    {
        Running = false;
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        try { _listener?.Stop(); } catch (SocketException) { }
        foreach (KeyValuePair<byte, NetChannel> pair in _channels) pair.Value.Dispose();
        _channels.Clear();
        _discovery?.Dispose();
        try { _cts.Dispose(); } catch (ObjectDisposedException) { }
    }
}
