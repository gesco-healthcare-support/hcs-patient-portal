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

    private static Appointment NewAppointment(
        AppointmentStatusType status = AppointmentStatusType.Approved,
        Guid? rescheduledFromAppointmentId = null) =>
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
            RescheduledFromAppointmentId = rescheduledFromAppointmentId,
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

    /// <summary>
    /// Wires a REAL <see cref="CaseTrackerPacketPublishService"/> over substituted queues rather than
    /// substituting the service itself, so these tests exercise the actual intake-vs-document-update
    /// branch instead of trusting a stub to have picked correctly.
    /// </summary>
    /// <param name="hasExistingIntake">
    /// Whether the Case Tracker has already been told about this appointment. False is the normal
    /// case since 2026-07-30 -- the settle moment IS first contact, so a complete set becomes the
    /// intake. True models packets settling a SECOND time (a regenerated or finally-rendered kind),
    /// where the intake is history and the change belongs on the document feed.
    /// </param>
    private static (PacketsCompleteHandler Handler, ICaseTrackerDocumentQueue DocumentQueue, ICaseTrackerIntakeQueue IntakeQueue) Build(
        List<AppointmentPacket> packets,
        AppointmentStatusType status = AppointmentStatusType.Approved,
        bool hasExistingIntake = false,
        Guid? rescheduledFromAppointmentId = null)
    {
        var appointment = NewAppointment(status, rescheduledFromAppointmentId);

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

        var documentQueue = Substitute.For<ICaseTrackerDocumentQueue>();
        var intakeQueue = Substitute.For<ICaseTrackerIntakeQueue>();
        intakeQueue.EnqueueIntakeAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(NewOutboxRow(ci.ArgAt<Guid>(0), ci.ArgAt<Guid?>(1))));

        var existingRows = hasExistingIntake
            ? new List<IntegrationOutboxItem> { NewOutboxRow(AppointmentId, TenantId) }
            : new List<IntegrationOutboxItem>();

        var outboxRepo = Substitute.For<IIntegrationOutboxRepository>();
        outboxRepo.GetQueryableAsync().Returns(Task.FromResult(existingRows.AsQueryable()));

        var publishService = new CaseTrackerPacketPublishService(
            outboxRepo,
            intakeQueue,
            documentQueue,
            resolver,
            NullLogger<CaseTrackerPacketPublishService>.Instance);

        return (
            new PacketsCompleteHandler(
                appointmentRepo, packetRepo, publishService, NullLogger<PacketsCompleteHandler>.Instance),
            documentQueue,
            intakeQueue);
    }

    private static IntegrationOutboxItem NewOutboxRow(Guid appointmentId, Guid? tenantId) =>
        new(
            Guid.NewGuid(),
            tenantId,
            IntegrationMessageType.Intake,
            CaseTrackerEndpoints.Intake,
            appointmentId,
            "{\"data\":{}}",
            "key-" + appointmentId.ToString("N"));

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
        var (handler, documentQueue, intakeQueue) = Build(new List<AppointmentPacket>
        {
            NewPacket(PacketKind.Patient, PacketGenerationStatus.Generated),
            NewPacket(PacketKind.Doctor, PacketGenerationStatus.Generating),
            NewPacket(PacketKind.AttorneyClaimExaminer, PacketGenerationStatus.Generating),
        });

        await handler.HandleEventAsync(Event(PacketKind.Patient));

        await documentQueue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(default, default, default!, default);
        await intakeQueue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Fact]
    public async Task WhenTheLastPacketCompletesAndNoIntakeHasGone_TheSetBecomesTheIntake()
    {
        // The 2026-07-30 change: settle is first contact, so the packets ride inside the intake
        // instead of arriving as a document update the receiver has no case to attach to.
        var (handler, documentQueue, intakeQueue) = Build(AllThreeGenerated());

        await handler.HandleEventAsync(Event(PacketKind.AttorneyClaimExaminer));

        await intakeQueue.Received(1).EnqueueIntakeAsync(AppointmentId, TenantId, Arg.Any<CancellationToken>());
        await documentQueue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(default, default, default!, default);
    }

    [Fact]
    public async Task WhenPacketsSettleAgainAfterTheIntake_TheyGoOnTheDocumentFeed()
    {
        // A regenerated packet, or a stalled kind that finally rendered. Sending another intake here
        // would be a full-appointment push to deliver one file.
        var (handler, documentQueue, intakeQueue) = Build(AllThreeGenerated(), hasExistingIntake: true);

        await handler.HandleEventAsync(Event(PacketKind.AttorneyClaimExaminer));

        await documentQueue.Received(1).EnqueueDocumentEntriesAsync(
            AppointmentId,
            TenantId,
            Arg.Is<IReadOnlyList<IntakeDocumentEntry>>(list =>
                list.Count == 3 && list.All(e => e.Source == DocumentEntryMapper.PacketSource)),
            Arg.Any<CancellationToken>());
        await intakeQueue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    /// <summary>
    /// Phase 4e (2026-08-06) -- the REPLACEMENT's settling packet set becomes its intake, which is
    /// how the second case is opened. 4d suppressed exactly this while the contract still told the
    /// receiver a reschedule was one case with a changed date; 4e makes the two-case shape the
    /// documented truth, so the first-contact branch runs normally.
    /// </summary>
    [Fact]
    public async Task WhenTheReplacementHalfsPacketsSettle_TheSetBecomesItsIntake()
    {
        var (handler, documentQueue, intakeQueue) = Build(
            AllThreeGenerated(),
            rescheduledFromAppointmentId: new Guid("7a1b2c3d-4e5f-4061-8273-9a0b1c2d3e4f"));

        await handler.HandleEventAsync(Event(PacketKind.AttorneyClaimExaminer));

        await intakeQueue.Received(1).EnqueueIntakeAsync(AppointmentId, TenantId, Arg.Any<CancellationToken>());
        await documentQueue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(default, default, default!, default);
    }

    /// <summary>
    /// Phase 4e (2026-08-06) -- the old half keeps its packets as a historical record and is not
    /// regenerated, so this is the narrower case of one stalled kind finally rendering after the
    /// close. It belongs on the DOCUMENT feed, not as another intake: the case already exists.
    /// </summary>
    [Theory]
    [InlineData(AppointmentStatusType.RescheduledNoBill)]
    [InlineData(AppointmentStatusType.RescheduledLate)]
    public async Task WhenTheOldHalfsPacketsSettleAgain_TheyGoOnTheDocumentFeed(AppointmentStatusType status)
    {
        var (handler, documentQueue, intakeQueue) = Build(
            AllThreeGenerated(), status: status, hasExistingIntake: true);

        await handler.HandleEventAsync(Event(PacketKind.AttorneyClaimExaminer));

        await documentQueue.Received(1).EnqueueDocumentEntriesAsync(
            AppointmentId,
            TenantId,
            Arg.Any<IReadOnlyList<IntakeDocumentEntry>>(),
            Arg.Any<CancellationToken>());
        await intakeQueue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Fact]
    public async Task PublishedPacketsClaimPdf_NotDocx()
    {
        var (handler, documentQueue, _) = Build(AllThreeGenerated(), hasExistingIntake: true);

        await handler.HandleEventAsync(Event(PacketKind.Doctor));

        await documentQueue.Received(1).EnqueueDocumentEntriesAsync(
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
        var (handler, documentQueue, intakeQueue) = Build(new List<AppointmentPacket>
        {
            NewPacket(PacketKind.Patient, PacketGenerationStatus.Generated),
            NewPacket(PacketKind.Doctor, PacketGenerationStatus.Generated),
            NewPacket(PacketKind.AttorneyClaimExaminer, PacketGenerationStatus.Failed),
        });

        await handler.HandleEventAsync(Event(PacketKind.Doctor));

        await documentQueue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(default, default, default!, default);
        await intakeQueue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Fact]
    public async Task WhenTheAppointmentWasNeverApproved_NothingIsQueued()
    {
        var (handler, documentQueue, intakeQueue) = Build(AllThreeGenerated(), status: AppointmentStatusType.Pending);

        await handler.HandleEventAsync(Event(PacketKind.Patient));

        await documentQueue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(default, default, default!, default);
        await intakeQueue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
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
