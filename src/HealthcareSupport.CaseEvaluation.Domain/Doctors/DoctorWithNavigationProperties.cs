using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Locations;
using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.Doctors;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public class DoctorWithNavigationProperties
{
    public Doctor Doctor { get; set; } = null!;

    public List<AppointmentType> AppointmentTypes { get; set; } = null!;
    public List<Location> Locations { get; set; } = null!;
}