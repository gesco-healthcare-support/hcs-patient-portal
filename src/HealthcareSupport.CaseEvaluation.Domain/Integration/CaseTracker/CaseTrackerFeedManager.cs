using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>What the offices screen shows about one office's feed (#927).</summary>
public sealed record CaseTrackerFeedStatus(
    bool Active,
    DateTime? StartedAt,
    DateTime? LastRequestAt,
    DateTime? LastAdvancedAt,
    int? OutstandingCount);

/// <summary>
/// The operator's side of the feed (#927): start it for an office at cutover, return the office to push, and
/// read its state for the offices screen. Callers run inside the office's scope.
/// </summary>
public class CaseTrackerFeedManager : ITransientDependency
{
    private readonly ICaseTrackerFeedStateRepository _feedStateRepository;
    private readonly ICaseTrackerFeedStore _feedStore;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public CaseTrackerFeedManager(
        ICaseTrackerFeedStateRepository feedStateRepository,
        ICaseTrackerFeedStore feedStore,
        IClock clock,
        IGuidGenerator guidGenerator)
    {
        _feedStateRepository = feedStateRepository;
        _feedStore = feedStore;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Switches the office to the feed, starting from the store's start floor: just below the lower of the oldest
    /// Pending row and the oldest write still in flight (decided 2026-09-24), so nothing still owed is stranded.
    /// Returns false, changing nothing, when the feed is already on. One write: the floor and the switch from push
    /// to feed cannot disagree.
    /// </summary>
    public virtual async Task<bool> StartAsync(Guid officeId, CancellationToken cancellationToken = default)
    {
        var state = await _feedStateRepository.FindCurrentAsync(cancellationToken);
        if (state is { IsActive: true })
        {
            return false;
        }

        var floor = await _feedStore.GetStartFloorAsync(officeId, cancellationToken);
        if (state == null)
        {
            state = new CaseTrackerFeedState(_guidGenerator.Create(), officeId);
            state.Start(floor, _clock.Now);
            await _feedStateRepository.InsertAsync(state, autoSave: true, cancellationToken);
        }
        else
        {
            state.Start(floor, _clock.Now);
            await _feedStateRepository.UpdateAsync(state, autoSave: true, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Returns the office to push. Returns false, changing nothing, when it is not on the feed. The drain resumes
    /// on its next pass and pushes every Pending row, including ones the feed already delivered (accepted
    /// 2026-09-24; the receiver's upsert absorbs the duplicates).
    /// </summary>
    public virtual async Task<bool> ReturnToPushAsync(CancellationToken cancellationToken = default)
    {
        var state = await _feedStateRepository.FindCurrentAsync(cancellationToken);
        if (state is not { IsActive: true })
        {
            return false;
        }

        state.ReturnToPush(_clock.Now);
        await _feedStateRepository.UpdateAsync(state, autoSave: true, cancellationToken);
        return true;
    }

    /// <summary>
    /// The office's feed state. The outstanding count is read only while the feed is on, so an office on push
    /// never touches the SQL-Server-only store.
    /// </summary>
    public virtual async Task<CaseTrackerFeedStatus> GetStatusAsync(Guid officeId, CancellationToken cancellationToken = default)
    {
        var state = await _feedStateRepository.FindCurrentAsync(cancellationToken);
        if (state is not { IsActive: true })
        {
            return new CaseTrackerFeedStatus(false, state?.StartedAt, state?.LastRequestAt, state?.LastAdvancedAt, null);
        }

        var outstanding = await _feedStore.CountOutstandingAsync(officeId, state.AcknowledgedPosition, cancellationToken);
        return new CaseTrackerFeedStatus(true, state.StartedAt, state.LastRequestAt, state.LastAdvancedAt, outstanding);
    }
}
