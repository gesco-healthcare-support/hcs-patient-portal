using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
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
/// Routes the Submit-Query / Contact-Us email when a
/// <see cref="UserQuerySubmittedEto"/> is published. Port of OLD
/// <c>UserQueryDomain.Add</c> (PatientAppointment.Domain\UserQueryModule):
///
/// <list type="bullet">
///   <item>When the submitter supplied a confirmation number that resolves
///     to an <b>Approved</b> appointment, email that appointment's
///     <c>PrimaryResponsibleUserId</c> (the owning internal staffer) with a
///     patient/claim/ADJ subject.</item>
///   <item>Otherwise (no confirmation number, no matching Approved
///     appointment, OR the responsible user has no resolvable email) fan out
///     to every <c>IT Admin</c> user.</item>
/// </list>
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
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<UserQuerySubmittedEmailHandler> _logger;

    public UserQuerySubmittedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IRepository<Appointment, Guid> appointmentRepository,
        IdentityUserManager userManager,
        ICurrentTenant currentTenant,
        ILogger<UserQuerySubmittedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _appointmentRepository = appointmentRepository;
        _userManager = userManager;
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
                    responsibleEmail = await _contextResolver.ResolveResponsibleUserEmailAsync(
                        ctx?.ResponsibleUserId);
                }
            }

            List<NotificationRecipient> recipients;
            if (!string.IsNullOrWhiteSpace(responsibleEmail))
            {
                recipients = new List<NotificationRecipient>
                {
                    new(email: responsibleEmail!, role: RecipientRole.OfficeAdmin, isRegistered: true),
                };
            }
            else
            {
                // OLD branch-2 fan-out + B-09-02 fix: broadcast to all
                // IT-Admins when no Approved appointment matched OR the
                // responsible user had no resolvable email (OLD NRE'd here).
                recipients = await ResolveItAdminRecipientsAsync();
            }

            if (recipients.Count == 0)
            {
                _logger.LogWarning(
                    "UserQuerySubmittedEmailHandler: no recipients for query {UserQueryId} (no IT-Admin users in tenant); email skipped.",
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
                    "UserQuerySubmittedEmailHandler: UserQuery template missing/inactive; email skipped for query {UserQueryId}.",
                    eventData.UserQueryId);
            }
        }
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
            var users = await _userManager.GetUsersInRoleAsync(
                InternalUserRoleDataSeedContributor.ItAdminRoleName);
            foreach (var user in users)
            {
                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    continue;
                }
                byEmail[user.Email] = new NotificationRecipient(
                    email: user.Email, role: RecipientRole.OfficeAdmin, isRegistered: true);
            }
        }

        return byEmail.Values.ToList();
    }
}
