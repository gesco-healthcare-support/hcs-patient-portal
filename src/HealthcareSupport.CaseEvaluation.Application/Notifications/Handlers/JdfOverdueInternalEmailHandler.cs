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
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Tells INTERNAL STAFF that an AME appointment passed its Joint Declaration Form deadline
/// (2026-08-08).
///
/// <para>REPLACES <c>JdfAutoCancelledEmailHandler</c>, which mailed every stakeholder -- patient,
/// attorneys, examiner -- to say the appointment had been cancelled. Nothing is cancelled now, so
/// there is nothing to announce to the parties: what exists is a decision somebody has to make.
/// Adrian, 2026-08-08: "auto-cancel without staff or anyone's involvement seems like a risky thing
/// and we should not do that."</para>
///
/// <para>Recipients are the same internal tier the no-show notice uses -- Staff Supervisor and
/// Intake Staff -- resolved through <see cref="IdentityUserManager"/> and deduped on email so a user
/// holding both roles gets one message. Deliberately NOT the stakeholder walker: sending "your form
/// is late" to a patient's defense attorney would leak an internal workflow problem outward.</para>
///
/// <para>Fires once per appointment. The job only raises this event on the run that FIRST detects
/// the overdue state, so a document that stays missing does not generate a daily email.</para>
/// </summary>
public class JdfOverdueInternalEmailHandler :
    ILocalEventHandler<AppointmentJointDeclarationOverdueEto>,
    ITransientDependency
{
    /// <summary>
    /// Same allow-list as the no-show notice in <c>StatusChangeEmailHandler</c>, kept duplicated
    /// here so each handler stays self-contained -- that file states the same rationale.
    /// </summary>
    private static readonly string[] InternalRoles =
    {
        "Staff Supervisor",
        "Intake Staff",
    };

    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<JdfOverdueInternalEmailHandler> _logger;

    public JdfOverdueInternalEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IdentityUserManager userManager,
        ICurrentTenant currentTenant,
        ILogger<JdfOverdueInternalEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _userManager = userManager;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentJointDeclarationOverdueEto eventData)
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
                    "JdfOverdueInternalEmailHandler: no context for appointment {AppointmentId}; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var recipients = await ResolveInternalRecipientsAsync(eventData.AppointmentId);
            if (recipients.Count == 0)
            {
                // Worth a warning rather than a debug: the appointment is now flagged in the UI but
                // nobody was told, so the flag is the only signal that exists.
                _logger.LogWarning(
                    "JdfOverdueInternalEmailHandler: no internal recipients for appointment {AppointmentId}; the overdue flag is the only notice.",
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

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.AppointmentJointDeclarationOverdueInternal,
                recipients: recipients,
                variables: variables,
                contextTag: $"JdfOverdue/{eventData.AppointmentId}");
        }
    }

    /// <summary>
    /// Every user in the internal tier, deduped on email. Mirrors
    /// <c>StatusChangeEmailHandler.ResolveNoShowInternalRecipientsAsync</c>.
    /// </summary>
    private async Task<List<NotificationRecipient>> ResolveInternalRecipientsAsync(Guid appointmentId)
    {
        var byEmail = new Dictionary<string, NotificationRecipient>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleName in InternalRoles)
        {
            var users = await _userManager.GetUsersInRoleAsync(roleName);
            foreach (var user in users)
            {
                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    _logger.LogDebug(
                        "JdfOverdueInternalEmailHandler: skipping {Role} user {UserId} -- empty email; appointment {AppointmentId}.",
                        roleName, user.Id, appointmentId);
                    continue;
                }

                byEmail[user.Email] = new NotificationRecipient(
                    email: user.Email,
                    role: RecipientRole.OfficeAdmin,
                    isRegistered: true);
            }
        }

        return byEmail.Values.ToList();
    }
}
