using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// task_349a723c (2026-07-21): custom outbox repository. Adds an atomic status-gated lease
/// (<see cref="TryLeaseAsync"/>) so overlapping drains no longer collide on optimistic-
/// concurrency saves. Uses the same <c>CaseEvaluationDbContext</c> as the other custom
/// per-tenant repositories (ABP resolves the office connection at runtime).
/// </summary>
public class EfCoreNotificationOutboxRepository
    : EfCoreRepository<CaseEvaluationDbContext, NotificationOutboxItem, Guid>, INotificationOutboxRepository
{
    public EfCoreNotificationOutboxRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<bool> TryLeaseAsync(
        Guid id,
        DateTime nowUtc,
        DateTime leaseUntil,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        // Single UPDATE ... WHERE <claim gate>. The DB row lock serializes racing drains: the
        // first flips LockedUntil into the future, so a concurrent call's gate no longer matches
        // and it updates 0 rows. The EF query filters (IMultiTenant + soft-delete) are applied to
        // the WHERE, so this only ever touches the current office's live rows.
        var affected = await dbSet
            .Where(x => x.Id == id
                && x.Status == NotificationOutboxStatus.Pending
                && (x.LockedUntil == null || x.LockedUntil <= nowUtc)
                && (x.NextAttemptAt == null || x.NextAttemptAt <= nowUtc))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.LockedUntil, leaseUntil),
                cancellationToken);

        return affected == 1;
    }
}
