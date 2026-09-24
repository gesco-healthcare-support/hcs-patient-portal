using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The per-office feed record (#927). Office scoping is the ambient tenant filter, as for every Case Tracker
/// repository: callers run inside the office's scope.
///
/// <para>Everything a poll or the health job writes goes through a single set-based UPDATE rather than a
/// tracked save. Two overlapping polls, or a poll and the job, then cannot fail on the concurrency stamp, and
/// each touches only the columns it owns.</para>
/// </summary>
public interface ICaseTrackerFeedStateRepository : IRepository<CaseTrackerFeedState, Guid>
{
    /// <summary>This office's feed record, or null when the office has never been switched to the feed.</summary>
    Task<CaseTrackerFeedState?> FindCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records one authenticated request, MONOTONICALLY: the acknowledged and highest-issued positions only
    /// ever move forward, and <c>LastAdvancedAt</c> changes only when the acknowledged position actually does.
    /// A late or repeated request can therefore never move the office backwards.
    /// </summary>
    Task RecordRequestAsync(
        Guid id,
        DateTime nowUtc,
        long acknowledged,
        long highestIssued,
        CancellationToken cancellationToken = default);

    /// <summary>Sets or clears the silence-alert stamp. Null clears it, which re-arms the alert.</summary>
    Task SetSilenceAlertedAsync(Guid id, DateTime? alertedAt, CancellationToken cancellationToken = default);

    /// <summary>Sets or clears the stall-alert stamp. Null clears it, which re-arms the alert.</summary>
    Task SetStallAlertedAsync(Guid id, DateTime? alertedAt, CancellationToken cancellationToken = default);

    /// <summary>Sets or clears the cursor-ahead stamp. Null clears it, which re-arms the alert.</summary>
    Task SetCursorAheadAlertedAsync(Guid id, DateTime? alertedAt, CancellationToken cancellationToken = default);
}
