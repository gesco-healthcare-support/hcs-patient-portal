using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.States;
using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

public class AppointmentEmployerDetailWithNavigationProperties
{
    public AppointmentEmployerDetail AppointmentEmployerDetail { get; set; } = null!;
    public Appointment? Appointment { get; set; }
    public State? State { get; set; }
}