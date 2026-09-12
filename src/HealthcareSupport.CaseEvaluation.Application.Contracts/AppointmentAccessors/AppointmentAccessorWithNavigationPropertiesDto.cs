using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.Appointments;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

public class AppointmentAccessorWithNavigationPropertiesDto
{
    public AppointmentAccessorDto AppointmentAccessor { get; set; } = null!;
    public IdentityUserDto? IdentityUser { get; set; }
    public AppointmentDto? Appointment { get; set; }

    /// <summary>
    /// QA item 14: the accessor's external role (Patient / Applicant Attorney /
    /// Defense Attorney / Claim Examiner), resolved server-side from the user's
    /// roles. The view-time authorized-users list binds this so the Role column is
    /// always populated -- the older client-side role lookup excluded some roles
    /// and left the cell blank.
    /// </summary>
    public string? UserRoleName { get; set; }
}