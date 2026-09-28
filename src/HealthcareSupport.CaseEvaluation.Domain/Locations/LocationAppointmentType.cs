using System;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.Locations;

/// <summary>
/// I3 (2026-06-08) -- M2M join between <see cref="Location"/> and
/// <see cref="AppointmentType"/>: the appointment types offered at a clinic
/// location. Composite primary key on (LocationId, AppointmentTypeId).
///
/// This join carries no TenantId, unlike DoctorAvailabilityAppointmentType. It does
/// not need one: both sides of the join are themselves <c>IMultiTenant</c>, so a row
/// is only reachable through rows already scoped to the office, and the composite key
/// is unique within that office's database. (An earlier version of this comment gave
/// the reason as "<see cref="Location"/> is not IMultiTenant", which was wrong -- it is.)
/// </summary>
public class LocationAppointmentType : Entity
{
    public Guid LocationId { get; protected set; }

    public Guid AppointmentTypeId { get; protected set; }

    public virtual AppointmentType AppointmentType { get; protected set; } = null!;

    protected LocationAppointmentType()
    {
    }

    public LocationAppointmentType(Guid locationId, Guid appointmentTypeId)
    {
        LocationId = locationId;
        AppointmentTypeId = appointmentTypeId;
    }

    public override object[] GetKeys()
    {
        return new object[] { LocationId, AppointmentTypeId };
    }
}
