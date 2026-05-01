using System;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentBodyParts;

public class AppointmentBodyPartDto : FullAuditedEntityDto<Guid>
{
    public Guid AppointmentInjuryDetailId { get; set; }
    public string BodyPartDescription { get; set; } = null!;
}
