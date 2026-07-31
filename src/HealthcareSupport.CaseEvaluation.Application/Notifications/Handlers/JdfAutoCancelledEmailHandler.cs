using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
/// Phase 14b (2026-05-04) -- subscribes to
/// <see cref="AppointmentAutoCancelledEto"/> (specifically the
/// JDF-not-uploaded reason) and dispatches the OLD-parity
/// <c>JDFAutoCancelled</c> email to all stakeholders on the
/// appointment.
///
/// <para>OLD parity: spec line 419 says "a notification email will be
/// sent to all the stakeholders related to the appointment." NEW uses
/// the existing <c>IAppointmentRecipientResolver</c> (Session A's
/// shared recipient walker) to fan out to office, booker, patient,
/// applicant + defense attorneys, claim examiner, primary insurance,
/// employer.</para>
///
/// <para>Filters by <see cref="AppointmentAutoCancelledEto.Reason"/>
/// so a future generic auto-cancel reason (e.g.,
/// <c>"due-date-elapsed"</c>) gets its own template-coded handler
/// without firing this one twice.</para>
/// </summary>
public class JdfAutoCancelledEmailHandler :
    ILocalEventHandler<AppointmentAutoCancelledEto>,
    ITransientDependency
{
    private const string JdfReason = "JDF-not-uploaded";

    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<JdfAutoCancelledEmailHandler> _logger;

    public JdfAutoCancelledEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        ICurrentTenant currentTenant,
        ILogger<JdfAutoCancelledEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentAutoCancelledEto eventData)
    {
        if (eventData == null || !string.Equals(eventData.Reason, JdfReason, StringComparison.Ordinal))
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, appointmentDocumentId: null);
            if (ctx == null)
            {
                _logger.LogWarning(
                    "JdfAutoCancelledEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var resolverOutput = await _recipientResolver.ResolveAsync(
                eventData.AppointmentId,
                NotificationKind.JdfAutoCancelled);

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
                    "JdfAutoCancelledEmailHandler: no recipients resolved for appointment {AppointmentId}; skipping.",
                    eventData.AppointmentId);
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
                documentName: "Joint Declaration Form",
                rejectionNotes: null,
                clinicName: _currentTenant.Name,
                portalUrl: ctx.PortalBaseUrl);

            // OLD-verbatim per docs/parity/it-admin-notification-templates.md
            // Phase 1 scope: "JDF auto-cancel" -> AppointmentCancelledDueDate
            // (EmailTemplate disk HTML).
            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.AppointmentCancelledDueDate,
                recipients: recipients,
                variables: variables,
                contextTag: $"JdfAutoCancelled/{eventData.AppointmentId}");
        }
    }
}
