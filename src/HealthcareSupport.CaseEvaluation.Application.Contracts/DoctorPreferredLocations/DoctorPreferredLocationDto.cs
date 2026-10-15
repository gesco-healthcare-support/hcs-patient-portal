using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.DoctorPreferredLocations;

public class DoctorPreferredLocationDto : EntityDto
{
    public Guid? TenantId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid LocationId { get; set; }
    public bool IsActive { get; set; }
}
