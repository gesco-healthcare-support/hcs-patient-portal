using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Pins the two consent-round lookups the generic repository cannot express (epic phase 4c,
/// 2026-08-05): resolving a consent token to the round that owns it, and finding which round
/// is CURRENT for a change request. Runs against a real office database on the multi-office
/// harness, so the ordering and the non-superseded filter are proven in SQL rather than LINQ
/// to objects.
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeConsentRoundRepositoryTests : CaseEvaluationMultiOfficeTestBase
{
    private readonly IChangeRequestConsentRoundRepository _roundRepository;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;
    private readonly ICurrentTenant _currentTenant;

    public MultiOfficeConsentRoundRepositoryTests()
    {
        _roundRepository = GetRequiredService<IChangeRequestConsentRoundRepository>();
        _changeRequestRepository = GetRequiredService<IRepository<AppointmentChangeRequest, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task FindByTokenHashAsync_matches_either_side_and_returns_null_for_an_unknown_hash()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        var changeRequestId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var sideAHash = $"hash-a-{roundId:N}";
        var sideBHash = $"hash-b-{roundId:N}";

        await InOfficeAsync(officeA, async () =>
        {
            await SeedChangeRequestAsync(officeA, changeRequestId);
            var round = NewRound(officeA, changeRequestId, roundId, roundNumber: 1);
            round.IssueSideConsent(ChangeRequestSide.SideA, sideAHash, DateTime.UtcNow.AddDays(7));
            round.IssueSideConsent(ChangeRequestSide.SideB, sideBHash, DateTime.UtcNow.AddDays(7));
            await _roundRepository.InsertAsync(round, autoSave: true);
        });

        await InOfficeAsync(officeA, async () =>
        {
            (await _roundRepository.FindByTokenHashAsync(sideAHash))!.Id.ShouldBe(roundId);
            (await _roundRepository.FindByTokenHashAsync(sideBHash))!.Id.ShouldBe(roundId);
            (await _roundRepository.FindByTokenHashAsync("hash-nobody-issued")).ShouldBeNull();
        });
    }

    [Fact]
    public async Task GetCurrentAsync_returns_the_highest_non_superseded_round()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        var changeRequestId = Guid.NewGuid();
        var round2Id = Guid.NewGuid();

        await InOfficeAsync(officeA, async () =>
        {
            await SeedChangeRequestAsync(officeA, changeRequestId);

            // Round 1 was proposed, declined, then replaced -- it must never be "current"
            // again, or a finalize would gate on a date staff already abandoned.
            var round1 = NewRound(officeA, changeRequestId, Guid.NewGuid(), roundNumber: 1);
            round1.Supersede(DateTime.UtcNow);
            await _roundRepository.InsertAsync(round1, autoSave: true);

            await _roundRepository.InsertAsync(
                NewRound(officeA, changeRequestId, round2Id, roundNumber: 2), autoSave: true);
        });

        await InOfficeAsync(officeA, async () =>
        {
            var current = await _roundRepository.GetCurrentAsync(changeRequestId);

            current.ShouldNotBeNull();
            current!.Id.ShouldBe(round2Id);
            current.RoundNumber.ShouldBe(2);
        });
    }

    [Fact]
    public async Task GetCurrentAsync_returns_null_when_every_round_is_superseded()
    {
        // Staff confirmed a date, then withdrew it: the request is back to "no date confirmed",
        // which the finalize gate must read as "not consentable yet" rather than as granted.
        var (officeA, _) = await GetSeededOfficesAsync();
        var changeRequestId = Guid.NewGuid();

        await InOfficeAsync(officeA, async () =>
        {
            await SeedChangeRequestAsync(officeA, changeRequestId);
            var round = NewRound(officeA, changeRequestId, Guid.NewGuid(), roundNumber: 1);
            round.Supersede(DateTime.UtcNow);
            await _roundRepository.InsertAsync(round, autoSave: true);
        });

        await InOfficeAsync(officeA, async () =>
        {
            (await _roundRepository.GetCurrentAsync(changeRequestId)).ShouldBeNull();
        });
    }

    [Fact]
    public async Task GetCurrentAsync_returns_null_for_a_request_with_no_rounds()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            (await _roundRepository.GetCurrentAsync(Guid.NewGuid())).ShouldBeNull();
        });
    }

    private async Task SeedChangeRequestAsync(SeededOffice office, Guid changeRequestId)
    {
        await _changeRequestRepository.InsertAsync(
            new AppointmentChangeRequest(
                id: changeRequestId,
                tenantId: office.OfficeId,
                appointmentId: office.AppointmentId,
                changeRequestType: ChangeRequestType.Reschedule,
                cancellationReason: null,
                reScheduleReason: "TEST-reschedule-reason",
                newDoctorAvailabilityId: null),
            autoSave: true);
    }

    private static ChangeRequestConsentRound NewRound(
        SeededOffice office, Guid changeRequestId, Guid roundId, int roundNumber) =>
        new(
            id: roundId,
            tenantId: office.OfficeId,
            appointmentChangeRequestId: changeRequestId,
            roundNumber: roundNumber,
            proposedDoctorAvailabilityId: office.DoctorAvailabilityId,
            proposedByUserId: office.BookerUserId);

    private Task InOfficeAsync(SeededOffice office, Func<Task> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office.OfficeId))
            {
                await action();
            }
        }, requiresNew: true);
}
