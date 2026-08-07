using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the completeness sweep -- the only thing that catches an approval whose enqueue itself
/// threw, leaving NO outbox row and therefore nothing to retry, dead-letter or alert on.
///
/// <para>All fixture data is synthetic.</para>
/// </summary>
public class CaseTrackerCompletenessSweepJobTests
{
    private static readonly Guid OfficeId = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid WithRow = new("ada5e3c5-0034-ebde-253c-3a2293631dee");
    private static readonly Guid WithoutRow = new("3c9d1b77-2e40-4a51-8bb2-77f0a1c9d233");

    private static readonly DateTime Now = new(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);

    private static Appointment NewAppointment(
        Guid id,
        AppointmentStatusType status,
        DateTime? changedAt = null,
        Guid? rescheduledFromAppointmentId = null) =>
        new(
            id,
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
            TenantId = OfficeId,
            // Must be set explicitly: an unsaved entity's CreationTime is default(DateTime), which is
            // outside any lookback window, so every case here would silently stop exercising the sweep.
            CreationTime = changedAt ?? Now.AddHours(-1),
            RescheduledFromAppointmentId = rescheduledFromAppointmentId,
        };

    private static IntegrationOutboxItem IntakeRowFor(Guid appointmentId)
    {
        return new IntegrationOutboxItem(
            Guid.NewGuid(), OfficeId, IntegrationMessageType.Intake,
            CaseTrackerEndpoints.Intake, appointmentId, "{\"data\":{}}",
            "key-" + appointmentId.ToString("N"));
    }

    /// <summary>
    /// A settled (fully generated) packet set. This is the DEFAULT for these tests because the sweep
    /// only recovers appointments whose packets have settled -- since 2026-07-30 "published with no
    /// intake row" is the normal mid-render state, so recovering unconditionally would race the
    /// deferral and send the packet-less intake the deferral exists to prevent.
    /// </summary>
    private static List<AppointmentPacket> SettledPackets(Guid appointmentId) =>
        PacketSetPolicy.AllKinds
            .Select(k => new AppointmentPacket(
                Guid.NewGuid(),
                OfficeId,
                appointmentId,
                k,
                blobName: "tenantseg/apptseg/packet/patient/228d6bed62e04be7b1146e58629bf901.pdf",
                status: PacketGenerationStatus.Generated)
            {
                GeneratedAt = Now.AddHours(-1),
                CreationTime = Now.AddHours(-1),
            })
            .ToList();

    private static (CaseTrackerCompletenessSweepJob Job, ICaseTrackerIntakeQueue Queue) Build(
        List<Appointment> appointments,
        List<IntegrationOutboxItem> outboxRows,
        Func<Guid, List<AppointmentPacket>>? packetsFor = null)
    {
        var runner = Substitute.For<ITenantWorkRunner>();
        runner.ForEachOfficeAsync(Arg.Any<Func<Guid, Task>>())
            .Returns(ci => ci.Arg<Func<Guid, Task>>()(OfficeId));

        var appointmentRepo = Substitute.For<IRepository<Appointment, Guid>>();
        appointmentRepo.GetQueryableAsync().Returns(_ => appointments.AsQueryable());

        var packetRepo = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        packetRepo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentPacket, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult((packetsFor ?? SettledPackets)(WithoutRow)));

        var outboxRepo = Substitute.For<IIntegrationOutboxRepository>();
        outboxRepo.GetQueryableAsync().Returns(_ => outboxRows.AsQueryable());

