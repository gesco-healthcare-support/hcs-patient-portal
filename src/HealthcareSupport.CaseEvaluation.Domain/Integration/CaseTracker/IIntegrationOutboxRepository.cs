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

    /// <summary>
    /// Whether this appointment has EVER had an intake row, in any status. Pending, Sent, Failed and
    /// Resolved all count: the question is "has an intake for this appointment been queued", and a
    /// Failed or still-Pending row means that telling is already in hand -- retrying it is the
    /// outbox's job, not a reason to queue a second intake or to hold back what follows it.
    ///
    /// <para>The document queue gates on this (#931) so a document update can never sit AHEAD of its
    /// own intake in the stream, and the packet publisher branches on it to choose between an intake
    /// and a document update.</para>
    ///
    /// <para>Office scoping is the ambient tenant filter, matching every other query on this
    /// repository.</para>
    ///
    /// <para>DEPENDS ON OUTBOX ROWS NEVER BEING DELETED. Nothing purges this table today. The entity
    /// is soft-deletable, so EF's soft-delete filter would hide a deleted intake row from this query:
    /// if a retention job is ever added, every later document update for that appointment is
    /// suppressed SILENTLY. Such a job must keep intake rows, or this must stop filtering them.</para>
    /// </summary>
    Task<bool> HasIntakeAsync(Guid appointmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes the per-appointment ordering lock for the rest of the current transaction (#931). Both
    /// the intake enqueue and the document enqueue take it BEFORE they read anything, so they cannot
    /// interleave for the same appointment.
    ///
    /// <para>Why it is needed: the intake reads the document list and only then writes its row, while
    /// the document path checks for that row. Without the lock, a document accepted in that gap is
    /// suppressed by the document gate AND missing from the intake -- lost. With it, whichever path
    /// runs second waits for the first to commit and then sees its result.</para>
    ///
    /// <para>A SQL Server application lock (<c>sp_getapplock</c>), owned by the transaction and named
    /// after the appointment: it touches no data rows, so it cannot block staff reading or saving the
    /// appointment, and the database releases it at commit or rollback. It REQUIRES an active
    /// transaction; every caller runs inside a transactional unit of work.</para>
    ///
    /// <para>Throws when the lock is not granted (timeout, deadlock victim, error) rather than
    /// carrying on unlocked. On a provider other than SQL Server -- the SQLite test database -- it is
    /// a no-op, so tests prove the lock is ASKED FOR in order, not that it blocks.</para>
    /// </summary>
    Task AcquireAppointmentLockAsync(Guid appointmentId, CancellationToken cancellationToken = default);
}
