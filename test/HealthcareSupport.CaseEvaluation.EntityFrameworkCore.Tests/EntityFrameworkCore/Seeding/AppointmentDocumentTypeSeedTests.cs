using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Seeding;

/// <summary>
/// <see cref="AppointmentDocumentTypeDataSeedContributor"/>: every practice gets the reserved
/// "Generated Packet" system row plus five document-type labels, each offered for its own set of
/// appointment types; a second run adds nothing; the host scope is skipped.
///
/// <para>The "already there?" checks are per office, so the test carries an OFFICE decoy: office B
/// already has a "Joint Letter" label, and the new practice must still get its own.</para>
/// </summary>
public class AppointmentDocumentTypeSeedTests : SeedContributorTestBase
{
    private static readonly string[] LabelNames = { "Joint Letter", "Medical Records", "Advocacy Letter", "Cover Letter", AppointmentDocumentTypeConsts.PanelStrikeListName };

    private readonly AppointmentDocumentTypeDataSeedContributor _seeder;
    private readonly IRepository<AppointmentDocumentType, Guid> _rows;
    private readonly IAppointmentDocumentTypeRepository _documentTypes;

    public AppointmentDocumentTypeSeedTests()
    {
        _seeder = GetRequiredService<AppointmentDocumentTypeDataSeedContributor>();
        _rows = GetRequiredService<IRepository<AppointmentDocumentType, Guid>>();
        _documentTypes = GetRequiredService<IAppointmentDocumentTypeRepository>();
    }

    [Fact]
    public async Task TenantPass_SeedsTheSystemRowAndTheFiveLabels_OnceOnly_EvenWhenAnotherOfficeHasOne()
    {
        await InScopeAsync(TenantsTestData.TenantBRef, () => _rows.InsertAsync(new AppointmentDocumentType(
            Guid.NewGuid(), "Joint Letter", appliesToAll: false, isActive: true, isSystem: false, tenantId: TenantsTestData.TenantBRef), autoSave: true));
        var practiceId = await CreatePracticeAsync();

        await SeedAsync(_seeder, new DataSeedContext(practiceId));
        await SeedAsync(_seeder, new DataSeedContext(practiceId));

        var rows = await InScopeAsync(practiceId, () => _rows.GetListAsync());
        rows.Count.ShouldBe(6);
        var system = rows.Where(r => r.IsSystem).ShouldHaveSingleItem();
        system.Name.ShouldBe(AppointmentDocumentTypeConsts.GeneratedPacketName);
        system.AppliesToAll.ShouldBeTrue();
        rows.Where(r => !r.IsSystem).Select(r => r.Name).OrderBy(n => n).ShouldBe(LabelNames.OrderBy(n => n));
        rows.ShouldAllBe(r => r.TenantId == practiceId && r.IsActive);

        (await TypeIdsOf(practiceId, rows, "Joint Letter")).ShouldBe(new[] { CaseEvaluationSeedIds.AppointmentTypes.Ame });
        (await TypeIdsOf(practiceId, rows, "Medical Records")).ShouldBe(
            new[] { CaseEvaluationSeedIds.AppointmentTypes.Ame, CaseEvaluationSeedIds.AppointmentTypes.Ime, CaseEvaluationSeedIds.AppointmentTypes.PanelQme }.OrderBy(g => g).ToArray());
        (await TypeIdsOf(practiceId, rows, "Cover Letter")).ShouldBe(new[] { CaseEvaluationSeedIds.AppointmentTypes.PanelQme });
    }

    [Fact]
    public async Task HostPass_WritesNothing()
    {
        var before = await InScopeAsync<long>(null, () => _rows.GetCountAsync());

        await SeedAsync(_seeder, new DataSeedContext(null));

        (await InScopeAsync<long>(null, () => _rows.GetCountAsync())).ShouldBe(before);
    }

    private async Task<Guid[]> TypeIdsOf(Guid practiceId, System.Collections.Generic.List<AppointmentDocumentType> rows, string name)
    {
        var id = rows.Single(r => r.Name == name).Id;
        var withTypes = await InScopeAsync(practiceId, () => _documentTypes.GetWithAppointmentTypesAsync(id));
        return withTypes.AppointmentTypes.Select(t => t.AppointmentTypeId).OrderBy(g => g).ToArray();
    }
}
