using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

public class AppointmentAccessorCreateDto
{
    public AccessType AccessTypeId { get; set; } = Enum.GetValues<AccessType>()[0];
    public Guid IdentityUserId { get; set; }

    public Guid AppointmentId { get; set; }
}