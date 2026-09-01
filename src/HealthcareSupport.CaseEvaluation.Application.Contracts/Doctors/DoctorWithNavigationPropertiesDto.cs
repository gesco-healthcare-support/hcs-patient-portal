using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Locations;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public class DoctorWithNavigationPropertiesDto
{
    public DoctorDto Doctor { get; set; } = null!;

    public List<AppointmentTypeDto> AppointmentTypes { get; set; } = new List<AppointmentTypeDto>();
    public List<LocationDto> Locations { get; set; } = new List<LocationDto>();
}