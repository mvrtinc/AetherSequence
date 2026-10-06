using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AetherSequence.Net;

/// <summary>
/// Клиент дуэли: подключается к комнате по адресу из списка, отправляет свой ввод,
/// принимает снапшоты и слушает отказы и завершение матча.
/// </summary>
internal sealed class DuelClient : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private NetChannel? _channel;
    private LanDiscovery? _discovery;

    public string Nick = "ИГРОК";
    public string Status = string.Empty;
    public bool Connected { get; private set; }
    public byte LocalId { get; private set; } = 1;

    public readonly List<(byte Id, string Nick, bool IsHost)> Peers = new();

    /// <summary>
    /// Классы пиров из Welcome, по индексу совпадают с Peers. Отдельный
 /// список, а не поле в кортеже: кортеж разбирается в трёх местах
    /// (DuelSession, DevTools), и менять его ради одного числа дороже,
 /// чем держать список рядом.
    /// </summary>
    public readonly List<byte> PeerClasses = new();

    /// <summary>Класс, которым играет этот клиент. Уходит в Hello.</summary>
    public byte PlayerClass;

    public readonly List<PeerSnapshot> Snapshots = new();
    public readonly List<ProjectileSnapshot> Projectiles = new();
    public readonly List<byte> Effects = new();

    public int RoundWinner = -1;
    public int MatchWinner = -1;
    public uint MatchSeed;
    public int RoundNumber = 1;

    public event Action<string>? Rejected;
    public event Action? PeerListChanged;
    public event Action? SnapshotArrived;
    public event Action? RoundFinished;
    public event Action? MatchFinished;
    public event Action? StartRequested;

    public IReadOnlyList<RoomListing> Rooms => _discovery?.Rooms ?? Array.Empty<RoomListing>();

    public DuelClient(string nick) => Nick = nick;

    public void StartDiscovery()
    {
        _discovery?.Dispose();
        _discovery = new LanDiscovery(Nick, NetProtocol.GamePort);
    }

    public void RefreshRooms() => _discovery?.Discover();

    public bool Connect(RoomListing room)
    {
        Disconnect();
        try
        {
            TcpClient tcp = new();
            IAsyncResult ar = tcp.BeginConnect(room.Address, room.Port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(2500))
            {
                tcp.Close();
                Status = "нет ответа от комнаты";
                return false;
            }
            tcp.EndConnect(ar);
            tcp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);

            _channel = new NetChannel(tcp, OnMessage);
            _channel.Send(MsgKind.Hello, w =>
            {
          w.Write(NetProtocol.Version);
    NetProtocol.WriteString(w, Nick, NetProtocol.MaxNickLength);

   // Класс идёт сразу после ника: хост обязан знать его до старта матча,
       // иначе он создаст соперника как мага.
          w.Write(PlayerClass);
   });
            Connected = true;
            Status = string.Empty;
            return true;
        }
        catch (SocketException)
        {
            Status = "не удалось подключиться";
            return false;
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            Status = "соединение оборвано";
            return false;
        }
    }

    private void OnMessage(MsgKind kind, byte[] body)
    {
        try
        {
            using MemoryStream stream = new(body, writable: false);
            using BinaryReader reader = new(stream, Encoding.UTF8);
            switch (kind)
            {
                case MsgKind.Welcome:
                    LocalId = reader.ReadByte();
                    int version = reader.ReadInt32();
                    if (version != NetProtocol.Version) throw new InvalidDataException("version");
      Peers.Clear();
            PeerClasses.Clear();
         int count = reader.ReadByte();
          for (int i = 0; i < count; i++)
{
    byte id = reader.ReadByte();
          string nick = NetProtocol.ReadString(reader, NetProtocol.MaxNickLength);
          bool isHost = reader.ReadBoolean();
     byte cls = reader.ReadByte();
  Peers.Add((id, nick, isHost));
      PeerClasses.Add(cls);
      }
                    PeerListChanged?.Invoke();
                    break;

                case MsgKind.Reject:
                    string reason = NetProtocol.ReadString(reader, 64);
                    Disconnect();
                    Rejected?.Invoke(reason);
                    break;

                case MsgKind.StartMatch:
                    reader.ReadByte();
                    MatchSeed = reader.ReadUInt32();
                    RoundNumber = reader.ReadInt32();
                    Snapshots.Clear();
                    Projectiles.Clear();
                    RoundWinner = -1;
                    MatchWinner = -1;
                    PeerListChanged?.Invoke();
                    StartRequested?.Invoke();
                    break;

                case MsgKind.InputCmd:
                    break;

                case MsgKind.Snapshot:
                    ReadSnapshot(reader);
                    SnapshotArrived?.Invoke();
                    break;

                case MsgKind.RoundEnd:
                    RoundWinner = reader.ReadByte();
                    RoundNumber = reader.ReadInt32();
                    RoundFinished?.Invoke();
                    break;

                case MsgKind.MatchEnd:
                    MatchWinner = reader.ReadByte();
                    MatchFinished?.Invoke();
                    break;

                case MsgKind.Pong:
                    break;
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or EndOfStreamException)
        {
            Disconnect();
            Status = "соединение потеряно";
        }
    }

    /// <summary>
    /// Разбор снапшота. Метод internal, а не private: --nettest раньше
    /// вообще не вызывал его, потому что пробный клиент уничтожался до
    /// старта матча. Из-за этого изменение формата снапшота проходило
    /// незамеченным, и рассинхрон WritePeer/ReadSnapshot был бы найден
 /// только двумя людьми на двух компьютерах.
    /// </summary>
    internal void ReadSnapshot(BinaryReader reader)
    {
        Snapshots.Clear();
        int tick = reader.ReadInt32();
        int players = reader.ReadByte();
        for (int i = 0; i < players; i++)
        {
            PeerSnapshot snapshot = new()
            {
                Id = reader.ReadByte(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Hp = reader.ReadSingle(),
                MaxHp = reader.ReadSingle(),
                Mana = reader.ReadSingle(),
                Aim = reader.ReadSingle(),
                Alive = reader.ReadBoolean(),
                Dashing = reader.ReadBoolean(),
                Rune = reader.ReadByte(),
                HitFlash = reader.ReadSingle(),
   Flow = reader.ReadByte(),
      PlayerClass = reader.ReadByte(),
        };
            Snapshots.Add(snapshot);
        }

        Projectiles.Clear();
        int shots = reader.ReadByte();
        for (int i = 0; i < shots; i++)
        {
            Projectiles.Add(new ProjectileSnapshot
            {
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Vx = reader.ReadSingle(),
                Vy = reader.ReadSingle(),
                Radius = reader.ReadSingle(),
                Damage = reader.ReadSingle(),
                Element = reader.ReadByte(),
                Style = reader.ReadByte(),
                Hostile = reader.ReadBoolean(),
                Owner = reader.ReadByte(),
                Life = reader.ReadSingle(),
                Size = reader.ReadSingle(),
                Length = reader.ReadSingle(),
                ColorArgb = reader.ReadInt32(),
            });
        }
        LastTick = tick;
    }

    public int LastTick { get; private set; }

    public void SendInput(int tick, uint down, uint pressed, float mouseX, float mouseY)
    {
        if (!Connected) return;
        _channel?.Send(MsgKind.InputCmd, w =>
        {
            w.Write(tick);
            w.Write(down);
            w.Write(pressed);
            w.Write(mouseX);
            w.Write(mouseY);
        });
    }

    public void Disconnect()
    {
        Connected = false;
        _channel?.Dispose();
        _channel = null;
        Peers.Clear();
        Snapshots.Clear();
        Projectiles.Clear();
    }

    public void Dispose()
    {
        Disconnect();
        _discovery?.Dispose();
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        try { _cts.Dispose(); } catch (ObjectDisposedException) { }
    }
}
