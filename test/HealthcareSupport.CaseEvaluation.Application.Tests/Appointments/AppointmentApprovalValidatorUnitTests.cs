using System;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 12 (2026-05-04) -- pure unit tests for the
/// <see cref="AppointmentApprovalValidator"/> helpers. Verifies OLD-faithful
/// idempotency / responsible-user / rejection-notes / patient-match-override
/// semantics from
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>:312-344
/// without standing up the ABP integration harness (still gated behind the
/// Phase 4 license-checker test-host crash).
///
/// <para>Coverage:</para>
/// <list type="bullet">
///   <item>EnsureApprovable: empty responsible-user GUID,
///     idempotent re-approval (current=Approved), Pending happy path,
///     null-arg defenses.</item>
///   <item>EnsureRejectable: missing/whitespace notes, idempotent
///     re-rejection (current=Rejected), Pending happy path,
///     null-arg defenses.</item>
///   <item>ShouldOverridePatientMatch: matrix on
///     <c>(IsPatientAlreadyExist, OverridePatientMatch)</c>.</item>
/// </list>
///
/// <para>Test appointments are constructed directly via
/// <c>new Appointment(...)</c> per the user's Phase 12 directive
/// ("Seed test appointments directly via <c>new Appointment(...)</c>
/// rather than via Manager.CreateAsync"). The state-machine guard for
/// non-Pending-to-Approved illegal transitions lives in Session A's
/// <c>AppointmentManager.TransitionAsync</c> and is out of scope for
/// this validator's unit tests.</para>
/// </summary>
public class AppointmentApprovalValidatorUnitTests
{
    // ------------------------------------------------------------------
    // EnsureApprovable
    // OLD parity: AppointmentDomain.cs:312-344 (idempotency)
    //          + AppointmentDomain.cs:560-562 (responsible user required)
    // ------------------------------------------------------------------

    [Fact]
    public void EnsureApprovable_PendingWithResponsibleUser_DoesNotThrow()
    {
        var appointment = NewAppointment(AppointmentStatusType.Pending);
        var input = new ApproveAppointmentInput
        {
            PrimaryResponsibleUserId = Guid.NewGuid(),
        };

        Should.NotThrow(() => AppointmentApprovalValidator.EnsureApprovable(appointment, input));
    }

