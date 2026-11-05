using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Identity;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

/// <summary>Pure rule for office decisions 3, 4 and 5: who may manage which Authorized Users.</summary>
public class AccessorManagementRulesTests
{
    private const string AppAttorney = "applicant.lawyer@example.test";
    private const string DefAttorney = "defense.lawyer@example.test";
    private const string Examiner = "examiner@example.test";

    private static readonly AccessorCase TheCase = new(AppAttorney, DefAttorney, Examiner);

    private static AccessorCaller Caller(
        string? email = null, bool booker = false, bool authorizedUser = false, bool internalUser = false, params string[] roles) =>
        new(internalUser, roles, email, booker, authorizedUser, IsExternalAccessorManager: roles.Any(r => r != "Patient" && r != "Claim Examiner"));

    private static AccessorTarget Target(params string[] roles) => AccessorTarget.Of(roles);

    [Fact]
    public void Sides_of_a_firm_account_holding_both_sides_lists_both()
    {
        AccessorManagementRules.SidesOf(new[] { ExternalRoleConsts.DefenseParalegal, ExternalRoleConsts.ApplicantAttorney })
            .ShouldBe(new[] { ChangeRequestSide.SideA, ChangeRequestSide.SideB }, ignoreOrder: true);
        AccessorManagementRules.SidesOf(new[] { "Patient", "Claim Examiner" }).ShouldBeEmpty();
    }

    [Fact]
    public void Staff_may_manage_anyone()
    {
        AccessorManagementRules.Decide(Caller(internalUser: true, roles: "Intake Staff"), TheCase,
            Target(ExternalRoleConsts.DefenseParalegal), false).ShouldBe(AccessorManageDecision.Allowed);
    }

    [Fact]
    public void The_named_attorney_manages_only_the_own_side()
    {
        var caller = Caller(DefAttorney, roles: ExternalRoleConsts.DefenseAttorney);
        AccessorManagementRules.Decide(caller, TheCase, Target(ExternalRoleConsts.DefenseParalegal), false)
            .ShouldBe(AccessorManageDecision.Allowed);
        AccessorManagementRules.Decide(caller, TheCase, Target(ExternalRoleConsts.ApplicantParalegal), false)
            .ShouldBe(AccessorManageDecision.OtherSide);
    }

    [Fact]
    public void The_attorney_email_alone_without_the_attorney_role_is_not_named()
    {
        var caller = Caller(DefAttorney, roles: ExternalRoleConsts.DefenseParalegal);
        AccessorManagementRules.IsPartyOfSide(ChangeRequestSide.SideB, caller, TheCase).ShouldBeFalse();
    }

    [Fact]
    public void An_attorney_named_on_the_other_side_column_is_not_a_party_of_this_side()
    {
        var caller = Caller(AppAttorney, roles: ExternalRoleConsts.DefenseAttorney);
        AccessorManagementRules.IsPartyOfSide(ChangeRequestSide.SideB, caller, TheCase).ShouldBeFalse();
    }

    [Fact]
    public void A_firm_with_both_roles_is_a_party_only_of_the_side_it_is_named_on()
    {
        var caller = Caller(DefAttorney, roles: new[] { ExternalRoleConsts.ApplicantAttorney, ExternalRoleConsts.DefenseAttorney });
        AccessorManagementRules.IsPartyOfSide(ChangeRequestSide.SideB, caller, TheCase).ShouldBeTrue();
        AccessorManagementRules.IsPartyOfSide(ChangeRequestSide.SideA, caller, TheCase).ShouldBeFalse();
    }

    [Fact]
    public void A_booker_or_authorized_user_is_a_party_of_the_side_of_the_role_they_hold()
    {
        AccessorManagementRules.IsPartyOfSide(ChangeRequestSide.SideA,
            Caller(booker: true, roles: ExternalRoleConsts.ApplicantParalegal), TheCase).ShouldBeTrue();
        AccessorManagementRules.IsPartyOfSide(ChangeRequestSide.SideA,
            Caller(authorizedUser: true, roles: ExternalRoleConsts.ApplicantParalegal), TheCase).ShouldBeTrue();
        AccessorManagementRules.IsPartyOfSide(ChangeRequestSide.SideB,
            Caller(authorizedUser: true, roles: ExternalRoleConsts.ApplicantParalegal), TheCase).ShouldBeFalse();
    }

    [Fact]
    public void A_target_holding_both_sides_needs_a_party_of_both()
    {
        var caller = Caller(DefAttorney, roles: ExternalRoleConsts.DefenseAttorney);
        AccessorManagementRules.Decide(caller, TheCase,
            Target(ExternalRoleConsts.ApplicantParalegal, ExternalRoleConsts.DefenseParalegal), false)
            .ShouldBe(AccessorManageDecision.OtherSide);
    }

    [Fact]
    public void A_law_office_account_that_is_not_on_the_case_is_denied()
    {
        AccessorManagementRules.Decide(Caller("stranger@example.test", roles: ExternalRoleConsts.DefenseAttorney), TheCase,
            Target(ExternalRoleConsts.DefenseParalegal), false).ShouldBe(AccessorManageDecision.Denied);
    }

    [Fact]
    public void A_patient_manages_nothing()
    {
        var patient = Caller("patient@example.test", booker: true, authorizedUser: true, roles: "Patient");
        AccessorManagementRules.Decide(patient, TheCase, Target(ExternalRoleConsts.DefenseParalegal), true)
            .ShouldBe(AccessorManageDecision.OtherSide);
        AccessorManagementRules.Decide(patient, TheCase, Target("Claim Examiner"), true)
            .ShouldBe(AccessorManageDecision.Denied);
        AccessorManagementRules.Decide(patient, TheCase, Target("Patient"), true)
            .ShouldBe(AccessorManageDecision.Denied);
    }

    [Fact]
    public void A_claim_examiner_party_manages_claim_examiner_helpers_only()
    {
        var named = Caller(Examiner, roles: "Claim Examiner");
        AccessorManagementRules.Decide(named, TheCase, Target("Claim Examiner"), false).ShouldBe(AccessorManageDecision.Allowed);
        AccessorManagementRules.Decide(named, TheCase, Target(ExternalRoleConsts.ApplicantParalegal), false)
            .ShouldBe(AccessorManageDecision.OtherSide);
        AccessorManagementRules.Decide(named, TheCase, Target("Patient"), false).ShouldBe(AccessorManageDecision.Denied);

        AccessorManagementRules.Decide(Caller(authorizedUser: true, roles: "Claim Examiner"), TheCase, Target("Claim Examiner"), false)
            .ShouldBe(AccessorManageDecision.Allowed);
        AccessorManagementRules.Decide(Caller("other@example.test", roles: "Claim Examiner"), TheCase, Target("Claim Examiner"), false)
            .ShouldBe(AccessorManageDecision.Denied);
    }

    [Fact]
    public void A_law_office_booker_may_still_add_a_non_law_office_role_but_not_remove_one()
    {
        var booker = Caller(booker: true, roles: ExternalRoleConsts.ApplicantParalegal);
        AccessorManagementRules.Decide(booker, TheCase, Target("Claim Examiner"), true).ShouldBe(AccessorManageDecision.Allowed);
        AccessorManagementRules.Decide(booker, TheCase, Target("Claim Examiner"), false).ShouldBe(AccessorManageDecision.Denied);
    }
}
