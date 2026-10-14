using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.PhiAccess;

/// <summary>
/// One read of protected health information: who, what, and when. Ids only, never the data
/// itself, so the log holds no PHI of its own. Append-only: <see cref="PhiAccessRecorder"/>
/// inserts these rows and nothing updates or deletes them.
///
/// <para>WHY IT EXISTS (2026-10-13). The HTTP audit log does not record GET requests
/// (<c>IsEnabledForGetRequests</c> is false, the ABP default), so until now nobody could say who
/// opened a patient's record, revealed an SSN or downloaded a document. Turning on GET auditing
/// globally would log every page load; this table records only the reads that disclose PHI.</para>
/// </summary>
public class PhiAccessLogEntry : Entity<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }

    /// <summary>The signed-in user who read the data.</summary>
    public virtual Guid? UserId { get; protected set; }

    /// <summary>The real person behind an impersonated session, when there was one.</summary>
    public virtual Guid? ImpersonatorUserId { get; protected set; }

    public virtual PhiAccessAction Action { get; protected set; }

    public virtual Guid? PatientId { get; protected set; }

    public virtual Guid? AppointmentId { get; protected set; }

    /// <summary>The document or packet downloaded, for the two download actions.</summary>
    public virtual Guid? DocumentId { get; protected set; }

    public virtual DateTime AccessedAt { get; protected set; }

    protected PhiAccessLogEntry()
    {
    }

    public PhiAccessLogEntry(
        Guid id,
        Guid? tenantId,
        Guid? userId,
        Guid? impersonatorUserId,
        PhiAccessAction action,
        PhiAccessTarget target,
        DateTime accessedAt)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        ImpersonatorUserId = impersonatorUserId;
        Action = action;
        PatientId = target.PatientId;
        AppointmentId = target.AppointmentId;
        DocumentId = target.DocumentId;
        AccessedAt = accessedAt;
    }
}