    [Fact]
    public void EnsureApprovable_EmptyResponsibleUserGuid_Throws()
    {
        var appointment = NewAppointment(AppointmentStatusType.Pending);
        var input = new ApproveAppointmentInput
        {
            PrimaryResponsibleUserId = Guid.Empty,
        };

        var ex = Should.Throw<BusinessException>(
            () => AppointmentApprovalValidator.EnsureApprovable(appointment, input));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentApprovalRequiresResponsibleUser);
    }

    [Fact]
    public void EnsureApprovable_AlreadyApproved_ThrowsIdempotent()
    {
        var appointment = NewAppointment(AppointmentStatusType.Approved);
        var input = new ApproveAppointmentInput
        {
            PrimaryResponsibleUserId = Guid.NewGuid(),
        };

        var ex = Should.Throw<BusinessException>(
            () => AppointmentApprovalValidator.EnsureApprovable(appointment, input));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentNotPendingForApproval);
    }

    [Fact]
    public void EnsureApprovable_RejectedAppointment_LeavesIllegalTransitionToManager()
    {
        // Per Phase 12 design: rejected -> approved is an illegal
        // transition. The validator stays silent so the manager's state
        // machine surfaces the canonical "InvalidTransition" error;
        // duplicating the error here would diverge from Session A's
        // wording.
        var appointment = NewAppointment(AppointmentStatusType.Rejected);
        var input = new ApproveAppointmentInput
        {
            PrimaryResponsibleUserId = Guid.NewGuid(),
        };

        Should.NotThrow(() => AppointmentApprovalValidator.EnsureApprovable(appointment, input));
    }

    [Fact]
    public void EnsureApprovable_NullAppointment_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => AppointmentApprovalValidator.EnsureApprovable(null!, new ApproveAppointmentInput()));
    }

    [Fact]
    public void EnsureApprovable_NullInput_Throws()
    {
        var appointment = NewAppointment(AppointmentStatusType.Pending);
        Should.Throw<ArgumentNullException>(
            () => AppointmentApprovalValidator.EnsureApprovable(appointment, null!));
    }

    // ------------------------------------------------------------------
    // EnsureRejectable
    // ------------------------------------------------------------------

    [Fact]
    public void EnsureRejectable_PendingWithReason_DoesNotThrow()
    {
        var appointment = NewAppointment(AppointmentStatusType.Pending);
        var input = new RejectAppointmentInput { Reason = "Insufficient injury details." };

        Should.NotThrow(() => AppointmentApprovalValidator.EnsureRejectable(appointment, input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void EnsureRejectable_NullOrWhitespaceReason_Throws(string? reason)
    {
        var appointment = NewAppointment(AppointmentStatusType.Pending);
        var input = new RejectAppointmentInput { Reason = reason };

        var ex = Should.Throw<BusinessException>(
            () => AppointmentApprovalValidator.EnsureRejectable(appointment, input));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentRejectionRequiresNotes);
    }

    [Fact]
    public void EnsureRejectable_AlreadyRejected_ThrowsIdempotent()
    {
        var appointment = NewAppointment(AppointmentStatusType.Rejected);
        var input = new RejectAppointmentInput { Reason = "redundant rejection" };

        var ex = Should.Throw<BusinessException>(
            () => AppointmentApprovalValidator.EnsureRejectable(appointment, input));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentNotPendingForRejection);
    }

    [Fact]
    public void EnsureRejectable_ApprovedAppointment_LeavesIllegalTransitionToManager()
    {
        var appointment = NewAppointment(AppointmentStatusType.Approved);
        var input = new RejectAppointmentInput { Reason = "actually rejecting an approved appt" };

        Should.NotThrow(() => AppointmentApprovalValidator.EnsureRejectable(appointment, input));
    }

    [Fact]
    public void EnsureRejectable_NullAppointment_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => AppointmentApprovalValidator.EnsureRejectable(null!, new RejectAppointmentInput { Reason = "x" }));
    }

    [Fact]
    public void EnsureRejectable_NullInput_Throws()
    {
        var appointment = NewAppointment(AppointmentStatusType.Pending);
        Should.Throw<ArgumentNullException>(
            () => AppointmentApprovalValidator.EnsureRejectable(appointment, null!));
    }

    // ------------------------------------------------------------------
    // ShouldOverridePatientMatch -- matrix
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShouldOverridePatientMatch_MatrixDecision(
        bool isPatientAlreadyExist,
        bool overrideFlag,
        bool expected)
    {
        var appointment = NewAppointment(AppointmentStatusType.Pending);
        appointment.IsPatientAlreadyExist = isPatientAlreadyExist;
        var input = new ApproveAppointmentInput
        {
            PrimaryResponsibleUserId = Guid.NewGuid(),
            OverridePatientMatch = overrideFlag,
        };

        AppointmentApprovalValidator.ShouldOverridePatientMatch(appointment, input).ShouldBe(expected);
    }

    [Fact]
    public void ShouldOverridePatientMatch_NullAppointment_ReturnsFalse()
    {
        AppointmentApprovalValidator.ShouldOverridePatientMatch(null!, new ApproveAppointmentInput()).ShouldBeFalse();
    }

    [Fact]
    public void ShouldOverridePatientMatch_NullInput_ReturnsFalse()
    {
        var appointment = NewAppointment(AppointmentStatusType.Pending);
        AppointmentApprovalValidator.ShouldOverridePatientMatch(appointment, null!).ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds an Appointment in the requested status without going through
    /// AppointmentManager.CreateAsync (per Phase 12 directive). Required
    /// fields are populated with deterministic synthetic values; the
    /// validator only reads <see cref="Appointment.AppointmentStatus"/>
    /// and <see cref="Appointment.IsPatientAlreadyExist"/>.
    /// </summary>
    private static Appointment NewAppointment(AppointmentStatusType status)
    {
        return new Appointment(
            id: Guid.NewGuid(),
            patientId: Guid.NewGuid(),
            identityUserId: Guid.NewGuid(),
            appointmentTypeId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A00001",
            appointmentStatus: status);
    }
}
