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
/// The per-office feed record. Binds <c>CaseEvaluationDbContext</c> for the same reason the outbox repository
/// does: ABP resolves the office's connection at runtime, so one context type serves every database.
/// </summary>
public class EfCoreCaseTrackerFeedStateRepository
    : EfCoreRepository<CaseEvaluationDbContext, CaseTrackerFeedState, Guid>, ICaseTrackerFeedStateRepository
{
    public EfCoreCaseTrackerFeedStateRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<CaseTrackerFeedState?> FindCurrentAsync(CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        // One row per office (unique index on TenantId); the tenant filter picks this office's.
        return await dbSet.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task RecordRequestAsync(
        Guid id,
        DateTime nowUtc,
        long acknowledged,
        long highestIssued,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        // ONE statement, so it is atomic without a transaction. Every SET reads the row as it was BEFORE the
        // update (SQL semantics on both SQL Server and SQLite), which is why LastAdvancedAt can compare against
        // the old acknowledged position while the same statement raises it.
        await dbSet
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.LastRequestAt, nowUtc)
                    .SetProperty(x => x.LastAdvancedAt, x => acknowledged > x.AcknowledgedPosition ? nowUtc : x.LastAdvancedAt)
                    .SetProperty(x => x.AcknowledgedPosition, x => acknowledged > x.AcknowledgedPosition ? acknowledged : x.AcknowledgedPosition)
                    .SetProperty(x => x.HighestIssuedPosition, x => highestIssued > x.HighestIssuedPosition ? highestIssued : x.HighestIssuedPosition),
                cancellationToken);
    }

    public async Task SetSilenceAlertedAsync(Guid id, DateTime? alertedAt, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        await dbSet
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SilenceAlertedAt, alertedAt), cancellationToken);
    }

    public async Task SetStallAlertedAsync(Guid id, DateTime? alertedAt, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        await dbSet
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.StallAlertedAt, alertedAt), cancellationToken);
    }

    public async Task SetCursorAheadAlertedAsync(Guid id, DateTime? alertedAt, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        await dbSet
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.CursorAheadAlertedAt, alertedAt), cancellationToken);
    }
}
