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
/// <see cref="AppointmentChangeRequestApprovedEto"/> and dispatches
/// the OLD-parity stakeholder-notification email through Phase 18's
/// <see cref="INotificationDispatcher"/>. Branches on
/// <see cref="ChangeRequestType"/> to pick the right
/// <c>NotificationTemplateConsts.Codes.*</c>.
///
/// <para>Mirrors OLD's
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentChangeRequestDomain.cs</c>:289-306
/// (cancellation-approved fan-out) + 555-718 (reschedule-approved
/// fan-out + admin-override branch). NEW unifies the two paths via
/// the per-recipient resolver.</para>
/// </summary>
public class ChangeRequestApprovedEmailHandler :
    ILocalEventHandler<AppointmentChangeRequestApprovedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<ChangeRequestApprovedEmailHandler> _logger;

    public ChangeRequestApprovedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        ICurrentTenant currentTenant,
        ILogger<ChangeRequestApprovedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentChangeRequestApprovedEto eventData)
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
                    "ChangeRequestApprovedEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var notificationKind = eventData.ChangeRequestType == ChangeRequestType.Cancel
                ? NotificationKind.Approved
                : NotificationKind.Approved;
            var resolverOutput = await _recipientResolver.ResolveAsync(
                eventData.AppointmentId,
                notificationKind);

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
                    "ChangeRequestApprovedEmailHandler: no recipients resolved for appointment {AppointmentId}; skipping.",
                    eventData.AppointmentId);
                return;
            }

            // Branch template by (ChangeRequestType, IsAdminOverride):
            //   Cancel + any -> CancellationRequestAccepted
            //   Reschedule + IsAdminOverride=false -> RescheduleApproved
            //   Reschedule + IsAdminOverride=true  -> RescheduleApproved
            //     (the admin-reason template variable carries the override
            //      context; OLD's separate "AdminReschedule" template path
            //      collapses into the same template with a populated
            //      ##AdminReScheduleReason## variable per the audit
            //      "Reschedule by admin (override)" gap row).
            var templateCode = eventData.ChangeRequestType switch
            {
                ChangeRequestType.Cancel => NotificationTemplateConsts.Codes.CancellationRequestAccepted,
                ChangeRequestType.Reschedule => NotificationTemplateConsts.Codes.RescheduleApproved,
                _ => NotificationTemplateConsts.Codes.CancellationRequestAccepted,
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
                rejectionNotes: null,
                clinicName: _currentTenant.Name,
                portalUrl: ctx.PortalBaseUrl);

            await _dispatcher.DispatchAsync(
                templateCode: templateCode,
                recipients: recipients,
                variables: variables,
                contextTag: $"ChangeRequestApproved/{eventData.ChangeRequestType}/{eventData.ChangeRequestId}");
        }
    }
}
