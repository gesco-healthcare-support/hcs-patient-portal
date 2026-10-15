using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Raises a Case Tracker feed alert (#927) for the email handler in Application. Shared by the feed, which raises
/// cursor-ahead and skip alerts as requests arrive, and the health job, which raises silence and stall alerts, so
/// both name the office the same way.
/// </summary>
public class CaseTrackerFeedAlertPublisher : ITransientDependency
{
    private readonly ILocalEventBus _localEventBus;
    private readonly ITenantStore _tenantStore;

    public CaseTrackerFeedAlertPublisher(ILocalEventBus localEventBus, ITenantStore tenantStore)
    {
        _localEventBus = localEventBus;
        _tenantStore = tenantStore;
    }

    /// <summary>
    /// Publishes one alert. The office's name comes from the tenant STORE: inside <c>ICurrentTenant.Change</c>
    /// the current tenant's name is null, which once put a blank office into an alert email.
    /// </summary>
    public virtual async Task PublishAsync(
        Guid officeId,
        DateTime nowUtc,
        CaseTrackerFeedAlertKind kind,
        Action<CaseTrackerFeedAlertEto>? fill = null)
    {
        var tenant = await _tenantStore.FindAsync(officeId);
        var eto = new CaseTrackerFeedAlertEto
        {
            Kind = kind,
            TenantId = officeId,
            OfficeName = tenant?.Name ?? string.Empty,
            OccurredAt = nowUtc,
        };
        fill?.Invoke(eto);
        await _localEventBus.PublishAsync(eto);
    }
}
