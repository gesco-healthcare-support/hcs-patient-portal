using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 16 (2026-05-04) -- pure tests for
/// <see cref="RescheduleRequestValidators"/>.
/// </summary>
public class RescheduleRequestValidatorsUnitTests
{
    [Theory]
    [InlineData(AppointmentStatusType.Approved, true)]
    [InlineData(AppointmentStatusType.Pending, false)]
    [InlineData(AppointmentStatusType.Rejected, false)]
    [InlineData(AppointmentStatusType.NoShow, false)]
    [InlineData(AppointmentStatusType.CancelledNoBill, false)]
    [InlineData(AppointmentStatusType.CancelledLate, false)]
    [InlineData(AppointmentStatusType.RescheduleRequested, false)]
    [InlineData(AppointmentStatusType.RescheduledNoBill, false)]
    [InlineData(AppointmentStatusType.RescheduledLate, false)]
    [InlineData(AppointmentStatusType.CheckedIn, false)]
    [InlineData(AppointmentStatusType.CheckedOut, false)]
    [InlineData(AppointmentStatusType.Billed, false)]
    [InlineData(AppointmentStatusType.CancellationRequested, false)]
    public void CanRequestReschedule_External_AllowsOnlyApproved(AppointmentStatusType status, bool expected)
    {
        RescheduleRequestValidators.CanRequestReschedule(status, allowPendingSource: false).ShouldBe(expected);
    }

    // B1 (2026-07-01): internal staff may also reschedule a Pending appointment;
    // every other non-Approved status stays rejected.
    [Theory]
    [InlineData(AppointmentStatusType.Approved, true)]
    [InlineData(AppointmentStatusType.Pending, true)]
    [InlineData(AppointmentStatusType.Rejected, false)]
    [InlineData(AppointmentStatusType.NoShow, false)]
    [InlineData(AppointmentStatusType.CancelledNoBill, false)]
    [InlineData(AppointmentStatusType.CancelledLate, false)]
    [InlineData(AppointmentStatusType.RescheduleRequested, false)]
    [InlineData(AppointmentStatusType.RescheduledNoBill, false)]
    [InlineData(AppointmentStatusType.RescheduledLate, false)]
    [InlineData(AppointmentStatusType.CheckedIn, false)]
    [InlineData(AppointmentStatusType.CheckedOut, false)]
    [InlineData(AppointmentStatusType.Billed, false)]
    [InlineData(AppointmentStatusType.CancellationRequested, false)]
    public void CanRequestReschedule_Internal_AllowsApprovedAndPending(AppointmentStatusType status, bool expected)
    {
        RescheduleRequestValidators.CanRequestReschedule(status, allowPendingSource: true).ShouldBe(expected);
    }

    [Theory]
    [InlineData(BookingStatus.Available, true)]
    [InlineData(BookingStatus.Reserved, false)]
    [InlineData(BookingStatus.Booked, false)]
    public void IsSlotAvailable_AllowsOnlyAvailable(BookingStatus status, bool expected)
    {
        RescheduleRequestValidators.IsSlotAvailable(status).ShouldBe(expected);
    }
}
