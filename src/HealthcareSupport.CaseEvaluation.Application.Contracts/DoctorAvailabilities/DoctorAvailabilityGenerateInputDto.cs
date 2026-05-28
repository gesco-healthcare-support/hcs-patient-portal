using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityGenerateInputDto
{
    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    public TimeOnly FromTime { get; set; }

    public TimeOnly ToTime { get; set; }

    public BookingStatus BookingStatusId { get; set; }

    public Guid LocationId { get; set; }

    /// <summary>
    /// 2026-05-15 -- the set of AppointmentType ids each generated slot
    /// in this bucket will accept. Empty list = any type accepted (loose
    /// mode).
    /// </summary>
    public List<Guid> AppointmentTypeIds { get; set; } = new();

    /// <summary>
    /// 2026-05-15 -- per-slot capacity applied to every generated slot in
    /// this bucket. Internal staff override per call; default 3.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Capacity { get; set; } = 3;

    public int AppointmentDurationMinutes { get; set; } = 15;
}
