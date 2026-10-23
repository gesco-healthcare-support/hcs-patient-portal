using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Issues activate-your-account links and sends the email that carries one. Every path that creates
/// an account for someone else, or that meets an unconfirmed account asking to get in, ends here, so
/// the token, the URL and the email cannot drift apart between them.
/// </summary>
public interface IAccountActivationSender
{
    /// <summary>
    /// A fresh activation link for <paramref name="user"/>, on the user's own office sign-in host
    /// (or the host one). Issue it LAST: any later change to the user's security stamp kills it.
    /// </summary>
    Task<string> CreateLinkAsync(IdentityUser user);

    /// <summary>
    /// Queues the <see cref="NotificationTemplateConsts.Codes.AccountActivation"/> email to the user,
    /// inside the user's own office scope (template lookup and the outbox row are per office).
    /// Throws when the email cannot be queued; callers decide whether that is fatal.
    /// </summary>
    Task SendAsync(IdentityUser user, string contextTag, string? greetingName = null);

    /// <summary>
    /// Sends the activation email to an office's administrator, the one account the office is
    /// created with: by New Practice, and again from the Practices screen. Never throws -- New
    /// Practice runs it after the office exists, so a failure is logged as an error for an operator,
    /// and the office admin can still ask for a link from the sign-in page. The outcome says what
    /// happened, for the screen that asked.
    /// </summary>
    Task<OfficeAdminActivationOutcome> TrySendToOfficeAdminAsync(Guid officeId, string? greetingName);
}

/// <summary>What <see cref="IAccountActivationSender.TrySendToOfficeAdminAsync"/> did.</summary>
public enum OfficeAdminActivationOutcome
{
    /// <summary>The activation email was queued.</summary>
    Sent,

    /// <summary>The administrator already activated; nothing was sent.</summary>
    AlreadyActive,

    /// <summary>The office has no administrator account with an email address; nothing was sent.</summary>
    NoAdministrator,

    /// <summary>The administrator is switched off, so its link would be dead; nothing was sent.</summary>
    Inactive,

    /// <summary>Queueing failed; the cause is logged as an error.</summary>
    Failed,
}

public class AccountActivationSender : IAccountActivationSender, ITransientDependency
{
    private readonly IdentityUserManager _userManager;
    private readonly IAccountUrlBuilder _accountUrlBuilder;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<AccountActivationSender> _logger;

    public AccountActivationSender(
        IdentityUserManager userManager,
        IAccountUrlBuilder accountUrlBuilder,
        INotificationDispatcher dispatcher,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<AccountActivationSender> logger)
    {
        _userManager = userManager;
        _accountUrlBuilder = accountUrlBuilder;
        _dispatcher = dispatcher;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    public virtual async Task<string> CreateLinkAsync(IdentityUser user)
    {
        Check.NotNull(user, nameof(user));
        var token = await _userManager.GenerateUserTokenAsync(
            user,
            _userManager.Options.Tokens.PasswordResetTokenProvider,
            AccountActivationTokens.Purpose);
        return await _accountUrlBuilder.BuildAccountActivationUrlForUserAsync(user.TenantId, user.Id, token);
    }

    public virtual async Task SendAsync(IdentityUser user, string contextTag, string? greetingName = null)
    {
        Check.NotNull(user, nameof(user));
        Check.NotNullOrWhiteSpace(contextTag, nameof(contextTag));
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new ArgumentException("An activation email needs an address; this account has none.", nameof(user));
        }

        using (_currentTenant.Change(user.TenantId))
        {
            var url = await CreateLinkAsync(user);
            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.AccountActivation,
                recipients: new[]
                {
                    new NotificationRecipient(
                        email: user.Email,
                        role: RecipientRole.Patient,
                        isRegistered: false),
                },
                variables: BuildVariables(user, url, greetingName),
                contextTag: contextTag);
        }
    }

    public virtual async Task<OfficeAdminActivationOutcome> TrySendToOfficeAdminAsync(Guid officeId, string? greetingName)
    {
        try
        {
            using (_currentTenant.Change(officeId))
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
            {
                var admin = await _userManager.FindByNameAsync(CaseEvaluationConsts.AdminUserName);
                if (admin == null || string.IsNullOrWhiteSpace(admin.Email))
                {
                    _logger.LogError(
                        "AccountActivationSender: office {OfficeId} has no administrator with an email; no activation email was sent.",
                        officeId);
                    return OfficeAdminActivationOutcome.NoAdministrator;
                }

                if (admin.EmailConfirmed)
                {
                    // A re-run against an office whose admin already activated. Nothing to send.
                    return OfficeAdminActivationOutcome.AlreadyActive;
                }

                if (!admin.IsActive)
                {
                    // Its link would be refused on arrival (AccountActivationLinkChecker).
                    return OfficeAdminActivationOutcome.Inactive;
                }

                await SendAsync(admin, NotificationOccurrence.PerRequest($"AccountActivation/OfficeAdmin/{admin.Id}"), greetingName);
                await uow.CompleteAsync();
                return OfficeAdminActivationOutcome.Sent;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "AccountActivationSender: the activation email for office {OfficeId}'s administrator could not be queued. They can ask for a new link with Resend verification on the office's sign-in page.",
                officeId);
            return OfficeAdminActivationOutcome.Failed;
        }
    }

    /// <summary>
    /// The template's tokens. The greeting prefers a name the caller knows (New Practice passes the
    /// doctor's first name) over the account's own: the seeded office admin is NAMED "admin", and
    /// "Hello admin," reads like a machine wrote it.
    /// </summary>
    internal static Dictionary<string, object?> BuildVariables(IdentityUser user, string url, string? greetingName)
    {
        var first = !string.IsNullOrWhiteSpace(greetingName)
            ? greetingName.Trim()
            : !string.IsNullOrWhiteSpace(user.Name)
              && !string.Equals(user.Name.Trim(), user.UserName, StringComparison.OrdinalIgnoreCase)
                ? user.Name.Trim()
                : "there";

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["PatientFirstName"] = first,
            ["PatientLastName"] = user.Surname ?? string.Empty,
            ["PatientFullName"] = string.IsNullOrWhiteSpace(user.Surname) ? first : $"{first} {user.Surname!.Trim()}",
            ["PatientEmail"] = user.Email ?? string.Empty,
            ["URL"] = url,
            ["ExpiresInHours"] = AccountActivationTokens.LifetimeHours.ToString(CultureInfo.InvariantCulture),
            ["CompanyLogo"] = string.Empty,
            ["lblHeaderTitle"] = string.Empty,
            ["lblFooterText"] = string.Empty,
            ["Email"] = string.Empty,
            ["Skype"] = string.Empty,
            ["ph_US"] = string.Empty,
            ["fax"] = string.Empty,
            ["imageInByte"] = string.Empty,
        };
    }
}
