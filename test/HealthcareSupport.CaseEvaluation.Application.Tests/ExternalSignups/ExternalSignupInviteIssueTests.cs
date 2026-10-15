using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// The seam: issuing an invite (<c>InviteExternalUserAsync</c>) and emailing an existing account
/// its portal link (<c>SendPortalLinkAsync</c>). The rule worth the most here is the DELIBERATE
/// ASYMMETRY between them -- the invite swallows a dispatch failure because the caller still holds
/// a copyable link, and the portal link does not, because staff would otherwise believe they had
/// helped someone they had not.
///
/// <para>The rig gives us that failure for free: <c>NotificationTemplateDataSeedContributor</c>
/// seeds only the four HOST-scoped codes, so neither <c>InviteExternalUser</c> nor
/// <c>ExternalUserPortalLink</c> exists inside an office and every render throws
/// <c>BusinessException("CaseEvaluation:NotificationTemplate.NotFound")</c>. The tests below PROVE
/// that fault is live before relying on it, so if someone later seeds templates per office these
/// tests fail loudly instead of quietly becoming vacuous. Do not "fix" them by seeding a template.</para>
///
/// <para>NOT pinned here: authorization (always-allow in the test module), email delivery itself,
/// and the <c>Guid.Empty</c> / null-email guards at the top of both methods -- ABP's
/// DataAnnotations interceptor rejects a blank or malformed Email first, so those service guards
/// are unreachable through their own public surface.</para>
/// </summary>
public abstract class ExternalSignupInviteIssueTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Password = "Test1234!";
    private const string InvitePathMarker = "/Account/Register?inviteToken=";

    private readonly IExternalSignupAppService _appService;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ICurrentTenant _currentTenant;

    protected ExternalSignupInviteIssueTests()
    {
        _appService = GetRequiredService<IExternalSignupAppService>();
        _notificationDispatcher = GetRequiredService<INotificationDispatcher>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    private static string ExtractInviteToken(string inviteUrl)
    {
        var index = inviteUrl.IndexOf(InvitePathMarker, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0);
        return WebUtility.UrlDecode(inviteUrl[(index + InvitePathMarker.Length)..]);
    }

    /// <summary>
    /// The copyable link is the whole fallback when email is degraded, so it has to carry a token
    /// that really validates -- not merely a URL of the right shape.
    /// </summary>
    [Fact]
    public async Task InviteExternalUserAsync_ReturnsUrlCarryingAWorkingToken()
    {
        var token = NewToken();
        var email = $"iss-{token}@test.local";

        var result = await _appService.InviteExternalUserAsync(new InviteExternalUserDto
        {
            Email = email,
            UserType = ExternalUserType.ClaimExaminer,
            TenantId = TenantsTestData.TenantARef,
        });

        result.AlreadyRegistered.ShouldBeFalse();
        result.InviteUrl.ShouldNotBeNullOrWhiteSpace();
        result.InviteUrl!.ShouldContain(InvitePathMarker);

        var rawToken = ExtractInviteToken(result.InviteUrl!);

        // FindByTokenHashAsync honours the IMultiTenant filter, so validate inside the office the
        // invite was issued for -- the same scope the subdomain gives the register page.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var validated = await _appService.ValidateInviteAsync(rawToken);
            validated.Email.ShouldBe(email);
            validated.RoleName.ShouldBe(IdentityUsersTestData.ClaimExaminerRoleName);
            validated.TenantName.ShouldBe(TenantsTestData.TenantAName);
        }
    }

    /// <summary>
    /// A dispatch failure must not cost the admin the link. The swallow around the invite dispatch
    /// is deliberate, and this is the test that stops someone "tidying" it away.
    /// </summary>
    [Fact]
    public async Task InviteExternalUserAsync_TemplateFaultStaysInsideTheService()
    {
        var token = NewToken();
        var email = $"swl-{token}@test.local";

        // Step 1: prove the fault is REAL in this rig. Dispatching the invite template inside the
        // office throws, because the office has no such template. If this assertion ever fails,
        // the wall has gone and step 2 below has stopped proving anything.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var wall = await Should.ThrowAsync<BusinessException>(
                () => _notificationDispatcher.DispatchAsync(
                    templateCode: NotificationTemplateConsts.Codes.InviteExternalUser,
                    recipients: new[]
                    {
                        new NotificationRecipient(
                            email: $"probe-{token}@test.local",
                            role: null,
                            isRegistered: false),
                    },
                    variables: new Dictionary<string, object?>(),
                    contextTag: $"TEST-template-wall-probe/{token}"));

            wall.Code.ShouldBe(CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound);
        }

        // Step 2: the same failing dispatch happens inside the invite, and the caller still gets
        // a link plus a real pending invitation.
        var result = await _appService.InviteExternalUserAsync(new InviteExternalUserDto
        {
            Email = email,
            UserType = ExternalUserType.Patient,
            TenantId = TenantsTestData.TenantARef,
        });

        result.InviteUrl.ShouldNotBeNullOrWhiteSpace();

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var active = await _appService.GetActiveInvitedEmailsAsync(new List<string> { email });
            active.ShouldContain(email);
        }
    }

    /// <summary>
    /// Guard ORDER: the external-role check runs before the role-name mapping, so a non-external
    /// value is refused as a policy decision rather than falling through to the mapper's
    /// "Invalid user type." catch-all.
    ///
    /// <para>Honest limitation: both branches throw <c>UserFriendlyException</c>, so this test
    /// discriminates on the MESSAGE alone. It is the weakest assertion in the file and is kept
    /// only because the ordering is the rule and nothing else covers it.</para>
    /// </summary>
    [Fact]
    public async Task InviteExternalUserAsync_NonExternalUserType_IsRefusedBeforeRoleMapping()
    {
        var token = NewToken();

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            () => _appService.InviteExternalUserAsync(new InviteExternalUserDto
            {
                Email = $"bad-{token}@test.local",
                // [Required] on a non-nullable enum accepts any numeric value, so this reaches
                // the service body rather than being rejected by DataAnnotations.
                UserType = (ExternalUserType)99,
                TenantId = TenantsTestData.TenantARef,
            }));

        ex.Message.ShouldContain("external roles");
    }

    /// <summary>
    /// Nobody to email is not an error. Staff already know from the invite result whether the
    /// address has an account, so a throw here would only add noise to a support call.
    /// </summary>
    [Fact]
    public async Task SendPortalLinkAsync_UnknownEmail_IsASilentNoOp()
    {
        var token = NewToken();

        // THIS FACT USED TO HAVE NO ASSERTION AT ALL -- the only one of the batch. It called the
        // method and ended, relying on a comment pointing at the companion Fact below to establish
        // that the silence means "returned early" rather than "the method does nothing".
        //
        // That link was real but IMPLICIT, and an implicit link is not a guarantee: delete or rename
        // the companion and this becomes a test that passes against an empty method body, with
        // nothing anywhere reporting the loss. Shouldly's NotThrowAsync makes the claim explicit and
        // gives the failure a name and a reason.
        await Should.NotThrowAsync(
            async () => await _appService.SendPortalLinkAsync(new SendPortalLinkInput
            {
                Email = $"nobody-{token}@test.local",
                TenantId = TenantsTestData.TenantARef,
            }),
            "An address with no account must be a silent no-op. Staff already learn from the invite "
            + "result whether the address is registered, so throwing here adds noise to a support "
            + "call without adding information. The companion Fact "
            + "SendPortalLinkAsync_DispatchFailure_IsNotSwallowed proves a resolvable address DOES "
            + "reach the dispatcher, so this silence is the early return rather than a dead method.");
    }

    /// <summary>
    /// The asymmetry against the invite. There is no copyable fallback on this path, so a dispatch
    /// failure has to reach the caller.
    /// </summary>
    [Fact]
    public async Task SendPortalLinkAsync_DispatchFailure_IsNotSwallowed()
    {
        var token = NewToken();
        var email = $"prt-{token}@test.local";

        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.Patient,
            Email = email,
            Password = Password,
            ConfirmPassword = Password,
            TenantId = TenantsTestData.TenantARef,
        });

        var ex = await Should.ThrowAsync<BusinessException>(
            () => _appService.SendPortalLinkAsync(new SendPortalLinkInput
            {
                Email = email,
                TenantId = TenantsTestData.TenantARef,
            }));

        // The office has no ExternalUserPortalLink template, which is exactly the outage this
        // path must not hide.
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound);
    }
}
