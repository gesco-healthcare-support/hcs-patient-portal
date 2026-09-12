using HealthcareSupport.CaseEvaluation.States;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.Locations;

namespace HealthcareSupport.CaseEvaluation.Locations;

public class LocationWithNavigationProperties
{
    public Location Location { get; set; } = null!;
    public State? State { get; set; }

    public List<AppointmentType> AppointmentTypes { get; set; } = new();
}