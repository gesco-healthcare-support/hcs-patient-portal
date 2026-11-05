using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
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
/// 2026-11-03 (email routing, row 9d) -- a side DECLINED a change request: the office inbox and the Intake
/// Staff are told at once. Nobody else: the parties are told the outcome through the existing turned-down
/// email when staff close the request. Raised by <c>ChangeRequestConsentManager</c>, so it covers the public
/// consent link and the in-portal Decline alike.
/// </summary>
public class ChangeRequestConsentDeclinedEmailHandler :
    ILocalEventHandler<ChangeRequestConsentDeclinedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;
    private readonly IRepository<DoctorAvailability, Guid> _slotRepository;
    private readonly IChangeRequestConsentRoundRepository _roundRepository;
    private readonly OfficeNotificationRecipients _officeRecipients;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<ChangeRequestConsentDeclinedEmailHandler> _logger;

    public ChangeRequestConsentDeclinedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IRepository<AppointmentChangeRequest, Guid> changeRequestRepository,
        IRepository<DoctorAvailability, Guid> slotRepository,
        IChangeRequestConsentRoundRepository roundRepository,
        OfficeNotificationRecipients officeRecipients,
        ICurrentTenant currentTenant,
        ILogger<ChangeRequestConsentDeclinedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _changeRequestRepository = changeRequestRepository;
        _slotRepository = slotRepository;
        _roundRepository = roundRepository;
        _officeRecipients = officeRecipients;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(ChangeRequestConsentDeclinedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, appointmentDocumentId: null);
            var changeRequest = await _changeRequestRepository.FindAsync(eventData.ChangeRequestId);
            if (ctx == null || changeRequest == null)
            {
                _logger.LogWarning(
                    "ChangeRequestConsentDeclinedEmailHandler: appointment {AppointmentId} or change request {ChangeRequestId} not found; skipping.",
                    eventData.AppointmentId, eventData.ChangeRequestId);
                return;
            }

            var recipients = await _officeRecipients.GetOfficeInboxAndIntakeAsync(
                $"ConsentDeclined/{eventData.ChangeRequestId}");
            if (recipients.Count == 0)
            {
                return;
            }

            var isReschedule = eventData.ChangeRequestType == ChangeRequestType.Reschedule;
            string? newDateTime = null;
            if (isReschedule)
            {
                var round = await _roundRepository.GetCurrentAsync(eventData.ChangeRequestId);
                var slotId = round?.ProposedDoctorAvailabilityId ?? changeRequest.NewDoctorAvailabilityId;
                if (slotId.HasValue)
                {
                    var slot = await _slotRepository.FindAsync(slotId.Value);
                    newDateTime = slot == null ? null : ConsentSlotLabel.Format(slot);
                }
            }

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["AppointmentRequestConfirmationNumber"] = ctx.RequestConfirmationNumber,
                ["ChangeActionLabel"] = isReschedule ? "reschedule" : "cancel",
                ["PatientFirstName"] = ctx.PatientFirstName ?? string.Empty,
                ["PatientLastName"] = ctx.PatientLastName ?? string.Empty,
                ["ConsentDetailsBlock"] = ChangeRequestConsentRequestEmailHandler.BuildDetailsBlock(
                    newDateTime,
                    isReschedule ? changeRequest.ReScheduleReason : changeRequest.CancellationReason),
            };

            try
            {
                await _dispatcher.DispatchAsync(
                    notificationCase: NotificationCase.Of(eventData.AppointmentId),
                    templateCode: NotificationTemplateConsts.Codes.ChangeRequestConsentDeclined,
                    recipients: recipients,
                    variables: variables,
                    contextTag: $"ChangeRequestConsentDeclined/{eventData.ChangeRequestId}/r{eventData.RoundNumber}/{eventData.Side}");
            }
            catch (BusinessException ex)
                when (ex.Code == CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
            {
                _logger.LogWarning(
                    ex,
                    "ChangeRequestConsentDeclinedEmailHandler: template missing/inactive; notice skipped for change request {ChangeRequestId}.",
                    eventData.ChangeRequestId);
            }
        }
    }
}
