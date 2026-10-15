using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;

/// <summary>
/// Issue #1014 on the REAL rig: the office name in a rendered email comes from the tenant STORE,
/// keyed by the office the handler entered -- never from <c>ICurrentTenant.Name</c>, which ABP leaves
/// null inside <c>Change(id)</c>. That null is why the Joint Declaration overdue notice went out with
/// a blank office name at both of its <c>##ClinicName##</c> positions.
///
/// <para>Real renderer, real template repository, real <c>ITenantStore</c>, real <c>ICurrentTenant</c>:
/// nothing is substituted, because a substituted tenant returning a name is exactly what hid this.
/// Each case first asserts the premise (the ambient name IS null), so it cannot pass for the wrong
/// reason.</para>
///
/// <para>The OFFICE DECOY is real: the same template is rendered in office A and office B, and each
/// render must carry its own office's name and not the other's. A lookup that ignored the current
/// office (for example "the first tenant in the store") would fail one of the two rows.</para>
///
/// <para>The rig seeds only the host-scoped template codes, so the Joint Declaration template is
/// inserted per office from its shipped seed content.</para>
/// </summary>
public class NotificationTemplateRendererOfficeNameTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string JdfCode = NotificationTemplateConsts.Codes.AppointmentJointDeclarationOverdueInternal;

    /// <summary>
    /// Rows are office NAMES only, mapped to the id inside the test: xUnit cannot serialise a
    /// <see cref="Guid"/> theory argument (xUnit1044), so passing the id would cost each row its own
    /// entry in the test explorer.
    /// </summary>
    public static TheoryData<string, string> Offices() => new()
    {
        { TenantsTestData.TenantAName, TenantsTestData.TenantBName },
        { TenantsTestData.TenantBName, TenantsTestData.TenantAName },
    };

    private static readonly Dictionary<string, Guid> OfficeIdsByName = new(StringComparer.Ordinal)
    {
        [TenantsTestData.TenantAName] = TenantsTestData.TenantARef,
        [TenantsTestData.TenantBName] = TenantsTestData.TenantBRef,
    };

    [Theory]
    [MemberData(nameof(Offices))]
    public async Task TheJdfNotice_NamesTheOfficeItIsRenderedFor_AtBothPositions(
        string officeName, string otherOfficeName)
    {
        var officeId = OfficeIdsByName[officeName];
        RenderedNotification? rendered = null;

        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(officeId))
            {
                GetRequiredService<ICurrentTenant>().Name.ShouldBeNull(
                    "the premise of #1014: ABP's Change(id) leaves the ambient name null");

                await EnsureJdfTemplateAsync(officeId);

                // No ClinicName in the variables: that is what every handler passes after the fix.
                rendered = await GetRequiredService<INotificationTemplateRenderer>().RenderAsync(
                    JdfCode,
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["DocumentName"] = "TEST-Joint Declaration Form",
                    });
            }
        });

        var body = rendered.ShouldNotBeNull().BodyEmail;
        body.ShouldNotContain("##ClinicName##");
        CountOf(body, officeName).ShouldBe(2, $"both ##ClinicName## positions must name {officeName}");
        body.ShouldNotContain(otherOfficeName);
    }

    private async Task EnsureJdfTemplateAsync(Guid officeId)
    {
        var templates = GetRequiredService<INotificationTemplateRepository>();
        if (await templates.FindByCodeAsync(JdfCode) != null)
        {
            return;
        }

        var types = GetRequiredService<INotificationTemplateTypeRepository>();
        var type = new NotificationTemplateType(Guid.NewGuid(), "TEST-Email-" + Guid.NewGuid().ToString("N")[..6]);
        await types.InsertAsync(type, autoSave: true);

        var defaults = NotificationTemplateSeedDefaults.GetSeedDefaults(JdfCode);
        await templates.InsertAsync(
            new NotificationTemplate(
                id: Guid.NewGuid(),
                tenantId: officeId,
                templateCode: JdfCode,
                templateTypeId: type.Id,
                subject: defaults.Subject,
                bodyEmail: defaults.BodyEmail,
                bodySms: defaults.BodySms),
            autoSave: true);
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }
        return count;
    }
}
