using Volo.Abp.Identity.Settings;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// Turns off ABP's self-service email change on the account profile
/// (<c>IsEmailUpdateEnabled</c>, default true). The portal has no flow where a
/// signed-in user edits their own login email; the address is also the key
/// that links a user to appointments naming it, so changing it is a staff
/// action. A tenant can still override the stored setting; the access rules
/// additionally require a confirmed email, so this is not the only guard.
/// </summary>
public class DisableSelfEmailChangeSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        var emailUpdate = context.GetOrNull(IdentitySettingNames.User.IsEmailUpdateEnabled);
        if (emailUpdate != null)
        {
            emailUpdate.DefaultValue = false.ToString();
        }
    }
}
