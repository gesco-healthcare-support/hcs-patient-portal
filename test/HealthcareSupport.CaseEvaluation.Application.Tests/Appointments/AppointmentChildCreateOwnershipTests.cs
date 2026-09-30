using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The CREATE half of the child-record ownership rule: one case per service, so removing any single
/// service's check fails its own test. <see cref="AppointmentChildOwnershipPerServiceTests"/> covers
/// the update half the same way.
///
/// <para>Each test asks for a row under an appointment and asserts two things: the service asked the
/// guard about THAT appointment (for body parts, about the appointment the parent injury detail belongs
/// to), and NO write reached the manager. The second is what proves the order. The update-side tests
/// rely on the exception type instead, and that is not enough here: a check moved to after the write
/// but before the mapping still refuses with the same exception, and was measured passing those
/// assertions. The managers' CreateAsync methods are virtual, so the substitute records any call.</para>
///
/// <para>Booking still passes the real check; that is proven end to end by
/// <c>MultiOfficeAtomicBookingSubmitTests.SubmitAsync_ByAnExternalBooker_WithEveryChildGroup_PersistsAllOfThem</c>.</para>
/// </summary>
public sealed class AppointmentChildCreateOwnershipTests
{
    private static readonly Guid TargetAppointment = new("0ff1ce00-0000-4000-8000-00000000a0c1");
    private static readonly Guid InjuryDetailId = new("0ff1ce00-0000-4000-8000-00000000a0c2");
    private static readonly Guid InjuryDetailAppointment = new("0ff1ce00-0000-4000-8000-00000000a0c3");
    private static readonly Guid OtherId = new("0ff1ce00-0000-4000-8000-00000000b0c1");

    private static AppointmentChildOwnershipGuard RefusingGuard() =>
        AppointmentChildOwnershipPerServiceTests.RefusingGuard();

    /// <summary>The write did not happen: the refused request never reached the manager's CreateAsync.</summary>
    private static void ShouldNotHaveWritten(object manager) =>
        manager.ReceivedCalls().ShouldNotContain(call => call.GetMethodInfo().Name == "CreateAsync");

    [Fact]
    public async Task ClaimExaminers_Create_AsksAboutTheTargetAppointment()
    {
        var repo = Substitute.For<IRepository<AppointmentClaimExaminer, Guid>>();
        var manager = Substitute.For<AppointmentClaimExaminerManager>(repo);
        var guard = RefusingGuard();
        var service = new AppointmentClaimExaminersAppService(
            repo,
            manager,
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid>>(),
            guard);

        await Should.ThrowAsync<BusinessException>(async () =>
            await service.CreateAsync(new AppointmentClaimExaminerCreateDto { AppointmentId = TargetAppointment }));

        await guard.Received(1).EnsureIsPartyAsync(TargetAppointment);
        ShouldNotHaveWritten(manager);
    }

    [Fact]
    public async Task PrimaryInsurances_Create_AsksAboutTheTargetAppointment()
    {
        var repo = Substitute.For<IRepository<AppointmentPrimaryInsurance, Guid>>();
        var manager = Substitute.For<AppointmentPrimaryInsuranceManager>(repo);
        var guard = RefusingGuard();
        var service = new AppointmentPrimaryInsurancesAppService(
            repo,
            manager,
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid>>(),
            guard);

        await Should.ThrowAsync<BusinessException>(async () =>
            await service.CreateAsync(new AppointmentPrimaryInsuranceCreateDto { AppointmentId = TargetAppointment }));

        await guard.Received(1).EnsureIsPartyAsync(TargetAppointment);
        ShouldNotHaveWritten(manager);
    }

    [Fact]
    public async Task EmployerDetails_Create_AsksAboutTheTargetAppointment()
    {
        var repo = Substitute.For<IAppointmentEmployerDetailRepository>();
        var manager = Substitute.For<AppointmentEmployerDetailManager>(repo);
        var guard = RefusingGuard();
        var service = new AppointmentEmployerDetailsAppService(
            repo,
            manager,
            Substitute.For<IRepository<Appointment, Guid>>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid>>(),
            guard);

        await Should.ThrowAsync<BusinessException>(async () =>
            await service.CreateAsync(new AppointmentEmployerDetailCreateDto
            {
                AppointmentId = TargetAppointment,
                EmployerName = "a1b2c3d4",
                Occupation = "e5f6a7b8",
            }));

        await guard.Received(1).EnsureIsPartyAsync(TargetAppointment);
        ShouldNotHaveWritten(manager);
    }

