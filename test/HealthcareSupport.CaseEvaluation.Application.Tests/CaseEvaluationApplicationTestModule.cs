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
        // The default ABP test harness does not register IHostEnvironment, and
        // services resolved under test take it in their constructors (for example
        // AdminPasswordRotator and CaseEvaluationTenantDatabaseMigrationHandler).
        // Register a stub so they can be instantiated. Production composition is
        // unchanged.
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
/// <remarks>
/// It validates ONLY a token it generated for the same user, the same purpose and the user's
/// CURRENT security stamp, as the real DataProtector provider does. It used to return true for
/// anything, so under this harness a garbage, blank, tampered, wrong-user, wrong-purpose or
/// already-consumed token (consuming rotates the stamp) all validated, and no test could show a
/// token being refused (#1261).
/// </remarks>
public class NoOpTwoFactorTokenProvider : IUserTwoFactorTokenProvider<Volo.Abp.Identity.IdentityUser>
{
    public Task<bool> CanGenerateTwoFactorTokenAsync(
        UserManager<Volo.Abp.Identity.IdentityUser> manager, Volo.Abp.Identity.IdentityUser user)
        => Task.FromResult(false);

    public async Task<string> GenerateAsync(
        string purpose, UserManager<Volo.Abp.Identity.IdentityUser> manager, Volo.Abp.Identity.IdentityUser user)
        => await IssueAsync(purpose, manager, user);

    public async Task<bool> ValidateAsync(
        string purpose, string token, UserManager<Volo.Abp.Identity.IdentityUser> manager, Volo.Abp.Identity.IdentityUser user)
        => !string.IsNullOrEmpty(token) && string.Equals(token, await IssueAsync(purpose, manager, user), System.StringComparison.Ordinal);

    private static async Task<string> IssueAsync(
        string purpose, UserManager<Volo.Abp.Identity.IdentityUser> manager, Volo.Abp.Identity.IdentityUser user)
        => $"test-token:{purpose}:{user.Id:N}:{await manager.GetSecurityStampAsync(user)}";
}
