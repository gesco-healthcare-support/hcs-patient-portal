using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using HealthcareSupport.CaseEvaluation.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Users;

/// <summary>
/// 2026-10-27 (Levon): what an administrator's change to someone's sign-in email sends. "Send just one
/// verify email; password and everything else stays exactly the same."
/// <list type="bullet">
///   <item>An activated account: one confirm-your-email link at the new address. Following it confirms
///         the address and leaves the password exactly as it was.</item>
///   <item>An account never activated: a fresh activation link at the new address.</item>
///   <item>Either way, a notice with no link to the previous address.</item>
///   <item>An edit that keeps the address (however it is written) sends nothing.</item>
/// </list>
/// </summary>
/// <remarks>
/// The confirm link goes out through the account emailer, which queues a background job; the job
/// manager is replaced for THIS class only, so that email is asserted and never sent. The activation
/// link and the notice go through the notification outbox. Host users, synthetic addresses.
/// </remarks>
public abstract class UserEmailChangeNotificationTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Admin = "admin";
    private const string Intake = "Intake Staff";
    private const string Password = "1q2w3E*Orig!";

    private IBackgroundJobManager _jobs = null!;
    private readonly UserExtendedAppService _service;
    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    protected UserEmailChangeNotificationTests()
    {
        _service = GetRequiredService<UserExtendedAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _roleManager = GetRequiredService<IdentityRoleManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _jobs = Substitute.For<IBackgroundJobManager>();
        services.Replace(ServiceDescriptor.Singleton<IBackgroundJobManager>(_jobs));
    }

    private static string Address(Guid id) => $"intake-{id:N}@example.test";

    private async Task<Guid> SeedUserAsync(string role)
    {
        var id = Guid.NewGuid();
        using (_currentTenant.Change(null))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                if (await _roleManager.FindByNameAsync(role) == null)
                {
                    (await _roleManager.CreateAsync(new IdentityRole(Guid.NewGuid(), role, tenantId: null))).Succeeded.ShouldBeTrue();
                }

                var user = new IdentityUser(id, $"user-{id:N}", Address(id), tenantId: null) { Name = "Synthetic", Surname = "Person" };
                (await _userManager.CreateAsync(user, Password)).Succeeded.ShouldBeTrue();
                (await _userManager.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();
            });
        }
        return id;
    }

    private async Task ActivateAsync(Guid id)
    {
        using (_currentTenant.Change(null))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var user = await _userManager.GetByIdAsync(id);
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                (await _userManager.ConfirmEmailAsync(user, token)).Succeeded.ShouldBeTrue();
            });
        }
    }

    private async Task<T> AsAdminAsync<T>(Func<Task<T>> call)
    {
        using (_currentTenant.Change(null))
        using (WithCurrentUser.Run(_principalAccessor, Guid.NewGuid(), Admin))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    private async Task EditAsync(Guid id, Action<IdentityUserUpdateDto> tweak)
    {
        var current = await AsAdminAsync(() => _service.GetAsync(id));
        var dto = new IdentityUserUpdateDto
        {
            UserName = current.UserName,
            Name = current.Name,
            Surname = current.Surname,
            Email = current.Email,
            PhoneNumber = current.PhoneNumber,
            IsActive = current.IsActive,
            LockoutEnabled = current.LockoutEnabled,
            RoleNames = new[] { Intake },
            ConcurrencyStamp = current.ConcurrencyStamp,
        };
        dto.ExtraProperties["IsExternalUser"] = false;
        dto.ExtraProperties["IsAccessor"] = false;
        dto.ExtraProperties["LockoutCycle"] = 0;
        tweak(dto);
        await AsAdminAsync(() => _service.UpdateAsync(id, dto));
    }

    private async Task<(bool Confirmed, string Hash, bool PasswordWorks)> StateAsync(Guid id)
    {
        using (_currentTenant.Change(null))
        {
            return await WithUnitOfWorkAsync(async () =>
            {
                var user = await _userManager.GetByIdAsync(id);
                return (user.EmailConfirmed, user.PasswordHash ?? string.Empty, await _userManager.CheckPasswordAsync(user, Password));
            });
        }
    }

    private SendAppointmentEmailArgs[] QueuedConfirmLinks(Guid id) =>
        _jobs.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IBackgroundJobManager.EnqueueAsync))
            .Select(c => c.GetArguments()[0])
            .OfType<SendAppointmentEmailArgs>()
            .Where(a => a.Context != null && a.Context.StartsWith($"AccountEmailer/EmailConfirmationLink/{id}/", StringComparison.Ordinal))
            .ToArray();

    private async Task<List<NotificationOutboxItem>> HostOutboxAsync(string kind)
    {
        var outbox = GetRequiredService<IRepository<NotificationOutboxItem, Guid>>();
        using (_currentTenant.Change(null))
        {
            var occurrences = kind + "/";
            return await WithUnitOfWorkAsync(() => outbox.GetListAsync(x => x.Context.StartsWith(occurrences)));
        }
    }

    [Fact]
    public async Task An_activated_account_gets_one_confirm_link_at_the_new_address_and_following_it_keeps_the_password()
    {
        var id = await SeedUserAsync(Intake);
        await ActivateAsync(id);
        var before = await StateAsync(id);
        var newEmail = $"moved-{Guid.NewGuid():N}@example.test";

        await EditAsync(id, d => d.Email = newEmail);

        var link = QueuedConfirmLinks(id).ShouldHaveSingleItem("one confirm-your-email link");
        link.To.ShouldBe(newEmail);
        link.Body.ShouldContain($"/Account/EmailConfirmation?userId={id}");
        (await HostOutboxAsync($"AccountActivation/EmailChanged/{id}")).ShouldBeEmpty(
            "an activated account keeps its password, so it is not sent an activation link");

        var notice = (await HostOutboxAsync($"AccountEmailChanged/{id}")).ShouldHaveSingleItem("one notice to the previous address");
        notice.To.ShouldBe(Address(id));
        notice.Body.ShouldNotContain(newEmail, Case.Insensitive, "the notice shows the new address masked");

        var unconfirmed = await StateAsync(id);
        unconfirmed.Confirmed.ShouldBeFalse("the new address is not confirmed until its owner follows the link");

        // The link in that email confirms the new address and changes nothing else.
        var token = WebUtility.UrlDecode(Regex.Match(link.Body, "confirmationToken=([^\"&<\\s]+)").Groups[1].Value);
        token.ShouldNotBeNullOrEmpty();
        using (_currentTenant.Change(null))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var user = await _userManager.GetByIdAsync(id);
                (await _userManager.ConfirmEmailAsync(user, token)).Succeeded.ShouldBeTrue();
            });
        }

        var after = await StateAsync(id);
        after.Confirmed.ShouldBeTrue();
        after.Hash.ShouldBe(before.Hash, "confirming the new address must not touch the password");
        after.PasswordWorks.ShouldBeTrue();
    }

    [Fact]
    public async Task An_account_never_activated_gets_a_fresh_activation_link_at_the_new_address()
    {
        var id = await SeedUserAsync(Intake);
        var newEmail = $"moved-{Guid.NewGuid():N}@example.test";

        await EditAsync(id, d => d.Email = newEmail);

        var activation = (await HostOutboxAsync($"AccountActivation/EmailChanged/{id}")).ShouldHaveSingleItem();
        activation.To.ShouldBe(newEmail);
        activation.Body.ShouldContain($"/Account/ActivateAccount?userId={id}");
        QueuedConfirmLinks(id).ShouldBeEmpty();
        (await HostOutboxAsync($"AccountEmailChanged/{id}")).ShouldHaveSingleItem().To.ShouldBe(Address(id));
    }

    [Fact]
    public async Task An_edit_that_keeps_the_address_sends_nothing_and_each_real_change_sends_again()
    {
        var id = await SeedUserAsync(Intake);
        await ActivateAsync(id);

        // The same address, written differently, with another field changed: not an address change.
        await EditAsync(id, d =>
        {
            d.Email = "  " + Address(id).ToUpperInvariant() + " ";
            d.Name = "Renamed";
        });

        QueuedConfirmLinks(id).ShouldBeEmpty();
        (await HostOutboxAsync($"AccountActivation/EmailChanged/{id}")).ShouldBeEmpty();
        (await HostOutboxAsync($"AccountEmailChanged/{id}")).ShouldBeEmpty();
        (await StateAsync(id)).Confirmed.ShouldBeTrue("an unchanged address keeps its confirmation");

        // Each real change is its own occurrence, so the second one is sent too.
        await EditAsync(id, d => d.Email = $"first-{Guid.NewGuid():N}@example.test");
        await EditAsync(id, d => d.Email = $"second-{Guid.NewGuid():N}@example.test");
        (await HostOutboxAsync($"AccountEmailChanged/{id}")).Count.ShouldBe(2);
    }
}
