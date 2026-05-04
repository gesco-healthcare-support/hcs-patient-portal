using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 11h (2026-05-04) -- pure tests for
/// <see cref="BookingFlowRoles"/>. Verifies the OLD-parity decisions
/// for the internal-user fast-path
/// (<c>AppointmentDomain.cs</c>:221-240) and the Adjuster ClaimExaminer
/// auto-fill (<c>AppointmentDomain.cs</c>:358-380).
/// </summary>
public class BookingFlowRolesUnitTests
{
    [Theory]
    [InlineData("admin", true)]
    [InlineData("Clinic Staff", true)]
    [InlineData("Staff Supervisor", true)]
    [InlineData("IT Admin", true)]
    [InlineData("Doctor", true)]
    [InlineData("Patient", false)]
    [InlineData("Applicant Attorney", false)]
    [InlineData("Defense Attorney", false)]
    [InlineData("Claim Examiner", false)]
    public void IsInternalUserCaller_ReturnsExpectedForSingleRole(string role, bool expected)
    {
        BookingFlowRoles.IsInternalUserCaller(new[] { role }).ShouldBe(expected);
    }

    [Fact]
    public void IsInternalUserCaller_AnyInternalRoleAmongMany_ReturnsTrue()
    {
        var roles = new[] { "Patient", "Clinic Staff" };
        BookingFlowRoles.IsInternalUserCaller(roles).ShouldBeTrue();
    }

    [Fact]
    public void IsInternalUserCaller_AllExternal_ReturnsFalse()
    {
        var roles = new[] { "Patient", "Applicant Attorney" };
        BookingFlowRoles.IsInternalUserCaller(roles).ShouldBeFalse();
    }

    [Fact]
    public void IsInternalUserCaller_NullOrEmpty_ReturnsFalse()
    {
        BookingFlowRoles.IsInternalUserCaller(null).ShouldBeFalse();
        BookingFlowRoles.IsInternalUserCaller(System.Array.Empty<string>()).ShouldBeFalse();
    }

    [Fact]
    public void IsInternalUserCaller_CaseInsensitive()
    {
        BookingFlowRoles.IsInternalUserCaller(new[] { "CLINIC STAFF" }).ShouldBeTrue();
        BookingFlowRoles.IsInternalUserCaller(new[] { "  it admin  " }).ShouldBeTrue();
    }

    [Fact]
    public void ResolveClaimExaminerEmail_ClaimExaminerCaller_OverridesWithCurrentEmail()
    {
        var result = BookingFlowRoles.ResolveClaimExaminerEmail(
            new[] { "Claim Examiner" },
            currentUserEmail: "ce@example.com",
            dtoClaimExaminerEmail: "someone-else@example.com");

        result.ShouldBe("ce@example.com");
    }

    [Fact]
    public void ResolveClaimExaminerEmail_ClaimExaminerCaller_CaseInsensitiveRoleMatch()
    {
        var result = BookingFlowRoles.ResolveClaimExaminerEmail(
            new[] { "claim examiner" },
            currentUserEmail: "ce@example.com",
            dtoClaimExaminerEmail: null);

        result.ShouldBe("ce@example.com");
    }

    [Fact]
    public void ResolveClaimExaminerEmail_NonClaimExaminerCaller_KeepsDtoValue()
    {
        var result = BookingFlowRoles.ResolveClaimExaminerEmail(
            new[] { "Applicant Attorney" },
            currentUserEmail: "aa@example.com",
            dtoClaimExaminerEmail: "ce@example.com");

        result.ShouldBe("ce@example.com");
    }

    [Fact]
    public void ResolveClaimExaminerEmail_ClaimExaminerCallerButNoCurrentEmail_KeepsDtoValue()
    {
        // Defensive: if CurrentUser.Email is somehow null (impersonation
        // edge cases), fall back to the DTO value rather than blowing
        // away a meaningful user choice with null.
        var result = BookingFlowRoles.ResolveClaimExaminerEmail(
            new[] { "Claim Examiner" },
            currentUserEmail: null,
            dtoClaimExaminerEmail: "ce@example.com");

        result.ShouldBe("ce@example.com");
    }

    [Fact]
    public void ResolveClaimExaminerEmail_OldAdjusterRoleName_DoesNotMatch()
    {
        // Pin: "Adjuster" is OLD's role-name for the same role NEW
        // calls "Claim Examiner". NEW's seed contributor seeds
        // "Claim Examiner" only; if a tenant DB happens to carry an
        // "Adjuster" role from a manual data load, this code path
        // does NOT auto-fill against it. Use NEW's canonical name.
        var result = BookingFlowRoles.ResolveClaimExaminerEmail(
            new[] { "Adjuster" },
            currentUserEmail: "x@example.com",
            dtoClaimExaminerEmail: "dto@example.com");

        result.ShouldBe("dto@example.com");
    }

    [Fact]
    public void ResolveClaimExaminerEmail_NullRoles_KeepsDtoValue()
    {
        var result = BookingFlowRoles.ResolveClaimExaminerEmail(
            null,
            currentUserEmail: "x@example.com",
            dtoClaimExaminerEmail: "ce@example.com");

        result.ShouldBe("ce@example.com");
    }

    [Fact]
    public void InternalUserRoles_PinnedAtFiveCanonicalRoles()
    {
        // Drift guard: if the seed contributor renames a role, this
        // surfaces immediately so the booking flow's fast-path stays
        // aligned with the role registry.
        BookingFlowRoles.InternalUserRoles.ShouldContain("admin");
        BookingFlowRoles.InternalUserRoles.ShouldContain("Clinic Staff");
        BookingFlowRoles.InternalUserRoles.ShouldContain("Staff Supervisor");
        BookingFlowRoles.InternalUserRoles.ShouldContain("IT Admin");
        BookingFlowRoles.InternalUserRoles.ShouldContain("Doctor");
        BookingFlowRoles.InternalUserRoles.Count.ShouldBe(5);
    }
}
