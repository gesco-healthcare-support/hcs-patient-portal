using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.HostOperators;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.InternalUsers;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.Users;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// 2026-10-27, on real per-office databases: a host operator's office login (shadow) belongs to the
/// operator's host user id, and follows the host account. Office staff email is sent to the shadow, so a
/// shadow that does not follow keeps mailing a person who was switched off, removed, moved to another role,
/// or given a new address, and a shadow found by address alone is lost when the address changes.
/// </summary>
/// <remarks>
/// The offices and the host database are shared by every MultiOffice test and never roll back, so every
/// account carries a per-test token and every assertion is about this test's own accounts. All names and
/// addresses are synthetic.
/// </remarks>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeShadowFollowsHostAccountTests : CaseEvaluationMultiOfficeTestBase
{
    private const string IntakeStaff = InternalUserRoleDataSeedContributor.IntakeStaffRoleName;
    private const string Supervisor = InternalUserRoleDataSeedContributor.StaffSupervisorRoleName;
    private const string ItAdmin = InternalUserRoleDataSeedContributor.ItAdminRoleName;
    private const string AdminRole = CaseEvaluationIdentityDataSeedContributor.AdminRoleName;

    private readonly IIntakeShadowUserProvisioner _shadows;
    private readonly IdentityUserManager _users;
    private readonly IdentityRoleManager _roles;
    private readonly InternalUserRoleDataSeedContributor _roleSeeder;
    private readonly IIntakeAssignmentsAppService _assignments;
    private readonly IInternalUsersAppService _staff;
    private readonly UserExtendedAppService _identity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    public MultiOfficeShadowFollowsHostAccountTests()
    {
        _shadows = GetRequiredService<IIntakeShadowUserProvisioner>();
        _users = GetRequiredService<IdentityUserManager>();
        _roles = GetRequiredService<IdentityRoleManager>();
        _roleSeeder = GetRequiredService<InternalUserRoleDataSeedContributor>();
        _assignments = GetRequiredService<IIntakeAssignmentsAppService>();
        _staff = GetRequiredService<IInternalUsersAppService>();
        _identity = GetRequiredService<UserExtendedAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    // ------------------------------------------------------------------ harness

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    private async Task<(SeededOffice A, SeededOffice B)> OfficesAsync()
    {
        var offices = await GetSeededOfficesAsync();

        // The host's account-email templates, as the migrator seeds them: an address change sends from host.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                await GetRequiredService<NotificationTemplateDataSeedContributor>().SeedAsync(new DataSeedContext(tenantId: null));
            }
        }, requiresNew: true);
        foreach (var officeId in new Guid?[] { null, offices.A.OfficeId, offices.B.OfficeId })
        {
            await WithUnitOfWorkAsync(async () =>
            {
                using (_currentTenant.Change(officeId))
                {
                    await _roleSeeder.SeedAsync(new DataSeedContext(officeId));
                    if (await _roles.FindByNameAsync(AdminRole) == null)
                    {
                        (await _roles.CreateAsync(new IdentityRole(Guid.NewGuid(), AdminRole, officeId))).Succeeded.ShouldBeTrue();
                    }
                }
            }, requiresNew: true);
        }

        return offices;
    }

    /// <summary>A host login holding exactly <paramref name="role"/>; username = email unless given.</summary>
    private Task<IdentityUser> HostUserAsync(string token, string role, string? userName = null) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var email = $"op.{token}@example.test";
                var user = new IdentityUser(Guid.NewGuid(), userName ?? email, email, tenantId: null) { Name = "Op", Surname = token };
                (await _users.CreateAsync(user, "Synthetic-Passw0rd!")).Succeeded.ShouldBeTrue();
                (await _users.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();
                return user;
            }
        }, requiresNew: true);

    /// <summary>An office account made directly, as a shadow made before the stamp, or an unrelated account.</summary>
    private Task<Guid> OfficeAccountAsync(Guid officeId, string userName, string email, string role) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                var account = new IdentityUser(Guid.NewGuid(), userName, email, tenantId: officeId);
                (await _users.CreateAsync(account, "Synthetic-Passw0rd!")).Succeeded.ShouldBeTrue();
                (await _users.AddToRoleAsync(account, role)).Succeeded.ShouldBeTrue();
                return account.Id;
            }
        }, requiresNew: true);

    private Task<Guid> EnsureAsync(Guid officeId, Guid operatorId, string role) =>
        WithUnitOfWorkAsync(() => _shadows.EnsureShadowUserAsync(officeId, operatorId, role), requiresNew: true);

    private sealed record Account(Guid Id, string UserName, string Email, bool Active, string? Stamp, string[] Roles);

    private Task<Account?> OfficeAccountByIdAsync(Guid officeId, Guid id) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                var user = await _users.FindByIdAsync(id.ToString());
                return user == null
                    ? null
                    : new Account(user.Id, user.UserName!, user.Email!, user.IsActive,
                        user.GetProperty<string>(IntakeShadowUserProvisioner.OperatorIdProperty),
                        (await _users.GetRolesAsync(user)).OrderBy(r => r).ToArray());
            }
        }, requiresNew: true);

    /// <summary>Every office account stamped as this operator's shadow (there must never be two).</summary>
    private Task<List<Guid>> StampedShadowsAsync(Guid officeId, Guid operatorId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                var all = new List<IdentityUser>();
                foreach (var role in new[] { IntakeStaff, Supervisor, AdminRole })
                {
                    all.AddRange(await _users.GetUsersInRoleAsync(role));
                }
                return all.Where(u => u.GetProperty<string>(IntakeShadowUserProvisioner.OperatorIdProperty) == operatorId.ToString("D"))
                    .Select(u => u.Id).Distinct().ToList();
            }
        }, requiresNew: true);

    private async Task AsAdminAsync(Func<Task> call)
    {
        using (_currentTenant.Change(null))
        using (WithCurrentUser.Run(_principal, Guid.NewGuid(), AdminRole))
        {
            await WithUnitOfWorkAsync(call, requiresNew: true);
        }
    }

    /// <summary>The identity Users screen's Edit on the staff site, keeping the person's role.</summary>
    private async Task EditHostUserAsync(Guid id, string role, Action<IdentityUserUpdateDto> tweak)
    {
        IdentityUserDto current = null!;
        await AsAdminAsync(async () => current = await _identity.GetAsync(id));
        var dto = new IdentityUserUpdateDto
        {
            UserName = current.UserName,
            Name = current.Name,
            Surname = current.Surname,
            Email = current.Email,
            PhoneNumber = current.PhoneNumber,
            IsActive = current.IsActive,
            LockoutEnabled = current.LockoutEnabled,
            RoleNames = new[] { role },
            ConcurrencyStamp = current.ConcurrencyStamp,
        };
        dto.ExtraProperties["IsExternalUser"] = false;
        dto.ExtraProperties["IsAccessor"] = false;
        dto.ExtraProperties["LockoutCycle"] = 0;
        tweak(dto);
        await AsAdminAsync(() => _identity.UpdateAsync(id, dto));
    }

    // ------------------------------------------------------------------ the shadow belongs to the operator's id

    [Fact]
    public async Task A_new_shadow_is_stamped_with_the_operators_host_user_id()
    {
        var (a, _) = await OfficesAsync();
        var op = await HostUserAsync(NewToken(), Supervisor);

        var shadowId = await EnsureAsync(a.OfficeId, op.Id, Supervisor);

        var shadow = (await OfficeAccountByIdAsync(a.OfficeId, shadowId))!;
        shadow.Stamp.ShouldBe(op.Id.ToString("D"));
        shadow.Email.ShouldBe(op.Email);
        shadow.Roles.ShouldBe(new[] { Supervisor });
    }

    [Fact]
    public async Task A_legacy_shadow_without_the_stamp_is_found_by_its_address_and_stamped_on_first_use()
    {
        // The shape of every shadow made before 2026-10-27, such as the one Intake test login on the server.
        var (a, _) = await OfficesAsync();
        var op = await HostUserAsync(NewToken(), IntakeStaff);
        var legacyId = await OfficeAccountAsync(a.OfficeId, op.Email!, op.Email!, IntakeStaff);

        var resolved = await EnsureAsync(a.OfficeId, op.Id, IntakeStaff);

        resolved.ShouldBe(legacyId, "the legacy shadow is reused, not duplicated");
        var shadow = (await OfficeAccountByIdAsync(a.OfficeId, legacyId))!;
        shadow.Stamp.ShouldBe(op.Id.ToString("D"));
        shadow.Active.ShouldBeTrue();
        shadow.Roles.ShouldBe(new[] { IntakeStaff });
        (await StampedShadowsAsync(a.OfficeId, op.Id)).ShouldBe(new[] { legacyId });
    }

    [Fact]
    public async Task A_stamped_shadow_is_found_by_the_operators_id_even_when_its_address_has_drifted()
    {
        var (a, _) = await OfficesAsync();
        var op = await HostUserAsync(NewToken(), Supervisor);
        var shadowId = await EnsureAsync(a.OfficeId, op.Id, Supervisor);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(a.OfficeId))
            {
                var shadow = await _users.GetByIdAsync(shadowId);
                (await _users.SetUserNameAsync(shadow, $"drift-{shadowId:N}@example.test")).Succeeded.ShouldBeTrue();
                (await _users.SetEmailAsync(shadow, $"drift-{shadowId:N}@example.test")).Succeeded.ShouldBeTrue();
            }
        }, requiresNew: true);

        var again = await EnsureAsync(a.OfficeId, op.Id, Supervisor);

        again.ShouldBe(shadowId, "found by the id, not lost with its address");
        var after = (await OfficeAccountByIdAsync(a.OfficeId, shadowId))!;
        after.Email.ShouldBe(op.Email, "and moved back to the operator's current address");
        after.UserName.ShouldBe(op.Email);
    }

    [Fact]
    public async Task The_operators_username_is_never_a_lookup_key()
    {
        // The System owner's host username is "admin", as is every office admin's: a username lookup would
        // hand the operator the office's own account. Here the decoy holds a shadow role, so it WOULD be
        // adopted if a username matched.
        var (a, _) = await OfficesAsync();
        var token = NewToken();
        var sharedName = $"shared-{token}";
        var op = await HostUserAsync(token, Supervisor, userName: sharedName);
        var decoyId = await OfficeAccountAsync(a.OfficeId, sharedName, $"decoy-{token}@example.test", Supervisor);

        var shadowId = await EnsureAsync(a.OfficeId, op.Id, Supervisor);

        shadowId.ShouldNotBe(decoyId);
        var decoy = (await OfficeAccountByIdAsync(a.OfficeId, decoyId))!;
        decoy.Stamp.ShouldBeNull();
        decoy.Email.ShouldBe($"decoy-{token}@example.test");
    }

    [Fact]
    public async Task An_office_admin_that_only_shares_the_operators_email_is_never_adopted()
    {
        // An office's own administrator has its own username and an address an operator may share (#593).
        // The old lookup adopted it by that address, so the operator would have been signed in AS the
        // office's administrator account, and the exact-role sync would now strip its admin role. It is
        // refused instead, as any other account that is not the operator's shadow is.
        var (a, _) = await OfficesAsync();
        var token = NewToken();
        var op = await HostUserAsync(token, Supervisor);
        var officeAdminId = await OfficeAccountAsync(a.OfficeId, $"officeadmin-{token}", op.Email!, AdminRole);

        (await Should.ThrowAsync<BusinessException>(() => EnsureAsync(a.OfficeId, op.Id, Supervisor)))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.InternalUserDuplicateEmail);

        var officeAdmin = (await OfficeAccountByIdAsync(a.OfficeId, officeAdminId))!;
        officeAdmin.Roles.ShouldBe(new[] { AdminRole });
        officeAdmin.Stamp.ShouldBeNull();
    }

    // ------------------------------------------------------------------ the shadow follows the host account

    [Fact]
    public async Task Switching_a_host_operator_off_locks_their_office_logins_in_every_office()
    {
        var (a, b) = await OfficesAsync();
        var op = await HostUserAsync(NewToken(), Supervisor);
        var inA = await EnsureAsync(a.OfficeId, op.Id, Supervisor);
        var inB = await EnsureAsync(b.OfficeId, op.Id, Supervisor);

        await EditHostUserAsync(op.Id, Supervisor, d => d.IsActive = false);

        (await OfficeAccountByIdAsync(a.OfficeId, inA))!.Active.ShouldBeFalse();
        (await OfficeAccountByIdAsync(b.OfficeId, inB))!.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Switching_an_intake_operator_back_on_wakes_the_login_of_each_office_they_are_assigned_to()
    {
        var (a, _) = await OfficesAsync();
        var op = await HostUserAsync(NewToken(), IntakeStaff);
        await _assignments.AssignAsync(new AssignIntakeOfficeDto { OperatorUserId = op.Id, OfficeId = a.OfficeId });
        var shadowId = (await StampedShadowsAsync(a.OfficeId, op.Id)).ShouldHaveSingleItem();

        await EditHostUserAsync(op.Id, IntakeStaff, d => d.IsActive = false);
        (await OfficeAccountByIdAsync(a.OfficeId, shadowId))!.Active.ShouldBeFalse();

        await EditHostUserAsync(op.Id, IntakeStaff, d => d.IsActive = true);
        (await OfficeAccountByIdAsync(a.OfficeId, shadowId))!.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task Deleting_a_host_operator_locks_their_office_logins()
    {
        var (a, _) = await OfficesAsync();
        var op = await HostUserAsync(NewToken(), Supervisor);
        var shadowId = await EnsureAsync(a.OfficeId, op.Id, Supervisor);

        await AsAdminAsync(() => _identity.DeleteAsync(op.Id));

        (await OfficeAccountByIdAsync(a.OfficeId, shadowId))!.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Changing_a_host_operators_email_moves_their_office_logins_at_once()
    {
        var (a, b) = await OfficesAsync();
        var token = NewToken();
        var op = await HostUserAsync(token, Supervisor);
        var inA = await EnsureAsync(a.OfficeId, op.Id, Supervisor);
        var inB = await EnsureAsync(b.OfficeId, op.Id, Supervisor);
        var newEmail = $"moved.{token}@example.test";

        await EditHostUserAsync(op.Id, Supervisor, d => d.Email = newEmail);

        // Moved now, before any switch-in, so office mail already reaches the new address.
        (await OfficeAccountByIdAsync(a.OfficeId, inA))!.Email.ShouldBe(newEmail);
        (await OfficeAccountByIdAsync(b.OfficeId, inB))!.Email.ShouldBe(newEmail);

        // And the next entry finds the same login: no second shadow under the new address.
        (await EnsureAsync(a.OfficeId, op.Id, Supervisor)).ShouldBe(inA);
        (await StampedShadowsAsync(a.OfficeId, op.Id)).ShouldBe(new[] { inA });
    }

    [Fact]
    public async Task A_legacy_shadow_follows_an_email_change_too_and_is_stamped()
    {
        var (a, _) = await OfficesAsync();
        var token = NewToken();
        var op = await HostUserAsync(token, Supervisor);
        var legacyId = await OfficeAccountAsync(a.OfficeId, op.Email!, op.Email!, Supervisor);
        var newEmail = $"moved.{token}@example.test";

        await EditHostUserAsync(op.Id, Supervisor, d => d.Email = newEmail);

        var shadow = (await OfficeAccountByIdAsync(a.OfficeId, legacyId))!;
        shadow.Email.ShouldBe(newEmail);
        shadow.Stamp.ShouldBe(op.Id.ToString("D"));
    }

    // ------------------------------------------------------------------ Change role

    [Fact]
    public async Task Change_role_locks_a_supervisors_office_logins_in_every_office_and_the_next_entry_has_only_the_new_role()
    {
        // A Supervisor has no assignment rows, so before 2026-10-27 their shadows stayed active with the
        // Supervisor role after a move to Intake Staff.
        var (a, b) = await OfficesAsync();
        var sup = await HostUserAsync(NewToken(), Supervisor);
        var inA = await EnsureAsync(a.OfficeId, sup.Id, Supervisor);
        var inB = await EnsureAsync(b.OfficeId, sup.Id, Supervisor);

        await _staff.ChangeRoleAsync(sup.Id, new ChangeInternalUserRoleDto { RoleName = IntakeStaff });

        (await OfficeAccountByIdAsync(a.OfficeId, inA))!.Active.ShouldBeFalse();
        (await OfficeAccountByIdAsync(b.OfficeId, inB))!.Active.ShouldBeFalse();

        (await EnsureAsync(a.OfficeId, sup.Id, IntakeStaff)).ShouldBe(inA);
        (await OfficeAccountByIdAsync(a.OfficeId, inA))!.Roles.ShouldBe(new[] { IntakeStaff });
    }

    [Fact]
    public async Task The_system_owner_or_an_it_admin_can_move_someone_to_it_admin_and_back_through_change_role()
    {
        var (a, _) = await OfficesAsync();
        var sup = await HostUserAsync(NewToken(), Supervisor);
        var inA = await EnsureAsync(a.OfficeId, sup.Id, Supervisor);

        foreach (var (role, caller) in new[] { (ItAdmin, AdminRole), (Supervisor, ItAdmin) })
        {
            using (_currentTenant.Change(null))
            using (WithCurrentUser.Run(_principal, Guid.NewGuid(), caller))
            {
                (await _staff.ChangeRoleAsync(sup.Id, new ChangeInternalUserRoleDto { RoleName = role })).Role.ShouldBe(role);
            }

            var held = await WithUnitOfWorkAsync(async () =>
            {
                using (_currentTenant.Change(null))
                {
                    return await _users.GetRolesAsync(await _users.GetByIdAsync(sup.Id));
                }
            }, requiresNew: true);
            held.ShouldBe(new[] { role });
            (await OfficeAccountByIdAsync(a.OfficeId, inA))!.Active.ShouldBeFalse();
        }
    }
}
