using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.MultiTenancy;

/// <summary>
/// The single audited place where "do something for every office" happens under the
/// database-per-office model. ABP cannot query across office databases in one
/// statement, so cross-office work must enumerate offices from the tenant registry
/// (which lives in the shared management database, unaffected by database-per-office)
/// and run per office inside that office's connection via
/// <see cref="Volo.Abp.MultiTenancy.ICurrentTenant.Change"/>. Consumed by the recurring
/// jobs (process every office) and the host dashboard (sum a value across offices);
/// this is the reusable cross-office aggregation seam (decision D8).
/// </summary>
public interface ITenantWorkRunner
{
    /// <summary>
    /// Runs <paramref name="work"/> once per office, each invocation scoped to that
    /// office's database; the office id is passed to the delegate. Exceptions
    /// propagate (a failing office aborts the run) -- callers needing best-effort
    /// iteration must catch inside the delegate.
    /// </summary>
    Task ForEachOfficeAsync(Func<Guid, Task> work);

    /// <summary>
    /// Runs <paramref name="selector"/> once per office (scoped to that office's
    /// database) and returns one result per office, in registry order. Callers
    /// aggregate the returned values (e.g. <c>.Sum()</c>).
    /// </summary>
    Task<List<TResult>> AggregateAcrossOfficesAsync<TResult>(Func<Guid, Task<TResult>> selector);
}
