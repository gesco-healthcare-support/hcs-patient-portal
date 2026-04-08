using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.States;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

public class AppointmentEmployerDetailWithNavigationPropertiesDto
{
    public AppointmentEmployerDetailDto AppointmentEmployerDetail { get; set; } = null!;
    public AppointmentDto Appointment { get; set; } = null!;
    public StateDto? State { get; set; }
}