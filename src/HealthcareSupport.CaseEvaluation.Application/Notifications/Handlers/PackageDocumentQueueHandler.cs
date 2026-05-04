using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Events;
using HealthcareSupport.CaseEvaluation.PackageDetails;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Phase 12 (2026-05-04) -- subscribes to
/// <see cref="AppointmentApprovedEto"/> and resolves the
/// <see cref="PackageDetail"/> linked to the approved appointment's
/// <c>AppointmentTypeId</c>. For each linked
/// <see cref="DocumentPackage"/> row, this handler logs the queue
/// intent so the Phase 14 (Document review) work can then create the
/// matching <c>AppointmentDocument</c> rows in <c>Pending</c> status
/// (the entity's current constructor demands file metadata that does
/// not exist pre-upload, so row insert lives in Phase 14 once the
/// entity gains a queued-state factory).
///
/// <para>OLD parity:
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>:560-566
/// fires <c>AddAppointmentDocumentsAndSendDocumentToEmail</c> on
/// approval. NEW splits this into (a) row queueing -- this handler --
/// and (b) email send -- a future per-feature email handler in Phase
/// 14 using Phase 18's <c>INotificationDispatcher</c>. The split
/// keeps domain mutations isolated from delivery concerns and matches
/// ABP's domain-event model (one observable event, multiple
/// independent subscribers).</para>
///
/// <para>Why the row insert is deferred to Phase 14: the
/// <c>AppointmentDocument</c> constructor at
/// <c>AppointmentDocument.cs</c>:92-120 calls
/// <c>Check.NotNullOrWhiteSpace</c> on <c>BlobName</c>,
/// <c>FileName</c>, and validates <c>FileSize</c>. A pre-upload
/// queued row has none of those values. Phase 14 (Document review)
/// owns <c>AppointmentDocument</c>'s queued-state surface; adding a
/// factory there is cleaner than working around the constructor here.</para>
/// </summary>
public class PackageDocumentQueueHandler :
    ILocalEventHandler<AppointmentApprovedEto>,
    ITransientDependency
{
    private readonly IPackageDetailRepository _packageDetailRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<PackageDocumentQueueHandler> _logger;

    public PackageDocumentQueueHandler(
        IPackageDetailRepository packageDetailRepository,
        ICurrentTenant currentTenant,
        ILogger<PackageDocumentQueueHandler> logger)
    {
        _packageDetailRepository = packageDetailRepository;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public virtual async Task HandleEventAsync(AppointmentApprovedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        // Tenant scope: switch to the publishing tenant so the
        // PackageDetail query filter resolves the right rows. The
        // local-event-bus delivers in the publisher's UoW, but the
        // tenant context can drift during async dispatch -- be
        // explicit.
        using (_currentTenant.Change(eventData.TenantId))
        {
            var queryable = await _packageDetailRepository.GetQueryableAsync();
            var matches = queryable
                .Where(p => p.IsActive && p.AppointmentTypeId == eventData.AppointmentTypeId)
                .Select(p => new
                {
                    p.Id,
                    p.PackageName,
                    DocumentCount = p.DocumentPackages.Count(dp => dp.IsActive),
                })
                .ToList();

            if (matches.Count == 0)
            {
                _logger.LogInformation(
                    "PackageDocumentQueueHandler: no active PackageDetail for AppointmentTypeId={AppointmentTypeId} on appointment {AppointmentId}; nothing to queue.",
                    eventData.AppointmentTypeId,
                    eventData.AppointmentId);
                return;
            }

            foreach (var packet in matches)
            {
                _logger.LogInformation(
                    "PackageDocumentQueueHandler: appointment {AppointmentId} matched package '{PackageName}' (PackageDetailId={PackageDetailId}) with {DocCount} active document(s); row insert deferred to Phase 14.",
                    eventData.AppointmentId,
                    packet.PackageName,
                    packet.Id,
                    packet.DocumentCount);
            }
        }
    }
}
