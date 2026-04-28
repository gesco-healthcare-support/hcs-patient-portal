using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Captures the office's send-back-for-info action: a list of appointment-form
/// field names the booker must revisit, plus a freeform note. Per T11
/// (`docs/product/cross-cutting/appointment-lifecycle.md`), the booker's
/// response screen highlights the flagged fields and shows the note alongside.
///
/// W1 stores <c>FlaggedFields</c> as a JSON-serialised <c>string[]</c> in
/// <see cref="FlaggedFieldsJson"/>; getters/setters expose the typed list.
/// Strongly-typed field enum is deferred until W2 custom-fields lands the
/// canonical form-field registry (see deferred-from-mvp.md ledger).
///
/// Multiple send-back rounds are allowed (Pending -> AwaitingMoreInfo,
/// SaveAndResubmit, re-send-back). Each round inserts a new row; the most
/// recent row drives the AwaitingMoreInfo response screen.
/// </summary>
public class AppointmentSendBackInfo : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual Guid AppointmentId { get; set; }

    /// <summary>JSON array of appointment-form field names. Backing storage for <see cref="FlaggedFields"/>.</summary>
    public virtual string FlaggedFieldsJson { get; set; } = "[]";

    public virtual string? Note { get; set; }

    public virtual DateTime SentBackAt { get; set; }

    public virtual Guid? SentBackByUserId { get; set; }

    /// <summary>Marks this row as the booker's resolution of the send-back (response received). Set when the appointment auto-transitions back to Pending.</summary>
    public virtual bool IsResolved { get; set; }

    public virtual DateTime? ResolvedAt { get; set; }

    protected AppointmentSendBackInfo()
    {
    }

    public AppointmentSendBackInfo(
        Guid id,
        Guid? tenantId,
        Guid appointmentId,
        IEnumerable<string> flaggedFields,
        string? note,
        Guid? sentBackByUserId)
    {
        Id = id;
        TenantId = tenantId;
        AppointmentId = appointmentId;
        FlaggedFieldsJson = JsonSerializer.Serialize((flaggedFields ?? Enumerable.Empty<string>()).ToArray());
        Note = note;
        SentBackAt = DateTime.UtcNow;
        SentBackByUserId = sentBackByUserId;
        IsResolved = false;
    }

    public IReadOnlyList<string> GetFlaggedFields()
    {
        if (string.IsNullOrWhiteSpace(FlaggedFieldsJson))
        {
            return Array.Empty<string>();
        }

        return JsonSerializer.Deserialize<string[]>(FlaggedFieldsJson) ?? Array.Empty<string>();
    }

    public void MarkResolved()
    {
        IsResolved = true;
        ResolvedAt = DateTime.UtcNow;
    }
}
