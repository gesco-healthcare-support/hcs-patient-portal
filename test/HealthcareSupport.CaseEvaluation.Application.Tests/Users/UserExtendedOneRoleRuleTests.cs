using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Users;

/// <summary>
/// The one-role-per-person rule (2026-10-26) as <see cref="UserExtendedAppService.EnsureExactlyOneRole"/>
/// decides it, without a database: exactly one distinct, non-blank role name passes; none or two refuse.
/// </summary>
public class UserExtendedOneRoleRuleTests
{
    [Theory]
    [InlineData("Intake Staff")]
    [InlineData("Intake Staff", "Intake Staff")]
    [InlineData("Intake Staff", "intake staff")]
    [InlineData("Intake Staff", " ")]
    public void Exactly_one_distinct_role_passes(params string[] roles)
    {
        Should.NotThrow(() => UserExtendedAppService.EnsureExactlyOneRole(roles));
    }

    [Theory]
    [InlineData]
    [InlineData(" ")]
    [InlineData("Intake Staff", "Staff Supervisor")]
    [InlineData("Patient", "Applicant Attorney", "Defense Attorney")]
    public void None_or_more_than_one_is_refused(params string[] roles)
    {
        Should.Throw<BusinessException>(() => UserExtendedAppService.EnsureExactlyOneRole(roles))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.OneRolePerPerson);
    }

    [Fact]
    public void No_list_at_all_is_refused_too()
    {
        Should.Throw<BusinessException>(() => UserExtendedAppService.EnsureExactlyOneRole(null))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.OneRolePerPerson);
    }
}
