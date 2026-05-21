using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.Account.Emailing;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.Emailing;

/// <summary>
/// 2026-05-06 -- replaces ABP Commercial's default
/// <c>Volo.Abp.Account.Emailing.AccountEmailer</c> for this AuthServer.
///
/// <para><b>Why we override:</b> ABP's default implementation renders
/// email bodies through <c>ITemplateRenderer</c> (Scriban-backed). In
/// our build the Scriban version is pinned to 7.1.0 to clear NuGetAudit
/// CVEs, which is binary-incompatible with the ABP 10.0.2 package
/// expecting Scriban 6.x's <c>ParserOptions</c> layout, so any call into
/// the default emailer throws <c>System.TypeLoadException</c>. Our
/// override sidesteps Scriban entirely by rendering bodies through the
/// in-house DB-backed <c>NotificationTemplate</c> aggregate +
/// <see cref="TemplateVariableSubstitutor"/> (<c>##Var##</c> placeholders),
/// which is the same pipeline our app-side handlers
/// (<c>UserRegisteredEmailHandler</c>, <c>BookingSubmissionEmailHandler</c>,
/// etc.) already use. Single template engine for the whole app.</para>
///
/// <para><b>Where it fires:</b> ABP's <c>AccountAppService</c> resolves
/// <c>IAccountEmailer</c> from DI for every account-email surface
/// (Verify button on <c>/Account/ConfirmUser</c>, Forgot Password,
/// 2FA email codes). With this override registered via
/// <c>[Dependency(ReplaceServices = true)]</c> +
/// <c>[ExposeServices(typeof(IAccountEmailer))]</c>, every call lands
/// here instead of the default <c>AccountEmailer</c>.</para>
///
/// <para><b>How it sends:</b> the rendered subject + body are wrapped
/// in a <see cref="SendAppointmentEmailArgs"/> and enqueued through
/// <see cref="IBackgroundJobManager"/>. The job is a row in the shared
/// SQL Hangfire schema; the API host runs the only Hangfire server
/// (<c>HttpApi.Host:IsJobExecutionEnabled=true</c>) and dequeues the
/// job, sending via SMTP. The AuthServer never opens an SMTP socket
/// itself.</para>
///
/// <para><b>URL construction (2026-05-18):</b> verification + reset
/// links point at the AuthServer Razor pages (port 44368) regardless
/// of the <c>appName</c> argument ABP passes. The AuthServer hosts
/// <c>/Account/EmailConfirmation</c> (custom override added 2026-05-18)
/// and <c>/Account/ResetPassword</c> (custom override added Phase 10
/// 2026-05-03) for the click-from-email landing surface. Base URL
/// comes from the per-tenant
/// <c>Notifications.AuthServerBaseUrl</c> setting, defaulting to
/// <c>http://falkinstein.localhost:44368</c> for the Phase 1A demo
/// tenant. The prior SPA-route targets (<c>/account/email-confirmation</c>
/// and <c>/account/reset-password</c>) were 404 after the SPA's
/// <c>/account/*</c> routes were deleted 2026-05-15; see
/// docs/plans/2026-05-18-fix-verification-email-url.md.</para>
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IAccountEmailer))]
public class CaseEvaluationAccountEmailer : IAccountEmailer, ITransientDependency
{
    private const string DefaultAuthServerBaseUrl = "http://falkinstein.localhost:44368";

    private readonly INotificationTemplateRepository _templateRepository;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ISettingProvider _settingProvider;
    private readonly ILogger<CaseEvaluationAccountEmailer> _logger;

    public CaseEvaluationAccountEmailer(
        INotificationTemplateRepository templateRepository,
        IBackgroundJobManager backgroundJobManager,
        ICurrentTenant currentTenant,
        ISettingProvider settingProvider,
        ILogger<CaseEvaluationAccountEmailer> logger)
    {
        _templateRepository = templateRepository;
        _backgroundJobManager = backgroundJobManager;
        _currentTenant = currentTenant;
        _settingProvider = settingProvider;
        _logger = logger;
    }

