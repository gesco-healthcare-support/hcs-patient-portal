using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilitySlotPreviewDto
{
    public DateTime AvailableDate { get; set; }

    public TimeOnly FromTime { get; set; }

    public TimeOnly ToTime { get; set; }

    public BookingStatus BookingStatusId { get; set; }

    public Guid LocationId { get; set; }

    /// <summary>
    /// 2026-05-15 -- the set of AppointmentType ids this preview slot will
    /// accept. Empty list = any type accepted (loose mode).
    /// </summary>
    public List<Guid> AppointmentTypeIds { get; set; } = new();

    /// <summary>
    /// 2026-05-15 -- max simultaneous appointments for this preview slot.
    /// </summary>
    public int Capacity { get; set; } = 3;

    public int TimeId { get; set; }

    public bool IsConflict { get; set; }
}
