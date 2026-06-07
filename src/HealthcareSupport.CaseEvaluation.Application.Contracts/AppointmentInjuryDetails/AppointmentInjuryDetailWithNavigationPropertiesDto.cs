using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using System;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

public class AppointmentInjuryDetailWithNavigationPropertiesDto
{
    public AppointmentInjuryDetailDto AppointmentInjuryDetail { get; set; } = null!;
    public AppointmentDto? Appointment { get; set; }
    public WcabOfficeDto? WcabOffice { get; set; }
    public List<AppointmentBodyPartDto> BodyParts { get; set; } = new();
}
