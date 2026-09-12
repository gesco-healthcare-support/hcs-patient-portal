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
    // BUG-029 v3 fix (2026-05-21): DefaultAuthServerBaseUrl removed.

    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AccessorInvitedEmailHandler> _logger;
    private readonly IAccountUrlBuilder _accountUrlBuilder;
    private readonly ITenantStore _tenantStore;

    public AccessorInvitedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        IdentityUserManager userManager,
        ICurrentTenant currentTenant,
        ILogger<AccessorInvitedEmailHandler> logger,
        IAccountUrlBuilder accountUrlBuilder,
        ITenantStore tenantStore)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _userManager = userManager;
        _currentTenant = currentTenant;
        _logger = logger;
        _accountUrlBuilder = accountUrlBuilder;
        _tenantStore = tenantStore;
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
            // BUG-029 v3 fix (2026-05-21): tenant-aware setup URL via the
            // builder; the URL shape matches BuildPasswordResetUrl
            // ({base}/Account/ResetPassword?userId=...&resetToken=...).
            var setupUrl = await _accountUrlBuilder.BuildPasswordResetUrlAsync(
                eventData.TenantId!.Value, eventData.InvitedUserId, resetToken);

            var recipients = new[]
            {
                new NotificationRecipient(
                    email: eventData.Email,
                    role: MapRoleName(eventData.RoleName),
                    isRegistered: false),
            };

            // The practice name for the template's "... at {ClinicName}" line.
            // Resolve it from the tenant store (the same source AccountUrlBuilder
            // uses): inside the _currentTenant.Change(TenantId) scope
            // ICurrentTenant.Name is null, so passing it rendered "... at ."
            // with an empty location (2026-07-10 QA fix).
            var tenantConfig = await _tenantStore.FindAsync(eventData.TenantId!.Value);

            var variables = BuildAccessorEmailVariables(
                ctx, tenantConfig?.Name, setupUrl, eventData.Email);

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.AccessorAppointmentBooked,
                recipients: recipients,
                variables: variables,
                contextTag: $"AccessorInvited/{eventData.InvitedUserId}");
        }
    }

    // ResolveAuthServerBaseUrlAsync removed; URL composition lives in
    // IAccountUrlBuilder, which reads the tenant name from an explicit
    // tenantId argument instead of ambient ICurrentTenant.Name.

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
    /// Builds the accessor-invite template variable bag. The two accessor-specific
    /// keys are BARE ("URL"/"Email"): <see cref="TemplateVariableSubstitutor"/> wraps
    /// each key itself as "##" + key + "##", so pre-wrapping to "##URL##" produced
    /// "####URL####" -- which never matched the template, leaving the set-password
    /// link dead and locking the invited party out (2026-07-10 QA fix). The clinic
    /// (practice) name is resolved from the tenant store by the caller, since
    /// ICurrentTenant.Name is null inside the Change(tenantId) scope. Internal for
    /// render-test coverage.
    /// </summary>
    internal static Dictionary<string, object?> BuildAccessorEmailVariables(
        DocumentEmailContext ctx, string? practiceName, string setupUrl, string invitedEmail)
    {
        return new Dictionary<string, object?>(
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
                clinicName: practiceName,
                portalUrl: ctx.PortalBaseUrl))
        {
            ["URL"] = setupUrl,
            ["Email"] = invitedEmail,
        };
    }

    // BUG-029 v3 fix (2026-05-21): BuildAccountSetupUrl static helper
    // moved into IAccountUrlBuilder.BuildPasswordResetUrlAsync (same
    // shape; matches ExternalAccountAppService's reset URL contract).
}
