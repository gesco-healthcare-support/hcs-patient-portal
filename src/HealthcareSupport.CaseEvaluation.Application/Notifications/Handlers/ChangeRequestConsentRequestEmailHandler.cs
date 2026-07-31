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

            string? newDateTime = null;
            if (isReschedule && changeRequest.NewDoctorAvailabilityId.HasValue)
            {
                var slot = await _slotRepository.FindAsync(changeRequest.NewDoctorAvailabilityId.Value);
                if (slot != null)
                {
                    var date = slot.AvailableDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
                    var time = new DateTime(2000, 1, 1, slot.FromTime.Hour, slot.FromTime.Minute, slot.FromTime.Second)
                        .ToString("h:mm tt", CultureInfo.GetCultureInfo("en-US"));
                    newDateTime = $"{date} at {time}";
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
                    contextTag: $"ChangeRequestConsent/{eventData.ChangeRequestId}");
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
