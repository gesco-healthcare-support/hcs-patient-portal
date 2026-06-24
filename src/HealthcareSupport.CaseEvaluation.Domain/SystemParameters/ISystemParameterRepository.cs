using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.SystemParameters;

/// <summary>
/// Custom repository for the per-tenant <c>SystemParameter</c> singleton.
/// </summary>
public interface ISystemParameterRepository : IRepository<SystemParameter, Guid>
{
    /// <summary>
    /// Returns the singleton row for the current tenant scope, or null if
    /// the tenant has not been seeded yet. The data seed contributor inserts
    /// exactly one row per tenant on tenant-create.
    /// </summary>
    Task<SystemParameter?> GetCurrentTenantAsync(CancellationToken cancellationToken = default);
}
