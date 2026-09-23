using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// The failure branches of <see cref="ExternalAccountAppService"/>'s anonymous password-reset
/// and email-verification endpoints: a mail send that fails, a rate-limit store that fails, a
/// used-up allowance, a bad reset token, and a host account that may not be mailed.
/// </summary>
/// <remarks>
/// <para>
/// <c>EfCoreExternalAccountAppServiceTests</c> covers the successful paths with the real
/// dispatcher, where a failed send can only be simulated by a missing template. Two collaborators
/// are replaced for THIS class only, in <c>AfterAddApplication</c>:
/// <list type="bullet">
///   <item>the notification dispatcher, so a send can fail with an ordinary exception and every
///   send is asserted;</item>
///   <item>the distributed cache the rate limiter uses. It is a real in-memory cache that can be
///   told to fail for the rate limiter's own keys only. The framework's caches share this service,
///   so failing every key would break unrelated lookups.</item>
/// </list>
/// </para>
/// <para>
/// The test module's "Default" token provider accepts ANY token, so no reset could be refused
/// for a bad token there. This class registers <see cref="IssuedTokensOnlyProvider"/> in its place,
/// which accepts only a token it issued for that user and purpose.
/// </para>
/// <para>All users are created per test in office A, with synthetic names and emails.</para>
/// </remarks>
public abstract class ExternalAccountFailurePathTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string ResetPrefix = "password-reset";
    private const string ResendPrefix = "resend-verify";

    private INotificationDispatcher _dispatcher = null!;
    private FaultInjectingCache _cache = null!;

    private readonly IExternalAccountAppService _account;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;

    protected ExternalAccountFailurePathTests()
    {
        _account = GetRequiredService<IExternalAccountAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _dispatcher = Substitute.For<INotificationDispatcher>();
        _cache = new FaultInjectingCache();
        services.Replace(ServiceDescriptor.Singleton(typeof(INotificationDispatcher), _dispatcher));
        services.Replace(ServiceDescriptor.Singleton(typeof(IDistributedCache), _cache));
        services.AddTransient<IssuedTokensOnlyProvider>();
        services.Configure<IdentityOptions>(options =>
            options.Tokens.ProviderMap["Default"] = new TokenProviderDescriptor(typeof(IssuedTokensOnlyProvider)));
    }

    // ------------------------------------------------------------------ harness

    private static string NewEmail() => $"acct-{Guid.NewGuid():N}@example.test";

    private async Task<(Guid Id, string Email)> CreateUserAsync(Guid? tenantId, bool confirmEmail)
    {
        var email = NewEmail();
        var user = new IdentityUser(Guid.NewGuid(), email, email, tenantId) { Name = "Synthetic" };
        using (_currentTenant.Change(tenantId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                (await _userManager.CreateAsync(user, IdentityUsersTestData.SeedPassword)).Succeeded.ShouldBeTrue();
                if (confirmEmail)
                {
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    (await _userManager.ConfirmEmailAsync(user, token)).Succeeded.ShouldBeTrue();
                }
            });
        }
        return (user.Id, email);
    }

    private async Task InOfficeA(Func<Task> call)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await WithUnitOfWorkAsync(call);
        }
    }

    private void FailSending(string templateCode) =>
        _dispatcher.DispatchAsync(
                templateCode, Arg.Any<IReadOnlyCollection<NotificationRecipient>>(),
                Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<string>(),
                Arg.Any<PacketAttachmentRef?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Synthetic mail outage"));

    private List<object?[]> Sends(string templateCode) =>
        _dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchAsync))
            .Select(c => c.GetArguments())
            .Where(args => (string)args[0]! == templateCode)
            .ToList();

    private static string Key(string prefix, string kind, string email) =>
        $"{prefix}:{kind}:{email.ToLowerInvariant()}";

    private async Task<(Guid Id, string Email, string Token)> UserWithResetTokenAsync()
    {
        var (id, email) = await CreateUserAsync(TenantsTestData.TenantARef, confirmEmail: true);
        string token = null!;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            token = await WithUnitOfWorkAsync(async () =>
                await _userManager.GeneratePasswordResetTokenAsync(await _userManager.GetByIdAsync(id)));
        }
        return (id, email, token);
    }

    // ------------------------------------------------------------------ reset link request

    [Fact]
    public async Task A_failed_reset_email_still_answers_success_and_the_request_is_counted()
    {
        var (_, email) = await CreateUserAsync(TenantsTestData.TenantARef, confirmEmail: true);
        FailSending(NotificationTemplateConsts.Codes.ResetPassword);

        await InOfficeA(() => _account.SendPasswordResetCodeAsync(new SendPasswordResetCodeInput { Email = email }));

        Sends(NotificationTemplateConsts.Codes.ResetPassword).Count.ShouldBe(1);
        (await _cache.GetStringAsync(Key(ResetPrefix, "cooldown", email))).ShouldBe("1");
        (await _cache.GetStringAsync(Key(ResetPrefix, "hourly", email))).ShouldBe("1");
    }

    [Fact]
    public async Task A_return_address_is_carried_on_the_reset_link()
    {
        var (_, email) = await CreateUserAsync(TenantsTestData.TenantARef, confirmEmail: true);

        await InOfficeA(() => _account.SendPasswordResetCodeAsync(
            new SendPasswordResetCodeInput { Email = email, ReturnUrl = "/appointments?tab=mine" }));

        var variables = (IReadOnlyDictionary<string, object?>)Sends(NotificationTemplateConsts.Codes.ResetPassword)
            .ShouldHaveSingleItem()[2]!;
        ((string)variables["URL"]!).ShouldEndWith("&returnUrl=" + WebUtility.UrlEncode("/appointments?tab=mine"));
    }

    [Fact]
    public async Task A_used_up_hourly_allowance_throttles_a_reset_request_before_anything_is_sent()
    {
        var (_, email) = await CreateUserAsync(TenantsTestData.TenantARef, confirmEmail: true);
        await _cache.SetStringAsync(Key(ResetPrefix, "hourly", email), "10");

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            InOfficeA(() => _account.SendPasswordResetCodeAsync(new SendPasswordResetCodeInput { Email = email })));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.PasswordResetThrottled);
        Sends(NotificationTemplateConsts.Codes.ResetPassword).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_rate_limit_store_that_cannot_be_read_or_written_lets_the_request_through()
    {
        var (_, email) = await CreateUserAsync(TenantsTestData.TenantARef, confirmEmail: true);
        _cache.FailsFor = key => key.StartsWith(ResetPrefix + ":", StringComparison.Ordinal);

        await InOfficeA(() => _account.SendPasswordResetCodeAsync(new SendPasswordResetCodeInput { Email = email }));

        Sends(NotificationTemplateConsts.Codes.ResetPassword).Count.ShouldBe(1);
        _cache.FailuresRaised.ShouldBeGreaterThanOrEqualTo(2);
    }

    // ------------------------------------------------------------------ completing a reset

    [Fact]
    public async Task A_reset_carrying_a_token_that_was_never_issued_is_refused_as_invalid()
    {
        var (id, _) = await CreateUserAsync(TenantsTestData.TenantARef, confirmEmail: true);

        var refused = await Should.ThrowAsync<BusinessException>(() => InOfficeA(() => _account.ResetPasswordAsync(
            new ResetPasswordInput { UserId = id, ResetToken = "synthetic-not-a-token", Password = "Synthetic-Pass2!", ConfirmPassword = "Synthetic-Pass2!" })));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ResetPasswordTokenInvalid);
    }

    [Fact]
    public async Task A_reset_to_a_password_the_rules_refuse_reports_the_rule_and_keeps_the_old_password()
    {
        var (id, _, token) = await UserWithResetTokenAsync();

        var refused = await Should.ThrowAsync<UserFriendlyException>(() => InOfficeA(() => _account.ResetPasswordAsync(
            new ResetPasswordInput { UserId = id, ResetToken = token, Password = "synthetic-lower-only", ConfirmPassword = "synthetic-lower-only" })));

        refused.Message.ShouldNotBeNullOrWhiteSpace();
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            (await WithUnitOfWorkAsync(async () =>
                await _userManager.CheckPasswordAsync(await _userManager.GetByIdAsync(id), IdentityUsersTestData.SeedPassword)))
                .ShouldBeTrue();
        }
    }

    [Fact]
    public async Task A_failed_confirmation_after_a_reset_does_not_undo_the_reset()
    {
        var (id, _, token) = await UserWithResetTokenAsync();
        FailSending(NotificationTemplateConsts.Codes.PasswordChange);

        await InOfficeA(() => _account.ResetPasswordAsync(
            new ResetPasswordInput { UserId = id, ResetToken = token, Password = "Synthetic-Pass2!", ConfirmPassword = "Synthetic-Pass2!" }));

        Sends(NotificationTemplateConsts.Codes.PasswordChange).Count.ShouldBe(1);
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            (await WithUnitOfWorkAsync(async () =>
                await _userManager.CheckPasswordAsync(await _userManager.GetByIdAsync(id), "Synthetic-Pass2!")))
                .ShouldBeTrue();
        }
    }

    // ------------------------------------------------------------------ verification resend

    [Fact]
    public async Task A_verification_resend_inside_the_cooldown_is_dropped_without_sending()
    {
        var (_, email) = await CreateUserAsync(TenantsTestData.TenantARef, confirmEmail: false);
        await _cache.SetStringAsync(Key(ResendPrefix, "cooldown", email), "1");

        await InOfficeA(() => _account.ResendEmailVerificationAsync(new ResendEmailVerificationInput { Email = email }));

        Sends(NotificationTemplateConsts.Codes.UserRegistered).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_verification_resend_for_an_address_nobody_holds_sends_nothing()
    {
        await InOfficeA(() => _account.ResendEmailVerificationAsync(new ResendEmailVerificationInput { Email = NewEmail() }));

        _dispatcher.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_host_account_with_no_internal_role_is_not_sent_a_verification()
    {
        var (_, email) = await CreateUserAsync(tenantId: null, confirmEmail: false);

        using (_currentTenant.Change(null))
        {
            await WithUnitOfWorkAsync(() => _account.ResendEmailVerificationAsync(new ResendEmailVerificationInput { Email = email }));
        }

        Sends(NotificationTemplateConsts.Codes.UserRegistered).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_verification_email_still_answers_success()
    {
        var (_, email) = await CreateUserAsync(TenantsTestData.TenantARef, confirmEmail: false);
        FailSending(NotificationTemplateConsts.Codes.UserRegistered);

        await InOfficeA(() => _account.ResendEmailVerificationAsync(new ResendEmailVerificationInput { Email = email }));

        Sends(NotificationTemplateConsts.Codes.UserRegistered).Count.ShouldBe(1);
        (await _cache.GetStringAsync(Key(ResendPrefix, "cooldown", email))).ShouldBe("1");
    }

    /// <summary>
    /// A token provider that validates only the token it generated for the same user and purpose.
    /// </summary>
    private sealed class IssuedTokensOnlyProvider : IUserTwoFactorTokenProvider<IdentityUser>
    {
        private static string Issue(string purpose, IdentityUser user) => $"issued:{purpose}:{user.Id:N}";

        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<IdentityUser> manager, IdentityUser user) =>
            Task.FromResult(false);

        public Task<string> GenerateAsync(string purpose, UserManager<IdentityUser> manager, IdentityUser user) =>
            Task.FromResult(Issue(purpose, user));

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<IdentityUser> manager, IdentityUser user) =>
            Task.FromResult(token == Issue(purpose, user));
    }

    /// <summary>
    /// A real in-memory distributed cache that throws for the keys <see cref="FailsFor"/> selects,
    /// and counts how often it did.
    /// </summary>
    private sealed class FaultInjectingCache : IDistributedCache
    {
        private readonly MemoryDistributedCache _inner =
            new(Options.Create(new MemoryDistributedCacheOptions()));

        public Func<string, bool> FailsFor { get; set; } = _ => false;

        public int FailuresRaised { get; private set; }

        private void ThrowIfSelected(string key)
        {
            if (FailsFor(key))
            {
                FailuresRaised++;
                throw new InvalidOperationException("Synthetic cache outage");
            }
        }

        public byte[]? Get(string key)
        {
            ThrowIfSelected(key);
            return _inner.Get(key);
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            ThrowIfSelected(key);
            return _inner.GetAsync(key, token);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            ThrowIfSelected(key);
            _inner.Set(key, value, options);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            ThrowIfSelected(key);
            return _inner.SetAsync(key, value, options, token);
        }

        public void Refresh(string key) => _inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => _inner.RefreshAsync(key, token);

        public void Remove(string key) => _inner.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) => _inner.RemoveAsync(key, token);
    }
}
