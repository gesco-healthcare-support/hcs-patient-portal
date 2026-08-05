using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Pins consent-token resolution after phase 4c moved RESCHEDULE consent onto
/// <see cref="ChangeRequestConsentRound"/> rows (2026-08-05). Consent now lives in two stores
/// by design -- rounds for reschedule, the request's own flat columns for cancellation -- and
/// a token from EITHER must still resolve, because cancellation consent was explicitly left
/// unchanged and pre-4c reschedule links may still be sitting in somebody's inbox.
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeConsentTokenResolutionTests : CaseEvaluationMultiOfficeTestBase
{
    private readonly ChangeRequestConsentManager _consentManager;
    private readonly IChangeRequestConsentRoundRepository _roundRepository;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;
    private readonly ICurrentTenant _currentTenant;

    public MultiOfficeConsentTokenResolutionTests()
    {
        _consentManager = GetRequiredService<ChangeRequestConsentManager>();
        _roundRepository = GetRequiredService<IChangeRequestConsentRoundRepository>();
        _changeRequestRepository = GetRequiredService<IRepository<AppointmentChangeRequest, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task A_round_token_resolves_to_its_round_request_and_side()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        var changeRequestId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        string rawToken = null!;

        await InOfficeAsync(officeA, async () =>
        {
            await SeedRescheduleRequestAsync(officeA, changeRequestId);
            var round = NewRound(officeA, changeRequestId, roundId, roundNumber: 1);
            rawToken = _consentManager.IssueSideConsent(round, ChangeRequestSide.SideB);
            await _roundRepository.InsertAsync(round, autoSave: true);
        });

        await InOfficeAsync(officeA, async () =>
        {
            var match = await _consentManager.ResolveByRawTokenAsync(rawToken);

            match.Round.ShouldNotBeNull();
            match.Round!.Id.ShouldBe(roundId);
            match.Request.Id.ShouldBe(changeRequestId);
            match.Side.ShouldBe(ChangeRequestSide.SideB);
        });
    }

    [Fact]
    public async Task A_cancellation_token_still_resolves_from_the_parent_columns_with_no_round()
    {
        // Cancellation consent was deliberately NOT migrated to rounds -- a cancellation has no
        // date to re-propose. Its tokens must keep resolving or every pending cancellation in
        // the wild breaks the moment 4c ships.
        var (officeA, _) = await GetSeededOfficesAsync();
        var changeRequestId = Guid.NewGuid();
        string rawToken = null!;

        await InOfficeAsync(officeA, async () =>
        {
            var request = await SeedCancellationRequestAsync(officeA, changeRequestId);
            rawToken = _consentManager.IssueSideConsent(request, ChangeRequestSide.SideA);
            await _changeRequestRepository.UpdateAsync(request, autoSave: true);
        });

        await InOfficeAsync(officeA, async () =>
        {
            var match = await _consentManager.ResolveByRawTokenAsync(rawToken);

            match.Round.ShouldBeNull();
            match.Request.Id.ShouldBe(changeRequestId);
            match.Side.ShouldBe(ChangeRequestSide.SideA);
        });
    }

    [Fact]
    public async Task Recording_a_decision_on_a_round_token_writes_to_the_round_not_the_parent()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        var changeRequestId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        string rawToken = null!;

        await InOfficeAsync(officeA, async () =>
        {
            await SeedRescheduleRequestAsync(officeA, changeRequestId);
            var round = NewRound(officeA, changeRequestId, roundId, roundNumber: 1);
            rawToken = _consentManager.IssueSideConsent(round, ChangeRequestSide.SideA);
            await _roundRepository.InsertAsync(round, autoSave: true);
        });

        await InOfficeAsync(officeA, async () =>
        {
            await _consentManager.RecordDecisionAsync(rawToken, approved: true, "rep-a@example.test");
        });

        await InOfficeAsync(officeA, async () =>
        {
            var round = await _roundRepository.GetAsync(roundId);
            round.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Approved);
            round.SideAConsentRespondedByEmail.ShouldBe("rep-a@example.test");

            // The parent must be untouched -- it is not where reschedule consent is recorded.
            var request = await _changeRequestRepository.GetAsync(changeRequestId);
            request.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.NotRequired);
        });
    }

    [Fact]
    public async Task Recording_a_decision_on_a_cancellation_token_still_writes_to_the_parent()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        var changeRequestId = Guid.NewGuid();
        string rawToken = null!;

        await InOfficeAsync(officeA, async () =>
        {
            var request = await SeedCancellationRequestAsync(officeA, changeRequestId);
            rawToken = _consentManager.IssueSideConsent(request, ChangeRequestSide.SideB);
            await _changeRequestRepository.UpdateAsync(request, autoSave: true);
        });

        await InOfficeAsync(officeA, async () =>
        {
            await _consentManager.RecordDecisionAsync(rawToken, approved: false, "rep-b@example.test");
        });

        await InOfficeAsync(officeA, async () =>
        {
            var request = await _changeRequestRepository.GetAsync(changeRequestId);
            request.SideConsentStatus(ChangeRequestSide.SideB).ShouldBe(ChangeRequestConsentStatus.Rejected);
            request.SideBConsentRespondedByEmail.ShouldBe("rep-b@example.test");
        });
    }

    [Fact]
    public async Task An_expired_round_token_is_defaulted_to_a_no_and_reported_as_expired()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        var changeRequestId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        // A raw token with a KNOWN value so the round can be seeded already past its expiry;
        // the manager always issues 7 days out, which no test can wait for.
        var rawToken = "TEST-expired-round-token";

        await InOfficeAsync(officeA, async () =>
        {
            await SeedRescheduleRequestAsync(officeA, changeRequestId);
            var round = NewRound(officeA, changeRequestId, roundId, roundNumber: 1);
            round.IssueSideConsent(
                ChangeRequestSide.SideA,
                ChangeRequestConsentManager.ComputeTokenHash(rawToken),
                DateTime.UtcNow.AddDays(-1));
            await _roundRepository.InsertAsync(round, autoSave: true);
        });

        await InOfficeAsync(officeA, async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(
                () => _consentManager.RecordDecisionAsync(rawToken, approved: true, "rep-a@example.test"));
            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentExpired);
        });

        await InOfficeAsync(officeA, async () =>
        {
            var round = await _roundRepository.GetAsync(roundId);
            round.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Expired);
        });
    }

    [Fact]
    public async Task An_unknown_token_is_rejected()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await InOfficeAsync(officeA, async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(
                () => _consentManager.ResolveByRawTokenAsync("TEST-nobody-ever-issued-this"));
            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentTokenInvalid);
        });
    }

    [Fact]
    public async Task An_oversized_token_is_rejected_before_a_db_roundtrip()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        var oversized = new string('a', AppointmentChangeRequestConsts.ConsentEncodedTokenMaxLength + 1);

        await InOfficeAsync(officeA, async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(
                () => _consentManager.ResolveByRawTokenAsync(oversized));
            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentTokenInvalid);
        });
    }

    private async Task SeedRescheduleRequestAsync(SeededOffice office, Guid changeRequestId)
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

    private async Task<AppointmentChangeRequest> SeedCancellationRequestAsync(
        SeededOffice office, Guid changeRequestId)
    {
        return await _changeRequestRepository.InsertAsync(
            new AppointmentChangeRequest(
                id: changeRequestId,
                tenantId: office.OfficeId,
                appointmentId: office.AppointmentId,
                changeRequestType: ChangeRequestType.Cancel,
                cancellationReason: "TEST-cancellation-reason",
                reScheduleReason: null,
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
