using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit coverage for <see cref="CaseTrackerDeadLetterAppService"/>: the staff screen that lists Case
/// Tracker pushes that failed for good, and the retry button beside each one.
///
/// <para><b>WHAT IS PINNED.</b> The list shows FAILED rows only, each with its appointment's
/// confirmation number and the office name taken from the tenant STORE (the service comment records
/// why: <c>ICurrentTenant.Name</c> is null inside the per-office loop). A retry refuses empty ids, an
/// unknown row, and any row that is not failed -- retrying a pending push would duplicate it -- and
/// otherwise queues a FRESH intake and marks the old row resolved.</para>
///
/// <para><b>The results asserted are the returned rows, the thrown error, the queued intake and the
/// saved row.</b> Collaborators are substitutes; the outbox manager is real over a substituted
/// repository, so the row it saves can be inspected. The service is built with <c>new</c> and given a
/// substituted <see cref="IAbpLazyServiceProvider"/>, which is all its localizer and logger need.
/// Nothing is pushed to Case Tracker. Synthetic data only (HIPAA).</para>
/// </summary>
public class CaseTrackerDeadLetterAppServiceTests
{
    private static readonly Guid OfficeId = new("99999999-9999-9999-9999-999999999999");
    private static readonly DateTime Now = new(2026, 9, 23, 23, 0, 0, DateTimeKind.Utc);

    private sealed class Rig
    {
        public List<IntegrationOutboxItem> Rows { get; } = new();
        public List<Appointment> Appointments { get; } = new();
        public IIntegrationOutboxRepository OutboxRepository { get; } = Substitute.For<IIntegrationOutboxRepository>();
        public ICaseTrackerIntakeQueue IntakeQueue { get; } = Substitute.For<ICaseTrackerIntakeQueue>();
        public ITenantStore TenantStore { get; } = Substitute.For<ITenantStore>();
        public IntegrationOutboxItem Queued { get; } = NewRow(Guid.NewGuid());

        public Rig()
        {
            OutboxRepository.GetQueryableAsync().Returns(_ => Rows.AsQueryable());
            OutboxRepository.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(ci => Rows.SingleOrDefault(r => r.Id == ci.Arg<Guid>()));
            IntakeQueue.EnqueueIntakeAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(Queued);
            TenantStore.FindAsync(Arg.Any<Guid>()).Returns(new TenantConfiguration(OfficeId, "TEST-Office"));
        }

        public CaseTrackerDeadLetterAppService Build()
        {
            var tenantRunner = Substitute.For<ITenantWorkRunner>();
            tenantRunner.AggregateAcrossOfficesAsync(Arg.Any<Func<Guid, Task<List<CaseTrackerDeadLetterDto>>>>())
                .Returns(async ci => new List<List<CaseTrackerDeadLetterDto>>
                {
                    await ci.Arg<Func<Guid, Task<List<CaseTrackerDeadLetterDto>>>>()(OfficeId),
                });
            var appointments = Substitute.For<IRepository<Appointment, Guid>>();
            appointments.GetListAsync(Arg.Any<Expression<Func<Appointment, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(ci => Appointments.Where(ci.Arg<Expression<Func<Appointment, bool>>>().Compile()).ToList());
            var clock = Substitute.For<IClock>();
            clock.Now.Returns(Now);

            return new CaseTrackerDeadLetterAppService(
                tenantRunner,
                OutboxRepository,
                new IntegrationOutboxManager(OutboxRepository, SimpleGuidGenerator.Instance),
                appointments,
                IntakeQueue,
                Substitute.For<ICurrentTenant>(),
                TenantStore,
                clock)
            {
                LazyServiceProvider = Substitute.For<IAbpLazyServiceProvider>(),
            };
        }
    }

    private static IntegrationOutboxItem NewRow(Guid appointmentId) => new(
        Guid.NewGuid(), OfficeId, IntegrationMessageType.Intake, "TEST/intake", appointmentId, "{}", $"TEST-key-{Guid.NewGuid():N}");

    private static IntegrationOutboxItem FailedRow(Guid appointmentId)
    {
        var row = NewRow(appointmentId);
        row.MarkFatal(Now.AddHours(-1), "TEST-error: 400 from Case Tracker");
        return row;
    }

    private static Appointment NewAppointment(string confirmation) => new(
        id: Guid.NewGuid(),
        patientId: Guid.NewGuid(),
        identityUserId: null,
        appointmentTypeId: Guid.NewGuid(),
        locationId: Guid.NewGuid(),
        doctorAvailabilityId: Guid.NewGuid(),
        appointmentDate: new DateTime(2026, 11, 4, 9, 0, 0),
        requestConfirmationNumber: confirmation,
        appointmentStatus: AppointmentStatusType.Approved);

