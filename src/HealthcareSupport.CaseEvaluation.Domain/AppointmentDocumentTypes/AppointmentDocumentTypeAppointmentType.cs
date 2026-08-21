using System;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

/// <summary>
/// #4 (2026-06-19) -- M2M join between <see cref="AppointmentDocumentType"/> and
/// an appointment type: the appointment types a single document category is
/// offered for. Composite primary key on (AppointmentDocumentTypeId,
/// AppointmentTypeId).
///
/// <para>Unlike <c>LocationAppointmentType</c>, this join carries a LOOSE
/// <see cref="AppointmentTypeId"/> Guid with NO foreign key and NO navigation:
/// <see cref="AppointmentDocumentType"/> is tenant-scoped (lives in the tenant
/// DBs) while the AppointmentType lookup is host-only, so a constraint cannot
/// span the two databases -- the same reason the parent's old single
/// AppointmentTypeId column was FK-less. The join is scoped to a tenant through
/// its required parent, not its own TenantId.</para>
/// </summary>
public class AppointmentDocumentTypeAppointmentType : Entity
{
    public Guid AppointmentDocumentTypeId { get; protected set; }

    public Guid AppointmentTypeId { get; protected set; }

    /// <summary>The owning document category. Drives the cascade-delete and the
    /// soft-delete query filter; the parent's IMultiTenant filter follows
    /// through this navigation.</summary>
    public virtual AppointmentDocumentType AppointmentDocumentType { get; protected set; } = null!;

    protected AppointmentDocumentTypeAppointmentType()
    {
    }

    public AppointmentDocumentTypeAppointmentType(Guid appointmentDocumentTypeId, Guid appointmentTypeId)
    {
        AppointmentDocumentTypeId = appointmentDocumentTypeId;
        AppointmentTypeId = appointmentTypeId;
    }

    public override object[] GetKeys()
    {
        return new object[] { AppointmentDocumentTypeId, AppointmentTypeId };
    }
}
