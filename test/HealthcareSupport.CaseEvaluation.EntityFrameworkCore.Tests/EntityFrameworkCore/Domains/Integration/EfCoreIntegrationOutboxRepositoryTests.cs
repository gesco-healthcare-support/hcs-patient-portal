using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Exercises the atomic status-gated lease against a real (SQLite) EF context -- the DB-level
/// behaviour a List-backed mock cannot demonstrate. Proves the claim serializes: exactly one drain
/// wins a due row, a second is skipped WITHOUT an exception (the whole reason this repository exists
/// rather than an optimistic UpdateAsync), an expired lease is reclaimable, and terminal rows are
/// not leasable.
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreIntegrationOutboxRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private static readonly DateTime Now = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AppointmentId = new("8f14e45f-ceea-467a-9f3a-1a2b3c4d5e6f");

    private readonly IIntegrationOutboxRepository _outboxRepository;

    public EfCoreIntegrationOutboxRepositoryTests()
    {
        _outboxRepository = GetRequiredService<IIntegrationOutboxRepository>();
    }

    private static IntegrationOutboxItem NewPending(Guid id) =>
        new(
            id,
            tenantId: null,
            IntegrationMessageType.Intake,
            targetPath: CaseTrackerEndpoints.Intake,
            appointmentId: AppointmentId,
            payload: "{\"data\":{}}",
            idempotencyKey: "key-" + id.ToString("N"));

    private Task InsertAsync(IntegrationOutboxItem item) =>
        WithUnitOfWorkAsync(() => _outboxRepository.InsertAsync(item, autoSave: true));

    [Fact]
    public async Task TryLeaseAsync_FirstWins_SecondBlockedWithinLease()
    {
        var id = Guid.NewGuid();
        var leaseUntil = Now.AddSeconds(IntegrationOutboxConsts.LeaseDurationSeconds);
        await InsertAsync(NewPending(id));

        bool first = false, second = false;
        await WithUnitOfWorkAsync(async () =>
        {
            first = await _outboxRepository.TryLeaseAsync(id, Now, leaseUntil);
            second = await _outboxRepository.TryLeaseAsync(id, Now, leaseUntil);
        });

        first.ShouldBeTrue();   // won the row
        second.ShouldBeFalse(); // the unexpired lease no longer matches the claim gate

        await WithUnitOfWorkAsync(async () =>
        {
            var row = await _outboxRepository.GetAsync(id);
            row.LockedUntil.ShouldBe(leaseUntil); // the UPDATE persisted
        });
    }

    [Fact]
    public async Task TryLeaseAsync_ExpiredLease_IsReclaimable()
    {
        var id = Guid.NewGuid();
        await InsertAsync(NewPending(id));

        await WithUnitOfWorkAsync(async () =>
            (await _outboxRepository.TryLeaseAsync(id, Now, Now.AddSeconds(120))).ShouldBeTrue());

        // A drain running past the lease expiry reclaims the still-Pending row rather than leaving it
        // stranded by a worker that died mid-post.
        await WithUnitOfWorkAsync(async () =>
        {
            var later = Now.AddSeconds(200);
            (await _outboxRepository.TryLeaseAsync(id, later, later.AddSeconds(120))).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task TryLeaseAsync_SentRow_IsNotLeasable()
    {
        var id = Guid.NewGuid();
        var sent = NewPending(id);
        sent.MarkSent(Now);
        await InsertAsync(sent);

        await WithUnitOfWorkAsync(async () =>
            (await _outboxRepository.TryLeaseAsync(id, Now, Now.AddSeconds(120))).ShouldBeFalse());
    }

    [Fact]
    public async Task TryLeaseAsync_DeadLetteredRow_IsNotLeasable()
    {
        // A fatally-failed row must never be picked up again by a drain -- only a human re-push.
        var id = Guid.NewGuid();
        var fatal = NewPending(id);
        fatal.MarkFatal(Now, "401 invalid token");
        await InsertAsync(fatal);

        await WithUnitOfWorkAsync(async () =>
            (await _outboxRepository.TryLeaseAsync(id, Now, Now.AddSeconds(120))).ShouldBeFalse());
    }

    [Fact]
    public async Task TryLeaseAsync_RowInBackoff_IsNotLeasableUntilDue()
    {
        var id = Guid.NewGuid();
        var backedOff = NewPending(id);
        backedOff.MarkFailed(Now, "503", TimeSpan.FromSeconds(IntegrationOutboxConsts.RetryBackoffSeconds));
        await InsertAsync(backedOff);

        await WithUnitOfWorkAsync(async () =>
            (await _outboxRepository.TryLeaseAsync(id, Now.AddSeconds(60), Now.AddSeconds(180))).ShouldBeFalse());

        await WithUnitOfWorkAsync(async () =>
        {
            var due = Now.AddSeconds(IntegrationOutboxConsts.RetryBackoffSeconds + 1);
            (await _outboxRepository.TryLeaseAsync(id, due, due.AddSeconds(120))).ShouldBeTrue();
        });
    }
}
