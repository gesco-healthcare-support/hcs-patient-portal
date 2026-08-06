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
using HealthcareSupport.CaseEvaluation.Patients;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the change trigger that keeps the Case Tracker current. All fixture data is
/// synthetic.
///
/// <para>The rule under test is WHICH appointments re-push. An unpublished appointment must enqueue
/// nothing (there is no case to update, and a push would 404 and dead-letter), while a patient edit
/// must reach every one of that patient's published appointments -- demographics appear in all of
/// them, not just the latest.</para>
/// </summary>
public class AppointmentChangedHandlerTests
{
    private static readonly Guid TenantId = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");
    private static readonly Guid SecondAppointmentId = new("3c9d1b77-2e40-4a51-8bb2-77f0a1c9d233");
    private static readonly Guid PatientId = new("e5f6a7b8-c9d0-4e1f-a2b3-c4d5e6f7a8bc");

    private static Appointment NewAppointment(
        Guid id,
        AppointmentStatusType status,
        Guid? rescheduledFromAppointmentId = null) =>
        new(
            id,
            PatientId,
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

    private static Patient NewPatient() =>
        new(
            PatientId,
            stateId: null,
            appointmentLanguageId: null,
            identityUserId: null,
            tenantId: TenantId,
            firstName: "Sample",
            lastName: "Testcase",
            email: "sample.testcase@example.test",
            genderId: default,
            dateOfBirth: new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            phoneNumberTypeId: default);

    private static readonly DateTime Now = new(2026, 7, 30, 20, 0, 0, DateTimeKind.Utc);

    private static AppointmentPacket NewPacket(
        Guid appointmentId,
        PacketKind kind,
        PacketGenerationStatus status,
        DateTime lastChanged) =>
        new(
            Guid.NewGuid(),
            TenantId,
            appointmentId,
            kind,
            blobName: "tenantseg/apptseg/packet/patient/228d6bed62e04be7b1146e58629bf901.pdf",
            status: status)
        {
            GeneratedAt = status == PacketGenerationStatus.Generated ? lastChanged : default,
            CreationTime = lastChanged,
        };

    /// <summary>A settled (fully generated) set -- the state after packet rendering finishes.</summary>
    private static List<AppointmentPacket> SettledPackets(Guid appointmentId) =>
        PacketSetPolicy.AllKinds
            .Select(k => NewPacket(appointmentId, k, PacketGenerationStatus.Generated, Now))
            .ToList();

    /// <summary>Mid-render: what an approval looks like in the seconds after it commits.</summary>
    private static List<AppointmentPacket> RenderingPackets(Guid appointmentId) =>
        PacketSetPolicy.AllKinds
            .Select(k => NewPacket(appointmentId, k, PacketGenerationStatus.Generating, Now))
            .ToList();

    private static (AppointmentChangedHandler Handler, ICaseTrackerIntakeQueue Queue) Build(
        List<Appointment>? patientAppointments = null,
        bool queueThrows = false,
        Func<Guid, List<AppointmentPacket>>? packetsFor = null)
    {
        var repo = Substitute.For<IRepository<Appointment, Guid>>();
        repo.GetListAsync(
                Arg.Any<Expression<Func<Appointment, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(patientAppointments ?? new List<Appointment>()));

        // The settle gate re-reads the appointment, so FindAsync must answer for every id under test.
        // NSubstitute returns null for an unstubbed class-returning member, which would silently make
        // every re-push assertion fail for the wrong reason.
        repo.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var id = ci.ArgAt<Guid>(0);
                var known = patientAppointments?.FirstOrDefault(a => a.Id == id);
                return Task.FromResult<Appointment?>(
                    known ?? NewAppointment(id, AppointmentStatusType.Approved));
            });

