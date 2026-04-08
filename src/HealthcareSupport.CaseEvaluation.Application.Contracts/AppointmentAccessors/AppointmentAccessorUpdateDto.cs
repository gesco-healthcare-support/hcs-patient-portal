using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

public class AppointmentAccessorUpdateDto
{
    public AccessType AccessTypeId { get; set; }

    public Guid IdentityUserId { get; set; }

    public Guid AppointmentId { get; set; }
}