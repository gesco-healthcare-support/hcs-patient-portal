using System;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityDeleteByDateInputDto
{
    public Guid LocationId { get; set; }

    public DateTime AvailableDate { get; set; }
}
