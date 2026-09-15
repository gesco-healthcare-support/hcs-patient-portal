using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Volo.Abp.Modularity;

namespace HealthcareSupport.CaseEvaluation;

[DependsOn(
    typeof(CaseEvaluationApplicationModule),
    typeof(CaseEvaluationDomainTestModule)
)]
public class CaseEvaluationApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // BUG-012 Sub-bug 1 (2026-05-22): ExternalSignupAppService's
        // constructor takes IHostEnvironment (used to gate dev-only
        // MarkEmailConfirmed / DeleteTestUsers helpers). The default
        // ABP test harness does not register it, so resolving the
        // AppService via DI fails with "Cannot resolve parameter
        // IHostEnvironment". Register a stub so the AppService can be
        // instantiated under test. Production composition is unchanged.
        context.Services.AddSingleton<IHostEnvironment>(
            new HostingEnvironment
            {
                EnvironmentName = Environments.Development,
                ApplicationName = "CaseEvaluationTests",
                ContentRootPath = System.IO.Directory.GetCurrentDirectory(),
            });

        // F-H01 (2026-06-25): the full RegisterAsync path generates an
        // email-confirmation token via UserManager.GenerateUserTokenAsync(user,
        // "Default", ...). The default test harness registers no Identity token
        // provider, so any registration-completing test throws "No
        // IUserTwoFactorTokenProvider named 'Default' is registered". Register a
        // no-op "Default" provider so register-after-booking tests can exercise
        // the adopt path end to end. The token value is irrelevant to these tests.
        context.Services.AddTransient<NoOpTwoFactorTokenProvider>();
        context.Services.Configure<IdentityOptions>(options =>
        {
            options.Tokens.ProviderMap["Default"] =
                new TokenProviderDescriptor(typeof(NoOpTwoFactorTokenProvider));
        });
    }
}

/// <summary>
/// Test-only stand-in for the Identity "Default" token provider. Lets
/// <c>UserManager.GenerateUserTokenAsync</c> succeed under the unit-test harness
/// (which wires no real DataProtector token provider). Not used in production.
/// </summary>
public class NoOpTwoFactorTokenProvider : IUserTwoFactorTokenProvider<Volo.Abp.Identity.IdentityUser>
{
    public Task<bool> CanGenerateTwoFactorTokenAsync(
        UserManager<Volo.Abp.Identity.IdentityUser> manager, Volo.Abp.Identity.IdentityUser user)
        => Task.FromResult(false);

    public Task<string> GenerateAsync(
        string purpose, UserManager<Volo.Abp.Identity.IdentityUser> manager, Volo.Abp.Identity.IdentityUser user)
        => Task.FromResult("test-token");

    public Task<bool> ValidateAsync(
        string purpose, string token, UserManager<Volo.Abp.Identity.IdentityUser> manager, Volo.Abp.Identity.IdentityUser user)
        => Task.FromResult(true);
}
