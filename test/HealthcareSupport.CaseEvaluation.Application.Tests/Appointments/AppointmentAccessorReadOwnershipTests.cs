using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// READ access on the appointment accessor service, the one child service the earlier read fix
/// left out. Its writes were guarded by <c>EnsureCanManageAccessorsAsync</c>; its reads and its
/// appointment lookup carried only <c>[Authorize]</c>, so any signed-in account could list every
/// accessor row in the office -- who may reach which appointment -- and every appointment's
/// confirmation number, with no id known in advance.
///
/// <para>Same shape as <c>AppointmentChildReadOwnershipTests</c>: two appointments in one office,
/// the caller a party (as booker) to exactly one. Each accessor row names a different user, never
/// the caller, so the row cannot itself make the caller a party.</para>
/// </summary>
public abstract class AppointmentAccessorReadOwnershipTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<AppointmentAccessor, Guid> _accessorRepository;
    private readonly IAppointmentAccessorsAppService _accessors;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    protected AppointmentAccessorReadOwnershipTests()
    {
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _accessorRepository = GetRequiredService<IRepository<AppointmentAccessor, Guid>>();
        _accessors = GetRequiredService<IAppointmentAccessorsAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private sealed record Family(Guid AppointmentId, Guid AccessorId);

    private sealed record Fixture(Guid CallerId, Family Own, Family Other);

    private async Task<Family> CreateFamilyAsync(Guid bookerId)
    {
        var family = new Family(Guid.NewGuid(), Guid.NewGuid());
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
                requestConfirmationNumber: $"A9-ACC-{Guid.NewGuid():N}",
                appointmentStatus: AppointmentStatusType.Pending)
            {
                TenantId = TenantsTestData.TenantARef,
            };
            appointment.RecordBookedBy(bookerId);
            await _appointmentRepository.InsertAsync(appointment, autoSave: true);

            await _accessorRepository.InsertAsync(
                new AppointmentAccessor(family.AccessorId, IdentityUsersTestData.Patient2UserId, family.AppointmentId, AccessType.View) { TenantId = TenantsTestData.TenantARef },
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

    [Fact]
    public async Task ById_NonParty_IsRefused()
    {
        var f = await CreateFixtureAsync();

        ShouldBeAccessDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(f.CallerId, () => _accessors.GetAsync(f.Other.AccessorId))));
        ShouldBeAccessDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(f.CallerId, () => _accessors.GetWithNavigationPropertiesAsync(f.Other.AccessorId))));
    }

    [Fact]
    public async Task ById_Party_And_Internal_StillRead()
    {
        var f = await CreateFixtureAsync();

        (await AsExternalAsync(f.CallerId, () => _accessors.GetAsync(f.Own.AccessorId))).Id.ShouldBe(f.Own.AccessorId);
        (await AsExternalAsync(f.CallerId, () => _accessors.GetWithNavigationPropertiesAsync(f.Own.AccessorId))).AppointmentAccessor.Id.ShouldBe(f.Own.AccessorId);
        (await AsInternalAsync(() => _accessors.GetAsync(f.Other.AccessorId))).Id.ShouldBe(f.Other.AccessorId);
    }

    [Fact]
    public async Task ById_UnknownId_IsNotFound()
    {
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(
            () => AsInternalAsync(() => _accessors.GetWithNavigationPropertiesAsync(Guid.NewGuid())));
    }

    [Fact]
    public async Task List_WithNoFilter_ReturnsOnlyTheCallersOwnRows_AndCountsOnlyThose()
    {
        var f = await CreateFixtureAsync();

        var list = await AsExternalAsync(f.CallerId, () => _accessors.GetListAsync(new GetAppointmentAccessorsInput { MaxResultCount = 1000 }));
        list.TotalCount.ShouldBe(1);
        list.Items.Single().AppointmentAccessor.Id.ShouldBe(f.Own.AccessorId);
    }

    [Fact]
    public async Task List_NamingSomeoneElsesAppointment_ReturnsAnEmptyPage_AndTheirOwnWorks()
    {
        var f = await CreateFixtureAsync();

        (await AsExternalAsync(f.CallerId, () => _accessors.GetListAsync(new GetAppointmentAccessorsInput { AppointmentId = f.Other.AppointmentId }))).TotalCount.ShouldBe(0);
        (await AsExternalAsync(f.CallerId, () => _accessors.GetListAsync(new GetAppointmentAccessorsInput { AppointmentId = f.Own.AppointmentId }))).TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task List_InternalCaller_SeesBothAppointmentsRows()
    {
        var f = await CreateFixtureAsync();

        var list = await AsInternalAsync(() => _accessors.GetListAsync(new GetAppointmentAccessorsInput { MaxResultCount = 1000 }));
        list.Items.Select(i => i.AppointmentAccessor.Id).ShouldContain(f.Own.AccessorId);
        list.Items.Select(i => i.AppointmentAccessor.Id).ShouldContain(f.Other.AccessorId);
    }

    [Fact]
    public async Task AppointmentLookup_ListsOnlyReachableAppointments_ForAnExternalCaller()
    {
        var f = await CreateFixtureAsync();

        var external = await AsExternalAsync(f.CallerId, () => _accessors.GetAppointmentLookupAsync(new LookupRequestDto { MaxResultCount = 1000 }));
        external.TotalCount.ShouldBe(1);
        external.Items.Single().Id.ShouldBe(f.Own.AppointmentId);

        var internalCaller = await AsInternalAsync(() => _accessors.GetAppointmentLookupAsync(new LookupRequestDto { MaxResultCount = 1000 }));
        internalCaller.Items.Select(i => i.Id).ShouldContain(f.Other.AppointmentId);
    }
}
