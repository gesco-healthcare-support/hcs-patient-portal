using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.Appointments;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

public class AppointmentAccessorWithNavigationPropertiesDto
{
    public AppointmentAccessorDto AppointmentAccessor { get; set; } = null!;
    public IdentityUserDto IdentityUser { get; set; } = null!;
    public AppointmentDto Appointment { get; set; } = null!;
}