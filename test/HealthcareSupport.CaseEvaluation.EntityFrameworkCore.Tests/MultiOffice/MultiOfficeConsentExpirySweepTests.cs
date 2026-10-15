using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests.Jobs;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Pins the consent-expiry sweep (epic phase 4c, 2026-08-05). Before it existed, expiry was
/// evaluated ONLY when a party clicked their link, so a token nobody clicked stayed Pending past
/// its 7-day TTL forever and blocked finalize with no signal to staff.
///
/// <para>Runs on the multi-office harness because the per-office iteration is half the point:
/// database-per-office means a sweep that forgets to switch tenant context only ever cleans
/// whichever database the ambient tenant happened to be.</para>
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeConsentExpirySweepTests : ConsentRoundTestBase
{
    private readonly ChangeRequestConsentExpirySweepJob _job;
    private readonly IChangeRequestConsentRoundRepository _roundRepository;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;

    public MultiOfficeConsentExpirySweepTests()
    {
        _job = GetRequiredService<ChangeRequestConsentExpirySweepJob>();
        _roundRepository = GetRequiredService<IChangeRequestConsentRoundRepository>();
        _changeRequestRepository = GetRequiredService<IRepository<AppointmentChangeRequest, Guid>>();
    }

    [Fact]
    public async Task A_lapsed_pending_side_is_expired_and_a_live_one_is_left_alone()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        var lapsedRoundId = Guid.NewGuid();
        var liveRoundId = Guid.NewGuid();

        await InOfficeAsync(officeA, async () =>
        {
            var lapsedRequestId = Guid.NewGuid();
            await SeedRescheduleRequestAsync(officeA, lapsedRequestId);
            var lapsed = NewRound(officeA, lapsedRequestId, lapsedRoundId);
            lapsed.IssueSideConsent(ChangeRequestSide.SideA, $"hash-lapsed-{lapsedRoundId:N}", DateTime.UtcNow.AddDays(-1));
            await _roundRepository.InsertAsync(lapsed, autoSave: true);

            var liveRequestId = Guid.NewGuid();
            await SeedRescheduleRequestAsync(officeA, liveRequestId);
            var live = NewRound(officeA, liveRequestId, liveRoundId);
            live.IssueSideConsent(ChangeRequestSide.SideA, $"hash-live-{liveRoundId:N}", DateTime.UtcNow.AddDays(7));
            await _roundRepository.InsertAsync(live, autoSave: true);
        });

        await WithUnitOfWorkAsync(() => _job.ExecuteAsync(), requiresNew: true);

        await InOfficeAsync(officeA, async () =>
        {
            (await _roundRepository.GetAsync(lapsedRoundId))
                .SideConsentStatus(ChangeRequestSide.SideA)
                .ShouldBe(ChangeRequestConsentStatus.Expired);

            (await _roundRepository.GetAsync(liveRoundId))
                .SideConsentStatus(ChangeRequestSide.SideA)
                .ShouldBe(ChangeRequestConsentStatus.Pending);
        });
    }

    [Fact]
    public async Task A_side_that_already_answered_is_untouched_even_past_its_expiry()
    {
        // The sweep must never rewrite a real decision into a lapse.
        var (officeA, _) = await GetSeededOfficesAsync();
        var roundId = Guid.NewGuid();

        await InOfficeAsync(officeA, async () =>
        {
            var changeRequestId = Guid.NewGuid();
            await SeedRescheduleRequestAsync(officeA, changeRequestId);
            var round = NewRound(officeA, changeRequestId, roundId);
            round.IssueSideConsent(ChangeRequestSide.SideA, $"hash-answered-{roundId:N}", DateTime.UtcNow.AddDays(-1));
            round.RecordSideDecision(ChangeRequestSide.SideA, approved: true, "rep-a@example.test", DateTime.UtcNow.AddDays(-2));
            await _roundRepository.InsertAsync(round, autoSave: true);
        });

        await WithUnitOfWorkAsync(() => _job.ExecuteAsync(), requiresNew: true);

        await InOfficeAsync(officeA, async () =>
        {
            var round = await _roundRepository.GetAsync(roundId);
            round.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Approved);
            round.SideAConsentRespondedByEmail.ShouldBe("rep-a@example.test");
        });
    }

    [Fact]
    public async Task A_superseded_round_is_left_alone()
    {
        // A superseded round gates nothing, and rewriting its sides would corrupt the record of
        // what each party did with the date they were actually asked about.
        var (officeA, _) = await GetSeededOfficesAsync();
        var roundId = Guid.NewGuid();

        await InOfficeAsync(officeA, async () =>
        {
            var changeRequestId = Guid.NewGuid();
            await SeedRescheduleRequestAsync(officeA, changeRequestId);
            var round = NewRound(officeA, changeRequestId, roundId);
            round.IssueSideConsent(ChangeRequestSide.SideB, $"hash-superseded-{roundId:N}", DateTime.UtcNow.AddDays(-1));
            round.Supersede(DateTime.UtcNow);
            await _roundRepository.InsertAsync(round, autoSave: true);
        });

        await WithUnitOfWorkAsync(() => _job.ExecuteAsync(), requiresNew: true);

        await InOfficeAsync(officeA, async () =>
        {
            (await _roundRepository.GetAsync(roundId))
                .SideConsentStatus(ChangeRequestSide.SideB)
                .ShouldBe(ChangeRequestConsentStatus.Pending);
        });
    }

    [Fact]
    public async Task Every_office_is_swept_in_one_run()
    {
        // Database-per-office: a sweep that forgets to switch tenant context would clean only one.
        var (officeA, officeB) = await GetSeededOfficesAsync();
        var roundAId = Guid.NewGuid();
        var roundBId = Guid.NewGuid();

        foreach (var (office, roundId) in new[] { (officeA, roundAId), (officeB, roundBId) })
        {
            await InOfficeAsync(office, async () =>
            {
                var changeRequestId = Guid.NewGuid();
                await SeedRescheduleRequestAsync(office, changeRequestId);
                var round = NewRound(office, changeRequestId, roundId);
                round.IssueSideConsent(ChangeRequestSide.SideA, $"hash-sweep-{roundId:N}", DateTime.UtcNow.AddDays(-1));
                await _roundRepository.InsertAsync(round, autoSave: true);
            });
        }

        await WithUnitOfWorkAsync(() => _job.ExecuteAsync(), requiresNew: true);

        await InOfficeAsync(officeA, async () =>
            (await _roundRepository.GetAsync(roundAId))
                .SideConsentStatus(ChangeRequestSide.SideA)
                .ShouldBe(ChangeRequestConsentStatus.Expired));

        await InOfficeAsync(officeB, async () =>
            (await _roundRepository.GetAsync(roundBId))
                .SideConsentStatus(ChangeRequestSide.SideA)
                .ShouldBe(ChangeRequestConsentStatus.Expired));
    }
}
