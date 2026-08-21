using System;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.Locations;

/// <summary>
/// I3 (2026-06-08) -- M2M join between <see cref="Location"/> and
/// <see cref="AppointmentType"/>: the appointment types offered at a clinic
/// location. Composite primary key on (LocationId, AppointmentTypeId). Host-
/// scoped because <see cref="Location"/> is not IMultiTenant, so -- unlike
/// DoctorAvailabilityAppointmentType -- this join carries no TenantId.
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
