using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AetherSequence.Net;

/// <summary>
/// Обнаружение комнат в локальной сети: хост периодически рассылает UDP-объявление
/// по широковещательному адресу, клиенты слушают тот же порт и собирают список.
/// Дополнительно клиент шлёт запрос Discover, чтобы хост ответил немедленно.
/// </summary>
internal sealed class LanDiscovery : IDisposable
{
    private readonly string _nick;
    private readonly int _port;
    private readonly UdpClient _socket;
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<string, RoomListing> _rooms = new();
    private readonly object _gate = new();

    /// <summary>Удалось ли занять порт обнаружения. false - значит поиск не заработает.</summary>
    public bool Listening { get; private set; }

    public LanDiscovery(string nick, int gamePort)
    {
        _nick = nick;
        _port = gamePort;
        _socket = new UdpClient();
        _socket.EnableBroadcast = true;

        // ReuseAddress задаётся ДО Bind, иначе на Windows он не действует
        // и второй экземпляр не сможет занять тот же порт.
        try
        {
            _socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.Client.Bind(new IPEndPoint(IPAddress.Any, NetProtocol.DiscoveryPort));
            Listening = true;
        }
        catch (SocketException)
        {
            // порт занят другим экземпляром игры - обойдёмся без объявлений
            Listening = false;
        }
        _ = Task.Run(ReceiveLoopAsync);
    }

    public IReadOnlyList<RoomListing> Rooms
    {
        get
        {
            lock (_gate)
            {
                List<string> stale = _rooms.Where(pair => !pair.Value.IsFresh).Select(pair => pair.Key).ToList();
                foreach (string key in stale) _rooms.Remove(key);
                return _rooms.Values.ToList();
            }
        }
    }

