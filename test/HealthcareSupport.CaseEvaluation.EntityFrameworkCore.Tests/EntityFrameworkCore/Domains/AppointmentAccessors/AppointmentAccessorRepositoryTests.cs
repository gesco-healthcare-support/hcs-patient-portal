using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class AppointmentAccessorRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IAppointmentAccessorRepository _accessorRepository;
    private readonly ICurrentTenant _currentTenant;

    public AppointmentAccessorRepositoryTests()
    {
        _accessorRepository = GetRequiredService<IAppointmentAccessorRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task GetListAsync_PlainOverload_AppliesAccessTypeFilter()
    {
        // Accessor1 lives in TenantA with View access; Accessor2 in TenantB with
        // Edit access. Filtering by AccessType.View inside TenantA context must
        // return Accessor1 and exclude every other row.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var results = await _accessorRepository.GetListAsync(
                    accessTypeId: AccessType.View);

                results.Any(x => x.Id == AppointmentAccessorsTestData.Accessor1Id).ShouldBeTrue();
                results.All(x => x.AccessTypeId == AccessType.View).ShouldBeTrue();
            }
        });
    }
}
