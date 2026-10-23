using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Pages.Account;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
/// <para>The test module's "Default" token provider validates only a token it issued (#1261), so a
/// token this test never issued is refused exactly as an expired or tampered link would be, and a
/// token it did issue is accepted. Users are created per test in office A, with synthetic
/// addresses.</para>
/// <para>2026-10-23: the GET handler used to confirm. A mail scanner's HEAD (answered by the GET
/// handler) confirmed an account before its owner clicked. GET now only looks; the POST behind the
/// page's button confirms. The first test below is the regression guard for exactly that.</para>
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

    private async Task<IdentityUser> NewUnconfirmedUserAsync()
    {
        var email = $"confirm-{Guid.NewGuid():N}@example.test";
        var user = new IdentityUser(Guid.NewGuid(), email, email, TenantsTestData.TenantARef);
        (await _userManager.CreateAsync(user, IdentityUsersTestData.SeedPassword)).Succeeded.ShouldBeTrue();
        return user;
    }

    [Fact]
    public async Task Opening_a_usable_link_shows_the_button_and_confirms_nothing_until_it_is_pressed()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var userId = Guid.Empty;
            var token = string.Empty;
            await WithUnitOfWorkAsync(async () =>
            {
                var user = await NewUnconfirmedUserAsync();
                userId = user.Id;
                token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            });

            // A scanner opening the link twice (HEAD then GET -- Razor Pages answers both with the GET
            // handler) leaves the account exactly as it was.
            for (var i = 0; i < 2; i++)
            {
                await WithUnitOfWorkAsync(async () =>
                {
                    var model = new EmailConfirmationModel(_userManager, new RecordingLogger())
                    {
                        UserId = userId,
                        ConfirmationToken = token,
                    };
                    (await model.OnGetAsync()).ShouldBeOfType<PageResult>();
                });
            }
            (await WithUnitOfWorkAsync(() => _userManager.GetByIdAsync(userId))).EmailConfirmed.ShouldBeFalse();

            LocalRedirectResult? pressed = null;
            await WithUnitOfWorkAsync(async () =>
            {
                var model = new EmailConfirmationModel(_userManager, new RecordingLogger())
                {
                    UserId = userId,
                    ConfirmationToken = token,
                };
                pressed = (await model.OnPostAsync()).ShouldBeOfType<LocalRedirectResult>();
            });

            pressed!.Url.ShouldBe("~/Account/Login?flash=email-verified");
            (await WithUnitOfWorkAsync(() => _userManager.GetByIdAsync(userId))).EmailConfirmed.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Opening_a_refused_link_goes_to_sign_in_as_invalid_without_a_warning()
    {
        var logger = new RecordingLogger();
        LocalRedirectResult? result = null;

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var user = await NewUnconfirmedUserAsync();
                var model = new EmailConfirmationModel(_userManager, logger)
                {
                    UserId = user.Id,
                    ConfirmationToken = "TEST-refused-token",
                };
                result = (await model.OnGetAsync()).ShouldBeOfType<LocalRedirectResult>();
            });
        }

        result!.Url.ShouldBe("~/Account/Login?flash=verification-invalid");
        logger.Entries.ShouldBeEmpty();
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
                var user = await NewUnconfirmedUserAsync();
                var model = new EmailConfirmationModel(_userManager, logger)
                {
                    UserId = user.Id,
                    ConfirmationToken = "TEST-refused-token",
                };
                result = (await model.OnPostAsync()).ShouldBeOfType<LocalRedirectResult>();
            });
        }

        result!.Url.ShouldBe("~/Account/Login?flash=verification-invalid");
        var warning = logger.Entries.ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain("InvalidToken");
        warning.Message.ShouldNotContain(nameof(IdentityError));
    }

    [Fact]
    public async Task An_already_confirmed_account_and_an_unknown_one_are_sent_to_sign_in_from_the_link()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var user = await NewUnconfirmedUserAsync();
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                (await _userManager.ConfirmEmailAsync(user, token)).Succeeded.ShouldBeTrue();

                var confirmed = new EmailConfirmationModel(_userManager, new RecordingLogger())
                {
                    UserId = user.Id,
                    ConfirmationToken = token,
                };
                (await confirmed.OnGetAsync()).ShouldBeOfType<LocalRedirectResult>()
                    .Url.ShouldBe("~/Account/Login?flash=email-verified");
                (await confirmed.OnPostAsync()).ShouldBeOfType<LocalRedirectResult>()
                    .Url.ShouldBe("~/Account/Login?flash=email-verified");

                var unknown = new EmailConfirmationModel(_userManager, new RecordingLogger())
                {
                    UserId = Guid.NewGuid(),
                    ConfirmationToken = token,
                };
                (await unknown.OnGetAsync()).ShouldBeOfType<LocalRedirectResult>()
                    .Url.ShouldBe("~/Account/Login?flash=verification-invalid");
                (await unknown.OnPostAsync()).ShouldBeOfType<LocalRedirectResult>()
                    .Url.ShouldBe("~/Account/Login?flash=verification-invalid");
            });
        }
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
