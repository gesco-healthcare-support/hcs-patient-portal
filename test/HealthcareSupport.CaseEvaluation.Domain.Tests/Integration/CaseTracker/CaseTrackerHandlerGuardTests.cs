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
/// The early exits of the Case Tracker trigger handlers that their main test classes do not reach: a
/// null event, an appointment that has disappeared, and a patient lookup that throws. Each exit must
/// publish NOTHING and must not throw, because every one of these handlers runs after the business
/// action has already committed. Each exit sits beside a sibling Fact on the same harness that DOES
/// publish, so a harness that could never publish cannot make the exits pass. All data is synthetic.
/// </summary>
public class CaseTrackerHandlerGuardTests
{
    private static readonly Guid TenantId = new("5a1c7e22-8d3b-4f60-9e1a-2b7c4d9e0f13");
    private static readonly Guid AppointmentId = new("6b2d8f33-9e4c-4071-8f2b-3c8d5e0f1a24");
    private static readonly Guid PatientId = new("7c3e9044-af5d-4182-9a3c-4d9e6f1a2b35");
    private static readonly Guid DocumentId = new("8d4fa155-b06e-4293-8b4d-5eaf7a2b3c46");
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    private static Appointment NewAppointment(AppointmentStatusType status) =>
        new(
            AppointmentId,
            PatientId,
            identityUserId: null,
            appointmentTypeId: new Guid("9e50b266-c17f-43a4-9c5e-6fb08b3c4d57"),
            locationId: new Guid("af61c377-d28a-44b5-8d6f-70c19c4d5e68"),
            doctorAvailabilityId: new Guid("b072d488-e39b-45c6-9e70-81d2ad5e6f79"),
            appointmentDate: new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A90071",
            appointmentStatus: status,
            panelNumber: "TEST-PANEL")
        {
            TenantId = TenantId,
        };

    private static Patient NewPatient() =>
        new(
            PatientId,
            stateId: null,
            appointmentLanguageId: null,
            identityUserId: null,
            tenantId: TenantId,
            firstName: "TEST-Guard",
            lastName: "TEST-Patient",
            email: "TEST-guard-patient@test.local",
            genderId: default,
            dateOfBirth: new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            phoneNumberTypeId: default);

    /// <summary>A generated packet set, so the settle gate never holds the re-push back.</summary>
    private static List<AppointmentPacket> SettledPackets() =>
        PacketSetPolicy.AllKinds
            .Select(k => new AppointmentPacket(
                Guid.NewGuid(),
                TenantId,
                AppointmentId,
                k,
                blobName: "tenantseg/apptseg/packet/guard/0f1e2d3c4b5a69788796a5b4c3d2e1f0.pdf",
                status: PacketGenerationStatus.Generated)
            {
                GeneratedAt = Now.AddHours(-1),
                CreationTime = Now.AddHours(-1),
            })
            .ToList();

    // ------------------------------------------------------------------------
    // AppointmentChangedHandler
    // ------------------------------------------------------------------------

    private sealed class ChangedHarness
    {
        public AppointmentChangedHandler Handler { get; init; } = null!;
        public IRepository<Appointment, Guid> Appointments { get; init; } = null!;
        public ICaseTrackerIntakeQueue Queue { get; init; } = null!;
    }

    private static ChangedHarness BuildChanged()
    {
        var appointments = Substitute.For<IRepository<Appointment, Guid>>();
        appointments.FindAsync(AppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Appointment?>(NewAppointment(AppointmentStatusType.Approved)));
        appointments.GetListAsync(
                Arg.Any<Expression<Func<Appointment, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<Appointment> { NewAppointment(AppointmentStatusType.Approved) }));

        var packets = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        packets.GetListAsync(
                Arg.Any<Expression<Func<AppointmentPacket, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(SettledPackets()));

        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        var queue = Substitute.For<ICaseTrackerIntakeQueue>();

