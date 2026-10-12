using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Uploads;

/// <summary>
/// An in-process stand-in for clamd: a loopback listener that accepts ONE connection, records every
/// byte the client sends, decodes the INSTREAM framing, and then behaves as scripted.
/// </summary>
/// <remarks>
/// The verdict strings the tests script are the ones a real clamd sends (checked against
/// clamav/clamav:1.5 when B11 landed): replies to a <c>z</c>-prefixed command end in NUL.
/// </remarks>
internal sealed class FakeClamd : IAsyncDisposable
{
    public enum Behaviour
    {
        /// <summary>Read the whole stream, then send <see cref="Reply"/>.</summary>
        Reply,

        /// <summary>Read the whole stream, then close without answering.</summary>
        CloseWithoutReply,

        /// <summary>Read the whole stream, then never answer (the client must time out).</summary>
        Hang,
    }

    private readonly TcpListener _listener;
    private readonly Task _serve;
    private readonly CancellationTokenSource _stop = new();

    private FakeClamd(Behaviour behaviour, string reply)
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _serve = ServeAsync(behaviour, reply);
    }

    public int Port { get; }

    /// <summary>Every byte received, framing included.</summary>
    public byte[] RawBytes { get; private set; } = Array.Empty<byte>();

    /// <summary>The lengths of each chunk, in order, ending with the zero-length terminator.</summary>
    public List<int> ChunkLengths { get; } = new();

    /// <summary>The payload with the framing removed.</summary>
    public byte[] Payload { get; private set; } = Array.Empty<byte>();

    public static FakeClamd Replying(string reply) => new(Behaviour.Reply, reply);

    public static FakeClamd Closing() => new(Behaviour.CloseWithoutReply, string.Empty);

    public static FakeClamd Hanging() => new(Behaviour.Hang, string.Empty);

    /// <summary>Waits until the fake has finished with its one connection.</summary>
    public Task Served => _serve;

    private async Task ServeAsync(Behaviour behaviour, string reply)
    {
        using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
        var stream = client.GetStream();
        var raw = new MemoryStream();
        var payload = new MemoryStream();

        var command = await ReadExactlyAsync(stream, "zINSTREAM\0".Length, raw);
        if (Encoding.ASCII.GetString(command) != "zINSTREAM\0")
        {
            Finish(raw, payload);
            return;
        }

        while (true)
        {
            var header = await ReadExactlyAsync(stream, 4, raw);
            var length = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
            ChunkLengths.Add(length);
            if (length == 0)
            {
                break;
            }

            payload.Write(await ReadExactlyAsync(stream, length, raw));
        }

        Finish(raw, payload);

        switch (behaviour)
        {
            case Behaviour.Reply:
                var bytes = Encoding.ASCII.GetBytes(reply);
                await stream.WriteAsync(bytes, _stop.Token);
                await stream.FlushAsync(_stop.Token);
                break;
            case Behaviour.Hang:
                await Task.Delay(Timeout.Infinite, _stop.Token).ContinueWith(_ => { }, TaskScheduler.Default);
                break;
            case Behaviour.CloseWithoutReply:
                break;
        }
    }

    private void Finish(MemoryStream raw, MemoryStream payload)
    {
        RawBytes = raw.ToArray();
        Payload = payload.ToArray();
    }

    private async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int count, MemoryStream raw)
    {
        var buffer = new byte[count];
        await stream.ReadExactlyAsync(buffer, _stop.Token);
        raw.Write(buffer);
        return buffer;
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try
        {
            await _serve;
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
        {
            // The fake is being torn down; a cancelled accept or read is the expected way out.
        }

        _stop.Dispose();
    }
}
