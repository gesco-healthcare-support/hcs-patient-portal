using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The feed's rowversion reads (#927), in raw SQL because LINQ cannot express <c>MIN_ACTIVE_ROWVERSION()</c> or
/// compare an 8-byte rowversion. Raw SQL also skips EF's query filters, so every statement states the office
/// and <c>IsDeleted = 0</c> itself.
///
/// <para>Each read is a <c>public static</c> method over a <see cref="DatabaseFacade"/>, and the instance
/// methods only find the office's context and call it. That is so the SQL Server tests run the very SQL this
/// class runs, against a real SQL Server, rather than a copy of it.</para>
///
/// <para>SQL Server only, and LOUD about it: on any other provider it throws. Unlike the ordering lock, which
/// is a harmless no-op elsewhere, an empty page here would read as "nothing to send".</para>
/// </summary>
public class EfCoreCaseTrackerFeedStore : ICaseTrackerFeedStore, ITransientDependency
{
    private const string Outbox = "[" + CaseEvaluationConsts.DbTablePrefix + "IntegrationOutboxItems]";
    private const string Version = "[" + CaseTrackerFeedConsts.ChangeVersionColumn + "]";

    /// <summary>The office's live Pending rows. Every statement below starts from this.</summary>
    private const string PendingInOffice =
        " FROM " + Outbox + " WHERE [TenantId] = @office AND [IsDeleted] = 0 AND [Status] = @pending";

    private const string ReadPageSql =
        "SELECT TOP (@take) CAST(" + Version + " AS bigint) AS [Position], [MessageType], [AppointmentId], [Payload]"
        + PendingInOffice
        + " AND " + Version + " > CAST(@after AS binary(8))"
        + " AND " + Version + " < MIN_ACTIVE_ROWVERSION()"
        + " ORDER BY " + Version;

    private const string StartFloorSql =
        "SELECT CAST(MIN_ACTIVE_ROWVERSION() AS bigint) AS [Horizon],"
        + " (SELECT MIN(CAST(" + Version + " AS bigint))" + PendingInOffice + ") AS [OldestPending]";

    private const string OutstandingSql =
        "SELECT COUNT(*) AS [Value]" + PendingInOffice + " AND " + Version + " > CAST(@after AS binary(8))";

    private const string OutstandingCreatedBeforeSql =
        "SELECT COUNT(*) AS [Value]" + PendingInOffice
        + " AND " + Version + " > CAST(@after AS binary(8)) AND [CreationTime] < @createdBefore";

    private const string FindRowSql =
        "SELECT CAST(" + Version + " AS bigint) AS [Position], [MessageType], [AppointmentId], [Payload]"
        + PendingInOffice
        + " AND " + Version + " = CAST(@after AS binary(8))";

    private readonly IDbContextProvider<CaseEvaluationDbContext> _dbContextProvider;

    public EfCoreCaseTrackerFeedStore(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider)
    {
        _dbContextProvider = dbContextProvider;
    }

    public async Task<List<CaseTrackerFeedRow>> ReadPageAsync(
        Guid officeId, long after, int take, CancellationToken cancellationToken = default) =>
        await ReadPageAsync(await DatabaseAsync(), officeId, after, take, cancellationToken);

    public async Task<long> GetStartFloorAsync(Guid officeId, CancellationToken cancellationToken = default) =>
        await GetStartFloorAsync(await DatabaseAsync(), officeId, cancellationToken);

    public async Task<int> CountOutstandingAsync(Guid officeId, long acknowledged, CancellationToken cancellationToken = default) =>
        await CountOutstandingAsync(await DatabaseAsync(), officeId, acknowledged, cancellationToken);

    public async Task<bool> HasOutstandingCreatedBeforeAsync(
        Guid officeId, long acknowledged, DateTime createdBefore, CancellationToken cancellationToken = default) =>
        await HasOutstandingCreatedBeforeAsync(await DatabaseAsync(), officeId, acknowledged, createdBefore, cancellationToken);

    public async Task<CaseTrackerFeedRow?> FindRowAsync(Guid officeId, long position, CancellationToken cancellationToken = default) =>
        await FindRowAsync(await DatabaseAsync(), officeId, position, cancellationToken);

