using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using System;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

public class AppointmentInjuryDetailWithNavigationProperties
{
    public AppointmentInjuryDetail AppointmentInjuryDetail { get; set; } = null!;
    public Appointment? Appointment { get; set; }
    public WcabOffice? WcabOffice { get; set; }
    public List<AppointmentBodyPart> BodyParts { get; set; } = new();
    public AppointmentClaimExaminer? ClaimExaminer { get; set; }
    public AppointmentPrimaryInsurance? PrimaryInsurance { get; set; }
}
