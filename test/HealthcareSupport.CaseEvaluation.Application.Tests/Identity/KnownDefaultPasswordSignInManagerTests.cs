using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AuthServer.AdminPasswords;
using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.Settings;
using Xunit;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// B12 task 8 -- the sign-in backstop, against the real IdentityUserManager and real password
/// hashes.
///
/// <para>The assertion that matters is not "a wrong password fails" -- that is true anyway. It is
/// that a published default fails EVEN WHEN IT IS THE ACCOUNT'S ACTUAL PASSWORD, which is why each
/// test sets the account to the password it then tries.</para>
/// </summary>
public abstract class KnownDefaultPasswordSignInManagerTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string ARealPassword = "Synthetic-Chosen-Pass3!";

    private readonly IdentityUserManager _userManager;

    protected KnownDefaultPasswordSignInManagerTests()
    {
        _userManager = GetRequiredService<IdentityUserManager>();
    }

    private KnownDefaultPasswordSignInManager SignInManager(bool isDevelopment)
    {
        return new KnownDefaultPasswordSignInManager(
            _userManager,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            GetRequiredService<IUserClaimsPrincipalFactory<IdentityUser>>(),
            GetRequiredService<IOptions<IdentityOptions>>(),
            NullLogger<SignInManager<IdentityUser>>.Instance,
            Substitute.For<IAuthenticationSchemeProvider>(),
            GetRequiredService<IUserConfirmation<IdentityUser>>(),
            GetRequiredService<IOptions<AbpIdentityOptions>>(),
            GetRequiredService<ISettingProvider>(),
            new StubHostEnvironment(isDevelopment ? Environments.Development : Environments.Production));
    }

    private async Task<IdentityUser> GivenAUserWithPasswordAsync(string password)
    {
        var user = new IdentityUser(Guid.NewGuid(), "b2c3d4e5", "b2c3d4e5@f6a7b8c9.com");
        (await _userManager.CreateAsync(user, password)).Succeeded.ShouldBeTrue();
        return user;
    }

    /// <summary>
    /// The account really is on this password -- the user manager agrees -- and the sign-in is still
    /// refused. That is the whole guarantee.
    /// </summary>
    [Fact]
    public async Task OutsideDevelopment_APublishedDefaultIsRefusedEvenWhenItIsTheRealPassword()
    {
        var user = await GivenAUserWithPasswordAsync(CaseEvaluationConsts.AdminPasswordDefaultValue);
        (await _userManager.CheckPasswordAsync(user, CaseEvaluationConsts.AdminPasswordDefaultValue))
            .ShouldBeTrue("the fixture needs the password to actually be correct");

        var result = await SignInManager(isDevelopment: false)
            .CheckPasswordSignInAsync(user, CaseEvaluationConsts.AdminPasswordDefaultValue, lockoutOnFailure: false);

        result.Succeeded.ShouldBeFalse();
    }

    /// <summary>
    /// This repository's seeded password is published too, in a public repository, so it is refused
    /// on the same terms as the framework's.
    /// </summary>
    [Fact]
    public async Task OutsideDevelopment_ThisRepositorysSeededPasswordIsAlsoRefused()
    {
        var user = await GivenAUserWithPasswordAsync(InternalUsersDataSeedContributor.DefaultPassword);

        var result = await SignInManager(isDevelopment: false)
            .CheckPasswordSignInAsync(user, InternalUsersDataSeedContributor.DefaultPassword, lockoutOnFailure: false);

        result.Succeeded.ShouldBeFalse();
    }

    /// <summary>
    /// The guard must not refuse everything. A password nobody publishes still signs in, which is
    /// what proves the check is narrow rather than a blanket failure.
    /// </summary>
    [Fact]
    public async Task OutsideDevelopment_APasswordNobodyPublishesStillSucceeds()
    {
        var user = await GivenAUserWithPasswordAsync(ARealPassword);

        var result = await SignInManager(isDevelopment: false)
            .CheckPasswordSignInAsync(user, ARealPassword, lockoutOnFailure: false);

        result.Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// The control. A local clone signs in with the documented credentials exactly as before, or
    /// every onboarding page and docker guide in this repository becomes wrong.
    /// </summary>
    [Fact]
    public async Task InDevelopment_ThePublishedDefaultStillSignsIn()
    {
        var user = await GivenAUserWithPasswordAsync(CaseEvaluationConsts.AdminPasswordDefaultValue);

        var result = await SignInManager(isDevelopment: true)
            .CheckPasswordSignInAsync(user, CaseEvaluationConsts.AdminPasswordDefaultValue, lockoutOnFailure: false);

        result.Succeeded.ShouldBeTrue();
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public StubHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
