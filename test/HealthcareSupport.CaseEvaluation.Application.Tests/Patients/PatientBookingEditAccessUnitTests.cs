using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// #598 -- pure tests for <see cref="PatientBookingEditAccess"/>, the rule for who may edit a patient
/// record through the booking flow.
///
/// Grid:
///   internal role (any)                -> true  (even when not the owner)
///   record owner (callerId == login)   -> true  (even with an external role)
///   external non-owner, claimed record -> false
///   external caller, UNCLAIMED record  -> false (no owner exists, so only staff may edit)
///   no authenticated user              -> false
///
/// There is deliberately no "party to the appointment" row: the predicate takes no party input at
/// all, because every party relationship is self-grantable by booking. The service-level tests
/// prove that a party still cannot edit.
/// </summary>
public class PatientBookingEditAccessUnitTests
{
    private static readonly Guid PatientUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Theory]
    [InlineData("Intake Staff")]
    [InlineData("Staff Supervisor")]
    [InlineData("IT Admin")]
    [InlineData("admin")]
    public void CanEdit_InternalRole_ReturnsTrueEvenWhenNotOwner(string internalRole)
    {
        PatientBookingEditAccess.CanEdit(
            new[] { (string?)internalRole },
            callerIdentityUserId: OtherUserId,
            patientIdentityUserId: PatientUserId).ShouldBeTrue();
    }

    [Fact]
    public void CanEdit_RecordOwner_ReturnsTrueEvenWithExternalRole()
    {
        PatientBookingEditAccess.CanEdit(
            new[] { (string?)"Patient" },
            callerIdentityUserId: PatientUserId,
            patientIdentityUserId: PatientUserId).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Applicant Attorney")]
    [InlineData("Defense Attorney")]
    [InlineData("Claim Examiner")]
    [InlineData("Patient")]
    public void CanEdit_ExternalNonOwnerOfAClaimedRecord_ReturnsFalse(string externalRole)
    {
        PatientBookingEditAccess.CanEdit(
            new[] { (string?)externalRole },
            callerIdentityUserId: OtherUserId,
            patientIdentityUserId: PatientUserId).ShouldBeFalse();
    }

    /// <summary>
    /// The case that matters most in practice: booking creates record-only patients with no login, so
    /// most rows are unclaimed. An unclaimed record has no owner, and an external caller must not be
    /// treated as one.
    /// </summary>
    [Fact]
    public void CanEdit_ExternalCallerOnAnUnclaimedRecord_ReturnsFalse()
    {
        PatientBookingEditAccess.CanEdit(
            new[] { (string?)"Applicant Attorney" },
            callerIdentityUserId: OtherUserId,
            patientIdentityUserId: null).ShouldBeFalse();
    }

    [Fact]
    public void CanEdit_NoAuthenticatedCaller_ReturnsFalse()
    {
        PatientBookingEditAccess.CanEdit(
            callerRoles: null,
            callerIdentityUserId: null,
            patientIdentityUserId: PatientUserId).ShouldBeFalse();
    }

    /// <summary>
    /// Guid.Empty is not a login. Without the HasValue checks a caller with an empty id would "own"
    /// any record whose login is also empty.
    /// </summary>
    [Fact]
    public void CanEdit_EmptyCallerIdAgainstAnUnclaimedRecord_ReturnsFalse()
    {
        PatientBookingEditAccess.CanEdit(
            new[] { (string?)"Patient" },
            callerIdentityUserId: Guid.Empty,
            patientIdentityUserId: null).ShouldBeFalse();
    }
}
