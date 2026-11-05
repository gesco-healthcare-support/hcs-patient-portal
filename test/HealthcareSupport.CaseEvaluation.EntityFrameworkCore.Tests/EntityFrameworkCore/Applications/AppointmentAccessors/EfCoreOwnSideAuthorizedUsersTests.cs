using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications.Delivery;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

/// <summary>
/// Office decisions 3 + 4 (2026-11-05): after booking, an EXTERNAL caller adds, edits and removes Authorized
/// Users only of a law-office side on which they are a party of THIS case -- the named attorney of the side
/// (verified email equals that side's attorney column and the attorney role), or the booker / an Authorized
/// User holding a role of the side. Delete gains the side check. Staff are unrestricted; Patient and Claim
/// Examiner callers manage nothing. Every fact drives <see cref="IAppointmentAccessorsAppService"/> as the
/// real caller with real roles; the dispatcher is a recorder (adding an Authorized User raises mail events
/// and the rig seeds no templates).
/// </summary>
public class EfCoreOwnSideAuthorizedUsersTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private const string DefenseAttorneyEmail = "defense.lawyer@example.test";
    private const string ApplicantAttorneyEmail = "applicant.lawyer@example.test";
    private const string ClaimExaminerEmail = "examiner@example.test";

    private readonly IAppointmentAccessorsAppService _service;
    private readonly IRepository<AppointmentAccessor, Guid> _accessors;
    private readonly IRepository<Appointment, Guid> _appointments;
    private readonly IdentityUserManager _users;
    private readonly IdentityRoleManager _roles;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly NotificationRecorder _mail = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_mail));
    }

    public EfCoreOwnSideAuthorizedUsersTests()
    {
        _service = GetRequiredService<IAppointmentAccessorsAppService>();
        _accessors = GetRequiredService<IRepository<AppointmentAccessor, Guid>>();
        _appointments = GetRequiredService<IRepository<Appointment, Guid>>();
        _users = GetRequiredService<IdentityUserManager>();
        _roles = GetRequiredService<IdentityRoleManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    // -- the named attorney (not the booker) -------------------------------------------------------

    [Fact]
    public async Task The_named_defense_attorney_adds_a_defense_paralegal()
    {
        var id = await BookAsync(booker: Guid.NewGuid());

        await AsAsync(Guid.NewGuid(), DefenseAttorneyEmail, true, () =>
            _service.CreateAsync(NewAccessor(id, NewEmail("dp"), ExternalRoleConsts.DefenseParalegal)),
            ExternalRoleConsts.DefenseAttorney);

        (await CountAsync(id)).ShouldBe(1);
    }

    [Fact]
    public async Task The_named_defense_attorney_cannot_add_an_applicant_paralegal()
    {
        var id = await BookAsync(booker: Guid.NewGuid());

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(Guid.NewGuid(), DefenseAttorneyEmail, true, () =>
                _service.CreateAsync(NewAccessor(id, NewEmail("ap"), ExternalRoleConsts.ApplicantParalegal)),
                ExternalRoleConsts.DefenseAttorney));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);
        (await CountAsync(id)).ShouldBe(0);
    }

    [Fact]
    public async Task The_named_defense_attorney_cannot_remove_an_applicant_side_row_but_can_remove_a_defense_side_row()
    {
        var id = await BookAsync(booker: Guid.NewGuid());
        var (applicantRow, defenseRow) = await SeedRowsAsync(id);

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(Guid.NewGuid(), DefenseAttorneyEmail, true, () => _service.DeleteAsync(applicantRow),
                ExternalRoleConsts.DefenseAttorney));
        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);
        (await CountAsync(id)).ShouldBe(2);

        await AsAsync(Guid.NewGuid(), DefenseAttorneyEmail, true, () => _service.DeleteAsync(defenseRow),
            ExternalRoleConsts.DefenseAttorney);
        (await CountAsync(id)).ShouldBe(1);
    }

    [Fact]
    public async Task An_unverified_email_does_not_make_a_caller_the_named_attorney()
    {
        var id = await BookAsync(booker: Guid.NewGuid());

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(Guid.NewGuid(), DefenseAttorneyEmail, false, () =>
                _service.CreateAsync(NewAccessor(id, NewEmail("dp"), ExternalRoleConsts.DefenseParalegal)),
                ExternalRoleConsts.DefenseAttorney));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
    }

    [Fact]
    public async Task A_paralegal_whose_email_equals_the_attorney_column_is_not_the_named_attorney()
    {
        var id = await BookAsync(booker: Guid.NewGuid());

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(Guid.NewGuid(), DefenseAttorneyEmail, true, () =>
                _service.CreateAsync(NewAccessor(id, NewEmail("dp"), ExternalRoleConsts.DefenseParalegal)),
                ExternalRoleConsts.DefenseParalegal));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
    }

    [Fact]
    public async Task A_firm_holding_both_attorney_roles_is_judged_per_side()
    {
        var id = await BookAsync(booker: Guid.NewGuid());
        var firm = Guid.NewGuid();

        await AsAsync(firm, DefenseAttorneyEmail, true, () =>
            _service.CreateAsync(NewAccessor(id, NewEmail("dp"), ExternalRoleConsts.DefenseParalegal)),
            ExternalRoleConsts.ApplicantAttorney, ExternalRoleConsts.DefenseAttorney);
        (await CountAsync(id)).ShouldBe(1);

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(firm, DefenseAttorneyEmail, true, () =>
                _service.CreateAsync(NewAccessor(id, NewEmail("ap"), ExternalRoleConsts.ApplicantParalegal)),
                ExternalRoleConsts.ApplicantAttorney, ExternalRoleConsts.DefenseAttorney));
        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);
        (await CountAsync(id)).ShouldBe(1);
    }

    // -- an Authorized User of the case --------------------------------------------------------------

    [Fact]
    public async Task An_applicant_paralegal_who_is_an_authorized_user_adds_an_applicant_side_person_but_not_a_defense_one()
    {
        var id = await BookAsync(booker: Guid.NewGuid());
        var paralegal = await InOfficeAsync(async () =>
        {
            var userId = await UserIdAsync(ExternalRoleConsts.ApplicantParalegal);
            await _accessors.InsertAsync(new AppointmentAccessor(Guid.NewGuid(), userId, id, AccessType.View), autoSave: true);
            return userId;
        });

        await AsAsync(paralegal, null, false, () =>
            _service.CreateAsync(NewAccessor(id, NewEmail("ap"), ExternalRoleConsts.ApplicantAttorney)),
            ExternalRoleConsts.ApplicantParalegal);
        (await CountAsync(id)).ShouldBe(2);

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(paralegal, null, false, () =>
                _service.CreateAsync(NewAccessor(id, NewEmail("dp"), ExternalRoleConsts.DefenseParalegal)),
                ExternalRoleConsts.ApplicantParalegal));
        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);
    }

    [Fact]
    public async Task A_law_office_account_with_the_right_role_but_not_on_the_case_is_denied()
    {
        var id = await BookAsync(booker: Guid.NewGuid());
        var (_, defenseRow) = await SeedRowsAsync(id);

        var add = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(Guid.NewGuid(), "other.lawyer@example.test", true, () =>
                _service.CreateAsync(NewAccessor(id, NewEmail("dp"), ExternalRoleConsts.DefenseParalegal)),
                ExternalRoleConsts.DefenseAttorney));
        add.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);

        var remove = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(Guid.NewGuid(), "other.lawyer@example.test", true, () => _service.DeleteAsync(defenseRow),
                ExternalRoleConsts.DefenseAttorney));
        remove.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
        (await CountAsync(id)).ShouldBe(2);
    }

    // -- the claim examiner (decision 5) -----------------------------------------------------------------

    [Fact]
    public async Task The_named_claim_examiner_adds_and_removes_a_claim_examiner_helper()
    {
        var id = await BookAsync(booker: Guid.NewGuid());

        await AsAsync(Guid.NewGuid(), ClaimExaminerEmail, true, () =>
            _service.CreateAsync(NewAccessor(id, NewEmail("ce"), ExternalRoleConsts.ClaimExaminer)),
            ExternalRoleConsts.ClaimExaminer);
        (await CountAsync(id)).ShouldBe(1);

        var helperRow = await InOfficeAsync(async () => (await _accessors.GetListAsync(a => a.AppointmentId == id))[0].Id);
        await AsAsync(Guid.NewGuid(), ClaimExaminerEmail, true, () => _service.DeleteAsync(helperRow),
            ExternalRoleConsts.ClaimExaminer);
        (await CountAsync(id)).ShouldBe(0);
    }

    [Fact]
    public async Task A_claim_examiner_cannot_add_a_law_office_person_a_patient_or_remove_another_roles_row()
    {
        var id = await BookAsync(booker: Guid.NewGuid());
        var (applicantRow, _) = await SeedRowsAsync(id);
        var patientRow = await SeedPatientRoleRowAsync(id);

        var addParalegal = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(Guid.NewGuid(), ClaimExaminerEmail, true, () =>
                _service.CreateAsync(NewAccessor(id, NewEmail("ap"), ExternalRoleConsts.ApplicantParalegal)),
                ExternalRoleConsts.ClaimExaminer));
        addParalegal.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);

        var addPatient = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(Guid.NewGuid(), ClaimExaminerEmail, true, () =>
                _service.CreateAsync(NewAccessor(id, NewEmail("pt"), ExternalRoleConsts.Patient)),
                ExternalRoleConsts.ClaimExaminer));
        addPatient.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);

        var removeLawOffice = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(Guid.NewGuid(), ClaimExaminerEmail, true, () => _service.DeleteAsync(applicantRow),
                ExternalRoleConsts.ClaimExaminer));
        removeLawOffice.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);

        var removePatient = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(Guid.NewGuid(), ClaimExaminerEmail, true, () => _service.DeleteAsync(patientRow),
                ExternalRoleConsts.ClaimExaminer));
        removePatient.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
        (await CountAsync(id)).ShouldBe(3);
    }

    [Fact]
    public async Task A_claim_examiner_authorized_user_adds_another_helper_but_an_outside_claim_examiner_cannot()
    {
        var id = await BookAsync(booker: Guid.NewGuid());
        var helper = await InOfficeAsync(async () =>
        {
            var userId = await UserIdAsync(ExternalRoleConsts.ClaimExaminer);
            await _accessors.InsertAsync(new AppointmentAccessor(Guid.NewGuid(), userId, id, AccessType.View), autoSave: true);
            return userId;
        });

        await AsAsync(helper, null, false, () =>
            _service.CreateAsync(NewAccessor(id, NewEmail("ce"), ExternalRoleConsts.ClaimExaminer)),
            ExternalRoleConsts.ClaimExaminer);
        (await CountAsync(id)).ShouldBe(2);

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(Guid.NewGuid(), "other.examiner@example.test", true, () =>
                _service.CreateAsync(NewAccessor(id, NewEmail("ce"), ExternalRoleConsts.ClaimExaminer)),
                ExternalRoleConsts.ClaimExaminer));
        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
    }

    // -- the patient manages nothing ---------------------------------------------------------------------

    [Theory]
    [InlineData("Patient")]
    public async Task A_patient_cannot_add_or_remove(string role)
    {
        var caller = Guid.NewGuid();
        var id = await BookAsync(booker: caller);
        var (applicantRow, defenseRow) = await SeedRowsAsync(id);
        var patientRow = await SeedPatientRoleRowAsync(id);

        var add = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(caller, ApplicantAttorneyEmail, true, () =>
                _service.CreateAsync(NewAccessor(id, NewEmail("ap"), ExternalRoleConsts.ApplicantParalegal)), role));
        add.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);

        foreach (var row in new[] { applicantRow, defenseRow })
        {
            var remove = await Should.ThrowAsync<BusinessException>(() =>
                AsAsync(caller, ApplicantAttorneyEmail, true, () => _service.DeleteAsync(row), role));
            remove.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);
        }

        var removePatientRow = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(caller, ApplicantAttorneyEmail, true, () => _service.DeleteAsync(patientRow), role));
        removePatientRow.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
        (await CountAsync(id)).ShouldBe(3);
    }

    // -- staff ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Intake Staff")]
    [InlineData("Staff Supervisor")]
    public async Task Staff_add_and_remove_any_side(string staffRole)
    {
        var id = await BookAsync(booker: Guid.NewGuid());
        var (applicantRow, defenseRow) = await SeedRowsAsync(id);

        await AsAsync(Guid.NewGuid(), null, false, () =>
            _service.CreateAsync(NewAccessor(id, NewEmail("dp"), ExternalRoleConsts.DefenseParalegal)), staffRole);
        await AsAsync(Guid.NewGuid(), null, false, () =>
            _service.CreateAsync(NewAccessor(id, NewEmail("ap"), ExternalRoleConsts.ApplicantParalegal)), staffRole);
        (await CountAsync(id)).ShouldBe(4);

        await AsAsync(Guid.NewGuid(), null, false, () => _service.DeleteAsync(applicantRow), staffRole);
        await AsAsync(Guid.NewGuid(), null, false, () => _service.DeleteAsync(defenseRow), staffRole);
        (await CountAsync(id)).ShouldBe(2);
    }

    // -- the booker ------------------------------------------------------------------------------------

    [Fact]
    public async Task The_booker_removes_an_own_side_row_and_is_refused_the_other_side()
    {
        var booker = Guid.NewGuid();
        var id = await BookAsync(booker);
        var (applicantRow, defenseRow) = await SeedRowsAsync(id);

        await AsAsync(booker, null, false, () => _service.DeleteAsync(applicantRow), ExternalRoleConsts.ApplicantParalegal);
        var refused = await Should.ThrowAsync<BusinessException>(() =>
            AsAsync(booker, null, false, () => _service.DeleteAsync(defenseRow), ExternalRoleConsts.ApplicantParalegal));
        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);
    }

    // -- fixtures ----------------------------------------------------------------------------------------

    private static string NewEmail(string label) => $"TEST-{label}-{Guid.NewGuid():N}@example.test";

    private static AppointmentAccessorCreateDto NewAccessor(Guid appointmentId, string email, string role) => new()
    {
        AppointmentId = appointmentId,
        Email = email,
        Role = role,
        AccessTypeId = AccessType.View,
    };

    private Task<int> CountAsync(Guid appointmentId) =>
        InOfficeAsync(async () => (await _accessors.GetListAsync(a => a.AppointmentId == appointmentId)).Count);

    /// <summary>One applicant-paralegal row and one defense-paralegal row.</summary>
    private Task<(Guid ApplicantRow, Guid DefenseRow)> SeedRowsAsync(Guid appointmentId) =>
        InOfficeAsync(async () =>
        {
            var applicant = await _accessors.InsertAsync(
                new AppointmentAccessor(Guid.NewGuid(), await UserIdAsync(ExternalRoleConsts.ApplicantParalegal), appointmentId, AccessType.View),
                autoSave: true);
            var defense = await _accessors.InsertAsync(
                new AppointmentAccessor(Guid.NewGuid(), await UserIdAsync(ExternalRoleConsts.DefenseParalegal), appointmentId, AccessType.View),
                autoSave: true);
            return (applicant.Id, defense.Id);
        });

    private Task<Guid> SeedPatientRoleRowAsync(Guid appointmentId) =>
        InOfficeAsync(async () =>
        {
            var row = await _accessors.InsertAsync(
                new AppointmentAccessor(Guid.NewGuid(), await UserIdAsync("Patient"), appointmentId, AccessType.View),
                autoSave: true);
            return row.Id;
        });

    private async Task<Guid> UserIdAsync(string role)
    {
        var email = await RoleUserSeeder.CreateUserInRoleAsync(
            _users, _roles, TenantsTestData.TenantARef, role, "target");
        return (await _users.FindByEmailAsync(email))!.Id;
    }

    private async Task<Guid> BookAsync(Guid booker)
    {
        var id = Guid.NewGuid();
        await InOfficeAsync(async () =>
        {
            var appointment = new Appointment(
                id: id,
                patientId: PatientsTestData.Patient1Id,
                identityUserId: IdentityUsersTestData.Patient1UserId,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                appointmentDate: new DateTime(2031, 7, 1, 9, 30, 0, DateTimeKind.Utc),
                requestConfirmationNumber: $"A9-OS-{Guid.NewGuid():N}",
                appointmentStatus: AppointmentStatusType.Pending)
            {
                TenantId = TenantsTestData.TenantARef,
                ApplicantAttorneyEmail = ApplicantAttorneyEmail,
                DefenseAttorneyEmail = DefenseAttorneyEmail,
                ClaimExaminerEmail = ClaimExaminerEmail,
            };
            appointment.RecordBookedBy(booker);
            await _appointments.InsertAsync(appointment, autoSave: true);
            return 0;
        });
        return id;
    }

    private Task AsAsync(Guid userId, string? email, bool verified, Func<Task> action, params string[] roles) =>
        AsAsync<int>(userId, email, verified, async () =>
        {
            await action();
            return 0;
        }, roles);

    private Task<T> AsAsync<T>(Guid userId, string? email, bool verified, Func<Task<T>> action, params string[] roles) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.RunWithEmail(_principalAccessor, userId, email, verified, roles))
            {
                return await action();
            }
        });

    private Task<T> InOfficeAsync<T>(Func<Task<T>> action) => WithUnitOfWorkAsync(async () =>
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            return await action();
        }
    });
}
