using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.HostOperators;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Saas;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Xunit;
using IdentityRole = Volo.Abp.Identity.IdentityRole;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Seeding;

/// <summary>
/// The office admin that ABP's identity seed creates for every tenant is NOT created for the
/// synthetic TEST office outside Development. That seed falls back to a publicly known default
/// password, and the TEST office also exists in production, so its admin would be a known
/// credential. The host admin invites the TEST office's staff instead.
///
/// <para>The TEST office still gets the admin ROLE, with the grants ABP gives it, because an IT
/// Admin's switch into an office lands them as a shadow user holding that role.</para>
///
/// <para>Every other office is the decoy: it must still get its admin, through ABP's own path. The
/// environment comes from ABP's <see cref="IAbpHostEnvironment"/>, which the rig leaves at its
/// Production default.</para>
/// </summary>
public class TestOfficeAdminSeedTests : SeedContributorTestBase
{
    [Fact]
    public async Task OutsideDevelopment_TheTestOfficeGetsNoAdmin_ButAnyOtherOfficeDoes()
    {
        var testOffice = await CreatePracticeAsync(OfficeSeedData.TestOffice.TenantName);
        var otherOffice = await CreatePracticeAsync("TEST-practice-" + Guid.NewGuid().ToString("N")[..10]);
        var seeder = GetRequiredService<IdentityDataSeedContributor>();

        await SeedAsync(seeder, AdminContext(testOffice));
        await SeedAsync(seeder, AdminContext(otherOffice));

        seeder.ShouldBeOfType<CaseEvaluationIdentityDataSeedContributor>();
        (await UsersOf(testOffice)).ShouldBeEmpty();
        (await UsersOf(otherOffice)).ShouldHaveSingleItem().Email.ShouldBe("test.admin@example.test");
    }

    [Fact]
    public async Task OutsideDevelopment_TheTestOfficeGetsTheAdminRoleWithItsGrants_ButNoUser()
    {
        var testOffice = await CreatePracticeAsync(OfficeSeedData.TestOffice.TenantName);
        var otherOffice = await CreatePracticeAsync("TEST-practice-" + Guid.NewGuid().ToString("N")[..10]);
        var identitySeeder = GetRequiredService<IdentityDataSeedContributor>();
        var permissionSeeder = GetRequiredService<PermissionDataSeedContributor>();

        foreach (var office in new[] { testOffice, otherOffice })
        {
            await SeedAsync(identitySeeder, AdminContext(office));
            await SeedAsync(identitySeeder, AdminContext(office)); // the migrator reruns on every deploy
            await SeedAsync(permissionSeeder, new DataSeedContext(office));
        }

        var role = (await RolesOf(testOffice)).Where(r => r.Name == CaseEvaluationIdentityDataSeedContributor.AdminRoleName)
            .ShouldHaveSingleItem();
        role.IsStatic.ShouldBeTrue();
        role.IsPublic.ShouldBeTrue();
        role.TenantId.ShouldBe(testOffice);
        (await UsersOf(testOffice)).ShouldBeEmpty();

        // The decoy took ABP's own path (user plus role), so its admin grants are the reference set.
        var testOfficeGrants = await AdminGrantNamesOf(testOffice);
        testOfficeGrants.ShouldNotBeEmpty();
        testOfficeGrants.ShouldBe(await AdminGrantNamesOf(otherOffice), ignoreOrder: true);
        (await UsersOf(otherOffice)).ShouldHaveSingleItem().Email.ShouldBe("test.admin@example.test");
    }

    /// <summary>
    /// The step of an IT Admin's switch into an office that needs the role: the grant lands the
    /// operator as their own shadow user holding the office's admin role. Without the role this
    /// throws. The token exchange around it runs in the AuthServer and is not exercised here.
    /// </summary>
    [Fact]
    public async Task OutsideDevelopment_AnItAdminsShadowUserCanTakeTheTestOfficesAdminRole()
    {
        var testOffice = await CreatePracticeAsync(OfficeSeedData.TestOffice.TenantName);
        await SeedAsync(GetRequiredService<IdentityDataSeedContributor>(), AdminContext(testOffice));
        var operatorId = await CreateHostOperatorAsync("test.itadmin@example.test");

        var shadowId = await InScopeAsync(null, () => GetRequiredService<IIntakeShadowUserProvisioner>()
            .EnsureShadowUserAsync(testOffice, operatorId, CaseEvaluationIdentityDataSeedContributor.AdminRoleName));

        (await UsersOf(testOffice)).ShouldHaveSingleItem().Id.ShouldBe(shadowId);
        (await InScopeAsync(testOffice, async () =>
        {
            var userManager = GetRequiredService<IdentityUserManager>();
            return await userManager.IsInRoleAsync(
                await userManager.GetByIdAsync(shadowId), CaseEvaluationIdentityDataSeedContributor.AdminRoleName);
        })).ShouldBeTrue();
    }

    [Fact]
    public void TheReplacementRunsOnce_UnderTheFrameworkContributorsEntry()
    {
        var contributors = GetRequiredService<IOptions<AbpDataSeedOptions>>().Value.Contributors;

        contributors.ShouldContain(typeof(IdentityDataSeedContributor));
        contributors.ShouldNotContain(typeof(CaseEvaluationIdentityDataSeedContributor));
    }

    internal static DataSeedContext AdminContext(Guid officeId) =>
        new DataSeedContext(officeId)
            .WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, "test.admin@example.test")
            .WithProperty(IdentityDataSeedContributor.AdminPasswordPropertyName, "TEST-Passw0rd!");

    private Task<List<IdentityUser>> UsersOf(Guid officeId) =>
        InScopeAsync(officeId, () => GetRequiredService<IRepository<IdentityUser, Guid>>().GetListAsync());

    private Task<List<IdentityRole>> RolesOf(Guid officeId) =>
        InScopeAsync(officeId, () => GetRequiredService<IRepository<IdentityRole, Guid>>().GetListAsync());

    private Task<List<string>> AdminGrantNamesOf(Guid officeId) =>
        InScopeAsync(officeId, async () => (await GetRequiredService<IPermissionGrantRepository>()
                .GetListAsync(RolePermissionValueProvider.ProviderName, CaseEvaluationIdentityDataSeedContributor.AdminRoleName))
            .Select(grant => grant.Name)
            .ToList());

    private Task<Guid> CreateHostOperatorAsync(string email) =>
        InScopeAsync(null, async () =>
        {
            var user = new IdentityUser(Guid.NewGuid(), email, email) { Name = "TEST", Surname = "Operator" };
            (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "TEST-Passw0rd!")).CheckErrors();
            return user.Id;
        });
}

/// <summary>
/// In Development the TEST office keeps its admin, so a fresh local stack can still sign in to it.
/// </summary>
public class TestOfficeAdminSeedDevelopmentTests : SeedContributorTestBase
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        base.SetAbpApplicationCreationOptions(options);
        options.Environment = Environments.Development;
    }

    [Fact]
    public async Task InDevelopment_TheTestOfficeGetsItsAdmin()
    {
        var testOffice = await CreatePracticeAsync(OfficeSeedData.TestOffice.TenantName);

        await SeedAsync(GetRequiredService<IdentityDataSeedContributor>(), TestOfficeAdminSeedTests.AdminContext(testOffice));

        (await InScopeAsync(testOffice, () => GetRequiredService<IRepository<IdentityUser, Guid>>().GetListAsync()))
            .ShouldHaveSingleItem().Email.ShouldBe("test.admin@example.test");
    }
}
