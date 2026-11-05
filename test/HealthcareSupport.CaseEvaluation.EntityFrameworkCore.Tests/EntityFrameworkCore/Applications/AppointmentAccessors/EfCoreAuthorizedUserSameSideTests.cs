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
/// Office decision 3 (2026-11-04), the same-side rule for Authorized Users: an EXTERNAL caller may add or
/// change a law-office Authorized User (attorney or paralegal) only of their OWN side, so one side can never
/// plant someone who then answers consent for the other. Staff are unrestricted; Patient and Claim Examiner
/// accessors carry no side and are unchanged. Every accessor write goes through
/// <see cref="IAppointmentAccessorsAppService"/>, including the ones inside the atomic booking submit, so
/// these facts drive that service as the real caller (the submit variant lives in
/// <c>MultiOfficeAtomicBookingSubmitTests</c>).
///
/// <para>Each fact builds its own appointment booked by the caller (so the accessor-management gate admits
/// them as the creator) and uses fresh synthetic addresses. The dispatcher is a recorder: adding an Authorized
/// User raises mail events and the rig seeds no templates.</para>
/// </summary>
public class EfCoreAuthorizedUserSameSideTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
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

    public EfCoreAuthorizedUserSameSideTests()
    {
        _service = GetRequiredService<IAppointmentAccessorsAppService>();
        _accessors = GetRequiredService<IRepository<AppointmentAccessor, Guid>>();
        _appointments = GetRequiredService<IRepository<Appointment, Guid>>();
        _users = GetRequiredService<IdentityUserManager>();
        _roles = GetRequiredService<IdentityRoleManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    // -- create ----------------------------------------------------------------------------------

    [Fact]
    public async Task An_applicant_attorney_may_add_an_applicant_paralegal()
    {
        var caller = Guid.NewGuid();
        var id = await BookAsync(caller);

        await AsCallerAsync(caller, ExternalRoleConsts.ApplicantAttorney, () =>
            _service.CreateAsync(NewAccessor(id, NewEmail("ap"), ExternalRoleConsts.ApplicantParalegal)));

        (await CountAccessorsAsync(id)).ShouldBe(1);
    }

    [Fact]
    public async Task An_applicant_attorney_cannot_add_a_defense_paralegal()
    {
        var caller = Guid.NewGuid();
        var id = await BookAsync(caller);

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            AsCallerAsync(caller, ExternalRoleConsts.ApplicantAttorney, () =>
                _service.CreateAsync(NewAccessor(id, NewEmail("dp"), ExternalRoleConsts.DefenseParalegal))));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);
        (await CountAccessorsAsync(id)).ShouldBe(0, "nothing is written when the side is wrong");
    }

    [Fact]
    public async Task An_applicant_attorney_cannot_add_a_defense_attorney_accessor()
    {
        var caller = Guid.NewGuid();
        var id = await BookAsync(caller);

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            AsCallerAsync(caller, ExternalRoleConsts.ApplicantAttorney, () =>
                _service.CreateAsync(NewAccessor(id, NewEmail("da"), ExternalRoleConsts.DefenseAttorney))));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);
    }

    [Fact]
    public async Task A_defense_paralegal_cannot_add_an_applicant_attorney()
    {
        var caller = Guid.NewGuid();
        var id = await BookAsync(caller);

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            AsCallerAsync(caller, ExternalRoleConsts.DefenseParalegal, () =>
                _service.CreateAsync(NewAccessor(id, NewEmail("aa"), ExternalRoleConsts.ApplicantAttorney))));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);
        (await CountAccessorsAsync(id)).ShouldBe(0);
    }

    [Fact]
    public async Task A_defense_attorney_may_add_a_defense_paralegal()
    {
        var caller = Guid.NewGuid();
        var id = await BookAsync(caller);

        await AsCallerAsync(caller, ExternalRoleConsts.DefenseAttorney, () =>
            _service.CreateAsync(NewAccessor(id, NewEmail("dp"), ExternalRoleConsts.DefenseParalegal)));

        (await CountAccessorsAsync(id)).ShouldBe(1);
    }

    [Fact]
    public async Task Staff_may_add_a_defense_paralegal_on_an_applicant_booked_case()
    {
        var applicantBooker = Guid.NewGuid();
        var id = await BookAsync(applicantBooker);

        await AsCallerAsync(Guid.NewGuid(), "Staff Supervisor", () =>
            _service.CreateAsync(NewAccessor(id, NewEmail("dp"), ExternalRoleConsts.DefenseParalegal)));

        (await CountAccessorsAsync(id)).ShouldBe(1);
    }

    // 2026-11-05 (decision 3, review): a Patient or Claim Examiner role is not a side, so a law-office booker
    // may not add one -- such an account gets the attorney packet and case mail, and the booker could not
    // remove the row again. Only a claim examiner party may add a Claim Examiner helper (decision 5).
    [Theory]
    [InlineData("Patient")]
    [InlineData("Claim Examiner")]
    public async Task A_law_office_booker_cannot_add_a_patient_or_claim_examiner_accessor(string role)
    {
        var caller = Guid.NewGuid();
        var id = await BookAsync(caller);

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            AsCallerAsync(caller, ExternalRoleConsts.DefenseParalegal, () =>
                _service.CreateAsync(NewAccessor(id, NewEmail("np"), role))));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
        (await CountAccessorsAsync(id)).ShouldBe(0);
    }

    [Theory]
    [InlineData("Patient", "Applicant Attorney")]
    [InlineData("Patient", "Applicant Paralegal")]
    [InlineData("Patient", "Defense Attorney")]
    [InlineData("Patient", "Defense Paralegal")]
    [InlineData("Claim Examiner", "Applicant Attorney")]
    [InlineData("Claim Examiner", "Applicant Paralegal")]
    [InlineData("Claim Examiner", "Defense Attorney")]
    [InlineData("Claim Examiner", "Defense Paralegal")]
    public async Task A_caller_with_no_law_office_role_cannot_add_any_law_office_accessor(string callerRole, string accessorRole)
    {
        var caller = Guid.NewGuid();
        var id = await BookAsync(caller);

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            AsCallerAsync(caller, callerRole, () =>
                _service.CreateAsync(NewAccessor(id, NewEmail("lo"), accessorRole))));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);
        (await CountAccessorsAsync(id)).ShouldBe(0);
    }

    [Theory]
    [InlineData("Patient")]
    [InlineData("Claim Examiner")]
    public async Task A_caller_with_no_law_office_role_cannot_point_an_accessor_at_a_law_office_account(string callerRole)
    {
        var caller = Guid.NewGuid();
        var id = await BookAsync(caller);
        var (rowId, ownUser, _, sameSideUser) = await SeedRowAndTargetsAsync(id);

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            AsCallerAsync(caller, callerRole, () => _service.UpdateAsync(rowId, Update(id, sameSideUser))));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);
        (await InOfficeAsync(() => _accessors.GetAsync(rowId))).IdentityUserId.ShouldBe(ownUser);
    }

    // -- update ----------------------------------------------------------------------------------

    [Fact]
    public async Task Pointing_an_accessor_at_an_other_side_account_is_refused_and_the_row_is_unchanged()
    {
        var caller = Guid.NewGuid();
        var id = await BookAsync(caller);
        var (rowId, ownUser, otherSideUser, sameSideUser) = await SeedRowAndTargetsAsync(id);

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            AsCallerAsync(caller, ExternalRoleConsts.ApplicantAttorney, () =>
                _service.UpdateAsync(rowId, Update(id, otherSideUser))));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);
        (await InOfficeAsync(() => _accessors.GetAsync(rowId))).IdentityUserId.ShouldBe(ownUser);

        // The same-side account is accepted, so the refusal above is the side and not the plumbing.
        await AsCallerAsync(caller, ExternalRoleConsts.ApplicantAttorney, () =>
            _service.UpdateAsync(rowId, Update(id, sameSideUser)));
        (await InOfficeAsync(() => _accessors.GetAsync(rowId))).IdentityUserId.ShouldBe(sameSideUser);
    }

    [Fact]
    public async Task Staff_may_point_an_accessor_at_an_other_side_account()
    {
        var caller = Guid.NewGuid();
        var id = await BookAsync(caller);
        var (rowId, _, otherSideUser, _) = await SeedRowAndTargetsAsync(id);

        await AsCallerAsync(Guid.NewGuid(), "Staff Supervisor", () =>
            _service.UpdateAsync(rowId, Update(id, otherSideUser)));

        (await InOfficeAsync(() => _accessors.GetAsync(rowId))).IdentityUserId.ShouldBe(otherSideUser);
    }

    [Fact]
    public async Task Changing_only_the_access_type_of_an_existing_row_is_not_re_judged()
    {
        var caller = Guid.NewGuid();
        var id = await BookAsync(caller);
        var (rowId, ownUser, _, _) = await SeedRowAndTargetsAsync(id);

        var updated = await AsCallerAsync(caller, ExternalRoleConsts.ApplicantAttorney, () =>
            _service.UpdateAsync(rowId, new AppointmentAccessorUpdateDto
            {
                AppointmentId = id,
                IdentityUserId = ownUser,
                AccessTypeId = AccessType.Edit,
            }));

        updated.AccessTypeId.ShouldBe(AccessType.Edit);
    }

    // -- fixtures --------------------------------------------------------------------------------

    private static string NewEmail(string label) => $"TEST-{label}-{Guid.NewGuid():N}@test.local";

    private static AppointmentAccessorCreateDto NewAccessor(Guid appointmentId, string email, string role) => new()
    {
        AppointmentId = appointmentId,
        Email = email,
        Role = role,
        AccessTypeId = AccessType.View,
    };

    private static AppointmentAccessorUpdateDto Update(Guid appointmentId, Guid userId) => new()
    {
        AppointmentId = appointmentId,
        IdentityUserId = userId,
        AccessTypeId = AccessType.View,
    };

    private Task<int> CountAccessorsAsync(Guid appointmentId) =>
        InOfficeAsync(async () => (await _accessors.GetListAsync(a => a.AppointmentId == appointmentId)).Count);

    /// <summary>
    /// An accessor row for an applicant paralegal, plus a defense paralegal and a second applicant
    /// paralegal to point it at.
    /// </summary>
    private Task<(Guid RowId, Guid Own, Guid OtherSide, Guid SameSide)> SeedRowAndTargetsAsync(Guid appointmentId) =>
        InOfficeAsync(async () =>
        {
            var own = await UserIdAsync(ExternalRoleConsts.ApplicantParalegal);
            var other = await UserIdAsync(ExternalRoleConsts.DefenseParalegal);
            var same = await UserIdAsync(ExternalRoleConsts.ApplicantParalegal);
            var row = await _accessors.InsertAsync(
                new AppointmentAccessor(Guid.NewGuid(), own, appointmentId, AccessType.View), autoSave: true);
            return (row.Id, own, other, same);
        });

    private async Task<Guid> UserIdAsync(string role)
    {
        var email = await RoleUserSeeder.CreateUserInRoleAsync(
            _users, _roles, TenantsTestData.TenantARef, role, "target");
        return (await _users.FindByEmailAsync(email))!.Id;
    }

    private async Task<Guid> BookAsync(Guid bookerId)
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
                requestConfirmationNumber: $"A9-SS-{Guid.NewGuid():N}",
                appointmentStatus: AppointmentStatusType.Pending)
            {
                TenantId = TenantsTestData.TenantARef,
            };
            appointment.RecordBookedBy(bookerId);
            await _appointments.InsertAsync(appointment, autoSave: true);
            return 0;
        });
        return id;
    }

    private Task AsCallerAsync(Guid userId, string role, Func<Task> action) =>
        AsCallerAsync<int>(userId, role, async () =>
        {
            await action();
            return 0;
        });

    private Task<T> AsCallerAsync<T>(Guid userId, string role, Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.Run(_principalAccessor, userId, role))
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
