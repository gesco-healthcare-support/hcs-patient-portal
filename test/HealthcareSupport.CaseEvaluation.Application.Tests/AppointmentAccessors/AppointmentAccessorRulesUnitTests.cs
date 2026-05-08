using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

/// <summary>
/// Phase 11i (2026-05-04) -- pure-rule tests for
/// <see cref="AppointmentAccessorRules"/>. Verifies the OLD-parity
/// branching that drives the orchestrator's side-effects in
/// <see cref="AppointmentAccessorManager.CreateOrLinkAsync"/>.
/// Mirrors OLD <c>AppointmentDomain.cs</c> lines 186-196 (validation)
/// and 222 / 290+ (Add path). The orchestrator itself is integration-
/// tested under Phase 20 E2E once the Identity stack is wired.
/// </summary>
public class AppointmentAccessorRulesUnitTests
{
    [Theory]
    [InlineData("Applicant Attorney", true)]
    [InlineData("applicant attorney", true)]   // case-insensitive
    [InlineData("  Applicant Attorney  ", true)] // trim
    [InlineData("Defense Attorney", false)]
    public void HoldsRequestedRole_TrueOnExactCaseInsensitiveTrimmedMatch(string requested, bool expected)
    {
        var roles = new[] { "Applicant Attorney", "SomeOtherRole" };
        AppointmentAccessorRules.HoldsRequestedRole(roles, requested).ShouldBe(expected);
    }

    [Fact]
    public void HoldsRequestedRole_NullRoles_ReturnsFalse()
    {
        AppointmentAccessorRules.HoldsRequestedRole(null, "Patient").ShouldBeFalse();
    }

    [Fact]
    public void HoldsRequestedRole_EmptyRequestedRole_ReturnsFalse()
    {
        AppointmentAccessorRules.HoldsRequestedRole(new[] { "Patient" }, "").ShouldBeFalse();
    }

    [Fact]
    public void HasConflictingExternalRole_UserHasDifferentExternalRole_ReturnsTrue()
    {
        var roles = new[] { "Applicant Attorney" };
        AppointmentAccessorRules.HasConflictingExternalRole(roles, "Defense Attorney").ShouldBeTrue();
    }

    [Fact]
    public void HasConflictingExternalRole_UserHasRequestedRole_ReturnsFalse()
    {
        var roles = new[] { "Applicant Attorney" };
        AppointmentAccessorRules.HasConflictingExternalRole(roles, "Applicant Attorney").ShouldBeFalse();
    }

    [Fact]
    public void HasConflictingExternalRole_UserHasOnlyInternalRole_ReturnsFalse()
    {
        // Internal-only roles do NOT trigger a mismatch -- the user can be
        // added as an accessor even if they hold "Clinic Staff" internally.
        var roles = new[] { "Clinic Staff" };
        AppointmentAccessorRules.HasConflictingExternalRole(roles, "Patient").ShouldBeFalse();
    }

    [Fact]
    public void HasConflictingExternalRole_NullRoles_ReturnsFalse()
    {
        AppointmentAccessorRules.HasConflictingExternalRole(null, "Patient").ShouldBeFalse();
    }

    [Fact]
    public void ResolveOutcome_UserDoesNotExist_ReturnsCreateUserAndLink()
    {
        AppointmentAccessorRules
            .ResolveOutcome(userExists: false, userRoles: null, requestedRole: "Patient")
            .ShouldBe(AccessorLinkOutcome.CreateUserAndLink);
    }

    [Fact]
    public void ResolveOutcome_UserExistsWithRequestedRole_ReturnsLinkExisting()
    {
        AppointmentAccessorRules
            .ResolveOutcome(
                userExists: true,
                userRoles: new[] { "Applicant Attorney" },
                requestedRole: "Applicant Attorney")
            .ShouldBe(AccessorLinkOutcome.LinkExisting);
    }

    [Fact]
    public void ResolveOutcome_UserExistsWithDifferentExternalRole_ReturnsRoleMismatch()
    {
        AppointmentAccessorRules
            .ResolveOutcome(
                userExists: true,
                userRoles: new[] { "Applicant Attorney" },
                requestedRole: "Defense Attorney")
            .ShouldBe(AccessorLinkOutcome.RoleMismatch);
    }

    [Fact]
    public void ResolveOutcome_UserExistsWithOnlyInternalRole_ReturnsGrantRoleAndLink()
    {
        // Internal staff being added as accessor: grant the requested
        // role on top of their existing internal role and link.
        AppointmentAccessorRules
            .ResolveOutcome(
                userExists: true,
                userRoles: new[] { "Clinic Staff" },
                requestedRole: "Applicant Attorney")
            .ShouldBe(AccessorLinkOutcome.GrantRoleAndLink);
    }

    [Fact]
    public void ResolveOutcome_UserExistsWithNoRoles_ReturnsGrantRoleAndLink()
    {
        AppointmentAccessorRules
            .ResolveOutcome(
                userExists: true,
                userRoles: System.Array.Empty<string>(),
                requestedRole: "Patient")
            .ShouldBe(AccessorLinkOutcome.GrantRoleAndLink);
    }

    [Fact]
    public void RecognizedExternalRoles_ContainsAllFourExpectedRoles()
    {
        // Pin the canonical 4-role list. OLD has exactly 4 external
        // roles per Roles.cs (Patient / Adjuster / PatientAttorney /
        // DefenseAttorney); NEW renamed Adjuster -> Claim Examiner and
        // PatientAttorney -> Applicant Attorney. If a role is renamed
        // in ExternalUserRoleDataSeedContributor, this test surfaces
        // the drift immediately.
        AppointmentAccessorRules.RecognizedExternalRoles.ShouldContain("Patient");
        AppointmentAccessorRules.RecognizedExternalRoles.ShouldContain("Applicant Attorney");
        AppointmentAccessorRules.RecognizedExternalRoles.ShouldContain("Defense Attorney");
        AppointmentAccessorRules.RecognizedExternalRoles.ShouldContain("Claim Examiner");
        AppointmentAccessorRules.RecognizedExternalRoles.Count.ShouldBe(4);
    }
}
