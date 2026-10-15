using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// T10: Hangfire wrapper that drains one office's due outbox rows. Enqueued on
/// UoW commit after the dispatcher writes Pending rows (prompt delivery), and
/// also invoked by the T11 reconciliation sweep (crash backstop). Losing this
/// enqueue never loses an email -- the rows are already committed, so the sweep
/// re-drives them.
/// </summary>
public class OutboxDrainJob :
    AsyncBackgroundJob<OutboxDrainArgs>,
    ITransientDependency
{
    private readonly OutboxDrainService _drainService;
    private readonly ICurrentTenant _currentTenant;

    public OutboxDrainJob(OutboxDrainService drainService, ICurrentTenant currentTenant)
    {
        _drainService = drainService;
        _currentTenant = currentTenant;
    }

    // [UnitOfWork] so the claim + mark-sent writes commit; tenant scope entered
    // from the args (Hangfire workers boot with no ambient tenant, so without
    // this the IMultiTenant filter would hide the office's rows).
    [UnitOfWork]
    public override async Task ExecuteAsync(OutboxDrainArgs args)
    {
        using (_currentTenant.Change(args.TenantId))
        {
            var result = await _drainService.DrainDueAsync();
            if (result.Sent > 0 || result.Failed > 0)
            {
                Logger.LogInformation(
                    "OutboxDrainJob: tenant {TenantId} drained sent={Sent} failed={Failed}.",
                    args.TenantId, result.Sent, result.Failed);
            }
        }
    }
}

/// <summary>Payload for <see cref="OutboxDrainJob"/> -- the office to drain.</summary>
[Serializable]
public class OutboxDrainArgs
{
    public Guid? TenantId { get; set; }
}
