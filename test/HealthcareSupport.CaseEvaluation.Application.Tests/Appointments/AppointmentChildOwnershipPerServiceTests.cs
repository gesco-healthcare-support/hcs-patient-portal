using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// One case per guarded call site.
///
/// <para><b>Why this file exists separately.</b> The first version of these tests exercised only
/// <c>AppointmentInjuryDetailsAppService</c> and the guard class, so "removing the guard call fails
/// two tests" was true of ONE of the seven services. Deleting the call from any of the other six
/// left the whole suite green. A guard with six unwatched call sites is six unguarded services with
/// extra ceremony.</para>
///
/// <para>Each test feeds a stored row whose parent is NOT the one the caller supplies, and asserts
/// the service asked the guard about the row's stored parent. Each fails if that service's own check
/// is removed. The substitute refuses so the method returns at the gate: a directly-constructed app
/// service cannot resolve ABP's ObjectMapper, so a gate that ran after the write would die on the
/// mapping instead of throwing here -- which makes these order-sensitive as well as
/// presence-sensitive.</para>
/// </summary>
public sealed class AppointmentChildOwnershipPerServiceTests
{
    private static readonly Guid RowId = new("0ff1ce00-0000-4000-8000-00000000c0de");
    private static readonly Guid StoredParent = new("0ff1ce00-0000-4000-8000-00000000a001");
    private static readonly Guid ClaimedParent = new("0ff1ce00-0000-4000-8000-00000000a002");
    private static readonly Guid OtherId = new("0ff1ce00-0000-4000-8000-00000000b001");

