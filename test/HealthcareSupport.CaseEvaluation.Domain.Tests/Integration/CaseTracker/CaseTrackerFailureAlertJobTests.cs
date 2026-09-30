using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the dead-letter alert. The behaviour that matters is the THROTTLE: the usual cause of a
/// dead letter is systemic, so the job must send one email covering a batch and must never re-send for a
/// row it has already reported. Since #917 the same job also sends a one-off early warning for pushes
/// that are still retrying. All fixture data is synthetic.
/// </summary>
public class CaseTrackerFailureAlertJobTests
{
    private static readonly Guid OfficeId = new("0ff1ce00-0000-4000-8000-000000000001");
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");
    private static readonly DateTime Now = new(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);

    private sealed class Harness
    {
        public CaseTrackerFailureAlertJob Job { get; init; } = null!;
        public ILocalEventBus Bus { get; init; } = null!;
        public List<IntegrationOutboxItem> Rows { get; init; } = null!;
        public IIntegrationOutboxRepository Repository { get; init; } = null!;
    }

    /// <summary>A push that has failed <paramref name="failures"/> times and is still retrying.</summary>
    private static IntegrationOutboxItem NewRetrying(int failures)
    {
        var row = new IntegrationOutboxItem(
            Guid.NewGuid(), OfficeId, IntegrationMessageType.Intake,
            CaseTrackerEndpoints.Intake, AppointmentId, "{\"data\":{}}",
            "key-" + Guid.NewGuid().ToString("N"));

        for (var i = 0; i < failures; i++)
        {
            row.MarkFailed(Now.AddMinutes(-60 + i), "503 service unavailable");
        }

        return row;
    }

    private static IntegrationOutboxItem NewFailed(bool alreadyAlerted)
    {
        var row = new IntegrationOutboxItem(
            Guid.NewGuid(), OfficeId, IntegrationMessageType.Intake,
            CaseTrackerEndpoints.Intake, AppointmentId, "{\"data\":{}}",
            "key-" + Guid.NewGuid().ToString("N"));

        row.MarkFatal(Now, "401 invalid token");
        if (alreadyAlerted)
        {
            row.MarkAlerted(Now.AddMinutes(-30));
        }

        return row;
    }

    private static Appointment NewAppointment() =>
        new(
            AppointmentId,
            patientId: new Guid("e5f6a7b8-c9d0-4e1f-a2b3-c4d5e6f7a8bc"),
            identityUserId: null,
            appointmentTypeId: new Guid("a1c2e3f4-5566-4778-9900-aabbccddeeff"),
            locationId: new Guid("c0ffee0a-bcde-4f01-9abc-de0123456f7a"),
            doctorAvailabilityId: new Guid("d1e2f3a4-b5c6-4d7e-8f90-a1b2c3d4e5fa"),
            appointmentDate: new DateTime(2026, 8, 15, 9, 30, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A00065",
            appointmentStatus: AppointmentStatusType.Approved,
            panelNumber: "PN-SAMPLE")
        {
            TenantId = OfficeId,
        };

    private static Harness Build(List<IntegrationOutboxItem> rows, bool withStaff = true)
    {
        var runner = Substitute.For<ITenantWorkRunner>();
        runner.ForEachOfficeAsync(Arg.Any<Func<Guid, Task>>())
            .Returns(ci => ci.Arg<Func<Guid, Task>>()(OfficeId));

        var outboxRepo = Substitute.For<IIntegrationOutboxRepository>();
        outboxRepo.GetQueryableAsync().Returns(_ => rows.AsQueryable());
        // Mirrors EfCoreIntegrationOutboxRepository.GetUnwarnedRetryingAsync.
        outboxRepo.GetUnwarnedRetryingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(rows
                .Where(x => x.Status == IntegrationOutboxStatus.Pending
                    && x.AttemptCount >= ci.ArgAt<int>(0)
                    && x.EarlyWarnedAt == null)
                .OrderBy(x => x.CreationTime)
                .ToList()));

        var appointmentRepo = Substitute.For<IRepository<Appointment, Guid>>();
        appointmentRepo.GetListAsync(
                Arg.Any<Expression<Func<Appointment, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<Appointment> { NewAppointment() }));

