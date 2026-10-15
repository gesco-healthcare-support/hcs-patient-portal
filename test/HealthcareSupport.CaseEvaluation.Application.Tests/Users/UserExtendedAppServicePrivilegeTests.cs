using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Security;
using Shouldly;
using Volo.Abp;
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

    private async Task<Guid> SeedAsync(string kind, string role)
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

                var user = new IdentityUser(id, $"{kind}-{id:N}", $"{kind}-{id:N}@example.test", tenantId: null)
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

    private async Task<T> AsCallerAsync<T>(Guid callerId, string role, Func<Task<T>> call)
    {
        using (_currentTenant.Change(null))
        using (WithCurrentUser.Run(_principalAccessor, callerId, role))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    private async Task AsCallerAsync(Guid callerId, string role, Func<Task> call)
    {
        using (_currentTenant.Change(null))
        using (WithCurrentUser.Run(_principalAccessor, callerId, role))
        {
            await WithUnitOfWorkAsync(call);
        }
    }

    private async Task<Snapshot> SnapshotAsync(Guid id)
    {
        using (_currentTenant.Change(null))
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
            () => viaInterface.UpdateRolesAsync(a.Intake, new IdentityUserUpdateRolesDto { RoleNames = new[] { Intake, ItAdmin } })));
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
            () => _service.UpdateRolesAsync(a.Supervisor, new IdentityUserUpdateRolesDto { RoleNames = new[] { Supervisor, role } })));
    }

    [Theory]
    [InlineData(ItAdmin)]
    [InlineData(Admin)]
    public async Task A_supervisor_cannot_give_another_user_a_privileged_role(string role)
    {
        var a = await SeedActorsAsync();
        await AssertRefusedAndUnchangedAsync(a.Intake, () => AsCallerAsync(a.Supervisor, Supervisor,
            () => _service.UpdateRolesAsync(a.Intake, new IdentityUserUpdateRolesDto { RoleNames = new[] { Intake, role } })));
        var dto = await UpdateDtoAsync(a.Intake, new[] { Intake, role });
        await AssertRefusedAndUnchangedAsync(a.Intake, () => AsCallerAsync(a.Supervisor, Supervisor, () => _service.UpdateAsync(a.Intake, dto)));
    }

    [Fact]
    public async Task A_supervisor_cannot_change_their_own_roles_even_within_the_staff_roles()
    {
        var a = await SeedActorsAsync();
        await AssertRefusedAndUnchangedAsync(a.Supervisor, () => AsCallerAsync(a.Supervisor, Supervisor,
            () => _service.UpdateRolesAsync(a.Supervisor, new IdentityUserUpdateRolesDto { RoleNames = new[] { Supervisor, Intake } })));
        await AssertRefusedAndUnchangedAsync(a.Supervisor, () => AsCallerAsync(a.Supervisor, Supervisor,
            () => _service.UpdateRolesAsync(a.Supervisor, new IdentityUserUpdateRolesDto { RoleNames = Array.Empty<string>() })));
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

        await AsCallerAsync(a.Supervisor, Supervisor,
            () => _service.UpdateRolesAsync(a.Intake, new IdentityUserUpdateRolesDto { RoleNames = new[] { Intake, Supervisor } }));
        (await SnapshotAsync(a.Intake)).Roles.ShouldBe($"{Intake},{Supervisor}");

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

        await AsCallerAsync(caller, callerRole,
            () => _service.UpdateRolesAsync(a.Intake, new IdentityUserUpdateRolesDto { RoleNames = new[] { Intake, ItAdmin } }));
        (await SnapshotAsync(a.Intake)).Roles.ShouldContain(ItAdmin);

        var other = callerRole == ItAdmin ? a.Admin : a.ItAdmin;
        await AsCallerAsync(caller, callerRole,
            () => _service.UpdatePasswordAsync(other, new IdentityUserUpdatePasswordInput { NewPassword = NewPassword }));
        (await SnapshotAsync(other)).OldPasswordWorks.ShouldBeFalse();

        await AsCallerAsync(caller, callerRole, () => _service.DeleteAsync(a.Supervisor));
        (await SnapshotAsync(a.Supervisor)).Exists.ShouldBeFalse();
    }
}
