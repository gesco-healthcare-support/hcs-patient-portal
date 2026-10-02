using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Domains.Patients;

public class PatientRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IPatientRepository _patientRepository;
    private readonly IDataFilter<IMultiTenant> _dataFilter;

    public PatientRepositoryTests()
    {
        _patientRepository = GetRequiredService<IPatientRepository>();
        // FEAT-09 (ADR-006 T4): Patient is now IMultiTenant. Repository
        // tests run in host context (CurrentTenant.Id == null), so the
        // auto-filter generates WHERE TenantId IS NULL and excludes both
        // seeded patients (each carries a real TenantId). Disabling the
        // filter inside the test scope restores cross-tenant visibility
        // so these tests verify repository LINQ behaviour without the
        // tenancy concern they were not written to test.
        _dataFilter = GetRequiredService<IDataFilter<IMultiTenant>>();
    }

    [Fact]
    public async Task GetListAsync_NoFilter_ReturnsAllSeededPatients()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable())
            {
                var result = await _patientRepository.GetListAsync();

                result.ShouldNotBeEmpty();
                result.Any(p => p.Id == PatientsTestData.Patient1Id).ShouldBeTrue();
                result.Any(p => p.Id == PatientsTestData.Patient2Id).ShouldBeTrue();
            }
        });
    }

    [Fact]
    public async Task GetListAsync_FilterByFirstName_ReturnsPatient1Only()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable())
            {
                var result = await _patientRepository.GetListAsync(
                    firstName: PatientsTestData.Patient1FirstName);

                result.Count.ShouldBe(1);
                result[0].Id.ShouldBe(PatientsTestData.Patient1Id);
            }
        });
    }

    [Fact]
    public async Task GetCountAsync_FilterByEmail_ReturnsOne()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable())
            {
                var count = await _patientRepository.GetCountAsync(
                    email: PatientsTestData.Patient2Email);

                count.ShouldBe(1);
            }
        });
    }

    [Fact]
    public async Task GetListAsync_FilterByIdentityUserId_ScopesToOnePatient()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable())
            {
                var result = await _patientRepository.GetListWithNavigationPropertiesAsync(
                    identityUserId: IdentityUsersTestData.Patient1UserId);

                result.Count.ShouldBe(1);
                result[0].Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
            }
        });
    }

    [Fact]
    public async Task GetWithNavigationPropertiesAsync_LeavesTenantNavNull()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable())
            {
                var result = await _patientRepository.GetWithNavigationPropertiesAsync(
                    PatientsTestData.Patient1Id);

                result.ShouldNotBeNull();
                result!.Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
                // BUG-01 (db-per-office): the SaaS Tenant row lives in the host DB only, so an
                // office DB context never joins SaasTenants -- the repo leaves the Tenant nav
                // null (joining it 500'd patient get-or-create on every booking).
                result.Tenant.ShouldBeNull();
            }
        });
    }

    [Fact]
    public async Task FindBestMatchAsync_ReturnsTheNumberOfKeysThatMatched()
    {
        // Four of the six keys match Patient1 -- first name, last name, date of birth and ZIP -- and
        // the other two are not supplied. The count is asserted exactly, not as "at least three": the
        // caller ranks candidates by it, so a count that is wrong but still clears the threshold
        // would pick the wrong patient without failing anything else. Names are passed already
        // normalised, as PatientManager.FindOrCreateAsync passes them.
        PatientMatchCandidate? match = null;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable())
            {
                match = await _patientRepository.FindBestMatchAsync(
                    TenantsTestData.TenantARef,
                    PatientMatching.Normalise(PatientsTestData.Patient1FirstName)!,
                    PatientMatching.Normalise(PatientsTestData.Patient1LastName)!,
                    PatientsTestData.FixedDateOfBirth,
                    ssn: null,
                    phone: null,
                    zip: PatientMatching.Normalise(PatientsTestData.Patient1ZipCode));
            }
        });

        match.ShouldNotBeNull();
        match.Id.ShouldBe(PatientsTestData.Patient1Id);
        match.MatchCount.ShouldBe(4);
    }
}
