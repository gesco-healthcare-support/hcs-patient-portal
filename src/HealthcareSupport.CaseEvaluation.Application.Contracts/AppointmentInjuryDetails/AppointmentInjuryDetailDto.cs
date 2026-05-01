using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

public class AppointmentInjuryDetailDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public Guid AppointmentId { get; set; }
    public DateTime DateOfInjury { get; set; }
    public DateTime? ToDateOfInjury { get; set; }
    public string ClaimNumber { get; set; } = null!;
    public bool IsCumulativeInjury { get; set; }
    public string? WcabAdj { get; set; }
    public string BodyPartsSummary { get; set; } = null!;
    public Guid? WcabOfficeId { get; set; }
    public string ConcurrencyStamp { get; set; } = null!;
}
