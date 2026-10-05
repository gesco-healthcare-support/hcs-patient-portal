using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Patients;
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

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Booking against a SUPPLIED <c>PatientId</c> (<c>AppointmentsAppService.SubmitAsync</c>) must not
/// accept an id the caller is not entitled to. Before the guard the id was used as-is, so any caller
/// who knew or guessed one could book against that patient and become a party to the record.
///
/// <para>Every refusal targets a record that EXISTS (Patient1, office A) and asserts no appointment
/// row was added, so a deleted guard cannot pass by failing to find the patient or by failing later.
/// The rule is the same one the by-id read uses (<see cref="PatientBookingReadAccess"/>), not a second
/// rule. All data is synthetic.</para>
/// </summary>
public abstract class AppointmentsAppServiceSuppliedPatientIdTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private static readonly Guid StaffUserId = new("7e1a0c11-0000-4000-9000-0000000099f2");

    private readonly IAppointmentsAppService _appointments;
    private readonly PatientBookingReadAccess _access;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    protected AppointmentsAppServiceSuppliedPatientIdTests()
    {
        _appointments = GetRequiredService<IAppointmentsAppService>();
        _access = GetRequiredService<PatientBookingReadAccess>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Theory]
    [InlineData("Applicant Attorney")]
    [InlineData("Defense Attorney")]
    [InlineData("Claim Examiner")]
    [InlineData("Patient")]
    public async Task Submit_with_a_patient_id_the_caller_is_not_entitled_to_is_refused_and_writes_nothing(string role)
    {
        var before = await CountAppointmentsAsync();

        await Should.ThrowAsync<AbpAuthorizationException>(() => As(
            Guid.NewGuid(), "TEST-stranger@test.local", role,
            () => _appointments.SubmitAsync(new AppointmentSubmitDto { PatientId = PatientsTestData.Patient1Id })));

        (await CountAppointmentsAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task Submit_refusal_for_a_missing_id_is_identical_to_the_refusal_for_someone_elses_record()
    {
        var stranger = Guid.NewGuid();
        Task Call(Guid id) => As(stranger, null, "Applicant Attorney",
            () => _appointments.SubmitAsync(new AppointmentSubmitDto { PatientId = id }));

        var real = await Should.ThrowAsync<AbpAuthorizationException>(() => Call(PatientsTestData.Patient1Id));
        var missing = await Should.ThrowAsync<AbpAuthorizationException>(() => Call(Guid.NewGuid()));

        missing.Message.ShouldBe(real.Message);
    }

    [Fact]
    public async Task Guard_admits_the_linked_applicant_attorney()
    {
        await As(IdentityUsersTestData.ApplicantAttorney1UserId, IdentityUsersTestData.ApplicantAttorney1Email,
            "Applicant Attorney", () => _access.EnsureCanBookForAsync(PatientsTestData.Patient1Id));
    }

    [Fact]
    public async Task Guard_admits_the_patients_own_login()
    {
        await As(IdentityUsersTestData.Patient1UserId, null, "Patient",
            () => _access.EnsureCanBookForAsync(PatientsTestData.Patient1Id));
    }

    [Fact]
    public async Task Guard_admits_staff_and_keeps_staff_a_not_found_for_a_missing_id()
    {
        await As(StaffUserId, null, "Staff Supervisor",
            () => _access.EnsureCanBookForAsync(PatientsTestData.Patient1Id));

        await Should.ThrowAsync<EntityNotFoundException>(() => As(StaffUserId, null, "Staff Supervisor",
            () => _access.EnsureCanBookForAsync(Guid.NewGuid())));
    }

    private Task<int> CountAppointmentsAsync() =>
        As(StaffUserId, null, "Staff Supervisor", async () => (await _appointmentRepository.GetQueryableAsync()).Count());

    private Task As(Guid userId, string? email, string role, Func<Task> call) =>
        As<bool>(userId, email, role, async () => { await call(); return true; });

    private async Task<T> As<T>(Guid userId, string? email, string role, Func<Task<T>> call)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(_principal, userId, email, role))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }
}
