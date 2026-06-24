using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 11e (2026-05-04) -- pure-predicate tests for
/// <see cref="AppointmentLifecycleValidators"/>. Verifies the OLD-parity
/// gates for Re-Submit and Reval flows match
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>
/// lines 162-184 (validation) and 240-275 (Add path branching).
/// </summary>
public class AppointmentLifecycleValidatorsUnitTests
{
    [Theory]
    [InlineData(AppointmentStatusType.Rejected, true)]
    [InlineData(AppointmentStatusType.Approved, false)]
    [InlineData(AppointmentStatusType.Pending, false)]
    [InlineData(AppointmentStatusType.NoShow, false)]
    [InlineData(AppointmentStatusType.CancelledNoBill, false)]
    [InlineData(AppointmentStatusType.CancelledLate, false)]
    [InlineData(AppointmentStatusType.RescheduledNoBill, false)]
    [InlineData(AppointmentStatusType.RescheduledLate, false)]
    [InlineData(AppointmentStatusType.CheckedIn, false)]
    [InlineData(AppointmentStatusType.CheckedOut, false)]
    [InlineData(AppointmentStatusType.Billed, false)]
    [InlineData(AppointmentStatusType.RescheduleRequested, false)]
    [InlineData(AppointmentStatusType.CancellationRequested, false)]
    public void CanResubmit_AllowsOnlyRejected(AppointmentStatusType status, bool expected)
    {
        AppointmentLifecycleValidators.CanResubmit(status).ShouldBe(expected);
    }

    [Theory]
    [InlineData(AppointmentStatusType.Approved, false, true)]
    [InlineData(AppointmentStatusType.Approved, true, true)]
    // Strict OLD parity: admin override surfaces a different error message
    // (line 172) but does NOT bypass the gate. The non-Approved + admin
    // case should still return false.
    [InlineData(AppointmentStatusType.Pending, true, false)]
    [InlineData(AppointmentStatusType.Pending, false, false)]
    [InlineData(AppointmentStatusType.Rejected, true, false)]
    [InlineData(AppointmentStatusType.Rejected, false, false)]
    [InlineData(AppointmentStatusType.NoShow, true, false)]
    public void CanCreateReval_AllowsOnlyApprovedRegardlessOfAdmin(
        AppointmentStatusType status,
        bool callerIsItAdmin,
        bool expected)
    {
        AppointmentLifecycleValidators.CanCreateReval(status, callerIsItAdmin).ShouldBe(expected);
    }

    [Fact]
    public void ResolveRevalRejectionCode_NonAdmin_UsesPatientFacingCode()
    {
        AppointmentLifecycleValidators.ResolveRevalRejectionCode(callerIsItAdmin: false)
            .ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentRevalSourceNotApproved);
    }

    [Fact]
    public void ResolveRevalRejectionCode_Admin_UsesAdminHintCode()
    {
        AppointmentLifecycleValidators.ResolveRevalRejectionCode(callerIsItAdmin: true)
            .ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentRevalSourceNotApprovedAdminHint);
    }

    [Fact]
    public void ResolveConfirmationNumber_ReSubmit_ReusesSourceNumber()
    {
        var result = AppointmentLifecycleValidators.ResolveConfirmationNumber(
            AppointmentLifecycleFlow.ReSubmit,
            sourceConfirmationNumber: "A12345",
            newlyGeneratedConfirmationNumber: "A99999");

        result.ShouldBe("A12345");
    }

    [Fact]
    public void ResolveConfirmationNumber_Reval_UsesFreshlyGeneratedNumber()
    {
        var result = AppointmentLifecycleValidators.ResolveConfirmationNumber(
            AppointmentLifecycleFlow.Reval,
            sourceConfirmationNumber: "A12345",
            newlyGeneratedConfirmationNumber: "A99999");

        result.ShouldBe("A99999");
    }

    [Fact]
    public void ResolveConfirmationNumber_NullSource_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            AppointmentLifecycleValidators.ResolveConfirmationNumber(
                AppointmentLifecycleFlow.ReSubmit,
                sourceConfirmationNumber: null!,
                newlyGeneratedConfirmationNumber: "A99999"));
    }

    [Fact]
    public void ResolveConfirmationNumber_NullNewlyGenerated_Throws()
    {
        // Even ReSubmit (which doesn't *use* the newly generated number) should
        // still demand it -- the helper rejects ambiguous inputs at the boundary
        // so the caller can't accidentally pass the wrong placeholder.
        Should.Throw<ArgumentException>(() =>
            AppointmentLifecycleValidators.ResolveConfirmationNumber(
                AppointmentLifecycleFlow.ReSubmit,
                sourceConfirmationNumber: "A12345",
                newlyGeneratedConfirmationNumber: null!));
    }

    [Fact]
    public void ResolveConfirmationNumber_UnknownFlow_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            AppointmentLifecycleValidators.ResolveConfirmationNumber(
                (AppointmentLifecycleFlow)99,
                sourceConfirmationNumber: "A12345",
                newlyGeneratedConfirmationNumber: "A99999"));
    }
}
