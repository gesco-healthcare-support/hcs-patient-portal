using System;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 17 (2026-05-04) -- pure unit tests for the
/// <see cref="ChangeRequestApprovalValidator"/> helpers. Replicates
/// OLD's outcome-bucket gate + admin-reason gate semantics without
/// the ABP integration harness (still gated behind the Phase 4
/// license-checker test-host crash).
/// </summary>
public class ChangeRequestApprovalValidatorUnitTests
{
    // ------------------------------------------------------------------
    // EnsurePending
    // ------------------------------------------------------------------

    [Fact]
    public void EnsurePending_PendingRequest_DoesNotThrow()
    {
        var request = NewRequest(RequestStatusType.Pending);
        Should.NotThrow(() => ChangeRequestApprovalValidator.EnsurePending(request));
    }

    [Theory]
    [InlineData(RequestStatusType.Accepted)]
    [InlineData(RequestStatusType.Rejected)]
    public void EnsurePending_NonPending_Throws(RequestStatusType status)
    {
        var request = NewRequest(status);
        var ex = Should.Throw<BusinessException>(
            () => ChangeRequestApprovalValidator.EnsurePending(request));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestAlreadyHandled);
    }

    [Fact]
    public void EnsurePending_NullRequest_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => ChangeRequestApprovalValidator.EnsurePending(null!));
    }

    // ------------------------------------------------------------------
    // EnsureCancellationOutcome
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(AppointmentStatusType.CancelledNoBill)]
    [InlineData(AppointmentStatusType.CancelledLate)]
    public void EnsureCancellationOutcome_ValidBucket_DoesNotThrow(AppointmentStatusType outcome)
    {
        Should.NotThrow(() => ChangeRequestApprovalValidator.EnsureCancellationOutcome(outcome));
    }

    [Theory]
    [InlineData(AppointmentStatusType.Approved)]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.RescheduledNoBill)]
    [InlineData(AppointmentStatusType.RescheduledLate)]
    [InlineData(AppointmentStatusType.Pending)]
    public void EnsureCancellationOutcome_InvalidBucket_Throws(AppointmentStatusType outcome)
    {
        var ex = Should.Throw<BusinessException>(
            () => ChangeRequestApprovalValidator.EnsureCancellationOutcome(outcome));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestInvalidCancellationOutcome);
    }

    // ------------------------------------------------------------------
    // EnsureRescheduleOutcome
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(AppointmentStatusType.RescheduledNoBill)]
    [InlineData(AppointmentStatusType.RescheduledLate)]
    public void EnsureRescheduleOutcome_ValidBucket_DoesNotThrow(AppointmentStatusType outcome)
    {
        Should.NotThrow(() => ChangeRequestApprovalValidator.EnsureRescheduleOutcome(outcome));
    }

    [Theory]
    [InlineData(AppointmentStatusType.Approved)]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.CancelledNoBill)]
    [InlineData(AppointmentStatusType.CancelledLate)]
    public void EnsureRescheduleOutcome_InvalidBucket_Throws(AppointmentStatusType outcome)
    {
        var ex = Should.Throw<BusinessException>(
            () => ChangeRequestApprovalValidator.EnsureRescheduleOutcome(outcome));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestInvalidRescheduleOutcome);
    }

    // ------------------------------------------------------------------
    // ResolveNewSlotAndEnsureAdminReason
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveNewSlot_NoOverride_ReturnsUserPickedSlot()
    {
        var userPicked = Guid.NewGuid();
        var resolved = ChangeRequestApprovalValidator.ResolveNewSlotAndEnsureAdminReason(
            userPickedSlotId: userPicked,
            overrideSlotId: null,
            adminReason: null);
        resolved.ShouldBe(userPicked);
    }

    [Fact]
    public void ResolveNewSlot_OverrideSameAsUserPicked_ReturnsUserPickedSlot()
    {
        var userPicked = Guid.NewGuid();
        var resolved = ChangeRequestApprovalValidator.ResolveNewSlotAndEnsureAdminReason(
            userPickedSlotId: userPicked,
            overrideSlotId: userPicked,
            adminReason: null);
        resolved.ShouldBe(userPicked);
    }

    [Fact]
    public void ResolveNewSlot_OverrideDifferent_WithReason_ReturnsOverrideSlot()
    {
        var userPicked = Guid.NewGuid();
        var overrideSlot = Guid.NewGuid();
        var resolved = ChangeRequestApprovalValidator.ResolveNewSlotAndEnsureAdminReason(
            userPickedSlotId: userPicked,
            overrideSlotId: overrideSlot,
            adminReason: "Slot conflict with another patient");
        resolved.ShouldBe(overrideSlot);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveNewSlot_OverrideDifferent_NoReason_Throws(string? reason)
    {
        var ex = Should.Throw<BusinessException>(
            () => ChangeRequestApprovalValidator.ResolveNewSlotAndEnsureAdminReason(
                userPickedSlotId: Guid.NewGuid(),
                overrideSlotId: Guid.NewGuid(),
                adminReason: reason));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestAdminReasonRequired);
    }

    [Fact]
    public void ResolveNewSlot_NullUserPicked_Throws()
    {
        Should.Throw<ArgumentException>(
            () => ChangeRequestApprovalValidator.ResolveNewSlotAndEnsureAdminReason(
                userPickedSlotId: null,
                overrideSlotId: Guid.NewGuid(),
                adminReason: "x"));
    }

    [Fact]
    public void ResolveNewSlot_EmptyUserPicked_Throws()
    {
        Should.Throw<ArgumentException>(
            () => ChangeRequestApprovalValidator.ResolveNewSlotAndEnsureAdminReason(
                userPickedSlotId: Guid.Empty,
                overrideSlotId: Guid.NewGuid(),
                adminReason: "x"));
    }

    // ------------------------------------------------------------------
    // EnsureRejectionNotes
    // ------------------------------------------------------------------

    [Fact]
    public void EnsureRejectionNotes_PresentNotes_DoesNotThrow()
    {
        Should.NotThrow(() =>
            ChangeRequestApprovalValidator.EnsureRejectionNotes("Slot already booked by an internal user."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void EnsureRejectionNotes_NullOrWhitespace_Throws(string? notes)
    {
        var ex = Should.Throw<BusinessException>(
            () => ChangeRequestApprovalValidator.EnsureRejectionNotes(notes));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestRejectionRequiresNotes);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static AppointmentChangeRequest NewRequest(RequestStatusType status)
    {
        var request = new AppointmentChangeRequest(
            id: Guid.NewGuid(),
            tenantId: null,
            appointmentId: Guid.NewGuid(),
            changeRequestType: ChangeRequestType.Cancel,
            cancellationReason: "Patient cannot attend",
            reScheduleReason: null,
            newDoctorAvailabilityId: null);
        request.RequestStatus = status;
        return request;
    }
}
