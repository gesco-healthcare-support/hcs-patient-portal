using System;
using System.ComponentModel.DataAnnotations;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 16 (2026-05-04) -- input DTO for an external user submitting
/// a reschedule request on an Approved appointment.
/// </summary>
public class RequestRescheduleDto
{
    /// <summary>
    /// User-picked new slot. Required: OLD's
    /// <c>AppointmentChangeRequestDomain.cs:103-106</c> rejects empty
    /// slots with <c>ProvideNewAppointmentDateTime</c>.
    /// </summary>
    [Required]
    public Guid NewDoctorAvailabilityId { get; set; }

    /// <summary>
    /// Verbatim reason from the user. Required: OLD's
    /// <c>AppointmentChangeRequestDomain.cs:99-102</c> rejects empty
    /// reasons with <c>ProvideRescheduleReason</c>.
    /// </summary>
    [Required]
    [StringLength(AppointmentChangeRequestConsts.ReasonMaxLength)]
    public string ReScheduleReason { get; set; } = null!;

    /// <summary>
    /// Phase 16 (2026-05-04) -- preserved on the entity for the future
    /// admin-override path. External-user submits should pass false;
    /// the Phase 17 supervisor approve flow consumes the field for the
    /// IT Admin "schedule beyond max horizon" override per OLD spec.
    /// </summary>
    public bool IsBeyondLimit { get; set; }
}