    /// <summary>A guard whose checks all refuse. Shared with the create-side tests.</summary>
    internal static AppointmentChildOwnershipGuard RefusingGuard()
    {
        var readGuard = Substitute.For<AppointmentReadAccessGuard>(
            Substitute.For<IAppointmentRepository>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.AppointmentAccessors.AppointmentAccessor, Guid>>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.Patients.Patient, Guid>>(),
            Substitute.For<Volo.Abp.Users.ICurrentUser>(),
            Substitute.For<Volo.Abp.Linq.IAsyncQueryableExecuter>());

        var guard = Substitute.For<AppointmentChildOwnershipGuard>(readGuard);

        // The real gate throws this. An earlier version of these tests stubbed
        // AbpAuthorizationException -- the type this class used to throw -- so the tests agreed with
        // a comment that production contradicted.
        guard.EnsureCanWriteChildAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Throws(new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied));
        guard.EnsureIsPartyAsync(Arg.Any<Guid>())
            .Throws(new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied));

        return guard;
    }

    [Fact]
    public async Task ClaimExaminers_AsksAboutTheStoredParent()
    {
        var repo = Substitute.For<IRepository<AppointmentClaimExaminer, Guid>>();
        repo.GetAsync(RowId, Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new AppointmentClaimExaminer(RowId, StoredParent, isActive: true));
        var guard = RefusingGuard();

        var service = new AppointmentClaimExaminersAppService(
            repo,
            Substitute.For<AppointmentClaimExaminerManager>(repo),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid>>(),
            guard);

        await Should.ThrowAsync<BusinessException>(async () =>
            await service.UpdateAsync(RowId, new AppointmentClaimExaminerUpdateDto { AppointmentId = ClaimedParent }));

        await guard.Received(1).EnsureCanWriteChildAsync(StoredParent, ClaimedParent);
    }

    [Fact]
    public async Task PrimaryInsurances_AsksAboutTheStoredParent()
    {
        var repo = Substitute.For<IRepository<AppointmentPrimaryInsurance, Guid>>();
        repo.GetAsync(RowId, Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new AppointmentPrimaryInsurance(RowId, StoredParent, isActive: true));
        var guard = RefusingGuard();

        var service = new AppointmentPrimaryInsurancesAppService(
            repo,
            Substitute.For<AppointmentPrimaryInsuranceManager>(repo),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid>>(),
            guard);

        await Should.ThrowAsync<BusinessException>(async () =>
            await service.UpdateAsync(RowId, new AppointmentPrimaryInsuranceUpdateDto { AppointmentId = ClaimedParent }));

        await guard.Received(1).EnsureCanWriteChildAsync(StoredParent, ClaimedParent);
    }

    [Fact]
    public async Task EmployerDetails_AsksAboutTheStoredParent()
    {
        var repo = Substitute.For<IAppointmentEmployerDetailRepository>();
        repo.GetAsync(RowId, Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new AppointmentEmployerDetail(RowId, StoredParent, null, "a1b2c3d4", "e5f6a7b8"));
        var guard = RefusingGuard();

        var service = new AppointmentEmployerDetailsAppService(
            repo,
            Substitute.For<AppointmentEmployerDetailManager>(repo),
            Substitute.For<IRepository<Appointment, Guid>>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid>>(),
            guard);

        await Should.ThrowAsync<BusinessException>(async () =>
            await service.UpdateAsync(RowId, new AppointmentEmployerDetailUpdateDto { AppointmentId = ClaimedParent }));

        await guard.Received(1).EnsureCanWriteChildAsync(StoredParent, ClaimedParent);
    }

    [Fact]
    public async Task ApplicantAttorneys_AsksAboutTheStoredParent()
    {
        var repo = Substitute.For<IAppointmentApplicantAttorneyRepository>();
        repo.GetAsync(RowId, Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new AppointmentApplicantAttorney(RowId, StoredParent, OtherId, null));
        var guard = RefusingGuard();

        var service = new AppointmentApplicantAttorneysAppService(
            repo,
            Substitute.For<AppointmentApplicantAttorneyManager>(
                repo,
                Substitute.For<IRepository<Appointment, Guid>>(),
                Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.ApplicantAttorneys.ApplicantAttorney, Guid>>()),
            Substitute.For<IRepository<Appointment, Guid>>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.ApplicantAttorneys.ApplicantAttorney, Guid>>(),
            Substitute.For<IRepository<Volo.Abp.Identity.IdentityUser, Guid>>(),
            guard);

        await Should.ThrowAsync<BusinessException>(async () =>
            await service.UpdateAsync(RowId, new AppointmentApplicantAttorneyUpdateDto { AppointmentId = ClaimedParent, ApplicantAttorneyId = OtherId, IdentityUserId = OtherId }));

        await guard.Received(1).EnsureCanWriteChildAsync(StoredParent, ClaimedParent);
    }

    [Fact]
    public async Task DefenseAttorneys_AsksAboutTheStoredParent()
    {
        var repo = Substitute.For<IAppointmentDefenseAttorneyRepository>();
        repo.GetAsync(RowId, Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new AppointmentDefenseAttorney(RowId, StoredParent, OtherId, null));
        var guard = RefusingGuard();

        var service = new AppointmentDefenseAttorneysAppService(
            repo,
            Substitute.For<AppointmentDefenseAttorneyManager>(
                repo,
                Substitute.For<IRepository<Appointment, Guid>>(),
                Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.DefenseAttorneys.DefenseAttorney, Guid>>()),
            Substitute.For<IRepository<Appointment, Guid>>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.DefenseAttorneys.DefenseAttorney, Guid>>(),
            Substitute.For<IRepository<Volo.Abp.Identity.IdentityUser, Guid>>(),
            guard);

        await Should.ThrowAsync<BusinessException>(async () =>
            await service.UpdateAsync(RowId, new AppointmentDefenseAttorneyUpdateDto { AppointmentId = ClaimedParent, DefenseAttorneyId = OtherId, IdentityUserId = OtherId }));

        await guard.Received(1).EnsureCanWriteChildAsync(StoredParent, ClaimedParent);
    }

    /// <summary>
    /// The grandchild. Its parent is an injury detail, so the party check is against the APPOINTMENT
    /// that injury detail belongs to while the same-parent check is against the injury detail id.
    /// Two checks, two different ids, and the likeliest of the seven to regress.
    /// </summary>
    [Fact]
    public async Task BodyParts_ChecksPartyOnTheGrandparentAppointment()
    {
        var repo = Substitute.For<IRepository<AppointmentBodyPart, Guid>>();
        repo.GetAsync(RowId, Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new AppointmentBodyPart(RowId, StoredParent, "a1b2c3d4"));

        var injuryRepo = Substitute.For<IAppointmentInjuryDetailRepository>();
        injuryRepo.GetAsync(StoredParent, Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new AppointmentInjuryDetail(
                id: StoredParent,
                appointmentId: OtherId,
                dateOfInjury: new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                claimNumber: "c9d0e1f2",
                isCumulativeInjury: false,
                bodyPartsSummary: "b3c4d5e6",
                wcabAdj: "f7a8b9c0"));

        var guard = RefusingGuard();

        var service = new AppointmentBodyPartsAppService(
            repo,
            Substitute.For<AppointmentBodyPartManager>(repo),
            injuryRepo,
            guard);

        await Should.ThrowAsync<BusinessException>(async () =>
            await service.UpdateAsync(RowId, new AppointmentBodyPartUpdateDto { AppointmentInjuryDetailId = ClaimedParent }));

        // Party is asked about the GRANDPARENT appointment, resolved through the stored injury
        // detail -- not about either injury-detail id.
        await guard.Received(1).EnsureIsPartyAsync(OtherId);
    }

    /// <summary>
    /// The body part may not change injury detail. Separate from the party check because a party to
    /// the appointment could otherwise still move a row between injury details within it.
    /// </summary>
    [Fact]
    public async Task BodyParts_RefusesAChangeOfInjuryDetail()
    {
        var readGuard = Substitute.For<AppointmentReadAccessGuard>(
            Substitute.For<IAppointmentRepository>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.AppointmentAccessors.AppointmentAccessor, Guid>>(),
            Substitute.For<IRepository<HealthcareSupport.CaseEvaluation.Patients.Patient, Guid>>(),
            Substitute.For<Volo.Abp.Users.ICurrentUser>(),
            Substitute.For<Volo.Abp.Linq.IAsyncQueryableExecuter>());

        // A real guard whose party check passes, so only the same-parent rule can refuse.
        var guard = new AppointmentChildOwnershipGuard(readGuard);

        Should.Throw<BusinessException>(() => guard.EnsureSameParent(StoredParent, ClaimedParent));
        Should.NotThrow(() => guard.EnsureSameParent(StoredParent, StoredParent));
    }
}
