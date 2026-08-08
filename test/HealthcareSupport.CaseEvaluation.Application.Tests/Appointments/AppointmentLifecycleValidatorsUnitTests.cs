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

    // Phase 5 (2026-08-07) -- this gate is no longer status-only. Decision 1: a
    // re-eval MAY be booked against a NoShow / NotSeen appointment, but ONLY if
    // that appointment was itself a re-evaluation. A no-showed FIRST evaluation
    // must come back as a new appointment request. This is a DELIBERATE break
    // from the strict-OLD-parity rule the remarks on CanCreateReval describe --
    // the previous test asserted NoShow was always rejected.
    [Theory]
    // Approved is unconditional, as before -- kind is irrelevant to it.
    [InlineData(AppointmentStatusType.Approved, EvaluationKind.Evaluation, false, true)]
    [InlineData(AppointmentStatusType.Approved, EvaluationKind.ReEvaluation, false, true)]
    [InlineData(AppointmentStatusType.Approved, EvaluationKind.Evaluation, true, true)]
    // The two attendance outcomes, rescued ONLY by a ReEvaluation source.
    [InlineData(AppointmentStatusType.NoShow, EvaluationKind.ReEvaluation, false, true)]
    [InlineData(AppointmentStatusType.NotSeen, EvaluationKind.ReEvaluation, false, true)]
    [InlineData(AppointmentStatusType.NoShow, EvaluationKind.ReEvaluation, true, true)]
    [InlineData(AppointmentStatusType.NoShow, EvaluationKind.Evaluation, false, false)]
    [InlineData(AppointmentStatusType.NotSeen, EvaluationKind.Evaluation, false, false)]
    [InlineData(AppointmentStatusType.NotSeen, EvaluationKind.Evaluation, true, false)]
    // Every other status stays rejected, and a ReEvaluation kind does NOT rescue
    // it -- the rescue is scoped to the two attendance outcomes, not to re-evals
    // in general. Strict OLD parity still holds here: admin surfaces a different
    // message but does not bypass the gate.
    [InlineData(AppointmentStatusType.Pending, EvaluationKind.ReEvaluation, false, false)]
    [InlineData(AppointmentStatusType.Pending, EvaluationKind.Evaluation, true, false)]
    [InlineData(AppointmentStatusType.Rejected, EvaluationKind.ReEvaluation, true, false)]
    [InlineData(AppointmentStatusType.CancelledLate, EvaluationKind.ReEvaluation, false, false)]
    [InlineData(AppointmentStatusType.RescheduledNoBill, EvaluationKind.ReEvaluation, false, false)]
    public void CanCreateReval_AllowsApprovedAndReEvaluatedAttendanceOutcomes(
        AppointmentStatusType status,
        EvaluationKind sourceKind,
        bool callerIsItAdmin,
        bool expected)
    {
        AppointmentLifecycleValidators.CanCreateReval(status, sourceKind, callerIsItAdmin)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(AppointmentStatusType.NoShow, false)]
    [InlineData(AppointmentStatusType.NoShow, true)]
    [InlineData(AppointmentStatusType.NotSeen, false)]
    [InlineData(AppointmentStatusType.NotSeen, true)]
    public void ResolveRevalRejectionCode_IncompleteFirstEvaluation_UsesItsOwnCode(
        AppointmentStatusType status,
        bool callerIsItAdmin)
    {
        // Regardless of admin: the admin hint says "approve it and try again",
        // which is actively wrong advice here -- the appointment can never be
        // approved again, and the caller must submit a new request instead.
        AppointmentLifecycleValidators.ResolveRevalRejectionCode(
                status, EvaluationKind.Evaluation, callerIsItAdmin)
            .ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentRevalSourceIncompleteFirstEvaluation);
    }

    [Fact]
    public void ResolveRevalRejectionCode_NonAdmin_UsesPatientFacingCode()
    {
        AppointmentLifecycleValidators.ResolveRevalRejectionCode(
                AppointmentStatusType.Pending, EvaluationKind.Evaluation, callerIsItAdmin: false)
            .ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentRevalSourceNotApproved);
    }

    [Fact]
    public void ResolveRevalRejectionCode_Admin_UsesAdminHintCode()
    {
        AppointmentLifecycleValidators.ResolveRevalRejectionCode(
                AppointmentStatusType.Pending, EvaluationKind.Evaluation, callerIsItAdmin: true)
            .ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentRevalSourceNotApprovedAdminHint);
    }

    [Fact]
    public void ResolveRevalRejectionCode_AttendanceOutcomeThatWasAReEval_IsNotTheIncompleteCode()
    {
        // Defensive: this combination is ALLOWED by CanCreateReval, so the
        // resolver should never be consulted for it. If a caller ever does, it
        // must not claim the source was a first evaluation -- it was not.
        AppointmentLifecycleValidators.ResolveRevalRejectionCode(
                AppointmentStatusType.NoShow, EvaluationKind.ReEvaluation, callerIsItAdmin: false)
            .ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentRevalSourceNotApproved);
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
