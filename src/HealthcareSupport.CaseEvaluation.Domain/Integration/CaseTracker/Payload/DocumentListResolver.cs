using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;
using HealthcareSupport.CaseEvaluation.Appointments;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Builds the document list: the union of uploaded documents and generated packets for one
/// appointment, restricted to rows that actually have bytes in MinIO.
///
/// <para>Omitting the non-fetchable rows is the point. A required document is queued at booking
/// time as a <c>Pending</c> placeholder with no object, and packets exist as rows while they are
/// still rendering -- publishing either would hand the receiver an object key that 404s. They
/// reappear through the document-update feed (Part 2) once they become fetchable.</para>
/// </summary>
public class DocumentListResolver : IDocumentListResolver, ITransientDependency
{
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly IRepository<AppointmentPacket, Guid> _packetRepository;
    private readonly IRepository<AppointmentDocumentType, Guid> _documentTypeRepository;

    public DocumentListResolver(
        IRepository<AppointmentDocument, Guid> documentRepository,
        IRepository<AppointmentPacket, Guid> packetRepository,
        IRepository<AppointmentDocumentType, Guid> documentTypeRepository)
    {
        _documentRepository = documentRepository;
        _packetRepository = packetRepository;
        _documentTypeRepository = documentTypeRepository;
    }

    public virtual async Task<List<IntakeDocumentEntry>> ResolveAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        if (appointment is null)
        {
            throw new ArgumentNullException(nameof(appointment));
        }

        var entries = new List<IntakeDocumentEntry>();
        entries.AddRange(await ResolveDocumentsAsync(appointment, cancellationToken));
        entries.AddRange(await ResolvePacketsAsync(appointment, cancellationToken));

        return entries.OrderBy(e => e.CreatedAtUtc, StringComparer.Ordinal).ToList();
    }

    private async Task<List<IntakeDocumentEntry>> ResolveDocumentsAsync(
        Appointment appointment,
        CancellationToken cancellationToken)
    {
        var documents = await _documentRepository.GetListAsync(
            d => d.AppointmentId == appointment.Id, cancellationToken: cancellationToken);

        var fetchable = documents.Where(DocumentEntryMapper.IsFetchable).ToList();
        if (fetchable.Count == 0)
        {
            return new List<IntakeDocumentEntry>();
        }

        var typeNames = await ResolveTypeNamesAsync(fetchable, cancellationToken);

        return fetchable
            .Select(d => DocumentEntryMapper.FromDocument(d, ResolveTypeLabel(d, typeNames), appointment.TenantId))
            .ToList();
    }

    /// <summary>
    /// One uploaded document, mapped exactly as the full list would map it -- including its resolved
    /// category label -- so a document published by the accept trigger is byte-identical to the same
    /// document published by an intake push.
    /// </summary>
    public virtual async Task<IntakeDocumentEntry?> ResolveDocumentAsync(
        Guid documentId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.FindAsync(documentId, cancellationToken: cancellationToken);
        if (document == null || !DocumentEntryMapper.IsFetchable(document))
        {
            return null;
        }

        var typeNames = await ResolveTypeNamesAsync(
            new List<AppointmentDocument> { document }, cancellationToken);

        return DocumentEntryMapper.FromDocument(
            document, ResolveTypeLabel(document, typeNames), tenantId);
    }

    public virtual async Task<List<IntakeDocumentEntry>> ResolvePacketsAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        if (appointment is null)
        {
            throw new ArgumentNullException(nameof(appointment));
        }

        var packets = await _packetRepository.GetListAsync(
            p => p.AppointmentId == appointment.Id, cancellationToken: cancellationToken);

        return packets
            .Where(DocumentEntryMapper.IsFetchable)
            .Select(p => DocumentEntryMapper.FromPacket(
                p, appointment.RequestConfirmationNumber, appointment.TenantId))
            .ToList();
    }

    /// <summary>Resolves the referenced categories in one query rather than per document.</summary>
    private async Task<Dictionary<Guid, string>> ResolveTypeNamesAsync(
        List<AppointmentDocument> documents,
        CancellationToken cancellationToken)
    {
        var typeIds = documents
            .Where(d => d.AppointmentDocumentTypeId.HasValue)
            .Select(d => d.AppointmentDocumentTypeId!.Value)
            .Distinct()
            .ToList();

        if (typeIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var types = await _documentTypeRepository.GetListAsync(
            t => typeIds.Contains(t.Id), cancellationToken: cancellationToken);

        return types.ToDictionary(t => t.Id, t => t.Name);
    }

    /// <summary>
    /// The chosen category's name, else the free-text label captured when the uploader picked
    /// "Other", else null. Mirrors how the portal itself labels a document.
    /// </summary>
    private static string? ResolveTypeLabel(AppointmentDocument document, Dictionary<Guid, string> typeNames)
    {
        if (document.AppointmentDocumentTypeId is { } typeId && typeNames.TryGetValue(typeId, out var name))
        {
            return name;
        }

        return document.OtherDocumentTypeName;
    }
}
