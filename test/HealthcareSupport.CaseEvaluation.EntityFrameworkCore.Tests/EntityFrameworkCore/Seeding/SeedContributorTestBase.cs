using System;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Seeding;

/// <summary>
/// Shared scaffolding for the production seed-contributor tests: a FRESH practice per test (the
/// rig's seeded offices already carry their catalogs), and a way to run a contributor exactly as
/// the migrator does, inside a unit of work, for a given scope.
///
/// <para>Each test application has its own database (#1056), so rows a seeder writes, including
/// fixed-id rows, never reach another test.</para>
/// </summary>
public abstract class SeedContributorTestBase : CaseEvaluationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    protected ICurrentTenant CurrentTenant { get; }

    private readonly ITenantManager _tenantManager;
    private readonly IRepository<Tenant, Guid> _tenants;

    protected SeedContributorTestBase()
    {
        CurrentTenant = GetRequiredService<ICurrentTenant>();
        _tenantManager = GetRequiredService<ITenantManager>();
        _tenants = GetRequiredService<IRepository<Tenant, Guid>>();
    }

    /// <summary>Creates a practice (tenant) with no catalog rows and returns its id.</summary>
    protected Task<Guid> CreatePracticeAsync(string? name = null) =>
        InScopeAsync(null, async () =>
        {
            var practice = await _tenantManager.CreateAsync(name ?? "TEST-practice-" + Guid.NewGuid().ToString("N")[..10]);
            await _tenants.InsertAsync(practice, autoSave: true);
            return practice.Id;
        });

    /// <summary>Runs <paramref name="contributor"/> for <paramref name="context"/> in its own unit of work.</summary>
    protected Task SeedAsync(IDataSeedContributor contributor, DataSeedContext context) =>
        InScopeAsync(context.TenantId, async () =>
        {
            await contributor.SeedAsync(context);
            return true;
        });

    /// <summary>Runs <paramref name="action"/> in a unit of work with the current tenant set to <paramref name="tenantId"/>.</summary>
    protected Task<T> InScopeAsync<T>(Guid? tenantId, Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (CurrentTenant.Change(tenantId))
            {
                return await action();
            }
        });
}
