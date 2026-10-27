using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ExternalAccount;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Users;

/// <summary>
/// Who may change privileged accounts through the identity user service. Staff Supervisor holds the
/// framework user-update permission so the internal-users hub can toggle staff, and that permission
/// alone lets a caller change any user roles and password. The rules under test are code-level, so
/// the always-allow authorization of this harness does not hide them.
///
/// <para>All users are synthetic host operators. The role claim of the caller is what the guard
/// reads, as it is in a real token, and every refused case asserts the target is unchanged
/// afterwards.</para>
/// </summary>
public abstract class UserExtendedAppServicePrivilegeTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string ItAdmin = "IT Admin";
    private const string Admin = "admin";
    private const string Supervisor = "Staff Supervisor";
    private const string Intake = "Intake Staff";
    private const string OriginalPassword = "1q2w3E*Orig!";
    private const string NewPassword = "Zq9!Brand-New-Pw";

    private readonly UserExtendedAppService _service;
    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    protected UserExtendedAppServicePrivilegeTests()
    {
        _service = GetRequiredService<UserExtendedAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _roleManager = GetRequiredService<IdentityRoleManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private sealed record Actors(Guid Supervisor, Guid Intake, Guid ItAdmin, Guid Admin);

    private sealed record Snapshot(string Roles, string Email, bool Active, bool Exists, bool OldPasswordWorks, bool Locked, int ClaimCount);

    private async Task<Guid> SeedAsync(string kind, string role, Guid? office = null)
    {
        var id = Guid.NewGuid();
        using (_currentTenant.Change(office))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                if (await _roleManager.FindByNameAsync(role) == null)
                {
                    (await _roleManager.CreateAsync(new IdentityRole(Guid.NewGuid(), role, tenantId: office))).Succeeded.ShouldBeTrue();
                }

                var user = new IdentityUser(id, $"{kind}-{id:N}", $"{kind}-{id:N}@example.test", tenantId: office)
                {
                    Name = "Synthetic",
                    Surname = kind,
                };
                (await _userManager.CreateAsync(user, OriginalPassword)).Succeeded.ShouldBeTrue();
                (await _userManager.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();
            });
        }
        return id;
    }

    private async Task<Actors> SeedActorsAsync() => new(
        await SeedAsync("sup", Supervisor),
        await SeedAsync("intake", Intake),
        await SeedAsync("itadmin", ItAdmin),
        await SeedAsync("admin", Admin));

    private async Task<T> AsCallerAsync<T>(Guid callerId, string role, Func<Task<T>> call, Guid? office = null)
    {
        using (_currentTenant.Change(office))
        using (WithCurrentUser.Run(_principalAccessor, callerId, role))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    private async Task AsCallerAsync(Guid callerId, string role, Func<Task> call, Guid? office = null)
    {
        using (_currentTenant.Change(office))
        using (WithCurrentUser.Run(_principalAccessor, callerId, role))
        {
            await WithUnitOfWorkAsync(call);
        }
    }

    private static readonly Guid Office = TenantsTestData.TenantARef;

    private async Task<Snapshot> SnapshotAsync(Guid id, Guid? office = null)
    {
        using (_currentTenant.Change(office))
        {
            return await WithUnitOfWorkAsync(async () =>
            {
                var user = await _userManager.FindByIdAsync(id.ToString());
                if (user == null)
                {
                    return new Snapshot("", "", false, false, false, false, 0);
                }
                var roles = string.Join(",", (await _userManager.GetRolesAsync(user)).OrderBy(r => r));
                var claims = (await _userManager.GetClaimsAsync(user)).Count;
                return new Snapshot(roles, user.Email!, user.IsActive, true,
                    await _userManager.CheckPasswordAsync(user, OriginalPassword),
                    await _userManager.IsLockedOutAsync(user), claims);
            });
        }
    }

    /// <summary>
    /// The three value-typed extension properties are validated as required on a DTO built in code,
    /// so a test caller states them, as a client that omits them has them defaulted to these values.
    /// </summary>
    private static T WithExtensionDefaults<T>(T dto) where T : IdentityUserCreateOrUpdateDtoBase
    {
        dto.ExtraProperties["IsExternalUser"] = false;
        dto.ExtraProperties["IsAccessor"] = false;
        dto.ExtraProperties["LockoutCycle"] = 0;
        return dto;
    }

    private async Task<IdentityUserUpdateDto> UpdateDtoAsync(Guid id, string[]? roles, Action<IdentityUserUpdateDto>? tweak = null)
    {
        var current = await AsCallerAsync(Guid.NewGuid(), Admin, () => _service.GetAsync(id));
        var dto = new IdentityUserUpdateDto
        {
            UserName = current.UserName,
            Name = current.Name,
            Surname = current.Surname,
            Email = current.Email,
            PhoneNumber = current.PhoneNumber,
            IsActive = current.IsActive,
            LockoutEnabled = current.LockoutEnabled,
            RoleNames = roles ?? (await AsCallerAsync(Guid.NewGuid(), Admin, () => _service.GetRolesAsync(id))).Items.Select(r => r.Name).ToArray(),
            ConcurrencyStamp = current.ConcurrencyStamp,
        };
        tweak?.Invoke(dto);
        return WithExtensionDefaults(dto);
    }

    private async Task<bool> ConfirmedAsync(Guid id)
    {
        using (_currentTenant.Change(null))
        {
            return await WithUnitOfWorkAsync(async () => (await _userManager.GetByIdAsync(id)).EmailConfirmed);
        }
    }

    private async Task ConfirmThroughTheMailboxAsync(Guid id)
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

    private async Task<List<NotificationOutboxItem>> HostOutboxAsync(string contextTag)
    {
        var outbox = GetRequiredService<IRepository<NotificationOutboxItem, Guid>>();
        using (_currentTenant.Change(null))
        {
            var occurrences = contextTag + "/";
            return await WithUnitOfWorkAsync(() => outbox.GetListAsync(
                x => x.Context == contextTag || x.Context.StartsWith(occurrences)));
        }
    }

    // 2026-10-23: an account made on this screen is switched on by its OWNER, through the
    // activation email, never by a tick box. A confirmed email is what grants a party access to
    // appointments, so the person filling in the form must not be able to set it -- least of all on
    // an address that may be somebody else's.

    [Fact]
    public async Task A_user_created_here_starts_unconfirmed_even_when_the_form_says_confirmed_and_gets_one_activation_email()
    {
        var a = await SeedActorsAsync();
        var name = $"made-{Guid.NewGuid():N}";

        var created = await AsCallerAsync(a.Admin, Admin, () => _service.CreateAsync(WithExtensionDefaults(new IdentityUserCreateDto
        {
            UserName = name,
            Email = $"{name}@example.test",
            Password = OriginalPassword,
            RoleNames = new[] { Intake },
            EmailConfirmed = true,
            SendConfirmationEmail = true,
        })));

        created.EmailConfirmed.ShouldBeFalse();
        (await ConfirmedAsync(created.Id)).ShouldBeFalse();
        var rows = await HostOutboxAsync($"AccountActivation/IdentityUserCreated/{created.Id}");
        rows.Count.ShouldBe(1, "one email, carrying the activation link");
        rows[0].To.ShouldBe($"{name}@example.test");
        rows[0].Body.ShouldContain($"/Account/ActivateAccount?userId={created.Id}");
    }

    [Fact]
    public async Task The_password_typed_on_the_form_is_never_the_accounts_password()
    {
        // The owner chooses the password from the activation link. Whatever the form sent is replaced
        // by a value nobody sees, so the person who created the account cannot sign in as it.
        var a = await SeedActorsAsync();
        var name = $"typed-{Guid.NewGuid():N}";

        var created = await AsCallerAsync(a.Admin, Admin, () => _service.CreateAsync(WithExtensionDefaults(new IdentityUserCreateDto
        {
            UserName = name,
            Email = $"{name}@example.test",
            Password = OriginalPassword,
            RoleNames = new[] { Intake },
            ShouldChangePasswordOnNextLogin = true,
        })));

        using (_currentTenant.Change(null))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var user = await _userManager.GetByIdAsync(created.Id);
                (await _userManager.CheckPasswordAsync(user, OriginalPassword)).ShouldBeFalse(
                    "the creator knows the typed password; it must not open the account");
                user.ShouldChangePasswordOnNextLogin.ShouldBeFalse(
                    "the owner chooses the password once, from the link");
            });
        }
    }

    [Fact]
    public async Task An_edit_can_never_turn_email_confirmation_on_and_a_changed_address_turns_it_off()
    {
        var a = await SeedActorsAsync();

        var tick = await UpdateDtoAsync(a.Intake, null, d => d.EmailConfirmed = true);
        await AsCallerAsync(a.Admin, Admin, () => _service.UpdateAsync(a.Intake, tick));
        (await ConfirmedAsync(a.Intake)).ShouldBeFalse("a tick box is not proof of the mailbox");

        await ConfirmThroughTheMailboxAsync(a.Intake);
        var same = await UpdateDtoAsync(a.Intake, null, d => d.Name = "Renamed");
        await AsCallerAsync(a.Admin, Admin, () => _service.UpdateAsync(a.Intake, same));
        (await ConfirmedAsync(a.Intake)).ShouldBeTrue("an edit that keeps the address keeps its confirmation");

        var moved = await UpdateDtoAsync(a.Intake, null, d =>
        {
            d.Email = $"moved-{Guid.NewGuid():N}@example.test";
            d.EmailConfirmed = true;
        });
        await AsCallerAsync(a.Admin, Admin, () => _service.UpdateAsync(a.Intake, moved));
        (await ConfirmedAsync(a.Intake)).ShouldBeFalse("a new address has not been proven");
    }

    private async Task AssertRefusedAndUnchangedAsync(Guid target, Func<Task> attempt)
    {
        var before = await SnapshotAsync(target);
        var refused = await Should.ThrowAsync<BusinessException>(attempt);
        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.PrivilegedUserChangeNotAllowed);
        (await SnapshotAsync(target)).ShouldBe(before);
    }

    /// <summary>
    /// The framework publishes its own identity routes from the interface, so the guard has to hold
    /// when the service is reached through <see cref="IIdentityUserAppService"/> and not only
    /// through the concrete class.
    /// </summary>
    [Fact]
    public async Task The_framework_interface_route_is_guarded_too()
    {
        var a = await SeedActorsAsync();
        var viaInterface = GetRequiredService<IIdentityUserAppService>();

        await AssertRefusedAndUnchangedAsync(a.Intake, () => AsCallerAsync(a.Supervisor, Supervisor,
            () => viaInterface.UpdateRolesAsync(a.Intake, new IdentityUserUpdateRolesDto { RoleNames = new[] { ItAdmin } })));
        await AssertRefusedAndUnchangedAsync(a.ItAdmin, () => AsCallerAsync(a.Supervisor, Supervisor,
            () => viaInterface.UpdatePasswordAsync(a.ItAdmin, new IdentityUserUpdatePasswordInput { NewPassword = NewPassword })));
    }

    // ------------------------------------------------------------------ refused

    [Theory]
    [InlineData(ItAdmin)]
    [InlineData(Admin)]
    public async Task A_supervisor_cannot_give_themselves_a_privileged_role(string role)
    {
        var a = await SeedActorsAsync();
        await AssertRefusedAndUnchangedAsync(a.Supervisor, () => AsCallerAsync(a.Supervisor, Supervisor,
            () => _service.UpdateRolesAsync(a.Supervisor, new IdentityUserUpdateRolesDto { RoleNames = new[] { role } })));
    }

    [Theory]
    [InlineData(ItAdmin)]
    [InlineData(Admin)]
    public async Task A_supervisor_cannot_give_another_user_a_privileged_role(string role)
    {
        var a = await SeedActorsAsync();
        await AssertRefusedAndUnchangedAsync(a.Intake, () => AsCallerAsync(a.Supervisor, Supervisor,
            () => _service.UpdateRolesAsync(a.Intake, new IdentityUserUpdateRolesDto { RoleNames = new[] { role } })));
        var dto = await UpdateDtoAsync(a.Intake, new[] { role });
        await AssertRefusedAndUnchangedAsync(a.Intake, () => AsCallerAsync(a.Supervisor, Supervisor, () => _service.UpdateAsync(a.Intake, dto)));
    }

    [Fact]
    public async Task A_supervisor_cannot_change_their_own_roles_even_within_the_staff_roles()
    {
        var a = await SeedActorsAsync();
        await AssertRefusedAndUnchangedAsync(a.Supervisor, () => AsCallerAsync(a.Supervisor, Supervisor,
            () => _service.UpdateRolesAsync(a.Supervisor, new IdentityUserUpdateRolesDto { RoleNames = new[] { Intake } })));
    }

    [Theory]
    [InlineData(ItAdmin)]
    [InlineData(Admin)]
    public async Task A_supervisor_cannot_reset_the_password_or_email_of_a_privileged_user(string privilegedRole)
    {
        var a = await SeedActorsAsync();
        var target = privilegedRole == ItAdmin ? a.ItAdmin : a.Admin;

        await AssertRefusedAndUnchangedAsync(target, () => AsCallerAsync(a.Supervisor, Supervisor,
            () => _service.UpdatePasswordAsync(target, new IdentityUserUpdatePasswordInput { NewPassword = NewPassword })));

        var dto = await UpdateDtoAsync(target, null, d => d.Email = "taken-over@example.test");
        await AssertRefusedAndUnchangedAsync(target, () => AsCallerAsync(a.Supervisor, Supervisor, () => _service.UpdateAsync(target, dto)));
    }

    [Fact]
    public async Task A_supervisor_cannot_delete_lock_unlock_deactivate_or_alter_a_privileged_user()
    {
        var a = await SeedActorsAsync();
        var t = a.ItAdmin;

        await AssertRefusedAndUnchangedAsync(t, () => AsCallerAsync(a.Supervisor, Supervisor,
            () => _service.LockAsync(t, DateTime.UtcNow.AddYears(1))));
        await AssertRefusedAndUnchangedAsync(t, () => AsCallerAsync(a.Supervisor, Supervisor, () => _service.UnlockAsync(t)));
        await AssertRefusedAndUnchangedAsync(t, () => AsCallerAsync(a.Supervisor, Supervisor,
            () => _service.SetTwoFactorEnabledAsync(t, true)));
        await AssertRefusedAndUnchangedAsync(t, () => AsCallerAsync(a.Supervisor, Supervisor,
            () => _service.UpdateClaimsAsync(t, new List<IdentityUserClaimDto> { new() { ClaimType = "x", ClaimValue = "y" } })));
        var deactivate = await UpdateDtoAsync(t, null, d => d.IsActive = false);
        await AssertRefusedAndUnchangedAsync(t, () => AsCallerAsync(a.Supervisor, Supervisor, () => _service.UpdateAsync(t, deactivate)));
        await AssertRefusedAndUnchangedAsync(t, () => AsCallerAsync(a.Supervisor, Supervisor, () => _service.DeleteAsync(t)));
    }

    [Theory]
    [InlineData(ItAdmin)]
    [InlineData(Admin)]
    public async Task A_supervisor_cannot_create_a_user_holding_a_privileged_role(string role)
    {
        var a = await SeedActorsAsync();
        var name = $"created-{Guid.NewGuid():N}";
        var input = WithExtensionDefaults(new IdentityUserCreateDto
        {
            UserName = name,
            Email = $"{name}@example.test",
            Password = OriginalPassword,
            RoleNames = new[] { role },
        });

        var refused = await Should.ThrowAsync<BusinessException>(() => AsCallerAsync(a.Supervisor, Supervisor, () => _service.CreateAsync(input)));
        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.PrivilegedUserChangeNotAllowed);

        using (_currentTenant.Change(null))
        {
            (await WithUnitOfWorkAsync(() => _userManager.FindByNameAsync(name))).ShouldBeNull();
        }
    }

    // ------------------------------------------------------------------ allowed

    [Fact]
    public async Task A_supervisor_can_still_toggle_a_staff_member_active_flag_as_the_spa_does()
    {
        var a = await SeedActorsAsync();
        var dto = await UpdateDtoAsync(a.Intake, null, d => d.IsActive = false);

        await AsCallerAsync(a.Supervisor, Supervisor, () => _service.UpdateAsync(a.Intake, dto));

        var after = await SnapshotAsync(a.Intake);
        after.Active.ShouldBeFalse();
        after.Roles.ShouldBe(Intake);
    }

    [Fact]
    public async Task A_supervisor_can_still_manage_the_staff_roles_of_another_user_and_create_staff()
    {
        var a = await SeedActorsAsync();

        // The staff roles of an OFFICE user (2026-10-27: a host user's role moves through Change role).
        var officeSupervisor = await SeedAsync("officesup", Supervisor, Office);
        var officeIntake = await SeedAsync("officeintake", Intake, Office);
        await AsCallerAsync(officeSupervisor, Supervisor,
            () => _service.UpdateRolesAsync(officeIntake, new IdentityUserUpdateRolesDto { RoleNames = new[] { Supervisor } }), Office);
        (await SnapshotAsync(officeIntake, Office)).Roles.ShouldBe(Supervisor);

        var name = $"staff-{Guid.NewGuid():N}";
        var created = await AsCallerAsync(a.Supervisor, Supervisor, () => _service.CreateAsync(WithExtensionDefaults(new IdentityUserCreateDto
        {
            UserName = name,
            Email = $"{name}@example.test",
            Password = OriginalPassword,
            RoleNames = new[] { Intake },
        })));
        (await SnapshotAsync(created.Id)).Roles.ShouldBe(Intake);
    }

    [Fact]
    public async Task A_supervisor_can_still_edit_their_own_profile_without_touching_roles()
    {
        var a = await SeedActorsAsync();
        var dto = await UpdateDtoAsync(a.Supervisor, null, d => d.Name = "Renamed");

        await AsCallerAsync(a.Supervisor, Supervisor, () => _service.UpdateAsync(a.Supervisor, dto));

        (await AsCallerAsync(a.Supervisor, Supervisor, () => _service.GetAsync(a.Supervisor))).Name.ShouldBe("Renamed");
    }

    [Theory]
    [InlineData(ItAdmin)]
    [InlineData(Admin)]
    public async Task A_privileged_caller_keeps_every_power(string callerRole)
    {
        var a = await SeedActorsAsync();
        var caller = callerRole == ItAdmin ? a.ItAdmin : a.Admin;

        // 2026-10-27: on the staff site a role is changed only through Staff Management's Change role
        // (MultiOfficeInternalUserRoleChangeTests covers a privileged caller giving IT Admin there).
        (await Should.ThrowAsync<BusinessException>(() => AsCallerAsync(caller, callerRole,
            () => _service.UpdateRolesAsync(a.Intake, new IdentityUserUpdateRolesDto { RoleNames = new[] { ItAdmin } }))))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.StaffRoleChangeUseStaffManagement);
        (await SnapshotAsync(a.Intake)).Roles.ShouldBe(Intake);

        // Inside an office, an administrator still changes an office user's role here.
        var officeAdmin = await SeedAsync("officeadmin", Admin, Office);
        var officeIntake = await SeedAsync("officeintake", Intake, Office);
        await AsCallerAsync(officeAdmin, Admin,
            () => _service.UpdateRolesAsync(officeIntake, new IdentityUserUpdateRolesDto { RoleNames = new[] { Admin } }), Office);
        (await SnapshotAsync(officeIntake, Office)).Roles.ShouldBe(Admin);

        var other = callerRole == ItAdmin ? a.Admin : a.ItAdmin;
        await AsCallerAsync(caller, callerRole,
            () => _service.UpdatePasswordAsync(other, new IdentityUserUpdatePasswordInput { NewPassword = NewPassword }));
        (await SnapshotAsync(other)).OldPasswordWorks.ShouldBeFalse();

        await AsCallerAsync(caller, callerRole, () => _service.DeleteAsync(a.Supervisor));
        (await SnapshotAsync(a.Supervisor)).Exists.ShouldBeFalse();
    }

    // ------------------------------------------------------------------ one role per person (2026-10-26)

    private async Task AssertOneRoleRefusedAndUnchangedAsync(Guid target, Func<Task> attempt)
    {
        var before = await SnapshotAsync(target);
        var refused = await Should.ThrowAsync<BusinessException>(attempt);
        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.OneRolePerPerson);
        (await SnapshotAsync(target)).ShouldBe(before);
    }

    private async Task<bool> ExistsByUserNameAsync(string userName)
    {
        using (_currentTenant.Change(null))
        {
            return await WithUnitOfWorkAsync(async () => await _userManager.FindByNameAsync(userName) != null);
        }
    }

    [Theory]
    [InlineData(ItAdmin)]
    [InlineData(Admin)]
    [InlineData(Supervisor)]
    public async Task No_caller_can_leave_a_person_with_two_roles_or_none(string callerRole)
    {
        var a = await SeedActorsAsync();
        var caller = callerRole switch { ItAdmin => a.ItAdmin, Admin => a.Admin, _ => a.Supervisor };

        await AssertOneRoleRefusedAndUnchangedAsync(a.Intake, () => AsCallerAsync(caller, callerRole,
            () => _service.UpdateRolesAsync(a.Intake, new IdentityUserUpdateRolesDto { RoleNames = new[] { Intake, Supervisor } })));
        await AssertOneRoleRefusedAndUnchangedAsync(a.Intake, () => AsCallerAsync(caller, callerRole,
            () => _service.UpdateRolesAsync(a.Intake, new IdentityUserUpdateRolesDto { RoleNames = Array.Empty<string>() })));

        var twoRoles = await UpdateDtoAsync(a.Intake, new[] { Intake, Supervisor });
        await AssertOneRoleRefusedAndUnchangedAsync(a.Intake, () => AsCallerAsync(caller, callerRole,
            () => _service.UpdateAsync(a.Intake, twoRoles)));
        var noRole = await UpdateDtoAsync(a.Intake, Array.Empty<string>());
        await AssertOneRoleRefusedAndUnchangedAsync(a.Intake, () => AsCallerAsync(caller, callerRole,
            () => _service.UpdateAsync(a.Intake, noRole)));
    }

    [Theory]
    [InlineData(ItAdmin)]
    [InlineData(Admin)]
    public async Task No_caller_can_create_a_person_with_two_roles_or_none(string callerRole)
    {
        var a = await SeedActorsAsync();
        var caller = callerRole == ItAdmin ? a.ItAdmin : a.Admin;

        foreach (var roles in new[] { new[] { Intake, Supervisor }, Array.Empty<string>() })
        {
            var name = $"roles-{Guid.NewGuid():N}";
            var refused = await Should.ThrowAsync<BusinessException>(() => AsCallerAsync(caller, callerRole,
                () => _service.CreateAsync(WithExtensionDefaults(new IdentityUserCreateDto
                {
                    UserName = name,
                    Email = $"{name}@example.test",
                    Password = OriginalPassword,
                    RoleNames = roles,
                }))));

            refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.OneRolePerPerson);
            (await ExistsByUserNameAsync(name)).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task A_role_named_twice_is_one_role_and_an_edit_without_a_role_list_keeps_the_role_held()
    {
        var a = await SeedActorsAsync();

        // Inside an office, where this screen still changes roles (2026-10-27).
        var officeAdmin = await SeedAsync("officeadmin", Admin, Office);
        var officeIntake = await SeedAsync("officeintake", Intake, Office);
        await SeedAsync("officesup", Supervisor, Office);
        await AsCallerAsync(officeAdmin, Admin,
            () => _service.UpdateRolesAsync(officeIntake, new IdentityUserUpdateRolesDto { RoleNames = new[] { Supervisor, Supervisor } }), Office);
        (await SnapshotAsync(officeIntake, Office)).Roles.ShouldBe(Supervisor);

        var dto = await UpdateDtoAsync(a.Intake, null, d =>
        {
            d.RoleNames = null!;
            d.Name = "Renamed";
        });
        await AsCallerAsync(a.Admin, Admin, () => _service.UpdateAsync(a.Intake, dto));

        var after = await SnapshotAsync(a.Intake);
        after.Roles.ShouldBe(Intake);
        (await AsCallerAsync(a.Admin, Admin, () => _service.GetAsync(a.Intake))).Name.ShouldBe("Renamed");
    }

    // 2026-10-23: the identity Users screen's "Resend activation email" row action.

    private IAccountActivationAppService Activation => GetRequiredService<IAccountActivationAppService>();

    [Fact]
    public async Task Resend_activation_sends_one_fresh_link_to_an_account_not_yet_active()
    {
        var a = await SeedActorsAsync();

        await AsCallerAsync(a.Admin, Admin, () => Activation.ResendToUserAsync(a.Intake));

        var rows = await HostOutboxAsync($"AccountActivation/StaffResend/{a.Intake}");
        rows.Count.ShouldBe(1);
        rows[0].Body.ShouldContain($"/Account/ActivateAccount?userId={a.Intake}");
    }

    [Fact]
    public async Task Resend_activation_pressed_again_within_a_minute_is_refused_and_sends_nothing_more()
    {
        // Each send gets its own tag, so nothing else would stop a double-click or a script.
        var a = await SeedActorsAsync();
        await AsCallerAsync(a.Admin, Admin, () => Activation.ResendToUserAsync(a.Intake));

        var ex = await Should.ThrowAsync<BusinessException>(
            () => AsCallerAsync(a.Admin, Admin, () => Activation.ResendToUserAsync(a.Intake)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ActivationResendTooSoon);
        (await HostOutboxAsync($"AccountActivation/StaffResend/{a.Intake}")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Resend_activation_refuses_an_account_that_is_already_active()
    {
        var a = await SeedActorsAsync();
        await ConfirmThroughTheMailboxAsync(a.Intake);

        var ex = await Should.ThrowAsync<BusinessException>(
            () => AsCallerAsync(a.Admin, Admin, () => Activation.ResendToUserAsync(a.Intake)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ActivationAlreadyActive);
        (await HostOutboxAsync($"AccountActivation/StaffResend/{a.Intake}")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Resend_activation_refuses_a_switched_off_account()
    {
        var a = await SeedActorsAsync();
        using (_currentTenant.Change(null))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var user = await _userManager.GetByIdAsync(a.Intake);
                user.SetIsActive(false);
                (await _userManager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
            });
        }

        var ex = await Should.ThrowAsync<BusinessException>(
            () => AsCallerAsync(a.Admin, Admin, () => Activation.ResendToUserAsync(a.Intake)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ActivationAccountInactive);
        (await HostOutboxAsync($"AccountActivation/StaffResend/{a.Intake}")).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(Admin)]
    [InlineData(ItAdmin)]
    public async Task A_supervisor_cannot_resend_activation_to_a_privileged_account(string privilegedRole)
    {
        // The same rule the Users screen applies to every edit of a privileged account.
        var a = await SeedActorsAsync();
        var target = privilegedRole == Admin ? a.Admin : a.ItAdmin;

        var ex = await Should.ThrowAsync<BusinessException>(
            () => AsCallerAsync(a.Supervisor, Supervisor, () => Activation.ResendToUserAsync(target)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.PrivilegedUserChangeNotAllowed);
        (await HostOutboxAsync($"AccountActivation/StaffResend/{target}")).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_supervisor_can_resend_activation_to_a_staff_account()
    {
        var a = await SeedActorsAsync();

        await AsCallerAsync(a.Supervisor, Supervisor, () => Activation.ResendToUserAsync(a.Intake));

        (await HostOutboxAsync($"AccountActivation/StaffResend/{a.Intake}")).Count.ShouldBe(1);
    }

    // ------------------------------------------------------------------------
    // User import from a file, switched off 2026-10-23.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task The_user_import_permission_is_switched_off_for_everyone()
    {
        // A disabled permission is granted to nobody, the admin roles included, and ABP's Users screen
        // hides its import menu without it. Deleting DisableUserImport makes this fail.
        var definitions = GetRequiredService<Volo.Abp.Authorization.Permissions.IPermissionDefinitionManager>();
        var import = await definitions.GetOrNullAsync(IdentityPermissions.Users.Import);

        import.ShouldNotBeNull("ABP defines the permission; it is disabled, not removed");
        import!.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task Importing_users_from_a_file_is_refused_even_past_the_permission()
    {
        // This harness allows every permission, so the call reaches the override itself: the second
        // lock, for the day the permission is enabled again.
        // A well-formed request, so input validation passes and the override itself answers.
        var file = new Volo.Abp.Content.RemoteStreamContent(
            new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes("UserName,EmailAddress")), "users.csv", "text/csv");
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _service.ImportUsersFromFileAsync(new ImportUsersFromFileInputWithStream
            {
                File = file,
                FileType = ImportUsersFromFileType.Csv,
            }));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.UserImportDisabled);
    }

    [Fact]
    public async Task Importing_an_external_user_is_refused_even_past_the_permission()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _service.ImportExternalUserAsync(new ImportExternalUserInput
            {
                Provider = "TEST-provider",
                UserNameOrEmailAddress = "TEST-import@test.local",
                Password = "TEST-Passw0rd!",
            }));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.UserImportDisabled);
    }
    // ---- 2026-10-27: on the staff site a role is changed in ONE place, Staff Management's Change role ----

    [Theory]
    [InlineData(Admin)]
    [InlineData(ItAdmin)]
    [InlineData(Supervisor)]
    public async Task On_the_staff_site_a_role_change_on_this_screen_is_refused_for_every_caller(string callerRole)
    {
        var a = await SeedActorsAsync();
        var caller = callerRole switch { Admin => a.Admin, ItAdmin => a.ItAdmin, _ => a.Supervisor };

        var edit = await UpdateDtoAsync(a.Intake, new[] { Supervisor });
        (await Should.ThrowAsync<BusinessException>(() => AsCallerAsync(caller, callerRole, () => _service.UpdateAsync(a.Intake, edit))))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.StaffRoleChangeUseStaffManagement);
        (await Should.ThrowAsync<BusinessException>(() => AsCallerAsync(caller, callerRole,
            () => _service.UpdateRolesAsync(a.Intake, new IdentityUserUpdateRolesDto { RoleNames = new[] { Supervisor } }))))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.StaffRoleChangeUseStaffManagement);

        (await SnapshotAsync(a.Intake)).Roles.ShouldBe(Intake);
    }

    [Fact]
    public async Task Resending_the_roles_a_person_holds_is_not_a_role_change_so_the_switch_off_toggle_still_works()
    {
        var a = await SeedActorsAsync();

        var off = await UpdateDtoAsync(a.Intake, new[] { "intake staff" }, d => d.IsActive = false);
        await AsCallerAsync(a.Supervisor, Supervisor, () => _service.UpdateAsync(a.Intake, off));

        var after = await SnapshotAsync(a.Intake);
        after.Active.ShouldBeFalse();
        after.Roles.ShouldBe(Intake);
    }
}
