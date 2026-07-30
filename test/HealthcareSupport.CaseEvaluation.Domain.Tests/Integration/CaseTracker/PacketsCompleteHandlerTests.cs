using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the all-or-nothing packet publish, plus the pure
/// <see cref="PacketSetPolicy"/> rules the reconciliation release shares with it.
///
/// <para>The behaviour worth protecting is the NEGATIVE one: the first two packets must publish
/// nothing. A partial set is not useful to their staff, and three pushes per approval would triple the
/// traffic to say the same thing.</para>
/// </summary>
public class PacketsCompleteHandlerTests
{
    private static readonly Guid TenantId = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");
    private static readonly DateTime Now = new(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);

    private static Appointment NewAppointment(AppointmentStatusType status = AppointmentStatusType.Approved) =>
        new(
            AppointmentId,
            patientId: new Guid("e5f6a7b8-c9d0-4e1f-a2b3-c4d5e6f7a8bc"),
            identityUserId: null,
            appointmentTypeId: new Guid("a1c2e3f4-5566-4778-9900-aabbccddeeff"),
            locationId: new Guid("c0ffee0a-bcde-4f01-9abc-de0123456f7a"),
            doctorAvailabilityId: new Guid("d1e2f3a4-b5c6-4d7e-8f90-a1b2c3d4e5fa"),
            appointmentDate: new DateTime(2026, 8, 15, 9, 30, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A00065",
            appointmentStatus: status,
            panelNumber: "PN-SAMPLE")
        {
            TenantId = TenantId,
        };

    private static AppointmentPacket NewPacket(
        PacketKind kind,
        PacketGenerationStatus status,
        DateTime? lastChanged = null) =>
        new(
            Guid.NewGuid(),
            TenantId,
            AppointmentId,
            kind,
            blobName: "tenantseg/apptseg/packet/patient/228d6bed62e04be7b1146e58629bf901.pdf",
            status: status)
        {
            GeneratedAt = status == PacketGenerationStatus.Generated ? Now : default,
            CreationTime = lastChanged ?? Now,
        };

    private static (PacketsCompleteHandler Handler, ICaseTrackerDocumentQueue Queue) Build(
        List<AppointmentPacket> packets,
        AppointmentStatusType status = AppointmentStatusType.Approved)
    {
        var appointment = NewAppointment(status);

        var appointmentRepo = Substitute.For<IRepository<Appointment, Guid>>();
        appointmentRepo.FindAsync(AppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Appointment?>(appointment));

        var packetRepo = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        packetRepo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentPacket, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(packets));

        var resolver = Substitute.For<IDocumentListResolver>();
        resolver.ResolvePacketsAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(packets
                .Where(DocumentEntryMapper.IsFetchable)
                .Select(p => DocumentEntryMapper.FromPacket(p, "A00065", TenantId))
                .ToList()));

        var queue = Substitute.For<ICaseTrackerDocumentQueue>();

