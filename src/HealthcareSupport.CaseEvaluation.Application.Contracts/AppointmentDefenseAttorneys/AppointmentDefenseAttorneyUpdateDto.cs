using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

public class AppointmentDefenseAttorneyUpdateDto : IHasConcurrencyStamp
{
    public Guid AppointmentId { get; set; }

    public Guid DefenseAttorneyId { get; set; }

    public Guid IdentityUserId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}
