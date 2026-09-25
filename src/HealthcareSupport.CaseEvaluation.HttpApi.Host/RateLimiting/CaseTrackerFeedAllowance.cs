using System;
using System.Threading.RateLimiting;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.RateLimiting;

/// <summary>
/// The changes feed's own allowance (#927): <see cref="CaseTrackerFeedConsts.RequestsPerHourPerOffice"/> requests
/// an hour PER OFFICE, separate from the 300 an hour that reconcile and attendance share.
///
/// <para>Counted in the controller AFTER the feed token is checked, not in the rate-limiting middleware
/// (decided 2026-09-24). The middleware runs before any token check, so an office-keyed bucket there could be
/// spent by anyone who knew an office's id, locking the real consumer out for up to an hour. Requests without
/// a valid token are capped separately, per address, in the middleware.</para>
///
/// <para>In memory, so the allowance is per API instance: exact while there is one instance (today), and to be
/// moved to a shared store if the API is ever scaled out.</para>
/// </summary>
public sealed class CaseTrackerFeedAllowance : ISingletonDependency, IDisposable
{
    private readonly PartitionedRateLimiter<Guid> _limiter = PartitionedRateLimiter.Create<Guid, Guid>(
        officeId => RateLimitPartition.GetFixedWindowLimiter(
            officeId,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = CaseTrackerFeedConsts.RequestsPerHourPerOffice,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            }));

    /// <summary>Takes one request from the office's hourly allowance; false when it is spent.</summary>
    public bool TryAcquire(Guid officeId)
    {
        using var lease = _limiter.AttemptAcquire(officeId);
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
