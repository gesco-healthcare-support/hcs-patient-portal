using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Pins the host-account eligibility gate (item C, 2026-08-22).
///
/// <para>The gate decides whether a user with NO tenant may be served a password-reset or
/// verification email. It exists because the previous blanket refusal broke self-service for internal
/// operators after Phase D made them host logins -- but lifting it entirely would enable reset on a
/// hand-made host row carrying an external role, which would make that account usable on the staff
/// portal. So: allow-list on role (fails closed), plus the IsExternalUser flag as defence in depth.
/// </para>
///
/// <para>Pure unit -- no DB, no ABP DI.</para>
/// </summary>
public class PasswordResetGateHostEligibilityTests
{
    [Theory]
    [InlineData("admin")]
    [InlineData("IT Admin")]
    [InlineData("Staff Supervisor")]
    [InlineData("Intake Staff")]
    public void IsHostAccountEligible_AllowsEachInternalRole(string role)
    {
        PasswordResetGate.IsHostAccountEligible(new[] { role }, isExternalFlag: false)
            .ShouldBeTrue(role);
    }

    [Theory]
    [InlineData("it admin")]
    [InlineData("IT ADMIN")]
    [InlineData("  Staff Supervisor  ")]
    public void IsHostAccountEligible_MatchesCaseInsensitivelyAndTrims(string role)
    {
        // Role strings come from the identity store, so casing and stray whitespace are the store's
        // business, not a reason to refuse a genuine operator.
        PasswordResetGate.IsHostAccountEligible(new[] { role }, isExternalFlag: false)
            .ShouldBeTrue(role);
    }

    [Theory]
    [InlineData("Patient")]
    [InlineData("Applicant Attorney")]
    [InlineData("Defense Attorney")]
    [InlineData("Claim Examiner")]
    [InlineData("Some Unknown Role")]
    public void IsHostAccountEligible_RefusesEveryNonInternalRole(string role)
    {
        // This is the privilege guard: a hand-made host row with an external role must not become
        // usable on the staff portal.
        PasswordResetGate.IsHostAccountEligible(new[] { role }, isExternalFlag: false)
            .ShouldBeFalse(role);
    }

    [Fact]
    public void IsHostAccountEligible_RefusesWhenTheExternalFlagIsSet_EvenWithAnInternalRole()
    {
        // Defence in depth: the flag alone is decisive, so a row that somehow holds both an internal
        // role and the external marker is refused rather than resolved in its favour.
        PasswordResetGate.IsHostAccountEligible(new[] { "IT Admin" }, isExternalFlag: true)
            .ShouldBeFalse();
        PasswordResetGate.IsHostAccountEligible(new[] { "admin", "Staff Supervisor" }, isExternalFlag: true)
            .ShouldBeFalse();
    }

    [Fact]
    public void IsHostAccountEligible_FailsClosedOnAnEmptyOrNullRoleSet()
    {
        PasswordResetGate.IsHostAccountEligible(null, isExternalFlag: false).ShouldBeFalse();
        PasswordResetGate.IsHostAccountEligible(new string?[0], isExternalFlag: false).ShouldBeFalse();
        PasswordResetGate.IsHostAccountEligible(new string?[] { null }, isExternalFlag: false).ShouldBeFalse();
        PasswordResetGate.IsHostAccountEligible(new string?[] { "" }, isExternalFlag: false).ShouldBeFalse();
        PasswordResetGate.IsHostAccountEligible(new string?[] { "   " }, isExternalFlag: false).ShouldBeFalse();
    }

    [Fact]
    public void IsHostAccountEligible_AllowsWhenAnInternalRoleSitsAlongsideOthers()
    {
        // A real operator can hold several roles; one internal role is enough.
        PasswordResetGate
            .IsHostAccountEligible(new[] { "Patient", "Intake Staff" }, isExternalFlag: false)
            .ShouldBeTrue();
    }
}
