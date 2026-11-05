using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Office decision 3 (2026-11-04): a paralegal may answer Agree / Decline in the portal for THEIR side.
/// Besides the side's representative (matched by verified email, as before), the in-portal card admits a
/// case follower of the appointment -- the booker while the booker still follows, or an active Authorized
/// User -- when <see cref="ConsentSideFollowerRule.MaySpeakForSide"/> allows it: a law-office role of the
/// asked side, and for an Authorized User a row added by staff or by an account of that side (a row that
/// predates the same-side rule is judged the same way). Answering for a side IS consenting for a party, so
/// every refusal below is a security property and is the single <c>ConsentNotAvailable</c>.
///
/// <para>The side representatives are stated explicitly (two fixed addresses that belong to no account), so
/// every caller here is a FOLLOWER, never the representative. Users, roles and accessor rows are real; the
/// accessor rows are inserted directly with <c>CreatorId</c> set, which is also how a legacy row that the
/// same-side rule never judged is built. All values synthetic.</para>
/// </summary>
public class EfCoreAuthorizedUserConsentTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private const string RepA = "TEST-rep-a@test.local";
    private const string RepB = "TEST-rep-b@test.local";

    private readonly IPartyChangeRequestConsentAppService _service;
    private readonly ChangeRequestConsentManager _consentManager;
    private readonly IRepository<AppointmentChangeRequest, Guid> _requests;
    private readonly IRepository<Appointment, Guid> _appointments;
    private readonly IRepository<AppointmentAccessor, Guid> _accessors;
    private readonly IdentityUserManager _users;
    private readonly IdentityRoleManager _roles;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    protected override void AfterAddApplication(IServiceCollection services)
    {
        var recipients = Substitute.For<IAppointmentRecipientResolver>();
        recipients.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>()).Returns(new List<SendAppointmentEmailArgs>
        {
            new() { To = RepA, Role = RecipientRole.ApplicantAttorney },
            new() { To = RepB, Role = RecipientRole.DefenseAttorney },
        });
        services.Replace(ServiceDescriptor.Singleton(typeof(IAppointmentRecipientResolver), recipients));
    }

    public EfCoreAuthorizedUserConsentTests()
    {
        _service = GetRequiredService<IPartyChangeRequestConsentAppService>();
        _consentManager = GetRequiredService<ChangeRequestConsentManager>();
        _requests = GetRequiredService<IRepository<AppointmentChangeRequest, Guid>>();
        _appointments = GetRequiredService<IRepository<Appointment, Guid>>();
        _accessors = GetRequiredService<IRepository<AppointmentAccessor, Guid>>();
        _users = GetRequiredService<IdentityUserManager>();
        _roles = GetRequiredService<IdentityRoleManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    // -- an Authorized User -----------------------------------------------------------------------

    [Fact]
    public async Task An_applicant_paralegal_added_by_the_applicant_attorney_answers_side_A_only()
    {
        var (request, outcome) = await RunAsync(async f =>
        {
            var adder = await f.UserAsync(ExternalRoleConsts.ApplicantAttorney);
            var ap = await f.UserAsync(ExternalRoleConsts.ApplicantParalegal);
            await f.AddAccessorAsync(ap, addedBy: adder);
            return ap;
        }, approve: true);

        outcome.Answered.ShouldBeTrue();
        request.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Approved);
        request.SideConsentStatus(ChangeRequestSide.SideB).ShouldBe(
            ChangeRequestConsentStatus.Pending, "a side A paralegal must not answer for the defense side");
    }

    [Fact]
    public async Task The_same_applicant_paralegal_cannot_answer_when_only_side_B_is_waiting()
    {
        var (_, outcome) = await RunAsync(async f =>
        {
            var adder = await f.UserAsync(ExternalRoleConsts.ApplicantAttorney);
            var ap = await f.UserAsync(ExternalRoleConsts.ApplicantParalegal);
            await f.AddAccessorAsync(ap, addedBy: adder);
            return ap;
        }, approve: true, sideAAlreadyAnswered: true);

        outcome.Answered.ShouldBeFalse();
        outcome.RefusalCode.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotAvailable);
        outcome.ListedCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_defense_paralegal_added_by_an_applicant_account_is_refused_for_side_B()
    {
        // A legacy row, inserted directly: the same-side rule on create never judged it.
        var (request, outcome) = await RunAsync(async f =>
        {
            var plantedBy = await f.UserAsync(ExternalRoleConsts.ApplicantAttorney);
            var dp = await f.UserAsync(ExternalRoleConsts.DefenseParalegal);
            await f.AddAccessorAsync(dp, addedBy: plantedBy);
            return dp;
        }, approve: true);

        outcome.Answered.ShouldBeFalse();
        outcome.RefusalCode.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotAvailable);
        request.SideConsentStatus(ChangeRequestSide.SideB).ShouldBe(ChangeRequestConsentStatus.Pending);
    }

    [Fact]
    public async Task An_applicant_paralegal_added_by_a_defense_account_is_refused_for_side_A()
    {
        var (request, outcome) = await RunAsync(async f =>
        {
            var plantedBy = await f.UserAsync(ExternalRoleConsts.DefenseAttorney);
            var ap = await f.UserAsync(ExternalRoleConsts.ApplicantParalegal);
            await f.AddAccessorAsync(ap, addedBy: plantedBy);
            return ap;
        }, approve: true);

        outcome.Answered.ShouldBeFalse();
        request.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Pending);
    }

    [Fact]
    public async Task A_row_with_no_known_adder_is_refused()
    {
        var (_, outcome) = await RunAsync(async f =>
        {
            var ap = await f.UserAsync(ExternalRoleConsts.ApplicantParalegal);
            await f.AddAccessorAsync(ap, addedBy: null);
            return ap;
        }, approve: true);

        outcome.Answered.ShouldBeFalse();
    }

    [Fact]
    public async Task A_defense_paralegal_added_by_staff_answers_side_B()
    {
        var (request, outcome) = await RunAsync(async f =>
        {
            var staff = await f.UserAsync("Staff Supervisor");
            var dp = await f.UserAsync(ExternalRoleConsts.DefenseParalegal);
            await f.AddAccessorAsync(dp, addedBy: staff);
            return dp;
        }, approve: true);

        outcome.Answered.ShouldBeTrue();
        request.SideConsentStatus(ChangeRequestSide.SideB).ShouldBe(ChangeRequestConsentStatus.Approved);
        request.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Pending);
    }

    [Fact]
    public async Task An_inactive_authorized_user_is_refused()
    {
        var (_, outcome) = await RunAsync(async f =>
        {
            var adder = await f.UserAsync(ExternalRoleConsts.ApplicantAttorney);
            var ap = await f.UserAsync(ExternalRoleConsts.ApplicantParalegal, active: false);
            await f.AddAccessorAsync(ap, addedBy: adder);
            return ap;
        }, approve: true);

        outcome.Answered.ShouldBeFalse();
        outcome.ListedCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_staff_account_holding_an_accessor_row_is_never_a_party()
    {
        var (_, outcome) = await RunAsync(async f =>
        {
            var adder = await f.UserAsync(ExternalRoleConsts.ApplicantAttorney);
            var staff = await f.UserAsync("Staff Supervisor");
            await f.AddAccessorAsync(staff, addedBy: adder);
            return staff;
        }, approve: true);

        outcome.Answered.ShouldBeFalse();
        outcome.ListedCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_patient_role_accessor_has_no_side_and_is_refused()
    {
        var (_, outcome) = await RunAsync(async f =>
        {
            var adder = await f.UserAsync(ExternalRoleConsts.ApplicantAttorney);
            var patient = await f.UserAsync(ExternalRoleConsts.Patient);
            await f.AddAccessorAsync(patient, addedBy: adder);
            return patient;
        }, approve: true);

        outcome.Answered.ShouldBeFalse();
    }

    [Fact]
    public async Task A_decline_by_an_authorized_user_is_recorded_for_their_side_with_their_address()
    {
        var (request, outcome) = await RunAsync(async f =>
        {
            var adder = await f.UserAsync(ExternalRoleConsts.DefenseAttorney);
            var dp = await f.UserAsync(ExternalRoleConsts.DefenseParalegal);
            await f.AddAccessorAsync(dp, addedBy: adder);
            return dp;
        }, approve: false);

        outcome.Answered.ShouldBeTrue();
        request.SideConsentStatus(ChangeRequestSide.SideB).ShouldBe(ChangeRequestConsentStatus.Rejected);
        request.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Pending);
        outcome.CallerEmail.ShouldNotBeNullOrWhiteSpace();
    }

    // -- the booker ------------------------------------------------------------------------------

    [Fact]
    public async Task A_booker_paralegal_who_follows_the_case_answers_their_side()
    {
        var (request, outcome) = await RunAsync(async f =>
        {
            var booker = await f.UserAsync(ExternalRoleConsts.ApplicantParalegal);
            f.BookedBy = booker;
            return booker;
        }, approve: true);

        outcome.Answered.ShouldBeTrue();
        request.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Approved);
        request.SideConsentStatus(ChangeRequestSide.SideB).ShouldBe(ChangeRequestConsentStatus.Pending);
    }

    [Fact]
    public async Task A_defense_booker_cannot_answer_for_side_A()
    {
        var (request, outcome) = await RunAsync(async f =>
        {
            var booker = await f.UserAsync(ExternalRoleConsts.DefenseParalegal);
            f.BookedBy = booker;
            return booker;
        }, approve: true, sideBAlreadyAnswered: true);

        outcome.Answered.ShouldBeFalse();
        request.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Pending);
    }

    [Fact]
    public async Task A_booker_who_opted_out_of_following_and_is_not_the_representative_is_refused()
    {
        var (request, outcome) = await RunAsync(async f =>
        {
            var booker = await f.UserAsync(ExternalRoleConsts.ApplicantParalegal);
            f.BookedBy = booker;
            f.BookerFollows = false;
            return booker;
        }, approve: true);

        outcome.Answered.ShouldBeFalse();
        outcome.RefusalCode.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotAvailable);
        request.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Pending);
    }

    // -- the representative is unchanged -----------------------------------------------------------

    [Fact]
    public async Task The_representative_still_answers_as_before()
    {
        var request = await InOfficeAsync(async () =>
        {
            var fixture = new Fixture(this);
            var (appointmentId, requestId) = await fixture.SeedAsync(sideAAnswered: false, sideBAnswered: false);
            using (WithCurrentUser.RunWithEmail(
                       _principalAccessor, Guid.NewGuid(), RepA, true, ExternalRoleConsts.ApplicantAttorney))
            {
                var answered = await _service.SubmitMyDecisionAsync(
                    requestId, new SubmitChangeRequestConsentDto { Approved = true });
                answered.ConsentStatus.ShouldBe(ChangeRequestConsentStatus.Approved);
            }
            appointmentId.ShouldNotBe(Guid.Empty);
            return await _requests.GetAsync(requestId);
        });

        request.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Approved);
        request.SideConsentStatus(ChangeRequestSide.SideB).ShouldBe(ChangeRequestConsentStatus.Pending);
    }

    // -- harness ---------------------------------------------------------------------------------

    private sealed record Outcome(
        bool Answered, string? RefusalCode, int ListedCount, string? CallerEmail);

    /// <summary>
    /// Builds an appointment and a pending cancellation (both sides asked, unless one is stated as already
    /// answered), then lets the caller built by <paramref name="arrange"/> look at its list and try to answer.
    /// </summary>
    private async Task<(AppointmentChangeRequest Request, Outcome Outcome)> RunAsync(
        Func<Fixture, Task<Guid>> arrange,
        bool approve,
        bool sideAAlreadyAnswered = false,
        bool sideBAlreadyAnswered = false)
    {
        return await InOfficeAsync(async () =>
        {
            var fixture = new Fixture(this);
            var callerId = await arrange(fixture);
            var (appointmentId, requestId) = await fixture.SeedAsync(sideAAlreadyAnswered, sideBAlreadyAnswered);
            var callerUser = (await _users.GetByIdAsync(callerId));
            var roles = await _users.GetRolesAsync(callerUser);

            Outcome outcome;
            using (WithCurrentUser.RunWithEmail(
                       _principalAccessor, callerId, callerUser.Email, true, roles.ToArray()))
            {
                var listed = (await _service.GetMyPendingAsync(appointmentId)).Items.Count;
                try
                {
                    await _service.SubmitMyDecisionAsync(
                        requestId, new SubmitChangeRequestConsentDto { Approved = approve });
                    outcome = new Outcome(true, null, listed, callerUser.Email);
                }
                catch (BusinessException ex)
                {
                    outcome = new Outcome(false, ex.Code, listed, callerUser.Email);
                }
            }

            return (await _requests.GetAsync(requestId), outcome);
        });
    }

    private Task<T> InOfficeAsync<T>(Func<Task<T>> action) => WithUnitOfWorkAsync(async () =>
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            return await action();
        }
    });

    /// <summary>Per-fact state: the users built so far and who booked the appointment.</summary>
    private sealed class Fixture
    {
        private readonly EfCoreAuthorizedUserConsentTests _t;
        private readonly List<(Guid UserId, Guid? AddedBy)> _accessorRows = new();

        public Fixture(EfCoreAuthorizedUserConsentTests t) => _t = t;

        public Guid? BookedBy { get; set; }

        public bool BookerFollows { get; set; } = true;

        public async Task<Guid> UserAsync(string role, bool active = true)
        {
            var email = await RoleUserSeeder.CreateUserInRoleAsync(
                _t._users, _t._roles, TenantsTestData.TenantARef, role, "consent");
            var user = (await _t._users.FindByEmailAsync(email))!;
            if (!active)
            {
                user.SetIsActive(false);
                (await _t._users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
            }
            return user.Id;
        }

        public Task AddAccessorAsync(Guid userId, Guid? addedBy)
        {
            _accessorRows.Add((userId, addedBy));
            return Task.CompletedTask;
        }

        public async Task<(Guid AppointmentId, Guid RequestId)> SeedAsync(bool sideAAnswered, bool sideBAnswered)
        {
            var appointmentId = Guid.NewGuid();
            var appointment = new Appointment(
                id: appointmentId,
                patientId: PatientsTestData.Patient1Id,
                identityUserId: IdentityUsersTestData.Patient1UserId,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                appointmentDate: new DateTime(2031, 7, 1, 9, 30, 0, DateTimeKind.Utc),
                requestConfirmationNumber: $"A9-AU-{Guid.NewGuid():N}",
                appointmentStatus: AppointmentStatusType.Approved)
            {
                TenantId = TenantsTestData.TenantARef,
                PatientFirstName = "TEST-Pat",
                PatientLastName = "TEST-Doe",
                PatientEmail = IdentityUsersTestData.Patient1Email,
                ApplicantAttorneyEmail = RepA,
                DefenseAttorneyEmail = RepB,
            };
            if (BookedBy.HasValue)
            {
                appointment.RecordBookedBy(BookedBy.Value);
            }
            if (!BookerFollows)
            {
                AppointmentMailPreferences.SetBookerFollowsCase(appointment, false);
            }
            await _t._appointments.InsertAsync(appointment, autoSave: true);

            foreach (var (userId, addedBy) in _accessorRows)
            {
                var row = new AppointmentAccessor(Guid.NewGuid(), userId, appointmentId, AccessType.View)
                {
                    TenantId = TenantsTestData.TenantARef,
                };
                if (addedBy.HasValue)
                {
                    // ABP stamps the creator only for the acting user; the test sets it, which is also how a
                    // legacy row that predates the same-side rule looks.
                    ObjectHelper.TrySetProperty(row, x => x.CreatorId, () => addedBy.Value);
                }
                await _t._accessors.InsertAsync(row, autoSave: true);
            }

            var request = new AppointmentChangeRequest(
                id: Guid.NewGuid(),
                tenantId: TenantsTestData.TenantARef,
                appointmentId: appointmentId,
                changeRequestType: ChangeRequestType.Cancel,
                cancellationReason: "TEST-cancel-reason",
                reScheduleReason: null,
                newDoctorAvailabilityId: null);
            _t._consentManager.IssueSideConsent(request, ChangeRequestSide.SideA);
            _t._consentManager.IssueSideConsent(request, ChangeRequestSide.SideB);
            if (sideAAnswered)
            {
                request.RecordSideDecision(ChangeRequestSide.SideA, true, RepA, DateTime.UtcNow);
            }
            if (sideBAnswered)
            {
                request.RecordSideDecision(ChangeRequestSide.SideB, true, RepB, DateTime.UtcNow);
            }
            await _t._requests.InsertAsync(request, autoSave: true);
            return (appointmentId, request.Id);
        }
    }
}
