using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.States;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// The read paths and guards of <see cref="PatientsAppService"/> that <c>PatientsAppServiceTests</c>
/// does not reach: the service-level by-id reads (office and host), the booking deduplication
/// match, the SSN reveal and its refusal, the lookups, the in-use delete guard, and the
/// own-profile lookups for a caller with no patient record.
/// </summary>
/// <remarks>
/// Every refusal below runs with the thing it refuses PRESENT: office B's patient exists when office
/// A asks for it, the patient has an SSN when a stranger asks to reveal it, the patient has an
/// appointment when it is deleted, and seeded patients exist when a caller with none asks for their
/// own. A refusal against an empty table would pass with the guard removed. All data is synthetic.
/// </remarks>
public abstract class PatientsAppServiceReadPathTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Staff = "Staff Supervisor";
    private static readonly Guid StaffUserId = new("7e1a0c11-0000-4000-9000-000000000001");

    private readonly IPatientsAppService _patients;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    protected PatientsAppServiceReadPathTests()
    {
        _patients = GetRequiredService<IPatientsAppService>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    // ------------------------------------------------------------------ harness

    private async Task<T> As<T>(Guid? tenantId, Guid userId, string role, Func<Task<T>> call)
    {
        using (_currentTenant.Change(tenantId))
        using (WithCurrentUser.Run(_principal, userId, role))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    private static string Last4(string ssn) => ssn[^4..];

    // ------------------------------------------------------------------ by-id reads

    [Fact]
    public async Task Office_staff_read_their_own_patient_masked_and_not_another_offices()
    {
        var own = await As(TenantsTestData.TenantARef, StaffUserId, Staff,
            () => _patients.GetWithNavigationPropertiesAsync(PatientsTestData.Patient1Id));
        var other = await As(TenantsTestData.TenantARef, StaffUserId, Staff,
            async () => (PatientWithNavigationPropertiesDto?)await _patients.GetWithNavigationPropertiesAsync(PatientsTestData.Patient2Id));

        own.Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
        own.Patient.SocialSecurityNumber.ShouldNotBe(PatientsTestData.Patient1SocialSecurityNumber);
        own.Patient.SocialSecurityNumber!.ShouldEndWith(Last4(PatientsTestData.Patient1SocialSecurityNumber));
        (other?.Patient).ShouldBeNull();
    }

    [Fact]
    public async Task The_host_reads_a_patient_from_any_office()
    {
        var fromB = await As(null, StaffUserId, Staff,
            () => _patients.GetWithNavigationPropertiesAsync(PatientsTestData.Patient2Id));
        var forBooking = await As(null, StaffUserId, Staff,
            () => _patients.GetPatientForAppointmentBookingAsync(PatientsTestData.Patient1Id));

        fromB.Patient.Id.ShouldBe(PatientsTestData.Patient2Id);
        forBooking.Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
        forBooking.Patient.SocialSecurityNumber.ShouldNotBe(PatientsTestData.Patient1SocialSecurityNumber);
    }

    // ------------------------------------------------------------------ booking

    [Fact]
    public async Task A_booking_that_matches_an_existing_patient_on_three_fields_reuses_that_patient()
    {
        var before = await As(TenantsTestData.TenantARef, StaffUserId, Staff, () => _patientRepository.GetCountAsync());

        var result = await As(TenantsTestData.TenantARef, StaffUserId, Staff,
            () => _patients.GetOrCreatePatientForAppointmentBookingAsync(new CreatePatientForAppointmentBookingInput
            {
                FirstName = "Synthetic",
                LastName = PatientsTestData.Patient1LastName,
                DateOfBirth = PatientsTestData.FixedDateOfBirth,
                SocialSecurityNumber = PatientsTestData.Patient1SocialSecurityNumber,
            }));

        result.IsExisting.ShouldBeTrue();
        result.Patient.Id.ShouldBe(PatientsTestData.Patient1Id);
        result.Patient.SocialSecurityNumber.ShouldNotBe(PatientsTestData.Patient1SocialSecurityNumber);
        (await As(TenantsTestData.TenantARef, StaffUserId, Staff, () => _patientRepository.GetCountAsync())).ShouldBe(before);
    }

    [Fact]
    public async Task Updating_a_patient_that_does_not_exist_for_a_booking_is_refused()
    {
        await Should.ThrowAsync<EntityNotFoundException>(() => As(TenantsTestData.TenantARef, StaffUserId, Staff,
            () => _patients.UpdatePatientForAppointmentBookingAsync(Guid.NewGuid(), new PatientUpdateDto
            {
                FirstName = "Synthetic",
                LastName = "Patient",
                Email = "synthetic.update@example.test",
                DateOfBirth = PatientsTestData.FixedDateOfBirth,
            })));
    }

    // ------------------------------------------------------------------ SSN reveal

    [Fact]
    public async Task The_patient_themselves_can_reveal_their_full_ssn_and_another_external_user_cannot()
    {
        var own = await As(TenantsTestData.TenantARef, IdentityUsersTestData.Patient1UserId, IdentityUsersTestData.PatientRoleName,
            () => _patients.GetFullSsnAsync(PatientsTestData.Patient1Id));

        own.SocialSecurityNumber.ShouldBe(PatientsTestData.Patient1SocialSecurityNumber);
        await Should.ThrowAsync<AbpAuthorizationException>(() => As(TenantsTestData.TenantARef,
            IdentityUsersTestData.ApplicantAttorney1UserId, IdentityUsersTestData.ApplicantAttorneyRoleName,
            () => _patients.GetFullSsnAsync(PatientsTestData.Patient1Id)));
    }

    // ------------------------------------------------------------------ lookups

    [Fact]
    public async Task The_lookups_return_only_what_matches_the_filter()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var userId = Guid.NewGuid();
        await As(TenantsTestData.TenantARef, StaffUserId, Staff, async () =>
        {
            await GetRequiredService<IRepository<State, Guid>>().InsertAsync(new State(Guid.NewGuid(), $"Synthetic State {token}"), autoSave: true);
            await GetRequiredService<IRepository<State, Guid>>().InsertAsync(new State(Guid.NewGuid(), "Synthetic Other State"), autoSave: true);
            await GetRequiredService<IRepository<AppointmentLanguage, Guid>>().InsertAsync(new AppointmentLanguage(Guid.NewGuid(), $"Synthetic Language {token}"), autoSave: true);
            var user = new IdentityUser(userId, $"lookup-{token}", $"lookup-{token}@example.test", TenantsTestData.TenantARef) { Name = $"Synthetic{token}" };
            (await GetRequiredService<IdentityUserManager>().CreateAsync(user)).Succeeded.ShouldBeTrue();
            return true;
        });

        var states = await As(TenantsTestData.TenantARef, StaffUserId, Staff,
            () => _patients.GetStateLookupAsync(new LookupRequestDto { Filter = token, MaxResultCount = 10 }));
        var languages = await As(TenantsTestData.TenantARef, StaffUserId, Staff,
            () => _patients.GetAppointmentLanguageLookupAsync(new LookupRequestDto { Filter = token, MaxResultCount = 10 }));
        var users = await As(TenantsTestData.TenantARef, StaffUserId, Staff,
            () => _patients.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = token, MaxResultCount = 10 }));
        var offices = await As(null, StaffUserId, Staff,
            () => _patients.GetTenantLookupAsync(new LookupRequestDto { Filter = TenantsTestData.TenantAName, MaxResultCount = 10 }));

        states.TotalCount.ShouldBe(1);
        states.Items.ShouldHaveSingleItem().DisplayName.ShouldBe($"Synthetic State {token}");
        languages.Items.ShouldHaveSingleItem().DisplayName.ShouldBe($"Synthetic Language {token}");
        // Matched on Name; the lookup displays the address.
        users.Items.ShouldHaveSingleItem().Id.ShouldBe(userId);
        offices.Items.ShouldHaveSingleItem().Id.ShouldBe(TenantsTestData.TenantARef);
    }

    // ------------------------------------------------------------------ guards

    [Fact]
    public async Task A_patient_with_an_appointment_cannot_be_deleted()
    {
        var refused = await Should.ThrowAsync<BusinessException>(() => As(TenantsTestData.TenantARef, StaffUserId, Staff, async () =>
        {
            await _patients.DeleteAsync(PatientsTestData.Patient1Id);
            return true;
        }));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.PatientInUse);
        (await As(TenantsTestData.TenantARef, StaffUserId, Staff,
            () => _patientRepository.FindAsync(PatientsTestData.Patient1Id))).ShouldNotBeNull();
    }

    [Fact]
    public async Task A_caller_with_no_patient_record_has_no_profile_and_an_anonymous_caller_is_refused()
    {
        await Should.ThrowAsync<EntityNotFoundException>(() => As(TenantsTestData.TenantARef,
            IdentityUsersTestData.ApplicantAttorney1UserId, IdentityUsersTestData.PatientRoleName,
            () => _patients.GetMyProfileAsync()));

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (_principal.Change(new ClaimsPrincipal(new ClaimsIdentity())))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => _patients.GetMyProfileAsync()));
        }
    }
}
