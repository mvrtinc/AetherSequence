namespace AetherSequence.Net;

/// <summary>Комната, найденная в локальной сети через UDP-обнаружение.</summary>
internal sealed class RoomListing
{
    public string Name = "КОМНАТА";
    public string HostNick = string.Empty;
    public System.Net.IPAddress Address = System.Net.IPAddress.Loopback;
    public int Port = NetProtocol.GamePort;
    public int Players;
    public int MaxPlayers = 2;
    public byte ProtocolVersion = (byte)NetProtocol.Version;
    public long LastSeen = Environment.TickCount64;

    public bool IsFresh => Environment.TickCount64 - LastSeen < 4000;
}

/// <summary>Игрок в комнате: локальный или подключённый.</summary>
internal sealed class DuelPeer
{
    public byte Id;
    public string Nick = "ИГРОК";
    public bool IsLocal;
    public bool IsHost;
    public bool Connected = true;
    public long LastSeen = Environment.TickCount64;

    /// <summary>Последний принятый пакет ввода (для удалённых игроков).</summary>
    public RemoteInputState Input = new();

    /// <summary>Бот: ввод не приходит из сети, его подставляет хост.</summary>
    public bool IsBot;
}

internal struct RemoteInputState
{
    public uint Down;
    public uint Pressed;
    public float MouseX;
    public float MouseY;
    public bool Any;

    public void Reset()
    {
        Down = 0u;
        Pressed = 0u;
        MouseX = 320f;
        MouseY = 180f;
        Any = false;
    }
}

/// <summary>Состояние одного игрока в снапшоте хоста.</summary>
internal struct PeerSnapshot
{
    public byte Id;
    public float X;
    public float Y;
    public float Hp;
    public float MaxHp;
    public float Mana;
    public float Aim;
    public bool Alive;
    public bool Dashing;
    public int Rune;
    public float HitFlash;
    public int Flow;
}

internal struct ProjectileSnapshot
{
    public float X;
    public float Y;
    public float Vx;
    public float Vy;
    public float Radius;
    public float Damage;
    public byte Element;
    public byte Style;
    public bool Hostile;
    public byte Owner;
    public float Life;
    public float Size;
    public float Length;
    public int ColorArgb;
}

internal static class DuelRules
{
    public const int WinsRequired = 3;
    public const int MaxPlayers = 2;
    public const float RoundResetDelay = 3.2f;

    public static readonly string[] ElementNames = { "ОГОНЬ", "ВЕТЕР", "ВОДА", "ЗЕМЛЯ", "ТЬМА", "СВЕТ" };
}
