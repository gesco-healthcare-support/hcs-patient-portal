using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 (office decision 2) -- an address whose emails are stopped on EVERY case at EVERY practice.
/// Lives in the HOST database only (one list for all offices); read with <c>CurrentTenant.Change(null)</c>.
/// Turning emails back on is a soft delete, so who stopped it, when and why stay in the table.
/// </summary>
public class GlobalEmailStop : FullAuditedAggregateRoot<Guid>
{
    public virtual string Email { get; protected set; } = null!;

    public virtual string NormalizedEmail { get; protected set; } = null!;

    public virtual Guid? StoppedByUserId { get; protected set; }

    public virtual string? StoppedByName { get; protected set; }

    public virtual DateTime StoppedAt { get; protected set; }

    /// <summary>The practice the stop was made from; null when made on the staff site (host).</summary>
    public virtual Guid? StoppedFromOfficeId { get; protected set; }

    public virtual string? StoppedFromOfficeName { get; protected set; }

    public virtual string? Note { get; protected set; }

    protected GlobalEmailStop()
    {
    }

    public GlobalEmailStop(
        Guid id,
        string email,
        Guid? stoppedByUserId,
        string? stoppedByName,
        DateTime stoppedAt,
        Guid? stoppedFromOfficeId,
        string? stoppedFromOfficeName,
        string? note)
        : base(id)
    {
        Email = Check.NotNullOrWhiteSpace(email, nameof(email), EmailStopConsts.EmailMaxLength).Trim();
        NormalizedEmail = EmailStopAddress.Normalize(Email)!;
        StoppedByUserId = stoppedByUserId;
        StoppedByName = Truncate(stoppedByName, EmailStopConsts.NameMaxLength);
        StoppedAt = stoppedAt;
        StoppedFromOfficeId = stoppedFromOfficeId;
        StoppedFromOfficeName = Truncate(stoppedFromOfficeName, EmailStopConsts.OfficeNameMaxLength);
        Note = string.IsNullOrWhiteSpace(note)
            ? null
            : Check.Length(note.Trim(), nameof(note), EmailStopConsts.NoteMaxLength);
    }

    internal static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
