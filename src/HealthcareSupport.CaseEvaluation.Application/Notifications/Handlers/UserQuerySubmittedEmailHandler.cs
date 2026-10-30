using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Routes the Submit-Query / Contact-Us email when a
/// <see cref="UserQuerySubmittedEto"/> is published. Port of OLD
/// <c>UserQueryDomain.Add</c> (PatientAppointment.Domain\UserQueryModule):
///
/// <list type="bullet">
///   <item>When the submitter supplied a confirmation number that resolves
///     to an <b>Approved</b> appointment, email that appointment's
///     <c>PrimaryResponsibleUserId</c> (the owning internal staffer) with a
///     patient/claim/ADJ subject. When that account is switched off
///     (2026-10-26) the office's notification inbox gets it instead, if the
///     office has one (<see cref="ResponsibleUserRecipientResolver"/>).</item>
///   <item>Otherwise (no confirmation number, no matching Approved
///     appointment, OR no responsible address resolved) the office's
///     notification inbox gets it (2026-10-29). Only an office with no inbox
///     falls back to every <c>IT Admin</c> user.</item>
/// </list>
///
/// <para>Why the inbox comes before the IT Admins: the IT Admin pool is host
/// staff, and on the live server it is empty, so a patient's or attorney's
/// Contact-Us question went to nobody and only a warning reached the log. A
/// question asked inside an office is that office's to answer.</para>
///
/// <para>OLD bug B-09-02 dereferenced the responsible-user email after a
/// <c>FirstOrDefault()</c> with no null guard -- an NRE after the row was
/// already committed. NEW guards the lookup and falls back to the IT-Admin
/// pool instead of throwing (root CLAUDE.md "clear bug -- fix it").</para>
/// </summary>
public class UserQuerySubmittedEmailHandler :
    ILocalEventHandler<UserQuerySubmittedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly ResponsibleUserRecipientResolver _responsibleUsers;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ISettingProvider _settingProvider;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<UserQuerySubmittedEmailHandler> _logger;

    public UserQuerySubmittedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        ResponsibleUserRecipientResolver responsibleUsers,
        IRepository<Appointment, Guid> appointmentRepository,
        IdentityUserManager userManager,
        ISettingProvider settingProvider,
        ICurrentTenant currentTenant,
        ILogger<UserQuerySubmittedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _responsibleUsers = responsibleUsers;
        _appointmentRepository = appointmentRepository;
        _userManager = userManager;
        _settingProvider = settingProvider;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(UserQuerySubmittedEto eventData)
    {
        if (eventData == null || string.IsNullOrWhiteSpace(eventData.Message))
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            // Resolve the optional appointment context (OLD :62 -- match on
            // confirmation number AND Approved status). Empty when no number
            // was supplied or it does not resolve to an Approved appointment.
            var subjectIdentity = string.Empty;
            string? responsibleEmail = null;

            var confirmationNumber = eventData.RequestConfirmationNumber?.Trim();
            if (!string.IsNullOrWhiteSpace(confirmationNumber))
            {
                var appointment = await _appointmentRepository.FirstOrDefaultAsync(
                    a => a.RequestConfirmationNumber == confirmationNumber
                        && a.AppointmentStatus == AppointmentStatusType.Approved);
                if (appointment != null)
                {
                    var ctx = await _contextResolver.ResolveAsync(appointment.Id, appointmentDocumentId: null);
                    subjectIdentity = EmailSubjectBuilder.BuildIdentitySuffix(
                        ctx?.PatientFirstName, ctx?.PatientLastName, ctx?.ClaimNumber, ctx?.WcabAdj);
                    responsibleEmail = (await _responsibleUsers.ResolveAsync(ctx?.ResponsibleUserId))?.Email;
                }
            }

            List<NotificationRecipient> recipients;
            if (!string.IsNullOrWhiteSpace(responsibleEmail))
            {
                recipients = new List<NotificationRecipient>
                {
                    new(email: responsibleEmail, role: RecipientRole.OfficeAdmin, isRegistered: true),
                };
            }
            else
            {
                // No responsible address: the office's inbox, read in the office's own scope. OLD's
                // branch-2 fan-out to every IT Admin (+ the B-09-02 null guard) is now only the
                // fallback for an office that has no inbox.
                recipients = await ResolveOfficeInboxRecipientsAsync();
                if (recipients.Count == 0)
                {
                    recipients = await ResolveItAdminRecipientsAsync();
                }
            }

            if (recipients.Count == 0)
            {
                _logger.LogWarning(
                    "UserQuerySubmittedEmailHandler: no recipients for query {UserQueryId} (no office inbox and no IT-Admin users); email skipped.",
                    eventData.UserQueryId);
                return;
            }

            var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["UserQueryMessage"] = eventData.Message,
                // Subject suffix carries its own " - " separator so an empty
                // identity (no matching Approved appointment) leaves no
                // dangling separator in the subject line.
                ["UserQuerySubjectIdentity"] = subjectIdentity.Length == 0
                    ? string.Empty
                    : $" - {subjectIdentity}",
            };

            try
            {
                await _dispatcher.DispatchAsync(
                    templateCode: NotificationTemplateConsts.Codes.UserQuery,
                    recipients: recipients,
                    variables: variables,
                    contextTag: $"UserQuery/Submitted/{eventData.UserQueryId}");
            }
            catch (BusinessException ex)
                when (ex.Code == CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
            {
                // The email side effect must not surface back to the submit
                // request (the query row already committed). Log + swallow,
                // matching StatusChangeEmailHandler.
                _logger.LogWarning(
                    ex,
                    "UserQuerySubmittedEmailHandler: UserQuery template missing/inactive; email skipped for query {UserQueryId}.",
                    eventData.UserQueryId);
            }
        }
    }

    /// <summary>
    /// The office's notification inbox as a single recipient, or none when the office has not set one.
    /// Runs inside the caller's office scope, so it reads that office's setting and never another's.
    /// </summary>
    private async Task<List<NotificationRecipient>> ResolveOfficeInboxRecipientsAsync()
    {
        var officeInbox = (await _settingProvider.GetOrNullAsync(
            CaseEvaluationSettings.NotificationsPolicy.OfficeEmail))?.Trim();

        return string.IsNullOrWhiteSpace(officeInbox)
            ? new List<NotificationRecipient>()
            : new List<NotificationRecipient>
            {
                new(email: officeInbox, role: RecipientRole.OfficeAdmin, isRegistered: true),
            };
    }

    /// <summary>
    /// Walks every <c>IT Admin</c> user and packages them into recipient
    /// rows, deduped by email. Mirrors OLD's <c>RoleId == Roles.ItAdmin</c>
    /// fan-out; empty set logs + no-ops (matches the NoShow handler).
    /// </summary>
    private async Task<List<NotificationRecipient>> ResolveItAdminRecipientsAsync()
    {
        var byEmail = new Dictionary<string, NotificationRecipient>(StringComparer.OrdinalIgnoreCase);

        // "IT Admin" is a HOST-scoped role (seeded with TenantId = null). This
        // handler runs inside _currentTenant.Change(eventData.TenantId), where
        // the IMultiTenant filter on IdentityUser would exclude every
        // host-level IT-Admin and silently drop the email. Resolve the pool in
        // host scope so the fallback recipients are actually found.
        using (_currentTenant.Change(null))
        {
            var users = await _userManager.GetActiveUsersInRoleAsync(
                InternalUserRoleDataSeedContributor.ItAdminRoleName);
            foreach (var email in users.Select(user => user.Email).Where(email => !string.IsNullOrWhiteSpace(email)))
            {
                byEmail[email] = new NotificationRecipient(
                    email: email, role: RecipientRole.OfficeAdmin, isRegistered: true);
            }
        }

        return byEmail.Values.ToList();
    }
}
