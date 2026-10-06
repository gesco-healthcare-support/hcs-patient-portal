using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Security;
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
/// External roles hold the attorney master and schedule permissions, and the harness grants every
/// permission, so only the in-code caller checks are exercised here. Each Fact pairs a refusal or
/// narrowing for an external caller with the internal caller (unchanged) and, where one exists, the
/// legitimate own-row path.
/// </summary>
public abstract class AttorneyAndScheduleReadScopeTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IApplicantAttorneysAppService _applicant;
    private readonly IDefenseAttorneysAppService _defense;
    private readonly IAppointmentsAppService _appointments;
    private readonly IDoctorAvailabilitiesAppService _availabilities;
    private readonly IRepository<ApplicantAttorney, Guid> _applicantRepository;
    private readonly IRepository<DefenseAttorney, Guid> _defenseRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    protected AttorneyAndScheduleReadScopeTests()
    {
        _applicant = GetRequiredService<IApplicantAttorneysAppService>();
        _defense = GetRequiredService<IDefenseAttorneysAppService>();
        _appointments = GetRequiredService<IAppointmentsAppService>();
        _availabilities = GetRequiredService<IDoctorAvailabilitiesAppService>();
        _applicantRepository = GetRequiredService<IRepository<ApplicantAttorney, Guid>>();
        _defenseRepository = GetRequiredService<IRepository<DefenseAttorney, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private sealed record Masters(Guid OwnApplicantId, Guid OtherApplicantId, Guid OwnDefenseId, Guid OtherDefenseId, string Token);

    private static Guid Caller => IdentityUsersTestData.ApplicantAttorney1UserId;

    // Own rows are bound to the caller's login; "other" rows to a different seeded user.
    private async Task<Masters> SeedAsync()
    {
        var m = new Masters(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), $"TEST-scope-{Guid.NewGuid():N}"[..30]);
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _applicantRepository.InsertAsync(new ApplicantAttorney(m.OwnApplicantId, null, Caller, m.Token, email: "own-a@test.local") { TenantId = TenantsTestData.TenantARef }, autoSave: true);
                await _applicantRepository.InsertAsync(new ApplicantAttorney(m.OtherApplicantId, null, IdentityUsersTestData.Patient2UserId, m.Token, email: "other-a@test.local") { TenantId = TenantsTestData.TenantARef }, autoSave: true);
                await _defenseRepository.InsertAsync(new DefenseAttorney(m.OwnDefenseId, null, Caller, m.Token, email: "own-d@test.local") { TenantId = TenantsTestData.TenantARef }, autoSave: true);
                await _defenseRepository.InsertAsync(new DefenseAttorney(m.OtherDefenseId, null, IdentityUsersTestData.Patient2UserId, m.Token, email: "other-d@test.local") { TenantId = TenantsTestData.TenantARef }, autoSave: true);
            }
        });
        return m;
    }

    private Task<T> AsAsync<T>(Guid userId, string role, Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.RunWithEmail(_principalAccessor, userId, null, role))
            {
                return await action();
            }
        });

    private Task<T> AsExternalAsync<T>(Func<Task<T>> action) => AsAsync(Caller, IdentityUsersTestData.ApplicantAttorneyRoleName, action);

    private Task<T> AsInternalAsync<T>(Func<Task<T>> action) => AsAsync(IdentityUsersTestData.HostAdminId, IdentityUsersTestData.HostAdminRoleName, action);

    private static void ShouldBeDenied(BusinessException ex) =>
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);

    private static async Task<int> Voided(Task t)
    {
        await t;
        return 0;
    }

    // ---- A: update / create / delete ----------------------------------------------------------

    [Fact]
    public async Task Update_ExternalCaller_CannotEditSomeoneElsesMaster()
    {
        var m = await SeedAsync();
        ShouldBeDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(() =>
            _applicant.UpdateAsync(m.OtherApplicantId, new ApplicantAttorneyUpdateDto { FirmName = "x", Email = "attacker@test.local" }))));
        ShouldBeDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(() =>
            _defense.UpdateAsync(m.OtherDefenseId, new DefenseAttorneyUpdateDto { FirmName = "x", Email = "attacker@test.local" }))));

        var untouched = await AsInternalAsync(() => _applicant.GetAsync(m.OtherApplicantId));
        untouched.Email.ShouldBe("other-a@test.local");
    }

    [Fact]
    public async Task Update_ExternalCaller_OwnMaster_KeepsLoginAndEmail()
    {
        var m = await SeedAsync();
        var own = await AsExternalAsync(() => _applicant.GetAsync(m.OwnApplicantId));

        var updated = await AsExternalAsync(() => _applicant.UpdateAsync(m.OwnApplicantId, new ApplicantAttorneyUpdateDto
        {
            FirmName = "TEST-new-firm",
            Email = "redirect@test.local",
            IdentityUserId = IdentityUsersTestData.Patient2UserId,
            ConcurrencyStamp = own.ConcurrencyStamp,
        }));

        updated.FirmName.ShouldBe("TEST-new-firm");
        updated.Email.ShouldBe("own-a@test.local");
        updated.IdentityUserId.ShouldBe(Caller);

        var ownDefense = await AsExternalAsync(() => _defense.GetAsync(m.OwnDefenseId));
        var updatedDefense = await AsExternalAsync(() => _defense.UpdateAsync(m.OwnDefenseId, new DefenseAttorneyUpdateDto
        {
            FirmName = "TEST-new-firm",
            Email = "redirect@test.local",
            IdentityUserId = IdentityUsersTestData.Patient2UserId,
            ConcurrencyStamp = ownDefense.ConcurrencyStamp,
        }));
        updatedDefense.Email.ShouldBe("own-d@test.local");
        updatedDefense.IdentityUserId.ShouldBe(Caller);
    }

    [Fact]
    public async Task Update_InternalCaller_CanEditAnyMasterIncludingEmail()
    {
        var m = await SeedAsync();
        var row = await AsInternalAsync(() => _applicant.GetAsync(m.OtherApplicantId));
        var updated = await AsInternalAsync(() => _applicant.UpdateAsync(m.OtherApplicantId, new ApplicantAttorneyUpdateDto
        {
            FirmName = "TEST-staff-edit",
            Email = "staff-set@test.local",
            IdentityUserId = row.IdentityUserId,
            ConcurrencyStamp = row.ConcurrencyStamp,
        }));
        updated.Email.ShouldBe("staff-set@test.local");
    }

    [Fact]
    public async Task Create_ExternalCaller_CannotBindAMasterToSomeoneElsesLogin()
    {
        ShouldBeDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(() =>
            _applicant.CreateAsync(new ApplicantAttorneyCreateDto { FirmName = "x", IdentityUserId = IdentityUsersTestData.Patient2UserId }))));
        ShouldBeDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(() =>
            _defense.CreateAsync(new DefenseAttorneyCreateDto { FirmName = "x", IdentityUserId = IdentityUsersTestData.Patient2UserId }))));
        ShouldBeDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(() =>
            _applicant.CreateAsync(new ApplicantAttorneyCreateDto { FirmName = "x" }))));
    }

    [Fact]
    public async Task Delete_ExternalCaller_CannotDeleteSomeoneElsesMaster()
    {
        var m = await SeedAsync();
        ShouldBeDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(() => Voided(_applicant.DeleteAsync(m.OtherApplicantId)))));
        (await AsInternalAsync(() => _applicant.GetAsync(m.OtherApplicantId))).ShouldNotBeNull();
    }

    // ---- B: reads -----------------------------------------------------------------------------

    [Fact]
    public async Task Reads_ExternalCaller_SeeOnlyTheirOwnMaster()
    {
        var m = await SeedAsync();

        var list = await AsExternalAsync(() => _applicant.GetListAsync(new GetApplicantAttorneysInput { FilterText = m.Token, MaxResultCount = 50 }));
        list.TotalCount.ShouldBe(1);
        list.Items.Single().ApplicantAttorney.Id.ShouldBe(m.OwnApplicantId);

        // Asking for another login's rows by filter does not widen the result.
        var asked = await AsExternalAsync(() => _applicant.GetListAsync(new GetApplicantAttorneysInput { FilterText = m.Token, IdentityUserId = IdentityUsersTestData.Patient2UserId, MaxResultCount = 50 }));
        asked.Items.ShouldAllBe(x => x.ApplicantAttorney.Id == m.OwnApplicantId);

        var defenseList = await AsExternalAsync(() => _defense.GetListAsync(new GetDefenseAttorneysInput { FilterText = m.Token, MaxResultCount = 50 }));
        defenseList.TotalCount.ShouldBe(1);
        defenseList.Items.Single().DefenseAttorney.Id.ShouldBe(m.OwnDefenseId);

        (await AsExternalAsync(() => _applicant.GetAsync(m.OwnApplicantId))).Id.ShouldBe(m.OwnApplicantId);
        ShouldBeDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(() => _applicant.GetAsync(m.OtherApplicantId))));
        ShouldBeDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(() => _applicant.GetWithNavigationPropertiesAsync(m.OtherApplicantId))));
        ShouldBeDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(() => _defense.GetAsync(m.OtherDefenseId))));
        ShouldBeDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(() => _defense.GetWithNavigationPropertiesAsync(m.OtherDefenseId))));
    }

    [Fact]
    public async Task Reads_InternalCaller_SeeEveryMaster()
    {
        var m = await SeedAsync();
        var list = await AsInternalAsync(() => _applicant.GetListAsync(new GetApplicantAttorneysInput { FilterText = m.Token, MaxResultCount = 50 }));
        list.TotalCount.ShouldBe(2);
        (await AsInternalAsync(() => _defense.GetAsync(m.OtherDefenseId))).Id.ShouldBe(m.OtherDefenseId);
    }

    // ---- C: per-appointment attorney reads ----------------------------------------------------

    [Fact]
    public async Task AppointmentAttorneyReads_NonParty_AreRefused_AndInternalIsNot()
    {
        var stranger = Guid.NewGuid();
        ShouldBeDenied(await Should.ThrowAsync<BusinessException>(() => AsAsync(stranger, IdentityUsersTestData.PatientRoleName,
            () => _appointments.GetAppointmentApplicantAttorneyAsync(AppointmentsTestData.Appointment1Id))));
        ShouldBeDenied(await Should.ThrowAsync<BusinessException>(() => AsAsync(stranger, IdentityUsersTestData.PatientRoleName,
            () => _appointments.GetAppointmentDefenseAttorneyAsync(AppointmentsTestData.Appointment1Id))));

        await Should.NotThrowAsync(() => AsInternalAsync(() => _appointments.GetAppointmentApplicantAttorneyAsync(AppointmentsTestData.Appointment1Id)));
        await Should.NotThrowAsync(() => AsInternalAsync(() => _appointments.GetAppointmentDefenseAttorneyAsync(AppointmentsTestData.Appointment1Id)));
    }

    // ---- D: booking-detail lookups ------------------------------------------------------------

    [Fact]
    public async Task BookingDetailLookups_ExternalCaller_ResolvesOnlyThemselves()
    {
        // Another user's id and email both answer null, exactly as an unknown value does.
        (await AsExternalAsync(() => _appointments.GetApplicantAttorneyDetailsForBookingAsync(IdentityUsersTestData.DefenseAttorney1UserId, null))).ShouldBeNull();
        (await AsExternalAsync(() => _appointments.GetApplicantAttorneyDetailsForBookingAsync(null, IdentityUsersTestData.DefenseAttorney1Email))).ShouldBeNull();
        (await AsExternalAsync(() => _appointments.GetDefenseAttorneyDetailsForBookingAsync(IdentityUsersTestData.DefenseAttorney1UserId, null))).ShouldBeNull();
        (await AsExternalAsync(() => _appointments.GetDefenseAttorneyDetailsForBookingAsync(null, IdentityUsersTestData.DefenseAttorney1Email))).ShouldBeNull();

        // Themselves still resolve.
        var self = await AsExternalAsync(() => _appointments.GetApplicantAttorneyDetailsForBookingAsync(Caller, null));
        self.ShouldNotBeNull();
        self!.IdentityUserId.ShouldBe(Caller);
        (await AsExternalAsync(() => _appointments.GetDefenseAttorneyDetailsForBookingAsync(null, IdentityUsersTestData.ApplicantAttorney1Email))).ShouldNotBeNull();

        // Internal staff resolve anyone.
        (await AsInternalAsync(() => _appointments.GetApplicantAttorneyDetailsForBookingAsync(IdentityUsersTestData.DefenseAttorney1UserId, null))).ShouldNotBeNull();
    }

    // ---- E: staff schedule --------------------------------------------------------------------

    [Fact]
    public async Task StaffSchedule_ExternalCaller_IsRefused_AndInternalIsNot()
    {
        var slots = new List<Guid> { DoctorAvailabilitiesTestData.Slot1Id };
        var range = new GetScheduleInput { LocationId = LocationsTestData.Location1Id, FromDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), ToDate = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc) };
        ShouldBeDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(() => _availabilities.GetSlotPatientNamesAsync(slots))));
        ShouldBeDenied(await Should.ThrowAsync<BusinessException>(() => AsExternalAsync(() => _availabilities.GetScheduleAsync(range))));

        (await AsInternalAsync(() => _availabilities.GetSlotPatientNamesAsync(slots))).ShouldNotBeNull();
        (await AsInternalAsync(() => _availabilities.GetScheduleAsync(range))).ShouldNotBeNull();
    }
}
