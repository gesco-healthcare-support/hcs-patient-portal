using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Uploads;

/// <summary>
/// The clamd INSTREAM client (B11), against an in-process fake daemon.
/// </summary>
/// <remarks>
/// <para>
/// The fake decides the verdict, so no test needs a real malware signature, and none must contain
/// one: Windows Defender quarantines a file holding the EICAR test string. The real-daemon check is
/// done once, inside a container, and recorded on the pull request.
/// </para>
/// <para>
/// Every "unavailable" fact is a fail-closed fact. Each was seen to fail against a scanner that
/// returned Clean unconditionally before the client was written.
/// </para>
/// </remarks>
public class ClamdUploadScannerTests
{
    private static ClamdUploadScanner ScannerFor(int port, int timeoutSeconds = 5, int chunkSize = 64 * 1024, string? host = "127.0.0.1") =>
        new(Options.Create(new ClamdOptions
        {
            Host = host,
            Port = port,
            TimeoutSeconds = timeoutSeconds,
            ChunkSizeBytes = chunkSize,
        }));

    private static byte[] Bytes(int count) =>
        Enumerable.Range(0, count).Select(i => (byte)(i * 7 % 251)).ToArray();

    // ------------------------------------------------------------------ the wire format

    [Fact]
    public async Task Sends_the_INSTREAM_command_then_big_endian_chunks_then_a_zero_terminator()
    {
        await using var clamd = FakeClamd.Replying("stream: OK\0");
        var payload = Bytes(10);

        await ScannerFor(clamd.Port, chunkSize: 4).ScanAsync(new MemoryStream(payload));
        await clamd.Served.WaitAsync(TimeSpan.FromSeconds(10));

        var expected = Encoding.ASCII.GetBytes("zINSTREAM\0")
            .Concat(new byte[] { 0, 0, 0, 4 }).Concat(payload.Take(4))
            .Concat(new byte[] { 0, 0, 0, 4 }).Concat(payload.Skip(4).Take(4))
            .Concat(new byte[] { 0, 0, 0, 2 }).Concat(payload.Skip(8))
            .Concat(new byte[] { 0, 0, 0, 0 })
            .ToArray();
        clamd.RawBytes.ShouldBe(expected);
    }

    [Fact]
    public async Task Sends_the_payload_byte_for_byte_across_many_chunks()
    {
        await using var clamd = FakeClamd.Replying("stream: OK\0");
        var payload = Bytes(200_000);

        await ScannerFor(clamd.Port, chunkSize: 64 * 1024).ScanAsync(new MemoryStream(payload));
        await clamd.Served.WaitAsync(TimeSpan.FromSeconds(10));

        clamd.Payload.ShouldBe(payload);
        clamd.ChunkLengths.Count.ShouldBeGreaterThan(2);
        clamd.ChunkLengths[^1].ShouldBe(0);
    }

    // ------------------------------------------------------------------ verdicts

    [Fact]
    public async Task A_stream_OK_reply_is_clean()
    {
        await using var clamd = FakeClamd.Replying("stream: OK\0");

        var result = await ScannerFor(clamd.Port).ScanAsync(new MemoryStream(Bytes(16)));

        result.IsClean.ShouldBeTrue();
    }

    [Fact]
    public async Task A_FOUND_reply_names_the_signature()
    {
        await using var clamd = FakeClamd.Replying("stream: Test.Sig-1 FOUND\0");

        var result = await ScannerFor(clamd.Port).ScanAsync(new MemoryStream(Bytes(16)));

        result.IsClean.ShouldBeFalse();
        result.Signature.ShouldBe("Test.Sig-1");
    }

    // ------------------------------------------------------------------ fail closed

    [Theory]
    [InlineData("INSTREAM size limit exceeded. ERROR\0")]
    [InlineData("stream: Can't allocate memory ERROR\0")]
    [InlineData("UNKNOWN COMMAND\0")]
    [InlineData("\0")]
    [InlineData("stream: OK FOUND extra")]
    public async Task Any_other_reply_fails_closed(string reply)
    {
        await using var clamd = FakeClamd.Replying(reply);

        await Should.ThrowAsync<UploadScanUnavailableException>(
            () => ScannerFor(clamd.Port).ScanAsync(new MemoryStream(Bytes(16))));
    }

    [Fact]
    public async Task A_connection_closed_without_a_verdict_fails_closed()
    {
        await using var clamd = FakeClamd.Closing();

        await Should.ThrowAsync<UploadScanUnavailableException>(
            () => ScannerFor(clamd.Port).ScanAsync(new MemoryStream(Bytes(16))));
    }

    [Fact]
    public async Task A_refused_connection_fails_closed()
    {
        var port = UnusedPort();

        await Should.ThrowAsync<UploadScanUnavailableException>(
            () => ScannerFor(port).ScanAsync(new MemoryStream(Bytes(16))));
    }

    [Fact]
    public async Task A_daemon_that_never_answers_fails_closed_within_the_timeout()
    {
        await using var clamd = FakeClamd.Hanging();
        var started = DateTime.UtcNow;

        await Should.ThrowAsync<UploadScanUnavailableException>(
            () => ScannerFor(clamd.Port, timeoutSeconds: 1).ScanAsync(new MemoryStream(Bytes(16))));

        (DateTime.UtcNow - started).ShouldBeLessThan(TimeSpan.FromSeconds(15));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task No_configured_host_fails_closed(string? host)
    {
        await Should.ThrowAsync<UploadScanUnavailableException>(
            () => ScannerFor(3310, host: host).ScanAsync(new MemoryStream(Bytes(16))));
    }

    // ------------------------------------------------------------------ the caller's stream

    [Fact]
    public async Task A_seekable_stream_is_returned_at_the_position_it_was_given_in()
    {
        await using var clamd = FakeClamd.Replying("stream: OK\0");
        var stream = new MemoryStream(Bytes(100)) { Position = 10 };

        await ScannerFor(clamd.Port).ScanAsync(stream);
        await clamd.Served.WaitAsync(TimeSpan.FromSeconds(10));

        stream.Position.ShouldBe(10);
        clamd.Payload.ShouldBe(Bytes(100).Skip(10).ToArray());
    }

    [Fact]
    public async Task A_non_seekable_stream_is_scanned_in_full()
    {
        await using var clamd = FakeClamd.Replying("stream: OK\0");
        var payload = Bytes(70_000);

        var result = await ScannerFor(clamd.Port).ScanAsync(new ForwardOnlyStream(payload));
        await clamd.Served.WaitAsync(TimeSpan.FromSeconds(10));

        result.IsClean.ShouldBeTrue();
        clamd.Payload.ShouldBe(payload);
    }

    private static int UnusedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>A read-only stream that cannot seek, like a request body.</summary>
    private sealed class ForwardOnlyStream : Stream
    {
        private readonly MemoryStream _inner;

        public ForwardOnlyStream(byte[] bytes) => _inner = new MemoryStream(bytes);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override void Flush()
        {
            // Read-only; nothing to flush.
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
