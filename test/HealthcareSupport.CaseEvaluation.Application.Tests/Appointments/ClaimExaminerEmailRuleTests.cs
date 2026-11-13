using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-11-13 -- when the claim examiner email may be left blank: only a law-office booker (attorney or
/// paralegal, never staff) with a real other-side attorney email. Synthetic addresses only.
/// </summary>
public class ClaimExaminerEmailRuleTests
{
    private const string Real = "TEST-other@firm.test";

    [Theory]
    [InlineData("Applicant Attorney", ChangeRequestSide.SideA)]
    [InlineData("Applicant Paralegal", ChangeRequestSide.SideA)]
    [InlineData("Defense Attorney", ChangeRequestSide.SideB)]
    [InlineData("defense paralegal", ChangeRequestSide.SideB)]
    public void LawOfficeSideOf_ReadsTheSideFromTheRole(string role, ChangeRequestSide side) =>
        ClaimExaminerEmailRule.LawOfficeSideOf(new[] { role }).ShouldBe(side);

    [Theory]
    [InlineData("Patient")]
    [InlineData("Claim Examiner")]
    [InlineData("admin")]
    [InlineData("Staff Supervisor")]
    public void LawOfficeSideOf_IsNullForEveryOtherRole(string role) =>
        ClaimExaminerEmailRule.LawOfficeSideOf(new[] { role }).ShouldBeNull();

    [Fact]
    public void LawOfficeSideOf_IsNullForNoRolesBothSidesOrStaffHoldingALawOfficeRole()
    {
        ClaimExaminerEmailRule.LawOfficeSideOf(null).ShouldBeNull();
        ClaimExaminerEmailRule.LawOfficeSideOf(new[] { "Applicant Attorney", "Defense Attorney" }).ShouldBeNull();
        ClaimExaminerEmailRule.LawOfficeSideOf(new[] { "Applicant Attorney", "admin" }).ShouldBeNull();
    }

    [Theory]
    [InlineData("Applicant Attorney", null, Real, true)]
    [InlineData("Applicant Paralegal", "unused", Real, true)]
    [InlineData("Defense Attorney", Real, null, true)]
    [InlineData("Defense Paralegal", Real, "", true)]
    public void MayLeaveBlank_WhenTheOtherSideHasARealEmail(string role, string? applicant, string? defense, bool expected) =>
        ClaimExaminerEmailRule.MayLeaveBlank(new[] { role }, applicant, defense).ShouldBe(expected);

    [Theory]
    [InlineData("Applicant Attorney", Real, null)]
    [InlineData("Applicant Attorney", Real, "")]
    [InlineData("Applicant Attorney", Real, "   ")]
    [InlineData("Applicant Attorney", Real, "unknown@firm.test")]
    [InlineData("Applicant Attorney", Real, "not-an-email")]
    [InlineData("Defense Attorney", null, Real)]
    [InlineData("Defense Paralegal", "na@firm.test", Real)]
    [InlineData("Patient", Real, Real)]
    [InlineData("Claim Examiner", Real, Real)]
    [InlineData("admin", Real, Real)]
    public void MayLeaveBlank_IsFalseWhenTheOtherSideHasNoRealEmailOrTheCallerIsNotALawOffice(
        string role, string? applicant, string? defense) =>
        ClaimExaminerEmailRule.MayLeaveBlank(new[] { role }, applicant, defense).ShouldBeFalse();
}
