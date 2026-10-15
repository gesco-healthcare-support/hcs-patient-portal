using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Seeding;

/// <summary>
/// The "already seeded" returns of two per-office seeders, reached by seeding a new practice twice:
/// <see cref="SystemParameterDataSeedContributor"/> keeps one settings row, and
/// <see cref="ExternalUserRoleDataSeedContributor"/> keeps one role per external user type.
///
/// <para>Both checks are per office, and both carry an OFFICE decoy from the rig: office A's seeded
/// settings row, and the host's external roles. A check that lost its office filter would find
/// those and seed nothing for the new practice.</para>
/// </summary>
public class SeedIdempotencyTests : SeedContributorTestBase
{
    [Fact]
    public async Task SystemParameters_SeededTwice_LeaveOneRowForThePractice()
    {
        var seeder = GetRequiredService<SystemParameterDataSeedContributor>();
        var rows = GetRequiredService<IRepository<SystemParameter, Guid>>();
        var practiceId = await CreatePracticeAsync();

        await SeedAsync(seeder, new DataSeedContext(practiceId));
        await SeedAsync(seeder, new DataSeedContext(practiceId));

        (await InScopeAsync(practiceId, () => rows.GetListAsync())).ShouldHaveSingleItem().TenantId.ShouldBe(practiceId);
    }

    [Fact]
    public async Task ExternalRoles_SeededTwice_LeaveOneRolePerTypeForThePractice()
    {
        var seeder = GetRequiredService<ExternalUserRoleDataSeedContributor>();
        var roles = GetRequiredService<IRepository<IdentityRole, Guid>>();
        var practiceId = await CreatePracticeAsync();

        await SeedAsync(seeder, new DataSeedContext(practiceId));
        await SeedAsync(seeder, new DataSeedContext(practiceId));

        var names = (await InScopeAsync(practiceId, () => roles.GetListAsync())).Select(r => r.Name).ToList();
        foreach (var roleName in ExternalRoleConsts.All)
        {
            names.Count(n => n == roleName).ShouldBe(1, roleName);
        }
    }
}
