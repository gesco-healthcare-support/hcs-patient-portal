using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using HealthcareSupport.CaseEvaluation.UserQueries;
using HealthcareSupport.CaseEvaluation.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.Account;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// The account services a signed-in user drives for themselves: changing their password (and the
/// receipt email it sends), the escalating lockout after failed sign-ins, submitting a query to the
/// office, and the extended identity-user service.
/// </summary>
/// <remarks>
/// <para>
/// Two collaborators are replaced for THIS class only, in <c>AfterAddApplication</c>: the
/// notification dispatcher, so the password receipt is asserted rather than rendered (the tenant
/// templates are not seeded in this rig), and the local event bus, so the query-submitted event is
/// asserted and its handler (Session A's area) does not run here.
/// </para>
/// <para>Every fixture user is host-scoped, created per test, with a synthetic name and email.</para>
/// </remarks>
public abstract class AccountSelfServiceFlowTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string OldPassword = "Synthetic-Pass1!";
    private const string NewPassword = "Synthetic-Pass2!";

    private INotificationDispatcher _dispatcher = null!;
    private ILocalEventBus _events = null!;

    private readonly IdentityUserManager _userManager;
    private readonly ICurrentPrincipalAccessor _principal;
    private readonly ICurrentTenant _currentTenant;

    protected AccountSelfServiceFlowTests()
    {
        _userManager = GetRequiredService<IdentityUserManager>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _dispatcher = Substitute.For<INotificationDispatcher>();
        _events = Substitute.For<ILocalEventBus>();
        services.Replace(ServiceDescriptor.Singleton(typeof(INotificationDispatcher), _dispatcher));
        services.Replace(ServiceDescriptor.Singleton(typeof(ILocalEventBus), _events));
    }

    // ------------------------------------------------------------------ harness

    private async Task<Guid> CreateUserAsync(string? password = OldPassword)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var user = new IdentityUser(Guid.NewGuid(), $"acct-{token}", $"acct-{token}@example.test", tenantId: null)
        {
            Name = "Synthetic",
        };
        await WithUnitOfWorkAsync(async () =>
        {
            var created = password == null
                ? await _userManager.CreateAsync(user)
                : await _userManager.CreateAsync(user, password);
            created.Succeeded.ShouldBeTrue();
            (await _userManager.SetLockoutEnabledAsync(user, true)).Succeeded.ShouldBeTrue();
        });
        return user.Id;
    }

    private Task<IdentityUser> ReloadAsync(Guid userId) =>
        WithUnitOfWorkAsync(() => _userManager.GetByIdAsync(userId));

    private async Task ChangePasswordAsUserAsync(Guid userId)
    {
        using (WithCurrentUser.Run(_principal, userId))
        {
            await WithUnitOfWorkAsync(() => GetRequiredService<IProfileAppService>().ChangePasswordAsync(
                new ChangePasswordInput { CurrentPassword = OldPassword, NewPassword = NewPassword }));
        }
    }

    private List<object?[]> Dispatches() =>
        _dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchAsync))
            .Select(c => c.GetArguments())
            .ToList();

    /// <summary>Fails sign-in until the account locks, and returns how many attempts that took.</summary>
    private async Task<int> FailUntilLockedAsync(Guid userId)
    {
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            var locked = await WithUnitOfWorkAsync(async () =>
            {
                var user = await _userManager.GetByIdAsync(userId);
                (await _userManager.AccessFailedAsync(user)).Succeeded.ShouldBeTrue();
                return await _userManager.IsLockedOutAsync(user);
            });
            if (locked)
            {
                return attempt;
            }
        }

        throw new InvalidOperationException("The account never locked within 20 failed attempts.");
    }

    // ------------------------------------------------------------------ password change

    [Fact]
    public async Task Changing_a_password_changes_it_and_sends_the_user_a_receipt()
    {
        var userId = await CreateUserAsync();

        await ChangePasswordAsUserAsync(userId);

        var user = await ReloadAsync(userId);
        (await _userManager.CheckPasswordAsync(user, NewPassword)).ShouldBeTrue();

        var args = Dispatches().ShouldHaveSingleItem();
        args[0].ShouldBe(NotificationTemplateConsts.Codes.PasswordChange);
        var recipient = ((IReadOnlyCollection<NotificationRecipient>)args[1]!).ShouldHaveSingleItem();
        recipient.Email.ShouldBe(user.Email);
        recipient.IsRegistered.ShouldBeTrue();
        var variables = (IReadOnlyDictionary<string, object?>)args[2]!;
        variables["PatientFirstName"].ShouldBe("Synthetic");
        variables["PatientEmail"].ShouldBe(user.Email);
        args[3].ShouldBe($"PasswordChange/InApp/{userId}");
    }

    [Fact]
    public async Task A_failed_receipt_does_not_undo_or_fail_the_password_change()
    {
        var userId = await CreateUserAsync();
        _dispatcher.DispatchAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyCollection<NotificationRecipient>>(),
                Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<string>(),
                Arg.Any<HealthcareSupport.CaseEvaluation.Appointments.PacketAttachmentRef?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Synthetic mail outage"));

        await ChangePasswordAsUserAsync(userId);

        (await _userManager.CheckPasswordAsync(await ReloadAsync(userId), NewPassword)).ShouldBeTrue();
        Dispatches().Count.ShouldBe(1);
    }

    // ------------------------------------------------------------------ lockout

    [Fact]
    public async Task A_failed_sign_in_below_the_threshold_neither_locks_nor_counts_a_cycle()
    {
        var userId = await CreateUserAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            var user = await _userManager.GetByIdAsync(userId);
            (await _userManager.AccessFailedAsync(user)).Succeeded.ShouldBeTrue();
        });

        var stored = await ReloadAsync(userId);
        (await _userManager.IsLockedOutAsync(stored)).ShouldBeFalse();
        stored.GetProperty<int?>(CaseEvaluationModuleExtensionConfigurator.LockoutCyclePropertyName).ShouldBeNull();
    }

    [Fact]
    public async Task Each_lockout_counts_a_cycle_and_the_second_lasts_longer_than_the_first()
    {
        var userId = await CreateUserAsync();

        await FailUntilLockedAsync(userId);
        var first = await ReloadAsync(userId);
        first.GetProperty<int>(CaseEvaluationModuleExtensionConfigurator.LockoutCyclePropertyName).ShouldBe(1);
        var firstLength = first.LockoutEnd!.Value - DateTimeOffset.UtcNow;
        firstLength.ShouldBeInRange(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));

        await FailUntilLockedAsync(userId);
        var second = await ReloadAsync(userId);
        second.GetProperty<int>(CaseEvaluationModuleExtensionConfigurator.LockoutCyclePropertyName).ShouldBe(2);
        (second.LockoutEnd!.Value - DateTimeOffset.UtcNow).ShouldBeInRange(TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5));
    }

    // ------------------------------------------------------------------ user queries

    [Fact]
    public async Task A_submitted_query_is_saved_and_announced_with_who_sent_it()
    {
        var userId = await CreateUserAsync();

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(_principal, userId))
        {
            await WithUnitOfWorkAsync(() => GetRequiredService<IUserQueryAppService>().CreateAsync(
                new UserQueryCreateDto { Message = "Synthetic question", RequestConfirmationNumber = "A90001" }));
        }

        var submitted = _events.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(ILocalEventBus.PublishAsync))
            .Select(c => c.GetArguments()[0])
            .OfType<UserQuerySubmittedEto>()
            .ShouldHaveSingleItem();
        submitted.Message.ShouldBe("Synthetic question");
        submitted.RequestConfirmationNumber.ShouldBe("A90001");
        submitted.SubmitterUserId.ShouldBe(userId);
        submitted.TenantId.ShouldBe(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var saved = await WithUnitOfWorkAsync(() =>
                GetRequiredService<IRepository<UserQuery, Guid>>().GetAsync(submitted.UserQueryId));
            saved.Message.ShouldBe("Synthetic question");
        }
    }

    // ------------------------------------------------------------------ extended identity-user service

    [Fact]
    public async Task The_extended_user_service_reads_a_user_like_the_identity_service()
    {
        var userId = await CreateUserAsync(password: null);
        var expected = await ReloadAsync(userId);

        var dto = await WithUnitOfWorkAsync(() => GetRequiredService<UserExtendedAppService>().GetAsync(userId));

        dto.UserName.ShouldBe(expected.UserName);
        dto.Email.ShouldBe(expected.Email);
    }
}
