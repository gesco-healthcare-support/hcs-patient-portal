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
}
