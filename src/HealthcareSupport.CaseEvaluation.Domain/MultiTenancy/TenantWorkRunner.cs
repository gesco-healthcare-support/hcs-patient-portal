using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.MultiTenancy;

/// <summary>
/// Default <see cref="ITenantWorkRunner"/>: enumerates office ids from the tenant
/// registry and runs the supplied delegate per office inside
/// <see cref="ICurrentTenant.Change"/>. Mirrors the per-tenant migration loop in
/// <c>CaseEvaluationDbMigrationService</c> -- the pattern proven to route repository
/// calls to each office's database under database-per-office.
/// </summary>
public class TenantWorkRunner : ITenantWorkRunner, ITransientDependency
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;

    public TenantWorkRunner(ITenantRepository tenantRepository, ICurrentTenant currentTenant)
    {
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
    }

    public async Task ForEachOfficeAsync(Func<Guid, Task> work)
    {
        Check.NotNull(work, nameof(work));

        foreach (var officeId in await GetOfficeIdsAsync())
        {
            using (_currentTenant.Change(officeId))
            {
                await work(officeId);
            }
        }
    }

    public async Task<List<TResult>> AggregateAcrossOfficesAsync<TResult>(Func<Guid, Task<TResult>> selector)
    {
        Check.NotNull(selector, nameof(selector));

        var results = new List<TResult>();
        foreach (var officeId in await GetOfficeIdsAsync())
        {
            using (_currentTenant.Change(officeId))
            {
                results.Add(await selector(officeId));
            }
        }

        return results;
    }

    /// <summary>
    /// Office ids come from the tenant registry (shared management database), read in
    /// host context so the multi-tenant filter does not hide rows.
    /// </summary>
    private async Task<List<Guid>> GetOfficeIdsAsync()
    {
        using (_currentTenant.Change(null))
        {
            var tenants = await _tenantRepository.GetListAsync();
            return tenants.Select(t => t.Id).ToList();
        }
    }
}
