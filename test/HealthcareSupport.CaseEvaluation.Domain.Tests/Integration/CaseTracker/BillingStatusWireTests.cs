using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Phase 2 (2026-07-31) -- billing intent must reach the Case Tracker as an EXPLICIT wire value
/// instead of the receiver string-matching our status enum names. Pure mapping, so these are
/// plain unit tests (mirrors EvaluationKindWire's contract).
///
/// A wrong value here misbills a real case, which is why every billing-bearing status is pinned
/// individually rather than via a loop over the enum.
/// </summary>
public class BillingStatusWireTests
{
    [Theory]
    [InlineData(AppointmentStatusType.CancelledNoBill)]
    [InlineData(AppointmentStatusType.RescheduledNoBill)]
    public void Maps_the_no_bill_outcomes_to_NO_BILL(AppointmentStatusType status) =>
        BillingStatusWire.ToWire(status).ShouldBe("NO_BILL");

    [Theory]
    [InlineData(AppointmentStatusType.CancelledLate)]
    [InlineData(AppointmentStatusType.RescheduledLate)]
    public void Maps_the_late_outcomes_to_LATE(AppointmentStatusType status) =>
        BillingStatusWire.ToWire(status).ShouldBe("LATE");

    // Every non-billing status must still serialize a value: the field is non-nullable on the
    // wire so the receiver never has to distinguish "absent" from "no billing intent".
    [Theory]
    [InlineData(AppointmentStatusType.Pending)]
    [InlineData(AppointmentStatusType.Approved)]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.NoShow)]
    [InlineData(AppointmentStatusType.CheckedIn)]
    [InlineData(AppointmentStatusType.CheckedOut)]
    [InlineData(AppointmentStatusType.Billed)]
    [InlineData(AppointmentStatusType.RescheduleRequested)]
    [InlineData(AppointmentStatusType.CancellationRequested)]
    [InlineData(AppointmentStatusType.InfoRequested)]
    public void Maps_every_other_status_to_NONE(AppointmentStatusType status) =>
        BillingStatusWire.ToWire(status).ShouldBe("NONE");

    [Fact]
    public void Never_derives_the_wire_value_from_the_enum_name()
    {
        // Guards the whole point of an explicit mapping: renaming CancelledNoBill must not change
        // the wire format, so the wire value and the enum name must not coincide.
        BillingStatusWire.ToWire(AppointmentStatusType.CancelledNoBill)
            .ShouldNotBe(AppointmentStatusType.CancelledNoBill.ToString());
    }

    [Fact]
    public void Exposes_the_wire_values_as_constants_for_callers_to_default_to()
    {
        BillingStatusWire.NoBill.ShouldBe("NO_BILL");
        BillingStatusWire.Late.ShouldBe("LATE");
        BillingStatusWire.None.ShouldBe("NONE");
    }
}
