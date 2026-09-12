using System;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Phase 4e (2026-08-06) -- WHY a case was superseded, as an explicit wire value.
///
/// <para>The old appointment's terminal status is the cause, so this maps from the same
/// <see cref="AppointmentStatusType"/> that <see cref="BillingStatusWire"/> reads. It exists
/// because a bare <c>supersededByAppointmentId</c> cannot say whether the successor is a
/// reschedule replacement or something else, and a reschedule and a re-evaluation are not the
/// same relationship: a rescheduled appointment did NOT happen, a re-evaluated one did.</para>
/// </summary>
public class SupersededReasonWireTests
{
    [Theory]
    [InlineData(AppointmentStatusType.RescheduledNoBill)]
    [InlineData(AppointmentStatusType.RescheduledLate)]
    public void Both_reschedule_closures_report_the_same_cause(AppointmentStatusType status)
    {
        // The billing outcome differs between these two; the CAUSE does not. Case Tracker reads
        // billingStatus for money and supersededReason for what happened.
        SupersededReasonWire.ToWire(status).ShouldBe("RESCHEDULED");
    }

    [Fact]
    public void The_wire_value_is_a_constant_not_the_enum_name()
    {
        // Serializing the enum name would silently change the wire format the moment someone
        // renamed a member -- the same reason EvaluationKindWire exists.
        SupersededReasonWire.Rescheduled.ShouldBe("RESCHEDULED");
    }

    /// <summary>
    /// Item 4 (2026-08-17) -- the re-book flow gives a cancelled / no-showed / not-seen
    /// appointment a successor, exactly as a reschedule does, so these four statuses now have a
    /// cause to report.
    ///
    /// <para>Before this they threw, and three of them were reachable: a re-book from a CANCELLED
    /// source would have broken that source's next push outright, because cancelled appointments
    /// ARE pushed. NoShow and NotSeen were latent only because we never push them.</para>
    /// </summary>
    [Theory]
    [InlineData(AppointmentStatusType.NoShow, "NO_SHOW")]
    [InlineData(AppointmentStatusType.NotSeen, "NOT_SEEN")]
    [InlineData(AppointmentStatusType.CancelledNoBill, "CANCELLED")]
    [InlineData(AppointmentStatusType.CancelledLate, "CANCELLED")]
    public void Re_book_closures_report_why_the_appointment_did_not_happen(
        AppointmentStatusType status,
        string expected)
    {
        SupersededReasonWire.ToWire(status).ShouldBe(expected);
    }

    [Fact]
    public void Both_cancellation_outcomes_collapse_to_one_cause()
    {
        // Same reasoning as the two reschedule statuses above: this field explains WHY a case
        // closed, not what it cost. The billing split travels on billingStatus.
        SupersededReasonWire.ToWire(AppointmentStatusType.CancelledNoBill)
            .ShouldBe(SupersededReasonWire.ToWire(AppointmentStatusType.CancelledLate));
    }

    /// <summary>
    /// Still throws for statuses that genuinely supersede nothing, unlike
    /// <see cref="BillingStatusWire"/>. This is only called once a successor is known to exist, so
    /// an unmapped status means the CALLER's guard is wrong -- and a confidently wrong cause is
    /// worse for the receiver than no cause at all.
    /// </summary>
    [Theory]
    [InlineData(AppointmentStatusType.Approved)]
    [InlineData(AppointmentStatusType.Pending)]
    [InlineData(AppointmentStatusType.Rejected)]
    public void A_status_that_supersedes_nothing_throws(AppointmentStatusType status)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => SupersededReasonWire.ToWire(status));
    }
}
