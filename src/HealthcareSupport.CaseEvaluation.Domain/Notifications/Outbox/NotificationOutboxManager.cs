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
    protected IRepository<NotificationOutboxItem, Guid> _outboxRepository;
    private readonly IGuidGenerator _guidGenerator;

    public NotificationOutboxManager(
        IRepository<NotificationOutboxItem, Guid> outboxRepository,
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
    /// Claims up to <paramref name="batchSize"/> due Pending rows (oldest first),
    /// leasing each via <see cref="NotificationOutboxItem.TryClaim"/>. A row whose
    /// concurrency stamp changed under a racing drain surfaces as
    /// <c>AbpDbConcurrencyException</c> on save; the caller decides whether to
    /// swallow per-row. Returns the rows this call successfully leased.
    /// </summary>
    public virtual async Task<List<NotificationOutboxItem>> ClaimDueBatchAsync(
        DateTime nowUtc,
        TimeSpan leaseDuration,
        int batchSize)
    {
        var queryable = await _outboxRepository.GetQueryableAsync();
        var candidates = queryable
            .Where(x => x.Status == NotificationOutboxStatus.Pending
                && (x.LockedUntil == null || x.LockedUntil <= nowUtc)
                && (x.NextAttemptAt == null || x.NextAttemptAt <= nowUtc))
            .OrderBy(x => x.CreationTime)
            .Take(batchSize)
            .ToList();

        var claimed = new List<NotificationOutboxItem>();
        foreach (var item in candidates)
        {
            if (item.TryClaim(nowUtc, leaseDuration))
            {
                await _outboxRepository.UpdateAsync(item, autoSave: true);
                claimed.Add(item);
            }
        }
        return claimed;
    }

    /// <summary>Persists a post-send state transition (MarkSent / MarkFailed).</summary>
    public virtual Task SaveAsync(NotificationOutboxItem item) =>
        _outboxRepository.UpdateAsync(item, autoSave: true);
}
