using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// #598 -- who may edit a patient record through
/// <see cref="PatientsAppService.UpdatePatientForAppointmentBookingAsync"/>: internal staff, or the
/// patient's own login. Nobody else, however they are related to the patient's appointments.
/// </summary>
/// <remarks>
/// <para><b>What this harness can and cannot prove.</b> The test module installs
/// <c>AddAlwaysAllowAuthorization()</c>, so the staff PERMISSION half of the rule
/// (<c>Patients.Edit</c>) cannot fail here -- that half is proven on the real pipeline in
/// <c>PatientBookingUpdateAuthorizationTests</c>. The OWNERSHIP half reads the caller's roles and id
/// from the principal, not from <c>IAuthorizationService</c>, so it is genuinely exercised here.</para>
///
/// <para>Every refusal runs with the refused thing PRESENT and asserts the row is unchanged, read
/// back as staff: a refusal against a missing row would pass with the rule deleted. The party decoy
/// sets up the strongest relationship an external user can hold -- an accessor on the patient's
/// appointment whose attorney email is theirs -- and is still refused. All data is synthetic.</para>
/// </remarks>
public abstract class PatientsAppServiceBookingUpdateAccessTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Staff = "Staff Supervisor";
    private const string ApplicantAttorney = "Applicant Attorney";
    private const string EditedCity = "TEST-Edited-City";
    private static readonly Guid StaffUserId = new("7e1a0c11-0000-4000-9000-000000000598");

    private readonly IPatientsAppService _patients;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    protected PatientsAppServiceBookingUpdateAccessTests()
    {
        _patients = GetRequiredService<IPatientsAppService>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    // ------------------------------------------------------------------ refused

    [Fact]
    public async Task A_stranger_cannot_edit_a_claimed_patient()
    {
        var before = await CityAsStaffAsync(TenantsTestData.TenantARef, PatientsTestData.Patient1Id);
        var edit = await EditOfAsync(TenantsTestData.TenantARef, PatientsTestData.Patient1Id);

        await Should.ThrowAsync<AbpAuthorizationException>(() => As(
            TenantsTestData.TenantARef, Guid.NewGuid(), null, ApplicantAttorney,
            () => _patients.UpdatePatientForAppointmentBookingAsync(
                PatientsTestData.Patient1Id, edit)));

        (await CityAsStaffAsync(TenantsTestData.TenantARef, PatientsTestData.Patient1Id)).ShouldBe(before);
    }

    /// <summary>
    /// Booking creates record-only patients with no login, so unclaimed rows are the common case. An
    /// unclaimed record has no owner, and an external caller must not be treated as one.
    /// </summary>
    [Fact]
    public async Task A_stranger_cannot_edit_an_unclaimed_patient()
    {
        var unclaimed = await InsertUnclaimedPatientAsync(TenantsTestData.TenantARef);
        var edit = await EditOfAsync(TenantsTestData.TenantARef, unclaimed);

        await Should.ThrowAsync<AbpAuthorizationException>(() => As(
            TenantsTestData.TenantARef, Guid.NewGuid(), null, ApplicantAttorney,
            () => _patients.UpdatePatientForAppointmentBookingAsync(unclaimed, edit)));

        (await CityAsStaffAsync(TenantsTestData.TenantARef, unclaimed)).ShouldBe("TEST-Unclaimed-City");
    }

    /// <summary>
    /// The DECOY with the relationship PRESENT. ApplicantAttorney1 is seeded as a View accessor on
    /// Appointment1 (Patient1's appointment), and here Appointment1 also names their email as the
    /// applicant attorney -- the two relationships an external user can create by booking. A rule
    /// that admitted parties would let this caller through; this one must not.
    /// </summary>
    [Fact]
    public async Task A_party_to_the_patients_appointment_still_cannot_edit_the_patient()
    {
        await NameTheAttorneyOnAppointment1Async();
        var before = await CityAsStaffAsync(TenantsTestData.TenantARef, PatientsTestData.Patient1Id);
        var edit = await EditOfAsync(TenantsTestData.TenantARef, PatientsTestData.Patient1Id);

        await Should.ThrowAsync<AbpAuthorizationException>(() => As(
            TenantsTestData.TenantARef,
            IdentityUsersTestData.ApplicantAttorney1UserId,
            IdentityUsersTestData.ApplicantAttorney1Email,
            ApplicantAttorney,
            () => _patients.UpdatePatientForAppointmentBookingAsync(
                PatientsTestData.Patient1Id, edit)));

        (await CityAsStaffAsync(TenantsTestData.TenantARef, PatientsTestData.Patient1Id)).ShouldBe(before);
    }

    // ------------------------------------------------------------------ allowed

    [Fact]
    public async Task The_patients_own_login_can_edit_their_record()
    {
        var edit = await EditOfAsync(TenantsTestData.TenantARef, PatientsTestData.Patient1Id);

        await As(TenantsTestData.TenantARef, IdentityUsersTestData.Patient1UserId, null, "Patient",
            () => _patients.UpdatePatientForAppointmentBookingAsync(
                PatientsTestData.Patient1Id, edit));

        (await CityAsStaffAsync(TenantsTestData.TenantARef, PatientsTestData.Patient1Id)).ShouldBe(EditedCity);
    }

    [Fact]
    public async Task Staff_can_edit_any_patient_in_their_office()
    {
        var edit = await EditOfAsync(TenantsTestData.TenantARef, PatientsTestData.Patient1Id);

        await As(TenantsTestData.TenantARef, StaffUserId, null, Staff,
            () => _patients.UpdatePatientForAppointmentBookingAsync(
                PatientsTestData.Patient1Id, edit));

        (await CityAsStaffAsync(TenantsTestData.TenantARef, PatientsTestData.Patient1Id)).ShouldBe(EditedCity);
    }

    /// <summary>
    /// The OFFICE decoy: staff in office A aim at a patient that exists only in office B. Passes
    /// before and after #598; a regression pin that the office boundary still holds for staff.
    /// </summary>
    [Fact]
    public async Task Staff_cannot_reach_a_patient_in_another_office()
    {
        var before = await CityAsStaffAsync(TenantsTestData.TenantBRef, PatientsTestData.Patient2Id);
        var edit = await EditOfAsync(TenantsTestData.TenantBRef, PatientsTestData.Patient2Id);

        await Should.ThrowAsync<EntityNotFoundException>(() => As(
            TenantsTestData.TenantARef, StaffUserId, null, Staff,
            () => _patients.UpdatePatientForAppointmentBookingAsync(
                PatientsTestData.Patient2Id, edit)));

        (await CityAsStaffAsync(TenantsTestData.TenantBRef, PatientsTestData.Patient2Id)).ShouldBe(before);
    }

    // ------------------------------------------------------------------ harness

    private async Task<T> As<T>(Guid? tenantId, Guid userId, string? email, string role, Func<Task<T>> call)
    {
        using (_currentTenant.Change(tenantId))
        using (WithCurrentUser.RunWithEmail(_principal, userId, email, role))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    private Task<string?> CityAsStaffAsync(Guid tenantId, Guid patientId) =>
        As(tenantId, StaffUserId, null, Staff, async () => (await _patientRepository.GetAsync(patientId)).City);

    /// <summary>
    /// An edit that changes only the city. Name and email are resent unchanged because the DTO
    /// requires them; the city is the marker the assertions read.
    /// </summary>
    private Task<PatientUpdateDto> EditOfAsync(Guid tenantId, Guid patientId) =>
        As(tenantId, StaffUserId, null, Staff, async () =>
        {
            var patient = await _patientRepository.GetAsync(patientId);
            return new PatientUpdateDto
            {
                FirstName = patient.FirstName,
                LastName = patient.LastName,
                Email = patient.Email,
                City = EditedCity,
            };
        });

    private Task<Guid> InsertUnclaimedPatientAsync(Guid tenantId) =>
        As(tenantId, StaffUserId, null, Staff, async () =>
        {
            var patient = new Patient(
                id: Guid.NewGuid(),
                stateId: null,
                appointmentLanguageId: null,
                identityUserId: null,
                tenantId: tenantId,
                firstName: "TEST-Unclaimed",
                lastName: "TEST-Record",
                email: "TEST-unclaimed@test.local",
                genderId: Gender.Female,
                dateOfBirth: PatientsTestData.FixedDateOfBirth,
                phoneNumberTypeId: PhoneNumberType.Home,
                city: "TEST-Unclaimed-City");
            await _patientRepository.InsertAsync(patient, autoSave: true);
            return patient.Id;
        });

    private Task NameTheAttorneyOnAppointment1Async() =>
        As(TenantsTestData.TenantARef, StaffUserId, null, Staff, async () =>
        {
            var appointment = await _appointmentRepository.GetAsync(AppointmentsTestData.Appointment1Id);
            appointment.ApplicantAttorneyEmail = IdentityUsersTestData.ApplicantAttorney1Email;
            await _appointmentRepository.UpdateAsync(appointment, autoSave: true);
            return true;
        });
}
