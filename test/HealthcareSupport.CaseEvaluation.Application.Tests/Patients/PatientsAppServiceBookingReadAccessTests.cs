using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// Who may READ a patient through <see cref="PatientsAppService.GetPatientForAppointmentBookingAsync"/>,
/// the booking wizard's by-id route: staff, the patient's own login, or a party to one of the patient's
/// appointments under the typeahead's rule (<see cref="BookingPartyAppointments"/>). It used to admit
/// any signed-in caller.
/// </summary>
/// <remarks>
/// <para><b>What this harness can and cannot prove.</b> The test module installs
/// <c>AddAlwaysAllowAuthorization()</c>, so the staff PERMISSION half (<c>CaseEvaluation.Patients</c>)
/// cannot fail here; it is proven on the real pipeline in <c>PatientBookingReadPermissionTests</c>. The
/// OWNERSHIP half reads the caller's roles, id and email from the principal and the party links from
/// the database, so it genuinely runs here.</para>
///
/// <para>Every refusal targets a record that EXISTS (Patient1, office A), so a deleted rule cannot pass
/// by failing to find it. The seed links ApplicantAttorney1 to Appointment1 as its applicant attorney,
/// which is a real party under the typeahead's rule. All data is synthetic.</para>
/// </remarks>
public abstract class PatientsAppServiceBookingReadAccessTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Staff = "Staff Supervisor";
    private const string ApplicantAttorney = "Applicant Attorney";
    private const string DefenseAttorney = "Defense Attorney";
    private const string ClaimExaminer = "Claim Examiner";
    private const string NamedExaminerEmail = "TEST-named-examiner@test.local";
    private static readonly Guid StaffUserId = new("7e1a0c11-0000-4000-9000-0000000099f1");

    private readonly IPatientsAppService _patients;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    protected PatientsAppServiceBookingReadAccessTests()
    {
        _patients = GetRequiredService<IPatientsAppService>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    // ------------------------------------------------------------------ refused

    [Fact]
    public async Task An_attorney_who_is_not_a_party_cannot_read_the_patient()
    {
        await Should.ThrowAsync<AbpAuthorizationException>(() => ReadAs(
            Guid.NewGuid(), null, ApplicantAttorney, PatientsTestData.Patient1Id));
    }

    /// <summary>
    /// From outside, a missing id and a real record the caller may not read must look the same: same
    /// exception type, same message. A not-found for the missing id would let a caller probe which
    /// ids are real.
    /// </summary>
    [Fact]
    public async Task The_refusal_for_a_missing_id_is_identical_to_the_refusal_for_someone_elses_record()
    {
        var stranger = Guid.NewGuid();

        var forRealRecord = await CaptureAsync(() => ReadAs(stranger, null, ApplicantAttorney, PatientsTestData.Patient1Id));
        var forMissingId = await CaptureAsync(() => ReadAs(stranger, null, ApplicantAttorney, Guid.NewGuid()));

        forRealRecord.ShouldBeOfType<AbpAuthorizationException>();
        forMissingId.GetType().ShouldBe(forRealRecord.GetType());
        forMissingId.Message.ShouldBe(forRealRecord.Message);
    }

    [Fact]
    public async Task A_patient_cannot_read_another_patients_record()
    {
        await Should.ThrowAsync<AbpAuthorizationException>(() => ReadAs(
            Guid.NewGuid(), null, "Patient", PatientsTestData.Patient1Id));
    }

    /// <summary>
    /// The attorney link the seed creates is an APPLICANT attorney link. The same user calling as a
    /// Defense Attorney is judged by the defense rule, which that link does not satisfy.
    /// </summary>
    [Fact]
    public async Task An_applicant_attorney_link_does_not_admit_a_defense_attorney()
    {
        await Should.ThrowAsync<AbpAuthorizationException>(() => ReadAs(
            IdentityUsersTestData.ApplicantAttorney1UserId, IdentityUsersTestData.ApplicantAttorney1Email,
            DefenseAttorney, PatientsTestData.Patient1Id));
    }

    [Fact]
    public async Task A_claim_examiner_not_named_on_the_appointment_cannot_read_the_patient()
    {
        await NameTheClaimExaminerOnAppointment1Async(NamedExaminerEmail);

        await Should.ThrowAsync<AbpAuthorizationException>(() => ReadAs(
            Guid.NewGuid(), "TEST-other-examiner@test.local", ClaimExaminer, PatientsTestData.Patient1Id));
    }

    // ------------------------------------------------------------------ admitted

    [Fact]
    public async Task The_linked_applicant_attorney_can_read_the_patient_with_the_ssn_masked()
    {
        var dto = await ReadAs(
            IdentityUsersTestData.ApplicantAttorney1UserId, IdentityUsersTestData.ApplicantAttorney1Email,
            ApplicantAttorney, PatientsTestData.Patient1Id);

        dto.Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
        dto.Patient.SocialSecurityNumber.ShouldNotBe(PatientsTestData.Patient1SocialSecurityNumber);
    }

    [Fact]
    public async Task A_defense_attorney_who_booked_the_appointment_can_read_the_patient()
    {
        var booker = Guid.NewGuid();
        await SetAppointment1BookerAsync(booker);

        var dto = await ReadAs(booker, null, DefenseAttorney, PatientsTestData.Patient1Id);

        dto.Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
    }

    [Fact]
    public async Task The_named_claim_examiner_can_read_the_patient_whatever_the_email_case()
    {
        await NameTheClaimExaminerOnAppointment1Async(NamedExaminerEmail);

        var dto = await ReadAs(Guid.NewGuid(), NamedExaminerEmail.ToUpperInvariant(), ClaimExaminer, PatientsTestData.Patient1Id);

        dto.Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
    }

    [Fact]
    public async Task The_patients_own_login_can_read_their_record()
    {
        var dto = await ReadAs(IdentityUsersTestData.Patient1UserId, null, "Patient", PatientsTestData.Patient1Id);

        dto.Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
    }

    [Fact]
    public async Task Staff_can_read_any_patient_in_their_office()
    {
        var dto = await ReadAs(StaffUserId, null, Staff, PatientsTestData.Patient1Id);

        dto.Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
    }

    // ------------------------------------------------------------------ harness

    private Task<PatientWithNavigationPropertiesDto> ReadAs(Guid userId, string? email, string role, Guid patientId) =>
        As(TenantsTestData.TenantARef, userId, email, role, () => _patients.GetPatientForAppointmentBookingAsync(patientId));

    private async Task<T> As<T>(Guid? tenantId, Guid userId, string? email, string role, Func<Task<T>> call)
    {
        using (_currentTenant.Change(tenantId))
        using (WithCurrentUser.RunWithEmail(_principal, userId, email, role))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    private static async Task<Exception> CaptureAsync(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (Exception ex)
        {
            return ex;
        }

        throw new ShouldAssertException("the call was expected to throw, and returned normally");
    }

    private Task NameTheClaimExaminerOnAppointment1Async(string email) =>
        As(TenantsTestData.TenantARef, StaffUserId, null, Staff, async () =>
        {
            var appointment = await _appointmentRepository.GetAsync(AppointmentsTestData.Appointment1Id);
            appointment.ClaimExaminerEmail = email;
            await _appointmentRepository.UpdateAsync(appointment, autoSave: true);
            return true;
        });

    /// <summary>
    /// The party rule reads <c>CreatorId ?? BookedByUserId</c>, and the seeded appointment has no
    /// creator stamp, so its booker is the field that decides.
    /// </summary>
    private Task SetAppointment1BookerAsync(Guid booker) =>
        As(TenantsTestData.TenantARef, StaffUserId, null, Staff, async () =>
        {
            var appointment = await _appointmentRepository.GetAsync(AppointmentsTestData.Appointment1Id);
            appointment.CreatorId.ShouldBeNull("the booker test assumes the seeded appointment has no creator stamp");
            appointment.BookedByUserId = booker;
            await _appointmentRepository.UpdateAsync(appointment, autoSave: true);
            return true;
        });
}