    [Fact]
    public async Task InjuryDetails_Create_AsksAboutTheTargetAppointment()
    {
        var repo = Substitute.For<IAppointmentInjuryDetailRepository>();
        var manager = Substitute.For<AppointmentInjuryDetailManager>(
                repo,
                Substitute.For<IRepository<Appointment, Guid>>(),
                Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.Patients.Patient, Guid>>(),
                Substitute.For<Volo.Abp.Timing.IClock>());
        var guard = RefusingGuard();
        var service = new AppointmentInjuryDetailsAppService(
            repo,
            manager,
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.WcabOffices.WcabOffice, Guid>>(),
            guard);

        await Should.ThrowAsync<BusinessException>(async () =>
            await service.CreateAsync(new AppointmentInjuryDetailCreateDto
            {
                AppointmentId = TargetAppointment,
                DateOfInjury = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ClaimNumber = "c9d0e1f2",
                BodyPartsSummary = "b3c4d5e6",
            }));

        await guard.Received(1).EnsureIsPartyAsync(TargetAppointment);
        ShouldNotHaveWritten(manager);
    }

    [Fact]
    public async Task ApplicantAttorneys_Create_AsksAboutTheTargetAppointment()
    {
        var repo = Substitute.For<IAppointmentApplicantAttorneyRepository>();
        var manager = Substitute.For<AppointmentApplicantAttorneyManager>(
                repo,
                Substitute.For<IRepository<Appointment, Guid>>(),
                Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.ApplicantAttorneys.ApplicantAttorney, Guid>>());
        var guard = RefusingGuard();
        var service = new AppointmentApplicantAttorneysAppService(
            repo,
            manager,
            Substitute.For<IRepository<Appointment, Guid>>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.ApplicantAttorneys.ApplicantAttorney, Guid>>(),
            Substitute.For<IRepository<Volo.Abp.Identity.IdentityUser, Guid>>(),
            guard);

        await Should.ThrowAsync<BusinessException>(async () =>
            await service.CreateAsync(new AppointmentApplicantAttorneyCreateDto
            {
                AppointmentId = TargetAppointment,
                ApplicantAttorneyId = OtherId,
                IdentityUserId = OtherId,
            }));

        await guard.Received(1).EnsureIsPartyAsync(TargetAppointment);
        ShouldNotHaveWritten(manager);
    }

    [Fact]
    public async Task DefenseAttorneys_Create_AsksAboutTheTargetAppointment()
    {
        var repo = Substitute.For<IAppointmentDefenseAttorneyRepository>();
        var manager = Substitute.For<AppointmentDefenseAttorneyManager>(
                repo,
                Substitute.For<IRepository<Appointment, Guid>>(),
                Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.DefenseAttorneys.DefenseAttorney, Guid>>());
        var guard = RefusingGuard();
        var service = new AppointmentDefenseAttorneysAppService(
            repo,
            manager,
            Substitute.For<IRepository<Appointment, Guid>>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.DefenseAttorneys.DefenseAttorney, Guid>>(),
            Substitute.For<IRepository<Volo.Abp.Identity.IdentityUser, Guid>>(),
            guard);

        await Should.ThrowAsync<BusinessException>(async () =>
            await service.CreateAsync(new AppointmentDefenseAttorneyCreateDto
            {
                AppointmentId = TargetAppointment,
                DefenseAttorneyId = OtherId,
                IdentityUserId = OtherId,
            }));

        await guard.Received(1).EnsureIsPartyAsync(TargetAppointment);
        ShouldNotHaveWritten(manager);
    }

    /// <summary>
    /// The grandchild: the party check is against the appointment its PARENT INJURY DETAIL belongs to,
    /// read from the stored injury detail, not against any id in the request.
    /// </summary>
    [Fact]
    public async Task BodyParts_Create_AsksAboutTheInjuryDetailsAppointment()
    {
        var repo = Substitute.For<IRepository<AppointmentBodyPart, Guid>>();
        var injuryRepo = Substitute.For<IAppointmentInjuryDetailRepository>();
        injuryRepo.GetAsync(InjuryDetailId, Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new AppointmentInjuryDetail(
                id: InjuryDetailId,
                appointmentId: InjuryDetailAppointment,
                dateOfInjury: new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                claimNumber: "c9d0e1f2",
                isCumulativeInjury: false,
                bodyPartsSummary: "b3c4d5e6",
                wcabAdj: "f7a8b9c0"));
        var manager = Substitute.For<AppointmentBodyPartManager>(repo);
        var guard = RefusingGuard();
        var service = new AppointmentBodyPartsAppService(
            repo,
            manager,
            injuryRepo,
            guard);

        await Should.ThrowAsync<BusinessException>(async () =>
            await service.CreateAsync(new AppointmentBodyPartCreateDto
            {
                AppointmentInjuryDetailId = InjuryDetailId,
                BodyPartDescription = "d7e8f9a0",
            }));

        await guard.Received(1).EnsureIsPartyAsync(InjuryDetailAppointment);
        ShouldNotHaveWritten(manager);
    }
}
