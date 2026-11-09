using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using HealthcareSupport.CaseEvaluation.Invitations;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.ExternalSignups;

/// <summary>
/// 2026-11-09: the invite, the resent invite and the portal-link email named the office by its
/// web-address slug ("Dr. falkinstein"). They now name it as people know it: the invitation is from
/// the doctor and practice, the portal link names the practice. The rig stores no letterhead, so
/// office A reads as its own seeded doctor here. The slug must not be the display name.
///
/// <para>A recording dispatcher captures what each email was sent with; values are synthetic.</para>
/// </summary>
public class EfCoreInviteOfficeDisplayNameTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private static readonly string OfficeADisplayName =
        $"Dr. {DoctorsTestData.Doctor1FirstName} {DoctorsTestData.Doctor1LastName}";

    private readonly RecordingNotificationDispatcher _dispatcher = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_dispatcher));
    }

    // The rig shares one SQLite connection for the whole run and never rolls back, so each test
    // keys its fixture with its own token. Lower-case: the service stores and sends the normalised
    // address.
    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task The_invite_email_is_from_the_office_by_name_not_by_slug()
    {
        var email = $"test-name-invite-{NewToken()}@test.local";

        var result = await GetRequiredService<IExternalSignupAppService>().InviteExternalUserAsync(new InviteExternalUserDto
        {
            Email = email,
            UserType = ExternalUserType.ApplicantParalegal,
            TenantId = TenantsTestData.TenantARef,
        });

        var invite = _dispatcher.Dispatches.Single(d =>
            d.TemplateCode == NotificationTemplateConsts.Codes.InviteExternalUser
            && d.Recipients.Contains(email));
        invite.Variables["TenantName"].ShouldBe(OfficeADisplayName);
        invite.Variables["RoleName"].ShouldBe("Paralegal / Case Manager (Applicant side)");

        // The staff result card ("Practice") keeps the slug: it is a staff-only identifier.
        result.TenantName.ShouldBe(TenantsTestData.TenantAName);
    }

    [Fact]
    public async Task A_resent_invite_is_from_the_office_by_name_too()
    {
        var email = $"test-name-resend-{NewToken()}@test.local";
        var signups = GetRequiredService<IExternalSignupAppService>();
        await signups.InviteExternalUserAsync(new InviteExternalUserDto
        {
            Email = email,
            UserType = ExternalUserType.Patient,
            TenantId = TenantsTestData.TenantARef,
        });

        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var invitation = await WithUnitOfWorkAsync(async () =>
                (await GetRequiredService<IRepository<Invitation, Guid>>().GetListAsync(i => i.Email == email)).Single());
            _dispatcher.Clear();

            await signups.ResendInviteAsync(invitation.Id);
        }

        var resent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        resent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.InviteExternalUser);
        resent.Variables["TenantName"].ShouldBe(OfficeADisplayName);
    }

    [Fact]
    public async Task The_portal_link_email_names_the_practice()
    {
        _dispatcher.Clear();

        await GetRequiredService<IExternalSignupAppService>().SendPortalLinkAsync(new SendPortalLinkInput
        {
            Email = IdentityUsersTestData.ApplicantAttorney1Email,
            TenantId = TenantsTestData.TenantARef,
        });

        var link = _dispatcher.Dispatches.ShouldHaveSingleItem();
        link.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.ExternalUserPortalLink);
        link.Variables["TenantName"].ShouldBe(OfficeADisplayName);
        link.Variables["ClinicName"].ShouldBe(OfficeADisplayName);
    }
}
