using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 4d (2026-08-05) -- pure tests for <see cref="RescheduleSplitPolicy"/>, which decides how
/// the OLD appointment closes when a reschedule is finalized.
///
/// <para>Replaces <c>RescheduleInPlacePolicy</c>. That policy existed because B2/4c kept ONE
/// appointment and therefore had to preserve its status; 4d creates a second appointment, so the
/// old one finally moves to a terminal Rescheduled status via the two state-machine transitions
/// that have been defined but unreachable since the state machine was written.</para>
/// </summary>
public class RescheduleSplitPolicyUnitTests
{
    [Fact]
    public void NoBill_outcome_maps_to_the_ConfirmReschedule_trigger()
    {
        RescheduleSplitPolicy.ResolveOldAppointmentTrigger(AppointmentStatusType.RescheduledNoBill)
            .ShouldBe(AppointmentTransitionTrigger.ConfirmReschedule);
    }

    [Fact]
    public void Late_outcome_maps_to_the_ConfirmRescheduleLate_trigger()
    {
        RescheduleSplitPolicy.ResolveOldAppointmentTrigger(AppointmentStatusType.RescheduledLate)
            .ShouldBe(AppointmentTransitionTrigger.ConfirmRescheduleLate);
    }

    /// <summary>
    /// The billing outcome reaches this from an API input. Anything outside the two reschedule
    /// buckets would drive the old appointment into a status the reschedule flow never intends --
    /// a cancellation bucket, or worse, back to Approved.
    /// </summary>
    [Theory]
    [InlineData(AppointmentStatusType.Approved)]
    [InlineData(AppointmentStatusType.Pending)]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.CancelledNoBill)]
    [InlineData(AppointmentStatusType.CancelledLate)]
    [InlineData(AppointmentStatusType.NoShow)]
    public void Any_other_outcome_is_rejected(AppointmentStatusType outcome)
    {
        var ex = Should.Throw<BusinessException>(
            () => RescheduleSplitPolicy.ResolveOldAppointmentTrigger(outcome));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestInvalidRescheduleOutcome);
    }

    // ---- the replacement appointment's starting status ----

    /// <summary>
    /// An external reschedule requires an Approved source, which the submit flow moves to
    /// RescheduleRequested. Both mean "this appointment was approved", so the replacement is too.
    /// </summary>
    [Theory]
    [InlineData(AppointmentStatusType.Approved)]
    [InlineData(AppointmentStatusType.RescheduleRequested)]
    public void An_approved_source_yields_an_approved_replacement(AppointmentStatusType sourceStatus)
    {
        RescheduleSplitPolicy.ResolveNewAppointmentStatus(sourceStatus)
            .ShouldBe(AppointmentStatusType.Approved);
    }

    /// <summary>
    /// B1 (2026-07-01) lets internal staff reschedule a still-Pending appointment. The replacement
    /// must stay Pending: handing it Approved would slip past the approval gate and the
    /// claim-information check purely because someone rescheduled it.
    /// </summary>
    [Fact]
    public void A_pending_source_yields_a_pending_replacement()
    {
        RescheduleSplitPolicy.ResolveNewAppointmentStatus(AppointmentStatusType.Pending)
            .ShouldBe(AppointmentStatusType.Pending);
    }

    [Theory]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.CancelledNoBill)]
    [InlineData(AppointmentStatusType.NoShow)]
    [InlineData(AppointmentStatusType.RescheduledNoBill)]
    public void A_source_that_should_never_reach_finalize_is_rejected(AppointmentStatusType sourceStatus)
    {
        var ex = Should.Throw<BusinessException>(
            () => RescheduleSplitPolicy.ResolveNewAppointmentStatus(sourceStatus));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestAppointmentNotApproved);
    }

    // ---- the parent appointment's status when a reschedule request is REJECTED ----

    /// <summary>
    /// An Approved source moved to RescheduleRequested on submit, so rejecting the request reverts
    /// it to Approved. (A source that is somehow still Approved reverts to Approved unchanged.)
    /// </summary>
    [Theory]
    [InlineData(AppointmentStatusType.RescheduleRequested)]
    [InlineData(AppointmentStatusType.Approved)]
    public void Rejecting_an_approved_source_reverts_it_to_approved(AppointmentStatusType sourceStatus)
    {
        RescheduleSplitPolicy.ResolveParentStatusOnReject(sourceStatus)
            .ShouldBe(AppointmentStatusType.Approved);
    }

    /// <summary>
    /// The regression guard for the reject-promotes-Pending bug. B1 lets internal staff reschedule
    /// a still-Pending appointment, which never leaves Pending. Rejecting that reschedule must leave
    /// it Pending -- the old code hardcoded Approved here, promoting a never-approved appointment
    /// past the approval gate purely because its reschedule was rejected.
    /// </summary>
    [Fact]
    public void Rejecting_a_pending_source_leaves_it_pending()
    {
        RescheduleSplitPolicy.ResolveParentStatusOnReject(AppointmentStatusType.Pending)
            .ShouldBe(AppointmentStatusType.Pending);
    }

    /// <summary>
    /// A reject must never fail hard and strand a change request, so an unexpected source status is
    /// left untouched rather than coerced (or thrown) -- and never silently promoted.
    /// </summary>
    [Theory]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.CancelledNoBill)]
    [InlineData(AppointmentStatusType.NoShow)]
    public void Rejecting_an_unexpected_source_leaves_it_untouched(AppointmentStatusType sourceStatus)
    {
        RescheduleSplitPolicy.ResolveParentStatusOnReject(sourceStatus)
            .ShouldBe(sourceStatus);
    }
}
