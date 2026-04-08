using HealthcareSupport.CaseEvaluation.Locations;
using System;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public class DoctorLocation : Entity
{
    public Guid DoctorId { get; protected set; }
    public virtual Doctor Doctor { get; set; }

    public Guid LocationId { get; protected set; }
    public virtual Location Location { get; set; }

    private DoctorLocation()
    {
    }

    public DoctorLocation(Guid doctorId, Guid locationId)
    {
        DoctorId = doctorId;
        LocationId = locationId;
    }

    public override object[] GetKeys()
    {
        return new object[] {
            DoctorId,
            LocationId
        };
    }
}