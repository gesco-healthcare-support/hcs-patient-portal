using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Invitations;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// The seam: the EXPIRY half of the invitation rules as they surface through
/// <c>ExternalSignupAppService</c> -- a lapsed invite is not "active" for the People hub chip, and
/// its token no longer validates for the register overlay.
///
/// <para>This file is concrete and lives in EntityFrameworkCore.Tests rather than being a
/// Shape-B abstract body in Application.Tests, for one reason: planting a PAST expiry needs
/// <c>Invitation.Reissue</c> and <c>InvitationManager.ComputeTokenHash</c>, which are Domain
/// internals. The Domain assembly grants InternalsVisibleTo to Domain.Tests, TestBase and
/// EntityFrameworkCore.Tests -- NOT to Application.Tests. Re-implementing SHA256 hex in the test
/// would assert the test's own crypto instead of the manager's, and substituting the ambient
/// <c>IClock</c> would change it for every test class sharing this module.</para>
///
/// <para>NOT pinned here: authorization (always-allow in the test module), and the ordering of
/// the accepted-before-expired checks inside <c>InvitationManager.ValidateAsync</c>, which is that
/// class's own concern.</para>
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreExternalSignupInvitationExpiryTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private readonly IExternalSignupAppService _appService;
    private readonly InvitationManager _invitationManager;
    private readonly IInvitationRepository _invitationRepository;
    private readonly ICurrentTenant _currentTenant;

    public EfCoreExternalSignupInvitationExpiryTests()
    {
        _appService = GetRequiredService<IExternalSignupAppService>();
        _invitationManager = GetRequiredService<InvitationManager>();
        _invitationRepository = GetRequiredService<IInvitationRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    // The rig shares one SQLite connection for the whole run and never rolls back, so each test
    // keys its fixture with its own token and filters to rows it created.
    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Issues a real invitation and then re-issues it in place with a KNOWN raw token and an
    /// expiry a day in the past. Caller must already be inside the TenantA scope.
    /// </summary>
    private async Task IssueAlreadyExpiredInviteAsync(string email, string rawToken)
    {
        var (invitation, _) = await _invitationManager.IssueAsync(
            tenantId: TenantsTestData.TenantARef,
            email: email,
            userType: ExternalUserType.Patient,
            invitedByUserId: IdentityUsersTestData.TenantAdmin1UserId);

        // A whole day of margin so the test cannot depend on the ambient IClock's DateTimeKind.
        invitation.Reissue(
            InvitationManager.ComputeTokenHash(rawToken),
            DateTime.UtcNow.AddDays(-1));

        await _invitationRepository.UpdateAsync(invitation, autoSave: true);
    }

    /// <summary>
    /// "Active" means not accepted AND not expired. The pending invitation alongside it proves the
    /// query returns rows at all, so the expired one's absence is the ExpiresAt clause and not an
    /// empty result set.
    /// </summary>
    [Fact]
    public async Task GetActiveInvitedEmailsAsync_ExpiredInviteIsNotActive()
    {
        var token = NewToken();
        var expiredEmail = $"xpa-{token}@test.local";
        var pendingEmail = $"xpp-{token}@test.local";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await IssueAlreadyExpiredInviteAsync(expiredEmail, InvitationManager.GenerateRawToken());

            await _invitationManager.IssueAsync(
                tenantId: TenantsTestData.TenantARef,
                email: pendingEmail,
                userType: ExternalUserType.Patient,
                invitedByUserId: IdentityUsersTestData.TenantAdmin1UserId);

            var matched = await _appService.GetActiveInvitedEmailsAsync(
                new List<string> { expiredEmail, pendingEmail });

            matched.ShouldContain(pendingEmail);
            matched.ShouldNotContain(expiredEmail);
        }
    }

    /// <summary>
    /// A lapsed link must say so. The overlay renders a different banner for Expired than for
    /// Invalid, so the recipient is told to ask for a new invite rather than that their link was
    /// never real.
    /// </summary>
    [Fact]
    public async Task ValidateInviteAsync_ExpiredToken_ThrowsInviteExpired()
    {
        var token = NewToken();
        var email = $"xpv-{token}@test.local";
        var rawToken = InvitationManager.GenerateRawToken();

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await IssueAlreadyExpiredInviteAsync(email, rawToken);

            var ex = await Should.ThrowAsync<BusinessException>(
                () => _appService.ValidateInviteAsync(rawToken));

            // Expired, not Invalid: the row was found by hash, it had simply lapsed.
            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.InviteExpired);
        }
    }
}
