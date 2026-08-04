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

    // Phase 4b (2026-08-04): a null user pick is now the NORMAL case -- the requestor sends a
    // reason only and staff choose the slot -- so the staff pick arrives as overrideSlotId with
    // nothing to "override". The two tests that previously pinned ArgumentException here were
    // replaced deliberately: they encoded the pre-4b contract.

    [Fact]
    public void ResolveNewSlot_NullUserPicked_WithStaffPick_ReturnsStaffPickAndNeedsNoReason()
    {
        var staffPick = Guid.NewGuid();

        var resolved = ChangeRequestApprovalValidator.ResolveNewSlotAndEnsureAdminReason(
            userPickedSlotId: null,
            overrideSlotId: staffPick,
            adminReason: null);

        resolved.ShouldBe(staffPick);
    }

    [Fact]
    public void ResolveNewSlot_EmptyUserPicked_WithStaffPick_ReturnsStaffPick()
    {
        // Guid.Empty is treated as "nothing proposed", matching the pre-4b coalescing.
        var staffPick = Guid.NewGuid();

        var resolved = ChangeRequestApprovalValidator.ResolveNewSlotAndEnsureAdminReason(
            userPickedSlotId: Guid.Empty,
            overrideSlotId: staffPick,
            adminReason: null);

        resolved.ShouldBe(staffPick);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void ResolveNewSlot_NoUserPickAndNoStaffPick_Throws(string? overrideSlot)
    {
        // Nothing to schedule onto -- a friendly BusinessException, not a 500.
        var ex = Should.Throw<BusinessException>(
            () => ChangeRequestApprovalValidator.ResolveNewSlotAndEnsureAdminReason(
                userPickedSlotId: null,
                overrideSlotId: overrideSlot == null ? null : Guid.Parse(overrideSlot),
                adminReason: null));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestNewSlotRequired);
    }

    // ------------------------------------------------------------------
    // IsAdminOverride (phase 4b, 2026-08-04)
    // ------------------------------------------------------------------

    [Fact]
    public void IsAdminOverride_StaffPickWithNoProposal_IsNotAnOverride()
    {
        // The 4b external path: nobody proposed a date, so the staff pick overrules no one.
        // Reported as an override, every requestor would get "changed by our team".
        ChangeRequestApprovalValidator
            .IsAdminOverride(proposedSlotId: null, staffSlotId: Guid.NewGuid())
            .ShouldBeFalse();
    }

    [Fact]
    public void IsAdminOverride_StaffPickDiffersFromProposal_IsAnOverride()
    {
        ChangeRequestApprovalValidator
            .IsAdminOverride(proposedSlotId: Guid.NewGuid(), staffSlotId: Guid.NewGuid())
            .ShouldBeTrue();
    }

    [Fact]
    public void IsAdminOverride_StaffAcceptsTheProposal_IsNotAnOverride()
    {
        var proposed = Guid.NewGuid();
        ChangeRequestApprovalValidator
            .IsAdminOverride(proposedSlotId: proposed, staffSlotId: proposed)
            .ShouldBeFalse();
    }

    [Fact]
    public void IsAdminOverride_NoStaffPick_IsNotAnOverride()
    {
        ChangeRequestApprovalValidator
            .IsAdminOverride(proposedSlotId: Guid.NewGuid(), staffSlotId: null)
            .ShouldBeFalse();
    }

    [Fact]
    public void IsAdminOverride_EmptyGuidsCountAsNotSupplied()
    {
        ChangeRequestApprovalValidator
            .IsAdminOverride(proposedSlotId: Guid.Empty, staffSlotId: Guid.NewGuid())
            .ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    // ResolveScheduledSlotId (phase 4b, 2026-08-04)
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveScheduledSlotId_PrefersTheStaffChoice()
    {
        var staffChoice = Guid.NewGuid();
        ChangeRequestApprovalValidator
            .ResolveScheduledSlotId(adminOverrideSlotId: staffChoice, newDoctorAvailabilityId: Guid.NewGuid())
            .ShouldBe(staffChoice);
    }

    [Fact]
    public void ResolveScheduledSlotId_FallsBackToTheSubmitProposal()
    {
        var proposed = Guid.NewGuid();
        ChangeRequestApprovalValidator
            .ResolveScheduledSlotId(adminOverrideSlotId: null, newDoctorAvailabilityId: proposed)
            .ShouldBe(proposed);
    }

    [Fact]
    public void ResolveScheduledSlotId_StaffChoiceOnlyStillResolves()
    {
        // The 4b external path. Resolving to null here is what blanked the date in the
        // approval email, because ResolveNewSlotAsync maps null to empty strings.
        var staffChoice = Guid.NewGuid();
        ChangeRequestApprovalValidator
            .ResolveScheduledSlotId(adminOverrideSlotId: staffChoice, newDoctorAvailabilityId: null)
            .ShouldBe(staffChoice);
    }

    [Fact]
    public void ResolveScheduledSlotId_NeitherSupplied_IsNull()
    {
        ChangeRequestApprovalValidator
            .ResolveScheduledSlotId(adminOverrideSlotId: null, newDoctorAvailabilityId: null)
            .ShouldBeNull();
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
