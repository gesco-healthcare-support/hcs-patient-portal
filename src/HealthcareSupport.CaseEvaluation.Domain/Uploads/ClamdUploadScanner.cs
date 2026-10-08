using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Uploads;

/// <summary>
/// A minimal clamd client speaking the INSTREAM command over TCP (B11, decision D1: our own client
/// rather than nClam, whose last release was 2024-02-13).
/// </summary>
/// <remarks>
/// <para>
/// The protocol, per the clamd man page: send <c>zINSTREAM\0</c>; send the file as chunks, each a
/// 4-byte big-endian length followed by that many bytes; end with a zero-length chunk. clamd answers
/// <c>stream: OK</c> or <c>stream: &lt;signature&gt; FOUND</c>, NUL-terminated because the command
/// carried the <c>z</c> prefix.
/// </para>
/// <para>
/// Everything else fails closed with <see cref="UploadScanUnavailableException"/>: no configured host,
/// a refused or dropped connection, the timeout, an empty reply, an <c>ERROR</c> reply (including
/// "INSTREAM size limit exceeded"), or any reply this client does not recognise.
/// </para>
/// </remarks>
public class ClamdUploadScanner : IUploadScanner, ITransientDependency
{
    private const string Command = "zINSTREAM\0";
    private const string CleanReply = "stream: OK";
    private const string FoundPrefix = "stream: ";
    private const string FoundSuffix = " FOUND";

    /// <summary>A verdict is one short line; anything longer is not a reply this client understands.</summary>
    private const int MaxReplyBytes = 4096;

    private readonly ClamdOptions _options;

    public ClamdUploadScanner(IOptions<ClamdOptions> options)
    {
        _options = options.Value;
    }

    public async Task<UploadScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new UploadScanUnavailableException("No clamd host is configured (Clamd:Host).");
        }

        var startPosition = content.CanSeek ? content.Position : (long?)null;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            var reply = await ExchangeAsync(content, linked.Token);
            return Interpret(reply);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UploadScanUnavailableException(
                $"clamd gave no verdict within {_options.TimeoutSeconds} seconds.");
        }
        catch (Exception ex) when (ex is SocketException or IOException)
        {
            throw new UploadScanUnavailableException("clamd could not be reached or dropped the connection.", ex);
        }
        finally
        {
            if (startPosition is { } position)
            {
                content.Position = position;
            }
        }
    }

    private async Task<string> ExchangeAsync(Stream content, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_options.Host!, _options.Port, cancellationToken);
        var network = client.GetStream();

        await network.WriteAsync(Encoding.ASCII.GetBytes(Command), cancellationToken);

        var header = new byte[4];
        var buffer = new byte[Math.Max(1, _options.ChunkSizeBytes)];
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            BinaryPrimitives.WriteUInt32BigEndian(header, (uint)read);
            await network.WriteAsync(header, cancellationToken);
            await network.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        BinaryPrimitives.WriteUInt32BigEndian(header, 0);
        await network.WriteAsync(header, cancellationToken);
        await network.FlushAsync(cancellationToken);

        return await ReadReplyAsync(network, cancellationToken);
    }

    private static async Task<string> ReadReplyAsync(NetworkStream network, CancellationToken cancellationToken)
    {
        var reply = new MemoryStream();
        var one = new byte[256];
        while (reply.Length < MaxReplyBytes)
        {
            var read = await network.ReadAsync(one, cancellationToken);
            if (read == 0)
            {
                break;
            }

            var terminator = Array.IndexOf(one, (byte)0, 0, read);
            if (terminator >= 0)
            {
                reply.Write(one, 0, terminator);
                break;
            }

            reply.Write(one, 0, read);
        }

        return Encoding.ASCII.GetString(reply.ToArray()).Trim();
    }

    private static UploadScanResult Interpret(string reply)
    {
        if (reply == CleanReply)
        {
            return UploadScanResult.Clean;
        }

        if (reply.StartsWith(FoundPrefix, StringComparison.Ordinal)
            && reply.EndsWith(FoundSuffix, StringComparison.Ordinal))
        {
            var signature = reply[FoundPrefix.Length..^FoundSuffix.Length].Trim();
            if (signature.Length > 0 && !signature.Contains(' '))
            {
                return UploadScanResult.Found(signature);
            }
        }

        throw new UploadScanUnavailableException(
            reply.Length == 0 ? "clamd closed the connection without a verdict." : "clamd answered without a verdict.");
    }
}
