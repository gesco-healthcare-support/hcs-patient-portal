using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// B12 task 7 -- the rotation pass, against the REAL IdentityUserManager.
///
/// <para>Substituting the user manager would prove only that this class calls the methods it calls.
/// What has to be true is that the account's stored hash actually changes, that the published
/// password stops authenticating against it, and that an account already on a real password is
/// left byte-identical -- and all three are properties of the hash, not of the call.</para>
/// </summary>
public abstract class AdminPasswordRotatorTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    /// <summary>Satisfies ABP's password rules and is not a value this product seeds.</summary>
    private const string StoredPassword = "Synthetic-Rotated-Pass1!";

    private const string ARealPassword = "Synthetic-Chosen-Pass2!";

    private IdentityUserManager _userManager = null!;

    protected AdminPasswordRotatorTests()
    {
        _userManager = GetRequiredService<IdentityUserManager>();
    }

    private AdminPasswordRotator Rotator(bool isDevelopment)
    {
        return new AdminPasswordRotator(
            _userManager,
            new FixedAdminPasswordStore(StoredPassword),
            new StubHostEnvironment(isDevelopment ? Environments.Development : Environments.Production));
    }

    private async Task<IdentityUser> GivenAnAdminWithPasswordAsync(string password)
    {
        var existing = await _userManager.FindByNameAsync(CaseEvaluationConsts.AdminUserName);
        if (existing != null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(existing);
            (await _userManager.ResetPasswordAsync(existing, token, password)).Succeeded.ShouldBeTrue();
            existing.SetShouldChangePasswordOnNextLogin(false);
            (await _userManager.UpdateAsync(existing)).Succeeded.ShouldBeTrue();
            return existing;
        }

        var admin = new IdentityUser(
            Guid.NewGuid(), CaseEvaluationConsts.AdminUserName, "a1b2c3d4@e5f6a7b8.com");
        (await _userManager.CreateAsync(admin, password)).Succeeded.ShouldBeTrue();
        return admin;
    }

    /// <summary>
    /// The whole point: an account on the framework's published password ends the pass on the
    /// stored one, and the published password no longer works.
    /// </summary>
    [Fact]
    public async Task AnAdminOnAPublishedDefault_IsMovedOntoTheStoredPassword()
    {
        await GivenAnAdminWithPasswordAsync(CaseEvaluationConsts.AdminPasswordDefaultValue);

        var rotated = await Rotator(isDevelopment: false)
            .RotateIfOnAKnownDefaultAsync(null, "the host database");

        rotated.ShouldBeTrue();

        var admin = await _userManager.FindByNameAsync(CaseEvaluationConsts.AdminUserName);
        admin.ShouldNotBeNull();
        (await _userManager.CheckPasswordAsync(admin, CaseEvaluationConsts.AdminPasswordDefaultValue))
            .ShouldBeFalse("the published default must no longer authenticate");
        (await _userManager.CheckPasswordAsync(admin, StoredPassword))
            .ShouldBeTrue("the stored password is what the operator will hand over");
    }

    /// <summary>
    /// The handover credential is known to whoever read it out of the store, so it is not allowed to
    /// become the account's lasting password.
    /// </summary>
    [Fact]
    public async Task ARotatedAdmin_MustChangeItsPasswordAtNextSignIn()
    {
        await GivenAnAdminWithPasswordAsync(CaseEvaluationConsts.AdminPasswordDefaultValue);

        await Rotator(isDevelopment: false).RotateIfOnAKnownDefaultAsync(null, "the host database");

        var admin = await _userManager.FindByNameAsync(CaseEvaluationConsts.AdminUserName);
        admin!.ShouldChangePasswordOnNextLogin.ShouldBeTrue();
    }

    /// <summary>
    /// This repository's own seeded password is a published default too -- it is in a public
    /// repository -- so it must rotate on exactly the same terms as the framework's.
    /// </summary>
    [Fact]
    public async Task AnAdminOnThisRepositorysSeededPassword_IsAlsoRotated()
    {
        await GivenAnAdminWithPasswordAsync(InternalUsersDataSeedContributor.DefaultPassword);

        var rotated = await Rotator(isDevelopment: false)
            .RotateIfOnAKnownDefaultAsync(null, "the host database");

        rotated.ShouldBeTrue();
    }

    /// <summary>
    /// THE control, and the one that would do real damage if it were wrong. An installation that has
    /// already chosen its own password must be left completely alone -- asserted on the stored hash
    /// itself, because "the password still works" would also pass if the hash had been rewritten
    /// with a new salt.
    /// </summary>
    [Fact]
    public async Task AnAdminOnItsOwnPassword_IsLeftByteIdentical()
    {
        var before = await GivenAnAdminWithPasswordAsync(ARealPassword);
        var hashBefore = before.PasswordHash;
        var forceChangeBefore = before.ShouldChangePasswordOnNextLogin;

        var rotated = await Rotator(isDevelopment: false)
            .RotateIfOnAKnownDefaultAsync(null, "the host database");

        rotated.ShouldBeFalse();

        var after = await _userManager.FindByNameAsync(CaseEvaluationConsts.AdminUserName);
        after!.PasswordHash.ShouldBe(hashBefore);
        after.ShouldChangePasswordOnNextLogin.ShouldBe(forceChangeBefore);
    }

    /// <summary>
    /// Development is untouched, so a local clone keeps the documented credentials and every
    /// runbook, onboarding page and docker guide stays correct.
    /// </summary>
    [Fact]
    public async Task InDevelopment_NothingIsRotated()
    {
        var before = await GivenAnAdminWithPasswordAsync(CaseEvaluationConsts.AdminPasswordDefaultValue);
        var hashBefore = before.PasswordHash;

        var rotated = await Rotator(isDevelopment: true)
            .RotateIfOnAKnownDefaultAsync(null, "the host database");

        rotated.ShouldBeFalse();

        var after = await _userManager.FindByNameAsync(CaseEvaluationConsts.AdminUserName);
        after!.PasswordHash.ShouldBe(hashBefore);
    }

    private sealed class FixedAdminPasswordStore : IAdminPasswordStore
    {
        private readonly string _password;

        public FixedAdminPasswordStore(string password)
        {
            _password = password;
        }

        public Task<string> GetOrCreateAsync(Guid? tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_password);
        }
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
