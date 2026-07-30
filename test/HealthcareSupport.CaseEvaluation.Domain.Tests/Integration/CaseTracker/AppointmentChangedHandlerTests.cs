using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
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

    private static Appointment NewAppointment(Guid id, AppointmentStatusType status) =>
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

    private static (AppointmentChangedHandler Handler, ICaseTrackerIntakeQueue Queue) Build(
        List<Appointment>? patientAppointments = null,
        bool queueThrows = false)
    {
        var repo = Substitute.For<IRepository<Appointment, Guid>>();
        repo.GetListAsync(
                Arg.Any<Expression<Func<Appointment, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(patientAppointments ?? new List<Appointment>()));

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
            new AppointmentChangedHandler(repo, queue, NullLogger<AppointmentChangedHandler>.Instance),
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
    [InlineData(AppointmentStatusType.RescheduledNoBill)]
    [InlineData(AppointmentStatusType.CancellationRequested)]
    public async Task LifecycleChangesAfterApprovalArePushed(AppointmentStatusType status)
    {
        // This is what subsumes the narrower cancel/reschedule trigger: any post-approval state change
        // is just another edit to an appointment their side already holds.
        var (handler, queue) = Build();

        await handler.HandleEventAsync(
            new EntityUpdatedEventData<Appointment>(NewAppointment(AppointmentId, status)));

        await queue.Received(1).EnqueueIntakeAsync(AppointmentId, TenantId, Arg.Any<CancellationToken>());
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
