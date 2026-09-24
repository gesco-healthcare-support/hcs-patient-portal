using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications.Delivery;

/// <summary>
/// <see cref="RecipientRoleResolver"/> against real seeded users and roles in office A. It decides
/// whether an email is a registered user holding the role the notice is for -- which picks the
/// "log in" versus "register" wording. The wrong-role and unmapped-role cases use a REAL user, so a
/// "not registered" verdict there cannot come from the lookup simply finding nobody. Office B's
/// seeded Patient2 is the office decoy: a real user in the right role, in the wrong office.
/// </summary>
public class RecipientRoleResolverTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankEmail_IsNotRegistered(string email)
    {
        var result = await ClassifyAsync(email, RecipientRole.ApplicantAttorney);

        result.ShouldBe(new RecipientRoleClassification(IsRegistered: false, MatchesRole: false, UserId: null));
    }

    [Fact]
    public async Task OfficeAdmin_IsAlwaysRegistered_WithoutAUserLookup()
    {
        // Not a registered address at all: the office mailbox is a setting, not an account.
        var result = await ClassifyAsync("TEST-office-mailbox@test.local", RecipientRole.OfficeAdmin);

        result.ShouldBe(new RecipientRoleClassification(IsRegistered: true, MatchesRole: true, UserId: null));
    }

    [Fact]
    public async Task UnknownEmail_IsNotRegistered()
    {
        var result = await ClassifyAsync("TEST-nobody@test.local", RecipientRole.ApplicantAttorney);

        result.ShouldBe(new RecipientRoleClassification(IsRegistered: false, MatchesRole: false, UserId: null));
    }

    [Fact]
    public async Task UserHoldingTheRole_IsRegisteredAndMatches()
    {
        var result = await ClassifyAsync(IdentityUsersTestData.ApplicantAttorney1Email, RecipientRole.ApplicantAttorney);

        result.ShouldBe(new RecipientRoleClassification(
            IsRegistered: true, MatchesRole: true, UserId: IdentityUsersTestData.ApplicantAttorney1UserId));
    }

    [Fact]
    public async Task EmailIsTrimmed_BeforeTheLookup()
    {
        var result = await ClassifyAsync($"  {IdentityUsersTestData.ApplicantAttorney1Email}  ", RecipientRole.ApplicantAttorney);

        result.UserId.ShouldBe(IdentityUsersTestData.ApplicantAttorney1UserId);
        result.MatchesRole.ShouldBeTrue();
    }

    [Fact]
    public async Task RealUserInADifferentRole_IsRoutedAsNotRegistered_ButKeepsTheirId()
    {
        // The applicant attorney is NOT a defense attorney.
        var result = await ClassifyAsync(IdentityUsersTestData.ApplicantAttorney1Email, RecipientRole.DefenseAttorney);

        result.ShouldBe(new RecipientRoleClassification(
            IsRegistered: false, MatchesRole: false, UserId: IdentityUsersTestData.ApplicantAttorney1UserId));
    }

    [Fact]
    public async Task AnotherOfficesUserInTheRole_IsUnknownFromThisOffice()
    {
        // The OFFICE decoy. Patient2 holds the Patient role, but in office B only. Classified from
        // office A they must read as unknown, with no id, or office A's notice would be worded (and
        // attributed) as if it were addressed to office B's account.
        var result = await ClassifyAsync(IdentityUsersTestData.Patient2Email, RecipientRole.Patient);

        result.ShouldBe(new RecipientRoleClassification(IsRegistered: false, MatchesRole: false, UserId: null));
    }

    [Fact]
    public async Task TheSameUser_IsRegisteredFromTheirOwnOffice()
    {
        // Positive control for the Fact above: same user, same role, their own office.
        var result = await ClassifyAsync(IdentityUsersTestData.Patient2Email, RecipientRole.Patient, TenantsTestData.TenantBRef);

        result.ShouldBe(new RecipientRoleClassification(
            IsRegistered: true, MatchesRole: true, UserId: IdentityUsersTestData.Patient2UserId));
    }

    [Theory]
    [InlineData(RecipientRole.InsuranceCarrierContact)]
    [InlineData(RecipientRole.Employer)]
    public async Task RealUserForARoleWithNoMapping_IsOffRole(RecipientRole role)
    {
        var result = await ClassifyAsync(IdentityUsersTestData.ApplicantAttorney1Email, role);

        result.ShouldBe(new RecipientRoleClassification(
            IsRegistered: false, MatchesRole: false, UserId: IdentityUsersTestData.ApplicantAttorney1UserId));
    }

    [Theory]
    [InlineData(RecipientRole.Patient, "Patient")]
    [InlineData(RecipientRole.ApplicantAttorney, "Applicant Attorney")]
    [InlineData(RecipientRole.DefenseAttorney, "Defense Attorney")]
    [InlineData(RecipientRole.ClaimExaminer, "Claim Examiner")]
    public void MapsEachPartyRoleToItsIdentityRoleName(RecipientRole role, string expected)
    {
        RecipientRoleResolver.MapToRoleName(role).ShouldBe(expected);
    }

    // ------------------------------------------------------------------------

    private Task<RecipientRoleClassification> ClassifyAsync(string email, RecipientRole role, Guid? officeId = null) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(officeId ?? TenantsTestData.TenantARef))
            {
                return await GetRequiredService<IRecipientRoleResolver>().ClassifyAsync(email, role);
            }
        });
}