        return (
            new PacketsCompleteHandler(
                appointmentRepo, packetRepo, resolver, queue, NullLogger<PacketsCompleteHandler>.Instance),
            queue);
    }

    private static PacketGeneratedEto Event(PacketKind kind) => new()
    {
        AppointmentId = AppointmentId,
        TenantId = TenantId,
        PacketId = Guid.NewGuid(),
        Kind = kind,
        OccurredAt = Now,
    };

    private static List<AppointmentPacket> AllThreeGenerated() =>
        PacketSetPolicy.AllKinds
            .Select(k => NewPacket(k, PacketGenerationStatus.Generated))
            .ToList();

    [Fact]
    public async Task WhenTheFirstPacketCompletes_NothingIsQueued()
    {
        var (handler, queue) = Build(new List<AppointmentPacket>
        {
            NewPacket(PacketKind.Patient, PacketGenerationStatus.Generated),
            NewPacket(PacketKind.Doctor, PacketGenerationStatus.Generating),
            NewPacket(PacketKind.AttorneyClaimExaminer, PacketGenerationStatus.Generating),
        });

        await handler.HandleEventAsync(Event(PacketKind.Patient));

        await queue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(default, default, default!, default);
    }

    [Fact]
    public async Task WhenTheLastPacketCompletes_AllThreeAreQueuedTogether()
    {
        var (handler, queue) = Build(AllThreeGenerated());

        await handler.HandleEventAsync(Event(PacketKind.AttorneyClaimExaminer));

        await queue.Received(1).EnqueueDocumentEntriesAsync(
            AppointmentId,
            TenantId,
            Arg.Is<IReadOnlyList<IntakeDocumentEntry>>(list =>
                list.Count == 3 && list.All(e => e.Source == DocumentEntryMapper.PacketSource)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishedPacketsClaimPdf_NotDocx()
    {
        var (handler, queue) = Build(AllThreeGenerated());

        await handler.HandleEventAsync(Event(PacketKind.Doctor));

        await queue.Received(1).EnqueueDocumentEntriesAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid?>(),
            Arg.Is<IReadOnlyList<IntakeDocumentEntry>>(list =>
                list.All(e => e.ContentType == "application/pdf" && e.FileName.EndsWith(".pdf"))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAKindFailed_NothingIsQueuedImmediately()
    {
        // The release path, not this handler, decides when to give up on the failed kind.
        var (handler, queue) = Build(new List<AppointmentPacket>
        {
            NewPacket(PacketKind.Patient, PacketGenerationStatus.Generated),
            NewPacket(PacketKind.Doctor, PacketGenerationStatus.Generated),
            NewPacket(PacketKind.AttorneyClaimExaminer, PacketGenerationStatus.Failed),
        });

        await handler.HandleEventAsync(Event(PacketKind.Doctor));

        await queue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(default, default, default!, default);
    }

    [Fact]
    public async Task WhenTheAppointmentWasNeverApproved_NothingIsQueued()
    {
        var (handler, queue) = Build(AllThreeGenerated(), status: AppointmentStatusType.Pending);

        await handler.HandleEventAsync(Event(PacketKind.Patient));

        await queue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(default, default, default!, default);
    }

    [Fact]
    public void IsComplete_RequiresEveryKind()
    {
        PacketSetPolicy.IsComplete(AllThreeGenerated()).ShouldBeTrue();
        PacketSetPolicy.IsComplete(new List<AppointmentPacket>()).ShouldBeFalse();
        PacketSetPolicy.IsComplete(new[] { NewPacket(PacketKind.Patient, PacketGenerationStatus.Generated) })
            .ShouldBeFalse();
    }

    [Fact]
    public void ShouldRelease_OnlyWhenStalledWithSomethingToPublish()
    {
        var stale = Now.AddHours(-2);
        var cutoff = Now.AddMinutes(-30);

        var oneFailedLongAgo = new[]
        {
            NewPacket(PacketKind.Patient, PacketGenerationStatus.Generated, stale),
            NewPacket(PacketKind.Doctor, PacketGenerationStatus.Generated, stale),
            NewPacket(PacketKind.AttorneyClaimExaminer, PacketGenerationStatus.Failed, stale),
        };
        PacketSetPolicy.ShouldRelease(oneFailedLongAgo, cutoff).ShouldBeTrue();

        // Still rendering: a slow job must not be mistaken for a stuck one.
        var recentlyChanged = new[]
        {
            NewPacket(PacketKind.Patient, PacketGenerationStatus.Generated, Now),
            NewPacket(PacketKind.Doctor, PacketGenerationStatus.Generating, Now),
            NewPacket(PacketKind.AttorneyClaimExaminer, PacketGenerationStatus.Generating, Now),
        };
        PacketSetPolicy.ShouldRelease(recentlyChanged, cutoff).ShouldBeFalse();

        // Complete sets are the handler's business, not the release path's.
        PacketSetPolicy.ShouldRelease(AllThreeGenerated(), cutoff).ShouldBeFalse();

        // Nothing generated yet: releasing would publish an empty set.
        var allFailed = PacketSetPolicy.AllKinds
            .Select(k => NewPacket(k, PacketGenerationStatus.Failed, stale))
            .ToList();
        PacketSetPolicy.ShouldRelease(allFailed, cutoff).ShouldBeFalse();
    }

    [Fact]
    public void AllKinds_TracksTheEnum()
    {
        // Guards against the completeness check silently checking a stale count if a fourth
        // template is ever added.
        PacketSetPolicy.AllKinds.Count.ShouldBe(Enum.GetValues<PacketKind>().Length);
    }
}
