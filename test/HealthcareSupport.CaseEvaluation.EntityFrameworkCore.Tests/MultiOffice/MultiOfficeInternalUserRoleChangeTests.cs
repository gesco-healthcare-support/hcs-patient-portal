using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.HostOperators;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.InternalUsers;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Staff Management's "Change role" (2026-10-26, one role per person), on real per-office databases: a
/// person switched from Intake Staff to Staff Supervisor is taken off every office they were assigned to,
/// with each office's shadow login locked, exactly as Staff Assignments' Unassign leaves it; switched the
/// other way, they are assigned nothing. IT Admin and administrator accounts, any other role, and the
/// caller's own account are refused, and a refusal changes nothing.
/// </summary>
/// <remarks>
/// The offices and the host database are shared by every MultiOffice test and never roll back, so every
/// user carries a per-test token and every assertion is about this test's own users. All names and emails
/// below are synthetic.
/// </remarks>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeInternalUserRoleChangeTests : CaseEvaluationMultiOfficeTestBase
{
    private const string IntakeStaff = InternalUserRoleDataSeedContributor.IntakeStaffRoleName;
    private const string Supervisor = InternalUserRoleDataSeedContributor.StaffSupervisorRoleName;
    private const string ItAdmin = InternalUserRoleDataSeedContributor.ItAdminRoleName;

    private readonly IInternalUsersAppService _staff;
    private readonly IIntakeAssignmentsAppService _assignments;
    private readonly IdentityUserManager _users;
    private readonly InternalUserRoleDataSeedContributor _roleSeeder;
    private readonly IRepository<IntakeOfficeAssignment, Guid> _rows;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    public MultiOfficeInternalUserRoleChangeTests()
    {
        _staff = GetRequiredService<IInternalUsersAppService>();
        _assignments = GetRequiredService<IIntakeAssignmentsAppService>();
        _users = GetRequiredService<IdentityUserManager>();
        _roleSeeder = GetRequiredService<InternalUserRoleDataSeedContributor>();
        _rows = GetRequiredService<IRepository<IntakeOfficeAssignment, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    // ------------------------------------------------------------------ harness

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    private async Task<(SeededOffice A, SeededOffice B)> OfficesAsync()
    {
        var offices = await GetSeededOfficesAsync();
        foreach (var officeId in new Guid?[] { null, offices.A.OfficeId, offices.B.OfficeId })
        {
            await WithUnitOfWorkAsync(async () =>
            {
                using (_currentTenant.Change(officeId))
                {
                    await _roleSeeder.SeedAsync(new DataSeedContext(officeId));
                }
            }, requiresNew: true);
        }

        return offices;
    }

    /// <summary>A host login named "{first} {token}" holding exactly <paramref name="role"/>.</summary>
    private Task<IdentityUser> HostUserAsync(string first, string token, string role) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var email = $"{first.ToLowerInvariant()}.{token}@example.test";
                var user = new IdentityUser(Guid.NewGuid(), email, email, tenantId: null) { Name = first, Surname = token };
                (await _users.CreateAsync(user, "Synthetic-Passw0rd!")).Succeeded.ShouldBeTrue();
                (await _users.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();
                return user;
            }
        }, requiresNew: true);

    private Task AssignAsync(Guid operatorId, Guid officeId) =>
        _assignments.AssignAsync(new AssignIntakeOfficeDto { OperatorUserId = operatorId, OfficeId = officeId });

    private Task<InternalUserListDto> ChangeRoleAsync(Guid userId, string role) =>
        _staff.ChangeRoleAsync(userId, new ChangeInternalUserRoleDto { RoleName = role });

    private Task<List<Guid>> AssignedOfficesAsync(Guid operatorId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                return (await _rows.GetListAsync(a => a.OperatorUserId == operatorId)).Select(a => a.OfficeId).ToList();
            }
        }, requiresNew: true);

    private Task<IList<string>> RolesOfAsync(Guid userId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                return await _users.GetRolesAsync(await _users.GetByIdAsync(userId));
            }
        }, requiresNew: true);

    /// <summary>The operator's shadow login as the office's own database holds it, or null.</summary>
    private Task<IdentityUser?> ShadowAsync(Guid officeId, string email) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                return await _users.FindByEmailAsync(email);
            }
        }, requiresNew: true);

    private async Task<T> As<T>(Guid userId, Func<Task<T>> call)
    {
        var claims = new List<Claim> { new(AbpClaimTypes.UserId, userId.ToString()) };
        using (_principal.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))))
        {
            return await call();
        }
    }

    // ------------------------------------------------------------------ the two directions

    [Fact]
    public async Task Intake_staff_moved_to_supervisor_is_taken_off_every_office_and_each_shadow_login_is_locked()
    {
        var (a, b) = await OfficesAsync();
        var op = await HostUserAsync("Ivy", NewToken(), IntakeStaff);
        await AssignAsync(op.Id, a.OfficeId);
        await AssignAsync(op.Id, b.OfficeId);
        (await ShadowAsync(a.OfficeId, op.Email!))!.IsActive.ShouldBeTrue();

        var row = await ChangeRoleAsync(op.Id, Supervisor);

        row.Role.ShouldBe(Supervisor);
        (await RolesOfAsync(op.Id)).ShouldBe(new[] { Supervisor });
        (await AssignedOfficesAsync(op.Id)).ShouldBeEmpty();
        (await ShadowAsync(a.OfficeId, op.Email!))!.IsActive.ShouldBeFalse();
        (await ShadowAsync(b.OfficeId, op.Email!))!.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task A_supervisor_moved_to_intake_staff_is_assigned_nothing()
    {
        var (a, b) = await OfficesAsync();
        var sup = await HostUserAsync("Sam", NewToken(), Supervisor);

        var row = await ChangeRoleAsync(sup.Id, IntakeStaff);

        row.Role.ShouldBe(IntakeStaff);
        (await RolesOfAsync(sup.Id)).ShouldBe(new[] { IntakeStaff });
        (await AssignedOfficesAsync(sup.Id)).ShouldBeEmpty();
        (await ShadowAsync(a.OfficeId, sup.Email!)).ShouldBeNull();
        (await ShadowAsync(b.OfficeId, sup.Email!)).ShouldBeNull();
    }

    [Fact]
    public async Task Choosing_the_role_already_held_changes_nothing_and_keeps_the_assignments()
    {
        var (a, _) = await OfficesAsync();
        var op = await HostUserAsync("Ivy", NewToken(), IntakeStaff);
        await AssignAsync(op.Id, a.OfficeId);

        var row = await ChangeRoleAsync(op.Id, "intake staff");

        row.Role.ShouldBe(IntakeStaff);
        (await AssignedOfficesAsync(op.Id)).ShouldBe(new[] { a.OfficeId });
        (await ShadowAsync(a.OfficeId, op.Email!))!.IsActive.ShouldBeTrue();
    }

    // ------------------------------------------------------------------ refused, and nothing changes

    [Fact]
    public async Task An_it_admin_another_role_the_callers_own_account_and_an_unknown_id_are_refused()
    {
        var (a, _) = await OfficesAsync();
        var token = NewToken();
        var admin = await HostUserAsync("Ada", token, ItAdmin);
        var op = await HostUserAsync("Ivy", token, IntakeStaff);
        await AssignAsync(op.Id, a.OfficeId);

        (await Should.ThrowAsync<BusinessException>(() => ChangeRoleAsync(admin.Id, Supervisor)))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.InternalUserRoleChangeNotAllowed);
        (await RolesOfAsync(admin.Id)).ShouldBe(new[] { ItAdmin });

        foreach (var role in new[] { ItAdmin, "admin", "Patient" })
        {
            (await Should.ThrowAsync<BusinessException>(() => ChangeRoleAsync(op.Id, role)))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.InternalUserRoleChangeNotAllowed);
        }

        (await Should.ThrowAsync<BusinessException>(() => As(op.Id, () => ChangeRoleAsync(op.Id, Supervisor))))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.InternalUserRoleChangeNotAllowed);

        (await Should.ThrowAsync<BusinessException>(() => ChangeRoleAsync(Guid.NewGuid(), Supervisor)))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.InternalUserNotFound);

        (await RolesOfAsync(op.Id)).ShouldBe(new[] { IntakeStaff });
        (await AssignedOfficesAsync(op.Id)).ShouldBe(new[] { a.OfficeId });
        (await ShadowAsync(a.OfficeId, op.Email!))!.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Unassign_and_change_role_leave_the_same_state_behind()
    {
        // Both go through IntakeAssignmentRevoker: the row is gone and the shadow login is locked.
        var (a, b) = await OfficesAsync();
        var token = NewToken();
        var unassigned = await HostUserAsync("Una", token, IntakeStaff);
        var promoted = await HostUserAsync("Pro", token, IntakeStaff);
        await AssignAsync(unassigned.Id, a.OfficeId);
        await AssignAsync(promoted.Id, a.OfficeId);

        await _assignments.UnassignAsync(unassigned.Id, a.OfficeId);
        await ChangeRoleAsync(promoted.Id, Supervisor);

        foreach (var user in new[] { unassigned, promoted })
        {
            (await AssignedOfficesAsync(user.Id)).ShouldBeEmpty();
            (await ShadowAsync(a.OfficeId, user.Email!))!.IsActive.ShouldBeFalse();
            (await ShadowAsync(b.OfficeId, user.Email!)).ShouldBeNull();
        }
    }
}
