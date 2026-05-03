using Volo.Abp.Identity.Settings;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// Enables ABP's email-confirmation-required-for-login gate to mirror OLD's
/// <c>IsVerified</c> flag verbatim (Phase 2.2, 2026-05-01). With this default
/// flipped, a registered user must click the verification link before
/// AuthServer issues tokens; pre-verification login attempts return the
/// localized "verify your email" message. ABP's
/// <c>DataProtectionTokenProvider</c> issues the cryptographic token,
/// replacing OLD's reuse of the user-row <c>VerificationCode</c> GUID.
/// </summary>
public class ChangeIdentityEmailConfirmationSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        var requireConfirmedEmail = context.GetOrNull(IdentitySettingNames.SignIn.RequireConfirmedEmail);
        if (requireConfirmedEmail != null)
        {
            requireConfirmedEmail.DefaultValue = true.ToString();
        }

        // Mirrors OLD's "verify your email before any further action" gate
        // -- ABP exposes a separate registration-time flag that, when true,
        // forces the verification link click before the user can complete
        // first-time setup actions.
        var requireEmailVerificationToRegister = context.GetOrNull(IdentitySettingNames.SignIn.RequireEmailVerificationToRegister);
        if (requireEmailVerificationToRegister != null)
        {
            requireEmailVerificationToRegister.DefaultValue = true.ToString();
        }
    }
}
