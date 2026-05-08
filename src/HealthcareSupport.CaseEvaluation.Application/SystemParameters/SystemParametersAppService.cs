using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.SystemParameters;

/// <summary>
/// Per-tenant <c>SystemParameter</c> singleton AppService. Only Get and
/// Update are exposed -- the singleton-per-tenant invariant is enforced by
/// the data seed contributor; create and delete have no business meaning
/// and would silently break the booking / cancel / reschedule / JDF gates.
///
/// Authorization (Phase 2.5):
///   - Class-level <c>[Authorize(SystemParameters.Default)]</c> gates Get
///     for all internal roles.
///   - <c>UpdateAsync</c> overrides with <c>[Authorize(SystemParameters.Edit)]</c>
///     so Clinic Staff (read-only) sees 403 on PUT.
///
/// Strict-parity notes:
///   - Mirrors OLD <c>SystemParametersController</c>'s GET-by-id and PUT
///     semantics.
///   - Re-applies OLD's `[Range(1, int.MaxValue)]` entity-level constraint
///     on every int via <see cref="ValidatePositiveIntegers"/>; OLD
///     enforced this only at insert time -- restoring symmetry on update
///     is treated as the OLD-bug-fix exception.
///   - Optimistic concurrency via <see cref="ConcurrencyStamp"/> -- additive
///     safety; OLD lacked it.
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.SystemParameters.Default)]
public class SystemParametersAppService : ApplicationService, ISystemParametersAppService
{
    private readonly ISystemParameterRepository _repository;

    public SystemParametersAppService(ISystemParameterRepository repository)
    {
        _repository = repository;
    }

    public virtual async Task<SystemParameterDto> GetAsync()
    {
        var entity = await _repository.GetCurrentTenantAsync();
        if (entity == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.SystemParameterNotSeeded);
        }
        return ObjectMapper.Map<SystemParameter, SystemParameterDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.SystemParameters.Edit)]
    public virtual async Task<SystemParameterDto> UpdateAsync(SystemParameterUpdateDto input)
    {
        Check.NotNull(input, nameof(input));
        ValidatePositiveIntegers(input);
        ValidateCcEmailIdsLength(input);

        var entity = await _repository.GetCurrentTenantAsync();
        if (entity == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.SystemParameterNotSeeded);
        }

        // ABP's optimistic concurrency: assigning the client-supplied stamp
        // makes EF Core include WHERE ConcurrencyStamp=@old in the UPDATE
        // statement. A mismatch surfaces as AbpDbConcurrencyException
        // (HTTP 409). Skip when the client did not round-trip a stamp --
        // tests / first-login flows may not have one yet.
        if (!string.IsNullOrEmpty(input.ConcurrencyStamp))
        {
            entity.ConcurrencyStamp = input.ConcurrencyStamp;
        }

        ApplyUpdate(input, entity);

        await _repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<SystemParameter, SystemParameterDto>(entity);
    }

    /// <summary>
    /// Hand-rolled field copy from update DTO onto the existing entity.
    /// Mapperly's <c>RequiredMappingStrategy.Target</c> would require a
    /// public parameterless constructor on <see cref="SystemParameter"/>,
    /// but the entity intentionally keeps its constructor protected to
    /// enforce the singleton-per-tenant invariant (only
    /// <c>SystemParameterDataSeedContributor</c> may instantiate rows).
    /// 13 explicit assignments is clearer than fighting the source-gen
    /// constraint and matches the user-editable surface verbatim.
    /// Internal so unit tests can verify the field copy without ABP infra.
    /// </summary>
    internal static void ApplyUpdate(SystemParameterUpdateDto source, SystemParameter destination)
    {
        destination.AppointmentLeadTime = source.AppointmentLeadTime;
        destination.AppointmentMaxTimePQME = source.AppointmentMaxTimePQME;
        destination.AppointmentMaxTimeAME = source.AppointmentMaxTimeAME;
        destination.AppointmentMaxTimeOTHER = source.AppointmentMaxTimeOTHER;
        destination.AppointmentCancelTime = source.AppointmentCancelTime;
        destination.AppointmentDueDays = source.AppointmentDueDays;
        destination.AppointmentDurationTime = source.AppointmentDurationTime;
        destination.AutoCancelCutoffTime = source.AutoCancelCutoffTime;
        destination.JointDeclarationUploadCutoffDays = source.JointDeclarationUploadCutoffDays;
        destination.PendingAppointmentOverDueNotificationDays = source.PendingAppointmentOverDueNotificationDays;
        destination.ReminderCutoffTime = source.ReminderCutoffTime;
        destination.IsCustomField = source.IsCustomField;
        destination.CcEmailIds = source.CcEmailIds;
    }

    /// <summary>
    /// Mirrors OLD's `[Range(1, int.MaxValue)]` attribute on every int
    /// column of the <c>SystemParameter</c> table. Re-applied at the
    /// AppService layer because:
    ///   - DataAnnotations on the DTO are validated by ASP.NET Core only on
    ///     the controller pipeline, not when the AppService is invoked
    ///     directly (e.g. from tests or in-process callers).
    ///   - Entity constructor `Check.Range` only fires on Add, not Update.
    /// Internal (not private) so unit tests in the test project can verify
    /// the validator without standing up the full ABP integration harness.
    /// </summary>
    internal static void ValidatePositiveIntegers(SystemParameterUpdateDto input)
    {
        Check.Range(input.AppointmentLeadTime, nameof(input.AppointmentLeadTime), 1, int.MaxValue);
        Check.Range(input.AppointmentMaxTimePQME, nameof(input.AppointmentMaxTimePQME), 1, int.MaxValue);
        Check.Range(input.AppointmentMaxTimeAME, nameof(input.AppointmentMaxTimeAME), 1, int.MaxValue);
        Check.Range(input.AppointmentMaxTimeOTHER, nameof(input.AppointmentMaxTimeOTHER), 1, int.MaxValue);
        Check.Range(input.AppointmentCancelTime, nameof(input.AppointmentCancelTime), 1, int.MaxValue);
        Check.Range(input.AppointmentDueDays, nameof(input.AppointmentDueDays), 1, int.MaxValue);
        Check.Range(input.AppointmentDurationTime, nameof(input.AppointmentDurationTime), 1, int.MaxValue);
        Check.Range(input.AutoCancelCutoffTime, nameof(input.AutoCancelCutoffTime), 1, int.MaxValue);
        Check.Range(input.JointDeclarationUploadCutoffDays, nameof(input.JointDeclarationUploadCutoffDays), 1, int.MaxValue);
        Check.Range(input.PendingAppointmentOverDueNotificationDays, nameof(input.PendingAppointmentOverDueNotificationDays), 1, int.MaxValue);
        Check.Range(input.ReminderCutoffTime, nameof(input.ReminderCutoffTime), 1, int.MaxValue);
    }

    internal static void ValidateCcEmailIdsLength(SystemParameterUpdateDto input)
    {
        if (input.CcEmailIds == null)
        {
            return;
        }
        Check.Length(input.CcEmailIds, nameof(input.CcEmailIds), SystemParameterConsts.CcEmailIdsMaxLength);
    }
}
