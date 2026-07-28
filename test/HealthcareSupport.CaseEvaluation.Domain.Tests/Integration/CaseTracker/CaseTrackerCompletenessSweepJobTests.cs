using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
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

    private static Appointment NewAppointment(Guid id, AppointmentStatusType status) =>
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
        };

    private static IntegrationOutboxItem IntakeRowFor(Guid appointmentId)
    {
        return new IntegrationOutboxItem(
            Guid.NewGuid(), OfficeId, IntegrationMessageType.Intake,
            CaseTrackerEndpoints.Intake, appointmentId, "{\"data\":{}}",
            "key-" + appointmentId.ToString("N"));
    }

    private static (CaseTrackerCompletenessSweepJob Job, ICaseTrackerIntakeQueue Queue) Build(
        List<Appointment> appointments,
        List<IntegrationOutboxItem> outboxRows)
    {
        var runner = Substitute.For<ITenantWorkRunner>();
        runner.ForEachOfficeAsync(Arg.Any<Func<Guid, Task>>())
            .Returns(ci => ci.Arg<Func<Guid, Task>>()(OfficeId));

        var appointmentRepo = Substitute.For<IRepository<Appointment, Guid>>();
        appointmentRepo.GetQueryableAsync().Returns(_ => appointments.AsQueryable());

        var outboxRepo = Substitute.For<IIntegrationOutboxRepository>();
        outboxRepo.GetQueryableAsync().Returns(_ => outboxRows.AsQueryable());

        var queue = Substitute.For<ICaseTrackerIntakeQueue>();

        return (
            new CaseTrackerCompletenessSweepJob(
                runner, appointmentRepo, outboxRepo, queue,
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
