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
/// C5 / Phase 18 (2026-05-04) -- subscribes to
/// <see cref="AppointmentAccessorInvitedEto"/> and dispatches the
/// "you have been invited as an accessor" email through Phase 18's
/// <see cref="INotificationDispatcher"/>. Template: OLD-verbatim
/// <c>AccessorAppointmentBooked</c> (on-disk HTML in OLD;
/// <c>EmailTemplate.AccessorAppointmentBooked</c>).
///
/// <para>Mirrors OLD's behavior at
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\Core\AppointmentAccessorDomain.cs</c>:69-89,
/// 263-303 (<c>CreateAccountOfAppointmentAccessors</c>). When OLD
/// auto-created an IdentityUser for a new accessor, it called
/// <c>SendEmailToAccessor(...)</c> with the freshly generated 8-char temp
/// password embedded in the email body
/// (<c>UserAuthenticationDomain</c> line 267-268). That is a security
/// regression we intentionally do NOT replicate -- NEW substitutes a
/// single-use ABP Identity password-reset token, rendered into the
/// email body via the standard <c>##URL##</c> template variable. The
/// recipient clicks the link, lands on AuthServer's
/// <c>/Account/ResetPassword</c>, sets their own password, then logs
/// in. Functionally equivalent to OLD's "you can log in now" flow with
/// modern credential hygiene.</para>
///
/// <para>The auto-creation of the IdentityUser already happens in
/// <c>AppointmentAccessorManager.CreateOrLinkAsync</c> (Phase 11i,
/// 2026-05-04) -- C5 is email-only.</para>
/// </summary>
public class AccessorInvitedEmailHandler :
    ILocalEventHandler<AppointmentAccessorInvitedEto>,
    ITransientDependency
{
    // 2026-05-07 (Wave 3 #17.1): default flipped to the Phase 1A Falkinstein
    // tenant subdomain on plain HTTP (the Docker-exposed AuthServer port).
    // Defensive fallback when ABP setting subsystem returns null for
    // AuthServerBaseUrl. Override per-tenant in /setting-management.
    private const string DefaultAuthServerBaseUrl = "http://falkinstein.localhost:44368";

    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IdentityUserManager _userManager;
    private readonly ISettingProvider _settingProvider;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AccessorInvitedEmailHandler> _logger;

    public AccessorInvitedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IdentityUserManager userManager,
        ISettingProvider settingProvider,
        ICurrentTenant currentTenant,
        ILogger<AccessorInvitedEmailHandler> logger)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _userManager = userManager;
        _settingProvider = settingProvider;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentAccessorInvitedEto eventData)
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
                    "AccessorInvitedEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            var user = await _userManager.FindByIdAsync(eventData.InvitedUserId.ToString());
            if (user == null)
            {
                _logger.LogWarning(
                    "AccessorInvitedEmailHandler: invited user {UserId} not found; skipping.",
                    eventData.InvitedUserId);
                return;
            }

            // Security improvement vs OLD: generate a single-use reset token
            // instead of echoing a plaintext temp password. Standard ABP
            // Identity flow; the link expires per IdentityOptions.Tokens.
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var authServerBaseUrl = await ResolveAuthServerBaseUrlAsync();
            var setupUrl = BuildAccountSetupUrl(authServerBaseUrl, eventData.InvitedUserId, resetToken);

            var recipients = new[]
            {
                new NotificationRecipient(
                    email: eventData.Email,
                    role: MapRoleName(eventData.RoleName),
                    isRegistered: false),
            };

            // BuildVariables returns a read-only dict; copy it into a mutable
            // map so we can append the two accessor-specific OLD-verbatim
            // variables (##URL## for the password-setup link, ##Email## for
            // the body's "we've created an account for {{ Email }}" line).
            var variables = new Dictionary<string, object?>(
                DocumentNotificationContext.BuildVariables(
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
                    portalUrl: ctx.PortalBaseUrl))
            {
                ["##URL##"] = setupUrl,
                ["##Email##"] = eventData.Email,
            };

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.AccessorAppointmentBooked,
                recipients: recipients,
                variables: variables,
                contextTag: $"AccessorInvited/{eventData.InvitedUserId}");
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
    /// Maps the Eto's free-text role name (set by
    /// <c>AppointmentAccessorManager.CreateOrLinkAsync</c> from the
    /// caller-supplied role) to the typed <see cref="RecipientRole"/>
    /// enum. Returns <c>null</c> for unrecognised names so the renderer
    /// falls back to the role-agnostic template body. Internal for
    /// unit-test coverage.
    /// </summary>
    internal static RecipientRole? MapRoleName(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return null;
        }
        return roleName.Trim() switch
        {
            "Patient" => RecipientRole.Patient,
            "Applicant Attorney" => RecipientRole.ApplicantAttorney,
            "Defense Attorney" => RecipientRole.DefenseAttorney,
            "Claim Examiner" => RecipientRole.ClaimExaminer,
            _ => null,
        };
    }

    /// <summary>
    /// Builds the AuthServer setup URL the invited user clicks from the
    /// email. Same query-string contract as ExternalAccountAppService's
    /// password-reset flow: <c>{base}/Account/ResetPassword?userId={guid}
    /// &amp;resetToken={url-encoded-token}</c>. The user lands on the
    /// AuthServer Razor page, sets a new password, and is then redirected
    /// to the SPA. Internal for unit-test coverage.
    /// </summary>
    internal static string BuildAccountSetupUrl(
        string authServerBaseUrl,
        Guid userId,
        string resetToken)
    {
        var builder = new StringBuilder();
        builder.Append(authServerBaseUrl);
        builder.Append("/Account/ResetPassword");
        builder.Append("?userId=").Append(userId.ToString());
        builder.Append("&resetToken=").Append(WebUtility.UrlEncode(resetToken));
        return builder.ToString();
    }
}
