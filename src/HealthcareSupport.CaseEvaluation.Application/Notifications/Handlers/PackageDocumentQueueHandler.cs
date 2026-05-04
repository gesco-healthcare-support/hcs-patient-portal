using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments.Events;
using HealthcareSupport.CaseEvaluation.Documents;
using HealthcareSupport.CaseEvaluation.PackageDetails;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Phase 12 (2026-05-04) / Phase 14 (2026-05-04) -- subscribes to
/// <see cref="AppointmentApprovedEto"/> and queues
/// <see cref="AppointmentDocument"/> rows in
/// <see cref="DocumentStatus.Pending"/> status, one per active
/// <see cref="DocumentPackage"/> linked to the
/// <see cref="PackageDetail"/> for the approved appointment's
/// <c>AppointmentTypeId</c>.
///
/// <para>OLD parity:
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>:560-566
/// fires <c>AddAppointmentDocumentsAndSendDocumentToEmail</c> on
/// approval. NEW splits this into (a) row queueing -- this handler --
/// and (b) email send -- a per-feature handler (Phase 14b) using
/// Phase 18's <c>INotificationDispatcher</c>. The split keeps domain
/// mutations isolated from delivery concerns.</para>
///
/// <para>Phase 14 closes the Phase 12 deferred row insert: the
/// <see cref="AppointmentDocumentManager.CreateQueuedAsync"/>
/// factory takes minimal data and produces a row with placeholder
/// file metadata until the patient uploads via the verification-code
/// link.</para>
/// </summary>
public class PackageDocumentQueueHandler :
    ILocalEventHandler<AppointmentApprovedEto>,
    ITransientDependency
{
    private readonly IPackageDetailRepository _packageDetailRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly AppointmentDocumentManager _documentManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<PackageDocumentQueueHandler> _logger;

    public PackageDocumentQueueHandler(
        IPackageDetailRepository packageDetailRepository,
        IRepository<Document, Guid> documentRepository,
        AppointmentDocumentManager documentManager,
        ICurrentTenant currentTenant,
        ILogger<PackageDocumentQueueHandler> logger)
    {
        _packageDetailRepository = packageDetailRepository;
        _documentRepository = documentRepository;
        _documentManager = documentManager;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentApprovedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            // Resolve the active PackageDetail rows for the appointment's
            // type. PackageDetail's IsActive gate + the AppointmentTypeId
            // FK both filter; OLD's spec says the staff approver picks
            // ONE package at approval time, but Phase 5's seed allows
            // multiple active packages per type. We queue documents for
            // every match -- if duplicates surface in practice, IT Admin
            // tightens to one active per type via the OneActive validator
            // (Phase 5).
            var packageQueryable = await _packageDetailRepository.GetQueryableAsync();
            var matchingPackages = packageQueryable
                .Where(p => p.IsActive && p.AppointmentTypeId == eventData.AppointmentTypeId)
                .Select(p => new
                {
                    p.Id,
                    p.PackageName,
                    DocumentIds = p.DocumentPackages
                        .Where(dp => dp.IsActive)
                        .Select(dp => dp.DocumentId)
                        .ToList(),
                })
                .ToList();

            if (matchingPackages.Count == 0)
            {
                _logger.LogInformation(
                    "PackageDocumentQueueHandler: no active PackageDetail for AppointmentTypeId={AppointmentTypeId} on appointment {AppointmentId}; nothing to queue.",
                    eventData.AppointmentTypeId,
                    eventData.AppointmentId);
                return;
            }

            // Resolve the Document master rows in one query so we can
            // copy their Name into AppointmentDocument.DocumentName --
            // gives the patient a recognizable label in the email link.
            var allDocumentIds = matchingPackages
                .SelectMany(p => p.DocumentIds)
                .Distinct()
                .ToList();

            if (allDocumentIds.Count == 0)
            {
                _logger.LogInformation(
                    "PackageDocumentQueueHandler: package matched but contains no active documents for AppointmentTypeId={AppointmentTypeId} on appointment {AppointmentId}.",
                    eventData.AppointmentTypeId,
                    eventData.AppointmentId);
                return;
            }

            var documentQueryable = await _documentRepository.GetQueryableAsync();
            var documents = documentQueryable
                .Where(d => allDocumentIds.Contains(d.Id) && d.IsActive)
                .Select(d => new { d.Id, d.Name })
                .ToList();
            var documentNamesById = documents.ToDictionary(d => d.Id, d => d.Name);

            var queuedCount = 0;
            foreach (var package in matchingPackages)
            {
                foreach (var documentId in package.DocumentIds)
                {
                    if (!documentNamesById.TryGetValue(documentId, out var documentName))
                    {
                        // Document master row missing or inactive --
                        // skip rather than crash the handler. Logged so
                        // IT Admin can fix the seed gap.
                        _logger.LogWarning(
                            "PackageDocumentQueueHandler: DocumentId {DocumentId} not found / inactive while queueing for appointment {AppointmentId} package {PackageName}; skipping.",
                            documentId,
                            eventData.AppointmentId,
                            package.PackageName);
                        continue;
                    }
                    await _documentManager.CreateQueuedAsync(
                        tenantId: eventData.TenantId,
                        appointmentId: eventData.AppointmentId,
                        documentName: documentName);
                    queuedCount++;
                }
            }

            _logger.LogInformation(
                "PackageDocumentQueueHandler: queued {QueuedCount} package document(s) for appointment {AppointmentId} (AppointmentTypeId={AppointmentTypeId}).",
                queuedCount,
                eventData.AppointmentId,
                eventData.AppointmentTypeId);
        }
    }
}
