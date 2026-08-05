using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests.Jobs;

/// <summary>
/// Phase 4c (2026-08-05) -- hourly sweep that expires consent tokens nobody clicked.
///
/// <para>Before this job, expiry was evaluated LAZILY: only inside
/// <c>ChangeRequestConsentManager.RecordDecisionAsync</c>, i.e. only when a party actually
/// followed their link. A token nobody ever clicked therefore stayed <c>Pending</c> forever,
/// well past its 7-day TTL, and blocked finalize INDEFINITELY with no signal to staff -- the
/// request simply sat in the queue looking like it was still waiting on somebody.</para>
///
/// <para>An expired side counts as a No, so the sweep does not silently approve anything: it
/// converts "waiting forever" into "declined by lapse", which staff resolve the same way as any
/// other decline -- confirm a new date, which opens the next round.</para>
/// </summary>
public class ChangeRequestConsentExpirySweepJob : ITransientDependency
{
    public const string RecurringJobId = "change-request-consent-expiry-sweep";

    /// <summary>Half past every hour, offset from the on-the-hour jobs so runs do not pile up.</summary>
    public const string CronExpression = "30 * * * *";

    private readonly IChangeRequestConsentRoundRepository _roundRepository;
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly IClock _clock;
    private readonly ILogger<ChangeRequestConsentExpirySweepJob> _logger;

    public ChangeRequestConsentExpirySweepJob(
        IChangeRequestConsentRoundRepository roundRepository,
        ITenantWorkRunner tenantWorkRunner,
        IClock clock,
        ILogger<ChangeRequestConsentExpirySweepJob> logger)
    {
        _roundRepository = roundRepository;
        _tenantWorkRunner = tenantWorkRunner;
        _clock = clock;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        var expiredTotal = 0;
        var officeCount = 0;

        // Database-per-office: the sweep has to be run once inside each office's context, or it
        // only ever sees whichever database the ambient tenant happens to point at.
        await _tenantWorkRunner.ForEachOfficeAsync(async _ =>
        {
            officeCount++;
            expiredTotal += await SweepOfficeAsync();
        });

        _logger.LogInformation(
            "ChangeRequestConsentExpirySweepJob: expired {ExpiredCount} consent side(s) across {OfficeCount} office(s).",
            expiredTotal,
            officeCount);
    }

    /// <summary>
    /// Expires every lapsed <c>Pending</c> side on the office's non-superseded rounds. Superseded
    /// rounds are skipped deliberately: they no longer gate anything, and rewriting their sides
    /// would corrupt the record of what each party actually did with the date they were asked
    /// about.
    /// </summary>
    private async Task<int> SweepOfficeAsync()
    {
        var nowUtc = _clock.Now.ToUniversalTime();

        var candidates = await _roundRepository.GetListAsync(r =>
            r.SupersededAt == null
            && ((r.SideAConsentStatus == ChangeRequestConsentStatus.Pending
                    && r.SideAConsentExpiresAt != null
                    && r.SideAConsentExpiresAt <= nowUtc)
                || (r.SideBConsentStatus == ChangeRequestConsentStatus.Pending
                    && r.SideBConsentExpiresAt != null
                    && r.SideBConsentExpiresAt <= nowUtc)));

        var expired = 0;
        foreach (var round in candidates)
        {
            var before = CountDecided(round);
            round.MarkSideExpired(ChangeRequestSide.SideA, nowUtc);
            round.MarkSideExpired(ChangeRequestSide.SideB, nowUtc);
            var changed = CountDecided(round) - before;

            if (changed == 0)
            {
                continue;
            }

            await _roundRepository.UpdateAsync(round, autoSave: true);
            expired += changed;

            _logger.LogInformation(
                "ChangeRequestConsentExpirySweepJob: change request {ChangeRequestId} round {RoundNumber} -- {Count} side(s) lapsed past their expiry.",
                round.AppointmentChangeRequestId,
                round.RoundNumber,
                changed);
        }

        return expired;
    }

    /// <summary>
    /// How many sides are no longer <c>Pending</c>. Comparing this before and after tells us how
    /// many <c>MarkSideExpired</c> calls actually did something -- the method is a deliberate
    /// no-op on a side that already answered, so counting its calls would over-report.
    /// </summary>
    private static int CountDecided(ChangeRequestConsentRound round) =>
        new[] { ChangeRequestSide.SideA, ChangeRequestSide.SideB }
            .Count(side => round.SideConsentStatus(side) != ChangeRequestConsentStatus.Pending);
}
