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
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// C4 / Phase 18 (2026-05-04) -- subscribes to
/// <see cref="AppointmentChangeRequestSubmittedEto"/> and dispatches the
/// "submit" stakeholder-notification email through Phase 18's
/// <see cref="INotificationDispatcher"/>. Branches on
/// <see cref="ChangeRequestType"/> to pick the OLD-verbatim template code.
///
/// <para>Phase 4c (2026-08-05, Adrian): CANCELLATIONS ONLY. A reschedule submit sends no
/// stakeholder email -- the consent email dispatched when staff confirm a date already tells
/// both sides a reschedule was requested, and names the date. This one could not: since 4b the
/// external path leaves <c>NewDoctorAvailabilityId</c> null, so its date variables rendered
/// empty. Cancellation keeps the email because its consent is issued at submit, leaving no
/// later message to fold the notice into.</para>
/// </summary>
public class ChangeRequestSubmittedEmailHandler :
    ILocalEventHandler<AppointmentChangeRequestSubmittedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<ChangeRequestSubmittedEmailHandler> _logger;

    public ChangeRequestSubmittedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        IRepository<AppointmentChangeRequest, Guid> changeRequestRepository,
        ICurrentTenant currentTenant,
        ILogger<ChangeRequestSubmittedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _changeRequestRepository = changeRequestRepository;
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

        // Phase 4c (2026-08-05, Adrian): RESCHEDULE submits send no stakeholder email. The
        // consent email that goes out when staff CONFIRM a date already tells both sides a
        // reschedule was requested -- and says which date, which this one cannot: since 4b the
        // external path leaves NewDoctorAvailabilityId null, so the NewAppointmentDate and
        // NewAppointmentFromTime variables below render EMPTY. This was the third stale reader
        // of the proposed slot, and the only one already reaching real inboxes.
        //
        // Deliberately scoped to the email only. The other two subscribers to this event are
        // untouched: staff still get their in-app notification (InAppNotificationHandlers) so
        // the request is visible in the queue, and the cancel-side clinical-staff email
        // (ClinicalStaffCancellationEmailHandler) is unaffected. CANCELLATION keeps this email
        // -- its consent is issued at submit, so there is no later message to fold it into.
        if (eventData.ChangeRequestType == ChangeRequestType.Reschedule)
        {
            _logger.LogInformation(
                "ChangeRequestSubmittedEmailHandler: reschedule {ChangeRequestId} -- submit email suppressed; the consent email sent at date-confirm carries this notice.",
                eventData.ChangeRequestId);
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

            var changeRequest = await _changeRequestRepository.FindAsync(eventData.ChangeRequestId);
            if (changeRequest == null)
            {
                _logger.LogWarning(
                    "ChangeRequestSubmittedEmailHandler: change request {ChangeRequestId} not found; skipping.",
                    eventData.ChangeRequestId);
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

            // Only cancellations reach here (reschedules returned above), so the OLD-verbatim
            // cancel template is the only one this handler still dispatches.
            var templateCode = NotificationTemplateConsts.Codes.AppointmentCancelledRequest;

            var baseVariables = DocumentNotificationContext.BuildVariables(
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

            var variables = new Dictionary<string, object?>(baseVariables, StringComparer.Ordinal)
            {
                ["CancellationReason"] = changeRequest.CancellationReason ?? string.Empty,
            };

            await _dispatcher.DispatchAsync(
                templateCode: templateCode,
                recipients: recipients,
                variables: variables,
                contextTag: $"ChangeRequestSubmitted/{eventData.ChangeRequestType}/{eventData.ChangeRequestId}");
        }
    }

}
