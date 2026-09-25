using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Seeding;

/// <summary>
/// The OFFICE pass of <see cref="NotificationTemplateDataSeedContributor"/>: a new practice gets
/// both template types and every template code; a second run adds nothing; a template whose body
/// ships from a resource file is refreshed when its stored copy drifts, while a plain template an
/// admin edited is left alone.
///
/// <para>Harness note: in production each office has its own database, so the office pass never
/// sees the host's rows. This rig keeps host and offices in ONE database, and the template types
/// use fixed ids, so the office pass would collide with the host's type rows. Each test therefore
/// first hard-deletes the host templates and types from its own per-test database (#1056), which
/// reproduces the office database the seeder is written for. No other test is affected.</para>
///
/// <para>The "already seeded?" read is per office, so the test carries an OFFICE decoy: office B
/// already holds a copy of one code, and the new practice must still get its own.</para>
/// </summary>
public class NotificationTemplateSeedTests : SeedContributorTestBase
{
    private readonly NotificationTemplateDataSeedContributor _seeder;
    private readonly INotificationTemplateRepository _templates;
    private readonly INotificationTemplateTypeRepository _types;
    private readonly IRepository<NotificationTemplate, Guid> _templateRows;
    private readonly IRepository<NotificationTemplateType, Guid> _typeRows;

    public NotificationTemplateSeedTests()
    {
        _seeder = GetRequiredService<NotificationTemplateDataSeedContributor>();
        _templates = GetRequiredService<INotificationTemplateRepository>();
        _types = GetRequiredService<INotificationTemplateTypeRepository>();
        _templateRows = GetRequiredService<IRepository<NotificationTemplate, Guid>>();
        _typeRows = GetRequiredService<IRepository<NotificationTemplateType, Guid>>();
    }

    [Fact]
    public async Task OfficePass_SeedsBothTypesAndEveryCode_OnceOnly_EvenWhenAnotherOfficeHasACode()
    {
        await RemoveHostCatalogAsync();
        var decoyCode = NotificationTemplateConsts.Codes.All[0];
        await InsertTemplateAsync(TenantsTestData.TenantBRef, decoyCode, "TEST-office B subject");
        var practiceId = await CreatePracticeAsync();

        await SeedAsync(_seeder, new DataSeedContext(practiceId));
        await SeedAsync(_seeder, new DataSeedContext(practiceId));

        var templates = await InScopeAsync(practiceId, () => _templateRows.GetListAsync());
        templates.Select(t => t.TemplateCode).OrderBy(c => c).ShouldBe(NotificationTemplateConsts.Codes.All.OrderBy(c => c));
        templates.ShouldAllBe(t => t.TenantId == practiceId && t.IsActive);
        templates.Single(t => t.TemplateCode == decoyCode).Subject.ShouldBe(NotificationTemplateSeedDefaults.GetSeedDefaults(decoyCode).Subject);
        (await InScopeAsync(practiceId, () => _typeRows.GetListAsync())).Count.ShouldBe(2);
    }

    [Fact]
    public async Task ASecondRun_RefreshesADriftedResourceBackedTemplate_ButKeepsAnAdminsEditToAPlainOne()
    {
        await RemoveHostCatalogAsync();
        var practiceId = await CreatePracticeAsync();
        await SeedAsync(_seeder, new DataSeedContext(practiceId));
        var resourceBacked = NotificationTemplateConsts.Codes.All.First(NotificationTemplateSeedDefaults.HasResourceBackedBody);
        var plain = NotificationTemplateConsts.Codes.All.First(c => !NotificationTemplateSeedDefaults.HasResourceBackedBody(c));
        await EditSubjectAsync(practiceId, resourceBacked, "TEST-drifted subject");
        await EditSubjectAsync(practiceId, plain, "TEST-admin subject");

        await SeedAsync(_seeder, new DataSeedContext(practiceId));

        var templates = await InScopeAsync(practiceId, () => _templateRows.GetListAsync());
        var refreshed = templates.Single(t => t.TemplateCode == resourceBacked);
        refreshed.Subject.ShouldBe(NotificationTemplateSeedDefaults.GetSeedDefaults(resourceBacked).Subject);
        refreshed.BodyEmail.ShouldBe(NotificationTemplateSeedDefaults.GetSeedDefaults(resourceBacked).BodyEmail);
        templates.Single(t => t.TemplateCode == plain).Subject.ShouldBe("TEST-admin subject");
    }

    /// <summary>Hard-deletes the host's templates, then its template types (templates reference types).</summary>
    private Task RemoveHostCatalogAsync() =>
        InScopeAsync(null, async () =>
        {
            await _templateRows.HardDeleteAsync(t => t.TenantId == null, autoSave: true);
            await _typeRows.HardDeleteAsync(t => t.TenantId == null, autoSave: true);
            return true;
        });

    private Task InsertTemplateAsync(Guid officeId, string code, string subject) =>
        InScopeAsync(officeId, async () =>
        {
            var type = await _types.InsertAsync(new NotificationTemplateType(Guid.NewGuid(), "TEST-Email-" + Guid.NewGuid().ToString("N")[..6]), autoSave: true);
            await _templates.InsertAsync(new NotificationTemplate(
                id: Guid.NewGuid(), tenantId: officeId, templateCode: code, templateTypeId: type.Id,
                subject: subject, bodyEmail: "<p>TEST</p>", bodySms: "TEST", description: null, isActive: true), autoSave: true);
            return true;
        });

    private Task EditSubjectAsync(Guid practiceId, string code, string subject) =>
        InScopeAsync(practiceId, async () =>
        {
            var template = await _templateRows.GetAsync(t => t.TemplateCode == code);
            template.Subject = subject;
            await _templates.UpdateAsync(template, autoSave: true);
            return true;
        });
}
