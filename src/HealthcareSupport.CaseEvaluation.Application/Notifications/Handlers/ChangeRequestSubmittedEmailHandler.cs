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
/// C4 / Phase 18 (2026-05-04) -- subscribes to
/// <see cref="AppointmentChangeRequestSubmittedEto"/> and dispatches the
/// "submit" stakeholder-notification email through Phase 18's
/// <see cref="INotificationDispatcher"/>. Branches on
/// <see cref="ChangeRequestType"/> to pick the OLD-verbatim template code:
/// <c>AppointmentCancelledRequest</c> (TemplateCode 4) for Cancel,
/// <c>AppointmentRescheduleRequest</c> (TemplateCode 7) for Reschedule.
///
/// <para>Mirrors OLD's behavior at
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentChangeRequestDomain.cs</c>:215
/// (reschedule-submit triggers <c>SendEmailData</c> with
/// <c>rescheduleRequested=true</c>, fan-out to
/// <c>appointmentStackHoldersEmailPhone.EmailList</c>). OLD did not send
/// a cancel-submit email; NEW fans out for both as a deliberate
/// parity-improvement (the <c>AppointmentCancelledRequest</c> code is
/// already in OLD's TemplateCode enum).</para>
///
/// <para>Recipient resolution goes through
/// <see cref="IAppointmentRecipientResolver"/> with
/// <see cref="NotificationKind.Submitted"/> -- same resolver used by all
/// stakeholder fan-out events.</para>
/// </summary>
public class ChangeRequestSubmittedEmailHandler :
    ILocalEventHandler<AppointmentChangeRequestSubmittedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<ChangeRequestSubmittedEmailHandler> _logger;

    public ChangeRequestSubmittedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        ICurrentTenant currentTenant,
        ILogger<ChangeRequestSubmittedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentChangeRequestSubmittedEto eventData)
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
                    "ChangeRequestSubmittedEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var resolverOutput = await _recipientResolver.ResolveAsync(
                eventData.AppointmentId,
                NotificationKind.Submitted);

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
                    "ChangeRequestSubmittedEmailHandler: no recipients resolved for appointment {AppointmentId}; skipping.",
                    eventData.AppointmentId);
                return;
            }

            // OLD-verbatim template codes per docs/parity/it-admin-notification-templates.md.
            // TemplateCode 4 (AppointmentCancelledRequest) / TemplateCode 7
            // (AppointmentRescheduleRequest) -- DB-managed in OLD.
            var templateCode = eventData.ChangeRequestType switch
            {
                ChangeRequestType.Cancel => NotificationTemplateConsts.Codes.AppointmentCancelledRequest,
                ChangeRequestType.Reschedule => NotificationTemplateConsts.Codes.AppointmentRescheduleRequest,
                _ => NotificationTemplateConsts.Codes.AppointmentCancelledRequest,
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
                contextTag: $"ChangeRequestSubmitted/{eventData.ChangeRequestType}/{eventData.ChangeRequestId}");
        }
    }
}
