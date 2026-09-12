using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Pins the reschedule finalize gate (epic phase 4c, 2026-08-05). The gate the cancel path
/// uses cannot serve here: on a reschedule the PARENT's two consent sides are both
/// <c>NotRequired</c>, and <c>AreAllRequiredSidesGranted</c> reads both-NotRequired as
/// "nothing to consent" -- which would wave a finalize through with no consent recorded at
/// all. This gate treats a missing round as a hard block instead. Pure unit -- no DB.
/// </summary>
public class RescheduleConsentGateUnitTests
{
    private static readonly DateTime Now = new(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);

    private static ChangeRequestConsentRound NewRound() =>
        new(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            appointmentChangeRequestId: Guid.NewGuid(),
            roundNumber: 1,
            proposedDoctorAvailabilityId: Guid.NewGuid(),
            proposedByUserId: Guid.NewGuid());

    [Fact]
    public void No_confirmed_date_blocks_finalize()
    {
        var ex = Should.Throw<BusinessException>(() =>
            RescheduleConsentGate.EnsureRoundConsentGranted(null, consentGatingEnabled: true));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotGranted);
    }

    [Fact]
    public void A_round_awaiting_a_side_blocks_finalize()
    {
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));
        round.IssueSideConsent(ChangeRequestSide.SideB, "hash-b", Now.AddDays(7));
        round.RecordSideDecision(ChangeRequestSide.SideA, approved: true, "rep-a@test", Now);

        var ex = Should.Throw<BusinessException>(() =>
            RescheduleConsentGate.EnsureRoundConsentGranted(round, consentGatingEnabled: true));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotGranted);
    }

    [Fact]
    public void A_rejected_side_blocks_finalize()
    {
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideB, "hash-b", Now.AddDays(7));
        round.RecordSideDecision(ChangeRequestSide.SideB, approved: false, "rep-b@test", Now);

        Should.Throw<BusinessException>(() =>
            RescheduleConsentGate.EnsureRoundConsentGranted(round, consentGatingEnabled: true));
    }

    [Fact]
    public void An_expired_side_blocks_finalize()
    {
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));
        round.MarkSideExpired(ChangeRequestSide.SideA, Now.AddDays(8));

        Should.Throw<BusinessException>(() =>
            RescheduleConsentGate.EnsureRoundConsentGranted(round, consentGatingEnabled: true));
    }

    [Fact]
    public void Every_solicited_side_approved_allows_finalize()
    {
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));
        round.IssueSideConsent(ChangeRequestSide.SideB, "hash-b", Now.AddDays(7));
        round.RecordSideDecision(ChangeRequestSide.SideA, approved: true, "rep-a@test", Now);
        round.RecordSideDecision(ChangeRequestSide.SideB, approved: true, "rep-b@test", Now);

        Should.NotThrow(() =>
            RescheduleConsentGate.EnsureRoundConsentGranted(round, consentGatingEnabled: true));
    }

    [Fact]
    public void A_side_with_no_representative_does_not_block_finalize()
    {
        // Only Side A had somebody to ask; Side B stays NotRequired and is satisfied.
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));
        round.RecordSideDecision(ChangeRequestSide.SideA, approved: true, "rep-a@test", Now);

        Should.NotThrow(() =>
            RescheduleConsentGate.EnsureRoundConsentGranted(round, consentGatingEnabled: true));
    }

    [Fact]
    public void Gating_disabled_allows_finalize_even_with_no_round()
    {
        Should.NotThrow(() =>
            RescheduleConsentGate.EnsureRoundConsentGranted(null, consentGatingEnabled: false));
    }

    [Fact]
    public void Gating_disabled_allows_finalize_even_with_a_rejected_side()
    {
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideB, "hash-b", Now.AddDays(7));
        round.RecordSideDecision(ChangeRequestSide.SideB, approved: false, "rep-b@test", Now);

        Should.NotThrow(() =>
            RescheduleConsentGate.EnsureRoundConsentGranted(round, consentGatingEnabled: false));
    }
}
