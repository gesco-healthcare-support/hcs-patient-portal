using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;

/// <summary>
/// Hangfire wrapper that drains one office's due Case Tracker outbox rows. Enqueued after the
/// approval unit of work commits (prompt delivery) and also by the reconciliation sweep (crash
/// backstop). Losing this enqueue never loses a push -- the row is already committed, so the sweep
/// re-drives it.
/// </summary>
public class IntegrationOutboxDrainJob :
    AsyncBackgroundJob<IntegrationOutboxDrainArgs>,
    ITransientDependency
{
    private readonly IntegrationOutboxDrainService _drainService;
    private readonly ICurrentTenant _currentTenant;

    public IntegrationOutboxDrainJob(
        IntegrationOutboxDrainService drainService,
        ICurrentTenant currentTenant)
    {
        _drainService = drainService;
        _currentTenant = currentTenant;
    }

    // [UnitOfWork] so the claim + mark writes commit. The tenant scope is entered from the args
    // because Hangfire workers boot with NO ambient tenant -- without this the IMultiTenant filter
    // would hide the office's rows and the drain would silently find nothing.
    [UnitOfWork]
    public override async Task ExecuteAsync(IntegrationOutboxDrainArgs args)
    {
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
