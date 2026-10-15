using System;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Pins the two-sided consent state machine on the change-request aggregate
/// (reschedule/cancel consent redesign, 2026-07-01). Side A = Patient/Applicant
/// Attorney; Side B = Defense Attorney/Claim Examiner. Party-initiated auto-grants
/// the requestor's side and tokens the opposing side; staff-initiated tokens both.
/// The finalize gate passes only when every side whose consent was required
/// (status != NotRequired) is Approved. Pure domain unit -- no DB.
/// </summary>
public class ChangeRequestConsentStateTests
{
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    private static AppointmentChangeRequest NewCancelRequest() =>
        new(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            appointmentId: Guid.NewGuid(),
            changeRequestType: ChangeRequestType.Cancel,
            cancellationReason: "Patient requested",
            reScheduleReason: null,
            newDoctorAvailabilityId: null);

    [Fact]
    public void Fresh_request_has_both_sides_not_required_and_the_gate_passes()
    {
        // Consent gating off (or never issued) -> nothing to consent -> finalize allowed.
        var request = NewCancelRequest();

        request.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.NotRequired);
        request.SideConsentStatus(ChangeRequestSide.SideB).ShouldBe(ChangeRequestConsentStatus.NotRequired);
        request.AreAllRequiredSidesGranted().ShouldBeTrue();
    }

    [Fact]
    public void Auto_grant_marks_a_side_approved_without_a_token()
    {
        var request = NewCancelRequest();

        request.AutoGrantSide(ChangeRequestSide.SideA, Now);

        request.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Approved);
        request.SideConsentTokenHash(ChangeRequestSide.SideA).ShouldBeNull();
    }

    [Fact]
    public void Party_initiated_needs_only_the_opposing_side_to_approve()
    {
        // Requestor = Side A (auto-granted); Side B is tokened and must approve.
        var request = NewCancelRequest();
        request.AutoGrantSide(ChangeRequestSide.SideA, Now);
        request.IssueSideConsent(ChangeRequestSide.SideB, "hash-b", Now.AddDays(7));

        request.AreAllRequiredSidesGranted().ShouldBeFalse();

        request.RecordSideDecision(ChangeRequestSide.SideB, approved: true, "rep-b@test", Now);

        request.AreAllRequiredSidesGranted().ShouldBeTrue();
    }

    [Fact]
    public void Staff_initiated_needs_both_sides_to_approve()
    {
        var request = NewCancelRequest();
        request.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));
        request.IssueSideConsent(ChangeRequestSide.SideB, "hash-b", Now.AddDays(7));

        request.AreAllRequiredSidesGranted().ShouldBeFalse();

        request.RecordSideDecision(ChangeRequestSide.SideA, approved: true, "rep-a@test", Now);
        request.AreAllRequiredSidesGranted().ShouldBeFalse(); // one side alone is not enough

        request.RecordSideDecision(ChangeRequestSide.SideB, approved: true, "rep-b@test", Now);
        request.AreAllRequiredSidesGranted().ShouldBeTrue();
    }

    [Fact]
    public void A_rejected_side_blocks_the_gate()
    {
        var request = NewCancelRequest();
        request.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));
        request.IssueSideConsent(ChangeRequestSide.SideB, "hash-b", Now.AddDays(7));

        request.RecordSideDecision(ChangeRequestSide.SideA, approved: true, "rep-a@test", Now);
        request.RecordSideDecision(ChangeRequestSide.SideB, approved: false, "rep-b@test", Now);

        request.AreAllRequiredSidesGranted().ShouldBeFalse();
    }

    [Fact]
    public void Missing_side_rep_left_not_required_is_satisfied()
    {
        // Staff-initiated but Side B has no representative -> only Side A is solicited.
        var request = NewCancelRequest();
        request.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));

        request.RecordSideDecision(ChangeRequestSide.SideA, approved: true, "rep-a@test", Now);

        request.AreAllRequiredSidesGranted().ShouldBeTrue();
    }

    [Fact]
    public void Expiry_defaults_a_side_to_a_no_and_blocks_the_gate()
    {
        var request = NewCancelRequest();
        request.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));

        request.IsSideExpired(ChangeRequestSide.SideA, Now.AddDays(8)).ShouldBeTrue();
        request.MarkSideExpired(ChangeRequestSide.SideA, Now.AddDays(8));

        request.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Expired);
        request.AreAllRequiredSidesGranted().ShouldBeFalse();
    }

    [Fact]
    public void Recording_a_decision_on_a_non_pending_side_throws()
    {
        var request = NewCancelRequest();

        // Side A was never issued (NotRequired) -> recording a decision is invalid.
        Should.Throw<BusinessException>(() =>
            request.RecordSideDecision(ChangeRequestSide.SideA, approved: true, "x@test", Now));
    }

    [Fact]
    public void Issuing_consent_twice_on_a_side_throws()
    {
        var request = NewCancelRequest();
        request.IssueSideConsent(ChangeRequestSide.SideA, "hash-a", Now.AddDays(7));

        Should.Throw<BusinessException>(() =>
            request.IssueSideConsent(ChangeRequestSide.SideA, "hash-a2", Now.AddDays(7)));
    }
}
