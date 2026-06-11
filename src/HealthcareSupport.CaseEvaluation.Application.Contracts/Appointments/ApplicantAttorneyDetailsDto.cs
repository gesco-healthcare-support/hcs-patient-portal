using System;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// DTO for Applicant Attorney details used in appointment add/view.
/// </summary>
public class ApplicantAttorneyDetailsDto
{
    public Guid? ApplicantAttorneyId { get; set; }

    public Guid IdentityUserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? FirmName { get; set; }

    public string? WebAddress { get; set; }

    public string? PhoneNumber { get; set; }

    public string? FaxNumber { get; set; }

    public string? Street { get; set; }

    public string? City { get; set; }

    public Guid? StateId { get; set; }

    public string? ZipCode { get; set; }

    public string? ConcurrencyStamp { get; set; }

    // Paralegal delegate (2026-06-10, Phase 1): an optional paralegal who books /
    // manages this appointment on the attorney's behalf. Collected in the booking
    // attorney section's "Paralegal (you)" sub-block and persisted on the link row;
    // read back so the appointment view can show the delegate. All optional.
    public string? ParalegalEmail { get; set; }

    public string? ParalegalFirstName { get; set; }

    public string? ParalegalLastName { get; set; }
}