        var queue = Substitute.For<ICaseTrackerIntakeQueue>();

        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        return (
            new CaseTrackerCompletenessSweepJob(
                runner, appointmentRepo, packetRepo, outboxRepo, queue, clock,
                NullLogger<CaseTrackerCompletenessSweepJob>.Instance),
            queue);
    }

    [Fact]
    public async Task AnApprovedAppointmentWithNoRow_IsEnqueued()
    {
        var (job, queue) = Build(
            new List<Appointment> { NewAppointment(WithoutRow, AppointmentStatusType.Approved) },
            new List<IntegrationOutboxItem>());

        await job.ExecuteAsync();

        await queue.Received(1).EnqueueIntakeAsync(WithoutRow, OfficeId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhilePacketsAreStillRendering_TheSweepLeavesItToTheSettlePath()
    {
        // Without this the sweep races the 2026-07-30 deferral: an approval at 10:59 whose packets land
        // at 11:00:30 would get a packet-less intake from the 11:00 run, reintroducing the double push.
        var (job, queue) = Build(
            new List<Appointment> { NewAppointment(WithoutRow, AppointmentStatusType.Approved) },
            new List<IntegrationOutboxItem>(),
            packetsFor: id => PacketSetPolicy.AllKinds
                .Select(k => new AppointmentPacket(
                    Guid.NewGuid(), OfficeId, id, k,
                    blobName: "tenantseg/apptseg/packet/patient/228d6bed62e04be7b1146e58629bf901.pdf",
                    status: PacketGenerationStatus.Generating)
                {
                    CreationTime = Now,
                })
                .ToList());

        await job.ExecuteAsync();

        await queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Fact]
    public async Task WhenPacketGenerationNeverCreatedRows_TheSweepStillRecoversItEventually()
    {
        // No packet rows at all -- generation itself never ran. The appointment's own stamp stands in,
        // so once it is older than the settle cutoff the intake is recovered rather than withheld
        // forever waiting for packets that will never exist.
        var (job, queue) = Build(
            new List<Appointment>
            {
                NewAppointment(WithoutRow, AppointmentStatusType.Approved, Now.AddHours(-2)),
            },
            new List<IntegrationOutboxItem>(),
            packetsFor: _ => new List<AppointmentPacket>());

        await job.ExecuteAsync();

        await queue.Received(1).EnqueueIntakeAsync(WithoutRow, OfficeId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnAppointmentThatAlreadyHasARow_IsLeftAlone()
    {
        var (job, queue) = Build(
            new List<Appointment> { NewAppointment(WithRow, AppointmentStatusType.Approved) },
            new List<IntegrationOutboxItem> { IntakeRowFor(WithRow) });

        await job.ExecuteAsync();

        await queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Fact]
    public async Task ARowInAnyState_CountsAsExisting()
    {
        // Pending, Sent, Failed and Resolved all mean "something was written". The sweep only cares
        // about the case where NOTHING was.
        var failed = IntakeRowFor(WithRow);
        failed.MarkFatal(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc), "401");

        var (job, queue) = Build(
            new List<Appointment> { NewAppointment(WithRow, AppointmentStatusType.Approved) },
            new List<IntegrationOutboxItem> { failed });

        await job.ExecuteAsync();

        await queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Theory]
    [InlineData(AppointmentStatusType.Pending)]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.InfoRequested)]
    public async Task AnUnpublishedAppointment_IsIgnored(AppointmentStatusType status)
    {
        // These never had an intake pushed, so a missing row is correct rather than a fault.
        var (job, queue) = Build(
            new List<Appointment> { NewAppointment(WithoutRow, status) },
            new List<IntegrationOutboxItem>());

        await job.ExecuteAsync();

        await queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    /// <summary>
    /// Phase 4d T10 (2026-08-05), DELETED IN 4E. This job is the reason suppressing the two publish
    /// paths is not enough: a replacement appointment is Approved, settled and has NO intake row,
    /// which is EXACTLY this job's definition of a lost enqueue. Within the hour it would re-create
    /// the second case the other two gates just prevented -- and log it as a recovery, so the
    /// divergence would read as the system working.
    /// </summary>
    [Fact]
    public async Task TheNewHalfOfARescheduleSplit_IsNotRecovered()
    {
        var (job, queue) = Build(
            new List<Appointment>
            {
                NewAppointment(
                    WithoutRow,
                    AppointmentStatusType.Approved,
                    rescheduledFromAppointmentId: WithRow),
            },
            new List<IntegrationOutboxItem>());

        await job.ExecuteAsync();

        await queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Theory]
    [InlineData(AppointmentStatusType.RescheduledNoBill)]
    [InlineData(AppointmentStatusType.RescheduledLate)]
    public async Task TheOldHalfOfARescheduleSplit_IsNotRecovered(AppointmentStatusType status)
    {
        // Phase 4d T9 (2026-08-05), DELETED IN 4E. The old half normally HAS an intake row from its
        // approval, so the sweep would pass over it -- but not if that original enqueue was the one
        // that got lost, and then the recovery would carry its NoBill/Late close on the wire.
        var (job, queue) = Build(
            new List<Appointment> { NewAppointment(WithoutRow, status) },
            new List<IntegrationOutboxItem>());

        await job.ExecuteAsync();

        await queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Fact]
    public async Task ADocumentUpdateRow_DoesNotSatisfyTheIntakeCheck()
    {
        // A document update can exist without its intake having landed; the sweep looks for INTAKE.
        var documentRow = new IntegrationOutboxItem(
            Guid.NewGuid(), OfficeId, IntegrationMessageType.DocumentUpdate,
            CaseTrackerEndpoints.DocumentUpdate(WithoutRow), WithoutRow, "[]", "key-doc");

        var (job, queue) = Build(
            new List<Appointment> { NewAppointment(WithoutRow, AppointmentStatusType.Approved) },
            new List<IntegrationOutboxItem> { documentRow });

        await job.ExecuteAsync();

        await queue.Received(1).EnqueueIntakeAsync(WithoutRow, OfficeId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnAppointmentOlderThanTheLookback_IsIgnored()
    {
        // THE point of the window. Every appointment predating the integration has no intake row, so
        // without a floor the sweep would enqueue an office's entire history -- and enabling that
        // office would flush all of it to the Case Tracker as fresh intakes, creating duplicate cases
        // for work their staff already did by hand.
        var old = NewAppointment(
            WithoutRow,
            AppointmentStatusType.Approved,
            changedAt: Now.AddDays(-(CaseTrackerCompletenessSweepJob.LookbackDays + 1)));

        var (job, queue) = Build(
            new List<Appointment> { old },
            new List<IntegrationOutboxItem>());

        await job.ExecuteAsync();

        await queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Fact]
    public async Task AnAppointmentInsideTheLookback_IsStillEnqueued()
    {
        // The window must not defeat the job's actual purpose: a lost enqueue from earlier today.
        var recent = NewAppointment(
            WithoutRow,
            AppointmentStatusType.Approved,
            changedAt: Now.AddDays(-1));

        var (job, queue) = Build(
            new List<Appointment> { recent },
            new List<IntegrationOutboxItem>());

        await job.ExecuteAsync();

        await queue.Received(1).EnqueueIntakeAsync(WithoutRow, OfficeId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ARecentEditToAnOldAppointment_BringsItBackIntoScope()
    {
        // LastModificationTime wins over CreationTime, so an old appointment approved (or edited)
        // today is inside the window -- which is the case the sweep exists for.
        var reopened = NewAppointment(WithoutRow, AppointmentStatusType.Approved, changedAt: Now.AddYears(-2));
        reopened.LastModificationTime = Now.AddMinutes(-30);

        var (job, queue) = Build(
            new List<Appointment> { reopened },
            new List<IntegrationOutboxItem>());

        await job.ExecuteAsync();

        await queue.Received(1).EnqueueIntakeAsync(WithoutRow, OfficeId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheSweepIsCappedPerOffice()
    {
        var many = Enumerable.Range(0, CaseTrackerCompletenessSweepJob.BatchSize + 10)
            .Select(_ => NewAppointment(Guid.NewGuid(), AppointmentStatusType.Approved))
            .ToList();

        var (job, queue) = Build(many, new List<IntegrationOutboxItem>());

        await job.ExecuteAsync();

        await queue.Received(CaseTrackerCompletenessSweepJob.BatchSize)
            .EnqueueIntakeAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }
}
