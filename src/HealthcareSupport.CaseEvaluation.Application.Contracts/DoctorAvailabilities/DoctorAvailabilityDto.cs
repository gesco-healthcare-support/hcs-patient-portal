using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public DateTime AvailableDate { get; set; }

    public TimeOnly FromTime { get; set; }

    public TimeOnly ToTime { get; set; }

    public BookingStatus BookingStatusId { get; set; }

    public Guid LocationId { get; set; }

    /// <summary>
    /// 2026-05-15 -- the set of AppointmentType ids this slot accepts.
    /// Empty list means "any type accepted" (loose mode).
    /// </summary>
    public List<Guid> AppointmentTypeIds { get; set; } = new();

    /// <summary>
    /// 2026-05-15 -- max simultaneous appointments this slot can hold.
    /// </summary>
    public int Capacity { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}
