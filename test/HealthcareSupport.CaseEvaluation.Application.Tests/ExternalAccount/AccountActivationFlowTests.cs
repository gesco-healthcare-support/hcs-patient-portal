using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// The activate-your-account flow end to end below the page: the link the sender issues, the
/// checker the page asks on GET, and the activation the POST runs -- with the REAL data-protection
/// token provider, because what matters here is which tokens are REFUSED (used, garbled, issued for
/// another purpose), and a provider that accepts anything can never show a refusal.
/// </summary>
/// <remarks>
/// <para>The dispatcher is substituted so the email is asserted rather than rendered. Users are
/// created per test with synthetic names and addresses; most are host users (no office), as an
/// account made on the host identity screen would be, and the office-admin tests use office A.</para>
/// </remarks>
public abstract class AccountActivationFlowTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string TypedByCreator = "Typed-By-Creator1!";
    private const string ChosenByOwner = "Chosen-By-Owner2!";

    private INotificationDispatcher _dispatcher = null!;

    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAccountActivationSender _sender;
    private readonly IAccountActivationLinkChecker _checker;
    private readonly IAccountActivationService _activation;

    protected AccountActivationFlowTests()
    {
        _userManager = GetRequiredService<IdentityUserManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _sender = GetRequiredService<IAccountActivationSender>();
        _checker = GetRequiredService<IAccountActivationLinkChecker>();
        _activation = GetRequiredService<IAccountActivationService>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _dispatcher = Substitute.For<INotificationDispatcher>();
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_dispatcher));

        // The real provider production uses (see AccountSelfServiceFlowTests for why).
        services.AddDataProtection();
        services.AddTransient<Microsoft.AspNetCore.Identity.DataProtectorTokenProvider<IdentityUser>>();
        services.Configure<Microsoft.AspNetCore.Identity.IdentityOptions>(options =>
            options.Tokens.ProviderMap["Default"] = new Microsoft.AspNetCore.Identity.TokenProviderDescriptor(
                typeof(Microsoft.AspNetCore.Identity.DataProtectorTokenProvider<IdentityUser>)));
    }

    // ------------------------------------------------------------------ harness

    /// <summary>An account as somebody else creates it: unconfirmed, a password its owner never saw.</summary>
    private async Task<Guid> CreateUnconfirmedHostUserAsync(bool mustChangePassword = true)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var user = new IdentityUser(Guid.NewGuid(), $"act-{token}", $"act-{token}@example.test", tenantId: null)
        {
            Name = "Synthetic",
        };
        await WithUnitOfWorkAsync(async () =>
        {
            (await _userManager.CreateAsync(user, TypedByCreator)).Succeeded.ShouldBeTrue();
            (await _userManager.SetLockoutEnabledAsync(user, true)).Succeeded.ShouldBeTrue();
            user.SetShouldChangePasswordOnNextLogin(mustChangePassword);
            (await _userManager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
        });
        return user.Id;
    }

    private Task<IdentityUser> ReloadAsync(Guid userId) =>
        WithUnitOfWorkAsync(() => _userManager.GetByIdAsync(userId));

    /// <summary>The token exactly as the email's link carries it, read back out of the URL.</summary>
    private async Task<string> IssueLinkTokenAsync(Guid userId)
    {
        var url = await WithUnitOfWorkAsync(async () => await _sender.CreateLinkAsync(await _userManager.GetByIdAsync(userId)));
        url.ShouldContain($"/Account/ActivateAccount?userId={userId}&token=");
        return HttpUtility.ParseQueryString(new Uri(url).Query)["token"]!;
    }

    private Task ActivateAsync(Guid userId, string token, string password = ChosenByOwner, string? confirm = null) =>
        WithUnitOfWorkAsync(() => _activation.ActivateAsync(new ActivateAccountInput
        {
            UserId = userId,
            Token = token,
            Password = password,
            ConfirmPassword = confirm ?? password,
        }));

    private Task<bool> UsableAsync(Guid userId, string? token) =>
        WithUnitOfWorkAsync(() => _checker.IsUsableAsync(userId, token));

    private List<object?[]> Sends() =>
        _dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchAsync))
            .Select(c => c.GetArguments())
            .ToList();

    // ------------------------------------------------------------------ the link

    [Fact]
    public async Task A_link_works_until_it_is_used_and_activating_sets_the_password_and_confirms_the_email()
    {
        var userId = await CreateUnconfirmedHostUserAsync();
        var token = await IssueLinkTokenAsync(userId);

        (await UsableAsync(userId, token)).ShouldBeTrue();
        // Looking is not using: a mail scanner's visit leaves the link working.
        (await UsableAsync(userId, token)).ShouldBeTrue();
        (await ReloadAsync(userId)).EmailConfirmed.ShouldBeFalse();

        await ActivateAsync(userId, token);

        var user = await ReloadAsync(userId);
        user.EmailConfirmed.ShouldBeTrue();
        user.ShouldChangePasswordOnNextLogin.ShouldBeFalse("they have just chosen it; asking again would make them choose twice");
        (await WithUnitOfWorkAsync(() => _userManager.CheckPasswordAsync(user, ChosenByOwner))).ShouldBeTrue();
        (await WithUnitOfWorkAsync(() => _userManager.CheckPasswordAsync(user, TypedByCreator)))
            .ShouldBeFalse("the password whoever created the account typed must not survive activation");

        (await UsableAsync(userId, token)).ShouldBeFalse();
        var second = await Should.ThrowAsync<BusinessException>(() => ActivateAsync(userId, token, "Another-Pass3!"));
        second.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ActivationLinkInvalid);
    }

    [Fact]
    public async Task An_activation_link_cannot_reset_a_password_and_a_reset_link_cannot_activate()
    {
        var userId = await CreateUnconfirmedHostUserAsync();
        var activationToken = await IssueLinkTokenAsync(userId);
        var resetToken = await WithUnitOfWorkAsync(async () =>
            await _userManager.GeneratePasswordResetTokenAsync(await _userManager.GetByIdAsync(userId)));

        (await UsableAsync(userId, resetToken)).ShouldBeFalse();
        (await Should.ThrowAsync<BusinessException>(() => ActivateAsync(userId, resetToken)))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.ActivationLinkInvalid);

        var resetChecker = GetRequiredService<IResetLinkChecker>();
        (await WithUnitOfWorkAsync(() => resetChecker.IsUsableAsync(userId, activationToken))).ShouldBeFalse();

        (await ReloadAsync(userId)).EmailConfirmed.ShouldBeFalse();
    }

    [Fact]
    public async Task A_password_the_rules_refuse_changes_nothing_and_the_link_still_works()
    {
        var userId = await CreateUnconfirmedHostUserAsync();
        var token = await IssueLinkTokenAsync(userId);

        await Should.ThrowAsync<UserFriendlyException>(() => ActivateAsync(userId, token, "alllowercase"));

        var user = await ReloadAsync(userId);
        user.EmailConfirmed.ShouldBeFalse();
        (await WithUnitOfWorkAsync(() => _userManager.CheckPasswordAsync(user, TypedByCreator))).ShouldBeTrue();
        (await UsableAsync(userId, token)).ShouldBeTrue();

        await ActivateAsync(userId, token);
        (await ReloadAsync(userId)).EmailConfirmed.ShouldBeTrue();
    }

    [Fact]
    public async Task Two_different_entries_are_refused_before_anything_changes()
    {
        var userId = await CreateUnconfirmedHostUserAsync();
        var token = await IssueLinkTokenAsync(userId);

        await Should.ThrowAsync<UserFriendlyException>(() => ActivateAsync(userId, token, ChosenByOwner, "Different-Pass9!"));

        (await ReloadAsync(userId)).EmailConfirmed.ShouldBeFalse();
        (await UsableAsync(userId, token)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_garbled_token_a_blank_token_and_an_unknown_user_are_all_dead_links()
    {
        var userId = await CreateUnconfirmedHostUserAsync();
        var token = await IssueLinkTokenAsync(userId);

        (await UsableAsync(userId, token + "x")).ShouldBeFalse();
        (await UsableAsync(userId, " ")).ShouldBeFalse();
        (await UsableAsync(Guid.NewGuid(), token)).ShouldBeFalse();
        (await UsableAsync(Guid.Empty, token)).ShouldBeFalse();

        foreach (var (id, t) in new[] { (userId, token + "x"), (userId, " "), (Guid.NewGuid(), token), (Guid.Empty, token) })
        {
            (await Should.ThrowAsync<BusinessException>(() => ActivateAsync(id, t)))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.ActivationLinkInvalid);
        }
        (await ReloadAsync(userId)).EmailConfirmed.ShouldBeFalse();
    }

    [Fact]
    public async Task A_deactivated_accounts_link_is_dead_and_cannot_set_its_password()
    {
        var userId = await CreateUnconfirmedHostUserAsync();
        var token = await IssueLinkTokenAsync(userId);
        await WithUnitOfWorkAsync(async () =>
        {
            var user = await _userManager.GetByIdAsync(userId);
            user.SetIsActive(false);
            (await _userManager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
        });

        (await UsableAsync(userId, token)).ShouldBeFalse();
        (await Should.ThrowAsync<BusinessException>(() => ActivateAsync(userId, token)))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.ActivationLinkInvalid);

        var after = await ReloadAsync(userId);
        after.EmailConfirmed.ShouldBeFalse();
        (await WithUnitOfWorkAsync(() => _userManager.CheckPasswordAsync(after, ChosenByOwner))).ShouldBeFalse();
    }

    [Fact]
    public async Task Activating_restores_an_account_that_was_locked_out()
    {
        var userId = await CreateUnconfirmedHostUserAsync();
        await WithUnitOfWorkAsync(async () =>
            (await _userManager.SetLockoutEndDateAsync(await _userManager.GetByIdAsync(userId), DateTimeOffset.UtcNow.AddHours(1)))
                .Succeeded.ShouldBeTrue());
        var token = await IssueLinkTokenAsync(userId);

        await ActivateAsync(userId, token);

        (await WithUnitOfWorkAsync(async () => await _userManager.IsLockedOutAsync(await _userManager.GetByIdAsync(userId))))
            .ShouldBeFalse();
    }

    // ------------------------------------------------------------------ the email

    [Fact]
    public async Task The_activation_email_goes_to_the_account_and_carries_a_link_that_works()
    {
        var userId = await CreateUnconfirmedHostUserAsync();
        var user = await ReloadAsync(userId);

        await WithUnitOfWorkAsync(() => _sender.SendAsync(user, "TEST-context"));

        var send = Sends().ShouldHaveSingleItem();
        ((string)send[0]!).ShouldBe(NotificationTemplateConsts.Codes.AccountActivation);
        ((IReadOnlyCollection<NotificationRecipient>)send[1]!).ShouldHaveSingleItem().Email.ShouldBe(user.Email);
        var variables = (IReadOnlyDictionary<string, object?>)send[2]!;
        variables["ExpiresInHours"].ShouldBe("24");
        var url = (string)variables["URL"]!;
        url.ShouldContain($"/Account/ActivateAccount?userId={userId}&token=");
        ((string)send[3]!).ShouldBe("TEST-context");

        var token = HttpUtility.ParseQueryString(new Uri(url).Query)["token"]!;
        (await UsableAsync(userId, token)).ShouldBeTrue("the emailed link itself must open the form");
    }

    [Fact]
    public async Task A_new_office_admin_is_sent_one_activation_email_greeted_by_the_doctors_name()
    {
        Guid adminId;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            adminId = await WithUnitOfWorkAsync(async () =>
            {
                var admin = new IdentityUser(Guid.NewGuid(), CaseEvaluationConsts.AdminUserName,
                    $"office-admin-{Guid.NewGuid():N}@example.test", TenantsTestData.TenantARef)
                {
                    Name = CaseEvaluationConsts.AdminUserName,
                };
                (await _userManager.CreateAsync(admin, TypedByCreator)).Succeeded.ShouldBeTrue();
                return admin.Id;
            });
        }

        (await _sender.TrySendToOfficeAdminAsync(TenantsTestData.TenantARef, "Ada"))
            .ShouldBe(OfficeAdminActivationOutcome.Sent);

        var send = Sends().ShouldHaveSingleItem();
        ((string)send[0]!).ShouldBe(NotificationTemplateConsts.Codes.AccountActivation);
        ((string)send[3]!).ShouldStartWith($"AccountActivation/OfficeAdmin/{adminId}/r");
        ((IReadOnlyDictionary<string, object?>)send[2]!)["PatientFirstName"].ShouldBe("Ada");
    }

    [Fact]
    public async Task An_office_with_no_administrator_is_sent_nothing_and_does_not_throw()
    {
        (await _sender.TrySendToOfficeAdminAsync(Guid.NewGuid(), "Ada"))
            .ShouldBe(OfficeAdminActivationOutcome.NoAdministrator);

        Sends().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, true, OfficeAdminActivationOutcome.AlreadyActive)]
    [InlineData(false, false, OfficeAdminActivationOutcome.Inactive)]
    public async Task An_office_admin_who_activated_or_is_switched_off_is_sent_nothing(
        bool confirmed, bool active, OfficeAdminActivationOutcome expected)
    {
        // The Practices screen's "Resend activation email" reports these instead of mailing: an
        // activated admin needs a password reset, and a switched-off admin's link would be dead.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var admin = new IdentityUser(Guid.NewGuid(), CaseEvaluationConsts.AdminUserName,
                    $"office-admin-{Guid.NewGuid():N}@example.test", TenantsTestData.TenantARef);
                admin.SetEmailConfirmed(confirmed);
                admin.SetIsActive(active);
                (await _userManager.CreateAsync(admin, TypedByCreator)).Succeeded.ShouldBeTrue();
            });
        }

        (await _sender.TrySendToOfficeAdminAsync(TenantsTestData.TenantARef, "Ada")).ShouldBe(expected);

        Sends().ShouldBeEmpty();
    }

    // 2026-10-23: the Practices screen's "Resend activation email" for an office's administrator.

    private IAccountActivationAppService Resend => GetRequiredService<IAccountActivationAppService>();

    private async Task<Guid> CreateOfficeAdminAsync(bool confirmed)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            return await WithUnitOfWorkAsync(async () =>
            {
                var admin = new IdentityUser(Guid.NewGuid(), CaseEvaluationConsts.AdminUserName,
                    $"office-admin-{Guid.NewGuid():N}@example.test", TenantsTestData.TenantARef);
                admin.SetEmailConfirmed(confirmed);
                (await _userManager.CreateAsync(admin, TypedByCreator)).Succeeded.ShouldBeTrue();
                return admin.Id;
            });
        }
    }

    private async Task ResendAsHostAsync(Guid officeId)
    {
        using (_currentTenant.Change(null))
        {
            await WithUnitOfWorkAsync(() => Resend.ResendToOfficeAdministratorAsync(officeId));
        }
    }

    [Fact]
    public async Task The_practices_screen_resends_one_activation_email_to_the_office_admin()
    {
        var adminId = await CreateOfficeAdminAsync(confirmed: false);

        await ResendAsHostAsync(TenantsTestData.TenantARef);

        var send = Sends().ShouldHaveSingleItem();
        ((string)send[0]!).ShouldBe(NotificationTemplateConsts.Codes.AccountActivation);
        ((string)send[3]!).ShouldStartWith($"AccountActivation/OfficeAdmin/{adminId}");
    }

    [Fact]
    public async Task The_practices_screen_resend_pressed_again_within_a_minute_is_refused()
    {
        await CreateOfficeAdminAsync(confirmed: false);
        await ResendAsHostAsync(TenantsTestData.TenantARef);

        var ex = await Should.ThrowAsync<BusinessException>(() => ResendAsHostAsync(TenantsTestData.TenantARef));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ActivationResendTooSoon);
        Sends().Count.ShouldBe(1);
    }

    [Fact]
    public async Task The_practices_screen_resend_reports_an_office_admin_who_already_activated()
    {
        await CreateOfficeAdminAsync(confirmed: true);

        var ex = await Should.ThrowAsync<BusinessException>(() => ResendAsHostAsync(TenantsTestData.TenantARef));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ActivationAlreadyActive);
        Sends().ShouldBeEmpty();
    }

    [Fact]
    public async Task The_practices_screen_resend_reports_an_office_with_no_administrator()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => ResendAsHostAsync(TenantsTestData.TenantARef));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ActivationNoOfficeAdministrator);
        Sends().ShouldBeEmpty();
    }

    [Fact]
    public async Task The_practices_screen_resend_refuses_an_unknown_office()
    {
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(() => ResendAsHostAsync(Guid.NewGuid()));
        Sends().ShouldBeEmpty();
    }

    [Fact]
    public async Task The_office_admin_resend_is_refused_from_inside_an_office()
    {
        // Practices are managed from the host; inside an office this is not an office's business.
        await CreateOfficeAdminAsync(confirmed: false);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(
                () => WithUnitOfWorkAsync(() => Resend.ResendToOfficeAdministratorAsync(TenantsTestData.TenantARef)));
        }

        Sends().ShouldBeEmpty();
    }
}

