using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AetherSequence.Net;

/// <summary>
/// Эмуляция двух машин: один процесс слушает и объявляет комнату,
/// второй ищет комнаты и подключается. Никакого ReuseAddress -
/// сокеты на разных портах, как на разных компьютерах.
/// </summary>
internal static class LanProbe
{
    private const byte Magic = NetProtocol.DiscoveryMagic;
    private const byte Version = NetProtocol.Version;
    private const int DiscoveryPort = NetProtocol.DiscoveryPort;
    private const int GamePort = NetProtocol.GamePort;

    public static int Run()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("=== ЭМУЛЯЦИЯ ДВУХ МАШИН ===");

        // --- "Хост": слушает 47800 и слушает TCP 47801
        using UdpClient hostUdp = new(DiscoveryPort);
        hostUdp.EnableBroadcast = true;
        using TcpListener hostTcp = new(IPAddress.Loopback, GamePort + 200);
        hostTcp.Start();
        int hostPort = ((IPEndPoint)hostTcp.LocalEndpoint).Port;
        Console.WriteLine($"хост: UDP {DiscoveryPort}, TCP {hostPort}");

        bool announced = false;
        Thread announce = new(() =>
        {
            while (!announced)
            {
                byte[] packet = Announce("ПЕЩЕРА", "ХОСТ", hostPort);
                try { hostUdp.Send(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort)); }
                catch (SocketException) { }
                Thread.Sleep(300);
            }
        }) { IsBackground = true };
        announce.Start();

        // --- "Клиент": ищет комнаты
        using UdpClient clientUdp = new(DiscoveryPort + 1);
        clientUdp.EnableBroadcast = true;

        byte[] discover = { Magic, 0 };
        int sent = 0;
        foreach (IPAddress target in new[] { IPAddress.Broadcast })
        {
            try { clientUdp.Send(discover, discover.Length, new IPEndPoint(target, DiscoveryPort + 1)); sent++; }
            catch (SocketException) { }
        }
        Console.WriteLine($"клиент: отправил Discover на {sent} адрес");

        // Хост получает Discover и отвечает объявлением точечно.
        hostUdp.Client.ReceiveTimeout = 3000;
        try
        {
            IPEndPoint from = new(IPAddress.Any, 0);
            hostUdp.Receive(ref from);
            byte[] reply = Announce("ПЕЩЕРА", "ХОСТ", hostPort);
            hostUdp.Send(reply, reply.Length, new IPEndPoint(from.Address, DiscoveryPort + 1));
            announced = true;
            Console.WriteLine($"хост: получил Discover от {from.Address}, ответил");
        }
        catch (SocketException)
        {
            announced = true;
            Console.WriteLine("хост: Discover не дошёл");
        }

        // Клиент читает ответ. Разбор идёт через настоящий парсер игры,
        // иначе проба проверяла бы свой код, а не код LanDiscovery.
        clientUdp.Client.ReceiveTimeout = 3000;
        bool received = false;
        bool connected = false;
        int announcedPort = 0;
        try
        {
            IPEndPoint from = new(IPAddress.Any, 0);
            // Клиент слышит и собственный Discover, идут пакеты по кругу.
            // Пропускаем всё, что не является объявлением.
            RoomListing? listing = null;
            for (int i = 0; i < 5 && listing is null; i++)
            {
                byte[] data = clientUdp.Receive(ref from);
                listing = LanDiscovery.ParseAnnounce(data, from);
                if (listing is null)
                {
                    Console.WriteLine($"клиент: пропущен пакет {data.Length} байт (не объявление)");
                }
            }
            if (listing is null)
            {
                Console.WriteLine("клиент: пакет не распознан как объявление");
            }
            else
            {
                received = true;
                announcedPort = listing.Port;
                Console.WriteLine($"клиент: получил объявление - комната '{listing.Name}', " +
                                  $"хост '{listing.HostNick}', порт {listing.Port}");

                // Подключается по объявленному порту.
                using TcpClient tcp = new();
                tcp.Connect(IPAddress.Loopback, listing.Port);
                connected = true;
                announced = true;
                Console.WriteLine($"клиент: TCP-подключение к порту {listing.Port}: OK");
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"клиент: не получил объявление ({ex.SocketErrorCode})");
        }
        catch (IOException)
        {
            Console.WriteLine("клиент: пакет оборван");
        }

        announced = true;
        Thread.Sleep(100);
        hostTcp.Stop();

        // Проверяем именно разбор объявления, а не вхождение порта в строку:
        // имя комнаты случайное и порт в нём может не встретиться.
        bool ok = received && connected && announcedPort == hostPort;
        Console.WriteLine(ok
            ? "=== ЭМУЛЯЦИЯ ПРОШЛА: Discover -> объявление -> подключение ==="
            : $"=== ЭМУЛЯЦИЯ ПРОВАЛЕНА (объявление={received}, подключение={connected}, порт {announcedPort} вместо {hostPort}) ===");
        return ok ? 0 : 1;
    }

    private static byte[] Announce(string room, string nick, int port)
    {
        using MemoryStream stream = new(512);
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write(Magic);
        writer.Write((byte)Version);
        WriteString(writer, room);
        WriteString(writer, nick);
        writer.Write(port);
        writer.Write((byte)0);
        writer.Write((byte)2);
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }

}
