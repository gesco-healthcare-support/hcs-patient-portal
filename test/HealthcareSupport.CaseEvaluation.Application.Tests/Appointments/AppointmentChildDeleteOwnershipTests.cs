using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// DELETE on the appointment child-resource services, against real services and the real party
/// rules. Delete carried only the <c>.Delete</c> permission; it now also requires the caller to be a
/// party to the row's stored parent appointment (for a body part, the appointment its injury detail
/// belongs to). Two appointments in one office, the caller a party to exactly one.
///
/// <para>The two attorney child services share the identical guard call but need attorney master
/// rows the shared test data does not provide, so they are not exercised here.</para>
/// </summary>
public abstract class AppointmentChildDeleteOwnershipTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<AppointmentEmployerDetail, Guid> _employerRepository;
    private readonly IRepository<AppointmentClaimExaminer, Guid> _claimExaminerRepository;
    private readonly IRepository<AppointmentPrimaryInsurance, Guid> _insuranceRepository;
    private readonly IRepository<AppointmentInjuryDetail, Guid> _injuryRepository;
    private readonly IRepository<AppointmentBodyPart, Guid> _bodyPartRepository;
    private readonly IAppointmentEmployerDetailsAppService _employers;
    private readonly IAppointmentClaimExaminersAppService _claimExaminers;
    private readonly IAppointmentPrimaryInsurancesAppService _insurances;
    private readonly IAppointmentInjuryDetailsAppService _injuries;
    private readonly IAppointmentBodyPartsAppService _bodyParts;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    protected AppointmentChildDeleteOwnershipTests()
    {
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _employerRepository = GetRequiredService<IRepository<AppointmentEmployerDetail, Guid>>();
        _claimExaminerRepository = GetRequiredService<IRepository<AppointmentClaimExaminer, Guid>>();
        _insuranceRepository = GetRequiredService<IRepository<AppointmentPrimaryInsurance, Guid>>();
        _injuryRepository = GetRequiredService<IRepository<AppointmentInjuryDetail, Guid>>();
        _bodyPartRepository = GetRequiredService<IRepository<AppointmentBodyPart, Guid>>();
        _employers = GetRequiredService<IAppointmentEmployerDetailsAppService>();
        _claimExaminers = GetRequiredService<IAppointmentClaimExaminersAppService>();
        _insurances = GetRequiredService<IAppointmentPrimaryInsurancesAppService>();
        _injuries = GetRequiredService<IAppointmentInjuryDetailsAppService>();
        _bodyParts = GetRequiredService<IAppointmentBodyPartsAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private sealed record Family(Guid AppointmentId, Guid EmployerId, Guid ClaimExaminerId, Guid InsuranceId, Guid InjuryId, Guid BodyPartId);

    private sealed record Fixture(Guid CallerId, Family Own, Family Other);

    private async Task<Family> CreateFamilyAsync(Guid bookerId)
    {
        var family = new Family(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
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
            await _insuranceRepository.InsertAsync(
                new AppointmentPrimaryInsurance(family.InsuranceId, family.AppointmentId, isActive: true) { TenantId = TenantsTestData.TenantARef },
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

    private Task<int> AsExternalAsync(Guid callerId, Func<Task> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.RunWithEmail(_principalAccessor, callerId, null, IdentityUsersTestData.PatientRoleName))
            {
                await action();
                return 0;
            }
        });

    private Task<int> AsInternalAsync(Func<Task> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.RunWithEmail(_principalAccessor, IdentityUsersTestData.HostAdminId, IdentityUsersTestData.HostAdminEmail, IdentityUsersTestData.HostAdminRoleName))
            {
                await action();
                return 0;
            }
        });

    private Task<bool> ExistsAsync<T>(Guid id) where T : class, IEntity<Guid> =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await GetRequiredService<IRepository<T, Guid>>().FindAsync(id) != null;
            }
        });

    private static void ShouldBeAccessDenied(BusinessException ex) =>
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);

    [Fact]
    public async Task Delete_NonParty_IsRefused_AndTheRowSurvives_ForEveryChildService()
    {
        var f = await CreateFixtureAsync();

        ShouldBeAccessDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(f.CallerId, () => _employers.DeleteAsync(f.Other.EmployerId))));
        ShouldBeAccessDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(f.CallerId, () => _claimExaminers.DeleteAsync(f.Other.ClaimExaminerId))));
        ShouldBeAccessDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(f.CallerId, () => _insurances.DeleteAsync(f.Other.InsuranceId))));
        ShouldBeAccessDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(f.CallerId, () => _bodyParts.DeleteAsync(f.Other.BodyPartId))));
        ShouldBeAccessDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(f.CallerId, () => _injuries.DeleteAsync(f.Other.InjuryId))));

        (await ExistsAsync<AppointmentEmployerDetail>(f.Other.EmployerId)).ShouldBeTrue();
        (await ExistsAsync<AppointmentClaimExaminer>(f.Other.ClaimExaminerId)).ShouldBeTrue();
        (await ExistsAsync<AppointmentPrimaryInsurance>(f.Other.InsuranceId)).ShouldBeTrue();
        (await ExistsAsync<AppointmentBodyPart>(f.Other.BodyPartId)).ShouldBeTrue();
        (await ExistsAsync<AppointmentInjuryDetail>(f.Other.InjuryId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_Party_StillDeletesTheirOwnRows()
    {
        var f = await CreateFixtureAsync();

        await AsExternalAsync(f.CallerId, () => _employers.DeleteAsync(f.Own.EmployerId));
        await AsExternalAsync(f.CallerId, () => _claimExaminers.DeleteAsync(f.Own.ClaimExaminerId));
        await AsExternalAsync(f.CallerId, () => _insurances.DeleteAsync(f.Own.InsuranceId));
        await AsExternalAsync(f.CallerId, () => _bodyParts.DeleteAsync(f.Own.BodyPartId));
        await AsExternalAsync(f.CallerId, () => _injuries.DeleteAsync(f.Own.InjuryId));

        (await ExistsAsync<AppointmentEmployerDetail>(f.Own.EmployerId)).ShouldBeFalse();
        (await ExistsAsync<AppointmentClaimExaminer>(f.Own.ClaimExaminerId)).ShouldBeFalse();
        (await ExistsAsync<AppointmentPrimaryInsurance>(f.Own.InsuranceId)).ShouldBeFalse();
        (await ExistsAsync<AppointmentBodyPart>(f.Own.BodyPartId)).ShouldBeFalse();
        (await ExistsAsync<AppointmentInjuryDetail>(f.Own.InjuryId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_InternalCaller_CanDeleteAnyAppointmentsRows()
    {
        var f = await CreateFixtureAsync();

        await AsInternalAsync(() => _employers.DeleteAsync(f.Other.EmployerId));
        await AsInternalAsync(() => _claimExaminers.DeleteAsync(f.Other.ClaimExaminerId));
        await AsInternalAsync(() => _insurances.DeleteAsync(f.Other.InsuranceId));
        await AsInternalAsync(() => _bodyParts.DeleteAsync(f.Other.BodyPartId));
        await AsInternalAsync(() => _injuries.DeleteAsync(f.Other.InjuryId));

        (await ExistsAsync<AppointmentEmployerDetail>(f.Other.EmployerId)).ShouldBeFalse();
        (await ExistsAsync<AppointmentBodyPart>(f.Other.BodyPartId)).ShouldBeFalse();
    }
}
