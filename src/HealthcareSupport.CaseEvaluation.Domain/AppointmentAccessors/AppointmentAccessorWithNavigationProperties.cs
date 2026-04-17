using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.Appointments;
using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

public class AppointmentAccessorWithNavigationProperties
{
    public AppointmentAccessor AppointmentAccessor { get; set; } = null!;
    public IdentityUser? IdentityUser { get; set; }
    public Appointment? Appointment { get; set; }
}