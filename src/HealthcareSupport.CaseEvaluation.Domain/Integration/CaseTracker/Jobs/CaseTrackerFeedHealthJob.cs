using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;

/// <summary>
/// Watches every office on the Case Tracker feed (#927) for the two failures only the portal can see, and
/// emails the technical list when one starts and when it clears (decided 2026-09-24):
/// <list type="bullet">
///   <item>SILENCE: no request from the office for <see cref="CaseTrackerFeedConsts.SilenceMinutes"/> minutes.
///   The consumer looks dead; per-row alerting cannot see this, because nothing fails.</item>
///   <item>STALL: a row has waited <see cref="CaseTrackerFeedConsts.StallMinutes"/> minutes and the acknowledged
///   position has not moved for as long. Requests may still be arriving.</item>
/// </list>
///
/// <para>Shipped WITH the endpoint rather than after it (decided 2026-09-24): the failures screen lists only
/// Failed rows, and under the feed nothing fails, so without this nobody on our side could see rows sitting
/// unclaimed.</para>
///
/// <para>Every 5 minutes, so a silence is reported within 20 minutes and a stall within 35. Once per incident:
/// a stamp on the feed record stops repeats and is cleared, with a second email, when the condition ends.</para>
///
/// <para>An office whose <c>CaseTrackerPushEnabled</c> switch is off is skipped: the feed refuses it, so the
/// quiet that follows is deliberate, not a fault.</para>
/// </summary>
public class CaseTrackerFeedHealthJob : ITransientDependency
{
    public const string RecurringJobId = "case-tracker-feed-health";

    public const string CronExpression = "*/5 * * * *";

    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly ICaseTrackerFeedStateRepository _feedStateRepository;
    private readonly ICaseTrackerFeedStore _feedStore;
    private readonly CaseTrackerFeedAlertPublisher _alerts;
    private readonly CaseTrackerDeliveryModeReader _deliveryMode;
    private readonly IClock _clock;
    private readonly ILogger<CaseTrackerFeedHealthJob> _logger;

    public CaseTrackerFeedHealthJob(
        ITenantWorkRunner tenantWorkRunner,
        ICaseTrackerFeedStateRepository feedStateRepository,
        ICaseTrackerFeedStore feedStore,
        CaseTrackerFeedAlertPublisher alerts,
        CaseTrackerDeliveryModeReader deliveryMode,
        IClock clock,
        ILogger<CaseTrackerFeedHealthJob> logger)
    {
        _tenantWorkRunner = tenantWorkRunner;
        _feedStateRepository = feedStateRepository;
        _feedStore = feedStore;
        _alerts = alerts;
        _deliveryMode = deliveryMode;
        _clock = clock;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        var offices = 0;
        var alerts = 0;

        await _tenantWorkRunner.ForEachOfficeAsync(async officeId =>
        {
            offices++;
            try
            {
                alerts += await CheckOfficeAsync(officeId);
            }
            catch (Exception ex)
            {
                // Per-office isolation: ForEachOfficeAsync aborts the run if a delegate throws, and one broken
                // office must not silence the check for every other one.
                _logger.LogError(
                    ex,
                    "CaseTrackerFeedHealthJob: office {OfficeId} failed; continuing with the next office.",
                    officeId);
            }
        });

        _logger.LogInformation(
            "CaseTrackerFeedHealthJob: checked {OfficeCount} offices, raised or cleared {AlertCount} feed alert(s).",
            offices, alerts);
    }

    /// <summary>
    /// Whether the consumer has been silent for the threshold. Measured from the last request, or from when the
    /// feed started if none has arrived yet. Pure, so the boundary is tested without a database.
    /// </summary>
    public static bool IsSilent(CaseTrackerFeedState state, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(state);
        var lastHeard = state.LastRequestAt ?? state.StartedAt ?? nowUtc;
        return lastHeard <= nowUtc.AddMinutes(-CaseTrackerFeedConsts.SilenceMinutes);
    }

    private async Task<int> CheckOfficeAsync(Guid officeId)
    {
        var state = await _feedStateRepository.FindCurrentAsync();
        if (state is not { IsActive: true } || !await _deliveryMode.IsPushSwitchOnAsync())
        {
            return 0;
        }

        var now = _clock.Now;
        return await CheckSilenceAsync(officeId, state, now) + await CheckStallAsync(officeId, state, now);
    }

    private async Task<int> CheckSilenceAsync(Guid officeId, CaseTrackerFeedState state, DateTime now)
    {
        var silent = IsSilent(state, now);
        if (silent == state.SilenceAlertedAt.HasValue)
        {
            return 0; // no change: still quiet and already told, or fine and nothing open
        }

        var kind = silent ? CaseTrackerFeedAlertKind.SilenceStarted : CaseTrackerFeedAlertKind.SilenceCleared;
        await _alerts.PublishAsync(officeId, now, kind, eto => eto.LastRequestAt = state.LastRequestAt);
        await _feedStateRepository.SetSilenceAlertedAsync(state.Id, silent ? now : null);
        return 1;
    }

    /// <summary>
    /// A stall needs BOTH a row waiting past the threshold and a position that has not moved for as long. The
    /// row check is not limited by <c>MIN_ACTIVE_ROWVERSION()</c>, so a long-running transaction holding the feed
    /// back shows up here as a stall -- and the email names that as a possible cause.
    /// </summary>
    private async Task<int> CheckStallAsync(Guid officeId, CaseTrackerFeedState state, DateTime now)
    {
        var threshold = now.AddMinutes(-CaseTrackerFeedConsts.StallMinutes);
        var positionIdle = (state.LastAdvancedAt ?? state.StartedAt ?? now) <= threshold;
        var stalled = positionIdle
            && await _feedStore.HasOutstandingCreatedBeforeAsync(officeId, state.AcknowledgedPosition, threshold);
        if (stalled == state.StallAlertedAt.HasValue)
        {
            return 0;
        }

        int? outstanding = stalled
            ? await _feedStore.CountOutstandingAsync(officeId, state.AcknowledgedPosition)
            : null;
        var kind = stalled ? CaseTrackerFeedAlertKind.StallStarted : CaseTrackerFeedAlertKind.StallCleared;
        await _alerts.PublishAsync(officeId, now, kind, eto => eto.OutstandingCount = outstanding);
        await _feedStateRepository.SetStallAlertedAsync(state.Id, stalled ? now : null);
        return 1;
    }
}
