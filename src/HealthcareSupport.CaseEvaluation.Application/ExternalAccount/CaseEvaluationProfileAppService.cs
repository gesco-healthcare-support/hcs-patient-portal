using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Account;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// G-04-04 (2026-06-02): wires the in-app authenticated change-password
/// security receipt. ABP's stock <see cref="ProfileAppService"/> (served at
/// <c>/Account/Manage</c> -> <c>api/account/my-profile/change-password</c>)
/// changes the password but dispatches no email. OLD sent a "your password
/// has been successfully changed" receipt after BOTH the in-app change and
/// the post-reset flow (<c>UserDomain.SendEmail</c>, IsChangePassword branch).
/// The post-reset half is already wired in
/// <see cref="ExternalAccountAppService.ResetPasswordAsync"/>; this override
/// closes the in-app half by dispatching the same per-tenant
/// <c>PasswordChange</c> template after a successful change.
///
/// <para>Registered via <see cref="DependencyAttribute.ReplaceServices"/> +
/// <see cref="ExposeServicesAttribute"/> so it replaces the framework
/// <c>IProfileAppService</c> on every host that serves the Manage page
/// (AuthServer) and the proxy (HttpApi.Host). ABP 10.0.2 publishes no
/// password-changed domain event to subscribe to, so overriding the
/// change-password seam is the precise trigger (mirrors the post-reset
/// dispatch rather than a broad EntityUpdated handler).</para>
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IProfileAppService))]
public class CaseEvaluationProfileAppService : ProfileAppService
{
    private readonly INotificationDispatcher _dispatcher;

    // This ctor mirrors ABP Commercial's ProfileAppService ctor solely to
    // forward its dependencies to base(...); only INotificationDispatcher is
    // ours. If an ABP Pro upgrade changes the base ctor, this will fail to
    // COMPILE (a safe, loud failure) -- update the parameter list to match.
    public CaseEvaluationProfileAppService(
        IdentityUserManager userManager,
        IdentitySecurityLogManager securityLogManager,
        IdentityProTwoFactorManager twoFactorManager,
        IOptions<IdentityOptions> identityOptions,
        IdentityUserTwoFactorChecker twoFactorChecker,
        ITimezoneProvider timezoneProvider,
        ISettingManager settingManager,
        INotificationDispatcher dispatcher)
        : base(
            userManager,
            securityLogManager,
            twoFactorManager,
            identityOptions,
            twoFactorChecker,
            timezoneProvider,
            settingManager)
    {
        _dispatcher = dispatcher;
    }

    public override async Task ChangePasswordAsync(ChangePasswordInput input)
    {
        await base.ChangePasswordAsync(input);

        // The password is already changed; a dispatch failure is logged, not
        // bubbled, so the API call still returns success (mirrors the
        // post-reset receipt's swallow-and-log in ExternalAccountAppService).
        try
        {
            var user = await UserManager.GetByIdAsync(CurrentUser.GetId());
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            // The role tag is pass-through metadata for the email job only (no
            // CC fan-out / role resolver runs on this dispatch path). Any
            // authenticated user can change their password, so Patient is the
            // generic external tag here, consistent with the post-reset receipt.
            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.PasswordChange,
                recipients: new[]
                {
                    new NotificationRecipient(
                        email: user.Email,
                        role: RecipientRole.Patient,
                        isRegistered: true),
                },
                variables: BuildReceiptVariables(user),
                contextTag: $"PasswordChange/InApp/{user.Id}");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "CaseEvaluationProfileAppService.ChangePasswordAsync: in-app password-changed receipt dispatch failed for user {UserId}.",
                CurrentUser.Id);
        }
    }

    /// <summary>
    /// Variable bag for the PasswordChange template. The seeded body uses only
    /// <c>##PatientFirstName##</c>; the companions mirror
    /// <c>ExternalAccountAppService.BuildPasswordTokenVariables</c> so any
    /// IT-Admin body edit that adds the standard tokens still substitutes.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> BuildReceiptVariables(IdentityUser user)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["PatientFirstName"] = user.Name ?? string.Empty,
            ["PatientLastName"] = user.Surname ?? string.Empty,
            ["PatientFullName"] = $"{user.Name} {user.Surname}".Trim(),
            ["PatientEmail"] = user.Email ?? string.Empty,
            ["URL"] = string.Empty,
        };
    }
}
