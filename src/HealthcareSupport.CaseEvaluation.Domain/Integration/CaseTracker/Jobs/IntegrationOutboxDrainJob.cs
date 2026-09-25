using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;

/// <summary>
/// Hangfire wrapper that drains one office's due Case Tracker outbox rows. Enqueued after the
/// approval unit of work commits (prompt delivery), every 5 minutes by
/// <see cref="CaseTrackerDrainKickJob"/> (#917, so a row is tried when its retry wait expires), and by
/// the reconciliation sweep (crash backstop). Losing an enqueue never loses a push -- the row is
/// already committed, so the next kick re-drives it.
///
/// <para>ONE PASS PER OFFICE AT A TIME (#917). A pass can run long during an outage (up to 50 rows,
/// each waiting out a timeout), and Hangfire queues overlapping jobs rather than skipping them, so more
/// passes for the same office would pile up and hit a service that is already down in parallel. The
/// pass therefore first tries the office's distributed lock with ZERO wait and simply returns when
/// another pass holds it. A distributed lock, not a database one, because the pass deliberately holds
/// no transaction across its work (see <see cref="IntegrationOutboxDrainService"/>).</para>
///
/// <para>Deliberately NO [UnitOfWork] here: the drain service commits each row in its own short
/// transaction, and an outer one would swallow those commits.</para>
/// </summary>
public class IntegrationOutboxDrainJob :
    AsyncBackgroundJob<IntegrationOutboxDrainArgs>,
    ITransientDependency
{
    private readonly IntegrationOutboxDrainService _drainService;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAbpDistributedLock _distributedLock;

    public IntegrationOutboxDrainJob(
        IntegrationOutboxDrainService drainService,
        ICurrentTenant currentTenant,
        IAbpDistributedLock distributedLock)
    {
        _drainService = drainService;
        _currentTenant = currentTenant;
        _distributedLock = distributedLock;
    }

    /// <summary>The lock that makes a drain pass exclusive per office.</summary>
    public static string LockName(Guid? tenantId) =>
        string.Create(CultureInfo.InvariantCulture, $"CaseTracker:OutboxDrain:{(tenantId.HasValue ? tenantId.Value.ToString("D") : "host")}");

    // The tenant scope is entered from the args because Hangfire workers boot with NO ambient tenant --
    // without this the IMultiTenant filter would hide the office's rows and the drain would find nothing.
    public override async Task ExecuteAsync(IntegrationOutboxDrainArgs args)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(LockName(args.TenantId), TimeSpan.Zero);
        if (handle == null)
        {
            Logger.LogDebug(
                "IntegrationOutboxDrainJob: tenant {TenantId} already has a drain pass running; skipping this one.",
                args.TenantId);
            return;
        }

        using (_currentTenant.Change(args.TenantId))
        {
            var result = await _drainService.DrainDueAsync();
            if (result.Sent > 0 || result.Failed > 0)
            {
                Logger.LogInformation(
                    "IntegrationOutboxDrainJob: tenant {TenantId} drained sent={Sent} failed={Failed}.",
                    args.TenantId, result.Sent, result.Failed);
            }
        }
    }
}

/// <summary>Payload for <see cref="IntegrationOutboxDrainJob"/> -- the office to drain.</summary>
[Serializable]
public class IntegrationOutboxDrainArgs
{
    public Guid? TenantId { get; set; }
}
