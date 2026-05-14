using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Phase 3 (Category 3, 2026-05-10) -- separate clinic-staff notification
/// when an external user submits a cancellation request. Mirrors OLD
/// <c>AppointmentChangeRequestDomain.cs</c>:659 -- a distinct email goes
/// to the clinic-staff inbox (not the stakeholder fan-out) so the office
/// sees the request in their primary mailbox.
///
/// <para>Recipient: per-tenant <c>NotificationsPolicy.OfficeEmail</c>
/// setting. Skipped silently when the setting is empty (no clinic-staff
/// inbox configured for the tenant).</para>
///
/// <para>Filter: only fires on <c>ChangeRequestType.Cancel</c> -- the
/// reschedule submit goes through <c>ChangeRequestSubmittedEmailHandler</c>
/// which already fans out to all stakeholders (office included).</para>
/// </summary>
public class ClinicalStaffCancellationEmailHandler :
    ILocalEventHandler<AppointmentChangeRequestSubmittedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;
    private readonly ISettingProvider _settingProvider;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<ClinicalStaffCancellationEmailHandler> _logger;

    public ClinicalStaffCancellationEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IRepository<AppointmentChangeRequest, Guid> changeRequestRepository,
        ISettingProvider settingProvider,
        ICurrentTenant currentTenant,
        ILogger<ClinicalStaffCancellationEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _changeRequestRepository = changeRequestRepository;
        _settingProvider = settingProvider;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentChangeRequestSubmittedEto eventData)
    {
        if (eventData == null || eventData.ChangeRequestType != ChangeRequestType.Cancel)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var officeEmail = await _settingProvider.GetOrNullAsync(
                CaseEvaluationSettings.NotificationsPolicy.OfficeEmail);
            if (string.IsNullOrWhiteSpace(officeEmail))
            {
                _logger.LogInformation(
                    "ClinicalStaffCancellationEmailHandler: tenant {TenantId} has no OfficeEmail configured; skipping cancel notification.",
                    eventData.TenantId);
                return;
            }

            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, appointmentDocumentId: null);
            if (ctx == null)
            {
                _logger.LogWarning(
                    "ClinicalStaffCancellationEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var changeRequest = await _changeRequestRepository.FindAsync(eventData.ChangeRequestId);
            if (changeRequest == null)
            {
                _logger.LogWarning(
                    "ClinicalStaffCancellationEmailHandler: change request {ChangeRequestId} not found; skipping.",
                    eventData.ChangeRequestId);
                return;
            }

            var variables = DocumentNotificationContext.BuildVariables(
                patientFirstName: ctx.PatientFirstName,
                patientLastName: ctx.PatientLastName,
                patientEmail: ctx.PatientEmail,
                requestConfirmationNumber: ctx.RequestConfirmationNumber,
                appointmentDate: ctx.AppointmentDate,
                claimNumber: ctx.ClaimNumber,
                wcabAdj: ctx.WcabAdj,
                documentName: null,
                rejectionNotes: null,
                clinicName: _currentTenant.Name,
                portalUrl: ctx.PortalBaseUrl);

            var withReason = new Dictionary<string, object?>(variables, StringComparer.Ordinal)
            {
                ["CancellationReason"] = changeRequest.CancellationReason ?? string.Empty,
            };

            var recipients = new List<NotificationRecipient>
            {
                new NotificationRecipient(
                    email: officeEmail!,
                    role: RecipientRole.OfficeAdmin,
                    isRegistered: false),
            };

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.ClinicalStaffCancellation,
                recipients: recipients,
                variables: withReason,
                contextTag: $"ClinicalStaffCancellation/{eventData.ChangeRequestId}");
        }
    }
}
