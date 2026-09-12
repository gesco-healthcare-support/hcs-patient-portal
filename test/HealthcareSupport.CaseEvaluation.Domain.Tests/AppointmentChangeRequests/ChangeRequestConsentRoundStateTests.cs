using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Pins the per-round consent state machine (epic phase 4c, 2026-08-05). A round is one
/// staff-confirmed reschedule date plus the consent both sides gave (or withheld) for it.
/// Superseding a round and opening the next is what lets a declined date be re-proposed --
/// <see cref="AppointmentChangeRequest.IssueSideConsent"/> is only valid from
/// <c>NotRequired</c>, so a fresh row is the reset. Pure domain unit -- no DB.
/// </summary>
public class ChangeRequestConsentRoundStateTests
{
    private static readonly DateTime Now = new(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);

    private static ChangeRequestConsentRound NewRound(int roundNumber = 1) =>
        new(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            appointmentChangeRequestId: Guid.NewGuid(),
            roundNumber: roundNumber,
            proposedDoctorAvailabilityId: Guid.NewGuid(),
            proposedByUserId: Guid.NewGuid());

    [Fact]
    public void A_new_round_starts_unsent_unsuperseded_and_with_neither_side_solicited()
    {
        var round = NewRound();

        round.RoundNumber.ShouldBe(1);
        round.SendAttempts.ShouldBe(1);
        round.SupersededAt.ShouldBeNull();
        round.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.NotRequired);
        round.SideConsentStatus(ChangeRequestSide.SideB).ShouldBe(ChangeRequestConsentStatus.NotRequired);
    }

    [Fact]
    public void A_round_number_below_one_is_rejected()
    {
        // Rounds are 1-based and the unique index is (TenantId, ChangeRequestId, RoundNumber);
        // a 0 would silently collide with nothing and read as "no round".
        Should.Throw<ArgumentException>(() => NewRound(roundNumber: 0));
    }

    [Fact]
    public void A_round_without_a_proposed_slot_is_rejected()
    {
        // The entire point of a round is the date it proposes -- an empty slot would ask both
        // sides to consent to nothing (the 4b stale-reader failure, reintroduced).
        Should.Throw<ArgumentException>(() => new ChangeRequestConsentRound(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            appointmentChangeRequestId: Guid.NewGuid(),
            roundNumber: 1,
            proposedDoctorAvailabilityId: Guid.Empty,
            proposedByUserId: null));
    }

    [Fact]
    public void Issuing_consent_moves_a_side_to_pending_and_stores_its_hash()
    {
        var round = NewRound();

        round.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));

        round.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Pending);
        round.SideConsentTokenHash(ChangeRequestSide.SideA).ShouldBe("hash-a");
        round.SideConsentStatus(ChangeRequestSide.SideB).ShouldBe(ChangeRequestConsentStatus.NotRequired);
    }

    [Fact]
    public void Both_solicited_sides_must_approve_before_the_gate_opens()
    {
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));
        round.IssueSideConsent(ChangeRequestSide.SideB, "hash-b", Now.AddDays(7));

        round.AreAllRequiredSidesGranted().ShouldBeFalse();

        round.RecordSideDecision(ChangeRequestSide.SideA, approved: true, "rep-a@test", Now);
        round.AreAllRequiredSidesGranted().ShouldBeFalse();

        round.RecordSideDecision(ChangeRequestSide.SideB, approved: true, "rep-b@test", Now);
        round.AreAllRequiredSidesGranted().ShouldBeTrue();
    }

    [Fact]
    public void A_side_with_no_representative_stays_not_required_and_is_satisfied()
    {
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));

        round.RecordSideDecision(ChangeRequestSide.SideA, approved: true, "rep-a@test", Now);

        round.AreAllRequiredSidesGranted().ShouldBeTrue();
    }

    [Fact]
    public void A_rejected_side_blocks_the_gate_and_records_who_answered()
    {
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideB, "hash-b", Now.AddDays(7));

        round.RecordSideDecision(ChangeRequestSide.SideB, approved: false, "rep-b@test", Now);

        round.SideConsentStatus(ChangeRequestSide.SideB).ShouldBe(ChangeRequestConsentStatus.Rejected);
        round.SideBConsentRespondedByEmail.ShouldBe("rep-b@test");
        round.SideBConsentRespondedAt.ShouldBe(Now);
        round.AreAllRequiredSidesGranted().ShouldBeFalse();
    }

    [Fact]
    public void Deciding_twice_on_one_side_throws()
    {
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));
        round.RecordSideDecision(ChangeRequestSide.SideA, approved: true, "rep-a@test", Now);

        Should.Throw<BusinessException>(() =>
            round.RecordSideDecision(ChangeRequestSide.SideA, approved: false, "rep-a@test", Now));
    }

    [Fact]
    public void Deciding_on_an_unsolicited_side_throws()
    {
        var round = NewRound();

        Should.Throw<BusinessException>(() =>
            round.RecordSideDecision(ChangeRequestSide.SideA, approved: true, "rep-a@test", Now));
    }

    [Fact]
    public void Issuing_consent_twice_on_one_side_throws()
    {
        // The reset for a declined side is a NEW ROUND, never a re-issue on the same one.
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));

        Should.Throw<BusinessException>(() =>
            round.IssueSideConsent(ChangeRequestSide.SideA, "hash-a2", Now.AddDays(7)));
    }

    [Fact]
    public void An_expired_pending_side_can_be_marked_expired_and_blocks_the_gate()
    {
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));

        round.IsSideExpired(ChangeRequestSide.SideA, Now.AddDays(8)).ShouldBeTrue();
        round.MarkSideExpired(ChangeRequestSide.SideA, Now.AddDays(8));

        round.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Expired);
        round.SideAConsentRespondedAt.ShouldBe(Now.AddDays(8));
        round.AreAllRequiredSidesGranted().ShouldBeFalse();
    }

    [Fact]
    public void Marking_a_decided_side_expired_is_a_no_op()
    {
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));
        round.RecordSideDecision(ChangeRequestSide.SideA, approved: true, "rep-a@test", Now);

        round.MarkSideExpired(ChangeRequestSide.SideA, Now.AddDays(8));

        round.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Approved);
        round.SideAConsentRespondedAt.ShouldBe(Now);
    }

    [Fact]
    public void A_resend_bumps_the_attempt_count_and_touches_nothing_else()
    {
        // The resend must reuse the SAME tokens -- a party may already be holding the link.
        // Only the attempt counter moves, because it is what makes the outbox idempotency
        // key distinct (NotificationOutboxManager silently returns the existing row otherwise).
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));
        round.IssueSideConsent(ChangeRequestSide.SideB, "hash-b", Now.AddDays(7));

        round.RegisterResend();

        round.SendAttempts.ShouldBe(2);
        round.SideConsentTokenHash(ChangeRequestSide.SideA).ShouldBe("hash-a");
        round.SideConsentTokenHash(ChangeRequestSide.SideB).ShouldBe("hash-b");
        round.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Pending);
        round.SideConsentStatus(ChangeRequestSide.SideB).ShouldBe(ChangeRequestConsentStatus.Pending);
        round.SideAConsentExpiresAt.ShouldBe(Now.AddDays(7));
    }

    [Fact]
    public void Reissuing_a_pending_side_swaps_its_token_and_restarts_its_window()
    {
        // The resend mints a NEW token because only the hash is stored, so the original raw
        // token cannot be recovered to rebuild its URL (Adrian, 2026-08-05). The link in the
        // superseded email stops working -- deliberate and accepted.
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));

        round.ReissueSideConsent(ChangeRequestSide.SideA, "hash-a-v2", Now.AddDays(14));

        round.SideConsentTokenHash(ChangeRequestSide.SideA).ShouldBe("hash-a-v2");
        round.SideAConsentExpiresAt.ShouldBe(Now.AddDays(14));
        round.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Pending);
    }

    [Fact]
    public void Reissuing_a_side_that_already_answered_throws()
    {
        // Re-asking a side that decided needs a NEW ROUND, not a resend -- otherwise a resend
        // could quietly reopen a decision the party already made.
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));
        round.RecordSideDecision(ChangeRequestSide.SideA, approved: false, "rep-a@test", Now);

        Should.Throw<BusinessException>(() =>
            round.ReissueSideConsent(ChangeRequestSide.SideA, "hash-a-v2", Now.AddDays(14)));
    }

    [Fact]
    public void Reissuing_a_side_that_was_never_solicited_throws()
    {
        var round = NewRound();

        Should.Throw<BusinessException>(() =>
            round.ReissueSideConsent(ChangeRequestSide.SideB, "hash-b", Now.AddDays(7)));
    }

    [Fact]
    public void The_round_carries_the_reason_staff_gave_for_this_date()
    {
        var round = new ChangeRequestConsentRound(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            appointmentChangeRequestId: Guid.NewGuid(),
            roundNumber: 2,
            proposedDoctorAvailabilityId: Guid.NewGuid(),
            proposedByUserId: Guid.NewGuid(),
            proposedReason: "TEST-doctor unavailable that week");

        round.ProposedReason.ShouldBe("TEST-doctor unavailable that week");
    }

    [Fact]
    public void Superseding_stamps_the_time_once_and_is_idempotent()
    {
        var round = NewRound();

        round.Supersede(Now);
        round.Supersede(Now.AddDays(1));

        round.SupersededAt.ShouldBe(Now);
    }

    [Fact]
    public void A_superseded_round_keeps_the_decisions_it_already_collected()
    {
        // The audit trail is the whole reason rounds are rows: "who declined which date"
        // must stay queryable after the date is replaced.
        var round = NewRound();
        round.IssueSideConsent(ChangeRequestSide.SideB, "hash-b", Now.AddDays(7));
        round.RecordSideDecision(ChangeRequestSide.SideB, approved: false, "rep-b@test", Now);

        round.Supersede(Now.AddHours(1));

        round.SideConsentStatus(ChangeRequestSide.SideB).ShouldBe(ChangeRequestConsentStatus.Rejected);
        round.SideBConsentRespondedByEmail.ShouldBe("rep-b@test");
        round.SupersededAt.ShouldBe(Now.AddHours(1));
    }
}
