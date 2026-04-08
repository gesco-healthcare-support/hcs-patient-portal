using HealthcareSupport.CaseEvaluation.Enums;
using System;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilitySlotPreviewDto
{
    public DateTime AvailableDate { get; set; }

    public TimeOnly FromTime { get; set; }

    public TimeOnly ToTime { get; set; }

    public BookingStatus BookingStatusId { get; set; }

    public Guid LocationId { get; set; }

    public Guid? AppointmentTypeId { get; set; }

    public int TimeId { get; set; }

    public bool IsConflict { get; set; }
}
