using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Rebuilds a dead letter as a fresh message from CURRENT data, for the manual retry (#961). Returns
/// every row it queued, or collapsed onto, so the caller can judge the result rather than trust it
/// (<see cref="DeadLetterRetryOutcome"/>).
///
/// <para>An intake is rebuilt as an intake, as the retry always did. A DOCUMENT update is rebuilt as a
/// document update. It used to be retried as a full intake too, and that could not deliver a
/// deletion: the contract removes a document only through an explicit <c>deleted: true</c> entry on
/// the document endpoint, and a re-push "does NOT drop others". So a failed deletion was reported as
/// retried and the Case Tracker kept the document.</para>
///
/// <para>"Current data" for a document update means the current state of each document the dead
/// letter listed, not its stored snapshot: an Accepted document is sent as its current entry, anything
/// else as a deletion (mirroring reject-after-accept, which the live flow also sends as a deletion).
/// Packets are sent if they still resolve and otherwise left out, because the portal never deletes a
/// packet on the Case Tracker side; a regenerated packet arrives through its own update.</para>
/// </summary>
public class CaseTrackerDeadLetterRequeuer : ITransientDependency
{
    private readonly ICaseTrackerIntakeQueue _intakeQueue;
    private readonly ICaseTrackerDocumentQueue _documentQueue;
    private readonly IDocumentListResolver _documentListResolver;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IClock _clock;

    public CaseTrackerDeadLetterRequeuer(
        ICaseTrackerIntakeQueue intakeQueue,
        ICaseTrackerDocumentQueue documentQueue,
        IDocumentListResolver documentListResolver,
        IRepository<Appointment, Guid> appointmentRepository,
        IClock clock)
    {
        _intakeQueue = intakeQueue;
        _documentQueue = documentQueue;
        _documentListResolver = documentListResolver;
        _appointmentRepository = appointmentRepository;
        _clock = clock;
    }

    /// <summary>
    /// Queues the rebuilt message(s) inside the current office scope and returns what was queued.
    /// Must run in that office's unit of work: the queues write through the ambient transaction.
    /// </summary>
    public virtual async Task<IReadOnlyList<IntegrationOutboxItem>> RequeueAsync(
        IntegrationOutboxItem deadLetter,
        Guid officeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deadLetter);

        if (deadLetter.MessageType == IntegrationMessageType.DocumentUpdate)
        {
            var documentRows = await RequeueDocumentUpdateAsync(deadLetter, officeId, cancellationToken);
            if (documentRows.Count > 0)
            {
                return documentRows;
            }

            // The document queue writes nothing for an appointment with no intake row (#931), and
            // nothing when every listed item is gone. Either way the intake is what carries the
            // current document list, so that is what goes.
        }

        return new[] { await _intakeQueue.EnqueueIntakeAsync(deadLetter.AppointmentId, officeId, cancellationToken) };
    }

    private async Task<List<IntegrationOutboxItem>> RequeueDocumentUpdateAsync(
        IntegrationOutboxItem deadLetter,
        Guid officeId,
        CancellationToken cancellationToken)
    {
        var listed = ParseListedItems(deadLetter.Payload);
        var upserts = await ResolveCurrentPacketsAsync(deadLetter.AppointmentId, listed, cancellationToken);
        var removals = new List<DocumentDeletionEntry>();

        foreach (var documentId in listed.Where(i => !i.IsPacket).Select(i => i.Id).Distinct())
        {
            var entry = await _documentListResolver.ResolveDocumentAsync(documentId, officeId, cancellationToken);
            if (entry != null && string.Equals(entry.Status, nameof(DocumentStatus.Accepted), StringComparison.Ordinal))
            {
                upserts.Add(entry);
            }
            else
            {
                removals.Add(new DocumentDeletionEntry
                {
                    Id = documentId,
                    UpdatedAt = IntegrationTimestamp.ToIsoUtc(_clock.Now),
                });
            }
        }

        var queued = new List<IntegrationOutboxItem>();
        if (upserts.Count > 0)
        {
            AddIfWritten(queued, await _documentQueue.EnqueueDocumentEntriesAsync(
                deadLetter.AppointmentId, officeId, upserts, cancellationToken));
        }

        if (removals.Count > 0)
        {
            AddIfWritten(queued, await _documentQueue.EnqueueDeletionsAsync(
                deadLetter.AppointmentId, officeId, removals, cancellationToken));
        }

        return queued;
    }

    private async Task<List<IntakeDocumentEntry>> ResolveCurrentPacketsAsync(
        Guid appointmentId,
        IReadOnlyList<ListedItem> listed,
        CancellationToken cancellationToken)
    {
        var packetIds = listed.Where(i => i.IsPacket).Select(i => i.Id).ToHashSet();
        if (packetIds.Count == 0)
        {
            return new List<IntakeDocumentEntry>();
        }

        var appointment = await _appointmentRepository.FindAsync(appointmentId, cancellationToken: cancellationToken);
        if (appointment == null)
        {
            return new List<IntakeDocumentEntry>();
        }

        var packets = await _documentListResolver.ResolvePacketsAsync(appointment, cancellationToken);
        return packets.Where(p => packetIds.Contains(p.Id)).ToList();
    }

    /// <summary>
    /// The items a stored document-update payload named: a JSON array of entries (with a
    /// <c>source</c>) or deletions (<c>{ id, deleted: true }</c>). A deletion is always a document,
    /// never a packet, because the portal never deletes packets.
    /// </summary>
    private static List<ListedItem> ParseListedItems(string payload)
    {
        using var json = JsonDocument.Parse(payload);
        return json.RootElement.EnumerateArray()
            .Select(e => new ListedItem(
                e.GetProperty("id").GetGuid(),
                e.TryGetProperty("source", out var source)
                    && source.ValueKind == JsonValueKind.String
                    && string.Equals(source.GetString(), DocumentEntryMapper.PacketSource, StringComparison.Ordinal)))
            .ToList();
    }

    private static void AddIfWritten(List<IntegrationOutboxItem> queued, IntegrationOutboxItem? row)
    {
        if (row != null)
        {
            queued.Add(row);
        }
    }

    private sealed record ListedItem(Guid Id, bool IsPacket);
}
