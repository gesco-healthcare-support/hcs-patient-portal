using System;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.SystemParameters;

/// <summary>
/// Per-tenant singleton holding the booking, cancel, reschedule, JDF,
/// reminder, and custom-field gates that the appointment workflow reads at
/// runtime. Mirrors OLD's <c>SystemParameter</c> table verbatim
/// (Phase 1.1, 2026-05-01). Exactly one row per tenant; the AppService
/// exposes only Get + Update, never Create / Delete.
/// </summary>
[Audited]
public class SystemParameter : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    /// <summary>Minimum lead-time (days) between today and the appointment slot for external users.</summary>
    public virtual int AppointmentLeadTime { get; set; }

    /// <summary>Max booking horizon (days) for PQME / PQME-REVAL appointments.</summary>
    public virtual int AppointmentMaxTimePQME { get; set; }

    /// <summary>Max booking horizon (days) for AME / AME-REVAL appointments.</summary>
    public virtual int AppointmentMaxTimeAME { get; set; }

    /// <summary>Max booking horizon (days) for OTHER appointments.</summary>
    public virtual int AppointmentMaxTimeOTHER { get; set; }

    /// <summary>Minimum (days) between today and the slot to allow a cancellation request.</summary>
    public virtual int AppointmentCancelTime { get; set; }

    /// <summary>Default number of days between approval and package-document due date.</summary>
    public virtual int AppointmentDueDays { get; set; }

    /// <summary>Default slot duration in minutes when generating doctor-availability slots.</summary>
    public virtual int AppointmentDurationTime { get; set; }

    /// <summary>Cutoff (days) for the JDF auto-cancel job to fire before the appointment date.</summary>
    public virtual int AutoCancelCutoffTime { get; set; }

    /// <summary>Cutoff (days) before the AME appointment for the JDF upload reminder.</summary>
    public virtual int JointDeclarationUploadCutoffDays { get; set; }

    /// <summary>Cutoff (days) for overdue-notification emails on Pending requests.</summary>
    public virtual int PendingAppointmentOverDueNotificationDays { get; set; }

    /// <summary>Cutoff (days) before the appointment for the day-of reminder email.</summary>
    public virtual int ReminderCutoffTime { get; set; }

    /// <summary>Gates whether IT-Admin-defined custom fields render on the booking intake form.</summary>
    public virtual bool IsCustomField { get; set; }

    /// <summary>Comma-separated CC list applied to outbound notifications.</summary>
    [CanBeNull]
    public virtual string? CcEmailIds { get; set; }

    protected SystemParameter()
    {
    }

    public SystemParameter(
        Guid id,
        Guid? tenantId,
        int appointmentLeadTime,
        int appointmentMaxTimePQME,
        int appointmentMaxTimeAME,
        int appointmentMaxTimeOTHER,
        int appointmentCancelTime,
        int appointmentDueDays,
        int appointmentDurationTime,
        int autoCancelCutoffTime,
        int jointDeclarationUploadCutoffDays,
        int pendingAppointmentOverDueNotificationDays,
        int reminderCutoffTime,
        bool isCustomField,
        string? ccEmailIds = null)
    {
        Id = id;
        TenantId = tenantId;
        Check.Range(appointmentLeadTime, nameof(appointmentLeadTime), 1, int.MaxValue);
        Check.Range(appointmentMaxTimePQME, nameof(appointmentMaxTimePQME), 1, int.MaxValue);
        Check.Range(appointmentMaxTimeAME, nameof(appointmentMaxTimeAME), 1, int.MaxValue);
        Check.Range(appointmentMaxTimeOTHER, nameof(appointmentMaxTimeOTHER), 1, int.MaxValue);
        Check.Range(appointmentCancelTime, nameof(appointmentCancelTime), 1, int.MaxValue);
        Check.Range(appointmentDueDays, nameof(appointmentDueDays), 1, int.MaxValue);
        Check.Range(appointmentDurationTime, nameof(appointmentDurationTime), 1, int.MaxValue);
        Check.Range(autoCancelCutoffTime, nameof(autoCancelCutoffTime), 1, int.MaxValue);
        Check.Range(jointDeclarationUploadCutoffDays, nameof(jointDeclarationUploadCutoffDays), 1, int.MaxValue);
        Check.Range(pendingAppointmentOverDueNotificationDays, nameof(pendingAppointmentOverDueNotificationDays), 1, int.MaxValue);
        Check.Range(reminderCutoffTime, nameof(reminderCutoffTime), 1, int.MaxValue);
        AppointmentLeadTime = appointmentLeadTime;
        AppointmentMaxTimePQME = appointmentMaxTimePQME;
        AppointmentMaxTimeAME = appointmentMaxTimeAME;
        AppointmentMaxTimeOTHER = appointmentMaxTimeOTHER;
        AppointmentCancelTime = appointmentCancelTime;
        AppointmentDueDays = appointmentDueDays;
        AppointmentDurationTime = appointmentDurationTime;
        AutoCancelCutoffTime = autoCancelCutoffTime;
        JointDeclarationUploadCutoffDays = jointDeclarationUploadCutoffDays;
        PendingAppointmentOverDueNotificationDays = pendingAppointmentOverDueNotificationDays;
        ReminderCutoffTime = reminderCutoffTime;
        IsCustomField = isCustomField;
        CcEmailIds = ccEmailIds;
    }
}
