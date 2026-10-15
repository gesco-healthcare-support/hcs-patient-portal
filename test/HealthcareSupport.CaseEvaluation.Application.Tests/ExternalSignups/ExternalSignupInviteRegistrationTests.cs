using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.Invitations;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// The seam: <c>ExternalSignupAppService.RegisterAsync</c> when the caller supplies an
/// <c>InviteToken</c>. The rule these tests exist for is that the SERVER, not the posted form,
/// decides who is being registered: the token resolves the email, the role, the office and the
/// invited firm name, and the invitation is consumed in the same unit of work as the account.
///
/// <para>NOT pinned here:</para>
/// <list type="bullet">
///   <item>Authorization. <c>AddAlwaysAllowAuthorization()</c> is active in the test module, so
///         every attribute on this service is a no-op and no test may assert an auth failure.</item>
///   <item>The DTO-level guards (<c>Check.NotNullOrWhiteSpace</c> on Email / Password /
///         ConfirmPassword). ABP's DataAnnotations interceptor rejects those inputs before the
///         service body runs, so they are unreachable through this surface;
///         <c>ExternalSignupValidatorUnitTests</c> covers them by calling the static directly.</item>
///   <item>Email DELIVERY. The rig seeds only the four host-scoped notification templates, so the
///         per-office bodies do not exist. <c>CaseEvaluationAccountEmailer</c> logs and skips a
///         missing template rather than throwing, which is why the register path completes here.</item>
/// </list>
/// </summary>
public abstract class ExternalSignupInviteRegistrationTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Password = "Test1234!";

    private readonly IExternalSignupAppService _appService;
    private readonly InvitationManager _invitationManager;
    private readonly IdentityUserManager _userManager;
    private readonly IRepository<ApplicantAttorney, Guid> _applicantAttorneyRepository;
    private readonly ICurrentTenant _currentTenant;

    protected ExternalSignupInviteRegistrationTests()
    {
        _appService = GetRequiredService<IExternalSignupAppService>();
        _invitationManager = GetRequiredService<InvitationManager>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _applicantAttorneyRepository = GetRequiredService<IRepository<ApplicantAttorney, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    // The rig shares ONE SQLite connection across the whole run with no rollback between tests,
    // so every fixture below is keyed by a token unique to the test that created it. No assertion
    // in this file counts rows it did not create itself.
    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// A tampered register form cannot become a different identity. The posted Email and UserType
    /// say "attacker, Patient"; the token says "invitee, Claim Examiner"; the token wins.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_InviteToken_OverridesTamperedEmailAndRole()
    {
        var token = NewToken();
        var invitedEmail = $"inv-{token}@test.local";
        var attackerEmail = $"atk-{token}@test.local";

        string rawToken;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            (_, rawToken) = await _invitationManager.IssueAsync(
                tenantId: TenantsTestData.TenantARef,
                email: invitedEmail,
                userType: ExternalUserType.ClaimExaminer,
                invitedByUserId: IdentityUsersTestData.TenantAdmin1UserId);
        }

        // ValidateAsync runs under the AMBIENT tenant, before RegisterAsync's own
        // CurrentTenant.Change, so scope to the office the way the subdomain-scoped
        // register request does.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _appService.RegisterAsync(new ExternalUserSignUpDto
            {
                UserType = ExternalUserType.Patient,
                Email = attackerEmail,
                Password = Password,
                ConfirmPassword = Password,
                InviteToken = rawToken,
            });

            var invitedAccount = await _userManager.FindByEmailAsync(invitedEmail);
            invitedAccount.ShouldNotBeNull();

            var roles = await _userManager.GetRolesAsync(invitedAccount!);
            roles.ShouldContain(IdentityUsersTestData.ClaimExaminerRoleName);

            // Nothing was created on the address the form asked for.
            (await _userManager.FindByEmailAsync(attackerEmail)).ShouldBeNull();
        }
    }

    /// <summary>
    /// OBS-25: holding the token already proves the recipient owns the mailbox, so the invite path
    /// confirms the address instead of sending a second verification round trip.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_InviteToken_MarksEmailConfirmedWithoutVerification()
    {
        var token = NewToken();
        var email = $"cnf-{token}@test.local";

        string rawToken;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            (_, rawToken) = await _invitationManager.IssueAsync(
                tenantId: TenantsTestData.TenantARef,
                email: email,
                userType: ExternalUserType.Patient,
                invitedByUserId: IdentityUsersTestData.TenantAdmin1UserId);

            await _appService.RegisterAsync(new ExternalUserSignUpDto
            {
                UserType = ExternalUserType.Patient,
                Email = email,
                Password = Password,
                ConfirmPassword = Password,
                InviteToken = rawToken,
            });

            var account = await _userManager.FindByEmailAsync(email);
            account.ShouldNotBeNull();
            account!.EmailConfirmed.ShouldBeTrue();
        }
    }

    /// <summary>
    /// The paired negative of the test above: the anonymous path has no proof of ownership, so it
    /// must still leave the address unconfirmed and run the verification flow.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_NoInviteToken_LeavesEmailUnconfirmed()
    {
        var token = NewToken();
        var email = $"unc-{token}@test.local";

        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.Patient,
            Email = email,
            Password = Password,
            ConfirmPassword = Password,
            TenantId = TenantsTestData.TenantARef,
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var account = await _userManager.FindByEmailAsync(email);
            account.ShouldNotBeNull();
            account!.EmailConfirmed.ShouldBeFalse();
        }
    }

    /// <summary>
    /// Accepting the invitation is part of the same unit of work as creating the account: once the
    /// account exists the invitation must no longer be offered as active, and the staff list must
    /// report it as Accepted.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_InviteToken_MarksInvitationAccepted()
    {
        var token = NewToken();
        var email = $"acc-{token}@test.local";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var (invitation, rawToken) = await _invitationManager.IssueAsync(
                tenantId: TenantsTestData.TenantARef,
                email: email,
                userType: ExternalUserType.Patient,
                invitedByUserId: IdentityUsersTestData.TenantAdmin1UserId);

            // The invitation IS active before registration. Without this the "not active
            // afterwards" assertion below could pass against a row that was never there.
            var beforeRegistration =
                await _appService.GetActiveInvitedEmailsAsync(new List<string> { email });
            beforeRegistration.ShouldContain(email);

            await _appService.RegisterAsync(new ExternalUserSignUpDto
            {
                UserType = ExternalUserType.Patient,
                Email = email,
                Password = Password,
                ConfirmPassword = Password,
                InviteToken = rawToken,
            });

            var afterRegistration =
                await _appService.GetActiveInvitedEmailsAsync(new List<string> { email });
            afterRegistration.ShouldNotContain(email);

            var page = await _appService.GetInvitesAsync(new GetInvitesInput { Filter = email });
            var row = page.Items.SingleOrDefault(i => i.Id == invitation.Id);
            row.ShouldNotBeNull();
            row!.Status.ShouldBe(InvitationStatus.Accepted);
            row.AcceptedAt.ShouldNotBeNull();
        }
    }

    /// <summary>
    /// #21: an attorney invite may carry the firm the inviter already knows. When the recipient
    /// leaves the firm field blank the invited value carries through, so the required-firm rule
    /// passes on the invited value rather than rejecting a form the recipient was never asked to
    /// fill in.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_AttorneyInvite_CarriesInvitedFirmNameWhenFormBlank()
    {
        var token = NewToken();
        var email = $"aaf-{token}@test.local";
        var invitedFirm = $"TEST-Invited-Firm-{token}";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var (_, rawToken) = await _invitationManager.IssueAsync(
                tenantId: TenantsTestData.TenantARef,
                email: email,
                userType: ExternalUserType.ApplicantAttorney,
                invitedByUserId: IdentityUsersTestData.TenantAdmin1UserId,
                firmName: invitedFirm);

            // FirmName deliberately absent. Without the carry-through this throws
            // BusinessException(RegistrationFirmNameRequired) instead of registering.
            await _appService.RegisterAsync(new ExternalUserSignUpDto
            {
                UserType = ExternalUserType.ApplicantAttorney,
                Email = email,
                Password = Password,
                ConfirmPassword = Password,
                FirmName = null,
                InviteToken = rawToken,
            });

            var masters = await _applicantAttorneyRepository.GetListAsync(
                a => a.Email != null && a.Email == email);
            masters.Count.ShouldBe(1);
            masters[0].FirmName.ShouldBe(invitedFirm);
        }
    }
}
