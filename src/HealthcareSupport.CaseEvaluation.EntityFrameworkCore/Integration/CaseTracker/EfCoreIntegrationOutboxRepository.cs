using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
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

    public async Task<int> CountSentSinceAsync(
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        // Counted in the database, not in memory: a flood is exactly when this must not materialise
        // thousands of rows. EF's query filters scope it to the current office's live rows.
        return await dbSet
            .Where(x => x.Status == IntegrationOutboxStatus.Sent && x.SentAt >= sinceUtc)
            .CountAsync(cancellationToken);
    }

    public async Task<bool> HasIntakeAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        // Deliberately NO status filter: every status counts (see the interface). EF's query filters
        // scope it to the current office's live rows -- which is exactly why a purge would break it.
        return await dbSet
            .AnyAsync(
                x => x.AppointmentId == appointmentId && x.MessageType == IntegrationMessageType.Intake,
                cancellationToken);
    }

    public async Task AcquireAppointmentLockAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        // The SQLite test provider has no application locks. Deliberately a no-op there rather than
        // a fake: the interface says tests prove the lock is REQUESTED in order, not that it blocks.
        if (!dbContext.Database.IsSqlServer())
        {
            return;
        }

        var status = new SqlParameter("@status", SqlDbType.Int) { Direction = ParameterDirection.Output };

        // Transaction-owned, so the database itself releases it at commit or rollback -- there is no
        // release call to forget. Application locks are per database, and each office has its own,
        // so the name only needs to be unique within an office.
        await dbContext.Database.ExecuteSqlRawAsync(
            "EXEC @status = sp_getapplock @Resource = @resource, @LockMode = N'Exclusive', " +
            "@LockOwner = N'Transaction', @LockTimeout = @timeoutMs;",
            new object[]
            {
                status,
                new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = AppointmentLockResource(appointmentId) },
                new SqlParameter("@timeoutMs", SqlDbType.Int) { Value = IntegrationOutboxConsts.AppointmentLockTimeoutMilliseconds },
            },
            cancellationToken);

        // 0 = granted at once, 1 = granted after waiting. Negative = not granted: -1 timeout,
        // -2 cancelled, -3 chosen as deadlock victim, -999 call error. Carrying on unlocked would
        // silently reopen the race, so fail loudly instead.
        var result = (int)status.Value;
        if (result < 0)
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"Case Tracker ordering lock for appointment {appointmentId:D} was not granted (sp_getapplock returned {result})."));
        }
    }

    /// <summary>
    /// The application-lock name for one appointment. Public so a live check against SQL Server can
    /// take the very same lock the enqueue paths take.
    /// </summary>
    public static string AppointmentLockResource(Guid appointmentId) =>
        string.Create(CultureInfo.InvariantCulture, $"case-tracker-integration:{appointmentId:D}");
}
