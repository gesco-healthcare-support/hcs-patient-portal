using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.ExternalAccount;
using HealthcareSupport.CaseEvaluation.Identity;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

/// <summary>
/// The two pure pieces behind staff-account creation, testable without the ABP harness: the
/// password an account is created with before its owner chooses one (<see cref="UnusablePassword"/>)
/// and <see cref="InternalUsersAppService"/>'s creatable-role allow-list (<c>CreatableRoleNames</c>).
///
/// <para>2026-10-23: staff accounts no longer receive a temporary password. They are created with a
/// password nobody sees and receive an activation link instead, so the old "{4}@{4}, no ambiguous
/// glyphs" generator and its four facts are gone. What still matters about the replacement is that
/// Identity accepts it on every draw (or account creation fails) and that it is not a constant (or
/// every activated-later account would share a known password until its owner chose one).</para>
///
/// <para>The draws are probabilistic by construction; 2000 makes a generator that only SOMETIMES
/// misses a character class fail with near certainty. The fixed "Aa1!" prefix is what makes the
/// policy hold on every draw, which is exactly what the first fact pins.</para>
/// </summary>
public class InternalUserPasswordFormatUnitTests
{
    private const int Draws = 2000;

    [Fact]
    public void The_unusable_password_satisfies_the_default_policy_on_every_draw()
    {
        for (var draw = 0; draw < Draws; draw++)
        {
            var password = UnusablePassword.Generate();

            password.Length.ShouldBeInRange(6, 128, $"Draw {draw} was {password.Length} characters long.");
            password.Any(char.IsUpper).ShouldBeTrue($"Draw {draw} carries no uppercase letter.");
            password.Any(char.IsLower).ShouldBeTrue($"Draw {draw} carries no lowercase letter.");
            password.Any(char.IsDigit).ShouldBeTrue($"Draw {draw} carries no digit.");
            password.Any(c => !char.IsLetterOrDigit(c)).ShouldBeTrue($"Draw {draw} carries no symbol.");
        }
    }

    [Fact]
    public void The_unusable_password_is_not_a_constant()
    {
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        for (var draw = 0; draw < 200; draw++)
        {
            distinct.Add(UnusablePassword.Generate());
        }

        distinct.Count.ShouldBe(200, "32 random bytes should never repeat in 200 draws.");
    }

    [Fact]
    public void CreatableRoleNames_HoldsOnlyTheTwoRolesItAdminMayCreate()
    {
        // The allow-list is the whole of step 1 in CreateAsync. Assert the EXCLUSIONS first,
        // because those are the security content: "IT Admin" is a real seeded host role
        // (InternalUserRoleDataSeedContributor.ItAdminRoleName), so leaving it off the list is a
        // deliberate refusal rather than a name that happens not to resolve. The four external
        // roles go through InviteExternalUserAsync, which applies the invite + registration flow
        // this surface does not.
        InternalUsersAppService.CreatableRoleNames.ShouldNotContain(
            InternalUserRoleDataSeedContributor.ItAdminRoleName,
            "IT Admin accounts are seeded, never self-created. Adding the name here would let an "
            + "IT Admin mint another IT Admin through the staff-create form.");

        foreach (var externalRole in new[]
                 {
                     "Patient",
                     "Applicant Attorney",
                     "Defense Attorney",
                     "Claim Examiner",
                 })
        {
            InternalUsersAppService.CreatableRoleNames.ShouldNotContain(
                externalRole,
                $"'{externalRole}' is an external role and must be created through the invite "
                + "flow, not the internal-staff form.");
        }

        // ToArray() on the left so both sides are string[] -- the exact shape every other
        // collection assertion in this suite uses, and the one Shouldly's structural comparer
        // is unambiguously selected for.
        InternalUsersAppService.CreatableRoleNames.ToArray().ShouldBe(
            new[]
            {
                InternalUserRoleDataSeedContributor.IntakeStaffRoleName,
                InternalUserRoleDataSeedContributor.StaffSupervisorRoleName,
            },
            "The allow-list is compared with StringComparer.Ordinal in CreateAsync, so both the "
            + "membership and the exact spelling are load-bearing.");
    }

    [Fact]
    public void CreatableRoleNames_MatchesTheRoleNamesTheSeedContributorActuallyCreates()
    {
        // CreateAsync looks each allow-listed name up with IdentityRoleManager.FindByNameAsync and
        // throws InternalUserRoleMissing when it resolves to nothing. That lookup is against the
        // roles InternalUserRoleDataSeedContributor seeds, and the two files spell the names
        // independently (the service keeps literals; the Domain layer keeps constants). A typo in
        // either one turns every create into a 400 that only a live stack would reveal.
        var seededInternalRoles = new List<string>
        {
            InternalUserRoleDataSeedContributor.ItAdminRoleName,
            InternalUserRoleDataSeedContributor.StaffSupervisorRoleName,
            InternalUserRoleDataSeedContributor.IntakeStaffRoleName,
        };

        foreach (var creatable in InternalUsersAppService.CreatableRoleNames)
        {
            seededInternalRoles.ShouldContain(
                creatable,
                $"CreateAsync accepts '{creatable}' but no seeded role carries that exact name, so "
                + "every create for it would fail with InternalUser.RoleMissing.");
        }
    }
}
