using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The dead-letter retry end to end, through the REAL intake queue, payload builder and outbox, on the
/// seeded Appointment1 (#961). Until this file <c>RetryAsync</c> had no test at all: the only mention
/// of it under <c>test/</c> was the authorisation-surface file recording its permission. #917 added
/// <c>RetryAllAsync</c>, whose point is that each row stands alone: one that cannot be retried must not
/// undo the others.
///
/// <para>The service is constructed by hand, as <c>AppointmentAccessorManagerTests</c> does, so the
/// authorisation interceptor does not run: what is under test is what a retry DOES, not who may call
/// it. Everything else is resolved from DI.</para>
/// </summary>
public abstract class CaseTrackerDeadLetterRetryTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICaseTrackerIntakeQueue _intakeQueue;
    private readonly IIntegrationOutboxRepository _outboxRepository;
    private readonly IntegrationOutboxManager _outboxManager;
    private readonly ICurrentTenant _currentTenant;

    protected CaseTrackerDeadLetterRetryTests()
    {
        _intakeQueue = GetRequiredService<ICaseTrackerIntakeQueue>();
        _outboxRepository = GetRequiredService<IIntegrationOutboxRepository>();
        _outboxManager = GetRequiredService<IntegrationOutboxManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private CaseTrackerDeadLetterAppService BuildService() =>
        new(
            GetRequiredService<ITenantWorkRunner>(),
            _outboxRepository,
            _outboxManager,
            GetRequiredService<IRepository<Appointment, Guid>>(),
            GetRequiredService<CaseTrackerDeadLetterRequeuer>(),
            _currentTenant,
            GetRequiredService<ITenantStore>(),
            GetRequiredService<IClock>())
        {
            LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>(),
        };

    /// <summary>Queues the appointment's current intake and returns it, inside the office scope.</summary>
    private async Task<IntegrationOutboxItem> EnqueueCurrentIntakeAsync(Guid officeId)
    {
        IntegrationOutboxItem row = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                row = await _intakeQueue.EnqueueIntakeAsync(AppointmentsTestData.Appointment1Id, officeId);
            }
        });
        return row;
    }

    private Task SettleAsync(Guid officeId, Guid rowId, Action<IntegrationOutboxItem> transition) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                var row = await _outboxRepository.GetAsync(rowId);
                transition(row);
                await _outboxManager.SaveAsync(row);
            }
        });

    private async Task<IntegrationOutboxItem> GetAsync(Guid officeId, Guid rowId)
    {
        IntegrationOutboxItem row = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                row = await _outboxRepository.GetAsync(rowId);
            }
        });
        return row;
    }

    [Fact]
    public async Task RetryAsync_AnUnchangedIntakeDeadLetter_LeavesADrainableRowAndResolvesIt()
    {
        // #961's required test: build a Failed row, retry it WITHOUT changing the appointment, and
        // assert a drainable row exists afterwards. Before #915 and #961 the retry rebuilt identical
        // content, collapsed onto the dead letter itself, resolved it, and reported "queued".
        var officeId = TenantsTestData.TenantARef;
        var deadLetter = await EnqueueCurrentIntakeAsync(officeId);
        await SettleAsync(officeId, deadLetter.Id, r => r.MarkFatal(DateTime.UtcNow, "401 invalid token"));

        CaseTrackerDeadLetterRetryResultDto result = null!;
        await WithUnitOfWorkAsync(async () => result = await BuildService().RetryAsync(officeId, deadLetter.Id));

        result.AlreadyDelivered.ShouldBeFalse();
        result.ResolvedOutboxItemId.ShouldBe(deadLetter.Id);
        result.QueuedOutboxItemId.ShouldNotBe(deadLetter.Id);

        var queued = await GetAsync(officeId, result.QueuedOutboxItemId);
        queued.Status.ShouldBe(IntegrationOutboxStatus.Pending); // drainable: the drain leases only Pending
        queued.MessageType.ShouldBe(IntegrationMessageType.Intake);
        queued.AppointmentId.ShouldBe(AppointmentsTestData.Appointment1Id);
        (await GetAsync(officeId, deadLetter.Id)).Status.ShouldBe(IntegrationOutboxStatus.Resolved);
    }

    [Fact]
    public async Task RetryAsync_WhenANewerRowAlreadyDeliveredTheSameContent_ResolvesAsAlreadyDelivered()
    {
        // Decided 2026-09-23: the Case Tracker already holds the current state, so the dead letter is
        // resolved without sending again, and the result says so.
        var officeId = TenantsTestData.TenantARef;
        var deadLetter = await EnqueueCurrentIntakeAsync(officeId);
        await SettleAsync(officeId, deadLetter.Id, r => r.MarkFatal(DateTime.UtcNow, "401 invalid token"));
        var newer = await EnqueueCurrentIntakeAsync(officeId); // same content; #915 re-sends after a Failed row
        newer.Id.ShouldNotBe(deadLetter.Id);
        await SettleAsync(officeId, newer.Id, r => r.MarkSent(DateTime.UtcNow));

        CaseTrackerDeadLetterRetryResultDto result = null!;
        await WithUnitOfWorkAsync(async () => result = await BuildService().RetryAsync(officeId, deadLetter.Id));

        result.AlreadyDelivered.ShouldBeTrue();
        result.QueuedOutboxItemId.ShouldBe(newer.Id);
        (await GetAsync(officeId, deadLetter.Id)).Status.ShouldBe(IntegrationOutboxStatus.Resolved);
    }

    /// <summary>A dead letter for an appointment that does not exist, so its retry cannot succeed.</summary>
    private async Task<IntegrationOutboxItem> InsertUnretryableDeadLetterAsync(Guid officeId)
    {
        IntegrationOutboxItem row = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                row = await _outboxManager.EnqueueAsync(
                    officeId,
                    IntegrationMessageType.Intake,
                    CaseTrackerEndpoints.Intake,
                    Guid.NewGuid(),
                    "{\"data\":{}}",
                    "unretryable-" + Guid.NewGuid().ToString("N"));
                row.MarkFatal(DateTime.UtcNow, "401 invalid token");
                await _outboxManager.SaveAsync(row);
            }
        });
        return row;
    }

    [Fact]
    public async Task RetryAllAsync_RetriesEveryDeadLetterInTheOffice_AndCountsThem()
    {
        // Two dead letters for the same appointment, as an outage leaves them. The first retry queues
        // the current content; the second collapses onto that same Pending row.
        var officeId = TenantsTestData.TenantARef;
        var first = await EnqueueCurrentIntakeAsync(officeId);
        await SettleAsync(officeId, first.Id, r => r.MarkFatal(DateTime.UtcNow, "503"));
        var second = await EnqueueCurrentIntakeAsync(officeId);
        await SettleAsync(officeId, second.Id, r => r.MarkFatal(DateTime.UtcNow, "503"));

        CaseTrackerDeadLetterRetryAllResultDto result = null!;
        await WithUnitOfWorkAsync(async () => result = await BuildService().RetryAllAsync(officeId));

        result.Requeued.ShouldBe(2);
        result.AlreadyDelivered.ShouldBe(0);
        result.NotRetried.ShouldBe(0);
        result.Remaining.ShouldBe(0);
        (await GetAsync(officeId, first.Id)).Status.ShouldBe(IntegrationOutboxStatus.Resolved);
        (await GetAsync(officeId, second.Id)).Status.ShouldBe(IntegrationOutboxStatus.Resolved);
    }

    [Fact]
    public async Task RetryAllAsync_CountsADeadLetterAlreadyDeliveredByANewerRow()
    {
        var officeId = TenantsTestData.TenantARef;
        var deadLetter = await EnqueueCurrentIntakeAsync(officeId);
        await SettleAsync(officeId, deadLetter.Id, r => r.MarkFatal(DateTime.UtcNow, "401 invalid token"));
        var newer = await EnqueueCurrentIntakeAsync(officeId);
        await SettleAsync(officeId, newer.Id, r => r.MarkSent(DateTime.UtcNow));

        CaseTrackerDeadLetterRetryAllResultDto result = null!;
        await WithUnitOfWorkAsync(async () => result = await BuildService().RetryAllAsync(officeId));

        result.AlreadyDelivered.ShouldBe(1);
        result.Requeued.ShouldBe(0);
        (await GetAsync(officeId, deadLetter.Id)).Status.ShouldBe(IntegrationOutboxStatus.Resolved);
    }

    [Fact]
    public async Task RetryAllAsync_ARowThatCannotBeRetried_StaysListed_AndDoesNotUndoTheOthers()
    {
        // THE LOAD-BEARING ONE: each row in its own transaction. Were the pass one transaction, the
        // failure would roll back the good row's resolution too, and the operator would see neither.
        var officeId = TenantsTestData.TenantARef;
        var good = await EnqueueCurrentIntakeAsync(officeId);
        await SettleAsync(officeId, good.Id, r => r.MarkFatal(DateTime.UtcNow, "503"));
        var bad = await InsertUnretryableDeadLetterAsync(officeId);

        CaseTrackerDeadLetterRetryAllResultDto result = null!;
        await WithUnitOfWorkAsync(async () => result = await BuildService().RetryAllAsync(officeId));

        result.Requeued.ShouldBe(1);
        result.NotRetried.ShouldBe(1);
        (await GetAsync(officeId, good.Id)).Status.ShouldBe(IntegrationOutboxStatus.Resolved);
        (await GetAsync(officeId, bad.Id)).Status.ShouldBe(IntegrationOutboxStatus.Failed);
    }

    [Fact]
    public async Task RetryAllAsync_WithNoOffice_IsRefused()
    {
        await Should.ThrowAsync<UserFriendlyException>(
            () => WithUnitOfWorkAsync(() => BuildService().RetryAllAsync(Guid.Empty)));
    }
}