/// <summary>The email's tokens, built without a host.</summary>
public class AccountActivationEmailVariablesTests
{
    private static IdentityUser User(string userName, string? name, string? surname = null) =>
        new(Guid.NewGuid(), userName, $"{userName}@example.test", tenantId: null) { Name = name, Surname = surname };

    [Fact]
    public void A_caller_supplied_name_wins_over_the_accounts_own()
    {
        var vars = AccountActivationSender.BuildVariables(User("admin", "admin"), "https://x/a", "Ada");

        vars["PatientFirstName"].ShouldBe("Ada");
        vars["URL"].ShouldBe("https://x/a");
        vars["ExpiresInHours"].ShouldBe("24");
    }

    [Fact]
    public void An_account_named_after_its_login_is_greeted_neutrally_not_by_its_login()
    {
        AccountActivationSender.BuildVariables(User("admin", "admin"), "u", null)["PatientFirstName"].ShouldBe("there");
        AccountActivationSender.BuildVariables(User("someone", null), "u", " ")["PatientFirstName"].ShouldBe("there");
    }

    [Fact]
    public void A_real_name_is_used_with_the_surname_for_the_full_name()
    {
        var vars = AccountActivationSender.BuildVariables(User("jdoe", "Jane", "Synthetic"), "u", null);

        vars["PatientFirstName"].ShouldBe("Jane");
        vars["PatientFullName"].ShouldBe("Jane Synthetic");
    }
}
