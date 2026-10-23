using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Completes an activate-your-account link: sets the password the person chose AND confirms their
/// email, in one unit of work, so they can sign in straight away.
///
/// <para>Confirming here, rather than on arrival, is what keeps the appointment-access rule honest.
/// Party access to an appointment matches the caller's email only once it is confirmed, and for an
/// account someone else created the confirmed email is the ONLY thing that grants that access.
/// Confirmation now needs the token from the mailbox AND a password the mailbox holder chose; the
/// password whoever created the account typed (or the throwaway New Practice generates) is replaced
/// before the account can ever sign in, since an unconfirmed account cannot.</para>
///
/// <para>A plain service, not an app service, so no anonymous HTTP route appears for it. The page
/// that calls it is throttled by the proxy's sign-in limit.</para>
/// </summary>
public interface IAccountActivationService
{
    /// <summary>
    /// Throws <see cref="BusinessException"/> with
    /// <see cref="CaseEvaluationDomainErrorCodes.ActivationLinkInvalid"/> for a dead link (used,
    /// expired, tampered, unknown user), and <see cref="UserFriendlyException"/> when the password
    /// breaks the policy or the two entries differ. Nothing changes in either case, so the same link
    /// can be retried after a policy failure.
    /// </summary>
    Task ActivateAsync(ActivateAccountInput input);
}

public class AccountActivationService : IAccountActivationService, ITransientDependency
{
    private readonly IdentityUserManager _userManager;

    public AccountActivationService(IdentityUserManager userManager)
    {
        _userManager = userManager;
    }

    [UnitOfWork]
    public virtual async Task ActivateAsync(ActivateAccountInput input)
    {
        Check.NotNull(input, nameof(input));
        ExternalAccountAppService.EnsurePasswordsMatch(input.Password, input.ConfirmPassword);

        if (input.UserId == Guid.Empty || string.IsNullOrWhiteSpace(input.Token))
        {
            throw InvalidLink();
        }

        var user = await _userManager.FindByIdAsync(input.UserId.ToString());
        if (user == null)
        {
            throw InvalidLink();
        }

        var tokenOk = await _userManager.VerifyUserTokenAsync(
            user,
            _userManager.Options.Tokens.PasswordResetTokenProvider,
            AccountActivationTokens.Purpose,
            input.Token);
        if (!tokenOk)
        {
            throw InvalidLink();
        }

        // The password goes through Identity's reset path with a token minted here, in process, so
        // the configured password policy applies and the security stamp rotates. The rotation is
        // what makes the activation link single-use: it is bound to the old stamp.
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await _userManager.ResetPasswordAsync(user, resetToken, input.Password);
        if (!reset.Succeeded)
        {
            if (ExternalAccountAppService.IsTokenFailure(reset))
            {
                throw InvalidLink();
            }
            throw new UserFriendlyException(string.Join(", ", reset.Errors.Select(e => e.Description)));
        }

        user.SetEmailConfirmed(true);
        // The person has just chosen this password, so asking again at the first sign-in would only
        // make them choose twice.
        user.SetShouldChangePasswordOnNextLogin(false);
        (await _userManager.UpdateAsync(user)).CheckErrors();

        // Same reasoning as a completed reset: proving the mailbox is a stronger signal than whatever
        // failed attempts caused a lockout, so access is restored.
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.SetLockoutEndDateAsync(user, null);
    }

    private static BusinessException InvalidLink() =>
        new(CaseEvaluationDomainErrorCodes.ActivationLinkInvalid);
}