        var packetRepo = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        packetRepo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentPacket, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult((packetsFor ?? SettledPackets)(AppointmentId)));

        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        var queue = Substitute.For<ICaseTrackerIntakeQueue>();
        if (queueThrows)
        {
            queue.EnqueueIntakeAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
                .Throws(new InvalidOperationException("payload build failed"));
        }
        else
        {
            queue.EnqueueIntakeAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
                .Returns(ci => Task.FromResult(new IntegrationOutboxItem(
                    Guid.NewGuid(),
                    ci.ArgAt<Guid?>(1),
                    IntegrationMessageType.Intake,
                    CaseTrackerEndpoints.Intake,
                    ci.ArgAt<Guid>(0),
                    "{\"data\":{}}",
                    "key-" + ci.ArgAt<Guid>(0).ToString("N"))));
        }

        return (
            new AppointmentChangedHandler(
                repo, packetRepo, queue, clock, NullLogger<AppointmentChangedHandler>.Instance),
            queue);
    }

    [Fact]
    public async Task WhenAnApprovedAppointmentIsEdited_ARePushIsQueued()
    {
        var (handler, queue) = Build();

        await handler.HandleEventAsync(
            new EntityUpdatedEventData<Appointment>(NewAppointment(AppointmentId, AppointmentStatusType.Approved)));

        await queue.Received(1).EnqueueIntakeAsync(AppointmentId, TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhileItsPacketsAreStillRendering_NothingIsQueued()
    {
        // REGRESSION, measured in production 2026-07-30 (falkinstein A00004). Approval is itself an
        // appointment update, so this handler fires on the approval and -- without this gate -- pushed
        // a packet-less intake that the settle path superseded ten seconds later. Deleting the approval
        // trigger alone did NOT fix that: the two enqueues had merely collapsed onto one row because
        // they shared an updatedAt. If this test ever passes trivially, check the gate still exists.
        var (handler, queue) = Build(packetsFor: RenderingPackets);

        await handler.HandleEventAsync(
            new EntityUpdatedEventData<Appointment>(NewAppointment(AppointmentId, AppointmentStatusType.Approved)));

        await queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Fact]
    public async Task WhenAStuckPacketSetPassesTheCutoff_TheEditIsPushedAnyway()
    {
        // A permanently failed template must not withhold the appointment forever -- the appointment
        // itself is the news, and a withheld intake is a case their staff never see.
        var stale = Now.AddHours(-2);
        var (handler, queue) = Build(packetsFor: id => new List<AppointmentPacket>
        {
            NewPacket(id, PacketKind.Patient, PacketGenerationStatus.Generated, stale),
            NewPacket(id, PacketKind.Doctor, PacketGenerationStatus.Failed, stale),
            NewPacket(id, PacketKind.AttorneyClaimExaminer, PacketGenerationStatus.Failed, stale),
        });

        await handler.HandleEventAsync(
            new EntityUpdatedEventData<Appointment>(NewAppointment(AppointmentId, AppointmentStatusType.Approved)));

        await queue.Received(1).EnqueueIntakeAsync(AppointmentId, TenantId, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(AppointmentStatusType.Pending)]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.InfoRequested)]
    public async Task WhenAnUnpublishedAppointmentIsEdited_NothingIsQueued(AppointmentStatusType status)
    {
        var (handler, queue) = Build();

        await handler.HandleEventAsync(
            new EntityUpdatedEventData<Appointment>(NewAppointment(AppointmentId, status)));

        await queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Theory]
    [InlineData(AppointmentStatusType.CancelledLate)]
    [InlineData(AppointmentStatusType.CancelledNoBill)]
    [InlineData(AppointmentStatusType.CancellationRequested)]
    public async Task LifecycleChangesAfterApprovalArePushed(AppointmentStatusType status)
    {
        // This is what subsumes the narrower cancel/reschedule trigger: any post-approval state change
        // is just another edit to an appointment their side already holds.
        //
        // Phase 4d (2026-08-05) CHANGED THIS TEST TWICE. RescheduledNoBill was one of the cases here;
        // it moved to the suppression tests below because 4d closes the OLD half of a split into that
        // status and it must stay off the wire until 4e. And the appointment is now passed to Build so
        // FindAsync answers with THIS status: the settle gate re-reads the row, and the stub used to
        // hand back an Approved one regardless, so this theory never actually exercised its own
        // parameter past the IsPublished check.
        var appointment = NewAppointment(AppointmentId, status);
        var (handler, queue) = Build(new List<Appointment> { appointment });

        await handler.HandleEventAsync(new EntityUpdatedEventData<Appointment>(appointment));

        await queue.Received(1).EnqueueIntakeAsync(AppointmentId, TenantId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Phase 4d T9 (2026-08-05), DELETED IN 4E -- the OLD half of a reschedule split. Its terminal
    /// status is published by <see cref="CaseTrackerPublishPolicy"/>, so silence needs the gate:
    /// left alone it would re-push the old appointment carrying a NoBill/Late billing status, telling
    /// the receiver the case is closed before 4e teaches it what that means.
    /// </summary>
    [Theory]
    [InlineData(AppointmentStatusType.RescheduledNoBill)]
    [InlineData(AppointmentStatusType.RescheduledLate)]
    public async Task WhenTheOldHalfOfARescheduleSplitIsEdited_NothingIsQueued(AppointmentStatusType status)
    {
        var appointment = NewAppointment(AppointmentId, status);
        var (handler, queue) = Build(new List<Appointment> { appointment });

        await handler.HandleEventAsync(new EntityUpdatedEventData<Appointment>(appointment));

        await queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    /// <summary>
    /// Phase 4d T10 (2026-08-05), DELETED IN 4E -- the NEW half. Suppressing only the packet-settle
    /// path would leave this hole: the replacement is Approved, so ANY later edit to it re-pushes,
    /// and with no intake row yet that push IS its first contact -- the second case for one claim
    /// that the whole suppression exists to prevent.
    /// </summary>
    [Fact]
    public async Task WhenTheReplacementHalfOfARescheduleSplitIsEdited_NothingIsQueued()
    {
        var replacement = NewAppointment(
            SecondAppointmentId,
            AppointmentStatusType.Approved,
            rescheduledFromAppointmentId: AppointmentId);
        var (handler, queue) = Build(new List<Appointment> { replacement });

        await handler.HandleEventAsync(new EntityUpdatedEventData<Appointment>(replacement));

        await queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Fact]
    public async Task WhenAPatientIsEdited_BothHalvesOfARescheduleSplitAreSkipped()
    {
        // The demographic fan-out is a second way into the same push. A gate keyed on the finalize
        // path would let a name correction publish the split an hour later.
        var normal = NewAppointment(AppointmentId, AppointmentStatusType.Approved);
        var closed = NewAppointment(
            new Guid("7a1b2c3d-4e5f-4061-8273-9a0b1c2d3e4f"), AppointmentStatusType.RescheduledNoBill);
        var replacement = NewAppointment(
            SecondAppointmentId, AppointmentStatusType.Approved, rescheduledFromAppointmentId: closed.Id);

        var (handler, queue) = Build(new List<Appointment> { normal, closed, replacement });

        await handler.HandleEventAsync(new EntityUpdatedEventData<Patient>(NewPatient()));

        await queue.Received(1).EnqueueIntakeAsync(AppointmentId, TenantId, Arg.Any<CancellationToken>());
        await queue.DidNotReceive().EnqueueIntakeAsync(closed.Id, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await queue.DidNotReceive().EnqueueIntakeAsync(
            SecondAppointmentId, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAPatientIsEdited_EveryPublishedAppointmentIsRePushed()
    {
        var (handler, queue) = Build(new List<Appointment>
        {
            NewAppointment(AppointmentId, AppointmentStatusType.Approved),
            NewAppointment(SecondAppointmentId, AppointmentStatusType.Approved),
        });

        await handler.HandleEventAsync(new EntityUpdatedEventData<Patient>(NewPatient()));

        await queue.Received(1).EnqueueIntakeAsync(AppointmentId, TenantId, Arg.Any<CancellationToken>());
        await queue.Received(1).EnqueueIntakeAsync(SecondAppointmentId, TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAPatientIsEdited_UnpublishedAppointmentsAreSkipped()
    {
        var (handler, queue) = Build(new List<Appointment>
        {
            NewAppointment(AppointmentId, AppointmentStatusType.Approved),
            NewAppointment(SecondAppointmentId, AppointmentStatusType.Pending),
        });

        await handler.HandleEventAsync(new EntityUpdatedEventData<Patient>(NewPatient()));

        await queue.Received(1).EnqueueIntakeAsync(AppointmentId, TenantId, Arg.Any<CancellationToken>());
        await queue.DidNotReceive().EnqueueIntakeAsync(
            SecondAppointmentId, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAPatientHasNoAppointments_NothingIsQueued()
    {
        var (handler, queue) = Build(new List<Appointment>());

        await handler.HandleEventAsync(new EntityUpdatedEventData<Patient>(NewPatient()));

        await queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Fact]
    public async Task WhenQueueingFails_TheEditItselfStillSucceeds()
    {
        var (handler, _) = Build(queueThrows: true);

        await Should.NotThrowAsync(() => handler.HandleEventAsync(
            new EntityUpdatedEventData<Appointment>(NewAppointment(AppointmentId, AppointmentStatusType.Approved))));
    }

    [Fact]
    public async Task WhenOnePatientAppointmentFails_TheOthersStillPush()
    {
        // One bad appointment must not strand the rest of the patient's corrections.
        var (handler, queue) = Build(new List<Appointment>
        {
            NewAppointment(AppointmentId, AppointmentStatusType.Approved),
            NewAppointment(SecondAppointmentId, AppointmentStatusType.Approved),
        });
        queue.EnqueueIntakeAsync(AppointmentId, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("payload build failed"));

        await handler.HandleEventAsync(new EntityUpdatedEventData<Patient>(NewPatient()));

        await queue.Received(1).EnqueueIntakeAsync(
            SecondAppointmentId, TenantId, Arg.Any<CancellationToken>());
    }
}
