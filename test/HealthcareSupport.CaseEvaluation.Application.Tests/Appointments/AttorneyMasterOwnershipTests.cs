using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-10-23: an EXTERNAL caller (patient, attorney, claim examiner) may not change another firm's
/// attorney record, nor attach an account to it. An attorney record is shared: every appointment that
/// names that attorney points at it, and each new booking copies its details. Before this, a patient
/// booking or editing their own appointment could rewrite any firm's record by id or by its email, and
/// set its account to their own.
///
/// <para>The caller here is the seeded Patient1, a party to Appointment1 through the patient pathway, so
/// every upsert passes the appointment read check and reaches the attorney rules. Attorney1 is a real
/// applicant attorney record linked to ApplicantAttorney1's account. The default test user is an admin,
/// so <see cref="AttorneyUpsertBranchTests{TStartupModule}"/> keeps pinning the unchanged staff
/// behaviour.</para>
/// </summary>
public abstract class AttorneyMasterOwnershipTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentsAppService _appointments;
    private readonly IAppointmentApplicantAttorneysAppService _applicantLinks;
    private readonly IAppointmentDefenseAttorneysAppService _defenseLinks;
    private readonly IApplicantAttorneyRepository _applicantMasters;
    private readonly IDefenseAttorneyRepository _defenseMasters;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    protected AttorneyMasterOwnershipTests()
    {
        _appointments = GetRequiredService<IAppointmentsAppService>();
        _applicantLinks = GetRequiredService<IAppointmentApplicantAttorneysAppService>();
        _defenseLinks = GetRequiredService<IAppointmentDefenseAttorneysAppService>();
        _applicantMasters = GetRequiredService<IApplicantAttorneyRepository>();
        _defenseMasters = GetRequiredService<IDefenseAttorneyRepository>();
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private IDisposable AsPatient() => WithCurrentUser.RunWithEmail(
        _principal, IdentityUsersTestData.Patient1UserId, IdentityUsersTestData.Patient1Email,
        IdentityUsersTestData.PatientRoleName);

    private IDisposable AsTheAttorney() => WithCurrentUser.RunWithEmail(
        _principal, IdentityUsersTestData.ApplicantAttorney1UserId, IdentityUsersTestData.ApplicantAttorney1Email,
        IdentityUsersTestData.ApplicantAttorneyRoleName);

    [Fact]
    public async Task A_patient_naming_another_firms_record_by_id_links_it_but_cannot_change_it()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var before = await _applicantMasters.GetAsync(ApplicantAttorneysTestData.Attorney1Id);

            using (AsPatient())
            {
                await _appointments.UpsertApplicantAttorneyForAppointmentAsync(
                    AppointmentsTestData.Appointment1Id,
                    new ApplicantAttorneyDetailsDto
                    {
                        ApplicantAttorneyId = ApplicantAttorneysTestData.Attorney1Id,
                        IdentityUserId = IdentityUsersTestData.Patient1UserId,
                        Email = IdentityUsersTestData.ApplicantAttorney1Email,
                        FirmName = "TEST Rewritten Firm",
                        PhoneNumber = "555-0911",
                        City = "Rewrittenville",
                    });
            }

            var after = await _applicantMasters.GetAsync(ApplicantAttorneysTestData.Attorney1Id);
            after.FirmName.ShouldBe(before.FirmName, "another party may not rewrite the shared record");
            after.PhoneNumber.ShouldBe(before.PhoneNumber);
            after.City.ShouldBe(before.City);
            after.IdentityUserId.ShouldBe(IdentityUsersTestData.ApplicantAttorney1UserId,
                "the record must stay on the attorney's own account");

            var appointment = await _appointmentRepository.GetAsync(AppointmentsTestData.Appointment1Id);
            appointment.ApplicantAttorneyFirmName.ShouldBe("TEST Rewritten Firm",
                "the appointment's own copy shows what the booker typed");
            appointment.ApplicantAttorneyPhoneNumber.ShouldBe("555-0911");
        }
    }

    [Fact]
    public async Task A_patient_matching_another_firms_record_by_email_only_fills_its_blank_fields()
    {
        const string email = "TEST-aa-owned-elsewhere@test.local";
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            // Staff (the default admin) create the record with a phone and no fax.
            await _appointments.UpsertApplicantAttorneyForAppointmentAsync(
                AppointmentsTestData.Appointment1Id,
                new ApplicantAttorneyDetailsDto
                {
                    Email = email,
                    FirstName = "TESTAA",
                    LastName = "TESTOWNED",
                    FirmName = "TEST Firm Kept",
                    PhoneNumber = "555-0920",
                });

            using (AsPatient())
            {
                await _appointments.UpsertApplicantAttorneyForAppointmentAsync(
                    AppointmentsTestData.Appointment1Id,
                    new ApplicantAttorneyDetailsDto
                    {
                        Email = email,
                        FirstName = "TESTAA",
                        LastName = "TESTOWNED",
                        FirmName = "TEST Firm Replaced",
                        PhoneNumber = "555-0929",
                        FaxNumber = "555-0921",
                    });
            }

            var master = await _applicantMasters.FindByNormalizedEmailAsync(email);
            master!.FirmName.ShouldBe("TEST Firm Kept");
            master.PhoneNumber.ShouldBe("555-0920");
            master.FaxNumber.ShouldBe("555-0921", "a blank field may be filled");

            var appointment = await _appointmentRepository.GetAsync(AppointmentsTestData.Appointment1Id);
            appointment.ApplicantAttorneyFirmName.ShouldBe("TEST Firm Replaced");
            appointment.ApplicantAttorneyPhoneNumber.ShouldBe("555-0929");
        }
    }

    [Fact]
    public async Task A_patient_cannot_attach_their_own_account_to_an_attorney_record()
    {
        const string email = "TEST-aa-unregistered@test.local";
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            using (AsPatient())
            {
                await _appointments.UpsertApplicantAttorneyForAppointmentAsync(
                    AppointmentsTestData.Appointment1Id,
                    new ApplicantAttorneyDetailsDto
                    {
                        IdentityUserId = IdentityUsersTestData.Patient1UserId,
                        Email = email,
                        FirstName = "TESTAA",
                        LastName = "TESTNEW",
                        FirmName = "TEST Firm New",
                    });
            }

            var master = await _applicantMasters.FindByNormalizedEmailAsync(email);
            master.ShouldNotBeNull();
            master!.IdentityUserId.ShouldBeNull("no account has that email, and the patient's own id is not honoured");
        }
    }

    [Fact]
    public async Task An_attorney_can_still_update_their_own_record()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var before = await _applicantMasters.GetAsync(ApplicantAttorneysTestData.Attorney1Id);
            using (AsTheAttorney())
            {
                await _appointments.UpsertApplicantAttorneyForAppointmentAsync(
                    AppointmentsTestData.Appointment1Id,
                    new ApplicantAttorneyDetailsDto
                    {
                        ApplicantAttorneyId = ApplicantAttorneysTestData.Attorney1Id,
                        Email = IdentityUsersTestData.ApplicantAttorney1Email,
                        FirstName = before.FirstName ?? "TESTAA",
                        LastName = before.LastName ?? "TESTOWN",
                        FirmName = before.FirmName,
                        PhoneNumber = "555-0930",
                    });
            }

            (await _applicantMasters.GetAsync(ApplicantAttorneysTestData.Attorney1Id)).PhoneNumber.ShouldBe("555-0930");
        }
    }

    [Fact]
    public async Task The_defense_side_follows_the_same_rules()
    {
        const string email = "TEST-da-owned-elsewhere@test.local";
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _appointments.UpsertDefenseAttorneyForAppointmentAsync(
                AppointmentsTestData.Appointment1Id,
                new DefenseAttorneyDetailsDto
                {
                    Email = email,
                    FirstName = "TESTDA",
                    LastName = "TESTOWNED",
                    FirmName = "TEST Defense Kept",
                    PhoneNumber = "555-0940",
                });
            var seeded = await _defenseMasters.FindByNormalizedEmailAsync(email);

            using (AsPatient())
            {
                await _appointments.UpsertDefenseAttorneyForAppointmentAsync(
                    AppointmentsTestData.Appointment1Id,
                    new DefenseAttorneyDetailsDto
                    {
                        DefenseAttorneyId = seeded!.Id,
                        IdentityUserId = IdentityUsersTestData.Patient1UserId,
                        Email = email,
                        FirstName = "TESTDA",
                        LastName = "TESTOWNED",
                        FirmName = "TEST Defense Replaced",
                        PhoneNumber = "555-0949",
                    });
            }

            var master = await _defenseMasters.GetAsync(seeded.Id);
            master.FirmName.ShouldBe("TEST Defense Kept");
            master.PhoneNumber.ShouldBe("555-0940");
            master.IdentityUserId.ShouldBeNull();

            var appointment = await _appointmentRepository.GetAsync(AppointmentsTestData.Appointment1Id);
            appointment.DefenseAttorneyFirmName.ShouldBe("TEST Defense Replaced");
        }
    }

    [Fact]
    public async Task A_patient_cannot_put_someone_elses_account_on_an_attorney_link()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (AsPatient())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _applicantLinks.CreateAsync(
                new AppointmentApplicantAttorneyCreateDto
                {
                    AppointmentId = AppointmentsTestData.Appointment1Id,
                    ApplicantAttorneyId = ApplicantAttorneysTestData.Attorney1Id,
                    IdentityUserId = IdentityUsersTestData.DefenseAttorney1UserId,
                }));

            // The account already on that attorney's record is allowed: it is that attorney.
            var link = await _applicantLinks.CreateAsync(new AppointmentApplicantAttorneyCreateDto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                ApplicantAttorneyId = ApplicantAttorneysTestData.Attorney1Id,
                IdentityUserId = IdentityUsersTestData.ApplicantAttorney1UserId,
            });
            link.IdentityUserId.ShouldBe(IdentityUsersTestData.ApplicantAttorney1UserId);
        }
    }

    [Fact]
    public async Task The_attorney_lookups_list_nothing_to_an_external_caller()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            (await _applicantLinks.GetApplicantAttorneyLookupAsync(new LookupRequestDto())).TotalCount
                .ShouldBeGreaterThan(0, "staff still see the records");

            using (AsPatient())
            {
                var applicant = await _applicantLinks.GetApplicantAttorneyLookupAsync(new LookupRequestDto());
                applicant.TotalCount.ShouldBe(0);
                applicant.Items.ShouldBeEmpty();
                (await _defenseLinks.GetDefenseAttorneyLookupAsync(new LookupRequestDto())).TotalCount.ShouldBe(0);
            }
        }
    }
}
