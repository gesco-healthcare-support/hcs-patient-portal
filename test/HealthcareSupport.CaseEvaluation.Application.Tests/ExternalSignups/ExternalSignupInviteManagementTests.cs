using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Invitations;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// The seam: the staff-side invitation lifecycle on <c>ExternalSignupAppService</c> -- resend,
/// revoke, the management list, and the active-invited-email lookup that drives the People hub's
/// portal-status chip.
///
/// <para>Two rules carry most of the weight. RESEND re-issues in place, so the previous token
/// dies the moment its hash is overwritten (one row per recipient, one live link). REVOKE
/// soft-deletes, which stops the token validating while the list DELIBERATELY disables the
/// soft-delete filter so the row stays visible as Revoked -- staff need to see that they revoked
/// it, not watch it vanish.</para>
///
/// <para>NOT pinned here: authorization (always-allow in the test module); the status derivation
/// itself, which <c>InvitationStatusResolverTests</c> owns -- the list is exercised only to pin
/// this service's filter wiring; and expiry, which needs a Domain-internal reissue and therefore
/// lives in <c>EfCoreExternalSignupInvitationExpiryTests</c>.</para>
/// </summary>
public abstract class ExternalSignupInviteManagementTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string InvitePathMarker = "/Account/Register?inviteToken=";

    private readonly IExternalSignupAppService _appService;
    private readonly InvitationManager _invitationManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    protected ExternalSignupInviteManagementTests()
    {
        _appService = GetRequiredService<IExternalSignupAppService>();
        _invitationManager = GetRequiredService<InvitationManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    // One SQLite connection for the whole run with no rollback between tests: every fixture is
    // keyed by its own token and every assertion filters to rows this test created.
    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    private static string ExtractInviteToken(string inviteUrl)
    {
        var index = inviteUrl.IndexOf(InvitePathMarker, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0);
        return WebUtility.UrlDecode(inviteUrl[(index + InvitePathMarker.Length)..]);
    }

    /// <summary>
    /// Resend is an in-place re-issue, not a second invitation. The old link must stop working
    /// the moment the new one is handed out, otherwise a forwarded or leaked URL stays live.
    /// </summary>
    [Fact]
    public async Task ResendInviteAsync_InvalidatesTheOldTokenAndIssuesAWorkingOne()
    {
        var token = NewToken();
        var email = $"rsd-{token}@test.local";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var (invitation, oldRawToken) = await _invitationManager.IssueAsync(
                tenantId: TenantsTestData.TenantARef,
                email: email,
                userType: ExternalUserType.Patient,
                invitedByUserId: IdentityUsersTestData.TenantAdmin1UserId);

            // DateTime is a value type, so this is a copy taken before ResendAsync mutates the
            // tracked entity in place.
            var expiryBeforeResend = invitation.ExpiresAt;

            var resent = await _appService.ResendInviteAsync(invitation.Id);
            resent.InviteUrl.ShouldNotBeNullOrWhiteSpace();

            var newRawToken = ExtractInviteToken(resent.InviteUrl!);
            newRawToken.ShouldNotBe(oldRawToken);

            var dead = await Should.ThrowAsync<BusinessException>(
                () => _appService.ValidateInviteAsync(oldRawToken));
            dead.Code.ShouldBe(CaseEvaluationDomainErrorCodes.InviteInvalid);

            var live = await _appService.ValidateInviteAsync(newRawToken);
            live.Email.ShouldBe(email);

            resent.ExpiresAt.ShouldBeGreaterThanOrEqualTo(expiryBeforeResend);
        }
    }

    /// <summary>
    /// An accepted invitation is spent. Re-issuing one would mint a live registration link for an
    /// address that already has an account, which registration then rejects as a duplicate.
    /// </summary>
    [Fact]
    public async Task ResendInviteAsync_AcceptedInvitation_IsRefused()
    {
        var token = NewToken();
        var email = $"rsa-{token}@test.local";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var (invitation, rawToken) = await _invitationManager.IssueAsync(
                tenantId: TenantsTestData.TenantARef,
                email: email,
                userType: ExternalUserType.Patient,
                invitedByUserId: IdentityUsersTestData.TenantAdmin1UserId);

            // The ACCEPTED row is present; the guard is being tested against a real accepted
            // invitation, not an absent one. AcceptedByUserId carries no DB foreign key.
            await _invitationManager.AcceptAsync(rawToken, Guid.NewGuid());

            var ex = await Should.ThrowAsync<UserFriendlyException>(
                () => _appService.ResendInviteAsync(invitation.Id));

            ex.Message.ShouldContain("already been accepted");
        }
    }

    /// <summary>
    /// Revoke does two things at once and this pins both: the token stops validating (the
    /// soft-delete filter hides the row from the hash lookup) and the row still appears in the
    /// staff list as Revoked, because the list turns that same filter off.
    /// </summary>
    [Fact]
    public async Task RevokeInviteAsync_StopsTheTokenButKeepsTheRowVisibleAsRevoked()
    {
        var token = NewToken();
        var email = $"rvk-{token}@test.local";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var (invitation, rawToken) = await _invitationManager.IssueAsync(
                tenantId: TenantsTestData.TenantARef,
                email: email,
                userType: ExternalUserType.Patient,
                invitedByUserId: IdentityUsersTestData.TenantAdmin1UserId);

            // Live before revoke, so neither assertion below can pass against a missing row.
            (await _appService.ValidateInviteAsync(rawToken)).Email.ShouldBe(email);

            await _appService.RevokeInviteAsync(invitation.Id);

            var dead = await Should.ThrowAsync<BusinessException>(
                () => _appService.ValidateInviteAsync(rawToken));
            dead.Code.ShouldBe(CaseEvaluationDomainErrorCodes.InviteInvalid);

            var page = await _appService.GetInvitesAsync(new GetInvitesInput { Filter = email });
            var row = page.Items.SingleOrDefault(i => i.Id == invitation.Id);
            row.ShouldNotBeNull();
            row!.Status.ShouldBe(InvitationStatus.Revoked);
        }
    }

    /// <summary>
    /// Symmetric with resend: an accepted invitation cannot be revoked either. Its token is
    /// already spent, and soft-deleting the row would erase the record that it was used.
    /// </summary>
    [Fact]
    public async Task RevokeInviteAsync_AcceptedInvitation_IsRefused()
    {
        var token = NewToken();
        var email = $"rva-{token}@test.local";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var (invitation, rawToken) = await _invitationManager.IssueAsync(
                tenantId: TenantsTestData.TenantARef,
                email: email,
                userType: ExternalUserType.Patient,
                invitedByUserId: IdentityUsersTestData.TenantAdmin1UserId);

            await _invitationManager.AcceptAsync(rawToken, Guid.NewGuid());

            var ex = await Should.ThrowAsync<UserFriendlyException>(
                () => _appService.RevokeInviteAsync(invitation.Id));

            ex.Message.ShouldContain("already been accepted");

            // Still listed as Accepted -- the refusal did not half-apply.
            var page = await _appService.GetInvitesAsync(new GetInvitesInput { Filter = email });
            var row = page.Items.SingleOrDefault(i => i.Id == invitation.Id);
            row.ShouldNotBeNull();
            row!.Status.ShouldBe(InvitationStatus.Accepted);
        }
    }

    /// <summary>
    /// "Invited by" has to name someone. The seeded staff accounts carry no Name or Surname, which
    /// is the normal shape for an account created without a profile, so the display name falls
    /// back to the username rather than rendering a blank cell.
    /// </summary>
    [Fact]
    public async Task GetInvitesAsync_ResolvesInviterDisplayNameFromUserNameWhenNamesBlank()
    {
        var token = NewToken();
        var email = $"inb-{token}@test.local";

        // Tenant outside, user inside.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(
                   _principal,
                   IdentityUsersTestData.TenantAdmin1UserId,
                   IdentityUsersTestData.TenantAdminRoleName))
        {
            await _appService.InviteExternalUserAsync(new InviteExternalUserDto
            {
                Email = email,
                UserType = ExternalUserType.Patient,
            });

            var page = await _appService.GetInvitesAsync(new GetInvitesInput { Filter = email });
            var row = page.Items.Single();

            row.InvitedByUserId.ShouldBe(IdentityUsersTestData.TenantAdmin1UserId);
            row.InvitedByName.ShouldBe(IdentityUsersTestData.TenantAdmin1UserName);
        }
    }

    /// <summary>
    /// #21: the register overlay pre-fills the firm from the invitation, so the validation
    /// response has to carry it. Without this the invited attorney is asked to retype a firm name
    /// the inviter already supplied.
    /// </summary>
    [Fact]
    public async Task ValidateInviteAsync_SurfacesFirmNameForAnAttorneyInvite()
    {
        var token = NewToken();
        var email = $"frm-{token}@test.local";
        var firmName = $"TEST-Firm-{token}";

        var result = await _appService.InviteExternalUserAsync(new InviteExternalUserDto
        {
            Email = email,
            UserType = ExternalUserType.ApplicantAttorney,
            FirstName = "TEST-Invited",
            LastName = "TEST-Attorney",
            FirmName = firmName,
            TenantId = TenantsTestData.TenantARef,
        });

        result.InviteUrl.ShouldNotBeNullOrWhiteSpace();
        var rawToken = ExtractInviteToken(result.InviteUrl!);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var validated = await _appService.ValidateInviteAsync(rawToken);
            validated.FirmName.ShouldBe(firmName);
            validated.FirstName.ShouldBe("TEST-Invited");
            validated.LastName.ShouldBe("TEST-Attorney");
            validated.RoleName.ShouldBe(IdentityUsersTestData.ApplicantAttorneyRoleName);
        }
    }

    /// <summary>
    /// The People hub sends whatever casing its page is showing and renders the chip against a
    /// lowercased key, so the lookup normalises on the way in AND on the way out. The invitation
    /// is stored in MIXED case on purpose: with a lowercase row both halves would pass with the
    /// normalisation deleted.
    /// </summary>
    [Fact]
    public async Task GetActiveInvitedEmailsAsync_MatchesCaseInsensitivelyAndReturnsLowercase()
    {
        var token = NewToken();
        var storedMixedCase = $"Case-{token}@Test.Local";
        var expectedLower = storedMixedCase.ToLowerInvariant();

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            // Issued through the manager rather than the AppService, which lowercases first.
            await _invitationManager.IssueAsync(
                tenantId: TenantsTestData.TenantARef,
                email: storedMixedCase,
                userType: ExternalUserType.Patient,
                invitedByUserId: IdentityUsersTestData.TenantAdmin1UserId);

            var matched = await _appService.GetActiveInvitedEmailsAsync(
                new List<string> { storedMixedCase.ToUpperInvariant() });

            matched.ShouldContain(expectedLower);
            matched.ShouldNotContain(storedMixedCase);
        }
    }

    /// <summary>
    /// "Active" excludes an invitation that has already been used. Both rows exist here -- one
    /// accepted, one pending -- so the pending one proves the query returns anything at all while
    /// the accepted one proves the AcceptedAt clause is doing work.
    /// </summary>
    [Fact]
    public async Task GetActiveInvitedEmailsAsync_AcceptedInviteIsNotActive()
    {
        var token = NewToken();
        var acceptedEmail = $"gaa-{token}@test.local";
        var pendingEmail = $"gap-{token}@test.local";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var (_, acceptedRawToken) = await _invitationManager.IssueAsync(
                tenantId: TenantsTestData.TenantARef,
                email: acceptedEmail,
                userType: ExternalUserType.Patient,
                invitedByUserId: IdentityUsersTestData.TenantAdmin1UserId);
            await _invitationManager.AcceptAsync(acceptedRawToken, Guid.NewGuid());

            await _invitationManager.IssueAsync(
                tenantId: TenantsTestData.TenantARef,
                email: pendingEmail,
                userType: ExternalUserType.Patient,
                invitedByUserId: IdentityUsersTestData.TenantAdmin1UserId);

            var matched = await _appService.GetActiveInvitedEmailsAsync(
                new List<string> { acceptedEmail, pendingEmail });

            matched.ShouldContain(pendingEmail);
            matched.ShouldNotContain(acceptedEmail);
        }
    }
}
