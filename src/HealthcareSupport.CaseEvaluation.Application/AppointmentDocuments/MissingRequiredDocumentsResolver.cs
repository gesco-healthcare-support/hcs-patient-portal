using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Documents;
using HealthcareSupport.CaseEvaluation.PackageDetails;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// 2026-06-09: injectable resolver for "which required documents are still
/// outstanding for an appointment". Mirrors the inline computation in
/// <c>AppointmentDocumentsAppService.GetMissingRequiredDocumentsAsync</c>, exposed
/// as a service so the document-email handlers (the RemainingDocs templates) can
/// reuse it without depending on the permission-gated AppService. The AppService
/// keeps its own copy for now; pointing it here is the eventual consolidation
/// flagged in <c>AppointmentDocuments/CLAUDE.md</c> ("consolidate when next touched").
///
/// <para>Required = active master <see cref="Document"/>s linked (active) to the
/// active <see cref="PackageDetail"/>(s) for the appointment's type. A
/// requirement is satisfied only when an <see cref="AppointmentDocument"/>
/// references it (<c>SourceDocumentId</c>) AND is <c>Accepted</c>; otherwise it
/// is reported in its most-actionable state via
/// <see cref="RequiredDocumentEvaluator"/>.</para>
/// </summary>
public class MissingRequiredDocumentsResolver : ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IPackageDetailRepository _packageDetailRepository;
    private readonly IRepository<Document, Guid> _masterDocumentRepository;
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public MissingRequiredDocumentsResolver(
        IRepository<Appointment, Guid> appointmentRepository,
        IPackageDetailRepository packageDetailRepository,
        IRepository<Document, Guid> masterDocumentRepository,
        IRepository<AppointmentDocument, Guid> documentRepository,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _appointmentRepository = appointmentRepository;
        _packageDetailRepository = packageDetailRepository;
        _masterDocumentRepository = masterDocumentRepository;
        _documentRepository = documentRepository;
        _asyncExecuter = asyncExecuter;
    }

    public virtual async Task<MissingRequiredDocumentsResult> ResolveAsync(Guid appointmentId)
    {
        if (appointmentId == Guid.Empty)
        {
            return MissingRequiredDocumentsResult.Empty;
        }

        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        if (appointment == null)
        {
            return MissingRequiredDocumentsResult.Empty;
        }

        // Required = active Documents linked (active) to the active PackageDetail(s)
        // for this appointment's type. Unions multiple active packages.
        var packageQueryable = await _packageDetailRepository.GetQueryableAsync();
        var requiredDocumentIds = await _asyncExecuter.ToListAsync(
            packageQueryable
                .Where(p => p.IsActive && p.AppointmentTypeId == appointment.AppointmentTypeId)
                .SelectMany(p => p.DocumentPackages)
                .Where(dp => dp.IsActive)
                .Select(dp => dp.DocumentId)
                .Distinct());

        if (requiredDocumentIds.Count == 0)
        {
            return MissingRequiredDocumentsResult.Empty;
        }

        var masterQueryable = await _masterDocumentRepository.GetQueryableAsync();
        var required = await _asyncExecuter.ToListAsync(
            masterQueryable
                .Where(d => requiredDocumentIds.Contains(d.Id) && d.IsActive)
                .OrderBy(d => d.Name)
                .Select(d => new { d.Id, d.Name }));

        var documentQueryable = await _documentRepository.GetQueryableAsync();
        var existing = await _asyncExecuter.ToListAsync(
            documentQueryable
                .Where(x => x.AppointmentId == appointmentId)
                .Select(x => new { x.SourceDocumentId, x.Status }));

        var missing = RequiredDocumentEvaluator.Evaluate(
            required.Select(r => (r.Id, r.Name)),
            existing.Select(e => (e.SourceDocumentId, e.Status)));

        return new MissingRequiredDocumentsResult(required.Count, missing);
    }
}

/// <summary>
/// Result of <see cref="MissingRequiredDocumentsResolver"/>: the total required
/// count and the still-outstanding documents (name + state).
/// </summary>
public sealed record MissingRequiredDocumentsResult(
    int RequiredCount,
    IReadOnlyList<MissingRequiredDocument> Missing)
{
    public static readonly MissingRequiredDocumentsResult Empty =
        new(0, Array.Empty<MissingRequiredDocument>());
}
