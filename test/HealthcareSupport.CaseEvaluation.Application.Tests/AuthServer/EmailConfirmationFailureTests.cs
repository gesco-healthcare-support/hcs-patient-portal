using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Pages.Account;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace HealthcareSupport.CaseEvaluation.AuthServer.Tests;

/// <summary>
/// Email confirmation PAST the user lookup, with ABP's real <see cref="IdentityUserManager"/>: the
/// path <c>AccountPageModelTests</c> records it cannot reach with a substitute.
/// </summary>
/// <remarks>
/// The test module's "Default" token provider accepts ANY token, so a confirmation could never fail
/// there. This class replaces it with <see cref="RefuseAllTokensProvider"/>, so the token the test
/// submits is refused exactly as an expired or tampered link would be, and the page takes its
/// failure branch. The user is created per test in office A, with a synthetic address.
/// </remarks>
public abstract class EmailConfirmationFailureTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;

    protected EmailConfirmationFailureTests()
    {
        _userManager = GetRequiredService<IdentityUserManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.AddTransient<RefuseAllTokensProvider>();
        services.Configure<IdentityOptions>(options =>
            options.Tokens.ProviderMap["Default"] = new TokenProviderDescriptor(typeof(RefuseAllTokensProvider)));
    }

    [Fact]
    public async Task A_refused_confirmation_logs_the_identity_error_code_not_its_type_name()
    {
        var logger = new RecordingLogger();
        LocalRedirectResult? result = null;

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var email = $"confirm-{Guid.NewGuid():N}@example.test";
                var user = new IdentityUser(Guid.NewGuid(), email, email, TenantsTestData.TenantARef);
                (await _userManager.CreateAsync(user, IdentityUsersTestData.SeedPassword)).Succeeded.ShouldBeTrue();

                var model = new EmailConfirmationModel(_userManager, logger)
                {
                    UserId = user.Id,
                    ConfirmationToken = "TEST-refused-token",
                };
                result = (await model.OnGetAsync()).ShouldBeOfType<LocalRedirectResult>();
            });
        }

        result!.Url.ShouldBe("~/Account/Login?flash=verification-invalid");
        var warning = logger.Entries.ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain("InvalidToken");
        warning.Message.ShouldNotContain(nameof(IdentityError));
    }

    /// <summary>Refuses every token, so a confirmation always fails with <c>InvalidToken</c>.</summary>
    private sealed class RefuseAllTokensProvider : IUserTwoFactorTokenProvider<IdentityUser>
    {
        public Task<string> GenerateAsync(string purpose, UserManager<IdentityUser> manager, IdentityUser user) =>
            Task.FromResult("TEST-unused");

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<IdentityUser> manager, IdentityUser user) =>
            Task.FromResult(false);

        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<IdentityUser> manager, IdentityUser user) =>
            Task.FromResult(false);
    }

    /// <summary>Keeps every warning-or-above entry with its formatted message.</summary>
    private sealed class RecordingLogger : ILogger<EmailConfirmationModel>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
