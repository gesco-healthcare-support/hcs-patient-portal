using Shouldly;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.TestData;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Domains.Doctors;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class DoctorRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IDoctorRepository _doctorRepository;
    private readonly ICurrentTenant _currentTenant;

    public DoctorRepositoryTests()
    {
        _doctorRepository = GetRequiredService<IDoctorRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task GetListAsync()
    {
        // Doctors are tenant-scoped (IMultiTenant). Both seeded doctors live in
        // TenantA; a repository-level query must run inside that tenant's scope
        // for the IMultiTenant filter to see them.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var result = await _doctorRepository.GetListAsync(
                    firstName: DoctorsTestData.Doctor1FirstName,
                    lastName: DoctorsTestData.Doctor1LastName,
                    email: DoctorsTestData.Doctor1Email);

                result.Count.ShouldBe(1);
                result.FirstOrDefault().ShouldNotBe(null);
                result.First().Id.ShouldBe(DoctorsTestData.Doctor1Id);
            }
        });
    }

    [Fact]
    public async Task GetCountAsync()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var result = await _doctorRepository.GetCountAsync(
                    firstName: DoctorsTestData.Doctor2FirstName,
                    lastName: DoctorsTestData.Doctor2LastName,
                    email: DoctorsTestData.Doctor2Email);

                result.ShouldBe(1);
            }
        });
    }
}