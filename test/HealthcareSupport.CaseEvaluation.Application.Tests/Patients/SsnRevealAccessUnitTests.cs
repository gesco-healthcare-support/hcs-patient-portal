using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// F1 / Design B (2026-05-29) -- pure tests for <see cref="SsnRevealAccess"/>,
/// the authorization predicate behind the SSN reveal endpoint
/// (PatientsAppService.GetFullSsnAsync).
///
/// Grid:
///   internal role (any)        -> true  (even when not the owner)
///   record owner (callerId==)  -> true  (even with an external role)
///   external non-owner         -> false
///   no authenticated user      -> false (unless an internal role is present)
/// </summary>
public class SsnRevealAccessUnitTests
{
    private static readonly Guid PatientUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // "Doctor" was removed from the internal-caller set in IR1 (2026-06-03,
    // BookingFlowRoles) -- it is no longer a presented internal persona, so it
    // is intentionally absent here (its old [InlineData("Doctor")] was stale).
    [Theory]
    [InlineData("Intake Staff")]
    [InlineData("Staff Supervisor")]
    [InlineData("IT Admin")]
    [InlineData("admin")]
    public void CanReveal_InternalRole_ReturnsTrueEvenWhenNotOwner(string internalRole)
    {
        var result = SsnRevealAccess.CanReveal(
            new[] { (string?)internalRole },
            callerIdentityUserId: OtherUserId,
            patientIdentityUserId: PatientUserId);
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanReveal_RecordOwner_ReturnsTrueEvenWithExternalRole()
    {
        var result = SsnRevealAccess.CanReveal(
            new[] { (string?)"Patient" },
            callerIdentityUserId: PatientUserId,
            patientIdentityUserId: PatientUserId);
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Applicant Attorney")]
    [InlineData("Defense Attorney")]
    [InlineData("Claim Examiner")]
    [InlineData("Patient")]
    public void CanReveal_ExternalNonOwner_ReturnsFalse(string externalRole)
    {
        var result = SsnRevealAccess.CanReveal(
            new[] { (string?)externalRole },
            callerIdentityUserId: OtherUserId,
            patientIdentityUserId: PatientUserId);
        result.ShouldBeFalse();
    }

    [Fact]
    public void CanReveal_NoAuthenticatedUser_ExternalContext_ReturnsFalse()
    {
        var result = SsnRevealAccess.CanReveal(
            new[] { (string?)"Patient" },
            callerIdentityUserId: null,
            patientIdentityUserId: PatientUserId);
        result.ShouldBeFalse();
    }

    [Fact]
    public void CanReveal_NullRoles_OwnerByIdMatch_ReturnsTrue()
    {
        var result = SsnRevealAccess.CanReveal(
            callerRoles: null,
            callerIdentityUserId: PatientUserId,
            patientIdentityUserId: PatientUserId);
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanReveal_EmptyRoles_NonOwner_ReturnsFalse()
    {
        var result = SsnRevealAccess.CanReveal(
            Array.Empty<string?>(),
            callerIdentityUserId: OtherUserId,
            patientIdentityUserId: PatientUserId);
        result.ShouldBeFalse();
    }

    // IP6 (2026-06-05): an unclaimed patient has a null IdentityUserId. Internal
    // staff may still reveal; no external caller can "own" an identity-less
    // record, so the owner branch must return false.
    [Fact]
    public void CanReveal_NullPatientIdentity_InternalRole_ReturnsTrue()
    {
        var result = SsnRevealAccess.CanReveal(
            new[] { (string?)"Intake Staff" },
            callerIdentityUserId: OtherUserId,
            patientIdentityUserId: null);
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanReveal_NullPatientIdentity_ExternalCaller_ReturnsFalse()
    {
        var result = SsnRevealAccess.CanReveal(
            new[] { (string?)"Patient" },
            callerIdentityUserId: OtherUserId,
            patientIdentityUserId: null);
        result.ShouldBeFalse();
    }
}
