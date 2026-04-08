using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

public class AppointmentApplicantAttorneyCreateDto
{
    public Guid AppointmentId { get; set; }

    public Guid ApplicantAttorneyId { get; set; }

    public Guid IdentityUserId { get; set; }
}