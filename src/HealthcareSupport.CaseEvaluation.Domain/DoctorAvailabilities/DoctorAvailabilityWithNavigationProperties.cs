using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityWithNavigationProperties
{
    public DoctorAvailability DoctorAvailability { get; set; } = null!;
    public Location Location { get; set; } = null!;
    public AppointmentType? AppointmentType { get; set; }
}