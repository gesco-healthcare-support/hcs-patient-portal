using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Domains.Patients;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class PatientRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IPatientRepository _patientRepository;

    public PatientRepositoryTests()
    {
        _patientRepository = GetRequiredService<IPatientRepository>();
    }

    [Fact]
    public async Task GetListAsync_NoFilter_ReturnsAllSeededPatients()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var result = await _patientRepository.GetListAsync();

            result.ShouldNotBeEmpty();
            result.Any(p => p.Id == PatientsTestData.Patient1Id).ShouldBeTrue();
            result.Any(p => p.Id == PatientsTestData.Patient2Id).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task GetListAsync_FilterByFirstName_ReturnsPatient1Only()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var result = await _patientRepository.GetListAsync(
                firstName: PatientsTestData.Patient1FirstName);

            result.Count.ShouldBe(1);
            result[0].Id.ShouldBe(PatientsTestData.Patient1Id);
        });
    }

    [Fact]
    public async Task GetCountAsync_FilterByEmail_ReturnsOne()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var count = await _patientRepository.GetCountAsync(
                email: PatientsTestData.Patient2Email);

            count.ShouldBe(1);
        });
    }

    [Fact]
    public async Task GetListAsync_FilterByIdentityUserId_ScopesToOnePatient()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var result = await _patientRepository.GetListWithNavigationPropertiesAsync(
                identityUserId: IdentityUsersTestData.Patient1UserId);

            result.Count.ShouldBe(1);
            result[0].Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
        });
    }

    [Fact]
    public async Task GetWithNavigationPropertiesAsync_ReturnsPatientWithTenantNavProp()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var result = await _patientRepository.GetWithNavigationPropertiesAsync(
                PatientsTestData.Patient1Id);

            result.ShouldNotBeNull();
            result!.Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
            // Tenant nav prop resolves because the orchestrator seeds TenantA as a real
            // SaasTenants row (via ITenantManager.CreateAsync, which is the production path).
            result.Tenant.ShouldNotBeNull();
            result.Tenant!.Id.ShouldBe(TenantsTestData.TenantARef);
        });
    }
}