        var users = Substitute.For<IIdentityUserRepository>();
        users.GetListByNormalizedRoleNameAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(withStaff
                ? new List<IdentityUser> { new(Guid.NewGuid(), "intake", "intake@example.test") }
                : new List<IdentityUser>()));

        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        // The tenant STORE, not ICurrentTenant.Name. ForEachOfficeAsync enters an office via
        // ICurrentTenant.Change(id), which leaves Name null -- substituting ICurrentTenant here used to
        // hide that, and live testing found the alert would name a blank office.
        var tenantStore = Substitute.For<ITenantStore>();
        tenantStore.FindAsync(OfficeId)
            .Returns(Task.FromResult<TenantConfiguration?>(
                new TenantConfiguration(OfficeId, "Sample Medical Group")));

        var bus = Substitute.For<ILocalEventBus>();

        return new Harness
        {
            Job = new CaseTrackerFailureAlertJob(
                runner, outboxRepo,
                new IntegrationOutboxManager(outboxRepo, SimpleGuidGenerator.Instance),
                appointmentRepo, users, bus, tenantStore, clock,
                NullLogger<CaseTrackerFailureAlertJob>.Instance),
            Bus = bus,
            Rows = rows,
            Repository = outboxRepo,
        };
    }

    [Fact]
    public async Task ThreeDeadLettersProduce_OneEmailListingAllThree()
    {
        // A bad token fails every queued row at once; three emails would be noise.
        var rows = new List<IntegrationOutboxItem>
        {
            NewFailed(alreadyAlerted: false),
            NewFailed(alreadyAlerted: false),
            NewFailed(alreadyAlerted: false),
        };
        var h = Build(rows);

        await h.Job.ExecuteAsync();

        await h.Bus.Received(1).PublishAsync(Arg.Is<CaseTrackerPushFailedEto>(e =>
            e.Kind == CaseTrackerPushAlertKind.DeadLettered && e.FailureCount == 3 && e.Failures.Count == 3));
    }

    [Fact]
    public async Task AlreadyAlertedRows_AreNotReported()
    {
        var h = Build(new List<IntegrationOutboxItem> { NewFailed(alreadyAlerted: true) });

        await h.Job.ExecuteAsync();

        await h.Bus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<CaseTrackerPushFailedEto>());
    }

    [Fact]
    public async Task ReportedRows_AreStampedSoASecondRunIsSilent()
    {
        var h = Build(new List<IntegrationOutboxItem> { NewFailed(alreadyAlerted: false) });

        await h.Job.ExecuteAsync();
        h.Rows[0].AlertedAt.ShouldBe(Now);

        await h.Job.ExecuteAsync();

        await h.Bus.Received(1).PublishAsync(Arg.Any<CaseTrackerPushFailedEto>());
    }

    [Fact]
    public async Task WithNoInternalStaff_RowsAreLeftUnstampedSoALaterRunStillAlerts()
    {
        // Stamping here would permanently swallow the batch for an office that simply had no staff yet.
        var h = Build(new List<IntegrationOutboxItem> { NewFailed(alreadyAlerted: false) }, withStaff: false);

        await h.Job.ExecuteAsync();

        h.Rows[0].AlertedAt.ShouldBeNull();
        await h.Bus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<CaseTrackerPushFailedEto>());
    }

    [Fact]
    public async Task TheAlertCarriesConfirmationNumbersAndNoPatientField()
    {
        var h = Build(new List<IntegrationOutboxItem> { NewFailed(alreadyAlerted: false) });

        await h.Job.ExecuteAsync();

        await h.Bus.Received(1).PublishAsync(Arg.Is<CaseTrackerPushFailedEto>(e =>
            e.Failures[0].ConfirmationNumber == "A00065"
            && e.Failures[0].MessageType == "Intake"
            && e.OfficeName == "Sample Medical Group"));
    }

    // -- #917 early warning ---------------------------------------------------------------------------

    [Fact]
    public async Task PushesStillRetryingAfterTwoFailures_GetOneEarlyWarning_AndAreStamped()
    {
        var rows = new List<IntegrationOutboxItem> { NewRetrying(2), NewRetrying(3) };
        var h = Build(rows);

        await h.Job.ExecuteAsync();

        await h.Bus.Received(1).PublishAsync(Arg.Is<CaseTrackerPushFailedEto>(e =>
            e.Kind == CaseTrackerPushAlertKind.StillRetrying
            && e.FailureCount == 2
            && e.Failures[0].ConfirmationNumber == "A00065"));
        await h.Repository.Received(1).StampEarlyWarnedAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(rows[0].Id) && ids.Contains(rows[1].Id)),
            Now,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task APushThatHasFailedOnce_IsNotWarnedYet()
    {
        // One failure is routine; the warning starts at the second.
        var h = Build(new List<IntegrationOutboxItem> { NewRetrying(1) });

        await h.Job.ExecuteAsync();

        await h.Bus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<CaseTrackerPushFailedEto>());
        await h.Repository.DidNotReceiveWithAnyArgs().StampEarlyWarnedAsync(default!, default, default);
    }

    [Fact]
    public async Task DeadLettersAndRetryingPushes_GoInSeparateEmails()
    {
        // They ask for different things: a dead letter needs a person, a retrying push does not yet.
        var h = Build(new List<IntegrationOutboxItem> { NewFailed(alreadyAlerted: false), NewRetrying(2) });

        await h.Job.ExecuteAsync();

        await h.Bus.Received(1).PublishAsync(Arg.Is<CaseTrackerPushFailedEto>(e =>
            e.Kind == CaseTrackerPushAlertKind.DeadLettered && e.FailureCount == 1));
        await h.Bus.Received(1).PublishAsync(Arg.Is<CaseTrackerPushFailedEto>(e =>
            e.Kind == CaseTrackerPushAlertKind.StillRetrying && e.FailureCount == 1));
    }

    [Fact]
    public async Task WithNoInternalStaff_RetryingPushesAreLeftUnstamped()
    {
        var h = Build(new List<IntegrationOutboxItem> { NewRetrying(2) }, withStaff: false);

        await h.Job.ExecuteAsync();

        await h.Repository.DidNotReceiveWithAnyArgs().StampEarlyWarnedAsync(default!, default, default);
    }

    [Fact]
    public async Task WithNothingFailed_NothingIsPublished()
    {
        var h = Build(new List<IntegrationOutboxItem>());

        await h.Job.ExecuteAsync();

        await h.Bus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<CaseTrackerPushFailedEto>());
    }
}
