using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.States;
using HealthcareSupport.CaseEvaluation.TestData;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Testing;

/// <summary>
/// Single orchestrator for integration-test data seeding. Replaces per-entity
/// IDataSeedContributor implementations because ABP does not guarantee the
/// execution order of multiple contributors (see ABP forum #571).
///
/// Seed order (strict FK dependency chain):
///   Tenant -&gt; IdentityUser -&gt; Location (+ State, AppointmentType) -&gt; Doctor -&gt; Patient -&gt; (future: DoctorAvailability, Appointment, ApplicantAttorney, ...)
///
/// Tenants are created via <c>ITenantManager.CreateAsync(name)</c> -- the same
/// framework path production uses (DoctorTenantAppService.CreateAsync hits this
/// transitively through TenantAppService). Returned tenant GUIDs are captured
/// into <see cref="TenantsTestData"/> static properties so downstream seeds and
/// tests can reference them.
/// </summary>
public class CaseEvaluationIntegrationTestSeedContributor : IDataSeedContributor, ISingletonDependency
{
    private bool _isSeeded;
    private readonly IDoctorRepository _doctorRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IRepository<State, Guid> _stateRepository;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypeRepository;
    private readonly ITenantManager _tenantManager;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly IdentityUsersDataSeedContributor _identityUsersSeeder;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public CaseEvaluationIntegrationTestSeedContributor(
        IDoctorRepository doctorRepository,
        IPatientRepository patientRepository,
        ILocationRepository locationRepository,
        IRepository<State, Guid> stateRepository,
        IRepository<AppointmentType, Guid> appointmentTypeRepository,
        ITenantManager tenantManager,
        IRepository<Tenant, Guid> tenantRepository,
        IdentityUsersDataSeedContributor identityUsersSeeder,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _doctorRepository = doctorRepository;
        _patientRepository = patientRepository;
        _locationRepository = locationRepository;
        _stateRepository = stateRepository;
        _appointmentTypeRepository = appointmentTypeRepository;
        _tenantManager = tenantManager;
        _tenantRepository = tenantRepository;
        _identityUsersSeeder = identityUsersSeeder;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (_isSeeded)
        {
            return;
        }

        await SeedTenantsAsync();
        await _unitOfWorkManager.Current!.SaveChangesAsync();

        await _identityUsersSeeder.SeedAsync(context);
        await _unitOfWorkManager.Current!.SaveChangesAsync();

        await SeedLocationsAsync();
        await _unitOfWorkManager.Current!.SaveChangesAsync();

        await SeedDoctorsAsync();
        await SeedPatientsAsync();
        await _unitOfWorkManager.Current!.SaveChangesAsync();

        _isSeeded = true;
    }

    private async Task SeedTenantsAsync()
    {
        // ITenantManager.CreateAsync constructs + validates uniqueness, it does not
        // itself insert the Tenant into SaasTenants. Explicitly InsertAsync after
        // each CreateAsync so the row exists before downstream FKs reference it.
        using (_currentTenant.Change(null))
        {
            var tenantA = await _tenantManager.CreateAsync(TenantsTestData.TenantAName);
            await _tenantRepository.InsertAsync(tenantA);
            TenantsTestData.TenantARef = tenantA.Id;

            var tenantB = await _tenantManager.CreateAsync(TenantsTestData.TenantBName);
            await _tenantRepository.InsertAsync(tenantB);
            TenantsTestData.TenantBRef = tenantB.Id;
        }
    }

