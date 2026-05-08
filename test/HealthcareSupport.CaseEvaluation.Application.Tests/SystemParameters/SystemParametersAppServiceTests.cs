using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Validation;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.SystemParameters;

/// <summary>
/// Phase 3 (2026-05-02) AppService coverage. Exercises:
///   - Singleton read inside a tenant scope.
///   - Read-then-update round-trip with field copy semantics.
///   - Update-path validation (positive-int range + CcEmailIds length).
///   - Tenant isolation -- updates to TenantA do not bleed into TenantB.
///
/// Authorization gates ([Authorize(SystemParameters.Default / .Edit)]) are
/// NOT exercised here: ABP's test base does not run a user-claims pipeline
/// for direct AppService calls, so role-based 403s require an integration
/// tier we do not yet wire. Permission grants ARE tested implicitly via
/// the seed contributor + CaseEvaluationPermissionDefinitionProvider tests
/// added in Phase 2.5.
/// </summary>
public abstract class SystemParametersAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ISystemParametersAppService _appService;
    private readonly ISystemParameterRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    protected SystemParametersAppServiceTests()
    {
        _appService = GetRequiredService<ISystemParametersAppService>();
        _repository = GetRequiredService<ISystemParameterRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
    }

    // ------------------------------------------------------------------
    // GetAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_AfterSeed_ReturnsSingletonWithDefaults()
    {
        await EnsureTenantSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var dto = await _appService.GetAsync();

            dto.ShouldNotBeNull();
            dto.Id.ShouldNotBe(Guid.Empty);
            dto.TenantId.ShouldBe(TenantsTestData.TenantARef);
            dto.AppointmentLeadTime.ShouldBe(SystemParameterConsts.DefaultAppointmentLeadTime);
            dto.AppointmentMaxTimePQME.ShouldBe(SystemParameterConsts.DefaultAppointmentMaxTimePQME);
            dto.AppointmentMaxTimeAME.ShouldBe(SystemParameterConsts.DefaultAppointmentMaxTimeAME);
            dto.AppointmentMaxTimeOTHER.ShouldBe(SystemParameterConsts.DefaultAppointmentMaxTimeOTHER);
            dto.AppointmentCancelTime.ShouldBe(SystemParameterConsts.DefaultAppointmentCancelTime);
            dto.AppointmentDueDays.ShouldBe(SystemParameterConsts.DefaultAppointmentDueDays);
            dto.AppointmentDurationTime.ShouldBe(SystemParameterConsts.DefaultAppointmentDurationTime);
            dto.AutoCancelCutoffTime.ShouldBe(SystemParameterConsts.DefaultAutoCancelCutoffTime);
            dto.JointDeclarationUploadCutoffDays.ShouldBe(SystemParameterConsts.DefaultJointDeclarationUploadCutoffDays);
            dto.PendingAppointmentOverDueNotificationDays.ShouldBe(SystemParameterConsts.DefaultPendingAppointmentOverDueNotificationDays);
            dto.ReminderCutoffTime.ShouldBe(SystemParameterConsts.DefaultReminderCutoffTime);
            dto.IsCustomField.ShouldBe(SystemParameterConsts.DefaultIsCustomField);
            dto.CcEmailIds.ShouldBeNull();
            dto.ConcurrencyStamp.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task GetAsync_OnUnseededTenant_ThrowsBusinessException()
    {
        // Use a synthetic tenant that the orchestrator never seeded.
        var unseededTenant = Guid.Parse("00000000-1111-2222-3333-444444444444");

        using (_currentTenant.Change(unseededTenant))
        {
            var ex = await Should.ThrowAsync<BusinessException>(() => _appService.GetAsync());
            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.SystemParameterNotSeeded);
        }
    }

    // ------------------------------------------------------------------
    // UpdateAsync -- happy path
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ChangesAllFields_PersistsRoundTrip()
    {
        await EnsureTenantSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var current = await _appService.GetAsync();

            var input = new SystemParameterUpdateDto
            {
                AppointmentLeadTime = 7,
                AppointmentMaxTimePQME = 75,
                AppointmentMaxTimeAME = 100,
                AppointmentMaxTimeOTHER = 80,
                AppointmentCancelTime = 5,
                AppointmentDueDays = 21,
                AppointmentDurationTime = 45,
                AutoCancelCutoffTime = 14,
                JointDeclarationUploadCutoffDays = 10,
                PendingAppointmentOverDueNotificationDays = 5,
                ReminderCutoffTime = 12,
                IsCustomField = true,
                CcEmailIds = "auditor@example.com;ops@example.com",
                ConcurrencyStamp = current.ConcurrencyStamp,
            };

            var result = await _appService.UpdateAsync(input);

            result.AppointmentLeadTime.ShouldBe(7);
            result.AppointmentMaxTimePQME.ShouldBe(75);
            result.AppointmentMaxTimeAME.ShouldBe(100);
            result.AppointmentMaxTimeOTHER.ShouldBe(80);
            result.AppointmentCancelTime.ShouldBe(5);
            result.AppointmentDueDays.ShouldBe(21);
            result.AppointmentDurationTime.ShouldBe(45);
            result.AutoCancelCutoffTime.ShouldBe(14);
            result.JointDeclarationUploadCutoffDays.ShouldBe(10);
            result.PendingAppointmentOverDueNotificationDays.ShouldBe(5);
            result.ReminderCutoffTime.ShouldBe(12);
            result.IsCustomField.ShouldBeTrue();
            result.CcEmailIds.ShouldBe("auditor@example.com;ops@example.com");
        }
    }

    [Fact]
    public async Task UpdateAsync_NullCcEmailIds_PersistsAsNull()
    {
        await EnsureTenantSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var current = await _appService.GetAsync();

            var input = BuildValidUpdateDto(current.ConcurrencyStamp);
            input.CcEmailIds = null;

            var result = await _appService.UpdateAsync(input);

            result.CcEmailIds.ShouldBeNull();
        }
    }

    [Fact]
    public async Task UpdateAsync_ToggleIsCustomField_Persists()
    {
        await EnsureTenantSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var current = await _appService.GetAsync();

            var input = BuildValidUpdateDto(current.ConcurrencyStamp);
            input.IsCustomField = !current.IsCustomField;

            var result = await _appService.UpdateAsync(input);

            result.IsCustomField.ShouldBe(!current.IsCustomField);
        }
    }

    // ------------------------------------------------------------------
    // UpdateAsync -- validation
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ZeroAppointmentLeadTime_Throws()
    {
        await EnsureTenantSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var current = await _appService.GetAsync();
            var input = BuildValidUpdateDto(current.ConcurrencyStamp);
            input.AppointmentLeadTime = 0;

            await Should.ThrowAsync<Exception>(() => _appService.UpdateAsync(input));
        }
    }

    [Fact]
    public async Task UpdateAsync_NegativeReminderCutoffTime_Throws()
    {
        await EnsureTenantSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var current = await _appService.GetAsync();
            var input = BuildValidUpdateDto(current.ConcurrencyStamp);
            input.ReminderCutoffTime = -1;

            await Should.ThrowAsync<Exception>(() => _appService.UpdateAsync(input));
        }
    }

    [Fact]
    public async Task UpdateAsync_NegativeAppointmentDurationTime_Throws()
    {
        await EnsureTenantSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var current = await _appService.GetAsync();
            var input = BuildValidUpdateDto(current.ConcurrencyStamp);
            input.AppointmentDurationTime = -10;

            await Should.ThrowAsync<Exception>(() => _appService.UpdateAsync(input));
        }
    }

    [Fact]
    public async Task UpdateAsync_CcEmailIdsTooLong_Throws()
    {
        await EnsureTenantSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var current = await _appService.GetAsync();
            var input = BuildValidUpdateDto(current.ConcurrencyStamp);
            input.CcEmailIds = new string('a', SystemParameterConsts.CcEmailIdsMaxLength + 1);

            await Should.ThrowAsync<Exception>(() => _appService.UpdateAsync(input));
        }
    }

    // ------------------------------------------------------------------
    // Tenant isolation
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_PreservesTenantIsolation()
    {
        await EnsureTenantSeededAsync(TenantsTestData.TenantARef);
        await EnsureTenantSeededAsync(TenantsTestData.TenantBRef);

        // Mutate TenantA only.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var currentA = await _appService.GetAsync();
            var input = BuildValidUpdateDto(currentA.ConcurrencyStamp);
            input.AppointmentLeadTime = 99;
            await _appService.UpdateAsync(input);
        }

        // TenantB defaults must still be intact.
        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            var currentB = await _appService.GetAsync();
            currentB.AppointmentLeadTime.ShouldBe(SystemParameterConsts.DefaultAppointmentLeadTime);
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static SystemParameterUpdateDto BuildValidUpdateDto(string concurrencyStamp) => new()
    {
        AppointmentLeadTime = SystemParameterConsts.DefaultAppointmentLeadTime,
        AppointmentMaxTimePQME = SystemParameterConsts.DefaultAppointmentMaxTimePQME,
        AppointmentMaxTimeAME = SystemParameterConsts.DefaultAppointmentMaxTimeAME,
        AppointmentMaxTimeOTHER = SystemParameterConsts.DefaultAppointmentMaxTimeOTHER,
        AppointmentCancelTime = SystemParameterConsts.DefaultAppointmentCancelTime,
        AppointmentDueDays = SystemParameterConsts.DefaultAppointmentDueDays,
        AppointmentDurationTime = SystemParameterConsts.DefaultAppointmentDurationTime,
        AutoCancelCutoffTime = SystemParameterConsts.DefaultAutoCancelCutoffTime,
        JointDeclarationUploadCutoffDays = SystemParameterConsts.DefaultJointDeclarationUploadCutoffDays,
        PendingAppointmentOverDueNotificationDays = SystemParameterConsts.DefaultPendingAppointmentOverDueNotificationDays,
        ReminderCutoffTime = SystemParameterConsts.DefaultReminderCutoffTime,
        IsCustomField = SystemParameterConsts.DefaultIsCustomField,
        CcEmailIds = null,
        ConcurrencyStamp = concurrencyStamp,
    };

    /// <summary>
    /// Ensures the per-tenant SystemParameter singleton exists. The orchestrator
    /// only creates Tenants A and B; my Phase 1 SystemParameterDataSeedContributor
    /// only runs when ABP's IDataSeeder is invoked with a tenant context, which
    /// the integration test orchestrator does not do. Seeding inline keeps the
    /// test class self-contained and doesn't require touching the orchestrator.
    /// </summary>
    private async Task EnsureTenantSeededAsync(Guid? tenantId)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        using (_currentTenant.Change(tenantId))
        {
            var existing = await _repository.GetCurrentTenantAsync();
            if (existing != null)
            {
                await uow.CompleteAsync();
                return;
            }

            var entity = new SystemParameter(
                id: _guidGenerator.Create(),
                tenantId: tenantId,
                appointmentLeadTime: SystemParameterConsts.DefaultAppointmentLeadTime,
                appointmentMaxTimePQME: SystemParameterConsts.DefaultAppointmentMaxTimePQME,
                appointmentMaxTimeAME: SystemParameterConsts.DefaultAppointmentMaxTimeAME,
                appointmentMaxTimeOTHER: SystemParameterConsts.DefaultAppointmentMaxTimeOTHER,
                appointmentCancelTime: SystemParameterConsts.DefaultAppointmentCancelTime,
                appointmentDueDays: SystemParameterConsts.DefaultAppointmentDueDays,
                appointmentDurationTime: SystemParameterConsts.DefaultAppointmentDurationTime,
                autoCancelCutoffTime: SystemParameterConsts.DefaultAutoCancelCutoffTime,
                jointDeclarationUploadCutoffDays: SystemParameterConsts.DefaultJointDeclarationUploadCutoffDays,
                pendingAppointmentOverDueNotificationDays: SystemParameterConsts.DefaultPendingAppointmentOverDueNotificationDays,
                reminderCutoffTime: SystemParameterConsts.DefaultReminderCutoffTime,
                isCustomField: SystemParameterConsts.DefaultIsCustomField,
                ccEmailIds: null);

            await _repository.InsertAsync(entity, autoSave: true);
            await uow.CompleteAsync();
        }
    }
}
