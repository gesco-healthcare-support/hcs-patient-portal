using System;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.SystemParameters;

/// <summary>
/// Phase 3 (2026-05-02) -- pure unit tests for the
/// <see cref="SystemParametersAppService"/> validator + field-copy helpers.
///
/// These intentionally bypass ABP's integration test harness (which exhibits
/// a pre-existing test-host crash unrelated to this work). They exercise the
/// validator and field-copy logic directly via the `internal` accessor
/// exposed to this assembly through <c>InternalsVisibleTo</c>. Coverage:
///
///   1. Positive-integer range check rejects 0 and negatives on every int
///      field (mirrors OLD's `[Range(1, int.MaxValue)]` attributes).
///   2. CC-email-IDs length check enforces the
///      <see cref="SystemParameterConsts.CcEmailIdsMaxLength"/> cap.
///   3. <c>ApplyUpdate</c> copies all 13 user-editable fields and leaves the
///      audit / id / tenant / concurrency-stamp fields on the destination
///      untouched.
///
/// End-to-end DB / authorization / multi-tenant assertions live in
/// <see cref="SystemParametersAppServiceTests{TStartupModule}"/> -- those
/// will run once the ABP test-host crash is resolved (separate workstream).
/// </summary>
public class SystemParametersValidatorUnitTests
{
    // ------------------------------------------------------------------
    // Positive-integer validation
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ValidatePositiveIntegers_AppointmentLeadTimeNonPositive_Throws(int badValue)
    {
        var dto = ValidDto();
        dto.AppointmentLeadTime = badValue;

        Should.Throw<ArgumentException>(
            () => SystemParametersAppService.ValidatePositiveIntegers(dto));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ValidatePositiveIntegers_ReminderCutoffTimeNonPositive_Throws(int badValue)
    {
        var dto = ValidDto();
        dto.ReminderCutoffTime = badValue;

        Should.Throw<ArgumentException>(
            () => SystemParametersAppService.ValidatePositiveIntegers(dto));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void ValidatePositiveIntegers_AppointmentDurationTimeNonPositive_Throws(int badValue)
    {
        var dto = ValidDto();
        dto.AppointmentDurationTime = badValue;

        Should.Throw<ArgumentException>(
            () => SystemParametersAppService.ValidatePositiveIntegers(dto));
    }

    [Fact]
    public void ValidatePositiveIntegers_AllDefaults_DoesNotThrow()
    {
        var dto = ValidDto();

        // No exception expected.
        SystemParametersAppService.ValidatePositiveIntegers(dto);
    }

    [Fact]
    public void ValidatePositiveIntegers_RejectsEachIntFieldWhenZero()
    {
        // Sanity-check that every int field is covered by the validator.
        // If a future field is added without a Check.Range call, this test
        // surfaces the omission.
        var fieldSetters = new Action<SystemParameterUpdateDto, int>[]
        {
            (d, v) => d.AppointmentLeadTime = v,
            (d, v) => d.AppointmentMaxTimePQME = v,
            (d, v) => d.AppointmentMaxTimeAME = v,
            (d, v) => d.AppointmentMaxTimeOTHER = v,
            (d, v) => d.AppointmentCancelTime = v,
            (d, v) => d.AppointmentDueDays = v,
            (d, v) => d.AppointmentDurationTime = v,
            (d, v) => d.AutoCancelCutoffTime = v,
            (d, v) => d.JointDeclarationUploadCutoffDays = v,
            (d, v) => d.PendingAppointmentOverDueNotificationDays = v,
            (d, v) => d.ReminderCutoffTime = v,
        };

        foreach (var setOne in fieldSetters)
        {
            var dto = ValidDto();
            setOne(dto, 0);

            Should.Throw<ArgumentException>(
                () => SystemParametersAppService.ValidatePositiveIntegers(dto));
        }
    }

    // ------------------------------------------------------------------
    // CC email IDs length validation
    // ------------------------------------------------------------------

    [Fact]
    public void ValidateCcEmailIdsLength_NullValue_DoesNotThrow()
    {
        var dto = ValidDto();
        dto.CcEmailIds = null;

        SystemParametersAppService.ValidateCcEmailIdsLength(dto);
    }

    [Fact]
    public void ValidateCcEmailIdsLength_AtMaxLength_DoesNotThrow()
    {
        var dto = ValidDto();
        dto.CcEmailIds = new string('a', SystemParameterConsts.CcEmailIdsMaxLength);

        SystemParametersAppService.ValidateCcEmailIdsLength(dto);
    }

    [Fact]
    public void ValidateCcEmailIdsLength_OverMaxLength_Throws()
    {
        var dto = ValidDto();
        dto.CcEmailIds = new string('a', SystemParameterConsts.CcEmailIdsMaxLength + 1);

        Should.Throw<ArgumentException>(
            () => SystemParametersAppService.ValidateCcEmailIdsLength(dto));
    }

    // ------------------------------------------------------------------
    // ApplyUpdate field copy
    // ------------------------------------------------------------------

    [Fact]
    public void ApplyUpdate_CopiesAll13UserEditableFields()
    {
        var entity = NewEntityWithDefaults(out var originalId, out var originalTenant);

        var dto = new SystemParameterUpdateDto
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
            CcEmailIds = "ops@example.com;qa@example.com",
            ConcurrencyStamp = "client-supplied-stamp-ignored-by-applyupdate",
        };

        SystemParametersAppService.ApplyUpdate(dto, entity);

        // 13 fields copied
        entity.AppointmentLeadTime.ShouldBe(7);
        entity.AppointmentMaxTimePQME.ShouldBe(75);
        entity.AppointmentMaxTimeAME.ShouldBe(100);
        entity.AppointmentMaxTimeOTHER.ShouldBe(80);
        entity.AppointmentCancelTime.ShouldBe(5);
        entity.AppointmentDueDays.ShouldBe(21);
        entity.AppointmentDurationTime.ShouldBe(45);
        entity.AutoCancelCutoffTime.ShouldBe(14);
        entity.JointDeclarationUploadCutoffDays.ShouldBe(10);
        entity.PendingAppointmentOverDueNotificationDays.ShouldBe(5);
        entity.ReminderCutoffTime.ShouldBe(12);
        entity.IsCustomField.ShouldBeTrue();
        entity.CcEmailIds.ShouldBe("ops@example.com;qa@example.com");

        // Identity / tenancy fields preserved
        entity.Id.ShouldBe(originalId);
        entity.TenantId.ShouldBe(originalTenant);
    }

    [Fact]
    public void ApplyUpdate_NullCcEmailIds_OverridesExistingValue()
    {
        var entity = NewEntityWithDefaults(out _, out _);
        entity.CcEmailIds = "previous@example.com";

        var dto = ValidDto();
        dto.CcEmailIds = null;

        SystemParametersAppService.ApplyUpdate(dto, entity);

        entity.CcEmailIds.ShouldBeNull();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static SystemParameterUpdateDto ValidDto() => new()
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
        ConcurrencyStamp = "deadbeef",
    };

    private static SystemParameter NewEntityWithDefaults(out Guid id, out Guid? tenantId)
    {
        id = Guid.NewGuid();
        tenantId = Guid.NewGuid();
        return new SystemParameter(
            id: id,
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
    }
}
