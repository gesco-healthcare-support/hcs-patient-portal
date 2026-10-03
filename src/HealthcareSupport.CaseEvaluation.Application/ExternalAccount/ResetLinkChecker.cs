using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Answers one question for the reset-password page: does this link still work. OBS-34 (#570).
///
/// <para>Deliberately a plain service and NOT a method on <c>IExternalAccountAppService</c>. An
/// app-service method would become an anonymous HTTP endpoint (every app service gets a controller),
/// and an anonymous "is this token valid for this user id" endpoint is an oracle. This is reachable
/// only from the AuthServer page model, in process.</para>
///
/// <para>A token that is consumed, expired or tampered with, and a user id that does not exist, all
/// return the same <c>false</c>, so the page can treat them identically and reveal nothing about
/// which accounts exist. Verification does not consume the token: Identity reset tokens are bound to
/// the user's security stamp and only a successful reset changes it.</para>
/// </summary>
public interface IResetLinkChecker
{
    Task<bool> IsUsableAsync(Guid userId, string? resetToken);
}

public class ResetLinkChecker : IResetLinkChecker, ITransientDependency
{
    private readonly IdentityUserManager _userManager;

    public ResetLinkChecker(IdentityUserManager userManager)
    {
        _userManager = userManager;
    }

    public virtual async Task<bool> IsUsableAsync(Guid userId, string? resetToken)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(resetToken))
        {
            return false;
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        return await _userManager.VerifyUserTokenAsync(
            user,
            _userManager.Options.Tokens.PasswordResetTokenProvider,
            UserManager<IdentityUser>.ResetPasswordTokenPurpose,
            resetToken);
    }
}
