using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Minio;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Uploads;

/// <summary>
/// The scanning provider (B11, decision D2): scan first, store only a clean file, fail closed.
/// </summary>
/// <remarks>
/// The MinIO write is replaced by a recorder (<see cref="RecordingProvider"/>), so "nothing was
/// stored" is a fact about the one call that would have stored it. Each refusal fact has a clean-file
/// counterpart in the same class proving that call IS reached when it should be -- otherwise a test
/// that never got as far as the write would pass whether or not the scan refused.
/// </remarks>
public class ScanningMinioBlobProviderTests
{
    private readonly IUploadScanner _scanner = Substitute.For<IUploadScanner>();
    private readonly ListLogger _log = new();

    private RecordingProvider Provider() =>
        new(Substitute.For<IMinioBlobNameCalculator>(), Substitute.For<IBlobNormalizeNamingService>(), _scanner, _log);

    private static BlobProviderSaveArgs Args(Stream stream, string container = "master-documents", string blob = "Patient Smith intake.pdf") =>
        new(container, new BlobContainerConfiguration(), blob, stream, overrideExisting: false, CancellationToken.None);

    private static byte[] Bytes(int count) => Enumerable.Range(0, count).Select(i => (byte)i).ToArray();

    [Fact]
    public async Task A_clean_file_is_stored_once_with_the_stream_rewound()
    {
        _scanner.ScanAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.Arg<Stream>().ReadByte(); // a scanner reads the stream; the store must still get all of it
                return UploadScanResult.Clean;
            });
        var provider = Provider();

        await provider.SaveAsync(Args(new MemoryStream(Bytes(32))));

        provider.Stored.Count.ShouldBe(1);
        provider.Stored[0].ShouldBe(Bytes(32));
    }

    [Fact]
    public async Task A_found_signature_is_refused_and_nothing_is_stored()
    {
        _scanner.ScanAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(UploadScanResult.Found("Test.Sig-1"));
        var provider = Provider();

        var ex = await Should.ThrowAsync<BusinessException>(() => provider.SaveAsync(Args(new MemoryStream(Bytes(32)))));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.UploadRefused);
        provider.Stored.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_unavailable_scanner_is_refused_and_nothing_is_stored()
    {
        _scanner.ScanAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UploadScanUnavailableException("down"));
        var provider = Provider();

        var ex = await Should.ThrowAsync<BusinessException>(() => provider.SaveAsync(Args(new MemoryStream(Bytes(32)))));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.UploadScanUnavailable);
        provider.Stored.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_non_seekable_stream_is_scanned_and_stored_in_full()
    {
        _scanner.ScanAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.Arg<Stream>().CanSeek.ShouldBeTrue(); // buffered before the scan
                return UploadScanResult.Clean;
            });
        var provider = Provider();

        await provider.SaveAsync(Args(new ForwardOnlyStream(Bytes(70_000))));

        provider.Stored.Count.ShouldBe(1);
        provider.Stored[0].ShouldBe(Bytes(70_000));
    }

    [Fact]
    public async Task The_refusal_log_names_the_signature_and_container_and_never_the_file_name()
    {
        _scanner.ScanAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(UploadScanResult.Found("Test.Sig-1"));

        await Should.ThrowAsync<BusinessException>(
            () => Provider().SaveAsync(Args(new MemoryStream(Bytes(8)), container: "master-documents", blob: "Patient Smith intake.pdf")));

        var entry = _log.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain("Test.Sig-1");
        entry.Message.ShouldContain("master-documents");
        entry.Message.ShouldNotContain("Smith");
        entry.Message.ShouldNotContain(".pdf");
    }

    /// <summary>The provider under test, with the MinIO write replaced by a recorder.</summary>
    private sealed class RecordingProvider : ScanningMinioBlobProvider
    {
        public RecordingProvider(
            IMinioBlobNameCalculator calculator,
            IBlobNormalizeNamingService naming,
            IUploadScanner scanner,
            ILogger<ScanningMinioBlobProvider> logger)
            : base(calculator, naming, scanner, logger)
        {
        }

        public List<byte[]> Stored { get; } = new();

        protected override async Task SaveToStorageAsync(BlobProviderSaveArgs args)
        {
            using var copy = new MemoryStream();
            await args.BlobStream.CopyToAsync(copy);
            Stored.Add(copy.ToArray());
        }
    }

    private sealed class ListLogger : ILogger<ScanningMinioBlobProvider>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
                // Nothing to release.
            }
        }
    }

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
