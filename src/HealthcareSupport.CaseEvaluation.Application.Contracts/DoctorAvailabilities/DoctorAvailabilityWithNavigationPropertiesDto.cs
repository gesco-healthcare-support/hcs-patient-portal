using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityWithNavigationPropertiesDto
{
    public DoctorAvailabilityDto DoctorAvailability { get; set; } = null!;
    public LocationDto? Location { get; set; }

    /// <summary>
    /// 2026-05-15 -- the materialized AppointmentTypes this slot accepts.
    /// Empty list means "any type accepted".
    /// </summary>
    public List<AppointmentTypeDto> AppointmentTypes { get; set; } = new();
}