    /// <summary>See <see cref="ICaseTrackerFeedStore.ReadPageAsync"/>.</summary>
    public static async Task<List<CaseTrackerFeedRow>> ReadPageAsync(
        DatabaseFacade database, Guid officeId, long after, int take, CancellationToken cancellationToken = default)
    {
        EnsureSqlServer(database);
        var rows = await database
            .SqlQueryRaw<FeedRowRecord>(ReadPageSql, Office(officeId), Pending(), After(after), Take(take))
            .ToListAsync(cancellationToken);
        return rows.Select(r => r.ToRow()).ToList();
    }

    /// <summary>See <see cref="ICaseTrackerFeedStore.GetStartFloorAsync"/>.</summary>
    public static async Task<long> GetStartFloorAsync(
        DatabaseFacade database, Guid officeId, CancellationToken cancellationToken = default)
    {
        EnsureSqlServer(database);
        var bounds = (await database
            .SqlQueryRaw<FloorRecord>(StartFloorSql, Office(officeId), Pending())
            .ToListAsync(cancellationToken)).Single();

        // Below the LOWER of the two: the horizon keeps an in-flight row, the oldest Pending keeps a retrying one.
        var lowest = bounds.OldestPending.HasValue ? Math.Min(bounds.Horizon, bounds.OldestPending.Value) : bounds.Horizon;
        return lowest - 1;
    }

    /// <summary>See <see cref="ICaseTrackerFeedStore.CountOutstandingAsync"/>.</summary>
    public static async Task<int> CountOutstandingAsync(
        DatabaseFacade database, Guid officeId, long acknowledged, CancellationToken cancellationToken = default)
    {
        EnsureSqlServer(database);
        return (await database
            .SqlQueryRaw<int>(OutstandingSql, Office(officeId), Pending(), After(acknowledged))
            .ToListAsync(cancellationToken)).Single();
    }

    /// <summary>See <see cref="ICaseTrackerFeedStore.HasOutstandingCreatedBeforeAsync"/>.</summary>
    public static async Task<bool> HasOutstandingCreatedBeforeAsync(
        DatabaseFacade database, Guid officeId, long acknowledged, DateTime createdBefore, CancellationToken cancellationToken = default)
    {
        EnsureSqlServer(database);
        var createdBeforeParameter = new SqlParameter("@createdBefore", SqlDbType.DateTime2) { Value = createdBefore };
        var count = (await database
            .SqlQueryRaw<int>(OutstandingCreatedBeforeSql, Office(officeId), Pending(), After(acknowledged), createdBeforeParameter)
            .ToListAsync(cancellationToken)).Single();
        return count > 0;
    }

    /// <summary>See <see cref="ICaseTrackerFeedStore.FindRowAsync"/>.</summary>
    public static async Task<CaseTrackerFeedRow?> FindRowAsync(
        DatabaseFacade database, Guid officeId, long position, CancellationToken cancellationToken = default)
    {
        EnsureSqlServer(database);
        var rows = await database
            .SqlQueryRaw<FeedRowRecord>(FindRowSql, Office(officeId), Pending(), After(position))
            .ToListAsync(cancellationToken);
        return rows.Count == 0 ? null : rows[0].ToRow();
    }

    private async Task<DatabaseFacade> DatabaseAsync() => (await _dbContextProvider.GetDbContextAsync()).Database;

    private static void EnsureSqlServer(DatabaseFacade database)
    {
        if (!database.IsSqlServer())
        {
            throw new NotSupportedException(
                "The Case Tracker feed reads rowversion positions and runs only on SQL Server.");
        }
    }

    private static SqlParameter Office(Guid officeId) =>
        new("@office", SqlDbType.UniqueIdentifier) { Value = officeId };

    private static SqlParameter Pending() =>
        new("@pending", SqlDbType.Int) { Value = (int)IntegrationOutboxStatus.Pending };

    private static SqlParameter After(long position) =>
        new("@after", SqlDbType.BigInt) { Value = position };

    private static SqlParameter Take(int take) =>
        new("@take", SqlDbType.Int) { Value = take };

    /// <summary>The raw shape of a feed row; the message type arrives as its stored int.</summary>
    private sealed class FeedRowRecord
    {
        public long Position { get; set; }

        public int MessageType { get; set; }

        public Guid AppointmentId { get; set; }

        public string Payload { get; set; } = null!;

        public CaseTrackerFeedRow ToRow() =>
            new(Position, (IntegrationMessageType)MessageType, AppointmentId, Payload);
    }

    private sealed class FloorRecord
    {
        public long Horizon { get; set; }

        public long? OldestPending { get; set; }
    }
}
