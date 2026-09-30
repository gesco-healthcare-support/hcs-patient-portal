using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Identity;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

/// <summary>
/// The two pieces of <see cref="InternalUsersAppService"/> that are pure and therefore
/// testable without the ABP harness: the temporary-password generator
/// (<c>GenerateParityPassword</c>) and the creatable-role allow-list
/// (<c>CreatableRoleNames</c>).
///
/// <para><b>WHY THIS FILE LIVES IN Application.Tests RATHER THAN EntityFrameworkCore.Tests.</b>
/// <c>GenerateParityPassword</c> is <c>internal static</c>, and the Application assembly's
/// <c>InternalsVisibleTo</c> (src/HealthcareSupport.CaseEvaluation.Application/AssemblyInfo.cs)
/// names Application.Tests ONLY. From EntityFrameworkCore.Tests the symbol does not resolve, so
/// the generator can be reached there only indirectly, through the rendered welcome email. The
/// integration file does exactly that for the end-to-end guarantee; the format contract itself
/// is pinned here, where the method can be called directly.</para>
///
/// <para><b>THESE FACTS ARE PROBABILISTIC BY CONSTRUCTION, AND THE ITERATION COUNTS ARE
/// LOAD-BEARING.</b> The generator draws from <c>RandomNumberGenerator</c>, so a single draw
/// cannot distinguish "the contract holds" from "this draw happened to satisfy it". The counts
/// below are chosen so that the weakest mutation each Fact is meant to catch fails with
/// probability greater than 1 - 1e-9. The worked case is the complexity Fact: swapping
/// <c>Pick(UpperChars)</c> for <c>Pick(MixedChars)</c> at block1[0] leaves six mixed draws that
/// could each miss uppercase with probability 32/57, so a single draw lacks an uppercase only
/// about 3.1% of the time -- 2000 draws make a surviving mutant effectively impossible
/// (0.969^2000 is on the order of 1e-28).</para>
///
/// <para><b>WHAT THESE DO NOT PIN.</b> Nothing about the distribution's quality beyond
/// "not a constant" (Fact 4): uniformity of <c>RandomNumberGenerator.GetInt32</c> is the
/// platform's guarantee, not this service's. Nothing about where the password goes -- the
/// "email only, never the response DTO" guarantee is an integration concern and is pinned in
/// EfCoreInternalUsersAppServiceTests. Nothing about ABP's password validator itself; these
/// assert the four properties ABP's defaults require, not that ABP checks them.</para>
/// </summary>
public class InternalUserPasswordFormatUnitTests
{
    /// <summary>Draws for the shape / distinctness Facts (cheap assertions).</summary>
    private const int ShapeDraws = 200;

    /// <summary>
    /// Draws for the two Facts that must catch a mutation which only SOMETIMES violates the
    /// contract. See the class remarks for the arithmetic behind this number.
    /// </summary>
    private const int ContractDraws = 2000;

    /// <summary>Glyph pairs the curated alphabets deliberately drop, per the source comments.</summary>
    private static readonly char[] AmbiguousGlyphs = { '0', '1', 'O', 'I', 'l' };

    [Fact]
    public void GenerateParityPassword_KeepsTheOldParityShape()
    {
        // OLD parity is {4 chars}@{4 chars} -- 9 characters with a literal '@' at index 4. The
        // '@' is not decoration: it is what satisfies ABP's RequireNonAlphanumeric without
        // rejection sampling, which is why the width of each block matters as much as the total.
        for (var draw = 0; draw < ShapeDraws; draw++)
        {
            var password = InternalUsersAppService.GenerateParityPassword();

            password.Length.ShouldBe(
                9,
                $"Draw {draw} was '{password}'. The OLD-parity shape is exactly 9 characters.");
            password[4].ShouldBe(
                '@',
                $"Draw {draw} was '{password}'. The separator must sit at index 4.");

            for (var i = 0; i < password.Length; i++)
            {
                if (i == 4)
                {
                    continue;
                }
                char.IsLetterOrDigit(password[i]).ShouldBeTrue(
                    $"Draw {draw} was '{password}'. Index {i} is '{password[i]}', which is not "
                    + "alphanumeric. Only index 4 may be a symbol.");
            }
        }
    }

    [Fact]
    public void GenerateParityPassword_SatisfiesAbpComplexityOnEveryOutcome()
    {
        // The generator's docstring claims complexity is met on EVERY random outcome, so no
        // rejection sampling is needed. That claim is the thing under test: it is true only
        // because block1 positions 0 and 1 draw from the upper-only and lower-only sets and
        // block2 position 0 draws from the digit-only set. Widen any of those three to
        // MixedChars and the claim silently becomes "usually".
        for (var draw = 0; draw < ContractDraws; draw++)
        {
            var password = InternalUsersAppService.GenerateParityPassword();

            password.Any(char.IsUpper).ShouldBeTrue(
                $"Draw {draw} was '{password}' and carries no uppercase letter. ABP's default "
                + "IdentityOptions.Password.RequireUppercase would reject it at CreateAsync.");
            password.Any(char.IsLower).ShouldBeTrue(
                $"Draw {draw} was '{password}' and carries no lowercase letter.");
            password.Any(char.IsDigit).ShouldBeTrue(
                $"Draw {draw} was '{password}' and carries no digit.");
            password.Any(c => !char.IsLetterOrDigit(c)).ShouldBeTrue(
                $"Draw {draw} was '{password}' and carries no non-alphanumeric character.");
        }
    }

    [Fact]
    public void GenerateParityPassword_ExcludesVisuallyAmbiguousGlyphs()
    {
        // The user retypes this password out of an email, so 0/O and 1/I/l are dropped from the
        // source alphabets. This is a NEGATIVE guarantee about characters the code removes, and
        // the only fixture that can prove it is volume: re-adding 'l' to LowerChars puts it in
        // roughly one draw in seven, so ContractDraws makes the mutant certain to be caught.
        for (var draw = 0; draw < ContractDraws; draw++)
        {
            var password = InternalUsersAppService.GenerateParityPassword();

            password.IndexOfAny(AmbiguousGlyphs).ShouldBe(
                -1,
                $"Draw {draw} was '{password}'. One of 0 1 O I l reached the alphabet; a user "
                + "retyping this from an email cannot tell them apart.");
        }
    }

    [Fact]
    public void GenerateParityPassword_IsNotAConstant()
    {
        // Facts 1-3 are all satisfied by a hardcoded literal such as "Abcd@2345". This is the one
        // that is not: it is the only assertion in the file that fails if the RandomNumberGenerator
        // draws are replaced by a fixed string.
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        for (var draw = 0; draw < ShapeDraws; draw++)
        {
            distinct.Add(InternalUsersAppService.GenerateParityPassword());
        }

        distinct.Count.ShouldBeGreaterThan(
            ShapeDraws / 2,
            $"{ShapeDraws} draws produced only {distinct.Count} distinct values. The keyspace is "
            + "roughly 25*24*57*57 * 8*57*57*57, so anything near-constant means the randomness "
            + "was removed.");
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