    /// <param name="directReply">
    /// Адрес, которому нужно ответить точечно. Широковещание доходит не
    /// везде (роутеры, Public-сети, виртуальные адаптеры), а прямой ответ
    /// по адресу отправителя работает всегда, поэтому хост шлёт и туда.
    /// </param>
    public void Announce(string roomName, int players, IPEndPoint? directReply = null)
    {
        try
        {
            // Буфер с запасом: WriteString пишет длину префиксом, и при
            // кириллическом нике объявление в 96 байт обрезалось - клиент
            // не мог его прочитать. 512 - заведомо достаточно.
            using MemoryStream stream = new(512);
            using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
            writer.Write(NetProtocol.DiscoveryMagic);
            writer.Write((byte)NetProtocol.Version);
            NetProtocol.WriteString(writer, roomName, 32);
            NetProtocol.WriteString(writer, _nick, NetProtocol.MaxNickLength);
            // Пишем реальный порт хоста, а не константу: если 47801 занят,
            // хост поднялся на 47802, и клиент должен знать куда стучаться.
            writer.Write(_port);
            writer.Write((byte)players);
            writer.Write((byte)DuelRules.MaxPlayers);
            writer.Flush();

            byte[] data = stream.ToArray();
            foreach (IPAddress target in BroadcastTargets())
            {
                try { _socket.Send(data, data.Length, new IPEndPoint(target, NetProtocol.DiscoveryPort)); }
                catch (SocketException) { }
            }

            // Прямой ответ: адрес отправителя известен точно, subnet broadcast
            // мог уйти не туда. Порт ответа - тот же, что и у запроса.
            if (directReply is not null && directReply.Address.AddressFamily == AddressFamily.InterNetwork)
            {
                try
                {
                    _socket.Send(data, data.Length, new IPEndPoint(directReply.Address, NetProtocol.DiscoveryPort));
                }
                catch (SocketException) { }
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    public void Discover()
    {
        try
        {
            using MemoryStream stream = new(8);
            using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
            writer.Write(NetProtocol.DiscoveryMagic);
            writer.Write((byte)0);
            writer.Flush();
            byte[] data = stream.ToArray();
            foreach (IPAddress target in BroadcastTargets())
            {
                try { _socket.Send(data, data.Length, new IPEndPoint(target, NetProtocol.DiscoveryPort)); }
                catch (SocketException) { }
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    private IEnumerable<IPAddress> BroadcastTargets()
    {
        foreach (IPAddress address in LocalBroadcasts())
        {
            yield return address;
        }
        yield return IPAddress.Broadcast;
    }

    private static IEnumerable<IPAddress> LocalBroadcasts()
    {
        List<IPAddress> result = new();
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (UnicastIPAddressInformation unicast in nic.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    byte[] bytes = unicast.Address.GetAddressBytes();
                    if (bytes.Length != 4 || bytes[0] == 127) continue;
                    bytes[3] = 255;
                    result.Add(new IPAddress(bytes));
                }
            }
        }
        catch (NetworkInformationException) { }
        return result;
    }

    private async Task ReceiveLoopAsync()
    {
        CancellationToken token = _cts.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult received = await _socket.ReceiveAsync(token).ConfigureAwait(false);
                HandleDatagram(received.Buffer, received.RemoteEndPoint);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (SocketException) { }
    }

    /// <summary>
    /// Пакет является запросом Discover? Таких байт всего два - magic и версия 0.
    /// Проверка на три байта здесь ломала бы поиск полностью.
    /// </summary>
    public static bool IsDiscover(byte[] data)
        => data.Length >= 2 && data[0] == NetProtocol.DiscoveryMagic && data[1] == 0;

    /// <summary>Разбирает пакет объявления комнаты. null - если это не объявление.</summary>
    public static RoomListing? ParseAnnounce(byte[] data, IPEndPoint from)
    {
        if (data.Length < 3 || data[0] != NetProtocol.DiscoveryMagic) return null;
        int version = data[1];
        if (version == 0 || version != NetProtocol.Version) return null;

        try
        {
            using MemoryStream stream = new(data, writable: false);
            using BinaryReader reader = new(stream, System.Text.Encoding.UTF8);
            reader.ReadByte();
            reader.ReadByte();
            string roomName = NetProtocol.ReadString(reader, 32);
            string hostNick = NetProtocol.ReadString(reader, NetProtocol.MaxNickLength);
            int port = reader.ReadInt32();
            int players = reader.ReadByte();
            int maxPlayers = reader.ReadByte();
            if (roomName.Length == 0) return null;

            return new RoomListing
            {
                Name = roomName,
                HostNick = hostNick,
                Port = port,
                Players = players,
                MaxPlayers = maxPlayers,
                ProtocolVersion = (byte)version,
                Address = from.Address,
                LastSeen = Environment.TickCount64,
            };
        }
        catch (IOException)
        {
            // IOException покрывает и EndOfStreamException при обрыве пакета.
            return null;
        }
        catch (ProtocolViolationException)
        {
            // Пакет разбирается как строка не той длины - это чужой или
            // повреждённый пакет. Молча игнорируем.
            return null;
        }
    }

    private void HandleDatagram(byte[] data, IPEndPoint from)
    {
        // Запрос Discover - это всего два байта (magic + версия 0),
        // поэтому проверять минимум три байта здесь нельзя.
        if (data.Length < 2 || data[0] != NetProtocol.DiscoveryMagic) return;

        if (IsDiscover(data))
        {
            // Отвечаем объявлением и широковещательно, и точечно отправителю -
            // на его адрес. Широковещание доходит не везде, а прямой ответ всегда.
            AnnounceRequested?.Invoke(from);
            return;
        }

        RoomListing? parsed = ParseAnnounce(data, from);
        if (parsed is null) return;

        string key = from.Address.ToString() + ":" + parsed.Port;
        lock (_gate)
        {
            if (!_rooms.TryGetValue(key, out RoomListing? room))
            {
                room = new RoomListing { Address = from.Address };
                _rooms[key] = room;
            }
            room.Name = parsed.Name;
            room.HostNick = parsed.HostNick;
            room.Port = parsed.Port;
            room.Players = parsed.Players;
            room.MaxPlayers = parsed.MaxPlayers;
            room.ProtocolVersion = parsed.ProtocolVersion;
            room.LastSeen = Environment.TickCount64;
        }
    }

    public event Action<IPEndPoint>? AnnounceRequested;

    public void Dispose()
    {
        _cts.Cancel();
        try { _socket.Dispose(); } catch (ObjectDisposedException) { }
        _cts.Dispose();
    }
}
