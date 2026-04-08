using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityUpdateDto : IHasConcurrencyStamp
{
    public DateTime AvailableDate { get; set; }

    public TimeOnly FromTime { get; set; }

    public TimeOnly ToTime { get; set; }

    public BookingStatus BookingStatusId { get; set; }

    public Guid LocationId { get; set; }

    public Guid? AppointmentTypeId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}