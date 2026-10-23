using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Answers one question for the activation page: does this link still work. Checking never consumes
/// the token, which is the whole point. Mail scanners such as Microsoft 365 Safe Links open a link
/// (HEAD, then GET) seconds before the person does; on 2026-10-23 one such HEAD confirmed an email by
/// itself. So the page only CHECKS on GET and HEAD, and acts on the POST of a form no scanner fills in.
///
/// <para>A plain service and not an app-service method, for the reason <see cref="ResetLinkChecker"/>
/// gives: an anonymous "is this token valid for this user" endpoint would be an oracle. Used, expired,
/// tampered and unknown-user all return the same false.</para>
/// </summary>
public interface IAccountActivationLinkChecker
{
    Task<bool> IsUsableAsync(Guid userId, string? token);
}

public class AccountActivationLinkChecker : IAccountActivationLinkChecker, ITransientDependency
{
    private readonly IdentityUserManager _userManager;

    public AccountActivationLinkChecker(IdentityUserManager userManager)
    {
        _userManager = userManager;
    }

    public virtual async Task<bool> IsUsableAsync(Guid userId, string? token)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        // A deactivated account's link is dead too: switching it off must also stop an old link from
        // setting its password.
        if (user == null || !user.IsActive)
        {
            return false;
        }

        return await _userManager.VerifyUserTokenAsync(
            user,
            _userManager.Options.Tokens.PasswordResetTokenProvider,
            AccountActivationTokens.Purpose,
            token);
    }
}
