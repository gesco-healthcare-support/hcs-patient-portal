using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Subscribes to <see cref="UserRegisteredEto"/> and dispatches the
/// OLD-parity registration-verification email. Mirrors OLD's
/// <c>UserDomain.SendEmail(user, isNewUser: true)</c> at
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\UserModule\UserDomain.cs</c>:314-333,
/// which fires a single email to the freshly-registered user with a
/// link they click to verify their address.
///
/// <para>NEW deviates from OLD's verify mechanism: OLD wrote a
/// <c>VerificationCode Guid</c> column on its <c>User</c> entity and
/// linked to <c>/verify-email/{userId}?query={code}</c>; NEW relies on
/// ABP's stock <c>IdentityUser.SetEmailConfirmed</c> flow, generating a
/// confirmation token via
/// <see cref="IdentityUserManager.GenerateEmailConfirmationTokenAsync"/>
/// and pointing the link at the AuthServer's
/// <c>/Account/EmailConfirmation</c> page. End-state identical:
/// clicking the link flips the user's <c>IsEmailConfirmed</c> flag.</para>
///
/// <para>Single recipient: the user's own email. No CC. No fan-out. The
/// stakeholder resolver does not run for registration events because
/// the user has no appointment yet.</para>
/// </summary>
public class UserRegisteredEmailHandler :
    ILocalEventHandler<UserRegisteredEto>,
    ITransientDependency
{
    /// <summary>
    /// Dev-default AuthServer URL when the per-tenant
    /// <see cref="CaseEvaluationSettings.NotificationsPolicy.AuthServerBaseUrl"/>
    /// setting is not configured. Mirrors the same default the
    /// AccessorInvitedEmailHandler uses, keeping the two flows in sync
    /// so a single setting flip switches both.
    /// </summary>
    private const string DefaultAuthServerBaseUrl = "https://localhost:44368";

    private readonly INotificationDispatcher _dispatcher;
    private readonly IdentityUserManager _userManager;
    private readonly ISettingProvider _settingProvider;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<UserRegisteredEmailHandler> _logger;

    public UserRegisteredEmailHandler(
        INotificationDispatcher dispatcher,
        IdentityUserManager userManager,
        ISettingProvider settingProvider,
        ICurrentTenant currentTenant,
        ILogger<UserRegisteredEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _userManager = userManager;
        _settingProvider = settingProvider;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(UserRegisteredEto eventData)
    {
        if (eventData == null || eventData.UserId == Guid.Empty)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(eventData.Email))
        {
            _logger.LogWarning(
                "UserRegisteredEmailHandler: empty email on event for user {UserId}; cannot send verification.",
                eventData.UserId);
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var user = await _userManager.FindByIdAsync(eventData.UserId.ToString());
            if (user == null)
            {
                _logger.LogWarning(
                    "UserRegisteredEmailHandler: user {UserId} not found in tenant {TenantId}; skipping.",
                    eventData.UserId, eventData.TenantId);
                return;
            }

            // ABP's stock email-confirmation flow: token is single-use,
            // expires per IdentityOptions.Tokens.EmailConfirmationTokenProvider.
            var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var authServerBaseUrl = await ResolveAuthServerBaseUrlAsync();
            var verifyUrl = BuildEmailConfirmationUrl(authServerBaseUrl, eventData.UserId, confirmationToken);

            var recipients = new List<NotificationRecipient>
            {
                new(
                    email: eventData.Email,
                    role: ResolveRecipientRole(eventData.RoleName),
                    isRegistered: true),
            };

            var variables = BuildVariables(eventData, verifyUrl);

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.UserRegistered,
                recipients: recipients,
                variables: variables,
                contextTag: $"UserRegistered/{eventData.UserId}");
        }
    }

    private async Task<string> ResolveAuthServerBaseUrlAsync()
    {
        var configured = await _settingProvider.GetOrNullAsync(
            CaseEvaluationSettings.NotificationsPolicy.AuthServerBaseUrl);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultAuthServerBaseUrl;
        }
        return configured.TrimEnd('/');
    }

    /// <summary>
    /// Builds ABP's stock confirm-email URL contract:
    /// <c>{base}/Account/EmailConfirmation?userId={guid}&amp;confirmationToken={url-encoded-token}</c>.
    /// Internal for unit-test coverage.
    /// </summary>
    internal static string BuildEmailConfirmationUrl(
        string authServerBaseUrl,
        Guid userId,
        string confirmationToken)
    {
        var builder = new StringBuilder();
        builder.Append(authServerBaseUrl);
        builder.Append("/Account/EmailConfirmation");
        builder.Append("?userId=").Append(userId.ToString());
        builder.Append("&confirmationToken=").Append(WebUtility.UrlEncode(confirmationToken));
        return builder.ToString();
    }

    /// <summary>
    /// Maps the Eto's role name to <see cref="RecipientRole"/>. Returns
    /// <see cref="RecipientRole.Patient"/> when the role does not match
    /// a typed value -- Patient is the most demo-frequent registration
    /// path and the only one the resolver currently uses for downstream
    /// branching. Unrecognised role names log at Debug.
    /// </summary>
    private RecipientRole ResolveRecipientRole(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return RecipientRole.Patient;
        }
        return roleName.Trim() switch
        {
            "Patient" => RecipientRole.Patient,
            "Adjuster" => RecipientRole.ClaimExaminer,
            "Applicant Attorney" => RecipientRole.ApplicantAttorney,
            "Defense Attorney" => RecipientRole.DefenseAttorney,
            "Claim Examiner" => RecipientRole.ClaimExaminer,
            _ => RecipientRole.Patient,
        };
    }

    /// <summary>
    /// Builds the variable bag the OLD-verbatim
    /// <c>User-Registed.html</c> body expects. Tokens used by the
    /// template:
    ///   <c>##PatientFirstName##</c>, <c>##URL##</c>, plus the brand
    /// placeholders rendered as empty strings until per-tenant branding
    /// ships.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> BuildVariables(
        UserRegisteredEto eventData,
        string verifyUrl)
    {
        var vars = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["PatientFirstName"] = eventData.FirstName ?? string.Empty,
            ["PatientLastName"] = eventData.LastName ?? string.Empty,
            ["PatientFullName"] = JoinName(eventData.FirstName, eventData.LastName),
            ["PatientEmail"] = eventData.Email ?? string.Empty,
            ["URL"] = verifyUrl,
        };

        AddBrandPlaceholders(vars);
        return vars;
    }

    private static void AddBrandPlaceholders(Dictionary<string, object?> vars)
    {
        vars["CompanyLogo"] = string.Empty;
        vars["lblHeaderTitle"] = string.Empty;
        vars["lblFooterText"] = string.Empty;
        vars["Email"] = string.Empty;
        vars["Skype"] = string.Empty;
        vars["ph_US"] = string.Empty;
        vars["fax"] = string.Empty;
        vars["imageInByte"] = string.Empty;
    }

    private static string JoinName(string? first, string? last)
    {
        var hasFirst = !string.IsNullOrWhiteSpace(first);
        var hasLast = !string.IsNullOrWhiteSpace(last);
        if (hasFirst && hasLast)
        {
            return first!.Trim() + " " + last!.Trim();
        }
        if (hasFirst)
        {
            return first!.Trim();
        }
        if (hasLast)
        {
            return last!.Trim();
        }
        return string.Empty;
    }
}
