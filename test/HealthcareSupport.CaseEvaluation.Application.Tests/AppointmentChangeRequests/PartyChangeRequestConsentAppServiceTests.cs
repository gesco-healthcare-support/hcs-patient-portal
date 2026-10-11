using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// 2026-10-10 -- the in-portal Agree / Decline for a reschedule date or a cancellation
/// (<see cref="IPartyChangeRequestConsentAppService"/>). Every refusal here is a security
/// property: answering for a side IS consenting on behalf of a party in a legal proceeding, so
/// the facts below pin who may answer -- the side's representative by VERIFIED email, nobody
/// else, never staff -- as much as they pin that the answer is recorded.
///
/// <para>Each fact builds its own appointment (party emails on the snapshot columns, so the
/// read guard's email+role rule admits the attorney) and its own change request. The recipient
/// resolver is substituted so the side representatives are stated explicitly: applicant
/// attorney for side A, defense attorney for side B. All values synthetic.</para>
/// </summary>
public abstract class PartyChangeRequestConsentAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private IPartyChangeRequestConsentAppService _service = null!;
    private ChangeRequestConsentManager _consentManager = null!;
    private IRepository<AppointmentChangeRequest, Guid> _requestRepository = null!;
    private IChangeRequestConsentRoundRepository _roundRepository = null!;
    private IRepository<Appointment, Guid> _appointmentRepository = null!;
    private ICurrentTenant _currentTenant = null!;
    private ICurrentPrincipalAccessor _principalAccessor = null!;
    private IAppointmentRecipientResolver _recipients = null!;

    protected PartyChangeRequestConsentAppServiceTests()
    {
        _service = GetRequiredService<IPartyChangeRequestConsentAppService>();
        _consentManager = GetRequiredService<ChangeRequestConsentManager>();
        _requestRepository = GetRequiredService<IRepository<AppointmentChangeRequest, Guid>>();
        _roundRepository = GetRequiredService<IChangeRequestConsentRoundRepository>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _recipients = Substitute.For<IAppointmentRecipientResolver>();
        _recipients.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>()).Returns(new List<SendAppointmentEmailArgs>
        {
            new() { To = IdentityUsersTestData.ApplicantAttorney1Email, Role = RecipientRole.ApplicantAttorney },
            new() { To = IdentityUsersTestData.DefenseAttorney1Email, Role = RecipientRole.DefenseAttorney },
        });
        services.Replace(ServiceDescriptor.Singleton(typeof(IAppointmentRecipientResolver), _recipients));
    }

    // -- the callers ------------------------------------------------------------------------

    private IDisposable AsApplicantAttorney(bool emailVerified = true) => WithCurrentUser.RunWithEmail(
        _principalAccessor,
        IdentityUsersTestData.ApplicantAttorney1UserId,
        IdentityUsersTestData.ApplicantAttorney1Email,
        emailVerified,
        IdentityUsersTestData.ApplicantAttorneyRoleName);

    private IDisposable AsDefenseAttorney() => WithCurrentUser.RunWithEmail(
        _principalAccessor,
        IdentityUsersTestData.DefenseAttorney1UserId,
        IdentityUsersTestData.DefenseAttorney1Email,
        IdentityUsersTestData.DefenseAttorneyRoleName);

    // -- fixtures ----------------------------------------------------------------------------

    private async Task<Guid> SeedAppointmentAsync()
    {
        var id = Guid.NewGuid();
        await _appointmentRepository.InsertAsync(
            new Appointment(
                id: id,
                patientId: PatientsTestData.Patient1Id,
                identityUserId: IdentityUsersTestData.Patient1UserId,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                appointmentDate: new DateTime(2031, 7, 1, 9, 30, 0, DateTimeKind.Utc),
                requestConfirmationNumber: $"A9-PCC-{Guid.NewGuid():N}",
                appointmentStatus: AppointmentStatusType.Approved)
            {
                TenantId = TenantsTestData.TenantARef,
                PatientFirstName = "TEST-Pat",
                PatientLastName = "TEST-Doe",
                PatientEmail = IdentityUsersTestData.Patient1Email,
                ApplicantAttorneyEmail = IdentityUsersTestData.ApplicantAttorney1Email,
                DefenseAttorneyEmail = IdentityUsersTestData.DefenseAttorney1Email,
            },
            autoSave: true);
        return id;
    }

    /// <summary>A staff-filed cancellation: both sides asked (Pending), as production issues it.</summary>
    private async Task<AppointmentChangeRequest> SeedCancellationAsync(Guid appointmentId)
    {
        var request = new AppointmentChangeRequest(
            id: Guid.NewGuid(),
            tenantId: TenantsTestData.TenantARef,
            appointmentId: appointmentId,
            changeRequestType: ChangeRequestType.Cancel,
            cancellationReason: "TEST-cancel-reason",
            reScheduleReason: null,
            newDoctorAvailabilityId: null);
        _consentManager.IssueSideConsent(request, ChangeRequestSide.SideA);
        _consentManager.IssueSideConsent(request, ChangeRequestSide.SideB);
        return await _requestRepository.InsertAsync(request, autoSave: true);
    }

    /// <summary>A reschedule with a staff-confirmed date (consent round 1), both sides asked.</summary>
    private async Task<AppointmentChangeRequest> SeedRescheduleRoundAsync(Guid appointmentId)
    {
        var request = await _requestRepository.InsertAsync(
            new AppointmentChangeRequest(
                id: Guid.NewGuid(),
                tenantId: TenantsTestData.TenantARef,
                appointmentId: appointmentId,
                changeRequestType: ChangeRequestType.Reschedule,
                cancellationReason: null,
                reScheduleReason: "TEST-reschedule-reason",
                newDoctorAvailabilityId: null),
            autoSave: true);
        var round = new ChangeRequestConsentRound(
            id: Guid.NewGuid(),
            tenantId: TenantsTestData.TenantARef,
            appointmentChangeRequestId: request.Id,
            roundNumber: 1,
            proposedDoctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
            proposedByUserId: null,
            proposedReason: null);
        _consentManager.IssueSideConsent(round, ChangeRequestSide.SideA);
        _consentManager.IssueSideConsent(round, ChangeRequestSide.SideB);
        await _roundRepository.InsertAsync(round, autoSave: true);
        return request;
    }

    private Task<T> InOfficeAsync<T>(Func<Task<T>> action) => WithUnitOfWorkAsync(async () =>
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            return await action();
        }
    });

    // -- what a party sees -------------------------------------------------------------------

    [Fact]
    public async Task The_applicant_attorney_sees_a_cancellation_waiting_for_their_side()
    {
        var mine = await InOfficeAsync(async () =>
        {
            var appointmentId = await SeedAppointmentAsync();
            var request = await SeedCancellationAsync(appointmentId);
            using (AsApplicantAttorney())
            {
                var list = await _service.GetMyPendingAsync(appointmentId);
                return (list.Items.Single(), request.Id);
            }
        });

        mine.Item1.ChangeRequestId.ShouldBe(mine.Id);
        mine.Item1.ChangeRequestType.ShouldBe(ChangeRequestType.Cancel);
        mine.Item1.Reason.ShouldBe("TEST-cancel-reason");
        mine.Item1.PatientName.ShouldBe("TEST-Pat TEST-Doe");
        mine.Item1.ConsentStatus.ShouldBe(ChangeRequestConsentStatus.Pending);
    }

    [Fact]
    public async Task A_reschedule_shows_the_proposed_date_from_the_current_round()
    {
        var item = await InOfficeAsync(async () =>
        {
            var appointmentId = await SeedAppointmentAsync();
            await SeedRescheduleRoundAsync(appointmentId);
            using (AsDefenseAttorney())
            {
                return (await _service.GetMyPendingAsync(appointmentId)).Items.Single();
            }
        });

        item.ChangeRequestType.ShouldBe(ChangeRequestType.Reschedule);
        item.RequestedNewDateTime.ShouldNotBeNullOrWhiteSpace();
        item.RequestedNewDateTime.ShouldContain(" at ");
    }

    [Fact]
    public async Task An_unverified_email_sees_nothing_and_cannot_answer()
    {
        var outcome = await InOfficeAsync(async () =>
        {
            var appointmentId = await SeedAppointmentAsync();
            var request = await SeedCancellationAsync(appointmentId);
            using (AsApplicantAttorney(emailVerified: false))
            {
                var list = await _service.GetMyPendingAsync(appointmentId);
                var refused = await Should.ThrowAsync<BusinessException>(() =>
                    _service.SubmitMyDecisionAsync(request.Id, new SubmitChangeRequestConsentDto { Approved = true }));
                return (list.Items.Count, refused.Code);
            }
        });

        outcome.Count.ShouldBe(0);
        outcome.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotAvailable);
    }

    [Fact]
    public async Task Internal_staff_are_never_treated_as_a_party()
    {
        var count = await InOfficeAsync(async () =>
        {
            var appointmentId = await SeedAppointmentAsync();
            await SeedCancellationAsync(appointmentId);
            // Same email as the applicant attorney, but an internal role: staff answering for a
            // party would defeat the point of asking the party.
            using (WithCurrentUser.RunWithEmail(
                       _principalAccessor,
                       Guid.NewGuid(),
                       IdentityUsersTestData.ApplicantAttorney1Email,
                       "Staff Supervisor"))
            {
                return (await _service.GetMyPendingAsync(appointmentId)).Items.Count;
            }
        });

        count.ShouldBe(0);
    }

    // -- answering ---------------------------------------------------------------------------

    [Fact]
    public async Task Agreeing_records_the_answer_on_THEIR_side_only()
    {
        var request = await InOfficeAsync(async () =>
        {
            var appointmentId = await SeedAppointmentAsync();
            var seeded = await SeedCancellationAsync(appointmentId);
            using (AsApplicantAttorney())
            {
                var answered = await _service.SubmitMyDecisionAsync(
                    seeded.Id, new SubmitChangeRequestConsentDto { Approved = true });
                answered.ConsentStatus.ShouldBe(ChangeRequestConsentStatus.Approved);
            }
            return await _requestRepository.GetAsync(seeded.Id);
        });

        request.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Approved);
        request.SideConsentStatus(ChangeRequestSide.SideB).ShouldBe(
            ChangeRequestConsentStatus.Pending,
            "the applicant attorney must not be able to answer for the defense side");
    }

    [Fact]
    public async Task Declining_a_reschedule_date_records_a_rejection_on_the_round()
    {
        var round = await InOfficeAsync(async () =>
        {
            var appointmentId = await SeedAppointmentAsync();
            var request = await SeedRescheduleRoundAsync(appointmentId);
            using (AsDefenseAttorney())
            {
                await _service.SubmitMyDecisionAsync(request.Id, new SubmitChangeRequestConsentDto { Approved = false });
            }
            return await _roundRepository.GetCurrentAsync(request.Id);
        });

        round!.SideConsentStatus(ChangeRequestSide.SideB).ShouldBe(ChangeRequestConsentStatus.Rejected);
        round.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Pending);
    }

    [Fact]
    public async Task A_second_answer_is_refused_and_the_request_drops_off_the_list()
    {
        var outcome = await InOfficeAsync(async () =>
        {
            var appointmentId = await SeedAppointmentAsync();
            var request = await SeedCancellationAsync(appointmentId);
            using (AsApplicantAttorney())
            {
                await _service.SubmitMyDecisionAsync(request.Id, new SubmitChangeRequestConsentDto { Approved = true });
                var again = await Should.ThrowAsync<BusinessException>(() =>
                    _service.SubmitMyDecisionAsync(request.Id, new SubmitChangeRequestConsentDto { Approved = false }));
                var list = await _service.GetMyPendingAsync(appointmentId);
                return (again.Code, list.Items.Count);
            }
        });

        outcome.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotAvailable);
        outcome.Count.ShouldBe(0);
    }

    [Fact]
    public async Task An_unknown_request_is_refused_exactly_like_someone_elses()
    {
        var code = await InOfficeAsync(async () =>
        {
            using (AsApplicantAttorney())
            {
                var refused = await Should.ThrowAsync<BusinessException>(() =>
                    _service.SubmitMyDecisionAsync(Guid.NewGuid(), new SubmitChangeRequestConsentDto { Approved = true }));
                return refused.Code;
            }
        });

        code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotAvailable);
    }

    [Fact]
    public async Task A_decided_request_is_no_longer_waiting_for_anyone()
    {
        var count = await InOfficeAsync(async () =>
        {
            var appointmentId = await SeedAppointmentAsync();
            var request = await SeedCancellationAsync(appointmentId);
            request.MarkDecided(RequestStatusType.Rejected, decidedById: null, nowUtc: DateTime.UtcNow);
            await _requestRepository.UpdateAsync(request, autoSave: true);
            using (AsApplicantAttorney())
            {
                return (await _service.GetMyPendingAsync(null)).Items.Count(i => i.ChangeRequestId == request.Id);
            }
        });

        count.ShouldBe(0);
    }
}
