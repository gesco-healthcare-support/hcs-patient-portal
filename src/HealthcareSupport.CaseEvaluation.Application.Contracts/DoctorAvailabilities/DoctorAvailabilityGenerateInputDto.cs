using HealthcareSupport.CaseEvaluation.Enums;
using System;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityGenerateInputDto
{
    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    public TimeOnly FromTime { get; set; }

    public TimeOnly ToTime { get; set; }

    public BookingStatus BookingStatusId { get; set; }

    public Guid LocationId { get; set; }

    public Guid? AppointmentTypeId { get; set; }

    public int AppointmentDurationMinutes { get; set; } = 15;
}
