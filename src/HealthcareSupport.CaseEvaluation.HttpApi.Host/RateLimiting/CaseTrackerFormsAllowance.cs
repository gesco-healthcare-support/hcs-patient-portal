using System;
using System.Threading.RateLimiting;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.RateLimiting;

/// <summary>
/// The blank forms' and office list's own allowances, apart from <see cref="CaseTrackerFeedAllowance"/> so they can
/// never spend the feed's hourly budget: <see cref="CaseTrackerFormsConsts.FormRequestsPerHourPerOffice"/> form
/// downloads an hour per office, and <see cref="CaseTrackerFormsConsts.OfficeListRequestsPerHour"/> office-list
/// requests an hour for the whole portal.
///
/// <para>Counted in the controller AFTER the feed token is checked, for the reason the feed's allowance gives: an
/// office-keyed bucket must not be spendable by anyone who merely knows an office id. In memory, so per API
/// instance, like the feed's; registered by convention, so no host module changes.</para>
/// </summary>
public sealed class CaseTrackerFormsAllowance : ISingletonDependency, IDisposable
{
    private readonly PartitionedRateLimiter<Guid> _forms = Create(CaseTrackerFormsConsts.FormRequestsPerHourPerOffice);
    private readonly PartitionedRateLimiter<Guid> _officeList = Create(CaseTrackerFormsConsts.OfficeListRequestsPerHour);

    /// <summary>Takes one download from the office's hourly allowance; false when it is spent.</summary>
    public bool TryAcquireForm(Guid officeId) => TryAcquire(_forms, officeId);

    /// <summary>Takes one request from the office list's hourly allowance; false when it is spent.</summary>
    public bool TryAcquireOfficeList() => TryAcquire(_officeList, Guid.Empty);

    public void Dispose()
    {
        _forms.Dispose();
        _officeList.Dispose();
    }

    private static bool TryAcquire(PartitionedRateLimiter<Guid> limiter, Guid key)
    {
        using var lease = limiter.AttemptAcquire(key);
        return lease.IsAcquired;
    }

    private static PartitionedRateLimiter<Guid> Create(int permitsPerHour) =>
        PartitionedRateLimiter.Create<Guid, Guid>(
            key => RateLimitPartition.GetFixedWindowLimiter(
                key,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitsPerHour,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true,
                }));
}
