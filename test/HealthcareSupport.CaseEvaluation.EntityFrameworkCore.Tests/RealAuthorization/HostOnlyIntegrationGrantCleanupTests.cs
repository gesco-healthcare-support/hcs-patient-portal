using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Permissions;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Saas.Tenants;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// <see cref="HostOnlyIntegrationGrantCleanupContributor"/> deletes one office's grant rows of the
/// now Host-only failures-list permission, and nothing else.
///
/// <para>A DELETE IS PROVEN ONLY AGAINST ROWS IT MUST NOT DELETE. Each test seeds decoys beside the
/// targets, and every decoy is re-read afterwards:</para>
/// <list type="bullet">
///   <item><description>the same office's <c>PushToCaseTracker</c> row, which the in-office push button
///   still uses;</description></item>
///   <item><description>the same office's row of an unrelated permission;</description></item>
///   <item><description>ANOTHER office's row of the target permission, in the SAME database, so only the
///   office scoping can spare it;</description></item>
///   <item><description>the host's row of the target permission, which is the grant that still
///   works.</description></item>
/// </list>
///
/// <para>Two offices sharing one database is deliberate: with a database per office the other
/// office's row would be spared by the connection alone, and the test would not see a filter that
/// ignored the office.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class HostOnlyIntegrationGrantCleanupTests : CaseEvaluationRealAuthorizationTestBase
{
    private const string Target = "CaseEvaluation.Appointments.ViewIntegrationDeadLetters";
    private const string PushPermission = "CaseEvaluation.Appointments.PushToCaseTracker";
    private const string UnrelatedPermission = "CaseEvaluation.DoctorAvailabilities";

    private readonly ICurrentTenant _currentTenant;
    private readonly IPermissionGrantRepository _grants;

    public HostOnlyIntegrationGrantCleanupTests()
    {
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _grants = GetRequiredService<IPermissionGrantRepository>();
    }

    [Fact]
    public void PermissionName_IsTheFailuresListPermission()
    {
        HostOnlyIntegrationGrantCleanupContributor.PermissionName
            .ShouldBe(CaseEvaluationPermissions.Appointments.ViewIntegrationDeadLetters);
    }

    [Fact]
    public async Task Cleanup_RemovesOnlyThatOfficesRowsOfTheTarget_AndLeavesEveryDecoy()
    {
        var seeded = await SeedAsync();

        await RunCleanupAsync(seeded.OfficeId);

        (await ReadAsync(seeded.OfficeId, seeded.All)).ShouldBe(seeded.OfficeDecoys, ignoreOrder: true);
        (await ReadAsync(seeded.OtherOfficeId, seeded.All)).ShouldBe(seeded.OtherOfficeDecoys, ignoreOrder: true);
        (await ReadAsync(null, seeded.All)).ShouldBe(seeded.HostDecoys, ignoreOrder: true);
    }

    [Fact]
    public async Task Cleanup_IsANoOp_WhenItRunsAgain()
    {
        var seeded = await SeedAsync();
        await RunCleanupAsync(seeded.OfficeId);
        var afterFirst = await ReadEverywhereAsync(seeded);

        await RunCleanupAsync(seeded.OfficeId);

        (await ReadEverywhereAsync(seeded)).ShouldBe(afterFirst, ignoreOrder: true);
    }

    [Fact]
    public async Task Cleanup_IsANoOp_OnTheHostPass()
    {
        var seeded = await SeedAsync();

        await RunCleanupAsync(null);

        // Everything is still there, the office's targets included.
        (await ReadEverywhereAsync(seeded)).ShouldBe(seeded.All, ignoreOrder: true);
    }

    // ------------------------------------------------------------------------

    private Task RunCleanupAsync(Guid? officeId) =>
        WithUnitOfWorkAsync(
            () => GetRequiredService<HostOnlyIntegrationGrantCleanupContributor>()
                .SeedAsync(new DataSeedContext(officeId)),
            requiresNew: true);

    private async Task<List<GrantRow>> ReadEverywhereAsync(Seeded seeded)
    {
        var rows = new List<GrantRow>();
        rows.AddRange(await ReadAsync(seeded.OfficeId, seeded.All));
        rows.AddRange(await ReadAsync(seeded.OtherOfficeId, seeded.All));
        rows.AddRange(await ReadAsync(null, seeded.All));
        return rows;
    }

    /// <summary>The seeded rows still present in one scope, by id, with every identifying column.</summary>
    private Task<List<GrantRow>> ReadAsync(Guid? scope, IReadOnlyCollection<GrantRow> seededRows)
    {
        var ids = seededRows.Select(r => r.Id).ToHashSet();
        return WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(scope))
            {
                return (await _grants.GetListAsync())
                    .Where(g => ids.Contains(g.Id))
                    .Select(GrantRow.From)
                    .ToList();
            }
        }, requiresNew: true);
    }

    /// <summary>
    /// Two fresh offices sharing the cleanup database, plus rows in each and at the host. Fresh per
    /// test, so no test depends on another having run first.
    /// </summary>
    private async Task<Seeded> SeedAsync()
    {
        await GetFixtureAsync();
        var tenantManager = GetRequiredService<ITenantManager>();
        var tenantRepository = GetRequiredService<IRepository<Tenant, Guid>>();
        var label = Guid.NewGuid().ToString("N")[..8];

        return await WithUnitOfWorkAsync(async () =>
        {
            Guid officeId;
            Guid otherOfficeId;
            GrantRow hostRow;
            using (_currentTenant.Change(null))
            {
                officeId = await CreateOfficeAsync(tenantManager, tenantRepository, $"F2-cleanup-{label}-a");
                otherOfficeId = await CreateOfficeAsync(tenantManager, tenantRepository, $"F2-cleanup-{label}-b");
                hostRow = await InsertAsync(Target, "R", $"TEST-cleanup-host-{label}", null);
            }

            GrantRow targetByRole;
            GrantRow targetByUser;
            GrantRow push;
            GrantRow unrelated;
            using (_currentTenant.Change(officeId))
            {
                targetByRole = await InsertAsync(Target, "R", $"TEST-cleanup-role-{label}", officeId);
                targetByUser = await InsertAsync(Target, "U", Guid.NewGuid().ToString(), officeId);
                push = await InsertAsync(PushPermission, "R", $"TEST-cleanup-role-{label}", officeId);
                unrelated = await InsertAsync(UnrelatedPermission, "R", $"TEST-cleanup-role-{label}", officeId);
            }

            GrantRow otherOfficeTarget;
            using (_currentTenant.Change(otherOfficeId))
            {
                otherOfficeTarget = await InsertAsync(Target, "R", $"TEST-cleanup-role-{label}", otherOfficeId);
            }

            return new Seeded(
                officeId,
                otherOfficeId,
                OfficeTargets: new[] { targetByRole, targetByUser },
                OfficeDecoys: new[] { push, unrelated },
                OtherOfficeDecoys: new[] { otherOfficeTarget },
                HostDecoys: new[] { hostRow });
        }, requiresNew: true);
    }

    private static async Task<Guid> CreateOfficeAsync(
        ITenantManager tenantManager, IRepository<Tenant, Guid> tenantRepository, string name)
    {
        var office = await tenantManager.CreateAsync(name);
        office.SetDefaultConnectionString(RealAuthorizationTestDatabase.CleanupOfficesConnectionString);
        await tenantRepository.InsertAsync(office, autoSave: true);
        return office.Id;
    }

    /// <summary>Written straight to the table: the permission manager now refuses the office-side ones.</summary>
    private async Task<GrantRow> InsertAsync(string name, string provider, string key, Guid? tenantId)
    {
        var grant = new PermissionGrant(Guid.NewGuid(), name, provider, key, tenantId);
        await _grants.InsertAsync(grant, autoSave: true);
        return GrantRow.From(grant);
    }

    private sealed record GrantRow(Guid Id, string Name, string ProviderName, string? ProviderKey, Guid? TenantId)
    {
        public static GrantRow From(PermissionGrant g) => new(g.Id, g.Name, g.ProviderName, g.ProviderKey, g.TenantId);
    }

    private sealed record Seeded(
        Guid OfficeId,
        Guid OtherOfficeId,
        GrantRow[] OfficeTargets,
        GrantRow[] OfficeDecoys,
        GrantRow[] OtherOfficeDecoys,
        GrantRow[] HostDecoys)
    {
        public IReadOnlyCollection<GrantRow> All =>
            OfficeTargets.Concat(OfficeDecoys).Concat(OtherOfficeDecoys).Concat(HostDecoys).ToList();
    }
}
