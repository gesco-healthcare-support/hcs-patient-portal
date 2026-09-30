using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Identity;
using Volo.Abp.Identity.AspNetCore;
using Volo.Abp.Settings;

// Both Microsoft.AspNetCore.Identity and Volo.Abp.Identity declare an IdentityUser, and this file
// needs types from both namespaces. ABP's is the one this product persists.
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace HealthcareSupport.CaseEvaluation.AuthServer.AdminPasswords;

/// <summary>
/// B12 decision D2 -- outside Development, a published default password never signs anyone in, even
/// when it is genuinely the account's password.
///
/// <para><b>Why this exists when rotation already runs.</b> Rotation is a migrator pass, and it only
/// covers databases the migrator reaches, when it reaches them. This is the backstop that does not
/// depend on that happening: an office restored from an old backup, a database created by a route
/// nobody anticipated, or a rotation that failed halfway, all still refuse the published password
/// here. A defence that runs once at deploy time and a defence that runs on every sign-in fail in
/// different ways, which is the point of having both.</para>
///
/// <para>The check happens BEFORE the hash is verified, so it costs nothing on the normal path and
/// cannot be timed against a real verification.</para>
///
/// <para>It returns a plain failure rather than a distinct one. A caller trying a published default
/// learns only that it did not work -- not whether it is the account's actual password, which would
/// tell them the account exists AND that it is on a default, on a host they have not signed in
/// to.</para>
///
/// <para>Overriding <see cref="CheckPasswordSignInAsync"/> rather than the two
/// <c>PasswordSignInAsync</c> overloads covers both: ASP.NET Core Identity routes every password
/// sign-in through this method, so the login page and any grant flow are both behind it.</para>
/// </summary>
public class KnownDefaultPasswordSignInManager : AbpSignInManager
{
    private readonly IHostEnvironment _hostEnvironment;

    public KnownDefaultPasswordSignInManager(
        IdentityUserManager userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<IdentityUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<IdentityUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<IdentityUser> confirmation,
        IOptions<AbpIdentityOptions> options,
        ISettingProvider settingProvider,
        IHostEnvironment hostEnvironment)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes,
            confirmation, options, settingProvider)
    {
        _hostEnvironment = hostEnvironment;
    }

    public override Task<SignInResult> CheckPasswordSignInAsync(
        IdentityUser user,
        string password,
        bool lockoutOnFailure)
    {
        if (!_hostEnvironment.IsDevelopment() && AdminPasswordPolicy.IsKnownDefault(password))
        {
            Logger.LogWarning(
                "Refused a sign-in attempt using a password this product ships as a default. " +
                "The password is not logged.");

            return Task.FromResult(SignInResult.Failed);
        }

        return base.CheckPasswordSignInAsync(user, password, lockoutOnFailure);
    }
}
