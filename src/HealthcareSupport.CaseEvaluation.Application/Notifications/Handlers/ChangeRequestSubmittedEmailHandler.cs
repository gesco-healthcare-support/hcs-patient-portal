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
/// C4 / Phase 18 (2026-05-04) -- subscribes to
/// <see cref="AppointmentChangeRequestSubmittedEto"/> and dispatches the
/// "submit" stakeholder-notification email through Phase 18's
/// <see cref="INotificationDispatcher"/>. Branches on
/// <see cref="ChangeRequestType"/> to pick the OLD-verbatim template code.
///
/// <para>Phase 3 (Category 3, 2026-05-10): fetches the
/// <c>AppointmentChangeRequest</c> entity to surface the human-relevant
/// reschedule details (`NewAppointmentDate`, `NewAppointmentFromTime`,
/// `ReScheduleReason`) and the `CancellationReason` for cancel-submits.
/// Mirrors OLD <c>AppointmentChangeRequestDomain.cs</c>:756-767 -- OLD
/// fetched the new <c>DoctorsAvailability</c> for AvailableDate +
/// FromTime; NEW does the same via <c>NewDoctorAvailabilityId</c>.</para>
/// </summary>
public class ChangeRequestSubmittedEmailHandler :
    ILocalEventHandler<AppointmentChangeRequestSubmittedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<ChangeRequestSubmittedEmailHandler> _logger;

    public ChangeRequestSubmittedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        IRepository<AppointmentChangeRequest, Guid> changeRequestRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        ICurrentTenant currentTenant,
        ILogger<ChangeRequestSubmittedEmailHandler> logger)
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

            var templateCode = eventData.ChangeRequestType switch
            {
                ChangeRequestType.Cancel => NotificationTemplateConsts.Codes.AppointmentCancelledRequest,
                ChangeRequestType.Reschedule => NotificationTemplateConsts.Codes.AppointmentRescheduleRequest,
                _ => NotificationTemplateConsts.Codes.AppointmentCancelledRequest,
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
                var (newDate, newTime) = await ResolveNewSlotAsync(changeRequest.NewDoctorAvailabilityId);
                variables["NewAppointmentDate"] = newDate;
                variables["NewAppointmentFromTime"] = newTime;
                variables["ReScheduleReason"] = changeRequest.ReScheduleReason ?? string.Empty;
            }
            else
            {
                variables["CancellationReason"] = changeRequest.CancellationReason ?? string.Empty;
            }

            await _dispatcher.DispatchAsync(
                templateCode: templateCode,
                recipients: recipients,
                variables: variables,
                contextTag: $"ChangeRequestSubmitted/{eventData.ChangeRequestType}/{eventData.ChangeRequestId}");
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
        // TimeOnly -> 12h "h:mm tt" per OLD :699.
        var time = new DateTime(2000, 1, 1, slot.FromTime.Hour, slot.FromTime.Minute, slot.FromTime.Second)
            .ToString("h:mm tt", CultureInfo.GetCultureInfo("en-US"));
        return (date, time);
    }
}
