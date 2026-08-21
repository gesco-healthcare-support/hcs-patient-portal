using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The document-update enqueue seam. Extracted so the trigger handlers are unit-testable without the
/// outbox repository behind them -- the same reason <see cref="IIntakePayloadBuilder"/> exists. The
/// handlers' logic is entirely about WHEN to publish, so that is what the tests should exercise.
/// </summary>
public interface ICaseTrackerDocumentQueue
{
    /// <summary>Upserts entries. Returns null when <paramref name="entries"/> is empty.</summary>
    Task<IntegrationOutboxItem?> EnqueueDocumentEntriesAsync(
        Guid appointmentId,
        Guid? tenantId,
        IReadOnlyList<IntakeDocumentEntry> entries,
        CancellationToken cancellationToken = default);

    /// <summary>Publishes tombstones. Returns null when <paramref name="entries"/> is empty.</summary>
    Task<IntegrationOutboxItem?> EnqueueDeletionsAsync(
        Guid appointmentId,
        Guid? tenantId,
        IReadOnlyList<DocumentDeletionEntry> entries,
        CancellationToken cancellationToken = default);
}
