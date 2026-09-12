using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Shared enqueue path for a document-update push, mirroring <see cref="CaseTrackerIntakeQueue"/>.
/// Every document trigger -- accept, reject, delete, packet completion, the reconciliation release --
/// funnels through here so they cannot drift into differently-shaped messages.
///
/// <para>Separate from the intake queue rather than folded into it because the two build genuinely
/// different bodies (envelope vs bare array) and version their idempotency keys off different facts
/// (the appointment's <c>UpdatedAt</c> vs the entry set).</para>
/// </summary>
public class CaseTrackerDocumentQueue : ICaseTrackerDocumentQueue, ITransientDependency
{
    private readonly IntegrationOutboxManager _outboxManager;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<CaseTrackerDocumentQueue> _logger;

    public CaseTrackerDocumentQueue(
        IntegrationOutboxManager outboxManager,
        IBackgroundJobManager backgroundJobManager,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<CaseTrackerDocumentQueue> logger)
    {
        _outboxManager = outboxManager;
        _backgroundJobManager = backgroundJobManager;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    /// <summary>
    /// Discriminates the two bodies inside the idempotency key. Without it, accepting and then
    /// rejecting a document within the same second would hash identically and the rejection would be
    /// silently swallowed by the idempotent enqueue.
    /// </summary>
    private const string EntriesKind = "entries";

    private const string DeletionsKind = "deletions";

    /// <summary>
    /// Enqueues an upsert for the given entries. Returns the ledger row, or <c>null</c> when there is
    /// nothing to say -- an empty array would tell the receiver the appointment has NO documents,
    /// which is a destructive statement rather than a no-op.
    /// </summary>
    public virtual Task<IntegrationOutboxItem?> EnqueueDocumentEntriesAsync(
        Guid appointmentId,
        Guid? tenantId,
        IReadOnlyList<IntakeDocumentEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return EnqueueAsync(
            appointmentId,
            tenantId,
            entries.Count == 0 ? null : IntakePayloadSerializer.SerializeDocumentEntries(entries),
            EntriesKind,
            entries.Select(e => (e.Id, e.UpdatedAt)));
    }

    /// <summary>Enqueues tombstones for documents the portal has removed or repudiated.</summary>
    public virtual Task<IntegrationOutboxItem?> EnqueueDeletionsAsync(
        Guid appointmentId,
        Guid? tenantId,
        IReadOnlyList<DocumentDeletionEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return EnqueueAsync(
            appointmentId,
            tenantId,
            entries.Count == 0 ? null : IntakePayloadSerializer.SerializeDeletionEntries(entries),
            DeletionsKind,
            entries.Select(e => (e.Id, e.UpdatedAt)));
    }

    private async Task<IntegrationOutboxItem?> EnqueueAsync(
        Guid appointmentId,
        Guid? tenantId,
        string? payloadJson,
        string kind,
        IEnumerable<(Guid Id, string UpdatedAt)> keyParts)
    {
        if (payloadJson == null)
        {
            return null;
        }

        var row = await _outboxManager.EnqueueAsync(
            tenantId,
            IntegrationMessageType.DocumentUpdate,
            CaseTrackerEndpoints.DocumentUpdate(appointmentId),
            appointmentId,
            payloadJson,
            IntegrationOutboxManager.BuildIdempotencyKey(
                IntegrationMessageType.DocumentUpdate,
                appointmentId,
                BuildVersion(kind, keyParts)));

        ScheduleDrain(tenantId, appointmentId);

        return row;
    }

    /// <summary>
    /// Versions the key by the entry SET: each id paired with its own stamp, ordered so that listing
    /// the same documents differently is recognised as the same message. A genuine change to any one
    /// document changes its stamp and therefore the key, so the update is pushed.
    /// </summary>
    private static string BuildVersion(string kind, IEnumerable<(Guid Id, string UpdatedAt)> parts)
    {
        var ordered = parts
            .Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.Id:D}@{p.UpdatedAt}"))
            .OrderBy(s => s, StringComparer.Ordinal);

        return string.Create(CultureInfo.InvariantCulture, $"{kind}|{string.Join(",", ordered)}");
    }

    /// <summary>
    /// Enqueues the drain AFTER the current unit of work commits -- see
    /// <see cref="CaseTrackerIntakeQueue"/> for why inline enqueueing races the transaction. These
    /// handlers run inside a staff action's UoW, so the race is the normal case here, not the edge.
    /// </summary>
    private void ScheduleDrain(Guid? tenantId, Guid appointmentId)
    {
        var args = new IntegrationOutboxDrainArgs { TenantId = tenantId };
        var currentUow = _unitOfWorkManager.Current;

        if (currentUow == null)
        {
            _ = _backgroundJobManager.EnqueueAsync(args);
            return;
        }

        currentUow.OnCompleted(async () =>
        {
            try
            {
                await _backgroundJobManager.EnqueueAsync(args);
            }
            catch (ObjectDisposedException ex)
            {
                // Losing one enqueue is recoverable: the row is committed and the 15-minute sweep
                // re-drives it. Propagating would fail an otherwise successful staff action.
                _logger.LogWarning(
                    ex,
                    "CaseTrackerDocumentQueue: drain enqueue skipped for appointment {AppointmentId} -- DI scope disposed before OnCompleted fired.",
                    appointmentId);
            }
        });
    }
}
