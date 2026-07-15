using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Unit tests for <see cref="AppointmentPacketManager"/> -- specifically the
/// T1 idempotency guard in <see cref="AppointmentPacketManager.EnsureGeneratingAsync"/>.
/// </summary>
public class AppointmentPacketManagerTests
{
    private static readonly Guid TenantId =
        new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid AppointmentId =
        new("b2c3d4e5-f6a7-8901-bcde-f12345678901");

    [Fact]
    public async Task EnsureGeneratingAsync_WhenAlreadyGenerated_ReturnsUnchangedWithoutReset()
    {
        var existing = new AppointmentPacket(
            Guid.NewGuid(), TenantId, AppointmentId, PacketKind.Patient,
            "blob/old.pdf", PacketGenerationStatus.Generated);

        var repo = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        repo.GetQueryableAsync()
            .Returns(new List<AppointmentPacket> { existing }.AsQueryable());

        var manager = new AppointmentPacketManager(repo);

        var result = await manager.EnsureGeneratingAsync(
            TenantId, AppointmentId, PacketKind.Patient, "blob/new.pdf");

        // Idempotency guard: an already-Generated kind is returned as-is so the
        // job skips re-render + re-publish. It must NOT be flipped back to
        // Generating and must NOT be updated (which would also overwrite BlobName
        // and orphan the prior blob).
        result.Status.ShouldBe(PacketGenerationStatus.Generated);
        result.BlobName.ShouldBe("blob/old.pdf");
        await repo.DidNotReceive().UpdateAsync(
            Arg.Any<AppointmentPacket>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
