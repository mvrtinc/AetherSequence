using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AetherSequence.Net;

/// <summary>
/// Диагностика поиска комнат. Показывает, доходит ли широковещательный
/// пакет и где именно обрыв: сеть, брандмауэр или протокол.
/// </summary>
internal static class NetDiag
{
    public static void Run(int seconds = 6)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== ДИАГНОСТИКА СЕТИ ДУЭЛИ ===");
        Console.WriteLine($" discovery port {NetProtocol.DiscoveryPort}, game port {NetProtocol.GamePort}");
        Console.WriteLine();

        // Сначала эмуляция двух машин на разных портах - она проверяет
        // сам протокол игры и не зависит от брандмауэра.
        TwoMachines();
        Console.WriteLine();

        Console.WriteLine("[1] сетевые адаптеры:");
        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (UnicastIPAddressInformation unicast in nic.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                byte[] b = unicast.Address.GetAddressBytes();
                string kind = b[0] == 169 ? "APIPA (адрес не получен)" :
                    b[0] == 172 && b[1] == 17 ? "виртуальный (VPN/Docker)" :
                    "обычный";
                Console.WriteLine($"     {nic.Name,-28} {unicast.Address,-15} /{unicast.PrefixLength,-3} {nic.NetworkInterfaceType,-14} {kind}");
            }
        }
        Console.WriteLine();

        Console.WriteLine("[2] брандмауэр и профиль сети:");
        RunPowerShell("Get-NetFirewallProfile | Select-Object Name,Enabled | Format-Table -HideTableHeaders", "профили брандмауэра");
        RunPowerShell("Get-NetFirewallRule -DisplayName 'AETHER SEQUENCE*' -ErrorAction SilentlyContinue | Select-Object DisplayName,Enabled,Profile | Format-Table -HideTableHeaders", "правила для игры");
        Console.WriteLine();

        Console.WriteLine("[3] проверка broadcast в пределах своего компьютера:");
        // Два сокета: один слушает, второй шлёт. Если пакет не вернулся даже
        // самому себе - broadcast в этой сети заблокирован.
        bool loopback = ProbeBroadcast();
        Console.WriteLine(loopback
            ? "     [ok] broadcast возвращается самому себе"
            : "     [!] broadcast НЕ возвращается - сеть его блокирует");
        Console.WriteLine();

        Console.WriteLine($"[4] слушаем {seconds}с, не отвечает ли кто-нибудь на Discover:");
        using UdpClient socket = new();
        socket.EnableBroadcast = true;
        socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        bool bound = true;
        try
        {
            socket.Client.Bind(new IPEndPoint(IPAddress.Any, NetProtocol.DiscoveryPort));
        }
        catch (SocketException ex)
        {
            bound = false;
            Console.WriteLine($"     [!] порт {NetProtocol.DiscoveryPort} занят: {ex.SocketErrorCode}");
            Console.WriteLine("         закройте вторую копию игры и повторите проверку");
        }

        if (bound)
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(seconds));
            int sent = SendDiscover(socket);
            Console.WriteLine($"     отправлено Discover на {sent} адресов, ждём ответ...");

            List<string> seen = new();
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    UdpReceiveResult got = socket.ReceiveAsync(cts.Token).GetAwaiter().GetResult();
                    if (got.Buffer.Length < 2 || got.Buffer[0] != NetProtocol.DiscoveryMagic) continue;
                    string line = Describe(got.Buffer, got.RemoteEndPoint);
                    if (!seen.Contains(line))
                    {
                        seen.Add(line);
                        Console.WriteLine($"     [ok] объявление от {got.RemoteEndPoint}: {line}");
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) { }

            if (seen.Count == 0)
            {
                Console.WriteLine("     ответов нет. Итог:");
                Console.WriteLine("       - если комната друга видна только у вас - скорее всего у него закрыт брандмауэр");
                Console.WriteLine("       - если ни у кого - проверьте, что оба в одной сети WiFi и не включена изоляция клиентов");
                Console.WriteLine("       - надёжный обходной путь: пункт 'ПОДКЛЮЧИТЬСЯ ПО IP' в меню дуэли");
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== конец диагностики ===");
    }

    private static string Describe(byte[] data, IPEndPoint from)
    {
        if (data.Length < 3) return "запрос Discover";
        int version = data[1];
        if (version == 0) return "запрос Discover";
        if (version != NetProtocol.Version) return $"чужая версия {version}";

        using System.IO.MemoryStream stream = new(data, writable: false);
        using System.IO.BinaryReader reader = new(stream, System.Text.Encoding.UTF8);
        reader.ReadByte();
        reader.ReadByte();
        string room = NetProtocol.ReadString(reader, 32);
        string nick = NetProtocol.ReadString(reader, NetProtocol.MaxNickLength);
        int port = reader.ReadInt32();
        int players = reader.ReadByte();
        int max = reader.ReadByte();
        return $"комната '{room}', хост '{nick}', {players}/{max}, порт {port}";
    }

    private static bool ProbeBroadcast()
    {
        const int probePort = 47877;
        try
        {
            using UdpClient listener = new(probePort);
            listener.EnableBroadcast = true;
            using UdpClient sender = new();
            sender.EnableBroadcast = true;

            byte[] payload = "AETHER-PROBE"u8.ToArray();
            sender.Send(payload, payload.Length, new IPEndPoint(IPAddress.Broadcast, probePort));

            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
            UdpReceiveResult got = listener.ReceiveAsync(cts.Token).GetAwaiter().GetResult();
            return got.Buffer.Length > 0;
        }
        catch (OperationCanceledException) { return false; }
        catch (SocketException) { return false; }
    }

    private static int SendDiscover(UdpClient socket)
    {
        int sent = 0;
        byte[] payload = { NetProtocol.DiscoveryMagic, 0 };
        foreach (IPAddress target in Targets())
        {
            try
            {
                socket.Send(payload, payload.Length, new IPEndPoint(target, NetProtocol.DiscoveryPort));
                sent++;
            }
            catch (SocketException) { }
        }
        return sent;
    }

    private static IEnumerable<IPAddress> Targets()
    {
        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (UnicastIPAddressInformation unicast in nic.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                byte[] b = unicast.Address.GetAddressBytes();
                if (b[0] == 127 || b[0] == 169) continue;
                b[3] = 255;
                yield return new IPAddress(b);
            }
        }
        yield return IPAddress.Broadcast;
    }

    /// <summary>
    /// Эмуляция двух машин: хост объявляет комнату, клиент ищет и подключается.
    /// Сокеты на разных портах - как на разных компьютерах, поэтому
    /// результат не зависит от того, занял ли кто-то порт обнаружения.
    /// </summary>
    private static void TwoMachines()
    {
        try
        {
            int code = LanProbe.Run();
            Console.WriteLine($"     итог эмуляции: {(code == 0 ? "протокол исправен" : "в протоколе есть ошибка")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"     эмуляция не выполнилась: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RunPowerShell(string script, string title)    {
        Console.WriteLine($"     {title}:");
        try
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"{script.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            })!;
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            foreach (string line in output.Split('\n'))
            {
                string trimmed = line.Trim('\r', ' ');
                if (trimmed.Length > 0) Console.WriteLine($"       {trimmed}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"       не удалось прочитать: {ex.Message}");
        }
    }
}
