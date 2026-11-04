using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
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
/// 2026-11-03 (email routing, row 8) -- the no-link "a change is waiting for agreement" notice. When staff
/// open a reschedule consent round, the sides' representatives get their consent LINK
/// (<see cref="ChangeRequestConsentRequestEmailHandler"/>); this handler tells everyone who was NOT asked:
/// the claim examiner (when not a link holder), the office inbox and the Intake Staff. The patient is never
/// sent it: the patient either holds a link (as a side's representative, and that mail is their one email)
/// or hears only the confirmed date when the change finishes.
///
/// <para>One email per person per event: a link holder is removed from the recipients, and the list is
/// de-duplicated by address, so a claim examiner who is also the office inbox gets one.</para>
/// </summary>
public class ChangeRequestConsentInfoEmailHandler :
    ILocalEventHandler<ChangeRequestConsentOpenedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;
    private readonly IRepository<DoctorAvailability, Guid> _slotRepository;
    private readonly OfficeNotificationRecipients _officeRecipients;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<ChangeRequestConsentInfoEmailHandler> _logger;

    public ChangeRequestConsentInfoEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        IRepository<AppointmentChangeRequest, Guid> changeRequestRepository,
        IRepository<DoctorAvailability, Guid> slotRepository,
        OfficeNotificationRecipients officeRecipients,
        ICurrentTenant currentTenant,
        ILogger<ChangeRequestConsentInfoEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _changeRequestRepository = changeRequestRepository;
        _slotRepository = slotRepository;
        _officeRecipients = officeRecipients;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(ChangeRequestConsentOpenedEto eventData)
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
                    "ChangeRequestConsentInfoEmailHandler: appointment {AppointmentId} or change request {ChangeRequestId} not found; skipping.",
                    eventData.AppointmentId, eventData.ChangeRequestId);
                return;
            }

            // The claim examiner is the only party who can be left out of the consent links: an attorney is
            // always a side's representative, and a side with no attorney is represented by the patient
            // (applicant side) or the claim examiner (defense side).
            var parties = await _recipientResolver.ResolveAsync(eventData.AppointmentId, NotificationKind.Submitted);
            var recipients = parties
                .Where(r => r.Role == RecipientRole.ClaimExaminer && !string.IsNullOrWhiteSpace(r.To))
                .Select(r => new NotificationRecipient(email: r.To, role: r.Role, isRegistered: r.IsRegistered))
                .ToList();
            recipients.AddRange(await _officeRecipients.GetOfficeInboxAndIntakeAsync(
                $"ConsentInfo/{eventData.ChangeRequestId}"));

            // 2026-11-04: the case followers (external booker, accessors) are told too, except the
            // patient (a patient address or a Patient-role account); link holders are removed below.
            recipients = CaseFollowers.AppendWithoutPatient(recipients, ctx);

            var holders = new HashSet<string>(eventData.ConsentLinkEmails ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            recipients = OfficeNotificationRecipients.Distinct(recipients)
                .Where(r => !holders.Contains(r.Email.Trim()))
                .ToList();
            if (recipients.Count == 0)
            {
                _logger.LogInformation(
                    "ChangeRequestConsentInfoEmailHandler: nobody to inform for change request {ChangeRequestId}; skipping.",
                    eventData.ChangeRequestId);
                return;
            }

            string? newDateTime = null;
            var slotId = eventData.ProposedDoctorAvailabilityId ?? changeRequest.NewDoctorAvailabilityId;
            if (slotId.HasValue)
            {
                var slot = await _slotRepository.FindAsync(slotId.Value);
                newDateTime = slot == null ? null : ConsentSlotLabel.Format(slot);
            }

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["AppointmentRequestConfirmationNumber"] = ctx.RequestConfirmationNumber,
                ["ChangeActionLabel"] = "reschedule",
                ["ConsentDetailsBlock"] = ChangeRequestConsentRequestEmailHandler.BuildDetailsBlock(
                    newDateTime, changeRequest.ReScheduleReason),
            };

            try
            {
                await _dispatcher.DispatchAsync(
                    templateCode: NotificationTemplateConsts.Codes.ChangeRequestConsentInfo,
                    recipients: recipients,
                    variables: variables,
                    contextTag: $"ChangeRequestConsentInfo/{eventData.ChangeRequestId}/r{eventData.RoundNumber}");
            }
            catch (BusinessException ex)
                when (ex.Code == CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
            {
                _logger.LogWarning(
                    ex,
                    "ChangeRequestConsentInfoEmailHandler: template missing/inactive; notice skipped for change request {ChangeRequestId}.",
                    eventData.ChangeRequestId);
            }
        }
    }
}
