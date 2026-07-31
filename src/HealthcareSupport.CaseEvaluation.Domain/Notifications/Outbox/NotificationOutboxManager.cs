using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// Phase 2 (T8): domain service over <see cref="NotificationOutboxItem"/>. Two
/// responsibilities: an idempotent enqueue (the atomic Pending write the approval
/// UoW performs) and a due-batch claim (what the drain job calls). The state
/// transitions themselves live on the entity; this service is the repository seam.
///
/// <para><see cref="IGuidGenerator"/> is constructor-injected (rather than taken
/// from the DomainService base property) so the enqueue path is exercisable in a
/// pure unit test without the full ABP DI container.</para>
/// </summary>
public class NotificationOutboxManager : DomainService
{
    protected INotificationOutboxRepository _outboxRepository;
    private readonly IGuidGenerator _guidGenerator;

    public NotificationOutboxManager(
        INotificationOutboxRepository outboxRepository,
        IGuidGenerator guidGenerator)
    {
        _outboxRepository = outboxRepository;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Idempotent enqueue: if a row already exists for this
    /// <paramref name="idempotencyKey"/> (within the current tenant), returns it
    /// untouched; otherwise inserts a new Pending row. This is what makes the
    /// approval UoW's write safe to replay -- a re-run of the same logical send
    /// collapses to one ledger row rather than a duplicate PHI email.
    /// </summary>
    public virtual async Task<NotificationOutboxItem> EnqueueAsync(
        Guid? tenantId,
        string to,
        IEnumerable<string>? cc,
        string subject,
        string body,
        bool isBodyHtml,
        string context,
        string idempotencyKey,
        Guid? packetAppointmentId = null,
        Guid? packetId = null,
        PacketKind? packetKind = null,
        int maxAttempts = NotificationOutboxConsts.DefaultMaxAttempts)
    {
        Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));

        var queryable = await _outboxRepository.GetQueryableAsync();
        var existing = queryable.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey);
        if (existing != null)
        {
            return existing; // idempotent: collapse a replayed send to the existing row
        }

        var item = new NotificationOutboxItem(
            _guidGenerator.Create(), tenantId, to, cc, subject, body, isBodyHtml, context,
            idempotencyKey, packetAppointmentId, packetId, packetKind, maxAttempts);
        return await _outboxRepository.InsertAsync(item, autoSave: true);
    }

    /// <summary>
    /// Claims up to <paramref name="batchSize"/> due Pending rows (oldest first) via an ATOMIC
    /// status-gated lease (<see cref="INotificationOutboxRepository.TryLeaseAsync"/>). Overlapping
    /// drains no longer collide on save: a row already leased by another drain updates 0 rows and
    /// is skipped, with no <c>AbpDbConcurrencyException</c>. Returns the rows this call leased.
    /// </summary>
    public virtual async Task<List<NotificationOutboxItem>> ClaimDueBatchAsync(
        DateTime nowUtc,
        TimeSpan leaseDuration,
        int batchSize)
    {
        // task_349a723c (2026-07-21): read the due candidate IDs, then lease each ATOMICALLY. This
        // replaces the prior read-then-optimistic-UpdateAsync, where two overlapping drains both
        // passed TryClaim in memory and one then threw AbpDbConcurrencyException on save (noisy;
        // aborted the whole drain -> Hangfire retry). Now the loser's lease updates 0 rows and is
        // skipped. A freshly-leased row is reloaded so the send + MarkSent run against its current
        // state (the lease UPDATE bypasses the change-tracker).
        var leaseUntil = nowUtc.Add(leaseDuration);
        var queryable = await _outboxRepository.GetQueryableAsync();
        // Synchronous LINQ (as EnqueueAsync does) rather than AsyncExecuter: the candidate
        // read is a tiny indexed query (<= batchSize Ids) on a background thread, and keeping
        // it sync lets the manager be unit-tested without the DI-injected AsyncExecuter.
        var candidateIds = queryable
            .Where(x => x.Status == NotificationOutboxStatus.Pending
                && (x.LockedUntil == null || x.LockedUntil <= nowUtc)
                && (x.NextAttemptAt == null || x.NextAttemptAt <= nowUtc))
            .OrderBy(x => x.CreationTime)
            .Take(batchSize)
            .Select(x => x.Id)
            .ToList();

        var claimed = new List<NotificationOutboxItem>();
        foreach (var id in candidateIds)
        {
            if (await _outboxRepository.TryLeaseAsync(id, nowUtc, leaseUntil))
            {
                claimed.Add(await _outboxRepository.GetAsync(id));
            }
        }
        return claimed;
    }

    /// <summary>Persists a post-send state transition (MarkSent / MarkFailed).</summary>
    public virtual Task SaveAsync(NotificationOutboxItem item) =>
        _outboxRepository.UpdateAsync(item, autoSave: true);
}
