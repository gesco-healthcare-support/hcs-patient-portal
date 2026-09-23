using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
    /// Deterministic CONTENT key: a SHA-256 digest over (message type, appointment, version). Two
    /// enqueues describing the same state of the same appointment get the same key, so a redelivered
    /// domain event can be recognised -- while a genuinely different version gets a different key.
    /// Whether same content actually collapses onto an earlier row is decided in
    /// <see cref="EnqueueAsync"/> (#915); the key alone no longer decides it.
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
    /// Enqueues a message, or returns an earlier row that already carries the same content.
    /// <paramref name="idempotencyKey"/> is the CONTENT key from <see cref="BuildIdempotencyKey"/>.
    ///
    /// <para>WHEN SAME CONTENT COLLAPSES (#915). Only onto a row that is still Pending or already
    /// Sent: those mean the message is in hand or delivered, so a replayed event must not send it
    /// twice. A Failed row never arrived and a Resolved one was set aside by a person, so same content
    /// after either is a genuine new attempt and gets a new row. Before #915 any row with the key
    /// collapsed whatever its status, which is how a manual retry of an unchanged dead letter
    /// "succeeded" and sent nothing (#961).</para>
    ///
    /// <para>WHICH ROW IT IS COMPARED WITH. An intake is a full snapshot, so it is compared with the
    /// NEWEST intake row only: a value changed A -> B -> A must reach the Case Tracker a third time,
    /// and matching the first A (already Sent) is how that revert used to be dropped. A document
    /// update is a delta for particular documents, so it is compared with ANY earlier row: a replayed
    /// accept for an older document must still collapse after a newer document went out, and
    /// document keys cannot revert because each document's timestamp only moves forward.</para>
    ///
    /// <para>A new row takes the content key if it is free, otherwise the first free generation key
    /// (<see cref="BuildGenerationKey"/>), which satisfies the unique index without a new column.
    /// Both callers hold the per-appointment ordering lock, so two enqueues cannot pick the same
    /// generation; without it the unique index would refuse the second, as it always has.</para>
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

        var rows = await _outboxRepository.GetForAppointmentAsync(appointmentId, messageType);
        var sameContentKeys = ContentKeys(idempotencyKey, rows.Count);

        var comparedWith = messageType == IntegrationMessageType.Intake ? rows.Take(1) : rows;
        var existing = comparedWith.FirstOrDefault(r =>
            sameContentKeys.Contains(r.IdempotencyKey) && CanCollapseOnto(r));
        if (existing != null)
        {
            return existing; // a replayed enqueue of content already in hand or delivered
        }

        var usedKeys = rows.Select(r => r.IdempotencyKey).ToHashSet(StringComparer.Ordinal);
        var key = idempotencyKey;
        for (var generation = 1; usedKeys.Contains(key); generation++)
        {
            key = BuildGenerationKey(idempotencyKey, generation);
        }

        var item = new IntegrationOutboxItem(
            _guidGenerator.Create(),
            tenantId,
            messageType,
            targetPath,
            appointmentId,
            payload,
            key);

        return await _outboxRepository.InsertAsync(item, autoSave: true);
    }

    /// <summary>
    /// The key for the <paramref name="generation"/>-th re-send of the same content: a SHA-256 over
    /// the content key and the generation, so it is the same length as a content key and cannot
    /// collide with any other appointment's or message type's keys (the content key already carries
    /// both).
    /// </summary>
    public static string BuildGenerationKey(string contentKey, int generation)
    {
        var material = string.Create(CultureInfo.InvariantCulture, $"{contentKey}|generation|{generation}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    /// <summary>
    /// Every key a row carrying this content could have: the content key itself, or one of its
    /// generation keys. A generation is only ever taken when all lower ones are in use, so it can
    /// never exceed the number of rows that exist -- which bounds the set.
    /// </summary>
    private static HashSet<string> ContentKeys(string contentKey, int rowCount)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal) { contentKey };
        for (var generation = 1; generation <= rowCount; generation++)
        {
            keys.Add(BuildGenerationKey(contentKey, generation));
        }

        return keys;
    }

    private static bool CanCollapseOnto(IntegrationOutboxItem row) =>
        row.Status is IntegrationOutboxStatus.Pending or IntegrationOutboxStatus.Sent;

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

    /// <summary>
    /// Whether an intake has ever been queued for the appointment, in any status. See
    /// <see cref="IIntegrationOutboxRepository.HasIntakeAsync"/> for why every status counts and what
    /// the answer depends on.
    /// </summary>
    public virtual Task<bool> HasIntakeAsync(Guid appointmentId, CancellationToken cancellationToken = default) =>
        _outboxRepository.HasIntakeAsync(appointmentId, cancellationToken);

    /// <summary>
    /// Takes the per-appointment ordering lock until the current transaction ends. See
    /// <see cref="IIntegrationOutboxRepository.AcquireAppointmentLockAsync"/> for the race it closes.
    /// </summary>
    public virtual Task AcquireAppointmentLockAsync(Guid appointmentId, CancellationToken cancellationToken = default) =>
        _outboxRepository.AcquireAppointmentLockAsync(appointmentId, cancellationToken);
}
