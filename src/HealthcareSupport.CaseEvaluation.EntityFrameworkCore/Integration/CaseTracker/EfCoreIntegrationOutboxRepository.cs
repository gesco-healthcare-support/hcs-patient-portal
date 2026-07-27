using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Custom outbox repository with an atomic status-gated lease. Binds
/// <c>CaseEvaluationDbContext</c> -- the same choice the notification outbox repository makes --
/// because ABP resolves the per-office connection at runtime, so one context type serves both host
/// and office databases.
/// </summary>
public class EfCoreIntegrationOutboxRepository
    : EfCoreRepository<CaseEvaluationDbContext, IntegrationOutboxItem, Guid>, IIntegrationOutboxRepository
{
    public EfCoreIntegrationOutboxRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<bool> TryLeaseAsync(
        Guid id,
        DateTime nowUtc,
        DateTime leaseUntil,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        // Single UPDATE ... WHERE <claim gate>. The row lock serializes racing drains: the winner
        // flips LockedUntil into the future so a concurrent call's gate no longer matches and it
        // updates 0 rows. EF's query filters (IMultiTenant + soft delete) apply to the WHERE, so
        // this only ever touches the current office's live rows.
        var affected = await dbSet
            .Where(x => x.Id == id
                && x.Status == IntegrationOutboxStatus.Pending
                && (x.LockedUntil == null || x.LockedUntil <= nowUtc)
                && (x.NextAttemptAt == null || x.NextAttemptAt <= nowUtc))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.LockedUntil, leaseUntil),
                cancellationToken);

        return affected == 1;
    }
}
