using System;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Staff-initiated "Send Back / Request more information" on a Pending
/// appointment (redesign, 2026-06-14). Staff flag specific booking fields and
/// add a note; the appointment moves Pending -&gt; InfoRequested. The external
/// user edits ONLY the flagged fields and resubmits, which marks this row
/// Resolved and moves the appointment back to Pending.
///
/// One row per send-back round (a new send-back opens a new Open row), so the
/// table is the full request history. The Note + RequestedFields are exposed to
/// the external user un-masked (unlike InternalUserComments).
/// </summary>
[Audited]
public class AppointmentInfoRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual Guid AppointmentId { get; protected set; }

    /// <summary>Staff note shown to the external user verbatim (email + fix-it page).</summary>
    public virtual string Note { get; protected set; } = string.Empty;

    /// <summary>
    /// JSON array of the field keys the external user must fix (e.g.
    /// <c>["panelNumber","dob","docStrike"]</c>) plus optional per-field hints,
    /// serialized by the Application layer. Opaque to the domain.
    /// </summary>
    public virtual string RequestedFields { get; protected set; } = "[]";

    public virtual InfoRequestStatus Status { get; protected set; }

    /// <summary>IdentityUser id of the staff member who sent it back.</summary>
    public virtual Guid? RequestedByUserId { get; protected set; }

    /// <summary>UTC timestamp the external user resubmitted (Status -&gt; Resolved).</summary>
    public virtual DateTime? ResolvedAt { get; protected set; }

    protected AppointmentInfoRequest()
    {
    }

    public AppointmentInfoRequest(
        Guid id,
        Guid? tenantId,
        Guid appointmentId,
        string note,
        string requestedFields,
        Guid? requestedByUserId)
    {
        Id = id;
        TenantId = tenantId;
        AppointmentId = appointmentId;
        Check.NotNullOrWhiteSpace(note, nameof(note));
        Check.Length(note, nameof(note), AppointmentInfoRequestConsts.NoteMaxLength);
        Check.Length(requestedFields, nameof(requestedFields), AppointmentInfoRequestConsts.RequestedFieldsMaxLength);
        Note = note;
        RequestedFields = string.IsNullOrWhiteSpace(requestedFields) ? "[]" : requestedFields;
        RequestedByUserId = requestedByUserId;
        Status = InfoRequestStatus.Open;
    }

    /// <summary>Mark this request resolved when the external user resubmits. Idempotent.</summary>
    public void MarkResolved(DateTime nowUtc)
    {
        if (Status == InfoRequestStatus.Resolved)
        {
            return;
        }
        Status = InfoRequestStatus.Resolved;
        ResolvedAt = nowUtc;
    }
}
