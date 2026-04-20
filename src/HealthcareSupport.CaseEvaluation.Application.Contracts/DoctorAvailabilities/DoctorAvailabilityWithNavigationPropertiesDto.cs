using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityWithNavigationPropertiesDto
{
    public DoctorAvailabilityDto DoctorAvailability { get; set; } = null!;
    public LocationDto? Location { get; set; }
    public AppointmentTypeDto? AppointmentType { get; set; }
}