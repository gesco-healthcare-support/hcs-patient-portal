namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityBulkDeleteResultDto
{
    public int DeletedCount { get; set; }
    public List<Guid> SkippedSlotIds { get; set; } = new();
}
