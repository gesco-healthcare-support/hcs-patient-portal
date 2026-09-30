using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// task_349a723c (2026-07-21): custom repository for the notification outbox, adding an ATOMIC
/// lease claim. The prior claim used the generic optimistic-concurrency UpdateAsync: two
/// overlapping drains (an approval's fan-out drain + the reconciliation sweep, say) could both
/// pass <see cref="NotificationOutboxItem.TryClaim"/> in memory and then collide on save with
/// AbpDbConcurrencyException -- self-healing but noisy, and it aborted the whole drain (Hangfire
/// retried). <see cref="TryLeaseAsync"/> claims via a single status-gated UPDATE so exactly one
/// drain wins per row, with no exception.
/// </summary>
public interface INotificationOutboxRepository : IRepository<NotificationOutboxItem, Guid>
{
    /// <summary>
    /// Atomically leases one row: sets <c>LockedUntil = <paramref name="leaseUntil"/></c> only if
    /// the row is still Pending, holds no unexpired lease, and is past any retry backoff at
    /// <paramref name="nowUtc"/> -- the same gate as <see cref="NotificationOutboxItem.TryClaim"/>,
    /// but enforced in the database. Returns true when this call won the row (1 row updated), false
    /// when another drain already holds it (0 rows). Never throws on contention.
    /// </summary>
    Task<bool> TryLeaseAsync(
        Guid id,
        DateTime nowUtc,
        DateTime leaseUntil,
        CancellationToken cancellationToken = default);
}
