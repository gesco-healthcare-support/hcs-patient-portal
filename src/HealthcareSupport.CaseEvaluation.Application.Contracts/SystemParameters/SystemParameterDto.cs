using System;
using JetBrains.Annotations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.SystemParameters;

/// <summary>
/// Read DTO for the per-tenant <c>SystemParameter</c> singleton. Surfaces all
/// 14 fields IT Admin can manage (12 ints + 1 bool toggle + nullable
/// CcEmailIds free-text). Carries <see cref="ConcurrencyStamp"/> so the
/// client can round-trip it on update for optimistic concurrency.
///
/// Mirrors OLD's <c>vSystemParameter</c> view fields verbatim plus ABP's
/// audit columns (CreatorId / CreationTime / LastModifierId /
/// LastModificationTime / IsDeleted / DeleterId / DeletionTime via
/// <see cref="FullAuditedEntityDto{TKey}"/>).
/// </summary>
public class SystemParameterDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public Guid? TenantId { get; set; }

    public int AppointmentLeadTime { get; set; }
    public int AppointmentMaxTimePQME { get; set; }
    public int AppointmentMaxTimeAME { get; set; }
    public int AppointmentMaxTimeOTHER { get; set; }
    public int AppointmentMaxTimeInternal { get; set; }
    public int AppointmentCancelTime { get; set; }
    public int AppointmentDueDays { get; set; }
    public int AppointmentDurationTime { get; set; }
    public int AutoCancelCutoffTime { get; set; }
    public int JointDeclarationUploadCutoffDays { get; set; }
    public int PendingAppointmentOverDueNotificationDays { get; set; }
    public int ReminderCutoffTime { get; set; }
    public bool IsCustomField { get; set; }

    [CanBeNull]
    public string? CcEmailIds { get; set; }

    /// <summary>
    /// QA item 13: the office inbox that receives the intake-staff notices
    /// (cancel-request, booking-submission, pending digest). Backed by the ABP
    /// setting <c>NotificationsPolicy.OfficeEmail</c>, not a column -- surfaced
    /// here so staff can set it. Empty = those office notifications are skipped.
    /// </summary>
    public string? OfficeEmail { get; set; }

    /// <summary>
    /// Issue #4a (2026-07-16): master email on/off switch. Backed by the ABP setting
    /// <c>NotificationsPolicy.EmailEnabled</c> (not a column). When false, the outbox
    /// drain holds all email Pending until re-enabled. Default true.
    /// </summary>
    public bool EmailEnabled { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}
