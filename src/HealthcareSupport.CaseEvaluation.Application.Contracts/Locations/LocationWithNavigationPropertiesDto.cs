using HealthcareSupport.CaseEvaluation.States;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Locations;

public class LocationWithNavigationPropertiesDto
{
    public LocationDto Location { get; set; } = null!;
    public StateDto? State { get; set; }

    public AppointmentTypeDto? AppointmentType { get; set; }
}