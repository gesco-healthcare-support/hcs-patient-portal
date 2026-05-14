using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
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
/// Phase 17 (2026-05-04) -- subscribes to
/// <see cref="AppointmentChangeRequestApprovedEto"/> and dispatches
/// the OLD-parity stakeholder-notification email through Phase 18's
/// <see cref="INotificationDispatcher"/>. Branches on
/// <see cref="ChangeRequestType"/> to pick the right
/// <c>NotificationTemplateConsts.Codes.*</c>.
///
/// <para>Phase 3 (Category 3, 2026-05-10): when
/// <c>IsAdminOverride</c> is set, the handler renders the OLD
/// "Reschedule request has been changed by our team" wording via the
/// <c>##ApprovedSubjectQualifier##</c> + <c>##ApprovedHeadline##</c> +
/// <c>##ReasonBlock##</c> variables (Adrian Decision: single template,
/// variable-driven copy, no separate AdminReschedule template). The
/// admin uses <c>AdminOverrideSlotId</c> when they pick a different
/// slot from the user's request -- handler reads that slot's date/time
/// instead of the user-requested NewDoctorAvailabilityId.</para>
/// </summary>
public class ChangeRequestApprovedEmailHandler :
    ILocalEventHandler<AppointmentChangeRequestApprovedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<ChangeRequestApprovedEmailHandler> _logger;

    public ChangeRequestApprovedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        IRepository<AppointmentChangeRequest, Guid> changeRequestRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        ICurrentTenant currentTenant,
        ILogger<ChangeRequestApprovedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _changeRequestRepository = changeRequestRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
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

            var changeRequest = await _changeRequestRepository.FindAsync(eventData.ChangeRequestId);
            if (changeRequest == null)
            {
                _logger.LogWarning(
                    "ChangeRequestApprovedEmailHandler: change request {ChangeRequestId} not found; skipping.",
                    eventData.ChangeRequestId);
                return;
            }

            var resolverOutput = await _recipientResolver.ResolveAsync(
                eventData.AppointmentId,
                NotificationKind.Approved);

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

            var templateCode = eventData.ChangeRequestType switch
            {
                ChangeRequestType.Cancel => NotificationTemplateConsts.Codes.AppointmentCancelledRequestApproved,
                ChangeRequestType.Reschedule => NotificationTemplateConsts.Codes.AppointmentRescheduleRequestApproved,
                _ => NotificationTemplateConsts.Codes.AppointmentCancelledRequestApproved,
            };

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

            var variables = new Dictionary<string, object?>(baseVariables, StringComparer.Ordinal);

            if (eventData.ChangeRequestType == ChangeRequestType.Reschedule)
            {
                // OLD :721-722 uses newDoctorsAvailability for the rendered
                // date/time. Admin-override path uses AdminOverrideSlotId
                // (NEW field) when set; user-initiated approval uses
                // NewDoctorAvailabilityId.
                var slotId = eventData.IsAdminOverride && changeRequest.AdminOverrideSlotId.HasValue
                    ? changeRequest.AdminOverrideSlotId
                    : changeRequest.NewDoctorAvailabilityId;
                var (newDate, newTime) = await ResolveNewSlotAsync(slotId);
                variables["NewAppointmentDate"] = newDate;
                variables["NewAppointmentFromTime"] = newTime;

                // Adrian Decision 2026-05-10: single template; variables
                // carry the OLD-parity wording fork.
                if (eventData.IsAdminOverride)
                {
                    variables["ApprovedSubjectQualifier"] = "Reschedule request has been changed by our team";
                    variables["ApprovedHeadline"] = "Our clinic staff has changed your appointment to the date and time below.";
                    variables["ReasonBlock"] = string.IsNullOrWhiteSpace(changeRequest.AdminReScheduleReason)
                        ? string.Empty
                        : $"<strong>Reason for change:</strong> {System.Net.WebUtility.HtmlEncode(changeRequest.AdminReScheduleReason)}";
                }
                else
                {
                    variables["ApprovedSubjectQualifier"] = "Your reschedule request has been approved";
                    variables["ApprovedHeadline"] = $"Your reschedule request for appointment <b style=\"font-size:17px\">{System.Net.WebUtility.HtmlEncode(ctx.RequestConfirmationNumber ?? string.Empty)}</b> has been approved.";
                    variables["ReasonBlock"] = string.IsNullOrWhiteSpace(changeRequest.ReScheduleReason)
                        ? string.Empty
                        : $"<strong>Your reason:</strong> {System.Net.WebUtility.HtmlEncode(changeRequest.ReScheduleReason)}";
                }
            }

            await _dispatcher.DispatchAsync(
                templateCode: templateCode,
                recipients: recipients,
                variables: variables,
                contextTag: $"ChangeRequestApproved/{eventData.ChangeRequestType}/{(eventData.IsAdminOverride ? "AdminOverride" : "UserRequested")}/{eventData.ChangeRequestId}");
        }
    }

    private async Task<(string Date, string Time)> ResolveNewSlotAsync(Guid? slotId)
    {
        if (!slotId.HasValue || slotId.Value == Guid.Empty)
        {
            return (string.Empty, string.Empty);
        }
        var slot = await _doctorAvailabilityRepository.FindAsync(slotId.Value);
        if (slot == null)
        {
            return (string.Empty, string.Empty);
        }
        var date = slot.AvailableDate.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
        var time = new DateTime(2000, 1, 1, slot.FromTime.Hour, slot.FromTime.Minute, slot.FromTime.Second)
            .ToString("h:mm tt", CultureInfo.GetCultureInfo("en-US"));
        return (date, time);
    }
}
