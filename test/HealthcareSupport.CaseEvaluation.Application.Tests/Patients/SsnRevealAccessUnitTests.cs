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

    [Theory]
    [InlineData("Clinic Staff")]
    [InlineData("Staff Supervisor")]
    [InlineData("IT Admin")]
    [InlineData("admin")]
    [InlineData("Doctor")]
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
}