    public virtual async Task SendEmailConfirmationLinkAsync(
        IdentityUser user,
        string confirmationToken,
        string appName,
        string? returnUrl = null,
        string? returnUrlHash = null)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));

        var authServerBaseUrl = await ResolveAuthServerBaseUrlAsync();
        var url = BuildEmailConfirmationUrl(authServerBaseUrl, user.Id, confirmationToken);

        await DispatchAsync(
            templateCode: NotificationTemplateConsts.Codes.UserRegistered,
            recipient: user.Email ?? string.Empty,
            variables: BuildLinkVariables(user, url),
            contextTag: $"AccountEmailer/EmailConfirmationLink/{user.Id}");
    }

    public virtual async Task SendPasswordResetLinkAsync(
        IdentityUser user,
        string resetToken,
        string appName,
        string? returnUrl = null,
        string? returnUrlHash = null)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));

        var authServerBaseUrl = await ResolveAuthServerBaseUrlAsync();
        var url = BuildPasswordResetUrl(authServerBaseUrl, user.Id, resetToken);

        await DispatchAsync(
            templateCode: NotificationTemplateConsts.Codes.ResetPassword,
            recipient: user.Email ?? string.Empty,
            variables: BuildLinkVariables(user, url),
            contextTag: $"AccountEmailer/PasswordResetLink/{user.Id}");
    }

    public virtual async Task SendEmailSecurityCodeAsync(IdentityUser user, string code)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));

        // Email-based 2FA security code path. Not on the demo critical-path
        // (2FA is not enabled by default) but implemented to keep the
        // IAccountEmailer override complete. Reuses the UserRegistered
        // template; the URL slot carries the human-readable code so the
        // template's call-to-action region surfaces it.
        await DispatchAsync(
            templateCode: NotificationTemplateConsts.Codes.UserRegistered,
            recipient: user.Email ?? string.Empty,
            variables: BuildCodeVariables(user.Name, user.Surname, code),
            contextTag: $"AccountEmailer/SecurityCode/{user.Id}");
    }

    public virtual async Task SendEmailConfirmationCodeAsync(string emailAddress, string code)
    {
        // Code-based confirmation (no IdentityUser handle yet -- e.g.
        // pre-registration email-claim verification). Same template as
        // the 2FA path; not on the demo critical-path.
        await DispatchAsync(
            templateCode: NotificationTemplateConsts.Codes.UserRegistered,
            recipient: emailAddress ?? string.Empty,
            variables: BuildCodeVariables(firstName: null, lastName: null, code: code),
            contextTag: $"AccountEmailer/ConfirmationCode/{emailAddress}");
    }

    private async Task DispatchAsync(
        string templateCode,
        string recipient,
        IReadOnlyDictionary<string, object?> variables,
        string contextTag)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            _logger.LogWarning(
                "CaseEvaluationAccountEmailer: empty recipient for {TemplateCode} ({Context}); skipping.",
                templateCode, contextTag);
            return;
        }

        var template = await _templateRepository.FindByCodeAsync(templateCode);
        if (template == null || !template.IsActive)
        {
            _logger.LogWarning(
                "CaseEvaluationAccountEmailer: template {TemplateCode} missing or inactive ({Context}); skipping send.",
                templateCode, contextTag);
            return;
        }

        var subject = TemplateVariableSubstitutor.Substitute(template.Subject, variables);
        var body = TemplateVariableSubstitutor.Substitute(template.BodyEmail, variables);

        await _backgroundJobManager.EnqueueAsync(new SendAppointmentEmailArgs
        {
            To = recipient,
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
            Context = contextTag,
            IsRegistered = true,
            TenantName = _currentTenant.Name,
        });
    }

    private async Task<string> ResolveAuthServerBaseUrlAsync()
    {
        var configured = await _settingProvider.GetOrNullAsync(
            CaseEvaluationSettings.NotificationsPolicy.AuthServerBaseUrl);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultAuthServerBaseUrl;
        }
        // BUG-014 (Task A): prepend tenant subdomain so the tenant-less base
        // URL from Settings__CaseEvaluation__Notifications__AuthServerBaseUrl
        // becomes <tenant>.localhost:<port>. No-op on already-prefixed URLs.
        return TenantUrlComposer.ComposeForTenant(configured.TrimEnd('/'), _currentTenant.Name)!;
    }

    private static IReadOnlyDictionary<string, object?> BuildLinkVariables(IdentityUser user, string url)
    {
        var vars = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["PatientFirstName"] = user.Name ?? string.Empty,
            ["PatientLastName"] = user.Surname ?? string.Empty,
            ["PatientFullName"] = JoinName(user.Name, user.Surname),
            ["PatientEmail"] = user.Email ?? string.Empty,
            ["URL"] = url,
        };
        AddBrandPlaceholders(vars);
        return vars;
    }

    private static IReadOnlyDictionary<string, object?> BuildCodeVariables(string? firstName, string? lastName, string code)
    {
        var vars = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["PatientFirstName"] = firstName ?? string.Empty,
            ["PatientLastName"] = lastName ?? string.Empty,
            ["PatientFullName"] = JoinName(firstName, lastName),
            ["URL"] = "Your code: " + code,
            ["Code"] = code,
        };
        AddBrandPlaceholders(vars);
        return vars;
    }

    private static void AddBrandPlaceholders(Dictionary<string, object?> vars)
    {
        // Branding placeholders are kept blank until per-tenant branding
        // ships (Adrian directive 2026-05-05). The OLD-verbatim template
        // bodies still reference these tokens; emitting empty strings
        // matches what UserRegisteredEmailHandler does today.
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
        if (hasFirst && hasLast) return first!.Trim() + " " + last!.Trim();
        if (hasFirst) return first!.Trim();
        if (hasLast) return last!.Trim();
        return string.Empty;
    }

    /// <summary>
    /// Builds the AuthServer-hosted email-confirmation URL:
    /// <c>{authServerBaseUrl}/Account/EmailConfirmation?userId={guid}&amp;confirmationToken={url-encoded}</c>.
    /// Lands on the project's custom Razor PageModel
    /// <see cref="HealthcareSupport.CaseEvaluation.Pages.Account.EmailConfirmationModel"/>
    /// which processes the token server-side and 302's to
    /// <c>/Account/Login?flash=email-verified</c>. Internal so unit
    /// tests can verify the encoding behavior.
    /// 2026-05-18 -- repointed from the deleted SPA route
    /// <c>/account/email-confirmation</c>; see
    /// docs/plans/2026-05-18-fix-verification-email-url.md.
    /// </summary>
    internal static string BuildEmailConfirmationUrl(string authServerBaseUrl, Guid userId, string token)
    {
        var sb = new StringBuilder();
        sb.Append(authServerBaseUrl);
        sb.Append("/Account/EmailConfirmation");
        sb.Append("?userId=").Append(userId.ToString());
        sb.Append("&confirmationToken=").Append(WebUtility.UrlEncode(token));
        return sb.ToString();
    }

    /// <summary>
    /// Builds the AuthServer-hosted password-reset URL:
    /// <c>{authServerBaseUrl}/Account/ResetPassword?userId={guid}&amp;resetToken={url-encoded}</c>.
    /// Lands on the project's custom Razor PageModel
    /// <see cref="HealthcareSupport.CaseEvaluation.Pages.Account.ResetPasswordModel"/>
    /// (Phase 10 2026-05-03). 2026-05-18 -- repointed from the deleted
    /// SPA route <c>/account/reset-password</c>; this path was dead-letter
    /// before today because the project's ForgotPassword flow uses
    /// <c>ExternalAccountAppService.BuildResetUrl</c> directly. Fixed
    /// alongside the email-confirmation repoint for symmetry.
    /// </summary>
    internal static string BuildPasswordResetUrl(string authServerBaseUrl, Guid userId, string token)
    {
        var sb = new StringBuilder();
        sb.Append(authServerBaseUrl);
        sb.Append("/Account/ResetPassword");
        sb.Append("?userId=").Append(userId.ToString());
        sb.Append("&resetToken=").Append(WebUtility.UrlEncode(token));
        return sb.ToString();
    }
}