    private async Task SeedLocationsAsync()
    {
        // Host-scoped seeds: Location, State, AppointmentType are all !IMultiTenant.
        // Location1 references State1 + AppointmentType1 so nav-prop join tests can
        // assert on populated related entities. Location2 and Location3 keep null
        // nav FKs to exercise LEFT JOIN with nulls and satisfy the "varied FKs"
        // clause in the PR-1D T5 plan.
        //
        // Runs before Doctor / Patient so the orchestrator's dependency chain
        // stays consistent for future Wave-2 seeds (DoctorAvailability and
        // Appointment both FK into Location).
        using (_currentTenant.Change(null))
        {
            await _stateRepository.InsertAsync(new State(
                id: LocationsTestData.State1Id,
                name: LocationsTestData.State1Name));

            await _appointmentTypeRepository.InsertAsync(new AppointmentType(
                id: LocationsTestData.AppointmentType1Id,
                name: LocationsTestData.AppointmentType1Name));

            await _locationRepository.InsertAsync(new Location(
                id: LocationsTestData.Location1Id,
                stateId: LocationsTestData.State1Id,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                name: LocationsTestData.Location1Name,
                parkingFee: LocationsTestData.Location1ParkingFee,
                isActive: LocationsTestData.Location1IsActive,
                address: LocationsTestData.Location1Address,
                city: LocationsTestData.Location1City,
                zipCode: LocationsTestData.Location1ZipCode));

            await _locationRepository.InsertAsync(new Location(
                id: LocationsTestData.Location2Id,
                stateId: null,
                appointmentTypeId: null,
                name: LocationsTestData.Location2Name,
                parkingFee: LocationsTestData.Location2ParkingFee,
                isActive: LocationsTestData.Location2IsActive,
                address: LocationsTestData.Location2Address,
                city: LocationsTestData.Location2City,
                zipCode: LocationsTestData.Location2ZipCode));

            await _locationRepository.InsertAsync(new Location(
                id: LocationsTestData.Location3Id,
                stateId: null,
                appointmentTypeId: null,
                name: LocationsTestData.Location3Name,
                parkingFee: LocationsTestData.Location3ParkingFee,
                isActive: LocationsTestData.Location3IsActive,
                address: LocationsTestData.Location3Address,
                city: LocationsTestData.Location3City,
                zipCode: LocationsTestData.Location3ZipCode));
        }
    }

    private async Task SeedDoctorsAsync()
    {
        // Both doctors scoped to TenantA -- reflects the realistic case of a
        // practice with multiple practitioners. Doctor2 was previously planned
        // for TenantB but this complicates host-context tests unnecessarily:
        // Patient tests don't depend on Doctor tenancy, and keeping the tenant
        // model "two-tenant for Patients, one-tenant for Doctors" keeps existing
        // Doctor tests workable with a single tenant-context wrap.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _doctorRepository.InsertAsync(new Doctor(
                id: DoctorsTestData.Doctor1Id,
                firstName: DoctorsTestData.Doctor1FirstName,
                lastName: DoctorsTestData.Doctor1LastName,
                email: DoctorsTestData.Doctor1Email,
                gender: default,
                identityUserId: IdentityUsersTestData.Doctor1UserId));

            await _doctorRepository.InsertAsync(new Doctor(
                id: DoctorsTestData.Doctor2Id,
                firstName: DoctorsTestData.Doctor2FirstName,
                lastName: DoctorsTestData.Doctor2LastName,
                email: DoctorsTestData.Doctor2Email,
                gender: default,
                identityUserId: IdentityUsersTestData.Doctor2UserId));
        }
    }

    private async Task SeedPatientsAsync()
    {
        await _patientRepository.InsertAsync(new Patient(
            id: PatientsTestData.Patient1Id,
            stateId: null,
            appointmentLanguageId: null,
            identityUserId: IdentityUsersTestData.Patient1UserId,
            tenantId: TenantsTestData.TenantARef,
            firstName: PatientsTestData.Patient1FirstName,
            lastName: PatientsTestData.Patient1LastName,
            email: PatientsTestData.Patient1Email,
            genderId: (Gender)PatientsTestData.PatientGenderIdValue,
            dateOfBirth: PatientsTestData.FixedDateOfBirth,
            phoneNumberTypeId: (PhoneNumberType)PatientsTestData.PatientPhoneNumberTypeIdValue,
            address: PatientsTestData.Patient1Address,
            city: PatientsTestData.Patient1City,
            zipCode: PatientsTestData.Patient1ZipCode,
            socialSecurityNumber: PatientsTestData.Patient1SocialSecurityNumber));

        await _patientRepository.InsertAsync(new Patient(
            id: PatientsTestData.Patient2Id,
            stateId: null,
            appointmentLanguageId: null,
            identityUserId: IdentityUsersTestData.Patient2UserId,
            tenantId: TenantsTestData.TenantBRef,
            firstName: PatientsTestData.Patient2FirstName,
            lastName: PatientsTestData.Patient2LastName,
            email: PatientsTestData.Patient2Email,
            genderId: (Gender)PatientsTestData.PatientGenderIdValue,
            dateOfBirth: PatientsTestData.FixedDateOfBirth,
            phoneNumberTypeId: (PhoneNumberType)PatientsTestData.PatientPhoneNumberTypeIdValue));
    }
}
