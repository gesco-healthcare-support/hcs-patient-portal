using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityUpdateDto : IHasConcurrencyStamp
{
    public DateTime AvailableDate { get; set; }

    public TimeOnly FromTime { get; set; }

    public TimeOnly ToTime { get; set; }

    public BookingStatus BookingStatusId { get; set; }

    public Guid LocationId { get; set; }

    /// <summary>
    /// 2026-05-15 -- the set of AppointmentType ids this slot accepts.
    /// Empty list means "any type accepted" (loose mode). The manager
    /// syncs the M2M join to match this list on update.
    /// </summary>
    public List<Guid> AppointmentTypeIds { get; set; } = new();

    /// <summary>
    /// 2026-05-15 -- max simultaneous appointments. 2026-10-30: 0 is allowed on an edit and
    /// closes the slot to new requests; creating or generating a slot still needs at least 1.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int Capacity { get; set; } = 3;

    public string ConcurrencyStamp { get; set; } = null!;
}
