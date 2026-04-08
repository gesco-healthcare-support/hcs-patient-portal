using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

public class AppointmentApplicantAttorneyUpdateDto : IHasConcurrencyStamp
{
    public Guid AppointmentId { get; set; }

    public Guid ApplicantAttorneyId { get; set; }

    public Guid IdentityUserId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}