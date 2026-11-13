using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ExternalLogins;
using HealthcareSupport.CaseEvaluation.HostOperators;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// 2026-11-13 -- a practice's Staff Supervisor switches an OUTSIDE user's login off and on, under REAL
/// authorization and a real Identity database. The permission is narrow on purpose (it is not
/// <c>AbpIdentity.Users.Update</c>), so what must hold is who may call it, and which accounts it may touch.
///
/// <para>The office admin is given the grant the way a deployed office has it (a row in the grant table); Staff
/// Supervisor and the other roles come from the PRODUCTION seeder, so the supervisor facts prove what production
/// ships. A HOST Staff Supervisor reaches an office through a stamped shadow login whose office role is Staff
/// Supervisor, so the same call is made as one of those.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class ExternalLoginsAuthorizationTests : CaseEvaluationRealAuthorizationTestBase
{
    private const string RoleProviderName = "R";
    private const string InterceptorCodePrefix = "Volo.Authorization:";
    private const string SupervisorRole = InternalUserRoleDataSeedContributor.StaffSupervisorRoleName;
    private const string IntakeRole = InternalUserRoleDataSeedContributor.IntakeStaffRoleName;
    private const string AdminRole = "admin";

    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public ExternalLoginsAuthorizationTests()
    {
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task A_native_staff_supervisor_switches_an_outside_login_off_and_back_on_without_confirming_it()
    {
        var office = await OfficeAsync();
        var target = await CreateUserAsync(office, new[] { ExternalRoleConsts.ApplicantParalegal }, emailConfirmed: false);
        var before = await ReadAsync(office, target);

        var off = await CallAsync(office, Guid.NewGuid(), SupervisorRole, sp => Service(sp).SetActiveAsync(target, new ExternalLoginSetActiveDto { IsActive = false }));
        off.Exception.ShouldBeNull();
        var afterOff = await ReadAsync(office, target);
        afterOff.IsActive.ShouldBeFalse();
        afterOff.SecurityStamp.ShouldNotBe(before.SecurityStamp, "switching off rotates the security stamp");

        var on = await CallAsync(office, Guid.NewGuid(), SupervisorRole, sp => Service(sp).SetActiveAsync(target, new ExternalLoginSetActiveDto { IsActive = true }));
        on.Exception.ShouldBeNull();
        var afterOn = await ReadAsync(office, target);
        afterOn.IsActive.ShouldBeTrue();
        afterOn.EmailConfirmed.ShouldBeFalse("switching ON must never confirm an email that was never confirmed");
    }

    [Fact]
    public async Task Switching_off_revokes_the_persons_sessions_and_switching_on_changes_no_stamp()
    {
        var office = await OfficeAsync();
        var target = await CreateUserAsync(office, new[] { ExternalRoleConsts.Patient });
        var sessionId = "TEST-extlogin-" + Guid.NewGuid().ToString("N");
        await WithOfficeAsync(office, async () =>
        {
            await GetRequiredService<IdentitySessionManager>().CreateAsync(sessionId, "Web", "TEST", target, office, "TEST-client", "127.0.0.1");
        });
        (await SessionExistsAsync(office, sessionId)).ShouldBeTrue("positive control: the session was created");

        await CallAsync(office, Guid.NewGuid(), SupervisorRole, sp => Service(sp).SetActiveAsync(target, new ExternalLoginSetActiveDto { IsActive = false }));
        (await SessionExistsAsync(office, sessionId)).ShouldBeFalse("switch-off signs the person out");

        var off = await ReadAsync(office, target);
        await CallAsync(office, Guid.NewGuid(), SupervisorRole, sp => Service(sp).SetActiveAsync(target, new ExternalLoginSetActiveDto { IsActive = true }));
        (await ReadAsync(office, target)).SecurityStamp.ShouldBe(off.SecurityStamp, "switch-on leaves the stamp alone");
    }

    [Fact]
    public async Task Asking_for_the_state_the_login_already_has_writes_nothing()
    {
        var office = await OfficeAsync();
        var target = await CreateUserAsync(office, new[] { ExternalRoleConsts.ClaimExaminer });
        var before = await ReadAsync(office, target);

        var result = await CallAsync(office, Guid.NewGuid(), SupervisorRole, sp => Service(sp).SetActiveAsync(target, new ExternalLoginSetActiveDto { IsActive = true }));

        result.Exception.ShouldBeNull();
        var after = await ReadAsync(office, target);
        after.SecurityStamp.ShouldBe(before.SecurityStamp);
        after.ConcurrencyStamp.ShouldBe(before.ConcurrencyStamp);
    }

    /// <summary>
    /// A host Staff Supervisor works inside an office as a stamped SHADOW login whose office role is Staff Supervisor.
    /// The call must work for it exactly as for a native supervisor.
    /// </summary>
    [Fact]
    public async Task A_host_staff_supervisor_acting_through_its_office_shadow_login_can_do_the_same()
    {
        var office = await OfficeAsync();
        var shadow = await CreateUserAsync(office, new[] { SupervisorRole }, hostOperatorStamp: Guid.NewGuid());
        var target = await CreateUserAsync(office, new[] { ExternalRoleConsts.DefenseParalegal });

        var off = await CallAsync(office, shadow, SupervisorRole, sp => Service(sp).SetActiveAsync(target, new ExternalLoginSetActiveDto { IsActive = false }));
        var offState = (await ReadAsync(office, target)).IsActive;
        var on = await CallAsync(office, shadow, SupervisorRole, sp => Service(sp).SetActiveAsync(target, new ExternalLoginSetActiveDto { IsActive = true }));

        off.Exception.ShouldBeNull();
        offState.ShouldBeFalse();
        on.Exception.ShouldBeNull();
        (await ReadAsync(office, target)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task The_office_admin_can_use_it_too()
    {
        var office = await OfficeAsync();
        await EnsureAdminGrantAsync(office);
        var target = await CreateUserAsync(office, new[] { ExternalRoleConsts.ApplicantAttorney });

        var result = await CallAsync(office, Guid.NewGuid(), AdminRole, sp => Service(sp).SetActiveAsync(target, new ExternalLoginSetActiveDto { IsActive = false }));

        result.Exception.ShouldBeNull();
        (await ReadAsync(office, target)).IsActive.ShouldBeFalse();
    }

    [Theory]
    [InlineData(IntakeRole)]
    [InlineData("Patient")]
    [InlineData("Applicant Attorney")]
    [InlineData("Applicant Paralegal")]
    [InlineData("Claim Examiner")]
    public async Task Intake_staff_and_outside_roles_are_refused_by_the_permission(string callerRole)
    {
        var office = await OfficeAsync();
        var target = await CreateUserAsync(office, new[] { ExternalRoleConsts.Patient });

        var list = await CallAsync(office, Guid.NewGuid(), callerRole, sp => Service(sp).GetListAsync(new GetExternalLoginsInput { MaxResultCount = 10 }));
        var change = await CallAsync(office, Guid.NewGuid(), callerRole, sp => Service(sp).SetActiveAsync(target, new ExternalLoginSetActiveDto { IsActive = false }));

        IsInterceptorRefusal(list.Exception).ShouldBeTrue($"{callerRole} must be refused the list. Got: {Describe(list.Exception)}.");
        IsInterceptorRefusal(change.Exception).ShouldBeTrue($"{callerRole} must be refused the change. Got: {Describe(change.Exception)}.");
        (await ReadAsync(office, target)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task A_caller_on_the_staff_site_is_refused()
    {
        var office = await OfficeAsync();
        var target = await CreateUserAsync(office, new[] { ExternalRoleConsts.Patient });

        var change = await CallAsync(null, Guid.NewGuid(), SupervisorRole, sp => Service(sp).SetActiveAsync(target, new ExternalLoginSetActiveDto { IsActive = false }));

        change.Exception.ShouldBeOfType<AbpAuthorizationException>();
        (await ReadAsync(office, target)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task A_staff_account_cannot_be_switched_off()
    {
        var office = await OfficeAsync();
        var target = await CreateUserAsync(office, new[] { IntakeRole });
        await ShouldBeRefusedAsync(office, target, CaseEvaluationDomainErrorCodes.ExternalLoginNotAnOutsideUser);
    }

    [Fact]
    public async Task An_administrator_account_cannot_be_switched_off()
    {
        var office = await OfficeAsync();
        var target = await CreateUserAsync(office, new[] { AdminRole });
        await ShouldBeRefusedAsync(office, target, CaseEvaluationDomainErrorCodes.ExternalLoginNotAnOutsideUser);
    }

    [Fact]
    public async Task A_supervisor_account_cannot_be_switched_off()
    {
        var office = await OfficeAsync();
        var target = await CreateUserAsync(office, new[] { SupervisorRole });
        await ShouldBeRefusedAsync(office, target, CaseEvaluationDomainErrorCodes.ExternalLoginNotAnOutsideUser);
    }

    [Fact]
    public async Task An_account_with_no_role_cannot_be_switched_off()
    {
        var office = await OfficeAsync();
        var target = await CreateUserAsync(office, Array.Empty<string>());
        await ShouldBeRefusedAsync(office, target, CaseEvaluationDomainErrorCodes.ExternalLoginNotAnOutsideUser);
    }

    [Fact]
    public async Task An_outside_role_plus_a_staff_role_cannot_be_switched_off()
    {
        var office = await OfficeAsync();
        var target = await CreateUserAsync(office, new[] { ExternalRoleConsts.ApplicantParalegal, IntakeRole });
        await ShouldBeRefusedAsync(office, target, CaseEvaluationDomainErrorCodes.ExternalLoginNotAnOutsideUser);
    }

    /// <summary>A host operator's shadow login is refused even when its roles look outside.</summary>
    [Fact]
    public async Task A_host_operators_shadow_login_cannot_be_switched_off_even_with_an_outside_role()
    {
        var office = await OfficeAsync();
        var target = await CreateUserAsync(office, new[] { ExternalRoleConsts.Patient }, hostOperatorStamp: Guid.NewGuid());
        await ShouldBeRefusedAsync(office, target, CaseEvaluationDomainErrorCodes.ExternalLoginNotAnOutsideUser);
    }

    [Fact]
    public async Task A_caller_cannot_switch_off_their_own_login()
    {
        var office = await OfficeAsync();
        var target = await CreateUserAsync(office, new[] { ExternalRoleConsts.Patient });

        var result = await CallAsync(office, target, SupervisorRole, sp => Service(sp).SetActiveAsync(target, new ExternalLoginSetActiveDto { IsActive = false }));

        result.Exception.ShouldBeOfType<BusinessException>().Code.ShouldBe(CaseEvaluationDomainErrorCodes.ExternalLoginCannotChangeSelf);
        (await ReadAsync(office, target)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task An_unknown_login_is_not_found()
    {
        var office = await OfficeAsync();
        var result = await CallAsync(office, Guid.NewGuid(), SupervisorRole, sp => Service(sp).SetActiveAsync(Guid.NewGuid(), new ExternalLoginSetActiveDto { IsActive = false }));
        result.Exception.ShouldBeOfType<EntityNotFoundException>();
    }

    [Fact]
    public async Task The_list_holds_only_outside_logins_and_applies_the_filter_and_paging()
    {
        var office = await OfficeAsync();
        var tag = "extl" + Guid.NewGuid().ToString("N")[..10];
        var a = await CreateUserAsync(office, new[] { ExternalRoleConsts.Patient }, tag: tag, surname: "Aa");
        var b = await CreateUserAsync(office, new[] { ExternalRoleConsts.ApplicantParalegal }, tag: tag, surname: "Bb", active: false);
        var c = await CreateUserAsync(office, new[] { ExternalRoleConsts.DefenseAttorney }, tag: tag, surname: "Cc", firmName: "TEST Firm");
        await CreateUserAsync(office, new[] { IntakeRole }, tag: tag, surname: "Dd");
        await CreateUserAsync(office, new[] { ExternalRoleConsts.Patient, SupervisorRole }, tag: tag, surname: "Ee");
        await CreateUserAsync(office, Array.Empty<string>(), tag: tag, surname: "Ff");
        await CreateUserAsync(office, new[] { ExternalRoleConsts.Patient }, tag: tag, surname: "Gg", hostOperatorStamp: Guid.NewGuid());

        var all = (await CallAsync(office, Guid.NewGuid(), SupervisorRole, sp => Service(sp).GetListAsync(new GetExternalLoginsInput { FilterText = tag, MaxResultCount = 50 }))).Value!;
        // The shadow-stamped account is dropped from the page, and the three staff-ish accounts never match.
        all.Items.Select(i => i.Id).ShouldBe(new[] { a, b, c });
        all.Items.Single(i => i.Id == c).FirmName.ShouldBe("TEST Firm");
        all.Items.Single(i => i.Id == b).Roles.ShouldBe(new[] { ExternalRoleConsts.ApplicantParalegal });

        var off = (await CallAsync(office, Guid.NewGuid(), SupervisorRole, sp => Service(sp).GetListAsync(new GetExternalLoginsInput { FilterText = tag, IsActive = false, MaxResultCount = 50 }))).Value!;
        off.Items.Select(i => i.Id).ShouldBe(new[] { b });
        var active = (await CallAsync(office, Guid.NewGuid(), SupervisorRole, sp => Service(sp).GetListAsync(new GetExternalLoginsInput { FilterText = tag, IsActive = true, MaxResultCount = 50 }))).Value!;
        active.Items.Select(i => i.Id).ShouldBe(new[] { a, c });

        var page1 = (await CallAsync(office, Guid.NewGuid(), SupervisorRole, sp => Service(sp).GetListAsync(new GetExternalLoginsInput { FilterText = tag, MaxResultCount = 2 }))).Value!;
        var page2 = (await CallAsync(office, Guid.NewGuid(), SupervisorRole, sp => Service(sp).GetListAsync(new GetExternalLoginsInput { FilterText = tag, MaxResultCount = 2, SkipCount = 2 }))).Value!;
        page1.Items.Select(i => i.Id).ShouldBe(new[] { a, b });
        page2.Items.Select(i => i.Id).ShouldBe(new[] { c });
        page1.TotalCount.ShouldBe(page2.TotalCount);
    }

    // ------------------------------------------------------------------------

    private static IExternalLoginsAppService Service(IServiceProvider sp) => sp.GetRequiredService<IExternalLoginsAppService>();

    private async Task<Guid> OfficeAsync()
    {
        var fixture = await GetFixtureAsync();
        await EnsureAdminGrantAsync(fixture.Office.OfficeId);
        return fixture.Office.OfficeId;
    }

    private async Task ShouldBeRefusedAsync(Guid office, Guid target, string code)
    {
        var result = await CallAsync(office, Guid.NewGuid(), SupervisorRole, sp => Service(sp).SetActiveAsync(target, new ExternalLoginSetActiveDto { IsActive = false }));
        result.Exception.ShouldBeOfType<BusinessException>().Code.ShouldBe(code);
        (await ReadAsync(office, target)).IsActive.ShouldBeTrue("a refused change leaves the account active");
    }

    private static bool IsInterceptorRefusal(Exception? outcome) =>
        outcome is AbpAuthorizationException refusal
        && refusal.Code != null
        && refusal.Code.StartsWith(InterceptorCodePrefix, StringComparison.Ordinal);

    private static string Describe(Exception? outcome) =>
        outcome == null ? "no exception" : outcome.GetType().Name + ": " + outcome.Message;

    private static ClaimsPrincipal Principal(Guid userId, string role, Guid? officeId)
    {
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, userId.ToString()),
            new(AbpClaimTypes.UserName, $"test-user-{userId:N}"),
            new(AbpClaimTypes.Role, role),
        };
        if (officeId.HasValue)
        {
            claims.Add(new Claim(AbpClaimTypes.TenantId, officeId.Value.ToString()));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private sealed record Outcome<T>(T? Result, Exception? Exception)
    {
        /// <summary>The call's value; reading it when the call threw rethrows that exception, so a failure reads as itself.</summary>
        public T? Value => Exception == null ? Result : throw new InvalidOperationException("The call threw: " + Exception, Exception);
    }

    private async Task<Outcome<T>> CallAsync<T>(Guid? scope, Guid userId, string role, Func<IServiceProvider, Task<T>> call)
    {
        Exception? caught = null;
        T? value = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(scope))
            using (_principalAccessor.Change(Principal(userId, role, scope)))
            {
                try
                {
                    value = await call(ServiceProvider);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            }
        }, requiresNew: true);
        return new Outcome<T>(value, caught);
    }

    private async Task WithOfficeAsync(Guid office, Func<Task> action)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office))
            {
                await action();
            }
        }, requiresNew: true);
    }

    private async Task<Guid> CreateUserAsync(
        Guid office,
        string[] roles,
        bool emailConfirmed = true,
        bool active = true,
        Guid? hostOperatorStamp = null,
        string tag = "extl",
        string surname = "Test",
        string? firmName = null)
    {
        var id = Guid.NewGuid();
        await WithOfficeAsync(office, async () =>
        {
            var userManager = GetRequiredService<IdentityUserManager>();
            var roleManager = GetRequiredService<IdentityRoleManager>();
            foreach (var role in roles)
            {
                if (await roleManager.FindByNameAsync(role) == null)
                {
                    await roleManager.CreateAsync(new IdentityRole(Guid.NewGuid(), role, office));
                }
            }

            var email = $"TEST-{tag}-{id:N}@example.test";
            var user = new IdentityUser(id, $"{tag}-{id:N}", email, office)
            {
                Name = "TEST",
                Surname = surname,
            };
            user.SetEmailConfirmed(emailConfirmed);
            user.SetIsActive(active);
            if (hostOperatorStamp.HasValue)
            {
                user.SetProperty(IntakeShadowUserProvisioner.OperatorIdProperty, hostOperatorStamp.Value.ToString("D"));
            }
            if (firmName != null)
            {
                user.SetProperty(CaseEvaluationModuleExtensionConfigurator.FirmNamePropertyName, firmName);
            }
            (await userManager.CreateAsync(user)).Succeeded.ShouldBeTrue();
            if (roles.Length > 0)
            {
                (await userManager.AddToRolesAsync(user, roles)).Succeeded.ShouldBeTrue();
            }
        });
        return id;
    }

    private sealed record UserState(bool IsActive, bool EmailConfirmed, string SecurityStamp, string ConcurrencyStamp);

    private async Task<UserState> ReadAsync(Guid office, Guid id)
    {
        UserState? state = null;
        await WithOfficeAsync(office, async () =>
        {
            var user = await GetRequiredService<IdentityUserManager>().FindByIdAsync(id.ToString());
            user.ShouldNotBeNull();
            state = new UserState(user.IsActive, user.EmailConfirmed, user.SecurityStamp ?? string.Empty, user.ConcurrencyStamp ?? string.Empty);
        });
        return state!;
    }

    private async Task<bool> SessionExistsAsync(Guid office, string sessionId)
    {
        var exists = false;
        await WithOfficeAsync(office, async () =>
        {
            exists = await GetRequiredService<IdentitySessionManager>().ExistAsync(sessionId);
        });
        return exists;
    }

    /// <summary>The grant a deployed office gives its admin role, written the way ABP does.</summary>
    private async Task EnsureAdminGrantAsync(Guid office)
    {
        await WithOfficeAsync(office, async () =>
        {
            var grants = GetRequiredService<IPermissionGrantRepository>();
            var permission = CaseEvaluationPermissions.UserManagement.SwitchOffExternalUser;
            if (await grants.FindAsync(permission, RoleProviderName, AdminRole) == null)
            {
                await grants.InsertAsync(
                    new PermissionGrant(Guid.NewGuid(), permission, RoleProviderName, AdminRole, office),
                    autoSave: true);
            }
        });
    }
}
