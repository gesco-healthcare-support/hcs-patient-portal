using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Custom repository for the Case Tracker outbox, adding an ATOMIC lease claim. Mirrors
/// <c>INotificationOutboxRepository</c>, which exists for the same reason: a read-then-optimistic-
/// update claim lets two overlapping drains both pass the in-memory gate and then collide on save
/// with <c>AbpDbConcurrencyException</c> -- self-healing but noisy, and it aborts the whole drain.
/// </summary>
public interface IIntegrationOutboxRepository : IRepository<IntegrationOutboxItem, Guid>
{
    /// <summary>
    /// Atomically leases one row: sets <c>LockedUntil</c> only if the row is still Pending, holds no
    /// unexpired lease, and is past any retry backoff -- the same gate as
    /// <see cref="IntegrationOutboxItem.TryClaim"/> but enforced in the database. Returns true when
    /// this call won the row (1 row updated), false when another drain holds it (0 rows). Never
    /// throws on contention.
    /// </summary>
    Task<bool> TryLeaseAsync(
        Guid id,
        DateTime nowUtc,
        DateTime leaseUntil,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many rows this office has SENT since <paramref name="sinceUtc"/>. Feeds the drain's volume
    /// guard.
    ///
    /// <para>Counted from the ledger rather than a separate counter or cache key on purpose: the
    /// outbox already stamps <c>SentAt</c> on every successful push, so the number is derivable and
    /// cannot drift from reality, survives a restart, and needs no migration. It also means the guard
    /// releases itself as the window slides -- there is no trip flag to reset and therefore no way to
    /// leave delivery stuck off by accident.</para>
    ///
    /// <para>Office scoping is the ambient tenant filter, matching every other query on this
    /// repository; the drain always runs inside one office's scope.</para>
    /// </summary>
    Task<int> CountSentSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);
}
