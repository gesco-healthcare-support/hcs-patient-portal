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
using Volo.Abp.Identity;
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
/// <para>Phase 4c (2026-08-05) suppressed the reschedule email: its date variables rendered
/// empty because the external path leaves <c>NewDoctorAvailabilityId</c> null. 2026-11-03 (email
/// routing, row 7) sends it again, to the parties, the office inbox and the Intake Staff (the patient
/// only when the patient asked), with the neutral <c>AppointmentRescheduleRequest</c> wording and a
/// details block that simply omits the date line when no date was proposed.</para>
/// </summary>
public class ChangeRequestSubmittedEmailHandler :
    ILocalEventHandler<AppointmentChangeRequestSubmittedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IAppointmentRecipientResolver _recipientResolver;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;
    private readonly OfficeNotificationRecipients _officeRecipients;
    private readonly ConsentLinkHolders _consentLinkHolders;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IRepository<DoctorAvailability, Guid> _slotRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<ChangeRequestSubmittedEmailHandler> _logger;

    public ChangeRequestSubmittedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IAppointmentRecipientResolver recipientResolver,
        IRepository<AppointmentChangeRequest, Guid> changeRequestRepository,
        OfficeNotificationRecipients officeRecipients,
        ConsentLinkHolders consentLinkHolders,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<DoctorAvailability, Guid> slotRepository,
        ICurrentTenant currentTenant,
        ILogger<ChangeRequestSubmittedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _recipientResolver = recipientResolver;
        _changeRequestRepository = changeRequestRepository;
        _officeRecipients = officeRecipients;
        _consentLinkHolders = consentLinkHolders;
        _identityUserRepository = identityUserRepository;
        _slotRepository = slotRepository;
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

            // 2026-11-02: the office's Intake Staff are copied on the cancel request.
            await _officeRecipients.AppendIntakeAsync(recipients);

            var isReschedule = eventData.ChangeRequestType == ChangeRequestType.Reschedule;
            if (isReschedule)
            {
                // Row 7: a reschedule request asks nobody to agree yet (consent opens when staff set a
                // date), so nobody holds a link. The patient hears of it only when the patient asked.
                recipients = PatientBeforeApprovalRule.ApplyForRequester(
                    recipients, ctx, await IsPatientRequesterAsync(ctx, eventData.SubmittedByUserId));
            }
            else
            {
                // 2026-11-03: one email per person per event. Whoever is asked to agree or decline holds a
                // consent link, and that consent email is their email for this cancel request, so they do
                // not also get this information notice. The office inbox and Intake Staff (who hold no
                // link) get this one only: the separate clinical-staff cancellation email was retired.
                var linkHolders = await _consentLinkHolders.ResolveEmailsAsync(
                    eventData.AppointmentId, eventData.SubmittedByUserId);
                if (linkHolders.Count > 0)
                {
                    recipients = recipients
                        .Where(r => !linkHolders.Contains(r.Email, StringComparer.OrdinalIgnoreCase))
                        .ToList();
                }
            }

            if (recipients.Count == 0)
            {
                _logger.LogInformation(
                    "ChangeRequestSubmittedEmailHandler: no recipients resolved for appointment {AppointmentId}; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var templateCode = isReschedule
                ? NotificationTemplateConsts.Codes.AppointmentRescheduleRequest
                : NotificationTemplateConsts.Codes.AppointmentCancelledRequest;

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
                // Filled by NotificationTemplateRenderer from the tenant store (#1014):
                // ICurrentTenant.Name is null inside Change(TenantId).
                clinicName: null,
                portalUrl: ctx.PortalBaseUrl);

            var variables = new Dictionary<string, object?>(baseVariables, StringComparer.Ordinal);
            if (isReschedule)
            {
                string? requestedDateTime = null;
                if (changeRequest.NewDoctorAvailabilityId is { } slotId)
                {
                    var slot = await _slotRepository.FindAsync(slotId);
                    requestedDateTime = slot == null ? null : ConsentSlotLabel.Format(slot);
                }
                variables["ConsentDetailsBlock"] = ChangeRequestConsentRequestEmailHandler.BuildDetailsBlock(
                    requestedDateTime, changeRequest.ReScheduleReason);
            }
            else
            {
                variables["CancellationReason"] = changeRequest.CancellationReason ?? string.Empty;
            }

            try
            {
                await _dispatcher.DispatchAsync(
                    templateCode: templateCode,
                    recipients: recipients,
                    variables: variables,
                    contextTag: $"ChangeRequestSubmitted/{eventData.ChangeRequestType}/{eventData.ChangeRequestId}");
            }
            catch (BusinessException ex)
                when (ex.Code == CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
            {
                // This handler runs inside the submit's unit of work, so a throw here would roll back the
                // party's request. The renderer raises this one code for a missing template AND for one
                // staff switched off on the template screen, which is a runtime state, not only a seed
                // bug. So: this exception type only, logged at Error so it is seen.
                _logger.LogError(
                    ex,
                    "ChangeRequestSubmittedEmailHandler: template {TemplateCode} missing/inactive; notice skipped for change request {ChangeRequestId} on appointment {AppointmentId}.",
                    templateCode,
                    eventData.ChangeRequestId,
                    eventData.AppointmentId);
            }
        }
    }

    /// <summary>Whether the account that filed the change request is the patient.</summary>
    private async Task<bool> IsPatientRequesterAsync(DocumentEmailContext ctx, Guid submittedByUserId)
    {
        if (submittedByUserId == Guid.Empty)
        {
            return false;
        }

        var submitter = await _identityUserRepository.FindAsync(submittedByUserId);
        return PatientBeforeApprovalRule.IsPatientAccount(
            ctx, submittedByUserId, submitter is { EmailConfirmed: true } ? submitter.Email : null);
    }

}
