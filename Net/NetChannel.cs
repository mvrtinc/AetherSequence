using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AetherSequence.Net;

/// <summary>
/// Дуплексный канал поверх TCP: длину-префиксованные кадры [kind][len][payload],
/// приём в фоновом цикле, отправка через очередь, чтобы не блокировать игровой поток.
/// </summary>
internal sealed class NetChannel : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentQueue<byte[]> _outgoing = new();
    private readonly SemaphoreSlim _outgoingSignal = new(0);
    private Action<MsgKind, byte[]>? _onMessage;
    private volatile bool _disposed;
    private bool _started;

    public bool IsAlive => !_disposed && _client.Connected;
    public string Error = string.Empty;

    public NetChannel(TcpClient client, Action<MsgKind, byte[]>? onMessage, bool autoStart = true)
    {
        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
        _onMessage = onMessage;
        if (autoStart) Start();
    }

    /// <summary>Запустить приём и отправку. Позволяет сначала настроить обработчик.</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;
        _ = Task.Run(ReceiveLoopAsync);
        _ = Task.Run(SendLoopAsync);
    }

    public void Send(MsgKind kind, Action<BinaryWriter> fill)
    {
        if (_disposed) return;
        using MemoryStream payload = new(256);
        using (BinaryWriter writer = new(payload, System.Text.Encoding.UTF8, true))
        {
            fill(writer);
        }
        byte[] body = payload.ToArray();
        using MemoryStream frame = new(body.Length + 3);
        frame.WriteByte((byte)kind);
        frame.WriteByte((byte)(body.Length & 0xFF));
        frame.WriteByte((byte)(body.Length >> 8));
        frame.Write(body, 0, body.Length);
        _outgoing.Enqueue(frame.ToArray());
        _outgoingSignal.Release();
    }

    private async Task SendLoopAsync()
    {
        CancellationToken token = _cts.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                await _outgoingSignal.WaitAsync(token).ConfigureAwait(false);
                while (_outgoing.TryDequeue(out byte[]? frame))
                {
                    await _stream.WriteAsync(frame, token).ConfigureAwait(false);
                }
                await _stream.FlushAsync(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (IOException e) { Error = e.Message; }
        catch (SocketException e) { Error = e.Message; }
    }

    private async Task ReceiveLoopAsync()
    {
        CancellationToken token = _cts.Token;
        try
        {
            byte[] header = new byte[3];
            while (!token.IsCancellationRequested)
            {
                if (!await ReadExactAsync(header, token).ConfigureAwait(false)) break;
                MsgKind kind = (MsgKind)header[0];
                int length = header[1] | (header[2] << 8);
                if (length is < 0 or > 1 << 20) break;
                byte[] body = new byte[length];
                if (length > 0 && !await ReadExactAsync(body, token).ConfigureAwait(false)) break;
                _onMessage?.Invoke(kind, body);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (IOException e) { Error = e.Message; }
        catch (SocketException e) { Error = e.Message; }
    }

    private async Task<bool> ReadExactAsync(byte[] buffer, CancellationToken token)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int got = await _stream.ReadAsync(buffer.AsMemory(read), token).ConfigureAwait(false);
            if (got <= 0) return false;
            read += got;
        }
        return true;
    }

    public void Dispose()
    {
        _disposed = true;
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        try { _stream.Dispose(); } catch (ObjectDisposedException) { }
        try { _client.Close(); } catch (SocketException) { }
        try { _cts.Dispose(); } catch (ObjectDisposedException) { }
        try { _outgoingSignal.Dispose(); } catch (ObjectDisposedException) { }
    }
}
