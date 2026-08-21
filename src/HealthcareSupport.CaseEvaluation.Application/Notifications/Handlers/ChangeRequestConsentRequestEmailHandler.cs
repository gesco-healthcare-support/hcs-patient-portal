using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Group D (2026-06-09) -- subscribes to <see cref="ChangeRequestConsentRequestedEto"/>
/// and sends the ONE actionable consent email to the opposing side's single
/// representative. The body links to the public consent landing page (the Yes/No is
/// recorded there). Confirmation-to-all-parties is handled separately by the existing
/// <c>ChangeRequestSubmittedEmailHandler</c>. Includes the requested new date/time
/// (reschedule) + the reason so the recipient can decide.
/// </summary>
public class ChangeRequestConsentRequestEmailHandler :
    ILocalEventHandler<ChangeRequestConsentRequestedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<DoctorAvailability, Guid> _slotRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<ChangeRequestConsentRequestEmailHandler> _logger;

    public ChangeRequestConsentRequestEmailHandler(
        INotificationDispatcher dispatcher,
        IRepository<AppointmentChangeRequest, Guid> changeRequestRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DoctorAvailability, Guid> slotRepository,
        ICurrentTenant currentTenant,
        ILogger<ChangeRequestConsentRequestEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _changeRequestRepository = changeRequestRepository;
        _appointmentRepository = appointmentRepository;
        _slotRepository = slotRepository;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(ChangeRequestConsentRequestedEto eventData)
    {
        if (eventData == null || string.IsNullOrWhiteSpace(eventData.OpposingRecipientEmail))
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var changeRequest = await _changeRequestRepository.FindAsync(eventData.ChangeRequestId);
            if (changeRequest == null)
            {
                _logger.LogWarning(
                    "ChangeRequestConsentRequestEmailHandler: change request {ChangeRequestId} not found; skipping.",
                    eventData.ChangeRequestId);
                return;
            }

            var appointment = await _appointmentRepository.FindAsync(eventData.AppointmentId);
            var confirmationNumber = appointment?.RequestConfirmationNumber ?? string.Empty;

            var isReschedule = eventData.ChangeRequestType == ChangeRequestType.Reschedule;
            var actionLabel = isReschedule ? "reschedule" : "cancel";
            var reason = isReschedule ? changeRequest.ReScheduleReason : changeRequest.CancellationReason;

            // Phase 4c (2026-08-05): the date a reschedule asks consent FOR is the round's
            // proposed slot, carried on the event. Reading NewDoctorAvailabilityId alone was a
            // latent blank-date bug: 4b moved the staff slot off that column, leaving it null on
            // the external path, so BuildDetailsBlock would have omitted the date line entirely
            // and asked a party to approve a reschedule with no date shown anywhere. Inert until
            // now only because 4b had suppressed reschedule consent. The fallback keeps
            // pre-4c rows -- and internal staff-filed requests that did propose a slot -- working.
            string? newDateTime = null;
            var slotId = eventData.ProposedDoctorAvailabilityId ?? changeRequest.NewDoctorAvailabilityId;
            if (isReschedule && slotId.HasValue)
            {
                var slot = await _slotRepository.FindAsync(slotId.Value);
                if (slot != null)
                {
                    newDateTime = FormatSlot(slot.AvailableDate, slot.FromTime);
                }
            }

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["AppointmentRequestConfirmationNumber"] = confirmationNumber,
                ["ChangeActionLabel"] = actionLabel,
                ["ConsentDetailsBlock"] = BuildDetailsBlock(newDateTime, reason),
                ["ConsentUrl"] = eventData.ConsentUrl,
            };

            var recipients = new[]
            {
                new NotificationRecipient(
                    email: eventData.OpposingRecipientEmail,
                    role: eventData.OpposingRecipientRole,
                    isRegistered: false),
            };

            try
            {
                await _dispatcher.DispatchAsync(
                    templateCode: NotificationTemplateConsts.Codes.ChangeRequestConsentRequest,
                    recipients: recipients,
                    variables: variables,
                    contextTag: BuildContextTag(eventData));
            }
            catch (BusinessException ex)
                when (ex.Code == CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
            {
                _logger.LogWarning(
                    "ChangeRequestConsentRequestEmailHandler: consent template missing/inactive; email skipped for change request {ChangeRequestId}.",
                    eventData.ChangeRequestId);
            }
        }
    }

    /// <summary>
    /// Phase 4c (2026-08-05) -- the outbox idempotency key is
    /// <c>SHA256(tenantId | recipientEmail | contextTag | packetKind)</c> and
    /// <c>NotificationOutboxManager.EnqueueAsync</c> SILENTLY RETURNS THE EXISTING ROW on a
    /// match: no throw, no log. With the pre-4c tag of <c>ChangeRequestConsent/{id}</c> a second
    /// round's email to the same recipient -- and every resend -- would therefore vanish without
    /// a trace. Round + attempt make each dispatch its own key.
    ///
    /// <para>CANCELLATION consent keeps the original tag verbatim (its <c>RoundNumber</c> is 0):
    /// it has no rounds, is only ever sent once per request, and phase 4c deliberately left the
    /// cancel path untouched.</para>
    /// </summary>
    internal static string BuildContextTag(ChangeRequestConsentRequestedEto eventData) =>
        eventData.RoundNumber > 0
            ? $"ChangeRequestConsent/{eventData.ChangeRequestId}/r{eventData.RoundNumber}/a{eventData.SendAttempt}"
            : $"ChangeRequestConsent/{eventData.ChangeRequestId}";

    private static string FormatSlot(DateTime availableDate, TimeOnly fromTime)
    {
        var date = availableDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        var time = new DateTime(2000, 1, 1, fromTime.Hour, fromTime.Minute, fromTime.Second)
            .ToString("h:mm tt", CultureInfo.GetCultureInfo("en-US"));
        return $"{date} at {time}";
    }

    private static string BuildDetailsBlock(string? newDateTime, string? reason)
    {
        var sb = new System.Text.StringBuilder("<p>");
        if (!string.IsNullOrWhiteSpace(newDateTime))
        {
            sb.Append("<strong>Requested new date &amp; time:</strong> ")
              .Append(WebUtility.HtmlEncode(newDateTime))
              .Append("<br />");
        }
        sb.Append("<strong>Reason:</strong> ")
          .Append(WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(reason) ? "(not provided)" : reason));
        sb.Append("</p>");
        return sb.ToString();
    }
}
