using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class AppointmentEmployerDetailRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IAppointmentEmployerDetailRepository _detailRepository;
    private readonly ICurrentTenant _currentTenant;

    public AppointmentEmployerDetailRepositoryTests()
    {
        _detailRepository = GetRequiredService<IAppointmentEmployerDetailRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task GetListAsync_PlainOverload_AppliesEmployerNameFilter()
    {
        // Seeded Detail1 lives in TenantA. Filtering by its EmployerName inside
        // the TenantA context must return Detail1 and exclude the tenant-B row.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var results = await _detailRepository.GetListAsync(
                    employerName: AppointmentEmployerDetailsTestData.Detail1EmployerName);

                results.Any(x => x.Id == AppointmentEmployerDetailsTestData.Detail1Id).ShouldBeTrue();
                results.All(x => x.EmployerName == AppointmentEmployerDetailsTestData.Detail1EmployerName).ShouldBeTrue();
            }
        });
    }
}
