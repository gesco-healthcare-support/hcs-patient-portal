using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// READ access on the appointment child-resource services, against real services and the real
/// party rules (no stubbed guard).
///
/// <para>The services guarded create and update and left read to the <c>.Default</c> permission,
/// which every external role holds. A caller who was not a party to an appointment could read its
/// employer, injury, body-part and claim-examiner rows by id, and list them with no appointment
/// filter, which needs no known id at all. These tests build two appointments in one office, make
/// the caller a party to exactly one, and assert the other is unreachable by id, by list and by
/// lookup, while a party and an internal caller are unaffected.</para>
///
/// <para>The caller id is fresh per fact, so the "exactly one row" list assertions cannot be
/// disturbed by rows other tests in the shared collection created.</para>
/// </summary>
public abstract class AppointmentChildReadOwnershipTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<AppointmentEmployerDetail, Guid> _employerRepository;
    private readonly IRepository<AppointmentClaimExaminer, Guid> _claimExaminerRepository;
    private readonly IRepository<AppointmentInjuryDetail, Guid> _injuryRepository;
    private readonly IRepository<AppointmentBodyPart, Guid> _bodyPartRepository;
    private readonly IAppointmentEmployerDetailsAppService _employers;
    private readonly IAppointmentClaimExaminersAppService _claimExaminers;
    private readonly IAppointmentInjuryDetailsAppService _injuries;
    private readonly IAppointmentBodyPartsAppService _bodyParts;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    protected AppointmentChildReadOwnershipTests()
    {
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _employerRepository = GetRequiredService<IRepository<AppointmentEmployerDetail, Guid>>();
        _claimExaminerRepository = GetRequiredService<IRepository<AppointmentClaimExaminer, Guid>>();
        _injuryRepository = GetRequiredService<IRepository<AppointmentInjuryDetail, Guid>>();
        _bodyPartRepository = GetRequiredService<IRepository<AppointmentBodyPart, Guid>>();
        _employers = GetRequiredService<IAppointmentEmployerDetailsAppService>();
        _claimExaminers = GetRequiredService<IAppointmentClaimExaminersAppService>();
        _injuries = GetRequiredService<IAppointmentInjuryDetailsAppService>();
        _bodyParts = GetRequiredService<IAppointmentBodyPartsAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private sealed record Family(Guid AppointmentId, Guid EmployerId, Guid ClaimExaminerId, Guid InjuryId, Guid BodyPartId);

    private sealed record Fixture(Guid CallerId, Family Own, Family Other);

    private async Task<Family> CreateFamilyAsync(Guid bookerId)
    {
        var family = new Family(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(_principalAccessor, bookerId, IdentityUsersTestData.PatientRoleName))
        {
            var appointment = new Appointment(
                id: family.AppointmentId,
                patientId: PatientsTestData.Patient1Id,
                identityUserId: IdentityUsersTestData.Patient1UserId,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                appointmentDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                requestConfirmationNumber: $"A9-CHILD-{Guid.NewGuid():N}",
                appointmentStatus: AppointmentStatusType.Pending)
            {
                TenantId = TenantsTestData.TenantARef,
            };
            appointment.RecordBookedBy(bookerId);
            await _appointmentRepository.InsertAsync(appointment, autoSave: true);

            await _employerRepository.InsertAsync(
                new AppointmentEmployerDetail(family.EmployerId, family.AppointmentId, null, "Employer", "Occupation") { TenantId = TenantsTestData.TenantARef },
                autoSave: true);
            await _claimExaminerRepository.InsertAsync(
                new AppointmentClaimExaminer(family.ClaimExaminerId, family.AppointmentId, isActive: true) { TenantId = TenantsTestData.TenantARef },
                autoSave: true);
            await _injuryRepository.InsertAsync(
                new AppointmentInjuryDetail(family.InjuryId, family.AppointmentId, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), "CLM-1", false, "Knee", wcabAdj: "ADJ1") { TenantId = TenantsTestData.TenantARef },
                autoSave: true);
            await _bodyPartRepository.InsertAsync(
                new AppointmentBodyPart(family.BodyPartId, family.InjuryId, "Knee") { TenantId = TenantsTestData.TenantARef },
                autoSave: true);
        }

        return family;
    }

    private async Task<Fixture> CreateFixtureAsync()
    {
        var callerId = Guid.NewGuid();
        Family own = null!, other = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            own = await CreateFamilyAsync(callerId);
            other = await CreateFamilyAsync(Guid.NewGuid());
        });
        return new Fixture(callerId, own, other);
    }

    private Task<T> AsExternalAsync<T>(Guid callerId, Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.RunWithEmail(_principalAccessor, callerId, null, IdentityUsersTestData.PatientRoleName))
            {
                return await action();
            }
        });

    private Task<T> AsInternalAsync<T>(Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.RunWithEmail(_principalAccessor, IdentityUsersTestData.HostAdminId, IdentityUsersTestData.HostAdminEmail, IdentityUsersTestData.HostAdminRoleName))
            {
                return await action();
            }
        });

    private static void ShouldBeAccessDenied(BusinessException ex) =>
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);

    // ---- by id -----------------------------------------------------------

    [Fact]
    public async Task ById_NonParty_IsRefused_ForEveryChildService()
    {
        var f = await CreateFixtureAsync();

        ShouldBeAccessDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(f.CallerId, () => _employers.GetAsync(f.Other.EmployerId))));
        ShouldBeAccessDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(f.CallerId, () => _employers.GetWithNavigationPropertiesAsync(f.Other.EmployerId))));
        ShouldBeAccessDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(f.CallerId, () => _claimExaminers.GetAsync(f.Other.ClaimExaminerId))));
        ShouldBeAccessDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(f.CallerId, () => _injuries.GetAsync(f.Other.InjuryId))));
        ShouldBeAccessDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(f.CallerId, () => _injuries.GetWithNavigationPropertiesAsync(f.Other.InjuryId))));
        ShouldBeAccessDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(f.CallerId, () => _injuries.GetByAppointmentIdAsync(f.Other.AppointmentId))));
        ShouldBeAccessDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(f.CallerId, () => _bodyParts.GetAsync(f.Other.BodyPartId))));
    }

    [Fact]
    public async Task ById_Party_StillReadsTheirOwnRows()
    {
        var f = await CreateFixtureAsync();

        (await AsExternalAsync(f.CallerId, () => _employers.GetAsync(f.Own.EmployerId))).Id.ShouldBe(f.Own.EmployerId);
        (await AsExternalAsync(f.CallerId, () => _employers.GetWithNavigationPropertiesAsync(f.Own.EmployerId))).AppointmentEmployerDetail.Id.ShouldBe(f.Own.EmployerId);
        (await AsExternalAsync(f.CallerId, () => _claimExaminers.GetAsync(f.Own.ClaimExaminerId))).Id.ShouldBe(f.Own.ClaimExaminerId);
        (await AsExternalAsync(f.CallerId, () => _injuries.GetAsync(f.Own.InjuryId))).Id.ShouldBe(f.Own.InjuryId);
        (await AsExternalAsync(f.CallerId, () => _injuries.GetByAppointmentIdAsync(f.Own.AppointmentId))).Count.ShouldBe(1);
        (await AsExternalAsync(f.CallerId, () => _bodyParts.GetAsync(f.Own.BodyPartId))).Id.ShouldBe(f.Own.BodyPartId);
    }

    [Fact]
    public async Task ById_InternalCaller_ReadsAnyAppointmentsRows()
    {
        var f = await CreateFixtureAsync();

        (await AsInternalAsync(() => _employers.GetAsync(f.Other.EmployerId))).Id.ShouldBe(f.Other.EmployerId);
        (await AsInternalAsync(() => _claimExaminers.GetAsync(f.Other.ClaimExaminerId))).Id.ShouldBe(f.Other.ClaimExaminerId);
        (await AsInternalAsync(() => _injuries.GetAsync(f.Other.InjuryId))).Id.ShouldBe(f.Other.InjuryId);
        (await AsInternalAsync(() => _bodyParts.GetAsync(f.Other.BodyPartId))).Id.ShouldBe(f.Other.BodyPartId);
    }

    // ---- list: the enumeration vector --------------------------------------

    [Fact]
    public async Task List_WithNoFilter_ReturnsOnlyTheCallersOwnRows_AndCountsOnlyThose()
    {
        var f = await CreateFixtureAsync();

        var employers = await AsExternalAsync(f.CallerId, () => _employers.GetListAsync(new GetAppointmentEmployerDetailsInput { MaxResultCount = 1000 }));
        employers.TotalCount.ShouldBe(1);
        employers.Items.Single().AppointmentEmployerDetail.Id.ShouldBe(f.Own.EmployerId);

        var claimExaminers = await AsExternalAsync(f.CallerId, () => _claimExaminers.GetListAsync(new GetAppointmentClaimExaminersInput { MaxResultCount = 1000 }));
        claimExaminers.TotalCount.ShouldBe(1);
        claimExaminers.Items.Single().Id.ShouldBe(f.Own.ClaimExaminerId);

        var injuries = await AsExternalAsync(f.CallerId, () => _injuries.GetListAsync(new GetAppointmentInjuryDetailsInput { MaxResultCount = 1000 }));
        injuries.TotalCount.ShouldBe(1);
        injuries.Items.Single().AppointmentInjuryDetail.Id.ShouldBe(f.Own.InjuryId);

        var bodyParts = await AsExternalAsync(f.CallerId, () => _bodyParts.GetListAsync(new GetAppointmentBodyPartsInput { MaxResultCount = 1000 }));
        bodyParts.TotalCount.ShouldBe(1);
        bodyParts.Items.Single().Id.ShouldBe(f.Own.BodyPartId);
    }

    [Fact]
    public async Task List_NamingSomeoneElsesAppointment_ReturnsAnEmptyPage()
    {
        var f = await CreateFixtureAsync();

        (await AsExternalAsync(f.CallerId, () => _employers.GetListAsync(new GetAppointmentEmployerDetailsInput { AppointmentId = f.Other.AppointmentId }))).TotalCount.ShouldBe(0);
        (await AsExternalAsync(f.CallerId, () => _claimExaminers.GetListAsync(new GetAppointmentClaimExaminersInput { AppointmentId = f.Other.AppointmentId }))).TotalCount.ShouldBe(0);
        (await AsExternalAsync(f.CallerId, () => _injuries.GetListAsync(new GetAppointmentInjuryDetailsInput { AppointmentId = f.Other.AppointmentId }))).TotalCount.ShouldBe(0);
        (await AsExternalAsync(f.CallerId, () => _bodyParts.GetListAsync(new GetAppointmentBodyPartsInput { AppointmentInjuryDetailId = f.Other.InjuryId }))).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task List_NamingTheirOwnAppointment_Works()
    {
        var f = await CreateFixtureAsync();

        var employers = await AsExternalAsync(f.CallerId, () => _employers.GetListAsync(new GetAppointmentEmployerDetailsInput { AppointmentId = f.Own.AppointmentId }));
        employers.TotalCount.ShouldBe(1);
        var bodyParts = await AsExternalAsync(f.CallerId, () => _bodyParts.GetListAsync(new GetAppointmentBodyPartsInput { AppointmentInjuryDetailId = f.Own.InjuryId }));
        bodyParts.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task List_InternalCaller_SeesBothAppointmentsRows()
    {
        var f = await CreateFixtureAsync();

        var employers = await AsInternalAsync(() => _employers.GetListAsync(new GetAppointmentEmployerDetailsInput { MaxResultCount = 1000 }));
        employers.Items.Select(i => i.AppointmentEmployerDetail.Id).ShouldContain(f.Own.EmployerId);
        employers.Items.Select(i => i.AppointmentEmployerDetail.Id).ShouldContain(f.Other.EmployerId);

        var bodyParts = await AsInternalAsync(() => _bodyParts.GetListAsync(new GetAppointmentBodyPartsInput { MaxResultCount = 1000 }));
        bodyParts.Items.Select(i => i.Id).ShouldContain(f.Own.BodyPartId);
        bodyParts.Items.Select(i => i.Id).ShouldContain(f.Other.BodyPartId);
    }

    // ---- lookup ------------------------------------------------------------

    [Fact]
    public async Task AppointmentLookup_ListsOnlyReachableAppointments_ForAnExternalCaller()
    {
        var f = await CreateFixtureAsync();

        PagedResultDto<LookupDto<Guid>> external = await AsExternalAsync(f.CallerId, () => _employers.GetAppointmentLookupAsync(new LookupRequestDto { MaxResultCount = 1000 }));
        external.TotalCount.ShouldBe(1);
        external.Items.Single().Id.ShouldBe(f.Own.AppointmentId);

        var internalCaller = await AsInternalAsync(() => _employers.GetAppointmentLookupAsync(new LookupRequestDto { MaxResultCount = 1000 }));
        internalCaller.Items.Select(i => i.Id).ShouldContain(f.Other.AppointmentId);
    }
}
