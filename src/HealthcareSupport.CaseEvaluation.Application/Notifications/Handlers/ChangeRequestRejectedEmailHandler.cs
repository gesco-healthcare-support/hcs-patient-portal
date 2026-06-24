using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Phase 17 (2026-05-04) -- subscribes to
/// <see cref="AppointmentChangeRequestRejectedEto"/> and dispatches
/// the OLD-parity rejection email to all stakeholders. Branches on
/// <see cref="ChangeRequestType"/> for the template code:
/// <c>CancellationRequestRejected</c> for cancel-reject,
/// <c>RescheduleRejected</c> for reschedule-reject. The verbatim
/// rejection notes are passed through via the
/// <c>##RejectionNotes##</c> variable.
/// </summary>
public class ChangeRequestRejectedEmailHandler :
    ILocalEventHandler<AppointmentChangeRequestRejectedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<ChangeRequestRejectedEmailHandler> _logger;

    public ChangeRequestRejectedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        ICurrentTenant currentTenant,
        ILogger<ChangeRequestRejectedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentChangeRequestRejectedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, appointmentDocumentId: null);
            if (ctx == null)
            {
                _logger.LogWarning(
                    "ChangeRequestRejectedEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var resolverOutput = await _recipientResolver.ResolveAsync(
                eventData.AppointmentId,
                NotificationKind.Rejected);

            var recipients = resolverOutput
                .Where(r => !string.IsNullOrWhiteSpace(r.To))
                .Select(r => new NotificationRecipient(
                    email: r.To,
                    role: r.Role,
                    isRegistered: r.IsRegistered))
                .ToList();

            if (recipients.Count == 0)
            {
                _logger.LogInformation(
                    "ChangeRequestRejectedEmailHandler: no recipients resolved for appointment {AppointmentId}; skipping.",
                    eventData.AppointmentId);
                return;
            }

            // OLD-verbatim template codes per docs/parity/it-admin-notification-templates.md
            // Phase 1 scope: TemplateCode 6 / 9 (DB-managed in OLD).
            var templateCode = eventData.ChangeRequestType switch
            {
                ChangeRequestType.Cancel => NotificationTemplateConsts.Codes.AppointmentCancelledRequestRejected,
                ChangeRequestType.Reschedule => NotificationTemplateConsts.Codes.AppointmentRescheduleRequestRejected,
                _ => NotificationTemplateConsts.Codes.AppointmentCancelledRequestRejected,
            };

            var variables = DocumentNotificationContext.BuildVariables(
                patientFirstName: ctx.PatientFirstName,
                patientLastName: ctx.PatientLastName,
                patientEmail: ctx.PatientEmail,
                requestConfirmationNumber: ctx.RequestConfirmationNumber,
                appointmentDate: ctx.AppointmentDate,
                claimNumber: ctx.ClaimNumber,
                wcabAdj: ctx.WcabAdj,
                documentName: null,
                rejectionNotes: eventData.RejectionNotes,
                clinicName: _currentTenant.Name,
                portalUrl: ctx.PortalBaseUrl);

            await _dispatcher.DispatchAsync(
                templateCode: templateCode,
                recipients: recipients,
                variables: variables,
                contextTag: $"ChangeRequestRejected/{eventData.ChangeRequestType}/{eventData.ChangeRequestId}");
        }
    }
}
