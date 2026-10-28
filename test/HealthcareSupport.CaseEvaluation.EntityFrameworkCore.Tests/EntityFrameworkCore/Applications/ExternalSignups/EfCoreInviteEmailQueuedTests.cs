using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.ExternalSignups;

/// <summary>
/// 2026-10-27: the invite result says whether its email was queued. The shared invite tests run where the
/// office has no invite template, so every dispatch fails there and they pin the false half; this pins the
/// true half, with a dispatcher that accepts the email. Values are synthetic.
/// </summary>
public class EfCoreInviteEmailQueuedTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly RecordingNotificationDispatcher _dispatcher = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_dispatcher));
    }

    [Fact]
    public async Task An_invite_whose_email_is_queued_says_so()
    {
        var result = await GetRequiredService<IExternalSignupAppService>().InviteExternalUserAsync(new InviteExternalUserDto
        {
            Email = "TEST-queued-invite@test.local",
            UserType = ExternalUserType.ClaimExaminer,
            TenantId = TenantsTestData.TenantARef,
        });

        result.EmailQueued.ShouldBeTrue();
        _dispatcher.Dispatches.Where(d => d.TemplateCode == NotificationTemplateConsts.Codes.InviteExternalUser)
            .ShouldHaveSingleItem();
    }

    [Fact]
    public async Task An_invite_whose_email_cannot_be_queued_says_so_and_still_returns_the_link()
    {
        _dispatcher.ThrowTemplateNotFound = true;

        var result = await GetRequiredService<IExternalSignupAppService>().InviteExternalUserAsync(new InviteExternalUserDto
        {
            Email = "TEST-unqueued-invite@test.local",
            UserType = ExternalUserType.ClaimExaminer,
            TenantId = TenantsTestData.TenantARef,
        });

        result.EmailQueued.ShouldBeFalse();
        result.InviteUrl.ShouldNotBeNullOrWhiteSpace();
    }
}
