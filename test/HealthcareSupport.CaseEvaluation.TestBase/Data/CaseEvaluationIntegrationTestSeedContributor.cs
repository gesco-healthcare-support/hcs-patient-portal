using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
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
///   Tenant -&gt; IdentityUser -&gt; Location (+ State, AppointmentType) -&gt; DoctorAvailability -&gt; Doctor -&gt; Patient -&gt; ApplicantAttorney -&gt; Appointment
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
    private readonly IApplicantAttorneyRepository _applicantAttorneyRepository;
    private readonly IDoctorAvailabilityRepository _doctorAvailabilityRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IAppointmentAccessorRepository _appointmentAccessorRepository;
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
        IApplicantAttorneyRepository applicantAttorneyRepository,
        IDoctorAvailabilityRepository doctorAvailabilityRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IAppointmentAccessorRepository appointmentAccessorRepository,
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
        _applicantAttorneyRepository = applicantAttorneyRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _appointmentRepository = appointmentRepository;
        _appointmentAccessorRepository = appointmentAccessorRepository;
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

        await SeedDoctorAvailabilitiesAsync();
        await _unitOfWorkManager.Current!.SaveChangesAsync();

        await SeedDoctorsAsync();
        await SeedPatientsAsync();
        await SeedApplicantAttorneysAsync();
        await _unitOfWorkManager.Current!.SaveChangesAsync();

        await SeedAppointmentsAsync();
        await _unitOfWorkManager.Current!.SaveChangesAsync();

        await SeedAppointmentAccessorsAsync();
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

    private async Task SeedApplicantAttorneysAsync()
    {
        // ApplicantAttorney is IMultiTenant. Two attorneys seeded so tests can
        // exercise tenant isolation (the contrast case vs Patient's non-IMultiTenant
        // leak at docs/issues/INCOMPLETE-FEATURES.md FEAT-09):
        //   Attorney1 -- TenantARef, IdentityUserId = ApplicantAttorney1UserId
        //   Attorney2 -- TenantBRef, IdentityUserId = DefenseAttorney1UserId
        // IdentityUserId FK integrity at DB level is tenant-agnostic; ABP's
        // IMultiTenant filter acts on ApplicantAttorney.TenantId which is set
        // by the CurrentTenant context at insert time.
        //
        // The ctor sets FirmName + FirmAddress + PhoneNumber (3 fields via args).
        // ApplicantAttorneyManager normally assigns WebAddress + FaxNumber +
        // Street + City + ZipCode (5 fields) post-construction on the entity.
        // The seed assigns both paths directly so tests can assert against the
        // ctor path AND the manager-post-construction path end-to-end.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var attorney1 = new ApplicantAttorney(
                id: ApplicantAttorneysTestData.Attorney1Id,
                stateId: null,
                identityUserId: IdentityUsersTestData.ApplicantAttorney1UserId,
                firmName: ApplicantAttorneysTestData.Attorney1FirmName,
                firmAddress: ApplicantAttorneysTestData.Attorney1FirmAddress,
                phoneNumber: ApplicantAttorneysTestData.Attorney1PhoneNumber);
            attorney1.WebAddress = ApplicantAttorneysTestData.Attorney1WebAddress;
            attorney1.FaxNumber = ApplicantAttorneysTestData.Attorney1FaxNumber;
            attorney1.Street = ApplicantAttorneysTestData.Attorney1Street;
            attorney1.City = ApplicantAttorneysTestData.Attorney1City;
            attorney1.ZipCode = ApplicantAttorneysTestData.Attorney1ZipCode;
            await _applicantAttorneyRepository.InsertAsync(attorney1);
        }

        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            var attorney2 = new ApplicantAttorney(
                id: ApplicantAttorneysTestData.Attorney2Id,
                stateId: null,
                identityUserId: IdentityUsersTestData.DefenseAttorney1UserId,
                firmName: ApplicantAttorneysTestData.Attorney2FirmName,
                firmAddress: ApplicantAttorneysTestData.Attorney2FirmAddress,
                phoneNumber: ApplicantAttorneysTestData.Attorney2PhoneNumber);
            attorney2.WebAddress = ApplicantAttorneysTestData.Attorney2WebAddress;
            attorney2.FaxNumber = ApplicantAttorneysTestData.Attorney2FaxNumber;
            attorney2.Street = ApplicantAttorneysTestData.Attorney2Street;
            attorney2.City = ApplicantAttorneysTestData.Attorney2City;
            attorney2.ZipCode = ApplicantAttorneysTestData.Attorney2ZipCode;
            await _applicantAttorneyRepository.InsertAsync(attorney2);
        }
    }

    private async Task SeedDoctorAvailabilitiesAsync()
    {
        // DoctorAvailability is IMultiTenant. Three slots seeded across TenantA+B
        // so Tier-2 entities (AppointmentAccessors, AppointmentApplicantAttorneys,
        // AppointmentEmployerDetails) have valid Appointment FK targets, and so
        // future DoctorAvailability Wave-2 CRUD tests have slot rows to assert
        // against. Slot1 + Slot3 are Booked (referenced by Appointment1 + 2);
        // Slot2 is Available (reserved as a free slot for booking-flow tests).
        //
        // Runs AFTER SeedLocationsAsync (LocationId + AppointmentTypeId FKs
        // satisfied) and BEFORE SeedAppointmentsAsync (Appointment's
        // DoctorAvailabilityId FK depends on these slots).
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _doctorAvailabilityRepository.InsertAsync(new DoctorAvailability(
                id: DoctorAvailabilitiesTestData.Slot1Id,
                locationId: LocationsTestData.Location1Id,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                availableDate: DoctorAvailabilitiesTestData.Slot1AvailableDate,
                fromTime: DoctorAvailabilitiesTestData.Slot1FromTime,
                toTime: DoctorAvailabilitiesTestData.Slot1ToTime,
                bookingStatusId: DoctorAvailabilitiesTestData.Slot1BookingStatus));

            await _doctorAvailabilityRepository.InsertAsync(new DoctorAvailability(
                id: DoctorAvailabilitiesTestData.Slot2Id,
                locationId: LocationsTestData.Location2Id,
                appointmentTypeId: null,
                availableDate: DoctorAvailabilitiesTestData.Slot2AvailableDate,
                fromTime: DoctorAvailabilitiesTestData.Slot2FromTime,
                toTime: DoctorAvailabilitiesTestData.Slot2ToTime,
                bookingStatusId: DoctorAvailabilitiesTestData.Slot2BookingStatus));
        }

        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            await _doctorAvailabilityRepository.InsertAsync(new DoctorAvailability(
                id: DoctorAvailabilitiesTestData.Slot3Id,
                locationId: LocationsTestData.Location1Id,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                availableDate: DoctorAvailabilitiesTestData.Slot3AvailableDate,
                fromTime: DoctorAvailabilitiesTestData.Slot3FromTime,
                toTime: DoctorAvailabilitiesTestData.Slot3ToTime,
                bookingStatusId: DoctorAvailabilitiesTestData.Slot3BookingStatus));
        }
    }

    private async Task SeedAppointmentsAsync()
    {
        // Appointment is IMultiTenant. Two appointments seeded so Tier-2 entities
        // that FK into Appointment (AppointmentAccessors,
        // AppointmentApplicantAttorneys, AppointmentEmployerDetails) have valid
        // AppointmentId targets. Minimum viable seed -- no PanelNumber and no
        // DueDate; status + confirmation number hardcoded in AppointmentsTestData.
        //
        // Runs AFTER SeedPatientsAsync + SeedApplicantAttorneysAsync so the
        // Patient + IdentityUser FKs resolve, and AFTER SeedDoctorAvailabilitiesAsync
        // so the DoctorAvailabilityId FK resolves.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _appointmentRepository.InsertAsync(new Appointment(
                id: AppointmentsTestData.Appointment1Id,
                patientId: PatientsTestData.Patient1Id,
                identityUserId: IdentityUsersTestData.Patient1UserId,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                appointmentDate: AppointmentsTestData.Appointment1Date,
                requestConfirmationNumber: AppointmentsTestData.Appointment1RequestConfirmationNumber,
                appointmentStatus: AppointmentsTestData.Appointment1Status));
        }

        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            await _appointmentRepository.InsertAsync(new Appointment(
                id: AppointmentsTestData.Appointment2Id,
                patientId: PatientsTestData.Patient2Id,
                identityUserId: IdentityUsersTestData.Patient2UserId,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot3Id,
                appointmentDate: AppointmentsTestData.Appointment2Date,
                requestConfirmationNumber: AppointmentsTestData.Appointment2RequestConfirmationNumber,
                appointmentStatus: AppointmentsTestData.Appointment2Status));
        }
    }

    private async Task SeedAppointmentAccessorsAsync()
    {
        // AppointmentAccessor is IMultiTenant. Two accessors seeded across the
        // two tenants so Tier-2 tests can assert multi-tenant isolation AND the
        // View/Edit AccessType split:
        //   Accessor1 -- TenantA, Appointment1, ApplicantAttorney1UserId, View
        //   Accessor2 -- TenantB, Appointment2, DefenseAttorney1UserId, Edit
        // Runs after SeedAppointmentsAsync so Appointment1/2 FKs are satisfied.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _appointmentAccessorRepository.InsertAsync(new AppointmentAccessor(
                id: AppointmentAccessorsTestData.Accessor1Id,
                identityUserId: IdentityUsersTestData.ApplicantAttorney1UserId,
                appointmentId: AppointmentsTestData.Appointment1Id,
                accessTypeId: AppointmentAccessorsTestData.Accessor1AccessType));
        }

        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            await _appointmentAccessorRepository.InsertAsync(new AppointmentAccessor(
                id: AppointmentAccessorsTestData.Accessor2Id,
                identityUserId: IdentityUsersTestData.DefenseAttorney1UserId,
                appointmentId: AppointmentsTestData.Appointment2Id,
                accessTypeId: AppointmentAccessorsTestData.Accessor2AccessType));
        }
    }
}