    // ----- GetListAsync -----

    [Fact]
    public async Task GetListAsync_ListsOnlyFailedRowsWithConfirmationNumberAndOfficeName()
    {
        var rig = new Rig();
        var appointment = NewAppointment("TEST-DL0001");
        rig.Appointments.Add(appointment);
        var failed = FailedRow(appointment.Id);
        rig.Rows.Add(failed);
        rig.Rows.Add(NewRow(appointment.Id)); // still pending: not a dead letter

        var rows = await rig.Build().GetListAsync();

        var row = rows.ShouldHaveSingleItem();
        row.Id.ShouldBe(failed.Id);
        row.OfficeId.ShouldBe(OfficeId);
        row.OfficeName.ShouldBe("TEST-Office");
        row.ConfirmationNumber.ShouldBe("TEST-DL0001");
        row.MessageType.ShouldBe("Intake");
        row.AttemptCount.ShouldBe(1);
        row.LastError.ShouldBe("TEST-error: 400 from Case Tracker");
    }

    /// <summary>
    /// A failed push whose appointment is gone is still listed -- with a blank confirmation number --
    /// and an office missing from the tenant store gets a blank name rather than hiding its failures.
    /// </summary>
    [Fact]
    public async Task GetListAsync_AppointmentOrOfficeGone_StillListsTheRowWithBlanks()
    {
        var rig = new Rig();
        rig.Rows.Add(FailedRow(Guid.NewGuid()));
        rig.TenantStore.FindAsync(Arg.Any<Guid>()).Returns((TenantConfiguration?)null);

        var row = (await rig.Build().GetListAsync()).ShouldHaveSingleItem();

        row.ConfirmationNumber.ShouldBe(string.Empty);
        row.OfficeName.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task GetListAsync_NoFailures_ReturnsAnEmptyList()
    {
        var rig = new Rig();
        rig.Rows.Add(NewRow(Guid.NewGuid()));

        (await rig.Build().GetListAsync()).ShouldBeEmpty();
    }

    // ----- RetryAsync -----

    /// <summary>
    /// <b>POSITIVE CONTROL for the refusals below.</b> A failed row is retried: a FRESH intake is
    /// queued for its appointment in that office, and the old row is saved as resolved.
    /// </summary>
    [Fact]
    public async Task RetryAsync_AFailedRow_QueuesAFreshIntakeAndResolvesTheOldRow_PositiveControl()
    {
        var rig = new Rig();
        var appointmentId = Guid.NewGuid();
        var failed = FailedRow(appointmentId);
        rig.Rows.Add(failed);

        var result = await rig.Build().RetryAsync(OfficeId, failed.Id);

        result.QueuedOutboxItemId.ShouldBe(rig.Queued.Id);
        result.ResolvedOutboxItemId.ShouldBe(failed.Id);
        failed.Status.ShouldBe(IntegrationOutboxStatus.Resolved);
        await rig.IntakeQueue.Received(1).EnqueueIntakeAsync(appointmentId, OfficeId, Arg.Any<CancellationToken>());
        await rig.OutboxRepository.Received(1).UpdateAsync(failed, true, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RetryAsync_AnEmptyId_IsRefused(bool emptyOffice, bool emptyRow)
    {
        var rig = new Rig();

        await Should.ThrowAsync<UserFriendlyException>(() => rig.Build().RetryAsync(
            emptyOffice ? Guid.Empty : OfficeId, emptyRow ? Guid.Empty : Guid.NewGuid()));

        await rig.IntakeQueue.DidNotReceive().EnqueueIntakeAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetryAsync_AnUnknownRow_IsNotFound()
    {
        var rig = new Rig();

        await Should.ThrowAsync<EntityNotFoundException>(() => rig.Build().RetryAsync(OfficeId, Guid.NewGuid()));
    }

    /// <summary>
    /// Only a row that failed for good can be retried: retrying a still-pending push would send it
    /// twice. The row is left as it was and nothing is queued.
    /// </summary>
    [Fact]
    public async Task RetryAsync_ARowThatIsNotFailed_IsRefusedAndLeftAlone()
    {
        var rig = new Rig();
        var pending = NewRow(Guid.NewGuid());
        rig.Rows.Add(pending);

        await Should.ThrowAsync<UserFriendlyException>(() => rig.Build().RetryAsync(OfficeId, pending.Id));

        pending.Status.ShouldBe(IntegrationOutboxStatus.Pending);
        await rig.IntakeQueue.DidNotReceive().EnqueueIntakeAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }
}
