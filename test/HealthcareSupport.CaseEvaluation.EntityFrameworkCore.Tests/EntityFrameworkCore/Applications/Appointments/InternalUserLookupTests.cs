using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The Approve modal's Responsible-User list (<c>GetInternalUserLookupAsync</c>) with internal
/// users actually present. The existing lifecycle test runs it against an office with none, so the
/// de-duplication, the name/email filter and the surname-name-email ordering were never reached.
///
/// <para>This is a ROLE lookup, so it carries an OFFICE decoy: office B holds a user in the same
/// role, and office B's role is created first with an id that sorts before any random one. ABP
/// resolves a role name to the lowest-id role of that name, so a lookup that lost its office
/// filter would list office B's user on every run, not only on lucky ones.</para>
/// </summary>
public class InternalUserLookupTests : CaseEvaluationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private const string IntakeStaff = "Intake Staff";
    private const string StaffSupervisor = "Staff Supervisor";

    private readonly IAppointmentApprovalAppService _approval;
    private readonly IdentityUserManager _users;
    private readonly IdentityRoleManager _roles;
    private readonly ICurrentTenant _currentTenant;

    public InternalUserLookupTests()
    {
        _approval = GetRequiredService<IAppointmentApprovalAppService>();
        _users = GetRequiredService<IdentityUserManager>();
        _roles = GetRequiredService<IdentityRoleManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Lookup_ListsThisOfficesInternalUsersOnceEach_FiltersThem_AndOrdersThem()
    {
        var decoy = await InOffice(TenantsTestData.TenantBRef, async () =>
        {
            await RoleUserSeeder.CreateRoleSortingFirstAsync(_roles, TenantsTestData.TenantBRef, IntakeStaff, ordinal: 1);
            return await RoleUserSeeder.CreateUserInRoleAsync(_users, _roles, TenantsTestData.TenantBRef, IntakeStaff, "lookup-decoy");
        });
        var intake = await InOffice(TenantsTestData.TenantARef,
            () => RoleUserSeeder.CreateUserInRoleAsync(_users, _roles, TenantsTestData.TenantARef, IntakeStaff, "lookup-b-intake"));
        var supervisor = await InOffice(TenantsTestData.TenantARef, async () =>
        {
            var email = await RoleUserSeeder.CreateUserInRoleAsync(_users, _roles, TenantsTestData.TenantARef, StaffSupervisor, "lookup-a-both");
            // The same person in a second internal role must still be listed once.
            var user = (await _users.FindByEmailAsync(email)).ShouldNotBeNull();
            (await _users.AddToRoleAsync(user, IntakeStaff)).Succeeded.ShouldBeTrue();
            return email;
        });

        var all = await InOffice(TenantsTestData.TenantARef,
            () => _approval.GetInternalUserLookupAsync(new LookupRequestDto { MaxResultCount = 50 }));
        var filtered = await InOffice(TenantsTestData.TenantARef,
            () => _approval.GetInternalUserLookupAsync(new LookupRequestDto { Filter = "  LOOKUP-B-INTAKE  ", MaxResultCount = 50 }));

        var emails = all.Items.Select(i => i.DisplayName).ToList();
        emails.ShouldNotContain(decoy);
        emails.Count(e => e == supervisor).ShouldBe(1);
        emails.IndexOf(supervisor).ShouldBeLessThan(emails.IndexOf(intake)); // no names set, so ordered by email
        all.TotalCount.ShouldBe(emails.Count);
        filtered.Items.ShouldHaveSingleItem().DisplayName.ShouldBe(intake);
        filtered.TotalCount.ShouldBe(1);
    }

    private Task<T> InOffice<T>(Guid officeId, Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                return await action();
            }
        });
}
