using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Enums;
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

    // #1202: a row stored with separators must still match digits-only input. Synthetic values only;
    // the date of birth is unique to these rows so nothing seeded can contribute a key.
    private static readonly DateTime PunctuatedDob = new DateTime(1971, 3, 9, 0, 0, 0, DateTimeKind.Utc);

    private async Task SeedPunctuatedPatientAsync(string phone, string ssn)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable())
            {
                await _patientRepository.InsertAsync(
                    new Patient(
                        id: Guid.NewGuid(),
                        stateId: null,
                        appointmentLanguageId: null,
                        identityUserId: null,
                        tenantId: TenantsTestData.TenantARef,
                        firstName: "TEST-Punct",
                        lastName: "Synthetic",
                        email: $"TEST-p-{Guid.NewGuid().ToString("N")[..12]}@test.local",
                        genderId: Gender.Unspecified,
                        dateOfBirth: PunctuatedDob,
                        phoneNumberTypeId: PhoneNumberType.Work,
                        phoneNumber: phone,
                        socialSecurityNumber: ssn),
                    autoSave: true);
            }
        });
    }

    [Fact]
    public async Task FindBestMatchAsync_MatchesPhoneAndSsnStoredWithSeparators()
    {
        await SeedPunctuatedPatientAsync("(555) 010-4567", "900-12-3456");
        PatientMatchCandidate? match = null;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable())
            {
                // Names differ, so DOB + SSN + phone are the only three keys that can match.
                match = await _patientRepository.FindBestMatchAsync(
                    TenantsTestData.TenantARef, "other", "person", PunctuatedDob,
                    ssn: "900123456", phone: "5550104567", zip: null);
            }
        });

        match.ShouldNotBeNull();
        match.MatchCount.ShouldBe(3);
    }

    [Fact]
    public async Task FindBestMatchAsync_AnInputThatNormalisesToNothingMatchesNothing()
    {
        await SeedPunctuatedPatientAsync("", "");
        PatientMatchCandidate? match = null;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable())
            {
                // DOB is the only real key; "---" must not count as matching the empty stored values.
                match = await _patientRepository.FindBestMatchAsync(
                    TenantsTestData.TenantARef, "other", "person", PunctuatedDob,
                    ssn: "---", phone: "---", zip: null);
            }
        });

        match.ShouldBeNull();
    }

    [Fact]
    public async Task GetDeduplicationCandidatesAsync_MatchesPhoneAndSsnStoredWithSeparatorsFromFormattedInput()
    {
        await SeedPunctuatedPatientAsync("555.010.4568", "900-12-3457");
        System.Collections.Generic.List<Patient> byPhone = null!;
        System.Collections.Generic.List<Patient> bySsn = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable())
            {
                // Different separators on each side: only digit-level comparison can match.
                byPhone = await _patientRepository.GetDeduplicationCandidatesAsync(
                    TenantsTestData.TenantARef, null, null, "(555) 010-4568", null, null, null);
                bySsn = await _patientRepository.GetDeduplicationCandidatesAsync(
                    TenantsTestData.TenantARef, null, null, null, null, "900 12 3457", null);
            }
        });

        byPhone.Count.ShouldBe(1);
        bySsn.Count.ShouldBe(1);
    }
}
