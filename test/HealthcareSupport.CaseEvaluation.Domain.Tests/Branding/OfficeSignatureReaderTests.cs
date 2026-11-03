using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Logging;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// <see cref="OfficeSignatureReader"/> prints a doctor's signature on a legal form, so these
/// pin which blob it reads (only the current office's own, at host scope), what it accepts (a PNG
/// of at most 200 KB) and that it never fails a packet. Every image here is a synthetic
/// byte string, never a real signature.
/// </summary>
public class OfficeSignatureReaderTests
{
    private static readonly Guid OfficeA = Guid.NewGuid();
    private static readonly Guid OfficeB = Guid.NewGuid();
    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>Synthetic "PNG": the real signature bytes plus filler. Not a decodable image.</summary>
    private static byte[] FakePng(int totalLength = 40) =>
        PngMagic.Concat(Enumerable.Repeat((byte)0x2A, totalLength - PngMagic.Length)).ToArray();

    private sealed class Rig
    {
        public Dictionary<string, byte[]> Blobs { get; } = new();
        public List<string> ReadNames { get; } = new();
        public List<Guid?> ReadTenants { get; } = new();
        public RecordingLogger<OfficeSignatureReader> Log { get; } = new();
        public bool StorageDown { get; set; }
        public ICurrentTenant Tenant { get; } = Substitute.For<ICurrentTenant>();
        public IBlobContainer<OfficeSignaturesContainer> Container { get; } = Substitute.For<IBlobContainer<OfficeSignaturesContainer>>();

        private Guid? _effectiveTenant;

        public Rig(Guid? officeId)
        {
            _effectiveTenant = officeId;
            Tenant.Id.Returns(_ => _effectiveTenant);
            Tenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(ci =>
            {
                var previous = _effectiveTenant;
                _effectiveTenant = ci.ArgAt<Guid?>(0);
                return new Restore(() => _effectiveTenant = previous);
            });
            Container.GetOrNullAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ci =>
            {
                var name = ci.ArgAt<string>(0);
                ReadNames.Add(name);
                ReadTenants.Add(_effectiveTenant);
                if (StorageDown)
                {
                    throw new IOException("storage unreachable");
                }

                return Task.FromResult<Stream?>(Blobs.TryGetValue(name, out var bytes) ? new MemoryStream(bytes) : null);
            });
        }

        public OfficeSignatureReader Build() => new(Container, Tenant, Log);
    }

    private sealed class Restore(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    [Fact]
    public void The_blob_name_is_under_office_signatures_and_never_the_logo_key()
    {
        OfficeSignatureReader.BlobNameFor(OfficeA).ShouldBe($"office-signatures/{OfficeA:N}.png");
        OfficeSignatureReader.BlobNameFor(OfficeA).ShouldNotBe($"{OfficeA:N}.png");
    }

    [Fact]
    public async Task A_stored_png_becomes_a_data_uri_read_at_host_scope()
    {
        var rig = new Rig(OfficeA);
        var png = FakePng();
        rig.Blobs[OfficeSignatureReader.BlobNameFor(OfficeA)] = png;

        var uri = await rig.Build().ReadDataUriAsync();

        uri.ShouldBe("data:image/png;base64," + Convert.ToBase64String(png));
        rig.ReadTenants.ShouldBe(new Guid?[] { null });
    }

    [Fact]
    public async Task No_stored_signature_is_empty_without_a_warning()
    {
        var rig = new Rig(OfficeA);

        (await rig.Build().ReadDataUriAsync()).ShouldBeEmpty();
        rig.Log.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Another_offices_signature_is_never_read()
    {
        var rig = new Rig(OfficeB);
        rig.Blobs[OfficeSignatureReader.BlobNameFor(OfficeA)] = FakePng();

        (await rig.Build().ReadDataUriAsync()).ShouldBeEmpty();
        rig.ReadNames.ShouldBe(new[] { OfficeSignatureReader.BlobNameFor(OfficeB) });
    }

    [Fact]
    public async Task Host_context_has_no_office_and_reads_nothing()
    {
        var rig = new Rig(officeId: null);

        (await rig.Build().ReadDataUriAsync()).ShouldBeEmpty();
        rig.ReadNames.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_blob_that_is_not_a_png_is_refused_with_a_warning_naming_the_office_only()
    {
        var rig = new Rig(OfficeA);
        rig.Blobs[OfficeSignatureReader.BlobNameFor(OfficeA)] = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0, 0, 0, 0 };

        (await rig.Build().ReadDataUriAsync()).ShouldBeEmpty();

        var warning = rig.Log.Entries.ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain(OfficeA.ToString());
    }

    [Fact]
    public async Task An_oversize_png_is_refused()
    {
        var rig = new Rig(OfficeA);
        rig.Blobs[OfficeSignatureReader.BlobNameFor(OfficeA)] = FakePng(OfficeSignatureReader.MaxBytes + 1);

        (await rig.Build().ReadDataUriAsync()).ShouldBeEmpty();
        rig.Log.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public async Task A_png_at_exactly_the_cap_is_accepted()
    {
        var rig = new Rig(OfficeA);
        rig.Blobs[OfficeSignatureReader.BlobNameFor(OfficeA)] = FakePng(OfficeSignatureReader.MaxBytes);

        (await rig.Build().ReadDataUriAsync()).ShouldStartWith("data:image/png;base64,");
    }

    [Fact]
    public async Task An_empty_blob_is_refused()
    {
        var rig = new Rig(OfficeA);
        rig.Blobs[OfficeSignatureReader.BlobNameFor(OfficeA)] = Array.Empty<byte>();

        (await rig.Build().ReadDataUriAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Storage_being_down_does_not_fail_the_packet()
    {
        var rig = new Rig(OfficeA) { StorageDown = true };

        (await rig.Build().ReadDataUriAsync()).ShouldBeEmpty();
        rig.Log.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Warning);
    }
}
