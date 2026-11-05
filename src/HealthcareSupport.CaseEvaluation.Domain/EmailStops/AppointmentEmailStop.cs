using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 (office decision 2) -- an address whose emails are stopped on ONE appointment only. Lives in
/// the office database, in its own table rather than on the appointment, so stopping someone's emails never
/// updates the appointment row (and so never re-sends the case to the Case Tracker feed). Turning emails back
/// on is a soft delete, so the history stays.
/// </summary>
public class AppointmentEmailStop : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual Guid AppointmentId { get; protected set; }

    public virtual string Email { get; protected set; } = null!;

    public virtual string NormalizedEmail { get; protected set; } = null!;

    public virtual Guid? StoppedByUserId { get; protected set; }

    public virtual string? StoppedByName { get; protected set; }

    public virtual DateTime StoppedAt { get; protected set; }

    public virtual string? Note { get; protected set; }

    protected AppointmentEmailStop()
    {
    }

    public AppointmentEmailStop(
        Guid id,
        Guid? tenantId,
        Guid appointmentId,
        string email,
        Guid? stoppedByUserId,
        string? stoppedByName,
        DateTime stoppedAt,
        string? note)
        : base(id)
    {
        TenantId = tenantId;
        AppointmentId = appointmentId;
        Email = Check.NotNullOrWhiteSpace(email, nameof(email), EmailStopConsts.EmailMaxLength).Trim();
        NormalizedEmail = EmailStopAddress.Normalize(Email)!;
        StoppedByUserId = stoppedByUserId;
        StoppedByName = GlobalEmailStop.Truncate(stoppedByName, EmailStopConsts.NameMaxLength);
        StoppedAt = stoppedAt;
        Note = string.IsNullOrWhiteSpace(note)
            ? null
            : Check.Length(note.Trim(), nameof(note), EmailStopConsts.NoteMaxLength);
    }
}
