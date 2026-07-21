using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// task_349a723c (2026-07-21): exercises the atomic status-gated lease against a real
/// (SQLite) EF context -- the DB-level behavior a List-backed mock cannot show. Proves the
/// claim serializes: exactly one drain wins a due row, a second is skipped without an
/// exception, an expired lease is reclaimable, and a terminal (Sent) row is not leasable.
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreNotificationOutboxRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly INotificationOutboxRepository _outboxRepository;

    public EfCoreNotificationOutboxRepositoryTests()
    {
        _outboxRepository = GetRequiredService<INotificationOutboxRepository>();
    }

    private static NotificationOutboxItem NewPending(Guid id) =>
        new(
            id, tenantId: null,
            to: "party@example.test", cc: null, subject: "s", body: "b",
            isBodyHtml: true, context: "ctx", idempotencyKey: "key-" + id.ToString("N"));

    private Task InsertAsync(NotificationOutboxItem item) =>
        WithUnitOfWorkAsync(() => _outboxRepository.InsertAsync(item, autoSave: true));

    [Fact]
    public async Task TryLeaseAsync_FirstWins_SecondBlockedWithinLease()
    {
        var id = Guid.NewGuid();
        var leaseUntil = Now.AddSeconds(120);
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

        // A drain running past the lease expiry reclaims the still-Pending row.
        await WithUnitOfWorkAsync(async () =>
        {
            var later = Now.AddSeconds(200);
            (await _outboxRepository.TryLeaseAsync(id, later, later.AddSeconds(120))).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task TryLeaseAsync_NonPendingRow_IsNotLeasable()
    {
        var id = Guid.NewGuid();
        var sent = NewPending(id);
        sent.MarkSent(Now); // terminal Sent
        await InsertAsync(sent);

        await WithUnitOfWorkAsync(async () =>
            (await _outboxRepository.TryLeaseAsync(id, Now, Now.AddSeconds(120))).ShouldBeFalse());
    }
}
