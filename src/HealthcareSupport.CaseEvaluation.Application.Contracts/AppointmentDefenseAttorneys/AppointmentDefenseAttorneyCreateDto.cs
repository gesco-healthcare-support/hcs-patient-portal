using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

public class AppointmentDefenseAttorneyCreateDto
{
    public Guid AppointmentId { get; set; }

    public Guid DefenseAttorneyId { get; set; }

    public Guid IdentityUserId { get; set; }
}