        return new ChangedHarness
        {
            Handler = new AppointmentChangedHandler(
                appointments, packets, queue, clock, NullLogger<AppointmentChangedHandler>.Instance),
            Appointments = appointments,
            Queue = queue,
        };
    }

    [Fact]
    public async Task AppointmentEdit_OnTheGuardHarness_IsRePushed()
    {
        var h = BuildChanged();

        await h.Handler.HandleEventAsync(
            new EntityUpdatedEventData<Appointment>(NewAppointment(AppointmentStatusType.Approved)));

        await h.Queue.Received(1).EnqueueIntakeAsync(AppointmentId, TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NullAppointmentEvent_PushesNothing()
    {
        var h = BuildChanged();

        await h.Handler.HandleEventAsync((EntityUpdatedEventData<Appointment>)null!);

        await h.Queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Fact]
    public async Task AppointmentGoneByTheTimeOfTheRePush_PushesNothing()
    {
        var h = BuildChanged();
        h.Appointments.FindAsync(AppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Appointment?>(null));

        await h.Handler.HandleEventAsync(
            new EntityUpdatedEventData<Appointment>(NewAppointment(AppointmentStatusType.Approved)));

        await h.Queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Fact]
    public async Task PatientEdit_OnTheGuardHarness_RePushesTheirPublishedAppointment()
    {
        var h = BuildChanged();

        await h.Handler.HandleEventAsync(new EntityUpdatedEventData<Patient>(NewPatient()));

        await h.Queue.Received(1).EnqueueIntakeAsync(AppointmentId, TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NullPatientEvent_PushesNothing()
    {
        var h = BuildChanged();

        await h.Handler.HandleEventAsync((EntityUpdatedEventData<Patient>)null!);

        await h.Queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Fact]
    public async Task PatientEdit_WhoseAppointmentLookupThrows_IsSwallowed_AndPushesNothing()
    {
        // The patient edit has already committed; a failed re-push must not surface as a failed save.
        var h = BuildChanged();
        h.Appointments.GetListAsync(
                Arg.Any<Expression<Func<Appointment, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("TEST-appointment-lookup-down"));

        await Should.NotThrowAsync(
            () => h.Handler.HandleEventAsync(new EntityUpdatedEventData<Patient>(NewPatient())));

        await h.Queue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    // ------------------------------------------------------------------------
    // DocumentAcceptedHandler
    // ------------------------------------------------------------------------

    private static (DocumentAcceptedHandler Handler, ICaseTrackerDocumentQueue Queue) BuildAccepted(bool appointmentExists)
    {
        var appointments = Substitute.For<IRepository<Appointment, Guid>>();
        appointments.FindAsync(AppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Appointment?>(
                appointmentExists ? NewAppointment(AppointmentStatusType.Approved) : null));

        var resolver = Substitute.For<IDocumentListResolver>();
        resolver.ResolveDocumentAsync(DocumentId, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IntakeDocumentEntry?>(new IntakeDocumentEntry
            {
                Id = DocumentId,
                Source = DocumentEntryMapper.DocumentSource,
                DocumentName = "TEST-Guard document",
                FileName = "TEST-guard.pdf",
                Status = nameof(DocumentStatus.Accepted),
            }));

        var queue = Substitute.For<ICaseTrackerDocumentQueue>();

        return (
            new DocumentAcceptedHandler(appointments, resolver, queue, NullLogger<DocumentAcceptedHandler>.Instance),
            queue);
    }

    private static AppointmentDocumentAcceptedEto AcceptedEvent() => new()
    {
        AppointmentId = AppointmentId,
        AppointmentDocumentId = DocumentId,
        TenantId = TenantId,
        AcceptedByUserId = new Guid("c183e599-f4ac-46d7-8f81-92e3be6f7a80"),
        OccurredAt = Now,
    };

    [Fact]
    public async Task AcceptedDocument_OnTheGuardHarness_IsQueued()
    {
        var (handler, queue) = BuildAccepted(appointmentExists: true);

        await handler.HandleEventAsync(AcceptedEvent());

        await queue.Received(1).EnqueueDocumentEntriesAsync(
            AppointmentId,
            TenantId,
            Arg.Is<IReadOnlyList<IntakeDocumentEntry>>(list => list.Count == 1 && list[0].Id == DocumentId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptedDocument_OnAnAppointmentThatNoLongerExists_QueuesNothing()
    {
        var (handler, queue) = BuildAccepted(appointmentExists: false);

        await handler.HandleEventAsync(AcceptedEvent());

        await queue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(default, default, default!, default);
    }

    [Fact]
    public async Task NullAcceptedEvent_QueuesNothing()
    {
        var (handler, queue) = BuildAccepted(appointmentExists: true);

        await handler.HandleEventAsync(null!);

        await queue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(default, default, default!, default);
    }

    // ------------------------------------------------------------------------
    // DocumentDeletedHandler and DocumentRejectedHandler
    // ------------------------------------------------------------------------

    private static (IRepository<Appointment, Guid> Appointments, ICaseTrackerDocumentQueue Queue, IClock Clock) BuildRemoval()
    {
        var appointments = Substitute.For<IRepository<Appointment, Guid>>();
        appointments.FindAsync(AppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Appointment?>(NewAppointment(AppointmentStatusType.Approved)));

        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        return (appointments, Substitute.For<ICaseTrackerDocumentQueue>(), clock);
    }

    [Fact]
    public async Task DeletedDocument_OnTheGuardHarness_QueuesATombstone()
    {
        var (appointments, queue, clock) = BuildRemoval();
        var handler = new DocumentDeletedHandler(appointments, queue, clock, NullLogger<DocumentDeletedHandler>.Instance);

        await handler.HandleEventAsync(new AppointmentDocumentDeletedEto
        {
            AppointmentId = AppointmentId,
            AppointmentDocumentId = DocumentId,
            TenantId = TenantId,
            DeletedByUserId = new Guid("d294f6aa-05bd-47e8-9092-a3f4cf708b91"),
            OccurredAt = Now,
        });

        await queue.Received(1).EnqueueDeletionsAsync(
            AppointmentId,
            TenantId,
            Arg.Is<IReadOnlyList<DocumentDeletionEntry>>(list => list.Count == 1 && list[0].Id == DocumentId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NullDeletedEvent_QueuesNothing()
    {
        var (appointments, queue, clock) = BuildRemoval();
        var handler = new DocumentDeletedHandler(appointments, queue, clock, NullLogger<DocumentDeletedHandler>.Instance);

        await handler.HandleEventAsync(null!);

        await queue.DidNotReceiveWithAnyArgs().EnqueueDeletionsAsync(default, default, default!, default);
    }

    [Fact]
    public async Task RejectedDocument_OnTheGuardHarness_QueuesATombstone()
    {
        var (appointments, queue, clock) = BuildRemoval();
        var handler = new DocumentRejectedHandler(appointments, queue, clock, NullLogger<DocumentRejectedHandler>.Instance);

        await handler.HandleEventAsync(new AppointmentDocumentRejectedEto
        {
            AppointmentId = AppointmentId,
            AppointmentDocumentId = DocumentId,
            TenantId = TenantId,
            RejectionNotes = "TEST-illegible",
            RejectedByUserId = new Guid("e3a507bb-16ce-48f9-81a3-b40fd0819ca2"),
            OccurredAt = Now,
        });

        await queue.Received(1).EnqueueDeletionsAsync(
            AppointmentId,
            TenantId,
            Arg.Is<IReadOnlyList<DocumentDeletionEntry>>(list => list.Count == 1 && list[0].Id == DocumentId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NullRejectedEvent_QueuesNothing()
    {
        var (appointments, queue, clock) = BuildRemoval();
        var handler = new DocumentRejectedHandler(appointments, queue, clock, NullLogger<DocumentRejectedHandler>.Instance);

        await handler.HandleEventAsync(null!);

        await queue.DidNotReceiveWithAnyArgs().EnqueueDeletionsAsync(default, default, default!, default);
    }
}
