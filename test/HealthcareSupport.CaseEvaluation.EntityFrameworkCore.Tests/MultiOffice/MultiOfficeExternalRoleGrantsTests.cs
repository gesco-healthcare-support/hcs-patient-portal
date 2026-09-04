using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Phase 3 task 4 (2026-09-04) -- pins the ROLE SET that receives the external booking
/// baseline, which nothing asserted before.
///
/// <para>WHY A SEPARATE TEST WHEN ExternalUserRoleGrantsTests ALREADY EXISTS. That one
/// calls <c>BookingBaselineGrants()</c> statically, so it pins the CONTENTS of the grant
/// list and never reaches <c>SeedAsync</c>. Measured 2026-09-04: removing
/// <c>"Defense Attorney"</c> from the seeded role array left **1,919 tests green** --
/// Domain 693, Application 1,127, MultiOffice 99, zero failures. A whole external role
/// silently receiving no booking permissions, and nothing noticed.</para>
///
/// <para>THE FAILURE MODE IS NOT "the role disappears". <c>SeedAsync</c> creates the four
/// roles in one place and grants to them in another, so a role dropped from the grant loop
/// still EXISTS and is still assignable -- it simply holds nothing. A user given that role
/// logs in successfully and finds every booking action refused.</para>
///
/// <para>This test therefore exercises the loop rather than the list: it seeds for real and
/// asks the permission store what each role actually holds. Mirrors
/// <c>MultiOfficeImpersonationRoleTests</c>, which does the same for the internal Staff
/// Supervisor role.</para>
///
/// <para>CHARACTERIZATION. It asserts the four-role set exactly as it stands today. It does
/// NOT couple the seeder to <c>AppointmentAccessorRules.RecognizedExternalRoles</c>, which
/// is a THIRD list of the same four names -- whether these lists should be one is a design
/// question, recorded in the backlog, and not settled by a test.</para>
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeExternalRoleGrantsTests : CaseEvaluationMultiOfficeTestBase
{
    // Matches RolePermissionValueProvider.ProviderName ("R"), as in
    // MultiOfficeImpersonationRoleTests.
    private const string RoleProviderName = "R";

    /// <summary>
    /// The four external booking roles, as the seeder grants them today.
    ///
    /// <para>DELIBERATELY A LITERAL, not read from the seeder. Deriving it from the code
    /// under test would make this assertion vacuous -- the list and the loop would move
    /// together and the test could never fail. The duplication is the point.</para>
    /// </summary>
    private static readonly string[] ExternalBookingRoles =
    {
        "Patient",
        "Claim Examiner",
        "Applicant Attorney",
        "Defense Attorney",
    };

    private readonly ExternalUserRoleDataSeedContributor _externalRoleSeeder;
    private readonly IPermissionManager _permissionManager;
    private readonly ICurrentTenant _currentTenant;

    public MultiOfficeExternalRoleGrantsTests()
    {
        _externalRoleSeeder = GetRequiredService<ExternalUserRoleDataSeedContributor>();
        _permissionManager = GetRequiredService<IPermissionManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Every_external_booking_role_receives_the_whole_baseline_grant_set()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        await SeedExternalRolesAsync(officeA.OfficeId);

        var baseline = ExternalUserRoleDataSeedContributor.BookingBaselineGrants().ToList();
        baseline.ShouldNotBeEmpty("a baseline of zero grants would make every assertion below vacuous");

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                foreach (var role in ExternalBookingRoles)
                {
                    foreach (var permission in baseline)
                    {
                        (await IsGrantedAsync(permission, role)).ShouldBeTrue(
                            $"external booking role '{role}' must hold '{permission}' -- "
                            + "a role that exists but holds nothing fails every booking action "
                            + "for a user who can still log in");
                    }
                }
            }
        }, requiresNew: true);
    }

    private Task SeedExternalRolesAsync(Guid officeId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                await _externalRoleSeeder.SeedAsync(new DataSeedContext(officeId));
            }
        }, requiresNew: true);

    private async Task<bool> IsGrantedAsync(string permission, string roleName)
    {
        var result = await _permissionManager.GetAsync(permission, RoleProviderName, roleName);
        return result.IsGranted;
    }
}
