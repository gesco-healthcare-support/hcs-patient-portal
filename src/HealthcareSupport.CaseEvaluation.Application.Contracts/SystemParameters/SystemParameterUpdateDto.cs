using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.SystemParameters;

/// <summary>
/// Input DTO for <c>SystemParametersAppService.UpdateAsync</c>. Mirrors OLD's
/// edit-form payload (12 int day-fields + IsCustomField bool +
/// CcEmailIds string), with three additions:
///
/// 1. <see cref="ConcurrencyStamp"/> -- round-tripped from the read DTO so
///    multi-IT-Admin overlapping edits surface as 409 rather than silently
///    overwrite. OLD lacked this; treated as OLD-bug-fix exception.
/// 2. <c>[Range(1, int.MaxValue)]</c> on every int -- mirrors OLD's
///    entity-level <c>[Range]</c> attributes verbatim and re-applied on the
///    update path (OLD only validated on insert).
/// 3. <c>[StringLength]</c> on CcEmailIds -- mirrors NEW's
///    <see cref="SystemParameterConsts.CcEmailIdsMaxLength"/>; OLD did not
///    enforce a max length but stored in nvarchar(MAX) so this is additive
///    safety with no visible-behavior change.
/// </summary>
public class SystemParameterUpdateDto : IHasConcurrencyStamp
{
    [Range(1, int.MaxValue)]
    public int AppointmentLeadTime { get; set; }

    [Range(1, int.MaxValue)]
    public int AppointmentMaxTimePQME { get; set; }

    [Range(1, int.MaxValue)]
    public int AppointmentMaxTimeAME { get; set; }

    [Range(1, int.MaxValue)]
    public int AppointmentMaxTimeOTHER { get; set; }

    [Range(1, int.MaxValue)]
    public int AppointmentCancelTime { get; set; }

    [Range(1, int.MaxValue)]
    public int AppointmentDueDays { get; set; }

    [Range(1, int.MaxValue)]
    public int AppointmentDurationTime { get; set; }

    [Range(1, int.MaxValue)]
    public int AutoCancelCutoffTime { get; set; }

    [Range(1, int.MaxValue)]
    public int JointDeclarationUploadCutoffDays { get; set; }

    [Range(1, int.MaxValue)]
    public int PendingAppointmentOverDueNotificationDays { get; set; }

    [Range(1, int.MaxValue)]
    public int ReminderCutoffTime { get; set; }

    public bool IsCustomField { get; set; }

    [CanBeNull]
    [StringLength(SystemParameterConsts.CcEmailIdsMaxLength)]
    public string? CcEmailIds { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}
