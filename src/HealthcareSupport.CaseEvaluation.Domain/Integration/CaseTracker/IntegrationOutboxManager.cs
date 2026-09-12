using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Domain service over <see cref="IntegrationOutboxItem"/>: an idempotent enqueue (the atomic
/// Pending write the approval unit of work performs) and a due-batch claim (what the drain calls).
/// State transitions live on the entity; this is the repository seam.
///
/// <para><see cref="IGuidGenerator"/> is constructor-injected rather than taken from the
/// <c>DomainService</c> base property so the enqueue path is exercisable in a plain unit test
/// without the ABP container -- the same reason <c>NotificationOutboxManager</c> does it.</para>
/// </summary>
public class IntegrationOutboxManager : DomainService
{
    protected IIntegrationOutboxRepository _outboxRepository;
    private readonly IGuidGenerator _guidGenerator;

    public IntegrationOutboxManager(
        IIntegrationOutboxRepository outboxRepository,
        IGuidGenerator guidGenerator)
    {
        _outboxRepository = outboxRepository;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Deterministic dedup key: a SHA-256 digest over (message type, appointment, version). Two
    /// enqueues describing the SAME state of the same appointment collapse to one row, so a
    /// redelivered domain event cannot push a duplicate case -- while a genuinely newer version of
    /// the appointment produces a different key and is therefore pushed.
    ///
    /// <para>Hashed rather than concatenated so the column stays bounded and carries no readable
    /// identifiers.</para>
    /// </summary>
    public static string BuildIdempotencyKey(
        IntegrationMessageType messageType,
        Guid appointmentId,
        string version)
    {
        var material = string.Create(
            CultureInfo.InvariantCulture,
            $"{messageType}|{appointmentId:D}|{version}");

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(digest);
    }

    /// <summary>
    /// Idempotent enqueue: if a row already exists for this key within the current office, returns
    /// it untouched; otherwise inserts a new Pending row.
    /// </summary>
    public virtual async Task<IntegrationOutboxItem> EnqueueAsync(
        Guid? tenantId,
        IntegrationMessageType messageType,
        string targetPath,
        Guid appointmentId,
        string payload,
        string idempotencyKey)
    {
        Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));

        var queryable = await _outboxRepository.GetQueryableAsync();
        var existing = queryable.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey);
        if (existing != null)
        {
            return existing; // idempotent: a replayed enqueue collapses to the existing row
        }

        var item = new IntegrationOutboxItem(
            _guidGenerator.Create(),
            tenantId,
            messageType,
            targetPath,
            appointmentId,
            payload,
            idempotencyKey);

        return await _outboxRepository.InsertAsync(item, autoSave: true);
    }

    /// <summary>
    /// Claims up to <paramref name="batchSize"/> due Pending rows (oldest first) via the ATOMIC
    /// status-gated lease, so overlapping drains never collide on save: a row already leased
    /// elsewhere updates 0 rows and is skipped. A freshly-leased row is reloaded so the send and the
    /// subsequent mark run against its current state (the lease UPDATE bypasses the change tracker).
    /// </summary>
    public virtual async Task<List<IntegrationOutboxItem>> ClaimDueBatchAsync(
        DateTime nowUtc,
        TimeSpan leaseDuration,
        int batchSize)
    {
        var leaseUntil = nowUtc.Add(leaseDuration);
        var queryable = await _outboxRepository.GetQueryableAsync();
        var candidateIds = queryable
            .Where(x => x.Status == IntegrationOutboxStatus.Pending
                && (x.LockedUntil == null || x.LockedUntil <= nowUtc)
                && (x.NextAttemptAt == null || x.NextAttemptAt <= nowUtc))
            .OrderBy(x => x.CreationTime)
            .Take(batchSize)
            .Select(x => x.Id)
            .ToList();

        var claimed = new List<IntegrationOutboxItem>();
        foreach (var id in candidateIds)
        {
            if (await _outboxRepository.TryLeaseAsync(id, nowUtc, leaseUntil))
            {
                claimed.Add(await _outboxRepository.GetAsync(id));
            }
        }

        return claimed;
    }

    /// <summary>Persists a post-send state transition (MarkSent / MarkFailed / MarkFatal).</summary>
    public virtual Task SaveAsync(IntegrationOutboxItem item) =>
        _outboxRepository.UpdateAsync(item, autoSave: true);
}
