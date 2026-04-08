using System;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityDeleteBySlotInputDto
{
    public Guid LocationId { get; set; }

    public DateTime AvailableDate { get; set; }

    public TimeOnly FromTime { get; set; }

    public TimeOnly ToTime { get; set; }
}
