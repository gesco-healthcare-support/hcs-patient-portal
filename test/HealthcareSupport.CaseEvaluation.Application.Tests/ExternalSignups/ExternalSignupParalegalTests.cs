using System;
using System.Net;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// 2026-11-04: what a PARALEGAL's registration and invite do. A paralegal is a person on a law
/// office's staff, not the attorney of record, so registering one must create the account with its
/// own role and claim NOTHING an attorney's registration would: no attorney record, no link rows,
/// even when the paralegal's address is the one a booker typed as the attorney.
/// </summary>
/// <remarks>
/// Each test seeds the very things an attorney's registration would claim under the paralegal's
/// email -- an unlinked attorney record and an appointment naming that email -- so a regression
/// that ran the attorney branch for a paralegal fails here instead of passing against an empty
/// fixture. All names and emails are synthetic.
/// </remarks>
public abstract class ExternalSignupParalegalTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Password = "Test1234!";

    private readonly IExternalSignupAppService _signups;
    private readonly IdentityUserManager _users;
    private readonly IRepository<Appointment, Guid> _appointments;
    private readonly IRepository<ApplicantAttorney, Guid> _applicants;
    private readonly IRepository<DefenseAttorney, Guid> _defenses;
    private readonly IRepository<AppointmentApplicantAttorney, Guid> _applicantLinks;
    private readonly IRepository<AppointmentDefenseAttorney, Guid> _defenseLinks;
    private readonly ICurrentTenant _currentTenant;

    protected ExternalSignupParalegalTests()
    {
        _signups = GetRequiredService<IExternalSignupAppService>();
        _users = GetRequiredService<IdentityUserManager>();
        _appointments = GetRequiredService<IRepository<Appointment, Guid>>();
        _applicants = GetRequiredService<IRepository<ApplicantAttorney, Guid>>();
        _defenses = GetRequiredService<IRepository<DefenseAttorney, Guid>>();
        _applicantLinks = GetRequiredService<IRepository<AppointmentApplicantAttorney, Guid>>();
        _defenseLinks = GetRequiredService<IRepository<AppointmentDefenseAttorney, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    private async Task<T> InOfficeA<T>(Func<Task<T>> call)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    [Theory]
    [InlineData(ExternalUserType.ApplicantParalegal, "Applicant Paralegal")]
    [InlineData(ExternalUserType.DefenseParalegal, "Defense Paralegal")]
    public async Task A_paralegal_registers_with_its_own_role_and_claims_no_attorney_record(
        ExternalUserType userType, string expectedRole)
    {
        var token = NewToken();
        var email = $"TEST-paralegal-{token}@test.local";
        var (applicantId, defenseId, appointmentId) = await InOfficeA(async () =>
        {
            // What an attorney's registration under this email WOULD claim.
            var applicant = await _applicants.InsertAsync(
                new ApplicantAttorney(Guid.NewGuid(), null, null, email: email), autoSave: true);
            var defense = await _defenses.InsertAsync(
                new DefenseAttorney(Guid.NewGuid(), null, null, email: email), autoSave: true);
            var appointment = new Appointment(
                id: Guid.NewGuid(),
                patientId: PatientsTestData.Patient1Id,
                identityUserId: null,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot2Id,
                appointmentDate: new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc),
                requestConfirmationNumber: $"A9PL{token}",
                appointmentStatus: AppointmentStatusType.Pending);
            appointment.ApplicantAttorneyEmail = email;
            appointment.DefenseAttorneyEmail = email;
            await _appointments.InsertAsync(appointment, autoSave: true);
            await _applicantLinks.InsertAsync(
                new AppointmentApplicantAttorney(Guid.NewGuid(), appointment.Id, applicant.Id, null), autoSave: true);
            await _defenseLinks.InsertAsync(
                new AppointmentDefenseAttorney(Guid.NewGuid(), appointment.Id, defense.Id, null), autoSave: true);
            return (applicant.Id, defense.Id, appointment.Id);
        });

        await InOfficeA(async () =>
        {
            await _signups.RegisterAsync(new ExternalUserSignUpDto
            {
                UserType = userType,
                Email = email,
                Password = Password,
                ConfirmPassword = Password,
                FirstName = "TEST-Para",
                LastName = "TEST-Legal",
                FirmName = "TEST Firm LLP",
                TenantId = TenantsTestData.TenantARef,
            });
            return true;
        });

        await InOfficeA(async () =>
        {
            var account = (await _users.FindByEmailAsync(email)).ShouldNotBeNull();
            (await _users.GetRolesAsync(account)).ShouldBe(new[] { expectedRole });
            // A paralegal is a person: the names are kept (an attorney's are blanked), and the
            // optional firm is stored on the account.
            account.Name.ShouldBe("TEST-Para");
            account.Surname.ShouldBe("TEST-Legal");
            account.GetProperty<string>(CaseEvaluationModuleExtensionConfigurator.FirmNamePropertyName)
                .ShouldBe("TEST Firm LLP");

            (await _applicants.GetAsync(applicantId)).IdentityUserId.ShouldBeNull();
            (await _defenses.GetAsync(defenseId)).IdentityUserId.ShouldBeNull();
            (await _applicants.CountAsync(a => a.IdentityUserId == account.Id)).ShouldBe(0);
            (await _defenses.CountAsync(d => d.IdentityUserId == account.Id)).ShouldBe(0);
            (await _applicantLinks.GetListAsync(l => l.AppointmentId == appointmentId)).ShouldHaveSingleItem()
                .IdentityUserId.ShouldBeNull();
            (await _defenseLinks.GetListAsync(l => l.AppointmentId == appointmentId)).ShouldHaveSingleItem()
                .IdentityUserId.ShouldBeNull();
            return true;
        });
    }

    [Fact]
    public async Task A_paralegal_may_register_without_a_firm_name()
    {
        var email = $"TEST-nofirm-{NewToken()}@test.local";

        await InOfficeA(async () =>
        {
            await _signups.RegisterAsync(new ExternalUserSignUpDto
            {
                UserType = ExternalUserType.DefenseParalegal,
                Email = email,
                Password = Password,
                ConfirmPassword = Password,
                FirstName = "TEST-Solo",
                LastName = "TEST-Paralegal",
                TenantId = TenantsTestData.TenantARef,
            });
            return true;
        });

        await InOfficeA(async () =>
        {
            var account = (await _users.FindByEmailAsync(email)).ShouldNotBeNull();
            (await _users.GetRolesAsync(account)).ShouldBe(new[] { "Defense Paralegal" });
            account.GetProperty<string>(CaseEvaluationModuleExtensionConfigurator.FirmNamePropertyName)
                .ShouldBeNull();
            return true;
        });
    }

    [Fact]
    public async Task Staff_can_invite_a_paralegal_and_the_invite_locks_the_paralegal_role()
    {
        var email = $"TEST-invite-para-{NewToken()}@test.local";

        var result = await _signups.InviteExternalUserAsync(new InviteExternalUserDto
        {
            Email = email,
            UserType = ExternalUserType.ApplicantParalegal,
            FirmName = "TEST Invited Firm",
            TenantId = TenantsTestData.TenantARef,
        });

        result.AlreadyRegistered.ShouldBeFalse();
        const string marker = "/Account/Register?inviteToken=";
        var index = result.InviteUrl!.IndexOf(marker, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0);
        var rawToken = WebUtility.UrlDecode(result.InviteUrl[(index + marker.Length)..]);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var validated = await _signups.ValidateInviteAsync(rawToken);
            validated.UserType.ShouldBe(ExternalUserType.ApplicantParalegal);
            validated.RoleName.ShouldBe("Applicant Paralegal");
            validated.FirmName.ShouldBe("TEST Invited Firm");
        }
    }
}
