using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// T11 unit tests for the <see cref="PacketReconciliation.IncompleteKinds"/>
/// detection predicate: missing, Failed, and stale-Generating kinds are flagged;
/// Generated and freshly-Generating kinds are left alone.
/// </summary>
public class PacketReconciliationTests
{
    private static readonly Guid TenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid AppointmentId = new("b2c3d4e5-f6a7-8901-bcde-f12345678901");
    private static readonly DateTime Now = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    private static AppointmentPacket Packet(PacketKind kind, PacketGenerationStatus status, DateTime? lastAttemptAt)
    {
        var p = new AppointmentPacket(Guid.NewGuid(), TenantId, AppointmentId, kind, "blob/x.pdf", status);
        p.LastAttemptAt = lastAttemptAt;
        return p;
    }

    [Fact]
    public void NoPackets_AllThreeKindsIncomplete()
    {
        var result = PacketReconciliation.IncompleteKinds(
            Enumerable.Empty<AppointmentPacket>(), Now, StaleAfter);

        result.ShouldBe(PacketReconciliation.ExpectedKinds, ignoreOrder: true);
    }

    [Fact]
    public void AllThreeGenerated_NoneIncomplete()
    {
        var packets = PacketReconciliation.ExpectedKinds
            .Select(k => Packet(k, PacketGenerationStatus.Generated, Now))
            .ToList();

        var result = PacketReconciliation.IncompleteKinds(packets, Now, StaleAfter);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void FailedKind_IsIncomplete()
    {
        var packets = new List<AppointmentPacket>
        {
            Packet(PacketKind.Patient, PacketGenerationStatus.Generated, Now),
            Packet(PacketKind.Doctor, PacketGenerationStatus.Generated, Now),
            Packet(PacketKind.AttorneyClaimExaminer, PacketGenerationStatus.Failed, Now),
        };

        var result = PacketReconciliation.IncompleteKinds(packets, Now, StaleAfter);

        result.ShouldBe(new[] { PacketKind.AttorneyClaimExaminer });
    }

    [Fact]
    public void MissingKind_IsIncomplete()
    {
        var packets = new List<AppointmentPacket>
        {
            Packet(PacketKind.Patient, PacketGenerationStatus.Generated, Now),
            Packet(PacketKind.Doctor, PacketGenerationStatus.Generated, Now),
            // AttorneyClaimExaminer row never created.
        };

        var result = PacketReconciliation.IncompleteKinds(packets, Now, StaleAfter);

        result.ShouldBe(new[] { PacketKind.AttorneyClaimExaminer });
    }

    [Fact]
    public void FreshlyGenerating_IsNotIncomplete()
    {
        // Attempt started 5 minutes ago -- still well within the render/retry window.
        var packets = new List<AppointmentPacket>
        {
            Packet(PacketKind.Patient, PacketGenerationStatus.Generating, Now.AddMinutes(-5)),
            Packet(PacketKind.Doctor, PacketGenerationStatus.Generated, Now),
            Packet(PacketKind.AttorneyClaimExaminer, PacketGenerationStatus.Generated, Now),
        };

        var result = PacketReconciliation.IncompleteKinds(packets, Now, StaleAfter);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void StaleGenerating_IsIncomplete()
    {
        // Attempt started 45 minutes ago and never finished -> worker died.
        var packets = new List<AppointmentPacket>
        {
            Packet(PacketKind.Patient, PacketGenerationStatus.Generating, Now.AddMinutes(-45)),
            Packet(PacketKind.Doctor, PacketGenerationStatus.Generated, Now),
            Packet(PacketKind.AttorneyClaimExaminer, PacketGenerationStatus.Generated, Now),
        };

        var result = PacketReconciliation.IncompleteKinds(packets, Now, StaleAfter);

        result.ShouldBe(new[] { PacketKind.Patient });
    }

    [Fact]
    public void StaleGenerating_WithNullLastAttempt_FallsBackToGeneratedAt()
    {
        // LastAttemptAt not stamped (legacy row); GeneratedAt (insert time) is old.
        var p = Packet(PacketKind.Patient, PacketGenerationStatus.Generating, lastAttemptAt: null);
        p.GeneratedAt = Now.AddHours(-2);

        var result = PacketReconciliation.IncompleteKinds(new[] { p }, Now, StaleAfter);

        result.ShouldContain(PacketKind.Patient);
    }
}
