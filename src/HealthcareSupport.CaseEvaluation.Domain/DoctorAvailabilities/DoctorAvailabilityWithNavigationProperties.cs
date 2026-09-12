using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityWithNavigationProperties
{
    public DoctorAvailability DoctorAvailability { get; set; } = null!;
    public Location? Location { get; set; }

    /// <summary>
    /// 2026-05-15 -- the set of AppointmentTypes this slot accepts,
    /// materialized via the EF repository's join projection. Empty list
    /// means "any type accepted".
    /// </summary>
    public List<AppointmentType> AppointmentTypes { get; set; } = new();
}
